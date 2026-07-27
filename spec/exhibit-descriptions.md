# Exhibit Descriptions — Append-Only Entries + Streamlined Exhibit List

**Ticket:** CES-42
**Status:** Draft — for review
**Depends on:** [exhibit-classification.md](completed/exhibit-classification.md), [exhibit-search.md](exhibit-search.md) (Exhibit Detail Popup & Registry Notes), [accepted-file-storage.md](completed/accepted-file-storage.md)

---

## Overview

Today an exhibit has a **single, mutable `Description` string** (`StoredFiles.Description`, max 250 chars, edited inline in `ExhibitList.vue`, overwritten on each save, with before/after rows landing in `SubmissionAuditLog`).

This spec replaces it with an **append-only, immutable description-entry model**, structurally identical to the existing registry `ExhibitNote` (many entries → one exhibit):

- A description entry, once saved, is never edited or deleted.
- Correcting or expanding a description means **adding a new entry**; the earlier entries remain as history.
- The full description history is shown in the Exhibit Detail modal and exported in `metadata.json`.
- Entries are **multiline plain text** with whitespace preserved (no markdown/HTML).

It also **streamlines `ExhibitList.vue`** into a collapsed single-row form to reclaim screen space (chiefly for admin Exhibit Search), with the classification controls moved behind a chevron, and **opens the Exhibit Detail modal to officers** (minus the registry Notes section).

The project is not live. **Existing `Description` data and its audit rows are dropped** — no data migration is required.

---

## Terminology

| Term | Meaning |
|---|---|
| **Description entry** | One immutable row in the new `ExhibitDescriptions` table. |
| **First description** | The chronologically earliest entry for an exhibit — the only one addable from the exhibit list. |
| **Addendum** | Any entry after the first. Addable only from the Exhibit Detail modal. |
| **Condensed row** | `ExhibitList` row with row 2 collapsed (default). |
| **Expanded row** | `ExhibitList` row with row 2 (Marked/Entered/Source) visible. |

---

## Decisions

1. **Append-only, immutable.** No `PATCH`/`PUT`/`DELETE` on a description entry. Same contract as `ExhibitNote`.
2. **Locked once Entered (officers).** An officer cannot add a description entry to an exhibit that has an `EnteredValue`; the API throws `InvalidOperationException` → `409`. **Admins may always append** (`isAdminOverride`, the same flag `MarkExhibitAsync`/`EnterExhibitAsync` already take). This preserves the current classification lock rather than loosening it.
3. **Max 1000 characters per entry** (`ClassificationConstants.DescriptionMaxLength = 1000`, up from 250). Unlimited entry count.
4. **Plain text, whitespace preserved.** Line breaks and indentation survive round-trip. No markup is parsed or rendered. Newlines normalized to `\n` on save; leading/trailing whitespace of the whole entry is trimmed; interior whitespace untouched.
5. **Empty entries rejected.** Whitespace-only text → `ArgumentException` → `400`.
6. **Descriptions leave the audit log.** `SubmissionAuditLog` no longer records `FieldName = "Description"` — the entry list *is* the description's history. The change-history table (modal + list popup) therefore shows only Marked / Entered / Source. Legacy `Description` audit rows are deleted by the migration.
7. **Descriptions are embedded in `SubmissionFile`.** Every payload that already carries a `SubmissionFile` (submission review, prior-file lookup, exhibit search) carries its ordered `descriptions[]`. No separate `GET` endpoint is added — unlike notes, the list view needs the first entry inline to render a condensed row, so a per-file fetch would be an N+1.
8. **Officers get the Exhibit Detail modal, without Notes.** The modal moves to `components/shared/` and gains a `canViewNotes` prop. Notes remain **Admin-only at the API level** (`/api/files/{id}/notes` stays `[Authorize(Roles = "Admin")]`); the prop only controls whether the section renders and is fetched.
9. **Metadata schema version bumps to 2.** `AcceptedMetadataExhibit.description` (string) becomes `descriptions` (array of `{ text, by, atUTC }`).

---

## Data model

### New entity — `api/CES.Entities/Entities/ExhibitDescription.cs`

Mirrors `ExhibitNote` exactly, minus the registry-only semantics (descriptions are visible to officers).

```csharp
public class ExhibitDescription
{
    public int Id { get; set; }
    public Guid FileId { get; set; }
    public StoredFiles File { get; set; } = null!;
    public string DescriptionText { get; set; } = null!;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAtUTC { get; set; } = SystemDate.UtcNow();
}
```

### Changed — `api/CES.Entities/Entities/StoredFiles.cs`

- **Remove** `public string? Description { get; set; }`.
- **Add** the navigation collection: `public ICollection<ExhibitDescription> Descriptions { get; set; } = new List<ExhibitDescription>();`
  (`ExhibitNote` has no reverse nav today — the description collection needs one because `AcceptedMetadataWriter` builds the sidecar from the loaded `Submission` graph.)

### EF — `CES.EF`

- `CESDataStore` / `ICESDataStore`: add `DbSet<ExhibitDescription> ExhibitDescriptions`.
- `ModelRelationships.BindRelationships`: `ExhibitDescription` → `StoredFiles` via `HasMany(f => f.Descriptions).WithOne(d => d.File).HasForeignKey(d => d.FileId).OnDelete(DeleteBehavior.Cascade)`.
- Migration `ExhibitDescriptionEntries`:
  1. `CreateTable ExhibitDescriptions` (index on `FileId`, and on `(FileId, CreatedAtUTC)` for ordered reads).
  2. `DropColumn StoredFiles.Description`.
  3. `migrationBuilder.Sql("DELETE FROM \"SubmissionAuditLogs\" WHERE \"FieldName\" = 'Description';")` — the field no longer exists, so its history rows must not linger in the change-history UI.

---

## Backend (`/api`)

### Constants

`CES.Business/Constants/ClassificationConstants.cs`
- `DescriptionMaxLength` 250 → **1000**.

### Models — `CES.Business/Models/ExhibitClassificationModels.cs`

- **Remove** `ExhibitDescriptionModel { string Description }` (the old PATCH body).
- **Add**:
  ```csharp
  // One immutable description entry. Read model returned to the client.
  public class ExhibitDescriptionEntryModel
  {
      public int Id { get; set; }
      public string DescriptionText { get; set; } = string.Empty;
      public string? CreatedBy { get; set; }
      public DateTime CreatedAtUTC { get; set; }
  }

  // Request body for appending a description entry.
  public class AddExhibitDescriptionModel
  {
      public string DescriptionText { get; set; } = string.Empty;
  }
  ```

`CES.Business/Models/SubmissionReviewModel.cs` → `SubmissionFile`:
- **Remove** `public string? Description { get; set; }`.
- **Add** `public List<ExhibitDescriptionEntryModel> Descriptions { get; set; } = new();` — ordered oldest → newest.

### Projection — `CES.Business/Extensions/Entities/StoredFilesExtensions.cs`

`ToSubmissionFile` maps `f.Descriptions.OrderBy(d => d.CreatedAtUTC)` into `Descriptions`. Every call site that projects a `StoredFiles` must therefore `.Include(f => f.Descriptions)`:
- `SubmissionService.RetrieveSubmission`
- `SubmissionService.RetrieveSubmissionListing`
- `SubmissionService.GetSubmissionsByFileNumberAsync`
- `SubmissionService.SearchExhibitsAsync`
- `FileService.LoadFileWithSubmissionAsync` (both `.Include(f => f.Descriptions)` on the file itself and `.ThenInclude(f => f.Descriptions)` on `Submission.Files`, so the metadata writer sees them)

### Service — `CES.Business/Services/FileService.cs` (+ `IFileService`)

**Remove** `UpdateExhibitDescriptionAsync`. **Add**:

```csharp
Task<SubmissionFile> AddExhibitDescriptionAsync(Guid fileId, string descriptionText, string createdBy, bool isAdminOverride = false);
```

Behaviour:
1. Load the file with its submission graph (`LoadFileWithSubmissionAsync`); missing → `KeyNotFoundException` (404).
2. `if (!isAdminOverride && file.EnteredValue != null)` → `InvalidOperationException("Entered exhibits cannot be modified.")` (409). *(Decision 2.)*
3. Normalize: `text.Replace("\r\n", "\n").Replace("\r", "\n").Trim()`.
4. Empty → `ArgumentException("Description text is required.")` (400). Over `DescriptionMaxLength` → `ArgumentException` (400).
5. Insert an `ExhibitDescription`; `file.SetUpdateBy(createdBy)`.
6. **No `SubmissionAuditLog` row** *(Decision 6)*.
7. `await FinalizeClassificationAsync(file, autoAccept: false)` — persists, and refreshes `metadata.json` when the file is already accepted. Adding a description never triggers acceptance on its own (unchanged from today).
8. Return the updated `SubmissionFile` (with the new `descriptions[]`), matching `MarkExhibitAsync`/`EnterExhibitAsync` so the frontend's `fileUpdated` flow is unchanged.

> Returning `SubmissionFile` (not the bare entry) is deliberate: `ExhibitList` re-renders the whole row from the emitted file, so the entry alone would leave the row stale.

### Controller — `CES.API/Controllers/FilesController.cs`

Replace the `PATCH /api/files/{fileId:guid}/description` action with:

```
POST /api/files/{fileId:guid}/descriptions      [Authorize(Roles = "User,Admin")]
Body: { "descriptionText": "…" }                → 200 SubmissionFile
```

Same `isAdmin` / `changedBy` claim resolution as the existing actions. No `GET` (Decision 7), no update/delete (Decision 1).

### Metadata export — `CES.Business/FileStorage/AcceptedMetadataWriter.cs` + `Models/AcceptedMetadata.cs`

- `AcceptedStorageConstants.MetadataSchemaVersion` 1 → **2**.
- `AcceptedMetadataExhibit.Description` (string?) → `public List<AcceptedMetadataDescription> Descriptions { get; set; } = new();`
- New: `public class AcceptedMetadataDescription { public string Text; public string? By; public DateTime AtUTC; }`
- `BuildMetadata` maps `f.Descriptions.OrderBy(d => d.CreatedAtUTC)`.
- `Revisions` no longer contain `Description …` lines (they are gone from the audit log); the per-exhibit `descriptions[]` array carries that history instead. *(Requirement: "description history is included with exported metadata file.")*

---

## Frontend (`/web`)

### Models

`models/ExhibitDescriptionModel.ts` (new):
```ts
// One immutable description entry (CES-42). Append-only: never edited or deleted.
export interface ExhibitDescriptionModel {
  id: number;
  descriptionText: string;
  createdBy?: string | null;
  createdAtUTC: string;
}
```

`models/SubmissionReviewModel.ts`:
- `SubmissionFile.description?: string | null` → **`descriptions: ExhibitDescriptionModel[]`** (oldest → newest).
- Remove `ExhibitDescriptionModel { description: string }` (the old request body type).

### Service — `services/SubmissionService.ts`

Replace `updateExhibitDescription(fileId, { description })` with:
```ts
const addExhibitDescription = async (fileId: string, descriptionText: string): Promise<SubmissionFile> =>
  (await api.post<SubmissionFile>(`/files/${fileId}/descriptions`, { descriptionText })).data;
```

### Constants — `constants/classification.ts`

```ts
// Mirrors backend ClassificationConstants.DescriptionMaxLength.
export const DESCRIPTION_MAX_LENGTH = 1000;
// Characters of the first description shown inline in the exhibit list before ellipsis.
export const DESCRIPTION_PREVIEW_MAX_LENGTH = 200;
// Rows the description textarea starts at; it auto-grows to this many rows before scrolling.
export const DESCRIPTION_INPUT_MIN_ROWS = 1;
export const DESCRIPTION_INPUT_MAX_ROWS = 8;
```

---

## Component: `shared/ExhibitList.vue` (streamlined)

### Props / emits

| Change | Detail |
|---|---|
| **Replace** `descriptionFn` | `addDescriptionFn: (fileId: string, text: string) => Promise<SubmissionFile>` |
| **Add** `initialExpanded?: boolean` | Row-2 default state. `false` (condensed) in `ExhibitSearch`; `true` in `SubmissionReview` and `SubmissionForm`, where the user is actively classifying. |
| Unchanged | `entries`, `markFn`, `enterFn`, `evidenceSourceFn`, `alwaysEditable`, `showRemoved`, `canDownload`, `canRemove`, `linkableTitle`, and all emits. |

`linkableTitle` is now passed by **`SubmissionForm`** too, so officers can open the detail modal from the filename.

### Condensed row (default)

```
[▸] [🕑] filename.mp4   2026-07-14 09:15   File #12345   [Marked A]   Lorem ipsum description text…   [✓] [View] [Download]
```

- New leading **chevron button** (`▸` / `▾`, `aria-expanded`, `aria-controls` → row 2). Toggles per-exhibit; state is local (`reactive<Set<string>>`), not persisted.
- Row 2 is not rendered when collapsed.
- Vertical padding/gap of the item tightens in the condensed state (`.prior-file-item--condensed`) — target ~1 line-height per exhibit.
- **Description cell** (right of the status chip, before the save indicator / actions):
  - **Has ≥1 entry:** the *first* entry, whitespace collapsed to single spaces, truncated to `DESCRIPTION_PREVIEW_MAX_LENGTH` with an ellipsis, on one line (`text-overflow: ellipsis`). Not editable. `title` attribute carries the full first entry. A `+N` chip appears when there are addenda; clicking the filename opens the detail modal for the whole history.
  - **No entries:** an inline **auto-growing textarea** (`rows=1`, grows to `DESCRIPTION_INPUT_MAX_ROWS`) — the only place the first description can be added from the list. Saves on blur (matching today's description UX) when non-empty; shows the existing ✓ / ✕ save indicator. Disabled when `isDescriptionEnabled()` is false.
  - **Removed exhibits:** no cell, no input (as today).

### Expanded row

Row 1 as above (chevron now `▾`); row 2 is today's control strip — **Marked**, **Entered**, **Source** — plus the description block, which moves here and renders full-width:
- **Has ≥1 entry:** read-only, `white-space: pre-wrap` render of the **first** entry (full text, wrapped), with `+N more — open details` when addenda exist.
- **No entries:** the same auto-growing textarea, now with room for several lines and the `n remaining` counter.

The description cell/block is one sub-component-shaped block inside `ExhibitList` with a `compact` class variant, so the two states share logic and only differ in layout.

### Enablement (`isDescriptionEnabled`)

Unchanged rule, new meaning — *may append*:
```ts
const isDescriptionEnabled = (file) => !!props.alwaysEditable || file.enteredValue == null;
```
Officers lose the input once the exhibit is Entered (Decision 2); admin (`alwaysEditable`) keeps it.

### History labels

`HISTORY_FIELD_LABELS` drops the `Description` key in both `ExhibitList.vue` and the detail modal — the audit log no longer emits it.

---

## Component: `shared/ExhibitDetailModal.vue` (moved from `admin/`, extended)

### Props

```ts
{
  result: ExhibitSearchResultModel;   // unchanged shape
  canViewNotes?: boolean;             // default false — Notes section renders/fetches only when true
  addDescriptionFn?: (fileId: string, text: string) => Promise<SubmissionFile>;  // omit → read-only
}
```
Emits `close` (unchanged) and **`fileUpdated: [SubmissionFile]`** so the parent list/search results stay in sync after an addendum.

### Sections

1. **Submission** — unchanged.
2. **Exhibit** — the `Description` `<dd>` is **removed** from the detail grid.
3. **Descriptions** (new, sits where the old field was — *above* Notes):
   - Same visual pattern as Notes, **without** the "Registry use only" badge.
   - Ordered list of entries, oldest → newest: `descriptionText` rendered with `white-space: pre-wrap` (Decision 4), then a meta line `{createdBy ?? '—'} · {formatDateTime(createdAtUTC, true)}`.
   - **Add entry** textarea (multiline, `DESCRIPTION_MAX_LENGTH` counter, "Save description" button, "saved permanently and cannot be edited" placeholder). This is the **only** place an addendum can be added.
   - Hidden/disabled when `addDescriptionFn` is absent or the exhibit is Entered and the caller is an officer (the API is the authority; the UI mirrors it via the same `enteredValue` check).
   - Entries come from `props.result.file.descriptions` — no fetch. After a successful save, the returned `SubmissionFile` replaces the local file and is emitted as `fileUpdated`.
4. **Notes** — `v-if="canViewNotes"`. The `getExhibitNotes` call moves inside that guard so an officer never fires an Admin-only request.
5. **Change History** — unchanged (now Marked/Entered/Source only).
6. **Metadata** — unchanged.

### Callers

| Caller | `canViewNotes` | `addDescriptionFn` | Notes |
|---|---|---|---|
| `admin/ExhibitSearch.vue` | `true` | provided | Import path updates to `../shared/ExhibitDetailModal.vue`. |
| `officer/SubmissionForm.vue` | `false` (omit) | provided | **New.** `linkableTitle` on `ExhibitList` → `@title-click` opens the modal. |

`SubmissionForm` must build the `ExhibitSearchResultModel` shape the modal expects. `flatPriorFiles` currently drops the fields the modal needs, so it should carry them through from `PriorSubmissionModel` (`submissionId`, `submissionDate`, `appearanceDateTime`, `location`, `room`) and take `accusedName` from the matching ticket in the local `tickets` ref. **No backend change is needed** — `PriorSubmissionModel` already returns all of these.

---

## Testing

Notify the user before writing tests (per project rules). Both suites must pass:
`dotnet test api/CES.API/CES.API.sln` and `cd web && npm run test`.

### Backend

`CES.Business.Tests/Services/FileServiceTests.cs` — replace the four `UpdateExhibitDescriptionAsync` tests with:
- appends an entry and returns the file with `descriptions` ordered oldest → newest;
- a second call **appends** (does not overwrite) — two entries survive;
- writes **no** `SubmissionAuditLog` row;
- officer + `EnteredValue != null` → `InvalidOperationException`;
- admin override + `EnteredValue != null` → succeeds;
- whitespace-only text → `ArgumentException`;
- text > `DescriptionMaxLength` → `ArgumentException`;
- `\r\n` normalized to `\n` and interior whitespace preserved;
- unknown `fileId` → `KeyNotFoundException`;
- an accepted file's `metadata.json` is rewritten on append (existing `FinalizeClassificationAsync` assertion pattern).

`CES.Business.Tests/FileStorage/AcceptedMetadataWriterTests.cs` — exhibit carries `descriptions[]` in order; `schemaVersion == 2`; no `description` scalar; revisions contain no `Description` lines.

`CES.API.Tests/Controllers/FilesControllerTests.cs` — `POST /api/files/{id}/descriptions` 200 for User and Admin; 401/403 unauthenticated; 400 on empty; 404 on unknown id; 409 for an officer on an Entered exhibit. Old `PATCH /description` tests deleted.

`CES.Business.Tests/Services/SubmissionServiceTests.cs` — `SearchExhibitsAsync` / `GetSubmissionsByFileNumberAsync` / `RetrieveSubmission` hydrate `descriptions[]`.

### Frontend

`services/__tests__/SubmissionService.spec.ts` — `addExhibitDescription` POSTs to `/files/{id}/descriptions` with `{ descriptionText }`.

`components/__tests__/shared/ExhibitList.spec.ts` — rewrite the description block:
- renders condensed by default when `initialExpanded` is false; row 2 (Marked/Entered/Source selects) absent until the chevron is clicked;
- chevron toggles a single row, independently of siblings, and sets `aria-expanded`;
- **no** descriptions → textarea rendered; blur with text calls `addDescriptionFn` and emits `fileUpdated`;
- **has** descriptions → no textarea anywhere in the list; first entry shown, truncated at `DESCRIPTION_PREVIEW_MAX_LENGTH`, with `+N` when addenda exist;
- textarea disabled for an officer when `enteredValue` is set; enabled with `alwaysEditable`;
- `linkableTitle` → filename click emits `titleClick`.

`components/__tests__/shared/ExhibitDetailModal.spec.ts` (moved from `admin/`):
- Descriptions section lists all entries oldest → newest and preserves newlines;
- add-description textarea calls `addDescriptionFn`, appends to the list, emits `fileUpdated`;
- `canViewNotes: false` → Notes section absent **and** `getExhibitNotes` never called;
- `canViewNotes: true` → Notes section renders (existing tests).

`components/__tests__/officer/SubmissionForm.spec.ts` — clicking an exhibit filename opens the detail modal; the modal shows no Notes section.

`components/__tests__/admin/ExhibitSearch.spec.ts` / `admin/SubmissionReview.spec.ts` — update fixtures from `description: '…'` to `descriptions: [...]`; assert the condensed default in search.

---

## Verification

1. `cd docker && ./manage debug`.
2. **Officer:** select tickets → upload an exhibit → on the list, type a multiline first description into the inline textarea (it grows) → blur → ✓, and the row now shows it truncated on one line with no input.
3. Click the filename → detail modal opens with the Descriptions section and **no Notes section**; add an addendum → both entries listed, newlines preserved, in order.
4. Mark the exhibit, then Enter it → the officer's description input disappears (locked).
5. **Admin:** Exhibit Search → results render one condensed line per exhibit; chevron expands to reveal Marked/Entered/Source; admin can append a description to an Entered exhibit from the modal.
6. Inspect `metadata.json` for the submission: `schemaVersion: 2`, each exhibit has `descriptions[]` with `text`/`by`/`atUTC`, and `revisions` contain no `Description` lines.

---

## Out of Scope / Open Questions

- **Redaction / deletion of a description entry.** Nothing can remove a mistaken entry; the only remedy is an addendum. If registry policy later requires striking an entry, it should be a soft-delete flag with the original text retained — not a hard delete.
- **Editing within a grace window** (analogous to `ClassificationEditWindowSeconds`) is deliberately not offered: append-only is the whole point.
- **Expand-all / collapse-all** control on the Exhibit Search results header — easy add later if a JJ asks for it; not built now.
- **Notes for officers** remain out of scope: registry-only, Admin-only API.
