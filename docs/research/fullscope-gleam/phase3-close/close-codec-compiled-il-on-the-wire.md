<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T064 close-codec-compiled-il-on-the-wire` (b3-c2-036)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Closes**: `verify-codec-compiled-il-on-the-wire` (b3-c1-011)
**Gated by**: `rule-codec-compiled-il-on-the-wire` (b3-c2-024) — must land before wave 4
**Backing detail_ids**: `term-codec-tlv`, `result-envelope-codec`, `result-envelope-builder`, `il-codec`, `il-codec-round-trip`, `compiled-il-on-the-wire`

## What was verified DELIVERED (byte-parity)

The delivered Gleam codec seam — `glp/codec/term_codec.gleam` (Section-15 TLV: LEB128 varints,
8-byte LE int64, IEEE-754 double LE, varint+UTF-8 strings, tags `0x02..0x06` + `0x07`
unbound-VarRef, loud-fail on malformed), `glp/codec/result_envelope.gleam` (the `0x01`/`0x11`
envelope), and `glp/codec/result_envelope_builder.gleam` (depth-bounded deep-resolve with
truncation + circular markers) — is **byte-parity** to the Dart/029 source of truth. Parity is
proven, not assumed: `glp_gleam/test/glp/codec/golden_corpus_test.gleam` reads
`specs/038-result-codec-and-framecodec-ride/contracts/golden/corpus.hex` **live** (via
`file:read_file/1`, not inlined bytes) and asserts `encode(corpus[name]) == golden_bytes[name]`
per non-gated entry plus a name-set drift guard — a stale or edited golden fails the suite rather
than false-passing.

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Golden byte-parity (live `corpus.hex`) | `wsl -e bash -lc 'cd glp_gleam && gleam clean && gleam test'` | **465+ passed, 0 failures** — includes `golden_corpus_test.gleam` matching every non-gated shape byte-for-byte |
| No Gleam IL codec module | `rg -n 'il_codec' glp_gleam/src` | **no matches** |
| Codec surface sweep | `rg -n 'il_codec\|envelope\|tlv' glp_gleam/src` | the three delivered codec modules + envelope consumers; **no `il_codec`** |
| Bytecode dir carries ISA model only | `ls glp_gleam/src/glp/bytecode/` | `opcodes.gleam`, `program.gleam`, `guard_defs.gleam` — **no `BytecodeProgram → bytes` serializer** |

Gated term-codec entries (`0x03` float, int64 edges — excluded from `corpus.hex` as round-trip-only)
were separately confirmed byte-identical on **real AtomVM 0.7.0-alpha.1** this wave
(`guard-atomvm-gated-probe`), extending the byte-parity evidence to the deliberately-excluded shapes.

## Disposition (per detail_id)

| detail_id | disposition |
|---|---|
| `term-codec-tlv` | **CONFIRMED-DELIVERED (byte-parity)** — live `corpus.hex` rerun + AtomVM gated edges |
| `result-envelope-codec` | **CONFIRMED-DELIVERED** — `result_envelope_codec_test` + golden T028 cross-decode green |
| `result-envelope-builder` | **CONFIRMED-DELIVERED** — `deref_fidelity_test`, `cyclic_term_test`, `suspended_acceptance_test` green |
| `il-codec` | **ABSENT BY DESIGN / owner-deferred** — no `il_codec` module; the engine assembles goal-terms over **pre-compiled** bytecode and emits no IL at runtime (`b1-c1-040`). No reproducer owed. |
| `il-codec-round-trip` | **ABSENT BY DESIGN / owner-deferred** — no IL codec ⇒ no round-trip; same recorded deferral |
| `compiled-il-on-the-wire` | **ABSENT — owner-deferred, NOT a gap** — the MVP puts source text on the wire; the compiler factored into a front-end (compiled IL on the wire) is a deliberately-deferred follow-up with no spec dir (`b1-c1-042`), reconciled owner-side in 026 US2 (`b1-c1-040`) |

## Gate held for the IL-codec family

The close acceptance line optimistically anticipated "a `BytecodeProgram` round-trips
Gleam-encode to C#-decode, **or the ruled alternative**." Per the verify verdict, the IL-codec
family is **not a gap** — it is confirmed absent by construction, matching the `b1-c1-040` /
`b1-c1-042` deferral records. This close therefore takes the **ruled-alternative** branch: it does
not fabricate an IL codec. The out-of-scope disposition is **gated on
`rule-codec-compiled-il-on-the-wire` (b3-c2-024)**, which files the proposal to mark
`compiled-il-on-the-wire` out-of-scope as a post-feature follow-on. That ruling MUST land before
wave 4, because `build-fe-be-process-split` assumes source-text-on-the-wire; a ruling that instead
*rejects* the deferral would change that build's wire contract.

**Close status: CLOSED** for the three delivered codec detail_ids (byte-parity, in-suite,
golden-backed). The three IL-codec detail_ids are recorded **ABSENT-by-design / owner-deferred**
and held pending the b3-c2-024 ruling — not silently waived, not fabricated.
