> Conversion-spec artifact for test/compiler/project_linker_test.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.
>
> File is a `package:test`-based unit-test suite (319 lines, 13
> `test()` cases across FOUR sibling outer groups inside one
> `void main()`: `Project discovery` (4 tests), `Type checking`
> (1 test), `Linking` (8 tests), `End-to-end compilation` (2 tests)).
> It exercises the static project linker (`discoverProject`,
> `typeCheckProject`, `linkProject`) over the on-disk
> `../programs/cssg_modules` directory, then drives the linked
> bytecode end-to-end through `GlpCompiler` + `GlpRuntime` +
> `Scheduler` + `BytecodeRunner` (the final `fplay1 produces correct
> output` test). `main()` carries a TRIPLE pre-group block: load
> prelude source from `../programs/self.glp` via `dart:io` `File`
> + apply to BOTH `setPreludeUnitClauseSource` and
> `setPreludeEnvironmentSource`; gate everything on
> `Directory(cssgRoot).existsSync()` with an early `return` (NOT
> `throw StateError` — softer than `cssg_modules_test.dart`); the
> `Linking` group uses `setUp(...)` for per-test fixture rebuild
> over `late List<DiscoveredModule>`, `late LinkResult`, `late
> Program` fields. Every non-trivial construct REUSES an idiom
> recorded by the prior test- and lib-spec batches (notably
> `test/compiler/partial_evaluator_test.dart.md`,
> `test/module/cssg_modules_test.dart.md`,
> `test/glp_runtime_test.dart.md`, `lib/compiler/project_linker.dart.md`,
> `lib/compiler/compiler.dart.md`, `lib/runtime/runtime.dart.md`,
> `lib/runtime/scheduler.dart.md`, `lib/runtime/machine_state.dart.md`,
> `lib/bytecode/runner.dart.md`).

```yaml
schema_version: 1
source_path: test/compiler/project_linker_test.dart
source_sha256: f9c5c7d728fb53ad1b5d0bda9c918d9af1d7360ac7f3840334c05ee2906d12da
target_code_unit: test/compiler/ProjectLinkerTest.cs
constructs:
  - construct_key: dart.docblock_triple_slash_file_header_with_library_directive
    source_form: >-
      "/// Project linker tests: static linking of multi-module GLP projects.
       ///
       /// Tests discovery, type checking, renaming, call resolution, and
       /// end-to-end compilation of the cssg_modules project.
       library;"
    target_decision: >-
      Map the Dart `///` triple-slash file-header doc-comment block to a
      C# `///` XML-doc summary block placed immediately above the test
      class declaration (`<summary>...</summary>` — Microsoft Learn
      "Documentation comments" at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/recommended-tags`).
      The trailing Dart `library;` directive (unnamed Dart-2.19+ library
      marker, only required because the file contains a doc comment
      attached to the library — see Dart language tour
      `https://dart.dev/language/libraries#library-directive`) DROPS on
      the C# side: C# has no library directive — namespace + assembly
      together provide the library-scope concept. REUSE the lib-spec
      idiom `rf-dart-library-directive-to-csharp-namespace-elision`
      (precedent: `lib/compiler/project_linker.dart.md`).
    idiom_id: rf-dart-library-directive-to-csharp-namespace-elision
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Library-directive nuance (carry-forward, EXPLICITLY addressed):
      `library;` is a Dart-only construct — Dart files are libraries by
      default; the unnamed `library;` directive exists ONLY to anchor
      library-level doc-comments. C#'s analogue is the `namespace`
      declaration; the doc-comment moves to the test class and is
      reformatted as a `<summary>` XML-doc block. Triple-slash-to-XML
      nuance: Dart's `///` doc lines map 1-to-1 to C#'s `///`
      doc lines, but C# requires the well-formed XML tags around the
      content for IntelliSense/`dotnet build`-emitted XML output.

  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and
      replace it at the file head with `using Xunit;`. REUSE the
      batch-wide xUnit pinned by `test/smoke_test.dart.md` and every
      sibling test spec (KB hit ⇒ REUSE verbatim per FR-012/SC-007,
      no re-research, no re-derivation). Codegen MUST also add
      `using System.IO;` (used by the `File(...)`/`Directory(...)`
      translations under
      `dart.platform.file_existsSync_readAsStringSync` and
      `dart.platform.directory_existsSync_skip_with_print`) and
      `using System.Collections.Generic;` +
      `using System.Linq;` (used by the LINQ `Select`/`Where`/`ToHashSet`/
      `ToList`/`FirstOrDefault` translations of `map(...)`/`where(...)`/
      `toSet()`/`toList()`/`firstWhere(...)` in the Linking-group
      tests). Project to a namespace mirroring the Dart
      `test/compiler` directory (e.g. `<RootNs>.Test.Compiler`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Cached idiom — reused verbatim. Lifecycle nuance (carry-forward):
      xUnit creates a FRESH instance of the test class per `[Fact]`
      (xunit.net "Shared Context between Tests" —
      `https://xunit.net/docs/shared-context`). For THIS file the
      lifecycle is non-trivial: (a) the pre-group block in Dart `main()`
      is process-scoped — lifts to a `static` constructor; (b) the
      `Linking` group has a `setUp(...)` that REBUILDS modules +
      linkResult + linked program before each test — naturally
      satisfied by xUnit's per-Fact fresh-instance semantics with the
      build executed in the (instance) constructor of a dedicated
      nested Linking test class.

  - construct_key: dart.dart_io.import_directive
    source_form: "import 'dart:io';"
    target_decision: >-
      Drop the Dart `import 'dart:io';` directive and replace it at
      the file level with `using System.IO;`. REUSE the
      `rf-dart-import-dartio-to-csharp-using-systemio` idiom recorded
      in `lib/runtime/runtime.dart.md` and reused in
      `test/compiler/partial_evaluator_test.dart.md` and
      `test/module/cssg_modules_test.dart.md`. The `dart:io` surface
      used in THIS file is `File` (constructor + `.existsSync()` +
      `.readAsStringSync()` + `.absolute` property + `.path`
      property) and `Directory` (constructor + `.existsSync()`) — both
      covered by `System.IO.File`/`System.IO.FileInfo` and
      `System.IO.Directory`. No `Platform`, `Process`, `Socket`,
      `Stdin`, or `Stdout` references.
    idiom_id: rf-dart-import-dartio-to-csharp-using-systemio
    research_finding_id: rf-dart-import-dartio-to-csharp-using-systemio
    nuance: >-
      Cached idiom — reused verbatim (precedents: runtime.dart.md,
      partial_evaluator_test.dart.md, cssg_modules_test.dart.md).
      Library-vs-namespace nuance (carry-forward, load-bearing):
      `dart:io` is one Dart-core library; .NET splits the same surface
      across several `System.*` namespaces (`System.IO` for file/
      directory/stream APIs, `System.Diagnostics` for `Process`,
      `System.Net.Sockets` for sockets). For THIS file only
      `System.IO` is required because only `File` and `Directory` are
      used.

  - construct_key: dart.internal_package_import.glp_runtime_compiler_analysis_runtime_bytecode_set
    source_form: >-
      "import 'package:glp_runtime/compiler/project_linker.dart';
       import 'package:glp_runtime/compiler/compiler.dart';
       import 'package:glp_runtime/compiler/partial_evaluator.dart' show setPreludeUnitClauseSource;
       import 'package:glp_runtime/analysis/type_checker/type_environment_builder.dart' show setPreludeEnvironmentSource;
       import 'package:glp_runtime/compiler/ast.dart';
       import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';
       import 'package:glp_runtime/runtime/scheduler.dart';
       import 'package:glp_runtime/bytecode/runner.dart';"
    target_decision: >-
      Replace the nine Dart `package:glp_runtime/*` imports with C#
      `using` directives that name the namespaces produced by the
      langpair file-to-namespace fold (Dart libraries are file-grained;
      C# namespaces are directory-grained, see
      `cssg_modules_test.dart.md` precedent). Per the lib specs the
      five Dart imports under `lib/compiler/*` (`project_linker`,
      `compiler`, `partial_evaluator`, `ast`) collapse into ONE
      `using <RootNs>.Compiler;`. The single
      `lib/analysis/type_checker/type_environment_builder.dart` import
      collapses into `using <RootNs>.Analysis.TypeChecker;` (per
      `lib/analysis/type_checker/type_environment_builder.dart.md`).
      The three `lib/runtime/*` imports (`runtime`, `machine_state`,
      `scheduler`) collapse into ONE `using <RootNs>.Runtime;` (per
      `lib/runtime/runtime.dart.md`, `lib/runtime/machine_state.dart.md`,
      `lib/runtime/scheduler.dart.md`). The single
      `lib/bytecode/runner.dart` import maps to
      `using <RootNs>.Bytecode;` (per `lib/bytecode/runner.dart.md`).
      Net result: FOUR `using` lines for the nine Dart imports.
      .
      Dart `show <symbol>` clauses (used on `partial_evaluator` —
      `show setPreludeUnitClauseSource` — and `type_environment_builder`
      — `show setPreludeEnvironmentSource`) are narrowing imports
      restricting which top-level names enter scope. C# has no
      per-`using` symbol-filter syntax — `using` brings the whole
      namespace into scope. The `show` narrowing has NO observable
      effect at this test site because the two narrowed symbols are
      already the ONLY symbols this file references from those two
      files (the corresponding host static classes
      `PreludeUnitClauses` and `PreludeEnvironment` per the lib specs
      already encapsulate the narrowing). Codegen MAY drop the
      narrowing entirely; no `using static`-with-filter pattern is
      required.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cached idiom (precedents: boot_loader_test.dart.md,
      partial_evaluator_test.dart.md, module_parser_test.dart.md,
      module_syntax_v2_test.dart.md, cssg_modules_test.dart.md).
      Granularity-mismatch nuance (carry-forward, load-bearing):
      Dart imports are file-grained; C# `using` is namespace-grained —
      same-directory Dart files collapse into a single C# `using`.
      `show` narrowing nuance EXPLICITLY addressed (new facet here vs
      partial_evaluator_test.dart.md): Dart `import '...' show
      <symbol>` has no direct C# counterpart — the narrowing
      requirement is satisfied by the LIB SPEC having already moved
      those top-level symbols onto distinct host static classes, so
      the test site need only reference the host class by qualified
      name. Codegen drops the `show` clause and emits the unqualified
      namespace-grained `using`. Symbol visibility: every imported
      symbol used here (`discoverProject`, `linkProject`,
      `typeCheckProject`, `DiscoveredModule`, `LinkResult`,
      `GlpCompiler`, `setPreludeUnitClauseSource`,
      `setPreludeEnvironmentSource`, `Program`, `GlpRuntime`,
      `CallEnv`, `Scheduler`, `BytecodeRunner`, `GoalRef`) is
      library-public on the Dart side (no leading underscore) and
      maps to `public` C# accessibility — no relaxation required.

  - construct_key: dart.package_test.main_entrypoint
    source_form: >-
      "void main() {
        final rootSelfGlp = File('../programs/self.glp');
        if (rootSelfGlp.existsSync()) {
          final source = rootSelfGlp.readAsStringSync();
          setPreludeUnitClauseSource(source);
          setPreludeEnvironmentSource(source);
        }
        final cssgRoot = '../programs/cssg_modules';
        final rootSelfPath = rootSelfGlp.existsSync() ? rootSelfGlp.absolute.path : null;
        if (!Directory(cssgRoot).existsSync()) {
          print('cssg_modules directory not found at $cssgRoot, skipping tests');
          return;
        }
        group('Project discovery', () { ... });
        group('Type checking', () { ... });
        group('Linking', () { ... });
        group('End-to-end compilation', () { ... });
      }"
    target_decision: >-
      Eliminate the Dart `void main()` per-file entrypoint entirely
      (xUnit discovers `[Fact]` methods by reflection). The body
      decomposes into FIVE target shapes:
      .
      (1) The PRE-GROUP file-IO block (the `File('../programs/self.glp')`
      existence check + `setPreludeUnitClauseSource(source)` +
      `setPreludeEnvironmentSource(source)`) lifts into a `static`
      constructor on a SHARED test-base class
      `ProjectLinkerTestsBase` (or directly on EACH of the four
      lifted test classes — see topology nuance below). REUSE the
      `static` constructor approach pinned by
      `partial_evaluator_test.dart.md` and `cssg_modules_test.dart.md`
      (Microsoft Learn "Static constructors" at
      `https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/static-constructors`).
      .
      (2) The `cssgRoot` Dart `final` local + the `rootSelfPath` Dart
      `final` local (ternary-derived) lift onto the shared base as
      `private const string CssgRoot = "../programs/cssg_modules";`
      and `private static readonly string? RootSelfPath = ...;` (see
      `dart.local_var.final_conditional_nullable_path_from_file_absolute`).
      `RootSelfPath` is `static readonly` (NOT `const`) because its
      initialiser is `File.Exists(...) ? new FileInfo(...).FullName :
      null` — non-compile-time-constant.
      .
      (3) The early-return skip guard
      `if (!Directory(cssgRoot).existsSync()) { print(...); return; }`
      lifts to the static-ctor — but the SKIP semantics ("skip all
      tests") needs special handling: see
      `dart.platform.directory_existsSync_skip_with_print` below.
      Spec default: emit the directory-check + `Console.WriteLine`
      in the static-ctor but DO NOT throw — instead set a
      `static readonly bool CssgRootExists` flag and gate every
      `[Fact]` body with `Assert.SkipWhen(!CssgRootExists, "<msg>");`
      (xUnit v3 `Assert.SkipWhen` — see
      `https://xunit.net/docs/skipping-tests`) OR `Skip.IfNot(...)`
      (Xunit.SkippableFact NuGet package, xUnit v2). Codegen picks
      the form matching the langpair's pinned xUnit major version.
      .
      (4) Each of the four `group(...)` calls lifts to one nested
      OR sibling test class on the shared base — see
      `dart.package_test.four_sibling_top_level_groups_in_one_main`.
      .
      (5) The `setUp(() { modules = ...; linkResult = ...; linked =
      ...; })` inside the `Linking` group lifts to the constructor
      of the `LinkingTests` class — see
      `dart.package_test.setUp_inside_group_with_three_late_fields`.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Cached idiom (precedents: boot_loader_test.dart.md,
      partial_evaluator_test.dart.md, cssg_modules_test.dart.md).
      Pre-group-init nuance (carry-forward from
      partial_evaluator_test.dart.md, EXPLICITLY addressed): this
      file has a THREE-STEP pre-`group` block (TWO prelude setters
      sharing one `File.ReadAllText`, plus a directory-existence
      precondition that uses `print(...) + return;` — softer than
      `cssg_modules_test.dart`'s `throw StateError`). All three side
      effects belong in a `static` constructor that runs before any
      `[Fact]`. Skip-vs-throw nuance EXPLICITLY addressed (new facet
      here, contrast with cssg_modules_test.dart.md): the Dart code
      uses `print(...) + return;` (a SOFT skip — the test file
      compiles and silently does nothing) rather than `throw
      StateError` (a HARD failure). The C# equivalent is xUnit's
      `Skip` mechanism, not `throw new InvalidOperationException`.
      Local-function lift nuance: there are NO Dart local functions
      in `main` outside the `group` bodies (the `setUp` callback
      sits inside the `Linking` group).

  - construct_key: dart.platform.file_existsSync_readAsStringSync
    source_form: >-
      "final rootSelfGlp = File('../programs/self.glp');
      if (rootSelfGlp.existsSync()) {
        final source = rootSelfGlp.readAsStringSync();
        setPreludeUnitClauseSource(source);
        setPreludeEnvironmentSource(source);
      }"
    target_decision: >-
      Map Dart `File('<path>')` + `.existsSync()` + `.readAsStringSync()`
      to the C# static-class form `System.IO.File.Exists(<path>)` +
      `System.IO.File.ReadAllText(<path>)` per the cached
      `rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext`
      idiom (precedents: partial_evaluator_test.dart.md,
      cssg_modules_test.dart.md). Emitted form inside the static
      constructor: `if (File.Exists("../programs/self.glp")) { var
      source = File.ReadAllText("../programs/self.glp");
      PreludeUnitClauses.SetPreludeUnitClauseSource(source);
      PreludeEnvironment.SetPreludeEnvironmentSource(source); }`. The
      Dart variable `rootSelfGlp` is also referenced LATER (for
      `rootSelfGlp.absolute.path` — see
      `dart.local_var.final_conditional_nullable_path_from_file_absolute`),
      so codegen MUST preserve the `File`-like instance variable. The
      idiomatic C# equivalent uses `System.IO.FileInfo` for the
      "store the path-bound instance and reuse it" shape:
      `var rootSelfGlp = new FileInfo("../programs/self.glp"); if
      (rootSelfGlp.Exists) { var source = File.ReadAllText(
      rootSelfGlp.FullName); ... }`. The `.Exists` property on
      `FileInfo` is documented at
      `https://learn.microsoft.com/dotnet/api/system.io.fileinfo.exists`
      (Microsoft Learn `System.IO.FileInfo.Exists`).
    idiom_id: rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext
    research_finding_id: rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext
    nuance: >-
      Cached idiom (precedents: partial_evaluator_test.dart.md,
      cssg_modules_test.dart.md). All three cached nuances
      (instance-vs-static; sync-vs-async; encoding) carry forward
      verbatim. NEW facet EXPLICITLY addressed here (a HYBRID of the
      partial_evaluator-style and cssg_modules-style precedents):
      this file STORES the `File`-instance into a Dart `final` local
      (`rootSelfGlp`) because it is referenced TWICE — once for
      existence + read, then later for `.absolute.path`. C#'s
      idiomatic match is `System.IO.FileInfo` (instance class with
      `Exists` property and `FullName` property — the path-bound
      instance shape) rather than `System.IO.File` (static-only).
      Codegen MUST emit a `FileInfo` for this file even though
      partial_evaluator_test.dart.md and cssg_modules_test.dart.md
      could get away with the static-only form (their `File`
      instances are read-once). The two prelude setters are called
      with the SAME `source` argument from the single
      `File.ReadAllText` (no duplicate read).

  - construct_key: dart.platform.directory_existsSync_skip_with_print
    source_form: >-
      "if (!Directory(cssgRoot).existsSync()) {
        print('cssg_modules directory not found at $cssgRoot, skipping tests');
        return;
      }"
    target_decision: >-
      Map Dart `Directory('<path>').existsSync()` to C# static
      `System.IO.Directory.Exists(<path>)` per the cached
      `rf-dart-directory-existssync-to-system-io-directory-exists`
      idiom (precedents: `lib/runtime/module_hierarchy.dart.md`,
      `lib/compiler/project_linker.dart.md`,
      `cssg_modules_test.dart.md`). The Dart `!` negation maps to
      identical C# `!`. The SOFT-SKIP semantics
      (`print(...) + return;`) does NOT map to `throw new
      InvalidOperationException` (that would be the HARD-FAIL shape
      from `cssg_modules_test.dart`'s `throw StateError`); it maps
      to xUnit's per-test skip mechanism. The pre-group sets a
      `private static readonly bool CssgRootExists =
      Directory.Exists(CssgRoot);` flag; each `[Fact]` body opens
      with `Assert.SkipWhen(!CssgRootExists, "cssg_modules directory
      not found at ../programs/cssg_modules, skipping tests");`
      (xUnit v3 — see "Skipping tests" docs at
      `https://xunit.net/docs/skipping-tests` which describes
      `Assert.Skip`, `Assert.SkipWhen`, and `Assert.SkipUnless`) OR
      the `Skip="..."` argument on `[Fact]` if a non-runtime
      condition were applicable (here the condition is runtime —
      directory existence — so the in-method `Assert.SkipWhen` form
      is correct). On xUnit v2 (no built-in skip) the equivalent is
      the `Xunit.SkippableFact` NuGet package's
      `[SkippableFact]` attribute + `Skip.IfNot(CssgRootExists,
      "<msg>")` call. Codegen picks the form matching the langpair's
      pinned xUnit major version (xUnit v3 default per
      smoke_test.dart.md's "modern xUnit" assumption).
      .
      Per-Fact `Console.WriteLine` of the skip message is OPTIONAL —
      xUnit reports the `Assert.SkipWhen` reason automatically.
    idiom_id: rf-dart-directory-existssync-to-system-io-directory-exists
    research_finding_id: rf-dart-directory-existssync-to-system-io-directory-exists
    nuance: >-
      Cached idiom partial-reuse (the `Directory.Exists` half is
      cached). Skip-vs-throw nuance EXPLICITLY addressed (NEW facet
      in this batch, NOT cached by `cssg_modules_test.dart.md` which
      took the throw route): Dart code that uses `print(msg) +
      return;` from `main` BEFORE registering any `test()` cases is
      asking "skip the whole test file silently" — semantically a
      RUNNER-LEVEL SKIP, not a fatal error. xUnit's matching primitive
      is `Assert.SkipWhen`/`Assert.SkipUnless` (v3) or the
      SkippableFact package (v2). Wrapping in `throw new
      InvalidOperationException` would convert a silent skip into a
      failing test class init (`TypeInitializationException`) —
      semantically WRONG. The skip-flag pattern (`static readonly
      bool` set in the static ctor + per-Fact `Assert.SkipWhen`) is
      the faithful conversion. Diagnostic-quality nuance: the Dart
      `print(...)` emits to stdout once; xUnit's skip mechanism
      emits the reason once per skipped test — slightly noisier in
      reporters, but no information loss.

  - construct_key: dart.module.global_setter_function
    source_form: |-
      "setPreludeUnitClauseSource(source);
       setPreludeEnvironmentSource(source);"
    target_decision: >-
      Map each free top-level setter call to a `public static void`
      method on its host class. Per the lib specs:
      `setPreludeUnitClauseSource` is hosted by `internal static
      class PreludeUnitClauses` (per
      `lib/compiler/partial_evaluator.dart.md`
      `dart.toplevel.mutable_global_nullable_string_init_to_null_and_setter_function`);
      `setPreludeEnvironmentSource` is hosted by the parallel
      `internal static class PreludeEnvironment` (per
      `lib/analysis/type_checker/type_environment_builder.dart.md`).
      Emitted form (inside static ctor):
      `PreludeUnitClauses.SetPreludeUnitClauseSource(source);
       PreludeEnvironment.SetPreludeEnvironmentSource(source);`.
      Method-name PascalCase: `setPrelude...Source` →
      `SetPrelude...Source`. Spec default: emit the QUALIFIED call
      (no `using static`), per the cross-file dependency convention
      from partial_evaluator_test.dart.md.
    idiom_id: csharp-static-class-no-toplevel-members
    research_finding_id: csharp-static-class-no-toplevel-members
    nuance: >-
      Cached idiom (precedents: partial_evaluator_test.dart.md,
      glp_runtime_test.dart.md, cssg_modules_test.dart.md). Twin-setter
      nuance EXPLICITLY addressed (same shape as
      cssg_modules_test.dart.md): the two setters live on DIFFERENT
      host classes on the C# side because they originate from different
      lib files. Both calls receive the SAME `source` argument. Codegen
      MUST NOT merge or alias the two setters.

  - construct_key: dart.local_var.final_conditional_nullable_path_from_file_absolute
    source_form: >-
      "final rootSelfPath = rootSelfGlp.existsSync() ? rootSelfGlp.absolute.path : null;"
    target_decision: >-
      Map the Dart `final` local + ternary + `File.absolute.path`
      property chain to a C# `static readonly string? RootSelfPath`
      field on the shared test base, initialised with a conditional
      expression: `private static readonly string? RootSelfPath =
      RootSelfGlp.Exists ? RootSelfGlp.FullName : null;` (assuming
      the `FileInfo` shape from
      `dart.platform.file_existsSync_readAsStringSync` above). Dart's
      `File.absolute` returns a NEW `File` with an absolute path;
      `.path` returns its path as a string. The semantic composition
      "give me the absolute path string for this file" matches .NET
      `FileInfo.FullName` (Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.io.fileinfo.fullname`:
      "Gets the full path of the file"). The two are not 1:1 byte-
      identical (`FullName` resolves relative to the process CWD at
      construction time; Dart's `File.absolute.path` resolves to the
      same — Dart docs say "Returns a File whose path is the absolute
      path of this") — semantically equivalent for the
      string-path-passed-to-discoverProject use here.
      .
      Nullability: Dart `final rootSelfPath = ... ? <String> : null;`
      gives `rootSelfPath` static type `String?`; C# `string?` with
      nullable reference types enabled (the langpair convention per
      smoke_test.dart.md). The downstream pass to `discoverProject(
      ..., rootSelfGlpPath: rootSelfPath)` accepts the `null` case
      (named-optional `String? rootSelfGlpPath` on the Dart side per
      `lib/compiler/project_linker.dart.md`'s
      `dart.top_level_function.named_optional_string_param_returns_list_dart_io_recursive_walk_with_filtered_skip_logic`,
      where the C# signature uses optional named parameter `string?
      rootSelfGlpPath = null`).
    idiom_id: rf-dart-final-local-to-csharp-var
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Three intertwined nuances. (1) `final`-vs-`static readonly`:
      this `final` local is referenced from EVERY group's tests, so
      it lifts OUT of `main()` (which is being deleted) onto the
      shared base class as a `static readonly` field. The "single-
      assignment" semantics of Dart `final` is preserved by C#'s
      `static readonly` (assignable only in the static constructor or
      inline initialiser). (2) Ternary expression: Dart `cond ? a :
      b` maps 1:1 to C# `cond ? a : b` (identical operator). (3)
      `.absolute.path` vs `.FullName`: explicitly addressed above —
      C#'s `FileInfo.FullName` collapses the two-step Dart property
      chain (`.absolute` returns a `File`; `.path` returns its
      string) into a single property access. Semantically
      equivalent.

  - construct_key: dart.package_test.four_sibling_top_level_groups_in_one_main
    source_form: >-
      "group('Project discovery', () { ... 4 tests ... });
       group('Type checking', () { ... 1 test ... });
       group('Linking', () { late ...; setUp(...); ... 8 tests ... });
       group('End-to-end compilation', () { ... 2 tests ... });"
    target_decision: >-
      Each Dart `group(label, body)` maps to ONE xUnit test class per
      the canonical `rf-dart-package-test-group-to-xunit-class` idiom
      (precedents: smoke_test → boot_loader_test → heap/* → module/*
      → analysis/type_checker/* → partial_evaluator_test.dart.md ).
      Four sibling top-level groups ⇒ FOUR sibling test classes in
      the file (contrast with cssg_modules_test.dart.md's single-
      group topology). All four classes derive from a shared
      `public abstract class ProjectLinkerTestsBase` that hosts the
      static initialiser (prelude load + `CssgRoot` + `RootSelfGlp`
      + `RootSelfPath` + `CssgRootExists` flag) so every derived
      class sees the same pre-test setup. PascalCased class names
      from the group labels (spaces/hyphens dropped + `Tests`
      suffix):
      .
      - `'Project discovery'` → `ProjectDiscoveryTests` (4 `[Fact]`)
      - `'Type checking'` → `TypeCheckingTests` (1 `[Fact]`)
      - `'Linking'` → `LinkingTests` (8 `[Fact]` + 3 `late` fields +
        constructor running the `setUp` body)
      - `'End-to-end compilation'` → `EndToEndCompilationTests`
        (2 `[Fact]`)
      .
      Each inner `test('<label>', () { ... })` becomes one
      `[Fact(DisplayName = "<label>")]` method on the respective
      class.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Cached idiom (precedents: smoke_test through every batch test
      spec). Topology nuance EXPLICITLY addressed (CONTRAST with
      partial_evaluator_test.dart.md's single-group and
      cssg_modules_test.dart.md's single-group): this file has FOUR
      sibling top-level groups, so FOUR classes — same shape as
      module_parser_test.dart.md (six classes) and
      moded_head_test.dart.md (three classes). Shared-base-class
      nuance (NEW facet for this file, EXPLICITLY addressed): the
      pre-group block + the `CssgRoot`/`RootSelfPath`/`CssgRootExists`
      fields are referenced by EVERY class — so they lift to a
      shared `public abstract class ProjectLinkerTestsBase` (or a
      `static class TestFixture` with public static fields the
      classes read). Spec default: abstract base class with `static`
      constructor — keeps initialisation cohesive and per-Fact skip
      gating ergonomic. xUnit class-fixtures (`IClassFixture<T>`)
      and collection fixtures are NOT needed because the shared
      state is process-scoped via `static` members, not instance-
      scoped. Per-Fact fresh-instance nuance: xUnit recreates each
      derived class per `[Fact]`; the static-base-class state is
      built once per AppDomain — semantically matches Dart `main`'s
      "runs once per test-file process".

  - construct_key: dart.package_test.setUp_inside_group_with_three_late_fields
    source_form: >-
      "group('Linking', () {
        late List<DiscoveredModule> modules;
        late LinkResult linkResult;
        late Program linked;
        setUp(() {
          modules = discoverProject(cssgRoot, rootSelfGlpPath: rootSelfPath);
          linkResult = linkProject(modules, 'boot');
          linked = linkResult.program;
        });
        test('procedures are renamed with module prefix', () { ... });
        ...
      });"
    target_decision: >-
      Within `LinkingTests` (the lifted class for the `'Linking'`
      group), the three `late` field declarations lift to three
      `private` non-nullable fields with the null-forgiving
      initialiser pattern (cached
      `rf-dart-late-field-to-csharp-nullforgiving-field` idiom from
      boot_loader_test.dart.md and partial_evaluator_test.dart.md):
      `private List<DiscoveredModule> _modules = null!;`
      `private LinkResult _linkResult = null!;`
      `private Program _linked = null!;`.
      The `setUp(() { ... })` callback body lifts to the test
      class's INSTANCE CONSTRUCTOR — xUnit's fresh-instance-per-Fact
      semantics makes the constructor the per-test setup hook
      (precedent partial_evaluator_test.dart.md). Emitted:
      `public LinkingTests() {
         _modules = ProjectLinker.DiscoverProject(CssgRoot,
                       rootSelfGlpPath: RootSelfPath);
         _linkResult = ProjectLinker.LinkProject(_modules, "boot");
         _linked = _linkResult.Program;
       }`. The Dart top-level functions `discoverProject`/
      `linkProject` (per `lib/compiler/project_linker.dart.md`)
      host on the converted `public static class ProjectLinker` (per
      `csharp-static-class-no-toplevel-members` cached idiom); the
      qualified `ProjectLinker.DiscoverProject(...)` /
      `ProjectLinker.LinkProject(...)` call form matches the cross-
      file qualification convention. The Dart NAMED argument
      `rootSelfGlpPath: rootSelfPath` maps to C# named argument
      `rootSelfGlpPath: RootSelfPath` (per
      `rf-dart-named-argument-to-csharp-named-argument` cached
      idiom). The Dart `final` reads `linkResult.program` →
      PascalCase property `_linkResult.Program` (per
      `rf-dart-camelcase-field-to-csharp-pascalcase-property`).
    idiom_id: rf-dart-late-field-to-csharp-nullforgiving-field
    research_finding_id: rf-dart-late-field-to-csharp-nullforgiving-field
    nuance: >-
      Cached idiom (precedents: boot_loader_test.dart.md,
      partial_evaluator_test.dart.md). Three-field-setup nuance
      EXPLICITLY addressed (new facet vs single-field precedents):
      all three fields are assigned in the constructor before any
      `[Fact]` reads them — `_linkResult` depends on `_modules`,
      `_linked` depends on `_linkResult`, so the assignment ORDER
      MUST be preserved (matches the Dart `setUp` order). xUnit
      lifecycle: constructor runs per-Fact, so each test sees a
      fresh build over the same on-disk `CssgRoot` — matches Dart
      `setUp` "runs before every test" semantics. NULL-forgiving
      nuance: `private T _x = null!;` silences nullable-context
      warnings; the constructor MUST assign before any `[Fact]`
      reads — guaranteed by xUnit lifecycle.

  - construct_key: dart.iterable.map_toset_member_access
    source_form: |-
      "final names = modules.map((m) => m.moduleName).toSet();
       final filenames = modules.map((m) => m.filePath).toList();
       final procNames = linked.procedures.map((p) => p.name).toSet();
       final prefixedProcs = linked.procedures.where((p) => p.name.contains(':')).map((p) => p.name).toSet();
       final mergeProcs = linked.procedures.where((p) => p.name.endsWith(':merge')).toList();
       final mergeNames = mergeProcs.map((p) => p.name).toSet();
       final bootProcNames = bootModule.ast.procedures.map((p) => p.name).toSet();"
    target_decision: >-
      Map Dart `Iterable.map((x) => x.field).toSet()` to C# LINQ
      `Select(x => x.Field).ToHashSet()` (REUSE
      `rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset`,
      precedent: cssg_modules_test.dart.md, suspension_pointer_test.dart.md,
      linter_ok_test.dart.md, binding_pointer_test.dart.md). Dart
      `.toList()` ⇒ `.ToList()`
      (REUSE `rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist`
      where preceded by `where`; for bare `.toList()` use
      `.ToList()` directly). Dart `.where((p) => predicate).toList()`
      ⇒ `.Where(p => predicate).ToList()`. Field access `.moduleName`
      / `.filePath` / `.name` ⇒ PascalCase property
      `.ModuleName`/`.FilePath`/`.Name`
      (`rf-dart-camelcase-field-to-csharp-pascalcase-property`
      cached). Lambda `(m) => ...` ⇒ C# `m => ...`
      (`rf-dart-arrow-lambda-to-csharp-lambda` cached). String
      method calls: `.contains(':')` ⇒ `.Contains(':')`,
      `.endsWith(':merge')` ⇒ `.EndsWith(":merge")` (per
      Microsoft Learn `System.String.Contains` and `EndsWith` —
      `https://learn.microsoft.com/dotnet/api/system.string.contains`
      and `https://learn.microsoft.com/dotnet/api/system.string.endswith`).
      .
      Codegen MUST emit `using System.Linq;` and `using
      System.Collections.Generic;` at file head.
    idiom_id: rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset
    research_finding_id: rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset
    nuance: >-
      Cached idiom (precedents listed above). Hash-set
      mutability nuance (carry-forward): Dart `Set<T>` from
      `toSet()` is mutable; C# `HashSet<T>` from `ToHashSet()`
      is also mutable — same semantics. Equality nuance: both
      sides use structural string equality for `string` keys —
      no `IEqualityComparer<string>` override needed. Chain-
      composition nuance EXPLICITLY addressed (new facet here):
      this file uses BOTH `.map(...).toSet()` and
      `.where(...).map(...).toSet()` — the LINQ chain
      `Where(...).Select(...).ToHashSet()` is the natural composition;
      no intermediate materialisation needed. Eager-vs-lazy: Dart
      `.map(...)` is lazy, materialised by `.toSet()`/`.toList()`;
      LINQ `Select` is lazy, materialised by `ToHashSet`/`ToList`
      — semantically equivalent.

  - construct_key: dart.iterable.firstWhere_with_lambda_predicate
    source_form: |-
      "final bootPlay1 = linked.procedures.firstWhere((p) => p.name == 'boot:play1');
       final sendTagged = linked.procedures.firstWhere((p) => p.name == 'boot:send_to_user_tagged');
       final bootModule = modules.firstWhere((m) => m.moduleName == 'boot');
       final play1Alias = linked.procedures.firstWhere((p) => p.name == 'play1');"
    target_decision: >-
      Map Dart `Iterable.firstWhere(predicate)` to C# LINQ `First(
      predicate)` (NOT `FirstOrDefault` — Dart `firstWhere` throws
      `StateError` if no element matches, Microsoft Learn LINQ
      `Enumerable.First<TSource>(IEnumerable<TSource>, Func<TSource,
      bool>)` at
      `https://learn.microsoft.com/dotnet/api/system.linq.enumerable.first`
      "Throws InvalidOperationException if no element satisfies the
      condition" — semantically equivalent). Emitted: `var bootPlay1
      = _linked.Procedures.First(p => p.Name == "boot:play1");`,
      `var sendTagged = _linked.Procedures.First(p => p.Name ==
      "boot:send_to_user_tagged");`, `var bootModule = _modules.First(
      m => m.ModuleName == "boot");`, `var play1Alias = _linked.
      Procedures.First(p => p.Name == "play1");`. Dart `==` string
      equality maps to C# `==` (string overload, ordinal/case-
      sensitive — see Microsoft Learn `System.String.op_Equality`).
      First-seen idiom in this batch (NEW: registers
      `rf-dart-iterable-firstwhere-to-csharp-linq-first`).
    idiom_id: rf-dart-iterable-firstwhere-to-csharp-linq-first
    research_finding_id: rf-dart-iterable-firstwhere-to-csharp-linq-first
    nuance: >-
      Throwing-vs-default nuance (load-bearing, EXPLICITLY addressed):
      Dart `Iterable.firstWhere(test)` documentation (Dart api.dart.dev
      `https://api.dart.dev/stable/dart-core/Iterable/firstWhere.html`)
      says "If no such element is found, the result of invoking the
      orElse function is returned. If orElse is omitted, it defaults
      to throwing a StateError." This file omits `orElse`, so the
      Dart semantics is "throw on no match". The C# counterpart is
      `Enumerable.First(predicate)` — which throws
      `InvalidOperationException` on no match per the cited
      Microsoft Learn page. The OTHER LINQ option,
      `FirstOrDefault(predicate)`, returns `default(T)` (likely
      `null` for reference types) on no match — SEMANTICALLY WRONG
      here (would silently substitute null and then NRE downstream).
      Codegen MUST emit `First(...)`, NOT `FirstOrDefault(...)`. The
      `StateError` vs `InvalidOperationException` mapping is
      symmetric with `rf-dart-stateerror-throw-to-csharp-invalidoperationexception`
      cached above.

  - construct_key: dart.iterable.any_with_lambda_predicate
    source_form: "expect(filenames.any((f) => f.contains('boot_direct')), isFalse, reason: 'boot_direct.glp should be excluded');"
    target_decision: >-
      Map Dart `Iterable.any(predicate)` to C# LINQ `Any(predicate)`
      (Microsoft Learn `Enumerable.Any<TSource>(IEnumerable<TSource>,
      Func<TSource, bool>)` at
      `https://learn.microsoft.com/dotnet/api/system.linq.enumerable.any`
      "Determines whether any element of a sequence satisfies a
      condition"). The Dart `String.contains(substr)` ⇒ C#
      `String.Contains(substr)`. The wrapping `expect(..., isFalse,
      reason: '...')` ⇒ xUnit `Assert.False(...)` with the reason
      moved to an in-method comment (xUnit's `Assert.False(bool,
      string)` overload IS available — Microsoft Learn xUnit
      `Assert.False(Boolean, String)` at
      `https://learn.microsoft.com/dotnet/api/xunit.assert.false` —
      so codegen MAY pass the message as the 2nd argument). REUSE
      `rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq`
      (precedent: linter_body_precommit_test.dart.md) — symmetric
      `Any/False` form. Emitted: `Assert.False(filenames.Any(f =>
      f.Contains("boot_direct")), "boot_direct.glp should be
      excluded");`.
    idiom_id: rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq
    research_finding_id: rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq
    nuance: >-
      Cached idiom (precedent: linter_body_precommit_test.dart.md).
      Negation-direction nuance EXPLICITLY addressed: this site uses
      `expect(<any>, isFalse)` (assert "no element matches"); the
      cached precedent often uses `expect(<any>, isTrue)` (assert
      "some element matches"). Both shapes resolve to LINQ `.Any(...)`
      — the difference is `Assert.True` vs `Assert.False`. Could
      alternatively use `Assert.True(filenames.All(f => !f.Contains
      ("boot_direct")), ...)` for the "none-match" semantics, but
      `Assert.False(...Any...)` is the closer Dart-shape mirror and
      is the spec default.

  - construct_key: dart.package_test.expect_collection_contains_string
    source_form: |-
      "expect(names, contains('agent'));
       expect(names, isNot(contains('self')));
       expect(procNames, contains('agent:agent'));"
    target_decision: >-
      Map Dart `expect(<set/list>, contains(<value>))` to xUnit
      `Assert.Contains(<value>, <collection>)` (Microsoft Learn
      `Xunit.Assert.Contains<T>(T, IEnumerable<T>)` at
      `https://learn.microsoft.com/dotnet/api/xunit.assert.contains`).
      Map Dart `expect(<set/list>, isNot(contains(<value>)))` to
      xUnit `Assert.DoesNotContain(<value>, <collection>)` (Microsoft
      Learn `Xunit.Assert.DoesNotContain<T>(T, IEnumerable<T>)`).
      First-seen idiom in this batch (NEW: registers
      `rf-dart-expect-collection-contains-to-xunit-assert-contains`).
      Concrete emissions: `Assert.Contains("agent", names);`,
      `Assert.DoesNotContain("self", names);`,
      `Assert.Contains("agent:agent", procNames);`.
    idiom_id: rf-dart-expect-collection-contains-to-xunit-assert-contains
    research_finding_id: rf-dart-expect-collection-contains-to-xunit-assert-contains
    nuance: >-
      Authoritative both sides. Dart `package:matcher` `contains`
      matcher at `https://pub.dev/documentation/matcher/latest/matcher/contains.html`:
      "An object that matches if the expected value is contained in
      the actual value (a List, Set, Map, or String)." xUnit
      `Assert.Contains<T>(T, IEnumerable<T>)` is documented at the
      Microsoft Learn link above — "Verifies that a collection
      contains a given object". String overload vs collection
      overload: this file passes `Set<String>` / `Set<String>` /
      `List<String>` arguments (NOT a single String), so the
      collection-overload `Assert.Contains<T>(T, IEnumerable<T>)` is
      the right resolution — NOT `Assert.Contains(string, string)`
      (substring check). Codegen MUST resolve to the IEnumerable
      overload for these calls. Negation form
      (`isNot(contains(...))`) maps cleanly to
      `Assert.DoesNotContain` per the symmetric Microsoft Learn
      reference.

  - construct_key: dart.package_test.expect_length_equals
    source_form: |-
      "expect(names.length, equals(5));
       expect(mergeProcs.length, greaterThanOrEqualTo(2), reason: '...');
       expect(play1Alias.clauses.length, equals(1));
       expect(body!.length, equals(1));"
    target_decision: >-
      For `expect(collection.length, equals(N))` map to xUnit
      `Assert.Equal(N, collection.Count)` (REUSE
      `rf-dart-expect-equals-to-xunit-assert-equal-argorder` —
      EXPECTED-FIRST argument order per Microsoft Learn xUnit
      `Assert.Equal<T>(T, T)` at
      `https://learn.microsoft.com/dotnet/api/xunit.assert.equal`).
      Dart `.length` on `List<T>`/`Set<T>` ⇒ C# `.Count` per
      `rf-dart-list-length-to-csharp-list-count` cached.
      .
      For `expect(collection.length, greaterThanOrEqualTo(N),
      reason: '...')` — Dart `package:matcher` `greaterThanOrEqualTo`
      maps to xUnit `Assert.True(collection.Count >= N, "<reason>")`
      (xUnit has no built-in `Assert.GreaterOrEqual` — see xunit.net
      FAQ "What's missing" at `https://xunit.net/docs/comparisons`;
      the canonical workaround is `Assert.True(expression,
      message)`). First-seen idiom in this batch for the GTE form
      (NEW: registers
      `rf-dart-expect-length-greaterthanorequalto-to-xunit-assert-true`).
      Emitted: `Assert.True(mergeProcs.Count >= 2, "agent:merge
      and boot:merge should both exist");`.
    idiom_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Two cached idioms applied (equals + length-to-count). New
      facet: greaterThanOrEqualTo. xUnit-omission nuance EXPLICITLY
      addressed: xUnit deliberately omits `Assert.GreaterOrEqual` /
      `Assert.LessOrEqual` (xUnit team's "positive assertions only"
      stance — see also the omission of `Assert.DoesNotThrow`).
      The faithful translation uses `Assert.True(comparison,
      message)` with the message preserving the Dart `reason:`
      string. Bang-operator nuance EXPLICITLY addressed for
      `body!.length`: Dart `body!` asserts non-null on a `List<Goal>?`
      → C# `body!` (null-forgiving) per
      `rf-dart-bang-to-csharp-null-forgiving` cached. Emitted:
      `Assert.Equal(1, body!.Count);`.

  - construct_key: dart.package_test.expect_isNotEmpty_with_reason
    source_form: |-
      "expect(aliases, isNotEmpty, reason: 'Entry point alias should exist for $name');
       expect(output, isNotEmpty, reason: 'fplay1 should produce tagged output');"
    target_decision: >-
      Map Dart `expect(<collection>, isNotEmpty)` to xUnit
      `Assert.NotEmpty(<collection>)` (REUSE
      `rf-dart-expect-isNotEmpty-to-xunit-assert-notempty`, precedent:
      mad_scenarios_test.dart.md or sibling — Microsoft Learn
      `Xunit.Assert.NotEmpty(IEnumerable)` at
      `https://learn.microsoft.com/dotnet/api/xunit.assert.notempty`).
      The Dart `reason:` named-argument string is REASON-MOVE: xUnit
      `Assert.NotEmpty(IEnumerable)` has no second `message`
      parameter, so the reason cannot be preserved as a method
      argument. Codegen options: (a) drop the reason (xUnit's
      diagnostic on `Assert.NotEmpty` failure is "Assert.NotEmpty()
      Failure: Collection was empty" — adequate for the unique
      assertion contexts here); (b) replace with `Assert.True(
      aliases.Any(), $"Entry point alias should exist for {name}");`
      to preserve the reason via the `Assert.True(bool, string)`
      overload. Spec default: option (b) — preserve the reason via
      `Assert.True(<collection>.Any(), <reason>)`, because the Dart
      `reason:` string in the first call contains a `$name`
      interpolation that conveys WHICH alias is missing (not
      preservable via option (a)).
    idiom_id: rf-dart-expect-isNotEmpty-to-xunit-assert-notempty
    research_finding_id: rf-dart-expect-isNotEmpty-to-xunit-assert-notempty
    nuance: >-
      Cached idiom partial-reuse (the empty-check half is cached).
      Reason-preservation nuance EXPLICITLY addressed (NEW facet in
      this batch): xUnit's `Assert.NotEmpty` has no message overload
      — Microsoft Learn confirms the only signature is
      `Assert.NotEmpty(IEnumerable)`. The faithful conversion when
      the Dart `reason:` string is informative (especially with
      interpolation) is to switch to `Assert.True(<col>.Any(),
      <msg>)` — which preserves the message verbatim. Spec default:
      reason-preserving form for THIS file. Interpolation nuance
      (carry-forward): Dart `'... $name ...'` ⇒ C# `$"... {name} ..."`
      per `rf-dart-string-interpolation-to-csharp-interpolated-string`
      cached.

  - construct_key: dart.package_test.expect_isNotNull
    source_form: |-
      "expect(mod.ancestorScope, isNotNull, reason: '${mod.moduleName} should have an ancestor scope');
       expect(body, isNotNull);"
    target_decision: >-
      Map Dart `expect(<nullable>, isNotNull)` to xUnit
      `Assert.NotNull(<value>)` (REUSE
      `rf-dart-expect-isNotNull-to-xunit-assert-notnull`, precedent:
      mad_scenarios_test.dart.md; Microsoft Learn `Xunit.Assert.NotNull
      (Object)` at `https://learn.microsoft.com/dotnet/api/xunit.assert.notnull`).
      Reason preservation: same as `dart.package_test.expect_isNotEmpty_with_reason`
      above — `Assert.NotNull` has no message overload, so reason-
      bearing sites emit `Assert.True(<value> is not null,
      <msg>)` to preserve the reason. The second occurrence (`expect(
      body, isNotNull)`) has no reason — emits the canonical
      `Assert.NotNull(body);`.
    idiom_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    research_finding_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    nuance: >-
      Cached idiom partial-reuse + reason-preservation facet
      EXPLICITLY addressed (same shape as the `isNotEmpty` row).
      Pattern-vs-`!=null` nuance: C# 9+ pattern matching
      `<value> is not null` is preferred over `<value> != null` for
      reference types under nullable reference types (Microsoft
      Learn "Patterns - Constant pattern" at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/patterns#constant-pattern`).
      Both compile identically; spec default = `is not null`.

  - construct_key: dart.package_test.expect_isTrue_isFalse
    source_form: |-
      "expect(prefixedProcs.contains('merge'), isFalse);
       expect(prefixedProcs, contains('agent:merge'));
       expect(bytecode.labels.containsKey('boot:play1/0'), isTrue);
       expect(bytecode.labels.containsKey('play1/0'), isTrue, reason: 'Entry point alias should be in bytecode');"
    target_decision: >-
      Map Dart `expect(<bool>, isTrue)` / `expect(<bool>, isFalse)`
      to xUnit `Assert.True(<bool>)` / `Assert.False(<bool>)` (REUSE
      `rf-dart-expect-isTrue-to-xunit-assert-true` and
      `rf-dart-expect-isFalse-to-xunit-assert-false`, precedents:
      smoke_test.dart.md, mad_scenarios_test.dart.md). With `reason:`
      use the two-arg overload `Assert.True(bool, string)` /
      `Assert.False(bool, string)` (Microsoft Learn — these BOTH
      have message overloads, in contrast to `Assert.NotEmpty`/
      `Assert.NotNull` above). Dart `Map<K,V>.containsKey(K)` ⇒
      C# `IDictionary<K,V>.ContainsKey(K)` (or `Dictionary<K,V>.ContainsKey`)
      — the access pattern translates directly. Dart `Set<T>.contains(T)`
      ⇒ C# `HashSet<T>.Contains(T)`. Emitted: `Assert.False(
      prefixedProcs.Contains("merge"));`, `Assert.Contains("agent:merge",
      prefixedProcs);` (for the second line — a contains-on-collection
      shape, see `dart.package_test.expect_collection_contains_string`
      above), `Assert.True(bytecode.Labels.ContainsKey("boot:play1/0"));`,
      `Assert.True(bytecode.Labels.ContainsKey("play1/0"), "Entry
      point alias should be in bytecode");`.
    idiom_id: rf-dart-expect-isTrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Cached idiom (precedents: smoke_test.dart.md and the entire
      batch). Message-overload nuance EXPLICITLY addressed: xUnit's
      `Assert.True(bool, string)` and `Assert.False(bool, string)`
      DO accept a message — Microsoft Learn confirms both
      signatures. The `reason:` argument moves to the second
      argument naturally. Set-`contains` vs map-`containsKey`
      nuance: `Set.contains` and `Map.containsKey` are distinct on
      both sides — Dart `Map.containsKey` ⇒ C# `ContainsKey`; Dart
      `Set.contains` ⇒ C# `Contains` on `HashSet<T>`/`ISet<T>`. The
      `bytecode.labels.containsKey(...)` sites use the Map shape;
      the `prefixedProcs.contains(...)` site uses the Set shape.

  - construct_key: dart.package_test.expect_call_returnsNormally
    source_form: "expect(() => typeCheckProject(modules), returnsNormally);"
    target_decision: >-
      REUSE the cached `rf-dart-expect-returns-normally-to-xunit-bare-call`
      idiom (precedent: partial_evaluator_test.dart.md). Drop the
      assertion wrapper entirely; emit a BARE call. The Dart shape
      `expect(() => typeCheckProject(modules), returnsNormally)`
      becomes `ProjectLinker.TypeCheckProject(_modules);` on its
      own line in the `[Fact]` body. If the xUnit runner sees the
      method body complete without an uncaught exception, the test
      passes — semantically identical to `returnsNormally`'s
      contract. The top-level Dart function `typeCheckProject` lifts
      to a `public static void TypeCheckProject(...)` method on the
      converted `ProjectLinker` static class per
      `lib/compiler/project_linker.dart.md`'s
      `dart.top_level_function.type_check_per_module_throws_exception_with_per_module_error_report`.
    idiom_id: rf-dart-expect-returns-normally-to-xunit-bare-call
    research_finding_id: rf-dart-expect-returns-normally-to-xunit-bare-call
    nuance: >-
      Cached idiom — reused verbatim. xUnit "no DoesNotThrow"
      position carries forward (xunit.net FAQ / issue 2073). Single
      occurrence in this file.

  - construct_key: dart.control_flow.for_in_clauses_collect_goal_functors
    source_form: >-
      "final bodyFunctors = <String>{};
       for (final clause in bootPlay1.clauses) {
         if (clause.body != null) {
           for (final goal in clause.body!) {
             bodyFunctors.add(goal.functor);
           }
         }
       }"
    target_decision: >-
      Map Dart `for (final x in iterable) { ... }` to C# `foreach
      (var x in iterable) { ... }` (REUSE
      `rf-dart-for-in-to-csharp-foreach`, cached). Dart `final` in
      `for-in` is a single-iteration declaration; C# `var` in
      `foreach` is equivalent. Nested `for-in` over `clause.body!`
      (after non-null check) maps to nested `foreach` over
      `clause.Body!`. Dart `Set<T>` literal `<String>{}` ⇒ C#
      `new HashSet<string>()` (REUSE
      `rf-dart-set-literal-typed-to-csharp-hashset-initializer`,
      precedent: linter_ok_test.dart.md). Dart `.add(...)` on
      `Set<T>` ⇒ C# `HashSet<T>.Add(T)`. Dart `if (clause.body !=
      null)` followed by `clause.body!` is the idiomatic null-check-
      then-bang shape; C# converts to `if (clause.Body is not null)`
      and the inner access uses C# `clause.Body` (the compiler's
      flow analysis narrows the type so `!` is unnecessary, though
      emitting `clause.Body!` is also valid — Microsoft Learn
      "Definite assignment" / "Flow analysis" at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/nullable-reference-types`).
      Spec default: emit `if (clause.Body is not null) { foreach
      (var goal in clause.Body) { bodyFunctors.Add(goal.Functor); }
      }` — relies on the C# compiler's flow analysis to elide the
      bang.
    idiom_id: rf-dart-for-in-to-csharp-foreach
    research_finding_id: rf-dart-for-in-to-csharp-foreach
    nuance: >-
      Cached idiom (precedent: heap/*, lint/*). Null-check-then-bang
      nuance EXPLICITLY addressed: Dart's `body != null` + `body!`
      shape is required because Dart's flow analysis is more limited
      across statements; C#'s flow analysis is sufficient that after
      `if (clause.Body is not null)` the compiler narrows the type
      and `body!` is redundant. Spec default: drop the bang (cleaner
      C# emission). Set-literal nuance: Dart `<String>{}` is an
      empty typed set literal; C# `new HashSet<string>()` is the
      direct counterpart. Field-PascalCase: `goal.functor` ⇒
      `goal.Functor`; `clause.body` ⇒ `clause.Body`;
      `bootPlay1.clauses` ⇒ `bootPlay1.Clauses`.

  - construct_key: dart.compiler.compile_program_with_named_optional_arg
    source_form: |-
      "final compiler = GlpCompiler();
       final bytecode = compiler.compileProgram(
         result.program,
         procDeclarations: result.procDeclarations,
       );"
    target_decision: >-
      Map the implicit-`new` constructor invocation `GlpCompiler()`
      to C# `new GlpCompiler()` per
      `rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase`
      cached. The instance-method call
      `compiler.compileProgram(<program>, procDeclarations: <decls>)`
      maps to C# `compiler.CompileProgram(<program>, procDeclarations:
      <decls>)` — method name PascalCased, named argument
      preserved per
      `rf-dart-named-argument-to-csharp-named-argument` cached. The
      `result.program` and `result.procDeclarations` field reads map
      to PascalCase properties `result.Program` and
      `result.ProcDeclarations` per
      `rf-dart-camelcase-field-to-csharp-pascalcase-property` cached.
      Per `lib/compiler/compiler.dart.md` the
      `GlpCompiler.compileProgram` signature has named optional
      `Map<String, ProcDecl>? procDeclarations`, which converts to
      C# optional named parameter
      `IDictionary<string, ProcDecl>? procDeclarations = null`.
    idiom_id: rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase
    research_finding_id: rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase
    nuance: >-
      Cached idiom (precedents: parser.dart.md, lexer.dart.md,
      partial_evaluator.dart.md). Three carry-forward facets:
      implicit-new ⇒ explicit-new, camelCase ⇒ PascalCase, named
      arg ⇒ named arg. No new facets in this site.

  - construct_key: dart.compiler.compile_program_merge_to_var_reassign
    source_form: |-
      "final stdlibProg = compiler.compile(File('../programs/self.glp').readAsStringSync());
       var program = bytecode.merge(stdlibProg);"
    target_decision: >-
      Map the chained `compiler.compile(File('<path>').readAsStringSync())`
      call to C# `compiler.Compile(File.ReadAllText("<path>"))` per
      the cached `rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext`
      idiom (the existence-check half is omitted here — the call
      assumes the file exists because the static-ctor pre-check
      passed). `compiler.compile(...)` per
      `lib/compiler/compiler.dart.md` returns a `Program` (the
      bytecode unit); maps to C# instance method `Compile(string)
      → Program`. Dart `var program = bytecode.merge(stdlibProg);`
      maps to C# `var program = bytecode.Merge(stdlibProg);` — the
      `Program.merge(Program)` method per the lib spec returns a
      new merged `Program`. Dart `var` (mutable local) ⇒ C# `var`
      (mutable local) per `rf-dart-var-mutable-local-to-csharp-var-local`
      cached. The local is never reassigned in this test body
      (despite the `var` declaration) — the strict mapping is still
      `var`.
    idiom_id: rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase
    research_finding_id: rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase
    nuance: >-
      Cached idiom (multi-facet reuse). The construct does not
      check `File.Exists` before `File.ReadAllText` — the Dart code
      also skips the check at this call site, relying on the
      precondition that the file exists (the same path was checked
      successfully at file open). Codegen preserves the same shape
      (no defensive `File.Exists` check inserted; the spec discipline
      forbids adding checks not in the source — FR-024).

  - construct_key: dart.runtime.glpruntime_construction_outputcallback_assign
    source_form: |-
      "final rt = GlpRuntime();
       final output = <String>[];
       rt.outputCallback = (s) => output.add(s);"
    target_decision: >-
      Map `GlpRuntime()` to `new GlpRuntime()` per the cached
      implicit-new idiom. Dart `<String>[]` empty typed list literal
      maps to C# `new List<string>()` per
      `rf-dart-list-literal-to-csharp-list-of-T` cached. The setter
      `rt.outputCallback = (s) => output.add(s);` maps based on the
      lib spec for `lib/runtime/runtime.dart.md`'s
      `GlpRuntime.outputCallback` field. Per the cached
      `rf-dart-camelcase-field-to-csharp-pascalcase-property` idiom
      the field becomes a C# property `OutputCallback`. The Dart
      type of `outputCallback` is `void Function(String)?`; the C#
      equivalent is `Action<string>?` (Microsoft Learn
      `System.Action<T>` at
      `https://learn.microsoft.com/dotnet/api/system.action-1`:
      "Encapsulates a method that has a single parameter and does
      not return a value"). The arrow-lambda `(s) => output.add(s)`
      maps to C# `s => output.Add(s)` per
      `rf-dart-arrow-lambda-to-csharp-lambda` cached. Emitted: `var
      rt = new GlpRuntime(); var output = new List<string>(); rt.
      OutputCallback = s => output.Add(s);`.
    idiom_id: rf-dart-arrow-lambda-to-csharp-lambda
    research_finding_id: rf-dart-arrow-lambda-to-csharp-lambda
    nuance: >-
      Function-type-to-`Action<T>` nuance EXPLICITLY addressed (load-
      bearing): Dart's `void Function(String)?` is a structural
      function type with one parameter and no return; C#'s
      idiomatic counterpart is `Action<string>?`. The `?` suffix
      preserves the Dart nullability — `OutputCallback` is nullable
      on both sides (the runtime may have no callback installed).
      Assignment with a lambda (vs `Action<string>?` field) is
      consistent with Dart's first-class function-typed field. The
      lambda body `output.Add(s)` returns `void` (Dart `List.add`
      returns `void`; C# `List<T>.Add` returns `void` per Microsoft
      Learn `List<T>.Add(T)` at
      `https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.add`)
      — `Action<string>` discards return, so signature matches.

  - construct_key: dart.runtime.runners_map_indexer_assignment
    source_form: "rt.runners[program] = BytecodeRunner(program);"
    target_decision: >-
      The Dart `Map<Program, Runner>` field `runners` on `GlpRuntime`
      (per `lib/runtime/runtime.dart.md`) maps to a C#
      `IDictionary<Program, IRunner>` (or `Dictionary<Program,
      BytecodeRunner>` per lib spec). Indexer assignment is a direct
      1-to-1 mapping: Dart `map[k] = v` ⇒ C# `map[k] = v` per
      Microsoft Learn `System.Collections.Generic.Dictionary<TKey,TValue>`
      Item property indexer
      (`https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.item`)
      "Setting the value of this property [the indexer] adds or
      overwrites a value with the specified key". The Dart implicit-
      new `BytecodeRunner(program)` ⇒ C# `new BytecodeRunner(program)`
      per cached idiom. Emitted: `rt.Runners[program] = new
      BytecodeRunner(program);`.
    idiom_id: rf-dart-list-indexer-to-csharp-list-indexer
    research_finding_id: rf-dart-list-indexer-to-csharp-list-indexer
    nuance: >-
      Cached idiom partial-reuse — Dart's `Map` indexer behaviour
      ("add or overwrite" on assignment) matches C#'s
      `Dictionary<K,V>.Item` indexer-setter exactly (Microsoft
      Learn link above). Reference-vs-value-key nuance EXPLICITLY
      addressed: the key here is a `Program` instance (a class), so
      both Dart and C# use REFERENCE equality by default (Dart
      `Map<Program, ...>` uses `==` which defaults to identity for
      classes without `==` overridden; C# `Dictionary<K, V>` uses
      `EqualityComparer<K>.Default` which for reference types
      without `IEquatable<T>` defaults to reference equality per
      Microsoft Learn `EqualityComparer<T>.Default`). Per
      `lib/compiler/compiler.dart.md`'s `Program` lib spec the class
      uses reference identity — semantically equivalent.

  - construct_key: dart.runtime.scheduler_constructor_named_arg
    source_form: "final scheduler = Scheduler(rt: rt);"
    target_decision: >-
      Implicit-`new` + Dart NAMED argument `rt: rt` maps to C# `new
      Scheduler(rt: rt)` per the cached `rf-dart-named-argument-to-csharp-named-argument`
      idiom. The Dart `Scheduler` constructor per
      `lib/runtime/scheduler.dart.md` has a named REQUIRED parameter
      `required GlpRuntime rt`. C# does not have `required`
      constructor parameters (the `required` keyword in C# 11+ only
      applies to properties/fields — Microsoft Learn `required`
      modifier at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/required`).
      The faithful conversion uses positional-or-named parameter
      `Scheduler(GlpRuntime rt)` (no default, no `required` modifier)
      — per `rf-dart-named-required-params-to-csharp-positional-params`
      cached. Emitted: `var scheduler = new Scheduler(rt: rt);`
      preserving the named-argument call form for readability.
    idiom_id: rf-dart-named-argument-to-csharp-named-argument
    research_finding_id: rf-dart-named-argument-to-csharp-named-argument
    nuance: >-
      Cached idiom (precedents: across the lib spec batch).
      Required-named-parameter nuance EXPLICITLY addressed: Dart
      `{required T x}` constructor param has no direct C# equivalent
      at the constructor-parameter site — the C# `required` keyword
      applies only to properties/fields/`init`-setters, not to
      constructor parameters. The faithful C# shape is to make the
      parameter positional or named-without-default (no `?`, no
      `=` default) so the C# compiler requires the caller to pass
      it. Spec default: emit the parameter as named-optional-less
      (i.e., positional `GlpRuntime rt` in the C# constructor
      signature) — the caller `new Scheduler(rt: rt)` still uses
      C# named-argument syntax, identical to the Dart side.

  - construct_key: dart.runtime.member_access_assignment_dotted_property_chains
    source_form: |-
      "final goalId = rt.nextGoalId++;
       final env = CallEnv(args: {});
       rt.setGoalEnv(goalId, env);
       rt.setGoalProgram(goalId, program);
       final fplayPc = program.labels['fplay1/0']!;
       rt.gq.enqueue(GoalRef(goalId, fplayPc));
       final execResult = scheduler.drainWithStatus(maxCycles: 50000);"
    target_decision: >-
      Per the lib specs (`lib/runtime/runtime.dart.md`,
      `lib/runtime/machine_state.dart.md`,
      `lib/runtime/scheduler.dart.md`, `lib/bytecode/runner.dart.md`):
      .
      (1) `rt.nextGoalId++` (post-increment of mutable int field) ⇒
      `rt.NextGoalId++` — Dart and C# share `++` semantics on
      integer fields/properties for the post-increment form
      (Microsoft Learn `++` operator
      `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/arithmetic-operators#increment-operator-`).
      Property-PascalCasing cached. Note: C# `++` on a property
      requires the property be settable; the lib spec records
      `NextGoalId` as a settable property.
      .
      (2) `CallEnv(args: {})` — implicit-new constructor with named
      `args:` arg of empty map literal ⇒ `new CallEnv(args: new
      Dictionary<string, object>())` (REUSE
      `rf-dart-map-literal-typed-to-csharp-dictionary` cached). The
      Dart `{}` empty literal in named-arg-with-static-type-`Map`
      context infers the map type from the parameter signature per
      `lib/runtime/machine_state.dart.md`'s `CallEnv.args` field
      type. The C# emission MUST be explicit about the dictionary
      type because C# has no `{}` empty-map literal.
      .
      (3) `rt.setGoalEnv(goalId, env);` / `rt.setGoalProgram(goalId,
      program);` — instance method calls with positional args ⇒
      PascalCased `rt.SetGoalEnv(goalId, env); rt.SetGoalProgram(
      goalId, program);` per cached camelCase-to-PascalCase idiom.
      .
      (4) `program.labels['fplay1/0']!` — Dart `Map` indexer access
      `[k]` returns nullable `V?`; the `!` asserts non-null. C# has
      TWO shapes: (a) `program.Labels["fplay1/0"]` (which throws
      `KeyNotFoundException` on missing — Microsoft Learn
      `Dictionary<TKey,TValue>.Item` setter docs: "Returns: The
      value associated with the specified key. If the specified key
      is not found, a get operation throws a KeyNotFoundException");
      (b) `program.Labels["fplay1/0"]!` (with the null-forgiving
      `!` if the C# dictionary returned nullable). Dart's `Map[k]`
      returns `V?`; C#'s `Dictionary<K,V>.Item` returns `V`
      (throwing) — NOT directly symmetric. The faithful conversion
      is `program.Labels["fplay1/0"]` (no `!` needed — C# already
      throws on miss). The Dart `!` after the indexer is the
      "I expect this key" assertion; the C# direct indexer is
      observationally equivalent (throws on miss). Codegen drops
      the `!`. NEW idiom registered for this batch (REGISTERS
      `rf-dart-map-bracket-bang-to-csharp-dictionary-bracket`).
      .
      (5) `rt.gq.enqueue(GoalRef(goalId, fplayPc));` — chained property
      access `gq` + instance call `enqueue(...)` ⇒ `rt.Gq.Enqueue(new
      GoalRef(goalId, fplayPc));` — implicit-new + PascalCasing.
      Per `lib/runtime/runtime.dart.md` the field `gq` is the goal
      queue (a `GoalQueue` type per
      `lib/runtime/goal_queue.dart.md`); PascalCases to `Gq` (or
      `GoalQueue` if the lib spec renamed). Spec default: preserve
      the abbreviation `Gq` to match the Dart field name —
      consistent with property-name preservation.
      .
      (6) `scheduler.drainWithStatus(maxCycles: 50000);` — instance
      method call with named arg ⇒ `scheduler.DrainWithStatus(
      maxCycles: 50000);` per cached camelCase + named-arg idioms.
    idiom_id: rf-dart-instance-method-call-camelcase-to-csharp-pascalcase
    research_finding_id: rf-dart-instance-method-call-camelcase-to-csharp-pascalcase
    nuance: >-
      Multiple cached idioms applied in a single multi-line block.
      Map-bracket-bang nuance EXPLICITLY addressed (NEW idiom): Dart's
      `Map<K,V>` indexer returns `V?` (per Dart api.dart.dev
      `https://api.dart.dev/stable/dart-core/Map/operator_get.html`:
      "Returns the value for the given key, or null if key is not
      in the map"); C#'s `Dictionary<K,V>` indexer returns `V`
      (throwing `KeyNotFoundException` on miss). The Dart
      idiom of `map[k]!` (explicit non-null assertion on the lookup)
      is semantically equivalent to C# `dict[k]` (which throws on
      miss). Drop the bang in C# emission. Settable-property nuance
      for `++`: `nextGoalId++` needs `NextGoalId` to be a settable
      property in the converted C#. Per
      `lib/runtime/runtime.dart.md` it IS a mutable field (Dart
      `int nextGoalId = 0;`) ⇒ C# `public int NextGoalId { get;
      set; } = 0;` or `public int NextGoalId = 0;` (field). Either
      supports `++`. ITestOutputHelper-vs-Console nuance NOT
      applicable here (the `print(...)` calls are diagnostic, NOT
      `Assert.True/False/Equal` style — handled separately).

  - construct_key: dart.io.print_in_test_with_string_interpolation
    source_form: |-
      "print('=== Static link fplay1 output (${output.length} lines) ===');
       for (final line in output) {
         print('  $line');
       }
       print('=== Static link fplay1 produced no output ===');
       print('Status: ${execResult.status}');"
    target_decision: >-
      Map Dart `print(...)` IN A TEST BODY to xUnit
      `ITestOutputHelper.WriteLine(...)` (REUSE
      `rf-dart-print-to-xunit-itestoutputhelper-writeline`,
      precedents: utility_instructions_test.dart.md,
      debug_negative.dart.md, circular_term_pointer_test.dart.md,
      linter_*_test.dart.md, etc.). xUnit's
      `Microsoft.Extensions.Logging`-style test output is captured
      and surfaced per-test in the runner output. The capture
      mechanism: the test class's CONSTRUCTOR takes
      `ITestOutputHelper output` and stores it in a private field
      `_output`; each `print(...)` call becomes `_output.WriteLine(
      ...)`. xUnit injects the `ITestOutputHelper` automatically
      (xunit.net "Capturing Output" at
      `https://xunit.net/docs/capturing-output`).
      .
      Dart string interpolation
      `'=== Static link fplay1 output (${output.length} lines) ==='`
      maps to C# interpolated string
      `$"=== Static link fplay1 output ({output.Count} lines) ==="`
      per `rf-dart-string-interpolation-to-csharp-interpolated-string`
      cached. `output.length` ⇒ `output.Count`. `$line` ⇒ `{line}`.
      `${execResult.status}` ⇒ `{execResult.Status}`.
      .
      Emitted (sketch):
      `_output.WriteLine($"=== Static link fplay1 output ({output.Count} lines) ===");
       foreach (var line in output) { _output.WriteLine($"  {line}"); }
       _output.WriteLine("=== Static link fplay1 produced no output ===");
       _output.WriteLine($"Status: {execResult.Status}");`.
    idiom_id: rf-dart-print-to-xunit-itestoutputhelper-writeline
    research_finding_id: rf-dart-print-to-xunit-itestoutputhelper-writeline
    nuance: >-
      Cached idiom (precedents: utility_instructions_test.dart.md,
      linter_*_test.dart.md). Test-context vs lib-context nuance
      EXPLICITLY addressed: Dart `print(...)` in a LIB file would map
      to `Console.WriteLine` (per `rf-dart-print-in-console-exe-to-console-writeline`);
      in a TEST file under xUnit, the canonical translation is
      `ITestOutputHelper.WriteLine` because xUnit's runner CAPTURES
      this output per-test (whereas `Console.WriteLine` output is
      shared across the test process and not per-test-attributed —
      xunit.net "Capturing Output" explicitly warns about this).
      Constructor-injection nuance: the test class's constructor
      must take `ITestOutputHelper output` and store it. For
      `EndToEndCompilationTests` this means modifying the constructor
      signature from default to `public EndToEndCompilationTests(
      ITestOutputHelper output) { _output = output; }`. Interpolation
      nuance: cached.

  - construct_key: dart.package_test.expect_string_contains_substring_with_reason
    source_form: |-
      "expect(outputStr, contains('tagged(alice'), reason: 'Output should contain tagged messages for alice');
       expect(outputStr, contains('connected(bob)'), reason: 'Alice should get connected(bob)');
       expect(outputStr, contains('connected(alice)'), reason: 'Charlie should get connected(alice)');
       expect(bodyFunctors, contains('actors:alice1'), reason: 'actors # alice1 should become actors:alice1');
       expect(bodyFunctors, isNot(contains('#')), reason: 'No RemoteGoal # dispatch should remain');"
    target_decision: >-
      For STRING-contains-string (the `outputStr` calls), map Dart
      `expect(<str>, contains(<substr>))` to xUnit
      `Assert.Contains(<substr>, <str>)` (Microsoft Learn
      `Xunit.Assert.Contains(String, String)` at
      `https://learn.microsoft.com/dotnet/api/xunit.assert.contains`).
      Note this is the STRING overload of `Assert.Contains` — distinct
      from the IEnumerable overload used in
      `dart.package_test.expect_collection_contains_string` above.
      Both Microsoft Learn pages exist; codegen disambiguates by
      operand types. For SET-contains-string-with-reason (the
      `bodyFunctors` calls), use the IEnumerable overload (same as
      the earlier construct).
      .
      Reason preservation: `Assert.Contains` has NO message
      overload (Microsoft Learn confirms only `Assert.Contains(String,
      String)` and `Assert.Contains<T>(T, IEnumerable<T>)` exist).
      So the `reason:` strings cannot become method arguments. Spec
      default: convert to `Assert.True(<str>.Contains(<substr>),
      <reason>);` to preserve the reason. Emitted: `Assert.True(
      outputStr.Contains("tagged(alice"), "Output should contain
      tagged messages for alice");` etc. For the `isNot(contains(
      '#'))` shape: `Assert.False(bodyFunctors.Contains("#"), "No
      RemoteGoal # dispatch should remain");`.
    idiom_id: rf-dart-expect-collection-contains-to-xunit-assert-contains
    research_finding_id: rf-dart-expect-collection-contains-to-xunit-assert-contains
    nuance: >-
      Cached idiom (just-registered above). Reason-preservation
      nuance EXPLICITLY addressed: `Assert.Contains` has no message
      overload — the reason-bearing form switches to
      `Assert.True(<contains-expr>, <reason>)`. Without `reason:` the
      bare `Assert.Contains(...)` is the canonical form. String-
      overload vs IEnumerable-overload nuance: disambiguated by
      operand types — `outputStr` is `string`, `bodyFunctors` is
      `HashSet<string>`.

  - construct_key: dart.string.join_with_newline
    source_form: "final outputStr = output.join('\\n');"
    target_decision: >-
      Map Dart `Iterable<String>.join(<separator>)` to C#
      `string.Join(<separator>, <enumerable>)` per the cached
      `rf-dart-iterable-join-to-csharp-string-join` idiom (precedent:
      binding_pointer_test.dart.md, suspension_pointer_test.dart.md).
      Microsoft Learn `String.Join<T>(String?, IEnumerable<T>)` at
      `https://learn.microsoft.com/dotnet/api/system.string.join`
      "Concatenates the members of a constructed IEnumerable<T>
      collection of type String, using the specified separator
      between each member". Emitted: `var outputStr = string.Join(
      "\n", output);`. Dart `'\n'` ⇒ C# `"\n"` (same escape
      sequence; Dart and C# single-quoted-vs-double-quoted-string
      handling is reconciled by REUSE
      `rf-dart-single-quoted-string-to-csharp-double-quoted-string`
      cached).
    idiom_id: rf-dart-iterable-join-to-csharp-string-join
    research_finding_id: rf-dart-iterable-join-to-csharp-string-join
    nuance: >-
      Cached idiom — reused verbatim. Argument-order nuance EXPLICITLY
      addressed (carry-forward): Dart `coll.join(sep)` puts the
      collection first (instance method) and separator second
      (positional arg). C# `string.Join(sep, coll)` puts the
      separator first (positional arg) and collection second
      (positional arg). The static-vs-instance + argument-order flip
      is the load-bearing translation step. Encoding nuance: `'\n'`
      is the LF character on both sides (no CRLF translation) —
      consistent.

conversion_units:
  - cu-1: "file-scope using directives — `using Xunit;`, `using System.IO;`, `using System.Collections.Generic;`, `using System.Linq;`, `using <RootNs>.Compiler;`, `using <RootNs>.Analysis.TypeChecker;`, `using <RootNs>.Runtime;`, `using <RootNs>.Bytecode;` (four-`using` collapse of nine Dart package imports per the namespace-fold rule + four BCL usings for File/Directory + collections + LINQ)"
  - cu-2: "namespace declaration mirroring the test/compiler path — `namespace <RootNs>.Test.Compiler;`"
  - cu-3: "abstract base class `public abstract class ProjectLinkerTestsBase` hosting (a) static-readonly fields `RootSelfGlp` (FileInfo), `RootSelfPath` (string?), `CssgRoot` (const string `../programs/cssg_modules`), `CssgRootExists` (bool); (b) static constructor running the prelude-load block — `if (RootSelfGlp.Exists) { var source = File.ReadAllText(RootSelfGlp.FullName); PreludeUnitClauses.SetPreludeUnitClauseSource(source); PreludeEnvironment.SetPreludeEnvironmentSource(source); }` followed by `CssgRootExists = Directory.Exists(CssgRoot);` — runs exactly once per AppDomain before first member access (Microsoft Learn `static constructors`)"
  - cu-4: "file-header XML-doc `<summary>` block on `ProjectLinkerTestsBase` (lifted from the Dart `///` library doc comment); Dart `library;` directive dropped"
  - cu-5: "sealed test class `public sealed class ProjectDiscoveryTests : ProjectLinkerTestsBase` with 4 `[Fact]` methods — `DiscoversAllModulesInCssgModules`, `ExcludesSelfGlpFromModules`, `ExcludesBootDirectGlpFromModules`, `ModulesHaveCorrectAncestorScopes`; each `[Fact(DisplayName = \"<original label>\")]`; each method body opens with `Assert.SkipWhen(!CssgRootExists, \"cssg_modules directory not found at ../programs/cssg_modules, skipping tests\");`"
  - cu-6: "sealed test class `public sealed class TypeCheckingTests : ProjectLinkerTestsBase` with 1 `[Fact]` `AllModulesTypeCheckSuccessfully` — body: skip-gate + `var modules = ProjectLinker.DiscoverProject(CssgRoot, rootSelfGlpPath: RootSelfPath); ProjectLinker.TypeCheckProject(modules);` (returnsNormally → bare call)"
  - cu-7: "sealed test class `public sealed class LinkingTests : ProjectLinkerTestsBase` with three private null-forgiving fields (`_modules`, `_linkResult`, `_linked`), instance constructor running the `setUp` body (skip-aware: `if (!CssgRootExists) return;` then the three assignments), and 8 `[Fact]` methods — `ProceduresAreRenamedWithModulePrefix`, `BareProcedureNamesDoNotExistExceptAliases`, `NoNameConflictsBetweenModules`, `CrossModuleCallsAreResolved`, `LocalCallsAreResolved`, `PreludeCallsArePreservedUnprefixed`, `EntryPointAliasesExistForTopModule`, `EntryPointAliasCallsRenamedProcedure`"
  - cu-8: "sealed test class `public sealed class EndToEndCompilationTests : ProjectLinkerTestsBase` with `ITestOutputHelper`-injecting constructor (`private readonly ITestOutputHelper _output;`) and 2 `[Fact]` methods — `LinkedProgramCompilesToBytecode`, `Fplay1ProducesCorrectOutput`; the second test uses `_output.WriteLine(...)` for the diagnostic prints"
  - cu-9: "all `expect(...)` ⇒ `Assert.*` translations following the assertion-shape routing in the construct rows above (Assert.Equal / Assert.True / Assert.False / Assert.NotNull / Assert.NotEmpty / Assert.Contains / Assert.DoesNotContain; reason-preserving forms switch to Assert.True(expr, msg) when the underlying overload lacks a message parameter)"
  - cu-10: "LINQ chains (`Select`/`Where`/`ToHashSet`/`ToList`/`First`/`Any`/`Contains`/`ContainsKey`) emitted at each iterable-projection site per the cached LINQ idioms; `using System.Linq;` mandatory at file head"
  - cu-11: "ITestOutputHelper-based diagnostic prints in EndToEndCompilationTests.Fplay1ProducesCorrectOutput — replaces Dart `print(...)` calls (xUnit per-test output capture)"
  - cu-12: "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven; the pre-`group` init lifts into the shared base class's static constructor (cu-3); the four sibling groups become four sibling sealed classes (cu-5..cu-8)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-library-directive-to-csharp-namespace-elision — `library;` directive ⇒ drop (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: precedent
  `lib/compiler/project_linker.dart.md` (the SUT lib file for
  THIS test) records this idiom for the matching file-header
  comment + `library;` directive shape. Authoritative Dart docs
  at `https://dart.dev/language/libraries#library-directive`
  carry forward verbatim. Microsoft Learn XML-doc tags reference
  cited above for the receiving shape on the C# side.

### rf-dart-package-test-import-to-xunit-using — `package:test` ⇒ xUnit (REUSED)

- **KB reuse**: pinned by `test/smoke_test.dart.md` and reused
  across the batch. Microsoft Learn unit-testing-csharp-with-xunit
  + xunit.net v3 carry forward as authoritative.

### rf-dart-import-dartio-to-csharp-using-systemio — `dart:io` ⇒ `System.IO` (REUSED)

- **KB reuse**: precedent `lib/runtime/runtime.dart.md`,
  reused in `test/compiler/partial_evaluator_test.dart.md` and
  `test/module/cssg_modules_test.dart.md`. Only `File` +
  `Directory` used here → single `using System.IO;`.

### rf-dart-internal-package-import-to-csharp-using — `package:glp_runtime/*` ⇒ collapsed `using` (REUSED, plus `show`-narrowing facet)

- **KB reuse**: precedent `cssg_modules_test.dart.md` (eight Dart
  imports across three subtrees → three `using` lines). THIS file
  has NINE Dart imports across FOUR subtrees (`compiler`,
  `analysis/type_checker`, `runtime`, `bytecode`) → FOUR `using`
  lines. The `show` narrowing on two imports is documented in
  the structured block — codegen drops the narrowing (the
  symbols are already encapsulated on distinct host static
  classes per the lib specs). Authoritative Dart sources:
  `https://dart.dev/language/libraries#specifying-a-library-prefix`
  (the `show`/`hide` narrowing); authoritative C# source:
  Microsoft Learn `using` directive
  `https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/using-directive`.

### rf-dart-package-test-main-omit-in-xunit — Dart `void main()` ⇒ omit + lift body (REUSED + skip-vs-throw nuance)

- **KB reuse for the omission**: pinned across the batch.
- **NEW nuance for THIS file (load-bearing, documented above)**:
  pre-group block ends with a SOFT skip (`print(...) + return;`),
  not a hard throw. The faithful C# translation uses xUnit's
  per-Fact skip mechanism (`Assert.SkipWhen` in xUnit v3 — see
  `https://xunit.net/docs/skipping-tests` — or the `SkippableFact`
  NuGet package in xUnit v2) gated on a `static readonly bool
  CssgRootExists` flag set in the static constructor. Authoritative
  source: xunit.net "Skipping tests" docs cited above; Microsoft
  Learn "Static constructors" at
  `https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/static-constructors`.

### rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext — `File(...).existsSync()`+`.readAsStringSync()` ⇒ `File.Exists`+`File.ReadAllText` (REUSED, with `FileInfo` instance facet)

- **KB reuse**: cached on
  `test/compiler/partial_evaluator_test.dart.md` and
  `test/module/cssg_modules_test.dart.md`. Authoritative Dart +
  Microsoft Learn pages carry forward.
- **NEW facet here**: the `rootSelfGlp` Dart `final` is referenced
  TWICE (once for existence+read, once for `.absolute.path`). The
  C# idiomatic match is `System.IO.FileInfo` — instance class
  with `.Exists` property AND `.FullName` property (Microsoft
  Learn `System.IO.FileInfo` at
  `https://learn.microsoft.com/dotnet/api/system.io.fileinfo`,
  `.Exists` at
  `https://learn.microsoft.com/dotnet/api/system.io.fileinfo.exists`,
  `.FullName` at
  `https://learn.microsoft.com/dotnet/api/system.io.fileinfo.fullname`).
  This is a HYBRID shape vs the static-only emissions in the
  precedents. Codegen MUST emit `FileInfo` for this file.

### rf-dart-directory-existssync-to-system-io-directory-exists — `Directory(p).existsSync()` ⇒ `Directory.Exists(p)` (REUSED, with skip-vs-throw facet)

- **KB reuse**: cached on `lib/runtime/module_hierarchy.dart.md`,
  `lib/compiler/project_linker.dart.md`,
  `test/module/cssg_modules_test.dart.md`. Microsoft Learn
  `System.IO.Directory.Exists` at
  `https://learn.microsoft.com/dotnet/api/system.io.directory.exists`
  carries forward.
- **NEW facet**: skip-vs-throw (per the `dart.package_test.main_entrypoint`
  nuance above). The directory miss is a SOFT SKIP here, not a hard
  fail — gated via `Assert.SkipWhen`.

### csharp-static-class-no-toplevel-members — top-level setters ⇒ qualified static calls (REUSED)

- **KB reuse**: cached on the lib specs and on every test spec
  that calls the prelude setters. Twin-setter pattern (both
  `setPreludeUnitClauseSource` and `setPreludeEnvironmentSource`)
  matches `cssg_modules_test.dart.md` exactly.

### rf-dart-final-local-to-csharp-var — `final` local lifted to `static readonly` field (REUSED, with `.absolute.path` ⇒ `.FullName` facet)

- **KB reuse**: cached. Variant `static readonly` (instead of `var`)
  is used here because the local lifts onto the shared base class.
  Microsoft Learn `readonly` modifier at
  `https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/readonly`:
  "A readonly field can be assigned multiple times in the field
  declaration and in any constructor of the class" — matches
  Dart `final` single-assignment semantics.
- **NEW facet**: `.absolute.path` ⇒ `.FullName` collapse on the
  `FileInfo` shape. Authoritative both sides.

### rf-dart-package-test-group-to-xunit-class — `group(...)` ⇒ test class (REUSED, four-class topology)

- **KB reuse**: cached across the batch. Four sibling top-level
  groups → four sibling classes. Shared abstract base class
  `ProjectLinkerTestsBase` lifts the cross-class shared state
  (prelude load + cssgRoot flag) — same shape as
  `cssg_modules_test.dart.md` (single class) and
  `module_parser_test.dart.md` (six classes), parameterised on the
  group count.

### rf-dart-late-field-to-csharp-nullforgiving-field — `late T x;` + `setUp` ⇒ field + constructor (REUSED, three-field facet)

- **KB reuse**: cached on
  `test/multiagent/boot_loader_test.dart.md` and
  `test/compiler/partial_evaluator_test.dart.md`. THREE fields here
  (vs ONE in the precedents) — order of assignment in the
  constructor matters; documented above.

### rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset / rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist / rf-dart-iterable-join-to-csharp-string-join — LINQ chains (REUSED)

- **KB reuse**: cached on `test/lint/linter_ok_test.dart.md`,
  `test/module/cssg_modules_test.dart.md`, `test/heap/*`, etc.
  Microsoft Learn LINQ `Enumerable.Select`, `.Where`, `.ToHashSet`,
  `.ToList` (`https://learn.microsoft.com/dotnet/api/system.linq.enumerable`)
  carries forward.

### rf-dart-iterable-firstwhere-to-csharp-linq-first — `firstWhere(p)` ⇒ `First(p)` (NEW idiom, AUTHORITATIVE)

- **Deep analysis**: Dart `Iterable.firstWhere(test)` per
  `https://api.dart.dev/stable/dart-core/Iterable/firstWhere.html`:
  "If no such element is found, the result of invoking the orElse
  function is returned. If orElse is omitted, it defaults to
  throwing a StateError." This file omits `orElse` → "throw on no
  match".
- **Authoritative .NET**: Microsoft Learn `Enumerable.First<TSource>(
  IEnumerable<TSource>, Func<TSource, bool>)` at
  `https://learn.microsoft.com/dotnet/api/system.linq.enumerable.first`:
  "Throws: InvalidOperationException — No element satisfies the
  condition in predicate."
- **Conclusion**: emit `First(predicate)` — semantically equivalent
  (both throw on no match). `FirstOrDefault(predicate)` would
  silently return `null` (or `default(T)`) — semantically WRONG.
  NEW idiom registered for batch reuse.

### rf-dart-iterable-any-with-expect-reason-join-to-xunit-assert-true-linq — `expect(coll.any(p), isFalse, reason: '...')` ⇒ `Assert.False(coll.Any(p), reason)` (REUSED)

- **KB reuse**: cached on `test/lint/linter_body_precommit_test.dart.md`.
  Microsoft Learn xUnit `Assert.False(Boolean, String)` confirms
  the message-overload signature exists.

### rf-dart-expect-collection-contains-to-xunit-assert-contains — `expect(coll, contains(v))` ⇒ `Assert.Contains(v, coll)` (NEW idiom, AUTHORITATIVE)

- **Deep analysis**: Dart `package:matcher` `contains` at
  `https://pub.dev/documentation/matcher/latest/matcher/contains.html`:
  "An object that matches if the expected value is contained in
  the actual value (a List, Set, Map, or String)."
- **Authoritative .NET**: Microsoft Learn xUnit
  `Xunit.Assert.Contains<T>(T, IEnumerable<T>)` at
  `https://learn.microsoft.com/dotnet/api/xunit.assert.contains`:
  "Verifies that a collection contains a given object." Also
  `Xunit.Assert.Contains(String, String)`: "Verifies that a string
  contains a given sub-string." Codegen disambiguates by operand
  types. Symmetric `Assert.DoesNotContain` for the `isNot(
  contains(...))` shape (negation form).
- **Conclusion**: emit `Assert.Contains(v, coll)` for the
  collection-overload (Set/List) and `Assert.Contains(substr, str)`
  for the string-overload. Reason-bearing sites (Assert.Contains
  has no message overload — verified on Microsoft Learn) switch to
  `Assert.True(<contains-expr>, reason)`. NEW idiom registered.

### rf-dart-expect-equals-to-xunit-assert-equal-argorder / rf-dart-list-length-to-csharp-list-count — equals + length-to-Count (REUSED)

- **KB reuse**: cached across the batch. EXPECTED-FIRST argument
  order per Microsoft Learn xUnit `Assert.Equal<T>(T, T)`. Length
  ⇒ `.Count` on `IList`/`ICollection`/`HashSet` per Microsoft Learn
  `System.Collections.Generic.List<T>.Count` and similar.

### rf-dart-expect-isNotEmpty-to-xunit-assert-notempty — `isNotEmpty` ⇒ `Assert.NotEmpty` (REUSED, with reason-preservation facet)

- **KB reuse**: cached. xUnit `Assert.NotEmpty(IEnumerable)` has
  NO message overload — Microsoft Learn confirms only one signature.
  Reason-bearing sites switch to `Assert.True(coll.Any(), msg)` to
  preserve the reason.

### rf-dart-expect-isNotNull-to-xunit-assert-notnull — `isNotNull` ⇒ `Assert.NotNull` (REUSED, with reason-preservation facet)

- **KB reuse**: cached. Same reason-preservation switching as
  `isNotEmpty` — `Assert.NotNull(object)` has no message overload.

### rf-dart-expect-isTrue-to-xunit-assert-true / rf-dart-expect-isFalse-to-xunit-assert-false — boolean asserts (REUSED)

- **KB reuse**: cached. Both have message overloads per Microsoft
  Learn — message-bearing sites preserve the reason as the 2nd arg.

### rf-dart-expect-returns-normally-to-xunit-bare-call — bare call (REUSED)

- **KB reuse**: cached on
  `test/compiler/partial_evaluator_test.dart.md`. Same rationale
  (xUnit "no DoesNotThrow" position) carries forward verbatim.

### rf-dart-for-in-to-csharp-foreach / rf-dart-set-literal-typed-to-csharp-hashset-initializer — control-flow + set literal (REUSED)

- **KB reuse**: cached. Null-check-then-bang elision relies on C#
  compiler flow analysis (Microsoft Learn nullable-reference-types).

### rf-dart-implicit-new-and-camelcase-to-csharp-explicit-new-pascalcase — call-shape (REUSED across many sites)

- **KB reuse**: cached across the lib spec batch. Three facets
  (implicit-new ⇒ explicit-new, camelCase ⇒ PascalCase, named arg
  ⇒ named arg) apply to every constructor + method-call site here.

### rf-dart-arrow-lambda-to-csharp-lambda + Action<T>?-typed callback field (REUSED, with `void Function(String)?` ⇒ `Action<string>?` facet)

- **KB reuse**: cached. `outputCallback` field is the load-bearing
  use; `Action<string>?` per Microsoft Learn `System.Action<T>` at
  `https://learn.microsoft.com/dotnet/api/system.action-1`.

### rf-dart-named-argument-to-csharp-named-argument / rf-dart-named-required-params-to-csharp-positional-params — named/required arg conversion (REUSED)

- **KB reuse**: cached. C# `required` keyword does NOT apply to
  constructor parameters per Microsoft Learn `required` modifier
  reference; the faithful C# shape uses positional-or-named
  parameter (no default value) so the compiler requires the caller
  to pass it.

### rf-dart-list-indexer-to-csharp-list-indexer / rf-dart-map-bracket-bang-to-csharp-dictionary-bracket — indexers (REUSED + NEW)

- **KB reuse** for List indexer; **NEW** for Map-bracket-bang ⇒
  Dictionary-bracket. Dart `Map[k]` returns `V?` (api.dart.dev
  `https://api.dart.dev/stable/dart-core/Map/operator_get.html`);
  C# `Dictionary<K,V>[k]` returns `V` and throws
  `KeyNotFoundException` on miss (Microsoft Learn
  `Dictionary<TKey,TValue>.Item` at
  `https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.item`).
  Dart `map[k]!` (explicit non-null assertion) is observationally
  equivalent to C# `dict[k]` (throws on miss). NEW idiom registered.

### rf-dart-print-to-xunit-itestoutputhelper-writeline — `print(...)` in test body (REUSED)

- **KB reuse**: cached across `test/bytecode/utility_instructions_test.dart.md`,
  `test/heap/circular_term_pointer_test.dart.md`,
  `test/lint/*_test.dart.md`. xunit.net "Capturing Output" at
  `https://xunit.net/docs/capturing-output`: "When you want to
  capture output written to the standard output / standard error
  streams, you need to take a different approach... inject the
  ITestOutputHelper interface into your test class constructor."
  Authoritative source.

### rf-dart-string-interpolation-to-csharp-interpolated-string + rf-dart-single-quoted-string-to-csharp-double-quoted-string — string handling (REUSED)

- **KB reuse**: cached across the batch. Dart `$x` / `${expr}` ⇒
  C# `{x}` / `{expr}`. Microsoft Learn "Interpolated string
  expressions" at
  `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/interpolated`.

## Notes

- 13 `test()` cases total, all synchronous (no `async` test
  bodies, no `Future`/`Stream`/`async Task` surface — the
  `[Fact]` methods all return `void`). One file-level driver
  (`fplay1 produces correct output`) runs the bytecode runtime
  end-to-end, but synchronously via `scheduler.drainWithStatus(
  maxCycles: 50000)`.
- Four sibling top-level groups → four sibling classes; shared
  base class hosts the prelude-load static-ctor and the
  `CssgRootExists` skip flag. The four-class topology contrasts
  with the single-class topologies of `partial_evaluator_test.dart.md`
  and `cssg_modules_test.dart.md`; it matches the multi-class
  topology of `module_parser_test.dart.md` (precedent).
- The pre-group block uses `print(...) + return;` for missing
  cssg_modules (a SOFT SKIP), NOT `throw StateError` (a HARD
  FAIL); the faithful C# translation is `Assert.SkipWhen` gated
  on a static-readonly bool flag — not `throw new
  InvalidOperationException` (which would be wrong: the Dart
  semantics is "silently skip all tests", not "fail the test
  class init").
- The `Linking` group uses `setUp(...)` with THREE `late`
  fields — three null-forgiving fields + constructor assignment
  in the correct dependency order (`_modules` before
  `_linkResult` before `_linked`). xUnit per-Fact fresh-instance
  semantics rebuilds all three before every test, matching Dart
  `setUp`'s contract.
- The `End-to-end compilation` group's `fplay1 produces correct
  output` test exercises `GlpRuntime`, `Scheduler`,
  `BytecodeRunner`, `GoalRef`, `CallEnv` — all of which live in
  the converted runtime per the respective lib specs. The test
  also uses `print(...)` for diagnostic output, which maps to
  `ITestOutputHelper.WriteLine` per the cached idiom.
- Two NEW idioms registered for batch reuse:
  `rf-dart-iterable-firstwhere-to-csharp-linq-first` (the
  throw-on-no-match LINQ `First` mapping, authoritative both
  sides) and `rf-dart-expect-collection-contains-to-xunit-assert-contains`
  (the collection-overload `Assert.Contains` mapping, with
  symmetric `Assert.DoesNotContain` for negation and reason-
  preservation switching to `Assert.True(<contains-expr>, msg)`
  when the underlying overload lacks a message parameter).
- One NEW idiom registered for the indexer-bang collapse:
  `rf-dart-map-bracket-bang-to-csharp-dictionary-bracket` —
  Dart `map[k]!` ⇒ C# `dict[k]` (the throwing behaviour of the
  C# Dictionary indexer subsumes the Dart non-null assertion).
- The two `show <symbol>` clauses on the Dart imports (one each
  on `partial_evaluator` and `type_environment_builder`) are
  source-shape narrowing that has NO observable effect on the
  C# emission — the symbols are already encapsulated on
  distinct host static classes per the lib specs, so the test
  site references them via qualified names regardless. Codegen
  drops the narrowing.
- The Dart `library;` directive (file-header doc-comment anchor)
  is dropped on the C# side; the doc comment moves to the
  shared base class as an XML-doc `<summary>` block.
- The relative paths `'../programs/self.glp'` and
  `'../programs/cssg_modules'` are preserved verbatim — both
  Dart and .NET resolve relative paths against the process CWD
  at call time. The static-ctor skip-flag design ensures missing
  paths surface as test SKIPS (not failures), preserving Dart's
  soft-skip semantics.
- Zero escalations: every construct is authoritative-supported
  on both sides. Twenty-one construct rows REUSE idioms /
  findings recorded by prior batches (the lib + test batch).
  Three construct rows register NEW idioms — all are
  authoritative on both sides via the cited api.dart.dev and
  Microsoft Learn / xunit.net references.
