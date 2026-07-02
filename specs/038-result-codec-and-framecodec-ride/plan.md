# Implementation Plan: Result-Envelope Codec (rides the Section-15 term codec)

**Branch**: `038-result-codec-and-framecodec-ride` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/038-result-codec-and-framecodec-ride/spec.md`

## Summary

Deliver the heap-independent **result envelope** — the result side of the ratified ED-1 seam — and its **byte codec**, riding the Section-15 *term* codec (ED-6 obl#1). The envelope `{Status, ResolvedBindings, var→writer, suspended, captured, Error}` carries **no live heap address** (server-side deep-resolve before emission) and encodes/decodes **byte-identically across Dart, C#, and Gleam**. Framing and transport are explicitly out of scope (they move to link-layer #36).

**Technical approach** (simplest design that satisfies the spec): author the envelope's term encoding on the **already-shipped 029 term-codec conventions** (the `ConstantCodec` tag table `0x00–0x06` + `ByteIo` LEB128 varints / fixed 8-byte little-endian int64 / IEEE-754 double-bits / varint+UTF-8 strings), add a thin **envelope header** (a new `RESULT_ENVELOPE` payload-type byte) plus status / map / set framing in the same conventions, implement matching encoders+decoders in **Dart (source of truth)**, **C# (reference)**, and **Gleam (on the 034 term/heap layer)**, and prove parity with a **cross-runtime byte-diff harness** over a golden FB-M1/M2 result corpus.

**Why this is buildable now**: the envelope is **terms + scalars, not opcodes**. It rides only the *term* portion of Section-15, whose concrete conventions already exist and shipped (029). It therefore does **not** require the unbuilt F5 Gleam bytecode runner, nor the v2-ISA opcode freeze, to reach term-portion byte-parity. Only **float**, **64-bit-int edge**, and **cyclic-term** parity — plus the declaration that the *whole* Section-15 codec is byte-parity-**final** — remain gated (see Constraints + research.md).

## Technical Context

**Language/Version**: Dart (source-of-truth runtime, `glp_runtime/`); C# (reference runtime, `csharp/` + `out/csharp/`); Gleam ≥1.17 on BEAM / **AtomVM 0.6.6** (`glp_gleam/`).
**Primary Dependencies**: 029 `GlpRuntime.IlCodec` term-codec conventions (`ByteIo`, `ConstantCodec` tags `0x00–0x06`) = the C# **oracle only** (FR-007); 034 Gleam `glp/runtime/{terms,heap,suspension,unify}.gleam` (`Constant`/`Term`/`VarRef`, `deref → Bound|Unbound`); existing `ExecutionResult` (`glp_runtime/lib/engine/glp_engine.dart:34-48`, `out/csharp/lib/engine/glp_engine.cs:51-80`); `DrainResult.{SuspendedGoals,BlockingReaders}` (`scheduler.cs:58-91`); deep-resolve `_ResolveDeepForTrace` (`glp_engine.cs:607-619`, depth-32).
**Storage**: N/A (in-memory values + byte streams).
**Testing**: Dart `dart test`; C# xUnit; Gleam `gleam test` (BEAM); a **cross-runtime byte-diff conformance harness** over a shared golden corpus; optionally a Lean `decode∘encode=id` round-trip proof mirroring 029.
**Target Platform**: in-process **and** over-the-wire (M2); Gleam encoder must run on AtomVM 0.6.6.
**Project Type**: multi-runtime **codec library**, additive-only.
**Performance Goals**: not a hot path — correctness + byte-parity first; deep-resolve is server-side and bounded.
**Constraints**: 0 live heap addresses in any envelope (SC-003); `decode(encode(R)) == R` on the in-scope corpus (SC-001); byte-identical Dart/C#/Gleam encodings (SC-002); loud-fail on trailing/garbage bytes or unknown tag (SC-004); **additive-only** — no GLP language change, no runtime/scheduler/compiler/REPL semantics change.
**Scale/Scope**: the recorded FB-M1-17 / FB-M1-41 / FB-M1-42 Dart result outcomes + the shared FB-M2-06 cross-runtime corpus.

**NEEDS CLARIFICATION (resolved in research.md)**: deep-resolve depth/truncation policy (Dart currently silently truncates at depth 32); the `RESULT_ENVELOPE` payload-type byte value; endianness declaration (029 term codec is little-endian; #36 FrameCodec TLV is big-endian — separate seam); reuse-vs-mirror of 029's `ByteIo`/`ConstantCodec` primitives; var→writer global-id (`agentId:localId`) construction in Gleam (034 has only local `VarRef(addr)`); D5/FORK-1 cyclic-term handling (defer to runtime, never self-decide).

## Constitution Check

*GATE: must pass before Phase 0. Re-checked after Phase 1.*

| Principle | Assessment | Verdict |
|---|---|---|
| I Spec-First | spec.md identified + ridden; owner gates D4=A / ED-6=A encoded (spec Clarifications, FR-010/011). | PASS |
| II Bug-Protocol / No-Workarounds | The silent depth-32 truncation is treated as a decision to resolve (explicit policy), NOT masked with try/catch. | PASS |
| III SRSW inviolable | Codec preserves the var→writer reader/writer polarity; **0** `skipSRSW` tokens introduced. | PASS (machine-checkable) |
| IV-a Language Authority | **No** GLP language change (serialization + result-collection only; no new guard/predicate/kernel/type/primitive). The v2-ISA freeze (D4) it depends on is **owner-ruled A**. | PASS |
| IV-b Preserve internals | Strictly additive; removes nothing (`_ClauseVar`/`_TentativeStruct`/fallbacks untouched). | PASS |
| V Claude-only LM | No external API anywhere. **0** `OPENAI_API_KEY`/`litellm`/`openai`. | PASS (machine-checkable) |
| VI-a/-b Persistence | No migrations, no PGLite cluster. | N/A |
| VII Test-gated, commit-scoped | Baseline-green→change→re-test; commit-by-name; ship via GitFlow. | PASS |
| VIII Single source of truth | Rides ED-6 Section-15 term codec; cites 029 as **C#-only oracle** (FR-007), never as Dart/Gleam proof; references, does not duplicate. | PASS |

**No violations → Complexity Tracking empty.**

## Project Structure

### Documentation (this feature)

```text
specs/038-result-codec-and-framecodec-ride/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions + rejected alternatives
├── data-model.md        # Phase 1 — Result Envelope entity + Section-15 term tag table
├── quickstart.md        # Phase 1 — build + run the cross-runtime byte-parity harness
├── contracts/
│   └── result-envelope-codec.md   # Phase 1 — wire format contract (header/tags/loud-fail/parity criterion)
└── tasks.md             # Phase 2 — /bk-tasks (NOT created by /bk-plan)
```

### Source Code (repository root)

```text
glp_runtime/                                  # Dart — SOURCE OF TRUTH (authored first)
├── lib/codec/
│   ├── result_envelope.dart                  #   the envelope value type (Status/ResolvedBindings/var→writer/suspended/captured/Error)
│   ├── term_codec.dart                        #   term sub-codec (ByteIo varints/LE + Constant/Term tags 0x00–0x06)
│   └── result_envelope_codec.dart             #   encode/decode envelope; loud-fail on trailing/unknown
└── test/codec/
    ├── result_envelope_codec_test.dart        #   round-trip + no-heap-address + loud-fail
    └── golden_corpus_test.dart                #   asserts byte-identity to specs/.../contracts golden

csharp/glp_result_codec/                      # C# — REFERENCE (clobber-safe new dir, like 029)
├── ResultEnvelope.cs
├── TermCodec.cs                               #   mirrors 029 ByteIo/ConstantCodec conventions (byte-parity on the wire, not code reuse)
├── ResultEnvelopeCodec.cs
└── tests/                                      #   xUnit round-trip + golden byte-identity

glp_gleam/src/glp/codec/                       # Gleam — PORT (on 034 terms/heap; runs on AtomVM 0.6.6)
├── term_codec.gleam                            #   encodes glp/runtime/terms.Term via the same tags
└── result_envelope.gleam                       #   envelope + deep-resolve over glp/runtime/heap.deref
glp_gleam/test/glp/codec/
└── result_envelope_codec_test.gleam            #   round-trip + golden byte-identity

specs/038-.../contracts/golden/                 # shared golden corpus: logical-result → expected hex bytes
```

**Structure Decision**: a **codec layer added in each runtime**, parallel to (not refactoring) the shipped 029 IL codec. The C# result codec **mirrors** 029's wire conventions rather than reusing 029's `internal` `ByteIo`/`ConstantCodec` (byte-parity is a property of the bytes, not shared code — and 029 is shipped and must not be churned; a conformance test asserts the result codec's term bytes equal 029's `ConstantCodec` bytes for shared term inputs). The Dart runtime is authored first and emits the **golden corpus**; C# and Gleam encoders must reproduce it byte-for-byte.

## Complexity Tracking

> No Constitution violations — section intentionally empty.
