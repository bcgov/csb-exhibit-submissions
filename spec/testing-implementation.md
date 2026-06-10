# Testing Implementation Specification

**Status:** Complete  
**Date:** 2026-05-28  
**JIRA:** CES-33 

## Overview

This document defines the initial testing strategy for CES (CSB Exhibit Submissions). The project currently has no testing infrastructure. This spec establishes the frameworks, project structure, tooling, and test categories for both the ASP.NET Core backend and Vue 3 frontend.

**Goal:** Tests must be runnable with a single command (`dotnet test` or `npm run test`) so that automated validation of future development is possible without manual setup.

---

## Backend Testing (ASP.NET Core 10)

### Framework Choices

| Concern | Package | Rationale |
|---|---|---|
| Test runner & assertions | `xunit.v3` 3.2.2 | First-class .NET support, used by ASP.NET Core itself. Do not use v2, its in maintenance mode. |
| Mock generation | `Moq` 4.x | Standard .NET mock library, works with all interfaces. Version 4.20.72 is latest. `netstandard2.0` target — net10.0 compatible. |
| HTTP integration tests | `Microsoft.AspNetCore.Mvc.Testing` 10.x | `WebApplicationFactory<T>` spins up the real pipeline in-process. Must match project's net10.0 / ASP.NET Core 10 target. |
| In-memory database | `Microsoft.EntityFrameworkCore.InMemory` 10.x | Replaces PostgreSQL in integration tests, no Docker needed. Version must match project's EF Core 10.0.3. |
| Fluent assertions | `FluentAssertions` 8.x | More readable `result.Should().Be(x)` syntax. Latest version is 8.10.0. **Note:** FA 8.0+ uses a dual license — Apache 2.0 for open-source projects (this repo qualifies), commercial license otherwise. |

### Project Structure

Create two new .csproj projects inside `api/`:

```
api/
├── CES.Business.Tests/         # Unit tests — isolated service logic
│   ├── CES.Business.Tests.csproj
│   └── Services/
│       ├── SubmissionServiceTests.cs
│       ├── FileServiceTests.cs
│       └── PasswordServiceTests.cs
└── CES.API.Tests/              # Integration tests — full HTTP pipeline
    ├── CES.API.Tests.csproj
    ├── Fixtures/
    │   └── TestWebApplicationFactory.cs
    ├── Authentication/
    │   └── LocalTokenServiceTests.cs
    └── Controllers/
        ├── LoginControllerTests.cs
        ├── SubmissionsControllerTests.cs
        ├── FilesControllerTests.cs
        └── LocationsControllerTests.cs
```

Add both projects to `CES.API.sln`:

```bash
dotnet sln api/CES.API/CES.API.sln add api/CES.Business.Tests/CES.Business.Tests.csproj
dotnet sln api/CES.API/CES.API.sln add api/CES.API.Tests/CES.API.Tests.csproj
```

### CES.Business.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit.v3" Version="3.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.*" />
    <PackageReference Include="Moq" Version="4.*" />
    <PackageReference Include="FluentAssertions" Version="8.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\CES.Business\CES.Business.csproj" />
    <ProjectReference Include="..\CES.EF\CES.EF.csproj" />
  </ItemGroup>
</Project>
```

### CES.API.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit.v3" Version="3.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.*" />
    <PackageReference Include="Moq" Version="4.*" />
    <PackageReference Include="FluentAssertions" Version="8.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\CES.API\CES.API.csproj" />
  </ItemGroup>
</Project>
```

### WebApplicationFactory Fixture

`api/CES.API.Tests/Fixtures/TestWebApplicationFactory.cs` — configures the test host to use an in-memory SQLite/EF InMemory database and replace real external services (file storage, JC client) with test doubles:

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace PostgreSQL with EF InMemory
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<CESDataStore>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<CESDataStore>(options =>
                options.UseInMemoryDatabase("TestDb"));

            // Replace LocalFileStorage with a no-op stub
            services.AddScoped<IFileStorage, InMemoryFileStorage>();
        });
    }
}
```

A simple `InMemoryFileStorage` stub satisfies all four `IFileStorage` methods without touching disk: `SaveAsync` stores bytes in a `Dictionary<Guid, byte[]>`, `GetAsync` returns a `MemoryStream` for the stored bytes, `DeleteAsync` removes the entry, and `AcceptAsync` is a no-op.

### Unit Test Coverage — CES.Business

#### SubmissionServiceTests.cs

| Test | Behavior |
|---|---|
| `SubmitEvidence_PersistsSubmissionAndFiles` | Given a valid `EvidenceSubmissionModel` with 2 files, after calling `SubmitEvidence`, `DbContext.Submissions` has 1 record and `DbContext.StoredFiles` has 2 records. |
| `SubmitEvidence_CallsFileStorageSaveAsync` | Verifies `IFileStorage.SaveAsync` is called once per uploaded file. |
| `RetrieveSubmission_ReturnsModel_WhenExists` | Given a seeded `Submission` with files, `RetrieveSubmission(id)` returns a non-null `SubmissionReviewModel` with correct field values. |
| `RetrieveSubmission_ReturnsNull_WhenNotFound` | Given an unknown ID, returns `null`. |
| `RetrieveSubmissionListing_ExcludesDeleted` | Seeds 3 submissions (1 soft-deleted), expects listing to return only 2. |
| `AcceptSubmissions_MarksFilesDeleted` | Calls `AcceptSubmissions` with file IDs; verifies those `StoredFiles.IsDeleted == true`. |
| `RejectSubmissions_DeletesSubmissionAndFiles` | Calls `RejectSubmissions`; verifies submission is removed and `IFileStorage.DeleteAsync` is called for each file. |

#### PasswordServiceTests.cs

| Test | Behavior |
|---|---|
| `HashPassword_ReturnsDifferentHash_EachCall` | Two hashes of same input differ (BCrypt salt). |
| `VerifyPassword_ReturnsTrue_ForCorrectPassword` | Hash from `HashPassword` validates against original input. |
| `VerifyPassword_ReturnsFalse_ForWrongPassword` | Hash does not validate against different input. |

#### FileServiceTests.cs

| Test | Behavior |
|---|---|
| `RetrieveFileMetaData_ReturnsEntity_WhenExists` | Seeded `StoredFiles` record is returned by ID. |
| `RetrieveFileMetaData_ReturnsNull_WhenNotFound` | Unknown ID returns null. |

### Integration Test Coverage — CES.API

Integration tests use `TestWebApplicationFactory` and an `HttpClient` against the real middleware pipeline. JWT tokens for the test client are generated via `LocalTokenService` with a test signing key.

#### LocalTokenServiceTests.cs

`LocalTokenService` lives in `CES.API/Authentication/` and is referenced from `CES.API.Tests`.

| Test | Behavior |
|---|---|
| `GenerateToken_ReturnsValidJwt` | Token decodes without error and contains expected `role` and `sub` claims. |
| `GenerateToken_ExpiresAfterConfiguredDuration` | `exp` claim is in the future at generation time. |

#### LoginControllerTests.cs

| Test | Expected |
|---|---|
| `Login_WithValidCredentials_Returns200WithToken` | `POST /api/auth/login` with known admin creds → 200 + JWT body |
| `Login_WithInvalidCredentials_Returns401` | Unknown user → 401 |

#### SubmissionsControllerTests.cs

| Test | Expected |
|---|---|
| `Submit_WithUserRole_Returns200` | Authenticated User token + multipart form → 200 |
| `Submit_WithoutAuth_Returns401` | No token → 401 |
| `Submit_WithAdminRole_Returns403` | Admin token (wrong role) → 403 |
| `Retrieve_WithAdminRole_Returns200` | Admin token + valid fileId → 200 |
| `Retrieve_WithUserRole_Returns403` | User token → 403 |
| `Listing_WithAdminRole_Returns200WithList` | Admin token → 200 + JSON array |
| `Accept_WithAdminRole_Returns200` | Admin token + valid model → 200 |
| `Reject_WithAdminRole_Returns200` | Admin token + valid model → 200 |

#### FilesControllerTests.cs

| Test | Expected |
|---|---|
| `ViewFile_WithValidId_ReturnsFileStream` | Known file ID → 200 + binary content |
| `ViewFile_WithUnknownId_Returns404` | Unknown ID → 404 |
| `DownloadFile_WithValidId_Returns200WithDispositionHeader` | Known file ID → `Content-Disposition: attachment` header |

#### LocationsControllerTests.cs

Test the endpoint with a mocked `ILocationService` and `ICourtListService` to avoid hitting the real JC API.

Note: `GetCourtList` has an unexpected route — it is on `api/files/getCourtList`, not `api/location/`. Both actions are handled by `LocationsController`; the route paths are:
- `GET api/location/getLocations`
- `GET api/files/getCourtList`

| Test | Expected |
|---|---|
| `GetLocations_Returns200WithData` | `GET api/location/getLocations` with mocked locations → 200 + JSON body |
| `GetCourtList_Returns200WithData` | `GET api/files/getCourtList` with mocked court list → 200 + JSON body |

### Running Backend Tests

```bash
# From repo root
dotnet test api/CES.API/CES.API.sln

# With detailed output
dotnet test api/CES.API/CES.API.sln --logger "console;verbosity=normal"

# With coverage (add coverlet.collector package)
dotnet test api/CES.API/CES.API.sln --collect:"XPlat Code Coverage"
```

---

## Frontend Testing (Vue 3 + TypeScript + Vite)

### Framework Choices

| Concern | Package | Rationale |
|---|---|---|
| Test runner | `vitest` 4.1.7 | Native Vite integration, same config as `vite.config.ts`. Requires Vite >=6.0.0 (project uses 8.x ✅) and Node >=20.0.0 ✅. |
| Component mounting | `@vue/test-utils` 2.x | Official Vue 3 component testing library. Must be 2.x — 1.x is Vue 2 only. Compatible with `vue ^3.5.33` and `vue-router ^5.0.6`. |
| DOM environment | `jsdom` | Browser-like DOM in Node. Latest requires Node `^20.19.0 \|\| ^22.13.0 \|\| >=24.0.0`. The project engines field is already set to `>=22.13.0` ✅. |
| HTTP mock | `msw` 2.14.6 | Intercepts `axios` at the network level. Must be **2.x** — the handler syntax in this spec (`http.post`, `HttpResponse`) is the MSW 2 API; 1.x is incompatible. |
| Coverage | `@vitest/coverage-v8` 4.1.7 | V8 native coverage, zero config with Vitest. **Version must match vitest's major** (both 4.x) — mismatched majors cause runtime failures. |

### Installation

```bash
cd web
npm install --save-dev vitest @vitest/coverage-v8 @vue/test-utils jsdom msw
```

### Configuration

Add `vitest` config inside the existing `web/vite.config.ts`:

```ts
/// <reference types="vitest" />
export default defineConfig({
  // ...existing config...
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      include: ['src/**/*.ts', 'src/**/*.vue'],
      exclude: ['src/main.ts', 'src/plugins/**', 'src/assets/**'],
    },
  },
})
```

Add test scripts to `web/package.json`:

```json
{
  "scripts": {
    "test": "vitest run",
    "test:watch": "vitest",
    "test:coverage": "vitest run --coverage"
  }
}
```

### MSW Setup

`web/src/test/setup.ts` — runs before every test file:

```ts
import { setupServer } from 'msw/node'
import { handlers } from './handlers'

export const server = setupServer(...handlers)

beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
```

`web/src/test/handlers.ts` — default API mocks:

```ts
import { http, HttpResponse } from 'msw'

export const handlers = [
  http.post('/api/auth/login', () =>
    HttpResponse.json({ token: '<test-jwt>' })),
  http.get('/api/submissions/listing', () =>
    HttpResponse.json([])),
  http.post('/api/submissions/submit', () =>
    HttpResponse.json({ success: true })),
  http.get('/api/location/getLocations', () =>
    HttpResponse.json([])),
]
```

### File Structure

```
web/src/
├── test/
│   ├── setup.ts               # MSW server init, global beforeAll/afterAll
│   └── handlers.ts            # Default MSW request handlers
├── stores/__tests__/
│   ├── authStore.spec.ts
│   └── courtFileSelectionStore.spec.ts
├── services/__tests__/
│   ├── AuthService.spec.ts
│   └── SubmissionService.spec.ts
├── helpers/__tests__/
│   └── (one spec per helper module)
└── components/__tests__/
    ├── shared/
    │   ├── AutocompleteSelect.spec.ts
    │   └── FileDropZone.spec.ts
    └── LoginView.spec.ts
```

### Unit Test Coverage — Stores

#### authStore.spec.ts

| Test | Behavior |
|---|---|
| `isAuthenticated returns false when no token` | Fresh store → `isAuthenticated` is `false` |
| `setToken stores token and decodes user` | After `setToken(jwt)`, `user` contains decoded claims |
| `isAuthenticated returns true with valid token` | Non-expired token → `isAuthenticated` is `true` |
| `isAuthenticated returns false with expired token` | Token with past `exp` → `isAuthenticated` is `false` |
| `clearAuth resets state` | After `clearAuth()`, `token` is null, `user` is null |
| `hasRole returns true for matching role` | Token with role `Admin` → `hasRole('Admin')` is `true` |

#### courtFileSelectionStore.spec.ts

| Test | Behavior |
|---|---|
| `initial state is empty` | No selections on init |
| `selectFile adds to selection` | Adding a court file ID populates the selection |
| `deselectFile removes from selection` | Removing an ID updates the set |
| `clearSelection empties all` | After clear, selection is empty |

### Unit Test Coverage — Services

#### AuthService.spec.ts

| Test | Behavior |
|---|---|
| `login calls POST /api/auth/login with credentials` | MSW intercepts; verifies request body contains `username` and `password` |
| `login stores token in authStore on success` | After successful login, `authStore.token` is non-null |
| `login throws on 401` | MSW returns 401; expect rejection |
| `logout clears authStore` | After `logout()`, store is reset |

#### SubmissionService.spec.ts

| Test | Behavior |
|---|---|
| `submitExhibits sends multipart POST to /api/submissions/submit` | MSW intercepts; verifies `Content-Type: multipart/form-data` |
| `submitExhibits calls progressCallback` | Progress callback is invoked during upload |
| `retrieveSubmission fetches GET /api/submissions/retrieve?fileId=X` | Returns mocked submission object |
| `retrieveSubmissionListing fetches GET /api/submissions/listing` | Returns mocked array |
| `acceptSubmissionFiles sends POST /api/submissions/accept` | Request body matches model |
| `rejectAndCloseSubmission sends POST /api/submissions/reject` | Request body matches model |

### Component Test Coverage

#### AutocompleteSelect.spec.ts

| Test | Behavior |
|---|---|
| `renders with provided items` | Mounts with items prop; DOM contains expected option labels |
| `emits update:modelValue on selection` | Selecting an item emits the correct value |
| `displays placeholder text when empty` | No value → placeholder visible |

#### FileDropZone.spec.ts

| Test | Behavior |
|---|---|
| `renders drop target` | Component mounts without error |
| `emits files-added when files are dropped` | Simulate drop event; verify emitted payload |
| `displays file size error for oversized files` | File exceeding max → error message visible |

#### LoginView.spec.ts

| Test | Behavior |
|---|---|
| `renders username and password fields` | Both inputs present in DOM |
| `submit button calls AuthService.login` | Click submit → `AuthService.login` called with form values |
| `shows error message on login failure` | MSW returns 401 → error text visible |
| `redirects to home on successful login` | Successful login → `router.push` called |

### Running Frontend Tests

```bash
# From repo root
cd web && npm run test

# Watch mode (re-runs on file change)
cd web && npm run test:watch

# With coverage report
cd web && npm run test:coverage
```

---

## Coverage Targets

These are minimum thresholds to enforce once the initial test suite is established. Set them in `vitest.config` (frontend) and via CI quality gates (backend).

| Layer | Target |
|---|---|
| Backend — business services | 80% line coverage |
| Backend — controllers (integration) | All happy-path and auth-failure cases covered |
| Frontend — stores | 90% line coverage |
| Frontend — services | 80% line coverage |
| Frontend — components | Key interactions covered (no strict % initially) |

---

## CI Integration

Do not add yet — this will be implemented in a follow-up spec.

When ready, add these steps to `.github/workflows/`:

```yaml
# Backend
- name: Run backend tests
  run: dotnet test api/CES.API/CES.API.sln --logger "github;verbosity=normal"
  working-directory: .

# Frontend
- name: Install frontend deps
  run: npm ci
  working-directory: web

- name: Run frontend tests
  run: npm run test
  working-directory: web
```

---

## Implementation Order

1. **Backend unit tests first** — no dependencies, fastest feedback loop
   - Create `CES.Business.Tests` project
   - Implement `PasswordServiceTests` (pure logic, no DB)
   - Add `SubmissionServiceTests` with EF InMemory
   - Add `FileServiceTests`

2. **Backend integration tests second**
   - Create `CES.API.Tests` project
   - Implement `TestWebApplicationFactory` with `InMemoryFileStorage` stub
   - Add `LocalTokenServiceTests` (`LocalTokenService` is in `CES.API`, so it belongs here)
   - Add controller tests starting with `LoginControllerTests`
   - Add `SubmissionsControllerTests` (most critical path)

3. **Frontend stores and services**
   - Install Vitest + MSW; configure `vite.config.ts`
   - Write `authStore.spec.ts` (foundational — auth affects all other tests)
   - Write service specs with MSW handlers

4. **Frontend component tests last**
   - Add `@vue/test-utils` mounting for shared components
   - Add `LoginView.spec.ts`

---

## Notes for Claude

When validating new development, run:

```bash
# Verify backend
dotnet test api/CES.API/CES.API.sln --logger "console;verbosity=normal"

# Verify frontend
cd web && npm run test
```

Both commands must pass before a feature is considered complete. If tests fail, diagnose from the output — do not skip or comment out tests to make them pass. If a test is genuinely wrong (spec changed), update the test to match the new spec and document why in the commit message.
