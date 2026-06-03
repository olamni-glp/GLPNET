# Contract — Normalized trace + heap→logical relabeling (FR-002)

`tools/equiv/trace.py` (model) + `tools/equiv/normalize.py` (parsers). **Pure**, no I/O beyond reading trace text; unit-tested without any runtime.

## Normalized trace
An ordered list of `Event`, each:
```
Event = {
  seq:        int                 # capture order within the run
  kind:       UNIFY | SUSPEND | REACTIVATE | WRITER_BIND | BYTECODE_OP
  payload:    dict                # kind-specific, addresses already relabeled
  causes:     set[int]            # seq ids of data-dependence predecessors (causal edges)
}
```

### Event kinds + payloads (the ONLY compared fields)
- `UNIFY`        → `{outcome: success|suspend|fail, vars: [logical_var…]}`
- `SUSPEND`      → `{reader: logical_var, goal: goal_id}`
- `REACTIVATE`   → `{writer: logical_var, goal: goal_id}`
- `WRITER_BIND`  → `{writer: logical_var, value_shape: canonical_term_shape}`
- `BYTECODE_OP`  → `{opcode: str, logical_pc: int}`   ← the spine (FR-003)

## Heap→logical relabeling (FR-002)
First-occurrence canonicalization, per run: the i-th distinct heap address encountered → logical var `v_i`. Applied at parse time so payloads never contain raw addresses. The two runs are relabeled independently; the relation (separate contract) matches by structural/causal position, not by label value — so `v_3` in golden need not equal `v_3` in candidate.

## Causal-edge derivation (`causes`)
A data-dependence edge `e_j ∈ causes(e_k)` iff `e_k` reads/binds a logical var written/bound by `e_j` (writer-MGU: only writers bind; readers depend on the writer that bound them). Bytecode-op spine events are totally ordered within a single computation (sequential PC), so on the strict tier `causes` yields a total order; on the dynamic tier cross-agent events may be unordered.

## Outcome-only mode (bonds, FR-005)
`normalize.py` also produces an `Outcome = {status: succeed|suspend|fail, bindings: canonical}` with NO event list. `compare_mode = outcome` uses only this (no trace diff) — escrow-timer suspension is a valid status, compared as-is.

## Dart vs C# parsing (R10)
`normalize.py` has a `parse_dart(:trace text)` and `parse_csharp(trace text)` producing the SAME model. The Dart golden is **not modified**; if a needed event is absent from Dart's `:trace`, that is a spec gap → STOP & report (CLAUDE.md), not a normalizer workaround.

## Tests
- relabeling stability (same addresses → same logical vars within a run).
- two runs differing only in address values normalize to structurally identical traces (SC-005 false-divergence guard).
- bonds outcome extraction ignores interleaving.
