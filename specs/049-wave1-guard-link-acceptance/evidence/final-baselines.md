# Final baselines (T030 / SC-004)

**Date**: 2026-07-09 · **Host**: gavri · **Commit**: `06aaec6e` (+ evidence commits)

| Suite | Command | Result | vs baseline |
|---|---|---|---|
| Unified REPL | `DART=<win dart> bash test/run_all_tests.sh` | **529 total / 528 passed / 1 failed** | Baseline 524/525 preserved — the single failure is the SAME pre-existing Section Q AOT-smoke case; +4 new guard tests (A29 ×3, A30 ×1) all green |
| C# crdtmsg | `dotnet test csharp/glp_crdtmsg.tests` | **124/124 passed** (includes PolicyVectorParityTests vs vectors.json) | unchanged-or-better (SC-004) |
| C# quick host | `dotnet build csharp/glp_quick_host` | Build succeeded | no xUnit project by design (T025) — pytest is its end-to-end suite |
| Python glp_quick | `glp_quick/.venv/Scripts/python.exe -m pytest glp_quick -q` | **181 passed, 6 skipped** | unchanged-or-better |

**SC-004 verdict: PASS** — all repo baselines green at their pre-change level or better.

Note: the pre-existing AOT-smoke failure (Section Q, `glp_repl.exe` regression smoke) predates
this feature (present in the pre-change baseline run on this host) and is the known 524/525
baseline failure named in plan.md/tasks.md.
