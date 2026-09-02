# Performance Budget

## Philosophy

Performance matters architecturally (see `docs/VISION.md` and
`docs/ARCHITECTURE.md`'s threading policy), but Phase 1 does not invent
absolute performance numbers to promise. The goal here is to establish a
**measured baseline** with `Simulation.Benchmarks`, so that:

1. Future phases can tell whether a change made things faster or slower.
2. Optimization work is driven by actual measurement, not guesswork —
   per the project brief: "simple architecture + benchmark +
   measurement-driven optimization," not premature or obscure
   optimization.

Benchmarks are not unit tests. Nothing in CI fails because a benchmark
number regressed by some percentage; that kind of gating requires a
stable, dedicated benchmarking machine, which this phase does not set up.

## What's benchmarked today (`Simulation.Benchmarks`)

| Suite | What it measures |
|---|---|
| `RandomnessBenchmarks` | `DeterministicRandom` draw throughput (`uint`/`ulong`/`double`) and `RandomStreamCatalog` stream derivation cost |
| `EntityIdBenchmarks` | Entity id allocation throughput |
| `SchedulerBenchmarks` | Scheduler throughput advancing one simulated day with a handful (5) vs. many (200) registered systems |
| `DispatchBenchmarks` | Event publish and command dispatch throughput |
| `SnapshotBenchmarks` | Kernel snapshot capture, JSON serialization, and JSON deserialization cost |
| `SimulationAdvanceBenchmarks` | End-to-end cost of advancing a wired-up engine by one simulated year |

Run them with:

```bash
dotnet run --project src/Simulation.Benchmarks -c Release
```

## Future benchmark scenarios (not implemented, for context)

As real domains are added, this document should grow scenario-level
baselines roughly like:

```text
small simulation:   a handful of entities/systems, single city
medium simulation:  thousands of entities, one country
large simulation:   population-scale, aggregated where needed (see
                    SIMULATION_MODEL.md's LOD philosophy)
stress simulation:  worst-case cadence overlap, maximum entity churn
```

None of these scenarios exist yet because the domains that would define
"an entity" in this sense (people, companies, properties) don't exist
yet. Defining them now would be speculative.

## How to measure a regression

1. Run `dotnet run --project src/Simulation.Benchmarks -c Release` on the
   `main` branch (or whatever your baseline is) and record the results
   (BenchmarkDotNet writes a report to `BenchmarkDotNet.Artifacts/`,
   which is gitignored — copy the numbers you care about into your PR
   description or an issue instead of committing that folder).
2. Run the same command on your branch.
3. Compare mean time and allocated bytes per operation for the affected
   benchmark(s). A regression worth investigating is a sustained,
   repeatable increase — not a one-off noisy sample. Re-run a benchmark
   if a single result looks like an outlier.

## Hot-path discipline

Independent of formal benchmark gating, the following are enforced by
review discipline throughout `Simulation.Core`'s hot paths (the
scheduler's due-occurrence collection, the PRNG, and stable hashing):

- No unnecessary heap allocation per tick (the scheduler reuses a scratch
  list across calls to `RunDueOccurrences` rather than allocating one per
  call).
- No reflection.
- No LINQ where a plain loop is clearer and cheaper.
- No boxing of value types in hot paths (`SimulationInstant`,
  `SimulationDuration`, `EntityId` are all `readonly struct`s).
