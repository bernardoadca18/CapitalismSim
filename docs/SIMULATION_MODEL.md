# Simulation Model

## Logical time

The kernel represents time as an integer count of simulated seconds since
an arbitrary epoch (`SimulationInstant.Epoch`), via `SimulationInstant`
(a point in time) and `SimulationDuration` (a span of time). This is
deliberately **not** `float`/`double`-based: floating point accumulates
rounding error over the long simulated durations this project targets
(the brief explicitly mentions simulating decades or centuries), which
would eventually corrupt scheduling and break determinism. Integer
arithmetic with explicit overflow checking (`checked` operators) has
neither problem.

No civil calendar (days-of-week, months, leap years, time zones) exists
yet. `SimulationDuration.FromDays`/`FromHours`/`FromMinutes` are just
fixed-size integer multiples of a second, not calendar-aware. A calendar
system, if one is ever needed, can be layered on top of this without
changing `SimulationInstant`/`SimulationDuration`'s contract.

## The logical clock

`SimulationClock` holds the current instant for a world. It only moves
forward via `Advance`/`AdvanceTo`, driven exclusively by explicit calls
from `SimulationEngine`. It never reads wall-clock time and is never
tied to a frame rate or an OS timer. This is what allows a headless run
to simulate, say, one hundred years in a few seconds — the constraint is
CPU time to process scheduled work, not real elapsed time.

## Deterministic scheduling

Real gameplay systems will eventually need to run at wildly different
cadences: local interaction at high frequency, transactions on the order
of minutes, employment and real estate on the order of days,
macroeconomics on the order of months. A single "tick every entity every
frame" loop cannot express this efficiently or clearly.

Instead, each `ISimulationSystem` registers with `SimulationScheduler`
under a `SchedulingPolicy` describing a fixed interval (e.g., "every
simulated hour, starting at instant zero"). When the engine advances the
clock from instant A to instant B, the scheduler:

1. For each registered system with due occurrences in `(A, B]`, computes
   the next occurrence index and pushes it onto a min-heap.
2. Repeatedly extracts the minimum entry from the heap, executes that
   occurrence, and pushes the system's next occurrence (if any) back onto
   the heap.
3. Orders entries **deterministically** by (scheduled instant, registration
   order) — registration order breaks ties when multiple systems are due
   at the same instant.

This streaming approach uses O(S) auxiliary memory (one heap entry per
system with pending work) and O(E log S) time for E due occurrences across
S systems, avoiding the O(E log E) cost of collecting and sorting all
occurrences upfront.

This guarantees, by construction, that:

- No occurrence is ever skipped, however large the jump from A to B.
- No occurrence ever fires twice.
- Splitting one large advance into many smaller advances (as a save/load
  cycle necessarily does) produces the exact same sequence of
  occurrences as one large advance covering the same total range —
  because the occurrence index for a given instant is a pure function of
  that instant and the policy, not of how execution happened to be
  chunked.
- The order two systems run in in the same instant is documented and
  reproducible, rather than depending on hash table iteration order or
  any other incidental detail.

## Random streams

Simulation randomness is produced by `DeterministicRandom`, a small,
explicitly specified PCG32 generator (see `docs/adr/0004-*`), never by
`System.Random` (whose internal algorithm Microsoft does not guarantee as
a stable contract across .NET versions).

To prevent unrelated future systems from perturbing each other's random
sequences — the brief's example: adding a random draw to a future
real-estate system must never change the numbers a future population
generator produces — `RandomStreamCatalog` derives an independent stream
for every distinct combination of:

```text
world seed + domain name + (optional) entity identity + (optional) purpose
```

using a stable 64-bit hash (`StableHash`, FNV-1a) rather than
`string.GetHashCode()` or `object.GetHashCode()`, both of which are
explicitly documented by Microsoft as randomized per process and
unsuitable for anything persisted. No concrete domains ("Population",
"RealEstate", etc.) are defined in Phase 1 — this is purely the
derivation mechanism, exercised today by diagnostic probe systems.

## Simulation LOD (level of detail) — philosophy only

A world with a large population cannot be represented as that many live,
individually-updated objects without catastrophic performance cost. The
long-term intent (not implemented in Phase 1) is a spectrum of
resolution:

```text
distant country      → aggregated macroeconomic figures
distant city          → aggregated population/economic statistics
relevant region       → more detailed economic entities
NPC near the player   → a fully materialized individual
important NPC         → a persistent, detailed individual
```

Entities gain resolution ("materialize") only when something — typically
player proximity or narrative importance — actually needs it, and can
conceptually be "dematerialized" back into an aggregate when it no
longer does. Phase 1 does not implement population, aggregation, or
materialization. What it does do is avoid foundational choices that would
make this impossible later: the scheduler does not assume "one system
call per live entity per tick," and random stream derivation already
supports being scoped to an entity identity without requiring that
entity to exist as a persistent object today.

## No per-frame requirement

Nothing in the kernel requires or assumes a rendering frame rate. A
future Godot frontend (Phase 9+) will drive the simulation forward by
calling `SimulationEngine.AdvanceBy`/`AdvanceTo` on its own schedule
(likely much less often than every rendered frame); the simulation itself
has no opinion about frames.
