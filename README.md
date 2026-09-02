# CapitalismSim

A systemic, single-player capitalism/economy simulator, engineered
engine-agnostic from day one. `CapitalismSim` is a **working name** for the
repository, not a final product title.

## What this is (and is not)

This is a **socioeconomic simulation kernel**, not a game engine project.
The long-term vision (see [`docs/VISION.md`](docs/VISION.md)) is a
single-player game where careers, companies, banking, real estate, and
markets all affect each other through one shared, deterministic
simulation. A future Godot .NET frontend will visualize that simulation —
it does not exist yet, and nothing in this repository depends on it.

## Current state: Phase 0 + Phase 1

This delivery implements exactly two phases from
[`docs/ROADMAP.md`](docs/ROADMAP.md):

- **Phase 0 — Technical constitution**: repository layout, coding
  standards, CI, and foundational documentation.
- **Phase 1 — Simulation Kernel**: logical time, a deterministic
  scheduler, entity ids, a deterministic PRNG with independent stream
  derivation, command/event infrastructure, a headless runner, kernel
  snapshotting, and benchmarks.

No economic domain (people, jobs, companies, money, property, banking,
markets, vehicles, crime, law enforcement, or anything else from Phase 2
onward) is implemented. See [`docs/GAME_DESIGN.md`](docs/GAME_DESIGN.md)
for what's planned and explicitly marked as future.

**Godot is not integrated.** It will not be until Phase 9. See
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Repository structure

```text
CapitalismSim/
├── .github/workflows/     CI (restore, build, test)
├── docs/                  Vision, architecture, design, ADRs, roadmap
├── scripts/verify.sh      One-shot local validation script
├── src/
│   ├── Simulation.Core/         Pure C# simulation kernel (no dependencies)
│   ├── Simulation.Persistence/  Kernel snapshot save/load (JSON, Phase 1)
│   ├── Simulation.Headless/     CLI runner, no engine required
│   └── Simulation.Benchmarks/   BenchmarkDotNet baselines
├── tests/
│   ├── Simulation.Core.Tests/
│   └── Simulation.Persistence.Tests/
├── AGENTS.md              Rules and commands for coding agents
└── CapitalismSim.sln
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/) (`net10.0`, pinned in
  `global.json`)
- Linux, macOS, or Windows (the kernel has no OS-specific dependencies)

## Build

```bash
dotnet restore
dotnet build -c Release
```

## Test

```bash
dotnet test -c Release
```

## Run the headless simulation

The simulation runs entirely without any engine or graphical frontend:

```bash
dotnet run --project src/Simulation.Headless -c Release -- \
  --seed 123456 \
  --duration-days 365
```

Other options:

```bash
# Verify that two runs with the same seed produce the same fingerprint.
dotnet run --project src/Simulation.Headless -c Release -- \
  --seed 123456 --duration-days 365 --verify-determinism

# Save a kernel snapshot after advancing.
dotnet run --project src/Simulation.Headless -c Release -- \
  --seed 123456 --duration-days 180 --save /tmp/mysave.json

# Resume from a snapshot and keep advancing.
dotnet run --project src/Simulation.Headless -c Release -- \
  --load /tmp/mysave.json --duration-days 185
```

Run `--help` for the full option list.

## Run the benchmarks

```bash
dotnet run --project src/Simulation.Benchmarks -c Release
```

See [`docs/PERFORMANCE_BUDGET.md`](docs/PERFORMANCE_BUDGET.md) for how to
interpret results — this phase establishes a baseline, not a fixed target.

## Verification script

```bash
./scripts/verify.sh
```

Runs restore, build, test, and a headless smoke test in one shot.

## Documentation index

| Document | Purpose |
|---|---|
| [VISION.md](docs/VISION.md) | Why this project exists, at a glance |
| [GAME_DESIGN.md](docs/GAME_DESIGN.md) | Planned systems, current vs. future |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Dependency graph, invariants, threading |
| [DOMAIN_MODEL.md](docs/DOMAIN_MODEL.md) | What Phase 1 actually models |
| [SIMULATION_MODEL.md](docs/SIMULATION_MODEL.md) | Time, scheduling, LOD philosophy |
| [PERFORMANCE_BUDGET.md](docs/PERFORMANCE_BUDGET.md) | Benchmarking approach |
| [TESTING_STRATEGY.md](docs/TESTING_STRATEGY.md) | Test categories and coverage |
| [PERSISTENCE_STRATEGY.md](docs/PERSISTENCE_STRATEGY.md) | Save format now and later |
| [ROADMAP.md](docs/ROADMAP.md) | Phases 0–9 |
| [AI_DEVELOPMENT_WORKFLOW.md](docs/AI_DEVELOPMENT_WORKFLOW.md) | How AI agents should work on this repo |
| [adr/](docs/adr/) | Architecture Decision Records |

## Roadmap (summary)

Phases 0–1 are implemented here. Phases 2–8 (people, companies, property,
banking, markets, consolidation/stress-testing) are future headless-only
milestones. Phase 9 is the first Godot integration. See
[`docs/ROADMAP.md`](docs/ROADMAP.md) for details.
