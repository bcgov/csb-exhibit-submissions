# Exhibit Search — Admin Exhibit Lookup by File Number / Accused Name

**Status:** Draft
**Date:** 2026-07-07
**JIRA:** CES-38

---

## Overview

Admins (Judicial Justices) currently land on the **Submission Listing** ([SubmissionListing.vue](../web/src/components/admin/SubmissionListing.vue)) — a paginated, submission-grouped table where they double-click through to a per-submission review ([SubmissionReview.vue](../web/src/components/admin/SubmissionReview.vue)). That view is organized around *submissions*, but what a JJ on the bench actually needs is to look up a **file (ticket) number** or an **accused's last name** and immediately see **every exhibit** for it — ordered the way exhibits are called in court: **Marked** (A–Z) first, then **Entered** (1–50).

This spec introduces a new **Exhibit Search** page that becomes the admin landing view — a **one-stop shop** where the JJ enters a file number or last name (with an optional date range) and, in a single screen, can **see, view, download, and update** the exhibits for that file. The old Submission Listing and Review screens are **left in place** — routes and files untouched — but **removed from navigation** until a decision is made about whether to keep or retire them.

The results list **reuses the shared [ExhibitList.vue](../web/src/components/shared/ExhibitList.vue) control** in `alwaysEditable` (admin) mode — exactly as [SubmissionReview.vue](../web/src/components/admin/SubmissionReview.vue) already drives it. That control already provides the blended row layout (filename + date + file-number badge + status chip + View/Download on one line; Marked / Entered / Description controls below), inline **auto-save** on every classification change (select `change`) and description `blur` with ✓/✕ save indicators, and the per-exhibit change-history popup. In `alwaysEditable` mode there are no officer-style edit windows or locks. **No Save/Accept button is required — edits persist automatically.**

This reuses the existing classification data model from [exhibit-classification.md](completed/exhibit-classification.md) (Marked / Entered / Description / timestamps on the shared `SubmissionFile`) and the multi-ticket retrieval groundwork from [multi-ticket-exhibit-upload.md](completed/multi-ticket-exhibit-upload.md). The classification endpoints (`/api/files/{id}/mark|enter|description|history`) already authorize `User,Admin`, so **editing requires no backend changes.**

---

## Terminology

Following [admin-listing-update.md](completed/admin-listing-update.md): a **Submission** is one officer upload event (one or more tickets, one or more files); an **Exhibit** is a single stored file with its own classification lifecycle. This page is **exhibit-centric** — results are a flat list of exhibits, not submissions, rendered as one `ExhibitList.vue` per result. Each result row still carries its parent submission's context (file numbers, accused, court date) for display.

---

## User Stories

1. As a JJ, I can search by **file number** (partial, from 5 characters) and see every exhibit ever submitted for that file, across court sessions, so I have the complete exhibit record in one place.
2. As a JJ, I can search by the accused's **last name** so I can find exhibits when I don't have the file number to hand.
3. As a JJ, I can optionally narrow results to a **court-date range** so a busy file's history is scoped to the appearance I care about.
4. As a JJ, I see each exhibit's **classification** (Marked A–Z / Entered 1–50 / Unclassified) clearly alongside the file details, ordered the way exhibits are called, so the list reads like the exhibit sheet.
5. As a JJ, I can **view or download** an exhibit directly from the results.
6. As a JJ, I can **update** an exhibit's Marked / Entered / Description inline on the same screen, saved automatically, and review its **change history**, so I never have to leave the search to maintain the record.

---

## Decisions

| Question | Decision |
|---|---|
| File-number matching | **Partial, contains-match, 5-character minimum** before a search runs. |
| Last-name matching | Contains-match against the ticket's `AccusedName`. Input labeled **"Last name"**, placeholder `last name`. |
| File-number input | Placeholder `e.g. AH123456789-1`. |
| Helper text | **"Enter file number or accused name to get exhibit list"**. |
| Unclassified exhibits | **Shown**, sorted **last** (after Marked, after Entered). |
| Removed exhibits | Excluded from results (`IsDeleted`). |
| Date range | Optional; filters on **court/appearance date** (`SubmissionTicket.AppearanceDateTime`). |
| Sortable columns | **No** — fixed classification order, not user-sortable. |
| Editing | Marked / Entered / Description are **editable inline** via `ExhibitList.vue` `alwaysEditable` mode, **auto-saved** (no Save button). Change-history popup available per exhibit. |
| Remove exhibit | **Not** enabled on this page (`canRemove=false`) — destructive removal stays on the Submission Review flow. See Open Questions. |
| Navigation | New page **replaces** the old listing in nav and becomes the admin landing. Old route/files stay but are unlinked. |

### Sort order

1. **Marked** (`MarkedValue` set) → by `MarkedValue` A→Z
2. **Entered** (`EnteredValue` set, `MarkedValue` null) → by `EnteredValue` **numeric** 1→50
3. **Unclassified** (neither) → last

`AppearanceDateTime` is an ISO string (e.g. `2026-07-07T09:00:00`), so a date-range filter compares the `yyyy-MM-dd` prefix lexicographically.

---

## Backend (`/api`)

### 1. Models — `CES.Business/Models/`

- **`ExhibitSearchFilter.cs`** — `string? FileNumberText`, `string? AccusedName`, `DateTime? AppearanceDateFrom`, `DateTime? AppearanceDateTo`.
- **`ExhibitSearchResultModel.cs`** — one row per exhibit:
  - `SubmissionFile File` — reuse the existing `SubmissionFile` from `SubmissionReviewModel.cs` (carries Marked/Entered/Description, timestamps, derived status).
  - `int SubmissionId`, `DateTime? SubmissionDate`, `string? AppearanceDateTime`, `string Location`, `string Room`.
  - `List<string> FileNumbers` (distinct file numbers on the submission), `string? AccusedName`.

### 2. Constant — `CES.Business/Constants/`

Add `ExhibitSearchConstants.FileNumberMinLength = 5` (alongside the existing `PagingConstants` pattern). No inline literal (CLAUDE.md rule).

### 3. Service — `CES.Business/Services/SubmissionService.cs` (+ `ISubmissionService.cs`)

`Task<List<ExhibitSearchResultModel>> SearchExhibitsAsync(ExhibitSearchFilter filter)`:

- Query `_datastore.Submissions.Include(s => s.Tickets).Include(s => s.Files)`, `!s.IsDeleted`.
- Apply each **provided** term as a constraint (AND when both are given):
  - `FileNumberText` → `s.Tickets.Any(t => t.FileNumberText.ToLower().Contains(fnLower))`
  - `AccusedName` → `s.Tickets.Any(t => t.AccusedName != null && t.AccusedName.ToLower().Contains(nameLower))`
  - Date range → `s.Tickets.Any(t => t.AppearanceDateTime != null && <date-prefix within [from, to]>)`
- Flatten to **one row per non-deleted file**; attach the submission's distinct `FileNumbers`, first ticket's `AccusedName` / `AppearanceDateTime`, `LocationNameText` / `RoomText`, and `UploadDate`.
- Sort by the 3-tier key above (parse `EnteredValue` to `int` for numeric order).
- Reuse `f.DeriveStatus()` ([StoredFilesExtensions.cs](../api/CES.Business/Extensions/Entities/StoredFilesExtensions.cs)) and build each `SubmissionFile` the same way `ToReviewModel` does ([SubmissionExtensions.cs](../api/CES.Business/Extensions/Entities/SubmissionExtensions.cs)) — factor a small shared file-projection helper rather than duplicating that projection.

This is a **new** query. The existing `GetSubmissionsByFileNumberAsync` is exact-match, submission-grouped, and User+Admin; it is not reused here.

### 4. Controller — `CES.API/Controllers/SubmissionsController.cs`

`GET /api/submissions/exhibit-search` — `[Authorize(Roles = "Admin")]`, `[FromQuery] ExhibitSearchFilter`:

- Require at least one of `FileNumberText` / `AccusedName` non-empty → else `BadRequest`.
- If `FileNumberText` present and trimmed length `< FileNumberMinLength` → `BadRequest` with a clear message.
- Delegate to `SearchExhibitsAsync`; return `Ok(results)`.

---

## Frontend (`/web`)

### 5. Model — `web/src/models/ExhibitSearchResultModel.ts`

Mirror the backend: `{ file: SubmissionFile; submissionId; submissionDate?; appearanceDateTime?; location; room; fileNumbers: string[]; accusedName? }`. Add an `ExhibitSearchFilter` interface. Note this shape is a superset of `ExhibitList.vue`'s `PriorFileEntry` (`{ file, submissionDate?, fileNumbers[] }`), so each result maps directly onto an entry.

### 6. Service — `web/src/services/SubmissionService.ts`

Add `searchExhibits(filter)` → `GET /submissions/exhibit-search`, mirroring the param-building style of `retrieveSubmissionListing` (omit empty params). Export it.

### 7. Constant — `web/src/constants/submission.ts`

Add `FILE_NUMBER_MIN_LENGTH = 5` (mirror backend; no inline literal).

### 8. Component — `web/src/components/admin/ExhibitSearch.vue` (new)

A thin wrapper around the search form and the shared `ExhibitList.vue` control — most of the results/editing behavior comes for free from that control.

- **Search form:** File-number input (placeholder `e.g. AH123456789-1`); Last-name input (label "Last name", placeholder "last name"); optional Date-from / Date-to; helper text **"Enter file number or accused name to get exhibit list"**; Search + Clear buttons.
- **Validation:** block search unless a file number ≥ `FILE_NUMBER_MIN_LENGTH` **or** a last name is present; show an inline hint otherwise. Provide a "no results" empty state and a 400 / permission error state (pattern from `SubmissionListing.fetchListing`).
- **Results:** map the sorted `searchExhibits` response to `ExhibitList.vue`'s `entries` (`{ file, submissionDate, fileNumbers }`), preserving backend order (Marked → Entered → Unclassified). Render **one `ExhibitList`** for the whole result set, configured like [SubmissionReview.vue](../web/src/components/admin/SubmissionReview.vue):
  - `:always-editable="true"` (admin mode — inline Marked/Entered/Description editing, auto-saved, no locks)
  - `:can-download="true"`, `:can-remove="false"`, `:show-removed="false"`
  - `:mark-fn`, `:enter-fn`, `:description-fn` wired to `markExhibit` / `enterExhibit` / `updateExhibitDescription` from `SubmissionService`
  - handle `@file-updated` (patch the local row so the classification badge/state updates in place), `@preview-file`, `@download-file`
- **Preview + download:** reuse [FileViewer.vue](../web/src/components/shared/FileViewer.vue) in a modal and the blob-download helper — copy the `openPreview` / `closePreview` / `downloadFile` / `updateFileInSubmission` pattern verbatim from `SubmissionReview.vue` (URLs `/api/files/{id}/{view|download}`). The change-history popup is internal to `ExhibitList` — no extra wiring.

### 9. View, route, and navigation

- **`web/src/views/admin/ExhibitSearchView.vue`** (new) wraps the component (mirror [ListingView.vue](../web/src/views/admin/ListingView.vue)).
- [router/index.ts](../web/src/router/index.ts): add `{ path: '/admin/exhibit-search', name: 'AdminExhibitSearch', component: ExhibitSearchView, meta: { requiresAuth: true, roles: ['Admin'] } }`. Leave `/admin/list` and `/admin/view/:id` untouched.
- [LoginView.vue](../web/src/views/LoginView.vue): change the admin post-login redirect from `AdminSubmissionList` → `AdminExhibitSearch`.
- [App.vue](../web/src/App.vue): replace the "Admin Listing" `v-tab` (→ `/admin/list`) with "Exhibit Search" (→ `/admin/exhibit-search`); update the `selectedTab` default accordingly.

---

## Testing

Per CLAUDE.md, all new work ships with tests.

**Backend** — `CES.Business.Tests/Services/SubmissionServiceTests.cs` and `CES.API.Tests/Controllers/SubmissionsControllerTests.cs`:

| Test | Asserts |
|---|---|
| File-number contains-match | Returns all exhibits for the file across multiple submissions. |
| Last-name contains-match | Matches on `AccusedName`. |
| Date-range filter | Filters on appearance date. |
| Sort order | Marked (A–Z) → Entered (1–50 numeric) → Unclassified last. |
| Removed excluded | `IsDeleted` files are omitted. |
| Controller — missing terms | Both terms empty → 400. |
| Controller — short file number | File number < 5 chars → 400. |
| Controller — auth | Admin-only. |

**Frontend** — service test under `web/src/services/__tests__` + a component test (follow existing store/service/component layout):

- `searchExhibits` builds correct query params and omits empties.
- `ExhibitSearch.vue`: validation blocks short/empty search; maps results into `ExhibitList` entries in sorted order; passes `always-editable`; `@file-updated` patches the row; empty and error states. (Editing/auto-save internals are already covered by the existing `ExhibitList` tests — don't re-test them here.)

---

## Verification

- `dotnet test api/CES.API/CES.API.sln` and `cd web && npm run test` — both green; also `cd web && npm run type-check`.
- `cd docker && ./manage debug`; log in as Admin → land on **Exhibit Search**. Search a known file number (≥ 5 chars) and a last name; confirm the blended list, Marked→Entered→Unclassified order, View/Download, date-range narrowing, and that the old Submission Listing tab is gone from nav (while `/admin/list` still loads if hit directly).

---

## Out of Scope / Open Questions

- **Removing exhibits from this page** — `canRemove=false` for v1; destructive removal stays on the Submission Review flow. Open question: should the "one-stop shop" also allow remove? Easy to enable later (wire `remove-fn` + confirm modal, per `SubmissionReview.vue`).
- **Pagination** — result sets for a single file / last name are expected to be modest; v1 returns all matches ordered. Revisit with a result cap if last-name searches prove large.
- **Retiring the old Submission Listing / Review** — deferred; this spec only unlinks them from nav.
