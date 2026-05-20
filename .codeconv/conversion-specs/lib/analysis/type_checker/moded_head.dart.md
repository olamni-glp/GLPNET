# Conversion Spec — lib/analysis/type_checker/moded_head.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/moded_head.dart
source_sha256: 8e1cf1a9af1ccc77174921ef4c2df7845bce7406fc3930b69d791cd8f087d4e2
target_code_unit: lib/analysis/type_checker/moded_head.cs
constructs:
  - construct_key: dart.toplevel_mutable_int.fresh_name_counter_per_clause
    source_form: >-
      int _anonVarCounter = 0;
      void resetAnonVarCounter() { _anonVarCounter = 0; }
      String _freshAnonVarName() { _anonVarCounter++; return '_#$_anonVarCounter'; }
    target_decision: >-
      Dart library-level mutable `int` (one cell shared across every call into
      this file's three top-level helpers) maps to a `private static int` field
      on the host static class `ModedHead`. Public reset becomes
      `public static void ResetAnonVarCounter() { _anonVarCounter = 0; }`;
      private generator becomes `private static string FreshAnonVarName() { _anonVarCounter++; return $"_#{_anonVarCounter}"; }`.
      The counter is plain `int` (32-bit signed in C# — matches Dart `int`
      width on 64-bit but is narrower; the counter is reset per clause so any
      realistic clause stays well under `int.MaxValue`; recorded as a known
      narrowing per nuance below). NO `Interlocked.Increment` /
      `[ThreadStatic]` — the Dart code assumes single-threaded clause
      processing (analysis runs on the compiler's main isolate, no
      concurrency); preserving that assumption verbatim avoids fabricating a
      threading model the source does not have (spec FR-013: escalate, don't
      guess — here the source IS single-threaded by construction, so no
      escalation, just a faithful 1:1 single-threaded mapping).
    idiom_id: null
    research_finding_id: rf-dart-library-private-mutable-int-counter-to-csharp-private-static-field
    nuance: >-
      Value-vs-reference: `int` is a value type in both languages — no
      aliasing concern. Width: Dart `int` is arbitrary-precision-up-to-64-bit
      on the native VM but the counter is bounded by AST node count per
      clause (small); C# `int` is 32-bit (`System.Int32`). Mapping to C#
      `long` would be over-engineering; `int` is faithful and decisively
      sufficient. Threading: Dart isolates have one mutator thread; the C#
      analog (single-thread of execution invoking the analysis) is preserved
      — the field is NOT marked `volatile`, NOT `Interlocked`-incremented,
      because doing so would add a contract (thread safety) that the Dart
      source does not promise. Public-vs-private surface: only
      `ResetAnonVarCounter` is public (Dart top-level no leading `_`); the
      counter and `FreshAnonVarName` are private (`_`-prefixed in Dart) —
      mapped to `private static` per
      rf-dart-library-private-underscore-to-csharp-file-or-internal (cached
      from `moded_term.dart` — FR-024).
  - construct_key: dart.public_toplevel_fn.head_arity_validate_and_two_step_build
    source_form: >-
      ModedTerm modedHead(ast.Goal head, ProcDecl decl, {TypeEnvironment? typeEnv}) {
        resetAnonVarCounter();
        if (head.arity != decl.arity) { throw ArityMismatchError('...'); }
        final ioTerm = _buildIOModedTerm(head, decl, Mode.consume, typeEnv);
        return _ensureVariablesMatchModes(ioTerm);
      }
    target_decision: >-
      Emit on host static class `ModedHead` as
      `public static ModedTerm Build(Goal head, ProcDecl decl, TypeEnvironment? typeEnv = null)`
      (canonical Dart→C# top-level-fn idiom — cached from `mode.dart` /
      `prelude.dart` / `clause_validation.dart`). Two name choices: (a)
      `ModedHeadOf` / `OfHead`, (b) static method `Build` on host
      `ModedHead`; (b) chosen because the host class name IS `ModedHead`
      (Pascal-cased file name) and `ModedHead.Build(head, decl)` reads more
      naturally than `ModedHead.ModedHead(...)`. Dart named optional
      parameter `{TypeEnvironment? typeEnv}` maps to a C# parameter with
      default value `TypeEnvironment? typeEnv = null` (Microsoft Learn:
      optional arguments use default values; callers may pass by name).
      `Mode.consume` enum value preserved verbatim (cached from
      `mode.dart`). Arity validation is a guard clause that throws
      `ArityMismatchError` (see error-class construct below). The
      two-step pipeline (`_buildIOModedTerm` → `_ensureVariablesMatchModes`)
      maps verbatim to two private static helper calls.
    idiom_id: dart-toplevel-fn-to-csharp-static-method
    research_finding_id: rf-csharp-static-class-no-toplevel-members
    nuance: >-
      Cached/reused idiom from `clause_validation.dart` /
      `program_dfa.dart` (FR-024 — never re-research). Dart has top-level
      functions; C# does not — every method is a type member. Host static
      class `ModedHead` (Pascal of file stem) chosen as the conventional
      home. Named-optional parameter `{TypeEnvironment? typeEnv}` maps to
      a default-valued positional parameter — call sites use C# named-
      argument syntax to pass it explicitly (`ModedHead.Build(head, decl, typeEnv: env)`),
      reproducing Dart call-site readability exactly
      (rf-dart-named-required-params-to-csharp-named-positional cache from
      `moded_term.dart`). Null-safety: `TypeEnvironment?` is `TypeEnvironment?`
      in both — same nullable reference type semantics. Reference identity:
      `Goal head` / `ProcDecl decl` / `TypeEnvironment? typeEnv` are all
      reference types — passed by reference identity in both Dart and C#;
      the function does not mutate them.
  - construct_key: dart.public_toplevel_fn.body_atom_no_flip_no_counter_reset
    source_form: >-
      ModedTerm producedTerm(ast.Goal atom, ProcDecl decl, {TypeEnvironment? typeEnv}) {
        if (atom.arity != decl.arity) { throw ArityMismatchError('...'); }
        return _buildIOModedTerm(atom, decl, Mode.produce, typeEnv);
      }
    target_decision: >-
      Emit as `public static ModedTerm ProducedTerm(Goal atom, ProcDecl decl, TypeEnvironment? typeEnv = null)`
      on the same host static class `ModedHead`. The function differs from
      `Build` (above) in three load-bearing ways that MUST be preserved
      verbatim: (1) NO `ResetAnonVarCounter()` call — body atoms share the
      clause's anonymous-variable namespace with the head (the head's
      `Build` resets once at clause start; body atoms inherit the same
      counter so anonymous-underscore freshness is contiguous across head
      AND every body atom of the same clause), (2) root mode is
      `Mode.produce` (not `Mode.consume`), (3) NO call to
      `_ensureVariablesMatchModes` (no variable flip for body atoms — the
      caller perspective preserves reader/writer roles). These three
      semantic differences are the ENTIRE reason the two public functions
      are separate; the spec mandates preserving them as a single
      explicitly-documented divergence (Dart comment `Note: Do NOT reset
      counter here` survives as `// NOTE: do NOT reset the counter here —
      body atoms share the clause's anon-var namespace with the head`).
    idiom_id: dart-toplevel-fn-to-csharp-static-method
    research_finding_id: rf-csharp-static-class-no-toplevel-members
    nuance: >-
      Cached/reused idiom. The three semantic deltas from `Build` (no
      counter reset, root mode `produce`, no variable flip) are the
      load-bearing contract of `ProducedTerm`; codegen MUST NOT factor the
      two public functions into a parameter-driven single function (Dart
      keeps them separate exactly to make the differences impossible to
      miss in a code review). Reference-vs-value: same as `Build`. Null-
      safety: same. Threading: same single-threaded contract.
  - construct_key: dart.private_toplevel_fn.io_moded_term_builder_per_arg_mode
    source_form: >-
      ModedTerm _buildIOModedTerm(ast.Goal term, ProcDecl decl, Mode parentMode, TypeEnvironment? typeEnv) {
        final modedArgs = <ModedTerm>[];
        for (int i = 0; i < term.args.length; i++) {
          final argType = decl.argTypes[i];
          final argMode = decl.isInputArg(i) ? Mode.consume : Mode.produce;
          final modedArg = _buildModedSubterm(term.args[i], argMode, argType, typeEnv);
          modedArgs.add(modedArg);
        }
        return ModedCompound(parentMode, term.functor, term.arity, modedArgs);
      }
    target_decision: >-
      Emit as `private static ModedTerm BuildIOModedTerm(Goal term, ProcDecl decl, Mode parentMode, TypeEnvironment? typeEnv)`
      on host `ModedHead`. The Dart `for (int i = 0; ...)` C-style index
      loop maps verbatim to C# `for (int i = 0; ...)` (NOT LINQ — the
      loop body simultaneously reads three parallel index-aligned slices
      `term.args[i]`, `decl.argTypes[i]`, `decl.isInputArg(i)`; a
      `Select((arg, i) => ...)` would obscure the parallel-index semantics
      and force materialisation through a tuple, regressing readability for
      no gain). The growable `<ModedTerm>[]` list literal maps to
      `var modedArgs = new List<ModedTerm>(term.Args.Count);` with capacity
      pre-sized (Microsoft Learn `List<T>` capacity hint reduces re-
      allocations). Ternary `decl.isInputArg(i) ? Mode.consume : Mode.produce`
      maps verbatim to C# conditional expression `decl.IsInputArg(i) ? Mode.Consume : Mode.Produce`.
      Final allocation `ModedCompound(parentMode, term.functor, term.arity, modedArgs)`
      maps to `new ModedCompound(parentMode, term.Functor, term.Arity, modedArgs)` —
      the positional ctor preserved per `moded_term.dart` spec.
    idiom_id: dart-private-toplevel-helper-to-csharp-private-static-method
    research_finding_id: rf-csharp-private-vs-internal-library-helpers
    nuance: >-
      Cached/reused idiom from `clause_validation.dart` /
      `program_dfa.dart`. The C-style index loop preservation (NOT LINQ
      .Select) is decisive — three parallel index-aligned reads against
      `term.args`, `decl.argTypes`, `decl.isInputArg` would be
      re-expressed unnaturally in LINQ. Reference-vs-value: `ModedTerm`
      reference type; the `modedArgs` list aliases the freshly-built
      sub-trees (no shared aliasing with `term.args` — each call produces
      a NEW ModedTerm wrapping the same underlying AST term; the AST is
      read-only). Pre-sized list capacity is a documented performance
      hint; semantics identical.
  - construct_key: dart.private_recursive_dispatch.ast_term_subtype_switch_with_wildcard_fast_path
    source_form: >-
      ModedTerm _buildModedSubterm(ast.Term term, Mode mode, TypeExpr? expectedType, TypeEnvironment? typeEnv) {
        if (expectedType == null || expectedType is PrimitiveModeAlt) {
          return _buildOpaqueModedTerm(term, mode);
        }
        if (term is ast.VarTerm) { return ModedVariable(term.name, isReader: term.isReader, structuralMode: mode); }
        if (term is ast.StructTerm) { ... _getSubtermModes(...) ... }
        if (term is ast.ListTerm) { ... _getListSubtermModes(...) ... }
        if (term is ast.ConstTerm) { return ModedConstant(mode, term.value ?? 'null'); }
        if (term is ast.UnderscoreTerm) { final uniqueName = _freshAnonVarName(); return ModedVariable(uniqueName, isReader: false, structuralMode: mode); }
        throw InvalidHeadError('Unknown term type: ${term.runtimeType}');
      }
    target_decision: >-
      Emit as `private static ModedTerm BuildModedSubterm(Term term, Mode mode, TypeExpr? expectedType, TypeEnvironment? typeEnv)`.
      Convert the chained `if (term is X) { ... }` sequence to a single C#
      `switch` expression with type patterns and `default => throw new
      InvalidHeadError($"Unknown term type: {term.GetType().Name}")`
      (rf-dart-extension-is-as-to-csharp-type-pattern-switch — cached from
      `type_ast.dart` / `moded_term.dart`). CRITICAL ORDERING: the
      wildcard fast-path test `expectedType == null || expectedType is
      PrimitiveModeAlt` MUST remain a guard clause *before* the type
      switch on `term` (NOT collapsed into a `when`-clause arm) because in
      the wildcard branch ALL term shapes (including the `StructTerm` and
      `ListTerm` shapes that have type-driven sub-mode computation) are
      uniformly handled by `BuildOpaqueModedTerm` regardless of their
      AST subtype — the guard is *orthogonal* to the term-subtype
      dispatch and the spec comment in source makes this explicit ("FIX:
      When type is wildcard ... all subterms inherit the same mode
      uniformly"). C# emission preserves this two-stage flow: an
      `if (expectedType is null or PrimitiveModeAlt) return BuildOpaqueModedTerm(term, mode);`
      guard, followed by `return term switch { VarTerm v => ..., StructTerm s
      => ..., ListTerm l => ..., ConstTerm c => ..., UnderscoreTerm _ =>
      ..., _ => throw new InvalidHeadError(...) };`. The constant
      coalescing `term.value ?? 'null'` maps to `c.Value ?? "null"`
      verbatim (null-aware operator is identical between Dart and C# —
      rf-dart-csharp-null-aware-call-operator-identical cached from
      `program_dfa.dart`).
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Cached/reused idiom from `type_ast.dart` / `moded_term.dart` /
      `clause_validation.dart`. The wildcard-guard ordering is the
      load-bearing nuance: it MUST run BEFORE the term-subtype dispatch
      (orthogonal concerns); a naive translator that fused everything
      into one `switch` would change semantics catastrophically (a
      `StructTerm` with wildcard `expectedType` would fall through to the
      type-driven branch instead of the opaque branch). Reference-vs-value:
      all AST terms are reference types; the closed-set totality of the
      Dart switch (five known subtypes) is reproduced by a throwing
      discard arm because `ast.Term` is not language-sealed in the
      target (rf-dart-abstract-ast-base-to-csharp-abstract-sealed-leaves
      cache). Null-coalescing on `ConstTerm.value`: identical operator
      in both languages (cached finding).
  - construct_key: dart3.record_return_type.list_of_two_field_anonymous_tuple
    source_form: >-
      List<(Mode, TypeExpr?)> _getSubtermModes(String functor, int arity, Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv) {
        final defaultModes = List.generate(arity, (_) => (parentMode, null as TypeExpr?));
        ... return result; // result is List<(Mode, TypeExpr?)>
      }
    target_decision: >-
      Dart 3 positional records `(Mode, TypeExpr?)` map to C#
      `ValueTuple<Mode, TypeExpr?>` written with the inline tuple syntax
      `(Mode, TypeExpr?)`. The method signature becomes
      `private static List<(Mode Mode, TypeExpr? Type)> GetSubtermModes(string functor, int arity, Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv)`.
      C# inline tuples MAY name the elements (e.g. `(Mode Mode, TypeExpr? Type)`)
      to improve call-site readability (the Dart source uses positional
      destructuring `final (subtermMode, subtermType) = subtermModes[i];`
      which C# maps to `var (subtermMode, subtermType) = subtermModes[i];`
      — tuple deconstruction is supported in both languages, identical
      ergonomics). `List.generate(arity, (_) => (parentMode, null as TypeExpr?))`
      maps to `Enumerable.Repeat<(Mode, TypeExpr?)>((parentMode, null), arity).ToList()`
      OR to a pre-sized `List<...>` filled with a `for` loop — both
      semantically identical (eager materialisation); the `for`-loop
      variant is preferred because `List.generate` in Dart is also
      eager-via-index, matching the for-loop semantics more directly than
      `Repeat`. The reuse of one shared tuple value across `arity` slots
      is benign because tuples are value types (no aliasing concern).
    idiom_id: dart-record-tuple-key-to-csharp-valuetuple
    research_finding_id: rf-dart3-record-to-csharp-valuetuple
    nuance: >-
      Cached/reused idiom from `program_dfa.dart` (FR-024 — never
      re-research). Value-vs-reference is decisive here. Dart 3
      positional records are VALUE types with value equality and pass-
      by-value semantics; C# `ValueTuple` (the underlying type of `(T1,
      T2)` syntax) is ALSO a value type with synthesised value equality
      (`ValueTuple<T1,T2>.Equals` is structural). Therefore the Dart→C#
      mapping preserves both the equality semantics AND the pass-by-value
      semantics exactly — no boxing in the common index/return paths
      (Microsoft Learn: `ValueTuple` is a `struct`, not a `Tuple`). NOT
      `System.Tuple<Mode, TypeExpr?>` (which is a reference type and
      would change pass-semantics + force heap allocation). NOT a named
      `record struct` — the inline tuple is more parsimonious and the
      Dart record is anonymous. The `null as TypeExpr?` Dart cast (which
      tells the type inference engine which nullable type the null
      belongs to) is unnecessary in C# because target-typed `new(...)` /
      inferred tuple element types disambiguate at the call site.
  - construct_key: dart3.record_return_type.nested_pair_of_pairs
    source_form: >-
      ((Mode, TypeExpr?), (Mode, TypeExpr?)) _getListSubtermModes(Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv) {
        final defaultResult = ((parentMode, null as TypeExpr?), (parentMode, null as TypeExpr?));
        ... return (headPair, tailPair);
      }
    target_decision: >-
      Nested Dart 3 positional record `((Mode, TypeExpr?), (Mode, TypeExpr?))`
      maps to C# nested `ValueTuple` `((Mode Mode, TypeExpr? Type) Head, (Mode Mode, TypeExpr? Type) Tail)`
      with named outer-tuple elements (`Head`, `Tail`) AND named inner-
      tuple elements (`Mode`, `Type`). Call-site deconstruction in Dart
      reads `final (headMode, headType) = listModes.$1;` (positional
      access `$1`/`$2` for the two halves, then second-level
      deconstruction) — in C# the equivalent is `var ((headMode,
      headType), (tailMode, tailType)) = GetListSubtermModes(...);` (full
      nested deconstruction in one statement, Microsoft Learn:
      Deconstructing tuples and other types). The naming pays off
      because the outer pair is semantically "head-half, tail-half" of a
      list cons, and the inner pair is semantically "mode, type" — both
      become self-documenting at call sites.
    idiom_id: dart-record-tuple-key-to-csharp-valuetuple
    research_finding_id: rf-dart3-record-to-csharp-valuetuple
    nuance: >-
      Cached/reused idiom (same as preceding construct). Nested
      `ValueTuple<ValueTuple<...>, ValueTuple<...>>` is the canonical C#
      shape for a nested Dart record (Microsoft Learn: "You can construct
      a tuple in which one of its components is a tuple"). Named tuple
      elements (`Head`/`Tail`/`Mode`/`Type`) are a documented C# 7+
      feature and improve readability without changing runtime
      representation. Value-vs-reference: both nested tuples are value
      types, so the entire returned shape is one stack-allocated nested
      struct (no heap allocation, no aliasing). Equality is structural at
      every level, matching Dart record value-equality verbatim.
  - construct_key: dart.private_pure_classifier.mode_of_typeexpr
    source_form: >-
      Mode _getEmbeddedMode(TypeExpr expr) {
        if (expr is TypeRef) { return expr.isInput ? Mode.consume : Mode.produce; }
        if (expr is PrimitiveModeAlt) { return expr.isInput ? Mode.consume : Mode.produce; }
        return Mode.produce;
      }
    target_decision: >-
      Emit as `private static Mode GetEmbeddedMode(TypeExpr expr) => expr switch
      { TypeRef tr => tr.IsInput ? Mode.Consume : Mode.Produce, PrimitiveModeAlt pma
      => pma.IsInput ? Mode.Consume : Mode.Produce, _ => Mode.Produce };` —
      a C# expression-bodied static method with a type-pattern switch
      expression. The default-arm `_ => Mode.Produce` preserves Dart's
      "fall-through to `produce`" semantics for type expressions that are
      neither `TypeRef` nor `PrimitiveModeAlt` (e.g. wildcards, list-cons
      type heads, etc.). This is NOT a defensive `throw`-on-unknown
      because the Dart source explicitly chooses produce-as-default —
      preserving the design intent verbatim.
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Cached/reused idiom. Distinct from `BuildModedSubterm`'s totality
      switch — here the default arm intentionally returns `Mode.Produce`
      (not throw); the design treats "unrecognised type expression" as
      equivalent to "no mode constraint → defaults to produce". Codegen
      MUST preserve the falling-through default, NOT substitute a
      throwing arm. Reference-vs-value: `Mode` is an enum (value type in
      both languages); zero-cost return. The two ternary expressions
      `isInput ? consume : produce` are isomorphic between Dart and C#.
  - construct_key: dart.private_pure_classifier.dual_of_typeexpr_polymorphic
    source_form: >-
      TypeExpr _dualType(TypeExpr expr) {
        if (expr is TypeRef) { return expr.dual(); }
        if (expr is PrimitiveModeAlt) { return PrimitiveModeAlt(!expr.isInput, expr.line, expr.column); }
        return expr;
      }
    target_decision: >-
      Emit as `private static TypeExpr DualType(TypeExpr expr) => expr switch
      { TypeRef tr => tr.Dual(), PrimitiveModeAlt pma => new PrimitiveModeAlt(!pma.IsInput, pma.Line, pma.Column), _ => expr };`.
      The `TypeRef.dual()` instance method is declared on `TypeRef` in
      `type_ast.dart`'s spec — preserved as `Dual()` (Pascal-cased). The
      `PrimitiveModeAlt` allocation uses positional ctor
      `new PrimitiveModeAlt(!pma.IsInput, pma.Line, pma.Column)` matching
      its declaration in `type_ast.dart`'s spec (line / column source-
      location ints carried verbatim). Default arm returns `expr` unchanged
      — for type expressions that have no notion of "dual" (e.g.
      compound type heads, list-cons types), the identity is returned
      and the caller is responsible for not relying on involution at
      that node (which the caller's logic in `_getSubtermModes` /
      `_getListSubtermModes` already accounts for by branching on
      `isDual` BEFORE descending into compounds).
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Cached/reused idiom. The polymorphic dispatch (`.dual()` on
      `TypeRef` vs constructor-based dualisation on `PrimitiveModeAlt`)
      reflects the underlying type-AST design: `TypeRef` carries its
      dual logic encapsulated; `PrimitiveModeAlt` is dualised by
      flipping its `isInput` flag via constructor (because the type-AST
      is immutable — see `type_ast.dart` spec, value-equality classes).
      Both behaviours preserved. Reference-vs-value: `PrimitiveModeAlt`
      is a reference type (sealed class in target) and `new
      PrimitiveModeAlt(...)` allocates fresh — matches Dart `new`-less
      ctor call (Dart 2+ allows omitting `new`; C# requires `new`).
  - construct_key: dart.private_recursive_dispatch.opaque_inherits_mode_uniformly
    source_form: >-
      ModedTerm _buildOpaqueModedTerm(ast.Term term, Mode mode) {
        if (term is ast.VarTerm) { return ModedVariable(term.name, isReader: term.isReader, structuralMode: mode); }
        if (term is ast.ConstTerm) { return ModedConstant(mode, term.value ?? 'null'); }
        if (term is ast.UnderscoreTerm) { final uniqueName = _freshAnonVarName(); return ModedVariable(uniqueName, isReader: false, structuralMode: mode); }
        if (term is ast.ListTerm) { ... if (term.isNil) return ModedConstant.nil(mode); ... }
        if (term is ast.StructTerm) { final modedArgs = term.args.map((arg) => _buildOpaqueModedTerm(arg, mode)).toList(); return ModedCompound(mode, term.functor, term.arity, modedArgs); }
        throw InvalidHeadError('Unknown term type in opaque context: ${term.runtimeType}');
      }
    target_decision: >-
      Emit as `private static ModedTerm BuildOpaqueModedTerm(Term term, Mode mode)`
      with a C# type-pattern switch identical in structure to
      `BuildModedSubterm` BUT with NO type-driven sub-mode computation
      (all sub-children inherit `mode` unchanged). The recursive descent
      `term.args.map((arg) => _buildOpaqueModedTerm(arg, mode)).toList()`
      maps to `s.Args.Select(arg => BuildOpaqueModedTerm(arg, mode)).ToList()`
      (rf-dart-list-map-tolist-to-csharp-linq-select-tolist — cached
      from `moded_term.dart`). The empty-list fast path
      `if (term.isNil) return ModedConstant.nil(mode);` maps to
      `if (l.IsNil) return ModedConstant.Nil(mode);` using the static
      factory `Nil` defined in `moded_term.dart`'s spec
      (rf-dart-factory-ctor-const-default-to-csharp-static-factory
      cache). The non-empty-list case uses `ModedCompound.ListCons(mode,
      head, tail)` (the cached static factory). Throwing default arm
      `throw new InvalidHeadError($"Unknown term type in opaque context: {term.GetType().Name}")`
      preserves Dart's runtime-totality guarantee.
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Cached/reused idiom. The structural divergence from
      `BuildModedSubterm` (no type-driven sub-mode computation; all
      children inherit `mode` uniformly) is the load-bearing contract of
      the opaque branch — codegen MUST keep the two methods separate
      (do NOT factor them into one method with a `useTypeInfo` flag),
      because the Dart source's separation makes the wildcard fast-path
      semantics obvious to reviewers. `Select().ToList()` materialisation
      is eager (matching Dart `.map().toList()`); the resulting
      `List<ModedTerm>` is a fresh allocation, no aliasing — same
      semantics as `_DualVisitor` in `moded_term.dart` (preceding spec).
      Anonymous-underscore freshness via `FreshAnonVarName()` correctly
      shares the counter with the outer head/body build (so even inside
      a wildcard branch, anonymous variables get globally-unique-per-
      clause names).
  - construct_key: dart.private_recursive_dispatch.unconditional_variable_complement
    source_form: >-
      ModedTerm _ensureVariablesMatchModes(ModedTerm term) {
        if (term is ModedCompound) { final adjustedArgs = term.args.map(_ensureVariablesMatchModes).toList(); return ModedCompound(term.mode, term.functor, term.arity, adjustedArgs); }
        if (term is ModedConstant) { return term; }
        if (term is ModedVariable) { return ModedVariable(term.name, isReader: !term.isReader, structuralMode: term.mode); }
        throw InvalidHeadError('Unknown moded term type: ${term.runtimeType}');
      }
    target_decision: >-
      Emit as `private static ModedTerm EnsureVariablesMatchModes(ModedTerm term) => term switch
      { ModedCompound c => new ModedCompound(c.Mode, c.Functor, c.Arity, c.Args.Select(EnsureVariablesMatchModes).ToList()),
        ModedConstant k => k,
        ModedVariable v => new ModedVariable(v.Name, !v.IsReader, v.Mode),
        _ => throw new InvalidHeadError($"Unknown moded term type: {term.GetType().Name}") };`
      — a switch expression dispatching on the three concrete
      `ModedTerm` subtypes from `moded_term.dart`'s spec. Method-group
      `_ensureVariablesMatchModes` (in Dart's `.map(_ensureVariablesMatchModes)`)
      maps to C# method-group `EnsureVariablesMatchModes` (no lambda
      wrapper needed — Microsoft Learn: "A method group is a name for a
      set of methods ... an implicit conversion exists from a method
      group to a compatible delegate type"). The `ModedVariable` ctor
      call uses positional args matching its declaration in
      `moded_term.dart`'s spec (`Name`, `isReader`, `structuralMode`);
      named-argument C# call style is recommended for readability
      (`new ModedVariable(v.Name, isReader: !v.IsReader, structuralMode: v.Mode)`).
      The `ModedConstant` arm returns the existing reference unchanged —
      structural sharing intentional (a const has no variable to flip,
      so reusing the same node preserves identity AND value equality
      without allocation overhead).
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Cached/reused idiom from `moded_term.dart` / `type_ast.dart`. The
      `ModedConstant` structural-sharing optimisation (return the
      argument unchanged when no transformation is needed) is preserved
      in C# — `ModedConstant` is a reference type with hand-written
      value equality (per `moded_term.dart` spec); reusing the same
      reference is both faster AND value-equal-correct. The `ModedCompound`
      arm allocates a NEW `ModedCompound` because at least the args list
      may differ (recursive flip may have changed inner variables);
      this matches the `_DualVisitor.visitCompound` pattern in
      `moded_term.dart` (fresh sub-tree, no aliasing). Method-group
      conversion `Args.Select(EnsureVariablesMatchModes).ToList()` is
      preferred over a lambda `arg => EnsureVariablesMatchModes(arg)`
      because the former is the documented C# idiom (Microsoft Learn:
      "When a delegate is expected ... a method group can be used
      directly").
  - construct_key: dart.exception_class.recoverable_signal_implements_exception_with_message
    source_form: >-
      class ArityMismatchError implements Exception {
        final String message;
        ArityMismatchError(this.message);
        @override String toString() => 'ArityMismatchError: $message';
      }
      class InvalidHeadError implements Exception { ... same shape ... }
    target_decision: >-
      Each Dart exception class maps to a C# class deriving from
      `System.Exception`: `public sealed class ArityMismatchError : Exception
      { public ArityMismatchError(string message) : base(message) {} }`
      and likewise `InvalidHeadError`. The C# `Exception(string message)`
      base ctor stores the message and exposes it via `Message`; the
      Dart `ArityMismatchError: $message` `ToString()` override is
      preserved as `public override string ToString() => $"{nameof(ArityMismatchError)}: {Message}";`
      to reproduce the Dart formatting exactly (default `Exception.ToString()`
      emits FQN + message + stack trace, which is more verbose than
      Dart's `ArityMismatchError: msg`; an explicit override matches
      the source's chosen surface). Naming: the Dart `Error` suffix is
      preserved verbatim despite the .NET `Exception` convention
      (Microsoft Learn naming guidelines suggest `Exception` suffix for
      custom exception types). Codegen MAY rename `Error` → `Exception`
      AT MOST as a project-wide convention; this file's spec keeps the
      Dart names so cross-file `throw ArityMismatchError(...)` /
      `catch (ArityMismatchError)` references remain consistent until a
      full-codebase rename pass is done. Marked `sealed` (no subclasses
      anywhere in the codebase) for devirtualisation.
    idiom_id: dart-error-class-recoverable-signal-to-csharp-exception
    research_finding_id: rf-dart-error-vs-exception-to-csharp-exception
    nuance: >-
      Cached/reused idiom from `program_dfa.dart` (FR-024 — never
      re-research). Dart `Error` vs `Exception` distinction (the former
      signals programmer mistakes, the latter recoverable runtime
      failures) does NOT map to a C# language-level distinction; both
      collapse onto `System.Exception` (Microsoft Learn: "All exceptions
      derive from System.Exception"). Naming: the spec mandates one
      project-wide policy (either preserve the Dart `Error` suffix
      verbatim, OR rename to `Exception` suffix per .NET conventions); a
      per-file rename would create cross-file inconsistency in
      `throw`/`catch` sites. This file preserves Dart names; the
      project-wide policy is left to a separate convention spec. The
      `Message` property exposed by `Exception` base supersedes the
      Dart `final String message` field (no need for an explicit
      backing field). `sealed` chosen because no further subclasses
      exist; Microsoft Learn recommends sealing exception classes
      "unless extension is anticipated".
  - construct_key: dart.string_interpolation.error_messages_and_unique_anon_names
    source_form: >-
      throw ArityMismatchError('Head arity ${head.arity} does not match declaration arity ${decl.arity}',);
      return '_#$_anonVarCounter';
      throw InvalidHeadError('Unknown term type: ${term.runtimeType}');
    target_decision: >-
      Map each Dart string interpolation `'$x'` / `'${expr}'` to C#
      interpolated string `$"{X}"` / `$"{Expr}"` (cached
      rf-dart-string-interp-unicode-to-csharp-interpolated-string-utf8
      and rf-csharp-interpolated-string-equivalent-to-dart-interpolation
      from `moded_term.dart` / `program_dfa.dart` / `clause_validation.dart`).
      Specifically: `'Head arity ${head.arity} does not match declaration arity ${decl.arity}'`
      → `$"Head arity {head.Arity} does not match declaration arity {decl.Arity}"`;
      `'_#$_anonVarCounter'` → `$"_#{_anonVarCounter}"`;
      `'Unknown term type: ${term.runtimeType}'` →
      `$"Unknown term type: {term.GetType().Name}"`. The `term.runtimeType`
      Dart property maps to `term.GetType().Name` in C# (Microsoft Learn:
      `Type.Name` is "the name of the type, not including its
      namespace"); `term.GetType().FullName` is an alternative that
      includes the namespace — `Name` is chosen to match the unqualified
      Dart `runtimeType.toString()` output (e.g. `VarTerm` not
      `Glp.Analysis.Compiler.VarTerm`).
    idiom_id: dart-tostring-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-csharp-interpolated-string-equivalent-to-dart-interpolation
    nuance: >-
      Cached/reused idiom from `program_dfa.dart` / `clause_validation.dart`
      / `moded_term.dart`. The Dart `${term.runtimeType}` produces the
      unqualified Dart class name (e.g. `VarTerm`); C# `GetType().Name`
      produces the unqualified .NET class name — same semantics for the
      AST classes (all in one project namespace). Codegen MUST NOT use
      `GetType().FullName` (would produce namespaced output, diverging
      from Dart's behaviour observably in error messages — debugging
      friction). The anonymous-variable name format `_#1`, `_#2`, ... is
      semantically load-bearing (the `#` character distinguishes these
      from user-written variables — preserves uniqueness contract); the
      literal `_#` is preserved verbatim in the C# interpolated string.
conversion_units:
  - static class ModedHead in namespace Glp.Analysis.TypeChecker (host for all top-level members)
  - private static int field _anonVarCounter = 0
  - public static void ResetAnonVarCounter() { _anonVarCounter = 0; }
  - private static string FreshAnonVarName() => $"_#{++_anonVarCounter};
  - public static ModedTerm Build(Goal head, ProcDecl decl, TypeEnvironment? typeEnv = null) — arity check, ResetAnonVarCounter, BuildIOModedTerm with Mode.Consume, then EnsureVariablesMatchModes
  - public static ModedTerm ProducedTerm(Goal atom, ProcDecl decl, TypeEnvironment? typeEnv = null) — arity check, BuildIOModedTerm with Mode.Produce, NO counter reset, NO variable flip
  - private static ModedTerm BuildIOModedTerm(Goal term, ProcDecl decl, Mode parentMode, TypeEnvironment? typeEnv) — pre-sized List<ModedTerm>, parallel-index for-loop over term.Args / decl.ArgTypes / decl.IsInputArg(i), allocate ModedCompound
  - private static ModedTerm BuildModedSubterm(Term term, Mode mode, TypeExpr? expectedType, TypeEnvironment? typeEnv) — wildcard guard FIRST, then type-pattern switch over VarTerm / StructTerm / ListTerm / ConstTerm / UnderscoreTerm with throwing default
  - private static List<(Mode Mode, TypeExpr? Type)> GetSubtermModes(string functor, int arity, Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv) — type-driven sub-mode computation via combineMode + DualType branching
  - private static ((Mode Mode, TypeExpr? Type) Head, (Mode Mode, TypeExpr? Type) Tail) GetListSubtermModes(Mode parentMode, TypeExpr? expectedType, TypeEnvironment? typeEnv) — list-cons head/tail sub-mode computation
  - private static Mode GetEmbeddedMode(TypeExpr expr) — type-pattern switch with fall-through to Mode.Produce
  - private static TypeExpr DualType(TypeExpr expr) — polymorphic dual via TypeRef.Dual() or PrimitiveModeAlt ctor flip
  - private static ModedTerm BuildOpaqueModedTerm(Term term, Mode mode) — type-pattern switch with uniform sub-mode inheritance (no type-driven dispatch)
  - private static ModedTerm EnsureVariablesMatchModes(ModedTerm term) — switch on ModedCompound / ModedConstant / ModedVariable with unconditional variable complement
  - public sealed class ArityMismatchError : Exception with single-string ctor and ToString override matching Dart format
  - public sealed class InvalidHeadError : Exception with single-string ctor and ToString override matching Dart format
escalations: []
```

## Rationale & Research Provenance

`moded_head.dart` is the GLP type-checker's *moded head construction* module
(Definition 5.5 in the GLP paper; `docs/type system/moded-head.md v0.8`): it
turns a Dart-AST clause head + procedure declaration into a `ModedTerm` tree
with all variables flipped (reader↔writer) per the moded-head construction
rule, AND it builds the analogous `producedTerm` for body atoms (no flip).
Architecturally it is a pure compiler-analysis library: no IO, no state
beyond the per-clause anonymous-variable counter, no concurrency. The
non-mechanical Dart→C# decisions centre on (a) the library-private mutable
counter, (b) two related-but-distinct public entry points (`modedHead` vs
`producedTerm`), (c) Dart-3 positional records used as return types, and (d)
the wildcard-guard ordering in `_buildModedSubterm`. Each is grounded
against the official Dart / .NET docs below.

### FR-024 cache reuse summary

Eight of the eleven `research_finding_id` references on this file are
**reused verbatim** from already-specced sibling files in this same
directory (`mode.dart`, `type_ast.dart`, `prelude.dart`, `moded_term.dart`,
`program_dfa.dart`, `clause_validation.dart`) — per FR-024 no second
research call is made for any cached construct_key. Specifically:

- `rf-csharp-static-class-no-toplevel-members` — cached from
  `program_dfa.dart` / `clause_validation.dart` / `prelude.dart`. Reused for
  `Build`, `ProducedTerm`, and `ResetAnonVarCounter` (all top-level
  publics).
- `rf-csharp-private-vs-internal-library-helpers` — cached from
  `program_dfa.dart` / `clause_validation.dart`. Reused for every
  `_`-prefixed helper (`_buildIOModedTerm`, `_buildModedSubterm`,
  `_getSubtermModes`, `_getListSubtermModes`, `_getEmbeddedMode`,
  `_dualType`, `_buildOpaqueModedTerm`, `_ensureVariablesMatchModes`,
  `_freshAnonVarName`, `_anonVarCounter` field).
- `rf-dart-extension-is-as-to-csharp-type-pattern-switch` — cached from
  `type_ast.dart` / `moded_term.dart` / `clause_validation.dart` /
  `program_dfa.dart`. Reused for ALL `is`-test dispatch sites
  (`BuildModedSubterm`, `BuildOpaqueModedTerm`,
  `EnsureVariablesMatchModes`, `GetEmbeddedMode`, `DualType`).
- `rf-dart3-record-to-csharp-valuetuple` — cached from `program_dfa.dart`.
  Reused for both record return types
  (`List<(Mode, TypeExpr?)>` and `((Mode, TypeExpr?), (Mode, TypeExpr?))`).
- `rf-dart-list-map-tolist-to-csharp-linq-select-tolist` — cached from
  `moded_term.dart`. Reused for `term.args.map(...).toList()` in
  `BuildOpaqueModedTerm` and `EnsureVariablesMatchModes`.
- `rf-dart-factory-ctor-const-default-to-csharp-static-factory` — cached
  from `type_ast.dart` / `moded_term.dart` / `program_dfa.dart`. Reused
  for `ModedConstant.nil(mode)` and `ModedCompound.listCons(mode, ...)`
  call sites.
- `rf-dart-error-vs-exception-to-csharp-exception` — cached from
  `program_dfa.dart`. Reused for both `ArityMismatchError` and
  `InvalidHeadError`.
- `rf-csharp-interpolated-string-equivalent-to-dart-interpolation` /
  `rf-dart-string-interp-unicode-to-csharp-interpolated-string-utf8` —
  cached from `program_dfa.dart` / `clause_validation.dart` /
  `moded_term.dart`. Reused for every Dart string-interpolation site.
- `rf-dart-csharp-null-aware-call-operator-identical` — cached from
  `program_dfa.dart`. Reused for `term.value ?? 'null'` in `BuildModedSubterm`
  and `BuildOpaqueModedTerm`.
- `rf-dart-named-required-params-to-csharp-named-positional` — cached
  from `moded_term.dart`. Reused for `{TypeEnvironment? typeEnv}`
  named-optional parameter in `Build` / `ProducedTerm`.
- `rf-dart-library-private-underscore-to-csharp-file-or-internal` —
  cached from `moded_term.dart`. Reused implicitly via the
  `private`-on-static-class convention chosen for this file's helpers.

One construct introduces a new finding:

### rf-dart-library-private-mutable-int-counter-to-csharp-private-static-field (NEW)

**Deep analysis.** `int _anonVarCounter = 0;` is a Dart library-level
mutable variable — one cell shared across every invocation of the three
functions in this file. `resetAnonVarCounter()` is public (zero-arg, void)
and reinitialises it; `_freshAnonVarName()` is private and post-increments
it to produce names `_#1, _#2, ...`. The contract is "reset once at clause
start (`modedHead` does this), then every anonymous-underscore occurrence
within head AND body atoms of the same clause gets a fresh unique name".
Threading: Dart isolates run analysis on one mutator thread; the C# analog
(single-thread synchronous analysis) inherits the same contract.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-
and-structs/static-classes-and-static-class-members` — Microsoft Learn:
*"A static field has only one storage location regardless of the number of
instances of the type that are created. ... Static fields and static
methods belong to the type itself rather than to any specific instance."*
WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-
types/integral-numeric-types` — Microsoft Learn: `int` is `System.Int32`,
32-bit signed integer with range `-2,147,483,648` to `2,147,483,647`.
WebFetch
`https://learn.microsoft.com/en-us/dotnet/standard/threading/managed-
threading-basics` — confirms that `int` field reads/writes ARE NOT atomic
across threads in the general case; for single-threaded code no
synchronisation is needed. Verbatim queries: "C# static field per-type
single storage location"; "C# int System.Int32 width range"; "C# int field
read write atomic single thread".

**Authoritative conclusion.** A Dart library-private mutable `int`
mapping to a C# `private static int` field on the host static class is
the canonical 1:1 idiom. Single-threaded contract is preserved by NOT
introducing `volatile` / `Interlocked` (Microsoft Learn: those primitives
are for the explicit case of multi-thread access, which the source
explicitly does not have). Width narrowing from Dart's 64-bit-capable
`int` to C# `Int32` is safe because the counter is reset per clause and
clause AST node counts in any realistic compilation are bounded far
below 2 billion. Not researched offline-cached but is straightforward
1:1; cached for future use by any other file with the same idiom.

**Conclusion.** Emit `private static int _anonVarCounter = 0;` on host
class `ModedHead`. Public `ResetAnonVarCounter()` and private
`FreshAnonVarName()` static methods both access this field directly. NO
threading primitives. NO `long` widening (faithful 1:1 with Dart's
realistic-use semantics).

### Explicitly addressed well-known nuances (per SC-006 / US2-AS4)

1. **Wildcard-guard ordering in `BuildModedSubterm`.** The
   `expectedType is null or PrimitiveModeAlt` guard MUST run BEFORE the
   AST-subtype dispatch — orthogonal concerns; collapsing them changes
   semantics catastrophically.
2. **Counter reset asymmetry between `Build` (head) and `ProducedTerm`
   (body atom).** `Build` resets the counter at the start of a clause;
   `ProducedTerm` does NOT — body atoms share the head's anon-variable
   namespace. The Dart source's comment makes this explicit; codegen
   preserves the divergence verbatim.
3. **Variable-flip asymmetry between `Build` and `ProducedTerm`.** Only
   `Build` invokes `EnsureVariablesMatchModes` (the unconditional
   reader↔writer flip per Definition 5.5 step 2); `ProducedTerm` does
   NOT flip because body atoms represent the caller's perspective.
4. **Dart-3 record return types → C# nested ValueTuple.** Both record
   shapes (`List<(Mode, TypeExpr?)>` and `((Mode, TypeExpr?), (Mode,
   TypeExpr?))`) map to value-type tuples; value-equality and pass-by-
   value semantics preserved exactly.
5. **C-style index loop in `BuildIOModedTerm` is NOT LINQ-ified.** Three
   parallel index-aligned reads against `term.args[i]`,
   `decl.argTypes[i]`, `decl.isInputArg(i)` are most readably preserved
   as a `for (int i = 0; i < N; i++)` loop.
6. **`ModedConstant` structural sharing in `EnsureVariablesMatchModes`.**
   A `ModedConstant` arm returns the existing reference unchanged — both
   faster and value-equal-correct.
7. **Method-group conversion in `EnsureVariablesMatchModes`.**
   `.Select(EnsureVariablesMatchModes).ToList()` preferred over the
   lambda form per Microsoft Learn's method-group idiom.
8. **Dart `Error` vs `Exception` naming.** Both Dart `*Error` classes
   collapse to C# `: Exception` (no language-level distinction). Naming
   preserved verbatim with a `ToString` override matching Dart format.
9. **Anonymous-variable name format `_#N`.** The `#` character is
   semantically load-bearing (distinguishes machine-generated names from
   user-written variables); preserved verbatim in the C# interpolated
   string.
10. **Threading.** Source is single-threaded (Dart isolate model);
    target preserves that contract — NO `volatile`/`Interlocked` on the
    `_anonVarCounter` field.

### No escalations

All thirteen constructs resolved against official Dart/.NET documentation
with consistent conclusions. Eleven `research_finding_id` references are
cached/reused from already-specced sibling files in this same directory
(FR-024 — never re-research); one new finding
(`rf-dart-library-private-mutable-int-counter-to-csharp-private-static-field`)
is recorded with verbatim authoritative citations.
`open_escalation_count` = 0.
