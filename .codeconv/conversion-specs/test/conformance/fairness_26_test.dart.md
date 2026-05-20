> Conversion-spec artifact for test/conformance/fairness_26_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/conformance/fairness_26_test.dart
source_sha256: bb89ae3cfa3df92ffb3305f90fc80250bc658914cb53c211c49157ce5c469a6e
target_code_unit: test/conformance/Fairness26Test.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and emit
      `using Xunit;` at file scope. REUSE the batch-wide test-framework
      idiom recorded in the sibling specs
      `.codeconv/conversion-specs/test/smoke_test.dart.md` and
      `.codeconv/conversion-specs/test/glp_runtime_test.dart.md` (and the
      `test/heap/`, `test/multiagent/`, `test/analysis/`, `test/module/`
      siblings — every prior `package:test` file in this batch resolved
      to xUnit). Per FR-012 / SC-007 this construct is NOT re-researched
      here; the `rf-dart-package-test-to-dotnet-xunit` finding carries
      forward verbatim. The .NET test project's `.csproj` (referencing
      `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) is
      OUT OF SCOPE for this per-file artifact — same langpair-level
      emission concern recorded in the sibling specs.
    idiom_id: null
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice reuse (explicitly addressed, no re-derivation):
      the framework decision (xUnit vs MSTest vs NUnit) was settled in
      the first test-file spec of this batch (`smoke_test.dart`) and
      every subsequent test file reuses it via the KB (FR-012). The
      module / discovery / lifecycle nuances (top-level `test()` ⇒
      `[Fact]` instance methods, fresh test-class instance per `[Fact]`
      per xunit.net "Shared Context between Tests", no top-level
      function surface in xUnit) carry forward verbatim from the
      siblings. No async / Future / Stream / isolate surface in this
      file, so the synchronous `void`-returning `[Fact]` shape (not
      `async Task`) still applies. Strict-bool / strict-equality
      semantics are unaffected by the import directive itself.
  - construct_key: dart.internal_package_import.same_package
    source_form: >-
      "import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';"
    target_decision: >-
      Drop both Dart `import 'package:glp_runtime/runtime/...';`
      directives and collapse them into a SINGLE C# `using
      <RootNs>.Runtime;` directive (the converted runtime sub-namespace
      decided by the SUT specs `.codeconv/conversion-specs/lib/runtime/
      runtime.dart.md` and `.codeconv/conversion-specs/lib/runtime/
      machine_state.dart.md` — both Dart libraries lift into the same
      C# `Runtime` sub-namespace, so a single `using` suffices). This
      brings `GlpRuntime` (the converted class for Dart's `GlpRuntime`)
      and `GoalId` (the converted type-alias for Dart `typedef GoalId =
      int;` — see the machine_state SUT spec; in C# this is either a
      `using GoalId = System.Int32;` alias or a plain `long`/`int`
      depending on the SUT spec's recorded decision) into scope so the
      test body can name them unqualified. The test assembly's `.csproj`
      must reference the converted-SUT assembly — that project-system
      wiring is OUT OF SCOPE for this per-file artifact (langpair-level
      concern; same as every other test convspec in the batch).
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed, reused from
      the test/heap/ siblings): in Dart each `package:` URI is a
      separate import; in C# all sub-paths under the same converted
      namespace collapse into ONE `using` directive (C# `using` is
      per-namespace, not per-file). The two Dart imports here
      (`runtime/runtime.dart` and `runtime/machine_state.dart`) both
      target the `Runtime` sub-namespace per their SUT specs, so they
      collapse. No `using static` is needed — the test body names
      `GlpRuntime` (a class) and `GoalId` (a type alias), both of
      which are reachable through the namespace-level `using`. No
      cross-package, cross-isolate, or transitive-export semantics
      apply. Visibility: both imported identifiers are library-public
      on the Dart side (no leading underscore) ⇒ `public` on the C#
      side per the SUT specs.
  - construct_key: dart.test_file.void_main_as_test_registration_root
    source_form: "void main() { test('...', () { ... }); }"
    target_decision: >-
      Eliminate the Dart `void main()` function entirely and lift the
      single registered `test(...)` call into one `[Fact]`-attributed
      public instance method on a `public class Fairness26Test`
      (mirroring the file name `fairness_26_test.dart` ⇒
      `Fairness26Test.cs`). The Dart test name
      `'26-step tail recursion budget yields and resets'` becomes the
      method identifier
      `Step26TailRecursionBudgetYieldsAndResets` (PascalCased, no
      hyphen/space), with
      `[Fact(DisplayName = "26-step tail recursion budget yields and resets")]`
      to preserve the original human-readable reporting name (C# method
      identifiers cannot begin with a digit, so `26-step` must be
      prefixed — `Step26...` is the canonical fix; an alternative
      `TwentySixStep...` is also valid but `Step26...` keeps the
      numeric token visible at the identifier). REUSE the idiom recorded
      in the sibling smoke_test.dart and glp_runtime_test.dart specs —
      same structural lift; no re-research (FR-012).
    idiom_id: null
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Discovery-model nuance (explicitly addressed, carry-forward from
      siblings): xUnit discovers tests by reflection over `[Fact]`
      attributes with a FRESH instance of the test class per `[Fact]`
      invocation (xunit.net "Shared Context between Tests"). The Dart
      `main()` registration pass has no xUnit equivalent and is dropped.
      Identifier-leading-digit nuance (file-specific, load-bearing):
      C# identifiers MUST begin with a letter or `_` per the C# language
      reference (learn.microsoft.com/en-us/dotnet/csharp/language-
      reference/language-specification/lexical-structure §
      "Identifiers") — Dart test names that begin with a digit (like
      `'26-step ...'`) cannot be converted by stripping non-identifier
      characters alone; a non-digit prefix MUST be prepended. Spec
      default = `Step<N>...` (preserves the numeric token), with the
      original human-readable name preserved via `[Fact(DisplayName =
      ...)]` so the test runner's report shows
      `"26-step tail recursion budget yields and resets"` verbatim. No
      setUp / tearDown / group / async — synchronous `void` `[Fact]`, no
      constructor / `IDisposable.Dispose` / `IAsyncLifetime` surface.
      Per-test fresh-instance lifecycle nuance recorded but does not
      fire here (the `GlpRuntime` instance is local to the method
      body — `final rt = ...` is method-scoped, not field-scoped).
  - construct_key: dart.local_var.final_typed_constructor_invocation
    source_form: "final rt = GlpRuntime();"
    target_decision: >-
      Emit `var rt = new GlpRuntime();` in the C# `[Fact]` method body
      (type inferred via C# `var`, matching Dart's `final` + RHS-typed
      inference). `final` on a Dart local that is never reassigned maps
      idiomatically to C# `var` (not `readonly` — `readonly` applies to
      fields, not locals; C# has no method-local `readonly` keyword).
      The Dart `GlpRuntime()` invocation maps to C# `new GlpRuntime()`
      (C# requires the `new` operator for constructor calls; Dart made
      `new` optional in Dart 2 and the source omits it). The converted
      `GlpRuntime` class lives in the `Runtime` sub-namespace already
      brought into scope by the file-level `using` (see the internal-
      package-import construct above).
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Local-variable mutability nuance (explicitly addressed): Dart
      `final <name> = expr;` declares a single-assignment local with
      RHS-inferred type (Dart language tour
      `https://dart.dev/language/variables#final-and-const` — "Use
      `final` ... for a variable that's set only once"). C# has no
      method-local single-assignment modifier; the idiomatic equivalent
      is `var` (Microsoft Learn C# reference for implicitly typed local
      variables at `learn.microsoft.com/en-us/dotnet/csharp/language-
      reference/statements/declarations`). The single-assignment
      INTENT is lost at the language level — a later edit could
      reassign `rt` — but the converted code does not reassign and the
      generated body is faithful to the source. `readonly` is the
      WRONG mapping (it applies to fields, not locals). `const` is the
      WRONG mapping (Dart `const` ⇒ compile-time constant, not
      `final`'s runtime single-assignment). Reference-vs-value:
      `GlpRuntime` is a reference type in both Dart and C# (Dart
      classes are reference types; the converted C# class is a `class`
      not a `struct` per the SUT spec), so `rt` holds a reference in
      both. Constructor syntax: Dart 2+ `new` is optional and omitted
      in idiomatic code; C# requires `new` (Microsoft Learn C# language
      reference for the `new` operator at `learn.microsoft.com/en-us/
      dotnet/csharp/language-reference/operators/new-operator`).
  - construct_key: dart.const_local.typed_int_literal
    source_form: "const GoalId g = 123;"
    target_decision: >-
      Emit `const GoalId g = 123;` in the C# `[Fact]` method body
      (C# does support `const` on method locals). The `GoalId` type
      identifier is reachable via the file-level `using
      <RootNs>.Runtime;`. The `GoalId` SUT spec records the converted
      shape of `typedef GoalId = int;` — if the SUT spec converts the
      Dart typedef to a C# `using GoalId = System.Int32;` alias (or to
      a plain `int`/`long`), this file's local declaration uses that
      same shape unchanged. NOTE: C# `const` requires a compile-time
      constant initializer, and integer literals satisfy that — the
      source literal `123` is a compile-time constant in both Dart and
      C#, so the C# `const` works directly. If the converted `GoalId`
      is a `long` (not `int`), the literal becomes `123L` for
      unambiguous binding (mirrors the lib spec's
      `rf-dart-int-to-csharp-long-width` mapping carried forward from
      the SUT specs).
    idiom_id: null
    research_finding_id: rf-dart-const-local-typed-int-to-csharp-const
    nuance: >-
      `const` semantics nuance (explicitly addressed): Dart `const` on
      a local creates a compile-time canonicalised constant (Dart
      language tour `https://dart.dev/language/variables#const`). C#
      `const` on a local also creates a compile-time constant
      (Microsoft Learn C# reference for the `const` keyword at
      `learn.microsoft.com/en-us/dotnet/csharp/language-reference/
      keywords/const`) — semantically the closest match. The conversion
      `Dart const local ⇒ C# const local` IS authoritative-supported on
      both sides for primitive integer literals; this is NOT a case
      that requires `readonly` or `static readonly` (those are for
      fields and require runtime initialization). Integer-width nuance:
      Dart `int` ⇒ C# `long` (64-bit) per the SUT spec's
      `rf-dart-int-to-csharp-long-width` carry-forward — the literal
      `123` is within both ranges so no truncation hazard, but the
      converted code emits the literal in the chosen width for type
      cleanliness. Identifier-via-typedef: `GoalId` itself is the SUT
      machine_state spec's responsibility; this convspec records only
      that the local declaration uses the SUT-decided shape verbatim.
  - construct_key: dart.for_loop.c_style_int_index
    source_form: "for (var i = 0; i < 25; i++) { ... }"
    target_decision: >-
      Emit a C# `for` loop with identical structure:
      `for (var i = 0; i < 25; i++) { ... }`. Dart and C# share the
      C-style `for (init; cond; update)` syntax verbatim (Dart language
      tour `https://dart.dev/language/loops#for-loops`; Microsoft Learn
      C# `for` statement reference at `learn.microsoft.com/en-us/dotnet/
      csharp/language-reference/statements/iteration-statements#the-for-
      statement`). The loop variable `i` is inferred as `int` in both
      languages from the literal `0`. The body contains an `expect(...)`
      call that uses an interpolated string for the failure reason —
      that interpolation is handled by the
      `dart.string.interpolation.simple_expression` construct below.
    idiom_id: null
    research_finding_id: rf-dart-c-style-for-loop-to-csharp-verbatim
    nuance: >-
      Loop-construct nuance (explicitly addressed): Dart and C# both
      inherit the C-style `for (init; cond; update)` shape with
      identical semantics — init runs once, cond is evaluated before
      each iteration, update runs after each iteration body. The loop
      variable scope is the loop in both languages. The `var i = 0`
      declaration infers `int` in both languages. No conversion-specific
      hazard; this is a verbatim transcription. The body translation
      handles `expect` (matcher routing) and the interpolated reason
      string (string-interpolation syntax) separately.
  - construct_key: dart.string.interpolation.simple_expression
    source_form: "'should not yield on step ${i+1}'"
    target_decision: >-
      Translate the Dart single-quoted interpolated string
      `'should not yield on step ${i+1}'` to a C# interpolated string
      literal `$"should not yield on step {i + 1}"`. Dart uses `${expr}`
      for the embedded expression; C# uses `{expr}` inside a `$"..."`
      literal. The single-quote-vs-double-quote difference is a syntax
      detail — Dart accepts either; C# requires double quotes for
      string literals (Microsoft Learn C# `string` reference at
      `learn.microsoft.com/en-us/dotnet/csharp/language-reference/
      tokens/interpolated`). The expression `i+1` translates verbatim
      (both languages evaluate to the same `int` result for the same
      `int i`).
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-to-csharp-dollar-string
    nuance: >-
      Interpolation-syntax nuance (explicitly addressed): Dart's
      `${expr}` (with mandatory braces for non-identifier expressions —
      `${i+1}` requires braces; `$i` would be a bare-identifier form)
      maps to C# `{expr}` inside a `$"..."` literal (Microsoft Learn
      "Interpolated string expressions"). Brace handling: C# uses
      `{{`/`}}` to escape literal braces in interpolated strings; Dart
      uses `\$` to escape literal dollar signs. Neither escape is
      needed for this file's source. Newline / format-spec: Dart has no
      built-in format-spec syntax in interpolation; C# supports
      `{expr:format}` and `{expr,alignment}` syntaxes — not used here.
      Implicit `toString()`: Dart calls `Object.toString()` on the
      embedded expression; C# calls `Object.ToString()` (or the
      `IFormattable.ToString(string,IFormatProvider)` overload if a
      format spec is given). For the `int` value `i + 1` both produce
      the canonical decimal string representation; no culture-
      sensitivity hazard for plain decimal ints.
  - construct_key: dart.package_test.expect_value_boolean_matcher
    source_form: "expect(y, isFalse, reason: '...');"
    target_decision: >-
      Translate `expect(actual, isFalse, reason: <msg>)` to xUnit
      `Assert.False(actual, <msg>);` — xUnit's `Assert.False` has an
      overload `public static void False(bool? condition, string
      userMessage)` (per xunit.net Assert API reference) that mirrors
      Dart's `expect(actual, isFalse, reason: msg)` exactly: the
      `userMessage` surfaces in the failure output the same way Dart's
      `reason:` does. The matcher routing follows the table recorded in
      the smoke_test.dart spec's nuance — `isFalse` ⇒ `Assert.False`,
      `isTrue` ⇒ `Assert.True` — and this file's two occurrences
      (`expect(y, isFalse, reason:...)` and the bare
      `expect(y1, isFalse);` later) both map to `Assert.False`.
    idiom_id: null
    research_finding_id: rf-dart-expect-isFalse-with-reason-to-xunit-assert-false
    nuance: >-
      Reason-parameter mapping (load-bearing, explicitly addressed):
      Dart `expect`'s named `reason:` parameter (pub.dev
      `https://pub.dev/documentation/test_api/latest/expect/expect.html`
      — "An optional reason for the matcher to use") surfaces in the
      failure-message header. xUnit `Assert.True` / `Assert.False` /
      `Assert.Equal` overloads accept an optional `userMessage` string
      argument (xunit.net Assert API reference; e.g. `Assert.False(bool
      condition, string userMessage)`) that surfaces the same way. The
      conversion preserves the `reason:` text verbatim as the
      `userMessage` positional argument. Strict-boolean nuance: Dart
      `isFalse` (from `package:matcher` `isFalse` constant) asserts
      strict `false` (not falsey — Dart booleans are strict); xUnit
      `Assert.False(bool condition, ...)` asserts strict `false` —
      semantically identical. Argument-order nuance: `Assert.False`
      takes ACTUAL-FIRST then optional message — same positional order
      as Dart's `expect(actual, isFalse, reason: msg)` (modulo the
      `reason:` named-vs-positional difference). Exception-on-failure:
      Dart throws `TestFailure`; xUnit throws `Xunit.Sdk.FalseException`
      (subclass of `Xunit.Sdk.XunitException`) — runner-caught,
      equivalent.
  - construct_key: dart.package_test.expect_value_boolean_matcher_no_reason
    source_form: "expect(y26, isTrue, reason: '...'); ... expect(y1, isFalse);"
    target_decision: >-
      The two boolean-matcher cases with NO `reason:` parameter
      (`expect(y1, isFalse)`) and WITH `reason:` (`expect(y26, isTrue,
      reason: '...')`) BOTH route through the same `isTrue`/`isFalse`
      ⇒ `Assert.True`/`Assert.False` mapping; the only emission
      difference is whether the optional second argument is supplied.
      Spec emission: `Assert.True(y26, "should yield on step 26");`
      and `Assert.False(y1);`. The bare-form `Assert.False(y1)`
      produces the generic "Assert.False() Failure" diagnostic on
      mismatch — acceptable because the `reason:` was omitted in the
      source (the source author judged the position-in-code sufficient
      context); spec preserves that author judgment.
    idiom_id: null
    research_finding_id: rf-dart-expect-isTrue-isFalse-bare-to-xunit-assert
    nuance: >-
      Reused matcher-routing-table row (explicitly addressed): this is
      the same matcher routing recorded in the smoke_test.dart spec's
      broader nuance table — `isTrue` ⇒ `Assert.True`, `isFalse` ⇒
      `Assert.False`. The `userMessage`-overload-vs-bare distinction
      mirrors Dart's `reason:`-supplied-vs-omitted distinction
      one-for-one. No matcher-object materialisation in C# (xUnit's
      assertion is encoded by the method name, not by a matcher
      argument). Strict-boolean semantics carry forward verbatim.
  - construct_key: dart.package_test.expect_value_equals_matcher_with_reason
    source_form: "expect(rt.budgetOf(g), 26, reason: 'budget resets after yielding');"
    target_decision: >-
      Translate the bare-value `expect(actual, value, reason: msg)`
      form to xUnit `Assert.Equal<T>(expected, actual, userMessage)` —
      WAIT: xUnit's `Assert.Equal<T>` has NO `userMessage` overload
      (xunit.net Assert API reference). xUnit deliberately rejects
      per-assertion user messages on `Assert.Equal` to keep failure
      diagnostics focused on the value diff. The conversion handles
      this by emitting `Assert.Equal(26, rt.BudgetOf(g));` (EXPECTED-
      FIRST per the smoke_test.dart spec's recorded swap) WITHOUT a
      user message, and routing the Dart `reason:` text into an XML
      doc-comment ABOVE the assertion (`// budget resets after
      yielding`) so the human-readable rationale survives the
      conversion even though xUnit cannot surface it at runtime. The
      `26` literal binds to `T = int` for the generic `Assert.Equal<T>`
      (the SUT spec for `runtime.dart` records `budgetOf` returning
      `int`/`long` per the lib spec's int-width mapping; if the SUT
      spec converted `int` ⇒ `long`, the literal becomes `26L`).
    idiom_id: null
    research_finding_id: rf-dart-expect-bare-value-int-to-xunit-assert-equal
    nuance: >-
      `reason:` lossiness nuance (load-bearing, explicitly addressed):
      xUnit's `Assert.Equal<T>` does NOT accept a `userMessage`
      argument (xunit.net Assert API; the equality-assertion family
      deliberately omits it because the value diff IS the diagnostic).
      Spec routes the Dart `reason:` text to an inline comment so the
      author's rationale survives review even though it cannot surface
      in the xUnit failure output. Alternative considered: emit
      `Assert.True(rt.BudgetOf(g) == 26, "budget resets after
      yielding");` — this WOULD accept a user message but LOSES the
      value-diff diagnostic (xUnit `Assert.True` on a comparison
      reports only "Assert.True() Failure: budget resets after
      yielding" without the actual vs expected values). Spec default =
      `Assert.Equal(26, actual)` + inline-comment for `reason:`
      because the value diff is the primary diagnostic and the comment
      preserves the author's note for review. EXPECTED-FIRST argument
      order is the load-bearing footgun (smoke_test.dart spec already
      recorded this); the conversion does NOT preserve Dart's ACTUAL-
      FIRST positional order. Method-name PascalCasing: Dart
      `budgetOf` ⇒ C# `BudgetOf` per the SUT spec's general method-
      naming carry-forward (`camelCase` ⇒ `PascalCase` for public
      members per Microsoft's C# Coding Conventions). Integer-width:
      carry-forward from the SUT spec (Dart `int` ⇒ C# `long` or `int`
      per the recorded decision); literal is emitted in the chosen
      width.
  - construct_key: dart.package_test.expect_value_equals_matcher_bare_with_reason
    source_form: "expect(rt.budgetOf(g), 25);"
    target_decision: >-
      Translate the bare-value form (no `reason:`) to
      `Assert.Equal(25, rt.BudgetOf(g));` — EXPECTED-FIRST per the
      recorded swap, same width handling as above. No inline-comment
      needed because the Dart source supplied no `reason:`.
    idiom_id: null
    research_finding_id: rf-dart-expect-bare-value-int-to-xunit-assert-equal
    nuance: >-
      Same matcher-routing-table row as the previous construct, with
      no `reason:` ⇒ no inline-comment emission. The
      argument-order-swap and integer-width nuances carry forward
      verbatim.
conversion_units:
  - "using Xunit; (file-level using directive replacing `import 'package:test/test.dart';`)"
  - "using <RootNs>.Runtime; (single file-level using directive collapsing both `import 'package:glp_runtime/runtime/runtime.dart';` and `import 'package:glp_runtime/runtime/machine_state.dart';` — the exact namespace identifier is owned by the SUT specs at .codeconv/conversion-specs/lib/runtime/runtime.dart.md and lib/runtime/machine_state.dart.md)"
  - "public class Fairness26Test { ... } (single public test class, name mirrors the .dart file name fairness_26_test.dart ⇒ Fairness26Test, no base class needed)"
  - "[Fact(DisplayName = \"26-step tail recursion budget yields and resets\")] public void Step26TailRecursionBudgetYieldsAndResets() { ... } (one Fact-attributed method per Dart test() call; DisplayName preserves the original human-readable test name; Step26... prefix resolves the C#-identifier-cannot-start-with-digit constraint)"
  - "method body line 1: var rt = new GlpRuntime(); (Dart `final rt = GlpRuntime();` ⇒ C# `var` with explicit `new`)"
  - "method body line 2: const GoalId g = 123; (Dart `const GoalId g = 123;` ⇒ C# `const` on a method local — both languages accept compile-time-constant integer literals; GoalId type-alias shape owned by the machine_state SUT spec)"
  - "method body lines 3-6: for (var i = 0; i < 25; i++) { var y = rt.TailReduce(g); Assert.False(y, $\"should not yield on step {i + 1}\"); } (verbatim C-style for loop; TailReduce method-name PascalCased per SUT spec; isFalse ⇒ Assert.False with the reason: string mapped to the userMessage overload, $-interpolated)"
  - "method body line 7: var y26 = rt.TailReduce(g); (verbatim translation)"
  - "method body line 8: Assert.True(y26, \"should yield on step 26\"); (isTrue ⇒ Assert.True; reason: ⇒ userMessage overload)"
  - "method body line 9: Assert.Equal(26, rt.BudgetOf(g)); // budget resets after yielding (equals ⇒ Assert.Equal EXPECTED-FIRST; reason: routed to inline comment because Assert.Equal has no userMessage overload)"
  - "method body line 10: var y1 = rt.TailReduce(g); (verbatim)"
  - "method body line 11: Assert.False(y1); (isFalse with no reason: ⇒ bare Assert.False)"
  - "method body line 12: Assert.Equal(25, rt.BudgetOf(g)); (equals with no reason: ⇒ bare EXPECTED-FIRST Assert.Equal)"
  - "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven, registration-via-main is dropped entirely (same as every other test-file conversion in this batch)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-to-dotnet-xunit — `package:test` ⇒ xUnit framework choice (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: this finding was
  authoritatively researched and recorded in the first test-file spec
  of this batch (`smoke_test.dart`); every subsequent test convspec in
  the batch (`glp_runtime_test.dart`, `test/heap/*`, `test/multiagent/*`,
  `test/analysis/*`, `test/module/*`) reuses it. Authoritative sources
  cited verbatim in the originating spec: Microsoft Learn
  `unit-testing-csharp-with-xunit`, xunit.net,
  pub.dev/package:test, pub.dev/documentation/test/latest/test/test.html.
- **Conclusion**: drop `import 'package:test/test.dart';`, emit
  `using Xunit;`. The `.csproj`-level NuGet wiring (xunit,
  xunit.runner.visualstudio, Microsoft.NET.Test.Sdk) is out of scope
  for this per-file artifact (langpair-level emission). Zero
  escalation.

### rf-dart-internal-package-import-to-csharp-using — `package:glp_runtime/runtime/*` ⇒ collapsed `using <RootNs>.Runtime;`

- **KB reuse (FR-012 / SC-007)**: recorded in the `test/heap/`
  siblings (`binding_pointer_test.dart`, `varref_pointer_test.dart`)
  where four runtime `package:` imports collapsed into one `using`.
  Same rule applies here for the two runtime imports.
- **Authoritative Dart**: Dart's official language tour at
  `https://dart.dev/tools/pub/dependencies` and
  `https://dart.dev/guides/libraries/create-packages` documents
  `package:` imports as per-file path-based imports.
- **Authoritative .NET**: Microsoft Learn's C# `using directive`
  reference at `https://learn.microsoft.com/en-us/dotnet/csharp/
  language-reference/keywords/using-directive` documents the `using
  <namespace>;` shape — per-namespace, not per-file. Multiple Dart
  imports into the same converted namespace collapse to one C# `using`.
- **Conclusion**: emit a single `using <RootNs>.Runtime;`. Zero
  escalation.

### rf-dart-test-main-to-xunit-class-with-facts — `void main() { test(...) }` ⇒ `class { [Fact] }` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in the smoke_test.dart
  and glp_runtime_test.dart siblings. Same structural lift: drop
  `main()`, lift the one `test()` registration into a `[Fact]` method
  on a class whose name mirrors the .dart file name. Authoritative
  sources cited in the siblings: Microsoft Learn xUnit tutorial,
  xunit.net "Shared Context between Tests", pub.dev `test` API
  reference, Dart language tour `#hello-world`.
- **File-specific application**: `fairness_26_test.dart` ⇒
  `Fairness26Test.cs` ⇒ `public class Fairness26Test`; the test name
  `'26-step tail recursion budget yields and resets'` ⇒ method
  identifier `Step26TailRecursionBudgetYieldsAndResets` (PascalCased,
  with `Step` prefix to satisfy C#'s identifier-cannot-start-with-
  digit rule per the C# language specification's lexical-structure
  section, `learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  language-specification/lexical-structure`), with `[Fact(DisplayName
  = "26-step tail recursion budget yields and resets")]` preserving
  the original human-readable reporting name. Zero escalation.

### rf-dart-final-local-to-csharp-var — `final <local> = <expr>;` ⇒ `var <local> = <expr>;`

- **Deep analysis**: Dart `final` on a local is a single-assignment
  modifier with RHS-inferred type. The source uses
  `final rt = GlpRuntime();` — a single-assignment local initialised
  by a constructor call.
- **Authoritative Dart**: Dart language tour
  `https://dart.dev/language/variables#final-and-const` — "Use
  `final` ... for a variable that's set only once."
- **Authoritative .NET**: Microsoft Learn C# reference for local
  variable declarations at `https://learn.microsoft.com/en-us/dotnet/
  csharp/language-reference/statements/declarations` — `var` is the
  implicitly typed local variable form. C# has no method-local
  `readonly` (that keyword is field-only per
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  keywords/readonly`), and `const` requires a compile-time constant
  initializer (not satisfied by `new GlpRuntime()`).
- **Conclusion**: `var rt = new GlpRuntime();`. The single-assignment
  intent is lost at the language level but the generated body does
  not reassign. Zero escalation.

### rf-dart-const-local-typed-int-to-csharp-const — `const GoalId g = 123;` ⇒ `const GoalId g = 123;`

- **Deep analysis**: Dart `const` on a local with a literal initialiser
  is a compile-time constant. C# `const` on a local with a literal
  initialiser is also a compile-time constant — semantically
  equivalent.
- **Authoritative Dart**: Dart language tour
  `https://dart.dev/language/variables#const` — "Use `const` for
  variables that you want to be compile-time constants."
- **Authoritative .NET**: Microsoft Learn C# reference for `const` at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  keywords/const` — "You use the `const` keyword to declare a constant
  field or a constant local. ... The type of a constant must be ...
  any reference type. ... The expression that's used to initialize a
  constant ... must be a constant expression."
- **Conclusion**: emit `const GoalId g = 123;` verbatim. The `GoalId`
  type-alias shape is owned by the machine_state SUT spec (recorded
  there as `typedef GoalId = int;` ⇒ C# `using GoalId = System.Int32;`
  or equivalent). Zero escalation.

### rf-dart-c-style-for-loop-to-csharp-verbatim — `for (var i = 0; i < N; i++)` ⇒ verbatim

- **Deep analysis**: Dart and C# share the C-style `for (init; cond;
  update)` syntax with identical semantics. The source's
  `for (var i = 0; i < 25; i++)` transcribes 1-to-1.
- **Authoritative Dart**: Dart language tour
  `https://dart.dev/language/loops#for-loops`.
- **Authoritative .NET**: Microsoft Learn C# `for` statement at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  statements/iteration-statements#the-for-statement`.
- **Conclusion**: verbatim transcription. Zero escalation.

### rf-dart-string-interpolation-to-csharp-dollar-string — Dart `'${expr}'` ⇒ C# `$"{expr}"`

- **Deep analysis**: Dart `'should not yield on step ${i+1}'` is a
  single-quoted string literal with one interpolated expression. C#
  `$"should not yield on step {i + 1}"` is the canonical C#
  interpolated-string equivalent.
- **Authoritative Dart**: Dart language tour
  `https://dart.dev/language/built-in-types#strings` — "You can put
  the value of an expression inside a string by using `${expression}`."
- **Authoritative .NET**: Microsoft Learn "Interpolated string
  expressions" at `https://learn.microsoft.com/en-us/dotnet/csharp/
  language-reference/tokens/interpolated` — "An interpolated string is
  a string literal that might contain interpolation expressions."
- **Conclusion**: `$"should not yield on step {i + 1}"`. Brace-escape
  differs (`{{`/`}}` in C# vs `\$` in Dart) but neither needed here.
  Zero escalation.

### rf-dart-expect-isFalse-with-reason-to-xunit-assert-false — `expect(actual, isFalse, reason: msg)` ⇒ `Assert.False(actual, msg)`

- **Deep analysis**: Dart `expect`'s `reason:` named parameter
  surfaces in the failure-message header. xUnit's `Assert.False` has
  an overload accepting `userMessage` that surfaces the same way.
- **Authoritative Dart**: pub.dev
  `https://pub.dev/documentation/test_api/latest/expect/expect.html`
  — `expect(actual, matcher, {String? reason, ...})`. Pub.dev
  `https://pub.dev/documentation/matcher/latest/matcher/isFalse-constant.html`
  — `isFalse` matches strict `false`.
- **Authoritative .NET**: xunit.net Assert API reference for
  `Assert.False(bool condition, string userMessage)` — verbatim
  overload with a user message. Microsoft Learn xUnit tutorial uses
  the `Assert.True` / `Assert.False` family throughout.
- **Conclusion**: `Assert.False(y, $"should not yield on step {i +
  1}");`. Strict-boolean semantics match. Zero escalation.

### rf-dart-expect-isTrue-isFalse-bare-to-xunit-assert — bare `expect(actual, isTrue/isFalse)` ⇒ bare `Assert.True/False`

- **KB reuse (FR-012 / SC-007)**: matcher-routing-table row recorded
  in smoke_test.dart's spec. Same authoritative sources (xunit.net
  Assert API; pub.dev `package:matcher` `isTrue`/`isFalse` constants).
- **Conclusion**: `Assert.True(y26, "should yield on step 26");` and
  `Assert.False(y1);`. Zero escalation.

### rf-dart-expect-bare-value-int-to-xunit-assert-equal — `expect(actual, value)` (with or without `reason:`) ⇒ `Assert.Equal(value, actual)`

- **Deep analysis**: the source has two `expect(rt.budgetOf(g),
  <int>)` calls — one with `reason:`, one without. Both use the
  bare-value matcher form (second argument is a value, not a
  `Matcher`); `package:test_api`'s `expect` documents that
  non-`Matcher` values are auto-wrapped as `equals(value)`. xUnit's
  counterpart is `Assert.Equal<T>(T expected, T actual)` —
  EXPECTED-FIRST argument order (the OPPOSITE of Dart's ACTUAL-FIRST
  `expect(actual, equals(expected))`).
- **Authoritative Dart (`expect` bare-value)**: pub.dev
  `https://pub.dev/documentation/test_api/latest/expect/expect.html`
  — "If [matcher] is not a [Matcher], it will be implicitly wrapped
  in [equals]."
- **Authoritative .NET (`Assert.Equal`)**: xunit.net Assert API for
  `Equal<T>(T expected, T actual)` — verbatim EXPECTED-FIRST. NOTE:
  no `userMessage` overload exists for `Assert.Equal<T>` (verified
  against the xunit.net API reference; this is a deliberate xUnit
  design choice — the value diff IS the diagnostic).
- **`reason:` handling nuance (file-specific, load-bearing)**: Dart
  `expect(rt.budgetOf(g), 26, reason: 'budget resets after
  yielding')` supplies a `reason:` text that xUnit's `Assert.Equal`
  cannot surface (no `userMessage` overload). Spec routes the
  `reason:` text to an inline `// ...` comment ABOVE or beside the
  assertion so the author's rationale survives review. An alternative
  — emit `Assert.True(rt.BudgetOf(g) == 26, "budget resets after
  yielding");` — was considered and rejected because it loses the
  value-diff diagnostic (xUnit `Assert.True` on a comparison reports
  only the user message, not the actual/expected values).
- **Conclusion**: `Assert.Equal(26, rt.BudgetOf(g)); // budget resets
  after yielding` and `Assert.Equal(25, rt.BudgetOf(g));`.
  EXPECTED-FIRST argument order is the load-bearing footgun (recorded
  in the smoke_test.dart spec's matcher-table nuance). Integer-width
  is owned by the SUT `runtime.dart` spec's `BudgetOf` return-type
  decision. Zero escalation.

## Notes

- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface in this file — the `[Fact]` method is `void` (not `async
  Task`). The well-known async-Dart-vs-.NET-async nuance is
  deliberately not asserted here (does not apply to this file's source
  surface).
- No `late`, `mixin`, `extension`, generics, sealed/abstract,
  bitwise/shift, isolate, or null-safety nuance — all absent.
- The file exercises the runtime's tail-recursion-budget public
  surface (`tailReduce`, `budgetOf`) on a `GlpRuntime` instance. The
  SUT-side conversion shape (class name, method names, return types,
  GoalId type-alias shape) is owned by the SUT specs at
  `.codeconv/conversion-specs/lib/runtime/runtime.dart.md` and
  `.codeconv/conversion-specs/lib/runtime/machine_state.dart.md`; this
  test convspec references their decisions but does not duplicate
  them.
- Identifier-leading-digit nuance is load-bearing for the Dart test
  name `'26-step tail recursion budget yields and resets'` — C#
  identifiers cannot start with a digit per the C# language
  specification, so the conversion prefixes `Step` to preserve the
  numeric token while satisfying the lexical rule, and preserves the
  original human-readable name verbatim via `[Fact(DisplayName =
  ...)]`. Recorded as a reusable consideration for any future test
  file whose test name starts with a digit.
- The `reason:`-to-`Assert.Equal` lossiness nuance is load-bearing
  here: xUnit's `Assert.Equal<T>` has no `userMessage` overload, so
  Dart's `reason:` text on equality assertions is routed to an inline
  comment rather than dropped. Recorded as a reusable consideration
  for any future test file using `expect(..., <value>, reason: ...)`.
- Zero escalations: every construct in this file is
  authoritative-supported on both sides, the majority REUSE idioms /
  findings recorded by sibling specs (smoke_test.dart, glp_runtime_
  test.dart, test/heap/* siblings) per FR-012 / SC-007 KB-reuse
  decision order, and the file-specific nuances (leading-digit
  identifier, `reason:`-to-`Assert.Equal` routing) are recorded as
  reusable considerations for future test conversions.
