# Persistence Strategy

## Now: kernel snapshot (Phase 1)

`Simulation.Persistence` captures a `KernelSnapshotDto` from a running
`SimulationEngine` and can restore it onto a freshly constructed engine
with the same world seed. The schema is intentionally tiny — it captures
exactly what the Phase 1 kernel owns:

- `FormatVersion` — an explicit integer schema version.
- `WorldSeed` — must match the target engine's seed to restore.
- `CurrentInstantSeconds` — the logical clock's position.
- `NextEntityId` — the entity id allocator's cursor.
- `NextEventSequenceNumber` — the event bus's sequence counter.
- `ProbeSystems` — snapshots of diagnostic probe systems (keyed by system id).

The scheduler itself needs no snapshot entry: because
`SimulationScheduler.RunDueOccurrences` computes due occurrences purely
as a function of the previous and new clock instant plus each system's
`SchedulingPolicy`, restoring the clock correctly is sufficient to resume
scheduling exactly where it left off. This is a direct, tested
consequence of the scheduler being deterministic and stateless between
calls (see `docs/SIMULATION_MODEL.md`).

Snapshots are serialized to JSON via `System.Text.Json`
(`JsonFileKernelSnapshotStore`). An unrecognized `FormatVersion` or a
structurally invalid file throws `SnapshotFormatException` — loading
code never silently guesses at an unfamiliar format.

**This JSON format is an initial, Phase 1-only choice.** It is legible
and simple, which matters while bootstrapping and debugging the kernel.
It is not a promise about the eventual shape of the finished game's save
files.

## Future: hybrid persistence (not implemented)

As real domains (people, companies, property, etc.) are added, a single
flat JSON document will not scale to the data volumes implied by
`docs/SIMULATION_MODEL.md`'s "simulation LOD" philosophy. The expected
long-term direction — not designed in detail here, since doing so now
would be speculative — is a hybrid of:

- **Snapshots** for compact, frequently-needed state (much like today's
  kernel snapshot, extended with new top-level sections per domain).
- **Deltas** for large, slowly-changing state, so we don't need to
  rewrite an entire world's data on every save.
- **Indexed/queryable data** (plausibly SQLite) for domains that
  genuinely need random access at scale — for example, per-property or
  per-company records in a large world — rather than being deserialized
  wholesale into memory.

A guiding principle for that future work: **procedurally generated state
should preferably be reconstructed from `(seed, deltas)` rather than
persisted in full**, consistent with `RandomStreamCatalog`'s design —
if a piece of world state can be regenerated deterministically from the
world seed plus a short history of the decisions that diverged from pure
generation, storing that history is far cheaper than storing the
generated result itself.

No SQLite dependency, delta format, or indexing scheme is introduced in
this phase; that would be exactly the kind of premature, speculative
infrastructure the project brief asks us to avoid.

## Versioning discipline

Any change to `KernelSnapshotDto`'s shape must increment
`KernelSnapshotWriter.CurrentFormatVersion`, and `KernelSnapshotReader`
must explicitly reject snapshots whose version it doesn't understand
(never silently reinterpret an old or unknown format). The same
discipline should apply to any future top-level section added to the
persisted schema.
