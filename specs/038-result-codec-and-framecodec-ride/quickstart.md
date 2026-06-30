# Quickstart: Result-Envelope Codec

**Feature**: `038-result-codec-and-framecodec-ride` · **Plan**: [plan.md](./plan.md)

How to build, test, and verify the cross-runtime byte-parity of the result-envelope codec. Contract: [contracts/result-envelope-codec.md](./contracts/result-envelope-codec.md).

## Prerequisites

- Dart SDK (Windows: `C:\Users\gavri\dart-sdk\bin\dart.exe`).
- .NET SDK (for the C# `csharp/glp_result_codec/` project).
- WSL Ubuntu + Gleam ≥1.17 / OTP 25 / AtomVM 0.6.6 at `/opt/atomvm/AtomVM-static` (the F1 toolchain; AtomVM run only needed for the float gate / AtomVM-faithfulness checks).

## Build + test per runtime

```bash
# Dart (source of truth) — authors the golden corpus
C:/Users/gavri/dart-sdk/bin/dart.exe test glp_runtime/test/codec/

# C# (reference)
dotnet test csharp/glp_result_codec/

# Gleam (port; from WSL, in glp_gleam/)
wsl -e bash -lc 'cd /mnt/d/bstdev/research/glp/glpnet/glp_gleam && gleam test'
```

## The golden corpus + byte-parity harness

The shared corpus lives at `specs/038-result-codec-and-framecodec-ride/contracts/golden/` as `{logical-result → expected hex bytes}`. The Dart encoder authors it; C# and Gleam reproduce it.

```bash
# 1. (re)generate the golden from the Dart reference encoder
C:/Users/gavri/dart-sdk/bin/dart.exe run glp_runtime/tool/gen_result_golden.dart > specs/038-.../contracts/golden/corpus.hex

# 2. each runtime asserts byte-identity to the golden (captured field masked)
#    - Dart  golden_corpus_test.dart
#    - C#    GoldenByteIdentityTests
#    - Gleam result_envelope_codec_test.gleam
```

Pass criterion (SC-002): all three encoders reproduce `corpus.hex` byte-for-byte on the **non-gated** corpus.

## What to verify (maps to spec success criteria)

- **SC-001** round-trip: `decode(encode(R)) == R` field-by-field — `result_envelope_codec_test.*`.
- **SC-002** byte-parity: Dart == C# == Gleam == golden (non-gated) — `golden_corpus_test.*`.
- **SC-003** no heap address: reconstruct every field with no heap handle; assert no field references a heap address.
- **SC-004** loud-fail: fuzz trailing/garbage bytes + unknown tags + bad version/payloadType; assert 0 silent acceptances.
- **V5 oracle cross-check** (C# only): the result codec's term bytes (`0x00–0x06`) equal 029 `ConstantCodec` bytes for shared inputs.

## Gated cases — quarantined, NOT asserted byte-final

Keep these in a separate, clearly-labelled corpus section; do not include them in the SC-002 byte-final assertion until their gate clears:

- **Floats** (`0x03`) — gated on the ED-6 AtomVM 0.6.6 `/float` bit-syntax decode spike (FR-011). Pairs with the m2-0 AtomVM work (AtomVM now provisioned at `/opt/atomvm/`). Verify decode on AtomVM-static before declaring float parity.
- **64-bit-int edges** — Gleam `Int` is BEAM bignum; plain-`gleam test` green is NOT an AtomVM-faithfulness signal. Run the edge corpus on AtomVM-static.
- **Cyclic / cross-goal cyclic terms** — gated on owner decision D5/FORK-1 (FR-008); codec defers to runtime deref (depth-bounded), never loops.

## AtomVM float-gate check (when running the ED-6 spike)

```bash
# compile a /float decode probe and run on AtomVM 0.6.6 (mirrors the m2-0 run pattern)
wsl -e bash -lc '/opt/atomvm/AtomVM-static <probe>.beam /opt/atomvm/atomvmlib-v0.6.6.avm'
```

## Definition of done (this feature)

- Envelope value type + codec in Dart, C#, Gleam; round-trip (SC-001), no-heap-address (SC-003), loud-fail (SC-004) green in all three.
- Non-gated golden corpus byte-identical across all three (SC-002).
- Gated cases quarantined + documented; byte-parity-**final** explicitly deferred to the D4 ISA freeze + ED-6 (FR-009/FR-010).
- #36 FrameCodec payload-type-prefix handoff fact recorded (FR-006); 029 cited as C# oracle only (FR-007).
