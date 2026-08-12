# 076 — Test baseline (T001)

Per CLAUDE.md Test Protocol / DISCIPLINE §2.2: known-good baseline recorded BEFORE any
change to the type checker.

## Pre-change baseline — 2026-08-12

**Command** (from repo root):

```
DART=/d/BSTDEV/tools/dart-sdk/bin/dart.exe bash test/run_all_tests.sh
```

**Result**: `Total: 547 | Passed: 547 | Failed: 0` — ALL TESTS PASSED (exit 0).

Recorded at commit `5c22ac7c` (the §1.14 approval + authoritative-spec amendment; no
checker code changed yet).

Notes:
- Section S (`ms_message` durable mesh messaging) SKIPPED — venv absent; it is a
  standalone gate (`ms_message/tests/drill_disconnect.py`), not a regression.
- Section I (cross-runtime Gleam × C#) passed: US5 round_trip 12/12, mismatch 2/2.
- `glp_gleam/build/` is single-OTP (CLAUDE.md): the WSJF/Windows Section I harness owns it
  after this run. Re-running the WSL `gleam test` suite requires `rm -rf glp_gleam/build`
  first — a beam-load error there is NOT a code regression.

## Post-change verification (T014)

Not yet run — the checker change is deferred to the implement stage, which the marathon
run `mrun-d086da8a860f` requires to happen in a NEW session after a safe restart
("implement: /bk-implement complete in NEW session (safe restart before), both suites
green vs baseline").

The post-change gate is: **547/547 with zero regressions**, plus the new FR-007 tests
(2 positive in Section B, 1 negative in Section C) green on top.
