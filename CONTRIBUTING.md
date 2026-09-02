# Contributing

This repository is developed largely with AI assistance under the
workflow described in `docs/AI_DEVELOPMENT_WORKFLOW.md`. If you are a
human contributor, the same principles apply.

## Before you start

1. Read `docs/ARCHITECTURE.md` and `AGENTS.md`. They describe invariants
   that are treated as hard constraints, not suggestions (dependency
   direction, determinism, no Godot before Phase 9, no speculative
   features).
2. Check `docs/ROADMAP.md` to understand which phase is currently in
   scope. Work belonging to a later phase should wait for that phase.

## Making a change

1. `dotnet build -c Release` and `dotnet test -c Release` must pass
   before you open a change for review.
2. If you touched a hot path (`SimulationScheduler`, `DeterministicRandom`,
   `RandomStreamCatalog`), run the relevant `Simulation.Benchmarks` suite
   before and after, and mention the result.
3. If you changed the kernel snapshot schema
   (`KernelSnapshotDto`), bump `KernelSnapshotWriter.CurrentFormatVersion`
   and update `docs/PERSISTENCE_STRATEGY.md`.
4. If you changed the dependency graph or a documented architectural
   invariant, update `docs/ARCHITECTURE.md` in the same change.
5. Run `./scripts/verify.sh` before submitting.

## Style

Follow `.editorconfig` and the analyzer configuration in
`Directory.Build.props` (`TreatWarningsAsErrors` is enabled for this
repository's own code). Prefer explicit, readable code over clever
abstractions — see `AGENTS.md`'s "no speculative features" rule and
`docs/ARCHITECTURE.md`'s dependency policy for the kind of restraint this
project expects.

## Commit scope

Keep changes scoped to the phase/task at hand. Do not opportunistically
start work on a later roadmap phase "while you're in there" — see
`AGENTS.md`, rule 7.
