<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks — 103 stable federation identity

Written from the commits, not from intent. Each line names the evidence that closes it, so a reader
can check the claim rather than take it.

| # | task | state | evidence |
|---|---|---|---|
| T001 | Reproduce the defect as a measurement, not a reading | ✅ | 5 probe runs, 5 pins (broadcast 17:45Z) |
| T002 | Locate the root cause in source | ✅ | `QuicLinkTransport.cs:95` `ECDsa.Create`, `:105` local named `ephemeral` |
| T003 | Decide the home: shared `glp_link/transports/`, beside the `SpkiPin` discipline both callers already delegate to | ✅ | `csharp/glp_link/transports/FederationIdentity.cs` |
| T004 | Implement load-or-create with a `.fingerprint` sidecar, reusing `SharedCertMaterial`'s fail-closed consistency check | ✅ | commit `7178eabb` |
| T005 | Delegate from `QuicLinkTransport.LoadFederationIdentity`; mark `CreateDevCert` as ephemeral-by-design in its doc | ✅ | commit `7178eabb` |
| T006 | Re-point `glp_quic_probe` and make it print keystore + MINTED/LOADED so the property is visible to whoever runs it | ✅ | commit `7178eabb` |
| T007 | Regression tests, led by the exact measurement that failed | ✅ | 13 tests, then 19 |
| T008 | Adversarial review, cycle 1 | ✅ | codex CLI, 6 findings |
| T009 | Close the CRITICAL race with an atomic rename claim | ✅ | commit `fbd3088e`; `ConcurrentFirstStart_ConvergesOnOneIdentity` |
| T010 | Close the key-at-rest window: 0600 at `open(2)`, protected DACL before any byte | ✅ | commits `fbd3088e`, `2871c9bf`; `Get-Acl` shows `protected=True`, one ACE |
| T011 | Adversarial review, cycle 2 — per-finding CLOSED/PARTIAL/OPEN verdicts | ✅ | 2 CLOSED, 3 PARTIAL, 1 OPEN→fixed, 2 new defects found |
| T012 | Fix cycle-2's new defects (rotation's `Created` semantics, undisposed cert) and diagnose interrupted rotation | ✅ | commit `2871c9bf` |
| T013 | Full-suite regression | ✅ | `glp_link` 217/217, `glp_crdtmsg` Quic 8/8 |
| T014 | Cross-process field verification incl. federation-capable bind | ✅ | one pin over 5 processes; `0.0.0.0:47890` bound |
| T015 | Merge to `develop` | ✅ | `cc2a0802` (local — see T016) |
| T016 | Push, PR, release | ⛔ **BLOCKED** | the environment's command classifier refuses `git push` in both Bash and PowerShell; needs engineer permission |
| T017 | Publish the pin + fix to the other three hosts, ACK-required | ✅ broadcast written | `docs/fleet/BROADCAST-…20260904T2130Z…` |
| T018 | Pair-atomic keystore write (codex #2, PARTIAL) | ⬜ deferred to hardening | needs a single-file format or a reader-side lock |
| T019 | Spawned-process race/restart test (codex #5, PARTIAL) | ⬜ deferred to hardening | needs a multi-process test harness |
| T020 | Key-at-rest policy ruling (`Q-GLPNETA21-05`) | ⬜ engineer | asked this session |
| T021 | Federation UDP port ruling (`Q-GLPNETA21-02`) | ⬜ engineer | asked 17:45Z, re-asked this session |
