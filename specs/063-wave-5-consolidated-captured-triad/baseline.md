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

## T007/T008 — divergence RECONCILED + un-skipped suite verdicts (2026-07-30, 063 @ 0d2dacbc)

**The audited "9 skipped integration tests" are the 9 dll-gated pytest
modules in `glp_quick/tests/`, NOT glp_link.tests** (whose only skip guards
are QUIC-platform gates): `test_csharp_adapter`, `test_mesh`, `test_demo`,
`test_gleam` (dll + Gleam-toolchain gate), and the five
`tests/integration/test_us{1,2,4,5,6}_*_mesh` modules. Each carries
`pytest.mark.skipif(not host_dll_path().exists(), ...)` — the audited
9-skip state reproduces exactly when the host dll is absent.

**Load condition (verified in code)**: `glp_quick.stacks.csharp.host_dll_path()`
reads `csharp/glp_quick_host/bin/{Debug,Release}/net10.0/glp_quick_host.dll`
(override `GLPQUICK_HOST_DLL`) — the project's own standard `dotnet build`
output, NOT `out/csharp/` (the tasks.md T007 wording assumed out/csharp; the
only out/csharp artifact the tier needs is `out/csharp/glp_repl`, which
`test_us5_repl_page_mesh` gates on separately and which IS built on this
tree). No hard-coded skip attribute exists; the build alone closes the gate.

**Un-skipped suite verdicts** (`dotnet build csharp/glp_quick_host` → 0
errors, then `python -m pytest -q -rs`):

    2 failed, 185 passed, 1 skipped

- The 1 skip is `test_gleam.py:62` — "Profile C IS built here — the
  not-built guard cannot fire" — a deliberate inverse-guard negative
  control, NOT dll-attributable. **C3's 0-dll-skips clause is satisfied.**
- The 2 failures are the SAME two pre-existing profile-C failures recorded
  at T004 (not exposed by the un-skip — they already ran at baseline).
  Root token now captured per Bug-Protocol (report, not fix):
  `LinkError: quic_unsupported: quicer NIF failed to start: {quicer, ...}`
  — the Gleam profile-C client's quicer/msquic NIF fails to LOAD at runtime
  on this host even though `profile_c_built()` reports built;
  `test_profile_c_pin_mismatch_rejected` then sees `quic_unsupported` where
  it expects `cert_mismatch` (the handshake never starts). Environment-level
  defect in the Gleam/quicer runtime on this host; root-cause during US1
  per the T004 note.
## T009/T010 — mesh_dup_id regression scenario + provenance finding (2026-07-30, 063 @ d5a0ac63)

`csharp/glp_link.tests/MeshDupIdRegressionTests.cs` (4 tests, selected by
`dotnet test csharp/glp_link.tests --filter mesh_dup_id`) drives the internal
`Mesh` router directly: incumbent keeps the route on a duplicate announce
(rejection visible as `WARN dup-id` in the mesh's own output), the newcomer's
DEATH never evicts the live incumbent (the audited `Program.cs:253` symptom),
clean departure leaves no stale route, and the rejected newcomer becomes
addressable under a fresh id.

- **Verdict: 4/4 PASS against the current code — the audited symptom does NOT
  reproduce.** Per T010's second arm: **provenance finding recorded** — the
  eviction guard in `Mesh.Remove` (conditional
  `TryRemove(KeyValuePair(id, link))`) and the no-hijack `Register` landed
  in-tree between the 2026-07-02 audit and this wave (the in-code guard
  comment's provenance was unverified until now); no fix was required, and
  NO change was made to `Program.cs`.
- **Witness validity proven by mutation check**: temporarily reverting
  `Mesh.Remove` to the audited naive eviction (`_byId.TryRemove(id, out _)`)
  makes `mesh_dup_id_newcomer_death_never_evicts_the_live_incumbent` FAIL
  (1F/3P), satisfying C2's "MUST fail against the audited defect behaviour";
  the mutation was reverted byte-identically (git diff empty) and the
  scenario re-verified green. The scenario ships as the closure witness.
- Full `glp_link.tests` after adding the scenario + the
  glp_quick_host project reference/InternalsVisibleTo wiring:
  **156/156, 0 skips** (baseline 152 + the 4 scenario tests).

## T014 — full 036 demo-suite re-verify: the table that SUPERSEDES "18/104" (2026-07-30, 063 @ 1590ce30)

The audited claim "18/104 green" (036 T039, 2026-07-01: 18 glp_quick pytest +
104 glp_link xUnit) is superseded by this current, reproducible count — the
suites have since grown (features 040/050/063) and everything the dll gates
now RUNS:

| Suite / scenario | Command | Verdict (2026-07-30) |
|---|---|---|
| glp_quick pytest (incl. all 9 dll-gated modules + the T013 bridge tests) | `glp_quick/.venv/Scripts/python -m pytest` | **187 passed, 2 failed (pre-existing profile-C quicer-NIF env), 1 skipped (designed inverse guard)** |
| glp_link.tests xUnit (incl. mesh_dup_id) | `dotnet test csharp/glp_link.tests` | **156/156, 0 skips** |
| Demo SC-001 real on-wire QUIC/HTTP-3 handshake | `glp-quick demo --addr 127.0.0.1 --port 44741 --cert <dir> --stack csharp --clients 3` | PASS |
| Demo SC-002 full-duplex GLP-message exchange | 〃 | PASS |
| Demo SC-002b peer-to-peer duplex mesh (to-routing + broadcast) | 〃 | PASS |
| Demo SC-003 ≥3 concurrent isolated clients | 〃 | PASS |
| Demo SC-004 single-client-failure resilience | 〃 | PASS |
| Demo SC-005 shared self-signed cert (SPKI pin) only trust anchor | 〃 | PASS |
| Demo SC-006 cross-stack csharp ≡ gleam | 〃 | NOT-RUN (needs `--stack gleam`; profile-C runtime is the recorded env defect on this host) |
| Two-host LAN acceptance (036 T040) | 〃 | NOT-RUN (second host; 036 full-acceptance feature scope) |
| Overall demo run criteria | 〃 | **PASS** |

Every scenario prints its explicit verdict (no silent PASS); the two NOT-RUN
rows are the demo's own honest attributions, unchanged in scope from 036's
followup-full-acceptance brief.

- **Operational hazard (reported)**: the two failing profile-C tests LEAK
  their spawned `glp_quick_host` server processes on teardown; the orphans
  hold `bin/Debug/net10.0/glp_link.dll` open and break the next
  `dotnet build` with MSB3027/MSB3021 file locks (observed and cleaned twice
  this session: 3 orphans from the predecessor's baseline run, then 3+1 from
  this session's runs). Test-teardown defect in the profile-C failure path —
  reported here, not masked.
