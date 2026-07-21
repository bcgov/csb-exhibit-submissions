# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CSB Exhibit Submissions (CES) is a BC Gov full-stack monorepo:
- **Frontend:** Vue 3 + TypeScript + Vite + Vuetify 4 (`/web`)
- **Backend:** ASP.NET Core 10 (`/api`)
- **Database:** PostgreSQL 16

## Development Commands

### Running the Stack (Docker — primary workflow)

```bash
cd docker
./manage debug       # Start all services with hot-reload (web-dev + api-dev + db)
./manage start       # Start production build
./manage stop        # Stop services
./manage build       # Build all images
./manage down        # Remove containers and volumes
```

Services when running with `./manage debug`:
- Frontend: http://localhost:9080 (Vite dev server with hot-reload)
- API: exposed through web nginx at `/api` path
- PostgreSQL: port 5432

### Frontend (`/web`)

```bash
npm run dev          # Vite dev server (port 5173, proxies /api → ASP.NET)
npm run build        # Type-check + production build
npm run type-check   # vue-tsc type checking only
npm run lint         # Run oxlint + eslint (both with auto-fix)
npm run format       # Prettier format src/
```

Requires Node ^20.19.0 or >=22.12.0.

### Backend (`/api`)

```bash
dotnet run --project CES.API          # Run API (port 5285)
dotnet watch --project CES.API        # Run with hot-reload
dotnet build                          # Build solution
dotnet test api/CES.API/CES.API.sln  # Run all backend tests (29 tests)
```

Migrations run automatically on startup. PostgreSQL must be running.

### Testing

```bash
# Backend (78 tests: 39 unit in CES.Business.Tests + 39 integration in CES.API.Tests)
dotnet test api/CES.API/CES.API.sln

# Frontend (46 tests across 8 test files: stores, services, components)
cd web && npm run test

# Frontend with coverage
cd web && npm run test:coverage

# Frontend watch mode
cd web && npm run test:watch
```

## Architecture

### Frontend Structure

```
web/src/
├── main.ts              # Entry point — registers plugins, mounts app
├── App.vue              # Root component: auth check, routing, nav layout
├── components/
│   ├── admin/           # Admin-only views
│   ├── officer/         # Officer/user views
│   └── shared/          # Reusable components
├── router/              # Vue Router (route definitions + auth guards)
├── stores/              # Pinia state (auth, application state)
├── services/            # Axios API clients + AuthService (JWT)
├── models/              # TypeScript interfaces matching API contracts
├── helpers/             # Utility functions
└── plugins/             # Vuetify setup, other Vue plugins
```

The Vite dev server proxies all `/api` requests to the ASP.NET backend (`localhost:5285`). In Docker, nginx handles this routing.

Authentication uses JWT Bearer tokens managed by `AuthService`. Routes are protected via Vue Router navigation guards.

### Backend Structure

```
api/
├── CES.API/             # ASP.NET entry point — controllers, middleware, DI setup
├── CES.Business/        # Business logic layer
├── CES.EF/              # Entity Framework Core DbContext + migrations
├── CES.Entities/        # Domain entity models
└── jc-interface-client/ # External JC interface integration
```

`Program.cs` configures JWT auth, CORS, EF Core (Npgsql/PostgreSQL), Swagger, and local file storage. CORS allows `localhost:9080`, `localhost:5285`, `localhost:5173`.

The API exposes controllers for: Login/Logout, Users, Submissions, Review, Files, Locations, and a Developer endpoint.

**Exception handling:** `ApiExceptionMiddleware` (registered in `Program.cs`) translates unhandled exceptions from services/controllers into HTTP status codes centrally, so controllers can `throw` instead of hand-rolling error responses. The mapping is:

| Exception | Status |
|---|---|
| `KeyNotFoundException`, `FileNotFoundException`, `DirectoryNotFoundException` | `404 Not Found` |
| `ArgumentException` | `400 Bad Request` |
| `InvalidOperationException` | `409 Conflict` |
| any other `Exception` | `500 Internal Server Error` |

The body is always `{ "message": "…" }` (generic text for 500). When adding a service method, throw the exception that maps to the status you want rather than returning ad-hoc error results.

### Key Integration Points

- **File uploads:** Dropzone on the frontend, max 100MB, stored locally by the API under the path `{locationId}/{shortDate}/{roomCode}/{submissionId}` (submission-scoped, not per-ticket)
- **Email:** SMTP configuration in `appsettings.json`
- **BC Gov design system:** BC Gov design tokens and fonts are imported for consistent styling

### BC Gov Design Tokens (styling reference)

Styling is driven by the official [`@bcgov/design-tokens`](https://www2.gov.bc.ca/gov/content/digital/design-system/foundations/design-tokens/glossary) package (`web/package.json`). Read this before writing or changing any SCSS.

**How the tokens are organized upstream** (~200 tokens, grouped by type + intended usage):

| Type | Purpose | Example token names (SCSS, non-prefixed) |
|---|---|---|
| **Surface** | Colour palettes/effects for styling UI elements | `$surface-color-primary-button-default`, `$surface-color-border-default`, `$surface-color-background-light-gray`, `$surface-shadow-medium` |
| **Support** | Colours for messaging (status/alerts/warnings) | `$support-surface-color-danger`, `$support-border-color-success`, `$support-surface-color-warning` |
| **Layout** | Sizing/spacing measures (mostly unitless) | `$layout-padding-medium`, `$layout-margin-large`, `$layout-border-radius-medium`, `$layout-border-width-small` |
| **Typography** | Typescale values | `$typography-color-primary`, `$typography-color-link`, `$typography-font-size-body`, `$typography-font-size-label` |
| **Icons** | Icon sizing/colour (unitless sizes) | `$icons-size-small`, `$icons-size-medium`, `$icons-size-large` |
| **Theme** | Base palette (gray scale, etc.) | `$theme-gray-20`, `$theme-gray-30` |

Naming reads left→right as `type[-subtype]-role-variant-state` (e.g. `surface-color-primary-button-hover`). Each token ships in a **prefixed** (`BCDS`/`bcds-` namespace, avoids collisions) and **non-prefixed** (matches Figma handoff) form; this repo uses the **non-prefixed** SCSS/CSS variants. Some tokens (layout/icon sizes) are **unitless** and need a `px`/unit suffix when consumed. Package is semver'd — treat major bumps as potentially breaking.

**How this repo consumes them — always go through the aliases:**
- CSS custom properties are loaded globally in `web/src/main.ts` (`@bcgov/design-tokens/css/variables.css`).
- SCSS tokens are wrapped in [`web/src/styles/_variables.scss`](web/src/styles/_variables.scss), which `@use`s the package as `t` and re-exports semantic aliases (`$color-primary`, `$padding-medium`, `$font-size-body`, `$border-radius-default`, …). **Use these aliases in style partials**, not raw hex or magic numbers (see Code Style rules).
- If no alias exists for a token you need, reference it directly as `t.$<token-name>` and add a new alias in `_variables.scss` rather than inlining a literal value.

## Environment Setup

Copy `docker/.env.template` to `docker/.env` and fill in required values before running Docker services. The devcontainer (`.devcontainer/`) is the recommended environment — it includes .NET 10 SDK, Node 22, Docker-in-Docker, and all required VS Code extensions.

## Feature Specs

Feature specifications live in [`/spec`](spec/). Read the relevant spec before implementing a feature.

| Spec | Description |
|---|---|
| [multi-ticket-exhibit-upload.md](spec/multi-ticket-exhibit-upload.md) | Officer selects multiple tickets on Court Search; one exhibit upload is linked to all of them. Requires new `SubmissionTickets` table and changes to the submit API contract. |
| [testing-implementation.md](spec/testing-implementation.md) | Initial testing strategy for backend (xUnit + Moq + WebApplicationFactory) and frontend (Vitest + MSW). Defines project structure, NuGet/npm packages, test cases, and CI integration. |
| [exhibit-classification.md](spec/exhibit-classification.md) | Officers classify each uploaded exhibit as Marked (A–Z) and/or Entered (1–50) at direction of the JJ. Tracks classification timestamps, enforces a state machine, and supports ticket-number retrieval of exhibit history across court sessions. |
| [admin-listing-update.md](spec/admin-listing-update.md) | Reworks the admin Submission Listing/Review: explicit `Pending`/`Accepted`/`Rejected` submission lifecycle (replaces `IsDeleted` overloading), historical view of all submissions, a search/filter panel, admin-editable exhibit classification, Accept gated on all-exhibits-final, and whole-submission Reject with a destructive warning. |
| [exhibit-search.md](spec/exhibit-search.md) | (CES-38) New admin landing page: JJ searches exhibits by file number (partial, 5-char min) or accused last name with an optional court-date range. Returns a flat, Marked→Entered→Unclassified-ordered exhibit list rendered via the shared `ExhibitList.vue` in `alwaysEditable` mode (view/download/edit/history in one screen). Replaces the old Submission Listing in nav; new `GET /api/submissions/exhibit-search` endpoint. |
| [exhibit-descriptions.md](spec/exhibit-descriptions.md) | (CES-42) Replaces the single mutable exhibit `Description` field with append-only, immutable description entries (same shape as registry `ExhibitNote`): multiline plain text, never edited, addenda only. Streamlines `ExhibitList.vue` into a collapsed single-row view with a chevron, and opens the Exhibit Detail modal to officers (without the Notes section). |
| [documents/component-rules.md](spec/documents/component-rules.md) | Reference: BC Gov Design System component rules (Buttons, Text field, Text area, Select, Dialog, Tags/Chips, Date picker) adapted for CES's native-HTML-+-SCSS approach. Universal a11y rules (focus ring, target sizes, labels), per-component states/variants, and a mapping to existing SCSS tokens. Read before styling or building a control. |
| [documents/typography-rules.md](spec/documents/typography-rules.md) | Reference: BC Gov typography standards mapped to CES. The sanctioned type scale (H1–H6 + body/small-body/label sizes, weights, line-heights) with their design tokens, the rules (rem-only, weights 400/700 only, min 16px body, sequential headings), and an audit of current off-scale sizes/weights to fix. Read before setting any `font-size`/`font-weight`/heading. |
| [keycloak-authentication.md](spec/keycloak-authentication.md) | (CES-36-2) BC Gov Keycloak/IDIR SSO login against a **confidential** client. The client secret is required, so the SPA cannot run the flow: the API initiates Authorization Code + PKCE, performs the code exchange and all refreshes, and keeps the refresh token in a Data Protection-encrypted `HttpOnly` cookie. Keycloak's registered redirect URI is the Vue route `/auth/callback`, which posts the code to the API rather than handling it. The browser holds only an in-memory access token, auto-renewed ahead of expiry (plus a one-shot `401` retry) so long file uploads never lapse. Supersedes the rejected [keycloak-simplified.md](spec/completed/keycloak-simplified.md) (public client, no secret) and [keycloak-integration.md](spec/completed/keycloak-integration.md). |

---

# Project Rules

## Testing

- **Write tests for all new development.** Every new service method, controller action, store mutation, and service function must have corresponding tests before the work is considered complete.
- notify user before writing any tests so they can verify functionality and completeness.
- **Update existing tests when modifying existing code.** If a change alters behavior covered by an existing test, update that test to reflect the new spec — do not delete or skip tests to make them pass.
- Run `dotnet test api/CES.API/CES.API.sln` and `cd web && npm run test` before marking any task done. Both must pass.
- See [spec/testing-implementation.md](spec/testing-implementation.md) for the full testing strategy, framework choices, project structure, and test case inventory.

## Code Style
- **`AppearanceId` casing:** use `appearanceId` in TypeScript/JSON/form keys and `AppearanceId` in C# properties. Never reintroduce the old `appearanceID` / `AppearanceID` variants — these were normalized as part of the multi-ticket work and the casing is now consistent across the codebase. Everything under `api/jc-interface-client/` (generated NSwag client) is excluded from this rule.
- Never hardcode configuration values, prices, rates, or magic numbers inline.
- All such values must be defined in a constants file or loaded from environment variables.
- If you introduce a numeric literal that isn't obvious (e.g. not `0`, `1`, `100`), extract it to a named constant with a comment explaining the source.
- Ensure type safety for all functions and variables.