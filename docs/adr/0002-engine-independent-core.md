# 0002. Simulation.Core is engine-independent and comes first

## Status
Accepted.

## Context
The project's identity (see `docs/VISION.md`) is "a socioeconomic
simulator that will eventually have a game frontend," not a game engine
project with economic systems attached. Godot .NET is the planned future
frontend, but is explicitly out of scope until Phase 9.

## Decision
`Simulation.Core` is a plain .NET class library with zero dependencies on
any engine, UI framework, filesystem, or console. It must be fully
buildable, testable, and runnable (via `Simulation.Headless`) without
Godot ever being installed or referenced. The eventual dependency
direction is `Game.Godot → Simulation.Core`, never the reverse.

## Consequences
- Every kernel concept (time, scheduling, randomness, entities, commands,
  events) must be expressible without any engine-specific type
  (`Vector2`, `Node`, `Resource`, etc.).
- The headless runner (`Simulation.Headless`) exists specifically to
  prove this independence end-to-end, not just as a debugging
  convenience.
- Introducing `Game.Godot` later is additive: a new project depending on
  `Simulation.Core`/`Simulation.Persistence`. No existing project should
  need to change to accommodate it.
