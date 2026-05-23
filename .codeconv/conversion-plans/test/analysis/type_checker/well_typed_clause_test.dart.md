---
path: test/analysis/type_checker/well_typed_clause_test.dart
cycle_group_id: 109
scc_siblings: []
generated_at: 2026-05-21T16:14:02Z
source_sha256: e31873cea8b664586eb0f7d6e5eb81aaedb3176fb7c579c20ad5ce40d22836c5
schema_version: 1
---

# Conversion Plan: test/analysis/type_checker/well_typed_clause_test.dart

## 1. Source Analysis

The source is a Dart `package:test` unit-test file (287 lines) for the
GLP type-checker's well-typed clause checking (Definition 5.7).

Imports (7 total):
- `package:test/test.dart` — Dart test framework.
- Six SUT imports under `package:glp_runtime/analysis/type_checker/` —
  `well_typed_clause.dart`, `well_typed_term.dart`, `moded_term.dart`,
  `mode.dart`, `program_dfa.dart`, `type_ast.dart`.
- One prefix-import `package:glp_runtime/compiler/ast.dart` as `ast`.

Top-level shape: `void main()` containing a single outer
`group('checkClause', () { ... })`.

Outer-group helpers (eleven local functions defined inside the outer
group's callback closure):
- Arrow-bodied (9): `goal(f, args)`, `writer(name)`, `reader(name)`,
  `nil()`, `cons(h, t)`, `state(name, {isDual, isFinal, isProcedure})`,
  `wildcardProduce()`, `wildcardConsume()`, `streamState({isDual})`.
- Block-bodied (2): `createMergeEnv()` builds a `TypeEnvironment` with a
  `Stream` `TypeDef` and a `merge/3` `ProcDecl`; `createStreamDFA()`
  builds a `ProgramDFA` with three automata (`Stream`, `Stream?`,
  `merge/3`) backed by populated `<(DFAState, TransitionLabel),
  DFAState>` map literals.

Inner groups (5) and test methods (6 total):
- `Condition 1: Head well-typed` — 2 tests (`valid head is well-typed`,
  `undefined procedure fails`).
- `Condition 2: Body atoms well-typed` — 1 test (`valid body atom is
  well-typed`).
- `Condition 3a: Variables in same part need dual types` — 1 test
  (`X at _? and X? at _ in head are dual (well-typed)`).
- `Condition 3b: Variables split across head/body need same type` — 1
  test (`head X at Stream? and body X? at Stream? are same type
  (well-typed)`).
- `Error cases` — 1 test (`wrong arity for procedure fails with
  UndefinedProcedureError`).

Each test follows arrange/act/assert: build `TypeEnvironment` and
`ProgramDFA` (often via the helpers, sometimes inline for the
Condition 3a case which has a bespoke `test/2` procedure), build a
`TypedClause` with named-arg constructor (`head:`, `bodyAtoms:`), call
top-level function `checkClause(clause, dfa, env)`, then assert via
`expect(...)` over the result's properties (`isWellTyped`, `errors`,
`variableTypes`, `modedHead`, `modedBodyAtoms`).

Assertion matchers used (6 distinct forms):
- `isTrue` (3 uses), `isFalse` (2 uses), `isEmpty` (1 use),
  `isNotNull` (1 use), `length, equals(N)` (1 use, N=1),
  `containsKey('X')` and `containsKey('X?')` (2 uses), and
  `result.errors.any((e) => e is UndefinedProcedureError)` composed-
  matcher (2 uses).

Constructor call sites (all default-constructor, implicit-`new`):
`ast.Goal`, `ast.VarTerm`, `ast.ListTerm`, `DFAState`, `TypeDef`,
`ProcDecl`, `TypeRef`, `PrimitiveModeAlt`, `ListNilAlt`, `ListConsAlt`,
`Automaton`, `ProgramDFA`, `TypedClause`. Two named-factory calls:
`TransitionLabel.constant(...)`, `TransitionLabel.functor(...)`,
`TypeEnvironment.empty()`.

Enum references: `Mode.produce`, `Mode.consume`.

No `async`/`Future`/`Stream`; no `setUp`/`tearDown`; no `skip:`; no
`expect(..., throwsA(...))`. All tests synchronous.

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the ratified convspec verbatim
(`.codeconv/conversion-specs/test/analysis/type_checker/well_typed_clause_test.dart.md`).

- `dart.package_test.import_directive`
  → `using Xunit;` at file scope (xUnit pinned project-wide). Codegen
  MUST also add `using System.Linq;` (needed by `Enumerable.Any(...)`
  underlying the `errors.any(...)` predicate assertions, though the
  dedicated `Assert.Contains(IEnumerable<T>, Predicate<T>)` overload
  obviates explicit `.Any` calls) and emit a single namespace
  declaration mirroring `test/analysis/type_checker`
  (e.g. `<RootNs>.Test.Analysis.TypeChecker`). REUSE
  `rf-dart-package-test-import-to-xunit-using` verbatim.

- `dart.package_under_test.import_directive`
  → Collapse the six same-directory SUT imports under
  `analysis/type_checker/` to ONE `using <RootNs>.Analysis.TypeChecker;`.
  The prefix import `import 'package:glp_runtime/compiler/ast.dart' as
  ast;` maps to a C# namespace alias `using ast =
  <RootNs>.Compiler.Ast;` (NOT `using static`, because the file
  references types under the prefix). REUSE
  `rf-dart-internal-package-import-to-csharp-using` verbatim.

- `dart.package_test.main_entrypoint`
  → Eliminate `void main()`. xUnit discovers `[Fact]` methods via
  reflection; the single statement (the outer `group('checkClause', ...)`
  call) becomes the enclosing test class. No constructor /
  `IDisposable.Dispose` emitted (no setUp/tearDown). REUSE
  `rf-dart-package-test-main-omit-in-xunit` verbatim.

- `dart.package_test.group_block`
  → FLATTEN the nested group topology into a single PascalCase xUnit
  class `CheckClauseTests` containing all six test methods. Each
  inner-group label is preserved as
  `[Trait("Group", "<label>")]` on every method belonging to that
  group. Each method name is prefixed with a PascalCased identifier-
  safe form of the inner-group label so cross-group collisions are
  impossible (six concrete names listed in the convspec
  group_block construct). Each test's original Dart label MUST be
  preserved verbatim via `[Fact(DisplayName = "<label>")]`. REUSE
  `rf-dart-package-test-group-to-xunit-class` verbatim.

- `dart.local_arrow_helper_in_group_callback` (9 helpers)
  → Each Dart arrow-bodied local function becomes a `private static`
  expression-bodied method on the test class. The five `ast.*`-
  returning helpers (`Goal`, `Writer`, `Reader`, `Nil`, `Cons`) use the
  `ast` namespace alias. Named optional params
  `{bool isDual = false, bool isFinal = false, bool isProcedure =
  false}` map to C# optional parameters with named-arg syntax
  preserved at all call sites. `null` literals passed to
  `ast.ListTerm(null, null, 0, 0)` in `Nil()` preserve as C# `null`
  literals (the SUT-side `lib/compiler/ast.dart.md` spec decides
  whether `ListTerm` head/tail are `Term?` or `Term` with sentinels;
  this artefact records only the call-site shape). REUSE
  `rf-dart-local-helper-closure-to-csharp-static-method` verbatim.

- `dart.local_block_bodied_helper_in_group_callback` (2 helpers)
  → `CreateMergeEnv()` and `CreateStreamDFA()` each become a
  `private static` block-bodied method on the test class. Dart
  `final env = TypeEnvironment.empty();` maps to C# `var env =
  TypeEnvironment.Empty();`. Dart factory call
  `TypeEnvironment.empty()` maps to C# static factory
  `TypeEnvironment.Empty()` (per the cached
  `rf-dart-factory-ctor-const-default-to-csharp-static-factory`
  idiom). Both helpers capture nothing from the outer scope —
  `private static` is safe. REUSE
  `rf-dart-local-helper-closure-to-csharp-static-method` verbatim.

- `dart.package_test.test_call_simple` (6 tests)
  → Each `test(label, body)` becomes a `public void` instance method
  on the enclosing class, decorated with
  `[Fact(DisplayName = "<label>")]`. The method name is the group-
  prefixed PascalCased label (as listed in the group_block
  construct). All six tests are synchronous; no method is
  `async Task`. REUSE
  `rf-dart-test-callback-to-xunit-method-body` verbatim.

- `dart.constructor_call_with_named_arguments_typedclause` (6 calls)
  → Map `TypedClause(head: ..., bodyAtoms: ...)` to `new
  TypedClause(head: ..., bodyAtoms: ...)` using C# named-arg syntax at
  every call site. The empty `bodyAtoms: []` (5 of 6 calls) maps to
  `new List<ast.Goal>()` (or C# 12 collection-expression `[]`); the
  single-element `bodyAtoms: [ goal('merge', [...]) ]` maps to `new
  List<ast.Goal> { ast.Goal(...) }`. REUSE
  `rf-dart-named-arguments-to-csharp-named-arguments` and
  `rf-dart-list-literal-to-csharp-list-or-collection-expression`
  verbatim.

- `dart.constructor_call_implicit_new` (all other ctor calls)
  → Map every Dart default-ctor call to `new <Type>(...)` with
  identical positional ordering. Concretely: `ast.Goal(f, args, 0, 0)`
  → `new ast.Goal(f, args, 0, 0)`; `ast.VarTerm(name, false, 0, 0)`
  → `new ast.VarTerm(name, false, 0, 0)`; `ast.ListTerm(h, t, 0, 0)`
  → `new ast.ListTerm(h, t, 0, 0)`; `ast.ListTerm(null, null, 0, 0)`
  → `new ast.ListTerm(null, null, 0, 0)`; `DFAState(name,
  isDual: ..., isFinal: ..., isProcedure: ...)` → `new DFAState(name,
  isDual: ..., isFinal: ..., isProcedure: ...)`; `TypeDef('Stream',
  [...], 0, 0)`, `ProcDecl('merge', [...], 0, 0)`, `ProcDecl('test',
  [...], 0, 0)`, `TypeRef('Stream', 0, 0, isInput: true|false)`,
  `PrimitiveModeAlt(true|false, 0, 0)`, `ListNilAlt(0, 0)`,
  `ListConsAlt(PrimitiveModeAlt(false, 0, 0), TypeRef('Stream', 0, 0,
  isInput: false), 0, 0)`, `Automaton(startState, transitions)`,
  `ProgramDFA({...}, {...})` all map analogously. REUSE
  `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`
  verbatim.

- `dart.empty_typed_map_literal_with_record_key` (recorded for
  completeness — does NOT appear in source)
  → No artefact emitted in `WellTypedClauseTest.cs`. REUSE
  `rf-dart-record-type-to-csharp-valuetuple` (mention only).

- `dart.populated_typed_map_literal_with_record_key_and_static_factory_value`
  (4 maps: `streamTransitions`, `streamDualTransitions`,
  `mergeTransitions`, `testTransitions`)
  → Map each Dart `<(DFAState, TransitionLabel), DFAState>{ (k1, k2):
  v, ... }` literal to a C# dictionary collection-initialiser
  `new Dictionary<(DFAState, TransitionLabel), DFAState> { { (k1, k2),
  v }, ... }` (or C# 6+ index-initialiser form `[(a, b)] = v`).
  `TransitionLabel.constant(...)` → `TransitionLabel.Constant(...)`;
  `TransitionLabel.functor(...)` → `TransitionLabel.Functor(...)`;
  `Mode.produce` → `Mode.Produce`; `Mode.consume` → `Mode.Consume`.
  Dart strings `'[]'`, `'[|]'`, `'merge'`, `'test'` map to C#
  `"[]"`, `"[|]"`, `"merge"`, `"test"`. The helper-returned
  `DFAState` instances MUST be assigned ONCE to a `var` local and
  reused as both map-key components and `ProgramDFA` dictionary
  values to preserve object identity. REUSE
  `rf-dart-record-type-to-csharp-valuetuple` verbatim.

- `dart.named_factory_constructor_call` (3 distinct factories,
  multiple call sites)
  → `TransitionLabel.constant(s)` → `TransitionLabel.Constant(s)`,
  `TransitionLabel.functor(f, a, i, mode: m)` →
  `TransitionLabel.Functor(f, a, i, mode: m)`, `TypeEnvironment.empty()`
  → `TypeEnvironment.Empty()`. Named-argument syntax preserved at the
  `mode:` call site. REUSE
  `rf-dart-factory-ctor-const-default-to-csharp-static-factory`
  verbatim.

- `dart.constructor_call_with_positional_list_of_factory_calls`
  (`TypeDef('Stream', [...], 0, 0)`, two `ProcDecl(...)` calls)
  → Map `TypeDef('Stream', [ListNilAlt(0,0), ListConsAlt(...)], 0, 0)`
  to `new TypeDef("Stream", new List<TypeAlt> { new ListNilAlt(0, 0),
  new ListConsAlt(new PrimitiveModeAlt(false, 0, 0), new
  TypeRef("Stream", 0, 0, isInput: false), 0, 0) }, 0, 0)` and
  analogously for the two `ProcDecl(...)` calls (element type is the
  SUT-decided base type `TypeAlt` from `lib/analysis/type_checker/
  type_ast.dart.md`). REUSE
  `rf-dart-list-literal-to-csharp-list-or-collection-expression`
  verbatim.

- `dart.enum_member_reference` (`Mode.produce`, `Mode.consume`)
  → `Mode.Produce`, `Mode.Consume`. REUSE
  `rf-dart-enum-member-pascalcase` verbatim.

- `dart.method_call_top_level_function` (`checkClause(clause, dfa,
  env)`, 6 call sites)
  → `WellTypedClauseChecker.CheckClause(clause, dfa, env)` (or the
  SUT-decided host class name from the forthcoming
  `lib/analysis/type_checker/well_typed_clause.dart.md` spec). REUSE
  `rf-dart-top-level-function-to-csharp-static-class-method`
  verbatim.

- `dart.method_call_instance_void` (`env.addType(...)`,
  `env.addProcedure(...)`)
  → `env.AddType(...)`, `env.AddProcedure(...)` — straight PascalCase
  renaming. Side-effect ordering preserved by sequential statement
  evaluation. REUSE
  `rf-dart-instance-method-camelcase-to-csharp-pascalcase` verbatim.

- `dart.package_test.expect_isTrue` (3 uses)
  → `Assert.True(result.IsWellTyped)`. REUSE
  `rf-dart-expect-istrue-to-xunit-asserttrue` verbatim.

- `dart.package_test.expect_isFalse` (2 uses)
  → `Assert.False(result.IsWellTyped)`. REUSE
  `rf-dart-expect-isfalse-to-xunit-assertfalse` verbatim.

- `dart.package_test.expect_isEmpty_matcher` (1 use)
  → `Assert.Empty(result.Errors)`. REUSE
  `rf-dart-expect-isEmpty-to-xunit-assert-empty` verbatim.

- `dart.package_test.expect_map_containskey` (2 uses)
  → `Assert.Contains("X", result.VariableTypes.Keys)` and
  `Assert.Contains("X?", result.VariableTypes.Keys)` (dedicated
  assertion over `IDictionary<TKey, TValue>.Keys`). REUSE
  `rf-dart-expect-map-containskey-to-xunit-assert-contains-keys`
  verbatim.

- `dart.package_test.expect_iterable_any_with_is_type_check` (2 uses)
  → `Assert.Contains(result.Errors, e => e is
  UndefinedProcedureError)` (predicate overload of `Assert.Contains`
  over `IEnumerable<T>`). REUSE
  `rf-dart-expect-iterable-any-to-xunit-assert-contains-predicate`
  verbatim.

- `dart.package_test.expect_isNotNull` (1 use)
  → `Assert.NotNull(result.ModedHead)`; the assertion also narrows
  the static type to non-null after the call in C# 9+ NRT mode.
  REUSE `rf-dart-expect-isnotnull-to-xunit-assert-notnull` verbatim
  (first appearance across test-side specs; idiom recorded for
  reuse).

- `dart.package_test.expect_property_length_equals` (1 use, N=1)
  → `Assert.Single(result.ModedBodyAtoms)` (dedicated assertion for
  "collection contains exactly one element"). For N>1 the fallback
  is `Assert.Equal(N, coll.Count)`. REUSE
  `rf-dart-expect-length-equals-to-xunit-assert-single-or-count`
  verbatim (first appearance across test-side specs; idiom recorded
  for reuse).

## 3. Decomposed Task Units

- T1: Emit file-scope `using` directives (`Xunit`, `System.Linq`,
  collapsed `<RootNs>.Analysis.TypeChecker`, namespace alias
  `using ast = <RootNs>.Compiler.Ast;`) — done.
- T2: Emit namespace declaration mirroring
  `test/analysis/type_checker` — done.
- T3: Emit `CheckClassTests` class shell from outer group label
  `checkClause` (PascalCased to `CheckClauseTests`) — done.
- T4: Emit nine `private static` arrow-bodied helper methods
  (`Goal`, `Writer`, `Reader`, `Nil`, `Cons`, `State`,
  `WildcardProduce`, `WildcardConsume`, `StreamState`) — done.
- T5: Emit two `private static` block-bodied helper methods
  (`CreateMergeEnv`, `CreateStreamDFA`) — done.
- T6: Emit `[Fact] public void
  Condition1HeadWellTyped_ValidHeadIsWellTyped()` with `[Trait]` and
  `[Fact(DisplayName = "valid head is well-typed")]`; body builds
  env+DFA, constructs `TypedClause(head:..., bodyAtoms: new
  List<ast.Goal>())`, calls
  `WellTypedClauseChecker.CheckClause(clause, dfa, env)`, asserts
  `Assert.True(result.IsWellTyped)` and `Assert.Empty(result.Errors)`
  — done.
- T7: Emit `[Fact] public void
  Condition1HeadWellTyped_UndefinedProcedureFails()` with `[Trait]` /
  `[Fact(DisplayName = "undefined procedure fails")]`;
  `Assert.False(result.IsWellTyped)` and
  `Assert.Contains(result.Errors, e => e is
  UndefinedProcedureError)` — done.
- T8: Emit `[Fact] public void
  Condition2BodyAtomsWellTyped_ValidBodyAtomIsWellTyped()` with
  `[Trait]` / `[Fact(DisplayName = "valid body atom is
  well-typed")]`; `Assert.NotNull(result.ModedHead)` and
  `Assert.Single(result.ModedBodyAtoms)` — done.
- T9: Emit `[Fact] public void
  Condition3aVariablesInSamePartNeedDualTypes_XAtUnderscoreQAndXQAtUnderscoreInHeadAreDualWellTyped()`
  with bespoke inline `test/2` env + DFA;
  `Assert.True(result.IsWellTyped)` plus two
  `Assert.Contains("X", result.VariableTypes.Keys)` /
  `Assert.Contains("X?", result.VariableTypes.Keys)` — done.
- T10: Emit `[Fact] public void
  Condition3bVariablesSplitAcrossHeadBodyNeedSameType_HeadXAtStreamQAndBodyXQAtStreamQAreSameTypeWellTyped()`;
  `Assert.True(result.IsWellTyped)` — done.
- T11: Emit `[Fact] public void
  ErrorCases_WrongArityForProcedureFailsWithUndefinedProcedureError()`;
  `Assert.False(result.IsWellTyped)` and `Assert.Contains(result.Errors,
  e => e is UndefinedProcedureError)` — done.

## 4. Research Findings

none required (all constructs reuse idioms ratified in the convspec
and recorded in peer specs
`test/analysis/type_checker/{moded_head,well_typed_term}_test.dart.md`
and upstream multiagent / smoke-test conversion specs; only two
matchers (`isNotNull` and `length, equals(1)`) make first appearance
in this file and the convspec already records their authoritative
xUnit targets `Assert.NotNull` and `Assert.Single`).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/analysis/type_checker/well_typed_clause_test.dart.md`
(escalations: []) plus peer convspecs
`test/analysis/type_checker/{moded_head,well_typed_term}_test.dart.md`
and CLAUDE.md.

## 6. Escalations

None.
