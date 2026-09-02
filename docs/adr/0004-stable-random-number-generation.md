# 0004. Use an explicitly specified PRNG (PCG32), not System.Random

## Status
Accepted.

## Context
Save/load equivalence requires that random number generator state can be
captured and restored exactly, and that the *sequence* of numbers a seed
produces never silently changes underneath us. `System.Random`'s
internal algorithm is not documented as a stable, persistable contract
across .NET versions, and `string.GetHashCode()` /
`object.GetHashCode()` are explicitly randomized per process by the
runtime — neither is suitable for anything that must be reproducible
across processes or persisted to a save file.

## Decision
Implement `DeterministicRandom`, a small, self-contained PCG32 generator
(permuted congruential generator, XSH-RR variant), with:
- An explicit, tiny amount of state (two `ulong`s) that can be captured
  and restored exactly.
- A public, stable, well-documented algorithm (not a cryptographic RNG;
  never used for anything security-sensitive).
Independent streams are derived deterministically via
`RandomStreamCatalog`, using `StableHash` (FNV-1a, a public, stable
64-bit hash) rather than any runtime-provided hash code.

## Consequences
- Save files can capture exact RNG state and resume bit-for-bit
  identical sequences.
- `DeterministicRandomTests` pins known output vectors for a fixed
  seed/stream; changing the algorithm's output is a breaking change that
  must bump the snapshot format version (see
  `docs/PERSISTENCE_STRATEGY.md`).
- We accept the (small) maintenance cost of owning this algorithm instead
  of using a BCL type, in exchange for a stable contract the BCL does not
  offer.
