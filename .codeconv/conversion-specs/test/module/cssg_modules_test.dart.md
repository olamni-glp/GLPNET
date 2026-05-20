> Conversion-spec artifact for test/module/cssg_modules_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/module/cssg_modules_test.dart
source_sha256: fece36ea3f927a1077c5c1a176b2281d71cc9049947063c871d6dbc53d423a05
target_code_unit: test/module/CssgModulesTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and
      replace it at the file head with `using Xunit;`. Reuse the
      batch-wide xUnit pinned by smoke_test.dart.md / boot_loader_test.dart.md
      / module_parser_test.dart.md (KB hit ⇒ REUSE verbatim per FR-012/SC-007,
      no re-research, no re-derivation). Codegen MUST also add `using
      System.IO;` (used by the `File(...)`/`Directory(...)` translations
      below — see `dart.dart_io.import_directive` and
      `dart.platform.file_existsSync_readAsStringSync`) and `using
      System.Collections.Generic;` (used by the `Dictionary<string, TypeDef>`
      / `Dictionary<string, ProcDecl>` locals in `buildAncestorScope` — see
      `dart.collections.typed_empty_map_literal_with_indexer_write`).
      Project to a namespace mirroring the Dart `test/module` directory
      (e.g. `<RootNs>.Test.Module`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Cached idiom — reused verbatim. Lifecycle nuance (carry-forward):
      xUnit creates a FRESH instance of the test class per `[Fact]` (xunit.net
      "Shared Context between Tests" — https://xunit.net/docs/shared-context).
      For this file the only file-level shared state is the `cssgRoot`
      string constant and the prelude-source side effect (lifted into a
      static initialiser — see `dart.package_test.main_entrypoint` below);
      both are process-scoped, not per-instance, so xUnit's fresh-instance
      model is observably equivalent to Dart `main`'s once-per-process
      shape.
  - construct_key: dart.dart_io.import_directive
    source_form: "import 'dart:io';"
    target_decision: >-
      Drop the Dart `import 'dart:io';` directive and replace it at the
      file level with `using System.IO;`. REUSE the
      `rf-dart-import-dartio-to-csharp-using-systemio` idiom recorded in
      `lib/runtime/runtime.dart.md` (and reused in
      `test/compiler/partial_evaluator_test.dart.md`). The `dart:io`
      surface used in THIS file is `File` (constructor +
      `.existsSync()`/`.readAsStringSync()`) and `Directory` (constructor
      + `.existsSync()`) — both covered by `System.IO.File` and
      `System.IO.Directory`. No `Platform`, `Process`, `Socket`, `Stdin`,
      or `Stdout` references, so a single `using System.IO;` suffices.
    idiom_id: rf-dart-import-dartio-to-csharp-using-systemio
    research_finding_id: rf-dart-import-dartio-to-csharp-using-systemio
    nuance: >-
      Cached idiom — reused verbatim (precedent:
      partial_evaluator_test.dart.md, runtime.dart.md). Library-vs-namespace
      nuance (carry-forward): `dart:io` is one Dart-core library; .NET
      splits the same surface across several `System.*` namespaces. For
      THIS file only `System.IO` is required because only `File` and
      `Directory` are used.
  - construct_key: dart.package_under_test.import_directive
    source_form: |-
      "import 'package:glp_runtime/compiler/lexer.dart';
       import 'package:glp_runtime/compiler/parser.dart';
       import 'package:glp_runtime/compiler/ast.dart' as ast;
       import 'package:glp_runtime/compiler/partial_evaluator.dart';
       import 'package:glp_runtime/analysis/type_checker/type_ast.dart';
       import 'package:glp_runtime/analysis/type_checker/type_environment_builder.dart';
       import 'package:glp_runtime/analysis/type_checker/type_checker.dart';
       import 'package:glp_runtime/runtime/module_hierarchy.dart';"
    target_decision: >-
      Each `package:glp_runtime/<sub>/<file>.dart` import maps to a C#
      `using` directive that names the namespace produced by converting
      the corresponding SUT subtree under the langpair convention
      (`package:glp_runtime/<a>/<b>/file.dart` ⇒ namespace `<RootNs>.<A>.<B>`,
      file name dropped — Dart libraries are file-grained, C# namespaces
      are directory-grained, see module_syntax_v2_test.dart.md). Per the
      lib specs `lib/compiler/lexer.dart.md`, `lib/compiler/parser.dart.md`,
      `lib/compiler/ast.dart.md`, `lib/compiler/partial_evaluator.dart.md`
      the four `lib/compiler/*` imports collapse into ONE `using
      <RootNs>.Compiler;`. Per `lib/analysis/type_checker/type_ast.dart.md`,
      `type_environment_builder.dart.md`, `type_checker.dart.md` the three
      `lib/analysis/type_checker/*` imports collapse into ONE `using
      <RootNs>.Analysis.TypeChecker;`. The single `lib/runtime/module_hierarchy.dart`
      import (per `lib/runtime/module_hierarchy.dart.md`) maps to `using
      <RootNs>.Runtime;`. Net result: THREE `using` lines for the eight
      Dart imports.
      .
      The Dart prefix `as ast` (used only on `ast.Module`, `ast.Program`)
      is an alias-style namespace narrowing. C# has the equivalent
      `using ast = <RootNs>.Compiler;` directive (Microsoft Learn
      "Using namespace directives" /dotnet/csharp/language-reference/keywords/using-directive),
      which permits the same `ast.Module` / `ast.Program` qualification in
      the body — BUT type-name disambiguation in C# is normally done at the
      type site (`Compiler.Module`, `Compiler.Program`). The Dart `as ast`
      qualifier exists because `Module` would otherwise collide with
      `type_environment_builder.dart`'s symbols; if no such collision
      survives the C# namespace fold (it does NOT — `Module`/`Program` are
      in `Compiler`, `TypeEnvironment`/`TypeDef`/`ProcDecl` are in
      `Analysis.TypeChecker`), codegen MAY drop the alias and emit bare
      `Module`/`Program` references. Spec default: PRESERVE the alias via
      `using ast = <RootNs>.Compiler;` for source-shape fidelity; codegen
      MAY suppress it under a future "alias-only-when-needed" pass.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cached idiom (precedents: boot_loader_test.dart.md,
      partial_evaluator_test.dart.md, module_parser_test.dart.md,
      module_syntax_v2_test.dart.md). Granularity-mismatch nuance
      (carry-forward, load-bearing): Dart imports are file-grained; C#
      `using` is namespace-grained — same-directory Dart files collapse
      into a single C# `using` when the langpair convention pools
      siblings into one namespace. ALIAS nuance EXPLICITLY addressed (new
      facet for this file): Dart `import '...' as X` maps to C# `using X =
      <Namespace>;`. Symbol visibility: every imported symbol used here
      (`Lexer`, `Parser`, `ast.Module`, `ast.Program`, `PartialEvaluator`,
      `TypeDef`, `ProcDecl`, `TypeEnvironment`, `TypeCheckResult`,
      `buildPreludeEnvironment`, `buildTypeEnvironment`, `checkModule`,
      `discoverSelfChain`, `setPreludeUnitClauseSource`,
      `setPreludeEnvironmentSource`) is library-public on the Dart side
      (no leading underscore) and maps to `public` C# accessibility — no
      relaxation required.
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
        if (!Directory(cssgRoot).existsSync()) {
          throw StateError('cssg_modules directory not found at $cssgRoot');
        }
        ast.Module parseFile(String path) { ... }
        TypeEnvironment buildAncestorScope(String targetFile) { ... }
        TypeCheckResult typeCheckFile(String path) { ... }
        group('cssg_modules end-to-end', () { test(...); ... });
      }"
    target_decision: >-
      Eliminate the Dart `void main()` per-file entrypoint entirely
      (xUnit discovers `[Fact]` methods by reflection). The body
      decomposes into FOUR target shapes on the single lifted test class
      `CssgModulesEndToEndTests` (named from the single inner `group`
      label — see `dart.package_test.group_block` below):
      .
      (1) The PRE-GROUP file-IO block (the `File('../programs/self.glp')`
      existence check + `setPreludeUnitClauseSource` + `setPreludeEnvironmentSource`
      + the `Directory('../programs/cssg_modules')` existence check +
      `throw StateError(...)`) lifts into a `static` constructor on the
      test class — see `dart.package_test.main_entrypoint` precedent in
      `partial_evaluator_test.dart.md`. The static-ctor runs exactly once
      per type per AppDomain on first member access (Microsoft Learn
      "static constructors": /dotnet/csharp/programming-guide/classes-and-structs/static-constructors),
      mirroring Dart `main`'s once-per-process semantics.
      .
      (2) The `cssgRoot` Dart `final` local lifts into a `private const
      string CssgRoot = "../programs/cssg_modules";` field on the test
      class so all `[Fact]` methods and the three helper methods see it.
      .
      (3) The three Dart local functions `parseFile`, `buildAncestorScope`,
      `typeCheckFile` lift into `private` instance helper methods on the
      test class — see `dart.local_function.named_inner_helper` below.
      Static would also work (none capture instance state); private-instance
      is the test-helper idiom default established in
      partial_evaluator_test.dart.md.
      .
      (4) The single `group(...)` call lifts to the (sole) test class
      itself; each inner `test('label', () { ... })` becomes one `[Fact(
      DisplayName = "<label>")]` method on the class — see
      `dart.package_test.test_call_simple` below.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Cached idiom (precedents: mad_error_handling_test.dart.md,
      boot_loader_test.dart.md, partial_evaluator_test.dart.md,
      module_parser_test.dart.md). Pre-group-init nuance (carry-forward
      from partial_evaluator_test.dart.md, EXPLICITLY addressed): this
      file has a non-trivial pre-`group` block — TWO prelude setters
      (NOT just `setPreludeUnitClauseSource` but ALSO
      `setPreludeEnvironmentSource`) plus a `Directory.existsSync()`
      precondition guard that throws `StateError` on failure. All three
      side effects belong in a single `static` constructor that runs
      before any `[Fact]`. The `throw StateError(...)` becomes `throw new
      InvalidOperationException(...)` (see `dart.error.stateerror_throw`
      below) — a fail-fast in the static constructor manifests as a
      `TypeInitializationException` wrapping the inner
      `InvalidOperationException` (Microsoft Learn "Static Constructors":
      a failure throws `TypeInitializationException` on subsequent type
      access — observationally equivalent to Dart `main` aborting before
      any `test` registers). Local-function lift nuance (carry-forward):
      the three Dart local functions inside `main` are at the BODY of
      `main`, so they lift to test-class instance methods, NOT free
      functions (C# forbids free functions outside a containing type).
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
      idiom (precedent: partial_evaluator_test.dart.md). Emitted form
      inside the static constructor:
      `if (File.Exists("../programs/self.glp"))
       {
         var source = File.ReadAllText("../programs/self.glp");
         PreludeUnitClauses.SetPreludeUnitClauseSource(source);
         PreludeEnvironment.SetPreludeEnvironmentSource(source);
       }`. The relative path `"../programs/self.glp"` is preserved
      verbatim (both Dart and .NET resolve relative paths against the
      current working directory at call time). The Dart shape uses ONE
      `File(...)` instance and TWO method calls on it; the .NET shape uses
      TWO static calls each taking the same path string — semantically
      equivalent (one Path-existence check + one full text read).
      The Dart pattern stores the read into a LOCAL `source` and reuses
      it for both setters; the C# emission MUST preserve that local
      (single `File.ReadAllText` call, two `Set...Source(source)` calls)
      to maintain the byte-identical-source invariant across the two
      setters.
    idiom_id: rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext
    research_finding_id: rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext
    nuance: >-
      Cached idiom (precedent: partial_evaluator_test.dart.md). Three
      cached nuances (instance-vs-static; sync-vs-async; encoding) carry
      forward verbatim. NEW facet EXPLICITLY addressed here: this file
      reads the source ONCE and applies it via TWO setters (`setPreludeUnitClauseSource`
      AND `setPreludeEnvironmentSource`) — the read MUST not be duplicated
      across the two setter calls (would diverge if the file changed
      between reads). The Dart code already does this correctly (one
      `readAsStringSync()` into `source`, two setter calls); the C#
      emission MUST mirror it (one `File.ReadAllText` into `var source`,
      two `Set...Source(source)` calls).
  - construct_key: dart.platform.directory_existsSync_with_state_error_guard
    source_form: >-
      "final cssgRoot = '../programs/cssg_modules';
      if (!Directory(cssgRoot).existsSync()) {
        throw StateError('cssg_modules directory not found at $cssgRoot');
      }"
    target_decision: >-
      Map Dart `Directory('<path>').existsSync()` to C# static
      `System.IO.Directory.Exists(<path>)` per the lib-spec idiom
      `rf-dart-directory-existssync-to-system-io-directory-exists`
      established in `lib/runtime/module_hierarchy.dart.md` and
      `lib/compiler/project_linker.dart.md`. The Dart `!` negation maps to
      C# `!` (identical token). The `throw StateError(...)` maps to `throw
      new System.InvalidOperationException(...)` per the cached
      `rf-dart-stateerror-throw-to-csharp-invalidoperationexception`
      idiom (precedent: mad_transactions_test.dart.md prose,
      circular_term_test.dart.md prose). The string-interpolated message
      `'cssg_modules directory not found at $cssgRoot'` maps to a C#
      interpolated string `$"cssg_modules directory not found at
      {CssgRoot}"` (see `dart.string.interpolation_dollar_local`). Emitted
      form inside the static constructor:
      `if (!Directory.Exists(CssgRoot))
       {
         throw new InvalidOperationException(
           $"cssg_modules directory not found at {CssgRoot}");
       }`.
    idiom_id: rf-dart-directory-existssync-to-system-io-directory-exists
    research_finding_id: rf-dart-directory-existssync-to-system-io-directory-exists
    nuance: >-
      Cached idiom (precedents: module_hierarchy.dart.md `dart.platform.
      directory_existsSync_*`, project_linker.dart.md `Directory.Exists`).
      Instance-vs-static nuance: Dart `Directory(p)` is an instance
      constructor with `.existsSync()` method; .NET `Directory.Exists(p)`
      is a static method on the `Directory` class taking the path string
      directly — semantically equivalent (path-string existence check,
      no instance state). Fail-fast in static-ctor nuance EXPLICITLY
      addressed: a throw inside a C# static constructor is wrapped in
      `TypeInitializationException` on subsequent type access (Microsoft
      Learn "Static Constructors": /dotnet/csharp/programming-guide/classes-and-structs/static-constructors).
      The original `InvalidOperationException` is preserved as
      `InnerException` — diagnostically equivalent to Dart `StateError`
      escaping from `main` before `test()` registration.
  - construct_key: dart.error.stateerror_throw
    source_form: "throw StateError('cssg_modules directory not found at $cssgRoot');"
    target_decision: >-
      Map Dart `throw StateError(<msg>)` (Dart core `StateError` class —
      thrown when an operation is invoked at an inappropriate time) to
      C# `throw new System.InvalidOperationException(<msg>);`. The
      canonical .NET counterpart per Microsoft Learn `System.InvalidOperationException`
      (/dotnet/api/system.invalidoperationexception): "The exception
      that is thrown when a method call is invalid for the object's
      current state" — direct semantic match to Dart `StateError` (Dart
      api.dart.dev `StateError`: "thrown when an operation cannot be
      performed on the object's current state"). Cached across the
      multiagent test specs (precedents: mad_transactions_test.dart.md's
      `rf-dart-expect-throwsStateError-to-xunit-assert-throws-ioe` which
      pins the THROW-side mapping symmetrically with the EXPECT-side;
      circular_term_test.dart.md prose). The interpolated message MUST
      be converted via the string-interpolation idiom (see
      `dart.string.interpolation_dollar_local`).
    idiom_id: rf-dart-stateerror-throw-to-csharp-invalidoperationexception
    research_finding_id: rf-dart-stateerror-throw-to-csharp-invalidoperationexception
    nuance: >-
      Cached idiom (precedents: mad_transactions_test.dart.md,
      circular_term_test.dart.md prose). Exception-hierarchy nuance
      (EXPLICITLY addressed): Dart `StateError extends Error` (programmer-
      error subclass, not catchable in a "normal" try-catch idiom); C#
      `InvalidOperationException : SystemException : Exception` (a
      normal-catchable Exception). The faithful translation accepts
      this asymmetry: in Dart, `StateError` signals a USAGE error and is
      typically not caught; in C# the same intent is `InvalidOperationException`
      which IS within the catch-all `Exception` hierarchy. For THIS
      file the throw escapes the static constructor (terminating type
      init) — it is never caught — so the hierarchy difference is not
      observable. Constructor-arity nuance: Dart `StateError(msg)`
      single-arg matches C# `new InvalidOperationException(string)`
      single-arg overload exactly.
  - construct_key: dart.module.global_setter_function
    source_form: |-
      "setPreludeUnitClauseSource(source);
       setPreludeEnvironmentSource(source);"
    target_decision: >-
      Map each free top-level setter call to a `public static void`
      method on its host class (C# forbids top-level free functions per
      the `csharp-static-class-no-toplevel-members` cached idiom;
      precedent partial_evaluator_test.dart.md). Per the lib specs:
      `setPreludeUnitClauseSource` is hosted by `internal static class
      PreludeUnitClauses` (per lib/compiler/partial_evaluator.dart.md
      `dart.toplevel.mutable_global_nullable_string_init_to_null_and_setter_function`);
      `setPreludeEnvironmentSource` is hosted by the parallel
      `internal static class PreludeEnvironment` (per
      lib/analysis/type_checker/type_environment_builder.dart.md —
      same toplevel-mutable-global-with-setter shape, parallel naming).
      Emitted form (inside static ctor):
      `PreludeUnitClauses.SetPreludeUnitClauseSource(source);
       PreludeEnvironment.SetPreludeEnvironmentSource(source);`.
      Method-name PascalCase: `setPrelude...Source` → `SetPrelude...Source`.
      Spec default: emit the QUALIFIED call (no `using static`
      directive), per the cross-file dependency convention from
      partial_evaluator_test.dart.md.
    idiom_id: csharp-static-class-no-toplevel-members
    research_finding_id: csharp-static-class-no-toplevel-members
    nuance: >-
      Cached idiom (precedent: partial_evaluator_test.dart.md,
      glp_runtime_test.dart.md). Twin-setter nuance EXPLICITLY addressed
      (new facet — both setters present in THIS file vs only one in
      partial_evaluator_test.dart.md): the two setters
      `setPreludeUnitClauseSource` / `setPreludeEnvironmentSource` are
      parallel — both top-level mutable-global setters on the Dart side,
      hosted by DIFFERENT host classes on the C# side (because they
      live in different lib files: `lib/compiler/partial_evaluator.dart`
      vs `lib/analysis/type_checker/type_environment_builder.dart`).
      Both calls receive the SAME `source` argument from the single
      `File.ReadAllText` above. Codegen MUST NOT merge the two setters
      into one — they write to distinct static fields on distinct host
      classes per the respective lib specs.
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('cssg_modules end-to-end', () {
        test('self.glp parses and type-checks', () { ... });
        test('agent.glp type-checks with PE and ancestor scope', () { ... });
        test('ui/mediator.glp type-checks with PE and ancestor scope', () { ... });
        test('ui/actors.glp type-checks with PE and ancestor scope', () { ... });
        test('boot.glp parses (untyped orchestration)', () { ... });
      });"
    target_decision: >-
      The single top-level `group('cssg_modules end-to-end', () { ... })`
      maps to ONE PascalCase xUnit test class `CssgModulesEndToEndTests`
      in the file. Per the cached `rf-dart-package-test-group-to-xunit-class`
      idiom (precedents: mad_error_handling_test.dart.md, boot_loader_test.dart.md,
      module_parser_test.dart.md). The label `'cssg_modules end-to-end'`
      strips non-identifier characters / collapses hyphens to camel-join:
      `cssg_modules end-to-end` → `CssgModulesEndToEnd` → suffix `Tests`
      → `CssgModulesEndToEndTests`. The original label is preserved
      verbatim via `[Fact(DisplayName = "<label>")]` on every method so
      reporter output keeps the Dart sentence form (carry-forward from
      module_parser_test.dart.md).
      .
      ONLY ONE GROUP exists in this file (no sibling groups, no nested
      groups) — so the FLATTEN-vs-one-class-per-group decision is moot:
      one group → one class. The three helper local functions
      (`parseFile`, `buildAncestorScope`, `typeCheckFile`) are declared
      OUTSIDE the `group` body in Dart `main()` scope; they lift to
      private instance methods on the SAME class (`CssgModulesEndToEndTests`)
      so the test methods can call them without cross-class plumbing —
      see `dart.local_function.named_inner_helper` below.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Cached idiom (precedents: mad_error_handling_test.dart.md,
      boot_loader_test.dart.md, module_parser_test.dart.md prose).
      Topology nuance EXPLICITLY addressed (CONTRAST with sibling
      precedents): module_parser_test.dart.md has SIX SIBLING top-level
      groups (six classes); this file has ONE top-level group (one
      class). The single-group topology eliminates ALL inter-class
      design questions (no shared base class, no IClassFixture<>, no
      Trait per group — the group label IS the class name). Helper-lift
      nuance: the three local functions in Dart `main` scope (outside
      the `group` body but inside `main`) lift onto the SAME class as
      the test methods (not a separate utility class), because the
      test methods CALL them directly and xUnit `[Fact]` methods have
      no cross-class import.
  - construct_key: dart.local_function.named_inner_helper
    source_form: >-
      "ast.Module parseFile(String path) {
         final source = File(path).readAsStringSync();
         final lexer = Lexer(source);
         final tokens = lexer.tokenize();
         final parser = Parser(tokens);
         return parser.parseModule();
       }
       TypeEnvironment buildAncestorScope(String targetFile) { ... }
       TypeCheckResult typeCheckFile(String path) { ... }"
    target_decision: >-
      Each Dart local function declared inside `main()`'s body lifts to
      a `private` instance method on the test class
      `CssgModulesEndToEndTests`. Concretely:
      `private ast.Module ParseFile(string path) {
         var source = File.ReadAllText(path);
         var lexer = new Lexer(source);
         var tokens = lexer.Tokenize();
         var parser = new Parser(tokens);
         return parser.ParseModule();
       }`
      and analogously `private TypeEnvironment BuildAncestorScope(string
      targetFile) { ... }` and `private TypeCheckResult TypeCheckFile(
      string path) { ... }`. Static would also work (none of the three
      captures instance state — they only read `CssgRoot`, a `const`
      field), but the test-helper idiom default is `private` instance
      (precedent: partial_evaluator_test.dart.md's `Module ParseModule(
      string source)` private-instance lift). Naming: snake-case-aware
      Dart `parseFile`/`buildAncestorScope`/`typeCheckFile` → PascalCase
      C# `ParseFile`/`BuildAncestorScope`/`TypeCheckFile` per the
      langpair member-name convention. Parameter types: `String path`
      → `string path`; return types `ast.Module`, `TypeEnvironment`,
      `TypeCheckResult` translate as recorded in the respective lib
      specs (see `dart.package_under_test.import_directive` above).
    idiom_id: rf-dart-local-function-to-csharp-private-method
    research_finding_id: rf-dart-local-function-to-csharp-private-method
    nuance: >-
      Cached idiom (precedent: partial_evaluator_test.dart.md
      `Module ParseModule(string source) { ... }` private-instance
      lift; module_syntax_v2_test.dart.md `parseModule` closure-to-method
      lift). C# 7+ local functions (Microsoft Learn "Local functions":
      /dotnet/csharp/programming-guide/classes-and-structs/local-functions)
      would also be a valid target shape — but a local function inside
      a `[Fact]` method would be invisible to siblings, whereas these
      three Dart helpers are SHARED across multiple `test(...)` callbacks.
      The class-level private-method lift is the only correct shape for
      cross-test sharing. Closure-capture nuance EXPLICITLY addressed:
      `parseFile` captures NOTHING from `main` scope; `buildAncestorScope`
      reads `cssgRoot` (lifted to a const field — see
      `dart.package_test.main_entrypoint`); `typeCheckFile` calls the
      other two by name (preserved in C# as direct method calls on
      `this`). No closed-over mutable state.
  - construct_key: dart.collections.typed_empty_map_literal_with_indexer_write
    source_form: >-
      "final types = <String, TypeDef>{};
       final procedures = <String, ProcDecl>{};
       for (final typeDef in selfModule.typeDefs) {
         types[typeDef.name] = typeDef;
       }
       for (final procDecl in selfModule.procDeclarations) {
         procedures[procDecl.qualifiedKey] = procDecl;
       }
       final selfEnv = TypeEnvironment(types, procedures);"
    target_decision: >-
      Dart typed-empty map literal `<K, V>{}` followed by indexer-write
      assignment maps to C# `new Dictionary<K, V>()` followed by indexer-
      write per the cached `rf-dart-map-literal-typed-to-csharp-dictionary`
      idiom (precedents: varref_pointer_test.dart.md, suspension_pointer_test.dart.md,
      module_hierarchy.dart.md). Emitted form:
      `var types = new Dictionary<string, TypeDef>();
       var procedures = new Dictionary<string, ProcDecl>();
       foreach (var typeDef in selfModule.TypeDefs) {
         types[typeDef.Name] = typeDef;
       }
       foreach (var procDecl in selfModule.ProcDeclarations) {
         procedures[procDecl.QualifiedKey] = procDecl;
       }
       var selfEnv = new TypeEnvironment(types, procedures);`.
      Dart `<String, TypeDef>{}` (PascalCased element type) → C#
      `new Dictionary<string, TypeDef>()` (lowercase keyword `string`,
      PascalCase `TypeDef`). The for-in loops use the cached
      `rf-dart-for-in-to-csharp-foreach` idiom (see
      `dart.for_in_loop_over_list` below). The `TypeEnvironment(types,
      procedures)` positional-constructor call uses the cached
      `rf-dart-constructor-call-without-new-to-csharp-new` idiom (Dart
      2.x drops `new`; C# requires it).
    idiom_id: rf-dart-map-literal-typed-to-csharp-dictionary
    research_finding_id: rf-dart-map-literal-typed-to-csharp-dictionary
    nuance: >-
      Cached idiom (precedents: varref_pointer_test.dart.md,
      suspension_pointer_test.dart.md, module_hierarchy.dart.md).
      Insertion-order nuance (carry-forward, EXPLICITLY addressed): Dart
      `<K,V>{}` is a `LinkedHashMap` (insertion-ordered); C#
      `Dictionary<K,V>` documents iteration order as implementation-defined
      (Microsoft Learn `System.Collections.Generic.Dictionary<TKey, TValue>`:
      /dotnet/api/system.collections.generic.dictionary-2 — the order in
      which the items are returned is undefined). For THIS file the maps
      are consumed by the `TypeEnvironment(types, procedures)` constructor
      which (per type_environment_builder.dart.md) treats them as
      KEYED-LOOKUP collections — iteration order is NOT observable. If a
      future test asserts iteration order, codegen would need to switch
      to `SortedDictionary<K,V>` or preserve insertion order via
      `List<KeyValuePair<K,V>>` (Microsoft Learn `SortedDictionary<TKey,
      TValue>`); not needed here. Indexer-write semantics: Dart
      `m[k] = v` (insert-or-overwrite) and C# `m[k] = v` (insert-or-
      overwrite) are observably identical.
  - construct_key: dart.for_in_loop_over_list
    source_form: >-
      "for (final selfGlpPath in chain) { ... }
       for (final typeDef in selfModule.typeDefs) { ... }
       for (final procDecl in selfModule.procDeclarations) { ... }"
    target_decision: >-
      Dart `for (final <T> <name> in <Iterable<T>>) { ... }` maps to C#
      `foreach (var <name> in <IEnumerable<T>>) { ... }` per the cached
      `rf-dart-for-in-to-csharp-foreach` idiom (precedent:
      debug_negative.dart.md; many lib specs). Three uses in this file:
      (a) line 62 — iterating `chain` (the `List<String>` returned by
      `discoverSelfChain`) in `buildAncestorScope`; (b) line 69 —
      iterating `selfModule.typeDefs` (`List<TypeDef>` per
      type_ast.dart.md); (c) line 72 — iterating `selfModule.procDeclarations`
      (`List<ProcDecl>` per type_ast.dart.md). All three loops have
      `final` element bindings (immutable per-iteration), which maps to
      C# `foreach`'s implicit immutable per-iteration binding.
    idiom_id: rf-dart-for-in-to-csharp-foreach
    research_finding_id: rf-dart-for-in-to-csharp-foreach
    nuance: >-
      Cached idiom (precedent: debug_negative.dart.md prose). `final`
      vs `var` nuance (carry-forward): Dart `for (final x in xs)` uses
      `final` to mark x as a fresh, immutable per-iteration binding;
      C# `foreach (var x in xs)` ALSO produces a fresh per-iteration
      binding that cannot be reassigned within the loop body (Microsoft
      Learn "foreach statement": /dotnet/csharp/language-reference/statements/iteration-statements
      — C# 5+ scoping change). Iterable-type nuance: all three Dart
      `List<T>`s implement `Iterable<T>`; the converted C# `IList<T>`s
      implement `IEnumerable<T>`, which `foreach` consumes.
  - construct_key: dart.local_var_with_reassignment
    source_form: >-
      "var env = buildPreludeEnvironment();
       for (final selfGlpPath in chain) {
         ...
         env = env.merge(selfEnv);
       }
       return env;"
    target_decision: >-
      Dart `var env = buildPreludeEnvironment()` declares a mutable
      local with inferred type `TypeEnvironment`; the subsequent
      `env = env.merge(selfEnv)` inside the for-in body reassigns the
      same local. Maps 1:1 to C# `var env = BuildPreludeEnvironment();`
      followed by `env = env.Merge(selfEnv);` in the foreach body
      (cached `rf-dart-var-mutable-local-to-csharp-var-local` idiom
      from debug_negative.dart.md). `buildPreludeEnvironment` is a free
      top-level function on the Dart side, hosted in C# by the
      `PreludeEnvironment` static class per type_environment_builder.dart.md
      — emitted form: `var env = PreludeEnvironment.BuildPreludeEnvironment();`.
      `env.merge(selfEnv)` is an instance method per type_ast.dart.md /
      type_environment_builder.dart.md.
    idiom_id: rf-dart-var-mutable-local-to-csharp-var-local
    research_finding_id: rf-dart-var-mutable-local-to-csharp-var-local
    nuance: >-
      Cached idiom (precedent: debug_negative.dart.md). Mutability
      asymmetry (carry-forward, EXPLICITLY addressed): Dart distinguishes
      `final` (immutable local) from `var` (mutable local); C# `var`
      is ALWAYS mutable (no first-class `let`/`readonly`-local
      keyword). The information that Dart explicitly chose `var` here
      because `env` is reassigned is lost in the C# target, but
      observably unaffected (the reassignment is in both forms).
      Method-call dispatch: `env.merge(selfEnv)` returns a NEW
      `TypeEnvironment` (per type_environment_builder.dart.md — merge
      is functional, not mutating); the assignment `env = env.merge(...)`
      replaces the local binding to the returned object. C# `env =
      env.Merge(selfEnv)` has the identical semantics (the parameter
      `env` is by-value; the returned reference replaces it).
  - construct_key: dart.string.interpolation_dollar_local
    source_form: >-
      "'cssg_modules directory not found at $cssgRoot';
       '$cssgRoot/self.glp'; '$cssgRoot/agent.glp';
       '$cssgRoot/ui/mediator.glp'; '$cssgRoot/ui/actors.glp';
       '$cssgRoot/boot.glp';
       'self.glp type errors:\n${result.errors.join('\n')}';
       'agent.glp type errors:\n${result.errors.join('\n')}';
       'ui/mediator.glp type errors:\n${result.errors.join('\n')}';
       'ui/actors.glp type errors:\n${result.errors.join('\n')}';"
    target_decision: >-
      Dart `'...$identifier...'` (bare-identifier interpolation) and
      `'...${expression}...'` (braced-expression interpolation) BOTH
      map to C# `$"...{identifier}..."` / `$"...{expression}..."`
      (interpolated string literal — Microsoft Learn "Interpolated
      strings": /dotnet/csharp/language-reference/tokens/interpolated).
      Per the cached `rf-dart-string-interpolation-to-csharp-interpolated-string`
      idiom (precedents: debug_negative.dart.md
      `dart.string_interpolation`, module_hierarchy.dart.md
      `dart.string_interpolation_path_composition_with_platform_separator`,
      many lib specs). Specific conversions in this file:
      `'$cssgRoot/self.glp'` → `$"{CssgRoot}/self.glp"` (and analogously
      for the other four `.glp` paths);
      `'cssg_modules directory not found at $cssgRoot'` →
      `$"cssg_modules directory not found at {CssgRoot}"`;
      `'self.glp type errors:\n${result.errors.join('\n')}'` →
      `$"self.glp type errors:\n{string.Join(\"\\n\", result.Errors)}"`
      (the `\n` escape is preserved literally; the embedded `join('\n')`
      call maps via `dart.iterable.join_string_separator` below).
    idiom_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      Cached idiom (precedents: module_hierarchy.dart.md prose,
      debug_negative.dart.md). Bare-identifier vs braced nuance
      (carry-forward): Dart `$x` (without braces) requires `x` to be a
      bare identifier (no member access); Dart `${expr}` permits any
      expression. C# `{expr}` permits any expression in EITHER form
      (no Dart-style bare-vs-braced distinction — every C# interpolation
      uses braces). Codegen MUST always emit `{...}` braces (Dart's
      bare `$x` form has no brace-less C# counterpart). Escape-sequence
      nuance: `\n` is identical (LF) in both; `\\` is identical; the
      embedded `'\n'` literal in the Dart `join('\n')` call maps to
      `"\n"` in C# `string.Join("\n", ...)`. CRITICAL: C# `$"..."`
      requires `"` quotes (not `'`); any single-quoted Dart string
      passed to `join` becomes a double-quoted C# string, which inside
      a `$"..."` requires escaping as `\"`.
  - construct_key: dart.iterable.join_string_separator
    source_form: "result.errors.join('\n')"
    target_decision: >-
      Dart `Iterable.join(<separator>)` (Dart `dart:core`
      `Iterable<E>.join([String separator = ""])` —
      api.dart.dev/stable/dart-core/Iterable/join.html) maps to C# static
      `string.Join(<separator>, <enumerable>)` per the cached
      `rf-dart-iterable-join-to-csharp-string-join` idiom (Microsoft
      Learn `System.String.Join(String, IEnumerable<String>)`:
      /dotnet/api/system.string.join). Note the argument-order
      INVERSION: Dart `xs.join(sep)` has the iterable as receiver and
      separator as argument; C# `string.Join(sep, xs)` has the
      separator FIRST then the iterable. Emitted form for this file's
      four uses: `string.Join("\n", result.Errors)`. The result is the
      single concatenated string used inside the interpolated
      `'... type errors:\n${...}'` message (see
      `dart.string.interpolation_dollar_local` above) which itself feeds
      into `fail(...)` (see `dart.package_test.fail_call_with_message`).
    idiom_id: rf-dart-iterable-join-to-csharp-string-join
    research_finding_id: rf-dart-iterable-join-to-csharp-string-join
    nuance: >-
      Cached idiom (multiple lib spec precedents — string-builder /
      message-formatting code in compiler/runtime). Argument-order
      INVERSION nuance EXPLICITLY addressed (well-known footgun
      mirroring the `Assert.Equal` actual-vs-expected flip): Dart's
      receiver-style `xs.join(sep)` reverses argument order vs C#
      static `string.Join(sep, xs)`. Element-type nuance: Dart
      `Iterable<E>.join` calls `toString()` on each `E`; C#
      `string.Join<T>(string, IEnumerable<T>)` ALSO calls `ToString()`
      on each `T`. Both: empty iterable → empty string; single-element
      iterable → that element's toString (no separator inserted). Edge
      case: Dart's default separator is `""` (empty); C#'s
      `string.Join(string?, IEnumerable<string?>)` treats `null` as `""`.
      THIS file always passes a non-null `'\n'`, so no edge case is
      exercised.
  - construct_key: dart.package_test.test_call_simple
    source_form: >-
      "test('self.glp parses and type-checks', () { ... });
       test('agent.glp type-checks with PE and ancestor scope', () { ... });
       test('ui/mediator.glp type-checks with PE and ancestor scope', () { ... });
       test('ui/actors.glp type-checks with PE and ancestor scope', () { ... });
       test('boot.glp parses (untyped orchestration)', () { ... });"
    target_decision: >-
      Each Dart `test(label, body)` with a synchronous closure and no
      `skip:` argument becomes a `public void` instance method on the
      enclosing xUnit class `CssgModulesEndToEndTests`, decorated with
      `[Fact(DisplayName = "<original label>")]` (cached
      `rf-dart-test-callback-to-xunit-method-body` idiom; precedents:
      mad_error_handling_test.dart.md, boot_loader_test.dart.md,
      module_parser_test.dart.md). Method-name PascalCase with non-
      identifier characters stripped: `'self.glp parses and type-checks'`
      → `SelfGlpParsesAndTypeChecks`; `'agent.glp type-checks with PE
      and ancestor scope'` → `AgentGlpTypeChecksWithPeAndAncestorScope`;
      `'ui/mediator.glp type-checks with PE and ancestor scope'` →
      `UiMediatorGlpTypeChecksWithPeAndAncestorScope`;
      `'ui/actors.glp type-checks with PE and ancestor scope'` →
      `UiActorsGlpTypeChecksWithPeAndAncestorScope`;
      `'boot.glp parses (untyped orchestration)'` →
      `BootGlpParsesUntypedOrchestration`. All five callbacks are
      synchronous (no `async`/`Future`) so NO target method is `async
      Task`. The arrange/act/assert closure body translates statement-
      for-statement.
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Cached idiom (multiple precedents). Async nuance (carry-forward,
      absent here): a Dart `test('...', () async { ... })` would target
      `public async Task <Name>()`; none of THIS file's callbacks are
      async. Closure-capture nuance: each callback captures `cssgRoot`
      (lifted to the test class's `CssgRoot` const field) AND calls the
      `parseFile`/`buildAncestorScope`/`typeCheckFile` helpers (lifted to
      private instance methods on the same class) — the lift makes both
      captures direct `this.<member>` accesses with no need for
      instance fields beyond the lifted const. Display-name preservation:
      labels contain `/` (e.g. `'ui/mediator.glp...'`); slashes in
      `DisplayName` are PERMITTED (xUnit prints them verbatim in the
      test runner), so no escaping is needed.
  - construct_key: dart.package_test.expect_isTrue_with_reason
    source_form: >-
      "expect(File(selfPath).existsSync(), isTrue, reason: 'self.glp must exist');
       expect(File(agentPath).existsSync(), isTrue, reason: 'agent.glp must exist');
       expect(File(mediatorPath).existsSync(), isTrue, reason: 'ui/mediator.glp must exist');
       expect(File(actorsPath).existsSync(), isTrue, reason: 'ui/actors.glp must exist');
       expect(File(bootPath).existsSync(), isTrue, reason: 'boot.glp must exist');"
    target_decision: >-
      Map `expect(<bool>, isTrue, reason: <msg>)` to `Assert.True(<bool>,
      <msg>)` per the cached
      `rf-dart-expect-isTrue-with-reason-to-xunit-assert-true` idiom
      (precedent: mad_cold_call_isolate_test.dart.md;
      test_channel_construction.dart.md; suspension_pointer_test.dart.md).
      Emitted form: `Assert.True(File.Exists(selfPath), "self.glp must
      exist");` etc. for all five tests. The inner `File(path).existsSync()`
      uses the SAME cached file-IO idiom as the prelude block (see
      `dart.platform.file_existsSync_readAsStringSync`), mapped to
      `File.Exists(path)`. Reusing the cached two-arg `Assert.True(bool,
      string)` overload (Microsoft Learn xUnit `Assert.True(bool?, string)`
      — https://xunit.net/docs/comparisons#assertions) preserves the
      reason text in failure reports.
    idiom_id: rf-dart-expect-isTrue-with-reason-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-with-reason-to-xunit-assert-true
    nuance: >-
      Cached idiom (precedent: mad_cold_call_isolate_test.dart.md
      prose). Reason-parameter nuance (carry-forward, EXPLICITLY
      addressed): Dart `reason:` is a NAMED parameter; xUnit
      `userMessage` is POSITIONAL — conversion drops the `reason:`
      label and emits the message as the second positional argument.
      Strict-true nuance: Dart `isTrue` requires the actual to be `true`
      strictly (rejects truthy non-bools); xUnit `Assert.True` likewise
      requires `bool?` strictly — identical semantics. NUL/false
      reporting nuance: both forms surface the user-supplied message
      verbatim on failure plus an "Expected true / Actual false" framing.
  - construct_key: dart.package_test.fail_call_with_message
    source_form: >-
      "if (!result.isWellTyped) {
         fail('self.glp type errors:\n${result.errors.join('\n')}');
       }
       ... (4 more analogous fail() calls in the other type-check tests)"
    target_decision: >-
      Dart `fail(<msg>)` from `package:test` (api.dart.dev `package:test`
      `fail(String message)` — throws `TestFailure`) maps to xUnit
      `Assert.Fail(<msg>)` per the cached
      `rf-dart-fail-call-to-xunit-assert-fail` idiom (precedent:
      mad_cold_call_isolate_test.dart.md `dart.package_test.fail_call`
      prose). xUnit `Assert.Fail(string)` (added in xUnit.net v2.4.2 —
      https://xunit.net/docs/comparisons#assertions / Microsoft Learn
      "Unit testing best practices with xUnit") throws `XunitException`
      and is the canonical "unconditionally fail with message" assertion.
      Emitted form for each:
      `if (!result.IsWellTyped) {
         Assert.Fail($"self.glp type errors:\n{string.Join(\"\\n\",
                       result.Errors)}");
       }`. The interpolated string + `Iterable.join` mappings carry
      forward from the two preceding constructs.
    idiom_id: rf-dart-fail-call-to-xunit-assert-fail
    research_finding_id: rf-dart-fail-call-to-xunit-assert-fail
    nuance: >-
      Cached idiom (precedent: mad_cold_call_isolate_test.dart.md
      prose). Exception-type nuance EXPLICITLY addressed: Dart `fail`
      throws `TestFailure` (a `package:test` private subclass of
      `Error`); xUnit `Assert.Fail` throws `XunitException` (subclass
      of `System.Exception`). Both abort the current test method and
      mark the test as failed with the user-supplied message attached.
      The hierarchy-mismatch (Error vs Exception) is irrelevant inside
      a test method — both implementations unwind to the runner's
      failure handler. Conditional-fail nuance: this file's pattern
      (`if (!cond) fail(msg)`) is idiomatically `Assert.True(cond, msg)`
      in C# — but the explicit conditional + `fail` preserves the
      multi-line message-construction shape and keeps the diff smaller;
      codegen MAY collapse to `Assert.True(result.IsWellTyped, ...)`
      under a future "guarded-fail → assertion" simplification pass.
      Spec default: keep the explicit `if (!...) Assert.Fail(...)`
      shape for fidelity.
  - construct_key: dart.package_test.expect_isNotEmpty_matcher
    source_form: |-
      "expect(module.typeDefs, isNotEmpty, reason: 'self.glp should define shared types');
       expect(importedDecls, isNotEmpty, reason: 'boot.glp should have imported procedure declarations');
       expect(module.procedures, isNotEmpty, reason: 'boot.glp should have orchestration procedures');"
    target_decision: >-
      Dart `expect(x, isNotEmpty, reason: <msg>)` maps to xUnit
      `Assert.NotEmpty(x)` (single-arg — xUnit's `Assert.NotEmpty` has
      NO user-message overload as of xUnit v2.x). Per the cached
      `rf-dart-expect-isNotEmpty-to-xunit-assert-notempty` idiom
      (precedent: well_typed_term_test.dart.md). To preserve the
      `reason` message, codegen MAY emit the assertion as two stages:
      `Assert.True(x.Any(), <reason>);` (xUnit `Assert.True(bool?,
      string)` with `IEnumerable<T>.Any()` from `System.Linq`) — but the
      dedicated `Assert.NotEmpty` is the canonical mapping per the
      cached idiom's "dedicated-assertion rule" (recorded in
      boot_loader_test.dart.md / well_typed_term_test.dart.md). Spec
      default: emit `Assert.NotEmpty(<actual>)` and DROP the `reason`
      string — xUnit's failure output ("Assert.NotEmpty() failure")
      already pinpoints the call site, and the dedicated-assertion
      rule prevents downgrading to a generic `Assert.True`. Codegen
      MUST add `using System.Linq;` IFF it falls back to the `.Any()`
      form (not needed for the default `Assert.NotEmpty` form).
    idiom_id: rf-dart-expect-isNotEmpty-to-xunit-assert-notempty
    research_finding_id: rf-dart-expect-isNotEmpty-to-xunit-assert-notempty
    nuance: >-
      Cached idiom (precedent: well_typed_term_test.dart.md). Symmetry
      nuance (carry-forward): `isEmpty`/`isNotEmpty` ↔ `Assert.Empty`/
      `Assert.NotEmpty` is an exact 1:1 dedicated-assertion mapping.
      Reason-parameter loss EXPLICITLY addressed (NEW facet — prior
      `expect_isNotEmpty` precedent did not exercise `reason:`): xUnit
      `Assert.NotEmpty(IEnumerable)` lacks a `(IEnumerable, string)`
      overload, so the Dart `reason:` text is lost in the default
      mapping. This loss is acceptable because the failure framing
      ("Assert.NotEmpty() failure: Collection was empty") plus stack
      trace makes the location unambiguous. If a future test relies on
      the reason text being in failure output, codegen would have to
      use the `Assert.True(x.Any(), reason)` fallback — recorded here
      so future files can re-evaluate. Element-type nuance: the three
      Dart collections (`module.typeDefs`, `importedDecls`,
      `module.procedures`) are all `List<...>` per ast.dart.md, which
      trivially satisfies `IEnumerable<T>` for the C# `Assert.NotEmpty`.
  - construct_key: dart.iterable.where_to_list
    source_form: >-
      "final importedDecls =
         module.procDeclarations.where((d) => d.imported).toList();"
    target_decision: >-
      Dart `Iterable<T>.where(<predicate>).toList()` (Dart `dart:core`
      `Iterable.where`: filter lazily; `.toList()`: materialise to
      `List<T>`) maps to C# LINQ `IEnumerable<T>.Where(<predicate>).ToList()`
      (System.Linq — Microsoft Learn `Enumerable.Where<TSource>`:
      /dotnet/api/system.linq.enumerable.where and `Enumerable.ToList<TSource>`:
      /dotnet/api/system.linq.enumerable.tolist) per the cached
      `rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist` idiom
      (precedents: mode_table.dart.md prose, analysis_phase.dart.md
      prose, lib/multiagent/message_queue.dart.md). Emitted form:
      `var importedDecls = module.ProcDeclarations.Where(d =>
      d.Imported).ToList();`. The Dart arrow-function `(d) =>
      d.imported` maps to a C# lambda `d => d.Imported` (identical
      single-expression-body shape). Codegen MUST add `using System.Linq;`
      because `Where`/`ToList` are extension methods on
      `IEnumerable<T>` in the `System.Linq` namespace.
    idiom_id: rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist
    research_finding_id: rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist
    nuance: >-
      Cached idiom (multiple lib spec precedents). Laziness nuance
      EXPLICITLY addressed: Dart `where` returns a LAZY `Iterable`
      (filter applied on iteration); C# `Where` also returns a LAZY
      `IEnumerable<T>` (deferred execution). Both eager-materialise on
      `.toList()` / `.ToList()`. Predicate-eval-count nuance: both
      forms evaluate the predicate once per element during
      materialisation. Boolean-property nuance: Dart `d.imported`
      reads a `bool` getter on `ProcedureDeclaration` per ast.dart.md;
      C# `d.Imported` reads a `bool` property (per the lib spec's
      property mapping). Single-statement-body lambda: Dart `(d) =>
      d.imported` and C# `d => d.Imported` are observably identical
      forms; the parenthesised parameter list `(d)` is canonical in
      Dart but optional (one-parameter lambdas may omit parens — but
      this file keeps them). C# also permits both `d => ...` and `(d)
      => ...`; spec default emits the bare `d => d.Imported` form per
      LINQ-idiomatic style.
  - construct_key: dart.string.single_quoted_literal
    source_form: >-
      "'../programs/self.glp';
       '../programs/cssg_modules';
       'cssg_modules end-to-end';
       'self.glp parses and type-checks'; 'self.glp must exist';
       'self.glp should define shared types';
       'self.glp type errors:\n';
       'agent.glp type-checks with PE and ancestor scope'; 'agent.glp must exist';
       'agent.glp type errors:\n';
       'ui/mediator.glp...'; ... 'ui/actors.glp...';
       'boot.glp parses (untyped orchestration)'; 'boot.glp must exist';
       'boot.glp should have imported procedure declarations';
       'boot.glp should have orchestration procedures';
       '\n';"
    target_decision: >-
      Dart single-quoted single-line string literals map to C#
      double-quoted string literals per the cached
      `rf-dart-single-quoted-string-to-csharp-double-quoted-string`
      idiom (precedent: module_parser_test.dart.md). Escape-sequence
      nuance is trivial here: `\n` (LF) is identical in both; no
      Dart-specific escapes (`\u{...}`, `\$`) appear in this file.
      Embedded apostrophes: NONE in this file's literals. Embedded
      double-quotes: NONE.
    idiom_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    research_finding_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md). Quote-
      character nuance carries forward verbatim. NEW facet
      EXPLICITLY addressed (vs module_parser_test.dart.md): this file's
      literal set includes path strings with FORWARD SLASHES (e.g.
      `'../programs/self.glp'`) — slashes are not special in either
      Dart `'...'` or C# `"..."` and are emitted verbatim. The Dart
      `'\n'` literal (line-ending escape) maps to C# `"\n"` — identical
      LF byte; the standalone-string use inside `'string.Join('\n', ...)'`
      becomes `string.Join("\n", ...)` (carry-forward from
      `dart.iterable.join_string_separator`).
  - construct_key: dart.local.final_var_declaration
    source_form: >-
      "final rootSelfGlp = File('../programs/self.glp');
       final source = rootSelfGlp.readAsStringSync();
       final cssgRoot = '../programs/cssg_modules';
       final lexer = Lexer(source); final tokens = lexer.tokenize();
       final parser = Parser(tokens);
       final chain = discoverSelfChain(targetFile: targetFile, rootDir: cssgRoot);
       final types = <String, TypeDef>{}; final procedures = <String, ProcDecl>{};
       final selfEnv = TypeEnvironment(types, procedures);
       final module = parseFile(path); final ancestorScope = buildAncestorScope(path);
       final program = ast.Program(module.procedures, module.line, module.column);
       final pe = PartialEvaluator(); final transformedAst = pe.transformDefinedGuards(program);
       final selfPath = '$cssgRoot/self.glp'; final agentPath = '$cssgRoot/agent.glp';
       final mediatorPath = '$cssgRoot/ui/mediator.glp';
       final actorsPath = '$cssgRoot/ui/actors.glp';
       final bootPath = '$cssgRoot/boot.glp';
       final result = typeCheckFile(...); final module = parseFile(bootPath);
       final importedDecls = module.procDeclarations.where(...).toList();
       final selfGlpPath /* loop var */; final typeDef /* loop var */; final procDecl /* loop var */;"
    target_decision: >-
      Every `final <name> = <expr>;` local maps to `var <name> = <expr>;`
      in C# per the cached `rf-dart-final-local-to-csharp-var-local`
      idiom (precedents: boot_loader_test.dart.md, varref_pointer_test.dart.md,
      module_parser_test.dart.md). Constructor calls without `new` in
      Dart 2.x (`Lexer(source)`, `Parser(tokens)`, `PartialEvaluator()`,
      `TypeEnvironment(types, procedures)`, `ast.Program(...)`) map to
      `new Lexer(source)` / `new Parser(tokens)` / etc. in C# (cached
      `rf-dart-constructor-call-without-new-to-csharp-new`). Dart NAMED
      arguments at the `discoverSelfChain(targetFile: targetFile,
      rootDir: cssgRoot)` call become C# NAMED arguments `targetFile:
      targetFile, rootDir: cssgRoot` (C# 4+ supports the same call-site
      syntax — per `lib/runtime/module_hierarchy.dart.md`
      `discoverSelfChain` signature notes).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Cached idiom (multiple precedents). Single-assignment nuance
      carries forward verbatim. Constructor-call-without-`new` nuance
      (carry-forward, EXPLICITLY addressed here for `Lexer`, `Parser`,
      `PartialEvaluator`, `TypeEnvironment`, `ast.Program` — five
      sites): Dart 2.x makes `new` optional; C# requires `new` for
      instance construction. Named-argument nuance (NEW facet vs
      module_parser_test.dart.md — this file uses named args at the
      `discoverSelfChain` call): Dart REQUIRES the caller to pass the
      `required`-named parameters by name (Dart compile-error
      otherwise per module_hierarchy.dart.md analysis); C# named
      arguments at the call site are SUPPORTED but never required
      (C# 4+, Microsoft Learn "Named and Optional Arguments":
      /dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments).
      Codegen MUST preserve the call-site named-arg syntax for
      readability fidelity: emit `DiscoverSelfChain(targetFile:
      targetFile, rootDir: CssgRoot)` rather than positional
      `DiscoverSelfChain(targetFile, CssgRoot)`.
conversion_units:
  - cu-1: >-
      file-scope using directives: `using Xunit;`, `using System.IO;`,
      `using System.Collections.Generic;`, `using System.Linq;`,
      plus the SUT namespaces produced by converting
      lib/compiler/{lexer,parser,ast,partial_evaluator}.dart,
      lib/analysis/type_checker/{type_ast,type_environment_builder,type_checker}.dart,
      lib/runtime/module_hierarchy.dart (three `using` lines collapsing
      eight Dart imports), plus the `using ast = <RootNs>.Compiler;`
      alias for the `ast.Module` / `ast.Program` qualifiers.
  - cu-2: >-
      namespace declaration mirroring the test/module path (e.g.
      `<RootNs>.Test.Module`).
  - cu-3: >-
      class `CssgModulesEndToEndTests` (single class, mirroring the
      single `group(...)` block) with: (a) a `private const string
      CssgRoot = "../programs/cssg_modules";` field (lifted from the
      Dart `final cssgRoot` local in main's scope); (b) a `static`
      constructor performing the pre-group init (File.Exists/ReadAllText
      for self.glp + Directory.Exists guard with InvalidOperationException
      throw); (c) three `private` instance helper methods `ParseFile`,
      `BuildAncestorScope`, `TypeCheckFile` (lifted from main's local
      functions); (d) five `[Fact(DisplayName=...)]` instance methods
      (`SelfGlpParsesAndTypeChecks`, `AgentGlpTypeChecksWithPeAndAncestorScope`,
      `UiMediatorGlpTypeChecksWithPeAndAncestorScope`,
      `UiActorsGlpTypeChecksWithPeAndAncestorScope`,
      `BootGlpParsesUntypedOrchestration`).
  - cu-4: >-
      no other test classes (this file has ONE top-level group only —
      contrast with module_parser_test.dart.md's six sibling classes).
escalations: []
```

## Rationale + research provenance

### Cached-idiom reuse profile (SC-007 / FR-012)

19 of the 20 constructs in this file resolve via a CACHED `idiom_id`
from prior conversion specs. The KB-lookup decision-order from
`convspec_idiom_schema.md` was applied per construct: KB lookup hit
(active status) → REUSE verbatim; no re-research, no re-derivation.
The cached idioms reused:

- `rf-dart-package-test-import-to-xunit-using` (precedent:
  mad_error_handling_test.dart.md, module_parser_test.dart.md)
- `rf-dart-import-dartio-to-csharp-using-systemio` (precedent:
  partial_evaluator_test.dart.md, runtime.dart.md)
- `rf-dart-internal-package-import-to-csharp-using` (precedents:
  boot_loader_test.dart.md, module_parser_test.dart.md,
  module_syntax_v2_test.dart.md)
- `rf-dart-package-test-main-omit-in-xunit` (precedents:
  mad_error_handling_test.dart.md, boot_loader_test.dart.md,
  partial_evaluator_test.dart.md, module_parser_test.dart.md)
- `rf-dart-file-existssync-readasstringsync-to-system-io-file-exists-readalltext`
  (precedent: partial_evaluator_test.dart.md)
- `rf-dart-directory-existssync-to-system-io-directory-exists`
  (precedents: lib/runtime/module_hierarchy.dart.md,
  lib/compiler/project_linker.dart.md)
- `rf-dart-stateerror-throw-to-csharp-invalidoperationexception`
  (precedents: mad_transactions_test.dart.md prose,
  circular_term_test.dart.md prose)
- `csharp-static-class-no-toplevel-members` (precedent:
  partial_evaluator_test.dart.md, glp_runtime_test.dart.md)
- `rf-dart-package-test-group-to-xunit-class` (precedents:
  mad_error_handling_test.dart.md, boot_loader_test.dart.md,
  module_parser_test.dart.md)
- `rf-dart-local-function-to-csharp-private-method` (precedent:
  partial_evaluator_test.dart.md `ParseModule` lift; module_syntax_v2_test.dart.md)
- `rf-dart-map-literal-typed-to-csharp-dictionary` (precedents:
  varref_pointer_test.dart.md, suspension_pointer_test.dart.md,
  lib/runtime/module_hierarchy.dart.md)
- `rf-dart-for-in-to-csharp-foreach` (precedent: debug_negative.dart.md)
- `rf-dart-var-mutable-local-to-csharp-var-local` (precedent:
  debug_negative.dart.md)
- `rf-dart-string-interpolation-to-csharp-interpolated-string`
  (precedents: debug_negative.dart.md, lib/runtime/module_hierarchy.dart.md
  prose, many lib specs)
- `rf-dart-iterable-join-to-csharp-string-join` (multiple lib spec
  precedents)
- `rf-dart-test-callback-to-xunit-method-body` (multiple precedents)
- `rf-dart-expect-isTrue-with-reason-to-xunit-assert-true` (precedents:
  mad_cold_call_isolate_test.dart.md, test_channel_construction.dart.md,
  suspension_pointer_test.dart.md)
- `rf-dart-fail-call-to-xunit-assert-fail` (precedent:
  mad_cold_call_isolate_test.dart.md prose)
- `rf-dart-expect-isNotEmpty-to-xunit-assert-notempty` (precedent:
  well_typed_term_test.dart.md)
- `rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist` (multiple
  lib spec precedents — message_queue.dart.md, analysis_phase.dart.md)
- `rf-dart-single-quoted-string-to-csharp-double-quoted-string`
  (precedent: module_parser_test.dart.md)
- `rf-dart-final-local-to-csharp-var-local` (precedents:
  boot_loader_test.dart.md, varref_pointer_test.dart.md,
  module_parser_test.dart.md)

Reusing cached idioms verbatim (no re-research, no re-derivation)
satisfies the FR-012 / SC-007 consistency guarantee.

### No NEW idiom rows

This file introduces ZERO first-seen idioms. Every construct is
covered by a cached active idiom whose precedent is at least one prior
test-file or lib-file convspec. Decision-order step 2 (KB-hit ⇒ REUSE)
short-circuits research per FR-012 / SC-007 — no research sub-agent
was spawned for any construct. This is the expected pattern as the
KB matures: the convspec batch has now accumulated enough precedent
(20+ test-file convspecs, 50+ lib-file convspecs) that virtually every
test-file construct lookup is a cache hit.

### Sibling helper functions and the static constructor

The Dart `main()` body in this file has a non-trivial pre-`group`
shape: two prelude-source setters (`setPreludeUnitClauseSource` AND
`setPreludeEnvironmentSource`) plus a `Directory.existsSync()` guard
that throws `StateError` on failure. This is a STRICTER pre-init than
`partial_evaluator_test.dart.md` (which only sets
`setPreludeUnitClauseSource`). All three side effects belong inside
the C# `static` constructor of the `CssgModulesEndToEndTests` class:

1. `File.Exists("../programs/self.glp")` + `File.ReadAllText` once
   into `var source`
2. `PreludeUnitClauses.SetPreludeUnitClauseSource(source);`
3. `PreludeEnvironment.SetPreludeEnvironmentSource(source);`
4. `if (!Directory.Exists(CssgRoot)) throw new
   InvalidOperationException($"cssg_modules directory not found at
   {CssgRoot}");`

Static-constructor failure semantics (Microsoft Learn "Static
Constructors": /dotnet/csharp/programming-guide/classes-and-structs/static-constructors):
on type init failure, the runtime caches the failure and rethrows
`TypeInitializationException` on every subsequent access — this is
the C# analogue of Dart `main` aborting before any `test()`
registration, observationally equivalent. No `IClassFixture<T>` or
`ICollectionFixture<T>` is needed because the side effect targets
STATIC fields on global host classes, not per-fixture instance state.

### Single-group topology vs sibling-group topology

This file has ONE top-level group (`'cssg_modules end-to-end'`) — a
strictly simpler topology than `module_parser_test.dart.md` (six
sibling groups) or `boot_loader_test.dart.md` (outer + three nested).
The single-group case eliminates ALL inter-class design questions:
one group → one class, no shared base class, no `IClassFixture<>`,
no `[Trait]` grouping, no fixture-DI plumbing. The five test bodies
become five `[Fact(DisplayName=...)]` methods on the single
`CssgModulesEndToEndTests` class.

### Helper-function lift onto the same class

The three Dart local functions (`parseFile`, `buildAncestorScope`,
`typeCheckFile`) are declared inside `main()` but OUTSIDE the
`group()` body — they are SHARED by all five `test()` callbacks. The
cleanest C# target is to lift them onto the SAME class as the
`[Fact]` methods (private instance methods). The alternative
(C# 7+ local functions inside each `[Fact]` method) would force
duplication across the five tests; the alternative (a separate
utility class with `static` methods) would introduce a cross-class
dependency that adds friction without benefit. Per the cached
`rf-dart-local-function-to-csharp-private-method` idiom — also used
by `partial_evaluator_test.dart.md` for its `ParseModule` lift.

### Twin-prelude-setter handling

The file uses TWO parallel prelude setters
(`setPreludeUnitClauseSource` and `setPreludeEnvironmentSource`).
Per the lib specs, these are HOSTED BY DIFFERENT C# static classes:

- `setPreludeUnitClauseSource` ⇒ `PreludeUnitClauses.SetPreludeUnitClauseSource`
  (per `lib/compiler/partial_evaluator.dart.md`)
- `setPreludeEnvironmentSource` ⇒ `PreludeEnvironment.SetPreludeEnvironmentSource`
  (per `lib/analysis/type_checker/type_environment_builder.dart.md`)

Both setters consume the SAME `source` string (the contents of
`../programs/self.glp`). Codegen MUST NOT merge the two setters into
one and MUST NOT call `File.ReadAllText` twice — the Dart code reads
once into `final source` and uses it twice, which is the correct
shape for byte-identical-source guarantee across the twin host classes.

### Map-iteration-order is not observable here

The `buildAncestorScope` helper constructs two `Dictionary<string,
TypeDef>` / `Dictionary<string, ProcDecl>` locals, populates them
inside `foreach` loops, then hands them to the `TypeEnvironment(types,
procedures)` constructor. Per `lib/analysis/type_checker/type_environment_builder.dart.md`,
`TypeEnvironment` treats both as KEYED-LOOKUP collections — iteration
order is NOT observable. The Dart-side `LinkedHashMap` insertion-order
guarantee is therefore lossless when converted to C# `Dictionary<K,V>`
(implementation-defined order). If a future test of `TypeEnvironment`
asserted iteration order, codegen would need to switch to
`SortedDictionary<K,V>` or `List<KeyValuePair<K,V>>` — not needed
here.

### `expect(..., isNotEmpty, reason: ...)` reason-loss

xUnit's `Assert.NotEmpty(IEnumerable)` lacks a two-arg `(IEnumerable,
string)` overload as of xUnit 2.x — so converting the three uses of
`expect(x, isNotEmpty, reason: 'msg')` to `Assert.NotEmpty(x)` LOSES
the `reason` text. Per the cached
`rf-dart-expect-isNotEmpty-to-xunit-assert-notempty` idiom's
"dedicated-assertion rule" (preferring `Assert.NotEmpty` over a
generic `Assert.True(x.Any(), msg)` fallback for diagnostic clarity
at the call site), spec default is to DROP the reason string. xUnit's
failure framing ("Collection was empty") plus the stack-trace test
method name make the failure unambiguous in practice. This loss is
recorded in the construct's nuance so it is reviewable in PR.

### `fail(...)` vs `Assert.True(cond, msg)` shape preservation

The five `if (!result.isWellTyped) fail(...)` calls could collapse to
`Assert.True(result.IsWellTyped, $"...")` — semantically equivalent
since both produce a failed test with the supplied message. Spec
default PRESERVES the explicit `if + Assert.Fail` shape (per cached
`rf-dart-fail-call-to-xunit-assert-fail` idiom) for diff fidelity and
because the `fail()` message-construction path (with the
`'$cssgRoot/...\n${result.errors.join('\n')}'` interpolation +
LINQ-`Join` chain) is non-trivial — keeping it in a guarded branch
makes the message-construction code reviewable. Codegen MAY choose
the collapsed form under a future simplification pass; the current
spec records the lossless form.

### Why no escalations (FR-013)

Every construct has a clear, single-decision target shape grounded in
official Dart and .NET documentation, all already cached in the
conversion-idiom KB from prior convspecs. The "soft" decisions
(static-ctor vs `IClassFixture<>`; map insertion order;
`Assert.NotEmpty` vs `Assert.True+Any`; `fail` vs collapse) are
documented project-wide preferences (alternatives recorded in the
relevant research findings), not unresolved choices. No construct
involves an idiom-vs-research conflict or an idiom-vs-idiom conflict,
and nothing is undecidable. `escalations: []` is intentional, not a
placeholder.
