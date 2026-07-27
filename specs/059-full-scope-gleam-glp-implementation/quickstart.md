<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Quickstart — Fresh-session acceptance sweep

Every check below is runnable from a **fresh session with zero conversational memory** (FR-013). This
is the wave-5 acceptance sweep; each success criterion names its committed-evidence check.

## Environment

```bash
# Gleam/BEAM (Windows-native build)
export PATH="/c/Users/smbuser/AppData/Local/Microsoft/WinGet/Packages/Gleam.Gleam_Microsoft.Winget.Source_8wekyb3d8bbwe:/c/Program Files/Erlang OTP/bin:$PATH"
# Dart oracle (Windows dart)
export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart.exe
```

## Pinned-suite tripwires (SC-001) — must be green and grow-only at every checkpoint

```bash
cd glp_gleam && gleam test          # Gleam gleeunit: ≥ 508 passed, 0 failed (463 freeze baseline, grow-only)
cd /d/bstdev/research/glp/glpnet && $DART_or_env bash test/run_all_tests.sh   # Dart unified REPL suite: all sections green
# C# reference suites:
cd csharp && dotnet test            # glp_link / glp_quick_host / result-codec / crdtmsg — grow-only
```

## Success-criteria evidence map

| SC | Check | Evidence location |
|---|---|---|
| SC-001 | 3 pinned suites green + grow-only | this sweep + `frozen-interface-register.md` |
| SC-002 | 97 unconfirmed-gap capabilities have committed verify verdicts | `docs/research/fullscope-gleam/phase2-verify/*.md` |
| SC-003 | 154 detail_ids + open-items all reach a terminal disposition | coverage/traceability table (data-model.md) |
| SC-004 | Gleam corpus outcomes identical to Dart oracle (byte-identical where pinned) | differential parity harness verdict |
| SC-005 | FE/BE e2e: kill-restart, snapshot/restore, two concurrent clients | committed e2e script (wave 4) |
| SC-006 | Gleam mesh acceptance (quic_mesh equivalent, C# peer participating) | Gleam `quic_mesh` equivalent run (wave 4) |
| SC-007 | reference multiagent plays pass on the Gleam instance | `programs/multiagent/*.glp` on Gleam (wave 3–4) |
| SC-008 | 4 spec-056 services on embedded Gleam engine, suites green + object-PUT e2e | `service-box.contract.md` acceptance + engineer sign-off |
| SC-009 | zero unresolved escalation-register entries; every ruling cited | escalation register (research.md ledger) |

## Wave dependency note

SC-001/002/003/004 are reachable within waves 1–3. **SC-005 (FE/BE), SC-006 (mesh), SC-007
(multiagent), SC-008 (yngenios)** depend on their wave-4 builds landing; SC-006 also depends on the
open escalation `rule-quic-sideprocess-relay` being ruled (blocked until then — see research.md D2).
The wave-5 sweep re-runs the FE/BE e2e, the mesh acceptance, and the full pinned-suite set from a
fresh session, and asserts every SC has a committed evidence row (SC-003/SC-009).
