---
path: test/analysis/type_checker/well_typed_term_test.dart
cycle_group_id: 110
scc_siblings: []
generated_at: 2026-05-21T16:01:19Z
source_sha256: 35b279ae85fe3b9fbf1d952650226551efbab338b524d4171ed9f51fbaf0518c
schema_version: 1
---

# Conversion Plan: test/analysis/type_checker/well_typed_term_test.dart

## 1. Source Analysis

The file is a Dart `package:test` unit-test suite for well-typed moded
term checking. Inspection of the actual `.dart` (186 lines) confirms:

- File header comment block citing `docs/type system/well-typed-term.md`
  and "Paper: Definition 5.4".
- Five import directives:
  - `package:test/test.dart`
  - `package:glp_runtime/analysis/type_checker/well_typed_term.dart`
  - `package:glp_runtime/analysis/type_checker/moded_term.dart`
  - `package:glp_runtime/analysis/type_checker/mode.dart`
  - `package:glp_runtime/analysis/type_checker/program_dfa.dart`
- One `void main()` entrypoint whose body is a single outer
  `group('checkModedTerm', ...)` call.
- Inside that outer group:
  - Six arrow-bodied local helper functions: `state`, `wildcardProduce`,
    `wildcardConsume`, `integerState`, `streamState`, `finalState`.
    `streamState` and `finalState` are defined but never called.
  - Four nested `group(...)` blocks:
    1. `'constant at primitive type position'` — 2 tests
       (`'integer constant at Integer type is well-typed'`,
       `'string constant at Integer type fails'`).
    2. `'variable at wildcard position'` — 2 tests
       (`'writer at _ (produce wildcard) is well-typed'`,
       `'reader at _? (consume wildcard) is well-typed'`).
    3. `'variable pair duality'` — 2 tests
       (`'writer type _ and reader type _? are dual'`,
       `'same type for both writer and reader fails duality'`).
    4. `'path consistency'` — 1 test
       (`'no transition for functor fails'`).
  - Six `test(...)` leaves in total.
- Each test body follows arrange / act / assert: build `DFAState`(s) via
  helpers, build a transitions map literal of type
  `<(DFAState, TransitionLabel), DFAState>{}` (Dart 3 record-keyed),
  build an `Automaton`, build a `ProgramDFA`, build a `ModedConstant` /
  `ModedVariable` / `ModedCompound` term, call top-level
  `checkModedTerm(term, automaton, dfa)`, then `expect(...)` with one or
  more matchers.
- Matchers used: `isTrue` (3x), `isFalse` (3x), `isEmpty` (1x),
  `isNotEmpty` (1x), `result.variableTypes.containsKey(...)` checked via
  `expect(..., isTrue)` (3x — one in the X-only test, one in the X?-only
  test, two in the duality test), and
  `expect(result.errors.any((e) => e is InconsistentPathError), isTrue)`
  (1x).
- Named-factory calls: `ModedVariable.writer(...)`,
  `ModedVariable.reader(...)`, `TransitionLabel.functor(...)`.
- Default constructor calls: `Automaton(...)`, `ProgramDFA(...)`,
  `ModedConstant(...)`, `ModedCompound(...)`, `DFAState(...)`.
- Enum-member references: `Mode.produce`, `Mode.consume`.
- All six tests are synchronous — no `async`/`Future`/`Stream`.
- No `setUp`/`tearDown`/`setUpAll`/`tearDownAll`.

## 2. Dart → C#/.NET Conversion Plan

Per convspec (mirror — verbatim decisions; the `→` below is U+2192):

- `import 'package:test/test.dart';` → `using Xunit;` (xUnit pinned
  project-wide; convspec construct
  `dart.package_test.import_directive`). Codegen MUST additionally emit
  `using System.Linq;` (needed for `Enumerable.Any` lineage if the
  literal-translation fallback ever appears) and a namespace declaration
  mirroring `test/analysis/type_checker` (e.g.
  `<RootNs>.Test.Analysis.TypeChecker`).
- Four `package:glp_runtime/analysis/type_checker/<file>.dart` imports
  → ONE `using <RootNs>.Analysis.TypeChecker;` directive (same-directory
  SUT files share a single C# namespace, per convspec construct
  `dart.package_under_test.import_directive`).
- `void main() { group('checkModedTerm', ...); }` → ELIMINATED entirely;
  the outer `group` becomes the enclosing test class (convspec construct
  `dart.package_test.main_entrypoint`).
- Outer + four inner `group(...)` blocks → FLATTEN into a single xUnit
  class `CheckModedTermTests` containing all six test methods, each
  decorated with `[Trait("Group", "<inner-group-label>")]` and
  `[Fact(DisplayName = "<original-test-label>")]`. Test-method names
  are group-prefixed PascalCased to prevent collisions across groups
  (convspec construct `dart.package_test.group_block`).
- Six arrow-bodied local helpers → six `private static`
  expression-bodied methods on the test class:
  - `DFAState State(string name, bool isDual = false, bool isFinal = false)`
  - `DFAState WildcardProduce()`
  - `DFAState WildcardConsume()`
  - `DFAState IntegerState(bool isDual = false)`
  - `DFAState StreamState(bool isDual = false)` (DEAD — codegen MAY omit,
    SHOULD keep, per convspec nuance; non-blocking)
  - `DFAState FinalState()` (DEAD — same treatment)

  Named-optional params `{bool isDual = false, bool isFinal = false}` →
  C# optional named params `bool isDual = false, bool isFinal = false`
  (convspec construct `dart.local_arrow_helper_in_group_callback`).
- Each `test('<label>', () { ... })` → `[Fact(DisplayName = "<label>")]
  public void <GroupPrefix>_<MethodName>() { ... }` (convspec construct
  `dart.package_test.test_call_simple`). No `async Task` — all six tests
  are synchronous.
- `<(DFAState, TransitionLabel), DFAState>{}` (empty) →
  `new Dictionary<(DFAState, TransitionLabel), DFAState>()` (convspec
  construct `dart.empty_typed_map_literal_with_record_key`). C# tuple
  syntax `(T1, T2)` (ValueTuple sugar) — NOT `Tuple<T1, T2>`.
- Populated record-keyed map literal → C# dictionary collection-
  initialiser `new Dictionary<(DFAState, TransitionLabel), DFAState> {
  { (compoundState, TransitionLabel.Functor("test", 2, 1, mode:
  Mode.Produce)), prodWild }, ... }` (convspec construct
  `dart.populated_typed_map_literal_with_record_key_and_static_factory_value`).
  Single-instance identity for `compoundState`/`prodWild`/`consWild`
  MUST be preserved.
- `Automaton(startState, transitions)` →
  `new Automaton(startState, transitions)` (convspec construct
  `dart.constructor_call_default_with_positional`). Implicit-new
  prepended.
- `ProgramDFA({'Integer': startState}, {'Integer': automaton})` →
  `new ProgramDFA(new Dictionary<string, DFAState> { { "Integer",
  startState } }, new Dictionary<string, Automaton> { { "Integer",
  automaton } })` (same construct; map-literal-as-positional-arg
  nuance).
- `ModedVariable.writer('X', structuralMode: Mode.produce)` →
  `ModedVariable.Writer("X", structuralMode: Mode.Produce)`;
  `ModedVariable.reader(...)` → `ModedVariable.Reader(...)`;
  `TransitionLabel.functor('test', 2, 1, mode: Mode.produce)` →
  `TransitionLabel.Functor("test", 2, 1, mode: Mode.Produce)` (convspec
  construct `dart.named_factory_constructor_call`).
- `ModedCompound(Mode.consume, 'test', 2, [ <factory calls> ])` →
  `new ModedCompound(Mode.Consume, "test", 2, new List<ModedTerm> {
  <factory calls> })` (convspec construct
  `dart.constructor_call_with_positional_list_of_factory_calls`).
- `Mode.produce` / `Mode.consume` → `Mode.Produce` / `Mode.Consume`
  (convspec construct `dart.enum_member_reference`).
- `checkModedTerm(term, automaton, dfa)` →
  `WellTypedTermChecker.CheckModedTerm(term, automaton, dfa)` (convspec
  construct `dart.method_call_top_level_function`; the host class name
  is decided in the SUT-side spec
  `.codeconv/conversion-specs/lib/analysis/type_checker/well_typed_term.dart.md`).
- `expect(result.isWellTyped, isTrue)` →
  `Assert.True(result.IsWellTyped)` (convspec construct
  `dart.package_test.expect_isTrue`).
- `expect(result.isWellTyped, isFalse)` →
  `Assert.False(result.IsWellTyped)` (convspec construct
  `dart.package_test.expect_isFalse`).
- `expect(result.errors, isEmpty)` →
  `Assert.Empty(result.Errors)` (convspec construct
  `dart.package_test.expect_isEmpty_matcher`).
- `expect(result.errors, isNotEmpty)` →
  `Assert.NotEmpty(result.Errors)` (convspec construct
  `dart.package_test.expect_isNotEmpty_matcher`).
- `expect(result.variableTypes.containsKey('X'), isTrue)` →
  `Assert.Contains("X", result.VariableTypes.Keys)` (convspec construct
  `dart.package_test.expect_map_containskey`).
- `expect(result.errors.any((e) => e is InconsistentPathError), isTrue)`
  → `Assert.Contains(result.Errors, e => e is InconsistentPathError)`
  (predicate-overload of `Assert.Contains` over `IEnumerable<T>`;
  convspec construct
  `dart.package_test.expect_iterable_any_with_is_type_check`).

Conversion-units inventory (mirror cu-1 … cu-9 from convspec):

- cu-1: file-scope `using` directives (`Xunit`, `System.Linq`, SUT
  namespace from `glp_runtime/analysis/type_checker`).
- cu-2: namespace declaration mirroring `test/analysis/type_checker`.
- cu-3: top-level test class `CheckModedTermTests` (from outer group
  `'checkModedTerm'`).
- cu-4: six `private static` helper methods (`State`, `WildcardProduce`,
  `WildcardConsume`, `IntegerState`, `StreamState`, `FinalState`)
  hoisted from the outer group's local-function closures.
- cu-5: two `[Fact]` methods in `'constant at primitive type position'`
  group (`IntegerConstantAtIntegerTypeIsWellTyped`,
  `StringConstantAtIntegerTypeFails`), each
  `[Trait("Group", "constant at primitive type position")]` and
  `[Fact(DisplayName = "<original label>")]`.
- cu-6: two `[Fact]` methods in `'variable at wildcard position'` group
  (`WriterAtUnderscoreProduceWildcardIsWellTyped`,
  `ReaderAtUnderscoreQConsumeWildcardIsWellTyped`), each
  `[Trait("Group", "variable at wildcard position")]`.
- cu-7: two `[Fact]` methods in `'variable pair duality'` group
  (`WriterTypeUnderscoreAndReaderTypeUnderscoreQAreDual`,
  `SameTypeForBothWriterAndReaderFailsDuality`), each
  `[Trait("Group", "variable pair duality")]`.
- cu-8: one `[Fact]` method in `'path consistency'` group
  (`NoTransitionForFunctorFails`), `[Trait("Group", "path consistency")]`,
  uses `Assert.Contains(IEnumerable, predicate)` for the
  `InconsistentPathError` type-test assertion.
- cu-9: per-method arrange/act/assert body (build automaton +
  `ProgramDFA` + term; call `WellTypedTermChecker.CheckModedTerm`;
  `expect(...)` → `Assert.*` per matcher idioms above).

## 3. Decomposed Task Units

- T1: Emit file-scope `using` directives (cu-1) — `using Xunit;`, `using
  System.Linq;`, `using <RootNs>.Analysis.TypeChecker;`.
- T2: Emit namespace declaration mirroring `test/analysis/type_checker`
  (cu-2).
- T3: Emit the test class `CheckModedTermTests` skeleton (cu-3) with a
  `public` modifier and (implicit) parameterless constructor.
- T4: Emit six `private static` helper methods (cu-4) translating the
  Dart arrow-bodied local functions: `State`, `WildcardProduce`,
  `WildcardConsume`, `IntegerState`, `StreamState` (DEAD-but-kept),
  `FinalState` (DEAD-but-kept).
- T5: Emit two `[Fact]` test methods for the
  `'constant at primitive type position'` group (cu-5) including
  per-test arrange/act/assert bodies.
- T6: Emit two `[Fact]` test methods for the
  `'variable at wildcard position'` group (cu-6) including per-test
  arrange/act/assert bodies; assertions include
  `Assert.Contains(<key>, result.VariableTypes.Keys)`.
- T7: Emit two `[Fact]` test methods for the
  `'variable pair duality'` group (cu-7) including per-test
  arrange/act/assert bodies; the success test asserts BOTH
  `Assert.Contains("X", result.VariableTypes.Keys)` and
  `Assert.Contains("X?", result.VariableTypes.Keys)`.
- T8: Emit the one `[Fact]` test method for the `'path consistency'`
  group (cu-8) including the
  `Assert.Contains(result.Errors, e => e is InconsistentPathError)`
  predicate-overload call.
- T9: Translate all map-literal call sites (transitions dictionaries,
  state-name dictionaries, automaton dictionaries) to C# collection-
  initialiser dictionaries with the correct generic-arg types
  (`<(DFAState, TransitionLabel), DFAState>`, `<string, DFAState>`,
  `<string, Automaton>`).
- T10: Translate every Dart constructor/factory call site
  (`Automaton(...)`, `ProgramDFA(...)`, `ModedConstant(...)`,
  `ModedCompound(...)`, `ModedVariable.writer/reader(...)`,
  `TransitionLabel.functor(...)`, `DFAState(...)`) per the convspec
  decisions in §2 — preserving single-instance identity for locals
  used as both map keys and ProgramDFA values.

## 4. Research Findings

none required — every decision in §2 is verbatim from the ratified
convspec, which itself cites cached/derived research findings
(`rf-dart-package-test-import-to-xunit-using`,
`rf-dart-internal-package-import-to-csharp-using`,
`rf-dart-package-test-main-omit-in-xunit`,
`rf-dart-package-test-group-to-xunit-class`,
`rf-dart-local-function-arrow-to-csharp-static-helper`,
`rf-dart-test-callback-to-xunit-method-body`,
`rf-dart-record-type-to-csharp-valuetuple`,
`rf-dart-default-ctor-call-to-csharp-new`,
`rf-dart-factory-ctor-const-default-to-csharp-static-factory`,
`rf-dart-list-literal-to-csharp-list`,
`rf-dart-enum-member-pascalcase`,
`rf-dart-top-level-function-to-csharp-static-class-method`,
`rf-dart-expect-istrue-to-xunit-asserttrue`,
`rf-dart-expect-isfalse-to-xunit-assertfalse`,
`rf-dart-expect-isEmpty-to-xunit-assert-empty`,
`rf-dart-expect-isNotEmpty-to-xunit-assert-notempty`,
`rf-dart-expect-map-containskey-to-xunit-assert-contains-keys`,
`rf-dart-expect-iterable-any-to-xunit-assert-contains-predicate`).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/analysis/type_checker/well_typed_term_test.dart.md`
(the ratified convspec is the mirror; all §2 decisions are verbatim).
Cross-file SUT references (host class `WellTypedTermChecker`, the
`InconsistentPathError` class name, the `Mode`/`ModedVariable`/
`ModedCompound`/`TransitionLabel`/`Automaton`/`ProgramDFA`/`DFAState`
types) are decided in the SUT-side specs at
`.codeconv/conversion-specs/lib/analysis/type_checker/{well_typed_term,
moded_term,mode,program_dfa}.dart.md`; this plan defers to them per
convspec policy.

## 6. Escalations

None.
