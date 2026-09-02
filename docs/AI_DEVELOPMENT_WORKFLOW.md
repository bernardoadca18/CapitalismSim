# AI Development Workflow

This repository is developed primarily with AI assistance. This document
describes the expected workflow and the rules that keep multiple AI
agents (and human reviewers) working coherently on the same codebase over
time.

## Expected pipeline

```text
specification (architect / human / planning model)
        │
        ▼
bounded implementation (coding agent, e.g. OpenCode + a coding model)
        │
        ▼
automated tests (dotnet test)
        │
        ▼
user execution / manual smoke check (headless runner, benchmarks)
        │
        ▼
architectural audit (review against ARCHITECTURE.md and this file)
        │
        ▼
correction
        │
        ▼
next phase
```

Each step should be completed, not skipped, before moving to the next.
In particular: do not let "the code compiles" substitute for "the tests
pass," and do not let "the tests pass" substitute for "this respects the
architecture."

## Rules for agents working on this repository

1. **Do not expand scope without instruction.** If a task specifies
   Phase N, implement Phase N. Do not begin Phase N+1's domain work
   "while you're in there," even if it looks small or obviously useful.
2. **Do not create speculative/future systems.** No empty classes for
   systems that don't exist yet (see `GAME_DESIGN.md` for the list of
   what's future). If a phase needs a new domain concept, that phase's
   specification should introduce it explicitly — don't pre-build it.
3. **Update docs when contracts change.** If you change
   `KernelSnapshotDto`'s shape, a public API in `Simulation.Core`, or the
   dependency graph, update the corresponding doc
   (`PERSISTENCE_STRATEGY.md`, `ARCHITECTURE.md`, etc.) in the same
   change. Documentation drift is treated as a defect.
4. **Respect the dependency direction.** `Simulation.Core` depends on
   nothing internal. Never add a reference that would make it depend on
   `Simulation.Persistence`, `Simulation.Headless`,
   `Simulation.Benchmarks`, or (once it exists) `Game.Godot`.
5. **Never introduce Godot into `Simulation.Core`.** Not a `using`, not a
   type, not a comment implying it's coming soon inside Core itself. See
   `ARCHITECTURE.md`.
6. **Do not sacrifice determinism without an explicit architectural
   decision.** If a change would introduce `DateTime.Now`,
   `System.Random`, `Guid.NewGuid()` for simulation state, or any other
   source of non-determinism into `Simulation.Core`, stop and write an
   ADR justifying it first (see `docs/adr/`) rather than merging it
   silently.
7. **Test every change.** New public behavior needs new tests. Changes to
   the scheduler, RNG, or persistence format need their existing tests
   (especially `SaveLoadEquivalenceTests`) to still pass, and usually need
   new cases added.
8. **Benchmark hot paths you touch.** If you change
   `SimulationScheduler.RunDueOccurrences`, `DeterministicRandom`, or
   `RandomStreamCatalog`, run the relevant `Simulation.Benchmarks` suite
   before and after and note the result in your change description (see
   `PERFORMANCE_BUDGET.md`).
9. **Preserve public contracts unless the task requires changing them.**
   This is a young codebase without a stability guarantee yet, but
   gratuitous renames/reshuffling make it harder for other agents (and
   humans) to track what changed and why.
10. **Keep the dependency graph clean.** Before adding a NuGet package,
    check `Directory.Packages.props` — does something already there solve
    this? Adding a new package requires a one-line justification comment
    next to its `<PackageVersion>` entry.

## Command reference

See `AGENTS.md` for the exact commands to restore, build, test, run the
headless simulation, and run benchmarks.
