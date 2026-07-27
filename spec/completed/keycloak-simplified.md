# Keycloak Integration (Simplified)

**Status:** Rejected - Do not use!
**Date:** 2026-07-16
**Supersedes:** [keycloak-integration.md](keycloak-integration.md)

---

## Overview

[keycloak-integration.md](keycloak-integration.md) was written against **bcgov/jasper**'s pattern: the API acts as a confidential OIDC client, does the token exchange itself, and relays an HttpOnly cookie to the browser (Option B — backend-driven token relay). That requires a client secret, a cookie-backed session on the API, server-side refresh-token rotation, and a hand-built Keycloak logout URL — none of which CES actually needs.

This spec replaces that approach with a **frontend-driven** flow: the SPA talks to Keycloak directly as a **public client** (Authorization Code + PKCE, no secret), gets an access token in the browser, and sends it to the API as a normal `Authorization: Bearer` header — exactly the same shape the API already accepts from today's mock JWT. The API's only job is to validate that token and read the caller's roles out of it. Nothing about the API becomes stateful; there is no cookie, no server-side refresh loop, and no client secret to manage.

This mirrors how the frontend already works today (`AuthService.ts` / `authStore.ts` decode a JWT client-side and attach it as a Bearer header) — the only thing that changes for the "real Keycloak" path is *where the token comes from*.

---

## User Stories

1. As an officer, I am redirected to the BC Gov Keycloak login page when I access the application unauthenticated, so that I sign in with my IDIR account.
2. As an officer, I am redirected back to the application after a successful login and can use the app without further authentication steps.
3. As an officer, clicking Logout ends my session in both the application and Keycloak.
4. As an admin, I have access to admin routes when my IDIR account carries the `ces-judicial` Keycloak client role.
5. As a developer, running `./docker/manage debug` gives me a mock login by default, so day-to-day development never requires a Keycloak client or touches real IDIR/SSO.

---

## Scope

| Area | In Scope |
|---|---|
| Frontend — OIDC login/logout redirect flow (public client, PKCE) | Yes |
| Frontend — decode user info/roles from the access token client-side | Yes |
| Frontend — router auth guards | Yes (unchanged in shape) |
| Frontend — remove username/password login form (real-Keycloak path only) | Yes |
| Backend — JWT bearer validation against Keycloak's discovery document | Yes |
| Backend — role claim extraction from the validated token | Yes |
| Backend — auth controller / login-callback / cookie / token relay | **No — not needed** |
| Backend — remove mock users and `LocalTokenService` | No — kept for dev bypass, unchanged |
| Dev bypass mode (mock login behind env flag) | Yes — unchanged from today |
| Keycloak realm/client provisioning | No — handled by the SSO team |
| User management in Keycloak | No — IDIR manages identity |

---

## Decisions Made

| Decision | Choice | Rationale |
|---|---|---|
| Identity provider | IDIR only | Government employees only; no BCeID or business accounts |
| Authentication flow | **Frontend-driven, public client, Authorization Code + PKCE** | No secret to manage; API stays a pure resource server; matches how the app already sends a Bearer token today |
| OIDC client type | Public (not confidential) | PKCE removes the need for a client secret in a browser-hosted SPA |
| Frontend library | [`oidc-client-ts`](https://github.com/authts/oidc-client-ts) | Actively maintained, framework-agnostic, resolves all endpoints from a single `authority` URL via OIDC discovery — matches the "just use the well-known config" approach directly |
| Role strategy | Keycloak **client** roles (on the CES client), unchanged | Scoped to the CES client rather than realm-wide; matches existing `RoleConstants` mapping |
| Role claim shape | Flat top-level `roles` claim (array), **not** nested under `resource_access.<clientId>.roles` | Confirmed against a real token from the CES client — a protocol mapper on that client already bubbles client roles up to a top-level `roles` claim. This is simpler to read than the nested shape the original Jasper-derived spec assumed — no JSON sub-parsing needed |
| Role names | `ces-user`, `ces-judicial`, `ces-clerk` → `User`, `Admin`, `Clerk` | Unchanged from today's `RoleConstants` |
| Token audience validation | Disabled (`ValidateAudience = false`); rely on issuer + signature, plus an `azp` (authorized party) check | Keycloak's default access token `aud` is `account`, not the client ID, unless an audience mapper is added on the realm. Avoiding that avoids an extra ask to the SSO team. Issuer validation (pinned to the exact realm authority) + signature validation + confirming `azp` equals the configured client ID is sufficient trust boundary here — `azp` is present on real tokens and costs nothing extra to check |
| Client secret | Not required | Public client + PKCE. Revisit only if the API itself ever needs to mint or exchange tokens (e.g. calling another downstream service on the user's behalf) |
| IDP hint | `kc_idp_hint=idir` passed as an extra query param on the authorize request | Forces login directly to IDIR without an IDP-selector screen |
| Dev bypass | Env-flag-controlled mock login, unchanged from today | Same as before — `./docker/manage debug` never needs a Keycloak client |

---

## Why this is simpler than the original spec

| | keycloak-integration.md (Option B) | keycloak-simplified.md (this spec) |
|---|---|---|
| Client type | Confidential (needs a secret) | Public (PKCE, no secret) |
| API auth scheme | Cookie + OpenIdConnect + JwtBearer (3 schemes) | JwtBearer only |
| API statefulness | Cookie-backed session, server refresh loop | Stateless — validates a Bearer token per request, like today |
| New API endpoints | `GET/POST /api/auth/login`, `/logout`, `/info`, `signin-oidc` callback | None |
| Logout | Hand-built Keycloak end-session URL (`id_token_hint` had to be preserved manually) | `oidc-client-ts`'s `signoutRedirect()` — handled by the library |
| Config values | `Keycloak__Authority`, `Keycloak__Client`, `Keycloak__Secret`, `TokenRefreshThreshold` | `Authority`, `Client`, `Redirect URI` — no secret, no refresh-threshold tuning |
| `X-Forwarded-*` handling | Required (API rewrites its own redirect URI behind the proxy) | Not needed — the browser talks to Keycloak directly using its own origin |

---

## Required Configuration Values

Only **three** values are needed, and none of them is a secret.

| # | Value | Used by | Config key |
|---|---|---|---|
| 1 | Authority (realm base URL) | Frontend + Backend | `VITE_KEYCLOAK_AUTHORITY` / `Keycloak__Authority` |
| 2 | Client ID | Frontend + Backend | `VITE_KEYCLOAK_CLIENT_ID` / `Keycloak__Client` |
| 3 | Redirect URI (post-login callback) | Frontend only | `VITE_KEYCLOAK_REDIRECT_URI` |

> Actual values (Authority URL, Client ID) are intentionally **not written in this spec** — they live only in `docker/.env` / `web/.env.local` (both gitignored) and the deployment pipeline's environment config. See [Toggling Between Dev Bypass and Real Keycloak Locally](#toggling-between-dev-bypass-and-real-keycloak-locally) for where to set them.

Everything else — authorization endpoint, token endpoint, end-session endpoint, JWKS signing keys — is resolved automatically from the Authority via `{authority}/.well-known/openid-configuration`. Neither side hardcodes an endpoint path.

- **Frontend** binds these as Vite env vars (`VITE_KEYCLOAK_*`), same convention as the existing `VITE_API_URL`.
- **Backend** binds `Authority`/`Client` under a `Keycloak` config section, same double-underscore convention as the rest of the app (`Keycloak__Authority`, `Keycloak__Client`).
- **`Keycloak:Enabled`** (backend) / **`VITE_DEV_AUTH_BYPASS`** (frontend) remain the mode toggles — not Keycloak config, just which auth path is active. Both default to the dev-bypass path so `./docker/manage debug` needs zero setup.
- **Client ID is constant across environments**; **Authority is environment-specific** — each environment gets its own realm base URL. Dev is confirmed and recorded outside this spec (see note above); test/prod Authority values still need to be requested from the SSO team when those environments are wired up.

---

## Toggling Between Dev Bypass and Real Keycloak Locally

Answers User Story #5's flip side: a developer needs to exercise the *real* Keycloak flow on demand, without that becoming the default everyone has to fight with day to day.

Two independent flags control this — both already introduced above, both default to the mock path:

| Flag | Layer | Read at | Lives in |
|---|---|---|---|
| `VITE_DEV_AUTH_BYPASS` | Frontend | Vite dev-server / build start | `web/.env` (tracked default `true`) |
| `Keycloak:Enabled` (`Keycloak__Enabled`) | Backend | ASP.NET process start | `docker/.env` / `appsettings.Development.json` (untracked default `false`) |

**To test against real Keycloak locally, without touching the checked-in defaults:**

1. Create/edit `web/.env.local` (gitignored via `*.local` — safe to leave personal values in it):
   ```
   VITE_DEV_AUTH_BYPASS=false
   VITE_KEYCLOAK_AUTHORITY=<dev authority URL — ask the team or check the deployment config, not committed here>
   VITE_KEYCLOAK_CLIENT_ID=<dev client id — same source as above>
   VITE_KEYCLOAK_REDIRECT_URI=http://localhost:9080/auth/callback
   ```
   `.env.local` takes precedence over `.env` in Vite, so this overrides the tracked bypass-on defaults without editing them.

2. In `docker/.env` (already gitignored, copied from `.env.template`), set:
   ```
   Keycloak__Enabled=true
   Keycloak__Authority=<same dev authority URL as above>
   Keycloak__Client=<same dev client id as above>
   ```

3. **Restart, don't just refresh.** Both `VITE_*` vars and `Keycloak__*` vars are read once at process start (Vite dev server / API startup respectively) — a browser refresh alone won't pick up the change. Restart `./docker/manage debug` (or `npm run dev` / `dotnet watch` if running outside Docker).

4. **Flip both together.** The two flags aren't cross-validated — if only one side points at real Keycloak, every API call 401s (frontend sends a Keycloak-issued Bearer token the mock `JwtBearer` scheme can't validate, or vice versa).

5. **Ask the SSO team to allow the local redirect URI.** The dev Keycloak client's *Valid Redirect URIs* must include the developer's local callback (`http://localhost:9080/auth/callback` for the dockerized `web-dev` server) or Keycloak will reject the redirect with an `invalid_redirect_uri` error. Confirm during realm setup (see [Keycloak Realm Setup Requirements](#keycloak-realm-setup-requirements)) whether localhost is already permitted on the dev client or needs to be added per-developer.

To go back to the mock login for day-to-day work, delete/blank `web/.env.local` and set `Keycloak__Enabled=false` (or delete the line — it defaults to `false`), then restart again.

---

## Token Claims

Confirmed against a real access token issued by the CES client (dev realm). Values below are **sanitized placeholders** — GUIDs, name, email, and host are fabricated; only the claim names and overall shape are real:

```json
{
  "exp": 1784226301,
  "iat": 1784226001,
  "iss": "<authority URL — see Required Configuration Values>",
  "sub": "00000000-0000-0000-0000-000000000000",
  "azp": "<client id — see Required Configuration Values>",
  "sid": "11111111-1111-1111-1111-111111111111",
  "allowed-origins": ["<app hostname>"],
  "roles": ["ces-clerk", "ces-judicial", "ces-user"],
  "scope": "openid profile email",
  "email_verified": true,
  "name": "Jane Doe",
  "preferred_username": "<opaque-broker-id>@azureidir",
  "given_name": "Jane",
  "family_name": "Doe",
  "email": "jane.doe@gov.bc.ca"
}
```

| Claim | Description | Example (sanitized) |
|---|---|---|
| `sub` | Keycloak subject — stable per user within this realm; use as the app's user key | `00000000-0000-0000-0000-000000000000` |
| `preferred_username` | Broker-qualified username; suffix reflects BC Gov's **Azure-backed** IDIR broker | `<opaque-broker-id>@azureidir` |
| `name` | Full display name | `Jane Doe` |
| `given_name` / `family_name` | First / last name | `Jane` / `Doe` |
| `email` | Work email | `jane.doe@gov.bc.ca` |
| `roles` | **Flat, top-level** array of CES client roles — already bubbled up via a protocol mapper on this client, not nested under `resource_access` | `["ces-clerk", "ces-judicial", "ces-user"]` |
| `azp` | Authorized party — equals the client ID; used as a defense-in-depth check since `aud` validation is disabled | (matches configured Client ID) |

> **Correction from the original (Jasper-derived) claim assumptions:** there is no `idir_user_guid` or `idir_userid` claim on this client's tokens, and `preferred_username` ends in `@azureidir`, not `@idir`. If a dedicated IDIR GUID is needed later (distinct from Keycloak's own `sub`), that requires a custom mapper request to the SSO team — out of scope unless a concrete need shows up (e.g. cross-referencing another system that already keys off the legacy IDIR GUID).

The frontend reads these directly off the decoded **access token** (same `jwt-decode` dependency already in `web/package.json` — no new decoding library needed). No backend `/api/auth/info` round-trip is required, since the browser already holds the token.

---

## Frontend Changes

### 1. New dependency

Add `oidc-client-ts` to `web/package.json`. `jwt-decode` (already installed) continues to be used for claim extraction — `oidc-client-ts` is only responsible for the redirect/token-exchange/silent-renewal mechanics, not claim parsing, so the existing `decodeAndSetUser` logic in `authStore.ts` is reused almost as-is.

### 2. OIDC manager (new file, `web/src/services/oidcManager.ts`)

```typescript
import { UserManager, WebStorageStateStore } from 'oidc-client-ts'

export const oidcManager = new UserManager({
  authority: import.meta.env.VITE_KEYCLOAK_AUTHORITY,
  client_id: import.meta.env.VITE_KEYCLOAK_CLIENT_ID,
  redirect_uri: import.meta.env.VITE_KEYCLOAK_REDIRECT_URI,
  response_type: 'code',
  scope: 'openid profile email',
  automaticSilentRenew: true,
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  extraQueryParams: { kc_idp_hint: 'idir' },
})
```

`automaticSilentRenew` uses the refresh token issued alongside the access token (standard for Keycloak's default client settings) rather than a hidden iframe — so no `silent-renew.html` static page is needed, and there's no dependency on third-party-cookie behaviour in the browser.

### 3. Login / Logout (`AuthService.ts`)

Add alongside the existing bypass-mode functions (which stay untouched):

```typescript
const loginViaKeycloak = () => oidcManager.signinRedirect()

const logoutViaKeycloak = () => oidcManager.signoutRedirect()

const handleUnauthorized = (currentPath?: string) => {
  const authStore = useAuthStore()
  authStore.clearAuth()
  if (import.meta.env.VITE_DEV_AUTH_BYPASS === 'true') {
    router.push({ name: 'Login', query: { redirect: currentPath } })
  } else {
    loginViaKeycloak()
  }
}
```

### 4. Callback route

Add one new route + view, `web/src/views/AuthCallback.vue`, registered at the path matching `VITE_KEYCLOAK_REDIRECT_URI` (e.g. `/auth/callback`):

```typescript
onMounted(async () => {
  const user = await oidcManager.signinRedirectCallback()
  authStore.setToken(user.access_token)
  router.push('/')
})
```

This is the only backend-free replacement for the old spec's `signin-oidc` callback, `AuthController`, and cookie relay — the whole exchange happens in the browser.

### 5. `authStore.ts`

**Dev bypass path is unchanged** — same `localStorage` token, `setToken`, `decodeAndSetUser`, `isTokenExpired`.

**Keycloak path** reuses the exact same `setToken` / `decodeAndSetUser` functions — the only difference is *what calls `setToken`* (the callback view above, and a subscriber on `oidcManager.events.addUserLoaded` for silent-renewal updates):

```typescript
oidcManager.events.addUserLoaded(user => authStore.setToken(user.access_token))
oidcManager.events.addUserUnloaded(() => authStore.clearAuth())
```

`decodeAndSetUser` needs to handle two different role-claim shapes, since the mock dev token and the real Keycloak token don't match:

- Dev-bypass mock token: singular `role` claim, a single string.
- Keycloak token: plural `roles` claim, already a flat array (`["ces-clerk", "ces-judicial", "ces-user"]`) — see [Token Claims](#token-claims).

`web/src/models/AuthModels.ts` gains the extra fields (all optional, so the mock token — which has none of them — still decodes cleanly):

```typescript
export interface JwtPayload {
  sub: string
  email: string
  exp: number
  iss?: string
  role?: string          // dev-bypass mock token only
  roles?: string[]       // Keycloak token only — already flat, no resource_access nesting
  name?: string
  preferred_username?: string
}

export interface User {
  id: string
  email: string
  roles: string[]
  displayName?: string
}
```

```typescript
function decodeAndSetUser(newToken: string) {
  const decoded = jwtDecode<JwtPayload>(newToken)
  const decodedRoles = decoded.roles ?? (decoded.role ? [decoded.role] : [])

  user.value = {
    id: decoded.sub,
    email: decoded.email,
    displayName: decoded.name,
    roles: decodedRoles,
  }
  roles.value = decodedRoles
}
```

### 6. Axios interceptor (`apiClient.ts`)

**No change.** Both paths already attach `Authorization: Bearer ${authStore.token}` from the store today; the Keycloak path just means that token came from Keycloak instead of `POST /api/auth/login`.

### 7. Router guards

**No structural change.** `authStore.isAuthenticated` / `authStore.roles` continue to drive `meta.roles` checks exactly as today. Only addition: register the `/auth/callback` route unconditionally (harmless in bypass mode — it's simply never navigated to), and gate `/login` behind `VITE_DEV_AUTH_BYPASS === 'true'` as before.

### 8. Environment variables

```
VITE_DEV_AUTH_BYPASS=true          # default — unchanged
VITE_KEYCLOAK_AUTHORITY=
VITE_KEYCLOAK_CLIENT_ID=
VITE_KEYCLOAK_REDIRECT_URI=
```

The three `VITE_KEYCLOAK_*` values are only read when `VITE_DEV_AUTH_BYPASS=false`.

---

## Backend Changes

### 1. New configuration class

`api/CES.API/configuration/KeycloakConfiguration.cs`:

```csharp
namespace CES.API.Configuration
{
    public class KeycloakConfiguration
    {
        public string Authority { get; set; } = string.Empty;
        public string Client { get; set; } = string.Empty;
    }
}
```

No `Secret` field — none is needed.

Add to `appsettings.json`:

```json
"Keycloak": {
  "Enabled": false,
  "Authority": "",
  "Client": ""
}
```

### 2. New authentication extension

`AuthenticationKeycloakExtensions.cs` is replaced entirely — the existing file's contents (commented-out Jasper/JASPER/PCSS/SiteMinder scaffolding) are deleted and replaced with:

```csharp
using CES.API.Configuration;
using CES.Business.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace CES.API.Authentication
{
    public static class AuthenticationKeycloakExtensions
    {
        public static IServiceCollection AddCESKeycloakAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var keycloak = configuration.GetSection("Keycloak").Get<KeycloakConfiguration>()!;

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = keycloak.Authority;
                    options.RequireHttpsMetadata = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = keycloak.Authority,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        RoleClaimType = ClaimTypes.Role,
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = ctx =>
                        {
                            if (ctx.Principal?.Identity is not ClaimsIdentity identity)
                                return Task.CompletedTask;

                            // Defense-in-depth: ValidateAudience is off (Keycloak's default
                            // aud is "account", not the client id), so confirm azp instead —
                            // it's present on every token and equals the client the token
                            // was actually issued to.
                            var azp = ctx.Principal.FindFirst("azp")?.Value;
                            if (!string.Equals(azp, keycloak.Client, StringComparison.Ordinal))
                            {
                                ctx.Fail("Token was not issued to the expected client.");
                                return Task.CompletedTask;
                            }

                            // Roles arrive as a flat top-level "roles" claim (bubbled up via
                            // a protocol mapper on this client) — no resource_access nesting.
                            foreach (var role in ctx.Principal.FindAll("roles").Select(c => c.Value))
                            {
                                var appRole = role switch
                                {
                                    "ces-judicial" => RoleConstants.Admin,
                                    "ces-user" => RoleConstants.User,
                                    "ces-clerk" => RoleConstants.Clerk,
                                    _ => null
                                };
                                if (appRole != null)
                                    identity.AddClaim(new Claim(ClaimTypes.Role, appRole));
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            return services;
        }
    }
}
```

**NuGet packages required:** none beyond what's already referenced (`Microsoft.AspNetCore.Authentication.JwtBearer` is already in use for the dev-bypass scheme).

### 3. `Program.cs`

```csharp
if (builder.Configuration.GetValue<bool>("Keycloak:Enabled"))
    builder.Services.AddCESKeycloakAuthentication(builder.Configuration);
else
    builder.Services.AddCESAuthentication(builder.Configuration); // today's mock JWT, unchanged
```

No rename of the existing `AddCESAuthentication` is needed this time — it keeps its current name and behavior; the new Keycloak path is an additive sibling method. `app.UseAuthentication()` / `app.UseAuthorization()` stay as-is.

### 4. Controllers — no change

`LoginController.cs`, `LogoutController.cs`, `LocalTokenService.cs`, `ITokenService.cs`, `AuthConfiguration.cs` (`UserAuth` section) all stay exactly as they are today, serving the dev-bypass path. No new `AuthController` is added — there's nothing for the API to do in the real-Keycloak login/logout flow, since the browser talks to Keycloak directly.

`[Authorize(Roles = ...)]` usage across `SubmissionsController`, `FilesController`, `UserController` needs **no changes** — `RoleConstants.Admin/User/Clerk` strings are produced identically by both the dev-bypass token and the Keycloak-mapped token.

---

## Keycloak Realm Setup Requirements

To be configured by the SSO team before integration testing:

1. **OIDC client** registered as a **public** client (not confidential):
   - `Standard flow` (Authorization Code) enabled
   - `PKCE Code Challenge Method` = `S256`
   - `Direct access grants` disabled
   - `Client authentication` (confidential) = **off** — this is what makes it public/secret-free
   - Valid redirect URIs per environment (e.g. `http://localhost:9080/auth/callback`, `https://<env-host>/auth/callback`)
   - Valid post-logout redirect URIs per environment
   - **Web origins** set to the app's origin(s) (or `+`) — required because the browser calls Keycloak's token endpoint directly cross-origin; this wasn't needed under the old backend-driven flow

2. **Client roles** on the client (unchanged from the original spec):
   - `ces-clerk`, `ces-user`, `ces-judicial`

3. **Flat `roles` claim mapper** — already configured on the CES client (confirmed by a real sample token); no action needed. This is what lets the backend read `roles` directly instead of parsing `resource_access.<clientId>.roles`.

4. **No client secret** is requested or provisioned.

5. **No audience mapper** is requested — same rationale as the original spec, now for a slightly different reason: the API validates the token's issuer + signature but intentionally skips `aud` validation (see Decisions table).

6. **Test IDIR accounts** assigned to each of the three client roles, for integration testing.

> **Note:** the callback path is now a **Vue route** (`/auth/callback`), not an ASP.NET endpoint — it's rendered entirely client-side.

---

## Open Questions / Follow-up Items

1. **Test/prod Authority + Client ID:** **Resolved** — Client ID stays constant across all environments; Authority (realm base URL) is environment-specific and must be requested per environment. Dev is confirmed and recorded outside this spec (see [Required Configuration Values](#required-configuration-values)); test/prod Authority values are still outstanding.
2. **Refresh-token silent renewal:** **Assumed for now, not yet verified.** Proceeding on the assumption that Keycloak issues refresh tokens by default for this public client, so `automaticSilentRenew` in `oidc-client-ts` can renew without a hidden iframe. If that assumption turns out wrong during implementation, fall back to a `silent-renew.html` static page plus a Web Origins allowance for iframe embedding.
3. **Session/token lifetimes:** **Deferred** — will be confirmed with the SSO team at a later date; not a blocker for initial development.
4. **Audit trail:** **Deferred** — will be confirmed after initial implementation. Development proceeds using Keycloak's `sub` claim as the stable identifier for audit/submission records in the meantime; revisit only if a legacy IDIR GUID turns out to be needed to cross-reference another system.
5. **Multi-tab logout:** **Confirmed acceptable** — if a user logs out in one tab, other tabs get a 401 on their next API call and redirect to Keycloak on their next silent-renew failure. No cross-tab coordination needed.
