> Conversion-spec artifact for test/analysis/type_checker/well_typed_clause_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/analysis/type_checker/well_typed_clause_test.dart
source_sha256: e31873cea8b664586eb0f7d6e5eb81aaedb3176fb7c579c20ad5ce40d22836c5
target_code_unit: test/analysis/type_checker/WellTypedClauseTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope (xUnit pinned project-wide;
      identical decision to test/analysis/type_checker/moded_head_test.dart.md
      and well_typed_term_test.dart.md). Codegen MUST also add
      `using System.Linq;` (needed by `Enumerable.Any(...)` underlying the
      `errors.any((e) => e is UndefinedProcedureError)` predicate
      assertions — collapsed to dedicated `Assert.Contains(IEnumerable<T>,
      Predicate<T>)`) and emit a single namespace declaration mirroring the
      Dart directory `test/analysis/type_checker` (e.g.
      `<RootNs>.Test.Analysis.TypeChecker`). REUSE existing idiom verbatim
      (FR-012 / SC-007); no new research.
    idiom_id: null
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is project-wide policy, not file-local
      choice. xUnit was selected because `[Fact]` maps 1:1 onto Dart
      `test(...)` and xUnit's constructor-per-test isolation matches
      `package:test`'s fresh-state semantics (identical rationale to
      well_typed_term_test.dart.md). NUnit and MSTest remain recorded as
      corroborating alternatives, not re-derived here.
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/analysis/type_checker/well_typed_clause.dart';
      import 'package:glp_runtime/analysis/type_checker/well_typed_term.dart';
      import 'package:glp_runtime/analysis/type_checker/moded_term.dart';
      import 'package:glp_runtime/analysis/type_checker/mode.dart';
      import 'package:glp_runtime/analysis/type_checker/program_dfa.dart';
      import 'package:glp_runtime/analysis/type_checker/type_ast.dart';
      import 'package:glp_runtime/compiler/ast.dart' as ast;"
    target_decision: >-
      Map each `package:glp_runtime/analysis/type_checker/<file>.dart`
      import to a `using` directive that names the C# namespace produced
      by converting that SUT file. The six SUT imports under
      `analysis/type_checker/` all live under the same directory and are
      spec'd to share a single C# namespace (per the existing SUT-side
      specs at `.codeconv/conversion-specs/lib/analysis/type_checker/
      {well_typed_term,moded_term,mode,program_dfa,type_ast}.dart.md`, plus
      the future `well_typed_clause.dart.md` to be produced when the SUT
      file is specced); they therefore collapse to ONE C# `using
      <RootNs>.Analysis.TypeChecker;` directive. The Dart prefix import
      `import 'package:glp_runtime/compiler/ast.dart' as ast;` maps to a
      C# namespace alias `using ast = <RootNs>.Compiler.Ast;` so the
      `ast.Goal`, `ast.VarTerm`, `ast.ListTerm`, `ast.Term` references in
      this file resolve via the alias (identical to the alias mechanism
      decided in moded_head_test.dart.md).
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance: Dart `package:` is a pubspec-anchored
      URI per file; C# `using` names a namespace, not a file — so N
      same-directory Dart imports collapse to 1 C# using. The
      prefix-import nuance (Dart `as <name>` -> C# namespace alias
      `using <name> = <Ns>;`, NOT `using static`) is reused verbatim from
      moded_head_test.dart.md — the file references types under the
      prefix (`ast.Goal`, `ast.VarTerm`, `ast.ListTerm`) as TYPES, not as
      static members. Microsoft Learn "Using directive — Using alias
      directive" is the authoritative citation. Project-file (assembly-
      reference) emission remains OUT OF SCOPE for this single-file
      artifact (a langpair-level concern).
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('checkClause', () { ... }); }"
    target_decision: >-
      Eliminate `void main()` entirely. xUnit discovers `[Fact]` methods
      by reflection; there is NO per-file entrypoint to emit. The single
      statement (the outer `group('checkClause', ...)`) becomes the
      enclosing test class.
    idiom_id: null
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance: Dart `main` runs once per test-file process; xUnit
      has no per-file hook. THIS file's `main` body is exactly one
      `group(...)` call with no other statements, so the omission is
      lossless. No `setUp`/`tearDown` exists in this file, so no
      constructor / `IDisposable.Dispose` is emitted. Identical shape to
      well_typed_term_test.dart.md (single outer group), unlike
      moded_head_test.dart.md (three sibling outer groups).
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('checkClause', () { <helpers>; group('Condition 1: Head
      well-typed', ...); group('Condition 2: Body atoms well-typed', ...);
      group('Condition 3a: Variables in same part need dual types', ...);
      group('Condition 3b: Variables split across head/body need same
      type', ...); group('Error cases', ...); });"
    target_decision: >-
      Nested `group` topology: outer `checkClause` + 5 inner groups
      (`Condition 1: Head well-typed`, `Condition 2: Body atoms
      well-typed`, `Condition 3a: Variables in same part need dual
      types`, `Condition 3b: Variables split across head/body need same
      type`, `Error cases`) + 6 `test(...)` leaves total. FLATTEN into a
      single PascalCase xUnit class `CheckClauseTests` containing ALL
      six test methods, with each inner-group label preserved as
      `[Trait("Group", "<label>")]` on every method belonging to that
      group. Per-test method names are prefixed with a PascalCased
      identifier-safe form of the inner-group label so collisions across
      groups are impossible (e.g.
      `Condition1HeadWellTyped_ValidHeadIsWellTyped`,
      `Condition1HeadWellTyped_UndefinedProcedureFails`,
      `Condition2BodyAtomsWellTyped_ValidBodyAtomIsWellTyped`,
      `Condition3aVariablesInSamePartNeedDualTypes_XAtUnderscoreQAndXQAtUnderscoreInHeadAreDualWellTyped`,
      `Condition3bVariablesSplitAcrossHeadBodyNeedSameType_HeadXAtStreamQAndBodyXQAtStreamQAreSameTypeWellTyped`,
      `ErrorCases_WrongArityForProcedureFailsWithUndefinedProcedureError`).
      The original test label MUST be preserved verbatim via
      `[Fact(DisplayName = "<label>")]` so the human-readable sentence
      form survives the conversion.
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Nested-group nuance (present in this file): three viable target
      shapes exist (FLATTEN with [Trait]; nested public classes; one
      [ClassFixture] per inner group). FLATTEN is chosen — identical
      rationale to well_typed_term_test.dart.md: the helpers
      `goal`, `writer`, `reader`, `nil`, `cons`, `state`,
      `wildcardProduce`, `wildcardConsume`, `streamState`,
      `createMergeEnv`, `createStreamDFA` are CLOSURES defined in the
      outer group's scope and read by every inner-group test. FLATTEN
      places them as `private` (or `private static`) helper methods on
      the same test class, avoiding the duplication / base-class costs
      of the nested-class topology. No helper is dead in this file (each
      is reachable from at least one test).
  - construct_key: dart.local_arrow_helper_in_group_callback
    source_form: >-
      "ast.Goal goal(String f, List<ast.Term> args) => ast.Goal(f, args, 0, 0);
       ast.VarTerm writer(String name) => ast.VarTerm(name, false, 0, 0);
       ast.VarTerm reader(String name) => ast.VarTerm(name, true, 0, 0);
       ast.ListTerm nil() => ast.ListTerm(null, null, 0, 0);
       ast.ListTerm cons(ast.Term h, ast.Term t) => ast.ListTerm(h, t, 0, 0);
       DFAState state(String name, {bool isDual = false, bool isFinal = false, bool isProcedure = false})
       => DFAState(name, isDual: isDual, isFinal: isFinal, isProcedure: isProcedure);
       DFAState wildcardProduce() => state('_', isDual: false, isFinal: true);
       DFAState wildcardConsume() => state('_', isDual: true, isFinal: true);
       DFAState streamState({bool isDual = false}) => state('Stream', isDual: isDual, isFinal: false);"
    target_decision: >-
      Each Dart arrow-bodied local function defined inside the outer
      `group` callback becomes a `private static` expression-bodied
      method on the test class (or instance method; `static` is preferred
      because no helper captures `this`). The five `ast.*`-returning
      helpers (`goal`, `writer`, `reader`, `nil`, `cons`) reference the
      `ast` namespace alias resolved at file scope (per the
      package_under_test.import_directive construct above). Named
      optional params `{bool isDual = false, bool isFinal = false, bool
      isProcedure = false}` map to C# optional named parameters per the
      cached rf-dart-named-arguments-to-csharp-named-arguments finding.
      `DFAState(name, isDual: isDual, ...)` (Dart default-constructor
      with named args) maps to `new DFAState(name, isDual: isDual, ...)`
      using C# named-argument syntax at call sites. The Dart `null`
      literals passed to `ast.ListTerm(null, null, 0, 0)` in `nil()`
      preserve as C# `null` literals — the SUT-side spec at
      `.codeconv/conversion-specs/lib/compiler/ast.dart.md` decides
      whether `ListTerm`'s head/tail parameters are `Term?` (nullable
      reference, C# 8+ NRT) or `Term` with documented null sentinels;
      this artifact records only the call-site shape, not the SUT
      signature.
    idiom_id: null
    research_finding_id: rf-dart-local-helper-closure-to-csharp-static-method
    nuance: >-
      Closure-vs-method nuance (explicitly addressed): Dart local
      functions inside `group` callbacks are CLOSURES over the enclosing
      scope; xUnit has no scope-of-group equivalent. Promotion to
      `private static` is safe IFF the local function captures nothing
      (verified: all nine helpers capture nothing — they either reference
      module-scope types `DFAState`, `ast.Goal`, `ast.VarTerm`,
      `ast.ListTerm`, or call other helpers in the same set). Named-
      optional-param nuance: Dart `{bool isDual = false}` is named-only
      with a default; C# `bool isDual = false` is positional-or-named
      with a default — observably equivalent at call sites that use
      named-arg syntax (which the converted call sites MUST use to
      preserve readability). Null-literal nuance for `nil()`: Dart's
      strong-null-safety mode treats `null` arguments as `T?`-typed; C#
      with NRT enabled requires the SUT param to be declared `Term?` to
      accept `null` — addressed in the SUT spec, not here.
  - construct_key: dart.local_block_bodied_helper_in_group_callback
    source_form: >-
      "TypeEnvironment createMergeEnv() {
         final env = TypeEnvironment.empty();
         env.addType(TypeDef('Stream', [...], 0, 0));
         env.addProcedure(ProcDecl('merge', [...], 0, 0));
         return env;
       }
       ProgramDFA createStreamDFA() {
         final prodWild = wildcardProduce();
         ...
         return ProgramDFA({...}, {...});
       }"
    target_decision: >-
      Each Dart block-bodied local function (NOT arrow-bodied) inside
      the outer `group` callback becomes a `private static` block-bodied
      method on the test class — same `private static` decision as the
      arrow helpers above, but with a `{ ... }` body rather than `=> ...`.
      The Dart `final env = TypeEnvironment.empty();` and `final prodWild
      = wildcardProduce();` locals map to C# `var env =
      TypeEnvironment.Empty();` and `var prodWild = WildcardProduce();`
      per the cached rf-dart-final-local-to-csharp-var-local finding.
      The Dart factory call `TypeEnvironment.empty()` maps to C# static
      factory `TypeEnvironment.Empty()` per the cached
      rf-dart-factory-ctor-const-default-to-csharp-static-factory idiom
      (decision recorded in the SUT-side
      `.codeconv/conversion-specs/lib/analysis/type_checker/type_ast.dart.md`
      spec).
    idiom_id: null
    research_finding_id: rf-dart-local-helper-closure-to-csharp-static-method
    nuance: >-
      Block-vs-arrow body nuance (explicitly addressed): Dart syntactic
      forms `() => expr` (expression body) and `() { ... }` (block body)
      both desugar to the same closure value; C# distinguishes
      `T M() => expr;` (expression-bodied method) from `T M() { ... }`
      (block-bodied method) only syntactically — same runtime semantics.
      The closure-vs-method capture analysis from the previous construct
      applies identically: both `createMergeEnv` and `createStreamDFA`
      capture nothing from the outer `group` callback (each builds its
      own local state and calls `TypeEnvironment.empty()` / the arrow
      helpers `wildcardProduce`/`wildcardConsume`/`state`/`streamState`
      which are themselves static), so `private static` is safe.
  - construct_key: dart.package_test.test_call_simple
    source_form: "test('<label>', () { /* arrange (build env + dfa + clause); act (checkClause); assert (expect on result) */ });"
    target_decision: >-
      Each Dart `test(label, body)` with a synchronous closure body and
      no `skip:` argument becomes a `public void` instance method on the
      enclosing class, decorated with `[Fact(DisplayName = "<label>")]`.
      The method name is the group-prefixed PascalCased label (see
      group_block). The closure body converts statement-for-statement
      into the method body. All six `test` calls in this file are
      synchronous (no `async`/`Future`/`Stream`) — no target method is
      `async Task`.
    idiom_id: null
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Async nuance (explicitly addressed even though absent in this
      file): a Dart `test('...', () async { ... })` would target
      `public async Task <Name>()`. Closure-capture nuance: each
      callback captures `goal`, `writer`, `reader`, `nil`, `cons`,
      `createMergeEnv`, `createStreamDFA`, `state` from the outer
      `group` scope; the xUnit translation replaces those captures with
      calls to the same-class `private static` helper methods (see
      previous two constructs) — equivalent in observable behaviour.
  - construct_key: dart.constructor_call_with_named_arguments_typedclause
    source_form: >-
      "TypedClause(
         head: goal('merge', [...]),
         bodyAtoms: [
           goal('merge', [...]),
         ],
       );"
    target_decision: >-
      Dart default-constructor call with named arguments `TypedClause(head:
      ..., bodyAtoms: ...)` maps to C# `new TypedClause(head: ...,
      bodyAtoms: ...)` using the named-argument call syntax. The Dart
      constructor's name-typed parameter conventions are decided in the
      SUT-side spec at
      `.codeconv/conversion-specs/lib/analysis/type_checker/well_typed_clause.dart.md`
      (forthcoming when the SUT file is specced); this artifact records
      only the call-site shape (the `new` prefix, named-arg syntax, and
      `bodyAtoms` -> `bodyAtoms` PascalCasing). PascalCasing for the
      parameter names follows .NET naming guidelines IF the SUT spec
      promotes the parameter to public-API-like prominence; for
      constructor-parameter-only naming, lowerCamelCase is permitted
      (Microsoft Learn .NET Naming Guidelines, Parameter Names section).
      The SUT spec is authoritative.
    idiom_id: null
    research_finding_id: rf-dart-named-arguments-to-csharp-named-arguments
    nuance: >-
      Named-vs-positional nuance (explicitly addressed): Dart `{Type head,
      Type bodyAtoms}` constructor parameters are named-only; C#
      constructor parameters can ALWAYS be passed by name OR position.
      To preserve authoring intent and readability, codegen MUST emit
      named-argument syntax at every `new TypedClause(...)` call site
      across this file (6 calls total). List-literal nuance for
      `bodyAtoms`: the value `[ goal('merge', [...]) ]` is a single-
      element Dart `List<ast.Goal>` literal that maps to C# `new
      List<ast.Goal> { ast.Goal(...) }` (or `new[] { ast.Goal(...) }` if
      the SUT param accepts `IReadOnlyList<ast.Goal>`) per the cached
      rf-dart-list-literal-to-csharp-list-or-collection-expression idiom.
      The empty case `bodyAtoms: []` (5 of the 6 calls) maps to `new
      List<ast.Goal>()` (or `[]` if C# 12 collection-expression is
      enabled — recorded as an alternative in the cached idiom).
  - construct_key: dart.constructor_call_implicit_new
    source_form: >-
      "ast.Goal(f, args, 0, 0); ast.VarTerm(name, false, 0, 0);
       ast.ListTerm(h, t, 0, 0); ast.ListTerm(null, null, 0, 0);
       DFAState(name, isDual: ..., isFinal: ..., isProcedure: ...);
       TypeDef('Stream', [...], 0, 0); ProcDecl('merge', [...], 0, 0);
       ProcDecl('test', [...], 0, 0); ProcDecl('undefined_proc', [...], 0, 0);
       TypeRef('Stream', 0, 0, isInput: true);
       TypeRef('Stream', 0, 0, isInput: false);
       PrimitiveModeAlt(false, 0, 0); PrimitiveModeAlt(true, 0, 0);
       ListNilAlt(0, 0);
       ListConsAlt(PrimitiveModeAlt(false, 0, 0), TypeRef('Stream', 0, 0, isInput: false), 0, 0);
       Automaton(startState, transitions); ProgramDFA({...}, {...});"
    target_decision: >-
      Map every Dart default-constructor call to a C# `new <Type>(...)`
      call with identical positional-argument shape (and named-arg
      preservation where Dart used named args). The SUT-side specs at
      `.codeconv/conversion-specs/lib/{analysis/type_checker/{moded_term,
      program_dfa,type_ast},compiler/ast}.dart.md` decide the C#
      constructor signatures for `ast.Goal`, `ast.VarTerm`,
      `ast.ListTerm`, `DFAState`, `TypeDef`, `ProcDecl`, `TypeRef`,
      `PrimitiveModeAlt`, `ListNilAlt`, `ListConsAlt`, `Automaton`,
      `ProgramDFA`; this artifact records only the call-site shape (the
      `new` prefix and positional ordering are preserved).
    idiom_id: null
    research_finding_id: rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new
    nuance: >-
      Implicit-new nuance (explicitly addressed): Dart 2+ allows omitting
      the `new` keyword at constructor call sites; C# requires `new`
      (target-typed `new()` is an alternative when the target type is
      known from context — Microsoft Learn "Target-typed new expressions"
      — but is OPTIONAL here, classic `new T(...)` is always correct).
      The Dart `final` line-number trailer `(..., 0, 0)` is a
      source-position convention used by the SUT AST/type-AST types; the
      C# positional shape preserves it 1:1.
  - construct_key: dart.empty_typed_map_literal_with_record_key
    source_form: "final transitions = <(DFAState, TransitionLabel), DFAState>{};"
    target_decision: >-
      The file uses populated record-keyed map literals (next construct);
      no empty form occurs. Recorded here for completeness because the
      idiom is decided ONCE for both empty and populated shapes (see
      well_typed_term_test.dart.md). REUSE
      rf-dart-record-type-to-csharp-valuetuple verbatim — no
      re-derivation.
    idiom_id: null
    research_finding_id: rf-dart-record-type-to-csharp-valuetuple
    nuance: >-
      Recorded-for-completeness nuance: this construct does NOT appear
      in the source; it is named here only to keep the idiom-resolution
      audit trail identical to the peer test/analysis/type_checker file.
      Codegen MUST NOT emit any artifact for this construct in
      WellTypedClauseTest.cs.
  - construct_key: dart.populated_typed_map_literal_with_record_key_and_static_factory_value
    source_form: >-
      "final streamTransitions = <(DFAState, TransitionLabel), DFAState>{
        (stream, TransitionLabel.constant('[]')): finalState,
        (stream, TransitionLabel.functor('[|]', 2, 1, mode: Mode.produce)): prodWild,
        (stream, TransitionLabel.functor('[|]', 2, 2, mode: Mode.produce)): stream,
       };
       final streamDualTransitions = <(DFAState, TransitionLabel), DFAState>{
        (streamDual, TransitionLabel.constant('[]')): finalState,
        (streamDual, TransitionLabel.functor('[|]', 2, 1, mode: Mode.consume)): consWild,
        (streamDual, TransitionLabel.functor('[|]', 2, 2, mode: Mode.consume)): streamDual,
       };
       final mergeTransitions = <(DFAState, TransitionLabel), DFAState>{
        (mergeState, TransitionLabel.functor('merge', 3, 1, mode: Mode.consume)): streamDual,
        (mergeState, TransitionLabel.functor('merge', 3, 2, mode: Mode.consume)): streamDual,
        (mergeState, TransitionLabel.functor('merge', 3, 3, mode: Mode.produce)): stream,
       };
       final testTransitions = <(DFAState, TransitionLabel), DFAState>{
        (testState, TransitionLabel.functor('test', 2, 1, mode: Mode.consume)): consWild,
        (testState, TransitionLabel.functor('test', 2, 2, mode: Mode.produce)): prodWild,
       };"
    target_decision: >-
      Map to a C# collection-initializer dictionary literal:
      `var streamTransitions = new Dictionary<(DFAState, TransitionLabel),
      DFAState> { { (stream, TransitionLabel.Constant("[]")), finalState
      }, { (stream, TransitionLabel.Functor("[|]", 2, 1, mode:
      Mode.Produce)), prodWild }, ... };` (or the C# 6+ index-initializer
      form `[ (a, b) ] = v`). Dart static-factory calls
      `TransitionLabel.constant(...)` and `TransitionLabel.functor(...)`
      map to `TransitionLabel.Constant(...)` and
      `TransitionLabel.Functor(...)` (PascalCase), and `Mode.produce` /
      `Mode.consume` map to `Mode.Produce` / `Mode.Consume` per the
      cached SUT-side idioms (see
      `.codeconv/conversion-specs/lib/analysis/type_checker/mode.dart.md`
      and `program_dfa.dart.md`). The literal `'[]'` and `'[|]'` Dart
      strings map to the C# verbatim/regular strings `"[]"` / `"[|]"`
      per rf-dart-raw-string-to-csharp-verbatim-or-escaped (no special
      characters require escaping).
    idiom_id: null
    research_finding_id: rf-dart-record-type-to-csharp-valuetuple
    nuance: >-
      Identity nuance (explicitly addressed): the helper-returned
      `DFAState` values `prodWild`, `consWild`, `stream`, `streamDual`,
      `finalState`, `mergeState`, `testState` appearing as both map key
      components AND `ProgramDFA` values are the SAME object identities
      in Dart (each test assigns the helper result to a `final` local
      that is then used in BOTH the transition map AND the `ProgramDFA`
      construction). The xUnit translation MUST preserve this single-
      instance pattern (one `var prodWild = WildcardProduce(); var stream
      = StreamState();` per `createStreamDFA()` call, reused) — not
      re-call the helpers, which would create distinct instances and
      potentially break value-equality-based dictionary lookups if
      `DFAState` is later re-spec'd to use reference equality. The SUT
      spec at `program_dfa.dart.md` records DFAState's equality
      contract; consumers depend on it. Dictionary-key nuance:
      `ValueTuple<,>` is a struct and its `GetHashCode`/`Equals` are
      value-based — so the populated map literals that key on
      `(DFAState, TransitionLabel)` will lookup-match a freshly
      constructed equal tuple, matching Dart `Map` semantics over record
      keys exactly. Microsoft Learn documents `(T1, T2)` (ValueTuple
      syntax sugar) as the canonical modern tuple form — reused from
      well_typed_term_test.dart.md.
  - construct_key: dart.named_factory_constructor_call
    source_form: >-
      "TransitionLabel.constant('[]');
       TransitionLabel.functor('[|]', 2, 1, mode: Mode.produce);
       TransitionLabel.functor('merge', 3, 1, mode: Mode.consume);
       TypeEnvironment.empty();"
    target_decision: >-
      Dart named-factory constructors `ClassName.factoryName(...)` map
      to C# static factory methods `ClassName.FactoryName(...)` per the
      cached rf-dart-factory-ctor-const-default-to-csharp-static-factory
      finding (recorded in
      lib/analysis/type_checker/{moded_term,program_dfa,type_ast}.dart.md).
      Concretely: `TransitionLabel.constant(s)` ->
      `TransitionLabel.Constant(s)`, `TransitionLabel.functor(f, a, i,
      mode: m)` -> `TransitionLabel.Functor(f, a, i, mode: m)`, and
      `TypeEnvironment.empty()` -> `TypeEnvironment.Empty()`. Named-
      argument syntax MUST be preserved at the call site.
    idiom_id: null
    research_finding_id: rf-dart-factory-ctor-const-default-to-csharp-static-factory
    nuance: >-
      Factory-vs-static-method nuance (explicitly addressed, reused from
      well_typed_term_test.dart.md): Dart `factory` constructors are
      syntactically constructor calls but semantically static-method
      calls returning an instance of the class. C# has no `factory`
      keyword — `public static T Name(...)` is the canonical mapping.
      PascalCase nuance: Dart `camelCase` factory names (`constant`,
      `functor`, `empty`) PascalCase to `Constant`, `Functor`, `Empty`
      per the project-wide identifier-casing idiom recorded in
      `mode.dart.md` and reused project-wide.
  - construct_key: dart.constructor_call_with_positional_list_of_factory_calls
    source_form: >-
      "TypeDef('Stream', [
         ListNilAlt(0, 0),
         ListConsAlt(PrimitiveModeAlt(false, 0, 0), TypeRef('Stream', 0, 0, isInput: false), 0, 0),
       ], 0, 0);
       ProcDecl('merge', [
         TypeRef('Stream', 0, 0, isInput: true),
         TypeRef('Stream', 0, 0, isInput: true),
         TypeRef('Stream', 0, 0, isInput: false),
       ], 0, 0);
       ProcDecl('test', [
         PrimitiveModeAlt(true, 0, 0),
         PrimitiveModeAlt(false, 0, 0),
       ], 0, 0);"
    target_decision: >-
      Map to `new TypeDef("Stream", new List<TypeAlt> { new ListNilAlt(0,
      0), new ListConsAlt(new PrimitiveModeAlt(false, 0, 0), new
      TypeRef("Stream", 0, 0, isInput: false), 0, 0) }, 0, 0)` and
      analogously for `ProcDecl(...)`. The Dart list literals `[...]`
      become C# `new List<T> { ... }` collection-initialisers per the
      cached rf-dart-list-literal-to-csharp-list-or-collection-expression
      idiom. Element type `T` is the SUT-decided base type (`TypeAlt` for
      `TypeDef`'s alts; the `ProcDecl` arg list contains `TypeRef` and
      `PrimitiveModeAlt` — both subtypes of `TypeAlt` per the SUT-side
      type_ast.dart.md spec; the C# list element type MUST be `TypeAlt`,
      not the inferred type).
    idiom_id: null
    research_finding_id: rf-dart-list-literal-to-csharp-list-or-collection-expression
    nuance: >-
      Element-type-inference nuance (explicitly addressed, reused from
      well_typed_term_test.dart.md): Dart infers `List<TypeAlt>` from
      the heterogeneous-looking elements; C# `new List<T> { ... }`
      requires explicit `T` OR a `var` target with explicit generic-arg
      list. Codegen MUST emit `new List<TypeAlt> { ... }` (using the
      type_ast.dart-decided base type) — the inferred-element-type
      shape from Dart does not survive into C#. List-vs-array nuance:
      the SUT ctor signature in type_ast.dart.md decides whether the
      param type is `IReadOnlyList<TypeAlt>` (then `new[] { ... }` works
      via array covariance, OR `[]` collection-expression in C# 12) or
      `List<TypeAlt>` (then `new List<TypeAlt> { ... }` is required);
      this artifact records the call-site shape only.
  - construct_key: dart.enum_member_reference
    source_form: "Mode.produce; Mode.consume;"
    target_decision: >-
      Dart enum-member references `Mode.produce` / `Mode.consume` map
      to C# `Mode.Produce` / `Mode.Consume` per the SUT-side spec at
      `.codeconv/conversion-specs/lib/analysis/type_checker/mode.dart.md`
      (which decides Mode -> `public enum Mode { Produce, Consume }`).
      This artifact reuses that decision verbatim.
    idiom_id: null
    research_finding_id: rf-dart-enum-member-pascalcase
    nuance: >-
      Casing nuance (explicitly addressed, reused from
      well_typed_term_test.dart.md): Dart enum members are
      conventionally `lowerCamelCase`; C# enum members are
      conventionally `PascalCase` (Microsoft Learn Naming Guidelines).
      The SUT-side `mode.dart.md` records this once for the project; all
      test-side references reuse it without re-deriving.
  - construct_key: dart.method_call_top_level_function
    source_form: "checkClause(clause, dfa, env);"
    target_decision: >-
      Dart top-level function `checkClause` maps to a C# `public static`
      method `CheckClause` on a containing static class (the SUT spec at
      `.codeconv/conversion-specs/lib/analysis/type_checker/well_typed_clause.dart.md`
      will decide the host class name, e.g.
      `WellTypedClauseChecker.CheckClause`, when the SUT file is
      specced). The call site becomes
      `WellTypedClauseChecker.CheckClause(clause, dfa, env)`. This
      artifact records only the call-site shape; the SUT spec decides
      the host class.
    idiom_id: null
    research_finding_id: rf-dart-top-level-function-to-csharp-static-class-method
    nuance: >-
      Top-level-function nuance (explicitly addressed, reused from
      well_typed_term_test.dart.md): Dart permits top-level functions;
      C# requires every method to belong to a type. The canonical
      mapping is a `public static class <File>` (or a thematic static
      class chosen at SUT-conversion time) hosting the converted method.
      PascalCase casing is applied to the method name. The cached idiom
      is recorded across all
      `.codeconv/conversion-specs/lib/analysis/type_checker/*` SUT
      specs and is reused here without re-deriving.
  - construct_key: dart.method_call_instance_void
    source_form: "env.addType(TypeDef(...)); env.addProcedure(ProcDecl(...));"
    target_decision: >-
      Dart instance void-method calls `env.addType(...)` and
      `env.addProcedure(...)` map to C# `env.AddType(...)` and
      `env.AddProcedure(...)` — straight PascalCase method-name
      renaming per .NET Naming Guidelines (Microsoft Learn Framework
      Design Guidelines: Names of Methods). The SUT-side spec at
      `.codeconv/conversion-specs/lib/analysis/type_checker/type_ast.dart.md`
      (forthcoming for `TypeEnvironment`) decides the C# instance-method
      signatures; this artifact records only the call-site shape.
    idiom_id: null
    research_finding_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    nuance: >-
      Void-return nuance (explicitly addressed): both `addType` and
      `addProcedure` are statement-level void calls — return value
      ignored. The C# translation is identical: `env.AddType(...);`
      statement, no LINQ chaining, no `void`-returning expression
      placement. Side-effect ordering nuance: the two calls execute
      sequentially in source order; both languages guarantee
      left-to-right statement evaluation, so the order is preserved
      automatically.
  - construct_key: dart.package_test.expect_isTrue
    source_form: "expect(result.isWellTyped, isTrue);"
    target_decision: >-
      Map to xUnit `Assert.True(result.IsWellTyped)`. Used three times
      in this file (the valid-head success test, the Condition 3a
      duality success test, and the Condition 3b same-type success
      test). The Dart property `isWellTyped` is PascalCased to
      `IsWellTyped` per the SUT-side spec at
      `.codeconv/conversion-specs/lib/analysis/type_checker/well_typed_clause.dart.md`
      (which will decide the result-type properties when that SUT file
      is specced; current peer SUT
      `.codeconv/conversion-specs/lib/analysis/type_checker/well_typed_term.dart.md`
      already pins this property as `IsWellTyped`). REUSE
      rf-dart-expect-istrue-to-xunit-asserttrue verbatim
      (FR-012/SC-007).
    idiom_id: null
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      Diagnostic nuance: `Assert.True(b)` without a message produces a
      generic "Assert.True() Failure" — if a converted test needs the
      Dart matcher's richer message, codegen may add the optional
      `userMessage` overload (`Assert.True(b, "<msg>")`); this file's
      three uses are on comprehensible predicates so the bare form
      suffices. Identical decision to well_typed_term_test.dart.md.
  - construct_key: dart.package_test.expect_isFalse
    source_form: "expect(result.isWellTyped, isFalse);"
    target_decision: >-
      Map to xUnit `Assert.False(result.IsWellTyped)`. Used two times
      in this file (the undefined-procedure failure test and the
      wrong-arity error-case test). REUSE
      rf-dart-expect-isfalse-to-xunit-assertfalse from
      well_typed_term_test.dart.md verbatim (FR-012/SC-007), no
      re-derivation.
    idiom_id: null
    research_finding_id: rf-dart-expect-isfalse-to-xunit-assertfalse
    nuance: >-
      Symmetry nuance (explicitly addressed, reused from
      well_typed_term_test.dart.md): the pairing
      `isTrue`/`isFalse` -> `Assert.True`/`Assert.False` is an exact
      1:1 idiom — neither side has an `isNot(isTrue)` ambiguity.
      Diagnostic nuance identical to `isTrue` (above).
  - construct_key: dart.package_test.expect_isEmpty_matcher
    source_form: "expect(result.errors, isEmpty);"
    target_decision: >-
      Map to xUnit `Assert.Empty(result.Errors)`. Used once in this
      file (the Condition 1 valid-head success test). REUSE
      rf-dart-expect-isEmpty-to-xunit-assert-empty verbatim — no
      re-derivation.
    idiom_id: null
    research_finding_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    nuance: >-
      Collection-shape nuance (explicitly addressed, reused from
      well_typed_term_test.dart.md): Dart `isEmpty` matcher accepts
      any object with an `isEmpty` getter; xUnit `Assert.Empty(IEnumerable)`
      accepts only `IEnumerable`. The Dart-side `result.errors` is a
      `List<WellTypedError>`-shaped collection (per the future SUT spec
      for `well_typed_clause.dart`), so `Assert.Empty(result.Errors)` is
      type-safe. Diagnostic nuance: `Assert.Empty` reports element count
      and offending elements on failure — strictly richer than
      `Assert.True(x.Count == 0)`.
  - construct_key: dart.package_test.expect_map_containskey
    source_form: "expect(result.variableTypes.containsKey('X'), isTrue); expect(result.variableTypes.containsKey('X?'), isTrue);"
    target_decision: >-
      Two valid target shapes; CHOSEN: collapse the dedicated matcher
      call to xUnit's `Assert.Contains(<key>, <dict>.Keys)` for
      diagnostic richness. Concretely
      `expect(result.variableTypes.containsKey('X'), isTrue)` ->
      `Assert.Contains("X", result.VariableTypes.Keys)`. Used twice in
      this file (the Condition 3a duality success test asserts both
      `'X'` and `'X?'` keys). REUSE
      rf-dart-expect-map-containskey-to-xunit-assert-contains-keys
      verbatim (FR-012/SC-007).
    idiom_id: null
    research_finding_id: rf-dart-expect-map-containskey-to-xunit-assert-contains-keys
    nuance: >-
      Dedicated-assertion-vs-boolean nuance (explicitly addressed,
      reused from well_typed_term_test.dart.md): the literal-translation
      alternative is `Assert.True(result.VariableTypes.ContainsKey("X"))`,
      but `Assert.Contains(<key>, <enumerable>)` produces a richer
      failure diagnostic (lists the actual keys present, not just a
      boolean). Equality-semantics nuance: `IDictionary<TKey,
      TValue>.Keys` enumerates keys; `Assert.Contains` uses the
      dictionary's `EqualityComparer<TKey>.Default` (string default =
      ordinal), matching Dart `Map<String, ...>.containsKey` which uses
      `String ==` (also ordinal). The two reader/writer key strings
      `'X'` and `'X?'` exercise this — `'X?'` is the reader-suffixed
      variable name, an ordinary string for dictionary purposes.
  - construct_key: dart.package_test.expect_iterable_any_with_is_type_check
    source_form: "expect(result.errors.any((e) => e is UndefinedProcedureError), isTrue);"
    target_decision: >-
      Map to xUnit `Assert.Contains(result.Errors, e => e is
      UndefinedProcedureError)` (the predicate-overload of
      `Assert.Contains` over `IEnumerable<T>` — Microsoft Learn
      `Xunit.Assert.Contains<T>(IEnumerable<T>, Predicate<T>)`). Used
      two times in this file (the undefined-procedure failure test and
      the wrong-arity error-case test). REUSE
      rf-dart-expect-iterable-any-to-xunit-assert-contains-predicate
      from well_typed_term_test.dart.md verbatim (FR-012/SC-007).
    idiom_id: null
    research_finding_id: rf-dart-expect-iterable-any-to-xunit-assert-contains-predicate
    nuance: >-
      Dedicated-assertion nuance (explicitly addressed, reused from
      well_typed_term_test.dart.md): the literal-translation alternative
      is `Assert.True(result.Errors.Any(e => e is
      UndefinedProcedureError))`, but `Assert.Contains(enumerable,
      predicate)` reports the actual contents of the enumerable on
      failure. `is` operator nuance: Dart `e is UndefinedProcedureError`
      and C# `e is UndefinedProcedureError` have the SAME subtype-
      tolerant semantics (Microsoft Learn `is` operator reference). The
      SUT-side spec for `well_typed_clause.dart` (forthcoming) decides
      the C# class name `UndefinedProcedureError` (PascalCased, same as
      Dart); this artifact reuses that decision.
  - construct_key: dart.package_test.expect_isNotNull
    source_form: "expect(result.modedHead, isNotNull);"
    target_decision: >-
      Map to xUnit `Assert.NotNull(result.ModedHead)`. Used once in
      this file (the Condition 2 valid-body-atom test). The Dart property
      `modedHead` is PascalCased to `ModedHead` per the (forthcoming)
      SUT-side spec at
      `.codeconv/conversion-specs/lib/analysis/type_checker/well_typed_clause.dart.md`.
      First appearance of `isNotNull` -> `Assert.NotNull` across the
      test-side conversion specs; idiom recorded for reuse.
    idiom_id: null
    research_finding_id: rf-dart-expect-isnotnull-to-xunit-assert-notnull
    nuance: >-
      Null-safety nuance (explicitly addressed): Dart `expect(x,
      isNotNull)` works on any nullable expression; xUnit
      `Assert.NotNull(object)` accepts any reference and the call also
      narrows the static type to non-null after the assertion in C# 9+
      (Microsoft Learn "Nullable static analysis"). This narrowing is
      stronger than Dart's matcher — the assertion is observably MORE
      useful in C#, not less. Authoritative basis: official xUnit
      assertion comparison table
      (https://xunit.net/docs/comparisons#assertions) lists
      `Assert.NotNull` as the canonical dedicated assertion for "is not
      null"; Dart `isNotNull` matcher is documented at
      https://pub.dev/documentation/matcher/latest/matcher/isNotNull-constant.html.
      Symmetric with `Assert.Null` should it appear in a future test.
  - construct_key: dart.package_test.expect_property_length_equals
    source_form: "expect(result.modedBodyAtoms.length, equals(1));"
    target_decision: >-
      Map to xUnit `Assert.Single(result.ModedBodyAtoms)` because the
      expected count is 1 — `Assert.Single(IEnumerable)` is the
      dedicated assertion for "collection contains exactly one element"
      (Microsoft Learn `Xunit.Assert.Single`). For expected counts > 1,
      `Assert.Equal(<n>, <coll>.Count)` is the fallback. Used once in
      this file (the Condition 2 valid-body-atom test).
    idiom_id: null
    research_finding_id: rf-dart-expect-length-equals-to-xunit-assert-single-or-count
    nuance: >-
      Dedicated-assertion preference (explicitly addressed, same rule
      as containsKey / errors.any): the literal-translation alternative
      is `Assert.Equal(1, result.ModedBodyAtoms.Count)`, but
      `Assert.Single(enumerable)` produces a richer failure diagnostic
      (reports the actual element count and lists the elements when the
      assertion fails). The dedicated-assertion preference matches the
      rule recorded in well_typed_term_test.dart.md
      (containsKey/errors.any). First appearance of
      `expect(coll.length, equals(N))` across the test-side specs;
      idiom is recorded for reuse — codegen MUST select `Assert.Single`
      when N==1 and `Assert.Equal(N, coll.Count)` otherwise. Property
      naming: Dart `length` (List, String, Iterable) -> C# `Count`
      (List, IReadOnlyCollection) or `Length` (Array, string) — for
      `result.modedBodyAtoms` (a `List<ModedGoal>`-shaped value per the
      forthcoming SUT spec), `Count` is the C# property; `Assert.Single`
      bypasses the property entirely and enumerates the collection.
conversion_units:
  - "cu-1: file-scope using directives (Xunit + System.Linq + SUT namespace from glp_runtime/analysis/type_checker + namespace alias `using ast = <RootNs>.Compiler.Ast`)"
  - "cu-2: namespace declaration mirroring test/analysis/type_checker"
  - "cu-3: top-level test class CheckClauseTests (from outer group label 'checkClause')"
  - "cu-4: nine private static helper methods hoisted from the outer group's local-function closures — Goal, Writer, Reader, Nil, Cons, State, WildcardProduce, WildcardConsume, StreamState — plus two block-bodied helpers CreateMergeEnv and CreateStreamDFA"
  - "cu-5: one [Fact] method in the 'Condition 1 — Head well-typed' group (ValidHeadIsWellTyped), [Trait('Group','Condition 1 — Head well-typed')] and [Fact(DisplayName = 'valid head is well-typed')]"
  - "cu-6: one [Fact] method in the 'Condition 1 — Head well-typed' group (UndefinedProcedureFails), [Trait], [Fact(DisplayName = 'undefined procedure fails')]"
  - "cu-7: one [Fact] method in the 'Condition 2 — Body atoms well-typed' group (ValidBodyAtomIsWellTyped), [Trait], [Fact(DisplayName = 'valid body atom is well-typed')], using Assert.NotNull + Assert.Single"
  - "cu-8: one [Fact] method in the 'Condition 3a — Variables in same part need dual types' group (XAtUnderscoreQAndXQAtUnderscoreInHeadAreDualWellTyped), [Trait], [Fact(DisplayName = 'X at _? and X? at _ in head are dual (well-typed)')], using Assert.True + two Assert.Contains (key/Keys) calls"
  - "cu-9: one [Fact] method in the 'Condition 3b — Variables split across head/body need same type' group (HeadXAtStreamQAndBodyXQAtStreamQAreSameTypeWellTyped), [Trait], [Fact(DisplayName = 'head X at Stream? and body X? at Stream? are same type (well-typed)')]"
  - "cu-10: one [Fact] method in the 'Error cases' group (WrongArityForProcedureFailsWithUndefinedProcedureError), [Trait], [Fact(DisplayName = 'wrong arity for procedure fails with UndefinedProcedureError')], using Assert.False + Assert.Contains(enumerable, predicate)"
  - "cu-11: per-method arrange/act/assert body (build env + ProgramDFA + TypedClause; call WellTypedClauseChecker.CheckClause; expect()-> Assert.* routed per the matcher idioms above — IsTrue->True, IsFalse->False, IsEmpty->Empty, IsNotNull->NotNull, length-equals-1->Single, containsKey->Contains(key, Keys), errors.any(is X)->Contains(enumerable, predicate))"
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative)

This file is the eighth `package:test` file specced; xUnit was pinned
project-wide in `test/smoke_test.dart.md` and reused by every subsequent
test spec, most recently `test/analysis/type_checker/{moded_head,
well_typed_term}_test.dart.md`. Maintaining that pin satisfies SC-007
(consistency via recorded idiom, not re-derivation). Authoritative basis
is the xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for the
`[Fact]` / `[Trait]` / constructor-as-setUp model, and the Dart
`package:test` README on `pub.dev` (`https://pub.dev/packages/test`) for
the `group` / `test` / `expect` / matcher semantics.

### Nested-group topology: FLATTEN with `[Trait]`

This file has 1 outer group + 5 inner groups + 6 leaf tests (one in
every inner group except Condition 1, which has two). The same three
target topologies enumerated in `test/multiagent/boot_loader_test.dart.md`
apply; FLATTEN into a single `CheckClauseTests` class with
`[Trait("Group", "...")]` per method is chosen because the nine helper
closures (`goal`, `writer`, `reader`, `nil`, `cons`, `state`,
`wildcardProduce`, `wildcardConsume`, `streamState`) and the two block-
bodied helpers (`createMergeEnv`, `createStreamDFA`) are defined in the
OUTER group's scope and read by every inner-group test — splitting into
per-inner-group classes would force duplicating those helpers or
introducing a shared base class. `[Trait]` is the documented xUnit
mechanism for ad-hoc categorisation
(`https://xunit.net/docs/comparisons#categories`); reporters
(VS Test Explorer, `dotnet test --logger trx`, Rider) render `[Trait]`
groupings, so the human-readable group structure survives.

### Dart prefix import (`import '<uri>' as ast`) -> C# namespace alias

The single prefix import `import 'package:glp_runtime/compiler/ast.dart'
as ast;` maps to a C# `using ast = <RootNs>.Compiler.Ast;` namespace
alias — NOT `using static`, because the file references types under the
prefix (`ast.Goal`, `ast.VarTerm`, `ast.ListTerm`, `ast.Term`) as TYPES,
not as static members. The decision is reused verbatim from
moded_head_test.dart.md, which addresses the same construct;
authoritative basis is Microsoft Learn "Using directive — Using alias
directive" (`https://learn.microsoft.com/dotnet/csharp/language-
reference/keywords/using-directive#using-alias-directive`).

### `TypedClause(head: ..., bodyAtoms: ...)` named-argument constructor

This file uses the Dart `TypedClause` constructor with named arguments
six times. The cached idiom
`rf-dart-named-arguments-to-csharp-named-arguments` (recorded in
moded_head_test.dart.md) maps Dart named-only parameters to C#
named-or-positional parameters with the named-argument-call-site
preservation rule. Microsoft Learn "Named and optional arguments"
(`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-
and-structs/named-and-optional-arguments`) documents the C# named-arg
syntax — both syntactically and semantically a 1:1 match to Dart's
named-arg call syntax. The empty `bodyAtoms: []` literal (5 of 6 calls)
maps to `new List<ast.Goal>()` or C# 12 collection-expression `[]`, per
the cached list-literal idiom.

### `errors.any((e) => e is UndefinedProcedureError)` -> dedicated assert

Two composed Dart `expect(...)` calls collapse to dedicated xUnit
`Assert.Contains(IEnumerable<T>, Predicate<T>)` instead of literal-
translation `Assert.True(<bool>)`. The dedicated-assertion rule is the
same one recorded in `test/multiagent/boot_loader_test.dart.md`
(`rf-dart-expect-isnot-contains-to-xunit-doesnotcontain` nuance),
`test/multiagent/globalize_test.dart.md`, and reused verbatim in
`well_typed_term_test.dart.md`: composed matcher calls SHOULD collapse
to a dedicated xUnit assertion when one exists, because dedicated
assertions produce richer failure diagnostics. Microsoft Learn
`Xunit.Assert.Contains<T>(IEnumerable<T>, Predicate<T>)` documents the
predicate overload.

### `is` type-test operator (Dart and C# are observably identical)

The Condition 1 undefined-procedure test and the Error-cases wrong-arity
test both use `result.errors.any((e) => e is UndefinedProcedureError)`.
Dart `is` and C# `is` both test the runtime type and are SUBTYPE-
TOLERANT (the expression is true for `T` and any subtype). No nuance
survives the conversion. The `UndefinedProcedureError` class is decided
in the SUT-side spec at
`.codeconv/conversion-specs/lib/analysis/type_checker/well_typed_clause.dart.md`
(forthcoming; name preserved verbatim; PascalCase already).

### First appearances: `isNotNull`, `length, equals(N)`

Two test-side matcher constructs make their first appearance in this
file:

- `expect(result.modedHead, isNotNull)` -> `Assert.NotNull(result.ModedHead)`,
  with the additional C# 9+ nullable-static-analysis bonus that the
  static type narrows to non-null after the assertion. Recorded under
  `rf-dart-expect-isnotnull-to-xunit-assert-notnull` for reuse.
  Authoritative basis: official Dart `package:matcher` `isNotNull`
  constant
  (`https://pub.dev/documentation/matcher/latest/matcher/isNotNull-constant.html`)
  and Microsoft Learn `Xunit.Assert.NotNull(object)`
  (`https://xunit.net/docs/comparisons#assertions`).

- `expect(result.modedBodyAtoms.length, equals(1))` ->
  `Assert.Single(result.ModedBodyAtoms)` (dedicated-assertion
  preference; for general N the fallback is `Assert.Equal(N,
  coll.Count)`). Recorded under
  `rf-dart-expect-length-equals-to-xunit-assert-single-or-count` for
  reuse. Authoritative basis: official Dart `package:matcher` `equals`
  (`https://pub.dev/documentation/matcher/latest/matcher/equals.html`)
  and Microsoft Learn `Xunit.Assert.Single(IEnumerable)` /
  `Xunit.Assert.Equal<T>(T, T)`
  (`https://xunit.net/docs/comparisons#assertions`).

### Why no escalations

Every construct has a clear, single-decision target shape grounded in
official documentation for both Dart `package:test`/`package:matcher`
and xUnit / .NET / Microsoft Learn. The three "soft" decisions (xUnit
vs NUnit/MSTest, FLATTEN vs nested classes, dedicated-assertion vs
literal `Assert.True(<bool>)`) are documented project-wide policy with
corroborating alternatives in their research findings, not unresolved
choices. The overwhelming majority of constructs reuse already-cached
idioms verbatim (FR-012/SC-007) from
`test/analysis/type_checker/{moded_head,well_typed_term}_test.dart.md`
and the upstream multiagent and smoke-test conversion specs. Only two
new `rf-*` findings are introduced (`isNotNull` and
`length-equals-1->Assert.Single`), each backed by a single
authoritative xUnit target. `escalations: []` is therefore intentional,
not a placeholder.
