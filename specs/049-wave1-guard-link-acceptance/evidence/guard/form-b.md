# EquivalenceRun — form (b), the system guard primitive (SC-009)

- **form**: b (system guard primitive per the recorded §1.14 staged ruling)
- **suite_commit**: `06aaec6e` (feat(049): T011 form (b) system guard primitive satisfiable/2)
- **host**: gavri
- **date**: 2026-07-09

## Realization

`satisfiable/2` is a **native system guard**: a constant clause-spec table
(`BytecodeRunner.systemDefinedGuards`, helpers `$sat:`-namespaced so they can never collide with
user programs) evaluated by the same three-valued interpretive machinery as form (a); the Guard
opcode dispatches `satisfiable/2` to it BEFORE consulting the user-program table. Registration:
`'satisfiable'` in the analyzer's non-negatable guard set + `'satisfiable/2'` in
`builtinProcedures` (declaration-only callers parse/type-check — the same mechanism as `atom`/`@<`
and the 025 link kernels). The GLP source's []-coverage arms (type-checker-required decidable-fail
`~ground(a)` clauses) are represented in the system table by clause absence — no head matches []
⇒ FAIL, the identical outcome. Form selection: `GLP_POLICY_GUARD_FORM` env (default `b`; `a`
re-routes to the user-program table) — a runtime toggle for the SC-009 verification, not a
language surface.

## SC-002 (re-run under form (b)) — worked examples

- **Command**: `echo -e 'load programs/tests/typed/policy_guard_worked.glp\n<4 goals>\n:quit' | dart run glp_runtime/bin/glp_repl.dart` (GLP_POLICY_GUARD_FORM unset ⇒ form b)
- **Output**: `→ succeeds`, `→ failed`, `→ failed`, `→ suspended`
- **Verdict**: PASS — identical to the form-(a) map (evidence/guard/form-a.md)
- **Date**: 2026-07-09

## SC-009 — (a) ≡ (b) equivalence on ALL 12 vectors, same build

| id | form (a) (env `GLP_POLICY_GUARD_FORM=a`, commit 06aaec6e) | form (b) (default) | equal |
|----|------|------|-------|
| wx1 | succeeds | succeeds | ✓ |
| wx2 | failed | failed | ✓ |
| wx3 | failed | failed | ✓ |
| wx4 | suspended | suspended | ✓ |
| v05 | succeeds | succeeds | ✓ |
| v06 | failed | failed | ✓ |
| v07 | succeeds | succeeds | ✓ |
| v08 | succeeds | succeeds | ✓ |
| v09 | failed | failed | ✓ |
| v10 | failed | failed | ✓ |
| v11 | suspended | suspended | ✓ |
| v12 | failed | failed | ✓ |

- **Verdict**: PASS — 100% identical three-valued outcome maps under both forms (12/12 vectors +
  4/4 worked examples). Form (a) remains the reference; no divergence observed, no fix needed.

## Form (b) as a genuine primitive (declaration-only caller, no user clauses)

- **Program**: `programs/tests/typed/policy_guard_formb.glp` (declares `procedure
  satisfiable(Policy?, PeerList?).` with NO clauses)
- **Output**: `test_b1.` → succeeds; `test_b2(Rb2?).` → **failed** (v12 shape — decidable exclusion
  with reachability unbound); `test_b3(Rb3?).` → suspended
- **Verdict**: PASS — the guard exists natively, independent of any user program
- **Date**: 2026-07-09

## repl_baseline (T012 full-suite re-run under form (b))

- Suite now carries permanent both-form regression coverage: A29 (form-b worked + form-b vectors
  + form-(a)-env vectors reference) and A30 (pure-(b) probe) — suite total 529.
- Full-suite result @ `06aaec6e` (2026-07-09): **529 total | 528 passed | 1 failed** — the single
  failure is the same pre-existing Section Q AOT-smoke case as the pre-change baseline; all four
  policy-guard entries PASS (`worked wx1-wx4`, `vectors 12/12`, `form (a) reference 12/12
  SC-009`, `form (b) no-user-clauses S/F/Susp`). Command:
  `DART=<win dart> bash test/run_all_tests.sh` (scratchpad `form-b-suite.txt`).
