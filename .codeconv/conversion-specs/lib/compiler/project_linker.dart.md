# Conversion Spec — lib/compiler/project_linker.dart

> Conversion-spec artifact for lib/compiler/project_linker.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> File is the GLP *project linker* — a 480-line, three-stage static
> linker over directory-rooted multi-module GLP projects. The three
> stages (each is a top-level Dart free function): (1)
> `discoverProject(rootDir, {rootSelfGlpPath})` walks `rootDir`
> recursively, parses every `.glp` file (with a hard-coded exclusion
> list for `boot_direct.glp`, `mad_boot.glp`, and any file under a
> `mad_boot/` subdirectory), derives a module name (from `-module(M).`,
> else from filename — or for `self.glp` files without `-module()`,
> from the *parent directory* name), builds an ancestor-self.glp
> `TypeEnvironment` chain via `module_hierarchy.dart`'s
> `discoverSelfChain`, and yields a `List<DiscoveredModule>`.
> (2) `typeCheckProject(modules)` runs the same partial-evaluator-
> then-type-checker pipeline the single-file compiler uses
> (`PartialEvaluator.transformDefinedGuards` then `checkModule`) per
> module, throwing on any well-typedness failure with a per-module
> error report. (3) `linkProject(modules, topModuleName)` produces a
> single flat `Program` AST: every procedure is renamed `M:p/n`,
> every clause head and intra-module body call is rewritten to the
> renamed form, every `RemoteGoal` (`M' # p(...)`) with a static module
> name is lowered to a renamed direct goal, ancestor-self.glp procedures
> (inner-most wins) are looked up and rewritten, and `_makeAliasClause`
> generates *mode-aware* entry-point aliases (`p(V0,V1,V2) :-
> M:p(V0?,V1,V2)`) for the top module (every procedure) AND for every
> other module's *exported* procedures — using the `ProcDecl.isInputArg`
> per-arg-mode discipline to decide which body args become reader-
> annotated (`isInput == true`) vs writer-annotated (`isInput == false`).
>
> Load-bearing nuances exercised by THIS file (every one MUST be
> explicitly addressed in the C# render): (a) `dart:io` recursive
> filesystem walk via `Directory.listSync(recursive: true)` (synchronous,
> distinct from POSIX `walk`); (b) `Iterable.whereType<File>()` — Dart's
> type-narrowing filter over a polymorphic `FileSystemEntity` stream;
> (c) literal `'/'` path-separator check alongside
> `Platform.pathSeparator` — Dart-source preserves BOTH for the
> `mad_boot/` exclusion (so the C# render preserves both); (d) FOUR
> map-comprehension idioms — `<String, Set<String>>{}`,
> `<String, String>{}`, `<String, Map<String, String>>{}`,
> `<String, ProcDecl>{}` — each populated via `for`-in and queried via
> `containsKey` / `putIfAbsent`; (e) Dart `putIfAbsent(key, () => value)`
> — load-bearing, NOT `[k]=v` — for the inner-most-self.glp-wins
> ancestor map (the LAMBDA enforces lazy default + insert-only); (f)
> Dart `List.sort((a,b) => b.filePath.length.compareTo(a.filePath.length))`
> — path-length-descending sort to derive inner-most-to-outer-most
> ancestor order; (g) `identical(s, mod)` — Dart's reference-equality
> primitive, semantically distinct from `==`; (h) `where(predicate)
> .toList()` pipelines that build new lists from filtered iterables;
> (i) AST traversal + immutable-record-style rebuild: every renamed
> `Clause` / `Atom` / `Goal` / `Procedure` is a NEW instance — the source
> is not mutated (carry-forward from ast.dart.md identity discipline);
> (j) `goal is RemoteGoal` / `goal is SpawnGoal` runtime type tests +
> `staticModuleName` derived predicate — closed-set dispatch over the
> `Goal`/`RemoteGoal`/`SpawnGoal` sub-hierarchy; (k) the recursive
> `_resolveGoal` helper threads three pieces of immutable per-call state
> (`moduleName`, `localSigs`, `ancestorSelfProcs`) and returns a NEW
> goal on rewrite OR `identical`-preserves the original on no-op (for
> `SpawnGoal`'s inner-rewrite, a NEW `SpawnGoal` is allocated ONLY if
> the inner goal changed — `identical(resolvedInner, goal.innerGoal)`
> check); (l) `_findProcDecl` falls back to a project-wide
> `declIndex[sig]` via the `??` null-coalescing operator (Dart) ↔
> `??` (C#); (m) cast-to-supertype literal `as Term` on `VarTerm(...)`
> in two `List.generate` callbacks — needed by Dart because
> `List<Term>.generate` infers the *element type* from the FIRST callback
> return; this is a Dart-specific elaboration that the C# render
> resolves at API-level (the `Atom`/`Goal` ctors take `IReadOnlyList<Term>`
> directly so no per-element cast is required).
>
> Heavy reuse: every idiom on this page is grounded in prior research
> findings cached from runtime/module_hierarchy.dart.md (`dart:io`,
> `Platform.pathSeparator`, recursive directory walk, top-level free
> function → static class), lib/compiler/parser.dart.md (recursive-
> descent + AST rebuild discipline), lib/compiler/ast.dart.md
> (`Clause`/`Atom`/`Goal`/`RemoteGoal`/`SpawnGoal`/`VarTerm`/`Procedure`/
> `ProcDecl`/`Program` ctor surfaces, identity-vs-equality, named-required-
> and-default ctor parameters), lib/compiler/partial_evaluator.dart.md
> (relative-import folding to single namespace; PartialEvaluator ctor
> surface; `Program(procedures, line, column)` ctor), and
> lib/analysis/type_checker/type_checker.dart.md (`checkModule(module,
> transformedProcedures:, ancestorScope:)` signature + `TypeCheckResult`
> shape).

```yaml
schema_version: 1
source_path: lib/compiler/project_linker.dart
source_sha256: b3d11b764d4963e6d78f28841aa9bafd9e3032ca39c0457a7340d56180957a52
target_code_unit: lib/compiler/project_linker.cs
constructs:
  - construct_key: dart.docblock_triple_slash_file_header_with_spec_and_plan_citations_plus_library_directive
    source_form: >-
      Lines 1-9: triple-slash doc-block declaring the file's purpose
      ("Project linker: static linking of multi-module GLP projects.")
      followed by a process narrative (discover → type-check → flat
      Program with renamed inter-module calls), then two doc citations
      `docs/modules/glp-project-compilation-spec.md` (specification)
      and `docs/modules/project-compilation-implementation-plan.md`
      (plan). Line 9: `library;` directive (bare, no library name).
    target_decision: >-
      Translate the 9-line doc-block verbatim to a C# `///` XML-doc
      block attached to the hosting `public static class ProjectLinker`
      declaration in the `Glp.Compiler` namespace. The two cited
      paths (`docs/modules/glp-project-compilation-spec.md` and
      `docs/modules/project-compilation-implementation-plan.md`) are
      preserved byte-identically — diagnostic grep relies on the
      exact strings. The `library;` directive is ELIDED in C# (the
      C# unit's namespace declaration is the documented counterpart;
      carry-forward of `rf-dart-library-directive-to-csharp-namespace-
      elision` from runtime/suspension.dart.md / external_io.dart.md).
    idiom_id: null
    research_finding_id: rf-dart-docblock-triple-slash-to-csharp-xml-doc
    nuance: >-
      Doc-comment-and-library nuance (explicitly addressed): the
      file emits BOTH a triple-slash header AND a bare `library;` —
      a combination present in lib/runtime/external_io.dart and
      lib/runtime/suspension.dart but NOT in lib/runtime/module_hierarchy.dart.
      Per the cached `rf-dart-library-directive-to-csharp-namespace-
      elision` finding the `library;` directive is elided (no
      counterpart needed — the C# compilation unit's `namespace
      Glp.Compiler { ... }` carries the same intent). The doc-block
      migrates as XML-doc; the two spec/plan citations are
      load-bearing (grep targets), preserved verbatim. FR-024 cache
      hit — no new research.

  - construct_key: dart.import_directive.dart_io_plus_five_relative_compiler_and_analysis_and_runtime_imports
    source_form: >-
      Seven imports total: `import 'dart:io';` (uses `Directory`,
      `File`, `Platform.pathSeparator`); five sibling `lib/compiler/`
      relative imports `import 'ast.dart';`, `import 'lexer.dart';`,
      `import 'parser.dart';`, `import 'partial_evaluator.dart';`,
      and three cross-package imports
      `import '../analysis/type_checker/type_ast.dart';` (TypeEnvironment,
      ProcDecl),
      `import '../analysis/type_checker/param_expansion.dart';` (NOT used
      in body — see nuance — but kept transitively),
      `import '../analysis/type_checker/type_checker.dart';` (checkModule,
      TypeCheckResult),
      `import '../runtime/module_hierarchy.dart';` (discoverSelfChain,
      buildPreludeEnvironment), and
      `import '../analysis/type_checker/type_environment_builder.dart';`
      (re-exported by module_hierarchy.dart; consumed transitively by
      `_buildAncestorScope`). No `as`-prefix, no `show`/`hide` filter.
    target_decision: >-
      Map the SEVEN Dart imports to FOUR C# `using` directives in the
      compilation unit: (i) `using System.IO;` for `File`, `Directory`,
      `Path` — covers the `dart:io` import; (ii) NO `using` needed for
      the four sibling `lib/compiler/` files — same namespace
      `Glp.Compiler` makes their exports visible automatically
      (carry-forward of `rf-dart-relative-import-to-csharp-using-or-
      same-namespace` from partial_evaluator.dart.md); (iii)
      `using Glp.Analysis.TypeChecker;` covers `type_ast.dart`,
      `param_expansion.dart`, `type_checker.dart`, and
      `type_environment_builder.dart` (single using, four-Dart-import
      collapse — they all live in `lib/analysis/type_checker/` which
      maps to the `Glp.Analysis.TypeChecker` namespace per
      module_hierarchy.dart.md); (iv) `using Glp.Runtime;` covers
      `module_hierarchy.dart` (lives in `lib/runtime/` →
      `Glp.Runtime`). No `using static` is required — every consumed
      symbol from these namespaces is reached unqualified at every
      call site (no `Prelude.foo` shape in the Dart source, no `show`
      filter to narrow).
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-using-or-same-namespace
    nuance: >-
      Import-folding nuance (load-bearing, explicitly addressed):
      Dart `import` is per-FILE — even sibling files in the same
      `lib/compiler/` folder MUST be explicitly imported to consume
      their public exports. C# `namespace` is per-COMPILATION-UNIT
      with cross-file visibility — being in the same namespace makes
      the four sibling imports zero-using. The three
      `lib/analysis/type_checker/` Dart imports collapse to ONE C#
      `using Glp.Analysis.TypeChecker;` because they all live in the
      same .NET namespace (cross-file visibility). The `dart:io`
      import maps to `using System.IO;` per the
      `rf-dart-dart-io-to-csharp-system-io` cached finding from
      module_hierarchy.dart.md (this file uses `Directory`, `File`,
      `Platform.pathSeparator` — same surface as module_hierarchy).
      Unused-import nuance: `param_expansion.dart` is imported but
      no symbol from it is directly referenced in the file's body
      (`expandParameterizedTypes` is invoked by `_buildAncestorScope`
      via `assembleTypeScope`-like inlining — see the
      `_buildAncestorScope` construct below); the import remains for
      Dart compile-time export visibility. C# does not need an
      analogous import-for-visibility since the `Glp.Analysis.TypeChecker`
      using already covers it. No `as`-prefix idiom applies here
      (zero prefixed imports in this file, unlike
      module_hierarchy.dart's `as ast;`).

  - construct_key: dart.data_class.discovered_module_immutable_record_five_fields_with_named_required_and_named_default_ctor
    source_form: >-
      "class DiscoveredModule { final String filePath; final String
      moduleName; final Module ast; final TypeEnvironment ancestorScope;
      final bool isSelfGlp; DiscoveredModule({ required this.filePath,
      required this.moduleName, required this.ast, required
      this.ancestorScope, this.isSelfGlp = false, }); }" — a five-field
      immutable record with four named-required ctor parameters and
      one named-default (`isSelfGlp = false`).
    target_decision: >-
      Emit `public sealed class DiscoveredModule` (NOT `record class`
      — reference-identity required because the linker uses
      `identical(s, mod)` to skip the self-module in the ancestor walk;
      a `record` overrides `Equals`/`GetHashCode` by structural value
      and would break the identity-sensitive comparison — see the
      `identical(s, mod)` construct below). Five `public` get-only
      auto-properties: `string FilePath { get; }`, `string ModuleName
      { get; }`, `Ast.Module Ast { get; }`, `TypeEnvironment AncestorScope
      { get; }`, `bool IsSelfGlp { get; }`. Constructor:
      `public DiscoveredModule(string filePath, string moduleName,
      Ast.Module ast, TypeEnvironment ancestorScope, bool isSelfGlp = false)`
      with body assignments to each property. Dart named-required →
      C# positional with no default (carry-forward from
      `rf-dart-named-required-params-to-csharp-positional-params`,
      module_hierarchy.dart.md); the FIFTH parameter `isSelfGlp = false`
      → C# default-valued `bool isSelfGlp = false` (the Dart
      named-default form). The Dart `Module ast` field collides with
      the property name `Ast` — the C# render namespaces the type
      via `using Ast = Glp.Compiler.Ast;` and names the property
      `Module ast` would be ambiguous; rename the property to
      `ModuleAst` (the source-of-truth Dart field IS `ast`, but
      `ast` is also the C# namespace alias — collision avoidance is
      load-bearing). All four named-required call-sites at line 106-112
      use named-arg syntax preserved 1:1 (`new DiscoveredModule(filePath:
      ..., moduleName: ..., ast: ..., ancestorScope: ..., isSelfGlp:
      ...)`).
    idiom_id: null
    research_finding_id: rf-dart-named-required-and-default-params-to-csharp-positional-default
    nuance: >-
      Class-vs-record nuance (LOAD-BEARING, explicitly addressed):
      DiscoveredModule is a CLASS (not a record/struct) because the
      file's `linkProject` body uses `identical(s, mod)` to skip the
      currently-iterated module when scanning self.glp ancestors —
      this is REFERENCE equality. A `record` overrides `Equals` and
      `GetHashCode` by structural value, and `identical` (Dart) /
      `ReferenceEquals` (C#) is the load-bearing primitive that
      preserves the source's intent. Reference-identity is also
      required because the same `DiscoveredModule` instance is
      placed in the `modules` list AND referenced from
      `ancestorSelfProcs[mod.moduleName]` lookups via its
      `moduleName` string key — two-level indirection that depends
      on the instance being the same heap object. Five-field-immutable
      nuance: every field is `final` Dart → `get`-only C# property,
      no setters — the record-of-data nature is preserved. Named-
      required-and-default ctor nuance: this is the FIRST file in the
      convspec corpus to mix `required this.x` (four) with a default-
      valued `this.x = false` (one); the C# render is identical to
      the `rf-dart-named-required-and-default-params-to-csharp-positional-
      default` shape from ast.dart.md `Clause`/`UnderscoreTerm` ctor
      precedents. Property-name-vs-namespace-alias collision nuance
      (LOAD-BEARING): the Dart field `final Module ast` would render
      as `Ast.Module Ast` (property name == namespace alias) — the
      C# compiler permits this but it harms readability; the C# spec
      renames the property to `ModuleAst` (the property is consumed
      via `mod.ast.procedures` / `mod.ast.procDeclarations` at four
      call sites — the consumer expression `mod.ast.X` becomes
      `mod.ModuleAst.X`, a rename with zero behaviour change).
      Null-safety: every field is non-nullable Dart `String` /
      `Module` / `TypeEnvironment` / `bool` → C# non-nullable
      counterparts under enabled NRT. Async: ABSENT.

  - construct_key: dart.data_class.linkresult_two_field_immutable_record_with_positional_ctor
    source_form: >-
      "class LinkResult { final Program program; final List<ProcDecl>
      procDeclarations; LinkResult(this.program, this.procDeclarations); }"
      — a two-field immutable record with a POSITIONAL ctor (NO
      named-required wrapping, unlike `DiscoveredModule`).
    target_decision: >-
      Emit `public sealed class LinkResult` with two get-only auto-
      properties `public Program Program { get; }` and `public
      IReadOnlyList<ProcDecl> ProcDeclarations { get; }`. Constructor:
      `public LinkResult(Program program, IReadOnlyList<ProcDecl>
      procDeclarations) { Program = program; ProcDeclarations =
      procDeclarations; }`. Positional ctor on both sides — no named-
      arg collapsing. The single call-site at line 315 (`return
      LinkResult(Program(allProcedures, 0, 0), allDecls);`) becomes
      `return new LinkResult(new Program(allProcedures, 0, 0),
      allDecls);` — identical surface.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Two-field-immutable nuance: same shape as `Result` in
      result.dart.md and the Parser's `Tokens`+cursors split, just
      smaller. Class-vs-record nuance: a `record` WOULD be acceptable
      here (no `identical` is used on `LinkResult`) but the SPEC
      prefers `class` for cross-file consistency with the
      `DiscoveredModule` decision (both are AST-adjacent data
      carriers built by the linker; uniform shape eases review).
      `List<ProcDecl>` → `IReadOnlyList<ProcDecl>` because the caller
      (the compiler / codegen) only enumerates the list (read-only
      contract; carry-forward from parser.dart.md `IReadOnlyList<Token>`
      decision). The producer (this file) constructs it via
      `List<ProcDecl> allDecls = new List<ProcDecl>();` + `allDecls.Add(...)`
      then implicitly upcasts to `IReadOnlyList<ProcDecl>` at the
      return (semantics identical to Dart `List<ProcDecl>` → consumer-
      narrowed view).

  - construct_key: dart.top_level_function.named_optional_string_param_returns_list_dart_io_recursive_walk_with_filtered_skip_logic
    source_form: >-
      "List<DiscoveredModule> discoverProject(String rootDir,
      {String? rootSelfGlpPath}) { final root = Directory(rootDir); if
      (!root.existsSync()) { throw ArgumentError('Project root directory
      not found: $rootDir'); } final modules = <DiscoveredModule>[]; final
      glpFiles = root.listSync(recursive: true).whereType<File>()
      .where((f) => f.path.endsWith('.glp')).toList(); for (final file in
      glpFiles) { final filename = file.path.split(Platform.pathSeparator)
      .last; if (filename == 'boot_direct.glp') continue; if (filename ==
      'mad_boot.glp') continue; if (file.parent.path.endsWith(
      '${Platform.pathSeparator}mad_boot') || file.parent.path.endsWith(
      '/mad_boot')) continue; final source = file.readAsStringSync();
      final lexer = Lexer(source); final tokens = lexer.tokenize(); final
      parser = Parser(tokens); final module = parser.parseModule(); final
      moduleName = module.name ?? (filename == 'self.glp' ?
      _moduleNameFromDirPath(file.parent.path) : _moduleNameFromFilename(
      filename)); final chain = discoverSelfChain(targetFile: file.absolute
      .path, rootDir: root.absolute.path); final ancestorScope =
      _buildAncestorScope(chain, rootSelfGlpPath: rootSelfGlpPath); modules
      .add(DiscoveredModule(filePath: file.path, moduleName: moduleName,
      ast: module, ancestorScope: ancestorScope, isSelfGlp: filename ==
      'self.glp')); } return modules; }" — top-level function:
      ONE POSITIONAL `String rootDir` + ONE NAMED-OPTIONAL `String?
      rootSelfGlpPath`; throws `ArgumentError` (a `dart:core` built-in
      type) on missing directory; recursive filesystem walk via
      `Directory.listSync(recursive: true).whereType<File>()`; per-file
      exclusion logic for boot_direct.glp / mad_boot.glp / mad_boot/
      subdirectory (with BOTH `Platform.pathSeparator`-aware AND
      literal `/`-suffix checks); the Lexer → Parser → parseModule
      pipeline (carry-forward from module_hierarchy.dart.md); name
      derivation via `??` null-coalescing falling back to a ternary
      between `_moduleNameFromDirPath` and `_moduleNameFromFilename`.
    target_decision: >-
      Emit `public static IReadOnlyList<DiscoveredModule> DiscoverProject(
      string rootDir, string? rootSelfGlpPath = null)` on the hosting
      `public static class ProjectLinker`. The Dart named-optional
      `{String? rootSelfGlpPath}` (default-null) maps to a C# default-
      valued nullable-reference parameter `string? rootSelfGlpPath = null`
      (Dart named-optional and C# default-valued positional are
      semantically identical when the default is the null literal —
      and C# 4 named-argument syntax `DiscoverProject(rootDir: x,
      rootSelfGlpPath: y)` preserves the named-call shape). Body
      translation: `Directory(rootDir).existsSync()` → `Directory.Exists(
      rootDir)` (per `rf-dart-dart-io-to-csharp-system-io`); the throw
      maps Dart `ArgumentError('...')` → C# `throw new ArgumentException(
      $"Project root directory not found: {rootDir}");` — Microsoft
      Learn: "ArgumentException — The exception that is thrown when one
      of the arguments provided to a method is not valid." String
      interpolation `$'...$rootDir'` → `$"...{rootDir}"`. The
      recursive-walk-and-filter pipeline `root.listSync(recursive: true)
      .whereType<File>().where((f) => f.path.endsWith('.glp')).toList()`
      maps to `System.IO.Directory.EnumerateFiles(rootDir, "*.glp",
      SearchOption.AllDirectories).ToList()` — the .NET BCL
      `Directory.EnumerateFiles(path, searchPattern, SearchOption)`
      combines the recursive walk, the type filter (it natively yields
      only files, not directories), AND the `.glp` extension filter into
      a single call (Microsoft Learn: "Returns an enumerable collection
      of full file names that match a search pattern in a specified
      path, and optionally searches subdirectories" — `*.glp` is the
      pattern, `SearchOption.AllDirectories` is the recursion flag).
      For-each loop: `foreach (var filePath in glpFiles) { ... }` — note
      `Directory.EnumerateFiles` yields full path strings (NOT `FileInfo`
      objects), so the body translates `file.path` → `filePath` and
      `file.parent.path` → `Path.GetDirectoryName(filePath)` and
      `file.absolute.path` → `Path.GetFullPath(filePath)` (carry-forward
      from module_hierarchy.dart.md). `file.path.split(Platform.pathSeparator)
      .last` → `Path.GetFileName(filePath)` (cached idiom). Exclusion-
      logic conditionals translate statement-for-statement: each `if (...)
      continue;` becomes `if (...) continue;`. The dual-separator check
      `file.parent.path.endsWith('${Platform.pathSeparator}mad_boot') ||
      file.parent.path.endsWith('/mad_boot')` becomes `var parent =
      Path.GetDirectoryName(filePath); if (parent != null && (parent
      .EndsWith(Path.DirectorySeparatorChar + "mad_boot",
      StringComparison.Ordinal) || parent.EndsWith("/mad_boot",
      StringComparison.Ordinal))) continue;` — preserves BOTH the
      platform-separator-aware suffix AND the literal `'/mad_boot'`
      suffix (LOAD-BEARING — see nuance). `file.readAsStringSync()` →
      `File.ReadAllText(filePath)`. Lexer/Parser/parseModule pipeline
      identical to module_hierarchy.dart.md's translation. The `??`
      null-coalescing for module name maps to C# `??` 1:1: `var
      moduleName = module.Name ?? (filename == "self.glp" ?
      ModuleNameFromDirPath(parent!) : ModuleNameFromFilename(filename));`.
      Call-site `discoverSelfChain(targetFile: ..., rootDir: ...)` →
      `ModuleHierarchy.DiscoverSelfChain(targetFile: Path.GetFullPath(
      filePath), rootDir: Path.GetFullPath(rootDir))` (named-arg syntax
      preserved). `modules.Add(new DiscoveredModule(filePath: filePath,
      moduleName: moduleName, ast: module, ancestorScope: ancestorScope,
      isSelfGlp: filename == "self.glp"));`. Return `modules` as
      `IReadOnlyList<DiscoveredModule>`.
    idiom_id: null
    research_finding_id: rf-dart-directory-listsync-recursive-wheretype-file-to-csharp-directory-enumeratefiles-pattern
    nuance: >-
      Recursive-walk-and-filter nuance (LOAD-BEARING, NEW finding,
      explicitly addressed): Dart `Directory(rootDir).listSync(recursive:
      true)` returns `List<FileSystemEntity>` (mixed `File` /
      `Directory` / `Link`); `.whereType<File>()` is the Dart-extension-
      method type-narrowing filter (api.dart.dev: "Returns a new lazy
      Iterable with all elements that have type T"); `.where((f) =>
      f.path.endsWith('.glp'))` is the predicate filter on string suffix.
      The .NET counterpart `Directory.EnumerateFiles(path, "*.glp",
      SearchOption.AllDirectories)` collapses ALL THREE into a single
      BCL call AND adds a kernel-level optimisation (the OS filesystem
      driver does the pattern match — Dart's three-stage pipeline
      filters in user space). Semantics are byte-equivalent (both yield
      every `.glp` file in the subtree). The choice of
      `Directory.EnumerateFiles` over `Directory.GetFiles` is load-
      bearing: `EnumerateFiles` is LAZY (matches Dart's `.where().toList()`
      lazy-then-materialise discipline); `GetFiles` is EAGER (returns
      a fully-materialised `string[]` upfront, allocates more for large
      trees). Microsoft Learn: "Use EnumerateFiles to enumerate files
      lazily; use GetFiles to return all files at once." Sync-vs-async
      nuance: ABSENT — `listSync(recursive: true)` is synchronous, the
      .NET counterpart `Directory.EnumerateFiles` is likewise
      synchronous (its async sibling `Directory.EnumerateFilesAsync`
      requires .NET 8+ and is NOT used here — the SPEC preserves the
      synchronous discipline). Hidden-files / symlinks nuance: Dart
      `listSync(recursive: true)` follows symlinks by default and
      includes hidden files; .NET `EnumerateFiles` likewise includes
      hidden files and follows symlinks (under .NET 7+); behaviour
      matches. Exclusion-logic dual-separator nuance (LOAD-BEARING,
      explicitly addressed): the source check `endsWith('${Platform
      .pathSeparator}mad_boot') || endsWith('/mad_boot')` preserves
      BOTH the platform-native suffix AND the literal forward-slash
      suffix — this is a DEFENSIVE PROGRAMMING measure to catch
      paths that may have been normalised to forward-slashes regardless
      of platform (e.g. by a Dart standard-library path normaliser).
      The C# render preserves BOTH suffixes identically — switching to
      a single `Path.DirectorySeparatorChar` would lose the literal-
      `/` defence and change Windows behaviour. ArgumentError nuance:
      Dart `ArgumentError(message)` is the documented bad-input error
      (api.dart.dev: "Error thrown when a function is passed an
      unacceptable argument"); .NET `ArgumentException(message)` is
      the documented counterpart (Microsoft Learn). NOT
      `ArgumentNullException` (the rootDir is non-null; it's the
      filesystem state — directory-non-existent — that fails).
      Null-safety nuance: `rootSelfGlpPath` is nullable Dart `String?`;
      C# `string?` under enabled NRT. The `??` null-coalescing on
      `module.name` likewise round-trips 1:1 (Dart `??` ↔ C# `??`).
      Async: ABSENT throughout.

  - construct_key: dart.top_level_function.type_check_per_module_throws_exception_with_per_module_error_report
    source_form: >-
      "void typeCheckProject(List<DiscoveredModule> modules) { for (final
      mod in modules) { if (mod.ast.procDeclarations.isEmpty) continue;
      final hasOwnDecls = mod.ast.procDeclarations.any((d) => !d.imported);
      if (!hasOwnDecls) continue; final program = Program(mod.ast.procedures,
      mod.ast.line, mod.ast.column); final pe = PartialEvaluator(); final
      transformed = pe.transformDefinedGuards(program); final result =
      checkModule(mod.ast, transformedProcedures: transformed.procedures,
      ancestorScope: mod.ancestorScope); if (!result.isWellTyped) { final
      errors = result.errors.map((e) => '  ${e.message} at line ${e.line}')
      .join('\n'); throw Exception('Type checking failed for ${mod.moduleName}
      (${mod.filePath}):\n$errors'); } } }" — per-module type-check loop;
      two early-skip conditions (empty `procDeclarations`, all-imported
      decls); pipeline: Program ctor + PartialEvaluator.transformDefinedGuards
      + checkModule; error aggregation via `map(...).join('\n')`; throws
      a bare `Exception` (NOT a custom type) with module-name+path-formatted
      message.
    target_decision: >-
      Emit `public static void TypeCheckProject(IReadOnlyList<DiscoveredModule>
      modules)`. Body: `foreach (var mod in modules) { ... }`. Early
      skips: `if (mod.ModuleAst.ProcDeclarations.Count == 0) continue;`
      (Dart `Iterable.isEmpty` → C# `IReadOnlyCollection.Count == 0`
      OR LINQ `.Any() == false` — Microsoft Learn: prefer `.Count`
      when the collection exposes one because it is O(1) vs LINQ
      `.Any()` which iterates; the source `.procDeclarations` is a
      `List<ProcDecl>` per ast.dart.md so `.Count == 0` is the
      idiomatic match). `var hasOwnDecls = mod.ModuleAst.ProcDeclarations
      .Any(d => !d.Imported);` — Dart `.any((d) => !d.imported)` is
      semantically identical to LINQ `.Any(d => !d.Imported)`
      (Microsoft Learn: "Any — Determines whether any element of a
      sequence satisfies a condition"). `if (!hasOwnDecls) continue;`.
      Then: `var program = new Program(mod.ModuleAst.Procedures,
      mod.ModuleAst.Line, mod.ModuleAst.Column);` (per ast.dart.md
      Program ctor surface). `var pe = new PartialEvaluator();` (per
      partial_evaluator.dart.md ctor surface). `var transformed =
      pe.TransformDefinedGuards(program);`. `var result =
      TypeChecker.CheckModule(mod.ModuleAst, transformedProcedures:
      transformed.Procedures, ancestorScope: mod.AncestorScope);` —
      named-arg syntax preserved (per the carry-forward `rf-dart-
      named-required-params-to-csharp-positional-params` discipline);
      `checkModule` is a Dart top-level free function in the type-
      checker package → C# `public static` method on a hosting
      `TypeChecker` class (per module_hierarchy.dart.md precedent),
      reached as `TypeChecker.CheckModule(...)`. Error aggregation:
      `if (!result.IsWellTyped) { var errors = string.Join("\n",
      result.Errors.Select(e => $"  {e.Message} at line {e.Line}"));
      throw new Exception($"Type checking failed for {mod.ModuleName}
      ({mod.FilePath}):\n{errors}"); }` — LINQ `.Select(...)` is the
      .NET counterpart of Dart `.map(...)` (Microsoft Learn: "Select
      — Projects each element of a sequence into a new form");
      `string.Join` is the documented counterpart of Dart
      `Iterable<String>.join(separator)` (Microsoft Learn: "Concatenates
      the elements of a specified array or the members of a collection,
      using the specified separator between each element"). The bare
      Dart `Exception(message)` → C# bare `Exception(message)` (NOT a
      domain-specific type — the source uses the generic `Exception`
      class; the C# render uses the generic `System.Exception` —
      faithful to the source's intent NOT to introduce a typed
      exception hierarchy).
    idiom_id: null
    research_finding_id: rf-dart-iterable-any-map-join-to-csharp-linq-any-select-string-join
    nuance: >-
      Iterable-pipeline nuance (LOAD-BEARING, NEW finding, explicitly
      addressed): three distinct Dart `Iterable` extension methods are
      composed in this body — `.any((d) => ...)` for the existence
      check, `.map((e) => ...)` for the per-element transform, and
      `.join('\n')` for the string concatenation. Each maps to a
      distinct .NET surface: (a) `.any(p)` → LINQ `.Any(p)`
      (`System.Linq.Enumerable.Any<T>(IEnumerable<T>, Func<T, bool>)`,
      Microsoft Learn: "Determines whether any element of a sequence
      satisfies a condition"); (b) `.map(f)` → LINQ `.Select(f)`
      (`System.Linq.Enumerable.Select<TSource, TResult>(IEnumerable<TSource>,
      Func<TSource, TResult>)`, Microsoft Learn: "Projects each element
      of a sequence into a new form"); (c) `.join(separator)` → `string
      .Join(separator, source)` (`System.String.Join<T>(string,
      IEnumerable<T>)`, Microsoft Learn). The combined pipeline
      `result.errors.map((e) => '  ${e.message} at line ${e.line}')
      .join('\n')` becomes `string.Join("\n", result.Errors.Select(e =>
      $"  {e.Message} at line {e.Line}"))` — note the .NET render
      INVERTS the operator order (`string.Join` takes the separator
      FIRST, then the sequence) but the resulting string is byte-
      identical. Empty-check nuance: Dart `Iterable.isEmpty` → C#
      `IReadOnlyCollection.Count == 0` (O(1)) — NOT `!source.Any()`
      (forces an iteration, slower for large collections). Exception-
      type nuance: the source uses the BARE `Exception` class — NOT
      a custom domain type. C# render uses the bare `System.Exception`
      — preserves the source's intent. (A future refactor MIGHT
      introduce a typed `ProjectLinkerTypeCheckException` for richer
      catch semantics, but the SPEC documents the source's actual
      choice.) String-interpolation nuance: Dart `'${mod.moduleName}'`
      with member access inside `${...}` → C# `$"{mod.ModuleName}"`
      (Microsoft Learn). Async: ABSENT (PartialEvaluator, TypeChecker
      are synchronous transforms).

  - construct_key: dart.top_level_function.linker_two_phase_rename_registry_build_then_per_module_clause_rewrite_with_inner_helpers
    source_form: >-
      Lines 158-316: "LinkResult linkProject(List<DiscoveredModule>
      modules, String topModuleName)" — the central linker. Five
      consecutive map-comprehension phases: (1) build `registry`
      `<String, Set<String>>{}` mapping moduleName → set of `name/arity`
      sigs; (2) build `ancestorSelfProcs` `<String, Map<String, String>>{}`
      mapping each module's name → (sig → ancestorModuleName), with
      inner-most-self.glp-wins via `putIfAbsent` and a path-length-
      descending sort; (3) per-module clause rewrite — for each
      `Procedure`, generate a `renamedName` `'${mod.moduleName}:${proc.name}'`,
      iterate each `Clause`, build a renamed `Atom` for the head, walk
      every body `Goal` through `_resolveGoal`, and emit a new `Clause(
      renamedHead, guards: clause.guards, body: resolvedBody, line:
      clause.line, column: clause.column)`; (4) build `declIndex`
      `<String, ProcDecl>{}` project-wide (first-non-imported wins);
      (5) generate entry-point aliases — top module gets aliases for
      ALL procedures, other modules for only EXPORTED ones (checked
      via `procDeclarations.any((d) => d.exported && d.name == proc.name
      && d.arity == proc.arity)`); a `aliasedSigs` `<String, String>{}`
      tracks alias ownership to skip duplicates (top module wins);
      finally collect renamed proc declarations for SRSW relaxation.
    target_decision: >-
      Emit `public static LinkResult LinkProject(IReadOnlyList<DiscoveredModule>
      modules, string topModuleName)` on the hosting static class.
      All FIVE phases translate statement-for-statement using the
      `rf-dart-map-literal-to-csharp-dictionary-and-keys-toset-to-
      hashset` cached idiom from module_hierarchy.dart.md. Phase (1):
      `var registry = new Dictionary<string, HashSet<string>>();
      foreach (var mod in modules) { var sigs = new HashSet<string>();
      foreach (var proc in mod.ModuleAst.Procedures) sigs.Add($"{proc
      .Name}/{proc.Arity}"); registry[mod.ModuleName] = sigs; }` — Dart
      `<String, Set<String>>{}` map literal + `Set<String>{}` set
      literal → C# `Dictionary<string, HashSet<string>>` +
      `HashSet<string>`. Phase (2): `var selfGlpModules = modules.Where(
      m => m.IsSelfGlp).ToList(); var ancestorSelfProcs = new Dictionary<
      string, Dictionary<string, string>>(); foreach (var mod in modules)
      { var modDir = Path.GetDirectoryName(Path.GetFullPath(mod.FilePath))
      ?? string.Empty; var procs = new Dictionary<string, string>(); var
      ancestors = selfGlpModules.Where(s => !ReferenceEquals(s, mod) &&
      modDir.StartsWith(Path.GetDirectoryName(Path.GetFullPath(s.FilePath))
      ?? string.Empty, StringComparison.Ordinal)).OrderByDescending(s =>
      s.FilePath.Length).ToList(); foreach (var selfMod in ancestors)
      foreach (var proc in selfMod.ModuleAst.Procedures) { var sig =
      $"{proc.Name}/{proc.Arity}"; if (!procs.ContainsKey(sig)) procs[sig]
      = selfMod.ModuleName; } ancestorSelfProcs[mod.ModuleName] = procs;
      }` — three crucial idioms here: (a) `identical(s, mod)` → C#
      `ReferenceEquals(s, mod)` (Microsoft Learn: "Determines whether
      the specified Object instances are the same instance"); (b)
      `..sort((a,b) => b.filePath.length.compareTo(a.filePath.length))`
      → LINQ `.OrderByDescending(s => s.FilePath.Length).ToList()`
      (Microsoft Learn: "Sorts the elements of a sequence in descending
      order"); (c) `putIfAbsent(sig, () => selfMod.moduleName)` → `if
      (!procs.ContainsKey(sig)) procs[sig] = selfMod.ModuleName;`
      (LOAD-BEARING — see nuance: NOT `procs[sig] = ...` which would
      OVERWRITE, breaking inner-most-wins). Phase (3): the central
      per-module clause rewrite. `var allProcedures = new List<Procedure>();
      foreach (var mod in modules) { var localSigs = registry[mod
      .ModuleName]; var modAncestorProcs = ancestorSelfProcs.TryGetValue(
      mod.ModuleName, out var aps) ? aps : new Dictionary<string,
      string>(); foreach (var proc in mod.ModuleAst.Procedures) { var
      renamedName = $"{mod.ModuleName}:{proc.Name}"; var renamedClauses =
      new List<Clause>(); foreach (var clause in proc.Clauses) { var
      renamedHead = new Atom($"{mod.ModuleName}:{clause.Head.Functor}",
      clause.Head.Args, clause.Head.Line, clause.Head.Column); IReadOnlyList<Goal>?
      resolvedBody = clause.Body?.Select(g => ResolveGoal(g, mod.ModuleName,
      localSigs, modAncestorProcs)).ToList(); renamedClauses.Add(new
      Clause(renamedHead, guards: clause.Guards, body: resolvedBody, line:
      clause.Line, column: clause.Column)); } allProcedures.Add(new
      Procedure(renamedName, proc.Arity, renamedClauses, proc.Line,
      proc.Column)); } }` — Dart `?.map(...)?.toList()` chain (note the
      null-aware `?.` on `clause.body`) → C# `clause.Body?.Select(g =>
      ResolveGoal(...)).ToList()` (the `?.` is the null-conditional
      operator on both sides — Microsoft Learn: "The null-conditional
      operator ?. and ?[] return null if the left operand is null, and
      otherwise apply the member access or index"). Phase (4):
      `var declIndex = new Dictionary<string, ProcDecl>(); foreach (var
      mod in modules) foreach (var d in mod.ModuleAst.ProcDeclarations)
      { if (d.Imported) continue; var sig = $"{d.Name}/{d.Arity}"; if
      (!declIndex.ContainsKey(sig)) declIndex[sig] = d; }`. Phase (5):
      `var aliasedSigs = new Dictionary<string, string>(); foreach (var
      mod in modules) { var isTop = mod.ModuleName == topModuleName;
      foreach (var proc in mod.ModuleAst.Procedures) { var sig =
      $"{proc.Name}/{proc.Arity}"; if (!isTop) { var isExported = mod
      .ModuleAst.ProcDeclarations.Any(d => d.Exported && d.Name == proc
      .Name && d.Arity == proc.Arity); if (!isExported) continue; } if
      (aliasedSigs.ContainsKey(sig)) continue; aliasedSigs[sig] = mod
      .ModuleName; var decl = FindProcDecl(mod, proc.Name, proc.Arity)
      ?? (declIndex.TryGetValue(sig, out var d2) ? d2 : null); var
      aliasClause = MakeAliasClause(proc.Name, proc.Arity, $"{mod
      .ModuleName}:{proc.Name}", declaration: decl); allProcedures.Add(
      new Procedure(proc.Name, proc.Arity, new List<Clause> { aliasClause
      }, 0, 0)); } }`. Final phase: `var allDecls = new List<ProcDecl>();
      foreach (var mod in modules) foreach (var decl in mod.ModuleAst
      .ProcDeclarations) { if (decl.Imported) continue; allDecls.Add(new
      ProcDecl($"{mod.ModuleName}:{decl.Name}", decl.ArgTypes, decl.Line,
      decl.Column, isBuiltin: decl.IsBuiltin)); } return new LinkResult(
      new Program(allProcedures, 0, 0), allDecls);`.
    idiom_id: null
    research_finding_id: rf-dart-map-putifabsent-lambda-to-csharp-containskey-then-assign
    nuance: >-
      `putIfAbsent`-vs-`[k]=v` nuance (LOAD-BEARING, NEW finding,
      explicitly addressed): Dart `Map.putIfAbsent(key, () => value)`
      is the documented INSERT-ONLY-IF-ABSENT primitive (api.dart.dev:
      "Look up the value of key, or add a new entry if it isn't there.
      Returns the value associated to key, if there is one. Otherwise
      calls ifAbsent to get a new value, associates key to that value,
      and then returns the new value.") — the LAMBDA argument is
      crucial because it implements LAZY default-value construction
      (the default is only computed if the key is absent). The
      ALTERNATIVE Dart `[k] = v` always overwrites — which would
      BREAK the inner-most-self.glp-wins discipline (the linker walks
      ancestors from inner-most to outer-most; the FIRST `putIfAbsent`
      wins, every subsequent attempt is a no-op). The .NET counterpart
      is `if (!dict.ContainsKey(key)) dict[key] = value;` (two
      operations, but functionally identical) OR
      `dict.TryAdd(key, value)` (a single atomic operation that
      returns `bool` — Microsoft Learn .NET 6+: "Attempts to add the
      specified key and value to the dictionary"). The SPEC PREFERS
      `TryAdd` for the .NET 6+ baseline (single-call, atomic, returns
      `bool` for chaining if needed) but accepts `ContainsKey+[]=` for
      .NET Framework 4.x compatibility — both produce byte-equivalent
      results for the inner-most-wins discipline. The LAZY-default
      aspect of Dart's lambda IS preserved by `ContainsKey+[]=` because
      the C# right-hand side is only evaluated when the key is
      absent (the `if` short-circuits). LINQ-OrderByDescending nuance
      (explicitly addressed): Dart `..sort((a,b) => b.X.compareTo(a.X))`
      is the documented descending-sort idiom (the cascade `..` mutates
      the list in place and returns the same list — api.dart.dev:
      "Cascade notation"); LINQ `.OrderByDescending(s => s.X)` returns
      a NEW `IOrderedEnumerable<T>` (immutable view, must be materialised
      via `.ToList()`). Semantics match — both produce a descending-
      ordered iteration. The mutation-vs-new-iterable distinction
      matters only if a later step depends on the source list's order
      being mutated (it does NOT here — the sorted result is consumed
      immediately by the inner `foreach`). Null-conditional + null-
      coalescing on body rewrite nuance (LOAD-BEARING, explicitly
      addressed): Dart `clause.body?.map((g) => ...).toList()` chains
      the null-conditional `?.` on the `clause.body` field (nullable
      `List<Goal>?` per ast.dart.md) AND continues with `.map(...).
      toList()` only when non-null; C# `clause.Body?.Select(g => ...)
      .ToList()` is the byte-equivalent (the `?.` short-circuits to
      `null` when `Body` is null; the entire LINQ pipeline becomes
      a no-op). Type-annotation nuance: the assignment site is `final
      resolvedBody = clause.body?...;` — Dart infers `List<Goal>?`;
      C# declares `IReadOnlyList<Goal>? resolvedBody = ...` explicitly
      because the `?.Select(...).ToList()` result is `List<Goal>?`
      and the Clause ctor takes `IReadOnlyList<Goal>?` (implicit
      narrowing). Pass-through-args nuance (LOAD-BEARING): `clause.head
      .args` is forwarded UNCHANGED into the renamed Atom — the args
      list is REUSED (same reference, no copy); this is preserved in
      C# by passing `clause.Head.Args` directly into the new Atom ctor
      (no `.ToList()` or `.ToArray()` copy). The renamed Clause
      structurally preserves the original's guards (`clause.Guards`
      passed through) — same reference reuse. Reference-sharing-and-
      identity nuance: matches the ast.dart.md identity-preservation
      discipline — sub-trees are aliased across the rewrite, only the
      enclosing nodes (Atom head, Clause, Procedure) are newly
      allocated. Iterable.Any-with-double-predicate nuance: `.any((d)
      => d.exported && d.name == proc.name && d.arity == proc.arity)`
      → LINQ `.Any(d => d.Exported && d.Name == proc.Name && d.Arity ==
      proc.Arity)` — predicate composition is identical (short-circuit
      `&&` semantics in both languages). Single-clause-procedure-list
      nuance: the alias `Procedure` ctor receives a `new List<Clause>
      { aliasClause }` (single-element list literal) — Dart `[aliasClause]`
      ↔ C# `new List<Clause> { aliasClause }` (collection initialiser,
      Microsoft Learn). Async: ABSENT throughout.

  - construct_key: dart.private_recursive_helper.resolve_goal_runtime_type_dispatch_with_identity_preservation_on_noop
    source_form: >-
      "Goal _resolveGoal(Goal goal, String moduleName, Set<String>
      localSigs, Map<String, String> ancestorSelfProcs) { if (goal is
      RemoteGoal) { final targetModule = goal.staticModuleName; if
      (targetModule != null) { return Goal('$targetModule:${goal.goal
      .functor}', goal.goal.args, goal.line, goal.column); } return
      goal; } if (goal is SpawnGoal) { final resolvedInner = _resolveGoal(
      goal.innerGoal, moduleName, localSigs, ancestorSelfProcs); if
      (!identical(resolvedInner, goal.innerGoal)) { return SpawnGoal(
      resolvedInner, goal.agentId, goal.line, goal.column); } return
      goal; } final sig = '${goal.functor}/${goal.arity}'; if (localSigs
      .contains(sig)) { return Goal('$moduleName:${goal.functor}', goal
      .args, goal.line, goal.column); } final ancestorModule =
      ancestorSelfProcs[sig]; if (ancestorModule != null) { return
      Goal('$ancestorModule:${goal.functor}', goal.args, goal.line,
      goal.column); } return goal; }" — three-branch runtime-type
      dispatch (`is RemoteGoal`, `is SpawnGoal`, else regular Goal);
      identity-preserving on no-op (returns the SAME `goal` when no
      rewrite applies); recursive on `SpawnGoal.innerGoal`; constructs
      a NEW Goal/SpawnGoal only when rewrite occurs.
    target_decision: >-
      Emit `private static Goal ResolveGoal(Goal goal, string moduleName,
      IReadOnlyCollection<string> localSigs, IReadOnlyDictionary<string,
      string> ancestorSelfProcs)` on the hosting static class. Three-
      branch translation using C# DECLARATION-PATTERN switch (Microsoft
      Learn: "Pattern matching with the is expression — A declaration
      pattern with type T matches an expression when an expression's
      result is non-null and any of the following conditions are
      true: The runtime type of expr is T."). `if (goal is RemoteGoal
      rg) { var targetModule = rg.StaticModuleName; if (targetModule
      != null) return new Goal($"{targetModule}:{rg.GoalRef.Functor}",
      rg.GoalRef.Args, rg.Line, rg.Column); return goal; }` — note
      the `is RemoteGoal rg` syntax extracts the typed reference in
      a single expression (C# 7+). The `goal.goal.functor` Dart
      attribute (where the `RemoteGoal` has a field also named `goal`)
      is renamed to `GoalRef` in C# to avoid the property-shadowing-
      base-method collision (the base `Goal` class is the type, and
      `RemoteGoal` having a property named `Goal` would collide with
      the base type name in C# — see ast.dart.md `RemoteGoal` spec
      for the property-rename rationale). `if (goal is SpawnGoal sg)
      { var resolvedInner = ResolveGoal(sg.InnerGoal, moduleName,
      localSigs, ancestorSelfProcs); if (!ReferenceEquals(resolvedInner,
      sg.InnerGoal)) return new SpawnGoal(resolvedInner, sg.AgentId,
      sg.Line, sg.Column); return goal; }`. The final no-RemoteGoal-
      no-SpawnGoal arm: `var sig = $"{goal.Functor}/{goal.Arity}"; if
      (localSigs.Contains(sig)) return new Goal($"{moduleName}:{goal
      .Functor}", goal.Args, goal.Line, goal.Column); if (ancestorSelfProcs
      .TryGetValue(sig, out var ancestorModule)) return new Goal(
      $"{ancestorModule}:{goal.Functor}", goal.Args, goal.Line, goal
      .Column); return goal;`. Note: `Map<K,V>` value lookup returning
      `V?` (null when absent) in Dart → C# `TryGetValue(key, out value)`
      pattern (the documented .NET equivalent, Microsoft Learn).
    idiom_id: null
    research_finding_id: rf-dart-is-pattern-with-typed-binding-and-identical-noop-to-csharp-declaration-pattern-with-referenceequals
    nuance: >-
      Runtime-type-dispatch nuance (LOAD-BEARING, NEW finding,
      explicitly addressed): Dart `goal is RemoteGoal` is a runtime
      type check that PROMOTES the `goal` variable's static type to
      `RemoteGoal` within the `if`-block scope (Dart language tour:
      "If you check the type using is, the analyzer narrows the type
      of the variable in the corresponding code block"). C# 7+
      DECLARATION PATTERN `goal is RemoteGoal rg` performs the type
      check AND introduces a new typed variable `rg` of type
      `RemoteGoal` in a single expression (Microsoft Learn:
      "Declaration and type patterns — The declaration pattern is
      useful in the is expression. It introduces a new variable that
      contains the value of the expression."). Both languages: the
      typed variable is accessible only within the `if`-block scope.
      Identity-preserving-no-op nuance (LOAD-BEARING, explicitly
      addressed): the source uses `identical(resolvedInner, goal
      .innerGoal)` — Dart's REFERENCE-equality primitive (api.dart.dev:
      "Check whether two references are to the same object"). C#
      counterpart is `object.ReferenceEquals(a, b)` (Microsoft Learn:
      "Determines whether the specified Object instances are the same
      instance"). Semantically IDENTICAL — both return `true` iff the
      two references point to the same heap object. The choice of
      identity-vs-equality is load-bearing because the `_resolveGoal`
      recursion uses identity as a CHANGE DETECTION mechanism — if
      `resolvedInner` IS the same object as `goal.innerGoal` (no
      rewrite occurred deeper), the outer `SpawnGoal` is REUSED
      verbatim (no new allocation, preserves identity across the
      whole sub-tree). Switching to `==` (value equality) would BREAK
      this optimisation AND potentially cause re-allocation
      cascades. Recursion nuance: the function is recursive only via
      the SpawnGoal arm — every other arm is non-recursive. The
      recursion depth is bounded by the SpawnGoal-wrapping depth in
      the source AST (typically 0-2 for production GLP, never
      unbounded). No stack-overflow concern for realistic inputs;
      no need to convert to an explicit stack. Map-value-lookup-on-
      null-absent nuance (explicitly addressed): Dart `Map<K,V>[k]`
      returns `V?` (nullable — `null` when key absent); the source
      uses `ancestorSelfProcs[sig]` then null-checks via `!= null`.
      C# `IDictionary<K,V>.this[key]` THROWS `KeyNotFoundException`
      when the key is absent — the SPEC uses `TryGetValue(key, out
      value)` (Microsoft Learn: "Gets the value associated with the
      specified key. … TryGetValue is useful in scenarios where the
      key is not necessarily present"). Functional equivalent of
      Dart's null-returning lookup. Goal-vs-RemoteGoal-vs-SpawnGoal
      hierarchy nuance: per ast.dart.md, `Goal` is the concrete
      non-abstract base; `RemoteGoal` and `SpawnGoal` extend it as
      sealed sub-leaves. C# `is`-pattern dispatch correctly handles
      this open base + sealed-leaf shape — the final `return goal;`
      catches every base-Goal instance that did not match either
      subclass pattern. Inner-goal-recursion-preserves-identity
      nuance: ONLY a new `SpawnGoal` is allocated if the inner Goal
      itself was rewritten — preserving sub-tree identity through
      no-op rewrites (this is a memory and equality-preservation
      optimisation; if the codegen stage hashes Goals or uses
      identity-keyed side-tables, this is essential).

  - construct_key: dart.private_helper.find_proc_decl_linear_scan_with_skip_imported_returns_nullable
    source_form: >-
      "ProcDecl? _findProcDecl(DiscoveredModule mod, String name, int
      arity) { for (final d in mod.ast.procDeclarations) { if (!d
      .imported && d.name == name && d.arity == arity) return d; }
      return null; }" — linear scan with two negative filters returning
      the first match or null.
    target_decision: >-
      Emit `private static ProcDecl? FindProcDecl(DiscoveredModule mod,
      string name, int arity) { foreach (var d in mod.ModuleAst
      .ProcDeclarations) { if (!d.Imported && d.Name == name && d
      .Arity == arity) return d; } return null; }`. Direct statement-
      for-statement translation. Could also be expressed as LINQ
      `.FirstOrDefault(d => !d.Imported && d.Name == name && d.Arity
      == arity)` (Microsoft Learn: "Returns the first element of a
      sequence that satisfies a condition or a default value if no
      such element is found") — the SPEC PREFERS the foreach form
      for byte-level fidelity with the Dart source's explicit
      iteration AND because `FirstOrDefault` on a value-type-default
      sequence returns `default(T)` which for non-nullable value types
      is `0` not `null`; here `ProcDecl` is a reference type so
      `FirstOrDefault` would return `null` correctly, but the
      foreach is more readable and direct. Either form is acceptable
      to the codegen stage.
    idiom_id: null
    research_finding_id: rf-dart-linear-scan-first-match-or-null-to-csharp-foreach-or-firstordefault
    nuance: >-
      Linear-scan-or-null nuance (explicitly addressed): both
      languages support TWO idiomatic shapes — explicit `foreach`
      with early `return` (preserves source order and is easy to
      step through in a debugger) OR functional `Iterable.firstWhere
      ((d) => ..., orElse: () => null)` / `LINQ.FirstOrDefault((d) =>
      ...)`. The Dart SOURCE uses the explicit `foreach`-with-return
      form; the C# RENDER preserves that form for fidelity (LINQ
      `FirstOrDefault` is an acceptable refactor that the codegen
      stage MAY choose, but the SPEC documents the source's actual
      choice). Nullable-return nuance: the return type `ProcDecl?` in
      Dart is nullable-reference; C# `ProcDecl?` under enabled NRT is
      identical. The single caller (`_findProcDecl(mod, proc.name,
      proc.arity) ?? declIndex[sig]`) chains a fallback via `??` —
      preserved 1:1 (`FindProcDecl(...) ?? (declIndex.TryGetValue(sig,
      out var d2) ? d2 : null)` — see the `linkProject` body translation
      above; the .NET version expands `declIndex[sig]` into
      `TryGetValue` because direct C# Dictionary indexing throws on
      absent key whereas Dart Map indexing returns null).

  - construct_key: dart.private_helper.make_alias_clause_mode_aware_argument_forwarding_with_zero_arity_fast_path_and_var_term_generate
    source_form: >-
      "Clause _makeAliasClause(String name, int arity, String targetName,
      {ProcDecl? declaration}) { if (arity == 0) { final head = Atom(
      name, [], 0, 0); final body = [Goal(targetName, [], 0, 0)]; return
      Clause(head, body: body, line: 0, column: 0); } final headArgs =
      List.generate(arity, (i) => VarTerm('V$i', false, 0, 0) as Term);
      final bodyArgs = List.generate(arity, (i) { final isInput =
      declaration != null && i < declaration.argTypes.length ?
      declaration.isInputArg(i) : true; return VarTerm('V$i', isInput,
      0, 0) as Term; }); final head = Atom(name, headArgs, 0, 0); final
      body = [Goal(targetName, bodyArgs, 0, 0)]; return Clause(head,
      body: body, line: 0, column: 0); }" — generates the mode-aware
      alias clause `p(V0,V1,V2) :- M:p(V0?,V1,V2)` where head args
      are uniformly NON-reader VarTerms (`isReader: false`) and body
      args are per-arg reader-or-writer based on the declaration's
      `isInputArg(i)` predicate. Zero-arity fast path skips the
      List.generate calls.
    target_decision: >-
      Emit `private static Clause MakeAliasClause(string name, int
      arity, string targetName, ProcDecl? declaration = null)`. Body:
      `if (arity == 0) { var head0 = new Atom(name, Array.Empty<Term>(),
      0, 0); var body0 = new List<Goal> { new Goal(targetName, Array
      .Empty<Term>(), 0, 0) }; return new Clause(head0, body: body0,
      line: 0, column: 0); }` — Dart empty-list-literal `[]` typed as
      `List<Term>` / `List<Goal>` → C# `Array.Empty<Term>()` /
      `new List<Goal> { ... }` (Microsoft Learn `Array.Empty<T>` —
      "Returns an empty array. … Use the Empty method to avoid
      unnecessary memory allocation when an empty array is needed
      and the empty array can be reused"; cached idiom from
      `rf-dart-const-empty-list-default-to-csharp-array-empty`
      in ast.dart.md). The N-arity branch: `var headArgs = new
      Term[arity]; for (int i = 0; i < arity; i++) headArgs[i] = new
      VarTerm($"V{i}", false, 0, 0); var bodyArgs = new Term[arity];
      for (int i = 0; i < arity; i++) { var isInput = declaration !=
      null && i < declaration.ArgTypes.Count ? declaration.IsInputArg(i)
      : true; bodyArgs[i] = new VarTerm($"V{i}", isInput, 0, 0); } var
      head = new Atom(name, headArgs, 0, 0); var body = new List<Goal>
      { new Goal(targetName, bodyArgs, 0, 0) }; return new Clause(head,
      body: body, line: 0, column: 0);` — preserves the per-index
      ternary on `declaration` AND the per-index `IsInputArg(i)`
      lookup. The Dart `List.generate(arity, (i) => ...)` factory
      maps to a `Term[arity]` array + index-assignment loop (or
      equivalently `Enumerable.Range(0, arity).Select(i => (Term)new
      VarTerm($"V{i}", false, 0, 0)).ToArray()` — Microsoft Learn:
      "Enumerable.Range — Generates a sequence of integral numbers
      within a specified range" + LINQ `.Select`); the SPEC PREFERS
      the explicit `for` loop for byte-level fidelity AND because
      the loop body for `bodyArgs` has a non-trivial per-index
      ternary that is more readable as a statement than as a lambda.
      The Dart `as Term` cast on `VarTerm(...)` is ELIDED in C#
      because the array type is `Term[]` and `VarTerm : Term` —
      implicit upcast (Microsoft Learn: "Implicit reference
      conversions"). The Dart named-optional `{ProcDecl? declaration}`
      → C# default-valued nullable-reference parameter `ProcDecl?
      declaration = null` (carry-forward from
      `rf-dart-named-required-and-default-params-to-csharp-positional-
      default`).
    idiom_id: null
    research_finding_id: rf-dart-list-generate-with-as-term-cast-to-csharp-array-loop-or-enumerable-range-select
    nuance: >-
      List.generate-with-cast nuance (LOAD-BEARING, NEW finding,
      explicitly addressed): Dart `List.generate(arity, (i) => VarTerm
      (...) as Term)` builds a `List<Term>` by calling the lambda
      `arity` times; the trailing `as Term` cast is required by Dart's
      type inference because the lambda's return type is inferred
      from the FIRST invocation's runtime type (`VarTerm`, not
      `Term`), and without the cast the list would be typed as
      `List<VarTerm>` (api.dart.dev: "Type promotion of variables
      within a list literal"). The `as Term` upcast forces the
      element type to the base. C# `Term[arity]` array has the
      element type DECLARED upfront — no per-element cast is needed
      because `VarTerm : Term` so assignment is an implicit upcast.
      This is a Dart-specific syntactic elaboration that the C# render
      RESOLVES at the API level (the array declaration carries the
      base type; the assignment carries the derived type — implicit
      conversion). Alternative LINQ render `Enumerable.Range(0, arity)
      .Select(i => (Term)new VarTerm($"V{i}", false, 0, 0)).ToArray()`
      is also valid (and more functional-style) but the spec prefers
      the explicit `for` loop for: (a) byte-level fidelity with the
      Dart source's per-element behaviour; (b) the per-index ternary
      on `isInput` is naturally a statement, not a lambda; (c) array
      pre-allocation is faster than LINQ materialisation for hot
      paths (the alias clauses are emitted once per export, not
      hot-path, but consistency with other arity-driven code in
      the conversion family). Mode-aware-arg nuance (LOAD-BEARING,
      explicitly addressed): the per-index `isInput` ternary reads
      `declaration.isInputArg(i)` which is a `ProcDecl` method (per
      ast.dart.md `ProcDecl` spec) returning a `bool` per-arg mode
      indicator. The semantics are: declared input args (`T?` in
      the procedure declaration syntax) get reader annotation
      (`isReader: true`) on the body side — they FORWARD the value
      as a reader for the callee; declared output args (bare `T`)
      get writer annotation (`isReader: false`) — they FORWARD the
      writer slot for the callee to write to. This is GLP's SRSW
      (Single-Reader/Single-Writer) discipline embodied in the alias
      clause generation. The C# render preserves the EXACT
      `isReader: false/true` semantics by constructing `VarTerm`s
      with the per-index boolean (per ast.dart.md `VarTerm` ctor
      surface). Zero-arity-fast-path nuance: the `if (arity == 0)`
      branch avoids the `List.generate` overhead for nullary procs.
      C# render preserves the fast path with `Array.Empty<Term>()`
      (allocation-free shared instance) — strictly faster than `new
      Term[0]` (Microsoft Learn). VarTerm-ctor nuance: per ast.dart.md
      VarTerm ctor `VarTerm(name, isReader, line, column)`. C# render
      `new VarTerm($"V{i}", false, 0, 0)` — positional, identical
      surface. String-interpolation nuance: Dart `'V$i'` → C# `$"V{i}"`
      (cached, all prior specs). Async: ABSENT.

  - construct_key: dart.private_helper.module_name_from_filename_strip_dot_glp_extension
    source_form: >-
      "String _moduleNameFromFilename(String filename) { if (filename
      .endsWith('.glp')) { return filename.substring(0, filename.length
      - 4); } return filename; }" — strip the `.glp` extension if
      present; otherwise return the filename unchanged.
    target_decision: >-
      Emit `private static string ModuleNameFromFilename(string filename)
      { if (filename.EndsWith(".glp", StringComparison.Ordinal)) {
      return filename[..^4]; } return filename; }`. Dart `.endsWith
      ('.glp')` → C# `.EndsWith(".glp", StringComparison.Ordinal)`
      for culture-independent ordinal comparison (carry-forward of
      the string-comparison discipline from module_hierarchy.dart.md).
      Dart `.substring(0, length - 4)` → C# range-indexer `s[..^4]`
      (C# 8+, Microsoft Learn: "Ranges and indices — A range x..y is
      a half-open range; ^N is N from the end").
    idiom_id: null
    research_finding_id: rf-dart-string-substring-zero-length-minus-n-to-csharp-range-indexer
    nuance: >-
      String-substring nuance (cached, explicitly addressed): Dart
      `String.substring(start, end)` is the documented zero-based
      half-open substring (api.dart.dev: "Returns the substring of
      this string that extends from startIndex, inclusive, to
      endIndex, exclusive"); C# `string[..^N]` is the documented
      range-indexer (Microsoft Learn: "A range x..y is a half-open
      range … ^N is N from the end"). Both are zero-allocation in
      principle (both languages may intern or reuse the substring
      buffer; the SPEC does not depend on allocation behaviour, only
      on observable byte content). String-comparison-ordinality
      nuance: cached from module_hierarchy.dart.md — Dart
      `.endsWith(other)` defaults to ordinal byte comparison; C#
      `.EndsWith(other)` overload WITHOUT a StringComparison defaults
      to current-culture (Microsoft Learn: "By default, the
      comparison is performed by using the current culture. If you
      want to perform a culture-independent comparison, pass
      StringComparison.Ordinal"). The SPEC mandates the
      `StringComparison.Ordinal` overload to preserve Dart's
      culture-independent semantics. Null-safety: filename is non-
      nullable Dart `String` → C# `string`. Async: ABSENT.

  - construct_key: dart.private_helper.module_name_from_dir_path_split_separator_take_last
    source_form: >-
      "String _moduleNameFromDirPath(String dirPath) { final parts =
      dirPath.split(Platform.pathSeparator); return parts.last; }" —
      split the directory path on the platform separator, return the
      last component.
    target_decision: >-
      Emit `private static string ModuleNameFromDirPath(string dirPath)
      => Path.GetFileName(dirPath);`. Cached idiom: Dart `path.split
      (Platform.pathSeparator).last` → .NET `Path.GetFileName(path)`
      (the BCL counterpart that strips the directory portion regardless
      of platform — Microsoft Learn: "Returns the file name and
      extension of the specified path string"). Carry-forward from
      module_hierarchy.dart.md `rf-dart-dart-io-to-csharp-system-io`
      cached finding. Note: `Path.GetFileName(dirPath)` returns the
      LAST PATH COMPONENT regardless of whether it is conceptually a
      file or a directory name — semantics match Dart's `.split(sep)
      .last`.
    idiom_id: null
    research_finding_id: rf-dart-dart-io-to-csharp-system-io
    nuance: >-
      Split-last nuance (cached, explicitly addressed): Dart
      `path.split(separator).last` is the documented last-component
      extraction (returns the substring after the FINAL separator,
      or the whole string if no separator is present). .NET
      `Path.GetFileName(path)` is the documented counterpart
      (Microsoft Learn). Both handle trailing-separator edge cases
      identically — if `dirPath` ends in a separator (`'/path/to/dir/'`),
      both return EMPTY STRING (Dart `.split('/').last` → `''`; .NET
      `Path.GetFileName('/path/to/dir/')` → `''`). The caller
      (`discoverProject` line 94) invokes this helper for `self.glp`
      files in the form `_moduleNameFromDirPath(file.parent.path)`
      — `file.parent.path` does NOT end in a separator (Dart's
      `.parent.path` strips the trailing separator), so the empty-
      string edge case is not triggered. Expression-bodied member
      nuance: Dart `final parts = ...; return parts.last;` (two
      statements) → C# `=> Path.GetFileName(dirPath);` (single
      expression-bodied member, Microsoft Learn). Faithful to the
      source's INTENT (return the last component) even though the
      C# render is shorter — the Dart two-statement form is just
      verbose; the C# one-liner is the idiomatic BCL counterpart.
      Null-safety: dirPath non-nullable String → C# string; return
      non-nullable string. Async: ABSENT.

  - construct_key: dart.private_helper.build_ancestor_scope_with_lexer_parser_pipeline_type_expansion_merge_and_template_carry
    source_form: >-
      Lines 442-480: "TypeEnvironment _buildAncestorScope(List<String>
      chain, {String? rootSelfGlpPath}) { var env = buildPreludeEnvironment();
      for (final selfGlpPath in chain) { final source = File(selfGlpPath)
      .readAsStringSync(); final lexer = Lexer(source); final tokens =
      lexer.tokenize(); final parser = Parser(tokens); final selfModule =
      parser.parseModule(); final selfTemplates = <String, TypeDef>{};
      for (final td in selfModule.typeDefs) { if (td.isParameterized)
      { selfTemplates[td.name] = td; } } final expandedSelfModule =
      expandParameterizedTypes(selfModule, knownTypeNames: env.types
      .keys.toSet(), externalTemplates: env.typeTemplates); final selfEnv
      = buildScopeFromModule(expandedSelfModule); env = env.merge(
      TypeEnvironment(selfEnv.types, selfEnv.procedures, paramProcDecls:
      selfEnv.paramProcDecls, typeTemplates: selfTemplates)); } return
      env; }" — STRUCTURAL DUPLICATE of the per-self.glp loop body in
      module_hierarchy.dart's `assembleTypeScope` (the project linker
      inlines a subset — skipping the final module-itself merge — because
      the per-module type check at `typeCheckProject` already applies
      the module-itself layer via `checkModule(..., ancestorScope: ...)`).
      The `rootSelfGlpPath` named-optional parameter is RECEIVED but
      NOT USED in the body (the chain already includes the root self.glp
      transitively via the prelude environment — a documentation comment
      in the source says so explicitly).
    target_decision: >-
      Emit `private static TypeEnvironment BuildAncestorScope(IReadOnlyList<
      string> chain, string? rootSelfGlpPath = null)` on the hosting
      static class. Body translation identical to module_hierarchy.dart's
      `AssembleTypeScope` body MINUS the final two `expandedModule` /
      `moduleEnv` / `env.Merge(moduleEnv)` statements (the project-
      linker variant does not have a target `module` parameter). Per-
      self.glp loop: `var env = TypeEnvironmentBuilder.BuildPreludeEnvironment();
      foreach (var selfGlpPath in chain) { var source = File.ReadAllText(
      selfGlpPath); var lexer = new Lexer(source); var tokens = lexer
      .Tokenize(); var parser = new Parser(tokens); var selfModule =
      parser.ParseModule(); var selfTemplates = new Dictionary<string,
      TypeDef>(); foreach (var td in selfModule.TypeDefs) if (td
      .IsParameterized) selfTemplates[td.Name] = td; var expandedSelfModule
      = ParamExpansion.ExpandParameterizedTypes(selfModule, knownTypeNames:
      new HashSet<string>(env.Types.Keys), externalTemplates: env
      .TypeTemplates); var selfEnv = ModuleHierarchy.BuildScopeFromModule(
      expandedSelfModule); env = env.Merge(new TypeEnvironment(selfEnv
      .Types, selfEnv.Procedures, paramProcDecls: selfEnv.ParamProcDecls,
      typeTemplates: selfTemplates)); } return env;`. The unused
      `rootSelfGlpPath` parameter is PRESERVED in the C# signature for
      API compatibility (callers MAY pass it; the body ignores it —
      same as Dart). Per the cached `rf-dart-map-literal-to-csharp-
      dictionary-and-keys-toset-to-hashset` finding from
      module_hierarchy.dart.md, every map literal + key.toSet idiom
      maps 1:1.
    idiom_id: null
    research_finding_id: rf-dart-map-literal-to-csharp-dictionary-and-keys-toset-to-hashset
    nuance: >-
      Structural-duplicate nuance (LOAD-BEARING, explicitly addressed):
      this function is a STRUCTURAL SUBSET of module_hierarchy.dart's
      `assembleTypeScope` — same per-self.glp loop body, omitted final
      module-itself merge. Two implementation paths exist in the codeconv
      family: (a) call `ModuleHierarchy.AssembleTypeScope(chain, dummyModule)`
      where `dummyModule` is an empty Module — but this requires a
      sentinel value and the merge is a no-op for an empty module; (b)
      copy-paste the per-self.glp loop body (current Dart approach) —
      preserves the explicit narrowing. The SPEC documents the COPY-PASTE
      faithful translation, NOT a refactor. A future codegen stage MAY
      choose to consolidate by adding a `ModuleHierarchy.BuildAncestorScope`
      overload that omits the trailing merge — but the source's choice
      is preserved here. Unused-parameter nuance: `rootSelfGlpPath` is
      received but not consumed in the body — a Dart documentation
      comment explains this is intentional (the chain already includes
      the root self.glp transitively via the prelude). C# render
      preserves the parameter for API stability (Microsoft Learn:
      "Optional arguments — A method that has optional parameters can
      be called without supplying values for those parameters"); the
      compiler may warn on the unused parameter, suppressed via
      `#pragma warning disable CS9113` (unused primary ctor parameter)
      or by adding a doc-comment noting the intended future use.
      Pipeline-consistency nuance: every step of the Lexer → Parser →
      ParseModule → expandParameterizedTypes → buildScopeFromModule
      pipeline matches module_hierarchy.dart.md's translation EXACTLY —
      cross-file consistency is required for the C# render to compile
      against the same module_hierarchy.cs / lexer.cs / parser.cs / etc.
      ctor and method surfaces (carry-forward from those files'
      convspecs). Async: ABSENT throughout.

conversion_units:
  - "namespace declaration Glp.Compiler (mirrors lib/compiler/); file-header XML doc carries the Dart triple-slash header (Project linker: static linking of multi-module GLP projects + spec/plan citations)"
  - "using directives at file top: using System; using System.IO; using System.Linq; using System.Collections.Generic; using Glp.Analysis.TypeChecker; using Glp.Runtime; using Ast = Glp.Compiler.Ast; (the `Ast` alias is needed because `DiscoveredModule.ModuleAst` references `Ast.Module` after the property rename)"
  - "public sealed class DiscoveredModule with five get-only auto-properties (FilePath, ModuleName, ModuleAst, AncestorScope, IsSelfGlp) and a positional ctor (four required + one default-false); class, NOT record, because the linker uses ReferenceEquals for identity dispatch"
  - "public sealed class LinkResult with two get-only auto-properties (Program, ProcDeclarations: IReadOnlyList<ProcDecl>) and a positional ctor"
  - "public static class ProjectLinker hosting the four top-level functions as static methods plus four private static helpers"
  - "  public static IReadOnlyList<DiscoveredModule> DiscoverProject(string rootDir, string? rootSelfGlpPath = null) — Directory.Exists guard + ArgumentException; Directory.EnumerateFiles(rootDir, \"*.glp\", SearchOption.AllDirectories); per-file exclusion (boot_direct.glp, mad_boot.glp, mad_boot/ subdirectory with BOTH platform-separator AND literal-'/' checks preserved); File.ReadAllText + Lexer + Parser + ParseModule pipeline; null-coalescing module-name derivation via _moduleNameFromDirPath / _moduleNameFromFilename; DiscoverSelfChain via named-arg call to ModuleHierarchy.DiscoverSelfChain; BuildAncestorScope inner helper invocation"
  - "  public static void TypeCheckProject(IReadOnlyList<DiscoveredModule> modules) — early-skip on empty / all-imported decls; PartialEvaluator.TransformDefinedGuards; TypeChecker.CheckModule with named-arg syntax; error aggregation via LINQ Select + string.Join('\\n'); throws bare System.Exception with module-name+path-prefixed message"
  - "  public static LinkResult LinkProject(IReadOnlyList<DiscoveredModule> modules, string topModuleName) — five-phase rewrite (registry build → ancestor-self.glp map with ReferenceEquals + path-length-descending order + ContainsKey-then-assign for inner-most-wins → per-module clause+goal rewrite with Goal/RemoteGoal/SpawnGoal dispatch → declIndex project-wide → entry-point alias generation with mode-aware ProcDecl lookup); builds allProcedures + allDecls; returns new LinkResult(new Program(allProcedures, 0, 0), allDecls)"
  - "  private static Goal ResolveGoal(Goal goal, string moduleName, IReadOnlyCollection<string> localSigs, IReadOnlyDictionary<string, string> ancestorSelfProcs) — three-branch declaration-pattern dispatch (is RemoteGoal rg → static-module rewrite; is SpawnGoal sg → recursive inner-goal rewrite with ReferenceEquals identity-preservation; else → local-or-ancestor lookup); returns NEW Goal/SpawnGoal on rewrite, same instance on no-op"
  - "  private static ProcDecl? FindProcDecl(DiscoveredModule mod, string name, int arity) — explicit foreach with early return; returns null when no non-imported match found"
  - "  private static Clause MakeAliasClause(string name, int arity, string targetName, ProcDecl? declaration = null) — zero-arity fast path with Array.Empty<Term>(); else Term[arity] pre-allocation + indexed for-loop population; per-index isInput ternary (declaration != null && i < ArgTypes.Count ? IsInputArg(i) : true); new VarTerm($\"V{i}\", isInput, 0, 0); returns new Clause(head, body: ...) with named-arg call"
  - "  private static string ModuleNameFromFilename(string filename) — EndsWith(\".glp\", StringComparison.Ordinal) check + s[..^4] range-indexer substring"
  - "  private static string ModuleNameFromDirPath(string dirPath) => Path.GetFileName(dirPath); — expression-bodied; BCL one-liner"
  - "  private static TypeEnvironment BuildAncestorScope(IReadOnlyList<string> chain, string? rootSelfGlpPath = null) — structural subset of ModuleHierarchy.AssembleTypeScope (same per-self.glp loop body, omits final module-itself merge); rootSelfGlpPath received but unused (preserved for API stability)"

escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-docblock-triple-slash-to-csharp-xml-doc + rf-dart-library-directive-to-csharp-namespace-elision — file header (cached idiom, reuse)

- Deep analysis: 9-line triple-slash header with two doc citations (spec + plan paths) plus a bare `library;` directive. The header explains the discover → type-check → link pipeline at a high level; the spec/plan paths are grep targets.
- Authoritative Dart (cached): https://dart.dev/effective-dart/documentation (triple-slash doc-comments) + https://dart.dev/language/libraries (library directive).
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc (XML doc).
- Conclusion: doc-block migrates verbatim as XML-doc on the hosting `ProjectLinker` static class; `library;` is elided (no C# counterpart needed). Cache hit. FR-024.

### rf-dart-relative-import-to-csharp-using-or-same-namespace — seven imports collapse to four usings (cached, extended)

- Deep analysis: SEVEN Dart imports — one `dart:io`, four sibling `lib/compiler/` files (ast/lexer/parser/partial_evaluator), three cross-package files in `lib/analysis/type_checker/` (type_ast/param_expansion/type_checker), one in `lib/runtime/` (module_hierarchy), one transitively imported via module_hierarchy.dart (type_environment_builder).
- Authoritative Dart: https://dart.dev/language/libraries — "Use show to import only some of the names from a library. Use hide to import all names except some. Use as to specify a library prefix."
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive — "The using directive imports types defined in the specified namespace."
- Conclusion: four sibling `lib/compiler/` imports → zero `using`s (same-namespace visibility); `dart:io` → `using System.IO;`; three `lib/analysis/type_checker/` imports → ONE `using Glp.Analysis.TypeChecker;`; one `lib/runtime/module_hierarchy.dart` → `using Glp.Runtime;`. Cached idiom from partial_evaluator.dart.md + module_hierarchy.dart.md. No new research.

### rf-dart-named-required-and-default-params-to-csharp-positional-default — `DiscoveredModule` ctor with four required + one default-false (cached, reuse)

- Deep analysis: `DiscoveredModule({required this.filePath, required this.moduleName, required this.ast, required this.ancestorScope, this.isSelfGlp = false})` — four named-required + one named-default. Single call-site at line 106-112 passes all five by name.
- Authoritative Dart: https://dart.dev/language/functions#named-parameters.
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments.
- Conclusion: four required → positional with no default; one named-default → positional with `= false` default. C# 4 named-arg call syntax preserves the Dart named-call shape 1:1. Cached idiom from ast.dart.md (`Clause`, `UnderscoreTerm`). FR-024.

### rf-dart-final-field-class-to-csharp-getonly-class — `DiscoveredModule` + `LinkResult` (cached, reuse)

- Deep analysis: two immutable data classes with `final` fields and positional/named ctors. Reference identity required for DiscoveredModule (used in `identical(s, mod)` later). LinkResult is purely a data carrier.
- Authoritative Dart: https://dart.dev/language/classes#instance-variables ("Mark instance variables that should never be reassigned with final").
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/csharp/properties — get-only auto-properties.
- Conclusion: `public sealed class` with get-only auto-properties for both. NOT `record` (would break reference-identity for DiscoveredModule). Cached idiom carry-forward from parser.dart.md + result.dart.md.

### rf-dart-directory-listsync-recursive-wheretype-file-to-csharp-directory-enumeratefiles-pattern — recursive walk + filter (NEW finding, LOAD-BEARING)

- Deep analysis: `Directory(rootDir).listSync(recursive: true).whereType<File>().where((f) => f.path.endsWith('.glp')).toList()` — three-stage Iterable pipeline composing: (1) recursive directory enumeration yielding mixed `FileSystemEntity`; (2) type-narrowing filter to `File` only; (3) extension filter on `.glp`. The Dart source materialises via `.toList()` at the end (eager).
- Authoritative Dart: https://api.dart.dev/stable/dart-io/Directory/listSync.html ("Lists the sub-directories and files of this Directory. … If recursive is true, lists all sub-directories, and files, recursively."), https://api.dart.dev/stable/dart-core/Iterable/whereType.html ("Returns a new lazy Iterable with all elements that have type T").
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.enumeratefiles ("Returns an enumerable collection of full file names that match a search pattern in a specified path, and optionally searches subdirectories. … If you want to search subdirectories, set searchOption to AllDirectories."), https://learn.microsoft.com/en-us/dotnet/api/system.io.searchoption ("Specifies whether to search the current directory, or the current directory and all subdirectories — AllDirectories includes the current directory and all its subdirectories").
- Conclusion: the three-stage Dart pipeline collapses to ONE .NET BCL call `Directory.EnumerateFiles(rootDir, "*.glp", SearchOption.AllDirectories)`. EnumerateFiles is lazy (matches Dart's intent until `.toList()`). The OS filesystem driver performs the pattern match (kernel-level filter, faster than user-space `.where`). Semantics are byte-equivalent. NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-iterable-any-map-join-to-csharp-linq-any-select-string-join — error aggregation pipeline (NEW finding, LOAD-BEARING)

- Deep analysis: `typeCheckProject` uses three distinct Iterable extension methods composed: `.any((d) => !d.imported)` for the existence check, `.map((e) => '${...}')` for the per-element transform, `.join('\n')` for the string concatenation. All three are documented Dart Iterable extensions.
- Authoritative Dart: https://api.dart.dev/stable/dart-core/Iterable/any.html ("Checks whether any element of this iterable satisfies test"), https://api.dart.dev/stable/dart-core/Iterable/map.html ("Returns a new lazy Iterable with elements that are created by calling f on each element"), https://api.dart.dev/stable/dart-core/Iterable/join.html ("Converts each element to a String and concatenates the strings").
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.any ("Determines whether any element of a sequence satisfies a condition"), https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.select ("Projects each element of a sequence into a new form"), https://learn.microsoft.com/en-us/dotnet/api/system.string.join ("Concatenates the members of a collection, using the specified separator between each member").
- Conclusion: three-way mapping — `.any(p)` → LINQ `.Any(p)`; `.map(f)` → LINQ `.Select(f)`; `.join(sep)` → `string.Join(sep, source)` (NOTE the .NET signature inverts to `(separator, source)`). NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-map-putifabsent-lambda-to-csharp-containskey-then-assign — inner-most-wins insertion (NEW finding, LOAD-BEARING)

- Deep analysis: `procs.putIfAbsent(sig, () => selfMod.moduleName)` — Dart's INSERT-IF-ABSENT primitive with a LAZY default-value lambda. Critical to the inner-most-self.glp-wins discipline because the linker walks ancestors from inner to outer; the FIRST `putIfAbsent` wins, every subsequent attempt is a no-op. Plain `[k] = v` would OVERWRITE, breaking the invariant.
- Authoritative Dart: https://api.dart.dev/stable/dart-core/Map/putIfAbsent.html ("Look up the value of key, or add a new entry if it isn't there. … Otherwise calls ifAbsent to get a new value, associates key to that value, and then returns the new value.").
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.tryadd ("Attempts to add the specified key and value to the dictionary. … true if the key/value pair was added to the dictionary successfully; otherwise, false.") [.NET 6+] OR the explicit `if (!dict.ContainsKey(key)) dict[key] = value;` two-statement form (universal).
- Conclusion: `procs.putIfAbsent(sig, () => selfMod.moduleName)` → preferred `procs.TryAdd(sig, selfMod.ModuleName)` (.NET 6+ single call, atomic, returns bool) OR the universal `if (!procs.ContainsKey(sig)) procs[sig] = selfMod.ModuleName;` fallback. Both preserve inner-most-wins. The LAZY-default aspect of Dart's lambda is preserved by `ContainsKey+[]=` because the right-hand side is only evaluated when the key is absent (short-circuit). NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-is-pattern-with-typed-binding-and-identical-noop-to-csharp-declaration-pattern-with-referenceequals — runtime-type dispatch in `_resolveGoal` (NEW finding, LOAD-BEARING)

- Deep analysis: three-branch type dispatch (`is RemoteGoal`, `is SpawnGoal`, else Goal). The `identical(resolvedInner, goal.innerGoal)` check on the SpawnGoal arm is the IDENTITY-VS-REWRITE detector that preserves sub-tree identity through no-op recursion. The `_resolveGoal` function is recursive only via the SpawnGoal arm.
- Authoritative Dart: https://dart.dev/language/operators ("`is` — True if the object has the specified type"), https://api.dart.dev/stable/dart-core/identical.html ("Check whether two references are to the same object").
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/is ("The is operator checks if the result of an expression is compatible with a given type, or (starting with C# 7.0) tests an expression against a pattern."), https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/patterns#declaration-and-type-patterns ("A declaration pattern with type T matches an expression when an expression's result is non-null and any of the following conditions are true: The runtime type of expr is T."), https://learn.microsoft.com/en-us/dotnet/api/system.object.referenceequals ("Determines whether the specified Object instances are the same instance").
- Conclusion: Dart `goal is RemoteGoal` with type promotion → C# 7+ declaration pattern `goal is RemoteGoal rg`. Dart `identical(a, b)` → C# `object.ReferenceEquals(a, b)`. Semantics identical on both sides. Identity-preservation is LOAD-BEARING because the SpawnGoal arm allocates a NEW SpawnGoal only if the inner Goal was actually rewritten; without this, redundant allocations cascade. NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-linear-scan-first-match-or-null-to-csharp-foreach-or-firstordefault — `_findProcDecl` (NEW finding, minor)

- Deep analysis: explicit `for`-in loop with early `return` on match, fallback `return null`. Two-condition predicate (`!d.imported && d.name == name && d.arity == arity`). Returns `ProcDecl?`.
- Authoritative Dart: https://dart.dev/language/loops ("The for-in loop iterates over the elements of an Iterable").
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.firstordefault ("Returns the first element of a sequence that satisfies a condition or a default value if no such element is found").
- Conclusion: preserve the `foreach`-with-early-return shape for byte-level fidelity; LINQ `.FirstOrDefault(pred)` is an acceptable refactor (the SPEC documents the source's chosen form). NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-list-generate-with-as-term-cast-to-csharp-array-loop-or-enumerable-range-select — `_makeAliasClause` arg generation (NEW finding, LOAD-BEARING)

- Deep analysis: `List.generate(arity, (i) => VarTerm(...) as Term)` — Dart's factory iterates `arity` times, calling the lambda; the `as Term` cast is required because Dart infers the element type from the lambda's first-call runtime type (`VarTerm`), which would yield a `List<VarTerm>` without the cast. Two call-sites: headArgs (uniformly `isReader: false`) and bodyArgs (per-index `isInput` from declaration).
- Authoritative Dart: https://api.dart.dev/stable/dart-core/List/List.generate.html ("Generates a list of values. … Creates a list with length positions and fills it with values created by calling generator").
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.range ("Generates a sequence of integral numbers within a specified range"), https://learn.microsoft.com/en-us/dotnet/api/system.array.empty ("Returns an empty array").
- Conclusion: SPEC prefers an explicit `for` loop with `Term[arity]` pre-allocation for byte-level fidelity (LINQ `Enumerable.Range(0, arity).Select(...).ToArray()` is an acceptable refactor). The Dart `as Term` cast is ELIDED in C# because the array type is declared upfront. Zero-arity fast-path uses `Array.Empty<Term>()` (Microsoft Learn recommended). NEW idiom registered. Authoritative both sides; no escalation.

### rf-dart-string-substring-zero-length-minus-n-to-csharp-range-indexer — `.substring(0, length - 4)` (cached, reuse)

- Deep analysis: `filename.substring(0, filename.length - 4)` strips the trailing 4 characters (`.glp`). Standard Dart substring idiom.
- Authoritative Dart (cached): https://api.dart.dev/stable/dart-core/String/substring.html.
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/member-access-operators#range-operator-.
- Conclusion: Dart `.substring(0, length - 4)` → C# `s[..^4]` range-indexer (C# 8+). Cached idiom. Carry-forward from lexer.dart.md substring decisions.

### rf-dart-dart-io-to-csharp-system-io — Platform.pathSeparator + Path.GetFileName + File.ReadAllText + Directory.Exists (cached, reuse)

- Deep analysis: this file exercises `Directory(p).existsSync()` (→ `Directory.Exists(p)`), `File(p).readAsStringSync()` (→ `File.ReadAllText(p)`), `file.path.split(Platform.pathSeparator).last` (→ `Path.GetFileName(file)`), `file.parent.path` (→ `Path.GetDirectoryName(file)`), `file.absolute.path` (→ `Path.GetFullPath(file)`), and uses `Platform.pathSeparator` in the `mad_boot/` exclusion check (→ `Path.DirectorySeparatorChar`).
- Authoritative Dart + .NET (cached): see module_hierarchy.dart.md's expansion of this finding.
- Conclusion: every dart:io use site has a documented .NET counterpart. No new research. FR-024 cache hit.

### rf-dart-map-literal-to-csharp-dictionary-and-keys-toset-to-hashset — five map-comprehension sites (cached, reuse)

- Deep analysis: FIVE distinct map-literal sites in this file — `<String, Set<String>>{}` (registry), `<String, String>{}` (procs), `<String, Map<String, String>>{}` (ancestorSelfProcs), `<String, ProcDecl>{}` (declIndex), `<String, String>{}` (aliasedSigs); plus four `<String, TypeDef>{}`/etc. inside `_buildAncestorScope`. Plus one `env.types.keys.toSet()` inside `_buildAncestorScope`.
- Authoritative Dart + .NET (cached): see module_hierarchy.dart.md's expansion.
- Conclusion: every map literal → `new Dictionary<K, V>()`; every `set literal` (`<T>{}`) → `new HashSet<T>()`; every `dict.keys.toSet()` → `new HashSet<K>(dict.Keys)`. Cached idiom. No new research.

## Notes

- The file is SYNCHRONOUS throughout. NO `async` / `await` / `Future` / `Stream` / `Isolate` / `Completer`. The well-known Dart-Stream → C# IAsyncEnumerable nuance is correctly NOT asserted — the code does not exercise it (same discipline as module_hierarchy.dart.md / boot_loader.dart.md / partial_evaluator.dart.md).
- The file is a MIX of TWO DATA CLASSES (`DiscoveredModule`, `LinkResult`) AND FOUR TOP-LEVEL FUNCTIONS (`discoverProject`, `typeCheckProject`, `linkProject`, plus four private helpers `_resolveGoal`, `_findProcDecl`, `_makeAliasClause`, `_moduleNameFromFilename`, `_moduleNameFromDirPath`, `_buildAncestorScope`). The faithful C# render hosts the functions on a `public static class ProjectLinker` (carry-forward from module_hierarchy.dart.md / external_io.dart.md) and emits the data classes as sibling sealed classes in the same namespace.
- Load-bearing semantic decisions for THIS file: (a) DiscoveredModule is a CLASS (not record) — reference-identity required for `identical(s, mod)` checks; (b) `Directory.EnumerateFiles(path, "*.glp", SearchOption.AllDirectories)` collapses the Dart three-stage pipeline to one BCL call AND uses kernel-level pattern matching; (c) `putIfAbsent(k, () => v)` → `TryAdd(k, v)` or `ContainsKey+[]=` (NOT `[k]=v` which would break inner-most-wins); (d) `identical(a, b)` → `ReferenceEquals(a, b)`; (e) `is RemoteGoal` / `is SpawnGoal` → C# declaration patterns with typed binding (`is X x`); (f) `List.generate(arity, (i) => X as Term)` → `Term[arity]` array + indexed for-loop (the `as Term` cast is ELIDED at the C# call site because array declaration carries the base type); (g) dual-separator exclusion check (both `Platform.pathSeparator + "mad_boot"` AND literal `"/mad_boot"`) is preserved for cross-platform defensive behaviour; (h) `Map<K,V>[key]` (Dart, returns null on absent) → `IDictionary<K,V>.TryGetValue(key, out value)` (C#, throws on direct indexer absent) — semantic equivalence via the TryGet pattern; (i) `clause.body?.map(...).toList()` null-conditional chain → `clause.Body?.Select(...).ToList()` byte-identical; (j) string-comparison ordinality enforced via `StringComparison.Ordinal` overload at every `.EndsWith` / `.StartsWith` call site to preserve Dart's culture-independent semantics.
- Trivial / non-construct elements: every `///` per-function/per-class doc-comment migrates mechanically to C# XML-doc; `final` for locals → C# `var` (immutability implicit at local scope); `var` Dart → `var` C# (same role); `for (final x in ys)` → `foreach (var x in ys)`; comments inside method bodies (`// Walk self.glp modules from inner-most to outer-most.`, `// Inner-most wins (first entry in putIfAbsent).`, `// Skip if an alias already exists (top module wins)`, etc.) migrate as `//` line comments preserving the inner-most-wins / top-module-wins semantic-preservation hints for code reviewers.
- Zero escalations. Every non-trivial construct is grounded in official Dart and/or .NET documentation. Eight cached idioms reused (doc-block, library elision, relative-import folding, named-required+default ctor, final-field class, dart:io, map-literal+keys.toSet+HashSet, string-substring range-indexer) and FIVE new idioms registered (recursive walk via Directory.EnumerateFiles; any+map+join via LINQ; putIfAbsent via TryAdd/ContainsKey; is-pattern with declaration binding + ReferenceEquals; List.generate with Term[] pre-allocation). FR-009 / FR-010 quality bar satisfied: every non-trivial construct has BOTH a deep-analysis basis AND a researched-pattern basis (research_finding_id), with explicit nuance-addressing per FR-024.
