# Accepted Exhibit File Storage — Development Plan

**Status:** Draft dev plan (implements [accepted-file-storage.md](accepted-file-storage.md))
**Date:** 2026-07-02
**JIRA:** CES-39
**Audience:** Developer or AI assistant picking up the build

---

## Purpose

[accepted-file-storage.md](accepted-file-storage.md) records the product decisions for moving
accepted exhibits from a single per-submission ZIP to **unzipped, single-instance files** with a
**maintained metadata sidecar** and **per-file auto-acceptance**. This document turns those
decisions into a concrete, ordered engineering plan: what code changes where, in what sequence,
with which tests, and which questions still block specific pieces.

It is deliberately mapped to the code as it exists today. Key touch-points:

- [LocalFileStorage.cs](../api/CES.API/FileStorage/LocalFileStorage.cs) — current ZIP writer (`AcceptSubmissionAsync`), `GetAcceptedPackageAsync`, `SaveAsync`, `GetAsync`, `DeleteAsync`.
- [IFileStorage.cs](../api/CES.Business/Interfaces/IFileStorage.cs) — storage abstraction.
- [SubmissionService.cs](../api/CES.Business/Services/SubmissionService.cs) — `AcceptSubmissions`, `RejectSubmissions`, `GetAcceptedPackageAsync`, `RemoveFileAsync`.
- [FileService.cs](../api/CES.Business/Services/FileService.cs) — `MarkExhibitAsync`, `EnterExhibitAsync`, `UpdateExhibitDescriptionAsync` (the auto-accept + metadata-refresh trigger points).
- [StoredFiles.cs](../api/CES.Entities/Entities/StoredFiles.cs) — needs `IsAccepted` / `AcceptedAtUTC` / `CanonicalPath` / `Sha256`.
- [StorageOptions.cs](../api/CES.API/configuration/StorageOptions.cs) — `AcceptedPath`, `LocalPath`.
- [FilesController.cs](../api/CES.API/Controllers/FilesController.cs) — per-file view/download endpoints.
- [SubmissionTicket.cs](../api/CES.Entities/Entities/SubmissionTicket.cs) — `FileNumberText`, the "who" path segment.

---

## Guiding constraints (from the spec's decisions)

| # | Decision | Engineering consequence |
|---|---|---|
| 1 / 12 | Metadata pointer, **no symlinks** | Cross-ticket resolution is app logic reading DB (`SubmissionTicket → SubmissionId → path`); never `File.CreateSymbolicLink`. |
| 2 (revised) | Path `{AcceptedPath}/{locationId}/{roomCode}/{shortDate}/{submissionId}/` — **submission-leaf, not ticket-leaf** (see [Proposed revision to Decision #2](#proposed-revision-to-decision-2)) | New path builder + per-segment sanitizer. Single-instance storage becomes **structural** (one submission → one folder); no canonical-folder selection, no per-ticket pointer folders. |
| 3 | Id-addressed key `{exhibitId}{ext}` | Canonical filename derives from `StoredFiles.Id`, not SHA256, not original name. |
| 5 | **DB is source of truth** | Persist `CanonicalPath`/`Sha256`/`IsAccepted` on `StoredFiles`; `metadata.json` is a derived export. Resolution must work from DB even if the sidecar is missing. Ticket→file lookup is a DB query, not a filesystem walk. |
| 6 | **Per-file download only** | Rework `GetAcceptedPackageAsync` into a per-exhibit getter; drop the stored-ZIP contract. |
| 13 | **Per-file auto-accept** on first Marked **or** Entered | Promotion logic lives in the classification path, not only whole-submission Accept. |
| 13 | Bytes immutable at accept; metadata mutable until Entered | Every metadata edit on a Marked-but-not-Entered accepted file re-writes the **single** `metadata.json` in the submission folder (atomic). |

Out of scope for this build (per spec): legacy ZIP migration (#9), retention/disposition (#8),
integrity sweeps / signing (#10), file-lock concurrency (#11), zip-on-the-fly (#6), and the
new-role authorization/editable-field-set spec (#7 — interim download role settled in Q4 below).

---

## Proposed revision to Decision #2

> **Status: adopted (PO-confirmed 2026-07-02).** This is now the plan of record. The parent spec's
> Decision #2 still carries a proposed-revision note for its own formal update; the only thing that
> would reopen this is a downstream/records system that must browse the raw store by ticket number
> (none known).

**Original Decision #2:** leaf folder = `{fileNumberText}` (ticket number), with a chosen "canonical"
ticket folder holding the bytes and a pointer-only `metadata.json` copied into every other associated
ticket folder.

**Revised Decision #2:** leaf folder = **`{submissionId}`**:

```
{AcceptedPath}/{locationId}/{roomCode}/{shortDate}/{submissionId}/
              └── where ──┘ └where┘ └─ when ─┘ └── submission ──┘
                  metadata.json          # one per submission
                  {exhibitId}{ext}       # exhibit bytes, stored once
```

**Why the change is sound (and consistent with decisions already made):**

- **Exhibit↔ticket association is per-submission, not per-file.** `StoredFiles` has no ticket FK;
  tickets hang off `Submission.Tickets`. Every file in a submission shares the same full ticket set,
  so `submissionId` is the natural storage grain. A multi-ticket exhibit is stored **once** simply by
  living in its submission folder — single-instance is structural, not engineered.
- **The pointer/canonical machinery dissolves.** No canonical-folder selection, no writing pointer
  metadata into N ticket folders, no re-writing metadata across N folders on a Marked-file edit.
- **Ticket lookup is unchanged in capability.** `SubmissionTicket.FileNumberText → SubmissionId →
  path` is a DB query — and Decision #5 already makes the DB the source of truth. The metadata
  `associatedTickets[]` still records the full mapping for traceability.
- **It removes the spec's top injection risk.** `fileNumberText` is externally-sourced ticket data;
  Decision #2 called it the most dangerous path segment. The submission-leaf makes the leaf a
  **system-generated int**, eliminating that surface (location/room/date still get sanitized).
- **The only thing lost was already lost.** Decision #2's rationale was a ticket-navigable on-disk
  tree, but Decision #1 (pointers → non-canonical ticket folders contain *only* `metadata.json`, no
  bytes) already made raw browse-by-ticket unreliable and mandated app-mediated access. Submission-leaf
  keeps `where/when` navigability (the `location/room/date` prefix) and maps to S3 identically.

**Confirm with the PO:** does any downstream/records system expect to browse the raw store organized
**by ticket number**? If no, adopt the revision. If yes, keep ticket-leaf + pointers.

---

## Proposed build sequence

The work splits into six phases. Phases 1–4 build the storage engine (they can land and be unit-tested
without changing user-facing behaviour); Phase 5 flips behaviour to per-file auto-accept and **retires
the whole-submission Accept** (Open Q3); Phase 6 reworks download. Sequence 5 and 6 together so the
old ZIP Accept/download path is removed in one coherent change.

### Phase 1 — Data model & configuration

**Goal:** persist per-file acceptance and canonical location in the DB (the source of truth).

1. **Extend [StoredFiles.cs](../api/CES.Entities/Entities/StoredFiles.cs):**
   - `bool IsAccepted` (default `false`)
   - `DateTime? AcceptedAtUTC`
   - `string? CanonicalPath` — path **relative to `AcceptedPath`**, e.g. `{locationId}/{roomCode}/{shortDate}/{submissionId}/{exhibitId}{ext}`. One value per accepted file; with the submission-leaf layout there are no pointer copies to keep in sync.
   - `string? Sha256` — captured once at acceptance; the immutability proof.
   - `string? AcceptedFileName` (optional) — the `{exhibitId}{ext}` leaf, or derive from `Id` + `Path.GetExtension(OriginalFileName)`.
2. **EF migration:** add columns via `dotnet ef migrations add AcceptedFileStorage_PerFileAcceptance` against [CESDataStore.cs](../api/CES.EF/CESDataStore.cs) / [ModelRelationships.cs](../api/CES.EF/ModelRelationships.cs). Migrations run automatically on startup — verify the new migration applies cleanly on an existing dev DB. (No data backfill needed — POC, Decision #9.)
3. **[StorageOptions.cs](../api/CES.API/configuration/StorageOptions.cs):** document `AcceptedPath` as the **root of the logical tree** (not a flat ZIP directory). No new option strictly required, but consider `AcceptedPath` default rename for clarity. Keep `MaxFileSize` as-is.
4. **Constants:** metadata `schemaVersion = 1`, hash algorithm name `"SHA256"`, and the canonical-folder selection rule go in a constants file (project rule: no inline magic values). Reuse [ClassificationConstants.cs](../api/CES.Business/Constants/ClassificationConstants.cs) style.

**Tests:** entity defaults; migration up/down; `DeriveStatus` unaffected.

### Phase 2 — Path building & sanitization (security-critical)

**Goal:** a single, tested helper that turns `(locationId, roomCode, shortDate, submissionId, exhibitId, ext)` into a safe path **guaranteed to stay under `AcceptedPath`**.

1. New helper, e.g. `AcceptedPathBuilder` in [CES.API/FileStorage](../api/CES.API/FileStorage/) (or `CES.Business` if it needs to be unit-tested without the API — recommended):
   - `SanitizeSegment(string raw)` — **deterministic & idempotent** (same input → same output every time, so paths are exactly recreatable). Whitelist charset = **lowercased** alphanumerics + `-`; strip all other punctuation, path separators, `..`, absolute-path markers; reject empty/overlong. (Lowercase because the target FS/object-store may be case-sensitive.) Applies to `locationId`, `roomCode`, `shortDate`. `submissionId` is a system-generated int (no sanitization risk) and `exhibitId` is a GUID — both are still range/format-checked defensively. See resolved Open Q1 for the full contract.
   - `BuildCanonicalRelativePath(...)` → `{loc}/{room}/{date}/{submissionId}/{exhibitId}{ext}` using sanitized segments.
   - `ResolveAndVerifyWithinRoot(acceptedRoot, relativePath)` → combine, `Path.GetFullPath`, assert `StartsWith(fullAcceptedRoot)` (with trailing-separator guard). Throw a typed exception on escape.
2. **No canonical-folder selection needed.** With the submission-leaf layout each submission owns exactly one folder, so the "pick a canonical ticket / write pointers elsewhere" step from the original spec is removed. (This is what dissolves Open Q2.)

**Tests (high priority — this is the injection surface):** `..`, `../../etc`, absolute paths, embedded separators, unicode/overlong, empty segment on `locationId`/`roomCode`/`shortDate`, and a valid happy path. Assert `ResolveAndVerifyWithinRoot` rejects every traversal attempt. (`shortDate` is app-generated `yyyyMMdd` but still sanitized defensively.)

### Phase 3 — Metadata sidecar writer

**Goal:** produce/refresh `metadata.json` atomically, derived from DB.

1. New `AcceptedMetadataWriter` (Business layer so it's unit-testable). Serialize the shape defined in the spec (`schemaVersion`, submission, tickets, exhibits[] with `canonicalPath`/`sha256`/`isAccepted`/classification/`associatedTickets`, `revisions[]`).
2. **Atomic write:** write to `metadata.json.tmp` in the target folder, `File.Move(tmp, final, overwrite: true)` (temp+rename per spec).
3. **One file per submission.** With the submission-leaf layout there is a single `metadata.json` per submission folder — no pointer copies, no "canonical vs pointer" flag. It lists every exhibit with its `canonicalPath` and `associatedTickets[]` (the full ticket mapping, so traceability by ticket is preserved without per-ticket folders).
4. **Revisions:** append-only. Source the audit entries from existing [SubmissionAuditLog](../api/CES.Business/Services/FileService.cs) rows (already written on Mark/Enter/Description) rather than inventing a parallel trail — map them into the `revisions[]` array at write time. `revisions[].by` = `SubmissionAuditLog.ChangedBy` (resolved Open Q7); the current `"Admin"`/`"Officer"` fallbacks are acceptable in the exported artifact for now and will become Keycloak user identifiers once Keycloak is integrated — no special handling needed here.

**Tests:** serialization snapshot; atomic replace leaves no `.tmp`; pointer folder contains only metadata; revisions reflect audit logs.

### Phase 4 — `IFileStorage` surface & `LocalFileStorage` rewrite

**Goal:** replace ZIP write/read with per-file promotion + resolution.

1. **Extend [IFileStorage.cs](../api/CES.Business/Interfaces/IFileStorage.cs):**
   - `Task<AcceptedFileResult> PromoteToAcceptedAsync(Submission submission, StoredFiles file)` — copy bytes from pending (`LocalPath/StoredPath/StoredFileName`) into the submission folder **once**, compute SHA256, return `{ canonicalPath, sha256 }`. Idempotent: if already accepted, no re-copy.
   - `Task WriteMetadataAsync(Submission submission)` — (re)write the **single** `metadata.json` in the submission folder.
   - `Task<Stream> GetAcceptedExhibitAsync(StoredFiles file)` — open the canonical file by `CanonicalPath` (resolved + verified within root); throw a clear error if missing (fail safe, Decision/Security).
   - **Remove** `AcceptSubmissionAsync(Submission)` and `GetAcceptedPackageAsync(Submission)` (the whole-submission ZIP path). Per resolved Open Q3, the whole-submission Accept action is retired for this iteration — auto-accept on classification covers traffic court, and keeping a parallel bulk-Accept would only muddy the codebase (it stays in git history if ever needed). Do **not** reimplement it as an orchestration.
2. **[LocalFileStorage.cs](../api/CES.API/FileStorage/LocalFileStorage.cs):**
   - Remove `System.IO.Compression` / `ZipArchive` usage.
   - Implement the new methods using Phase 2 path builder + Phase 3 writer.
   - Keep `SaveAsync`/`GetAsync`/`DeleteAsync` (pending store) unchanged.
   - **Single-instance guarantee:** structural — one submission writes to one folder, so an exhibit associated with N tickets is physically one file by construction. Still covered by a test asserting N tickets → 1 physical file.
   - **Atomic byte placement:** copy to `{exhibitId}{ext}.tmp` then rename.

**Tests:** promote copies once + hashes; second promote is a no-op; `GetAcceptedExhibitAsync` streams canonical bytes; missing canonical file → typed error, not empty stream; write-metadata produces one file per submission folder.

### Phase 5 — Auto-accept trigger & post-accept metadata refresh (behaviour switch)

**Goal:** wire per-file acceptance into the classification path (Decision #13).

1. **[FileService.cs](../api/CES.Business/Services/FileService.cs) — inject `IFileStorage`** (currently only has `ICESDataStore`). After a successful Mark/Enter:
   - **`MarkExhibitAsync`:** if `!file.IsAccepted`, promote → set `IsAccepted`/`AcceptedAtUTC`/`CanonicalPath`/`Sha256`, then `WriteMetadataAsync`. If already accepted (Marked→re-Marked), **refresh metadata only** (bytes/sha unchanged).
   - **`EnterExhibitAsync`:** same promote-if-needed; Entered locks classification (existing edit-window logic already guards further edits) — refresh metadata.
   - **`UpdateExhibitDescriptionAsync`:** if accepted-and-not-Entered, refresh metadata; description edits after Entered are already blocked by existing guard.
2. **Load associated tickets** for the metadata refresh: `FileService` currently fetches `StoredFiles` without `Submission.Tickets`. Add `.Include(f => f.Submission).ThenInclude(s => s.Tickets)` (and `.Files`) where promotion/refresh happens.
3. **Ordering / failure safety:** persist DB changes (source of truth) first, then write files; if the file write throws, surface it but the DB remains authoritative and the sidecar can be regenerated. Consider wrapping promote+metadata so a partial failure is logged and retried on next edit.
4. **Immutability enforcement (Open Q6):** extend [`RemoveFileAsync`](../api/CES.Business/Services/SubmissionService.cs) to also forbid removal when `file.IsAccepted` (today it only checks `Submission.Status == Pending`; with per-file accept, a Pending submission can now contain accepted files). **An accepted file can never be removed** — this preserves current behaviour and sidesteps the reference-counting question. Correspondingly, [`RejectSubmissions`](../api/CES.Business/Services/SubmissionService.cs) must only delete **non-accepted** retained files (`!f.IsDeleted && !f.IsAccepted`); accepted files stay put even on a whole-submission Reject.
5. **Submission-level status derivation (Open Q6):** a submission's `Status` is no longer set by an explicit Accept action (removed in Phase 4). Decide how it is derived from its files — e.g. a submission reads as `Accepted` once **all** its non-deleted files are accepted, and flips back to `Pending` if a **new** file is uploaded into the same submission (the recent change reuses one `submissionId` for files added in the same session, so an already-accepted submission can gain an un-accepted file). Centralize this transition (a helper on `SubmissionService`/`SubmissionExtensions`) and call it after upload and after each auto-accept. *(Confirm the exact rule — see Open Q6 note.)*

**Tests:** first Mark auto-accepts + writes metadata; Marked→Entered keeps same bytes/sha, updates metadata; description edit on Marked file rewrites the submission's `metadata.json`; Entered file rejects further classification; accepted file cannot be removed even on a Pending submission; Reject leaves accepted files but deletes un-accepted ones; adding a new file to an accepted submission flips it back to Pending.

### Phase 6 — Download / retrieval rework

**Goal:** serve individual accepted files; retire the stored ZIP.

1. **[SubmissionService.GetAcceptedPackageAsync](../api/CES.Business/Services/SubmissionService.cs):** replace with a per-exhibit getter — e.g. `GetAcceptedExhibitAsync(Guid fileId)` returning `(stream, fileName, contentType, error)`, authorization-gated. Resolve via DB `CanonicalPath` (never trust a client-supplied path).
2. **[FilesController.cs](../api/CES.API/Controllers/FilesController.cs):** the existing `View`/`Download` endpoints call `_fileStorage.GetAsync` (pending path). For accepted files, route to `GetAcceptedExhibitAsync`. Branch on `IsAccepted` **in the service** so the controller stays thin. **Enable `[Authorize(Roles = "User,Admin")]`** on these endpoints (resolved Open Q4 — same roles as the classification endpoints; the definitive role owner is the forthcoming new-role spec, Decision #7). They are currently commented out — do not ship them open.
3. **Remove** `GetPackageName`/`GetPackagePath`/`GetAcceptedPackageAsync(Submission)` ZIP paths from `LocalFileStorage`. Update [FilesControllerTests](../api/CES.API.Tests/Controllers/FilesControllerTests.cs) and any submission-package test.
4. **Zip-on-the-fly:** not built now (Decision #6). Leave a clearly named seam (e.g. an `IExhibitPackager` interface with no implementation, or just a `// deferred` note) so re-introduction later doesn't require reshaping the storage layer.

**Tests:** accepted file downloads by id with correct filename/content-type; unauthorized request rejected; non-accepted/removed file → 404; path never exposed to client.

---

## Cross-cutting: testing checklist (project rule — tests required)

Run `dotnet test api/CES.API/CES.API.sln` and `cd web && npm run test` before "done". Backend
additions map to the spec's "Tests" impact row:

- [ ] Auto-accept on first Marked; on first Entered.
- [ ] De-dup: one exhibit, N tickets → **one** physical file (storage assertion).
- [ ] Marked-but-not-Entered metadata edit rewrites the submission's single `metadata.json`.
- [ ] Entered locks classification.
- [ ] Path sanitization rejects traversal/injection; resolved path stays within `AcceptedPath`.
- [ ] Immutability: accepted bytes read-only, `sha256` unchanged across metadata edits, no delete.
- [ ] Orphaned/missing canonical file fails safe with a clear error.
- [ ] SHA256 captured once and persisted to DB.

**Frontend:** minimal for this backend-heavy change. Where the admin/officer UI offers a
"download package (.zip)" action, **swap it to per-file download silently** — no UI/UX sign-off needed
(resolved Open Q5; a traffic-court-specific admin rework is coming in this iteration). Adjust the
affected store/service tests.

---

## Risk notes

- **Behaviour change is load-bearing on classification.** Auto-accept means the *first* Mark now
  has storage side-effects. Keep DB-first ordering so a storage hiccup never corrupts the source of
  truth, and make `WriteMetadataAsync` fully regenerable from DB.
- **Path-segment sanitization still matters** even with the submission-leaf revision — `locationId`,
  `roomCode`, and `shortDate` remain in the path. The revision *removes* the worst case (the
  externally-sourced `FileNumberText` is no longer a directory name), but don't skip Phase 2's
  within-root verification.
- **Whole-submission Accept is being removed** (`SubmissionService.AcceptSubmissions` + its
  "all-exhibits-Entered" gate), per resolved Open Q3. Confirm nothing else depends on that action:
  the admin listing's Accept control, its route, and any frontend call must be retired or repointed at
  the per-file flow in the same change. Submission-level `Accepted` status is now **derived** from its
  files (Phase 5 step 5), not set by a button — verify the admin listing reflects the derived status.
- **Ticket→file resolution is app-only.** Anything that reads the accepted store directly (backups,
  future object-store sync) must resolve ticket numbers via DB/metadata — the store is organized by
  submission, not ticket, so there is no per-ticket folder to walk.

---

## Resolved engineering questions

These surfaced from the spec while planning the build. The spec's Decisions resolved the product
direction; the **engineering** unknowns below have now all been answered by the product owner
(2026-07-02) and are reflected in the phases above. They are kept here as a decision record. The only
items left for the *build* to nail down are the two follow-ons flagged in Q1 (segment charsets) and Q6
(the exact `Accepted`↔`Pending` transition rule) — neither blocks starting.

1. **~~Allowed charset for `fileNumberText`.~~ Resolved — `fileNumberText` is no longer a path segment
   (revised Decision #2).** The remaining segments (`locationId`, `roomCode`, `shortDate`) are still
   sanitized, and the PO's guidance from this thread is now the **`SanitizeSegment` contract**:
   - **Deterministic & idempotent** — the same input must always produce the same segment (so a path is
     exactly recreatable). No random or time-based component.
   - **Strip most punctuation**; **keep `-`** (safe in a folder name). Reject/remove path separators,
     `..`, and other punctuation rather than encoding it.
   - **Lowercase all alpha** — assume the target filesystem/object-store may be case-sensitive, so
     normalize to lowercase for a stable, collision-free key.
   - Still enforce empty/overlong rejection and the within-root check.

2. **~~Canonical-folder tie-break & stability.~~ Resolved — adopt the submission-leaf layout.** Per the
   PO note in this thread: files aren't accessed by humans directly; all access is through the app (or a
   possible internal API), so the store only needs to let the *system* find files. Using `submission.Id`
   as the leaf means each submission owns exactly one folder, there is no canonical-folder selection, and
   the path is stable regardless of ticket add/remove churn. See [Proposed revision to Decision
   #2](#proposed-revision-to-decision-2). *(This revision is now the plan of record; the parent spec's
   Decision #2 should be updated to match — a proposed-revision note has been added there for sign-off.)*

3. **~~`AcceptSubmissionAsync` — keep or retire?~~ Resolved — retire it.** *(Phase 4/5)* The
   whole-submission Accept has value only for far-future development and would muddy the codebase if
   left in; remove it for now (recoverable from git history if ever needed). Remove
   `SubmissionService.AcceptSubmissions`, its "all-exhibits-Entered" gate, `AcceptSubmissionAsync`, and
   the associated route/UI control. Submission-level `Accepted` status is derived from its files instead
   (Phase 5 step 5). Ties to [admin-listing-update.md](admin-listing-update.md).

4. **~~Download authorization role.~~ Resolved — `User,Admin`.** *(Phase 6)* Guard the individual-file
   download with `[Authorize(Roles = "User,Admin")]` (same as the classification endpoints). The
   definitive role owner remains the forthcoming new-role spec (Decision #7); this is the interim gate
   so the endpoints aren't shipped open.

5. **~~Existing "download package" UI.~~ Resolved — swap silently.** *(Phase 6)* Switch any
   `submission-{id}-package.zip` download to per-file **now, with no UI/UX sign-off**. A traffic-court-
   specific admin rework is coming in this same iteration and will absorb any follow-on UI changes.

6. **~~Reject-before-accept vs. per-file accept.~~ Resolved — accepted files are never removed.**
   *(Phase 5)* Once a file `IsAccepted` it cannot be removed, which keeps behaviour identical to today
   and avoids the reference-counting question entirely. `RejectSubmissions` deletes only non-accepted
   retained files. **Follow-on to confirm during build:** because a recent change reuses one
   `submissionId` for files uploaded in the same session, an already-`Accepted` submission that gains a
   new file should flip back to `Pending` until that file is accepted — implement and confirm the exact
   status-transition rule (Phase 5 step 5).

7. **~~Metadata `by` / actor attribution.~~ Resolved — use `SubmissionAuditLog.ChangedBy`.** *(Phase 3)*
   The current `"Admin"`/`"Officer"` values are acceptable in the exported artifact for now. Once
   Keycloak is integrated (near future), `ChangedBy` will carry the Keycloak user identifier instead —
   no change needed in this work; the metadata writer just serializes whatever `ChangedBy` holds.

8. **~~Object-storage target confirmation.~~ Resolved — no confirmation needed now.** *(Design-wide)*
   Nothing to lock down at this stage. A new `IFileStorage` implementation can be added for whatever
   storage target is chosen later; keeping that interface **consistent and stable during testing** is
   the only concern for now. Avoid POSIX-only assumptions that wouldn't map to object keys.