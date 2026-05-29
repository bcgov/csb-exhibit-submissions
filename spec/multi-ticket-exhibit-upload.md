# Multi-Ticket Exhibit Upload

**Status:** Draft  
**Date:** 2026-05-28  

---

## Overview

In traffic court, a single disputant may have multiple tickets heard at the same location, room, and date. An officer with bodycam footage or other evidence that applies to all of those tickets currently must submit it once per ticket — a redundant process. This feature allows an officer to select multiple tickets on the Court Search screen and submit one exhibit upload that is associated with all of them.

---

## User Stories

1. As an officer, I can select one or more tickets on the Court Search screen so that a single exhibit upload can cover all of them.
2. As an officer, I can see all selected tickets on the Exhibit Upload screen and remove individual tickets that do not apply to my evidence, as long as at least one ticket remains.
3. As an officer, I can navigate back from the Exhibit Upload screen to Court Search to start over with a new search and selection.
4. As an admin, I can see which tickets a submission covers from both the submission listing and the submission review screen.

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
| Email notifications | No — no change |
| File storage | No — no change |
| Authentication / permissions | No — no change |

---

## Decisions Made

| Decision | Choice | Rationale |
|---|---|---|
| Selection mechanism | Checkboxes per row | Explicit and accessible; standard table pattern |
| Navigation trigger | New "Upload Exhibit" button | Explicit action; supports multi-select |
| Double-click shortcut | Retained | Selects that single ticket and proceeds immediately |
| Selection scope restriction | Same location + room + date | Naturally enforced by search; validated as a safety net |
| Back navigation behaviour | Full reset | Avoids stale state; officer re-searches with fresh intent |
| Remove last ticket | Prevent (hide/disable remove button) | Ensures the upload always has at least one ticket |
| Backend model | One Submission linked to many SubmissionTickets | Correct normalisation; avoids duplicate file storage |
| Admin screens | In scope | Listing and review must surface multi-ticket context |

---

## Frontend Changes

### 1. Court Search Screen (`CourtListing.vue`)

#### Checkbox column
- Add a checkbox as the **first column** of the results table (before the existing "Order" column).
- A **header checkbox** selects / deselects all visible rows at once.
- Rows that share the same `locationId`, `roomCode`, and calendar date (date portion of `appearanceDateTime`) as the first checked ticket are selectable. All other rows have their checkboxes disabled with a tooltip: _"This ticket is from a different location, room, or date."_
- Since the search form already scopes results to a single location, room, and date, this restriction will normally have no effect; it acts as a safety net only.

#### Upload Exhibit button
- A **"Upload Exhibit"** button is shown below (or above) the results table.
- The button is **disabled** when zero tickets are checked, with label text "Upload Exhibit (0 selected)".
- When one or more tickets are checked, the label updates to "Upload Exhibit (N selected)" and the button becomes enabled.
- Clicking the button stores all selected `CourtFileList` objects in the Pinia store and navigates to the Exhibit Upload screen.

#### Double-click shortcut (retained)
- Double-clicking a row still works as before: that single ticket is placed in the store and the app navigates to the Exhibit Upload screen immediately, regardless of checkbox state.

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
| `removeFile(appearanceID: string)` | Remove one ticket by its `appearanceID` |
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

> **Note on `AppearanceSequenceNumber` and `AppearanceReasonCode`:** These were previously on `Submissions` (via `ExhibitSubmissionModel`) but are per-ticket values; they move to `SubmissionTickets`.

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
officerNumber=4567       ← shared
files[]=<binary>
```

**Validation:**
- `tickets` must contain at least one element.
- Each ticket must have a non-empty `appearanceId` and `fileNumberText`.
- All tickets must share the same `locationId`, `roomCode`, and date portion of `appearanceDateTime`.

#### `GET /api/submissions/retrieve` and `GET /api/submissions/listing`

Response models must include the `Tickets` collection. Downstream consumers (admin screens) read ticket data from this array.

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
  locationId: string
  locationNameText: string
  roomCode: string
  roomText: string
  officerNumber: string
}
```

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

## Open Questions / Follow-up Items

1. **Admin listing expand:** Should the "+N more" badge be clickable to expand inline, or is a tooltip sufficient? Deferred.
2. **Partial accept across tickets:** Currently Accept/Reject operates on files, not on per-ticket associations. If a submission covers two tickets but only one charge is proven, can an admin accept files for one ticket and reject for another? This is a separate feature; out of scope here.
3. **Court Search pagination:** If search results are paginated in the future, selection across pages would need special handling. Currently results are not paginated; note for future.
4. **Accessibility:** Ensure the checkbox column meets WCAG 2.1 AA (keyboard navigation, screen reader labels). Recommend an accessibility pass before shipping.
