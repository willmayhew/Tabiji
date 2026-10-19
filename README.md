# Tabiji

A collaborative Japan trip planner. Create a trip, invite your friends, propose
activities, vote on them, discuss the details, and settle on a shared itinerary.

This repository currently contains the **project foundation**: a .NET 10 modular
monolith, an Angular 22 standalone SPA, a SQL Server database, and one thin end-to-end
vertical slice (`Trip`) that proves every layer is wired together.

> **Status:** Phase 0 of the roadmap. See [`docs/roadmap.md`](docs/roadmap.md) for what
> comes next and [`docs/architecture.md`](docs/architecture.md) for how the code is put
> together.

---

## Stack

| | |
|---|---|
| Backend | ASP.NET Core 10 (minimal APIs), C# 13, EF Core 10, SQL Server |
| Tests | xUnit 2.9, VSTest, `Microsoft.AspNetCore.Mvc.Testing` |
| Frontend | Angular 22 (standalone components), TypeScript 6, signals, Vitest |
| Repository | Git monorepo |

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | **10.0.402** | Pinned by `global.json`. ASP.NET Core runtime 10.0.13. |
| Node.js | **22.22.3+ / 24.15+ / 26+** | The version range Angular 22 supports. |
| npm | 11+ | Ships with Node. |
| SQL Server | 2019+ or LocalDB | The dev setup uses LocalDB. |
| Git | Any recent version | |

Check your setup:

```powershell
dotnet --list-sdks
node -v
npm -v
git --version
```

---

## Repository layout

```
Tabiji/
├── backend/                    .NET solution (modular monolith)
│   ├── Tabiji.sln
│   ├── Directory.Build.props   shared compiler settings
│   ├── Directory.Packages.props central package versions
│   ├── Tabiji.Domain/          entities and invariants — no dependencies
│   ├── Tabiji.Application/     use cases and ports — no EF Core, no ASP.NET Core
│   ├── Tabiji.Infrastructure/  EF Core, repositories, DI
│   ├── Tabiji.Api/             minimal API host and composition root
│   └── Tabiji.Tests/           xUnit tests
├── frontend/                   Angular 22 standalone SPA
├── docs/
│   ├── architecture.md
│   └── roadmap.md
├── .github/copilot-instructions.md
├── global.json                 pins the .NET SDK
└── .config/dotnet-tools.json   pins the dotnet-ef tool
```

---

## Setup

### 1. Backend

```powershell
git clone <your-remote> tabiji
cd tabiji

dotnet tool restore          # installs the pinned dotnet-ef 10.0.13
dotnet build backend/Tabiji.sln
```

The development database is **SQL Server LocalDB** with Windows integrated
authentication. The connection string is already set in
`backend/Tabiji.Api/appsettings.Development.json` and contains no password.

Create the database and apply the migrations:

```powershell
dotnet ef database update --project backend/Tabiji.Infrastructure --startup-project backend/Tabiji.Api
```

Start the API:

```powershell
dotnet run --project backend/Tabiji.Api --launch-profile http
```

It listens on <http://localhost:5080>.

> **Pointing at another server?** Keep the credential out of the repository:
> ```powershell
> cd backend/Tabiji.Api
> dotnet user-secrets set "ConnectionStrings:Tabiji" "Server=...;Database=Tabiji;User Id=...;Password=...;"
> ```
> The `appsettings.json` connection string is intentionally empty so that a missing
> configuration fails loudly instead of silently using a default.

### 2. Frontend

```powershell
cd frontend
npm ci
npm start
```

Open <http://localhost:4200>. The Angular dev server proxies `/api` and `/health` to
`http://localhost:5080`, so the browser only ever talks to its own origin.

> **If `npm ci` installs nothing and `ng` is "not recognized":** your shell has
> `NODE_ENV=production` set, which makes npm skip devDependencies. Override it for the
> install:
> ```powershell
> $env:NODE_ENV = "development"   # PowerShell
> npm ci
> ```
> This is a local environment setting, not a project requirement.

---

## Commands

### Backend

```powershell
dotnet build backend/Tabiji.sln                  # build everything
dotnet test  backend/Tabiji.sln                  # run the test suite
dotnet run --project backend/Tabiji.Api --launch-profile http

# Migrations
dotnet ef migrations add <Name> --project backend/Tabiji.Infrastructure --startup-project backend/Tabiji.Api --output-dir Persistence/Migrations
dotnet ef database update --project backend/Tabiji.Infrastructure --startup-project backend/Tabiji.Api
dotnet ef migrations remove  --project backend/Tabiji.Infrastructure --startup-project backend/Tabiji.Api
```

### Frontend

```powershell
cd frontend
npm ci                # install
npm start             # dev server on http://localhost:4200
npm run build         # production build into frontend/dist/tabiji
npm test              # unit tests (Vitest)
```

---

## API

Base URL in development: `http://localhost:5080`.

| Method | Route | Description |
|---|---|---|
| `GET` | `/health` | Liveness probe. Does not touch the database. |
| `GET` | `/health/ready` | Readiness probe. Verifies the database is reachable. |
| `GET` | `/api/trips` | Lists every trip, ordered by start date and then by name. |
| `POST` | `/api/trips` | Creates a trip. |
| `GET` | `/api/trips/{id}` | Returns a single trip. |
| `GET` | `/openapi/v1.json` | The OpenAPI document. |

List the trips:

```bash
curl http://localhost:5080/api/trips
```

```json
[
  {
    "id": "c47a8f9c-ba3a-4e0e-b1db-dc175473c7fa",
    "name": "Tokyo & Kyoto",
    "destination": "Japan",
    "startsOn": "2027-04-01",
    "endsOn": "2027-04-14",
    "status": "Planning"
  }
]
```

Create a trip:

```bash
curl -X POST http://localhost:5080/api/trips \
  -H "Content-Type: application/json" \
  -d '{"name":"Tokyo & Kyoto","destination":"Japan","startsOn":"2027-04-01","endsOn":"2027-04-14"}'
```

```json
{
  "id": "c47a8f9c-ba3a-4e0e-b1db-dc175473c7fa",
  "name": "Tokyo & Kyoto",
  "destination": "Japan",
  "startsOn": "2027-04-01",
  "endsOn": "2027-04-14",
  "status": "Planning"
}
```

---

## Architecture at a glance

The backend is a **modular monolith**. Four projects, one deployable, and a dependency
rule that only ever points inwards:

```
Tabiji.Domain          no dependencies at all
      ↑
Tabiji.Application     use cases and ports; no EF Core, no ASP.NET Core
      ↑
Tabiji.Infrastructure  EF Core, repositories, DI
      ↑
Tabiji.Api             minimal API host, composition root
```

A request flows through the layers like this:

```
POST /api/trips
  → TripEndpoints            (Api)         bind and validate the request
  → TripService              (Application) enforce the rules, map to a DTO
  → ITripRepository          (Application) the port
  → TripRepository           (Infrastructure) the EF Core implementation
  → TabijiDbContext          (Infrastructure) EF Core
  → SQL Server
```

`Tabiji.Domain` has **no package references**. `Tabiji.Application` references exactly
one package, `Microsoft.Extensions.DependencyInjection.Abstractions`. That is what keeps
the business rules independent of EF Core and ASP.NET Core.

The dependency set is deliberately small: no MediatR, AutoMapper, FluentValidation,
Swashbuckle, Serilog or mocking library. Health checks, problem details and OpenAPI all
come from the ASP.NET Core shared framework.

Read [`docs/architecture.md`](docs/architecture.md) for the full picture, including the
decision records.

---

## Testing

```powershell
dotnet test backend/Tabiji.sln   # 23 tests
cd frontend && npm test           # 36 tests
```

The backend suite contains domain and use-case unit tests plus API integration tests that
boot the real host in memory. **No test needs a running database** — the liveness probe
performs no I/O by design, and the readiness test asserts a well-formed report rather than
a specific outcome.

The frontend suite covers the root component and the two API clients with
`HttpTestingController`.

---

## Configuration

| Setting | Where | Notes |
|---|---|---|
| `ConnectionStrings:Tabiji` | `appsettings.json` (empty), `appsettings.Development.json` (LocalDB) | Use user secrets or `ConnectionStrings__Tabiji` for other servers. |
| `Cors:AllowedOrigins` | `appsettings.json` | The Angular dev origin, `http://localhost:4200`. |
| `ASPNETCORE_ENVIRONMENT` | `launchSettings.json` | `Development` for the local profiles. |

No credential is committed to this repository.

---

## Development notes

- **VS Code.** Recommended extensions are listed in `.vscode/extensions.json`. Open the
  repository root so that `global.json` and `.editorconfig` apply.
- **Package versions** are centralised in `backend/Directory.Packages.props`. Bump a
  version there, not in the individual `.csproj` files.
- **The SDK version** is pinned in `global.json` with `rollForward=latestFeature`.
- **TLS.** The API does not redirect HTTP to HTTPS, because the Angular dev server
  proxies to it over plain HTTP. Terminate TLS at the reverse proxy in any real
  deployment. The `https` launch profile expects a trusted development certificate:
  `dotnet dev-certs https --trust`.

---

## Licence

Not yet specified. Add one before publishing.
