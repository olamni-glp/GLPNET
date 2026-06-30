# Feature Specification: Result-Envelope Codec (rides the Section-15 bytecode/term codec)

**Feature Branch**: `038-result-codec-and-framecodec-ride`
**Created**: 2026-06-30
**Status**: Draft
**Input**: Promoted full-gleam M1 feature (roadmap `result-codec-and-framecodec-ride`, #15). REALIGN + SPLIT per `docs/research/glp-gleam-baseline/pipelines/P1b-realignment/DISPOSITIONS.md` row #15: (a) result-envelope codec rides ED-6 obl#1 Section-15 codec → full-gleam (THIS feature); (b) FrameCodec framing → link-layer #36; (c) C# TcpTransport → superseded by BEAM `gen_tcp` (#36).

## Overview

The combined Gleam/AtomVM GLP instance must hand a query **result** to a consumer as a **self-contained, heap-independent value** — readable with no access to the producer's live heap, and **byte-for-byte identical** whether the consumer is in-process or across a wire (the result side of the ratified ED-1 seam). This feature delivers that result envelope **and its binary codec**, where the codec **rides** the Section-15 bytecode/term codec (ED-6 obligation #1). Framing and transport are explicitly NOT in this feature (they move to link-layer #36).

The "users" here are runtime-internal: the Gleam GLP REPL/engine that produces and consumes results, and the cross-runtime conformance harness that proves the codec is a faithful shared contract across Dart, C#, and Gleam.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Heap-independent result envelope across the seam (Priority: P1)

A combined Gleam GLP instance finishes a goal and returns the result as a self-contained envelope — Status, resolved bindings, variable→writer references, suspended set, captured output, error — carrying **no live heap addresses**. A consumer reads the full result with zero access to the producer's heap, and gets the identical result whether it read it in-process or decoded it from bytes.

**Why this priority**: This is the result side of the ED-1 seam — the minimum that makes the front/back separation real and is reused unchanged over the wire (M2). Without it there is no faithful, transportable result.

**Independent Test**: Produce results for the M1 result corpus, deep-resolve to an envelope, assert no field references a heap address, and assert a consumer with no heap handle reconstructs every field.

**Acceptance Scenarios**:

1. **Given** a successful goal with bound terms, **When** the result is emitted as an envelope, **Then** all bindings are present as deep-resolved (heap-independent) values and no live heap address appears anywhere in the envelope.
2. **Given** the same envelope, **When** a consumer reads it in-process vs. decodes it from bytes, **Then** the two reconstructed results are equal field-by-field.
3. **Given** a suspended goal, **When** emitted, **Then** the envelope reports Status=suspended and the suspended/blocking-reader set, with no heap address leaking.

### User Story 2 - Cross-runtime byte-parity of the codec (Priority: P2)

The cross-runtime conformance harness encodes the **same logical result** on Dart, C#, and Gleam and obtains **byte-identical** output, proving the codec is a single shared contract (FB-M2-06), not three lookalikes.

**Why this priority**: Byte-parity is the contract that lets a C# instance and a Gleam instance interoperate (M2) and lets Dart/C# serve as the reference oracle. It is the codec's correctness criterion.

**Independent Test**: For the shared result corpus, run each runtime's encoder and diff the byte streams; require 100% identical bytes.

**Acceptance Scenarios**:

1. **Given** a result in the shared corpus, **When** encoded by Dart, C#, and Gleam, **Then** the three byte sequences are identical.
2. **Given** bytes produced by one runtime, **When** decoded by another, **Then** the reconstructed envelope equals the original.

### User Story 3 - Deref + variable→writer fidelity in the envelope (Priority: P3)

A result containing deeply-nested bound terms and unbound variable→writer references is encoded and decoded preserving GLP deref semantics (depth-bounded resolution; writer identity), matching the Dart/C# reference behavior (FB-M1-17, FB-M1-41/42).

**Why this priority**: The envelope's fidelity to GLP semantics (not just byte round-trip) is what makes M1 parity meaningful for non-trivial results.

**Independent Test**: Encode/decode the deref + var→writer corpus and assert structural + identity equality against the recorded Dart outcomes.

**Acceptance Scenarios**:

1. **Given** a nested bound term, **When** encoded then decoded, **Then** deref resolution matches the reference up to the bound depth.
2. **Given** an unbound variable paired to a writer, **When** encoded then decoded, **Then** the variable→writer reference (by global identity) is preserved.

### Edge Cases

- **Deeply nested / depth-bounded terms** (e.g. depth-32 bound) — resolution must match the reference, not over- or under-resolve.
- **Circular / cross-goal cyclic terms** — governed by the FORK-1 / owner decision **D5** (cycle discriminator); until ruled, the codec MUST behave consistently with the runtime's deref (defer/error, not silently loop).
- **Floats** — `/float` bit-syntax decode on AtomVM 0.6.6 is UNVERIFIED (ED-6); float-bearing results are gated (see FR-008).
- **64-bit integer edges** — Gleam `Int` is bignum on BEAM, masking 64-bit overflow parity; plain-BEAM green is NOT an AtomVM-faithfulness signal for these edges.
- **Captured output** — present in the envelope but EXCLUDED from the parity/byte-identity criterion (PB:9).
- **Malformed bytes** — trailing/garbage bytes or an unknown tag must fail loudly, never silently truncate or accept.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST represent a query result as a self-contained envelope `{Status, ResolvedBindings, var→writer, suspended, captured, Error}` that contains **no live heap addresses** (server-side deep-resolve before emission).
- **FR-002**: The system MUST encode the envelope to, and decode it from, a byte sequence via the Section-15 bytecode/term codec (ED-6 obl#1), with `decode(encode(R)) == R` for every in-scope result shape.
- **FR-003**: The byte encoding MUST be identical across Dart, C#, and Gleam producers for the same logical result (cross-runtime byte-parity, FB-M2-06).
- **FR-004**: Decoding MUST preserve GLP deref semantics for bound terms (depth-bounded resolution) and variable→writer references by global identity (FB-M1-17, FB-M1-41/42).
- **FR-005**: The codec MUST fail loudly on trailing/garbage bytes or an unknown tag (no silent truncation or acceptance).
- **FR-006**: FrameCodec framing and transport are OUT of scope for this feature (they move to link-layer #36); this feature delivers only the result-envelope value and its byte codec. The correct framing fact for #36 is that `FrameCodec.cs:64 OffKind` is **fragmentation**, not a payload-type discriminator → a payload-type prefix byte is needed there (recorded here for handoff, not built here).
- **FR-007**: The codec MUST NOT cite the shipped 029 C# `IlCodec` as proof of correctness for the Dart/Gleam path; 029 is the C# reference **oracle** only.
- **FR-008**: Circular/cross-goal cyclic terms in a result MUST be handled consistently with the runtime's deref per owner decision **D5** (FORK-1 cycle discriminator); the codec MUST NOT define its own divergent cycle behavior.
- **FR-009**: The codec's byte layout for the bytecode/term portion MUST be finalized only against a **frozen, versioned ISA** — see FR-010 (D4). Until then the codec is implementable against a *candidate* layout but MUST NOT be declared byte-parity-final.
- **FR-010**: System MUST resolve the bytecode ISA freeze before byte-parity is declared final [NEEDS CLARIFICATION: **owner gate D4** — ISA freeze + v1/v2 (IOp/IOpV2) opcode-split resolution. The Section-15 term/bytecode codec the envelope rides cannot be frozen for byte-parity until the v1/v2 ISA split is unified/versioned; ISA-freeze and Section-15 authoring are mutually blocking. Decision and sequencing are the owner's.].
- **FR-011**: System MUST ground float encoding/decoding on AtomVM before float-bearing byte-parity is declared [NEEDS CLARIFICATION: **ED-6 float-decode on AtomVM UNVERIFIED** — the `/float` bit-syntax extraction on AtomVM 0.6.6 is not grounded; a spike MUST confirm it before the byte-parity codec is committed for float-bearing results, else the byte-parity decision is invalidated].

### Key Entities

- **Result Envelope**: the heap-independent result value — Status (success | suspended | failed), ResolvedBindings (deep-resolved terms), variable→writer map (global writer identity), suspended/blocking-reader set, captured output (excluded from parity), Error.
- **Section-15 codec**: the binary term/bytecode codec (ED-6 obl#1) the envelope rides; shared contract across Dart/C#/Gleam.
- **Reference oracle**: the shipped 029 C# `IlCodec` (byte-parity reference for C# only; not proof for Dart/Gleam).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `decode(encode(R)) == R` for 100% of the in-scope result corpus (all FB-M1-17/41/42 shapes), excluding the D4/ED-6-gated float and ISA-final cases.
- **SC-002**: For the shared result corpus, Dart, C#, and Gleam produce byte-identical encodings (FB-M2-06) — 100% agreement on the non-gated corpus.
- **SC-003**: 0 result encodings carry a live heap address (verified by a consumer reconstructing every field with no heap access).
- **SC-004**: 100% of malformed inputs (trailing bytes / unknown tag, fuzzed) are rejected loudly; 0 silent acceptances.

## Assumptions

- The C#(025)/Dart reference runtimes and the combined Gleam instance exist; this feature adds the result-envelope codec layer riding the Section-15 codec.
- Float and 64-bit-int edge encodings are gated on ED-6 (FR-011) and the bignum-masking risk respectively; plain-BEAM green is not an AtomVM-faithfulness signal for those.
- FrameCodec framing and transport live in link-layer #36; the circular-term policy lives in FORK-1 / owner decision D5.
- "Result corpus" = the recorded Dart result outcomes for FB-M1-17/41/42 plus the shared cross-runtime corpus for FB-M2-06.

## Dependencies

- **Owner gate D4** (ISA freeze + v1/v2 opcode split) — BLOCKS final byte-parity declaration (FR-010).
- **ED-6 obl#1** Section-15 codec authored + **ED-6 float-decode AtomVM spike** (FR-011).
- **#4 il-codec-spike** (029, shipped, `v2026.06.11.1`) = the C# byte-parity reference oracle (FR-007).
- **D5 / FORK-1** circular-term discriminator (FR-008).
