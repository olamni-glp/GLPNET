# Conversion Spec — test/multiagent/mad_scenarios_test.dart

> Conversion-spec artifact for test/multiagent/mad_scenarios_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/mad_scenarios_test.dart
source_sha256: 59bbfd23496686b05f542804fbe56eb5e7e02e8154753ead5456b7d2f71d61a1
target_code_unit: test/multiagent/MadScenariosTest.cs
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
      test/multiagent/global_send_test.dart.md). THIS file MUST reuse that
      idiom verbatim (FR-012 / SC-007) — no re-research. The .NET test
      project (.csproj — out of this single-file artifact's scope) provides
      `xunit` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` NuGet
      references. Codegen projects to a single namespace mirroring the Dart
      `test/multiagent` directory (e.g. `<RootNs>.Test.Multiagent`). Codegen
      MUST also add `using System.Collections.Generic;` at file scope
      because the test bodies materialise `List<TermVar>` literals through
      the `globalize`/`localize` calls (see
      dart.expression.list_literal_typed below).
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice nuance (load-bearing, explicitly addressed): xUnit
      pinned project-wide; NUnit / MSTest recorded as alternatives in the
      research-finding row but NOT used here. Module/namespace nuance:
      Dart's `package:test` exposes top-level functions (`group`, `test`,
      `expect`, `isA`, `isEmpty`, `isNotNull`) re-exported via the one
      import; xUnit has NO top-level test functions — tests are public
      instance methods on a public class discovered via `[Fact]` reflection.
      No async / Future / Stream / isolate surface in this file (the
      scenarios are synchronous heap+callback orchestration).
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
      directive that names the namespace the converted SUT files emit into.
      Two distinct namespaces are involved: the `runtime/` subset (two of
      the six imports — `runtime.dart`, `terms.dart`) emits into the
      runtime namespace (e.g. `using <RootNs>.Runtime;` per sibling SUT
      specs `.codeconv/conversion-specs/lib/runtime/runtime.dart.md` and
      `.../lib/runtime/terms.dart.md`), and the `multiagent/` subset (four
      of the six — `mad_context.dart`, `message_queue.dart`,
      `mad_helpers.dart`, `global_send.dart`) emits into the multiagent
      namespace (e.g. `using <RootNs>.Multiagent;` per sibling SUT specs
      `.../lib/multiagent/mad_context.dart.md`,
      `.../lib/multiagent/message_queue.dart.md`,
      `.../lib/multiagent/mad_helpers.dart.md`,
      `.../lib/multiagent/global_send.dart.md`). Codegen MUST emit `using`s
      that resolve every symbol this test references: from runtime —
      `GlpRuntime` (constructor + `Heap` property + `Heap.AllocateVariable`,
      `Heap.BindVariable`, `Heap.DerefAddr`), `Term` (the discriminated
      base of `VarRef`/`StructTerm`/`ConstTerm`), `VarRef`, `StructTerm`
      (positional ctor `(string functor, IReadOnlyList<Term> args)` +
      `Functor`/`Args` getters), `ConstTerm` (positional ctor with
      object-typed `Value`); from multiagent — `MadContext` (named-args
      ctor `MadContext(string agentId, GlpRuntime runtime)` as pinned by
      sibling `.../lib/multiagent/mad_context.dart.md` + instance members
      `Wp`, `Mp`, `GlobalSendRegistry`, `OnMessageReady`,
      `RegisterGlobalSendSpawns`, `HandleMadAssignment`, `OnWriterBound`,
      `FlushMessages`), top-level helpers `Globalize`/`Localize` (both
      static methods on `MadHelpers` per sibling
      `.../lib/multiagent/mad_helpers.dart.md`), `TermVar`,
      `TermVar.Reader` / `TermVar.Writer` (static factories — pinned by
      same SUT spec), `GlobalName`, `GlobalName.Writer` / `GlobalName.Reader`
      (static factories — same SUT spec). The `message_queue` import is
      needed for the `OutboundMessage` parameter type on the
      `OnMessageReady` callback delegate `MessageDeliveryCallback`. Per
      `lib/multiagent/mad_context.dart.md` the delegate is `public delegate
      void MessageDeliveryCallback(string destination, OutboundMessage
      message);` — both type names resolve through the same `using
      <RootNs>.Multiagent;` (one `using` covers all four multiagent SUT
      types).
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed and IDENTICAL to
      global_send_test.dart.md / globalize_test.dart.md): a `package:`
      import that resolves to an in-repo Dart library (NOT to a pub.dev
      third-party package) maps to a C# `using <Namespace>;` that targets
      the OUTPUT namespace of the converted Dart library — NOT a separate
      NuGet reference. Distinguish by inspecting the `package:` URI prefix
      against the host repo's `pubspec.yaml` `name:` (here, `glp_runtime`).
      Project-file wiring (`<ProjectReference>` from the test .csproj to
      the runtime .csproj) is langpair/project-skeleton level, recorded so
      codegen knows the `using` alone is insufficient without the project
      reference. Two-namespace-collapse nuance: the six SUT imports
      collapse to exactly TWO `using` directives (`using <RootNs>.Runtime;`
      + `using <RootNs>.Multiagent;`) — codegen emits two, not six. The
      `mad_helpers.dart` import is needed for the top-level free functions
      `globalize`/`localize`, which the convspec for `mad_helpers.dart`
      maps to STATIC METHODS on a `MadHelpers` static class; callers
      reference them as `MadHelpers.Globalize(...)` /
      `MadHelpers.Localize(...)` UNLESS codegen emits an additional
      `using static <RootNs>.Multiagent.MadHelpers;` at file scope (which
      it SHOULD — every test in this file calls both `globalize(...)` and
      `localize(...)` unqualified, matching the Dart call-site shape).
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
      Lifecycle nuance (explicitly addressed): Dart `main` runs once per
      test-file process and registers tests; xUnit has no per-file hook —
      only per-class (constructor + `IDisposable.Dispose`) and
      per-collection fixtures. THIS file's `main` body is exactly four
      sibling `group(...)` calls with no other statements, so omitting
      `main` is lossless. No `setUp` / `setUpAll` / `tearDown` /
      `tearDownAll` anywhere in this file, so no constructor or
      `IDisposable.Dispose` content is needed. Four-sibling-groups nuance
      (EXTENDS the two-sibling-groups nuance from
      global_send_test.dart.md): the four sibling groups
      ('Section 10.1: Direct Communication (Client-Monitor)',
      'Section 10.2: Return Value Scenario',
      'Section 10.3: Friend-Mediated Introduction',
      'Section 5.4: Both Ends Exported') become FOUR sibling public
      classes under the same namespace — NOT a nested-class layout. xUnit
      `[Trait("Group", "<original label>")]` on each class preserves the
      spec-section label for reporter parity (the labels carry
      `madGLP-spec.md` section numbers — load-bearing for traceability).
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('Section 10.1: Direct Communication (Client-Monitor)', () { test(...); });
       group('Section 10.2: Return Value Scenario', () { test(...); });
       group('Section 10.3: Friend-Mediated Introduction', () { test(...); });
       group('Section 5.4: Both Ends Exported', () { test(...); });"
    target_decision: >-
      Each Dart `group(label, body)` maps to a separate `public class
      <Label>Tests`. Group-label-to-class-name mangling strips non-identifier
      characters (spaces, colons, dots, parentheses, hyphens) and PascalCases
      the remaining tokens, then appends `Tests`. Specifically:
      `'Section 10.1: Direct Communication (Client-Monitor)'` ->
      `Section101DirectCommunicationClientMonitorTests`;
      `'Section 10.2: Return Value Scenario'` ->
      `Section102ReturnValueScenarioTests`;
      `'Section 10.3: Friend-Mediated Introduction'` ->
      `Section103FriendMediatedIntroductionTests`;
      `'Section 5.4: Both Ends Exported'` ->
      `Section54BothEndsExportedTests`.
      The original label MUST be preserved via `[Trait("Group", "<original
      label>")]` on each class for reporter parity AND because the labels
      encode `madGLP-spec.md` section numbers (load-bearing). No nested
      `group(...)`, no `setUp`/`tearDown` inside any group — each test
      constructs its own per-agent `GlpRuntime` + `MadContext` pair (two
      pairs for 10.1/10.2/5.4, three triples for 10.3) locally, so xUnit's
      per-test fresh-instance lifecycle ("xUnit.net creates a new instance
      of the test class for every test that is run") maps cleanly with NO
      shared state and NO constructor-side fixture needed.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance (EXTENDS global_send_test.dart.md, which had
      already-identifier-safe labels): THIS file's labels contain spaces,
      colons, parentheses, hyphens, dots, and digits — codegen MUST strip
      ALL non-identifier characters and concatenate PascalCased remaining
      tokens. The leading digit (none of the four labels actually start
      with a digit after `Section` is preserved, so the C# identifier rule
      "must not start with a digit" is satisfied). Sibling-groups-NOT-
      nested-groups nuance: SAME as global_send_test.dart.md — the four
      groups are SIBLING inside `main`, neither nested in the other; the
      documented mapping is four SEPARATE classes. Per-test labels in this
      file are sentence-form descriptions of the scenario (e.g. `'p sends
      stream X to q, p assigns X := [add|Xs1], q receives'`) — see next
      construct for the per-test method-name mangling rule.
  - construct_key: dart.package_test.test_call_executable
    source_form: >-
      "test('p sends stream X to q, p assigns X := [add|Xs1], q receives', () { ... });
       test('p sends [value(V?)|...] to q, q assigns V_q := Sum, p receives Sum', () { ... });
       test('Bob forwards X from Alice to Charlie, Charlie assigns, Alice receives', () { ... });
       test('p exports [X, X?] to q, q assigns Y_q := T, T flows back to p', () { ... });"
    target_decision: >-
      Each Dart `test(label, body)` (no `skip` argument anywhere) becomes
      a `public void` method on the enclosing class, decorated with
      `[Fact(DisplayName = "<original label>")]`. Method name = label
      PascalCased with non-identifier chars stripped (commas, colons,
      square brackets, pipes, parentheses, spaces, the question-mark, the
      assignment-operator characters `:=`, underscores preserved AS-IS for
      `X_c` / `Y_q` / `Xs1` / `V_q` style identifiers):
      `'p sends stream X to q, p assigns X := [add|Xs1], q receives'` ->
      `PSendsStreamXToQPAssignsXAddXs1QReceives`;
      `'p sends [value(V?)|...] to q, q assigns V_q := Sum, p receives Sum'`
      -> `PSendsValueVToQQAssignsV_qSumPReceivesSum`;
      `'Bob forwards X from Alice to Charlie, Charlie assigns, Alice receives'`
      -> `BobForwardsXFromAliceToCharlieCharlieAssignsAliceReceives`;
      `'p exports [X, X?] to q, q assigns Y_q := T, T flows back to p'` ->
      `PExportsXXToQQAssignsY_qTTFlowsBackToP`.
      Method body translates the Dart arrange-act-assert verbatim, with
      `expect(actual, matcher)` calls routed to xUnit `Assert.*` per the
      matcher-routing idioms below
      (`rf-dart-expect-equals-to-xunit-assertequal`,
      `rf-dart-expect-isA-to-xunit-assert-istype`,
      `rf-dart-expect-isempty-to-xunit-assert-empty` etc.). The
      Given/When/Then-style comments at the top of each test body (each
      block starts with "Corrected definitions:" / "Corrected scenario per
      spec Section X.Y:" referencing `madGLP-spec.md`) MUST carry into the
      target as a `/// <summary>` doc-comment block per method so the
      `madGLP-spec.md` section traceability survives the conversion
      (FR-024 doc-level — IDENTICAL to the spec-traceability rule applied
      in global_send_test.dart.md).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Method-body translation nuance (explicitly addressed): every `test`
      callback in this file is synchronous (no `async`/`Future`/`await`);
      target method returns `void` (xUnit also supports `async Task` for
      async tests — not applicable here). Closure-capture nuance: every
      `final ctx... = ...`, `final runtime... = ...`, `final (writer, reader)
      = ...`, `final ...Result = ...`, `final writer... = ...`, `final
      streamValue = ...`, `final derefed... = ...` is local to the test
      body, mapping 1-to-1 to local `var <name> = ...` in the C# method
      (see dart.expression.final_local_variable_with_initializer). Each
      test body ALSO assigns the `onMessageReady` field on one or more
      `MadContext` instances inside the test body — this is a statement-
      bodied lambda assignment to a delegate-typed field (see
      dart.expression.statement_bodied_lambda_assigned_to_delegate_field).
      Skip-semantics nuance (NOT firing here): no `skip:` argument
      anywhere, so NO `Skip=` property on `[Fact]`.
  - construct_key: dart.expression.final_local_variable_with_initializer
    source_form: >-
      "final runtimeP = GlpRuntime();
       final runtimeQ = GlpRuntime();
       final ctxP = MadContext(agentId: 'p', runtime: runtimeP);
       final ctxQ = MadContext(agentId: 'q', runtime: runtimeQ);
       final globalizeResult = globalize(variables: [...], ...);
       final localizeResult = localize(globalNames: ..., ...);
       final writerZq = localizeResult.freshPairs[0].writerAddr;
       final streamValue = StructTerm('.', [ConstTerm('add'), VarRef(readerXs1)]);
       final derefed = runtimeQ.heap.derefAddr(writerZq);
       final list = derefed as StructTerm;
       final writerYCharlie = charlieFromBob.freshPairs[0].writerAddr;"
    target_decision: >-
      Translate `final <name> = <expr>;` to `var <name> = <expr>;` in C#
      where the initializer is a constructor invocation, a method call, a
      list literal, a property access, or a cast expression. Specifically:
      `final runtimeP = GlpRuntime()` -> `var runtimeP = new GlpRuntime();`
      (note the mandatory C# `new` keyword — Dart's optional-`new`
      constructor call requires C#'s explicit `new`);
      `final ctxP = MadContext(agentId: 'p', runtime: runtimeP)` -> per
      the SUT spec `lib/multiagent/mad_context.dart.md` which collapses
      the Dart named-required ctor to a positional C# ctor, this becomes
      `var ctxP = new MadContext("p", runtimeP);` (call-site loses the
      `agentId:`/`runtime:` named-arg labels because the SUT spec
      explicitly pins a positional C# ctor — see
      dart.class.named_required_parameter_constructor_invocation below for
      the load-bearing nuance that distinguishes this SUT from
      `GlobalSendGoal`/`GlobalSendSpawn`);
      `final globalizeResult = globalize(variables: ..., localAgent: ...,
      remoteAgent: ..., table: ...)` ->
      `var globalizeResult = MadHelpers.Globalize(variables: ...,
      localAgent: ..., remoteAgent: ..., table: ...);` (top-level Dart
      function -> static method on `MadHelpers`, named arguments
      preserved — UNLESS `using static <RootNs>.Multiagent.MadHelpers;`
      is also emitted at file scope, in which case codegen MAY drop the
      `MadHelpers.` qualifier);
      `final localizeResult = localize(globalNames: ..., localAgent: ...,
      table: ..., freshAddrAllocator: () => runtimeQ.heap.allocateVariable())`
      -> `var localizeResult = MadHelpers.Localize(globalNames: ...,
      localAgent: ..., table: ..., freshAddrAllocator: () =>
      runtimeQ.Heap.AllocateVariable());` (the lambda parameter list is
      EMPTY here — a zero-arg arrow lambda — see
      dart.expression.lambda_zero_arg_arrow);
      `final writerZq = localizeResult.freshPairs[0].writerAddr` ->
      `var writerZq = localizeResult.FreshPairs[0].WriterAddr;`
      (indexer + PascalCased property chain);
      `final streamValue = StructTerm('.', [ConstTerm('add'),
      VarRef(readerXs1)])` -> `var streamValue = new StructTerm(".",
      new List<Term> { new ConstTerm("add"), new VarRef(readerXs1) });`
      (the inner Dart list literal becomes a `List<Term>` — see
      dart.expression.list_literal_typed_polymorphic; note the SUT spec
      `lib/runtime/terms.dart.md` pins the `StructTerm` positional ctor
      shape with `IReadOnlyList<Term>` parameter type);
      `final derefed = runtimeQ.heap.derefAddr(writerZq)` ->
      `var derefed = runtimeQ.Heap.DerefAddr(writerZq);`;
      `final list = derefed as StructTerm` ->
      `var list = (StructTerm)derefed;` (Dart `as` is a runtime-checked
      cast that throws `_CastError` if the cast fails; the C# unconditional
      cast `(StructTerm)x` ALSO throws `InvalidCastException` at runtime
      if the cast fails — semantics agree; the source-line is preceded by
      `expect(derefed, isA<StructTerm>())` so the cast is statically
      reachable AS-IF the assertion guarantees the type; see
      dart.expression.as_cast_after_isA_assertion below).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability-semantics nuance (IDENTICAL to global_send_test.dart.md):
      Dart `final <local>` prevents REBINDING the local after init but does
      NOT prevent mutation of the referenced object's state — exactly the
      same semantics as C# `var`. Constructor-syntax nuance: Dart allows
      `Foo(...)` without `new`; C# requires `new Foo(...)`. String-literal
      nuance: Dart `'p'` / `'q'` / `'alice'` / `'bob'` / `'charlie'` /
      `'add'` / `'.'` / `'hello_from_charlie'` / `'value_from_q'` are all
      single-quoted strings; C# uses ONLY `"..."` for `string`. Codegen MUST
      emit `new MadContext("p", runtimeP)` — single-quote literals would be
      `char` in C# and select non-existent `char`-arg constructors.
      Numeric-literal nuance (FIRST-RECORDED for the multiagent test specs):
      `ConstTerm(100)` (line 132) and `ConstTerm(100)` (line 139) use the
      Dart `int` literal `100` — passed to a `ConstTerm` ctor that accepts
      `Object?`. In C# the literal `100` defaults to `int`; `new
      ConstTerm(100)` boxes to `object` at the parameter boundary
      (`ConstTerm`'s `Value` property is declared `object?` per the SUT
      spec `lib/runtime/terms.dart.md`). Semantics agree. Per-test working-
      directory convention does NOT change the local-variable translation.
  - construct_key: dart.expression.record_destructuring_pattern_assignment
    source_form: >-
      "final (writerXs, readerXs) = runtimeP.heap.allocateVariable();
       final (writerXs1, readerXs1) = runtimeP.heap.allocateVariable();
       final (writerV, readerV) = runtimeP.heap.allocateVariable();
       final (writerXBob, readerXBob) = runtimeBob.heap.allocateVariable();
       final (writerX, readerX) = runtimeP.heap.allocateVariable();"
    target_decision: >-
      Dart 3 RECORD-DESTRUCTURING PATTERN: `final (a, b) = expr;` where
      `expr` returns a positional record `(T1, T2)`. The `Heap.allocateVariable()`
      method returns a positional record `(int writerAddr, int readerAddr)`
      per the sibling SUT spec `lib/runtime/heap_fcp.dart.md` (the heap's
      `allocateVariable` factory). In C# the counterpart is `ValueTuple<int,
      int>` (i.e. `(int, int)`) with DECONSTRUCTION assignment. The pinned
      idiom from `lib/multiagent/mad_context.dart.md`
      (`rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction`)
      is reused verbatim (FR-012 / SC-007 — no re-research). Specifically:
      `final (writerXs, readerXs) = runtimeP.heap.allocateVariable()` ->
      `var (writerXs, readerXs) = runtimeP.Heap.AllocateVariable();`
      (C# tuple deconstruction with `var` on the OUTER side; both elements
      inferred as `int`). Apply uniformly to all five occurrences in this
      file:
      lines 31-34 (Section 10.1, twice — for `(writerXs, readerXs)` and
      `(writerXs1, readerXs1)`);
      line 103 (Section 10.2, once — `(writerV, readerV)`);
      line 173 (Section 10.3, once — `(writerXBob, readerXBob)`);
      line 282 (Section 5.4, once — `(writerX, readerX)`).
      The SUT method MUST return a `(int, int)` ValueTuple per the heap
      SUT convspec — codegen MUST emit `public (int writerAddr, int
      readerAddr) AllocateVariable()` on the heap class. ALTERNATIVE
      `out int writerAddr, out int readerAddr` parameter shape is REJECTED
      (would force every Dart call site into `int writerAddr, readerAddr;
      runtimeP.Heap.AllocateVariable(out writerAddr, out readerAddr);`
      — verbose and inconsistent with the modern .NET tuple-returning
      convention).
    idiom_id: rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction
    research_finding_id: rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction
    nuance: >-
      Record-vs-tuple nuance (explicitly addressed, IDENTICAL to
      lib/multiagent/mad_context.dart.md): Dart 3 records are STRUCTURAL
      (positional/named field shape, no nominal type at the use site —
      ANY value with the same shape satisfies the type); C# ValueTuples
      are STRUCTURAL by NAME at the language level (tuple field names are
      hints, the underlying type is `ValueTuple<T1,T2,...>` which IS
      structural). Equality nuance: Dart records have value-equality
      (positional fields compared by `==`); C# `ValueTuple` overrides
      `Equals`/`GetHashCode` to compare element-wise — semantics agree.
      Field-name nuance: the Dart source uses POSITIONAL records (`(int,
      int)`) — no field names; the C# port preserves positional ValueTuple
      AT THE CALL SITE (deconstruction names `writerXs`/`readerXs` are
      LOCAL variable names, NOT tuple field names). Async/Future: ABSENT
      — `allocateVariable` is synchronous in both languages. Single-thread
      ownership nuance: heap operations are agent-owned per the
      MadContext-owns-runtime invariant pinned by
      `lib/multiagent/mad_context.dart.md` — no concurrent access; no
      lock/synchronisation needed.
  - construct_key: dart.class.named_constructor_factory
    source_form: >-
      "TermVar.reader(readerXs, writerAddr: writerXs)
       TermVar.writer(writerV, readerAddr: readerV)
       TermVar.reader(readerXBob, writerAddr: writerXBob)
       TermVar.writer(writerXBob, readerAddr: readerXBob)
       TermVar.writer(writerX, readerAddr: readerX)
       TermVar.reader(readerX, writerAddr: writerX)"
    target_decision: >-
      Dart's NAMED CONSTRUCTORS (`ClassName.namedCtor(...)`) — used here
      for `TermVar.reader` and `TermVar.writer` — have NO direct C#
      equivalent. The pinned mapping (recorded by globalize_test.dart.md
      and reused by global_send_test.dart.md as
      `rf-dart-named-constructor-to-csharp-static-factory`) is reused
      verbatim (FR-012 / SC-007): Dart `Foo.bar(args)` -> C# `Foo.Bar(args)`
      STATIC FACTORY METHOD on the converted class. So
      `TermVar.reader(readerXs, writerAddr: writerXs)` ->
      `TermVar.Reader(readerXs, writerAddr: writerXs)`;
      `TermVar.writer(writerV, readerAddr: readerV)` ->
      `TermVar.Writer(writerV, readerAddr: readerV)`. The factory method
      name is the named-constructor identifier PascalCased. The SUT spec
      `.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md` is
      the source of truth for the exact static-factory signatures emitted.
    idiom_id: rf-dart-named-constructor-to-csharp-static-factory
    research_finding_id: rf-dart-named-constructor-to-csharp-static-factory
    nuance: >-
      Constructor-semantics nuance (explicitly addressed and IDENTICAL to
      global_send_test.dart.md): Dart named constructors are CONSTRUCTORS;
      C# static factories are METHOD CALLS returning `new Foo(...)`. The
      ALTERNATIVE C# encoding — multiple constructor overloads
      disambiguated by parameter type — was rejected for `TermVar` because
      `TermVar.writer(int, {int readerAddr})` and `TermVar.reader(int,
      {int writerAddr})` differ ONLY by named-parameter LABEL, not by type
      signature — two `(int, int)` constructors would conflict.
      Same-class-different-tag nuance: `TermVar` is a sealed two-flavour
      record in Dart (reader-flavour vs writer-flavour, differentiated by
      an internal discriminator field); the C# port preserves the same
      shape via a single sealed class with a `Kind` enum discriminator
      (per SUT spec `mad_helpers.dart.md`).
  - construct_key: dart.class.named_required_parameter_constructor_invocation
    source_form: >-
      "MadContext(agentId: 'p', runtime: runtimeP)
       MadContext(agentId: 'q', runtime: runtimeQ)
       MadContext(agentId: 'alice', runtime: runtimeAlice)
       MadContext(agentId: 'bob', runtime: runtimeBob)
       MadContext(agentId: 'charlie', runtime: runtimeCharlie)
       globalize(variables: [...], localAgent: 'p', remoteAgent: 'q', table: ctxP.wp)
       localize(globalNames: ..., localAgent: 'q', table: ctxQ.wp, freshAddrAllocator: () => runtimeQ.heap.allocateVariable())
       ctxQ.handleMadAssignment(globalName: ..., value: ..., fromAgent: 'p')
       ctxP.handleMadAssignment(globalName: ..., value: ..., fromAgent: 'q')
       ctxBob.handleMadAssignment(globalName: ..., value: ConstTerm('hello_from_charlie'), fromAgent: 'charlie')
       ctxAlice.handleMadAssignment(globalName: ..., value: ConstTerm('hello_from_charlie'), fromAgent: 'bob')"
    target_decision: >-
      Dart's PRIMARY NAMED-REQUIRED constructor / NAMED-REQUIRED method-
      parameter invocation forms map to call-site translations whose
      target depends on the SUT spec for each callee:
      (a) `MadContext(agentId: ..., runtime: ...)` — the SUT spec
      `lib/multiagent/mad_context.dart.md` explicitly COLLAPSES the Dart
      named-required ctor to a POSITIONAL C# ctor `public MadContext(string
      agentId, GlpRuntime runtime)`. So the call site drops the `agentId:`
      / `runtime:` named-arg labels:
      `MadContext(agentId: 'p', runtime: runtimeP)` ->
      `new MadContext("p", runtimeP)`. This is a load-bearing DEVIATION
      from the general "preserve named args verbatim" rule applied to
      `globalize`/`localize`/`handleMadAssignment` — codegen MUST consult
      the SUT spec PER CALLEE.
      (b) `globalize(...)` / `localize(...)` — top-level Dart functions,
      converted per `mad_helpers.dart.md` to STATIC methods on `MadHelpers`
      with named PARAMETERS preserved (callable as `MadHelpers.Globalize(
      variables: ..., localAgent: ..., remoteAgent: ..., table: ...)` or
      unqualified under `using static`).
      (c) `ctx.handleMadAssignment(globalName: ..., value: ..., fromAgent:
      ...)` — instance method on `MadContext` per `mad_context.dart.md`
      with named parameters preserved; C# call site emits
      `ctxQ.HandleMadAssignment(globalName: ..., value: ..., fromAgent:
      "p")` (PascalCased method, camelCase named arguments).
      All Dart `{required Type name}` parameters MUST translate to C#
      parameters WITHOUT default values (so the compiler enforces the
      "must be supplied" guarantee) — IDENTICAL to the global_send_test.
      dart.md nuance.
    idiom_id: rf-dart-named-argument-to-csharp-named-argument
    research_finding_id: rf-dart-named-argument-to-csharp-named-argument
    nuance: >-
      Required-vs-optional nuance: see global_send_test.dart.md.
      Order-independence nuance: Dart named arguments may appear in any
      order; C# named arguments may also appear in any order — this file
      preserves the source call-site order verbatim.
      SUT-spec-determines-call-shape nuance (LOAD-BEARING, NEW for this
      file): the `MadContext` ctor at every call site here uses Dart named
      args, but the C# SUT ctor is POSITIONAL (per the explicit
      `mad_context.dart.md` decision). Codegen MUST NOT mechanically
      preserve named-arg labels at the call site — it MUST consult the
      SUT spec and choose between `new MadContext("p", runtimeP)`
      (positional, matches the C# SUT decision) and `new MadContext(
      agentId: "p", runtime: runtimeP)` (would compile but breaks the
      faithful translation contract that the SUT spec pinned positional).
      Codegen also MUST NOT name-collide on the per-`MadContext`-instance
      variable identifiers: the test bodies use `ctxP`/`ctxQ`/`ctxAlice`/
      `ctxBob`/`ctxCharlie` — identifier-safe in C#, carry over verbatim.
  - construct_key: dart.expression.lambda_zero_arg_arrow
    source_form: >-
      "freshAddrAllocator: () => runtimeQ.heap.allocateVariable()
       freshAddrAllocator: () => runtimeAlice.heap.allocateVariable()
       freshAddrAllocator: () => runtimeCharlie.heap.allocateVariable()"
    target_decision: >-
      Dart zero-arg arrow-style lambda `() => <expr>` maps to a C# zero-arg
      lambda `() => <expr>` directly — both languages require the empty
      parentheses for a zero-parameter lambda. Specifically
      `() => runtimeQ.heap.allocateVariable()` ->
      `() => runtimeQ.Heap.AllocateVariable()`. The lambda is assigned to
      the `freshAddrAllocator` parameter declared as
      `Func<(int writerAddr, int readerAddr)>` per the SUT spec
      `mad_helpers.dart.md` (returns a ValueTuple of two ints). Reuses the
      pinned arrow-lambda idiom from global_send_test.dart.md.
    idiom_id: rf-dart-arrow-lambda-to-csharp-lambda
    research_finding_id: rf-dart-arrow-lambda-to-csharp-lambda
    nuance: >-
      Zero-arg-lambda nuance (explicitly addressed, NEW relative to
      global_send_test.dart.md which only exercised single-arg lambdas):
      both Dart and C# require `()` for a zero-parameter lambda — there is
      no single-token shorthand. Return-type nuance: the lambda returns
      the heap's allocate-pair record `(int, int)`; on the C# side this
      is a `ValueTuple<int, int>`; the lambda's inferred return type
      MUST match the `Func<(int, int)>` parameter type signature on
      `Localize`. Capture-and-closure nuance: each lambda captures one
      enclosing local (`runtimeQ` / `runtimeAlice` / `runtimeCharlie`)
      by reference; closure semantics agree between Dart and C# at the
      use-site.
  - construct_key: dart.expression.statement_bodied_lambda_assigned_to_delegate_field
    source_form: >-
      "ctxP.onMessageReady = (dest, msg) { if (dest == 'q') { ctxQ.handleMadAssignment(...); } };
       ctxQ.onMessageReady = (dest, msg) { if (dest == 'p') { ctxP.handleMadAssignment(...); } };
       ctxCharlie.onMessageReady = (dest, msg) { if (dest == 'bob') { ctxBob.handleMadAssignment(...); ctxBob.onWriterBound(...); ctxBob.flushMessages(); } };
       ctxBob.onMessageReady = (dest, msg) { if (dest == 'alice') { ctxAlice.handleMadAssignment(...); } };"
    target_decision: >-
      Dart STATEMENT-BODY anonymous function `(arg1, arg2) { <statements>; }`
      assigned to a delegate-typed field. Maps to a C# STATEMENT-BODY
      lambda `(arg1, arg2) => { <statements>; }`. The right-hand side
      assigns to the `OnMessageReady` field declared `MessageDeliveryCallback?
      OnMessageReady;` per the SUT spec `mad_context.dart.md` (a
      DELEGATE-typed nullable field, NOT an `event`). Specifically:
      `ctxP.onMessageReady = (dest, msg) { if (dest == 'q') {
      ctxQ.handleMadAssignment(globalName: ..., value: ..., fromAgent:
      'p'); } };` -> `ctxP.OnMessageReady = (dest, msg) => { if (dest ==
      "q") { ctxQ.HandleMadAssignment(globalName: ..., value: ...,
      fromAgent: "p"); } };`. The C# lambda parameters' static types are
      inferred from the `MessageDeliveryCallback` delegate signature
      (`(string destination, OutboundMessage message)`) — codegen MAY emit
      explicit parameter types `(string dest, OutboundMessage msg) => { ... }`
      OR rely on inference `(dest, msg) => { ... }`; the inferred form is
      shorter and matches the Dart shape. The four occurrences in this
      file (one in each test) all follow the same pattern: a delegate
      field assignment of a statement-body lambda that pattern-matches on
      the `dest` argument and invokes `handleMadAssignment` on the matching
      receiver `MadContext`. The Section 10.3 case also calls
      `ctxBob.onWriterBound(...)` and `ctxBob.flushMessages()` inside the
      lambda body, demonstrating that the lambda may issue MULTIPLE method
      calls (a sequence of statements, NOT a single expression).
    idiom_id: null
    research_finding_id: rf-dart-statement-body-lambda-to-csharp-statement-body-lambda
    nuance: >-
      Statement-body-vs-arrow nuance (explicitly addressed, NEW for this
      file — globalize_test.dart.md and global_send_test.dart.md only had
      ARROW-form `(arg) => expr` lambdas, never statement-body):
      Dart `(args) { stmts }` is the equivalent of C# `(args) => { stmts; }`
      — note the `=>` arrow is REQUIRED on the C# side even for the
      statement-body form (it separates the parameter list from the body
      block). Both languages allow `return <expr>;` inside the block; both
      languages allow control flow (`if`/`for`/`while`); both languages
      allow `void` lambdas without an explicit `return` for the
      no-return-value case (here every body is `void`-returning per
      `MessageDeliveryCallback` signature). Closure-capture nuance: each
      lambda captures the OTHER `MadContext` instance from the enclosing
      test scope (e.g. `ctxP.onMessageReady` captures `ctxQ`); closure
      semantics agree between Dart and C#. Async/Future nuance: ABSENT —
      every lambda is synchronous; the delegate signature is `void`-returning,
      so a `Task`-returning lambda would not satisfy
      `MessageDeliveryCallback` (an `async Action<...>` would be
      `async void` — discouraged). Delegate-vs-event nuance: per the SUT
      spec, `OnMessageReady` is a PUBLIC delegate-typed field, NOT an
      `event` — direct assignment with `=` is valid C#; `+=` (multicast)
      is grammatically allowed but the test uses single-assignment
      semantics matching the Dart shape. Authoritative Dart side:
      dart.dev language tour `Functions / anonymous functions`
      (`https://dart.dev/language/functions#anonymous-functions`).
      Authoritative .NET side: Microsoft Learn `Lambda expressions`
      statement-body section
      (`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/lambda-expressions#statement-lambdas`).
      Both sides authoritative.
  - construct_key: dart.expression.list_literal_typed_polymorphic
    source_form: >-
      "[TermVar.reader(readerXs, writerAddr: writerXs)]
       [TermVar.writer(writerV, readerAddr: readerV)]
       [TermVar.reader(readerXBob, writerAddr: writerXBob)]
       [TermVar.writer(writerXBob, readerAddr: readerXBob)]
       [TermVar.writer(writerX, readerAddr: readerX), TermVar.reader(readerX, writerAddr: writerX)]
       [ConstTerm('add'), VarRef(readerXs1)]
       [ConstTerm('add'), VarRef(readerXs1)]   (second use, inside handleMadAssignment value)"
    target_decision: >-
      Dart list literal `[a, b]` whose static element type is inferred from
      the call-site signature (here `List<TermVar>` for the `globalize`
      `variables:` parameter, and `List<Term>` for the `StructTerm` second
      ctor parameter) maps to C# `new List<T> { a, b }`
      (collection-initializer syntax on `System.Collections.Generic.List<T>`).
      The `using System.Collections.Generic;` at file scope (see
      dart.package_test.import_directive nuance) makes `List<T>` resolvable.
      Specifically: `[TermVar.reader(readerXs, writerAddr: writerXs)]` ->
      `new List<TermVar> { TermVar.Reader(readerXs, writerAddr: writerXs) }`;
      `[TermVar.writer(writerX, readerAddr: readerX), TermVar.reader(readerX,
      writerAddr: writerX)]` -> `new List<TermVar> { TermVar.Writer(writerX,
      readerAddr: readerX), TermVar.Reader(readerX, writerAddr: writerX) }`;
      `[ConstTerm('add'), VarRef(readerXs1)]` (the inner `StructTerm` args
      list, used twice — once at line 72 and once at line 64 inside the
      `value:` of `handleMadAssignment`) -> `new List<Term> { new
      ConstTerm("add"), new VarRef(readerXs1) }`. Element types are
      polymorphic in the latter case (`ConstTerm` and `VarRef` are both
      subclasses of `Term`), which works because the call site assigns to
      a `List<Term>` (or `IReadOnlyList<Term>` per the SUT spec — `List<T>`
      satisfies `IReadOnlyList<T>`).
    idiom_id: rf-dart-list-literal-to-csharp-list-initializer
    research_finding_id: rf-dart-list-literal-to-csharp-list-initializer
    nuance: >-
      Collection-type nuance (IDENTICAL to global_send_test.dart.md): Dart
      `List<T>` growable maps to C# `List<T>` growable — same runtime
      characteristic. Polymorphic-element nuance (NEW relative to
      global_send_test.dart.md, which had homogeneous `List<GlobalSendSpawn>`):
      the `StructTerm` args list is `[ConstTerm, VarRef]` — two distinct
      `Term` subclasses; C# `new List<Term> { new ConstTerm(...), new
      VarRef(...) }` REQUIRES the explicit element-type `Term` (the C#
      collection-initializer infers the element type from the
      `List<T>` generic argument, so the explicit `<Term>` is mandatory —
      `new List { ... }` would not compile). ALTERNATIVE
      `new Term[] { ... }` (an array literal) is REJECTED for consistency
      with the sibling test specs and because the SUT side declares
      `IReadOnlyList<Term>` (an array also satisfies that, but `List<T>`
      is more growable-friendly if downstream codegen needs to extend the
      collection). Index-access nuance: `list.args[0]` (line 84) and
      `list.args[0] as ConstTerm` (line 85) translate to `list.Args[0]`
      and `(ConstTerm)list.Args[0]` respectively — the C# indexer on
      `List<T>` (or on `IReadOnlyList<T>`) is identical in shape to the
      Dart subscript operator.
  - construct_key: dart.expression.expect_isA_to_xunit_assert_istype
    source_form: >-
      "expect(derefed, isA<StructTerm>());
       expect(list.args[0], isA<ConstTerm>());
       expect(derefed, isA<ConstTerm>());
       expect(derefedP, isA<ConstTerm>());
       expect(derefedQ, isA<ConstTerm>());"
    target_decision: >-
      Dart `expect(actual, isA<T>())` (asserts that `actual` is an instance
      of type `T`, returns nothing — the assertion either passes or throws
      a `TestFailure`) maps to xUnit `Assert.IsType<T>(actual)` (which
      ALSO returns the value cast to `T` — a useful side-channel exploited
      in test_channel_construction.dart.md's
      `rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return` idiom).
      Here the spec REUSES the simple non-folding form:
      `expect(derefed, isA<StructTerm>())` ->
      `Assert.IsType<StructTerm>(derefed);` (discards the returned cast
      value because the next line performs a separate `as StructTerm` cast
      — see dart.expression.as_cast_after_isA_assertion). Codegen MAY
      optimise to the folded form
      `var list = Assert.IsType<StructTerm>(derefed);` (eliminating both
      the simple assert AND the subsequent `as StructTerm` cast), per the
      pinned idiom `rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`.
      The simple form is the safe minimum; the folded form is the
      stylistically-preferred optimisation. Apply to all five occurrences:
      lines 81/84 (Section 10.1), 147 (Section 10.2), 258 (Section 10.3),
      356/361 (Section 5.4).
    idiom_id: rf-dart-expect-isA-to-xunit-assert-istype
    research_finding_id: rf-dart-expect-isA-to-xunit-assert-istype
    nuance: >-
      Type-assertion nuance (IDENTICAL to test_channel_construction.dart.md
      and heap/binding_pointer_test.dart.md): Dart `isA<T>` is a matcher
      that returns `true`/`false` and `expect` wraps it in a failure; xUnit
      `Assert.IsType<T>` throws `IsTypeException` on failure. Both produce
      the same outcome (test fails). Strict-vs-loose nuance: Dart `isA<T>()`
      is loose — it returns `true` if `actual` is `T` or any subtype; xUnit
      `Assert.IsType<T>(actual)` is STRICT — fails if `actual` is a STRICT
      SUBCLASS of `T`. For this file the comparisons are to leaf types
      (`StructTerm`/`ConstTerm` have no subclasses in the runtime), so the
      strict/loose distinction does not surface. If a future test
      compared to a non-leaf type, codegen would have to substitute
      `Assert.IsAssignableFrom<T>(actual)` (the loose form) — recorded for
      future reference.
  - construct_key: dart.expression.as_cast_after_isA_assertion
    source_form: >-
      "final list = derefed as StructTerm;
       (list.args[0] as ConstTerm).value
       (derefed as ConstTerm).value
       (derefedP as ConstTerm).value
       (derefedQ as ConstTerm).value"
    target_decision: >-
      Dart `as T` is a runtime-checked cast that throws `_CastError` if
      the cast fails. The C# counterpart is the unconditional cast
      `(T)expr` (which throws `InvalidCastException` at runtime if the cast
      fails). Semantics agree. After the preceding `Assert.IsType<T>(expr)`
      the cast is statically guaranteed to succeed. Translate:
      `final list = derefed as StructTerm` -> `var list = (StructTerm)derefed;`
      OR equivalently `var list = Assert.IsType<StructTerm>(derefed);`
      (folded form — see preceding construct);
      `(list.args[0] as ConstTerm).value` -> `((ConstTerm)list.Args[0]).Value`;
      `(derefed as ConstTerm).value` -> `((ConstTerm)derefed).Value`;
      `(derefedP as ConstTerm).value` -> `((ConstTerm)derefedP).Value`;
      `(derefedQ as ConstTerm).value` -> `((ConstTerm)derefedQ).Value`.
      The folded form `Assert.IsType<ConstTerm>(derefed).Value` is the
      stylistic optimum because it eliminates the redundant `Assert.IsType
      <ConstTerm>(derefed);` + separate `(ConstTerm)derefed` cast.
    idiom_id: rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return
    research_finding_id: rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return
    nuance: >-
      As-cast-semantics nuance (explicitly addressed, IDENTICAL to
      test_channel_construction.dart.md): Dart `as T` and C# `(T)expr` BOTH
      throw at runtime on cast failure — semantically identical. The
      `expr as T?` form (Dart safe-cast returning null) is NOT used in
      this file. Folded-vs-separate nuance: separate `Assert.IsType<T>(x)`
      + later `(T)x` cast is two operations; the folded `var t =
      Assert.IsType<T>(x);` is one — both produce identical runtime
      behaviour; codegen prefers the folded form for terseness.
  - construct_key: dart.expression.expect_equals_to_xunit_assertequal
    source_form: >-
      "expect(globalizeResult.spawns.length, 1);
       expect(ctxP.wp.globalizeEntryCount, 0);
       expect(localizeResult.useReader[0], true);
       expect(list.functor, '.');
       expect((list.args[0] as ConstTerm).value, 'add');
       expect(ctxP.wp.lookupByIndex(1), isNotNull);
       expect(globalizeResult.spawns, isEmpty);
       expect(localizeResult.useReader[0], false);
       expect((derefed as ConstTerm).value, 100);
       expect(bobToAliceGlobal.spawns.length, 1);
       expect(ctxBob.wp.globalizeEntryCount, 0);
       expect(aliceFromBob.useReader[0], true);
       expect(aliceFromBob.spawns, isEmpty);
       expect(bobToCharlieGlobal.spawns, isEmpty);
       expect(ctxBob.wp.globalizeEntryCount, 1);
       expect(charlieFromBob.useReader[0], false);
       expect(charlieFromBob.spawns.length, 1);
       expect((derefed as ConstTerm).value, 'hello_from_charlie');
       expect(globalizeResult.globalNames.length, 2);
       expect(globalizeResult.globalNames[0], GlobalName.writer('p', 1));
       expect(globalizeResult.globalNames[1], GlobalName.reader('p', 2));
       expect(globalizeResult.spawns.length, 1);
       expect(ctxP.wp.globalizeEntryCount, 1);
       expect(localizeResult.useReader[0], false);
       expect(localizeResult.useReader[1], true);
       expect(localizeResult.spawns.length, 1);
       expect((derefedP as ConstTerm).value, 'value_from_q');
       expect((derefedQ as ConstTerm).value, 'value_from_q');"
    target_decision: >-
      Dart `expect(actual, expected)` where `expected` is a literal value
      (not a matcher) maps to xUnit `Assert.Equal(expected, actual)` — note
      the ARGUMENT ORDER FLIPS (Dart puts actual first; xUnit puts expected
      first). Specifically:
      `expect(globalizeResult.spawns.length, 1)` ->
      `Assert.Equal(1, globalizeResult.Spawns.Count);` (Dart `.length` on
      `List<T>` -> C# `.Count` on `List<T>` per the SUT spec);
      `expect(list.functor, '.')` -> `Assert.Equal(".", list.Functor);`
      (single-quote Dart -> double-quote C#);
      `expect((list.args[0] as ConstTerm).value, 'add')` ->
      `Assert.Equal("add", ((ConstTerm)list.Args[0]).Value);`;
      `expect(localizeResult.useReader[0], true)` ->
      `Assert.True(localizeResult.UseReader[0]);` (special-case: Dart
      boolean literal `true` -> C# `Assert.True(actual);` — see next
      construct);
      `expect(globalizeResult.globalNames[0], GlobalName.writer('p', 1))` ->
      `Assert.Equal(GlobalName.Writer("p", 1), globalizeResult.GlobalNames[0]);`
      (GlobalName equality MUST be value-equality on the C# side —
      the SUT spec `mad_helpers.dart.md` pins `GlobalName` as a `record`
      or `IEquatable<GlobalName>` value type for exactly this reason;
      reference-equality would fail because the two `GlobalName.Writer`
      instances are different objects).
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order nuance (LOAD-BEARING, IDENTICAL to
      test_channel_construction.dart.md and heap/binding_pointer_test.dart.md):
      Dart `expect(actual, expected)`; xUnit `Assert.Equal(expected,
      actual)` — codegen MUST flip the order. Boolean-special-case nuance:
      `expect(x, true)` / `expect(x, false)` translates to `Assert.True(x)`
      / `Assert.False(x)` — NOT `Assert.Equal(true, x)` (which would also
      work but is less idiomatic in xUnit). isNotNull / isEmpty matcher
      nuance: `expect(x, isNotNull)` -> `Assert.NotNull(x);`; `expect(x,
      isEmpty)` -> `Assert.Empty(x);` (xUnit's `Assert.Empty` accepts any
      `IEnumerable`). Value-equality nuance for `GlobalName`: the C# side
      MUST treat `GlobalName.Writer("p", 1)` as value-equal to another
      `GlobalName.Writer("p", 1)` — pinned by the SUT spec
      `mad_helpers.dart.md` (record type or `IEquatable<>` override).
  - construct_key: dart.expression.expect_istrue_isfalse_isempty_isnotnull
    source_form: >-
      "expect(localizeResult.useReader[0], true)
       expect(localizeResult.useReader[0], false)
       expect(localizeResult.useReader[1], true)
       expect(ctxP.wp.lookupByIndex(1), isNotNull)
       expect(globalizeResult.spawns, isEmpty)
       expect(aliceFromBob.spawns, isEmpty)
       expect(bobToCharlieGlobal.spawns, isEmpty)"
    target_decision: >-
      Boolean-matcher and presence-matcher idioms route to xUnit's typed
      assertion methods (more readable than `Assert.Equal(true, ...)` /
      `Assert.NotNull(...) == null` etc.). Specifically:
      `expect(x, true)` -> `Assert.True(x);`;
      `expect(x, false)` -> `Assert.False(x);`;
      `expect(x, isNotNull)` -> `Assert.NotNull(x);`;
      `expect(x, isEmpty)` -> `Assert.Empty(x);` (xUnit `Assert.Empty`
      accepts any `IEnumerable` — works for `IReadOnlyList<GlobalSendSpawn>`
      and for `List<T>` alike). Pinned idiom names reused from
      heap/binding_pointer_test.dart.md:
      `rf-dart-expect-istrue-to-xunit-asserttrue`,
      `rf-dart-expect-isfalse-to-xunit-assertfalse`,
      `rf-dart-expect-isnotnull-to-xunit-assertnotnull`,
      `rf-dart-expect-isempty-to-xunit-assert-empty`.
    idiom_id: rf-dart-expect-istrue-to-xunit-asserttrue
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      Multiple-matcher nuance (FIRST-RECORDED grouping for this file):
      this construct intentionally covers FOUR sibling matchers
      (`isTrue`/`isFalse`/`isNotNull`/`isEmpty`) under one construct row
      because all four route through the same xUnit `Assert.<X>` family
      and share the same translation discipline (the actual-only form,
      no `expected`-arg flip). The structured-block entry pins ONE
      research_finding_id (`-istrue-to-xunit-asserttrue`) as canonical;
      the three siblings reuse pinned KB rows discovered by prior test
      specs and ARE NOT duplicated as separate construct rows.
  - construct_key: dart.expression.method_invocation_on_owned_madcontext
    source_form: >-
      "ctxP.registerGlobalSendSpawns(globalizeResult.spawns);
       ctxQ.registerGlobalSendSpawns(localizeResult.spawns);
       ctxBob.registerGlobalSendSpawns(bobToAliceGlobal.spawns);
       ctxCharlie.registerGlobalSendSpawns(charlieFromBob.spawns);
       ctxP.onWriterBound(writerXs, streamValue);
       ctxP.flushMessages();
       ctxQ.onWriterBound(writerVq, ConstTerm(100));
       ctxQ.flushMessages();
       ctxBob.onWriterBound(writerXBob, ConstTerm('hello_from_charlie'));
       ctxBob.flushMessages();
       ctxCharlie.onWriterBound(writerYCharlie, ConstTerm('hello_from_charlie'));
       ctxCharlie.flushMessages();
       ctxP.onWriterBound(writerX, ConstTerm('value_from_q'));
       ctxP.flushMessages();
       runtimeP.heap.bindVariable(writerXs, streamValue);
       runtimeQ.heap.bindVariable(writerVq, ConstTerm(100));
       runtimeCharlie.heap.bindVariable(writerYCharlie, ConstTerm('hello_from_charlie'));
       runtimeQ.heap.bindVariable(writerYq, ConstTerm('value_from_q'));
       runtimeP.heap.derefAddr(writerV);
       runtimeQ.heap.derefAddr(writerZq);
       runtimeAlice.heap.derefAddr(writerZAlice);
       runtimeP.heap.derefAddr(writerX);
       runtimeQ.heap.derefAddr(writerZq);"
    target_decision: >-
      Ordinary Dart instance-method invocation `receiver.method(args)`
      maps to C# `receiver.Method(args)` with PascalCased method name.
      Each method's C# signature is pinned by the SUT spec:
      `MadContext.registerGlobalSendSpawns` ->
      `MadContext.RegisterGlobalSendSpawns(IReadOnlyList<GlobalSendSpawn>)`
      (per `mad_context.dart.md`);
      `MadContext.onWriterBound` ->
      `MadContext.OnWriterBound(int writerId, Term value)`;
      `MadContext.flushMessages` -> `MadContext.FlushMessages()` (returns
      `int` count per the SUT spec);
      `runtime.heap.bindVariable` ->
      `runtime.Heap.BindVariable(int writerAddr, Term value)` (per
      `lib/runtime/heap_fcp.dart.md` — heap-mutation operation; void
      return);
      `runtime.heap.derefAddr` ->
      `runtime.Heap.DerefAddr(int addr)` (returns `Term` per the heap SUT
      spec — the resolved-down term after following all set-pointers).
      No async; all calls are synchronous in both languages.
    idiom_id: null
    research_finding_id: rf-dart-instance-method-call-to-csharp-pascalcase-call
    nuance: >-
      Member-naming-PascalCase nuance (project-wide rule, IDENTICAL to all
      sibling test specs): Dart instance methods are camelCase by
      convention; C# instance methods are PascalCase by convention.
      Codegen MUST PascalCase every method name AND every property name
      while leaving local variables, parameters, and named-argument labels
      as camelCase. Receiver-chain nuance: `runtimeP.heap.bindVariable(...)`
      has two PascalCased members — `Heap` (the property) and `BindVariable`
      (the method); both flip from camelCase to PascalCase. State-mutation
      nuance: `bindVariable` and `onWriterBound` MUTATE per-agent heap and
      MadContext state — single-threaded per the agent-ownership invariant
      from `mad_context.dart.md` (NO lock/synchronisation needed).
  - construct_key: dart.expression.indexed_property_access
    source_form: >-
      "localizeResult.freshPairs[0].writerAddr
       globalizeResult.globalNames[0]
       globalizeResult.globalNames[1]
       localizeResult.useReader[0]
       localizeResult.useReader[1]
       list.args[0]
       (list.args[0] as ConstTerm)
       aliceFromBob.freshPairs[0].writerAddr
       charlieFromBob.freshPairs[0].writerAddr
       localizeResult.freshPairs[0].writerAddr (Section 5.4)
       localizeResult.freshPairs[1].writerAddr (Section 5.4)
       globalizeResult.globalNames[0]
       bobToAliceGlobal.globalNames[0]
       bobToCharlieGlobal.globalNames[0]"
    target_decision: >-
      Dart `expr[index]` on a `List<T>` (subscript operator) maps to C#
      `expr[index]` on a `List<T>` or `IReadOnlyList<T>` (indexer
      operator — IDENTICAL syntax). The receiver's PascalCased property
      name applies (`freshPairs` -> `FreshPairs`, `globalNames` ->
      `GlobalNames`, `useReader` -> `UseReader`, `args` -> `Args`).
      Specifically `localizeResult.freshPairs[0].writerAddr` ->
      `localizeResult.FreshPairs[0].WriterAddr;` (the trailing
      `.writerAddr` is a CAMELCASE field-name on the `(int writerAddr,
      int readerAddr)` ValueTuple — preserved as `WriterAddr` if the SUT
      spec for `mad_helpers.dart` pins a named-field record `FreshPair`
      with a `WriterAddr` property, OR as `.Item1`/`.Item2` if the SUT
      uses the raw ValueTuple form). The SUT spec
      `lib/multiagent/mad_helpers.dart.md` is the source of truth — codegen
      consults it. Out-of-range index nuance: Dart `List` throws `RangeError`
      on OOB; C# `List<T>` throws `ArgumentOutOfRangeException` —
      semantically equivalent (both throw, no silent failure).
    idiom_id: null
    research_finding_id: rf-dart-list-indexer-to-csharp-list-indexer
    nuance: >-
      Indexer-syntax nuance (explicitly addressed): both languages use
      `expr[i]` for list/array indexing; the syntax is IDENTICAL.
      Member-name nuance (LOAD-BEARING for this file): `freshPairs[0].
      writerAddr` chains an indexer with a property access; the C# port
      preserves the chain shape but PascalCases each member name. If the
      SUT chose to expose `freshPairs` as `List<(int writerAddr, int
      readerAddr)>` (a ValueTuple list), the trailing `.writerAddr` in C#
      would still be `.writerAddr` (tuple-field-name preserved as camelCase
      — tuple element names are NOT PascalCased by the .NET naming
      guideline). Codegen MUST consult the SUT spec to decide: if
      `FreshPair` is a named record then `.WriterAddr`; if a ValueTuple
      then `.writerAddr`.
  - construct_key: dart.expression.dart_3_numeric_underscore_in_identifier
    source_form: >-
      "X_c, Y_q, V_q, Y_c, Xs1, Z_a, Z_q (Dart identifiers in COMMENTS only,
      not in executable code — the executable code uses camelCase
      variable names like `writerXs`/`writerYq`/`writerZAlice`)"
    target_decision: >-
      No executable-code translation required — the underscore-bearing
      identifiers (`X_c`, `Y_q`, `V_q`, `Z_a`, `Z_q`, `Xs1`, `_w(p,1)`,
      `_r(p,2)`, etc.) appear ONLY inside Dart `//` comments and inside
      string-literal arguments to `globalName:` parameters (where they
      reference the spec's notation for global names like `_w(p,1)`). For
      the comment-only cases, codegen preserves the underscore-bearing
      tokens VERBATIM inside the `///` doc-comment block. For the executable
      identifiers, the test bodies use camelCase Dart locals (`writerXBob`,
      `writerYCharlie`, `readerXs1`, etc.) that carry over as-is to C#
      local-variable names (Dart camelCase locals = C# camelCase locals;
      identifier-safe in both languages).
    idiom_id: null
    research_finding_id: rf-dart-identifier-spec-notation-in-comments-preserved
    nuance: >-
      Spec-notation-in-comments nuance (explicitly addressed, FIRST-RECORDED
      for the multiagent test specs): the source file's comments encode
      `madGLP-spec.md` mathematical notation (`X?`, `_w(p,1)`, `_r(p,1)`,
      `[value(V?)|...]`, `:=`) — these characters are NOT valid C#
      identifier characters, but they survive intact inside `///` doc-
      comment text. Codegen MUST preserve the comment text verbatim so the
      spec-traceability link is not lost. No executable translation needed;
      no idiom_id created (the rf-id is purely an idiom-KB anchor for the
      "preserve spec notation in comments" rule).
conversion_units:
  - file_header_and_using_directives_block
    # Drop Dart imports; emit `using Xunit;`, `using System.Collections.Generic;`,
    # `using <RootNs>.Runtime;`, `using <RootNs>.Multiagent;`, optional
    # `using static <RootNs>.Multiagent.MadHelpers;`.
  - namespace_declaration
    # `namespace <RootNs>.Test.Multiagent;` (file-scoped namespace per .NET 6+
    # convention) — mirrors the Dart test/multiagent directory path.
  - class_Section101DirectCommunicationClientMonitorTests
    # Single `[Fact]` method PSendsStreamXToQPAssignsXAddXs1QReceives —
    # arrange (two GlpRuntime + two MadContext + four heap variable pairs),
    # act (globalize + localize + assign onMessageReady delegate + bindVariable
    # + onWriterBound + flushMessages), assert (Assert.IsType<StructTerm> +
    # Assert.Equal on functor/args).
  - class_Section102ReturnValueScenarioTests
    # Single `[Fact]` method PSendsValueVToQQAssignsV_qSumPReceivesSum —
    # writer-globalize path; assertions on lookupByIndex / spawns isEmpty;
    # final Assert.Equal(100, ((ConstTerm)derefed).Value).
  - class_Section103FriendMediatedIntroductionTests
    # Single `[Fact]` method BobForwardsXFromAliceToCharlieCharlieAssignsAliceReceives
    # — three-runtime/three-MadContext arrangement; two-hop forwarding via
    # nested onMessageReady delegate handlers; final Assert on Alice's
    # received value.
  - class_Section54BothEndsExportedTests
    # Single `[Fact]` method PExportsXXToQQAssignsY_qTTFlowsBackToP —
    # both-ends-exported scenario with TWO TermVar entries in the globalize
    # variables list; two-direction message routing; final Assert.Equal on
    # both writerX (at p) and writerZq (at q) — both receive the value.
escalations: []
```

## B. Embedded human-readable rationale + provenance

This file is the **scenario-level** test for the madGLP multi-agent
implementation. Unlike the per-component test files
(`global_send_test.dart`, `globalize_test.dart`, `localize_test.dart`,
`global_writers_table_test.dart`, `mad_error_handling_test.dart`), each of
its four tests exercises the FULL end-to-end path from `globalize` →
heap-binding → `onWriterBound` → `flushMessages` → cross-agent message
delivery → `handleMadAssignment` → heap-binding on the receiver →
(possibly) onward propagation. The scenarios are taken verbatim from
`madGLP-spec.md` Sections 5.4, 10.1, 10.2, 10.3 — the section labels are
the test's group names and the spec-traceability path is the test's most
valuable structural property. Every conversion decision below has been
made under the constraint that the spec-traceability survives the port.

### rf-dart-package-test-to-dotnet-xunit — project-wide xUnit framework choice

REUSED VERBATIM from sibling specs. xUnit is the project-wide test
framework. The two distinguishing facts for THIS file relative to the
sibling specs are (a) four sibling groups instead of one or two, and
(b) every test scenario carries a `madGLP-spec.md` section number that
MUST survive as a class `[Trait]`. Authoritative Dart side:
[dart.dev `package:test`](https://dart.dev/tools/dart-test#the-package-test-package).
Authoritative .NET side:
[Microsoft Learn xUnit + .NET Test Sdk](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test).

### rf-dart-package-sut-import-to-csharp-using — two-namespace SUT import collapse

This file imports SIX SUT files, collapsing to TWO `using` directives:
`using <RootNs>.Runtime;` for `runtime.dart` + `terms.dart`, and
`using <RootNs>.Multiagent;` for `mad_context.dart` + `message_queue.dart`
+ `mad_helpers.dart` + `global_send.dart`. The `OutboundMessage` type is
referenced only as a parameter type of the `MessageDeliveryCallback`
delegate — never constructed in the test body. Codegen SHOULD also emit
`using static <RootNs>.Multiagent.MadHelpers;` so the call sites read
`Globalize(...)` / `Localize(...)` unqualified, matching the Dart shape.

### rf-dart-test-main-to-xunit-class-with-facts + group-block — four sibling classes

Four `group(...)` calls inside `main` → four sibling public classes.
Each class name encodes both the spec-section number AND the scenario
title with non-identifier chars stripped (`Section101DirectCommunicationClientMonitorTests`
etc.). Each class carries `[Trait("Group", "<original label>")]` so the
spec-section number is queryable from the xUnit test reporter.

### rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction — heap alloc-pair pattern

The `final (writerXs, readerXs) = runtimeP.heap.allocateVariable();`
construct appears FIVE times in this file (once per agent-stream the test
sets up). All five reuse the pinned idiom from
`lib/multiagent/mad_context.dart.md` — Dart 3 positional record →
C# `ValueTuple<int,int>` with deconstruction `var (a, b) = ...;`. No
research re-derivation; KB hit (FR-012 / SC-007).
Authoritative Dart side:
[dart.dev language tour `Records`](https://dart.dev/language/records).
Authoritative .NET side:
[Microsoft Learn `Tuple types` § deconstruction](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-tuples#tuple-assignment-and-deconstruction).

### rf-dart-statement-body-lambda-to-csharp-statement-body-lambda — FIRST RECORDED for the multiagent test specs

The four `onMessageReady = (dest, msg) { ... }` assignments are
STATEMENT-BODY lambdas (multi-line `{ stmts; }` block) — distinct from
the arrow-body `(arg) => expr` lambdas already pinned by
`global_send_test.dart.md`. This spec records the new rf-id
`rf-dart-statement-body-lambda-to-csharp-statement-body-lambda` for the
KB; the per-construct nuance documents the body-block vs arrow-expression
distinction. The lambda is assigned to a delegate-typed FIELD (not an
`event`), so single-assignment `=` is preserved; multicast `+=` is
grammatically allowed but not the source convention. Authoritative Dart
side:
[dart.dev language tour `Functions / anonymous functions`](https://dart.dev/language/functions#anonymous-functions).
Authoritative .NET side:
[Microsoft Learn `Lambda expressions` § statement lambdas](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/lambda-expressions#statement-lambdas).

### MadContext ctor: positional C# despite Dart named-required — SUT-spec-determined call shape

LOAD-BEARING: every `MadContext(agentId: 'p', runtime: runtimeP)` call
site here uses Dart NAMED-REQUIRED args, but the SUT spec
`lib/multiagent/mad_context.dart.md` explicitly pins a POSITIONAL C# ctor
`public MadContext(string agentId, GlpRuntime runtime)`. The conversion
DROPS the named-arg labels at every call site — `new MadContext("p",
runtimeP)`. This contrasts with `globalize(...)` / `localize(...)` /
`handleMadAssignment(...)` which preserve named args verbatim because
their SUT specs pin named C# parameters. Codegen MUST consult the
per-callee SUT spec — there is no file-local rule.

### rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return — folded type-check + cast

The five `expect(x, isA<T>())` + subsequent `(x as T)` pairs are folded
into `var t = Assert.IsType<T>(x);` per the pinned idiom from
`test_channel_construction.dart.md`. The folded form is stylistically
preferred; the separate form is the safe minimum. Codegen MAY emit either.

### Spec-section preservation

Every test method emits a `/// <summary>` doc-comment block carrying the
`madGLP-spec.md` Section X.Y reference and the "Corrected definitions:" /
"Corrected scenario per spec Section X.Y:" annotations from the Dart
source. This is FR-024 doc-level — the spec-traceability MUST survive
the port. The `[Trait("Group", "Section X.Y: ...")]` attribute on each
test class duplicates the section number at the class level so the xUnit
reporter surfaces it without parsing doc-comments.

### Out-of-scope but recorded

- Project-system wiring (`<ProjectReference>` from the test .csproj to
  the runtime .csproj) is langpair-level; recorded so codegen knows the
  `using` alone is insufficient without the project reference.
- The exact `<RootNs>` placeholder is langpair-level and pinned at the
  workspace level, not per file.
- `Heap.AllocateVariable` / `Heap.BindVariable` / `Heap.DerefAddr` C#
  signatures live in the SUT convspec `lib/runtime/heap_fcp.dart.md`; THIS
  spec depends on those signatures but does not redefine them.
- The `MessageDeliveryCallback` delegate type is pinned by
  `lib/multiagent/mad_context.dart.md`; THIS spec records the call-site
  shape of the field assignment, not the delegate declaration.

### KB cache hits (no re-research)

All of the following pinned rf-ids were KB cache hits — re-research was
NOT performed (FR-024 reproducibility-offline rule):
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
`rf-dart-list-literal-to-csharp-list-initializer`,
`rf-dart-expect-isA-to-xunit-assert-istype`,
`rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`,
`rf-dart-expect-equals-to-xunit-assertequal`,
`rf-dart-expect-istrue-to-xunit-asserttrue`.

### Newly recorded rf-ids (defined by this file's first-use rows)

- `rf-dart-statement-body-lambda-to-csharp-statement-body-lambda` —
  statement-body lambda assigned to a delegate-typed field. (See
  the `dart.expression.statement_bodied_lambda_assigned_to_delegate_field`
  construct row.)
- `rf-dart-instance-method-call-to-csharp-pascalcase-call` —
  catch-all rf for `receiver.camelCaseMethod(...)` →
  `receiver.PascalCaseMethod(...)` with no further nuance.
- `rf-dart-list-indexer-to-csharp-list-indexer` —
  identity translation for `expr[i]` subscript on `List<T>`.
- `rf-dart-identifier-spec-notation-in-comments-preserved` —
  comment-only spec notation (`_w(p,1)` / `X?` / `:=`) preserved
  verbatim inside `///` doc-comments.

### No escalations

This file's constructs all resolve via the decision-order in
`convspec_idiom_schema.md`: every construct row records EITHER a pinned
idiom_id (KB cache hit) OR a research_finding_id with authoritative
Dart+.NET citations (the four new rf-ids above). No `idiom_vs_research`
conflicts; no `idiom_vs_idiom` conflicts; no undecidable points.
`open_escalation_count = 0` ⇒ file is `specced`, NOT escalated.
