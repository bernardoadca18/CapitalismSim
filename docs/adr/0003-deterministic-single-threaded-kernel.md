# 0003. The kernel is deterministic and single-threaded in this phase

## Status
Accepted.

## Context
The simulation must eventually support save/load equivalence
(`docs/PERSISTENCE_STRATEGY.md`) and reproducible behavior given the same
seed and inputs. Concurrency (locks, `Parallel.For`, background threads,
actors, channels) introduces non-determinism unless very carefully
designed and validated, which is disproportionate effort for a
foundational phase that has no performance data yet to justify it.

## Decision
For Phase 1, `SimulationEngine`, `SimulationScheduler`,
`SimulationEventBus`, and `CommandDispatcher` are single-threaded and
not thread-safe by design. No locks, no concurrent collections, no
`Task.Run`/`Parallel.*`/PLINQ, no actor or channel framework is used
anywhere in `Simulation.Core`.

## Consequences
- Behavior is trivially reproducible and easy to debug.
- This single-threaded implementation becomes the correctness reference
  a future parallel implementation (introduced only after profiling
  justifies it) must be validated against.
- Callers must not call into engine APIs concurrently from multiple
  threads; this is documented on the relevant types rather than enforced
  at runtime, to avoid paying for locking overhead not required by this
  phase's usage.
