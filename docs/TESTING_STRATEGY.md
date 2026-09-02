# Testing Strategy

## Categories

- **Unit tests** (`tests/Simulation.Core.Tests`,
  `tests/Simulation.Persistence.Tests`) — xUnit, covering each kernel
  component in isolation: time value types, the clock, the scheduler, the
  PRNG and stream derivation, entity id allocation, command dispatch, the
  event bus, the engine, and snapshot capture/apply/store.
- **Deterministic/property-style tests** — rather than pulling in a
  property-based testing framework, several tests loop over multiple
  seeds/durations/split-points and assert an invariant holds for all of
  them (see `SaveLoadEquivalenceTests`'s `[Theory]` case and
  `SimulationSchedulerTests`'s large-advance and split-advance cases).
  This gives good confidence without adding a dependency the project
  brief doesn't call for.
- **Determinism tests** — assert that two independently constructed runs
  with the same seed and inputs produce identical observable state and
  identical fingerprints.
- **Save/load equivalence tests** — the single most important acceptance
  test for this phase (see below).
- **Benchmark baselines** (`src/Simulation.Benchmarks`) — not
  correctness tests, but part of the overall quality bar; see
  `PERFORMANCE_BUDGET.md`.
- **Future: long-running simulation tests, regression suites** — not
  implemented in Phase 1; there is no economic domain yet to regress.

## The save/load equivalence test

`SaveLoadEquivalenceTests.ContinuousRun_MatchesSaveLoadContinueRun_ForSameTotalDuration`
(in `tests/Simulation.Persistence.Tests`) implements the project brief's
central acceptance criterion:

```text
Run A: seed S, advance N minutes continuously.
Run B: seed S, advance N/2 minutes, save, load into a fresh engine,
       advance the remaining N/2 minutes.

Run A's final fingerprint must equal Run B's final fingerprint.
```

A companion `[Theory]` runs the same shape of test across multiple seeds
and durations, including a split that does not fall on a round number, to
reduce the chance that the plain case happens to hide an off-by-one.

This test intentionally does not depend on `Simulation.Headless`; it
defines its own minimal scheduled system inline so that the persistence
layer's contract is proven independently of the CLI.

## Coverage by area

| Area | Test file(s) |
|---|---|
| Time | `Time/SimulationInstantAndDurationTests.cs`, `Time/SimulationClockTests.cs` |
| Scheduler | `Scheduling/SimulationSchedulerTests.cs` |
| Entity ids | `Entities/EntityIdAllocatorTests.cs` |
| RNG | `Randomness/DeterministicRandomTests.cs`, `Randomness/RandomStreamCatalogTests.cs`, `Randomness/StableHashTests.cs` |
| Commands | `Commands/CommandDispatcherTests.cs` |
| Events | `Events/SimulationEventBusTests.cs` |
| Engine | `Engine/SimulationEngineTests.cs` |
| Persistence | `Snapshots/KernelSnapshotRoundTripTests.cs`, `Snapshots/SaveLoadEquivalenceTests.cs` |

## Known test vectors

`DeterministicRandomTests.KnownVector_Seed42Stream54_MatchesRecordedSequence`
and `StableHashTests.KnownVector_HelloString_MatchesRecordedHash` pin
exact output values captured from this implementation. If either test
ever needs to change, that means the underlying algorithm's *output
sequence* changed — which is a breaking change for any save file that
depends on it, and must be treated as such (bump
`KernelSnapshotWriter.CurrentFormatVersion` and document the break).

## Running the suite

```bash
dotnet test -c Release
```

## Environment note on this delivery

The sandbox this repository was authored in restricts outbound network
access to a fixed allow-list of hosts that does not include
`api.nuget.org`. This means `xunit` and `BenchmarkDotNet` — both
NuGet-only packages — could not be restored inside that sandbox, so
`dotnet test` and the benchmark project could not be executed there.
`Simulation.Core`, `Simulation.Persistence`, and `Simulation.Headless`
have **no external NuGet dependencies** and were fully built and
exercised (including manual determinism and save/load smoke tests via
the headless CLI). Any environment with normal internet access (including
the CI workflow in `.github/workflows/`) will restore and run the full
test suite normally.
