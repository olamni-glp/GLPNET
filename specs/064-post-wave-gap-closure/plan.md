<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Post-wave consolidation — verified gap closure (REPL/engine + Full-Gleam)

**Branch**: `064-post-wave-gap-closure` | **Date**: 2026-08-03 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/064-post-wave-gap-closure/spec.md` (four clarify rulings encoded 2026-08-03)

## Summary

Close the 3rtask-verified residual gaps (run 20260803T133715Z-20ac) in five prioritized stories: (US1) bring the Gleam link to full C#-parity distributed semantics — distributed unification, quiescence oracle, multi-accept, plus QUIC-WS mesh access via the C# bridge; (US2) wire the C# engine host to serve multiple real clients through the shipped A31 GLP control program over a continuous multi-accept TCP loop; (US3) add an IL request kind to the split protocol and factor the compiler out of the engine execute path; (US4) BUILD the Gleam FE/BE process split and the yngenios embeddability artifact, then discharge the 059 acceptance sweep; (US5) close `:boot`, bytecode-lint, and the `param_arity` panic. MVP gate = US1+US2. Reference semantics: the C# link (FCP writer-MGU across the link); execution oracle: the Dart runtime; every checkpoint zero-regression across all suites.

## Technical Context

**Language/Version**: Gleam 1.17.0 / OTP 25 (glp_gleam, WSL smoke gate); C# .NET 8 (csharp/*); Dart ^3.9.4 (oracle runtime); bash (suite harnesses)
**Primary Dependencies**: existing glp_gleam link stack (primitives/reliability/seam/transports), csharp glp_link + glp_engine_host + glp_split_protocol + glp_il_codec (CompiledIlEnvelope), test/parity + cross_runtime harnesses, FCP reference semantics (github.com/EShapiro2/FCP Savannah mirror)
**Storage**: N/A (no new persistence; marathon/pipeline state via existing buildkit catalog)
**Testing**: gleeunit (glp_gleam, 569+ green baseline), xUnit (C# suites: glp_link 161, il_codec 64, engine-host 55+), bash REPL suite (549 baseline incl. A31), parity corpus 206/206, cross-runtime Section I 18/18 — all zero-regression gates
**Target Platform**: Windows (C#/Dart) + WSL/BEAM (Gleam); cross-host TCP/ZMQ/QUIC-WS(bridged)
**Project Type**: multi-runtime distributed-language runtime extension
**Performance Goals**: cross-runtime suite stable ×10 consecutive loops (SC-001); no suite slowdown >2× per scenario wall-clock
**Constraints**: zero regression at every checkpoint (FR-010); no new GLP language surface without §1.14 approval (FR-011, propose-only); `{exit_on_close, false}` + D-9 barrier on every new BEAM socket path (FR-012); C# link semantics are frozen reference — no C#-side semantic drift while porting
**Scale/Scope**: 5 user stories; est. 40–50 tasks; touches glp_gleam/src/glp/link+engine+repl, csharp/glp_link, csharp/glp_engine_host, csharp/glp_split_protocol, csharp/glp_repl_client, test/parity, test/run_all_tests.sh

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.x — PASS pre-Phase-0; re-checked PASS post-Phase-1.*

- **I. Spec-First**: PASS — every story traces to spec FRs; link semantics implement against the C# reference + FCP source (DISCIPLINE §1.13), with the 050/059/060 spec task lists as authoritative enumerations. dist_unify/quiescence get contracts (Phase 1) BEFORE implementation.
- **II. Bug-Protocol**: PASS — plan mandates STOP-and-report on any discovered core-GLP or C#-reference bug; no tolerance shims (the D-9/exit_on_close lesson is encoded as FR-012, a spec-backed norm, not a workaround).
- **III. SRSW**: PASS — no SRSW-bypass token anywhere in the artifacts (machine scan clean); the A31 control program is already SRSW-clean and is wired, not modified.
- **IV-a. Language Authority**: PASS — FR-011 makes any new guard/kernel/directive propose-only; the plan's US1 design explicitly targets zero new GLP surface (link-level implementation under existing kernels).
- **IV-b. Preserve Working Internals**: PASS — no removal of `_ClauseVar`/`_TentativeStruct`/fallbacks; Gleam runner extensions are additive dispatch arms.
- **V. Claude-Only LM**: PASS — no LM-in-the-loop components in this feature.
- **VI-a/VI-b. Persistence**: PASS — no migrations, no new clusters.
- **VII. Test-Gated, Commit-Scoped**: PASS — baseline-green → change → re-test per checkpoint; scoped commits; GitFlow ship.
- **VIII (marathon)**: PASS — run mrun-35df7ddfe4ec uses the deploy-home store; 024 schema untouched.

## Project Structure

### Documentation (this feature)

```text
specs/064-post-wave-gap-closure/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (dist-unify, quiescence, il-request-kind, febe-split)
└── tasks.md             # Phase 2 output (/bk-tasks)
```

### Source Code (repository root)

```text
glp_gleam/src/glp/link/
├── primitives/dist_unify.gleam        # NEW: distributed unification (writer-MGU across link)
├── primitives/quiescence.gleam        # NEW: quiescence oracle over goal states + in-flight counts
├── transports/multi_accept.gleam      # NEW: N-concurrent-link listener (E12: exit_on_close false)
└── transports/bridge_client.gleam     # NEW: dial helper targeting the C# QUIC-WS bridge endpoint

glp_gleam/src/glp/repl/commands.gleam  # EXTEND: :boot (multi-isolate plays)
glp_gleam/src/glp/bytecode/lint.gleam  # COMPLETE: bytecode-lint checks
glp_gleam/src/glp/engine/…             # FIX: param_arity panic → reported error
glp_gleam/src/glp/fe/…  glp_gleam/src/glp/be/…   # NEW (US4): FE/BE process split entrypoints
glp_gleam/src/glp_embed.gleam          # NEW (US4): embeddability surface (G3-A)

csharp/glp_link/transports/TcpTransport.cs      # EXTEND: continuous multi-accept ListenAsync loop
csharp/glp_engine_host/EngineServer.cs          # EXTEND: multi-client serve path (control-program wiring)
csharp/glp_engine_host/ClientSession.cs         # NEW: per-client channel pair + reply routing
csharp/glp_split_protocol/WireProtocol.cs       # EXTEND: LOAD_IL / RUN_GOAL_IL request kinds
csharp/glp_repl_client/…                        # EXTEND: local compile + CompiledIlEnvelope send
csharp/glp_quick_host/…                         # EXTEND: bridge acceptor for Gleam peers

test/parity/cross_runtime/…            # EXTEND: dist-unify/quiescence/multi-link/bridge scenarios
test/run_all_tests.sh                  # EXTEND: new sections; A31 stays green
glp_gleam/test/…                       # NEW suites per new module
csharp/*.tests/…                       # NEW suites per extended project
```

**Structure Decision**: extend the existing per-runtime trees in place — no new top-level projects except the two US4 build surfaces (fe/be entrypoints, embed surface) inside glp_gleam, mirroring how the C# split lives inside csharp/.

## Phase 0 → research.md (complete)

Decisions recorded: (D1) dist_unify ports the C# link's remote-binding protocol 1:1 (message kinds, ordering, writer-MGU checks) with the FCP Savannah emulator as tie-breaker — full parity per clarify Q1; (D2) quiescence uses the C# oracle's algorithm (goal-state census + in-flight message accounting over the link seam) — no Dijkstra-Scholten generalization beyond what C# does; (D3) QUIC-WS by bridge per clarify Q2 — glp_quick_host gains a Gleam-facing TCP acceptor that relays to the QUIC mesh, Gleam gains a bridge_client dial helper; native quicer NIF recorded as gated deferral; (D4) FE/BE split reuses the proven C# split shape: wire = the existing split protocol over the Gleam TCP transport, FE = thin REPL loop, BE = engine+scheduler process — per clarify Q3 BUILD ruling; (D5) embeddability = a public Gleam module surface (load/run/observe) consumable from a host BEAM app, per G3-A; (D6) MVP gate = US1+US2 per clarify Q4; Anchor review after US2, incremental reviews after US3/US4/US5.

## Phase 1 → data-model.md, contracts/, quickstart.md (complete)

Contracts to author: `contracts/dist-unify.md` (message kinds, ordering, failure modes, parity matrix vs C#), `contracts/quiescence.md` (oracle states, census protocol, fault interaction), `contracts/il-request-kind.md` (LOAD_IL/RUN_GOAL_IL frames wrapping CompiledIlEnvelope, version/digest refusal taxonomy), `contracts/febe-split.md` (process roles, lifecycle, embeddability surface). Agent context: CLAUDE.md buildkit block points at this plan.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) — all gates pass | — | — |
