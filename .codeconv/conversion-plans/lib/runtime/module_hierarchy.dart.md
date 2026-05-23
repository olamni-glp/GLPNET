---
path: lib/runtime/module_hierarchy.dart
cycle_group_id: 44
scc_siblings: []
generated_at: 2026-05-21T16:18:59Z
source_sha256: db87dd95891c91cf5d37ba3d1e17349102b04226388a6a81e45f00fe59513298
schema_version: 1
---

# Conversion Plan: lib/runtime/module_hierarchy.dart

## 1. Source Analysis

Source file inspected at `glp_runtime_net/lib/runtime/module_hierarchy.dart` (175 lines, sha256 `db87dd95891c91cf5d37ba3d1e17349102b04226388a6a81e45f00fe59513298`). The file is a pure-procedural module exporting three top-level free functions that together implement GLP's directory-based module hierarchy per `docs/modules/glp-module-system-spec.md` Sections 2–3.3:

- **Lines 1–9**: triple-slash file-header doc-block citing the spec and four sub-sections (Directory-based hierarchy, Implicit ancestor scoping, Shadowing, Sibling isolation). No `library;` directive follows.
- **Lines 11–17**: six imports — one `dart:io` (for `File`, `Directory`, `Platform`) plus five `package:glp_runtime/...` imports. The fourth import is the only PREFIXED one (`as ast;`), used at every reference site (`ast.Module`); the other five are bare.
- **Lines 32–85** `discoverSelfChain`: named-required-params (`{required String targetFile, required String rootDir}`) → `List<String>`. Normalises both inputs via `Directory(_).absolute.path` / `File(_).absolute.path`, derives `targetName` via `target.split(Platform.pathSeparator).last`, branches on `targetName == 'self.glp'` (sets `startDir` to GRANDPARENT — `File(target).parent.parent.path`) vs other (parent), then runs an unbounded `while (true)` loop with TWO break conditions: (a) `!currentNorm.startsWith(rootNorm)` (walked past root); (b) `currentNorm == rootNorm` (reached root, evaluated AFTER appending). Loop body strips a LITERAL `'/'` trailing slash (NOT `Platform.pathSeparator` — load-bearing for Windows), checks `File('$dir${Platform.pathSeparator}self.glp').existsSync()`, appends `selfGlp.absolute.path` to `chain`, walks up via `Directory(currentDir).parent.path`. Returns `chain.reversed.toList()` (reverse target-to-root collection into root-first order).
- **Lines 98–148** `assembleTypeScope`: named-required-params (`{required List<String> chain, required ast.Module module}`) → `TypeEnvironment`. Starts with `buildPreludeEnvironment()`, then for each `selfGlpPath` in `chain` reads the file (`readAsStringSync`), runs the Lexer→Parser→parseModule pipeline, extracts parameterized `TypeDef`s into a fresh `selfTemplates` Map, calls `expandParameterizedTypes(selfModule, knownTypeNames: env.types.keys.toSet(), externalTemplates: env.typeTemplates)`, builds a `selfEnv` via `buildScopeFromModule`, and merges via `env.merge(new TypeEnvironment(selfEnv.types, selfEnv.procedures, paramProcDecls: selfEnv.paramProcDecls, typeTemplates: selfTemplates))`. After the loop, repeats the same expand+build+merge on the target `module`. Returns `env`.
- **Lines 156–174** `buildScopeFromModule`: ONE positional param (`ast.Module module`) → `TypeEnvironment`. Initialises three empty maps (`types`, `procedures`, `paramProcDecls`), populates each via three `for-in` loops (key-assignment `m[k] = v` insert-or-replace), returns `TypeEnvironment(types, procedures, paramProcDecls: paramProcDecls)` (omits `typeTemplates` named-optional, allowing default). The doc-comment explicitly contrasts THIS function with `_buildEnvironmentFromModule` in `type_environment_builder.dart` — the contrast (no predefined-redefinition check, no alias resolution) is LOAD-BEARING semantic documentation.

All seven `dart:io` uses are SYNCHRONOUS (`existsSync`, `readAsStringSync`, `.absolute.path` getters, `.parent.path` getters, `Platform.pathSeparator`). No `async` / `await` / `Future` / `Stream` / `Isolate` / `Completer`. No `==` / `hashCode` overrides; no custom exception types; no callbacks; no generic-parameterised methods. The file is a pure procedural module composing types defined elsewhere (Lexer, Parser, Module, TypeEnvironment, TypeDef, ProcDecl).

## 2. Dart → C#/.NET Conversion Plan

### Hosting type and namespace

- **C# namespace**: `Glp.Runtime` (mirrors `lib/runtime/`).
- **Hosting class**: `public static class ModuleHierarchy` — carry-forward of the static-host-class idiom from `external_io.dart.md`. No instance state.
- **File-header XML doc**: the 9-line triple-slash header migrates byte-identically as `///` XML-doc attached to `class ModuleHierarchy` (sigil identical, content identical, four cited spec-section strings — "Section 2", "Section 3.1", "Section 3.2", "Section 3.3" — preserved verbatim for diagnostic grep).
- **No `library;` to elide** — this file omits the directive (in contrast to `suspension.dart` / `external_io.dart`). The elision idiom does NOT apply.

### Using directives at file top

```
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Glp.Analysis.TypeChecker;
using Glp.Compiler;
using Ast = Glp.Compiler.Ast;
```

- `dart:io` → `using System.IO;` (covers `File`, `Directory` analogues + `Path` for separators).
- `System.Linq` for `Enumerable.Reverse(...)`.
- `System.Collections.Generic` for `List<T>`, `Dictionary<K,V>`, `HashSet<T>`.
- Five `package:glp_runtime/...` imports collapse into TWO bare `using`s (`Glp.Compiler`, `Glp.Analysis.TypeChecker`) plus ONE NAMESPACE-ALIAS `using Ast = Glp.Compiler.Ast;`. The alias form is the namespace alias (covers every symbol in the aliased namespace), NOT a type alias (would silently narrow Dart's library-wide prefix surface).

### Construct-by-construct (mirrors convspec §`constructs:`)

**(a) File-header doc-block (`dart.docblock_triple_slash_file_header_citing_external_spec`)** — verbatim migration to C# `///` XML-doc on `class ModuleHierarchy`. Cited spec-section strings preserved.

**(b) `dart:io` import (`dart.import_directive.dart_io_to_csharp_using_system_io`)** — `using System.IO;`. `Platform.pathSeparator` routes to `Path.DirectorySeparatorChar` (a `char`), reached via the same `using`. No `using System.Runtime.InteropServices` needed.

**(c) Five package imports (`dart.import_directive.package_internal_to_using_namespace_six_imports`)** — `using Glp.Compiler;` (lexer.dart, parser.dart sibling) + `using Glp.Analysis.TypeChecker;` (type_ast.dart, param_expansion.dart, type_environment_builder.dart) + `using Ast = Glp.Compiler.Ast;` (the load-bearing `as ast;` prefix maps to a namespace alias). Every Dart `ast.Module` becomes C# `Ast.Module`.

**(d) `DiscoverSelfChain` (`dart.top_level_function.named_required_params_string_return_list_string_returns_filesystem_walk`)**:

```
public static IReadOnlyList<string> DiscoverSelfChain(string targetFile, string rootDir)
{
    // Normalize paths
    var root = Path.GetFullPath(rootDir);
    var target = Path.GetFullPath(targetFile);
    var targetName = Path.GetFileName(target);

    // Determine the starting directory for the walk.
    // If the target IS self.glp, start from its parent (don't include itself).
    // Otherwise, start from the target's directory.
    string startDir;
    if (targetName == "self.glp")
    {
        // Target is self.glp — start from its parent directory
        startDir = Path.GetDirectoryName(Path.GetDirectoryName(target));
    }
    else
    {
        // Target is a regular module — start from its directory
        startDir = Path.GetDirectoryName(target);
    }

    // Walk from startDir up to root, collecting self.glp files
    var chain = new List<string>();
    var currentDir = startDir;

    while (true)
    {
        // Normalize for comparison, stripping trailing slashes for consistency
        var currentNorm = Path.GetFullPath(currentDir);
        var rootNorm = Path.GetFullPath(root);
        if (currentNorm.EndsWith("/")) currentNorm = currentNorm[..^1];
        if (rootNorm.EndsWith("/")) rootNorm = rootNorm[..^1];

        // Check if we've gone above the root
        if (!currentNorm.StartsWith(rootNorm, StringComparison.Ordinal))
        {
            break;
        }

        var selfGlp = Path.Combine(currentDir, "self.glp");
        if (File.Exists(selfGlp))
        {
            chain.Add(Path.GetFullPath(selfGlp));
        }

        // If we've reached the root, stop
        if (currentNorm == rootNorm)
        {
            break;
        }

        // Walk up
        currentDir = Path.GetDirectoryName(currentDir);
    }

    // Reverse: we collected from target-to-root, but want root-first
    return Enumerable.Reverse(chain).ToList();
}
```

Notes (all per convspec):
- Dart `{required ...}` → C# positional with no default (compile-site mandatory; C# 4 named-argument syntax preserves caller-readable named-call shape).
- Return `List<String>` → `IReadOnlyList<string>` (consumer treats read-only; carry-forward).
- `Directory(p).absolute.path` / `File(p).absolute.path` → `Path.GetFullPath(p)`.
- `path.split(Platform.pathSeparator).last` → `Path.GetFileName(p)` (single BCL call, semantically equivalent).
- `File(p).parent.parent.path` → `Path.GetDirectoryName(Path.GetDirectoryName(p))`.
- `File(p).parent.path` → `Path.GetDirectoryName(p)`.
- Trailing-slash strip uses LITERAL `"/"` — NOT `Path.DirectorySeparatorChar` (load-bearing for Windows behaviour preservation per convspec).
- `string.StartsWith(other, StringComparison.Ordinal)` preserves Dart's culture-independent ordinal comparison.
- `'$dir${Platform.pathSeparator}self.glp'` → `Path.Combine(currentDir, "self.glp")`.
- `chain.reversed.toList()` → `Enumerable.Reverse(chain).ToList()` (Microsoft Learn LINQ `Reverse`).

**(e) `AssembleTypeScope` (`dart.top_level_function.named_required_chain_module_returns_typeenvironment_layered_merge`)**:

```
public static TypeEnvironment AssembleTypeScope(IReadOnlyList<string> chain, Ast.Module module)
{
    // Start with prelude
    var env = TypeEnvironmentBuilder.BuildPreludeEnvironment();

    // Layer each self.glp in order (root first, children shadow parents)
    foreach (var selfGlpPath in chain)
    {
        var source = File.ReadAllText(selfGlpPath);
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var selfModule = parser.ParseModule();

        // Extract templates from this self.glp before expansion removes them.
        // These chain to descendant modules so they can expand references.
        var selfTemplates = new Dictionary<string, TypeDef>();
        foreach (var td in selfModule.TypeDefs)
        {
            if (td.IsParameterized)
            {
                selfTemplates[td.Name] = td;
            }
        }

        // Expand parameterized types before building scope
        // Pass accumulated env type names so earlier types aren't mistaken for type params.
        // Pass ancestor templates so this self.glp can expand references to prelude templates.
        var expandedSelfModule = ParamExpansion.ExpandParameterizedTypes(
            selfModule,
            knownTypeNames: new HashSet<string>(env.Types.Keys),
            externalTemplates: env.TypeTemplates);

        // Build environment from this self.glp (without prelude check — ancestors
        // can define types with same names, shadowing is allowed)
        var selfEnv = BuildScopeFromModule(expandedSelfModule);

        // Merge: later entries overwrite earlier ones (shadowing).
        // Include this self.glp's templates in the environment for descendants.
        env = env.Merge(new TypeEnvironment(
            selfEnv.Types, selfEnv.Procedures,
            paramProcDecls: selfEnv.ParamProcDecls,
            typeTemplates: selfTemplates));
    }

    // Finally, merge the target module's own definitions (shadows all ancestors)
    var expandedModule = ParamExpansion.ExpandParameterizedTypes(
        module,
        knownTypeNames: new HashSet<string>(env.Types.Keys),
        externalTemplates: env.TypeTemplates);
    var moduleEnv = BuildScopeFromModule(expandedModule);
    env = env.Merge(moduleEnv);

    return env;
}
```

Notes:
- `buildPreludeEnvironment()` → `TypeEnvironmentBuilder.BuildPreludeEnvironment()` (per type_environment_builder.dart.md, top-level Dart function hosted on a static class).
- `File(p).readAsStringSync()` → `File.ReadAllText(p)` (synchronous, UTF-8 default both sides — encoding identity load-bearing for spec parsing).
- `Lexer(source)` / `Parser(tokens)` → `new Lexer(source)` / `new Parser(tokens)` (per lexer.dart.md / parser.dart.md).
- `<String, TypeDef>{}` → `new Dictionary<string, TypeDef>()`; insert-or-replace via `m[k] = v` identical semantics both sides.
- `env.types.keys.toSet()` → `new HashSet<string>(env.Types.Keys)` (snapshot copy, NOT live view — caller mutates `env` after).
- `expandParameterizedTypes` → `ParamExpansion.ExpandParameterizedTypes` (per param_expansion.dart.md).
- `TypeEnvironment` ctor uses mixed positional+named arguments; C# supports this directly (positional first, named after).

**(f) `BuildScopeFromModule` (`dart.top_level_function.positional_module_param_returns_typeenvironment_three_maps_population`)**:

```
/// Build a TypeEnvironment from a Module's types and procedure declarations.
///
/// Unlike _buildEnvironmentFromModule in type_environment_builder.dart,
/// this does NOT check for predefined type redefinition (because shadowing
/// ancestor types is allowed) and does NOT resolve aliases (that happens
/// after all scopes are assembled).
public static TypeEnvironment BuildScopeFromModule(Ast.Module module)
{
    var types = new Dictionary<string, TypeDef>();
    var procedures = new Dictionary<string, ProcDecl>();
    var paramProcDecls = new Dictionary<string, ProcDecl>();

    foreach (var typeDef in module.TypeDefs)
    {
        types[typeDef.Name] = typeDef;
    }

    foreach (var procDecl in module.ProcDeclarations)
    {
        procedures[procDecl.QualifiedKey] = procDecl;
    }

    foreach (var paramDecl in module.ParamProcDecls)
    {
        paramProcDecls[paramDecl.QualifiedKey] = paramDecl;
    }

    return new TypeEnvironment(types, procedures, paramProcDecls: paramProcDecls);
}
```

Notes:
- Single positional param → ordinary C# positional, identical surface.
- Three `Dictionary` initialisations + three `foreach` loops; `instance.qualifiedKey` getter → `instance.QualifiedKey` property (per ast.dart.md `ProcDecl`).
- `typeTemplates:` named-optional OMITTED at the ctor call site — faithful to Dart source which also omits it (default applies on both sides).
- The doc-comment contrast with `_buildEnvironmentFromModule` is LOAD-BEARING and MUST be preserved verbatim in the C# XML doc.

**(g) Path-composition (`dart.string_interpolation_path_composition_with_platform_separator`)** — `Path.Combine(currentDir, "self.glp")`. Microsoft Learn `Path.Combine` is the documented platform-portable counterpart of Dart's interpolation; ALSO handles trailing-separator-on-left, which manual concat with `Path.DirectorySeparatorChar` would not.

**(h) `while (true) { ... break; }` loop (`dart.while_true_loop_with_two_break_conditions_walking_directory_tree`)** — statement-for-statement translation. `.substring(0, length - 1)` → C# range-indexer `s[..^1]` (C# 8+). `currentNorm == rootNorm` value equality on `string` identical both sides. The redundant per-iteration `rootNorm` recomputation is PRESERVED (codegen MAY hoist later as micro-opt; SPEC records literal Dart-source order — not a hot path).

### Cross-file dependencies (informational; not generated here)

- `Glp.Compiler.Lexer` (ctor `(string source)`, method `IReadOnlyList<Token> Tokenize()`) — per lexer.dart.md.
- `Glp.Compiler.Parser` (ctor `(IReadOnlyList<Token> tokens)`, method `Ast.Module ParseModule()`) — per parser.dart.md.
- `Glp.Compiler.Ast.Module` with `TypeDefs`, `ProcDeclarations`, `ParamProcDecls` collection properties — per ast.dart.md.
- `Glp.Analysis.TypeChecker.TypeDef` with `Name`, `IsParameterized` properties — per type_ast.dart.md.
- `Glp.Analysis.TypeChecker.ProcDecl` with `QualifiedKey` property — per type_ast.dart.md.
- `Glp.Analysis.TypeChecker.TypeEnvironment` ctor `(IReadOnlyDictionary<string,TypeDef> types, IReadOnlyDictionary<string,ProcDecl> procedures, paramProcDecls: ..., typeTemplates: ...)`; methods `Merge(TypeEnvironment)`, properties `Types`, `Procedures`, `ParamProcDecls`, `TypeTemplates` — per type_ast.dart.md.
- `Glp.Analysis.TypeChecker.ParamExpansion.ExpandParameterizedTypes(Ast.Module module, HashSet<string> knownTypeNames, IReadOnlyDictionary<string,TypeDef> externalTemplates)` — per param_expansion.dart.md.
- `Glp.Analysis.TypeChecker.TypeEnvironmentBuilder.BuildPreludeEnvironment()` — per type_environment_builder.dart.md.

### Null-safety (NRT enabled)

All inputs are non-nullable Dart types under Dart sound null-safety; all C# renders are non-nullable counterparts (`string`, `IReadOnlyList<string>`, `Ast.Module`, `TypeEnvironment`) under NRT-enabled compile. No `?` on any signature.

## 3. Decomposed Task Units

- T1: emit `Glp.Runtime` namespace + file-header XML-doc + `public static class ModuleHierarchy` host type.
- T2: emit using directives — `System.Collections.Generic`, `System.IO`, `System.Linq`, `Glp.Compiler`, `Glp.Analysis.TypeChecker`, `Ast = Glp.Compiler.Ast` (namespace alias).
- T3: emit `public static IReadOnlyList<string> DiscoverSelfChain(string targetFile, string rootDir)` per §2(d), including the literal `"/"` trailing-slash strip and the `Path.Combine` interpolation site.
- T4: emit `public static TypeEnvironment AssembleTypeScope(IReadOnlyList<string> chain, Ast.Module module)` per §2(e), composing the Lexer→Parser→ParseModule pipeline + `selfTemplates` Dictionary + `ExpandParameterizedTypes` call + `Merge` shape.
- T5: emit `public static TypeEnvironment BuildScopeFromModule(Ast.Module module)` per §2(f), including the LOAD-BEARING contrast XML-doc (vs `_buildEnvironmentFromModule`).
- T6: preserve per-function method-level XML-doc blocks (three docstrings) and inline body comments (numbered-step comments, "Walk up", "Reverse: ...") byte-for-byte as C# `///` and `//`.
- T7: confirm cross-file ctor/property surfaces against lexer.dart.md / parser.dart.md / ast.dart.md / type_ast.dart.md / param_expansion.dart.md / type_environment_builder.dart.md plans before codegen consumes this plan.

## 4. Research Findings

None required. The convspec at `.codeconv/conversion-specs/lib/runtime/module_hierarchy.dart.md` already carries the full provenance for every non-trivial construct: seven `research_finding_id`s, each backed by both authoritative-Dart and authoritative-.NET citations (`dart.dev`, `api.dart.dev`, `learn.microsoft.com`). Two cached idioms reused (doc-block migration; `dart:io` → `System.IO` subset) and five new idioms registered (`as`-prefix → using alias; named-required → positional; `pathSeparator` → `Path.Combine`; map literal → `Dictionary` + `keys.toSet` → `new HashSet`; `while(true)` + break twin). FR-024 cache hits documented inline. This plan mirrors the convspec without introducing new claims.

## 5. Consistency Pass

Fixed — derived from `.codeconv/conversion-specs/lib/runtime/module_hierarchy.dart.md` (ratified mirror, sha256 match `db87dd95891c91cf5d37ba3d1e17349102b04226388a6a81e45f00fe59513298`). Every construct, target-decision, idiom_id, research_finding_id, and nuance in §2 above is verbatim-derivable from the convspec's `constructs:` array and the seven `### rf-...` rationale sections. Cross-file ctor/property surfaces (Lexer, Parser, Module, TypeEnvironment, TypeDef, ProcDecl, ParamExpansion, TypeEnvironmentBuilder) are inherited from the respective sibling convspecs (lexer.dart.md, parser.dart.md, ast.dart.md, type_ast.dart.md, param_expansion.dart.md, type_environment_builder.dart.md) per the convspec's §Notes carry-forward discipline. Zero new claims; zero deviations from the convspec.

## 6. Escalations

None.
