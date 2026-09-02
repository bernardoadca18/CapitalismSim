# AGENTS.md

Short, load-bearing rules for any coding agent (or human) working on this
repository. See `docs/AI_DEVELOPMENT_WORKFLOW.md` for the full workflow
this supports.

## Mandatory rules

1. **`Simulation.Core` stays independent.** Zero references to
   `Simulation.Persistence`, `Simulation.Headless`,
   `Simulation.Benchmarks`, or `Game.Godot`. Zero filesystem, console, or
   network I/O inside `Simulation.Core`.
2. **No Godot until Phase 9.** No Godot package reference, no `Node`, no
   `Vector2`/`Vector3`/`Transform`/`Resource`, no
   `GodotSimulationManager`/`GameEngineBridge`-style adapter, anywhere in
   this repository, until a task explicitly starts Phase 9.
3. **No microservices / distributed architecture.** No HTTP server, no
   message broker, no database server, no containers, no DI container.
   See `docs/adr/0005-no-microservices.md`.
4. **Determinism first.** No `DateTime.Now`/`UtcNow`, no
   `System.Random`/`Random.Shared`, no `Guid.NewGuid()` for simulation
   state, no reliance on dictionary/hash-set enumeration order, inside
   `Simulation.Core`. Use `SimulationClock`, `DeterministicRandom`
   /`RandomStreamCatalog`, and `EntityIdAllocator` instead.
5. **Test all changes.** `dotnet test -c Release` must pass. Changes to
   the scheduler or persistence format must keep
   `SaveLoadEquivalenceTests` passing and usually need new cases.
6. **Benchmark hot paths you touch.** See `docs/PERFORMANCE_BUDGET.md`.
   Changes to `SimulationScheduler`, `DeterministicRandom`, or
   `RandomStreamCatalog` should be benchmarked before/after.
7. **No speculative features.** Do not implement, or scaffold empty
   classes for, any system marked FUTURE in `docs/GAME_DESIGN.md` or
   `docs/ROADMAP.md` unless the current task explicitly asks for that
   phase.
8. **Keep the dependency graph clean.** See `docs/ARCHITECTURE.md`'s
   dependency graph. Justify any new NuGet package with a one-line
   comment in `Directory.Packages.props`.
9. **Preserve public contracts** (types, method signatures, the snapshot
   schema) **unless the task requires changing them.** If you do change
   `KernelSnapshotDto`, bump `KernelSnapshotWriter.CurrentFormatVersion`.
10. **Update docs for architectural changes.** If you touch the
    dependency graph, the persistence schema, or a documented invariant,
    update the corresponding file under `docs/` in the same change.

## Essential commands

```bash
# Restore, build, test
dotnet restore
dotnet build -c Release
dotnet test -c Release

# One-shot local verification (restore + build + test + headless smoke test)
./scripts/verify.sh

# Run the headless simulation
dotnet run --project src/Simulation.Headless -c Release -- \
  --seed 123456 --duration-days 365

# Verify two runs with the same seed produce the same fingerprint
dotnet run --project src/Simulation.Headless -c Release -- \
  --seed 123456 --duration-days 365 --verify-determinism

# Save and resume a kernel snapshot
dotnet run --project src/Simulation.Headless -c Release -- \
  --seed 123456 --duration-days 180 --save /tmp/save.json
dotnet run --project src/Simulation.Headless -c Release -- \
  --load /tmp/save.json --duration-days 185

# Run benchmarks
dotnet run --project src/Simulation.Benchmarks -c Release
```

## Where to look first

- `docs/ARCHITECTURE.md` — dependency graph and invariants.
- `docs/SIMULATION_MODEL.md` — time, scheduling, and randomness model.
- `docs/PERSISTENCE_STRATEGY.md` — snapshot schema and versioning rules.
- `docs/ROADMAP.md` — what phase comes next, and what is explicitly not
  yet in scope.
- `docs/adr/` — why specific foundational decisions were made.
