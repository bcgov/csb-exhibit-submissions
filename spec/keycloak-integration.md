# Keycloak Integration

**Status:** Draft  
**Date:** 2026-05-29  

---

## Overview

The current authentication system uses a self-issued JWT with mock credentials hardcoded in `LoginController`. This spec describes replacing that with BC Gov's Keycloak SSO, using IDIR as the identity provider. Once complete, users will authenticate through their government IDIR account rather than a local username/password form.

The existing `AuthenticationKeycloakExtensions.cs` contains commented-out scaffolding copied from [bcgov/jasper](https://github.com/bcgov/jasper) — a related BC Gov court-services project. That code is not usable as-is for CES (it is heavily coupled to Jasper's MongoDB user store, PCSS judge service, and SiteMinder), but Jasper's authentication architecture is the primary reference for this implementation. The patterns in this spec are derived from Jasper's working implementation, simplified to CES's requirements.

---

## Reference Implementation

**Repository:** https://github.com/bcgov/jasper  
**Relevant files:**
- `api/Infrastructure/Authentication/AuthenticationServiceCollectionExtension.cs` — full OIDC/Cookie/JwtBearer setup
- `api/Infrastructure/Authorization/AuthorizationServiceCollectionExtension.cs` — policy/role handlers
- `api/Controllers/AuthController.cs` — login, logout, and user-info endpoints
- `web/src/services/AuthService.ts` — frontend 401 → backend login redirect flow  
- `web/src/services/RedirectHandlerService.ts` — centralized redirect-to-login helper

Jasper uses Option B (backend-driven token relay, described below) with no OIDC library on the frontend. CES should follow the same pattern.

---

## User Stories

1. As an officer, I am redirected to the BC Gov Keycloak login page when I access the application unauthenticated, so that I sign in with my IDIR account.
2. As an officer, I am redirected back to the application after a successful login and can use the app without further authentication steps.
3. As an officer, clicking Logout ends my session in both the application and Keycloak.
4. As an admin, I have access to admin routes when my IDIR account carries the `ces-admin` Keycloak realm role.
5. As a developer, I can run the application locally with a mock login when Keycloak is not configured, so that development does not require a Keycloak client.

---

## Scope

| Area | In Scope |
|---|---|
| Frontend — OIDC login/logout redirect flow | Yes |
| Frontend — User info from API claims endpoint | Yes |
| Frontend — Router auth guards | Yes |
| Frontend — Remove username/password login form | Yes |
| Backend — OIDC + Cookie authentication middleware | Yes |
| Backend — Role claim extraction from Keycloak token | Yes |
| Backend — Auth controller (login, logout, user-info endpoints) | Yes |
| Backend — Remove mock users and LocalTokenService | Yes (guarded by dev flag) |
| Backend — Environment configuration | Yes |
| Dev bypass mode (mock login behind env flag) | Yes |
| Keycloak realm/client provisioning | No — handled by the SSO team |
| User management in Keycloak | No — IDIR manages identity |
| Docker/deploy pipeline config | Noted — values required, setup outside this spec |
| SiteMinder / legacy BC Gov proxy auth | No — not applicable to CES |

---

## Decisions Made

| Decision | Choice | Rationale |
|---|---|---|
| Identity provider | IDIR only | Government employees only; no BCeID or business accounts |
| Authentication flow | Option B — backend-driven token relay | Matches bcgov/jasper pattern; proven in production on a sister project |
| Role strategy | Keycloak realm roles | Roles live in the realm, not in a DB; simpler for an internal tool |
| Role names | `ces-admin`, `ces-user` | Namespaced to avoid collision with other clients on the same realm |
| IDP hint | `kc_idp_hint=idir` | Forces login directly to IDIR without showing an IDP selector screen |
| Dev bypass | Env-flag-controlled mock login | Allows development without a Keycloak client; matches existing pattern |

---

## OIDC Flow Strategy

### Token Relay via API (Server-driven) ✓ Selected

The ASP.NET API acts as the confidential OIDC client. The browser is redirected to Keycloak by a backend endpoint; the API handles the token exchange. The access token and ID token are **never stored** — only the refresh token is kept in an HttpOnly, Secure, SameSite=None cookie on the server side. The frontend operates via this cookie and never sees raw tokens. User info is obtained by calling a backend `GET /api/auth/info` endpoint that reads from claims.

This is exactly the pattern used in **bcgov/jasper** (see `AuthenticationServiceCollectionExtension.cs`). The commented-out code already in `AuthenticationKeycloakExtensions.cs` is a direct copy of this pattern.

**Pros:**
- Tokens are never exposed to JavaScript — eliminates XSS token-theft risk
- No OIDC library required on the frontend
- Token refresh is handled server-side in the cookie validation hook
- Proven pattern: bcgov/jasper uses this in production
- Simpler frontend — mirrors what the existing `AuthService.ts` comment describes

**Cons:**
- API is not fully stateless (cookie-backed sessions)
- Confidential client requires a client secret to be managed as a secret

---

## Required Configuration Values

The following must be provided by the SSO/infrastructure team. Values are environment-specific (dev, test, prod).

| Value | Description | Config key |
|---|---|---|
| Keycloak Authority URL | Base URL of the realm, e.g. `https://dev.loginproxy.gov.bc.ca/auth/realms/standard` | `Keycloak__Authority` |
| Client ID | The OIDC confidential client ID registered in the realm | `Keycloak__Client` |
| Client Secret | The OIDC client secret (confidential client — Option B requires this) | `Keycloak__Secret` |
| Audience | The expected `aud` claim value in access tokens | `Keycloak__Audience` |
| Token Refresh Threshold | How far before expiry to refresh (e.g. `"00:01:00"` = 1 minute) | `TokenRefreshThreshold` |

> **Realm:** Jasper connects to `https://common-sso.justice.gov.bc.ca/auth/realms/Judiciary`. CES will likely use the same Justice Keycloak realm since it is a court-adjacent system — confirm with the SSO/infrastructure team.

All config values are passed to the API container as environment variables using ASP.NET's double-underscore binding convention: `Keycloak__Authority`, `Keycloak__Client`, `Keycloak__Secret`, `Keycloak__Audience`.

---

## IDIR Token Claims

When a user authenticates via IDIR, Keycloak includes these claims in the token. CES should use `idir_user_guid` as the stable user identifier for any audit records.

| Claim | Description | Example |
|---|---|---|
| `sub` | Keycloak subject (internal ID) | `abc123@idir` |
| `idir_user_guid` | Stable IDIR GUID — use this as the app's user key | `A1B2C3D4E5F6...` |
| `idir_userid` | IDIR username | `JSMITH` |
| `preferred_username` | Always ends with `@idir` for IDIR users | `jsmith@idir` |
| `display_name` | Full display name | `Smith, John CITZ:EX` |
| `email` | Work email | `john.smith@gov.bc.ca` |
| `realm_access.roles` | Array of realm roles assigned to the user | `["ces-admin", "default-roles-standard"]` |
| `groups` | Keycloak group memberships (requires `groups` scope) | `["/ces-admins"]` |

---

## Backend Changes

### 1. Authentication Middleware (`AuthenticationExtensions.cs`)

Replace the current symmetric-key JWT setup entirely. Following Jasper's pattern, register three schemes:

```csharp
public static IServiceCollection AddCESAuthentication(
    this IServiceCollection services,
    IConfiguration configuration,
    IWebHostEnvironment env)
{
    var authority = configuration.GetValue<string>("Keycloak:Authority")!;
    var clientId  = configuration.GetValue<string>("Keycloak:Client")!;
    var secret    = configuration.GetValue<string>("Keycloak:Secret")!;
    var audience  = configuration.GetValue<string>("Keycloak:Audience")!;

    services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        var cookieName = "CES";
        if (env.IsDevelopment()) cookieName += ".Development";

        options.Cookie.Name = cookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return ctx.Response.CompleteAsync();
            },
            OnValidatePrincipal = async cookieCtx =>
            {
                // Server-side token refresh before expiry
                var accessTokenExpiration = DateTimeOffset.Parse(
                    cookieCtx.Properties.GetTokenValue("expires_at")!,
                    CultureInfo.InvariantCulture);
                var timeRemaining = accessTokenExpiration.Subtract(DateTimeOffset.UtcNow);
                var threshold = TimeSpan.Parse(
                    configuration.GetValue<string>("TokenRefreshThreshold") ?? "00:01:00",
                    CultureInfo.InvariantCulture);

                if (timeRemaining > threshold) return;

                var refreshToken = cookieCtx.Properties.GetTokenValue("refresh_token");
                var httpClientFactory = cookieCtx.HttpContext.RequestServices
                    .GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient();
                var response = await httpClient.RequestRefreshTokenAsync(new RefreshTokenRequest
                {
                    Address = $"{authority}/protocol/openid-connect/token",
                    ClientId = clientId,
                    ClientSecret = secret,
                    RefreshToken = refreshToken
                });

                if (response.IsError)
                {
                    cookieCtx.RejectPrincipal();
                    await cookieCtx.HttpContext.SignOutAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme);
                }
                else
                {
                    var expiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
                    cookieCtx.Properties.UpdateTokenValue("expires_at",
                        expiresAt.ToString(CultureInfo.InvariantCulture));
                    cookieCtx.Properties.UpdateTokenValue("refresh_token", response.RefreshToken);
                    cookieCtx.ShouldRenew = true;
                }
            }
        };
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Authority = authority;
        options.ClientId = clientId;
        options.ClientSecret = secret;
        options.RequireHttpsMetadata = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.CallbackPath = "/api/auth/signin-oidc";
        options.Scope.Add("groups");
        options.Events = new OpenIdConnectEvents
        {
            OnTicketReceived = ctx =>
            {
                // Strip access_token and id_token from the cookie — only keep refresh_token.
                // Prevents long-lived tokens from sitting in the browser.
                ctx.Properties.Items.Remove(".Token.id_token");
                ctx.Properties.Items.Remove(".Token.access_token");
                ctx.Properties.Items[".TokenNames"] = "refresh_token;token_type;expires_at";
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                if (ctx.Principal?.Identity is not ClaimsIdentity identity)
                    return Task.CompletedTask;

                // Map Keycloak realm roles to app roles
                var realmRolesClaim = ctx.Principal.FindFirst("realm_access")?.Value;
                if (realmRolesClaim != null)
                {
                    var realmAccess = JsonSerializer.Deserialize<JsonElement>(realmRolesClaim);
                    if (realmAccess.TryGetProperty("roles", out var rolesElement))
                    {
                        foreach (var role in rolesElement.EnumerateArray()
                            .Select(r => r.GetString()).OfType<string>())
                        {
                            var appRole = role switch
                            {
                                "ces-admin" => "Admin",
                                "ces-user"  => "User",
                                _ => null
                            };
                            if (appRole != null)
                                identity.AddClaim(new Claim(ClaimTypes.Role, appRole));
                        }
                    }
                }
                return Task.CompletedTask;
            },
            OnRedirectToIdentityProvider = ctx =>
            {
                // Always force IDIR — skip the Keycloak IDP selector screen
                ctx.ProtocolMessage.SetParameter("kc_idp_hint", "idir");

                // Support reverse proxy (OpenShift / nginx) redirect URI rewriting
                if (ctx.HttpContext.Request.Headers.TryGetValue("X-Forwarded-Host", out var host))
                {
                    var port = ctx.HttpContext.Request.Headers["X-Forwarded-Port"].ToString();
                    var baseHref = ctx.HttpContext.Request.Headers["X-Base-Href"].ToString();
                    ctx.ProtocolMessage.RedirectUri =
                        $"https://{host}{(string.IsNullOrEmpty(port) ? "" : $":{port}")}{baseHref}{options.CallbackPath}";
                }
                return Task.CompletedTask;
            }
        };
    });

    return services;
}
```

**NuGet packages required:**
- `Microsoft.AspNetCore.Authentication.OpenIdConnect`
- `IdentityModel` (for `RequestRefreshTokenAsync`)

---

### 2. Auth Controller (`AuthController.cs`)

Replace the existing `LoginController` with a new controller that handles the three auth endpoints the frontend needs.

```csharp
[ApiController]
public class AuthController : ControllerBase
{
    // Triggers the OIDC challenge → redirects browser to Keycloak
    [HttpGet("api/auth/login")]
    [Authorize(AuthenticationSchemes = OpenIdConnectDefaults.AuthenticationScheme)]
    public IActionResult Login([FromQuery] string redirectUri = "/")
    {
        return Redirect(redirectUri);
    }

    // Signs out of cookie + Keycloak
    [HttpGet("api/auth/logout")]
    public async Task<IActionResult> Logout([FromQuery] string redirectUri = "/")
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
            new AuthenticationProperties { RedirectUri = redirectUri });
        return NoContent();
    }

    // Returns user info from claims — replaces the frontend's jwtDecode pattern
    [HttpGet("api/auth/info")]
    [Authorize]
    public IActionResult GetUserInfo()
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return Ok(new
        {
            idirUserGuid  = User.FindFirst("idir_user_guid")?.Value,
            idirUserId    = User.FindFirst("idir_userid")?.Value,
            displayName   = User.FindFirst("display_name")?.Value,
            email         = User.FindFirst(ClaimTypes.Email)?.Value,
            roles,
            isAdmin       = roles.Contains("Admin"),
        });
    }
}
```

---

### 3. New Configuration Class

Add `api/CES.API/configuration/KeycloakConfiguration.cs`:

```csharp
namespace CES.API.Configuration
{
    public class KeycloakConfiguration
    {
        public string Authority { get; set; } = string.Empty;
        public string Client    { get; set; } = string.Empty;
        public string Secret    { get; set; } = string.Empty;
        public string Audience  { get; set; } = string.Empty;
    }
}
```

Add to `appsettings.json` (empty values — overridden via environment variables in each environment):

```json
"Keycloak": {
  "Authority": "",
  "Client": "",
  "Secret": "",
  "Audience": ""
},
"TokenRefreshThreshold": "00:01:00"
```

---

### 4. Remove / Retire Login Infrastructure

| File / Class | Disposition |
|---|---|
| `LoginController.cs` | Delete — replaced by `AuthController` |
| `LocalTokenService.cs` | Keep for dev bypass only (see Dev Bypass section) |
| `ITokenService.cs` | Keep for dev bypass; delete if dev bypass is eventually removed |
| `AuthConfiguration.cs` (`UserAuth` section) | Delete — replaced by `KeycloakConfiguration` |
| `AuthenticationKeycloakExtensions.cs` | Delete — entirely superseded by the new `AuthenticationExtensions.cs` |

---

### 5. `Program.cs` Changes

```csharp
// Before
builder.Services.AddCESAuthentication(builder.Configuration);

// After
if (builder.Configuration.GetValue<bool>("Keycloak:Enabled"))
    builder.Services.AddCESAuthentication(builder.Configuration, builder.Environment);
else
    builder.Services.AddCESDevAuthentication(builder.Configuration); // dev bypass path
```

Also add `app.UseAuthentication()` and `app.UseAuthorization()` in the middleware pipeline if not already present (they are required for cookie + OIDC to work).

---

## Frontend Changes

### 1. Login / Logout Flow

The frontend no longer manages tokens. The entire authentication flow becomes browser redirects to backend endpoints — matching exactly the approach in bcgov/jasper's `RedirectHandlerService`.

**`AuthService.ts`** — replace the current implementations:

```typescript
const login = (redirectUri: string = window.location.href) => {
  window.location.replace(`/api/auth/login?redirectUri=${encodeURIComponent(redirectUri)}`)
}

const logout = () => {
  window.location.replace('/api/auth/logout?redirectUri=/')
}

const handleUnauthorized = (currentPath?: string) => {
  const authStore = useAuthStore()
  authStore.clearAuth()
  login(currentPath ?? '/')
  // This replaces the router.push({ name: 'Login' }) that is currently here.
  // The comment in the current AuthService.ts anticipates exactly this change.
}
```

No OIDC library is needed. No callback route or token handling is needed on the frontend.

---

### 2. AuthStore (`authStore.ts`)

Replace the JWT-decode-based store with an API call to `GET /api/auth/info`.

```typescript
export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const roles = ref<string[]>([])
  const isAuthenticated = computed(() => !!user.value)
  const hasRole = (role: string) => roles.value.includes(role)

  async function loadUser() {
    try {
      const response = await api.get('/auth/info')
      user.value = {
        id:          response.data.idirUserGuid,
        email:       response.data.email,
        displayName: response.data.displayName,
        roles:       response.data.roles,
      }
      roles.value = response.data.roles
    } catch {
      clearAuth()
    }
  }

  function clearAuth() {
    user.value = null
    roles.value = []
  }

  return { user, roles, isAuthenticated, hasRole, loadUser, clearAuth }
})
```

Key changes:
- Remove `token` ref — the session cookie is managed by the browser automatically
- Remove `localStorage` usage — no token to store client-side
- Remove `jwtDecode` — user info comes from the API
- Remove the `jwtDecode` / `jwt-decode` npm dependency
- `loadUser()` is called on app startup (`App.vue` or `main.ts`) and after any navigation that requires auth

---

### 3. Axios Interceptor (`apiClient.ts`)

The `Authorization: Bearer ...` header is removed. The browser sends the `CES` session cookie automatically on every request to the same origin.

Update the response interceptor to call `handleUnauthorized` on 401 (this already exists; ensure it calls the new `login()` redirect rather than `router.push`):

```typescript
api.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      useAuthService().handleUnauthorized(window.location.pathname)
    }
    return Promise.reject(error)
  }
)
```

---

### 4. Router Guards

The existing `router.beforeEach` guard logic — checking `authStore.isAuthenticated` and `authStore.roles` — does not need structural changes. `isAuthenticated` is now derived from whether `user.value` is populated after `loadUser()`.

Remove the `Login` route from the router unless dev bypass is active:

```typescript
if (import.meta.env.VITE_DEV_AUTH_BYPASS === 'true') {
  router.addRoute({ path: '/login', name: 'Login', component: LoginView })
}
```

---

### 5. Environment Variables

Remove all `VITE_KEYCLOAK_*` variables (no frontend OIDC config needed). Add only:

```
VITE_DEV_AUTH_BYPASS=false
```

---

## Dev Bypass Mode

When Keycloak is not configured (local development without SSO access), the application falls back to the existing mock login. Jasper does not include a local Keycloak container; CES should follow the same approach — dev bypass mode stands in for it.

**Backend** — `Program.cs` checks `Keycloak:Enabled`:
```csharp
if (builder.Configuration.GetValue<bool>("Keycloak:Enabled"))
    builder.Services.AddCESAuthentication(builder.Configuration, builder.Environment);
else
    builder.Services.AddCESDevAuthentication(builder.Configuration); // existing local JWT path
```
`AddCESDevAuthentication` is a thin wrapper that keeps the current `LocalTokenService` + symmetric-key JWT behavior intact.

**Frontend** — `VITE_DEV_AUTH_BYPASS=true` keeps the `/login` route active and preserves the current `POST /api/auth/login` flow in `AuthService`.

The mock users (`admin@gov.bc.ca` / `officer@gov.bc.ca`) remain available only in bypass mode.

Docker's `.env.template` sets both flags to bypass mode by default so the project runs out of the box without a Keycloak client.

---

## Keycloak Realm Setup Requirements

The following must be configured in the target Keycloak realm by the SSO team before integration testing can begin.

1. **OIDC client** registered as a **confidential** client with:
   - `Standard flow` enabled
   - `Direct access grants` disabled
   - Valid redirect URIs per environment (e.g. `https://app.example.gov.bc.ca/api/auth/signin-oidc`)
   - Valid post-logout redirect URIs per environment
   - A client secret generated and securely provided to the deployment team

2. **Realm roles** created:
   - `ces-admin`
   - `ces-user`

3. **`groups` scope** enabled on the client so group membership is included in the token.

4. **Audience mapper** (likely required): a protocol mapper that adds the client ID to the `aud` claim so the API audience validation passes.

5. **Test IDIR accounts** assigned to `ces-admin` and `ces-user` roles for integration testing.

> **Note:** The callback path registered in Keycloak must be `/api/auth/signin-oidc` — this is where ASP.NET's OpenIdConnect middleware listens. It is not a Vue route.

---

## Open Questions / Follow-up Items

1. **Keycloak realm:** Confirm whether CES will use the Justice realm (`https://common-sso.justice.gov.bc.ca/auth/realms/Judiciary`) — the same one used by Jasper — or a different BC Gov SSO realm. This affects the Authority URL and the client registration process.

2. **Audit trail:** The current system identifies users by email (`admin@gov.bc.ca`). After Keycloak, `idir_user_guid` is the stable identifier. Confirm whether existing audit/submission records need a migration or can tolerate a format change at cutover.

3. **Session timeout UX:** When the server-side cookie refresh fails (Keycloak session has expired), the next API call returns 401 and the user is redirected to Keycloak. Define whether a "your session has expired" interstitial is needed or if a silent redirect is acceptable.

4. **Cookie `SameSite=None`:** Required for cross-origin iframe scenarios (if any). If the app is served from the same origin as the API in all environments, `SameSite=Strict` is more secure. Confirm deployment topology.

5. **Multi-tab logout:** If the user logs out in one tab, other open tabs will get 401s on their next API call and redirect to Keycloak automatically (following the Jasper pattern). Confirm this is acceptable rather than coordinating logout across tabs explicitly.

6. **`X-Forwarded-*` headers:** The `OnRedirectToIdentityProvider` handler rewrites the redirect URI when `X-Forwarded-Host` is present. Confirm that the nginx/OpenShift ingress layer forwards these headers and that `ForwardedHeaders` middleware is configured in `Program.cs`.
