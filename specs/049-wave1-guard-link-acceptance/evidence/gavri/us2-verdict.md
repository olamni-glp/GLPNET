# US2 integration verdict — Olamnit review of the gavri evidence (T018, SC-005)

- **Criterion**: spec US2 / FR-009 (in-process BEAM QUIC conformance = Profile A baseline) + FR-010 (reproducible provisioning)
- **Host(s)**: gavri (execution, WSL2 Ubuntu 24.04) · Olamnit (integration review)
- **Reviewed evidence**: branch `049a-gavri-us2-us3` @ `ef88ec68` — `evidence/gavri/00-environment.md`,
  `10-profile-c.md`, `20-two-host.md` (fetched; merge into the feature branch deferred to the US3 close so
  the delegation lands as one reviewed integration)
- **Verification against contracts/acceptance-evidence.md + gavri-delegation.md**:
  - Environment discovery committed FIRST (D2.1): PASS
  - FR-010 provisioning documented + reproducible: PASS — quicer 0.2.15 + vendored msquic 2.5.7 on OTP 25,
    carried by a committed rebar3 app (`gleam_quic/profile_c/rebar.config`); prebuilt-first order honored;
    Windows MSVC blocker honestly re-confirmed (unchanged 036 finding), Linux/WSL path per profile_c README
  - Profile A baseline recorded first, then Profile C at equal criteria: PASS — all 7 demo criteria
    (SC-001/002/002b/003/004/005/006) PASS under both profiles
  - Genuine in-process claim substantiated: PASS — process-table proof of ZERO dotnet client processes
    (only the reference server); SPKI pin verified mid-handshake with a loud-reject negative control
    (`test_profile_c_pin_mismatch_rejected`)
  - Suites green on the branch: WSL 179 passed/2 skipped, Windows 175 passed/6 skipped; new FR-009 tests included
  - Push discipline: own branch only, milestone-by-milestone (4 commits) — per D1/D3
- **US2 acceptance scenario 1**: satisfied (scenario 2 BLOCKED arm not needed)
- **Verdict**: **PASS (SC-005)**
- **Date**: 2026-07-08
- **Carried observations for the wave close-out (recorded, not fixed — bug protocol)**:
  1. relative `--cert` re-resolves under `gleam_quic/` cwd for `--stack gleam` (docs nuance);
  2. `terminate_tree` on Linux does not kill grandchildren (gleam→erl→dotnet) — Windows path unaffected.

## Follow-up record — gavri evidence correction (2026-07-08, gavri commit `8facff21`)

The environment claim this verdict relayed ("Windows MSVC blocker honestly re-confirmed") was
**corrected on the gavri branch**: gavri HAS MSVC 14.50 (VS Community 2026 Insiders 18.4,
`cl.exe` on disk, vswhere-visible); the original "MSVC-less" finding misread a 2022-BuildTools-only
probe. Impact on this verdict: **none to the PASS** — the WSL run stands as the sanctioned
"target Linux" path of `gleam_quic/profile_c/README.md` and the conformance results are unaffected;
only the *justification* ("no native option existed on gavri") was wrong. Olamnit's own MSVC absence
(the 036 blocker on THIS host) is unchanged. A Windows-native quicer build on gavri is now a viable
future path, not a wave-1 requirement.
