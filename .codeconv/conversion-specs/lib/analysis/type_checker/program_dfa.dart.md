# Conversion Spec — lib/analysis/type_checker/program_dfa.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/program_dfa.dart
source_sha256: bf0151e2d78f26961d8153beede8211ba2f823b127de7ec7fd673299658a6057
target_code_unit: lib/analysis/type_checker/program_dfa.cs
constructs:
  - construct_key: dart.value_class.manual_eq_hashcode_two_field_state
    source_form: >-
      class DFAState { final String baseName; final bool isDual; final bool isFinal;
      final bool isProcedure; DFAState(this.baseName, {required this.isDual,
      required this.isFinal, this.isProcedure=false}); @override bool operator==
      (Object other) => other is DFAState && other.baseName==baseName &&
      other.isDual==isDual; @override int get hashCode => Object.hash(baseName,
      isDual); }
    target_decision: >-
      Emit `sealed class DFAState : IEquatable<DFAState>` (NOT a `record` — see
      nuance) with four read-only auto-properties `BaseName` (string),
      `IsDual` (bool), `IsFinal` (bool), `IsProcedure` (bool), set via a single
      constructor `public DFAState(string baseName, bool isDual, bool isFinal,
      bool isProcedure = false)`. Override `Equals(object?)` /
      `Equals(DFAState?)` / `GetHashCode()` hand-written to mirror Dart
      *exactly*: equality and hash use ONLY `BaseName` (ordinal) + `IsDual` —
      `IsFinal`/`IsProcedure` are intentionally EXCLUDED from identity
      (matching Dart `==`). This is deliberate so a "Stream" state equals
      another "Stream" state regardless of how it was constructed. `BaseName`
      compares with `StringComparer.Ordinal` (state names are exact tokens like
      `"Integer"`, `"_FINAL_"`). `GetHashCode` reproduces `Object.hash(baseName,
      isDual)` via `HashCode.Combine(BaseName, IsDual)`. A C# positional
      `record` is REJECTED because the synthesized record equality uses *all*
      declared positional members, which would silently include
      `IsFinal`/`IsProcedure` and change identity semantics.
    idiom_id: dart-value-class-partial-equality-to-csharp-iequatable
    research_finding_id: rf-csharp-record-uses-all-members-equality
    nuance: >-
      Value-vs-reference + partial-equality. Both Dart `class` (with overridden
      `==`) and C# `class` are reference types, but with overridden equality
      they behave as *value-equal*. The load-bearing nuance is that Dart's
      `==` here is a STRICT SUBSET of the fields (only `baseName`/`isDual`):
      two states with identical name+dual but different `isFinal` ARE equal in
      Dart, and this is intentional (the `dual` getter returns a new state
      sharing the same `isFinal`/`isProcedure` flags, so the asymmetry never
      surfaces in correct callers — but DFA states are keyed in `Map<String,
      DFAState>` *by full name*, so equality is consulted on tuple-keys not on
      map keys). A C# `record` would silently widen equality to four fields,
      producing the very kind of subtle equality regression rf-csharp-record-
      uses-all-members-equality documents. Ordinal string comparison is
      mandated (StringComparer.Ordinal) — the same nuance applied in
      type_ast.dart's rf-dart-const-set-to-csharp-frozenset-ordinal.
  - construct_key: dart.value_class.derived_getter_returns_new_instance
    source_form: >-
      DFAState get dual => DFAState(baseName, isDual: !isDual, isFinal: isFinal,
      isProcedure: isProcedure);
    target_decision: >-
      Expose as a read-only property `public DFAState Dual` (expression-bodied
      getter) that returns a freshly-constructed `DFAState` with `IsDual`
      flipped and the other three fields copied. NOT a method `Dual()` —
      preserves the Dart getter shape and the zero-arg, side-effect-free,
      pure-computation contract. Each access allocates a new instance (Dart
      semantics — no caching in source); document this so a future caller does
      NOT rely on `state.Dual == state.Dual` being reference-identical (it is
      value-equal via the IEquatable override but not reference-equal).
    idiom_id: dart-pure-getter-returns-new-instance-to-csharp-property
    research_finding_id: rf-csharp-property-vs-method-pure-getter
    nuance: >-
      A Dart instance getter with no observable side-effect maps to a C#
      property (Microsoft Learn property guidance: "use a property when ... the
      member represents a logical attribute and the get accessor has no
      observable side effect"). `Dual` is *pure* but ALLOCATES on each call —
      this is identical to the Dart source semantics. Reference-vs-value note:
      because `DFAState` overrides equality on `(BaseName,IsDual)`, two
      independently-allocated `Dual` instances ARE equal by `Equals` even
      though they are distinct references; downstream uses (transition-table
      keys) consume `Equals`, so behaviour is preserved.
  - construct_key: dart.value_class.cluster_of_boolean_classifier_getters
    source_form: >-
      bool get isWildcard => baseName == '_'; bool get isProducedWildcard =>
      baseName == '_' && !isDual; bool get isConsumedWildcard => baseName ==
      '_' && isDual; bool get isIntegerType => baseName == 'Integer'; ... bool
      get isAnonymousFinal => baseName == '_FINAL_'; bool get isNumericType =>
      isIntegerType || isRealType || isNumberType; bool get isPrimitiveType =>
      isWildcard || isIntegerType || ...; bool get isUserDefinedType =>
      !isPrimitiveType && !isProcedure && !isAnonymousFinal;
    target_decision: >-
      Emit each as an expression-bodied `public bool IsWildcard => BaseName ==
      "_";` etc., on `DFAState`. ALL string literals in the comparisons
      (`"_"`, `"Integer"`, `"Real"`, `"Number"`, `"String"`, `"_FINAL_"`)
      MUST use ordinal comparison — `BaseName == "Integer"` in C# uses the
      string `==` operator which is documented (Microsoft Learn String
      equality) to perform an *ordinal* comparison, so the literal-equality
      idiom maps 1:1 with no comparer-injection needed (unlike collection
      contains). The composite getters (`IsNumericType`, `IsPrimitiveType`,
      `IsUserDefinedType`) become expression-bodied properties that compose
      the leaf properties exactly as in Dart — preserving short-circuit
      evaluation order, which matters because `IsUserDefinedType` deliberately
      gates on three negations.
    idiom_id: dart-boolean-classifier-getter-to-csharp-expression-property
    research_finding_id: rf-csharp-string-equality-ordinal-by-default
    nuance: >-
      C# `string ==` is ordinal by default (Microsoft Learn: "Equality
      operators ... perform an ordinal comparison"), matching Dart's `String
      ==`. So literal-token classifiers translate without a comparer. The
      cluster is intentionally kept as *separate small properties* rather than
      collapsed into a single enum — Dart code reads these idiomatically and
      callers consume them in many combinations (test-then-act flow); C#
      property dispatch is free (JIT-inlined for trivial getters) so no
      performance reason to consolidate. `_FINAL_` and `_` are
      reserved-marker tokens, not user-supplied — case sensitivity is
      load-bearing and ordinal preserves it.
  - construct_key: dart.value_class.private_named_factory_constructors
    source_form: >-
      class TransitionLabel { final String symbol; final int arity; final int
      argIndex; final Mode? mode; TransitionLabel._(this.symbol, this.arity,
      this.argIndex, this.mode); factory TransitionLabel.functor(String name,
      int arity, int argIndex, {Mode? mode}) => TransitionLabel._(name, arity,
      argIndex, mode); factory TransitionLabel.constant(Object value) =>
      TransitionLabel._(value.toString(), 0, 0, null); }
    target_decision: >-
      Emit `sealed class TransitionLabel : IEquatable<TransitionLabel>` with
      four read-only auto-properties (`Symbol` string, `Arity` int, `ArgIndex`
      int, `Mode? Mode` nullable enum). The Dart private generative
      constructor `_(...)` becomes a `private TransitionLabel(string symbol,
      int arity, int argIndex, Mode? mode)` constructor. The two Dart
      `factory` constructors become two `public static TransitionLabel
      Functor(string name, int arity, int argIndex, Mode? mode = null)` and
      `public static TransitionLabel Constant(object value)` static factory
      methods (C# has no `factory` keyword; static-factory is the canonical
      mapping, per the rf-dart-factory-ctor-const-default-to-csharp-static-
      factory finding cached in type_ast.dart). `Constant` calls
      `value.ToString() ?? string.Empty` to mirror Dart `value.toString()`
      (which is never null for non-null `Object`). The Dart property name
      `mode` collides with the enum type name `Mode`; the spec records the
      property must be named `Mode` (preserving Dart shape) and is
      disambiguated by C# member-access rules (`label.Mode` is the property;
      `Mode.Output` is the enum-qualified value).
    idiom_id: dart-factory-ctor-const-default-to-csharp-static-factory
    research_finding_id: rf-dart-factory-ctor-const-default-to-csharp-static-factory
    nuance: >-
      Dart `factory` constructors map to C# static factory methods (no
      language `factory`); identical to the cached finding from type_ast.dart.
      Two non-trivial sub-nuances here: (1) Dart `Mode?` (nullable enum) → C#
      `Mode?` (System.Nullable<Mode>) — value-type nullable, NOT a reference
      nullable — which is the correct semantic match (an enum is a value
      type, so the null state is an absent-mode marker, exactly Dart's
      meaning). (2) `Object` (Dart) → `object` (C#) for the `value`
      parameter of `Constant`; `value.toString()` (Dart, total) maps to
      `value?.ToString() ?? ""` or `value.ToString() ?? ""` depending on the
      C# null-state inference — the spec requires a defensive `?? ""` to
      preserve totality even if `ToString` returns null (allowed by C#'s
      annotated signature) where Dart's Object.toString is total.
  - construct_key: dart.value_class.full_field_equality_with_nullable_enum
    source_form: >-
      @override bool operator ==(Object other) => other is TransitionLabel &&
      other.symbol==symbol && other.arity==arity && other.argIndex==argIndex &&
      other.mode==mode; @override int get hashCode => Object.hash(symbol,
      arity, argIndex, mode);
    target_decision: >-
      Override `Equals(object?)`/`Equals(TransitionLabel?)`/`GetHashCode()`
      hand-written, comparing all four fields (`Symbol` ordinal, `Arity`,
      `ArgIndex`, `Mode`). The nullable enum `Mode?` is compared with C# `==`
      which handles `null == null` and `null == Output` correctly (lifted
      equality on `Nullable<T>`). Hash: `HashCode.Combine(Symbol, Arity,
      ArgIndex, Mode)`. `IEquatable<TransitionLabel>` is implemented so
      `Dictionary<TransitionLabel, ...>` and `HashSet<TransitionLabel>` use
      the typed overload directly (no boxing of the value-type-shape value).
      Same rationale as DFAState: `record` is REJECTED here, but for a
      different reason — `record` synthesized equality WOULD include all four
      members faithfully, but the codebase convention (set in type_ast.dart
      for TypeRef) is to hand-write equality on AST/value nodes for
      consistency and to make the equality fields *explicitly visible* in
      review. The class is sealed (closed leaf in the conceptual hierarchy)
      and reference-typed (not `record struct`) because instances are stored
      as tuple-keys (`(DFAState, TransitionLabel)`) in dictionaries shared
      across automata — copying as value would explode allocations.
    idiom_id: dart-value-class-full-field-equality-to-csharp-iequatable
    research_finding_id: rf-csharp-nullable-value-type-lifted-equality
    nuance: >-
      Lifted equality on `Mode?` is the load-bearing nuance: C# `Nullable<T>`
      `==` is defined so `null == null` is true, `null == anyValue` is false,
      and `value1 == value2` defers to the underlying `T.Equals` (Microsoft
      Learn nullable value types: "Both equality and inequality operators ...
      compare two operands of Nullable<T>"). This matches Dart `==` on
      `Mode?` exactly. `HashCode.Combine` accepts a nullable enum: a `null`
      `Mode?` is hashed deterministically as 0 (well-defined behaviour),
      reproducing Dart `Object.hash(...,null)` semantics for the null arm.
  - construct_key: dart.value_class.derived_property_flipping_optional_field
    source_form: "TransitionLabel get dual => TransitionLabel._(symbol, arity, argIndex, mode?.flip);"
    target_decision: >-
      `public TransitionLabel Dual` read-only property; body
      `=> new TransitionLabel(Symbol, Arity, ArgIndex, Mode?.Flip())`. The
      Dart null-aware operator `?.` maps DIRECTLY to C# `?.` — both languages
      use the identical token with identical "evaluate target only if
      receiver is non-null, else short-circuit to null" semantics. `Flip()`
      on `Mode` (or `Mode.Flip()` as an extension from mode.cs) returns
      `Mode`; through `?.` the expression has type `Mode?` (null propagates),
      which is exactly what the constructor expects. NOT a method `Dual()` —
      mirrors the Dart getter (zero-arg pure derivation), same idiom as
      DFAState.Dual.
    idiom_id: dart-pure-getter-returns-new-instance-to-csharp-property
    research_finding_id: rf-dart-csharp-null-aware-call-operator-identical
    nuance: >-
      `?.` is one of the small set of operators that are syntactically AND
      semantically identical in Dart and C# (Microsoft Learn null-conditional
      operators: "operators apply a member access ... operation to its
      operand only if that operand evaluates to non-null; otherwise, it
      returns null"); dart.dev: "The ?. operator is like ., except that the
      leftmost operand can be null". This is one of the cases where the
      conversion is genuinely trivial *because* the languages agree.
      Recorded explicitly so callers see we examined and confirmed the
      nuance, not skipped it (US2-AS4).
  - construct_key: dart.tostring.conditional_format_via_interpolation
    source_form: >-
      @override String toString() { if (arity == 0) return symbol; final modeStr
      = mode != null ? ':${mode == Mode.produce ? '↑' : '↓'}' : ''; return
      '$symbol($arity,$argIndex)$modeStr'; }  (and DFAState.toString =>
      isDual ? '$baseName?' : baseName)
    target_decision: >-
      `public override string ToString()` on both DFAState and
      TransitionLabel. DFAState: `=> IsDual ? $"{BaseName}?" : BaseName`
      (expression-bodied, ternary). TransitionLabel: a statement body that
      reproduces the Dart logic verbatim: constant-arity short-circuit
      returns `Symbol`; otherwise build `modeStr` via a ternary
      (`Mode is null ? "" : Mode == ModeAliases.Produce ? ":↑" : ":↓"`) and
      return `$"{Symbol}({Arity},{ArgIndex}){modeStr}"`. The Unicode arrows
      `↑` (U+2191) / `↓` (U+2193) are preserved as literal UTF-16 string
      content in the C# source file (the file MUST be saved UTF-8 with BOM
      or declared `// <auto-generated>` with UTF-8 — the convspec records the
      encoding requirement, the codegen stage enforces it). `Mode.produce` is
      a static-const alias in Dart (mode.dart finding); the C# equivalent is
      `ModeAliases.Produce` (per mode.dart spec) — NOT raw `Mode.Output`,
      because the source uses the alias and the spec preserves source intent.
    idiom_id: dart-tostring-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-csharp-interpolated-string-equivalent-to-dart-interpolation
    nuance: >-
      Dart `'$x'` and C# `$"{X}"` are documented-equivalent string-
      interpolation forms (Microsoft Learn $-strings: "interpolation
      expressions [are] enclosed in braces ({ and })"); the conversion is
      mechanical. The two non-trivial sub-nuances are (i) Unicode-character
      preservation (`↑`/`↓` are non-ASCII; UTF-8-with-BOM or explicit
      encoding declaration is required to round-trip) and (ii) preservation
      of the Dart alias `Mode.produce` as `ModeAliases.Produce` rather than
      "optimising" to the underlying enum constant — keeps the conversion
      transparent against the cached mode.dart conversion-unit.
  - construct_key: dart.value_class.tuple_record_key_in_map
    source_form: >-
      final Map<(DFAState, TransitionLabel), DFAState> _transitions; (used as
      _transitions[(from, label)], _transitions.entries iterates `(from, label)
      → to`)
    target_decision: >-
      Emit `private readonly Dictionary<(DFAState From, TransitionLabel
      Label), DFAState> _transitions;` — using C# native value tuples
      `(DFAState, TransitionLabel)` as the dictionary key, matching Dart 3
      record types `(DFAState, TransitionLabel)` 1:1. C# value-tuple equality
      is defined element-wise (Microsoft Learn tuple types: "the assigned
      tuple and the tuple to which values are assigned must have the same
      number of elements ... corresponding element of the right-hand-side
      tuple") — for use as a dictionary key, each element's `Equals`/
      `GetHashCode` is consulted, which is exactly the hand-written
      DFAState/TransitionLabel equality above. Field name preserved as
      `_transitions` (C# convention allows underscore-prefix on private
      fields, matches Dart convention). NOT a custom struct/record — C#
      ValueTuple is the documented direct counterpart of Dart records.
    idiom_id: dart-record-tuple-key-to-csharp-valuetuple
    research_finding_id: rf-dart3-record-to-csharp-valuetuple
    nuance: >-
      Dart 3 records and C# ValueTuple are the closest cross-language analog:
      both are structural lightweight value composites with positional fields
      and structural equality. The load-bearing nuance is that ValueTuple
      equality DELEGATES to each component's `Equals` — so the hand-written
      DFAState `==` (which uses only BaseName+IsDual) and TransitionLabel
      `==` (all four fields) are the actual identity for the dictionary key.
      Reference-typed components (DFAState, TransitionLabel are classes, not
      structs) stored in a ValueTuple are stored by reference inside the
      tuple — no copying, consistent with Dart record-of-objects semantics.
  - construct_key: dart.collection.map_dict_clone_with_transformed_keys_and_values
    source_form: >-
      Automaton get dual { final newTransitions = <(DFAState, TransitionLabel),
      DFAState>{}; for (final entry in _transitions.entries) { final (fromState,
      label) = entry.key; final toState = entry.value;
      newTransitions[(fromState.dual, label.dual)] = toState.dual; } return
      Automaton(startState.dual, newTransitions, acceptedPrimitives:
      acceptedPrimitives); }
    target_decision: >-
      Body: allocate `var newTransitions = new Dictionary<(DFAState,
      TransitionLabel), DFAState>(_transitions.Count);` (pre-sized for one
      allocation), then `foreach (var entry in _transitions) {
      newTransitions[(entry.Key.From.Dual, entry.Key.Label.Dual)] =
      entry.Value.Dual; }`, then return `new Automaton(StartState.Dual,
      newTransitions, AcceptedPrimitives)`. The Dart pattern destructure
      `final (fromState, label) = entry.key` maps to C# tuple-deconstruction
      `var (fromState, label) = entry.Key;` (C# supports this on
      ValueTuple). `acceptedPrimitives` is passed verbatim — alias the same
      reference (shared shallow copy), matching Dart constructor parameter
      semantics where the caller passes the existing set. LINQ
      `.ToDictionary(...)` is REJECTED here: the imperative `foreach` is
      lower-allocation and matches the Dart loop shape exactly (the codebase
      convention from type_ast.dart's rf-dart-map-spread-merge-to-csharp-
      dictionary-upsert: prefer explicit upsert over LINQ for clarity).
    idiom_id: dart-map-iter-transform-to-csharp-foreach-upsert
    research_finding_id: rf-csharp-dictionary-foreach-iteration-keyvaluepair
    nuance: >-
      Dart `Map.entries` yields `MapEntry<K,V>` with `.key`/`.value`; C#
      `Dictionary<K,V>` `foreach` yields `KeyValuePair<K,V>` with
      `.Key`/`.Value` (Microsoft Learn Dictionary: "If you use foreach to
      iterate ... you'll see ... a KeyValuePair<TKey,TValue>"). Both iterate
      in undefined order, both are stable mid-iteration if not mutated.
      Pre-sizing `new Dictionary(count)` (capacity hint, Microsoft Learn:
      "you can avoid several resizing operations") is a minor performance
      polish that preserves Dart's effective amortised behaviour. The
      transformation is pure (no side effects on `_transitions`); the
      returned `Automaton` is a fresh aggregate (Dart constructor takes the
      new map by reference — no defensive copy in source, preserved in C#).
  - construct_key: dart.collection.map_indexer_nullable_lookup
    source_form: >-
      DFAState? transition(DFAState from, TransitionLabel label) { return
      _transitions[(from, label)]; }
    target_decision: >-
      `public DFAState? Transition(DFAState from, TransitionLabel label)
      { return _transitions.TryGetValue((from, label), out var to) ? to :
      null; }`. Dart `Map<K,V>` indexer returns `V?` (nullable) on missing
      key — `_transitions[(from,label)]` in Dart is *typed* `DFAState?` due
      to the implicit null on miss. C# `Dictionary<K,V>` indexer THROWS
      `KeyNotFoundException` on miss (Microsoft Learn: "If the specified key
      is not found, a get operation throws a KeyNotFoundException") — a
      direct port of `_transitions[(from,label)]` would change behaviour.
      The faithful idiom is `TryGetValue` returning the value or `default`.
      The `?` on the C# return type preserves Dart's `DFAState?` signature.
    idiom_id: dart-map-nullable-indexer-to-csharp-trygetvalue
    research_finding_id: rf-csharp-dictionary-indexer-throws-vs-trygetvalue
    nuance: >-
      This is one of the most frequently-overlooked Dart→C# behavioural
      mismatches: Dart `Map[k]` returns null on miss; C# `Dictionary[k]`
      throws. The conversion MUST use `TryGetValue` (or `GetValueOrDefault`
      for value types) and propagate nullability through the return type.
      For reference-typed value (`DFAState`), `default` is `null`, and the
      nullable reference type `DFAState?` on the return signature
      communicates the absence to the C# nullability flow analysis. A naive
      `_transitions[(from,label)]` port would compile but throw at runtime
      on the first lookup of a missing transition — exactly the
      well-known nuance to never gloss.
  - construct_key: dart.collection.unmodifiable_view_getter_returning_internal_map
    source_form: >-
      Map<(DFAState, TransitionLabel), DFAState> get transitions => _transitions;
    target_decision: >-
      `public IReadOnlyDictionary<(DFAState, TransitionLabel), DFAState>
      Transitions => _transitions;`. The Dart getter returns the *same*
      `Map` reference (Dart `Map` is a mutable interface, no built-in
      view-wrapper at this call site), so callers could in principle mutate
      it (the codebase comments document it as for-iteration-only by the
      coverage checker). Preserve intent — not exact wire — by narrowing the
      C# return type to `IReadOnlyDictionary<,>`, which `Dictionary<,>`
      directly implements (Microsoft Learn: "implements ...
      IReadOnlyDictionary<TKey,TValue>"). The underlying storage remains
      mutable for internal use; external callers see a read-only view via
      the interface. This is a *deliberate, documented* tightening: the
      Dart source comment "for iteration by coverage checker" makes the
      read-only intent explicit, so encoding it in the C# type is faithful
      to *intent*, not a behavioural change observable through correct
      callers.
    idiom_id: dart-mutable-collection-getter-readonly-intent-to-csharp-ireadonly
    research_finding_id: rf-csharp-ireadonlydictionary-narrowed-public-view
    nuance: >-
      Dart has no nominal read-only dictionary interface in core, so
      mutable-by-default is the only option; the codebase uses comment-
      enforced read-only conventions. C# DOES have nominal
      `IReadOnlyDictionary<TKey,TValue>` and `Dictionary<,>` implements it
      directly, so we can promote a conventional invariant to a type-level
      one without changing storage. Reference-vs-value: the returned
      reference IS the internal dictionary (no defensive copy), matching
      Dart aliasing. A caller could still cast back to `Dictionary<,>` —
      the C# guarantee is type-level, not capability-level, exactly like
      Dart's comment-level guarantee.
  - construct_key: dart.collection.immutable_set_field_with_const_default
    source_form: >-
      final Set<String> acceptedPrimitives; Automaton(this.startState,
      this._transitions, {this.acceptedPrimitives = const {}});  (and used as
      acceptedPrimitives.contains('Integer'), acceptedPrimitives.isNotEmpty)
    target_decision: >-
      Field: `private readonly IReadOnlySet<string> _acceptedPrimitives;`
      (exposed as `public IReadOnlySet<string> AcceptedPrimitives =>
      _acceptedPrimitives;`). Constructor: optional parameter
      `IReadOnlySet<string>? acceptedPrimitives = null` with body
      `_acceptedPrimitives = acceptedPrimitives ?? FrozenSet<string>.Empty;`
      — replacing Dart's `const {}` default. The `FrozenSet<string>.Empty`
      static is the documented immutable empty set (rf-dotnet-frozenset-
      immutable-readheavy from prelude.dart, reused/cached). MUST NOT use a
      shared mutable static `HashSet<string>` as default (would alias mutable
      state across automata). Element comparer: `StringComparer.Ordinal`
      when the set is built (typically via `acceptedPrimitives.ToFrozenSet
      (StringComparer.Ordinal)` upstream) — strings here are exact tokens
      `"Integer"`, `"Real"`, `"Number"`, `"String"`, never user-input.
    idiom_id: dart-const-empty-set-default-to-csharp-frozenset-empty
    research_finding_id: rf-dart-const-set-to-csharp-frozenset-ordinal
    nuance: >-
      Dart `const {}` as a parameter default is a compile-time canonicalised
      *immutable* empty set; C# has no `const` collection — the faithful
      equivalent is `FrozenSet<string>.Empty` (or
      `ImmutableHashSet<string>.Empty`). Two load-bearing nuances: (1) the
      default MUST be immutable to preserve Dart's "shared-but-cannot-
      mutate" semantics — using `new HashSet<string>()` would lose this; (2)
      `StringComparer.Ordinal` is mandated (same rationale as type_ast.dart
      builtins set) because the set is keyed by exact reserved tokens.
  - construct_key: dart.collection.factory_seeded_mutable_string_dictionary
    source_form: >-
      class ProgramDFA { final Map<String, DFAState> states; final Map<String,
      Automaton> automata; ProgramDFA(this.states, this.automata); DFAState
      getState(String name) { final state = states[name]; if (state == null)
      throw StateError('State not found: $name'); return state; } }  +
      buildProgramDFA which mutates `states`/`automata` in place
    target_decision: >-
      `public sealed class ProgramDFA { private readonly
      Dictionary<string, DFAState> _states; private readonly
      Dictionary<string, Automaton> _automata; public ProgramDFA(
      Dictionary<string, DFAState> states, Dictionary<string, Automaton>
      automata) { _states = states; _automata = automata; } public DFAState
      GetState(string name) { if (!_states.TryGetValue(name, out var s))
      throw new InvalidOperationException($"State not found: {name}");
      return s; } public Automaton GetAutomaton(string typeName) { ... } }`.
      Dictionary instances are aliased from the caller (same as Dart — no
      defensive copy). String comparer: the caller's `buildProgramDFA` MUST
      construct both dictionaries with `StringComparer.Ordinal` (e.g.
      `new Dictionary<string, DFAState>(StringComparer.Ordinal)`) because
      keys are exact state-name tokens (e.g. `"Integer?"`, `"_FINAL_"`)
      and case-sensitivity is load-bearing. `StateError` (Dart) → C#
      `InvalidOperationException` is the canonical mapping (Microsoft Learn
      StateError analog).
    idiom_id: dart-string-keyed-map-to-csharp-ordinal-dictionary
    research_finding_id: rf-csharp-dictionary-stringcomparer-ordinal-discipline
    nuance: >-
      Dart `Map<String, T>` uses ordinal string equality by default
      (`Object.hashCode`/`==` on String). C# `Dictionary<string, T>` uses
      the DEFAULT comparer which is `EqualityComparer<string>.Default` —
      this is `StringComparer.Ordinal` for `string` per
      EqualityComparer<string>.Default behaviour, BUT the codebase
      convention from prelude.dart / type_ast.dart is to PASS
      `StringComparer.Ordinal` EXPLICITLY to make the contract reviewable
      (defence-in-depth against future refactors that swap the type). The
      lookup mistake nuance (indexer throws) applies here too: `_states[
      name]` is replaced with `TryGetValue` plus an explicit throw with a
      descriptive `InvalidOperationException` message preserving the Dart
      `$name` interpolation.
  - construct_key: dart.error_subclass.named_typed_error
    source_form: >-
      class UnknownTypeError extends Error { final String typeName;
      UnknownTypeError(this.typeName); @override String toString() =>
      'UnknownTypeError: $typeName'; }
    target_decision: >-
      Emit `public sealed class UnknownTypeException : Exception { public
      string TypeName { get; } public UnknownTypeException(string typeName)
      : base($"UnknownTypeError: {typeName}") { TypeName = typeName; } }`.
      Dart's `Error` (a *programmer-error* base, not catchable-by-design
      semantically — though catchable mechanically) is normally NOT mapped
      to a C# `System.Exception` subclass because Dart distinguishes Error
      (programmer mistake) from Exception (recoverable). HOWEVER this
      project's use of `UnknownTypeError` is as a *recoverable signal* to
      the caller (it carries the missing type name as data, indicating a
      lookup miss). The conversion documents this and maps to `Exception`
      (NOT `SystemException` — that's reserved for the BCL). The Dart
      override `toString()` message is preserved verbatim by passing the
      formatted string as the inner `Exception.Message` via the base
      constructor — so a C# `ex.Message` returns "UnknownTypeError:
      <name>" exactly, and `ex.ToString()` adds the type name + stack
      trace (standard C# behaviour, additive — message bytes preserved).
      The class name keeps the `Exception` suffix (C# BCL convention,
      Microsoft Learn Exception design: "Use the Exception suffix for new
      exception class names").
    idiom_id: dart-error-class-recoverable-signal-to-csharp-exception
    research_finding_id: rf-dart-error-vs-exception-to-csharp-exception
    nuance: >-
      Dart distinguishes `Error` (programmer/logic error, not normally
      caught) from `Exception` (recoverable runtime condition); C# folds
      both into the single `System.Exception` hierarchy (Microsoft Learn:
      "Exceptions are used to indicate that an error has occurred"). The
      decision is per-call-site: if the Dart code catches and acts on the
      error (recoverable), C# `Exception`; if it propagates as a fatal
      programmer mistake, C# could use `InvalidOperationException` or
      `Debug.Assert`. Here the error carries data (`typeName`) consumed by
      callers — clearly recoverable signal — so a dedicated `Exception`
      subclass is the correct mapping. The naming convention shift
      (`...Error` → `...Exception`) is mandated by Microsoft Learn exception
      design guidance.
  - construct_key: dart.toplevel.driver_function_with_imperative_phase_loops
    source_form: >-
      ProgramDFA buildProgramDFA(TypeEnvironment env) { final states = <String,
      DFAState>{}; final automata = <String, Automaton>{}; // create system
      states; for (...) { ... } // create defined-type states; for (...) { ... }
      // build defined-type automata; for (...) { ... } // build procedure
      states/automata; return ProgramDFA(states, automata); }
    target_decision: >-
      Convert to a `public static ProgramDFA Build(TypeEnvironment env)` on
      a `static class ProgramDfaBuilder` (or as a static factory `public
      static ProgramDFA Build(TypeEnvironment env)` directly on
      `ProgramDFA`). Body preserves the four-phase imperative structure
      verbatim: (1) system states + system automata (procedural literal
      population); (2) declared-type *states* for every `env.Types` entry
      (creates both T and T? states); (3) declared-type *automata* —
      MUST be a SEPARATE second pass over `env.Types` because automaton
      construction references states for other types (forward references);
      (4) procedure states + automata. The two-pass ordering over the same
      collection is LOAD-BEARING (commented in the Dart source: "Create
      states for ALL defined types FIRST"). Dictionaries are seeded with
      `StringComparer.Ordinal`. `env.Types` and `env.Procedures` are
      iterated by `foreach (var (typeName, typeDef) in env.Types)` (C#
      KeyValuePair deconstruction).
    idiom_id: dart-toplevel-driver-fn-to-csharp-static-builder-method
    research_finding_id: rf-csharp-static-class-no-toplevel-members
    nuance: >-
      Dart top-level function → C# static method (cached from prelude.dart;
      `csharp-static-class-no-toplevel-members`). The substantive nuance
      here is preservation of the two-pass discipline: declared-type
      automaton construction reads `states[targetName]` for forward-
      referenced types, so all states must be created BEFORE any automaton
      transition is wired. A naive single-pass refactor would compile but
      throw `UnknownTypeError` on forward references (e.g. mutually
      recursive type definitions). The spec records the pass discipline
      explicitly so the codegen stage does not "optimise" it away. Iteration
      order of `env.Types` is not load-bearing for correctness (each
      iteration is independent within a phase) but must be deterministic
      for reproducible DFA output; Dart `Map` preserves insertion order, C#
      `Dictionary` also preserves insertion order since .NET Core 3.0 (this
      is documented behaviour but not contractually guaranteed — recorded
      here as a minor invariant the codegen stage should pin via the
      collection-type choice).
  - construct_key: dart.helper.private_top_level_constructor_functions
    source_form: >-
      Automaton _finalAutomaton(DFAState state) => Automaton(state, {});
      Automaton _primitiveTypeAutomaton(DFAState state, DFAState finalState)
      => Automaton(state, {}); Automaton _buildTypeAutomaton(TypeDef typeDef,
      Map<String, DFAState> states, {required bool isDual}) { ... } void
      _addTypeTransitions(DFAState fromState, TypeExpr alt, Mode contextMode,
      Map<String, DFAState> states, Map<(DFAState, TransitionLabel), DFAState>
      transitions, bool isDual) { ... } DFAState _resolveTypeExpr(TypeExpr
      typeExpr, Map<String, DFAState> states, bool isDual) { ... } Mode _modeOf
      (TypeExpr typeExpr, Mode contextMode) { ... } Automaton
      _buildProcedureAutomaton(ProcDecl procDecl, Map<String, DFAState> states)
      { ... } String _getFullTypeName(TypeExpr typeExpr) { ... }
    target_decision: >-
      Move all seven private top-level helpers to `private static` methods
      on the same `static class ProgramDfaBuilder` (or as `internal` static
      methods if a sibling builder needs them — internal preferred for
      testability without expanding the public surface). The Dart `_` prefix
      (library-private) maps to C# `private` or `internal` (Microsoft Learn:
      C# has no library-level privacy; assembly-level `internal` is the
      closest analog). Signatures preserve required positional + named
      parameters: `_buildTypeAutomaton(typeDef, states, isDual: false)` →
      `BuildTypeAutomaton(TypeDef typeDef, Dictionary<string, DFAState>
      states, bool isDual)` — named-arg semantics are preserved at call
      sites via C# named arguments `BuildTypeAutomaton(td, states, isDual:
      false)`. The `required` Dart named parameter forces caller to supply
      `isDual`; in C# this is enforced by making the parameter
      non-optional (no default) at the *required* position. The two trivial
      helpers `_finalAutomaton` and `_primitiveTypeAutomaton` collapse to
      expression-bodied static methods.
    idiom_id: dart-private-toplevel-helper-to-csharp-private-static-method
    research_finding_id: rf-csharp-private-vs-internal-library-helpers
    nuance: >-
      Dart privacy is library-scoped (`_` prefix); C# privacy is
      type-scoped (`private`) or assembly-scoped (`internal`). The choice
      is per-helper based on whether the codegen testing strategy needs
      to invoke the helper directly: `internal` exposes to in-assembly
      test code via `[InternalsVisibleTo]`; `private` is strictly type-
      local. The codebase convention from prior specs (prelude.dart's
      static-class container) suggests `private static` for pure
      computation helpers when they're only used within the host class.
      Required Dart named-arg `isDual` is type-system-enforced; C# named
      args are syntactic and the *requiredness* comes from not supplying
      a default — equivalent in effect.
  - construct_key: dart.controlflow.if_else_type_pattern_chain_on_sealed_sum
    source_form: >-
      if (alt is ConstantAlt) { ... } else if (alt is ListNilAlt) { ... } else
      if (alt is ListConsAlt) { final headMode = ...; ... } else if (alt is
      StructAlt) { for (var i = 0; i < alt.args.length; i++) { ... } } else
      if (alt is DiffListAlt) { ... }  (in _addTypeTransitions and
      _resolveTypeExpr handles PrimitiveModeAlt + TypeRef)
    target_decision: >-
      Convert to a C# *type-pattern* `switch` statement (NOT chained
      `if`/`else if`) over the closed sum type defined in type_ast.dart:
      `switch (alt) { case ConstantAlt c: ... break; case ListNilAlt: ...
      break; case ListConsAlt l: ... break; case StructAlt s: for (int i=0;
      i < s.Args.Count; i++) { ... } break; case DiffListAlt d: ... break;
      default: /* PrimitiveModeAlt handled in ResolveTypeExpr; explicit
      pass-through */ break; }`. Each arm uses declaration-pattern (`case
      ConstantAlt c:`) to fuse test + cast (same idiom as type_ast.dart's
      rf-dart-extension-is-as-to-csharp-type-pattern-switch). The
      `PrimitiveModeAlt` "leaf-not-constructor" comment in source is
      preserved as an explicit `default` arm (with the comment) — DO NOT
      collapse to an implicit fallthrough that hides intent. For
      `_resolveTypeExpr`, the final `throw StateError('Cannot resolve type
      expression: $typeExpr')` becomes an explicit `_ =>` switch-expression
      arm or `default: throw new InvalidOperationException(...)`.
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Reuses the cached type_ast.dart finding directly: each Dart `if (x is
      T) { (x as T).m }` cluster fuses to a C# declaration pattern
      `case T t: ... t.M`. The non-trivial nuance specific to this file is
      that the SUM IS CLOSED across `type_ast.dart` (six leaves of
      `TypeExpr`), but one leaf (`PrimitiveModeAlt`) is *intentionally
      omitted* from `_addTypeTransitions` (handled in `_resolveTypeExpr`) —
      the C# switch MUST preserve this intentional omission via an
      explicit no-op `default:` arm with the source comment, not by
      throwing. Conversely, `_resolveTypeExpr` handles only
      `PrimitiveModeAlt` + `TypeRef` and throws `StateError` on anything
      else — the C# version throws `InvalidOperationException` on the
      default arm (`StateError` → `InvalidOperationException`, same idiom
      as `UnknownTypeError` mapping above for the recoverable-vs-
      programmer-error distinction, but here it's a true programmer error
      → `InvalidOperationException`).
  - construct_key: dart.controlflow.xor_via_boolean_inequality
    source_form: >-
      final baseIsComplement = typeExpr.isInput; final finalIsComplement =
      baseIsComplement != isDual; // XOR
    target_decision: >-
      `bool baseIsComplement = typeExpr.IsInput; bool finalIsComplement =
      baseIsComplement != isDual;` — verbatim. C# `!=` on `bool` operates
      identically to Dart `!=` on `bool` (boolean inequality == XOR), and
      the source comment `// XOR` is preserved as `// XOR`. Idiom is
      mechanical; recorded so the codegen stage does not "modernise" to
      `bool finalIsComplement = baseIsComplement ^ isDual;` (which is
      legal C# but changes the read-and-review surface — the source
      explicitly uses `!=` with a comment, so the spec preserves that
      choice).
    idiom_id: dart-boolean-xor-via-inequality-to-csharp-verbatim
    research_finding_id: rf-csharp-boolean-equality-operators-trivial
    nuance: >-
      Both languages define `!=` on `bool` as XOR (Microsoft Learn:
      `bool` "supports the comparison ... (==) and inequality (!=)
      operators"); the conversion is mechanical. The non-trivial *choice*
      is whether to preserve `!=` or rewrite to `^` — the spec mandates
      preserving the source-form because (a) the comment names the
      semantics, (b) `^` on bool is less idiomatic in C# code review,
      and (c) faithful conversion is the project bar.
escalations: []
conversion_units:
  - "sealed class DFAState : IEquatable<DFAState> (BaseName, IsDual, IsFinal, IsProcedure auto-properties; ctor; Name getter; Dual property; eleven Is*-classifier expression-bodied properties; ToString override; Equals/GetHashCode hand-written on BaseName+IsDual only)"
  - "sealed class TransitionLabel : IEquatable<TransitionLabel> (Symbol, Arity, ArgIndex, Mode?; private ctor; static Functor/Constant factory methods; Dual property; ToString override; Equals/GetHashCode hand-written on all four fields)"
  - "sealed class Automaton (StartState, _transitions Dictionary, _acceptedPrimitives IReadOnlySet; ctor with optional acceptedPrimitives default FrozenSet.Empty; Transition method via TryGetValue; Transitions IReadOnlyDictionary property; Dual property building fresh Dictionary)"
  - "sealed class ProgramDFA (States/Automata Dictionary fields; ctor; GetState/GetAutomaton via TryGetValue with explicit throw)"
  - "sealed class UnknownTypeException : Exception (TypeName property; ctor sets base.Message to 'UnknownTypeError: {typeName}')"
  - "sealed class LeafTerm (immutable; private ctor with all fields; six static factory methods Writer/Reader/IntegerConstant/RealConstant/StringConstant/Constant)"
  - "sealed class LeafConsistencyResult (IsConsistent, Type?, Reason?; two static factory methods Consistent(state)/Inconsistent(reason))"
  - "static class ProgramDfaBuilder — public static Build(TypeEnvironment); private static FinalAutomaton/PrimitiveTypeAutomaton/BuildTypeAutomaton/AddTypeTransitions/ResolveTypeExpr/ModeOf/BuildProcedureAutomaton/GetFullTypeName"
  - "public static LeafConsistencyResult CheckLeafConsistency(LeafTerm leaf, DFAState state, ProgramDFA dfa) on a static class (likely ProgramDfaBuilder or a dedicated LeafConsistencyChecker)"
```

## Rationale & Research Provenance

This file defines four reference-typed value classes (`DFAState`,
`TransitionLabel`, `Automaton`, `ProgramDFA`), three immutable data carriers
(`UnknownTypeError`, `LeafTerm`, `LeafConsistencyResult`), a top-level driver
function (`buildProgramDFA`) with seven private helpers, and one top-level
leaf-consistency checker. The non-mechanical decisions all turn on Dart→C#
*semantics* — hand-written equality (partial vs full field set),
nullable-map-lookup mismatch (Dart returns null, C# throws), Dart-3-record
→ ValueTuple, factory constructors, immutable-collection defaults, error-vs-
exception, type-pattern dispatch over the closed AST sum from
`type_ast.dart` — each grounded below. Cached findings from
`prelude.dart`/`mode.dart`/`type_ast.dart` are reused (FR-024 — never re-
research a cached construct_key).

### rf-csharp-record-uses-all-members-equality

**Deep analysis.** `DFAState` overrides `==` and `hashCode` on `baseName +
isDual` ONLY — not on `isFinal` or `isProcedure`. This is deliberate (the
`dual` getter constructs a new state sharing the same `isFinal`/`isProcedure`,
and consumers treat "Stream" and "Stream produced via dual" as the same
identity for tuple-key lookups).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record`
— Microsoft Learn: synthesized record equality "uses the declared data
members" — i.e. ALL members declared in the positional parameter list or as
init-able properties. There is no per-member opt-out short of a custom
override. Verbatim query: "C# record equality declared data members all
fields". Therefore a positional `record DFAState(string BaseName, bool
IsDual, bool IsFinal, bool IsProcedure)` would silently produce equality
over four fields, not two — a behavioural change vs the Dart source.

**Conclusion.** Hand-written `IEquatable<DFAState>` `Equals`/`GetHashCode` on
the two-field subset; `class`, not `record`. Sealed (no subclasses exist in
the Dart source). `StringComparer.Ordinal` for `BaseName` comparison
(consistent with project-wide ordinal-string discipline from
`type_ast.dart`'s `rf-dart-const-set-to-csharp-frozenset-ordinal`).

### rf-csharp-property-vs-method-pure-getter

**Deep analysis.** `DFAState.dual`, `TransitionLabel.dual`,
`Automaton.dual`, and the eleven `Is*` getters on `DFAState` are all
zero-arg pure derivations (no observable side effect, allocate at most a
single new instance).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/using-properties`
— Microsoft Learn property guidance: "use a property when ... the member
represents a logical attribute and the get accessor has no observable side
effect." Verbatim query: "C# property vs method pure getter design
guidance". Authoritative conclusion: zero-arg pure derivations idiomatically
map to read-only C# properties.

**Conclusion.** `Dual` is a C# property on all three carrier types
(`DFAState`, `TransitionLabel`, `Automaton`); the eleven `Is*` classifiers on
`DFAState` are expression-bodied properties. Allocation behaviour (`Dual`
returns a new instance per call) is preserved — recorded as a load-bearing
nuance because reference-identity callers (`state.Dual == state.Dual` by
reference) would observe the difference, but value-equality callers (via the
overridden `Equals`) see the equality preserved.

### rf-csharp-string-equality-ordinal-by-default

**Deep analysis.** The `Is*` classifier cluster compares `baseName` (a Dart
`String`) to reserved literal tokens like `'_'`, `'Integer'`, `'_FINAL_'`.
Dart `String ==` is ordinal-by-design; the spec must confirm the same for C#.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types`
and the `System.String` reference — Microsoft Learn:
the `string` equality operator "compares the value of two strings using
ordinal (binary) comparison." Verbatim query: "C# string equality operator
ordinal comparison". Authoritative conclusion: `string ==` is ordinal,
matching Dart exactly.

**Conclusion.** The classifier cluster ports verbatim with no explicit
comparer needed (in contrast to `FrozenSet<string>` construction where the
comparer MUST be passed). The asymmetry is grounded: free `==` on `string`
is ordinal; collection comparers default to `EqualityComparer<string>
.Default` which is also ordinal, but the project convention from
`prelude.dart` is to pass `StringComparer.Ordinal` explicitly for
collections — preserved.

### rf-csharp-nullable-value-type-lifted-equality

**Deep analysis.** `TransitionLabel.mode` is `Mode?` (nullable enum), and
the equality override `other.mode == mode` relies on Dart `==` short-
circuiting correctly through null on both sides.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/nullable-value-types`
— Microsoft Learn: "Both equality and inequality operators compare two
operands of `Nullable<T>` ... If both operands are null, they are equal;
if exactly one operand is null, they are not equal; otherwise, they compare
as the underlying type." Verbatim query: "C# Nullable<T> lifted equality
operator". Authoritative conclusion: `Mode? == Mode?` in C# matches Dart
`Mode? == Mode?` exactly (lifted equality with null-aware semantics).

**Conclusion.** The `Equals(TransitionLabel?)` body uses `Mode == other.Mode`
directly (lifted to `Mode?`); `HashCode.Combine(Symbol, Arity, ArgIndex,
Mode)` handles the null-mode case deterministically. No special-casing
needed.

### rf-dart-csharp-null-aware-call-operator-identical

**Deep analysis.** `TransitionLabel.dual` uses `mode?.flip` — invoke `flip`
only if `mode != null`, else propagate null. The C# null-conditional
operator `?.` has identical shape and semantics.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/member-access-operators`
— Microsoft Learn: "Null-conditional operators apply a member access (?.)
or element access (?[]) operation to its operand only if that operand
evaluates to non-null; otherwise, it returns null." Cross-checked with
`https://dart.dev/language/operators` — Dart: "?. is like ., except that
the leftmost operand can be null." Verbatim queries: "C# null-conditional
operator ?. evaluate non-null"; "Dart null-aware operator ?. semantics".

**Conclusion.** `Mode?.Flip()` is the direct C# port — token-for-token,
semantically identical. The result type is `Mode?` (null propagated). This
is one of the small set of operators where Dart→C# is genuinely trivial;
recorded explicitly per US2-AS4 ("never gloss a well-known nuance").

### rf-csharp-interpolated-string-equivalent-to-dart-interpolation

**Deep analysis.** `TransitionLabel.toString()` and `DFAState.toString()`
use Dart string interpolation `'$x(...)'`. Dart and C# share equivalent
interpolated-string forms.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated`
— Microsoft Learn: "an interpolated string is a string literal that might
contain interpolation expressions. When an interpolated string is resolved
to a result string, items with interpolation expressions are replaced by
the string representations of the expression results." Verbatim query:
"C# interpolated string `$\"...\"` syntax expressions in braces".

**Conclusion.** `'$symbol($arity,$argIndex)$modeStr'` maps directly to
`$"{Symbol}({Arity},{ArgIndex}){modeStr}"`. The Unicode arrows `↑`/`↓`
require UTF-8 file encoding (BOM-or-explicit); recorded as an encoding
constraint the codegen stage must honour.

### rf-dart3-record-to-csharp-valuetuple

**Deep analysis.** The dictionary key `(DFAState, TransitionLabel)` is a
Dart 3 *record type* used as a `Map` key. C# ValueTuple is the documented
direct counterpart.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/value-tuples`
— Microsoft Learn: tuple types "provide concise syntax to group multiple
data elements in a lightweight data structure" and "tuple equality ... is
defined as structural equality on each element using each element's
`==` operator." Verbatim query: "C# value tuple equality structural
element-wise dictionary key". Cross-checked with
`https://dart.dev/language/records` — Dart: "Records are an anonymous,
immutable, aggregate type ... two records are equal if they have the same
shape (set of fields), and their corresponding fields have the same
values." Authoritative conclusion: Dart 3 records and C# ValueTuple share
structural equality semantics.

**Conclusion.** `Map<(DFAState, TransitionLabel), DFAState>` → C#
`Dictionary<(DFAState From, TransitionLabel Label), DFAState>` (with
optional element names for readability). Element equality consults each
component's `Equals`, which routes to the hand-written `DFAState.Equals`
(BaseName+IsDual) and `TransitionLabel.Equals` (all four fields) — the
intended composite key semantics.

### rf-csharp-dictionary-foreach-iteration-keyvaluepair

**Deep analysis.** `Automaton.dual` iterates `_transitions.entries` to
build a new transformed dictionary. Dart `Map.entries` yields `MapEntry<K,
V>`; C# `Dictionary<K,V>` iterates `KeyValuePair<K,V>`.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2`
— Microsoft Learn Dictionary remarks: "The foreach statement returns an
object of the type of the elements in the collection. Since each element
of the Dictionary<TKey,TValue> is a key-value pair, the element type is
not the type of the key or the type of the value. Instead, the element
type is `KeyValuePair<TKey,TValue>`." Verbatim query: "C# Dictionary
foreach KeyValuePair iteration". Authoritative conclusion: iteration is
`foreach (var kvp in dict)` yielding `KeyValuePair<TKey,TValue>` with
`Key`/`Value` properties.

**Conclusion.** The `Automaton.dual` body is a `foreach` over the
dictionary, deconstructing each `KeyValuePair` (or using `entry.Key`/
`entry.Value`); each iteration upserts into a fresh dictionary. Capacity
pre-sizing (`new Dictionary(_transitions.Count)`) is a documented
performance polish (Microsoft Learn: pre-sizing "avoid[s] several
resizing operations").

### rf-csharp-dictionary-indexer-throws-vs-trygetvalue

**Deep analysis.** `Automaton.transition` does `_transitions[(from,
label)]` and returns `DFAState?` — relying on Dart `Map`'s missing-key-
returns-null semantics. C# `Dictionary` throws `KeyNotFoundException` on
missing key.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.item`
— Microsoft Learn `Dictionary<TKey,TValue>.this[TKey]` Property /
Exceptions: "KeyNotFoundException — The property is retrieved and `key`
does not exist in the collection." And
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.trygetvalue`
— `TryGetValue` "Returns: `true` if the `Dictionary<TKey,TValue>`
contains an element with the specified key; otherwise, `false`."
Verbatim query: "C# Dictionary indexer throws KeyNotFoundException
TryGetValue null safe lookup". Cross-checked with
`https://api.dart.dev/stable/dart-core/Map/operator_get.html` — Dart
Map `operator []`: "Returns null if the key is not in the map."

**Conclusion.** This is the single highest-impact behavioural mismatch
in the file. Convert `_transitions[(from, label)]` to
`_transitions.TryGetValue((from, label), out var s) ? s : null` and the
return signature stays `DFAState?`. The same idiom applies to
`ProgramDFA.GetState`/`GetAutomaton` — they DO want the throw (matching
the explicit Dart `if (state == null) throw StateError(...)`), so they
keep `TryGetValue` + explicit `throw new InvalidOperationException(...)`,
preserving the error message text via interpolation.

### rf-csharp-ireadonlydictionary-narrowed-public-view

**Deep analysis.** `Automaton.transitions` getter returns the internal
mutable `Map`. Dart has no nominal read-only interface; the codebase uses
comments for the read-only contract. C# has `IReadOnlyDictionary<K,V>`,
which `Dictionary<K,V>` implements directly.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2`
— Microsoft Learn: `Dictionary<TKey,TValue>` "Implements ...
IReadOnlyDictionary<TKey,TValue>". Authoritative conclusion: a read-only
narrowing at the property type is free (no allocation, no wrapping —
the same instance is exposed through a narrower interface). Verbatim
query: "C# Dictionary IReadOnlyDictionary public view narrowing".

**Conclusion.** `Transitions` becomes `public
IReadOnlyDictionary<(DFAState, TransitionLabel), DFAState> Transitions
=> _transitions;`. Storage stays `Dictionary<,>` for internal use. The
read-only intent encoded in Dart's source comment becomes type-level in
C# — a faithful tightening, observable only to (incorrect) callers that
were mutating the dictionary through the getter.

### rf-csharp-dictionary-stringcomparer-ordinal-discipline

**Deep analysis.** `ProgramDFA.states`/`automata` and the local
`states`/`automata` in `buildProgramDFA` are `Map<String, ...>`. Dart
keys via ordinal `String ==`. The project convention from `prelude.dart`
and `type_ast.dart` is to construct C# `Dictionary<string, ...>` with
`StringComparer.Ordinal` explicitly.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.stringcomparer.ordinal`
— Microsoft Learn `StringComparer.Ordinal`: "Gets a StringComparer
object that performs a case-sensitive ordinal string comparison."
Verbatim query: "C# StringComparer Ordinal case-sensitive default
Dictionary key comparer". Authoritative conclusion: passing
`StringComparer.Ordinal` to a `Dictionary<string,T>` constructor
guarantees ordinal keying regardless of future default-comparer changes.

**Conclusion.** Construct both top-level dictionaries with
`new Dictionary<string, T>(StringComparer.Ordinal)`. State-name keys
contain reserved tokens (`_FINAL_`, `Integer?`) where case-sensitivity
is load-bearing — recorded so the codegen stage does not "modernise" to
`StringComparer.InvariantCulture` or similar.

### rf-dart-error-vs-exception-to-csharp-exception

**Deep analysis.** `UnknownTypeError extends Error` in Dart distinguishes
*programmer error* from `Exception` (recoverable). The Dart docs caution
not to catch `Error` — but this code carries the missing-type-name as
data, indicating a recoverable signal that callers may handle (e.g.
report a missing import to a user). C# has only `System.Exception`.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions`
— Microsoft Learn exception design guidelines: "Use the predefined .NET
exception types only when they apply to the situation. Throw these
exceptions when they apply ... Add information to exceptions only when
it is required by the program logic." And
`https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/exception-throwing`
— "DO use the most appropriate Framework exception type" and the
`...Exception` naming suffix convention. Cross-checked with
`https://api.dart.dev/stable/dart-core/Error-class.html` — Dart: "An
`Error` object represents a program failure that the programmer should
have avoided ... Should not be caught." Verbatim queries: "C# custom
exception class design Exception suffix"; "Dart Error vs Exception
catchable recoverable".

**Conclusion.** Map `UnknownTypeError extends Error` to
`UnknownTypeException : System.Exception` (rename per BCL convention,
preserve `TypeName` data field, base-constructor preserves the
"UnknownTypeError: <name>" message verbatim via `ex.Message`). The
class name change is non-optional under Microsoft Learn design
guidance. Internal `StateError` throws in helpers → C#
`InvalidOperationException` (no data carrier needed, true programmer
error).

### rf-dart-extension-is-as-to-csharp-type-pattern-switch (cached from type_ast.dart)

**Cached use.** The `if (alt is T) { (alt as T).x }` cluster in
`_addTypeTransitions` and `_resolveTypeExpr` reuses the exact idiom from
`type_ast.dart` (closed AST sum, exhaustive type-pattern switch with a
throwing or explicit-no-op default). No new research — cited as
authoritative cache (FR-024). The file-specific nuance recorded is the
intentional asymmetry: `_addTypeTransitions` deliberately omits
`PrimitiveModeAlt` (handled in `_resolveTypeExpr`); the C# switch
preserves this with an explicit no-op `default` plus the source comment.

### rf-dart-factory-ctor-const-default-to-csharp-static-factory (cached from type_ast.dart)

**Cached use.** `TransitionLabel.functor` / `TransitionLabel.constant`
and the `LeafTerm.writer`/`reader`/`integerConstant`/etc. factory
constructors reuse the `factory ctor → static factory method` idiom from
`type_ast.dart` (TypeEnvironment.empty). No new research. File-specific
nuance: `LeafTerm` has SIX factory ctors over a single private
generative ctor — all become `public static LeafTerm Writer(...)` /
`Reader(...)` / `IntegerConstant(...)` etc. on the same class, each
expression-bodied calling `new LeafTerm(...)`.

### rf-dart-const-set-to-csharp-frozenset-ordinal (cached from type_ast.dart)

**Cached use.** `Automaton.acceptedPrimitives` default `const {}` reuses
the `const set → FrozenSet` idiom (immutable default for an immutable
read-only-membership set keyed by string with ordinal comparer).
`FrozenSet<string>.Empty` is the documented immutable empty.

### rf-csharp-static-class-no-toplevel-members (cached from prelude.dart)

**Cached use.** `buildProgramDFA` and the seven private helpers (`_finalAutomaton`,
`_primitiveTypeAutomaton`, `_buildTypeAutomaton`, `_addTypeTransitions`,
`_resolveTypeExpr`, `_modeOf`, `_buildProcedureAutomaton`,
`_getFullTypeName`) and `checkLeafConsistency` all use the
toplevel-fn-to-static-method idiom from `prelude.dart`. Host class
`ProgramDfaBuilder` (or grouped with `ProgramDFA` as static factory) per
codebase convention. No new research.

### rf-csharp-boolean-equality-operators-trivial / rf-csharp-private-vs-internal-library-helpers

**Trivial nuances** recorded for completeness:
- `bool !=` (XOR) is identical in both languages (Microsoft Learn `bool`
  reference); preserve source-form `!=` with the `// XOR` comment intact.
- Dart `_`-prefix library-private → C# `private`/`internal`. `private`
  preferred for in-type helpers; `internal` only if test code needs
  direct invocation via `[InternalsVisibleTo]`. Cached behaviour, no new
  research (covered in standard C# accessibility documentation —
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/access-modifiers`).

### LeafTerm / LeafConsistencyResult (data carriers)

Both classes are immutable data carriers with private generative ctors and
static factory methods:

- `LeafTerm` — eight fields (name, isVariable, isReader, mode, value,
  isInteger, isReal, isString), six factory methods (writer, reader,
  integerConstant, realConstant, stringConstant, constant). Maps to a
  `sealed class LeafTerm` with read-only auto-properties and six `public
  static LeafTerm <Name>(...)` factory methods. Equality is NOT
  overridden in Dart, so the C# class keeps reference identity (NO
  `IEquatable`, NO `record`) — matching Dart exactly. Nullable fields
  (`name`, `mode`, `value`) map to C# nullable reference / nullable
  value types (`string? Name`, `Mode? Mode`, `object? Value`).
- `LeafConsistencyResult` — three fields (isConsistent, type?, reason?),
  two factory methods (consistent, inconsistent). Maps identically: sealed
  class, factory methods, nullable fields. Recorded under the same
  cached idiom `dart-factory-ctor-const-default-to-csharp-static-factory`.

### checkLeafConsistency (large multi-branch checker)

The `checkLeafConsistency` function is a large dispatch over leaf-vs-state
combinations. Conversion is mechanical given the idioms above:
- The early `if (leaf.isVariable) { ... }` branch maps to a top-level
  `if` (no type-pattern needed — it's a boolean check on a property).
- The cascading `if (state.isXType) { ... }` chain converts to a
  type-pattern-free `if/else if` ladder over the boolean classifier
  properties on `DFAState` (these are already properties; using a `switch
  expression` over an enum would require introducing an enum, not in
  source — preserve the source structure).
- `LeafConsistencyResult.consistent(state)` /
  `LeafConsistencyResult.inconsistent(reason)` are static factory
  invocations preserved verbatim under the cached factory idiom.
- The trailing `automaton.transition(state, constLabel)` invocation
  consumes the nullable-lookup mapping (`TryGetValue`-based
  `Transition`) above.

No new research is needed for this function beyond the already-cited
findings; the file-specific nuance is preserving the *order* of the
checks (variable-first, then anonymous-final fast-path, then primitive
types, then wildcards, then user-defined types) because the order
encodes the precedence of leaf-vs-state dispatch.
