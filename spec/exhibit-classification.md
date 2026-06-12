# Exhibit Classification — Marked, Entered, and Description

**Status:** Draft
**Date:** 2026-06-04
**Revised:** 2026-06-12 — reframed onto the Prior Exhibits list (post-submit), added Description editing, the 10-second UI correction window, in-browser-only viewing, save feedback indicators, and a generic submission audit log. Aligned identifiers with the shipped [multi-ticket-exhibit-upload.md](multi-ticket-exhibit-upload.md) (Guid file ids, `SubmissionFile`, `GetSubmissionsByFileNumberAsync`).
**JIRA:** CES-28

---

## Overview

After an exhibit has been **submitted** for a ticket, an officer classifies it under direction of the JJ: "Marked" (a letter A–Z, denoting the exhibit's validity as acknowledged by the JJ), "Entered" (a number 1–50, denoting admission into evidence), or both — in that order. The officer may also maintain a free-text **Description** on each exhibit. Classification and description changes are recorded with timestamps and written to an audit log so the full lifecycle of each exhibit is reconstructable.

Classification happens in the **Prior Exhibits list** on the Exhibit Upload screen — the per-ticket panel introduced by [multi-ticket-exhibit-upload.md](multi-ticket-exhibit-upload.md), which surfaces every exhibit previously submitted for the selected ticket's `FileNumberText` across all dates. That feature shipped first and already provides the retrieval (`GetSubmissionsByFileNumberAsync` → `PriorSubmissionModel`/`SubmissionFile`); this feature makes the per-file rows in that panel editable and adds the classification fields to the shared `SubmissionFile` shape.

> **Files in the DropZone are not classified.** A file being uploaded in the current submission has no Marked/Entered/Description controls. It can only be removed (before submit). Classification begins once the file is submitted and reappears as a Prior Exhibit.

---

## Exhibit Classification State Machine

Each submitted file is in exactly one **status**, shown on its row:

| Status       | Marked Value | Entered Value | Meaning                                                       |
| ------------ | ------------ | ------------- | ------------------------------------------------------------ |
| Unclassified | blank        | blank         | Submitted; no classification yet                             |
| Marked       | A–Z          | blank         | Officer has Marked the exhibit; awaiting an Entered decision |
| Entered      | A–Z or blank | 1–50          | Admitted into evidence (with or without a prior Mark). **Terminal — read-only to the officer.** |

> The previously separate "Marked & Entered" and "Entered (direct)" states are both reported simply as **Entered**. The status reflects the furthest progression reached; the `MarkedValue`/`EnteredValue` pair still records exactly which designations were applied.

### Allowed transitions

| From         | To      | Trigger                                                                  |
| ------------ | ------- | ------------------------------------------------------------------------ |
| Unclassified | Marked  | Officer selects a letter from the Marked dropdown                        |
| Unclassified | Entered | Officer selects a number from the Entered dropdown (no letter required)  |
| Marked       | Entered | Officer selects a number from the Entered dropdown                       |

### Disallowed transitions

- **Entered → any change:** Once Entered, the exhibit is terminal. The officer cannot change Marked, Entered, or Description. Enforced both in the UI (disabled controls) and on the backend (see Backend §2 — the only hard server-side lock).
- **Entered → Marked:** Implied by the above — an exhibit that has been Entered cannot subsequently be Marked.

### The 10-second correction window (UI only)

To absorb human error, a **Marked** or **Entered** value the officer just set remains editable for **10 seconds** after it is set. Within that window the officer may pick a different value (e.g. correct `A` → `B`). After 10 seconds that control disables permanently and the value can never again be changed by the officer.

- For **Marked**, the window is **UI-controlled only** — the backend persists any Marked value it receives on a not-yet-Entered file and does not police the 10 seconds. For **Entered**, the backend *does* read `EnteredAt` so it can permit the in-window correction and lock afterward (Open Question A, option i). See [[`CLASSIFICATION_EDIT_WINDOW_SECONDS`]] in Constants.
- **Selecting Entered locks Marked and Description immediately** — even inside the Entered value's own 10-second window, only the Entered dropdown stays correctable.
- **On screen load**, Prior Exhibits are rendered from their persisted state. Any Marked/Entered value already set is treated as past its window and is rendered **disabled** — the load path never re-opens a 10-second window for historical data.
- Description has **no** correction window; it stays editable until the exhibit is Entered (then locks with everything else).

---

## User Stories

1. As an officer, I can mark a submitted exhibit with a letter (A–Z) at the direction of the JJ so that the exhibit's validity is documented, while staying on the upload screen.
2. As an officer, I can enter a submitted exhibit with a number (1–50) — either after Marking it or directly — so that admission into evidence is recorded.
3. As an officer, I can correct a Marked or Entered value I just set within 10 seconds, after which it locks, so that a slip of the dropdown is recoverable but the record stays trustworthy.
4. As an officer, I can edit an exhibit's Description until it is Entered so that contextual notes can be maintained, with every change tracked.
5. As an officer, I can mark an exhibit at one court session and enter it at a future session so that the full classification lifecycle is preserved across dates.
6. As an officer, I get immediate visual confirmation (green check) when a change saves, and a clear error (red ✕ with the message) when it fails, so that I know the record is current.
7. As an officer, I can view a Prior Exhibit in my browser (without downloading it) so that I can confirm its contents; if a file is not browser-viewable, I cannot view it.
8. As a JJ/Admin, I can retrieve exhibits by ticket number and review their classification state, description, and timestamps at any time, including the change history, so that I have a complete record.

---

## Scope

| Area                                                            | In Scope                                                      |
| -------------------------------------------------------------- | ------------------------------------------------------------ |
| Officer — Prior Exhibits list (Exhibit Upload screen)          | Yes — per-file Marked / Entered / Description editing + status |
| Officer — in-browser view of a Prior Exhibit                   | Yes — view only, no download; gated on browser-viewable type  |
| Exhibit state machine (Unclassified / Marked / Entered)        | Yes                                                          |
| 10-second correction window                                    | Yes — UI only                                               |
| Save-feedback indicators (green check / red ✕)                 | Yes                                                         |
| MarkedAt / EnteredAt / Description timestamps — display        | Yes                                                         |
| Generic submission audit log table + writes                    | Yes                                                         |
| CHUNK drive `.txt` file (written only after Entered)           | Yes                                                         |
| Ticket-number retrieval of exhibit history (popup)             | Yes — reuses the multi-ticket retrieval                     |
| Backend API — mark / enter / update-description endpoints      | Yes                                                         |
| Database schema — classification + description on `StoredFiles`| Yes                                                         |
| Admin — Submission Review screen                               | Yes — read-only classification display; remove guard (unless Entered) |
| Officer removal of submitted exhibits                          | No — officers cannot remove a submitted exhibit (DropZone only) |
| Email notifications / file storage (binary) / auth             | No — no change                                              |

---

## Decisions Made

| Decision                          | Choice                                                                                          | Rationale                                                                                              |
| --------------------------------- | ---------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| Where classification happens      | In the **Prior Exhibits list** (post-submit), not on DropZone uploads                          | An exhibit is only classifiable once it exists in the system; the prior-exhibits panel already surfaces every submitted file for the ticket |
| DropZone files                    | Not classifiable; removable before submit only                                                 | Pre-submit there is no persisted exhibit to classify or audit                                          |
| Status set                        | Unclassified / Marked / Entered (Entered is terminal)                                          | Simpler than the four-state model; "Marked & Entered" and "Entered direct" both read as Entered        |
| Marked value set                  | Dropdown A–Z + blank default                                                                    | Blank means not Marked; officer selects at direction of JJ                                             |
| Entered value set                 | Dropdown 1–50 + blank default                                                                   | Blank means not yet Entered                                                                            |
| Description                       | Free-text field on each exhibit, editable until Entered                                         | Officers maintain contextual notes; tracked for audit                                                  |
| Persistence trigger               | Immediate API call on each change (dropdown select / description blur); no global Save button   | Officer stays on screen                                                                                |
| 10-second correction window       | Marked/Entered editable for 10s after set, then permanently disabled. **Marked window is UI-only**; the **Entered** window is also honoured server-side (Open Question A → option i) | Absorbs slips without a re-open path; Marked stays UI-driven, but the Entered correction must survive a real request so the backend reads `EnteredAt` |
| Terminal lock enforcement         | Backend rejects mark/description changes once `EnteredValue` is set, and rejects enter once `EnteredAt` is older than `CLASSIFICATION_EDIT_WINDOW_SECONDS` (in-window enter correction allowed) | The server-side invariant protecting an admitted-into-evidence record, with the one carve-out for the 10s Entered correction |
| Officer removal                   | Officers cannot remove a submitted exhibit (only DropZone files, pre-submit)                    | Once in the system the record is retained                                                              |
| Admin removal                     | Admin may remove an exhibit **unless** it is Entered                                            | Admins manage erroneous uploads, but an Entered exhibit is immutable                                   |
| Viewing                           | Officer gets in-browser view (`/api/files/{fileId}/view`) only; no download; hidden when the content type is not browser-viewable | Officers confirm contents without taking a local copy                                                  |
| Save feedback                     | Green ✓ on success (fades after 5s, re-shown on next change); red ✕ with hover error on failure | Immediate per-row confirmation without a page-level banner                                             |
| Audit storage                     | A **generic** `SubmissionAuditLogs` table records field-level changes (Marked, Entered, Description, …) | Reusable for any future tracked submission/file change, not just these three fields                    |
| Cross-session actions / ownership | Any authenticated officer with access may act on any exhibit                                    | Open Question 1 — no per-officer scoping                                                               |
| CHUNK `.txt` timing               | Written only **after** an exhibit is Entered                                                    | Open Question 2 — no stale early write to reconcile                                                    |
| Ticket history placement          | Hidden popup opened from a badge/link on the Exhibit Upload screen                              | Open Question 3 — non-intrusive; not relevant to the general flow                                      |
| Retrieval endpoint                | Reuse the shipped `GET /api/submissions/by-file-number` (`GetSubmissionsByFileNumberAsync`)    | Open Question 4 — one query, one shape; the separate `exhibits-by-ticket` endpoint is dropped          |
| Timestamp format                  | UTC stored; local time displayed as `YYYY-MM-DD HH:mm`                                          | Consistent storage; human-readable display                                                            |

---

## Constants

Per the project code-style rule, no magic numbers inline. Introduce:

| Constant                              | Value | Where                              | Notes                                                      |
| ------------------------------------- | ----- | ---------------------------------- | ---------------------------------------------------------- |
| `CLASSIFICATION_EDIT_WINDOW_SECONDS`  | `10`  | Frontend constants module          | Seconds a just-set Marked/Entered value stays editable (UI) |
| `SAVE_INDICATOR_FADE_SECONDS`         | `5`   | Frontend constants module          | Seconds the green success check stays visible before fading |
| `DESCRIPTION_MAX_LENGTH`              | `250` | Shared (FE + BE validation)        | Max Description length; UI shows a live remaining-characters counter |
| `MARKED_MIN` / `MARKED_MAX`           | `A` / `Z` | Shared (FE + BE validation)    | Marked dropdown range                                       |
| `ENTERED_MIN` / `ENTERED_MAX`         | `1` / `50` | Shared (FE + BE validation)   | Entered dropdown range                                      |

---

## Frontend Changes

### 1. Prior Exhibits list (`SubmissionForm.vue`)

The Prior Exhibits panel (one collapsible section per selected ticket, from the multi-ticket feature) becomes the home of all classification editing. Each prior-exhibit row gains:

| Control                       | Type            | Enabled when                                                                                   |
| ----------------------------- | --------------- | ---------------------------------------------------------------------------------------------- |
| **Status** chip               | Read-only badge | Always — shows Unclassified / Marked / Entered                                                  |
| **Marked** dropdown (A–Z)     | Select          | Status is Unclassified, **and** (`markedValue` is null **or** still inside its 10s window)      |
| **Entered** dropdown (1–50)   | Select          | `enteredValue` is null **or** still inside its 10s window; otherwise disabled                   |
| **Description** field         | Text input/area | Status is not Entered; max `DESCRIPTION_MAX_LENGTH` (250) chars with a live remaining-characters counter |
| **View** button/icon          | Action          | The file's `contentType` is browser-viewable (see §3); never offers download                   |
| **Marked at / Entered at**    | Read-only text  | Shown when the corresponding timestamp is set                                                   |
| **Save indicator**            | ✓ / ✕ slot      | Appears after a save attempt (see §4)                                                           |

#### Interaction rules

- **Select a Marked letter:** calls `POST /api/files/{fileId}/mark`. On success, the status chip becomes **Marked**, `MarkedAt` shows, and a 10-second timer starts; while it runs the Marked dropdown stays enabled (to allow a correction). When it elapses the Marked dropdown disables permanently.
- **Select an Entered number:** calls `POST /api/files/{fileId}/enter`. On success, status becomes **Entered**, `EnteredAt` shows, a 10-second timer starts on the Entered dropdown, and **the Marked dropdown and Description field disable immediately** (Entered is terminal — only the just-set Entered value retains its short correction window). When the timer elapses every control on the row is disabled.
- **Edit Description:** persists on blur (or debounced) via `PATCH /api/files/{fileId}/description`. Allowed until the exhibit is Entered. The input caps at `DESCRIPTION_MAX_LENGTH` (250) and shows a live counter of remaining characters.
- **On load:** rows render from persisted state; any already-set Marked/Entered value is rendered disabled (window treated as expired). An already-Entered row is fully read-only except for View.
- **No Remove on prior exhibits.** A submitted exhibit cannot be removed by the officer; there is no Remove control in this panel. (Removal exists only in the DropZone for not-yet-submitted files — unchanged from current behaviour.)

#### Status chip mapping

`Unclassified` when both values null · `Marked` when `markedValue` set and `enteredValue` null · `Entered` when `enteredValue` set.

> **No pre-submit classification validation.** Because classification is post-submit, the previous "every file must be Marked/Entered before Submit" rule is **removed**. The Submit button's enablement reverts to its pre-classification behaviour (files present + officer number, per the multi-ticket spec).

### 2. Save-feedback indicators

After any per-row save (mark / enter / description):

- **Success:** a small **green checkmark** appears on the row. It fades out after `SAVE_INDICATOR_FADE_SECONDS` (5s). If the same row is updated again, the checkmark reappears and the timer restarts.
- **Failure:** a small **red ✕** appears on the row and persists (does not fade). Hovering it shows a tooltip with the server error message. The next successful save replaces it with the green check.

### 3. In-browser viewing

- The View action opens `GET /api/files/{fileId}/view`, which streams the file **inline** (`FileStreamResult` with range processing — already implemented in `FilesController.View`). The officer is never offered `GET /api/files/{fileId}/download`.
- The View action is only rendered when the file's `contentType` is one the browser can render inline (e.g. `video/*`, `image/*`, `application/pdf`, `audio/*`). For any other type, no view affordance is shown — "if it cannot be browser-viewable it cannot be viewed by the officer." Maintain the allowed-prefix list as a frontend constant.

### 4. Ticket-number History popup

A small **badge/link** on the Exhibit Upload screen opens a **non-intrusive popup** (dialog) where the officer can look up any ticket number and see a read-only history table. It does not occupy page space when closed.

| File Name   | Submission Date | Status | Marked | Marked At        | Entered | Entered At       | Description |
| ----------- | --------------- | ------ | ------ | ---------------- | ------- | ---------------- | ----------- |
| exhibit.mp4 | 2026-05-15      | Entered | A     | 2026-05-15 09:45 | 3       | 2026-05-20 14:10 | Bodycam     |
| footage.mov | 2026-05-15      | Marked  | B     | 2026-05-15 10:00 | —       | —                | —           |

The popup reuses `GET /api/submissions/by-file-number` (the same retrieval that fills the inline Prior Exhibits panel) and is read-only.

### 5. TypeScript Model Updates

The shared per-file model is **`SubmissionFile`** (used by `PriorSubmissionModel` and `SubmissionReviewModel`). File ids are **GUID strings**, and the original file name field is `originalFileName`.

```typescript
interface SubmissionFile {
  id: string;                 // Guid
  originalFileName: string;
  contentType: string;
  url: string;
  fileSize: number;
  status: string;             // "Unclassified" | "Marked" | "Entered" (replaces "Pending")
  // ── classification (new) ──
  markedValue: string | null; // "A"–"Z", or null
  markedAt: string | null;    // ISO 8601 UTC, or null
  enteredValue: string | null;// "1"–"50", or null
  enteredAt: string | null;   // ISO 8601 UTC, or null
  description: string | null; // free text, or null
}

interface ExhibitMarkModel  { markedValue: string }   // single letter A–Z
interface ExhibitEnterModel { enteredValue: string }  // "1"–"50"
interface ExhibitDescriptionModel { description: string }
```

`fileId` is passed in the route, not the body.

---

## Backend Changes

### 1. Database Schema

#### Modified table: `StoredFiles`

The entity is `CES.Entities.StoredFiles` (it has its own audit fields — `CreatedDateUTC`, `CreatedBy`, `UpdatedBy`, `UpdatedDateUTC`, `IsDeleted` — and a **`Guid Id`**; it does **not** derive from `BaseEntity`). Add five nullable columns:

| Column         | Type          | Nullable | Notes                                                      |
| -------------- | ------------- | -------- | ---------------------------------------------------------- |
| `MarkedValue`  | `varchar(1)`  | Yes      | Letter A–Z (stored uppercase); null if not Marked          |
| `MarkedAt`     | `timestamptz` | Yes      | UTC timestamp set when Marked; null otherwise              |
| `EnteredValue` | `varchar(2)`  | Yes      | Number 1–50 as string; null if not Entered                 |
| `EnteredAt`    | `timestamptz` | Yes      | UTC timestamp set when Entered; null otherwise             |
| `Description`  | `text`        | Yes      | Free-text officer description; null if none                |

```csharp
public class StoredFiles
{
    public Guid Id { get; set; }
    // ... existing properties (OriginalFileName, StoredPath, ContentType, audit fields, IsDeleted) ...
    public string? MarkedValue { get; set; }
    public DateTime? MarkedAt { get; set; }
    public string? EnteredValue { get; set; }
    public DateTime? EnteredAt { get; set; }
    public string? Description { get; set; }
}
```

**Migration:** one EF Core migration adds the five nullable columns with no default. Existing rows are null on all five (retroactively Unclassified, no description).

#### New table: `SubmissionAuditLogs` (generic)

A reusable change-log keyed to a submission (and optionally a file). Every tracked field change writes one row.

| Column         | Type          | Nullable | Notes                                                          |
| -------------- | ------------- | -------- | -------------------------------------------------------------- |
| `Id`           | `int`         | No       | PK, identity                                                   |
| `SubmissionId` | `int`         | No       | FK → `Submissions.Id`                                          |
| `FileId`       | `uuid`        | Yes      | FK → `StoredFiles.Id`; null for submission-level changes        |
| `FieldName`    | `varchar`     | No       | e.g. `MarkedValue`, `EnteredValue`, `Description`               |
| `OldValue`     | `text`        | Yes      | Previous value (null when first set)                           |
| `NewValue`     | `text`        | Yes      | New value                                                      |
| `ChangedBy`    | `varchar`     | Yes      | Target identity is the user's **id/email**; not reliably available until Keycloak integration, so populate with the best available principal value (or a placeholder) until then |
| `ChangedAtUTC` | `timestamptz` | No       | `SystemDate.UtcNow()`                                          |

```csharp
public class SubmissionAuditLog
{
    public int Id { get; set; }
    public int SubmissionId { get; set; }
    public Guid? FileId { get; set; }
    public string FieldName { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAtUTC { get; set; } = SystemDate.UtcNow();
}
```

**EF Core registration:** add `DbSet<SubmissionAuditLog> SubmissionAuditLogs` to `ICESDataStore` (`CES.Entities/Interfaces/ICESDataStore.cs`) and `CESDataStore` (`CES.EF/CESDataStore.cs`), and configure the FKs in `OnModelCreating` (no cascade delete from `StoredFiles`, since `FileId` is nullable; cascade from `Submissions` is acceptable). The migration creates this table alongside the `StoredFiles` columns.

### 2. API Endpoints

All three mutation endpoints take the **GUID file id in the route**, write a `SubmissionAuditLog` row on success, and return the updated `SubmissionFile`. The backend enforces the terminal **Entered** lock; it does **not** police the Marked correction window or reject re-marks within the pre-Entered window — those are the UI's responsibility. The **one** timestamp the backend reads is `EnteredAt`, to permit the in-window Entered correction (Open Question A, option i): a file whose `EnteredValue` is set is locked **once `EnteredAt` is older than `CLASSIFICATION_EDIT_WINDOW_SECONDS`**; within that window the Entered value may still be overwritten via the enter endpoint.

#### `POST /api/files/{fileId}/mark`

```json
{ "markedValue": "A" }
```

- `markedValue` must be a single letter A–Z (normalised to uppercase) — `400` otherwise.
- If `EnteredValue` is set → **`409 Conflict`**, `"Entered exhibits cannot be modified."` Marked locks the instant the file is Entered, **including within the Entered correction window** — only the Entered value itself is correctable in that window, never Marked.
- On success: set `MarkedValue` + `MarkedAt = SystemDate.UtcNow()`; audit `FieldName="MarkedValue"`, `OldValue=<previous>`, `NewValue=<new>`.
- `200 OK` with updated `SubmissionFile`; `404` if file not found.

#### `POST /api/files/{fileId}/enter`

```json
{ "enteredValue": "3" }
```

- `enteredValue` must parse to an integer in `[ENTERED_MIN, ENTERED_MAX]` (1–50) — `400` otherwise.
- May be applied to an Unclassified or a Marked file; both valid.
- **Terminal lock + in-window correction (Open Question A → option i):** if `EnteredValue` is already set, the backend reads `EnteredAt`:
  - within `CLASSIFICATION_EDIT_WINDOW_SECONDS` of `EnteredAt` → **accept** the new value (correction), overwrite `EnteredValue`, and audit it; `EnteredAt` is **not** advanced (the window is measured from the original set, so a correction cannot extend the editable period).
  - past the window → **`409 Conflict`**, `"Entered exhibits cannot be modified."`
- On a first set: set `EnteredValue` + `EnteredAt = SystemDate.UtcNow()`; audit `FieldName="EnteredValue"` (old/new).
- `200 OK` with updated `SubmissionFile`; `404` if not found.

#### `PATCH /api/files/{fileId}/description`

```json
{ "description": "Front-door bodycam, 09:12–09:40" }
```

- `description` must be at most `DESCRIPTION_MAX_LENGTH` (250) characters — `400` otherwise. The UI also enforces this, but the backend validates it independently.
- If `EnteredValue` is set → **`409 Conflict`** (Description locks once Entered, including within the Entered correction window — only the Entered value is correctable there).
- On success: set `Description`; audit `FieldName="Description"`, `OldValue`/`NewValue`.
- `200 OK` with updated `SubmissionFile`; `404` if not found.

#### Removed / consolidated retrieval

The standalone `GET /api/submissions/exhibits-by-ticket` from the earlier draft is **dropped**. The Prior Exhibits panel and the History popup both consume the shipped **`GET /api/submissions/by-file-number?fileNumberText={…}`** (`GetSubmissionsByFileNumberAsync` → `List<PriorSubmissionModel>`). The classification fields ride along on its `SubmissionFile` rows (see §5).

#### File remove guard

- **Officer:** there is no officer-facing remove for a submitted exhibit; the prior-exhibits panel exposes none. (Existing DropZone pre-submit removal is unchanged.)
- **Admin remove endpoint:** add a guard — if the target file has a non-null `EnteredValue`, return **`409 Conflict`**, `"Entered exhibits cannot be removed."`. A Marked-only or Unclassified file may still be removed by an admin.

### 3. Business Logic Layer

Add to the submission/classification service (e.g. `ExhibitClassificationService` or `SubmissionService`):

```csharp
Task<SubmissionFile> MarkExhibitAsync(Guid fileId, string markedValue, string changedBy);
Task<SubmissionFile> EnterExhibitAsync(Guid fileId, string enteredValue, string changedBy);
Task<SubmissionFile> UpdateExhibitDescriptionAsync(Guid fileId, string description, string changedBy);
```

Each enforces the terminal-Entered lock (reading `EnteredAt` for the in-window correction), persists, and writes the audit row in the same unit of work. Retrieval reuses the existing `GetSubmissionsByFileNumberAsync`; **do not** add a parallel query. Controllers delegate entirely — no business rules in controllers. `changedBy` is intended to be the user's id/email; until Keycloak integration lands that claim is not reliably present, so pass the best available principal value (placeholder acceptable) and revisit when Keycloak ships.

### 4. CHUNK Drive `.txt` File Format

Per Open Question 2, the CHUNK `.txt` is written **only after an exhibit is Entered** — there is no early/stale write to reconcile. When written, each file block includes:

```
File: exhibit.mp4
...existing fields...
Description: Front-door bodycam, 09:12–09:40
Marked: A
Marked At: 2026-05-15 09:45:00 UTC
Entered: 3
Entered At: 2026-05-20 14:10:00 UTC
```

Unset fields are written as `—`.

### 5. Response Model Updates

Add the classification fields to the shared **`SubmissionFile`** (`CES.Business/Models/SubmissionReviewModel.cs`) so every consumer — submission review, listing, and the prior-exhibit/by-file-number retrieval — exposes them:

```csharp
public class SubmissionFile
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StorageProvider { get; set; } = string.Empty;
    public string Status { get; set; } = "Unclassified";   // was "Pending"
    public string? MarkedValue { get; set; }
    public DateTime? MarkedAt { get; set; }
    public string? EnteredValue { get; set; }
    public DateTime? EnteredAt { get; set; }
    public string? Description { get; set; }
}
```

- `Status` is derived from the values: `Entered` if `EnteredValue` set, else `Marked` if `MarkedValue` set, else `Unclassified`.
- Update the `StoredFiles → SubmissionFile` projection (in `SubmissionService`/`SubmissionExtensions`) to map all five new fields and compute `Status`.

---

## Admin Changes

### Submission Review (`SubmissionReview.vue`)

Each file row gains read-only **Status / Marked / Marked At / Entered / Entered At / Description** columns:

| File Name   | Status  | Marked | Marked At        | Entered | Entered At       | Description |
| ----------- | ------- | ------ | ---------------- | ------- | ---------------- | ----------- |
| exhibit.mp4 | Entered | A      | 2026-05-15 09:45 | 3       | 2026-05-20 14:10 | Bodycam     |
| footage.mov | Marked  | B      | 2026-05-15 10:30 | —       | —                | —           |

All classification fields are read-only in admin. The admin's existing remove action is gated by the §2 guard (allowed unless Entered).

---

## Resolved Open Questions

The earlier follow-up items are resolved as follows (carried from the prior draft's answers):

1. **Cross-session / cross-officer action:** Any authenticated user with access to the submission may perform any action. No per-officer scoping.
2. **CHUNK re-push on late Entering:** The `.txt` is written only after the exhibit is Entered; there is no earlier write to go stale.
3. **History lookup placement:** A hidden, non-intrusive popup opened from a badge/link on the Exhibit Upload screen.
4. **Audit storage:** A generic `SubmissionAuditLogs` table (Backend §1) records field-level changes; reusable beyond these three fields.
5. **Marked-only at submission close:** Out of scope here. Auto-accept on Marked is deferred until the officer flow is validated.
6. **Endpoint consolidation:** Resolved with multi-ticket Open Question 4 — reuse `GET /api/submissions/by-file-number`; drop the separate flat endpoint.

## Resolved This Revision

A. **Entered 10-second correction vs. terminal lock (Backend §2).** **Resolved — option (i).** The backend permits an overwrite of `EnteredValue` while `EnteredAt` is within `CLASSIFICATION_EDIT_WINDOW_SECONDS`, then locks permanently. The backend therefore reads `EnteredAt` on any mark/enter/description request to evaluate the lock. Marked and Description lock immediately on Enter; only the Entered value is correctable in its window. Reflected in Backend §2 and the state-machine window rules.

B. **Description size / formatting.** **Resolved — 250-char cap** (`DESCRIPTION_MAX_LENGTH`), plain text. The Description field shows a live counter of remaining characters; the backend validates the cap independently. Reflected in Constants, Frontend §1, and Backend §2.

C. **`changedBy` identity source.** **Resolved — user id/email**, but that claim is not reliably available until Keycloak integration lands. Until then, populate `ChangedBy` with the best available principal value (placeholder acceptable) and revisit when Keycloak ships. Reflected in the `SubmissionAuditLogs` table notes and Backend §3.

## Remaining Open Questions

None — all follow-ups for this spec are resolved. (Cross-feature dependency: meaningful `ChangedBy` values await the separate Keycloak integration.)

---

## Testing

Per the project testing rule, all new service methods, controller actions, store mutations, and service functions require tests; existing tests touched by these changes are updated, not skipped. Frameworks/structure follow [spec/testing-implementation.md](testing-implementation.md).

### Backend (xUnit)

**Unit — CES.Business.Tests**

| Test                                                       | Behaviour                                                                         |
| ---------------------------------------------------------- | --------------------------------------------------------------------------------- |
| `MarkExhibit_PersistsValueTimestampAndAudit`               | Marking an Unclassified file sets `MarkedValue` (uppercase) + `MarkedAt` and writes one `MarkedValue` audit row |
| `MarkExhibit_NormalisesLetterToUppercase`                  | `"a"` → `"A"`                                                                      |
| `MarkExhibit_Rejects_WhenAlreadyEntered`                   | File with `EnteredValue` set → `409`/terminal-lock error                           |
| `EnterExhibit_PersistsValueTimestampAndAudit`              | Entering sets `EnteredValue` + `EnteredAt`; writes audit row                       |
| `EnterExhibit_SucceedsOnMarkedFile`                        | Marked file → Enter succeeds; both values present                                 |
| `EnterExhibit_AllowsOverwrite_WithinWindow`                | Re-entering when `EnteredAt` is within `CLASSIFICATION_EDIT_WINDOW_SECONDS` overwrites the value, audits it, and does **not** advance `EnteredAt` |
| `EnterExhibit_Rejects_WhenWindowExpired`                   | Re-entering when `EnteredAt` is older than the window → `409` terminal lock        |
| `MarkExhibit_Rejects_WhenEntered_EvenWithinWindow`         | Marked is locked the instant the file is Entered, including inside the Entered window |
| `UpdateDescription_PersistsAndAudits`                      | Description saved; audit row with old/new                                          |
| `UpdateDescription_Rejects_WhenEntered`                    | Description change blocked once Entered (incl. within the Entered window)          |
| `UpdateDescription_Rejects_WhenOverMaxLength`              | Description longer than `DESCRIPTION_MAX_LENGTH` (250) → validation error          |
| `StatusProjection_DerivesUnclassifiedMarkedEntered`        | `Status` computed correctly from the value pair                                    |
| `AdminRemove_Rejects_WhenEntered`                          | Remove blocked for an Entered file; allowed for Marked/Unclassified               |
| `GetSubmissionsByFileNumber_IncludesClassificationFields`  | Reused retrieval projects Marked/Entered/Description on each `SubmissionFile`      |

**Integration — CES.API.Tests**

| Test                                                          | Expected                                              |
| ------------------------------------------------------------- | ----------------------------------------------------- |
| `POST /api/files/{id}/mark` valid → `200`                     | `markedValue` + `markedAt` in body; status `Marked`   |
| `POST /api/files/{id}/mark` already Entered → `409`           | Terminal-lock message                                 |
| `POST /api/files/{id}/mark` invalid letter (`"AA"`) → `400`   | Validation error                                      |
| `POST /api/files/{id}/enter` direct → `200`                   | `enteredValue` + `enteredAt`; `markedValue` null      |
| `POST /api/files/{id}/enter` after Mark → `200`               | Both values present; status `Entered`                 |
| `POST /api/files/{id}/enter` correction within window → `200` | New `enteredValue` accepted; `enteredAt` unchanged    |
| `POST /api/files/{id}/enter` after window expired → `409`     | Terminal-lock message                                 |
| `POST /api/files/{id}/enter` out of range (`"51"`) → `400`    | Validation error                                      |
| `PATCH /api/files/{id}/description` valid → `200`             | `description` updated                                 |
| `PATCH /api/files/{id}/description` over 250 chars → `400`     | Validation error                                      |
| `PATCH /api/files/{id}/description` when Entered → `409`       | Locked                                                |
| Admin remove Entered file → `409`                             | `"Entered exhibits cannot be removed."`               |
| Admin remove Marked file → `200/204`                          | Allowed                                               |
| Each mutation writes a `SubmissionAuditLogs` row              | Row present with correct `FieldName`/old/new/changedBy |
| All mutation endpoints unauthenticated → `401`               | Auth required                                          |

### Frontend (Vitest)

| Test                                                          | Behaviour                                                                  |
| ------------------------------------------------------------- | -------------------------------------------------------------------------- |
| `Status chip reflects Unclassified/Marked/Entered`            | Chip text matches the value pair                                           |
| `Marked dropdown enabled for unclassified, just-set, in window` | Enabled until 10s elapse                                                   |
| `Marked dropdown disabled after 10s window`                  | Disabled when timer elapses (fake timers)                                  |
| `Loaded prior exhibit with Marked value renders disabled`    | On-load values never re-open the window                                    |
| `Selecting Entered locks Marked and Description immediately`  | Both disabled the moment Entered is set                                    |
| `Entered row fully read-only after window`                   | All controls (except View) disabled                                       |
| `Description editable until Entered, then disabled`          | Reflects lock                                                              |
| `Description counter and 250-char cap`                       | Live remaining-characters counter updates; input cannot exceed 250         |
| `Entered correction within 10s re-sends enter`              | Changing the just-set Entered value within the window calls enter again     |
| `Mark/Enter/Description calls the correct endpoint+payload`  | Route id + body verified                                                   |
| `Green check shows on save and fades after 5s`               | Indicator appears then hides (fake timers)                                 |
| `Red X with tooltip shows on save failure`                   | Error message surfaced on hover                                            |
| `View shown only for browser-viewable content types`         | Hidden for non-viewable types; opens `/view`, never `/download`           |
| `No Remove control on prior exhibits`                        | Officers cannot remove submitted exhibits                                  |
| `History popup opens from badge and renders rows / empty`    | Reuses by-file-number; read-only                                          |
| `Submit no longer requires classification`                   | Pre-submit classification validation is gone                              |

**Existing tests to update:** `SubmissionServiceTests.cs`, `SubmissionsControllerTests.cs`, `FilesControllerTests.cs`, `SubmissionService.spec.ts`, `SubmissionForm.spec.ts` (add `markedValue`/`enteredValue`/`description`/`status` to mocked `SubmissionFile` models; the `Status` default changes from `"Pending"` to `"Unclassified"`).
