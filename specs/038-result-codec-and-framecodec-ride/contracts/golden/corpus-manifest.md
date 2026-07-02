# Golden Corpus Manifest — Result-Envelope Codec (feature 038)

`corpus.hex` pins the **byte-parity subset** (SC-002): each line is `<name> <hex>`,
the bytes Dart (source of truth, R9) produces for that logical result. C# and Gleam
MUST reproduce every line byte-for-byte (the cross-runtime gate
`encode(decode(line)) == line` in each runtime).

Authored by `glp_runtime/tool/gen_result_golden.dart` from `goldenCorpus`
(`glp_runtime/test/codec/corpus.dart`) — the non-gated entries with **empty
`captured`** (R4 excludes `captured` from the parity criterion, so the golden needs
no masking).

## In-scope (non-gated) shapes — in `corpus.hex`

| name | shape (FB-M1) |
|---|---|
| `empty_success` | success, no bindings |
| `success_atom` | bound atom (tag 0x05) — FB-M1-41 |
| `success_int` / `success_int_negative` | bound int64 (tag 0x02), incl. two's-complement |
| `success_string` | bound string (tag 0x04) |
| `success_nested_struct` | `point(1,2)` — FB-M1-17 deref |
| `success_list` | `[1,2,3]` cons/nil — FB-M1-17 |
| `success_deep_nested` | nested `node(node(...),node(...))` |
| `multi_binding` | 3 bindings, canonical order (parity) |
| `var_to_writer` | unbound `VarRef`→`GlobalVarId` (tag 0x07) — FB-M1-14 |
| `suspended` / `suspended_with_binding` | suspended set of `GlobalVarId` — FB-M1-42 |
| `failed` | failed + error string |

(`with_captured` is in the round-trip corpus but EXCLUDED from `corpus.hex` —
captured output is not part of the byte-parity criterion, R4.)

## Quarantined (GATED) shapes — NOT in `corpus.hex`, NOT byte-parity-final

| shape | gate |
|---|---|
| **float** (tag 0x03, `ConstReal`) | ED-6 — AtomVM 0.6.6 `/float` bit-syntax decode spike (FR-011) |
| **64-bit-int edges** | Gleam `Int` BEAM bignum masking — plain `gleam test` is NOT an AtomVM-faithfulness signal |
| **cyclic / cross-goal cyclic terms** | owner decision D5 / FORK-1 (FR-008) — codec defers to runtime deref, never loops |

These run round-trip-only in each runtime; they are quarantined from the SC-002
byte-final assertion until their gate clears (R11/R6). Whole-Section-15
byte-parity-**final** additionally waits on the D4 v2-ISA freeze (FR-009/FR-010).
