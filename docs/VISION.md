# Vision

## What we're building

A single-player, procedural, systemic simulation of capitalism — careers,
companies, banking, real estate, markets, vehicles, and (eventually)
jurisdictions and law — where these systems share one economic reality and
causally affect each other, rather than existing as isolated minigames.

Conceptual inspiration includes mobile business-sim games like
*Capitalism*, but the ambition is much larger: a living economy a player
can work inside of, in first person, once a rendering frontend exists.

## Core principles

1. **Simulation-first, not engine-first.** This is a socioeconomic
   simulator that will eventually grow a game engine frontend — not a
   game engine project with some economic systems bolted on. The
   simulation must be able to run, be tested, and be reasoned about with
   zero dependency on any rendering or physics engine.

2. **Systemic integration over isolated features.** A factory closing
   should be able to ripple into employment, consumption, delinquency,
   rents, property prices, other companies' results, equity markets, and
   public revenue. Systems are not implemented as walled-off minigames.

3. **Engine independence until it's actually needed.** Godot .NET is the
   planned future rendering/interaction frontend, but it is not
   introduced until Phase 9. Until then, "the game" is fully playable and
   testable as a headless simulation.

4. **Simulation LOD (level of detail).** A world with tens or hundreds of
   millions of simulated people cannot be represented as that many live
   objects. The architecture must eventually support aggregated,
   statistical representations that only "materialize" into individually
   simulated entities when a player (or another system) needs that
   resolution. Phase 1 does not implement this, but nothing in Phase 1's
   foundation should make it structurally impossible later.

5. **Performance as an architectural concern, not an afterthought.**
   Different subsystems will eventually run at wildly different cadences
   (seconds for local interaction, minutes for transactions, days for
   employment, months for macroeconomics). The kernel is built around a
   deterministic scheduler with explicit cadences instead of a single
   "update everything every frame" loop.

6. **Expandability without speculation.** We design so that future systems
   *can* be added without breaking the foundation, but we do not
   pre-build empty scaffolding for systems that don't exist yet. See
   [`docs/adr/`](adr/) and [`AGENTS.md`](../AGENTS.md) for how this
   tension is resolved in practice.

## What Phase 1 delivers toward this vision

Phase 1 delivers none of the economic domain itself. It delivers the
kernel that every future domain will be built on: deterministic logical
time, a scheduler capable of expressing wildly different cadences,
stable entity identity, reproducible randomness with independent stream
derivation (so unrelated future systems can't accidentally perturb each
other), and a snapshot mechanism proven end-to-end with a save/load
equivalence test. Everything else in this vision document is future work.
