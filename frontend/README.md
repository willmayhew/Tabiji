# Tabiji — Frontend

The Angular 22 standalone SPA for Tabiji.

Setup, scripts and the full project overview live in the [repository README](../README.md).
The architecture is described in [`docs/architecture.md`](../docs/architecture.md).

## Quick start

```powershell
npm ci
npm start     # http://localhost:4200
```

The dev server proxies `/api` and `/health` to the API on `http://localhost:5080`
(see `proxy.conf.json`), so start the backend first:

```powershell
dotnet run --project ../backend/Tabiji.Api --launch-profile http
```

## Layout

```
src/app/
├── core/
│   ├── api/       HealthService, TripsService — the only code that calls the API
│   └── models/    HealthReport, Trip
├── features/      home/, trips/ — one folder per screen, lazy loaded
└── shared/ui/     reusable presentational components
```

## Scripts

| Command | Purpose |
|---|---|
| `npm start` | Dev server on <http://localhost:4200> with the API proxy. |
| `npm run build` | Production build into `dist/tabiji`. |
| `npm test` | Unit tests, run with Vitest. |
| `npm run watch` | Development build in watch mode. |

## Scaffolding

```bash
npx ng generate component features/my-feature/my-feature
npx ng generate service core/api/my-service
```
