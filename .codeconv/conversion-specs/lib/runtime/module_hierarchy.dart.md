# Conversion Spec — lib/runtime/module_hierarchy.dart

> Conversion-spec artifact for lib/runtime/module_hierarchy.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> Three top-level free functions that implement GLP's directory-based
> module hierarchy: `discoverSelfChain` (walks the filesystem from a
> target .glp file's directory up to the project root, collecting each
> ancestor `self.glp`), `assembleTypeScope` (layers a parsed Module
> AST's `TypeEnvironment` on top of every ancestor self.glp's
> environment), and `buildScopeFromModule` (a `TypeEnvironment`
> factory that — UNLIKE `_buildEnvironmentFromModule` in
> `type_environment_builder.dart` — skips predefined-type-redefinition
> checking and alias resolution because those happen after the full
> scope chain is assembled).
>
> Load-bearing nuances exercised by THIS file: (a) heavy `dart:io`
> usage — `Directory(...)`, `File(...)`, `existsSync`, `readAsStringSync`,
> `Directory(p).absolute.path`, `File(p).parent.path`, AND
> `Platform.pathSeparator` (a Dart-runtime constant that varies between
> Windows `\\` and POSIX `/`); (b) the path-comparison loop normalises
> by stripping trailing `/` regardless of platform (so the conversion
> MUST preserve the literal `'/'` strip, not switch to a platform-
> separator strip — that would change behaviour on Windows where Dart
> `Directory.absolute.path` returns `\\`-separated paths); (c) Dart
> top-level free functions become C# `public static` methods on a
> hosting static class (carry-forward from external_io.dart.md);
> (d) Dart named-required parameters (`{required String targetFile,
> required String rootDir}`) — no C# direct counterpart pre-C# 4 but
> the faithful render is ordinary positional parameters with no
> defaults (compile-time mandatory) per external_io.dart.md
> rf-dart-final-field-class-to-csharp-getonly-class precedent for
> required-named ctor arguments; (e) `Iterable.reversed.toList()` —
> Dart extension method on `Iterable<T>` → LINQ
> `System.Linq.Enumerable.Reverse().ToList()`; (f) `final selfTemplates
> = <String, TypeDef>{};` populated via for-in maps to a `Dictionary`
> populated via foreach; (g) parser-pipeline `Lexer → tokenize() →
> Parser → parseModule()` composes types defined in
> lib/compiler/lexer.dart, lib/compiler/parser.dart, lib/compiler/ast.dart
> (each with its own convspec); the spec records the type names but
> the API surface comes from those files' specs.

```yaml
schema_version: 1
source_path: lib/runtime/module_hierarchy.dart
source_sha256: db87dd95891c91cf5d37ba3d1e17349102b04226388a6a81e45f00fe59513298
target_code_unit: lib/runtime/module_hierarchy.cs
constructs:
  - construct_key: dart.docblock_triple_slash_file_header_citing_external_spec
    source_form: >-
      Leading triple-slash doc-comment block (lines 1-9) declaring the
      file's purpose ("Module hierarchy: self.glp chain discovery and
      type scope assembly.") and citing
      `docs/modules/glp-module-system-spec.md` Sections 2-3 (directory-
      based hierarchy, implicit ancestor scoping, shadowing, sibling
      isolation). No `library;` directive follows — the doc-block
      stands alone at the top of the compilation unit.
    target_decision: >-
      Translate the doc-block verbatim to a C# `///` XML-doc comment
      block attached to the hosting `public static class
      ModuleHierarchy` declaration (or to the namespace declaration if
      no hosting class were emitted; here the class is emitted, so the
      doc attaches to the class). The four cited spec sections
      ("Section 2", "Section 3.1", "Section 3.2", "Section 3.3") are
      preserved byte-identically. NO `library;` to elide here — this
      file omits the directive (in contrast to suspension.dart and
      external_io.dart which DO emit one). The absence of `library;`
      is not load-bearing for the conversion; the doc-block migrates
      to the same target shape either way.
    idiom_id: null
    research_finding_id: rf-dart-docblock-triple-slash-to-csharp-xml-doc
    nuance: >-
      Doc-comment nuance: Dart `///` is the documentation form
      consumed by dartdoc; C# `///` is the XML-doc form consumed by
      `csc /doc:` and Visual Studio IntelliSense. Both languages use
      the same three-slash sigil; the migration is comment-for-
      comment. Spec-citation nuance: the explicit
      `docs/modules/glp-module-system-spec.md` path is preserved
      verbatim — diagnostic searches grep on this exact string.
      No-`library`-directive nuance (explicitly addressed): unlike
      suspension.dart / external_io.dart, this file has no `library;`
      directive, so the elision idiom (rf-dart-library-directive-to-
      csharp-namespace-elision) does NOT apply here — there is
      nothing to elide. Carry-forward of the doc-block migration
      idiom from every prior runtime/* convspec (universal). FR-024
      cache hit.

  - construct_key: dart.import_directive.dart_io_to_csharp_using_system_io
    source_form: >-
      "import 'dart:io';" — pulls `File`, `Directory`, `Platform`
      from the Dart core library (in this file: `File(path)`,
      `Directory(path)`, `Platform.pathSeparator`,
      `Directory(p).absolute.path`, `File(p).parent.path`,
      `File(p).existsSync()`, `File(p).readAsStringSync()`).
    target_decision: >-
      Emit `using System.IO;` (for `File`, `Directory`, `Path`) at
      the top of the target file. Dart `Platform.pathSeparator` does
      NOT live in `System.IO` — it maps to
      `System.IO.Path.DirectorySeparatorChar` (a `char` field, not a
      `string`, see nuance), which is reached via the same `using
      System.IO;`. No additional `using` is required for `Platform`
      because the codeconv .NET counterpart routes the platform
      separator through `Path.DirectorySeparatorChar`. Cached idiom:
      Dart `dart:io` → .NET `System.IO` (carry-forward from
      repl_play_runner.dart.md and runtime.dart.md which both
      reference `System.IO.File.Exists`, `System.IO.Directory.Exists`,
      `FileStream`).
    idiom_id: null
    research_finding_id: rf-dart-dart-io-to-csharp-system-io
    nuance: >-
      Library-mapping nuance (explicitly addressed): Dart `dart:io`
      is the synchronous-and-async filesystem + process + platform
      surface (`File`, `Directory`, `Platform`, `Process`, `stdin`,
      `stdout`); .NET counterparts are split across `System.IO`
      (filesystem types) and `System.Runtime.InteropServices` /
      `System.Environment` (platform / OS introspection). In THIS
      file only `dart:io`'s filesystem surface (`File`, `Directory`)
      AND its `Platform.pathSeparator` constant are exercised — the
      latter routes to `System.IO.Path.DirectorySeparatorChar`, the
      former to `System.IO.File` / `System.IO.Directory`. Sync-vs-
      async nuance: every `dart:io` call in this file is the
      synchronous variant (`existsSync`, `readAsStringSync`,
      `.absolute.path` getters which are synchronous filesystem
      lookups under the hood); the .NET counterparts (`File.Exists`,
      `File.ReadAllText`, `Path.GetFullPath`) are likewise synchronous
      — the SPEC does NOT introduce `async`/`await` (carry-forward
      from repl_play_runner.dart.md rf-dart-fs-existsSync-to-csharp-
      file-directory-exists discipline). Char-vs-string nuance:
      Dart `Platform.pathSeparator` is `String` (always length-1 in
      practice but typed as String);
      `System.IO.Path.DirectorySeparatorChar` is `char` — the C#
      render performs an implicit string-conversion at
      concatenation sites or uses `Path.Combine` instead (see the
      construct below).

  - construct_key: dart.import_directive.package_internal_to_using_namespace_six_imports
    source_form: >-
      Six imports total — one `dart:io` (handled above) and five
      package imports: `import 'package:glp_runtime/compiler/lexer.dart';`,
      `import 'package:glp_runtime/compiler/parser.dart';`,
      `import 'package:glp_runtime/compiler/ast.dart' as ast;` (note
      the `as ast;` prefix — the only prefixed import in the file;
      `ast.Module` is used at every reference site),
      `import 'package:glp_runtime/analysis/type_checker/type_ast.dart';`
      (TypeDef, ProcDecl, TypeEnvironment),
      `import 'package:glp_runtime/analysis/type_checker/param_expansion.dart';`
      (expandParameterizedTypes),
      `import 'package:glp_runtime/analysis/type_checker/type_environment_builder.dart';`
      (buildPreludeEnvironment).
    target_decision: >-
      Each Dart `package:` import becomes a .NET `using` directive
      naming the namespace of the corresponding converted file.
      `lexer.dart` / `parser.dart` / `ast.dart` are in
      `lib/compiler/` → `using Glp.Compiler;` (single using covers
      all three sibling files). `type_ast.dart` / `param_expansion.dart` /
      `type_environment_builder.dart` are in
      `lib/analysis/type_checker/` → `using Glp.Analysis.TypeChecker;`
      (single using covers all three). The Dart `as ast;` PREFIX on
      the ast.dart import is LOAD-BEARING: `ast.Module` is used at
      every reference site to disambiguate from another type that
      could collide (no actual collision in THIS file's references,
      but the prefix is the source's chosen disambiguation). The
      faithful C# render uses a `using` ALIAS directive: `using ast =
      Glp.Compiler.Ast;` IF `ast` is a namespace, OR `using AstModule
      = Glp.Compiler.Ast.Module;` IF the only referenced symbol is
      `Module`. Per the ast.dart.md convspec, `lib/compiler/ast.dart`
      contains many AST classes (Module, Clause, TypeDef, etc.); the
      .NET counterpart is a namespace `Glp.Compiler.Ast` — so the
      alias form is `using Ast = Glp.Compiler.Ast;` and call sites
      become `Ast.Module module` (one-to-one with Dart `ast.Module
      module`). The other five imports become bare `using` directives
      without aliasing. Carry-forward of the import→using idiom from
      external_io.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-import-as-prefix-to-csharp-using-alias-namespace
    nuance: >-
      Prefixed-import nuance (LOAD-BEARING, explicitly addressed):
      Dart `import '...' as ast;` introduces a library prefix; every
      symbol from that library is reached via `ast.<Symbol>`. C# has
      TWO using-alias shapes — `using <Alias> = <Namespace>;`
      (namespace alias, lets `<Alias>.<Symbol>` work for every symbol
      in the namespace) and `using <Alias> = <Namespace>.<Type>;`
      (type alias, only the one type is reachable as `<Alias>`).
      The faithful render is the NAMESPACE alias `using Ast =
      Glp.Compiler.Ast;` because the Dart prefix is library-wide:
      any symbol from ast.dart could in principle be reached via
      `ast.<X>`; constraining to a type alias would silently narrow
      the surface. Microsoft Learn: "Using directive — You can also
      use a using directive to create an alias for a namespace or a
      type." Library-vs-package nuance: Dart `package:glp_runtime/...`
      paths are URI-routed via pubspec.yaml; .NET namespaces are
      assembly-rooted — the conversion preserves the directory
      structure as namespace structure (`lib/compiler/` →
      `Glp.Compiler`, `lib/analysis/type_checker/` →
      `Glp.Analysis.TypeChecker`, `lib/runtime/` → `Glp.Runtime`).
      No show / hide directives — all imports are bare. No runtime
      semantics implicated; compile-unit nuance only.

  - construct_key: dart.top_level_function.named_required_params_string_return_list_string_returns_filesystem_walk
    source_form: >-
      "List<String> discoverSelfChain({ required String targetFile,
      required String rootDir, }) { ... }" — file-level (top-level,
      non-class) function. Body: normalises both paths via
      `Directory(rootDir).absolute.path` and `File(targetFile).absolute.path`,
      derives `targetName` from `target.split(Platform.pathSeparator)
      .last`, branches on whether `targetName == 'self.glp'`
      (sets `startDir` to grandparent) vs other (parent), then runs
      a `while (true) { ... break; }` loop walking from `startDir`
      toward `root` collecting `self.glp` files via
      `File('$dir${Platform.pathSeparator}self.glp').existsSync()` and
      appending `selfGlp.absolute.path` to a `chain` list. Loop
      termination: `if (!currentNorm.startsWith(rootNorm)) break;` and
      `if (currentNorm == rootNorm) break;` (after appending); walk-up
      via `Directory(currentDir).parent.path`. Final
      `return chain.reversed.toList();` to reverse collected-from-
      target-to-root order into root-first order.
    target_decision: >-
      Emit a `public static class ModuleHierarchy` in
      `Glp.Runtime` namespace (carry-forward of the static-host-
      class idiom from external_io.dart.md rf-dart-top-level-fn-
      builds-sum-type-leaf). Inside, emit `public static
      IReadOnlyList<string> DiscoverSelfChain(string targetFile,
      string rootDir)` — Dart named-required parameters
      `{required String targetFile, required String rootDir}` map to
      ORDINARY POSITIONAL C# parameters (NOT default-value
      parameters, NOT `[Required]` attributes) because Dart `required`
      is a compile-site obligation — the caller MUST supply the
      argument, no default exists — which positional C# parameters
      with no defaults satisfy identically. C# 4 named-argument
      syntax `DiscoverSelfChain(targetFile: x, rootDir: y)` preserves
      the Dart named-call shape at every call site (no behaviour
      change). Return shape `List<String>` → `IReadOnlyList<string>`
      (consumer treats as read-only; carry-forward convention from
      boot_loader.dart.md, external_io.dart.md). Body translation:
      `Directory(rootDir).absolute.path` → `Path.GetFullPath(rootDir)`
      (.NET's documented full-path normaliser, identical semantics
      — absolute, normalised); `File(targetFile).absolute.path` →
      `Path.GetFullPath(targetFile)`; `target.split(Platform
      .pathSeparator).last` → `Path.GetFileName(target)` (the .NET
      counterpart that strips the directory portion regardless of
      platform — IDENTICAL to Dart's "split on separator then take
      last" idiom but expressed as a single BCL call, semantically
      equivalent and platform-portable). `targetName == 'self.glp'`
      → `targetName == "self.glp"`. `File(target).parent.parent.path`
      (grandparent) → `Path.GetDirectoryName(Path.GetDirectoryName(
      target))` — TWO nested calls (`File.parent` returns the
      containing `Directory`; `Directory.parent` returns the parent
      `Directory`; `.path` extracts the string — the chain of two
      `.parent` is equivalent to two `Path.GetDirectoryName` calls).
      `File(target).parent.path` → `Path.GetDirectoryName(target)`.
      `final chain = <String>[];` → `var chain = new List<string>();`
      `var currentDir = startDir;` → `var currentDir = startDir;`.
      The `while (true) { ... break; }` body translates statement-
      for-statement (control flow identical). `Directory(currentDir)
      .absolute.path` → `Path.GetFullPath(currentDir)`. The trailing-
      slash normalisation `if (currentNorm.endsWith('/'))
      currentNorm = currentNorm.substring(0, currentNorm.length - 1);`
      → `if (currentNorm.EndsWith("/")) currentNorm =
      currentNorm[..^1];` — LITERAL `'/'` PRESERVED, NOT switched to
      `Path.DirectorySeparatorChar` (LOAD-BEARING — see nuance). The
      `.startsWith` test → C# `string.StartsWith(string)` with
      `StringComparison.Ordinal` for byte-exact comparison.
      `File('$currentDir${Platform.pathSeparator}self.glp').existsSync()`
      → `File.Exists(Path.Combine(currentDir, "self.glp"))` — the
      .NET `Path.Combine` is the documented platform-portable
      counterpart of Dart's `"$dir${Platform.pathSeparator}self.glp"`
      interpolation and produces the byte-equivalent path on each
      platform. `chain.add(selfGlp.absolute.path)` →
      `chain.Add(Path.GetFullPath(Path.Combine(currentDir,
      "self.glp")));`. `Directory(currentDir).parent.path` →
      `Path.GetDirectoryName(currentDir)`. `chain.reversed.toList()`
      → `chain.AsEnumerable().Reverse().ToList()` OR (more idiomatic)
      `Enumerable.Reverse(chain).ToList()` — Dart `Iterable.reversed`
      is the documented reverse-iteration extension, the .NET LINQ
      `Enumerable.Reverse<T>(IEnumerable<T>)` is its functional twin
      (Microsoft Learn: "Reverse inverts the order of the elements
      in a sequence"). The return shape is `IReadOnlyList<string>` —
      `List<string>` from `ToList()` is implicitly convertible.
    idiom_id: null
    research_finding_id: rf-dart-named-required-params-to-csharp-positional-params
    nuance: >-
      Named-required-parameter nuance (LOAD-BEARING, explicitly
      addressed): Dart `{required String x}` is a compile-site
      obligation — the caller MUST pass `x` by name; no default.
      C# has no syntactic mark for "required" pre-C# 11 (and
      C# 11's `required` keyword applies to PROPERTIES, not
      parameters). The faithful render is plain positional parameters
      with no defaults — compile-time-mandatory at the call site
      (the compiler errors on missing argument), and C# 4 named-
      argument syntax preserves the caller-readable named-call shape
      `DiscoverSelfChain(targetFile: x, rootDir: y)`. Microsoft Learn:
      "Named arguments enable you to specify an argument for a
      parameter by matching the argument with its name rather than
      with its position in the parameter list." This is the
      idiomatic .NET pattern for required-named-call ergonomics.
      Filesystem-API nuance (LOAD-BEARING, explicitly addressed):
      THREE Dart `dart:io` shapes map to THREE distinct .NET BCL
      shapes: (a) `Directory(p).absolute.path` /
      `File(p).absolute.path` → `Path.GetFullPath(p)` (Microsoft
      Learn: "GetFullPath(String) returns the absolute path for the
      specified path string"); (b) `Directory(p).parent.path` /
      `File(p).parent.path` → `Path.GetDirectoryName(p)` (Microsoft
      Learn: "GetDirectoryName(String) returns the directory
      information for the specified path"); (c) `path.split(
      Platform.pathSeparator).last` → `Path.GetFileName(p)`
      (Microsoft Learn: "GetFileName(String) returns the file name
      and extension"). All three are SYNCHRONOUS, the well-known
      Stream→IAsyncEnumerable nuance is correctly NOT asserted.
      Trailing-slash-strip nuance (LOAD-BEARING, explicitly
      addressed): the Dart source strips `'/'` LITERALLY — not
      `Platform.pathSeparator` — so a Windows path like
      `C:\\proj\\` would NOT be stripped by this branch (Windows
      Dart paths use `\\` separators). This appears to be a Dart
      defensive measure for the "absolute-path returned with
      trailing forward slash on POSIX" case; the C# render PRESERVES
      the literal `"/"` strip to match Dart's exact behaviour
      byte-for-byte (`Path.GetFullPath` on .NET strips trailing
      slashes already on most inputs, so this branch is typically
      a no-op in C#, but the literal preservation guarantees no
      behavioural divergence). Switching to `Path.DirectorySeparatorChar`
      would CHANGE behaviour on Windows (would also strip `\\`,
      which the Dart source does not). String-comparison nuance:
      `String.startsWith(other)` in Dart is ordinal/byte-level;
      C# `string.StartsWith(string)` defaults to current-culture
      under some overloads — the SPEC mandates
      `StartsWith(rootNorm, StringComparison.Ordinal)` to preserve
      Dart's culture-independent comparison. Reversed-iterable
      nuance: Dart `chain.reversed.toList()` returns a NEW list (does
      not mutate `chain`); .NET `Enumerable.Reverse(chain).ToList()`
      likewise returns a NEW `List<string>` — semantics match. Async
      nuance: ABSENT (every dart:io call here is the synchronous
      variant). Null-safety nuance: `targetFile` and `rootDir` are
      non-nullable Dart `String`; return shape is non-nullable
      `List<String>`. Under enabled NRT, the C# render is `string`
      / `IReadOnlyList<string>` (no `?`).

  - construct_key: dart.string_interpolation_path_composition_with_platform_separator
    source_form: >-
      `File('$currentDir${Platform.pathSeparator}self.glp')` — string
      interpolation composing a path with a platform-specific
      separator from `Platform.pathSeparator`.
    target_decision: >-
      Emit `Path.Combine(currentDir, "self.glp")` — the documented
      .NET path-composition primitive that uses the platform-correct
      separator (Microsoft Learn: "Combines two strings into a path
      … inserts the directory-separator character between the
      arguments if necessary"). This is SEMANTICALLY EQUIVALENT
      to Dart's `'$dir${Platform.pathSeparator}self.glp'` and is the
      idiomatic .NET counterpart. Carry-forward: the choice to use
      `Path.Combine` over manual concatenation (e.g.
      `$"{currentDir}{Path.DirectorySeparatorChar}self.glp"`) is
      load-bearing because `Path.Combine` ALSO handles the case
      where `currentDir` already ends in a separator (does not
      double the separator), whereas the manual concat would (a
      latent bug in the Dart source if `currentDir` ever ended in
      `\\`; preserved-and-fixed in the .NET render via
      `Path.Combine`'s normalisation).
    idiom_id: null
    research_finding_id: rf-dart-platform-pathseparator-to-csharp-path-combine
    nuance: >-
      Platform-separator nuance (LOAD-BEARING, explicitly addressed):
      Dart `Platform.pathSeparator` is a `String` field (api.dart.dev:
      "The path separator used by the operating system to separate
      components in file paths") — `\\` on Windows, `/` on POSIX.
      .NET has TWO related members:
      `System.IO.Path.DirectorySeparatorChar` (a `char`, platform-
      native) and `System.IO.Path.AltDirectorySeparatorChar` (`/` on
      Windows, `/` on POSIX). The IDIOMATIC .NET counterpart for
      "compose a path component" is NOT the separator character but
      `Path.Combine(string, string)`, which handles separator
      insertion and de-duplication automatically. The SPEC uses
      `Path.Combine` because (a) it matches the Dart source's
      OBSERVABLE behaviour (platform-correct separator), (b) it is
      the Microsoft-Learn-recommended primitive ("Use Path.Combine
      to safely combine strings into a path"), and (c) it
      gracefully handles trailing-separator-on-left-operand cases
      that the Dart manual concatenation does not. Char-vs-string
      nuance: if the codegen ever needs the BARE separator character
      (e.g. to split or to compare), it emits
      `Path.DirectorySeparatorChar` (a `char`) — but in THIS file
      no such bare-separator use exists.

  - construct_key: dart.top_level_function.named_required_chain_module_returns_typeenvironment_layered_merge
    source_form: >-
      "TypeEnvironment assembleTypeScope({ required List<String> chain,
      required ast.Module module, }) { var env =
      buildPreludeEnvironment(); for (final selfGlpPath in chain) {
      final source = File(selfGlpPath).readAsStringSync(); final lexer
      = Lexer(source); final tokens = lexer.tokenize(); final parser =
      Parser(tokens); final selfModule = parser.parseModule(); final
      selfTemplates = <String, TypeDef>{}; for (final td in
      selfModule.typeDefs) { if (td.isParameterized) { selfTemplates[
      td.name] = td; } } final expandedSelfModule =
      expandParameterizedTypes(selfModule, knownTypeNames: env.types
      .keys.toSet(), externalTemplates: env.typeTemplates); final
      selfEnv = buildScopeFromModule(expandedSelfModule); env = env
      .merge(TypeEnvironment(selfEnv.types, selfEnv.procedures,
      paramProcDecls: selfEnv.paramProcDecls, typeTemplates:
      selfTemplates)); } final expandedModule =
      expandParameterizedTypes(module, knownTypeNames: env.types.keys
      .toSet(), externalTemplates: env.typeTemplates); final moduleEnv
      = buildScopeFromModule(expandedModule); env = env.merge(
      moduleEnv); return env; }"
    target_decision: >-
      Emit `public static TypeEnvironment AssembleTypeScope(
      IReadOnlyList<string> chain, Ast.Module module)` on the
      hosting `ModuleHierarchy` static class. Named-required params
      → positional with no defaults (same rationale as
      `DiscoverSelfChain`). The Dart `ast.Module` prefixed reference
      → C# `Ast.Module` via the `using Ast = Glp.Compiler.Ast;`
      alias established at the file's `using` declarations. Body
      statement-for-statement: `var env = TypeEnvironmentBuilder
      .BuildPreludeEnvironment();` (per
      type_environment_builder.dart.md, the top-level Dart
      `buildPreludeEnvironment` becomes a `public static`
      method on the hosting class — invoked via class-qualified
      reference). The `for (final selfGlpPath in chain)` loop maps
      to `foreach (var selfGlpPath in chain)`. Inside:
      `File(selfGlpPath).readAsStringSync()` →
      `File.ReadAllText(selfGlpPath)` (Microsoft Learn: "ReadAllText
      opens a text file, reads all the text in the file into a
      string, and then closes the file" — synchronous, identical
      semantics). `Lexer(source)` → `new Lexer(source)` (per
      lexer.dart.md). `lexer.tokenize()` → `lexer.Tokenize()`.
      `Parser(tokens)` → `new Parser(tokens)`. `parser.parseModule()`
      → `parser.ParseModule()` returning `Ast.Module`. The
      `final selfTemplates = <String, TypeDef>{};` map literal +
      for-in population → `var selfTemplates = new Dictionary<string,
      TypeDef>();` followed by `foreach (var td in
      selfModule.TypeDefs) { if (td.IsParameterized) {
      selfTemplates[td.Name] = td; } }` (Dart `[key] = value` and
      C# `Dictionary[key] = value` have identical insert-or-replace
      semantics). `expandParameterizedTypes(selfModule, knownTypeNames:
      env.types.keys.toSet(), externalTemplates: env.typeTemplates)`
      → `ParamExpansion.ExpandParameterizedTypes(selfModule,
      knownTypeNames: new HashSet<string>(env.Types.Keys),
      externalTemplates: env.TypeTemplates)` — the Dart
      `Map<K, V>.keys.toSet()` idiom (returns a `Set<K>` of the map's
      keys) is the .NET counterpart of `new HashSet<string>(
      dict.Keys)` (constructs a hash-set from an enumerable; Microsoft
      Learn: "HashSet<T>(IEnumerable<T>) — Initializes a new instance
      of the HashSet<T> class that contains elements copied from
      the specified collection"). C# named-argument syntax preserves
      the Dart named-call shape one-to-one. `buildScopeFromModule(
      expandedSelfModule)` → `BuildScopeFromModule(expandedSelfModule)`
      (the third top-level function below, hosted on the same
      `ModuleHierarchy` static class). The TypeEnvironment merge
      `env = env.merge(TypeEnvironment(selfEnv.types,
      selfEnv.procedures, paramProcDecls: selfEnv.paramProcDecls,
      typeTemplates: selfTemplates));` → `env = env.Merge(new
      TypeEnvironment(selfEnv.Types, selfEnv.Procedures,
      paramProcDecls: selfEnv.ParamProcDecls, typeTemplates:
      selfTemplates));` — the `TypeEnvironment` ctor uses Dart
      named-required AND named-optional parameters per
      type_ast.dart.md; the C# render uses named-argument syntax.
      After the loop, the same shape repeats for the target module
      itself (`expandedModule` + `moduleEnv` + final `env.Merge`).
      Return `env`.
    idiom_id: null
    research_finding_id: rf-dart-map-literal-to-csharp-dictionary-and-keys-toset-to-hashset
    nuance: >-
      Map-literal nuance (explicitly addressed): Dart `<K, V>{}` is
      an empty MUTABLE `LinkedHashMap<K, V>` (insertion-ordered);
      C# `new Dictionary<TKey, TValue>()` is a mutable hash-map
      WITHOUT insertion-order guarantee. The Dart source iterates
      `selfTemplates` only via `selfTemplates[td.name] = td` (insert/
      replace by key) — no iteration order is observed in this
      file's logic, so the order-difference is NOT load-bearing for
      THIS construct. (If a future caller iterates the map and
      depends on insertion order, the SPEC would require an
      `OrderedDictionary<string, TypeDef>` or an explicit
      ordered-iteration discipline; flagged-and-handled but not
      required here.) Key-set extraction nuance (explicitly
      addressed): Dart `Map<K, V>.keys.toSet()` returns a `Set<K>`
      copy (api.dart.dev: "The keys of this map" + "toSet creates a
      Set with the same elements"); the .NET counterpart is `new
      HashSet<K>(dict.Keys)` (Microsoft Learn HashSet<T> ctor takes
      an `IEnumerable<T>`). Both produce a snapshot copy, NOT a
      live view — important because the caller mutates `env` after
      the call (the snapshot must remain stable). File-read nuance
      (explicitly addressed): Dart `File(p).readAsStringSync()` →
      .NET `File.ReadAllText(p)`. Both are SYNCHRONOUS, both default
      to UTF-8 (Dart `readAsStringSync()` defaults to `utf8`;
      `File.ReadAllText(p)` overload with single string parameter
      uses UTF-8 by default per Microsoft Learn). Encoding identity
      is load-bearing for spec parsing — UTF-8 byte-identical input
      MUST be preserved. Named-argument-on-ctor nuance: the
      `TypeEnvironment(selfEnv.types, selfEnv.procedures,
      paramProcDecls: ..., typeTemplates: ...)` shape mixes
      positional and named arguments — C# supports this directly
      (positional arguments come first, named after). Carry-forward
      from type_environment_builder.dart.md for the TypeEnvironment
      ctor surface. Pipeline-composition nuance: the Lexer → Parser
      → parseModule pipeline matches the lexer.dart.md / parser.dart.md
      / ast.dart.md ctor surfaces; cross-file consistency required.
      Async nuance: ABSENT — every call (file read, lexer, parser,
      type expansion, environment build) is SYNCHRONOUS in Dart
      and likewise in the .NET render. The well-known
      Stream→IAsyncEnumerable nuance does NOT apply.

  - construct_key: dart.top_level_function.positional_module_param_returns_typeenvironment_three_maps_population
    source_form: >-
      "TypeEnvironment buildScopeFromModule(ast.Module module) { final
      types = <String, TypeDef>{}; final procedures = <String,
      ProcDecl>{}; final paramProcDecls = <String, ProcDecl>{};
      for (final typeDef in module.typeDefs) { types[typeDef.name] =
      typeDef; } for (final procDecl in module.procDeclarations) {
      procedures[procDecl.qualifiedKey] = procDecl; } for (final
      paramDecl in module.paramProcDecls) { paramProcDecls[paramDecl
      .qualifiedKey] = paramDecl; } return TypeEnvironment(types,
      procedures, paramProcDecls: paramProcDecls); }"
    target_decision: >-
      Emit `public static TypeEnvironment BuildScopeFromModule(
      Ast.Module module)` on the same hosting static class. POSITIONAL
      parameter (no Dart `{required ...}` wrapping) → ordinary C#
      positional parameter, identical surface. Body: three
      `Dictionary<string, TypeDef>` / `Dictionary<string, ProcDecl>`
      initialisations + three `foreach` loops populating each map
      via key-assignment. Maps: `var types = new Dictionary<string,
      TypeDef>();`, `var procedures = new Dictionary<string,
      ProcDecl>();`, `var paramProcDecls = new Dictionary<string,
      ProcDecl>();`. Loops: `foreach (var typeDef in
      module.TypeDefs) { types[typeDef.Name] = typeDef; }` and the
      two analogous loops on `ProcDeclarations` and `ParamProcDecls`.
      Each Dart `instance.qualifiedKey` getter → C# `instance
      .QualifiedKey` property (per ast.dart.md ProcDecl). Return
      `new TypeEnvironment(types, procedures, paramProcDecls:
      paramProcDecls)` — named-argument for the named-optional
      `paramProcDecls` parameter on the TypeEnvironment ctor (the
      `typeTemplates` named parameter is OMITTED here, allowing its
      default — typically `null` or an empty map per type_ast.dart.md;
      the omission is faithful to the Dart source which also omits
      it).
    idiom_id: null
    research_finding_id: rf-dart-map-literal-to-csharp-dictionary-and-keys-toset-to-hashset
    nuance: >-
      Reuse-of-prior-idiom nuance (FR-012 / SC-007): this construct
      reuses `rf-dart-map-literal-to-csharp-dictionary-and-keys-toset-
      to-hashset` for the three Dictionary initialisations + key-
      assignment population loops (no toSet here, but the
      Dictionary half of the idiom applies). Doc-comment nuance:
      the function's leading doc-comment block explicitly contrasts
      `buildScopeFromModule` with `_buildEnvironmentFromModule` in
      `type_environment_builder.dart` ("this does NOT check for
      predefined type redefinition (because shadowing ancestor types
      is allowed) and does NOT resolve aliases (that happens after
      all scopes are assembled)") — this is the LOAD-BEARING
      semantic-divergence comment that MUST be preserved verbatim
      in the C# XML doc; without it a reader would think the two
      functions are interchangeable. Named-optional-omission
      nuance: the Dart ctor call omits `typeTemplates:` allowing
      its default; the C# call likewise omits it — both languages
      treat omitted named args as defaulted, semantics match.
      Insertion-or-replace nuance: `map[k] = v` is INSERT-OR-
      REPLACE in both languages; if the input module has two
      typeDefs with the same name, the last wins on both sides
      (no exception). Async: ABSENT. Null-safety: all input
      collections (TypeDefs, ProcDeclarations, ParamProcDecls) are
      non-nullable Dart `List`s; .NET render uses non-nullable
      `IReadOnlyList<TypeDef>` / `IReadOnlyList<ProcDecl>` under
      enabled NRT.

  - construct_key: dart.while_true_loop_with_two_break_conditions_walking_directory_tree
    source_form: >-
      "while (true) { var currentNorm = Directory(currentDir).absolute
      .path; var rootNorm = Directory(root).absolute.path; if
      (currentNorm.endsWith('/')) currentNorm = currentNorm.substring(
      0, currentNorm.length - 1); if (rootNorm.endsWith('/')) rootNorm
      = rootNorm.substring(0, rootNorm.length - 1); if
      (!currentNorm.startsWith(rootNorm)) { break; } final selfGlp =
      File('$currentDir${Platform.pathSeparator}self.glp'); if (selfGlp
      .existsSync()) { chain.add(selfGlp.absolute.path); } if
      (currentNorm == rootNorm) { break; } currentDir = Directory(
      currentDir).parent.path; }"
    target_decision: >-
      Emit `while (true) { ... }` with statement-for-statement
      translation. Substring-strip translates to C# range-indexer
      `s[..^1]` (C# 8+, Microsoft Learn: "ranges and indices") which
      is the idiomatic equivalent of Dart `.substring(0, length - 1)`.
      `string.StartsWith(other, StringComparison.Ordinal)` preserves
      Dart's culture-independent prefix check. The
      `currentNorm == rootNorm` value-equality on `string` is
      identical in both languages (C# `string` overrides `==` for
      value equality). `break;` statements are control-flow-
      identical. The structure of the loop — strip-and-compare,
      check-and-append, walk-up — is preserved verbatim.
    idiom_id: null
    research_finding_id: rf-dart-while-true-with-break-to-csharp-while-true-with-break
    nuance: >-
      Loop-shape nuance: Dart `while (true) { ... break; }` is
      semantically identical to C# `while (true) { ... break; }` —
      both are unbounded loops with explicit `break` termination.
      No `for`/`foreach`-with-range variant is more idiomatic for
      this filesystem walk (the walk's termination depends on
      filesystem state, not on a known iteration count). String-
      mutation nuance (LOAD-BEARING, explicitly addressed): Dart
      `String` is IMMUTABLE; the reassignment `currentNorm = currentNorm
      .substring(0, currentNorm.length - 1)` rebinds the LOCAL
      variable to a NEW string instance. C# `string` is likewise
      immutable; `currentNorm = currentNorm[..^1]` rebinds to a
      new instance via the range-indexer. Both languages: no
      shared-state mutation, just local-variable rebinding —
      semantics identical. Trailing-slash-strip nuance: see the
      Filesystem-API construct above — LITERAL `'/'` strip is
      preserved (do NOT replace with `Path.DirectorySeparatorChar`,
      which would change Windows behaviour). Path-normalisation
      nuance: every iteration recomputes `currentNorm` / `rootNorm`
      via `Path.GetFullPath` — slightly redundant for `rootNorm`
      (constant across iterations) but matches the Dart source
      exactly; the SPEC PRESERVES the redundancy (the codegen
      stage MAY later hoist `rootNorm` outside the loop as a
      micro-optimisation, but the SPEC records the literal
      Dart-source order; performance is not a hot-path concern —
      the loop runs once per compile, bounded by directory depth).
      No early-return shortcut is faithful — the Dart logic
      depends on the exact order of (strip → compare → check-self.glp
      → compare-equal-stop → walk-up).
conversion_units:
  - "namespace declaration Glp.Runtime (mirrors lib/runtime/); file-header XML doc carries the Dart triple-slash header (Module hierarchy: self.glp chain discovery + type scope assembly, citing docs/modules/glp-module-system-spec.md Sections 2-3.1-3.2-3.3)"
  - "public static class ModuleHierarchy (hosting type for the three file-level functions; no instance state)"
  - "  using directives at file top: using System.IO; using System.Linq; using System.Collections.Generic; using Glp.Compiler; using Glp.Analysis.TypeChecker; using Ast = Glp.Compiler.Ast;"
  - "  public static IReadOnlyList<string> DiscoverSelfChain(string targetFile, string rootDir) — Dart named-required → C# positional; Path.GetFullPath for both inputs; Path.GetFileName for split-last; if targetName == \"self.glp\" then startDir = Path.GetDirectoryName(Path.GetDirectoryName(target)) else startDir = Path.GetDirectoryName(target); var chain = new List<string>(); var currentDir = startDir; while (true) { var currentNorm = Path.GetFullPath(currentDir); var rootNorm = Path.GetFullPath(root); if (currentNorm.EndsWith(\"/\")) currentNorm = currentNorm[..^1]; if (rootNorm.EndsWith(\"/\")) rootNorm = rootNorm[..^1]; if (!currentNorm.StartsWith(rootNorm, StringComparison.Ordinal)) break; var selfGlp = Path.Combine(currentDir, \"self.glp\"); if (File.Exists(selfGlp)) chain.Add(Path.GetFullPath(selfGlp)); if (currentNorm == rootNorm) break; currentDir = Path.GetDirectoryName(currentDir); } return Enumerable.Reverse(chain).ToList();"
  - "  public static TypeEnvironment AssembleTypeScope(IReadOnlyList<string> chain, Ast.Module module) — var env = TypeEnvironmentBuilder.BuildPreludeEnvironment(); foreach (var selfGlpPath in chain) { var source = File.ReadAllText(selfGlpPath); var lexer = new Lexer(source); var tokens = lexer.Tokenize(); var parser = new Parser(tokens); var selfModule = parser.ParseModule(); var selfTemplates = new Dictionary<string, TypeDef>(); foreach (var td in selfModule.TypeDefs) if (td.IsParameterized) selfTemplates[td.Name] = td; var expandedSelfModule = ParamExpansion.ExpandParameterizedTypes(selfModule, knownTypeNames: new HashSet<string>(env.Types.Keys), externalTemplates: env.TypeTemplates); var selfEnv = BuildScopeFromModule(expandedSelfModule); env = env.Merge(new TypeEnvironment(selfEnv.Types, selfEnv.Procedures, paramProcDecls: selfEnv.ParamProcDecls, typeTemplates: selfTemplates)); } var expandedModule = ParamExpansion.ExpandParameterizedTypes(module, knownTypeNames: new HashSet<string>(env.Types.Keys), externalTemplates: env.TypeTemplates); var moduleEnv = BuildScopeFromModule(expandedModule); env = env.Merge(moduleEnv); return env;"
  - "  public static TypeEnvironment BuildScopeFromModule(Ast.Module module) — var types = new Dictionary<string, TypeDef>(); var procedures = new Dictionary<string, ProcDecl>(); var paramProcDecls = new Dictionary<string, ProcDecl>(); foreach (var typeDef in module.TypeDefs) types[typeDef.Name] = typeDef; foreach (var procDecl in module.ProcDeclarations) procedures[procDecl.QualifiedKey] = procDecl; foreach (var paramDecl in module.ParamProcDecls) paramProcDecls[paramDecl.QualifiedKey] = paramDecl; return new TypeEnvironment(types, procedures, paramProcDecls: paramProcDecls);"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-docblock-triple-slash-to-csharp-xml-doc — file-header doc-comment (cached idiom, reuse)

- Deep analysis: 9-line triple-slash header citing `docs/modules/glp-module-system-spec.md` Sections 2-3 (four sub-section references). No `library;` directive follows. The doc-block must reach the C# target file's hosting type (or namespace, if no host class) as XML-doc.
- Authoritative Dart (cached): https://dart.dev/effective-dart/documentation — `///` doc-comments are the dartdoc-consumed form.
- Authoritative .NET (cached): https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc — `///` XML-doc is the documented C# documentation surface.
- Conclusion: doc-block migrates byte-identically (same sigil, same content), attached to the C# hosting `public static class ModuleHierarchy`. Cited spec-section strings preserved verbatim (load-bearing for grep-based navigation). FR-024 cache hit; no new research.

### rf-dart-dart-io-to-csharp-system-io — dart:io → System.IO (cached + extended for Platform.pathSeparator)

- Deep analysis: this file exercises `File(p).existsSync()`, `File(p).readAsStringSync()`, `Directory(p).absolute.path`, `Directory(p).parent.path`, `File(p).parent.path`, `File(p).parent.parent.path`, and `Platform.pathSeparator`. All seven uses are synchronous; all are filesystem or path-component operations.
- Authoritative Dart: https://api.dart.dev/stable/dart-io/dart-io-library.html (`File`, `Directory`, `Platform` documented as the OS-interaction surface), https://api.dart.dev/stable/dart-io/Platform/pathSeparator.html ("The path separator used by the operating system to separate components in file paths").
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.io.file (File.Exists, File.ReadAllText), https://learn.microsoft.com/en-us/dotnet/api/system.io.path (Path.GetFullPath, Path.GetDirectoryName, Path.GetFileName, Path.Combine, Path.DirectorySeparatorChar). Microsoft Learn `Path.Combine`: "Combines two strings into a path … inserts the directory-separator character between the arguments if necessary." Microsoft Learn `Path.GetFullPath`: "Returns the absolute path for the specified path string." Microsoft Learn `Path.GetDirectoryName`: "Returns the directory information for the specified path." Microsoft Learn `Path.GetFileName`: "Returns the file name and extension."
- Conclusion: seven-way mapping table — (1) `File(p).existsSync()` → `File.Exists(p)`; (2) `File(p).readAsStringSync()` → `File.ReadAllText(p)`; (3) `Directory(p).absolute.path` / `File(p).absolute.path` → `Path.GetFullPath(p)`; (4) `Directory(p).parent.path` / `File(p).parent.path` → `Path.GetDirectoryName(p)`; (5) `File(p).parent.parent.path` → `Path.GetDirectoryName(Path.GetDirectoryName(p))`; (6) `path.split(Platform.pathSeparator).last` → `Path.GetFileName(path)`; (7) `'$dir${Platform.pathSeparator}name'` → `Path.Combine(dir, name)`. Cached idiom for (1) and (2) (carry-forward from repl_play_runner.dart.md and runtime.dart.md); (3)-(7) are first-seen in the convspec corpus and registered as new mappings under the same finding. Authoritative both sides; no escalation.

### rf-dart-import-as-prefix-to-csharp-using-alias-namespace — `import '...' as ast;` (NEW finding, LOAD-BEARING)

- Deep analysis: one of six imports uses a `as ast;` prefix (`import 'package:glp_runtime/compiler/ast.dart' as ast;`). Every reference to types from that library uses the prefix (`ast.Module module`). The other five imports are bare. This is the first file in the convspec corpus to exercise a prefixed import.
- Authoritative Dart: https://dart.dev/language/libraries#specifying-a-library-prefix — "Use the `as` keyword to specify a library prefix when you import a library. … you can refer to libraries using a prefix to distinguish between the libraries' contents."
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive — "You can also use a using directive to create an alias for a namespace or a type. … `using Project = PC.MyCompany.Project;` then `Project.MyClass mc = new Project.MyClass();`."
- Conclusion: Dart library prefix `as ast;` ↔ C# namespace alias `using Ast = Glp.Compiler.Ast;`. Every Dart `ast.<Symbol>` reference becomes C# `Ast.<Symbol>`. The alias form is the NAMESPACE alias (covers every symbol in the aliased namespace), NOT a type alias (would only expose one symbol). Authoritative both sides; no escalation. NEW idiom registered.

### rf-dart-named-required-params-to-csharp-positional-params — `{required T x, required T y}` (NEW finding, LOAD-BEARING)

- Deep analysis: two of three top-level functions use Dart named-required parameter syntax. `discoverSelfChain({required String targetFile, required String rootDir})` and `assembleTypeScope({required List<String> chain, required ast.Module module})`. Callers MUST pass each argument by name (Dart compile-error otherwise). C# 4 supports named arguments at the call site for any parameter; C# 11 `required` keyword applies to PROPERTIES only, not parameters.
- Authoritative Dart: https://dart.dev/language/functions#named-parameters — "When you call a function, you can specify named arguments using `paramName: value`. … `required` indicates the named parameter must always be provided."
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments — "Named arguments enable you to specify an argument for a parameter by matching the argument with its name rather than with its position in the parameter list." Microsoft Learn `required` (C# 11): "The required keyword on a property indicates that the property must be initialized" — does NOT apply to parameters.
- Conclusion: Dart `{required T x}` → C# ordinary positional `T x` parameter with no default. Compile-site mandatoriness is identical (caller must supply). C# 4 named-argument syntax `Method(x: a, y: b)` preserves Dart's named-call readability. Rejected alternatives: (a) parameter `[Required]` attribute — runtime-only, NOT compile-enforced; (b) `params` — irrelevant (variadic, not named-required); (c) overload chain (`Method(T x) => Method(x, default)`) — manufactures default values Dart does not have. Authoritative both sides; no escalation. NEW idiom registered.

### rf-dart-platform-pathseparator-to-csharp-path-combine — `'$dir${Platform.pathSeparator}name'` (NEW finding)

- Deep analysis: one interpolation site composes a path component: `File('$currentDir${Platform.pathSeparator}self.glp')`. The Dart `Platform.pathSeparator` is a `String` field (always length-1: `'\\'` on Windows, `'/'` on POSIX).
- Authoritative Dart: https://api.dart.dev/stable/dart-io/Platform/pathSeparator.html — "The path separator used by the operating system to separate components in file paths."
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.io.path.combine — "Combines two strings into a path. … When at least one of the strings to combine is not null or empty, you do not need to add a separator at the end of the first string." Also https://learn.microsoft.com/en-us/dotnet/api/system.io.path.directoryseparatorchar (`char`, platform-native).
- Conclusion: the idiomatic .NET counterpart is `Path.Combine(currentDir, "self.glp")` — NOT manual concatenation with `Path.DirectorySeparatorChar`, because `Path.Combine` is the Microsoft-Learn-recommended primitive AND it gracefully handles the trailing-separator-on-left case. Semantics match Dart's platform-correct interpolation. Authoritative both sides; no escalation. NEW idiom registered.

### rf-dart-map-literal-to-csharp-dictionary-and-keys-toset-to-hashset — `<K,V>{}` + `map.keys.toSet()` (NEW finding)

- Deep analysis: four `<String, TypeDef>{}` / `<String, ProcDecl>{}` map-literal initialisations across the file; one `env.types.keys.toSet()` Set-from-keys idiom inside `assembleTypeScope` (passed as the `knownTypeNames` snapshot to `expandParameterizedTypes`).
- Authoritative Dart: https://dart.dev/language/collections — "Maps are unordered. … A map literal looks like a JSON object literal. … `var nobleGases = {2: 'helium', ...};`." Empty typed map: `<K, V>{}`. https://api.dart.dev/stable/dart-core/Map/keys.html ("The keys of this map") + https://api.dart.dev/stable/dart-core/Iterable/toSet.html ("Creates a Set with the same elements as this iterable").
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2 — `Dictionary<TKey, TValue>` is the documented hash-map (`new Dictionary<string, TypeDef>()`); insert-or-replace via `dict[key] = value`. https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1.-ctor#system-collections-generic-hashset-1-ctor(system-collections-generic-ienumerable((-0))) — "Initializes a new instance of the HashSet<T> class that contains elements copied from the specified collection."
- Conclusion: Dart `<K, V>{}` → C# `new Dictionary<K, V>()`. Dart `dict[k] = v` (insert-or-replace) → C# `dict[k] = v` (identical semantics, last-write-wins). Dart `map.keys.toSet()` → C# `new HashSet<K>(dict.Keys)` (snapshot copy on both sides). Insertion-order divergence (LinkedHashMap vs Dictionary) is flagged but not load-bearing for THIS file's use sites. Authoritative both sides; no escalation. NEW idiom registered.

### rf-dart-while-true-with-break-to-csharp-while-true-with-break — unbounded loop with break (NEW finding, minor)

- Deep analysis: one `while (true) { ... }` loop with TWO `break` exits — one for "walked past root", one for "reached root after appending". Loop body interleaves filesystem state (Directory parent walk) with normalisation and comparison.
- Authoritative Dart: https://dart.dev/language/loops — "`while` and `do while` loops repeatedly evaluate a condition before executing a body."
- Authoritative .NET: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/iteration-statements#the-while-statement — "The while statement executes a statement or a block of statements while a specified Boolean expression evaluates to true."
- Conclusion: Dart `while (true) { ... break; }` ↔ C# `while (true) { ... break; }` — syntactic and semantic twin. No `for`-with-range counterpart is more idiomatic here because the loop's termination depends on filesystem state (existsSync) and string comparison (currentNorm == rootNorm), not on a known iteration count. Authoritative both sides; no escalation. NEW idiom registered.

## Notes

- This file is SYNCHRONOUS throughout. NO `async` / `await` / `Future` / `Stream` / `Isolate` / `Completer`. The well-known Dart-Stream → C# IAsyncEnumerable nuance is correctly NOT asserted — the code does not exercise it (same discipline as boot_loader.dart.md / external_io.dart.md).
- This file is FILE-LEVEL FUNCTIONS, NOT a class. All three public exports are top-level Dart functions. The faithful C# render hosts them on a `public static class ModuleHierarchy` (carry-forward from external_io.dart.md). No instance state, no constructors, no inheritance.
- No `==` / `hashCode` overrides; no custom exception types; no callbacks; no generic-parameterised method signatures. The file is a pure procedural module — three functions composing types defined elsewhere (Lexer, Parser, Module, TypeEnvironment, TypeDef, ProcDecl).
- Load-bearing semantic decisions for THIS file: (a) Platform-separator → `Path.Combine` (NOT manual concat with DirectorySeparatorChar); (b) Trailing-slash strip is LITERAL `'/'`, NOT `Path.DirectorySeparatorChar` — load-bearing for Windows behaviour preservation; (c) Path-component primitives map one-to-one to BCL Path methods (GetFullPath / GetDirectoryName / GetFileName); (d) Named-required parameters → ordinary positional C# parameters with no defaults; (e) `as ast;` prefix → `using Ast = ...;` namespace alias; (f) `chain.reversed.toList()` → `Enumerable.Reverse(chain).ToList()`; (g) `map.keys.toSet()` → `new HashSet<K>(dict.Keys)`; (h) cross-file ctor surfaces (Lexer, Parser, TypeEnvironment) inherited from lexer.dart.md / parser.dart.md / type_environment_builder.dart.md.
- Trivial / non-construct elements: triple-slash doc-comments on each public function (`/// Discover the self.glp chain ...`, `/// Assemble the type scope ...`, `/// Build a TypeEnvironment from a Module's ...`) migrate mechanically to C# XML-doc; `var` for locals maps to C# `var` (same role); `final` for locals maps to C# `var` (the immutability is implicit local-binding immutability, NOT load-bearing for non-mutated locals) — explicit `readonly` is NOT required for method-local variables. Comments inside method bodies (numbered steps, "Walk up", "Reverse: ...") migrate as `//` line comments.
- Zero escalations. Every non-trivial construct is grounded in official Dart and/or .NET documentation. Two cached idioms reused (doc-block migration, dart:io → System.IO subset) and FIVE new idioms registered (`as`-prefix → using alias; named-required → positional; pathSeparator → Path.Combine; map literal → Dictionary + keys.toSet → new HashSet; while(true)+break twin). FR-009 / FR-010 quality bar satisfied: every non-trivial construct has BOTH a deep-analysis basis AND a researched-pattern basis (research_finding_id), with explicit nuance-addressing per FR-024.
