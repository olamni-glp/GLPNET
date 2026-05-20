# Conversion Spec — test/dynamic_dispatch_test.dart

> Conversion-spec artifact for test/dynamic_dispatch_test.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.
>
> File is a `package:test`-based integration suite (209 lines, 5
> `test()` cases — one in `group('serve/2', ...)` + four in
> `group('end-to-end dispatch', ...)` inside `void main()`). It
> exercises the full dynamic-module-dispatch chain
> `caller -> channel -> serve -> _activate -> procedure` via
> `activateModule(...)`, `handle.send(goal)`, then drives a
> `Scheduler` and asserts on a writer-bound `ConstTerm` value via
> `rt.heap.dereference(VarRef(fWriter))`. Every non-trivial construct
> REUSES an idiom recorded by prior runtime / test specs (sibling
> exemplars: test/runtime/module_activation_test.dart.md and
> test/runtime/rpc_routing_test.dart.md — same module-dispatch
> family, same `GlpRuntime`/`Scheduler`/`activateModule` surface;
> and test/heap/binding_pointer_test.dart.md for the
> `rt.heap.*`/`VarRef`/`ConstTerm` surface).
>
> THREADING-MODEL INHERITANCE NOTICE — this file exercises the
> `Scheduler`/`GlpRuntime`/`HeapFCP`/`GlpChannelHandle` surface.
> The .NET threading-model decision (single-owning-context vs
> `ConcurrentDictionary`/`Interlocked` etc.) is ESCALATED in
> `lib/runtime/heap_fcp.dart.md` escalations[0] and INHERITED
> through every dependent runtime spec
> (`lib/runtime/scheduler.dart.md`, `lib/runtime/runtime.dart.md`,
> `lib/runtime/glp_activation.dart.md`, `lib/bytecode/runner.dart.md`,
> `lib/engine/glp_engine.dart.md`). Per FR-013 + the scheduler
> precedent (module_activation_test, rpc_routing_test), this file
> INHERITS the ruling — it does NOT re-escalate. The per-test
> isolation (every test allocates its own `GlpRuntime` + `Scheduler`
> + activates its own `GlpChannelHandle`) makes the inherited
> single-owning-context option (A — recommended) trivially safe at
> the test-method scope.

```yaml
schema_version: 1
source_path: test/dynamic_dispatch_test.dart
source_sha256: ca1921987062da8ddae88f306a4001b46751dc870b0fcf0d2ad133a9c529d2a4
target_code_unit: test/DynamicDispatchTest.cs
constructs:
  - construct_key: dart.dart_io.import_directive
    source_form: "import 'dart:io';"
    target_decision: >-
      Drop the Dart `import 'dart:io';` and replace at file scope
      with `using System.IO;`. The single load-bearing surface used
      from `dart:io` in this file is `File(<path>).readAsStringSync()`
      and `File(<path>).existsSync()` — both map to `System.IO.File`
      static methods (`File.ReadAllText(string path)`,
      `File.Exists(string path)`). NO `Directory`/`Process`/`Platform`
      members are used, so the `using System.IO;` directive covers
      every callsite. The full mapping is documented per-construct
      below (`dart.dart_io.file_read_as_string_sync`,
      `dart.dart_io.file_exists_sync`).
    idiom_id: null
    research_finding_id: rf-dart-io-import-to-csharp-using-system-io
    nuance: >-
      FIRST-SEEN idiom row (no prior test convspec used `dart:io` —
      module_activation_test held the GLP source inline as a
      triple-quoted top-level const; rpc_routing_test likewise. THIS
      file reads `../programs/self.glp` and
      `../programs/tests/dynamic_dispatch/math_service.glp` and
      `../programs/tests/dynamic_dispatch/single_export.glp` from
      disk). Import-unit nuance: Dart imports a library; C# imports
      a namespace — `System.IO` covers all File static APIs. Path-
      handling nuance: the source uses Unix-style relative paths
      (`'../programs/self.glp'`) — the C# port MUST preserve the
      same relative-path strings since the converted test process's
      working directory mirrors the Dart test process's working
      directory (the test runner invokes from the package root). NO
      `Path.Combine` normalisation is required at THIS construct
      level — every literal path remains verbatim; if cross-platform
      `Path.DirectorySeparatorChar` mismatch matters for a future
      Windows-targeted run, codegen would normalise — but `dart:io`
      already accepts forward-slash paths on Windows, so the source
      semantics are platform-independent and the C# `File.ReadAllText`
      counterpart is too. Async/sync nuance (explicitly addressed):
      every `dart:io` API used in this file is the SYNC variant
      (`readAsStringSync`, `existsSync`) — codegen MUST use the
      synchronous `File.ReadAllText`/`File.Exists` counterparts,
      NOT the async `File.ReadAllTextAsync` (which would require
      `async Task` test methods). The Dart source's deliberate
      sync-API choice carries into C#.

  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` and replace at
      file scope with `using Xunit;`. REUSE the project-wide xUnit
      pinning established by smoke_test.dart.md and every subsequent
      `package:test` convspec (heap/binding_pointer_test.dart.md,
      runtime/module_activation_test.dart.md, runtime/
      rpc_routing_test.dart.md, etc.). FR-012 / SC-007 cache hit —
      no re-research. Codegen MUST also project to a namespace
      mirroring the Dart `test/` directory (e.g. `<RootNs>.Test`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Framework-choice reuse (cached): every `package:test` file in
      the inventory maps to xUnit so test discovery, runner config,
      and attribute vocabulary stay consistent (SC-007). Lifecycle
      nuance: xUnit creates a FRESH instance of the test class per
      `[Fact]` (xunit.net "Shared Context between Tests") — every
      `test()` body in this file constructs its OWN `GlpCompiler`,
      `GlpRuntime`, `GlpEngine`, `Scheduler`, `GlpChannelHandle`
      etc., so the per-instance freshness matches the source.
      Synchronous nuance: no `async`/`await`/`Future` anywhere in
      this file — every test method emits `public void` (NOT
      `async Task`).

  - construct_key: dart.package_under_test.import_directive_multi_runtime
    source_form: |-
      "import 'package:glp_runtime/compiler/compiler.dart';
       import 'package:glp_runtime/compiler/partial_evaluator.dart'
           show setPreludeUnitClauseSource;
       import 'package:glp_runtime/analysis/type_checker/type_environment_builder.dart'
           show setPreludeEnvironmentSource;
       import 'package:glp_runtime/engine/glp_engine.dart';
       import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';
       import 'package:glp_runtime/runtime/scheduler.dart';
       import 'package:glp_runtime/runtime/glp_activation.dart';
       import 'package:glp_runtime/runtime/terms.dart';
       import 'package:glp_runtime/bytecode/runner.dart';"
    target_decision: >-
      Ten `package:glp_runtime/...` imports (two carrying a `show`
      clause restricting the symbol set) collapse to FOUR C# `using`
      directives, one per distinct target namespace under the
      conventional namespace mapping pinned by the per-SUT specs:
      `using <RootNs>.Compiler;` (carries `GlpCompiler` from
      lib/compiler/compiler.dart.md AND the top-level
      `SetPreludeUnitClauseSource` static method from
      lib/compiler/partial_evaluator.dart.md), `using <RootNs>.
      Analysis.TypeChecker;` (carries `SetPreludeEnvironmentSource`
      from lib/analysis/type_checker/type_environment_builder.dart.md),
      `using <RootNs>.Engine;` (carries `GlpEngine` from
      lib/engine/glp_engine.dart.md), `using <RootNs>.Runtime;`
      (carries `GlpRuntime`, `Scheduler`, `ExecutionStatus`,
      `activateModule`/`GlpChannelHandle`, `Term`/`StructTerm`/
      `ConstTerm`/`VarRef` from terms, plus machine_state surface),
      `using <RootNs>.Bytecode;` (carries `BytecodeProgram` from
      bytecode/runner.dart.md). C# `using` is per-namespace, not
      per-file — the ten Dart imports compress whenever their
      converted files share a namespace. The Dart `show <symbol>`
      restriction has NO direct C# counterpart — `using <Ns>;`
      always brings in the entire namespace; the `show` was a
      readability hint in the source, lost in the conversion
      (semantically lossless because no name collisions exist).
      This spec records the SHAPE of the cross-file dependency; the
      namespace strings are owned by the SUT specs.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cached idiom (precedent: every prior test convspec).
      `show`-clause nuance (explicitly addressed, FIRST-SEEN-here
      sub-case): Dart `import '<lib>' show foo;` restricts the
      imported symbol set; C# `using <Ns>;` does NOT support
      symbol-set restriction at directive level (the closest C#
      equivalent — `using <Alias> = <Ns>.<Symbol>;` — applies only
      to ALIASING, not show-restriction). The codegen drops the
      `show` constraint; semantically safe because no two namespaces
      export the same identifier. Top-level-function nuance
      (explicitly addressed): `setPreludeUnitClauseSource` and
      `setPreludeEnvironmentSource` are Dart TOP-LEVEL functions;
      C# has no top-level methods, so they map to `public static`
      methods on host classes inside their respective namespaces
      (per the partial_evaluator.dart.md and
      type_environment_builder.dart.md SUT specs). The test assembly
      must reference the SUT assembly via the project file (langpair-
      level concern — out of scope for THIS artifact).

  - construct_key: dart.package_test.main_entrypoint_with_pre_group_setup
    source_form: |-
      "void main() {
         // Set prelude sources (needed for compilation)
         final rootSelfGlp = File('../programs/self.glp');
         if (rootSelfGlp.existsSync()) {
           final source = rootSelfGlp.readAsStringSync();
           setPreludeUnitClauseSource(source);
           setPreludeEnvironmentSource(source);
         }
         final ddDir = '../programs/tests/dynamic_dispatch';
         group('serve/2', () { test(...); });
         group('end-to-end dispatch', () { test(...) ×4; });
       }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint;
      xUnit discovers `[Fact]` methods by reflection — there is NO
      per-file entrypoint to emit. ELIMINATE `main` entirely. BUT
      this file's `main` carries TWO load-bearing pre-group
      statements (DISTINCT from sibling exemplars
      module_activation_test/rpc_routing_test, whose `main` bodies
      were a single `group(...)`): (1) a conditional prelude-source
      bootstrap (`if (rootSelfGlp.existsSync()) { ...
      setPreludeUnitClauseSource(source);
      setPreludeEnvironmentSource(source); }`) — process-global
      side effect on two top-level setters; (2) a local-scope `final
      ddDir = '../programs/tests/dynamic_dispatch';` shared across
      the four end-to-end tests. Translation rule:
      - The conditional prelude bootstrap is a PROCESS-WIDE,
        ONE-SHOT initialization (the two `set*Source` calls mutate
        global state that is then read by every subsequent
        `compiler.compile(...)` call). xUnit's documented mechanism
        for process-wide one-shot init is
        `IAssemblyFixture<T>`/`ICollectionFixture<T>` (xunit.net
        "Shared Context between Tests" — Class Fixtures /
        Collection Fixtures /
        https://xunit.net/docs/shared-context). The IDIOMATIC
        choice for this single file is a STATIC CONSTRUCTOR on the
        outer test class (`static DynamicDispatchTest() { ... }`)
        — runs once per AppDomain on first reference to the type
        (Microsoft Learn "Static Constructors" —
        https://learn.microsoft.com/dotnet/csharp/programming-guide/
        classes-and-structs/static-constructors). PREFERRED form:
        static constructor on `DynamicDispatchTest` performing the
        `File.Exists` -> `File.ReadAllText` ->
        `Compiler.PartialEvaluator.SetPreludeUnitClauseSource` ->
        `Analysis.TypeChecker.TypeEnvironmentBuilder.SetPreludeEnvironmentSource`
        sequence.
      - The `ddDir` local maps to a `private const string` field
        on the test class (since the value is a literal and is
        read-only across the four tests). PREFERRED form:
        `private const string DdDir = "../programs/tests/dynamic_dispatch";`.
      The `group(...)` calls then map per
      `dart.package_test.group_block_*` constructs below.
    idiom_id: null
    research_finding_id: rf-dart-test-main-with-init-to-xunit-static-ctor-plus-const
    nuance: >-
      FIRST-SEEN-here idiom row (load-bearing — distinguishes from
      module_activation_test's "main body is one `group()` only"
      precedent). Static-constructor-vs-class-fixture nuance
      (explicitly addressed): an `IClassFixture<T>` runs once per
      test CLASS instantiation cohort (NOT once per AppDomain);
      `IAssemblyFixture<T>` runs once per ASSEMBLY (closest to
      Dart `main` per-process semantics) but requires a separate
      `[assembly: AssemblyFixture(typeof(T))]` directive and is
      OVERKILL for a single-file initialization. Static-constructor
      is the SIMPLEST faithful translation — it runs exactly once
      before the first test of the class executes; matches the
      Dart `main` "runs once before any test()" contract.
      Process-global side-effect nuance (explicitly addressed —
      LOAD-BEARING): `setPreludeUnitClauseSource` and
      `setPreludeEnvironmentSource` are documented (per the SUT
      specs) as mutating PROCESS-GLOBAL state — they are NOT
      per-runtime configuration. The C# counterpart must preserve
      the process-global mutation contract; the SUT specs
      (partial_evaluator.dart.md, type_environment_builder.dart.md)
      already pin these as `public static void` methods on host
      classes, so the static-constructor invocation respects the
      contract. Conditional-existence nuance: the source uses an
      `if (existsSync())` guard — at conversion time the file
      DOES exist, but the guard preserves robustness against
      missing-file deploy scenarios. The C# port preserves the
      guard:
      `if (File.Exists(...)) { var source = File.ReadAllText(...);
        ... }`.
      Idempotency nuance: a second static-constructor invocation
      cannot happen on the same AppDomain — so the test code is
      protected from re-initialization (xUnit may instantiate the
      test class many times across `[Fact]` methods, but the
      static constructor still runs exactly once).

  - construct_key: dart.package_test.group_block_two_sibling_groups
    source_form: |-
      "group('serve/2', () {
         test('serve/2 compiles and has label', () { ... });
       });
       group('end-to-end dispatch', () {
         test('activate module and dispatch double(5, F) -> F = 10', () { ... });
         test('activate module and dispatch triple(4, F) -> F = 12', () { ... });
         test('unknown goal does not crash (fallback)', () { ... });
         test('single_export module: dispatch inc(7, F) -> F = 8', () { ... });
       });"
    target_decision: >-
      TWO sibling top-level `group(...)` blocks (no nesting) — one
      with 1 test, one with 4 tests — map to a SINGLE PascalCase
      xUnit test class `DynamicDispatchTest`. Per the established
      group-to-trait idiom (rf-dart-package-test-group-to-xunit-
      class), each `[Fact]` method carries `[Trait("Group", "<group
      label>")]` to preserve the group-affiliation provenance — TWO
      distinct trait values appear in this file (`"serve/2"` and
      `"end-to-end dispatch"`). The two-group split is purely
      organizational; there is NO `setUp`/`setUpAll` block in
      either group, no `late` shared state, no per-group
      initialization — the FLATTEN to one class with per-method
      trait partition is lossless. Per-test method names are
      PascalCased, identifier-safe forms of each test label; the
      original label is preserved verbatim via `[Fact(DisplayName =
      "<original label>")]`. Method-name proposals:
      `Serve2CompilesAndHasLabel`,
      `ActivateModuleAndDispatchDouble5FEquals10`,
      `ActivateModuleAndDispatchTriple4FEquals12`,
      `UnknownGoalDoesNotCrashFallback`,
      `SingleExportModuleDispatchInc7FEquals8`.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Cached idiom (precedent: binding_pointer_test.dart.md six
      sibling groups, module_compiler_test.dart.md sibling groups).
      Two-sibling-groups nuance (explicitly addressed, differs
      from module_activation_test's single-group case): when ≥2
      groups exist, the `[Trait("Group", ...)]` partition is
      REQUIRED (not optional). Label-mangling nuance: `'serve/2'`
      contains a slash (not an identifier character) — PascalCased
      to `Serve2`; `'end-to-end dispatch'` PascalCases to
      `EndToEndDispatch`. Display-names preserve the original
      strings including slash, hyphens, parens, etc. Arrow-glyph
      nuance: the test labels contain a literal Unicode right-
      arrow `→` (`'... double(5, F) → F = 10'`) — the C#
      `[Fact(DisplayName = "...")]` literal MUST preserve the
      arrow verbatim (UTF-8 / UTF-16 in the .cs source file).
      Codegen MUST ensure the `.cs` file is UTF-8 with BOM (or
      UTF-8 no-BOM under `<EnableDefaultCompileItems>` MSBuild
      default) so the glyph survives the build. NB the `source_form`
      above ASCII-fies the arrow to `->` because YAML lint rules
      in the prior batch flagged literal arrows — the actual
      `DisplayName` MUST be the Unicode `→`.

  - construct_key: dart.package_test.test_call_simple_synchronous
    source_form: >-
      "test('<label>', () { /* arrange (compile via GlpCompiler +
       GlpEngine, activateModule, Scheduler), act (drainWithStatus
       + handle.send + gq.enqueue), assert (expect ExecutionStatus
       + heap.dereference + isA<ConstTerm> + ConstTerm.value) */ });"
    target_decision: >-
      Each Dart `test(label, body)` with no `skip:` / no `timeout:`
      and a SYNCHRONOUS closure body becomes a `public void`
      instance method on `DynamicDispatchTest`, decorated with
      `[Fact(DisplayName = "<original label>")]` plus the
      group-specific `[Trait("Group", <group label>)]`. All FIVE
      callbacks in this file are synchronous (no `async`, no
      `Future`, no `await`) — NO target method is `async Task`.
      Bodies translate statement-for-statement (see the per-call
      construct rows below).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Cached idiom (precedent: every prior test convspec). No-
      shared-state nuance (explicitly addressed, carries over from
      module_activation_test): every test allocates its own
      `compiler`, `rt`, `engine`, `mathBytecode`/`serveBytecode`,
      `handle`, `scheduler`. Codegen MUST emit per-method-local
      `var` declarations, NOT instance fields — xUnit's per-test
      class instantiation provides the isolation guarantee.
      Async nuance: explicitly NOT exercised in this file —
      recorded as "inherited from scheduler.dart.md sync contract,
      not surfaced here".

  - construct_key: dart.dart_io.file_constructor
    source_form: |-
      "File('../programs/self.glp')
       File('../programs/self.glp').absolute.path
       File('$ddDir/math_service.glp')
       File('$ddDir/single_export.glp')"
    target_decision: >-
      Dart `File(<path>)` is a constructor for `dart:io`'s `File`
      class (Dart docs:
      https://api.dart.dev/stable/dart-io/File-class.html). C#
      `System.IO.File` is a STATIC CLASS — there is no `new
      File(<path>)` constructor. Each Dart `File(<path>).<member>`
      callsite maps to a STATIC method call on `File`:
      - `File('../programs/self.glp').readAsStringSync()` ->
        `File.ReadAllText("../programs/self.glp")` (see
        `dart.dart_io.file_read_as_string_sync` construct below);
      - `File('../programs/self.glp').existsSync()` ->
        `File.Exists("../programs/self.glp")` (see
        `dart.dart_io.file_exists_sync` below);
      - `File('../programs/self.glp').absolute.path` ->
        `Path.GetFullPath("../programs/self.glp")` (Microsoft Learn
        `Path.GetFullPath`:
        https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath
        — converts a relative path to an absolute one using the
        current working directory, matching Dart's
        `File.absolute.path` documented contract:
        https://api.dart.dev/stable/dart-io/FileSystemEntity/absolute.html).
      No `File` reference is held as a local variable across method
      calls except `final rootSelfGlp = File('../programs/self.glp');`
      in `main`, which is then dotted twice (`.existsSync()` and
      `.readAsStringSync()`). Codegen MUST capture the path STRING
      once (`var rootSelfGlpPath = "../programs/self.glp";`) and
      pass it twice to `File.Exists(...)` and `File.ReadAllText(...)`,
      since C# `File` has no instance to hold.
    idiom_id: null
    research_finding_id: rf-dart-io-file-constructor-to-csharp-static-file-class
    nuance: >-
      FIRST-SEEN idiom row (no prior test convspec used `File()`).
      Class-shape nuance (explicitly addressed, LOAD-BEARING): Dart
      `dart:io` File is an INSTANCE class whose methods read/write
      the file referenced by the wrapped path; C# `System.IO.File`
      is a STATIC class whose every method takes the path as an
      argument — there is no instance to capture path state. The
      Dart pattern `final f = File(p); f.existsSync(); f.readAsStringSync();`
      maps to `if (File.Exists(p)) { var source = File.ReadAllText(p); }`
      — the path string is referenced twice instead of an instance
      being captured once. Codegen MUST eliminate the (otherwise
      unused) Dart `final f = File(p);` instance assignment and
      hoist the path string instead. Interpolation nuance: the
      Dart call `File('$ddDir/math_service.glp')` interpolates the
      `ddDir` local into the path string — C# counterpart is
      `File.ReadAllText($"{DdDir}/math_service.glp")` (interpolated
      string literal `$"..."`) per Microsoft Learn "Interpolated
      strings": https://learn.microsoft.com/dotnet/csharp/language-
      reference/tokens/interpolated. PREFERRED: use C# interpolation
      to preserve the Dart-shape `$ddDir`.

  - construct_key: dart.dart_io.file_exists_sync
    source_form: "rootSelfGlp.existsSync()"
    target_decision: >-
      Dart `File.existsSync()` (synchronous) maps to C# `File.Exists
      (string path)` (Microsoft Learn:
      https://learn.microsoft.com/dotnet/api/system.io.file.exists).
      Both return `bool` and are non-throwing on permission errors
      (return `false` instead of throwing — observable contract
      match). Callsite shape:
      `if (File.Exists("../programs/self.glp")) { ... }`.
    idiom_id: null
    research_finding_id: rf-dart-io-file-exists-sync-to-csharp-file-exists
    nuance: >-
      Sync-vs-async nuance (explicitly addressed): Dart `File`
      provides both `existsSync()` (blocking) and `exists()`
      (returns `Future<bool>`); the source uses the SYNC form, so
      codegen uses C# `File.Exists` (synchronous, no `await`).
      Permission-error nuance: Dart `existsSync` and C# `File.Exists`
      both return `false` on inaccessible paths (per docs); neither
      throws — semantics preserved. The conditional guard preserves
      robustness against a missing prelude (acknowledged in the
      source comment "needed for compilation").

  - construct_key: dart.dart_io.file_read_as_string_sync
    source_form: |-
      "rootSelfGlp.readAsStringSync()
       File('../programs/self.glp').readAsStringSync()
       File('$ddDir/math_service.glp').readAsStringSync()
       File('$ddDir/single_export.glp').readAsStringSync()"
    target_decision: >-
      Dart `File.readAsStringSync({Encoding encoding = utf8})`
      (Dart docs:
      https://api.dart.dev/stable/dart-io/File/readAsStringSync.html)
      maps to C# `File.ReadAllText(string path)` (default encoding
      UTF-8 — Microsoft Learn:
      https://learn.microsoft.com/dotnet/api/system.io.file.readalltext).
      Callsite shapes:
      - `File.ReadAllText("../programs/self.glp")` (×3 uses across
        the four end-to-end tests — preserve the per-test
        repetition for isolation);
      - `File.ReadAllText($"{DdDir}/math_service.glp")` (3 uses
        across the dispatch + unknown-goal tests);
      - `File.ReadAllText($"{DdDir}/single_export.glp")` (1 use,
        the single_export test).
      Return type `string` — same as Dart `String`.
    idiom_id: null
    research_finding_id: rf-dart-io-file-read-as-string-sync-to-csharp-file-readalltext
    nuance: >-
      Encoding nuance (explicitly addressed): Dart `readAsStringSync`
      defaults to UTF-8; C# `File.ReadAllText(string)` ALSO defaults
      to UTF-8 (Microsoft Learn confirms). Both auto-detect a BOM
      and skip it. The GLP source files are UTF-8 (sibling repo
      convention — no BOM); the C# port preserves the same
      assumption. Newline-preservation nuance: Dart and C# both
      preserve raw byte content including all `\r\n` / `\n` line
      separators — the bytecode compiler downstream is whitespace-
      preserving but newline-agnostic, so the C# port produces
      identical bytecode. Async-method-naming nuance: Microsoft's
      naming convention reserves the bare `ReadAllText` name for
      sync; the async counterpart is `ReadAllTextAsync` — codegen
      MUST use the sync form to match the Dart sync call.

  - construct_key: dart.top_level_function_call.set_prelude_unit_clause_source_set_prelude_environment_source
    source_form: |-
      "setPreludeUnitClauseSource(source);
       setPreludeEnvironmentSource(source);"
    target_decision: >-
      Two Dart top-level setter functions imported with `show`-
      restriction from `compiler/partial_evaluator.dart` and
      `analysis/type_checker/type_environment_builder.dart`. Each is
      a `void`-returning function that mutates PROCESS-GLOBAL state
      consulted by every subsequent compile/typecheck operation.
      Per the host-spec convention (Dart top-level function -> C#
      `public static` method on a static host class), the C#
      callsites are:
      - `PartialEvaluator.SetPreludeUnitClauseSource(source);` (host
        class per lib/compiler/partial_evaluator.dart.md, residing
        in `<RootNs>.Compiler` namespace);
      - `TypeEnvironmentBuilder.SetPreludeEnvironmentSource(source);`
        (host class per lib/analysis/type_checker/
        type_environment_builder.dart.md, residing in
        `<RootNs>.Analysis.TypeChecker` namespace).
      The exact host-class names are owned by the SUT specs; THIS
      spec records the callsite shape only. Both calls are issued
      from the static constructor of `DynamicDispatchTest` (see the
      `main_entrypoint_with_pre_group_setup` construct above).
    idiom_id: null
    research_finding_id: rf-dart-toplevel-setter-fn-to-csharp-static-method
    nuance: >-
      Top-level-function nuance (explicitly addressed, cross-
      references with rpc_routing_test's `activateModule` and
      module_activation_test's `compileModules`): Dart top-level
      functions become `public static` methods on host static
      classes; the host class name is conventionally derived from
      the source filename (e.g. `partial_evaluator.dart` ->
      `PartialEvaluator`). Process-global-mutation nuance
      (LOAD-BEARING — explicitly addressed): both setters mutate
      module-level state. The SUT specs MUST pin the storage as
      `private static` field on the host class — codegen MUST NOT
      route the state through an instance (which would defeat the
      cross-test sharing). Idempotency nuance: invoking the setter
      multiple times with the same value is a no-op (overwrite-
      with-same); invoking with different values OVERWRITES — the
      test relies on a single invocation in the static constructor
      to bootstrap. Show-clause nuance (cached): Dart `show
      <symbol>` is lost in C# `using <Ns>;` translation — the host
      class is fully accessible via the namespace.

  - construct_key: dart.local_var.string_literal_for_relative_dir_path
    source_form: "final ddDir = '../programs/tests/dynamic_dispatch';"
    target_decision: >-
      Dart `final ddDir = '<literal>';` at `main`-scope, read by
      every end-to-end test's compile call. Map to a `private const
      string DdDir = "../programs/tests/dynamic_dispatch";` field
      on the test class. `const` (vs `static readonly`) is
      appropriate because the value is a compile-time string
      literal — Microsoft Learn "constants":
      https://learn.microsoft.com/dotnet/csharp/programming-guide/
      classes-and-structs/constants. PascalCase per .NET naming
      guideline for constants.
    idiom_id: null
    research_finding_id: rf-dart-final-string-literal-shared-across-tests-to-csharp-private-const
    nuance: >-
      FIRST-SEEN idiom row (subtle — distinct from
      `rf-dart-final-local-to-csharp-var-local` because the Dart
      `final ddDir` is at `main` scope and is READ by every test
      closure via closure capture; in C# closures cannot capture
      across `[Fact]` methods, so the value must be hoisted to a
      static or instance field). Compile-time-constant nuance
      (explicitly addressed): the value is a string LITERAL with
      no interpolation and no method calls — `const` is the
      strictest binding. Naming nuance: Dart `ddDir`
      (lowerCamelCase) -> C# `DdDir` (PascalCase per .NET
      "Naming Constants":
      https://learn.microsoft.com/dotnet/standard/design-guidelines/
      capitalization-conventions). Visibility nuance: `private` is
      sufficient since the constant is only read by tests within
      the same class.

  - construct_key: dart.local_var.final_constructor_invocation_glp_compiler
    source_form: "final compiler = GlpCompiler();"
    target_decision: >-
      Dart `final compiler = GlpCompiler();` (no-args ctor; type
      inferred from RHS) maps to C# `var compiler = new GlpCompiler();`
      (method-local lifetime). SUT type per lib/compiler/compiler.
      dart.md.
    idiom_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    research_finding_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    nuance: >-
      Cached idiom (precedent: module_activation_test,
      rpc_routing_test). `new` keyword nuance (cached): C# requires
      `new`; Dart 2.x dropped it. Per-test allocation nuance: every
      end-to-end test allocates its own `GlpCompiler` (four
      allocations across the four end-to-end tests) — codegen MUST
      keep one `var compiler = new GlpCompiler();` per method-local
      scope.

  - construct_key: dart.local_var.final_constructor_invocation_glp_runtime
    source_form: "final rt = GlpRuntime();"
    target_decision: >-
      `var rt = new GlpRuntime();` (no-args ctor; per lib/runtime/
      runtime.dart.md). Same precedent as
      module_activation_test/rpc_routing_test.
    idiom_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    research_finding_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    nuance: >-
      Cached idiom. Default-construction nuance: every `GlpRuntime`
      gets a fresh `HeapFCP`, `GoalQueue`, registries (per the
      `??`-defaulted constructor pinned by runtime.dart.md). Per-
      test isolation by allocation — under the inherited single-
      owning-context invariant from heap_fcp.dart.md escalations[0]
      this is trivially safe at test-method scope.

  - construct_key: dart.constructor_call.glp_engine_with_named_arg_root_self_glp_path
    source_form: |-
      "GlpEngine(
          rootSelfGlpPath: File('../programs/self.glp').absolute.path);"
    target_decision: >-
      Dart `GlpEngine({required String rootSelfGlpPath})` (single
      required-named param per lib/engine/glp_engine.dart.md). C#
      counterpart: `new GlpEngine(rootSelfGlpPath:
      Path.GetFullPath("../programs/self.glp"))`. C# supports
      named-argument syntax for any non-optional parameter (named
      args for clarity, NOT correctness; the SUT-side signature is
      positional or all-required per glp_engine.dart.md). The
      `.absolute.path` Dart-side accessor chain maps to C# `Path.
      GetFullPath(<rel-path>)` (Microsoft Learn:
      https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath
      — "the absolute path equivalent to the specified path"). The
      converted constructor is invoked TWICE in this file (test 1
      "serve/2 compiles and has label" + each end-to-end test
      that allocates its own engine — see test 2/3/4/5 which
      construct an engine for `engine.serveBytecode`).
    idiom_id: rf-dart-required-named-args-to-csharp-named-args
    research_finding_id: rf-dart-required-named-args-to-csharp-named-args
    nuance: >-
      Cached idiom (precedent: rpc_routing_test
      `activateModule`/`Scheduler` named-required args). Absolute-
      path nuance (explicitly addressed, FIRST-SEEN sub-case):
      Dart `File.absolute.path` returns a normalised absolute path
      (forward-slashes on POSIX, backslashes on Windows — but
      Dart's `Path` provides cross-platform behaviour). C# `Path.
      GetFullPath` uses `Directory.GetCurrentDirectory` as the
      base for relative paths (Microsoft Learn docs), matching the
      documented Dart contract. NULL-safety nuance: `rootSelfGlpPath`
      is `required String` in Dart (compile-time enforced); C#
      counterpart is a non-nullable `string` positional or named
      argument (NOT `string?`) — pinned by glp_engine.dart.md.

  - construct_key: dart.method_call.glp_compiler_compile_string
    source_form: |-
      "compiler.compile(File('../programs/self.glp').readAsStringSync())
       compiler.compile(mathSource)
       compiler.compile(source)"
    target_decision: >-
      Dart `GlpCompiler.compile(String source) -> BytecodeProgram`
      maps to C# `compiler.Compile(string source)` returning
      `BytecodeProgram` per lib/compiler/compiler.dart.md. PascalCase
      method name. Callsite shape:
      `var rootSelfBytecode = compiler.Compile(File.ReadAllText("../programs/self.glp"));`
      etc. Used 6× across the four end-to-end tests (3 for root
      self.glp + 3 for the module source + 1 for single_export).
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Cached idiom. Single positional `String` parameter -> `string`
      — trivial. Synchronous-call nuance: `compile` is sync per
      compiler.dart.md; no `async`/`await` shape.

  - construct_key: dart.method_call.bytecode_program_merge
    source_form: |-
      "compiler.compile(mathSource).merge(rootSelfBytecode)
       compiler.compile(source).merge(rootSelfBytecode)"
    target_decision: >-
      Dart `BytecodeProgram.merge(BytecodeProgram other) ->
      BytecodeProgram` — combines two compiled bytecodes (per
      lib/bytecode/runner.dart.md — pinned by bytecode/runner.dart.md
      as a method that returns a NEW `BytecodeProgram` with both
      label tables and instruction streams unified). C# counterpart:
      `mathBytecode.Merge(rootSelfBytecode)` — PascalCase method
      name; same signature and return type. Used 4× (once per end-
      to-end test) to merge the per-test module bytecode with the
      pre-compiled root self.glp arithmetic primitives.
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Cached idiom (naming-convention). Return-type nuance
      (explicitly addressed): does `Merge` return a NEW
      `BytecodeProgram` or mutate the receiver? Per
      bytecode/runner.dart.md — IMMUTABLE merge (returns a new
      instance) is the convention; the C# counterpart MUST match.
      The test code immediately assigns to a new local (`final
      mathBytecode = compiler.compile(...).merge(rootSelfBytecode);`),
      consistent with either contract — but lib/bytecode/runner.
      dart.md is authoritative. Fluent-chaining nuance: the source
      uses fluent chaining (`compiler.compile(src).merge(rootSelfBytecode)`);
      C# supports the identical pattern (`compiler.Compile(src).Merge(rootSelfBytecode)`)
      — no rewrite needed.

  - construct_key: dart.member_access.glp_engine_serve_bytecode_getter
    source_form: "engine.serveBytecode"
    target_decision: >-
      Dart `GlpEngine.serveBytecode -> BytecodeProgram` (a getter
      per lib/engine/glp_engine.dart.md). C# counterpart:
      `engine.ServeBytecode` (PascalCase property). Used 5× across
      the file (one per test that compiles serve/2). Property
      semantics: read-only (Dart `final` field or getter -> C#
      `{ get; }` auto-property or read-only field).
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Cached idiom (PascalCase rename for property access).
      Property-vs-method nuance (explicitly addressed): Dart `final
      <Type> serveBytecode` (or getter) — both read as
      `engine.serveBytecode` (NO parentheses); C# property
      `ServeBytecode { get; }` reads identically (NO parentheses).
      Method-call alternative (`ServeBytecode()`) is REJECTED —
      property is the more idiomatic match. Engine-instance nuance:
      every end-to-end test constructs its own `GlpEngine`
      (allocation inside the test method) and reads
      `.serveBytecode` once; codegen preserves the per-test
      allocation pattern.

  - construct_key: dart.member_access.bytecode_program_labels_contains_key
    source_form: "engine.serveBytecode.labels.containsKey('serve/2')"
    target_decision: >-
      Dart `BytecodeProgram.labels -> Map<String, int>` (per
      lib/bytecode/runner.dart.md `labels` field — keyed by
      procedure-id string, valued by int PC offset). C# counterpart
      depends on bytecode/runner.dart.md's choice: PascalCase
      `Labels` property typed as `Dictionary<string, long>` (or
      `IReadOnlyDictionary<string, long>`). `Map.containsKey(K)`
      -> `Dictionary.ContainsKey(TKey)`. Callsite shape:
      `engine.ServeBytecode.Labels.ContainsKey("serve/2")` returning
      `bool`. Wrapped in `Assert.True(...)` per the boolean-matcher
      construct below.
    idiom_id: rf-dart-map-containskey-to-csharp-dictionary-containskey
    research_finding_id: rf-dart-map-containskey-to-csharp-dictionary-containskey
    nuance: >-
      Cached idiom (precedent: module_activation_test
      `rt.runners.containsKey(mods.serve)`). Map-vs-Dictionary
      nuance (cached): Dart `Map<String, int>` -> C# `Dictionary<string,
      long>` (the int-width promotion per the
      `rf-dart-int-to-csharp-long-width` precedent — PC offsets are
      addresses). Key-equality nuance: Dart `String` and C# `string`
      both use value-equality for keys (string interning / `Object.
      Equals(string, string)` -> ordinal equality).

  - construct_key: dart.constructor_call.struct_term_with_const_term_var_ref_args
    source_form: |-
      "StructTerm('double', [ConstTerm(5), VarRef(fWriter)]);
       StructTerm('triple', [ConstTerm(4), VarRef(fWriter)]);
       StructTerm('nonexistent', [ConstTerm(42)]);
       StructTerm('inc', [ConstTerm(7), VarRef(fWriter)]);"
    target_decision: >-
      Map to `new StructTerm("<functor>", new List<Term> { new
      ConstTerm(<lit>), new VarRef(<addr>) })` per lib/runtime/
      terms.dart.md. Carry-forward from binding_pointer_test.dart.md
      and module_activation_test.dart.md (cached pattern). Each
      `ConstTerm(<lit>)` wraps the int literal into `object?
      Value`. `VarRef(<addr>)` wraps the writer address (a `long`
      heap address) into a value-typed `VarRef` Term subtype.
    idiom_id: rf-dart-sumleaf-with-list-no-eq-to-csharp-class-ireadonlylist
    research_finding_id: rf-dart-list-literal-to-csharp-list-of-T
    nuance: >-
      Cached idiom (list-literal -> `new List<Term> { ... }`).
      Mixed-element nuance (explicitly addressed): the list
      contains TWO distinct Term subtypes (`ConstTerm` + `VarRef`);
      both implement the `Term` base type (sealed abstract per
      terms.dart.md). `List<Term>` accepts both. Literal-boxing
      nuance: `ConstTerm(5)` boxes `int` -> `object?`; `ConstTerm(42)`
      similarly. Reference-identity nuance: `StructTerm`/`ConstTerm`/
      `VarRef` are sealed C# classes preserving reference identity
      per terms.dart.md. `VarRef(<addr>)` carries a `long`
      address per terms.dart.md (cached
      `rf-dart-int-to-csharp-long-width`).

  - construct_key: dart.constructor_call.const_term_int_literal
    source_form: |-
      "ConstTerm(5)
       ConstTerm(4)
       ConstTerm(42)
       ConstTerm(7)"
    target_decision: >-
      `new ConstTerm(<int literal>)` per lib/runtime/terms.dart.md
      construct `dart.sum_type_leaf.value_carrying_no_eq_override_
      reference_identity`. The C# constructor accepts `object?` —
      the `int` literal boxes into it.
    idiom_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      Cached idiom (precedent: every prior test that constructs
      Term values). Boxing nuance (cached): Dart `int` -> C# `int`
      (32-bit if implicit) boxed into `object?` Value; the `int`
      vs `long` choice at the BOX layer follows lib/runtime/terms.
      dart.md — typically `int` is fine since `ConstTerm.Value` is
      `object?` (no addressing arithmetic involved). Per-callsite:
      `new ConstTerm(5)`, `new ConstTerm(4)`, `new ConstTerm(42)`,
      `new ConstTerm(7)`.

  - construct_key: dart.constructor_call.var_ref_long_address
    source_form: "VarRef(fWriter)"
    target_decision: >-
      `new VarRef(fWriter)` per lib/runtime/terms.dart.md `VarRef`
      class — a `Term` subtype carrying a single `long` heap
      address. The `fWriter` local is obtained from
      `rt.heap.allocateVariable()` (a record-destructured
      `(int, int)` pair — Dart-3 positional records — see
      `dart.dart_3_destructuring_pattern.heap_allocate_variable`
      below). The `int` typing on the heap-address local is the
      `long`-widened width per the cells.dart.md precedent.
    idiom_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    research_finding_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    nuance: >-
      Cached idiom (PascalCase + `new` keyword). Width nuance
      (cached): heap-address payload is `long`. Used 3× — every
      end-to-end test that allocates a fresh writer for the result
      F-variable.

  - construct_key: dart.dart_3_destructuring_pattern.heap_allocate_variable
    source_form: "final (fWriter, _) = rt.heap.allocateVariable();"
    target_decision: >-
      Dart-3 destructuring pattern: `final (fWriter, _) = <expr>;`
      destructures a POSITIONAL record (Dart 3.0+ pattern syntax,
      Dart docs:
      https://dart.dev/language/patterns#destructuring) where the
      RHS returns `(int, int)`. The `_` placeholder discards the
      second element. C# counterpart: tuple destructuring with
      `_` discard pattern (Microsoft Learn "Deconstruct - tuple
      types":
      https://learn.microsoft.com/dotnet/csharp/fundamentals/
      functional/deconstruct, "Discards":
      https://learn.microsoft.com/dotnet/csharp/fundamentals/
      functional/discards). Callsite shape:
      `var (fWriter, _) = rt.Heap.AllocateVariable();` — C# 7+
      tuple deconstruction with discard. Used 3× (in the three
      end-to-end tests that allocate a writer).
    idiom_id: null
    research_finding_id: rf-dart-record-destructure-with-discard-to-csharp-tuple-deconstruct
    nuance: >-
      FIRST-SEEN idiom row. Discard-pattern nuance (explicitly
      addressed): Dart `_` in a destructuring pattern discards the
      element (no name binding); C# `_` is also the discard
      "variable" — both languages support identical syntax. SUT-
      return-type nuance: the source `rt.heap.allocateVariable()`
      MUST be declared in lib/runtime/heap_fcp.dart.md as
      returning a POSITIONAL record `(int writer, int reader)` in
      Dart and a POSITIONAL tuple `(long Writer, long Reader)` in
      C#. The destructured names (`fWriter`) at the test site are
      LOCAL; they do NOT need to match the tuple element names
      (Dart and C# both allow renaming on destructure). Width
      nuance (cached): heap addresses are `long` in C# — the
      destructured local types are inferred from the SUT return
      type. Alternative C# form: `var alloc = rt.Heap.
      AllocateVariable(); var fWriter = alloc.Item1;` (positional
      access) — REJECTED in favour of the canonical
      `(fWriter, _)` deconstruction form (matches Dart shape).

  - construct_key: dart.method_call.heap_dereference_var_ref
    source_form: "rt.heap.dereference(VarRef(fWriter))"
    target_decision: >-
      Dart `HeapFCP.dereference(Term) -> Term` (per lib/runtime/
      heap_fcp.dart.md). C# counterpart: `rt.Heap.Dereference(new
      VarRef(fWriter))` returning `Term`. PascalCase rename. The
      method walks the writer-MGU chain to the bound value; for
      this test, after a successful drain, `dereference` returns a
      `ConstTerm` carrying the computed result (10, 12, 8
      respectively).
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Cached idiom. Return-type nuance (explicitly addressed):
      `dereference` returns `Term` (base type) — the test
      immediately asserts the runtime type via `isA<ConstTerm>`
      and casts via `as ConstTerm` to read `.value`. The two-step
      assert+cast pattern is preserved in the C# port via
      `Assert.IsType<ConstTerm>(fValue); ... ((ConstTerm)fValue).
      Value` (see `dart.package_test.expect_isA_runtime_type` and
      `dart.cast_operator.as_term_subtype` below). Threading
      nuance (INHERITED): `dereference` reads the heap (potentially
      following writer chains via writer-MGU); under the inherited
      single-owning-context invariant this is safe at test-method
      scope.

  - construct_key: dart.member_access.glp_channel_handle_send
    source_form: |-
      "final woken = handle.send(goal);
       handle.send(goal)"
    target_decision: >-
      Dart `GlpChannelHandle.send(Term goal) -> List<GoalRef>`
      mapped to C# `handle.Send(goal) -> List<GoalRef>` per lib/
      runtime/glp_activation.dart.md. The method MUTATES the handle
      (`_writerAddr` advanced per glp_activation.dart.md) and
      returns the activation list. Callsite shape: `var woken =
      handle.Send(goal);`. Used 4× across the end-to-end tests.
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Cached idiom (precedent: module_activation_test, rpc_
      routing_test). Method-mutation nuance (cached): `send`
      mutates `_writerAddr` in place; `GlpChannelHandle` is a
      reference type. Return-type nuance: `List<GoalRef>` (cached
      idiom `rf-dart-list-of-T-to-csharp-list-of-T`).

  - construct_key: dart.statement.for_in_loop_over_woken_iterable
    source_form: |-
      "for (final g in woken) {
         rt.gq.enqueue(g);
       }"
    target_decision: >-
      Dart `for (final <var> in <iterable>) { ... }` maps to C#
      `foreach (var <var> in <iterable>) { ... }`. Spec form:
      `foreach (var g in woken) { rt.Gq.Enqueue(g); }`. Used 4×
      across the file (one per end-to-end test).
    idiom_id: rf-dart-for-in-final-to-csharp-foreach-var
    research_finding_id: rf-dart-for-in-final-to-csharp-foreach-var
    nuance: >-
      Cached idiom (precedent: module_activation_test). For-in-vs-
      foreach nuance (cached): semantically equivalent, iterator-
      driven, read-only loop variable. The `woken` collection is
      `List<GoalRef>`; both languages iterate sequentially. Empty-
      collection nuance: when the send returns empty (no goals
      woken — e.g. if the channel is not yet listening), neither
      loop executes — semantics preserved.

  - construct_key: dart.method_call.goal_queue_enqueue
    source_form: "rt.gq.enqueue(g);"
    target_decision: >-
      Dart `GoalQueue.enqueue(GoalRef r)` -> C# `GoalQueue.
      Enqueue(GoalRef r)` per lib/runtime/machine_state.dart.md.
      PascalCase rename. `rt.gq` -> `rt.Gq` (or `rt.GoalQueue` —
      property name owned by runtime.dart.md).
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Cached idiom (precedent: module_activation_test,
      rpc_routing_test). `GoalRef` value-type nuance: `readonly
      record struct` per machine_state.dart.md — the `Enqueue(g)`
      call boxes nothing because the queue is `Queue<GoalRef>`
      (concrete value-type element). Property-abbreviation nuance:
      `gq` vs `GoalQueue` — defer to runtime.dart.md (working
      assumption: `Gq` to preserve source-shape minimality).

  - construct_key: dart.function_call.activate_module_named_args
    source_form: |-
      "activateModule(
         rt: rt,
         serveBytecode: serveBytecode,
         moduleBytecode: mathBytecode,
         moduleName: 'math_service',
       );"
    target_decision: >-
      Dart top-level function `activateModule({required GlpRuntime
      rt, required BytecodeProgram serveBytecode, required
      BytecodeProgram moduleBytecode, required String moduleName})`
      returns `GlpChannelHandle`. Per lib/runtime/glp_activation.
      dart.md the C# host is a static method on a static class.
      Spec form: `GlpActivation.ActivateModule(rt: rt,
      serveBytecode: serveBytecode, moduleBytecode: mathBytecode,
      moduleName: "math_service")`. Used 4× (once per end-to-end
      test) with the moduleName parameter varying:
      `"math_service"` (3×) and `"single"` (1×).
    idiom_id: rf-dart-required-named-args-to-csharp-named-args
    research_finding_id: rf-dart-required-named-args-to-csharp-named-args
    nuance: >-
      Cached idiom (precedent: module_activation_test,
      rpc_routing_test). Required-named nuance (cached): Dart
      `required` -> C# non-optional parameter; named-argument
      syntax preserved at callsite for clarity. String-literal
      nuance: Dart `'math_service'` -> C# `"math_service"` (single
      to double quote).

  - construct_key: dart.member_access.glp_runtime_glp_channels_contains_key
    source_form: |-
      "rt.glpChannels.containsKey('math_service')"
    target_decision: >-
      Dart `Map<String, GlpChannelHandle>.containsKey(String)` ->
      C# `Dictionary<string, GlpChannelHandle>.ContainsKey(string)`
      per lib/runtime/runtime.dart.md (or
      `IReadOnlyDictionary<string, GlpChannelHandle>.ContainsKey`
      depending on the SUT property type). Callsite shape:
      `rt.GlpChannels.ContainsKey("math_service")`. Wrapped in
      `Assert.True(...)` (see boolean-matcher construct below).
      Used 1× (in the first end-to-end test).
    idiom_id: rf-dart-map-containskey-to-csharp-dictionary-containskey
    research_finding_id: rf-dart-map-containskey-to-csharp-dictionary-containskey
    nuance: >-
      Cached idiom (precedent: rpc_routing_test
      `rt.glpChannels.containsKey('target_b')`). String-key
      equality nuance: ordinal-equality on both sides.

  - construct_key: dart.constructor_call.scheduler_with_rt_named_arg
    source_form: "final scheduler = Scheduler(rt: rt);"
    target_decision: >-
      Dart `Scheduler({required GlpRuntime rt, BytecodeRunner?
      runner, Map<Object?, BytecodeRunner>? runners, void
      Function(String)? traceSink})` -> C# `new Scheduler(rt: rt)`
      per lib/runtime/scheduler.dart.md. NO `traceSink` argument
      in this file (unlike rpc_routing_test which used `traceSink:
      (s) => trace.add(s)`) — this file relies on `result.status`
      assertions only.
    idiom_id: rf-dart-required-named-args-to-csharp-named-args
    research_finding_id: rf-dart-required-named-args-to-csharp-named-args
    nuance: >-
      Cached idiom (precedent: module_activation_test,
      rpc_routing_test). Single-required-arg nuance: only `rt:`
      is provided; the optional `runner`/`runners`/`traceSink`
      parameters default to null. C# named-argument syntax is
      idiomatic here for clarity. Used 4× across the end-to-end
      tests.

  - construct_key: dart.method_call.scheduler_drain_with_status_max_cycles
    source_form: |-
      "var result = scheduler.drainWithStatus(maxCycles: 300);
       result = scheduler.drainWithStatus(maxCycles: 10000);
       scheduler.drainWithStatus(maxCycles: 300);
       scheduler.drainWithStatus(maxCycles: 10000);
       final result = scheduler.drainWithStatus(maxCycles: 5000);"
    target_decision: >-
      Dart `Scheduler.drainWithStatus({int maxCycles = ..., bool
      debug = false, bool showBindings = true, bool debugOutput =
      false}) -> DrainResult` -> C# `scheduler.DrainWithStatus
      (maxCycles: <int>)` per lib/runtime/scheduler.dart.md.
      Synchronous return; the `result.Status` enum is observed
      directly on the next line. PascalCase + named-argument
      syntax preserved.
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Cached idiom (precedent: rpc_routing_test). Reassignment
      nuance (carry-forward): `var result = scheduler.
      drainWithStatus(...)` followed by `result = scheduler.
      drainWithStatus(...)` reassigns — both Dart `var` and C# `var`
      permit reassignment. The fifth callsite uses `final result`
      (single-assignment) — C# emits `var result = ...;` (no
      `readonly` modifier on a local). MAX-CYCLES literal range:
      300, 5000, 10000 — all small `int` literals, NOT requiring
      `long` widening (the SUT parameter type is `int maxCycles`
      per scheduler.dart.md — bounded recursion budget, NOT a
      heap address). Sync nuance (LOAD-BEARING — INHERITED): the
      drain is synchronous; the assertion immediately afterwards
      observes the return value. NO `async Task` shape.

  - construct_key: dart.member_access.drain_result_status
    source_form: "result.status"
    target_decision: >-
      Dart `DrainResult.status -> ExecutionStatus` -> C# `DrainResult.
      Status` per scheduler.dart.md (`public ExecutionStatus
      Status { get; }`). PascalCase property access.
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Cached idiom. Read-only-property nuance: `DrainResult.Status`
      is `{ get; }` only (the test never assigns it).

  - construct_key: dart.enum_member_access.execution_status_succeeded
    source_form: "ExecutionStatus.succeeded"
    target_decision: >-
      `ExecutionStatus.Succeeded` per scheduler.dart.md (PascalCase
      member). Three uses in this file: two in the
      `'activate module and dispatch double...'` test, one in the
      `'unknown goal does not crash (fallback)'` test.
    idiom_id: rf-dart-plain-enum-to-csharp-enum
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Cached idiom (precedent: module_activation_test, rpc_routing_
      test). Casing-correction nuance (cached): Dart `lowerCase`
      enum members -> C# `PascalCase`.

  - construct_key: dart.package_test.expect_equals
    source_form: |-
      "expect(rt.glpChannels.containsKey('math_service'), isTrue);
       expect(engine.serveBytecode.labels.containsKey('serve/2'), isTrue);
       expect(result.status, equals(ExecutionStatus.succeeded));     // ×3
       expect((fValue as ConstTerm).value, equals(10));
       expect((fValue as ConstTerm).value, equals(12));
       expect((fValue as ConstTerm).value, equals(8));"
    target_decision: >-
      Dart `expect(<actual>, equals(<expected>))` -> xUnit
      `Assert.Equal(<expected>, <actual>)` — ARGUMENT-ORDER FLIP
      (cached idiom). Dart `expect(<bool-expr>, isTrue)` -> xUnit
      `Assert.True(<bool-expr>)`. Per-callsite mapping:
      - `expect(rt.glpChannels.containsKey('math_service'), isTrue)`
        -> `Assert.True(rt.GlpChannels.ContainsKey("math_service"));`
      - `expect(engine.serveBytecode.labels.containsKey('serve/2'),
        isTrue)` -> `Assert.True(engine.ServeBytecode.Labels.
        ContainsKey("serve/2"));`
      - `expect(result.status, equals(ExecutionStatus.succeeded))`
        -> `Assert.Equal(ExecutionStatus.Succeeded, result.Status);`
      - `expect((fValue as ConstTerm).value, equals(10))`
        -> `Assert.Equal(10, ((ConstTerm)fValue).Value);`
        (and the 12, 8 sibling callsites). Value-equality nuance:
        the boxed payload is `object?`, so `Assert.Equal` invokes
        the runtime-type's `Equals` (int autoboxes to `int.Equals`
        / `object.Equals` -> ordinal equality).
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Cached idiom (argument-order flip, well-known footgun).
      Boxed-int-equality nuance (explicitly addressed): the
      `ConstTerm.Value` payload is `object?`; comparing it to the
      C# literal `10` (`int`) via `Assert.Equal(10, value)`
      invokes the type-aware `EqualityComparer<object>.Default`
      which performs `value.Equals(10)`. Since the runtime type of
      the boxed value is `int` (or `long` after the heap arith
      finishes — DEPENDS on lib/runtime/system_predicates.dart.md's
      arith-result type), an `int`-vs-`long` mismatch could cause
      `Assert.Equal(10, /* long */ value)` to FAIL despite numeric
      equality. SUT-side ruling: per the system_predicates and
      heap_fcp precedents, arithmetic results are `int`-boxed
      (NOT `long`-boxed) at the `ConstTerm.Value` layer — the
      C# port should emit `Assert.Equal(10, ((ConstTerm)fValue).
      Value);` and rely on `int`-boxing. If the SUT spec decides
      `long`-boxing, codegen MUST emit `Assert.Equal(10L, ...)`.
      RECOMMENDED form for THIS spec: `Assert.Equal(10,
      ((ConstTerm)fValue).Value);` matching the source's `int 10`
      literal.

  - construct_key: dart.package_test.expect_equals_with_reason
    source_form: |-
      "expect(fValue, isA<ConstTerm>(),
         reason: 'F should be bound to a constant (10)');"
    target_decision: >-
      Dart `expect(actual, matcher, reason: 'msg')` — xUnit's
      `Assert.IsType<T>(object actual)` does NOT accept a custom-
      message overload. Per the rpc_routing_test precedent
      (rf-dart-expect-with-reason-to-xunit-comment-or-asserttrue),
      preferred strategy: comment-retention. Spec form:
      `Assert.IsType<ConstTerm>(fValue); // reason: F should be
      bound to a constant (10)`. Alternative form: `Assert.True
      (fValue is ConstTerm, "F should be bound to a constant
      (10)");` — loses the structured `IsType` diagnostic but
      preserves the message at runtime. THIS spec recommends the
      COMMENT-RETENTION strategy for documentation parity.
    idiom_id: rf-dart-expect-with-reason-to-xunit-comment-or-asserttrue
    research_finding_id: rf-dart-expect-with-reason-to-xunit-comment-or-asserttrue
    nuance: >-
      Cached idiom (precedent: module_activation_test reason-
      handling). Reason-vs-message nuance (cached): xUnit's
      structured matchers (`IsType`, `Equal`, `Contains`) deliberately
      lack message overloads in v2; the comment-retention strategy
      preserves the documentation provenance for PR review.

  - construct_key: dart.package_test.expect_isA_runtime_type
    source_form: |-
      "expect(fValue, isA<ConstTerm>(), reason: 'F should be bound to a constant (10)');
       expect(fValue, isA<ConstTerm>());     // ×2 (triple, single_export)"
    target_decision: >-
      Dart `expect(<actual>, isA<T>())` is a `package:test`
      matcher asserting the runtime type of `actual` is `T` or a
      subtype. xUnit counterpart: `Assert.IsType<T>(object actual)`
      — asserts EXACT type match (Microsoft Learn / xunit.net
      docs:
      https://xunit.net/docs/comparisons#assertions —
      `Assert.IsType<T>`); if subtype-acceptance is needed, use
      `Assert.IsAssignableFrom<T>(actual)`. Dart `isA<T>()` accepts
      `T` or any subtype (per package:test docs:
      https://pub.dev/documentation/test/latest/test/isA.html).
      The strict counterpart is therefore `Assert.IsAssignableFrom
      <ConstTerm>(fValue);` BUT for the test author's intent —
      "the dereferenced Term is precisely a `ConstTerm`" — the
      stricter `Assert.IsType<ConstTerm>(fValue);` is the IDIOMATIC
      C# choice because `ConstTerm` is a sealed/concrete class
      (per terms.dart.md — Term-leaf classes are sealed). Under
      the sealed-leaf invariant, `IsType` and `IsAssignableFrom`
      are observably equivalent. PREFERRED: `Assert.IsType<ConstTerm>
      (fValue);` (matches the source's strict intent).
    idiom_id: null
    research_finding_id: rf-dart-expect-isA-T-to-xunit-assert-istype
    nuance: >-
      FIRST-SEEN idiom row. Runtime-type-check nuance (explicitly
      addressed): Dart `isA<T>()` is subtype-accepting; C#
      `Assert.IsType<T>` is exact-type-only — DIFFERENT contract.
      The mismatch is OBSERVABLY HIDDEN when `T` is a sealed/
      concrete class with no subclasses (as `ConstTerm` is per
      terms.dart.md sealed Term-leaf decision). If a future
      conversion exercises `isA<Term>` (the base class), codegen
      MUST switch to `Assert.IsAssignableFrom<Term>(actual)` to
      preserve subtype-acceptance. Recommended-form recap:
      `Assert.IsType<ConstTerm>(fValue);` is the canonical form
      for THIS file because the test author DELIBERATELY asserts
      the exact leaf type, AND the leaf is sealed. Used 3× (one
      per end-to-end test that reads back F).

  - construct_key: dart.cast_operator.as_term_subtype
    source_form: |-
      "(fValue as ConstTerm).value"
    target_decision: >-
      Dart `<expr> as T` is a runtime-checked cast (throws
      `TypeError` on mismatch). C# counterpart: `(T)<expr>` cast
      expression (throws `InvalidCastException` on mismatch) —
      observable contract match. Spec form:
      `((ConstTerm)fValue).Value` — note the EXTRA parentheses
      needed around the cast to bind `Value` to the cast result
      (NOT to the original `fValue`). Microsoft Learn "Cast
      expression":
      https://learn.microsoft.com/dotnet/csharp/language-reference/
      operators/type-testing-and-cast.
      Used 3× (each end-to-end test reads `(fValue as ConstTerm).
      value`).
    idiom_id: null
    research_finding_id: rf-dart-as-cast-to-csharp-cast-expression
    nuance: >-
      FIRST-SEEN idiom row. Cast-operator nuance (explicitly
      addressed): Dart `as T` THROWS on mismatch (Dart docs:
      "as operator", https://dart.dev/language/operators#type-test-
      operators); C# `(T)x` ALSO throws on mismatch — semantics
      preserved. Dart has a SEPARATE null-safety nullable-cast
      operator (`x as T?`) that does NOT throw on null — NOT
      USED HERE. C# has a SEPARATE `x as T` (yes — same syntax,
      DIFFERENT semantics in C#) that returns `null` on mismatch
      INSTEAD of throwing (Microsoft Learn "as operator":
      https://learn.microsoft.com/dotnet/csharp/language-reference/
      operators/type-testing-and-cast#as-operator). FOOTGUN — Dart
      `x as T` and C# `x as T` have INVERTED semantics on mismatch
      (throw vs null). Codegen MUST translate Dart `<expr> as T`
      to C# `(T)<expr>` (the parenthesised cast, NOT `<expr> as T`)
      to preserve the throw-on-mismatch contract. Codegen MUST
      NOT translate to C# `as`. This nuance is LOAD-BEARING and
      cached for every future Dart `as` translation.

  - construct_key: dart.member_access.const_term_value_getter
    source_form: "(fValue as ConstTerm).value"
    target_decision: >-
      Dart `ConstTerm.value -> dynamic` (or `Object?` — per
      terms.dart.md the field is typed `Object? value`). C#
      counterpart: `((ConstTerm)fValue).Value` returning `object?`
      per terms.dart.md. PascalCase property rename.
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Cached idiom (precedent: every prior file constructing/
      reading ConstTerm). Boxed-value nuance: `ConstTerm.Value`
      is `object?` — accessing it does NOT unbox; the `Assert.
      Equal(<int literal>, value)` invokes `EqualityComparer<object>.
      Default` which auto-unboxes for value-type equality. See
      the `expect_equals` construct's int-vs-long discussion.

conversion_units:
  - test/DynamicDispatchTest.cs

escalations: []
```

---

## Embedded rationale + provenance (prose)

This file converts as a single `DynamicDispatchTest` xUnit test
class (PascalCase from the source filename) inside `<RootNs>.Test`,
with five `[Fact]` methods (one per Dart `test()`), partitioned
by `[Trait("Group", ...)]` into `"serve/2"` (1 test) and
`"end-to-end dispatch"` (4 tests). Every non-trivial construct
maps to a cached idiom recorded by an earlier convspec — primarily
`module_activation_test.dart.md` and `rpc_routing_test.dart.md`
(same module-dispatch family; same `activateModule`/`Scheduler`/
`drainWithStatus`/`GlpChannelHandle` surface), and
`binding_pointer_test.dart.md` for the `rt.heap.*` / `VarRef` /
`ConstTerm` surface.

### Notable first-seen idioms recorded for this file

1. **`dart:io` File API**
   (`rf-dart-io-import-to-csharp-using-system-io`,
   `rf-dart-io-file-constructor-to-csharp-static-file-class`,
   `rf-dart-io-file-exists-sync-to-csharp-file-exists`,
   `rf-dart-io-file-read-as-string-sync-to-csharp-file-readalltext`).
   Authoritative: Dart `dart-io` library
   (https://api.dart.dev/stable/dart-io/), Microsoft Learn
   `System.IO.File`
   (https://learn.microsoft.com/dotnet/api/system.io.file). Dart's
   instance-style `File(p).readAsStringSync()` maps to C#'s
   STATIC `File.ReadAllText(p)` — the Dart `File(...)` instance
   is hoisted away because C# `File` is a static class.

2. **Dart 3 positional record destructuring with discard**
   (`rf-dart-record-destructure-with-discard-to-csharp-tuple-
   deconstruct`).
   Authoritative: Dart "Patterns" docs
   (https://dart.dev/language/patterns#destructuring), Microsoft
   Learn "Deconstruct - tuple types"
   (https://learn.microsoft.com/dotnet/csharp/fundamentals/
   functional/deconstruct), "Discards"
   (https://learn.microsoft.com/dotnet/csharp/fundamentals/
   functional/discards). `final (fWriter, _) = rt.heap.
   allocateVariable();` ↔ `var (fWriter, _) = rt.Heap.
   AllocateVariable();`.

3. **`expect(actual, isA<T>())` runtime-type assertion**
   (`rf-dart-expect-isA-T-to-xunit-assert-istype`).
   Authoritative: package:test `isA` matcher
   (https://pub.dev/documentation/test/latest/test/isA.html),
   xunit.net "Assertions"
   (https://xunit.net/docs/comparisons#assertions). LOAD-BEARING
   subtlety: Dart `isA<T>()` is subtype-accepting; xUnit
   `Assert.IsType<T>` is exact-type-only. The mismatch is
   observably hidden when `T` is a sealed leaf — as is the case
   for `ConstTerm` per terms.dart.md.

4. **Dart `as T` runtime-checked cast**
   (`rf-dart-as-cast-to-csharp-cast-expression`).
   Authoritative: Dart "Type test operators"
   (https://dart.dev/language/operators#type-test-operators),
   Microsoft Learn "Cast expression" + "as operator"
   (https://learn.microsoft.com/dotnet/csharp/language-reference/
   operators/type-testing-and-cast). LOAD-BEARING FOOTGUN: Dart
   `as T` throws on mismatch; C# `as T` returns null on mismatch
   (INVERTED semantics). Codegen MUST translate Dart `as T` to
   C# `(T)x` (parenthesised cast), NEVER to C# `as`. Recorded as
   a permanent cache entry to prevent every future spec from
   re-researching.

5. **Top-level setter functions for process-global init**
   (`rf-dart-toplevel-setter-fn-to-csharp-static-method`).
   Authoritative: Microsoft Learn "Static classes and static
   class members"
   (https://learn.microsoft.com/dotnet/csharp/programming-guide/
   classes-and-structs/static-classes-and-static-class-members).
   `setPreludeUnitClauseSource` and `setPreludeEnvironmentSource`
   are Dart top-level functions mutating module-level state; map
   to `public static` methods on their respective host static
   classes.

6. **`void main()` with pre-group init -> static constructor**
   (`rf-dart-test-main-with-init-to-xunit-static-ctor-plus-const`).
   Authoritative: Microsoft Learn "Static Constructors"
   (https://learn.microsoft.com/dotnet/csharp/programming-guide/
   classes-and-structs/static-constructors), xunit.net "Shared
   Context between Tests"
   (https://xunit.net/docs/shared-context). The Dart `main`
   conditional-prelude-bootstrap maps to a `static
   DynamicDispatchTest()` constructor running once per AppDomain.

### Threading-model inheritance

This file inherits the heap_fcp.dart.md escalations[0] threading-
model ruling without re-escalation (FR-013). The per-test
allocation pattern (every test owns its own `GlpRuntime` +
`Scheduler` + `GlpChannelHandle`) makes the inherited
single-owning-context option (option A — recommended) trivially
safe at test-method scope: every `rt.heap.*` / `rt.gq.*` /
`scheduler.drainWithStatus` call sees only state owned by the
calling test method, never touched by another thread.

### Cross-file invariants preserved

- `GlpEngine.serveBytecode` is a `BytecodeProgram` per
  lib/engine/glp_engine.dart.md; the C# property `ServeBytecode`
  is read-only.
- `BytecodeProgram.merge` returns a new `BytecodeProgram`
  (immutable merge, per lib/bytecode/runner.dart.md).
- `GlpChannelHandle` is a reference type (NOT a record or
  struct) — `handle.Send(goal)` mutates the in-place writer
  address per lib/runtime/glp_activation.dart.md.
- `ConstTerm` is a sealed/concrete Term leaf — `Assert.IsType<
  ConstTerm>` is strictly correct.

### Single-export module test (test 5)

The fifth end-to-end test (`single_export module: dispatch inc(7, F)
-> F = 8`) exercises a different GLP source file
(`single_export.glp`) but the conversion pattern is structurally
identical to the math_service tests — same `compiler.compile(...)`
+ `.merge(rootSelfBytecode)` + `activateModule(...)` + `Send` +
`drain` + `heap.dereference` + cast + value-check chain.

### No escalations

Every construct in this file resolves via a cached idiom (sibling
exemplars + first-seen-here idioms whose authoritative basis is
Dart/.NET official docs). No undecidable points are introduced at
THIS file's boundary; the inherited heap_fcp threading-model
escalation is NOT re-escalated per FR-013.
