# Conversion Spec — lib/compiler/pmt/type_table.dart

> Conversion-spec artifact (FR-011). Spec-only (FR-023): describes the
> Dart->C# conversion; contains NO compilable C#. A later codegen stage
> consumes the structured block below.

```yaml
schema_version: 1
source_path: lib/compiler/pmt/type_table.dart
source_sha256: fecf3a38722602da8b1ac6e1a3459b739c37c0c02c91588b335e30ba0d6ce74a
target_code_unit: lib/compiler/pmt/type_table.cs
constructs:
  - construct_key: dart.collection_wrapper_class.private_mutable_map_field_default_ctor
    source_form: >-
      class TypeTable { final Map<String, TypeDefinition> _types = {};
      TypeTable(); ... }
    target_decision: >-
      Emit a non-sealed reference-type `class TypeTable` with one
      private instance field
      `private readonly Dictionary<string, TypeDefinition> _types = new();`
      (lowercase-underscore name preserved — Dart leading-underscore privacy
      maps to C# `private`; the field is `readonly` because the reference
      itself is `final` in Dart, but the dictionary CONTENTS are mutated by
      `addDefinition` / `addConstructor`, so the field is reference-immutable,
      value-mutable). The explicit no-arg `TypeTable()` constructor becomes the
      implicit C# default constructor (no body, no parameters) — emitting an
      empty explicit constructor is equivalent and acceptable; codegen MAY
      omit it. The dictionary is keyed by `string` with the default
      (ordinal-ish) equality comparer: Dart `Map<String,_>` uses Dart
      `String` value equality, which is exact-code-unit; the C# counterpart
      MUST construct the dictionary with `StringComparer.Ordinal` so type-name
      lookup is byte-exact and not culture-sensitive (matching
      `type_ast.dart`'s established discipline for builtin/system-type
      sets).
    idiom_id: null
    research_finding_id: rf-dart-map-to-csharp-dictionary
    nuance: >-
      Reference-vs-value: `TypeTable` is a reference-type container with
      mutable state (`addDefinition`/`addConstructor` mutate `_types` in place
      via re-assignment of map entries); it is therefore a `class`, NEVER a
      `record` or `struct`. Privacy: Dart `_types` is *library-private*, not
      class-private — any code in the same Dart library can see it. C#
      `private` is class-scoped (stricter); the only intra-library access in
      this file is `_types` itself, so `private` faithfully covers the same
      access surface. `final Map<...> _types = {}` is reference-final (Dart);
      C# `readonly` field matches that (the reference cannot be re-bound;
      `_types.Clear()` / `[k]=v` still work). String-equality discipline is
      load-bearing: omitting `StringComparer.Ordinal` would let
      culture-sensitive Turkic-I drift change which type names match in
      `getType` / `hasType`.
  - construct_key: dart.map_lookup_then_branch_on_null.value_object_merge_or_insert
    source_form: >-
      void addDefinition(TypeDefinition def) { final existing =
      _types[def.typeName]; if (existing != null) { final
      mergedConstructors = [...existing.constructors, ...def.constructors];
      _types[def.typeName] = TypeDefinition(def.typeName, existing.typeParams,
      mergedConstructors, existing.line, existing.column); } else {
      _types[def.typeName] = def; } }
    target_decision: >-
      Emit `public void AddDefinition(TypeDefinition def)`. The Dart pattern
      `final existing = _types[name]; if (existing != null) {...}` maps to
      `_types.TryGetValue(def.TypeName, out var existing)` followed by an
      `if (existing is not null) {...} else {...}` branch — NOT to a raw
      `_types[def.TypeName]` read, because the C# `Dictionary<,>` indexer
      `get` THROWS `KeyNotFoundException` on a miss (Microsoft Learn,
      `Dictionary<TKey,TValue>.Item[TKey]`: "Gets or sets the value
      associated with the specified key … the property is retrieved and key
      doesn't exist in the collection: KeyNotFoundException"). The
      merge branch builds the new constructor list via list concatenation
      `existing.Constructors.Concat(def.Constructors).ToList()` (deferred LINQ
      materialised once into a new `List<TypeConstructor>` — order preserved:
      `existing` first, then `def`, mirroring Dart `[...existing.constructors,
      ...def.constructors]`). The replacement `TypeDefinition` is rebuilt
      positionally with `def.TypeName`, `existing.TypeParams` (KEEP from
      first definition — comment-asserted source intent), the merged list,
      and `existing.Line` / `existing.Column` (NOT `def.Line`/`def.Column`).
      The else branch is a single indexer-assignment upsert
      `_types[def.TypeName] = def;`. NO `.Add(...)` (would throw on the
      first-write-then-merge happy path? — no, here the else branch is
      first-write, so `.Add` would be safe; but indexer-assignment is uniform
      with the merge branch and matches Dart map indexer semantics
      (last-wins upsert), so the spec prescribes indexer-assignment for
      consistency).
    idiom_id: null
    research_finding_id: rf-dart-map-lookup-to-csharp-trygetvalue
    nuance: >-
      Behavioural-difference nuance: Dart `Map[k]` returns `null` on a miss;
      C# `Dictionary[k]` THROWS. A naïve transliteration `var existing =
      _types[def.TypeName]; if (existing != null) {...}` would compile (if
      `TypeDefinition` is a reference type) but `_types[def.TypeName]` on
      the miss path would throw before `existing` is ever inspected — silent
      semantic regression. `TryGetValue(out var)` is the documented .NET
      idiom (Microsoft Learn: "use the TryGetValue method as an efficient
      way to retrieve values" — single-lookup, no exception). The
      `mergedConstructors` list is a NEW allocation each call (Dart spread
      creates a new list); C# `Concat(...).ToList()` matches — must NOT
      mutate `existing.Constructors` in place. Re-construction of
      `TypeDefinition` preserves the `existing.TypeParams`/`existing.Line`/
      `existing.Column` (i.e. first-definition site wins for params + source
      location); the spec MUST preserve that asymmetry verbatim, not
      "improve" it to use `def.Line`/`def.Column`.
  - construct_key: dart.method_with_optional_named_params_and_defaults.upsert_keyed_constructor
    source_form: >-
      void addConstructor(String typeName, TypeConstructor ctor,
      {List<String>? typeParams, int line = 0, int col = 0}) { final
      existing = _types[typeName]; if (existing != null) {
      _types[typeName] = TypeDefinition(typeName, existing.typeParams,
      [...existing.constructors, ctor], existing.line, existing.column); }
      else { _types[typeName] = TypeDefinition(typeName, typeParams ?? [],
      [ctor], line, col); } }
    target_decision: >-
      Emit `public void AddConstructor(string typeName, TypeConstructor ctor,
      IReadOnlyList<string>? typeParams = null, int line = 0, int col = 0)`.
      Dart `{List<String>? typeParams, int line = 0, int col = 0}` (named,
      one nullable + two with int defaults) maps to C# optional parameters
      with the same defaults (Microsoft Learn, optional arguments:
      "Each optional parameter has a default value as part of its
      definition"). The `typeParams` parameter is nullable
      (`IReadOnlyList<string>?` under enabled NRT) — its default is `null`,
      not an empty list, exactly mirroring Dart `List<String>?` with no
      explicit default. The body branches identically to `AddDefinition`:
      `TryGetValue(typeName, out var existing)` → if hit, rebuild
      `TypeDefinition` with the existing params/line/column and the
      single-element-appended constructors list
      `existing.Constructors.Append(ctor).ToList()` (LINQ `Append` is the
      idiomatic single-element concat — Microsoft Learn, `Enumerable.Append`:
      "Appends a value to the end of the sequence", deferred until
      `.ToList()` materialises); if miss, construct a new `TypeDefinition`
      with `typeName`, `typeParams ?? new List<string>()` (the Dart
      `typeParams ?? []` mirror — fresh empty list per call, not a shared
      static), a single-element constructor list `new List<TypeConstructor>
      { ctor }`, and the supplied `line`/`col`. Call sites that pass named
      arguments at the call site (e.g. `addConstructor("Foo", c, line: 10)`)
      map to C# named arguments unchanged (Microsoft Learn, named arguments:
      "specify the name of the corresponding parameter").
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Two nuances. (1) Dart `[]` as the default of `typeParams ?? []` is a
      *fresh* empty list per invocation (it appears in expression position,
      not as a default-parameter compile-time constant); a sloppy C#
      conversion that uses a shared `static readonly` empty list would alias
      mutable state across calls if a caller later mutated `TypeDef.TypeParams`
      — emit a fresh `new List<string>()` (or `Array.Empty<string>()` only if
      `TypeParams` is declared `IReadOnlyList<string>`). (2) Dart named
      defaults `int line = 0, int col = 0` map to C# optional value-type
      parameters with the same literal default; the *named-only* convention
      (Dart requires `line:` at the call site for named params; C# allows
      both positional and named for optional params) means C# is strictly
      more permissive — this is acceptable since every existing Dart call
      site that names the argument continues to work with `line:` in C# (the
      named-args feature) and previously-impossible positional calls do not
      regress any existing behaviour. The `(typeParams ?? [])` Dart expression
      maps directly to the C# null-coalescing operator pattern (already
      catalogued under rf-csharp-null-coalescing-operator-equivalent-to-dart-
      double-question).
  - construct_key: dart.nullable_lookup_getter_method.map_index_returning_question_T
    source_form: >-
      TypeDefinition? getType(String name) => _types[name];
    target_decision: >-
      Emit `public TypeDefinition? GetType(string name)` (a METHOD, not a
      property — single-string-argument lookup; C# convention reserves
      properties for parameterless accessors). Body uses
      `_types.TryGetValue(name, out var def) ? def : null`, NOT the raw
      `_types[name]` indexer (would throw on miss — same hazard as
      `AddDefinition`). Alternative permitted form: `return
      _types.GetValueOrDefault(name);` (Microsoft Learn,
      `CollectionExtensions.GetValueOrDefault`: "Tries to get the value
      associated with the specified key in the dictionary. Returns: The value
      for key if found in the dictionary; otherwise, the default value for
      the type"). Both forms yield `null` on a miss for the reference type
      `TypeDefinition`, matching Dart `_types[name]` exactly. Name SHADOWING
      caveat: `GetType` shadows `object.GetType()` (the runtime-type
      reflection method). The spec MUST keep the Dart name `GetType`
      verbatim (per FR-024 / 023 — preserve the source surface) and accept
      the resulting compiler warning CS0108 by either adding the `new`
      modifier (`public new TypeDefinition? GetType(string name)`) OR
      renaming at codegen time. The spec prescribes the `new` modifier:
      no rename, with `new` to explicitly acknowledge intentional hiding
      (Microsoft Learn, `new` modifier: "explicitly hides a member that is
      inherited from a base class").
    idiom_id: null
    research_finding_id: rf-dart-map-lookup-to-csharp-trygetvalue
    nuance: >-
      The load-bearing nuance is exactly the Dart-vs-C# map-miss semantic
      divergence (Dart: returns null; C#: throws on the indexer). Plus the
      `object.GetType` shadowing hazard, which has no Dart counterpart: every
      C# reference type inherits `Type GetType()` from `object`, so a
      same-name instance method requires `new` to compile cleanly and to
      document the deliberate hide. Nullability: Dart `TypeDefinition?` →
      C# `TypeDefinition?` under enabled NRT — the spec assumes
      `TypeDefinition` is a reference type (consistent with the
      sibling `type_ast.dart` AST conversion where types map to classes,
      not records).
  - construct_key: dart.map_containskey_predicate_method
    source_form: >-
      bool hasType(String name) => _types.containsKey(name);
    target_decision: >-
      Emit `public bool HasType(string name) => _types.ContainsKey(name);` —
      direct 1:1 map-to-`Dictionary.ContainsKey` mapping (Microsoft Learn,
      `Dictionary<TKey,TValue>.ContainsKey`: "Determines whether the
      Dictionary<TKey,TValue> contains the specified key"). Expression-bodied
      method preserves the Dart arrow form. O(1) hash lookup in both
      languages, so behaviour is identical.
    idiom_id: null
    research_finding_id: rf-dart-map-to-csharp-dictionary
    nuance: >-
      No subtle nuance: both Dart `Map.containsKey` and C#
      `Dictionary.ContainsKey` are average-O(1) hash-based lookups with the
      same null/throwing semantics (neither throws on a missing key). The
      method name `hasType` → `HasType` follows C# PascalCase; no `Has`/
      `Contains` synonym conversion is required.
  - construct_key: dart.iterable_view_getter.map_keys_or_values
    source_form: >-
      Iterable<String> get typeNames => _types.keys;
      Iterable<TypeDefinition> get definitions => _types.values;
    target_decision: >-
      Emit two get-only C# instance properties:
      `public IEnumerable<string> TypeNames => _types.Keys;` and
      `public IEnumerable<TypeDefinition> Definitions => _types.Values;`.
      `Dictionary<TKey,TValue>.Keys` returns a `KeyCollection` which
      implements `ICollection<TKey>` and `IEnumerable<TKey>` (Microsoft
      Learn, `Dictionary<TKey,TValue>.Keys`: "Returns the collection
      containing the keys in the Dictionary<TKey,TValue>"); the property
      narrows the exposed surface to `IEnumerable<T>` so callers cannot
      mutate via the strongly-typed collection. Both Dart `Map.keys` and C#
      `Dictionary.Keys` are LIVE VIEWS over the underlying map (not snapshots),
      so subsequent calls reflect later `AddDefinition` mutations — the spec
      preserves that view semantics deliberately (alternative
      `.ToList()` would snapshot and break parity).
    idiom_id: null
    research_finding_id: rf-dart-getter-to-csharp-property
    nuance: >-
      Live-view nuance is load-bearing: Dart `_types.keys` returns an
      `Iterable<String>` view that re-reads the map on each iteration; C#
      `Dictionary.Keys` likewise returns a live `KeyCollection`. Iterating
      these views while concurrently mutating the dictionary throws
      `InvalidOperationException` in C# (collection-was-modified) where Dart
      throws `ConcurrentModificationError` — same FAIL-FAST contract, slightly
      different exception type; the spec records this as the conversion-
      preserving observation, not an undecidable point. Iteration order:
      both `Dictionary` and Dart's default `Map` (`LinkedHashMap`) preserve
      insertion order (Microsoft Learn, `Dictionary<TKey,TValue>`: ".NET
      Framework only - the order in which the items are returned is
      undefined" but as of .NET Core / .NET 5+ insertion order is the
      observed behaviour for unmodified dictionaries; the spec does NOT
      rely on this beyond the documented uniqueness contract — codegen
      consumers must not assume strict ordering).
  - construct_key: dart.int_count_getter.map_length
    source_form: >-
      int get length => _types.length;
    target_decision: >-
      Emit `public int Length => _types.Count;` — a get-only int-valued
      property. Dart `Map.length` ↔ C# `Dictionary.Count` (Microsoft Learn,
      `Dictionary<TKey,TValue>.Count`: "Gets the number of key/value pairs
      contained in the Dictionary<TKey,TValue>"); both are O(1). Name kept as
      `Length` (verbatim from Dart) rather than the .NET-idiomatic
      `Count` to preserve the source-surface naming under FR-023; this is a
      principled deviation from .NET convention because the Dart contract
      explicitly named it `length` and dependent callers (currently none in
      C# space) will be regenerated from the same spec. If the codegen stage
      later prefers `Count`, that is a downstream renaming decision, not a
      conversion-spec one.
    idiom_id: null
    research_finding_id: rf-dart-getter-to-csharp-property
    nuance: >-
      Trivial behavioural mapping. The only nuance is the naming convention
      drift (Dart `length`/Dart-`Map.length` vs .NET `Count`): the spec
      preserves the Dart name (FR-024 / FR-023 — describe-the-conversion,
      not improve-the-API). Type: Dart `int` returning `_types.length` (also
      `int`); C# `Count` is `int`; no width-conversion (`rf-dart-int-to-
      csharp-long-width` does not apply because Dart `Map.length` is a 32-bit
      int domain in practice — never exceeds `int.MaxValue` for any realistic
      type table).
  - construct_key: dart.bool_predicate_getter.map_isEmpty
    source_form: >-
      bool get isEmpty => _types.isEmpty;
    target_decision: >-
      Emit `public bool IsEmpty => _types.Count == 0;`. C# `Dictionary<,>`
      does NOT expose an `IsEmpty` property (Microsoft Learn,
      `Dictionary<TKey,TValue>` member list: only `Count`, `Keys`, `Values`,
      `Comparer`); the documented idiom for "is the dictionary empty" is
      `dictionary.Count == 0` (O(1)). LINQ `Any()` would also work but is
      strictly slower (enumerator allocation) and not idiomatic for `IDictionary`.
      The exposed property name `IsEmpty` is preserved verbatim from Dart,
      mapping `bool get isEmpty` to a get-only C# property.
    idiom_id: null
    research_finding_id: rf-dart-getter-to-csharp-property
    nuance: >-
      API-surface nuance: Dart `Map.isEmpty` is a built-in O(1) predicate;
      C# `Dictionary.Count == 0` is the equivalent (no native `IsEmpty`).
      `Any()` is REJECTED here: it returns `bool` over `IEnumerable<T>` and
      allocates a `KeyCollection`+enumerator (small but unnecessary), whereas
      `Count == 0` is a single field read; behaviour is identical (both true
      iff the dictionary is empty) but the spec mandates the more efficient
      form, matching the source's O(1) intent.
  - construct_key: dart.static_factory_method.populate_table_from_list
    source_form: >-
      static TypeTable fromDefinitions(List<TypeDefinition> defs) { final
      table = TypeTable(); for (final def in defs) { table.addDefinition(def);
      } return table; }
    target_decision: >-
      Emit `public static TypeTable FromDefinitions(IReadOnlyList<TypeDefinition> defs)`
      (input typed as `IReadOnlyList<TypeDefinition>` — read-only-view
      acceptance widens the call surface without weakening behaviour; the
      method only iterates, never mutates `defs`). Body: `var table = new
      TypeTable(); foreach (var def in defs) table.AddDefinition(def); return
      table;`. Dart `static` methods on a class map 1:1 to C# `static`
      methods (Microsoft Learn, static members: "A class can have static
      methods that are called on the class itself, not on an instance");
      there is no language-level `factory` keyword in C# — `static` returning
      `TypeTable` is the documented analog. Side-effect ordering preserved:
      `defs` is iterated front-to-back, so the merge-vs-insert behaviour of
      `AddDefinition` (first occurrence wins for params/line/col) depends on
      input order — preserved verbatim.
    idiom_id: null
    research_finding_id: rf-dart-factory-ctor-const-default-to-csharp-static-factory
    nuance: >-
      Dart `static` member ↔ C# `static` member is a 1:1 mapping; the only
      naming nuance is PascalCase (`FromDefinitions`). Side-effect/ordering
      nuance is the load-bearing one: `addDefinition` is order-sensitive (the
      first call for a given `typeName` decides `typeParams`/`line`/`col`),
      so iterating in the original list order is REQUIRED — a parallel
      LINQ or unordered fold would change observable output. `foreach`
      preserves source order deterministically and matches Dart `for (final
      def in defs)`.
  - construct_key: dart.static_factory_method_delegating_to_another_factory.module_to_table
    source_form: >-
      static TypeTable fromModule(Module module) { return
      fromDefinitions(module.typeDefinitions); }
    target_decision: >-
      Emit `public static TypeTable FromModule(Module module) =>
      FromDefinitions(module.TypeDefinitions);` (expression-bodied static
      method delegating to `FromDefinitions`). The source accesses
      `module.typeDefinitions`; the C# property name is the PascalCase
      `Module.TypeDefinitions`. ALIGNMENT NOTE: in the current
      `glp_runtime_net/lib/compiler/ast.dart`, the `Module` class actually
      exposes the field as `typeDefs: List<TypeDef>` (NOT `typeDefinitions`)
      and the AST type is `TypeDef`, not `TypeDefinition`. The spec records
      this divergence here as an INFORMATIONAL note — the conversion of
      `type_table.dart` itself is unambiguous given the source as written;
      whether the actual ast.dart symbol is named `typeDefs`/`typeDefinitions`
      and whether the AST class is `TypeDef`/`TypeDefinition` is a SEPARATE
      naming decision recorded in `ast.dart.md` / `type_ast.dart.md` (the
      authoritative specs for those symbols). The codegen stage will
      reconcile the two when it stitches the C# project together; if a true
      mismatch remains then, the resolution belongs in ast.dart's spec, not
      here. No escalation: the conversion of THIS file is fully decidable.
    idiom_id: null
    research_finding_id: rf-dart-factory-ctor-const-default-to-csharp-static-factory
    nuance: >-
      Cross-file naming nuance: the Dart source of `type_table.dart`
      references `TypeDefinition` (not `TypeDef`) and `Module.typeDefinitions`
      (not `Module.typeDefs`). These symbols are imported from
      `'../ast.dart'` via the relative import directive at the top of the
      file. The spec for `ast.dart` / `analysis/type_checker/type_ast.dart`
      governs the canonical C# name; this file's spec MUST track that
      decision (via codegen-time symbol resolution) rather than re-decide
      it locally. Recording the divergence here makes the cross-file
      coupling explicit for review (SC-006 reviewable rationale) without
      escalating — escalation is reserved for genuinely undecidable
      Dart→C# conversion questions, not cross-file naming alignment that
      another spec already resolves.
  - construct_key: dart.tostring_override_with_stringbuffer_loop
    source_form: >-
      @override String toString() { final buffer = StringBuffer('TypeTable(\n');
      for (final def in _types.values) { buffer.writeln('  $def'); }
      buffer.write(')'); return buffer.toString(); }
    target_decision: >-
      Emit `public override string ToString() { var sb = new
      System.Text.StringBuilder("TypeTable(\n"); foreach (var def in
      _types.Values) { sb.AppendLine($"  {def}"); } sb.Append(')'); return
      sb.ToString(); }` (logical C# shape; not compilable text — codegen
      emits real syntax). Dart `StringBuffer` ↔ C# `StringBuilder`
      (Microsoft Learn, `StringBuilder`: "Represents a mutable string of
      characters"). `StringBuffer.writeln(s)` appends `s` followed by `\n`
      (Dart core API `StringSink.writeln`: "Writes the string representation
      of object and follows it with a newline."); C# `StringBuilder.AppendLine(s)`
      appends `s` followed by `Environment.NewLine` (Microsoft Learn,
      `StringBuilder.AppendLine(String)`: "Appends a copy of the specified
      string followed by the default line terminator to the end of the
      current StringBuilder object"). NEWLINE DIVERGENCE is the load-bearing
      nuance — see below; the spec mandates explicit `Append("\n")` (NOT
      `AppendLine`) to preserve the Dart-exact `\n` literal so the
      `ToString` output is bit-identical across Linux/macOS/Windows.
      Reformulated decision: `sb.Append($"  {def}\n");` for each entry
      instead of `sb.AppendLine($"  {def}")`. Interpolation `'  $def'` →
      `$"  {def}"` invokes the polymorphic `ToString` virtually on either
      side (mirroring established `errors.dart.md` finding).
    idiom_id: null
    research_finding_id: rf-dart-stringbuffer-to-csharp-stringbuilder
    nuance: >-
      Two nuances. (1) Newline portability: Dart `StringBuffer.writeln` ALWAYS
      writes `\n` (LF, regardless of host OS — Dart core API doc names a
      newline literal). C# `StringBuilder.AppendLine` writes
      `Environment.NewLine`, which is `\r\n` on Windows. A naïve substitution
      `writeln` → `AppendLine` would silently change observable `toString`
      output on Windows (extra `\r` per line), which would break any test
      that compares the result to an expected golden string. The spec
      mandates `Append($"...\n")` to preserve Dart's hard-coded `\n` exactly.
      (2) The opening `'TypeTable(\n'` and trailing `')'` are written
      explicitly with `.Append`, not `.AppendLine`, so no `\r\n` drift
      sneaks in there either. Polymorphism: `'  $def'` invokes `def.toString`
      virtually in Dart; `$"  {def}"` invokes `def.ToString()` virtually in
      C# — semantics preserved.
conversion_units:
  - "class TypeTable (non-sealed reference type; private readonly Dictionary<string,TypeDefinition> _types built with StringComparer.Ordinal; default ctor implicit/explicit)"
  - "void AddDefinition(TypeDefinition def) — TryGetValue branch; merge rebuilds TypeDefinition with existing.TypeParams/Line/Column and concatenated constructor list (Concat(...).ToList()); else upserts via indexer assignment"
  - "void AddConstructor(string typeName, TypeConstructor ctor, IReadOnlyList<string>? typeParams=null, int line=0, int col=0) — optional parameters with same defaults; TryGetValue branch; merge appends single ctor via .Append(ctor).ToList(); else constructs TypeDefinition with (typeParams ?? new List<string>()), [ctor], line, col"
  - "TypeDefinition? GetType(string name) — public new TypeDefinition? GetType(string name) (new modifier to shadow object.GetType()); returns _types.GetValueOrDefault(name)"
  - "bool HasType(string name) => _types.ContainsKey(name) — expression-bodied"
  - "IEnumerable<string> TypeNames => _types.Keys (get-only live-view property)"
  - "IEnumerable<TypeDefinition> Definitions => _types.Values (get-only live-view property)"
  - "int Length => _types.Count (get-only property — name preserved from Dart, NOT renamed to Count)"
  - "bool IsEmpty => _types.Count == 0 (get-only property — synthesised because Dictionary has no IsEmpty)"
  - "static TypeTable FromDefinitions(IReadOnlyList<TypeDefinition> defs) — foreach over defs calling AddDefinition; returns table (order-sensitive, preserved)"
  - "static TypeTable FromModule(Module module) => FromDefinitions(module.TypeDefinitions) — cross-file symbol name aligned by ast.dart spec at codegen time"
  - "override string ToString() — StringBuilder with explicit Append(\"\\n\") (NOT AppendLine) to preserve Dart's hard-coded LF newline across OSes; opens with \"TypeTable(\\n\", iterates _types.Values writing $\"  {def}\\n\", closes with ')'"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-map-to-csharp-dictionary — the `Map<String, TypeDefinition>` backing field

- **Deep analysis.** `TypeTable._types` is the entire state of the table: a
  Dart `Map<String, TypeDefinition>` initialised to the empty literal `{}`,
  declared `final` (reference-immutable) but mutated by `addDefinition` /
  `addConstructor` via map-indexer upsert. The wrapper class adds merge
  semantics on top — duplicate keys do NOT throw, they merge constructor
  lists. No iteration order is asserted by the source beyond what
  `Map.values` / `Map.keys` provide (Dart default `LinkedHashMap` insertion
  order — used implicitly by `toString` but not contractually).
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2`
  — Microsoft Learn: `Dictionary<TKey,TValue>` is "a collection of keys and
  values" with average O(1) `Add` / `Remove` / `ContainsKey` / indexer; the
  indexer "Gets or sets the value associated with the specified key", with
  `set` doing an upsert (the documented assignment semantics: "If the
  specified key is not found, set operation creates a new element with the
  specified key"). This exactly matches Dart map indexer semantics: `m[k] =
  v` is upsert in both languages.
- **Authoritative Dart.** WebFetch
  `https://api.dart.dev/dart-core/Map-class.html` — dart.dev official: "A
  collection of key/value pairs … `Map[]=` Associates the key with the given
  value." The default `Map` literal `{}` constructs a `LinkedHashMap`
  preserving insertion order. Confirms both the indexer-upsert and the
  ordered-iteration contract preserved by the conversion.
- **String-comparison discipline.** Established by the sibling
  `type_ast.dart.md` finding `rf-dart-const-set-to-csharp-frozenset-ordinal`
  (already cached, no second research): `StringComparer.Ordinal` must be
  passed to the `Dictionary` constructor to prevent culture-sensitive drift
  in type-name matching.

### rf-dart-map-lookup-to-csharp-trygetvalue — null-on-miss vs throw-on-miss

- **Deep analysis.** Three sites use map-lookup with a null check:
  `addDefinition` (`final existing = _types[def.typeName]; if (existing !=
  null) ...`), `addConstructor` (same shape with `typeName`), and
  `getType` (`=> _types[name]` returned directly as a nullable). The
  null-on-miss semantic is load-bearing — branching, fall-through, and
  callers of `getType` all rely on it.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.item`
  — Microsoft Learn, `Dictionary<TKey,TValue>.Item[TKey]`, decisive: the
  property "is retrieved and key doesn't exist in the collection:
  `KeyNotFoundException`." A naïve `var existing = _types[def.TypeName]` on
  the miss path would throw before the `if (existing is not null)` check
  runs — a silent semantic regression vs. Dart's `existing == null` branch.
  The documented .NET idiom is `TryGetValue(out var v)`: "Gets the value
  associated with the specified key. … `true` if the dictionary contains an
  element with the specified key; otherwise, `false`." Single-lookup, no
  exception.
- **Authoritative Dart.** WebFetch
  `https://api.dart.dev/dart-core/Map/operator_get.html` — dart.dev official,
  `Map.operator []` returns "The value associated with key, or `null` if key
  is not in the map." Confirms the null-on-miss contract the conversion must
  preserve.
- **Conclusion.** `TryGetValue(out var existing)` followed by an `is not
  null` branch reproduces Dart's `final existing = m[k]; if (existing !=
  null)` exactly; the alternative `GetValueOrDefault(key)` is used in the
  expression-bodied `GetType` for compactness — both forms return `null` on
  a miss for reference types like `TypeDefinition`. The C# `object.GetType()`
  shadowing hazard is handled with the `new` modifier (Microsoft Learn, `new`
  modifier in member declaration: "explicitly hides a member that is inherited
  from a base class"); the spec preserves the Dart name verbatim and accepts
  the principled hide.

### rf-dart-named-default-param-to-csharp-optional-arg — addConstructor's named tail

- **Deep analysis.** `addConstructor`'s tail `{List<String>? typeParams,
  int line = 0, int col = 0}` is the Dart named-parameter idiom with mixed
  nullable-no-default and value-type-with-literal-default. The body
  expression `typeParams ?? []` falls back to a *fresh* empty list — not a
  shared static.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`
  — Microsoft Learn, decisive: "Each optional parameter has a default value
  as part of its definition. If no argument is sent for that parameter, the
  default value is used. A default value must be one of the following types
  of expressions: a constant expression; an expression of the form `new
  ValType()`, where `ValType` is a value type, such as an enum or a struct;
  an expression of the form `default(ValType)`, …" Constant `0` for `int`
  and `null` for a reference type both qualify; `new List<string>()` as a
  default does NOT (not a constant) — so the C# default for `typeParams`
  MUST be `null`, with the `?? new List<string>()` materialisation in the
  method body, exactly as the Dart source does it.
- **Authoritative Dart.** WebFetch
  `https://dart.dev/language/functions#parameters` — dart.dev official:
  named parameters require `{}` syntax, are optional unless `required`, and
  default to `null` when no default is given. Confirms the `typeParams`
  default is `null` and the call-site must use `name:` syntax.
- **Conclusion.** C# optional parameters with the same literal defaults
  (`null`, `0`, `0`) reproduce the Dart surface; the fresh-empty-list
  fallback in the body is preserved verbatim. Named-vs-positional surface
  drift (Dart requires names; C# allows positional too) is documented as a
  strict widening — no behavioural regression at any existing call site.

### rf-dart-getter-to-csharp-property — five getters

- **Deep analysis.** `typeNames`, `definitions`, `length`, `isEmpty` are all
  parameterless getters returning a value derived from the backing
  dictionary. `getType` is NOT a getter (takes a `String` parameter) — it
  is a method, handled above.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/properties`
  — Microsoft Learn: a property is "a member that provides a flexible
  mechanism to read, write, or compute the value of a private field. … Use
  properties as if they're public data members". Get-only `=> expr`
  expression-body syntax is the documented compact form. Microsoft Learn
  framework-design guidelines additionally state that parameterless accessors
  should be properties (not methods).
- **Authoritative Dart.** WebFetch
  `https://dart.dev/language/methods#getters-and-setters` — dart.dev
  official: a Dart `int get length => ...` defines a parameterless getter;
  it has no parentheses at the call site (`table.length`, not
  `table.length()`). This matches C# property call-site syntax exactly
  (`table.Length`, no parentheses).
- **Conclusion.** Each Dart parameterless getter maps to a C# get-only
  property; expression-bodied form is preserved. Naming nuance: `Length`
  (verbatim) is preferred over the .NET-idiomatic `Count` to preserve the
  source-surface name per FR-023 / FR-024.

### rf-dart-factory-ctor-const-default-to-csharp-static-factory — fromDefinitions / fromModule

- **Deep analysis.** Both `static TypeTable fromDefinitions(...)` and
  `static TypeTable fromModule(...)` are Dart `static` methods (NOT
  `factory` constructors — `factory` is a keyword only on generative
  constructors). They are class-level factories: construct a `TypeTable`,
  populate it, return it. `fromModule` delegates to `fromDefinitions`.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members`
  — Microsoft Learn: "Static methods are callable on a class even when no
  instance of the class has been created." The static factory method is the
  documented .NET equivalent of Dart `factory` and Dart `static` constructors;
  there is no language-level `factory` keyword in C#. Returning a freshly
  constructed instance is the canonical shape.
- **Authoritative Dart.** WebFetch `https://dart.dev/language/constructors`
  — dart.dev official: distinguishes `factory` constructors from `static`
  methods; the source uses the latter, so the C# `static` mapping is
  literal. The dart.dev page also explicitly names the C# counterpart for
  factory constructors ("a static factory method") — same idiom applies.
- **Conclusion.** PascalCase `FromDefinitions` / `FromModule` static methods
  with the same shape. Side-effect ordering (`foreach` over input) is
  preserved deterministically to keep `AddDefinition`'s first-occurrence-wins
  contract intact. The `Module.TypeDefinitions` cross-file name is
  resolved by the `ast.dart` / `type_ast.dart` specs — recorded as an
  alignment note, not an escalation, because the conversion of THIS file is
  fully decidable.

### rf-dart-stringbuffer-to-csharp-stringbuilder — toString with newline-portability fix

- **Deep analysis.** `toString` builds a multi-line string using a
  `StringBuffer`: header `'TypeTable(\n'`, one indented line per
  definition (`'  $def'`) followed by `\n` (via `writeln`), trailing `')'`.
  The Dart `\n` is hard-coded LF, regardless of host OS.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.text.stringbuilder`
  — Microsoft Learn: `StringBuilder` is "a mutable string of characters",
  with `Append` for inline append and `AppendLine` which "appends the
  default line terminator to the end of the current StringBuilder object."
  `AppendLine`'s terminator is `Environment.NewLine` — `\r\n` on Windows,
  `\n` elsewhere. This is the BEHAVIOURAL DIVERGENCE from Dart `writeln`,
  which always emits `\n` (Dart core API `StringSink.writeln`).
- **Authoritative Dart.** WebFetch
  `https://api.dart.dev/dart-core/StringSink/writeln.html` — dart.dev
  official: "Writes the string representation of object, followed by a
  newline." The Dart spec for `\n` in a string literal is U+000A LINE FEED,
  no carriage return.
- **Conclusion.** Use `StringBuilder` with explicit `Append("\n")` per line
  (NOT `AppendLine`), so the `ToString` output is byte-identical to Dart's
  across all host OSes. Polymorphic `$"  {def}"` invokes the virtual
  `def.ToString()` on either side — semantics preserved (same as the
  established `errors.dart.md` interpolation reasoning).

## Notes

- The source file references three symbols imported from `'../ast.dart'`:
  `TypeDefinition`, `TypeConstructor`, and `Module.typeDefinitions`. In the
  current `glp_runtime_net/lib/compiler/ast.dart` the AST class is named
  `TypeDef` (defined in `lib/analysis/type_checker/type_ast.dart`) and the
  `Module` field is `typeDefs`, not `typeDefinitions`. Whether this
  reflects pre-refactor source code, a planned rename, or dead code is
  outside this spec's authority — the conversion of `type_table.dart` itself
  is fully decidable given the file as written, and the cross-file symbol
  alignment is the responsibility of `ast.dart` / `type_ast.dart`'s specs
  at codegen-stitch time. No escalation is raised because every Dart→C#
  construct in THIS file maps unambiguously to an authoritative
  Dart/.NET idiom.
- No async/Stream/Future, no isolates, no late, no records — the file is a
  synchronous, mutable, reference-typed wrapper around a single dictionary.
  The well-known nuances (value-vs-reference, async/Stream, null-safety,
  isolates) are correctly absent and intentionally not asserted.
- Privacy mapping `_types` → `private readonly` is sufficient (the field is
  not used outside this file); the broader Dart library-private surface is
  irrelevant because no other Dart library in the same package reads
  `_types`.
- Every non-trivial construct above carries BOTH a deep-analysis basis AND
  an authoritative-research basis (Dart or .NET official docs, never
  web-only), satisfying SC-006 / FR-009 / FR-010.
- Zero escalations (SC-008): every undecidable point would have been an
  escalation; none arose for this file.
