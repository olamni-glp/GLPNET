# Conversion Spec — lib/compiler/result.dart

> Conversion-spec artifact for lib/compiler/result.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/compiler/result.dart
source_sha256: 87c7c24334491b7377c4f653e30d53401b140240f08e1c765ad4d074d650e8c2
target_code_unit: lib/compiler/result.cs
constructs:
  - construct_key: dart.import_directive.package_with_show_clause_bytecode_program
    source_form: >-
      "import 'package:glp_runtime/bytecode/runner.dart' show BytecodeProgram;"
      — single `package:`-prefixed import using Dart `show` to narrow the
      imported surface from `glp_runtime/bytecode/runner.dart` to the single
      type `BytecodeProgram`. `BytecodeProgram` is referenced once as the
      declared type of the `program` field.
    target_decision: >-
      Emit one C# `using <root>.Bytecode;` directive — the namespace that
      hosts the converted `runner.cs` (the source of `BytecodeProgram`).
      This is the same namespace decision recorded in `bytecode/runner.dart.md`
      (`rf-dart-import-relative-to-csharp-using-namespace`) and reused across
      runtime/*.dart specs. The Dart `show BytecodeProgram` per-symbol
      narrowing has NO faithful C# counterpart — `using <Namespace>;` imports
      the full public surface of the namespace, and C# has no per-symbol
      allow-list at the import level. The spec records "show clauses are
      dropped" as the established convention (carry-forward from
      `rf-dart-import-show-clause-no-csharp-counterpart`, originated in
      runtime/heap_fcp.dart.md and runtime.dart.md). Codegen MUST NOT attempt
      to emit `using static BytecodeProgram` — that imports type *members*,
      not the type itself, and the Dart `show` here narrows a TYPE import.
    idiom_id: null
    research_finding_id: rf-dart-import-show-clause-no-csharp-counterpart
    nuance: >-
      Show-clause nuance (explicitly addressed): Dart `import '…' show A;`
      narrows the imported library's exposed surface to the named symbol(s)
      at THIS compilation unit. C# has no per-symbol `using` narrowing —
      the faithful render is a bare `using <Namespace>;` and the per-symbol
      filter is dropped. This is a one-way coarsening: any *other* public
      symbol from `<root>.Bytecode` becomes accessible in the converted
      `result.cs` too, where the Dart source restricted it. The `show` was a
      code-hygiene affordance, not a load-bearing semantic — dropping it
      preserves observable behaviour. Package-vs-relative nuance: a Dart
      `package:` import targeting the same Dart package as the importing
      file (here `glp_runtime` importing from `package:glp_runtime/…`) is
      semantically identical to a relative import after .NET conversion,
      because both ultimately resolve to the same C# namespace
      `<root>.Bytecode`. Value-vs-reference / null-safety / async: NOT
      APPLICABLE to import directives.
  - construct_key: dart.data_class.immutable_two_final_fields_positional_ctor
    source_form: >-
      "class CompilationResult { final BytecodeProgram program; final
      Map<String, int> variableMap; CompilationResult(this.program,
      this.variableMap); }" — a plain Dart class with two `final` instance
      fields and a single positional constructor using initialising-formals
      (`this.program`, `this.variableMap`). No `==`/`hashCode` override
      (default reference equality), no `toString` override, no named
      parameters, no defaults, no methods, no inheritance, no interface
      implementation, no generics on the class itself.
    target_decision: >-
      Emit a C# reference `class CompilationResult` (NOT a `record`, NOT a
      `struct`) with two get-only auto-properties initialised from a single
      positional constructor mirroring Dart parameter order:
      `CompilationResult(BytecodeProgram program, Dictionary<string, long>
      variableMap)`. The constructor body assigns `Program` and `VariableMap`
      from the parameters (Dart's initialising-formal sugar `this.x`
      expanded to explicit assignments — C# has no equivalent sugar; primary
      constructors are deliberately NOT used because the project-wide
      convention recorded in `token.dart.md`
      (`rf-dart-final-field-class-to-csharp-getonly-class`) prefers
      get-only auto-properties for `final` field carry-overs). A `record`
      is REJECTED: `record` synthesises structural value-equality from all
      positional properties, but `CompilationResult` MUST keep default
      reference equality so that two distinct compilation outputs holding
      structurally-equal `BytecodeProgram`/`variableMap` payloads remain
      distinguishable by identity (matches the Dart source, which inherits
      default `Object` identity equality from not overriding `==`). A
      `struct` is REJECTED: instances are produced once by the compiler and
      held by reference downstream (consumers cache and pass them between
      stages); a value-type would force defensive copies and conflate
      identity. Privacy: no `_`-prefixed members in the Dart source; the
      class and both fields are library-public → C# `public` class with
      `public` get-only properties.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Immutability nuance (explicitly addressed): Dart `final` instance
      fields are write-once (assigned in the constructor's
      initialising-formal list, never mutated after construction) and map
      to C# get-only auto-properties (no setter, NOT `readonly` *fields* —
      properties preserve the public field-access surface Dart exposes via
      the `result.program` / `result.variableMap` getter shape). The
      referenced VALUES are still mutable: `BytecodeProgram` is a reference
      object whose internal state may change after `CompilationResult`
      construction (Dart `final` is shallow), and `Map<String, int>` /
      `Dictionary<string, long>` is a mutable map — Dart `final` only
      prevents the *binding* from being reassigned, not the map contents
      from being modified. This shallow-immutability semantic is preserved
      exactly in C# get-only auto-properties: the property cannot be
      reassigned, but `result.VariableMap[k] = v` and any mutation through
      `Program`'s surface remain legal in both languages. Reference-vs-value
      nuance: `CompilationResult` MUST remain a reference `class` (see
      target_decision rejection of `struct` and `record`); identity-based
      equality and aliasing are preserved across the compiler pipeline.
      Null-safety nuance: both fields are non-nullable in the Dart source
      (no `?` annotation on `BytecodeProgram` or `Map<String, int>`); they
      map to non-nullable C# property/parameter types under an enabled NRT
      context. The constructor cannot be called with `null` for either
      parameter without an explicit `null!` override at the caller — this
      matches the Dart behaviour (passing `null` to a non-nullable formal
      is a compile error). No `Stream`/`Future`/async/isolate concerns
      (synchronous data container).
  - construct_key: dart.map_string_int.variable_index_lookup
    source_form: >-
      "final Map<String, int> variableMap;  // Variable name -> register
      index" — a `Map<String, int>` field mapping variable-name keys to
      integer register indices, populated by the compiler and read by the
      bytecode runner to resolve variable references. Allocation site is
      external (the constructor receives a fully-populated map).
    target_decision: >-
      Map Dart `Map<String, int>` → C# `Dictionary<string, long>`. Two
      sub-decisions, both carrying forward established idioms:
        (1) `Map<K,V>` → `Dictionary<K,V>` per
            `rf-dart-map-to-csharp-dictionary` (cached idiom; first
            recorded in `machine_state.dart.md`, reused across runtime/*
            and compiler/* specs). Dart `Map` is an interface backed by
            `LinkedHashMap` (insertion-ordered) by default;
            `Dictionary<TKey,TValue>` is the .NET-idiomatic counterpart
            for keyed lookup. The Dart source NEVER iterates `variableMap`
            in insertion order (it is consumed purely by-key lookup —
            `variableMap[name]` to fetch a register index), so the loss
            of LinkedHashMap's stable iteration order is observably
            irrelevant; this matches the analysis recorded in the
            `system_predicates.dart.md` registry idiom. If a future
            caller iterates `variableMap.entries` and depends on the
            order, that caller's spec must escalate.
        (2) `int` (Dart, 64-bit native) → C# `long` (System.Int64) per
            `rf-dart-int-to-csharp-long-width` (cached idiom; first
            recorded in `token.dart.md`, reused across compiler/* specs).
            Register indices fit `int` (Int32) in practice (no program
            has 2^31 variables), but the SPEC decision is the
            type-faithful mapping `long` so the baseline never silently
            narrows Dart 64-bit semantics. A future codegen pass MAY
            down-map register indices to `int` with a recorded
            justification (e.g. interop with a runner API that takes
            `Dictionary<string,int>`); absent that, default is `long`.
    idiom_id: null
    research_finding_id: rf-dart-map-to-csharp-dictionary
    nuance: >-
      Two-axis nuance (explicitly addressed). Map-vs-Dictionary axis:
      Dart `Map<K,V>` is an *interface* (`dart:core` library; default
      implementation `LinkedHashMap` preserves insertion order); C#
      `Dictionary<TKey,TValue>` is a *concrete generic class*
      (`System.Collections.Generic`). The faithful 1:1 is `Dictionary`
      (not `IDictionary` — the Dart source field is the concrete map
      surface, used with indexer `[]` access; codegen MAY abstract to
      `IDictionary<string,long>` at the property type if call-sites
      benefit, but the default per the cached idiom is the concrete
      `Dictionary`). Iteration-order delta: `LinkedHashMap` preserves
      insertion order; `Dictionary` does NOT guarantee enumeration order
      (Microsoft Learn: "The order in which the items are returned is
      undefined"). This file does not iterate the map, so the delta is
      latent, not active — but the cross-file decision is recorded:
      enumeration-order-sensitive callers must escalate per
      `rf-dart-map-to-csharp-dictionary`. Integer-width axis (load-bearing
      per project convention): Dart `int` (native) is 64-bit signed
      (-2^63..2^63-1); C# `int` is 32-bit. Default mapping is `long` to
      preserve Dart's 64-bit value range; see `token.dart.md`
      `rf-dart-int-to-csharp-long-width` for the authoritative basis and
      down-map escape. Value-vs-reference: `string` (Dart `String`) is a
      reference type with value-equality in both languages — appropriate
      dictionary key. `long` is a value type — boxing occurs ONLY if the
      dictionary value is read into an `object` slot, which does not
      happen in this file. Null-safety: neither type parameter is
      nullable in the Dart source (`Map<String, int>`, not
      `Map<String?, int?>`); under enabled NRT the C# counterpart is
      `Dictionary<string, long>` (non-nullable string key, non-nullable
      long value). A missing-key lookup in Dart returns `null` (because
      `Map.operator[]` returns `V?`); in C#
      `Dictionary<string,long>.this[string]` THROWS
      `KeyNotFoundException` for a missing key — this is a divergence
      the converted call-sites of `variableMap[name]` must address (use
      `TryGetValue` for the Dart-equivalent "nullable return"
      semantic). The divergence is RECORDED here as cross-file
      guidance; the call-sites are in `compiler/*.dart` and
      `bytecode/runner.dart` and their specs already record the
      `TryGetValue`/`ContainsKey` migration. Async / Stream / isolate /
      late / mixin: NOT APPLICABLE.
  - construct_key: dart.docblock_triple_slash
    source_form: >-
      "/// Result of compilation including bytecode and metadata" — a
      single Dart triple-slash doc comment immediately above the class
      declaration.
    target_decision: >-
      Map to a C# XML-doc comment
      `/// <summary>Result of compilation including bytecode and
      metadata</summary>` immediately above the class declaration.
      Trivial mechanical mapping.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
  - construct_key: dart.line_comment.inline_after_field_declaration
    source_form: >-
      "final Map<String, int> variableMap;  // Variable name -> register
      index" — single-line `//` comment on the same line as the field
      declaration, documenting the map's semantic.
    target_decision: >-
      Preserve as a C# `//` line comment adjacent to the property
      declaration (or, optionally, promote to an XML-doc `<summary>` on
      the property — spec default = preserve as `//` for byte-identical
      shape, matching the convention in `error.dart.md`). Trivial.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
conversion_units:
  - "using <root>.Bytecode; (single using directive; show-clause dropped per rf-dart-import-show-clause-no-csharp-counterpart)"
  - "class CompilationResult (reference type, default identity equality, NOT a record, NOT a struct)"
  - "property: get-only BytecodeProgram Program (non-nullable, Dart final → C# get-only auto-property)"
  - "property: get-only Dictionary<string, long> VariableMap (non-nullable; Map<String,int> → Dictionary<string,long> per rf-dart-map-to-csharp-dictionary + rf-dart-int-to-csharp-long-width)"
  - "constructor: CompilationResult(BytecodeProgram program, Dictionary<string, long> variableMap) — positional, assigns Program and VariableMap"
  - "doc-comment → /// <summary>Result of compilation including bytecode and metadata</summary> on the class"
  - "inline // comment on VariableMap property preserved"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-import-show-clause-no-csharp-counterpart — `package:`-prefixed import with `show` clause (cached idiom, reuse)

- Deep analysis: the source's only `import` is
  `import 'package:glp_runtime/bytecode/runner.dart' show BytecodeProgram;`
  — a `package:`-prefixed import targeting the same Dart package
  (`glp_runtime`) that contains this file, narrowed to the single type
  `BytecodeProgram` via a Dart `show` clause. The narrowed symbol is
  consumed exactly once, as the declared type of the `program` field.
- Provenance: cached idiom first recorded in `runtime/heap_fcp.dart.md`
  and elaborated in `runtime/runtime.dart.md`'s
  `dart.import_directive.package_with_show_clause_*` constructs. The
  authoritative bases were established there:
    - Dart official: `package:`-prefixed imports resolve to libraries
      inside the named Dart package; `show` narrows the imported surface
      at the compilation unit (Dart language tour: "import directives").
    - .NET official: `using <Namespace>;` is name-only namespace import
      with no per-symbol allow-list (Microsoft Learn: `using` directive
      imports an entire namespace's public surface). `using static
      <Type>;` imports *members* of a type, not the type itself, so it
      is NOT a `show`-clause counterpart for type narrowing.
- Conclusion: emit a bare `using <root>.Bytecode;` (the namespace
  hosting the converted `runner.cs`); drop the `show BytecodeProgram`
  filter per the established convention. The carry-forward decision
  also harmonises the `using` set for the converted `result.cs` with
  every other compiler/* and runtime/* file that imports
  `BytecodeProgram`: a single shared namespace, no per-file narrowing
  divergence. FR-024 cache hit; no new research required.

### rf-dart-final-field-class-to-csharp-getonly-class — immutable two-field data class (cached idiom, reuse)

- Deep analysis: `class CompilationResult` is a synchronous data
  container with two `final` instance fields (`program`,
  `variableMap`) initialised by a single positional constructor using
  initialising-formals. No methods, no equality overrides, no
  inheritance, no generics on the class itself. The Dart source
  surfaces the fields as read-after-construct accessors; consumers
  use `result.program` and `result.variableMap`.
- Provenance: cached idiom first recorded in `compiler/token.dart.md`
  (`dart.data_class.immutable_final_fields_positional_ctor_with_optional_positional`)
  and reused across compiler/* specs. The authoritative bases were
  established there:
    - Dart official: Dart `final` instance fields are write-once at
      construction; initialising-formals (`this.x`) are syntactic
      sugar for "assign argument to field" (Dart language tour:
      "Classes" → constructor initialising formals).
    - .NET official: C# get-only auto-properties (`public T Name {
      get; }` with constructor assignment) provide write-once
      semantics with public field-access surface (Microsoft Learn:
      "Auto-implemented properties").
- Why NOT `record`: `record` synthesises structural value-equality
  from all positional properties. `CompilationResult` inherits
  default `Object` reference identity from Dart (no `==` override);
  a `record` would silently introduce structural equality where the
  Dart source has identity equality — a behaviour change. Records
  are appropriate when the source class overrides `==`/`hashCode`
  to compare fields; this class does not. (Same posture as the
  `token.dart.md` decision for `Token`, which also rejects `record`
  but for a different concrete reason — explicit field-by-field
  equality override. Here the rejection is identity-vs-structural;
  in both cases `record` is wrong.)
- Why NOT `struct`: instances are produced once by the compiler and
  flow downstream by reference. Aliasing (the same
  `CompilationResult` consumed by multiple pipeline stages) is
  observable and would be broken by per-pass defensive copies of a
  value type. (Same posture as the `token.dart.md` decision for
  `Token`.)
- Conclusion: emit a reference `class CompilationResult` with two
  `public` get-only auto-properties (`Program`, `VariableMap`)
  initialised by a single positional constructor; Dart's
  initialising-formal sugar is expanded to explicit assignments in
  the body. Authoritative both sides; no escalation.

### rf-dart-map-to-csharp-dictionary — `Map<String, int>` field (cached idiom, reuse)

- Deep analysis: the `variableMap` field stores variable-name →
  register-index entries populated by the compiler and consumed by
  the bytecode runner via keyed lookup (`variableMap[name]`). No
  ordered iteration over the map appears in this file; the map is a
  pure keyed-lookup table.
- Provenance: cached idiom first recorded in
  `runtime/machine_state.dart.md`
  (`dart.map_field.sigma_hat_mutable_map`) and reused across
  runtime/* and compiler/* specs (`runtime/body_kernels.dart.md`,
  `runtime/system_predicates.dart.md`, `compiler/parser.dart.md`,
  `compiler/pmt/type_table.dart.md`, etc.). The authoritative bases
  were established there:
    - Dart official: `Map<K,V>` is the keyed-collection interface
      in `dart:core`; the default `Map()` literal yields a
      `LinkedHashMap` preserving insertion order (api.dart.dev:
      `Map` class).
    - .NET official: `Dictionary<TKey,TValue>`
      (`System.Collections.Generic`) is the .NET-idiomatic
      keyed-lookup collection; Microsoft Learn documents that
      enumeration order is undefined ("the order in which the items
      are returned is undefined").
- Iteration-order delta: latent, not active in this file. The map is
  consumed purely by-key. The cross-file rule (callers that depend
  on insertion order must escalate or migrate to an
  insertion-ordered .NET collection) is recorded in the idiom and
  re-stated here for completeness.
- Missing-key semantics delta: load-bearing for downstream callers.
  Dart `Map.operator[]` returns `V?` (nullable; `null` for missing
  key — api.dart.dev `Map`). C# `Dictionary<TKey,TValue>.this[key]`
  THROWS `KeyNotFoundException` for missing keys (Microsoft Learn).
  Faithful per-call-site migration uses `TryGetValue(key, out var
  v)` or `ContainsKey(key)`; this is recorded as cross-file
  guidance for the consumers of `variableMap` in
  `compiler/*.dart`/`bytecode/runner.dart`.
- Conclusion: `Map<String, int>` → `Dictionary<string, long>` per
  the cached idiom, combined with `rf-dart-int-to-csharp-long-width`
  for the value-type axis (see next entry). Authoritative both sides;
  no escalation in THIS file.

### rf-dart-int-to-csharp-long-width — `int` value type in the map (cached idiom, reuse)

- Deep analysis: the map's value type is Dart `int`, used to store
  register indices. No arithmetic or bitwise ops appear in this file
  — values are written once (by the compiler that constructs the
  `CompilationResult`) and read by the runner. No overflow path is
  exercised here.
- Provenance: cached idiom first recorded in
  `compiler/token.dart.md`
  (`dart.int.fixed_width_source_position_field`) and reused across
  compiler/* specs. The authoritative bases were established there:
    - Dart official: native `int` is 64-bit signed two's complement
      (api.dart.dev `int` class: range -2^63..2^63-1).
    - .NET official: C# `int` is `System.Int32` (32-bit), C# `long`
      is `System.Int64` (64-bit) (Microsoft Learn: integral numeric
      types).
- Conclusion: Dart `int` → C# `long` by default to preserve 64-bit
  range. Down-mapping to `int` is permitted only with a per-field
  recorded justification (none here). Authoritative; no escalation.

## Notes

- No Stream/Future/async, no isolates, no `late`, no `mixin`, no
  `extension`, no generics-with-bounds, no `sealed`/`abstract`
  classes, no bitwise/shift, no `==`/`hashCode` override, no
  `toString` override, no inheritance, no interface implementation,
  no static members, no nullable fields. The well-known nuances
  ABSENT from this file are deliberately not asserted.
- The conversion is entirely composed of cached idioms (FR-012 / SC-007):
  `rf-dart-import-show-clause-no-csharp-counterpart`,
  `rf-dart-final-field-class-to-csharp-getonly-class`,
  `rf-dart-map-to-csharp-dictionary`,
  `rf-dart-int-to-csharp-long-width`. No new research was required
  (FR-024 cache hit at every construct). No escalation —
  every construct has an authoritative-supported decision basis.
- Cross-file divergence recorded (not blocking THIS file): the
  Dart `Map.operator[] → V?` vs C#
  `Dictionary<TKey,TValue>.this[key] → throws KeyNotFoundException`
  difference applies to the consumers of `variableMap` (call-sites
  in `compiler/*.dart` and `bytecode/runner.dart`), not to this
  declaration file. Those consumer specs record the
  `TryGetValue`/`ContainsKey` migration.
