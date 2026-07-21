# Keycloak Authentication (CES-36-2)

**Status:** Active — this is the spec to implement
**Date:** 2026-07-21
**Branch:** `ces-36-2`
**Supersedes:** [completed/keycloak-simplified.md](completed/keycloak-simplified.md) (public client, no secret — **rejected**), [completed/keycloak-integration.md](completed/keycloak-integration.md) (first draft — **rejected**)

---

## Why this spec exists

Both earlier specs are dead. The second one ([keycloak-simplified.md](completed/keycloak-simplified.md)) assumed the CES Keycloak client was a **public** client, so the SPA could run Authorization Code + PKCE in the browser and never handle a secret.

The Bruno collection (`Bruno/bcgov/keycloak/`) proved that assumption wrong. The working request that successfully returns an access token **and** a refresh token uses:

```yaml
credentials:
  clientId: "{{keycloakClientId}}"
  clientSecret: "{{keycloakClientSecret}}"   # <-- required; the exchange 401s without it
```

The client is **confidential**. Keycloak requires client authentication on the token endpoint, which means:

1. The browser can never complete the code exchange — a confidential client's secret cannot be shipped to a SPA under any circumstance.
2. The token exchange, and every subsequent refresh, must happen **server-side in the CES API**.
3. Therefore the API stops being a pure stateless resource server and becomes the party that holds the Keycloak credentials.

This spec defines that server-mediated flow. Two properties drive every decision below:

- **The client secret and the refresh token never reach browser-readable storage.** The secret lives only in API process configuration. The refresh token lives only in an encrypted, `HttpOnly` cookie the API issues and reads back.
- **Access tokens auto-renew ahead of expiry.** CES uploads single files up to 100MB over court-network connections; a submission can occupy a browser tab for a long time. An upload must not fail because a 5-minute access token lapsed mid-session.

---

## User Stories

1. As an officer, hitting the app unauthenticated sends me to the BC Gov Keycloak login page, and I sign in with my IDIR account.
2. As an officer, after signing in I land back on the page I originally requested, already authenticated.
3. As an officer uploading a large exhibit, my session renews itself in the background so a long upload never fails with a `401`.
4. As an officer, reloading the browser tab keeps me signed in without a second trip to Keycloak.
5. As an officer, clicking Logout ends my session in both CES and Keycloak.
6. As a judicial user, admin routes are available to me because my IDIR account carries the `ces-judicial` client role.
7. As a developer, `./docker/manage debug` still gives me the mock login by default, so routine work needs no Keycloak client and no secret.
8. As a security reviewer, I can confirm by inspection that neither the client secret nor the refresh token is reachable from JavaScript.

---

## Scope

| Area | In Scope |
|---|---|
| Backend — OIDC authorization-code initiation (`state` + PKCE generated server-side) | Yes |
| Backend — code→token exchange using the client secret | Yes |
| Backend — refresh-token custody, rotation, and renewal endpoint | Yes |
| Backend — encrypted `HttpOnly` session cookie | Yes |
| Backend — bearer validation + role mapping on protected endpoints | Yes |
| Backend — RP-initiated logout URL construction | Yes |
| Frontend — redirect to login, callback landing, silent renewal scheduler | Yes |
| Frontend — remove username/password form on the real-Keycloak path | Yes |
| Frontend — move the access token out of `localStorage` into memory | Yes |
| Dev bypass mode (mock login behind an env flag) | Yes — behavior unchanged |
| Keycloak realm/client provisioning | No — SSO team owns it |
| User/role assignment in Keycloak | No — IDIR owns identity; SSO team assigns client roles |
| Persisting a CES `ApplicationUser` keyed on Keycloak `sub` | Yes — identifier only, not a display source |
| Resumable / chunked uploads | No — out of scope, see [Resolved Questions](#resolved-questions) |
| Cross-tab logout coordination | No — not required |

---

## Decisions

| # | Decision | Choice | Rationale |
|---|---|---|---|
| 1 | Identity provider | IDIR only (`kc_idp_hint=idir`) | Government staff only; skips the IDP-selector screen |
| 2 | OIDC client type | **Confidential** | Not a choice — the realm client requires client authentication, as proven by the Bruno collection |
| 3 | Flow | Authorization Code **+ PKCE**, exchanged server-side | PKCE is kept even though the client is confidential: it costs nothing and blocks authorization-code interception at the redirect |
| 4 | Who holds the secret | **API only**, from environment configuration | Never in the SPA bundle, never in a committed `appsettings.json`, never in an API response, never logged |
| 5 | Where the code exchange happens | CES API (`POST /api/auth/callback`) | Only party that may authenticate to the token endpoint |
| 5a | Redirect URI | The **SPA** route `/auth/callback`, which posts the code to the API | This is the URI SSO has already authorized; routing the code through the SPA avoids blocking on an SSO change request. See [Redirect URI: why the SPA route](#redirect-uri-why-the-spa-route) |
| 5b | Token-endpoint client authentication | `client_id` + `client_secret` as **form fields** | The method the Bruno collection proved works against this client; no reason to deviate |
| 6 | Refresh-token custody | Encrypted `HttpOnly`, `Secure`, `SameSite=Lax` cookie scoped to `/api/auth` | Unreachable from JS (XSS cannot exfiltrate it); no server-side session store, so the API stays horizontally scalable |
| 7 | Cookie payload protection | ASP.NET **Data Protection** (`IDataProtector`) | The cookie is opaque and tamper-evident even if a browser profile is copied off a workstation |
| 8 | Access-token custody | **Browser memory only** (Pinia state, no `localStorage`) | Strict improvement on today's `localStorage` token; the cookie makes persistence unnecessary since a reload can silently re-mint one |
| 9 | Token renewal trigger | Proactive timer at `expires_at − 60s`, **plus** a one-shot retry on `401` | The timer covers the common case; the retry covers clock skew, laptop sleep, and a tab that was backgrounded and had its timer throttled |
| 10 | Concurrent renewal | Single-flight — one in-flight refresh promise, all other callers await it | Parallel `401`s from a page issuing several requests must not fire N refreshes; with rotation enabled, N refreshes would invalidate each other |
| 11 | Refresh-token rotation | Assume **on**; the API re-issues the cookie on every successful refresh | Keycloak rotates by default on many realm configs. Writing the new token back is required either way and is harmless if rotation is off |
| 12 | Audience validation | `ValidateAudience = false`, compensated by an `azp == <client id>` check | Keycloak's default `aud` is `account`. Issuer + signature + `azp` is a sufficient trust boundary and avoids requesting an audience mapper from the SSO team |
| 13 | Role source | Keycloak **client** roles on the CES client | Scoped to CES rather than realm-wide; matches the existing `RoleConstants` |
| 14 | Role claim shape | Read flat top-level `roles`, fall back to `resource_access.<clientId>.roles` | A sample token from this client showed the flat shape (a protocol mapper bubbles it up), but the fallback costs ~6 lines and removes a hard dependency on that mapper surviving |
| 15 | Role names | `ces-user` → `User`, `ces-judicial` → `Admin`, `ces-clerk` → `Clerk` | Unchanged from today's `RoleConstants` |
| 16 | Dev bypass | Retained, unchanged, default-on | `./docker/manage debug` must keep working with no Keycloak client and no secret |
| 17 | Session ceiling | Cookie lifetime **8 hours**, matching Keycloak's max SSO session | Confirmed with SSO. A cookie outliving the Keycloak session is useless — its refresh token is already dead — so aligning them turns a confusing mid-action failure into a predictable one |
| 18 | Data Protection keys | `PersistKeysToFileSystem` on a **mounted volume** | Confirmed. Survives pod restarts and is shared across replicas, without adding an EF dependency to the auth path |
| 19 | Local user record | Upsert an `ApplicationUser` keyed on Keycloak `sub` at login | Gives audit/`CreatedBy` records a stable internal FK. **Identifier only — never a display source**; name and email always come from the token |

---

## Architecture

The redirect URI already registered with SSO is the **SPA route** `http://localhost:9080/auth/callback`, so Keycloak returns the browser to a Vue page, which immediately hands the authorization code to the API. The API still owns the secret, the code exchange, and every refresh — only the delivery path of the code differs. See [Redirect URI: why the SPA route](#redirect-uri-why-the-spa-route) below.

```
Browser (SPA)                  CES API                        Keycloak
     |                            |                               |
 1.  |-- GET /api/auth/login ---->|                               |
     |                            | generate state + PKCE verifier|
     |                            | encrypt into short-lived cookie
     |<-- 302 to authorize URL ---|                               |
 2.  |------------------------------ authorize (kc_idp_hint=idir) ->|
     |                            |                    IDIR login  |
 3.  |<----------------------------- 302 /auth/callback?code=&state= |
     |  (Vue AuthCallback.vue mounts, reads query, posts it on)     |
     |-- POST /api/auth/callback ->|   { code, state }             |
     |   (ces.login cookie rides along)                            |
     |                            |-- POST /token ---------------->|
     |                            |   code + verifier + SECRET     |
     |                            |<-- access + refresh + id ------|
     |                            | verify state; upsert user;     |
     |                            | encrypt refresh + id_token     |
     |                            | into session cookie            |
     |<-- { accessToken, expiresIn, user, returnUrl } --           |
 4.  |-- POST /api/auth/refresh ->|  (cookie sent automatically)   |
     |                            |-- POST /token grant=refresh -->|
     |                            |<-- new access + new refresh ---|
     |                            | re-issue rotated cookie        |
     |<-- { accessToken, expiresIn, user } --                      |
 5.  |-- GET /api/submissions ---->|  Authorization: Bearer <access>
     |   (validated against Keycloak JWKS, roles mapped)           |
```

Steps 4 and 5 are the entire steady state. Step 4 repeats on a timer for as long as the tab is open.

The SPA's only durable inputs are the access token returned by `/api/auth/callback` and `/api/auth/refresh`, and the `HttpOnly` cookie it cannot read. It touches the authorization code exactly once, in transit, and never sees the secret or the refresh token.

### Redirect URI: why the SPA route

SSO has authorized `http://localhost:9080/auth/callback` — a Vue route, not an API path. Rather than block on an SSO change request, the design routes the code through the SPA. The trade-off, stated plainly:

- **Unchanged:** the client secret, the code→token exchange, and refresh-token custody all remain server-side. Every item in the [Security Review Checklist](#security-review-checklist) still holds.
- **Slightly worse:** the authorization code passes through application JavaScript and lands in the browser's URL bar and history. The code is single-use, short-lived, PKCE-bound, and worthless without both the secret and the `code_verifier` (which never leaves the API) — so the exposure is small but non-zero.
- **Mitigations:** `AuthCallback.vue` calls `router.replace()` immediately after reading the query string, so the code does not persist in history; the API rejects a reused `state` (the `ces.login` cookie is cleared on first use), making replay ineffective.

If the SSO team later registers `/api/auth/callback`, switching is small and strictly an improvement — see [Appendix A](#appendix-a-server-side-callback-preferred-future-state).

### Why not a full BFF (cookie-only, API attaches the token)?

A full backend-for-frontend would drop the Bearer header entirely and have the API attach the access token to downstream calls itself. Rejected because every controller in the app already sits behind `[Authorize]` reading a Bearer token, `apiClient.ts` already attaches one, and the dev-bypass path depends on that shape. Keeping the Bearer header means **`apiClient.ts`'s request interceptor, every controller, and every `[Authorize(Roles = …)]` attribute are unchanged**. The confidential-client requirement is satisfied by moving only the *token acquisition* server-side, which is the minimum change that solves the actual problem.

---

## Configuration

| Key | Layer | Secret? | Notes |
|---|---|---|---|
| `Keycloak__Enabled` | API | No | Mode toggle. Default `false` → dev-bypass path |
| `Keycloak__Authority` | API | No | Realm base URL. All endpoints resolved from `{Authority}/.well-known/openid-configuration` |
| `Keycloak__Client` | API | No | Client ID |
| `Keycloak__Secret` | API | **Yes** | Client secret. Environment/secret-store only |
| `Keycloak__RedirectUri` | API | No | The **SPA** callback route. Must byte-match a Valid Redirect URI on the client, and must be sent identically on both the authorize request and the token exchange |
| `Keycloak__PostLogoutRedirectUri` | API | No | Where Keycloak returns the browser after logout |
| `DataProtection__KeyPath` | API | No | Mounted-volume path for the key ring. Must be shared across replicas (Decision 18) |
| `VITE_DEV_AUTH_BYPASS` | Web | No | Default `true`. The **only** auth env var the frontend needs now |

The frontend requires no `VITE_KEYCLOAK_*` variables at all — a direct consequence of moving the flow server-side, and a nice simplification over the superseded spec.

> **No real Authority URL, Client ID, or secret appears in this document or anywhere under `spec/`.** They live in `docker/.env` (gitignored), the deployment secret store, and `Bruno/bcgov/environments/dev.yml` (gitignored via `Bruno/bcgov/.gitignore`). Ask the team for dev values.

### Secret-handling rules (non-negotiable)

1. `Keycloak__Secret` is **never** written to `appsettings.json` or `appsettings.Development.json` — those are tracked files. It is read from environment variables only.
2. `docker/.env.template` gains the key with an **empty** value, as a placeholder for developers to fill in.
3. `KeycloakConfiguration.Secret` is never returned from any endpoint, never included in a log statement, and never placed in an exception message. Failures during the token exchange log the Keycloak `error` / `error_description` fields only.
4. Test/prod secrets are supplied by the platform secret store, not baked into an image.
5. If a secret is ever committed, it is treated as compromised: rotate with the SSO team before anything else.

### `docker/.env.template` additions

```
# Keycloak (leave Enabled=false for the mock dev login)
Keycloak__Enabled=false
Keycloak__Authority=
Keycloak__Client=
Keycloak__Secret=
Keycloak__RedirectUri=http://localhost:9080/auth/callback
Keycloak__PostLogoutRedirectUri=http://localhost:9080/

# Data Protection key ring (encrypts the auth cookies)
DataProtection__KeyPath=/keys
```

`Keycloak__RedirectUri` is the value SSO has already authorized for local development. Change it only in lockstep with a registered Valid Redirect URI — Keycloak rejects the exchange with `invalid_grant` if the `redirect_uri` on the token request differs by even a trailing slash from the one on the authorize request.

---

## Backend Changes

### 1. `api/CES.API/configuration/KeycloakConfiguration.cs` (new)

```csharp
namespace CES.API.Configuration
{
    public class KeycloakConfiguration
    {
        public bool Enabled { get; set; }
        public string Authority { get; set; } = string.Empty;
        public string Client { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string PostLogoutRedirectUri { get; set; } = string.Empty;
    }
}
```

`appsettings.json` gains the non-secret shape only:

```json
"Keycloak": {
  "Enabled": false,
  "Authority": "",
  "Client": "",
  "RedirectUri": "",
  "PostLogoutRedirectUri": ""
}
```

Note the absence of a `Secret` line — omitted deliberately so no one is tempted to fill it in a tracked file.

### 2. Auth constants (new, `api/CES.API/Authentication/AuthConstants.cs`)

Per the project rule against inline magic values:

```csharp
namespace CES.API.Authentication
{
    public static class AuthConstants
    {
        /// Cookie holding the Data Protection-encrypted refresh token + id_token.
        public const string SessionCookieName = "ces.session";

        /// Cookie holding the encrypted PKCE verifier + state, alive only between
        /// the authorize redirect and the callback.
        public const string LoginStateCookieName = "ces.login";

        /// Path the session cookie is scoped to — the browser only ever sends it
        /// to the auth endpoints, never to submission/file endpoints.
        public const string AuthCookiePath = "/api/auth";

        /// Data Protection purpose strings (distinct purposes cannot be swapped).
        public const string SessionProtectorPurpose = "CES.Auth.Session.v1";
        public const string LoginStateProtectorPurpose = "CES.Auth.LoginState.v1";

        /// Login round-trip budget. An IDIR login that takes longer than this
        /// restarts rather than replaying a stale state/verifier pair.
        public const int LoginStateLifetimeMinutes = 10;

        /// Matches Keycloak's max SSO session (confirmed with the SSO team, 2026-07).
        /// Past this point the refresh token is dead anyway, so the cookie expires
        /// with it rather than lingering as a token that cannot be redeemed.
        /// Revisit if the SSO team changes the realm's max-session setting.
        public const int SessionCookieLifetimeHours = 8;

        /// Keycloak client roles → CES application roles.
        public const string KeycloakRoleAdmin = "ces-judicial";
        public const string KeycloakRoleUser = "ces-user";
        public const string KeycloakRoleClerk = "ces-clerk";

        /// Claim names.
        public const string RolesClaim = "roles";
        public const string ResourceAccessClaim = "resource_access";
        public const string AuthorizedPartyClaim = "azp";
    }
}
```

### 3. `IKeycloakTokenService` / `KeycloakTokenService` (new)

The only class in the codebase that touches the client secret. Uses a named `HttpClient` from `IHttpClientFactory`, and resolves endpoints from the discovery document rather than hardcoding paths.

```csharp
public interface IKeycloakTokenService
{
    /// Builds the authorize URL and returns it with the state/verifier the caller must persist.
    (string AuthorizeUrl, LoginState State) BuildAuthorizeRequest(string? returnUrl);

    /// Exchanges an authorization code. Throws ArgumentException on a Keycloak error.
    Task<KeycloakTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct);

    /// Redeems a refresh token. Throws ArgumentException when the grant is rejected
    /// (expired / already-rotated / revoked) so the middleware maps it to a 400→SPA re-login.
    Task<KeycloakTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct);

    /// RP-initiated logout URL, including id_token_hint when one is available.
    Task<string> BuildEndSessionUrlAsync(string? idToken, CancellationToken ct);
}
```

Implementation requirements:

- `code_verifier`: 43–128 chars from a CSPRNG (`RandomNumberGenerator`), base64url-encoded; `code_challenge = BASE64URL(SHA256(verifier))`, `code_challenge_method=S256`.
- `state`: independent CSPRNG value, compared with a **fixed-time** comparison on callback (`CryptographicOperations.FixedTimeEquals`).
- Client authentication on the token endpoint: `client_id` + `client_secret` as **form fields** — the method the Bruno collection proved works against this client (Decision 5b). Do not switch to `client_secret_basic` without re-verifying in Bruno first.
- `redirect_uri` must be sent on the token exchange **byte-identical** to the value sent on the authorize request (`Keycloak__RedirectUri`); Keycloak returns `invalid_grant` otherwise. Send the configured constant on both, never a value reconstructed from the incoming request.
- Discovery document is fetched once and cached (`Microsoft.IdentityModel.Protocols.OpenIdConnect` `ConfigurationManager`, or a simple cached `HttpClient` call) — do not hit `.well-known` on every login.
- On a non-2xx token response, log `error` / `error_description` and throw `ArgumentException` (→ `400` via `ApiExceptionMiddleware`). **Never** log the request body — it contains the secret.

### 4. `AuthController` (new, `api/CES.API/Controllers/AuthController.cs`)

Four endpoints, all `[AllowAnonymous]` except where noted. All are no-ops (`404`) when `Keycloak:Enabled` is false.

| Endpoint | Purpose |
|---|---|
| `GET /api/auth/login?returnUrl=` | Generates state + PKCE, writes the encrypted `ces.login` cookie, `302`s to Keycloak's authorize URL with `kc_idp_hint=idir` |
| `POST /api/auth/callback` | Body `{ code, state }` from `AuthCallback.vue`. Validates state against the `ces.login` cookie, exchanges the code, upserts the `ApplicationUser`, writes the encrypted `ces.session` cookie, **clears `ces.login`**, returns `{ accessToken, expiresIn, user, returnUrl }` |
| `POST /api/auth/refresh` | Reads the session cookie, redeems the refresh token, **re-issues the rotated cookie**, returns `{ accessToken, expiresIn, user }`. `401` if there is no cookie or the grant is rejected |
| `POST /api/auth/logout` | Clears the session cookie and returns `{ endSessionUrl }` for the SPA to navigate to. Cookie is cleared even if URL construction fails |

`returnUrl` handling: validated at `/api/auth/login`, then carried **inside the encrypted `ces.login` cookie** — not through the `state` parameter and not through the browser's URL — and handed back to the SPA by the callback response. Accept only same-site relative paths: must start with a single `/`, must not start with `//` or `/\`, and must not contain a scheme. Anything else falls back to `/`. This is an open-redirect guard and needs a dedicated unit test.

Because the cookie is encrypted and server-issued, a user cannot tamper with `returnUrl` between the authorize redirect and the callback — the only validation point is the one entry into `/api/auth/login`.

Cookie attributes for both cookies:

```csharp
new CookieOptions
{
    HttpOnly = true,
    Secure   = true,          // localhost is a secure context, so this works in local dev
    SameSite = SameSiteMode.Lax,   // Lax, not Strict: the ces.login cookie must survive
                                   // Keycloak's top-level redirect back to /auth/callback
    Path     = AuthConstants.AuthCookiePath,
    IsEssential = true,
    MaxAge   = /* per-cookie constant above */
}
```

`Path = /api/auth` matters: the browser will not attach the session cookie to `/api/submissions` or `/api/files`, so the refresh token is not present on the huge multipart upload requests.

### 5. Bearer validation — `AuthenticationKeycloakExtensions.cs` (rewritten)

The file's current contents (commented-out scaffolding) are deleted.

```csharp
public static IServiceCollection AddCESKeycloakAuthentication(
    this IServiceCollection services, IConfiguration configuration)
{
    var keycloak = configuration.GetSection("Keycloak").Get<KeycloakConfiguration>()
        ?? throw new InvalidOperationException("Configuration section 'Keycloak' not found.");

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = keycloak.Authority;   // JWKS resolved + cached from discovery
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = keycloak.Authority,
                ValidateAudience = false,             // see Decision 12
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RoleClaimType = ClaimTypes.Role,
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = ctx => MapKeycloakRoles(ctx, keycloak.Client),
            };
        });

    return services;
}
```

`MapKeycloakRoles` does two things:

1. **`azp` check.** With audience validation off, confirm `azp` equals the configured client ID; `ctx.Fail(...)` otherwise. This is what stops a valid token minted for a *different* realm client being accepted by CES.
2. **Role mapping.** Read the flat `roles` claim; if it is absent, parse `resource_access.<clientId>.roles` from the JSON claim value. Map through `AuthConstants` to `RoleConstants.Admin/User/Clerk` and add each as a `ClaimTypes.Role` claim.

Because the output claims are the same `RoleConstants` strings the mock token produces, **every existing `[Authorize(Roles = …)]` attribute across `SubmissionsController`, `FilesController`, `ReviewController`, and `UserController` is unchanged.**

### 6. `Program.cs`

```csharp
if (builder.Configuration.GetValue<bool>("Keycloak:Enabled"))
{
    builder.Services.AddCESKeycloakAuthentication(builder.Configuration);
    builder.Services.AddHttpClient<IKeycloakTokenService, KeycloakTokenService>();
}
else
{
    builder.Services.AddCESAuthentication(builder.Configuration);  // today's mock JWT, untouched
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath))
    .SetApplicationName(DataProtectionApplicationName);
```

`AddCESAuthentication` keeps its name and behavior. `app.UseAuthentication()` / `app.UseAuthorization()` are unchanged.

**Data Protection key ring — mounted volume (Decision 18).** The key path comes from configuration (`DataProtection__KeyPath`), defaulting to a local directory so a bare `dotnet run` still works:

```
DataProtection__KeyPath=/keys        # mounted volume in Docker/OpenShift
```

Three requirements, all easy to get wrong:

1. The volume must be **shared across replicas and survive pod restarts**. Without it, a restarted or second pod cannot decrypt a cookie it did not issue, and users are silently bounced to Keycloak mid-session — a bug that will not reproduce on a single-replica developer machine.
2. `SetApplicationName` must be an explicit constant, identical across replicas. ASP.NET otherwise derives it from the content-root path, and two pods with different paths will not share keys even off the same volume.
3. The volume holds **key material**: mount it read-write only for the API, and keep it out of image layers and backups that travel more widely than the app.

`docker/docker-compose.yaml` gets a named volume mounted at that path for the `api` service, so local multi-restart behaviour matches deployed behaviour.

### 7. Local user record (`ApplicationUser` upsert)

Per Decision 19, a CES-local row is created or updated on each successful login, keyed on the Keycloak `sub`.

`ApplicationUser` gains one nullable column plus a unique index:

```csharp
/// Keycloak subject — stable per user within the realm. Null for legacy/mock
/// dev-bypass users, which is why this is nullable rather than required.
public string? KeycloakSub { get; set; }
```

An EF Core migration adds the column and a **unique index filtered to non-null values**, so existing mock rows do not collide.

`AuthController`'s callback calls an `IUserService.UpsertFromTokenAsync(sub, email, givenName, familyName)` which finds the row by `KeycloakSub` and inserts it if absent, refreshing `Email` / `FirstName` / `LastName` from the token each login so the record does not drift from IDIR.

Two constraints on how this is used:

- **Identifier only, never a display source.** All name and email rendering comes from the token claims the SPA already decodes. The row exists so audit records (`BaseEntity.CreatedBy` / `UpdatedBy`, `SubmissionAuditLog`) have a stable internal key that survives a user's name changing in IDIR.
- **`ApplicationUser.Password` stays unused and unset** on this path. Keycloak owns authentication; a Keycloak-provisioned row must never carry a credential. Consider a follow-up migration to drop the column once the dev-bypass path retires.

Roles are **not** persisted on this row — Keycloak remains the single source of truth for authorization, and a stale local copy would be a security bug waiting to happen.

### 8. Untouched

`LoginController.cs`, `LogoutController.cs`, `LocalTokenService.cs`, `ITokenService.cs`, `AuthenticationExtensions.cs`, and the `UserAuth` config section all stay exactly as they are, serving the dev-bypass path.

---

## Frontend Changes

### 1. No new dependency

`oidc-client-ts` is **not** added. It exists to run the OIDC flow in the browser, and the browser no longer runs the flow. `jwt-decode` (already installed) continues to decode the access token for display name and roles. Fewer dependencies than the superseded spec.

### 2. `authStore.ts` — token moves to memory

The meaningful security change on the frontend.

```typescript
const token = ref<string | null>(null);        // was: localStorage.getItem('jwt_token')
const expiresAt = ref<number | null>(null);    // epoch ms, from the access token's exp
```

- `setToken` no longer writes `localStorage`; it decodes the token, sets `user`/`roles`/`expiresAt`, and schedules the next renewal.
- `clearAuth` cancels the pending renewal timer.
- The bypass path keeps `localStorage` (gated on `VITE_DEV_AUTH_BYPASS`) so the mock login still survives a reload with no API round-trip.
- `decodeAndSetUser` handles both claim shapes: mock tokens carry a singular string `role`; Keycloak tokens carry a plural `roles` array.

`AuthModels.ts`:

```typescript
export interface JwtPayload {
  sub: string;
  email: string;
  exp: number;
  iss?: string;
  role?: string;      // dev-bypass mock token
  roles?: string[];   // Keycloak token
  name?: string;
  preferred_username?: string;
}

export interface User {
  id: string;
  email: string;
  roles: string[];
  displayName?: string;
}
```

Losing the token on reload is fine and intended: `App.vue` calls `POST /api/auth/refresh` on mount, the `HttpOnly` cookie rides along automatically, and a fresh access token comes back without any user-visible redirect. That satisfies User Story #4 while keeping nothing long-lived in JS-readable storage.

### 3. `sessionService.ts` (new, `web/src/services/sessionService.ts`)

Owns renewal. Three responsibilities:

**a. Proactive scheduling.** After every `setToken`, schedule a refresh at `expiresAt − TOKEN_REFRESH_LEAD_MS`:

```typescript
// web/src/constants/auth.ts
/** Renew this far ahead of exp. Covers clock skew between browser and Keycloak
 *  plus the round-trip, so a request is never sent with a token about to lapse. */
export const TOKEN_REFRESH_LEAD_MS = 60_000;

/** Floor on the timer — a token issued with < 2x the lead time left refreshes
 *  almost immediately rather than scheduling a negative delay. */
export const MIN_REFRESH_DELAY_MS = 5_000;
```

**b. Single-flight.** One module-level `Promise<string> | null`. Concurrent callers await the same promise; it is cleared in a `finally`. Mandatory under refresh-token rotation — two parallel refreshes would race, and the loser's rotated token would already be invalidated.

**c. Failure handling.** A `401` from `/api/auth/refresh` means the Keycloak session is genuinely over (idle timeout, max session, admin revocation, logout elsewhere). Clear auth and redirect to `/api/auth/login?returnUrl=<current path>` via `window.location.assign` — a full-page navigation, because the browser must follow the API's `302` to Keycloak.

### 4. `apiClient.ts` — response interceptor

The **request** interceptor is unchanged; it still attaches `Bearer ${authStore.token}`. Add `withCredentials: true` to the axios instance so the session cookie is sent on the auth endpoints.

The response interceptor's `401` branch changes from "redirect to login" to "try once to renew, then replay":

```typescript
if (status === 401 && !originalRequest._retried) {
  originalRequest._retried = true;              // one attempt only — no refresh loop
  try {
    const newToken = await sessionService.refresh();   // single-flight
    originalRequest.headers.Authorization = `Bearer ${newToken}`;
    return api(originalRequest);
  } catch {
    handleUnauthorized(window.location.pathname);      // refresh itself failed → full re-login
  }
}
```

`_retried` is what prevents an infinite refresh/retry cycle when the API is rejecting tokens for a reason a refresh cannot fix.

### 5. Long uploads (User Story #3)

The failure mode this spec must actually defeat, spelled out:

- A 100MB upload can hold a request open for many minutes. Since ASP.NET validates the bearer token when the request **arrives**, an upload that starts with a valid token completes even if the token expires mid-transfer. So the in-flight request is safe.
- The real risk is the *next* request after a long upload — and the renewal timer having been throttled while the tab was busy or backgrounded.
- Mitigations, in order:
  1. The proactive timer keeps renewing during the upload; a refresh is a small request to `/api/auth/refresh` and is not blocked by the upload.
  2. Each successful refresh also **extends the Keycloak SSO idle timeout**, so an active uploader is never idle-timed-out.
  3. If the timer was throttled and a request 401s anyway, the interceptor renews and replays it.
- **Known limitation:** a request replayed by the interceptor is re-sent from the start. For a 100MB upload that means re-uploading. Because refresh is proactive, an upload should never be the request that 401s — but resumable/chunked uploads are the durable fix and are **out of scope** (confirmed); revisit only if operational data shows it biting.

**The 8-hour ceiling.** Keycloak's max SSO session is 8 hours (Decision 17), and refreshing does **not** extend it — only the idle timeout resets. So a tab left open past 8 hours gets a hard, unavoidable re-login no matter how healthy the renewal loop is. Two consequences worth designing for:

- Refresh failure must be handled as *expected*, not exceptional. At the 8-hour mark every open tab will hit it.
- An officer who starts a large upload near the end of an 8-hour session can lose it. Since `expiresAt` for the overall session is knowable from the first login, a **follow-up** worth considering is warning before starting an upload when little session time remains. Not required for this spec — recorded so the failure mode is a known one rather than a surprise.

### 6. `AuthService.ts`

Bypass functions stay as they are. Added:

```typescript
const loginViaKeycloak = (returnUrl?: string) => {
  const target = returnUrl && returnUrl !== '/' ? `?returnUrl=${encodeURIComponent(returnUrl)}` : '';
  window.location.assign(`/api/auth/login${target}`);   // full navigation, not axios
};

const logoutViaKeycloak = async () => {
  const { data } = await api.post<{ endSessionUrl: string }>('/auth/logout');
  useAuthStore().clearAuth();
  window.location.assign(data.endSessionUrl);
};
```

`handleUnauthorized` branches on `VITE_DEV_AUTH_BYPASS`: bypass → `router.push({ name: 'Login' })` as today; Keycloak → `loginViaKeycloak(currentPath)`.

### 7. `AuthCallback.vue` (new, `web/src/views/AuthCallback.vue`)

The landing page for the registered redirect URI `/auth/callback`. It renders a spinner and nothing else — the user should never meaningfully see it.

```typescript
onMounted(async () => {
  const { code, state, error, error_description } = route.query;

  // Keycloak reports failures on the redirect, not with an error status.
  if (error) return router.replace({ name: 'AuthError', query: { reason: error_description ?? error } });
  if (!code || !state) return router.replace({ name: 'AuthError' });

  // Drop the code from the URL bar and history before doing anything else.
  router.replace({ path: '/auth/callback', query: {} });

  try {
    const { data } = await api.post('/auth/callback', { code, state });
    authStore.setToken(data.accessToken);           // schedules the first renewal
    await router.replace(data.returnUrl || '/');
  } catch {
    router.replace({ name: 'AuthError' });
  }
});
```

Three details that matter:

- **`router.replace` before the POST**, so a single-use code never survives in browser history — one of the mitigations that makes the SPA-mediated callback acceptable.
- **Keycloak signals errors by redirecting with `?error=`**, not by failing the request. `access_denied` (user cancelled at the IDIR screen) must not present as a generic crash.
- The route is registered **unconditionally**, and is harmless in bypass mode — nothing ever navigates there.

A minimal `AuthError` view (or a reuse of `ForbiddenView.vue` with different copy) gives the user a "Try signing in again" button rather than a blank screen. Errors here are dead-ends unless the user can restart the flow.

### 8. Router

- `/login` is only reachable when `VITE_DEV_AUTH_BYPASS === 'true'`; otherwise the guard redirects via `loginViaKeycloak()`.
- `/auth/callback` and the auth-error route are registered unconditionally and are **not** guarded — guarding the callback would deadlock the login it exists to complete.
- Guards otherwise unchanged: `authStore.isAuthenticated` and `authStore.roles` continue to drive `meta.roles`.
- `App.vue` awaits a bootstrap `sessionService.refresh()` before the first guarded navigation resolves, so a hard reload does not flash the login screen. That bootstrap must be **skipped on `/auth/callback`**, where there is legitimately no session cookie yet and a `401` is expected rather than a failure.

### 9. `LoginView.vue`

Unchanged, but only mounted on the bypass path. On the Keycloak path the user never sees it — the guard redirects before it renders.

---

## Keycloak Realm Setup Requirements

For the SSO team. Items 1–3 are already confirmed working via the Bruno collection.

1. **Confidential OIDC client** — `Client authentication` **on** (this is the change from the rejected spec):
   - Standard flow (Authorization Code) enabled
   - `PKCE Code Challenge Method` = `S256`
   - Direct access grants disabled
   - Service accounts not required
2. **Client secret** issued to the CES team through a secure channel (not email, not a ticket comment, not Teams chat).
3. **Client roles**: `ces-user`, `ces-judicial`, `ces-clerk`.
4. **Flat `roles` protocol mapper** — appears to be present already; confirm it stays, though the backend has a `resource_access` fallback.
5. **Valid Redirect URIs**:
   - `http://localhost:9080/auth/callback` — **already authorized**, unblocking local development
   - `https://<env-host>/auth/callback` per deployed environment — **still to be requested** for test and prod
   - `http://localhost:5173/auth/callback` only if developers run bare `npm run dev` rather than Docker; otherwise skip it and keep the registered surface small
6. **Valid Post Logout Redirect URIs** — the app root per environment.
7. **Web Origins** — not required for this flow (the browser never calls Keycloak's token endpoint), but harmless if already set.
8. **Session lifetimes** — max SSO session confirmed at **8 hours**. Still to confirm: the **idle** timeout, and whether **refresh-token rotation** is enabled. The implementation is correct either way, but both numbers should be recorded here once known so the renewal behaviour is verifiable rather than assumed.
9. **Test IDIR accounts** assigned to each of the three client roles.
10. **No audience mapper** requested — see Decision 12.

---

## Local Development

Dev bypass stays the default; nobody needs a secret for routine work.

**To run the real Keycloak flow locally:**

1. In `docker/.env` (gitignored), set `Keycloak__Enabled=true` plus `Authority`, `Client`, `Secret`, and the two redirect URIs.
2. In `web/.env.local` (gitignored via `*.local`), set `VITE_DEV_AUTH_BYPASS=false`.
3. **Restart, don't refresh.** `VITE_*` is read at Vite start, `Keycloak__*` at API start.
4. **Flip both together.** They are not cross-validated; if only one side is on real Keycloak, every call 401s.
5. **Reach the app on `http://localhost:9080`, not `5173`.** `http://localhost:9080/auth/callback` is the URI SSO authorized; any other origin gets `invalid_redirect_uri` from Keycloak.
6. The Data Protection volume must be mounted, or every API restart invalidates outstanding cookies and silently forces a re-login.

Reverting: blank `web/.env.local`, set `Keycloak__Enabled=false`, restart.

### Bruno

`Bruno/bcgov/keycloak/` already exercises the flow. Once the API side is built:

- `UserInfo` proves Keycloak issues a usable token for the confidential client.
- `VerifyBearerWithApi` proves the CES API's `azp` / issuer / signature validation accepts it — only meaningful with `Keycloak__Enabled=true` pointed at the same realm.
- Add a third request hitting `POST {{apiBaseUrl}}/api/auth/refresh` to verify cookie-based renewal and rotation independently of the browser.

Environment files: keep real values in `dev.yml` / `test.yml`, which `Bruno/bcgov/.gitignore` excludes. `keycloakClientSecret` is already marked `secret: true` in both samples — keep it that way.

---

## Testing

Per the project testing rule, tests are written for all of this, and the user is notified before they are written so functionality and completeness can be verified first.

### Backend — `CES.Business.Tests` / `CES.API.Tests`

| Area | Cases |
|---|---|
| PKCE | Verifier length in 43–128 and base64url-safe; `challenge == BASE64URL(SHA256(verifier))` |
| State | Round-trips through protect/unprotect; mismatched state is rejected; a tampered byte fails to unprotect |
| Token exchange | Mocked `HttpMessageHandler` — success parses tokens; Keycloak `400 invalid_grant` throws `ArgumentException`; **the secret never appears in any log the test captures** |
| Refresh | Success returns new tokens; a rotated refresh token replaces the cookie value; `invalid_grant` surfaces as `401` from the endpoint |
| Cookie | `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/api/auth` all asserted on the `Set-Cookie` header; value is not a readable JWT |
| `returnUrl` | `/officer/court-list` allowed; `//evil.example`, `/\evil.example`, `https://evil.example`, and `javascript:` all fall back to `/` |
| Role mapping | Flat `roles` maps correctly; `resource_access` fallback maps correctly; unknown roles ignored; empty roles yields no role claims |
| `azp` | Mismatched `azp` fails authentication even when the signature is valid |
| `redirect_uri` | The configured constant is sent on both the authorize request and the token exchange, byte-identical |
| User upsert | First login inserts an `ApplicationUser` with `KeycloakSub`; second login updates rather than duplicating; a changed name/email in the token refreshes the row; `Password` is left unset |
| Disabled mode | With `Keycloak:Enabled=false`, `/api/auth/*` returns `404` and the mock login still works |
| Integration | `WebApplicationFactory` — protected endpoint returns `401` without a bearer, `403` with the wrong role, `200` with the right one |

### Frontend — Vitest

| Area | Cases |
|---|---|
| `authStore` | Token is **not** written to `localStorage` on the Keycloak path; `expiresAt` derives from `exp`; both `role` and `roles` claim shapes decode; `clearAuth` cancels the timer |
| `sessionService` | Renewal fires at `exp − TOKEN_REFRESH_LEAD_MS` (fake timers); three concurrent `refresh()` calls issue exactly **one** network request; a `401` from refresh triggers full re-login |
| `apiClient` | A `401` refreshes once and replays with the new token; a second `401` on the replay does **not** loop; `withCredentials` is set |
| `AuthCallback.vue` | Posts `code` + `state` and stores the returned token; clears the query string before posting; `?error=access_denied` routes to the error view instead of crashing; a missing `code` routes to the error view |
| Router | Guarded route with no token redirects to Keycloak login when bypass is off, and to `/login` when it is on; `/auth/callback` is reachable unauthenticated and skips the bootstrap refresh |

Both suites must pass before the work is done: `dotnet test api/CES.API/CES.API.sln` and `cd web && npm run test`.

---

## Security Review Checklist

Answers User Story #8 — verifiable by inspection at PR time.

- [ ] `Keycloak__Secret` appears in no tracked file (`git grep` the key name; only `.env.template` with an empty value and this spec's placeholder text may match).
- [ ] The secret is referenced by exactly one class, `KeycloakTokenService`, and appears in no log statement, response body, or exception message.
- [ ] No `VITE_KEYCLOAK_*` variable exists — the frontend has no Keycloak configuration at all.
- [ ] The refresh token appears in no response body; grep the SPA bundle for `refresh_token` and find nothing.
- [ ] The session cookie is `HttpOnly` + `Secure` + `SameSite=Lax` + `Path=/api/auth`, verified in browser devtools.
- [ ] The access token is absent from `localStorage` and `sessionStorage` on the Keycloak path.
- [ ] `state` is compared in fixed time and is single-use (the login cookie is cleared on callback).
- [ ] `returnUrl` cannot produce an off-site redirect, and is carried in the encrypted cookie rather than the URL.
- [ ] `AuthCallback.vue` clears the authorization code from the URL before any await, so it does not persist in browser history.
- [ ] `ValidateIssuer`, `ValidateLifetime`, `ValidateIssuerSigningKey` are all on, and the `azp` check compensates for the disabled audience check.
- [ ] `RequireHttpsMetadata = true` in every non-Development environment.
- [ ] The Data Protection key ring is on a shared mounted volume with an explicit `SetApplicationName`, in test/prod.
- [ ] `ApplicationUser` rows created from Keycloak carry no password and no persisted roles.
- [ ] Logout clears the cookie server-side **and** ends the Keycloak session.

---

## Implementation Order

Each step leaves the app working, and the dev-bypass path never breaks.

1. Config plumbing — `KeycloakConfiguration`, `AuthConstants`, `appsettings.json`, `.env.template`, Data Protection volume in `docker-compose.yaml`. No behavior change.
2. `KeycloakTokenService` + unit tests against a mocked handler. Nothing wired up yet.
3. `AuthController` `GET /login` + `POST /callback` with cookie issuance.
4. `AuthCallback.vue` + its route. **First end-to-end checkpoint:** browser reaches IDIR, returns to `/auth/callback`, and the API sets `ces.session`. This is the step that proves the registered redirect URI works — do not build further until it does.
5. `AddCESKeycloakAuthentication` + role mapping, gated on `Keycloak:Enabled`. Verify with Bruno's `VerifyBearerWithApi`.
6. `ApplicationUser.KeycloakSub` migration + upsert on callback.
7. `/api/auth/refresh` + rotation. Verify with the new Bruno request.
8. Frontend: `authStore` moved to memory, bootstrap refresh in `App.vue`, `sessionService`.
9. Frontend: `apiClient` `401` retry + `AuthService` login/logout redirects + router guard changes.
10. Logout end-session.
11. Full test pass, both suites, both modes (`Keycloak__Enabled` true and false).

---

## Resolved Questions

All answered 2026-07-21. Recorded here because each one shaped a decision above.

| # | Question | Answer | Where it landed |
|---|---|---|---|
| 1 | Registered redirect URI path | `http://localhost:9080/auth/callback` — the **SPA route** | Decision 5a; the SPA-mediated callback became the primary design, and `AuthCallback.vue` is now a required deliverable |
| 2 | Token-endpoint client auth | Form-post `client_secret`, as Bruno proved | Decision 5b; the `client_secret_basic` alternative is dropped |
| 3 | Session lifetimes | Max SSO session **8 hours** | Decision 17; cookie lifetime aligned, and the hard-ceiling consequence documented under [Long uploads](#5-long-uploads-user-story-3) |
| 4 | Data Protection key persistence | **Mounted volume** | Decision 18; `DataProtection__KeyPath` config, plus the `SetApplicationName` requirement that makes a shared volume actually work |
| 5 | Local user record | **Yes** — identifier only, never a display source | Decision 19; new `ApplicationUser.KeycloakSub` + upsert, moved from deferred into scope |
| 6 | Resumable uploads | Out of scope for now | Noted as a known limitation, not designed for |
| 7 | Cross-tab logout | Not required | Accepted as-is; other tabs `401` on their next call |

## Remaining Unknowns

Neither blocks the start of implementation.

1. **Refresh-token rotation on/off, and the idle timeout.** The implementation is rotation-safe either way — the API always writes back whatever refresh token Keycloak returns — but both values should be recorded in [Keycloak Realm Setup Requirements](#keycloak-realm-setup-requirements) once the SSO team confirms them, so renewal behaviour is verified rather than assumed.
2. **Test/prod redirect URIs and Authority values.** Only the local dev callback is authorized today. Request `https://<env-host>/auth/callback` and the per-environment realm Authority when those environments are wired up.

---

## Appendix A: server-side callback (preferred future state)

Not needed now, and not a blocker — recorded so the option is not forgotten.

If the SSO team later registers `/api/auth/callback` as a Valid Redirect URI, the flow can be tightened: Keycloak would redirect the browser straight to the API, which exchanges the code and `302`s to the SPA. The authorization code would then never touch application JavaScript, the URL bar, or browser history.

**What changes:** `Keycloak__RedirectUri` points at the API path; `POST /api/auth/callback` becomes `GET /api/auth/callback?code=&state=` and ends in a `302` rather than a JSON body; `AuthCallback.vue` and its route are deleted; the SPA's bootstrap `refresh()` call picks up the session on the landing page instead of receiving a token directly.

**What does not change:** the client secret, the code exchange, refresh-token custody, cookie handling, role mapping, and renewal are all identical. This is a delivery-path change, not an architecture change — which is exactly why shipping the SPA-mediated version first costs nothing later.
