# Implementation Plan: GLEAM implementation — combined Full-Gleam feature

**Branch**: `050-full-gleam-combined` | **Date**: 2026-07-10 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/050-full-gleam-combined/spec.md`

## Summary

Deliver a complete standalone Gleam GLP instance end-to-end, in two milestones mirroring the 036 baseline-program dossier:

- **M1 — standalone instance**: hand-ported recursive-descent parser + full load pipeline (SRSW → partial evaluation → type check → compile → load), three-phase HEAD/GUARD/BODY bytecode engine over the delivered 034 immutable heap (generation-scoped activation dedup, positional X-registers, writer-MGU), standalone REPL (`:trace`/`:limit`/`:quit`) exposing the engine as a typed in-process value through the existing result-envelope seam, and the shared test corpus ported with 100% agreement against recorded Dart golden outputs (GAP-G1/G2/G3/G8 + FORK-1 explicit; MISS-04 differential harness built).
- **M2 — distributed instance**: link-layer port (link primitives, byte-for-byte TLV term codec + FrameCodec parity, deferred-local-assignment dist-unify, globalize/localize on `known/1`, fault-as-data) over **all three transports** (loopback, TCP via `gen_tcp`, QUIC-WS/HTTP3 via the `gleam_quic` Profile-C groundwork), quiescence oracle (GAP-G6), and the C#↔Gleam split-pair capstone at 16/16.

Both OPEN proof obligations (writer-MGU-under-value-copy, distributed-dereference convergence) are discharged as **Lean + prose + adversarial tests** per the 2026-07-10 clarification.

## Technical Context

**Language/Version**: Gleam 1.17.0 targeting Erlang/OTP 29 (erts 17.0.3); Dart 3.x (`glp_runtime/`) is the reference oracle; C# .NET 10 (`glp_runtime_net/` + `csharp/glp_link/`) is the peer runtime for the capstone.
**Primary Dependencies**: `gleam_stdlib`, `gleam_erlang`, `gleeunit` (dev) — **`gleam_otp` is deliberately excluded** (FR-007; AtomVM-compatibility by construction). QUIC: `quicer`/MsQuic via `gleam_quic/profile_c/` (rebar3). Lean 4 + Lake for mechanized proofs.
**Storage**: N/A (no persistence in this feature).
**Testing**: `gleeunit` unit tests (run under WSL — known Windows path-separator defect in test discovery); bash corpus runner (`test/run_all_tests.sh` shape); new differential harness (`test/parity/`); cross-runtime link rig extending `test/link/run_link_tests_cross.sh`; Lean `lake build` as proof check.
**Target Platform**: BEAM on Windows (native `gleam build --target erlang`); WSL for `gleam test` and for the QUIC (Profile-C) runtime, which is WSL-only per feature 049.
**Project Type**: runtime port — compiler + VM + REPL + network layer inside the existing `glp_gleam/` subtree.
**Performance Goals**: full shared corpus on Gleam within 10× recorded Dart wall-clock (SC-009 sanity bound; no stricter optimization goal).
**Constraints**: no `gleam_otp`; byte-for-byte codec/wire parity with the shipped C# encodings; language semantics frozen (no new guards/kernels/types — Constitution IV-a); Dart runtime is source of truth for ported semantics; canonical-bytecode v2.16 (`docs/glp-bytecode-v216-complete.md`) is normative.
**Scale/Scope**: six folded subsystems; `glp_gleam` grows from ~1,200 real lines (034 terms/heap/unify + 038 codecs) to a full instance; corpus of several hundred cases; 16-scenario cross-runtime suite ×3 transports.

## Constitution Check

*GATE: evaluated against constitution v1.1.0 before Phase 0; re-checked after Phase 1 design.*

| Principle | Status | Note |
|---|---|---|
| I. Spec-First | PASS | This plan derives from spec.md (clarified 2026-07-10); ported semantics carry their Dart/dossier spec anchors (bytecode v2.16 doc, 025 contracts, 036 dossier registers). |
| II. Bug-Protocol / No-Workarounds | PASS | Parity divergences found during the port are reported against the reference behaviour, never patched around (research.md R4 protocol). |
| III. SRSW inviolable | PASS | The Gleam type checker ports the existing SRSW check unchanged; no escape mechanism is introduced anywhere in these artifacts. |
| IV-a. Language Authority | PASS | No language change: the port implements already-approved semantics, including the existing `ground/1`-guard SRSW relaxation (dossier ruling D6 = the relaxation already live in Dart; typed-glp-manual §3). Any semantic question found mid-port STOPs and escalates. |
| IV-b. Preserve Working Internals | PASS | Port adds Gleam code; no Dart/C# internals (`_ClauseVar`, `_TentativeStruct`, fallbacks) are touched. |
| V. Claude-Only LM | PASS | No LM-in-the-loop component in this feature. |
| VI-a. Additive-only migrations | PASS (N/A) | No database schema work. |
| VI-b. Single PGLite cluster | PASS (N/A) | No new persistence; no cluster created. |
| VII. Test-Gated, Commit-Scoped Shipping | PASS | Baseline suites green before each phase; marathon scoped-commit checkpoints; ship via buildkit GitFlow. |
| VIII. Single Source of Truth | PASS | This plan references (never duplicates) the 036 dossier registers, 025 contracts, and the bytecode spec; corpus goldens get ONE recording location (research.md R4). |

**Post-Phase-1 re-check (2026-07-10)**: design artifacts (data-model.md, contracts/) introduce no violation — PASS unchanged. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/050-full-gleam-combined/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions R1–R8
├── data-model.md        # Phase 1 — runtime/wire/test entities
├── quickstart.md        # Phase 1 — build/run/test commands
├── contracts/
│   ├── gleam-instance-surface.md   # load pipeline, REPL, engine-as-value, envelope seam
│   ├── link-parity.md              # transports, framing, codec byte-parity, dist-unify
│   ├── corpus-parity.md            # goldens protocol, differential harness, 10× bound
│   └── proof-obligations.md        # the two OPEN proofs: form, location, acceptance
└── tasks.md             # Phase 2 (/bk-tasks — NOT created by /bk-plan)
```

### Source Code (repository root)

```text
glp_gleam/                          # the Gleam instance (all new code lands here)
├── gleam.toml                      # unchanged deps policy: stdlib+erlang+gleeunit, NO gleam_otp
├── src/
│   ├── glp/
│   │   ├── parser/                 # NEW: hand-ported RD lexer+parser (from glp_runtime/lib/compiler/)
│   │   ├── analysis/               # NEW: SRSW check + type checker port (fills analysis.gleam stub)
│   │   ├── compiler/               # NEW: partial evaluator + codegen → v2.16 bytecode (fills compiler.gleam)
│   │   ├── bytecode/               # NEW: opcode defs + program loader (fills bytecode.gleam)
│   │   ├── engine/                 # NEW: 3-phase runner + scheduler + activation dedup (fills engine.gleam)
│   │   ├── runtime/                # EXISTS (034): terms, heap, unify, suspension — extended, not rewritten
│   │   ├── codec/                  # EXISTS (038): term_codec, result_envelope(+builder) — reused as-is
│   │   ├── repl/                   # NEW: REPL loop, :trace/:limit/:quit, engine-as-typed-value seam
│   │   └── link/                   # NEW: primitives/, reliability/ (FrameCodec port), seam/, transports/
│   │       └── transports/         #   loopback, tcp (gen_tcp), quic_ws (via gleam_quic FFI)
│   └── glp_gleam.gleam             # entry point (gleam run — REPL main)
├── test/glp/                       # gleeunit tests per subsystem (WSL)
└── lean/                           # NEW: WriterMguBindsOnlyWriters/, DistDerefConvergence/ (Lake projects)

gleam_quic/                         # EXISTS: BEAM QUIC groundwork (quicer/MsQuic, profile_c) — wired via FFI, not forked
test/parity/                        # NEW: differential harness (MISS-04) + recorded Dart goldens
│   ├── record_dart_goldens.sh      #   one-time+refresh recording against glp_runtime
│   ├── run_differential.sh         #   same program on Dart/C#/Gleam → diff outputs
│   └── goldens/                    #   the ONE recording location for corpus goldens
test/link/                          # EXISTS: cross-runtime rig — extended with C#↔Gleam runs
programs/tests/                     # EXISTS: shared corpus source of truth (typed/, link/) — grows GAP/FORK cases
docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/   # prose proofs + INDEX status flips
```

**Structure Decision**: single-subtree growth — all Gleam code lands in the existing `glp_gleam/` package (033 scaffold placement), replacing the 033 placeholder modules with real subsystem directories; the shared corpus stays in `programs/tests/` (single source of truth); parity tooling gets one new `test/parity/` home; proofs live where the repo's Lean convention puts them (colocated `lean/` dir + P4 PROOFS index for prose).

## Complexity Tracking

No constitution violations to justify. The one scope-heavy decision — full multi-protocol transport parity including QUIC-WS/HTTP3 on BEAM — is an explicit owner clarification (2026-07-10), not a plan-introduced complexity; its risk is tracked in research.md R5.
