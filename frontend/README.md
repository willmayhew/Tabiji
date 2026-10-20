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
src/
├── styles/
│   ├── tokens.css        design tokens: colour, type, space, radius, shadow
│   ├── base.css          element defaults, focus ring, reduced motion
│   ├── components.css    shared primitives: .button, .field, .card, .callout, .plate
│   └── utilities.css     a small set of single purpose helpers
├── app/
│   ├── core/
│   │   ├── api/           HealthService, TripsService — the only code that calls the API
│   │   └── models/        HealthReport, Trip
│   ├── features/          home/, trips/, trip-create/, trip-detail/ — one folder per screen
│   └── shared/
│       ├── format/        pure formatting helpers, e.g. trip dates
│       ├── media/         the route motif generator
│       └── ui/            reusable presentational components
└── public/
    └── fonts/             self hosted Fraunces and Inter, with their OFL licences
```

The design system lives in the global stylesheets rather than in component
styles, because Angular caps each component stylesheet at 4kB. A component
stylesheet is for that component's layout; anything shared belongs in
`src/styles/components.css`.

## Design notes

- **Colour roles are constrained.** `--color-ink`, `--color-ink-soft` and
  `--color-ink-faint` are the only text colours, and each clears 4.5:1 on
  every surface. `--color-terracotta` is for large text, rules and
  indicators, never small body text; `--color-sage` is a surface and
  border accent and is never text.
- **Fonts are self hosted.** Fraunces for display, Inter for body, both
  under the SIL Open Font License. The app makes no request to a third
  party at runtime.
- **There is no photography.** The product ships no image asset and makes
  zero image requests. A trip is drawn as a *plate* — a ground, a faint
  graticule, a route motif and the destination set in type — generated from
  the trip's id by `route-motif.ts`. Nothing selects media by guessing from
  the destination text, and there is no destination-to-asset table.

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
