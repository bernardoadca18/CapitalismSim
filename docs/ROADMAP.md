# Roadmap

## Phase 0 — Technical constitution ✅ implemented in this delivery

Repository layout, solution/project structure, dependency graph,
coding standards (`.editorconfig`, analyzers, `TreatWarningsAsErrors`),
CI, and foundational documentation (this file and its siblings in
`docs/`).

## Phase 1 — Simulation Kernel ✅ implemented in this delivery

Logical time (`SimulationInstant`/`SimulationDuration`), a deterministic
`SimulationClock`, a deterministic `SimulationScheduler`, `EntityId`
allocation, a deterministic PRNG (`DeterministicRandom`, PCG32) with
independent stream derivation (`RandomStreamCatalog`), synchronous
command (`CommandDispatcher`) and event (`SimulationEventBus`)
infrastructure, a top-level `SimulationEngine`, kernel snapshotting
(`Simulation.Persistence`), a headless CLI runner
(`Simulation.Headless`), a diagnostic probe system, benchmarks
(`Simulation.Benchmarks`), and an automated test suite including a
save/load equivalence test.

## Phase 2 — People, families and money ✅ implemented in this delivery

Population system with `Person` and `Household` entities, `PopulationState`
tracking both detailed individuals and aggregate cohorts (LOD architecture),
`PopulationCohort` for large-scale populations (millions without per-entity
allocation), `MoneyLedger` for double-entry accounting with supply
invariants, `BasicNeedsProfile` and `HouseholdNeedsState` for consumption
modeling, snapshot persistence for all Phase 2 state, Phase 2 benchmarks,
and `--verify-save-load` for save/load verification.

## Phase 3 — Companies and employment ✅ implemented in this delivery

Company entities with `Company` and `JobPosition` types, `CompaniesState`
managing companies and their positions with cross-domain validation,
`EmploymentContract` linking persons to job positions, `EmploymentState`
tracking contracts and payroll with wage arrears support, `PayrollState`
for tracking unpaid wages and missed pay periods, snapshot persistence
for all Phase 3 state, and extended validation for cross-domain integrity.

### Phase 3 Fingerprint (30-day simulation)

```
seed: 123456
fingerprint: 6f44a9dc15369e7e
```

## Phase 4 — Production and consumption — FUTURE

Not started.

## Phase 5 — Fundamental properties — FUTURE

Not started.

## Phase 6 — Real estate market — FUTURE

Not started.

## Phase 7 — Banks, credit and debt — FUTURE

Not started.

## Phase 8 — Headless consolidation and stress testing — FUTURE

Not started. Intended to validate Phases 2–7 together at larger scale
before any rendering frontend is introduced.

## Phase 9 — First Godot integration — FUTURE

Not started. This is the first phase in which `Game.Godot` (or
equivalent) is introduced as a new project depending on
`Simulation.Core`/`Simulation.Persistence` — never the reverse. See
`docs/ARCHITECTURE.md`.

## Beyond Phase 9 — FUTURE, unscoped

Multiple jurisdictions/currencies, additional financial markets, the
informal/underground economy, law enforcement, and combat are all named
in `GAME_DESIGN.md` as long-term ambitions with no assigned phase number
yet.
