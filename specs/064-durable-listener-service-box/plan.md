<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Durable listener service box (gavri variant)

**Branch**: `064-durable-listener-service-box` | **Date**: 2026-08-03 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/064-durable-listener-service-box/spec.md`

## Summary

Make a GLP QUIC listener service survive REPL restarts with full message
history, with ZERO new GLP language surface (gavri variant). Three moves, all
host-side: (1) a repo-root `glpservice/resume.json` registration read by the
existing `AfterEngineCreated` shim seam, which replays the operator's exact
manual load+goal sequence at launch; (2) a durable received-message log reusing
the shipped crdtmsg op journal (`IOpWal`: PGlite primary on the repo's single
`.pgdb/` cluster, file fallback), appended once per delivered term via one
additive `LinkPump` delivery hook, and replayed history-before-live at boot;
(3) `QuicTransport.ConnectAsync` gains the TCP transport's retry-until-ct loop
(role-order independence). Full decisions + code anchors: [research.md](research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (`out/csharp` REPL host + `csharp/` link and crdtmsg libraries); GLP programs unchanged
**Primary Dependencies**: existing `csharp/glp_link` (transports, LinkPump), `csharp/glp_crdtmsg` (IOpWal/OpWal/PgliteOpWal/Projection), `System.Net.Quic`, Npgsql (already in use)
**Storage**: repo's single PGlite cluster `.pgdb/` via `PgliteOpWal` (Constitution VI-b), file fallback `glpservice/wal/`
**Testing**: xunit suites under `csharp/*.tests`; REPL-level drills via piped stdin (`dotnet run --project out/csharp/glp_repl`); unified suite `bash test/run_all_tests.sh` as the non-regression gate
**Target Platform**: Windows glpnet hosts (the three-host fleet); loopback + LAN QUIC
**Project Type**: host runtime extension (composition-root shim + two library seams) — no new project
**Performance Goals**: SC-001 launch-to-accepting ≤ 10 s; SC-005 zero startup regression with no registration
**Constraints**: zero GLP language surface (FR-006); converted files under `out/csharp/bin` + `out/csharp/lib` are NOT edited (codeconv discipline) — only the hand-authored shim `out/csharp/glp_repl/Program.cs` and `csharp/` libraries change
**Scale/Scope**: chat-scale WAL (thousands of ops; replay linear, accepted per spec assumption)

## Constitution Check

*GATE evaluated pre-Phase-0 and re-checked post-Phase-1 — PASS (no violations, no complexity entries).*

- **I. Spec-First**: 064 spec written, validated, quoted throughout the design docs — PASS.
- **II. Bug-Protocol**: the QUIC single-shot connect is treated as the spec'd FR-008 work item (contracts/quic-connect-retry.md), not silently patched — PASS.
- **III. SRSW**: no GLP clauses change; zero `skipSRSW` tokens in artifacts — PASS (machine-check).
- **IV-a. Language Authority**: FR-006 pins ZERO language surface; the store_put/store_get variant was rejected at intake, so no §1.14 approval is required — PASS by construction.
- **IV-b. Preserve Working Internals**: additive-only hooks (`LinkPump.OnDelivered` nullable, default null ⇒ byte-identical); no removals — PASS.
- **V. Claude-Only LM**: no LM in the runtime path; zero `OPENAI_API_KEY`/`litellm`/`openai` tokens — PASS (machine-check).
- **VI-a. Additive migrations**: no schema migration in this feature (PgliteOpWal's schema already exists; if a table-ensure is needed it is the store's existing EnsureSchema idiom, additive) — PASS.
- **VI-b. Single PGlite cluster**: WAL primary rides the one `.pgdb/` cluster through the existing conventions; file fallback is not a second cluster — PASS.
- **VII. Test-Gated Shipping**: baseline suites green before implement; drills per SC-001/002/003; buildkit GitFlow ship — PASS (process).
- **VIII. Single Source of Truth**: this plan references research/contracts rather than duplicating; roadmap linkage: `durable-listener-service-box` (promoted) — PASS.

## Project Structure

### Documentation (this feature)

```text
specs/064-durable-listener-service-box/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions R1–R7 with code anchors
├── data-model.md        # Phase 1 — ResumeRegistration, MessageLogEntry, ServiceListenerEndpoint
├── quickstart.md        # Phase 1 — register → run → survive-restart walkthrough
├── contracts/
│   ├── resume-registration.md      # file schema + launch sequence + diagnostics
│   ├── message-log-and-replay.md   # append hook, WAL composition, replay semantics
│   └── quic-connect-retry.md       # TCP-parity retry loop
└── tasks.md             # Phase 2 (/bk-tasks — not created by /bk-plan)
```

### Source Code (repository root)

```text
out/csharp/glp_repl/
└── Program.cs                 # shim (hand-authored): registration read, replay, resume run, WAL wiring  [EDIT]

csharp/glp_link/
├── primitives/LinkPump.cs     # + optional OnDelivered hook (additive, default null)                     [EDIT]
└── transports/QuicTransport.cs# ConnectAsync retry-until-ct loop (TCP parity)                            [EDIT]

csharp/glp_crdtmsg/store/      # IOpWal / OpWal / PgliteOpWal / Projection — REUSED, no edits expected

csharp/glp_link.tests/         # QuicTransport retry tests (dial-before-listen, budget exhaustion)        [ADD]
csharp/glp_crdtmsg.tests/      # append-hook + replay-idempotence tests                                    [ADD]
out/csharp/glp_repl/           # REPL-level resume drills via piped stdin (scripted in test/)             [ADD]

glpservice/                    # runtime artifacts (resume.json authored by operator; wal/ fallback)      [gitignored except a sample]
test/run_all_tests.sh          # + Section for the restart drill if scriptable headlessly                 [EDIT if feasible]
```

**Structure Decision**: extend the existing host composition root + two existing
`csharp/` libraries in place; no new projects, no new storage seams. The only
generated-code boundary touched is the hand-authored shim (explicitly not a
converted file).

## Complexity Tracking

No Constitution violations — table intentionally empty.
