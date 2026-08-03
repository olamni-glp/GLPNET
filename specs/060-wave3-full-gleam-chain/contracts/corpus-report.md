# Contract: Conformance corpus report

**Feature**: `060-wave3-full-gleam-chain` | Serves **User Story 3** (FR-016 … FR-019, SC-002, SC-008, SC-010)

Output contract for `test/parity/run_gleam_corpus.sh` and `run_differential.sh`. This report is the wave's evidence of correctness, so its completeness rules are stricter than its formatting rules.

## Per-case verdict

Every case in `corpus.list` produces exactly one verdict:

| Verdict | Meaning | Required fields |
|---|---|---|
| `pass` | Gleam outcome matches the golden | `case_id` |
| `fail` | Gleam outcome differs from the golden | `case_id`, `expected`, `observed` |
| `out_of_scope` | case deliberately not compared | `case_id`, `reason` |

**Outcome values** compared are the runtime's own classification: `success` (with bindings), `failure`, `suspended`. Suspension is compared as itself, never collapsed into failure.

## Aggregate block

```text
total:        <N>
pass:         <P>
fail:         <F>
out_of_scope: <O>
```

## Invariants

1. **Completeness**: `P + F + O == N`, always. A case that produced no verdict is a defect in the runner, not an absent line (FR-017, SC-002).
2. **No silent skips**: skipping requires an `out_of_scope` verdict *with a reason*. There is no third state.
3. **Missing golden ⇒ out_of_scope**: a case with no reference golden is `out_of_scope` with reason `golden missing — 059 T051 drift`. It is **never** `pass` (FR-018a). 44 such cases exist at wave start.
4. **Determinism**: identical code over an identical corpus yields identical verdicts and identical counts (FR-019, SC-008). A case whose verdict varies between runs is a flake and must be reported, not re-run until green.
5. **Divergences are named**: every `fail` names the case and both outcomes, so the report is actionable without re-running (FR-017).
6. **Exit code is not the report.** A non-zero exit must be accompanied by the aggregate block; consumers read the counts, not the code.

## Wave-3 targets

| Measure | At wave start | At wave end |
|---|---|---|
| `out_of_scope("golden missing")` | 44 | 0, or each remaining case individually reasoned (SC-010) |
| in-scope pass rate | — | ≥ 95%, every exception named (SC-001) |
| completeness invariant | must hold | must hold (SC-002) |
