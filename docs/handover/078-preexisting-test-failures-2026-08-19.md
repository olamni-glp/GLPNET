# Pre-existing codeconv test failures (filed from 078, engineer-ruled "file separately")

**Date:** 2026-08-19
**Discovered by:** the 078-verification-receipts implement baseline (full `codeconv` suite run to completion).
**Engineer ruling:** proceed with 078 to codexreview; file these 18 separately (not in 078's scope).

## Status

`736 passed, 18 failed, 9 skipped` (2083 s). **All 29 receipts tests pass; zero regressions.**
Proven independent of 078: the 078 commit touched only receipts-scoped files, and none of the 7
failing test files import `codeconv.receipts`. 078 added **no** migration (FR-022 sidecar files,
not catalog).

**Meta-note:** this is itself an instance of the class 078 fixes — the suite *looked* green under a
`tail`-masked / `timeout`-killed baseline but was actually red. A receipted check would have caught it.

## The 18 failures (7 files)

| File | Failures | Root cause (characterised) |
|---|---|---|
| `test_migration_0008_single_head.py` | 2 | **Stale test.** The Alembic chain grew to **0011/0012**; this test hardcodes the expected chain ending at 0008 and asserts equality. Cheap fix: update the expected-chain dict. |
| `test_migration_0009_single_head.py` | 2 | Same stale-chain cause (expected ends at 0009; actual has 0011/0012). |
| `test_tutorials_run.py` | several | JSON-schema / CLI-parity mismatches in the tutorials (glp-quick) subsystem. Likely the known tutorials/CLI-JSON drift. |
| `test_tutorials_skill_parity.py` | ≥1 | `test_cli_json_is_pure_serialization_of_engine_model` — CLI/engine JSON drift. |
| `test_phase7_verifications.py` | — | DBOS/pipeline integration; likely environmental (bridge/DB state). |
| `test_planagents_lifecycle.py` | — | DBOS/pipeline integration; likely environmental. |
| `test_equiv_capture.py` | — | DBOS/pipeline integration; likely environmental. |

## Recommended triage (separate from 078)

1. **Cheap & real:** update `test_migration_0008/0009_single_head.py` expected-chain dicts to include
   0011/0012. Also note **Constitution VI-a is stale** — it states `heads == [0010]` but the real head
   is now **0012**; the constitution's Evidence anchor should be restamped (owner-approved amendment).
2. **Investigate:** the tutorials JSON-schema/parity failures (real drift vs environment).
3. **Confirm environmental:** re-run the DBOS/pipeline integration failures with the bridge up; if they
   pass in isolation they are flakes, else real.

## Where 078 stands

MVP mechanism (US1+US2+US3) implemented on the reference check, committed, 29/29 green. Next gate:
`/bk-codexreview` → STOP at ship for SHIP-TOKEN. See the safe-restart doc alongside this file.
