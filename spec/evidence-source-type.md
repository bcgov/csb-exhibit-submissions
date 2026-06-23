# Evidence Source Type — File Classification at Upload

**Status:** Draft  
**Date:** 2026-06-18  
**JIRA:** CES-18

---

## Overview

When an officer queues files for upload, each file should carry an **Evidence Source Type** identifying the recording device: `BodyCam`, `DashCam`, or `Other`. The classification is **optional at upload time** and can be set or corrected in the Prior Exhibits panel after submission — subject to the same terminal lock as `Description` in [exhibit-classification.md](exhibit-classification.md): once a file reaches **Entered** status, the Evidence Source Type is permanently locked.

The source type is saved per file and displayed read-only to admins in the Submission Review screen.

---

## User Stories

1. As an officer, when I queue files for upload I can select the Evidence Source Type (BodyCam / DashCam / Other) for each file so the source is captured at submission time.
2. As an officer, I can leave the Evidence Source Type blank at upload time and set it later from the Prior Exhibits panel, as long as the file has not been Entered.
3. As an officer, I can change an Evidence Source Type I previously set for a prior exhibit, as long as that exhibit has not been Entered.
4. As an officer, I cannot change the Evidence Source Type once an exhibit is Entered.
5. As an admin, I can see each file's Evidence Source Type in the Submission Review screen so I know the source of the evidence.

---

## Scope

| Area                                                                      | In Scope       |
| ------------------------------------------------------------------------- | -------------- |
| Officer — Queued file list (per-file source type dropdown at upload time) | Yes            |
| Officer — Prior Exhibits panel (editable until Entered)                   | Yes            |
| Backend — `StoredFiles` schema (`EvidenceSourceType` column)              | Yes            |
| Backend — Submit endpoint (source type in payload)                        | Yes            |
| Backend — `PATCH /api/files/{fileId}/source-type` endpoint                | Yes            |
| Backend — Audit log writes on source type change (post-submit)            | Yes            |
| Admin — Submission Review (read-only display per file)                    | Yes            |
| Admin — Submission Listing (no change)                                    | No             |
| Exhibit History popup (add Evidence Type column)                          | Yes            |
| Terminal lock enforcement (locked once Entered)                           | Yes            |
| Email notifications / file storage                                        | No — no change |

---

## Decisions Made

| Decision                                | Choice                                                                             | Rationale                                                                                                                                                                        |
| --------------------------------------- | ---------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Classification required at upload       | **Optional** — officer may skip                                                    | Confirmed with product owner; can be set later in Prior Exhibits panel                                                                                                           |
| Post-submit editability                 | Editable in Prior Exhibits panel until file is Entered                             | Mirrors the `Description` field's lock rule from exhibit-classification.md                                                                                                       |
| Lock trigger                            | `EnteredValue` set (status = Entered)                                              | Consistent with existing terminal lock; source type locks the instant the file is Entered                                                                                        |
| Editing within Entered 10-second window | Source Type locks immediately on Enter                                             | Same rule as `Description` — only the `EnteredValue` itself is correctable within the Entered window                                                                             |
| Allowed values                          | `BodyCam`, `DashCam`, `Other`                                                      | As specified; defined as constants, no free-text field                                                                                                                           |
| Value storage                           | Stored as the string value of the option (e.g. `"BodyCam"`)                        | Human-readable in DB; validated against the constant list                                                                                                                        |
| Column nullability                      | Nullable — null means "not set"                                                    | Officer may skip at upload; backward-compatible for existing rows                                                                                                                |
| Submit payload shape                    | Parallel form-field array `fileSourceTypes` in the same order as `files`           | Consistent with how multipart form data is submitted; simple ASP.NET model binding                                                                                               |
| `FileDropZone.vue` refactor             | File list rendering moves from `FileDropZone` to `SubmissionForm.vue`              | The dropzone component becomes a pure drop/browse zone; `SubmissionForm` owns the enriched file list with classification dropdowns. Required to allow per-file UI in the parent. |
| Audit log on post-submit changes        | Write a `SubmissionAuditLog` row on each source-type change via the PATCH endpoint | Consistent with how Marked/Entered/Description changes are tracked; no audit at initial submit (same convention as initial Description value)                                    |

---

## Constants

Per the project code-style rule, no magic strings inline.

| Constant                | Value                                      | Location                                            | Notes                                               |
| ----------------------- | ------------------------------------------ | --------------------------------------------------- | --------------------------------------------------- |
| `EVIDENCE_SOURCE_TYPES` | `['BodyCam', 'DashCam', 'Other'] as const` | `web/src/constants/classification.ts`               | Valid options for the Evidence Source Type dropdown |
| `EvidenceSourceType`    | TypeScript type derived from above         | `web/src/constants/classification.ts`               | `'BodyCam' \| 'DashCam' \| 'Other'`                 |
| `EvidenceSourceTypes`   | `{ "BodyCam", "DashCam", "Other" }`        | `CES.Business/Constants/ClassificationConstants.cs` | Backend validation list                             |

---

## Frontend Changes

### 1. `FileDropZone.vue` — Refactor (prerequisite)

Extract the file list (`<ul class="file-list">`) from `FileDropZone.vue`. After this change:

- `FileDropZone.vue` renders only the drop-target zone and the hidden file `<input>`.
- `FileDropZone.vue` continues to emit `filesChanged(files: File[])` when files are added via drop or browse.
- Add a new emit: `removeFile(index: number)` so the parent can remove a file and splice its own parallel state arrays at the same index.
- `reset()` (already exposed via `defineExpose`) is retained; it clears the hidden `<input>` value, allowing the same files to be re-selected after a submit.

> **Impact on `FileDropZone.spec.ts`:** Update the existing test that asserts the file list is rendered — that responsibility has moved to `SubmissionForm`. Tests for drop/browse/remove-emit behaviour remain.

### 2. `SubmissionForm.vue` — Queued File List with Classification

After `<FileDropZone>`, `SubmissionForm.vue` renders the queued-file list. The parent owns two parallel reactive arrays:

```typescript
const files = ref<File[]>([]);
const fileSourceTypes = ref<string[]>([]); // '' means unset; EVIDENCE_SOURCE_TYPES[n] when set
```

**State synchronisation:**

- `@filesChanged(newFiles)` — diff against `files.value` to find newly added files, append `''` entries to `fileSourceTypes` for each. Update `files.value` to `newFiles`.
- `@removeFile(index)` — splice both `files.value` and `fileSourceTypes.value` at `index`.

**Queued file list row (per file):**

| Element                       | Details                                                                                                                                     |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| File icon                     | Reuse `getFileIcon(file)`                                                                                                                   |
| File name                     | `file.name`                                                                                                                                 |
| Evidence Source Type dropdown | `<select v-model="fileSourceTypes[i]">` with `<option value="">— Select type —</option>` + one `<option>` per `EVIDENCE_SOURCE_TYPES` entry |
| Remove button                 | Emits `removeFile(i)` or handled inline; splices both arrays                                                                                |

The submit button's enabled state is **unchanged** — this feature adds no "source type required" gate.

### 3. `SubmissionForm.vue` — Prior Exhibits Panel

Each prior-file row gains an **Evidence Source Type** dropdown alongside the existing Marked / Entered / Description controls.

| Control                  | Type   | Enabled when                                      |
| ------------------------ | ------ | ------------------------------------------------- |
| **Source Type** dropdown | Select | `enteredValue` is null (same rule as Description) |

- On change: immediately calls `PATCH /api/files/{fileId}/source-type`.
- On success: updates the file in the local store; shows the green ✓ save indicator via the existing `showSaveSuccess` / `showSaveError` mechanism.
- **Entering an exhibit locks Source Type immediately** — the Source Type dropdown disables the moment `enteredValue` is set, even within the 10-second Entered correction window.
- On screen load, prior exhibits already Entered have the dropdown disabled.

Add handler to `SubmissionForm.vue`:

```typescript
const onSourceTypeChange = async (file: SubmissionFile, value: string) => {
  try {
    const updated = await updateExhibitSourceType(file.id, {
      evidenceSourceType: value,
    });
    updateFileInStore(updated);
    showSaveSuccess(file.id);
  } catch (err: unknown) {
    const msg =
      err instanceof Error ? err.message : "Failed to save source type.";
    showSaveError(file.id, msg);
  }
};
```

The `isSourceTypeEnabled` guard mirrors `isDescriptionEnabled`:

```typescript
const isSourceTypeEnabled = (file: SubmissionFile): boolean =>
  file.enteredValue == null;
```

### 4. Exhibit History Popup

Add an **Evidence Type** column to the history table:

| File Name   | Submission Date | Status  | Evidence Type | Marked | Marked At        | Entered | Entered At       | Description |
| ----------- | --------------- | ------- | ------------- | ------ | ---------------- | ------- | ---------------- | ----------- |
| bodycam.mp4 | 2026-05-15      | Entered | BodyCam       | A      | 2026-05-15 09:45 | 3       | 2026-05-20 14:10 | Front door  |
| dash.mp4    | 2026-05-15      | Marked  | DashCam       | B      | 2026-05-15 10:00 | —       | —                | —           |

Display `file.evidenceSourceType ?? '—'` for each row.

### 5. TypeScript Model Updates

#### `web/src/models/SubmissionReviewModel.ts`

```typescript
export interface SubmissionFile {
  id: string;
  originalFileName: string;
  storedFileName: string;
  viewUrl: string;
  downloadUrl: string;
  contentType: string;
  fileSize: number;
  storageProvider: string;
  status?: string;
  markedValue?: string | null;
  markedAt?: string | null;
  enteredValue?: string | null;
  enteredAt?: string | null;
  description?: string | null;
  evidenceSourceType?: string | null; // new
}

// Add new request model
export interface ExhibitSourceTypeModel {
  evidenceSourceType: string;
}
```

#### `web/src/constants/classification.ts`

```typescript
export const EVIDENCE_SOURCE_TYPES = ["BodyCam", "DashCam", "Other"] as const;
export type EvidenceSourceType = (typeof EVIDENCE_SOURCE_TYPES)[number];
```

#### `web/src/services/SubmissionService.ts`

Add new service method:

```typescript
const updateExhibitSourceType = async (
  fileId: string,
  model: ExhibitSourceTypeModel,
): Promise<SubmissionFile> => {
  const result = await api.patch<SubmissionFile>(
    `/files/${fileId}/source-type`,
    model,
  );
  return result.data;
};
```

Update `submitExhibits` to include source types in the form data alongside files:

```typescript
files.forEach((file, i) => {
  formData.append("files", file);
  formData.append("fileSourceTypes", fileSourceTypes[i] ?? "");
});
```

> **Note:** The `fileSourceTypes` parameter must be passed into `submitExhibits` from `SubmissionForm.vue`. Update the function signature to `submitExhibits(model, files, fileSourceTypes, progressCallback?)`.

---

## Backend Changes

### 1. Database Schema

#### Modified table: `StoredFiles`

Add one nullable column:

| Column               | Type          | Nullable | Notes                                                       |
| -------------------- | ------------- | -------- | ----------------------------------------------------------- |
| `EvidenceSourceType` | `varchar(50)` | Yes      | One of `"BodyCam"`, `"DashCam"`, `"Other"`; null if not set |

```csharp
public class StoredFiles
{
    // ... existing properties ...
    public string? EvidenceSourceType { get; set; }  // new — added for evidence source type feature
}
```

**Migration:** One EF Core migration adds the nullable column with no default. Existing rows remain null (treated as "not set" in the UI).

### 2. API Endpoints

#### `POST /api/submissions/submit` (modified)

The `multipart/form-data` payload gains an order-matched `fileSourceTypes` collection:

```
files=<binary1>
files=<binary2>
fileSourceTypes=BodyCam
fileSourceTypes=
```

- `fileSourceTypes[i]` corresponds to `files[i]` by position.
- Empty string or omitted = null (not set). No error for unset.
- If provided and non-empty, must be one of `ClassificationConstants.EvidenceSourceTypes` → `400` otherwise.
- If the collection is shorter than `files`, remaining files default to null.

`SubmissionModel` gains:

```csharp
public List<string?> FileSourceTypes { get; set; } = new();
```

The mapping in `SubmissionService.SubmitEvidence` sets `storedFile.EvidenceSourceType` from `model.FileSourceTypes[i]` (if the index exists and the value is non-empty). This is applied in the per-file loop that already creates `StoredFiles` entities.

#### `PATCH /api/files/{fileId}/source-type` (new)

Request body:

```json
{ "evidenceSourceType": "DashCam" }
```

Rules:

- `evidenceSourceType` must be one of `ClassificationConstants.EvidenceSourceTypes`, or empty string to clear the value → `400` otherwise.
- If `file.EnteredValue` is set → **`409 Conflict`**, `"Entered exhibits cannot be modified."` (Source Type locks the instant the file is Entered, including within the Entered correction window — only the `EnteredValue` itself is correctable in that window, not Source Type.)
- On success: persist `EvidenceSourceType` (null if empty); write one `SubmissionAuditLog` row (`FieldName = "EvidenceSourceType"`, `OldValue`, `NewValue`).
- `200 OK` with the updated `SubmissionFile`; `404` if the file is not found.

Add to `FilesController`:

```csharp
[HttpPatch]
[Route("api/files/{fileId:guid}/source-type")]
[Authorize(Roles = "User")]
public async Task<IActionResult> UpdateSourceType(Guid fileId, [FromBody] ExhibitSourceTypeModel model)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    var changedBy = User.FindFirstValue(ClaimTypes.UserData) ?? "Officer";
    var result = await _fileService.UpdateExhibitSourceTypeAsync(fileId, model.EvidenceSourceType, changedBy);
    return Ok(result);
}
```

### 3. Business Logic Layer

Add to `IFileService` / `FileService`:

```csharp
Task<SubmissionFile> UpdateExhibitSourceTypeAsync(Guid fileId, string? evidenceSourceType, string changedBy);
```

Enforcement:

- Load `StoredFiles` by `fileId`; return `404` equivalent if not found.
- If `file.EnteredValue != null` → throw a domain exception that maps to `409` (same pattern as `UpdateExhibitDescriptionAsync`).
- If `evidenceSourceType` is non-null and non-empty and not in `ClassificationConstants.EvidenceSourceTypes` → throw a validation exception that maps to `400`.
- Set `file.EvidenceSourceType` (null when empty string supplied).
- Write `SubmissionAuditLog` (`FieldName = "EvidenceSourceType"`, `OldValue = previous`, `NewValue = new`).
- Save and return the updated `SubmissionFile`.

### 4. Request Model (C#)

Add `ExhibitSourceTypeModel` to `CES.API/Models/` and `CES.Business/Models/`:

```csharp
public class ExhibitSourceTypeModel
{
    public string? EvidenceSourceType { get; set; }
}
```

### 5. Response Model Updates

Add `EvidenceSourceType` to `SubmissionFile` in `CES.Business/Models/SubmissionReviewModel.cs`:

```csharp
public class SubmissionFile
{
    // ... existing properties ...
    public string? EvidenceSourceType { get; set; }  // new
}
```

Update the `StoredFiles → SubmissionFile` projection in `SubmissionExtensions` / `StoredFilesExtensions` to map `EvidenceSourceType`.

### 6. Backend Constants

Add to `CES.Business/Constants/ClassificationConstants.cs`:

```csharp
public static readonly string[] EvidenceSourceTypes = { "BodyCam", "DashCam", "Other" };
```

---

## Admin Changes

### `SubmissionReview.vue`

Each file row in the **Submitted Evidence** section gains a read-only **Evidence Type** badge in the `classification-info` div:

```html
<span class="cl-field">{{ file.evidenceSourceType ?? '—' }}</span>
```

Place it as the first item in `classification-info`, before the status chip, so it is immediately visible.

No changes to `SubmissionListing.vue`.

---

## Testing

Per the project testing rule, all new service methods, controller actions, store mutations, and service functions require tests; existing tests touched by these changes are updated, not skipped. Frameworks follow [spec/testing-implementation.md](testing-implementation.md).

### Backend (xUnit)

**Unit — CES.Business.Tests**

| Test                                                           | Behaviour                                                                                              |
| -------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `SubmitEvidence_PersistsEvidenceSourceType`                    | Source type from `fileSourceTypes[i]` is saved to `StoredFiles[i].EvidenceSourceType`                  |
| `SubmitEvidence_DefaultsToNull_WhenSourceTypeOmitted`          | File with no matching `fileSourceTypes` entry gets `null`                                              |
| `SubmitEvidence_DefaultsToNull_WhenSourceTypeEmpty`            | Empty string entry → null persisted                                                                    |
| `UpdateSourceType_PersistsAndAudits`                           | Source type saved; `SubmissionAuditLog` row written with correct `FieldName`/old/new                   |
| `UpdateSourceType_Rejects_WhenEntered`                         | `409` when `EnteredValue` is set                                                                       |
| `UpdateSourceType_Rejects_WhenEntered_EvenWithinEnteredWindow` | Still `409` when `EnteredAt` is within `CLASSIFICATION_EDIT_WINDOW_SECONDS` — same lock as Description |
| `UpdateSourceType_Rejects_InvalidValue`                        | `400` for a value not in `EvidenceSourceTypes`                                                         |
| `UpdateSourceType_AllowsEmpty_ToUnset`                         | Empty string → `null` persisted; audit row written                                                     |
| `UpdateSourceType_Succeeds_ForMarkedFile`                      | Marked-but-not-Entered file → source type updated successfully                                         |
| `SubmissionFile_Projection_IncludesEvidenceSourceType`         | `StoredFiles → SubmissionFile` correctly maps `EvidenceSourceType`                                     |

**Integration — CES.API.Tests**

| Test                                                                  | Expected                                                                       |
| --------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| `POST /api/submissions/submit` with `fileSourceTypes=BodyCam` → `200` | Retrieved file has `evidenceSourceType = "BodyCam"`                            |
| `POST /api/submissions/submit` with invalid source type → `400`       | Validation error                                                               |
| `POST /api/submissions/submit` with no `fileSourceTypes` → `200`      | Files stored with `evidenceSourceType = null`                                  |
| `PATCH /api/files/{id}/source-type` valid → `200`                     | `evidenceSourceType` updated in response body                                  |
| `PATCH /api/files/{id}/source-type` when Entered → `409`              | Terminal-lock message                                                          |
| `PATCH /api/files/{id}/source-type` invalid value → `400`             | Validation error                                                               |
| `PATCH /api/files/{id}/source-type` empty string → `200`              | `evidenceSourceType = null` in response                                        |
| `PATCH /api/files/{id}/source-type` unauthenticated → `401`           | Auth required                                                                  |
| `GET /api/submissions/retrieve` returns `evidenceSourceType` per file | Field present on each `SubmissionFile`                                         |
| Each `PATCH` source-type change writes one `SubmissionAuditLogs` row  | Row present with `FieldName = "EvidenceSourceType"` and correct old/new values |

### Frontend (Vitest)

| Test                                                          | Behaviour                                                                                       |
| ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `FileDropZone no longer renders file list`                    | File list `<ul>` is absent from the component; component emits `removeFile(index)`              |
| `Source type dropdown renders per queued file`                | Each file row in `SubmissionForm` shows a dropdown with blank + `EVIDENCE_SOURCE_TYPES` options |
| `fileSourceTypes array is parallel to files array — add`      | Adding a file appends a `''` entry to `fileSourceTypes`                                         |
| `fileSourceTypes array is parallel to files array — remove`   | Removing file at index `i` splices both `files` and `fileSourceTypes` at index `i`              |
| `Submit includes fileSourceTypes in form data`                | `FormData` contains `fileSourceTypes` entries in the same order as `files`                      |
| `Prior exhibit source type dropdown enabled when not Entered` | Dropdown is interactive for Unclassified/Marked files                                           |
| `Prior exhibit source type dropdown disabled when Entered`    | `disabled` when `enteredValue` is set                                                           |
| `Entering an exhibit locks Source Type immediately`           | Source Type dropdown disables the moment Entered is set — not after the 10s window              |
| `onSourceTypeChange calls correct endpoint and payload`       | `PATCH /api/files/{id}/source-type` called with `{ evidenceSourceType: "DashCam" }`             |
| `Green check on save, red X on failure`                       | Same save-indicator behaviour as Description/Mark/Enter                                         |
| `History popup shows Evidence Type column`                    | Column present; shows value or `—` for each file row                                            |
| `Admin review shows evidenceSourceType per file`              | `cl-field` with value or `—` rendered in classification-info                                    |

**Existing tests to update:**

- `FileDropZone.spec.ts` — update to reflect that file list is no longer rendered by the component; add assertion for `removeFile` emit.
- `SubmissionForm.spec.ts` — add `evidenceSourceType` to mocked `SubmissionFile` models; add tests for the queued-file source type dropdown and the prior-exhibit source type controls.
- `SubmissionService.spec.ts` — add `evidenceSourceType` field to `SubmissionFile` mocks; add handler for `PATCH .../source-type`.
- `SubmissionServiceTests.cs` / `FilesControllerTests.cs` — add `EvidenceSourceType` to `StoredFiles` / `SubmissionFile` mocks.

---

## Open Questions

None — all design decisions are resolved. Optional-at-upload / editable-until-Entered / locked-at-Entered behaviour confirmed by product owner.
