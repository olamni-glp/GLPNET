# Contract — Depgraph CLI additions (US1)

## `depgraph mark-and-recompute`
- **Input**: `--mark <path> [--mark <path> ...]` (one or more file paths in the project).
- **Behaviour**: marks the given nodes + their transitive dependents dirty; recomputes only the
  dirty subgraph; preserves all unmarked results.
- **Output**: summary of nodes recomputed vs preserved. Unknown paths → reported, recomputed
  nothing (exit 1, no fabricated nodes).
- **Persistence**: additive catalog rows only; no new schema head.

## `depgraph trends`
- **Input**: `--runs <run_id> <run_id> [...]` (≥2) or a selector resolving to ≥2 recorded runs.
- **Behaviour**: computes per-metric deltas across the runs.
- **Output**: deterministic, secret-redacted report; byte-identical on unchanged inputs
  (timestamp filename-only). `<2` runs → exit 1 "at least two runs required".

## Exit codes
- `0` success/no-op · `1` refused (unknown path, <2 runs) · `2` PGLite bridge unavailable.
