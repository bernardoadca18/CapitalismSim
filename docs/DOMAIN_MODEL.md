# Domain Model (Phase 1)

Phase 1 has no economic domain. This document describes the only
"domain" that exists in this delivery: the simulation kernel's own
concepts. It also sketches, at a purely conceptual level, the domains
future phases will add — without specifying their models, since doing so
now would be speculative design the project brief explicitly asks us to
avoid.

## Kernel concepts (implemented)

- **`SimulationInstant` / `SimulationDuration`** — integer-second logical
  time values. See `SIMULATION_MODEL.md`.
- **`SimulationClock`** — the authoritative current instant for a world.
- **`EntityId`** — a lightweight 64-bit identifier for "some entity that
  will exist in the simulated world." No typed ids (`PersonId`,
  `CompanyId`, ...) exist yet; those belong to the phases that introduce
  those concepts.
- **`ISimulationSystem` / `SchedulingPolicy` / `SimulationScheduler`** —
  the mechanism by which behavior is attached to the passage of time.
- **`DeterministicRandom` / `RandomStreamCatalog`** — reproducible
  randomness, with independent streams derivable per future domain,
  entity, and purpose.
- **`ISimulationCommand` / `CommandDispatcher`** — a request to change
  state, synchronously handled by exactly one handler.
- **`ISimulationEvent` / `SimulationEventBus`** — a notification that
  something happened, synchronously delivered to subscribers.
- **`SimulationEngine`** — composes all of the above into one coherent
  runtime for a single simulated world.
- **`KernelSnapshotDto`** (in `Simulation.Persistence`) — the
  serializable representation of a `SimulationEngine`'s state.

None of these are gameplay concepts; they are the substrate gameplay
concepts will be built from.

## Future domains (conceptual only — not modeled here)

The following are named in `GAME_DESIGN.md` and `ROADMAP.md` purely to
give context for where the kernel is headed. **No classes, interfaces, or
data structures for these exist in this repository**, and none should be
added speculatively (see `AGENTS.md`):

- People, households, and demographics
- Employment, careers, and companies
- Goods, production, and consumption
- Property and real estate
- Banking, credit, and debt
- Financial markets
- Vehicles and transportation professions
- Jurisdictions, currencies, and law

When any of these is actually implemented, that phase should extend this
document with its real model — entities, invariants, and relationships —
rather than this repository guessing at that shape today.
