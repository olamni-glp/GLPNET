# Phase 0 Research: Result-Envelope Codec

**Feature**: `038-result-codec-and-framecodec-ride` · **Plan**: [plan.md](./plan.md)

Each decision below resolves a NEEDS CLARIFICATION from the plan's Technical Context. Format: **Decision / Rationale / Alternatives rejected**. Constraints and rejected options are called out explicitly (per planning guidance).

---

## R1 — The envelope rides the *term* portion of Section-15, not the bytecode portion

- **Decision**: Scope 038's codec to the **result envelope value** and the **term sub-codec** it needs. Encode `ResolvedBindings` and any `StructTerm` via the Section-15 *term* tags; do **not** encode opcode/IOpV2 programs (those are the bytecode portion of Section-15, governed by the v2-ISA freeze and the F5 runner).
- **Rationale**: The envelope fields are `{Status (enum), ResolvedBindings (terms), var→writer (ids), suspended (ids), captured (bytes, excluded), Error (string)}` — terms + scalars, never opcodes. The term sub-codec is the only Section-15 surface the envelope touches, and its concrete conventions already exist and shipped (029 `ConstantCodec`). This decouples 038 from (a) the unbuilt F5 Gleam bytecode runner and (b) the v2-ISA opcode freeze, both of which would otherwise block all parity.
- **Alternatives rejected**: (a) *Author the full Section-15 bytecode+term codec inside 038* — rejected: pulls in the opcode freeze (D4) and F5, blocking the envelope unnecessarily; the envelope needs no opcodes. (b) *Encode bindings as opaque blobs* — rejected: defeats heap-independent, structured `decode==R` fidelity (FR-004) and cross-runtime parity (FR-003).

## R2 — Wire conventions: ride the 029 term-codec byte layout verbatim

- **Decision**: Use exactly the 029 conventions for the term portion: **counts/lengths = unsigned LEB128 varint**; **int64 / register ids = fixed 8-byte little-endian**; **doubles = IEEE-754 bit pattern (8 bytes, `DoubleToInt64Bits`)**; **strings = varint length + UTF-8**; constant/term tags = `0x00 null · 0x01 bool · 0x02 int64 · 0x03 double · 0x04 string · 0x05 ConstTerm(atom) · 0x06 StructTerm(functor+arity+recurse)`. **Endianness = little-endian** for the term codec.
- **Rationale**: 029 is the shipped, Lean-proven (`decode∘encode=id`) C# reference; reusing its layout makes the C# result codec provably consistent with the existing oracle and gives Dart/Gleam a single concrete target. A conformance test asserts the result codec's term bytes are byte-identical to 029 `ConstantCodec` for shared term inputs.
- **Constraint / note**: The link-layer `FrameCodec`/`PayloadSerializer` TLV is **big-endian** (`FrameCodec.cs:30-32`) — a **different seam** (#36 term-link, not this codec). 038 declares little-endian and records the divergence as a #36 handoff fact; it does **not** reconcile them (FR-006).
- **Alternatives rejected**: (a) *Big-endian to match FrameCodec* — rejected: would diverge from the shipped 029 oracle and the term codec is not the FrameCodec seam. (b) *Protobuf/CBOR/MsgPack* — rejected: an external schema lib is heavier, is not byte-deterministic across three hand-written runtimes without pinning, and abandons the proven 029 layout.

## R3 — Envelope framing: a `RESULT_ENVELOPE` payload-type byte + length-prefixed sections

- **Decision**: Frame the envelope with a 2-byte header `{version 0x01, payloadType 0x11 RESULT_ENVELOPE}` (029 IL programs use `0x10`; the envelope takes the **next** value `0x11`), then: `Status` (1 byte: `0x00 success · 0x01 suspended · 0x02 failed`), `ResolvedBindings` (varint count, then `name:string + Term` pairs), `var→writer` (varint count, then `name:string + GlobalVarId`), `suspended` (varint count of `GlobalVarId`), `capturedLen + bytes`, `Error` (presence byte + optional string). Decode MUST consume every byte (loud-fail on trailing) and reject unknown tags/payloadType/version.
- **Rationale**: Mirrors the 029 `PayloadHeader` discipline (version + payloadType, loud version/type mismatch) so envelopes and IL programs are self-describing and never confusable on a shared wire; length-prefixing keeps each section independently parseable and supports the FR-005 loud-fail guarantees.
- **Alternatives rejected**: (a) *No payload-type byte (reuse 0x10)* — rejected: makes envelopes indistinguishable from IL programs (the exact #36 `OffKind`-is-fragmentation-not-type confusion, FR-006). (b) *Self-delimiting (no length prefixes)* — rejected: weakens loud-fail and complicates the Gleam/AtomVM decoder.

## R4 — `captured` is carried but EXCLUDED from the byte-parity criterion

- **Decision**: Include `captured` output in the envelope (as a length-prefixed byte/UTF-8 field) so a consumer reads it, but **exclude it from the SC-002 byte-identity criterion** and from the cross-runtime golden comparison.
- **Rationale**: Spec Edge Cases + `PB:9` exclude captured output from parity (capture ordering/interleaving is runtime-incidental, not a contract). Carrying-but-excluding keeps the field useful without making incidental capture differences fail parity.
- **Alternatives rejected**: *Drop captured entirely* — rejected: it is a named envelope field (FR-001); consumers may want it.

## R5 — Deep-resolve depth + truncation policy: depth-bounded, **explicitly signalled**, never silent

- **Decision**: Deep-resolve server-side to the **same depth-32 bound** as the Dart reference (`_ResolveDeepForTrace`, `glp_engine.cs:609`) so resolution **matches the reference, not over/under-resolves** (spec Edge Case). When the bound is hit, the envelope records an **explicit truncation marker** on that binding (an additive flag/sentinel term) — it does **not** silently truncate. The wire codec itself fails loudly on malformed bytes (FR-005); the truncation marker is a faithful, decodable value, not an error.
- **Rationale**: Reconciles two requirements — match the Dart reference depth (parity) and "fail loudly / no silent truncation" (FR-005, II No-Workarounds). Silent truncation is a masked bug; an explicit marker is additive and parity-stable across runtimes.
- **Open item surfaced to owner (not self-decided)**: whether to *raise* the depth-32 limit in the reference is a **reference-behavior change** (touches `_ResolveDeepForTrace`) and is therefore deferred — 038 matches the current bound + marks truncation; raising it is a separate owner-gated change.
- **Alternatives rejected**: (a) *Keep silent truncation* — rejected: violates FR-005/II. (b) *Unbounded resolve* — rejected: diverges from the Dart reference and risks non-termination on deep/cyclic terms (see R6).

## R6 — Cyclic / cross-goal cyclic terms: defer to the runtime's deref, never define a divergent policy

- **Decision**: The codec handles cyclic terms **consistently with the runtime's deref** (defer/error as the runtime does), and **surfaces the open D5/FORK-1 fork** rather than inventing cycle behavior (FR-008). Concretely: encode whatever the depth-bounded deref yields (R5); never loop. Final cyclic-term *correctness* is gated on the owner's D5 ruling.
- **Rationale**: D5 (FORK-1 cycle discriminator) is an **open owner fork**; the constitution + program rules forbid self-deciding it. Depth-bounding (R5) already prevents non-termination, so the codec is safe-by-construction pending D5.
- **Alternatives rejected**: *Define a codec-local cycle tag/policy now* — rejected: pre-empts the owner's D5 decision (FR-008) and risks diverging from runtime deref.

## R7 — var→writer uses GlobalVarId (`agentId:localId`), not the local heap address

- **Decision**: Encode variable→writer references by **global identity** `GlobalVarId` (scheme `agentId:localId`, FB-M1-14), never the local heap int (which is "meaningless cross-process", DISPOSITIONS #15). In Gleam, add a thin local→global mapping over 034's `VarRef(addr:Int)` at envelope-build time (034 carries only the local addr; the global id is assigned by the producing engine/agent, consistent with Dart/C#).
- **Rationale**: The envelope is heap-independent and cross-process (M2); only a global identity is meaningful to a remote consumer. This matches the Dart/C# `GlobalVarId` scheme so all three runtimes agree on the same identity bytes.
- **Alternatives rejected**: *Encode the local heap addr* — rejected: not heap-independent (violates FR-001/SC-003), not parity-stable across runtimes.

## R8 — 029 is the C# oracle ONLY (FR-007 hard line)

- **Decision**: Treat 029 `GlpRuntime.IlCodec` as the **C# reference oracle for the term-codec byte layout** and nothing more. Do **not** cite 029's passing tests/Lean proof as evidence that the **Dart** or **Gleam** envelope codec is correct. Each runtime earns its own round-trip + golden byte-identity tests.
- **Rationale**: 029 covers IL/bytecode programs in C# only; it has no Dart/Gleam mirror and does not encode result envelopes. Citing it for Dart/Gleam would be a false proof (FR-007, DECISIONS.md:46).
- **Alternatives rejected**: *Reuse 029's Lean proof as the codec's correctness argument* — rejected: the proof is over the C# IL model, not the envelope or the Dart/Gleam ports.

## R9 — Cross-runtime parity via a golden corpus authored from Dart (source of truth)

- **Decision**: Author a **golden corpus** `{logical result → expected hex bytes}` from the **Dart** encoder (source of truth). Each runtime's test (Dart/C#/Gleam) (a) encodes the logical result and asserts byte-equality to the golden, and (b) decodes the golden and asserts field-by-field reconstruction. SC-002 byte-parity = all three reproduce the golden on the non-gated corpus.
- **Rationale**: A single golden makes "byte-identical across three runtimes" a concrete, checkable invariant and pins the contract; it follows the established oracle pattern (Dart truth → C# reference → Gleam port) used by 034's parity corpus.
- **Alternatives rejected**: (a) *Pairwise runtime diffs with no golden* — rejected: no canonical reference, drift-prone. (b) *Generate Gleam via the 032 codeconv langpair and trust it* — rejected: 032 is the mirroring data-flow, **not** the conformance criterion; the hand-authored `glp_gleam/` codec is what ships and must pass the golden.

## R10 — Gleam encoder is buildable on 034 now (no F5 dependency)

- **Decision**: Build the Gleam envelope/term codec directly on 034's `glp/runtime/{terms,heap,suspension}.gleam` (`Term`, `deref → Bound|Unbound`). No dependency on the F5 bytecode runner (#26, unbuilt) because the envelope encodes terms, not opcodes.
- **Rationale**: 034 already provides `Term` (Const/Struct/VarRef), `deref`, and suspension records — exactly the envelope's inputs. The deep-resolve (R5) is a recursive walk over `deref`. The bignum-masking and float gates (below) are the only Gleam-specific risks.
- **Risk surfaced (F4 review carry-over)**: F4 immutable suspensions do NOT preserve the cross-writer single-fire guard ("F5 must dedupe activations by `goal_id`", 034 data-model:113). The envelope's `suspended` set is built from `goal_id`s; if a future runner double-counts activations, the envelope must dedupe by `goal_id` when assembling the set. Recorded as an implementation note for the suspended-set builder.

## R11 — Gated cases: float, 64-bit-int edges, whole-Section-15 "final"

- **Decision**: Implement the codec against a **candidate** layout and declare term-portion parity on the **non-gated** corpus now. Explicitly gate: **floats** (FR-011, ED-6 AtomVM 0.6.6 `/float` decode spike — pairs with the m2-0 AtomVM work, now provisioned); **64-bit-int edges** (Gleam `Int` is BEAM bignum → plain-BEAM green is NOT an AtomVM-faithfulness signal); and the declaration that the **whole** Section-15 codec is byte-parity-**final** (FR-009/FR-010 — needs the v2-ISA freeze + bytecode-portion authoring to LAND).
- **Rationale**: Honors the owner rulings (D4=A, ED-6=A) and the program's "don't declare final until it lands" discipline while still delivering and proving the bulk of the codec (the non-float, non-edge term corpus) immediately.
- **Alternatives rejected**: *Declare full byte-parity-final now* — rejected: violates FR-009 (no frozen ISA yet) and asserts unverified float behavior on AtomVM.

---

## Resolved unknowns summary

| NEEDS CLARIFICATION | Resolution |
|---|---|
| Scope vs Section-15 bytecode portion | R1 — term portion only |
| Wire conventions / endianness | R2 — 029 layout, little-endian |
| Envelope framing / payloadType | R3 — header `0x11`, length-prefixed sections |
| Deep-resolve depth / truncation | R5 — depth-32 + explicit truncation marker |
| Cyclic terms | R6 — defer to runtime deref; D5 open |
| var→writer identity | R7 — GlobalVarId `agentId:localId` |
| 029 as proof | R8 — C# oracle only (FR-007) |
| Cross-runtime parity mechanism | R9 — golden corpus from Dart |
| Gleam buildability | R10 — on 034, no F5 |
| Float / 64-bit / final | R11 — gated, candidate-then-final |
