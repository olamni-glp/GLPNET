# 049 Wave-1 pre-change baselines (T002, constitution VII)

**Date**: 2026-07-08 · **Host**: Olamnit · **Commit at baseline**: `62562951` (branch `049-wave1-guard-link-acceptance`, clean tree)

| Suite | Command | Result | Verdict |
|---|---|---|---|
| Unified REPL suite | `bash test/run_all_tests.sh` | **Total: 525 \| Passed: 524 \| Failed: 1** | PASS — matches recorded baseline; the 1 failure is the pre-existing, unrelated AOT-smoke case (Section "AOT smoke: PASS=8 FAIL=1") |
| C# crdtmsg xUnit | `dotnet test csharp/glp_crdtmsg.tests` | **Passed: 114, Failed: 0, Skipped: 0** (13 s) | PASS (suite grew from the 104 recorded at 036-close; all green) |
| glp_quick pytest | `glp_quick/.venv/Scripts/pytest.exe -q` | **178 passed, 1 skipped** (87 s) | PASS (suite grew from the 18 recorded at 036-close; the 1 skip is an integration case gated on an unbuilt artifact) |

Full REPL log captured at the session scratchpad (`claude-049-repl-baseline.log`); tail recorded above.

**Lane-D pre-existing-fix audit (FR-015)** — verified against git history at baseline:

| Finding | Fix commit | Regression test in that commit |
|---|---|---|
| #3 dup `endpoint_id` eviction | `bdab8585` (2026-07-03) | YES — `glp_quick/tests/test_mesh.py::test_duplicate_announced_id_never_evicts_the_incumbent` |
| #5 demo AttributeError on handshake timeout | `d0acab2f` (2026-07-03) | NO — fix only; regression added by this feature (T026) |
| #6 pre-readiness stdout-pipe hang | `b8c474b1` (2026-07-03) | NO — fix only; regression added by this feature (T027) |
| #7 gleam relay >1 MiB misroute | `28db9e5b` (2026-07-03) | NO — fix (fragment reassembly, erlc-verified) only; regression added by this feature (T028) |
