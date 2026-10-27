# Tabiji

A collaborative travel planner. Create a trip, invite your friends, propose
activities, vote on them, discuss the details, and settle on a shared itinerary.

Tabiji plans trips anywhere in the world. Its visual identity is Japanese travel
editorial — warm paper, forest green, a Fraunces display serif and the 旅 monogram —
and it is drawn entirely in type and line, with no photography anywhere in the product.

This repository contains a .NET 10 modular monolith, an Angular 22 standalone SPA and a
SQL Server database. The `Trip` slice is complete end to end: a trip can be created from the
browser, listed, and opened, with an editorial interface built around it.

> **Status:** Phase 0.8 of the roadmap. See [`docs/roadmap.md`](docs/roadmap.md) for what
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
| `PUT` | `/api/trips/{id}` | Replaces a trip and its route. |
| `GET` | `/openapi/v1.json` | The OpenAPI document. |

List the trips:

```bash
curl http://localhost:5080/api/trips
```

```json
[
  {
    "id": "c47a8f9c-ba3a-4e0e-b1db-dc175473c7fa",
    "name": "Japan Adventure",
    "destinations": [
      {
        "id": "6d343873-45bc-42c6-abc2-5c271f99354a",
        "name": "Osaka",
        "sortOrder": 0,
        "startsOn": "2027-05-16",
        "endsOn": "2027-05-20",
        "latitude": null,
        "longitude": null,
        "stops": []
      },
      {
        "id": "3c21be5e-0868-4a3e-91c6-64b72892eaa3",
        "name": "Kyoto",
        "sortOrder": 1,
        "startsOn": "2027-05-20",
        "endsOn": "2027-05-24",
        "latitude": null,
        "longitude": null,
        "stops": []
      },
      {
        "id": "6df575e9-6e69-4c8a-b55e-52573a32168f",
        "name": "Ise",
        "sortOrder": 2,
        "startsOn": null,
        "endsOn": null,
        "latitude": null,
        "longitude": null,
        "stops": []
      }
    ],
    "startsOn": "2027-05-16",
    "endsOn": "2027-06-05",
    "status": "Planning"
  }
]
```

Create a trip — destinations are the main route, and each may hold stops:

```bash
curl -X POST http://localhost:5080/api/trips \
  -H "Content-Type: application/json" \
  -d '{"name":"Japan Adventure","startsOn":"2027-05-08","endsOn":"2027-05-29","destinations":[{"name":"Osaka","startsOn":"2027-05-08","endsOn":"2027-05-12","stops":[{"name":"Namba","category":"Food","address":"Namba, Osaka","notes":"Eat here.","plannedOn":"2027-05-09"},{"name":"Umeda"}]},{"name":"Kyoto","startsOn":"2027-05-13","endsOn":"2027-05-17","stops":[{"name":"Gion"}]}]}'
```

Change a trip — the request holds the whole trip, the order of `destinations` is the main
route, and the order of each `stops` array is the order within that destination:

```bash
curl -X PUT http://localhost:5080/api/trips/c47a8f9c-ba3a-4e0e-b1db-dc175473c7fa \
  -H "Content-Type: application/json" \
  -d '{"name":"Japan Adventure","startsOn":"2027-05-08","endsOn":"2027-05-29","destinations":[{"id":"6d343873-45bc-42c6-abc2-5c271f99354a","name":"Osaka","startsOn":"2027-05-08","endsOn":"2027-05-12","stops":[{"id":"…","name":"Namba"},{"id":null,"name":"Tennoji"}]},{"id":null,"name":"Nagoya","startsOn":null,"endsOn":null,"stops":[]}]}'
```

A destination with an `id` is a destination that already exists: it is renamed,
rescheduled and moved to its new position, and it keeps its identity. A destination with
`"id": null` is new. A destination the request leaves out is removed with its stops. The
same rules apply to the entries in `stops`: an `id` keeps a stop, `null` adds one, an
omitted stop is removed, and the array order is the order within the destination. A stop
named under a different destination is moved there and keeps its identity and its place
details (category, address, notes, planned day). A date sent as `null` is cleared.

A stop is the place. It carries its own order inside its parent, optional stay dates and
coordinates, and an optional category, address, notes and planned day. It is labelled
hierarchically (`1a`, `1b`, ... `1aa`) in the Atlas and connects to the other stops of its
destination with a separate line; it is never a numbered main-route pin.

`PUT` also accepts an optional `unfiledPlaces` array for legacy saved places that are not
yet in a destination. Omitting the array leaves them alone; naming one under a destination
files it there with the same identity. The old saved-place write routes are retired; the
read-only `GET /api/trips/{tripId}/places` remains for one release.

A trip holds an **ordered route** of destinations, at least one and at most twenty. Each
name is free text of up to 200 characters: it is never parsed and never compared against a
list of countries, so any place in the world is accepted.

Each destination also carries the **optional days of its stay**. Both may be left out, so a
place can be added before its dates are known. Every date must fall inside the trip's own
dates, a destination may not end before it starts, and the route must be able to be
visited in the order it is given. The order is the traveller's: a route whose dates
contradict it is refused with a message naming both stops, never silently reordered.

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
dotnet test backend/Tabiji.sln   # 351 tests
cd frontend && npm test           # 454 tests
```

The backend suite contains domain and use-case unit tests plus API integration tests that
boot the real host in memory. **Almost no test needs a running database** — the liveness
probe performs no I/O by design, the readiness test asserts a well-formed report rather
than a specific outcome, and the route table is checked against the EF model rather than
against a server.

The exception is the repository suite, which writes a route to a real database, because
that is the one thing that cannot be checked any other way. Those tests are marked with
`[DatabaseFact]` and are **skipped** when SQL Server LocalDB cannot be reached, so the
suite still runs on a machine with no database. They use a database of their own, created
with the real migrations and dropped again afterwards.

The frontend suite covers the root component, the two API clients, the rules, the route
motif, the destination plate, the shared editor and every screen.

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
