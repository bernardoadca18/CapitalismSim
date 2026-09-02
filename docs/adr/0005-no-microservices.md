# 0005. No distributed architecture of any kind

## Status
Accepted.

## Context
This is a single-player game. There is no multiplayer, no server, and no
need for the simulation to run outside the same process as its host
application (today: the headless CLI; eventually: the Godot frontend).

## Decision
No HTTP/REST/gRPC, no message broker (Kafka, RabbitMQ, MassTransit), no
distributed actor framework (Orleans, Akka.NET), no database server
(PostgreSQL, Redis), no containerization (Docker/Kubernetes), and no
dependency injection container are used anywhere in this repository.
Independence between `Simulation.Core` and its future consumers is
achieved entirely through the in-process dependency graph
(`docs/ARCHITECTURE.md`), not through process or network boundaries.

## Consequences
- Radically simpler deployment: the game is one process.
- Command and event dispatch (`CommandDispatcher`, `SimulationEventBus`)
  are synchronous and in-process by design — they are not, and must not
  become, thin wrappers around a message queue.
- If a genuine need for out-of-process communication ever arises (e.g., a
  companion tool), it should be justified by a new ADR at that time, not
  assumed now.
