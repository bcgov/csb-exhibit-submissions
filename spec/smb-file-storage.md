# SMB Secure File Storage for Accepted Exhibits

**Status:** Stage 1 built — awaiting the VPN run that answers domain / share name / base path
**Date:** 2026-08-14 (updated 2026-08-17)
**JIRA:** TBD
**Reference implementation:** `C:\Development\bcgov\jasper\transitory-documents-api` (read-only, SMBLibrary)
**Supersedes nothing.** Extends [completed/accepted-file-storage.md](completed/accepted-file-storage.md).

---

## Overview

Accepted exhibits currently land on the API pod's own filesystem (`FileStorage:AcceptedPath`,
a Docker volume at `/data/accepted`). That is fine for local development and unacceptable as a
system of record: the pod's disk is ephemeral, unmanaged, unbacked-up, and outside whatever
retention and access controls the ministry applies to evidence.

This spec plans moving the **accepted store** onto a secure SMB file share, reached over the
network by the API using the [`SMBLibrary`](https://www.nuget.org/packages/SMBLibrary) NuGet
package (the same package the Jasper `transitory-documents-api` uses).

The goal is a new `IFileStorage` implementation. **No new interface methods are required** —
all seven existing members map onto SMB operations. Some small, additive refactors to shared
helpers are needed so the local and SMB implementations can share the parts that are not
filesystem-specific; these are enumerated in [Shared refactors](#shared-refactors) and none of
them change `LocalFileStorage`'s behaviour.

### Decisions taken (confirmed 2026-08-14)

| # | Decision |
|---|---|
| D1 | **Only the accepted store moves to SMB.** Pending uploads stay on the pod's local disk under `FileStorage:LocalPath`. |
| D2 | **Stage One ships a dev-only diagnostic endpoint** that connects, tree-connects, lists a probe folder and optionally reads one file. |
| D3 | **Downloads stream.** A purpose-built read-only `Stream` issues SMB reads on demand rather than buffering the whole exhibit into memory. |

---

## Environment — confirmed 2026-08-17

A read-only service account and a dev file server exist. Per the repo's no-secrets-in-specs rule the
concrete hostname, share, username and password are **not recorded here** — they live in
`docker/.env` (gitignored) locally and in an OpenShift secret when deployed. What is safe to record:

| Fact | Status | How it was established |
|---|---|---|
| Dev file server hostname resolves | ✅ | DNS lookup **while connected to the ministry VPN**. Fails to resolve off-VPN — expect that, it is not a config error. |
| TCP 445 (DirectTCP) reachable from the developer workstation | ✅ | `Test-NetConnection -Port 445` succeeded over VPN. |
| TCP 139 (NetBIOS) also reachable | ✅ | Available as a fallback; `DirectTcp` remains the default. |
| Workstation DNS suffix | `idir.bcgov` | Relevant to the domain question below. |
| Credentials | Read-only account in hand | Read + write + delete account still outstanding (Stage 3 only). |

### The one genuine unknown: which AD domain

The reference project logs into a `*.provjud.local` server with domain `PROVJUD`. Our dev server sits
in a **different DNS domain** (`idir.bcgov`), so `PROVJUD` is a plausible guess rather than a known
value — and a wrong domain surfaces as `STATUS_LOGON_FAILURE`, indistinguishable at a glance from a
bad password or from NTLM being disabled entirely ([Q2](#deferred-questions)).

This does **not** block Stage 1. It is resolved by running the diagnostic endpoint three times with
`FileStorage__Smb__Domain` set to `IDIR`, then `PROVJUD`, then empty (an empty domain makes NTLM
defer to the server's own domain). No rebuild — it is an environment variable. Whichever returns
`STATUS_SUCCESS` is the answer, and it gets recorded in `.env`, not here.

### Verified against SMBLibrary 1.5.3

Checked by reflecting the actual assembly, because three points in the original draft assumed an
API surface the package does not have:

| Capability | Reality |
|---|---|
| `SMB2Client.ListShares(out NTStatus)` | **Public.** Stage 1 can enumerate the server's shares, so a share name we do not know yet is discoverable rather than blocking. |
| `Connect(server, transport, responseTimeoutInMilliseconds)` | **Public.** `ConnectTimeoutMs` is implementable. |
| `Login(domain, user, password, AuthenticationMethod)` | **Public.** Enum is `NTLMv1`, `NTLMv1ExtendedSessionSecurity`, `NTLMv2` — confirming [Q2](#deferred-questions): NTLM only, no Kerberos. |
| Negotiated dialect / whether the session is encrypted | **Not public.** `m_dialect` and `m_encryptSessionData` are private instance fields. See the correction under [Stage 1](#stage-1--prove-we-can-connect-and-read). |
| `MaxReadSize` / `MaxWriteSize` / `MaxTransactSize` | **Public.** These are what the diagnostic can honestly report, and they bound `BufferSize`. |
| `INTFileStore` write primitives (`CreateFile`, `WriteFile`, `FlushFileBuffers`, `SetFileInformation`, `QueryDirectory`) | **All present**, so nothing in Stage 3 is blocked by the library. |

---

## Why not just port the Jasper implementation

The Jasper API is more complex than what CES needs, and almost all of that complexity comes from
one thing: **Jasper does not know where its files are.** It receives a region, a location, a date
and maybe a room, then has to *go find* matching folders on a share whose naming conventions it
does not control (`"10 OCTOBER/OCTOBER 31(Fri)"`, `"Courtroom 009"` vs `"R9"`). Everything
expensive in that codebase — wildcard prefix matching, multiple configurable date-folder formats,
room-code digit normalisation, parallel bounded-concurrency directory-tree traversal, the
region/location correction-mapping table — exists to solve *discovery*.

**CES has no discovery problem.** The API creates every folder and every filename itself, from
`AcceptedPathBuilder`, and stores the resulting `CanonicalPath` on the `StoredFiles` row. Every
read is an exact, known, self-authored path. That deletes most of the reference implementation:

| Jasper component | CES needs it? | Why |
|---|---|---|
| `ListFilesAsync`, `GetFileInfoAsync` | **No** | We never enumerate. Paths come from the DB. |
| `TraverseRoomDirectoriesParallelAsync`, `TraverseDirectoryTreeIterative`, `ConcurrentBag`, `SemaphoreSlim` fan-out | **No** | Nothing to traverse. |
| `ListFilesWithWildcardAsync`, `TryParseWildcardPath`, `DateFolderFormats` | **No** | No wildcards; `shortDate` is a sanitized segment we chose. |
| `NormalizeRoomCode` / `IsRoomMatch` (regex digit matching) | **No** | `roomCode` is sanitized by `AcceptedPathBuilder.SanitizeSegment` and written by us. |
| `CorrectionMappingOptions` (region/location renames) | **No** | Not applicable. |
| Case-sensitivity workaround (README §CorrectionMappings) | **No** | We only ever open names we created; the server does the matching. |
| `ISmbClient` + `ISmbClientFactory` + `SmbClientService` (three types to wrap `SMB2Client`) | **Collapse to one** | One `ISmbSessionFactory` returning a disposable `SmbSession`. |
| Polly `AsyncRetryPolicy` around whole operations | **Narrow it** | See [Retry policy](#retry-policy) — blanket retry around a *write* is dangerous. |
| `SmbConnection` (connect/login/tree-connect, disposable) | **Yes, keep** | Correct lifecycle model; borrow near-verbatim. |
| `OpenSmbPath` (CreateFile + NTStatus→exception mapping) | **Yes, keep** | Extended with write dispositions. |
| `SmbPathUtility.GetSmbPath` / `CombinePath` | **Yes, keep a subset** | ~30 lines instead of 185. |
| Buffered read loop (`ReadFileToStreamAsync`) | **Replace** | Per D3, becomes a lazy `Stream` rather than a `MemoryStream` fill. |

What is left is roughly: a session factory, a session, a path helper, a read stream, a write
helper, and the `IFileStorage` implementation itself — six small files, plus the diagnostic
endpoint.

**What Jasper does *not* cover at all: writing.** Everything in `SmbFileSystemClient` opens with
`AccessMask.GENERIC_READ` / `CreateDisposition.FILE_OPEN`. Directory creation, file creation,
chunked writes, rename and delete are all new ground for us, and are the subject of Stage 3.

---

## Current behaviour being preserved

Nothing about the **path layout** changes. `AcceptedPathBuilder` already produces an
OS-agnostic, forward-slash-joined relative path from sanitized segments:

```
{locationId}/{roomCode}/{shortDate}/{submissionId}/{exhibitId}{ext}
{locationId}/{roomCode}/{shortDate}/{submissionId}/metadata.json
```

On SMB that becomes, with separators flipped and the configured base path prepended:

```
\\{Server}\{ShareName}\{BasePath}\{locationId}\{roomCode}\{shortDate}\{submissionId}\{exhibitId}{ext}
```

Segments are already lowercased alphanumerics + `-` only (`SanitizeSegment`), so there are no
spaces, no case ambiguity, no traversal characters and no illegal Windows filename characters to
worry about. Path traversal is impossible **by construction**, not by validation — which is why
the SMB path resolver needs far less defensive code than
`AcceptedPathBuilder.ResolveAndVerifyWithinRoot` does.

Also unchanged:

- Pending uploads: `{LocalPath}/{locationId}/{roomCode}/{shortDate}/{submissionId}/{guid}{ext}` on pod disk.
- Users never choose a location or a name. `SubmissionService` and `AcceptedPathBuilder` decide both.
- One submission → one accepted folder → single-instance bytes for a multi-ticket exhibit.
- `StoredFiles.CanonicalPath` / `Sha256` / `AcceptedFileName` remain the DB source of truth.
- SHA256 verification before the pending copy is deleted.

---

## Architecture

### The one new class

**Completed 2026-08-17, ahead of the SMB work.** The original draft proposed a single
`SmbFileStorage : IFileStorage` that implemented the accepted half over SMB and delegated the
three pending methods to an injected `LocalFileStorage`. That hybrid is gone: the store has been
split so the two halves are configured and implemented independently.

```
IFileStorage                          ← unchanged; still what the services consume
   └── FileStorageCoordinator         ← CES.Business/FileStorage
         ├── IPendingFileStore        ← Save / Get / Delete / Exists
         │     └── LocalPendingFileStore
         └── IAcceptedFileStore       ← TryGetExisting / Promote / WriteMetadata / GetAccepted
               ├── LocalAcceptedFileStore
               └── SmbAcceptedFileStore   ← Stage 3, three of the four members
```

Providers are chosen by `FileStorage:PendingProvider` and `FileStorage:AcceptedProvider`, so
`Local`/`Local` and `Local`/`Smb` are both just configuration. What this changed for the SMB work:

- **The SMB class implements three or four members, not seven**, and has no dependency on the
  local store. Delegation and the "hybrid" concept disappear entirely.
- **`PromoteToAcceptedAsync` takes the pending bytes as a `Stream`.** The coordinator opens it from
  whichever pending store is configured. This is the decoupling that makes any pairing work — the
  accepted store never reaches into `FileStorage:LocalPath`.
- **`DeletePendingCopyAsync` lives in the coordinator**, so the verification sequence (canonical
  hash vs DB hash vs pending hash) is written once and holds for every pairing rather than being
  reimplemented per provider. Its cheap length gate now applies only when both streams report a
  length, so a store that cannot is skipped rather than assumed good.
- **`TryGetExistingAsync`** was added to `IAcceptedFileStore` to keep promotion idempotent without
  requiring the pending copy to still exist — it is deleted after a successful acceptance, so a
  re-run must not depend on it.

`ChunkFileStorage.cs` — an unregistered stub of seven `NotImplementedException`s — was deleted in
the same change. It implemented `IFileStorage` directly, which is now the coordinator's role, so
keeping it would have advertised the wrong extension point.

### Supporting types

```
api/CES.API/FileStorage/Smb/
├── SmbOptions.cs           # bound from FileStorage:Smb (see Configuration)
├── ISmbSessionFactory.cs   # one seam, for tests + the diagnostic endpoint
├── SmbSessionFactory.cs    # connect → login → tree-connect
├── SmbSession.cs           # IDisposable: exposes ISMBFileStore; disconnect/logoff on dispose
├── SmbPath.cs              # relative "a/b/c" → "Base\a\b\c"; parent-chain enumeration
├── SmbReadStream.cs        # lazy read-only Stream; owns its session
└── SmbFileWriter.cs        # EnsureDirectory / WriteStream / Rename / Delete / Exists
api/CES.API/FileStorage/SmbAcceptedFileStore.cs   # IAcceptedFileStore, 4 members
```

Already in place from the provider split:

```
api/CES.Business/Constants/FileStorageProviders.cs
api/CES.Business/Interfaces/IPendingFileStore.cs
api/CES.Business/Interfaces/IAcceptedFileStore.cs
api/CES.Business/FileStorage/FileStorageCoordinator.cs
api/CES.API/FileStorage/LocalPendingFileStore.cs
api/CES.API/FileStorage/LocalAcceptedFileStore.cs
api/CES.API/FileStorage/FileStorageRegistration.cs   # provider switch + legacy-key guard
```

### Connection lifecycle

One session per operation, disposed at the end — Jasper's model, and the right one. SMBLibrary's
`SMB2Client` is not thread-safe and sharing one across concurrent requests causes cross-operation
interference.

The one exception is `GetAcceptedExhibitAsync`: `SmbReadStream` **owns its session for the life
of the stream**, because the stream is consumed by ASP.NET after the controller action returns.
It disposes the session in `Stream.Dispose()`, which ASP.NET calls when the response completes.

That means a concurrent download holds an SMB session for the duration of the download. A 200 MB
video on a slow client could hold one for minutes. `Smb:MaxConcurrentSessions` (default 16) gates
this with a semaphore, released on stream disposal; over the cap, the request waits then fails
with a 503-mapped exception rather than exhausting the file server's session table.

> **Open risk.** If a client abandons a download mid-stream, ASP.NET should still dispose the
> stream — but this needs to be verified under test, because a leaked session also leaks a
> semaphore slot and the leak is cumulative. Stage 2 must include an abandoned-download test.

### Retry policy

Jasper wraps entire operations in a Polly retry. For reads that is harmless. For a **write** it
is not: retrying a partially-completed multi-chunk write can produce a truncated or
double-written file, and the operation is not idempotent.

Proposal — no Polly dependency, and retry only where it is provably safe:

- **Connect / login / tree-connect:** retry up to `Smb:MaxRetryAttempts` (default 3) with
  exponential backoff from `Smb:InitialRetryDelayMs` (default 1000). Establishing a session has
  no side effects.
- **Read operations** (`GetAcceptedExhibitAsync`, hash verification): retry the *whole* operation
  from a fresh session. Reads are idempotent.
- **Write operations** (`PromoteToAcceptedAsync`, `WriteMetadataAsync`): **no automatic retry.**
  On failure, delete the `.tmp` and throw. The pending copy is still intact, the DB has not been
  updated, and the operation is safely repeatable by the user re-triggering acceptance.

This is ~25 lines of helper instead of a package reference.

### Atomicity on SMB

`LocalFileStorage` writes `{exhibitId}{ext}.tmp`, verifies it, then `File.Move(overwrite: true)`.
The same shape is achievable over SMB2 via `SetFileInformation` with `FileRenameInformationType2`
(`ReplaceIfExists = true`), which SMBLibrary exposes.

> **Verify in Stage 3.** SMBLibrary's rename support against the actual target server needs
> hands-on confirmation. If server-side rename is unavailable or the account lacks `DELETE`
> access on the source handle (rename requires it), the fallback is: write directly to the final
> name, verify, and delete the file on verification failure. That is a strictly weaker guarantee
> — a crash mid-write leaves a partial file at the canonical path — so it should be a last resort,
> and if we take it, `PromoteToAcceptedAsync`'s "already accepted, don't re-copy" idempotency
> check must also compare size before trusting an existing file.

Directory creation has no `mkdir -p` over SMB. `SmbFileWriter.EnsureDirectoryAsync` walks the
parent chain and issues `CreateFile` with `FILE_DIRECTORY_FILE` + `CreateDisposition.FILE_OPEN_IF`
for each segment, ignoring "already exists". For our five-deep path that is at most five extra
round trips per submission folder, only on the first promotion into it.

### Integrity verification cost

Today's flow, per accepted exhibit, does: read pending (hash) → write accepted → read accepted
(verify) → later, read accepted again + read pending again (`DeletePendingCopyAsync`). With the
accepted side on SMB, that is **one upload and two full downloads over the network** for every
exhibit — 300 MB of traffic for a 100 MB video.

Recommendation: **keep full verification.** Integrity is the entire point of a system of record,
and the alternative is deleting the only other copy of an exhibit on an unverified assumption.
Mitigations that do not weaken the guarantee:

- Hash the accepted copy **once** during promotion and pass that result forward, so
  `DeletePendingCopyAsync` re-reads the canonical file only (one download, not two). This is a
  behaviour change to a deliberately-paranoid design, so it is raised as [Q7](#questions-for-validation).
- Expose `Smb:VerifyAfterWrite` (default `true`) as an escape hatch for a share that turns out to
  be too slow, rather than hard-coding the trade-off.

---

## Shared refactors

All additive. None change `LocalFileStorage` behaviour; each exists because a helper currently
assumes `System.IO`.

| # | File | Change | Status |
|---|---|---|---|
| R1 | `CES.Business/Services/CryptographicService.cs` | Add `ComputeSHA256Async(Stream)`; the existing `(string filePath)` overload opens the file and calls it. | ✅ **Done** |
| R3 | `CES.Business/FileStorage/AcceptedPathBuilder.cs` | Add `BuildSubmissionFolderRelativePath(...)`. `BuildCanonicalRelativePath` now delegates to it, so the folder path and its canonical prefix cannot drift. | ✅ **Done** — also removed the inline duplicate that was in `WriteMetadataAsync` |
| R5 | `CES.API/Program.cs` | Replaced the hard-coded `AddScoped<IFileStorage, LocalFileStorage>()` with `AddFileStorage(configuration)`, which switches on both provider settings. | ✅ **Done** — the "`Provider` is read then ignored" trap is gone |
| R2 | `CES.Business/FileStorage/AcceptedMetadataWriter.cs` | Extract `byte[] Serialize(AcceptedMetadata)`. `WriteAsync` keeps its signature and calls it. | Stage 3 — the SMB writer needs the bytes, not a local temp+rename |
| R4 | `CES.API/configuration/StorageOptions.cs` | Add `public SmbOptions Smb { get; set; }`. | ✅ **Done** — also bound on its own as `IOptions<SmbOptions>` so the SMB types don't take the whole storage config |
| R6 | `CES.API/CES.API.csproj` | `<PackageReference Include="SMBLibrary" Version="1.5.3" />` | ✅ **Done** |

`IFileStorage` itself is **unchanged** — which is why `InMemoryFileStorage`, both `Mock<IFileStorage>`
suites and all 389 backend tests passed the provider split without a single assertion changing.

---

## Staged delivery

### Stage 0 — Prerequisites ✅ complete

Read-only account obtained, hostname resolves, port 445 reachable. See
[Environment](#environment--confirmed-2026-08-17).

### Stage 1 — Prove we can connect and read ✅ built 2026-08-17

The minimum that proves the network path, the credentials and the config binding, without touching
`IFileStorage` at all.

**Shipped:**
- `SMBLibrary` 1.5.3 package reference (R6).
- `SmbOptions` + binding under `FileStorage:Smb` (R4). `Password` is `[JsonIgnore]`d and excluded
  from `ToString()`, so it cannot reach a diagnostic response or a log line by accident.
- `SmbConstants` / `SmbException` / `SmbPath` / `SmbSession` / `ISmbSessionFactory` /
  `SmbSessionFactory`, plus `ISmbDiagnosticsService` / `SmbDiagnosticsService` and the
  `SmbHealthResponse` shape.
- `GET /api/dev/smb/health` on `DeveloperController`, gated to the admin role **and** to
  Development-or-`DiagnosticsEnabled`. It returns **404** when disabled rather than 403, so it does
  not advertise itself.
- `FileStorage__Smb__*` in `docker-compose.yaml` (both `api` and `api-dev`) and `.env.template`,
  pulled forward from Stage 4 because the exit criteria are measured from inside the container.

**Deviations from the draft above, all deliberate:**
- **`ISmbSessionFactory` has two entry points**, not one: `ConnectAsync` (connect + login) and
  `OpenShareAsync` (+ tree connect). The diagnostic genuinely needs the half-built form — with the
  share name unknown, `ListShares` runs on a logged-in session that was never tree-connected, and
  that is how the share name gets discovered.
- **Retry is narrower than "connect / login / tree-connect".** `SmbException.IsRetryable` is true
  only when the server never answered (`Status is null`). A returned `NTStatus` is a real answer —
  a wrong domain or a wrong share is not transient, and retrying it just triples the wait before the
  operator sees the actual reason.
- **`MaxConcurrentSessions` is enforced in the factory now**, not deferred to Stage 2. The slot is
  an `IDisposable` handed to the `SmbSession`, so it is released by the same `Dispose()` that tears
  the session down — which is exactly the property the Stage 2 abandoned-download test needs.
- **The session factory is a singleton** (the semaphore is process-wide); the sessions it hands out
  are per-operation and disposed by the caller.
- **SMB infrastructure is registered regardless of `AcceptedProvider`.** The point of the diagnostic
  is to prove the share works *before* switching the accepted store onto it. Nothing contacts the
  network at boot.

The endpoint is deliberately **progressive**: each step runs only if the previous one succeeded, and
every step reports its own outcome. One call should tell us exactly how far we got, because the
open unknowns (domain, share name, base path) each fail at a different step.

```jsonc
// GET /api/dev/smb/health
{
  "steps": {
    "connect":       { "ok": true,  "elapsedMs": 42 },
    "login":         { "ok": true,  "status": "STATUS_SUCCESS", "domain": "IDIR", "method": "NTLMv2" },
    "listShares":    { "ok": true,  "status": "STATUS_SUCCESS",
                       "shares": ["IPC$", "…"] },        // discovery — see below
    "treeConnect":   { "ok": true,  "status": "STATUS_SUCCESS", "share": "…" },
    "listBasePath":  { "ok": true,  "status": "STATUS_SUCCESS",
                       "basePath": "…", "entries": ["…"] },   // first N names
    "probeRead":     { "ok": true,  "bytes": 1024, "sha256": "A1B2C3…", "elapsedMs": 88 }
  },
  "negotiated": { "maxReadSize": 1048576, "maxWriteSize": 1048576, "maxTransactSize": 1048576 },
  "elapsedMs": 412
}
```

Notes on the shape, each of which is load-bearing:

- **`listShares` is the answer to a share name we do not have.** `SMB2Client.ListShares` is public
  and needs only the login, not a tree connect. If `ShareName` is unconfigured or wrong, the
  endpoint still gets this far and hands us the list. (It goes through `IPC$`/SRVSVC, so a hardened
  server may return `STATUS_ACCESS_DENIED` here while normal share access works fine — that is
  informative, not fatal, and the endpoint must continue to `treeConnect` regardless.)
- **`listBasePath` doubles as base-path discovery.** With `BasePath` empty it lists the share root,
  which is how we work out where the accepted root should sit.
- **Raw `NTStatus` on every step, never flattened.** `STATUS_LOGON_FAILURE` (wrong domain, wrong
  password, or NTLM disabled), `STATUS_ACCESS_DENIED` (authenticated but not permitted),
  `STATUS_BAD_NETWORK_NAME` (wrong share) and `STATUS_OBJECT_PATH_NOT_FOUND` (wrong base path) are
  four entirely different conversations, and collapsing them into "connection failed" is how a
  half-day gets lost.
- **No `dialect` or `encrypted` field.** The original draft promised both; SMBLibrary 1.5.3 keeps
  `m_dialect` and `m_encryptSessionData` private, so neither is observable through the public API.
  `negotiated.*` sizes are reported instead — they are public, they differ by dialect, and
  `BufferSize` must be clamped to them anyway. If knowing the dialect turns out to matter, the
  dev-only endpoint may read the private fields by reflection, clearly marked best-effort; nothing
  outside the diagnostic may depend on it.

Listing is used **only** here. The production paths never enumerate.

**Exit criteria:** the endpoint returns `login.ok`, `treeConnect.ok` and `listBasePath.ok` from
inside the running API container, with the working domain and share recorded in `.env`.

### Stage 2 — Read path

**Ships:**
- `SmbReadStream` (D3): lazy, seekable-for-range-requests, owns its session, semaphore-gated.
- `SmbAcceptedFileStore` with `GetAcceptedExhibitAsync` implemented against SMB; the three write
  members throw `NotSupportedException` until Stage 3.
- Registering it in `FileStorageRegistration` so `FileStorage__AcceptedProvider=Smb` selects it,
  replacing the `NotImplementedException` placeholder.

Validated by seeding the share manually (files placed by hand, or by a `Local` run whose
`AcceptedPath` output is copied up) and then downloading through the normal
`FilesController` endpoints. This proves the real read path with the real DB `CanonicalPath`
values — not just the diagnostic.

**Exit criteria:** an admin can view and download an accepted exhibit end-to-end from the UI, and
a large video seeks correctly (range requests).

### Stage 3 — Write path

Blocked on a service account with write + delete on the share.

**Ships:**
- `SmbFileWriter`: `ExistsAsync`, `EnsureDirectoryAsync`, `WriteAsync(Stream)`, `RenameAsync`, `DeleteAsync`.
- `TryGetExistingAsync`, `PromoteToAcceptedAsync` and `WriteMetadataAsync` on
  `SmbAcceptedFileStore`. (`DeletePendingCopyAsync` needs nothing new — it already runs in the
  coordinator against whatever the accepted store returns.)
- R2 (`AcceptedMetadataWriter.Serialize`).
- Extension of `/api/dev/smb/health` with an opt-in `?write=true` that writes, verifies, reads
  back and deletes a scratch file under `{BasePath}/_diagnostics/`, reporting each step's
  `NTStatus`. This is how we will diagnose partial permissions (a very common outcome: create
  granted, delete denied).

**Exit criteria:** classifying an exhibit as Marked promotes the bytes to the share, writes
`metadata.json` beside them, verifies the hash, and removes the pending local copy — with the DB
`CanonicalPath`/`Sha256` matching what is on the share.

### Stage 4 — Operationalise

- `docker/.env.template` + `docker-compose.yaml` entries for every `FileStorage__Smb__*` value.
- Password sourced from an OpenShift secret, never from `appsettings.json`. Confirm it is absent
  from logs — `SmbOptions.ToString()` must be overridden or the type marked so it is never
  serialized into a diagnostic response.
- Startup validation: when `Provider=Smb`, fail fast at boot on missing `Server`/`ShareName`/
  `Username`/`Password`/`BasePath` rather than at the first acceptance.
- A local **Samba container** in `docker-compose` (see [Testing](#testing)) so the write path is
  developable and CI-testable without the real share.
- Runbook note: what to do when `DeletePendingCopyAsync` logs a verification failure (the
  existing `FileService` behaviour — pending copy retained, needs a human).

---

## Configuration

All values bind under `FileStorage:Smb`, i.e. `FileStorage__Smb__<Name>` as an environment
variable.

### Values you need to obtain

Status as of 2026-08-17. The two ⚠️ rows are **not** blocking asks — Stage 1's diagnostic endpoint
discovers both. Nothing here needs to be chased with the file-services team before we start.

| Setting | Example | Notes |
|---|---|---|
| `FileStorage__Smb__Server` | *(dev host — in `.env`)* | ✅ **Have it.** Hostname or FQDN. Must resolve **and** be reachable from the API pod. Not a UNC path — no leading `\\`. |
| `FileStorage__Smb__ShareName` | `example_dev$` | ⚠️ **Not yet known.** Share name only, no server, no sub-path. `$` suffix (hidden share) is fine. Discoverable via the Stage 1 `listShares` step. |
| `FileStorage__Smb__Domain` | `IDIR` \| `PROVJUD` \| *(empty)* | ⚠️ **Unconfirmed** — see [Environment](#environment--confirmed-2026-08-17). Determined by running the Stage 1 endpoint. |
| `FileStorage__Smb__Username` | *(service account — in `.env`)* | ✅ **Have it** (read-only). **Read + write + delete** account still needed for Stage 3. |
| `FileStorage__Smb__Password` | *(secret)* | ✅ **Have it.** OpenShift secret. Never committed, never logged. |
| `FileStorage__Smb__BasePath` | `CES/accepted` | ⚠️ **Not yet known.** Path **inside** the share, above `{locationId}`. May be empty if the share root is the accepted root; leave it empty in Stage 1 to list the root and find out. |

### Values with defaults (tune later, no need to obtain)

| Setting | Default | Notes |
|---|---|---|
| `FileStorage__PendingProvider` | `Local` | Only `Local` is supported — pending uploads stay on pod disk by design (D1). |
| `FileStorage__AcceptedProvider` | `Local` | Set to `Smb` to move the accepted store onto the share. Throws at boot until Stage 3 lands. |
| `FileStorage__Smb__TransportType` | `DirectTcp` | Port 445. `NetBios` (139) is also open on the dev host if 445 is ever blocked. |
| `FileStorage__Smb__AuthenticationMethod` | `NTLMv2` | `NTLMv1ExtendedSessionSecurity` and `NTLMv1` are the other options. A cheap thing to vary if `NTLMv2` returns `STATUS_LOGON_FAILURE`. |
| `FileStorage__Smb__BufferSize` | `65536` | 64 KiB read/write chunk. Clamped to the negotiated `MaxReadSize`/`MaxWriteSize`. |
| `FileStorage__Smb__MaxConcurrentSessions` | `16` | Semaphore cap; mainly bounds in-flight downloads. |
| `FileStorage__Smb__ConnectTimeoutMs` | `10000` | |
| `FileStorage__Smb__MaxRetryAttempts` | `3` | Session establishment and reads only. |
| `FileStorage__Smb__InitialRetryDelayMs` | `1000` | Exponential backoff base. |
| `FileStorage__Smb__VerifyAfterWrite` | `true` | Read-back SHA256 verification. See [Q7](#questions-for-validation). |
| `FileStorage__Smb__ProbeFile` | *(empty)* | Stage 1 only: a small file under `BasePath` the health endpoint reads. |
| `FileStorage__Smb__DiagnosticsEnabled` | `false` | Gates `/api/dev/smb/health` independently of `ASPNETCORE_ENVIRONMENT`. |

`FileStorage__LocalPath` and `FileStorage__MaxFileSize` keep their current meaning.
`FileStorage__AcceptedPath` becomes unused when `AcceptedProvider=Smb`.

> **`FileStorage__Provider` was removed.** A single provider could not express a `Local` pending +
> `Smb` accepted pairing. Because .NET config binding ignores unknown keys silently, a deployment
> still setting the old key would have looked configured while doing nothing — so
> `FileStorageRegistration` throws at boot if it sees `FileStorage:Provider`, naming the two
> replacements in the message.

> **Dropped: `RequireEncryption`.** The original draft proposed failing closed unless SMB 3.x
> encryption was negotiated. SMBLibrary 1.5.3 exposes no such switch and no way to read back
> whether the session is encrypted, so the setting could not be honoured — a config knob that
> silently does nothing is worse than no knob. Encryption is negotiated by the server, and if the
> transport needs to be provably encrypted that is an infrastructure requirement on the share
> rather than something this API can assert ([Q4](#deferred-questions)).

---

## Testing

> Per the project testing rule: **this section is a proposal.** No tests will be written until you
> confirm the functionality and the coverage below.

> **Stage 1 tests are deliberately deferred (decided 2026-08-17).** They will be written *after* the
> first VPN run against the real share, not before. The diagnostic's response shape and its
> step-gating exist to answer questions we have not asked the server yet; if what the server says
> forces a change, tests written now would be written twice. What is queued for that pass:
> `SmbPath` (join, empty `BasePath`, separator normalisation, traversal guards, ancestor chain),
> `SmbOptions` transport/auth parsing and password redaction, `SmbDiagnosticsService` against a
> mocked `ISmbSessionFactory` (step gating, skip reasons, nothing secret in the output), and the
> `/api/dev/smb/health` gate returning 404 / 403 / 200. All of it was exercised by hand for this
> build; none of it is guarded by a regression test yet.

### Existing coverage that must keep passing

`CES.API.Tests/Fixtures/InMemoryFileStorage.cs` implements `IFileStorage` for integration tests.
Because `IFileStorage` does not change, it needs no modification — the whole point of not adding
interface methods. `FileServiceTests` and `SubmissionServiceTests` mock `IFileStorage` and are
likewise unaffected. This held in practice: the provider split landed with all 389 backend tests
green and no assertion edited.

`LocalFileStorageTests` was renamed `LocalFileStorageCoordinatorTests` and now constructs
`FileStorageCoordinator(LocalPendingFileStore, LocalAcceptedFileStore)` behind an `IFileStorage`
reference. Its 15 cases are otherwise untouched, and they now cover the `Local`/`Local` pairing
end to end rather than one class.

### New unit tests (no network)

| Target | Cases |
|---|---|
| `SmbPath` | relative → SMB join; empty `BasePath`; leading/trailing separator normalisation; forward→back slash conversion; parent-chain enumeration for `EnsureDirectory`. |
| `SmbAcceptedFileStore` (mocked `ISmbSessionFactory`) | `GetAcceptedExhibitAsync` throws `FileNotFoundException` when `IsAccepted` is false or `CanonicalPath` is null (parity with `LocalAcceptedFileStore`); `TryGetExistingAsync` returns null for an absent canonical file rather than throwing. |
| `FileStorageCoordinator` (mocked halves) | Pending methods never touch the accepted store; `PromoteToAcceptedAsync` short-circuits on `TryGetExistingAsync` without opening the pending stream; `DeletePendingCopyAsync` returns `VerificationFailed` on each mismatch and never calls `DeleteAsync`; the length gate is skipped when a stream cannot report `Length`. |
| `CryptographyService` (R1) | Stream and file-path overloads agree on the same bytes. |
| `AcceptedMetadataWriter` (R2) | `Serialize` output is byte-identical to what `WriteAsync` puts on disk. |
| `FileStorageRegistration` | Legacy `FileStorage:Provider` throws at boot; unknown provider names throw; `Local`/`Local` resolves an `IFileStorage`. |
| Retry helper | Retries connect failures up to the cap; does **not** retry writes. |

### New integration tests (against a Samba container)

Since no real write account exists, add a `samba` service to `docker-compose.yaml` (e.g.
`ghcr.io/servercontainers/samba`) exposing a scratch share with a known user. These tests are
skipped unless `FileStorage__Smb__Server` is set, so they no-op on a developer machine that has
not started it.

| Scenario | Assertion |
|---|---|
| Round trip | Promote a pending file → the exhibit exists on the share at the DB `CanonicalPath` with a matching SHA256. |
| Idempotent promotion | Promoting twice does not re-copy and returns the same hash. |
| Directory creation | A five-deep path is created from an empty share root. |
| Atomic write | No `.tmp` remains on success. |
| Failed write | A write interrupted before verification leaves the canonical path absent and the pending copy intact. |
| Metadata refresh | `WriteMetadataAsync` twice leaves exactly one valid `metadata.json`. |
| Streaming read | A file larger than `BufferSize` reads back byte-identical; a range request returns the right slice. |
| Session hygiene | Disposing the read stream releases the semaphore; N sequential downloads do not exhaust it. |
| Pending cleanup | `DeletePendingCopyAsync` deletes the local copy only after the SMB hash matches; a corrupted canonical file yields `VerificationFailed` and the pending copy survives. |

### Manual validation steps (for you)

1. **Stage 1.** Connect to the ministry VPN first — without it the hostname does not resolve.
   Copy the `FileStorage__Smb__*` block out of `.env.template` into `.env` and fill in `Server`,
   `Username`, `Password` and `Domain`; leave `ShareName`, `BasePath` and `ProbeFile` empty on the
   first run. Then `./manage debug`.

   The endpoint requires the **Admin** role, so it cannot be opened straight from the address bar.
   The `smb` folder of the [Bruno collection](../Bruno/bcgov/smb/) does the token handling: run
   **Login (dev bypass)** once (it needs `username` / `password` on the active environment set to
   `admin@gov.bc.ca`, and stashes the JWT in a runtime variable), then **SMB health**
   after each `.env` change. Its assertions are this stage's exit criteria, and its post-response
   script prints the step-by-step transcript plus a suggested next move. Failing that, sign in at
   http://localhost:9080, lift the bearer token and
   `curl -H "Authorization: Bearer <token>" http://localhost:9080/api/dev/smb/health`.
   A **404** means the gate rejected it (not Development, and `DiagnosticsEnabled` not set); a
   **401/403** means the token is missing or not an admin's.

   - If `login.ok` is false, re-run with `FileStorage__Smb__Domain` set to `IDIR`, then `PROVJUD`,
     then empty — it is an environment variable, so `./manage stop && ./manage debug` is enough, no
     rebuild. `login.domain` echoes back which value the run actually used. Only if all three fail
     on all three `AuthenticationMethod` variants does [Q2](#deferred-questions) fire.
   - Read the share name out of `steps.listShares.shares`, put it in `.env`, re-run. If `listShares`
     itself returns `STATUS_ACCESS_DENIED`, that is informative rather than fatal — the run still
     continues to `treeConnect`, and the share name has to come from the file-services team instead.
   - Read the folder layout out of `steps.listBasePath.entries`, decide `BasePath`, re-run.
     `truncated: true` means the folder holds more names than the diagnostic reports.
   - Optionally set `FileStorage__Smb__ProbeFile` to a small file under `BasePath` and re-run to
     exercise a real read (`probeRead` reports bytes + SHA256, capped at 1 MiB).
   - Confirm the password appears nowhere in the response or in `docker logs`.
2. **Stage 2.** Place a known file on the share at a path matching an existing `StoredFiles`
   row's `CanonicalPath`, set `FileStorage__AcceptedProvider=Smb`, restart, then view and download
   that exhibit from the admin UI. Scrub a video partway through to exercise range requests.
3. **Stage 3.** `GET /api/dev/smb/health?write=true` — confirm the scratch file is created,
   verified, read back and deleted. Then upload a new exhibit and classify it Marked. Confirm on
   the share: the exhibit file appears under `{loc}\{room}\{date}\{subId}\`, `metadata.json` sits
   beside it and is readable, and the pending copy is gone from `/data/uploads`.
4. **Failure drill.** Point `Server` at an unreachable host and confirm acceptance fails cleanly
   with the pending copy retained and a useful log line — not a 500 with the exhibit lost.

---

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **SMBLibrary is NTLM-only** — confirmed by reflection; no Kerberos. | If the share mandates Kerberos, this entire approach fails. | [Q2](#deferred-questions). Stage 1's `login` step is the test; it is the first thing the endpoint will tell us. |
| **SMBLibrary does not follow DFS referrals.** | A DFS namespace path silently fails or resolves nowhere. | [Q3](#deferred-questions). Unlikely given a concrete hostname. |
| Wrong AD domain guessed for the service account | `STATUS_LOGON_FAILURE`, easily misread as bad credentials or as Q2 firing. | Try `IDIR` / `PROVJUD` / empty via env var before escalating. The endpoint echoes which domain it used. |
| VPN required to reach the share from a developer workstation | DNS does not resolve off-VPN; looks like a broken config. | Documented in [Environment](#environment--confirmed-2026-08-17); the `connect` step failing with a DNS error is the tell. |
| Server-side rename unsupported / `DELETE` access not granted | Loses write atomicity. | Fallback documented in [Atomicity on SMB](#atomicity-on-smb); detected by the Stage 3 `?write=true` probe. |
| Verification triples network traffic per exhibit | Slow acceptance for large videos. | [Q7](#questions-for-validation); `VerifyAfterWrite` escape hatch. |
| Long-lived sessions during large downloads | File server session exhaustion. | `MaxConcurrentSessions` semaphore; abandoned-download disposal test. |
| Share unavailable at acceptance time | An exhibit cannot be promoted. | The existing design already handles this correctly — DB commits after promotion, pending copy is only deleted after verification. Failure is safe and retryable. |
| No write account for an indefinite period | Stage 3 stalls. | Samba container makes the write path fully developable and testable in the meantime. |

---

## Questions for validation

**Q1 — Environment and read-only account: answered ✅.** Recorded in
[Environment](#environment--confirmed-2026-08-17). Stage 1 is cleared to build.

### Deferred questions

Confirmed 2026-08-17: rather than pre-clearing these with infrastructure, we build Stage 1 and let
it tell us. Most of them are *observations the diagnostic endpoint makes for free*, and chasing
answers up front would cost more than running the endpoint once. Each row below records the symptom
that reactivates the question, so a failure gets diagnosed rather than re-investigated.

| # | Question | Reactivated by | If it fires |
|---|---|---|---|
| Q2 | Kerberos vs NTLM. Confirmed: SMBLibrary is **NTLM-only** (`AuthenticationMethod` = NTLMv1 / NTLMv1ExtendedSessionSecurity / NTLMv2). | `login.status = STATUS_LOGON_FAILURE` on **all three** domain values *and* all three NTLM variants. | Escalate to file services: is NTLM disabled? If so this approach is dead and the fallback is a platform-performed CIFS mount treated as a local path. **This is still the one risk that can invalidate the design.** |
| Q3 | DFS namespace. The dev hostname is a concrete host, not a domain-shaped namespace root, so almost certainly not DFS. | `treeConnect` succeeds but paths resolve nowhere, or `STATUS_PATH_NOT_COVERED`. | Ask for the concrete target server; SMBLibrary does not follow referrals. |
| Q4 | SMB 3.x encryption. Not assertable from the client — see the note under [Configuration](#configuration). | A security review asking whether exhibit bytes are encrypted in transit. | Requirement lands on the share/infrastructure, not this API. |
| Q5 | Port 445 egress. ✅ confirmed from the developer workstation over VPN. **Still unconfirmed from the OpenShift namespace** — a different network path entirely. | First deployment attempt times out at `connect`. | Raise a network-policy request with platform services. |
| Q6 | Write account rights: create, write, **delete**, directory-create on `{BasePath}` and below. Partial grants are the common failure. | Stage 3 start, or the `?write=true` probe returning `STATUS_ACCESS_DENIED` on one specific step. | Ask for all four explicitly. Until then the Samba container covers development. |
| Q8 | Records retention on the share — see below. | Before go-live. | — |
| Q9 | Local fallback on share outage — see below. | A stakeholder objecting that acceptance can be blocked by a network fault. | — |

### Still open for your call (design, not infrastructure)

**Q7 — May `DeletePendingCopyAsync` trust the hash computed during promotion?**
Currently it re-reads the canonical file and re-hashes it immediately before deleting the pending
copy, deliberately re-verifying rather than trusting the earlier promotion in the same request.
On SMB that is a second full download of, potentially, a 100 MB video. Reusing the promotion hash
removes one download per exhibit but weakens a safeguard that was written on purpose. My
recommendation is to **keep the re-read** for now and revisit only if acceptance is measurably too
slow — but it is your call, since it is your integrity guarantee.

**Q8 — Retention and cleanup on the share: whose job?**
The existing design never deletes an accepted exhibit (immutable once accepted). Once these files
live on a managed ministry share, does a records-retention process act on them independently, and
does anything need to be written into `metadata.json` to support it? *(Out of scope for this
build; asking so we do not paint ourselves into a corner.)*

**Q9 — Should `AcceptedPath` (local) keep working as a fallback?**
When `Provider=Smb`, should a share outage fall back to writing locally and reconcile later, or
should acceptance simply fail? My recommendation is **fail** — a silent split-brain between two
accepted stores is worse than a visible error, and the current design already fails safely. But
if traffic court cannot tolerate acceptance being blocked by a network issue, say so now, because
that changes the design materially.
