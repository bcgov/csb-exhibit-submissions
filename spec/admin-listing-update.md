# Admin Listing Update — Historical View, Search, and Submission Lifecycle

**Status:** Draft
**Date:** 2026-06-23
**JIRA:** CES-31

---

## Overview

The admin **Submission Listing** and **Submission Review** screens were built incrementally alongside [multi-ticket-exhibit-upload.md](multi-ticket-exhibit-upload.md) and [exhibit-classification.md](exhibit-classification.md). As those features landed, the admin side accumulated semantic drift: there is no real submission status, `IsDeleted` is overloaded to mean three different things, accepted/rejected submissions disappear from the list, and the listing cannot serve as a historical record.

This spec realigns the admin experience around a clear separation between a **Submission** and an **Exhibit**, introduces an explicit submission lifecycle (`Pending` / `Accepted` / `Rejected`), adds a search/filter panel above the listing, makes the review screen's exhibit fields admin-editable (with no officer-style locks), and corrects the Accept/Reject semantics so the listing becomes a durable historical view.

---

## Terminology — Submission vs Exhibit

These two concepts are currently blurred in the code and UI. This spec treats them as strictly distinct, and all naming should follow this from here on.

| Concept        | Definition                                                                                                            | Entity        | Lifecycle states                                  |
| -------------- | --------------------------------------------------------------------------------------------------------------------- | ------------- | ------------------------------------------------- |
| **Submission** | The collection of files submitted into evidence for a specific court appearance date. The unit admin accepts/rejects. | `Submission`  | `Pending` → `Accepted` \| `Rejected`              |
| **Exhibit**    | An individual file that makes up a submission. The unit officers/admin classify.                                      | `StoredFiles` | `Unclassified` / `Marked` / `Entered` / `Removed` |

> A Submission is **Accepted** or **Rejected**. An Exhibit is **Marked**, **Entered**, or **Removed**. Do not describe a submission as "Removed" or an exhibit as "Rejected."

---

## Current State Audit

The behaviours below were verified in the codebase and motivate this spec.

1. **No submission status field.** `Submission` ([Submission.cs](../api/CES.Entities/Entities/Submission.cs)) has only `IsDeleted` (inherited from `BaseEntity`). The listing UI hardcodes the literal string `"Pending"` for every row ([SubmissionListing.vue:112](../web/src/components/admin/SubmissionListing.vue#L112)).

2. **`IsDeleted` is overloaded across three meanings:**
   - **Accept** marks each accepted file `IsDeleted = true` and the whole submission `IsDeleted = true` when all files are processed ([SubmissionService.cs:85-90](../api/CES.Business/Services/SubmissionService.cs#L85-L90)).
   - **Reject** deletes files from disk and sets submission `IsDeleted = true` ([SubmissionService.cs:96-114](../api/CES.Business/Services/SubmissionService.cs#L96-L114)).
   - **Remove exhibit** sets a single file `IsDeleted = true` ([SubmissionService.cs:116-130](../api/CES.Business/Services/SubmissionService.cs#L116-L130)).

3. **No historical view.** The listing query filters out every `IsDeleted` submission ([SubmissionService.cs:61](../api/CES.Business/Services/SubmissionService.cs#L61)). Because Accept and Reject both set `IsDeleted`, **accepted and rejected submissions vanish from the listing** — the opposite of the durable historical record we want.

4. **Status semantics crossed over.** `DeriveStatus` ([StoredFilesExtensions.cs:7-13](../api/CES.Business/Extensions/Entities/StoredFilesExtensions.cs#L7-L13)) returns `"Removed"` for any `IsDeleted` file. But Accept _also_ sets accepted files `IsDeleted = true`, so an accepted exhibit derives as **"Removed"** — exhibit-level and submission-level outcomes are conflated.

5. **Accept does not gate on terminal states.** `AcceptSubmissions` accepts whatever file ids the client sends, with no rule that all exhibits first be Entered or Removed. Each file is zipped individually with a metadata + SHA256 manifest by `LocalFileStorage.AcceptAsync` ([LocalFileStorage.cs:64-152](../api/CES.API/FileStorage/LocalFileStorage.cs#L64-L152)).

6. **Listing omits files.** `RetrieveSubmissionListing` includes only `Tickets`, not `Files` ([SubmissionService.cs:58-66](../api/CES.Business/Services/SubmissionService.cs#L58-L66)), so the list cannot show exhibit counts or derive readiness.

7. **No search/filter.** The listing renders the full set unfiltered; pagination is stubbed and hidden (`v-show="false"`, [SubmissionListing.vue:117](../web/src/components/admin/SubmissionListing.vue#L117)).

8. **Audit history is partial.** A generic `SubmissionAuditLog` table exists ([SubmissionAuditLog.cs](../api/CES.Entities/Entities/SubmissionAuditLog.cs)) but is written **only** for exhibit classification changes (`MarkedValue` / `EnteredValue` / `Description`) in `FileService` ([FileService.cs:43-51,86-94,115-123](../api/CES.Business/Services/FileService.cs#L43-L51)). Accept, Reject, exhibit removal, and submission-level edits are **not** logged.

---

## Submission Lifecycle State Machine

Each submission is in exactly one **status**, stored explicitly (not derived from `IsDeleted`).

| Status     | Meaning                                                                                            | Terminal |
| ---------- | -------------------------------------------------------------------------------------------------- | -------- |
| `Pending`  | Submitted by an officer; exhibits may still be classified/removed. Default on creation.            | No       |
| `Accepted` | Admin packed the submission: every exhibit is Entered or Removed, files zipped + metadata written. | Yes      |
| `Rejected` | Admin rejected the whole submission; all associated files deleted from storage and unretrievable.  | Yes      |

### Allowed transitions

| From      | To         | Trigger                                                                               |
| --------- | ---------- | ------------------------------------------------------------------------------------- |
| `Pending` | `Accepted` | Admin clicks **Accept** _and_ every exhibit is in a final state (Entered or Removed). |
| `Pending` | `Rejected` | Admin confirms **Reject** (warning shown — all files will be permanently deleted).    |

### Disallowed transitions

- **Accepted → anything** and **Rejected → anything**: both are terminal. The review screen renders read-only for these submissions (no Accept/Reject/edit actions; Rejected exhibits are not viewable).
- **Pending → Accepted while any exhibit is `Unclassified` or `Marked`-only**: blocked in the UI (disabled Accept button with reason) and on the backend (returns a validation error).

### Exhibit states (unchanged from [exhibit-classification.md](exhibit-classification.md))

`Unclassified` / `Marked` / `Entered` / `Removed`. **We keep the label `Removed`** for a deleted exhibit (file deleted from disk, record retained, not viewable, deletion timestamped). The submission-level outcome (`Rejected`) and the exhibit-level outcome (`Removed`) stay distinct.

> **Accept readiness rule:** a submission is acceptable iff, for every exhibit, `IsDeleted == true` (Removed) **or** `EnteredValue != null` (Entered).

---

## Scope

### In scope

- Explicit `Submission.Status` field replacing the `IsDeleted` overloading for accept/reject.
- Listing shows **all** submissions (Pending, Accepted, Rejected) — true historical view.
- Search/filter panel: submission date range, file number, accused name, status.
- Listing shows real per-row status and exhibit count.
- Review screen: admin-editable exhibit classification (Marked / Entered / Description) with **no** edit-window and **no** entered-lock.
- Review screen shows **Removed** exhibits (greyed, not viewable) instead of hiding them.
- Accept gated on the readiness rule; sets `Accepted`, no longer flags `IsDeleted`.
- Accept produces **one zip per submission** (all retained exhibits + a single combined metadata manifest), replacing the current per-exhibit zipping.
- Reject reframed as whole-submission rejection → `Rejected`, with an explicit destructive warning.
- Record an exhibit's deletion timestamp.
- Admin can remove an `Entered` exhibit (bypassing the officer-only guard), but only while the submission is `Pending`.
- **Server-side pagination** on the listing, alongside the filters.

### Out of scope (future specs)

- **Location and room filters** — require lookup tables; deferred until confirmed necessary.
- **Submission-level audit history** for accept/reject/remove/edit actions — the gap is documented below; a dedicated `submission-audit-history` spec will follow.
- **Downstream CHUNK pipeline** — the per-submission zip + manifest produced at Accept is the CHUNK-bound artifact (it lands via `LocalFileStorage`), but the pipeline that consumes those artifacts is out of scope here.
- Submission/ticket **metadata** editing (location, room, officer number, ticket info) — admin edit scope is limited to exhibit classification this round.
- **Reopening terminal submissions** — `Accepted`/`Rejected` are permanent; no reopen-to-`Pending` path.

---

## Decisions Made

| #   | Decision                                                                                                                                  | Rationale                                                                                                       |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| 1   | Submission statuses are exactly `Pending` / `Accepted` / `Rejected`.                                                                      | Matches the three real outcomes; `Rejected` is the delete-all terminal state.                                   |
| 2   | Keep the exhibit label `Removed` (do **not** rename to "Deleted").                                                                        | Avoids churn on the officer flow and existing tests; keeps exhibit `Removed` vs submission `Rejected` distinct. |
| 3   | Admin review edits **exhibit classification only** (Marked / Entered / Description).                                                      | Submission/ticket metadata editing deferred; keeps surface and validation focused.                              |
| 4   | Admin can always edit classification, even on `Entered` exhibits.                                                                         | Admin overrides the officer-only terminal lock and 10-second window.                                            |
| 5   | `IsDeleted` is **no longer** set by Accept; it remains a true soft-delete flag (used by exhibit `Removed` and by Reject's file deletion). | Removes the overloading that hid accepted submissions.                                                          |
| 6   | Submission-level audit logging is **deferred** to a future spec.                                                                          | The existing `SubmissionAuditLog` table and classification logging stay; new admin actions are not yet logged.  |
| 7   | Admin **can remove an `Entered` exhibit**, but only while the submission is `Pending`.                                                    | Extends "admin can always modify"; terminal submissions stay immutable.                                         |
| 8   | Accept produces **one zip per submission** with a single combined metadata manifest (replaces per-exhibit zips).                          | This is the CHUNK-bound artifact; one archive per submission matches the unit admin accepts.                    |
| 9   | Listing ships **server-side pagination** with the filters.                                                                                | Historical view will grow unbounded; paging belongs server-side from the start.                                 |
| 10  | `Accepted`/`Rejected` are **permanent** — no reopen path.                                                                                 | Terminal states keep the historical record trustworthy.                                                         |
| 11  | Migration backfill classifies existing `IsDeleted` submissions as `Accepted` (dev-only; no data deletion).                                | Dev data is disposable but kept testable; can be purged manually if it gets in the way.                         |

---

## Constants

No new prices/rates. Reuse existing classification constants (`ENTERED_MIN`, `ENTERED_MAX`, `MARKED_MIN`, `DESCRIPTION_MAX_LENGTH`) from [constants/classification](../web/src/constants/classification) and `ClassificationConstants` on the backend. Any new literal (e.g. default page size for the listing) must be a named constant per the project rule — do not inline.

| Constant                          | Location                              | Purpose                                                                                                                                           |
| --------------------------------- | ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SUBMISSION_LIST_PAGE_SIZE`       | frontend constants                    | Default listing page size (replaces inline `pageSize = 10` at [SubmissionListing.vue:59](../web/src/components/admin/SubmissionListing.vue#L59)). |
| `DefaultPageSize` / `MaxPageSize` | backend constants (`PagingConstants`) | Server-side default + cap for the listing endpoint's `pageSize` query param, so a client cannot request an unbounded page.                        |

---

## Backend Changes

### 1. Database Schema

#### Modified entity: `Submission`

Add an explicit status and a status-change timestamp.

```csharp
public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
public DateTime? StatusChangedDateUTC { get; set; }   // set on Accept/Reject
```

New enum (`CES.Entities/Enums/SubmissionStatus.cs`), stored as a string column for readability and forward-compatibility:

```csharp
public enum SubmissionStatus { Pending = 0, Accepted = 1, Rejected = 2 }
```

#### Modified entity: `StoredFiles`

Record when an exhibit was removed (the "date when something is deleted" requirement):

```csharp
public DateTime? DeletedAtUTC { get; set; }   // set when IsDeleted flips true
```

#### Migration

- Add `Submission.Status` (default `Pending`), `Submission.StatusChangedDateUTC`, `StoredFiles.DeletedAtUTC`.
- **Backfill (dev-only):** all data is currently dev data, so loss is not a concern. Rather than deleting, classify every existing `Submission.IsDeleted == true` row as `Accepted` and clear `IsDeleted` (set `Status = Accepted`, `IsDeleted = false`) so the rows remain visible and testable in the new historical listing. `Pending` rows (`IsDeleted == false`) are left as-is. The team can manually purge any stale rows later if they get in the way.
- After this migration, **stop** treating `Submission.IsDeleted` as accept/reject. It is retained only as a genuine soft-delete safety flag.

### 2. Service Layer (`SubmissionService`)

#### `RetrieveSubmissionListing` → filtered, paged, includes files, returns all statuses

- Remove the `!s.IsDeleted` filter; return Pending, Accepted, and Rejected submissions.
- `Include(s => s.Files)` — **all** files, so the model can compute readiness and counts. The **Exhibits** column shows the _active_ (non-`Removed`) count; readiness still considers every file.
- Accept a filter (with paging) and apply it server-side, returning a paged result with the total count:

```csharp
public class SubmissionListFilter
{
    public DateTime? SubmissionDateFrom { get; set; }
    public DateTime? SubmissionDateTo { get; set; }
    public string? FileNumberText { get; set; }   // matches any ticket on the submission
    public string? AccusedName { get; set; }       // contains, case-insensitive
    public SubmissionStatus? Status { get; set; }
    public int Page { get; set; } = 1;             // 1-based
    public int PageSize { get; set; }              // defaulted/capped via PagingConstants
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

Filtering rules (applied **before** paging, so `TotalCount` reflects the filtered set):

- **Date range** → `UploadDate` between `From`/`To` (inclusive, null-tolerant on each bound).
- **File number** → `s.Tickets.Any(t => EF.Functions.ILike(t.FileNumberText, $"%{filter.FileNumberText}%"))`.
- **Accused name** → `s.Tickets.Any(t => EF.Functions.ILike(t.AccusedName, $"%{name}%"))`.
- **Status** → `s.Status == filter.Status`.

Paging: order deterministically (e.g. `UploadDate` desc, then `Id`), then `.Skip((Page-1)*PageSize).Take(PageSize)`. `PageSize` is clamped to `PagingConstants.MaxPageSize` and defaulted to `DefaultPageSize` when unset.

#### `RetrieveSubmission` → include removed exhibits

Change `Include(s => s.Files.Where(f => !f.IsDeleted))` ([SubmissionService.cs:49](../api/CES.Business/Services/SubmissionService.cs#L49)) to include **all** files. The review screen needs to display Removed exhibits (greyed, not viewable) for historical completeness. The review model must carry each file's `Status` (already derived) and removal state so the UI can render and disable appropriately.

#### `AcceptSubmissions` → gate on readiness, set status, stop overloading `IsDeleted`

```
1. Load submission with all files + tickets. Block if Status != Pending.
2. Validate readiness: every file must satisfy (file.IsDeleted || file.EnteredValue != null).
   - If not, return a validation failure naming the unready exhibits.
3. _fileStorage.AcceptSubmissionAsync(submission)  // ONE zip for the whole submission
4. submission.Status = Accepted; submission.StatusChangedDateUTC = SystemDate.UtcNow();
   // DO NOT set file.IsDeleted or submission.IsDeleted.
5. SaveChanges.
```

The current per-file checkbox selection (`acceptedFiles`) is replaced by the all-or-nothing readiness rule; the request no longer needs a per-file list (see API contract). Accepted exhibits keep their `Entered` status (they are no longer mislabelled `Removed`).

#### `LocalFileStorage` → `AcceptSubmissionAsync(Submission submission)`

Replaces the per-exhibit `AcceptAsync` ([LocalFileStorage.cs:64-152](../api/CES.API/FileStorage/LocalFileStorage.cs#L64-L152)) with a single archive per submission:

- One zip named per submission (e.g. `{shortDate}_{submissionId}.zip`) written under `AcceptedPath`.
- Contains every **retained** exhibit (skip `Removed` files — their bytes are already gone), each under its `OriginalFileName` (de-duplicate names if two originals collide).
- A single combined `metadata.json` describing the submission (location, room, officer, tickets) plus an `exhibits[]` array — one entry per included file with its classification, timestamps, description, and `SHA256` (reusing `CryptographyService.ComputeSHA256Async`). Each exhibit's hash lives on its `metadata.json` entry; there is no separate hash file.

> Keep the existing `ZipArchive` dispose-ordering discipline from `AcceptAsync` (Central Directory written before the stream is flushed). Removed exhibits are recorded in the manifest as metadata only (status `Removed`, deletion timestamp), with no file bytes.

#### `RejectSubmissions` → whole-submission rejection

```
1. Load submission with all files. Block if Status != Pending.
2. For each non-deleted file: _fileStorage.DeleteAsync(file); file.IsDeleted = true; file.DeletedAtUTC = now.
3. submission.Status = Rejected; submission.StatusChangedDateUTC = now.
   // DO NOT set submission.IsDeleted (status carries the meaning).
4. SaveChanges.
```

#### `RemoveFileAsync` → admin can remove Entered, gated on Pending, records deletion timestamp

This endpoint is admin-only. Changes from current behaviour ([SubmissionService.cs:116-130](../api/CES.Business/Services/SubmissionService.cs#L116-L130)):

- **Drop** the `EnteredValue != null` guard — admin can remove an `Entered` exhibit.
- **Add** a guard that the parent submission's `Status == Pending`; removing from an `Accepted`/`Rejected` submission is rejected (those are terminal/immutable).
- Set `file.DeletedAtUTC = SystemDate.UtcNow()` alongside the existing `IsDeleted = true` / `SetUpdateBy("Admin")`.

### 3. Admin classification edits (always-on)

Admin edits reuse the existing `POST /api/files/{id}/mark`, `POST /api/files/{id}/enter`, `PATCH /api/files/{id}/description` endpoints, but admin must bypass the officer-only locks in `FileService` (the `EnteredValue != null` rejections at [FileService.cs:31,74,105](../api/CES.Business/Services/FileService.cs#L31)) and the entered-correction window.

Proposed approach: thread an `isAdminOverride` flag (derived from the caller's role in the controller) into `MarkExhibitAsync` / `EnterExhibitAsync` / `UpdateExhibitDescriptionAsync`. When true:

- Skip the `Entered exhibits cannot be modified` guards.
- Skip the `ClassificationEditWindowSeconds` terminal lock on re-enter.
- Still validate value ranges (A–Z, 1–50, description length).
- Still write the existing `SubmissionAuditLog` entry with `ChangedBy` = admin identity (this classification logging already exists and is **not** part of the deferred submission-audit work).

> Admin's `Remove` also bypasses the entered guard (see `RemoveFileAsync` above) — but only on a `Pending` submission. All admin classification/removal edits are blocked once the submission is terminal.

### 4. API Contract (`SubmissionsController`)

| Endpoint                                 | Change                                                                                                                                                                                                                                                        |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/submissions/listing`           | Accept filter + paging query params (`submissionDateFrom`, `submissionDateTo`, `fileNumberText`, `accusedName`, `status`, `page`, `pageSize`); return a `PagedResult` with `items` (status + active exhibit count per row), `totalCount`, `page`, `pageSize`. |
| `GET /api/submissions/retrieve`          | Include Removed exhibits; response carries per-file status + removal flag.                                                                                                                                                                                    |
| `POST /api/submissions/accept`           | Body simplified to `{ submissionId }`; enforce readiness rule; 422 with reason when not ready; produces one zip per submission.                                                                                                                               |
| `POST /api/submissions/reject`           | Body simplified to `{ submissionId }`; deletes all files; sets `Rejected`.                                                                                                                                                                                    |
| `DELETE /api/submissions/files/{fileId}` | Records `DeletedAtUTC`.                                                                                                                                                                                                                                       |

All admin endpoints remain `[Authorize(Roles = "Admin")]`.

### 5. File viewing for removed/rejected exhibits

Confirmed: `FilesController.View`/`Download` ([FilesController.cs:64-90](../api/CES.API/Controllers/FilesController.cs#L64-L90)) currently apply **no** `IsDeleted` guard — `RetrieveFileMetaData` returns metadata for removed files, and `GetAsync` then throws `FileNotFoundException` (a 500) since the bytes are gone. Add a guard so both endpoints return a clean 404/410 when `file.IsDeleted == true` (Removed exhibits and all files of a Rejected submission). The UI must also not surface a View/Download control for them.

> Out of scope but noted: `[Authorize(Roles = "Admin")]` is commented out on these endpoints ([FilesController.cs:63,79](../api/CES.API/Controllers/FilesController.cs#L63)) — an existing WIP security gap, tracked separately.

---

## Frontend Changes

### 1. Submission Listing (`SubmissionListing.vue`)

**Filter panel** above the table:

- **Submission date range** — two date inputs (from / to).
- **File number** — text input (matches any ticket on the submission).
- **Accused name** — text input (contains match).
- **Status** — select: All / Pending / Accepted / Rejected.
- **Apply** and **Clear** actions; filters passed to `retrieveSubmissionListing(filter)`.

**Table changes:**

- Replace the hardcoded `"Pending"` cell ([SubmissionListing.vue:112](../web/src/components/admin/SubmissionListing.vue#L112)) with the real `item.status`, rendered as a status chip (Pending / Accepted / Rejected).
- Show all statuses (historical view). Optionally visually de-emphasize Rejected rows.
- Add an **Exhibits** count column (`item.exhibitCount`, active/non-removed).
- **Real server-side pagination**: replace the dead `nextPage`/`prevPage` re-fetch hack ([SubmissionListing.vue:65-71](../web/src/components/admin/SubmissionListing.vue#L65-L71)) with controls that send `page`/`pageSize` and render from the `PagedResult` (`totalCount` drives the page count). Default `pageSize` from `SUBMISSION_LIST_PAGE_SIZE`. Changing a filter resets to page 1.

### 2. Submission Review (`SubmissionReview.vue`)

**Editable exhibit classification** — port the officer's Prior-Exhibit controls ([SubmissionForm.vue:411-451](../web/src/components/officer/SubmissionForm.vue#L411-L451)) into the admin file rows, but:

- **No** 10-second edit window and **no** entered-lock — every Marked/Entered/Description control is always enabled for a `Pending` submission (the requirement: "admin can always modify fields even if entered").
- Wire `@change`/`@blur` to `markExhibit` / `enterExhibit` / `updateExhibitDescription` with the admin override path.
- Reuse the save-indicator pattern (✓ / ✕) for feedback.

**Removed exhibits are shown, not hidden** — render Removed rows greyed with a `Removed` chip, no View/Download/Remove actions. (Backed by the `RetrieveSubmission` change to include deleted files.)

**Accept** — gate the button:

- Enabled only when every exhibit is `Entered` or `Removed`. Otherwise disabled with a tooltip listing the blocking exhibits ("3 exhibits not yet Entered or Removed").
- Replaces the current per-file checkbox selection ([SubmissionReview.vue:172-180](../web/src/components/admin/SubmissionReview.vue#L172-L180)) — Accept is now all-or-nothing on the ready submission.

**Reject** — relabel and reframe the existing `Reject / Delete All` action ([SubmissionReview.vue:209-223](../web/src/components/admin/SubmissionReview.vue#L209-L223)) as whole-**submission** rejection. The confirmation modal warning must state plainly:

> Rejecting this submission permanently deletes **all** associated files. This cannot be undone and the files are unretrievable.

**Read-only terminal states** — when `submission.status` is `Accepted` or `Rejected`, hide Accept/Reject/edit controls; the screen is a read-only historical record.

### 3. TypeScript model updates

- `SubmissionReviewModel`: add `status: 'Pending' | 'Accepted' | 'Rejected'`, `statusChangedDate?: string`, and `exhibitCount` (listing rows).
- `SubmissionFile`: add `deletedAt?: string | null` (already carries `status`).
- New `SubmissionListFilter` interface (filter fields + `page`/`pageSize`) and a `PagedResult<T>` interface (`items`, `totalCount`, `page`, `pageSize`) for the listing response.
- Replace `SubmissionAcceptanceModel` (`{ fileId, acceptedFiles }`) with `{ submissionId }` for accept/reject; update `SubmissionService.ts` callers ([SubmissionService.ts:81-91](../web/src/services/SubmissionService.ts#L81-L91)).
- `retrieveSubmissionListing(filter?: SubmissionListFilter)` passes filter + paging as query params and returns `PagedResult<SubmissionReviewModel>`.

---

## Audit History (gap + deferral)

**Today:** `SubmissionAuditLog` logs only exhibit classification field changes (Marked / Entered / Description) via `FileService`. It does **not** capture Accept, Reject, exhibit Remove, or admin field edits at the submission level.

**This spec:** classification logging continues to work (including admin overrides, which record `ChangedBy` = admin). No new audit logging is added for the lifecycle actions.

**Follow-up:** a dedicated **`submission-audit-history`** spec should extend the existing table (or a sibling log) to record every state transition (`Pending→Accepted`, `Pending→Rejected`), exhibit removals, and admin edits, with actor + timestamp, to give a fully reconstructable history per submission and per exhibit. The schema already in place (`SubmissionId`, `FileId`, `FieldName`, `OldValue`, `NewValue`, `ChangedBy`, `ChangedAtUTC`) is a suitable foundation.

---

## Testing

Per project rules, all new/changed behaviour needs tests; both suites must pass (`dotnet test api/CES.API/CES.API.sln` and `cd web && npm run test`).

### Backend (xUnit)

- **Listing** returns Pending, Accepted, and Rejected submissions (historical view) — and that Accept/Reject no longer remove rows.
- **Filters**: date range, file number, accused-name contains, and status each narrow the result correctly, and combine.
- **Paging**: `TotalCount` reflects the filtered set (not the page); `pageSize` is clamped to `MaxPageSize`; out-of-range `page` yields an empty page, not an error.
- **Accept readiness**: rejects when any exhibit is Unclassified or Marked-only; succeeds when all are Entered/Removed; sets `Accepted` + `StatusChangedDateUTC`; does **not** set `IsDeleted`; accepted exhibits keep `Entered` status (not `Removed`).
- **Accept packaging**: produces exactly **one** zip per submission containing every retained exhibit plus one combined `metadata.json` (which carries each exhibit's `SHA256`); Removed exhibits appear as metadata-only entries (no bytes).
- **Reject**: deletes all files, sets each `IsDeleted` + `DeletedAtUTC`, sets submission `Rejected`, does not set submission `IsDeleted`.
- **RemoveFile**: sets `DeletedAtUTC`; **succeeds on an `Entered` exhibit** when the submission is `Pending`; **rejected** when the submission is `Accepted`/`Rejected`.
- **Admin override**: mark/enter/description succeed on an already-`Entered` exhibit when `isAdminOverride` is true, and still write an audit entry; value-range validation still enforced.
- **Terminal guards**: Accept/Reject/remove/edit rejected on a non-`Pending` submission.

### Frontend (Vitest)

- Filter panel builds the correct query params and triggers re-fetch on Apply/Clear.
- Listing renders real status chips and exhibit counts; Rejected rows visible.
- Review: Accept disabled with reason until all exhibits Entered/Removed.
- Review: Reject modal shows the destructive warning copy.
- Review: classification controls always enabled on Pending; absent on Accepted/Rejected.
- Removed exhibits render greyed with no View/Download.
- Pagination controls send `page`/`pageSize`, render from `totalCount`, and reset to page 1 when a filter changes.

---

## Resolved Questions

All five open questions have been answered and folded into the body above (Decisions 7–11).

1. **Admin remove of an `Entered` exhibit** → Admin **can** remove an `Entered` exhibit, but **cannot** remove an exhibit from an `Accepted`/`Rejected` submission. (See `RemoveFileAsync`, Decision 7.)
2. **Submission-level packaging (CHUNK)** → Accept produces a **single zip per submission** (all retained exhibits + one combined metadata manifest). (See `AcceptSubmissionAsync`, Decision 8.)
3. **Migration backfill** → Dev-only; classify existing `IsDeleted` submissions as `Accepted` (no deletion) so they stay testable. (See Migration, Decision 11.)
4. **Editing on terminal submissions** → No reopen path; `Accepted`/`Rejected` are permanent. (Decision 10.)
5. **Server vs client paging** → Ship **server-side pagination** with the filters now. (Decision 9.)

## Remaining Open Questions

None blocking. Minor design choices left to implementation:

- Whether the per-submission `Accepted` zip should include Removed exhibits as metadata-only entries (assumed **yes** for a complete record) or omit them entirely.
  Answer: The `Removed` exhibits should not be included in the metadata.
- Exact `DefaultPageSize` / `MaxPageSize` values.
