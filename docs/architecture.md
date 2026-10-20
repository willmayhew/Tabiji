# Tabiji — Architecture

## 1. What Tabiji is

Tabiji is a collaborative travel planner. Friends create a trip, get invited into it,
propose activities, vote on those proposals, discuss them, and settle on a shared
itinerary.

The product plans trips anywhere in the world. Its *identity* is Japanese travel
editorial — the Tabiji name, the 旅 monogram, Fraunces and Inter, warm paper and forest
green — but no part of the data or the domain assumes a trip is in Japan. The `destination`
is free text and has always been free text; see §5.1.

This document describes the technical foundation of the project. It describes what exists
today, not the finished product.

## 2. Goals and non-goals

**Goals**

- Keep business rules testable and independent of the database and the web framework.
- Keep the deployment surface small: one API, one SPA, one SQL Server database.
- Make every architectural decision visible and reversible.

**Non-goals (explicitly out of scope for now)**

- Microservices. Tabiji is a modular monolith.
- Authentication and authorisation. Not implemented yet; see the roadmap.
- Infrastructure as code, CI/CD pipelines, containerisation, cloud deployment.
- Real-time collaboration, notifications, offline support.

## 3. The modular monolith

The whole system is one deployable backend and one deployable frontend. The backend is
split into four .NET projects that are deployed together but separated by dependency rules.
This gives the testability of a layered design without the operational cost of services.

```
┌──────────────────────────────────────────────────────────────────┐
│  frontend/            Angular 22 standalone SPA                   │
│  ────────────────────────────────────────────────────────────────│
│  core/  →  features/  →  shared/                                 │
│  HTTP only, no knowledge of the database or the domain model.    │
└───────────────────────────────┬──────────────────────────────────┘
                                │  HTTP/JSON
┌───────────────────────────────▼──────────────────────────────────┐
│  backend/Tabiji.Api      ASP.NET Core minimal API                │
│  ────────────────────────────────────────────────────────────────│
│  Program.cs  ·  Endpoints/  ·  Health/                           │
│  Composition root. Translates HTTP into Application calls.       │
└───────────────────────────────┬──────────────────────────────────┘
                                │
┌───────────────────────────────▼──────────────────────────────────┐
│  backend/Tabiji.Application  Use cases and ports                │
│  ────────────────────────────────────────────────────────────────│
│  TripService  ·  ITripRepository (port)  ·  DTOs                 │
│  No EF Core. No ASP.NET Core.                                    │
└───────────────────────────────┬──────────────────────────────────┘
                                │
┌───────────────────────────────▼──────────────────────────────────┐
│  backend/Tabiji.Domain      Entities and invariants              │
│  ────────────────────────────────────────────────────────────────│
│  Trip  ·  TripStatus                                            │
│  No dependencies at all.                                         │
└───────────────────────────────┬──────────────────────────────────┘
                                │
┌───────────────────────────────▼──────────────────────────────────┐
│  backend/Tabiji.Infrastructure  EF Core and adapters             │
│  ────────────────────────────────────────────────────────────────│
│  TabijiDbContext  ·  TripRepository  ·  EF configurations        │
└───────────────────────────────┬──────────────────────────────────┘
                                │
                        ┌───────▼────────┐
                        │   SQL Server   │
                        └────────────────┘
```

## 4. The dependency rule

Project references are the only coupling between the layers, and they only ever point
inwards:

| Project | References | May depend on |
|---|---|---|
| `Tabiji.Domain` | *(none)* | The BCL only. Zero package references. |
| `Tabiji.Application` | `Domain` | `Domain`, DI abstractions. **No EF Core, no ASP.NET Core.** |
| `Tabiji.Infrastructure` | `Application` | `Application`, EF Core, SQL Server. |
| `Tabiji.Api` | `Application`, `Infrastructure` | The ASP.NET Core shared framework. |
| `Tabiji.Tests` | all four | Anything, because tests are allowed to cross layers. |

Consequences that are worth stating explicitly:

- `Tabiji.Domain` has no `PackageReference` at all. If domain logic ever needs a package,
  that is a signal the logic is in the wrong place.
- `Tabiji.Application` references exactly one package:
  `Microsoft.Extensions.DependencyInjection.Abstractions`. It is enough to expose an
  `AddApplication()` extension method, and it drags in neither EF Core nor ASP.NET Core.
- The `ITripRepository` interface lives in `Application`, not `Infrastructure`. The
  dependency therefore points inwards: the use case depends on an abstraction, and
  `Infrastructure` supplies the implementation.

### 4.1 Why the database health check lives in the Api project

`IHealthCheck` and `HealthCheckResult` ship in the `Microsoft.AspNetCore.App` shared
framework, not in `Microsoft.NETCore.App`. A plain class library therefore cannot
reference them without either an extra NuGet package or an ASP.NET framework reference.

`DatabaseHealthCheck` lives in `Tabiji.Api/Health/` so that:

- `Tabiji.Infrastructure` stays free of ASP.NET Core;
- no `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` package is needed;
- the probe still uses the real `TabijiDbContext`, because `Tabiji.Api` already references
  `Tabiji.Infrastructure`.

The composition root owns the wiring of the probe, which is exactly its job.

## 5. Backend

### 5.1 Domain

`Trip` is the aggregate root. It owns the invariants and exposes them through a factory
and a small set of behaviours:

```csharp
Trip.Create(name, destination, startsOn, endsOn, createdAt)
trip.Rename(name)
trip.Reschedule(startsOn, endsOn)
```

Invariants, all enforced in the domain and all covered by unit tests:

- the name is required and at most 100 characters;
- the destination is required and at most 200 characters;
- the trip cannot end before it starts;
- a new trip always starts in `Planning`.

**The destination is free text and is treated as such.** It is trimmed, checked for
blankness and length, and then stored as-is. Nothing in the domain, the DTOs, the
persistence mapping or the API parses it, splits it or compares it against a list of
countries, so a trip may be to `"Rome, Italy"`, `"Reykjavík, Ísland"` or `"東京, 日本"`
without anything else changing. The `nvarchar(200)` column holds any Unicode, and there
is no check constraint or foreign key on it.

Invalid input raises `ArgumentException` with the offending parameter name. The API maps
that to a `400` with an RFC 7807 problem document.

The `Trip` type has a private parameterless constructor and private setters so that EF
Core can materialise it without exposing a public mutation API.

### 5.2 Application

`TripService` is the use-case surface. It depends on the `ITripRepository` port and on
`TimeProvider` (registered as `TimeProvider.System`), which makes the creation timestamp
deterministic in tests.

`TripService` maps `Trip` to `TripResponse`. The response is a plain record, so no EF
type ever crosses the layer boundary and the wire contract is explicit.

### 5.3 Infrastructure

`TabijiDbContext` exposes `DbSet<Trip>` and applies the entity configurations found in
its own assembly. `TripConfiguration` maps the aggregate to the `Trips` table:

| Property | Column | Type |
|---|---|---|
| `Id` | `Id` | `uniqueidentifier` |
| `Name` | `Name` | `nvarchar(100)` |
| `Destination` | `Destination` | `nvarchar(200)` |
| `StartsOn` | `StartsOn` | `date` |
| `EndsOn` | `EndsOn` | `date` |
| `Status` | `Status` | `nvarchar(30)` |
| `CreatedAt` | `CreatedAt` | `datetimeoffset` |

`Status` is stored as a string rather than an integer so that the stored value stays
readable and adding an enum member cannot silently change the meaning of existing rows.

`TripRepository` is the only class that knows how a `Trip` is persisted. It is registered
against the `ITripRepository` abstraction, so the composition root is the only place that
knows the concrete implementation.

`ITripRepository.ListAsync` returns every trip ordered by start date and then by name.
The ordering is part of the port's contract, and the repository produces it with a SQL
`ORDER BY` rather than sorting in memory.

### 5.4 API

`Program.cs` is a thin composition root. It calls `AddApplication()`,
`AddInfrastructure(configuration)`, registers the health checks, the CORS policy and
OpenAPI, and then maps the endpoints. It contains no business logic.

Endpoints:

| Method | Route | Behaviour |
|---|---|---|
| `GET` | `/api/trips` | Lists every trip, ordered by start date and then by name. |
| `POST` | `/api/trips` | Creates a trip. `201` with the trip, `400` on invalid input. |
| `GET` | `/api/trips/{id}` | Returns a trip, or `404`. |
| `GET` | `/health` | Liveness. Never touches the database. |
| `GET` | `/health/ready` | Readiness. Verifies the database is reachable. |
| `GET` | `/openapi/v1.json` | The OpenAPI document. |

Enums are serialised as strings (`"Planning"`, not `0`) via a `JsonStringEnumConverter`,
so the wire format is stable and readable. Errors use `AddProblemDetails()` from the
shared framework, which produces RFC 7807 problem documents.

### 5.5 Health checks

Two probes, split on purpose:

- **`/health` (liveness)** answers "is this process running?". It performs no I/O, so it
  stays fast and cannot fail because of a database outage. This is the endpoint a
  container orchestrator or load balancer should poll.
- **`/health/ready` (readiness)** answers "can this instance serve traffic?". It calls
  `Database.CanConnectAsync` and reports `Healthy` or `Unhealthy`, returning `503` when
  the database cannot be reached.

The split means the integration test for liveness does not need a running SQL Server,
which keeps the test suite environment-independent.

## 6. Frontend

Angular 22, standalone components, strict TypeScript, signals, the new control flow
syntax, and the Vitest test runner. No NgModules.

```
frontend/src/
├── styles/
│   ├── tokens.css                       colour, type, space, radius, shadow
│   ├── base.css                         element defaults, focus ring, reduced motion
│   ├── components.css                   shared primitives
│   └── utilities.css                    single purpose helpers
└── app/
    ├── app.ts / app.html / app.css      root shell and navigation
    ├── app.config.ts                    providers: router, HttpClient
    ├── app.routes.ts                    lazy-loaded routes
    ├── core/
    │   ├── api/                         HealthService, TripsService, http-error
    │   └── models/                      HealthReport, Trip
    ├── features/
    │   ├── home/                        the landing page, the journey index and the status strip
    │   ├── trips/                       the trip list
    │   ├── trip-create/                 the create trip form and its page
    │   └── trip-detail/                 a single trip
    └── shared/
        ├── format/                      pure formatting helpers
        ├── media/                       the route motif generator
        └── ui/                          StatusBadge, TripCard, DestinationPlate
```

The rules:

- `core/` holds singletons and cross-cutting concerns. Services here are the only code
  that talks to the API.
- `features/` holds one folder per screen. A feature may use `core/` and `shared/`.
- `shared/` holds presentational components with no feature knowledge.
- Features are lazy-loaded, so the initial bundle stays small.

Every screen keeps one signal per state — `loading`, `error` and the payload — rather
than a single union, because Angular's template type checker cannot narrow a union that
is reached through a signal call. `Home` established that pattern and the trip screens
follow it.

**The design system is global, not per component.** Angular caps a component stylesheet
at 4kB, so a shared component library cannot live in component CSS without breaking the
build. `src/styles/` therefore holds the tokens, the element defaults and the shared
primitives, and a component stylesheet is left for that component's own layout. The
global classes are the primitives; anything that needs behaviour is a component.

**Colour roles are constrained, not merely chosen.** `--color-ink`, `--color-ink-soft`
and `--color-ink-faint` are the only text colours and each clears 4.5:1 on every
surface. `--color-terracotta` is 4.4:1 on paper, so it is reserved for large text, rules
and indicators, and `--color-terracotta-deep` exists for small text. `--color-sage` is
3.1:1 and is never used for text at all.

**There is no photography in the product.** The image policy is a hard rule, and it is
enforced by the design as much as by the code:

- no permanent decorative photograph anywhere, including the home page and the shared
  components;
- no *default destination image* — nothing is chosen by guessing from free text, and
  there is no table mapping a destination to an asset of any kind;
- no image asset is committed to the repository, and the application issues zero image
  requests at runtime;
- a trip renders a **plate**: a drawn panel carrying a ground, a faint graticule, a
  route motif and the destination set in type.

**A plate is drawn from the trip's id, never from its destination.**
`shared/media/route-motif.ts` hashes the id into a route that travels across the plate
and passes through three to six waypoints, and into the ground the plate is drawn on. The
same trip always draws the same plate, and because the motif says nothing about the
place, it can never be wrong about where the trip is. The plate is `aria-hidden`; the
destination and the index are repeated as real text beside it, so a reader hears each
fact exactly once.

The contrast of a plate is fixed by its ground, because the ink is fixed by the ground:
`--plate-ink-light` on the paper, sage and clay grounds (14.25:1, 13.47:1 and 13.53:1)
and `--plate-ink-dark` on the forest ground (10.30:1). The route is drawn strongly
enough to read as a line at around 4.8:1; the graticule is decoration and is allowed to
stay faint. The destination carries a soft halo in the ground colour, so the route never
cuts the type in half.

**The trip detail page has no scrim, and no image-dependent contrast risk.** Its cover
is a forest-ground band, so the type over it is always the same two colours.

Form validation is owned by the feature, not by a library. In `features/trip-create/`,
`trip-date-range.ts` holds a pure `validateTripDates` function and the `ValidatorFn` that
wraps it, so the rule that the end date cannot precede the start date can be unit tested
on its own. The rule itself lives in the domain; the form mirrors it so the user gets
immediate feedback, and the API remains the authority.

**Dates are parsed and formatted in UTC.** The API sends `DateOnly` values as plain
`YYYY-MM-DD` strings, so `formatTripDates` parses them as UTC and formats them with
`timeZone: 'UTC'`. Parsing them in local time would move a trip a day for some readers.

In development the Angular dev server proxies `/api` and `/health` to
`http://localhost:5080` (`proxy.conf.json`). The browser therefore only ever talks to
its own origin and there is no CORS preflight in the common path. A CORS policy is still
configured on the API for direct cross-origin calls.

**The app makes no third party request at runtime.** Fraunces and Inter are self hosted
under the SIL Open Font License, and there are no image assets at all, so the browser
only ever talks to this origin.

## 7. Cross-cutting decisions

**Central Package Management.** `backend/Directory.Packages.props` is the single place
where package versions live. Every `PackageReference` in the solution omits its version.
This makes a dependency bump one edit instead of five, and it makes the dependency set
auditable in one file.

**A pinned transitive package.** `Microsoft.Data.SqlClient` asks for
`System.Security.Cryptography.Pkcs` 9.0.11, which carries two known advisories
(`GHSA-2fmv-rv2c-59x7`, `GHSA-846f-f764-76mx`). The NuGet audit treats these as errors
because warnings are errors in this solution. The package is pinned to 10.0.13, the build
that ships with the .NET 10 runtime the solution already targets. The alternative —
suppressing `NU1902`/`NU1903` — would have silenced the warning while leaving the
vulnerable code in place.

**No mocking library.** The test project uses a hand-written in-memory
`FakeTripRepository` and a `FixedTimeProvider`. The interfaces being faked are small, so a
mocking framework would add a dependency without removing any work.

**No mapping library.** `Trip` to `TripResponse` is a single explicit method call. A
mapping library would add a dependency and hide a conversion that is currently three lines.

**Warnings are errors.** `TreatWarningsAsErrors` is on, together with nullable reference
types and `AnalysisLevel=latest-recommended`. The single exception is `CA1707` in the
test project, because xUnit's `Method_Scenario` naming convention is intentional.

**`.sln`, not `.slnx`.** The .NET 10 SDK generates `.slnx` by default, but the classic
format is understood by every current tool, including Visual Studio and the VS Code C#
extension.

**`global.json` pins the SDK** to 10.0.402 with `rollForward=latestFeature`, so every
developer and every build uses the same compiler.

**The EF tool is pinned locally.** `.config/dotnet-tools.json` pins `dotnet-ef` to
10.0.13, matching the EF Core runtime version. Run `dotnet tool restore` once.

## 8. Testing

| Suite | Location | Scope | Needs a database? |
|---|---|---|---|
| Domain and use-case tests | `backend/Tabiji.Tests/TripTests.cs` | Invariants, validation, the service, the list order | No |
| Health endpoint tests | `backend/Tabiji.Tests/HealthEndpointTests.cs` | The health probes, via `WebApplicationFactory` | No |
| Trip endpoint tests | `backend/Tabiji.Tests/TripEndpointTests.cs` | The trip HTTP surface, via `WebApplicationFactory` | No |
| Frontend unit tests | `frontend/src/app/**/*.spec.ts` | Components, the API clients, the date rule, the route motif, the destination plate | No |

`TabijiWebApplicationFactory` starts the real host in memory and supplies a connection
string, but no connection is opened. The suite therefore passes on a machine with no SQL
Server, which matters for CI.

`TripApiFactory` derives from it and replaces the trip repository with an in-memory
fake, so the trip endpoint tests exercise real routing, status codes and JSON without a
database. `TabijiWebApplicationFactory` is left open for that reason, with a
`ConfigureServices` hook for the override.

## 9. Configuration and secrets

No credential is committed to the repository.

- `appsettings.json` holds an **empty** `Tabiji` connection string.
- `appsettings.Development.json` holds the LocalDB connection string. It uses Windows
  integrated authentication and contains no password.
- Point the app at any other server with user secrets:

  ```powershell
  cd backend/Tabiji.Api
  dotnet user-secrets set "ConnectionStrings:Tabiji" "Server=...;Database=Tabiji;..."
  ```

  or with the `ConnectionStrings__Tabiji` environment variable, which is the better option
  in CI and containers.

User secrets live outside the repository, in the user profile, so nothing sensitive can
be committed by accident.

## 10. Where this goes next

The layering is designed so that the roadmap features land without rework:

- **Invitations, proposals, votes, comments and itinerary items** each become an aggregate
  in `Domain`, a service and a port in `Application`, and a repository in `Infrastructure`.
- **Authentication** is a cross-cutting concern that the API project will own, most likely
  as a JWT bearer setup with ASP.NET Core Identity for the user store.
- **Real-time updates** would be added at the API layer over SignalR, with the domain
  unchanged.

See [roadmap.md](./roadmap.md) for the phases.
