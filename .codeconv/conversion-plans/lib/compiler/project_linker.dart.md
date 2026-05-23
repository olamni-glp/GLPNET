---
path: lib/compiler/project_linker.dart
cycle_group_id: 45
scc_siblings: []
generated_at: 2026-05-21T16:29:43Z
source_sha256: b3d11b764d4963e6d78f28841aa9bafd9e3032ca39c0457a7340d56180957a52
schema_version: 1
---

# Conversion Plan: lib/compiler/project_linker.dart

## 1. Source Analysis

Inspected `glp_runtime_net/lib/compiler/project_linker.dart` (481 lines, sha256 `b3d11b76…57a52`). The file is the GLP project linker — a three-stage static linker over directory-rooted multi-module GLP projects, plus six private helpers.

**Structural inventory**:

- **Lines 1-9** — triple-slash doc-block declaring file purpose ("Project linker: static linking of multi-module GLP projects."), the discover → type-check → flat-Program narrative, and two grep-target doc citations (`docs/modules/glp-project-compilation-spec.md`, `docs/modules/project-compilation-implementation-plan.md`); followed by bare `library;` directive (no library name).
- **Lines 11-20** — SEVEN imports: `dart:io` (uses Directory, File, Platform.pathSeparator); four sibling `lib/compiler/` relative imports (ast, lexer, parser, partial_evaluator); three cross-package imports under `lib/analysis/type_checker/` (type_ast, param_expansion, type_checker); one `lib/runtime/module_hierarchy.dart`; one `lib/analysis/type_checker/type_environment_builder.dart` (re-exported by module_hierarchy; consumed transitively by `_buildAncestorScope`). No `as`-prefix; no `show`/`hide`.
- **Lines 22-37** — `class DiscoveredModule` — five-field immutable data class: `String filePath`, `String moduleName`, `Module ast`, `TypeEnvironment ancestorScope`, `bool isSelfGlp`. Ctor: four `required this.x` named params + one named-default `this.isSelfGlp = false`.
- **Lines 39-45** — `class LinkResult` — two-field immutable: `Program program`, `List<ProcDecl> procDeclarations`. Positional ctor `LinkResult(this.program, this.procDeclarations)`.
- **Lines 47-116** — `discoverProject(String rootDir, {String? rootSelfGlpPath})` returning `List<DiscoveredModule>`. Body: `Directory(rootDir).existsSync()` guard + `ArgumentError`; `root.listSync(recursive: true).whereType<File>().where((f) => f.path.endsWith('.glp')).toList()`; per-file exclusion loop with `boot_direct.glp` / `mad_boot.glp` filename matches and DUAL-separator parent-dir `.endsWith('${Platform.pathSeparator}mad_boot')` OR literal `.endsWith('/mad_boot')` check; `File.readAsStringSync` + Lexer + Parser + parseModule pipeline; null-coalescing `module.name ?? (filename == 'self.glp' ? _moduleNameFromDirPath(file.parent.path) : _moduleNameFromFilename(filename))`; named-arg call to `discoverSelfChain(targetFile: file.absolute.path, rootDir: root.absolute.path)`; inner-helper `_buildAncestorScope(chain, rootSelfGlpPath: rootSelfGlpPath)`; per-module emit `modules.add(DiscoveredModule(filePath: …, moduleName: …, ast: …, ancestorScope: …, isSelfGlp: filename == 'self.glp'))`.
- **Lines 118-149** — `typeCheckProject(List<DiscoveredModule> modules)`. Two early-skip conditions per mod (`mod.ast.procDeclarations.isEmpty` and `!mod.ast.procDeclarations.any((d) => !d.imported)`); per-mod `Program(mod.ast.procedures, mod.ast.line, mod.ast.column)` + `PartialEvaluator()` + `transformDefinedGuards(program)` + `checkModule(mod.ast, transformedProcedures: transformed.procedures, ancestorScope: mod.ancestorScope)`; on `!result.isWellTyped` aggregate via `result.errors.map((e) => '  ${e.message} at line ${e.line}').join('\n')` and throw bare `Exception('Type checking failed for ${mod.moduleName} (${mod.filePath}):\n$errors')`.
- **Lines 151-316** — `linkProject(List<DiscoveredModule> modules, String topModuleName)` returning `LinkResult`. Five consecutive phases:
  - **Phase 1 (lines 160-167)** — `<String, Set<String>> registry`: per-mod, build set of `${proc.name}/${proc.arity}` sigs.
  - **Phase 2 (lines 169-198)** — `<String, Map<String, String>> ancestorSelfProcs`: per-mod, find self-glp ancestors (using `File(mod.filePath).parent.absolute.path.startsWith(...)`), `identical(s, mod)` self-skip, sort path-length-DESCENDING via `..sort((a,b) => b.filePath.length.compareTo(a.filePath.length))`, then `procs.putIfAbsent(sig, () => selfMod.moduleName)` inner-most-wins.
  - **Phase 3 (lines 200-243)** — Per-mod clause rewrite: `<Procedure> allProcedures` accumulator; for each `Procedure` build `renamedName = '${mod.moduleName}:${proc.name}'`; for each `Clause` build renamed `Atom` head (`'${mod.moduleName}:${clause.head.functor}'`, args/line/column passed through); body via null-conditional `clause.body?.map((g) => _resolveGoal(g, mod.moduleName, localSigs, modAncestorProcs)).toList()`; emit new `Clause(renamedHead, guards: clause.guards, body: resolvedBody, line: clause.line, column: clause.column)`; emit new `Procedure(renamedName, proc.arity, renamedClauses, proc.line, proc.column)`.
  - **Phase 4 (lines 246-255)** — `<String, ProcDecl> declIndex`: per-mod, per non-imported `ProcDecl` `d`, `declIndex.putIfAbsent('${d.name}/${d.arity}', () => d)` (first-wins).
  - **Phase 5 (lines 258-298)** — Entry-point aliases: `<String, String> aliasedSigs` tracking owner; per-mod isTop-or-exported check (`mod.ast.procDeclarations.any((d) => d.exported && d.name == proc.name && d.arity == proc.arity)`); `aliasedSigs.containsKey(sig)` skip-if-already (top wins); `_findProcDecl(mod, proc.name, proc.arity) ?? declIndex[sig]` decl lookup; emit `Procedure(proc.name, proc.arity, [aliasClause], 0, 0)` wrapping a single-clause list with the aliasClause from `_makeAliasClause(...)`.
  - **Final (lines 300-315)** — Build `<ProcDecl> allDecls` (per non-imported decl, renamed `'${mod.moduleName}:${decl.name}'` with `argTypes`/`line`/`column` passed through and `isBuiltin: decl.isBuiltin` named-arg); return `LinkResult(Program(allProcedures, 0, 0), allDecls)`.
- **Lines 318-373** — `Goal _resolveGoal(Goal goal, String moduleName, Set<String> localSigs, Map<String, String> ancestorSelfProcs)` — three-branch dispatch. `goal is RemoteGoal` arm: if `goal.staticModuleName != null`, return new `Goal('$targetModule:${goal.goal.functor}', goal.goal.args, goal.line, goal.column)`; else return original `goal`. `goal is SpawnGoal` arm: recursively `_resolveGoal` the `goal.innerGoal`; if `!identical(resolvedInner, goal.innerGoal)`, return new `SpawnGoal(resolvedInner, goal.agentId, goal.line, goal.column)`; else return original `goal`. Else: check `localSigs.contains(sig)` → new `Goal('$moduleName:${goal.functor}', ...)`; check `ancestorSelfProcs[sig]` → new `Goal('$ancestorModule:${goal.functor}', ...)`; else return original `goal`. Identity-preserving on no-op.
- **Lines 375-381** — `ProcDecl? _findProcDecl(DiscoveredModule mod, String name, int arity)` — `for` loop early-return on `(!d.imported && d.name == name && d.arity == arity)`; else `null`.
- **Lines 383-418** — `Clause _makeAliasClause(String name, int arity, String targetName, {ProcDecl? declaration})`. Zero-arity fast-path: `Atom(name, [], 0, 0)` + `[Goal(targetName, [], 0, 0)]` + `Clause(head, body: body, line: 0, column: 0)`. N-arity: `headArgs = List.generate(arity, (i) => VarTerm('V$i', false, 0, 0) as Term)`; `bodyArgs = List.generate(arity, (i) { final isInput = declaration != null && i < declaration.argTypes.length ? declaration.isInputArg(i) : true; return VarTerm('V$i', isInput, 0, 0) as Term; })`; emit `Atom(name, headArgs, 0, 0)` head + `[Goal(targetName, bodyArgs, 0, 0)]` body + `Clause(head, body: body, line: 0, column: 0)`.
- **Lines 420-426** — `_moduleNameFromFilename(String filename)`: if `.endsWith('.glp')` return `filename.substring(0, filename.length - 4)`; else filename.
- **Lines 428-432** — `_moduleNameFromDirPath(String dirPath)`: `dirPath.split(Platform.pathSeparator).last`.
- **Lines 434-480** — `_buildAncestorScope(List<String> chain, {String? rootSelfGlpPath})` returning `TypeEnvironment`. STRUCTURAL SUBSET of `module_hierarchy.dart`'s `assembleTypeScope` (per-self.glp loop body, omits final module-itself merge). `var env = buildPreludeEnvironment();`; per `selfGlpPath` in chain: `File.readAsStringSync` + Lexer + Parser + parseModule; build `<String, TypeDef> selfTemplates` from `selfModule.typeDefs` filtered `isParameterized`; call `expandParameterizedTypes(selfModule, knownTypeNames: env.types.keys.toSet(), externalTemplates: env.typeTemplates)`; `buildScopeFromModule(expandedSelfModule)`; `env = env.merge(TypeEnvironment(selfEnv.types, selfEnv.procedures, paramProcDecls: selfEnv.paramProcDecls, typeTemplates: selfTemplates))`. `rootSelfGlpPath` parameter is received but NOT USED in the body (chain already includes root self.glp transitively via prelude — explicit doc comment in source).

**Synchronous throughout.** No `async`/`await`/`Future`/`Stream`/`Isolate`/`Completer`. No mutation of external state.

**Identity discipline**: AST sub-trees (args lists, guards, lines, columns) are passed-through by reference into the renamed Atom/Goal/Clause/Procedure constructors — only the enclosing wrapper nodes are newly allocated. `identical(s, mod)` and `identical(resolvedInner, goal.innerGoal)` are reference-equality primitives used as change-detection mechanisms.

## 2. Dart → C#/.NET Conversion Plan

Translation per construct, mirroring convspec ratified decisions.

### File header (lines 1-9)
- Dart triple-slash doc-block + `library;` → C# `///` XML-doc block attached to the hosting `public static class ProjectLinker` declaration in namespace `Glp.Compiler`. Two cited paths preserved byte-identically (grep targets).
- `library;` directive ELIDED in C# (carry-forward `rf-dart-library-directive-to-csharp-namespace-elision`).

### Imports (lines 11-20)
- `dart:io` → `using System.IO;` (provides File, Directory, Path).
- Four sibling `lib/compiler/` imports (ast, lexer, parser, partial_evaluator) → ZERO `using`s (same-namespace `Glp.Compiler` cross-file visibility).
- Three `lib/analysis/type_checker/` imports (type_ast, param_expansion, type_checker) → ONE `using Glp.Analysis.TypeChecker;`.
- `lib/runtime/module_hierarchy.dart` + `lib/analysis/type_checker/type_environment_builder.dart` → `using Glp.Runtime;` + already covered by `using Glp.Analysis.TypeChecker;`.
- `using Ast = Glp.Compiler.Ast;` alias added — required because the renamed `ModuleAst` property's TYPE is `Ast.Module` after the property/namespace-name collision resolution (see DiscoveredModule below).
- Plus `using System;` (Exception, ArgumentException, StringComparison), `using System.Collections.Generic;` (Dictionary, HashSet, List, IReadOnlyList, IReadOnlyDictionary, IReadOnlyCollection), `using System.Linq;` (Any, Select, OrderByDescending, Where, ToList).

### `class DiscoveredModule` (lines 22-37)
- Emit `public sealed class DiscoveredModule` (NOT `record class` — reference-identity load-bearing for the later `identical(s, mod)` check; a record would override Equals/GetHashCode by value and break the discipline).
- Five public get-only auto-properties: `string FilePath`, `string ModuleName`, `Ast.Module ModuleAst`, `TypeEnvironment AncestorScope`, `bool IsSelfGlp`.
- Property-name-vs-namespace-alias collision: Dart field `ast` (type `Module`) → C# property RENAMED `ModuleAst` (avoid `Ast.Module Ast` ambiguity; consumer expressions `mod.ast.X` become `mod.ModuleAst.X`).
- Positional ctor: `public DiscoveredModule(string filePath, string moduleName, Ast.Module ast, TypeEnvironment ancestorScope, bool isSelfGlp = false)` — four Dart-named-required → C# positional-no-default; one Dart-named-default → C# default-valued positional.
- Call-site at line 106 preserves named-arg shape via C# 4 named-argument syntax.

### `class LinkResult` (lines 39-45)
- Emit `public sealed class LinkResult` with two get-only auto-properties: `Program Program { get; }`, `IReadOnlyList<ProcDecl> ProcDeclarations { get; }` (consumer-narrowed view; producer constructs `List<ProcDecl>` then implicitly upcasts).
- Positional ctor: `public LinkResult(Program program, IReadOnlyList<ProcDecl> procDeclarations) { Program = program; ProcDeclarations = procDeclarations; }`.
- Class (NOT record) for uniformity with DiscoveredModule even though identity is not strictly required.

### `public static class ProjectLinker` host (lines 47-480)
All four top-level functions migrate as `public static` methods plus six `private static` helpers, hosted on a single `public static class ProjectLinker` in the `Glp.Compiler` namespace (carry-forward from `module_hierarchy.dart.md` / `external_io.dart.md` precedent).

#### `DiscoverProject` (lines 47-116)
- Signature: `public static IReadOnlyList<DiscoveredModule> DiscoverProject(string rootDir, string? rootSelfGlpPath = null)`.
- Guard: `if (!Directory.Exists(rootDir)) throw new ArgumentException($"Project root directory not found: {rootDir}");`.
- Recursive walk + extension filter COLLAPSED to one BCL call: `var glpFiles = Directory.EnumerateFiles(rootDir, "*.glp", SearchOption.AllDirectories).ToList();` (kernel-level pattern matching; lazy until `.ToList()`).
- Iteration yields `string filePath` (NOT `FileInfo`); body translations:
  - `file.path` → `filePath`.
  - `file.path.split(Platform.pathSeparator).last` → `Path.GetFileName(filePath)`.
  - `file.parent.path` → `Path.GetDirectoryName(filePath)` (returns nullable `string?`; the predicate guards with `parent != null`).
  - `file.absolute.path` → `Path.GetFullPath(filePath)`.
  - `file.readAsStringSync()` → `File.ReadAllText(filePath)`.
- Exclusion conditionals: `if (filename == "boot_direct.glp") continue;`, `if (filename == "mad_boot.glp") continue;`. Dual-separator parent-dir check: `var parent = Path.GetDirectoryName(filePath); if (parent != null && (parent.EndsWith(Path.DirectorySeparatorChar + "mad_boot", StringComparison.Ordinal) || parent.EndsWith("/mad_boot", StringComparison.Ordinal))) continue;` — preserves BOTH the platform-separator suffix AND the literal-`/` defensive suffix.
- Lexer/Parser/parseModule: `var lexer = new Lexer(source); var tokens = lexer.Tokenize(); var parser = new Parser(tokens); var module = parser.ParseModule();`.
- Module-name derivation: `var moduleName = module.Name ?? (filename == "self.glp" ? ModuleNameFromDirPath(parent!) : ModuleNameFromFilename(filename));`.
- `discoverSelfChain` call: `var chain = ModuleHierarchy.DiscoverSelfChain(targetFile: Path.GetFullPath(filePath), rootDir: Path.GetFullPath(rootDir));` (named-arg syntax preserved).
- `var ancestorScope = BuildAncestorScope(chain, rootSelfGlpPath: rootSelfGlpPath);`.
- Emit: `modules.Add(new DiscoveredModule(filePath: filePath, moduleName: moduleName, ast: module, ancestorScope: ancestorScope, isSelfGlp: filename == "self.glp"));`.

#### `TypeCheckProject` (lines 118-149)
- Signature: `public static void TypeCheckProject(IReadOnlyList<DiscoveredModule> modules)`.
- Per-mod loop with two early-skips:
  - `if (mod.ModuleAst.ProcDeclarations.Count == 0) continue;` (Dart `.isEmpty` → C# `.Count == 0` since underlying is `List<ProcDecl>`).
  - `var hasOwnDecls = mod.ModuleAst.ProcDeclarations.Any(d => !d.Imported); if (!hasOwnDecls) continue;` (LINQ `.Any(p)`).
- Pipeline: `var program = new Program(mod.ModuleAst.Procedures, mod.ModuleAst.Line, mod.ModuleAst.Column); var pe = new PartialEvaluator(); var transformed = pe.TransformDefinedGuards(program); var result = TypeChecker.CheckModule(mod.ModuleAst, transformedProcedures: transformed.Procedures, ancestorScope: mod.AncestorScope);` — named-arg syntax preserved.
- Error path: `if (!result.IsWellTyped) { var errors = string.Join("\n", result.Errors.Select(e => $"  {e.Message} at line {e.Line}")); throw new Exception($"Type checking failed for {mod.ModuleName} ({mod.FilePath}):\n{errors}"); }` — LINQ `.Select` + `string.Join("\n", source)` (separator FIRST per BCL signature). Bare `System.Exception` (preserves source's intent — no typed exception introduced).

#### `LinkProject` (lines 151-316)
- Signature: `public static LinkResult LinkProject(IReadOnlyList<DiscoveredModule> modules, string topModuleName)`.
- **Phase 1**: `var registry = new Dictionary<string, HashSet<string>>(); foreach (var mod in modules) { var sigs = new HashSet<string>(); foreach (var proc in mod.ModuleAst.Procedures) sigs.Add($"{proc.Name}/{proc.Arity}"); registry[mod.ModuleName] = sigs; }`.
- **Phase 2**: 
  - `var selfGlpModules = modules.Where(m => m.IsSelfGlp).ToList();`.
  - `var ancestorSelfProcs = new Dictionary<string, Dictionary<string, string>>();`.
  - Per-mod: `var modDir = Path.GetDirectoryName(Path.GetFullPath(mod.FilePath)) ?? string.Empty; var procs = new Dictionary<string, string>(); var ancestors = selfGlpModules.Where(s => !ReferenceEquals(s, mod) && modDir.StartsWith(Path.GetDirectoryName(Path.GetFullPath(s.FilePath)) ?? string.Empty, StringComparison.Ordinal)).OrderByDescending(s => s.FilePath.Length).ToList();`.
  - `identical(s, mod)` → `ReferenceEquals(s, mod)` (LOAD-BEARING; documented `object.ReferenceEquals` semantics).
  - `..sort((a,b) => b.filePath.length.compareTo(a.filePath.length))` → LINQ `.OrderByDescending(s => s.FilePath.Length)` — Dart cascade mutates in place + returns same list; LINQ returns new `IOrderedEnumerable<T>` materialised via `.ToList()`. Sorted output is consumed immediately by the inner foreach so mutation-vs-new distinction is invisible.
  - Per `selfMod` in ancestors, per `proc`: `var sig = $"{proc.Name}/{proc.Arity}"; if (!procs.ContainsKey(sig)) procs[sig] = selfMod.ModuleName;` (or equivalently `procs.TryAdd(sig, selfMod.ModuleName);` on .NET 6+). LOAD-BEARING — preserves Dart `putIfAbsent(k, () => v)` inner-most-wins semantics; `procs[sig] = v` (overwrite) would BREAK invariant.
  - `ancestorSelfProcs[mod.ModuleName] = procs;`.
- **Phase 3**: `var allProcedures = new List<Procedure>(); foreach (var mod in modules) { var localSigs = registry[mod.ModuleName]; var modAncestorProcs = ancestorSelfProcs.TryGetValue(mod.ModuleName, out var aps) ? aps : new Dictionary<string, string>(); foreach (var proc in mod.ModuleAst.Procedures) { var renamedName = $"{mod.ModuleName}:{proc.Name}"; var renamedClauses = new List<Clause>(); foreach (var clause in proc.Clauses) { var renamedHead = new Atom($"{mod.ModuleName}:{clause.Head.Functor}", clause.Head.Args, clause.Head.Line, clause.Head.Column); IReadOnlyList<Goal>? resolvedBody = clause.Body?.Select(g => ResolveGoal(g, mod.ModuleName, localSigs, modAncestorProcs)).ToList(); renamedClauses.Add(new Clause(renamedHead, guards: clause.Guards, body: resolvedBody, line: clause.Line, column: clause.Column)); } allProcedures.Add(new Procedure(renamedName, proc.Arity, renamedClauses, proc.Line, proc.Column)); } }`.
  - Null-conditional `clause.body?.map(...).toList()` → C# `clause.Body?.Select(g => ResolveGoal(...)).ToList()` — `?.` short-circuits on null; LINQ pipeline becomes no-op.
  - Args/guards pass-through (REFERENCE reuse, no copy) — `clause.Head.Args` and `clause.Guards` passed directly into new Atom/Clause ctors.
- **Phase 4**: `var declIndex = new Dictionary<string, ProcDecl>(); foreach (var mod in modules) foreach (var d in mod.ModuleAst.ProcDeclarations) { if (d.Imported) continue; var sig = $"{d.Name}/{d.Arity}"; if (!declIndex.ContainsKey(sig)) declIndex[sig] = d; }`.
- **Phase 5**: `var aliasedSigs = new Dictionary<string, string>(); foreach (var mod in modules) { var isTop = mod.ModuleName == topModuleName; foreach (var proc in mod.ModuleAst.Procedures) { var sig = $"{proc.Name}/{proc.Arity}"; if (!isTop) { var isExported = mod.ModuleAst.ProcDeclarations.Any(d => d.Exported && d.Name == proc.Name && d.Arity == proc.Arity); if (!isExported) continue; } if (aliasedSigs.ContainsKey(sig)) continue; aliasedSigs[sig] = mod.ModuleName; var decl = FindProcDecl(mod, proc.Name, proc.Arity) ?? (declIndex.TryGetValue(sig, out var d2) ? d2 : null); var aliasClause = MakeAliasClause(proc.Name, proc.Arity, $"{mod.ModuleName}:{proc.Name}", declaration: decl); allProcedures.Add(new Procedure(proc.Name, proc.Arity, new List<Clause> { aliasClause }, 0, 0)); } }`.
  - Dart `declIndex[sig]` (returns null on absent) → C# `declIndex.TryGetValue(sig, out var d2) ? d2 : null` (direct C# indexer would throw KeyNotFoundException).
  - Single-clause list `[aliasClause]` → C# collection initialiser `new List<Clause> { aliasClause }`.
- **Final**: `var allDecls = new List<ProcDecl>(); foreach (var mod in modules) foreach (var decl in mod.ModuleAst.ProcDeclarations) { if (decl.Imported) continue; allDecls.Add(new ProcDecl($"{mod.ModuleName}:{decl.Name}", decl.ArgTypes, decl.Line, decl.Column, isBuiltin: decl.IsBuiltin)); } return new LinkResult(new Program(allProcedures, 0, 0), allDecls);`.

#### `ResolveGoal` (lines 318-373)
- Signature: `private static Goal ResolveGoal(Goal goal, string moduleName, IReadOnlyCollection<string> localSigs, IReadOnlyDictionary<string, string> ancestorSelfProcs)`.
- Three-branch C# 7+ declaration-pattern dispatch:
  - `if (goal is RemoteGoal rg) { var targetModule = rg.StaticModuleName; if (targetModule != null) return new Goal($"{targetModule}:{rg.GoalRef.Functor}", rg.GoalRef.Args, rg.Line, rg.Column); return goal; }` — note Dart `goal.goal.functor` (RemoteGoal's `goal` field) → C# `rg.GoalRef.Functor` (the `goal` field of `RemoteGoal` is renamed `GoalRef` in C# per ast.dart.md to avoid base-type-name collision).
  - `if (goal is SpawnGoal sg) { var resolvedInner = ResolveGoal(sg.InnerGoal, moduleName, localSigs, ancestorSelfProcs); if (!ReferenceEquals(resolvedInner, sg.InnerGoal)) return new SpawnGoal(resolvedInner, sg.AgentId, sg.Line, sg.Column); return goal; }` — `identical` → `ReferenceEquals`; LOAD-BEARING identity-preservation (no-op recursion returns original `goal`).
  - Else: `var sig = $"{goal.Functor}/{goal.Arity}"; if (localSigs.Contains(sig)) return new Goal($"{moduleName}:{goal.Functor}", goal.Args, goal.Line, goal.Column); if (ancestorSelfProcs.TryGetValue(sig, out var ancestorModule)) return new Goal($"{ancestorModule}:{goal.Functor}", goal.Args, goal.Line, goal.Column); return goal;`.

#### `FindProcDecl` (lines 375-381)
- Signature: `private static ProcDecl? FindProcDecl(DiscoveredModule mod, string name, int arity)`.
- Body: `foreach (var d in mod.ModuleAst.ProcDeclarations) { if (!d.Imported && d.Name == name && d.Arity == arity) return d; } return null;` — preserve explicit foreach-with-return form for byte-level fidelity (LINQ `FirstOrDefault` is an acceptable refactor; spec documents source's actual choice).

#### `MakeAliasClause` (lines 383-418)
- Signature: `private static Clause MakeAliasClause(string name, int arity, string targetName, ProcDecl? declaration = null)`.
- Zero-arity fast-path: `if (arity == 0) { var head0 = new Atom(name, Array.Empty<Term>(), 0, 0); var body0 = new List<Goal> { new Goal(targetName, Array.Empty<Term>(), 0, 0) }; return new Clause(head0, body: body0, line: 0, column: 0); }` — `Array.Empty<Term>()` is allocation-free shared instance (Microsoft Learn recommended).
- N-arity: `var headArgs = new Term[arity]; for (int i = 0; i < arity; i++) headArgs[i] = new VarTerm($"V{i}", false, 0, 0); var bodyArgs = new Term[arity]; for (int i = 0; i < arity; i++) { var isInput = declaration != null && i < declaration.ArgTypes.Count ? declaration.IsInputArg(i) : true; bodyArgs[i] = new VarTerm($"V{i}", isInput, 0, 0); } var head = new Atom(name, headArgs, 0, 0); var body = new List<Goal> { new Goal(targetName, bodyArgs, 0, 0) }; return new Clause(head, body: body, line: 0, column: 0);`.
- Dart `as Term` cast ELIDED — `Term[arity]` array declares element type upfront; `VarTerm : Term` so per-element assignment is implicit upcast.
- Preserves SRSW per-arg mode discipline: `isInput == true` → reader annotation on body; `isInput == false` → writer annotation.

#### `ModuleNameFromFilename` (lines 420-426)
- Signature: `private static string ModuleNameFromFilename(string filename)`.
- Body: `if (filename.EndsWith(".glp", StringComparison.Ordinal)) return filename[..^4]; return filename;`.
- Dart `.endsWith(".glp")` → C# `.EndsWith(".glp", StringComparison.Ordinal)` (preserve culture-independent semantics).
- Dart `.substring(0, length - 4)` → C# range-indexer `filename[..^4]` (C# 8+).

#### `ModuleNameFromDirPath` (lines 428-432)
- Signature: `private static string ModuleNameFromDirPath(string dirPath) => Path.GetFileName(dirPath);` — expression-bodied; BCL one-liner.
- Dart `dirPath.split(Platform.pathSeparator).last` → .NET `Path.GetFileName(dirPath)` (returns last path component regardless of platform).

#### `BuildAncestorScope` (lines 434-480)
- Signature: `private static TypeEnvironment BuildAncestorScope(IReadOnlyList<string> chain, string? rootSelfGlpPath = null)`.
- Structural subset of `ModuleHierarchy.AssembleTypeScope` per-self.glp loop body (no final module-itself merge).
- Body: `var env = TypeEnvironmentBuilder.BuildPreludeEnvironment(); foreach (var selfGlpPath in chain) { var source = File.ReadAllText(selfGlpPath); var lexer = new Lexer(source); var tokens = lexer.Tokenize(); var parser = new Parser(tokens); var selfModule = parser.ParseModule(); var selfTemplates = new Dictionary<string, TypeDef>(); foreach (var td in selfModule.TypeDefs) if (td.IsParameterized) selfTemplates[td.Name] = td; var expandedSelfModule = ParamExpansion.ExpandParameterizedTypes(selfModule, knownTypeNames: new HashSet<string>(env.Types.Keys), externalTemplates: env.TypeTemplates); var selfEnv = ModuleHierarchy.BuildScopeFromModule(expandedSelfModule); env = env.Merge(new TypeEnvironment(selfEnv.Types, selfEnv.Procedures, paramProcDecls: selfEnv.ParamProcDecls, typeTemplates: selfTemplates)); } return env;`.
- Dart `env.types.keys.toSet()` → C# `new HashSet<string>(env.Types.Keys)` (cached `rf-dart-map-literal-to-csharp-dictionary-and-keys-toset-to-hashset`).
- `rootSelfGlpPath` parameter received but NOT consumed in body (faithful to source — Dart doc-comment explicitly explains the chain already includes root self.glp transitively via prelude). C# render preserves parameter for API stability; may suppress unused-parameter warning via doc-comment.

## 3. Decomposed Task Units

- **T1** — Emit `namespace Glp.Compiler` declaration + file-header XML doc block (Dart triple-slash → C# `///`, two doc-citation paths preserved byte-identically); `library;` elided. — done.
- **T2** — Emit using directives at top of compilation unit: `System`, `System.IO`, `System.Linq`, `System.Collections.Generic`, `Glp.Analysis.TypeChecker`, `Glp.Runtime`, and `using Ast = Glp.Compiler.Ast;` alias for property-type disambiguation. — done.
- **T3** — Emit `public sealed class DiscoveredModule` with five get-only auto-properties (FilePath, ModuleName, ModuleAst with rename, AncestorScope, IsSelfGlp) and a positional ctor (four required + one default-false); class (NOT record) for ReferenceEquals discipline. — done.
- **T4** — Emit `public sealed class LinkResult` with two get-only auto-properties (Program, ProcDeclarations: IReadOnlyList<ProcDecl>) and a positional ctor. — done.
- **T5** — Emit `public static class ProjectLinker` shell hosting the eight static methods. — done.
- **T6** — Emit `public static IReadOnlyList<DiscoveredModule> DiscoverProject(string rootDir, string? rootSelfGlpPath = null)`: Directory.Exists guard + ArgumentException; Directory.EnumerateFiles + dual-separator exclusion; Lexer + Parser pipeline; null-coalescing module-name; DiscoverSelfChain + BuildAncestorScope invocation; emit list. — done.
- **T7** — Emit `public static void TypeCheckProject(IReadOnlyList<DiscoveredModule> modules)`: per-mod early skips; Program + PartialEvaluator + CheckModule pipeline; LINQ .Select + string.Join error aggregation; throw bare Exception. — done.
- **T8** — Emit `public static LinkResult LinkProject(IReadOnlyList<DiscoveredModule> modules, string topModuleName)` — five-phase rewrite (registry, ancestorSelfProcs with ReferenceEquals + OrderByDescending + ContainsKey-then-assign, per-module clause rewrite, declIndex, alias generation) + final allDecls collection + return new LinkResult(new Program(...), allDecls). — done.
- **T9** — Emit `private static Goal ResolveGoal(Goal goal, string moduleName, IReadOnlyCollection<string> localSigs, IReadOnlyDictionary<string, string> ancestorSelfProcs)`: three-branch declaration-pattern dispatch with ReferenceEquals identity-preservation on SpawnGoal recursion. — done.
- **T10** — Emit `private static ProcDecl? FindProcDecl(DiscoveredModule mod, string name, int arity)` foreach-with-return form. — done.
- **T11** — Emit `private static Clause MakeAliasClause(string name, int arity, string targetName, ProcDecl? declaration = null)`: zero-arity fast-path with Array.Empty<Term>(); N-arity with Term[arity] pre-allocation + indexed for-loop population; per-index ternary mode lookup. — done.
- **T12** — Emit `private static string ModuleNameFromFilename(string filename)`: EndsWith(StringComparison.Ordinal) + range-indexer `[..^4]`. — done.
- **T13** — Emit `private static string ModuleNameFromDirPath(string dirPath) => Path.GetFileName(dirPath);` — expression-bodied BCL one-liner. — done.
- **T14** — Emit `private static TypeEnvironment BuildAncestorScope(IReadOnlyList<string> chain, string? rootSelfGlpPath = null)`: structural subset of ModuleHierarchy.AssembleTypeScope per-self.glp loop; rootSelfGlpPath preserved-but-unused. — done.

## 4. Research Findings

None required — all idioms are grounded in cached research findings from prior convspecs (carry-forward) plus five NEW idioms registered in this file's convspec (already deep-researched with Dart + .NET authoritative citations in the ratified spec; no further research needed).

Cached idiom carry-forwards (8):
- `rf-dart-docblock-triple-slash-to-csharp-xml-doc` (file header).
- `rf-dart-library-directive-to-csharp-namespace-elision` (library; → none).
- `rf-dart-relative-import-to-csharp-using-or-same-namespace` (seven imports → four usings).
- `rf-dart-named-required-and-default-params-to-csharp-positional-default` (DiscoveredModule + MakeAliasClause + DiscoverProject + BuildAncestorScope).
- `rf-dart-final-field-class-to-csharp-getonly-class` (DiscoveredModule + LinkResult).
- `rf-dart-dart-io-to-csharp-system-io` (Directory.Exists, File.ReadAllText, Path.GetFileName, Path.GetDirectoryName, Path.GetFullPath, Path.DirectorySeparatorChar).
- `rf-dart-map-literal-to-csharp-dictionary-and-keys-toset-to-hashset` (five map-literal sites + env.types.keys.toSet).
- `rf-dart-string-substring-zero-length-minus-n-to-csharp-range-indexer` (ModuleNameFromFilename).

NEW idioms registered in this file's convspec (5):
- `rf-dart-directory-listsync-recursive-wheretype-file-to-csharp-directory-enumeratefiles-pattern` — three-stage Dart pipeline collapses to one `Directory.EnumerateFiles(path, "*.glp", SearchOption.AllDirectories)` BCL call (kernel-level pattern match).
- `rf-dart-iterable-any-map-join-to-csharp-linq-any-select-string-join` — error aggregation pipeline (`.any` → `.Any`; `.map` → `.Select`; `.join` → `string.Join` with INVERTED arg order: separator first).
- `rf-dart-map-putifabsent-lambda-to-csharp-containskey-then-assign` — inner-most-wins via `TryAdd` (.NET 6+) or `if (!ContainsKey(k)) [k]=v` (universal); NOT `[k]=v` overwrite.
- `rf-dart-is-pattern-with-typed-binding-and-identical-noop-to-csharp-declaration-pattern-with-referenceequals` — runtime-type dispatch with identity-preservation (`is X x` declaration pattern + `ReferenceEquals`).
- `rf-dart-list-generate-with-as-term-cast-to-csharp-array-loop-or-enumerable-range-select` — `List.generate(n, lambda) as Term` → `Term[n]` array + indexed for-loop; `as Term` cast elided because array element type is declared upfront.
- `rf-dart-linear-scan-first-match-or-null-to-csharp-foreach-or-firstordefault` — explicit foreach with early return preferred over LINQ FirstOrDefault for byte-level fidelity.

## 5. Consistency Pass

All construct decisions derived verbatim from the ratified convspec at `.codeconv/conversion-specs/lib/compiler/project_linker.dart.md`:

- T1 (file header) — derived from convspec `dart.docblock_triple_slash_file_header_with_spec_and_plan_citations_plus_library_directive` construct.
- T2 (imports) — derived from convspec `dart.import_directive.dart_io_plus_five_relative_compiler_and_analysis_and_runtime_imports` construct.
- T3 (DiscoveredModule) — derived from convspec `dart.data_class.discovered_module_immutable_record_five_fields_with_named_required_and_named_default_ctor` construct (including the property-name-vs-namespace-alias collision resolution to `ModuleAst`).
- T4 (LinkResult) — derived from convspec `dart.data_class.linkresult_two_field_immutable_record_with_positional_ctor` construct.
- T5 (ProjectLinker host) — derived from convspec conversion_units bullet #5.
- T6 (DiscoverProject) — derived from convspec `dart.top_level_function.named_optional_string_param_returns_list_dart_io_recursive_walk_with_filtered_skip_logic` construct.
- T7 (TypeCheckProject) — derived from convspec `dart.top_level_function.type_check_per_module_throws_exception_with_per_module_error_report` construct.
- T8 (LinkProject) — derived from convspec `dart.top_level_function.linker_two_phase_rename_registry_build_then_per_module_clause_rewrite_with_inner_helpers` construct.
- T9 (ResolveGoal) — derived from convspec `dart.private_recursive_helper.resolve_goal_runtime_type_dispatch_with_identity_preservation_on_noop` construct.
- T10 (FindProcDecl) — derived from convspec `dart.private_helper.find_proc_decl_linear_scan_with_skip_imported_returns_nullable` construct.
- T11 (MakeAliasClause) — derived from convspec `dart.private_helper.make_alias_clause_mode_aware_argument_forwarding_with_zero_arity_fast_path_and_var_term_generate` construct.
- T12 (ModuleNameFromFilename) — derived from convspec `dart.private_helper.module_name_from_filename_strip_dot_glp_extension` construct.
- T13 (ModuleNameFromDirPath) — derived from convspec `dart.private_helper.module_name_from_dir_path_split_separator_take_last` construct.
- T14 (BuildAncestorScope) — derived from convspec `dart.private_helper.build_ancestor_scope_with_lexer_parser_pipeline_type_expansion_merge_and_template_carry` construct.

Cross-file consistency: every transitive type reference (`Module`, `Procedure`, `Clause`, `Atom`, `Goal`, `RemoteGoal`, `SpawnGoal`, `VarTerm`, `Term`, `ProcDecl`, `Program`, `TypeDef`, `TypeEnvironment`, `PartialEvaluator`, `Lexer`, `Parser`) is honoured per its sibling convspec — ctor surfaces, named-required params, ProcDecl.IsInputArg method signature, Goal/RemoteGoal/SpawnGoal class hierarchy (concrete base + sealed leaves), VarTerm(name, isReader, line, column) ctor, all preserved.

CompileError discipline: not relevant here — this file uses bare `Exception` (preserved as `System.Exception`) and `ArgumentError` (mapped to `ArgumentException`), neither is a domain-specific custom type per the convspec.

All construct decisions are fixed — derived from convspec. No ambiguities surfaced.

## 6. Escalations

None.
