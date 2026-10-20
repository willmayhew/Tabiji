# Copilot instructions for Tabiji

## Project overview

Tabiji is a collaborative travel planner: users create trips, invite friends, propose
activities, vote on proposals, discuss them, and build a shared itinerary.

The product plans trips anywhere in the world. The *identity* is Japanese travel
editorial — the Tabiji name, the 旅 monogram, Fraunces and Inter, warm paper and forest
green — but nothing in the data or the domain assumes a trip is in Japan. `destination`
is free text of up to 200 characters and is never parsed or matched against a list.

This repository is a **monorepo** containing a .NET 10 backend and an Angular 22
frontend. The backend is a **modular monolith**: one deployable API, split into four
projects separated by dependency rules.

## Stack

| Area | Technology |
|---|---|
| Backend | ASP.NET Core 10, minimal APIs, C# 13 |
| Persistence | Entity Framework Core 10, SQL Server |
| Tests | xUnit 2.9, `Microsoft.NET.Test.Sdk`, VSTest |
| Frontend | Angular 22, standalone components, TypeScript 6, signals |
| Frontend tests | Vitest |
| Tooling | .NET SDK 10.0.402 (pinned in `global.json`), npm, ESLint-free (Prettier) |

## Repository layout

```
backend/
  Tabiji.sln
  Directory.Build.props        shared compiler settings
  Directory.Packages.props     central package versions
  Tabiji.Domain/               entities and invariants, zero dependencies
  Tabiji.Application/          use cases, ports, DTOs
  Tabiji.Infrastructure/       EF Core, repositories, DI
  Tabiji.Api/                  minimal API host, composition root
  Tabiji.Tests/                xUnit tests
frontend/
  src/app/core/                API services and models
  src/app/features/            one folder per screen, lazy loaded
  src/app/shared/              reusable presentational components
docs/                          architecture.md, roadmap.md
```

## The dependency rule — this is the most important rule in the repository

```
Tabiji.Domain          →  (nothing)
Tabiji.Application     →  Tabiji.Domain
Tabiji.Infrastructure  →  Tabiji.Application
Tabiji.Api             →  Tabiji.Application, Tabiji.Infrastructure
Tabiji.Tests           →  all four
```

- **`Tabiji.Domain` must never reference EF Core or ASP.NET Core.** It has no
  `PackageReference` at all. If domain logic seems to need a package, the logic is in the
  wrong layer.
- **`Tabiji.Application` must never reference EF Core or ASP.NET Core.** It references
  `Microsoft.Extensions.DependencyInjection.Abstractions` and nothing else.
- Ports such as `ITripRepository` belong in `Application`, not `Infrastructure`, so that
  the dependency points inwards.
- `Program.cs` is a composition root. It wires services and maps endpoints. It contains no
  business logic.

## Conventions

### C#

- File-scoped namespaces, one type per file, file name matches the type.
- `sealed` by default; `record` for DTOs and value objects.
- Nullable reference types are on. `TreatWarningsAsErrors` is on.
- Guard clauses in the domain raise `ArgumentException` and name the offending parameter.
- Entities keep private setters and a private parameterless constructor for EF Core.
- Prefer explicit code over a library. No MediatR, AutoMapper, FluentValidation,
  Swashbuckle, Serilog or Moq.
- Use `TimeProvider` rather than `DateTimeOffset.UtcNow` directly, so time is testable.

### EF Core

- All EF Core code lives in `Tabiji.Infrastructure`. Nothing else may reference
  `DbContext`, `DbSet` or `IQueryable`.
- One `IEntityTypeConfiguration` per entity, in `Persistence/Configurations`.
- The `DbContext` uses `ApplyConfigurationsFromAssembly`; do not configure entities inline.
- Store enums as strings with `HasConversion<string>()`.
- Migrations live in `Persistence/Migrations` and are generated with:
  ```powershell
  dotnet ef migrations add <Name> --project backend/Tabiji.Infrastructure --startup-project backend/Tabiji.Api --output-dir Persistence/Migrations
  ```

### API

- Minimal APIs with endpoint groups, not controllers.
- One file per feature under `Endpoints/`, exposing a `MapXxxEndpoints` extension.
- Use `Results.Created`, `Results.Ok`, `Results.NotFound` and `Results.Problem`.
- Register services in `AddApplication()` and `AddInfrastructure()`. Keep `Program.cs` thin.
- Health checks: `/health` for liveness (no I/O) and `/health/ready` for readiness
  (checks the database).

### Angular

- Standalone components only. No NgModules.
- The 2025 file naming style: `home.ts`, class `Home`, template `home.html`.
- Signals for state, `inject()` instead of constructor injection.
- `takeUntilDestroyed()` must always be given an explicit `DestroyRef`
  (`takeUntilDestroyed(this.destroyRef)`). Without one it calls `inject()`, which
  throws outside an injection context — a template event handler is not one.
- Keep one signal per state (`loading`, `error`, payload) rather than a single union.
  Angular's template type checker cannot narrow a union reached through a signal call.
- New control flow syntax: `@if`, `@for`, `@switch`.
- `core/` holds API services and models. `features/` holds screens. `shared/` holds
  presentational components with no feature knowledge.
- Route to a feature with `loadComponent` so it stays lazy.
- Only `core/api/` may call the API. Features talk to services, never to `HttpClient`.
- Map API failures to a user-facing message with `core/api/http-error.ts`.
- Form validation lives in the feature. Put the rule in a pure function and wrap it in
  a `ValidatorFn`, so the rule can be unit tested on its own.
- Components use `ChangeDetectionStrategy.OnPush`.

### Git and secrets

- Never commit credentials, connection strings with passwords, or API keys. The
  `appsettings.json` connection string is intentionally empty.
- Local overrides go through `dotnet user-secrets` or the
  `ConnectionStrings__Tabiji` environment variable.
- `appsettings.Local.json` and `.env` files are gitignored.

## Commands

```powershell
# Build and test the backend
dotnet build backend/Tabiji.sln
dotnet test  backend/Tabiji.sln

# Database
dotnet tool restore
dotnet ef database update --project backend/Tabiji.Infrastructure --startup-project backend/Tabiji.Api

# Run the API on http://localhost:5080
dotnet run --project backend/Tabiji.Api --launch-profile http

# Frontend
cd frontend
npm ci
npm start        # dev server on http://localhost:4200, proxies to the API
npm run build
npm test
```

## Do not

- Do not add a package without stating the reason in the pull request. The dependency set
  is deliberately small.
- Do not reference EF Core or ASP.NET Core from `Tabiji.Domain` or `Tabiji.Application`.
- Do not introduce microservices, a message broker, or a second database.
- Do not implement authentication until the identity provider has been chosen.
- Do not hardcode secrets, and do not disable the NuGet audit to make a warning go away.
  Pin the package to a patched version instead.
- Do not build out the roadmap features ahead of their phase. See `docs/roadmap.md`.
- Do not deploy anything or provision cloud resources from this repository.
- Do not add photography, decorative imagery, or a destination-to-asset lookup. The
  product ships no image asset and makes zero image requests; a trip is drawn as a plate
  by `shared/media/route-motif.ts`, generated from the trip's id. See the image policy in
  `docs/architecture.md`.
- Do not assume a trip is in Japan. The `destination` is free text and must stay that way.

## Before opening a pull request

```powershell
dotnet build backend/Tabiji.sln
dotnet test  backend/Tabiji.sln
cd frontend; npm run build; npm test
```

All four must pass, with no warnings.
