# Contract — Causal/partial-order equivalence relation (FR-003, FR-008, FR-009)

`tools/equiv/relation.py`. **Pure**, deterministic, unit-tested without a runtime.

## Signature
```
compare(golden: Trace, candidate: Trace, mode: TRACE|OUTCOME, tier: STRICT|DYNAMIC)
  -> Verdict{equivalent: bool, divergence: DivergenceRecord | None}
```

## OUTCOME mode (bonds)
Equivalent iff `golden.outcome.status == candidate.outcome.status` AND canonical bindings match. No event comparison. (FR-005.)

## TRACE mode

### Requirements (REQUIRE identical — FR-003)
1. **Outcomes** match (final status + bindings).
2. **Bytecode-op spine**: the `BYTECODE_OP` subsequence is identical — same opcodes at same logical PCs (the primary spine).
3. **Dependent events**: for every causal edge present in golden, the same ordering and the same events hold in candidate — i.e. the partial orders are isomorphic on `{UNIFY, SUSPEND, REACTIVATE, WRITER_BIND}` projected by `causes`.

### Abstracted (MUST NOT cause divergence — FR-003)
- Heap-address values (already relabeled).
- Relative order of **causally-independent** events (no edge between them).

### STRICT tier specialization (FR-008)
Deterministic subsystems have a single causal linearization ⇒ the relation degenerates to **total-order equality** of the full event list (the stricter, cheaper check). Implementation: skip the partial-order isomorphism and compare event lists positionally. If a strict-tier trace exhibits unordered concurrency (unexpected), reclassify the source to the dynamic tier (spec edge case) and re-run under partial-order — never silently relax to make it pass.

### DYNAMIC tier (FR-009)
Full partial-order isomorphism on dependent events + outcome-equivalence. The **verification mode** for genuinely concurrent dynamics — (a) pin a canonical verification-schedule in both runtimes, or (b) accept any causally-valid schedule — is DEFERRED to when the multiagent tier is reached, decided with empirical divergence data, and recorded in `subsystem_curriculum.md` BEFORE bulk dynamic-tier generation (US4 acceptance 3). Until decided, the dynamic tier is not bulk-generated.

## DivergenceRecord (FR-003 entity; also the GEPA reflective feedback — single representation)
```
DivergenceRecord = {
  event_kind:     str            # the first divergent event's kind
  causal_position: int|path      # position in golden's causal order
  expected:       Event|Outcome  # golden
  actual:         Event|Outcome|None  # candidate (None = missing event)
  spine_pc:       int|None       # logical PC if a bytecode-spine divergence
}
```
"First" = earliest in golden causal order with no admissible match in candidate.

## SC-005 obligations (tests)
- zero false divergences on (a) heap-address relabeling, (b) independent-goal reordering — constructed cases.
- zero false equivalences on a seeded divergence battery (e.g. a suspended writer bound eagerly → must be `divergent` at the WRITER_BIND/REACTIVATE event).
