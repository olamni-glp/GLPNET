# Data Model — 049 Wave 1

No database entities — all state is files (spec artifacts, evidence records, vectors) plus the
existing marathon durable rows (verified, not modified). Entities below are the shared shapes.

## RoutingPolicy
Mirrors the shipped C# `RoutingPolicy` (read-only reference). GLP side per the proposal types.

| Field | Type | Notes |
|---|---|---|
| targets | PeerList | must-reach; **empty-targets outcome pending R1-checkpoint ruling** (C# delivers vacuously; proposal text fails) |
| waypoints | PeerList | advisory-ordered for MVP; MUST NOT affect the per-hop decision |
| excludes | PeerList | any target ∈ excludes ⇒ unsatisfiable |

`PeerName ::= Constant` — authenticated peer name, never a cert/SPKI pin.
GLP: `Policy ::= policy(PeerList, PeerList, PeerList).` `PeerList ::= [] ; [PeerName | PeerList].`

## ReachableSet
`Reachable ::= [] ; [PeerName | Reachable].` — currently-reachable authenticated peers at a hop.
May be **unbound** (or have an unbound suffix) at evaluation time ⇒ guard Suspend arm.

## DecisionVector (contracts/vectors.json — SSOT for parity + equivalence)

| Field | Type | Notes |
|---|---|---|
| id | string | stable, e.g. `wx1` (worked example 1), `v07` |
| policy | {targets, waypoints, excludes: string[]} | ground |
| reachable | string[] \| "unbound" | "unbound" ⇒ guard_only |
| expected_matcher | "deliver" \| "no_route" | omitted when guard_only |
| expected_guard | "success" \| "fail" \| "suspend" | three-valued |
| guard_only | bool | true ⇒ C# side skips (Suspend arm is guard-only behavior) |
| note | string | provenance (worked example #, edge case) |

Invariants: for every vector with `guard_only=false`, `expected_guard=success ⇔
expected_matcher=deliver` and `expected_guard=fail ⇔ expected_matcher=no_route` (FR-005);
form (a) and form (b) outcomes identical on ALL vectors (FR-001a / SC-009).

## §1.14 RulingRecord
Lives in the spec's Clarifications (durable). Base ruling recorded 2026-07-08 (approved staged
(a)→(b)). This feature appends one **realization addendum** from the R1 checkpoint: chosen
mechanism (a1 / a3 / other), empty-targets outcome, date. State transitions:
`recorded-base → realization-confirmed → (implementation unlocked)`. Guard code MUST NOT exist
left of `realization-confirmed` (SC-001 audit).

## EquivalenceRun (evidence/guard/)
One record per suite execution per form: `{form: a|b, suite_commit, vectors_pass/fail per id,
worked_examples: 4 outcomes, repl_baseline: n/of}`. SC-009 needs identical outcome maps for the
two forms.

## AcceptanceEvidence (evidence/{gavri,two-host,marathon}/ — format in contracts/acceptance-evidence.md)
Per-criterion record: `{criterion (e.g. 036 SC-002b), host(s), command, output (path to captured
log), verdict: PASS|FAIL|BLOCKED, date}`. BLOCKED records carry what-was-attempted +
what-is-missing and keep the ship gate closed (FR-008/FR-010).

## GavriDelegation
`{branch: off 049-wave1-guard-link-acceptance, push scope: own branch only, prompt:
gavri-task-prompt.md, evidence dir: evidence/gavri/, feed-back: early + continuous push}` —
contract in `contracts/gavri-delegation.md`.

## MarathonDurabilityRecord (evidence/marathon/)
`{run_id, checkpoints_before_kill[], kill: {method, pid, timestamp}, resume: {session, reported
position}, assertions: {position==durable rows, zero re-execution, zero loss}, redrive: {step,
commit completed once}}`.
