# Tabiji

A collaborative travel planner. Create a trip, invite your friends, propose
activities, vote on them, discuss the details, and settle on a shared itinerary.

Tabiji plans trips anywhere in the world. Its visual identity is Japanese travel
editorial — warm paper, forest green, a Fraunces display serif and the 旅 monogram —
and it is drawn entirely in type and line, with no photography anywhere in the product.

This repository contains a .NET 10 modular monolith, an Angular 22 standalone SPA and a
SQL Server database. The `Trip` slice is complete end to end: a trip can be created from
the browser, listed, and opened, with an editorial interface built around it. Trips are
private to their members: the account that created one can invite other registered users as
editors, and only an owner can manage the membership or delete the trip.

> **Status:** Phase 1b of the roadmap. See [`docs/roadmap.md`](docs/roadmap.md) for what
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

### Browser suite

```powershell
cd frontend
npx playwright install chromium   # once
npm run e2e                       # starts its own API on 5081 and app on 4300
npm run e2e:ui                    # the same, with the Playwright UI
npm run e2e -- e2e/invitation-journey.spec.ts
```

The suite lives in `frontend/e2e` and is described under [Testing](#testing). If your
shell sets `NODE_ENV=production`, npm skips devDependencies — including Playwright; see the
note in the setup section.

---

## API

Base URL in development: `http://localhost:5080`.

| Method | Route | Description |
|---|---|---|
| `GET` | `/health` | Liveness probe. Does not touch the database. Anonymous. |
| `GET` | `/health/ready` | Readiness probe. Verifies the database is reachable. Anonymous. |
| `GET` | `/api/trips` | Lists the trips the signed-in account is a member of, ordered by start date and then by name. |
| `POST` | `/api/trips` | Creates a trip owned by the signed-in account. Requires `X-XSRF-TOKEN`. |
| `GET` | `/api/trips/{id}` | Returns a single trip and the caller's role in it. |
| `PUT` | `/api/trips/{id}` | Replaces a trip and its route. Owners and editors. Requires `X-XSRF-TOKEN`. |
| `DELETE` | `/api/trips/{id}` | Deletes a trip. Owners only. Requires `X-XSRF-TOKEN`. |
| `GET` | `/api/trips/{id}/members` | Lists the travellers on a trip and the caller's role. Any member. |
| `PATCH` | `/api/trips/{id}/members/{userId}` | Changes a member's role. Owners only. Requires `X-XSRF-TOKEN`. |
| `DELETE` | `/api/trips/{id}/members/{userId}` | Removes a member. Owners only; the last owner cannot be removed. Requires `X-XSRF-TOKEN`. |
| `GET` | `/api/trips/{id}/invitations` | Lists a trip's invitations. Owners only. |
| `POST` | `/api/trips/{id}/invitations` | Invites an email address. Owners only. Requires `X-XSRF-TOKEN`. |
| `DELETE` | `/api/trips/{id}/invitations/{invitationId}` | Revokes a pending invitation. Owners only. Requires `X-XSRF-TOKEN`. |
| `GET` | `/api/invitations` | Lists the pending invitations addressed to the signed-in account. |
| `POST` | `/api/invitations/preview` | Shows an invitation to the account it names. Requires `X-XSRF-TOKEN`. |
| `POST` | `/api/invitations/accept` | Accepts an invitation by its token. Requires `X-XSRF-TOKEN`. |
| `POST` | `/api/invitations/decline` | Declines an invitation by its token. Requires `X-XSRF-TOKEN`. |
| `POST` | `/api/invitations/{invitationId}/accept` | Accepts an invitation by id, for the trips page. Requires `X-XSRF-TOKEN`. |
| `POST` | `/api/invitations/{invitationId}/decline` | Declines an invitation by id, for the trips page. Requires `X-XSRF-TOKEN`. |
| `GET` | `/api/trips/{tripId}/places` | Read-only view of the places that carry a position. |
| `POST` | `/api/auth/register` | Creates an account. It does not sign the reader in. |
| `POST` | `/api/auth/login?useCookies=true` | Signs in and sets the `.Tabiji.Auth` cookie. |
| `GET` | `/api/auth/session` | Reports who the browser is. `200` either way. |
| `POST` | `/api/auth/logout` | Clears the cookie. Requires `X-XSRF-TOKEN`. |
| `GET` | `/api/auth/csrf` | Issues the antiforgery token and its cookie. |
| `GET` | `/openapi/v1.json` | The OpenAPI document. Anonymous. |

The account routes are mounted under `/api/auth` by ASP.NET Core Identity; the four that
need an email transport (`confirmEmail`, `resendConfirmationEmail`, `forgotPassword`,
`resetPassword`) answer `503` until one exists. The framework also maps `refresh` and
`manage/*`; Tabiji does not use them, so they answer `404`.

**Every trip route requires an account.** A trip is private to its members. The account
that creates it is an **owner**; an accepted invitation makes someone an **editor**.
Owners manage the trip and its membership; editors change the journey but cannot invite,
remove, change roles or delete the trip. A trip the caller cannot reach is answered `404`
exactly as a trip that does not exist; a member whose role is not enough is answered `403`;
a conflict such as the last owner or a duplicate invitation is `409`. The API is the
boundary: the Angular screens only decide what to show.

Invitations are addressed to an email address and carry a single-use token. The raw token
is never stored — the database keeps its SHA-256 — and it is never returned in a production
response. In Development, with `Email:DeliveryEnabled=false`, the create response includes
a link with the token in the URL **fragment** (`/invite#token=…`) so the flow can be used
without an email provider. Outside Development creation answers `503` until one exists.
The invitation lasts seven days (`Invitations:ExpiryDays`) and can be revoked or declined.

A trust boundary is one thing; a session is another. The examples below sign in first and
keep the cookie in a jar. A write also needs the antiforgery token from `GET
/api/auth/csrf`, echoed in `X-XSRF-TOKEN` — which is what the Angular client does for
you.

```bash
# 1. Sign in and keep the cookie
curl -c cookies.txt -X POST "http://localhost:5080/api/auth/login?useCookies=true" \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"CorrectHorse42"}'

# 2. Ask for the antiforgery token (it also sets its own cookie)
curl -b cookies.txt -c cookies.txt http://localhost:5080/api/auth/csrf
# {"token":"CfDJ8..."}

# 3. List the trips on this account
curl -b cookies.txt http://localhost:5080/api/trips
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
    "status": "Planning",
    "viewerRole": "Owner"
  }
]
```

Create a trip — destinations are the main route, and each may hold stops:

```bash
curl -b cookies.txt -X POST http://localhost:5080/api/trips \
  -H "Content-Type: application/json" \
  -H "X-XSRF-TOKEN: CfDJ8..." \
  -d '{"name":"Japan Adventure","startsOn":"2027-05-08","endsOn":"2027-05-29","destinations":[{"name":"Osaka","startsOn":"2027-05-08","endsOn":"2027-05-12","stops":[{"name":"Namba","category":"Food","address":"Namba, Osaka","notes":"Eat here.","plannedOn":"2027-05-09"},{"name":"Umeda"}]},{"name":"Kyoto","startsOn":"2027-05-13","endsOn":"2027-05-17","stops":[{"name":"Gion"}]}]}'
```

Change a trip — the request holds the whole trip, the order of `destinations` is the main
route, and the order of each `stops` array is the order within that destination:

```bash
curl -b cookies.txt -X PUT http://localhost:5080/api/trips/c47a8f9c-ba3a-4e0e-b1db-dc175473c7fa \
  -H "Content-Type: application/json" \
  -H "X-XSRF-TOKEN: CfDJ8..." \
  -d '{"name":"Japan Adventure","startsOn":"2027-05-08","endsOn":"2027-05-29","destinations":[{"id":"6d343873-45bc-42c6-abc2-5c271f99354a","name":"Osaka","startsOn":"2027-05-08","endsOn":"2027-05-12","stops":[{"id":"…","name":"Namba"},{"id":null,"name":"Tennoji"}]},{"id":null,"name":"Nagoya","startsOn":null,"endsOn":null,"stops":[]}]}'
```

Invite someone to a trip — only an owner may, and in Development the response includes the
link to send, because there is no email provider yet:

```bash
curl -b cookies.txt -X POST http://localhost:5080/api/trips/c47a8f9c-ba3a-4e0e-b1db-dc175473c7fa/invitations \
  -H "Content-Type: application/json" \
  -H "X-XSRF-TOKEN: CfDJ8..." \
  -d '{"email":"friend@example.com"}'
# {"id":"…","email":"friend@example.com","role":"Editor","status":"Pending",
#  "invitationUrl":"http://localhost:4200/invite#token=…"}
```

The recipient opens that link, signs in as the address it names, and accepts or declines.
An owner sees the pending invitations on the trip page and can revoke one there.

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
dotnet test backend/Tabiji.sln   # 450 tests
cd frontend && npm test           # 399 unit tests
npm run e2e                       # 30 browser tests
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

The browser suite (`frontend/e2e`, Playwright, Chromium) drives the real Angular app
against the real API. Two independent browser contexts stand for two accounts, so an owner
and an invitee are genuinely separate sessions rather than two tabs of one. It starts its
own servers — the app on `4300`, the API on `5081` — so it never collides with a
development session, and its own LocalDB database, `TabijiE2E`, which `global-setup`
drops and recreates from the real migrations before every run. **The development database
is never opened.** The suite signs the three fixed accounts
(`owner@tabiji.test`, `editor@tabiji.test`, `stranger@tabiji.test`) in once per run through
the real endpoints and reuses the cookie jar, because `/api/auth/login` is rate limited.

---

## Configuration

| Setting | Where | Notes |
|---|---|---|
| `ConnectionStrings:Tabiji` | `appsettings.json` (empty), `appsettings.Development.json` (LocalDB) | Use user secrets or `ConnectionStrings__Tabiji` for other servers. |
| `Cors:AllowedOrigins` | `appsettings.json` | The Angular dev origin, `http://localhost:4200`. |
| `Email:DeliveryEnabled` | `appsettings.json` (`false`) | Off until a mail transport exists; the four email-dependent account routes and invitation creation outside Development answer `503` while it is. |
| `Invitations:ExpiryDays` | `appsettings.json` (`7`) | How long an invitation remains acceptable, between 1 and 30 days. |
| `Invitations:PublicBaseUrl` | `appsettings.Development.json` (`http://localhost:4200`) | The frontend origin the invitation link is built from. Required when `Email:DeliveryEnabled` is true — the host refuses to start without it rather than mailing links to localhost. |
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
- **Accounts.** Signing in sets an `HttpOnly`, `SameSite=Lax` cookie named `.Tabiji.Auth`.
  In development it is not marked `Secure`, because the dev server is plain HTTP; outside
  development it always is. The cookie is encrypted with Data Protection keys, so a
  deployment that does not persist its key ring will sign everybody out on restart.

---

## Licence

Not yet specified. Add one before publishing.
