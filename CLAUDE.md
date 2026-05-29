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
dotnet test                           # Run all tests
```

Migrations run automatically on startup. PostgreSQL must be running.

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

### Key Integration Points

- **File uploads:** Dropzone on the frontend, max 100MB, stored locally by the API
- **Email:** SMTP configuration in `appsettings.json`
- **BC Gov design system:** BC Gov design tokens and fonts are imported for consistent styling

## Environment Setup

Copy `docker/.env.template` to `docker/.env` and fill in required values before running Docker services. The devcontainer (`.devcontainer/`) is the recommended environment — it includes .NET 10 SDK, Node 22, Docker-in-Docker, and all required VS Code extensions.

## Feature Specs

Feature specifications live in [`/spec`](spec/). Read the relevant spec before implementing a feature.

| Spec | Description |
|---|---|
| [multi-ticket-exhibit-upload.md](spec/multi-ticket-exhibit-upload.md) | Officer selects multiple tickets on Court Search; one exhibit upload is linked to all of them. Requires new `SubmissionTickets` table and changes to the submit API contract. |
| [testing-implementation.md](spec/testing-implementation.md) | Initial testing strategy for backend (xUnit + Moq + WebApplicationFactory) and frontend (Vitest + MSW). Defines project structure, NuGet/npm packages, test cases, and CI integration. |

---

# Project Rules

## Testing

- **Write tests for all new development.** Every new service method, controller action, store mutation, and service function must have corresponding tests before the work is considered complete.
- **Update existing tests when modifying existing code.** If a change alters behavior covered by an existing test, update that test to reflect the new spec — do not delete or skip tests to make them pass.
- Run `dotnet test api/CES.API/CES.API.sln` and `cd web && npm run test` before marking any task done. Both must pass.
- See [spec/testing-implementation.md](spec/testing-implementation.md) for the full testing strategy, framework choices, project structure, and test case inventory.

## Code Style
- Never hardcode configuration values, prices, rates, or magic numbers inline.
- All such values must be defined in a constants file (e.g. `constants.py`, `config.py`) or loaded from environment variables.
- If you introduce a numeric literal that isn't obvious (e.g. not `0`, `1`, `100`), extract it to a named constant with a comment explaining the source.
- Ensure type safety for all functions and variables.