# Conversion Spec — test/multiagent/mad_transactions_test.dart

> Conversion-spec artifact for test/multiagent/mad_transactions_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/mad_transactions_test.dart
source_sha256: 6f95521ac3a698eebba120929ac864f47b7195345b088b7cf5c62a8df86a15a0
target_code_unit: test/multiagent/MadTransactionsTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and replace
      with `using Xunit;` at file scope. xUnit is the project-wide test
      framework already pinned by every prior test-file convspec
      (test/smoke_test.dart.md, test/multiagent/mad_error_handling_test.dart.md,
      test/multiagent/boot_loader_test.dart.md,
      test/multiagent/global_writers_table_test.dart.md,
      test/multiagent/globalize_test.dart.md,
      test/multiagent/localize_test.dart.md,
      test/multiagent/global_send_test.dart.md,
      test/multiagent/mad_scenarios_test.dart.md,
      test/multiagent/mad_cold_call_isolate_test.dart.md). THIS file MUST
      reuse that idiom verbatim (FR-012 / SC-007) — no re-research. The .NET
      test project (.csproj — out of this single-file artifact's scope)
      provides `xunit` + `xunit.runner.visualstudio` +
      `Microsoft.NET.Test.Sdk` NuGet references. Codegen projects to a
      single namespace mirroring the Dart `test/multiagent` directory (e.g.
      `<RootNs>.Test.Multiagent`). Codegen MUST also add
      `using System;` at file scope because `Assert.Throws<T>` and the test
      callback's local-variable capture of `(string, OutboundMessage)`
      tuple types resolve through `System.ValueTuple` (a part of
      `System.Runtime`; the `using System;` covers `ValueTuple` literals and
      `InvalidOperationException` for `throwsStateError` translation; see
      dart.package_test.expect_throwsStateError below). Codegen MUST also
      add `using System.Collections.Generic;` because the test body
      materialises a `List<(string, OutboundMessage)>` literal (the `sent`
      capture list — see
      dart.expression.generic_list_of_tuple_local_variable_with_collection_add below).
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice nuance (load-bearing, explicitly addressed): xUnit
      pinned project-wide; NUnit / MSTest recorded as alternatives in the
      research-finding row but NOT used here. Module/namespace nuance:
      Dart's `package:test` exposes top-level functions (`group`, `test`,
      `expect`, `isA`, `isNull`, `isNotNull`, `throwsStateError`)
      re-exported via the one import; xUnit has NO top-level test
      functions — tests are public instance methods on a public class
      discovered via `[Fact]` reflection. No async / Future / Stream /
      isolate surface in this file (every test body is synchronous heap +
      `MadContext` + delegate-field orchestration). The
      `throwsStateError` matcher used at lines 112-119 and 127-134 is the
      FIRST executable exception-asserting use in the multiagent test
      specs — recorded under
      `dart.package_test.expect_throwsStateError` below and pinned via
      the already-recorded `binding_pointer_test.dart.md`
      `rf-dart-expect-throwsStateError-to-xunit-assert-throws-ioe` idiom.
  - construct_key: dart.package_test.import_sut_relative_package
    source_form: >-
      "import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/terms.dart';
       import 'package:glp_runtime/multiagent/mad_context.dart';
       import 'package:glp_runtime/multiagent/message_queue.dart';
       import 'package:glp_runtime/multiagent/mad_helpers.dart';
       import 'package:glp_runtime/multiagent/global_send.dart';"
    target_decision: >-
      All six imports are SUT (system-under-test) references — Dart
      `package:glp_runtime/...` URIs that resolve to the converted C#
      namespaces for the same source units. Replace each with a C# `using`
      directive that names the namespace the converted SUT files emit
      into. Two distinct namespaces are involved: the `runtime/` subset
      (two of the six imports — `runtime.dart`, `terms.dart`) emits into
      the runtime namespace (`using <RootNs>.Runtime;` per sibling SUT
      specs `.codeconv/conversion-specs/lib/runtime/runtime.dart.md` and
      `.../lib/runtime/terms.dart.md`), and the `multiagent/` subset (four
      of the six — `mad_context.dart`, `message_queue.dart`,
      `mad_helpers.dart`, `global_send.dart`) emits into the multiagent
      namespace (`using <RootNs>.Multiagent;` per sibling SUT specs
      `.../lib/multiagent/mad_context.dart.md`,
      `.../lib/multiagent/message_queue.dart.md`,
      `.../lib/multiagent/mad_helpers.dart.md`,
      `.../lib/multiagent/global_send.dart.md`). Codegen MUST emit
      `using`s that resolve every symbol this test references: from
      runtime — `GlpRuntime` (constructor + `Heap` property +
      `Heap.AllocateVariable`, `Heap.BindVariable`, `Heap.DerefAddr`),
      `ConstTerm` (positional ctor with object-typed `Value`); from
      multiagent — `MadContext` (positional ctor `MadContext(string
      agentId, GlpRuntime runtime)` as pinned by sibling
      `.../lib/multiagent/mad_context.dart.md` + instance members `Wp`,
      `Mp`, `OnMessageReady`, `RegisterGlobalSendSpawns`,
      `HandleMadAssignment`, `HandleMadAssignmentWithGlobalNames`,
      `OnWriterBound`, `FlushMessages`), top-level helpers `Globalize` /
      `Localize` (both static methods on `MadHelpers` per sibling
      `.../lib/multiagent/mad_helpers.dart.md`), `TermVar`,
      `TermVar.Reader` / `TermVar.Writer` (static factories — pinned by
      same SUT spec), `GlobalName`, `GlobalName.Writer` /
      `GlobalName.Reader` (static factories — same SUT spec),
      `GlobalWritersTable` members `AddGlobalizeEntry` /
      `AddLocalizeEntry` / `LookupByIndex` / `FindByRemote` /
      `GlobalizeEntryCount` (per
      `.../lib/multiagent/global_writers_table.dart.md`). The
      `message_queue` import resolves `OutboundMessage` (constructed
      directly at line 144 — see
      dart.class.named_required_parameter_constructor_invocation below)
      and `MessageType` (referenced as `MessageType.assignment` at lines
      146 and 159 — see dart.expression.enum_dotted_member_access below).
      The `global_send` import is needed for the
      `RegisterGlobalSendSpawns` argument type
      (`IReadOnlyList<GlobalSendSpawn>`) per the
      `.../lib/multiagent/global_send.dart.md` SUT spec.
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed and IDENTICAL to
      mad_scenarios_test.dart.md / global_send_test.dart.md /
      globalize_test.dart.md): a `package:` import that resolves to an
      in-repo Dart library (NOT to a pub.dev third-party package) maps to
      a C# `using <Namespace>;` that targets the OUTPUT namespace of the
      converted Dart library — NOT a separate NuGet reference. Distinguish
      by inspecting the `package:` URI prefix against the host repo's
      `pubspec.yaml` `name:` (here, `glp_runtime`). Project-file wiring
      (`<ProjectReference>` from the test .csproj to the runtime .csproj)
      is langpair/project-skeleton level, recorded so codegen knows the
      `using` alone is insufficient without the project reference.
      Two-namespace-collapse nuance: the six SUT imports collapse to
      exactly TWO `using` directives (`using <RootNs>.Runtime;` + `using
      <RootNs>.Multiagent;`) — codegen emits two, not six. The
      `mad_helpers.dart` import is needed for the top-level free
      functions `globalize`/`localize`, which the convspec for
      `mad_helpers.dart` maps to STATIC METHODS on a `MadHelpers` static
      class; callers reference them as `MadHelpers.Globalize(...)` /
      `MadHelpers.Localize(...)` UNLESS codegen emits an additional
      `using static <RootNs>.Multiagent.MadHelpers;` at file scope (which
      it SHOULD — the two end-to-end tests (Direct Communication, Return
      Value) call both `globalize(...)` and `localize(...)` unqualified,
      matching the Dart call-site shape). `message_queue` and
      `global_send` namespaces collapse into the same `using
      <RootNs>.Multiagent;` because all four `lib/multiagent/*.dart` SUT
      specs pin the same namespace.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group(...); group(...); group(...); group(...); }"
    target_decision: >-
      Drop Dart `void main()` entirely — xUnit discovers `[Fact]` methods
      on `public` classes by reflection; there is no per-file entrypoint
      to emit. The FOUR sibling `group(...)` calls inside `main` become
      FOUR sibling test classes at the file's namespace scope (see next
      construct).
    idiom_id: rf-dart-test-main-to-xunit-class-with-facts
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Lifecycle nuance (explicitly addressed, IDENTICAL to
      mad_scenarios_test.dart.md): Dart `main` runs once per test-file
      process and registers tests; xUnit has no per-file hook — only
      per-class (constructor + `IDisposable.Dispose`) and per-collection
      fixtures. THIS file's `main` body is exactly four sibling
      `group(...)` calls with no other statements, so omitting `main` is
      lossless. No `setUp` / `setUpAll` / `tearDown` / `tearDownAll`
      anywhere in this file, so no constructor or `IDisposable.Dispose`
      content is needed. Four-sibling-groups nuance (REUSED VERBATIM from
      mad_scenarios_test.dart.md): the four sibling groups ('Receive
      Transaction', 'Send Transaction', 'Direct Communication Scenario',
      'Return Value Scenario') become FOUR sibling public classes under
      the same namespace — NOT a nested-class layout. xUnit
      `[Trait("Group", "<original label>")]` on each class preserves the
      group label for reporter parity (the labels are scenario-oriented
      English without spec-section numbers, so the trait is the sole
      label carrier in this file — distinct from
      mad_scenarios_test.dart.md, whose labels carry `madGLP-spec.md`
      section numbers).
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('Receive Transaction', () { test(...); test(...); test(...); test(...); test(...); });
       group('Send Transaction', () { test(...); });
       group('Direct Communication Scenario', () { test(...); });
       group('Return Value Scenario', () { test(...); });"
    target_decision: >-
      Each Dart `group(label, body)` maps to a separate `public class
      <Label>Tests`. Group-label-to-class-name mangling strips
      non-identifier characters (spaces only — none of the four labels
      contain colons, parentheses, hyphens, dots, or digits) and
      PascalCases the remaining tokens, then appends `Tests`. Specifically:
      `'Receive Transaction'` -> `ReceiveTransactionTests`;
      `'Send Transaction'` -> `SendTransactionTests`;
      `'Direct Communication Scenario'` -> `DirectCommunicationScenarioTests`;
      `'Return Value Scenario'` -> `ReturnValueScenarioTests`.
      The original label MUST be preserved via
      `[Trait("Group", "<original label>")]` on each class for reporter
      parity. No nested `group(...)`, no `setUp`/`tearDown` inside any
      group — each test constructs its own per-agent `GlpRuntime` +
      `MadContext` pair (one pair for the Receive Transaction and Send
      Transaction tests; two pairs each for Direct Communication
      Scenario / Return Value Scenario) locally, so xUnit's per-test
      fresh-instance lifecycle ("xUnit.net creates a new instance of the
      test class for every test that is run") maps cleanly with NO
      shared state and NO constructor-side fixture needed.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance (SIMPLER than mad_scenarios_test.dart.md — all
      four labels in THIS file are space-separated English words with no
      punctuation or digits): the mangling reduces to PascalCase + space
      stripping. Sibling-groups-NOT-nested-groups nuance: SAME as
      mad_scenarios_test.dart.md — the four groups are SIBLING inside
      `main`, neither nested in the other; the documented mapping is
      four SEPARATE classes. Per-test labels in this file are
      sentence-form descriptions (e.g. `'_w(p,i) message: finds
      GlobalizeEntry by index, binds writer'`) — see next construct for
      the per-test method-name mangling rule. Per-class test-count
      nuance: ReceiveTransactionTests carries 5 `[Fact]` methods; the
      other three classes each carry exactly 1 `[Fact]` method —
      asymmetric per-class density preserved verbatim from the source.
  - construct_key: dart.package_test.test_call_executable
    source_form: >-
      "test('_w(p,i) message: finds GlobalizeEntry by index, binds writer', () { ... });
       test('_r(p,i) message: finds LocalizeEntry, binds writer', () { ... });
       test('receive localizes nested variables', () { ... });
       test('receive for non-existent GlobalizeEntry throws', () { ... });
       test('receive for non-existent LocalizeEntry throws', () { ... });
       test('flushMessages sends queued messages', () { ... });
       test('p sends X to q, p assigns X := 1, q receives value', () { ... });
       test('p sends V? to q, q assigns V := result, p receives result', () { ... });"
    target_decision: >-
      Each Dart `test(label, body)` (no `skip` argument anywhere) becomes
      a `public void` method on the enclosing class, decorated with
      `[Fact(DisplayName = "<original label>")]`. Method name = label
      PascalCased with non-identifier chars stripped (commas, colons,
      parentheses, the question-mark, the assignment-operator characters
      `:=`, the underscore-prefixed spec notation `_w(p,i)` / `_r(p,i)`
      reduced to a token-safe form by dropping `_(,)` and PascalCasing).
      Specifically:
      `'_w(p,i) message: finds GlobalizeEntry by index, binds writer'` ->
      `WPiMessageFindsGlobalizeEntryByIndexBindsWriter`;
      `'_r(p,i) message: finds LocalizeEntry, binds writer'` ->
      `RPiMessageFindsLocalizeEntryBindsWriter`;
      `'receive localizes nested variables'` ->
      `ReceiveLocalizesNestedVariables`;
      `'receive for non-existent GlobalizeEntry throws'` ->
      `ReceiveForNonExistentGlobalizeEntryThrows`;
      `'receive for non-existent LocalizeEntry throws'` ->
      `ReceiveForNonExistentLocalizeEntryThrows`;
      `'flushMessages sends queued messages'` ->
      `FlushMessagesSendsQueuedMessages`;
      `'p sends X to q, p assigns X := 1, q receives value'` ->
      `PSendsXToQPAssignsX1QReceivesValue`;
      `'p sends V? to q, q assigns V := result, p receives result'` ->
      `PSendsVToQQAssignsVResultPReceivesResult`.
      Method body translates the Dart arrange-act-assert verbatim, with
      `expect(actual, matcher)` calls routed to xUnit `Assert.*` per the
      matcher-routing idioms below
      (`rf-dart-expect-equals-to-xunit-assertequal`,
      `rf-dart-expect-isA-to-xunit-assert-istype`,
      `rf-dart-expect-isnull-to-xunit-assert-null`,
      `rf-dart-expect-isnotnull-to-xunit-assertnotnull`,
      `rf-dart-expect-throwsStateError-to-xunit-assert-throws-ioe`). The
      Given/When/Then-style comments at the top of each test body MUST
      carry into the target as a `/// <summary>` doc-comment block per
      method so the spec-link traceability survives the conversion
      (FR-024 doc-level — IDENTICAL to the spec-traceability rule applied
      in mad_scenarios_test.dart.md). For the two end-to-end tests
      (Direct Communication / Return Value) the EXTENSIVE in-body
      "Corrected definitions:" / "Wait — this is the REVERSE direction
      ..." commentary blocks are LOAD-BEARING — they encode the bound
      semantics each scenario tests (writer-side vs reader-side
      globalize, with-spawn vs no-spawn outcomes). Codegen MUST preserve
      these blocks verbatim inside the `/// <summary>` (or as inline
      `// ...` comments) so the conversion does not silently drop the
      annotated proof-of-correctness.
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Method-body translation nuance (explicitly addressed): every `test`
      callback in this file is synchronous (no `async`/`Future`/`await`);
      target method returns `void` (xUnit also supports `async Task` for
      async tests — not applicable here). Closure-capture nuance: every
      `final ctx = ...`, `final runtime = ...`, `final (writerAddr,
      readerAddr) = ...`, `final index = ...`, `final globalName = ...`,
      `final sent = ...`, `final count = ...`, `final derefed = ...`,
      `final ...Result = ...`, `final globalizeResult = ...`, `final
      localizeResult = ...` is local to the test body, mapping 1-to-1 to
      local `var <name> = ...` in the C# method (see
      dart.expression.final_local_variable_with_initializer). Two test
      bodies (Direct Communication / Return Value) ALSO assign the
      `onMessageReady` field on a `MadContext` instance inside the test
      body — this is a statement-bodied lambda assignment to a
      delegate-typed field (see
      dart.expression.statement_bodied_lambda_assigned_to_delegate_field).
      Skip-semantics nuance (NOT firing here): no `skip:` argument
      anywhere, so NO `Skip=` property on `[Fact]`.
      Exception-asserting-test nuance (NEW relative to
      mad_scenarios_test.dart.md): two test bodies (lines 107-120,
      122-135) consist of arrange + a single `expect(() => ...,
      throwsStateError)` — the C# method body is correspondingly
      arrange + `Assert.Throws<InvalidOperationException>(() => ...)`
      with NO trailing assertions; see
      dart.package_test.expect_throwsStateError below.
  - construct_key: dart.expression.final_local_variable_with_initializer
    source_form: >-
      "final runtime = GlpRuntime();
       final ctx = MadContext(agentId: 'p', runtime: runtime);
       final (writerAddr, readerAddr) = runtime.heap.allocateVariable();
       final index = ctx.wp.addGlobalizeEntry(writerAddr, 'q');
       final globalName = GlobalName.writer('p', index);
       final derefed = runtime.heap.derefAddr(writerAddr);
       final sent = <(String, OutboundMessage)>[];
       final count = ctx.flushMessages();
       final runtimeP = GlpRuntime();
       final runtimeQ = GlpRuntime();
       final ctxP = MadContext(agentId: 'p', runtime: runtimeP);
       final ctxQ = MadContext(agentId: 'q', runtime: runtimeQ);
       final (writerXp, readerXp) = runtimeP.heap.allocateVariable();
       final globalizeResult = globalize(variables: [...], localAgent: 'p', remoteAgent: 'q', table: ctxP.wp);
       final localizeResult = localize(globalNames: ..., localAgent: 'q', table: ctxQ.wp, freshAddrAllocator: () => runtimeQ.heap.allocateVariable());
       final writerZq = localizeResult.freshPairs[0].writerAddr;
       final writerYq = localizeResult.freshPairs[0].writerAddr;
       final (writerVp, readerVp) = runtimeP.heap.allocateVariable();
       final nestedGlobalNames = [GlobalName.reader('q', 2)];"
    target_decision: >-
      Translate `final <name> = <expr>;` to `var <name> = <expr>;` in C#
      where the initializer is a constructor invocation, a method call, a
      list literal, a property access, a cast expression, or a tuple
      destructuring expression. Specifically:
      `final runtime = GlpRuntime()` -> `var runtime = new GlpRuntime();`
      (note the mandatory C# `new` keyword — Dart's optional-`new`
      constructor call requires C#'s explicit `new`);
      `final ctx = MadContext(agentId: 'p', runtime: runtime)` -> per
      the SUT spec `lib/multiagent/mad_context.dart.md` which collapses
      the Dart named-required ctor to a positional C# ctor, this becomes
      `var ctx = new MadContext("p", runtime);` (call-site loses the
      `agentId:`/`runtime:` named-arg labels — same load-bearing nuance
      as mad_scenarios_test.dart.md);
      `final index = ctx.wp.addGlobalizeEntry(writerAddr, 'q')` ->
      `var index = ctx.Wp.AddGlobalizeEntry(writerAddr, "q");` (instance
      method call returning `int` per
      `lib/multiagent/global_writers_table.dart.md` — the SUT pins
      `AddGlobalizeEntry(int writerAddr, string remoteAgent) -> int`,
      returning the allocated 1-based index);
      `final globalName = GlobalName.writer('p', index)` ->
      `var globalName = GlobalName.Writer("p", index);` (named-ctor ->
      static factory per
      `rf-dart-named-constructor-to-csharp-static-factory`);
      `final derefed = runtime.heap.derefAddr(writerAddr)` ->
      `var derefed = runtime.Heap.DerefAddr(writerAddr);`;
      `final sent = <(String, OutboundMessage)>[]` ->
      `var sent = new List<(string, OutboundMessage)>();` (Dart 3
      typed-empty-list literal with a record element type → C# `new
      List<ValueTuple<string, OutboundMessage>>()` written as `new
      List<(string, OutboundMessage)>()` — see
      dart.expression.generic_list_of_tuple_local_variable_with_collection_add
      below);
      `final count = ctx.flushMessages()` ->
      `var count = ctx.FlushMessages();` (returns `int` per SUT spec);
      `final globalizeResult = globalize(variables: ..., localAgent: ...,
      remoteAgent: ..., table: ...)` ->
      `var globalizeResult = MadHelpers.Globalize(variables: ...,
      localAgent: ..., remoteAgent: ..., table: ...);` (top-level Dart
      function -> static method on `MadHelpers`, named arguments
      preserved — UNLESS `using static <RootNs>.Multiagent.MadHelpers;`
      is also emitted at file scope, in which case codegen MAY drop the
      `MadHelpers.` qualifier);
      `final localizeResult = localize(globalNames: ..., localAgent: ...,
      table: ..., freshAddrAllocator: () =>
      runtimeQ.heap.allocateVariable())` ->
      `var localizeResult = MadHelpers.Localize(globalNames: ...,
      localAgent: ..., table: ..., freshAddrAllocator: () =>
      runtimeQ.Heap.AllocateVariable());` (the lambda is a zero-arg
      arrow lambda — see dart.expression.lambda_zero_arg_arrow);
      `final writerZq = localizeResult.freshPairs[0].writerAddr` ->
      `var writerZq = localizeResult.FreshPairs[0].WriterAddr;`
      (indexer + PascalCased property chain per
      mad_scenarios_test.dart.md `dart.expression.indexed_property_access`);
      `final nestedGlobalNames = [GlobalName.reader('q', 2)]` ->
      `var nestedGlobalNames = new List<GlobalName> {
      GlobalName.Reader("q", 2) };` (single-element typed list literal —
      see dart.expression.list_literal_typed_polymorphic below).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability-semantics nuance (IDENTICAL to mad_scenarios_test.dart.md):
      Dart `final <local>` prevents REBINDING the local after init but
      does NOT prevent mutation of the referenced object's state —
      exactly the same semantics as C# `var`. Constructor-syntax nuance:
      Dart allows `Foo(...)` without `new`; C# requires `new Foo(...)`.
      String-literal nuance: Dart `'p'` / `'q'` / `'placeholder'` are
      single-quoted strings; C# uses ONLY `"..."` for `string`. Codegen
      MUST emit `new MadContext("p", runtime)` — single-quote literals
      would be `char` in C# and select non-existent `char`-arg
      constructors. Numeric-literal nuance (REUSED from
      mad_scenarios_test.dart.md): `ConstTerm(42)` (lines 36, 67, 98,
      115, 131), `ConstTerm(1)` (line 223, 229), `ConstTerm(42)` again
      (line 294, 300) use Dart `int` literals — passed to a `ConstTerm`
      ctor that accepts `Object?`. In C# the literal `42` defaults to
      `int`; `new ConstTerm(42)` boxes to `object` at the parameter
      boundary (`ConstTerm`'s `Value` property is declared `object?` per
      the SUT spec `lib/runtime/terms.dart.md`). Semantics agree. Empty
      typed list literal nuance (NEW for this file —
      mad_scenarios_test.dart.md only had non-empty lists): Dart
      `<T>[]` is an empty list with explicit element type; C# equivalent
      is `new List<T>()` (the zero-arg ctor — NOT `new List<T> { }`
      which is grammatically valid but the empty-collection-initializer
      form is stylistically equivalent and codegen MAY emit either).
  - construct_key: dart.expression.record_destructuring_pattern_assignment
    source_form: >-
      "final (writerAddr, readerAddr) = runtime.heap.allocateVariable();
       final (writerAddr, _) = runtime.heap.allocateVariable();
       final (writerXp, readerXp) = runtimeP.heap.allocateVariable();
       final (writerVp, readerVp) = runtimeP.heap.allocateVariable();"
    target_decision: >-
      Dart 3 RECORD-DESTRUCTURING PATTERN: `final (a, b) = expr;` where
      `expr` returns a positional record `(T1, T2)`. The
      `Heap.allocateVariable()` method returns a positional record `(int
      writerAddr, int readerAddr)` per the sibling SUT spec
      `lib/runtime/heap_fcp.dart.md`. The pinned idiom from
      `lib/multiagent/mad_context.dart.md`
      (`rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction`)
      is reused verbatim (FR-012 / SC-007 — no re-research).
      Specifically: `final (writerAddr, readerAddr) =
      runtime.heap.allocateVariable()` -> `var (writerAddr, readerAddr)
      = runtime.Heap.AllocateVariable();` (C# tuple deconstruction with
      `var` on the OUTER side; both elements inferred as `int`). Apply
      uniformly to all FOUR occurrences in this file: line 26 (Receive
      Transaction test 1), line 58 (test 2), line 89 (test 3 — the
      `_` discard form, see nuance), line 184 (Direct Communication),
      line 255 (Return Value).
      The SUT method MUST return a `(int, int)` ValueTuple per the heap
      SUT convspec.
      DISCARD-FORM SUBCASE (line 89): `final (writerAddr, _) =
      runtime.heap.allocateVariable()` uses the Dart 3 `_` wildcard
      pattern to discard the second tuple element. C# 7+ supports the
      same wildcard via the `_` discard-pattern in deconstruction
      assignment: `var (writerAddr, _) = runtime.Heap.AllocateVariable();`
      is valid C# and discards the second element identically. No new
      idiom row needed — `_` discard is part of the same C# tuple
      deconstruction grammar.
    idiom_id: rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction
    research_finding_id: rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction
    nuance: >-
      Record-vs-tuple nuance (explicitly addressed, IDENTICAL to
      mad_scenarios_test.dart.md / lib/multiagent/mad_context.dart.md):
      Dart 3 records are STRUCTURAL (positional/named field shape, no
      nominal type at the use site); C# ValueTuples are STRUCTURAL by
      NAME at the language level (tuple field names are hints, the
      underlying type is `ValueTuple<T1,T2,...>` which IS structural).
      Equality nuance: Dart records have value-equality (positional
      fields compared by `==`); C# `ValueTuple` overrides `Equals` /
      `GetHashCode` to compare element-wise — semantics agree.
      Discard-pattern nuance (NEW for this file relative to
      mad_scenarios_test.dart.md): Dart 3 `_` wildcard and C# 7+ `_`
      discard pattern are SYNTACTICALLY identical and SEMANTICALLY
      identical (both bind nothing and allow the slot's value to be
      garbage-collected if no other reference). Authoritative Dart side:
      [dart.dev language tour `Patterns / Wildcard`](https://dart.dev/language/patterns#wildcard).
      Authoritative .NET side: [Microsoft Learn `Discards`](https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/discards).
      Async/Future: ABSENT — `allocateVariable` is synchronous in both
      languages. Single-thread ownership nuance: heap operations are
      agent-owned per the MadContext-owns-runtime invariant pinned by
      `lib/multiagent/mad_context.dart.md` — no concurrent access; no
      lock/synchronisation needed.
  - construct_key: dart.class.named_constructor_factory
    source_form: >-
      "GlobalName.writer('p', index)
       GlobalName.reader('p', 3)
       GlobalName.writer('p', 5)
       GlobalName.reader('p', 5)
       GlobalName.writer('p', index)        // second use, inside nested test
       GlobalName.reader('q', 2)
       TermVar.reader(readerXp, writerAddr: writerXp)
       TermVar.writer(writerVp, readerAddr: readerVp)"
    target_decision: >-
      Dart's NAMED CONSTRUCTORS (`ClassName.namedCtor(...)`) — used here
      for `GlobalName.writer` / `GlobalName.reader` and for
      `TermVar.reader` / `TermVar.writer` — have NO direct C#
      equivalent. The pinned mapping (recorded by globalize_test.dart.md
      and reused by global_send_test.dart.md / mad_scenarios_test.dart.md
      as `rf-dart-named-constructor-to-csharp-static-factory`) is reused
      verbatim (FR-012 / SC-007): Dart `Foo.bar(args)` -> C# `Foo.Bar(args)`
      STATIC FACTORY METHOD on the converted class. So
      `GlobalName.writer('p', index)` -> `GlobalName.Writer("p", index)`;
      `GlobalName.reader('p', 3)` -> `GlobalName.Reader("p", 3)`;
      `TermVar.reader(readerXp, writerAddr: writerXp)` ->
      `TermVar.Reader(readerXp, writerAddr: writerXp)`;
      `TermVar.writer(writerVp, readerAddr: readerVp)` ->
      `TermVar.Writer(writerVp, readerAddr: readerVp)`. The factory
      method name is the named-constructor identifier PascalCased. The
      SUT specs `.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md`
      (for `TermVar` / `GlobalName`) are the source of truth for the
      exact static-factory signatures emitted.
    idiom_id: rf-dart-named-constructor-to-csharp-static-factory
    research_finding_id: rf-dart-named-constructor-to-csharp-static-factory
    nuance: >-
      Constructor-semantics nuance (explicitly addressed and IDENTICAL
      to mad_scenarios_test.dart.md / global_send_test.dart.md): Dart
      named constructors are CONSTRUCTORS; C# static factories are
      METHOD CALLS returning `new Foo(...)`. The ALTERNATIVE C#
      encoding — multiple constructor overloads disambiguated by
      parameter type — was rejected for `TermVar` because
      `TermVar.writer(int, {int readerAddr})` and `TermVar.reader(int,
      {int writerAddr})` differ ONLY by named-parameter LABEL, not by
      type signature — two `(int, int)` constructors would conflict.
      Same applies for `GlobalName.writer(String, int)` vs
      `GlobalName.reader(String, int)` (both `(string, int)` shapes).
      Same-class-different-tag nuance: `GlobalName` is a sealed
      two-flavour record in Dart (writer-flavour vs reader-flavour,
      differentiated by an internal discriminator field); the C# port
      preserves the same shape via a single sealed class with a `Kind`
      enum discriminator (per SUT spec `mad_helpers.dart.md`).
  - construct_key: dart.class.named_required_parameter_constructor_invocation
    source_form: >-
      "MadContext(agentId: 'p', runtime: runtime)
       MadContext(agentId: 'q', runtime: runtime)
       MadContext(agentId: 'p', runtime: runtimeP)
       MadContext(agentId: 'q', runtime: runtimeQ)
       OutboundMessage(destination: 'q', type: MessageType.assignment, payload: [1, 2, 3])
       ctx.handleMadAssignment(globalName: ..., value: ..., fromAgent: 'q')
       ctx.handleMadAssignment(globalName: ..., value: ..., fromAgent: 'p')
       ctx.handleMadAssignmentWithGlobalNames(globalName: ..., value: ..., nestedGlobalNames: ..., fromAgent: 'q')
       ctxP.handleMadAssignment(globalName: ..., value: ..., fromAgent: 'q')
       ctxQ.handleMadAssignment(globalName: ..., value: ..., fromAgent: 'p')
       globalize(variables: [...], localAgent: 'p', remoteAgent: 'q', table: ctxP.wp)
       localize(globalNames: ..., localAgent: 'q', table: ctxQ.wp, freshAddrAllocator: () => runtimeQ.heap.allocateVariable())"
    target_decision: >-
      Dart's PRIMARY NAMED-REQUIRED constructor / NAMED-REQUIRED method-
      parameter invocation forms map to call-site translations whose
      target depends on the SUT spec for each callee:
      (a) `MadContext(agentId: ..., runtime: ...)` — the SUT spec
      `lib/multiagent/mad_context.dart.md` explicitly COLLAPSES the Dart
      named-required ctor to a POSITIONAL C# ctor `public
      MadContext(string agentId, GlpRuntime runtime)`. So the call site
      drops the `agentId:` / `runtime:` named-arg labels:
      `MadContext(agentId: 'p', runtime: runtime)` -> `new
      MadContext("p", runtime)`. LOAD-BEARING DEVIATION from the general
      "preserve named args verbatim" rule (REUSED VERBATIM from
      mad_scenarios_test.dart.md).
      (b) `OutboundMessage(destination: 'q', type: MessageType.assignment,
      payload: [1, 2, 3])` — the SUT spec
      `lib/multiagent/message_queue.dart.md` (construct row 17-22) pins
      `OutboundMessage` with get-only properties `Destination` / `Type`
      / `Payload` and a single positional C# constructor (the Dart
      named-required ctor is collapsed to positional). So:
      `OutboundMessage(destination: 'q', type: MessageType.assignment,
      payload: [1, 2, 3])` -> `new OutboundMessage("q",
      MessageType.Assignment, new List<int> { 1, 2, 3 });` — the named
      args are dropped at the call site (positional C# ctor); the
      `MessageType.assignment` enum-member becomes
      `MessageType.Assignment` (PascalCased per
      `lib/multiagent/message_queue.dart.md` enum mapping); the payload
      list literal `[1, 2, 3]` becomes a `List<int>` collection-initializer
      (see dart.expression.list_literal_int_collection_initializer
      below).
      (c) `globalize(...)` / `localize(...)` — top-level Dart functions,
      converted per `mad_helpers.dart.md` to STATIC methods on
      `MadHelpers` with named PARAMETERS preserved (callable as
      `MadHelpers.Globalize(variables: ..., localAgent: ...,
      remoteAgent: ..., table: ...)` or unqualified under `using
      static`).
      (d) `ctx.handleMadAssignment(globalName: ..., value: ...,
      fromAgent: ...)` and `ctx.handleMadAssignmentWithGlobalNames(
      globalName: ..., value: ..., nestedGlobalNames: ..., fromAgent: ...)`
      — instance methods on `MadContext` per `mad_context.dart.md`
      with named parameters preserved; C# call site emits
      `ctx.HandleMadAssignment(globalName: ..., value: ..., fromAgent:
      "q")` (PascalCased method, camelCase named arguments). The
      `HandleMadAssignmentWithGlobalNames` method is a four-named-parameter
      overload that accepts the nested-global-names list — pinned by
      the SUT spec; C# preserves the four named arguments verbatim.
      All Dart `{required Type name}` parameters MUST translate to C#
      parameters WITHOUT default values (so the compiler enforces the
      "must be supplied" guarantee) — IDENTICAL to the
      global_send_test.dart.md / mad_scenarios_test.dart.md nuance.
    idiom_id: rf-dart-named-argument-to-csharp-named-argument
    research_finding_id: rf-dart-named-argument-to-csharp-named-argument
    nuance: >-
      Required-vs-optional nuance: see global_send_test.dart.md.
      Order-independence nuance: Dart named arguments may appear in any
      order; C# named arguments may also appear in any order — this file
      preserves the source call-site order verbatim.
      SUT-spec-determines-call-shape nuance (LOAD-BEARING, IDENTICAL to
      mad_scenarios_test.dart.md): both `MadContext` AND `OutboundMessage`
      ctors at every call site here use Dart named args, but the C# SUT
      ctors are POSITIONAL (per the explicit `mad_context.dart.md` and
      `message_queue.dart.md` decisions). Codegen MUST consult the
      per-callee SUT spec — there is no file-local rule. By contrast
      `globalize` / `localize` / `handleMadAssignment` /
      `handleMadAssignmentWithGlobalNames` SUT specs pin NAMED C#
      parameters; their call sites preserve the labels.
      HandleMadAssignmentWithGlobalNames first-recorded nuance (NEW for
      this file — mad_scenarios_test.dart.md never invoked it): four
      named-required parameters (`globalName`, `value`,
      `nestedGlobalNames`, `fromAgent`); the SUT spec pins the C#
      signature as `public void HandleMadAssignmentWithGlobalNames(
      GlobalName globalName, Term value, IReadOnlyList<GlobalName>
      nestedGlobalNames, string fromAgent)` — codegen MUST emit all four
      named arguments at the call site to preserve the readability of
      the Dart source.
  - construct_key: dart.expression.enum_dotted_member_access
    source_form: >-
      "MessageType.assignment       // line 146 (OutboundMessage ctor)
       msg.type                    // line 159 (read access on OutboundMessage.type)
       MessageType.assignment       // implicit: the second use is in the
                                    // expect(sent[0].$2.type, MessageType.assignment)"
    target_decision: >-
      Dart enum member access `MessageType.assignment` maps to C# enum
      member access `MessageType.Assignment` with PascalCased member
      name per the SUT spec `lib/multiagent/message_queue.dart.md`
      construct row 11-12 ("C# enum MessageType with two members
      Assignment and AgentMessage (PascalCase per .NET naming
      convention)"). Specifically:
      `MessageType.assignment` (constructor argument) ->
      `MessageType.Assignment`;
      `sent[0].$2.type` (property read returning the enum value) ->
      `sent[0].Item2.Type` (the underlying `OutboundMessage.Type`
      property is PascalCased per the SUT spec; the `.$2` positional
      record getter is mapped per dart.expression.record_positional_getter_dollar_n
      below). Reuses the pinned enum-member idiom from the SUT spec —
      no new idiom row needed.
    idiom_id: null
    research_finding_id: rf-dart-enum-member-access-pascalcase
    nuance: >-
      Enum-naming-PascalCase nuance (explicitly addressed): Dart enum
      members are conventionally `lowerCamelCase`; C# enum members are
      conventionally `PascalCase` (per [Microsoft `Names of Enumerations`
      naming guideline](https://learn.microsoft.com/dotnet/standard/design-guidelines/names-of-classes-structs-and-interfaces#names-of-enumerations)).
      The SUT spec for `MessageType` already pins both members in
      PascalCase form (`Assignment` / `AgentMessage`); codegen at the
      call site MUST follow the SUT's pinning. Implicit-comparison
      nuance: at line 159 the source compares
      `expect(sent[0].$2.type, MessageType.assignment)` — C# enum
      equality is value-equality (enums are value types backed by an
      integral type), so `Assert.Equal(MessageType.Assignment,
      sent[0].Item2.Type)` succeeds exactly when the Dart `==` would.
      Numeric-underlying-type nuance: Dart enums and C# enums both
      default to int-backed; no width nuance applies here.
  - construct_key: dart.expression.list_literal_typed_polymorphic
    source_form: >-
      "[TermVar.reader(readerXp, writerAddr: writerXp)]
       [TermVar.writer(writerVp, readerAddr: readerVp)]
       [GlobalName.reader('q', 2)]"
    target_decision: >-
      Dart list literal `[a]` (single-element typed list) whose static
      element type is inferred from the call-site signature (here
      `List<TermVar>` for the `globalize` `variables:` parameter and
      `List<GlobalName>` for the `handleMadAssignmentWithGlobalNames`
      `nestedGlobalNames:` parameter) maps to C# `new List<T> { a }`
      (collection-initializer syntax on
      `System.Collections.Generic.List<T>`). The `using
      System.Collections.Generic;` at file scope (see
      dart.package_test.import_directive nuance) makes `List<T>`
      resolvable. Specifically:
      `[TermVar.reader(readerXp, writerAddr: writerXp)]` -> `new
      List<TermVar> { TermVar.Reader(readerXp, writerAddr: writerXp) }`;
      `[TermVar.writer(writerVp, readerAddr: readerVp)]` -> `new
      List<TermVar> { TermVar.Writer(writerVp, readerAddr: readerVp) }`;
      `[GlobalName.reader('q', 2)]` -> `new List<GlobalName> {
      GlobalName.Reader("q", 2) }`. The SUT param type
      (`IReadOnlyList<TermVar>` / `IReadOnlyList<GlobalName>`) is
      assignable from `List<T>` because `List<T>` implements
      `IReadOnlyList<T>` — no extra cast needed at the call site.
      Reuses the pinned list-literal idiom from mad_scenarios_test.dart.md.
    idiom_id: rf-dart-list-literal-to-csharp-list-initializer
    research_finding_id: rf-dart-list-literal-to-csharp-list-initializer
    nuance: >-
      Single-element-vs-multi-element nuance (NEW for this file relative
      to mad_scenarios_test.dart.md): mad_scenarios_test.dart.md
      exercised single-element AND two-element typed list literals; THIS
      file exercises only single-element typed list literals at the
      `globalize` / `handleMadAssignmentWithGlobalNames` call sites.
      Both map identically — the collection-initializer braces accept
      one or many comma-separated elements. Polymorphism nuance: the
      list element type is the explicit nominal type of the call-site
      parameter (`TermVar` / `GlobalName`), NOT the static type inferred
      from the inner element (`TermVar.Reader(...)` returns `TermVar`,
      `GlobalName.Reader(...)` returns `GlobalName`) — codegen MUST emit
      `new List<TermVar>` / `new List<GlobalName>` and NOT `new var[]`
      / `new List<>` (no diamond-operator inference at this position in
      C#). Authoritative Dart side: [dart.dev language tour `Lists`](https://dart.dev/language/collections#lists).
      Authoritative .NET side: [Microsoft Learn `Object and Collection
      Initializers`](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers).
  - construct_key: dart.expression.list_literal_int_collection_initializer
    source_form: "payload: [1, 2, 3]"
    target_decision: >-
      Dart list literal `[1, 2, 3]` whose static element type is inferred
      as `int` from the `OutboundMessage.payload` parameter declared
      `List<int>` (per `lib/multiagent/message_queue.dart.md` SUT spec,
      construct row 17 — "`final List<int> payload`"; the SUT C# emits
      `IReadOnlyList<int> Payload { get; }` and the ctor accepts
      `IReadOnlyList<int>` or `List<int>` per the SUT spec).
      Specifically: `[1, 2, 3]` -> `new List<int> { 1, 2, 3 }`
      (collection-initializer syntax). The `using
      System.Collections.Generic;` makes `List<int>` resolvable.
      Reuses the pinned list-literal idiom; the only nuance is that this
      is a value-type element (int) list, not a reference-type element
      list as in dart.expression.list_literal_typed_polymorphic above.
    idiom_id: rf-dart-list-literal-to-csharp-list-initializer
    research_finding_id: rf-dart-list-literal-to-csharp-list-initializer
    nuance: >-
      Value-type element nuance (NEW for this file): Dart `int` is a
      true integer (not boxed); C# `int` is a value type (struct); a
      `List<int>` stores unboxed `int` values directly. No boxing
      overhead on either side. Note: the SUT spec
      `lib/multiagent/message_queue.dart.md` construct row 17 source-form
      summary says `List<int> payload` — but a related construct row
      mentions `List<byte>` in the C# nuance ("non-nullable fields ->
      `string`, `MessageType`, `List<byte>`"). The byte-vs-int question
      is a SUT-level decision pinned in `message_queue.dart.md` (not
      this file); whichever the SUT pins, the test-call-site emission
      MUST match. If the SUT C# parameter is `IReadOnlyList<int>`
      codegen emits `new List<int> { 1, 2, 3 }`; if `IReadOnlyList<byte>`
      codegen emits `new List<byte> { 1, 2, 3 }` (the literals
      `1, 2, 3` are within byte range so the implicit narrowing is
      legal). Codegen MUST defer to the SUT spec for the element type.
  - construct_key: dart.expression.generic_list_of_tuple_local_variable_with_collection_add
    source_form: >-
      "final sent = <(String, OutboundMessage)>[];
       sent.add((dest, msg));"
    target_decision: >-
      Dart 3 generic list literal with a positional-record element type
      `<(String, OutboundMessage)>[]` declares an empty
      `List<(String, OutboundMessage)>`. Maps to C# `var sent = new
      List<(string, OutboundMessage)>();`. The subsequent `sent.add((dest,
      msg))` calls invoke `List<T>.Add(T)` where `T` is the positional
      tuple type; the argument `(dest, msg)` is a Dart record literal
      that maps to a C# ValueTuple literal `(dest, msg)` directly (C#
      tuple literals and Dart record literals share the same `(a, b)`
      syntax). Specifically: `sent.add((dest, msg))` -> `sent.Add((dest,
      msg));` (PascalCased `Add`). The capture list `sent` is read after
      the `flushMessages` call via `sent.length` -> `sent.Count` and
      `sent[0].$1` / `sent[0].$2` indexer accesses — see
      dart.expression.record_positional_getter_dollar_n below.
    idiom_id: null
    research_finding_id: rf-dart-record-typed-list-and-tuple-add-to-csharp-valuetuple-list
    nuance: >-
      Empty-generic-list-with-record-element nuance (NEW for this file
      and FIRST-RECORDED for the multiagent test specs):
      mad_scenarios_test.dart.md exercised only `<Type>[]` (single
      nominal-type element) list literals; THIS file exercises a Dart 3
      `<(T1, T2)>[]` list with a positional-record element type, which
      requires the C# port to instantiate `List<(T1, T2)>` (i.e.
      `List<ValueTuple<T1, T2>>`). Both sides support the syntax
      directly — Dart 3 records and C# ValueTuples have isomorphic
      positional-record/structural-tuple semantics. Authoritative Dart
      side: [dart.dev language tour `Records`](https://dart.dev/language/records).
      Authoritative .NET side: [Microsoft Learn `Tuple types`](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-tuples).
      Tuple-construction nuance (explicitly addressed): the literal
      `(dest, msg)` in Dart is a positional record literal; the literal
      `(dest, msg)` in C# is a ValueTuple literal — IDENTICAL syntax.
      Codegen MUST NOT wrap with `new ValueTuple<string,
      OutboundMessage>(dest, msg)` (more verbose, equivalent semantics).
      `.Add` casing nuance: Dart `List.add` is camelCase; C# `List.Add`
      is PascalCase — flip per the project-wide convention.
  - construct_key: dart.expression.record_positional_getter_dollar_n
    source_form: >-
      "sent[0].$1
       sent[0].$2
       sent[0].$2.type"
    target_decision: >-
      Dart 3 positional record getter `record.$N` (1-based) maps to C#
      ValueTuple positional getter `tuple.ItemN` (also 1-based — `Item1`
      / `Item2` / ...). Pinned in `localize_test.dart.md` (line 222):
      "Dart `result.$1` / `result.$2` ⇒ C# `tuple.Item1` / `tuple.Item2`".
      Specifically: `sent[0].$1` -> `sent[0].Item1` (the destination
      `string`); `sent[0].$2` -> `sent[0].Item2` (the `OutboundMessage`);
      `sent[0].$2.type` -> `sent[0].Item2.Type` (PascalCased
      `OutboundMessage.Type` property per
      `lib/multiagent/message_queue.dart.md` SUT spec).
      Reuses the pinned localize_test.dart.md idiom verbatim (FR-012 /
      SC-007).
    idiom_id: rf-dart-record-positional-getter-to-csharp-valuetuple-itemn
    research_finding_id: rf-dart-record-positional-getter-to-csharp-valuetuple-itemn
    nuance: >-
      1-based-indexing nuance (LOAD-BEARING, explicitly addressed): both
      Dart `record.$N` and C# `ValueTuple.ItemN` use 1-based field
      indexing — `$1` is the first field, `Item1` is the first field.
      Codegen MUST emit `Item1`/`Item2`/etc. NOT `Item0`/`Item1`. Named
      tuple field names DO NOT survive the positional accessor in C#
      (`var t = (Name: "x", Value: 1); t.Item1` is "x" — the name is
      lost when accessed positionally) — codegen MAY choose to emit
      named-field tuple types instead (`(string Destination,
      OutboundMessage Message)` and access via `t.Destination`), but
      the simpler positional `Item1`/`Item2` matches the Dart `$1`/`$2`
      shape and is what `localize_test.dart.md` pins. Authoritative
      Dart side: [dart.dev language tour `Records`](https://dart.dev/language/records#record-fields).
      Authoritative .NET side: [Microsoft Learn `Tuple types` § field
      accessors](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-tuples#tuple-field-names).
  - construct_key: dart.expression.lambda_zero_arg_arrow
    source_form: >-
      "freshAddrAllocator: () => runtimeQ.heap.allocateVariable()"
    target_decision: >-
      Dart zero-arg arrow-style lambda `() => <expr>` maps to a C# zero-arg
      lambda `() => <expr>` directly — both languages require the empty
      parentheses for a zero-parameter lambda. Specifically `() =>
      runtimeQ.heap.allocateVariable()` -> `() =>
      runtimeQ.Heap.AllocateVariable()`. The lambda is assigned to the
      `freshAddrAllocator` parameter declared as `Func<(int writerAddr,
      int readerAddr)>` per the SUT spec `mad_helpers.dart.md` (returns
      a ValueTuple of two ints). Reuses the pinned arrow-lambda idiom
      from mad_scenarios_test.dart.md.
    idiom_id: rf-dart-arrow-lambda-to-csharp-lambda
    research_finding_id: rf-dart-arrow-lambda-to-csharp-lambda
    nuance: >-
      Zero-arg-lambda nuance (explicitly addressed): both Dart and C#
      require `()` for a zero-parameter lambda. Return-type nuance: the
      lambda returns the heap's allocate-pair record `(int, int)`; on
      the C# side this is a `ValueTuple<int, int>`; the lambda's
      inferred return type MUST match the `Func<(int, int)>` parameter
      type signature on `Localize`. Capture-and-closure nuance: each
      lambda captures one enclosing local (`runtimeQ`) by reference;
      closure semantics agree between Dart and C# at the use-site.
  - construct_key: dart.expression.statement_bodied_lambda_assigned_to_delegate_field
    source_form: >-
      "ctx.onMessageReady = (dest, msg) { sent.add((dest, msg)); };
       ctxP.onMessageReady = (dest, msg) { if (dest == 'q') { ctxQ.handleMadAssignment(...); } };
       ctxQ.onMessageReady = (dest, msg) { if (dest == 'p') { ctxP.handleMadAssignment(...); } };"
    target_decision: >-
      Dart STATEMENT-BODY anonymous function `(arg1, arg2) { <statements>;
      }` assigned to a delegate-typed field. Maps to a C# STATEMENT-BODY
      lambda `(arg1, arg2) => { <statements>; }`. The right-hand side
      assigns to the `OnMessageReady` field declared
      `MessageDeliveryCallback? OnMessageReady;` per the SUT spec
      `mad_context.dart.md` (a DELEGATE-typed nullable field, NOT an
      `event`). Specifically:
      `ctx.onMessageReady = (dest, msg) { sent.add((dest, msg)); };` ->
      `ctx.OnMessageReady = (dest, msg) => { sent.Add((dest, msg)); };`;
      `ctxP.onMessageReady = (dest, msg) { if (dest == 'q') {
      ctxQ.handleMadAssignment(globalName: ..., value: ..., fromAgent:
      'p'); } };` -> `ctxP.OnMessageReady = (dest, msg) => { if (dest
      == "q") { ctxQ.HandleMadAssignment(globalName: ..., value: ...,
      fromAgent: "p"); } };`. The C# lambda parameters' static types are
      inferred from the `MessageDeliveryCallback` delegate signature
      (`(string destination, OutboundMessage message)`) — codegen MAY
      emit explicit parameter types `(string dest, OutboundMessage msg)
      => { ... }` OR rely on inference `(dest, msg) => { ... }`. The
      THREE occurrences in this file follow two patterns: (1) Send
      Transaction test — a simple capture-list-append closure (one
      statement, no conditional); (2) Direct Communication / Return
      Value tests — conditional dispatch to the OTHER context's
      `handleMadAssignment` (REUSED VERBATIM from
      mad_scenarios_test.dart.md `(dest, msg) { if (dest == '<dest>') {
      ... } }` discipline).
    idiom_id: rf-dart-statement-body-lambda-to-csharp-statement-body-lambda
    research_finding_id: rf-dart-statement-body-lambda-to-csharp-statement-body-lambda
    nuance: >-
      Statement-body-vs-arrow nuance (explicitly addressed, REUSED from
      mad_scenarios_test.dart.md): Dart `(args) { stmts }` is the
      equivalent of C# `(args) => { stmts; }` — note the `=>` arrow is
      REQUIRED on the C# side even for the statement-body form (it
      separates the parameter list from the body block). Closure-capture
      nuance: in the Send Transaction case the lambda captures `sent`
      (a `List<(string, OutboundMessage)>` local) by reference and
      mutates it (`sent.Add(...)`) — closure-by-reference semantics
      agree between Dart and C#; in the Direct Communication / Return
      Value cases the lambda captures the OTHER `MadContext` instance
      and calls a method on it — same closure-by-reference semantics.
      Async/Future nuance: ABSENT — every lambda is synchronous; the
      delegate signature is `void`-returning, so a `Task`-returning
      lambda would not satisfy `MessageDeliveryCallback`.
      Delegate-vs-event nuance (IDENTICAL to mad_scenarios_test.dart.md):
      per the SUT spec, `OnMessageReady` is a PUBLIC delegate-typed
      field, NOT an `event` — direct assignment with `=` is valid C#.
      Capture-list-mutation nuance (NEW for this file relative to
      mad_scenarios_test.dart.md): the Send Transaction test pattern
      `sent.add((dest, msg))` inside the lambda body MUTATES an
      enclosing local; mad_scenarios_test.dart.md never had this — its
      delegates only CALLED methods on captured `MadContext` instances,
      never appended to an enclosing collection. Both Dart and C#
      handle captured-local mutation identically (the closure captures
      the LOCAL VARIABLE, not its value at capture time — so post-lambda
      reads of `sent` see the mutations).
  - construct_key: dart.package_test.expect_throwsStateError
    source_form: >-
      "expect(() => ctx.handleMadAssignment(globalName: GlobalName.writer('p', 5), value: ConstTerm(42), fromAgent: 'q'), throwsStateError);
       expect(() => ctx.handleMadAssignment(globalName: GlobalName.reader('p', 5), value: ConstTerm(42), fromAgent: 'p'), throwsStateError);"
    target_decision: >-
      Dart `throwsStateError` is a constant matcher that asserts the
      executed thunk throws `StateError` (Dart `StateError` is the
      "invalid state for the requested operation" exception — the C#
      counterpart is `InvalidOperationException`). The pinned mapping
      from `binding_pointer_test.dart.md` (lines 664-705,
      `rf-dart-expect-throwsStateError-to-xunit-assert-throws-ioe`):
      `expect(() => <thunk>, throwsStateError)` ->
      `Assert.Throws<InvalidOperationException>(() => <thunk>)`. This
      file's two occurrences (lines 112-119 and 127-135) become:
      `Assert.Throws<InvalidOperationException>(() =>
      ctx.HandleMadAssignment(globalName: GlobalName.Writer("p", 5),
      value: new ConstTerm(42), fromAgent: "q"));` and
      `Assert.Throws<InvalidOperationException>(() =>
      ctx.HandleMadAssignment(globalName: GlobalName.Reader("p", 5),
      value: new ConstTerm(42), fromAgent: "p"));`.
      Reuses the pinned idiom verbatim (FR-012 / SC-007) — no
      re-research. The SUT side (`mad_context.dart.md`) MUST pin
      `HandleMadAssignment` as throwing `InvalidOperationException`
      when no matching `GlobalizeEntry` / `LocalizeEntry` exists, so
      the assertion succeeds on the C# side exactly when it does on
      the Dart side. (The SUT spec for `mad_context.dart` should
      already capture this — if it does not, codegen MUST flag a
      cross-file consistency issue.)
    idiom_id: rf-dart-expect-throwsStateError-to-xunit-assert-throws-ioe
    research_finding_id: rf-dart-expect-throwsStateError-to-xunit-assert-throws-ioe
    nuance: >-
      Exception-type-mapping nuance (LOAD-BEARING, IDENTICAL to
      binding_pointer_test.dart.md): Dart `StateError` (extends `Error`,
      "the operation is not allowed for the current state of the object")
      maps to C# `InvalidOperationException` (extends `SystemException`,
      "thrown when a method call is invalid for the object's current
      state"). Both express the same conceptual condition; the mapping
      is project-wide and pinned.
      Exact-vs-subtype nuance: Dart `throwsStateError` matches
      `StateError` AND its subtypes; C# `Assert.Throws<T>` matches ONLY
      the exact type; C# `Assert.ThrowsAny<T>` matches the type AND its
      subtypes. The SUT spec MUST pin which exception type is thrown —
      if the SUT throws exactly `InvalidOperationException` (no
      subclass), `Assert.Throws<InvalidOperationException>` is the
      tighter assertion and matches the source intent. If a subclass is
      thrown, codegen MUST emit
      `Assert.ThrowsAny<InvalidOperationException>` instead.
      Thunk-shape nuance: both Dart `() => <expr>` and C# `() => <expr>`
      arrow-lambda forms are zero-arg thunks; both `Action` (returns
      void, used by `Assert.Throws`) and `Func<T>` (returns T) thunks
      are valid; the arrow body here is a void-returning method call,
      so the C# lambda is implicitly typed as `Action`. Authoritative
      Dart side: [dart.dev `package:test` `throwsStateError`](https://pub.dev/documentation/test_api/latest/matcher/throwsStateError-constant.html).
      Authoritative .NET side: [Microsoft Learn
      `InvalidOperationException`](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)
      + [xUnit `Assert.Throws<T>`](https://xunit.net/docs/comparisons#exceptions).
  - construct_key: dart.expression.expect_isA_to_xunit_assert_istype
    source_form: >-
      "expect(derefed, isA<ConstTerm>());"
    target_decision: >-
      Dart `expect(actual, isA<T>())` asserts the actual is an instance
      of `T`. Maps to xUnit `Assert.IsType<T>(actual)` — pinned by
      mad_scenarios_test.dart.md / binding_pointer_test.dart.md / many
      others as `rf-dart-expect-isA-to-xunit-assert-istype`. THIS file
      exercises it FOUR times — once in each of the two Receive
      Transaction binding tests (lines 42, 73) and once in each
      end-to-end test (lines 237, 307) — all on `derefed` against
      `ConstTerm`. Translation: `expect(derefed, isA<ConstTerm>())` ->
      `Assert.IsType<ConstTerm>(derefed);`. The folded form
      `var ct = Assert.IsType<ConstTerm>(derefed);` is stylistically
      preferred when followed by an `as`-cast on the same value (see
      next construct).
    idiom_id: rf-dart-expect-isA-to-xunit-assert-istype
    research_finding_id: rf-dart-expect-isA-to-xunit-assert-istype
    nuance: >-
      Exact-type-vs-subtype nuance (REUSED from mad_scenarios_test.dart.md):
      `Assert.IsType<T>` enforces exact type identity; `Assert.IsAssignableFrom<T>`
      allows subclasses. Dart `isA<T>()` matches T AND its subtypes —
      strictly the latter. For `ConstTerm` (a leaf class in the
      `terms.dart` discriminated hierarchy per the SUT spec), exact-type
      and subtype-aware checks coincide, so `Assert.IsType<ConstTerm>`
      is the appropriate emission. The relationship between the
      `expect(x, isA<T>())` assertion and the subsequent `(x as T).value`
      cast is folded into one operation via the `Assert.IsType<T>(x)`
      return value — see next construct.
  - construct_key: dart.expression.as_cast_after_isA_assertion
    source_form: >-
      "(derefed as ConstTerm).value
       (derefed as ConstTerm).value      // four occurrences total
       (derefed as ConstTerm).value
       (derefed as ConstTerm).value"
    target_decision: >-
      Dart `as T` is a runtime-checked cast. The C# counterpart is the
      unconditional cast `(T)expr` (which throws
      `InvalidCastException` at runtime if the cast fails). After the
      preceding `Assert.IsType<T>(expr)` the cast is statically
      guaranteed to succeed. Translate:
      `(derefed as ConstTerm).value` -> `((ConstTerm)derefed).Value`,
      OR equivalently fold with the preceding IsType assertion:
      `var ct = Assert.IsType<ConstTerm>(derefed); /* assertions on
      ct.Value */`. The folded form is the stylistic optimum; codegen
      MAY emit either. Reuses the pinned idiom from
      mad_scenarios_test.dart.md `rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`.
    idiom_id: rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return
    research_finding_id: rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return
    nuance: >-
      As-cast-semantics nuance (IDENTICAL to mad_scenarios_test.dart.md /
      test_channel_construction.dart.md): Dart `as T` and C# `(T)expr`
      BOTH throw at runtime on cast failure — semantically identical.
      The `expr as T?` safe-cast form (returning null) is NOT used in
      this file. Folded-vs-separate nuance: separate `Assert.IsType<T>(x)`
      + later `(T)x` cast is two operations; the folded `var t =
      Assert.IsType<T>(x);` is one — both produce identical runtime
      behaviour; codegen prefers the folded form for terseness.
  - construct_key: dart.expression.expect_equals_to_xunit_assertequal
    source_form: >-
      "expect((derefed as ConstTerm).value, 42);
       expect((derefed as ConstTerm).value, 42);     // second use, Receive test 2
       expect(count, 1);
       expect(sent.length, 1);
       expect(sent[0].$1, 'q');
       expect(sent[0].$2.type, MessageType.assignment);
       expect(localizeResult.useReader[0], true);
       expect(localizeResult.useReader[0], false);
       expect((derefed as ConstTerm).value, 1);
       expect((derefed as ConstTerm).value, 42);"
    target_decision: >-
      Dart `expect(actual, expected)` where `expected` is a literal
      value (not a matcher) maps to xUnit `Assert.Equal(expected,
      actual)` — note the ARGUMENT ORDER FLIPS (Dart puts actual first;
      xUnit puts expected first). Specifically:
      `expect((derefed as ConstTerm).value, 42)` ->
      `Assert.Equal(42, ((ConstTerm)derefed).Value);` (or folded:
      `Assert.Equal(42, Assert.IsType<ConstTerm>(derefed).Value);`);
      `expect(count, 1)` -> `Assert.Equal(1, count);`;
      `expect(sent.length, 1)` -> `Assert.Equal(1, sent.Count);` (Dart
      `.length` on `List<T>` -> C# `.Count` on `List<T>`);
      `expect(sent[0].$1, 'q')` -> `Assert.Equal("q", sent[0].Item1);`
      (single-quote Dart -> double-quote C#; `$1` -> `Item1`);
      `expect(sent[0].$2.type, MessageType.assignment)` ->
      `Assert.Equal(MessageType.Assignment, sent[0].Item2.Type);`
      (enum value-equality on both sides);
      `expect(localizeResult.useReader[0], true)` ->
      `Assert.True(localizeResult.UseReader[0]);` (boolean special-case;
      `expect(x, true)` -> `Assert.True(x)`);
      `expect(localizeResult.useReader[0], false)` ->
      `Assert.False(localizeResult.UseReader[0]);`.
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order nuance (LOAD-BEARING, IDENTICAL to
      mad_scenarios_test.dart.md / test_channel_construction.dart.md):
      Dart `expect(actual, expected)`; xUnit `Assert.Equal(expected,
      actual)` — codegen MUST flip the order. Boolean-special-case
      nuance: `expect(x, true)` / `expect(x, false)` translates to
      `Assert.True(x)` / `Assert.False(x)` — NOT `Assert.Equal(true,
      x)`. Enum-value-equality nuance (NEW concrete use for THIS file):
      `expect(sent[0].$2.type, MessageType.assignment)` requires C#
      enum value-equality, which is the default for `enum` types
      (backed by integral comparison) — `Assert.Equal(MessageType.Assignment,
      sent[0].Item2.Type)` succeeds exactly when the Dart `==` does.
      String-quote nuance: Dart `'q'` is a one-character STRING (not
      `char`); C# `"q"` is a string literal — codegen MUST emit
      double-quote string literal, not the single-quote `'q'` which is
      a C# `char` literal.
  - construct_key: dart.expression.expect_isnull_isnotnull
    source_form: >-
      "expect(ctx.wp.lookupByIndex(index), isNull);
       expect(ctx.wp.findByRemote('p', 3), isNull);
       expect(ctx.wp.findByRemote('q', 2), isNotNull);"
    target_decision: >-
      Dart `isNull` / `isNotNull` matcher idioms route to xUnit's typed
      assertion methods (more readable than `Assert.Equal(null, ...)` /
      `Assert.NotEqual(null, ...)`). Specifically:
      `expect(x, isNull)` -> `Assert.Null(x);`;
      `expect(x, isNotNull)` -> `Assert.NotNull(x);`.
      Pinned idiom names reused from binding_pointer_test.dart.md
      (`rf-dart-expect-isnotnull-to-xunit-assertnotnull`) plus a
      first-recorded `rf-dart-expect-isnull-to-xunit-assertnull` for the
      negative form (mad_scenarios_test.dart.md never used `isNull`;
      THIS file does, twice). Translation table:
      `expect(ctx.wp.lookupByIndex(index), isNull)` ->
      `Assert.Null(ctx.Wp.LookupByIndex(index));`;
      `expect(ctx.wp.findByRemote('p', 3), isNull)` ->
      `Assert.Null(ctx.Wp.FindByRemote("p", 3));`;
      `expect(ctx.wp.findByRemote('q', 2), isNotNull)` ->
      `Assert.NotNull(ctx.Wp.FindByRemote("q", 2));`.
    idiom_id: rf-dart-expect-isnotnull-to-xunit-assertnotnull
    research_finding_id: rf-dart-expect-isnotnull-to-xunit-assertnotnull
    nuance: >-
      Null-return-type nuance (LOAD-BEARING, explicitly addressed): both
      `lookupByIndex(int)` and `findByRemote(String, int)` return
      nullable types (`GlobalizeEntry?` and `LocalizeEntry?` per
      `lib/multiagent/global_writers_table.dart.md` SUT spec) — the
      `isNull` / `isNotNull` assertions check whether the lookup found
      a matching row. The C# SUT MUST expose `GlobalizeEntry?
      LookupByIndex(int)` and `LocalizeEntry? FindByRemote(string,
      int)` (nullable reference types under .NET 6+ nullable-context
      ON). `Assert.Null` and `Assert.NotNull` both accept `object?`
      arguments and perform a runtime null check — the assertion
      succeeds/fails on the actual null/non-null state, regardless of
      the static nullable annotation. First-recorded-isNull nuance:
      THIS file is the first multiagent test convspec to record the
      `expect(x, isNull)` -> `Assert.Null(x)` direction; the new rf-id
      `rf-dart-expect-isnull-to-xunit-assertnull` is registered for the
      KB. Authoritative Dart side: [dart.dev `package:test` matchers](https://pub.dev/documentation/matcher/latest/matcher/isNull-constant.html).
      Authoritative .NET side: [xUnit `Assert.Null` /
      `Assert.NotNull`](https://xunit.net/docs/comparisons#null).
  - construct_key: dart.expression.method_invocation_on_owned_madcontext
    source_form: >-
      "ctx.handleMadAssignment(globalName: ..., value: ..., fromAgent: 'q');
       ctx.handleMadAssignmentWithGlobalNames(globalName: ..., value: ..., nestedGlobalNames: ..., fromAgent: 'q');
       ctx.flushMessages();
       ctxP.registerGlobalSendSpawns(globalizeResult.spawns);
       ctxQ.registerGlobalSendSpawns(localizeResult.spawns);
       ctxP.onWriterBound(writerXp, ConstTerm(1));
       ctxQ.onWriterBound(writerYq, ConstTerm(42));
       ctxP.flushMessages();
       ctxQ.flushMessages();
       runtimeP.heap.bindVariable(writerXp, ConstTerm(1));
       runtimeQ.heap.bindVariable(writerYq, ConstTerm(42));
       runtime.heap.derefAddr(writerAddr);
       runtimeP.heap.derefAddr(writerVp);
       runtimeQ.heap.derefAddr(writerZq);
       ctx.wp.addGlobalizeEntry(writerAddr, 'q');
       ctx.wp.addLocalizeEntry(writerAddr, 'p', 3);
       ctx.wp.lookupByIndex(index);
       ctx.wp.findByRemote('p', 3);
       ctx.wp.findByRemote('q', 2);
       ctx.mp.add(OutboundMessage(...));"
    target_decision: >-
      Ordinary Dart instance-method invocation `receiver.method(args)`
      maps to C# `receiver.Method(args)` with PascalCased method name.
      Each method's C# signature is pinned by the SUT spec:
      `MadContext.handleMadAssignment` ->
      `MadContext.HandleMadAssignment(GlobalName globalName, Term
      value, string fromAgent)` (per `mad_context.dart.md`; throws
      `InvalidOperationException` on no-matching-entry);
      `MadContext.handleMadAssignmentWithGlobalNames` ->
      `MadContext.HandleMadAssignmentWithGlobalNames(GlobalName
      globalName, Term value, IReadOnlyList<GlobalName>
      nestedGlobalNames, string fromAgent)` (per SUT spec);
      `MadContext.registerGlobalSendSpawns` ->
      `MadContext.RegisterGlobalSendSpawns(IReadOnlyList<GlobalSendSpawn>)`;
      `MadContext.onWriterBound` ->
      `MadContext.OnWriterBound(int writerId, Term value)`;
      `MadContext.flushMessages` -> `MadContext.FlushMessages()` (returns
      `int` count per the SUT spec);
      `GlobalWritersTable.addGlobalizeEntry(int, String) -> int` ->
      `GlobalWritersTable.AddGlobalizeEntry(int writerAddr, string
      remoteAgent) -> int` (per
      `lib/multiagent/global_writers_table.dart.md`);
      `GlobalWritersTable.addLocalizeEntry(int, String, int) -> void` ->
      `GlobalWritersTable.AddLocalizeEntry(int writerAddr, string
      remoteAgent, int remoteIndex)`;
      `GlobalWritersTable.lookupByIndex(int) -> GlobalizeEntry?` ->
      `GlobalWritersTable.LookupByIndex(int index) -> GlobalizeEntry?`;
      `GlobalWritersTable.findByRemote(String, int) -> LocalizeEntry?` ->
      `GlobalWritersTable.FindByRemote(string remoteAgent, int
      remoteIndex) -> LocalizeEntry?`;
      `MessageQueue.add(OutboundMessage)` ->
      `MessageQueue.Add(OutboundMessage)` (per
      `lib/multiagent/message_queue.dart.md` SUT spec — pinned in the
      "MessageQueue (private Dictionary<string, Queue<OutboundMessage>>
      _queuesByDestination)" public-API list);
      `runtime.heap.bindVariable` ->
      `runtime.Heap.BindVariable(int writerAddr, Term value)`;
      `runtime.heap.derefAddr` ->
      `runtime.Heap.DerefAddr(int addr)` -> `Term`.
      No async; all calls are synchronous in both languages.
    idiom_id: null
    research_finding_id: rf-dart-instance-method-call-to-csharp-pascalcase-call
    nuance: >-
      Member-naming-PascalCase nuance (project-wide rule, IDENTICAL to
      all sibling test specs): Dart instance methods are camelCase by
      convention; C# instance methods are PascalCase by convention.
      Codegen MUST PascalCase every method name AND every property name
      while leaving local variables, parameters, and named-argument
      labels as camelCase. Receiver-chain nuance:
      `runtimeP.heap.bindVariable(...)` has two PascalCased members —
      `Heap` (the property) and `BindVariable` (the method); both flip
      from camelCase to PascalCase. State-mutation nuance: `bindVariable`,
      `onWriterBound`, `addGlobalizeEntry`, `addLocalizeEntry`, and
      `mp.add` MUTATE per-agent state — single-threaded per the
      agent-ownership invariant from `mad_context.dart.md` (NO
      lock/synchronisation needed). Owned-property nuance (NEW first-use
      in THIS file relative to mad_scenarios_test.dart.md):
      `ctx.mp.add(OutboundMessage(...))` accesses the `mp` property
      (`MessageQueue` instance) on `MadContext` and calls `Add` on it —
      the SUT spec `mad_context.dart.md` MUST expose `MadContext.Mp`
      (PascalCased) as the `MessageQueue` property; the chained call is
      one C# member access + one method call.
  - construct_key: dart.expression.indexed_property_access
    source_form: >-
      "localizeResult.freshPairs[0].writerAddr
       globalizeResult.globalNames[0]
       localizeResult.useReader[0]
       sent[0]"
    target_decision: >-
      Dart `expr[index]` on a `List<T>` (subscript operator) maps to C#
      `expr[index]` on a `List<T>` or `IReadOnlyList<T>` (indexer
      operator — IDENTICAL syntax). The receiver's PascalCased property
      name applies (`freshPairs` -> `FreshPairs`, `globalNames` ->
      `GlobalNames`, `useReader` -> `UseReader`). Specifically:
      `localizeResult.freshPairs[0].writerAddr` ->
      `localizeResult.FreshPairs[0].WriterAddr;`;
      `globalizeResult.globalNames[0]` ->
      `globalizeResult.GlobalNames[0];`;
      `localizeResult.useReader[0]` -> `localizeResult.UseReader[0];`;
      `sent[0]` -> `sent[0];` (the `List<(string, OutboundMessage)>`
      local indexer — no PascalCasing needed on the local variable).
      Reuses the pinned indexer idiom from mad_scenarios_test.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-list-indexer-to-csharp-list-indexer
    nuance: >-
      Indexer-syntax nuance (explicitly addressed): both languages use
      `expr[i]` for list/array indexing; the syntax is IDENTICAL.
      Member-name nuance (REUSED from mad_scenarios_test.dart.md):
      `freshPairs[0].writerAddr` chains an indexer with a property
      access; the C# port preserves the chain shape but PascalCases each
      member name. If the SUT chose to expose `freshPairs` as `List<(int
      writerAddr, int readerAddr)>` (a ValueTuple list), the trailing
      `.writerAddr` in C# would still be `.writerAddr` (tuple-field-name
      preserved as camelCase — tuple element names are NOT PascalCased
      by the .NET naming guideline). Codegen MUST consult the SUT spec
      for `mad_helpers.dart` to decide: if `FreshPair` is a named record
      then `.WriterAddr`; if a ValueTuple then `.writerAddr`.
      Local-list-indexer nuance (NEW for this file): `sent[0]` indexes
      a LOCAL `List<(string, OutboundMessage)>` (the test-body capture)
      — the indexer maps directly without any PascalCasing because
      `sent` is a local variable, not a property.
  - construct_key: dart.expression.identifier_spec_notation_in_comments_preserved
    source_form: >-
      "// Given: Agent p has a GlobalizeEntry (X, q) at index 1 from globalizing writer X
       // When: p receives message _w(p,1) := 42 from q
       // Then: X is bound to 42, entry removed
       // Globalize writer X at p → entry (X, q) at index i, no spawn
       // Localize _w(p,i) at q → fresh pair (Y_q, Y_q?), spawn gs, use Y_q (writer)
       // q assigns Y_q → gs fires → sends _w(p,i) := T to p → p binds X
       // p sends X to q ... p must send X? (reader):"
    target_decision: >-
      No executable-code translation required — the underscore-bearing
      and special-character-bearing identifiers (`X?`, `_w(p,1)`,
      `_r(p,3)`, `:=`, `Y_q`, `Z_q`, etc.) appear ONLY inside Dart `//`
      comments (which encode the `madGLP-spec.md` mathematical notation
      for global names, readers, writers, and assignment). Codegen
      preserves the comment text VERBATIM inside the `///` doc-comment
      block on each xUnit test method. For the executable identifiers,
      the test bodies use camelCase Dart locals (`writerAddr`,
      `readerAddr`, `writerXp`, `writerYq`, `writerZq`, `writerVp`,
      etc.) that carry over as-is to C# local-variable names (Dart
      camelCase locals = C# camelCase locals; identifier-safe in both
      languages).
    idiom_id: null
    research_finding_id: rf-dart-identifier-spec-notation-in-comments-preserved
    nuance: >-
      Spec-notation-in-comments nuance (explicitly addressed, REUSED
      from mad_scenarios_test.dart.md): the source file's comments
      encode `madGLP-spec.md` mathematical notation (`X?`, `_w(p,1)`,
      `_r(p,1)`, `:=`, `Y_q`, `Z_q`) — these characters are NOT valid
      C# identifier characters, but they survive intact inside `///`
      doc-comment text. Codegen MUST preserve the comment text verbatim
      so the spec-traceability link is not lost. No executable
      translation needed; no idiom_id created (the rf-id is purely an
      idiom-KB anchor for the "preserve spec notation in comments" rule).
      Extended-commentary nuance (NEW for this file relative to
      mad_scenarios_test.dart.md): the Direct Communication test (lines
      164-177) carries a TWELVE-LINE meta-commentary explaining the
      "REVERSE direction" semantic gotcha that the test exercises;
      these lines are LOAD-BEARING for understanding the test
      intent. Codegen MUST carry them verbatim (likely as a `///
      <summary>` block at the top of the C# test method).
conversion_units:
  - file_header_and_using_directives_block
    # Drop Dart imports; emit `using Xunit;`, `using System;`,
    # `using System.Collections.Generic;`, `using <RootNs>.Runtime;`,
    # `using <RootNs>.Multiagent;`, optional
    # `using static <RootNs>.Multiagent.MadHelpers;`.
  - namespace_declaration
    # `namespace <RootNs>.Test.Multiagent;` (file-scoped namespace per .NET 6+
    # convention) — mirrors the Dart test/multiagent directory path.
  - class_ReceiveTransactionTests
    # Five `[Fact]` methods:
    #  - WPiMessageFindsGlobalizeEntryByIndexBindsWriter (binds writer via
    #    GlobalizeEntry lookup; asserts entry removed via LookupByIndex==null)
    #  - RPiMessageFindsLocalizeEntryBindsWriter (mirrors the above for
    #    LocalizeEntry via FindByRemote)
    #  - ReceiveLocalizesNestedVariables (handleMadAssignmentWithGlobalNames
    #    with a nested GlobalName.reader; assert FindByRemote(q, 2) != null)
    #  - ReceiveForNonExistentGlobalizeEntryThrows
    #    (Assert.Throws<InvalidOperationException>)
    #  - ReceiveForNonExistentLocalizeEntryThrows
    #    (Assert.Throws<InvalidOperationException>)
    # `[Trait("Group", "Receive Transaction")]` on the class.
  - class_SendTransactionTests
    # Single `[Fact]` method FlushMessagesSendsQueuedMessages — arrange
    # MadContext + mp.Add(OutboundMessage), set OnMessageReady to a
    # statement-body lambda capturing `sent`, call FlushMessages, assert
    # count==1 + sent.Count==1 + sent[0].Item1=="q" + sent[0].Item2.Type
    # == MessageType.Assignment.
    # `[Trait("Group", "Send Transaction")]` on the class.
  - class_DirectCommunicationScenarioTests
    # Single `[Fact]` method PSendsXToQPAssignsX1QReceivesValue — TWO
    # GlpRuntime + TWO MadContext arrangement; reader-side globalize at p
    # (spawn-not-entry); localize at q (entry); cross-agent OnMessageReady
    # dispatch; final Assert.IsType<ConstTerm>(derefed) + Assert.Equal(1,
    # ((ConstTerm)derefed).Value).
    # `[Trait("Group", "Direct Communication Scenario")]` on the class.
  - class_ReturnValueScenarioTests
    # Single `[Fact]` method PSendsVToQQAssignsVResultPReceivesResult —
    # mirror of the above with writer-side globalize at p
    # (entry-not-spawn); localize at q (spawn); reverse-direction message
    # routing; final Assert.IsType<ConstTerm>(derefed) + Assert.Equal(42,
    # ((ConstTerm)derefed).Value).
    # `[Trait("Group", "Return Value Scenario")]` on the class.
escalations: []
```

## B. Embedded human-readable rationale + provenance

This file is the **transaction-level** test for the madGLP multi-agent
implementation. The first group (`Receive Transaction`) exercises the
LOW-LEVEL `handleMadAssignment` / `handleMadAssignmentWithGlobalNames`
methods in isolation against a single `MadContext` (no cross-agent
routing). The second group (`Send Transaction`) exercises `flushMessages`
delivering a manually-added `OutboundMessage` via the `OnMessageReady`
delegate. The third and fourth groups (`Direct Communication Scenario` /
`Return Value Scenario`) are end-to-end two-agent scenarios that
EXTENSIVELY COMMENT the "REVERSE direction" semantic gotcha — these
in-source comments are LOAD-BEARING for the test's intent and MUST
survive the port (FR-024 doc-level traceability).

### rf-dart-package-test-to-dotnet-xunit — project-wide xUnit framework choice

REUSED VERBATIM from sibling specs. xUnit is the project-wide test
framework. The distinguishing facts for THIS file relative to the
siblings are (a) four sibling groups of which one (Receive Transaction)
carries five tests while the other three carry exactly one each, and (b)
FIRST executable use of `throwsStateError` in the multiagent test
convspec layer (already pinned by `binding_pointer_test.dart.md`).
Authoritative Dart side:
[dart.dev `package:test`](https://dart.dev/tools/dart-test#the-package-test-package).
Authoritative .NET side:
[Microsoft Learn xUnit + .NET Test Sdk](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test).

### rf-dart-package-sut-import-to-csharp-using — two-namespace SUT import collapse

This file imports SIX SUT files, collapsing to TWO `using` directives:
`using <RootNs>.Runtime;` for `runtime.dart` + `terms.dart`, and
`using <RootNs>.Multiagent;` for `mad_context.dart` + `message_queue.dart`
+ `mad_helpers.dart` + `global_send.dart`. `OutboundMessage` and
`MessageType` are constructed/referenced directly in this file (unlike
mad_scenarios_test.dart.md, which referenced them only as parameter
types); the SUT spec `lib/multiagent/message_queue.dart.md` pins both
shapes (positional C# ctor for `OutboundMessage`, PascalCased
`MessageType.Assignment` enum member). Codegen SHOULD emit `using static
<RootNs>.Multiagent.MadHelpers;` so the end-to-end test bodies'
`Globalize(...)` / `Localize(...)` calls read unqualified.

### rf-dart-test-main-to-xunit-class-with-facts + group-block — four sibling classes

Four `group(...)` calls inside `main` → four sibling public classes.
Each class name encodes the scenario title with non-identifier chars
stripped (`ReceiveTransactionTests`, `SendTransactionTests`,
`DirectCommunicationScenarioTests`, `ReturnValueScenarioTests`). Each
class carries `[Trait("Group", "<original label>")]`. Per-class
test-method density is asymmetric: 5 / 1 / 1 / 1 — preserved verbatim.

### rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction — heap alloc-pair pattern

The `final (writerAddr, readerAddr) = runtime.heap.allocateVariable();`
construct appears FIVE times in this file. NEW relative to
mad_scenarios_test.dart.md: ONE occurrence uses the Dart 3 `_` discard
wildcard (`final (writerAddr, _) = ...`) at line 89 in the
ReceiveLocalizesNestedVariables test. Both Dart 3 and C# 7+ support `_`
as a discard pattern; codegen emits `var (writerAddr, _) =
runtime.Heap.AllocateVariable();` directly. Authoritative Dart side:
[dart.dev language tour `Patterns / Wildcard`](https://dart.dev/language/patterns#wildcard).
Authoritative .NET side: [Microsoft Learn `Discards`](https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/discards).

### rf-dart-statement-body-lambda-to-csharp-statement-body-lambda — three uses, one new pattern

THIS file exercises the statement-body-lambda assignment to
`OnMessageReady` THREE times. Two follow the cross-agent dispatch
pattern already pinned by mad_scenarios_test.dart.md (`(dest, msg) { if
(dest == '<dest>') { ctx.handleMadAssignment(...); } }`). The third is
the Send Transaction test's capture-list-append form `(dest, msg) {
sent.add((dest, msg)); }` — the FIRST use in the multiagent test
convspec layer where the lambda body mutates an enclosing
non-MadContext local (`sent`, a `List<(string, OutboundMessage)>`).
Both Dart and C# capture-by-reference semantics handle the mutation
identically (post-lambda reads of `sent` see the appended elements).

### rf-dart-record-positional-getter-to-csharp-valuetuple-itemn — `$1`/`$2` -> `Item1`/`Item2`

The Send Transaction test asserts on the capture-list contents via
`sent[0].$1` (destination string) and `sent[0].$2.type` (the
OutboundMessage's enum type). Reuses the pinned idiom from
localize_test.dart.md verbatim — `$1` -> `Item1`, `$2` -> `Item2`, both
1-based.

### rf-dart-expect-throwsStateError-to-xunit-assert-throws-ioe — FIRST executable use in multiagent test specs

Two tests in this file (`ReceiveForNonExistentGlobalizeEntryThrows`,
`ReceiveForNonExistentLocalizeEntryThrows`) assert that
`handleMadAssignment` throws `StateError` when no matching entry exists.
Maps to `Assert.Throws<InvalidOperationException>(() => ...)` per the
pinned idiom from `binding_pointer_test.dart.md` (lines 664-705 of that
spec). The SUT spec `lib/multiagent/mad_context.dart.md` MUST pin
`HandleMadAssignment` as throwing `InvalidOperationException` in the
no-matching-entry case — codegen MUST verify this cross-file constraint
when emitting the test. The earlier `mad_error_handling_test.dart.md`
recorded the mapping as a future research-finding candidate; THIS file
USES it for the first time in the multiagent test layer (the heap-level
`binding_pointer_test.dart.md` already established the idiom).

### MadContext + OutboundMessage ctors: positional C# despite Dart named-required — SUT-spec-determined call shape

LOAD-BEARING (REUSED + EXTENDED from mad_scenarios_test.dart.md): every
`MadContext(agentId: 'p', runtime: runtime)` call site here uses Dart
NAMED-REQUIRED args, but the SUT spec `lib/multiagent/mad_context.dart.md`
pins a POSITIONAL C# ctor. SAME applies to `OutboundMessage(destination:
'q', type: ..., payload: [1, 2, 3])` — per
`lib/multiagent/message_queue.dart.md` (construct row 17-22), the C# SUT
ctor is POSITIONAL. The conversion DROPS the named-arg labels at both
ctor call sites. `globalize(...)` / `localize(...)` /
`handleMadAssignment(...)` /
`handleMadAssignmentWithGlobalNames(...)` SUT specs pin NAMED C#
parameters — their call sites preserve the labels. Codegen MUST consult
the per-callee SUT spec — there is no file-local rule.

### rf-dart-record-typed-list-and-tuple-add-to-csharp-valuetuple-list — NEW for this file's Send Transaction test

The `final sent = <(String, OutboundMessage)>[];` + `sent.add((dest,
msg))` pattern is a Dart 3 typed empty list of positional records plus
a tuple-literal append. Maps directly to C# `var sent = new
List<(string, OutboundMessage)>(); sent.Add((dest, msg));`. First
recorded for the multiagent test convspec layer. Authoritative Dart
side: [dart.dev `Records`](https://dart.dev/language/records).
Authoritative .NET side: [Microsoft Learn `Tuple types`](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-tuples).

### rf-dart-enum-member-access-pascalcase — `MessageType.assignment` -> `MessageType.Assignment`

The SUT spec `lib/multiagent/message_queue.dart.md` already pins the
two `MessageType` enum members as `Assignment` and `AgentMessage`
(PascalCased per .NET naming convention). THIS file's call sites
follow the SUT's pinning at construction (`new OutboundMessage(...,
MessageType.Assignment, ...)`) and at read (`sent[0].Item2.Type ==
MessageType.Assignment`). Authoritative .NET side: [Microsoft `Names
of Enumerations`](https://learn.microsoft.com/dotnet/standard/design-guidelines/names-of-classes-structs-and-interfaces#names-of-enumerations).

### rf-dart-expect-isnull-to-xunit-assertnull — FIRST RECORDED for the multiagent test convspec layer

mad_scenarios_test.dart.md exercised `isNotNull` and `isEmpty` but not
`isNull`. THIS file uses `expect(x, isNull)` twice
(`lookupByIndex(index) == null` after entry removal;
`findByRemote('p', 3) == null` after entry removal). Maps to
`Assert.Null(x)`. New rf-id `rf-dart-expect-isnull-to-xunit-assertnull`
registered for the KB. Authoritative Dart side: [`matcher` package
`isNull`](https://pub.dev/documentation/matcher/latest/matcher/isNull-constant.html).
Authoritative .NET side: [xUnit `Assert.Null` /
`Assert.NotNull`](https://xunit.net/docs/comparisons#null).

### Spec-section preservation

The test labels in THIS file do NOT carry explicit `madGLP-spec.md`
section numbers (unlike mad_scenarios_test.dart.md). The file-level
docstring at line 1-5 of the source DOES carry "See: madGLP-spec.md
Sections 8.1-8.4" — codegen MUST emit this as the file-level
`// <auto-generated>`-style or `/// <remarks>` comment at the
namespace declaration or as a `[Trait("SpecSection", "8.1-8.4")]` on
each test class. The in-test Given/When/Then + "Corrected definitions:"
+ "REVERSE direction" commentary blocks are LOAD-BEARING for the
test's intent and MUST be carried verbatim into `/// <summary>` doc
blocks on each test method.

### Out-of-scope but recorded

- Project-system wiring (`<ProjectReference>` from the test .csproj to
  the runtime .csproj) is langpair-level; recorded so codegen knows the
  `using` alone is insufficient without the project reference.
- The exact `<RootNs>` placeholder is langpair-level and pinned at the
  workspace level, not per file.
- `Heap.AllocateVariable` / `Heap.BindVariable` / `Heap.DerefAddr` C#
  signatures live in the SUT convspec `lib/runtime/heap_fcp.dart.md`;
  THIS spec depends on those signatures but does not redefine them.
- The `MessageDeliveryCallback` delegate type is pinned by
  `lib/multiagent/mad_context.dart.md`; THIS spec records the call-site
  shape of the field assignment, not the delegate declaration.
- The exception type thrown by `HandleMadAssignment` on missing entry
  (asserted via `Assert.Throws<InvalidOperationException>`) is pinned
  by the SUT spec `lib/multiagent/mad_context.dart.md` and the
  underlying `GlobalWritersTable` SUT spec — codegen MUST verify the
  cross-file consistency.

### KB cache hits (no re-research)

All of the following pinned rf-ids were KB cache hits — re-research
was NOT performed (FR-024 reproducibility-offline rule):
`rf-dart-package-test-to-dotnet-xunit`,
`rf-dart-package-sut-import-to-csharp-using`,
`rf-dart-test-main-to-xunit-class-with-facts`,
`rf-dart-package-test-group-to-xunit-class`,
`rf-dart-test-callback-to-xunit-method-body`,
`rf-dart-final-local-to-csharp-var-local`,
`rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction`,
`rf-dart-named-constructor-to-csharp-static-factory`,
`rf-dart-named-argument-to-csharp-named-argument`,
`rf-dart-arrow-lambda-to-csharp-lambda`,
`rf-dart-statement-body-lambda-to-csharp-statement-body-lambda`,
`rf-dart-list-literal-to-csharp-list-initializer`,
`rf-dart-record-positional-getter-to-csharp-valuetuple-itemn`,
`rf-dart-expect-isA-to-xunit-assert-istype`,
`rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`,
`rf-dart-expect-equals-to-xunit-assertequal`,
`rf-dart-expect-isnotnull-to-xunit-assertnotnull`,
`rf-dart-expect-throwsStateError-to-xunit-assert-throws-ioe`,
`rf-dart-instance-method-call-to-csharp-pascalcase-call`,
`rf-dart-list-indexer-to-csharp-list-indexer`,
`rf-dart-identifier-spec-notation-in-comments-preserved`,
`rf-dart-enum-member-access-pascalcase`.

### Newly recorded rf-ids (defined by this file's first-use rows)

- `rf-dart-record-typed-list-and-tuple-add-to-csharp-valuetuple-list` —
  Dart 3 typed empty list of positional records (`<(T1, T2)>[]`) +
  tuple-literal `.add((a, b))` calls, mapping to C# `new List<(T1,
  T2)>()` + `.Add((a, b))`. (See the
  `dart.expression.generic_list_of_tuple_local_variable_with_collection_add`
  construct row.)
- `rf-dart-expect-isnull-to-xunit-assertnull` — `expect(x, isNull)` ->
  `Assert.Null(x)`; sibling of the already-pinned `isNotNull` mapping.

### No escalations

This file's constructs all resolve via the decision-order in
`convspec_idiom_schema.md`: every construct row records EITHER a pinned
idiom_id (KB cache hit) OR a research_finding_id with authoritative
Dart+.NET citations (the two new rf-ids above). No
`idiom_vs_research` conflicts; no `idiom_vs_idiom` conflicts; no
undecidable points. The `escalations: []` is intentional.
