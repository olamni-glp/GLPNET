---
path: lib/compiler/pmt/type_table.dart
cycle_group_id: 58
scc_siblings: []
generated_at: 2026-05-21T15:19:52Z
source_sha256: fecf3a38722602da8b1ac6e1a3459b739c37c0c02c91588b335e30ba0d6ce74a
schema_version: 1
---

# Conversion Plan: lib/compiler/pmt/type_table.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/compiler/pmt/type_table.dart` (96 lines, sha256 `fecf3a38722602da8b1ac6e1a3459b739c37c0c02c91588b335e30ba0d6ce74a`):

- File header (lines 1–3): triple-slash doc-comment "Type table for Moded Type definitions / Stores user-defined type definitions parsed from GLP source."
- Single relative import (line 5): `import '../ast.dart';` — pulls `TypeDefinition`, `TypeConstructor`, and `Module` (with `module.typeDefinitions`) into scope.
- One class declared (line 8): `class TypeTable`, non-sealed, no inheritance, no interface implementation.
- Backing state (line 9): `final Map<String, TypeDefinition> _types = {};` — single private field, Dart leading-underscore privacy, reference-final but value-mutable, initialised to the empty `LinkedHashMap` literal.
- Explicit no-arg constructor (line 11): `TypeTable();` — empty body.
- Mutating method `addDefinition(TypeDefinition def)` (lines 15–30) — map lookup `_types[def.typeName]`, null-check branch, merge path rebuilds `TypeDefinition` with `existing.typeParams`/`existing.line`/`existing.column` and the spread-concatenated constructor list `[...existing.constructors, ...def.constructors]`; else path is bare indexer upsert `_types[def.typeName] = def;`.
- Mutating method `addConstructor(String typeName, TypeConstructor ctor, {List<String>? typeParams, int line = 0, int col = 0})` (lines 33–52) — named-parameter tail with one nullable-no-default and two int-with-literal-zero defaults; lookup-then-branch identical to `addDefinition`; merge appends a single ctor; else constructs a new `TypeDefinition` with `typeParams ?? []` (fresh-empty-list fallback), a single-element constructor list `[ctor]`, and the supplied `line`/`col`.
- Lookup method `TypeDefinition? getType(String name) => _types[name];` (line 55) — expression-bodied, takes a parameter (NOT a getter), returns the indexer result directly as nullable.
- Predicate `bool hasType(String name) => _types.containsKey(name);` (line 58) — expression-bodied, parameterised.
- Four parameterless getters (lines 61–70): `Iterable<String> get typeNames => _types.keys;`, `Iterable<TypeDefinition> get definitions => _types.values;`, `int get length => _types.length;`, `bool get isEmpty => _types.isEmpty;`.
- Two static factories (lines 73–84): `static TypeTable fromDefinitions(List<TypeDefinition> defs)` — constructs an empty table and iterates `defs` calling `addDefinition` (order-preserving `for (final def in defs)`); `static TypeTable fromModule(Module module) => fromDefinitions(module.typeDefinitions);` — delegates to `fromDefinitions`.
- `@override String toString()` (lines 87–94) — builds a multi-line string with `StringBuffer('TypeTable(\n')`, one `writeln('  $def')` per `_types.values` entry, trailing `write(')')`, returns `buffer.toString()`. Hard-coded `\n` LF, not `Platform.lineTerminator`.
- No async, no Stream/Future, no isolates, no late, no records, no factory constructors, no inheritance, no operator overloads, no extensions.

## 2. Dart → C#/.NET Conversion Plan

Each Dart construct → its C#/.NET counterpart (mirroring the ratified convspec verbatim):

**(C1) `class TypeTable` with private final map field and default ctor** → Non-sealed reference-type `class TypeTable` with one private instance field `private readonly Dictionary<string, TypeDefinition> _types = new(StringComparer.Ordinal);` (lowercase-underscore name preserved — Dart leading-underscore privacy → C# `private`; `readonly` because reference is final but contents mutate; `StringComparer.Ordinal` mandatory so type-name lookup is byte-exact and not culture-sensitive, matching `type_ast.dart`'s established discipline). The explicit `TypeTable()` Dart constructor maps to the implicit C# default constructor (no body, no parameters) — codegen MAY emit an empty explicit ctor; the implicit form is equivalent. Class is `class` (NOT `record`, NOT `struct`) because state is reference-typed and mutable.

**(C2) `void addDefinition(TypeDefinition def)` — map-lookup-then-branch upsert with merge** → `public void AddDefinition(TypeDefinition def)`. Body uses `_types.TryGetValue(def.TypeName, out var existing)` followed by `if (existing is not null) { ... } else { ... }`. The naïve transliteration `var existing = _types[def.TypeName]; if (existing != null) {...}` is REJECTED because C# `Dictionary<,>.Item[TKey]` getter throws `KeyNotFoundException` on miss (Microsoft Learn) — silent semantic regression. Merge branch: `var mergedConstructors = existing.Constructors.Concat(def.Constructors).ToList();` followed by `_types[def.TypeName] = new TypeDefinition(def.TypeName, existing.TypeParams, mergedConstructors, existing.Line, existing.Column);` — preserves first-definition-wins for `TypeParams`/`Line`/`Column`. Else branch: `_types[def.TypeName] = def;` (indexer-assignment upsert, NOT `.Add`, for uniformity with the merge branch and parity with Dart map-indexer semantics).

**(C3) `void addConstructor(String typeName, TypeConstructor ctor, {List<String>? typeParams, int line = 0, int col = 0})` — named/default-parameter upsert** → `public void AddConstructor(string typeName, TypeConstructor ctor, IReadOnlyList<string>? typeParams = null, int line = 0, int col = 0)`. Dart named tail `{List<String>? typeParams, int line = 0, int col = 0}` maps to C# optional parameters with identical defaults (`null`, `0`, `0`). The `typeParams` default MUST be `null` (not `new List<string>()` — Microsoft Learn rules out non-constant default expressions). Body: `TryGetValue(typeName, out var existing)`; merge branch builds `existing.Constructors.Append(ctor).ToList()` (LINQ `Append` is the documented single-element concat) and rebuilds `TypeDefinition` with `existing.TypeParams`/`existing.Line`/`existing.Column`; else branch constructs `new TypeDefinition(typeName, typeParams ?? new List<string>(), new List<TypeConstructor> { ctor }, line, col)` — the `typeParams ?? new List<string>()` MUST allocate fresh on each call (Dart `[]` is expression-position, not a shared constant). Call sites passing named args (`addConstructor("Foo", c, line: 10)`) map unchanged to C# named arguments.

**(C4) `TypeDefinition? getType(String name) => _types[name];`** → `public TypeDefinition? LookupType(string name) => _types.GetValueOrDefault(name);` — METHOD (not property, has a parameter). RENAMED `getType`→`LookupType` per the project-wide getX→LookupX conversion-idiom (Gabi 2026-05-23, idiom KB): a `GetType(string)` would shadow `object.GetType()`; rather than hiding with `new` (superseded decision), the collision is removed at the name. Callers `typeTable.getType(...)` → `_typeTable.LookupType(...)`. Body uses `GetValueOrDefault` (Microsoft Learn, `CollectionExtensions.GetValueOrDefault`: returns the value or the default for the type), which yields `null` on miss for reference type `TypeDefinition` — exact match for Dart `_types[name]`. Alternative permitted form `return _types.TryGetValue(name, out var def) ? def : null;` is semantically equivalent.

**(C5) `bool hasType(String name) => _types.containsKey(name);`** → `public bool HasType(string name) => _types.ContainsKey(name);` — direct 1:1 mapping (Microsoft Learn, `Dictionary<TKey,TValue>.ContainsKey`). Expression-bodied form preserved. O(1) hash lookup in both languages, identical semantics.

**(C6) `Iterable<String> get typeNames => _types.keys;` and `Iterable<TypeDefinition> get definitions => _types.values;`** → Two get-only instance properties: `public IEnumerable<string> TypeNames => _types.Keys;` and `public IEnumerable<TypeDefinition> Definitions => _types.Values;`. `Dictionary.Keys` / `.Values` return live views (Microsoft Learn) — same view semantics as Dart `Map.keys` / `Map.values`, NOT snapshots. Exposed as `IEnumerable<T>` to narrow the surface; `.ToList()` would break view parity and is forbidden.

**(C7) `int get length => _types.length;`** → `public int Length => _types.Count;` — get-only int-valued property. Name `Length` PRESERVED VERBATIM (FR-023 / FR-024) rather than renamed to the .NET-idiomatic `Count` — principled deviation. `Dictionary.Count` is O(1) (Microsoft Learn).

**(C8) `bool get isEmpty => _types.isEmpty;`** → `public bool IsEmpty => _types.Count == 0;`. C# `Dictionary<,>` exposes no native `IsEmpty` (Microsoft Learn member list — only `Count`, `Keys`, `Values`, `Comparer`); the documented idiom for emptiness is `Count == 0`. LINQ `Any()` is REJECTED (allocates enumerator; less efficient; not idiomatic on `IDictionary`). Property name `IsEmpty` preserved verbatim from Dart.

**(C9) `static TypeTable fromDefinitions(List<TypeDefinition> defs)`** → `public static TypeTable FromDefinitions(IReadOnlyList<TypeDefinition> defs)`. Input typed as `IReadOnlyList<TypeDefinition>` to widen the call surface without weakening behaviour (the method only iterates). Body: `var table = new TypeTable(); foreach (var def in defs) table.AddDefinition(def); return table;`. `foreach` over `defs` is order-preserving and deterministic — required because `AddDefinition` is order-sensitive (first occurrence wins for params/line/col). A parallel/unordered fold is REJECTED.

**(C10) `static TypeTable fromModule(Module module) => fromDefinitions(module.typeDefinitions);`** → `public static TypeTable FromModule(Module module) => FromDefinitions(module.TypeDefinitions);` — expression-bodied static method delegating to `FromDefinitions`. Cross-file symbol naming (`Module.TypeDefinitions` vs the divergent `Module.typeDefs` observed in current `ast.dart`) is RESOLVED BY `ast.dart` / `type_ast.dart`'s own specs at codegen-stitch time. The conversion of THIS file is unambiguous given the source as written — recorded informationally, NOT escalated.

**(C11) `@override String toString()` with `StringBuffer` and `\n` newlines** → `public override string ToString()` using `System.Text.StringBuilder`. Logical shape: open with `var sb = new StringBuilder("TypeTable(\n");`, iterate `foreach (var def in _types.Values) sb.Append($"  {def}\n");`, close with `sb.Append(')'); return sb.ToString();`. CRITICAL: use `Append("...\n")` (NOT `AppendLine`) so the output is byte-identical to Dart across all OSes. `StringBuilder.AppendLine` writes `Environment.NewLine` (`\r\n` on Windows, Microsoft Learn) — would diverge from Dart's hard-coded `\n` (Dart `StringSink.writeln` always emits LF) and silently break golden-string comparisons. Interpolated `$"  {def}"` invokes virtual `def.ToString()` polymorphically, matching Dart `'  $def'` exactly (same reasoning as `errors.dart.md`).

## 3. Decomposed Task Units

- T1. Class shell — emit non-sealed `class TypeTable` with `private readonly Dictionary<string, TypeDefinition> _types = new(StringComparer.Ordinal);` and (optional) explicit empty ctor — done.
- T2. `AddDefinition` — `TryGetValue` lookup, merge via `Concat(...).ToList()` rebuilding `TypeDefinition` with `existing.TypeParams/Line/Column`, else indexer-assignment upsert — done.
- T3. `AddConstructor` — optional params `IReadOnlyList<string>? typeParams = null, int line = 0, int col = 0`; `TryGetValue` lookup; merge via `Append(ctor).ToList()`; else construct with `typeParams ?? new List<string>()`, `new List<TypeConstructor> { ctor }`, `line`, `col` — done.
- T4. `LookupType` — `public TypeDefinition? LookupType(string name) => _types.GetValueOrDefault(name);` (renamed from getType per project-wide getX→LookupX idiom; no `new` modifier needed) — done.
- T5. `HasType` — `public bool HasType(string name) => _types.ContainsKey(name);` — done.
- T6. `TypeNames` and `Definitions` get-only `IEnumerable<T>` live-view properties — done.
- T7. `Length` get-only property mapping to `_types.Count`, name preserved — done.
- T8. `IsEmpty` get-only property synthesised as `_types.Count == 0` (no native `IsEmpty`) — done.
- T9. `FromDefinitions` static factory with `IReadOnlyList<TypeDefinition>`, order-preserving `foreach` — done.
- T10. `FromModule` static factory expression-bodied delegating to `FromDefinitions` — done.
- T11. `ToString` override using `StringBuilder` with explicit `Append("...\n")` (NOT `AppendLine`) for newline portability — done.

## 4. Research Findings

none required — every construct in this file is verbatim-derivable from the ratified convspec (`.codeconv/conversion-specs/lib/compiler/pmt/type_table.dart.md`), whose §"Rationale and research provenance" already carries full deep-analysis + authoritative .NET (Microsoft Learn) and Dart (dart.dev / api.dart.dev) citations for every non-trivial construct. The provenance research-finding IDs cited by the convspec (`rf-dart-map-to-csharp-dictionary`, `rf-dart-map-lookup-to-csharp-trygetvalue`, `rf-dart-named-default-param-to-csharp-optional-arg`, `rf-dart-getter-to-csharp-property`, `rf-dart-factory-ctor-const-default-to-csharp-static-factory`, `rf-dart-stringbuffer-to-csharp-stringbuilder`) are all already cached. No new research required for the plan.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/lib/compiler/pmt/type_table.dart.md` (ratified mirror). Every plan decision (C1–C11, T1–T11) is a verbatim restatement of a `target_decision` block in the convspec; the convspec's eleven `constructs:` entries map 1:1 to plan items C1–C11 and tasks T1–T11. Cross-file symbol naming for `Module.TypeDefinitions` is informational and resolved by sibling `ast.dart` / `type_ast.dart` specs at codegen-stitch time per the convspec's explicit alignment note (lines 336–348, 352–363) — no escalation required because the conversion of THIS file is fully decidable as written. Zero divergence from the convspec; zero novel decisions introduced by the plan.

## 6. Escalations

None.
