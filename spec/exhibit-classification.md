# Exhibit Classification — Marked and Entered

**Status:** Draft  
**Date:** 2026-06-04  
**JIRA:** CES-28

---

## Overview

Officers uploading exhibits on the Exhibit Upload screen must classify each uploaded file as "Marked" (a letter A–Z, denoting the exhibit's validity as acknowledged by the JJ), "Entered" (a number 1–50, denoting admission into evidence), or both — in that order — under direction of the JJ. Classification is recorded with timestamps so that the full lifecycle of each exhibit is auditable.

---

## Exhibit Classification State Machine

Each uploaded file transitions through the following states:

| State            | Marked Value | Entered Value | Description                                               |
| ---------------- | ------------ | ------------- | --------------------------------------------------------- |
| Unclassified     | blank        | blank         | Just uploaded; no classification yet                      |
| Marked           | A–Z          | blank         | Officer has Marked the exhibit; awaiting Entered decision |
| Entered (direct) | blank        | 1–50          | Entered directly without a Marked designation             |
| Marked & Entered | A–Z          | 1–50          | Fully classified; read-only                               |

### Allowed transitions

| From         | To               | Trigger                                                                 |
| ------------ | ---------------- | ----------------------------------------------------------------------- |
| Unclassified | Marked           | Officer selects a letter from the Marked dropdown                       |
| Unclassified | Entered (direct) | Officer selects a number from the Entered dropdown (no letter selected) |
| Marked       | Marked & Entered | Officer selects a number from the Entered dropdown                      |

### Disallowed transitions

- **Entered (direct) → Marked:** An exhibit that has been Entered cannot subsequently be Marked.
- **Any classified state → Removed:** Once exhibit has been originally submitted, the exhibit cannot be removed.
- **Marked & Entered → any change:** Once fully classified, all controls are read-only.

---

## User Stories

1. As an officer, I can mark an uploaded exhibit with a letter (A–Z) at the direction of the JJ so that the exhibit's validity is documented, and I remain on the upload screen to continue processing.
2. As an officer, I can enter an uploaded exhibit with a number (1–50) — either after Marking it or directly without Marking — so that admission into evidence is recorded.
3. As an officer, I can mark an exhibit at one court session and enter it at a future court session so that the full classification lifecycle is preserved across dates.
4. As an officer, I can see the timestamps of when each exhibit was Marked and/or Entered directly on the Exhibit Upload screen so that I have an inline audit trail.
5. As an officer, I can see Entered exhibits in read-only mode so that I have a record without risking accidental modification.
6. As a JJ/Admin, I can retrieve exhibits by ticket number through the admin view and review their classification state and timestamps at any time so that I have a complete record of submission history, including any detail modifications.

---

## Scope

| Area                                                      | In Scope                                              |
| --------------------------------------------------------- | ----------------------------------------------------- |
| Officer — Exhibit Upload screen                           | Yes — per-file classification controls and timestamps |
| Exhibit state machine (Marked / Entered / Removed)        | Yes                                                   |
| MarkedAt / EnteredAt timestamps — display on screen       | Yes                                                   |
| MarkedAt / EnteredAt timestamps — CHUNK drive `.txt` file | Yes                                                   |
| Ticket number retrieval of exhibit history                | Yes                                                   |
| Backend API — mark and enter endpoints                    | Yes                                                   |
| Database schema — classification columns on `StoredFiles` | Yes                                                   |
| Admin — Submission Review screen                          | Yes — read-only classification display                |
| Email notifications                                       | No — no change                                        |
| File storage (binary)                                     | No — no change                                        |
| Authentication / permissions                              | No — no change                                        |

---

## Decisions Made

| Decision                        | Choice                                                                            | Rationale                                                                                                         |
| ------------------------------- | --------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| Classification granularity      | Per uploaded file within a submission                                             | Each exhibit is independently classified; a submission may have multiple files at different classification states |
| Marked value set                | Dropdown A–Z + blank default                                                      | Blank means the exhibit was not Marked (Entered directly); officer selects at direction of JJ                     |
| Entered value set               | Dropdown 1–50 + blank default                                                     | Blank means not yet Entered; officer selects at direction of JJ                                                   |
| Action persistence trigger      | Immediate API call on dropdown selection                                          | Officer stays on screen; no explicit "Save" button; classification is saved the moment the dropdown changes       |
| Marked lock                     | Marked dropdown disabled once a letter is selected                                | Prevents re-marking; enforces the immutability of the Marked designation                                          |
| Entered lock                    | Both dropdowns and Remove disabled once Entered is set                            | Fully-classified exhibits are read-only; officer can see but not modify                                           |
| Marked-after-Entered prevention | Marked dropdown is disabled when `enteredValue` is set                            | Enforces the unidirectional Marked → Entered flow                                                                 |
| Cross-session Entering          | Enter action available on any file in Marked state, regardless of submission date | Officers need to Enter exhibits that were Marked at a previous court session                                      |
| Timestamp format                | UTC stored; local time displayed as `YYYY-MM-DD HH:mm`                            | Consistent storage; human-readable display                                                                        |
| CHUNK drive `.txt` update       | MarkedAt and EnteredAt appended to each file's block                              | Keeps the written record self-describing; blank fields written as `—`                                             |

---

## Frontend Changes

### 1. Exhibit Upload Screen (`SubmissionForm.vue`)

#### Per-file classification controls

Each uploaded file in the file list gains a classification row directly below the filename. Controls per file:

| Control                    | Type           | Default | Active when                                          |
| -------------------------- | -------------- | ------- | ---------------------------------------------------- |
| **Marked** dropdown        | Select A–Z     | blank   | `markedValue` is null AND `enteredValue` is null     |
| **Entered** dropdown       | Select 1–50    | blank   | `enteredValue` is null (regardless of `markedValue`) |
| **Remove** button          | Button         | —       | `markedValue` is null AND `enteredValue` is null     |
| **Marked:** label + value  | Read-only text | Hidden  | `markedValue` is set                                 |
| **Marked at:** timestamp   | Read-only text | Hidden  | `markedAt` is set                                    |
| **Entered:** label + value | Read-only text | Hidden  | `enteredValue` is set                                |
| **Entered at:** timestamp  | Read-only text | Hidden  | `enteredAt` is set                                   |

#### Interaction rules

- **Selecting a Marked letter:** Immediately calls `POST /api/submissions/mark`. On success: disables the Marked dropdown, hides the Remove button, and displays the `MarkedAt` timestamp inline. The officer remains on the screen.
- **Selecting an Entered number:** Immediately calls `POST /api/submissions/enter`. On success: disables all controls for that file (including the Entered dropdown and Remove button) and displays the `EnteredAt` timestamp inline. The officer remains on the screen.
- **Remove:** Only visible and active while both `markedValue` and `enteredValue` are null. Calls the existing remove/delete endpoint. Behaviour is unchanged from current design.
- **Fully Entered file:** All controls are disabled. The file row is displayed read-only — the officer can see the classification values and timestamps but cannot make changes.
- **Marked-only file:** The Marked dropdown is disabled (cannot re-mark). The Entered dropdown remains active so the officer can Enter it later. Remove is hidden.

#### Pre-submit validation

Before the officer submits the form, validate that every file in the submission has at least one of `markedValue` or `enteredValue` set. If any file is unclassified, show an inline error beneath that file row: _"This exhibit must be Marked, Entered, or removed before submitting."_ The Submit button remains disabled until all files pass this check.

---

### 2. Ticket Number History Lookup

A **"Exhibit History"** lookup is added to the Exhibit Upload screen. The officer enters a ticket number (file number text) and the UI fetches all exhibits across all submissions linked to that ticket, displaying a read-only table:

| File Name   | Submission Date | Marked | Marked At        | Entered | Entered At       |
| ----------- | --------------- | ------ | ---------------- | ------- | ---------------- |
| exhibit.mp4 | 2026-05-15      | A      | 2026-05-15 09:45 | 3       | 2026-05-20 14:10 |
| footage.mov | 2026-05-15      | —      | —                | 2       | 2026-05-15 10:00 |

This is read-only. Officers use it to confirm whether a Marked exhibit has been assigned an Entered number at a prior session, or to retrieve the context before a JJ makes a determination.

---

### 3. TypeScript Model Updates

#### Updated file model (`StoredFileModel.ts` or equivalent)

```typescript
interface StoredFileModel {
  id: number;
  fileName: string;
  // ... existing fields ...
  markedValue: string | null; // Letter A–Z, or null if not Marked
  markedAt: string | null; // ISO 8601 UTC string, or null
  enteredValue: string | null; // "1"–"50", or null if not Entered
  enteredAt: string | null; // ISO 8601 UTC string, or null
}
```

#### New request model

```typescript
interface ExhibitMarkModel {
  fileId: number;
  markedValue: string; // Single letter A–Z
}

interface ExhibitEnterModel {
  fileId: number;
  enteredValue: string; // "1"–"50"
}
```

---

## Backend Changes

### 1. Database Schema

#### Modified table: `StoredFiles`

Add four nullable columns:

| Column         | Type          | Nullable | Notes                                                      |
| -------------- | ------------- | -------- | ---------------------------------------------------------- |
| `MarkedValue`  | `varchar(1)`  | Yes      | Letter A–Z (stored uppercase); null if not Marked          |
| `MarkedAt`     | `timestamptz` | Yes      | UTC timestamp set at time of marking; null if not Marked   |
| `EnteredValue` | `varchar(2)`  | Yes      | Number 1–50 as string; null if not Entered                 |
| `EnteredAt`    | `timestamptz` | Yes      | UTC timestamp set at time of entering; null if not Entered |

**Migration:** A single EF Core migration adds these four nullable columns with no default value. Existing rows have all four as null (retroactively unclassified; no data-copy step required).

#### Updated entity (C#)

```csharp
public class StoredFile : BaseEntity
{
    // ... existing properties ...
    public string? MarkedValue { get; set; }
    public DateTime? MarkedAt { get; set; }
    public string? EnteredValue { get; set; }
    public DateTime? EnteredAt { get; set; }
}
```

---

### 2. API Endpoints

#### `POST /api/submissions/mark`

Marks an exhibit with a letter.

**Request body (JSON):**

```json
{
  "fileId": 42,
  "markedValue": "A"
}
```

**Business rules enforced:**

- `markedValue` must be a single letter A–Z (normalise to uppercase).
- The file must not already have `MarkedValue` set — reject with `400` if already Marked.
- The file must not already have `EnteredValue` set — reject with `400` (cannot Mark after Entering).
- On success: set `MarkedValue` and `MarkedAt = DateTime.UtcNow`.

**Response:** `200 OK` with the updated `StoredFileModel` (including timestamps); `400` on validation failure; `404` if file not found.

---

#### `POST /api/submissions/enter`

Enters an exhibit with a number.

**Request body (JSON):**

```json
{
  "fileId": 42,
  "enteredValue": "3"
}
```

**Business rules enforced:**

- `enteredValue` must be a string representation of an integer between 1 and 50.
- The file must not already have `EnteredValue` set — reject with `400` if already Entered.
- The file may be Unclassified (direct entry) or in Marked state; both are valid.
- On success: set `EnteredValue` and `EnteredAt = DateTime.UtcNow`.

**Response:** `200 OK` with the updated `StoredFileModel`; `400` on validation failure; `404` if not found.

---

#### `GET /api/submissions/exhibits-by-ticket?ticketNumber={ticketNumber}`

> **Cross-reference — share retrieval with [multi-ticket-exhibit-upload.md](multi-ticket-exhibit-upload.md).** That feature is targeted **first** and introduces `GET /api/submissions/by-file-number` plus `GetSubmissionsByFileNumberAsync` on `SubmissionService`, which already retrieves every submission/file for a `FileNumberText` across all dates, locations, and rooms (keyed on `SubmissionTickets.FileNumberText`, **not** `appearanceId`). When this classification work begins, the `ticketNumber` parameter **is** that `FileNumberText` — do not build a second query. Reuse the multi-ticket service method and project the per-file classification shape below from its results, or add this flat shape as a second projection on the shared method. The `StoredFiles ↔ FileNumberText` join path (via `Submission.Tickets`) already exists once multi-ticket ships.

Returns all exhibit files linked to a ticket number across all submissions, with classification state.

**Response:** Array of exhibit records:

```json
[
  {
    "fileId": 42,
    "fileName": "exhibit.mp4",
    "submissionId": 10,
    "submissionDate": "2026-05-15T09:30:00Z",
    "markedValue": "A",
    "markedAt": "2026-05-15T09:45:00Z",
    "enteredValue": "3",
    "enteredAt": "2026-05-20T14:10:00Z"
  }
]
```

**Access:** Officer and Admin roles. Returns an empty array (not 404) when no exhibits are found for the ticket number.

---

#### Updated: file remove endpoint

The existing file remove / delete endpoint must add a guard:

- If the file has a non-null `MarkedValue` **or** a non-null `EnteredValue`, return `409 Conflict` with body: `"Classified exhibits cannot be removed."`.

---

### 3. Business Logic Layer

Add the following service methods (in `SubmissionService` or a dedicated `ExhibitClassificationService`):

```csharp
Task<StoredFileModel> MarkExhibitAsync(int fileId, string markedValue);
Task<StoredFileModel> EnterExhibitAsync(int fileId, string enteredValue);
Task<IEnumerable<StoredFileModel>> GetExhibitsByTicketNumberAsync(string ticketNumber);
```

Each method is responsible for enforcing its state machine rules before persisting. The controller delegates entirely to the service; no business rules live in the controller.

> **`GetExhibitsByTicketNumberAsync` reuses the multi-ticket retrieval.** Because [multi-ticket-exhibit-upload.md](multi-ticket-exhibit-upload.md) ships first and already provides `GetSubmissionsByFileNumberAsync(string fileNumberText)` (the same `FileNumberText` key), implement `GetExhibitsByTicketNumberAsync` as a thin projection over that method rather than a parallel `.Where(...).Include(...)` query. This keeps the cross-date join and soft-delete filtering defined in one place.

---

### 4. CHUNK Drive `.txt` File Format

The `.txt` file written alongside submitted exhibits on the CHUNK drive must include classification data for each file. Append the following fields to each file's block in the `.txt` output:

```
File: exhibit.mp4
...existing fields...
Marked: A
Marked At: 2026-05-15 09:45:00 UTC
Entered: 3
Entered At: 2026-05-20 14:10:00 UTC
```

When a classification field has not been set at the time of the push, write `—` as the value (e.g., `Marked: —`, `Marked At: —`). The `.txt` captures the state at push time; updates to classification after the initial push are out of scope for this spec.

---

### 5. Response Model Updates

The file sub-model returned by submission review, listing, and the new classification endpoints must expose the classification fields:

```csharp
public class StoredFileModel
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    // ... existing fields ...
    public string? MarkedValue { get; set; }
    public DateTime? MarkedAt { get; set; }
    public string? EnteredValue { get; set; }
    public DateTime? EnteredAt { get; set; }
}
```

Update `ToReviewModel()` (or equivalent mapping) to project these four fields from the entity.

---

## Admin Changes

### Submission Review (`SubmissionReview.vue`)

Each file listed in the submission review panel gains classification columns:

| File Name   | Marked | Marked At        | Entered | Entered At       |
| ----------- | ------ | ---------------- | ------- | ---------------- |
| exhibit.mp4 | A      | 2026-05-15 09:45 | 3       | 2026-05-20 14:10 |
| footage.mov | —      | —                | 2       | 2026-05-15 10:00 |
| clip.avi    | B      | 2026-05-15 10:30 | —       | —                |

All classification fields are read-only in the admin view. A JJ or admin sees the full classification state at a glance alongside the file list.

---

## Testing

Per the project testing rule, all new service methods, controller actions, store mutations, and service functions require tests. Frameworks and structure follow [spec/testing-implementation.md](testing-implementation.md).

### Backend (xUnit)

**Unit tests — CES.Business.Tests**

| Test                                                 | Behaviour                                                                                                        |
| ---------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `MarkExhibit_PersistsMarkedValueAndTimestamp`        | Calling `MarkExhibitAsync` on an Unclassified file sets `MarkedValue` (uppercase) and `MarkedAt` in the DB       |
| `MarkExhibit_NormalisesLetterToUppercase`            | Input `"a"` is stored as `"A"`                                                                                   |
| `MarkExhibit_Rejects_WhenAlreadyMarked`              | File already has `MarkedValue` set → throws or returns error                                                     |
| `MarkExhibit_Rejects_WhenAlreadyEntered`             | File has `EnteredValue` set → throws or returns error                                                            |
| `EnterExhibit_PersistsEnteredValueAndTimestamp`      | Calling `EnterExhibitAsync` on an Unclassified file sets `EnteredValue` and `EnteredAt`                          |
| `EnterExhibit_SucceedsOnMarkedFile`                  | File has `MarkedValue` set; Enter succeeds and both fields are present                                           |
| `EnterExhibit_Rejects_WhenAlreadyEntered`            | File already has `EnteredValue` set → throws or returns error                                                    |
| `GetExhibitsByTicketNumber_ReturnsAllMatchingFiles`  | Files across multiple submissions linked to the same ticket number are returned with correct classification data |
| `GetExhibitsByTicketNumber_ReturnsEmpty_WhenNoMatch` | Unknown ticket number returns empty collection, not an error                                                     |
| `RemoveExhibit_Rejects_WhenMarked`                   | Remove blocked for file with `MarkedValue` set                                                                   |
| `RemoveExhibit_Rejects_WhenEntered`                  | Remove blocked for file with `EnteredValue` set                                                                  |

**Integration tests — CES.API.Tests**

| Test                                                                     | Expected                                                   |
| ------------------------------------------------------------------------ | ---------------------------------------------------------- |
| `POST /api/submissions/mark` — valid request → `200`                     | `markedValue` and `markedAt` present in response body      |
| `POST /api/submissions/mark` — already Marked → `400`                    | Error message in response                                  |
| `POST /api/submissions/mark` — already Entered → `400`                   | Error message in response                                  |
| `POST /api/submissions/mark` — invalid letter (e.g. `"AA"`) → `400`      | Validation error                                           |
| `POST /api/submissions/enter` — valid (direct) → `200`                   | `enteredValue` and `enteredAt` present; `markedValue` null |
| `POST /api/submissions/enter` — valid (after Mark) → `200`               | Both `markedValue` and `enteredValue` present              |
| `POST /api/submissions/enter` — already Entered → `400`                  | Error message                                              |
| `POST /api/submissions/enter` — value out of range (e.g. `"51"`) → `400` | Validation error                                           |
| `GET /api/submissions/exhibits-by-ticket?ticketNumber=X` → `200`         | Array contains files with classification data              |
| `GET /api/submissions/exhibits-by-ticket?ticketNumber=UNKNOWN` → `200`   | Empty array                                                |
| Remove classified file → `409`                                           | `"Classified exhibits cannot be removed."`                 |
| All new endpoints — unauthenticated → `401`                              | Auth required                                              |

### Frontend (Vitest)

| Test                                                | Behaviour                                                                           |
| --------------------------------------------------- | ----------------------------------------------------------------------------------- |
| `Marked dropdown is active for unclassified file`   | Dropdown is enabled when both `markedValue` and `enteredValue` are null             |
| `Marked dropdown disabled after selection`          | After selecting a letter, Marked dropdown is disabled                               |
| `Marked dropdown disabled when enteredValue is set` | A file with `enteredValue` has its Marked dropdown disabled                         |
| `Entered dropdown active for unclassified file`     | Entered dropdown enabled when `enteredValue` is null                                |
| `Entered dropdown active for Marked file`           | File with `markedValue` set but no `enteredValue` still has Entered dropdown active |
| `Entered dropdown disabled after selection`         | After selecting a number, both dropdowns are disabled                               |
| `Remove button visible for unclassified file`       | Remove shown when both `markedValue` and `enteredValue` are null                    |
| `Remove button hidden after Marking`                | Remove is hidden once `markedValue` is set                                          |
| `Remove button hidden after Entering`               | Remove is hidden once `enteredValue` is set                                         |
| `MarkedAt timestamp appears when markedAt is set`   | Timestamp text visible in DOM when `markedAt` is non-null                           |
| `EnteredAt timestamp appears when enteredAt is set` | Timestamp text visible when `enteredAt` is non-null                                 |
| `Mark action calls POST /api/submissions/mark`      | Dropdown change triggers the mark endpoint with correct payload                     |
| `Enter action calls POST /api/submissions/enter`    | Dropdown change triggers the enter endpoint with correct payload                    |
| `Fully-Entered file row is entirely read-only`      | All controls disabled when both `markedValue` and `enteredValue` are set            |
| `Submit blocked with unclassified file present`     | Inline error visible; Submit button disabled when any file has no classification    |
| `Submit allowed when all files are at least Marked` | No validation error when every file has `markedValue` or `enteredValue` set         |
| `Ticket history lookup renders result rows`         | Mocked API response renders correct file rows in the history table                  |
| `Ticket history lookup renders empty state`         | Empty array response shows an appropriate empty-state message                       |

**Existing tests to update:** `SubmissionServiceTests.cs`, `SubmissionsControllerTests.cs`, `FilesControllerTests.cs`, `SubmissionService.spec.ts` (add `markedValue`/`enteredValue` to mocked file models where present).

---

## Open Questions / Follow-up Items

1. **Cross-session Enter access:** Should any authenticated officer be able to Enter an exhibit that was Marked at a prior session by a different officer, or is access scoped to the original submitting officer?

- This is not a consideration at this time. Anyone who accesses the submissions can perform any required action.

2. **CHUNK drive re-push on late Entering:** If an exhibit is Marked at Session 1 and the CHUNK drive file is written then, a subsequent Enter at Session 2 leaves the stored `.txt` stale. Does the CHUNK drive record need updating, or is a supplemental write acceptable?

- The file should only be saved to chunk AFTER a submission has been Entered. Before that a '.txt' is not written yet.

3. **Ticket history lookup placement:** Should the lookup live embedded on the Exhibit Upload screen (contextual) or as a separate Officer view (cleaner navigation)? Embedding keeps the officer on one screen; a separate view reduces clutter on the upload form.

- It can be on the Exhibit Upload screen but in a hidden format that only displays when a badge or link is clicked to show in a popup style that is non-intrussive. This information is no relevant generally so it shouldn't take up screen space.

4. **Audit log vs. timestamp columns:** Are the four timestamp columns on `StoredFiles` sufficient for audit purposes, or should classification events also be written to a separate event/audit table for a full immutable log?

- Audit should be written to a separate event/audit table. This audit table should be generic enough it can track any required auditing information in relation to a submission having its state or information changed.

5. **Marked-only file at submission close:** Can a submission be accepted/closed by an admin while some of its files are still in Marked-only state (awaiting Entered)? Define whether admin Accept/Reject actions are blocked until all files reach Marked & Entered or Entered (direct).

- At this moment do not focus on the submission close. A submission will be automatically accepted in the future once 'Marked', but that will follow once can validate the functionality through the Officer views.
