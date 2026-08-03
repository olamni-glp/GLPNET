<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Wave 3 consolidated — Full Gleam chain

**Branch**: `060-wave3-full-gleam-chain` | **Date**: 2026-07-27 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/060-wave3-full-gleam-chain/spec.md`

## Summary

Wave 3 is **gap-closing, not greenfield**. `glp_gleam/src/` already carries 72 modules — lexer/parser, the full analysis and type-checker stack, bytecode opcodes and program representation, compiler codegen + loader + partial-eval, an engine with runner/scheduler/kernels, a REPL surface, the link seam with loopback/TCP/ZMQ transports, term and result-envelope codecs, and the `mad` multiagent layer. A parity harness already exists under `test/parity/` (`corpus.list`, `expected.list`, `run_gleam_corpus.sh`, `run_differential.sh`, `record_dart_goldens.sh`).

What feature 059's verification recorded as **ABSENT or PARTIAL** is precisely this wave's work:

| Area | 059 verdict | Wave-3 close |
|---|---|---|
| Module static linking / dynamic dispatch | ABSENT (`Unimplemented distribute`) | FR-008, FR-009 |
| `reduce` metainterpreter | PARTIAL (missing `_copy/2`) | FR-004 support |
| Bytecode lint | ABSENT (placeholder `glp/lint.gleam`) | FR-004 quality gate |
| REPL `:boot` / `:bytecode` | ABSENT | FR-011, FR-014 |
| Inbound pump, link acceptance, capability gate, instance network join | ABSENT (T050–T058 open) | FR-020…FR-024 |
| Multiagent boot loader | ABSENT (empty module) | supports US4/US5 |
| Engine composition root | PARTIAL (kernels compiled-in, no transport injection seam) | FR-020 prerequisite |
| Corpus goldens | HALT/ESCALATE — 44 missing (T051 drift) | FR-018a, FR-018b, SC-010 |

The approach is therefore: **inventory the real gap per module, close it behind the existing seams, and let the parity harness be the acceptance instrument.** No new architecture is introduced; the transport seam, bytecode set, and wire format are consumed as-is.

## Technical Context

**Language/Version**: Gleam (Erlang/BEAM target), with Erlang FFI shims (`*_ffi.erl`) for transport and platform edges  
**Primary Dependencies**: `gleam_stdlib`, `gleam_erlang`, `gleeunit`; the Dart reference runtime (arbiter of correctness); the C# GLP engine (cross-runtime peer)  
**Storage**: N/A — source files and corpus goldens on disk; no database in the feature's runtime path  
**Testing**: `gleeunit` (58 existing Gleam test modules); the bash parity harness `test/parity/{run_gleam_corpus,run_differential,record_dart_goldens}.sh`; the repo REPL suite `test/run_all_tests.sh` as the non-regression baseline  
**Target Platform**: full BEAM (FR-031). Embedded AtomVM deferred, with FR-032 forbidding constructs that would foreclose it  
**Project Type**: language runtime + compiler, plus a distributed link layer  
**Performance Goals**: none for wave 3 beyond liveness — parity of *outcome*, not speed (spec Assumptions)  
**Constraints**: SRSW inviolable; bytecode instruction set and wire format consumed unchanged (owner approval required to alter); loopback + TCP are the only acceptance transports; peer-loss detection bounded at 30 s (SC-007)  
**Scale/Scope**: ~72 Gleam source modules, 58 test modules, Gleam test baseline 465 green; corpus of which 44 cases currently lack goldens

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate-ability | Assessment |
|---|---|---|
| **I. Spec-First** | judgement | **PASS** — `spec.md` exists, clarified, and every plan item traces to an FR. No implementation precedes it. |
| **II. Bug-Protocol / No-Workarounds** | judgement | **PASS with a standing obligation** — the 44 missing goldens are an *escalation carried forward*, recorded as out-of-scope with a reason (FR-018a) rather than papered over by lowering the pass bar. Any divergence found during the wave must STOP and be reported, not "handled". |
| **III. SRSW inviolable** | machine | **PASS** — zero occurrences of `skipSRSW` in the artifacts under review. FR-005 requires load-time SRSW enforcement in the Gleam loader. |
| **IV-a. Language Authority** | judgement | **PASS** — the wave consumes the existing instruction set, guards, and kernels. Technical Context records that altering them requires owner approval. No new language surface is proposed. |
| **IV-b. Preserve Working Internals** | judgement | **PASS** — the work is additive gap-closing behind existing seams; no removal of load-bearing internals is planned. Existing transports (including the new ZMQ leaf) stay in place even though unproven this wave. |
| **V. Claude-only LM** | machine | **PASS** — zero occurrences of `OPENAI_API_KEY` / `litellm` / `openai`. This feature has no LM-in-the-loop path at all. |
| **VI-a. Additive, idempotent, single-head migrations** | machine | **PASS — not applicable.** The feature introduces no migration; the single head stays at `0010`. |
| **VI-b. Single PGLite cluster** | judgement | **PASS — not applicable.** No repo working-data cluster access in the feature runtime. The marathon run backing this feature uses the sanctioned out-of-repo store. |
| **VII. Test-gated, commit-scoped shipping** | advisory | **PASS** — baseline is the current Gleam 465-green plus the repo REPL suite; every marathon checkpoint uses a scoped commit with explicit file paths (already enforced — the harness refused a directory path). |
| **VIII. Single source of truth & traceability** | judgement | **PASS, one advisory note** — roadmap→pipeline→tasks traceability holds. Advisory-only slug drift: roadmap feature `wave-3-consolidated-full-gleam-chain` will not auto-link to spec dir `060-wave3-full-gleam-chain`. Principle VIII marks the roadmap-linkage clause advisory and pre-existing drift not retroactively flagged. |

**Result: no violations. Complexity Tracking is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/060-wave3-full-gleam-chain/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── repl-commands.md
│   ├── link-handshake.md
│   └── corpus-report.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/bk-tasks)
```

### Source Code (repository root)

```text
glp_gleam/
├── gleam.toml
├── src/
│   ├── glp_gleam.gleam                  # entry point
│   ├── glp/
│   │   ├── parser/{lexer,parser,ast}.gleam          # front end (DELIVERED)
│   │   ├── analysis/                                 # SRSW + type checker (DELIVERED)
│   │   │   ├── srsw.gleam, type_ast.gleam, prelude.gleam
│   │   │   └── type_checker/*.gleam
│   │   ├── bytecode/{opcodes,program,guard_defs}.gleam   # instruction set (DELIVERED)
│   │   ├── compiler/{codegen,loader,partial_eval}.gleam  # ← module linking gap (US1)
│   │   ├── lint.gleam                                    # ← placeholder, to implement (US1)
│   │   ├── engine/                                       # ← composition-root seam gap (US4)
│   │   │   ├── runner.gleam, scheduler.gleam, kernels.gleam
│   │   │   ├── goal_boot.gleam, output_capture.gleam
│   │   │   └── arith.gleam, goal_format.gleam, types.gleam
│   │   ├── repl/{repl,commands,results}.gleam            # ← :boot/:bytecode gap (US2)
│   │   ├── runtime/{terms,heap,unify,suspension}.gleam   # (DELIVERED)
│   │   ├── codec/{term_codec,result_envelope*}.gleam     # wire format (DELIVERED)
│   │   ├── link.gleam
│   │   ├── link/seam/*.gleam                             # transport seam (DELIVERED)
│   │   ├── link/reliability/{frame_codec,crc32}.gleam    # PARTIAL — floor only
│   │   ├── link/transports/{loopback,tcp,zmq}.gleam      # ← acceptance: loopback+TCP (US4)
│   │   └── mad/*.gleam                                   # ← boot loader gap (US4/US5)
│   └── ...
└── test/                                 # 58 gleeunit modules

test/parity/                              # cross-runtime acceptance harness
├── corpus.list, expected.list, corpus-manifest.md
├── run_gleam_corpus.sh                   # ← US3 instrument
├── run_differential.sh                   # ← US3/US5 instrument
└── record_dart_goldens.sh                # ← US3 golden regeneration (FR-018b)

test/run_all_tests.sh                     # repo REPL suite — non-regression baseline
```

**Structure Decision**: The existing `glp_gleam/` subtree and `test/parity/` harness are used unchanged in shape. Every wave-3 task lands inside a module that already exists, or adds a sibling module under an existing namespace. No new top-level directory is created. The parity harness is the acceptance instrument for US3 and US5; `gleeunit` covers unit-level work inside US1, US2, and US4.

## Phase plan

- **Phase 0 — Research** (`research.md`): pin down the exact gap in each ABSENT/PARTIAL area against the source, and record the decisions already taken at clarify.
- **Phase 1 — Design & contracts**: `data-model.md` for the entities the spec names; `contracts/` for the three interfaces this feature exposes — REPL command surface, link handshake, corpus report format; `quickstart.md` for the 5-minute path in SC-003.
- **Phase 2 — Tasks** (`/bk-tasks`): one phase per user story, MVP = US1.

## Complexity Tracking

*No constitution violations. Section intentionally empty.*
