---
path: lib/compiler/result.dart
cycle_group_id: 41
scc_siblings: []
generated_at: 2026-05-21T16:18:47Z
source_sha256: 87c7c24334491b7377c4f653e30d53401b140240f08e1c765ad4d074d650e8c2
schema_version: 1
---

# Conversion Plan: lib/compiler/result.dart

## 1. Source Analysis

Inspection of `glp_runtime_net/lib/compiler/result.dart` (10 lines, sha256
`87c7c24334491b7377c4f653e30d53401b140240f08e1c765ad4d074d650e8c2`):

- Line 1: `import 'package:glp_runtime/bytecode/runner.dart' show BytecodeProgram;`
  — single `package:`-prefixed import targeting the same Dart package
  (`glp_runtime`); narrowed via Dart `show` to the single type
  `BytecodeProgram`. That type is referenced exactly once, as the declared
  type of the `program` field on line 5.
- Line 3: `/// Result of compilation including bytecode and metadata` —
  Dart triple-slash doc comment immediately above the class.
- Line 4: `class CompilationResult {` — plain library-public Dart class;
  no inheritance, no interface implementation, no generics on the class,
  no `==`/`hashCode` override, no `toString` override, no static members,
  no methods beyond the constructor.
- Line 5: `final BytecodeProgram program;` — non-nullable `final`
  instance field of reference type `BytecodeProgram`.
- Line 6: `final Map<String, int> variableMap;  // Variable name -> register index`
  — non-nullable `final` instance field of type `Map<String, int>`
  (interface backed by `LinkedHashMap`), with an inline `//` line
  comment on the same line documenting the map semantic.
- Line 8: `CompilationResult(this.program, this.variableMap);` —
  single positional constructor using Dart initialising-formals
  (`this.x`) for both fields; no body, no defaults, no named
  parameters, no `assert`s, no factory variants.
- Line 9: `}` — class close.

No async / `Stream` / `Future` / isolate, no `late`, no `mixin`, no
`extension`, no nullable types, no arithmetic / bitwise on `int`,
no iteration of `variableMap` in this file, no `==` / `hashCode` /
`toString` override, no inheritance, no interface, no static
members, no method other than the constructor.

## 2. Dart → C#/.NET Conversion Plan

Per-construct mapping (mirrors the ratified convspec exactly):

- **`import 'package:glp_runtime/bytecode/runner.dart' show BytecodeProgram;`** → C# `using <root>.Bytecode;` — a single `using` directive naming the
  namespace that hosts the converted `runner.cs` (the source of
  `BytecodeProgram`). The Dart `show BytecodeProgram` per-symbol
  narrowing is DROPPED because C# `using <Namespace>;` has no per-symbol
  allow-list (cached idiom
  `rf-dart-import-show-clause-no-csharp-counterpart`). `using static
  BytecodeProgram` is NOT emitted — that imports type *members*, not the
  type itself, so it is not a valid `show`-clause counterpart for a TYPE
  import. The dropped `show` is a one-way coarsening (`<root>.Bytecode`'s
  full public surface becomes accessible in `result.cs`); it preserves
  observable behaviour because `show` was a code-hygiene affordance, not
  a load-bearing semantic.

- **`/// Result of compilation including bytecode and metadata`** → C# XML-doc `/// <summary>Result of compilation including bytecode and
  metadata</summary>` immediately above the class declaration. Trivial.

- **`class CompilationResult { … }`** → C# reference `class CompilationResult` (NOT a `record`, NOT a `struct`).
  - `record` is REJECTED: it would synthesise structural value-equality
    from all positional properties, but the Dart source has no `==`
    override and therefore inherits default `Object` identity
    equality — a `record` would silently change equality semantics.
  - `struct` is REJECTED: instances are produced once by the compiler
    and flow downstream by reference; aliasing across pipeline stages
    is observable and would be broken by per-pass defensive copies of
    a value type.
  - Cached idiom `rf-dart-final-field-class-to-csharp-getonly-class`.

- **`final BytecodeProgram program;`** → C# `public BytecodeProgram Program { get; }` — non-nullable get-only
  auto-property. NOT a `readonly` field (properties preserve the
  public field-access surface Dart exposes via the `result.program`
  getter shape). Initialised once in the constructor. Shallow
  immutability preserved: the property reference cannot be reassigned,
  but mutation through `Program`'s own surface remains legal in both
  languages.

- **`final Map<String, int> variableMap;`** → C# `public Dictionary<string, long> VariableMap { get; }` —
  non-nullable get-only auto-property. Two cached sub-idioms compose:
  - `rf-dart-map-to-csharp-dictionary`: `Map<K,V>` → `Dictionary<K,V>`
    (concrete `Dictionary`, not `IDictionary` — matches the indexer-only
    consumption pattern). Iteration-order delta (LinkedHashMap stable
    order vs Dictionary undefined order) is latent; this file never
    iterates the map. Missing-key delta (Dart `[]` → `V?` vs C#
    `Dictionary[]` throws `KeyNotFoundException`) is recorded as
    cross-file guidance for the call-site consumers in
    `compiler/*.cs` and `bytecode/runner.cs` (TryGetValue/ContainsKey
    migration); it does not bind THIS declaration file.
  - `rf-dart-int-to-csharp-long-width`: Dart `int` (64-bit) → C# `long`
    (System.Int64). Register indices fit Int32 in practice but the
    spec default is type-faithful 64-bit to avoid silent narrowing.

- **`// Variable name -> register index`** (inline on line 6) → preserved as a C# `//` line comment adjacent to the
  `VariableMap` property declaration. Default = preserve as `//`
  (byte-identical shape) per the convention from `error.dart.md`;
  promotion to `<summary>` is not chosen here.

- **`CompilationResult(this.program, this.variableMap);`** → C# positional constructor:
  ```
  public CompilationResult(BytecodeProgram program,
                           Dictionary<string, long> variableMap)
  {
      Program = program;
      VariableMap = variableMap;
  }
  ```
  Initialising-formal sugar (`this.x`) is expanded to explicit
  assignments — C# has no equivalent sugar. Primary constructors are
  DELIBERATELY NOT used (project-wide convention recorded in
  `token.dart.md` `rf-dart-final-field-class-to-csharp-getonly-class`
  prefers get-only auto-properties for `final` field carry-overs).
  Parameter order mirrors Dart (`program`, then `variableMap`).
  Non-nullable parameters (NRT context); callers cannot pass `null`
  without an explicit `null!` override, matching Dart's non-nullable
  formals.

Resulting file shape (single file `lib/compiler/result.cs`):

- Namespace declaration `<root>.Compiler` (the namespace that hosts
  converted `compiler/*.cs` files; matches the cross-file namespace
  decision recorded in `rf-dart-import-relative-to-csharp-using-namespace`).
- One `using <root>.Bytecode;` directive.
- One `public class CompilationResult` with:
  - XML-doc `<summary>` on the class.
  - `public BytecodeProgram Program { get; }`
  - `public Dictionary<string, long> VariableMap { get; }` (with the
    inline `//` comment retained).
  - One positional constructor as above.

Non-applicable axes (deliberately not asserted): async / `Stream` /
`Future` / isolate (synchronous data container); `late` /
`mixin` / `extension` / generics-with-bounds (none present);
`sealed` / `abstract` / inheritance / interface (none present);
arithmetic / bitwise / overflow (no operations on `int`);
`==` / `hashCode` / `toString` override (none present).

## 3. Decomposed Task Units

- T1: Emit namespace declaration `<root>.Compiler` and a single `using <root>.Bytecode;` directive (drop Dart `show` clause per `rf-dart-import-show-clause-no-csharp-counterpart`). One-line done.
- T2: Emit XML-doc `/// <summary>Result of compilation including bytecode and metadata</summary>` immediately above the class. One-line done.
- T3: Emit `public class CompilationResult` (reference type, default identity equality; NOT record, NOT struct). One-line done.
- T4: Emit `public BytecodeProgram Program { get; }` get-only auto-property (non-nullable). One-line done.
- T5: Emit `public Dictionary<string, long> VariableMap { get; }` get-only auto-property (non-nullable) and preserve the inline `// Variable name -> register index` `//` comment adjacent to it. One-line done.
- T6: Emit positional constructor `public CompilationResult(BytecodeProgram program, Dictionary<string, long> variableMap)` whose body assigns `Program = program;` and `VariableMap = variableMap;`. One-line done.

## 4. Research Findings

none required — all four cached idioms (`rf-dart-import-show-clause-no-csharp-counterpart`, `rf-dart-final-field-class-to-csharp-getonly-class`, `rf-dart-map-to-csharp-dictionary`, `rf-dart-int-to-csharp-long-width`) carry forward verbatim from the ratified convspec and are reused without new research (FR-024 cache hit at every construct, per convspec Notes).

## 5. Consistency Pass

- T1 import/using mapping — fixed; derived from convspec construct `dart.import_directive.package_with_show_clause_bytecode_program` (research_finding_id `rf-dart-import-show-clause-no-csharp-counterpart`).
- T2 doc-comment mapping — fixed; derived from convspec construct `dart.docblock_triple_slash` (trivial).
- T3 class shape (reference `class`, no record, no struct, identity equality) — fixed; derived from convspec construct `dart.data_class.immutable_two_final_fields_positional_ctor` (research_finding_id `rf-dart-final-field-class-to-csharp-getonly-class`).
- T4 `Program` property — fixed; derived from the same convspec construct (Dart `final BytecodeProgram program` → C# get-only auto-property), namespace for `BytecodeProgram` shared with T1's `using` decision.
- T5 `VariableMap` property + inline comment — fixed; derived from convspec constructs `dart.map_string_int.variable_index_lookup` (research_finding_id `rf-dart-map-to-csharp-dictionary`, composed with `rf-dart-int-to-csharp-long-width`) and `dart.line_comment.inline_after_field_declaration` (trivial, preserve-as-`//` per `error.dart.md` convention).
- T6 positional constructor — fixed; derived from convspec construct `dart.data_class.immutable_two_final_fields_positional_ctor` (initialising-formal sugar expanded to explicit assignments; primary constructors deliberately not used per cached idiom).

## 6. Escalations

None.
