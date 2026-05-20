> Conversion-spec artifact for test/analysis/type_checker/well_typed_term_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/analysis/type_checker/well_typed_term_test.dart
source_sha256: 35b279ae85fe3b9fbf1d952650226551efbab338b524d4171ed9f51fbaf0518c
target_code_unit: test/analysis/type_checker/WellTypedTermTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope (xUnit pinned project-wide; same
      decision as test/smoke_test.dart.md, test/multiagent/boot_loader_test.dart.md,
      test/multiagent/globalize_test.dart.md). Codegen MUST also add
      `using System.Linq;` (needed for `Enumerable.Any(...)` used by the
      InconsistentPathError predicate assertion below) and a single
      namespace declaration mirroring the Dart `test/analysis/type_checker`
      directory (e.g. `<RootNs>.Test.Analysis.TypeChecker`). REUSE existing
      idiom verbatim (FR-012 / SC-007); no new research.
    idiom_id: null
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is project-wide policy, not file-local
      choice. xUnit was selected because `[Fact]` maps 1:1 onto Dart
      `test(...)` and xUnit's constructor-per-test isolation matches
      `package:test`'s fresh-state semantics. NUnit and MSTest remain
      recorded as corroborating alternatives, not re-derived here.
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/analysis/type_checker/well_typed_term.dart';
      import 'package:glp_runtime/analysis/type_checker/moded_term.dart';
      import 'package:glp_runtime/analysis/type_checker/mode.dart';
      import 'package:glp_runtime/analysis/type_checker/program_dfa.dart';"
    target_decision: >-
      Map each `package:glp_runtime/analysis/type_checker/<file>.dart`
      import to a `using` directive that names the C# namespace produced
      by converting that SUT file (e.g.
      `using <RootNs>.Analysis.TypeChecker;`). Because the four imported
      SUT files all live under the same directory and are spec'd to share
      a single C# namespace (per the SUT-side specs at
      `.codeconv/conversion-specs/lib/analysis/type_checker/{well_typed_term,
      moded_term,mode,program_dfa}.dart.md`), the four Dart imports collapse
      to ONE C# `using` directive. The SUT namespace string is decided when
      `well_typed_term.dart` itself is converted; this artifact records only
      the shape of the cross-file dependency.
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance: Dart `package:` is a pubspec-anchored
      URI per file; C# `using` names a namespace, not a file — so N
      same-directory Dart imports collapse to 1 C# using. No `as` alias /
      partial import is used in this file, so the simple `using <Ns>;`
      form suffices. Project-file (assembly-reference) emission is OUT OF
      SCOPE for this single-file artifact (a langpair-level concern).
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('checkModedTerm', () { ... }); }"
    target_decision: >-
      Eliminate `void main()` entirely. xUnit discovers `[Fact]` methods by
      reflection; there is NO per-file entrypoint to emit. The single
      statement (the outer `group('checkModedTerm', ...)`) becomes the
      enclosing test class.
    idiom_id: null
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance: Dart `main` runs once per test-file process; xUnit
      has no per-file hook. THIS file's `main` body is exactly one
      `group(...)` call with no other statements, so the omission is
      lossless. No `setUp`/`tearDown` exists in this file, so no
      constructor / `IDisposable.Dispose` is emitted.
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('checkModedTerm', () { <helpers>; group('constant at primitive
      type position', ...); group('variable at wildcard position', ...);
      group('variable pair duality', ...); group('path consistency', ...); });"
    target_decision: >-
      Nested `group` topology: outer `checkModedTerm` + 4 inner groups
      (`constant at primitive type position`, `variable at wildcard
      position`, `variable pair duality`, `path consistency`) +
      6 `test(...)` leaves total. FLATTEN into a single PascalCase xUnit
      class `CheckModedTermTests` containing ALL six test methods, with
      each inner-group label preserved as `[Trait("Group", "<label>")]`
      on every method belonging to that group. Per-test method names are
      prefixed with a PascalCased identifier-safe form of the inner-group
      label so collisions across groups are impossible (e.g.
      `ConstantAtPrimitiveTypePosition_IntegerConstantAtIntegerTypeIsWellTyped`,
      `VariablePairDuality_WriterTypeUnderscoreAndReaderTypeUnderscoreQAreDual`,
      `PathConsistency_NoTransitionForFunctorFails`). The original test
      label MUST be preserved verbatim via `[Fact(DisplayName = "<label>")]`
      so the human-readable sentence form survives the conversion.
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Nested-group nuance (present in this file): three viable target
      shapes exist (FLATTEN with [Trait]; nested public classes; one
      [ClassFixture] per inner group). FLATTEN is chosen — identical
      rationale to boot_loader_test.dart.md: the helpers `state`,
      `wildcardProduce`, `wildcardConsume`, `integerState`,
      `streamState`, `finalState` are CLOSURES defined in the outer
      group's scope and read by every inner-group test. FLATTEN places
      them as `private` (or `private static`) helper methods on the same
      test class, avoiding the duplication/base-class costs of the
      nested-class topology. The `streamState` and `finalState` helpers
      are DEAD in this file (defined, never called) — codegen MAY omit
      them but SHOULD keep them (lossless, audit-friendly); recorded as
      a non-blocking nuance, not an escalation.
  - construct_key: dart.local_arrow_helper_in_group_callback
    source_form: >-
      "DFAState state(String name, {bool isDual = false, bool isFinal = false})
       => DFAState(name, isDual: isDual, isFinal: isFinal);
       DFAState wildcardProduce() => state('_', isDual: false, isFinal: true);
       DFAState wildcardConsume() => state('_', isDual: true, isFinal: true);
       DFAState integerState({bool isDual = false})
       => state('Integer', isDual: isDual, isFinal: true);
       DFAState streamState({bool isDual = false})
       => state('Stream', isDual: isDual, isFinal: false);
       DFAState finalState() => state('_FINAL_', isDual: false, isFinal: true);"
    target_decision: >-
      Each Dart arrow-bodied local function defined inside the outer
      `group` callback becomes a `private static` expression-bodied method
      on the test class (or instance method; `static` is preferred because
      no helper captures `this`). Named optional params `{bool isDual =
      false, bool isFinal = false}` map to C# optional named parameters
      `bool isDual = false, bool isFinal = false` per the cached
      rf-dart-named-required-params-to-csharp-named-positional finding.
      `DFAState(name, isDual: isDual, ...)` (Dart default-constructor with
      named args) maps to `new DFAState(name, isDual: isDual, ...)` using
      C# named-argument syntax at call sites.
    idiom_id: null
    research_finding_id: rf-dart-local-function-arrow-to-csharp-static-helper
    nuance: >-
      Closure-vs-method nuance (explicitly addressed): Dart local
      functions inside `group` callbacks are CLOSURES over the enclosing
      scope; xUnit has no scope-of-group equivalent. Promotion to
      `private static` is safe IFF the local function captures nothing
      (verified: `state`, `wildcardProduce`, `wildcardConsume`,
      `integerState`, `streamState`, `finalState` capture nothing — they
      either reference enum constants `Mode.produce`/`Mode.consume` or
      call other helpers in the same set). Named-optional-param nuance:
      Dart `{bool isDual = false}` is named-only with a default; C#
      `bool isDual = false` is positional-or-named with a default —
      observably equivalent at call sites that already use named-arg
      syntax (which the converted call sites MUST use to preserve
      readability and match Dart's call-site shape).
  - construct_key: dart.package_test.test_call_simple
    source_form: "test('<label>', () { /* arrange (build automaton + dfa + term); act (checkModedTerm); assert (expect on result) */ });"
    target_decision: >-
      Each Dart `test(label, body)` with a synchronous closure body and no
      `skip:` argument becomes a `public void` instance method on the
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
      callback captures `state`/`wildcardProduce`/... from the outer
      `group` scope; the xUnit translation replaces those captures with
      calls to the same-class `private static` helper methods (see
      previous construct) — equivalent in observable behaviour.
  - construct_key: dart.empty_typed_map_literal_with_record_key
    source_form: "final transitions = <(DFAState, TransitionLabel), DFAState>{};"
    target_decision: >-
      Dart 3 record-keyed typed empty map literal maps to a C# explicit
      `IDictionary<,>` instantiation with a `ValueTuple` key type:
      `var transitions = new Dictionary<(DFAState, TransitionLabel),
      DFAState>();`. Empty-literal form `<K, V>{}` -> `new Dictionary<K,
      V>()`. Codegen MUST emit the C# tuple syntax `(DFAState,
      TransitionLabel)` (sugar for `ValueTuple<DFAState, TransitionLabel>`)
      — NOT `Tuple<DFAState, TransitionLabel>` (reference-type, distinct
      identity/equality).
    idiom_id: null
    research_finding_id: rf-dart-record-type-to-csharp-valuetuple
    nuance: >-
      Record-vs-ValueTuple nuance (load-bearing, explicitly addressed):
      Dart 3 positional records `(T1, T2)` and C# `ValueTuple<T1, T2>`
      both provide value-equality and a structural hash; both are
      stack-allocatable; both support tuple-literal syntax `(a, b)`.
      Microsoft Learn documents `(T1, T2)` (ValueTuple syntax sugar) as
      the canonical modern tuple form. Reusing the cached
      rf-dart-record-type-to-csharp-valuetuple idiom from
      test/multiagent/localize_test.dart.md (FR-012/SC-007 reuse, no
      re-research). Dictionary-key nuance: `ValueTuple<,>` is a struct
      and its `GetHashCode`/`Equals` are value-based — so populated map
      literals (next construct) that key on `(DFAState, TransitionLabel)`
      will lookup-match a freshly constructed equal tuple, matching
      Dart `Map` semantics over record keys exactly.
  - construct_key: dart.populated_typed_map_literal_with_record_key_and_static_factory_value
    source_form: >-
      "final transitions = <(DFAState, TransitionLabel), DFAState>{
        (compoundState, TransitionLabel.functor('test', 2, 1, mode: Mode.produce)): prodWild,
        (compoundState, TransitionLabel.functor('test', 2, 2, mode: Mode.consume)): consWild,
      };"
    target_decision: >-
      Map to a C# collection-initializer dictionary literal:
      `var transitions = new Dictionary<(DFAState, TransitionLabel),
      DFAState> { { (compoundState, TransitionLabel.Functor("test", 2, 1,
      mode: Mode.Produce)), prodWild }, { (compoundState,
      TransitionLabel.Functor("test", 2, 2, mode: Mode.Consume)),
      consWild } };` (or the C# 6+ index-initializer form `[ (a, b) ] =
      v`). The Dart static-factory call `TransitionLabel.functor(...)`
      maps to `TransitionLabel.Functor(...)` (PascalCase), and `Mode.produce`
      / `Mode.consume` map to `Mode.Produce` / `Mode.Consume` per the
      cached SUT-side idioms (see
      `.codeconv/conversion-specs/lib/analysis/type_checker/mode.dart.md`
      and `program_dfa.dart.md`).
    idiom_id: null
    research_finding_id: rf-dart-record-type-to-csharp-valuetuple
    nuance: >-
      Identity nuance (explicitly addressed): the `compoundState` and
      `prodWild`/`consWild` values appearing as both map key components
      AND ProgramDFA values are the SAME object identities in Dart (the
      helper functions return fresh instances per call, but each test
      assigns them to a `final` local that is then used in BOTH the
      transition map AND the ProgramDFA construction). The xUnit
      translation MUST preserve this single-instance pattern (one `var
      compoundState = ...; var prodWild = ...;` per test, reused) — not
      re-call the helpers, which would create distinct instances and
      potentially break value-equality-based dictionary lookups if
      `DFAState` is later re-spec'd to use reference equality. The SUT
      spec at `program_dfa.dart.md` records DFAState's equality
      contract; consumers depend on it.
  - construct_key: dart.constructor_call_default_with_positional
    source_form: "Automaton(startState, transitions); ProgramDFA({'Integer': startState}, {'Integer': automaton});"
    target_decision: >-
      Map Dart default constructor calls `Automaton(...)` and
      `ProgramDFA(...)` to C# `new Automaton(...)` and `new ProgramDFA(...)`
      with identical positional-argument shape. The Dart-side specs
      (`.codeconv/conversion-specs/lib/analysis/type_checker/program_dfa.dart.md`)
      decide the C# constructor signatures for these types; this
      artifact records only the call-site shape (the `new` prefix and
      positional ordering are preserved).
    idiom_id: null
    research_finding_id: rf-dart-default-ctor-call-to-csharp-new
    nuance: >-
      Implicit-new nuance (explicitly addressed): Dart 2+ allows
      omitting the `new` keyword at constructor call sites; C# requires
      `new`. Codegen MUST prepend `new`. Argument-evaluation nuance:
      both languages evaluate arguments left-to-right before the call —
      no behavioural difference. Map-literal-as-positional-arg nuance:
      `ProgramDFA({'Integer': startState}, {'Integer': automaton})`
      passes two map literals as positional args; in C# the call site
      becomes `new ProgramDFA(new Dictionary<string, DFAState> {
      { "Integer", startState } }, new Dictionary<string, Automaton> {
      { "Integer", automaton } })` — the inferred C# generic arguments
      are determined by the constructor signatures in program_dfa.cs
      (decided in the SUT spec, not here).
  - construct_key: dart.named_factory_constructor_call
    source_form: >-
      "ModedVariable.writer('X', structuralMode: Mode.produce);
       ModedVariable.reader('X', structuralMode: Mode.consume);
       TransitionLabel.functor('test', 2, 1, mode: Mode.produce);"
    target_decision: >-
      Dart named-factory constructors `ClassName.factoryName(...)` map
      to C# static factory methods `ClassName.FactoryName(...)` per the
      cached rf-dart-factory-ctor-const-default-to-csharp-static-factory
      finding (recorded in lib/analysis/type_checker/moded_term.dart.md
      and lib/analysis/type_checker/type_ast.dart.md). Concretely:
      `ModedVariable.writer(name, structuralMode: m)` ->
      `ModedVariable.Writer(name, structuralMode: m)`, and
      `TransitionLabel.functor(f, a, i, mode: m)` ->
      `TransitionLabel.Functor(f, a, i, mode: m)`. Named-argument syntax
      MUST be preserved at the call site.
    idiom_id: null
    research_finding_id: rf-dart-factory-ctor-const-default-to-csharp-static-factory
    nuance: >-
      Factory-vs-static-method nuance (explicitly addressed): Dart
      `factory` constructors are syntactically constructor calls but
      semantically static-method calls returning an instance of the
      class (with optional caching, subtype-returning, etc.). C# has no
      `factory` keyword — `public static T Name(...)` is the canonical
      mapping. PascalCase nuance: Dart `camelCase` factory names
      (`writer`, `reader`, `functor`) PascalCase to `Writer`, `Reader`,
      `Functor` per the project-wide identifier-casing idiom recorded
      in `mode.dart.md` and reused project-wide.
  - construct_key: dart.constructor_call_with_positional_list_of_factory_calls
    source_form: >-
      "ModedCompound(Mode.consume, 'test', 2, [
         ModedVariable.writer('X', structuralMode: Mode.produce),
         ModedVariable.reader('X', structuralMode: Mode.consume),
       ]);"
    target_decision: >-
      Map to `new ModedCompound(Mode.Consume, "test", 2, new List<ModedTerm>
      { ModedVariable.Writer("X", structuralMode: Mode.Produce),
      ModedVariable.Reader("X", structuralMode: Mode.Consume) })` (or
      `new[] { ... }` if the SUT signature accepts `IReadOnlyList<ModedTerm>`
      via covariant array — decided in moded_term.dart.md). The Dart
      list literal `[...]` becomes a C# `new List<T> { ... }` collection-
      initialiser per the cached rf-dart-list-literal-to-csharp-list
      idiom.
    idiom_id: null
    research_finding_id: rf-dart-list-literal-to-csharp-list
    nuance: >-
      Element-type-inference nuance (explicitly addressed): Dart infers
      `List<ModedTerm>` from the heterogeneous-looking elements (both
      `ModedVariable.writer(...)` and `ModedVariable.reader(...)` return
      `ModedVariable`, a subtype of `ModedTerm`); C# `new List<T> { ... }`
      requires explicit `T` OR a `var` target with explicit generic-arg
      list. Codegen MUST emit `new List<ModedTerm> { ... }` (using the
      moded_term.dart-decided base type) — the inferred-element-type
      pattern from Dart does not survive into C#. List-vs-array nuance:
      the SUT ctor signature in moded_term.dart.md decides whether the
      param type is `IReadOnlyList<ModedTerm>` (then `new[] { ... }`
      works) or `List<ModedTerm>` (then `new List<ModedTerm> { ... }` is
      required); this artifact records the call-site shape only.
  - construct_key: dart.enum_member_reference
    source_form: "Mode.produce; Mode.consume;"
    target_decision: >-
      Dart enum-member references `Mode.produce` / `Mode.consume` map
      to C# `Mode.Produce` / `Mode.Consume` per the SUT-side spec at
      `.codeconv/conversion-specs/lib/analysis/type_checker/mode.dart.md`
      (which decides Mode → `public enum Mode { Produce, Consume }`).
      This artifact reuses that decision verbatim.
    idiom_id: null
    research_finding_id: rf-dart-enum-member-pascalcase
    nuance: >-
      Casing nuance (explicitly addressed): Dart enum members are
      conventionally `lowerCamelCase`; C# enum members are
      conventionally `PascalCase` (Microsoft Learn Naming Guidelines).
      The SUT-side `mode.dart.md` records this once for the project; all
      test-side references reuse it without re-deriving.
  - construct_key: dart.method_call_top_level_function
    source_form: "checkModedTerm(term, automaton, dfa);"
    target_decision: >-
      Dart top-level function `checkModedTerm` maps to a C# `public
      static` method `CheckModedTerm` on a containing static class (the
      SUT spec at `.codeconv/conversion-specs/lib/analysis/type_checker/
      well_typed_term.dart.md` decides the host class name, e.g.
      `WellTypedTermChecker.CheckModedTerm`). The call site becomes
      `WellTypedTermChecker.CheckModedTerm(term, automaton, dfa)`.
      This artifact records only the call-site shape; the SUT spec
      decides the host class.
    idiom_id: null
    research_finding_id: rf-dart-top-level-function-to-csharp-static-class-method
    nuance: >-
      Top-level-function nuance (explicitly addressed): Dart permits
      top-level functions; C# requires every method to belong to a
      type. The canonical mapping is a `public static class <File>` (or
      a thematic static class chosen at SUT-conversion time) hosting
      the converted method. PascalCase casing is applied to the method
      name. The cached idiom is recorded across all
      `.codeconv/conversion-specs/lib/analysis/type_checker/*` SUT
      specs and is reused here without re-deriving.
  - construct_key: dart.package_test.expect_isTrue
    source_form: "expect(result.isWellTyped, isTrue);"
    target_decision: >-
      Map to xUnit `Assert.True(result.IsWellTyped)`. Used three times
      in this file (the two well-typed-success tests and the duality-
      success test). The Dart property `isWellTyped` is PascalCased to
      `IsWellTyped` per the SUT-side spec at
      `.codeconv/conversion-specs/lib/analysis/type_checker/well_typed_term.dart.md`
      (which decides the result-type's three properties as
      `IsWellTyped` / `VariableTypes` / `Errors`).
    idiom_id: null
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      Diagnostic nuance: `Assert.True(b)` without a message produces a
      generic "Assert.True() Failure" — if a converted test needs the
      Dart matcher's richer message, codegen may add the optional
      `userMessage` overload (`Assert.True(b, "<msg>")`); this file's
      three uses are on comprehensible predicates so the bare form
      suffices. No first-time research — cached idiom reused.
  - construct_key: dart.package_test.expect_isFalse
    source_form: "expect(result.isWellTyped, isFalse);"
    target_decision: >-
      Map to xUnit `Assert.False(result.IsWellTyped)`. Used three
      times in this file (`string constant at Integer type fails`,
      `same type for both writer and reader fails duality`, and
      `no transition for functor fails`). The Dart matcher `isFalse` is
      symmetric to `isTrue` — both reduce to the single-argument
      `Assert.True`/`Assert.False` xUnit assertions.
    idiom_id: null
    research_finding_id: rf-dart-expect-isfalse-to-xunit-assertfalse
    nuance: >-
      Symmetry nuance (explicitly addressed): the pairing
      `isTrue`/`isFalse` -> `Assert.True`/`Assert.False` is an exact
      1:1 idiom — neither side has an `isNot(isTrue)` ambiguity. First
      appearance of `isFalse` across the test-side conversion specs;
      idiom is recorded for reuse in subsequent test specs. Diagnostic
      nuance identical to `isTrue` (above).
  - construct_key: dart.package_test.expect_isEmpty_matcher
    source_form: "expect(result.errors, isEmpty);"
    target_decision: >-
      Map to xUnit `Assert.Empty(result.Errors)`. Used once in this
      file (the integer-at-Integer success test). Reuses the cached
      idiom from test/multiagent/globalize_test.dart.md and
      localize_test.dart.md — no re-derivation (FR-012/SC-007).
    idiom_id: null
    research_finding_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    nuance: >-
      Collection-shape nuance (explicitly addressed): Dart `isEmpty`
      matcher accepts any object with an `isEmpty` getter (Iterable,
      String, Map, etc.); xUnit `Assert.Empty(IEnumerable)` accepts
      only `IEnumerable`. The Dart-side `result.errors` is a
      `List<WellTypedError>` (per well_typed_term.dart's SUT spec), so
      `Assert.Empty(result.Errors)` is type-safe. Diagnostic nuance:
      `Assert.Empty` reports element count and offending elements on
      failure — strictly richer than `Assert.True(x.Count == 0)`,
      which is why the dedicated matcher is preferred.
  - construct_key: dart.package_test.expect_isNotEmpty_matcher
    source_form: "expect(result.errors, isNotEmpty);"
    target_decision: >-
      Map to xUnit `Assert.NotEmpty(result.Errors)`. Used once in
      this file (the string-at-Integer failure test). Symmetric to
      `isEmpty` -> `Assert.Empty`. First appearance of `isNotEmpty`
      across the test-side conversion specs; idiom is recorded for
      reuse in subsequent specs.
    idiom_id: null
    research_finding_id: rf-dart-expect-isNotEmpty-to-xunit-assert-notempty
    nuance: >-
      Symmetry nuance (explicitly addressed): the pairing
      `isEmpty`/`isNotEmpty` -> `Assert.Empty`/`Assert.NotEmpty` is an
      exact 1:1 mapping. The Dart-side matcher composition
      `isNot(isEmpty)` would map to the SAME `Assert.NotEmpty` (per
      the dedicated-assertion rule recorded in
      test/multiagent/boot_loader_test.dart.md's
      `rf-dart-expect-isnot-contains-to-xunit-doesnotcontain` nuance:
      composed `isNot(matcher)` collapses to a dedicated xUnit
      assertion, not a generic `Assert.False(...)`). Collection-shape
      nuance: identical to the `isEmpty` construct above — type-safe
      because `result.errors` is a `List<WellTypedError>`.
  - construct_key: dart.package_test.expect_map_containskey
    source_form: "expect(result.variableTypes.containsKey('X'), isTrue);"
    target_decision: >-
      Two valid target shapes; CHOSEN: collapse the dedicated matcher
      call to xUnit's `Assert.Contains(<key>, <dict>.Keys)` for
      diagnostic richness. Concretely
      `expect(result.variableTypes.containsKey('X'), isTrue)` ->
      `Assert.Contains("X", result.VariableTypes.Keys)`. Used three
      times in this file (the two wildcard tests assert one key each,
      the duality success test asserts BOTH `'X'` and `'X?'` keys).
    idiom_id: null
    research_finding_id: rf-dart-expect-map-containskey-to-xunit-assert-contains-keys
    nuance: >-
      Dedicated-assertion-vs-boolean nuance (explicitly addressed):
      the literal-translation alternative is
      `Assert.True(result.VariableTypes.ContainsKey("X"))`, but
      `Assert.Contains(<key>, <enumerable>)` produces a richer failure
      diagnostic (lists the actual keys present, not just a boolean
      "false"). Dedicated-assertion preference matches the rule
      recorded in boot_loader_test.dart.md and globalize_test.dart.md.
      First appearance of `containsKey` -> `Assert.Contains(key, Keys)`
      across the test-side specs; idiom is recorded for reuse.
      Equality-semantics nuance: `IDictionary<TKey, TValue>.Keys`
      enumerates keys; `Assert.Contains` uses the dictionary's
      `EqualityComparer<TKey>.Default` (string default = ordinal),
      matching Dart `Map<String, ...>.containsKey` which uses `String
      ==` (also ordinal). The two reader/writer key strings `'X'` and
      `'X?'` in the duality test exercise this — `'X?'` is the
      reader-suffixed variable name, an ordinary string for dictionary
      purposes.
  - construct_key: dart.package_test.expect_iterable_any_with_is_type_check
    source_form: "expect(result.errors.any((e) => e is InconsistentPathError), isTrue);"
    target_decision: >-
      Map to xUnit `Assert.Contains(result.Errors, e => e is
      InconsistentPathError)` (the predicate-overload of
      `Assert.Contains` over `IEnumerable<T>` — Microsoft Learn
      `Xunit.Assert.Contains<T>(IEnumerable<T>, Predicate<T>)`).
      The Dart `is` type-test operator maps 1:1 to the C# `is`
      operator. The Dart `Iterable.any(predicate)` -> C#
      `Enumerable.Any(predicate)` mapping is the cached LINQ idiom;
      however the COMPOSED `expect(x.any(p), isTrue)` collapses to
      the dedicated `Assert.Contains(<enumerable>, <predicate>)` for
      diagnostic richness — same dedicated-assertion preference as
      `containsKey` above and `isNot(contains(...))` in
      boot_loader_test.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-expect-iterable-any-to-xunit-assert-contains-predicate
    nuance: >-
      Dedicated-assertion nuance (explicitly addressed): the literal-
      translation alternative is `Assert.True(result.Errors.Any(e => e
      is InconsistentPathError))`, but `Assert.Contains(enumerable,
      predicate)` reports the actual contents of the enumerable on
      failure, which is strictly more useful than a bare boolean.
      `is` operator nuance: Dart `e is InconsistentPathError` is true
      iff `e`'s runtime type IS or extends `InconsistentPathError`
      (subtype-tolerant); C# `e is InconsistentPathError` has the
      SAME subtype-tolerant semantics (Microsoft Learn `is` operator
      reference). The SUT-side spec for `well_typed_term.dart` decides
      the C# class name `InconsistentPathError` (PascalCased, same
      as Dart); this artifact reuses that decision. First appearance
      of `expect(x.any(...), isTrue)` -> `Assert.Contains(x, predicate)`
      across the test-side specs; idiom recorded for reuse.
conversion_units:
  - cu-1: file-scope using directives (Xunit + System.Linq + SUT namespace from glp_runtime/analysis/type_checker)
  - cu-2: namespace declaration mirroring test/analysis/type_checker
  - cu-3: top-level test class CheckModedTermTests (from outer group label "checkModedTerm")
  - cu-4: six private static helper methods (State, WildcardProduce, WildcardConsume, IntegerState, StreamState, FinalState) hoisted from the outer group's local-function closures
  - cu-5: two [Fact] methods in the "constant at primitive type position" group (IntegerConstantAtIntegerTypeIsWellTyped, StringConstantAtIntegerTypeFails), each [Trait("Group", "constant at primitive type position")] and [Fact(DisplayName = "<original label>")]
  - cu-6: two [Fact] methods in the "variable at wildcard position" group (WriterAtUnderscoreProduceWildcardIsWellTyped, ReaderAtUnderscoreQConsumeWildcardIsWellTyped), each [Trait("Group", "variable at wildcard position")]
  - cu-7: two [Fact] methods in the "variable pair duality" group (WriterTypeUnderscoreAndReaderTypeUnderscoreQAreDual, SameTypeForBothWriterAndReaderFailsDuality), each [Trait("Group", "variable pair duality")]
  - cu-8: one [Fact] method in the "path consistency" group (NoTransitionForFunctorFails), [Trait("Group", "path consistency")], using Assert.Contains(IEnumerable, predicate) for the InconsistentPathError type-test assertion
  - cu-9: per-method arrange/act/assert body (build automaton + ProgramDFA + term; call WellTypedTermChecker.CheckModedTerm; expect()->Assert.* routed per the matcher idioms above — IsTrue->True, IsFalse->False, IsEmpty->Empty, IsNotEmpty->NotEmpty, containsKey->Contains(key, Keys), errors.any(is X)->Contains(enumerable, predicate))
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative)

This file is the seventh `package:test` file specced; xUnit was pinned
project-wide in `test/smoke_test.dart.md` and reused by
`test/glp_runtime_test.dart.md`, `test/multiagent/{boot_loader,globalize,
localize,global_writers_table,mad_error_handling}_test.dart.md`. Maintaining
that pin satisfies SC-007 (consistency via recorded idiom, not
re-derivation). Authoritative basis is the xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for the
`[Fact]` / `[Trait]` / constructor-as-setUp model, and the Dart
`package:test` README on `pub.dev` (`https://pub.dev/packages/test`) for
the `group` / `test` / `expect` / matcher semantics.

### Nested-group topology: FLATTEN with `[Trait]`

This file has 1 outer group + 4 inner groups + 6 leaf tests. The same
three target topologies enumerated in
`test/multiagent/boot_loader_test.dart.md` apply; FLATTEN into a single
`CheckModedTermTests` class with `[Trait("Group", "...")]` per method is
chosen because the six helper closures (`state`, `wildcardProduce`,
`wildcardConsume`, `integerState`, `streamState`, `finalState`) are
defined in the OUTER group's scope and read by every inner-group test —
splitting into per-inner-group classes would force duplicating those
helpers or introducing a shared base class. `[Trait]` is the documented
xUnit mechanism for ad-hoc categorisation
(`https://xunit.net/docs/comparisons#categories`); reporters
(VS Test Explorer, `dotnet test --logger trx`, Rider) render `[Trait]`
groupings, so the human-readable group structure survives. Test-method
names are group-prefixed to prevent collisions across groups.

### Dart 3 record-keyed map literal -> ValueTuple-keyed Dictionary

The map literal `<(DFAState, TransitionLabel), DFAState>{}` (and its
populated variants in the two compound-term tests) uses Dart 3
positional records as keys. Reusing the cached
`rf-dart-record-type-to-csharp-valuetuple` idiom from
`test/multiagent/localize_test.dart.md`: Microsoft Learn documents
`(T1, T2)` (ValueTuple syntax sugar) as the canonical modern tuple
form; `ValueTuple<,>` is a struct with value-based
`Equals`/`GetHashCode`, so its use as a `Dictionary` key matches Dart's
record-as-map-key semantics exactly. Reference-type `Tuple<T1, T2>` was
REJECTED (recorded in the cached finding) because identity equality
would make every fresh tuple a distinct key, breaking lookup.

### Dedicated-assertion preference (containsKey, errors.any(is X))

Two composed Dart `expect(...)` calls collapse to dedicated xUnit
assertions instead of literal-translation `Assert.True(<bool>)`:
`expect(map.containsKey(k), isTrue)` -> `Assert.Contains(k, map.Keys)`,
and `expect(xs.any((e) => e is T), isTrue)` ->
`Assert.Contains(xs, e => e is T)` (the predicate overload). The
dedicated-assertion rule is the same one recorded in
`test/multiagent/boot_loader_test.dart.md`
(`rf-dart-expect-isnot-contains-to-xunit-doesnotcontain` nuance) and
`test/multiagent/globalize_test.dart.md`: composed matcher calls SHOULD
collapse to a dedicated xUnit assertion when one exists, because
dedicated assertions produce richer failure diagnostics (the actual
keys / actual elements) instead of a bare boolean. Microsoft Learn
`Xunit.Assert.Contains<T>(IEnumerable<T>, Predicate<T>)` documents the
predicate overload.

### `is` type-test operator (Dart and C# are observably identical)

The path-consistency failure test uses
`result.errors.any((e) => e is InconsistentPathError)`. Dart `is` and
C# `is` both test the runtime type and are SUBTYPE-TOLERANT (the
expression is true for `T` and any subtype). No nuance survives the
conversion. The `InconsistentPathError` class is decided in the SUT-side
spec at
`.codeconv/conversion-specs/lib/analysis/type_checker/well_typed_term.dart.md`
(name preserved verbatim; PascalCase already).

### `isFalse`, `isNotEmpty`, `containsKey`, `errors.any(is X)` — first appearances

This file is the first test-side spec that uses Dart matchers
`isFalse`, `isNotEmpty`, `map.containsKey`, and
`iterable.any(predicate-with-is)` as `expect(...)` arguments. Each is
recorded under a new `rf-*` finding so subsequent test-spec runs reuse
the decision without re-deriving. Each finding cites the official Dart
`package:matcher` constant or method
(`https://pub.dev/documentation/matcher/latest/matcher/`) and the
corresponding xUnit assertion in the official xUnit assertion comparison
table (`https://xunit.net/docs/comparisons#assertions`) for the
authoritative basis (FR-024).

### Why no escalations

Every construct has a clear, single-decision target shape grounded in
official documentation for both Dart `package:test`/`package:matcher`
and xUnit / .NET / Microsoft Learn. The three "soft" decisions (xUnit vs
NUnit/MSTest, FLATTEN vs nested classes, dedicated-assertion vs literal
`Assert.True(<bool>)`) are documented project-wide policy with
corroborating alternatives in their research findings, not unresolved
choices. Six of the eighteen constructs reuse already-cached idioms
verbatim (FR-012/SC-007). The five first-appearance matcher mappings
(`isFalse`, `isNotEmpty`, `containsKey`, `errors.any(is X)`,
`local-arrow-helper`) each have a single authoritative xUnit target;
none is undecidable. `escalations: []` is therefore intentional, not a
placeholder.
