# Conversion Spec — test/multiagent/global_send_test.dart

> Conversion-spec artifact for test/multiagent/global_send_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/global_send_test.dart
source_sha256: c998b41351407035919314db767e3b490b4b49953c66d2e0b0c06b56a306a1f6
target_code_unit: test/multiagent/GlobalSendTest.cs
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
      test/multiagent/localize_test.dart.md). THIS file MUST reuse that
      idiom verbatim (FR-012 / SC-007) — no re-research. The .NET test
      project (.csproj — out of this single-file artifact's scope) provides
      `xunit` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk`
      NuGet references. Codegen projects to a single namespace mirroring
      the Dart `test/multiagent` directory (e.g. `<RootNs>.Test.Multiagent`).
      No `using System;` or `using System.Collections.Generic;` strictly
      required at file scope for this file: no `IDisposable`, no
      `Exception` typed asserts, and `List<TermVar>` is not materialised
      at the call site (the test bodies pass either `[]`-empty-list or a
      single-element list through the `extractVariables` callback, see
      dart.expression.lambda_returning_list_literal below — codegen emits
      `new List<TermVar>()` / `new List<TermVar> { ... }` inline, which
      requires `using System.Collections.Generic;`). So add
      `using System.Collections.Generic;` at file scope as well.
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice nuance (load-bearing, explicitly addressed): xUnit
      pinned project-wide; NUnit / MSTest recorded as alternatives in the
      research-finding row but NOT used here. Module/namespace nuance:
      Dart's `package:test` exposes top-level functions (`group`, `test`,
      `expect`, `isTrue`, `isNotNull`, `isNull`) re-exported via the one
      import; xUnit has NO top-level test functions — tests are public
      instance methods on a public class discovered via `[Fact]`
      reflection. No async / Future / Stream / isolate surface in this
      file. This file introduces no new framework-level surface beyond
      what the six sibling test specs already pin.
  - construct_key: dart.package_test.import_sut_relative_package
    source_form: >-
      "import 'package:glp_runtime/multiagent/global_send.dart';
       import 'package:glp_runtime/multiagent/global_writers_table.dart';
       import 'package:glp_runtime/multiagent/mad_helpers.dart';"
    target_decision: >-
      All three imports are SUT (system-under-test) references — Dart
      `package:glp_runtime/...` URIs that resolve to the converted C#
      namespace for the same source units. Replace each with a C# `using`
      directive that names the namespace the converted `global_send.dart`
      / `global_writers_table.dart` / `mad_helpers.dart` will emit into
      (e.g. `using <RootNs>.Multiagent;` — all three SUT files emit into
      the SAME sub-namespace under the multiagent directory, so ONE
      `using` covers all three). The exact namespace string is determined
      by each SUT file's own conversion-spec (siblings
      `.codeconv/conversion-specs/lib/multiagent/global_send.dart.md`,
      `.../lib/multiagent/global_writers_table.dart.md`, and
      `.../lib/multiagent/mad_helpers.dart.md`, produced separately);
      this test-file spec records the DEPENDENCY relationship. Codegen
      MUST emit `using`s that resolve the symbols this test references:
      `GlobalSendRegistry`, `GlobalSendGoal` (constructor +
      `register`/`registerSpawns`/`hasGoalFor`/`getGoalFor`/
      `onWriterBound`/`pendingCount` instance members),
      `GlobalSendFiredResult` (the result type with `value`/`globalName`/
      `destination`/`newGoals` members), `GlobalSendSpawn` (the spawn
      constructor with `readerAddr`/`globalName`/`destAgent` named
      parameters — see SUT spec for shape),
      `GlobalWritersTable`, `GlobalName`,
      `GlobalName.writer`/`GlobalName.reader` (named constructors —
      mapped per dart.class.named_constructor_factory below), `TermVar`,
      `TermVar.reader` (named constructor — see SUT spec). Per-file
      working-directory convention from feature 016/017 (`<file>__/`)
      means the SUT and test live in sibling working dirs; the `using`
      resolves through the test .csproj's project-reference to the
      runtime .csproj (langpair-level concern, OUT OF SCOPE here —
      recorded for codegen cross-file wiring).
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed): a `package:`
      import that resolves to an in-repo Dart library (NOT to a pub.dev
      third-party package) maps to a C# `using <Namespace>;` that
      targets the OUTPUT namespace of the converted Dart library — NOT
      a separate NuGet reference. Distinguish by inspecting the
      `package:` URI prefix against the host repo's `pubspec.yaml`
      `name:` (here, `glp_runtime`). Project-file wiring
      (`<ProjectReference>` from the test .csproj to the runtime .csproj)
      is langpair/project-skeleton level, recorded so codegen knows the
      `using` alone is insufficient without the project reference.
      Three-imports-collapse nuance: all three SUT files target the SAME
      C# namespace (`<RootNs>.Multiagent`); codegen emits ONE `using`
      not three. No top-level `globalize(...)` is called from THIS file
      (contrast with globalize_test.dart, which calls the top-level
      function directly) — every call goes through the `GlobalSendRegistry`
      instance API; therefore NO `using static <RootNs>.Multiagent.MadHelpers;`
      is needed. The `mad_helpers.dart` import is needed solely for
      `GlobalName` / `TermVar` type references at the call site
      (constructor calls and assertion value comparisons).
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('GlobalSendGoal', () { ... }); group('GlobalSendRegistry', () { ... }); }"
    target_decision: >-
      Drop Dart `void main()` entirely — xUnit discovers `[Fact]` methods
      on `public` classes by reflection; there is no per-file entrypoint
      to emit. The TWO sibling `group(...)` calls inside `main` become
      TWO sibling test classes at the file's namespace scope (see next
      construct).
    idiom_id: rf-dart-test-main-to-xunit-class-with-facts
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Lifecycle nuance (explicitly addressed): Dart `main` runs once per
      test-file process and registers tests; xUnit has no per-file hook —
      only per-class (constructor + `IDisposable.Dispose`) and
      per-collection fixtures. THIS file's `main` body is exactly two
      sibling `group()` calls with no other statements, so omitting
      `main` is lossless. No `setUp` / `setUpAll` / `tearDown` /
      `tearDownAll` anywhere in this file, so no constructor or
      `IDisposable.Dispose` content is needed — identical to
      globalize_test.dart and global_writers_table_test.dart.
      Two-sibling-groups nuance (NEW relative to globalize_test.dart,
      which had ONE outer group): the two sibling groups
      ('GlobalSendGoal' and 'GlobalSendRegistry') become TWO sibling
      public classes (`GlobalSendGoalTests` and `GlobalSendRegistryTests`)
      under the same namespace — NOT a nested-class layout. xUnit's
      `[Trait]` on each class preserves the group label for reporter
      parity.
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('GlobalSendGoal', () { test(...); test(...); test(...); test(...); });
       group('GlobalSendRegistry', () { test(...); test(...); });"
    target_decision: >-
      Each Dart `group(label, body)` maps to a separate `public class
      <Label>Tests`. Specifically:
      `group('GlobalSendGoal', ...)` -> `public class GlobalSendGoalTests`
      containing 4 `[Fact]` methods;
      `group('GlobalSendRegistry', ...)` -> `public class
      GlobalSendRegistryTests` containing 2 `[Fact]` methods.
      Both class names encode the group label in PascalCase with the
      conventional `Tests` suffix. The original label MAY be preserved
      via `[Trait("Group", "GlobalSendGoal")]` / `[Trait("Group",
      "GlobalSendRegistry")]` on each class for reporter parity. No
      nested `group(...)`, no `setUp`/`tearDown` inside either group —
      each test constructs its own `GlobalSendRegistry` instance and
      its own `GlobalWritersTable` instance locally, so xUnit's
      per-test fresh-instance lifecycle ("xUnit.net creates a new
      instance of the test class for every test that is run") maps
      cleanly with NO shared state and NO constructor-side fixture
      needed.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance: both group labels (`'GlobalSendGoal'`,
      `'GlobalSendRegistry'`) are already valid C# identifiers; mangle
      is trivial (append `Tests`). Sibling-groups-NOT-nested-groups
      nuance: contrast with boot_loader_test.dart which has NESTED
      groups mapped to a single class + `[Trait]`-tagged methods; here
      the groups are SIBLING (both inside `main`, neither inside the
      other), so the documented mapping is two SEPARATE classes — same
      shape as the standard "one Dart file -> N xUnit classes" carve-out
      already pinned by the framework idiom. Per-test labels in this
      file (`'fires when reader becomes known'`, `'produces correct
      message'`, `'nested variables spawn additional goals'`, `'goal
      removed after firing'`, `'registerSpawns converts GlobalSendSpawn
      to goals'`, `'onWriterBound returns null when no goal registered'`)
      contain spaces and one underscore-bearing identifier
      (`global_send`); the per-test method-name mangling strips
      non-identifier characters and PascalCases — see next construct.
  - construct_key: dart.package_test.test_call_executable
    source_form: >-
      "test('<label>', () { /* Given/When/Then with executable
      arrange-act-assert */ });" — applied to all 6 test cases in this
      file (none use `skip:`).
    target_decision: >-
      Each Dart `test(label, body)` (no `skip` argument) becomes a
      `public void` method on the enclosing class, decorated with
      `[Fact(DisplayName = "<original label>")]`. Method name = label
      PascalCased with non-identifier chars stripped:
      `'fires when reader becomes known'` ->
      `FiresWhenReaderBecomesKnown`;
      `'produces correct message'` -> `ProducesCorrectMessage`;
      `'nested variables spawn additional goals'` ->
      `NestedVariablesSpawnAdditionalGoals`;
      `'goal removed after firing'` -> `GoalRemovedAfterFiring`;
      `'registerSpawns converts GlobalSendSpawn to goals'` ->
      `RegisterSpawnsConvertsGlobalSendSpawnToGoals`;
      `'onWriterBound returns null when no goal registered'` ->
      `OnWriterBoundReturnsNullWhenNoGoalRegistered`.
      Method body translates the Dart arrange-act-assert verbatim,
      with `expect(actual, matcher)` calls routed to xUnit `Assert.*`
      per the matcher-routing idioms below. The Given/When/Then
      comments (which carry "Spec Section 4" / "Spec Section 12"
      references citing the madGLP-spec.md `global_send` predicate)
      MUST be carried into the target as a `/// <summary>` doc-comment
      block per method so spec traceability survives the conversion
      (FR-024 doc-level).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Method-body translation nuance (explicitly addressed): every
      `test` callback in this file is synchronous (no `async`/`Future`/
      `await`); target method returns `void` (xUnit also supports
      `async Task` for async tests — not applicable here). Closure-
      capture nuance: no `setUp` variables — every `final registry =
      ...`, `final table = ...`, `final goal = ...`, `final result =
      ...`, `final spawns = [...]`, `final goal1/goal2 = ...` is local
      to the test body, mapping 1-to-1 to local `var <name> = ...` in
      the C# method (see dart.expression.final_local_variable_with_initializer).
      Skip-semantics nuance (NOT firing here): no `skip:` argument
      anywhere, so NO `Skip=` property on `[Fact]` — contrast with
      mad_error_handling_test.dart.
  - construct_key: dart.expression.final_local_variable_with_initializer
    source_form: >-
      "final registry = GlobalSendRegistry('p');
       final table = GlobalWritersTable('p');
       final goal = GlobalSendGoal(readerAddr: 100, globalName: ..., destination: 'q');
       final result = registry.onWriterBound(writerAddr: 100, value: 42, table: table, extractVariables: (_) => []);
       final spawns = [ GlobalSendSpawn(...), GlobalSendSpawn(...) ];
       final goal1 = registry.getGoalFor(100)!;
       final goal2 = registry.getGoalFor(200)!;"
    target_decision: >-
      Translate `final <name> = <expr>;` to `var <name> = <expr>;` in C#
      where the initializer is a constructor invocation, a method call,
      a list literal, or a lambda. Specifically:
      `final registry = GlobalSendRegistry('p')` ->
      `var registry = new GlobalSendRegistry("p");` (note the mandatory
      C# `new` keyword — Dart's optional-`new` constructor call requires
      C#'s explicit `new`; Dart single-quote string `'p'` -> C# double-
      quote `"p"`);
      `final table = GlobalWritersTable('p')` ->
      `var table = new GlobalWritersTable("p");`;
      `final goal = GlobalSendGoal(readerAddr: 100, ...)` ->
      `var goal = new GlobalSendGoal(readerAddr: 100, globalName:
      GlobalName.Writer("p", 0), destination: "q");`
      (named arguments preserved verbatim per construct
      dart.expression.named_argument_in_invocation);
      `final result = registry.onWriterBound(writerAddr: 100, value:
      42, table: table, extractVariables: (_) => [])` ->
      `var result = registry.OnWriterBound(writerAddr: 100, value: 42,
      table: table, extractVariables: _ => new List<TermVar>());`
      (the OnWriterBound positional+named parameter list shape is
      pinned by the SUT spec lib/multiagent/global_send.dart.md; see
      dart.expression.lambda_returning_list_literal below for the
      lambda translation);
      `final spawns = [ GlobalSendSpawn(...), ... ]` ->
      `var spawns = new List<GlobalSendSpawn> { new GlobalSendSpawn(...),
      new GlobalSendSpawn(...) };` (see
      dart.expression.list_literal_typed below);
      `final goal1 = registry.getGoalFor(100)!` ->
      `var goal1 = registry.GetGoalFor(100)!;` (the SUT spec records
      `GetGoalFor` returns `GlobalSendGoal?`; the `!` null-forgiving
      operator is preserved — see dart.expression.null_assertion_bang_operator).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability-semantics nuance (explicitly addressed): Dart
      `final <local>` prevents REBINDING the local after init but does
      NOT prevent mutation of the referenced object's state — exactly
      the same semantics as C# `var`. C# 7+ has no `readonly` modifier
      for locals; conversion accepts this minor semantic loss (sibling
      global_writers_table_test.dart.md and globalize_test.dart.md
      record the same trade-off). Constructor-syntax nuance: Dart allows
      `Foo(...)` without `new`; C# requires `new Foo(...)`.
      String-literal nuance: Dart `'p'` / `'q'` / `'hello'` / `'done'` /
      `'r'` / `'foo(Y?)'` / `'ignored'` are all single-quoted strings;
      C# uses ONLY `"..."` for `string`. Codegen MUST emit
      `new GlobalSendRegistry("p")` — single-quote literals would be
      `char` in C# and select non-existent `char`-arg constructors.
      Null-forgiving on dictionary lookup nuance: `registry.getGoalFor(K)!`
      is the Dart pattern for "I just asserted this key is present;
      please don't make me handle the null case"; the C# equivalent
      `registry.GetGoalFor(K)!` is identical IN SHAPE but DIFFERENT
      in semantics (Dart `!` throws at runtime if null; C# `!` is
      compile-time-only) — addressed under
      dart.expression.null_assertion_bang_operator.
  - construct_key: dart.class.named_constructor_factory
    source_form: >-
      "GlobalName.writer('p', 0)
       GlobalName.reader('r', 5)
       TermVar.reader(401, writerAddr: 400)"
    target_decision: >-
      Dart's NAMED CONSTRUCTORS (`ClassName.namedCtor(...)`) — used
      here for `GlobalName.writer`/`GlobalName.reader` (both heavily)
      and `TermVar.reader` (once, line 96) — have NO direct C#
      equivalent. The pinned mapping (already recorded by
      globalize_test.dart.md as
      `rf-dart-named-constructor-to-csharp-static-factory`, reused
      verbatim) is: Dart `Foo.bar(args)` -> C# `Foo.Bar(args)` STATIC
      FACTORY METHOD on the converted class. So
      `GlobalName.writer('p', 0)` -> `GlobalName.Writer("p", 0)`;
      `GlobalName.reader('r', 5)` -> `GlobalName.Reader("r", 5)`;
      `TermVar.reader(401, writerAddr: 400)` ->
      `TermVar.Reader(401, writerAddr: 400)` (NB: also a named argument
      — see next construct). The factory method name is the
      named-constructor identifier PascalCased per the cross-cutting
      C# member-naming idiom. The SUT spec
      (`.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md`)
      is the source of truth for the exact static-factory signatures
      emitted; THIS test spec records the call-site shape and reuses
      the pinned mapping.
    idiom_id: rf-dart-named-constructor-to-csharp-static-factory
    research_finding_id: rf-dart-named-constructor-to-csharp-static-factory
    nuance: >-
      Constructor-semantics nuance (explicitly addressed, NOT glossed):
      Dart named constructors are still CONSTRUCTORS — they go through
      the same allocation+initialization pipeline as the primary
      constructor and can chain via `: super(...)` or `: this(...)`.
      C# static factories are METHOD CALLS that internally
      `return new Foo(...)`. Both `GlobalName` and `TermVar` are
      sealed data classes in Dart with no subclasses, so the
      sub-classing semantic gap is benign. The ALTERNATIVE C# encoding
      — multiple constructor overloads disambiguated by parameter type
      — was rejected for `TermVar` because `TermVar.writer(int,
      {int readerAddr})` and `TermVar.reader(int, {int writerAddr})`
      differ ONLY by the named-parameter LABEL, not the type signature,
      so two `(int, int)` constructors would conflict. Static factories
      on the same type sidestep the ambiguity. Pinned mapping:
      named-ctor -> PascalCase static method on the same class
      returning `new ClassName(...)`.
  - construct_key: dart.class.positional_param_primary_constructor
    source_form: >-
      "GlobalSendRegistry('p')
       GlobalWritersTable('p')"
    target_decision: >-
      Dart's POSITIONAL-PARAMETER PRIMARY CONSTRUCTOR (no named args,
      no factory) — `GlobalSendRegistry(this.agentId)` in
      `lib/multiagent/global_send.dart` and `GlobalWritersTable(this.agentId)`
      in the sibling SUT — maps to a C# single-positional-parameter
      `public` constructor. Translate `GlobalSendRegistry('p')` ->
      `new GlobalSendRegistry("p")` and `GlobalWritersTable('p')` ->
      `new GlobalWritersTable("p")`. NO named arguments at the call
      site (the Dart side uses positional invocation). The SUT spec
      (`.codeconv/conversion-specs/lib/multiagent/global_send.dart.md`
      and `.../global_writers_table.dart.md`) pins the constructor
      signature emitted on the C# side (`public
      GlobalSendRegistry(string agentId)`); THIS test spec records the
      call-site shape.
    idiom_id: null
    research_finding_id: rf-dart-positional-primary-ctor-to-csharp-positional-ctor
    nuance: >-
      Initializer-list nuance (explicitly addressed): Dart's
      `Foo(this.field)` shorthand assigns the parameter directly to the
      field as part of the initializer list. C# has no syntactic
      equivalent; the constructor body MUST assign the parameter to the
      backing property (`AgentId = agentId;`). Per-file working-dir
      convention places the constructor body in the SUT class file (one
      line: `AgentId = agentId;`), not here — THIS test spec only
      depends on the call-site signature `new
      GlobalSendRegistry(string)`. Distinguish from the
      `GlobalSendGoal` and `GlobalSendSpawn` ctors which use
      named-required parameters (next construct + dart.class.named_constructor_factory).
  - construct_key: dart.class.named_required_parameter_constructor_invocation
    source_form: >-
      "GlobalSendGoal(readerAddr: 100, globalName: GlobalName.writer('p', 0), destination: 'q')
       GlobalSendGoal(readerAddr: 200, globalName: ..., destination: 'q')
       GlobalSendGoal(readerAddr: 300, globalName: ..., destination: 'q')
       GlobalSendGoal(readerAddr: 500, globalName: ..., destination: 'q')
       GlobalSendSpawn(readerAddr: 100, globalName: GlobalName.writer('p', 0), destAgent: 'q')
       GlobalSendSpawn(readerAddr: 200, globalName: GlobalName.reader('r', 5), destAgent: 'r')"
    target_decision: >-
      Dart's PRIMARY NAMED-REQUIRED constructor (`GlobalSendGoal({required
      this.readerAddr, required this.globalName, required this.destination})`
      and the analogous `GlobalSendSpawn` shape recorded in the SUT
      spec) at the CALL SITE uses `ClassName(name: value, ...)`. This
      maps to a C# single-`public`-constructor invocation
      `new ClassName(name: value, ...)` — C# named arguments at the
      call site, with the underlying constructor declared as ordinary
      positional parameters in the SUT (no `?` on parameter names; the
      C# convention for parameter names is camelCase, identical to the
      Dart spelling — see dart.expression.named_argument_in_invocation).
      So `GlobalSendGoal(readerAddr: 100, globalName: GlobalName.writer
      ('p', 0), destination: 'q')` -> `new GlobalSendGoal(readerAddr:
      100, globalName: GlobalName.Writer("p", 0), destination: "q")`;
      `GlobalSendSpawn(readerAddr: 100, globalName: ..., destAgent: 'q')`
      -> `new GlobalSendSpawn(readerAddr: 100, globalName:
      GlobalName.Writer("p", 0), destAgent: "q")`. Note the SUT
      preserves the `destAgent` vs `destination` vocabulary split
      verbatim (per the SUT spec for global_send.dart — `GlobalSendGoal`
      has `Destination`, `GlobalSendSpawn` has `DestAgent`; the
      FromSpawn static factory renames at the boundary).
    idiom_id: null
    research_finding_id: rf-dart-named-argument-to-csharp-named-argument
    nuance: >-
      Required-vs-optional nuance (explicitly addressed, IDENTICAL to
      globalize_test.dart.md): Dart `{required Type name}` is
      COMPILE-TIME-MANDATORY at every call site; C# named arguments are
      by default OPTIONAL (the caller may pass positionally OR by name,
      and may omit if the parameter has a default value). To preserve
      the "must be supplied" guarantee that Dart `required` provides,
      the C# parameter MUST NOT have a default value — leaving the
      constructor parameter as `int readerAddr` (no `= default`) forces
      the caller to supply a value, which is the same end-state
      (compile-time required). Order-independence nuance: Dart named
      arguments may appear in any order at the call site; C# named
      arguments may also appear in any order. This file preserves the
      Dart call-site order verbatim. Vocabulary-translation nuance
      (LOAD-BEARING for this file): the `destAgent` (on `GlobalSendSpawn`)
      vs `destination` (on `GlobalSendGoal`) field-name split is a
      deliberate vocabulary translation in the Dart source — a spawn
      carries `destAgent`; a goal exposes `destination` — both with
      identical runtime value at the `GlobalSendGoal.fromSpawn` boundary.
      The SUT spec records the rename on the C# side; this test spec
      relies on the rename being preserved verbatim (the
      RegisterSpawnsConvertsGlobalSendSpawnToGoals test cross-checks
      both vocabularies).
  - construct_key: dart.expression.named_argument_in_invocation
    source_form: >-
      "TermVar.reader(401, writerAddr: 400)
       registry.onWriterBound(writerAddr: 100, value: 42, table: table, extractVariables: (_) => [])
       registry.onWriterBound(writerAddr: 200, value: 'hello', ...)
       registry.onWriterBound(writerAddr: 300, value: 'foo(Y?)', extractVariables: (_) => [TermVar.reader(401, writerAddr: 400)])
       registry.onWriterBound(writerAddr: 500, value: 'done', ...)
       registry.onWriterBound(writerAddr: 999, value: 'ignored', ...)"
    target_decision: >-
      Dart NAMED ARGUMENTS (`name: value` at call site, with the
      callee's parameter declared either `{required Type name}` or
      `{Type name = default}`) map to C# NAMED ARGUMENTS (`name: value`
      at call site, with the callee's parameter declared as an
      ordinary parameter — optionally with a default value). The C#
      naming convention requires the parameter name to be camelCase
      (e.g. `readerAddr`, `writerAddr`, `value`, `table`,
      `extractVariables`) — IDENTICAL to the Dart spelling. So
      `TermVar.reader(401, writerAddr: 400)` ->
      `TermVar.Reader(401, writerAddr: 400)`;
      `registry.onWriterBound(writerAddr: 100, value: 42, table: table,
      extractVariables: (_) => [])` -> `registry.OnWriterBound(
      writerAddr: 100, value: 42, table: table, extractVariables:
      _ => new List<TermVar>())`. The C# parameter is declared on the
      target method (see SUT spec) — `public GlobalSendFiredResult?
      OnWriterBound(int writerAddr, object? value, GlobalWritersTable
      table, Func<object?, IReadOnlyList<TermVar>> extractVariables)`
      — positional in the declaration; named at the call site.
    idiom_id: rf-dart-named-argument-to-csharp-named-argument
    research_finding_id: rf-dart-named-argument-to-csharp-named-argument
    nuance: >-
      Required-vs-optional nuance: see previous construct. Order-
      independence nuance: see previous construct. Naming-convention
      nuance: Dart parameter names are camelCase (e.g. `readerAddr`,
      `writerAddr`); C# convention for PARAMETER names is also
      camelCase (NOT PascalCase — PascalCase is for public members
      like methods / properties / types). So `readerAddr` /
      `writerAddr` / `extractVariables` carry over VERBATIM; this is
      a non-obvious carve-out from the general Dart-member-name-to-C#-
      PascalCase rule that the test-file specs have pinned for getters
      and methods. Re-confirmed here for the `extractVariables`
      callback (a `Func<...>` parameter — see next construct).
  - construct_key: dart.expression.lambda_returning_list_literal
    source_form: >-
      "extractVariables: (_) => []
       extractVariables: (_) => [TermVar.reader(401, writerAddr: 400)]"
    target_decision: >-
      Dart arrow-style lambda `(_) => <expr>` (one positional argument
      bound to `_` — the conventional Dart "I don't care" name, NOT
      Dart 3's pattern-wildcard; this is just an ordinary identifier
      that happens to be a single underscore) maps to a C# lambda
      `_ => <expr>`. C# parses `_` as an ordinary identifier in lambda
      parameter position (the C# discard `_` exists only in pattern /
      deconstruction contexts; in a lambda parameter slot the
      identifier is bound as a local). Specifically:
      `(_) => []` (empty `List<TermVar>` literal — empty Dart list
      literals are inferred from the target type, here
      `List<TermVar> Function(Object?)`) ->
      `_ => new List<TermVar>()`; OR equivalently
      `_ => new List<TermVar> { }`. Codegen prefers the constructor-call
      form `new List<TermVar>()` because the empty collection-initializer
      `new List<TermVar> { }` is syntactically valid but unidiomatic.
      `(_) => [TermVar.reader(401, writerAddr: 400)]` ->
      `_ => new List<TermVar> { TermVar.Reader(401, writerAddr: 400) }`.
      The lambda is assigned to the `extractVariables` parameter
      declared as `Func<object?, IReadOnlyList<TermVar>>` (per SUT
      spec) — `List<T>` implements `IReadOnlyList<T>`, so the implicit
      conversion is valid.
    idiom_id: null
    research_finding_id: rf-dart-arrow-lambda-to-csharp-lambda
    nuance: >-
      Lambda-syntax nuance (explicitly addressed, FIRST RECORDED here
      among the test-file specs): Dart `(arg) => expr` is the arrow
      function form (synchronous, single-expression body); C# `arg =>
      expr` is the lambda expression form. The Dart parentheses around
      a single positional argument are OPTIONAL (`x => expr` is also
      valid), but the source uses `(_)` form — codegen preserves the
      bare-identifier form `_ => expr` (C# requires parentheses ONLY
      when the parameter has an explicit type annotation or when
      there are zero/multiple parameters). Underscore-identifier
      nuance: Dart `_` in a lambda parameter is just an ordinary
      identifier with no semantic significance (the analyzer may
      warn about unused parameters but the code is valid); C# `_` in
      a lambda parameter slot is ALSO just an identifier (the
      C# 9 discard `_` is a pattern context — NOT a lambda parameter).
      Codegen MAY emit `_ => ...` verbatim; analyzers won't object.
      Inferred-empty-list nuance: Dart `[]` with `List<TermVar>`
      context infers `List<TermVar>`; C# `new List<TermVar>()` is the
      explicit equivalent (NOT `new[] { }` which would fail to infer
      the element type). Capture-and-closure nuance: neither lambda
      captures any state from the enclosing test body; both are pure
      functions; closure semantics agree between Dart and C# at the
      use-site. Async/Future nuance: ABSENT — `(_) => ...` is
      synchronous; `extractVariables` parameter is `Func<object?,
      IReadOnlyList<TermVar>>`, NOT `Func<object?, Task<...>>`.
      Authoritative Dart side: dart.dev language tour `Functions /
      anonymous functions`
      (`https://dart.dev/language/functions#anonymous-functions`).
      Authoritative .NET side: Microsoft Learn `Lambda expressions`
      (`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/lambda-expressions`).
      Both sides authoritative.
  - construct_key: dart.expression.list_literal_typed
    source_form: >-
      "final spawns = [ GlobalSendSpawn(readerAddr: 100, globalName:
      GlobalName.writer('p', 0), destAgent: 'q'), GlobalSendSpawn(
      readerAddr: 200, globalName: GlobalName.reader('r', 5),
      destAgent: 'r') ];"
    target_decision: >-
      Dart list literal `[a, b]` whose static element type is inferred
      (here `List<GlobalSendSpawn>` because every element is a
      `GlobalSendSpawn` ctor call) maps to C# `new List<GlobalSendSpawn>
      { a, b }` (collection-initializer syntax on
      `System.Collections.Generic.List<T>`). The `using
      System.Collections.Generic;` at file scope (see
      dart.package_test.import_directive nuance) makes
      `List<GlobalSendSpawn>` resolvable. Element calls are themselves
      converted per
      dart.class.named_required_parameter_constructor_invocation above.
      The ALTERNATIVE `new[] { ... }` (a C# array literal of type
      `GlobalSendSpawn[]`) is REJECTED because `registerSpawns`'
      parameter is declared `IReadOnlyList<GlobalSendSpawn>` per the
      SUT spec (`lib/multiagent/global_send.dart.md`); a `List<T>`
      instance satisfies that interface naturally, and standardising
      on `new List<T> { ... }` keeps the call-site shape consistent
      with sibling test specs.
    idiom_id: rf-dart-list-literal-to-csharp-list-initializer
    research_finding_id: rf-dart-list-literal-to-csharp-list-initializer
    nuance: >-
      Collection-type nuance (explicitly addressed): Dart `List<T>`
      growable maps to C# `List<T>` growable — same runtime
      characteristic. Element-equality nuance: not asserted as a
      whole-list compare in this file (no `expect(spawns, equals([...]))`);
      only individual `registry.getGoalFor(K)` lookups are inspected
      (the spawns list is consumed by `registry.registerSpawns(spawns)`
      and never re-read directly), so the collection-equality idiom
      from boot_loader_test.dart.md
      (`rf-dart-list-equality-to-xunit-assertequal-collection`) is NOT
      exercised here. Length nuance: the list has 2 elements;
      `spawns.length` is NOT referenced — `expect(registry.pendingCount,
      2)` checks the registry's count after `registerSpawns`, not the
      input list's length.
  - construct_key: dart.expression.null_assertion_bang_operator
    source_form: >-
      "result!.value (line 40)
       result!.globalName.isWriter (line 69)
       result!.newGoals.length (line 102)
       registry.getGoalFor(100)! (line 169)
       registry.getGoalFor(200)! (line 173)"
    target_decision: >-
      Dart's null-assertion operator `expr!` (asserts non-null at
      runtime, throws `TypeError` if null) maps to C#'s null-forgiving
      operator `expr!` (compile-time annotation only — does NOT throw,
      just silences the NRT warning). The semantic difference is
      load-bearing and MUST be addressed: in C#, the runtime
      null-throw guarantee MUST come from an explicit
      `Assert.NotNull(result)` (or equivalent) on a prior line. In
      THIS file:
      `result!.value` (line 40) is preceded by `expect(result, isNotNull)`
      on line 39 (-> `Assert.NotNull(result)`), so `result!.Value` is
      compile-only-safe;
      `result!.globalName.isWriter` (line 69) is preceded by
      `expect(result, isNotNull)` on line 68 — same as above;
      `result!.newGoals.length` (line 102) is preceded by
      `expect(result, isNotNull)` on line 101 — same as above;
      `registry.getGoalFor(100)!` (line 169) and `registry.getGoalFor(
      200)!` (line 173) are NOT preceded by `Assert.NotNull` lines
      directly, but the immediately prior `Assert.Equal(registry.
      PendingCount, 2)` (-> `Assert.Equal(2, registry.PendingCount)`)
      establishes that both keys are present, and the goal lookups
      cannot return null. Codegen MUST audit each `!` translation:
      if the immediately preceding statement is NOT an `Assert.NotNull`
      of the SAME expression (lines 169 and 173 do not satisfy this
      strict form), codegen MUST insert an explicit `Assert.NotNull(
      registry.GetGoalFor(100));` line (or rewrite to `var goal1 =
      Assert.IsType<GlobalSendGoal>(registry.GetGoalFor(100));`) to
      preserve the Dart runtime-throw semantics. Translate
      `result!.value` -> `result!.Value`,
      `result!.globalName.isWriter` -> `result!.GlobalName.IsWriter`,
      `result!.newGoals.length` -> `result!.NewGoals.Count` (List.Count
      idiom),
      `registry.getGoalFor(100)!` -> `registry.GetGoalFor(100)!`
      (PascalCased method per member-naming idiom).
    idiom_id: rf-dart-bang-operator-to-csharp-null-forgiving
    research_finding_id: rf-dart-bang-operator-to-csharp-null-forgiving
    nuance: >-
      Runtime-vs-compile-time nuance (explicitly addressed, NOT
      glossed, IDENTICAL to globalize_test.dart.md and
      global_writers_table_test.dart.md): Dart `!` is a RUNTIME
      null-check that throws `TypeError`; C# `!` is a COMPILE-TIME
      NRT annotation. The semantic gap is closed in this file for the
      three `result!.*` usages because each is preceded by
      `expect(result, isNotNull)` on the immediately previous line.
      For the two `registry.getGoalFor(K)!` usages, the gap is closed
      indirectly — the `expect(registry.pendingCount, 2)` on the line
      before guarantees both keys are present, AND xUnit's
      `Assert.Equal` will throw if `PendingCount` is not 2, so by
      the time the lookups run, both calls must succeed. Strict
      codegen MAY add an explicit `Assert.NotNull(registry.GetGoalFor(K))`
      between the count assertion and the lookup, OR use the `goal1 =
      Assert.IsType<GlobalSendGoal>(...)` form which both asserts
      non-null and narrows the static type. CONVERSION INVARIANT
      carried over from sibling test specs verbatim.
  - construct_key: dart.package_test.expect_isTrue_matcher
    source_form: >-
      "expect(registry.hasGoalFor(100), isTrue);  // (lines 28, 122, 164, 165)
       expect(result!.globalName.isWriter, isTrue);  // (line 69)
       expect(result.newGoals[0].globalName.isReader, isTrue);  // (line 104)
       expect(goal1.globalName.isWriter, isTrue);  // (line 170)
       expect(goal2.globalName.isReader, isTrue);  // (line 174)"
    target_decision: >-
      Dart `expect(<bool>, isTrue)` (using the `package:matcher`
      `isTrue` constant) maps to xUnit `Assert.True(<bool>);` — strict
      `bool`-typed assertion. Better diagnostic message than
      `Assert.Equal(true, ...)`. Translate
      `expect(registry.hasGoalFor(100), isTrue)` ->
      `Assert.True(registry.HasGoalFor(100));`;
      `expect(result!.globalName.isWriter, isTrue)` ->
      `Assert.True(result!.GlobalName.IsWriter);` (NB: `isWriter` ->
      `IsWriter` property per cross-cutting
      `rf-dart-getter-to-csharp-property` idiom);
      `expect(result.newGoals[0].globalName.isReader, isTrue)` ->
      `Assert.True(result.NewGoals[0].GlobalName.IsReader);`;
      `expect(goal1.globalName.isWriter, isTrue)` ->
      `Assert.True(goal1.GlobalName.IsWriter);`;
      `expect(goal2.globalName.isReader, isTrue)` ->
      `Assert.True(goal2.GlobalName.IsReader);`.
    idiom_id: rf-dart-expect-isTrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Boolean-semantics nuance (explicitly addressed): Dart `isTrue`
      matches only the boolean value `true` — NOT truthiness (Dart has
      no truthiness coercion). xUnit `Assert.True(bool)` is identically
      strict (requires actual `bool true`). Getter-to-property nuance:
      `isWriter` / `isReader` are `bool get` getters on `GlobalName` in
      the SUT (defined as `bool get isWriter => type ==
      GlobalNameType.writer;`); C# equivalent is an expression-bodied
      property (`public bool IsWriter => Type == GlobalNameType.Writer;`).
      The SUT spec pins the property-vs-method choice; this test spec
      relies on the property form (zero-arg, no parentheses at call
      site) because the Dart source accesses it without parens.
  - construct_key: dart.package_test.expect_isFalse_matcher
    source_form: "expect(registry.hasGoalFor(500), isFalse);  // (line 134)"
    target_decision: >-
      Dart `expect(<bool>, isFalse)` maps to xUnit
      `Assert.False(<bool>);` — strict `bool`-typed assertion. Better
      diagnostic message than `Assert.Equal(false, ...)`. Translate
      `expect(registry.hasGoalFor(500), isFalse)` ->
      `Assert.False(registry.HasGoalFor(500));`. Used ONCE in this
      file.
    idiom_id: rf-dart-expect-isFalse-to-xunit-assert-false
    research_finding_id: rf-dart-expect-isFalse-to-xunit-assert-false
    nuance: >-
      Symmetric-to-isTrue nuance (explicitly addressed): same
      strict-bool semantics; `Assert.False(b)` mirror of
      `Assert.True(b)`. Authoritative on both sides: pub.dev
      `package:matcher` `isFalse`
      (`https://pub.dev/documentation/matcher/latest/matcher/isFalse-constant.html`);
      xunit.net `Assert.False` API. First first-recorded use in the
      test-file batch (sibling specs use `isTrue` but not `isFalse`).
  - construct_key: dart.package_test.expect_isNotNull_matcher
    source_form: >-
      "expect(result, isNotNull);  // (lines 39, 68, 101)"
    target_decision: >-
      `expect(x, isNotNull)` -> `Assert.NotNull(x);` per the
      matcher-routing table pinned by smoke_test.dart and
      global_writers_table_test.dart. Used 3x in this file (always
      before a `result!.*` dereference — see
      dart.expression.null_assertion_bang_operator). xUnit
      `Assert.NotNull(object?)` throws `NotNullException` on null,
      otherwise passes — strict null-vs-not-null semantics identical
      to Dart `isNotNull`.
    idiom_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    research_finding_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    nuance: >-
      Null-safety nuance (explicitly addressed): Dart `isNotNull`
      matches any non-null value (including `false`, `0`, empty
      string — Dart has no truthiness coercion); xUnit
      `Assert.NotNull(object?)` is identically strict. NRT-flow
      nuance: after `Assert.NotNull(result)`, the C# flow-analyzer
      narrows `result` to non-nullable ONLY if xUnit's `Assert.NotNull`
      is annotated with `[NotNull]` (xUnit >= 2.5 does this). For
      older xUnit, the converted code must use the null-forgiving
      operator `result!.Value` at the subsequent dereference — which
      is what the Dart source already does (lines 39 -> 40, 68 -> 69,
      101 -> 102), so the conversion mirrors the source bang verbatim.
  - construct_key: dart.package_test.expect_isNull_matcher
    source_form: "expect(result, isNull);  // (line 190)"
    target_decision: >-
      Dart `expect(<value>, isNull)` (using `package:matcher` `isNull`
      constant) maps to xUnit `Assert.Null(<value>);`. Used ONCE in
      this file. xUnit `Assert.Null(object?)` throws `NotNullException`
      (sic — xUnit uses one exception for both null/not-null
      assertions; the message differentiates) if the argument is NOT
      null, otherwise passes. Translate `expect(result, isNull)` ->
      `Assert.Null(result);`.
    idiom_id: rf-dart-expect-isNull-to-xunit-assert-null
    research_finding_id: rf-dart-expect-isNull-to-xunit-assert-null
    nuance: >-
      Null-semantics nuance (explicitly addressed): Dart `isNull` is
      strict (matches only `null`, never `false`/`0`/empty); xUnit
      `Assert.Null(object?)` is identically strict. Already pinned by
      global_writers_table_test.dart.md
      (`rf-dart-expect-isNull-to-xunit-assert-null`); reused verbatim.
      Used here in the final test case (`onWriterBound returns null
      when no goal registered`) — exercising the
      `OnWriterBound` -> `null` early-return path that maps to
      `if (!_goals.Remove(writerAddr, out var goal)) return null;` in
      the SUT spec.
  - construct_key: dart.package_test.expect_equals_implicit_matcher
    source_form: >-
      "expect(result!.value, 42);  // (line 40)
       expect(result.globalName, GlobalName.writer('p', 0));  // (line 41)
       expect(result.destination, 'q');  // (line 42)
       expect(result!.globalName.agent, 'p');  // (line 70)
       expect(result.globalName.index, 0);  // (line 71)
       expect(result.destination, 'q');  // (line 72)
       expect(result.value, 'hello');  // (line 73)
       expect(result!.newGoals.length, 1);  // (line 102)
       expect(result.newGoals[0].readerAddr, 400);  // (line 103)
       expect(result.newGoals[0].destination, 'q');  // (line 105)
       expect(registry.pendingCount, 1);  // (line 123)
       expect(registry.pendingCount, 0);  // (line 135)
       expect(registry.pendingCount, 2);  // (line 166)
       expect(goal1.destination, 'q');  // (line 171)
       expect(goal2.destination, 'r');  // (line 175)"
    target_decision: >-
      Dart `expect(actual, value)` where the second argument is a
      non-matcher value (bare value rather than a `Matcher`) is sugar
      for `expect(actual, equals(value))` — the
      `package:test`/`package:matcher` rule auto-wraps bare values in
      `equals(...)`. Translate to `Assert.Equal(expected, actual);`
      with the EXPECTED value FIRST and the ACTUAL second — the
      argument order is the INVERSE of Dart's `expect(actual,
      equals(expected))`. Codegen MUST swap. Used ~15x in this file.
      Examples:
      `expect(result!.value, 42)` ->
      `Assert.Equal(42, result!.Value);` (NB: `value` field is `object?`
      per SUT spec; the int `42` is boxed — xUnit's
      `Assert.Equal<object?>(object?, object?)` overload handles the
      box-unbox correctly, comparing via `Object.Equals`);
      `expect(result.globalName, GlobalName.writer('p', 0))` ->
      `Assert.Equal(GlobalName.Writer("p", 0), result.GlobalName);`
      (the expected value is a `GlobalName` factory call —
      value-equality semantics depend on `GlobalName.Equals` /
      `GetHashCode` being correctly converted by the SUT spec; see
      nuance);
      `expect(result.destination, 'q')` ->
      `Assert.Equal("q", result.Destination);`;
      `expect(result!.newGoals.length, 1)` ->
      `Assert.Equal(1, result!.NewGoals.Count);` (List.Count idiom);
      `expect(registry.pendingCount, 2)` ->
      `Assert.Equal(2, registry.PendingCount);`;
      `expect(result.newGoals[0].readerAddr, 400)` ->
      `Assert.Equal(400, result.NewGoals[0].ReaderAddr);` (List
      indexer + property rename).
    idiom_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Argument-order footgun (explicitly addressed — well-known):
      Dart `expect(actual, equals(expected))` is actual-first; xUnit
      `Assert.Equal<T>(T expected, T actual)` is expected-first.
      Codegen MUST swap; sibling specs pre-flagged this for batch
      reuse.
      Value-vs-reference nuance (LOAD-BEARING for this file, NEW for
      object?-typed value comparison): the implicit-equals matcher is
      applied to (a) `int` literals (0, 1, 2, 42, 400), (b) `String`
      literals ('p', 'q', 'r', 'hello'), (c) `GlobalName` instances
      (constructed via `GlobalName.writer('p', 0)` etc.), AND (d)
      Dart `Object?`-typed `value` (`result!.value` is declared
      `Object?` in `GlobalSendFiredResult` per SUT spec; the C#
      equivalent is `object?`). For case (d), the bound value is
      either an `int` (42) or a `String` ('hello'); both flow through
      `Assert.Equal<object?>(object?, object?)` which dispatches to
      `Object.Equals` — `int.Equals(int)` and `string.Equals(string)`
      both perform value equality. NO unboxing risk: xUnit's overload
      resolution picks `Assert.Equal<object?>` and both sides are
      boxed identically. For case (c), comparing `GlobalName`
      references — REQUIRES the SUT's `GlobalName` class to override
      `Object.Equals(object?)` and `Object.GetHashCode()` (or
      implement `IEquatable<GlobalName>`) so that `Assert.Equal(
      GlobalName.Writer("p", 0), result.GlobalName)` performs
      STRUCTURAL equality (same `type` + `agent` + `index`), NOT
      REFERENCE equality. The SUT spec for
      `lib/multiagent/mad_helpers.dart` owns the implementation
      choice; recorded here as a CROSS-FILE INVARIANT.
      List-length idiom: Dart `<list>.length` -> C# `<list>.Count`
      (PROPERTY rename, specific to `IList<T>` / `List<T>` / arrays);
      reused from the SUT side `rf-dart-list-length-to-csharp-list-count`
      idiom.
  - construct_key: dart.expression.member_access_method_call_propagation
    source_form: >-
      "registry.register(goal)
       registry.registerSpawns(spawns)
       registry.hasGoalFor(100)
       registry.getGoalFor(100)
       registry.onWriterBound(...)
       registry.pendingCount
       result.value / result.globalName / result.destination
       result.newGoals[0].readerAddr / result.newGoals[0].globalName
       goal1.globalName.isWriter
       goal2.globalName.isReader"
    target_decision: >-
      Dart member access on an instance — method call `x.foo(args)`,
      getter access `x.bar` — maps DIRECTLY to C# member access
      `x.Foo(args)` / `x.Bar` (PascalCased per the cross-cutting
      `rf-dart-getter-to-csharp-property` and
      `rf-dart-method-to-csharp-method` idioms pinned by sibling SUT
      specs). Specifically: `registry.register(goal)` ->
      `registry.Register(goal)`; `registry.registerSpawns(spawns)` ->
      `registry.RegisterSpawns(spawns)`; `registry.hasGoalFor(K)` ->
      `registry.HasGoalFor(K)`; `registry.getGoalFor(K)` ->
      `registry.GetGoalFor(K)`; `registry.onWriterBound(...)` ->
      `registry.OnWriterBound(...)`; `registry.pendingCount` ->
      `registry.PendingCount`; `result.value` -> `result.Value`;
      `result.globalName` -> `result.GlobalName`; `result.destination`
      -> `result.Destination`; `result.newGoals` -> `result.NewGoals`;
      `goal1.globalName.isWriter` -> `goal1.GlobalName.IsWriter`;
      `goal.readerAddr` -> `goal.ReaderAddr`. The SUT spec
      (`lib/multiagent/global_send.dart.md`) is the source of truth
      for each emitted member's exact shape (auto-property vs method,
      get-only vs get/set, etc.); THIS test spec records the call-site
      shape (PascalCase).
    idiom_id: null
    research_finding_id: rf-dart-member-access-to-csharp-member-access-pascalcase
    nuance: >-
      Casing-rename nuance (explicitly addressed): Dart lowerCamelCase
      members (`hasGoalFor`, `pendingCount`, `isWriter`) map to C#
      PascalCase (`HasGoalFor`, `PendingCount`, `IsWriter`) per the
      cross-cutting `rf-dart-getter-to-csharp-property` /
      `rf-dart-method-to-csharp-method` idioms. The .NET naming
      conventions doc
      (`https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions`
      + Framework Design Guidelines) authoritatively pins this. Indexer
      access `<list>[i]` is preserved verbatim (same `[]` syntax on
      both sides — see `rf-dart-list-indexer-to-csharp-list-indexer`
      from globalize_test.dart.md, reused). The `pendingCount`
      construct is a Dart GETTER (`int get pendingCount =>
      _goals.length;` per the SUT spec); on the C# side it becomes a
      `public int PendingCount => _goals.Count;` expression-bodied
      property — zero-arg, no parentheses at call site.
conversion_units:
  - "cu-1: file-scope using directives (Xunit + System.Collections.Generic + <RootNs>.Multiagent for the three SUT references). NO `using static` form — every call goes through instance APIs."
  - "cu-2: namespace declaration mirroring test/multiagent path (<RootNs>.Test.Multiagent)"
  - "cu-3: two sibling top-level classes — public class GlobalSendGoalTests (4 [Fact] methods) and public class GlobalSendRegistryTests (2 [Fact] methods) — each optionally tagged with [Trait(\"Group\", \"<label>\")]"
  - "cu-4: 6 [Fact(DisplayName=\"<original label>\")] public void methods total (4 + 2), one per Dart test() call, all executable (NO Skip), all with /// <summary> carrying the original Given/When/Then comments + spec-section references (Section 4, Section 12)"
  - "cu-5: per-method body: arrange-act-assert translation with `var registry = new GlobalSendRegistry(\"p\")`, `var table = new GlobalWritersTable(\"p\")`, `var goal = new GlobalSendGoal(readerAddr: ..., globalName: GlobalName.Writer(\"p\", 0), destination: \"q\")`, `var spawns = new List<GlobalSendSpawn> { new GlobalSendSpawn(...), ... }`, `var result = registry.OnWriterBound(writerAddr: ..., value: ..., table: table, extractVariables: _ => new List<TermVar>())`, expect() routed to Assert.* per matcher-routing idioms (isTrue -> True, isFalse -> False, isNotNull -> NotNull, isNull -> Null, implicit-equals -> Equal-with-arg-swap)"
  - "cu-6: lambda translations — `(_) => []` -> `_ => new List<TermVar>()`, `(_) => [TermVar.reader(...)]` -> `_ => new List<TermVar> { TermVar.Reader(...) }` — assigned to `Func<object?, IReadOnlyList<TermVar>>` parameter per SUT spec"
  - "cu-7: `!` and member-access translations preserved with the Dart runtime-vs-C#-compile-time-semantics caveat. Three `result!.*` usages are each preceded by `Assert.NotNull(result)`. Two `registry.GetGoalFor(K)!` usages on lines 169 and 173 are NOT preceded by per-key `Assert.NotNull` but ARE preceded by `Assert.Equal(2, registry.PendingCount)` — codegen MAY insert explicit `Assert.NotNull(registry.GetGoalFor(K))` to fully preserve Dart runtime-throw semantics (recommended)."
  - "cu-8: cross-file dependency on the SUT's structural-equality implementation: `GlobalName` MUST be emitted with IEquatable<GlobalName> + Object.Equals/GetHashCode (or as a `record class`) so Assert.Equal(GlobalName.Writer(\"p\", 0), result.GlobalName) performs value-equality not reference-equality — recorded as a hard invariant; the SUT spec for lib/multiagent/mad_helpers.dart.md owns the implementation choice"
  - "cu-9: cross-file dependency on `GlobalSendFiredResult.Value` being typed `object?` (per SUT spec lib/multiagent/global_send.dart.md) and `Assert.Equal<object?>` correctly dispatching to `Object.Equals` for boxed int/string comparisons — recorded as a hard invariant"
  - "cu-10: cross-file dependency on `GlobalSendRegistry.OnWriterBound` being SYNCHRONOUS (returns `GlobalSendFiredResult?`, NOT `Task<GlobalSendFiredResult?>`) per SUT spec — isolate-ownership invariant. The .NET port MUST NOT introduce `async` / `Task` here; doing so would force the test body into `await registry.OnWriterBound(...)` and silently change concurrency semantics"
escalations: []
```

## Rationale + research provenance

### Why all 6 tests are `[Fact]` (NOT `[Fact(Skip=...)]`)

Every `test(...)` call in this file has executable arrange-act-assert in the
body (no `skip:` argument anywhere). Contrast with the sibling
`mad_error_handling_test.dart`, where all 5 tests are
`skip: 'Not yet implemented'` and map to `[Fact(Skip="Not yet implemented")]`.
The same test-framework-mapping idiom (xUnit, `[Fact]` per Dart `test()`)
applies to both files; the only difference is the absence of the `Skip=`
argument. THIS file therefore reuses the framework idiom and the
test-callback idiom verbatim from the sibling specs and adds NO new
skip-related surface.

### Two sibling groups -> two sibling classes (NEW relative to globalize_test.dart)

This file is the FIRST test-file convspec to record the two-sibling-groups
shape. globalize_test.dart, global_writers_table_test.dart, and
localize_test.dart each had ONE outer group. boot_loader_test.dart had
nested groups mapped to a single class with `[Trait]`-tagged methods. THIS
file is the simplest case: two sibling groups (`GlobalSendGoal` and
`GlobalSendRegistry`) — each maps to its OWN public class. The mapping is
the documented xUnit convention (one class per logical test grouping); no
new framework-level research required.

### Reuse from sibling test-file specs (FR-012 / SC-007)

Idiom KB reuse (no re-research) per FR-012:

- `rf-dart-package-test-to-dotnet-xunit` — framework choice pinned by all
  prior test-file specs. xUnit selected as batch-wide default. Authoritative
  source: Microsoft Learn `unit-testing-csharp-with-xunit` + xunit.net docs.
- `rf-dart-test-main-to-xunit-class-with-facts` — drop `main`, lift
  registered tests to `[Fact]` methods on classes.
- `rf-dart-package-test-group-to-xunit-class` — `group(label, body)` ->
  `public class <Label>Tests`; sibling groups -> sibling classes.
- `rf-dart-test-callback-to-xunit-method-body` — Dart test callback
  closure becomes the method body of the `[Fact]` method.
- `rf-dart-expect-isTrue-to-xunit-assert-true`,
  `rf-dart-expect-isNotNull-to-xunit-assert-notnull`,
  `rf-dart-expect-isNull-to-xunit-assert-null`,
  `rf-dart-expect-equals-to-xunit-assert-equal-argorder` — matcher-routing
  rows pinned by smoke_test.dart, global_writers_table_test.dart, and
  globalize_test.dart.
- `rf-dart-package-sut-import-to-csharp-using` — in-repo `package:` ->
  `using <Namespace>;`.
- `rf-dart-final-local-to-csharp-var-local` — `final <local>` ->
  `var <local>`.
- `rf-dart-bang-operator-to-csharp-null-forgiving` — `expr!.x` ->
  `expr!.X` with the runtime-vs-compile-time invariant.
- `rf-dart-named-constructor-to-csharp-static-factory` — Dart
  `ClassName.namedCtor(args)` -> C# `ClassName.NamedCtor(args)` static
  factory method. First-recorded by globalize_test.dart; reused.
- `rf-dart-named-argument-to-csharp-named-argument` — Dart named arg ->
  C# named arg, both at call site. Recorded by globalize_test.dart; reused.
- `rf-dart-list-literal-to-csharp-list-initializer` — Dart `[a, b]` ->
  C# `new List<T> { a, b }`. Recorded by globalize_test.dart; reused.

All reused verbatim. SC-007 (>=95% recurring constructs via recorded
idiom) is satisfied.

### New idioms first-recorded by this file (NEW research findings)

This file introduces FOUR new construct kinds not covered by sibling
test specs:

- `rf-dart-expect-isFalse-to-xunit-assert-false`: Dart `isFalse` matcher
  -> xUnit `Assert.False`. Authoritative Dart side: pub.dev
  `package:matcher` `isFalse` constant
  (`https://pub.dev/documentation/matcher/latest/matcher/isFalse-constant.html`).
  Authoritative .NET side: xunit.net `Assert.False` API reference. Both
  sides authoritative. Strict-boolean semantics identical. Symmetric
  to the already-pinned `rf-dart-expect-isTrue-to-xunit-assert-true`.

- `rf-dart-positional-primary-ctor-to-csharp-positional-ctor`: Dart's
  positional-parameter primary ctor (`ClassName(this.field)`) -> C# single
  positional-parameter constructor (`public ClassName(string field) {
  Field = field; }`). Authoritative Dart side: dart.dev language tour
  `constructors`
  (`https://dart.dev/language/constructors#initializer-list`).
  Authoritative .NET side: Microsoft Learn `constructors` reference
  (`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/constructors`).
  Both sides authoritative. The Dart `this.field` shorthand has no
  C# equivalent (each parameter must be assigned in the constructor
  body — or via C# 12 primary constructors, an alternative encoding
  recorded in the SUT spec but not adopted at the test-call-site level).

- `rf-dart-arrow-lambda-to-csharp-lambda`: Dart arrow-style anonymous
  function `(arg) => expr` -> C# lambda expression `arg => expr`.
  Authoritative Dart side: dart.dev language tour `Functions / anonymous
  functions`
  (`https://dart.dev/language/functions#anonymous-functions`).
  Authoritative .NET side: Microsoft Learn `Lambda expressions`
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/lambda-expressions`).
  Both sides authoritative. Single-expression body maps verbatim;
  underscore-identifier `_` in lambda parameter slot is just an
  identifier on both sides (NOT the C# 9 discard pattern, which only
  applies in pattern / deconstruction contexts).

- `rf-dart-member-access-to-csharp-member-access-pascalcase`: Dart
  member access (method call / getter access) on an instance ->
  C# member access with PascalCased member name. Authoritative Dart side:
  dart.dev `Operators` / `Member access`
  (`https://dart.dev/language/operators`). Authoritative .NET side:
  Microsoft Learn `Member access operators`
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/member-access-operators`)
  + .NET naming conventions
  (`https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions`).
  Both sides authoritative. The PascalCase rename is the cross-cutting
  C# convention; sibling SUT specs pin the per-member shape (auto-
  property vs method vs expression-bodied).

### Cross-file invariants

Three hard cross-file invariants this test spec depends on (recorded as
conversion-units cu-8/cu-9/cu-10):

(1) `GlobalName` MUST be emitted with structural equality (`Object.Equals`
+ `GetHashCode` overrides, OR `IEquatable<GlobalName>`, OR `record class`).
Without this, `Assert.Equal(GlobalName.Writer("p", 0), result.GlobalName)`
silently degrades to reference equality and ALL `expect(..., GlobalName.
xxx('p', i))` assertions in this file fail. The SUT spec
`lib/multiagent/mad_helpers.dart.md` is the source of truth.

(2) `GlobalSendFiredResult.Value` MUST be typed `object?` (per SUT spec).
`Assert.Equal<object?>(object?, object?)` dispatches to `Object.Equals`,
which handles boxed int/string comparisons correctly. If the SUT
specced `Value` as `dynamic` or as a specific narrow type, the
comparison semantics would diverge.

(3) `GlobalSendRegistry.OnWriterBound` MUST be SYNCHRONOUS (returns
`GlobalSendFiredResult?`, not `Task<...>`). The SUT spec records the
isolate-ownership invariant: per-agent state is single-threaded;
async/Task would force every caller into `await` and silently introduce
concurrency the Dart side does not have. THIS test spec relies on the
synchronous shape — every `var result = registry.OnWriterBound(...);`
line is a direct synchronous call.

### Spec-section traceability preserved

The Dart source documents 4 spec-section references in inline comments
(Section 4 — the `global_send` predicate; Section 12 — Goal Atomicity).
Each must be carried into the corresponding C# method's `/// <summary>`
XML-doc block — this is the spec-only-no-guessing discipline
(FR-013/023) at the doc-comment level: the conversion preserves the
invariant-tracing the test file documents. NOT a separate construct row
because it is uniform across all 6 tests and falls under the
test-callback idiom's already-recorded `/// <summary>` carry-over
requirement.

### Why no escalations

Every construct in this file is authoritative-supported on both sides.
The matcher routing table is mostly already pinned by sibling test specs;
the one new matcher row (`isFalse`) cites official Dart matcher
documentation and xUnit API documentation. The new construct rows
(arrow-lambda -> C# lambda; positional-primary-ctor -> C# positional
ctor; member-access -> PascalCase member access) each have authoritative
docs on both sides. The two SUT-spec-owned dependencies
(structural-equality on `GlobalName`; `object?` typing on `Value`;
synchronous `OnWriterBound`) are recorded as cross-file invariants,
not unresolved decisions. NO idiom-vs-research conflict, NO
idiom-vs-idiom conflict, NOTHING undecidable. The `escalations: []` is
intentional, not a placeholder.

### Cross-file dependency note

The three SUT specs
(`.codeconv/conversion-specs/lib/multiagent/global_send.dart.md`,
`.../global_writers_table.dart.md`,
`.../mad_helpers.dart.md`) are the sources of truth for: the exact
namespace, the emitted shape of `GlobalSendGoal` / `GlobalSendFiredResult`
/ `GlobalSendRegistry` (constructor signatures, property names,
structural vs identity equality), the emitted shape of `GlobalSendSpawn`
/ `GlobalizeResult` / `LocalizeResult`, the `GlobalName` / `TermVar`
static-factory placements, and the `OnWriterBound` /
`RegisterSpawns` / `GetGoalFor` method signatures (return types,
parameter shapes, sync/async stance). THIS test spec records the
call-site dependencies without pinning the SUT's internal choices.
Codegen wiring joins the four specs at the project-skeleton level
(langpair / 016-init scope, OUT OF this single-file artifact).
