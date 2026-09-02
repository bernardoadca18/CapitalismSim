# 0001. Single monorepo for the whole project

## Status
Accepted.

## Context
The project will eventually include a simulation kernel, a persistence
layer, a headless CLI, benchmarks, tests, and (from Phase 9 onward) a
Godot .NET frontend. These pieces are developed together, largely by AI
coding agents, and change frequently in ways that span project
boundaries during early phases.

## Decision
Keep the entire project — including the future `Game.Godot` project — in
a single repository (`CapitalismSim`), with internal boundaries enforced
by project references and documented invariants (see
`docs/ARCHITECTURE.md`) rather than by repository separation. No git
submodules are used.

## Consequences
- Simple, atomic commits/PRs across layers during early development.
- Engine independence must be enforced by convention and review (and
  ideally, eventually, an architecture test), not by repository
  boundaries alone.
- Adding `Game.Godot` in Phase 9 is a matter of adding a new project
  folder under `src/`, not standing up a new repository.
