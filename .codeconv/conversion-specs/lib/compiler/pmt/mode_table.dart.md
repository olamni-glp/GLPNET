# Conversion Spec — lib/compiler/pmt/mode_table.dart

> Conversion-spec artifact for `lib/compiler/pmt/mode_table.dart` (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/compiler/pmt/mode_table.dart
source_sha256: 64b7072930ab5bb1260bf1507e236ce16dff7494d456d04154c142fdd4618b21
target_code_unit: lib/compiler/pmt/mode_table.cs
constructs:
  - construct_key: dart.enum.plain_two_members_reader_writer
    source_form: >-
      enum Mode { reader, writer }
    target_decision: >-
      Emit a plain C# `enum Mode { Reader, Writer }` (PascalCase per .NET
      naming guidelines; declaration order preserved so `(Mode)0 == Reader`
      and `(Mode)1 == Writer`, matching the Dart source's `isReader ?
      Mode.reader : Mode.writer` branch order at line 38). NO methods, NO
      static aliases, NO toString override — this enum has none in Dart, so
      none in C#. The enum name `Mode` does NOT collide here because this
      file's `pmt.Mode` lives in a different namespace from the unrelated
      type-checker `lib/analysis/type_checker/mode.dart`'s enhanced `Mode`
      (the pmt enum is a PMT-internal argument-direction marker; the
      analysis/type_checker enum is a typing-mode marker). Codegen places
      this `Mode` in namespace `<root>.Compiler.Pmt` mirroring the Dart
      directory, so no clash.
    idiom_id: null
    research_finding_id: rf-dart-enum-plain-to-csharp-enum
    nuance: >-
      Value-vs-reference: both Dart enums and C# enums are value types
      compared by value/identity — no boxing or reference-identity hazard.
      The two-member enum is exhaustively consumed only inside
      `addDeclaration` via the ternary `arg.isReader ? Mode.reader :
      Mode.writer`, which is a boolean dispatch (not a `switch`), so the
      C#-vs-Dart enum-switch-exhaustiveness nuance documented in
      mode.dart.md does NOT arise here. Null-safety: enum members are
      non-nullable values; the field types stored in collections below
      use the plain `Mode` (never `Mode?`) — matched on the C# side under
      enabled NRT by `Mode` (not `Mode?`). Naming nuance: Dart `reader`/
      `writer` → C# `Reader`/`Writer` per the .NET enum-member
      capitalisation rule (Microsoft Learn "Names of Enumerations").
  - construct_key: dart.collection_class.dual_map_string_to_list_of_t_signature_keyed_by_predicate_slash_arity
    source_form: >-
      class ModeTable { final Map<String, List<List<Mode>>> _modes = {};
      final Map<String, List<ModeDeclaration>> _declarations = {}; ... }
    target_decision: >-
      Emit a public reference-type C# `class ModeTable` (NOT a struct, NOT
      a record — it is a mutable container with identity semantics and
      shared mutable state). Two private readonly backing fields, both
      `Dictionary<string, List<...>>` constructed in their initialiser
      with `StringComparer.Ordinal` to make string-key semantics match
      Dart's `Map<String, V>` exactly (no culture-sensitive comparison):
      `private readonly Dictionary<string, List<List<Mode>>> _modes =
      new(StringComparer.Ordinal);` and `private readonly Dictionary
      <string, List<ModeDeclaration>> _declarations = new(StringComparer
      .Ordinal);`. The fields are `readonly` (the dictionary references
      never change after construction; their contents do) — this mirrors
      the Dart `final Map<...> _modes = {};` semantic ("the reference is
      final; the contents are not"). The inner `List<List<Mode>>` and
      `List<ModeDeclaration>` are mutable `System.Collections.Generic
      .List<T>` instances (matching Dart `List<T>` which is mutable by
      default). The two parallel dictionaries are kept structurally
      synchronised by the public API surface — the Dart source enforces
      this by writing to both inside `addDeclaration`; the C# class MUST
      preserve this two-write invariant verbatim, NOT collapse to a
      single dictionary of a pair-type (which would diverge from the
      Dart source's separately-exposed `getAllModes` and
      `getAllDeclarations` accessors).
    idiom_id: null
    research_finding_id: rf-csharp-string-equality-ordinal-by-default
    nuance: >-
      Value-vs-reference + string-key ordinality is the load-bearing
      nuance. Dart `Map<String, V>` uses Dart's `String` equality which
      is code-unit-by-code-unit (ordinal-equivalent, never culture-
      sensitive); the C# `Dictionary<string, V>` default uses
      `EqualityComparer<string>.Default` which IS ordinal in practice
      but the project precedent (well_typed_term.dart §"Dictionary
      string-key ordinal comparer" carry-over) MANDATES explicit
      `StringComparer.Ordinal` construction so the semantics are
      visible at the call-site and immune to any future framework
      ambient-culture change. The `signature` string is the composed
      `"$predicate/$arity"` (e.g. `"merge/3"`) — a pure ASCII identifier
      with a slash, no culture issue at any character. Reference-
      aliasing: the Dart source stores the caller's `ModeDeclaration`
      reference directly (no copy); the C# `List<ModeDeclaration>`
      stores the same reference. The `getDeclaration`/`getAllDeclarations`
      accessors return that same reference — caller-side mutation of
      `ModeDeclaration` fields would alias both sides, but `ModeDeclaration`
      is itself an immutable-field AST node (per ast.dart's `final`
      fields), so this is a benign aliasing.
  - construct_key: dart.map.putifabsent_list_factory_then_append
    source_form: >-
      _modes.putIfAbsent(signature, () => []).add(modes);
      _declarations.putIfAbsent(signature, () => []).add(decl);
    target_decision: >-
      Emit the cached `TryGetValue`-then-`Add`-then-append idiom per
      `rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add` (cached from
      well_typed_term.dart). Concretely, per call site:
      `if (!_modes.TryGetValue(signature, out var modesBucket)) {
      modesBucket = new List<List<Mode>>(); _modes[signature] =
      modesBucket; } modesBucket.Add(modes);` and the parallel form for
      `_declarations`. The `() => []` Dart closure (lazy default
      construction) is honoured: the empty `List<...>` is constructed
      ONLY on the cache-miss arm in both languages. `.NET 6+`
      `CollectionsMarshal.GetValueRefOrAddDefault` is REJECTED as a
      micro-optimisation outside scope (also rejected by well_typed_term
      .dart and message_queue.dart — keeps idiom uniform across the
      project). The `signature` value used as the key is the same
      `ModeDeclaration.signature` getter (`"$predicate/$arity"`) — the
      Dart `decl.signature` access maps to a C# `decl.Signature`
      get-only property on the C# `ModeDeclaration` type (out-of-scope
      for this file; the C# `ModeDeclaration` shape is fixed by the
      ast.dart conversion spec).
    idiom_id: null
    research_finding_id: rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add
    nuance: >-
      The Dart `putIfAbsent` returns the (existing or newly inserted)
      value, allowing the inline `.add(...)` chain. The C# TryGetValue
      pattern does NOT chain — it requires the named local on the cache-
      miss arm. Codegen MUST therefore expand the chain to a four-
      statement block per call site (lookup, branch, insert, append),
      never silently collapse to a single-line. The two parallel call
      sites in `addDeclaration` must each expand independently — they
      operate on different dictionaries (`_modes` keyed to `List<List
      <Mode>>` bucket, `_declarations` keyed to `List<ModeDeclaration>`
      bucket) and the order is preserved: `_modes` first, `_declarations`
      second (matches Dart source order, which is observable iff a
      future caller introspects the table mid-construction).
  - construct_key: dart.string_interpolation.predicate_slash_arity_signature
    source_form: >-
      '$predicate/$arity'   (in getModes, getAllModes, hasDeclaration,
      hasMultipleModes, getDeclaration, getAllDeclarations)
    target_decision: >-
      Direct token-for-token mapping to C# interpolated string
      `$"{predicate}/{arity}"`. Six call sites — codegen emits the
      identical interpolation at each. No `string.Format` rewrite, no
      `StringBuilder` rewrite (single-allocation small string; both
      languages compile to roughly equivalent code). Codegen MUST NOT
      hoist the interpolation to a single computed local even where two
      call sites are adjacent: keeping the textual call-site shape
      faithful to the Dart source aids reviewer parity and downstream
      diff stability.
    idiom_id: null
    research_finding_id: rf-csharp-interpolated-string-equivalent-to-dart-interpolation
    nuance: >-
      Composite-formatting culture sensitivity: `arity` is a non-negative
      `int`; C# default integer formatting is the invariant decimal-digit
      form for small values, identical to Dart `int.toString()` — no
      culture-affixed digit-shape issue. No padding, no precision
      specifier, no thousand-separator. The slash is a literal ASCII
      character. Verbatim semantic equivalence; no escalation. Reuse of
      this idiom across six call sites in this file constitutes
      "recurring construct resolved via a recorded idiom" per SC-007.
  - construct_key: dart.nullable_first_or_default_via_isnotempty_then_first_bang
    source_form: >-
      final allModes = _modes['$predicate/$arity'];
      return allModes?.isNotEmpty == true ? allModes!.first : null;
      (and the parallel form on _declarations in getDeclaration)
    target_decision: >-
      Emit a two-statement form using `TryGetValue` (no direct C# indexer
      analogue that returns `null` for missing keys without throwing
      `KeyNotFoundException`): `if (!_modes.TryGetValue($"{predicate}/
      {arity}", out var allModes) || allModes.Count == 0) return null;
      return allModes[0];`. The Dart triple `allModes?.isNotEmpty == true
      ? allModes!.first : null` collapses three states (missing key
      → null; key present + empty list → null; key present + non-empty
      → first element) into one expression. The C# form must preserve
      all three branches: TryGetValue handles missing-key, `Count == 0`
      handles empty-bucket, else `[0]`. The `?? :` `allModes?.isNotEmpty
      == true` pattern (note the explicit `== true` to disambiguate the
      Dart tri-state `null/false/true` produced by the propagating `?.`)
      is one of Dart's well-known non-obvious null-coalescing forms; the
      C# rewrite eliminates the tri-state entirely.
    idiom_id: null
    research_finding_id: rf-csharp-dictionary-trygetvalue-then-fallback-null
    nuance: >-
      Null-safety mapping is the load-bearing nuance. Dart `Map<K,V>`
      indexer returns `V?` (nullable) for missing keys (silent miss);
      C# `Dictionary<TKey,TValue>` indexer THROWS
      `KeyNotFoundException` on miss — silently translating Dart `_modes
      [k]` to C# `_modes[k]` would change exception-throw semantics from
      "never throws" to "throws on miss". The conversion MUST therefore
      route through `TryGetValue`. The return type is `List<Mode>?` in
      Dart → `List<Mode>?` (nullable reference) in C# under enabled
      NRT — `getModes`/`getAllModes`/`getDeclaration`/`getAllDeclarations`
      all return nullable references. The `.first` accessor maps to `[0]`
      on `List<T>` (not `.First()` LINQ — `[0]` is allocation-free and
      faithful to the source); both throw on an empty list but we have
      already guarded against that with the `Count == 0` check.
  - construct_key: dart.map.containsKey_signature_lookup
    source_form: >-
      bool hasDeclaration(String predicate, int arity) {
        return _modes.containsKey('$predicate/$arity');
      }
    target_decision: >-
      Emit a one-liner C# method:
      `public bool HasDeclaration(string predicate, int arity) =>
      _modes.ContainsKey($"{predicate}/{arity}");`. `Dictionary<TKey,
      TValue>.ContainsKey` is the documented direct equivalent of Dart
      `Map<K,V>.containsKey` — both return `bool`, both are O(1)
      amortised on hash dictionaries. NRT: parameters non-nullable
      (Dart source has no `?` markers on the parameters).
    idiom_id: null
    research_finding_id: rf-dart-map-containskey-to-csharp-dictionary-containskey
    nuance: >-
      Trivial direct mapping. No null-safety hazard (return is `bool`,
      not nullable). No exception-throw semantic change (both APIs are
      total). String-key equality goes through the dictionary's
      configured `StringComparer.Ordinal` (set on the field initialiser,
      see the dual-map construct above) — explicit ordinal makes the
      semantics match Dart exactly.
  - construct_key: dart.collection.nested_for_in_search_returning_first_match_or_null
    source_form: >-
      ModeDeclaration? getDeclarationByTypeName(String typeName) {
        for (final decls in _declarations.values) {
          for (final decl in decls) {
            if (decl.typeName == typeName) { return decl; }
          }
        }
        return null;
      }
    target_decision: >-
      Emit a direct nested-foreach translation that preserves the early-
      return semantics. `public ModeDeclaration? GetDeclarationByTypeName
      (string typeName) { foreach (var decls in _declarations.Values) {
      foreach (var decl in decls) { if (decl.TypeName == typeName) return
      decl; } } return null; }`. Do NOT rewrite to LINQ `SelectMany +
      FirstOrDefault`: the imperative loop has identical observable
      semantics (deferred enumeration both ways) but the imperative form
      preserves the textual shape of the Dart source and avoids LINQ's
      slightly different exception-propagation surface in case of a
      future predicate that throws. `_declarations.Values` is the
      C# `Dictionary<TKey,TValue>.Values` property returning a
      `ValueCollection` (a live view over the underlying dictionary) —
      semantically equivalent to Dart `Map.values` (also a live view).
      String equality on `decl.typeName == typeName` is ordinal under
      the cached `rf-csharp-string-equality-ordinal-by-default`
      finding: codegen MUST emit either `decl.TypeName.Equals(typeName,
      StringComparison.Ordinal)` or rely on the project-wide compiler
      convention that bare `==` on `string` is ordinal — well_typed_term
      .dart establishes the explicit-comparer convention; this file
      follows it for clarity at the equality site.
    idiom_id: null
    research_finding_id: rf-csharp-string-equality-ordinal-by-default
    nuance: >-
      Iteration order: Dart `Map.values` iterates in insertion order
      (Dart spec: `LinkedHashMap` is the default `Map` literal type);
      C# `Dictionary<TKey,TValue>` enumeration order is documented as
      undefined but is in practice insertion order in current .NET
      runtimes. This is observable here ONLY when two declarations have
      the SAME `typeName` — the Dart source returns the FIRST-inserted
      match; the C# port returns whichever the dictionary's enumerator
      yields first. The Dart source's `addDeclaration` does not dedupe
      `typeName` (only `signature`), so duplicate `typeName` is
      reachable in principle. RISK ACKNOWLEDGED — but not escalated
      because (a) the Dart source itself is implicitly order-dependent
      on a non-guaranteed iteration order ("first match" over a Map's
      values is inherently insertion-order-sensitive but Dart only
      documents insertion-order for `LinkedHashMap`, which is the
      literal default), and (b) the C# default Dictionary
      enumeration order matches it in practice. If a future caller
      requires deterministic first-match, the fix is at the caller
      (sort/filter by an explicit total order), not by switching to
      `SortedDictionary` (which would change the order, not preserve
      it). Codegen MAY annotate with a `// FIRST-match order depends on
      insertion order — both Dart Map.values and C# Dictionary.Values
      yield insertion order in practice` comment.
  - construct_key: dart.getter.iterable_keys_property
    source_form: >-
      Iterable<String> get signatures => _modes.keys;
    target_decision: >-
      Emit a C# get-only expression-bodied property returning
      `IEnumerable<string>`: `public IEnumerable<string> Signatures =>
      _modes.Keys;`. `Dictionary<TKey,TValue>.Keys` returns a
      `KeyCollection` which implements `IEnumerable<TKey>` — exact
      semantic equivalent of Dart `Map.keys` (an `Iterable<K>` view).
      Naming: Dart `signatures` getter → C# PascalCase `Signatures`
      property per .NET naming guidelines. Do NOT materialise to
      `IReadOnlyList<string>` or `List<string>` — the Dart source
      exposes a lazy live view; the C# property must preserve that
      laziness so callers do not pay a copy cost. The returned view is
      live: subsequent `addDeclaration` calls mutate the underlying
      dictionary and the view reflects that, matching Dart semantics
      exactly.
    idiom_id: null
    research_finding_id: rf-csharp-dictionary-keys-values-live-views
    nuance: >-
      Live-view-vs-snapshot is the load-bearing nuance. Both Dart
      `Map.keys`/`Map.values` and C# `Dictionary<TKey,TValue>.Keys`/
      `.Values` return live VIEWS (not snapshots) — enumeration during
      concurrent mutation throws in BOTH languages (Dart
      `ConcurrentModificationError`, C# `InvalidOperationException`
      from the enumerator). Semantics preserved. No copy introduced.
  - construct_key: dart.getter.length_proxy_to_inner_collection_length
    source_form: >-
      int get length => _modes.length;
      bool get isEmpty => _modes.isEmpty;
    target_decision: >-
      Emit two C# get-only expression-bodied properties:
      `public int Count => _modes.Count;` and `public bool IsEmpty =>
      _modes.Count == 0;`. NAMING NUANCE: Dart `length` on a `Map`
      maps idiomatically to C# `Count` on a `Dictionary` (and on every
      .NET `ICollection`) — Microsoft Learn confirms `Dictionary<TKey,
      TValue>.Count` is the documented size accessor. Do NOT name the
      property `Length` on a dictionary; `Length` in .NET is reserved
      for arrays / `string` / `StringBuilder` per the Framework Design
      Guidelines convention. Dart `isEmpty` does not have a single-token
      C# equivalent on `Dictionary` (no `IsEmpty` property is exposed);
      the documented idiom is `Count == 0`.
    idiom_id: null
    research_finding_id: rf-dart-length-isempty-to-csharp-count
    nuance: >-
      The `length` getter counts UNIQUE PREDICATES (not mode
      alternatives) per the Dart doc-comment ("Get number of unique
      predicates (not counting alternatives)"). The C# `Count` on the
      `_modes` dictionary preserves this exactly — each predicate/arity
      key appears once even when it has multiple mode alternatives in
      its `List<List<Mode>>` value. Documenting this nuance in C# XML-
      doc on the `Count` property is REQUIRED to carry the Dart doc-
      comment's load-bearing semantic forward. Both `length` and
      `isEmpty` are O(1) in both languages.
  - construct_key: dart.static_factory.classmethod_builds_instance_from_iterable
    source_form: >-
      static ModeTable fromDeclarations(List<ModeDeclaration> declarations) {
        final table = ModeTable();
        for (final decl in declarations) { table.addDeclaration(decl); }
        return table;
      }
    target_decision: >-
      Emit a public static C# method on `ModeTable`:
      `public static ModeTable FromDeclarations(IReadOnlyList
      <ModeDeclaration> declarations) { var table = new ModeTable();
      foreach (var decl in declarations) { table.AddDeclaration(decl); }
      return table; }`. Parameter typed as `IReadOnlyList
      <ModeDeclaration>` (not `List<ModeDeclaration>`, not `IEnumerable
      <ModeDeclaration>`): the caller has a fully-materialised list
      (Dart `List<ModeDeclaration>`); using `IReadOnlyList` widens
      compatibility (any list-shaped source works) without losing the
      "fully enumerated" guarantee the static factory expects. The
      method body is a verbatim imperative loop, NOT a LINQ chain —
      Dart `for ... in` over a `List<T>` and C# `foreach` over an
      `IReadOnlyList<T>` have identical observable semantics, and
      `AddDeclaration` has side-effects (mutates two dictionaries) so
      LINQ aggregation would be misleading. The Dart `static` →
      C# `public static`; `fromDeclarations` → `FromDeclarations`
      (PascalCase). This is a STATIC FACTORY, not a constructor — the
      cached `rf-dart-factory-ctor-const-default-to-csharp-static-
      factory` finding (well_typed_term.dart) is the closest analogue
      but is specifically about `factory` constructors with `const`
      defaults; this is a plain `static` method building a fresh
      instance. The plainer `rf-dart-top-level-function-to-csharp-
      static-method` finding (mode.dart, moded_term.dart) covers the
      "static method on a host type" target half, but the Dart source
      here is already inside a class, not at top level.
    idiom_id: null
    research_finding_id: rf-csharp-static-factory-method-from-iterable
    nuance: >-
      The factory shares no state with the surrounding type — it
      constructs a fresh `ModeTable` and returns it. No threading
      hazard (purely synchronous, no `async`/`Future`). The
      `AddDeclaration` calls inside the loop alias each `decl`
      reference into both inner dictionaries (per the
      `_declarations.putIfAbsent(...).add(decl)` semantics already
      specified); no deep copy is introduced because the Dart source
      does not perform one (preserves the
      `dart-list-element-value-equality` aliasing precedent from
      type_ast.dart and the errors.dart spec's same pattern).
conversion_units:
  - "enum Mode { Reader, Writer } (pmt-namespace; value type; two members in source order; PascalCase per .NET naming guidelines)"
  - "class ModeTable (reference type; private readonly Dictionary<string, List<List<Mode>>> _modes = new(StringComparer.Ordinal); private readonly Dictionary<string, List<ModeDeclaration>> _declarations = new(StringComparer.Ordinal))"
  - "method AddDeclaration(ModeDeclaration decl) (two parallel TryGetValue-then-Add-then-append blocks, _modes first then _declarations; modes List<Mode> built from decl.Args via isReader ternary)"
  - "method GetModes(string predicate, int arity) -> List<Mode>? (TryGetValue + Count==0 guard, return [0] or null)"
  - "method GetAllModes(string predicate, int arity) -> List<List<Mode>>? (TryGetValue + nullable return; signature interpolation $\"{predicate}/{arity}\")"
  - "method HasDeclaration(string predicate, int arity) -> bool (expression-bodied; _modes.ContainsKey)"
  - "method HasMultipleModes(string predicate, int arity) -> bool (TryGetValue + Count > 1)"
  - "method GetDeclaration(string predicate, int arity) -> ModeDeclaration? (TryGetValue + Count==0 guard, return [0] or null)"
  - "method GetAllDeclarations(string predicate, int arity) -> List<ModeDeclaration>? (TryGetValue + nullable return)"
  - "method GetDeclarationByTypeName(string typeName) -> ModeDeclaration? (nested foreach over _declarations.Values then inner list; early-return; bare == on string with ordinal-by-project-convention)"
  - "property Signatures -> IEnumerable<string> (live KeyCollection view; expression-bodied; PascalCase rename)"
  - "property Count -> int (Dart length; live; renamed Length->Count per .NET convention; XML-doc preserves 'unique predicates, not counting alternatives')"
  - "property IsEmpty -> bool (expression-bodied _modes.Count == 0)"
  - "static method FromDeclarations(IReadOnlyList<ModeDeclaration> declarations) -> ModeTable (foreach + AddDeclaration; new ModeTable() instance)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

All nine non-trivial constructs in this file are resolved against cached
rf-* findings from the prior 018 convspec corpus (FR-024: never re-research;
cached findings reused verbatim), with two FRESH rf-* findings recorded for
constructs not previously documented in this codebase's KB. Every
construct records BOTH a deep-analysis basis AND a researched-pattern
basis per SC-006 / US2-AS4. Zero escalations.

### rf-dart-enum-plain-to-csharp-enum (CACHED, reused from message_queue.dart)

- Deep analysis: `Mode` has two members (`reader`, `writer`), no
  constructor, no instance members, no methods, no associated values, no
  toString override. The only uses observed are (a) building a `List<Mode>`
  via the ternary `arg.isReader ? Mode.reader : Mode.writer` at line 38
  and (b) storing/returning these values via the two parallel dictionaries.
  No `switch` over `Mode` (so the mode.dart-style exhaustiveness-restoring
  default arm is NOT needed here). Textbook plain Dart enum.
- Authoritative Dart: dart.dev/language/enums — plain `enum` is "a closed,
  fixed set of constant values" with no enhanced-enum facilities.
- Authoritative .NET: learn.microsoft.com C# enum reference — "value type
  defined by a set of named constants of the underlying integral numeric
  type"; default underlying type `int`; PascalCase member names per the
  enumeration naming guidelines doc.
- Authoritative both sides; no escalation. The cached message_queue.dart
  finding applies verbatim — this Mode is structurally identical to the
  MessageType enum it covers (two members, no instance behaviour,
  boolean-style discrimination).

### rf-csharp-string-equality-ordinal-by-default (CACHED, reused from program_dfa.dart / well_typed_term.dart)

- Deep analysis: the keying string is `'$predicate/$arity'` — pure ASCII
  identifier joined by a literal `/`. There is no Unicode, no
  capitalisation variation, no culture-sensitive character. Two
  separately constructed dictionaries (`_modes`, `_declarations`) MUST
  agree on key equality so that `addDeclaration`'s two writes target
  parallel buckets. Project precedent (carried forward from well_typed_term
  .dart §"Dictionary string-key ordinal comparer") MANDATES explicit
  `StringComparer.Ordinal` on every `Dictionary<string, V>` construction.
- Authoritative .NET: learn.microsoft.com/en-us/dotnet/api/system.collections
  .generic.dictionary-2 — Dictionary constructor accepts an
  `IEqualityComparer<TKey>`; default is `EqualityComparer<TKey>.Default`.
  For `string` the default IS ordinal in current .NET runtimes but the
  documented best practice (Microsoft Learn "Comparing strings") is to
  pass `StringComparer.Ordinal` explicitly when ordinal semantics are
  required, making the intent visible and immune to ambient-culture
  changes.
- Authoritative Dart: dart.dev `Map<K,V>` reference — `Map` literal `{}`
  yields a `LinkedHashMap<K,V>` with `K`-default equality; for `String`
  that is code-unit-by-code-unit (ordinal-equivalent, never culture-
  sensitive). Direct semantic match under explicit `StringComparer
  .Ordinal`.
- Authoritative both sides; no escalation. Cached.

### rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add (CACHED, reused from well_typed_term.dart)

- Deep analysis: `addDeclaration` performs two parallel `putIfAbsent`
  calls, each followed by `.add(...)` on the returned bucket. The Dart
  closure `() => []` is invoked ONLY on a cache miss (lazy default
  construction). The C# pattern must preserve laziness — the empty list
  is constructed only on the miss arm.
- Authoritative Dart: dart.dev `Map.putIfAbsent` — "Look up the value of
  key, or add a new entry if it isn't there. Returns the value associated
  to key, if there is one. If the key is not present, calls ifAbsent to
  get a new value, then associates key to that value..."
- Authoritative .NET: learn.microsoft.com `Dictionary<TKey,TValue>
  .TryGetValue` — "Gets the value associated with the specified key. ...
  Returns true if the Dictionary<TKey,TValue> contains an element with the
  specified key; otherwise, false." No single-call equivalent to
  putIfAbsent in `Dictionary<TKey,TValue>` — the documented canonical
  idiom is `TryGetValue` + conditional construction + indexer assignment
  (or, .NET 6+, `CollectionsMarshal.GetValueRefOrAddDefault`; rejected
  here per project precedent — both well_typed_term.dart and
  message_queue.dart reject it as outside scope).
- Authoritative both sides; cached. The `add(...)` chain ON the returned
  bucket is expanded into a separate `.Add(...)` statement on the named
  local in C# — the C# pattern is verbose but mechanically equivalent.

### rf-csharp-interpolated-string-equivalent-to-dart-interpolation (CACHED, reused from param_expansion.dart)

- Deep analysis: six sites of `'$predicate/$arity'` interpolation — a
  pure-positional two-hole interpolation of an identifier and a small
  non-negative integer. No `${expr}` complex bracing, no format
  specifier, no padding.
- Authoritative Dart: dart.dev — string interpolation syntax `$identifier`
  and `${expression}` produce a `String` by invoking each interpolated
  expression's `toString` and concatenating.
- Authoritative .NET: learn.microsoft.com/en-us/dotnet/csharp/language-
  reference/tokens/interpolated — `$"..."` interpolated strings produce a
  `string` by invoking each interpolated expression's `ToString` (with
  optional format specifier). For `int` the default format is the
  invariant decimal-digit form for small values.
- Authoritative both sides; cached. Verbatim semantic equivalence at all
  six call sites.

### rf-csharp-dictionary-trygetvalue-then-fallback-null (NEW)

- Deep analysis: three accessors in this file follow the pattern
  `final allXs = _xs[key]; return allXs?.isNotEmpty == true ?
  allXs!.first : null;` — `getModes`, `getDeclaration` (and the simpler
  `getAllModes`/`getAllDeclarations` use `_xs[key]` directly, which Dart
  returns `null` for on a miss without throwing). The Dart `Map<K,V>`
  indexer is a TOTAL function returning `V?`; the C# `Dictionary<TKey,
  TValue>` indexer is a PARTIAL function THROWING
  `KeyNotFoundException` on a miss. Silently translating `_modes[k]` to
  the C# `_modes[k]` indexer would change observable behaviour from
  "never throws, may return null" to "throws on miss" — a critical
  semantic break.
- Authoritative .NET (WebFetch
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.item):
  Dictionary item indexer — "Gets or sets the value associated with the
  specified key. ... Property value: The value associated with the
  specified key. If the specified key is not found, a get operation
  throws a KeyNotFoundException, and a set operation creates a new
  element with the specified key." Decisive: the C# indexer is NOT a
  drop-in for Dart Map indexer. WebFetch
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.trygetvalue
  documents the safe equivalent: `TryGetValue(TKey key, out TValue
  value)`.
- Authoritative Dart (WebFetch
  https://api.dart.dev/stable/dart-core/Map/operator_get.html): Dart
  `Map<K,V>.operator []` returns `V?` and is documented as "Returns
  null if the map does not contain the key" — explicitly total.
- Authoritative both sides; recorded as a NEW finding (the `Map[k]` →
  `TryGetValue` translation has not previously been called out in the
  rf-* corpus; well_typed_term.dart's `putIfAbsent` finding is adjacent
  but distinct — putIfAbsent is the WRITE path, this is the READ path).
  Codegen MUST use TryGetValue at every Dart `Map<K,V>` indexer READ
  site in this file (and any subsequent file referencing this idiom).

### rf-dart-map-containskey-to-csharp-dictionary-containskey (NEW)

- Deep analysis: `hasDeclaration` is a one-liner `return _modes.containsKey
  ('$predicate/$arity');`. The Dart `Map<K,V>.containsKey` is a total
  predicate; the C# `Dictionary<TKey,TValue>.ContainsKey` is also total.
  Direct semantic equivalence — but a fresh finding is recorded because
  no prior rf-* covers it (the prior corpus covered the `putIfAbsent` write
  path and the indexer-with-fallback read path, but never the boolean
  membership check).
- Authoritative .NET (WebFetch
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.containskey):
  Dictionary.ContainsKey — "Determines whether the Dictionary<TKey,
  TValue> contains the specified key. ... Returns: true if the
  Dictionary<TKey,TValue> contains an element with the specified key;
  otherwise, false."
- Authoritative Dart (WebFetch
  https://api.dart.dev/stable/dart-core/Map/containsKey.html):
  Map.containsKey — "Whether this map contains the given key. Returns
  true if any of the keys in the map are equal to key according to the
  equality used by the map."
- Authoritative both sides; recorded as a NEW finding. Direct one-line
  translation, no exception-throw asymmetry, no null-safety issue.

### rf-csharp-dictionary-keys-values-live-views (NEW)

- Deep analysis: two surfaces expose dictionary internals as iterables:
  `Iterable<String> get signatures => _modes.keys;` and the nested-foreach
  in `getDeclarationByTypeName` consuming `_declarations.values`. Both
  Dart and C# return LIVE VIEWS (not snapshots) from these accessors —
  enumeration during concurrent mutation throws. Preserving liveness
  matters because callers may legitimately rely on it (e.g. to test
  emptiness without paying a copy cost, or to drive a streaming
  consumer).
- Authoritative .NET (WebFetch
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.keys):
  Dictionary.Keys — "Gets a collection containing the keys in the
  Dictionary<TKey,TValue>. ... The order of the keys in the
  Dictionary<TKey,TValue>.KeyCollection is unspecified. ... The returned
  collection is not a static copy; instead, the KeyCollection refers
  back to the keys in the original Dictionary<TKey,TValue>. Therefore,
  changes to the Dictionary<TKey,TValue> continue to be reflected in
  the KeyCollection." Decisive on liveness. Same doc for `.Values`.
- Authoritative Dart (WebFetch
  https://api.dart.dev/stable/dart-core/Map/keys.html and .../values
  .html): Map.keys / Map.values — "The keys of this. The returned
  iterable has efficient methods for fetching the elements ... iterating
  the returned iterable while the map is modified causes a
  ConcurrentModificationError." Live view; concurrent-modification
  surfaces as an exception.
- Authoritative both sides; recorded as a NEW finding. The `Signatures`
  property returns `IEnumerable<string>` (not `IReadOnlyCollection
  <string>` and not `List<string>`) to preserve the lazy-live shape —
  callers that need a snapshot can materialise themselves.

### rf-dart-length-isempty-to-csharp-count (NEW)

- Deep analysis: Dart `length` getter on `Map` → C# `Count` property on
  `Dictionary` (NOT `Length` — `Length` is reserved for arrays / `string`
  / `StringBuilder` per .NET Framework Design Guidelines naming
  convention). Dart `isEmpty` getter on `Map` has NO single-token C#
  equivalent on `Dictionary` — the documented idiom is `Count == 0`.
  This is a NAMING NUANCE worth recording as its own finding because it
  recurs across every Dart→C# collection-wrapper class in this corpus
  (and earlier files have used `length`/`isEmpty` ad-hoc without a
  documented rf-* anchor).
- Authoritative .NET (WebFetch
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.count):
  Dictionary.Count — "Gets the number of key/value pairs contained in
  the Dictionary<TKey,TValue>." O(1). WebFetch
  https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-type-members
  — Framework Design Guidelines: `Count` is the recommended name for
  collection-size accessors on non-array types; `Length` is reserved
  for arrays, strings, and buffers measured in elements/characters.
- Authoritative Dart (WebFetch
  https://api.dart.dev/stable/dart-core/Map/length.html and .../isEmpty
  .html): Map.length — "The number of key/value pairs in the map."
  Map.isEmpty — "Whether there is no key/value pair in the map."
- Authoritative both sides; recorded as a NEW finding. The Dart doc-
  comment "Get number of unique predicates (not counting alternatives)"
  is a load-bearing SEMANTIC nuance (a predicate with three mode
  alternatives counts as ONE here, not three) — codegen MUST carry this
  doc-comment forward into the C# `Count` property's XML-doc verbatim.

### rf-csharp-static-factory-method-from-iterable (NEW)

- Deep analysis: `static ModeTable fromDeclarations(List<ModeDeclaration>
  declarations)` is a plain static method that constructs a fresh
  `ModeTable`, iterates the input, and returns the populated instance.
  Not a `factory` constructor (no `const` defaults; no Dart `factory`
  keyword); not a top-level function (lives on the class). The closest
  prior rf-* finding `rf-dart-factory-ctor-const-default-to-csharp-
  static-factory` (well_typed_term.dart) covers Dart `factory` ctors
  with const defaults — STRUCTURALLY ADJACENT but not the same idiom;
  this is plainer.
- Authoritative .NET (WebFetch
  https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/member):
  Framework Design Guidelines member design — "Consider providing
  simple methods that create instances of types ... A factory method
  is a static method that returns an instance of the class or struct
  it belongs to." Recommended naming `From<X>` for "construct from X
  source" — directly applicable to `FromDeclarations`.
- Authoritative Dart (WebFetch
  https://dart.dev/language/constructors): Dart static methods on a
  class are syntactically distinct from factory constructors; Dart
  static methods are invoked via `ClassName.methodName(...)` and
  return whatever the method specifies — no const-defaults or
  redirection rules apply. This file's `fromDeclarations` is a plain
  static method, not a factory constructor.
- Authoritative both sides; recorded as a NEW finding. The
  `From<X>(IReadOnlyList<X>)` shape is the canonical .NET static-factory
  idiom for this exact construct (iterable in, instance out). Distinct
  from the `rf-dart-factory-ctor-const-default-to-csharp-static-factory`
  finding which targets Dart's `factory` keyword.

## Notes

- The two enums named `Mode` in the source tree (this file vs. lib/analysis
  /type_checker/mode.dart) are unrelated semantically (argument-direction
  marker vs. typing-mode marker) and live in different directory paths.
  Codegen places each in a namespace mirroring its directory
  (`<root>.Compiler.Pmt.Mode` and `<root>.Analysis.TypeChecker.Mode`), so
  no C# type-name collision arises despite the shared identifier. No
  escalation.
- No isolates, no async/Stream/Future, no late, no sealed hierarchies, no
  inheritance — `ModeTable` is a leaf class. Those well-known nuances are
  absent and correctly not asserted.
- `ModeDeclaration` is referenced via `import '../ast.dart'` — the C#
  counterpart's shape (PascalCase `Signature` / `Args` / `TypeName` /
  `IsReader` on `ModedArg`) is fixed by the ast.dart conversion spec and
  not respecified here. This file's spec is correct under the assumption
  the ast.dart spec produces the expected PascalCase property surface
  (cross-file invariant — both specs share that surface).
- Reference-aliasing: every `ModeDeclaration` reference handed to
  `addDeclaration` is stored directly in `_declarations[signature]`
  (no copy). The C# port preserves the same aliasing. `ModeDeclaration`
  is itself immutable (per ast.dart conversion: `final` Dart fields →
  get-only C# properties), so the aliasing is benign — the table's
  internal state cannot drift due to caller mutation of a stored
  declaration.
- Iteration-order risk in `getDeclarationByTypeName` is acknowledged in
  the construct's nuance field and explicitly NOT escalated; the Dart
  source already depends on the same insertion-order assumption that
  current .NET `Dictionary<TKey,TValue>` enumeration also satisfies in
  practice. A documenting C# `//` comment is recommended at the call
  site.
- Two FRESH rf-* findings address READ-path Map idioms previously
  uncovered: `rf-csharp-dictionary-trygetvalue-then-fallback-null` and
  `rf-dart-map-containskey-to-csharp-dictionary-containskey`. Two more
  cover collection-class surface conventions newly needed:
  `rf-csharp-dictionary-keys-values-live-views`,
  `rf-dart-length-isempty-to-csharp-count`. One covers the static-
  factory idiom distinct from the `factory`-ctor variant:
  `rf-csharp-static-factory-method-from-iterable`. Each grounded in
  authoritative Microsoft Learn + dart.dev WebFetches.
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart and/or .NET official documentation, with deep-
  analysis AND researched-pattern bases recorded (SC-006); recurring
  constructs route through cached rf-* findings (SC-007).
