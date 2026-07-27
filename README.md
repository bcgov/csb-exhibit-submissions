# csb-exhibit-submissions

# Running the Stack (Docker — primary workflow)
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
# Backend (29 tests: 12 unit + 17 integration)
dotnet test api/CES.API/CES.API.sln

# Frontend (29 tests: stores, services, components)
cd web && npm run test

# Frontend with coverage
cd web && npm run test:coverage

# Frontend watch mode
cd web && npm run test:watch
```