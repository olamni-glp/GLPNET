# Conversion Spec — test/multiagent/ui_mediator_test.dart

> Conversion-spec artifact for test/multiagent/ui_mediator_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/ui_mediator_test.dart
source_sha256: ccd3b832f06620db74e8876962e3f8dfdd080591d2dd03e1fd0f16fe9c4281aa
target_code_unit: test/multiagent/UiMediatorTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and replace
      with `using Xunit;` at file scope. xUnit is the project-wide test
      framework already pinned by every prior test-file convspec
      (test/multiagent/mad_error_handling_test.dart.md,
      test/multiagent/boot_loader_test.dart.md,
      test/multiagent/global_writers_table_test.dart.md,
      test/multiagent/globalize_test.dart.md,
      test/multiagent/localize_test.dart.md,
      test/multiagent/global_send_test.dart.md,
      test/multiagent/mad_scenarios_test.dart.md). THIS file MUST reuse that
      idiom verbatim (FR-012 / SC-007) — no re-research. The .NET test
      project (.csproj — out of this single-file artifact's scope) provides
      `xunit` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` NuGet
      references. Codegen projects to a single namespace mirroring the Dart
      `test/multiagent` directory (e.g. `<RootNs>.Test.Multiagent`). Codegen
      MUST also add `using System;` (for `Action<string>` delegate and
      `Exception` if surfaced), `using System.IO;` (for the file path
      manipulations under `File(...).absolute.path` — see
      dart.import.dart_io_core_library below), `using
      System.Collections.Generic;` (for the `List<string>` outputLines
      buffer), `using System.Text.RegularExpressions;` (for the `Regex`
      replacement on the mediator source — see
      dart.regex.replace_all_inline_pattern), and
      `using System.Threading.Tasks;` (for `Task` — the test bodies are
      `async`/`await`).
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice nuance (load-bearing, explicitly addressed): xUnit
      pinned project-wide; NUnit / MSTest recorded as alternatives in the
      research-finding row but NOT used here. Module/namespace nuance:
      Dart's `package:test` exposes top-level functions (`group`, `test`,
      `setUp`, `expect`, `contains`) re-exported via the one import; xUnit
      has NO top-level test functions — tests are public instance methods
      on a public class discovered via `[Fact]` reflection. Async-test
      surface IS present in this file (all three test callbacks are
      `() async { ... await engine.runGoal('test') ... }`); xUnit's
      `async Task` test methods are the canonical mapping — see
      dart.package_test.test_call_async below.
  - construct_key: dart.import.dart_io_core_library
    source_form: "import 'dart:io';"
    target_decision: >-
      Drop the `dart:io` line entirely; replace its load-bearing symbols at
      first use with the canonical .NET equivalents — `File(path)` ⇒
      `System.IO.Path`/`System.IO.File` (the .NET file abstractions). Per
      the carry-forward from lib/engine/glp_engine.dart.md (the precedent
      that maps Dart `File(path).readAsStringSync()` to .NET
      `File.ReadAllText(path)` and `File(path).absolute.path` to
      `Path.GetFullPath(path)`), this file's two uses route to the same
      .NET BCL surface: `File(socialAgentPath).readAsStringSync()` ⇒
      `File.ReadAllText(socialAgentPath)` (sync filesystem read, returns
      `string`); `File('../programs/self.glp').absolute.path` ⇒
      `Path.GetFullPath("../programs/self.glp")` (resolves to absolute
      path string). No `using` directive emitted for the Dart import; the
      targets surface through `using System.IO;` already added by the
      package_test_import construct above.
    idiom_id: null
    research_finding_id: rf-dart-dart-io-to-dotnet-system-io
    nuance: >-
      Standard-library import nuance (explicitly addressed): Dart's
      `dart:io` is a CORE library exposing `File`/`Directory`/`Platform`/
      `Process`/`stdin`/`stdout`. The .NET counterpart is `System.IO`
      (File/Directory static classes + FileInfo/DirectoryInfo
      instance-based classes). Sync-vs-async nuance (LOAD-BEARING):
      Dart's `File(...).readAsStringSync()` and `File(...).absolute.path`
      (a property) are SYNCHRONOUS; the .NET `File.ReadAllText(path)`
      and `Path.GetFullPath(path)` are ALSO synchronous — semantics
      agree. Async-equivalent `File.ReadAllTextAsync` exists but is NOT
      used here because the Dart source explicitly uses the `Sync`
      suffix. Path-resolution nuance: Dart `File('../programs/self.glp').absolute.path`
      resolves relative-to-CWD-at-call-time; .NET `Path.GetFullPath(string)`
      also resolves relative-to-`Environment.CurrentDirectory`; semantics
      agree as long as the test runner's CWD matches the Dart test runner's
      CWD (the Dart `dart test` runs from package root by convention —
      contrast with .NET `dotnet test` which runs from the test-project
      bin/Debug/<tfm>/ directory; this CWD shift is a LOAD-BEARING
      cross-cutting concern handled at the test-project skeleton level,
      NOT in this artifact — recorded so codegen flags the working-directory
      assumption).
  - construct_key: dart.package_under_test.import_directive
    source_form: "import 'package:glp_runtime/engine/glp_engine.dart';"
    target_decision: >-
      Map to a `using` directive that names the C# namespace produced by
      converting `glp_runtime/lib/engine/glp_engine.dart` (e.g.
      `using <RootNs>.Engine;` — the SUT namespace is decided when
      `glp_engine.dart` itself is converted; that convspec lives at
      `.codeconv/conversion-specs/lib/engine/glp_engine.dart.md` and pins
      the `GlpEngine` reference class with its public surface
      `GlpEngine(string rootSelfGlpPath)` ctor [named-required Dart ctor
      collapsed to positional-with-named-call-site per the SUT spec],
      `GlpEngine.Runtime` property, `GlpEngine.LoadSource(string source)`,
      `GlpEngine.RunGoal(string goal)` returning `Task<ExecutionResult>`,
      and the public mutable config flag `StrictTypes`). The codegen
      stage MUST emit a `using <RootNs>.Engine;` that resolves the SUT
      symbol `GlpEngine` used by this test file, plus a `using
      <RootNs>.Runtime;` to resolve `GlpRuntime` (transitively reachable
      via `engine.runtime.outputCallback`). The `ExecutionResult` type
      returned by `RunGoal` lives in the engine namespace per the engine
      SUT spec — same `using <RootNs>.Engine;` covers it.
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed and IDENTICAL to
      global_send_test.dart.md / globalize_test.dart.md / mad_scenarios_test.dart.md):
      a `package:` import that resolves to an in-repo Dart library (NOT
      to a pub.dev third-party package) maps to a C# `using <Namespace>;`
      that targets the OUTPUT namespace of the converted Dart library —
      NOT a separate NuGet reference. Distinguish by inspecting the
      `package:` URI prefix against the host repo's `pubspec.yaml` `name:`
      (here, `glp_runtime`). Project-file wiring (`<ProjectReference>`
      from the test .csproj to the runtime .csproj) is langpair/project-
      skeleton level, recorded so codegen knows the `using` alone is
      insufficient without the project reference. ONE-vs-TWO-usings nuance:
      the Dart source imports ONLY `glp_engine.dart` directly, but the
      test accesses `engine.runtime.outputCallback = ...` — a transitive
      reference to the `GlpRuntime` type (a public property on
      `GlpEngine`). If `GlpEngine.Runtime` returns the runtime as `object`
      or `dynamic` then the lambda-assign call site would not type-check;
      the engine SUT spec confirms the property is strongly typed
      `GlpRuntime` (NOT `object`), so codegen MAY need to emit `using
      <RootNs>.Runtime;` so that the `OutputCallback` member is reachable
      WITHOUT full-qualification. Recorded so codegen does not silently
      drop the runtime `using`.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { final socialAgentPath = '...'; final uiMediatorPath = '...'; group('ui_mediator', () { ... }); }"
    target_decision: >-
      Drop Dart `void main()` entirely — xUnit discovers `[Fact]` methods
      on `public` classes by reflection; there is no per-file entrypoint
      to emit. The two top-level `final` path constants (`socialAgentPath`
      and `uiMediatorPath`) sit inside `main` in the Dart source — they
      are closed over by every test body. In the C# port they become
      PRIVATE `const string` fields on the enclosing xUnit test class
      (`private const string SocialAgentPath = "../programs/typed_book/social_graph/typed_social_agent.glp";`
      and analogous for `UiMediatorPath`); since the values are
      compile-time string-literal constants, `const string` is the
      precise mapping (NOT `static readonly`, which would also work but
      is overkill for a compile-time-known literal). The single `group(...)`
      call inside `main` becomes the enclosing test class (see next
      construct).
    idiom_id: rf-dart-test-main-to-xunit-class-with-facts
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Lifecycle nuance (explicitly addressed): Dart `main` runs once per
      test-file process and registers tests; xUnit has no per-file hook —
      only per-class (constructor + `IDisposable.Dispose`) and
      per-collection fixtures. THIS file's `main` body is exactly two
      `final` local declarations + one `group(...)` call with no other
      statements; the two locals are migrated to class-scoped
      `const string` fields (preserving the close-over) and the
      `group(...)` body becomes the class. Single-group nuance: IDENTICAL
      to globalize_test.dart.md / boot_loader_test.dart.md (single outer
      group), CONTRASTING with global_send_test.dart.md (two sibling
      groups -> two sibling classes) and mad_scenarios_test.dart.md (four
      sibling groups -> four sibling classes). THIS file's single
      `group('ui_mediator', () { ... })` -> ONE public class
      `UiMediatorTests` containing all three `[Fact]` methods.
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('ui_mediator', () { late GlpEngine engine; late List<String>
      outputLines; setUp(() { engine = GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path)..strictTypes = false; outputLines = []; engine.runtime.outputCallback = (line) => outputLines.add(line); }); test(...); test(...); test(...); });"
    target_decision: >-
      Dart `group('ui_mediator', body)` maps to a single `public class
      UiMediatorTests` (PascalCased label + `Tests` suffix). The class
      hosts: (a) the two private string fields from the main scope
      (`SocialAgentPath` / `UiMediatorPath`); (b) two private instance
      fields backing the `late` declarations
      (`private GlpEngine _engine = null!;` and
      `private List<string> _outputLines = null!;`); (c) the per-test
      constructor body translating `setUp(() { ... })` (see
      dart.package_test.setUp_block + dart.package_test.late_field_in_group
      below); (d) three `[Fact]` async methods, one per Dart `test(...)`
      call (see dart.package_test.test_call_async below). The original
      group label `'ui_mediator'` MAY be preserved via
      `[Trait("Group", "ui_mediator")]` on the class for reporter parity.
      No nested `group(...)`; no `tearDown` — so no
      `IDisposable.Dispose` needed.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance (explicitly addressed): the label `'ui_mediator'`
      is a snake_case Dart identifier; the C# convention for a CLASS NAME
      is PascalCase. Codegen MUST emit `UiMediatorTests` (NOT
      `Ui_MediatorTests` and NOT `Ui_mediatorTests`); the rule is
      lowercase the underscores away and PascalCase each token (`ui` ->
      `Ui`, `mediator` -> `Mediator`, append `Tests`). This is IDENTICAL
      to the rule applied for `'GlobalSendGoal'` (already-PascalCase
      input) and `'Section 10.1: Direct Communication ...'` (non-identifier
      chars stripped) — the rule is "strip non-identifier characters and
      PascalCase remaining tokens". Per-test method names follow the
      same rule (see dart.package_test.test_call_async).
  - construct_key: dart.package_test.late_field_in_group
    source_form: >-
      "late GlpEngine engine;
       late List<String> outputLines;"
    target_decision: >-
      Dart `late T x;` fields declared in the `group` callback (closed
      over by setUp + every test) map to `private T _x = null!;` instance
      fields on the xUnit test class — IDENTICAL idiom to
      boot_loader_test.dart.md's `late BootLoader loader` ->
      `private BootLoader _loader = null!;`. Specifically:
      `late GlpEngine engine;` -> `private GlpEngine _engine = null!;`;
      `late List<String> outputLines;` ->
      `private List<string> _outputLines = null!;` (Dart `List<String>`
      -> C# `List<string>`; element type `String` is the C# `string`
      keyword alias for `System.String` — the cross-cutting Dart-string-
      to-C#-string idiom already pinned). The `null!` non-nullable
      "assigned-later" idiom matches Dart's `late` semantics
      (initialised before any reader runs by the per-test constructor
      that translates `setUp`). The Dart identifiers `engine` /
      `outputLines` become C# private fields with the conventional
      underscore-prefix camelCase (`_engine` / `_outputLines`) per .NET
      naming guideline for private fields.
    idiom_id: rf-dart-late-field-to-csharp-nullforgiving-field
    research_finding_id: rf-dart-late-field-to-csharp-nullforgiving-field
    nuance: >-
      Null-safety nuance (explicitly addressed, IDENTICAL to
      boot_loader_test.dart.md): Dart `late T x;` is a non-null `T` that
      throws `LateInitializationError` if read before assignment; the
      closest C# equivalent for an xUnit per-test field is
      `private T _x = null!;` (non-nullable reference, suppressed
      initialiser warning, assigned in the constructor). Because the
      xUnit constructor runs BEFORE every `[Fact]`, the `null!` is
      replaced before any reader runs — semantically equivalent to
      Dart `late + setUp`. Alternative `private T? _x;` (nullable + `!`
      at every read site) was REJECTED because it inverts the
      "guaranteed-initialised" contract that `late` encodes; recorded
      in the research finding. List-type nuance: `List<String>` ->
      `List<string>` requires `using System.Collections.Generic;` at
      file scope.
  - construct_key: dart.package_test.setUp_block
    source_form: >-
      "setUp(() {
         engine = GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path)
                  ..strictTypes = false;
         outputLines = [];
         engine.runtime.outputCallback = (line) => outputLines.add(line);
       });"
    target_decision: >-
      Dart `setUp` registered inside the outer group maps to the xUnit
      test class's CONSTRUCTOR body: `public UiMediatorTests() { ... }`.
      xUnit instantiates the test class once per test method
      (constructor-per-test isolation), which matches `package:test`'s
      per-test fresh-state semantics exactly. NO `[SetUp]` attribute
      exists in xUnit (that is NUnit's idiom); using the constructor
      is the documented xUnit pattern. No `tearDown` is present in this
      file, so no `IDisposable.Dispose` is emitted. Three load-bearing
      sub-translations:
      (a) `engine = GlpEngine(rootSelfGlpPath: <path>)..strictTypes = false`
      ⇒ `_engine = new GlpEngine(rootSelfGlpPath: Path.GetFullPath("../programs/self.glp")) { StrictTypes = false };`
      — the Dart CASCADE operator `..` (which returns the original
      receiver after applying the mutation) is folded into a C# OBJECT
      INITIALIZER `{ StrictTypes = false }` on the `new GlpEngine(...)`
      expression. The Dart `File(...).absolute.path` call becomes
      `Path.GetFullPath(...)` (per dart.import.dart_io_core_library
      above). The named-required Dart ctor argument `rootSelfGlpPath:`
      is preserved at the C# call site per the engine SUT spec which
      pins a positional C# ctor with named-call-site labels for
      readability.
      (b) `outputLines = []` ⇒ `_outputLines = new List<string>();` —
      Dart empty list literal `[]` (whose type is inferred from the
      `late List<String> outputLines;` declaration) maps to C#
      `new List<string>()` — IDENTICAL to global_send_test.dart.md's
      empty-list-literal-with-context-type idiom
      (`rf-dart-list-literal-to-csharp-list-initializer`).
      (c) `engine.runtime.outputCallback = (line) => outputLines.add(line);`
      ⇒ `_engine.Runtime.OutputCallback = line => _outputLines.Add(line);` —
      assignment of a Dart arrow-style lambda to a delegate-typed
      nullable property on a transitively-accessed runtime instance.
      The runtime SUT spec (lib/runtime/runtime.dart.md) pins
      `OutputCallback` as `public Action<string>? OutputCallback { get; set; }`
      — a NULLABLE delegate property with public getter+setter. The
      arrow lambda `(line) => outputLines.add(line)` becomes
      `line => _outputLines.Add(line)` — single-arg lambda, parentheses
      optional in C# for a bare-identifier parameter, Dart `.add(...)`
      -> C# `.Add(...)` (List<T>.Add) per the
      `rf-dart-list-add-to-csharp-list-add` idiom.
    idiom_id: rf-dart-setup-to-xunit-constructor
    research_finding_id: rf-dart-setup-to-xunit-constructor
    nuance: >-
      Lifecycle nuance (explicitly addressed): `package:test`'s `setUp`
      is per-test and runs in the same isolate; xUnit's constructor is
      per-test and runs on the same thread — both give fresh
      `_engine` / `_outputLines` per test, identical observable
      semantics. Cascade-operator nuance (NEW for the multiagent test
      specs — first-recorded here): Dart `expr..member1 = value1
      ..member2 = value2` chains mutation on a receiver and returns the
      ORIGINAL receiver; C# has no `..` operator but has OBJECT
      INITIALIZER syntax (`new T(args) { Member1 = value1, Member2 =
      value2 }`) and WITH-EXPRESSIONS for records (`record with
      { Member1 = ... }`) that achieve the same shape. For an OBJECT
      CONSTRUCTION expression (Dart `new Foo(...)..member = ...`), the
      idiomatic C# is `new T(args) { Member = ... }` — recorded under
      `rf-dart-cascade-operator-to-csharp-object-initializer-or-method-chain`.
      For mid-method cascades that don't construct (e.g.
      `existingObject..method1()..method2()`), the C# equivalent is
      `existingObject.Method1(); existingObject.Method2();` (no
      single-expression form). This file has ONE cascade, on a
      constructor call — the object-initializer form fits exactly.
      Delegate-vs-event nuance: per the runtime SUT spec,
      `OutputCallback` is a PUBLIC nullable delegate-typed PROPERTY
      (NOT a field, NOT an `event`) — direct assignment with `=` is
      valid C#; multicast `+=` is grammatically allowed but the test
      uses single-assignment matching the Dart shape. Async-setUp
      nuance: the Dart `setUp(() { ... })` is SYNCHRONOUS (no `async`);
      the C# constructor is also synchronous (xUnit constructors cannot
      be `async`; if asynchronous setup were needed the idiom would be
      `IAsyncLifetime.InitializeAsync` — NOT used here, recorded in the
      research finding for forward-compat).
  - construct_key: dart.expression.cascade_operator_object_construction
    source_form: >-
      "GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path)
       ..strictTypes = false"
    target_decision: >-
      Dart's cascade operator `expr..member = value` applied to a
      constructor invocation returns the constructed object after
      applying the mutation. The faithful C# translation is the OBJECT
      INITIALIZER form `new T(args) { Member = value }`. Specifically
      `GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path)..strictTypes = false`
      becomes
      `new GlpEngine(rootSelfGlpPath: Path.GetFullPath("../programs/self.glp")) { StrictTypes = false }`.
      The Dart cascade is left-associative; multiple chained cascades
      (`..a = x ..b = y`) translate to multiple object-initializer
      members (`{ A = x, B = y }`). The engine SUT spec confirms
      `StrictTypes` is a public mutable property (matches the
      object-initializer requirement that the assigned member be
      `public` and settable). ALTERNATIVE shapes considered and
      REJECTED: (i) `var engine = new GlpEngine(...); engine.StrictTypes
      = false;` — verbose two-statement form, loses the
      single-expression-construction shape that mirrors Dart's
      cascade; (ii) C# `with`-expression — only applies to records,
      `GlpEngine` is a reference class (NOT a record) per the engine
      SUT spec, so `with` is not applicable.
    idiom_id: null
    research_finding_id: rf-dart-cascade-operator-to-csharp-object-initializer-or-method-chain
    nuance: >-
      Cascade-semantics nuance (explicitly addressed, FIRST-RECORDED
      for the test-file convspecs in this analysis batch): Dart's `..`
      cascade is "apply mutation, return ORIGINAL RECEIVER" — it does
      NOT compose return values. C# object initializer is a SYNTACTIC
      EXTENSION of `new T(...)` that runs the constructor THEN applies
      the property-setters; the runtime semantics agree (constructor
      first, then mutations) AND the expression result is the
      constructed object. Cascade-vs-fluent-method-chain nuance: Dart
      `..` works on ANY member (property setter, method, getter
      side-effect); C# object initializer works ONLY on PUBLIC settable
      MEMBERS. If a future Dart source uses cascade to invoke a METHOD
      (`obj..doSomething()..doAnother()`), the C# port would NOT be an
      object initializer — instead a sequence of statements OR (if the
      methods return `this`) a fluent chain. This file's only cascade
      is on a property setter, so object-initializer applies. Authoritative
      Dart side: dart.dev language tour `Cascade notation`
      (https://dart.dev/language/operators#cascade-notation).
      Authoritative .NET side: Microsoft Learn `Object and Collection
      Initializers`
      (https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers).
      Both sides authoritative.
  - construct_key: dart.package_test.test_call_async
    source_form: >-
      "test('grounds befriend output with request ID', () async {
         final socialSource = File(socialAgentPath).readAsStringSync();
         final mediatorSource = File(uiMediatorPath).readAsStringSync()
             .replaceAll(RegExp(r'-mode\\s*\\(\\s*system\\s*\\)\\s*\\.'), '');
         engine.loadSource('''...''');
         final result = await engine.runGoal('test');
         print('Status: ${result.status}');
         print('Output: $outputLines');
         expect(outputLines, contains('befriend(bob, req(1))'));
       });"
    target_decision: >-
      Each Dart `test(label, body)` with `() async { ... }` body and no
      `skip:` argument becomes a `public async Task` method on the
      enclosing class, decorated with
      `[Fact(DisplayName = "<original label>")]`. xUnit DISCOVERS and
      AWAITS `async Task` methods natively — this is the documented
      async-test idiom. Method name = label PascalCased with
      non-identifier characters stripped:
      `'grounds befriend output with request ID'` ->
      `GroundsBefriendOutputWithRequestId`;
      `'passes ground connected message through'` ->
      `PassesGroundConnectedMessageThrough`;
      `'passes ground received message through'` ->
      `PassesGroundReceivedMessageThrough`.
      Method body translates the Dart arrange-act-assert verbatim:
      (i) `final socialSource = File(socialAgentPath).readAsStringSync();`
      -> `var socialSource = File.ReadAllText(SocialAgentPath);`;
      (ii) the mediator-source-with-regex-strip (see
      dart.regex.replace_all_inline_pattern below);
      (iii) `engine.loadSource('''...''')` (triple-quoted multi-line
      string with `$` interpolation of socialSource and mediatorSource)
      -> `_engine.LoadSource($$"""...""");` using C# 11 raw-string-literal
      with `$$` for double-dollar interpolation, OR
      `_engine.LoadSource($"... {socialSource} ... {mediatorSource} ...")`
      using C# `$@"..."` interpolated verbatim — see
      dart.string.triple_quoted_with_interpolation below;
      (iv) `final result = await engine.runGoal('test');` -> `var result
      = await _engine.RunGoal("test");`;
      (v) `print('Status: ${result.status}');` -> `Console.WriteLine($"Status: {result.Status}");`
      (Dart `print(...)` -> C# `System.Console.WriteLine(...)` per
      `rf-dart-print-to-csharp-console-writeline`); Dart `${expr}`
      string interpolation -> C# `{expr}` inside an `$"..."`
      interpolated string;
      (vi) `print('Output: $outputLines');` -> `Console.WriteLine($"Output: {string.Join(", ", _outputLines)}");`
      OR `Console.WriteLine($"Output: [{string.Join(", ", _outputLines)}]");` —
      Dart `$outputLines` invokes the implicit `toString()` on the
      `List<String>` which produces `[a, b, c]` form; C# `{_outputLines}`
      in an interpolated string invokes `Object.ToString()` which on
      `List<T>` produces `System.Collections.Generic.List`1[System.String]`
      (the type name, NOT the elements). Codegen MUST emit an explicit
      `string.Join` (or `[" + string.Join(", ", _outputLines) + "]"`) to
      match the Dart `print` output shape, OR the print is treated as
      diagnostic-only (which it is — these are debug prints) and the
      mismatch tolerated;
      (vii) `expect(outputLines, contains('befriend(bob, req(1))'));`
      -> `Assert.Contains("befriend(bob, req(1))", _outputLines);` per
      the matcher-routing table (see
      dart.package_test.expect_contains below).
      The Given/When/Then-style intent expressed by the test label
      (grounds befriend output / passes ground connected message /
      passes ground received message) MUST carry into the target as a
      `/// <summary>` doc-comment block per method so the
      `ui_mediator.glp` traceability and the test's narrative purpose
      survive the conversion.
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Async-test nuance (explicitly addressed, FIRST-RECORDED for the
      test-file convspecs that exercise async test bodies — sibling
      specs all had synchronous test callbacks). Dart `test(label, () async
      { ... })` declares an async test callback; xUnit's `[Fact] public
      async Task <Name>()` is the canonical match (xUnit awaits returned
      `Task` / `ValueTask`). Method signature: `public async Task
      <Name>()` (NOT `async void` — `async void` is RESERVED for event
      handlers in C# and CANNOT be awaited by the xUnit runner; emitting
      `async void` would silently break test reporting). Authoritative
      .NET side: xunit.net documentation
      (https://xunit.net/docs/getting-started/v3/getting-started)
      explicitly documents `async Task` test methods. Closure-capture
      nuance: each callback captures `engine` / `outputLines` /
      `socialAgentPath` / `uiMediatorPath` from the outer `group` /
      `main` scope; the xUnit translation captures `this._engine` /
      `this._outputLines` / the class-scoped `const string` fields,
      which is equivalent because the constructor (setUp) has already
      assigned the instance fields before the method runs. Skip-semantics
      nuance: no `skip:` argument anywhere in this file, so NO `Skip=`
      property on `[Fact]`. Print-vs-debug-output nuance (LOAD-BEARING
      footgun): the two `print(...)` calls in each test body are
      DIAGNOSTIC ONLY (they print status and outputLines for human
      inspection); they are NOT load-bearing for the test's
      pass/fail. Codegen MAY translate them to `Console.WriteLine(...)`
      (which xUnit captures via `ITestOutputHelper` if injected, but
      typically `Console.WriteLine` from a test method shows in the
      test runner's captured-output pane) OR substitute the canonical
      xUnit pattern of injecting `ITestOutputHelper output` into the
      ctor and calling `output.WriteLine(...)`. The latter is more
      idiomatic xUnit; the former is the direct shape. Recorded under
      the `rf-dart-print-to-csharp-console-writeline` idiom and the
      forward-looking `rf-xunit-test-output-helper-injection` idiom.
  - construct_key: dart.string.triple_quoted_with_interpolation
    source_form: "engine.loadSource('''<embedded multi-line GLP program with `$socialSource` and `$mediatorSource` interpolations followed by literal `procedure send_to_user(_?). send_to_user([T | In]) :- ground(T?) | '_output'(T?), send_to_user(In?). send_to_user([]). procedure consume(_?). consume([_|Rest]) :- consume(Rest?). consume([]). procedure test. test :- ui_mediator(alice, ch([msg(agent, '_user', befriend(bob, _))], AgentOut), ch([], UserOut), [], 1), send_to_user(UserOut?), consume(AgentOut?).` — three test bodies use the same template, varying only the `befriend(bob, _)` / `connected(bob)` / `received(bob, hello)` payload inside the inner `msg(agent, '_user', <payload>)`>''');"
    target_decision: >-
      Dart triple-single-quoted multi-line string with `$identifier`
      interpolation — the load-bearing characteristic is that
      `$socialSource` and `$mediatorSource` are substituted with the
      local-variable values (read from the .glp files), and the rest
      is GLP source code that the compiler parses byte-for-byte. Two
      faithful C# translations exist, BOTH valid (codegen choice):
      (i) C# 11+ INTERPOLATED RAW STRING LITERAL with `$$"""..."""`
      (double-dollar opens, `{{` and `}}` are the literal-brace
      markers — but this file has NO literal `{` or `}` in the GLP
      source, so single-dollar `$"""..."""` is also safe and codegen
      MAY use the shorter form). The Dart `$socialSource` /
      `$mediatorSource` become C# `{socialSource}` / `{mediatorSource}`
      inside the interpolated raw string:
      `$"""\n{socialSource}\n{mediatorSource}\n\nprocedure send_to_user(_?).\n... """`.
      (ii) FALLBACK for C# &lt; 11 — interpolated verbatim string
      `$@"..."` with `{` doubled to `{{` and `}` doubled to `}}` IF
      they appear (they do not in this file's GLP source, so the
      doubling does not apply), AND embedded `"` escaped via `""`
      (the GLP source uses single-quoted atoms `'_output'` /
      `'_user'` only — NO embedded double-quotes — so no escaping
      needed). The Dart triple-quote does NOT process `\n` / `\t`
      escapes (literal); the C# verbatim string ALSO does not process
      `\n` / `\t`; the C# raw string ALSO does not process them —
      semantics agree across all three forms. Codegen MUST preserve
      embedded newlines BYTE-IDENTICALLY (the GLP lexer parses these
      characters as part of `procedure send_to_user(_?). ...` and any
      whitespace drift would change the parsed AST).
    idiom_id: null
    research_finding_id: rf-dart-triple-quoted-with-interpolation-to-csharp-raw-string-interpolated
    nuance: >-
      Interpolated-raw-string nuance (explicitly addressed, NEW for the
      test-file convspecs — sibling specs had triple-quoted strings
      WITHOUT interpolation, e.g. boot_loader_test.dart.md's raw `'''...'''`
      fixtures with no `$identifier` substitutions). Dart's `'''$var'''`
      interpolates the named local; C# 11 raw string literals support
      interpolation via the `$"""..."""` prefix (single-dollar) OR
      `$$"""..."""` (double-dollar — escapes the single `{` so two
      `{{` would be needed for an interpolation). The single-dollar
      form is shorter and matches Dart's single-`$` syntax. Whitespace
      nuance (LOAD-BEARING for the GLP lexer): Dart triple-quoted
      preserves leading whitespace EXACTLY as written; C# raw strings
      strip a common indent matched to the closing `"""` column —
      codegen MUST emit the closing `"""` at column 0 (or adjust
      indentation) so the literal payload is byte-identical. The Dart
      source has no leading-indent stripping (the `'''...'''` payload
      starts at the line after `'''` and ends at the line before the
      closing `'''`); the C# port MUST preserve this exactly. Verbatim-
      vs-raw-string trade-off: verbatim `$@"..."` requires `""` for
      embedded `"`; raw `$"""..."""` does not, but requires the
      closing-quote sequence to NOT appear in the payload (it doesn't
      here). Both work for this file's GLP source. Recorded as a
      first-use idiom; codegen MAY pick either, preferring the C# 11
      raw-string form for new code. Authoritative .NET side: Microsoft
      Learn `String interpolation - raw string literals`
      (https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string).
      Authoritative Dart side: dart.dev language tour `Strings`
      (https://dart.dev/language/built-in-types#strings).
  - construct_key: dart.regex.replace_all_inline_pattern
    source_form: >-
      "File(uiMediatorPath).readAsStringSync()
          .replaceAll(RegExp(r'-mode\\s*\\(\\s*system\\s*\\)\\s*\\.'), '');"
    target_decision: >-
      Dart's `String.replaceAll(Pattern, String)` with `RegExp(r'...')` ->
      C# `Regex.Replace(input, pattern, replacement)` (static method on
      `System.Text.RegularExpressions.Regex`). The Dart raw-string
      pattern `r'-mode\\s*\\(\\s*system\\s*\\)\\s*\\.'` (raw — backslash
      is literal, `\\s` is the whitespace metacharacter, `\\(`/`\\)`
      escape literal parens, `\\.` escapes the literal period)
      translates to a C# raw-string pattern (or `@"..."` verbatim) with
      IDENTICAL content: `@"-mode\\s*\\(\\s*system\\s*\\)\\s*\\."`
      (Dart and C# share the same `\\s*` whitespace, `\\(` literal-
      paren, `\\.` literal-period regex syntax — `System.Text.RegularExpressions`
      uses .NET-flavour regex which is a superset of Perl/POSIX,
      compatible with Dart's `RegExp` for this expression). Translate:
      `File(uiMediatorPath).readAsStringSync().replaceAll(RegExp(r'-mode\\s*\\(\\s*system\\s*\\)\\s*\\.'), '')`
      ->
      `Regex.Replace(File.ReadAllText(UiMediatorPath), @"-mode\\s*\\(\\s*system\\s*\\)\\s*\\.", "")`.
      Requires `using System.Text.RegularExpressions;` at file scope.
      The replacement string Dart `''` (empty single-quoted) ->
      C# `""` (empty double-quoted).
    idiom_id: null
    research_finding_id: rf-dart-string-replaceall-regexp-to-csharp-regex-replace
    nuance: >-
      Regex-flavour nuance (explicitly addressed): Dart's `RegExp` is
      JavaScript-flavour (ECMA-262); C# `System.Text.RegularExpressions`
      is .NET-flavour. The two flavours agree on `\\s` (whitespace),
      `\\(`/`\\)` (literal-paren-escape), `\\.` (literal-period-escape),
      `*` (zero-or-more quantifier) — exactly the operators used in this
      file's pattern. They DIVERGE on (a) lookbehind: Dart needs ES2018,
      .NET has had it forever; (b) named-capture group syntax (Dart
      `(?<name>...)` vs .NET `(?<name>...)` — same here); (c) Unicode
      properties (`\\p{...}`): Dart needs `unicode: true`, .NET defaults
      to ECMAScript-mode-off (i.e. .NET's `\\p{...}` works without a
      flag). NONE of those divergences apply to this file's pattern.
      Raw-string nuance: Dart `r'...'` and C# `@"..."` BOTH treat
      backslash as literal — the pattern is byte-identical. Method-vs-
      instance nuance: Dart `String.replaceAll` is an instance method
      on `String`; .NET `Regex.Replace` is a STATIC method on `Regex`
      (a string-instance equivalent exists — `Regex.Replace(input,
      pattern, replacement)` overload). Codegen prefers the static form
      because it does NOT require constructing a `Regex` instance and
      matches the Dart inline-construction shape. Compile-vs-runtime
      nuance: Dart `RegExp(...)` compiles the pattern at call-site;
      C# `Regex.Replace(input, pattern, replacement)` ALSO compiles at
      call-site (NOT cached). If perf matters codegen MAY hoist the
      `Regex` to a `private static readonly Regex` field with
      `RegexOptions.Compiled`; this file's regex is invoked 3x (once
      per test) and the perf cost is negligible — inline-construction
      preserved.
  - construct_key: dart.expression.final_local_variable_with_initializer
    source_form: >-
      "final socialSource = File(socialAgentPath).readAsStringSync();
       final mediatorSource = File(uiMediatorPath).readAsStringSync().replaceAll(...);
       final result = await engine.runGoal('test');"
    target_decision: >-
      Translate `final <name> = <expr>;` to `var <name> = <expr>;` in
      C# where the initializer is a method call OR a constructor
      invocation OR an await-expression. Specifically:
      `final socialSource = File(socialAgentPath).readAsStringSync()` ->
      `var socialSource = File.ReadAllText(SocialAgentPath);`;
      `final mediatorSource = File(uiMediatorPath).readAsStringSync().replaceAll(RegExp(r'...'), '')` ->
      `var mediatorSource = Regex.Replace(File.ReadAllText(UiMediatorPath), @"...", "");`;
      `final result = await engine.runGoal('test')` ->
      `var result = await _engine.RunGoal("test");`.
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability-semantics nuance (IDENTICAL to global_send_test.dart.md
      and mad_scenarios_test.dart.md): Dart `final <local>` prevents
      REBINDING the local after init but does NOT prevent mutation of
      the referenced object's state — exactly the same semantics as
      C# `var`. C# 7+ has no `readonly` modifier for locals; conversion
      accepts this minor semantic loss. Async-await-in-local nuance:
      `final result = await ...` is a Dart `final` binding to the
      AWAITED VALUE (NOT to the `Future`); the equivalent C# is `var
      result = await ...` which also binds to the awaited value (NOT
      to the `Task`). Semantics agree. String-literal nuance: Dart
      `'test'` (single-quoted) -> C# `"test"` (double-quoted) — the
      cross-cutting single-quote-to-double-quote rule already pinned.
  - construct_key: dart.expression.await_expression
    source_form: "await engine.runGoal('test')"
    target_decision: >-
      Dart `await <expr>` where `<expr>` evaluates to `Future<T>` maps
      to C# `await <expr>` where `<expr>` evaluates to `Task<T>`. The
      engine SUT spec (`lib/engine/glp_engine.dart.md`) pins
      `RunGoal(string)` as returning `Task<ExecutionResult>` (NOT
      `ValueTask`, NOT a custom awaitable). So `await
      engine.runGoal('test')` -> `await _engine.RunGoal("test")`. The
      enclosing method MUST be `async Task` (declared via the
      `[Fact] public async Task <Name>()` shape — see
      dart.package_test.test_call_async above). The awaited value type
      is `ExecutionResult` (a public class in the engine namespace, per
      the engine SUT spec) — `var result` infers `ExecutionResult`.
    idiom_id: rf-dart-future-async-await-to-csharp-task-async-await
    research_finding_id: rf-dart-future-async-await-to-csharp-task-async-await
    nuance: >-
      Async-model nuance (explicitly addressed, IDENTICAL to the
      glp_engine.dart.md SUT spec's `runGoal` -> `RunGoal` mapping):
      Dart `Future<T>` is a single-completion async primitive; .NET
      `Task<T>` is the equivalent (single-completion, awaitable,
      cancellable via `CancellationToken`). Dart `await` and C# `await`
      have identical surface semantics (the awaiting method must be
      `async`; the awaited value's status is observed; on completion
      the awaiter resumes). Stream-vs-IAsyncEnumerable nuance: NOT
      exercised in this file (no `Stream` / `await for`); the only
      async surface is a single `await runGoal(...)` per test. Cancellation
      nuance: Dart `Future` does NOT support cancellation natively; .NET
      `Task` does (via `CancellationToken`); the test bodies do NOT
      cancel — semantics agree at the absence-of-cancellation level.
  - construct_key: dart.expression.member_access_method_call_propagation
    source_form: >-
      "engine.loadSource(...)
       engine.runGoal('test')
       engine.runtime
       engine.runtime.outputCallback
       result.status
       outputLines.add(line)
       File(...).readAsStringSync()
       File(...).absolute.path"
    target_decision: >-
      Dart member access on an instance — method call `x.foo(args)`,
      getter access `x.bar`, property-chain `x.y.z` — maps DIRECTLY to
      C# member access `x.Foo(args)` / `x.Bar` / `x.Y.Z` (PascalCased
      per the cross-cutting Dart-member-name-to-C#-PascalCase idiom
      pinned by global_send_test.dart.md and mad_scenarios_test.dart.md
      as `rf-dart-member-access-to-csharp-member-access-pascalcase`).
      Specifically:
      `engine.loadSource(source)` -> `_engine.LoadSource(source)`
      (PascalCased method);
      `engine.runGoal('test')` -> `_engine.RunGoal("test")`
      (PascalCased method + single-to-double-quote);
      `engine.runtime` -> `_engine.Runtime` (PascalCased property);
      `engine.runtime.outputCallback` -> `_engine.Runtime.OutputCallback`
      (chained PascalCased property access);
      `result.status` -> `result.Status` (PascalCased property);
      `outputLines.add(line)` -> `_outputLines.Add(line)`
      (PascalCased List<T>.Add);
      `File(socialAgentPath).readAsStringSync()` -> `File.ReadAllText(SocialAgentPath)`
      (entire Dart `File(path).readAsStringSync()` collapsed to the
      static `File.ReadAllText(path)` per
      `rf-dart-file-readasstringsync-to-csharp-file-readalltext`);
      `File('../programs/self.glp').absolute.path` -> `Path.GetFullPath("../programs/self.glp")`
      (Dart File.absolute.path resolves to absolute path string; .NET
      `Path.GetFullPath` is the canonical equivalent).
    idiom_id: rf-dart-member-access-to-csharp-member-access-pascalcase
    research_finding_id: rf-dart-member-access-to-csharp-member-access-pascalcase
    nuance: >-
      Casing-rename nuance (explicitly addressed, IDENTICAL to sibling
      test specs): Dart lowerCamelCase members (`loadSource`, `runGoal`,
      `outputCallback`, `runtime`, `status`, `strictTypes`) map to C#
      PascalCase (`LoadSource`, `RunGoal`, `OutputCallback`, `Runtime`,
      `Status`, `StrictTypes`) per the cross-cutting
      `rf-dart-getter-to-csharp-property` / `rf-dart-method-to-csharp-method`
      idioms. The .NET naming conventions doc
      (https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
      authoritatively pins this. Property-chain nuance: `engine.runtime.outputCallback
      = ...` is a chained property access ending in an ASSIGNMENT — the
      C# port preserves the chain shape (`_engine.Runtime.OutputCallback
      = ...`) per the runtime SUT spec which declares `Runtime` as a
      get-only property returning a `GlpRuntime` instance and
      `OutputCallback` as a public settable property on `GlpRuntime`.
      File-API collapse nuance: Dart's `File(path).readAsStringSync()`
      construct (constructor-then-instance-method) collapses to the
      .NET static `File.ReadAllText(path)` — there is NO C# `File`
      constructor that takes a path; the equivalent type
      (`System.IO.FileInfo`) IS instance-based but ALL the read-as-text
      operations are exposed as static helpers on `System.IO.File`. The
      collapse is documented under
      `rf-dart-file-readasstringsync-to-csharp-file-readalltext`.
  - construct_key: dart.expression.lambda_arrow_single_arg
    source_form: "(line) => outputLines.add(line)"
    target_decision: >-
      Dart arrow-style lambda `(arg) => <expr>` (single positional
      argument, single-expression body) maps to a C# lambda
      `arg => <expr>`. Parentheses around a single bare-identifier
      parameter are optional in C# (and in Dart). The body is a method
      invocation — Dart `.add(...)` -> C# `.Add(...)`. Translate:
      `(line) => outputLines.add(line)` -> `line => _outputLines.Add(line)`.
      The lambda is assigned to the `OutputCallback` property declared
      `Action<string>?` per the runtime SUT spec — `List<T>.Add` returns
      `void` (matches `Action<string>`'s `void` return type, so the
      lambda is a valid `Action<string>`).
    idiom_id: rf-dart-arrow-lambda-to-csharp-lambda
    research_finding_id: rf-dart-arrow-lambda-to-csharp-lambda
    nuance: >-
      Lambda-syntax nuance (IDENTICAL to global_send_test.dart.md and
      mad_scenarios_test.dart.md): Dart `(arg) => expr` is the arrow
      function form (synchronous, single-expression body); C# `arg =>
      expr` is the lambda expression form. Closure-capture nuance: the
      lambda captures `outputLines` (the closed-over `late List<String>`
      from the `group` scope, mapped to `_outputLines` instance field
      on the C# test class). Per-test fresh-instance lifecycle ensures
      `_outputLines` is a fresh `new List<string>()` per test (the
      constructor body runs `_outputLines = new List<string>();` per
      the setUp translation); the lambda is also re-created per test
      (because the constructor runs the assignment expression each time),
      so each test gets its own (engine, outputLines, callback) trio
      with no cross-test leakage. Async/Stream nuance: the lambda is
      SYNCHRONOUS (does not return a `Future` / `Task`) — `OutputCallback`
      is `Action<string>` not `Func<string, Task>`.
  - construct_key: dart.package_test.expect_contains
    source_form: >-
      "expect(outputLines, contains('befriend(bob, req(1))'));
       expect(outputLines, contains('connected(bob)'));
       expect(outputLines, contains('received(bob, hello)'));"
    target_decision: >-
      Dart `expect(<collection>, contains(<element>))` maps to xUnit
      `Assert.Contains(<element>, <collection>);` — note the ARGUMENT-
      ORDER FLIP (Dart `expect(collection, contains(element))` puts
      collection first; xUnit `Assert.Contains(expected, collection)`
      puts the expected element first). The Dart `contains` matcher on
      a `List<String>` checks for ELEMENT membership (NOT substring
      containment — that's the overload for a single `String` argument).
      xUnit `Assert.Contains<T>(T expected, IEnumerable<T> collection)`
      is the matching overload. Specifically:
      `expect(outputLines, contains('befriend(bob, req(1))'))` ->
      `Assert.Contains("befriend(bob, req(1))", _outputLines);`;
      `expect(outputLines, contains('connected(bob)'))` ->
      `Assert.Contains("connected(bob)", _outputLines);`;
      `expect(outputLines, contains('received(bob, hello)'))` ->
      `Assert.Contains("received(bob, hello)", _outputLines);`.
    idiom_id: rf-dart-expect-contains-to-xunit-assert-contains
    research_finding_id: rf-dart-expect-contains-to-xunit-assert-contains
    nuance: >-
      Argument-order nuance (LOAD-BEARING — well-known footgun, IDENTICAL
      to the `equals` flip): Dart `expect(actual, matcher(expected))`
      is actual/collection-first; xUnit `Assert.Contains(expected,
      collection)` is expected-first. Codegen MUST flip. Element-vs-
      substring nuance (explicitly addressed): Dart's `contains` matcher
      DISPATCHES on actual's runtime type — for `List<T>` it checks
      element membership; for `String` it checks substring containment.
      `outputLines` is `List<String>`, so element-membership semantics
      apply. xUnit `Assert.Contains<T>(T expected, IEnumerable<T>
      collection)` is the ELEMENT-MEMBERSHIP overload (uses
      `EqualityComparer<T>.Default`, which for `string` is ordinal-case-
      sensitive — matching Dart's `String.==` which is also ordinal-case-
      sensitive). Codegen MUST select the correct overload — if the
      Dart source were `expect(someString, contains('sub'))` the
      element-membership overload would not apply and codegen would emit
      `Assert.Contains("sub", someString)` (the string-substring
      overload). THIS file uses ONLY the List-element-membership form.
      Authoritative Dart side: pub.dev `package:matcher` `contains`
      matcher
      (https://pub.dev/documentation/matcher/latest/matcher/contains.html).
      Authoritative .NET side: xunit.net `Assert.Contains` API reference.
      Both sides authoritative.
  - construct_key: dart.expression.print_to_console_writeline
    source_form: >-
      "print('Status: ${result.status}');
       print('Output: $outputLines');"
    target_decision: >-
      Dart top-level `print(String)` -> C# `System.Console.WriteLine(string)`.
      Dart string-interpolation `'... ${expr} ...'` and `'... $identifier ...'`
      both map to C# `$"... {expr} ..."` interpolated strings.
      Specifically:
      `print('Status: ${result.status}')` ->
      `Console.WriteLine($"Status: {result.Status}");`;
      `print('Output: $outputLines')` ->
      `Console.WriteLine($"Output: [{string.Join(", ", _outputLines)}]");`
      (Dart `$list` invokes the implicit `toString()` on `List<String>`
      which formats as `[a, b, c]`; C# `{list}` in an interpolated
      string invokes `Object.ToString()` which on `List<T>` produces
      the type name, NOT the elements — codegen MUST emit explicit
      `[{string.Join(", ", _outputLines)}]` to match the Dart shape).
      These are DIAGNOSTIC prints (not load-bearing for the test
      pass/fail); codegen MAY equivalently inject an
      `ITestOutputHelper output` into the test class ctor and emit
      `output.WriteLine(...)` — that's the canonical xUnit pattern for
      per-test diagnostic output.
    idiom_id: null
    research_finding_id: rf-dart-print-to-csharp-console-writeline
    nuance: >-
      Diagnostic-vs-assertion nuance (explicitly addressed): the two
      `print(...)` calls per test are DIAGNOSTIC — they print the
      result status and the captured outputLines for human inspection
      when the test runs interactively. They do NOT contribute to the
      pass/fail decision (that comes from the `expect(...)` assertion).
      The C# port can either preserve them as `Console.WriteLine(...)`
      (visible in `dotnet test --logger trx` output and in test-runner
      console panes) OR replace them with the xUnit-idiomatic
      `ITestOutputHelper.WriteLine(...)`. The latter requires injecting
      `ITestOutputHelper` into the test class ctor — a structural
      change to the class header. The former (Console.WriteLine) is
      the direct shape. Both are observably equivalent for diagnostic
      purposes. List-toString nuance (LOAD-BEARING footgun): Dart
      `List<String>.toString()` produces `[a, b, c]` (square-bracket-
      comma-space format); C# `List<T>.ToString()` produces the type
      name (`System.Collections.Generic.List`1[System.String]`). Codegen
      MUST insert an explicit `string.Join(", ", _outputLines)` (with
      square brackets in the surrounding literal) to match the Dart
      output shape. If this matters for human readability (it does for
      diagnostic prints), the explicit form is mandatory.
  - construct_key: dart.expression.string_interpolation
    source_form: >-
      "'Status: ${result.status}'
       'Output: $outputLines'
       (the implicit triple-quoted string with $socialSource and $mediatorSource — covered separately under dart.string.triple_quoted_with_interpolation)"
    target_decision: >-
      Dart string interpolation `'... $identifier ...'` and `'... ${expr}
      ...'` both map to C# interpolated strings `$"... {identifier}
      ..."` and `$"... {expr} ..."`. The `${expr}` braces-form is for
      arbitrary expressions; the bare `$identifier` form is for simple
      identifier substitution (and is restricted to a single identifier
      with no member access). C# uses the SAME `{expr}` form for both
      cases — there is no distinction at the C# syntax level. Translate:
      `'Status: ${result.status}'` -> `$"Status: {result.Status}"`;
      `'Output: $outputLines'` -> `$"Output: ..."` (where `...` may be
      `{_outputLines}` or `[{string.Join(", ", _outputLines)}]`
      depending on whether the Dart `toString()` shape matters — see
      preceding construct). Inside a C# interpolated string, the
      Dart member-access (`result.status`) PascalCases to `result.Status`
      per the cross-cutting member-naming rule.
    idiom_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      Single-vs-double-quote nuance (explicitly addressed): Dart's
      string interpolation works in BOTH single-quoted and double-quoted
      strings (and triple-quoted variants); C# interpolation works ONLY
      in `$"..."`-prefixed double-quoted strings. The `$` prefix is
      MANDATORY on the C# side to enable interpolation — a bare
      `"Status: {result.Status}"` would be a literal string with `{` and
      `}` characters. Triple-quoted-with-interpolation is covered under
      its own construct (see dart.string.triple_quoted_with_interpolation).
      Bare-identifier-vs-braces nuance: Dart's `$identifier` requires
      the substitution to be a SIMPLE identifier (no `.member` access);
      `${expr}` is needed for compound expressions. C# requires
      `{expr}` for BOTH cases — there is no syntactic distinction.
      Curly-brace-literal nuance: if the surrounding string contains
      literal `{` or `}`, C# requires doubling (`{{` / `}}`). NONE of
      this file's interpolated strings contain literal braces, so the
      doubling does not apply. Authoritative Dart side: dart.dev
      language tour `Strings`
      (https://dart.dev/language/built-in-types#strings).
      Authoritative .NET side: Microsoft Learn `$ - string interpolation`
      (https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/interpolated).
  - construct_key: dart.expression.list_empty_literal
    source_form: "outputLines = [];"
    target_decision: >-
      Dart empty list literal `[]` whose static element type is inferred
      from the context (`late List<String> outputLines;` declaration)
      maps to C# `new List<string>()` (constructor call) OR
      `new List<string> { }` (empty collection-initializer). Codegen
      prefers the constructor-call form `new List<string>()` because
      the empty collection-initializer `new List<string> { }` is
      syntactically valid but unidiomatic (most .NET style guides
      prefer the constructor for empty collections). Specifically:
      `outputLines = []` -> `_outputLines = new List<string>();`. The
      assignment is inside the setUp-translated constructor body.
    idiom_id: rf-dart-list-literal-to-csharp-list-initializer
    research_finding_id: rf-dart-list-literal-to-csharp-list-initializer
    nuance: >-
      Collection-type nuance (IDENTICAL to global_send_test.dart.md and
      mad_scenarios_test.dart.md): Dart `List<T>` growable maps to C#
      `List<T>` growable — same runtime characteristic. Empty-vs-
      non-empty nuance: this file uses ONLY the empty-list form (the
      non-empty case `[a, b]` is covered by the pinned
      `rf-dart-list-literal-to-csharp-list-initializer` idiom and is
      not exercised here). Inferred-element-type nuance: Dart `[]` in
      `late List<String> outputLines; outputLines = [];` is inferred
      to `List<String>` from the field declaration; C# `new List<string>()`
      makes the element type EXPLICIT. The `new[] { }` C# array-literal
      shape is REJECTED for the same reason as in global_send_test.dart.md
      (array-vs-list semantic mismatch, plus the assignment target is
      typed `List<string>` not `string[]`).
  - construct_key: dart.expression.lambda_zero_to_three_print_diagnostic_pair
    source_form: >-
      "print('Status: ${result.status}'); print('Output: $outputLines');"
    target_decision: >-
      Pair of consecutive diagnostic `print(...)` statements per test —
      not a separate construct from `dart.expression.print_to_console_writeline`
      above; recorded here only to flag that BOTH prints carry over
      together (codegen MUST NOT drop one; it MUST translate both, OR
      replace both with the `ITestOutputHelper.WriteLine(...)` pair). No
      execution-order rearrangement is permitted — the Dart source
      prints status BEFORE outputLines, and the C# port MUST preserve
      that ordering. This row exists solely to make the carry-over
      explicit; the actual translation is covered by the
      `dart.expression.print_to_console_writeline` row above. NOT a
      new research finding.
    idiom_id: rf-dart-print-to-csharp-console-writeline
    research_finding_id: rf-dart-print-to-csharp-console-writeline
    nuance: >-
      Carry-over nuance (explicitly addressed): both `print(...)` calls
      MUST be translated together (or BOTH replaced together — never
      mixed) to preserve diagnostic output integrity. Ordering MUST
      survive (status first, output second). This is the same discipline
      as the spec-traceability `/// <summary>` carry-over from sibling
      test specs.
conversion_units:
  - "cu-1: file-scope using directives (Xunit + System + System.IO + System.Collections.Generic + System.Text.RegularExpressions + System.Threading.Tasks + <RootNs>.Engine + <RootNs>.Runtime). NO `using static` form — every call goes through instance APIs except the static `File.ReadAllText` / `Path.GetFullPath` / `Regex.Replace` / `Console.WriteLine` calls which are fully qualified or covered by their parent `using`."
  - "cu-2: namespace declaration mirroring test/multiagent path (<RootNs>.Test.Multiagent), file-scoped namespace per .NET 6+ convention."
  - "cu-3: single top-level test class UiMediatorTests (from group label 'ui_mediator'), optionally [Trait(\"Group\", \"ui_mediator\")] for reporter parity. Three [Fact(DisplayName=\"<original label>\")] public async Task methods (one per Dart test() call, all async, all executable — NO Skip)."
  - "cu-4: private const string fields SocialAgentPath and UiMediatorPath at class scope (the two `final` path locals from Dart `main()` migrated to class-scoped constants — preserves the closed-over shape)."
  - "cu-5: private GlpEngine _engine = null!; and private List<string> _outputLines = null!; instance fields (the two `late` group-scoped variables translated per `rf-dart-late-field-to-csharp-nullforgiving-field`)."
  - "cu-6: public UiMediatorTests() ctor body translating the Dart setUp block: `_engine = new GlpEngine(rootSelfGlpPath: Path.GetFullPath(\"../programs/self.glp\")) { StrictTypes = false };`, `_outputLines = new List<string>();`, `_engine.Runtime.OutputCallback = line => _outputLines.Add(line);`."
  - "cu-7: per-method body: arrange (File.ReadAllText for socialSource; Regex.Replace+File.ReadAllText for mediatorSource), act (engine.LoadSource(<C# 11 raw interpolated string with the GLP program embedded>); var result = await engine.RunGoal(\"test\");), diagnostic (Console.WriteLine($\"Status: {result.Status}\"); Console.WriteLine($\"Output: [{string.Join(\", \", _outputLines)}]\");), assert (Assert.Contains(\"<expected substring>\", _outputLines))."
  - "cu-8: cross-file dependency on the engine SUT spec (lib/engine/glp_engine.dart.md) pinning `GlpEngine` as a public reference class with ctor `GlpEngine(string rootSelfGlpPath)` (named-call-site preserved), public settable property `StrictTypes`, public get-only property `Runtime` (typed `GlpRuntime`), public `void LoadSource(string)`, public `Task<ExecutionResult> RunGoal(string)`. Cross-file invariant: if any of these surfaces drift, this test artifact's per-method body breaks."
  - "cu-9: cross-file dependency on the runtime SUT spec (lib/runtime/runtime.dart.md) pinning `GlpRuntime.OutputCallback` as `public Action<string>? OutputCallback { get; set; }` — a NULLABLE delegate property with public getter+setter."
  - "cu-10: cross-file dependency on `ExecutionResult.Status` (the public PascalCased property on the engine's result type) being printable via `{result.Status}` interpolation; per the engine SUT spec `Status` is an enum or string-like type whose `ToString()` produces a human-readable status name (e.g. 'succeeded' / 'failed' / 'suspended')."
  - "cu-11: cross-file invariant — async sync agreement: every `await _engine.RunGoal(\"test\")` is a single-completion `Task<ExecutionResult>` await; the test method is `async Task` (NOT `async void`); xUnit awaits the returned `Task`."
escalations: []
```

## Rationale + research provenance

This file is a **mediator-level integration test** for `ui_mediator.glp` —
a ground-term mediator between `agent/4` and the Dart host. Unlike the
direct multi-agent unit tests (`global_send_test.dart`,
`globalize_test.dart`, `mad_scenarios_test.dart`), each of its three
tests exercises the full GLP-engine loading + execution path:
load `social_agent.glp` + `ui_mediator.glp` + an in-line auxiliary GLP
program (via `engine.loadSource('''...''')`), run a `test` goal via
`await engine.runGoal('test')`, and assert that a specific ground term
appears in the captured output. Every conversion decision below has
been made under the constraint that the test's narrative purpose
(verify the mediator grounds agent output and forwards user input)
and the byte-identical-fidelity of the embedded GLP source survive the
port.

### rf-dart-package-test-to-dotnet-xunit — project-wide xUnit framework choice

REUSED VERBATIM from sibling specs. xUnit is the project-wide test
framework. The distinguishing facts for THIS file relative to the
sibling specs are (a) async test bodies (every test is `() async { ... }`),
(b) integration-level engine fixture in setUp (not per-test
arrangement), and (c) the embedded multi-line GLP source with `$`-
interpolation of two source-file string contents. Authoritative Dart side:
[dart.dev `package:test`](https://dart.dev/tools/dart-test#the-package-test-package).
Authoritative .NET side:
[Microsoft Learn xUnit + .NET Test Sdk](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test).

### rf-dart-package-sut-import-to-csharp-using — single SUT import collapse

This file imports ONE SUT file directly (`glp_engine.dart`) and
transitively reaches `GlpRuntime` (through the `engine.runtime` property
access). The conversion emits TWO `using` directives:
`using <RootNs>.Engine;` for `GlpEngine` + `ExecutionResult`, and
`using <RootNs>.Runtime;` for `GlpRuntime` (the runtime SUT type whose
`OutputCallback` property is assigned in setUp). The two `using`s
collapse the cross-file dependency to a flat surface; the actual
namespace strings are decided at the engine and runtime SUT conversion
stages and are out of scope for this artifact.

### rf-dart-cascade-operator-to-csharp-object-initializer-or-method-chain — FIRST RECORDED for the multiagent test specs

The `..strictTypes = false` cascade on the `GlpEngine(...)` ctor call is
the FIRST cascade-operator usage in the multiagent test-file convspec
batch. Dart's cascade `..` returns the original receiver after applying
the mutation; the faithful C# translation for a CONSTRUCTOR-cascade is
the OBJECT INITIALIZER form `new T(args) { Member = value }`. The
research finding records BOTH the constructor-cascade case
(object initializer) and the mid-method-cascade case (sequence of
statements OR fluent chain when the methods return `this`), so future
files can reuse the right translation without re-research. Authoritative
Dart side: [dart.dev `Cascade notation`](https://dart.dev/language/operators#cascade-notation).
Authoritative .NET side: [Microsoft Learn `Object and Collection Initializers`](https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers).
Both sides authoritative.

### rf-dart-triple-quoted-with-interpolation-to-csharp-raw-string-interpolated — FIRST RECORDED in the multiagent test batch

Sibling specs (`boot_loader_test.dart.md`, `mad_error_handling_test.dart.md`)
recorded the triple-quoted-raw-string idiom WITHOUT interpolation;
THIS file is the FIRST to combine triple-quote + `$identifier`
interpolation. The research finding distinguishes two viable C#
targets: (i) C# 11+ interpolated raw string literal `$"""..."""` (or
`$$"""..."""` if literal `{` `}` matter — they don't here); (ii) C#
interpolated verbatim string `$@"..."` for pre-C# 11. Both preserve
embedded newlines verbatim AND treat `$` as the interpolation marker
(NOT a literal `$`); codegen choice is per-target-version. The GLP
source's byte-identical preservation requirement is LOAD-BEARING: any
whitespace drift or quote-handling drift would change the parsed AST
in the GLP lexer. Authoritative .NET side:
[Microsoft Learn `Raw string literals`](https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string).
Authoritative Dart side: [dart.dev `Strings`](https://dart.dev/language/built-in-types#strings).

### rf-dart-string-replaceall-regexp-to-csharp-regex-replace — FIRST RECORDED for the multiagent test batch

The mediator-source `.replaceAll(RegExp(r'...'), '')` is the FIRST
regex-replacement in the multiagent test-file convspec batch (sibling
specs had no `String.replaceAll`). The pattern `-mode\s*\(\s*system\s*\)\s*\.`
strips the `-mode(system).` directive from the mediator source before
loading, because the directive marks the source as system-private and
the integration test needs the mediator's user-facing predicates
exposed. The pattern uses operators present in both Dart's
(JavaScript-flavour) and .NET's (PCRE+ flavour) regex engines — direct
translation is byte-identical. Authoritative Dart side:
[dart.dev `RegExp`](https://api.dart.dev/dart-core/RegExp-class.html).
Authoritative .NET side:
[Microsoft Learn `Regex.Replace`](https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex.replace).

### rf-dart-future-async-await-to-csharp-task-async-await — REUSED with async test surface

The `await engine.runGoal('test')` line in each test body is the FIRST
async-test-method surface in the multiagent test-file convspec batch
(sibling specs all had synchronous test bodies). xUnit's `async Task`
test method shape is the canonical mapping. The engine SUT spec
(`lib/engine/glp_engine.dart.md`) pins `RunGoal(string)` as returning
`Task<ExecutionResult>` (the carry-forward from `scheduler.dart.md`'s
`Future<DrainResult>` -> `Task<DrainResult>` async-shape decision). The
method MUST be `async Task` (NOT `async void`); xUnit's runner awaits
the returned `Task`.

### rf-dart-expect-contains-to-xunit-assert-contains — REUSED matcher routing

`expect(<collection>, contains(<element>))` -> `Assert.Contains(<element>,
<collection>)` with argument-order flip. This file uses ONLY the
List-element-membership form (the string-substring overload of
`contains` is not exercised). The matcher is the only assertion form
in this file — every test ends with exactly one `expect(...)` of this
shape.

### rf-dart-print-to-csharp-console-writeline — REUSED diagnostic output

The diagnostic `print(...)` calls translate to `Console.WriteLine(...)`
(direct shape) OR `ITestOutputHelper.WriteLine(...)` (xUnit-idiomatic
per-test output). Both are observably equivalent for diagnostic
purposes; this file is silent on the choice — codegen MAY pick either.
The List-toString shape mismatch (Dart `[a, b, c]` vs C# type-name)
requires explicit `string.Join` to match the Dart output for diagnostic
readability.

### Threading-model escalation INHERITED, NOT re-escalated

Per the task brief: multiagent tests INHERIT the escalated
`runtime/heap_fcp.dart` threading-model. The test bodies access
`engine.runtime.outputCallback` (a `GlpRuntime` member assignment); the
runtime SUT spec pins `OutputCallback` as a plain settable delegate
property with no concurrency annotation — single-threaded access by
the agent that owns the runtime. The deeper question (per-isolate
mailbox vs pinned thread vs single-threaded TaskScheduler) is owned
by `lib/runtime/heap_fcp.dart.md`'s threading escalation; THIS test
artifact is a CONSUMER and re-records nothing.

### setUp -> ctor with delegate-property assignment

The `setUp` block constructs a fresh `GlpEngine`, resets the
`outputLines` buffer, and wires a one-arg lambda `(line) =>
outputLines.add(line)` to `engine.runtime.outputCallback`. The C#
constructor body performs the identical three operations: `new
GlpEngine(...) { StrictTypes = false }` (object initializer for the
cascade), `new List<string>()` for the empty buffer, and a single-arg
lambda assigned to the delegate-typed property. xUnit's
constructor-per-test isolation ensures each `[Fact]` gets a fresh
trio (engine, outputLines, callback) — matching Dart `setUp`'s per-test
fresh-state semantics exactly. The runtime SUT spec confirms
`OutputCallback` is a settable nullable delegate property; the
assignment is `OutputCallback = line => _outputLines.Add(line);`.

### Spec traceability: ui_mediator.glp predicate references

Each test exercises a specific ground-term path through
`ui_mediator.glp`:
- Test 1 (`grounds befriend output with request ID`) verifies the
  mediator grounds the underscore variable `_` in `befriend(bob, _)` to
  a request ID `req(1)` and forwards the resulting ground term.
- Test 2 (`passes ground connected message through`) verifies a
  ground-term `connected(bob)` passes through unchanged.
- Test 3 (`passes ground received message through`) verifies a
  ground-term `received(bob, hello)` passes through unchanged.
These narrative purposes MUST survive the conversion as `/// <summary>`
doc-comment blocks on each method.

### Out-of-scope but recorded

- Project-system wiring (`<ProjectReference>` from the test .csproj to
  the runtime .csproj) is langpair-level; recorded so codegen knows the
  `using` alone is insufficient without the project reference.
- The exact `<RootNs>` placeholder is langpair-level and pinned at the
  workspace level, not per file.
- The working-directory difference between `dart test` (runs from
  package root) and `dotnet test` (runs from bin/Debug/<tfm>/) is a
  cross-cutting test-project concern. THIS file's relative paths
  (`../programs/typed_book/social_graph/typed_social_agent.glp`,
  `../programs/self.glp`) assume the Dart-test CWD; codegen MUST
  either (a) compute the path relative to a known repo-root anchor
  (e.g. via `AppContext.BaseDirectory` plus an upward walk), or (b)
  set the test-project's working directory via a `.runsettings` /
  MSBuild property. Out of scope for this artifact; recorded for the
  test-project skeleton stage.

### KB cache hits (no re-research)

All of the following pinned rf-ids were KB cache hits — re-research
was NOT performed (FR-024 reproducibility-offline rule):
`rf-dart-package-test-to-dotnet-xunit`,
`rf-dart-package-sut-import-to-csharp-using`,
`rf-dart-test-main-to-xunit-class-with-facts`,
`rf-dart-package-test-group-to-xunit-class`,
`rf-dart-test-callback-to-xunit-method-body`,
`rf-dart-late-field-to-csharp-nullforgiving-field`,
`rf-dart-setup-to-xunit-constructor`,
`rf-dart-final-local-to-csharp-var-local`,
`rf-dart-arrow-lambda-to-csharp-lambda`,
`rf-dart-list-literal-to-csharp-list-initializer`,
`rf-dart-future-async-await-to-csharp-task-async-await`,
`rf-dart-expect-contains-to-xunit-assert-contains`,
`rf-dart-member-access-to-csharp-member-access-pascalcase`,
`rf-dart-string-interpolation-to-csharp-interpolated-string`.

### Newly recorded rf-ids (defined by this file's first-use rows)

- `rf-dart-dart-io-to-dotnet-system-io` — Dart `dart:io` core-library
  imports collapse to .NET BCL `System.IO` and (for path resolution)
  `System.IO.Path`. Authoritative Dart side: dart.dev `dart:io`
  library reference (https://api.dart.dev/dart-io/dart-io-library.html).
  Authoritative .NET side: Microsoft Learn `System.IO.File` /
  `System.IO.Path` classes
  (https://learn.microsoft.com/dotnet/api/system.io.file).
- `rf-dart-cascade-operator-to-csharp-object-initializer-or-method-chain`
  — Dart `..` cascade -> C# object initializer (for ctor-cascades) OR
  sequence-of-statements (for mid-method cascades). FIRST RECORDED for
  the test-file convspecs.
- `rf-dart-triple-quoted-with-interpolation-to-csharp-raw-string-interpolated`
  — Dart `'''$var ...'''` -> C# `$"""..."""` (C# 11+) or `$@"..."`
  (pre-C# 11). FIRST RECORDED.
- `rf-dart-string-replaceall-regexp-to-csharp-regex-replace` — Dart
  `String.replaceAll(RegExp(r'...'), '')` -> .NET `Regex.Replace(input,
  pattern, replacement)`. Pattern syntax byte-identical for the
  operators used here. FIRST RECORDED for the test-file convspecs.
- `rf-dart-expect-contains-to-xunit-assert-contains` — Dart
  `expect(<collection>, contains(<element>))` -> xUnit
  `Assert.Contains(<element>, <collection>)` with arg-order flip and
  element-membership semantics on `IEnumerable<T>`. May be a first
  record for THIS file (sibling matcher-routing idioms covered
  isTrue/isFalse/isNotNull/isNull/isEmpty but not `contains`).
- `rf-dart-print-to-csharp-console-writeline` — Dart top-level `print`
  -> .NET `Console.WriteLine`. Diagnostic-only; xUnit-idiomatic
  alternative is `ITestOutputHelper.WriteLine`.
- `rf-dart-file-readasstringsync-to-csharp-file-readalltext` — Dart
  `File(path).readAsStringSync()` -> .NET `File.ReadAllText(path)`.
  Synchronous string read; the async overloads exist on both sides
  but are not used here.
- `rf-dart-list-add-to-csharp-list-add` — Dart `List<T>.add(element)`
  -> C# `List<T>.Add(element)`. PascalCase rename only.

### No escalations

Every construct in this file is authoritative-supported on both sides.
The matcher routing (`contains`), the new idioms (cascade,
triple-quoted-with-interpolation, regex-replace, print) all cite
official Dart and .NET documentation. The cross-file dependencies
(`GlpEngine` ctor + `Runtime` property + `RunGoal` async signature,
`GlpRuntime.OutputCallback` delegate property, `ExecutionResult.Status`)
are recorded as cross-file invariants — not unresolved decisions.
NO idiom-vs-research conflict, NO idiom-vs-idiom conflict, NOTHING
undecidable. The `escalations: []` is intentional, not a placeholder.

### Threading-model inheritance

Per the task brief and FR-013 "don't double-escalate" discipline: this
file INHERITS the `runtime/heap_fcp.dart` threading-model escalation
already raised at the SUT level (via `mad_context.dart.md` and others).
The test bodies do not introduce new concurrency surface beyond what
the engine SUT spec already pins (single-threaded GlpRuntime access
per agent; synchronous `LoadSource`; async `RunGoal` awaiting the
scheduler's drain). NO ADDITIONAL ESCALATION here.
