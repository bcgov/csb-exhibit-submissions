# Keycloak Integration

**Status:** Superseded — see [keycloak-simplified.md](keycloak-simplified.md)
**Date:** 2026-05-29

> This spec was written against bcgov/jasper's backend-driven token relay pattern (confidential client, client secret, cookie-backed session, server-side refresh). That turned out to be more than CES needs. [keycloak-simplified.md](keycloak-simplified.md) replaces it with a frontend-driven public-client flow (PKCE, no secret) and is the current plan. Kept here for historical reference only — do not implement from this file.

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
4. As an admin, I have access to admin routes when my IDIR account carries the `ces-judicial` Keycloak client role.
5. As a developer, running `./docker/manage debug` (or any local dev environment) gives me a mock login by default, so that day-to-day development work never requires a Keycloak client or touches real IDIR/SSO.

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
| Role strategy | Keycloak **client** roles (on the CES confidential client), not realm roles | Scoped to the CES client rather than shared realm-wide; avoids collision with other clients on the same realm |
| Role names | `ces-user`, `ces-judicial`, `ces-clerk` | Three roles: officer (submit), judicial/JJ (exhibit search), clerk (registry triage/review) |
| IDP hint | `kc_idp_hint=idir` | Forces login directly to IDIR without showing an IDP selector screen |
| Dev bypass | Env-flag-controlled mock login, unchanged from today's implementation | Allows development without a Keycloak client; deliberately does **not** attempt to replicate Keycloak's token/claim shape — simplicity for local dev matters more than fidelity |

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
| Keycloak Authority URL | Base URL of the realm | `Keycloak__Authority` |
| Client ID | The OIDC confidential client ID registered in the realm | `Keycloak__Client` |
| Client Secret | The OIDC client secret (confidential client — Option B requires this) | `Keycloak__Secret` |
| Token Refresh Threshold | How far before expiry to refresh (e.g. `"00:01:00"` = 1 minute) | `TokenRefreshThreshold` |

> **Audience deliberately omitted:** CES's Option B flow has no `JwtBearer` scheme validating incoming API tokens — the API is the OIDC client, not a resource server — so there's no `aud` validation in code for a `Keycloak:Audience` value to satisfy. Don't request an audience mapper from the SSO team; only revisit this if CES later adds a service that independently validates bearer tokens.

> **Realm — confirmed for dev, values not recorded here:** the dev Authority URL and Client ID have been confirmed with the SSO team; they're intentionally not written into this spec (see [keycloak-simplified.md](keycloak-simplified.md) for where they actually live — `docker/.env` / `web/.env.local`, both gitignored). This is a shared BC Gov SSO realm, distinct from both the generic `standard` realm and Jasper's `Judiciary` realm. **`Keycloak__Secret` is still outstanding** — request it from the SSO team for the dev client. **Test/prod values are not yet confirmed** — whether the client ID/secret differ per environment needs confirming before those environments can be wired up.

All config values are passed to the API container as environment variables using ASP.NET's double-underscore binding convention: `Keycloak__Authority`, `Keycloak__Client`, `Keycloak__Secret`.

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
| `resource_access.<clientId>.roles` | Array of **client** roles assigned to the user on the CES OIDC client | `["ces-judicial"]` |
| `groups` | Keycloak group memberships — **not currently consumed** by CES (role mapping uses `resource_access.<clientId>.roles` only); don't request the `groups` scope unless a future feature needs group membership | `["/ces-admins"]` |

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

                // Map Keycloak client roles (on the CES client) to app roles.
                // Client roles live under resource_access.<clientId>.roles, not
                // realm_access.roles — CES roles are scoped to the CES client, not realm-wide.
                var resourceAccessClaim = ctx.Principal.FindFirst("resource_access")?.Value;
                if (resourceAccessClaim != null)
                {
                    var resourceAccess = JsonSerializer.Deserialize<JsonElement>(resourceAccessClaim);
                    if (resourceAccess.TryGetProperty(clientId, out var clientAccess)
                        && clientAccess.TryGetProperty("roles", out var rolesElement))
                    {
                        foreach (var role in rolesElement.EnumerateArray()
                            .Select(r => r.GetString()).OfType<string>())
                        {
                            var appRole = role switch
                            {
                                "ces-judicial" => "Admin",
                                "ces-user"     => "User",
                                "ces-clerk"    => "Clerk",
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

**Logout follows Jasper's pattern** (manual Keycloak end-session URL, not the framework's `SignOutAsync(OpenIdConnectDefaults...)`) — see Open Question 7. `OnTicketReceived` strips `id_token` from the cookie, so there's no `id_token_hint` available for the framework's built-in sign-out to pass to Keycloak; Jasper's `AuthController.Logout` avoids that gap entirely by building the end-session URL itself. This requires injecting `IConfiguration` into the controller.

```csharp
[ApiController]
public class AuthController(IConfiguration configuration) : ControllerBase
{
    // Triggers the OIDC challenge → redirects browser to Keycloak
    [HttpGet("api/auth/login")]
    [Authorize(AuthenticationSchemes = OpenIdConnectDefaults.AuthenticationScheme)]
    public IActionResult Login([FromQuery] string redirectUri = "/")
    {
        return Redirect(redirectUri);
    }

    // Signs out of the cookie, then hands the browser to Keycloak's own end-session
    // endpoint (built manually — see Open Question 7 for why this doesn't use
    // SignOutAsync(OpenIdConnectDefaults...)).
    [HttpGet("api/auth/logout")]
    public async Task<IActionResult> Logout([FromQuery] string redirectUri = "/")
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var authority = configuration.GetValue<string>("Keycloak:Authority");

        var forwardedHost = Request.Headers.TryGetValue("X-Forwarded-Host", out var host)
            ? host.ToString()
            : Request.Host.ToString();
        var forwardedPort = Request.Headers["X-Forwarded-Port"].ToString();
        var baseHref = Request.Headers["X-Base-Href"].ToString();
        var appReturnUrl = $"https://{forwardedHost}{(string.IsNullOrEmpty(forwardedPort) ? "" : $":{forwardedPort}")}{baseHref}{redirectUri}";

        var keycloakLogoutUrl = $"{authority}/protocol/openid-connect/logout" +
            $"?post_logout_redirect_uri={Uri.EscapeDataString(appReturnUrl)}";

        return Redirect(keycloakLogoutUrl);
    }

    // Returns user info from claims — replaces the frontend's jwtDecode pattern.
    // Only called on the real Keycloak path; dev bypass decodes its mock JWT client-side instead.
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
    }
}
```

Add to `appsettings.json` (empty values — overridden via environment variables in each environment):

```json
"Keycloak": {
  "Authority": "",
  "Client": "",
  "Secret": ""
},
"TokenRefreshThreshold": "00:01:00"
```

---

### 4. Login Infrastructure — What to Keep vs. Remove

| File / Class | Disposition |
|---|---|
| `LoginController.cs` | **Keep, guarded by dev bypass** — this is the mock-login endpoint (`POST /api/auth/login`) that Dev Bypass Mode relies on. Do not delete while dev bypass exists; only remove it if dev bypass is retired entirely. `AuthController` is additive (new real-Keycloak endpoints), not a replacement for this controller |
| `LocalTokenService.cs` | Keep for dev bypass only (see Dev Bypass section) |
| `ITokenService.cs` | Keep for dev bypass; delete if dev bypass is eventually removed |
| `AuthConfiguration.cs` (`UserAuth` section) | **Keep** — `LocalTokenService.GenerateToken()` binds directly to this class (`_config.GetSection("UserAuth").Get<AuthConfiguration>()`). Deleting it breaks dev bypass at compile time. Add `KeycloakConfiguration` alongside it; don't replace it |
| `AuthenticationKeycloakExtensions.cs` | Delete — entirely superseded by the new `AuthenticationExtensions.cs` |

---

### 5. `Program.cs` Changes

**Rename step (do this first):** today's `AddCESAuthentication(IServiceCollection, IConfiguration)` in `AuthenticationExtensions.cs` — the existing symmetric-key JWT method — must be renamed to `AddCESDevAuthentication(IServiceCollection, IConfiguration)`, with no other behavior change. This frees up the `AddCESAuthentication` name for the new Keycloak method below (which takes an extra `IWebHostEnvironment` parameter) and gives the dev-bypass branch an explicit, already-implemented method rather than one that needs to be built from scratch.

```csharp
// Before
builder.Services.AddCESAuthentication(builder.Configuration);

// After
if (builder.Configuration.GetValue<bool>("Keycloak:Enabled"))
    builder.Services.AddCESAuthentication(builder.Configuration, builder.Environment);
else
    builder.Services.AddCESDevAuthentication(builder.Configuration); // renamed from today's AddCESAuthentication — zero behavior change
```

`Keycloak:Enabled` should be **absent (or explicitly `false`)** in `docker/.env.template`, so the dev-bypass branch is the out-of-the-box default for `./docker/manage debug` with no configuration required.

`app.UseAuthentication()` and `app.UseAuthorization()` are already present and unconditional in `Program.cs` today — no change needed there. Both the dev-bypass and Keycloak schemes rely on the same two calls; only the registered scheme differs.

---

## Frontend Changes

### 1. Login / Logout Flow

**This section applies only when `VITE_DEV_AUTH_BYPASS=false` (real Keycloak).** When the flag is `true`, `AuthService.ts` keeps its current `POST /api/auth/login` mock-login implementation completely unchanged — see Dev Bypass Mode below. Add the Keycloak path alongside the existing bypass code; don't delete or restructure it.

For the real path, the frontend no longer manages tokens. The entire authentication flow becomes browser redirects to backend endpoints — matching exactly the approach in bcgov/jasper's `RedirectHandlerService`.

**`AuthService.ts`** — add these alongside the existing bypass-mode functions:

```typescript
const loginViaKeycloak = (redirectUri: string = window.location.href) => {
  window.location.replace(`/api/auth/login?redirectUri=${encodeURIComponent(redirectUri)}`)
}

const logoutViaKeycloak = () => {
  window.location.replace('/api/auth/logout?redirectUri=/')
}

const handleUnauthorized = (currentPath?: string) => {
  const authStore = useAuthStore()
  authStore.clearAuth()
  if (import.meta.env.VITE_DEV_AUTH_BYPASS === 'true') {
    router.push({ name: 'Login', query: { redirect: currentPath } }) // existing bypass behavior, unchanged
  } else {
    loginViaKeycloak(currentPath ?? '/')
  }
}
```

No OIDC library is needed. No callback route or token handling is needed on the frontend for the Keycloak path.

---

### 2. AuthStore (`authStore.ts`)

Both paths must populate the same `user` / `roles` shape (`Admin` / `User`) so the router guard (`meta.roles: ['Admin']` / `['User']`) and every other consumer of the store work unchanged regardless of which path is active. The Keycloak-side role mapping in `OnTokenValidated` already produces these same strings for exactly this reason.

**Dev bypass (`VITE_DEV_AUTH_BYPASS=true`, the default):** keep today's implementation exactly as-is — `localStorage` token, `jwtDecode`, `setToken`, `isTokenExpired`. Don't touch this branch. This is the "simple, doesn't replicate Keycloak" path: it's a plain JWT the API already knows how to mint via `LocalTokenService`.

**Keycloak path (`VITE_DEV_AUTH_BYPASS=false`):** add a `loadUser()` that calls the new claims endpoint instead of decoding a token:

```typescript
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
```

`loadUser()` is called on app startup (`App.vue` or `main.ts`) only when `VITE_DEV_AUTH_BYPASS !== 'true'`; in bypass mode, startup continues to call the existing token-decode logic instead.

Do **not** remove `token`, `localStorage`, or `jwtDecode` from the store while dev bypass exists — they're still load-bearing for that path. Only drop them (and the `jwt-decode` npm dependency) if dev bypass is retired entirely.

---

### 3. Axios Interceptor (`apiClient.ts`)

**Dev bypass:** the `Authorization: Bearer ${authStore.token}` request interceptor stays exactly as it is today — the mock JWT is still sent as a bearer token, since `AddCESDevAuthentication` is still a `JwtBearer` scheme.

**Keycloak path:** no `Authorization` header is added; the browser sends the `CES` session cookie automatically on every request to the same origin. Gate the existing request interceptor on the flag rather than removing it:

```typescript
api.interceptors.request.use(config => {
  if (import.meta.env.VITE_DEV_AUTH_BYPASS === 'true') {
    const authStore = useAuthStore()
    if (authStore.token) config.headers.Authorization = `Bearer ${authStore.token}`
  }
  return config
})
```

The response interceptor's 401 handling is unchanged in shape — it already calls `handleUnauthorized`, which now branches internally on the same flag (see §1):

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
VITE_DEV_AUTH_BYPASS=true
```

**Default is `true`.** `docker/.env.template` and `web/.env` ship with `VITE_DEV_AUTH_BYPASS=true` so `./docker/manage debug` runs against the mock login out of the box with zero setup. Set it to `false` only in environments wired to a real Keycloak client (test/prod), or locally if a developer specifically wants to exercise the real SSO flow.

---

## Dev Bypass Mode

**Design goal: this must stay exactly as simple as it is today.** Running `./docker/manage debug` should never require a developer to authenticate against real Keycloak or stand up a local IDP. Dev bypass does **not** attempt to replicate Keycloak's claim shape, role model, or token lifecycle — it is today's mock login (two hardcoded users, a locally-signed JWT), preserved unchanged behind a flag and used **by default**. Jasper doesn't include a local Keycloak container either; CES follows the same approach.

**Backend** — `Program.cs` checks `Keycloak:Enabled` (absent/`false` by default):
```csharp
if (builder.Configuration.GetValue<bool>("Keycloak:Enabled"))
    builder.Services.AddCESAuthentication(builder.Configuration, builder.Environment);
else
    builder.Services.AddCESDevAuthentication(builder.Configuration); // renamed from today's AddCESAuthentication — zero behavior change
```
`AddCESDevAuthentication` is today's `AddCESAuthentication` method, renamed and otherwise untouched: `LocalTokenService`, the symmetric-key JWT, and the `UserAuth` config section all keep working exactly as they do now. `LoginController.cs`, `LocalTokenService.cs`, `ITokenService.cs`, and `AuthConfiguration.cs` are all kept specifically to support this path (see the disposition table above) — none of them are deleted while dev bypass exists.

**Frontend** — `VITE_DEV_AUTH_BYPASS=true` (the default) keeps the `/login` route active and preserves the current `POST /api/auth/login` → `localStorage` JWT → `jwtDecode` flow in `AuthService.ts` / `authStore.ts` / `apiClient.ts` completely unchanged (see Frontend Changes §1–3 above for exactly what's kept vs. added).

The mock users (`admin@gov.bc.ca` / `officer@gov.bc.ca`) remain available only in bypass mode, unchanged from today.

`docker/.env.template` ships with `Keycloak:Enabled` unset (defaults to `false`) and `VITE_DEV_AUTH_BYPASS=true`, so the project runs out of the box against the mock login with no Keycloak client and no configuration required.

---

## Keycloak Realm Setup Requirements

The following must be configured in the target Keycloak realm by the SSO team before integration testing can begin.

1. **OIDC client** registered as a **confidential** client with:
   - `Standard flow` enabled
   - `Direct access grants` disabled
   - Valid redirect URIs per environment (e.g. `https://app.example.gov.bc.ca/api/auth/signin-oidc`)
   - Valid post-logout redirect URIs per environment
   - A client secret generated and securely provided to the deployment team

2. **Client roles** created on the CES confidential client (not realm roles):
   - `ces-clerk`
   - `ces-user`
   - `ces-judicial`

3. ~~`groups` scope~~ — **not required.** CES's role mapping uses `resource_access.<clientId>.roles` only; the `groups` claim isn't consumed anywhere in this design. Skip requesting this unless a future feature needs group membership (see IDIR Token Claims table).

4. ~~Audience mapper~~ — **not required.** CES's Option B flow has no `JwtBearer` scheme validating incoming API tokens (the API is the OIDC client, not a resource server), so there's no `aud` validation in code for this to satisfy. Only revisit this if CES later adds a service that independently validates bearer tokens.

5. **Test IDIR accounts** assigned to `ces-judicial`, `ces-user`, and `ces-clerk` roles for integration testing.

> **Note:** The callback path registered in Keycloak must be `/api/auth/signin-oidc` — this is where ASP.NET's OpenIdConnect middleware listens. It is not a Vue route.

---

## Open Questions / Follow-up Items

1. **Keycloak realm:** ~~Confirm whether CES will use the Justice realm...~~ **Resolved for dev** — CES uses a shared BC Gov SSO realm (value recorded outside this spec, not Jasper's `Judiciary` realm). **Still open:** confirm the equivalent Authority URL and client ID/secret for test and prod (does the realm stay the same with only the hostname prefix changing, or does it differ per environment?).

2. **Audit trail:** The current system identifies users by email (`admin@gov.bc.ca`). After Keycloak, `idir_user_guid` is the stable identifier. Confirm whether existing audit/submission records need a migration or can tolerate a format change at cutover.

3. **Session timeout UX:** When the server-side cookie refresh fails (Keycloak session has expired), the next API call returns 401 and the user is redirected to Keycloak. Define whether a "your session has expired" interstitial is needed or if a silent redirect is acceptable.
- Silent redirect is acceptable

4. **Cookie `SameSite=None`:** Required for cross-origin iframe scenarios (if any). If the app is served from the same origin as the API in all environments, `SameSite=Strict` is more secure. Confirm deployment topology.
- 

5. **Multi-tab logout:** If the user logs out in one tab, other open tabs will get 401s on their next API call and redirect to Keycloak automatically (following the Jasper pattern). Confirm this is acceptable rather than coordinating logout across tabs explicitly.
- this is acceptable

6. **`X-Forwarded-*` headers:** The `OnRedirectToIdentityProvider` handler rewrites the redirect URI when `X-Forwarded-Host` is present. Confirm that the nginx/OpenShift ingress layer forwards these headers and that `ForwardedHeaders` middleware is configured in `Program.cs`.

7. **Logout confirmation screen / `id_token_hint`:** ~~Verify actual behavior against the target realm...~~ **Resolved** — adopt Jasper's manual Keycloak logout-URL construction outright rather than the framework's `SignOutAsync(OpenIdConnectDefaults...)`, since `OnTicketReceived` strips `id_token` and there's no `id_token_hint` for the framework path to rely on. `AuthController.Logout` (§2 above) has been updated to match Jasper's pattern.
- Jasper's implementation is the correct pattern to follow