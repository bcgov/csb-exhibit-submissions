# Officer Number — Stored on the User Profile

**Ticket:** CES-27
**Status:** Draft — for review
**Depends on:** [keycloak-authentication.md](keycloak-authentication.md), [multi-ticket-exhibit-upload.md](completed/multi-ticket-exhibit-upload.md)

---

## Overview

Officer Number is required on every exhibit submission, but IDIR/Keycloak does **not** expose it as a
claim — it cannot be read from the token. Today it is a free-text field on the Exhibit Upload form
([`SubmissionForm.vue`](../web/src/components/officer/SubmissionForm.vue)) that is optional, unvalidated,
and retyped for every submission.

This spec makes it a **user profile attribute**, collected once and reused:

1. `ApplicationUser` — the row CES already upserts on first login — gains an `OfficerNumber` column.
2. An officer landing on **Court Search** (`/officer/court-list`) with no stored number is shown a modal
   asking for it. It saves to their user row.
3. The value is loaded into `authStore` once per session and used to prefill the **read-only** Officer
   Number field on the Exhibit Upload form.
4. Officer Number becomes **mandatory** — validated on the client and rejected by the API when absent
   or malformed.

The project is not live. Existing `Submissions.OfficerNumber` values are left untouched; no backfill of
`ApplicationUser.OfficerNumber` is performed (it starts null for every user, so every officer is prompted
once).

---

## Decisions

1. **Not a token claim.** The access token is minted by the Keycloak realm and CES cannot add an
   `officer_number` claim to it. The value therefore lives in the CES database and is fetched over the
   API. This is also why it cannot ride on `authStore.user` alone — `decodeAndSetUser` rebuilds `user`
   from the token on **every** refresh (~4 min), so a naively merged field would be wiped. It is held as
   its own store ref and re-projected onto `user` on each decode.
2. **No validation schema exists.** No format is enforced beyond a defensive character allowlist:
   **1–30 characters, `A–Z a–z 0–9 . -` only.** Anything else is stripped as the officer types and
   rejected by the API. Length and pattern live in constants on both sides, never inline.
3. **Dismissible prompt, re-prompted.** The Court Search modal can be closed (backdrop, Esc, Cancel) so
   an officer is never trapped on the page. It reappears on every visit to `/officer/court-list` until a
   number is stored, and the Exhibit Upload page blocks submission without one.
4. **Read-only on the submission form, with an Edit link.** The field renders disabled from the stored
   profile value; **Edit** reopens the same modal, which writes through to the database. There is no
   per-submission override — the profile row stays the single source of truth, so an accidental
   keystroke cannot silently change the number recorded against a submission.
5. **Officers only.** The prompt is scoped to `ROLE_USER`; Admin/Clerk have no officer number and are
   never asked. Nothing outside `/officer/court-list` prompts.
6. **The request still carries the number.** `POST /api/submissions` keeps `officerNumber` on the
   multipart body rather than the API substituting the profile value server-side. Officer number is a
   user-supplied attribute, not an authorization claim — keeping it on the request preserves the existing
   contract, the dev-bypass path, and the `Submissions.OfficerNumber` snapshot semantics (a submission
   records the number **as it was at submission time**, and does not retroactively change if the officer
   later corrects their profile). The API validates it rather than trusting it blindly.
7. **`ApplicationUser` is still not a display source.** Names and email continue to be refreshed from
   token claims on every login ([`UserService.SaveIdentityAsync`](../api/CES.Business/Services/UserService.cs)).
   `OfficerNumber` is the first CES-owned, user-editable column on that row, and `SaveIdentityAsync` must
   not clear it.

---

## Data model

### Changed — `api/CES.Entities/Entities/ApplicationUser.cs`

```csharp
/// <summary>
/// The officer's badge/PIN number, supplied by the officer on first use — IDIR does not
/// expose it as a claim. Null until they provide it; only ever set for officer-role users.
/// </summary>
public string? OfficerNumber { get; set; }
```

### EF — `CES.EF`

- Column configuration in `ModelConfiguration` (alongside the existing `KeycloakSub` config):
  `HasMaxLength(UserConstants.OfficerNumberMaxLength)`, nullable, no index (never queried by).
- Migration **`ApplicationUserOfficerNumber`** — a single `AddColumn<string>` on `ApplicationUsers`,
  `nullable: true`, `maxLength: 30`.

---

## Backend (`/api`)

### Constants — `api/CES.Business/Constants/UserConstants.cs` (new)

```csharp
public static class UserConstants
{
    /// <summary>Ample for any badge/PIN format in use; also the DB column width.</summary>
    public const int OfficerNumberMaxLength = 30;

    /// <summary>
    /// No authoritative schema exists for officer numbers, so this is a defensive allowlist
    /// rather than a format check: alphanumerics, dashes and periods only.
    /// </summary>
    public const string OfficerNumberPattern = @"^[A-Za-z0-9.\-]+$";
}
```

### Models — `api/CES.Business/Models/UserProfileModel.cs` (new)

```csharp
public class UserProfileModel
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? OfficerNumber { get; set; }
}

public class OfficerNumberUpdateModel
{
    public string? OfficerNumber { get; set; }
}
```

### `IUserService` / `UserService`

| Method | Behaviour |
|---|---|
| `Task<UserProfileModel?> GetProfileAsync(string? keycloakSub, string? email)` | Resolves the local row by subject then email (reusing the existing `ResolveUserIdAsync` lookup order) and projects it. Null when no row exists. |
| `Task<UserProfileModel> SetOfficerNumberAsync(int userId, string? officerNumber)` | Trims, validates, persists, stamps `SetUpdateBy(userId)`, returns the updated profile. |

`SetOfficerNumberAsync` validation (all → `ArgumentException` → `400`, per the
[`ApiExceptionMiddleware`](../CLAUDE.md) mapping):

- null/whitespace → `"An officer number is required."`
- length > `OfficerNumberMaxLength` → `"An officer number cannot exceed 30 characters."`
- fails `OfficerNumberPattern` → `"An officer number may contain only letters, numbers, dashes and periods."`

`KeyNotFoundException` → `404` when the id resolves to no row.

`SaveIdentityAsync` is **not** changed — it assigns only the identity columns, so `OfficerNumber`
survives every subsequent login. A regression test locks this in.

### Controller — `api/CES.API/Controllers/UserController.cs` (rewritten)

The existing `POST /api/users/createUser` action is dead code (no caller anywhere in the repo) and is
removed with this change.

| Method | Route | Auth | Returns |
|---|---|---|---|
| `GET` | `/api/users/me` | `[Authorize]` | `200 UserProfileModel`; `404` when the caller has no local row |
| `PUT` | `/api/users/me/officer-number` | `[Authorize]` | `200 UserProfileModel`; `400` invalid; `404` no local row |

Both resolve the caller through `User.GetSubject()` / `User.GetEmail()`
([`ClaimsPrincipalExtensions`](../api/CES.API/Authentication/ClaimsPrincipalExtensions.cs)) — never from a
route or body parameter, so one user can never read or write another's profile.

### Submission validation — `SubmissionsController` / `SubmissionService`

`OfficerNumber` on `EvidenceSubmissionModel` is validated on **create** (not on append to an existing
submission, which carries no officer number): same three rules as above, throwing `ArgumentException`.
The property stays `string?` on the model so the failure surfaces as a `400` with a readable message
rather than a framework binding error.

---

## Frontend (`/web`)

### Constants — `web/src/constants/user.ts` (new)

`OFFICER_NUMBER_MAX_LENGTH = 30`, `OFFICER_NUMBER_PATTERN = /^[A-Za-z0-9.-]+$/`, and
`sanitizeOfficerNumber(raw: string): string` — strips disallowed characters and clamps to the max
length, applied on input so the officer cannot type an invalid value in the first place.

### Models

- `web/src/models/UserProfileModel.ts` (new) — mirrors `UserProfileModel`.
- `web/src/models/AuthModels.ts` — `User` gains `officerNumber?: string | null`.

### Service — `web/src/services/UserService.ts` (new)

`getProfile(): Promise<UserProfileModel | null>` (a `404` resolves to `null`, not a throw — a user
without a local row is an ordinary state) and
`saveOfficerNumber(officerNumber: string): Promise<UserProfileModel>`.

### Store — `web/src/stores/authStore.ts`

```
officerNumber      ref<string | null>   // profile-sourced, NOT from the token
profileLoaded      ref<boolean>
hasOfficerNumber   computed             // non-empty
setOfficerNumber(value)                 // updates the ref and user.officerNumber
loadProfile()                           // single-flight; no-op once loaded
```

- `decodeAndSetUser` projects `officerNumber.value` onto the rebuilt `user`, so a token refresh no
  longer loses it.
- `clearAuth` resets both new refs and the in-flight promise.
- `loadProfile` is single-flight (same guard style as `sessionService.refresh`) and swallows failures —
  a profile fetch that fails must not break an authenticated session; the officer is simply re-prompted.

### Hydration points

| Path | Where |
|---|---|
| Keycloak — first login | `AuthCallbackView` after `setToken` |
| Keycloak — reload | `sessionService.performRefresh`, guarded so only the **first** refresh of a page load fetches (subsequent renewals no-op via `profileLoaded`) |
| Dev bypass | `AuthService.login` after `setToken` |

Awaiting the profile is never a precondition for navigation — the modal simply appears once the fetch
lands.

### Component — `web/src/components/officer/OfficerNumberModal.vue` (new)

Overlay + `role="dialog" aria-modal="true"` markup matching
[`ExhibitDetailModal.vue`](../web/src/components/shared/ExhibitDetailModal.vue). Props: `initialValue?`.
Emits: `close`, `saved(officerNumber)`.

- Single labelled text input, `maxlength=30`, sanitized on input, autofocused.
- Explanatory copy: the number is saved to their profile and reused on every future submission.
- **Save** disabled while empty or saving; shows a spinner during the `PUT`; renders the API's message on
  failure and stays open.
- **Cancel** / backdrop / Esc closes without saving (Decision 3).
- Styling from the SCSS aliases in `web/src/styles/_variables.scss` — no literal colours or sizes.

### `CourtListing.vue`

Mounts the modal when `authStore.hasRole(ROLE_USER) && authStore.profileLoaded && !authStore.hasOfficerNumber`.
Because the profile load is async, the trigger is a `watch` on that condition rather than an `onMounted`
check, so the modal appears when the fetch resolves. Dismissal is remembered for the page visit only.

### `SubmissionForm.vue`

- `officerNumber` is a computed read of `authStore.officerNumber`; the local `ref` is removed.
- The input becomes `disabled` with a required marker, and an **Edit** button beside it opens
  `OfficerNumberModal`; saving updates the store, so the field reflects the new value immediately.
- When no number is stored, the field shows a "Not set" state, the modal is offered inline, and
  **Upload is disabled** — the client-side half of the mandatory rule.

---

## Testing

### Backend — `CES.Business.Tests`

| Test | Assertion |
|---|---|
| `SetOfficerNumberAsync` valid | Persists trimmed value; returns the updated profile |
| `SetOfficerNumberAsync` null/whitespace | `ArgumentException` |
| `SetOfficerNumberAsync` > 30 chars | `ArgumentException` |
| `SetOfficerNumberAsync` disallowed characters (`ABC 123`, `AB/12`) | `ArgumentException` |
| `SetOfficerNumberAsync` allows dashes and periods | Persists `A-1.2` unchanged |
| `SetOfficerNumberAsync` unknown user id | `KeyNotFoundException` |
| `GetProfileAsync` by subject / by email / no match | Correct row, correct fallback, null |
| `UpsertFromTokenAsync` on an existing user | **Does not clear** `OfficerNumber` (regression lock for Decision 7) |

### Backend — `CES.API.Tests`

`GET /api/users/me` unauthenticated → `401`; authenticated → `200` with the caller's profile.
`PUT /api/users/me/officer-number` → `200` on valid, `400` on invalid.
`POST /api/submissions` with a missing/invalid `officerNumber` → `400`. Existing submission tests keep
posting `OFF001`, which stays valid under the allowlist.

### Frontend — Vitest

- `constants/user`: `sanitizeOfficerNumber` strips spaces/slashes, keeps `.`/`-`, clamps at 30.
- `authStore`: `loadProfile` is single-flight; `officerNumber` **survives** a `setToken` refresh;
  `clearAuth` resets it.
- `UserService`: `getProfile` maps `404` → `null`; `saveOfficerNumber` posts the expected body.
- `OfficerNumberModal`: Save disabled when empty; emits `saved` on success; stays open and shows the
  message on API failure; Cancel emits `close` without a request.
- `SubmissionForm`: prefills from the store; Upload disabled with no stored number.

Existing `SubmissionService.spec.ts` and the submission form tests are updated for the new source of the
field — not deleted.

---

## Manual validation steps

1. Log in as an officer with no stored number → land on Court Search → modal appears.
2. Type `AB 12/34` → the field shows `AB1234`; type 40 characters → clamped to 30.
3. Cancel → modal closes; navigate away and back → modal reappears.
4. Save `PC-1234` → modal closes; reload the page → no modal (value came back from the API).
5. Court Search → select tickets → Upload Exhibit → Officer Number shows `PC-1234`, disabled.
6. Submit an exhibit → the submission record carries `PC-1234`.
7. **Edit** on the submission page → change to `PC.99` → save → field updates; a new submission records
   the new value while the earlier one keeps `PC-1234`.
8. Log out and back in → the number is still there, and the display name/email still track IDIR.
9. Log in as Admin/Clerk → no modal anywhere.
