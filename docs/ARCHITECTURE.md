# Architecture

## Dependency graph

```text
Simulation.Headless
        │
        ▼
Simulation.Core  ◄──────────  Simulation.Persistence
        ▲
        │
Simulation.Benchmarks

Simulation.Core.Tests        ──depends on──► Simulation.Core
Simulation.Persistence.Tests ──depends on──► Simulation.Core, Simulation.Persistence
```

Concretely:

- **`Simulation.Core`** depends on nothing else in this repository. It is
  a pure C# class library.
- **`Simulation.Persistence`** depends only on `Simulation.Core`.
- **`Simulation.Headless`** depends on `Simulation.Core` and
  `Simulation.Persistence`.
- **`Simulation.Benchmarks`** depends on `Simulation.Core` and
  `Simulation.Persistence` (the latter only to benchmark snapshot
  serialization).
- Test projects depend only on the project(s) they test.

This is enforced by `ProjectReference` entries in each `.csproj`, not by
convention alone — see each project file for the authoritative statement.

## Project responsibilities

| Project | Responsibility |
|---|---|
| `Simulation.Core` | Logical time, deterministic scheduler, entity ids, deterministic PRNG and stream derivation, command/event infrastructure, the top-level `SimulationEngine`. No I/O, no filesystem, no console. |
| `Simulation.Persistence` | Capturing kernel state into a versioned, serializable snapshot and restoring it. Owns the one place in this repository that knows about JSON files. |
| `Simulation.Headless` | A CLI that exercises the kernel end-to-end without any engine: constructs an engine, advances it, optionally saves/loads, and reports diagnostics. Contains the Phase 1 diagnostic probe systems (see `docs/SIMULATION_MODEL.md`). |
| `Simulation.Benchmarks` | BenchmarkDotNet baselines for kernel hot paths. |

## Core isolation

`Simulation.Core` must never reference `Simulation.Persistence`,
`Simulation.Headless`, `Simulation.Benchmarks`, or (in the future)
`Game.Godot`. It also must never reference the filesystem, console,
network, or any UI framework. This is what keeps the simulation testable
in complete isolation and portable to whatever frontend eventually
consumes it.

## No Godot until Phase 9

The eventual dependency direction will be:

```text
Game.Godot
     │
     ▼
Simulation.Core (+ Simulation.Persistence)
```

**Never the reverse.** Until Phase 9, no project in this repository
references Godot, defines a `Node`, uses Godot math types (`Vector2`,
`Vector3`, `Transform`, etc.), or contains anything resembling a
`GodotSimulationManager`/`GameEngineBridge` adapter. The simulation is a
plain .NET library that a future engine will consume, not the other way
around.

## Deterministic simulation

Given the same world seed, the same sequence of commands, and the same
total duration advanced, the kernel must produce the same observable
state — regardless of whether that duration was advanced in one call or
split across many (including across a save/load boundary). This is
enforced by automated tests; see `TESTING_STRATEGY.md`.

Practical consequences enforced throughout the codebase:

- No `DateTime.Now` / `DateTime.UtcNow` in simulation logic.
- No `System.Random` (whose algorithm is not a stable, persistable
  contract) — see `DeterministicRandom` and `docs/adr/0004-*`.
- No `Guid.NewGuid()` for simulation state — see `EntityId`.
- No reliance on hash table/dictionary enumeration order for anything
  observable; the scheduler's tie-breaking is explicit and documented.
- No wall-clock-driven timers anywhere in `Simulation.Core`.

## Scheduler model

The kernel does not run a single "update every entity every frame" loop.
Instead, `ISimulationSystem` implementations register with
`SimulationScheduler` under an explicit `SchedulingPolicy` (a fixed
cadence, e.g. "every simulated hour"). When the clock advances, the
scheduler computes exactly which occurrences are due across the advanced
range and executes them in a single deterministic order: primarily by
scheduled instant, and secondarily by system registration order for
occurrences that land on the same instant. See `SIMULATION_MODEL.md` for
the full rationale, including why this matters for the "simulate a
century in seconds" requirement and for the future performance/LOD
strategy.

## Threading policy

The kernel is **single-threaded and non-reentrant by design** in this
phase. `SimulationEngine`, `SimulationScheduler`,
`SimulationEventBus`, and `CommandDispatcher` are not thread-safe and
carry no internal locking. This is a deliberate baseline (see
`docs/adr/0003-deterministic-single-threaded-kernel.md`): it guarantees
reproducible behavior and gives us a correctness reference to validate
any future parallel implementation against, once profiling justifies
that work.

## Persistence boundary

`Simulation.Core` exposes its kernel state (world seed, clock instant,
entity ID allocator cursor, event sequence number) via `KernelSnapshotDto`.
The headless runner's probe systems capture their own state separately
via `ProbeSystemSnapshot` and store it in the snapshot's `ProbeSystems`
dictionary, keyed by system ID. This keeps Core independent of specific
gameplay domains while allowing external systems to persist their state.
See `PERSISTENCE_STRATEGY.md`.

## Dependency policy (external packages)

No dependency is added without a concrete reason recorded in
`Directory.Packages.props`. In this phase that means: `xunit` (testing,
per project brief), `BenchmarkDotNet` (performance baselines, per project
brief). Nothing else. In particular: no DI container, no ORM, no
mediator/messaging framework, no distributed-systems technology of any
kind (no HTTP/REST/gRPC/message broker/database server) — this is a
single-player, single-process application. See
`docs/adr/0005-no-microservices.md`.

## Architecture invariants (do not violate without an ADR)

1. `Simulation.Core` has zero internal project dependencies.
2. Nothing in this repository references Godot before Phase 9.
3. The kernel is single-threaded; no locks, no `Task.Run`, no
   `Parallel.*`, no background threads in `Simulation.Core`.
4. All simulation-relevant randomness flows through
   `DeterministicRandom`/`RandomStreamCatalog`, never `System.Random`.
5. All simulation-relevant identity uses `EntityId`, never `Guid`, for
   entities living inside the simulated world.
6. Any change to `KernelSnapshotDto`'s schema bumps
   `KernelSnapshotWriter.CurrentFormatVersion` and is rejected explicitly
   by `KernelSnapshotReader` if mismatched — never silently
   reinterpreted.
