---
path: lib/runtime/cells.dart
cycle_group_id: 35
scc_siblings: []
generated_at: 2026-05-21T14:42:19Z
source_sha256: a796313daaa1098e3edc1234f0216057a77df01aa65130265d29856d6eaed866
schema_version: 1
---

# Conversion Plan: lib/runtime/cells.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/runtime/cells.dart` (29 lines,
sha256 `a796313d…`):

- **Module-level doc comment** (line 1): `/// Minimal cell tags (extend later
  as needed).` — signals the entire hierarchy (enum + Cell + subtypes) is
  deliberately open / extensible.
- **`enum CellTag { writer, reader }`** (line 2) — plain Dart enum, no
  methods, no backing constructor, no fields. Two members in declaration
  order (writer == 0, reader == 1).
- **`abstract class Cell { CellTag get tag; }`** (lines 4-7) — abstract
  marker class with one abstract getter. No state, no constructor, no
  implementation. Used only via `implements Cell` from the two concrete
  subtypes. NOT declared `sealed` (Dart 3 class modifier absent).
- **`class WriterCell implements Cell`** (lines 9-19):
  - `@override final CellTag tag = CellTag.writer;` — `final` instance
    field whose implicit getter satisfies `Cell.tag`.
  - `final int writerId;` and `final int readerId;` — opaque WriterId /
    ReaderId pair (per inline `// WriterId` / `// ReaderId (pair)`
    comments).
  - `bool abandoned = false;` — the SOLE non-`final` field on the entire
    hierarchy; mutable writer-abandonment flag with field initialiser.
  - `WriterCell(this.writerId, this.readerId);` — positional `this.x`
    constructor in declaration order (writerId first, readerId second).
- **`class ReaderCell implements Cell`** (lines 22-28):
  - `@override final CellTag tag = CellTag.reader;` — symmetric to
    WriterCell.
  - `final int readerId;` — single opaque ReaderId; no writer pair on this
    side.
  - `ReaderCell(this.readerId);` — single-parameter positional constructor.

Caller graph (from tombstone): `lib/bytecode/runner.dart` and
`test/bytecode/utility_instructions_test.dart` consume Cell / WriterCell /
ReaderCell. Cells are paired across the heap by object identity (the
WriterId/ReaderId integers are book-keeping, not the equality key).

Imports: none. Stateful surface: the single `WriterCell.abandoned` mutable
flag. No async, no Stream / Future, no isolate primitives, no equality
override, no `toString`, no factory ctor, no static members, no generics.

## 2. Dart → C#/.NET Conversion Plan

The convspec (`.codeconv/conversion-specs/lib/runtime/cells.dart.md`) is
RATIFIED and authoritative. This section mirrors its six per-construct
decisions verbatim; no deviations.

| # | Dart construct | C#/.NET target | Source |
|---|---|---|---|
| 1 | `enum CellTag { writer, reader }` | Plain C# `enum CellTag { writer, reader }`. Declaration order preserved so `writer == 0`, `reader == 1`. Lowercase member spellings preserved verbatim (NOT PascalCased) for byte-identical reflective / string-keyed lookup. Default underlying type `int`. No `[Flags]`. | convspec construct `dart.enum.plain_two_member_marker_tag`; rf-dart-plain-enum-to-csharp-enum. |
| 2 | `abstract class Cell { CellTag get tag; }` | `interface ICell { CellTag Tag { get; } }` — a non-sealed marker contract with one get-only property. NOT a C# `abstract class` (Dart has no fields/ctor/impl; using an interface keeps the single base-class slot free). NOT `sealed` (Dart source is not sealed; the file comment marks the hierarchy as open). | convspec construct `dart.abstract_class.marker_base_with_abstract_getter_non_sealed`; rf-dart-abstract-marker-to-csharp-interface. |
| 3 | `@override final CellTag tag = CellTag.writer;` (WriterCell) and `@override final CellTag tag = CellTag.reader;` (ReaderCell) | C# get-only auto-property with initialiser: `public CellTag Tag { get; } = CellTag.writer;` (WriterCell) / `... = CellTag.reader;` (ReaderCell). NO `override` keyword (interface satisfaction in C# does not use `override`). Property is non-virtual. | convspec construct `dart.override_getter_final_field_initialised_to_enum_literal`; rf-dart-override-final-field-to-csharp-getonly-autoprop-init. |
| 4 | `class WriterCell implements Cell { final int writerId; final int readerId; bool abandoned = false; WriterCell(this.writerId, this.readerId); }` | C# reference `class WriterCell : ICell` with `public long WriterId { get; }`, `public long ReaderId { get; }`, mutable `public bool Abandoned { get; set; } = false;`, single positional ctor `WriterCell(long writerId, long readerId)` binding both id fields in declaration order. REJECTED: `record` (structural equality would coalesce distinct heap cells), `struct` / `record struct` (pass-by-value would split the `Abandoned` flag and break pair-by-identity). | convspec construct `dart.data_class.final_int_pair_ids_positional_ctor_reference_identity`; rf-dart-final-field-class-to-csharp-getonly-class. |
| 4b | `class ReaderCell implements Cell { final int readerId; ReaderCell(this.readerId); }` | C# reference `class ReaderCell : ICell` with `public long ReaderId { get; }` and single positional ctor `ReaderCell(long readerId)`. Same record/struct rejection as WriterCell — cells are paired by object identity in the heap/unification model. | Same as #4 above. |
| 5 | `final int writerId;` / `final int readerId;` (and the `int` parameter slot in `bool abandoned = false;`'s siblings — every `int` in this file) | C# `long` (System.Int64), NOT `int` / Int32. Dart native `int` is 64-bit signed; faithful width is `long`. No down-mapping to `int` without a per-field justification (none here — ids are opaque). No arithmetic / bitwise / shift in this file, so checked/unchecked context and overflow are not exercised and not asserted. | convspec construct `dart.int.fixed_width_identity_field`; rf-dart-int-to-csharp-long-width. |
| 6 | `bool abandoned = false;` (mutable, non-`final`, with initialiser) | `public bool Abandoned { get; set; } = false;` — mutable auto-property with initialiser. NO `volatile`, NO `Interlocked`, NO `lock` — the faithful 1:1 mapping preserves Dart's "non-racy by single-threaded isolate" assumption. C# `bool` is a value type (never null). Reference-class decision on #4 ensures the flip propagates across all aliases. | convspec construct `dart.mutable_bool_field_default_false_on_reference_class`; rf-dart-mutable-bool-field-to-csharp-bool-autoprop. |

Auxiliary mapping decisions (mechanical, mirrored from convspec):

- The `@override` annotation on the `tag` fields is subsumed by C#
  interface-property satisfaction (no keyword needed; `override` is for
  `virtual`/`abstract` base members, not interface members).
- File doc comment `/// Minimal cell tags (extend later as needed).` maps
  to a C# XML doc comment `/// <summary>Minimal cell tags (extend later
  as needed).</summary>` on the file's top-level type (or as a leading
  file-level `//` comment); inline `// WriterId` / `// ReaderId (pair)`
  comments map to `//` line comments on the corresponding properties.
- No `using` directives required from this file alone (`CellTag`, `long`,
  `bool` are all in scope without imports); the target unit
  `lib/runtime/cells.cs` belongs to the project's runtime namespace
  (namespace determined by surrounding project policy, not this file).

Target code unit (per convspec `target_code_unit` and per tombstone
`target_path`): `lib/runtime/cells.cs`. Single source-file → single
target-file mapping (one `enum`, one `interface`, two `class`es — same
file is idiomatic in .NET for a small tightly-coupled type cluster).

## 3. Decomposed Task Units

- **T1**: Emit `enum CellTag { writer, reader }` with member casing preserved.
- **T2**: Emit `interface ICell { CellTag Tag { get; } }` as non-sealed marker.
- **T3**: Emit `class WriterCell : ICell` with `WriterId`/`ReaderId` get-only `long` properties, mutable `Abandoned` `bool` auto-property defaulting to `false`, and positional ctor `WriterCell(long writerId, long readerId)` binding both ids.
- **T4**: Emit `class ReaderCell : ICell` with `ReaderId` get-only `long` property and positional ctor `ReaderCell(long readerId)`.
- **T5**: Emit `Tag` get-only auto-property initialised to `CellTag.writer` on WriterCell and `CellTag.reader` on ReaderCell.
- **T6**: Carry over file-level doc comment and inline `// WriterId` / `// ReaderId (pair)` comments to the C# target verbatim.
- **T7**: Verify zero `volatile` / `Interlocked` / `lock` / `record` / `struct` keywords appear on the cell types (faithfulness guard).

## 4. Research Findings

none required — every construct is resolved by the ratified convspec
(`.codeconv/conversion-specs/lib/runtime/cells.dart.md`) which cites
authoritative dart.dev and learn.microsoft.com sources for all six
research findings (rf-dart-plain-enum-to-csharp-enum,
rf-dart-abstract-marker-to-csharp-interface,
rf-dart-override-final-field-to-csharp-getonly-autoprop-init,
rf-dart-final-field-class-to-csharp-getonly-class,
rf-dart-int-to-csharp-long-width,
rf-dart-mutable-bool-field-to-csharp-bool-autoprop). No web research is
performed by this plan (FORBIDDEN per orchestration contract).

## 5. Consistency Pass

Cross-check of §2 / §3 against convspec + tombstone + source:

- Construct coverage: all six convspec constructs are mirrored in §2
  (rows 1, 2, 3, 4+4b, 5, 6). No convspec construct is dropped; no §2 row
  introduces a construct outside the convspec. Derived from
  `.codeconv/conversion-specs/lib/runtime/cells.dart.md` constructs[0..5].
- Target path: §2 target unit `lib/runtime/cells.cs` matches convspec
  `target_code_unit: lib/runtime/cells.cs` and tombstone
  `target_path: lib/runtime/cells.cs`. Derived from convspec + tombstone.
- sha256: this plan's `source_sha256` matches convspec `source_sha256`
  (`a796313d…`) and matches the freshly computed hash of the inspected
  Dart file. Derived from `python -c ... hashlib.sha256(...)` invocation.
- Reference-identity invariant: §2 #4/#4b mirror convspec's rejection of
  `record` / `struct` / `record struct`; T7 in §3 enforces the guard.
  Derived from convspec rf-dart-final-field-class-to-csharp-getonly-class.
- Mutable-flag invariant: §2 #6 mirrors convspec's rejection of
  `volatile` / `Interlocked` / `lock`; T7 in §3 enforces the guard.
  Derived from convspec rf-dart-mutable-bool-field-to-csharp-bool-autoprop
  and CLAUDE.md "Robustness is often a workaround in disguise" (do not
  manufacture guarantees the source does not provide).
- Enum-casing fidelity: §2 #1 preserves lowercase member names (not
  PascalCased); §2 #3 references `CellTag.writer` / `CellTag.reader`
  consistently. Derived from convspec rf-dart-plain-enum-to-csharp-enum
  ("StyleCop PascalCase is NOT applied here for the same string-fidelity
  rationale used in opcodes/token specs").
- Integer width: §2 #5 maps every Dart `int` to C# `long`; §2 #4 / #4b
  use `long` for the WriterId / ReaderId ctor params and properties.
  Derived from convspec rf-dart-int-to-csharp-long-width.
- Open hierarchy: §2 #2 mirrors convspec's NON-sealed interface
  decision; no exhaustiveness / closed-hierarchy semantics introduced.
  Derived from convspec rf-dart-abstract-marker-to-csharp-interface and
  the source file comment ("Minimal cell tags (extend later as needed)").
- Singleton (no scc_siblings): §7 deliberately omitted per orchestration
  contract for cycle_group_id 35 with empty scc_siblings. Derived from
  agent prompt ("Singleton — NO §7").
- Escalations: convspec records zero escalations on the cell-type mapping
  itself (the threading-model concern is explicitly NOT an escalation on
  THIS file). §6 of this plan mirrors that by emitting `None.`. Derived
  from convspec `escalations: []` and convspec Escalations section.

No gap detected. No row in §2 or task in §3 requires escalation; all
decisions are derived verbatim from the ratified convspec.

## 6. Escalations

None.
