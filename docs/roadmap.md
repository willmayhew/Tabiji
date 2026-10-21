# Tabiji — Roadmap

The roadmap is ordered by dependency, not by preference. Each phase assumes the previous
one is finished and adds one coherent capability.

Legend: **Done** · *In progress* · Planned

---

## Phase 0 — Foundation · **Done**

Get the repository, the build, the database and the deployment shape in place, with one
thin vertical slice proving every layer is actually wired.

- [x] Monorepo layout: `backend/`, `frontend/`, `docs/`, `.github/`
- [x] .NET 10 solution with `Domain`, `Application`, `Infrastructure`, `Api`, `Tests`
- [x] Dependency rule enforced through project references
- [x] Central Package Management and a pinned SDK (`global.json`)
- [x] EF Core 10 with SQL Server, initial migration applied to LocalDB
- [x] Minimal API host with liveness and readiness probes
- [x] `Trip` vertical slice: `POST /api/trips`, `GET /api/trips/{id}`
- [x] Angular 22 standalone SPA, lazy-loaded features, dev proxy
- [x] Unit and integration tests on both sides
- [x] Architecture and roadmap documentation

**Exit criteria:** a clean clone builds, the tests pass, a trip can be created from the
browser and read back out of SQL Server.

---

## Phase 0.5 — Trips experience · **Done**

Turn the `Trip` slice into something a person can actually use.

- [x] `GET /api/trips` lists every trip, ordered by start date and then by name
- [x] List screen with distinct loading, empty, loaded and error states
- [x] Create-trip form with per-field validation and inline messages
- [x] Trip detail screen at `/trips/:id`, with a distinct not-found state
- [x] Shared `TripCard` component reused by the list and the detail screen
- [x] Date rule shared between the form and the domain, and unit tested on its own
- [x] Fixed a defect where `takeUntilDestroyed()` was called outside an injection
      context, which broke the Home refresh button and both trip forms

**Exit criteria:** a trip can be listed, created and opened from the browser, and every
state of every screen is covered by a test.

---

## Phase 0.75 — Editorial interface · **Done**

Give the application a visual identity that is worth looking at, without
changing what it does. The goal was a product that reads as a travel
journal rather than an admin panel, built on the existing architecture.

- [x] A design system of global stylesheets: tokens, base, components and
      utilities, with the duplicated card rules consolidated into one place
- [x] A Japanese travel editorial palette: warm ivory paper, deep forest
      green, muted sage and a restrained terracotta, with the colour roles
      constrained so a token cannot be misused
- [x] Self hosted Fraunces and Inter under the SIL Open Font License, so
      the app makes no third party request at runtime
- [x] Ten curated photographs of Japan, committed locally, and a pure
      lookup that maps a trip's destination to one of them or to the 旅
      monogram
- [x] An editorial home page, a photo led trips list, a create trip page of
      its own at `/trips/new`, and a trip detail spread
- [x] Accessibility: a skip link, a focus ring on every control, labelled
      fields with errors wired to their inputs, a heading order with no
      gaps, and honoured reduced motion
- [x] Responsive from 320px to 2560px with no horizontal overflow

**Exit criteria:** every route renders in the new identity, the existing
tests still pass, an axe audit reports no violations, and the type over
the trip detail hero measures at least 4.5:1 against a white photograph.

> **Superseded in part by Phase 0.8.** The ten curated Japan photographs and the
> destination-to-photograph lookup introduced here have been removed. A trip is now drawn
> as a plate generated from its id, so the product ships no image asset at all.

---

## Phase 0.8 — Worldwide travel and an image-free interface · **Done**

Make Tabiji a planner for trips anywhere in the world, and give the interface a
visual identity that does not depend on photographs. The brand stays Japanese; the data
stops being Japanese.

- [x] The destination confirmed to be free text end to end — domain, DTOs, EF mapping
      and API — with no country list, no parsing and no constraint beyond its length
- [x] The Japan-specific destination-to-photograph lookup removed, along with the ten
      committed photographs and the `DestinationPhoto` component
- [x] A plate system that replaces the photograph: a ground, a faint graticule, a route
      motif and the destination set in type, drawn from the trip's id
- [x] Every route rebuilt on the new system: a typographic home page with an original
      SVG composition, an editorial trips grid with a featured leading entry, a
      numbered create-trip form, and a trip detail spread with a forest-ground cover
- [x] A journey index on the home page, fed by the real trips API
- [x] The API health panel reduced to a quiet status strip
- [x] Worldwide copy throughout, including the destination hint and the page titles
- [x] The backend test fixtures generalised to a mixed worldwide set, with new tests
      proving an international destination is accepted by the domain and the HTTP surface
- [x] Accessibility: an axe audit reports zero violations on every route, one `h1` per
      route, no horizontal overflow from 320px, and the focus ring verified in the browser

**Exit criteria:** the product plans a trip to anywhere in the world, the repository
contains no image asset, the application makes zero image requests, and the existing
tests still pass.

---

## Phase 0.9 — A route of many destinations · **Done**

Let a trip hold more than one place, in a deliberate order. The product already planned
trips anywhere in the world; this is about the *shape* of a trip rather than where it
goes.

- [x] A `TripDestination` entity with its own identity, a free-text name and an explicit
      sort order, owned by the trip aggregate
- [x] A `TripDestinations` table with a cascading foreign key and a unique index on
      `(TripId, SortOrder)`, so a trip can never hold two stops in the same position
- [x] `Trip.Create` takes an ordered list of destination names; `AddDestination`,
      `RemoveDestination` and `ReorderDestinations` keep the aggregate coherent
- [x] The API takes an ordered array on create and returns the stops in the same order
- [x] An additive, non-destructive migration that turns every existing destination into
      the first stop of its trip, keeps the legacy scalar column for one release, and
      ships the queries that verify the backfill
- [x] A route builder in the create form: numbered stops, add, remove and move up or
      down, with the focus following the stop that moved
- [x] The trip list and the trip page read the whole route, and the plates are labelled
      with the first stop
- [x] Duplicate names allowed — a route may visit the same place twice

**Exit criteria:** a trip can be created with any number of ordered destinations, the
order survives the database and the API, the existing data is intact, and every route
reads as a journey.

---

## Phase 0.95 — Dated stops and a trip that can be changed · **Done**

Let a stop say when the traveller expects to be there, and let the trip be changed after
it has been made. The route was ordered in Phase 0.9; this gives it a *schedule*, and it
gives the traveller the pen.

- [x] A stop carries an optional start day and an optional end day. Both may be left
      out, so a place can be added long before its dates are known
- [x] `TripSchedule`, a domain rule that keeps a route in chronological order:
      forgiving about missing dates, strict about contradictory ones, and never a reorder
- [x] Every date must fall inside the trip's own dates, and a stop may not end before it
      starts
- [x] The rule names the stop at fault and the stop it contradicts, so the traveller can
      put it right rather than guess
- [x] `Trip.ApplyRoute` checks the whole route before it changes anything, so a refused
      update leaves the trip exactly as it was
- [x] `PUT /api/trips/{id}` replaces the whole trip in one request and one transaction:
      rename, reschedule, add, rename a stop, remove a stop, reorder and clear a date
- [x] The stops keep their identity across an edit, so a future item can hang off one
- [x] An additive migration that adds the two nullable date columns and moves no data
- [x] A shared editor, used by the create page and by the trip page in its edit mode, so
      the route builder is written once
- [x] The trip page grows an inline edit mode, with a guard that asks before the reader
      leaves a page with an unsaved change
- [x] The schedule is read back honestly: a stop without dates says so, and the route
      says how much of it has been decided
- [x] The plate on a card carries the title of the trip rather than the first stop, so
      that a list of trips says which journey each card is

**Exit criteria:** a stop can be given the days of its stay, a route that contradicts
itself is refused with a message that says why, and a trip can be changed in place with
every change saved together.

---

## Phase 1 — Accounts, trips and membership

*Planned.* Everything downstream needs to know who the participants are.

- [ ] User accounts. Decide the identity provider and the token strategy first.
- [ ] `User` entity in the domain, with a persistence representation that does not leak
      identity-provider details.
- [ ] `TripMember` aggregate: a user, a trip, and a role (`Owner`, `Organiser`, `Member`).
- [ ] Invitations: create an invite for a trip, accept it, expire it.
- [ ] Full trip CRUD, including editing, cancelling and archiving.
- [ ] Authorisation policies: who may view, edit or delete a trip.
- [ ] Frontend: sign-in, trip list, trip detail, invitation flow.

**Design notes**

- Identity belongs in `Infrastructure` and `Api`; the domain only sees a `UserId` value
  object.
- Invitations are their own aggregate with their own lifecycle
  (`Pending` → `Accepted` / `Revoked` / `Expired`).
- Expiry and acceptance must be safe under concurrent requests, so the accept operation
  needs a conditional update rather than a read-then-write.

---

## Phase 2 — Proposals and voting

*Planned.* The core collaborative loop of the product.

- [ ] `ActivityProposal` aggregate: a trip, a title, a description, a category, a
      proposed time slot, an author, and a state
      (`Proposed` → `Accepted` / `Rejected` / `Withdrawn`).
- [ ] Voting: one vote per member per proposal, with the option to change or withdraw it.
- [ ] Vote tallies derived from the votes, never stored as a counter.
- [ ] Rules for accepting a proposal — a threshold, the organiser's decision, or both.
- [ ] Frontend: propose an activity, list proposals, vote, see the current tally.

**Design notes**

- A vote is modelled as a value object inside the proposal aggregate. Whether votes are
  stored as rows or as a JSON column is a persistence decision for Phase 2.
- Vote counts are computed, not denormalised. A stored counter would need careful
  invalidation, and the volume does not justify it.

---

## Phase 3 — Discussion

*Planned.*

- [ ] `Comment` aggregate attached to a trip, a proposal or an itinerary item.
- [ ] Threading, with a parent comment and a depth limit.
- [ ] Mentions, rendered and notified.
- [ ] Edit history for comments.
- [ ] Frontend: comment threads, composer, edit and delete.

**Design notes**

- Comments are a separate aggregate rather than a collection on the proposal, so that
  writing a comment does not need to load and rewrite the whole proposal.
- Mentions are resolved to `UserId`s at write time, so a mention survives a rename.

---

## Phase 4 — Shared itinerary

*Planned.* The plan the group agrees on.

- [ ] `Itinerary` aggregate owned by a trip: ordered `ItineraryItem`s.
- [ ] An item is either a proposal that was accepted or a manually added entry.
- [ ] Scheduling: date, start time, duration, and a venue with optional coordinates.
- [ ] Conflict detection: overlapping items on the same day.
- [ ] Manual reordering with a stable order key.
- [ ] A read-only share view for people who are not signed in.
- [ ] Frontend: itinerary timeline, drag-to-reorder, conflict warnings.

**Design notes**

- The itinerary references accepted proposals rather than copying them, so a proposal
  that changes is reflected in the plan.
- Ordering uses a fractional index so that a reorder does not rewrite the whole list.

---

## Phase 5 — Polish

*Planned.*

- [ ] Notifications, starting with in-app and expanding to email.
- [ ] Real-time updates over SignalR: new proposals, votes, comments, itinerary changes.
- [ ] Offline-friendly caching of the trip the user is looking at.
- [ ] Map view of the itinerary, using a geocoding provider.
- [ ] Search and filtering.
- [ ] Accessibility audit and keyboard navigation pass.

---

## Explicitly not planned

These are deliberate exclusions, not oversights. Each would add cost without adding value
at this stage of the project.

- **Microservices.** The whole system is one API and one database. Splitting it would
  add network calls, distributed transactions and deployment overhead for no user-visible
  gain. The layering in `backend/` keeps the door open if that ever changes.
- **A second database or a document store.** SQL Server handles the relational shape of
  this domain comfortably. Adding a second store would mean two sources of truth.
- **A message broker.** There is no asynchronous work that needs decoupling yet.
- **Premature optimisation.** No caching tier, no read replicas, no CDN. Add them when
  a measurement says so.

## Deferred by design

- **Authentication** is deliberately not implemented in Phase 0. It is the first item in
  Phase 1, and choosing an identity provider is a product decision.
- **Deployment** is deliberately not implemented. The application is built to be
  deployable — configuration comes from environment variables and secrets, and the API
  runs behind a reverse proxy with TLS terminated upstream — but no infrastructure is
  provisioned yet.
