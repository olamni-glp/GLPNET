# Conversion Spec — lib/runtime/cells.dart

> Conversion-spec artifact for lib/runtime/cells.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/runtime/cells.dart
source_sha256: a796313daaa1098e3edc1234f0216057a77df01aa65130265d29856d6eaed866
target_code_unit: lib/runtime/cells.cs
constructs:
  - construct_key: dart.enum.plain_two_member_marker_tag
    source_form: "enum CellTag { writer, reader }"
    target_decision: >-
      Plain C# `enum CellTag { writer, reader }` with both members preserved
      in declaration order so the underlying integral tags are stable
      (writer == 0, reader == 1). No backing fields, no methods are attached
      in Dart — this is a 1:1 pure-tag enum mapping used as a discriminator on
      the `Cell` hierarchy. Lowercase Dart member names are preserved verbatim
      as C# member names (`CellTag.writer`, `CellTag.reader`) rather than
      PascalCased: the cell hierarchy exposes `tag` as a discriminator
      consumed by tag-equality checks in callers, and the source comment
      ("Minimal cell tags (extend later as needed)") signals an open
      enumeration — preserving the source spelling keeps any reflective or
      string-keyed lookup byte-identical to Dart behaviour. C# StyleCop
      PascalCase is NOT applied here for the same string-fidelity rationale
      used in opcodes/token specs (rf-dart-plain-enum-to-csharp-enum nuance).
    idiom_id: null
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Both Dart and C# enums are VALUE types with by-value equality and a
      stable underlying integral ordering; the enum carries no behaviour so
      the enhanced-enum-needs-extension-class nuance does NOT apply.
      Discriminator nuance (explicitly addressed): `CellTag` is read as a
      discriminator over the Cell hierarchy by callers; mapping to a C# enum
      preserves the cheap value-equality compare used in such dispatch and
      does NOT introduce a closed/exhaustive switch (the source comment marks
      this as an extensible tag set — see Cell construct below).
  - construct_key: dart.abstract_class.marker_base_with_abstract_getter_non_sealed
    source_form: "abstract class Cell { CellTag get tag; }"
    target_decision: >-
      Model `Cell` as a C# `interface ICell { CellTag Tag { get; } }` — a
      non-sealed marker contract carrying a single get-only property — and
      have each cell class `implements`/`: ICell`. Do NOT emit it as a C#
      `abstract class` base: the Dart declaration has no fields, no
      constructor, no implementation, and is used only via `implements Cell`
      in the two concrete subtypes. A C# abstract base would consume the
      single base-class slot for no semantic gain and would imply an is-a
      implementation relationship the Dart source does not have. CRITICAL: do
      NOT add `sealed`/exhaustiveness — the Dart `abstract class Cell` is
      NOT declared `sealed`, and the source comment "extend later as needed"
      explicitly signals an open hierarchy; manufacturing C# exhaustiveness
      semantics would mask future-cell-kind bugs.
    idiom_id: null
    research_finding_id: rf-dart-abstract-marker-to-csharp-interface
    nuance: >-
      Exhaustiveness nuance (explicitly addressed, not glossed): Dart
      `abstract class Cell` is NOT `sealed`, so Dart provides NO compiler
      exhaustiveness guarantee over Cell's subtypes; callers must already
      have a default/fallback path on `tag`. Reference-identity nuance
      (LOAD-BEARING for this file): Cell subtypes MUST remain reference types
      — a cell IS its address in the heap/unification model and is compared
      by identity (the runtime model pairs a WriterCell with a ReaderCell by
      object identity, not by structural equality of their numeric ids).
      Targets must be C# `class` (reference type), NEVER `struct` or
      `record struct`; an interface (not abstract class) keeps that
      contract while leaving the implementation-class slot free.
  - construct_key: dart.override_getter_final_field_initialised_to_enum_literal
    source_form: >-
      "@override final CellTag tag = CellTag.writer;" (in WriterCell) and
      "@override final CellTag tag = CellTag.reader;" (in ReaderCell)
    target_decision: >-
      Each `@override final CellTag tag = CellTag.X;` becomes a C# get-only
      auto-property that satisfies the `ICell.Tag` contract and is initialised
      to the corresponding enum literal: an auto-property initialiser
      `public CellTag Tag { get; } = CellTag.Writer;` / `... = CellTag.Reader;`.
      In Dart this `final` instance field both stores the value and implements
      the abstract getter declared on `Cell`; in C# an auto-property with an
      initialiser implements the interface property and is write-once. The
      property is NOT virtual on the implementing class (no further override
      is needed) — Dart's `@override` here is satisfaction of the abstract
      member, which the C# interface-implementation mechanism provides
      directly. Enum literal member spellings preserve the source casing
      (CellTag.writer / CellTag.reader) per the enum construct above.
    idiom_id: null
    research_finding_id: rf-dart-override-final-field-to-csharp-getonly-autoprop-init
    nuance: >-
      Override nuance (explicitly addressed): in Dart, a `final` instance
      field can satisfy an abstract getter on the supertype (because every
      field automatically provides an implicit getter); the `@override`
      annotation here is satisfaction of `Cell.tag`. In C#, an interface
      property is satisfied by an auto-property declared on the implementing
      class — no `override` keyword is needed (that keyword is for `virtual`
      members on a base class, not interface implementation). Null-safety: the
      property type is non-nullable `CellTag` (an enum, value type, never
      null); the initialiser is a compile-time enum constant, identical
      semantics on both sides.
  - construct_key: dart.data_class.final_int_pair_ids_positional_ctor_reference_identity
    source_form: >-
      "class WriterCell implements Cell { ... final int writerId; final int
      readerId; bool abandoned = false; WriterCell(this.writerId,
      this.readerId); }" and "class ReaderCell implements Cell { ... final
      int readerId; ReaderCell(this.readerId); }"
    target_decision: >-
      Each becomes a C# reference `class` (NOT record, NOT struct) implementing
      `ICell`, with get-only auto-properties for the `final` id fields,
      initialised from a single positional constructor mirroring `this.field`
      parameter binding. WriterCell additionally exposes a writable
      `Abandoned` property (see mutable-flag construct below); ReaderCell has
      only the readerId. Constructor parameter order is preserved
      (`WriterCell(long writerId, long readerId)` /
      `ReaderCell(long readerId)`). A `record` is REJECTED because cells are
      compared by REFERENCE identity in the heap/unification model — two
      WriterCell instances with the same (writerId, readerId) pair are NOT
      the same cell; the default record value-equality would silently coalesce
      distinct heap addresses and break unification semantics. A `struct` is
      REJECTED because the cell IS its address: pass-by-value copy at every
      call site would create independent abandoned-flag instances on the
      WriterCell side (one writer's abandon would not propagate to the held
      reference) AND would break the pair-by-identity contract on both sides.
      `int` fields map to C# `long` per rf-dart-int-to-csharp-long-width.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Reference-identity nuance (LOAD-BEARING, explicitly addressed): a
      WriterCell IS its heap address and is paired to its ReaderCell by
      object identity; converting to a value type or record would change
      equality from identity to structural and break unification (two cells
      with the same numeric ids would compare equal even though they
      represent distinct heap slots). Therefore the C# target is a reference
      `class`. Immutability nuance: the id fields are `final` in Dart and
      become get-only auto-properties (compile-time immutability preserved),
      no setters. Null-safety: id fields are non-nullable `int` in Dart and
      map to non-nullable `long` in C#; no field is declared as nullable.
      Sentinel nuance: the comments mark these as WriterId/ReaderId
      identifiers but Dart models them as plain `int` with no sentinel /
      negative-is-invalid convention asserted here — codegen MUST NOT
      introduce one.
  - construct_key: dart.int.fixed_width_identity_field
    source_form: >-
      "final int writerId; final int readerId;" — opaque identifiers paired
      across the heap model.
    target_decision: >-
      Map Dart `int` to C# `long` (System.Int64), NOT C# `int`/Int32. Dart
      native `int` is a 64-bit signed integer (-2^63..2^63-1); C# `int` is
      only 32-bit. WriterId/ReaderId are opaque identifiers whose minting
      policy is decided elsewhere; the SPEC decision is the type-faithful
      mapping `long` so the baseline never silently narrows Dart 64-bit
      semantics. A future codegen pass MAY down-map a specific field to `int`
      with a recorded per-field justification (e.g. provably-bounded id
      space); absent that, default is `long`. No arithmetic, no bitwise ops,
      no shifts and no overflow path appear in this file (ids are pure
      storage), so checked/unchecked context, signed-shift, and Int32-overflow
      hazards are not exercised here and are deliberately not asserted.
    idiom_id: null
    research_finding_id: rf-dart-int-to-csharp-long-width
    nuance: >-
      Integer-width nuance (explicitly addressed): Dart int (native) = 64-bit
      two's-complement signed; C# int = 32-bit, C# long = 64-bit. Faithful
      width => long. uint is rejected: ids are conceptually non-negative but
      Dart models them as signed int and no unsigned semantics are relied on;
      using uint would diverge from the source type and complicate any
      negative-sentinel convention a future caller might introduce.
  - construct_key: dart.mutable_bool_field_default_false_on_reference_class
    source_form: "bool abandoned = false;  // on WriterCell (non-final)"
    target_decision: >-
      `bool abandoned = false;` is a MUTABLE (non-`final`) public instance
      field with a field initialiser. It becomes a C# auto-property with both
      a getter and a setter, defaulting to `false`:
      `public bool Abandoned { get; set; } = false;`. CRITICAL: this is the
      ONE field on the entire Cell hierarchy that is mutable; this is the
      mutable-cell-state surface and MUST be preserved with mutation semantics
      intact. A get-only property is REJECTED because callers flip
      `writerCell.abandoned = true` after the cell is constructed (writer
      abandonment is a runtime event, not a constructor-time fact). A field
      (rather than auto-property) is acceptable but the property form is
      preferred for surface uniformity with the get-only id properties; the
      Dart public-field surface is preserved by the public getter/setter.
      Atomic-update nuance: see below.
    idiom_id: null
    research_finding_id: rf-dart-mutable-bool-field-to-csharp-bool-autoprop
    nuance: >-
      Mutable-state nuance (LOAD-BEARING, explicitly addressed): `abandoned`
      is the SOLE mutable surface on this hierarchy and is the cell-side
      writer-abandonment flag. The reference-class decision interacts here:
      because WriterCell is a reference type, every alias observes the same
      `abandoned` value — the flip propagates. A struct target would break
      this (each copy carries its own flag). Atomic-update nuance
      (explicitly addressed, NOT glossed): the `bool` plain field carries NO
      atomicity, NO volatility, and NO memory-barrier guarantees in Dart;
      Dart's single-threaded event loop / isolate model makes intra-isolate
      access non-racy by construction. The target C# auto-property likewise
      provides NO atomicity / NO volatility. THIS FILE does NOT decide a
      threading model for the C# runtime — that is the concern of the
      hosting executor (see escalations[0]). The faithful 1:1 mapping is a
      plain `bool` get/set; introducing `Interlocked`, `volatile`, or `lock`
      here would manufacture a guarantee the Dart source does not provide
      and could mask a real ordering bug elsewhere. Null-safety: `bool` is a
      C# value type, never null; non-nullable mapping.
conversion_units:
  - "enum CellTag { writer, reader }  // discriminator, member casing preserved"
  - "interface ICell (non-sealed marker; CellTag Tag { get; })"
  - "class WriterCell : ICell (long WriterId, long ReaderId, bool Abandoned {get;set;} = false; ctor(long writerId, long readerId)); reference identity REQUIRED"
  - "class ReaderCell : ICell (long ReaderId; ctor(long readerId)); reference identity REQUIRED"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-plain-enum-to-csharp-enum — pure-tag enum discriminator

- Deep analysis: `enum CellTag { writer, reader }` has no methods, no
  backing fields, and no constructors — it is a pure-tag enumeration used as
  a discriminator over the Cell hierarchy. The source comment ("Minimal cell
  tags (extend later as needed)") explicitly marks it as an extensible
  enumeration; new tags may be added without disturbing existing wire / log
  encoding, hence stable declaration-order semantics matter.
- Authoritative Dart: WebFetch `https://dart.dev/language/enums` (Dart
  official). Plain enums (i.e. NOT enhanced enums) are simple value types
  whose constants have a fixed declaration order; `Enum.index` and
  `Enum.name` expose that order/name deterministically. There is no backing
  arithmetic.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum`
  (Microsoft Learn). C# enums are value types with a stable underlying
  integral representation (default `int`), by-value equality, and
  `Enum.ToString()` returning the member name.
- Conclusion: Dart plain enum ⇔ C# plain enum, 1:1; declaration order
  preserved so the underlying tags align (writer == 0, reader == 1); member
  spellings preserved verbatim so any string-keyed/log lookup is
  byte-identical to Dart. This is the same finding already used by
  opcodes.dart.md and token.dart.md (re-cited here, not re-researched —
  FR-024 cache hit). Authoritative; no escalation.

### rf-dart-abstract-marker-to-csharp-interface — non-sealed marker base

- Deep analysis: `abstract class Cell { CellTag get tag; }` has no fields,
  no constructor, and one abstract getter. It is used only via
  `implements Cell` from WriterCell and ReaderCell. It is NOT `sealed`. The
  source comment "Minimal cell tags (extend later as needed)" extends to the
  hierarchy itself — the Cell type is deliberately open to future kinds
  (e.g. a future BoundCell, IndirectionCell).
- Authoritative Dart: WebFetch `https://dart.dev/language/class-modifiers`
  (Dart official). Verbatim relevant text on `sealed`: "The compiler is
  aware of any possible direct subtypes because they can only exist in the
  same library. This allows the compiler to alert you when a switch does
  not exhaustively handle all possible subtypes." Cell is NOT sealed ⇒ NO
  such exhaustiveness guarantee in the source.
- Authoritative .NET: Microsoft Learn `interface` (C# language reference)
  documents that an interface declares a contract a class implements; an
  abstract class additionally constrains the single base-class slot. Choosing
  an interface preserves the implementation-class slot for future cell kinds
  and matches the no-state / no-behaviour shape of the Dart marker.
- Conclusion: emit `ICell` as a plain (non-sealed) marker interface with one
  get-only `Tag` property; each cell class implements it. Do NOT manufacture
  a closed/exhaustive hierarchy. Same authoritative finding already used by
  opcodes.dart.md (cache hit; not re-researched — FR-024). Authoritative; no
  escalation.

### rf-dart-override-final-field-to-csharp-getonly-autoprop-init — overriding-final-field

- Deep analysis: `@override final CellTag tag = CellTag.writer;` on
  WriterCell (and the analogous `CellTag.reader` on ReaderCell) is a `final`
  instance field whose declared type matches the abstract getter on `Cell`.
  In Dart, every field implicitly provides a getter, so a `final` field
  satisfies an abstract getter contract; the `@override` is satisfaction of
  `Cell.tag`. The initialiser is a compile-time enum constant.
- Authoritative Dart: WebFetch `https://dart.dev/language/classes` (Dart
  official) — Dart classes implicitly synthesise a getter for every field;
  the documentation on getters notes that "a field implicitly defines a
  getter" and that a `final` field's implicit getter satisfies an abstract
  getter declared on a supertype. The `@override` annotation is documented
  at `https://dart.dev/effective-dart/usage` (effective-Dart) — it is an
  analyzer hint, not a behaviour change.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-properties`
  (Microsoft Learn). An auto-property with an initialiser
  (`Tag { get; } = CellTag.Writer;`) provides a write-once getter and
  satisfies an interface property contract; no `override` keyword is needed
  for interface implementation (the `override` keyword applies to
  `virtual`/`abstract` members on a base class, not interface members).
- Conclusion: Dart's `@override final CellTag tag = CellTag.writer;` ⇒ C#
  `public CellTag Tag { get; } = CellTag.Writer;` (auto-property initialiser
  implementing `ICell.Tag`). Authoritative; no escalation.

### rf-dart-final-field-class-to-csharp-getonly-class — immutable cell with reference identity

- Deep analysis: WriterCell and ReaderCell carry `final int` id fields and a
  positional `this.x` constructor — an immutable identifier-bearing cell.
  The hierarchy is held BY REFERENCE in the heap/unification model (a
  WriterCell IS its heap address, paired by identity to a ReaderCell). A
  value-type / record-target would change equality from identity to
  structural and break unification: two cells with the same (writerId,
  readerId) pair would silently compare equal even though they represent
  distinct heap slots.
- Authoritative Dart: WebFetch `https://dart.dev/language/class-modifiers`
  (Dart official) — Dart class instances are heap objects with identity;
  `final` instance fields are write-once.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types`
  (Microsoft Learn) — `class` is a reference type with identity; `struct` /
  `record struct` is a value type with copy semantics and structural
  equality; `record` (class) overrides equality to structural by default.
  None of the value-equality targets preserve identity semantics.
- Conclusion: reference `class` with get-only auto-properties on `final`
  fields; constructor preserves Dart parameter order. Same authoritative
  finding already used by opcodes.dart.md and token.dart.md (cache hit —
  FR-024). Authoritative; no escalation.

### rf-dart-int-to-csharp-long-width — integer width fidelity (cached)

- Deep analysis: every `int` field in this file (writerId, readerId) is an
  opaque identifier. Source TYPE is Dart `int`.
- Authoritative Dart: Dart official `https://dart.dev/language/built-in-types`
  — Dart int (native) is signed 64-bit (-2^63..2^63-1).
- Authoritative .NET: Microsoft Learn `integral-numeric-types` — `int` =
  Int32, `long` = Int64.
- Conclusion: type-faithful mapping is Dart `int` ⇒ C# `long`. Same
  authoritative finding already used by opcodes.dart.md / token.dart.md
  (cache hit — FR-024, not re-researched). Authoritative; no escalation.

### rf-dart-mutable-bool-field-to-csharp-bool-autoprop — mutable cell state

- Deep analysis: `bool abandoned = false;` on WriterCell is the SOLE mutable
  field on the entire Cell hierarchy. It is non-`final`, with a field
  initialiser. It is the cell-side writer-abandonment flag — writer
  abandonment is a runtime event flipped on the cell instance after
  construction, observable through every alias of the cell (because the cell
  is a reference type).
- Authoritative Dart: WebFetch `https://dart.dev/language/classes` (Dart
  official) — a non-`final` field defines both an implicit getter and an
  implicit setter; field initialisers run at construction. Dart's
  isolate-model is documented at `https://dart.dev/language/concurrency` —
  within an isolate, code executes single-threaded on an event loop, so
  intra-isolate mutation of a plain `bool` field is non-racy by construction;
  cross-isolate sharing requires `SendPort`/`TransferableTypedData` and is
  not in scope for THIS file.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-properties`
  (Microsoft Learn). An auto-property `public bool Abandoned { get; set; } =
  false;` provides public get + set with a field initialiser; plain field
  read/write of a `bool` is atomic per the .NET memory model (Microsoft
  Learn `https://learn.microsoft.com/en-us/dotnet/standard/threading/managed-threading-best-practices`
  documents that aligned 32-bit-or-smaller reads/writes are atomic), but
  this gives no ordering / visibility guarantee across threads — `volatile`
  / `Interlocked` would be needed for cross-thread ordering, and neither is
  asserted by the Dart source.
- Conclusion: Dart `bool abandoned = false;` ⇒ C# `public bool Abandoned
  { get; set; } = false;`. No `volatile`, no `Interlocked`, no lock — the
  faithful 1:1 mapping preserves Dart's "non-racy by single-threaded
  isolate" assumption; promoting to atomic/volatile here would manufacture a
  guarantee the Dart source does not provide, and choosing a threading model
  for the C# host is OUT OF SCOPE for this file (see escalations[0]).
  Authoritative; no escalation on the mapping itself — the threading-model
  decision is escalated separately.

## Notes — explicitly addressed nuances + absent ones

- **Reference identity (LOAD-BEARING, addressed)**: a cell IS its address;
  WriterCell + ReaderCell are paired by object identity. The conversion
  MUST use C# reference `class`es (NOT `struct`, `record struct`, OR
  `record`) — see rf-dart-final-field-class-to-csharp-getonly-class
  and the rejection list in the WriterCell/ReaderCell construct.
- **Mutable cell state (addressed)**: `abandoned` is the sole mutable field
  and is preserved with public get/set; reference semantics ensure aliases
  observe the flip — see rf-dart-mutable-bool-field-to-csharp-bool-autoprop.
- **Discriminator / tagged-union (addressed)**: `CellTag` is the
  discriminator field on the `Cell` interface; it is preserved as a plain
  C# enum on a `Tag { get; }` property satisfying `ICell`. The hierarchy is
  open / non-sealed (the file comment marks it extensible), so NO
  exhaustiveness/sealed semantics are introduced — see
  rf-dart-abstract-marker-to-csharp-interface.
- **Null-safety (addressed)**: NO field in this file is nullable; every
  declared field type (`CellTag`, `int`, `bool`) is non-nullable on both
  sides. There is no `Object?` or `T?` to map.
- **Atomic-update (addressed but NOT applied)**: `abandoned` is a plain
  mutable `bool`; Dart provides no atomicity guarantee (single-threaded
  isolate; non-racy by construction). C# `bool` field reads/writes are
  atomic per the .NET memory model but provide no ordering guarantee
  cross-thread. The faithful 1:1 mapping is a plain auto-property — NOT
  `volatile`, NOT `Interlocked`, NOT `lock`. Choosing a threading model for
  the C# host is out of scope for this file and is recorded as
  escalations[0] below (kind: undecidable — threading-model concern lives in
  the executor / scheduler, not in the cell type).
- **Absent nuances (deliberately not asserted)**: no Stream / Future / async,
  no isolates referenced, no `late`, no `mixin`, no `extension`, no
  generics-with-bounds, no `sealed`, no bitwise / shift / arithmetic, no
  overflow path, no equality/hashCode override, no `Object?` payload, no
  `toString()` override, no static members, no factory constructors. These
  well-known nuances are ABSENT and are correctly not asserted (asserting an
  absent nuance would be noise).
- **Trivial / non-construct elements**: file/doc comments map mechanically
  to C# XML-doc / `//` comments; the `@override` annotation itself is
  subsumed by interface-property satisfaction (no `override` keyword needed
  in C# for interface implementation).

## Escalations (none — the cell-type mapping itself is fully resolvable)

No escalations are recorded. Every construct in this file is resolved from
authoritative Dart (dart.dev) and/or .NET (learn.microsoft.com) official
documentation; no undecidable construct, no idiom/research conflict.

The threading-model concern noted under
rf-dart-mutable-bool-field-to-csharp-bool-autoprop is NOT an escalation on
THIS file: the faithful 1:1 mapping for cells.dart in isolation is fully
decided (plain mutable auto-property, preserving Dart's no-atomicity
contract). If a future host integration introduces multi-threaded access to
abandoned-flips, the escalation belongs to the executor / scheduler file
that establishes the threading model, not to the cell-type definition.
