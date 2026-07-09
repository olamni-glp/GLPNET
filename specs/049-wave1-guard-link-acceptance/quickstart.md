# Quickstart — 049 Wave 1 verification runbook

Per-deliverable verification paths. Baselines FIRST (constitution VII): REPL suite
`bash test/run_all_tests.sh` (expected 524/525 — the 1 failure is the pre-existing AOT-smoke
case), `dotnet test csharp/glp_crdtmsg.tests` (104), `pytest glp_quick` (18).

## A. GLP policy-guard (US1) — GATED

1. **R1 checkpoint first**: present research.md R1 (form-(a) mechanism evidence + candidates
   a1/a3 + empty-targets ruling for v05/v12) to Gabi; record his answer as a Clarifications
   addendum in spec.md. **No guard code before this.**
2. Create `contracts/vectors.json` from `contracts/decision-vectors.md` with the ruled values.
3. Implement form (a) per the ruled mechanism; guard + types in
   `programs/crdtmsg/policy_guard.glp`; worked-example + vector programs in
   `programs/tests/typed/`; wire into `test/run_all_tests.sh`.
4. Verify in the REPL (pre-approved invocation):
   `echo -e 'load programs/tests/typed/policy_guard_worked.glp\n<goal>.' | dart run bin/glp_repl.dart`
   Expect wx1 `→ succeeds`, wx2/wx3 `→ failed`, wx4 `→ suspended` (step-limited, not hung).
5. Parity: add `PolicyVectorParityTests.cs`, `dotnet test csharp/glp_crdtmsg.tests` — 100% on
   non-guard_only vectors. `PolicyMatcher.cs` diff must be empty.
6. Evolve to form (b) (per ruling), re-run the ENTIRE guard suite under form (b); record both
   outcome maps in `evidence/guard/` (SC-009 identical).

## B. 036 link full acceptance

**US2 + US3 (gavri)**: post `gavri-task-prompt.md` in a gavri session (Gabi does the posting).
Olamnit side when the two-host run is scheduled:
```
glp-quick --server --addr 192.168.0.143 --port 8443 --cert ./glpquick-cert --max-clients 4
```
(036 quickstart §7; cert distributed out-of-band; UDP 8443 open in the firewall.) Pull the gavri
branch for evidence integration; verify records under `evidence/gavri/` per the contract.

**US4 (marathon durability)** — buildkit venv (`D:\bstdev\research\buildkit\.venv313`):
1. Create/adopt a real marathon run for this wave; checkpoint ≥2 steps.
2. Kill the owning process mid-flight (taskkill), resume from a FRESH session, assert position
   == durable rows, zero re-execution, zero loss.
3. Exercise the durable-first/commit re-drive path.
4. Records to `evidence/marathon/`.

**FR-015 fixes** (fix → regression test → suite):
- #3 `csharp/glp_quick_host/Program.cs` eviction guard → xUnit → `dotnet test`
- #5 `glp_quick/demo.py:79` timeout AttributeError → pytest
- #6 `glp_quick/stacks/csharp.py` reader-before-readiness → pytest
- #7 `gleam_quic/src/glpq_ffi.erl:17` length-framed read → erlang test (full tool paths on this
  host, or on gavri per the delegation contract)

## Ship gate
ALL FOUR user stories PASS (SC-010); every criterion has an evidence record (FR-013); both
roadmap origin features advanced at close (FR-014). Ship via `buildkit ship --skip-preflight`
after running the suites yourself.
