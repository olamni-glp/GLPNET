# Contract — shared decision-vector set

Traces: FR-005, FR-001a, FR-007; SC-003, SC-009. SSOT: `contracts/vectors.json` (created at
implement; schema per data-model DecisionVector). Consumers: (1) new
`csharp/glp_crdtmsg.tests/PolicyVectorParityTests.cs` driving `PolicyMatcher.Evaluate`;
(2) typed GLP vector programs in `programs/tests/typed/` run in the REPL (form (a), re-run
under form (b)).

## Seed vectors (minimum set; ids stable)

| id | policy {targets;waypoints;excludes} | reachable | matcher | guard |
|---|---|---|---|---|
| wx1 | {[bob,carol];[];[mallory]} | [alice,bob] | deliver | success |
| wx2 | {[bob];[];[mallory]} | [alice,carol] | no_route | fail |
| wx3 | {[bob];[];[bob]} | [alice,bob] | no_route | fail |
| wx4 | {[bob];[];[mallory]} | **unbound** | *(guard_only)* | suspend |
| v05 | {[];[];[]} | [alice] | **per R1-checkpoint ruling** (C# today: deliver) | same as matcher |
| v06 | {[bob,carol];[];[carol]} | [carol] | no_route (excluded target) | fail |
| v07 | {[bob,carol];[];[mallory]} | [carol] | deliver (one of many reachable) | success |
| v08 | {[bob];[alice,carol];[mallory]} | [bob] | deliver (waypoints advisory) | success |
| v09 | {[bob];[alice];[mallory]} | [carol] | no_route (waypoints don't save it) | fail |
| v10 | {[bob];[];[mallory]} | [] | no_route (empty reachable, ground) | fail |
| v11 | {[bob];[];[mallory]} | [alice \| **unbound tail**] | *(guard_only)* | suspend (needed prefix unbound) |
| v12 | {[bob];[];[bob]} | **unbound** | *(guard_only)* | **per ruling**: excluded-target unsatisfiability is decidable without reachability — fail vs suspend to be fixed at the R1 checkpoint |

Rules: wx1–wx4 are the proposal's worked examples verbatim and MUST keep those outcomes
(spec SC-002). Vectors marked "per ruling" get their expected value recorded at the R1
checkpoint BEFORE the vector file is created. Additional vectors may be added; none removed.

## Consumption invariants
- Both consumers read the SAME file; no transcription by hand into either suite.
- C# consumer skips `guard_only` vectors; GLP consumer runs all.
- A parity or equivalence mismatch is a defect (bug protocol) — never an expected-value edit
  to make suites pass.
