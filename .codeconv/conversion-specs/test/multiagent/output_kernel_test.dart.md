# Conversion Spec — test/multiagent/output_kernel_test.dart

> Conversion-spec artifact for test/multiagent/output_kernel_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/output_kernel_test.dart
source_sha256: c9a1c6ecd561b433029f9130f9006732643ca9915524c539454e9d3e09753a06
target_code_unit: test/multiagent/OutputKernelTest.cs
constructs:
  - construct_key: dart.import.dart_io
    source_form: "import 'dart:io';"
    target_decision: >-
      Dart `dart:io` is the platform synchronous filesystem/process API
      surface; in THIS file it is imported solely for the `File('...').absolute.path`
      expression used inside each `setUp` to resolve the prelude path
      `../programs/self.glp` to an absolute filesystem path. The .NET
      equivalent for absolute-path-resolution is
      `System.IO.Path.GetFullPath(string)` (per Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath`)
      — NOT `FileInfo.FullName`, because the source expression DOES NOT
      construct a `FileInfo`/`File` object and observe a derived property;
      it is a one-shot path-string resolution. Replace the Dart import
      with a file-scope `using System.IO;` (covers `Path` static class and
      `File` static class needed for the literal-path resolution). NO
      `FileInfo` object is constructed in the converted code — the
      `File('../programs/self.glp').absolute.path` expression collapses
      to a single `Path.GetFullPath("../programs/self.glp")` call (see
      dart.expression.file_absolute_path_resolution below).
    idiom_id: rf-dart-io-import-to-csharp-using-system-io
    research_finding_id: rf-dart-io-import-to-csharp-using-system-io
    nuance: >-
      Namespace-collapse nuance (explicitly addressed): Dart `dart:io`
      bundles File / Directory / Process / Socket / Platform under one
      import; C# splits these across `System.IO` (File / Directory / Path
      / Stream) and `System.Diagnostics` (Process) and `System.Net.Sockets`
      / `System.Environment` (Platform-equivalent surface). For THIS file
      only the path-resolution facet is used, so a single
      `using System.IO;` suffices. Per-file working-dir convention
      nuance: the relative path `../programs/self.glp` is resolved
      relative to the PROCESS current working directory at the moment
      `setUp` runs — Dart `File(p).absolute.path` uses
      `Directory.current` as the base, identically `Path.GetFullPath(p)`
      uses `System.IO.Directory.GetCurrentDirectory()` (per the same
      Microsoft Learn page). Both are CWD-sensitive — load-bearing for
      Dart `package:test` (which sets CWD to the package root, i.e. the
      `glp_runtime` directory containing `test/multiagent/output_kernel_test.dart`)
      AND for xUnit (which sets CWD to the test assembly's bin/Debug/
      output folder by default). The CWD parity is NOT preserved by
      the conversion at runtime; the test runner harness MUST set
      `WorkingDirectory` in the .csproj (or the test runner config) so
      that `../programs/self.glp` still resolves to the repo-root
      `programs/self.glp`. This is a CROSS-FILE PROJECT-WIRING
      invariant recorded here for codegen — NOT an in-file decision.
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
      test/multiagent/mad_transactions_test.dart.md). THIS file MUST
      reuse that idiom verbatim (FR-012 / SC-007) — no re-research.
      The .NET test project (.csproj — out of this single-file
      artifact's scope) provides `xunit` + `xunit.runner.visualstudio` +
      `Microsoft.NET.Test.Sdk` NuGet references. Codegen projects to a
      single namespace mirroring the Dart `test/multiagent` directory
      (e.g. `<RootNs>.Test.Multiagent`). Codegen MUST also add
      `using System.Collections.Generic;` at file scope because the
      test bodies materialise `List<string>` (the `outputLines`
      capture buffer) and collection-equality assertions on it.
      `using System.Threading.Tasks;` is REQUIRED at file scope because
      every `[Fact]` method in this file is `async Task`-returning (see
      dart.package_test.test_call_async below).
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice nuance (load-bearing, explicitly addressed): xUnit
      pinned project-wide; NUnit / MSTest recorded as alternatives in the
      research-finding row but NOT used here. Async surface nuance (NEW
      relative to most sibling test specs — this file's tests ARE async):
      every `test` callback in this file uses `() async { ... }` and
      `await engine.runGoal('test')` — see
      dart.package_test.test_call_async for the `[Fact]` -> `async Task`
      mapping. The `using System.Threading.Tasks;` import is therefore
      LOAD-BEARING (`Task` lives in that namespace per Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.threading.tasks.task`),
      NOT an optional convenience.
  - construct_key: dart.package_test.import_sut_relative_package
    source_form: "import 'package:glp_runtime/engine/glp_engine.dart';"
    target_decision: >-
      The single SUT import is a Dart `package:glp_runtime/...` URI
      resolving to the converted C# namespace for
      `glp_runtime/lib/engine/glp_engine.dart`. Replace with a C#
      `using` directive that names the namespace produced by the
      converted SUT (per sibling SUT spec
      `.codeconv/conversion-specs/lib/engine/glp_engine.dart.md` —
      e.g. `using <RootNs>.Engine;`). The SUT spec pins the
      `GlpEngine` reference-class shape with constructor
      `public GlpEngine(string rootSelfGlpPath)`, properties
      `StrictTypes` (bool flag), `Runtime` (the `GlpRuntime` instance —
      itself sporting `OutputCallback : Action<string>?`), and the
      single async entry point `Task<ExecutionResult> RunGoalAsync(string goalText)`
      with `-Async` suffix per Microsoft Framework Design Guidelines.
      The transitive `GlpRuntime.OutputCallback` reference and the
      `ExecutionResult` type are re-exported from the SUT namespace
      (or via a separate `using <RootNs>.Runtime;` if codegen places
      `GlpRuntime` in a sibling namespace per
      `lib/runtime/runtime.dart.md` — same convention as
      mad_scenarios_test.dart.md). Codegen MAY emit one or two
      `using`s depending on the SUT placement; the test file
      references both `GlpEngine` (constructor + LoadSource + RunGoalAsync +
      Runtime + StrictTypes) and (transitively, via `engine.runtime`)
      `GlpRuntime.OutputCallback`, plus the `ExecutionResult` return
      type.
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed and IDENTICAL
      to global_send_test.dart.md / mad_scenarios_test.dart.md): a
      `package:` import resolving to an in-repo Dart library (NOT a
      pub.dev third-party package) maps to a C# `using <Namespace>;`
      that targets the OUTPUT namespace of the converted Dart library —
      NOT a separate NuGet reference. Distinguish by inspecting the
      `package:` URI prefix against the host repo's `pubspec.yaml`
      `name:` (here, `glp_runtime`). Project-file wiring
      (`<ProjectReference>` from the test .csproj to the runtime .csproj)
      is langpair/project-skeleton level. Single-import nuance: only
      ONE SUT import in this file (`glp_engine.dart`); the SUT spec
      arranges so that `GlpRuntime` (and its `OutputCallback`
      property) and `ExecutionResult` are reachable through the same
      single `using`. The exact `using`-count depends on whether
      codegen places `GlpRuntime` in `<RootNs>.Engine` (transitive
      re-export) or `<RootNs>.Runtime` (separate namespace requiring
      a second `using`); recorded as a CROSS-FILE INVARIANT.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('_output kernel', () { ... }); group('send_to_user', () { ... }); }"
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
      `main` is lossless. Two-sibling-groups nuance (SAME as
      global_send_test.dart.md, NOT nested like boot_loader_test.dart.md):
      the two sibling groups ('_output kernel' and 'send_to_user')
      become TWO sibling public classes (`OutputKernelTests` and
      `SendToUserTests`) under the same namespace.
  - construct_key: dart.package_test.group_block_with_setUp
    source_form: >-
      "group('_output kernel', () {
         late GlpEngine engine;
         late List<String> outputLines;
         setUp(() { engine = GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path)..strictTypes = false; outputLines = []; engine.runtime.outputCallback = (line) => outputLines.add(line); });
         test(...); test(...); test(...);
       });
       group('send_to_user', () {
         late GlpEngine engine;
         late List<String> outputLines;
         setUp(() { /* identical body */ });
         test(...); test(...);
       });"
    target_decision: >-
      Each Dart `group(label, body)` containing two `late` fields + a
      `setUp` + N `test` callbacks maps to ONE `public class
      <Label>Tests`. Specifically:
      `group('_output kernel', ...)` -> `public class OutputKernelTests`
      containing 3 `[Fact]` methods + 2 private fields + a constructor;
      `group('send_to_user', ...)` -> `public class SendToUserTests`
      containing 2 `[Fact]` methods + 2 private fields + a constructor.
      Both class names encode the group label in PascalCase
      identifier-safe form (the underscore in `_output` is stripped —
      see Name-mangling nuance) with the conventional `Tests` suffix.
      The original label MAY be preserved via
      `[Trait("Group", "_output kernel")]` /
      `[Trait("Group", "send_to_user")]` on each class for reporter
      parity. The two `late` fields (`engine`, `outputLines`) become
      two private instance fields of the class (see
      dart.package_test.late_field_in_group below); the `setUp`
      callback becomes the class CONSTRUCTOR body (see
      dart.package_test.setUp_block below). NO nested `group(...)`,
      NO `tearDown` anywhere — so no `IDisposable.Dispose` content,
      no `IAsyncLifetime`-driven async teardown is emitted.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance (load-bearing, explicitly addressed): both
      group labels contain identifier-unsafe characters — `'_output
      kernel'` has a leading underscore + a space; `'send_to_user'` is
      already identifier-safe BUT all-lowercase. C# class-naming
      convention requires PascalCase and no leading underscore (a
      leading underscore on a public type would clash with the
      "underscore-prefix = private field" convention pinned by every
      sibling spec). Mangling:
      `'_output kernel'` -> strip leading underscore, split on
      space, PascalCase tokens, append `Tests` ->
      `OutputKernelTests`;
      `'send_to_user'` -> split on underscore, PascalCase tokens,
      append `Tests` -> `SendToUserTests`.
      The ORIGINAL label is preserved verbatim in
      `[Trait("Group", "<original>")]` so the underscore-prefix-IS-the-
      kernel-name semantic ('_output' IS the GLP kernel predicate name,
      a deliberate naming convention) survives at the reporter layer.
      Sibling-groups-NOT-nested-groups nuance: SAME as
      global_send_test.dart.md — the two groups are SIBLING inside
      `main`, neither nested in the other; the documented mapping is
      two SEPARATE classes (NOT one class + `[Trait]`-tagged methods
      à la boot_loader_test.dart.md). Per-group-shared-state nuance
      (LOAD-BEARING — NEW relative to global_send_test.dart.md, IDENTICAL
      to boot_loader_test.dart.md's setUp-having case): both groups
      have a `setUp` that initialises `engine` and `outputLines` afresh
      per test; both groups have private `late` fields shared by every
      test in the group; xUnit's constructor-per-test fresh-instance
      lifecycle ("xUnit.net creates a new instance of the test class
      for every test that is run") gives observably-identical
      semantics to Dart's per-test setUp. Identical setUp body across
      both groups nuance: the setUp bodies in both groups are TEXTUALLY
      IDENTICAL — codegen MAY refactor to a shared base class (e.g.
      `abstract class OutputKernelTestBase`) but the simpler, more-
      faithful translation duplicates the constructor body in both
      classes; recorded as a low-priority forward refactoring note,
      NOT a conversion decision.
  - construct_key: dart.package_test.late_field_in_group
    source_form: >-
      "late GlpEngine engine;
       late List<String> outputLines;"
    target_decision: >-
      Two Dart `late` fields declared in the `group` callback (closed
      over by `setUp` and every nested `test`) map to two `private`
      instance fields on the xUnit test class:
      `private GlpEngine _engine = null!;`
      `private List<string> _outputLines = null!;`
      Both fields are assigned by the class constructor (the setUp
      mapping — see next construct), so `null!` is the non-nullable
      "assigned-later" idiom that matches Dart's `late` semantics
      (initialised before any reader runs; would throw
      `LateInitializationError` if read uninitialised — though xUnit's
      constructor-per-test guarantees the constructor runs first).
      Field naming: PascalCased lower-camel Dart names become
      `_camelCase` underscore-prefixed C# private fields per the
      cross-cutting C# private-field naming convention (Microsoft Framework
      Design Guidelines / Roslyn-default rule IDE1006); both names
      have NO collision with the class identifier or any other member.
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
      Dart `late + setUp`. Alternative `private T? _x;` (nullable +
      `!` at every read site) was REJECTED because it inverts the
      "guaranteed-initialised" contract that `late` encodes; recorded
      in the research finding. Generic-list-field nuance: `late
      List<String>` -> `private List<string> _outputLines = null!;`
      — the generic type argument PascalCases its Dart spelling
      (`String` -> `string` per the cross-cutting
      `rf-dart-string-to-csharp-string` idiom; Dart `String` is C#
      `string`, NOT `String` — `System.String` and `string` are the
      same type per Microsoft Learn
      `https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types#the-string-type`,
      but `string` is the C# convention).
  - construct_key: dart.package_test.setUp_block
    source_form: >-
      "setUp(() {
         engine = GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path)..strictTypes = false;
         outputLines = [];
         engine.runtime.outputCallback = (line) => outputLines.add(line);
       });"
    target_decision: >-
      Dart `setUp` registered inside the group maps to the xUnit test
      class's CONSTRUCTOR body — NOT `[SetUp]` (NUnit) or
      `[TestInitialize]` (MSTest). xUnit instantiates the test class
      once per test method (constructor-per-test isolation), which
      matches `package:test`'s per-test fresh-state semantics exactly
      (xUnit docs `https://xunit.net/docs/shared-context` —
      "Constructor and Dispose"). The four-statement setUp body
      translates statement-for-statement:
      (1) `engine = GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path)..strictTypes = false;`
      decomposes into: (a) an absolute-path resolution of the literal
      `'../programs/self.glp'` (see
      dart.expression.file_absolute_path_resolution), (b) a
      named-argument constructor invocation
      `GlpEngine(rootSelfGlpPath: <path>)` -> `new GlpEngine(<path>)`
      (the SUT spec pins a POSITIONAL ctor signature
      `public GlpEngine(string rootSelfGlpPath)` — the call-site uses
      the Dart-named-arg syntax which collapses to a single positional
      arg on the C# side because there is only one parameter), and
      (c) the Dart CASCADE operator `..strictTypes = false` which sets
      `engine.strictTypes = false` (see
      dart.expression.cascade_operator_assignment below) — collapses
      into a SECOND statement in the constructor body:
      `_engine.StrictTypes = false;` (the cascade pattern is unrolled
      because C# has no cascade operator);
      (2) `outputLines = [];` -> `_outputLines = new List<string>();`
      per `rf-dart-empty-list-literal-to-csharp-list-of-string-ctor`
      (cached);
      (3) `engine.runtime.outputCallback = (line) => outputLines.add(line);`
      -> `_engine.Runtime.OutputCallback = line => _outputLines.Add(line);`
      per the cached
      `rf-dart-camelcase-field-to-csharp-pascalcase-property` +
      `rf-dart-arrow-lambda-to-csharp-lambda` idioms pinned by
      project_linker_test.dart.md. The arrow lambda
      `(line) => outputLines.add(line)` becomes the C# lambda
      `line => _outputLines.Add(line)` — the parameter is `string`
      (inferred from `Action<string>?` target type of `OutputCallback`
      per SUT spec lib/runtime/runtime.dart.md).
      Concretely the constructor emitted (in each of the two test
      classes) is:
      `public <ClassName>() {
         var path = Path.GetFullPath("../programs/self.glp");
         _engine = new GlpEngine(path);
         _engine.StrictTypes = false;
         _outputLines = new List<string>();
         _engine.Runtime.OutputCallback = line => _outputLines.Add(line);
       }`
    idiom_id: rf-dart-setup-to-xunit-constructor
    research_finding_id: rf-dart-setup-to-xunit-constructor
    nuance: >-
      Lifecycle nuance (explicitly addressed, IDENTICAL to
      boot_loader_test.dart.md): `package:test`'s `setUp` is per-test
      and runs in the same isolate; xUnit's constructor is per-test
      and runs on the same thread — both give a fresh `_engine` /
      `_outputLines` per test, identical observable semantics. No
      `tearDown` is present in this file, so NO `IDisposable.Dispose`
      is emitted. Async-setUp nuance: Dart `setUp(() async { ... })`
      would map to xUnit `IAsyncLifetime.InitializeAsync` (per
      `https://xunit.net/docs/shared-context#class-fixture`) — NOT
      used here; THIS file's setUp body is synchronous (no `async`,
      no `await`), so the simple constructor form suffices. Cascade-
      unrolling nuance: see dart.expression.cascade_operator_assignment
      (the `..strictTypes = false` cascade is unrolled into a
      separate statement). OutputCallback-delegate-assignment nuance
      (LOAD-BEARING — the test's core observation mechanism — see
      dart.runtime.runtime_outputcallback_assign below): the closure
      `(line) => outputLines.add(line)` captures `_outputLines` from
      `this`, which is identical capture-semantics on both sides
      (Dart implicit `this`-capture vs C# implicit `this`-capture in
      lambda) — the captured field reference is rebound per test
      instance, so the callback ALWAYS writes to the freshly-cleared
      list of the current test.
  - construct_key: dart.expression.file_absolute_path_resolution
    source_form: "File('../programs/self.glp').absolute.path"
    target_decision: >-
      Dart `File(path).absolute.path` is a two-step Dart-style fluent
      expression: (1) construct a `File` object wrapping a path string,
      (2) take its `.absolute` getter (returns another `File` whose
      path is the absolute form), (3) take its `.path` getter (returns
      the string path). The whole expression yields a `String` — the
      absolute filesystem path of the input. NO File-object is
      retained; only the resolved path-string is consumed. The
      idiomatic C# equivalent is `System.IO.Path.GetFullPath(string)`
      (Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath` —
      "Returns the absolute path for the specified path string"), which
      collapses the same three Dart steps into one call AND avoids
      constructing a transient `FileInfo` object. Translate
      `File('../programs/self.glp').absolute.path` ->
      `Path.GetFullPath("../programs/self.glp")`. Single-quote ->
      double-quote string literal (Dart `'...'` is a `String`; C# `"..."`
      is a `string` — `'..'` in C# would be a `char` and select a
      non-existent overload). Codegen MUST emit the `using System.IO;`
      at file scope (see dart.import.dart_io nuance) for `Path` to be
      resolvable.
    idiom_id: null
    research_finding_id: rf-dart-file-absolute-path-to-csharp-path-getfullpath
    nuance: >-
      Choice-of-API nuance (explicitly addressed, NOT glossed):
      THREE C# alternatives exist for absolute-path resolution from a
      relative path string and are recorded in the research finding:
      (i) `Path.GetFullPath(string)` — pure static, returns the path
      string immediately (CHOSEN — best 1:1 with the Dart expression's
      shape and intent; no transient object); (ii) `new
      FileInfo(path).FullName` — constructs a `FileInfo` object
      observing its `FullName` property (REJECTED for THIS use because
      the Dart code does NOT retain a File handle; (ii) is the
      preferred map when the Dart code retains the `File` object for
      subsequent operations — see project_linker_test.dart.md which
      uses (ii)); (iii) `Path.Combine(Directory.GetCurrentDirectory(),
      path)` then normalise — REJECTED because `Path.GetFullPath`
      already includes CWD-resolution + normalisation in one call.
      The carve-out rule between (i) and (ii) is recorded in the
      research-finding row as a forward-looking idiom rule. CWD-
      sensitivity nuance: `Path.GetFullPath(string)` is CWD-relative
      identically to Dart `File(p).absolute.path` (both resolve
      relative paths against `Directory.GetCurrentDirectory()` /
      `Directory.current`) — recorded as a CROSS-FILE PROJECT-WIRING
      invariant (see dart.import.dart_io nuance). Path-separator
      nuance: forward-slash `/` is universally accepted by both
      `File('path/...')` (Dart) and `Path.GetFullPath("path/...")` (C#);
      both normalise to the host platform's path separator. Codegen
      preserves the source forward-slashes verbatim — no rewriting.
  - construct_key: dart.expression.cascade_operator_assignment
    source_form: "GlpEngine(rootSelfGlpPath: ...).absolute.path)..strictTypes = false"
    target_decision: >-
      The Dart CASCADE operator `..` (Dart specification:
      `https://dart.dev/language/operators#cascade-notation`) is a
      sugar that evaluates the LHS expression, calls / sets the
      following member, and returns the LHS expression (not the
      member-call result). In this file `..strictTypes = false` is
      attached to a `GlpEngine(...)` constructor call, then the entire
      cascade-result is assigned to `engine`. Decomposed semantics:
      (a) evaluate `GlpEngine(rootSelfGlpPath: <path>)` -> `engine0`;
      (b) execute `engine0.strictTypes = false;`; (c) the cascade-
      expression-value is `engine0`; (d) `engine = engine0;`.
      C# has NO cascade operator. The faithful and idiomatic
      conversion UNROLLS the cascade into two statements:
      `_engine = new GlpEngine(<path>);` followed by
      `_engine.StrictTypes = false;`. NO C# `with`-expression / object-
      initializer is used because (a) `StrictTypes` is a mutable
      property (not an `init`-only property), and (b) the `with`-
      expression applies to `record`-types (not `class` types) and
      this SUT is a `class`. NO chained-assignment trick (`(_engine =
      new GlpEngine(<path>)).StrictTypes = false`) is used because it
      sacrifices readability for no benefit. Per the SUT spec
      `lib/engine/glp_engine.dart.md`, `StrictTypes` is exposed as a
      regular mutable property — assignment after construction is
      always valid.
    idiom_id: null
    research_finding_id: rf-dart-cascade-operator-to-csharp-unroll-to-statements
    nuance: >-
      Operator-semantics nuance (explicitly addressed — well-known
      footgun): Dart cascades RETURN THE RECEIVER, not the called
      member's value; the most common mis-translation in C# would
      be to chain `new T().Foo = X` and bind the assignment result
      (which in C# IS the assigned value, NOT the receiver), losing
      the receiver. Unrolling to two statements sidesteps the
      footgun. Object-initializer-as-alternative nuance:
      `new GlpEngine(<path>) { StrictTypes = false }` would WORK for
      this specific case (Dart `..strictTypes = false` is a simple
      property assignment with no observable side-effect ordering
      requirement) — recorded in the research finding as an
      acceptable alternative when the cascade body is exclusively
      property assignments. Codegen MAY use the object-initializer
      form for cascades containing ONLY property assignments;
      it MUST fall back to the unroll form for cascades containing
      method calls (which cannot appear inside C# object initializers).
      For consistency with the rest of this file's setUp body
      (which performs a sequence of statements), the spec records
      the UNROLL form as the default; the object-initializer form
      is a stylistic alternative.
  - construct_key: dart.runtime.runtime_outputcallback_assign
    source_form: "engine.runtime.outputCallback = (line) => outputLines.add(line);"
    target_decision: >-
      Dotted property-chain + delegate-field assignment with an
      arrow-lambda RHS. Decomposed: (a) `engine.runtime` accesses the
      `runtime` getter on `GlpEngine` (per SUT spec
      `lib/engine/glp_engine.dart.md` -> `Runtime` property);
      (b) `.outputCallback = ...` assigns the `outputCallback` field
      on `GlpRuntime` (per SUT spec `lib/runtime/runtime.dart.md` ->
      `Action<string>? OutputCallback { get; set; }`);
      (c) `(line) => outputLines.add(line)` is a one-arg arrow lambda
      that calls `List<String>.add` on the captured `outputLines`
      field. Maps verbatim to:
      `_engine.Runtime.OutputCallback = line => _outputLines.Add(line);`
      per cached idioms
      `rf-dart-camelcase-field-to-csharp-pascalcase-property` and
      `rf-dart-arrow-lambda-to-csharp-lambda` and
      `rf-dart-void-function-question-to-csharp-action-nullable` (all
      pinned by project_linker_test.dart.md and lib/runtime/runtime.dart.md).
      The lambda's parameter `line` is inferred as `string` from the
      target type `Action<string>?` (Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.action-1` —
      "Encapsulates a method that has a single parameter and does
      not return a value"). The RHS is implicitly convertible to
      `Action<string>` (per the lambda-target-typing rules,
      Microsoft Learn `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/lambda-expressions#natural-type-of-a-lambda-expression`),
      and `Action<string>` is implicitly convertible to
      `Action<string>?` (the nullable form).
    idiom_id: rf-dart-void-function-question-to-csharp-action-nullable
    research_finding_id: rf-dart-void-function-question-to-csharp-action-nullable
    nuance: >-
      Function-type-to-`Action<T>` nuance (explicitly addressed,
      LOAD-BEARING — IDENTICAL to project_linker_test.dart.md): Dart's
      `void Function(String)?` is a structural function type with one
      `String` parameter and no return; C#'s idiomatic counterpart is
      `Action<string>?`. The `?` suffix preserves the Dart nullability —
      `OutputCallback` is nullable on both sides (the runtime may have
      no callback installed, in which case the `_output` kernel
      defaults to `print(formatted)` per body_kernels.dart.md). The
      lambda body `_outputLines.Add(line)` returns `void`
      (`System.Collections.Generic.List<T>.Add(T)` per Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.add`
      returns `void`) — matches `Action<string>` (which discards
      return). Captured-state nuance: the lambda captures
      `_outputLines` from `this` (the test class instance); both Dart
      and C# resolve this as a closure over the field, allocated per-
      class-instance — exactly the semantics needed for per-test
      fresh-buffer isolation. Per-test-fresh-instance ensures each
      test's callback writes to a NEW `List<string>`, never the
      previous test's list. NULL-coalescing nuance: the SUT spec
      `lib/runtime/runtime.dart.md` documents call-side invocation as
      `OutputCallback?.Invoke(s)` (null-conditional invoke) — not
      relevant at THIS assignment site, but recorded as cross-file
      invariant for the kernel implementation.
  - construct_key: dart.package_test.test_call_async
    source_form: >-
      "test('<label>', () async {
         engine.loadSource('''<glp-source>''');
         final result = await engine.runGoal('test');
         expect(result.succeeded, isTrue);
         expect(outputLines, [...]);
       });"
    target_decision: >-
      Each Dart `test(label, () async { ... })` callback (THIS file
      has 5: 3 in `_output kernel`, 2 in `send_to_user`; no `skip:`
      argument anywhere) becomes a `public async Task` method on the
      enclosing class, decorated with
      `[Fact(DisplayName = "<original label>")]`. The `async Task`
      shape is xUnit's documented form for async test methods
      (xUnit docs `https://xunit.net/docs/comparisons#assertions` and
      Microsoft Learn `https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap`
      — "Async methods in TAP return a Task or Task<TResult>"). xUnit
      awaits the returned `Task` and surfaces any thrown assertion as
      a test failure. Method name = label PascalCased with non-
      identifier chars stripped (spaces -> CamelCase boundary;
      apostrophes ignored):
      `'prints a constant'` -> `PrintsAConstant`;
      `'prints a struct'` -> `PrintsAStruct`;
      `'prints a list'` -> `PrintsAList`;
      `'consumes a ground stream and prints each term'` ->
      `ConsumesAGroundStreamAndPrintsEachTerm`;
      `'waits for stream elements to become ground'` ->
      `WaitsForStreamElementsToBecomeGround`.
      Method body translates the Dart arrange-act-assert verbatim:
      (1) `engine.loadSource('''...''')` -> `_engine.LoadSource("""...""");`
      (raw-string triple-quote conversion — see
      dart.string.triple_quoted_literal);
      (2) `final result = await engine.runGoal('test');` ->
      `var result = await _engine.RunGoalAsync("test");`
      (the `await` keyword carries verbatim, the
      `runGoal` -> `RunGoalAsync` rename adds the `-Async` suffix per
      Microsoft Framework Design Guidelines as pinned by the SUT spec
      `lib/engine/glp_engine.dart.md`);
      (3) `expect(result.succeeded, isTrue);` ->
      `Assert.True(result.Succeeded);` per the cached
      `rf-dart-expect-isTrue-to-xunit-assert-true` idiom;
      (4) `expect(outputLines, [...]);` ->
      `Assert.Equal(new List<string> { ... }, _outputLines);`
      per the cached
      `rf-dart-expect-equals-to-xunit-assert-equal-argorder` idiom and
      `rf-dart-list-equality-to-xunit-assertequal-collection` cached
      idiom (collection-element-wise equality).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-async-to-xunit-async-task-method
    nuance: >-
      Async-test-shape nuance (LOAD-BEARING, explicitly addressed —
      NEW for the multiagent-test batch): every test in this file is
      async. The target shape is `public async Task <Name>() { ...
      await ... }` — NOT `public void` (which would block on the
      Task synchronously, defeating the entire async pipeline and
      potentially deadlocking) and NOT `public Task` without `async`
      (which would require returning the Task from RunGoalAsync
      manually rather than awaiting it, losing the ability to run
      assertions afterwards). xUnit's discovery + execution pipeline
      treats `async Task`-returning `[Fact]` methods identically to
      sync `void`-returning ones at the user-visible level (per
      `https://xunit.net/docs/comparisons#assertions`). Future-vs-Task
      nuance: Dart `Future<T>` <-> C# `Task<T>` carry-forward from
      `lib/engine/glp_engine.dart.md`
      (`rf-dart-future-async-await-to-csharp-task-async-await`). The
      `await` keyword has IDENTICAL surface syntax + semantics on
      both sides (yields control until the awaited
      future/task completes; resumes on a captured context). xUnit
      does NOT capture a SynchronizationContext by default
      (per `https://xunit.net/docs/parallelism-in-test-frameworks`),
      so `ConfigureAwait(false)` is NOT required in test bodies —
      recorded as a forward-looking note in the research finding.
      Skip-semantics nuance (NOT firing here): no `skip:` argument
      anywhere, so NO `Skip=` property on `[Fact]`.
  - construct_key: dart.expression.final_local_variable_with_initializer_await
    source_form: "final result = await engine.runGoal('test');"
    target_decision: >-
      Dart `final <name> = await <expr>` (final-local + await) maps to
      C# `var <name> = await <expr>` per the cached
      `rf-dart-final-local-to-csharp-var-local` idiom + the `await`
      keyword carrying verbatim. Specifically
      `final result = await engine.runGoal('test');` ->
      `var result = await _engine.RunGoalAsync("test");` (the
      RunGoalAsync rename and the single-quote -> double-quote
      string-literal conversion per cached idioms). The resulting
      `result` C# local has type `ExecutionResult` — flowing from
      `Task<ExecutionResult>.GetAwaiter().GetResult()` semantics
      (Microsoft Learn
      `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/await`).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability-semantics nuance (explicitly addressed): Dart
      `final <local>` prevents REBINDING the local after init but does
      NOT prevent mutation of the referenced object's state — exactly
      the same semantics as C# `var`. C# 7+ has no `readonly` modifier
      for locals; conversion accepts this minor semantic loss
      (sibling specs record the same trade-off). Await-context nuance:
      the C# `await` requires the enclosing method to be `async Task`
      / `async Task<T>` / `async void` — the enclosing `[Fact]` method
      MUST therefore carry the `async Task` modifier (see
      dart.package_test.test_call_async). Exception-propagation
      nuance: a `Future<ExecutionResult>` that completes with an
      exception propagates the exception at the `await` site;
      identically, a `Task<ExecutionResult>` that faults propagates
      the exception at the C# `await` site (per
      `https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/`).
      In THIS file the SUT spec for `glp_engine.dart` documents that
      `runGoal` catches all exceptions internally and returns a failed
      `ExecutionResult` rather than propagating — so the `await` is
      not expected to throw under any test in this file.
  - construct_key: dart.method.engine_load_source_invocation
    source_form: "engine.loadSource('''<glp source>''');"
    target_decision: >-
      Dart instance-method call `engine.loadSource(<source>)` with a
      single positional `String` argument maps to
      `_engine.LoadSource(<source>);` per the cached
      `rf-dart-method-to-csharp-method` idiom (lowerCamelCase ->
      PascalCase rename). The SUT spec `lib/engine/glp_engine.dart.md`
      pins the signature
      `public bool LoadSource(string source, string? filename = null)` —
      the second `filename` parameter is OPTIONAL with a default of
      `null`; in THIS file's call sites the second argument is OMITTED,
      so the C# call form remains `_engine.LoadSource(<rawStr>);` (no
      named-arg, no explicit `null`). The return value (a `bool`
      indicating typecheck success) is DISCARDED at every call site
      in this file (statement-as-expression usage) — the test bodies
      observe success via the subsequent `await engine.runGoal('test')`
      and `result.succeeded` assertion, not via the `loadSource`
      return value. Discard pattern translates verbatim (no special
      handling required — C# also allows expression-statement
      discard of non-void returns).
    idiom_id: rf-dart-method-to-csharp-method
    research_finding_id: rf-dart-method-to-csharp-method
    nuance: >-
      Return-value-discard nuance (explicitly addressed): Dart and C#
      both allow silent discard of non-void method-call return values
      at the statement level. Codegen does NOT need to emit
      `_ = _engine.LoadSource(...)` (the C# discard pattern). The
      Roslyn analyzer IDE0058 ("Expression value is never used")
      flags this by default — disable via `.editorconfig` for the
      test project, OR add the explicit discard `_ =` for IDE0058
      conformance (langpair / project-skeleton level concern; NOT a
      conversion decision). Side-effect ordering nuance: `LoadSource`
      mutates the engine's internal `_loadedPrograms` and
      `_loadedModules` registries (per SUT spec) — the mutation
      ordering is preserved verbatim because the C# `LoadSource` call
      is the first statement in the test body, identical to the Dart
      source.
  - construct_key: dart.string.triple_quoted_literal
    source_form: >-
      "'''
       -mode(system).
       procedure test.
       test :- '_output'(hello).
       '''"
    target_decision: >-
      Dart triple-single-quoted multi-line string literal (used to embed
      each `.glp` source fixture in this file) maps to C# 11+ raw string
      literal (`""" ... """`) per Microsoft Learn
      `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`
      ("Raw string literals make it easier to compose strings that
      contain ... newlines"). Raw strings preserve newlines without
      escape processing — IDENTICAL semantics to Dart triple-quoted
      strings. The fallback for pre-C#11 targets is the verbatim
      string literal `@" ... "` which ALSO preserves newlines without
      escape processing (but requires `""` for embedded double-quote;
      no fixture in this file contains a `"`, so both forms are
      equivalent here). Codegen MUST emit the closing `"""` at column
      0 (matching the source's column-0 closing) so the literal
      payload is byte-identical — Microsoft Learn raw-string docs
      ("The whitespace to the left of the closing quotes is removed
      from all of the lines of the raw string literal"). One subtlety
      load-bearing for THIS file: the GLP source fixtures contain
      Dart triple-single-quote `'''` but no `"""`; the C# raw-string
      form `""" ... """` does not conflict with any character inside
      the fixture, so the simplest three-double-quote form suffices.
    idiom_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    research_finding_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    nuance: >-
      String-literal nuance (explicitly addressed, IDENTICAL to
      boot_loader_test.dart.md and mad_error_handling_test.dart.md):
      Dart triple-quoted strings do NOT process `\n`/`\t` escapes
      (they are literal). C# raw string literals (`"""`) also do not
      process escapes. C# verbatim strings (`@"..."`) also do not
      process escapes but DO require `""` to escape an embedded `"`,
      which is not needed in any literal in this file. Whitespace-
      preservation nuance: Dart triple-quoted preserves leading
      whitespace exactly as written; C# raw strings strip a common
      indent matched to the closing `"""` column — codegen MUST emit
      the closing `"""` at column 0 (or adjust indentation) so the
      literal payload is byte-identical to the Dart source. GLP-source-
      embedding nuance (LOAD-BEARING for THIS file): the Dart source
      uses LEADING NEWLINE inside each `'''<newline>-mode(system).<newline>...'''`
      fixture — Dart triple-quoted strings have a documented rule
      that "If the first character after the opening triple quote is
      a newline, that newline is ignored" (Dart language spec, `Strings`
      section). C# 11 raw-strings have an analogous rule: "If the
      sequence of characters starts with a newline ... that newline
      is ignored". So the leading-newline behaviour is preserved
      identically across both languages. Codegen MUST place the
      opening `"""` followed immediately by a newline to maintain
      the exact-byte-equivalence. Single-quote-vs-double-quote
      delimiter nuance: Dart `'''` and Dart `"""` are interchangeable
      delimiters for triple-quoted; the source uses `'''`. C# only
      offers `"""` for raw strings — codegen MUST emit `"""`
      regardless of the Dart delimiter.
  - construct_key: dart.package_test.expect_isTrue_matcher
    source_form: "expect(result.succeeded, isTrue);"
    target_decision: >-
      Dart `expect(<bool>, isTrue)` (using the `package:matcher`
      `isTrue` constant) maps to xUnit `Assert.True(<bool>);` — strict
      `bool`-typed assertion. Better diagnostic message than
      `Assert.Equal(true, ...)`. Translate
      `expect(result.succeeded, isTrue);` ->
      `Assert.True(result.Succeeded);` per the cached
      `rf-dart-expect-isTrue-to-xunit-assert-true` idiom (pinned by
      boot_loader_test.dart.md and global_send_test.dart.md). The
      `succeeded` getter on `ExecutionResult` maps to the
      `Succeeded` property per the SUT spec
      `lib/engine/glp_engine.dart.md`
      (`rf-dart-getter-to-csharp-property` idiom).
    idiom_id: rf-dart-expect-isTrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Boolean-semantics nuance (explicitly addressed): Dart `isTrue`
      matches only the boolean value `true` — NOT truthiness (Dart
      has no truthiness coercion). xUnit `Assert.True(bool)` is
      identically strict (requires actual `bool true`). Getter-to-
      property nuance: `succeeded` is a `bool get` getter on
      `ExecutionResult` in the SUT (defined as
      `bool get succeeded => ...;`); C# equivalent is an
      expression-bodied property
      (`public bool Succeeded => ...;`). The SUT spec pins the
      property-vs-method choice; this test spec relies on the
      property form (zero-arg, no parentheses at call site) because
      the Dart source accesses it without parens.
  - construct_key: dart.package_test.expect_list_equality
    source_form: >-
      "expect(outputLines, ['hello']);
       expect(outputLines, ['msg(alice, bob, text(hi))']);
       expect(outputLines, ['[a, b, c]']);
       expect(outputLines, ['hello', 'world', 'msg(a, b)']);
       expect(outputLines, ['hello', 'world']);"
    target_decision: >-
      Dart `expect(<list>, <list-literal>)` where the second argument
      is a bare list literal (NOT a matcher) is sugar for
      `expect(<list>, equals(<list-literal>))` per the
      `package:test`/`package:matcher` auto-wrap rule. Translate to
      xUnit `Assert.Equal(<expected-list>, <actual-list>);` with the
      EXPECTED value FIRST and the ACTUAL second — the argument order
      is the INVERSE of Dart's `expect(actual, equals(expected))`.
      Codegen MUST swap. Specifically:
      `expect(outputLines, ['hello'])` ->
      `Assert.Equal(new List<string> { "hello" }, _outputLines);`;
      `expect(outputLines, ['msg(alice, bob, text(hi))'])` ->
      `Assert.Equal(new List<string> { "msg(alice, bob, text(hi))" }, _outputLines);`;
      `expect(outputLines, ['[a, b, c]'])` ->
      `Assert.Equal(new List<string> { "[a, b, c]" }, _outputLines);`;
      `expect(outputLines, ['hello', 'world', 'msg(a, b)'])` ->
      `Assert.Equal(new List<string> { "hello", "world", "msg(a, b)" }, _outputLines);`;
      `expect(outputLines, ['hello', 'world'])` ->
      `Assert.Equal(new List<string> { "hello", "world" }, _outputLines);`.
      xUnit `Assert.Equal<T>(IEnumerable<T>, IEnumerable<T>)`
      performs ELEMENT-WISE equality (per
      `https://xunit.net/docs/comparisons#assertions`) on
      `IEnumerable<T>` — matching Dart `equals` semantics over a
      `List<String>` (element-by-element via the elements' `==`).
    idiom_id: rf-dart-list-equality-to-xunit-assertequal-collection
    research_finding_id: rf-dart-list-equality-to-xunit-assertequal-collection
    nuance: >-
      Argument-order footgun (explicitly addressed — well-known,
      IDENTICAL to boot_loader_test.dart.md and global_send_test.dart.md):
      Dart `expect(actual, equals(expected))` is actual-first; xUnit
      `Assert.Equal<T>(T expected, T actual)` is expected-first.
      Codegen MUST swap; sibling specs pre-flagged this for batch
      reuse. Collection-equality nuance (explicitly addressed): Dart
      `equals` over a `List` does element-wise comparison via the
      elements' `==`; xUnit
      `Assert.Equal(IEnumerable, IEnumerable)` uses the default
      `IEqualityComparer<T>` (which for `string` falls through to
      `string.Equals(string)` = case-sensitive ordinal). Order-
      sensitivity matches in both languages (sequence-equality).
      Bare-list-as-matcher nuance (explicitly addressed, NEW for
      THIS file relative to global_send_test.dart.md which uses
      explicit `equals(...)` wrappers): the Dart source DOES NOT
      wrap with `equals(...)` — it relies on the implicit-equals
      sugar (Dart `package:matcher` `expect` auto-wraps non-matcher
      values). The implicit-vs-explicit form is semantically
      identical and the C# target is the same (`Assert.Equal`).
      List-literal element type inference nuance: `['hello']`,
      `['msg(...)']`, `['[a, b, c]']`, `['hello', 'world']` etc. are
      all `List<String>` literals (inferred from the literal
      element types — all are Dart `String`); C# equivalent is
      `new List<string> { ... }` (collection-initializer on
      `System.Collections.Generic.List<T>`). The single-string
      element `'msg(alice, bob, text(hi))'` and `'[a, b, c]'` contain
      parentheses and brackets but no quote characters — codegen
      preserves them verbatim inside the C# double-quoted string.
      GLP-printing-format nuance (LOAD-BEARING for the
      `prints a list` test): the C# expectation
      `new List<string> { "[a, b, c]" }` is the EXACT BYTE STRING the
      GLP `_output` kernel produces when invoked on a list term —
      it is the SUT-side printer's output, which the conversion
      preserves verbatim because the entire SUT pipeline is being
      converted byte-for-byte. The `[a, b, c]` format is the GLP
      printer's choice (NOT `[a,b,c]`, NOT `(a, b, c)`); the conversion
      relies on the GLP-printer's C# equivalent producing the
      identical formatted string — a CROSS-FILE INVARIANT recorded
      here.
conversion_units:
  - cu-1: file-scope using directives — `using Xunit;`, `using System.IO;` (for `Path.GetFullPath`), `using System.Collections.Generic;` (for `List<string>`), `using System.Threading.Tasks;` (for `Task`), `using <RootNs>.Engine;` (for `GlpEngine` + transitive `GlpRuntime.OutputCallback` + `ExecutionResult`) plus possibly `using <RootNs>.Runtime;` depending on SUT placement
  - cu-2: namespace declaration mirroring the test/multiagent path (e.g. `<RootNs>.Test.Multiagent`)
  - cu-3: public class `OutputKernelTests` (from outer group label `'_output kernel'` — underscore stripped + PascalCase) with `[Trait("Group", "_output kernel")]`; contains two private fields (`_engine`, `_outputLines`), one constructor (the setUp body — Path.GetFullPath + new GlpEngine + StrictTypes assignment + new List + OutputCallback lambda assignment), and three `[Fact(DisplayName = "...")]` `public async Task` methods (`PrintsAConstant`, `PrintsAStruct`, `PrintsAList`)
  - cu-4: public class `SendToUserTests` (from outer group label `'send_to_user'` — PascalCased) with `[Trait("Group", "send_to_user")]`; contains two private fields (`_engine`, `_outputLines`), one constructor (textually identical setUp body), and two `[Fact(DisplayName = "...")]` `public async Task` methods (`ConsumesAGroundStreamAndPrintsEachTerm`, `WaitsForStreamElementsToBecomeGround`)
  - cu-5: raw-string-literal payloads (`""" ... """`) for every embedded `.glp` source fixture inside each test method, emitted at column 0 to preserve the leading-newline/indentation byte-identically; all 5 test methods carry exactly ONE fixture string
  - cu-6: per-method body shape — `LoadSource` call (return discarded), `var result = await _engine.RunGoalAsync("test");`, `Assert.True(result.Succeeded);`, `Assert.Equal(new List<string> { ... }, _outputLines);`
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative, FR-012 cached idiom)

This file is the Nth `package:test` file specced in the inventory; the
batch was pinned to xUnit at the first multiagent test
(`test/multiagent/mad_error_handling_test.dart.md`) and has been
reused verbatim by every subsequent test convspec. Maintaining that
pin satisfies SC-007 (consistency via recorded idiom, not
re-derivation). The authoritative basis is xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for the
`[Fact]` / `[Trait]` / constructor-as-setUp model + `async Task` test
shape, and the Dart `package:test` README on `pub.dev`
(`https://pub.dev/packages/test`) for the `group` / `setUp` /
`expect` / matcher semantics. NUnit and MSTest remain corroborating
alternatives, recorded at the import-idiom level — not re-derived
per file.

### Two sibling groups → two sibling classes (carry-forward + setUp adaptation)

THIS file extends global_send_test.dart.md's two-sibling-groups
pattern (two classes per file) by adding a `setUp` in each group —
identical to boot_loader_test.dart.md's outer-setUp pattern. The two
patterns compose: each group becomes a class WITH a constructor body
mirroring its `setUp`. Both classes happen to have textually-identical
constructor bodies (because both groups initialise the same
`engine`/`outputLines` field pair the same way), but no refactor to
a shared base class is needed — the duplication is benign for two
small constructors, and the more-faithful translation duplicates the
body verbatim. Recorded as an optional forward refactoring note;
NOT a conversion decision.

### `_output kernel` group-label-to-class-name mangling (load-bearing nuance)

The outer group label `'_output kernel'` carries the LEADING
UNDERSCORE deliberately — `_output` is the GLP runtime kernel
predicate name (per `glp_runtime/lib/runtime/body_kernels.dart` and
its convspec `lib/runtime/body_kernels.dart.md`). C# class-naming
convention (Microsoft Framework Design Guidelines) requires PascalCase
and disallows leading underscores on public types (because
underscore-prefix is the convention for PRIVATE fields, per the
Roslyn rule IDE1006). Codegen strips the leading underscore in the
class name (`OutputKernelTests`) but PRESERVES the original label
verbatim inside `[Trait("Group", "_output kernel")]` so the
GLP-kernel-name semantic survives at the reporter layer. The
underscore-stripping rule is recorded in
`rf-dart-package-test-group-to-xunit-class` as a name-mangling sub-rule.

### `..strictTypes = false` cascade → unrolled statement (FR-024 + footgun nuance)

The Dart cascade operator `..` returns the RECEIVER, not the called
member's value (Dart spec
`https://dart.dev/language/operators#cascade-notation`). C# has no
cascade. The faithful translation UNROLLS the cascade to two
statements (`var engine = new ...; engine.Property = ...;`) rather
than chain-assignment tricks. An object-initializer form
(`new GlpEngine(<path>) { StrictTypes = false }`) is recorded as an
acceptable alternative for property-only cascades; codegen MAY use
it. The footgun (mis-binding the assignment result rather than the
receiver) is recorded as a nuance — a sibling spec (any file with a
cascade containing a method call) would have to fall back to the
unroll form.

### `engine.runtime.outputCallback` → `_engine.Runtime.OutputCallback` + lambda

Cached idiom reuse from project_linker_test.dart.md
(`rf-dart-camelcase-field-to-csharp-pascalcase-property` +
`rf-dart-arrow-lambda-to-csharp-lambda` +
`rf-dart-void-function-question-to-csharp-action-nullable`). The
load-bearing observation is that the callback is the test's CORE
observation mechanism — the entire test suite relies on the
`_output` kernel (per body_kernels.dart.md) calling the assigned
callback at every `_output(X)` invocation. The C# delegate
`Action<string>?` (Microsoft Learn
`https://learn.microsoft.com/dotnet/api/system.action-1`) is
semantically equivalent to Dart `void Function(String)?` — both
nullable, both single-arg-void-return. The lambda
`line => _outputLines.Add(line)` captures `_outputLines` from
`this` (the test class instance); per-test-fresh-instance ensures
each test's callback writes to a NEW `List<string>`, never the
previous test's list.

### Async tests → `async Task` `[Fact]` (NEW for this batch)

Every test in this file uses `() async { ... }` with
`await engine.runGoal('test')`. The target shape is
`public async Task <Name>() { ... }` — NOT `public void` (which
would block on the Task synchronously, defeating the entire async
pipeline) and NOT `public Task` without `async` (which would lose
the post-await assertions). xUnit's discovery + execution pipeline
treats `async Task`-returning `[Fact]` methods identically to sync
`void`-returning ones at the user-visible level (per
`https://xunit.net/docs/comparisons#assertions` and Microsoft Learn
`https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap`).
The `-Async` suffix on `RunGoalAsync` (vs Dart's bare `runGoal`) is
the Microsoft Framework Design Guidelines convention pinned by the
SUT spec `lib/engine/glp_engine.dart.md` — call sites reflect this
rename. xUnit does NOT capture a SynchronizationContext by default,
so `ConfigureAwait(false)` is NOT required.

### `File('../programs/self.glp').absolute.path` → `Path.GetFullPath("...")` (carve-out from FileInfo)

Two C# alternatives exist for absolute-path resolution: (i)
`Path.GetFullPath(string)` — pure static, returns the string
immediately; (ii) `new FileInfo(path).FullName` — constructs a
`FileInfo` object observing its `FullName` property. Both are
authoritative on Microsoft Learn
(`https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath`
and `https://learn.microsoft.com/dotnet/api/system.io.fileinfo.fullname`).
The carve-out rule: choose (i) when the Dart code does NOT retain
the `File` object (one-shot path resolution); choose (ii) when the
Dart code retains the `File` object for subsequent operations.
THIS file uses (i) because the `File(...)` is immediately
`.absolute.path`-resolved and discarded; the project_linker_test
sibling uses (ii) because it retains the `File` object across
`.existsSync()` + `.absolute.path`. The two-rule carve-out is
recorded in the research-finding row as a forward-looking idiom
sub-rule. Path-separator and CWD-sensitivity nuances are
identical on both sides.

### Triple-quoted GLP fixtures → C# 11 raw strings (cached idiom)

Cached from boot_loader_test.dart.md. C# 11 raw string literals
(`""" ... """`) preserve newlines + leading whitespace identically
to Dart triple-quoted strings (Microsoft Learn
`https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`).
Codegen MUST emit the closing `"""` at column 0 so the literal
payload is byte-identical — mis-indented closing delimiters
silently change the parsed input. The leading-newline rule
(if-the-sequence-starts-with-a-newline-it-is-ignored) is documented
on both sides and ensures byte-exact equivalence of the embedded
GLP source.

### Argument-order flip on `Assert.Equal`

Cached from boot_loader_test.dart.md and global_send_test.dart.md.
The Dart `expect(actual, equals(expected))` convention puts actual
first; xUnit `Assert.Equal(expected, actual)` puts expected first.
Every `expect(outputLines, [...])` in this file MUST be flipped at
the boundary. The implicit-equals sugar (bare list literal as
second arg) is semantically identical to explicit `equals(...)`
and produces the same C# target shape.

### List-equality observation mechanism (test core invariant)

The five `expect(outputLines, [...])` assertions are the CORE
observation mechanism of the test file — they check that the
`_output` kernel + the `send_to_user` GLP predicate together
produce the expected sequence of formatted strings on the
`outputCallback` channel. The C# expectation lists (e.g.
`new List<string> { "[a, b, c]" }`) are the EXACT BYTE STRINGS the
GLP printer produces — preserving them verbatim relies on the
GLP-printer's C# equivalent producing the identical formatted
string, which is a CROSS-FILE INVARIANT (the GLP printer is in
`compiler/glp_printer.dart` and is converted separately, with its
own convspec). Recorded as a forward-looking cross-file
dependency note.

### Async/Stream/Completer/isolate surface analysis (US2 AS4)

Per the deep-analysis discipline (US2 AS4 — well-known nuances MUST
be explicitly addressed), this file's async/Stream/Completer/isolate
posture is:

- **`async`/`Future`**: PRESENT — every test body is `async`, every
  `await engine.runGoal('test')` returns `Future<ExecutionResult>`.
  Maps to `async Task` + `Task<ExecutionResult>` per cached idiom.
  Addressed at dart.package_test.test_call_async +
  dart.expression.final_local_variable_with_initializer_await.
- **`Stream`/`IAsyncEnumerable`**: ABSENT at the .dart source surface
  (the test file does NOT use Dart `Stream` at all). HOWEVER the
  GLP-level "stream" concept (the `send_to_user` predicate's
  `[T | In]` list-as-stream consumption pattern) is a LOGIC-LANGUAGE
  stream, NOT a Dart-runtime stream — the conversion of the GLP
  `send_to_user` predicate is a GLP-source-level transformation,
  NOT a Dart-to-C# conversion. The test file embeds the GLP
  `send_to_user` definition INLINE as a triple-quoted string fixture
  (per the test body comments: "send_to_user is embedded in mad
  predicates, so we provide it inline") — that fixture is preserved
  byte-for-byte in the C# raw-string literal and consumed by the
  converted GLP engine at runtime. NO Dart `Stream` -> C#
  `IAsyncEnumerable` conversion applies. Explicitly addressed at
  dart.package_test.import_directive (no `dart:async` imported, no
  `Stream` type referenced).
- **`Completer`**: ABSENT — no Dart `Completer<T>` in this file. The
  `await engine.runGoal('test')` flows through the SUT's internal
  `Completer`-backed async pipeline (per
  `lib/engine/glp_engine.dart.md` + `lib/runtime/scheduler.dart.md`)
  but that conversion is the SUT-spec's concern; the test file's
  surface has no `Completer`.
- **`isolate`**: ABSENT at the source level. THIS file does NOT
  construct `IsolateManager` instances or invoke isolate-related
  APIs. The threading model nuance (from
  `runtime/heap_fcp.dart`'s escalation, INHERITED per FR-013 + the
  multiagent precedent) applies transitively at the SUT level but
  not at this test file's surface — the test file calls
  `_engine.RunGoalAsync(...)` from a single thread and awaits the
  result on the same thread (xUnit's default no-context model).
  Explicitly addressed as no isolate-construct in this file;
  inherited escalation does not require re-escalation here.
- **`closure-vs-identity-vs-value`**: ADDRESSED at every relevant
  construct. The arrow-lambda `(line) => outputLines.add(line)` is
  a CLOSURE OVER A FIELD (not over a stack-local), so per-test-class-
  instance the captured reference points at the freshly-created
  `_outputLines` list — identical on both sides. No identity-vs-
  value distinction is observable in the test surface (the only
  reference comparisons are list-element string comparisons, which
  are value-equal on both sides via `String.==` / `string.Equals`).

### Why no escalations

Every construct in this file has a clear single-decision target
shape grounded in official documentation for both Dart `package:test`,
`dart:io` (`Path` resolution), and xUnit/.NET (`async Task`,
`Assert.Equal`, `Action<T>`). The async-test-shape decision
(`public async Task` vs `public void`) is the only newly-recorded
idiom for this batch (`rf-dart-test-callback-async-to-xunit-async-task-method`);
it carries forward from the SUT spec `lib/engine/glp_engine.dart.md`'s
`rf-dart-future-async-await-to-csharp-task-async-await` cached
idiom. The cascade-unrolling decision is well-documented and the
object-initializer alternative is recorded for completeness. The
inherited `runtime/heap_fcp.dart` threading-model escalation applies
transitively at the SUT level but does NOT affect this test file's
surface (no isolate construction, no thread-affinity observation),
so per FR-013 + the multiagent-test precedent it is INHERITED
without re-escalation. `escalations: []` is therefore intentional,
not a placeholder.
