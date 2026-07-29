# T004 — pre-change baseline (2026-07-29, branch 063 @ fac669ed)

Recorded before any US1/US2 code change (Constitution VII). Commands and
verbatim tails; environment: dotnet in ~/.dotnet, PYTHONUTF8=1.

## csharp/glp_quick_host — `dotnet build`

    0 Error(s) — builds clean (11.9 s).

## glp_quick — `python -m pytest tests/ -q`

    2 failed, 185 passed, 1 skipped
    FAILED tests/test_gleam.py::test_profile_c_client_in_process_to_csharp_server
    FAILED tests/test_gleam.py::test_profile_c_pin_mismatch_rejected

**PRE-EXISTING failures** (Profile-C Gleam-client tests) — recorded per
Bug-Protocol as baseline defects NOT introduced by this wave; to be
root-caused during US1 (suspects: Gleam-side drift since 036, or the
erlang/gleam env visible to pytest). This wave must not obscure them: the
US1 exit re-run compares against THIS table.

## csharp/glp_link.tests — `dotnet test`

    Passed! — Failed: 0, Passed: 152, Skipped: 0 (152 total, 4 s)

**Divergence from the audit claim**: the audit recorded 9 integration tests
skipping for want of the in-tree host dll; TODAY's run shows 0 skips in
glp_link.tests — either the dll gap has since been closed (this repo builds
out/csharp/* routinely now) or the audited 9 live in a different suite
(glp_quick side?). T007/T008 must reconcile this divergence explicitly
against the audit record before claiming C3 done — never assume.
