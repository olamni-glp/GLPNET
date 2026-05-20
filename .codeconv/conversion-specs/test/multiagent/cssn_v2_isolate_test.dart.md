# Conversion Spec — test/multiagent/cssn_v2_isolate_test.dart

> Conversion-spec artifact for test/multiagent/cssn_v2_isolate_test.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.
>
> **Driver-style integration test.** Unlike `mad_cold_call_isolate_test`
> which builds isolate entrypoints + a message router IN-FILE, this file
> is a thin DRIVER that hands every threading concern to two SUTs
> (`IsolateManager` + `BootLoader`) and exercises 13 named plays
> (`mad_fplay1..13.glp`) by file-name iteration over a `setUp` / `tearDown`
> lifecycle. The threading-model question (`SendPort` /
> `ReceivePort` / `Isolate.spawn` etc.) is OWNED by
> `lib/multiagent/isolate_manager.dart.md` (which itself escalates to
> `lib/runtime/heap_fcp.dart.md`). Per FR-013's
> "don't double-escalate" discipline (and the precedent set by
> `mad_cold_call_isolate_test.dart.md`), THIS file's spec INHERITS those
> escalations and does NOT re-open them — it is a CONSUMER of the
> isolate-manager API surface, not a co-decider of the threading
> primitive.

```yaml
schema_version: 1
source_path: test/multiagent/cssn_v2_isolate_test.dart
source_sha256: ced133bbafaf1744fb59e6375e58cd5b7d825e2998c2cdf473a9cf2443b6c23d
target_code_unit: test/multiagent/CssnV2IsolateTest.cs
constructs:
  - construct_key: dart.import.dart_io_library
    source_form: "import 'dart:io';"
    target_decision: >-
      Drop the Dart `import 'dart:io';` directive. The single load-bearing
      symbol from `dart:io` used in this file is the `File` class
      (`File('$_madBootDir/$bootFilename')`, `.existsSync()`,
      `.readAsStringSync()`, `.path`). Replace at first use with the
      canonical .NET equivalents from `System.IO`: `File`
      (`System.IO.File`, https://learn.microsoft.com/dotnet/api/system.io.file)
      with `File.Exists(path)` and `File.ReadAllText(path)`. Codegen MUST
      add `using System.IO;` at file scope. The Dart `File(path)` ctor
      pattern (instance-then-call) maps to C#'s STATIC `File.Exists`/
      `File.ReadAllText` calls (which take the path string directly — there
      is no equivalent `FileInfo`-instance per-line shape needed here, and
      `FileInfo` would be unidiomatic for a one-shot read+exists check).
    idiom_id: null
    research_finding_id: rf-dart-dart-io-file-to-dotnet-system-io-file
    nuance: >-
      Standard-library nuance (explicitly addressed): Dart `dart:io`'s
      `File` (https://api.dart.dev/stable/dart-io/File-class.html) is an
      INSTANCE-style API — `File(path)` constructs a handle, then
      `.existsSync()` / `.readAsStringSync()` operate on it. .NET's
      `System.IO.File` is a STATIC API — `File.Exists(path)` /
      `File.ReadAllText(path)` take the path each call. Semantics agree
      for this file's use (one-shot existence check + one-shot string
      read). Encoding nuance: Dart `readAsStringSync()` defaults to
      UTF-8; .NET `File.ReadAllText(path)` ALSO defaults to UTF-8
      (https://learn.microsoft.com/dotnet/api/system.io.file.readalltext)
      — encodings match. Path-separator nuance: the Dart source uses
      forward slashes (`'$_cssnV2Dir/mad_boot'`, `'../programs/...'`);
      .NET's `File.*` accepts forward slashes on both Windows and Unix at
      runtime (the underlying Win32 layer normalises). The constants are
      preserved verbatim; if cross-platform separator handling becomes
      load-bearing for a future test, `Path.Combine` would be the
      idiomatic substitute — recorded but NOT applied here. Async-vs-sync
      nuance: the Dart source uses the SYNC variants (`existsSync`,
      `readAsStringSync`); the C# port preserves the sync shape (`File.
      Exists` / `File.ReadAllText` are sync). The async variants
      (`File.ReadAllTextAsync`) are NOT used here — they would require
      making `_RunPlay` `async` already-async-anyway, and would change the
      observable call signature; the faithful translation stays sync.
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` and replace with
      `using Xunit;` at file scope. xUnit is the project-wide pinned test
      framework (KB hit — every prior test-file convspec under
      `.codeconv/conversion-specs/test/`); REUSE verbatim (FR-012 /
      SC-007), no re-research. Codegen MUST also add `using System;`
      (`TimeSpan`, `Action`, `Exception`), `using System.Threading;`
      (`CancellationTokenSource`, `Thread.Sleep` for the `Future.delayed`
      mapping), `using System.Threading.Tasks;` (`Task`, `Task.Delay`),
      `using System.IO;` (see dart_io_library above), and the SUT
      `using <RootNs>.Multiagent;` (see package_under_test_import below).
      Target namespace mirrors the Dart `test/multiagent` path (e.g.
      `<RootNs>.Test.Multiagent`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework-selection nuance (cache hit from
      `mad_error_handling_test.dart.md`, `boot_loader_test.dart.md`,
      `mad_cold_call_isolate_test.dart.md`): xUnit is the SINGLE project-
      wide test-framework binding; every `package:test` file uses the
      same xUnit mapping for `[Fact]`/`[Trait]`/test-class-per-group/
      constructor-per-test. NUnit / MSTest recorded in the research
      finding as corroborating alternatives — not used. This file's tests
      are GENERATED in a for-loop over `[1..13]`-style integer ranges
      (see dart.package_test.for_loop_generated_tests below): the xUnit
      counterpart is `[Theory]` + `[InlineData]` parameterised tests
      (NOT `[Fact]` per iteration — would inflate to 13 near-identical
      method bodies). Both `[Fact]` and `[Theory]` are covered by the
      cached idiom; this file exercises BOTH (one `[Fact]` for fplay8,
      fplay11, fplay12, fplay13; one `[Theory]` per range-group for
      fplay1..3, fplay4..7, fplay9..10).
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/multiagent/boot_loader.dart';
       import 'package:glp_runtime/multiagent/isolate_manager.dart';"
    target_decision: >-
      Both imports are SUT (system-under-test) references that resolve to
      the converted C# namespaces of `lib/multiagent/boot_loader.dart` and
      `lib/multiagent/isolate_manager.dart`. Replace with a SINGLE `using
      <RootNs>.Multiagent;` directive at file scope (both SUT files emit
      into the same `Multiagent` namespace per the per-SUT-file convspecs
      `.codeconv/conversion-specs/lib/multiagent/boot_loader.dart.md` and
      `.../lib/multiagent/isolate_manager.dart.md`). Symbols pulled
      through this `using`: `BootLoader` (instance class), `BootConfig`
      (return type of `BootLoader.Load(string)` per boot_loader SUT spec
      — with `ProjectDir`/`RootSelfGlpPath` settable string properties),
      `IsolateManager` (instance class with `Boot`/`Start`/`Shutdown`
      members — concrete shape depends on the isolate_manager SUT
      escalation resolution), `TraceConfig` (value record / class with
      `Glp`, `Mad` boolean fields per isolate_manager SUT spec). The
      two-namespace-collapse pattern from `mad_scenarios_test.dart.md`
      applies here trivially — there is only ONE namespace.
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (cache hit): `package:glp_runtime/...`
      is an in-repo pubspec-anchored URI; resolves to the SUT's converted
      C# namespace, NOT to a third-party NuGet package. Project-file
      wiring (`<ProjectReference>` from the test .csproj to the runtime
      .csproj) is langpair/project-skeleton level — recorded for the
      codegen project-system layer, not in this single-file spec. The
      `boot_loader.dart` import provides `BootLoader` + its return type;
      the `isolate_manager.dart` import provides `IsolateManager` +
      `TraceConfig` AND the message-type hierarchy (`IsolateMessage`,
      `NetworkMsg`, `Ready`, `GlobalNamesMsg`, `Start`, `Done`, `UIEvent`)
      — though THIS test file only references `IsolateManager` and
      `TraceConfig` directly; the message classes flow through the
      isolate-manager's internal use.
  - construct_key: dart.toplevel_const_string_constant
    source_form: >-
      "const _cssnV2Dir = '../programs/cssn_modules_v2';
       const _madBootDir = '$_cssnV2Dir/mad_boot';
       const _rootSelfGlp = '../programs/self.glp';"
    target_decision: >-
      Three Dart top-level `const String` declarations with leading-
      underscore (library-private) names. Map to `private const string`
      class-level fields on the enclosing test class — the leading
      underscore convention is replaced by C#'s `private` access modifier
      (the convention idiom `rf-dart-leading-underscore-private-to-csharp-
      private`, cache hit across every prior convspec). Specifically:
      `private const string _CssnV2Dir = "../programs/cssn_modules_v2";`,
      `private const string _MadBootDir = _CssnV2Dir + "/mad_boot";`
      (Dart string interpolation `'$_cssnV2Dir/mad_boot'` ⇒ C# string
      concatenation `_CssnV2Dir + "/mad_boot"` because C# `const` fields
      MUST be initialised with a compile-time constant expression — and
      `string.Format("{0}/mad_boot", _CssnV2Dir)` is NOT a compile-time
      constant, but `string.Concat`/`+` over two string consts IS, per
      https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-
      types/built-in-types#built-in-value-types). Alternative: `static
      readonly string` if codegen wants to use `string.Format` / `Path.
      Combine` — but for verbatim faithfulness `const` + `+` is the
      pinned choice. `private const string _RootSelfGlp =
      "../programs/self.glp";`. Name-mangling: Dart's leading-underscore
      identifier `_cssnV2Dir` becomes PascalCased `_CssnV2Dir` (preserve
      the leading underscore to mark privacy AND PascalCase the remainder
      per the C# private-field naming idiom — note the project-wide
      convention recorded in prior specs uses leading underscore +
      PascalCase for private fields; `_cssnV2Dir`-style camelCase
      is non-idiomatic in C#).
    idiom_id: rf-dart-toplevel-const-private-to-csharp-private-const-field
    research_finding_id: rf-dart-toplevel-const-private-to-csharp-private-const-field
    nuance: >-
      Top-level-vs-class-member nuance (explicitly addressed): Dart allows
      LIBRARY-TOP-LEVEL `const` declarations; C# does NOT — every C#
      identifier MUST live in a type. The faithful target is class-level
      `private const string` on the test class (or `internal static class`
      container if shared across multiple test files — NOT applicable here,
      they are file-private). String-interpolation-in-const nuance
      (LOAD-BEARING): Dart `'$_cssnV2Dir/mad_boot'` is a string-
      interpolation expression that — because `_cssnV2Dir` is itself
      `const` — evaluates to a constant `'../programs/cssn_modules_v2/
      mad_boot'` at compile time, so `_madBootDir` is itself `const`. The
      C# counterpart is `_CssnV2Dir + "/mad_boot"` — the `+` operator over
      two const-string operands IS a compile-time constant expression
      (https://learn.microsoft.com/dotnet/csharp/language-reference/
      operators/addition-operator#string-concatenation). Codegen MUST NOT
      emit `$"{ _CssnV2Dir}/mad_boot"` (C# interpolated string) because
      that is NOT a compile-time constant in C# 9 and earlier; C# 10+ does
      allow it for string-typed `const` with `const-interpolated-string-
      literals` IF every interpolated value is itself `const string` (per
      https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-10#constant-
      interpolated-strings) — codegen MAY emit the interpolated form ONLY
      when targeting C# 10+. The baseline emission is the `+` operator
      form which compiles on every supported language version. Path-
      separator nuance: the literals contain forward slashes — preserved
      verbatim; the .NET File API normalises at runtime.
  - construct_key: dart.toplevel_function.async_helper_with_named_default_param
    source_form: >-
      "Future<void> _runPlay(IsolateManager manager, String bootFilename,
       {int timeoutSec = 10}) async { ... }"
    target_decision: >-
      Top-level `Future<void>`-returning async function `_runPlay` with
      ONE positional `IsolateManager` parameter, ONE positional `String`
      parameter, and ONE NAMED optional `int` parameter with default
      value `10`. Map to a `private static async Task _RunPlayAsync(
      IsolateManager manager, string bootFilename, int timeoutSec = 10)`
      method on the enclosing test class (file-private + static — matches
      the Dart top-level-helper shape). The cached idiom
      `rf-dart-future-void-async-to-csharp-task-async` from
      `mad_cold_call_isolate_test.dart.md` pins
      `Future<void> async` ⇒ `async Task` (NOT `async void` — see nuance
      below). The named-default parameter maps directly: C# supports
      OPTIONAL POSITIONAL parameters with defaults
      (`int timeoutSec = 10`); the Dart call site `_runPlay(manager,
      'mad_fplay13.glp', timeoutSec: 15)` becomes
      `_RunPlayAsync(manager, "mad_fplay13.glp", timeoutSec: 15)` — C#
      named-argument syntax at the call site is `paramName: value`
      (https://learn.microsoft.com/dotnet/csharp/programs/named-and-
      optional-arguments), IDENTICAL to Dart. Codegen MUST suffix the
      method name with `Async` per the .NET design guideline
      (https://learn.microsoft.com/dotnet/standard/asynchronous-
      programming-patterns/task-based-asynchronous-pattern-tap#naming-
      parameters-and-return-types) — Dart has no such convention.
    idiom_id: rf-dart-future-void-async-to-csharp-task-async
    research_finding_id: rf-dart-future-void-async-to-csharp-task-async
    nuance: >-
      Async-method-return-type nuance (cache hit, restated): Dart
      `Future<void>` ⇒ C# `Task`; NEVER `async void` (which swallows
      exceptions silently and cannot be awaited per
      https://learn.microsoft.com/dotnet/csharp/language-reference/
      operators/async). Naming-convention nuance: .NET's TAP guideline
      mandates the `Async` suffix; codegen renames `_runPlay` ⇒
      `_RunPlayAsync`. Named-vs-positional-default-param nuance
      (explicitly addressed): Dart named-parameter `{int timeoutSec = 10}`
      and C# optional-positional-parameter `int timeoutSec = 10` are
      semantically very similar BUT NOT IDENTICAL. In Dart, named
      parameters are passed BY NAME at the call site (`timeoutSec: 15`)
      and the call site MAY omit them (default applied); in C#, optional
      parameters MAY be passed by position OR by name. The C# call site
      `_RunPlayAsync(manager, "mad_fplay13.glp", timeoutSec: 15)`
      preserves the Dart by-name pass at the call site for clarity. NOT
      `out` parameter, NOT `params`. Static-vs-instance-method nuance:
      `_runPlay` is a Dart top-level helper (no enclosing class); C# has
      no top-level functions outside `top-level statements` in `Program.cs`
      (https://learn.microsoft.com/dotnet/csharp/fundamentals/program-
      structure/top-level-statements). The faithful translation is a
      `private static` method on the test class (or on a `file`-scoped
      static helper class IF the helper is shared with other files —
      NOT the case here, file-private static is correct). Closure-
      capture nuance: `_runPlay` captures the file-scope `_madBootDir` /
      `_rootSelfGlp` / `_cssnV2Dir` constants; in C# those constants
      ARE class-scope `private const string` fields, accessible to the
      enclosing class's static method without explicit qualification.
  - construct_key: dart.expression.file_construct_existsSync_readAsStringSync
    source_form: >-
      "final bootFile = File('$_madBootDir/$bootFilename');
       if (!bootFile.existsSync()) { print('Skipping: ...'); return; }
       final bootSource = bootFile.readAsStringSync();"
    target_decision: >-
      Map the Dart `File(path).existsSync()` / `File(path).readAsStringSync()`
      idiom to the C# STATIC `System.IO.File.Exists(path)` /
      `System.IO.File.ReadAllText(path)` pair. The local `bootFile`
      variable becomes a `string bootFilePath` local (storing the
      concatenated path) — there is no per-instance `File` handle in the
      .NET API for this shape. Specifically: `final bootFile =
      File('$_madBootDir/$bootFilename');` ⇒ `var bootFilePath =
      _MadBootDir + "/" + bootFilename;` (string concatenation; preserves
      the Dart-interpolation semantics — alternative `Path.Combine(
      _MadBootDir, bootFilename)` is the .NET-idiomatic form but the
      forward-slash literal is preserved verbatim for faithfulness);
      `if (!bootFile.existsSync()) { print('Skipping: ${bootFile.path} not
      found'); return; }` ⇒ `if (!File.Exists(bootFilePath)) {
      Console.WriteLine("Skipping: " + bootFilePath + " not found");
      return; }`; `final bootSource = bootFile.readAsStringSync();` ⇒
      `var bootSource = File.ReadAllText(bootFilePath);`. The `bootFile.
      path` getter on Dart's `File` (which returns the constructor-arg
      string) is replaced by the `bootFilePath` local variable directly
      (it IS the string).
    idiom_id: null
    research_finding_id: rf-dart-dart-io-file-to-dotnet-system-io-file
    nuance: >-
      Instance-vs-static-API nuance (LOAD-BEARING, explicitly addressed):
      Dart `File(path)` ALLOCATES an instance object with a `.path`
      getter; .NET `System.IO.File` is a static class with no instance
      counterpart for this minimal use. The conversion DISCARDS the
      instance and folds the path-string variable directly. If a future
      test exercises richer `File`-instance behaviour (`length`, `lastModified`,
      `openRead`), the .NET counterpart is `FileInfo` (the instance-
      style API, https://learn.microsoft.com/dotnet/api/system.io.fileinfo)
      — recorded for forward-compat. Skip-on-missing-file nuance
      (explicitly addressed): the Dart pattern PRINTS a diagnostic and
      RETURNS EARLY when the boot file is missing — graceful test-skip.
      The xUnit-idiomatic counterpart is `Assert.True(File.Exists(
      bootFilePath), $"Skipping: {bootFilePath} not found");` — but that
      would FAIL the test, not skip it. The faithful translation matches
      the Dart return-early shape: `if (!File.Exists(bootFilePath)) {
      Console.WriteLine("Skipping: " + bootFilePath + " not found");
      return; }`. ALTERNATIVE: `Skip.IfNot(File.Exists(bootFilePath),
      "...")` (xUnit.Skip extension, third-party — NOT used in the
      project per project-wide test-framework decision), OR throw
      `Xunit.SkipException` (xUnit v3+, https://xunit.net/docs/getting-
      started/v3/skipping-tests) — recorded as the xUnit-native skip
      shape; codegen MAY emit it under xUnit v3 to surface the skip in
      the test report rather than silently passing. The baseline
      emission preserves the Dart return-early behaviour with a
      `Console.WriteLine` diagnostic.
  - construct_key: dart.expression.print_diagnostic_log
    source_form: "print('Skipping: ${bootFile.path} not found');"
    target_decision: >-
      Map the Dart `print(...)` to `System.Console.WriteLine(...)`. Cache
      hit on `rf-dart-print-and-terminate-to-csharp-equivalent` from
      `lib/bytecode/runner.dart.md` (and reused in
      `mad_cold_call_isolate_test.dart.md`). The interpolated argument
      `'Skipping: ${bootFile.path} not found'` becomes the C# string
      concatenation `"Skipping: " + bootFilePath + " not found"` (using
      the local-variable path replacement from the preceding construct).
      Codegen MAY alternatively emit a C# interpolated string
      `$"Skipping: {bootFilePath} not found"` — semantics identical
      (https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/
      interpolated). Per-test-output-capture nuance see below.
    idiom_id: rf-dart-print-and-terminate-to-csharp-equivalent
    research_finding_id: rf-dart-print-and-terminate-to-csharp-equivalent
    nuance: >-
      Diagnostic-output-routing nuance (explicitly addressed, IDENTICAL
      to `mad_cold_call_isolate_test.dart.md`): Dart `print` writes to the
      process stdout from any isolate; .NET `Console.WriteLine` writes to
      the process stdout from any thread. Both are observably similar for
      a multi-isolate test. xUnit-test-output-capture alternative:
      `ITestOutputHelper.WriteLine(...)` is the xUnit-idiomatic way to
      route per-test diagnostics into the runner's per-test report;
      requires constructor injection (`public CssnV2IsolateTests(
      ITestOutputHelper output)`) and a `private readonly
      ITestOutputHelper _output` field. xUnit v2+ on .NET Core/5+ does
      NOT capture `Console.WriteLine` per-test (it goes to the runner's
      shared stdout), so `ITestOutputHelper` is the recommended shape for
      this project — codegen MAY emit ITestOutputHelper as the preferred
      form, with `Console.WriteLine` as the simple-faithful baseline.
      The recorded baseline emission is `Console.WriteLine` for cross-
      thread safety (this file's `_RunPlay` is invoked from a test
      thread, BUT the agents spawned by `IsolateManager.Boot` print on
      their own threads, and `ITestOutputHelper` is NOT thread-safe per
      xUnit docs — RHEL with `Console.WriteLine` is uniform-safe across
      the test thread and any spawned agent threads).
  - construct_key: dart.sut.boot_loader_load_with_settable_config
    source_form: >-
      "final loader = BootLoader();
       final config = loader.load(bootSource);
       config.projectDir = _cssnV2Dir;
       config.rootSelfGlpPath = _rootSelfGlp;"
    target_decision: >-
      Four-statement SUT pipeline: construct a `BootLoader`, call its
      instance `load(String)` method which returns a `BootConfig` value,
      then SET two mutable properties on the returned config. Maps to
      `var loader = new BootLoader(); var config = loader.Load(bootSource);
      config.ProjectDir = _CssnV2Dir; config.RootSelfGlpPath = _RootSelfGlp;`.
      The SUT-side shape is decided by
      `.codeconv/conversion-specs/lib/multiagent/boot_loader.dart.md`
      (cache hit, FR-012 / SC-007 — no re-research): `BootLoader` is a
      stateless instance class with a `BootConfig Load(string source)`
      instance method (NOT a static factory, NOT an `IBootLoader`
      interface — pinned by the SUT spec); `BootConfig` is a mutable
      class with `public string? ProjectDir { get; set; }` and `public
      string RootSelfGlpPath { get; set; } = "";` settable
      auto-properties (per the boot_loader SUT spec — mutable settable
      properties because the Dart source mutates them post-construction).
    idiom_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    research_finding_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    nuance: >-
      Mutable-vs-immutable-config nuance (explicitly addressed):
      `BootConfig` is mutable in Dart (the `load()` result is mutated by
      assigning two fields after the call) — the SUT spec MUST preserve
      that mutability on the C# side via settable properties (NOT
      `init`-only auto-properties, which would forbid post-construction
      assignment). An ALTERNATIVE C# shape would be `BootConfig` as a
      `record` with a `with`-expression to create a copy (`config =
      config with { ProjectDir = _CssnV2Dir, RootSelfGlpPath =
      _RootSelfGlp };`) — recorded as an alternative in the SUT spec but
      NOT pinned (would require the call site to RE-ASSIGN `config`,
      which is a non-faithful semantic shift). The faithful mapping is
      mutable settable properties. PascalCase rename idiom (cache hit):
      `projectDir` ⇒ `ProjectDir`; `rootSelfGlpPath` ⇒ `RootSelfGlpPath`;
      `load(...)` ⇒ `Load(...)` — per the project-wide Dart-camelCase
      to C#-PascalCase rename idiom, applied to every SUT call site.
      Static-vs-instance-method nuance: the SUT spec pins `Load` as an
      INSTANCE method (matching the Dart `final loader = BootLoader();
      ... loader.load(...)` shape); a static `BootLoader.Load(string)`
      would also be faithful but loses the parity with the Dart instance
      construction.
  - construct_key: dart.sut.isolate_manager_boot_with_named_trace_config
    source_form: >-
      "await manager.boot(config, traceConfig: TraceConfig(glp: false,
       mad: false));"
    target_decision: >-
      Async SUT call with one POSITIONAL argument (`config`) and one NAMED
      argument (`traceConfig: TraceConfig(glp: false, mad: false)`). The
      SUT-side decision is owned by
      `.codeconv/conversion-specs/lib/multiagent/isolate_manager.dart.md`
      (escalation-inherited — see header). Under the per-SUT spec the
      C# signature is `public Task BootAsync(BootConfig config,
      TraceConfig? traceConfig = null)` (the Dart `TraceConfig.off` static
      const default folds into `null`-default per the SUT spec's
      treatment of `const off = TraceConfig()`) — and the call site
      becomes `await manager.BootAsync(config, traceConfig: new
      TraceConfig(glp: false, mad: false));`. The `TraceConfig` ctor is a
      Dart `const TraceConfig({this.glp = false, this.mad = false,
      this.agents})` with three named-optional parameters all defaulted
      ⇒ C# `public TraceConfig(bool glp = false, bool mad = false,
      ISet<string>? agents = null)` per the cached idiom
      `rf-dart-named-optional-ctor-with-defaults-to-csharp-positional-ctor-
      with-defaults` (the named-arg shape preserved at the call site).
      Per the SUT spec, `TraceConfig` is a value-type record / sealed
      class with all-readonly fields. NOTE: the call-site value
      `TraceConfig(glp: false, mad: false)` is observationally identical
      to the default `TraceConfig.off` — the test specifies it
      explicitly for clarity; codegen preserves the explicit construction.
    idiom_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    research_finding_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    nuance: >-
      Named-argument-preservation nuance (LOAD-BEARING): the Dart call
      site uses `traceConfig:` named argument; the C# call site preserves
      `traceConfig:` exactly per the cached idiom. Async-method-rename
      nuance: `boot` ⇒ `BootAsync` per .NET TAP guideline (the SUT spec
      MUST pin the `Async` suffix on every `Future`-returning method).
      `await` keyword is identical in both languages. Escalation-
      inheritance nuance (explicitly addressed, FR-013): the concrete
      shape of `IsolateManager.BootAsync` (does it spawn dedicated
      Threads? does it allocate `Channel<T>` mailboxes? does it use an
      actor library?) is OWNED by the isolate_manager SUT spec and the
      heap_fcp threading-model escalation; THIS file's spec does NOT
      re-decide that — it consumes the SUT spec's pinned signature.
      Default-value-on-SUT-side nuance: the SUT spec pins
      `TraceConfig? traceConfig = null` as the C# default (folding
      Dart's `TraceConfig.off`-static-const default into `null`). The
      method body on the SUT side substitutes `TraceConfig.Off` (a
      static readonly field) when `traceConfig` is null — preserving
      the Dart semantics.
  - construct_key: dart.sut.isolate_manager_start_void_method
    source_form: "manager.start();"
    target_decision: >-
      Synchronous void-returning instance call on the SUT. Maps to
      `manager.Start();` per the SUT spec
      `.codeconv/conversion-specs/lib/multiagent/isolate_manager.dart.md`
      (which pins `public void Start()` — note: NOT `async Task Start()`
      and NOT `Task StartAsync()` because the Dart method is `void`-
      returning and synchronously triggers an event-loop kick that
      returns immediately; the agents continue running on their own
      isolate event loops, not on the caller's microtask queue). The
      method-rename idiom applies (camelCase ⇒ PascalCase).
    idiom_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    research_finding_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    nuance: >-
      Sync-vs-async nuance (explicitly addressed): the Dart `Start()`
      returns `void` synchronously — it does NOT wait for the agents to
      finish (it only kicks them off). The C# port MUST preserve that
      shape — `void Start()` — NOT `async Task` (would change observable
      semantics: caller might `await` and expect completion). The
      isolate_manager SUT spec pins this synchronous return; the test
      file consumes it verbatim. Fire-and-forget nuance: the agents'
      execution continues on their own threads/tasks AFTER `Start()`
      returns; the test then `await Future.delayed(...)` to give them
      time to run (see next construct). The C# port preserves the same
      shape — `Start()` returns immediately; the test then awaits a
      `Task.Delay(...)` to allow agent execution.
  - construct_key: dart.async.future_delayed_duration_seconds
    source_form: "await Future.delayed(Duration(seconds: timeoutSec));"
    target_decision: >-
      Map to `await Task.Delay(TimeSpan.FromSeconds(timeoutSec));` per
      the cached idiom `rf-dart-future-delayed-to-csharp-task-delay`
      (recorded in prior multiagent specs). The `Duration(seconds: n)`
      ctor maps to `TimeSpan.FromSeconds(n)` per the cached idiom
      `rf-dart-duration-to-csharp-timespan` (`Duration` takes a named
      `seconds:` parameter; `TimeSpan` has a static factory method
      `FromSeconds(double)` — semantics agree; both store 100-ns
      precision). The async-cancellation nuance: `Task.Delay` accepts an
      optional `CancellationToken` argument
      (https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.
      delay) that allows the delay to be cancelled — Dart `Future.delayed`
      has no equivalent (Futures are uncancellable per
      https://api.dart.dev/stable/dart-async/Future-class.html). For
      faithful translation the C# port omits the token argument; if a
      future test needs cancellation-on-test-timeout, codegen MAY add it
      — recorded but not applied.
    idiom_id: rf-dart-future-delayed-to-csharp-task-delay
    research_finding_id: rf-dart-future-delayed-to-csharp-task-delay
    nuance: >-
      Delay-semantics nuance (explicitly addressed): Dart `Future.delayed(
      Duration(seconds: n))` returns a Future that completes after `n`
      seconds (https://api.dart.dev/stable/dart-async/Future/Future.delayed.
      html); .NET `Task.Delay(TimeSpan)` returns a Task that completes
      after the span (https://learn.microsoft.com/dotnet/api/system.threading.
      tasks.task.delay). Both use the runtime's scheduling primitive;
      neither blocks a thread (both async). Duration-vs-TimeSpan nuance
      (cache hit): `Duration(seconds: N)` ⇒ `TimeSpan.FromSeconds(N)`;
      `Duration(milliseconds: N)` ⇒ `TimeSpan.FromMilliseconds(N)`; etc.
      Both languages use 100-ns precision under the hood. Synchronous-
      `Thread.Sleep` alternative: REJECTED because `Thread.Sleep` blocks
      the calling thread, breaking the async-test contract — `Task.Delay`
      is the only faithful translation. Async-context nuance: the
      enclosing method must be `async Task` for `await Task.Delay` to be
      valid C# — already satisfied by the `_RunPlayAsync` mapping
      (preceding construct).
  - construct_key: dart.package_test.setUp_block
    source_form: "setUp(() { manager = IsolateManager(); });"
    target_decision: >-
      Dart `setUp` registered inside the `group` callback maps to the
      xUnit test class's CONSTRUCTOR body. Per the cached idiom
      `rf-dart-setUp-to-xunit-constructor` from `boot_loader_test.dart.md`,
      `setUp(() { manager = IsolateManager(); });` ⇒ `public
      CssnV2IsolateTests() { _manager = new IsolateManager(); }`. xUnit
      creates a FRESH test-class instance for every test method
      (https://xunit.net/docs/comparisons#per-test-isolation), so the
      constructor is the documented per-test-setup hook. The Dart `late
      IsolateManager manager;` field declaration becomes `private readonly
      IsolateManager _manager;` (note: `readonly` because it is assigned
      once in the constructor and never re-assigned per test — `late`'s
      "assigned-once-after-declaration" pattern maps naturally to
      `readonly` + constructor-assign).
    idiom_id: rf-dart-setUp-to-xunit-constructor
    research_finding_id: rf-dart-setUp-to-xunit-constructor
    nuance: >-
      Lifecycle nuance (cache hit, restated): `package:test`'s `setUp`
      runs BEFORE EVERY test; xUnit's constructor runs BEFORE EVERY test
      method on a fresh instance — semantics agree exactly. NUnit's
      `[SetUp]` attribute method would be an alternative ALSO faithful;
      MSTest's `[TestInitialize]` would be another — both recorded in
      the research finding as alternatives. Async-setUp nuance: NOT
      exercised here — the Dart `setUp` body is synchronous (just `manager
      = IsolateManager()`); the C# constructor body is also synchronous.
      If a future test had `setUp(() async { ... })`, the xUnit
      `IAsyncLifetime.InitializeAsync` pattern would apply (recorded).
      `late`-field nuance (explicitly addressed): Dart `late IsolateManager
      manager;` defers initialisation to runtime first-write OR throws if
      read before write — equivalent to C# `private IsolateManager _manager
      = null!;` (the `null!` is the null-forgiving operator
      https://learn.microsoft.com/dotnet/csharp/language-reference/
      operators/null-forgiving). BUT since `_manager` is assigned in the
      constructor BEFORE any test method runs, the more correct C# shape
      is `private readonly IsolateManager _manager;` (immutable after
      ctor), with the assignment INSIDE the ctor body. This is the
      pinned shape from boot_loader_test.dart.md.
  - construct_key: dart.package_test.tearDown_block_async
    source_form: "tearDown(() async { await manager.shutdown(); });"
    target_decision: >-
      Dart `tearDown` with an ASYNC callback that awaits a SUT method.
      Map to xUnit's `IAsyncLifetime.DisposeAsync` interface
      (https://xunit.net/docs/shared-context#async-lifetime) — the test
      class implements `IAsyncLifetime` with `public Task InitializeAsync()
      => Task.CompletedTask;` and `public async Task DisposeAsync() {
      await _manager.ShutdownAsync(); }`. Cached idiom
      `rf-dart-tearDown-async-to-xunit-iasynclifetime-disposeasync`
      (first-recorded — this is the first test-file convspec to
      exercise an async tearDown). The SUT method `manager.shutdown()`
      ⇒ `_manager.ShutdownAsync()` per the isolate_manager SUT spec
      (which pins `public Task ShutdownAsync()` — the Async suffix
      follows the TAP guideline). ALTERNATIVE: xUnit `IDisposable.Dispose`
      (synchronous) — REJECTED because `manager.shutdown()` is async
      in Dart; collapsing it to a sync `Dispose` would force a
      blocking `_manager.ShutdownAsync().GetAwaiter().GetResult()`
      which risks deadlock on captured synchronization contexts (per
      https://devblogs.microsoft.com/dotnet/async-and-await/). The
      `IAsyncLifetime` pattern is the canonical xUnit shape for
      async per-test teardown.
    idiom_id: rf-dart-tearDown-async-to-xunit-iasynclifetime-disposeasync
    research_finding_id: rf-dart-tearDown-async-to-xunit-iasynclifetime-disposeasync
    nuance: >-
      Async-teardown nuance (LOAD-BEARING, FIRST-RECORDED for test
      convspecs): Dart's `tearDown(() async { ... })` cleanly maps to
      xUnit `IAsyncLifetime.DisposeAsync` — both are awaited by the
      runner before the next test begins; both surface exceptions as
      test failures with full stack traces. The xUnit `IAsyncLifetime`
      contract requires BOTH `InitializeAsync` and `DisposeAsync` to be
      implemented (interface methods), so codegen MUST emit BOTH —
      `InitializeAsync` returning `Task.CompletedTask` if there is no
      async-setup (this file has only sync setUp via the constructor).
      ALTERNATIVE: xUnit `IClassFixture<T>` + `IAsyncLifetime` on the
      fixture — REJECTED because that pattern shares state across all
      tests in the class (the opposite of `tearDown` semantics, which
      is per-test). xUnit v3's `[Fixture]` per-test pattern would
      also work — recorded for forward-compat. Method-rename nuance:
      `shutdown` ⇒ `ShutdownAsync` per TAP guideline. Cross-thread
      safety nuance: `ShutdownAsync` MUST be safe to call from any
      thread (the test thread) — per the isolate_manager SUT spec, it
      orchestrates closing each agent's mailbox / signalling each agent
      thread to terminate; whichever threading primitive the
      isolate_manager SUT escalation pins for the agent thread layer,
      the shutdown call site sees only a `Task`-returning API.
  - construct_key: dart.package_test.for_loop_generated_tests
    source_form: >-
      "for (final n in [1, 2, 3]) {
         test('fplay$n runs across isolates (3 adults)', () async {
           await _runPlay(manager, 'mad_fplay$n.glp');
         }, timeout: Timeout(Duration(seconds: 30)));
       }
       for (final n in [4, 5, 6, 7]) { ... }
       for (final n in [9, 10]) { ... }"
    target_decision: >-
      Three sibling Dart `for` loops generate test methods at file-eval
      time — `package:test` registers each loop iteration as a separate
      named test (because the `test(label, body)` call inside the loop
      executes during the synchronous file load and registers each test
      with its interpolated label). xUnit has no equivalent runtime-
      test-registration model — tests are static reflection-discovered
      `[Fact]` / `[Theory]` methods. The faithful and idiomatic C#
      counterpart is `[Theory]` + `[InlineData]` parameterised tests:
      one `[Theory]` method per range-group, with one `[InlineData(n)]`
      attribute per loop iteration. Concretely: the `[1, 2, 3]` loop
      becomes `[Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)]
      [Trait("Plays", "3 adults")] public async Task
      Fplay_RunsAcrossIsolates_3Adults(int n) { await _RunPlayAsync(
      _manager, $"mad_fplay{n}.glp"); }`; the `[4, 5, 6, 7]` loop becomes
      a separate `[Theory]` method (e.g. `Fplay_RunsAcrossIsolates_4Agents
      (int n)`) with four `[InlineData]` attributes; the `[9, 10]` loop
      becomes a third (e.g. `Fplay_RunsAcrossIsolates_3Agents(int n)`).
      Per-test display-name: xUnit `[Theory]` derives the display name
      from the method name + the parameter values (e.g.
      `Fplay_RunsAcrossIsolates_3Adults(n: 1)`); codegen MAY add
      `[Theory(DisplayName = "fplay{0} runs across isolates (3 adults)")]`
      — but this xUnit attribute does NOT format-interpolate the inline-
      data values into the DisplayName (it is a literal string). To
      preserve the Dart per-iteration label exactly, codegen would have
      to enumerate three `[Fact(DisplayName = "fplay1 runs across
      isolates (3 adults)")]` methods — but that defeats the parameter-
      isation. The pinned compromise: `[Theory]` per range-group with
      `[InlineData]` per iteration + a `[Trait("Group", "fplayN runs
      across isolates (3 adults)")]` if reviewer-visibility of the
      original label is load-bearing.
    idiom_id: null
    research_finding_id: rf-dart-package-test-for-loop-to-xunit-theory-inlinedata
    nuance: >-
      Runtime-test-registration nuance (LOAD-BEARING, FIRST-RECORDED for
      test convspecs): Dart `package:test` registers tests at runtime
      from any expression that calls `test(...)`; xUnit relies on
      compile-time / reflection-time discovery of `[Fact]` / `[Theory]`
      methods. A literal-loop in Dart MUST be unrolled OR parameterised
      in C# — there is no runtime-registration counterpart. `[Theory]` +
      `[InlineData]` (https://xunit.net/docs/getting-started/v2/data-
      driven-tests) is the canonical xUnit parameterisation; recorded
      alternative: `[MemberData]` referencing a `public static
      IEnumerable<object[]> N { get; }` property — equivalent semantics
      but more verbose for small integer ranges. ALTERNATIVE: unroll the
      loop into N `[Fact]` methods (three methods for `[1,2,3]`, four
      for `[4..7]`, two for `[9,10]`) — also faithful, preserves
      per-iteration display name LITERALLY, at the cost of code-size
      duplication. The pinned choice (`[Theory] [InlineData]`) is the
      DRY shape; codegen MAY emit either form based on a project-wide
      preference flag — recorded. Display-name-interpolation nuance:
      Dart `'fplay$n runs across isolates (3 adults)'` interpolates `n`
      at test-registration time, producing distinct labels like
      `'fplay1 runs across isolates'` etc. xUnit `[Theory(DisplayName
      = "..." )]` does NOT format-interpolate — the literal string is
      shown plus the parameter values appended in parens. If the exact
      Dart label is load-bearing (reviewer-visibility), the unroll-to-
      multiple-`[Fact]` alternative is preferred — recorded but not
      pinned. Per-test-timeout nuance: see next construct.
  - construct_key: dart.package_test.test_timeout_attribute_duration
    source_form: >-
      "test('...', () async { ... }, timeout: Timeout(Duration(seconds:
       30)));
       test('fplay13 ...', () async { ... }, timeout: Timeout(Duration(
       seconds: 45)));"
    target_decision: >-
      Dart `package:test`'s `timeout:` named parameter on `test(...)` is
      a per-test timeout that fails the test if the body has not
      completed within the specified `Duration`. The xUnit counterpart
      is `[Fact(Timeout = N)]` / `[Theory(Timeout = N)]` (xUnit v2.2+,
      https://xunit.net/docs/comparisons#assertions) — note the property
      takes MILLISECONDS as `int`, not a `TimeSpan` (a footgun). Map
      `Timeout(Duration(seconds: 30))` ⇒ `Timeout = 30000` (i.e.
      30_000ms). Map `Timeout(Duration(seconds: 45))` ⇒ `Timeout =
      45000`. Concrete attributes: `[Fact(Timeout = 30000)]` /
      `[Theory(Timeout = 30000)]` for the 12 tests using 30s timeout;
      `[Fact(Timeout = 45000)]` for fplay13. NOTE: xUnit's `Timeout`
      attribute is ONLY effective on tests that use `async Task` (it
      uses `Task.WaitAsync(TimeSpan)` internally per the xUnit source)
      — already satisfied by every test in this file (all are
      `async Task`).
    idiom_id: null
    research_finding_id: rf-dart-test-timeout-to-xunit-fact-timeout
    nuance: >-
      Per-test-timeout nuance (LOAD-BEARING, FIRST-RECORDED for the
      test convspecs): Dart `Timeout(Duration(seconds: N))` accepts a
      `Duration` object; xUnit `Timeout = N` accepts a raw `int` of
      MILLISECONDS — codegen MUST convert N seconds ⇒ N*1000 ms (e.g.
      30 ⇒ 30000). The footgun of passing `30` to xUnit (interpreted as
      30 milliseconds, not 30 seconds) is recorded explicitly in the
      research finding. Cancellation-on-timeout nuance: xUnit cancels
      the test by aborting the task — the agent threads spawned by
      `IsolateManager.BootAsync` would NOT automatically terminate
      (they have no awareness of the test's cancellation). The
      `tearDown`-equivalent (`DisposeAsync`) is STILL called after a
      timeout — so the `_manager.ShutdownAsync()` call in
      `DisposeAsync` will be invoked to clean up. This matches Dart's
      `package:test` behaviour where `tearDown` also runs after a
      timeout. ALTERNATIVE: per-test `CancellationTokenSource` with
      `cts.CancelAfter(...)` passed into the test body — RECORDED but
      not pinned; the xUnit `Timeout` attribute is the simpler shape
      that matches the Dart `timeout:` parameter 1:1.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('CSSN v2 Multi-Isolate', () { ... }); }"
    target_decision: >-
      Drop the Dart `void main()` entirely — xUnit discovers `[Fact]` /
      `[Theory]` methods on `public` classes by reflection. The single
      `group(...)` call inside `main` becomes the enclosing test class
      (see next construct). Cached idiom
      `rf-dart-package-test-main-omit-in-xunit` from
      `mad_cold_call_isolate_test.dart.md`. `void main()` is dropped
      losslessly because the entire `main` body is exactly one
      `group(...)` call.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (cache hit, restated): Dart `main` is invoked once
      per test-file process; xUnit has no per-file hook (only per-class
      via constructor / per-collection via `ICollectionFixture<T>`).
      THIS file's `main` body is exactly one `group(...)` call with no
      pre-group statements, so the omission is lossless.
  - construct_key: dart.package_test.group_block_with_setUp_tearDown
    source_form: >-
      "group('CSSN v2 Multi-Isolate', () {
         late IsolateManager manager;
         setUp(() { manager = IsolateManager(); });
         tearDown(() async { await manager.shutdown(); });
         /* 13 tests via 3 for-loops + 4 standalone test() calls */
       });"
    target_decision: >-
      One Dart `group(label, body)` containing a `late` field declaration
      + a `setUp` + an async `tearDown` + 13 tests. Maps to a single C#
      `public class CssnV2IsolateTests : IAsyncLifetime` PascalCase test
      class (non-identifier characters stripped: `'CSSN v2 Multi-Isolate'`
      ⇒ `CssnV2MultiIsolate` ⇒ class name `CssnV2IsolateTests`). The
      original label preserved via `[Trait("Group", "CSSN v2
      Multi-Isolate")]` on the class. The `late IsolateManager manager;`
      field ⇒ `private readonly IsolateManager _manager;` (constructor-
      assigned); `setUp` ⇒ constructor body; `tearDown` ⇒ `DisposeAsync`;
      13 tests ⇒ three `[Theory]` methods + four `[Fact]` methods.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Class-with-IAsyncLifetime nuance (FIRST-RECORDED — combines the
      cached idiom `rf-dart-package-test-group-to-xunit-class` with the
      new idiom `rf-dart-tearDown-async-to-xunit-iasynclifetime-
      disposeasync`): when a Dart group has an ASYNC tearDown, the
      enclosing xUnit class MUST implement `IAsyncLifetime` (the
      synchronous `IDisposable` alternative would force a deadlock-
      prone blocking-await). Single-group nuance: this file has only
      ONE outer group with 13 tests — no nested groups, no sibling
      groups (contrast `mad_scenarios_test.dart.md`'s four-sibling-groups
      pattern). Name-mangling nuance: `'CSSN v2 Multi-Isolate'` contains
      a space, lowercase v, dash — strip non-identifier characters,
      PascalCase remaining tokens: `CssnV2MultiIsolate` (the dash is
      stripped; the lowercase `v` between digits stays as `v` after
      PascalCase — `Cssn` + `V2` + `Multi` + `Isolate` is the natural
      mangling). The convention "test class name matches file name"
      gives `CssnV2IsolateTests` (matching the C# file
      `CssnV2IsolateTest.cs`); the `[Trait]` preserves the original
      label for reporter visibility.
  - construct_key: dart.expression.string_interpolation_in_test_label_and_path
    source_form: >-
      "'fplay$n runs across isolates (3 adults)'
       'mad_fplay$n.glp'
       '$_madBootDir/$bootFilename'
       'Skipping: ${bootFile.path} not found'"
    target_decision: >-
      Dart string interpolation `'...$expr...'` / `'...${expr}...'` maps
      to C# interpolated strings `$"...{expr}..."` (note the leading `$`
      — C# https://learn.microsoft.com/dotnet/csharp/language-reference/
      tokens/interpolated). Specifically: `'fplay$n runs across isolates
      (3 adults)'` ⇒ `$"fplay{n} runs across isolates (3 adults)"` —
      BUT this is inside a test-display-name attribute parameter, which
      is a COMPILE-TIME-CONSTANT string in C# attribute syntax; C#
      attribute parameters MUST be `const`, and interpolated strings are
      NOT `const` (even in C# 10+, attribute parameters specifically
      require literal-`string` or `nameof` — see https://learn.microsoft.
      com/dotnet/csharp/whats-new/csharp-10#constant-interpolated-strings
      which explicitly notes attribute parameters are still excluded).
      For the test-label use the interpolation MUST be folded into the
      `[InlineData]` attribute parameter — `[InlineData(1)]` ⇒ method
      body uses the parameter `n` to construct the runtime label /
      filename via `$"mad_fplay{n}.glp"` — and the `[Theory]` DisplayName
      attribute uses a literal string template like `[Theory(DisplayName
      = "fplay{n} runs across isolates")]` (xUnit's DisplayName does NOT
      format-interpolate parameter values — known limitation; the
      reviewer sees the per-parameter values appended by the runner).
      `'mad_fplay$n.glp'` (inside method body) ⇒ `$"mad_fplay{n}.glp"`;
      `'$_madBootDir/$bootFilename'` (inside method body) ⇒
      `_MadBootDir + "/" + bootFilename` (FOLDED to string-concat for
      const-friendliness in the path local — though `$"{ _MadBootDir}/{
      bootFilename}"` is also valid since it's not const here);
      `'Skipping: ${bootFile.path} not found'` ⇒ `"Skipping: " +
      bootFilePath + " not found"` OR `$"Skipping: {bootFilePath} not
      found"`.
    idiom_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      Interpolation-syntax nuance (cache hit): Dart `'$expr'` /
      `'${expr}'` ⇒ C# `$"{expr}"`. Single-quote-vs-double-quote nuance:
      Dart uses single quotes; C# uses double quotes for `string`. Const-
      context nuance (LOAD-BEARING, explicitly addressed): Dart string
      interpolations involving CONST string operands are themselves
      compile-time constants and may be used in `const` contexts; C#
      interpolated strings are NOT `const` in attribute parameters
      (compile-time constant interpolated strings in `const` field
      declarations work in C# 10+, but attribute parameters still
      reject them) — codegen MUST fold to `+`-concat over const
      operands for any const-context use, including the
      `_MadBootDir + "/mad_boot"` case in the top-level constants
      construct. Attribute-parameter nuance (xUnit-specific): the
      Dart `'fplay$n runs across isolates'` test label CANNOT be
      reproduced inside `[Theory(DisplayName = ...)]` as a literal-
      interpolated string at the n-value level — the xUnit
      `DisplayName` shows a template + parameter values appended.
      The simplest faithful shape is to omit `DisplayName` and let
      xUnit auto-derive from the method name + parameter values, OR
      to unroll the `[Theory]` into N `[Fact(DisplayName = "fplay1
      runs across isolates (3 adults)")]` methods preserving the
      LITERAL Dart label per iteration. The pinned choice is `[Theory]`
      with method name embedded; unroll alternative recorded.
conversion_units:
  - "cu-1: file-scope using directives — `using System;` + `using System.IO;` + `using System.Threading;` + `using System.Threading.Tasks;` + `using Xunit;` + `using <RootNs>.Multiagent;`"
  - "cu-2: namespace declaration mirroring test/multiagent path (e.g. `namespace <RootNs>.Test.Multiagent`)"
  - "cu-3: `public class CssnV2IsolateTests : IAsyncLifetime` — top-level test class derived from the group label 'CSSN v2 Multi-Isolate', with `[Trait(\"Group\", \"CSSN v2 Multi-Isolate\")]`"
  - "cu-4: three private const string fields on the class — `private const string _CssnV2Dir = \"../programs/cssn_modules_v2\";`, `private const string _MadBootDir = _CssnV2Dir + \"/mad_boot\";`, `private const string _RootSelfGlp = \"../programs/self.glp\";`"
  - "cu-5: one private readonly field — `private readonly IsolateManager _manager;`"
  - "cu-6: constructor `public CssnV2IsolateTests() { _manager = new IsolateManager(); }` mapping the Dart `setUp` block"
  - "cu-7: `public Task InitializeAsync() => Task.CompletedTask;` (IAsyncLifetime requires both members; no async setUp here)"
  - "cu-8: `public async Task DisposeAsync() { await _manager.ShutdownAsync(); }` mapping the Dart async `tearDown`"
  - "cu-9: one private static async helper method — `private static async Task _RunPlayAsync(IsolateManager manager, string bootFilename, int timeoutSec = 10) { ... }` — body translates the Dart helper verbatim: build path local, `File.Exists` skip-on-missing with `Console.WriteLine`, `File.ReadAllText`, `var loader = new BootLoader(); var config = loader.Load(bootSource); config.ProjectDir = _CssnV2Dir; config.RootSelfGlpPath = _RootSelfGlp; await manager.BootAsync(config, traceConfig: new TraceConfig(glp: false, mad: false)); manager.Start(); await Task.Delay(TimeSpan.FromSeconds(timeoutSec));`"
  - "cu-10: three `[Theory]` methods for the for-loop-generated tests — `Fplay_RunsAcrossIsolates_3Adults(int n)` with `[InlineData(1)] [InlineData(2)] [InlineData(3)] [Theory(Timeout = 30000)]`; `Fplay_RunsAcrossIsolates_4Agents(int n)` with `[InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [Theory(Timeout = 30000)]`; `Fplay_RunsAcrossIsolates_3Agents(int n)` with `[InlineData(9)] [InlineData(10)] [Theory(Timeout = 30000)]` — each body: `await _RunPlayAsync(_manager, $\"mad_fplay{n}.glp\");`"
  - "cu-11: four `[Fact]` methods for the standalone tests — `Fplay8_RunsAcrossIsolates_2Adults` (`Timeout = 30000`, body `await _RunPlayAsync(_manager, \"mad_fplay8.glp\");`); `Fplay11_RunsAcrossIsolates_6Agents` (`Timeout = 30000`, body `await _RunPlayAsync(_manager, \"mad_fplay11.glp\");`); `Fplay12_RunsAcrossIsolates_5Agents` (`Timeout = 30000`, body `await _RunPlayAsync(_manager, \"mad_fplay12.glp\");`); `Fplay13_RunsAcrossIsolates_Village_6Agents` (`Timeout = 45000`, body `await _RunPlayAsync(_manager, \"mad_fplay13.glp\", timeoutSec: 15);`)"
  - "cu-12: SUT API call sites translated via per-SUT-file convspec decisions — `new BootLoader()`, `loader.Load(bootSource)`, `config.ProjectDir`/`config.RootSelfGlpPath` settable properties, `new TraceConfig(glp: false, mad: false)`, `manager.BootAsync(config, traceConfig: ...)`, `manager.Start()`, `_manager.ShutdownAsync()` — all PascalCased + Async-suffixed per the project-wide rename idiom"
escalations: []
```

## Rationale + research provenance

### Why no escalations on isolate / threading / multi-isolate hosting

The .NET hosting model for the multiagent runtime's isolate-equivalent
is a TRUE undecidable point — but it is ALREADY OWNED by
`lib/multiagent/isolate_manager.dart.md`, which itself defers to the
`lib/runtime/heap_fcp.dart.md` threading-model escalation
(`dart.heap_fcp.concurrency_model_thread_safety_for_multiagent_hosting`).
THIS test file is a thin DRIVER that only sees the
`IsolateManager.BootAsync` / `Start` / `ShutdownAsync` PUBLIC API —
whichever threading primitive the SUT escalation pins is transparent
at the call sites here (the test does not introspect agent threads,
isolate handles, or message routing). Per FR-013's
"don't double-escalate" discipline — the same discipline applied in
`mad_cold_call_isolate_test.dart.md`, `lib/runtime/body_kernels.dart.md`,
and the rest of the multiagent SUT family — introducing a NEW
escalation here would (a) duplicate the isolate_manager decision point
and (b) block this file's conversion on a question that already has an
owner. `escalations: []` is therefore intentional, not a placeholder.

### xUnit pinning (cache hit)

xUnit pinned project-wide by every prior test-file convspec; reuse
verbatim per `rf-dart-package-test-import-to-xunit-using`. No
re-research. Authoritative basis recorded in
`mad_error_handling_test.dart.md`: xUnit docs
(https://xunit.net/docs/getting-started/v3/getting-started) for
`[Fact]` / `[Theory]` / `[Trait]` / constructor-per-test isolation /
`IAsyncLifetime`, and the Dart `package:test` README on pub.dev
(https://pub.dev/packages/test) for `group` / `test` / `expect` /
`timeout` / matcher semantics.

### `dart:io` `File` → `System.IO.File` (new finding)

Microsoft Learn — `System.IO.File`
(https://learn.microsoft.com/dotnet/api/system.io.file) — names this
as the canonical static-API for one-shot file existence and string-
read operations. Dart `dart:io` `File` class on api.dart.dev
(https://api.dart.dev/stable/dart-io/File-class.html) is the
instance-style counterpart. The conversion folds the Dart instance +
two method calls into two static .NET method calls + one string local.
Default encoding (UTF-8) is identical on both sides
(https://learn.microsoft.com/dotnet/api/system.io.file.readalltext).

### `Future.delayed` → `Task.Delay(TimeSpan)` (cache hit)

Microsoft Learn — `Task.Delay`
(https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.delay)
— is the canonical async-delay primitive. Dart `Future.delayed` docs
(https://api.dart.dev/stable/dart-async/Future/Future.delayed.html)
match the semantics 1:1. `Duration(seconds: N)` ⇒
`TimeSpan.FromSeconds(N)` per the cached
`rf-dart-duration-to-csharp-timespan` idiom. Both use 100-ns precision.

### `setUp` / async `tearDown` → constructor + `IAsyncLifetime.DisposeAsync` (new finding for async tearDown)

xUnit Shared Context docs
(https://xunit.net/docs/shared-context#async-lifetime) describe the
`IAsyncLifetime` interface as the canonical async-setup / async-
teardown contract — the test class implements `InitializeAsync` and
`DisposeAsync`, the runner awaits both around every test. Dart
`package:test`'s `tearDown(() async { ... })` semantics
(https://pub.dev/documentation/test/latest/test_api/tearDown.html) map
1:1 onto `DisposeAsync` (both are awaited per-test). Synchronous
`IDisposable.Dispose` would force a blocking-await that risks deadlock
per https://devblogs.microsoft.com/dotnet/async-and-await/ — REJECTED.

### `package:test` for-loop test generation → `[Theory]` + `[InlineData]` (new finding)

xUnit data-driven-tests docs
(https://xunit.net/docs/getting-started/v2/data-driven-tests) describe
`[Theory]` + `[InlineData]` as the canonical parameterised-test shape
— compile-time-reflection-discovered with one method body and N
attribute-supplied parameter sets. Dart's runtime-test-registration
model (where a literal `for` loop containing `test(...)` calls
registers N distinct tests at file-load time) has no exact
counterpart; the faithful translation is parameterised — alternative
unroll-to-N-`[Fact]`s recorded for label-preservation use cases.

### `test('...', body, timeout: Timeout(...))` → `[Fact(Timeout = N)]` / `[Theory(Timeout = N)]` (new finding)

xUnit `Timeout` property
(https://xunit.net/docs/comparisons#assertions) accepts an `int` of
MILLISECONDS — codegen MUST multiply Dart-seconds by 1000. Dart
`package:test` `timeout:` parameter docs
(https://pub.dev/documentation/test/latest/test_api/Timeout-class.html)
describe the per-test timeout semantics. The xUnit Timeout footgun
(passing `30` is 30ms, not 30s) is recorded explicitly.

### SUT call-site translation via per-SUT-file convspec (FR-012 cache hits)

Every SUT API call (`BootLoader`, `loader.Load`, `config.ProjectDir`,
`config.RootSelfGlpPath`, `TraceConfig(...)`, `manager.BootAsync`,
`manager.Start`, `_manager.ShutdownAsync`) is decided by the
corresponding per-SUT-file convspec
(`.codeconv/conversion-specs/lib/multiagent/boot_loader.dart.md`,
`.codeconv/conversion-specs/lib/multiagent/isolate_manager.dart.md`).
This test spec records only the SHAPE of the cross-file dependency —
the names, types, and call shapes come from the SUT specs. No SUT-side
decision is re-derived (FR-024 + FR-012/SC-007 — KB-resolved, not
re-researched).

### `print` → `Console.WriteLine` (cache hit)

Cached idiom `rf-dart-print-and-terminate-to-csharp-equivalent` from
`lib/bytecode/runner.dart.md`. ITestOutputHelper recorded as the
xUnit-isolated-capture alternative; Console.WriteLine pinned for
cross-thread safety (the SUT's spawned agents may also print on
their own threads, and ITestOutputHelper is not thread-safe per the
xUnit docs).
