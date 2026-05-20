# Conversion Spec — lib/runtime/heap_fcp.dart

> Conversion-spec artifact for lib/runtime/heap_fcp.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> Load-bearing core of the FCP runtime: the two-cell heap with pointer
> architecture (heap-pointer-architecture-spec.md v3.0/v3.2). Reference
> identity is non-negotiable here — every cell IS its address; the heap
> is a `List<HeapCell>` indexed by integer address, paired writer/reader
> cells point at each other by address, and unification mutates these
> cells in place. Suspensions live on writer cells via a shared-mutable
> compound (`WriterContent`) that preserves the reader back-pointer.
> Every cell-like type below is REJECTED as `record`/`struct`/`record
> class`/`record struct` and emitted as a plain reference `class`,
> inheriting the same rationale used in `lib/runtime/suspension.dart.md`
> and `lib/runtime/cells.dart.md`.

```yaml
schema_version: 1
source_path: lib/runtime/heap_fcp.dart
source_sha256: 18b5962454f8a7e7d8d1b48c9d711bfe92b3699180dcc4d9ac7a3288a26378f3
target_code_unit: lib/runtime/heap_fcp.cs
constructs:
  - construct_key: dart.library_directive.top_of_file_no_name
    source_form: "Top-of-file `library;` directive (no library name) following the leading doc-comments describing the FCP two-cell heap with pointer architecture (per heap-pointer-architecture-spec.md v3.0)."
    target_decision: >-
      No direct .NET counterpart — .NET's compilation-unit / namespace
      model has no `library` concept. The library doc-comments (reader
      cells point TO writer cells; writer cells contain null / Pointer
      / SuspensionListNode; suspensions live on writer cells; ValueTag
      indicates bound-to-ground) become a file-header XML doc / comment
      on the namespace declaration (mirroring `lib/runtime/`). The
      `library;` directive itself is elided.
    idiom_id: null
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: "Compilation-unit nuance only; no value/reference, null-safety, or async surface implicated. Carry-forward of the same finding used in suspension.dart.md / variable_table.dart.md (FR-024 cache hit; do not re-research)."

  - construct_key: dart.import_directive.package_internal_to_using_namespace
    source_form: >-
      Four `import` directives: `import 'package:glp_runtime/runtime/terms.dart';`,
      `import 'package:glp_runtime/runtime/suspension.dart';`,
      `import 'package:glp_runtime/runtime/machine_state.dart';`, and
      `import 'package:glp_runtime/multiagent/variable_table.dart' show VariableEntry;`.
      The first three pull in full library surface (Term hierarchy / SuspensionRecord+SuspensionListNode / GoalRef+GoalState+Pc+GoalId); the fourth uses `show VariableEntry` to narrow surface to one symbol.
    target_decision: >-
      Each Dart package-internal import becomes a .NET `using` directive
      naming the namespace of the corresponding converted file:
      `using <root>.Runtime;` (covers `Term` / `VarRef` / `ConstTerm` /
      `StructTerm` / `MutualRefTerm` / `ModuleTerm` from terms.cs;
      `SuspensionRecord` / `SuspensionListNode` from suspension.cs; and
      `GoalRef` / `GoalState` / `Pc` / `GoalId` from machine_state.cs —
      all three sibling files target the same `lib/runtime/`
      namespace), plus `using <root>.Multiagent;` (for
      `VariableEntry`). The Dart `show VariableEntry` allow-list has NO
      .NET counterpart — .NET `using` imports the full public surface
      of a namespace; per-symbol narrowing is unnecessary because this
      file references `VariableEntry` by simple name. Codegen MUST NOT
      synthesise a `using VariableEntry = ...Multiagent.VariableEntry;`
      alias — that would be over-translation.
    idiom_id: null
    research_finding_id: rf-dart-import-relative-to-csharp-using-namespace
    nuance: "Compilation-unit nuance: Dart resolves package imports by URI; .NET resolves type references by assembly + namespace. The `show` allow-list has no parallel — see goal_queue.dart.md rf-dart-export-directive-to-csharp-using-alias for the symmetric `export` case. No value-vs-reference, null-safety, or async surface implicated by import directives themselves."

  - construct_key: dart.enum.three_member_marker_tag_acronymed_members
    source_form: "`enum CellTag { WrtTag, RoTag, ValueTag }` — three-member tag-only enum, used as the discriminator on every `HeapCell.tag`. Member names are SHOUTcase-acronymed (`WrtTag` = writer, `RoTag` = read-only/reader, `ValueTag` = bound-to-ground). Ordinal positions are never observed by code; equality compare only."
    target_decision: >-
      A C# enum `CellTag` with three members in declaration order so
      the underlying integral tags are stable (WrtTag == 0, RoTag == 1,
      ValueTag == 2). Default underlying type `int`; no explicit member
      values needed. CRITICAL: member spellings (`WrtTag`, `RoTag`,
      `ValueTag`) are PRESERVED VERBATIM — NOT PascalCased to
      `Writer`/`Reader`/`Value` and NOT renamed per the .NET enum
      naming guideline. Justification: (1) the names are spec-named
      (heap-pointer-architecture-spec.md v3.0 references the exact
      strings `WrtTag` / `RoTag` / `ValueTag`); (2) callers compare on
      these literal tag identities and any rename would silently fork
      the source-to-spec correspondence used by trace logs / debugger
      / inspection of bytecode; (3) this matches the rationale already
      applied in cells.dart.md (lowercase `writer`/`reader` preserved
      for the same string-fidelity reason). The enum hierarchy is NOT
      `[Flags]` — these are mutually exclusive discriminators, not
      combinable bits.
    idiom_id: null
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Discriminator nuance (explicitly addressed): `CellTag` is the
      load-bearing discriminator on `HeapCell.tag` consumed by every
      type-test (`isWriter` / `isReader` / `isValue`) and every
      `switch (cell.tag)` in `derefAddr`. C# enum value-equality
      `==` matches Dart enum `==` exactly. Casing nuance (LOAD-BEARING,
      explicitly addressed): SHOUTcase-acronymed `WrtTag`/`RoTag`/`ValueTag`
      preserved verbatim per the spec-string-fidelity precedent in
      cells.dart.md and opcodes.dart.md — codegen MUST NOT PascalCase.
      Open-vs-closed nuance: this enum has no documented intent to
      grow at runtime; HOWEVER, source has no `sealed`/exhaustive-switch
      semantics either, and the `default` arm in any future consumer
      MUST throw `StateError`-equivalent (`InvalidOperationException`)
      to match Dart's `StateError('Reader cell at ... has invalid content')`
      pattern used in `derefAddr`. Null-safety: enum is a value type,
      never null; non-nullable on both sides.

  - construct_key: dart.heap_cell_class.dynamic_content_mutable_tag_reference_identity
    source_form: >-
      `class HeapCell { dynamic content; CellTag tag; HeapCell(this.content, this.tag); bool get hasValue => tag == CellTag.ValueTag; bool get hasSuspensions => content is WriterContent && (content as WriterContent).suspensions != null; }` — the ONE mutable container that holds every heap slot. `content` is typed as `dynamic` (Dart's wildcard) and at runtime is exactly one of: `null` | `Pointer` | `SuspensionListNode` | `Term` (any leaf — `ConstTerm`/`StructTerm`/`VarRef`/`MutualRefTerm`/`ModuleTerm`) | `VariableEntry` | `WriterContent`. Both `content` and `tag` are MUTATED IN PLACE — `bindWriter` sets `cell.content = value; cell.tag = CellTag.ValueTag;` — so neither field is `final`. No `==`/`hashCode` override (default identity equality).
    target_decision: >-
      A reference-type .NET `class HeapCell` in the namespace mirroring
      `lib/runtime/`. Two mutable public properties: `public object?
      Content { get; set; }` (typed as nullable `object?` — the closest
      faithful counterpart of Dart `dynamic` for an in-place sum-type
      slot — see nuance below) and `public CellTag Tag { get; set; }`
      (mutable enum). A single non-optional-parameter constructor
      `HeapCell(object? content, CellTag tag)` assigning both. Two
      expression-bodied get-only properties: `bool HasValue => Tag ==
      CellTag.ValueTag;` and `bool HasSuspensions => Content is
      WriterContent wc && wc.Suspensions != null;` (uses C# pattern-
      match to bind `wc` in the same expression — semantically
      identical to Dart's `content is WriterContent && (content as
      WriterContent).suspensions != null`). NOT a `record class` —
      synthesised value-equality on `Content`/`Tag` would silently make
      two distinct heap slots with the same content compare equal,
      catastrophically breaking unification (the runtime's invariant
      that two heap addresses are distinct iff the cells at those
      addresses are distinct objects). NOT a `struct`/`record struct`
      — every cell is referenced by integer address from the heap
      `cells` list AND from other cells' `Pointer` content; if
      `HeapCell` were a value type, `cells[addr]` would return a copy
      and `cell.content = value` would mutate the copy not the
      canonical slot (catastrophic — every binding would be lost).
      Reference identity is THE load-bearing semantic of this type.
    idiom_id: null
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      VALUE-VS-REFERENCE is the load-bearing nuance (LOAD-BEARING for
      this file, explicitly addressed). `HeapCell` IS the heap slot at
      its address; the entire runtime depends on `cells[addr]` returning
      the same object that other cells' `Pointer`s point at, and on
      `cell.content = X` propagating to every observer of that slot. C#
      `class` (reference type) is the only correct mapping; `record` /
      `record class` (value equality) / `struct` / `record struct`
      (value copy) are ALL categorically rejected — same authoritative
      rationale as `SuspensionRecord` in suspension.dart.md (FR-012
      cache hit — same idiom; not re-researched). DYNAMIC-VS-OBJECT
      nuance (explicitly addressed, NOT glossed): Dart `dynamic` defers
      all type-checking to runtime; `object?` under enabled NRT
      requires casts/pattern-matches to extract concrete types, which
      MIRRORS the type-tests this file already performs (`content is
      Pointer`, `content is WriterContent`, `content is VariableEntry`,
      `content is Term`). Mapping `dynamic` -> `object?` is the
      faithful translation — NOT C# `dynamic` (which uses the DLR,
      pays a per-access dispatch cost, and silently allows operations
      that compile but throw at runtime — out of character for a hot-
      path runtime kernel). The codegen MUST use `is` / `as` /
      pattern-match on `object?` everywhere this file does
      `content is X` / `content as X`. NULL-SAFETY: `content` is
      nullable (`null` is the initial state of an unbound imported
      reader/writer per `allocateImportedReader`/`allocateImportedWriter`);
      `object?` (Nullable Reference Type) preserves this exactly.
      `tag` is non-nullable (`CellTag` is a value type). MUTABLE-FIELD-
      VS-AUTO-PROPERTY: both `content` and `tag` are public mutable
      Dart fields — `bindWriter` mutates both in place — so the .NET
      counterpart uses `{ get; set; }` (public setter), NOT `{ get;
      private set; }` (would force every mutator in this file to be
      moved INTO the class, refactor the source shape). Async / Stream
      / Sealed / Mixin: ABSENT — correctly not asserted.

  - construct_key: dart.pointer_class.single_final_int_address_reference_identity_tostring_only
    source_form: "`class Pointer { final int targetAddr; Pointer(this.targetAddr); @override String toString() => 'Ptr($targetAddr)'; }` — a one-field reference wrapper around a heap address. `final` field; positional constructor. NO `==`/`hashCode` override — two `Pointer(7)` instances are NOT `==` in Dart (default reference identity). Heavily allocated (every cell allocation creates at least one `Pointer`; every reader-to-writer link is a `Pointer`)."
    target_decision: >-
      A reference-type .NET `sealed class Pointer` (NOT a record /
      record class / record struct / struct). One get-only auto-
      property `public int TargetAddr { get; }`; single non-optional-
      parameter constructor `Pointer(int targetAddr)`; override
      `public override string ToString() => $"Ptr({TargetAddr})";`.
      Equality is DELIBERATELY NOT overridden — the Dart source carries
      no `==` override, so two `Pointer(7)` are NOT equal in Dart
      (reference identity). The C# class keeps default `object.Equals`
      reference identity. Explicitly REJECTED: `record class Pointer(
      int TargetAddr)` (would synthesise value-equality, silently
      changing semantics — see `ConstTerm` in terms.dart.md for the
      symmetric rejection). Explicitly REJECTED: `readonly record
      struct Pointer(int TargetAddr)` — TEMPTING because the type has
      one immutable `int` field and is allocated heavily in hot paths
      (similar to `GoalRef` in machine_state.dart.md which IS a
      `readonly record struct`), but REJECTED here because: (1) `Pointer`
      instances are stored as `HeapCell.content` (typed `object?` in
      the target) — boxing a `record struct` into `object?` on every
      assignment AND unboxing it on every `content is Pointer` test
      would defeat the value-type allocation benefit AND would cause
      `content` references to lose pointer-equality semantics across
      box/unbox cycles; (2) the source semantic is reference identity
      (no `==` override) — two pointers to the same address are NOT
      `==` in Dart, and a `record struct` would silently inject value
      equality. The `GoalRef`-as-`readonly record struct` decision in
      machine_state.dart.md is justified by `GoalRef`'s value-equality
      `==` override AND by its being stored in a `Queue<GoalRef>`
      (which holds value-type elements unboxed); `Pointer` has neither
      property, so the decision is opposite. `sealed` is applied
      because no subclass exists in Dart (final via class shape) and
      no documented intent to extend.
    idiom_id: null
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      Value-vs-reference nuance (LOAD-BEARING for this construct,
      explicitly addressed): `Pointer` is stored in `HeapCell.content`
      (object? sum slot); reference identity preserved across all
      reads (`content is Pointer && (content as Pointer).targetAddr ==
      X` is a common check). A `record struct` would BOX into the
      `object?` slot — defeating any allocation win AND introducing
      unboxing copies on every type-test. Reference identity is the
      only safe mapping. The `Const(null)` analogue is the
      reference-identity decision on `ConstTerm` in terms.dart.md
      (FR-012 cache hit on rf-dart-sumleaf-no-eq-to-csharp-class-no-
      record — same idiom). Heap-allocation cost nuance: `Pointer` is
      allocated O(N) times per heap (N = cells.length); .NET small-
      object allocation is well-tuned (per Microsoft Learn GC docs)
      and a per-`Pointer` Gen0 allocation is comparable to Dart's
      per-`Pointer` heap allocation — no semantic difference. NULL-
      SAFETY: `targetAddr` is non-nullable `int` (Dart) -> non-nullable
      `int` (C#). Sealed: no subclass in Dart (a plain `class`); `sealed`
      in C# is a defensive narrowing that costs nothing and forbids
      future accidental subclassing — applied. ToString interpolation:
      Dart `'Ptr($targetAddr)'` -> C# `$"Ptr({TargetAddr})"`, with no
      null-handling concern (targetAddr is non-nullable). Async /
      Stream / Mixin: ABSENT.

  - construct_key: dart.writer_content_class.final_reader_addr_mutable_nullable_suspension_head_compound_payload
    source_form: >-
      `class WriterContent { final int readerAddr; SuspensionListNode? suspensions; WriterContent(this.readerAddr, [this.suspensions]); @override String toString() => 'WriterContent(reader=$readerAddr, sus=$suspensions)'; }` — a compound payload that lives inside `HeapCell.content` for unbound writer cells that have accumulated suspensions. Per spec v3.2 §2.3: when suspensions are added to an unbound writer, the reader pointer must be preserved (so `readerForWriter` keeps working). `readerAddr` is `final` (set once at construction; the paired-reader address never changes for this writer). `suspensions` is MUTABLE and NULLABLE (the head of the per-writer suspension chain; updated in place when suspensions are added via `suspendOnWriter` or removed/walked via `_walkAndActivate`/`_forwardSuspensions`). Positional constructor with an OPTIONAL POSITIONAL parameter `[this.suspensions]` (Dart `[]` brackets, defaults to `null` if omitted). NO `==`/`hashCode` override.
    target_decision: >-
      A reference-type .NET `sealed class WriterContent` in the
      namespace mirroring `lib/runtime/`. Members: get-only auto-
      property `public int ReaderAddr { get; }` (Dart `final` -> .NET
      get-only); mutable nullable property `public SuspensionListNode?
      Suspensions { get; set; }` (Dart mutable nullable -> .NET
      mutable nullable with PUBLIC setter — this file mutates
      `wc.suspensions = node;` in `suspendOnWriter` and
      `_forwardSuspensions`, so a private setter would force a
      refactor of the surrounding code). Single constructor with one
      default-valued parameter: `WriterContent(int readerAddr,
      SuspensionListNode? suspensions = null)` — Dart's optional
      positional `[this.suspensions]` maps to a C# default-valued
      positional parameter with default `null` (Microsoft Learn named-
      and-optional arguments: default-valued positional parameters
      are optional at the call site). `ToString()` override:
      `$"WriterContent(reader={ReaderAddr}, sus={Suspensions?.ToString() ?? "null"})"`
      — the explicit `?.ToString() ?? "null"` preserves Dart's
      `'$suspensions'` rendering of `null` as the literal string
      "null" (carry-forward of the documented Dart-vs-.NET
      interpolation difference from suspension.dart.md). NOT a
      `record class` — value-equality on `(ReaderAddr, Suspensions)`
      would compare two distinct writer-content objects equal if they
      coincidentally share a reader address AND a (reference-equal)
      suspension head; the runtime relies on each `WriterContent`
      being IDENTIFIED with its containing `HeapCell` (one
      `WriterContent` per unbound writer cell), and mutating
      `wc.Suspensions` MUST propagate via the shared reference held in
      `HeapCell.content`. NOT a `struct` / `record struct` — copy
      semantics would split the shared suspension-head mutation across
      independent struct copies, catastrophically breaking suspension
      management (a suspension added through one alias would be
      invisible to another alias — silent and undetectable). `sealed`
      because no Dart subclass.
    idiom_id: null
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      VALUE-VS-REFERENCE (LOAD-BEARING, explicitly addressed): this
      construct inherits the shared-mutable-state-by-reference rationale
      from `SuspensionRecord` / `SuspensionListNode` /
      `VariableEntry` (rf-dart-shared-mutable-record-by-reference-to-
      csharp-class — FR-012 cache hit). `WriterContent` is the
      compound payload that holds (i) the preserved reader back-
      pointer (`ReaderAddr`, never re-assigned) and (ii) the head of
      the suspension chain (`Suspensions`, mutated in place); the
      surrounding heap relies on the SAME `WriterContent` instance
      being observed through every `cell.content`/`wc` reference, so
      that `_forwardSuspensions` adding a new head node propagates to
      every observer. Reference identity is non-negotiable. OPTIONAL-
      POSITIONAL-PARAMETER nuance (explicitly addressed): Dart
      `[this.suspensions]` is OPTIONAL POSITIONAL (callable as either
      `WriterContent(7)` or `WriterContent(7, node)`), defaulting to
      `null`. C# has NO optional-positional-with-square-brackets
      syntax; the canonical mapping is a default-valued positional
      parameter `SuspensionListNode? suspensions = null` — Microsoft
      Learn (named-and-optional arguments) confirms this is the
      faithful counterpart; call sites `new WriterContent(7)` and
      `new WriterContent(7, node)` both work. NULL-SAFETY: `readerAddr`
      is non-nullable `int` (Dart) -> non-nullable `int` (C#);
      `suspensions` is nullable reference (Dart) -> nullable reference
      (C# NRT) — the same nullable-reference pattern as
      `SuspensionListNode? Next` in suspension.dart.md. ToString null-
      interpolation: Dart `'$suspensions'` renders `null` as the
      literal "null"; .NET `$"{Suspensions}"` renders `null` as
      empty string — preserved via the explicit
      `Suspensions?.ToString() ?? "null"` form (carry-forward from
      suspension.dart.md). Async / Stream / Mixin / Sealed-base:
      ABSENT.

  - construct_key: dart.heap_class.master_runtime_state_list_of_cells_mutable_hp_callback_map
    source_form: >-
      `class HeapFCP { final List<HeapCell> cells = []; int HP = 0; final Map<int, void Function(Term)> _bindCallbacks = {}; ... }` — the master heap runtime state. Three direct fields plus all the methods. `cells` is `final` (the list REFERENCE never re-targets) but the LIST CONTENTS are mutated (`cells.add(...)` on every allocation; `cells[addr].content = ...` on every binding). `HP` is mutable (the next-free heap pointer; incremented by 2 in `allocateVariable`, by 1 in `allocateImportedReader`/`allocateImportedWriter`/`storeTermOnHeap` for non-VarRef terms). `_bindCallbacks` is `final` (reference fixed) but contents mutated via `_bindCallbacks[writerAddr] = callback;` and `_bindCallbacks.remove(writerAddr);`. UPPER-case identifier `HP` (matches WAM/heap-pointer-architecture-spec.md naming convention). Field naming: `_bindCallbacks` has a leading underscore (Dart library-private convention). NO `==`/`hashCode` override.
    target_decision: >-
      A reference-type .NET `class HeapFCP` in the namespace mirroring
      `lib/runtime/`. Three direct members: (a) `public List<HeapCell>
      Cells { get; } = new();` — the list reference is fixed (Dart
      `final` -> get-only auto-property with `= new()` initialiser),
      but the LIST CONTENTS are mutated via `Cells.Add(...)` (matching
      Dart `cells.add(...)`); MUST be the concrete `System.Collections
      .Generic.List<T>` (NOT `IList<T>` — we need indexer + `Add` +
      `Count`; NOT `IReadOnlyList<T>` — we mutate); (b) `public int Hp
      { get; set; } = 0;` — UPPER-case `HP` is RENAMED to PascalCase
      `Hp` per the .NET naming guideline (Microsoft Learn capitalisation
      conventions: two-letter acronyms PascalCase the first letter only
      — but `HP` here is the documented WAM/spec name, see nuance);
      mutable property (incremented in every allocator); default 0; (c)
      `private readonly Dictionary<int, Action<Term>> _bindCallbacks =
      new();` — Dart leading-underscore `_bindCallbacks` -> .NET
      `private` field with the underscore prefix RETAINED (matches the
      idiom in goal_queue.dart.md's `_q`); Dart `Map<int, void
      Function(Term)>` -> `Dictionary<int, Action<Term>>` (Dart `void
      Function(Term)` -> .NET `Action<Term>` — Microsoft Learn
      System.Action delegate is the canonical .NET counterpart of a
      callback returning void with one argument). NOT `record class` /
      `struct` / `record struct` — the heap is the canonical mutable
      runtime state container, held by reference from every
      `BytecodeRunner` / `RunnerContext` and mutated in place; reference
      identity is load-bearing (carry-forward of `GoalState` and
      `GoalQueue` reasoning from machine_state.dart.md and of
      `SuspensionRecord` from suspension.dart.md — rf-dart-shared-
      mutable-record-by-reference-to-csharp-class). NOT `static class`
      — the runtime supports MULTIPLE concurrent heaps (one per
      isolate / per agent / per MadContext per the multiagent layer); a
      static singleton would silently break multiagent isolation. NO
      `partial` modifier (no need to split across files; emit as one
      `.cs`).
    idiom_id: null
    research_finding_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    nuance: >-
      VALUE-VS-REFERENCE / IDENTITY (LOAD-BEARING, explicitly addressed):
      `HeapFCP` is THE per-isolate / per-agent mutable runtime state
      container; the runtime mutates `Cells` (via `Add`), `Hp` (via
      `++`), and `_bindCallbacks` (via indexer set / Remove). Held by
      reference from outer runner state (analogous to `GoalState` in
      machine_state.dart.md — same authoritative rf-dart-mutable-state-
      class-identity-equality-to-csharp-class finding, FR-012 cache
      hit). Record/struct/record-struct categorically rejected — would
      either inject value-equality on heap-state coincidence or copy-
      on-assignment and lose `Hp` mutations. CONCURRENCY MODEL nuance
      (explicitly addressed, NOT glossed): Dart `HeapFCP` is owned by
      exactly one isolate (Dart's single-threaded event-loop model);
      no `lock`, no `Interlocked`, no `volatile` in the source. The
      .NET port MUST preserve the single-owning-context invariant
      (one `HeapFCP` per agent/MadContext; mutations only from the
      owning context's thread/Task) — see `lib/multiagent/global_writers_table.dart.md`
      and `lib/multiagent/variable_table.dart.md` for the canonical
      multiagent-isolate-model decision. Plain mutable property `Hp`
      (NOT `volatile int`, NOT `Interlocked.Increment`); plain
      `Dictionary<int, Action<Term>>` (NOT `ConcurrentDictionary`) —
      introducing atomics would advertise a safety property the
      surrounding logic does not need and could mask real ordering
      bugs in the multi-isolate composition (FR-009 — address the
      nuance, don't paper over it). If a future re-host introduces
      multi-threaded heap access that becomes a SEPARATE design
      decision (escalate at that point) — see escalations[0] below.
      NAMING nuance (LOAD-BEARING, explicitly addressed): `HP` is the
      documented WAM/heap-pointer-architecture-spec.md name; the
      faithful translation is `Hp` (PascalCase per .NET naming
      guideline — Microsoft Learn capitalisation conventions: two-
      letter acronyms PascalCase first letter). The `Hp` spelling
      preserves spec-correspondence at trace/log read sites; codegen
      MUST NOT rename to `HeapPointer` (would lose spec linkage).
      Field-vs-property: ALL members exposed as auto-properties (not
      bare public fields) per .NET design guideline (Microsoft Learn
      framework design guidelines: prefer properties to public fields).
      NULL-SAFETY: every direct field is non-nullable
      (`List<HeapCell>`, `int`, `Dictionary<int, Action<Term>>`).
      DELEGATE-VS-EVENT nuance: Dart `void Function(Term)` is a
      callback type; .NET `Action<Term>` is the canonical delegate
      counterpart (Microsoft Learn `System.Action` documentation). NOT
      a C# `event` (events have add/remove semantics and a multi-
      subscriber list; this map is single-subscriber-per-writer — one
      callback per writerAddr). MAP-MUTATION nuance: `_bindCallbacks`
      is mutated via `Map[key] = value` (set / replace) and `Map
      .remove(key)` (returning the removed value) — .NET `Dictionary
      <K,V>` indexer assignment and `Remove(key)` cover the first two;
      Dart `Map.remove` RETURNS the removed value, .NET `Dictionary
      .Remove` returns a `bool` indicating existence — the codegen
      MUST use `if (_bindCallbacks.Remove(writerAddr, out var
      callback)) { ... }` (the `Remove(TKey, out TValue)` overload,
      available since .NET Core 2.0 per Microsoft Learn) to preserve
      the "remove-and-get" semantics that this file's `firePendingCallback`
      / `bindWriterWithCallbackControl` / `bindWriterToReader`
      patterns rely on. The non-out-overload `Remove(key)` would
      silently lose the value and force a separate lookup. Async /
      Stream / Sealed / Mixin: ABSENT — all methods on `HeapFCP` are
      synchronous; callbacks are synchronous `Action<Term>`. The .NET
      port MUST NOT introduce `Task` / `async` / `IAsyncEnumerable`
      anywhere in this file.

  - construct_key: dart.tuple_return.record_two_int_addresses_allocate_variable
    source_form: >-
      `(int, int) allocateVariable() { final writerAddr = HP; final readerAddr = HP + 1; HP += 2; cells.add(HeapCell(Pointer(readerAddr), CellTag.WrtTag)); cells.add(HeapCell(Pointer(writerAddr), CellTag.RoTag)); return (writerAddr, readerAddr); }` — uses Dart 3 RECORD syntax `(int, int)` as the return type and `(writerAddr, readerAddr)` as the record literal at return. Allocates two adjacent cells: a Writer cell holding `Pointer(readerAddr)` and a Reader cell holding `Pointer(writerAddr)` (bidirectional FCP pairing). Increments `HP` by 2.
    target_decision: >-
      A C# method with a `System.ValueTuple<int, int>` return type:
      `public (int WriterAddr, int ReaderAddr) AllocateVariable()`.
      Microsoft Learn (tuple types: https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-tuples)
      documents `ValueTuple` as the canonical .NET counterpart of a
      multi-value return; the named-component syntax `(int WriterAddr,
      int ReaderAddr)` allows callers to destructure as `var (wid, rid)
      = heap.AllocateVariable();` (matches Dart's positional record
      destructuring exactly). NOT `KeyValuePair<int, int>` (semantic
      mismatch — these are two equal addresses, not a key-value pair).
      NOT a named `record struct AllocateVariableResult(int WriterAddr,
      int ReaderAddr)` (over-translation — the source uses an anonymous
      record literal, and `ValueTuple` is the .NET-canonical
      counterpart). Body: assign `int writerAddr = Hp; int readerAddr =
      Hp + 1; Hp += 2;`, then `Cells.Add(new HeapCell(new Pointer(
      readerAddr), CellTag.WrtTag)); Cells.Add(new HeapCell(new
      Pointer(writerAddr), CellTag.RoTag));`, return `(writerAddr,
      readerAddr)`. CRITICAL: the allocation order (writer cell first,
      then reader cell) MUST be preserved EXACTLY — the writer's
      `Pointer(readerAddr)` references the address `HP+1` which is
      computed BEFORE the second `Add`; reordering would corrupt the
      pairing.
    idiom_id: null
    research_finding_id: rf-dart-record-return-to-csharp-valuetuple
    nuance: >-
      RECORD-VS-VALUETUPLE nuance (explicitly addressed): Dart 3
      records `(int, int)` are anonymous structural value types
      (https://dart.dev/language/records — Dart official: records are
      "anonymous, immutable, aggregate types"); .NET `ValueTuple<int,
      int>` is the structurally equivalent counterpart (Microsoft Learn
      value-tuples). Both are value types with structural equality and
      positional field access; .NET adds optional element naming
      (`(int WriterAddr, int ReaderAddr)`) which improves call-site
      readability without changing semantics. NOT `Tuple<int, int>`
      (reference-type — allocates per call, semantic mismatch with
      Dart record's value semantics). NOT a fresh `record struct` —
      over-translation (the source uses anonymous records, the .NET
      counterpart is also anonymous via `ValueTuple`). ORDER-OF-
      MUTATION nuance (LOAD-BEARING, explicitly addressed): the body
      MUST compute `writerAddr = HP` BEFORE incrementing `HP`, allocate
      the writer cell BEFORE the reader cell, and pass `Pointer(
      readerAddr)` / `Pointer(writerAddr)` with the correct cross-
      references — any reordering breaks the bidirectional pairing
      invariant from spec v3.2 §3.1. NULL-SAFETY: both ints are non-
      nullable. Async / Stream: ABSENT.

  - construct_key: dart.method.simple_allocator_imported_reader_writer_hp_increment_post
    source_form: >-
      `int allocateImportedReader() { final readerAddr = HP++; cells.add(HeapCell(null, CellTag.RoTag)); return readerAddr; }` and `int allocateImportedWriter() { final writerAddr = HP++; cells.add(HeapCell(null, CellTag.WrtTag)); return writerAddr; }` — single-cell allocators for imported variables (no paired local writer/reader). Both use POST-INCREMENT `HP++` (return current then increment). Both allocate cells with `null` content (the caller is expected to set the content to a `VariableEntry` afterwards).
    target_decision: >-
      Two C# methods: `public int AllocateImportedReader() { int
      readerAddr = Hp++; Cells.Add(new HeapCell(null, CellTag.RoTag));
      return readerAddr; }` and `public int AllocateImportedWriter()
      { int writerAddr = Hp++; Cells.Add(new HeapCell(null, CellTag
      .WrtTag)); return writerAddr; }`. Post-increment semantics `Hp++`
      are IDENTICAL across Dart and C# (both return current value
      then increment) — Microsoft Learn (postfix increment operator
      `++`: "the result of the operation is the value of the operand
      before the operation"). `new HeapCell(null, ...)` passes a
      literal `null` for the `object? content` parameter (legal under
      NRT because the parameter type is nullable). The cell is in the
      "imported, content not yet set" state — the doc-comment notes
      the caller MUST set `cell.content = VariableEntry(...)` after
      this call; the convspec records this contract verbatim.
    idiom_id: null
    research_finding_id: rf-dart-postincrement-and-method-shape-to-csharp-equivalent
    nuance: >-
      Postfix-increment nuance (explicitly addressed): Dart `HP++` and
      C# `Hp++` are byte-for-byte semantic equivalents (return prior
      value, then increment). NULL-as-content nuance: `new HeapCell(
      null, CellTag.RoTag)` is the load-bearing "freshly allocated,
      caller-will-populate" state; the cell `content` is nullable
      (`object?`) precisely to express this. The runtime invariant
      (caller MUST set content to a `VariableEntry`) is documented in
      the source's doc-comment and MUST be preserved verbatim in the
      .NET XML-doc; codegen MUST NOT introduce a constructor overload
      that REQUIRES a `VariableEntry` (would force a caller refactor).
      Single-statement methods can be expression-bodied in C# but the
      Dart source uses a block body for both — KEEP the block form
      for source-shape fidelity. Async / Stream: ABSENT.

  - construct_key: dart.boolean_predicate.expression_body_bounds_check_and_tag_eq
    source_form: >-
      `bool isWriter(int addr) => addr >= 0 && addr < cells.length && cells[addr].tag == CellTag.WrtTag;` and the analogous `isReader` (RoTag) and `isValue` (ValueTag). Three expression-bodied bounds-checked tag predicates.
    target_decision: >-
      Three expression-bodied C# methods: `public bool IsWriter(int
      addr) => addr >= 0 && addr < Cells.Count && Cells[addr].Tag ==
      CellTag.WrtTag;` and the analogous `IsReader` (RoTag) and
      `IsValue` (ValueTag). Dart `cells.length` -> .NET `Cells.Count`
      (the `List<T>.Count` property — Microsoft Learn `System
      .Collections.Generic.List<T>.Count`). Short-circuit `&&`
      semantics are identical across Dart and C# (both lazy: right
      operand not evaluated if left is false — Microsoft Learn
      conditional logical operators). The bounds check (`addr >= 0 &&
      addr < Cells.Count`) MUST be preserved (defensive against
      negative addresses and out-of-range — the source uses these
      predicates as the safe-test gate).
    idiom_id: null
    research_finding_id: rf-dart-boolean-predicate-short-circuit-to-csharp-equivalent
    nuance: >-
      Short-circuit nuance (explicitly addressed): both `&&` evaluate
      lazily left-to-right; the bounds check `addr < Cells.Count` is
      load-bearing because the next conjunct `Cells[addr].Tag`
      dereferences the list — without the bounds check, an out-of-
      range access would throw `ArgumentOutOfRangeException` (.NET)
      vs `RangeError` (Dart). Both languages preserve the same
      defensive-test contract. List-length-property naming (Dart
      `length` -> .NET `Count`) per Microsoft Learn `IList<T>` /
      `ICollection<T>` — the canonical .NET name is `Count`, NOT
      `Length` (Length applies to arrays / strings, Count applies to
      collections). Expression-bodied form is the .NET-canonical idiom
      for single-expression methods (Microsoft Learn expression-
      bodied members). NULL-SAFETY: `Cells[addr]` returns a non-
      nullable `HeapCell` (list element type is non-nullable in the
      target); no null-check needed. Async / Stream: ABSENT.

  - construct_key: dart.nullable_int_navigation.try_writer_for_reader_optional_with_doc_intensive
    source_form: >-
      `int? tryWriterForReader(int readerAddr) { final cell = cells[readerAddr]; if (cell.tag != CellTag.RoTag) { return null; } if (cell.content is Pointer) { return (cell.content as Pointer).targetAddr; } return null; }` — returns `int?` (nullable int) representing either the writer address for a local reader or `null` for an imported reader / non-reader. The doc-comment is extremely detailed (200+ lines) explaining the three caller modes (suspending operations, read-only operations, binding operations) and documenting the common mistakes (using `wid!` without null check; silently ignoring null; throwing errors that break multiagent). NO `==`/`hashCode` override. Type-test pattern `cell.content is Pointer` followed by `(cell.content as Pointer).targetAddr`.
    target_decision: >-
      A C# method `public int? TryWriterForReader(int readerAddr)`
      with body using C# pattern-match: `var cell = Cells[readerAddr];
      if (cell.Tag != CellTag.RoTag) return null; if (cell.Content is
      Pointer ptr) return ptr.TargetAddr; return null;`. The pattern-
      match `is Pointer ptr` BINDS the typed reference in one
      statement (Microsoft Learn pattern matching) and is semantically
      equivalent to Dart's `is Pointer` + `as Pointer` two-step. NULL-
      INT return type is `int?` (System.Nullable<int>) — the .NET-
      canonical "value or absent" shape for a value type, matching
      Dart `int?`. The doc-comment is PRESERVED VERBATIM as XML doc:
      every paragraph (including the three caller-mode subsections,
      the "common mistakes to avoid" list, and the load-bearing
      contract that "Imported readers cannot be targets of writer-to-
      reader binding") is migrated to `<remarks>` / `<example>` XML
      doc tags — this is a load-bearing API contract document that
      every caller must read.
    idiom_id: null
    research_finding_id: rf-dart-nullable-int-return-with-type-test-to-csharp-pattern-match
    nuance: >-
      NULL-SAFETY nuance (LOAD-BEARING, explicitly addressed): `int?`
      is the explicit "no local writer" sentinel — the API contract
      relies on callers handling `null` explicitly. C# `int?`
      (Nullable<int>) preserves this exactly; codegen MUST NOT
      introduce a magic-int sentinel (e.g. `-1`) — would silently
      change the contract. PATTERN-MATCH nuance (explicitly
      addressed): C# pattern-match `is Pointer ptr` is the .NET-
      canonical replacement for Dart's `is X` + `as X` two-step
      (Microsoft Learn pattern matching: "type pattern with
      designation"). Semantically equivalent; pattern-match avoids the
      double type-test (Dart `is X` + `as X` both check the type at
      runtime); the .NET form is also clearer at the source level.
      DOC-COMMENT-FIDELITY nuance (LOAD-BEARING for THIS method,
      explicitly addressed, NOT glossed): the source's 90-line doc-
      comment is an API contract document — every caller-mode
      example (suspending / read-only / binding) and every "common
      mistake to avoid" warning is load-bearing. Codegen MUST
      preserve the full doc-comment content as XML doc (`<summary>`
      / `<remarks>` / `<example>` tags); shortening would silently
      drop the contract documentation that prevents real bugs
      (e.g. `wid!` without null check would crash on imported
      readers). Async / Stream / Mixin: ABSENT.

  - construct_key: dart.nullable_int_navigation.reader_for_writer_bidirectional_check_with_writer_content
    source_form: >-
      `int? readerForWriter(int writerAddr) { ... three cases: (1) Pointer to paired reader (verify bidirectional), (2) WriterContent.readerAddr, (3) bound or invalid -> null }` — searches three forms of writer content. Case (1) uses the BIDIRECTIONAL POINTER pattern (the writer's content is Pointer(reader), AND the reader at that address is Pointer(writer) pointing back — both must match). Uses nested type-tests with type-cast extraction.
    target_decision: >-
      A C# method `public int? ReaderForWriter(int writerAddr)` with
      C# pattern-match on each case: (1) `if (cell.Content is Pointer
      ptr1) { int target = ptr1.TargetAddr; if (target < Cells.Count
      && Cells[target].Tag == CellTag.RoTag && Cells[target].Content
      is Pointer readerPtr && readerPtr.TargetAddr == writerAddr) return
      target; return null; }` (2) `if (cell.Content is WriterContent
      wc) return wc.ReaderAddr;` (3) `return null;`. The BIDIRECTIONAL
      verification (`readerPtr.TargetAddr == writerAddr`) MUST be
      preserved — this is the runtime's check that the writer is
      actually unbound (its pointer to "reader" still points to the
      paired reader which still points back); if the writer has been
      bound to something else, this check fails and the method
      correctly returns null. NULL-RETURN semantic preserved as `int?`.
    idiom_id: null
    research_finding_id: rf-dart-nullable-int-return-with-type-test-to-csharp-pattern-match
    nuance: >-
      BIDIRECTIONAL-POINTER-PATTERN nuance (LOAD-BEARING, explicitly
      addressed): the FCP pairing invariant is "writer points at
      reader, reader points at writer, both unbound". Case (1)
      verifies BOTH pointers by following the chain — codegen MUST
      preserve the full check (Cells[target].Tag == CellTag.RoTag AND
      Cells[target].Content is Pointer AND that Pointer's TargetAddr
      == writerAddr). Reducing to a one-way check would silently
      accept a bound writer as unbound, a correctness bug. PATTERN-
      MATCH nuance: same as above — `is Pointer ptr` binds the typed
      reference; semantically equivalent to the Dart `is`+`as` two-
      step. NULL-SAFETY: `int?` return matches Dart `int?`. Async /
      Stream: ABSENT.

  - construct_key: dart.method.paired_reader_addr_fallback_to_address_arithmetic
    source_form: >-
      `int pairedReaderAddr(int writerAddr) { final reader = readerForWriter(writerAddr); if (reader != null) return reader; return writerAddr + 1; }` — non-nullable int return; falls back to address arithmetic (`writerAddr + 1`) when `readerForWriter` returns null. Doc-comment notes this is for "when you need the reader address regardless of whether the writer is currently bound" — by allocation invariant, reader is always at `writerAddr + 1`.
    target_decision: >-
      A C# method `public int PairedReaderAddr(int writerAddr) { int?
      reader = ReaderForWriter(writerAddr); if (reader != null) return
      reader.Value; return writerAddr + 1; }`. The non-nullable return
      type is plain `int`. NOT the `??` null-coalescing operator
      `return ReaderForWriter(writerAddr) ?? (writerAddr + 1);`
      (TEMPTING and arguably cleaner but the source uses an explicit
      `if` form — preserve source shape; both forms are semantically
      identical, and `??` is acceptable at codegen discretion since
      Microsoft Learn null-coalescing operator documents the operator
      as semantically equivalent to the `if`/`else` form). The
      address-arithmetic fallback is preserved verbatim — this is the
      "by allocation, reader is at writerAddr + 1" invariant; codegen
      MUST NOT change to `writerAddr + 2` or any other constant.
    idiom_id: null
    research_finding_id: rf-dart-nullable-int-fallback-to-csharp-equivalent
    nuance: >-
      ADDRESS-ARITHMETIC-INVARIANT nuance (LOAD-BEARING, explicitly
      addressed): the `writerAddr + 1` literal encodes the FCP
      two-cell allocation pattern (allocateVariable allocates writer
      at HP, reader at HP+1, then HP += 2). Codegen MUST preserve the
      literal `+ 1`. This invariant is documented at the
      allocateVariable site and is the documented fallback when the
      bidirectional pointer pattern fails (e.g. the writer has been
      bound to a different cell, losing direct pointer to its reader).
      NULL-COALESCE-VS-IF nuance: source uses explicit `if`; either
      `if` or `??` is semantically faithful in C#. Async / Stream:
      ABSENT.

  - construct_key: dart.deref_addr.large_switch_with_visited_cycle_detection_wxw_violation_and_three_tag_cases
    source_form: >-
      `Object derefAddr(int startAddr) { ... while (true) { ... visited.add(current); ... switch (cell.tag) { case CellTag.RoTag: ...; case CellTag.WrtTag: ...; case CellTag.ValueTag: ...; } } }` — the LOAD-BEARING dereferencing method. Returns `Object` (Dart top type; runtime returns one of `Term` (bound) / `VarRef` (unbound writer) / `VariableEntry` (imported unbound)). Tracks visited addresses in a `Set<int>` and throws `StateError('Cycle detected at address $current - SRSW violation!')` on revisit. Tracks the PREVIOUS cell's tag and throws `StateError('SRSW violation: writer at ... points to writer at ...')` on WxW (writer-to-writer pointer) detection. Three switch cases mirror the three tags. Reader cells with `VariableEntry` content check `entry.boundValue != null` and return that cached value; otherwise return the entry. Writer cells with `VariableEntry` content do the same. Writer cells with `WriterContent` content return `VarRef(current)` (unbound writer). Writer cells with `Pointer` content perform the bidirectional check (unbound -> VarRef; bound -> follow). ValueTag cells return `cell.content as Term`.
    target_decision: >-
      A C# method `public object DerefAddr(int startAddr)` returning
      non-nullable `object` (Dart `Object` -> .NET `object`; the
      method returns one of `Term` / `VarRef` / `VariableEntry`, all
      reference types, never null at any return site). Body uses a
      `while (true)` loop with manual visited-tracking via `HashSet<
      int>` (Dart `Set<int>` -> .NET `HashSet<int>` — Microsoft Learn
      System.Collections.Generic.HashSet<T> is the canonical .NET
      counterpart for an unordered uniqueness set), plus a `CellTag?`
      tracking variable (`previousTag`) initialised to null. Cycle
      detection: `if (visited.Contains(current)) throw new
      InvalidOperationException($"Cycle detected at address {current}
      - SRSW violation!");` — Dart `StateError` -> .NET `InvalidOperationException`
      (Microsoft Learn: "An InvalidOperationException is used in cases
      when the failure to invoke a method is caused by reasons other
      than invalid arguments" — the documented counterpart of Dart
      `StateError` which signals "an internally inconsistent state").
      WxW-violation throw: same `InvalidOperationException` mapping.
      Switch statement: C# switch on `cell.Tag` with three case arms;
      use pattern-match `case CellTag.RoTag:` and within each arm,
      pattern-match on `cell.Content` (`if (cell.Content is VariableEntry
      entry) { ... }` etc.). The `entry.BoundValue != null` check uses
      C# null-conditional/null-comparison. Return statements preserve
      Dart semantics exactly (return `entry.BoundValue!` becomes
      `return entry.BoundValue;` since the null-check has been done —
      NRT flow analysis tracks this, no `!` needed). The
      `previousTag = cell.Tag; current = ptr.TargetAddr; continue;`
      loop-continuation pattern is preserved verbatim.
    idiom_id: null
    research_finding_id: rf-dart-staterror-to-csharp-invalidoperationexception
    nuance: >-
      EXCEPTION-TYPE nuance (LOAD-BEARING, explicitly addressed):
      Dart `StateError` (https://api.dart.dev/stable/dart-core/StateError-class.html)
      "indicates that an operation cannot be performed because the
      object is in an inappropriate state" — Microsoft Learn documents
      `System.InvalidOperationException` with the IDENTICAL contract:
      "thrown when a method call is invalid for the object's current
      state". Mapping is canonical and 1:1. CRITICAL: do NOT map
      `StateError` -> .NET `Exception` (too broad — would catch
      everything) or .NET `ArgumentException` (semantic mismatch —
      arguments are not the problem; the OBJECT STATE is). CYCLE-
      DETECTION + WxW nuance (LOAD-BEARING, explicitly addressed):
      the visited-set and previous-tag tracking are the spec's
      defensive checks against SRSW (Single-Reader/Single-Writer)
      violations — chasing through a cyclic chain of cells or two
      writers pointing at each other. Codegen MUST preserve BOTH
      checks; removing either would silently allow corrupt heap
      states to loop or be accepted as valid. SET-IMPL nuance:
      Dart `<int>{}` (Set literal) -> .NET `new HashSet<int>()`. Dart
      `set.contains(x)` -> .NET `set.Contains(x)`; Dart `set.add(x)`
      -> .NET `set.Add(x)`. Microsoft Learn HashSet documentation
      confirms O(1) Contains/Add — same complexity profile as Dart's
      LinkedHashSet (the literal `{}` for int is backed by a hash
      set). PATTERN-MATCH nuance: use `case CellTag.RoTag:` arms and
      `cell.Content is X x` within each arm — semantically equivalent
      to Dart's case-then-is. SWITCH-EXHAUSTIVENESS nuance: the Dart
      switch has no `default` arm — relies on exhaustive coverage of
      the three enum cases. C# switch over an enum allows but does
      NOT require exhaustive coverage; codegen MAY add `default: throw
      new InvalidOperationException($"Unknown CellTag: {cell.Tag}");`
      as a defensive arm (a future fourth tag would land in default
      rather than silently returning the previous case's value). This
      defensive default is NOT in the Dart source but is a small
      additive safety net consistent with .NET conventions; either
      shape is acceptable to codegen. RETURN-TYPE nuance: Dart
      `Object` (NOT `Object?`) is the non-nullable top; .NET
      `object` (NOT `object?`) is the non-nullable top under NRT.
      Both correctly NON-nullable — every return-site returns a
      non-null reference (Term / VarRef / VariableEntry). NULL-
      ASSERTION nuance: Dart `entry.boundValue!` (null-assertion)
      AFTER an `entry.boundValue != null` test is the documented
      "trust me, it's non-null" pattern; .NET NRT flow analysis
      tracks the null-test and allows `return entry.BoundValue;`
      without `!` — codegen produces cleaner C#. Async / Stream:
      ABSENT.

  - construct_key: dart.bind_writer_family.callback_control_with_in_place_mutation_returning_activation_list
    source_form: >-
      Three related methods: `bindWriter` (fireCallback=true), `bindWriterNoCallback` (fireCallback=false), `bindWriterWithCallbackControl` (the internal worker with a `{required bool fireCallback}` named arg). The worker (1) validates the cell tag is WrtTag (throws StateError otherwise), (2) walks any existing `WriterContent.suspensions` and accumulates armed activations into a `List<GoalRef>`, (3) MUTATES `cell.content = value; cell.tag = CellTag.ValueTag;` IN PLACE (writer becomes a value cell), (4) optionally removes-and-fires the registered `_bindCallbacks[writerAddr]` callback, (5) returns the activations list. The mutation pattern (overwrite both content and tag, drop the suspension list which was already saved) is load-bearing — this is the FCP "binding fires suspensions and transitions cell to bound state" semantic.
    target_decision: >-
      Three C# methods in the namespace mirroring `lib/runtime/`:
      (1) `public List<GoalRef> BindWriter(int writerAddr, Term
      value) => BindWriterWithCallbackControl(writerAddr, value,
      fireCallback: true);` (delegating wrapper, expression-bodied);
      (2) `public List<GoalRef> BindWriterNoCallback(int writerAddr,
      Term value) => BindWriterWithCallbackControl(writerAddr, value,
      fireCallback: false);` (delegating wrapper, expression-bodied);
      (3) `public List<GoalRef> BindWriterWithCallbackControl(int
      writerAddr, Term value, bool fireCallback)` (the worker — `bool
      fireCallback` is the required-non-optional positional parameter;
      Dart's `{required bool fireCallback}` named-required maps to
      .NET non-optional positional, and callers pass it as named-
      argument `fireCallback: true` per the .NET named-argument call
      convention). Worker body: `var cell = Cells[writerAddr]; if
      (cell.Tag != CellTag.WrtTag) throw new InvalidOperationException
      ($"bindWriter called on non-writer cell at {writerAddr} (tag:
      {cell.Tag})"); var activations = new List<GoalRef>(); if (cell
      .Content is WriterContent wc) WalkAndActivate(wc.Suspensions,
      activations); cell.Content = value; cell.Tag = CellTag.ValueTag;
      if (fireCallback) { if (_bindCallbacks.Remove(writerAddr, out
      var callback)) callback(value); } return activations;`. The
      in-place mutation pattern is preserved exactly. `List<GoalRef>`
      is the C#-canonical mutable list (Dart `<GoalRef>[]` -> `new
      List<GoalRef>()`); `GoalRef` is the `readonly record struct`
      from machine_state.dart.md — value-type elements in a reference-
      type list is fine (no boxing concern; `List<T>` is generic and
      stores value-type T unboxed per Microsoft Learn List<T>).
    idiom_id: null
    research_finding_id: rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults
    nuance: >-
      IN-PLACE-MUTATION nuance (LOAD-BEARING, explicitly addressed):
      `cell.Content = value; cell.Tag = CellTag.ValueTag;` mutates
      BOTH fields on the same `HeapCell` instance — every observer of
      the cell (every reader's `Pointer(writerAddr)`, every
      `Cells[writerAddr]` lookup) sees the binding immediately
      because `HeapCell` is a reference type. A value-type `HeapCell`
      would copy on `Cells[writerAddr]` access and the mutation
      would be lost — catastrophic. Inherits the reference-identity
      rationale from the `HeapCell` construct above. NAMED-REQUIRED-
      ARG nuance: Dart `{required bool fireCallback}` -> .NET
      positional `bool fireCallback` (no default). Microsoft Learn
      (named-and-optional arguments) confirms positional parameters
      can be passed by name at call sites; the C# 11 `required`
      modifier on properties is REJECTED (semantically aligned but
      requires object-initialiser call syntax). CALLBACK-REMOVE-AND-
      FIRE nuance (LOAD-BEARING, explicitly addressed): Dart `_bindCallbacks
      .remove(writerAddr)` returns the removed value (or null if
      absent). .NET `Dictionary<K,V>.Remove(K)` returns a `bool`
      indicating existence; the `Remove(TKey, out TValue)` overload
      (Microsoft Learn, available since .NET Core 2.0) returns BOTH
      the bool AND the value. Codegen MUST use the out-overload to
      preserve "remove-and-get" atomic semantics (carry-forward from
      the `HeapFCP` class construct above). EXCEPTION-TYPE: StateError
      -> InvalidOperationException (carry-forward). LIST<T> nuance:
      `new List<GoalRef>()` is the C# counterpart of Dart `<GoalRef>[]`;
      `List<T>` stores `record struct` elements unboxed (Microsoft
      Learn `List<T>`). Async / Stream: ABSENT.

  - construct_key: dart.method.fire_pending_callback_remove_and_invoke_if_value_present
    source_form: >-
      `void firePendingCallback(int writerAddr) { final callback = _bindCallbacks.remove(writerAddr); if (callback != null) { final value = getValue(writerAddr); if (value != null) { callback(value); } } }` — used after all bindings complete to fire deferred callbacks. Removes the callback first (to prevent double-fire), then reads the value (which should be bound by now), then invokes.
    target_decision: >-
      A C# method `public void FirePendingCallback(int writerAddr) {
      if (_bindCallbacks.Remove(writerAddr, out var callback)) { var
      value = GetValue(writerAddr); if (value != null) callback(value);
      } }`. Uses the `Dictionary.Remove(TKey, out TValue)` overload
      (per Microsoft Learn) to atomically remove-and-get the
      callback; the resulting `callback` is non-null inside the if
      branch (`Remove` returns true iff value was present and out-set).
      The `value != null` check preserves the source guard — calling
      back with a null `Term` would violate the callback's `Action<
      Term>` non-nullable contract.
    idiom_id: null
    research_finding_id: rf-dart-map-remove-and-invoke-to-csharp-dictionary-remove-out
    nuance: >-
      REMOVE-AND-GET nuance (LOAD-BEARING, explicitly addressed, NOT
      glossed): Dart `Map.remove(key)` returns `V?` (the removed
      value or null); .NET `Dictionary<K,V>.Remove(key)` returns
      `bool` and DOES NOT yield the value via the single-arg overload.
      Codegen MUST use the .NET-canonical `Remove(TKey, out TValue)`
      overload (Microsoft Learn Dictionary<TKey,TValue>.Remove(TKey,
      TValue) — available since .NET Core 2.0) to preserve the
      atomic remove-and-get semantics. Naive translation
      `if (_bindCallbacks.ContainsKey(...)) { var cb = _bindCallbacks
      [...]; _bindCallbacks.Remove(...); }` is REJECTED — it's a
      two-step that could race in a future concurrent re-host AND
      pays a double-lookup cost. NULL-RETURN-AS-ABSENT nuance: Dart's
      "null means absent" is a load-bearing API contract on the
      callback map; .NET's "out-bool means absent" preserves the
      same semantic via a different idiom. Async / Stream: ABSENT.

  - construct_key: dart.bind_writer_to_reader.in_place_mutation_with_suspension_forwarding_and_callback_relocation
    source_form: >-
      `List<GoalRef> bindWriterToReader(int writerAddr, int readerAddr) { ... (1) validate writerCell.tag == WrtTag (throw StateError); (2) validate readerCell.tag == RoTag (throw StateError); (3) tryWriterForReader(readerAddr) — if null, throw StateError (imported reader can't be target); (4) forward suspensions from writerCell's WriterContent to targetWriter via _forwardSuspensions; (5) MUTATE writerCell.content = Pointer(readerAddr) (creates variable chain — tag remains WrtTag); (6) RELOCATE callback: if _bindCallbacks[writerAddr] exists, move it to _bindCallbacks[targetWriterAddr]; (7) return activations (empty for the no-immediate-activation case). }` — binds writer to writer-via-reader (creates variable chain). Tag REMAINS WrtTag (not transitioned to ValueTag — this is a chain, not a ground binding).
    target_decision: >-
      A C# method `public List<GoalRef> BindWriterToReader(int
      writerAddr, int readerAddr)` body: `var writerCell = Cells[
      writerAddr]; if (writerCell.Tag != CellTag.WrtTag) throw new
      InvalidOperationException($"bindWriterToReader called on non-
      writer at {writerAddr}"); var readerCell = Cells[readerAddr];
      if (readerCell.Tag != CellTag.RoTag) throw new
      InvalidOperationException($"bindWriterToReader target is not a
      reader at {readerAddr}"); int? targetWriterAddr =
      TryWriterForReader(readerAddr); if (targetWriterAddr == null)
      throw new InvalidOperationException($"bindWriterToReader target
      at {readerAddr} is an imported reader (no local writer)"); var
      activations = new List<GoalRef>(); if (writerCell.Content is
      WriterContent wc) ForwardSuspensions(wc.Suspensions,
      targetWriterAddr.Value); writerCell.Content = new Pointer(
      readerAddr); /* Tag remains WrtTag */ if (_bindCallbacks.Remove(
      writerAddr, out var callback)) _bindCallbacks[targetWriterAddr
      .Value] = callback; return activations;`. All three throw sites
      use `InvalidOperationException` (Dart StateError analogue). The
      tag-remains-WrtTag invariant is preserved by NOT setting `Tag`
      (only `Content` is mutated).
    idiom_id: null
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      TAG-REMAINS-WRTTAG nuance (LOAD-BEARING, explicitly addressed):
      this method does NOT transition the cell from writer to value
      — only `Content` is mutated to a new `Pointer`; `Tag` remains
      `CellTag.WrtTag`. This is the variable-chain semantic (per
      spec §5.3) — the cell still represents a writer, but bound to
      another writer (via the latter's reader). Catastrophic if
      codegen accidentally adds `writerCell.Tag = CellTag.ValueTag;`
      — would silently break unification (subsequent dereferences
      would treat the chain as a bound-to-ground value rather than
      following the chain). CALLBACK-RELOCATION nuance (LOAD-BEARING):
      when writer A is bound to writer B (via B's reader), any pending
      bind-callback registered on A must be MOVED to B — because A
      will never be bound directly again (it's now a chain to B), but
      B will eventually be bound (and then A's callback should fire).
      The Dart pattern `final callback = _bindCallbacks.remove(
      writerAddr); if (callback != null) _bindCallbacks[
      targetWriterAddr] = callback;` is preserved in C# via the
      `Remove(out var callback)` + indexer-set idiom. SUSPENSION-
      FORWARDING nuance: pending suspensions on A are forwarded to B
      via `ForwardSuspensions` (see next construct). NULLABLE-INT-
      UNWRAP nuance: `targetWriterAddr.Value` is the C# Nullable<int>
      unwrap; the if-null branch has thrown, so the access is safe
      (NRT flow analysis tracks this). Inherits the rf-dart-shared-
      mutable-record-by-reference-to-csharp-class idiom because
      `WriterContent` mutation is in-place. Async / Stream: ABSENT.

  - construct_key: dart.method.bind_writer_to_writer_explicit_wxw_violation_throw
    source_form: "`void bindWriterToWriter(int w1, int w2) { throw StateError('WxW violation: cannot bind writer $w1 to writer $w2'); }` — the FCP spec §5.2 violation method; always throws. Documented as 'this is forbidden and should throw'."
    target_decision: >-
      A C# method `public void BindWriterToWriter(int w1, int w2) =>
      throw new InvalidOperationException($"WxW violation: cannot
      bind writer {w1} to writer {w2}");` — expression-bodied throw
      (Microsoft Learn: expression-bodied members can throw via
      `throw` expression, supported since C# 7). The method exists
      to be CALLED and throw — codegen MUST NOT inline the throw into
      every caller (the method shape is part of the API surface,
      consumed by `bindVariable` below).
    idiom_id: null
    research_finding_id: rf-dart-staterror-to-csharp-invalidoperationexception
    nuance: >-
      ALWAYS-THROW method nuance (explicitly addressed): the method
      is a documented violation-detector; its only purpose is to
      throw. C# expression-bodied throw is the .NET-canonical idiom
      for always-throwing methods (Microsoft Learn throw expression).
      Exception-type carry-forward (StateError -> InvalidOperationException).
      Async / Stream: ABSENT.

  - construct_key: dart.suspend_on_writer.three_branch_promotion_writer_content_or_throw
    source_form: >-
      `void suspendOnWriter(int writerAddr, SuspensionRecord record) { final cell = cells[writerAddr]; if (cell.tag != CellTag.WrtTag) throw StateError(...); final node = SuspensionListNode(record); if (cell.content is WriterContent) { final wc = cell.content as WriterContent; node.next = wc.suspensions; wc.suspensions = node; } else if (cell.content is Pointer) { final readerAddr = (cell.content as Pointer).targetAddr; cell.content = WriterContent(readerAddr, node); } else { throw StateError('suspendOnWriter: unexpected content ${cell.content} at $writerAddr'); } }` — three branches: WriterContent (cons to existing list), Pointer (promote to WriterContent), other (throw).
    target_decision: >-
      A C# method `public void SuspendOnWriter(int writerAddr,
      SuspensionRecord record) { var cell = Cells[writerAddr]; if
      (cell.Tag != CellTag.WrtTag) throw new InvalidOperationException
      ($"suspendOnWriter called on non-writer at {writerAddr}"); var
      node = new SuspensionListNode(record); if (cell.Content is
      WriterContent wc) { node.Next = wc.Suspensions; wc.Suspensions =
      node; } else if (cell.Content is Pointer ptr) { cell.Content =
      new WriterContent(ptr.TargetAddr, node); } else throw new
      InvalidOperationException($"suspendOnWriter: unexpected content
      {cell.Content} at {writerAddr}"); }`. The pattern-match `is
      WriterContent wc` / `is Pointer ptr` binds the typed reference
      in one statement (Microsoft Learn pattern matching). Cons-to-
      list mutation (`node.Next = wc.Suspensions; wc.Suspensions =
      node;`) is preserved verbatim — this is the standard linked-
      list prepend pattern; both `Next` and `Suspensions` are
      mutable properties on reference-type classes (see suspension.dart.md
      and `WriterContent` above).
    idiom_id: null
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      LINKED-LIST-PREPEND nuance (explicitly addressed): `node.Next =
      wc.Suspensions; wc.Suspensions = node;` is the canonical O(1)
      head-insert; preserves Dart semantics verbatim. The order
      MATTERS — codegen MUST set `node.Next` BEFORE updating `wc
      .Suspensions` (otherwise the previous head is lost). PROMOTION
      nuance (LOAD-BEARING, explicitly addressed): when the writer's
      content is currently a bare `Pointer` (no suspensions yet), the
      first suspension PROMOTES the content to a `WriterContent`
      compound that wraps the same `readerAddr` AND the new
      suspension head. This promotion preserves the FCP pairing (the
      reader can still find the writer via the writer's content)
      while accumulating suspensions. ERROR-CONTENT nuance: the
      "unexpected content" branch covers cells whose content is
      `null` (unallocated imported), `Term`/`VariableEntry`/value
      (already bound — can't suspend on a bound writer), which is
      correctly an InvalidOperationException. Exception-type carry-
      forward. Async / Stream: ABSENT.

  - construct_key: dart.suspend_on_reader.two_branch_variable_entry_or_delegate
    source_form: >-
      `void suspendOnReader(int readerAddr, SuspensionRecord record) { ... if (cell.content is VariableEntry) { ... entry.suspensions = node; return; } if (cell.tag != CellTag.RoTag || cell.content is! Pointer) throw StateError(...); final writerAddr = (cell.content as Pointer).targetAddr; suspendOnWriter(writerAddr, record); }` — two branches: imported reader (VariableEntry → mutate entry.suspensions chain) vs local reader (delegate to suspendOnWriter via the reader's pointer).
    target_decision: >-
      A C# method `public void SuspendOnReader(int readerAddr,
      SuspensionRecord record) { var cell = Cells[readerAddr]; if
      (cell.Content is VariableEntry entry) { var node = new
      SuspensionListNode(record); node.Next = entry.Suspensions;
      entry.Suspensions = node; return; } if (cell.Tag != CellTag
      .RoTag || cell.Content is not Pointer ptr) throw new
      InvalidOperationException($"suspendOnReader called on invalid
      reader at {readerAddr}"); SuspendOnWriter(ptr.TargetAddr,
      record); }`. The `is not Pointer ptr` pattern (Microsoft Learn
      pattern matching: negated type pattern; available since C# 9)
      is the canonical counterpart of Dart `is! Pointer`. Imported-
      reader branch mutates `entry.Suspensions` chain (see
      variable_table.dart.md — `VariableEntry.Suspensions` is `{ get;
      set; }` precisely for this in-place mutation). Local-reader
      branch delegates to `SuspendOnWriter`.
    idiom_id: null
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      VARIABLE-ENTRY-MUTATION nuance (LOAD-BEARING, explicitly
      addressed): for imported readers, the `VariableEntry` holds
      the suspension chain (since the imported reader has no local
      writer cell to attach suspensions to — per the variable_table
      doc). Mutating `entry.Suspensions = node` propagates to every
      observer of the entry because `VariableEntry` is a reference-
      type class (carry-forward from variable_table.dart.md). PATTERN-
      NEGATION nuance: Dart `is!` -> C# `is not` (the C# 9 negated
      type pattern). Microsoft Learn pattern matching documents this
      as the .NET-canonical form. SHORT-CIRCUIT-OR nuance: Dart `||`
      and C# `||` are both lazy left-to-right; the order of the two
      conjuncts is preserved (tag check first, then content type
      check). Exception-type carry-forward. Async / Stream: ABSENT.

  - construct_key: dart.private_forward_suspensions.walk_armed_clone_node_three_target_branches
    source_form: >-
      `void _forwardSuspensions(SuspensionListNode? list, int targetWriterAddr) { ... walks list; for each armed node, creates a NEW node sharing the same record (NOT the old node — sharing the RECORD preserves disarm propagation), and prepends it to the target's WriterContent (or promotes Pointer → WriterContent). }` — leading-underscore private. Iterates a linked list, clones armed nodes (record SHARED, next CHAIN built fresh), and prepends to target.
    target_decision: >-
      A C# `private void ForwardSuspensions(SuspensionListNode?
      list, int targetWriterAddr)` (leading underscore dropped in
      .NET — the private modifier is the canonical visibility marker;
      private methods do not need underscore prefix per .NET naming
      guideline). Body: `var current = list; while (current != null)
      { if (current.Armed) { var newNode = new SuspensionListNode(
      current.Record); var targetCell = Cells[targetWriterAddr]; if
      (targetCell.Content is WriterContent wc) { newNode.Next = wc
      .Suspensions; wc.Suspensions = newNode; } else if (targetCell
      .Content is Pointer ptr) { targetCell.Content = new WriterContent(
      ptr.TargetAddr, newNode); } /* else: target is bound or invalid,
      ignored */ } current = current.Next; }`. CRITICAL: the new node
      shares the OLD record (`new SuspensionListNode(current.Record)`)
      — disarm propagation depends on multiple list nodes pointing
      at the same `SuspensionRecord` (see suspension.dart.md). Codegen
      MUST NOT clone the record itself; only the wrapper node is
      cloned.
    idiom_id: null
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      RECORD-SHARED-WRAPPER-CLONED nuance (LOAD-BEARING, explicitly
      addressed, NOT glossed): the canonical FCP suspension-sharing
      idiom — one `SuspensionRecord` per suspended goal, multiple
      `SuspensionListNode` wrappers (one per reader cell or, here,
      forwarded across writer-chain links). The wrapper is cloned
      (`new SuspensionListNode(current.Record)`) but the RECORD is
      shared by reference — so `disarm()` on the shared record
      propagates to every wrapper, preventing double-activation. The
      .NET counterpart MUST preserve this — pass `current.Record` by
      reference (which it is — `SuspensionRecord` is a reference-
      type class per suspension.dart.md). Codegen MUST NOT clone the
      record (e.g. `new SuspensionRecord(current.Record.GoalId,
      current.Record.ResumePC)` would create a fresh record with
      independent disarm state — catastrophic). PRIVATE-METHOD-
      NAMING nuance (explicitly addressed): Dart `_forwardSuspensions`
      (leading underscore = library-private) -> .NET `private void
      ForwardSuspensions` (PascalCase, no underscore — Microsoft Learn
      .NET naming guidelines: private methods use PascalCase like
      public methods; the `private` modifier is the canonical
      visibility marker; underscore prefix is reserved for private
      FIELDS, not methods). IGNORED-CASE nuance: the `else` branch
      (target is bound or invalid) is intentionally a NO-OP — the
      source has no else branch. Codegen MUST preserve this
      silent-ignore semantic (do NOT throw for bound targets — would
      change semantics; the spec accepts that forwarding to a
      bound target is a no-op). Async / Stream: ABSENT.

  - construct_key: dart.private_static_walk_and_activate.walk_armed_record_disarm_and_collect_activation
    source_form: >-
      `static void _walkAndActivate(SuspensionListNode? list, List<GoalRef> activations) { var current = list; while (current != null) { if (current.armed) { activations.add(GoalRef(current.goalId!, current.resumePC)); current.record.disarm(); } current = current.next; } }` — STATIC method (no `this`). Walks a list; for each armed node, constructs a GoalRef from goalId/resumePC, adds to activations list, and DISARMS the shared record (preventing re-activation through any other wrapper pointing at the same record). The `current.goalId!` null-assertion is safe because `armed` iff `goalId != null` (per SuspensionRecord.armed getter).
    target_decision: >-
      A C# `private static void WalkAndActivate(SuspensionListNode?
      list, List<GoalRef> activations)`. Body: `var current = list;
      while (current != null) { if (current.Armed) { activations.Add(
      new GoalRef(current.GoalId!.Value, current.ResumePC)); current
      .Record.Disarm(); } current = current.Next; }`. The
      `current.GoalId!.Value` form unwraps the nullable `int?`: Dart
      `goalId!` -> .NET `GoalId!.Value` (the `!` is the null-forgiving
      operator that suppresses the NRT warning; `.Value` extracts the
      underlying `int` from `Nullable<int>`). Safe because of the
      armed precondition. `new GoalRef(int, int)` constructs the
      `readonly record struct` value (per machine_state.dart.md);
      `activations.Add(new GoalRef(...))` adds the value to the
      list (List<T> stores record-struct elements unboxed per
      Microsoft Learn List<T>). `current.Record.Disarm()` mutates
      the shared record's `GoalId` to null — every other wrapper
      pointing at the same record now sees `Armed == false`.
    idiom_id: null
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      STATIC-METHOD nuance (explicitly addressed): Dart `static void
      _walkAndActivate` and .NET `private static void WalkAndActivate`
      are byte-for-byte equivalent — neither captures instance state;
      both compile to a static dispatch. NULL-ASSERTION + VALUE-EXTRACT
      nuance (explicitly addressed): Dart `current.goalId!` (NRT-
      forgiving on `int?`) -> .NET `current.GoalId!.Value` (NRT-
      forgiving + Nullable<int>.Value unwrap). Microsoft Learn
      Nullable<T>.Value documentation: `Value` throws
      `InvalidOperationException` if `HasValue == false` — but the
      `Armed` precondition guarantees `HasValue == true`, so the
      throw is unreachable. DISARM-PROPAGATION nuance (LOAD-BEARING,
      explicitly addressed): `current.Record.Disarm()` is the
      load-bearing FCP "prevent double-activation" mechanism —
      mutates the SHARED record (per suspension.dart.md); every
      other wrapper pointing at the same record observes `Armed ==
      false` on its next walk. Catastrophic if the record were a
      value type — disarm would only mutate the local copy and
      double-activation would not be prevented. Inherits the
      reference-identity rationale from suspension.dart.md. Async /
      Stream: ABSENT.

  - construct_key: dart.method.is_fully_bound_via_deref_returning_bool
    source_form: >-
      `bool isFullyBound(int writerAddr) { final result = derefAddr(writerAddr); return result is! VarRef && result is! VariableEntry; }` — bound iff deref returns a Term (NOT a VarRef-unbound-writer AND NOT a VariableEntry-imported-unbound).
    target_decision: >-
      A C# method `public bool IsFullyBound(int writerAddr) { var
      result = DerefAddr(writerAddr); return result is not VarRef &&
      result is not VariableEntry; }`. The `is not X` pattern (C# 9
      negated type pattern, Microsoft Learn pattern matching) is the
      canonical counterpart of Dart `is! X`.
    idiom_id: null
    research_finding_id: rf-dart-is-not-type-test-to-csharp-is-not-pattern
    nuance: >-
      NEGATED-TYPE-PATTERN nuance: Dart `is!` -> C# `is not`
      (carry-forward from `SuspendOnReader`). Short-circuit `&&` is
      identical across both languages. Return-type `bool` is a value
      type, non-nullable in both languages. Async / Stream: ABSENT.

  - construct_key: dart.method.get_value_nullable_term_from_deref
    source_form: >-
      `Term? getValue(int writerAddr) { final result = derefAddr(writerAddr); if (result is VarRef || result is VariableEntry) return null; return result as Term; }` — nullable Term return; null means unbound. Otherwise casts the Object result to Term.
    target_decision: >-
      A C# method `public Term? GetValue(int writerAddr) { var
      result = DerefAddr(writerAddr); if (result is VarRef || result
      is VariableEntry) return null; return (Term)result; }`. The
      explicit cast `(Term)result` is the .NET counterpart of Dart's
      `as Term` (Microsoft Learn casts: explicit cast throws
      `InvalidCastException` on mismatch — same semantic as Dart's
      `as Term` which throws `TypeError` on mismatch). NULL-RETURN
      preserved as `Term?`.
    idiom_id: null
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      AS-VS-EXPLICIT-CAST nuance (explicitly addressed): Dart `as
      Term` and C# `(Term)result` BOTH throw on mismatch — the
      semantic counterpart. C# also offers `result as Term` which
      returns null on mismatch (Microsoft Learn `as` operator) —
      REJECTED here because Dart `as` throws, and the source's
      intent is "we KNOW it's a Term at this point; throw if not".
      The explicit cast preserves this contract. NULL-AS-UNBOUND
      nuance (LOAD-BEARING): `null` return is the load-bearing
      "unbound" sentinel; `Term?` preserves this contract. Async /
      Stream: ABSENT.

  - construct_key: dart.method.dereference_term_with_varref_chase
    source_form: >-
      `Term dereference(Term term) { if (term is VarRef) { final result = derefAddr(term.addr); if (result is VariableEntry) return term; if (result is VarRef) return result; return result as Term; } return term; }` — non-nullable Term return. If input is a VarRef, deref the address; if result is a VariableEntry (imported unbound), return the ORIGINAL term (load-bearing — caller keeps the VarRef handle); otherwise return the resolved term.
    target_decision: >-
      A C# method `public Term Dereference(Term term) { if (term is
      VarRef varRef) { var result = DerefAddr(varRef.Addr); if
      (result is VariableEntry) return term; if (result is VarRef
      resultVar) return resultVar; return (Term)result; } return
      term; }`. Pattern-match `is VarRef varRef` binds the typed
      reference; subsequent `is VarRef resultVar` binds a different
      local for the result. The "return original `term` for imported
      unbound" branch is preserved verbatim — load-bearing per the
      source comment ("Imported unbound - return original").
    idiom_id: null
    research_finding_id: rf-dart-nullable-int-return-with-type-test-to-csharp-pattern-match
    nuance: >-
      RETURN-ORIGINAL-FOR-IMPORTED-UNBOUND nuance (LOAD-BEARING,
      explicitly addressed): for imported-unbound (VariableEntry
      result), the method returns the ORIGINAL `term` (not a fresh
      VarRef) — preserves caller's VarRef handle identity. Codegen
      MUST preserve this. PATTERN-MATCH nuance: as above. AS-CAST
      nuance: explicit cast (same as `GetValue`). Async / Stream:
      ABSENT.

  - construct_key: dart.method.on_bind_callback_register_or_immediate_invoke
    source_form: >-
      `void onBind(int writerAddr, void Function(Term) callback) { if (isFullyBound(writerAddr)) { final value = getValue(writerAddr); if (value != null) callback(value); return; } _bindCallbacks[writerAddr] = callback; }` — if already bound, fire immediately; else register for later firing on binding.
    target_decision: >-
      A C# method `public void OnBind(int writerAddr, Action<Term>
      callback) { if (IsFullyBound(writerAddr)) { var value = GetValue(
      writerAddr); if (value != null) callback(value); return; }
      _bindCallbacks[writerAddr] = callback; }`. The `void
      Function(Term)` Dart parameter type -> `Action<Term>` .NET
      delegate (Microsoft Learn System.Action<T>). The indexer-set
      `_bindCallbacks[writerAddr] = callback` REPLACES any previous
      callback at that address (Dart `Map[k]=v` and .NET `Dictionary
      [k]=v` both replace silently — same semantic). If two callbacks
      need to be registered, the surrounding logic must chain them
      (not this method's concern).
    idiom_id: null
    research_finding_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    nuance: >-
      DELEGATE-VS-ACTION nuance: `void Function(Term)` -> `Action<
      Term>` (carry-forward from `HeapFCP` class construct).
      INDEXER-SET nuance: Dart `Map[k]=v` and .NET `Dictionary[k]=v`
      both replace silently (Microsoft Learn Dictionary<TKey,
      TValue>.Item[TKey] property — "If the specified key isn't
      found, attempting to retrieve it throws a KeyNotFoundException,
      and attempting to set it creates a new entry using the
      specified key"). Replace-without-warn semantic preserved.
      Single-subscriber-per-address contract preserved (NOT a multi-
      cast event). Async / Stream: ABSENT.

  - construct_key: dart.method.remove_bind_callback_simple_dictionary_remove
    source_form: "`void removeBindCallback(int writerAddr) { _bindCallbacks.remove(writerAddr); }` — simple delete; ignores the return value (existence)."
    target_decision: >-
      A C# method `public void RemoveBindCallback(int writerAddr) =>
      _bindCallbacks.Remove(writerAddr);` (expression-bodied; ignores
      the bool return). The non-out-overload `Dictionary.Remove(K)`
      returns `bool` (true if removed) — the .NET counterpart of
      Dart `Map.remove(K)` returning `V?`. Since the return value is
      discarded, either overload is acceptable; the simpler form is
      preferred.
    idiom_id: null
    research_finding_id: rf-dart-map-remove-and-invoke-to-csharp-dictionary-remove-out
    nuance: "DISCARD-RETURN nuance: source discards Dart Map.remove's `V?` return; .NET discards Dictionary.Remove's `bool` return. Either is faithful. Async / Stream: ABSENT."

  - construct_key: dart.bind_imported_reader.allocate_value_cell_and_repoint_reader
    source_form: >-
      `List<GoalRef> bindImportedReader(int readerAddr, Term value, VariableEntry entry) { ... validate; extract activations from entry.suspensions via _walkAndActivate; allocate a NEW value cell at HP++ holding the value (CellTag.ValueTag); rewrite cell.content = Pointer(valueCellAddr); return activations; }` — transforms an unbound imported reader into a bound imported reader. The reader's content goes from `VariableEntry` to `Pointer(valueCellAddr)` where the value cell is a freshly-allocated `ValueTag` cell. Comment notes `HP++` is critical to keep HP in sync with cells.length.
    target_decision: >-
      A C# method `public List<GoalRef> BindImportedReader(int
      readerAddr, Term value, VariableEntry entry) { var cell =
      Cells[readerAddr]; if (cell.Tag != CellTag.RoTag) throw new
      InvalidOperationException($"bindImportedReader called on non-
      reader cell at {readerAddr} (tag: {cell.Tag})"); if (cell
      .Content is not VariableEntry) throw new
      InvalidOperationException($"bindImportedReader called on
      reader without VariableEntry at {readerAddr}"); var activations
      = new List<GoalRef>(); if (entry.Suspensions != null)
      WalkAndActivate(entry.Suspensions, activations); int
      valueCellAddr = Hp++; Cells.Add(new HeapCell(value, CellTag
      .ValueTag)); cell.Content = new Pointer(valueCellAddr); return
      activations; }`. The order MUST be preserved: validate → walk
      old suspensions → allocate value cell → repoint reader. The
      `Hp++` + `Cells.Add` pair MUST be co-located (HP-cells-length
      sync invariant).
    idiom_id: null
    research_finding_id: rf-dart-shared-mutable-record-by-reference-to-csharp-class
    nuance: >-
      HP-CELLS-LENGTH-SYNC nuance (LOAD-BEARING, explicitly
      addressed): the source comment explicitly says "Use HP++ to
      keep HP in sync with cells.length" — these two pieces of state
      must be co-incremented. Codegen MUST keep `Hp++` immediately
      followed by `Cells.Add(...)` (and never reorder, never insert
      other allocations in between, never use a separate "reserved
      slot" pattern). Violating this invariant would corrupt every
      future address calculation. IMPORTED-READER-TRANSFORM nuance
      (LOAD-BEARING, explicitly addressed): the reader cell's
      content transitions from `VariableEntry` to `Pointer(value
      cell)` — this transition is the documented sentinel for "this
      reader is now bound" used by `IsImportedReader`, `IsReaderBound`,
      `GetReaderValue`. Reference identity of the value-cell
      `HeapCell` is load-bearing (`Cells[valueCellAddr]` MUST return
      the same object the `Pointer` points at). Inherits the
      reference-identity rationale. Exception-type carry-forward.
      Async / Stream: ABSENT.

  - construct_key: dart.compat_wrappers.bind_variable_family_dispatching_on_value_kind
    source_form: >-
      Several "compatibility wrappers" for gradual migration of callers: `bindVariable(int, Term) -> List<GoalRef>` (dispatches: if value is VarRef → bindWriterToReader (local reader) / bindWriterToWriter (writer — throws) / else fall through; else bindWriter), `bindVariableConst(int, Object?) -> ` wraps in ConstTerm, `bindVariableStruct(int, String, List<Term>) -> ` wraps in StructTerm, plus `bindWriterConst`, `bindWriterStruct`, `isWriterBound`, `valueOfWriter`, `isBound` — all delegating wrappers.
    target_decision: >-
      A set of C# wrapper methods preserving the same dispatch
      shape: `public List<GoalRef> BindVariable(int writerAddr, Term
      value) { if (value is VarRef varRef) { if (IsReader(varRef.Addr))
      return BindWriterToReader(writerAddr, varRef.Addr); if (IsWriter(
      varRef.Addr)) { BindWriterToWriter(writerAddr, varRef.Addr);
      return new List<GoalRef>(); /* unreachable: throws */ } } return
      BindWriter(writerAddr, value); }`. Similar dispatch for
      `BindVariableConst(int, object?)` -> `BindWriter(writerAddr, new
      ConstTerm(v))`, `BindVariableStruct(int, string, List<Term>)` ->
      `BindWriter(writerAddr, new StructTerm(functor, args))`, plus
      the `BindWriterConst` / `BindWriterStruct` / `IsWriterBound` /
      `ValueOfWriter` / `IsBound` wrappers (each a one-line delegate
      to the canonical method). These ARE part of the API surface —
      the doc-comments say "compatibility wrappers for gradual
      migration"; codegen MUST emit them all (not consolidate). NOT
      marked `[Obsolete]` — the Dart source has no `@deprecated`
      annotation; the convspec preserves the as-is API surface.
    idiom_id: null
    research_finding_id: rf-dart-compat-wrapper-methods-to-csharp-delegating-methods
    nuance: >-
      COMPAT-SURFACE nuance (explicitly addressed): these methods
      exist purely for caller-migration convenience; preserving them
      verbatim avoids a forced refactor of every caller. Codegen
      MUST emit each wrapper as a method (NOT consolidate into the
      canonical method). DISPATCH-VARREF nuance (explicitly
      addressed): the `value is VarRef → IsReader/IsWriter` cascade
      preserves the FCP variable-binding rules (local reader →
      writer-to-reader chain; writer → WxW violation → throw). NULL
      handling: `Object?` (Dart) -> `object?` (C# NRT) on
      `BindVariableConst` — `ConstTerm` accepts a nullable payload.
      Async / Stream: ABSENT.

  - construct_key: dart.reader_abstraction.is_reader_bound_get_reader_value_is_imported_reader_get_writer_for_reader_extensive_docs
    source_form: >-
      Four "reader abstraction" methods that work for both local and imported readers: `isReaderBound`, `getReaderValue`, `isImportedReader`, `getWriterForReader`. `isImportedReader` has a 30-line doc-comment with a markdown table summarising cell-structure-per-state. `isReaderBound` distinguishes (Pointer → WrtTag → check isFullyBound) from (Pointer → ValueTag → return true) from (VariableEntry → return false). `getReaderValue` symmetrically reads the value via the same dispatch. `getWriterForReader` is a documented alias (`tryWriterForReader`).
    target_decision: >-
      Four C# methods with the same dispatch structure:
      (a) `public bool IsReaderBound(int readerAddr) { var cell =
      Cells[readerAddr]; if (cell.Tag != CellTag.RoTag) return false;
      if (cell.Content is Pointer ptr) { var targetCell = Cells[ptr
      .TargetAddr]; if (targetCell.Tag == CellTag.WrtTag) return
      IsFullyBound(ptr.TargetAddr); if (targetCell.Tag == CellTag
      .ValueTag) return true; } return false; }`;
      (b) `public Term? GetReaderValue(int readerAddr) { var cell =
      Cells[readerAddr]; if (cell.Tag != CellTag.RoTag) return null;
      if (cell.Content is Pointer ptr) { var targetCell = Cells[ptr
      .TargetAddr]; if (targetCell.Tag == CellTag.WrtTag) return
      GetValue(ptr.TargetAddr); if (targetCell.Tag == CellTag
      .ValueTag) return (Term)targetCell.Content!; } return null; }`
      — the `targetCell.Content!` null-forgiving is safe because a
      `ValueTag` cell always has a non-null Term content (allocation
      invariant);
      (c) `public bool IsImportedReader(int readerAddr) { var cell =
      Cells[readerAddr]; if (cell.Tag != CellTag.RoTag) return false;
      if (cell.Content is VariableEntry) return true; if (cell.Content
      is Pointer ptr) return Cells[ptr.TargetAddr].Tag == CellTag
      .ValueTag; return false; }` — preserves the documented
      "imported-after-bind = Pointer→ValueTag" structural sentinel;
      (d) `public int? GetWriterForReader(int readerAddr) =>
      TryWriterForReader(readerAddr);` — expression-bodied alias.
      All four doc-comments PRESERVED VERBATIM as XML doc (the
      markdown table in `IsImportedReader` is a load-bearing API
      contract; codegen MUST keep it as `<remarks>` text).
    idiom_id: null
    research_finding_id: rf-dart-nullable-int-return-with-type-test-to-csharp-pattern-match
    nuance: >-
      STRUCTURAL-SENTINEL nuance (LOAD-BEARING, explicitly
      addressed): the documented per-state cell structure — `Unbound
      imported = VariableEntry`, `Bound imported = Pointer→ValueTag`,
      `Local (any) = Pointer→WrtTag` — is a load-bearing structural
      invariant; the markdown table in the source doc-comment is the
      reference doc for every caller. Codegen MUST preserve this
      table verbatim. PATTERN-MATCH nuance: same as above.
      NULL-FORGIVING nuance: `targetCell.Content!` is safe because
      the `ValueTag` invariant guarantees non-null content; NRT
      flow analysis can't statically prove it (the invariant is
      encoded only in the tag), so the `!` is required to suppress
      the NRT warning. ALIAS-METHOD nuance: `GetWriterForReader` is
      a documented alias to `TryWriterForReader` — the doc-comment
      says "use this instead of writerForReader when the reader
      might be imported"; codegen MUST emit both methods.
      Exception-type carry-forward. Async / Stream: ABSENT.

  - construct_key: dart.legacy_wrappers.get_suspensions_add_suspension_writer_content_indirection
    source_form: >-
      Two "Legacy" methods preserved for compatibility: `getSuspensions(int writerAddr) -> SuspensionListNode?` (reads `WriterContent.suspensions` or null) and `addSuspension(int writerAddr, SuspensionListNode node) -> void` (adds via WriterContent or promotes Pointer→WriterContent — same dispatch as `suspendOnWriter` but takes a pre-built node).
    target_decision: >-
      Two C# methods: `public SuspensionListNode? GetSuspensions(int
      writerAddr) { var cell = Cells[writerAddr]; if (cell.Content is
      WriterContent wc) return wc.Suspensions; return null; }` and
      `public void AddSuspension(int writerAddr, SuspensionListNode
      node) { var cell = Cells[writerAddr]; if (cell.Content is
      WriterContent wc) { node.Next = wc.Suspensions; wc.Suspensions
      = node; } else if (cell.Content is Pointer ptr) { cell.Content
      = new WriterContent(ptr.TargetAddr, node); } }`. Both labelled
      as `/* Legacy: ... */` in the XML doc (carry-forward of source
      doc-comment). Codegen MUST emit both — they ARE part of the
      API surface; consolidating with `SuspendOnWriter` would force
      a caller refactor.
    idiom_id: null
    research_finding_id: rf-dart-compat-wrapper-methods-to-csharp-delegating-methods
    nuance: >-
      LEGACY-SURFACE nuance (explicitly addressed): preserves the
      pre-WriterContent API for any caller still using it; codegen
      MUST emit. PATTERN-MATCH and PROMOTION nuance: same as
      `SuspendOnWriter`. Async / Stream: ABSENT.

  - construct_key: dart.store_term_on_heap.recursive_term_to_heap_with_per_variant_dispatch
    source_form: >-
      `int storeTermOnHeap(Term term)` — recursive term-storage helper per spec v2.16.3 §1.1 (Heap-Only Requirement). Dispatches on term variant: VarRef → return existing addr (already on heap); ConstTerm → allocate ValueTag cell, return addr; StructTerm → RECURSIVELY store each arg, build a new StructTerm with VarRef args, allocate ValueTag cell, return addr; MutualRefTerm → allocate ValueTag cell, return addr; ModuleTerm → allocate ValueTag cell, return addr; default → throw ArgumentError. The HP++/cells.add pairing recurs for every non-VarRef variant.
    target_decision: >-
      A C# method `public int StoreTermOnHeap(Term term)` body using
      C# pattern-match: `if (term is VarRef varRef) return varRef.Addr;
      if (term is ConstTerm constTerm) { int addr = Hp++; Cells.Add(
      new HeapCell(constTerm, CellTag.ValueTag)); return addr; } if
      (term is StructTerm structTerm) { var heapArgs = new List<Term>();
      foreach (var arg in structTerm.Args) { int argAddr =
      StoreTermOnHeap(arg); heapArgs.Add(new VarRef(argAddr)); } int
      addr = Hp++; Cells.Add(new HeapCell(new StructTerm(structTerm
      .Functor, heapArgs), CellTag.ValueTag)); return addr; } if
      (term is MutualRefTerm) { int addr = Hp++; Cells.Add(new
      HeapCell(term, CellTag.ValueTag)); return addr; } if (term is
      ModuleTerm) { int addr = Hp++; Cells.Add(new HeapCell(term,
      CellTag.ValueTag)); return addr; } throw new ArgumentException(
      $"Unknown term type: {term.GetType()}");`. Dart `ArgumentError`
      -> .NET `ArgumentException` (Microsoft Learn: ArgumentException
      "is thrown when one of the arguments provided to a method is
      not valid" — the documented .NET counterpart of Dart's
      `ArgumentError`). RECURSION preserved on `StructTerm.Args`.
      `term.GetType()` is the .NET counterpart of Dart's
      `term.runtimeType` (Microsoft Learn Object.GetType: "Returns
      the Type of the current instance"). The StructTerm.Args is
      `IReadOnlyList<Term>` per terms.dart.md — `foreach` over an
      `IReadOnlyList<T>` is fully supported (`IEnumerable<T>`
      inherited). The new `heapArgs` is a `List<Term>` which can be
      passed to the StructTerm constructor (whose parameter is
      `IReadOnlyList<Term>` per terms.dart.md — List<T> implements
      IReadOnlyList<T>).
    idiom_id: null
    research_finding_id: rf-dart-argumenterror-to-csharp-argumentexception
    nuance: >-
      EXCEPTION-TYPE nuance (explicitly addressed): Dart
      `ArgumentError` -> .NET `ArgumentException`. Microsoft Learn
      `ArgumentException` documents the contract as "thrown when one
      of the arguments provided to a method is not valid" — the
      semantic counterpart of Dart `ArgumentError`. Dart
      `term.runtimeType` -> .NET `term.GetType()` for diagnostic
      message. RECURSION nuance (LOAD-BEARING, explicitly addressed):
      `StructTerm` args are recursively stored — each becomes a
      VarRef in the new StructTerm; this preserves the spec-v2.16.3
      §1.1 "heap-only argument registers" invariant. Codegen MUST
      preserve the recursive structure exactly; flattening or
      iterative rewriting would change the heap-allocation order and
      break callers that rely on specific address layouts.
      LIST-MUTATION nuance: `heapArgs` is a mutable `List<Term>`
      built via `Add`; passed by reference to the new `StructTerm`
      constructor; per terms.dart.md the StructTerm constructor
      ALIASES (not defensively copies) the list, so the term and
      the local both point at the same underlying list — matches
      Dart semantics exactly. PATTERN-MATCH nuance: same as above.
      HP-CELLS-LENGTH-SYNC nuance: the Hp++ / Cells.Add pairing
      recurs for every non-VarRef variant; codegen MUST keep them
      co-located. Async / Stream: ABSENT.

conversion_units:
  - "namespace declaration mirroring lib/runtime/ per the workspace's pair-specific namespace convention; file-header XML doc carries the Dart library doc-comments (FCP Two-Cell Heap with Pointer Architecture, per heap-pointer-architecture-spec.md v3.0)"
  - "using directives: using <root>.Runtime; (covers Term hierarchy + SuspensionRecord/SuspensionListNode + GoalRef/GoalState/Pc/GoalId), using <root>.Multiagent; (covers VariableEntry), using System.Collections.Generic; (covers List<T>, Dictionary<TKey,TValue>, HashSet<T>), using System; (covers Action<T>, InvalidOperationException, ArgumentException)"
  - "public enum CellTag { WrtTag, RoTag, ValueTag } (three-member discriminator; SHOUTcase-acronymed names preserved verbatim; default int underlying type)"
  - "public class HeapCell (reference type; identity equality preserved — NOT a record/struct):"
  - "  - object? Content { get; set; } — mutable nullable sum-type slot (was Dart `dynamic`; holds one of null/Pointer/SuspensionListNode/Term/VariableEntry/WriterContent at runtime)"
  - "  - CellTag Tag { get; set; } — mutable tag (CellTag.WrtTag/RoTag/ValueTag)"
  - "  - HeapCell(object? content, CellTag tag) — single positional ctor"
  - "  - bool HasValue => Tag == CellTag.ValueTag (expression-bodied)"
  - "  - bool HasSuspensions => Content is WriterContent wc && wc.Suspensions != null (expression-bodied)"
  - "public sealed class Pointer (reference type; identity equality; NOT a record/record struct):"
  - "  - int TargetAddr { get; } — get-only auto-property (Dart `final int`)"
  - "  - Pointer(int targetAddr) — single positional ctor"
  - "  - override string ToString() => $\"Ptr({TargetAddr})\""
  - "public sealed class WriterContent (reference type; identity equality; NOT a record/record struct):"
  - "  - int ReaderAddr { get; } — get-only auto-property (Dart `final int readerAddr`)"
  - "  - SuspensionListNode? Suspensions { get; set; } — mutable nullable head of suspension chain (Dart `SuspensionListNode? suspensions`)"
  - "  - WriterContent(int readerAddr, SuspensionListNode? suspensions = null) — single ctor with default-null optional positional"
  - "  - override string ToString() => $\"WriterContent(reader={ReaderAddr}, sus={Suspensions?.ToString() ?? \\\"null\\\"})\""
  - "public class HeapFCP (reference type; identity equality; NOT a record/struct; the per-isolate runtime state container):"
  - "  - public List<HeapCell> Cells { get; } = new() — get-only list reference; contents mutated"
  - "  - public int Hp { get; set; } = 0 — heap pointer; mutated in every allocator (WAM spec name preserved as `Hp`)"
  - "  - private readonly Dictionary<int, Action<Term>> _bindCallbacks = new() — keyed by writerAddr"
  - "  - (int WriterAddr, int ReaderAddr) AllocateVariable() — ValueTuple return; allocates two paired cells; HP += 2"
  - "  - int AllocateImportedReader() / AllocateImportedWriter() — single-cell allocators with null content; HP++"
  - "  - bool IsWriter(int addr) / IsReader(int addr) / IsValue(int addr) — bounds-checked tag predicates (expression-bodied)"
  - "  - int? TryWriterForReader(int readerAddr) — nullable; null for imported/non-reader (extensive doc-comment preserved as XML doc)"
  - "  - int? ReaderForWriter(int writerAddr) — bidirectional-pointer-verified; null for bound writers"
  - "  - int PairedReaderAddr(int writerAddr) — falls back to writerAddr+1 by allocation invariant"
  - "  - object DerefAddr(int startAddr) — large method with HashSet<int> visited tracking, CellTag? previousTag for WxW detection, switch over Tag with three arms"
  - "  - List<GoalRef> BindWriter(int, Term) / BindWriterNoCallback(int, Term) — delegating wrappers to..."
  - "  - List<GoalRef> BindWriterWithCallbackControl(int writerAddr, Term value, bool fireCallback) — in-place cell mutation (Tag → ValueTag, Content → value); walks WriterContent suspensions; optional callback fire via Dictionary.Remove(out var)"
  - "  - void FirePendingCallback(int writerAddr) — Dictionary.Remove(out var callback) then invoke if value bound"
  - "  - List<GoalRef> BindWriterToReader(int writerAddr, int readerAddr) — Tag REMAINS WrtTag; Content → Pointer(readerAddr); forwards suspensions; relocates callback"
  - "  - void BindWriterToWriter(int w1, int w2) => throw new InvalidOperationException(...) — always-throws WxW violation"
  - "  - void SuspendOnWriter(int writerAddr, SuspensionRecord record) — three-branch: WriterContent (cons) / Pointer (promote to WriterContent) / else throw"
  - "  - void SuspendOnReader(int readerAddr, SuspensionRecord record) — two-branch: VariableEntry (mutate entry.Suspensions chain) / Pointer (delegate to SuspendOnWriter)"
  - "  - private void ForwardSuspensions(SuspensionListNode? list, int targetWriterAddr) — walks armed; clones WRAPPER but SHARES record; prepends to target's WriterContent (promoting Pointer→WriterContent if needed); silently ignores bound targets"
  - "  - private static void WalkAndActivate(SuspensionListNode? list, List<GoalRef> activations) — walks armed; constructs GoalRef from goalId/resumePC; calls record.Disarm() to propagate"
  - "  - bool IsFullyBound(int writerAddr) — deref-and-test (NOT VarRef && NOT VariableEntry)"
  - "  - Term? GetValue(int writerAddr) — nullable; deref-and-cast"
  - "  - Term Dereference(Term term) — VarRef-chase; returns original term for imported unbound (LOAD-BEARING — preserves caller's VarRef handle)"
  - "  - void OnBind(int writerAddr, Action<Term> callback) — immediate-fire if bound, else register"
  - "  - void RemoveBindCallback(int writerAddr)"
  - "  - List<GoalRef> BindImportedReader(int readerAddr, Term value, VariableEntry entry) — transforms unbound imported reader (Content=VariableEntry) to bound (Content=Pointer→ValueTag cell); Hp++/Cells.Add co-located"
  - "  - List<GoalRef> BindVariable / BindVariableConst / BindVariableStruct + BindWriterConst / BindWriterStruct / IsWriterBound / ValueOfWriter / IsBound — compatibility wrappers preserved verbatim"
  - "  - bool IsReaderBound / Term? GetReaderValue / bool IsImportedReader / int? GetWriterForReader — reader abstraction; doc-comment markdown table on IsImportedReader preserved as XML doc <remarks>"
  - "  - SuspensionListNode? GetSuspensions / void AddSuspension — legacy wrappers (preserved per source comment)"
  - "  - int StoreTermOnHeap(Term term) — recursive per-variant dispatch: VarRef (no-op), ConstTerm/MutualRefTerm/ModuleTerm (allocate ValueTag), StructTerm (recurse on args then allocate ValueTag with VarRef args), default → throw ArgumentException"

escalations:
  - kind: undecidable
    construct_key: dart.heap_fcp.concurrency_model_thread_safety_for_multiagent_hosting
    detail: >-
      Dart `HeapFCP` is owned by exactly one isolate (Dart's single-
      threaded event-loop model — see Dart concurrency docs:
      https://dart.dev/language/concurrency). The source has NO
      `lock`, NO `Interlocked`, NO `volatile`, NO `async` — every
      mutation (`Hp++`, `Cells.Add`, `cell.Content = ...`,
      `_bindCallbacks[w] = cb`) is non-atomic by Dart's single-
      threaded contract. The .NET re-host's threading model for the
      multiagent runtime is NOT decided in THIS file — the per-
      MadContext / per-agent isolation pattern is defined in
      `lib/multiagent/global_writers_table.dart.md` and
      `lib/multiagent/variable_table.dart.md`, which delegate the
      concrete mechanism (actor-mailbox / pinned-thread / single-
      threaded-scheduler) to the future `isolate_manager` port. As
      long as each `HeapFCP` is owned by exactly one OS thread or
      Task scheduler (per the isolate-manager contract), the plain
      mutable C# implementation in this spec is CORRECT and SAFE.
      However, IF a future hosting decision shares a single
      `HeapFCP` across multiple threads, the entire mutation surface
      becomes a data-race source — `Hp++` (read-modify-write on a
      shared int), `Cells.Add` (List<T> is NOT thread-safe per
      Microsoft Learn), `cell.Content = X` (writes to a heap-shared
      object), and `_bindCallbacks` mutations all require either
      external synchronisation, `Interlocked.Increment(ref _hp)`,
      or replacement with `ConcurrentDictionary` / `ConcurrentBag`
      / explicit `lock` regions — none of which are in scope for
      THIS file's spec.
    needs: >-
      An authoritative decision on the .NET hosting concurrency
      model for HeapFCP. Options (each with a downstream spec
      change): (A) preserve Dart's single-owner-thread invariant
      via the multiagent isolate-manager port (RECOMMENDED — the
      current spec is correct under this assumption); (B) introduce
      external `lock` synchronisation around every HeapFCP mutation
      (would require an additional `private readonly object _lock`
      field and lock acquisitions wrapping every public method);
      (C) replace mutable internals with concurrent primitives
      (`Interlocked.Increment` for Hp, `ConcurrentDictionary` for
      `_bindCallbacks`, `List<HeapCell>` replaced by a `ConcurrentBag`
      or a custom growable list — would change snapshot/iteration
      semantics and require re-verification of every method
      against the new memory model). Note: this escalation is NOT
      about the cell/term/suspension types (which are correctly
      reference types per the per-construct decisions above); it
      is specifically about the `HeapFCP` owner's threading
      contract.
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-library-directive-to-csharp-namespace-elision — file-header directive (cache hit)

Same authoritative finding already used in `suspension.dart.md`,
`variable_table.dart.md`, `cells.dart.md` (FR-024 cache hit; not
re-researched). `library;` directive has no .NET counterpart;
doc-comments migrate to namespace-level XML doc.

### rf-dart-import-relative-to-csharp-using-namespace — package imports (cache hit)

Same authoritative finding already used in `variable_table.dart.md`.
Dart `import 'package:.../*.dart'` -> .NET `using <namespace>;`. The
`show` clause has no .NET parallel (carry-forward from
`goal_queue.dart.md` rf-dart-export-directive-to-csharp-using-alias)
— .NET `using` imports full namespace surface.

### rf-dart-plain-enum-to-csharp-enum — CellTag (cache hit + casing nuance)

Same authoritative finding already used in `cells.dart.md`,
`machine_state.dart.md`, `opcodes.dart.md` (FR-024 cache hit). Dart
plain enum -> C# plain enum, 1:1; declaration order preserved.
ADDITIONAL DECISION (NOT a new research call): the
SHOUTcase-acronymed member spellings `WrtTag`/`RoTag`/`ValueTag` are
PRESERVED VERBATIM per the spec-string-fidelity precedent already
applied in `cells.dart.md` (lowercase `writer`/`reader` preserved
for the same reason) and `opcodes.dart.md`. The spec name lookup
(heap-pointer-architecture-spec.md uses these exact strings) is
the load-bearing reason; this is a casing-preservation policy
already established in those convspecs.

### rf-dart-shared-mutable-record-by-reference-to-csharp-class — HeapCell / WriterContent / heap-mutation methods (cache hit)

Authoritative finding already used in `suspension.dart.md` (the
canonical source for the rationale) and `variable_table.dart.md`.
The FR-012 cache hit is exact: the FCP heap-cell sum-type holder is
the SAME shared-mutable-record-by-reference pattern — one `HeapCell`
per heap address, mutated in-place, with multiple observers
(pointers, the cells list, the runner) holding the same reference.
The decisive Microsoft Learn citation is the
`reference-types`/`record`/`struct` triumvirate documenting that
(a) `class` preserves identity equality + shared mutation, (b)
`record` injects value equality, (c) `struct`/`record struct` copies
on assignment. All three rejections apply to `HeapCell`,
`WriterContent`, and the `HeapFCP` master class. Inherited from
suspension.dart.md verbatim.

### rf-dart-sumleaf-no-eq-to-csharp-class-no-record — Pointer (cache hit + boxing nuance)

Same authoritative finding already used in `terms.dart.md`
(`ConstTerm`, `StructTerm`, `ModuleTerm`). The Dart source carries
no `==` override on `Pointer`, so the C# target must preserve
reference identity (NOT `record class`). ADDITIONAL ANALYSIS
(spec-only — no new research call): `Pointer` is a TEMPTING
`readonly record struct` candidate (one immutable int field,
heavily allocated), parallel to `GoalRef` in
`machine_state.dart.md`. The rejection reasoning is fully covered
by the existing `terms.dart.md` rationale (no `==` override = no
value equality) PLUS the boxing concern specific to this file:
`Pointer` is stored as `HeapCell.Content` (typed `object?`), so a
`record struct` would box on every assignment and unbox on every
type-test — defeating the value-type allocation benefit AND
introducing copies on the read path. The reference-class decision
in `terms.dart.md` for the symmetric `ConstTerm`/`StructTerm`/
`ModuleTerm` cases (also stored in cell-like contexts) confirms
this rejection.

### rf-dart-mutable-state-class-identity-equality-to-csharp-class — HeapFCP (cache hit + concurrency-escalation)

Same authoritative finding already used in
`machine_state.dart.md` (GoalState / GoalQueue) and
`global_writers_table.dart.md` /  `variable_table.dart.md`. The
authoritative .NET citation is the classes-vs-records distinction
in Microsoft Learn (cited verbatim in `machine_state.dart.md`):
`class` for reference identity + mutable state; `record` for
immutable value-equality bundle; `struct` for no-allocation small
value-type bundle. `HeapFCP` is mutable AND identity-equal AND
held by reference — `class` is the only correct mapping.
Concurrency model is escalated separately (escalations[0]).

### rf-dart-record-return-to-csharp-valuetuple — allocateVariable tuple

- **Deep analysis.** Dart 3 records `(int, int)` are anonymous
  structural value types used as method return types
  (`(writerAddr, readerAddr)`). The `allocateVariable` method
  is the canonical FCP two-cell allocator and returns BOTH
  addresses; callers destructure as `final (wid, rid) =
  heap.allocateVariable();`.
- **Authoritative Dart.** https://dart.dev/language/records — Dart
  official: records are "anonymous, immutable, aggregate types"
  with positional/named fields and structural equality.
- **Authoritative .NET.** https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-tuples
  — Microsoft Learn: `ValueTuple` is the canonical .NET multi-
  value return; "tuple types are value types" with positional /
  named fields and structural equality. The named-component
  syntax `(int WriterAddr, int ReaderAddr)` enables call-site
  destructuring `var (wid, rid) = heap.AllocateVariable();` —
  byte-for-byte semantic equivalent to Dart record destructuring.
- **Why not `Tuple<int, int>`.** `Tuple<T1, T2>` is a reference
  type (allocates per call) and semantically mismatches Dart's
  value-type records.
- **Why not a named `record struct`.** Over-translation — the
  source uses an anonymous record literal, the .NET counterpart
  is also anonymous via ValueTuple.
- **Decision.** Anonymous `(int WriterAddr, int ReaderAddr)`
  ValueTuple return. Authoritative both sides; no escalation
  on this construct (the heap concurrency escalation is
  separate).

### rf-dart-postincrement-and-method-shape-to-csharp-equivalent — allocators (trivial semantic equivalent)

Dart `HP++` and C# `Hp++` are byte-for-byte equivalent postfix-
increment operators per their respective language references
(https://dart.dev/language/operators and Microsoft Learn postfix
increment operator). No new research; semantic equivalent.

### rf-dart-boolean-predicate-short-circuit-to-csharp-equivalent — IsWriter/IsReader/IsValue (trivial)

Dart `&&` and C# `&&` both short-circuit lazily left-to-right
(Microsoft Learn conditional logical operators). Dart `cells
.length` -> C# `Cells.Count` (the canonical collection-count
property name per .NET design guidelines; `Length` is for arrays/
strings). Expression-bodied form per Microsoft Learn expression-
bodied members.

### rf-dart-nullable-int-return-with-type-test-to-csharp-pattern-match — TryWriterForReader / ReaderForWriter / DerefAddr / reader-abstraction methods

- **Deep analysis.** Multiple methods in this file return nullable
  int (`int?`) or chase through type-tested cell contents. The
  Dart pattern `content is X` followed by `(content as X).field`
  is a two-step type-test-then-cast.
- **Authoritative Dart.** https://dart.dev/language/operators —
  Dart official: `is` is the type-test operator; `as` is the
  type-cast operator (throws TypeError on mismatch). Two separate
  operations.
- **Authoritative .NET.** https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/pattern-matching
  — Microsoft Learn: pattern matching with `is Type identifier`
  ("type pattern with designation") is the C#-canonical single-
  step that combines type test + binding into one statement.
  Equivalent to Dart `is X` + `as X` two-step but cleaner.
- **Decision.** Use C# pattern-match `is X identifier` everywhere
  the source uses `is X` + `as X`. The `int?` return type
  preserves Dart `int?` exactly under NRT. The negated form
  `is not X` (C# 9, Microsoft Learn pattern matching) is the
  counterpart of Dart `is!`.

### rf-dart-nullable-int-fallback-to-csharp-equivalent — PairedReaderAddr (trivial)

Either `if (x != null) return x.Value; return fallback;` or the
null-coalescing operator `??` (Microsoft Learn) is correct; source
uses the explicit `if` form, preserved at codegen discretion.

### rf-dart-staterror-to-csharp-invalidoperationexception — exception type (load-bearing, used by every throw site)

- **Deep analysis.** Every throw site in this file uses Dart
  `StateError` to signal "the object is in an inappropriate
  state" (cycle detection, WxW violation, bind-on-non-writer,
  bind-imported-on-non-reader, suspend-on-non-writer,
  unexpected-content, WxW always-throw).
- **Authoritative Dart.** https://api.dart.dev/stable/dart-core/StateError-class.html
  — Dart official: "Thrown when an operation cannot be performed
  because the object is in an inappropriate state."
- **Authoritative .NET.** https://learn.microsoft.com/dotnet/api/system.invalidoperationexception
  — Microsoft Learn: "The exception that is thrown when a method
  call is invalid for the object's current state." Byte-for-byte
  semantic equivalent.
- **Decision.** Every `StateError` -> `InvalidOperationException`.
  Authoritative; no escalation. CRITICAL: do NOT map to
  `Exception` (too broad) or `ArgumentException` (semantic
  mismatch — arguments are not the problem; object state is).

### rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults — bindWriterWithCallbackControl (cache hit)

Same authoritative finding already used in `machine_state.dart.md`
(GoalState constructor) and `variable_table.dart.md` (VariableEntry
constructor). Dart `{required bool fireCallback}` -> .NET non-
optional positional `bool fireCallback`; callers use named-argument
call-site syntax (`fireCallback: true`).

### rf-dart-map-remove-and-invoke-to-csharp-dictionary-remove-out — firePendingCallback / bindWriter / bindWriterToReader (load-bearing)

- **Deep analysis.** Three sites use the Dart pattern `final cb =
  _bindCallbacks.remove(writerAddr); if (cb != null) ...` — the
  Dart `Map.remove(key)` returns the removed value or null
  ATOMICALLY. The semantic is "remove and get in one step".
- **Authoritative Dart.** https://api.dart.dev/stable/dart-core/Map/remove.html
  — Dart official: "Removes key and its associated value, if
  present, from the map. Returns the value associated with key
  before it was removed."
- **Authoritative .NET.** https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.remove
  — Microsoft Learn: the `Dictionary<TKey,TValue>.Remove(TKey,
  TValue)` overload (available since .NET Core 2.0) "removes the
  value with the specified key from the Dictionary<TKey,TValue>,
  and copies the element to the value parameter." This is the
  atomic remove-and-get counterpart.
- **Why not `ContainsKey` + indexer + `Remove`.** Two-step,
  pays double lookup, and races in any concurrent re-host.
- **Why not single-arg `Remove(K)`.** Returns only `bool`,
  loses the value — would require a separate indexer lookup
  BEFORE the remove, two-step + double-lookup.
- **Decision.** Use `if (_bindCallbacks.Remove(writerAddr, out
  var callback)) callback(value);` everywhere the source uses
  the remove-and-get pattern. Authoritative both sides; no
  escalation.

### rf-dart-as-cast-to-csharp-explicit-cast — getValue / dereference (trivial)

Dart `as X` and C# `(X)expr` BOTH throw on mismatch (Dart
`TypeError`, .NET `InvalidCastException`). Semantic equivalent.
Microsoft Learn casts: explicit cast throws on failure.

### rf-dart-is-not-type-test-to-csharp-is-not-pattern — isFullyBound / suspendOnReader (trivial)

Dart `is!` -> C# `is not` (C# 9 negated type pattern, Microsoft
Learn pattern matching). Semantic equivalent.

### rf-dart-compat-wrapper-methods-to-csharp-delegating-methods — compatibility wrappers (preservation)

- **Deep analysis.** The "Compatibility Methods" section
  (`bindVariable` / `bindVariableConst` / `bindVariableStruct` /
  `bindWriterConst` / `bindWriterStruct` / `isWriterBound` /
  `valueOfWriter` / `isBound`) exists for "gradual migration of
  callers" per the section header. Removing them would force a
  caller refactor.
- **Decision.** Preserve every wrapper as a delegating method.
  Not marked `[Obsolete]` (Dart source has no `@deprecated`
  annotation). Spec-only decision; no new research.

### rf-dart-argumenterror-to-csharp-argumentexception — storeTermOnHeap default arm

- **Authoritative Dart.** https://api.dart.dev/stable/dart-core/ArgumentError-class.html
  — Dart official: "An error thrown when a function is passed an
  unacceptable argument."
- **Authoritative .NET.** https://learn.microsoft.com/dotnet/api/system.argumentexception
  — Microsoft Learn: "The exception that is thrown when one of
  the arguments provided to a method is not valid." Byte-for-byte
  semantic counterpart.
- **Decision.** `throw new ArgumentException($"Unknown term
  type: {term.GetType()}");` — Dart `term.runtimeType` -> .NET
  `term.GetType()` (Microsoft Learn `Object.GetType`).

## Notes — well-known nuances explicitly addressed (FR-009 / US2 AS4)

- **REFERENCE IDENTITY (LOAD-BEARING, addressed across every
  cell-like type)**: `HeapCell`, `Pointer`, `WriterContent`, and
  `HeapFCP` itself are ALL reference-type C# `class`es. Records /
  record-classes / structs / record-structs are categorically
  REJECTED for every one of these types. Every cell IS its
  address; mutation in-place must propagate to every observer.
  This is the foundational invariant of the FCP two-cell heap
  with pointer architecture. See per-construct rejection lists
  for `HeapCell`, `Pointer`, `WriterContent`, `HeapFCP` above.
- **In-place mutation (LOAD-BEARING, addressed)**: `bindWriter`,
  `bindWriterToReader`, `bindImportedReader`, `suspendOnWriter`,
  `suspendOnReader`, `_forwardSuspensions`, `_walkAndActivate` all
  mutate cell content / tag / suspension chains in place. Every
  mutator preserves the Dart shape verbatim; reference-typed cell
  classes guarantee mutation propagation across all aliases.
- **Shared-mutable-record via WriterContent (LOAD-BEARING,
  addressed)**: when a writer accumulates suspensions, the cell
  content is promoted from a bare `Pointer` to a compound
  `WriterContent` that preserves both the reader back-pointer
  AND the suspension head. This compound is the FCP §2.3 design
  for preserving `readerForWriter` even with suspensions present.
  The .NET port preserves `WriterContent` as a reference-typed
  class so the suspension-head mutation propagates to every
  observer.
- **Disarm propagation via shared SuspensionRecord (LOAD-BEARING,
  inherited from suspension.dart.md)**: `_walkAndActivate` and
  `_forwardSuspensions` rely on multiple `SuspensionListNode`
  wrappers pointing at the SAME `SuspensionRecord` instance —
  `record.disarm()` mutates the shared record's `goalId` to
  null, causing every wrapper's `armed` getter to return false on
  subsequent walks. Codegen MUST preserve this — `new
  SuspensionListNode(current.Record)` shares the record by
  reference; cloning the record would catastrophically break
  double-activation prevention.
- **Null-safety (addressed)**: every Dart nullable maps to a
  .NET nullable; every Dart non-nullable maps to a .NET non-
  nullable under enabled NRT. Specifically: `HeapCell.content`
  is `object?` (the sum slot can be null for freshly-allocated
  imported cells); `WriterContent.suspensions` is nullable
  (head of chain); `tryWriterForReader` / `readerForWriter`
  return `int?` (nullable for "no local writer"); `getValue` /
  `getReaderValue` return `Term?` (nullable for unbound);
  callbacks are `Action<Term>` (non-nullable delegate); every
  other field is non-nullable.
- **Dynamic-vs-object (LOAD-BEARING, addressed)**: Dart's
  `dynamic` content slot is mapped to .NET `object?` (NOT C#
  `dynamic` which uses the DLR and pays per-access dispatch
  cost). Pattern-match (`is X identifier`) replaces every Dart
  `is X` + `as X` two-step. Out-of-character `dynamic` usage
  would change semantics (silent runtime errors that would
  compile but fail at execution) — REJECTED.
- **Exception types (LOAD-BEARING, addressed)**: `StateError` ->
  `InvalidOperationException` (every internal-state-violation
  throw); `ArgumentError` -> `ArgumentException` (the storeTermOnHeap
  default arm). NEVER mapped to `Exception` (too broad) or to
  `NotImplementedException` (semantic mismatch).
- **Pattern matching (addressed)**: every Dart `is X` + `as X`
  two-step becomes C# `is X identifier` pattern-match; every
  `is! X` becomes `is not X` (C# 9 negated pattern).
- **Trail / choice-points / WAM-style backtracking**: ABSENT
  from this file. `HeapFCP` does NOT carry a trail or choice-
  point chain — the runtime keeps those structures separately
  (the runner / bytecode-runner layer, NOT here). The .NET port
  MUST NOT introduce trail/choice-point fields. Correctly not
  asserted.
- **Concurrency model (ESCALATED — see escalations[0])**:
  `HeapFCP` is owned by exactly one isolate in Dart; the .NET
  re-host's threading contract is delegated to the future
  `isolate_manager` port (per multiagent specs). The current
  spec is correct under the single-owner-thread assumption; if
  a future hosting decision shares HeapFCP across threads, the
  entire mutation surface needs re-evaluation. Escalated as
  `kind: undecidable` for clarity (not a blocker — the spec is
  authoritatively correct under the documented assumption; the
  escalation marks the assumption explicitly so future re-hosts
  do not silently violate it).
- **Async / Stream / Future / Completer / IAsyncEnumerable**:
  ABSENT. Every method in this file is synchronous. Callbacks
  are synchronous `Action<Term>`. The .NET port MUST NOT
  introduce `Task` / `async` / `IAsyncEnumerable` /
  `Channel<T>` anywhere.
- **Mixin / sealed (base hierarchy)**: ABSENT on the type
  definitions in this file (HeapCell, Pointer, WriterContent,
  HeapFCP are plain classes; only `Pointer` and `WriterContent`
  are `sealed` defensively at the leaf-class level per the
  no-Dart-subclass observation).
- **Identifier casing**: Dart `camelCase` member names become
  `PascalCase` per .NET capitalisation conventions; private
  fields keep underscore-prefix (`_bindCallbacks`); private
  methods drop the underscore (`ForwardSuspensions`,
  `WalkAndActivate`). `HP` is renamed to `Hp` (PascalCase per
  acronym guideline) preserving spec-name correspondence.
  Enum members SHOUTcase-acronymed and preserved VERBATIM
  (`WrtTag`/`RoTag`/`ValueTag`) per the spec-string-fidelity
  precedent.
- **Doc-comment fidelity (LOAD-BEARING for tryWriterForReader
  and isImportedReader)**: two methods carry extensive doc-
  comments that are load-bearing API contracts — the
  `tryWriterForReader` doc enumerates the three caller modes
  and warns against common mistakes; the `isImportedReader`
  doc carries a markdown table summarising the per-state cell
  structure. Codegen MUST preserve these as XML doc
  `<summary>` / `<remarks>` / `<example>` tags verbatim.
  Shortening would silently drop contract documentation that
  prevents real bugs.
- **HP-cells-length-sync invariant (LOAD-BEARING, addressed)**:
  every `Hp++` MUST be immediately followed by `Cells.Add(...)`
  — these two operations are atomic at the source level and
  represent "allocate one cell". Codegen MUST keep them co-
  located. Violating this would corrupt every future address
  calculation.
- **Single ZERO escalation case (concurrency model)**: every
  other non-trivial construct in this file is resolvable from
  authoritative Dart and .NET official documentation. The
  one escalation (concurrency model) is documented as
  `kind: undecidable` because the .NET hosting concurrency
  contract is outside this file's scope; it belongs to the
  multiagent isolate-manager design.
