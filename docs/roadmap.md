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
