# Conversion Spec — test/engine/glp_engine_test.dart

> Conversion-spec artifact for test/engine/glp_engine_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> File is a `package:test`-based integration suite (97 lines, 5
> `test()` cases nested inside ONE outer `group('GlpEngine', ...)`
> in `void main()`). It exercises the just-spec'd `GlpEngine` SUT
> (`.codeconv/conversion-specs/lib/engine/glp_engine.dart.md`,
> source_sha256 `966bf3b7fa4deb9baca2696f2c221bad3eed61f189de1c7080d409fdcdb5a8df`):
> construct via `GlpEngine(rootSelfGlpPath: ...)`, load three-line GLP
> source snippets via `loadSource`, await the async
> `runGoal(String) -> Future<ExecutionResult>` entry point, and
> assert on `ExecutionResult.succeeded`/`.failed`/`.status`/`.bindings`/
> `.error`.
>
> Heavy idiom REUSE — every cross-file dependency inherits the prior
> convspec decisions: the SUT `GlpEngine` / `ExecutionResult` / `loadSource`
> / `runGoal` shapes from glp_engine.dart.md; the `ExecutionStatus` enum
> from scheduler.dart.md; the `package:test` → xUnit project-wide pinning
> from boot_loader_test.dart.md / binding_pointer_test.dart.md /
> module_activation_test.dart.md / rpc_routing_test.dart.md. The xUnit
> conventions (`[Fact]` + `[Trait]` + `DisplayName` + per-method
> arrangement, `Assert.Equal(expected, actual)` ARG-FLIP, `Assert.True` /
> `Assert.NotNull`) are reused verbatim from the test exemplar set.
>
> THREADING-MODEL INHERITANCE NOTICE — this file exercises `GlpEngine`
> which owns a `GlpRuntime` / `GlpRuntime.heap` (HeapFCP) and a
> `Scheduler`. The .NET threading-model decision (single-owning-context
> per goal / non-concurrent collections vs `ConcurrentDictionary` /
> `Interlocked` etc.) is ESCALATED in `lib/runtime/heap_fcp.dart.md`
> escalations[0] and INHERITED through every dependent runtime spec
> (`lib/runtime/scheduler.dart.md`, `lib/runtime/runtime.dart.md`,
> `lib/runtime/glp_activation.dart.md`, `lib/bytecode/runner.dart.md`,
> `lib/engine/glp_engine.dart.md`). Per FR-013 + the scheduler /
> rpc_routing_test precedent, this file does NOT re-escalate the same
> question; it inherits the parent ruling. The 5 tests are each run on
> a SINGLE thread (xUnit per-instance fixture instantiation, no
> `Task.WhenAll` / parallelism inside the bodies), so the
> single-owning-context invariant is naturally satisfied.
>
> Load-bearing nuances exercised by THIS file:
> (a) `import 'dart:io';` → the test uses `File('../programs/self.glp').absolute.path`
>     to compute the rootSelfGlpPath at engine-construction time. The
>     RELATIVE path `'../programs/self.glp'` is resolved against the
>     CWD of the test runner (Dart `package:test` invokes the test from
>     the package root by default — i.e. `glp_runtime/`); the C# port
>     MUST preserve the relative path AND the CWD assumption. Mapping:
>     `new FileInfo("../programs/self.glp").FullName` (Microsoft Learn
>     'FileInfo.FullName Property' returns the absolute path), per the
>     SUT spec construct `dart.method.dart_io_file_absolute_path_property`
>     (idiom rf-dart-file-absolute-path-to-csharp-fileinfo-fullname).
> (b) `setUp(() { engine = GlpEngine(rootSelfGlpPath: ...); })` —
>     Dart `package:test` per-test setUp callback that constructs a
>     FRESH engine before each test. Maps to the xUnit constructor on
>     the test class (xUnit constructs a NEW class instance per test —
>     the same lifecycle Dart's `setUp` provides). Per the
>     boot_loader_test.dart.md precedent (idiom
>     rf-dart-package-test-setup-to-xunit-constructor), `late GlpEngine
>     engine;` field maps to `private readonly GlpEngine _engine;`
>     assigned in the ctor.
> (c) The five test bodies are all `async` closures returning `Future`.
>     The `runGoal` returns `Future<ExecutionResult>`, awaited in each
>     test. Maps to `async Task` test methods per the SUT's
>     `RunGoalAsync` signature (FR-024 cached idiom
>     rf-dart-future-async-await-to-csharp-task-async-await).
> (d) Each `engine.loadSource(<triple-quoted-multi-line-string>)` passes
>     a Dart triple-quoted raw multi-line string holding GLP source.
>     The strings contain interpolation-unsafe characters (the GLP
>     surface uses `?`, `:-`, `|`, `,` etc.) but NO Dart `$` interpolation.
>     Maps to C# verbatim string `@"..."` or C# 11+ raw-string-literal
>     `"""..."""` per the SUT spec construct
>     `dart.toplevel_const_string.raw_string_serve_source_embedded_glp_program`
>     (idiom rf-dart-raw-string-triple-quote-to-csharp-verbatim-or-raw-string-literal).
>     Codegen target version determines which.
> (e) `result.bindings['X']` — Map index by VARIABLE-NAME string,
>     returning `rt.Term?` (nullable). Dart `Map[k]` returns null on
>     miss; C# `IReadOnlyDictionary<string, RtTerm?>[k]` throws
>     `KeyNotFoundException` on miss — but the test ASSERTS the key
>     IS present (`isNotNull`), so the lookup is expected to succeed.
>     Per the SUT spec construct
>     `dart.class.execution_result_three_final_fields_named_required_ctor_with_const_default_three_bool_getters`,
>     `Bindings` is `IReadOnlyDictionary<string, RtTerm?>`; indexer
>     usage `_engine.Bindings["X"]` is the canonical form (matches
>     Dart `result.bindings['X']` semantics under the success-path
>     assertion).
> (f) `expect(result.error, contains('not found'))` — Dart matcher
>     `contains(<substring>)` for `String` performs substring containment.
>     Maps to xUnit `Assert.Contains("not found", result.Error)`
>     (Microsoft xUnit `Assert.Contains(string, string)` documents
>     substring containment) — NEW idiom for this file.
> (g) `expect(result.status, isNot(ExecutionStatus.failed))` — Dart
>     `isNot(<matcher-or-value>)` is the NEGATION matcher; with a bare
>     enum value on the RHS, it asserts `actual != expected`. Maps to
>     xUnit `Assert.NotEqual(ExecutionStatus.Failed, result.Status)`
>     — NEW idiom for this file (negation of the `isA<T>` /
>     `Assert.Equal` paired idiom).
> (h) `print('X = ${result.bindings['X']}');` — Dart `print` with
>     string interpolation. Maps to C# `Console.WriteLine($"X = {result.Bindings[\"X\"]}");`
>     per the I/O carry-forward from the SUT spec. Diagnostic output
>     ONLY — does NOT affect test outcome; xUnit collects stdout per
>     test (Microsoft Learn 'Capturing Output in xUnit'). Preserved
>     verbatim.

```yaml
schema_version: 1
source_path: test/engine/glp_engine_test.dart
source_sha256: ba6d7b38ff34bd811a6ead5ef440929fb6c02eff1295f1906b68a37b7b4ac2eb
target_code_unit: test/engine/GlpEngineTest.cs
constructs:
  - construct_key: dart.doc_comment.toplevel_triple_slash
    source_form: "/// Tests for GlpEngine - the unified GLP execution core"
    target_decision: >-
      Preserve verbatim as an XML-doc `<summary>` comment on the test
      class `GlpEngineTests`. Dart `///` doc-comments above the first
      non-import declaration attach to the library; in the C# port
      they migrate to the enclosing test class.
    idiom_id: null
    research_finding_id: rf-dart-doc-comment-to-csharp-xml-doc
    nuance: >-
      Doc-comment migration nuance: Dart `///` lines become C# XML-doc
      `/// <summary>...</summary>` blocks. The single-line comment
      here has no `@param` / `@returns` content, so a plain
      `<summary>` suffices. Carry-forward from binding_pointer_test.dart.md
      / module_activation_test.dart.md.
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. xUnit is the project-pinned
      .NET test framework (precedent: every prior `test/**` convspec —
      smoke_test.dart.md, binding_pointer_test.dart.md, every
      test/multiagent/*.dart.md, every test/module/*.dart.md, every
      test/bytecode/*.dart.md, test/runtime/module_activation_test.dart.md,
      test/runtime/rpc_routing_test.dart.md). Reuse verbatim; no
      re-research (FR-024 cache hit; FR-012 SC-007 reuse).
    idiom_id: null
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Project-wide policy nuance — every `package:test` file in the
      inventory MUST map to the SAME .NET framework so test discovery,
      runner config, and attribute vocabulary stay consistent (SC-007).
      Lifecycle nuance: xUnit creates a FRESH instance of the test class
      per `[Fact]` (xunit.net "Shared Context between Tests") — every
      `test()` body in this file constructs its own state via the `setUp`
      callback (mapped to ctor), so per-instance freshness matches the
      Dart `setUp` semantic. ALL FIVE test bodies are `async` so each
      `[Fact]` method is `async Task`.
  - construct_key: dart.import.dart_io
    source_form: "import 'dart:io';"
    target_decision: >-
      Map to `using System.IO;` (provides `FileInfo`). The Dart import
      pulls in `File` which is used here as `File('...').absolute.path`
      — a relative-path-to-absolute-path computation. Per the SUT
      spec construct `dart.method.dart_io_file_absolute_path_property`
      and the external_io.dart.md carry-forward, this maps to
      `new FileInfo("...").FullName` (Microsoft Learn 'FileInfo.FullName
      Property' returns the absolute path).
    idiom_id: null
    research_finding_id: rf-dart-file-absolute-path-to-csharp-fileinfo-fullname
    nuance: >-
      File-vs-FileInfo nuance (LOAD-BEARING): Dart `File(path).absolute.path`
      computes the absolute path WITHOUT requiring the file to exist
      (it is pure CWD + path-join arithmetic). The closest .NET counterpart
      is `new FileInfo(path).FullName` (Microsoft Learn — FullName is
      computed at FileInfo construction by resolving against the current
      directory; the file need not exist). Alternative
      `System.IO.Path.GetFullPath(path)` is also valid (pure static
      function, no FileInfo instance needed) and is the more idiomatic
      C# choice when only the resolved path string is required —
      preferred here because the test only needs the string, not the
      FileInfo object. Codegen MAY use either; recommended:
      `Path.GetFullPath("../programs/self.glp")` for parsimony.
  - construct_key: dart.package_under_test.import_directive_engine_and_scheduler
    source_form: >-
      "import 'package:glp_runtime/engine/glp_engine.dart';
       import 'package:glp_runtime/runtime/scheduler.dart';"
    target_decision: >-
      Two `package:glp_runtime/...` imports collapse to TWO `using`
      directives (one per target namespace, per the C# convention).
      Expected: `using <RootNs>.Engine;` (carries `GlpEngine`,
      `ExecutionResult`, `ModuleInfo` per glp_engine.dart.md's
      `namespace lib.engine` decision) and `using <RootNs>.Runtime;`
      (carries `ExecutionStatus` from scheduler.dart.md). The exact
      namespace strings are owned by the SUT specs:
      `.codeconv/conversion-specs/lib/engine/glp_engine.dart.md` (engine
      sub-namespace) and `.codeconv/conversion-specs/lib/runtime/scheduler.dart.md`
      (runtime sub-namespace).
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (cached idiom — precedent:
      binding_pointer_test.dart.md, module_activation_test.dart.md,
      rpc_routing_test.dart.md): Dart `package:` imports are per-file;
      C# `using` is per-namespace. The two imports here target two
      distinct namespaces so they remain two `using` directives. NO
      `show`/`hide`/`as` clauses appear. The test assembly must
      reference the SUT assembly via the project file (langpair-level
      concern — out of scope for THIS artifact).
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('GlpEngine', () { ... }); }"
    target_decision: >-
      Eliminate `main` entirely; xUnit discovers `[Fact]` methods by
      reflection — there is NO per-file entrypoint to emit. The single
      `group(...)` call inside `main`'s body becomes the enclosing test
      class (see `dart.package_test.group_block` below).
    idiom_id: null
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance: Dart `main` is invoked once per test-file
      process; xUnit has no per-file hook — only per-class (constructor +
      `IDisposable.Dispose`) and per-collection fixtures. THIS file's
      `main` body is a single `group(...)` call with no other statements,
      so the omission is lossless. Carry-forward from binding_pointer_test.dart.md.
  - construct_key: dart.package_test.group_block_single_outer
    source_form: >-
      "group('GlpEngine', () {
         late GlpEngine engine;
         setUp(() { engine = GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path); });
         test('runs simple goal with binding', () async { ... });
         test('clause selection by constant matching', () async { ... });
         test('loads and runs actor-style clauses', () async { ... });
         test('fails on unknown predicate', () async { ... });
         test('runs conjunction', () async { ... });
       });"
    target_decision: >-
      Single outer group with label `'GlpEngine'` maps to a SINGLE
      PascalCase xUnit test class `GlpEngineTests` in namespace
      `<RootNs>.Test.Engine`. The five `test(...)` calls inside become
      five public `async Task` methods on this class, each decorated
      with `[Fact(DisplayName = "<original-label>")]` (the original
      Dart test label is preserved verbatim via `DisplayName`).
      Method names are PascalCased, identifier-safe forms of the label
      so they are valid C# identifiers — `RunsSimpleGoalWithBinding`,
      `ClauseSelectionByConstantMatching`, `LoadsAndRunsActorStyleClauses`,
      `FailsOnUnknownPredicate`, `RunsConjunction`. Because there is
      only ONE group in this file with no nesting, NO `[Trait("Group",
      ...)]` attribute is needed (a single trait partition would be
      noise) — but codegen MAY emit `[Trait("Group", "GlpEngine")]`
      for consistency with the other test convspecs in the engine
      sub-namespace; recommended: omit (no information value).
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Single-group nuance (explicitly addressed, simpler than the
      multi-group cases in binding_pointer_test.dart.md): one outer
      group + 5 tests + ONE `late` field + ONE `setUp` callback. The
      single-group shape MAPS to a single test class with the group
      label as the class name; nested-group flattening with `[Trait]`
      is NOT needed here because there is no nesting. Label-mangling
      nuance: the five test labels contain spaces, hyphens, and
      verb-phrase shapes — PascalCased identifier-safe forms ARE the
      method names; the original labels are preserved via `DisplayName`.
  - construct_key: dart.package_test.setup_callback_with_late_field
    source_form: >-
      "late GlpEngine engine;
       setUp(() { engine = GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path); });"
    target_decision: >-
      The `late GlpEngine engine;` field + `setUp` callback maps to an
      xUnit constructor + a `private readonly GlpEngine _engine;` field.
      Codegen emits: `private readonly GlpEngine _engine;` (field) +
      `public GlpEngineTests() { _engine = new GlpEngine(<rootSelfGlpPath named-arg>= Path.GetFullPath("../programs/self.glp")); }` (constructor).
      xUnit constructs a FRESH instance of the test class per `[Fact]`
      method (xunit.net "Shared Context between Tests"), giving the
      same per-test isolation that Dart's `setUp` provides. The named
      argument `rootSelfGlpPath:` is preserved at the construction
      site (C# named-argument syntax).
    idiom_id: null
    research_finding_id: rf-dart-package-test-setup-to-xunit-constructor
    nuance: >-
      setUp-vs-ctor nuance (cached idiom — precedent: boot_loader_test.dart.md,
      module_activation_test.dart.md): Dart `setUp(() => ...)` is a
      per-test BEFORE-each callback; xUnit per-test class construction
      is functionally equivalent (FRESH instance, FRESH field, no
      cross-test leakage). The `late` field maps to `readonly` because
      it is assigned exactly once (in the ctor) and never reassigned.
      Per-test-isolation nuance (LOAD-BEARING): EACH test gets a
      separate `GlpEngine` instance with a separate `GlpRuntime` /
      `GlpCompiler` pair — important because `loadSource` mutates the
      engine's `_loadedPrograms` / `_loadedModules` maps + activates
      modules; cross-test leakage would corrupt assertions. xUnit's
      per-test-class instantiation provides this isolation natively
      WITHOUT requiring `IDisposable` cleanup. The 'clause selection
      by constant matching' test ALSO constructs a SECOND engine inside
      the test body (`final engine2 = GlpEngine(...)`) — that local
      maps to a method-local `var engine2 = new GlpEngine(...);` (NOT
      a field — locally-scoped on purpose). Path-resolution nuance: the
      relative path `"../programs/self.glp"` is resolved against the
      test-runner's CWD; Dart `package:test` runs from the package
      root (`glp_runtime/`); the C# `dotnet test` runner runs from the
      test project's output dir BY DEFAULT, which differs from the
      Dart layout — codegen SHOULD either (a) emit a config-based
      path resolution, (b) document the CWD requirement in test setup,
      or (c) resolve relative to the test assembly location via
      `AppContext.BaseDirectory`. Recommended: resolve via
      `Path.GetFullPath("../programs/self.glp")` for fidelity AND
      document the CWD assumption (out of scope for this artifact —
      langpair-level test-harness concern). If the relative path
      resolution becomes a problem at codegen time, escalate then;
      for THIS convspec the literal mapping suffices.
  - construct_key: dart.constructor_call.file_path_absolute_path_property
    source_form: "File('../programs/self.glp').absolute.path"
    target_decision: >-
      Map to `Path.GetFullPath("../programs/self.glp")` (preferred,
      Microsoft Learn 'Path.GetFullPath(String) Method' — pure static,
      no `FileInfo` allocation needed) OR `new FileInfo("../programs/
      self.glp").FullName` (Microsoft Learn 'FileInfo.FullName Property').
      Both produce the absolute path string. The preferred form is
      `Path.GetFullPath` because the test only needs the string and
      does not retain a FileInfo reference.
    idiom_id: null
    research_finding_id: rf-dart-file-absolute-path-to-csharp-fileinfo-fullname
    nuance: >-
      Path-resolution nuance (carry-forward from SUT spec idiom):
      Dart `File(p).absolute.path` returns a String — the absolute
      path resolved against the current working directory at the time
      of the call. The .NET counterparts `FileInfo(p).FullName` and
      `Path.GetFullPath(p)` both resolve against `Environment.CurrentDirectory`
      at call time. Observable equivalence: both produce the same
      string for the same CWD. File-existence nuance: NEITHER form
      requires the file to actually exist; both are pure
      path-arithmetic — matching the Dart behaviour. The two
      construction sites in the test (`setUp` and `final engine2 =`)
      both apply the same mapping.
  - construct_key: dart.package_test.test_call_async_with_load_source_then_run_goal
    source_form: >-
      "test('<label>', () async { engine.loadSource('''<multi-line GLP>'''); final result = await engine.runGoal('<goal>'); expect(...); print(...); });"
    target_decision: >-
      Each Dart `test(<label>, () async { ... })` becomes a `public
      async Task <PascalLabel>()` method on `GlpEngineTests`, decorated
      with `[Fact(DisplayName = "<original label>")]`. The method
      body converts statement-for-statement: `loadSource(<triple-quoted-source>)`
      calls become `_engine.LoadSource(<verbatim-or-raw-string>);`;
      `final result = await engine.runGoal('<goal>')` becomes `var
      result = await _engine.RunGoalAsync("<goal>");` (per the SUT's
      `-Async` suffix per Microsoft Framework Design Guidelines).
      `expect` calls map to xUnit `Assert.*` per the per-matcher
      constructs below. `print` calls map to `Console.WriteLine`
      (xUnit captures stdout per test — Microsoft Learn 'Capturing
      Output in xUnit'). All FIVE test methods are `async Task` (the
      Dart bodies are all `async`, and `await engine.runGoal(...)` is
      the central call in each).
    idiom_id: null
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Async-test-method nuance (cached carry-forward): Dart `test(...,
      () async { ... await ... })` maps to xUnit `[Fact] public async
      Task NameAsync() { ... await ... }` per Microsoft Learn
      'xUnit async test'. Per Framework Design Guidelines async test
      methods MAY (not MUST) carry the `-Async` suffix; the xUnit
      community precedent is to OMIT the suffix on test methods
      themselves (test method names are documentation, not call
      sites). Codegen RECOMMENDED: OMIT the `-Async` suffix on test
      methods (matches the xUnit ecosystem norm AND the source's
      `test(<label>)` form, which does not contain the word "Async").
      `RunGoalAsync` (the SUT method) DOES carry the suffix per the
      SUT spec; the call site here is `await _engine.RunGoalAsync(...)`.
      ITextOutputHelper-vs-Console.WriteLine nuance: the test body's
      `print(...)` calls are diagnostic output. xUnit v2 DEPRECATED
      `Console.WriteLine` for test output (Microsoft Learn 'Capturing
      Output in xUnit v2'); the idiomatic xUnit form is to inject
      `ITestOutputHelper` via the constructor and call
      `_output.WriteLine(...)`. Codegen MAY choose either; recommended:
      use `ITestOutputHelper` for proper xUnit-runner output capture.
      For THIS spec, the simpler `Console.WriteLine` mapping is the
      literal counterpart of `print(...)`; if codegen prefers the
      idiomatic form, the constructor adds `private readonly
      ITestOutputHelper _output;` + ctor parameter `(ITestOutputHelper
      output) { _output = output; _engine = new GlpEngine(...); }`.
      Async/Stream/Future: present (Future<ExecutionResult> awaited);
      maps to Task<ExecutionResult>.
  - construct_key: dart.local_var.final_executionresult_from_await
    source_form: >-
      "final result = await engine.runGoal('test(a, X)');
       final result = await engine.runGoal('actor(alice, some_channel)');
       final result = await engine.runGoal('unknown_predicate(x)');
       final result = await engine.runGoal('set(a, X), set(b, Y)');
       var result = await engine.runGoal('pick(alice, X)');
       result = await engine2.runGoal('pick(bob, X)');"
    target_decision: >-
      Dart `final <T> x = await <expr>;` and `var <T> x = await <expr>;`
      with type inferred from the RHS map to C# `var x = await <expr>;`
      for method-local lifetimes. The `final` modifier conveys
      single-assignment intent; C# `var` produces a non-readonly local
      but the single-assignment is preserved by the converted body
      (the 'clause selection' test's `var result = await ...` followed
      by `result = await ...` is the ONE reassignment in this file —
      maps to `var result = await ...; result = await ...;` as-is
      since the Dart source uses `var` for that case).
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Final-vs-var nuance (carry-forward from binding_pointer_test.dart.md):
      Dart `final` is assignment-once at compile time; C# `var` is not.
      Codegen MUST emit `var` for all of these; the reassignment
      pattern in 'clause selection' (Dart `var result = ...; result =
      ...;`) maps to C# `var result = ...; result = ...;` directly.
      No `readonly` (illegal on locals in C#). Engine2-local nuance:
      the 'clause selection' test has `final engine2 = GlpEngine(...);`
      as a method-local — maps to `var engine2 = new GlpEngine(<rootSelfGlpPath named-arg>= Path.GetFullPath("../programs/self.glp"));` (NOT a field — local
      scope, explicit second-engine for cross-engine isolation).
  - construct_key: dart.method_call.engine_load_source_with_triple_quoted_glp
    source_form: >-
      "engine.loadSource('''
       procedure test(_?, _).
       test(a, b).
       test(b, c).
       ''');
       engine.loadSource('''
       procedure pick(_?, _).
       pick(alice, picked_alice).
       pick(bob, picked_bob).
       pick(charlie, picked_charlie).
       ''');
       engine2.loadSource('''<same triple-quoted block>''');
       engine.loadSource('''<actor-style three-clause body>''');
       engine.loadSource('''<set/2 conjunction-source>''');"
    target_decision: >-
      Maps to `_engine.LoadSource("""<verbatim multi-line GLP source>""");`
      using C# 11+ raw-string-literal syntax `"""..."""` OR C#'s
      universal verbatim-string syntax `@"<escaped-multi-line>"`. The
      raw-string-literal form is preferred where target language
      version permits because it preserves the GLP source byte-for-byte
      with NO escaping needed; the verbatim form requires escaping any
      `"` to `""`. The GLP source contains NO `"` characters in any of
      the five strings — both forms are byte-identical to the Dart
      triple-quoted literal. Per the SUT construct
      `dart.toplevel_const_string.raw_string_serve_source_embedded_glp_program`
      (idiom rf-dart-raw-string-triple-quote-to-csharp-verbatim-or-raw-string-literal),
      the choice is determined by the codegen target language version.
      Method name: Dart `loadSource` → C# `LoadSource` (PascalCase per
      Framework Design Guidelines; matches the SUT spec).
    idiom_id: null
    research_finding_id: rf-dart-raw-string-triple-quote-to-csharp-verbatim-or-raw-string-literal
    nuance: >-
      Triple-quoted-string nuance (cached carry-forward from SUT spec):
      Dart `'''...'''` is a multi-line string literal with NO escape
      processing for `$` (because there is no `$` in these strings).
      C# `@"..."` is the universal verbatim form (since C# 2.0); C# 11+
      `"""..."""` is the raw-string-literal form. Both preserve the
      content byte-for-byte. Newline-encoding nuance (LOAD-BEARING):
      Dart triple-quoted strings preserve the LINE-ENDING characters
      of the source file (LF on Unix, CRLF on Windows-saved source).
      The C# verbatim and raw-string forms similarly preserve the
      line-endings of THEIR source file. If the SOURCE file's line
      endings differ between the Dart and C# checkouts, the strings
      will differ at the byte level — the GLP lexer is line-ending-
      tolerant (treats CR+LF and LF identically) per lexer.dart.md so
      this is observably benign, BUT codegen MUST be aware. Whitespace-
      preservation nuance: the GLP source's leading newline (right
      after the opening `'''`) and trailing newline (right before the
      closing `'''`) are part of the string; both verbatim and raw
      string preserve them. Carry-forward from rpc_routing_test.dart.md
      (the seven `const ... = '''...''';` strings).
  - construct_key: dart.method_call.engine_run_goal_async_returning_execution_result
    source_form: >-
      "await engine.runGoal('test(a, X)');
       await engine.runGoal('pick(alice, X)');
       await engine2.runGoal('pick(bob, X)');
       await engine.runGoal('actor(alice, some_channel)');
       await engine.runGoal('unknown_predicate(x)');
       await engine.runGoal('set(a, X), set(b, Y)');"
    target_decision: >-
      Maps to `await _engine.RunGoalAsync("<goal>")` (and
      `await engine2.RunGoalAsync(...)` for the second-engine case).
      Per the SUT construct `dart.method.run_goal_async_entry_point_parse_conjunction_or_single_returning_future_executionresult`
      (idiom rf-dart-future-async-await-to-csharp-task-async-await),
      the SUT method carries the `-Async` suffix. Dart `Future<T> async/
      await` → C# `Task<T> async/await` with `-Async` suffix on the
      SUT method.
    idiom_id: null
    research_finding_id: rf-dart-future-async-await-to-csharp-task-async-await
    nuance: >-
      Async-call-site nuance (cached carry-forward from SUT spec):
      Dart `await engine.runGoal(...)` directly maps to C# `await
      _engine.RunGoalAsync(...)`. Goal-string nuance: the goal text
      passed to `RunGoalAsync` contains GLP surface syntax (`(`, `)`,
      `,`) — NO C# escaping needed (no `"` characters in any of the
      six goal strings); single-quoted Dart literals map to
      double-quoted C# literals byte-identically. Exception-wrapping
      nuance (LOAD-BEARING from SUT): `RunGoalAsync` internally
      `try-catch`es and wraps any throw in a failed `ExecutionResult`
      with `e.ToString()` as the error message (per SUT construct
      `dart.method.run_goal_async_entry_point_...`). The test
      'fails on unknown predicate' EXERCISES this: the engine throws
      'Predicate unknown_predicate/1 not found' internally, which the
      SUT wraps into `ExecutionResult(status: failed, error: "Predicate
      unknown_predicate/1 not found")`. The assertion
      `expect(result.error, contains('not found'))` matches via
      substring containment. C# preserves the EXACT same wrapping
      semantics per the SUT spec; the test passes byte-identically.
  - construct_key: dart.member_access.executionresult_succeeded_failed_status_error
    source_form: "result.succeeded, result.failed, result.status, result.error"
    target_decision: >-
      Map to PascalCase property accesses per the SUT spec construct
      `dart.class.execution_result_three_final_fields_named_required_ctor_with_const_default_three_bool_getters`:
      `result.Succeeded` (computed bool projection of Status),
      `result.Failed` (computed bool projection),
      `result.Status` (the `ExecutionStatus` enum-typed field),
      `result.Error` (the nullable `string?` error message field).
    idiom_id: null
    research_finding_id: rf-dart-immutable-result-bundle-with-final-fields-to-csharp-sealed-class-with-readonly-properties
    nuance: >-
      Property-naming nuance (LOAD-BEARING — cached from SUT spec):
      Dart camelCase getters (`succeeded`, `failed`, `status`, `error`)
      become C# PascalCase get-only properties on the `ExecutionResult`
      sealed class. Reference-vs-value nuance: `ExecutionResult` is a
      reference class per the SUT spec; property accesses on `result`
      yield the (immutable) field values. `Status` is the `ExecutionStatus`
      enum (a value type); `Bindings` is `IReadOnlyDictionary<string,
      RtTerm?>` (a reference); `Error` is `string?` (a nullable
      reference). All four property accesses in this file are read-only
      lookups — no mutation.
  - construct_key: dart.member_access.executionresult_bindings_indexer_string_key
    source_form: >-
      "result.bindings['X']
       result.bindings['Y']"
    target_decision: >-
      Map to `result.Bindings["X"]` / `result.Bindings["Y"]` — C#
      indexer on `IReadOnlyDictionary<string, RtTerm?>` per the SUT
      spec. Returns `RtTerm?` (nullable Term reference per the per-symbol
      alias for runtime `Term`; per the SUT spec construct
      `dart.import.dart_io_plus_seventeen_package_glp_runtime_plus_one_aliased`).
    idiom_id: null
    research_finding_id: rf-dart-map-index-to-csharp-dictionary-indexer
    nuance: >-
      Map-index-on-success-path nuance (LOAD-BEARING): Dart `Map[k]`
      returns null on miss; C# `IReadOnlyDictionary<TKey, TValue>[k]`
      throws `KeyNotFoundException` on miss. THIS FILE always uses the
      indexer in contexts where the key IS expected to be present
      (the goals query variable `X` / `Y` and the SUT guarantees the
      binding is populated under the success path). The assertion
      `expect(result.bindings['X'], isNotNull)` ASSERTS the value
      retrieved is non-null — which under the success path is always
      true (the SUT's `_RunSingleGoalAsync` populates the bindings map
      with `null` for unbound vars and a `RtTerm` for bound vars per
      SUT construct `dart.method.private_run_single_goal_async_parse_lookup_setup_args_drain_collect_bindings`).
      The `print('X = ${result.bindings['X']}')` calls similarly
      assume the key is present. Codegen MUST emit `result.Bindings["X"]`
      verbatim — NOT `TryGetValue` (the test expects throw-on-missing
      semantics IF the SUT ever fails to populate the key, surfacing
      a regression). The C# indexer is the faithful counterpart.
      Hot-path nuance: if a future regression causes `X` to be absent
      from `Bindings` (e.g. SUT bug), C# `KeyNotFoundException` is
      thrown — observably DIFFERENT from Dart's `expect(<null>, isNotNull)`
      failure mode (which fails with "expected non-null, got null").
      Both fail the test; C# diagnostic is slightly more obvious
      ("KeyNotFoundException: X"). This is OBSERVABLY EQUIVALENT for
      pass/fail outcomes; acceptable divergence.
  - construct_key: dart.string_interpolation.in_print_call
    source_form: >-
      "print('Status: ${result.status}, error: ${result.error}');
       print('X = ${result.bindings['X']}');
       print('pick(alice, X) -> X = ${result.bindings['X']}');
       print('pick(bob, X) -> X = ${result.bindings['X']}');
       print('actor(alice, some_channel) succeeded');
       print('X = ${result.bindings['X']}, Y = ${result.bindings['Y']}');"
    target_decision: >-
      Dart `'<lit>${expr}<lit>'` string interpolation maps to C#
      `$"<lit>{expr}<lit>"` interpolated string (Microsoft Learn
      'Interpolated strings (Reference)'). Each `print(...)` call
      maps to `Console.WriteLine($"...");` OR `_output.WriteLine($"...");`
      if the test class injects an `ITestOutputHelper`. The dollar-prefixed
      C# interpolation evaluates the `{expr}` at runtime, identical to
      Dart's `${expr}` evaluation.
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      Interpolation-syntax nuance (cached carry-forward from prior
      convspecs): Dart `${expr}` → C# `{expr}` inside an interpolated
      string prefixed with `$`. Single-identifier interpolation also
      supported in Dart as `$identifier` (without braces) — would map
      to C# `{identifier}` inside the `$"..."`. For THIS file, all
      interpolations use `${...}` (the brace form). xUnit-output-helper
      nuance (RECOMMENDED ALTERNATIVE): xUnit v2+ deprecates
      `Console.WriteLine` for test output (Microsoft Learn 'Capturing
      Output' xUnit doc — https://xunit.net/docs/capturing-output);
      the idiomatic form is to inject `ITestOutputHelper` via the
      ctor. Codegen MAY emit the `ITestOutputHelper` form for
      consistency with xUnit best-practice; the literal-mapping is
      `Console.WriteLine($"...");`. Recommended choice: inject
      `ITestOutputHelper` and call `_output.WriteLine($"...")`;
      acceptable fallback: `Console.WriteLine($"...")`. Both observe
      pass/fail outcomes identically; the difference is only in WHERE
      the diagnostic output is rendered.
  - construct_key: dart.package_test.expect_isTrue
    source_form: >-
      "expect(result.succeeded, isTrue, reason: 'Error: ${result.error}');
       expect(result.succeeded, isTrue);
       expect(result.succeeded, isTrue);
       expect(result.succeeded, isTrue);
       expect(result.failed, isTrue);"
    target_decision: >-
      Map to xUnit `Assert.True(<bool-expr>)` for plain `isTrue` cases.
      For the FIRST case with the `reason:` named argument
      ('Error: ${result.error}'), map to `Assert.True(<bool-expr>,
      $"Error: {result.Error}")` — xUnit `Assert.True` overload
      `(bool, string)` accepts a failure-message argument (Microsoft
      Learn 'xunit.Assert.True'). Per the boot_loader_test.dart.md
      precedent (idiom rf-dart-expect-istrue-to-xunit-asserttrue);
      reason-argument-with-interpolation NEW for this file.
    idiom_id: null
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      Reason-argument nuance (LOAD-BEARING — NEW): Dart `expect(actual,
      isTrue, reason: '<msg>')` attaches an explanation that appears
      in the failure output if the assertion fails. xUnit's
      `Assert.True(bool, string)` overload accepts a failure message
      that serves the same role. The interpolated form
      `$"Error: {result.Error}"` is evaluated EAGERLY in C# at call
      time — meaning the message string is materialised on every call
      including the success path; for high-volume tests this would
      have minor overhead. For THIS file (5 tests total), the overhead
      is negligible. Codegen MUST preserve the reason text verbatim
      (substituting C# string interpolation for Dart). Alternative
      `Assert.True(result.Succeeded, "Error: " + (result.Error ?? ""))`
      avoids interpolation but loses fidelity to the source. Recommended:
      `Assert.True(_result.Succeeded, $"Error: {_result.Error}")`.
      Diagnostic-only nuance: the four other `isTrue` cases lack the
      reason argument; map to plain `Assert.True(<bool-expr>)`.
  - construct_key: dart.package_test.expect_isNotNull
    source_form: "expect(result.bindings['X'], isNotNull);"
    target_decision: >-
      Map to xUnit `Assert.NotNull(<actual>)` (Microsoft Learn
      'xunit.Assert.NotNull'). Used once in this file ('runs simple goal
      with binding' test): `Assert.NotNull(result.Bindings["X"]);`.
    idiom_id: null
    research_finding_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    nuance: >-
      Nullable-target nuance: Dart `isNotNull` matcher works on `Object?`;
      C# `Assert.NotNull` accepts any nullable reference or nullable
      value type. The `result.Bindings["X"]` indexer returns `RtTerm?`
      (nullable per the SUT spec); the assertion is well-typed. The
      mirror matcher `isNull` (binding_pointer_test.dart.md idiom
      rf-dart-expect-isNull-to-xunit-assert-null) is the negation;
      both are recorded in the rf cache.
  - construct_key: dart.package_test.expect_contains_substring
    source_form: "expect(result.error, contains('not found'));"
    target_decision: >-
      Dart `contains(<substring>)` matcher with a String argument and
      a String actual asserts substring containment (Dart `package:test`
      matcher reference). Maps to xUnit `Assert.Contains(<substring>,
      <actual>)` (Microsoft Learn 'xunit.Assert.Contains(String, String)'
      — substring containment overload). Codegen emits
      `Assert.Contains("not found", result.Error)`.
    idiom_id: null
    research_finding_id: rf-dart-expect-contains-substring-to-xunit-assert-contains
    nuance: >-
      Argument-order nuance (LOAD-BEARING — well-known footgun): Dart
      `expect(actual, contains('substring'))` puts actual first; xUnit
      `Assert.Contains(substring, actual)` puts the substring (the
      expected sub-content) first. Same order-flip footgun as
      `Assert.Equal` per boot_loader_test.dart.md / binding_pointer_test.dart.md
      precedent. Codegen MUST emit `Assert.Contains("not found",
      result.Error)` (substring first, actual second). Null-actual
      nuance: `result.Error` is `string?` (nullable per SUT). xUnit
      `Assert.Contains(string, string)` — if `actual` is null, the
      assertion fails with `ContainsException`. The Dart counterpart
      `expect(null, contains('not found'))` ALSO fails. Observable
      equivalence preserved. The test exercises the success path where
      `Error` IS non-null with the substring 'not found' (the SUT
      wraps 'Predicate unknown_predicate/1 not found' into the error
      field).
  - construct_key: dart.package_test.expect_isNot_with_enum_value
    source_form: "expect(result.status, isNot(ExecutionStatus.failed));"
    target_decision: >-
      Dart `isNot(<matcher-or-value>)` is the NEGATION matcher; with
      a bare enum value on the RHS, it asserts `actual != expected`
      (Dart `package:test` matcher reference). Maps to xUnit
      `Assert.NotEqual(<expected>, <actual>)` (Microsoft Learn
      'xunit.Assert.NotEqual'). Codegen emits
      `Assert.NotEqual(ExecutionStatus.Failed, result.Status)`. Note
      that `ExecutionStatus.failed` (Dart enum member, lowerCamelCase
      per Dart convention) maps to `ExecutionStatus.Failed` (PascalCase
      per scheduler.dart.md's PascalCase enum decision).
    idiom_id: null
    research_finding_id: rf-dart-expect-isnot-value-to-xunit-assert-notequal
    nuance: >-
      Negation-matcher nuance (NEW idiom for this file): Dart `isNot(X)`
      where X is a value (not a matcher) is shorthand for `isNot(equals(X))`.
      The xUnit counterpart is `Assert.NotEqual(expected, actual)` (Microsoft
      Learn). Argument-order: `Assert.NotEqual(expected, actual)` — the
      same order as `Assert.Equal`. Enum-naming nuance (LOAD-BEARING):
      Dart enum members are conventionally lowerCamelCase (`failed`,
      `succeeded`, `suspended`) per Dart style; the C# port per
      scheduler.dart.md uses PascalCase (`Failed`, `Succeeded`,
      `Suspended`). Codegen MUST emit `ExecutionStatus.Failed`. Test
      semantic: the 'runs conjunction' test asserts the engine did
      NOT enter the failed state for `set(a, X), set(b, Y)` — it
      MAY have succeeded OR suspended (the SUT may suspend if the
      conjunction's drain doesn't fully bind both vars). The assertion
      tolerates both `Succeeded` and `Suspended` outcomes by negating
      only on `Failed`. C# `Assert.NotEqual` preserves the tolerance
      identically.
  - construct_key: dart.constructor_call.engine_named_required_root_self_glp_path
    source_form: >-
      "GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path);
       GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path);   // engine2"
    target_decision: >-
      Map to `new GlpEngine(rootSelfGlpPath: Path.GetFullPath("../programs/
      self.glp"))`. Per the SUT spec construct
      `dart.constructor.glp_engine_named_required_with_six_step_init_body_file_io_and_compile`,
      the SUT ctor is `public GlpEngine(string rootSelfGlpPath)` —
      Dart named-required → C# positional non-optional; the C# call
      site MAY use named-argument syntax `rootSelfGlpPath:` for
      readability AND fidelity to the Dart source. Recommended:
      preserve the named-argument syntax for self-documentation.
    idiom_id: null
    research_finding_id: rf-dart-named-required-to-csharp-positional-with-named-call-site
    nuance: >-
      Named-required → named-call-site nuance (cached idiom — precedent:
      module_activation_test.dart.md, mad_helpers.dart.md): Dart
      named-required parameters map to C# positional non-optional
      parameters, but the CALL SITE may still use named-argument
      syntax (Microsoft Learn 'Named and Optional Arguments' —
      named-argument syntax is supported on any positional parameter).
      Two instantiations in this file: the `setUp` callback's
      `engine = GlpEngine(<rootSelfGlpPath named-arg>= ...)` and the 'clause
      selection' test's `final engine2 = GlpEngine(<rootSelfGlpPath named-arg>= ...)`. Both map identically. The `File(...).absolute.path`
      expression at the argument position maps to
      `Path.GetFullPath("../programs/self.glp")` per the
      construct_key `dart.constructor_call.file_path_absolute_path_property`
      above.

conversion_units:
  - "cu-1: file-scope using directives (using Xunit; using System; using System.IO; using System.Threading.Tasks; the SUT engine namespace <RootNs>.Engine for GlpEngine / ExecutionResult; the SUT runtime namespace <RootNs>.Runtime for ExecutionStatus); RECOMMENDED also using Xunit.Abstractions; if ITestOutputHelper is adopted for print mapping"
  - "cu-2: namespace declaration mirroring the test/engine path (<RootNs>.Test.Engine)"
  - "cu-3: top-level XML-doc summary on the test class preserving the Dart /// doc-comment ('Tests for GlpEngine - the unified GLP execution core')"
  - "cu-4: top-level test class GlpEngineTests (single class — single outer Dart group means single C# class, no inner classes / no Trait partitions needed)"
  - "cu-5: private readonly GlpEngine _engine; field assigned in the ctor (the Dart late GlpEngine engine; + setUp callback)"
  - "cu-6: public ctor GlpEngineTests() (per-test fresh instance per xUnit's per-method instantiation; mirrors Dart's setUp lifecycle); ctor body assigns _engine = new GlpEngine with rootSelfGlpPath named-arg = Path.GetFullPath('../programs/self.glp')"
  - "cu-7: optional ctor parameter ITestOutputHelper output if the idiomatic xUnit-output route is chosen (then private readonly ITestOutputHelper _output; and _output.WriteLine(...) for every print(...) map)"
  - "cu-8: [Fact] public async Task RunsSimpleGoalWithBinding() method (DisplayName 'runs simple goal with binding'); body calls _engine.LoadSource(<raw-string GLP>); var result = await _engine.RunGoalAsync('test(a, X)'); Console.WriteLine($'Status...'); Assert.True(result.Succeeded, $'Error: {result.Error}'); Assert.NotNull(result.Bindings['X']); Console.WriteLine($'X = ...')"
  - "cu-9: [Fact] public async Task ClauseSelectionByConstantMatching() (DisplayName 'clause selection by constant matching'); body includes LOAD + first RunGoalAsync('pick(alice, X)') + assert + print, THEN a method-local var engine2 = new GlpEngine with rootSelfGlpPath named-arg = Path.GetFullPath('../programs/self.glp') + its LoadSource + second RunGoalAsync('pick(bob, X)') + assert + print; the result local is reassigned (matches Dart var result = ...; result = ...;)"
  - "cu-10: [Fact] public async Task LoadsAndRunsActorStyleClauses() (DisplayName 'loads and runs actor-style clauses'); single LoadSource + single RunGoalAsync('actor(alice, some_channel)') + Assert.True(result.Succeeded) + Console.WriteLine"
  - "cu-11: [Fact] public async Task FailsOnUnknownPredicate() (DisplayName 'fails on unknown predicate'); NO LoadSource (the engine has only the root-self loaded from the ctor); single RunGoalAsync('unknown_predicate(x)') + Assert.True(result.Failed) + Assert.Contains('not found', result.Error)"
  - "cu-12: [Fact] public async Task RunsConjunction() (DisplayName 'runs conjunction'); LoadSource (set/2 source) + RunGoalAsync('set(a, X), set(b, Y)') + Assert.NotEqual(ExecutionStatus.Failed, result.Status) + Console.WriteLine"
  - "cu-13: NO IDisposable / Dispose method needed (the engine owns its runtime + compiler + scheduler, and xUnit's per-test-method class instantiation provides cleanup-on-GC; the engine does not hold OS-level resources requiring deterministic disposal per SUT glp_engine.dart.md — no IDisposable recommendation there)"

escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative, cache hit)

Sixth+ `package:test` file specced; xUnit is project-pinned. The
authoritative basis is unchanged: xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / `[Trait]` / `DisplayName`; Dart `package:test` docs
(`https://pub.dev/packages/test`) for `group` / `test` / `expect` /
matcher semantics. Reused via `rf-dart-package-test-import-to-xunit-using`
— no re-research (FR-024 cache hit; SC-007 reuse). Lifecycle
mapping reused via `rf-dart-package-test-setup-to-xunit-constructor`
from boot_loader_test.dart.md.

### Single outer group, late field + setUp, async tests

Unlike binding_pointer_test (6 sibling groups, NO `late` field) and
boot_loader_test (1 outer + 3 inner + `late BootLoader loader`), this
file has 1 outer group + 1 `late` field + 5 SYNCHRONOUS-INSIDE-`async`-CLOSURE
tests. Mapping is the simplest yet: ONE test class, ONE constructor,
ONE `readonly` field, FIVE `[Fact] async Task` methods. No `[Trait]`
needed (single group). Per the boot_loader_test precedent the `late`
+ `setUp` → `readonly` + ctor mapping is reused verbatim. Recorded
under `rf-dart-package-test-setup-to-xunit-constructor`.

### `runGoal` → `RunGoalAsync` (`-Async` suffix on SUT method)

Per the SUT spec
`.codeconv/conversion-specs/lib/engine/glp_engine.dart.md` (cu-414
"public async Task<ExecutionResult> RunGoalAsync(string goalText)"),
the SUT carries the `-Async` suffix per Microsoft Framework Design
Guidelines. Test method names themselves do NOT carry the suffix
(xUnit community precedent; test method names are documentation).
Reused via `rf-dart-future-async-await-to-csharp-task-async-await`
from scheduler.dart.md.

### `ExecutionResult` shape REUSED from SUT spec

Per the SUT spec construct
`dart.class.execution_result_three_final_fields_named_required_ctor_with_const_default_three_bool_getters`,
`ExecutionResult` is a `sealed class` with three get-only properties
(`Status` typed `ExecutionStatus`, `Bindings` typed
`IReadOnlyDictionary<string, RtTerm?>`, `Error` typed `string?`) and
three computed bool projection properties (`Succeeded` / `Failed` /
`Suspended`). The test's `result.succeeded` / `result.failed` /
`result.status` / `result.error` / `result.bindings['X']` accesses
map directly to these PascalCase properties. NOT a `record` (identity
equality preserved per the SUT decision). Reused via
`rf-dart-immutable-result-bundle-with-final-fields-to-csharp-sealed-class-with-readonly-properties`.

### Triple-quoted GLP source → C# raw or verbatim string

Five `engine.loadSource('''...''')` calls in this file. Per the SUT
spec construct
`dart.toplevel_const_string.raw_string_serve_source_embedded_glp_program`
(idiom rf-dart-raw-string-triple-quote-to-csharp-verbatim-or-raw-string-literal),
the codegen target version determines the form: C# 11+ raw-string-literal
`"""..."""` (byte-identical, no escaping) OR C#'s universal verbatim
`@"..."` (since C# 2.0, requires `""` for embedded `"`). The five GLP
sources here contain NO `"` characters — both forms are byte-identical
to the Dart triple-quoted literal.

### `File(...).absolute.path` → `Path.GetFullPath(...)`

Per the SUT spec construct `dart.method.dart_io_file_absolute_path_property`
(idiom rf-dart-file-absolute-path-to-csharp-fileinfo-fullname),
Dart `File(p).absolute.path` maps to `Path.GetFullPath(p)` (or
equivalent `new FileInfo(p).FullName`). Both Microsoft Learn'ed:
`Path.GetFullPath(String)` (https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath)
returns the absolute path WITHOUT requiring the file to exist;
`FileInfo.FullName`
(https://learn.microsoft.com/dotnet/api/system.io.fileinfo.fullname)
does the same. Reused via the SUT's research finding.

### `expect(actual, contains('substring'))` → `Assert.Contains(substring, actual)` — NEW

xUnit docs (https://xunit.net/docs/comparisons#assertions) and
Microsoft Learn 'xunit.Assert.Contains' document the substring overload
`Assert.Contains(string expectedSubstring, string actualString)` —
note the argument order is (expected substring, actual string), same
order-flip pattern as `Assert.Equal`. NEW idiom for this file because
prior `package:test` convspecs in the inventory did NOT exercise the
`contains('substring')` matcher on a String actual. Authoritative;
recorded under `rf-dart-expect-contains-substring-to-xunit-assert-contains`.

### `expect(actual, isNot(<enum-value>))` → `Assert.NotEqual(expected, actual)` — NEW

Dart `isNot(<value>)` is the negation of `equals(<value>)`. xUnit's
counterpart `Assert.NotEqual(expected, actual)` (Microsoft Learn
'xunit.Assert.NotEqual') uses the same argument order as `Assert.Equal`.
NEW idiom for this file because prior `package:test` convspecs in the
inventory did NOT exercise the `isNot` matcher. Authoritative;
recorded under `rf-dart-expect-isnot-value-to-xunit-assert-notequal`.

### `expect(actual, isTrue, reason: '...')` → `Assert.True(actual, "...")`

xUnit's `Assert.True(bool, string)` overload (Microsoft Learn
'xunit.Assert.True(bool, string)') accepts a failure-message argument
that serves the same role as Dart's `reason:` named argument. The
test's first test interpolates `${result.error}` into the reason —
maps to C# interpolated string `$"Error: {result.Error}"`. Reused
via `rf-dart-expect-istrue-to-xunit-asserttrue` from boot_loader_test.

### `print` → `Console.WriteLine` or `ITestOutputHelper.WriteLine`

Six `print(...)` calls (diagnostic). Dart `print` → C# `Console.WriteLine`
is the literal mapping (cached idiom from the SUT spec). xUnit
RECOMMENDS `ITestOutputHelper` (https://xunit.net/docs/capturing-output)
because `Console.WriteLine` output is not captured per-test in xUnit v2+.
Codegen MAY choose either; recommended: inject `ITestOutputHelper`
via the ctor and call `_output.WriteLine(...)` for proper per-test
output capture. Both options preserve pass/fail outcomes identically.

### Threading-model decision INHERITED

From heap_fcp.dart.md escalations[0] (single-owning-context per goal /
non-concurrent collections), inherited through scheduler.dart.md /
runtime.dart.md / glp_activation.dart.md / runner.dart.md / and the SUT
glp_engine.dart.md. This convspec does NOT re-escalate (FR-013). The
five tests each run on a SINGLE thread with NO `Task.WhenAll` /
parallelism inside the bodies; xUnit per-test class instantiation
ensures fresh engine state per test; the single-owning-context
invariant is naturally satisfied. Codegen MUST consult the resolved
heap_fcp ruling and apply it uniformly down to GlpEngine; this file
will compile correctly under any of the candidate resolutions
(plain Dictionary, ConcurrentDictionary, or other) because the test
NEVER touches the engine's internal collections directly.

### Per-symbol `RtTerm` alias INHERITED from SUT spec

The SUT spec construct
`dart.import.dart_io_plus_seventeen_package_glp_runtime_plus_one_aliased`
pins the four per-symbol aliases `RtTerm` / `RtVarRef` / `RtConstTerm`
/ `RtStructTerm` for resolving the Dart `import 'package:glp_runtime/runtime/terms.dart' as rt;`
collision with AST types. THIS test file does NOT import the runtime
`terms.dart` directly — but `result.bindings` is typed
`IReadOnlyDictionary<string, RtTerm?>` (the SUT's `Bindings` property
type). The test code's `result.bindings['X']` lookup yields `RtTerm?`
which is then either asserted non-null (`Assert.NotNull(...)`) or
interpolated into a print string. The `RtTerm` alias resolution is
INHERITED from the SUT's file-header `using` declarations — the test
file does NOT need its own `RtTerm` alias because it never names the
type explicitly (every reference is structural through the
`Bindings` indexer). If a future test ASSIGNS the indexer result to a
named local (e.g. `RtTerm? value = result.Bindings["X"];`), the test
file would need its own `using RtTerm = <RootNs>.Runtime.Term;`
declaration; THIS file does not. No new idiom needed.

### Why no escalations

Every construct has a clear, single-decision target shape grounded in
official Dart / .NET documentation AND most non-trivial constructs
reuse an idiom recorded in a precedent spec (the SUT
glp_engine.dart.md + the test exemplar set
binding_pointer_test.dart.md / boot_loader_test.dart.md /
module_activation_test.dart.md / rpc_routing_test.dart.md). The three
genuinely-NEW idioms recorded in this file are:
(a) `rf-dart-expect-contains-substring-to-xunit-assert-contains`
    (substring containment on a String actual);
(b) `rf-dart-expect-isnot-value-to-xunit-assert-notequal`
    (negation of equality with a bare value);
(c) `rf-dart-expect-istrue-to-xunit-asserttrue` extended with the
    reason-with-interpolation pattern (the reason-argument variant
    was implicit in the boot_loader_test idiom; this file makes it
    explicit).
The threading-model question is INHERITED from heap_fcp.dart.md per
FR-013 (no double-escalation). The `Console.WriteLine` vs
`ITestOutputHelper.WriteLine` choice for `print` mapping is a
codegen-time TASTE decision with both forms authoritative — the
recommended choice is documented but no escalation is needed because
both forms pass the test identically. `escalations: []` is therefore
intentional.
