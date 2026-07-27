using System.Security.Claims;
using System.Text.Json;
using CES.Business.Constants;
using CES.Business.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CES.API.Authentication
{
    /// <summary>
    /// Bearer validation against Keycloak. Used in place of
    /// <see cref="AuthenticationExtensions.AddCESAuthentication"/> when
    /// <c>Keycloak:Enabled</c> is true.
    /// <para>
    /// The role claims this emits are the same <see cref="RoleConstants"/> strings the mock
    /// token produces, so every existing <c>[Authorize(Roles = …)]</c> attribute is unchanged.
    /// </para>
    /// </summary>
    public static class AuthenticationKeycloakExtensions
    {
        /// <summary>Keycloak client role → CES application role. Unknown roles are ignored.</summary>
        private static readonly IReadOnlyDictionary<string, string> KeycloakRoleMap =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AuthConstants.KeycloakRoleAdmin] = RoleConstants.Admin,
                [AuthConstants.KeycloakRoleUser] = RoleConstants.User,
                [AuthConstants.KeycloakRoleClerk] = RoleConstants.Clerk,
            };

        public static IServiceCollection AddCESKeycloakAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var keycloak = configuration.GetSection("Keycloak").Get<KeycloakConfiguration>()
                ?? throw new InvalidOperationException("Configuration section 'Keycloak' not found.");

            // LoginController resolves ITokenService regardless of mode, so the registration
            // stays. Tokens it mints are signed with the local key and fail validation here —
            // the mock login is inert on the Keycloak path rather than a second door in.
            services.AddScoped<ITokenService, LocalTokenService>();

            var issuer = keycloak.Authority.TrimEnd('/');

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // JWKS is resolved and cached from the discovery document.
                    options.Authority = keycloak.Authority;
                    options.RequireHttpsMetadata = true;
                    // Keep the token's own claim names verbatim. The default (true) runs
                    // inbound claims through a legacy type-mapping table that can move `roles`
                    // and `azp` out from under the names MapKeycloakRoles reads.
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        // Keycloak's default `aud` is `account`; the azp check below is what
                        // stops a token minted for a different realm client (Decision 12).
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        RoleClaimType = ClaimTypes.Role,
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context => MapKeycloakRoles(context, keycloak.Client),
                    };
                });

            return services;
        }

        /// <summary>
        /// Rejects tokens issued to another client, then projects Keycloak client roles onto
        /// the CES role claims the authorization attributes expect.
        /// </summary>
        internal static Task MapKeycloakRoles(TokenValidatedContext context, string clientId)
        {
            var principal = context.Principal;
            var identity = principal?.Identities.FirstOrDefault();

            if (principal is null || identity is null)
            {
                context.Fail("The validated token produced no identity.");
                return Task.CompletedTask;
            }

            if (!IsAuthorizedParty(principal, clientId))
            {
                context.Fail("The token was issued to a different client.");
                return Task.CompletedTask;
            }

            var roles = MapRoles(principal, clientId);

            if (roles.Count == 0)
            {
                // Trusted token, but nothing mapped — the account has no CES roles, or the
                // role claim is not where we look. Log the claim *types* present (never the
                // values: those include name/email) so a shape mismatch is diagnosable.
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<JwtBearerEvents>>();
                logger.LogWarning(
                    "Keycloak token accepted (azp ok) but produced no CES roles. Claim types present: {ClaimTypes}",
                    string.Join(", ", principal.Claims.Select(claim => claim.Type).Distinct()));
            }

            foreach (var role in roles)
                identity.AddClaim(new Claim(ClaimTypes.Role, role));

            return Task.CompletedTask;
        }

        /// <summary>
        /// With audience validation off, <c>azp</c> is the trust boundary: it names the client
        /// the token was minted for, and only ours is accepted.
        /// </summary>
        internal static bool IsAuthorizedParty(ClaimsPrincipal principal, string clientId) =>
            string.Equals(
                principal.FindFirst(AuthConstants.AuthorizedPartyClaim)?.Value,
                clientId,
                StringComparison.Ordinal);

        /// <summary>
        /// Maps Keycloak client roles to CES roles, de-duplicated. Unknown roles are dropped
        /// rather than surfaced, so a role added in Keycloak grants nothing here until it is
        /// mapped deliberately.
        /// </summary>
        internal static IReadOnlyList<string> MapRoles(ClaimsPrincipal principal, string clientId) =>
            ReadKeycloakRoles(principal, clientId)
                .Where(KeycloakRoleMap.ContainsKey)
                .Select(role => KeycloakRoleMap[role])
                .Distinct(StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// Reads the flat top-level <c>roles</c> claim a protocol mapper bubbles up, falling
        /// back to <c>resource_access.&lt;clientId&gt;.roles</c> so that mapper is not a hard
        /// dependency (Decision 14).
        /// </summary>
        private static IEnumerable<string> ReadKeycloakRoles(ClaimsPrincipal principal, string clientId)
        {
            var flatRoles = principal
                .FindAll(AuthConstants.RolesClaim)
                .SelectMany(claim => ExpandRoleClaim(claim.Value))
                .ToList();

            return flatRoles.Count > 0
                ? flatRoles
                : ReadResourceAccessRoles(principal, clientId);
        }

        /// <summary>
        /// One "roles" claim can arrive either as a bare role name or, depending on how the
        /// JWT handler materialises a JSON array, as the literal array string
        /// <c>["ces-user","ces-judicial"]</c>. This normalises both to individual role names.
        /// </summary>
        private static IEnumerable<string> ExpandRoleClaim(string value)
        {
            var trimmed = value.TrimStart();
            if (!trimmed.StartsWith('['))
                return [value];

            try
            {
                using var document = JsonDocument.Parse(value);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return [value];

                return document.RootElement
                    .EnumerateArray()
                    .Where(element => element.ValueKind == JsonValueKind.String)
                    .Select(element => element.GetString()!)
                    .ToList();
            }
            catch (JsonException)
            {
                // Not actually JSON despite the leading bracket — treat it as one opaque role.
                return [value];
            }
        }

        private static IEnumerable<string> ReadResourceAccessRoles(
            ClaimsPrincipal principal, string clientId)
        {
            var resourceAccess = principal.FindFirst(AuthConstants.ResourceAccessClaim)?.Value;
            if (string.IsNullOrWhiteSpace(resourceAccess))
                return [];

            try
            {
                using var document = JsonDocument.Parse(resourceAccess);

                if (!document.RootElement.TryGetProperty(clientId, out var client) ||
                    !client.TryGetProperty(AuthConstants.RolesClaim, out var roles) ||
                    roles.ValueKind != JsonValueKind.Array)
                {
                    return [];
                }

                return roles
                    .EnumerateArray()
                    .Where(role => role.ValueKind == JsonValueKind.String)
                    .Select(role => role.GetString()!)
                    .ToList();
            }
            catch (JsonException)
            {
                // A malformed resource_access claim grants no roles rather than failing the
                // request outright — the flat mapper is the expected source anyway.
                return [];
            }
        }
    }
}
