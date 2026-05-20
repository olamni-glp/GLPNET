> Conversion-spec artifact for test/bytecode/fairness_scheduler_loop_test.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/bytecode/fairness_scheduler_loop_test.dart
source_sha256: 15f2e909b0910c86fb911fa212eb0812ca811197cf88c5e6cc0c7d9cf981eba8
target_code_unit: test/bytecode/FairnessSchedulerLoopTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and emit
      `using Xunit;` at file scope. REUSE the batch-wide test-framework
      idiom recorded in the sibling specs
      `.codeconv/conversion-specs/test/smoke_test.dart.md`,
      `.codeconv/conversion-specs/test/glp_runtime_test.dart.md`, and the
      sibling fairness spec
      `.codeconv/conversion-specs/test/conformance/fairness_26_test.dart.md`
      (every prior `package:test` file in this batch resolved to xUnit).
      Per FR-012 / SC-007 this construct is NOT re-researched here; the
      `rf-dart-package-test-to-dotnet-xunit` finding carries forward
      verbatim. The .NET test project's `.csproj` (referencing `xunit`,
      `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) is OUT OF
      SCOPE for this per-file artifact — langpair-level emission concern.
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
      "import 'package:glp_runtime/bytecode/opcodes.dart';
       import 'package:glp_runtime/bytecode/runner.dart';
       import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';
       import 'package:glp_runtime/runtime/scheduler.dart';"
    target_decision: >-
      Drop all five Dart `import 'package:glp_runtime/...';` directives
      and collapse them into TWO C# `using` directives: `using
      <RootNs>.Bytecode;` (covering `bytecode/opcodes.dart` and
      `bytecode/runner.dart`) and `using <RootNs>.Runtime;` (covering
      `runtime/runtime.dart`, `runtime/machine_state.dart`, and
      `runtime/scheduler.dart`). The two collapsed namespaces come from
      the SUT specs: `.codeconv/conversion-specs/lib/bytecode/
      opcodes.dart.md` and `.codeconv/conversion-specs/lib/bytecode/
      runner.dart.md` (both lift into the `Bytecode` sub-namespace), and
      `.codeconv/conversion-specs/lib/runtime/runtime.dart.md`,
      `.codeconv/conversion-specs/lib/runtime/machine_state.dart.md`,
      `.codeconv/conversion-specs/lib/runtime/scheduler.dart.md` (all
      three lift into the `Runtime` sub-namespace). This brings
      `BytecodeProgram`, `BytecodeRunner`, `Label`, `TailStep` (from
      Bytecode), and `GlpRuntime`, `GoalRef`, `Scheduler` (from Runtime)
      into scope for the test body. The test assembly's `.csproj` must
      reference the converted-SUT assembly — that project-system wiring
      is OUT OF SCOPE for this per-file artifact (langpair-level
      concern; same as every other test convspec in the batch).
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed, reused from
      the test/heap/ and test/conformance/ siblings): in Dart each
      `package:` URI is a separate import; in C# all sub-paths under
      the same converted namespace collapse into ONE `using` directive
      (C# `using` is per-namespace, not per-file — Microsoft Learn
      `using-directive` reference). Five Dart imports collapse to two
      C# `using` directives here because the Dart files span two
      target sub-namespaces (`Bytecode` and `Runtime`) per the SUT
      specs. No `using static` is needed — the test body names types
      (`BytecodeProgram`, `BytecodeRunner`, `Label`, `TailStep`,
      `GlpRuntime`, `GoalRef`, `Scheduler`), all reachable through
      namespace-level `using`. No cross-package, cross-isolate, or
      transitive-export semantics apply. Visibility: every imported
      identifier is library-public on the Dart side (no leading
      underscore) ⇒ `public` on the C# side per the SUT specs.
  - construct_key: dart.test_file.void_main_as_test_registration_root
    source_form: "void main() { test('...', () { ... }); }"
    target_decision: >-
      Eliminate the Dart `void main()` function entirely and lift the
      single registered `test(...)` call into one `[Fact]`-attributed
      public instance method on a `public class FairnessSchedulerLoopTest`
      (mirroring the file name `fairness_scheduler_loop_test.dart` ⇒
      `FairnessSchedulerLoopTest.cs`). The Dart test name
      `'Two goals alternate due to 26-step tail yield'` becomes the
      method identifier `TwoGoalsAlternateDueTo26StepTailYield`
      (PascalCased, no spaces — leading-token `Two` is a letter so no
      `Step` prefix needed here, unlike the sibling fairness_26 file).
      Emit `[Fact(DisplayName = "Two goals alternate due to 26-step tail
      yield")]` to preserve the original human-readable reporting name.
      REUSE the idiom recorded in the sibling smoke_test.dart,
      glp_runtime_test.dart, and fairness_26_test.dart specs — same
      structural lift; no re-research (FR-012).
    idiom_id: null
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Discovery-model nuance (explicitly addressed, carry-forward from
      siblings): xUnit discovers tests by reflection over `[Fact]`
      attributes with a FRESH instance of the test class per `[Fact]`
      invocation (xunit.net "Shared Context between Tests"). The Dart
      `main()` registration pass has no xUnit equivalent and is
      dropped. No setUp / tearDown / group / async — synchronous `void`
      `[Fact]`, no constructor / `IDisposable.Dispose` /
      `IAsyncLifetime` surface. Per-test fresh-instance lifecycle
      nuance recorded but does not fire here (the `GlpRuntime`,
      `BytecodeProgram`, `BytecodeRunner`, `Scheduler` instances are
      local to the method body — all method-scoped `final` locals, not
      field-scoped). Identifier-leading-digit nuance recorded in
      fairness_26_test.dart does NOT fire here because the Dart test
      name starts with the letter `T`, not a digit; method identifier
      is a straight PascalCase of the original name with spaces
      stripped.
  - construct_key: dart.local_var.final_typed_constructor_invocation
    source_form: "final rt = GlpRuntime();"
    target_decision: >-
      Emit `var rt = new GlpRuntime();` in the C# `[Fact]` method
      body. REUSE the idiom recorded in the sibling fairness_26 spec
      (`rf-dart-final-local-to-csharp-var`); `final` on a Dart local
      that is never reassigned ⇒ C# `var` (not `readonly`, not
      `const`). Constructor call: Dart `GlpRuntime()` ⇒ C# `new
      GlpRuntime()` (C# requires the `new` operator). `GlpRuntime` is
      reachable via the `using <RootNs>.Runtime;` brought in by the
      collapsed-import construct above. The same idiom applies to the
      three other `final` locals in this method body: `final p =
      BytecodeProgram([...])` ⇒ `var p = new BytecodeProgram(new[] {
      ... })`; `final runner = BytecodeRunner(p)` ⇒ `var runner = new
      BytecodeRunner(p)`; `final sched = Scheduler(rt: rt, runner:
      runner)` ⇒ `var sched = new Scheduler(rt: rt, runner: runner);`
      (Dart named-arguments map to C# named arguments verbatim — C#
      `learn.microsoft.com/en-us/dotnet/csharp/language-reference/
      proposals/csharp-7.2/named-and-optional-arguments` supports
      colon-form named arguments at the call site, identical syntax).
      And the two later `final ran1 = sched.drain(maxCycles: 2);` /
      `final ran2 = sched.drain(maxCycles: 2);` ⇒ `var ran1 =
      sched.Drain(maxCycles: 2);` / `var ran2 = sched.Drain(maxCycles:
      2);` (method-name PascalCasing per SUT spec).
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Local-variable mutability + named-argument nuance (explicitly
      addressed): Dart `final <name> = expr;` declares a single-
      assignment local with RHS-inferred type — Dart language tour at
      `dart.dev/language/variables#final-and-const`. C# has no method-
      local single-assignment modifier; idiomatic equivalent is `var`
      per Microsoft Learn `learn.microsoft.com/en-us/dotnet/csharp/
      language-reference/statements/declarations`. The single-
      assignment INTENT is lost at the language level — a later edit
      could reassign — but the converted body does not reassign.
      `readonly` is WRONG (field-only). `const` is WRONG (compile-
      time, not satisfied by constructor calls). Named-argument
      preservation: Dart `Scheduler(rt: rt, runner: runner)` uses Dart
      named arguments; C# supports the identical `name: value` syntax
      at the call site (C# 4.0+), so the conversion preserves the
      named-argument form verbatim — no positional collapse needed.
      Reference-vs-value: `GlpRuntime`, `BytecodeProgram`,
      `BytecodeRunner`, `Scheduler` are reference types in both Dart
      and C# (per their SUT specs, all four are `class`, not `struct`).
  - construct_key: dart.list_literal.struct_elements_as_ctor_arg
    source_form: "BytecodeProgram([Label('LOOP'), TailStep('LOOP')])"
    target_decision: >-
      Translate the Dart positional list literal of constructor calls
      to a C# array initializer of `new` expressions, passed as the
      single positional argument to `new BytecodeProgram(...)`. Emit
      `new BytecodeProgram(new[] { new Label("LOOP"), new TailStep("LOOP") })`
      (or, equivalently, `new BytecodeProgram(new List<Op> { ... })`
      depending on the SUT `BytecodeProgram` constructor parameter
      type recorded in `.codeconv/conversion-specs/lib/bytecode/
      runner.dart.md` — whichever element-collection type the SUT
      converter chose for the Dart `List<Op>` parameter). Dart's
      list-literal `[a, b]` shape maps to C#'s collection-expression
      `new[] { a, b }` (or C# 12 collection-expression `[a, b]` —
      Microsoft Learn `learn.microsoft.com/en-us/dotnet/csharp/
      language-reference/operators/collection-expressions`, "Collection
      expressions are converted to the target type at compile time").
      The two element-constructor calls `Label('LOOP')` and
      `TailStep('LOOP')` map to `new Label("LOOP")` and `new
      TailStep("LOOP")` respectively (Dart's optional-`new` ⇒ C#
      mandatory-`new`; Dart single-quoted string literal ⇒ C# double-
      quoted — strings in C# require double quotes per Microsoft Learn
      `string` reference).
    idiom_id: null
    research_finding_id: rf-dart-list-literal-of-constructors-to-csharp-array-init
    nuance: >-
      List-literal vs collection-expression nuance (explicitly
      addressed): Dart `[expr1, expr2]` is a literal `List<T>`
      expression where `T` is the LUB of element types (Dart language
      tour `dart.dev/language/collections#lists`). C# has two
      idiomatic equivalents: pre-C#-12 `new[] { e1, e2 }` (array, type
      inferred via LUB) and C# 12+ `[e1, e2]` collection expression
      (target-typed; converts to any supported collection type). For a
      `BytecodeProgram` constructor whose Dart parameter is
      `List<Op>`, both forms work; spec emits `new[] { ... }` as the
      LCD-portable form unless the SUT spec records a C#-12+ target.
      Element-type inference: the two element constructors `Label` and
      `TailStep` must share a common base/interface (per the SUT
      `Op`/opcode hierarchy recorded in the `bytecode/opcodes.dart`
      SUT spec) — codegen MUST honour that hierarchy so `new[] { new
      Label(...), new TailStep(...) }` infers `Op[]` (or whichever
      base the SUT records). String-literal nuance: Dart accepts
      single or double quotes; C# requires double quotes. No escape
      issues for the bare ASCII `LOOP` identifier.
  - construct_key: dart.method_call.named_argument
    source_form: "Scheduler(rt: rt, runner: runner)"
    target_decision: >-
      Translate Dart's named-argument constructor call verbatim using
      C#'s named-argument syntax: `new Scheduler(rt: rt, runner:
      runner)`. C# 4.0+ supports `name: value` named-argument syntax
      at the call site (Microsoft Learn `learn.microsoft.com/en-us/
      dotnet/csharp/language-reference/proposals/csharp-7.2/named-
      and-optional-arguments` and the `learn.microsoft.com/en-us/
      dotnet/csharp/programming-guide/classes-and-structs/named-and-
      optional-arguments` programming-guide page). The SUT
      `Scheduler` class records the converted constructor parameter
      names in `.codeconv/conversion-specs/lib/runtime/scheduler.dart
      .md` — codegen uses the names from that SUT spec verbatim
      (Dart `rt` ⇒ C# `rt`, Dart `runner` ⇒ C# `runner`; lowercase
      parameter names per C# parameter-naming convention, Microsoft
      Learn C# Coding Conventions). The same idiom applies to the
      two `sched.drain(maxCycles: 2)` call sites ⇒
      `sched.Drain(maxCycles: 2)` (method-name PascalCased per SUT
      spec; named-argument preserved). REUSE
      `rf-dart-named-arg-to-csharp-named-arg` if recorded in a prior
      sibling; first authoritative citation here otherwise.
    idiom_id: null
    research_finding_id: rf-dart-named-arg-to-csharp-named-arg
    nuance: >-
      Named-argument syntax nuance (explicitly addressed): Dart named
      arguments use `name: value` colon-form (Dart language tour
      `dart.dev/language/functions#named-parameters`). C# named
      arguments use the identical `name: value` colon-form at the call
      site (Microsoft Learn programming-guide citation above) — direct
      1:1 transcription. Optional-vs-required: Dart named parameters
      may be optional (`{T name}`) or required (`{required T name}`);
      C# named arguments are determined by the called method's
      parameter list, not the caller. Re-ordering: both languages
      allow named arguments to appear in any order at the call site
      (C# requires that positional args precede named args; the source
      here uses only named args so ordering is unconstrained either
      way). Parameter-name stability: the named-argument name at the
      call site MUST match the converted parameter name in the SUT
      spec; codegen wiring is OUT OF SCOPE for this artifact (langpair
      concern). Method-name PascalCasing applies to the method
      identifier (`drain` ⇒ `Drain`) but NOT to the named-argument
      label (`maxCycles` stays `maxCycles` — parameter names follow
      camelCase per Microsoft C# Coding Conventions).
  - construct_key: dart.property_chain.field_method_call
    source_form: "rt.gq.enqueue(GoalRef(1, p.labels['LOOP']!));"
    target_decision: >-
      Translate the Dart property-chain `rt.gq.enqueue(...)` to C#
      `rt.Gq.Enqueue(...)` (member-access chain identical in both
      languages; PascalCasing applied to the public field/property
      `gq` ⇒ `Gq` and the public method `enqueue` ⇒ `Enqueue` per the
      SUT specs for `runtime.dart` and `goal_queue.dart`). The
      argument is a constructor call `GoalRef(1, p.labels['LOOP']!)`
      ⇒ `new GoalRef(1, p.Labels["LOOP"]!)` — Dart `[...]` indexer ⇒
      C# `[...]` indexer (identical syntax; the indexer is `IDictionary
      <string, int>.this[string]` per the SUT spec for
      `BytecodeProgram.labels`). The Dart `!` non-null assertion
      operator ⇒ C# `!` null-forgiving operator (semantically
      identical in this surface use: assert-non-null at point of use,
      do not coerce). The integer literal `1` stays `1` (Dart `int` ⇒
      C# `int` or `long` per the SUT spec's recorded width
      decision — if `GoalRef`'s first parameter is `long`, the
      literal becomes `1L`). Two occurrences (one with `1`, one with
      `2`) — both apply the same rule.
    idiom_id: null
    research_finding_id: rf-dart-property-chain-method-call-to-csharp
    nuance: >-
      Member-access + null-assertion nuance (explicitly addressed):
      Dart and C# both use `.` for member access on a non-null
      receiver, with identical left-to-right chaining semantics.
      Dart's `!` (non-null assertion) is documented at
      `dart.dev/language/operators#null-aware-operators` ("the
      null-assertion operator (!) ... will throw if the value is
      null"). C#'s `!` (null-forgiving operator) is documented at
      `learn.microsoft.com/en-us/dotnet/csharp/language-reference/
      operators/null-forgiving` ("suppresses nullable-state warnings
      ... has no effect at run time"). SEMANTIC NUANCE: Dart `!`
      DOES throw at runtime if the value is null; C# `!` does NOT —
      it is purely a compile-time annotation. For this file's call
      site (`p.labels['LOOP']!`) the conversion is faithful at the
      compile-time-nullability level but loses the runtime-throw
      guarantee. Recorded as a load-bearing nuance for any future
      Dart-`!` conversion; the SUT-side `BytecodeProgram.Labels`
      indexer must therefore be designed to throw `KeyNotFoundException`
      on missing keys (C# `IDictionary<TKey,TValue>` indexer throws
      `KeyNotFoundException` on missing key, matching the runtime-
      throw intent of Dart's `!`) — that SUT-side guarantee is
      recorded in the bytecode/runner SUT spec, not here. Indexer
      syntax: Dart `map['LOOP']` ⇒ C# `dict["LOOP"]` (identical
      shape, Dart single-quoted ⇒ C# double-quoted string literal).
      Method-name PascalCasing: `enqueue` ⇒ `Enqueue`, `gq` ⇒ `Gq`
      per Microsoft C# Coding Conventions and the SUT specs.
  - construct_key: dart.package_test.expect_value_equals_matcher_list_literal_with_reason
    source_form: "expect(ran1, [1, 2], reason: 'each goal runs until its first yield');"
    target_decision: >-
      Translate the bare-value `expect(actual, [literal-list], reason:
      msg)` form to xUnit `Assert.Equal(expected, actual)` with the
      `reason:` text routed to an inline `// ...` comment ABOVE the
      assertion: `// each goal runs until its first yield` then
      `Assert.Equal(new[] { 1, 2 }, ran1);`. REUSE the
      `rf-dart-list-equality-to-xunit-assertequal-collection` idiom
      recorded in the sibling spec
      `.codeconv/conversion-specs/test/multiagent/boot_loader_test.dart.md`
      (collection-equality row of the `expect`-matcher table). The
      `reason:`-on-`Assert.Equal` lossiness handling REUSES the
      file-specific nuance recorded in the sibling fairness_26 spec
      (`rf-dart-expect-bare-value-int-to-xunit-assert-equal` — Dart
      `reason:` text routed to inline comment because
      `Assert.Equal<T>` has no `userMessage` overload). xUnit
      `Assert.Equal<T>(IEnumerable<T>, IEnumerable<T>)` performs
      element-wise equality (xunit.net Assert API reference;
      Microsoft Learn xUnit tutorial) — matches Dart's `equals` over
      `List<int>` (auto-wrapping per `expect`'s documented
      "implicitly wrapped in [equals]" behaviour, pub.dev
      `https://pub.dev/documentation/test_api/latest/expect/expect.html`).
      EXPECTED-FIRST argument order per the smoke_test.dart spec's
      recorded swap (the load-bearing footgun: xUnit reverses Dart's
      ACTUAL-FIRST order). Same rule applies to the second occurrence
      `expect(ran2, [1, 2], reason: 'after re-enqueue, order remains
      FIFO');` ⇒ `// after re-enqueue, order remains FIFO` then
      `Assert.Equal(new[] { 1, 2 }, ran2);`. Element-type: the SUT
      `Scheduler.drain` return-type (`List<GoalId>` or
      `List<int>`/`List<long>` per `.codeconv/conversion-specs/lib/
      runtime/scheduler.dart.md`) drives whether the C# expected
      literal uses `new[] { 1, 2 }` (int inferred) or `new long[] {
      1L, 2L }` / `new GoalId[] { ... }` (explicit). The SUT spec is
      authoritative; codegen uses the chosen width.
    idiom_id: null
    research_finding_id: rf-dart-list-equality-to-xunit-assertequal-collection
    nuance: >-
      Collection-equality + `reason:` lossiness compound nuance
      (load-bearing, explicitly addressed). Two reused idioms compose
      at this call site: (1) collection-equality routing (Dart `List`
      element-wise `equals` ⇒ xUnit `Assert.Equal(IEnumerable<T>,
      IEnumerable<T>)` — xunit.net Assert API reference; default
      `IEqualityComparer<T>` falls through to `IEquatable<T>.Equals`,
      identical to Dart's element-wise `==` over `int`), and (2)
      `reason:`-to-`Assert.Equal` lossiness (xUnit's `Assert.Equal<T>`
      has NO `userMessage` overload per xunit.net Assert API; spec
      routes Dart `reason:` text to inline comment). Order-sensitivity:
      both Dart `equals` on `List<T>` and xUnit `Assert.Equal` on
      `IEnumerable<T>` are sequence-order-sensitive — the test
      semantics (FIFO scheduling) DEPEND on order, so the order-
      sensitive equality is the correct routing. Alternative
      `Assert.True(ran1.SequenceEqual(new[] {1, 2}), "...")` was
      considered: it WOULD accept a user message but LOSES the value-
      diff diagnostic (xUnit `Assert.True` on a comparison reports
      only the user message, not the actual/expected sequence diff).
      Spec default = `Assert.Equal` + inline-comment for `reason:`.
      EXPECTED-FIRST argument order is the load-bearing footgun
      (smoke_test.dart spec already recorded this); the conversion
      does NOT preserve Dart's ACTUAL-FIRST positional order.
      Materialisation nuance: Dart `[1, 2]` is an eager `List<int>`
      literal; C# `new[] { 1, 2 }` is an eager `int[]` literal —
      semantic match. The actual side `ran1` / `ran2` is the SUT
      `Scheduler.drain` return value; per the scheduler SUT spec it
      materialises (the SUT spec records `drain` returning a concrete
      `List<GoalId>`/`IReadOnlyList<GoalId>` rather than a deferred
      `IEnumerable<GoalId>`).
conversion_units:
  - "using Xunit; (file-level using directive replacing `import 'package:test/test.dart';`)"
  - "using <RootNs>.Bytecode; (single file-level using directive collapsing `import 'package:glp_runtime/bytecode/opcodes.dart';` and `import 'package:glp_runtime/bytecode/runner.dart';` — namespace identifier owned by the bytecode SUT specs)"
  - "using <RootNs>.Runtime; (single file-level using directive collapsing `import 'package:glp_runtime/runtime/runtime.dart';`, `import 'package:glp_runtime/runtime/machine_state.dart';`, and `import 'package:glp_runtime/runtime/scheduler.dart';` — namespace identifier owned by the runtime SUT specs)"
  - "public class FairnessSchedulerLoopTest { ... } (single public test class, name mirrors the .dart file name fairness_scheduler_loop_test.dart ⇒ FairnessSchedulerLoopTest, no base class needed)"
  - "[Fact(DisplayName = \"Two goals alternate due to 26-step tail yield\")] public void TwoGoalsAlternateDueTo26StepTailYield() { ... } (one Fact-attributed method per Dart test() call; DisplayName preserves the original human-readable test name verbatim)"
  - "method body line 1: var rt = new GlpRuntime(); (Dart `final rt = GlpRuntime();` ⇒ C# `var` + `new`)"
  - "method body lines 2-5: var p = new BytecodeProgram(new[] { new Label(\"LOOP\"), new TailStep(\"LOOP\") }); (Dart list-of-constructors literal ⇒ C# array initializer of `new` expressions; element type `Op` inferred from the SUT opcode hierarchy)"
  - "method body line 6: var runner = new BytecodeRunner(p); (verbatim — Dart `final` ⇒ `var`, positional arg)"
  - "method body line 7: var sched = new Scheduler(rt: rt, runner: runner); (named-argument preserved; C# `name: value` syntax 1:1 with Dart)"
  - "method body line 8: rt.Gq.Enqueue(new GoalRef(1, p.Labels[\"LOOP\"]!)); (property chain + indexer + null-forgiving; PascalCased method/field names per SUT specs; integer width per SUT GoalRef parameter type)"
  - "method body line 9: rt.Gq.Enqueue(new GoalRef(2, p.Labels[\"LOOP\"]!)); (same shape, second goal id)"
  - "method body line 10: var ran1 = sched.Drain(maxCycles: 2); (named-argument preserved; PascalCased method name)"
  - "method body line 11: // each goal runs until its first yield (inline comment carrying Dart `reason:` text — xUnit Assert.Equal has no userMessage overload)"
  - "method body line 12: Assert.Equal(new[] { 1, 2 }, ran1); (collection-equality; EXPECTED-FIRST per the smoke_test.dart spec's recorded swap; element type `int` inferred — switch to `long`/`GoalId[]` if the SUT scheduler.dart spec records that width)"
  - "method body line 13: var ran2 = sched.Drain(maxCycles: 2); (verbatim second drain call)"
  - "method body line 14: // after re-enqueue, order remains FIFO (inline comment carrying Dart `reason:` text)"
  - "method body line 15: Assert.Equal(new[] { 1, 2 }, ran2); (same collection-equality routing; EXPECTED-FIRST; reason: routed to inline comment above)"
  - "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven, registration-via-main is dropped entirely (same as every other test-file conversion in this batch)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-to-dotnet-xunit — `package:test` ⇒ xUnit framework choice (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: authoritatively
  researched and recorded in the first test-file spec of this batch
  (`smoke_test.dart`); reused verbatim by `glp_runtime_test.dart`,
  every `test/heap/*` sibling, every `test/multiagent/*` sibling,
  `test/analysis/type_checker/*`, `test/module/*`, and the sibling
  fairness spec `test/conformance/fairness_26_test.dart`.
  Authoritative sources cited verbatim in the originating spec:
  Microsoft Learn `unit-testing-csharp-with-xunit`, xunit.net,
  pub.dev/package:test,
  pub.dev/documentation/test/latest/test/test.html.
- **Conclusion**: drop `import 'package:test/test.dart';`, emit `using
  Xunit;`. `.csproj`-level NuGet wiring (xunit,
  xunit.runner.visualstudio, Microsoft.NET.Test.Sdk) is out of scope
  for this per-file artifact (langpair-level emission). Zero escalation.

### rf-dart-internal-package-import-to-csharp-using — `package:glp_runtime/...` ⇒ collapsed `using` directives (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in the `test/heap/` and
  `test/conformance/` siblings; here five Dart imports collapse to
  two C# `using` directives (two distinct target sub-namespaces).
- **Authoritative Dart**: language tour at
  `dart.dev/tools/pub/dependencies` and
  `dart.dev/guides/libraries/create-packages` documents `package:`
  imports as per-file path-based imports.
- **Authoritative .NET**: Microsoft Learn C# `using directive`
  reference at `learn.microsoft.com/en-us/dotnet/csharp/language-
  reference/keywords/using-directive` documents the per-namespace
  shape — multiple Dart imports under the same converted namespace
  collapse to one C# `using`.
- **Conclusion**: emit `using <RootNs>.Bytecode;` and `using
  <RootNs>.Runtime;`. Zero escalation.

### rf-dart-test-main-to-xunit-class-with-facts — `void main() { test(...) }` ⇒ `class { [Fact] }` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in smoke_test.dart,
  glp_runtime_test.dart, and fairness_26_test.dart siblings.
- **File-specific application**: `fairness_scheduler_loop_test.dart`
  ⇒ `FairnessSchedulerLoopTest.cs` ⇒ `public class
  FairnessSchedulerLoopTest`; the test name `'Two goals alternate due
  to 26-step tail yield'` ⇒ method identifier
  `TwoGoalsAlternateDueTo26StepTailYield` (PascalCased; the leading
  token `Two` is a letter so no `Step`/`TwentySix` prefix is needed
  here, unlike the sibling fairness_26 file — the leading-digit
  nuance is recorded but does NOT fire). `[Fact(DisplayName = "Two
  goals alternate due to 26-step tail yield")]` preserves the
  original human-readable reporting name. Zero escalation.

### rf-dart-final-local-to-csharp-var — `final <local> = <expr>;` ⇒ `var <local> = <expr>;` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in the sibling fairness_26
  spec and many earlier test-file siblings. Same authoritative
  sources: Dart language tour
  `dart.dev/language/variables#final-and-const`, Microsoft Learn C#
  reference `learn.microsoft.com/en-us/dotnet/csharp/language-
  reference/statements/declarations`.
- **File-specific application**: applies to all six `final` locals
  in this method body (`rt`, `p`, `runner`, `sched`, `ran1`, `ran2`)
  — all become `var`. None are reassigned in the source. Zero
  escalation.

### rf-dart-list-literal-of-constructors-to-csharp-array-init — `[Ctor1(...), Ctor2(...)]` ⇒ `new[] { new Ctor1(...), new Ctor2(...) }`

- **Deep analysis**: the source's
  `BytecodeProgram([Label('LOOP'), TailStep('LOOP')])` is a Dart list
  literal of two heterogeneous-but-related constructor calls, passed
  positionally to `BytecodeProgram(...)`. The Dart list-literal shape
  `[e1, e2]` has no direct C# equivalent at the syntactic level
  before C# 12, but C#'s pre-12 array-initializer `new[] { e1, e2 }`
  is the LCD-portable form, and C# 12 added a collection-expression
  `[e1, e2]` shape with the same semantics.
- **Authoritative Dart**: language tour
  `dart.dev/language/collections#lists` — "Lists are ordered groups
  of objects ... declared using square-bracket syntax".
- **Authoritative .NET (pre-12)**: Microsoft Learn array-initializer
  reference at `learn.microsoft.com/en-us/dotnet/csharp/programming-
  guide/arrays/single-dimensional-arrays`. **Authoritative .NET
  (C# 12+)**: Microsoft Learn collection-expressions reference at
  `learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  operators/collection-expressions`.
- **Conclusion**: emit `new BytecodeProgram(new[] { new
  Label("LOOP"), new TailStep("LOOP") })`. Element-type inference
  requires that `Label` and `TailStep` share a common base/interface
  (per the SUT `bytecode/opcodes.dart` spec's opcode hierarchy). If
  the SUT records a different element-collection type (e.g.
  `List<Op>` rather than `Op[]`), codegen substitutes
  `new List<Op> { ... }` or the C# 12 collection-expression `[...]`.
  Zero escalation.

### rf-dart-named-arg-to-csharp-named-arg — Dart `name: value` ⇒ C# `name: value`

- **Deep analysis**: the source uses named arguments at three call
  sites (`Scheduler(rt: rt, runner: runner)`, `sched.drain(maxCycles:
  2)` twice). Dart and C# both support `name: value` syntax at the
  call site — direct 1:1 transcription.
- **Authoritative Dart**: language tour
  `dart.dev/language/functions#named-parameters` — "When calling a
  function, you can specify named arguments using `paramName:
  value`."
- **Authoritative .NET**: Microsoft Learn
  `learn.microsoft.com/en-us/dotnet/csharp/programming-guide/
  classes-and-structs/named-and-optional-arguments` — "A named
  argument enables you to specify an argument for a parameter by
  matching the argument with its name rather than with its position
  in the parameter list."
- **Conclusion**: verbatim transcription with parameter-name
  PascalCasing skipped (parameter names follow camelCase per C# Coding
  Conventions; method names PascalCased). Zero escalation.

### rf-dart-property-chain-method-call-to-csharp — `obj.field.method(arg)` ⇒ `obj.Field.Method(arg)`

- **Deep analysis**: the source's `rt.gq.enqueue(...)` is a
  two-segment member-access chain ending in a method call. C# member
  access has identical left-to-right chaining semantics. PascalCasing
  applies to public-member names per C# Coding Conventions.
- **Authoritative Dart**: language tour
  `dart.dev/language/operators` documents `.` member access.
- **Authoritative .NET**: Microsoft Learn member-access operator
  reference at `learn.microsoft.com/en-us/dotnet/csharp/language-
  reference/operators/member-access-operators` documents the `.`
  operator.
- **Null-forgiving sub-construct**: Dart `!` non-null assertion at
  `dart.dev/language/operators#null-aware-operators` ("the
  null-assertion operator (!) ... will throw if the value is null")
  vs. C# `!` null-forgiving at
  `learn.microsoft.com/en-us/dotnet/csharp/language-reference/
  operators/null-forgiving` ("suppresses nullable-state warnings ...
  has no effect at run time"). LOAD-BEARING SEMANTIC NUANCE: Dart
  `!` throws at runtime on null; C# `!` does not. The SUT-side
  `BytecodeProgram.Labels` indexer must therefore be designed to
  throw on missing keys to preserve the runtime-throw guarantee — C#
  `IDictionary<TKey,TValue>` indexer throws `KeyNotFoundException` on
  missing key (Microsoft Learn `learn.microsoft.com/en-us/dotnet/api/
  system.collections.generic.idictionary-2.item`), matching the
  intent.
- **Conclusion**: emit `rt.Gq.Enqueue(new GoalRef(1,
  p.Labels["LOOP"]!));` and the same shape with `2`. Zero escalation.

### rf-dart-list-equality-to-xunit-assertequal-collection — `expect(actual, [literal-list], reason: msg)` ⇒ `Assert.Equal(new[]{...}, actual)` + inline-comment (REUSED + composed)

- **KB reuse (FR-012 / SC-007)**: the collection-equality row of the
  `expect`-matcher table was recorded in the sibling
  `test/multiagent/boot_loader_test.dart.md` spec (citation:
  `Assert.Equal(new[] { "alice", "bob", "charlie" },
  config.Directives.Select(d => d.AgentId).ToList())`). The
  `reason:`-to-`Assert.Equal` lossiness row was recorded in the
  sibling `test/conformance/fairness_26_test.dart.md` spec
  (`rf-dart-expect-bare-value-int-to-xunit-assert-equal`). This call
  site composes both reused idioms.
- **Authoritative Dart**: pub.dev
  `pub.dev/documentation/test_api/latest/expect/expect.html` — "If
  [matcher] is not a [Matcher], it will be implicitly wrapped in
  [equals]." pub.dev `package:matcher` `equals` over `Iterable`
  performs element-wise comparison.
- **Authoritative .NET**: xunit.net Assert API reference for
  `Assert.Equal<T>(IEnumerable<T> expected, IEnumerable<T> actual)`
  — verbatim EXPECTED-FIRST collection comparison. NO `userMessage`
  overload exists for `Assert.Equal<T>` (verified against xunit.net
  API reference — deliberate xUnit design choice).
- **`reason:` handling nuance (file-specific, load-bearing)**: Dart
  `expect(ran1, [1, 2], reason: 'each goal runs until its first
  yield')` supplies a `reason:` text that xUnit's `Assert.Equal`
  cannot surface. Spec routes the `reason:` text to an inline `// ...`
  comment ABOVE the assertion so the author's rationale survives
  review. Alternative — emit `Assert.True(ran1.SequenceEqual(new[]
  {1, 2}), "each goal runs until its first yield");` — was
  considered and rejected because it loses the sequence-diff
  diagnostic (xUnit `Assert.True` on a comparison reports only the
  user message, not the actual vs expected sequence diff).
- **Conclusion**: emit `// each goal runs until its first yield`
  then `Assert.Equal(new[] { 1, 2 }, ran1);`, and `// after
  re-enqueue, order remains FIFO` then `Assert.Equal(new[] { 1, 2 },
  ran2);`. EXPECTED-FIRST argument order is the load-bearing footgun
  (smoke_test.dart spec already recorded this). Element-type and
  width are owned by the SUT `scheduler.dart` spec's `Drain` return-
  type decision. Zero escalation.

## Notes

- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface in this file — the `[Fact]` method is `void` (not `async
  Task`). The well-known async-Dart-vs-.NET-async nuance is
  deliberately not asserted here (does not apply to this file's
  source surface).
- No `late`, `mixin`, `extension`, generics, sealed/abstract,
  bitwise/shift, isolate — all absent. Null-safety nuance fires
  exactly once (the `p.labels['LOOP']!` non-null assertion) and is
  addressed explicitly in the property-chain construct above.
- The file exercises the runtime's fairness/scheduler surface
  (`GlpRuntime`, `BytecodeProgram`, `BytecodeRunner`, `Scheduler`,
  `GoalRef`, `Label`, `TailStep`, `GoalQueue.enqueue`,
  `Scheduler.drain`, `Runtime.gq`, `BytecodeProgram.labels`). The
  SUT-side conversion shape (class names, method names, return
  types, constructor parameter names, indexer behaviour, opcode
  hierarchy) is owned by the SUT specs at
  `.codeconv/conversion-specs/lib/runtime/runtime.dart.md`,
  `.codeconv/conversion-specs/lib/runtime/machine_state.dart.md`,
  `.codeconv/conversion-specs/lib/runtime/scheduler.dart.md`,
  `.codeconv/conversion-specs/lib/runtime/goal_queue.dart.md`,
  `.codeconv/conversion-specs/lib/bytecode/opcodes.dart.md`, and
  `.codeconv/conversion-specs/lib/bytecode/runner.dart.md`; this test
  convspec references their decisions but does not duplicate them.
- The `reason:`-to-`Assert.Equal` lossiness nuance recorded in the
  sibling fairness_26 spec is REUSED here for collection-equality
  (composing two prior idioms: collection-equality routing and
  `reason:`-to-inline-comment). Recorded as a reusable consideration
  for any future test file using `expect(<list>, <list>, reason:
  ...)`.
- The Dart `!` non-null assertion vs. C# `!` null-forgiving
  semantic-divergence nuance (Dart throws, C# does not) is recorded
  as a load-bearing nuance — SUT-side indexers / property getters
  must be designed to throw at runtime on missing/null to preserve
  the Dart runtime-throw guarantee. The C# `IDictionary` indexer
  used by `BytecodeProgram.Labels` does throw `KeyNotFoundException`
  on missing keys, satisfying that intent for this file's specific
  use.
- Zero escalations: every construct is authoritative-supported on
  both sides, the majority REUSE idioms/findings from sibling specs
  (smoke_test.dart, glp_runtime_test.dart, fairness_26_test.dart,
  boot_loader_test.dart) per FR-012 / SC-007 KB-reuse decision
  order, and the two file-specific nuances (named-argument
  preservation, `!` runtime-vs-compile-time divergence) are recorded
  as reusable considerations for future test conversions.
