<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks — 104 WP-02 QUIC listener service

| id | task | FR / SC | state |
|---|---|---|---|
| T001 | Baseline: `dotnet test csharp/ynet_transport.tests -c Release` and record the count | — | done — **184/184** |
| T002 | `ListenerConfig` (service name, bind address, port) | FR-001 | done |
| T003 | `ListenerOutcome` enum + `ListenerReport` with `SkippedTiers` | FR-008, FR-010 | done |
| T004 | `IrohSidecarProvider` at tier 0, measuring `Probe()`, actionable refusal | FR-004, FR-005, FR-006 | done |
| T005 | Register iroh in `QuicProviderChain.Default` above msquic, msquic retained | FR-004, FR-007 | done |
| T006 | `YnetListenerService.BindAsync` — bind via chain, record actual provider | FR-002, FR-003 | done |
| T007 | Populate `SkippedTiers` when the winner is not tier 0; print in `Describe()` | FR-008 | done |
| T008 | Refuse to start when no provider serves; name every tier | FR-011 | done |
| T009 | `ProbeReachabilityAsync` — handshake **plus** bidirectional byte exchange | FR-009 | done |
| T010 | `BoundUnreachable` distinct from `Ok` and `BindFailed` | FR-010 | done |
| T011 | Test SC-001 bound provider reported and equals handle's `ProviderName` | SC-001 | done |
| T012 | Test SC-002 no sidecar → msquic wins **and tier-0 skip is reported with reason** | SC-002 | done |
| T013 | Test SC-003 stub sidecar → iroh wins at tier 0 | SC-003 | done |
| T014 | Test SC-004 bound-but-unreachable reports `BoundUnreachable` | SC-004 | done |
| T015 | Test SC-005 no provider → refusal naming all tiers | SC-005 | done |
| T016 | 🔴 SC-006 negative controls: break each mechanism, observe red, restore | SC-006 | done |
| T017 | FR-012 grep: no election / campaign / vote / leader in the new code | FR-012 | done |
| T018 | Full suite re-run; report delta against T001 | — | done — **193/193** (184 baseline + 9 new; 1 pre-existing test updated for the intended contract change) |
