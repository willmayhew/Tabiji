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
Trip.Create(name, destinations, startsOn, endsOn, createdAt)
trip.Rename(name)
trip.Reschedule(startsOn, endsOn)
trip.AddDestination(name)
trip.RemoveDestination(destinationId)
trip.ReorderDestinations(orderedIds)
trip.ApplyRoute(name, startsOn, endsOn, destinations)
```

Invariants, all enforced in the domain and all covered by unit tests:

- the name is required and at most 100 characters;
- a trip has at least one destination and at most twenty;
- each destination name is required and at most 200 characters;
- a destination's sort order cannot be negative;
- the trip cannot end before it starts;
- a new trip always starts in `Planning`.

**A trip's route is an ordered collection of `TripDestination` entities.** Each one has
its own identity, a name, an explicit sort order and the optional days of its stay:

```csharp
public sealed class TripDestination
{
    public const int NameMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid TripId { get; private set; }
    public string Name { get; private set; }
    public int SortOrder { get; private set; }
    public DateOnly? StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
}
```

`TripDestination.Create` is `internal`, so the owning trip is the only thing that can make
one, and it is the only thing that can put it on a route.

**The dates describe the planned stay at a stop.** They are the days the traveller expects
to be there, not a replacement for the trip's own dates. Both are optional, and either may
be left out on its own: a stop can be added long before its dates are known, and a trip that
is still being planned has stops with no dates at all. The interface says so where it reads
them back, rather than showing an empty schedule as though it were a complete one.

**The names are free text and are treated as such.** Each one is trimmed, checked for
blankness and length, and then stored as-is. Nothing in the domain, the DTOs, the
persistence mapping or the API parses it, splits it or compares it against a list of
countries, so a route may be to `"Rome, Italy"`, `"Reykjavík, Ísland"` or `"東京, 日本"`
without anything else changing. The `nvarchar(200)` column holds any Unicode, and there
is no check constraint or foreign key on it.

**The same place may appear twice.** A route that visits Rome on the way out and again on
the way home is a round trip, not a mistake, so duplicates are allowed. The domain rejects
only blank and over-long names, as it always did.

Invalid input raises `ArgumentException` with the offending parameter name. The API maps
that to a `400` with an RFC 7807 problem document.

The `Trip` type has a private parameterless constructor and private setters so that EF
Core can materialise it without exposing a public mutation API. Its destinations live in a
private list behind a read-only property, so nothing outside the aggregate can add a stop
or change the order.

### 5.1.1 The route and its dates

A route is checked as a whole, and the check lives in the domain:
`Tabiji.Domain.Trips.TripSchedule.EnsureChronological`. It is a pure function over plain
data, so it is unit-tested on its own.

The rule is **forgiving about missing dates and strict about contradictory ones**.

1. Every date a stop carries must fall inside the trip's own dates.
2. A stop may not end before it starts.
3. The stops must be able to be visited in the order they are given.

The third rule is a sweep from the first stop to the last, carrying a **floor**: the
earliest day the next stop may begin.

- A stop with **no dates** establishes nothing. It is stepped over, and the floor passes
  straight through it.
- A stop with a **start date** establishes that day, because its stay may be as short as a
  single day.
- A stop with **only an end date** establishes its end, because it must have begun at or
  before it.
- A stop with **both dates** establishes its end.

A stop is refused when its own date falls before the floor. That is the one case that no
choice of the missing dates could repair, because the offending date was given rather than
inferred. The floor only moves forward, so a stop is measured against every dated stop
before it and not only against its neighbour: `Osaka 16–20 May`, an undated stop, and
`Tokyo 18–22 May` is refused, even though the two dated stops are not adjacent.

Three things the rule deliberately does not do:

- **It never reorders.** The route order is the traveller's. A route whose dates contradict
  its order is refused with a message naming both stops, so that the traveller can put it
  right.
- **It never requires completeness.** A stop may have no dates, or only one of them.
- **It never requires coverage.** The days that belong to no stop are the traveller's own.

The earlier stop's end date may be **equal** to the later stop's start date: leaving Osaka
on the 20th and reaching Kyoto on the 20th is a hand-off, not an overlap. The overlap test
is therefore `end_i > start_j`, strictly greater.

### 5.1.2 Changing a trip

`Trip.ApplyRoute` applies a whole route in one go: the name, the trip's dates and the
ordered stops, with their names and their optional dates. It is the path the API takes
when a trip is changed.

Everything is checked **before** anything is changed. A refused route leaves the trip
exactly as it was, which is what makes "an invalid update changes nothing" true by
construction rather than by care.

A stop is written as a `TripDestinationDraft`: its identity, or none for a new one, its
name and its dates. An identity that names a stop the trip does not have is a mistake and
is refused; a stop the drafts leave out is removed.

```csharp
public sealed record TripDestinationDraft(
    Guid? Id, string Name, DateOnly? StartsOn, DateOnly? EndsOn);
```


### 5.2 Application

`TripService` is the use-case surface. It depends on the `ITripRepository` port and on
`TimeProvider` (registered as `TimeProvider.System`), which makes the creation timestamp
deterministic in tests.

`TripService` maps `Trip` to `TripResponse`. The response is a plain record, so no EF
type ever crosses the layer boundary and the wire contract is explicit. The stops are
ordered by their sort order in the mapping, so the contract holds whichever repository
produced the trip.

### 5.3 Infrastructure

`TabijiDbContext` exposes `DbSet<Trip>` and `DbSet<TripDestination>` and applies the
entity configurations found in its own assembly.

`TripConfiguration` maps the aggregate to the `Trips` table:

| Property | Column | Type |
|---|---|---|
| `Id` | `Id` | `uniqueidentifier` |
| `Name` | `Name` | `nvarchar(100)` |
| `StartsOn` | `StartsOn` | `date` |
| `EndsOn` | `EndsOn` | `date` |
| `Status` | `Status` | `nvarchar(30)` |
| `CreatedAt` | `CreatedAt` | `datetimeoffset` |
| `Destination` *(legacy)* | `Destination` | `nvarchar(200)`, nullable |

`TripDestinationConfiguration` maps the stops to the `TripDestinations` table:

| Property | Column | Type |
|---|---|---|
| `Id` | `Id` | `uniqueidentifier` |
| `TripId` | `TripId` | `uniqueidentifier` |
| `Name` | `Name` | `nvarchar(200)` |
| `SortOrder` | `SortOrder` | `int` |
| `StartsOn` | `StartsOn` | `date`, nullable |
| `EndsOn` | `EndsOn` | `date`, nullable |

The foreign key to `Trips` cascades, and a **unique** index on `(TripId, SortOrder)` makes
it impossible for a trip to hold two stops in the same position. The same index covers the
foreign-key lookup, so one index does both jobs.

A **check constraint** `CK_TripDestinations_Dates` refuses a stop that ends before it
starts. It is a belt-and-braces guard on a single row: the domain is the authority on this,
and the rule that spans the whole route cannot be written in SQL at all.

`Status` is stored as a string rather than an integer so that the stored value stays
readable and adding an enum member cannot silently change the meaning of existing rows.

**The legacy `Trips.Destination` column.** It held the only copy of a trip's destination
before the route existed. It is now a nullable column that nothing reads or writes, and it
is mapped in `TripConfiguration` as a *shadow property* so that the next migration does not
drop it. The column is kept for one release, so that no data is lost, and it is removed by
deleting those lines and running a migration of its own once the backfill has been
verified.

`TripRepository` is the only class that knows how a `Trip` is persisted. It is registered
against the `ITripRepository` abstraction, so the composition root is the only place that
knows the concrete implementation.

`ITripRepository.ListAsync` returns every trip ordered by start date and then by name.
The ordering is part of the port's contract, and the repository produces it with a SQL
`ORDER BY` rather than sorting in memory. The route of each trip is loaded with an
*ordered* include, so the materialised aggregate is in the order its stops are visited.

### 5.3.1 The migration that introduced the route

`20261020201209_AddTripDestinations` is additive and non-destructive. It creates the
table and the index, makes the legacy column nullable, and backfills every existing trip:

```sql
INSERT INTO [TripDestinations] ([Id], [TripId], [Name], [SortOrder])
SELECT NEWID(), [Id], LTRIM(RTRIM([Destination])), 0
FROM [Trips]
WHERE [Destination] IS NOT NULL
  AND LTRIM(RTRIM([Destination])) <> '';
```

Every existing destination becomes the **first stop** of its trip. A blank or
whitespace-only destination produces no stop at all: making one up would invent data, and
such a trip is rendered as an explicit empty state.

The `Down` migration puts the scalar back before the route disappears, taking the first
stop of each trip, and falls back to a placeholder for a trip that has none so that the
column can be made non-nullable again.

`backend/verify-migration.sql` holds the three queries that confirm the backfill is
complete and faithful. They are the gate for dropping the legacy column, and they must
return clean before that migration is written.

### 5.3.2 The migration that gave the stops their dates

`20261021165455_AddDestinationDates` is additive and moves no data. It adds the two
nullable `date` columns and the check constraint, and drops nothing:

```sql
ALTER TABLE [TripDestinations] ADD [EndsOn] date NULL;
ALTER TABLE [TripDestinations] ADD [StartsOn] date NULL;
```

Every existing stop gets `NULL` for both dates, which reads as *"the days for this stop
have not been decided"* — the honest answer, and the same reason the route's backfill
declined to invent a stop for a blank legacy destination.

The legacy `Trips.Destination` column is left exactly where it was. It is removed by a
migration of its own, once the backfill has been verified, and not before.

`backend/verify-migration.sql` holds two further queries: one for stops whose dates fall
outside their trip, and one for stops that contradict the order of their route. Both are
expected to return no rows.

### 5.3.3 Writing a route back

`TripRepository.UpdateAsync` writes a trip and its route in one transaction, so a trip is
never left half changed. The stops are updated **in place** rather than written afresh, so
that a stop keeps its identity across an edit — and so that a future item hanging off a
stop is never cascaded away by a trip being changed.

**The positions are moved in the database rather than through the change tracker.** The
unique index on `(TripId, SortOrder)` makes the tracker read a reorder as a circular
dependency: asked to move a stop into a place another stop is holding, it refuses to save
at all. So the repository:

1. **parks** the positions, moving every stop of the trip out of the way with a single
   statement. A trip holds at most twenty stops, so its positions live between 0 and 19 and
   the parking range can never reach them;
2. writes the **final** positions in the database, one stop at a time. Every stop is
   parked out of the way and every stop is given a position of its own, so no two stops
   ever share a position on the way;
3. lets the change tracker write the **names and the dates** of the stops that are
   staying, the insertions of the stops that are new and the deletions of the stops that
   are gone.

The tracker is told that the positions have not changed, so that it never tries to move a
stop into a place another stop is holding.


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
| `PUT` | `/api/trips/{id}` | Replaces a trip and its route. `200` with the trip, `400` on invalid input, `404` when there is no such trip. |
| `GET` | `/health` | Liveness. Never touches the database. |
| `GET` | `/health/ready` | Readiness. Verifies the database is reachable. |
| `GET` | `/openapi/v1.json` | The OpenAPI document. |

**Creating a trip takes an ordered array of stops.** The order of the array is the order of
the stops, and each stop carries the optional days of its stay:

```json
{
  "name": "Japan Adventure",
  "startsOn": "2027-05-16",
  "endsOn": "2027-06-05",
  "destinations": [
    { "name": "Osaka", "startsOn": "2027-05-16", "endsOn": "2027-05-20" },
    { "name": "Kyoto", "startsOn": "2027-05-20", "endsOn": "2027-05-24" },
    { "name": "Ise", "startsOn": null, "endsOn": null },
    { "name": "Tokyo", "startsOn": "2027-05-26", "endsOn": "2027-06-05" }
  ]
}
```

```json
{
  "id": "c47a8f9c-ba3a-4e0e-b1db-dc175473c7fa",
  "name": "Japan Adventure",
  "destinations": [
    { "id": "…", "name": "Osaka", "sortOrder": 0, "startsOn": "2027-05-16", "endsOn": "2027-05-20" },
    { "id": "…", "name": "Kyoto", "sortOrder": 1, "startsOn": "2027-05-20", "endsOn": "2027-05-24" },
    { "id": "…", "name": "Ise", "sortOrder": 2, "startsOn": null, "endsOn": null },
    { "id": "…", "name": "Tokyo", "sortOrder": 3, "startsOn": "2027-05-26", "endsOn": "2027-06-05" }
  ],
  "startsOn": "2027-05-16",
  "endsOn": "2027-06-05",
  "status": "Planning"
}
```

The response carries the sort order explicitly because the domain has one, and the array
is returned in that order too, so a client may rely on either. It also carries each stop's
**identity**, so that a client can name a stop again when it changes the trip.

**Changing a trip is one request.** `PUT /api/trips/{id}` replaces the whole trip: its
name, its dates and its route. One endpoint expresses every edit, because the route is one
aggregate and splitting it across five endpoints would mean running the chronological rule
in five places and leaving the trip half-saved between two calls.

| In the request | Meaning |
|---|---|
| `"id": "<guid>"` | An existing stop. It is renamed, rescheduled and moved to its new position, and keeps its identity. |
| `"id": null` | A **new** stop. The server generates the identity. |
| A stop absent from the array | **Removed.** |
| The order of the array | **The route order.** |
| `"startsOn": null` | **Cleared.** The same for `"endsOn"`. |

```json
{
  "name": "Japan Adventure",
  "startsOn": "2027-05-16",
  "endsOn": "2027-06-05",
  "destinations": [
    { "id": "6d343873-45bc-42c6-abc2-5c271f99354a", "name": "Osaka", "startsOn": "2027-05-16", "endsOn": "2027-05-20" },
    { "id": null, "name": "Nagoya", "startsOn": null, "endsOn": null }
  ]
}
```

The request is checked in full before anything is written, so a refused request leaves the
trip exactly as it was. The refusals say which stop is at fault and, where the dates
contradict the route, which stop it contradicts:

> `"Kyoto" starts on 20 May 2027, but "Tokyo", which comes before it, is there until 5 June 2027.`

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
    │   ├── trip-create/                 the page that makes a trip
    │   └── trip-detail/                 a single trip, and the mode that changes it
    └── shared/
        ├── format/                      pure formatting helpers
        ├── media/                       the route motif generator
        ├── validate/                    the pure rules the forms mirror
        ├── form/                        the editor, shared by two screens
        └── ui/                          StatusBadge, TripCard, DestinationPlate
```

The rules:

- `core/` holds singletons and cross-cutting concerns. Services here are the only code
  that talks to the API.
- `features/` holds one folder per screen. A feature may use `core/` and `shared/`.
- `shared/` holds components and rules that know no screen. It has three kinds of thing:
  presentational components in `shared/ui/`, pure formatting helpers in `shared/format/`,
  and the pure rules in `shared/validate/` that two screens both need.
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

**A plate is labelled with the first stop.** The plate's route motif is the beginning of
the journey, so it carries the first destination, and the body beside it sets out the
whole route. A trip with no stops at all — a legacy trip whose destination was blank —
renders the motif with no label and an explicit *"No destinations recorded"* line, never
a blank gap.

**The route is one list, used twice.** The `.route` primitives in `components.css` draw a
numbered stop joined to the next by a hairline. The editor uses them to *build* a route
and the trip page uses them to *read* one back, so the two always look the same. The
hairline that joins the stops is the same device the destination plates draw their route
with. A stop carries its name and its two date fields in one cell, so that the row stays
a single grid row and the hairline is not disturbed by the dates. The name keeps its room
and the dates give way when there is not enough space, because a place without a name is
not a place; the caption of a date travels with its field, so the two are never left
apart.

**The editor is shared.** `shared/form/trip-form.ts` is the route builder, and it is used
by two screens: the create page, and the trip page in its edit mode. The stops live in a
`FormArray`, so each one is validated on its own, and the whole route is validated by
`shared/validate/trip-destinations.ts`, a pure function wrapped in a `ValidatorFn` exactly
like the date rule beside it. The stops are moved with **move up and move down buttons**
rather than by dragging, so reordering is keyboard-accessible without a library and
without a pointer implementation. The template tracks each control rather than its
position, so a moved row is moved in the document and **the focus follows it**. Adding or
removing a stop moves the focus deliberately, and a polite live region says what
happened.

A stop handed to the editor is laid out in it, in the order it is visited, with its
identity kept so that the API can tell a renamed stop from a new one. The editor is the
one place that calls `TripsService`: it owns the form, the validation, the focus handling
and the saving, so that neither screen has to repeat them. It still never touches
`HttpClient` — only `core/api/` does that.

**A trip can be changed in place.** The trip page grows an edit mode: the masthead offers
*Edit trip*, and the page then shows the editor in place of the read-only route. The
editor saves the whole trip in one request, so every change is persisted together or not
at all. A `CanDeactivate` guard asks before the reader leaves a page with an unsaved
change, and only when there is one.

**The schedule is read back honestly.** A stop with dates shows them; a stop without them
says *Dates not decided*, and the route says how many stops have been decided. A partly
planned trip never looks as though it were fully scheduled. The same note appears on the
trip card, but only when some of the schedule has been set, so that the list does not turn
into a column of warnings.

The contrast of a plate is fixed by its ground, because the ink is fixed by the ground:
`--plate-ink-light` on the paper, sage and clay grounds (14.25:1, 13.47:1 and 13.53:1)
and `--plate-ink-dark` on the forest ground (10.30:1). The route is drawn strongly
enough to read as a line at around 4.8:1; the graticule is decoration and is allowed to
stay faint. The destination carries a soft halo in the ground colour, so the route never
cuts the type in half.

**The trip detail page has no scrim, and no image-dependent contrast risk.** Its cover
is a forest-ground band, so the type over it is always the same two colours.

Form validation is owned by the shared rules, not by a library. In `shared/validate/`,
`trip-date-range.ts` holds a pure `validateTripDates` function and the `ValidatorFn` that
wraps it, so the rule that the end date cannot precede the start date can be unit tested
on its own. `trip-destinations.ts` holds the rules for the route: the names, and the
schedule of the whole route against the trip it belongs to. The rules themselves live in
the domain; the form mirrors them so the user gets immediate feedback, and the API remains
the authority. The mirror says the same thing in the same words as the domain, so that
the message the reader sees is the message the API would have given.

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
| The chronological rule | `backend/Tabiji.Tests/TripScheduleTests.cs` | The rule on its own, case by case | No |
| Health endpoint tests | `backend/Tabiji.Tests/HealthEndpointTests.cs` | The health probes, via `WebApplicationFactory` | No |
| Trip endpoint tests | `backend/Tabiji.Tests/TripEndpointTests.cs` | The trip HTTP surface, via `WebApplicationFactory` | No |
| Migration tests | `backend/Tabiji.Tests/*MigrationTests.cs` | The operations of a migration, inspected by reflection | No |
| Repository tests | `backend/Tabiji.Tests/TripRepositoryTests.cs` | The route written to a real database | **Yes** |
| Frontend unit tests | `frontend/src/app/**/*.spec.ts` | Components, the API clients, the rules, the route motif, the destination plate | No |

The suite is written to run **without a database**, and most of it does. The one exception
is the repository, because writing a route back is the one thing that cannot be checked any
other way: the unique index on `(TripId, SortOrder)` and the change tracker's reaction to a
reorder are behaviour of the database, not of the code. Those tests are marked with
`[DatabaseFact]`, which **skips** them when SQL Server LocalDB cannot be reached, so the
suite still runs on a machine that has no database. They have a database of their own,
created with the real migrations and dropped again afterwards.


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
