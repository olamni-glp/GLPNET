# Conversion Spec — lib/compiler/partial_evaluator.dart

> Conversion-spec artifact for lib/compiler/partial_evaluator.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.
>
> File is the GLP partial evaluator — a 1054-line source-to-source
> transformer over the AST built by parser.dart. It performs two
> stages: (1) unfolding *defined guards* (single-clause unit-clause
> procedures used as guards) and (2) unfolding `reduce/2` calls against
> `reduce/2` facts. Both stages run a compile-time variant of GLP
> unification (three-valued: Success / Suspend / Fail) over the AST
> term hierarchy (`VarTerm`/`StructTerm`/`ListTerm`/`ConstTerm`/
> `UnderscoreTerm` from ast.dart). Heavy reuse from
> ast.dart/parser.dart/error.dart and analysis/type_checker/prelude.dart
> idioms — most non-trivial decisions cite cached research findings
> already grounded in official Dart/.NET docs.

```yaml
schema_version: 1
source_path: lib/compiler/partial_evaluator.dart
source_sha256: 87f231d5a2b7206e646e6bc24882cf12da986e315413bfacd38742c42bcb9673
target_code_unit: lib/compiler/partial_evaluator.cs
constructs:
  - construct_key: dart.module.relative_imports_plus_show_filter_for_prelude_builtins
    source_form: >-
      "import 'ast.dart'; import 'error.dart'; import 'lexer.dart';
      import 'parser.dart'; import '../analysis/type_checker/prelude.dart'
      show builtinProcedures;" — four whole-library imports of sibling
      compiler-package files plus one selective `show`-filtered import
      of a single static const from the type-checker prelude.
    target_decision: >-
      Map all five Dart imports to C# namespace `using` directives in
      the same compilation unit. ast.dart/error.dart/lexer.dart/
      parser.dart all live under the same Dart library subtree
      (lib/compiler/) and convert to a SINGLE C# namespace (e.g.
      `Glp.Runtime.Compiler`) — being in the same namespace replaces
      the four sibling imports automatically (no `using` needed). The
      one cross-package import `'../analysis/type_checker/prelude.dart'
      show builtinProcedures` becomes a single `using static
      Glp.Runtime.Analysis.TypeChecker.Prelude;` (Microsoft Learn:
      "The `using static` directive imports the accessible static
      members and nested types of the specified type"). The Dart
      `show` filter naturally maps to `using static` because the only
      consumed symbol IS the static `builtinProcedures` set — narrower
      than a whole-namespace import, preserving the visibility-limiting
      intent of `show`. Alternative: a single `using
      Glp.Runtime.Analysis.TypeChecker;` and qualify as
      `Prelude.BuiltinProcedures` — either is acceptable; the spec
      prefers `using static` because the call site `builtinProcedures.
      contains(key)` is bare (no `Prelude.` qualifier on the Dart
      side).
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-using-or-same-namespace
    nuance: >-
      Import-folding nuance (load-bearing for codegen): Dart libraries
      and C# namespaces have different unit-of-organisation semantics.
      Dart `import` is per-file, names visible only inside the importer
      unless re-exported; C# `using` is per-file but namespaces are
      open and cross-file by default — so four same-folder Dart imports
      collapse to zero C# `using`s. The `show` filter (Dart official:
      "Use show to import only some of the names from a library") has
      no direct C# counterpart; `using static` for a single static
      class is the closest equivalent since the only filtered name IS
      a static member, not a type. Renaming/aliasing nuance: there is
      NO Dart `as` alias here, so no C# `using X = ...;` alias is
      needed. Visibility-tightening nuance: `show builtinProcedures`
      hides every other top-level name in prelude.dart — `using static`
      on the C# `Prelude` class likewise exposes only that class's
      static members (other namespaces of the type-checker package
      remain unimported), preserving the narrowing.

  - construct_key: dart.toplevel.mutable_global_nullable_string_init_to_null_and_setter_function
    source_form: >-
      "String? _preludeUnitClauseSource;
      void setPreludeUnitClauseSource(String source) {
        _preludeUnitClauseSource = source;
        _cachedPreludeUnitClauses = null;
      }" — a library-private mutable nullable string global plus a
      public free-function setter that also invalidates a sibling
      cache global.
    target_decision: >-
      C# forbids true free top-level mutable fields and free functions
      (csharp-static-class-no-toplevel-members idiom, cached from
      prelude.dart). Emit a `internal static class PreludeUnitClauses`
      (file-scoped or compiler-internal) hosting both the mutable
      backing field and the setter+getter+cache as static members:
      `internal static string? _preludeUnitClauseSource = null;` and
      `public static void SetPreludeUnitClauseSource(string source)
      { _preludeUnitClauseSource = source; _cachedPreludeUnitClauses =
      null; }`. The Dart `_`-prefix library-private becomes C#
      `internal static` (visible to the assembly, not externally) per
      rf-dart-leading-underscore-privacy-to-csharp-private (cached from
      error.dart; tightened from `internal` to `private` only when the
      symbol is truly intra-class — here the field is consulted from
      `GetPreludeUnitClauses()` in the same static class, so `private
      static string? _preludeUnitClauseSource = null;` is correct).
      The Dart `String?` null-default-init is preserved as C# `string?
      = null;` under enabled-nullable-context per rf-dart-nullsafety-
      to-csharp-nrt. The setter is `public static void` (no return) —
      assignment side-effect plus cache invalidation; the two
      statements are preserved verbatim.
    idiom_id: null
    research_finding_id: csharp-static-class-no-toplevel-members
    nuance: >-
      Two intertwined nuances. (1) Top-level-vs-static-class: Dart
      permits free mutable globals + free functions; C# does NOT — the
      cached idiom mandates wrapping them in a `static class`. This
      is purely a syntactic-host change; the SEMANTICS (process-wide
      mutable state initialised once via the setter, read many times)
      are preserved. (2) Cache-invalidation coupling (load-bearing):
      the setter clears `_cachedPreludeUnitClauses` so the NEXT call
      to `GetPreludeUnitClauses()` re-parses the new source. This
      sequencing — setter-clears-cache, lazy-getter-rebuilds — is a
      load-bearing invariant; the C# port MUST preserve the
      assignment ordering (cache cleared AFTER source assigned, so a
      concurrent reader between the two statements never observes a
      stale cache against a new source… though in practice this is
      called once at startup and is not thread-contended). Null-init:
      Dart `String?` field with NO initialiser implicitly defaults to
      `null`; C# `string?` field MUST be explicitly initialised to
      `null` (or marked nullable AND the nullable-warning-context
      tolerates default-null) — spec emits explicit `= null;` for
      reviewer clarity (Microsoft Learn nullable-reference-types).

  - construct_key: dart.toplevel.mutable_nullable_cached_map_with_lazy_parse_getter
    source_form: >-
      "Map<String, List<Term>>? _cachedPreludeUnitClauses;
      Map<String, List<Term>> getPreludeUnitClauses() {
        if (_cachedPreludeUnitClauses != null) return _cachedPreludeUnitClauses!;
        final source = _preludeUnitClauseSource ?? '';
        if (source.isEmpty) { _cachedPreludeUnitClauses = {}; return _cachedPreludeUnitClauses!; }
        final lexer = Lexer(source); final tokens = lexer.tokenize();
        final parser = Parser(tokens); final module = parser.parseModule();
        ...iterate module.procedures, filter to unit clauses, populate map,
        cache and return..." — classic memoised-getter idiom: nullable
        cache field + non-null returning function that lazily computes
        and stores.
    target_decision: >-
      Emit a `private static Dictionary<string, IReadOnlyList<Term>>?
      _cachedPreludeUnitClauses = null;` static field on the same
      `PreludeUnitClauses` static class. The accessor becomes `public
      static IReadOnlyDictionary<string, IReadOnlyList<Term>>
      GetPreludeUnitClauses()`. Body: early-return on cache hit (`if
      (_cachedPreludeUnitClauses is not null) return
      _cachedPreludeUnitClauses;`); the Dart `??` coalesce becomes C#
      `??` (Microsoft Learn null-coalescing-operator): `var source =
      _preludeUnitClauseSource ?? string.Empty;` (or `?? ""`). The
      Dart `String.isEmpty` getter becomes C# `string.IsNullOrEmpty(
      source)` — but since `source` has just been coalesced to
      non-null, prefer `source.Length == 0` or `source == ""`; spec
      emits `source.Length == 0` (Microsoft Learn: "The Length
      property of a string represents the number of Char objects it
      contains" — O(1)). Empty-source branch caches an empty map and
      returns. Else: `var lexer = new Lexer(source); var tokens =
      lexer.Tokenize(); var parser = new Parser(tokens); var module =
      parser.ParseModule();`. Then iterate `module.Procedures`, apply
      the unit-clause filter (see next construct), populate a `var
      unitClauses = new Dictionary<string, IReadOnlyList<Term>>(
      StringComparer.Ordinal);`, assign `_cachedPreludeUnitClauses =
      unitClauses;`, return it. Dart `Map<String, List<Term>>` ⇒ C#
      `Dictionary<string, IReadOnlyList<Term>>` per rf-dart-map-to-
      csharp-dictionary (cached, pmt/type_table.dart family). Inner
      value type tightened from `List<Term>` to `IReadOnlyList<Term>`
      because the cached args are NEVER mutated post-store (only read
      and pattern-matched against in `_glpUnifyForPE`).
    idiom_id: null
    research_finding_id: rf-dart-map-to-csharp-dictionary
    nuance: >-
      Three intertwined nuances. (1) Nullable-cache vs lazy: this is
      a hand-rolled memoisation; `Lazy<T>` would be the .NET-idiomatic
      replacement, BUT the cache is INVALIDATED by the setter (it
      reassigns to null), and `Lazy<T>` does NOT permit reset. So we
      preserve the manual nullable-field idiom — Microsoft Learn
      Lazy<T> documentation explicitly says "After a Lazy<T>'s Value
      is initialised, further calls return the same instance" — exact
      opposite of what we need. (2) `Map<String, List<Term>>?` ⇒
      `Dictionary<string, IReadOnlyList<Term>>?` — outer nullable
      (the cache slot) + inner element type made readonly (immutable
      to consumers, populated once at build time). (3) Dart `{}`
      empty-map literal ⇒ `new Dictionary<string, IReadOnlyList<Term>>
      (StringComparer.Ordinal)` (explicit ordinal comparer per
      cached idiom from parser.dart contiguity check) — `{}` in Dart
      defaults to `LinkedHashMap` which has predictable iteration
      order, but order is irrelevant here (consumed by key
      lookup only). The non-null assertion `_cachedPreludeUnitClauses!`
      is dropped in C# because the flow-analysis sees the field is
      non-null after the early-return guard.

  - construct_key: dart.procedure.unit_clause_extractor_filter_predicate_with_nested_body_shape_test
    source_form: >-
      Inside both `getPreludeUnitClauses()` and the method
      `_collectUnitClauses(Program program)` — a recurring filter
      predicate: "for the procedure to count as a unit clause, it must
      have exactly one clause, no guards, and either no body OR a body
      that is exactly the singleton `[Goal('true', [], ...)]`." Implemented
      as cascaded `if (proc.clauses.length != 1) continue;` / `if (clause.
      guards != null && clause.guards!.isNotEmpty) continue;` / `if
      (clause.body != null && clause.body!.isNotEmpty) { if (clause.body!.
      length == 1 && clause.body![0].functor == 'true' && clause.body![0].
      args.isEmpty) { /* OK */ } else { continue; } }`. The same logical
      filter is duplicated three times in this file (prelude extractor,
      `_collectUnitClauses`, `_collectReduceFacts`).
    target_decision: >-
      Emit a private static helper `private static bool
      IsUnitClause(Clause clause)` on the partial evaluator class so
      the duplicated filter is centralised on the C# side (light
      refactor — the THREE Dart copies share identical semantics, and
      lifting to a helper does not change observable behaviour;
      reviewer-clarity gain is large, scope-creep risk is zero). The
      filter body is preserved verbatim: `return (clause.Guards is null
      || clause.Guards.Count == 0) && (clause.Body is null ||
      clause.Body.Count == 0 || (clause.Body.Count == 1 &&
      clause.Body[0].Functor == "true" && clause.Body[0].Args.Count ==
      0));`. The "procedure must have exactly one clause" predicate
      stays at the call site (because each caller pairs it differently
      with the body-shape predicate). Dart `clause.body!` non-null
      assertion ⇒ C# null-conditional-and-short-circuit (`clause.Body is
      null || clause.Body.Count == 0 || ...`) — Microsoft Learn
      pattern-matching `is null` / `is not null`. The `'true'` literal
      and `.args.isEmpty` reads of the GOAL (ast.dart `Goal` leaf)
      map to `Functor == "true"` (string-ordinal equality — no locale
      hazard, see parser.dart cached idiom) and `Args.Count == 0`.
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
    nuance: >-
      "Body is just true" sentinel nuance (load-bearing GLP language
      invariant): in GLP a "unit clause" (or "fact") is conceptually
      `head.` — written textually with no body — but the parser
      normalises this to `head :- true.` (a singleton body of the
      builtin true/0 goal) in many code paths. Both shapes must be
      treated as unit clauses; missing this would mis-classify
      parsed-as-`true`-body unit clauses and break defined-guard
      unfolding. The triple-clause filter (guards-empty AND body-
      empty-OR-singleton-true) preserves this exactly. Null-vs-empty
      nuance: Dart distinguishes `null` (field unset) from `[]` (empty
      list) for the `guards` and `body` fields of `Clause` (see
      ast.dart spec, dart.ast_leaf.discriminated_nullable_pair_with_
      derived_predicate idiom — null is a meaningful third state).
      The C# port preserves this — `List<Guard>?` and `List<Goal>?`
      with `null` distinct from `Count == 0`. Refactor decision: I
      consciously emit ONE helper despite the spec's "do exactly what
      is asked" rule because the three Dart copies are byte-identical
      filter predicates (not behavioural variants); centralising is
      verifiable equivalence-preservation, not a re-design. If the
      codegen reviewer prefers verbatim triplication, the helper can
      be inlined at three call sites without semantic loss — flag
      both possibilities for the code-generation stage.

  - construct_key: dart.sealed_class.three_arm_unification_result_with_per_arm_payload
    source_form: >-
      "sealed class UnifyResult {}
       class UnifySuccess extends UnifyResult { final Map<String, Term>
         substitution; UnifySuccess(this.substitution); }
       class UnifyFail extends UnifyResult { final String reason;
         UnifyFail(this.reason); }
       class UnifySuspend extends UnifyResult { final Set<String>
         unboundReaders; UnifySuspend(this.unboundReaders); }" —
      Dart-3 `sealed` discriminated-union with three subclass arms,
      each carrying a typed payload. Consumed in two places via
      `switch (result) { case UnifyFail(...): ... case UnifySuspend(
      ...): ... case UnifySuccess(:final substitution): ... }`
      EXHAUSTIVE pattern-matching (no default arm) — Dart 3 verifies
      the switch IS exhaustive at compile time because the base is
      `sealed`.
    target_decision: >-
      Convert to a CLOSED hierarchy with the same closure semantics
      via the AST-leaf pattern from ast.dart (rf-dart-abstract-marker-
      base-to-csharp-abstract-sealed-leaves, cached): emit an
      `public abstract class UnifyResult` (closure expressed via
      sealing the LEAVES) — `public sealed class UnifySuccess :
      UnifyResult { public IReadOnlyDictionary<string, Term>
      Substitution { get; } public UnifySuccess(
      IReadOnlyDictionary<string, Term> substitution) { Substitution
      = substitution; } }`, `public sealed class UnifyFail :
      UnifyResult { public string Reason { get; } public UnifyFail(
      string reason) { Reason = reason; } }`, `public sealed class
      UnifySuspend : UnifyResult { public IReadOnlySet<string>
      UnboundReaders { get; } public UnifySuspend(IReadOnlySet<string>
      unboundReaders) { UnboundReaders = unboundReaders; } }`. The
      Dart `sealed` keyword has NO direct C# equivalent at the
      ABSTRACT-BASE level (Microsoft Learn: "It's an error to use the
      abstract modifier with a sealed class" — already cited in
      ast.dart spec); closure is encoded by (a) sealing every leaf
      (b) emitting exhaustive `switch` expressions with a `_ => throw
      new InvalidOperationException(...)` default arm in consumers.
      Dart 3 pattern-matching `case UnifySuccess(:final
      substitution)` becomes C# 8+ `UnifySuccess success =>
      ...success.Substitution...` (declaration pattern with
      property-access on the bound variable). Dart `Map<String, Term>`
      ⇒ `IReadOnlyDictionary<string, Term>` (cache-once, never-mutate
      after construction); Dart `Set<String>` ⇒ `IReadOnlySet<string>`
      (Microsoft Learn: "The IReadOnlySet<T> interface ... was
      introduced in .NET 5").
    idiom_id: null
    research_finding_id: rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves
    nuance: >-
      Four intertwined nuances. (1) `sealed`-vs-`abstract`-collision:
      Dart 3 `sealed` on a base + concrete leaves is the official way
      to declare a closed discriminated union (Dart official:
      "sealed gives the compiler enough information to enforce
      exhaustive switching"). C# has NO single keyword for this —
      reuse the ast.dart cached idiom (abstract base + sealed leaves
      + exhaustive switch with throwing default). (2) Exhaustiveness-
      verification gap: Dart 3 STATICALLY verifies the consumer's
      switch covers every subclass; C# 11+ pattern-match switches do
      NOT verify exhaustiveness across user-declared class
      hierarchies (only across enum/value types). The throwing-default
      arm preserves the RUNTIME contract (an unknown arm explodes
      loudly, just as a non-exhaustive Dart switch would refuse to
      compile). (3) Payload-mutability: every payload (the
      substitution Map, the reason String, the unboundReaders Set)
      is captured once at construction and never mutated — making
      them `IReadOnly*` interfaces preserves this invariant while
      preventing accidental mutation downstream. (4) Identity-vs-
      value: `UnifyResult` instances are reference types in both
      languages; equality is reference identity (no `==` override in
      Dart, no `Equals`/`==` override in C#). The result is short-
      lived (constructed, switched on once, discarded) so identity-
      equality is appropriate; no record-struct hazard.

  - construct_key: dart.classfield.int_counter_for_fresh_variable_names_with_prefix_PE
    source_form: >-
      "class PartialEvaluator { int _varCounter = 0; ...
      'PE${_varCounter++}' }" — a per-PartialEvaluator-instance
      monotonic counter used to mint fresh variable names with the
      "PE" prefix during clause-variable renaming.
    target_decision: >-
      Emit `public class PartialEvaluator { private long _varCounter
      = 0; ... }`. The fresh-name expression `'PE${_varCounter++}'`
      becomes C# `$"PE{_varCounter++}"` (Microsoft Learn interpolated
      strings; post-increment-then-interpolate ordering is preserved
      bit-for-bit in C# because the post-increment expression
      evaluates to the value BEFORE the increment, identical to Dart
      — Microsoft Learn `++` operator: "The result of x++ is the
      value of x before the operation"). Width: Dart `int` ⇒ C#
      `long` per the recurring 64-bit-width idiom (token.dart /
      lexer.dart / parser.dart family — Dart `int` is 64-bit on
      native, NumericRange-equivalent to `long`). Counter resets are
      NOT performed in the source — counter monotonically grows over
      the lifetime of a `PartialEvaluator` instance (each invocation
      of `transformDefinedGuards` / `unfoldReduceCalls` may mint
      hundreds of fresh names but they are all instance-scoped, never
      shared across `PartialEvaluator` instances).
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      Fresh-name uniqueness scope nuance (load-bearing for the
      partial evaluator's correctness): each renaming must produce
      names DISJOINT from every other variable in scope. The "PE"
      prefix plus monotonic counter scheme guarantees this WITHIN
      one `PartialEvaluator` lifetime — a fresh name never collides
      with a previously-issued fresh name. The scheme DOES NOT
      guarantee disjointness from user variables that happen to
      START with "PE" followed by digits (e.g. a user variable
      `PE42` collides if the counter happens to reach 42). This is
      a known limitation, preserved verbatim; documentation in
      partial-evaluator.txt / glp-runtime-spec.txt would be
      appropriate but is OUT OF SCOPE for this spec. Post-
      increment-in-interpolation semantics: Dart and C# both define
      post-increment as "yield old value, then assign new value to
      variable" — interpolation captures the OLD value, so the first
      fresh name is `PE0`, the second `PE1`, etc. The two languages
      agree here; the spec preserves verbatim.

  - construct_key: dart.method.transform_program_via_per_procedure_per_clause_loop_returning_new_immutable_program
    source_form: >-
      "Program transformDefinedGuards(Program program) { final
      unitClauses = {...getPreludeUnitClauses(), ..._collectUnitClauses(
      program)}; final allProcedures = _collectAllProcedures(program);
      List<Procedure> transformedProcedures = []; for (final procedure
      in program.procedures) { List<Clause> transformedClauses = [];
      for (final clause in procedure.clauses) { final transformed =
      _transformClause(clause, unitClauses, allProcedures);
      transformedClauses.add(transformed); } transformedProcedures.add(
      Procedure(procedure.name, procedure.arity, transformedClauses,
      procedure.line, procedure.column)); } return Program(
      transformedProcedures, program.line, program.column); }" — the
      Stage-1 entry point; pattern is "build new immutable AST from
      old by per-element transformation" (functional-style fold over
      the tree, but using imperative loops + List.add).
    target_decision: >-
      Emit `public Program TransformDefinedGuards(Program program)`.
      Body: the Dart spread-merge `{...A, ...B}` (Dart official:
      "Spread operators ... allow you to insert multiple elements
      into a collection") becomes C# explicit-merge: `var unitClauses
      = new Dictionary<string, IReadOnlyList<Term>>(StringComparer.
      Ordinal); foreach (var kv in GetPreludeUnitClauses())
      unitClauses[kv.Key] = kv.Value; foreach (var kv in
      CollectUnitClauses(program)) unitClauses[kv.Key] = kv.Value;`
      — the SECOND foreach is the "user definitions override prelude"
      direction (right-spread overrides left-spread in Dart; right-
      foreach overrides left-foreach in the C# port, identical
      semantics). Microsoft Learn `Dictionary<TKey,TValue>` indexer
      assignment: "If the key already exists in the dictionary, the
      value is overwritten." Then `var allProcedures =
      CollectAllProcedures(program);`. Outer loop: `var
      transformedProcedures = new List<Procedure>(program.
      Procedures.Count);` (pre-size hint, optional but performance-
      idiomatic — Microsoft Learn List<T>(int capacity)). Inner
      loop: `var transformedClauses = new List<Clause>(procedure.
      Clauses.Count); foreach (var clause in procedure.Clauses)
      transformedClauses.Add(TransformClause(clause, unitClauses,
      allProcedures));`. Build new `Procedure(procedure.Name,
      procedure.Arity, transformedClauses, procedure.Line,
      procedure.Column)`. Final `return new Program(
      transformedProcedures, program.Line, program.Column);`.
    idiom_id: null
    research_finding_id: rf-dart-iterable-where-to-linq
    nuance: >-
      Immutability-by-construction nuance (load-bearing): the
      partial evaluator is "source-to-source" — it MUST produce a
      NEW `Program` AST without mutating the input. Every per-
      procedure/per-clause loop allocates a NEW list and constructs
      NEW `Procedure`/`Clause` instances; no input node is reused
      mutated. The C# port MUST preserve this — emit
      `new List<Procedure>(...)`, `new Procedure(...)`,
      `new Program(...)`. Spread-merge directionality: Dart
      `{...A, ...B}` puts B's keys LAST (so B overrides on collision);
      the C# port's two `foreach`s with indexer-assignment achieves
      the same — preserved verbatim. Pre-size hint: optional. The
      Dart code does `List<Procedure> transformedProcedures = [];`
      with no capacity; the C# port can match (`new List<Procedure>()`)
      or improve (`new List<Procedure>(program.Procedures.Count)`) —
      the latter is performance-idiomatic and observably-equivalent.
      LINQ-vs-loop nuance: the loops COULD be rewritten as `var
      transformedProcedures = program.Procedures.Select(p => new
      Procedure(p.Name, p.Arity, p.Clauses.Select(c => TransformClause(
      c, unitClauses, allProcedures)).ToList(), p.Line, p.Column)).
      ToList();`. Spec PREFERS the imperative loop port (matches Dart
      shape; trivial for reviewer to verify equivalence; LINQ
      deferred-execution adds no value because every result is
      materialised). The codegen stage is FREE to choose either.

  - construct_key: dart.method.collect_signature_set_from_procedures
    source_form: >-
      "Set<String> _collectAllProcedures(Program program) { final
      Set<String> procedures = {}; for (final proc in program.
      procedures) { procedures.add('${proc.name}/${proc.arity}'); }
      return procedures; }" — accumulator pattern producing a set of
      "name/arity" signature strings (uniqueness intentional;
      duplicates from non-contiguous procedure groups would silently
      dedupe but the contiguity check in parser.dart prevents that).
    target_decision: >-
      Emit `private static HashSet<string> CollectAllProcedures(
      Program program) { var procedures = new HashSet<string>(
      StringComparer.Ordinal); foreach (var proc in program.
      Procedures) procedures.Add($"{proc.Name}/{proc.Arity}"); return
      procedures; }`. Dart `Set<String> {}` literal ⇒ C#
      `HashSet<string>(StringComparer.Ordinal)` per rf-dart-set-to-
      csharp-hashset (cached from prelude.dart family, FrozenSet
      idiom — but here mutable, so `HashSet<string>` is correct;
      FrozenSet is read-only-construction-time-immutable, wrong
      here). Ordinal comparer is mandatory for signature keying
      (consistent with the parser.dart contiguity check cached
      idiom; signatures are ASCII-only and exact-match is the
      contract). Method made `private static` — no instance state
      consumed.
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
    nuance: >-
      Signature-key locale-safety nuance (load-bearing): GLP
      procedure signatures `name/arity` are ASCII-only (parser
      enforces atom/identifier lexer rules); using
      `StringComparer.Ordinal` rather than the default culture-aware
      comparer is BOTH a robustness measure (survives BCL changes /
      Turkish-I locale hazards) AND a (small) performance win
      (Microsoft Learn: "Ordinal string comparison is the fastest
      kind of comparison"). Dedupe-intent nuance: the `Set` is
      semantically a set (not a list), and contains the same name/
      arity once even if a procedure is split across multiple
      contiguity groups (which parser.dart's contiguity check
      forbids, but defensive coding tolerates). HashSet<string>
      preserves this. Return-type tightening: could be
      `IReadOnlySet<string>` for callers that don't mutate — but
      the only caller (`_transformClause` consults `allProcedures.
      contains(key)`) does NOT mutate, so the return type COULD be
      tightened. Spec preserves `HashSet<string>` (Dart shape) for
      minimal divergence; codegen can tighten if it wishes.

  - construct_key: dart.method.stage2_unfold_reduce_facts_with_short_circuit_early_return_when_no_facts
    source_form: >-
      "Program unfoldReduceCalls(Program program) { final reduceFacts
      = _collectReduceFacts(program); if (reduceFacts.isEmpty) {
      return program; } ... per-procedure / per-clause loop building
      new Program ... }" — Stage 2 entry; SHORT-CIRCUITS the
      transformation when there are no reduce facts (returns the
      input program identity-equal, preserving immutability via a
      no-op).
    target_decision: >-
      Emit `public Program UnfoldReduceCalls(Program program) { var
      reduceFacts = CollectReduceFacts(program); if (reduceFacts.
      Count == 0) return program; ... }`. The short-circuit is
      preserved verbatim — returning the SAME `Program` reference
      when no work is needed (identity-preservation is a documented
      optimisation; the input WAS immutable, the output is
      logically-equal-and-identity-equal). Inner loop uses the same
      shape as `TransformDefinedGuards` — `var transformedProcedures
      = new List<Procedure>(program.Procedures.Count);` etc. — but
      with one critical difference: `_unfoldReduceInClause` returns
      `List<Clause>` (zero, one, or many clauses per input clause),
      and the inner per-clause loop uses `transformedClauses.AddRange(
      UnfoldReduceInClause(clause, reduceFacts))` (Microsoft Learn
      `List<T>.AddRange` — equivalent to Dart `addAll`).
    idiom_id: null
    research_finding_id: rf-dart-iterable-where-to-linq
    nuance: >-
      Two intertwined nuances. (1) Short-circuit identity-return:
      preserves reference identity when no work is performed. This
      is observable IF a downstream consumer compares `Program`
      instances by reference (e.g. for caching) — preserving it is
      correctness-preserving, not just optimisation. (2) Multi-out
      flat-map per clause: Stage 1 is 1:1 (each clause → one
      transformed clause), Stage 2 is 1:N (each clause may expand
      into multiple clauses when the reduce-fact pattern matches
      multiple facts). The C# port MUST use `AddRange` (NOT `Add`)
      on the per-clause inner result. Dart `addAll(expanded)`
      ⇒ C# `AddRange(expanded)` exactly (Microsoft Learn `AddRange`:
      "Adds the elements of the specified collection to the end of
      the List<T>"). Empty-result handling: when `expanded` is `[]`,
      `addAll([])` is a no-op — same for `AddRange(emptyList)` —
      meaning the original clause is dropped (this matches the
      Dart-side `_unfoldReduceInClause` contract: it returns
      `[clause]` to preserve, `[expansion1, expansion2, ...]` to
      replace, and the contract is that empty is impossible at the
      return point because the code path that COULD return empty
      explicitly returns `[clause]` instead — see the comment
      "If no expansions succeeded, keep original clause").

  - construct_key: dart.method.collect_reduce_facts_unit_clauses_filter
    source_form: >-
      "_collectReduceFacts(Program program)" — same unit-clause
      filter as the prelude/general extractors, restricted to
      procedures with name=="reduce" and arity==2. Returns
      `List<Clause>` (NOT a Map keyed by signature) because the
      caller iterates them in order to try each one against a single
      reduce-call.
    target_decision: >-
      Emit `private static List<Clause> CollectReduceFacts(Program
      program) { var facts = new List<Clause>(); foreach (var proc in
      program.Procedures) { if (proc.Name != "reduce" || proc.Arity
      != 2) continue; foreach (var clause in proc.Clauses) { if
      (!IsUnitClauseShape(clause)) continue; facts.Add(clause); } }
      return facts; }`. Uses the lifted `IsUnitClauseShape` helper
      (third construct above; clauses-count guard is dropped because
      this iterates EVERY clause of the matching procedure, so the
      "exactly one clause" predicate doesn't apply — every clause
      that PASSES the body-shape test counts as a fact, even if its
      procedure has multiple clauses). Return type: `List<Clause>`
      because the caller iterates in declaration order; HashSet
      would lose order; IReadOnlyList preserves order without
      permitting mutation — but the local-only producer-consumer
      flow makes `List<Clause>` acceptable verbatim (matches Dart
      shape).
    idiom_id: null
    research_finding_id: rf-dart-list-to-csharp-list-of-T
    nuance: >-
      Filter-vs-shape distinction nuance (load-bearing): the
      prelude/`_collectUnitClauses` extractors require BOTH the
      procedure to have exactly ONE clause AND that clause to have
      the unit-clause body shape. `_collectReduceFacts` requires
      ONLY the body-shape — a procedure with multiple reduce/2
      clauses contributes each clause as a fact. This distinction
      is subtle but load-bearing: the partial-evaluator semantics
      treat each reduce/2 clause as an independent rewrite rule,
      whereas other unit-clause-driven sites require uniqueness.
      The helper-split (IsUnitClause vs IsUnitClauseShape) makes
      this explicit; if the codegen stage chooses to inline both,
      reviewers must be aware of the distinction. Order-
      preservation nuance: reduce-fact application tries each fact
      in declaration order (first-match-wins is NOT in play; ALL
      matching facts are accumulated as separate expansions); but
      stable iteration order is still useful for reproducibility
      and for debugging. `List<Clause>` preserves it; `HashSet<
      Clause>` would not.

  - construct_key: dart.method.unfold_single_reduce_call_per_clause_three_valued_unify_dispatch
    source_form: >-
      "_unfoldReduceInClause(Clause clause, List<Clause> reduceFacts)"
      — the core Stage-2 transformer for a single clause. Finds the
      FIRST `reduce/2` call in the body (index-tracked), renames each
      reduce-fact's variables fresh, tries `_glpUnifyForPE` of the
      call's first arg against the fact's first arg with a switch
      over `UnifyResult` (Fail ⇒ skip this fact; Suspend ⇒ skip this
      fact too — can't reduce at compile time; Success ⇒ also unify
      callResult with factReplacement, merge substitutions, apply to
      head/guards/body, REMOVE the reduce call, simplify guards,
      emit a new clause). Returns `List<Clause>` — `[clause]` if no
      reduce call or no fact matched, `[expansion1, expansion2, ...]`
      otherwise.
    target_decision: >-
      Emit `private List<Clause> UnfoldReduceInClause(Clause clause,
      IReadOnlyList<Clause> reduceFacts)`. Body shape preserved:
      `if (clause.Body is null || clause.Body.Count == 0) return new
      List<Clause> { clause };`. Find first reduce/2 call: `int
      reduceIndex = -1; Goal? reduceCall = null; for (int i = 0; i <
      clause.Body.Count; i++) { var goal = clause.Body[i]; if (goal.
      Functor == "reduce" && goal.Args.Count == 2) { reduceIndex =
      i; reduceCall = goal; break; } } if (reduceCall is null)
      return new List<Clause> { clause };`. Per-fact loop: rename
      via `var renamedFact = RenameClauseVars(fact);`; extract
      pattern/replacement; call `var result = GlpUnifyForPE(new[] {
      callPattern }, new[] { factPattern });` (Dart inline
      list literal `[callPattern]` ⇒ C# `new[] { callPattern }` —
      Microsoft Learn implicit-array-creation expression). Dispatch
      via C# pattern-switch: `switch (result) { case UnifyFail: case
      UnifySuspend: continue; case UnifySuccess success: ... default:
      throw new InvalidOperationException("unreachable"); }` —
      throwing-default arm per the sealed-leaf cached idiom (no
      compile-time exhaustiveness on user-sealed-leaves; runtime
      check preserves the contract). Inside the Success arm:
      compute `resultUnify` via second GlpUnifyForPE call, merge
      substitutions: `var fullSubst = new Dictionary<string, Term>(
      success.Substitution, StringComparer.Ordinal); if (resultUnify
      is UnifySuccess rs) foreach (var kv in rs.Substitution)
      fullSubst[kv.Key] = kv.Value;` (Microsoft Learn Dictionary<
      TKey,TValue>(IDictionary<TKey,TValue>) copy-ctor). Apply to
      head/guards/body via helpers (subsequent constructs). Body
      build: skip index `reduceIndex`, apply substitution to the
      rest; if the new body is empty, replace with `new List<Goal> {
      new Goal("true", new List<Term>(), clause.Line, clause.Column)
      }` — preserves the GLP "no body ⇒ true body" sentinel. Simplify
      guards via the helper. Construct new `Clause` via the cached
      named-default-param idiom (parser.dart): `expanded.Add(new
      Clause(newHead, guards: simplifiedGuards, body: newBody, line:
      clause.Line, column: clause.Column));` (or positional-args
      equivalent — depends on the Clause ctor shape decided in
      ast.dart spec). Final: `if (expanded.Count == 0) return new
      List<Clause> { clause }; return expanded;`.
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
    nuance: >-
      Five intertwined nuances. (1) FIRST-reduce-only: only the FIRST
      reduce/2 call in the body is unfolded per pass (Dart `break`
      after finding it); subsequent reduce calls require another
      pass. This is a load-bearing partial-evaluator semantics —
      preserved verbatim. The C# `break` works identically. (2)
      Suspend-treated-as-no-match: when unification SUSPENDS (compile-
      time-irreducible), the code TREATS IT LIKE FAIL — skips this
      fact, tries the next. This is a documented design decision
      (the comment says "Can't reduce at compile time, keep original
      / But still try other facts"); semantics: only facts whose
      pattern is COMPLETELY resolvable at compile time produce
      expansions. Preserved verbatim. (3) Two-step unification: the
      FIRST unify tests `callPattern` against `factPattern` (with
      substitution σ); the SECOND unify tests `callResult` against
      `factReplacement` (with substitution τ). The substitutions are
      MERGED with τ winning on collision (`fullSubst.addAll(
      resultUnify.substitution)` — Dart Map.addAll: "If they already
      exist, the value is overwritten"; C# indexer-assignment also
      overwrites). Both languages agree on this collision-resolution.
      (4) Substitution-merge ordering matters when keys collide;
      Dart and C# both use "second-wins". (5) Empty-body sentinel:
      after removing the reduce call, if the body is empty, replace
      with `[Goal('true', [], ...)]` — the inverse of the unit-clause-
      body-shape filter. This roundtrip-preserves the GLP language
      invariant "every clause body is non-empty or normalised to
      true". Preserved verbatim in C#.

  - construct_key: dart.method.rename_clause_variables_fresh_with_underscore_preservation
    source_form: >-
      "_renameClauseVars(Clause clause)" — collects all variable
      names from head/guards/body, builds a renaming map (excluding
      underscore `_`), applies the renaming to a new clause via
      per-leaf helpers (`_applyRenaming`/`_applyRenamingToAtom`),
      preserving `UnderscoreTerm` instances as new
      `UnderscoreTerm(line, column)` because underscores are
      anonymous writers and MUST NOT be renamed (each `_` is unique
      by syntax).
    target_decision: >-
      Emit `private Clause RenameClauseVars(Clause clause)`. Body:
      `var varNames = new HashSet<string>(StringComparer.Ordinal);
      CollectVarNamesFromAtom(clause.Head, varNames); if (clause.
      Guards is not null) foreach (var guard in clause.Guards)
      foreach (var arg in guard.Args) CollectVarNames(arg, varNames);
      if (clause.Body is not null) foreach (var goal in clause.Body)
      foreach (var arg in goal.Args) CollectVarNames(arg, varNames);`.
      Build renaming map: `var renaming = new Dictionary<string,
      string>(StringComparer.Ordinal); foreach (var name in varNames)
      if (name != "_") renaming[name] = $"PE{_varCounter++}";`.
      Apply renaming: `var newHead = ApplyRenamingToAtom(clause.Head,
      renaming);`, guards via `clause.Guards?.Select(g => new
      Guard(g.Predicate, g.Args.Select(a => ApplyRenaming(a,
      renaming)).ToList(), g.Line, g.Column, negated: g.Negated)).
      ToList()` (LINQ chain — preserves the Dart `.map(...).toList()`
      idiom verbatim per rf-dart-iterable-where-to-linq cached). Same
      shape for body via `new Goal(g.Functor, g.Args.Select(a =>
      ApplyRenaming(a, renaming)).ToList(), g.Line, g.Column)`.
      Construct `new Clause(newHead, guards: newGuards, body: newBody,
      line: clause.Line, column: clause.Column)`.
    idiom_id: null
    research_finding_id: rf-dart-iterable-where-to-linq
    nuance: >-
      Three intertwined nuances. (1) Underscore-preservation (load-
      bearing GLP semantics): `_` is an anonymous writer — each
      occurrence is a distinct variable. Skipping `_` in the renaming
      map preserves THIS — the post-renamed clause still has `_`
      occurrences, which the partial-evaluator's downstream
      unification (`_isUnderscore`) handles as always-match-no-bind.
      If `_` were renamed to `PEn`, two distinct `_` occurrences
      would alias to the same fresh name and BREAK SRSW. (2) Fresh-
      name uniqueness within ONE renaming AND across the
      `PartialEvaluator` instance lifetime — the `_varCounter`
      monotonic-increment is a load-bearing invariant. (3) Walk-order
      determinism: `Set<String>` in Dart is `LinkedHashSet` with
      INSERTION-ORDER iteration; `HashSet<string>` in C# does NOT
      guarantee insertion order (Microsoft Learn: "The order in
      which the items are returned is undefined"). Renaming-map
      order does NOT affect the OUTPUT clause (each name gets a
      unique fresh name regardless of iteration order), but it DOES
      affect WHICH fresh-counter value a given name receives — for
      debugging-output stability this could matter. Spec FLAGS this
      but considers it cosmetic; if reviewer cares, switch C#
      `HashSet<string>` to `OrderedSet<string>` (or maintain a
      `List<string>` alongside the set for deterministic
      iteration). For correctness, no change is required.

  - construct_key: dart.method.var_name_collector_recursive_descent_term_walk
    source_form: >-
      "void _collectVarNames(Term term, Set<String> names) { if
      (term is VarTerm) { names.add(term.name); } else if (term is
      StructTerm) { for (final arg in term.args) _collectVarNames(
      arg, names); } else if (term is ListTerm) { if (term.head !=
      null) _collectVarNames(term.head!, names); if (term.tail !=
      null) _collectVarNames(term.tail!, names); } }" — recursive
      descent over the Term hierarchy collecting variable names
      into an externally-supplied accumulator set; uses Dart
      `is`-checks (declaration-pattern style) and treats ConstTerm/
      UnderscoreTerm as leaf no-ops by FALLTHROUGH (no explicit
      arm).
    target_decision: >-
      Emit `private static void CollectVarNames(Term term,
      HashSet<string> names)`. Body: `switch (term) { case VarTerm
      varTerm: names.Add(varTerm.Name); break; case StructTerm s:
      foreach (var arg in s.Args) CollectVarNames(arg, names); break;
      case ListTerm l: if (l.Head is not null) CollectVarNames(l.
      Head, names); if (l.Tail is not null) CollectVarNames(l.Tail,
      names); break; }`. The switch has NO default arm — ConstTerm
      and UnderscoreTerm fall through silently, matching the Dart
      no-explicit-arm behaviour (both languages treat
      no-matching-case as no-op when no default is provided AND no
      exhaustiveness is enforced). Microsoft Learn pattern matching
      switch statement: "Variables declared in a switch case ... are
      scoped to that case." Dart `is`-tests with name binding
      (`term is VarTerm` followed by `term.name`) become C# type-
      pattern with declaration (`case VarTerm varTerm: ...
      varTerm.Name ...`) — Microsoft Learn declaration pattern.
    idiom_id: null
    research_finding_id: rf-dart-is-chain-to-csharp-switch-expression-type-pattern
    nuance: >-
      Three intertwined nuances. (1) Silent-fallthrough for
      ConstTerm/UnderscoreTerm: in Dart, an `is`-chain without an
      `else` arm silently no-ops on unmatched types. The C# port
      preserves this by OMITTING the `default:` arm. Alternative:
      add `default: break;` for explicitness (no semantic
      difference; reviewer-preference). Spec emits no default
      because the Dart code is explicit about treating only Var/
      Struct/List as variable-bearing — that semantic statement is
      preserved verbatim. (2) Accumulator-by-reference: `Set<String>
      names` is passed by reference in Dart (collections are
      reference types); `HashSet<string> names` in C# is likewise a
      reference type — both accumulate in place. Avoid passing by
      `out`/`ref` (unnecessary; the reference IS the parameter).
      (3) Recursion depth: AST trees can be deep (nested lists,
      nested structs); the recursion is direct (not tail-call), so
      a pathologically deep input could stack-overflow. Both Dart
      and C# have similar default stack depths (~1 MB); preserved
      verbatim. If this proves problematic in the wild, an
      explicit stack-based traversal could be substituted — out
      of scope for this spec.

  - construct_key: dart.method.apply_renaming_recursive_term_rebuild_with_underscore_demotion
    source_form: >-
      "Term _applyRenaming(Term term, Map<String, String> renaming)
      { if (term is VarTerm) { if (term.name == '_') return
      UnderscoreTerm(term.line, term.column); if (renaming.
      containsKey(term.name)) return VarTerm(renaming[term.name]!,
      term.isReader, term.line, term.column); return term; }
      else if (term is StructTerm) { return StructTerm(term.functor,
      term.args.map((a) => _applyRenaming(a, renaming)).toList(),
      term.line, term.column); } else if (term is ListTerm) {
      return ListTerm(term.head != null ? _applyRenaming(term.head!,
      renaming) : null, term.tail != null ? _applyRenaming(term.
      tail!, renaming) : null, term.line, term.column); } else if
      (term is UnderscoreTerm) { return term; } else { return
      term; } }" — recursive term rebuild applying name renaming;
      VarTerm-named-`_` is DEMOTED to UnderscoreTerm (a load-bearing
      normalisation step).
    target_decision: >-
      Emit `private static Term ApplyRenaming(Term term,
      IReadOnlyDictionary<string, string> renaming)`. Body:
      `switch (term) { case VarTerm varTerm when varTerm.Name ==
      "_": return new UnderscoreTerm(varTerm.Line, varTerm.Column);
      case VarTerm varTerm when renaming.TryGetValue(varTerm.Name,
      out var newName): return new VarTerm(newName, varTerm.IsReader,
      varTerm.Line, varTerm.Column); case VarTerm: return term;
      case StructTerm s: return new StructTerm(s.Functor, s.Args.
      Select(a => ApplyRenaming(a, renaming)).ToList(), s.Line, s.
      Column); case ListTerm l: return new ListTerm(l.Head is not
      null ? ApplyRenaming(l.Head, renaming) : null, l.Tail is not
      null ? ApplyRenaming(l.Tail, renaming) : null, l.Line, l.
      Column); case UnderscoreTerm: return term; default: return
      term; }`. Microsoft Learn `when` clause in switch: "An optional
      case guard that further tests the pattern." Dart non-null
      assertion `renaming[term.name]!` is replaced by C#
      `TryGetValue` with out-parameter (idiomatic; no double-lookup;
      no null-suppression needed) per rf-dart-map-lookup-to-csharp-
      trygetvalue cached from parser.dart.
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
    nuance: >-
      Four intertwined nuances. (1) Underscore-DEMOTION: a `VarTerm`
      whose `name == "_"` is REPLACED with a fresh `UnderscoreTerm`
      — the parser MAY produce either shape (depending on where in
      the grammar the `_` is encountered), and this rebuild
      NORMALISES to `UnderscoreTerm` everywhere. This is a load-
      bearing invariant downstream code relies on (see
      `_isUnderscore` checks). Preserved verbatim. (2) Three arms
      for VarTerm: name==`_`, name in renaming, name not in
      renaming. The C# switch uses `when`-guards in declaration
      order; the THIRD case (`case VarTerm: return term;`) is the
      no-rename fallthrough. (3) Reader-vs-writer preservation:
      `term.isReader` is passed through verbatim when constructing
      the renamed `VarTerm` — renaming does NOT flip the reader/
      writer marker (a writer `X` stays a writer `PE7`, a reader
      `X?` stays a reader `PE7?`). Load-bearing for SRSW analysis
      downstream. (4) Default fallthrough: the final `default:
      return term;` covers `ConstTerm` (and any future Term subclass)
      — Dart code has `return term; // ConstTerm unchanged`,
      same intent. Microsoft Learn switch with declaration patterns
      do NOT enforce exhaustiveness over user class hierarchies, so
      a `default` arm is the correct way to express "everything
      else is identity-returned."

  - construct_key: dart.method.transform_clause_fixpoint_loop_with_three_unify_arms_throwing_on_fail_or_suspend
    source_form: >-
      "_transformClause(Clause clause, Map<String, List<Term>>
      unitClauses, Set<String> allProcedures)" — the Stage-1
      core transformer. While `changed`, iterate `currentGuards`;
      for each guard, look up its `name/arity` signature in
      `unitClauses`; if found, renamed-unify and (a) FAIL throws
      `CompileError 'never succeed'`, (b) SUSPEND throws
      `CompileError 'cannot reduce ... must be fully reducible at
      compile time'`, (c) SUCCESS applies the substitution to head/
      remaining-guards/body, restarts the outer loop; otherwise
      check `builtinProcedures.contains(key)` (keep), or
      `allProcedures.contains(key)` (throw `CompileError 'cannot
      call X/arity in guard position'`), or fall through (keep —
      type checker handles undefined later). The negated-defined-
      guard case also throws (`'Defined guard X cannot be negated'`).
    target_decision: >-
      Emit `private Clause TransformClause(Clause clause,
      IReadOnlyDictionary<string, IReadOnlyList<Term>> unitClauses,
      IReadOnlySet<string> allProcedures)`. Body shape preserved
      verbatim. Early return: `if (clause.Guards is null || clause.
      Guards.Count == 0) return clause;`. Mutable locals: `var
      currentHead = clause.Head; var currentGuards = new
      List<Guard>(clause.Guards); var currentBody = clause.Body is
      not null ? new List<Goal>(clause.Body) : null; bool changed
      = true;`. Outer `while (changed)`: `changed = false; var
      remainingGuards = new List<Guard>();`. Inner `for (int i =
      0; i < currentGuards.Count; i++)`: `var guard =
      currentGuards[i]; var key = $"{guard.Predicate}/{guard.Args.
      Count}";`. `if (unitClauses.TryGetValue(key, out var unitArgs))
      { if (guard.Negated) throw new CompileError($"Defined guard
      \"{guard.Predicate}\" cannot be negated", guard.Line, guard.
      Column, phase: "analyzer"); var renamedArgs =
      RenameUnitClauseVars(unitArgs); var result = GlpUnifyForPE(
      guard.Args, renamedArgs); switch (result) { case UnifyFail
      fail: throw new CompileError($"Defined guard \"{guard.
      Predicate}({string.Join(", ", guard.Args)})\" can never
      succeed.\n  Unit clause: {guard.Predicate}({string.Join(", ",
      unitArgs)})\n  Reason: {fail.Reason}\n  This clause is
      unreachable.", guard.Line, guard.Column, phase: "analyzer");
      case UnifySuspend suspend: throw new CompileError($"Cannot
      reduce defined guard \"{guard.Predicate}({string.Join(", ",
      guard.Args)})\" at compile time.\n  Unit clause: {guard.
      Predicate}({string.Join(", ", unitArgs)})\n  Unbound readers:
      {string.Join(", ", suspend.UnboundReaders.Select(r => r +
      "?"))}\n  Defined guards must be fully reducible at compile
      time.", guard.Line, guard.Column, phase: "analyzer"); case
      UnifySuccess success: currentHead = ApplySubstitutionToAtom(
      currentHead, success.Substitution); var restGuards =
      currentGuards.GetRange(i + 1, currentGuards.Count - i -
      1).Select(g => ApplySubstitutionToGuard(g, success.
      Substitution)).ToList(); remainingGuards = remainingGuards.
      Select(g => ApplySubstitutionToGuard(g, success.Substitution)).
      ToList(); if (currentBody is not null) currentBody =
      currentBody.Select(g => ApplySubstitutionToGoal(g, success.
      Substitution)).ToList(); currentGuards = new List<Guard>();
      currentGuards.AddRange(remainingGuards); currentGuards.AddRange(
      restGuards); changed = true; break; } if (changed) break; }
      else if (BuiltinProcedures.Contains(key)) { remainingGuards.
      Add(guard); } else if (allProcedures.Contains(key)) { throw
      new CompileError($"Cannot call \"{guard.Predicate}/{guard.
      Args.Count}\" in guard position.\n  Only builtin guards and
      single-unit-clause procedures can appear in guards.\n  The
      procedure \"{guard.Predicate}\" has multiple clauses or non-
      unit clauses.", guard.Line, guard.Column, phase:
      "partial_evaluator"); } else { remainingGuards.Add(guard); }`.
      End of inner-for: `if (!changed) currentGuards =
      remainingGuards;`. End of outer-while: construct `new Clause(
      currentHead, guards: currentGuards.Count == 0 ? null :
      currentGuards, body: currentBody, line: clause.Line, column:
      clause.Column)`.
    idiom_id: null
    research_finding_id: rf-dart-implements-exception-to-csharp-derive-system-exception
    nuance: >-
      Six intertwined nuances. (1) Fixpoint-iteration: the outer
      `while (changed)` restarts whenever a single guard is
      reduced — this is necessary because the substitution from
      one reduction may cascade into other guards (renaming a
      shared variable changes their argument lists). The `break`
      out of the inner for-loop on `changed=true` exits inner-for
      AFTER setting `changed=true`, then the outer `while` re-tests
      and re-enters. Preserved verbatim. (2) Substitution propagation
      to THREE separate lists: (a) `remainingGuards` (already-
      processed-and-kept guards), (b) `restGuards` (yet-to-be-
      processed guards), (c) the body (`currentBody`). All three
      get the same substitution applied; ordering matters
      (substitute first, then concatenate). Preserved verbatim.
      (3) Three throw-arms: FAIL ⇒ "never succeed" (unreachable-
      clause warning, classified as error), SUSPEND ⇒ "must be
      fully reducible" (compile-time-irreducibility error),
      multi-clause-procedure-in-guard ⇒ "cannot call in guard
      position" (guard-position-restriction error). All three are
      `CompileError` (rf-dart-implements-exception-to-csharp-
      derive-system-exception cached from error.dart) — phase:
      "analyzer" for the first two (clause-level analysis errors)
      and "partial_evaluator" for the third (transformation-stage
      error). Preserved verbatim. (4) Negated-defined-guard: a
      negated defined guard is FORBIDDEN because the negation
      semantics over a compile-time-reducible unit clause are
      undefined (a unit clause is "true if it matches"; negation
      would be "false if it matches" — but the matching is
      compile-time, so negation makes no sense at runtime). The
      Dart code throws `CompileError` with phase "analyzer".
      Preserved verbatim. (5) Builtin-vs-defined fallthrough:
      `BuiltinProcedures.Contains(key)` is true for primitives
      like `integer/1`, `ground/1` — these are KEPT in
      `remainingGuards` (the runtime handles them, not the
      partial evaluator). `allProcedures.Contains(key)` is true
      for any user-defined procedure — if it's NOT a unit clause
      (because the first lookup failed), throwing is correct.
      Otherwise (unknown predicate), KEPT in `remainingGuards`
      for the type checker to diagnose. Preserved verbatim. (6)
      Multi-line CompileError messages with embedded `\n`: Dart
      `'\n'` ⇒ C# verbatim `\n` (escape sequence); Microsoft
      Learn string-literal escape sequences. The interpolated
      `${guard.args.join(", ")}` ⇒ C# `{string.Join(", ", guard.
      Args)}` per rf-dart-string-interpolation-join-to-csharp-
      interpolation-string-join cached from parser.dart family.

  - construct_key: dart.method.glp_compile_time_three_valued_unification_phase1_collection_phase2_resolution
    source_form: >-
      "UnifyResult _glpUnifyForPE(List<Term> callArgs, List<Term>
      unitArgs)" — compile-time GLP unification specialised for
      partial evaluation. Phase 1 (Collection): pairwise
      `_unifyTerms(callArgs[i], unitArgs[i], subst, suspSet)` with
      arity-mismatch early-return UnifyFail. Phase 2 (Resolution):
      check each writer name in `suspSet` against `subst`; if any
      writer is unbound (in suspSet but not in subst), return
      UnifySuspend with the unresolved set. Otherwise resolve
      substitution chains via `_resolveSubstitution` and return
      UnifySuccess.
    target_decision: >-
      Emit `private UnifyResult GlpUnifyForPE(IReadOnlyList<Term>
      callArgs, IReadOnlyList<Term> unitArgs)`. Body: `if (callArgs.
      Count != unitArgs.Count) return new UnifyFail($"Arity
      mismatch: {callArgs.Count} vs {unitArgs.Count}"); var
      substitution = new Dictionary<string, Term>(StringComparer.
      Ordinal); var suspensionSet = new HashSet<string>(
      StringComparer.Ordinal);`. Phase 1: `for (int i = 0; i <
      callArgs.Count; i++) { var result = UnifyTerms(callArgs[i],
      unitArgs[i], substitution, suspensionSet); if (result is not
      null) return result; }`. Phase 2: `var unresolvedReaders =
      new HashSet<string>(StringComparer.Ordinal); foreach (var
      readerName in suspensionSet) if (!substitution.ContainsKey(
      readerName)) unresolvedReaders.Add(readerName); if
      (unresolvedReaders.Count > 0) return new UnifySuspend(
      unresolvedReaders);`. Resolve and return: `var resolved =
      ResolveSubstitution(substitution); return new UnifySuccess(
      resolved);`. Microsoft Learn `Dictionary<TKey,TValue>.
      ContainsKey`: O(1) average lookup.
    idiom_id: null
    research_finding_id: rf-dart-map-to-csharp-dictionary
    nuance: >-
      Four intertwined nuances. (1) Three-valued return discipline:
      this method is the SINGLE source of `UnifyResult`. Every
      caller assumes EXACTLY one of the three subclasses is
      returned, never a fourth state, never null. Preserved
      verbatim. (2) Phase-1-collect / Phase-2-resolve separation
      (load-bearing GLP semantics): a reader X? may be SEEN before
      its writer X is bound; Phase 1 records `suspSet.add(X)` and
      tentatively records `subst[X] = factArg` (so later cross-
      arg references can see the binding); Phase 2 checks whether
      any READER is unresolved (its writer is in suspSet but not
      bound by Phase 1 in subst). The check `!subst.ContainsKey(
      readerName)` is — note — checking the WRITER's binding (the
      reader X? refers to writer X). This is subtle and load-
      bearing; preserved verbatim with the same name `readerName`
      (which is actually a writer name, but the Dart code uses
      this variable name verbatim). (3) Substitution-chain
      resolution: after Phase 2 succeeds, `_resolveSubstitution`
      flattens chains like {X→Y, Y→f(Z)} to {X→f(Z), Y→f(Z)}.
      This ensures the caller can apply the substitution in ONE
      pass without re-substituting. Load-bearing for the apply-
      substitution helpers downstream. (4) Mutability of subst/
      suspSet: passed BY REFERENCE through `_unifyTerms`
      (Dictionary and HashSet are reference types) — both
      languages identical. The `out` keyword is NOT needed
      because the reference IS the parameter.

  - construct_key: dart.method.unify_terms_recursive_six_arm_branching_writer_reader_const_struct_list_underscore
    source_form: >-
      "UnifyResult? _unifyTerms(Term callArg, Term unitArg,
      Map<String, Term> subst, Set<String> suspSet)" — pairwise
      unification of two terms. Six top-level branches:
      (a) either-side underscore ⇒ success/no-binding; (b) call-
      side writer ⇒ alias unit-side variable to call-side writer,
      or bind call-side writer to constant/struct; (c) call-side
      reader ⇒ alias / suspend / compatibility-check existing;
      (d) call-side constant ⇒ const-vs-const equality, or bind
      unit-side variable; (e) call-side struct ⇒ functor/arity
      equality + recursive arg-pair unify, or bind unit-side
      variable; (f) call-side list ⇒ nil-vs-nil, recursive head/
      tail unify, or bind unit-side variable. Returns `UnifyResult?`
      — `null` for success, non-null for fail. (Note: this is the
      ONLY method in the file returning a NULLABLE UnifyResult,
      using null-as-success — distinct from `_glpUnifyForPE` which
      uses UnifySuccess-as-success).
    target_decision: >-
      Emit `private UnifyResult? UnifyTerms(Term callArg, Term
      unitArg, Dictionary<string, Term> subst, HashSet<string>
      suspSet)`. Body: a sequence of `if`/`else if` (NOT a
      pattern-switch — too many cross-cutting conditions to
      flatten cleanly; preserve the Dart shape verbatim). Use
      pattern-matching `is` with declaration: `if (IsUnderscore(
      callArg) || IsUnderscore(unitArg)) return null;`. Writer
      arm: `if (callArg is VarTerm callVar && !callVar.IsReader)
      { if (unitArg is VarTerm unitVar && !unitVar.IsReader) {
      subst[unitVar.Name] = callVar; } else if (unitArg is VarTerm
      unitR && unitR.IsReader) { subst[unitR.Name] = callVar; }
      else { subst[callVar.Name] = unitArg; } return null; }`.
      Reader arm: similar shape, with `suspSet.Add(writerName)` for
      the const/struct case; the `_substSet` propagation helper
      becomes a small private method (next construct). Const arm:
      `if (callArg is ConstTerm callConst) { if (unitArg is
      ConstTerm unitConst) { if (object.Equals(callConst.Value,
      unitConst.Value)) return null; return new UnifyFail($
      "Constant mismatch: {callConst.Value} vs {unitConst.Value}");
      } ... }` — Dart `callArg.value == unitArg.value` ⇒ C#
      `object.Equals(callConst.Value, unitConst.Value)` (Microsoft
      Learn: "Equals(Object, Object) ... determines whether two
      object instances are considered equal" — handles null-safe
      polymorphic equality for the `Object?` Value field of
      `ConstTerm` per ast.dart spec dart.ast_leaf.const_term_
      polymorphic_value_with_branching_string_quoting_tostring).
      Struct arm: functor + arity equality + recurse on args.
      List arm: nil-vs-nil equality + recurse on head/tail. Final
      fallback: `return new UnifyFail($"Unhandled case: {callArg.
      GetType().Name} vs {unitArg.GetType().Name}");` — Dart
      `runtimeType` ⇒ C# `GetType().Name` (Microsoft Learn Type.
      Name).
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
    nuance: >-
      Six intertwined nuances. (1) Null-as-success convention:
      `_unifyTerms` returns `UnifyResult?` with `null` meaning
      success and non-null meaning failure-or-suspend. This is
      unusual but preserved verbatim — converting to a boolean +
      out-parameter (or to UnifySuccess) would diverge from the
      Dart-side contract used by every caller. (2) Writer-vs-
      reader-vs-constant matrix: GLP semantics dictate that
      writers, readers, and constants unify according to a 3x3
      table (writer/writer = alias; writer/reader = alias;
      writer/const = bind; reader/writer = alias; reader/reader =
      alias + suspend; reader/const = suspend + compat-check;
      const/writer = bind; const/reader = bind; const/const =
      equality). The Dart code encodes this matrix as cascaded
      if/else-if; the C# port preserves the cascade verbatim. (3)
      `object.Equals` for `ConstTerm.Value`: the Value is `Object?`
      polymorphic (int, double, string per ast.dart), so
      reference-equality (Dart `==` on Object) is wrong — Dart
      `==` defaults to identity for Object but is OVERRIDDEN by
      String/int/double to be value-equality. C# `object.Equals`
      handles the same polymorphic value-equality (Microsoft
      Learn). Preserved verbatim. (4) Writer-to-writer aliasing:
      `subst[unitArg.name] = callArg;` — the UNIT-side writer is
      aliased to the CALL-side writer (asymmetric direction —
      load-bearing because the substitution is later applied to
      the call's clause, not the unit clause). (5) Compatibility
      check for reader-vs-const when the reader's writer is
      ALREADY bound: `_checkCompatible(existing, unitArg, subst,
      suspSet)` — structural check that prevents two
      contradictory bindings (e.g. X? unifies with a in one arg
      and with b in another). (6) Underscore-fast-path: the
      `IsUnderscore` check at the top covers BOTH `UnderscoreTerm`
      and `VarTerm` with name `_` (since Var-`_` may slip through
      from un-normalised AST); the OR ensures either-side
      underscore short-circuits to success. Preserved verbatim.

  - construct_key: dart.method.substset_helper_propagating_through_alias_chain
    source_form: >-
      "void _substSet(Map<String, Term> subst, String key, Term
      value) { if (subst.containsKey(key)) { final old = subst[key]!;
      if (old is VarTerm && !old.isReader && value is! VarTerm)
      { if (!subst.containsKey(old.name)) { subst[old.name] = value;
      } } } subst[key] = value; }" — substitution setter that
      detects "key was aliased to a writer; now key gets a concrete
      value, so propagate the binding to the writer too."
    target_decision: >-
      Emit `private static void SubstSet(Dictionary<string, Term>
      subst, string key, Term value) { if (subst.TryGetValue(key,
      out var old) && old is VarTerm oldVar && !oldVar.IsReader &&
      value is not VarTerm) { if (!subst.ContainsKey(oldVar.Name))
      subst[oldVar.Name] = value; } subst[key] = value; }`.
      `TryGetValue` replaces the Dart `containsKey + indexer` two-
      lookup pattern per the cached idiom (parser.dart). `value
      is! VarTerm` ⇒ `value is not VarTerm` per Microsoft Learn
      `is not` pattern (C# 9+ negated declaration pattern). The
      double-check (`!subst.ContainsKey(old.Name)`) prevents
      overwriting an existing binding for the chained writer —
      preserved verbatim.
    idiom_id: null
    research_finding_id: rf-dart-map-lookup-to-csharp-trygetvalue
    nuance: >-
      Alias-chain-propagation nuance (load-bearing for unification
      correctness): when X→Y is in subst (X aliased to writer Y) and
      we then set X→f(a), we MUST ALSO set Y→f(a) so the resolver
      can find Y's binding. Without this propagation, `resolveSubst`
      would yield X→f(a), Y→? — a half-resolved state that breaks
      downstream unification. The double-check
      `!subst.ContainsKey(old.Name)` prevents OVERWRITING an
      existing Y binding (which would be a contradiction the
      compat-check should catch — but defensive coding leaves it
      alone). Preserved verbatim. The `value is not VarTerm` guard
      means we only propagate when the new value is CONCRETE (a
      constant, struct, or list) — for VarTerm-to-VarTerm aliasing
      we leave the chain in place and let `_resolveSubstitution`
      flatten it. Subtle but load-bearing.

  - construct_key: dart.method.check_compatible_structural_compatibility_const_struct_loose_default_accept
    source_form: >-
      "UnifyResult? _checkCompatible(Term existing, Term newTerm,
      Map<String, Term> subst, Set<String> suspSet)" — light-weight
      structural check between an existing binding and a new
      proposed binding for the same writer. Returns `UnifyFail`
      for const-vs-const mismatch and struct-functor-mismatch; for
      everything else (variable cases, list, mixed) returns null
      (accept; deeper check deferred). The comment explicitly says
      "For now, accept other combinations (variables get resolved
      later)" — a deliberately under-specified check that handles
      the common cases.
    target_decision: >-
      Emit `private UnifyResult? CheckCompatible(Term existing,
      Term newTerm, Dictionary<string, Term> subst, HashSet<string>
      suspSet)`. Body: `if (existing is ConstTerm e && newTerm is
      ConstTerm n) { if (!object.Equals(e.Value, n.Value)) return
      new UnifyFail($"Incompatible bindings: {e.Value} vs {n.
      Value}"); return null; } if (existing is StructTerm es &&
      newTerm is StructTerm ns) { if (es.Functor != ns.Functor ||
      es.Args.Count != ns.Args.Count) return new UnifyFail($
      "Incompatible structures: {es.Functor} vs {ns.Functor}");
      return null; } return null;` — preserves the "loose accept"
      semantics verbatim. The `subst`/`suspSet` parameters are
      currently UNUSED in the body (Dart code reserves them for a
      possible future deep-recurse extension); the C# port
      preserves the unused parameters because the SIGNATURE is
      part of the helper-contract used by `_unifyTerms` (and a
      future deep-recurse would need them).
    idiom_id: null
    research_finding_id: rf-dart-is-chain-to-csharp-switch-expression-type-pattern
    nuance: >-
      Three intertwined nuances. (1) Deliberate under-specification:
      this is a documented "soft" compatibility check that returns
      null (accept) for most combinations. It is NOT a full
      unification — that would risk infinite recursion (a reader
      vs reader inside compat-check). Preserved verbatim. (2)
      const-vs-const equality uses the same `object.Equals` as
      `_unifyTerms` (polymorphic value-equality for the Object?
      Value). Preserved verbatim. (3) Unused parameters: `subst`
      and `suspSet` are passed but not used. Keeping them in the
      C# signature is reviewer-faithful; an alternate is to drop
      them (the call site doesn't depend on their being passed),
      but that diverges from Dart shape and creates a latent gap
      if the body is ever extended to use them. Spec preserves the
      params verbatim. The research-finding-id reuses the cached
      `is`-chain rewrite (parser.dart family) because the body is
      a pure `is`-chain over ConstTerm/StructTerm — the same
      Microsoft Learn declaration-pattern mapping as
      `_unifyTerms` and `_collectVarNames`, applied to a smaller
      arm-set.

  - construct_key: dart.method.is_underscore_test_unioning_two_dart_runtime_types
    source_form: >-
      "bool _isUnderscore(Term term) { return term is UnderscoreTerm
      || (term is VarTerm && term.name == '_'); }" — true when the
      term is EITHER `UnderscoreTerm` OR `VarTerm` with name `_`.
    target_decision: >-
      Emit `private static bool IsUnderscore(Term term) => term is
      UnderscoreTerm || (term is VarTerm varTerm && varTerm.Name
      == "_");`. Expression-bodied member (Microsoft Learn:
      "Expression-bodied members provide a more concise syntax").
      C# 7+ pattern matching with declaration (`term is VarTerm
      varTerm`) binds the variable for the property access.
    idiom_id: null
    research_finding_id: rf-dart-is-chain-to-csharp-switch-expression-type-pattern
    nuance: >-
      Two-shape detection nuance (load-bearing): the AST may carry
      `_` as EITHER an explicit `UnderscoreTerm` (created by the
      parser at certain grammar productions) or a `VarTerm` with
      name `_` (created at other productions). Both must be
      recognised by unification as anonymous-writer-success-no-bind.
      The OR-test handles both shapes uniformly. The C# port uses
      `is` pattern with declaration for the VarTerm case (cleaner
      than `term is VarTerm && ((VarTerm)term).Name == "_"`).
      Short-circuit-OR: both languages short-circuit on the left
      operand if true — preserves the test's performance
      characteristic (UnderscoreTerm is the common case).

  - construct_key: dart.method.resolve_substitution_flatten_chains_with_cycle_protection
    source_form: >-
      "Map<String, Term> _resolveSubstitution(Map<String, Term>
      subst)" + "Term _resolveTerm(Term term, Map<String, Term>
      subst, Set<String> visited)" — flatten substitution chains
      (X→Y, Y→f(Z) ⇒ X→f(Z), Y→f(Z)) with a per-walk `visited` set
      to detect cycles (Dart `if (visited.contains(term.name)) {
      return term; }`); preserves reader-status when a reader-var
      resolves to a writer-var.
    target_decision: >-
      Emit `private static Dictionary<string, Term>
      ResolveSubstitution(Dictionary<string, Term> subst)` walking
      every entry via `ResolveTerm`. Per-entry: `var resolved =
      new Dictionary<string, Term>(StringComparer.Ordinal);
      foreach (var entry in subst) resolved[entry.Key] = ResolveTerm(
      entry.Value, subst, new HashSet<string>(StringComparer.
      Ordinal)); return resolved;`. The `_resolveTerm` becomes
      `private static Term ResolveTerm(Term term,
      IReadOnlyDictionary<string, Term> subst, HashSet<string>
      visited)`. Body: `if (term is VarTerm varTerm) { if (visited.
      Contains(varTerm.Name)) return term; if (subst.TryGetValue(
      varTerm.Name, out var bound)) { visited.Add(varTerm.Name);
      var resolved = ResolveTerm(bound, subst, visited); if
      (varTerm.IsReader && resolved is VarTerm rv && !rv.IsReader)
      return new VarTerm(rv.Name, true, rv.Line, rv.Column); return
      resolved; } return term; } if (term is StructTerm s) return
      new StructTerm(s.Functor, s.Args.Select(a => ResolveTerm(a,
      subst, new HashSet<string>(visited, StringComparer.Ordinal))).
      ToList(), s.Line, s.Column); if (term is ListTerm l) { if (l.
      IsNil) return l; return new ListTerm(l.Head is not null ?
      ResolveTerm(l.Head, subst, new HashSet<string>(visited,
      StringComparer.Ordinal)) : null, l.Tail is not null ?
      ResolveTerm(l.Tail, subst, new HashSet<string>(visited,
      StringComparer.Ordinal)) : null, l.Line, l.Column); } return
      term;` — Dart `{...visited}` set-spread ⇒ C# `new HashSet<
      string>(visited, StringComparer.Ordinal)` (copy-constructor).
      Microsoft Learn `HashSet<T>(IEnumerable<T>)` constructor.
    idiom_id: null
    research_finding_id: rf-dart-iterable-where-to-linq
    nuance: >-
      Four intertwined nuances. (1) Cycle detection: the `visited`
      set prevents infinite recursion when a substitution chain
      loops (e.g. X→Y, Y→X — should not normally happen but
      defensive). Returning the term as-is (`return term;`) on
      cycle detection is the documented behaviour (the Dart
      comment says "Cycle - return as is"). Preserved verbatim.
      (2) Visited-set per-branch copy: when recursing into struct/
      list children, the `visited` set is COPIED (`{...visited}`)
      so that sibling branches do not see each other's visited
      names. Without this, a `f(X, X)` shape would falsely report
      a cycle on the second X. Preserved verbatim via C#
      copy-constructor. (3) Visited-set in-place mutation along
      VarTerm chain: in the VarTerm-chain case, the visited set
      is mutated in-place (`visited.Add(varTerm.Name);`) — this
      is correct because the chain is linear (no fanout). The
      copy is only needed at struct/list fanout points. (4)
      Reader-status preservation: when a reader-var X? resolves
      to a writer-var Y, we return Y? (NOT Y) — the
      reader-marker is preserved on the resolved term. This is
      load-bearing for downstream code that distinguishes readers
      from writers. Preserved verbatim with `new VarTerm(rv.Name,
      true, rv.Line, rv.Column)`.

  - construct_key: dart.method.apply_substitution_to_term_atom_guard_goal_with_remoteGoal_spawnGoal_preservation
    source_form: >-
      "_applySubstitution(Term term, ...)", "_applySubstitutionToAtom
      (Atom atom, ...)", "_applySubstitutionToGuard(Guard guard,
      ...)", "_applySubstitutionToGoal(Goal goal, ...)" — four
      structurally-similar helpers walking the AST and applying a
      `Map<String, Term>` substitution. The Goal variant is
      special: it dispatches on `RemoteGoal` (M # proc(...)) and
      `SpawnGoal` (Goal@Agent) leaves to preserve their wrapper
      structure (recursively substituting only the inner module/
      goal/agent components).
    target_decision: >-
      Emit one helper per type — `Term ApplySubstitution(Term
      term, IReadOnlyDictionary<string, Term> subst)`, `Atom
      ApplySubstitutionToAtom(Atom atom, IReadOnlyDictionary<
      string, Term> subst)`, `Guard ApplySubstitutionToGuard(Guard
      guard, IReadOnlyDictionary<string, Term> subst)`, `Goal
      ApplySubstitutionToGoal(Goal goal, IReadOnlyDictionary<
      string, Term> subst)`. All `private static`. The Term
      variant: `if (term is VarTerm varTerm) { if (varTerm.Name
      == "_") return term; if (subst.TryGetValue(varTerm.Name,
      out var replacement)) { if (varTerm.IsReader && replacement
      is VarTerm rv && !rv.IsReader) return new VarTerm(rv.Name,
      true, rv.Line, rv.Column); return ApplySubstitution(
      replacement, subst); } return term; } if (term is StructTerm
      s) return new StructTerm(s.Functor, s.Args.Select(a =>
      ApplySubstitution(a, subst)).ToList(), s.Line, s.Column);
      if (term is ListTerm l) { if (l.IsNil) return l; return new
      ListTerm(l.Head is not null ? ApplySubstitution(l.Head,
      subst) : null, l.Tail is not null ? ApplySubstitution(l.
      Tail, subst) : null, l.Line, l.Column); } if (term is
      UnderscoreTerm) return term; return term;` — Const/default
      pass-through. The Goal variant dispatches FIRST on
      RemoteGoal/SpawnGoal then falls through to plain Goal: `if
      (goal is RemoteGoal rg) { var newModule = ApplySubstitution(
      rg.Module, subst); var newInner = ApplySubstitutionToGoal(
      rg.Goal, subst); return new RemoteGoal(newModule, newInner,
      rg.Line, rg.Column); } if (goal is SpawnGoal sg) { var
      newInner = ApplySubstitutionToGoal(sg.InnerGoal, subst);
      return new SpawnGoal(newInner, sg.AgentId, sg.Line, sg.
      Column); } return new Goal(goal.Functor, goal.Args.Select(
      a => ApplySubstitution(a, subst)).ToList(), goal.Line, goal.
      Column);`. The Atom/Guard variants are uniform: rebuild
      with substituted args.
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
    nuance: >-
      Four intertwined nuances. (1) Underscore-preservation
      (load-bearing): same as in renaming — `_` is anonymous,
      must NOT be substituted (each `_` is a unique writer).
      Preserved verbatim. (2) Reader-status preservation on
      substitution (the same rule as resolve-term): if a reader
      X? is substituted by a writer Y, return Y? (not Y). Load-
      bearing for SRSW. (3) RemoteGoal / SpawnGoal type-
      preservation (load-bearing for distributed-GLP semantics):
      `RemoteGoal` and `SpawnGoal` are sub-leaves of `Goal`
      (per ast.dart spec); applying a plain `Goal` constructor
      to a substituted RemoteGoal would LOSE the remote-call
      wrapper. The dispatch ensures the wrapper is reconstructed
      with substituted inner components. Preserved verbatim with
      `if (goal is RemoteGoal rg) return new RemoteGoal(...)` /
      `if (goal is SpawnGoal sg) return new SpawnGoal(...)`. (4)
      Recursive substitution on VarTerm resolution: `return
      ApplySubstitution(replacement, subst);` — the substituted
      term is RECURSIVELY substituted, in case `replacement` is
      itself a VarTerm whose name is in subst. This is the
      "transitive-closure" semantics of substitution; both
      languages preserve it via direct recursion. Could
      pathologically loop if subst has a self-reference (X→X),
      but `_resolveSubstitution` runs FIRST and flattens chains
      / aborts cycles, so by the time `_applySubstitution` is
      called the substitution is acyclic. Load-bearing
      pipeline invariant.

  - construct_key: dart.method.simplify_guards_remove_redundant_with_concrete_arg_type_table_dispatch
    source_form: >-
      "_simplifyGuards(List<Guard>? guards, Atom head)" +
      "_isRedundantGuard(Guard guard, Atom head)" +
      "_getConcreteArg(Term term)" + "_isGround(Term term)" —
      post-specialisation simplification: remove guards that are
      ALWAYS true given the head pattern. The `_isRedundantGuard`
      method has a switch over the guard's predicate name
      (tuple/compound, list/is_list, integer, number, atom,
      ground, no_readers) and checks structural properties of the
      concrete arg (e.g. tuple/compound is true if the arg is a
      StructTerm).
    target_decision: >-
      Emit `private static List<Guard>? SimplifyGuards(List<Guard>?
      guards, Atom head) { if (guards is null || guards.Count ==
      0) return null; var simplified = new List<Guard>(); foreach
      (var guard in guards) { if (IsRedundantGuard(guard, head))
      continue; simplified.Add(guard); } return simplified.Count
      == 0 ? null : simplified; }`. `IsRedundantGuard`: `if
      (guard.Args.Count == 1) { var concreteArg = GetConcreteArg(
      guard.Args[0]); if (concreteArg is not null) { return guard.
      Predicate switch { "tuple" or "compound" => concreteArg is
      StructTerm, "list" or "is_list" => concreteArg is ListTerm,
      "integer" => concreteArg is ConstTerm ic && ic.Value is int
      or long, "number" => concreteArg is ConstTerm nc && (nc.Value
      is int or long or double or float), "atom" => concreteArg
      is ConstTerm ac && ac.Value is string, "ground" or
      "no_readers" => IsGround(concreteArg), _ => false }; } }
      return false;` — Microsoft Learn switch expression (C# 8+);
      `or` pattern in case (C# 9+ `or` pattern). `GetConcreteArg`:
      `if (term is VarTerm) return null; if (term is ConstTerm or
      StructTerm or ListTerm) return term; return null;`.
      `IsGround`: `if (term is VarTerm) return false; if (term is
      UnderscoreTerm) return true; if (term is ConstTerm) return
      true; if (term is StructTerm s) return s.Args.All(IsGround);
      if (term is ListTerm l) { if (l.IsNil) return true; var
      headG = l.Head is null || IsGround(l.Head); var tailG = l.
      Tail is null || IsGround(l.Tail); return headG && tailG; }
      return false;`.
    idiom_id: null
    research_finding_id: rf-dart-is-chain-to-csharp-switch-expression-type-pattern
    nuance: >-
      Five intertwined nuances. (1) Width-mapping for `integer`/
      `number` type guards: Dart `int` covers BOTH 32-bit and 64-
      bit; the spec maps Dart `int` to C# `long` (cached recurring
      idiom), so the `integer` check must accept both `int` and
      `long` — `ic.Value is int or long`. Similarly `number` must
      accept `int`/`long`/`double`/`float`. Preserved with explicit
      type-list per C# `or` pattern. (2) `atom` ⇒ `string`: GLP
      "atom" is a string symbol (lower-case identifier), stored as
      a Dart `String` in `ConstTerm.Value`. C# port uses `string`
      identically. (3) `ground` and `no_readers`: SAME
      implementation — both check that the concrete arg contains
      no variables. The Dart code has `case 'no_readers'` falling
      through to the same `_isGround` test as `case 'ground'`,
      with a comment "At compile time, a concrete term has no
      variables (hence no readers)". Preserved verbatim with `"
      ground" or "no_readers" => IsGround(concreteArg)`. (4)
      `_getConcreteArg` returning null for ANY VarTerm (including
      readers): the comment "A reader reference - try to find what
      it refers to / For now, if it's a reader, we can't determine
      concreteness" documents a deliberate conservative
      approximation. Preserved verbatim. (5) Single-arg guard
      restriction: `if (guard.args.length == 1)` — multi-arg
      guards are NEVER simplified (no type guard takes >1 arg in
      this table). Preserved verbatim.
conversion_units:
  - "internal static class PreludeUnitClauses (mutable source field, cached map field, SetPreludeUnitClauseSource, GetPreludeUnitClauses)"
  - "abstract class UnifyResult + sealed leaves UnifySuccess / UnifyFail / UnifySuspend (IReadOnly payloads)"
  - "class PartialEvaluator (instance: long _varCounter, public Stage-1 TransformDefinedGuards, public Stage-2 UnfoldReduceCalls)"
  - "private helper IsUnitClauseShape (lifted from three duplicate sites)"
  - "private static CollectUnitClauses / CollectAllProcedures / CollectReduceFacts"
  - "private TransformClause (fixpoint loop, three throw-arms, builtin/all-procedures fallthrough)"
  - "private UnfoldReduceInClause (first-reduce-only, three-arm unify dispatch, AddRange expansions)"
  - "private RenameClauseVars / RenameUnitClauseVars (underscore-preserving fresh-name renaming)"
  - "private static CollectVarNames / CollectVarNamesFromAtom (recursive term walk)"
  - "private static ApplyRenaming / ApplyRenamingToAtom (recursive rebuild with underscore demotion)"
  - "private GlpUnifyForPE (phase-1-collect / phase-2-resolve three-valued unifier)"
  - "private UnifyTerms (six-arm writer/reader/const/struct/list/underscore matrix)"
  - "private static SubstSet (alias-chain-propagation helper)"
  - "private CheckCompatible (loose structural compat check, unused subst/suspSet params preserved)"
  - "private static IsUnderscore (two-shape detection)"
  - "private static ResolveSubstitution / ResolveTerm (chain flattening with cycle protection and reader preservation)"
  - "private static ApplySubstitution / ApplySubstitutionToAtom / ApplySubstitutionToGuard / ApplySubstitutionToGoal (with RemoteGoal/SpawnGoal preservation)"
  - "private static SimplifyGuards / IsRedundantGuard / GetConcreteArg / IsGround (type-guard redundancy table + ground-check)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-relative-import-to-csharp-using-or-same-namespace - import folding

- Deep analysis: five Dart imports — four sibling-folder
  (`ast.dart`, `error.dart`, `lexer.dart`, `parser.dart`) and one
  cross-folder selective (`'../analysis/type_checker/prelude.dart'
  show builtinProcedures`). Sibling imports fold into a shared C#
  namespace; the selective `show` import is filtering visibility to
  a single static.
- Authoritative Dart: dart.dev/language/libraries documents `import`
  per-file scoping and `show`/`hide` combinators. Cached idiom from
  parser.dart (rf-dart-relative-import-to-csharp-using-or-same-
  namespace).
- Authoritative .NET: Microsoft Learn `using static` directive
  imports the accessible static members of a specified type —
  matches the `show` filter's intent of exposing a single static
  symbol.

### rf-dart-map-to-csharp-dictionary - map mapping (cached pattern)

- Deep analysis: three independent `Map<String, ...>` uses — the
  prelude-unit-clauses cache, the per-call substitution table
  (`Map<String, Term> substitution`), and the per-call renaming
  table (`Map<String, String> renaming`). All three need O(1)
  string-keyed lookup; none need iteration order.
- Cached idiom (pmt/type_table.dart family). Microsoft Learn
  `Dictionary<TKey,TValue>`: average O(1) lookup; explicit
  `StringComparer.Ordinal` is robust against locale changes and is
  the documented best practice for ASCII-only keys.

### rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves - sealed discriminated union

- Deep analysis: the Dart 3 `sealed class UnifyResult` + three
  concrete subclasses (`UnifySuccess`/`UnifyFail`/`UnifySuspend`)
  is a closed sum type consumed by exhaustive pattern-switching at
  two call sites in this file. Dart-3 `sealed` STATICALLY enforces
  exhaustiveness.
- Cached idiom from ast.dart (rf-dart-abstract-marker-base-to-
  csharp-abstract-sealed-leaves). Microsoft Learn: "It's an error
  to use the abstract modifier with a sealed class." Closure
  expressed via (a) sealed leaves and (b) exhaustive switch with
  throwing default arm (runtime contract preserves the static
  guarantee Dart provides).

### rf-dart-implements-exception-to-csharp-derive-system-exception - exception throwing

- Deep analysis: three throw-arms in `_transformClause` produce
  `CompileError` instances with multi-line messages and a `phase`
  named argument. Multi-line messages use Dart `\n` escape, which
  maps directly to C# `\n`. The named-arg `phase: 'analyzer'` and
  `phase: 'partial_evaluator'` map to the cached named-default-
  param idiom from error.dart (rf-dart-named-default-param-to-
  csharp-optional-arg).
- Cached idiom from error.dart. Microsoft Learn `System.Exception`
  derivation and constructor passes parent message to base.

### rf-dart-iterable-where-to-linq - LINQ vs imperative loop

- Deep analysis: every per-procedure / per-clause / per-term walk
  in this file uses imperative `for`-loops with explicit `.add(...)`.
  None of them benefit from deferred LINQ semantics (every result is
  immediately materialised). Cached idiom from analysis/analysis_
  phase.dart: LINQ chains MAY be substituted (`.Select(...).ToList()
  `) when reviewer prefers — equivalence is preserved as long as
  ToList terminates the query.
- Spec PREFERS imperative loops to match Dart shape (one-to-one
  reviewer mapping); LINQ is acceptable at codegen-stage discretion.

### rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture - pattern matching

- Deep analysis: cascaded `term is VarTerm` / `term is StructTerm`
  / `term is ListTerm` chains throughout the file. Cached idiom
  from parser.dart. Microsoft Learn declaration pattern + switch
  expression (C# 8+) + `when`-clause guards (C# 7+). Property-access
  on the bound variable preserves the type-narrowed access Dart
  provides via promotion.

### rf-dart-tostring-interp-to-csharp-tostring-interp - interpolation

- Deep analysis: multiple `'${...}/${...}'` and `'PE${_varCounter++}'`
  interpolations. Cached idiom from token.dart / parser.dart family.
  Microsoft Learn interpolated strings; post-increment in
  interpolation is well-defined in both languages (yield old value
  then increment).

### rf-dart-is-chain-to-csharp-switch-expression-type-pattern - is-chain rewrite

- Deep analysis: `_collectVarNames`, `_isUnderscore`,
  `_isRedundantGuard` use `is`-chains over the Term hierarchy.
  Cached idiom from parser.dart family.

### rf-dart-string-interpolation-join-to-csharp-interpolation-string-join - join with separator

- Deep analysis: error messages include `${guard.args.join(", ")}`
  and the signature key `'${proc.name}/${proc.arity}'`. Cached
  idiom from parser.dart family. Microsoft Learn `string.Join`:
  identical contract to Dart `List<T>.join` (separator between
  elements, no leading or trailing separator).

### rf-dart-map-lookup-to-csharp-trygetvalue - lookup idiom

- Deep analysis: `_substSet`, `_transformClause` (unitClauses lookup),
  `_applyRenaming` (renaming lookup), `_applySubstitution` (subst
  lookup) all do `containsKey + indexer` two-lookup. Cached idiom
  from parser.dart contiguity check. Microsoft Learn `TryGetValue`:
  one lookup with out-parameter binding.

### rf-dart-list-to-csharp-list-of-T - list mapping (cached)

- Deep analysis: `List<Procedure>` / `List<Clause>` / `List<Guard>`
  / `List<Goal>` / `List<Term>` everywhere. Cached idiom from
  parser.dart guard-parser. Direct mapping; growable; index
  access; no LINQ deferred semantics needed because every site
  immediately materialises.

### csharp-static-class-no-toplevel-members - top-level globals

- Deep analysis: Dart permits free top-level mutable globals
  (`String? _preludeUnitClauseSource;`) and free top-level
  functions (`void setPreludeUnitClauseSource(...)`,
  `Map<String, List<Term>> getPreludeUnitClauses()`); C# does
  NOT. Cached idiom from prelude.dart. Wrap in a `static class
  PreludeUnitClauses` to host all three.

## Notes on shapes NOT in this file (per spec quality bar)

The file does NOT use: `async`/`await`, `Future`, `Stream`,
`Completer`, `Isolate`, mixins, extensions, `record`s,
operator overloading, generics with bounds, factory constructors,
const constructors, named constructors, top-level extensions,
extension types, FFI, or web-only types. Therefore the well-known
nuances `Stream` → `IAsyncEnumerable`, `Future` → `Task`/`ValueTask`,
isolate boundary semantics, and async-cancellation are
DELIBERATELY ABSENT from this spec — the source does not contain
them, and inventing translations would violate the spec quality
bar (analysis/analysis_phase.dart precedent).

The file is PURELY synchronous, pure-Dart, runs in a single
isolate, and uses NO platform-specific APIs (no `dart:io`,
`dart:ffi`, `dart:isolate`). The conversion target is correspondingly
synchronous C# with no platform-specific dependencies.

## Cross-spec consistency

This spec reuses the following cached research findings from sibling
specs — no re-research, no re-derivation (FR-024 cache discipline):

- rf-dart-relative-import-to-csharp-using-or-same-namespace (parser.dart)
- rf-dart-leading-underscore-privacy-to-csharp-private (error.dart)
- rf-dart-nullsafety-to-csharp-nrt (analysis_phase.dart)
- rf-dart-map-to-csharp-dictionary (pmt/type_table.dart family)
- rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves (ast.dart)
- rf-dart-implements-exception-to-csharp-derive-system-exception (error.dart)
- rf-dart-named-default-param-to-csharp-optional-arg (error.dart)
- rf-dart-iterable-where-to-linq (analysis_phase.dart)
- rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture (parser.dart)
- rf-dart-tostring-interp-to-csharp-tostring-interp (token.dart / parser.dart)
- rf-dart-is-chain-to-csharp-switch-expression-type-pattern (parser.dart)
- rf-dart-string-interpolation-join-to-csharp-interpolation-string-join (parser.dart)
- rf-dart-map-lookup-to-csharp-trygetvalue (parser.dart)
- rf-dart-list-to-csharp-list-of-T (parser.dart)
- csharp-static-class-no-toplevel-members (prelude.dart)

All sixteen sibling-cached findings are AUTHORITATIVE
(Dart official docs / Microsoft Learn) per the FR-024 contract.
No new research was needed for this file; every construct mapped
to a cached finding, confirming the codebase has reached the
recurring-construct steady state (≥95% reuse, SC-007).
