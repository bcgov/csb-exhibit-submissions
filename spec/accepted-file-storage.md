# Accepted Exhibit File Storage (Unzipped + Metadata)

**Status:** Draft — Decisions Recorded
**Date:** 2026-06-26
**Revised:** 2026-07-02
**JIRA:** CES-39

---

## Overview

Today, when a submission is **Accepted**, the API gathers all retained exhibits and writes them into a **single ZIP archive** (`{AcceptedPath}/{shortDate}_{submissionId}.zip`) with a combined `metadata.json` manifest inside the archive. The archive is the unit of download.

This spec explores changing that behaviour so that on Accept:

1. Exhibit files are written **unzipped** into the directory structure where they belong, rather than bundled into a single archive.
2. A **metadata file** is created alongside the files, and is **created/updated/maintained** whenever exhibit information changes (e.g. classification edits) — even after acceptance.
3. Once an exhibit is accepted it is **immutable and cannot be removed**. Only its metadata may change.
4. An exhibit associated with **multiple ticket numbers** is stored **exactly once** on disk but is discoverable from each associated ticket's location — **without duplicating the bytes**. This is critical because exhibits can be large video files; physically copying them per ticket is unacceptable for storage.

This document began as a draft for discussion. The open questions it surfaced have now been answered by the product owner; the resolved decisions are recorded in [Decisions & Discovery](#decisions--discovery) and are reflected throughout the body below.

---

## Motivation

- **ZIP is opaque and write-once.** The current `AcceptSubmissionAsync` builds the entire archive in one pass. If classification or other metadata changes after acceptance, there is no clean way to amend the package — you would have to rebuild the whole ZIP. The [admin-listing-update.md](admin-listing-update.md) and [exhibit-classification.md](exhibit-classification.md) work both allow admin-side edits to exhibit metadata, which can occur after the all-exhibits-final gate that enables Accept.
- **Downstream consumers want files, not archives.** Records/justice systems and reviewers generally want to open an exhibit directly, not unzip a bundle first.
- **Video files are large.** Duplicating a multi-hundred-MB body-cam video once per associated ticket multiplies storage cost and write time. We need single-instance storage with multiple logical references.
- **Auditability.** A human- and machine-readable metadata file that lives next to the files (and is maintained over time) is easier to audit than a manifest frozen inside a ZIP.

---

## Current Behaviour (as-built)

### Pending upload

`SubmissionService.SubmitEvidence` stores each uploaded file via `LocalFileStorage.SaveAsync` under:

```
{LocalPath}/{locationId}/{shortDate}/{roomCode}/{submissionId}/{guid}{ext}
```

Files are stored under a **GUID** name (not the original filename), with original name retained in the `StoredFiles` DB row. Storage is **submission-scoped**, not per-ticket.

### Accept

`LocalFileStorage.AcceptSubmissionAsync(submission)`:

- Resolves package path `{AcceptedPath}/{shortDate}_{submissionId}.zip`.
- Collects **retained** (`!IsDeleted`) exhibits.
- De-duplicates entry names within the archive (`name_1.ext`, …).
- Streams each file into the ZIP under its **original filename**.
- Computes a **SHA256** per file.
- Writes a single combined `metadata.json` (submission info, tickets, per-exhibit details + hashes) into the archive.

### Download

`SubmissionService.GetAcceptedPackageAsync` streams the ZIP back as `submission-{id}-package.zip`, gated on `Status == Accepted`.

### Reject

Retained files are physically deleted from `{LocalPath}` and marked `IsDeleted`.

### Relevant entities

- `Submission` — has `Status` (`Pending`/`Accepted`/`Rejected`), `LocationId`, `RoomCode`, `UploadDate`, `Files`, `Tickets`.
- `StoredFiles` — `Id` (GUID), `OriginalFileName`, `StoredFileName`, `StoredPath`, `ContentType`, `FileSize`, classification fields (`MarkedValue`/`MarkedAt`/`EnteredValue`/`EnteredAt`), `Description`, `IsDeleted`.
- `SubmissionTicket` — `AppearanceId`, `FileNumberText` (the ticket number), accused info. A submission has **many** tickets; a single uploaded exhibit therefore already maps to many ticket numbers (see [multi-ticket-exhibit-upload.md](multi-ticket-exhibit-upload.md)).

---

## Proposed Behaviour

### Goals

1. On Accept (either Marked or Entered), write each retained exhibit as a **standalone file** into an accepted store.
2. Write/maintain a **metadata file** that describes the accepted exhibits and is updatable after acceptance.
3. Guarantee **single-instance** storage of an exhibit's bytes even when it is associated with N ticket numbers.
4. Make an accepted exhibit **immutable** (no delete, no content change); only metadata may change.

### Directory layout — decided: Option A

The core tension: exhibits are conceptually organized **per ticket** (`FileNumberText`), but the bytes must exist **once**. Three candidate layouts were considered; **Option A is the chosen layout** (see [Decision #2](#decisions--discovery)). Options B and C are retained below only for context.

#### Option A — Canonical content store + per-ticket pointer directories (chosen)

Store the actual bytes once in an **id-addressed** "canonical" directory (see [Decision #3](#decisions--discovery) — canonical key is `{exhibitId}`, not `{sha256}`), then give each ticket its own directory containing **metadata pointers** (not copies, not symlinks).

```
{AcceptedPath}/
  _exhibits/                                  # canonical, single instance of each file
    {exhibitId}{ext}                          # id-addressed canonical key
  tickets/
    {FileNumberText}/
      metadata.json                           # ticket-scoped view + metadata pointers
```

- **Pointer mechanism** is resolved: the ticket `metadata.json` records the canonical path and the app resolves it. No symlinks (see [Decision #1](#decisions--discovery) and the section below).
- Pro: bytes stored once; each ticket directory is a complete logical view via its metadata.
- Con: requires the pointer-resolution layer to be reliable — but this is pure application logic, no filesystem link dependency.

**other options removed for brevity**

> **Decision:** Option A (canonical store + per-ticket pointer dirs), with the pointer mechanism resolved through the **metadata file** — never filesystem symlinks (see next section). This keeps de-duplication explicit and backup-safe while still presenting a per-ticket view, and ports cleanly to object storage.

### De-duplication: metadata pointer (symlinks ruled out)

| Approach                           | How a ticket finds the file                                                                                    | Pros                                                                                                                                   | Cons                                                                                                                                                                                                        |
| ---------------------------------- | -------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Filesystem symlink**             | OS-level link in the ticket dir resolves to the canonical file                                                 | Transparent to any tool that opens the path; per-ticket folder is browsable                                                            | Breaks across filesystems that don't support symlinks (some Windows shares, certain backup/restore, object stores like S3 have **no** symlink concept); restore can dangle; security risk of link traversal |
| **Metadata pointer** (recommended) | Ticket `metadata.json` records `{exhibitId, canonicalPath, sha256}`; the app resolves and serves the real file | Portable to object storage; no dangling-link class of bugs; resolution is explicit and auditable; integrity verifiable via stored hash | Browsing the raw folder does not "show" the file; all access must go through the app/metadata layer                                                                                                         |
| **Hard link**                      | Two directory entries, one inode                                                                               | No extra bytes; both paths are "real"                                                                                                  | Same-filesystem only; confusing ownership; not portable to object storage; backup tools may copy twice                                                                                                      |

> **Decision:** Use the **metadata pointer** to resolve which exhibit belongs to which ticket. Symlinks are **not used** (see [Decision #1 and #12](#decisions--discovery)). Note the database remains the ultimate source of truth for these associations; the metadata file is a convenience/traceability artifact exported alongside the files (see [Decision #5](#decisions--discovery)). This is also the only model that ports cleanly to S3/Azure-Blob-style object storage where symlinks do not exist.

### Metadata file

Replaces the in-ZIP `metadata.json`. Lives next to the canonical files and/or in each ticket directory. Maintained on every information change. The **database is the source of truth** ([Decision #5](#decisions--discovery)); this file is a convenience/traceability export derived from it, and any inconsistency is resolved in the database's favour.

Proposed shape (per submission, and/or per ticket view):

```jsonc
{
  "schemaVersion": 1,
  "submissionId": 123,
  "status": "Accepted",
  "acceptedAtUTC": "2026-06-26T18:00:00Z",
  "lastUpdatedUTC": "2026-06-26T18:00:00Z", // bumped on any metadata edit
  "hashAlgorithm": "SHA256",
  "tickets": [
    { "appearanceId": "...", "fileNumberText": "...", "accusedName": "..." },
  ],
  "exhibits": [
    {
      "exhibitId": "guid",
      "originalFileName": "bodycam.mp4",
      "canonicalPath": "_exhibits/guid.mp4", // single physical location
      "contentType": "video/mp4",
      "fileSize": 734003200,
      "sha256": "…", // integrity / immutability proof
      "markedValue": "A",
      "markedAt": "…",
      "enteredValue": "12",
      "enteredAt": "…",
      "description": "…",
      "associatedTickets": ["FILE-001", "FILE-002"], // de-dup: one file, many tickets
    },
  ],
  "revisions": [
    // append-only audit of metadata changes
    {
      "atUTC": "…",
      "by": "Admin",
      "change": "EnteredValue A->B on exhibitId …",
    },
  ],
}
```

- The **`sha256`** captured at acceptance is the immutability proof: the bytes must never change after Accept; only the metadata around them does.
- **`revisions`** gives an append-only audit trail so post-acceptance metadata edits are traceable.
- Metadata writes must be **atomic** (write temp + rename) to avoid a half-written file if the process dies mid-update.

### Immutability rules

- After Accept: exhibit **bytes** are read-only; no delete (`RemoveFileAsync` already forbids removal on non-Pending submissions — extend/keep this).
- Metadata **may** change (classification, description) and each change bumps `lastUpdatedUTC` and appends to `revisions`.
- A change to metadata must **never** alter the canonical file or its recorded `sha256`.
- **Who may edit, and the exact editable field set, is owned by a forthcoming spec** ([Decision #7](#decisions--discovery)) that introduces a new role with an exhibit-focused search page (exhibits, not submissions). This development targets that new role/path; the existing admin submission listing remains valid for future changes.

### Download

**This release ships per-file download only** ([Decision #6](#decisions--discovery)):

- Serve **individual files** by exhibit id (resolved through metadata/DB to the canonical path), authorization-gated exactly as the current package download.
- **Zip-on-the-fly** ("download all") is **deferred** — it has clear value when this is extended to additional court systems, and can be reintroduced then by generating the zip on demand from the metadata/DB view (no stored archive), decoupling the _download format_ from the _storage format_. The existing `GetAcceptedPackageAsync` `.zip` contract is not a hard requirement for this release (see [Decision #9](#decisions--discovery) — POC phase, no legacy consumers to preserve).

---

## Impact / Areas Touched

| Area                                        | Change                                                                                                                                                   |
| ------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LocalFileStorage.AcceptSubmissionAsync`    | Stop writing a ZIP; write canonical files + metadata file(s); compute and persist SHA256 per exhibit                                                     |
| `IFileStorage`                              | New methods: write/update metadata, resolve exhibit by id, enumerate by ticket; possibly `GetAcceptedExhibitAsync`                                       |
| `LocalFileStorage.GetAcceptedPackageAsync`  | Rebuild as zip-on-the-fly (or add per-file getter)                                                                                                       |
| `SubmissionService.AcceptSubmissions`       | Unchanged gate logic; new storage call shape                                                                                                             |
| Post-accept metadata edits (classification) | New code path to update the metadata file when `StoredFiles` classification changes on an Accepted submission                                            |
| `StorageOptions`                            | Possibly add `CanonicalPath` / `TicketIndexPath`; clarify `AcceptedPath` semantics                                                                       |
| DB                                          | Optionally persist `canonicalPath` / accepted SHA256 on `StoredFiles` so resolution doesn't depend solely on the metadata file                           |
| Tests                                       | Update `LocalFileStorage`/`SubmissionService` accept tests; new tests for de-dup (one file, multiple tickets), metadata update, immutability enforcement |

---

## Security & Storage Concerns

### Security

- **Path traversal / injection:** `FileNumberText`, `LocationId`, `RoomCode` flow into directory names. These must be **sanitized/validated** (whitelist charset, reject `..`, path separators, absolute paths) before being used as path segments. The current GUID-based pending naming avoids this; ticket-named directories reintroduce the risk.
- **Symlink attacks:** if symlinks are used, a malicious or buggy link could point outside the storage root (link traversal). Any symlink must be validated to resolve **within** the accepted root. This is a strong argument for the metadata-pointer approach.
- **Integrity / tamper evidence:** the stored `sha256` lets us detect post-acceptance tampering. Consider a periodic integrity sweep that re-hashes canonical files and flags drift. For stronger non-repudiation, consider signing the metadata file.
- **Access control:** individual-file download must enforce the same authorization as the current package download (Accepted-only, admin-gated). Don't expose canonical paths directly to clients — resolve server-side.
- **Orphaned references:** metadata pointing at a missing canonical file (or vice-versa) must fail safe (clear error), not serve a wrong/empty file.

### Storage

- **Single-instance is the whole point:** verify the design never copies large video bytes per ticket. A test should assert that associating one exhibit with N tickets results in **one** physical file.
- **Reference counting / lifecycle:** because one file serves many tickets, "deleting" (in the rare Reject-before-Accept path, or future purge/retention) must not remove bytes still referenced by another ticket/submission. Need a reference-count or retention policy before any cleanup job is built. (Accepted exhibits are immutable/non-deletable, so this mainly affects retention/disposition, not user-driven delete.)
- **Atomic writes & crash safety:** both file placement and metadata updates must be crash-safe (temp + atomic rename) so an interrupted Accept doesn't leave a partial state.
- **Backups & restore:** symlinks/hardlinks complicate backup and restore (dangling links, double-copy). Metadata-pointer model restores cleanly. Confirm with whoever owns backups.
- **Object storage future:** if storage moves to S3/Azure Blob, there is **no symlink/hardlink concept** and no real "directory." The metadata-pointer model maps directly to object keys; the symlink model does not. Designing for metadata pointers now avoids a rewrite later.
- **Capacity:** removing the ZIP avoids a transient 2x burst (file on disk + copy inside archive) during Accept — a storage win for large videos.

---

## Decisions & Discovery

The questions below were resolved by the product owner on 2026-07-02. Each records the original question, the **Decision**, and any follow-up it leaves open.

1. **Pointer mechanism** — _Do we commit to metadata-pointer resolution or must per-ticket folders be browsable on disk (forcing symlinks)?_
   **Decision:** Commit to **metadata-pointer resolution**. No requirement for browsable-on-disk per-ticket folders; no symlinks.

2. **Directory layout** — _Option A vs. B vs. C; does any downstream system expect a specific on-disk structure?_
   **Decision:** **Option A** — canonical id-addressed content store + per-ticket pointer directories.

3. **Canonical key** — _content-addressed (`{sha256}`) vs. id-addressed (`{exhibitId}`)?_
   **Decision:** **Id-addressed (`{exhibitId}`)** — most manageable; ownership and retention stay simple. (Cross-submission byte de-dup is explicitly not a goal — see #4.)

4. **Cross-submission de-dup** — _is the same physical file ever uploaded under two different submissions, and should those collapse to one instance?_
   **Decision:** **Not a concern.** No cross-submission byte collapsing and no deep content check. Instead, when the officer-side "Prior Exhibits" list is generated, if the officer uploads a file that appears to already exist, show a **"file may already exist" warning and ask for confirmation**, but allow the re-upload. _Follow-up:_ officer-side "Prior Exhibits" warning is a separate UI work item.

5. **Metadata authority** — _is the JSON metadata file the source of truth, the database, or both?_
   **Decision:** The **database is always the source of truth**. The metadata file exists for convenience and traceability only. (The "signed artifact" idea from the original recommendation is not adopted in this scope — see #10.)

6. **Download contract** — _keep single-zip (zip-on-the-fly), add per-file, or both?_
   **Decision:** **Per-file download only for this release.** Zip-on-the-fly is deferred; it has value once this extends to additional court systems and can be redeveloped then.

7. **Post-accept edit scope** — _which fields may change after Accept, and who is authorized?_
   **Decision:** Exact editable field set and authorization are **owned by a forthcoming spec** that introduces a **new role with a new access path and an exhibit-focused search page** (shows exhibits, not submissions). This development targets that new role/path; the existing admin submission listing remains valid for future changes. Related: [exhibit-classification.md](exhibit-classification.md), [admin-listing-update.md](admin-listing-update.md). _Follow-up:_ the new-role spec must be authored.

8. **Retention / disposition** — _is there a records-retention or destruction policy for accepted exhibits?_
   **Decision:** **Out of scope for now.** _Follow-up:_ confirm with system architects; not a blocker for this work.

9. **Migration** — _what happens to already-accepted ZIP packages from the current code?_
   **Decision:** **No migration required.** Still in POC phase; all existing data is destructable. Revisit only before launch.

10. **Integrity policy** — _scheduled re-hash sweeps? sign metadata? tamper response?_
    **Decision:** **Not in this scope.** A possible future security enhancement.

11. **Concurrency** — _can two admins edit metadata for the same accepted submission simultaneously?_
    **Decision:** Treated as a **negligible edge case.** Because the database is the source of truth (#5), concurrent metadata-file edits are not a concern; no dedicated file-locking strategy is required.

12. **Filesystem support matrix** — _what filesystems/volumes will production run on; are symlinks viable?_
    **Decision:** **Symlinks are not required** (reinforces #1). Production is expected to be an **S3-style object store**, which the metadata-pointer model maps to directly. _Follow-up:_ confirm the storage target if any design detail turns out to depend on it.

---

## Out of Scope (for this draft)

- Final API contract and DTO definitions.
- Migration tooling for legacy ZIP packages (see [Decision #9](#decisions--discovery) — none required in POC phase).
- Object-storage provider implementation (only its influence on the design is considered here).
