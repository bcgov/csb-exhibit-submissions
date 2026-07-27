# Multi-Ticket Exhibit Upload

**Status:** Complete  
**Date:** 2026-05-28  
**Revised:** 2026-06-10 — added Cross-Date Prior Exhibit Retrieval (keyed on `FileNumberText`)  
**JIRA:** CES-26

---

## Overview

In traffic court, a single disputant may have multiple tickets heard at the same location, room, and date. An officer with bodycam footage or other evidence that applies to all of those tickets currently must submit it once per ticket — a redundant process. This feature allows an officer to select multiple tickets on the Court Search screen and submit one exhibit upload that is associated with all of them.

A ticket (`FileNumberText`) is heard across **multiple court appearances on different dates** — an exhibit uploaded for a ticket at one appearance may need to be revisited at a later appearance (e.g. to classify it under [exhibit-classification.md](exhibit-classification.md)). When an officer selects a ticket on Court Search, the Exhibit Upload screen must therefore **surface any exhibits previously uploaded for that same `FileNumberText`** — regardless of the appearance date, location, or room they were originally submitted under — so the officer has the full prior context and can make further modifications. See **Cross-Date Prior Exhibit Retrieval** below.

---

## User Stories

1. As an officer, I can select one or more tickets on the Court Search screen so that a single exhibit upload can cover all of them.
2. As an officer, I can see all selected tickets on the Exhibit Upload screen and remove individual tickets that do not apply to my evidence, as long as at least one ticket remains.
3. As an officer, I can navigate back from the Exhibit Upload screen to Court Search to start over with a new search and selection.
4. As an admin, I can see which tickets a submission covers from both the submission listing and the submission review screen.
5. As an officer, when I select a ticket whose file number already has exhibits uploaded at a previous appearance, I can see those prior exhibits and their details on the Exhibit Upload screen so that I have the full history of the ticket and can act on it (e.g. classify a previously uploaded exhibit) rather than re-uploading a duplicate.

---

## Scope

| Area | In Scope |
|---|---|
| Officer — Court Search screen | Yes |
| Officer — Exhibit Upload screen | Yes |
| Frontend state management | Yes |
| Backend API contract | Yes |
| Database schema & migration | Yes |
| Admin — Submission Listing screen | Yes |
| Admin — Submission Review screen | Yes |
| Cross-date prior exhibit retrieval (by `FileNumberText`) | Yes — read + display on Exhibit Upload screen |
| Email notifications | No — no change |
| File storage | Yes — storage path becomes submission-scoped (see Backend §6) |
| Authentication / permissions | No — no change |

---

## Decisions Made

| Decision | Choice | Rationale |
|---|---|---|
| Selection mechanism | Checkboxes per row, no header "select all" | Individual selection only; a bulk-select scenario is never needed for this workflow |
| Navigation trigger | Floating "Upload Exhibit" bar (visible when ≥1 checked) | Always on screen; hidden when nothing is selected so it doesn't imply action on an empty selection |
| Double-click shortcut | Retained | Selects that single ticket and proceeds immediately |
| Selection scope restriction | Same location + room + date | Naturally enforced by search; validated as a safety net |
| Back navigation behaviour | Full reset | Avoids stale state; officer re-searches with fresh intent |
| Remove last ticket | Prevent (hide/disable remove button) | Ensures the upload always has at least one ticket |
| Backend model | One Submission linked to many SubmissionTickets | Correct normalisation; avoids duplicate file storage |
| File storage path | Keyed by submission `Id` (`{locationId}/{shortDate}/{roomCode}/{submissionId}`) | A submission spans many file numbers; per-ticket file-number paths no longer make sense |
| Row click (single) | Toggles that row's checkbox only | No single-row "highlight" selection state; selection is driven entirely by checkboxes |
| Admin screens | In scope | Listing and review must surface multi-ticket context |
| Identifier casing | Canonical `appearanceId` (TS) / `AppearanceId` (C#) | Existing CES code is split between `appearanceID` and `appearanceId`; normalise before building on it |
| Cross-date ticket identity key | `FileNumberText` (the JUSTIN file/ticket number) — **not** `appearanceId` | `appearanceId` is per-appearance and changes every court session; `FileNumberText` is the stable ticket number that persists across all appearances/dates. Prior exhibits for "the same ticket" can only be found by file number. Confirmed via the JUSTIN mapping in `JCCourtListExtensions.cs` (`ClCriminalCourtList.FileNumberText` → `CourtList.FileNumberText`). |
| Prior-exhibit retrieval scope | All submissions sharing the selected `FileNumberText`, **across all dates, locations, and rooms** | A ticket may be adjourned to a different room/date; prior exhibits are legitimately attached under earlier appearances. The same-location/room/date restriction governs only which tickets may share **one new** upload — it does not constrain history lookup. |

---

## Prerequisite (Phase 0 — required first)

> **This must be completed and merged as its own change before the multi-ticket work begins.** It is a pure rename with no behavioural change, kept separate so the structural migration below starts from a consistent baseline and its diff stays reviewable.

### Identifier casing normalisation

The CES codebase is currently inconsistent about the appearance-id field's casing, and the mismatch is already live:

- [`ExhibitSubmissionModel.ts`](../web/src/models/ExhibitSubmissionModel.ts) exposes `appearanceId`, but [`CourtFileList.ts`](../web/src/models/CourtFileList.ts) exposes `appearanceID`.
- [`SubmissionService.ts`](../web/src/services/SubmissionService.ts) reads `model.appearanceId` yet posts the form key as `appearanceID`, which binds to `AppearanceID` on the C# side.

**Canonical form:** `appearanceId` (TypeScript / JSON / form keys) and `AppearanceId` (C# properties).

**Rename across CES-owned code only:**

| Layer | Files |
|---|---|
| Web models | `web/src/models/CourtFileList.ts` (`appearanceID` → `appearanceId`) |
| Web components | `web/src/components/officer/CourtListing.vue`, `web/src/components/officer/SubmissionForm.vue` |
| Web service | `web/src/services/SubmissionService.ts` (form key `appearanceID` → `appearanceId`) |
| Business models | `api/CES.Business/Models/Location/CourtList.cs` (`AppearanceID` → `AppearanceId`) |
| Mapping extensions | `api/CES.Business/Extensions/Entities/SubmissionExtensions.cs`, `JCCourtListExtensions.cs` (left-hand assignments only) |
| Tests | `SubmissionServiceTests.cs`, `SubmissionsControllerTests.cs`, `FilesControllerTests.cs`, `LocationsControllerTests.cs`, `courtFileSelectionStore.spec.ts` |

**Explicitly excluded:** everything under `api/jc-interface-client/**` (generated NSwag client, `FileServices.yaml`, `.nswag`). Those identifiers mirror the external JUSTIN/JC API contract and must not be hand-edited. The boundary where the external `CriminalAppearanceID` is mapped into our `AppearanceId` stays in `JCCourtListExtensions.cs`.

**Note on the `Submissions.AppearanceID` column:** the existing persisted column does **not** need a rename migration here — the multi-ticket work below removes it from `Submissions` entirely and re-introduces it as `SubmissionTickets.AppearanceId`. Renaming it in Phase 0 would only create a throwaway migration.

---

## Frontend Changes

### 1. Court Search Screen (`CourtListing.vue`)

#### Checkbox column
- Add a checkbox as the **first column** of the results table (before the existing "Order" column).
- There is **no header checkbox** — each row must be selected individually. A "select all" scenario is not a valid workflow for this feature.
- Rows that share the same `locationId`, `roomCode`, and calendar date (date portion of `appearanceDateTime`) as the first checked ticket are selectable. All other rows have their checkboxes disabled with a tooltip: _"This ticket is from a different location, room, or date."_
- Since the search form already scopes results to a single location, room, and date, this restriction will normally have no effect; it acts as a safety net only.

#### Upload Exhibit button
- A **"Upload Exhibit (N selected)"** button is rendered in a **floating bottom bar** that is fixed to the bottom of the viewport and scrolls with the page, keeping it visible at all times.
- The bar and button are **only shown when at least one ticket is checked**. When zero tickets are checked the bar is hidden entirely.
- Clicking the button stores all selected `CourtFileList` objects in the Pinia store and navigates to the Exhibit Upload screen.

#### Row click behaviour
- A single click anywhere on a selectable row toggles that row's checkbox. There is **no** separate single-row "highlight" selection state — the existing `singleClickSelect` / `selectedFile` highlight is removed. All selection is expressed through checkboxes.

#### Double-click shortcut (retained)
- Double-clicking a row still works as before: that single ticket is placed in the store (replacing any checkbox selection) and the app navigates to the Exhibit Upload screen immediately, regardless of checkbox state.

#### Search reset
- When the officer clicks "Search" again, all checkbox selections are cleared.

---

### 2. Exhibit Upload Screen (`SubmissionForm.vue`)

#### Selected tickets list
Replace the current single-ticket read-only fields (File #, Disputant Name) with a **ticket list panel** showing one row per selected ticket containing:
- File number (`fileNumberText`)
- Accused name (`accusedName`)
- Appearance time (time portion of `appearanceDateTime`)
- A **Remove** button on each row

**Remove behaviour:**
- When **more than one** ticket is in the list, the Remove button is visible and active.
- When **only one** ticket remains, the Remove button is hidden or disabled — the officer must use the Back button to start over instead.

#### Prior exhibits panel (new)
Below each selected ticket (or as a collapsible panel per ticket), display any exhibits **already uploaded for that ticket's `FileNumberText`** at a previous appearance. The list is fetched on screen load via the retrieval endpoint defined in **Cross-Date Prior Exhibit Retrieval** and is **read-only within this feature** — it shows the officer the existing history so they don't re-upload duplicates and so they can act on prior exhibits where another feature allows it (e.g. classification). Per prior exhibit, show:
- Original file name
- The submission/appearance date it was uploaded under
- File size / content type
- A placeholder slot for classification state (Marked / Entered), populated once [exhibit-classification.md](exhibit-classification.md) ships — the two features share this retrieval (see that spec's `exhibits-by-ticket` endpoint).

When a selected ticket has no prior exhibits, render nothing (or a quiet "No previous exhibits for this ticket") rather than an empty panel. This panel is **independent of the new files being uploaded** in the current submission; it never participates in the submit payload.

#### Shared read-only fields (unchanged from current design)
These fields remain above the ticket list and apply to all selected tickets (they are the same for every ticket in the group):
- Court Date
- Location
- Room

#### Back button
- A **"Back"** button navigates to `/officer/court-list` and calls `selectionStore.clear()`.
- This performs a full reset: the Court Search form returns to its initial blank state.

#### Submit behaviour
- Officer Number input and file dropzone behave identically to the current design.
- On submit, the payload is updated to carry multiple ticket objects (see API Contract section).

---

### 3. Pinia Store (`useCourtFileSelectionStore.ts`)

The store changes from holding a single file to an array:

```typescript
// Before
state: () => ({
  selectedFile: null as CourtFileList | null
})

// After
state: () => ({
  selectedFiles: [] as CourtFileList[]
})
```

New actions:

| Action | Description |
|---|---|
| `setSelectedFiles(files: CourtFileList[])` | Replace the entire selection (used by Upload Exhibit button and double-click) |
| `removeFile(appearanceId: string)` | Remove one ticket by its `appearanceId` |
| `clear()` | Reset to empty array |

---

### 4. Router / Navigation

No new routes are required. Navigation continues to use the existing `OfficerSubmissions` route (`/officer/submission/`). The guard on that route should be updated to redirect to `OfficerCourtList` if `selectedFiles` is empty (currently guards against `selectedFile === null`).

---

## Backend Changes

### 1. Database Schema

#### New table: `SubmissionTickets`

Stores one row per ticket per submission. Replaces the ticket-specific columns that currently live on the `Submissions` table.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `int` | No | PK, identity |
| `SubmissionId` | `int` | No | FK → `Submissions.Id` |
| `AppearanceId` | `varchar` | No | Previously `AppearanceID` on Submissions |
| `AppearanceDateTime` | `varchar` | Yes | Per-ticket appearance time |
| `AppearanceSequenceNumber` | `varchar` | Yes | Order within the court list |
| `AppearanceReasonCode` | `varchar` | Yes | e.g. `ADJ`, `TRI` |
| `CourtListType` | `varchar` | Yes | |
| `FileNumberText` | `varchar` | No | Ticket / file number |
| `AccusedName` | `varchar` | Yes | |
| `AccusedDOB` | `varchar` | Yes | |

#### Modified table: `Submissions`

Remove the columns that are now in `SubmissionTickets`. Retain only fields that are shared across all tickets in a submission.

**Columns to remove:**
- `AppearanceID`
- `AppearanceDateTime`
- `CourtListType`
- `FileNumberText`
- `AccusedName`
- `AccusedDOB`

**Columns to keep (unchanged):**
- `Id`, audit fields, `IsDeleted`
- `UploadDate`
- `LocationId`, `LocationNameText`
- `RoomCode`, `RoomText`
- `OfficerNumber`

> **Note on `AppearanceSequenceNumber` and `AppearanceReasonCode`:** These are present on the inbound request model (`EvidenceSubmissionModel` → `CourtList`) but are **not** currently persisted on the `Submissions` table — the entity has no such columns. They are new persisted columns on `SubmissionTickets`. Because the source data does not exist on existing `Submissions` rows, the data-copy step (Migration §2) leaves them **null** for migrated/legacy tickets; they are only populated for new submissions going forward.

#### Entity changes (C#)

```csharp
// New entity
public class SubmissionTicket
{
    public int Id { get; set; }
    public int SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;
    public string AppearanceId { get; set; } = null!;
    public string? AppearanceDateTime { get; set; }
    public string? AppearanceSequenceNumber { get; set; }
    public string? AppearanceReasonCode { get; set; }
    public string? CourtListType { get; set; }
    public string FileNumberText { get; set; } = null!;
    public string? AccusedName { get; set; }
    public string? AccusedDOB { get; set; }
}

// Updated Submission — add navigation property, remove ticket-specific fields
public class Submission : BaseEntity
{
    // ... existing shared fields ...
    public ICollection<SubmissionTicket> Tickets { get; set; } = new List<SubmissionTicket>();
}
```

#### EF Core registration

The new entity must be registered alongside the existing `Submissions` set, or the migration will not scaffold:

- Add `DbSet<SubmissionTicket> SubmissionTickets { get; }` to `ICESDataStore` (`CES.Entities/Interfaces/ICESDataStore.cs`).
- Add `DbSet<SubmissionTicket> SubmissionTickets { get; set; }` to `CESDataStore` (`CES.EF/CESDataStore.cs`).
- Configure the relationship in `OnModelCreating`: one `Submission` → many `SubmissionTicket`, with `SubmissionId` as the FK and cascade delete (a ticket has no meaning without its submission).

---

### 2. Migration Strategy

The migration must be **data-preserving**:

1. Create the `SubmissionTickets` table.
2. For every existing row in `Submissions`, insert one row into `SubmissionTickets` copying the ticket-specific column values.
3. Drop the now-migrated columns from `Submissions`.

This is a single EF Core migration with a custom `Up()` that runs the data copy via raw SQL before the column drop. The `Down()` method reverses the process.

---

### 3. API Contract

#### `POST /api/submissions/submit`

The request remains `multipart/form-data`. The flat ticket fields are replaced with an **indexed array** of ticket objects using standard ASP.NET model binding notation.

**Before:**
```
appearanceId=ABC123
fileNumberText=123456
accusedName=John Doe
locationId=...
officerNumber=4567
files[]=<binary>
```

**After:**
```
tickets[0].appearanceId=ABC123
tickets[0].appearanceDateTime=2026-05-28T09:00:00
tickets[0].appearanceSequenceNumber=1
tickets[0].appearanceReasonCode=TRI
tickets[0].courtListType=...
tickets[0].fileNumberText=123456
tickets[0].accusedName=John Doe
tickets[0].accusedDOB=1980-01-01

tickets[1].appearanceId=DEF456
tickets[1].fileNumberText=123457
tickets[1].accusedName=John Doe
... (second ticket fields)

locationId=...           ← shared
locationNameText=...     ← shared
roomCode=...             ← shared
roomText=...             ← shared
shortDate=2026-05-28     ← shared (required; drives the storage path date segment)
officerNumber=4567       ← shared
files[]=<binary>
```

> Field casing follows the canonical `appearanceId` established in the Phase 0 prerequisite — do not reintroduce `appearanceID`.

**Validation:**
- `tickets` must contain at least one element.
- Each ticket must have a non-empty `appearanceId` and `fileNumberText`.
- `locationId`, `roomCode`, and `shortDate` are now **shared** single values, so there is nothing to cross-check between tickets for those.
- As a safety net, all tickets must share the same date portion of `appearanceDateTime` (still per-ticket); reject the request if any differ.

#### `GET /api/submissions/retrieve` and `GET /api/submissions/listing`

Both endpoints currently project the `Submission` entity through `ToReviewModel()` into `SubmissionReviewModel` (the listing reuses the same model — there is no separate listing model today). The response model must gain a `Tickets` collection so admin screens can read per-ticket data; the per-ticket `FileNumber`/`AccusedName` scalar fields are replaced by it. See **§7 Response Models** for the concrete changes.

#### `GET /api/submissions/by-file-number?fileNumberText={fileNumberText}` (new)

Returns every prior submission (and its non-deleted files) whose `SubmissionTickets.FileNumberText` matches the supplied file number, **across all dates, locations, and rooms**. Used by the Exhibit Upload screen to populate the prior-exhibits panel for each selected ticket.

- **Auth:** `User` role (officer). Officers must be able to call this during upload; admins may reuse it.
- **Query key:** `fileNumberText` only — never `appearanceId` (see Decisions Made: cross-date ticket identity key).
- **Result:** ordered newest-appearance-first; returns an **empty array, not 404**, when the file number has no history.
- **Excludes the in-progress submission** (there is none yet at this point) and excludes soft-deleted files/submissions (`IsDeleted == false`).
- **Overlap with classification:** the [exhibit-classification.md](exhibit-classification.md) spec defines `GET /api/submissions/exhibits-by-ticket?ticketNumber=…`, which returns a flat per-file list keyed on the same `FileNumberText`. These should resolve to **one shared endpoint/service method** keyed on file number; this spec owns the submission-grouped shape for the upload panel, the classification spec owns the flat per-file classification shape. Implementer must reconcile rather than build two queries. Whichever ships first defines `GetSubmissionsByFileNumberAsync` / `GetExhibitsByTicketNumberAsync` on `SubmissionService`; the second adapts to it.

---

### 4. TypeScript Model Changes

#### `ExhibitSubmissionModel.ts`

```typescript
// New sub-model
interface SubmissionTicketModel {
  appearanceId: string
  appearanceDateTime: string
  appearanceSequenceNumber: string
  appearanceReasonCode: string
  courtListType: string
  fileNumberText: string
  accusedName: string
  accusedDOB: string
}

// Updated submission model
interface ExhibitSubmissionModel {
  tickets: SubmissionTicketModel[]   // replaces all per-ticket fields
  shortDate: string                  // shared; required by the API
  locationId: string
  locationNameText: string
  roomCode: string
  roomText: string
  officerNumber: string
}
```

---

### 5. Request Model & Mapping (C#)

The wire-format change above requires restructuring the inbound DTOs and the entity mapping — these are **not** auto-derived from the entity change:

- Today `SubmissionModel : EvidenceSubmissionModel : CourtList` carries the per-ticket fields flat on the base. Introduce a `SubmissionTicketModel` (mirroring the entity's ticket fields) and replace the flat per-ticket properties with `List<SubmissionTicketModel> Tickets`. The shared fields (`LocationId`, `LocationNameText`, `RoomCode`, `RoomText`, `OfficerNumber`, `ShortDate`) remain on the request model.
- Rewrite [`SubmissionExtensions.ToEntity()`](../api/CES.Business/Extensions/Entities/SubmissionExtensions.cs): map the shared fields onto `Submission`, then project `model.Tickets` into `Submission.Tickets` (one `SubmissionTicket` per element). Validate `Tickets` is non-empty before mapping.
- The controller's `model.Files` handling is unchanged, but the per-file path construction moves to the submission-scoped scheme in §6.

---

### 6. File Storage Path

Today the storage path embeds a single ticket's file number (`Path.Combine(file.Location, file.Date, file.Room, file.FileNumber)` in [`SubmissionService`](../api/CES.Business/Services/SubmissionService.cs), fed by `FileNumber = model.FileNumberText` in the controller). A submission now spans many file numbers, so this no longer has a single value.

**New scheme:** files are stored under the **submission `Id`**:

```
{locationId}/{shortDate}/{roomCode}/{submissionId}/<filename>
```

- The `Submission` entity is added (and saved, or its key generated) before files are persisted so `Id` is available for the path.
- `FileUpload.FileNumber` is replaced by `SubmissionId` (or the path is composed directly in the service). The shared `locationId` / `shortDate` / `roomCode` continue to form the upper segments.
- This is the one place the "File storage: no change" assumption breaks; the Scope table is updated accordingly.

---

### 7. Response Models (C# + TS)

So the admin screens can render multi-ticket context, the review/listing response gains a tickets collection:

```csharp
// CES.Business/Models/SubmissionReviewModel.cs
public class SubmissionReviewModel
{
    public int Id { get; set; }
    public DateTime? SubmissionDate { get; set; }
    public string CourtDateTime { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public List<SubmissionTicketModel> Tickets { get; set; } = new();  // replaces FileNumber + AccusedName
    public List<SubmissionFile> Files { get; set; } = new();
}
```

- Update `ToReviewModel()` to project `entity.Tickets` (it currently sets the now-removed `FileNumber` / `AccusedName` scalars).
- `RetrieveSubmission` / `RetrieveSubmissionListing` must `.Include(s => s.Tickets)` so the collection is loaded.
- The matching frontend model [`SubmissionReviewModel.ts`](../web/src/models/SubmissionReviewModel.ts) replaces its `fileNumber` / `accusedName` scalars with a `tickets: SubmissionTicketModel[]` array.

---

## Cross-Date Prior Exhibit Retrieval

This is the core of User Story 5. The same physical ticket (`FileNumberText`) recurs at many court appearances on different dates. Exhibits uploaded under an earlier appearance must remain discoverable and modifiable at a later one.

### Why `FileNumberText` is the key (confirmation)

The court listing carries two distinct identifiers:

| Field | Stability | Use |
|---|---|---|
| `appearanceId` (`CriminalAppearanceID`) | **Per-appearance** — a new value for every court session/date | Identifies one ticket *at one session*; used to scope a single new upload |
| `FileNumberText` | **Stable across all appearances** of the ticket | The ticket's identity over time; the only key that links exhibits across dates |

Source of truth: [`JCCourtListExtensions.cs`](../api/CES.Business/Extensions/Entities/JCCourtListExtensions.cs) maps `ClCriminalCourtList.FileNumberText → CourtList.FileNumberText` and `ClCriminalCourtList.CriminalAppearanceID → CourtList.AppearanceID`. Because the multi-ticket migration persists `FileNumberText` on **`SubmissionTickets`** (one row per ticket per submission), the cross-date query is a straightforward join. **All retrieval in this section is keyed on `FileNumberText`; `appearanceId` is never used for history lookup.**

> **Uniqueness assumption:** `FileNumberText` is assumed globally unique to a ticket within JUSTIN. If a file number can be reused across registries/jurisdictions, retrieval would over-return. Flagged in Open Questions — confirm before relying on file number alone.

### Data access

```csharp
// ISubmissionService — shared with exhibit-classification.md (reconcile, do not duplicate)
Task<List<PriorSubmissionModel>> GetSubmissionsByFileNumberAsync(string fileNumberText);
```

Query shape:

```csharp
_datastore.Submissions
    .Where(s => !s.IsDeleted && s.Tickets.Any(t => t.FileNumberText == fileNumberText))
    .Include(s => s.Tickets)
    .Include(s => s.Files.Where(f => !f.IsDeleted))
    .OrderByDescending(s => s.UploadDate)
```

- The query crosses location/room/date deliberately — **no** location/room/date filter is applied (a ticket may be adjourned elsewhere).
- Soft-deleted submissions and files are excluded.
- Returns an empty list (controller returns `200` + `[]`) when there is no history.

### Response model

```csharp
public class PriorSubmissionModel
{
    public int SubmissionId { get; set; }
    public DateTime? SubmissionDate { get; set; }   // UploadDate
    public string? AppearanceDateTime { get; set; } // from the matching ticket row
    public string Location { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public List<SubmissionFile> Files { get; set; } = new();
    // Classification fields on each file (MarkedValue/EnteredValue/…) arrive with exhibit-classification.md
}
```

TypeScript mirror lives in a new `PriorSubmissionModel.ts` consumed by the Exhibit Upload screen.

### Frontend wiring

- On Exhibit Upload mount, for each distinct `FileNumberText` in `selectedFiles`, call `GET /api/submissions/by-file-number`. De-duplicate by file number so a file number appearing on more than one selected ticket is fetched once.
- Render results in the per-ticket **Prior exhibits panel** (Frontend §2). Failure to load history is **non-blocking**: show a quiet inline error and still allow the new upload to proceed.

### Interaction with other rules ("all other rules apply")

- **Storage path:** prior files retain whatever path they were stored under (legacy file-number scheme **or** the new submission-id scheme from §6). Retrieval is DB-driven, so the scheme change does not affect lookup; download URLs are resolved from each file's stored path as today.
- **Removal/classification of prior exhibits:** this spec only *surfaces* prior exhibits. Any modification (remove, mark, enter) is governed by the owning feature's rules — notably [exhibit-classification.md](exhibit-classification.md), under which a classified exhibit cannot be removed and a Marked exhibit may be Entered at this later session.
- **New upload independence:** the prior-exhibits panel is never part of the submit payload; submitting creates a brand-new `Submission` + `SubmissionTickets` as described in §3–§6.

---

## Admin Changes

### 1. Submission Listing (`SubmissionListing.vue`)

The **File #** and **Accused name** columns must handle multiple tickets. Proposed display:

- **Single ticket:** show values as today.
- **Multiple tickets:** show the first ticket's values followed by a badge, e.g. `123456 (+2 more)` / `John Doe (+2 more)`.
- Tooltip or expand affordance on the badge is a nice-to-have; leave for a follow-up.

No column additions or removals are needed at this stage.

### 2. Submission Review (`SubmissionReview.vue`)

Replace the single **Ticket #** and **Disputant** detail fields with a **Tickets** section that lists every associated ticket as a sub-row:

| File # | Accused Name | Appearance Time | Appearance Reason |
|---|---|---|---|
| 123456 | John Doe | 09:00 | Trial |
| 123457 | John Doe | 09:00 | Trial |

The section header should read **"Tickets (N)"** where N is the count.

All other fields (Court Date, Court Time, Location, Room, Submission Date, files, Accept/Reject actions) remain unchanged.

---

## Testing

Per the project testing rule, this feature is not complete until tests cover the new behaviour and existing tests touched by these changes are updated (not skipped). Specific cases are left to implementation; the categories below are the minimum coverage expected. Frameworks and structure follow [spec/testing-implementation.md](testing-implementation.md).

**Backend (xUnit)**
- Service: `SubmitEvidence` persists one `Submission` with many `SubmissionTickets`; storage path uses the submission-id scheme (§6); empty `tickets` is rejected.
- Mapping: `ToEntity()` projects shared fields + tickets; `ToReviewModel()` projects the `Tickets` collection.
- Controller / integration: `submit` binds the indexed `tickets[n].*` form fields and enforces validation (≥1 ticket, required ticket fields, same-date safety net); `retrieve` / `listing` return the `Tickets` array.
- Migration: data-copy step produces exactly one ticket row per existing submission with values preserved.
- Prior-exhibit retrieval: `GetSubmissionsByFileNumberAsync` returns submissions for a `FileNumberText` across **different dates/locations/rooms**; excludes soft-deleted submissions and files; returns an empty list for an unknown file number; is keyed on `FileNumberText` and **not** affected by `appearanceId`. Controller `GET /api/submissions/by-file-number` returns `200` + `[]` for no history and requires `User` auth (`401` unauthenticated).

**Frontend (Vitest)**
- Store: `setSelectedFiles`, `removeFile(appearanceId)`, and `clear` behave correctly, including the "cannot remove the last ticket" guard.
- Court Search: per-row checkbox selection (no header select-all), floating bar visibility (hidden at zero, shown ≥1), double-click still navigates with a single ticket.
- Exhibit Upload: ticket list renders one row per selection; Remove hidden/disabled at one ticket; submit payload carries the tickets array.
- Exhibit Upload prior exhibits: panel fetches history per distinct `FileNumberText` (de-duplicated) on mount; renders prior files when present; renders the quiet empty state when none; a failed history fetch is non-blocking (upload still works); the prior-exhibits panel is excluded from the submit payload.
- Admin: listing renders the `+N more` affordance; review renders the `Tickets (N)` section.

**Existing tests to update** (casing + model changes): `SubmissionServiceTests`, `SubmissionsControllerTests`, `FilesControllerTests`, `LocationsControllerTests`, `courtFileSelectionStore.spec.ts`, `SubmissionService.spec.ts`.

---

## Open Questions / Follow-up Items

1. **Admin listing expand:** Should the "+N more" badge be clickable to expand inline, or is a tooltip sufficient? 
Tooltip is sufficient.
2. **Partial accept across tickets:** Currently Accept/Reject operates on files, not on per-ticket associations. If a submission covers two tickets but only one charge is proven, can an admin accept files for one ticket and reject for another? 
No, this is acceptance of files into the system, the accept/reject is not part of the judgement process.  A file is likely selected for "Accept" before a determination is reached.
3. **`FileNumberText` uniqueness:** Cross-date retrieval assumes a file number uniquely identifies one ticket within JUSTIN. If file numbers can repeat across registries/jurisdictions, the prior-exhibit lookup would over-return unrelated exhibits and would need a compound key (e.g. file number + location, or a JUSTIN physical-file id). Confirm uniqueness with the business owner before relying on `FileNumberText` alone.
4. **Endpoint consolidation with classification:** This spec's `GET /api/submissions/by-file-number` (submission-grouped) and [exhibit-classification.md](exhibit-classification.md)'s `GET /api/submissions/exhibits-by-ticket` (flat per-file) both key on `FileNumberText`. Decide whether they ship as one endpoint with two response shapes or one shape that serves both screens, so the query and `.Include` logic are written once.
5. **Visibility of prior exhibits across officers:** Should an officer see prior exhibits uploaded for a ticket by a *different* officer, or only their own? (Mirrors the ownership question in the classification spec.) Affects whether `GetSubmissionsByFileNumberAsync` filters by `OfficerNumber`.