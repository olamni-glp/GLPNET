# Contract — satisfiable/2 three-valued guard (C24 successor)

Traces: FR-001..FR-008; SC-001..SC-004, SC-009. SSOT for the design remains
`programs/crdtmsg/policy-guard-proposal.glp`; this contract binds the implementation.

## G1. Gate (constitution IV-a / DISCIPLINE §1.14)
No guard implementation, compilation, or execution before the **realization addendum**
(research R1 checkpoint) is recorded in the spec's Clarifications. The base ruling
(2026-07-08, approved staged (a)→(b)) does not by itself unlock code, because the §8
mechanism it names cannot carry the semantics (see research R1 evidence).

## G2. Signature & semantics (both forms)
`satisfiable(Policy?, Reachable?)` with `Policy ::= policy(Targets, Waypoints, Excludes)`:
- **SUCCESS** iff some target ∈ reachable AND no target ∈ excludes (waypoints never affect it);
- **SUSPEND** while the reachable set (or a needed prefix) is an unbound reader — no default
  fallback may fire (the C21/037 invariant);
- **FAIL** loudly when the policy is ground and unsatisfiable; drops only via an explicit
  `otherwise` clause (logged-never-silent taxonomy, C23).
Empty-targets outcome: as ruled at the R1 checkpoint (parity vs proposal-text conflict —
research R2).

## G3. Parity (FR-005 / SC-003)
100% agreement with `PolicyMatcher.Evaluate` on every `guard_only=false` vector in
`vectors.json`. The matcher is the reference on divergence (defect in the guard; bug protocol
before any fix). `PolicyMatcher.cs` is READ-ONLY (FR-006).

## G4. Equivalence (FR-001a / SC-009)
After the form-(b) evolution, forms (a) and (b) produce identical three-valued outcomes on ALL
vectors + the four worked examples, proven by suite runs under each form. Form (a), already
green, is the reference on divergence (defect in form (b); bug protocol).

## G5. Tests (FR-007)
Worked examples (Success / Fail / Fail / Suspend) + vectors as typed GLP programs with
`procedure` declarations in `programs/tests/typed/`, wired into `test/run_all_tests.sh`.
Suspend assertions distinguish `→ suspended` from a hang via the REPL step limit.

## G6. Forbidden
Silent fallback on unknown reachability; silent drops; modifying the shipped matcher; any
SRSW escape; guard code preceding the recorded addendum (SC-001 audit is over feature history).
