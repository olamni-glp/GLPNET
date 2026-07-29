<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Wave 2 Consolidated — REPL Engine Split Spine

**Branch**: `061-wave-2-consolidated-repl-engine-split-spine` | **Date**: 2026-07-29 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/061-wave-2-consolidated-repl-engine-split-spine/spec.md`

## Summary

Split the C# GLP REPL into a thin client and a long-lived engine host over TCP
loopback (US1), add a quiescence-gated snapshot of the complete resumable
engine state behind a durable store (US2), supervise the engine with a
host-timer liveness ping and crash-restart (US3), and on restart restore the
snapshot, re-establish links through a new re-wire path, and resume with
at-most-once crash-boundary semantics (US4). Everything rides shipped
foundations: the 038 result-envelope codec (`csharp/glp_result_codec`), the
025/036/050 link layer (`csharp/glp_link` — `TcpTransport`, `FrameCodec`,
`LinkEstablish`/`LinkRegistry.GetOrEstablish`), and the 041 C#→PGLite store
precedent (`csharp/glp_crdtmsg/store/PgliteOpWal.cs`). The protocol is
model-checked with SPIN (wire), TLA+ (crash/restore consistency), and UPPAAL
(timed liveness) per the clarified R15 selection.

## Technical Context

**Language/Version**: C# / .NET 8 (`out/csharp/glp_runtime_net.csproj` — the
converted reference runtime; `csharp/*` satellite libraries). Dart runtime
untouched. No GLP language change (FR-042).
**Primary Dependencies**:
- Engine: `out/csharp/lib/engine/glp_engine.cs` (`GlpEngine`, `ExecutionResult`),
  `out/csharp/lib/runtime/{runtime,scheduler,glp_activation}.cs`,
  `out/csharp/lib/bytecode/runner.cs` (the `_waitReaders`/`_goalId`/
  `InfrastructureGoalIds`/`GlpChannels` state named by DEF-D1 lives here).
- Wire: `csharp/glp_link/reliability/FrameCodec.cs` (TLV framing),
  `csharp/glp_link/transports/TcpTransport.cs` (loopback transport).
- Result envelope: `csharp/glp_result_codec/` (`ResultEnvelope`,
  `ResultEnvelopeCodec` — shipped 038; ground-only subset per R6).
- Links: `csharp/glp_link/primitives/LinkEstablish.cs` (re-wire seam — the
  unbound-cell abort at lines 38–43 is the DEF-E1 target),
  `csharp/glp_link/primitives/LinkRegistry.cs` (`GetOrEstablish`).
- Store: Npgsql against the repo PGLite bridge, following
  `csharp/glp_crdtmsg/store/{IOpWal,PgliteOpWal,OpWal}.cs` (PGLite-primary +
  JSON-file fallback + monotonic seq).
**Storage**: snapshot blobs via the existing single repo PGLite cluster
(`<repo>/.pgdb/` through the shared bridge — Constitution VI-b) with a
gitignored JSON/file fallback directory; no new cluster, no Alembic change on
the codeconv side (store uses its own additive tables via the C# path like
041 did).
**Testing**: xUnit (`csharp/*.tests`, new `csharp/glp_engine_host.tests`),
`dotnet test`; REPL parity corpus re-run through the split client (SC-001);
kill-and-restart correctness test (FR-033); SPIN/TLA+/UPPAAL models under
`docs/research/repl-engine-separation/spikes/` conventions (WSL2 `run.sh` +
`run.ps1` wrappers, real-tool verdicts recorded like the #1a spike).
**Target Platform**: Windows 11 (MVP); supervision contract kept portable
(FR-025). TCP loopback only for the client↔engine wire (MVP).
**Project Type**: multi-project C# service + thin CLI client + verification
models — additive; the single-process REPL (`out/csharp/glp_repl/`) remains
unchanged (FR-005).
**Performance Goals**: not a hot path; parity + durability first. Detect →
restart → restore within one ping interval + restore time (SC-003).
**Constraints**: one engine / one client (FR-002, DEF-A2); ground-only
envelope subset with engine-side pre-rendered bindings (R6) + length-prefixed
UTF-8 output blob (R3); verbatim heap-address snapshot (FR-011/DEF-E2);
at-most-once crash boundary, no replay (FR-032); quiescence-gated snapshot
(FR-014); remaining-time timer re-arm (FR-015); self-prove liveness goal is
propose-only (FR-021/DEF-F1 — language-authority gate).
**Scale/Scope**: single engine instance; snapshot corpus = the REPL parity
program set; four user stories delivered in priority order on this one branch.

## Constitution Check

*GATE: must pass before Phase 0. Re-checked after Phase 1 — still passing.*

| Principle | Assessment | Verdict |
|---|---|---|
| I Spec-First | spec.md written, clarified (4 recorded answers), checklist green; binding R/DEF rows from the reconciliation register are cited inline in the FRs. | PASS |
| II Bug-Protocol / No-Workarounds | DEF-E1 is handled as a specified re-wire path (new code), not a try/catch around the existing abort; crash-boundary semantics are specified (at-most-once), not patched around. | PASS |
| III SRSW inviolable | No GLP clause changes; codec/link polarity preserved; 0 `skipSRSW` tokens in artifacts. | PASS (machine-checkable) |
| IV-a Language Authority | No new guard/predicate/kernel/type. The self-prove liveness goal (DEF-F1) is explicitly propose-only this wave (FR-021); MVP liveness is host-timer. | PASS |
| IV-b Preserve internals | Additive host/client/store/supervisor projects; engine internals (`_ClauseVar`-analogues, fallbacks) untouched except the additive snapshot capture/restore seam. | PASS |
| V Claude-only LM | No LM anywhere on the runtime or verification path (SPIN/TLC/verifyta are deterministic checkers). 0 `OPENAI_API_KEY`/`litellm`/`openai`. | PASS (machine-checkable) |
| VI-a Additive migrations | No codeconv Alembic change; snapshot tables are additive via the C# store path (041 precedent). | PASS |
| VI-b Single PGLite cluster | Snapshot store PRIMARY is the existing `<repo>/.pgdb/` bridge-guarded cluster; JSON fallback is a gitignored file dir, not a second cluster. | PASS |
| VII Test-gated, commit-scoped | Baseline suites green before each story lands; scoped commits via marathon checkpoints; ship via GitFlow at wave close. | PASS |
| VIII Single source of truth | This plan references 038/025/041 specs and the reconciliation register; duplicates nothing; roadmap linkage recorded (wave-2 → specified). | PASS |

**No violations → Complexity Tracking empty.**

## Project Structure

### Documentation (this feature)

```text
specs/061-wave-2-consolidated-repl-engine-split-spine/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions + rejected alternatives
├── data-model.md        # Phase 1 — entities (frames, snapshot, store, records)
├── quickstart.md        # Phase 1 — build, run split, snapshot, kill-restart
├── contracts/
│   ├── wire-protocol.md         # client↔engine frames over FrameCodec
│   ├── snapshot-store.md        # store API + blob content + seq semantics
│   └── supervision.md           # liveness ping, crash record, restart policy
└── tasks.md             # Phase 2 — /bk-tasks (NOT created by /bk-plan)
```

### Source Code (repository root)

```text
csharp/glp_split_protocol/               # NEW: tiny shared wire-protocol library (constants + codec)
├── GlpSplitProtocol.csproj              #   refs glp_link only — lets the client stay runtime-free (R7)
├── WireProtocol.cs                      #   payload types + kind bytes (contracts/wire-protocol.md)
└── RequestResponseCodec.cs              #   frame encode/decode over FrameCodec

csharp/glp_engine_host/                  # NEW (R5): the long-lived engine host
├── GlpEngineHost.csproj                 #   refs out/csharp runtime + glp_link + glp_result_codec + glp_split_protocol
├── Program.cs                           #   start empty | --from-snapshot <seq> | --store <dir>
├── EngineServer.cs                      #   one-accept TCP loopback listener; request loop
├── RequestDispatcher.cs                 #   LOAD_SOURCE / RUN_GOAL / SNAPSHOT / STATUS / SHUTDOWN
├── Quiescence.cs                        #   FR-014 quiescence detection over the scheduler
├── snapshot/
│   ├── SnapshotCapture.cs               #   FR-010 field set (DEF-D1), verbatim cells (FR-011)
│   ├── SnapshotRestore.cs               #   restore + remaining-time timer re-arm (FR-015)
│   └── SnapshotBlob.cs                  #   blob layout (029/038 byte conventions)
└── store/
    ├── ISnapshotStore.cs                #   write/latest/by-seq/list; torn-write safe (FR-013)
    ├── PgliteSnapshotStore.cs           #   primary (041 PgliteOpWal precedent)
    └── FileSnapshotStore.cs             #   JSON/file fallback

csharp/glp_repl_client/                  # NEW (R7): thin terminal client
├── GlpReplClient.csproj                 #   refs glp_link + glp_split_protocol ONLY (no runtime)
├── Program.cs                           #   REPL loop; transport-vs-goal failure split (FR-007)
└── ClientChannel.cs                     #   FrameCodec framing over TcpTransport

csharp/glp_supervisor/                   # NEW: liveness + supervised restart host
├── GlpSupervisor.csproj                 #   .NET BackgroundService (Windows service-able)
├── Supervisor.cs                        #   ping timer, exit-code capture, restart w/ backoff
├── CrashLog.cs                          #   crash records + history query (FR-024)
└── UnrecoverableTaxonomy.cs             #   FR-023 classification (DEF-F2)

csharp/glp_link/primitives/
└── RewireHandle.cs                      # NEW (DEF-E1): adopt restored pre-bound cells

csharp/glp_engine_host.tests/            # NEW: xUnit — server, quiescence, snapshot round-trip,
                                         #   store torn-write, kill-and-restart (FR-033)

docs/research/repl-engine-separation/models/   # NEW: full verification models (FR-040)
├── spin/       # wire protocol — extends spikes/spin harness (WSL2 run.sh + run.ps1)
├── tla/        # crash/restore/resume consistency (TLC)
└── uppaal/     # timed liveness/supervision (verifyta)
```

**Structure Decision**: four new satellite projects (incl. the tiny shared
protocol library that keeps the client runtime-free per R7) beside the existing
`csharp/*` libraries (matching the 038/041 convention of clobber-safe new
dirs), one net-new file in `glp_link` (RewireHandle — the only touch on a
shipped library, additive), zero changes to the single-process REPL. The
engine host references the `out/csharp` runtime project rather than forking
it; snapshot capture/restore lives in the host project and reaches runtime
state through the engine's existing surface plus minimal additive internal
accessors (no removal, IV-b).

## Complexity Tracking

> No Constitution violations — section intentionally empty.
