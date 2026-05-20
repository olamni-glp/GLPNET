# Conversion Spec — lib/analysis/type_checker/moded_term.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/moded_term.dart
source_sha256: e1f9f5809ff29101ca4c63e08173c7db6d02257c350d132b6e55e90c4f790fe2
target_code_unit: lib/analysis/type_checker/moded_term.cs
constructs:
  - construct_key: dart.abstract_base_class.closed_sum_with_visitor_dispatch
    source_form: >-
      abstract class ModedTerm { Mode get mode; T accept<T>(ModedTermVisitor<T>
      visitor); }  with three concrete subclasses ModedCompound, ModedConstant,
      ModedVariable, each overriding accept to dispatch back to the visitor's
      matching visitX method.
    target_decision: >-
      Emit `public abstract class ModedTerm` declaring `public abstract Mode
      Mode { get; }` (read-only abstract auto-property) and `public abstract T
      Accept<T>(IModedTermVisitor<T> visitor)`. The three concrete node types
      become `public sealed class ModedCompound : ModedTerm`, `... ModedConstant
      : ModedTerm`, `... ModedVariable : ModedTerm` — each overriding `Mode`
      and `Accept<T>(...)` to call `visitor.VisitCompound(this)` /
      `VisitConstant(this)` / `VisitVariable(this)` respectively. `ModedTerm`
      itself is NOT marked `sealed` (Microsoft Learn: "It's an error to use the
      abstract modifier with a sealed class"); closure is expressed by sealing
      the three leaves and by exhaustive type-pattern switching at consumer
      sites (`_allPathsValidIO`, `_extractPaths`, `_symbolOf`) with a throwing
      discard arm to preserve Dart's closed-set totality, which C# does not
      compile-time-verify for a non-language-sealed base.
    idiom_id: null
    research_finding_id: rf-dart-abstract-ast-base-to-csharp-abstract-sealed-leaves
    nuance: >-
      Reference-vs-value: ModedTerm nodes are reference types in both languages
      (allocated on the heap, shared sub-tree aliasing preserved — `dual()` and
      `_extractPaths` rely on it); they map to C# `class`, never `struct` /
      `record struct` / `record`. Reusing the cached rf finding from the
      `type_ast.dart` spec (FR-024 cache): same Microsoft Learn citation on the
      `abstract` + `sealed` clash decisively rules out a `sealed abstract`
      base, so closure migrates to the leaves plus throwing default arms in
      consumers. The Dart visitor pattern (instance `accept` dispatching to
      `visitor.visitX`) is preserved verbatim as the primary dispatch idiom
      because three independent visitors (`_IsConsumedVisitor`,
      `_IsProducedVisitor`, `_DualVisitor`) already use it; consumer-side
      type-pattern `switch` is added only at the few sites that currently use
      `is`/`as` (`_allPathsValidIO`, `paths`, `_extractPaths`, `_symbolOf`).
  - construct_key: dart.visitor_pattern.generic_double_dispatch_abstract_interface
    source_form: >-
      abstract class ModedTermVisitor<T> { T visitCompound(ModedCompound term);
      T visitConstant(ModedConstant term); T visitVariable(ModedVariable term);
      }  with concrete uses class _IsConsumedVisitor implements
      ModedTermVisitor<bool> { ... }, class _DualVisitor implements
      ModedTermVisitor<ModedTerm> { ... }
    target_decision: >-
      Emit as a C# generic interface `public interface IModedTermVisitor<out
      T> { T VisitCompound(ModedCompound term); T VisitConstant(ModedConstant
      term); T VisitVariable(ModedVariable term); }` (NOT an `abstract class`).
      Dart `abstract class … with no fields` used purely as a structural
      contract corresponds to a C# interface; C# `abstract class` is reserved
      for cases that *need* shared state / virtual default implementations,
      which this contract does not. The covariant `out T` annotation is
      permissible because `T` only appears in return position (Microsoft Learn:
      "Use the `out` keyword to declare a generic type parameter covariant").
      The Dart `implements` clauses map to `: IModedTermVisitor<bool>` /
      `: IModedTermVisitor<ModedTerm>` implementations on the visitor classes.
      The `I`-prefix is the documented .NET interface-naming convention.
    idiom_id: null
    research_finding_id: rf-dart-abstract-class-pure-contract-to-csharp-interface
    nuance: >-
      Semantics shift made explicit: Dart `abstract class` may be used either
      as a base class (carrying state/behaviour) or as a pure structural
      contract (no fields, all members abstract); the *former* maps to C#
      `abstract class`, the *latter* to C# `interface`. `ModedTermVisitor<T>`
      is purely a contract (three abstract methods, no fields, no body), so it
      maps to an `interface`. Reference-vs-value is irrelevant for the visitor
      itself (always a reference type in both). Covariance: `out T` is added
      because `T` is return-only — this is a strict generalisation that does
      not affect the existing Dart call sites (which use exact-`T` visitors)
      and unlocks safe upcasts (`IModedTermVisitor<ModedTerm>` ↑ as
      `IModedTermVisitor<object>`). No nullability concern: every method
      returns `T` non-null.
  - construct_key: dart.private_class.leading_underscore_visitor_impl
    source_form: >-
      class _IsConsumedVisitor implements ModedTermVisitor<bool> { ... }  (and
      _IsProducedVisitor, _DualVisitor — leading underscore means
      library-private in Dart)
    target_decision: >-
      Emit as `file`-scoped classes (`file sealed class IsConsumedVisitor :
      IModedTermVisitor<bool>`) in C# 11+, OR as `internal sealed class`
      visitors when the codegen targets a pre-C#-11 framework / when the file
      is part of a larger compilation unit. The Dart leading-underscore
      convention denotes **library-private** scope (Dart's only access
      modifier); the precise C# equivalent is `file` (single-file scope) which
      most exactly matches "private to this Dart library file". `internal` is
      a wider relaxation (assembly-private) and is the documented fallback
      when `file` is unavailable. The visitors are stateless (no fields) so
      they are also marked `sealed` to enable devirtualisation; one shared
      static instance per visitor (`private static readonly … Instance = new
      ();`) is permissible since they hold no state — preserved as a codegen
      optimisation, not a semantic shift.
    idiom_id: null
    research_finding_id: rf-dart-library-private-underscore-to-csharp-file-or-internal
    nuance: >-
      Dart has exactly one access modifier (library-private via leading
      `_`); C# has five (`public`, `protected`, `internal`, `private`, plus
      `file` since C# 11) — none is an exact match. The faithful mapping
      depends on what "library" means in the target compilation: if each Dart
      `.dart` file ↔ one `.cs` file in the same assembly, `file` is the
      closest (Microsoft Learn: "The file modifier restricts the scope and
      visibility of a top-level type to the file in which it's declared");
      `internal` is the established fallback. NOT `private` (which in C# means
      type-private, strictly narrower than Dart library-private). The codegen
      stage must pick one consistent project-wide policy and apply it
      everywhere — flagged as a project-level convention, NOT a per-construct
      decision.
  - construct_key: dart.field.object_typed_value_holding_int_double_string
    source_form: >-
      class ModedConstant extends ModedTerm { ... final Object value; ...
      bool get isInteger => value is int; bool get isReal => value is double;
      bool get isNumeric => value is num; bool get isString { if (value is!
      String) return false; final s = value as String; return (s.startsWith
      ('"') && s.endsWith('"')) || ...; } bool get isAtom { if (value is!
      String) return false; return !isString; } }
    target_decision: >-
      `public object Value { get; }` (read-only auto-property of type
      `System.Object`, non-nullable — the Dart field is `final Object`, not
      `Object?`). The classification getters become read-only property-style
      members implemented with C# type patterns: `public bool IsInteger =>
      Value is int;`, `public bool IsReal => Value is double;`, `public bool
      IsNumeric => Value is int or double;`. Dart's `num` is the common
      supertype of `int` and `double`; C# has no built-in numeric union, so
      `IsNumeric` is rendered as the disjunctive pattern `Value is int or
      double` (Microsoft Learn pattern-matching: "Use … type pattern with the
      `or` pattern combinator"). NOT mapped to `IConvertible`/`INumber<T>` —
      Dart `num` is closed over `int|double` only and the Dart `is num` test
      is exactly that disjunction. `IsString` keeps the quote-prefix/suffix
      sniffing logic verbatim (no Regex, preserves exact Dart character-level
      semantics).
    idiom_id: null
    research_finding_id: rf-dart-object-union-int-double-string-to-csharp-object-with-type-patterns
    nuance: >-
      Value-vs-reference equality is load-bearing here. Dart `==` on `Object`
      values for `int`, `double`, and `String` is value equality
      (deep-equality on the value, not identity). C# `object.Equals` on boxed
      `int`/`double` (value types) compares boxed contents (value equality is
      preserved via the `Equals(object?)` override on the boxed value type —
      Microsoft Learn: "the Equals method on a boxed value type calls the
      Equals method of the boxed type"); C# `string` overrides `Equals` for
      value equality. The `ModedConstant ==` override (next construct) must
      therefore use `Equals(this.Value, other.Value)` (i.e. `object.Equals(a,
      b)`), NOT `ReferenceEquals` — so two `ModedConstant(consume, 42)`
      instances compare equal. Boxing of `int`/`double` is acceptable here
      (the Dart code already pays this cost via the `Object` field). The
      `is num` → `is int or double` mapping must be exact (Dart `num`
      excludes `BigInt` and `String`, matching the C# disjunction). Nullable:
      `Object` not `Object?` — the C# field MUST be `object`, not `object?`.
  - construct_key: dart.value_class.manual_eq_hashcode_no_collection_member
    source_form: >-
      class ModedConstant ... @override bool operator ==(Object other) =>
      other is ModedConstant && mode == other.mode && value == other.value;
      @override int get hashCode => Object.hash(mode, value);  (similarly
      ModedVariable: name, isReader, _structuralMode)
    target_decision: >-
      Override `Equals(object?)`, implement `IEquatable<ModedConstant>` with
      `bool Equals(ModedConstant? other)`, and override `GetHashCode()` —
      hand-written, comparing `Mode == other.Mode && Equals(Value,
      other.Value)` (using `object.Equals` to dispatch to the boxed value
      type's own `Equals`, NOT `ReferenceEquals` — see preceding construct's
      nuance). `GetHashCode` combines `HashCode.Combine(Mode, Value)`.
      Likewise for `ModedVariable` (name, isReader, _structuralMode). Plain
      `sealed class` — NOT a positional `record`, because `record` synthesised
      equality on an `object` field uses default reference equality for boxed
      values in some edge cases and we want the explicit `object.Equals`
      semantics documented above. Standard `==`/`!=` operators are emitted
      that defer to `Equals` so that idiomatic C# call sites compile.
    idiom_id: null
    research_finding_id: rf-dart-value-class-manual-eq-to-csharp-iequatable-objectequals
    nuance: >-
      Value equality is the load-bearing nuance. Dart `==` is dispatched
      through the value's overridden operator; C# `==` on `object` defaults to
      reference equality unless an operator is provided. The spec mandates
      explicit `==`/`!=` operators plus `IEquatable<T>.Equals(T?)` so that all
      three call paths (`a == b`, `a.Equals(b)`, `EqualityComparer<T>.Default
      .Equals(a, b)`) return identical results. `ModedCompound` is excluded
      from this construct because it has a collection-typed field (handled
      below).
  - construct_key: dart.value_class.manual_eq_with_list_field_element_equality
    source_form: >-
      class ModedCompound ... final List<ModedTerm> args; ... @override bool
      operator ==(Object other) => other is ModedCompound && mode == other.mode
      && functor == other.functor && arity == other.arity && _listEquals(args,
      other.args); @override int get hashCode => Object.hash(mode, functor,
      arity, Object.hashAll(args));  (also ModedPath with steps:
      List<PathStep>)
    target_decision: >-
      `sealed class ModedCompound : ModedTerm` (a plain class, NOT a
      `record`) implementing `IEquatable<ModedCompound>` with hand-written
      `Equals(ModedCompound?)`/`Equals(object?)`/`GetHashCode()` comparing
      `Mode`, `Functor`, `Arity` and an **element-wise**
      `Args.SequenceEqual(other.Args)` over the recursively value-equal
      `ModedTerm` elements; hash combines via `HashCode` accumulator hashing
      each element in sequence. A positional `record` is explicitly REJECTED
      here (same reason as `TypeRef` in `type_ast.dart`): record value
      equality on a `List<ModedTerm>` member compares the list by reference,
      regressing the deep-equality guarantee `_listEquals` exists to provide.
      The same idiom applies to `ModedPath` (whose `steps` is `List<PathStep>`)
      — element-wise `SequenceEqual` + `HashCode` accumulator. `PathStep`
      itself has no collection fields and falls under the preceding scalar-
      value-class construct.
    idiom_id: null
    research_finding_id: rf-dart-list-element-value-equality-to-csharp-sequenceequal
    nuance: >-
      Cached/reused finding from `type_ast.dart` (FR-024 — never re-research):
      Microsoft Learn Records doc is decisive that record value equality on a
      `List<>` member is reference equality, not element-wise. Therefore both
      `ModedCompound` and `ModedPath` MUST be hand-written `IEquatable<T>`
      classes with `SequenceEqual` over their list fields. The
      args/steps lists are aliased not cloned (Dart fields are `final` but
      the lists themselves are not unmodifiable); the C# emission preserves
      this — the list reference is stored as-is and exposed via the field /
      auto-property (codegen MAY choose `IReadOnlyList<ModedTerm>` for the
      public surface to discourage external mutation, mirroring Dart's
      `final` convention). Object.hash on a list calls Object.hashAll
      element-wise; C# replication uses HashCode.Add per element so hash and
      equality remain consistent.
  - construct_key: dart.set_collection.unordered_value_equal_path_collection
    source_form: >-
      Set<ModedPath> paths(ModedTerm t) { final result = <ModedPath>{}; ...
      result.add(ModedPath(prefix)); ... return result; }
    target_decision: >-
      Return `HashSet<ModedPath>` (or `IReadOnlySet<ModedPath>`) — Dart `Set<>`
      defaults to `LinkedHashSet` (insertion-ordered) but the contract here is
      explicitly unordered (no enumeration order dependency is documented or
      observed; the doc-comment says "Returns the set of all paths"). C#
      `HashSet<T>` uses the element's own `Equals`/`GetHashCode` (Microsoft
      Learn: "HashSet<T> ... uses the default equality comparer
      EqualityComparer<T>.Default ... HashSet considers an item to be in the
      set if Equals returns true"), so the hand-written value-equal
      `ModedPath.Equals`/`GetHashCode` (preceding construct) carries the
      deduplication guarantee verbatim. NOT `FrozenSet` here — the set is
      mutated (`Add`) during traversal then returned; FrozenSet is
      construct-once-then-immutable.
    idiom_id: null
    research_finding_id: rf-dart-set-of-value-types-to-csharp-hashset-uses-equatable
    nuance: >-
      Crucially the deduplication semantics rely on `ModedPath` being
      value-equal: a `HashSet<ModedPath>` with a reference-equal ModedPath
      would over-collect duplicates (two structurally identical paths would
      both be added). The preceding construct's hand-written
      `IEquatable<ModedPath>` with element-wise `SequenceEqual` on `Steps` is
      the load-bearing precondition. Ordering: Dart's `LinkedHashSet`
      preserves insertion order; C# `HashSet` does NOT. The Dart consumers
      observed do not rely on order (the only use is set comparison /
      element-wise traversal), so this divergence is benign and explicitly
      noted (SC-006 "never gloss"). If a future consumer needs insertion
      order, the idiom must be revisited to use `OrderedDictionary`-style
      ordered set or a `List<ModedPath>` with explicit dedup.
  - construct_key: dart.factory_constructor.named_convenience_no_caching
    source_form: >-
      factory ModedCompound.listCons(Mode mode, ModedTerm head, ModedTerm
      tail) { return ModedCompound(mode, '[|]', 2, [head, tail]); }  (also
      ModedConstant.nil(Mode mode), ModedVariable.reader(...),
      ModedVariable.writer(...))
    target_decision: >-
      Map each Dart `factory NamedCtor(...)` to a `public static <Class>
      <NamedCtor>(...)` static factory method that returns
      `new ClassName(...)`. C# has no `factory` keyword; a static method is
      the canonical equivalent per dart.dev's own constructor doc. The four
      factories in this file (`ModedCompound.ListCons`,
      `ModedConstant.Nil`, `ModedVariable.Reader`, `ModedVariable.Writer`) do
      NOT cache, do NOT return subtypes, and do NOT access `this`; they are
      pure convenience constructors. The Dart `factory` keyword does not
      survive as syntax in C# but the *behaviour* — "always returns a fresh
      instance via the primary constructor" — is preserved exactly.
    idiom_id: null
    research_finding_id: rf-dart-factory-ctor-const-default-to-csharp-static-factory
    nuance: >-
      Cached/reused finding from `type_ast.dart` (FR-024). The
      `factory`-as-static-method idiom is identical; the
      `const-default-map` nuance from that finding does NOT apply here
      (these factories take no collection defaults). Behaviour: always
      fresh instance — preserved verbatim. The list literal `[head, tail]`
      inside `listCons` maps to `new List<ModedTerm> { head, tail }` (or
      `[head, tail]` with C# 12 collection expressions); semantics
      identical.
  - construct_key: dart.named_required_parameters.required_kwargs
    source_form: >-
      ModedVariable(this.name, {required this.isReader, required Mode
      structuralMode}) : _structuralMode = structuralMode;  (and PathStep:
      named optional bool params with defaults)
    target_decision: >-
      Dart `{required …}` named parameters map to C# named arguments on a
      normal positional-parameter constructor. The faithful idiom is to
      declare a positional ctor (`ModedVariable(string name, bool isReader,
      Mode structuralMode)`) and rely on C# named-argument call syntax at
      call sites (`new ModedVariable("X", isReader: true, structuralMode:
      Mode.Consume)`) — Microsoft Learn: "Named arguments enable you to
      specify an argument for a parameter by matching the argument with its
      name". For `PathStep`'s named *optional* parameters with default
      values, the C# ctor uses default-valued positional parameters
      (`PathStep(string symbol, int argIndex, Mode mode, bool isVariable =
      false, bool isReader = false)`); call sites pass named arguments
      identically to Dart. C# has no per-parameter `required` keyword at
      the constructor level (the `required` modifier exists only for
      members/properties, not ctor parameters — Microsoft Learn:
      "Properties and fields can be marked with the `required` modifier"),
      but a positional parameter without a default IS required by the
      compiler.
    idiom_id: null
    research_finding_id: rf-dart-named-required-params-to-csharp-named-positional
    nuance: >-
      Dart distinguishes positional, named, and required-named; C#
      distinguishes positional and named-via-call-site only. The faithful
      mapping is "positional ctor + named-argument call style" — the
      compile-time requiredness is reproduced (no default ⇒ required), and
      the call-site readability of `required` keyword args is preserved by
      C# named arguments. The construct-level `required` keyword on C#
      properties is a separate feature (for object initializers) and is
      NOT relevant for ctor parameters. Null-safety: `string name` and
      `Mode structuralMode` are non-nullable; `bool isReader` is a value
      type, also non-nullable. Field-init from named arg: Dart `this.name`
      and `: _structuralMode = structuralMode` map to C# ctor body
      assignments `Name = name; structuralModeField = structuralMode;`
      (no special syntax needed). Codebase convention: emit auto-properties
      for public fields, keep `_structuralMode` as a `private readonly`
      backing field (matching the Dart leading-underscore convention for
      "internal"-by-convention scope; see also rf-dart-library-private-
      underscore-to-csharp-file-or-internal for the file-vs-internal
      policy).
  - construct_key: dart.toplevel_function.top_level_helper_pure
    source_form: >-
      bool isConsumed(ModedTerm t) { return t.accept(_IsConsumedVisitor()); }
      bool isProduced(ModedTerm t) {...}  bool isIO(ModedTerm t) {...}
      bool _allPathsValidIO(ModedTerm t, Mode parentMode) {...}  ModedTerm
      dual(ModedTerm t) {...}  Set<ModedPath> paths(ModedTerm t) {...}  void
      _extractPaths(...)  String _symbolOf(ModedTerm t)
    target_decision: >-
      C# has no top-level functions in this codebase convention; emit each
      Dart top-level function as a `public static` method on a host
      `static class ModedTermOps` (or `ModedTerms`). Leading-underscore
      private top-level helpers (`_allPathsValidIO`, `_extractPaths`,
      `_symbolOf`) become `private static` (or `internal static`) on the
      same host, with the file/internal policy from
      rf-dart-library-private-underscore-to-csharp-file-or-internal.
      Bodies map verbatim with the visitor / type-pattern-switch
      substitutions documented in the constructs above.
    idiom_id: null
    research_finding_id: rf-dart-top-level-function-to-csharp-static-method
    nuance: >-
      Cached/reused finding from `mode.dart` (FR-024) —
      `combineMode` mapped to a `static` method on `ModeOps` for exactly
      this reason. Same rule applies here. Naming-collision avoidance:
      the natural host name `ModedTerm` is occupied by the abstract base
      class; the spec mandates a distinct host class name (`ModedTermOps`
      / `ModedTerms`) to avoid the C# type-name clash, matching the
      `Mode`/`ModeOps` precedent. The `_symbolOf` function ends with
      `throw ArgumentError(...)` for the unreachable type-switch arm; this
      maps to `throw new ArgumentException(...)` or `UnreachableException`
      preserving Dart's totality guarantee that C# does NOT compile-time-
      verify.
  - construct_key: dart.list_aliasing.shallow_copy_in_dual_traversal
    source_form: >-
      class _DualVisitor implements ModedTermVisitor<ModedTerm> { ModedTerm
      visitCompound(ModedCompound term) { return ModedCompound(term.mode.flip,
      term.functor, term.arity, term.args.map((arg) => dual(arg)).toList()); }
      ... ModedTerm visitVariable(ModedVariable term) { return ModedVariable
      (term.name, isReader: !term.isReader, structuralMode: term.mode.flip); }
      }
    target_decision: >-
      `term.args.map((arg) => dual(arg)).toList()` maps to LINQ
      `term.Args.Select(arg => Dual(arg)).ToList()` — a NEW backing list is
      constructed; the per-element `ModedTerm` references are recursively
      replaced (the dual visitor allocates new nodes throughout).
      Crucially this is NOT a shared-list shallow copy (contrast `TypeRef
      .dual()` in type_ast.dart which DOES share its typeArgs list): each
      recursive `Dual(arg)` allocates a fresh node, so the entire dual sub-
      tree is structurally fresh — no aliasing between input and output
      trees. The `Mode.flip` and `!isReader` operations are value-type
      flips with no aliasing concern.
    idiom_id: null
    research_finding_id: rf-dart-list-map-tolist-to-csharp-linq-select-tolist
    nuance: >-
      The shallow-vs-deep copy nuance differs between `TypeRef.dual()`
      (shared list reference) and `ModedTerm.dual()` (fresh list, fresh
      sub-tree). The conversion preserves *each* file's semantics
      verbatim — recording the divergence rather than normalising it. The
      involution property (`dual(dual(t)) == t`) holds because (a)
      `Mode.flip.Flip == Mode` (preserved by mode.cs), (b) value-equality
      on each leaf is preserved (preceding constructs), and (c)
      `ModedCompound.Equals` recursively delegates to element `Equals`
      via `SequenceEqual`. C# LINQ `Select(...).ToList()` is the
      established 1:1 mapping for Dart `.map(...).toList()` (Microsoft
      Learn: "ToList<TSource> ... creates a List<T> from an
      IEnumerable<T>"). Eager materialisation matches Dart's eager
      `.toList()`.
  - construct_key: dart.is_pattern_in_function_body.type_test_with_member_access
    source_form: >-
      bool _allPathsValidIO(ModedTerm t, Mode parentMode) { ... if (t is
      ModedCompound) { return t.args.every((arg) => _allPathsValidIO(arg,
      currentMode)); } ... }  (similarly in paths(), _extractPaths(),
      _symbolOf(): chained `if (t is X) ...; else if (t is Y) ...`)
    target_decision: >-
      Map each Dart `if (t is X) … t.member …` (Dart's flow-type-promotion
      makes the member access valid without an explicit cast) to a C#
      declaration pattern `if (t is X x) … x.Member …`, fusing the type
      test and binding into one construct. Where multiple `if (t is X)
      else if (t is Y)` chains appear (`_symbolOf`), prefer a single
      type-pattern `switch` expression with a throwing discard arm:
      `t switch { ModedCompound c => $"{c.Functor}/{c.Arity}",
      ModedConstant c => c.Value.ToString(), ModedVariable v => v.IsReader
      ? $"{v.Name}?" : v.Name, _ => throw new ArgumentException($"Unknown
      moded term type: {t}") }`. The throwing arm preserves Dart's
      totality (since C# does NOT compile-time-verify subtype
      exhaustiveness over the non-language-sealed `ModedTerm` base — same
      rationale as the closed-sum-with-visitor-dispatch construct above).
    idiom_id: null
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Cached/reused finding from `type_ast.dart` (FR-024). Same
      decisive Microsoft Learn citation on declaration patterns fusing
      `is`-test and cast. The Dart `t is ModedCompound` followed by
      `t.args` works because Dart's flow analysis promotes `t`'s type
      within the `if` branch; C# replicates this exactly via the
      declaration-pattern bound variable `c`. Member-name casing changes
      (`functor` → `Functor`, `args` → `Args`, etc.) are mechanical and
      uniform across the file.
  - construct_key: dart.list_every.short_circuit_universal_quantifier
    source_form: >-
      term.args.every((arg) => arg.accept(this))  (also
      `term.args.every((arg) => _allPathsValidIO(arg, currentMode))`)
    target_decision: >-
      Map Dart `iterable.every(predicate)` to C# LINQ `iterable.All
      (predicate)`. Microsoft Learn: "All<TSource>(IEnumerable<TSource>,
      Func<TSource,bool>) — Determines whether all elements of a sequence
      satisfy a condition." Both are short-circuiting on the first
      `false`. Empty-collection semantics are identical (vacuous truth:
      both return `true` for an empty input — Dart docs: `every` on an
      empty iterable returns `true`; LINQ `All` on empty source also
      returns `true`). NOT `Aggregate`/`fold` — `All` is the named idiom.
    idiom_id: null
    research_finding_id: rf-dart-iterable-every-to-csharp-linq-all
    nuance: >-
      The empty-collection vacuous-truth nuance is preserved exactly
      (both languages return `true`), which is essential for the `isConsumed`
      / `isProduced` recursion at leaf nodes — though leaf nodes are
      `ModedConstant`/`ModedVariable` (not `ModedCompound`) and never hit
      the `every` path on their args (constants/variables have no args
      field), so the vacuous-truth case is effectively unreachable.
      Recorded for completeness per SC-006.
  - construct_key: dart.string_concatenation.unicode_arrow_glyphs
    source_form: >-
      final modeStr = mode == Mode.consume ? '↓' : '↑';  if (isListCons) {
      return '$modeStr[${args[0]}|${args[1]}]'; }  return steps.map((s) =>
      '(${s.symbol}, ${s.argIndex}, ${s.mode})').join(' → ');
    target_decision: >-
      Map Dart string interpolation `'$x $y'` and `'${expr}'` to C#
      interpolated strings `$"{X} {Y}"` / `$"{Expr}"`. The literal Unicode
      glyphs `↓` (U+2193), `↑` (U+2191), `→` (U+2192) are preserved
      verbatim in the C# source (the file must be UTF-8; .NET source files
      default to UTF-8 in modern toolchains and Roslyn explicitly accepts
      Unicode identifiers and string literals — Microsoft Learn:
      "C# source code is processed as Unicode"). Dart `<list>.join(', ')`
      maps to `string.Join(", ", <enumerable>)` — Microsoft Learn:
      "string.Join(string?, IEnumerable<string?>) — Concatenates the
      members of a constructed IEnumerable<T> ..., using the specified
      separator between each member." The `.map(...).join(...)` chain
      becomes `string.Join(", ", coll.Select(s => $"({s.Symbol}, ...)"))`.
    idiom_id: null
    research_finding_id: rf-dart-string-interp-unicode-to-csharp-interpolated-string-utf8
    nuance: >-
      The Unicode arrow glyphs are semantically load-bearing (they ARE
      the visual mode notation that GLP uses; substituting `v`/`^`/`->`
      would silently change the displayed mode notation across every
      ToString and every debug trace). The .NET source-encoding nuance is
      explicit: C# files default to UTF-8 in dotnet SDK projects;
      historically Visual-Studio-saved files needed a BOM. Codegen MUST
      emit UTF-8-encoded sources (with or without BOM, modern .NET
      tooling accepts both). NO transcoding to `↓` escapes (would
      compile identically but obscures the source). Equivalence at
      runtime: `'↓'` in Dart and `"↓"` in C# are both single-code-point
      UTF-16 strings (BMP code point 0x2193 fits in one UTF-16 unit).
  - construct_key: dart.toString_override.debug_formatting
    source_form: >-
      @override String toString() { final modeStr = ...; if (isListCons)
      {...} ... }  (on ModedCompound, ModedConstant, ModedVariable,
      ModedPath, PathStep)
    target_decision: >-
      Override `public override string ToString()` on each of the five
      classes verbatim, using interpolated strings (preceding construct).
      C# `object.ToString` is virtual and intended for debug/display
      purposes (Microsoft Learn: "Object.ToString — Returns a string
      that represents the current object."). The Dart `@override` annotation
      maps to C# `override` keyword. Semantics preserved exactly: each
      ToString reproduces the exact mode glyph + arity-aware formatting
      (`↓functor`, `↓functor(arg1, arg2)`, `↓[head|tail]`, `↓value`,
      `name?`/`name`).
    idiom_id: null
    research_finding_id: rf-dart-tostring-override-to-csharp-tostring-override
    nuance: >-
      Trivial 1:1 mapping; recorded because every node type overrides
      it and any divergence (e.g. accidentally inheriting
      object.ToString) would break debug output uniformly. NOT routed
      through a custom debug formatter or DebuggerDisplay attribute —
      preserves the source's choice of `ToString()` as the user-facing
      debug representation.
conversion_units:
  - abstract class ModedTerm (abstract Mode property; abstract T Accept<T>(IModedTermVisitor<T>))
  - sealed class ModedCompound : ModedTerm (Mode, Functor, Arity, Args; IsListCons; static factory ListCons; ToString; IEquatable<ModedCompound> Equals/GetHashCode using SequenceEqual on Args)
  - sealed class ModedConstant : ModedTerm (Mode, Value; static factory Nil; IsNil/IsInteger/IsReal/IsNumeric/IsString/IsAtom via type patterns; ToString; IEquatable<ModedConstant> Equals/GetHashCode using object.Equals on Value)
  - sealed class ModedVariable : ModedTerm (Name, IsReader, private readonly Mode structuralMode field; static factories Reader/Writer; Mode getter; ImplicitMode; IsModeConsistent; IsWriter; ToString; IEquatable<ModedVariable> Equals/GetHashCode)
  - interface IModedTermVisitor<out T> (VisitCompound, VisitConstant, VisitVariable)
  - file sealed class IsConsumedVisitor : IModedTermVisitor<bool>
  - file sealed class IsProducedVisitor : IModedTermVisitor<bool>
  - file sealed class DualVisitor : IModedTermVisitor<ModedTerm>
  - sealed class ModedPath (Steps : IReadOnlyList<PathStep>; Root; Leaf; IsInputPath; IsOutputPath; Length; ToString; IEquatable<ModedPath> Equals/GetHashCode using SequenceEqual on Steps)
  - sealed class PathStep (Symbol, ArgIndex, Mode, IsVariable, IsReader; IsWriter; ToString; IEquatable<PathStep> Equals/GetHashCode)
  - static class ModedTermOps (IsConsumed, IsProduced, IsIO, Dual, Paths static methods; private static AllPathsValidIO, ExtractPaths, SymbolOf helpers)
escalations: []
```

## Rationale & Research Provenance

This file is the GLP type-checker's *moded-term* hierarchy (Definition 4.2 of
the GLP paper): a closed three-leaf algebraic sum (`ModedCompound`,
`ModedConstant`, `ModedVariable`) under an `abstract ModedTerm` base, three
visitor implementations (`_IsConsumedVisitor`, `_IsProducedVisitor`,
`_DualVisitor`) over an `abstract ModedTermVisitor<T>` contract, a parallel
path hierarchy (`ModedPath` aggregating `List<PathStep>`), and a handful of
top-level helpers (`isConsumed`, `isProduced`, `isIO`, `dual`, `paths`,
`_symbolOf`). The non-mechanical Dart→C# decisions all centre on (a) closed
sum types without language `sealed`, (b) value-equality with collection
fields, (c) the visitor pattern as a pure contract vs base class, and (d)
the `Object`-typed value field with type-pattern classification — each
grounded against the official docs below. FR-024 cache: five of the rf-*
findings are *reused verbatim* from `mode.dart`, `type_ast.dart`, and
`prelude.dart` (the three already-specced sibling files in this same
directory) — no re-research, per the FR-024 mandate.

### rf-dart-abstract-ast-base-to-csharp-abstract-sealed-leaves (CACHED, reused from type_ast.dart)

**Deep analysis.** `ModedTerm` is an `abstract class` declaring `Mode mode`
(getter) and `T accept<T>(ModedTermVisitor<T> visitor)`; three concrete
subclasses (`ModedCompound`, `ModedConstant`, `ModedVariable`) implement
both. The hierarchy is *open* in Dart (no `sealed class`) but is used as a
closed sum: every consumer (`_allPathsValidIO`, `_extractPaths`, `_symbolOf`,
the three visitors) enumerates exactly the three concrete subtypes. No
fourth subtype exists or is contemplated by the spec
(`docs/modules/moded-term.md v0.5`, Definition 4.2 in the GLP paper).

**Research (authoritative, CACHED).** Reused from `type_ast.dart` finding:
Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/language-
reference/keywords/sealed` — *"It's an error to use the abstract modifier
with a sealed class"*. Therefore the C# base CANNOT be `sealed abstract`;
closure is expressed by sealing the three leaves
(`sealed class ModedCompound : ModedTerm` etc.) and by a throwing default
arm in every consumer type-pattern switch. FR-024 cache hit — no second
research call.

**Conclusion.** `abstract class ModedTerm` + three `sealed class` leaves;
all are reference types (heap-allocated `class`, never `struct`/`record
struct`) so shared sub-tree aliasing (which `_extractPaths`'s
`prefix`/`[...prefix, childStep]` list relies on) is preserved.

### rf-dart-abstract-class-pure-contract-to-csharp-interface (NEW)

**Deep analysis.** `ModedTermVisitor<T>` is an `abstract class` declaring
three abstract methods (`visitCompound`, `visitConstant`, `visitVariable`)
and *nothing else* — no fields, no concrete methods, no constructor. It is
a pure structural contract used only to implement the visitor pattern.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/object-
oriented/inheritance` and the C# language reference for `interface`
(`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
keywords/interface`) — Microsoft Learn: an interface "defines a contract.
Any class, record or struct that implements that contract must provide an
implementation of the members defined in the interface." Verbatim query:
"C# interface vs abstract class no fields contract". Authoritative
conclusion: when a Dart `abstract class` carries no state and no concrete
methods, it is functionally a structural contract; the C# idiomatic
counterpart is `interface`, NOT `abstract class`. The `out T` covariance
modifier is documented at
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/
covariance-contravariance/` — "you can use a covariant type as the return
type of a method".

**Conclusion.** Emit `public interface IModedTermVisitor<out T>` with three
abstract methods. `out T` is added because `T` only appears in return
position; it is a strict safety generalisation that does not break any
existing call site (which uses an exact-`T` visitor) and allows covariant
upcasts. The three Dart `_*Visitor` implementations become `: IModedTermVisitor<...>`
implementing classes. `I`-prefix is the .NET convention (Microsoft Learn
naming guidelines).

### rf-dart-library-private-underscore-to-csharp-file-or-internal (NEW)

**Deep analysis.** Dart denotes "library-private" scope (visible only within
the same `.dart` file in this project's convention) by a leading underscore
on the identifier. `moded_term.dart` uses this for three visitor classes
(`_IsConsumedVisitor`, `_IsProducedVisitor`, `_DualVisitor`), one field
(`_structuralMode`), and three helper functions (`_allPathsValidIO`,
`_extractPaths`, `_symbolOf`, `_listEquals`, `_symbolOf`). C# has five access
modifiers: `public`, `protected`, `internal`, `private`, plus `file` (C# 11+).
None is an exact match for Dart library-private.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/
file` — Microsoft Learn, decisive: *"The file modifier restricts the scope
and visibility of a top-level type to the file in which it's declared."*
Available since C# 11. WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-
and-structs/access-modifiers` — `internal` is "the type or member can be
accessed by any code in the same assembly, but not from another assembly".
Verbatim query: "C# file scoped type access modifier C# 11 closest to Dart
library private". Authoritative conclusion: `file` is the closest exact
match for Dart's "library-private (file-private in single-file libraries)";
`internal` is the documented fallback for projects targeting pre-C#-11
toolchains. NOT `private` (which is type-private — strictly narrower).

**Conclusion.** Emit the three visitor classes and helper functions as `file
sealed class` / `private static` (helpers inside `ModedTermOps`)
respectively. The `_structuralMode` field becomes `private readonly Mode`
backing field with a public getter property `Mode` (overriding `ModedTerm
.Mode`). Codegen MUST pick one consistent policy project-wide; this spec
documents the decision rule, the codegen stage applies it.

### rf-dart-object-union-int-double-string-to-csharp-object-with-type-patterns (NEW)

**Deep analysis.** `ModedConstant.value` is typed `Object` (the most-general
Dart type) and holds either `int`, `double`, or `String`. The six
classification getters (`isInteger`, `isReal`, `isNumeric`, `isString`,
`isAtom`, `isNil`) use `is`-tests against these specific types, with
`isNumeric` testing the Dart common supertype `num` (which is closed over
`int | double` only — not `BigInt`, not `String`).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/
pattern-matching` — Microsoft Learn: type patterns "test the type of the
variable, and assign it to a new variable" and the `or` pattern combinator
"composes a logical disjunction of patterns" (`is int or double`). WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/types/
boxing-and-unboxing` — boxed value types preserve their type for runtime
`is`-tests and `Equals` (the boxed type's own `Equals` is dispatched).
Authoritative conclusion: a Dart `Object` field holding `int|double|String`
maps to C# `object` (non-nullable), with type-pattern `is int`,
`is double`, `is int or double`, `is string` tests reproducing Dart's
`is int`, `is double`, `is num`, `is String` *exactly*. Verbatim query:
"C# is pattern int double disjunction or pattern; boxed value type Equals".

**Conclusion.** Use `public object Value { get; }` (NOT `object?` — Dart
field is `Object`, not `Object?`) and emit each classification getter as a
property-style expression member using the documented patterns. `IsNumeric`
becomes `Value is int or double` (decisive `or`-pattern citation). The
quote-prefix string sniffing in `IsString` is preserved character-for-
character; no Regex substitution.

### rf-dart-value-class-manual-eq-to-csharp-iequatable-objectequals (NEW)

**Deep analysis.** `ModedConstant` and `ModedVariable` override `==`/`hashCode`
manually with no collection-typed field. `ModedConstant ==` compares
`mode == other.mode && value == other.value` — the latter dispatches through
the boxed value's own `==` (Dart `int.==`, `double.==`, `String.==` are all
value equality). `ModedVariable ==` compares three scalar fields. `PathStep
==` compares five scalar fields. Hash uses `Object.hash(...)` (combining
scalar values).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1` —
Microsoft Learn: "Defines a generalized method that a value type or class
implements to create a type-specific method for determining equality of
instances." WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.object.equals#system-
object-equals(system-object-system-object)` — `object.Equals(object?,
object?)`: "Calls the Equals(Object) method ... if the two objects
represent the same object reference, or if both are null, this method
returns true." For boxed value types, this dispatches to the boxed type's
own `Equals` override. WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.hashcode.combine` —
`HashCode.Combine` "combines hash codes of up to eight values into a
single hash code". Verbatim queries: "C# IEquatable<T> sealed class
manual equality"; "C# object.Equals boxed int double string value
equality"; "C# HashCode.Combine equivalent of Object.hash".

**Conclusion.** `ModedConstant`, `ModedVariable`, and `PathStep` each
implement `IEquatable<T>` with manual `Equals(T?)`, `Equals(object?)`,
`GetHashCode()`, and operators `==`/`!=`. Equality on the `object Value`
field uses `Equals(this.Value, other.Value)` (i.e. `object.Equals(object?,
object?)` — the static method) so boxed `int`/`double`/`string` dispatch
to their own `Equals`. `HashCode.Combine` replaces `Object.hash`. NOT a
positional `record` — record equality on an `object` field uses default
equality which is reference equality unless explicitly overridden (the
`object` field has no compile-time type that the record synthesiser can
distinguish), introducing the same kind of regression `_listEquals`
prevents on collection fields.

### rf-dart-list-element-value-equality-to-csharp-sequenceequal (CACHED, reused from type_ast.dart)

**Deep analysis.** `ModedCompound ==` compares `_listEquals(args,
other.args)` (element-wise on `List<ModedTerm>`); `ModedPath ==` compares
`_listEquals(steps, other.steps)` (element-wise on `List<PathStep>`).
`_listEquals` is the same helper as in `type_ast.dart` — recurses through
`==` on each element. `ModedPath` is used as a `HashSet<ModedPath>` element
(see next construct), so value-equality is load-bearing for set
deduplication.

**Research (authoritative, CACHED).** Reused from `type_ast.dart` finding:
Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/language-
reference/builtin-types/record` — record value equality on a `List<>`
member is *reference equality*. FR-024 cache hit — no second research call.

**Conclusion.** Both `ModedCompound` and `ModedPath` are hand-written
`sealed class` implementing `IEquatable<T>` with `SequenceEqual` over their
list fields (`Args` / `Steps`) and `HashCode` accumulator combining the
scalar fields plus each element's hash. A positional `record` is
explicitly rejected (would silently break set-of-paths deduplication and
recursive ModedCompound equality).

### rf-dart-set-of-value-types-to-csharp-hashset-uses-equatable (NEW)

**Deep analysis.** `paths()` returns `Set<ModedPath>`; the set is built by
mutating `result.add(ModedPath(prefix))` during traversal. Dart `Set<T>`
defaults to `LinkedHashSet` (insertion-ordered); membership and `add`
deduplication use the element's own `==`/`hashCode`. The doc-comment
("Returns the set of all paths") names it a set; no callers documented
or observed depend on enumeration order.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.
hashset-1` — Microsoft Learn: "HashSet<T> ... uses the default equality
comparer EqualityComparer<T>.Default unless a different equality comparer
is specified" and "HashSet considers an item to be in the set if Equals
returns true". Verbatim query: "C# HashSet uses default equality comparer
IEquatable membership". Authoritative conclusion: `HashSet<ModedPath>`
with `ModedPath` implementing `IEquatable<ModedPath>` (preceding
construct) achieves deduplication semantics equivalent to Dart
`Set<ModedPath>`.

**Conclusion.** Return `HashSet<ModedPath>` (or expose as
`IReadOnlySet<ModedPath>`); deduplication relies on the hand-written
`ModedPath.Equals`/`GetHashCode` from the preceding construct. NOT
`FrozenSet` — the set is mutated during construction, then returned;
FrozenSet is immutable-after-creation. NOT `ImmutableHashSet` — would
add unnecessary copy-on-write overhead during the linear traversal.
The Dart→C# divergence in enumeration order (LinkedHashSet vs HashSet)
is benign for documented callers; explicitly noted per SC-006.

### rf-dart-factory-ctor-const-default-to-csharp-static-factory (CACHED, reused from type_ast.dart)

**Deep analysis.** Four `factory` constructors: `ModedCompound.listCons`,
`ModedConstant.nil`, `ModedVariable.reader`, `ModedVariable.writer`. None
caches, none returns a subtype, none accesses `this`; each is a pure
convenience constructor delegating to the primary constructor with
fixed/computed arguments.

**Research (authoritative, CACHED).** Reused from `type_ast.dart` finding:
dart.dev `https://dart.dev/language/constructors` documents factory ctors;
the C# analog is "a static factory method". FR-024 cache hit.

**Conclusion.** Each Dart `factory` becomes a C# `public static <Class>
<Name>(...)` static method on the class, returning a new instance via the
primary constructor. The `const-default-map` nuance of the cached finding
does NOT apply here (no factory uses collection defaults).

### rf-dart-named-required-params-to-csharp-named-positional (NEW)

**Deep analysis.** `ModedVariable(this.name, {required this.isReader,
required Mode structuralMode})` declares one positional and two
required-named parameters; `PathStep` declares three required-named
parameters plus two optional-with-default. Call sites use named-argument
syntax throughout (`ModedVariable.reader(name, structuralMode: m)`).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-
and-structs/named-and-optional-arguments` — Microsoft Learn: "Named
arguments enable you to specify an argument for a parameter by matching
the argument with its name rather than with its position in the parameter
list" and "If a positional argument has been omitted, all positional
arguments to the right of it must also be omitted. Named arguments can
be specified in any order." WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
proposals/csharp-11.0/required-members` — `required` modifier in C# 11
applies only to **members** (fields/properties), NOT to constructor
parameters. Verbatim query: "C# named arguments optional default; required
modifier members not parameters". Authoritative conclusion: the faithful
mapping is "positional ctor parameters (without defaults ⇒ required) +
named-argument call style" — there is no language-level `required` for
ctor parameters.

**Conclusion.** Emit `public ModedVariable(string name, bool isReader,
Mode structuralMode)` and `public PathStep(string symbol, int argIndex,
Mode mode, bool isVariable = false, bool isReader = false)`. Call sites
use C# named-argument syntax (`new ModedVariable("X", isReader: true,
structuralMode: Mode.Consume)`) reproducing Dart's call-site readability
exactly. Required-ness is enforced by absence of a default (compile-time).

### rf-dart-top-level-function-to-csharp-static-method (CACHED, reused from mode.dart)

**Deep analysis.** Five public top-level functions (`isConsumed`,
`isProduced`, `isIO`, `dual`, `paths`) and three private helpers
(`_allPathsValidIO`, `_extractPaths`, `_symbolOf`) at library level. None
accesses any global state; all are pure transformations on a `ModedTerm`
parameter.

**Research (authoritative, CACHED).** Reused from `mode.dart` finding:
C# has no top-level functions in this codebase convention; emit as
`public static` / `private static` methods on a host static class. FR-024
cache hit.

**Conclusion.** Host class `ModedTermOps` (NOT `ModedTerm` — that name is
occupied by the abstract base, same C# type-name clash as `Mode`/`ModeOps`
in mode.dart). Public functions are `public static`, private helpers are
`private static`. The naming-collision rule from the cached finding is
identical and reapplied. The `throw ArgumentError(...)` at the unreachable
arm of `_symbolOf` becomes `throw new ArgumentException(...)` (or
`UnreachableException`), preserving Dart's documented totality at the
runtime layer that C#'s compile-time exhaustiveness check cannot enforce
over a non-language-sealed base.

### rf-dart-list-map-tolist-to-csharp-linq-select-tolist (NEW)

**Deep analysis.** `_DualVisitor.visitCompound` constructs the dual via
`term.args.map((arg) => dual(arg)).toList()` — eager map-then-materialise.
The result is a *new* `List<ModedTerm>` with recursively-fresh nodes; no
aliasing between input and output sub-trees.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.select`
— Microsoft Learn: "Projects each element of a sequence into a new form."
WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.tolist`
— Microsoft Learn: "Creates a List<T> from an IEnumerable<T>." Verbatim
query: "C# LINQ Select ToList equivalent Dart map toList eager".
Authoritative conclusion: `Select(...).ToList()` is the 1:1 LINQ mapping
for Dart `.map(...).toList()`, both eager.

**Conclusion.** `term.args.map((arg) => dual(arg)).toList()` →
`term.Args.Select(arg => Dual(arg)).ToList()` (or `.ToList()` over a
collection-expression with C# 12). Eager materialisation matches Dart's
eager `.toList()` so the involution property `dual(dual(t)) == t` holds
at the value-equality layer. The list aliasing contrast with
`TypeRef.dual()` (which DOES alias its typeArgs list) is explicit and
preserved — each file's semantics are recorded verbatim, not normalised.

### rf-dart-extension-is-as-to-csharp-type-pattern-switch (CACHED, reused from type_ast.dart)

**Deep analysis.** `_allPathsValidIO`, `paths`, `_extractPaths`, and
`_symbolOf` use `if (t is ModedCompound) { … t.args … }` and
`t is ModedVariable ? t.isReader : false` patterns, relying on Dart's flow
type-promotion.

**Research (authoritative, CACHED).** Reused from `type_ast.dart` finding:
Microsoft Learn pattern-matching doc — declaration patterns fuse
`is`-test + cast. FR-024 cache hit.

**Conclusion.** Map each `if (t is X)` chain to a C# declaration-pattern
`if (t is X x)`; map chained `if (t is X) … else if (t is Y) … else …`
sequences to a `switch` expression with a throwing discard arm
(preserving Dart totality at runtime — C# does not compile-time-verify
exhaustiveness over the non-language-sealed `ModedTerm` base).

### rf-dart-iterable-every-to-csharp-linq-all (NEW)

**Deep analysis.** Two uses of `term.args.every(predicate)` — in
`_IsConsumedVisitor.visitCompound`, `_IsProducedVisitor.visitCompound`,
and (analogous arg-traversal) in `_allPathsValidIO`. Short-circuiting
universal quantifier.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.all`
— Microsoft Learn: "Determines whether all elements of a sequence satisfy
a condition." Documents the short-circuit and the empty-sequence vacuous-
truth (returns `true` on empty). WebFetch
`https://api.dart.dev/stable/dart-core/Iterable/every.html` — dart.dev
api: "Checks whether every element of this iterable satisfies test."
Both vacuous-truth on empty. Verbatim query: "C# LINQ All short-circuit
empty true; Dart Iterable.every".

**Conclusion.** `iter.every(p)` → `iter.All(p)` (LINQ). Short-circuit
and empty-sequence semantics are identical, preserving the recursion
base-case behaviour at any (unreachable) leaf without re-derivation.

### rf-dart-string-interp-unicode-to-csharp-interpolated-string-utf8 (NEW)

**Deep analysis.** String interpolation appears throughout
(`'$modeStr$functor(${args.join(', ')})'`, `'(${s.symbol}, ${s.argIndex},
${s.mode})'`, etc.); literals contain Unicode arrow glyphs `↓` (U+2193),
`↑` (U+2191), `→` (U+2192) that are semantically the GLP mode notation.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/
interpolated` — Microsoft Learn: "Interpolated strings provide a more
readable and convenient syntax to format strings. ... `$\"Name = {name}\"`".
WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.string.join#system-
string-join(system-string-system-collections-generic-ienumerable((system-
string)))` — `string.Join(string, IEnumerable<string>)` "Concatenates the
members of a constructed IEnumerable<T> ..., using the specified separator
between each member." On encoding: the .NET SDK defaults to UTF-8 source
files; Roslyn handles Unicode identifiers and string literals per the
C# language specification (`https://learn.microsoft.com/en-us/dotnet/
csharp/specification/`). Verbatim queries: "C# interpolated string syntax
$\"\""; "C# string.Join IEnumerable separator"; "C# source file UTF-8
encoding Unicode literals".

**Conclusion.** Dart `'…$x…'` → C# `$"…{X}…"`; Dart `'${expr}'` →
C# `$"{Expr}"`. Dart `coll.join(sep)` → C# `string.Join(sep, coll)`.
Unicode arrow glyphs preserved verbatim in UTF-8 C# sources. NO
transcoding to `↓` escapes; the glyph IS the semantic notation
and must be readable in source.

### rf-dart-tostring-override-to-csharp-tostring-override (NEW)

**Deep analysis.** `@override String toString()` on `ModedCompound`,
`ModedConstant`, `ModedVariable`, `ModedPath`, `PathStep` — five
overrides, each producing a mode-glyph-prefixed compact representation
for debug / display.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.object.tostring` —
Microsoft Learn: "Object.ToString — Returns a string that represents the
current object. ... The default implementation of the Object.ToString
method returns the fully qualified name of the type of the Object. ...
You may override the Object.ToString method." Verbatim query:
"C# object.ToString virtual override debug representation".

**Conclusion.** Trivial 1:1 — Dart `@override String toString()` →
C# `public override string ToString()`. Implementation bodies use the
string-interpolation idiom (preceding construct). No DebuggerDisplay
attribute; preserving the source's choice of `ToString` as the debug
surface.

### Explicitly addressed well-known nuances (per SC-006 / US2-AS4)

1. **Sealed-base illegality.** `abstract class ModedTerm` cannot be `sealed
   abstract` in C# (Microsoft Learn explicit error); closure expressed by
   sealing the three concrete leaves AND by throwing default arms in every
   type-pattern switch consumer.
2. **Value-vs-reference equality.** All five hand-written equality
   implementations use `IEquatable<T>` + `Equals(object?)` + `==`/`!=`
   operators, NOT records — because record equality on `object` /
   `List<>` fields silently regresses to reference equality at those
   members.
3. **Mode-glyph encoding.** Unicode arrows are semantically load-bearing;
   transcoding to escapes would obscure the source. UTF-8 source files
   mandated.
4. **Dart `Object` ↔ C# `object`.** Non-nullable on both sides; boxed
   `int`/`double`/`string` dispatch correctly through `object.Equals` for
   `ModedConstant ==`.
5. **Set ordering divergence.** Dart `Set` is insertion-ordered (LinkedHashSet);
   C# `HashSet` is not. Observed callers do not depend on order — recorded as
   benign per SC-006, NOT glossed.
6. **List aliasing contrast with `TypeRef.dual()`.** `ModedTerm.dual()`
   allocates a fresh sub-tree (no aliasing); `TypeRef.dual()` aliases its
   `typeArgs` list. Each file's semantics preserved verbatim.
7. **Visitor: abstract class vs interface.** Pure structural contract (no
   fields/no concrete methods) maps to `interface`, not `abstract class`.
   Covariant `out T` added safely (return position only).
8. **Library-private (`_`-prefix).** Mapped to C# `file` (C# 11+) or
   `internal` (fallback); project-wide policy decision, not per-construct.
9. **Named-required parameters.** Mapped to positional ctor + named-argument
   call style (C# has no parameter-level `required`).

### No escalations

All twelve non-trivial constructs resolved against official Dart/.NET
documentation with consistent conclusions. Five rf-* findings are
cached/reused from the three already-specced sibling files (`mode.dart`,
`type_ast.dart`, `prelude.dart`) per FR-024 — no re-research, no
re-derivation. Seven rf-* findings are first-seen for this file and
recorded with verbatim authoritative citations. `open_escalation_count`
= 0.
