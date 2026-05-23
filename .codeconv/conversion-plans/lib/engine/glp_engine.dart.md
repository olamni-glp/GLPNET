---
path: lib/engine/glp_engine.dart
cycle_group_id: 48
scc_siblings: []
generated_at: 2026-05-21T16:40:11Z
source_sha256: 966bf3b7fa4deb9baca2696f2c221bad3eed61f189de1c7080d409fdcdb5a8df
schema_version: 1
---

# Conversion Plan: lib/engine/glp_engine.dart

## 1. Source Analysis

The file `glp_runtime_net/lib/engine/glp_engine.dart` (1183 lines) defines the embeddable GLP execution core — the ONE way to compile-typecheck-load-run a `.glp` program (used by REPL, IsolateManager for madGLP agent isolates, and tests). Direct inspection confirms:

- **File header** (lines 1-10): file-level `///` doc-comment block followed by bare `library;` directive.
- **Imports** (lines 12-31): 19 directives — `dart:io` plus 18 `package:glp_runtime/...`; ONE aliased `import 'package:glp_runtime/runtime/terms.dart' as rt;` (line 22) — LOAD-BEARING because AST `Term`/`VarTerm`/`ConstTerm`/`ListTerm`/`StructTerm` and runtime `Term`/`VarRef`/`ConstTerm`/`StructTerm` are simultaneously in scope.
- **Two top-level constants** (lines 71-82, 88-113): `const String _serveSource = r'''...''';` and `const String _madPredicatesSource = r'''...''';` — Dart raw triple-quote strings holding embedded `.glp` source that the GlpCompiler lexer parses byte-for-byte.
- **`class ExecutionResult`** (lines 34-48): three `final` fields (`status: ExecutionStatus`, `bindings: Map<String, rt.Term?> = const {}`, `error: String?`), named-required ctor, three computed bool getters `succeeded` / `failed` / `suspended`.
- **`class ModuleInfo`** (lines 51-64): six `final` fields (`name`, `program`, `imports`, `hasExports`, `exportedLabels = const {}`, `isTopLevel = false`), named-required ctor with two defaulted.
- **`class GlpEngine`** (lines 116-1182): central reference class. `final _compiler = GlpCompiler()` and `final _runtime = GlpRuntime()` (inline-initialised); `final Map<String, BytecodeProgram> _loadedPrograms = {}` and `final Map<String, ModuleInfo> _loadedModules = {}`; `late final BytecodeProgram _serveBytecode`; `int _goalId = 1`; four public mutable config flags (`maxCycles = 10000`, `debugTrace = false`, `debugOutput = false`, `strictTypes = true`); `late final String _rootSelfGlpPath`; nullable injection seam `MadContext? madContext`; three getters `runtime`, `loadedPrograms` (returns `Map.unmodifiable(...)`), `serveBytecode`.
- **Constructor** (lines 156-170): named-required `rootSelfGlpPath`; six ordered side-effecting steps (assign, conditional file-read + prelude setters, register-standard-predicates, `_loadRootSelf`, compile `_serveSource`).
- **Method surface**: `clear()` (snapshot-clear-restore), `_loadRootSelf()` (silent try/catch, fresh compiler), `loadFile(path)` (existence-check-throw-delegate), `loadSource(source, {filename})` (six-stage full pipeline: parse → ancestor-scope discovery → PE+type-check → compile → register → auto-activate), `loadProject(projectDir, {topModuleName})` (five-stage: discover → typecheck → detect-top → link → compile), `_detectTopModule` (LINQ filter + fallback mutate-sort-descending), `runGoal(goalText)` (async; strip trailing dot, dispatch conjunction or single, catch-all wrap as failed), `activateDynamicModule(moduleName)` (idempotent: skip-if-already, throw on missing or no-exports, merge with root-self, activate), `enableMadGLP({agentId})` (load embedded predicates + construct MadContext + inject), `combinedProgram` getter (concat ops + filter labels by allowed set per §19.3/§19.6), `_runSingleGoal`, `_runConjunction` (async drains), `_isConjunction` (depth-tracking comma scan), `_extractModuleInfo` (three regex scrapes), `_moduleNameFromFilename`, `_findProjectRoot` (DirectoryInfo walk-up topmost-wins), `_buildAncestorScope`, `_mergeModuleIntoEnv`, `_findModuleForProcedure` (linear scan), `_buildModuleContext` (1-based import index — LOAD-BEARING), `_setupArgument` ↔ `_setupConjunctionArg`, `_buildStructTerm` ↔ `_buildStructTermForConj`, `_buildListTerm` ↔ `_buildListTermForConj` (three pairs of mirrored term-builders).
- **No `==`/`hashCode` overrides anywhere** — identity equality is the explicit Dart choice.
- **Async surface** is limited to `runGoal` / `_runSingleGoal` / `_runConjunction`; everything else is synchronous (including filesystem I/O via `existsSync`/`readAsStringSync`).
- **Threading-model** invariant inherited from heap_fcp.dart.md escalations[0] (single-owning-context per goal); plain `Map`/`Dictionary` is correct, NO concurrent collections needed.

## 2. Dart → C#/.NET Conversion Plan

Each Dart construct → C#/.NET decision, mirroring the convspec verbatim. The 30 yaml `constructs:` entries in the convspec govern the codegen; cross-references to the convspec's `construct_key` are abbreviated below.

1. **`library;` directive** → ELIDED. No .NET counterpart. File-level `///` doc-comments preserved as XML-doc above `namespace lib.engine`. Public top-level identifiers emitted as `public`.

2. **19 imports incl. one aliased `as rt;`** → small set of `using` directives (`System`, `System.IO`, `System.Collections.Generic`, `System.Text.RegularExpressions`, `System.Threading.Tasks`, `System.Linq`, plus project namespaces `<root>.Compiler`, `<root>.Bytecode`, `<root>.Runtime`, `<root>.Analysis.TypeChecker`, `<root>.Multiagent`). The `as rt;` aliased import → FOUR per-symbol type aliases at the top of the file:
   ```
   using RtTerm = <root>.Runtime.Term;
   using RtVarRef = <root>.Runtime.VarRef;
   using RtConstTerm = <root>.Runtime.ConstTerm;
   using RtStructTerm = <root>.Runtime.StructTerm;
   ```
   `dart:io` `File` / `FileSystemException` → `System.IO.File` / `System.IO.FileNotFoundException`.

3. **`_serveSource` const string** → `private const string _ServeSource = @"...";` on `GlpEngine`. C# verbatim string `@"..."` preserves embedded newlines, backslashes, single-quotes. Alternative C# 11.0+ raw-string-literal `"""..."""` acceptable for target framework ≥ .NET 7. Byte-fidelity LOAD-BEARING.

4. **`_madPredicatesSource` const string** → `private const string _MadPredicatesSource = @"...";` on `GlpEngine`. Same byte-fidelity treatment. `%%` GLP-comment markers preserved as SOURCE content (NOT translated to `//`).

5. **`class ExecutionResult`** → `public sealed class ExecutionResult` with three get-only auto-properties (`Status`, `Bindings: IReadOnlyDictionary<string, RtTerm?>`, `Error: string?`), positional ctor with two optional defaults (`bindings` null-coalesced to fresh empty `Dictionary<string, RtTerm?>`, `error` defaults `null`), three computed bool projections `Succeeded` / `Failed` / `Suspended`. NOT a record (identity equality preserved per scheduler.dart.md DrainResult precedent).

6. **`class ModuleInfo`** → `public sealed class ModuleInfo` with six get-only properties (`Name: string`, `Program: BytecodeProgram`, `Imports: IReadOnlyList<string>`, `HasExports: bool`, `ExportedLabels: IReadOnlySet<string>`, `IsTopLevel: bool`), positional ctor with two optional defaults. NOT a record. Doc-comment on `IsTopLevel` preserved as XML-doc `<remarks>`.

7. **`class GlpEngine`** → `public sealed class GlpEngine`:
   - `private readonly GlpCompiler _compiler = new GlpCompiler();`
   - `private readonly GlpRuntime _runtime = new GlpRuntime();`
   - `private readonly Dictionary<string, BytecodeProgram> _loadedPrograms = new();`
   - `private readonly Dictionary<string, ModuleInfo> _loadedModules = new();`
   - `private readonly BytecodeProgram _serveBytecode;` (`late final` → `readonly`, assigned in ctor)
   - `private readonly string _rootSelfGlpPath;` (same)
   - `private int _goalId = 1;`
   - `public int MaxCycles { get; set; } = 10000;`
   - `public bool DebugTrace { get; set; } = false;`
   - `public bool DebugOutput { get; set; } = false;`
   - `public bool StrictTypes { get; set; } = true;`
   - `public <root>.Multiagent.MadContext? MadContext { get; set; }` (full-qualification to resolve name-collision with the property name)
   - `public GlpRuntime Runtime => _runtime;`
   - `public IReadOnlyDictionary<string, BytecodeProgram> LoadedPrograms => new ReadOnlyDictionary<string, BytecodeProgram>(_loadedPrograms);`
   - `public BytecodeProgram ServeBytecode => _serveBytecode;`
   - Identity equality preserved; NOT a record. Threading-model inherited from heap_fcp.dart.md escalations[0] — plain `Dictionary<>` is correct under single-owning-context invariant.

8. **Constructor `GlpEngine({required String rootSelfGlpPath})`** → `public GlpEngine(string rootSelfGlpPath)`. Six steps preserved verbatim:
   ```
   _rootSelfGlpPath = rootSelfGlpPath;
   if (System.IO.File.Exists(_rootSelfGlpPath)) {
       var rootSource = System.IO.File.ReadAllText(_rootSelfGlpPath);
       SetPreludeUnitClauseSource(rootSource);
       SetPreludeEnvironmentSource(rootSource);
   }
   RegisterStandardPredicates(_runtime.SystemPredicates);
   _LoadRootSelf();
   _serveBytecode = _compiler.Compile(_ServeSource);
   ```
   Step ordering LOAD-BEARING (steps 2-3 populate module-level prelude state which step 5 / subsequent `LoadSource` calls consume).

9. **`clear()`** → `public void Clear()`. Three-step COPY-then-clear-then-restore via `TryGetValue("__root_self__", out var rootSelf)`, `_loadedPrograms.Clear()`, `_loadedModules.Clear()`, `if (rootSelf != null) _loadedPrograms["__root_self__"] = rootSelf;`.

10. **`_loadRootSelf()`** → `private void _LoadRootSelf()`. Silent bare `catch { }` (Dart-fidelity — catches anything). FRESH `new GlpCompiler()` local (NOT `_compiler`) preserved verbatim per Dart source's explicit choice.

11. **`loadFile(String path)`** → `public bool LoadFile(string path)`. Existence-check throws `new FileNotFoundException("File not found", path);` (`path` → `fileName` ctor parameter). Then `System.IO.File.ReadAllText(path)` and delegate to `LoadSource(source, filename: path)`.

12. **`loadSource(String source, {String? filename})`** → `public bool LoadSource(string source, string? filename = null)`. Six stages preserved verbatim: (1) parse via `Lexer` + `Parser.ParseModule`; (2) ancestor-scope discovery (skip the three magic-names `"_source_"`, `"__mad_predicates__"`, `"__root_self__"`, then `_FindProjectRoot` + `DiscoverSelfChain` + `_BuildAncestorScope`); (3) if `module.ProcDeclarations.Count > 0`, run `PartialEvaluator.TransformDefinedGuards` + `CheckModule(module, transformedProcedures:..., ancestorScope:...)`; (4) on type-error: `string.Join("\n", typeResult.Errors.Select(e => $"  {e.Message} at line {e.Line}"))`, then `if (StrictTypes) throw new InvalidOperationException($"Type checking failed:\n{errors}");` else `Console.WriteLine($"[TYPE WARNING] Type errors found:\n{errors}");`; (5) `_compiler.Compile(source)` and register both `_loadedPrograms[name] = program` and `_loadedModules[moduleInfo.Name] = moduleInfo`; (6) if `HasExports`, `TryGetValue("__root_self__", out var rootSelf)` and `var moduleBytecode = rootSelf != null ? program.Merge(rootSelf) : program;` and call `ActivateModule(rt: _runtime, serveBytecode: _serveBytecode, moduleBytecode: ..., moduleName: ...)`. Magic-names preserved as string literals.

13. **`loadProject(String projectDir, {String? topModuleName})`** → `public bool LoadProject(string projectDir, string? topModuleName = null)`. Five stages: `DiscoverProject(projectDir, rootSelfGlpPath: _rootSelfGlpPath)`; if `modules.Count == 0` throw `InvalidOperationException`; `TypeCheckProject(modules)`; `var top = topModuleName ?? _DetectTopModule(modules);`; `var linked = LinkProject(modules, top);`; `var program = _compiler.CompileProgram(linked.Program, procDeclarations: linked.ProcDeclarations);`; register as `_loadedPrograms["__project__"]`.

14. **`_detectTopModule(List<DiscoveredModule>)`** → `private string _DetectTopModule(List<DiscoveredModule> modules)`. LINQ `Where(m => m.Ast.ProcDeclarations.Any(d => d.Imported)).ToList()`. If `Count == 1` return `[0].ModuleName`. Else mutate-sort-descending: `modules.Sort((a, b) => b.Ast.Procedures.Count.CompareTo(a.Ast.Procedures.Count));` then `return modules[0].ModuleName;`. In-place mutation preserved (NOT `OrderByDescending`).

15. **`runGoal(String goalText)`** → `public async Task<ExecutionResult> RunGoalAsync(string goalText)`. `try { var trimmed = goalText.Trim(); if (trimmed.EndsWith(".")) trimmed = trimmed.Substring(0, trimmed.Length - 1).Trim(); if (_IsConjunction(trimmed)) return await _RunConjunctionAsync(trimmed); return await _RunSingleGoalAsync(trimmed); } catch (Exception e) { return new ExecutionResult(status: ExecutionStatus.Failed, error: e.ToString()); }`. `-Async` suffix per Microsoft Framework Design Guidelines.

16. **`activateDynamicModule(String)`** → `public void ActivateDynamicModule(string moduleName)`. Idempotency: `if (_runtime.GlpChannels.ContainsKey(moduleName)) return;`. Missing-check: `if (!_loadedModules.TryGetValue(moduleName, out var moduleInfo)) throw new InvalidOperationException($"Module \"{moduleName}\" not loaded");`. No-exports check: `if (!moduleInfo.HasExports) throw new InvalidOperationException($"Module \"{moduleName}\" has no exported procedures");`. Merge + activate: `_loadedPrograms.TryGetValue("__root_self__", out var rootSelf); var moduleBytecode = rootSelf != null ? moduleInfo.Program.Merge(rootSelf) : moduleInfo.Program; ActivateModule(rt: _runtime, serveBytecode: _serveBytecode, moduleBytecode: moduleBytecode, moduleName: moduleName);`.

17. **`enableMadGLP({required String agentId})`** → `public void EnableMadGlp(string agentId)` (acronym `GLP` → `Glp` per .NET capitalisation conventions for three-letter acronyms). Body: `LoadSource(_MadPredicatesSource, filename: "__mad_predicates__"); MadContext = new <root>.Multiagent.MadContext(agentId, _runtime); _runtime.MadContext = MadContext;`. Full-qualification at RHS to resolve property-vs-type name-collision.

18. **`combinedProgram` getter** → `public BytecodeProgram CombinedProgram { get { ... } }` block-bodied getter. Concat ops: `var allOps = new List<object>(); foreach (var loaded in _loadedPrograms.Values) allOps.AddRange(loaded.Ops); var combined = new BytecodeProgram(allOps);`. Build allowed-labels: `var allowedLabels = new HashSet<string>();` then for each of root-self / project (via `TryGetValue` + `foreach k in .Labels.Keys allowedLabels.Add(k)`) and per-module (top-level: all `.Program.Labels.Keys`; non-top-level: `.ExportedLabels`). Prune: collect-then-remove pattern via LINQ: `var keysToRemove = combined.Labels.Where(kvp => !allowedLabels.Contains(kvp.Key)).Select(kvp => kvp.Key).ToList(); foreach (var k in keysToRemove) combined.Labels.Remove(k);`. (`Dictionary<,>` has no `RemoveWhere` — collect-then-remove avoids `InvalidOperationException: Collection was modified`.) Doc-comment spec §19.3 / §19.6 citation preserved as XML-doc `<remarks>`.

19. **`_runSingleGoal(String)`** → `private async Task<ExecutionResult> _RunSingleGoalAsync(string trimmed)`. Parse with appended `.`. Abort-if-empty on `Procedures.Count == 0` and `Clauses.Count == 0`. Extract `proc.Clauses[0].Head` → `functor`/`arity`/`args`. `var program = CombinedProgram; var procedureLabel = $"{functor}/{arity}"; if (!program.Labels.TryGetValue(procedureLabel, out var entryPC)) return new ExecutionResult(status: ExecutionStatus.Failed, error: $"Predicate {procedureLabel} not found");`. Set up `queryVarWriters`/`varNameToId`/`argSlots` via `_SetupArgument`. Wire CallEnv + goal-program-key `"main"` + module-context (if any). Create `BytecodeRunner(program)` + `Scheduler` (with `new Dictionary<string, BytecodeRunner> { { "main", runner } }`). `scheduler.ResetDisplayNumbering(); scheduler.SetQueryVarNames(queryVarWriters); _runtime.Gq.Enqueue(new GoalRef(_goalId, entryPC)); _goalId++;`. Await `scheduler.DrainAsyncWithStatusAsync(maxCycles: MaxCycles, debug: DebugTrace, showBindings: false, debugOutput: DebugOutput);`. Collect bindings via `foreach (var entry in queryVarWriters) { ... if (_runtime.Heap.IsBound(writerId)) bindings[varName] = _runtime.Heap.Dereference(new RtVarRef(writerId)); else bindings[varName] = null; }`. Return `new ExecutionResult(status: result.Status, bindings: bindings);`.

20. **`_runConjunction(String)`** → `private async Task<ExecutionResult> _RunConjunctionAsync(string trimmed)`. Wrap goals in synthetic `_conj_wrapper_ :- {trimmed}.` clause; parse; abort-if-empty on procedures/clauses/body. Build `goals = clause.Body.Select(g => new Atom(g.Functor, g.Args, g.Line, g.Column)).ToList();`. Create scheduler+runner ONCE before loop (reused across all goals). `var allSucceeded = true; var anySuspended = false;` Loop per goal: lookup entry-PC; if missing return Failed; setup argSlots via `_SetupConjunctionArg`; wire env/program/module-context; enqueue; await drain; on `Failed` set `allSucceeded = false; break;` on `Suspended` set `anySuspended = true;`. After loop collect bindings (same as single-goal). Status lattice: `var status = !allSucceeded ? ExecutionStatus.Failed : (anySuspended ? ExecutionStatus.Suspended : ExecutionStatus.Succeeded);`.

21. **`_isConjunction(String)`** → `private bool _IsConjunction(string query)`. Manual depth-tracking scanner over `(`/`)`/`[`/`]` brackets; depth-0 `,` → return `true`. Reserved keyword `char` renamed to `ch`.

22. **`_extractModuleInfo(String, BytecodeProgram, String)`** → `private ModuleInfo _ExtractModuleInfo(string source, BytecodeProgram program, string filename)`. Three regex scrapes via `Regex.Match` / `Regex.Matches` (or `new Regex(...).Matches(...)`). Match `m.Success` replaces null-check; group access `m.Groups[1].Value`. Verbatim `@"..."` regex patterns preserve backslashes. Ordered `List<string>` for `imports` (1-based ImportTable order LOAD-BEARING) with `if (!imports.Contains(moduleName))` dedup. `HashSet<string>` for `exportedLabels`; cross-match each exported functor against `program.Labels.Keys` via `label.StartsWith($"{functor}/")`.

23. **`_moduleNameFromFilename(String)`** → `private string _ModuleNameFromFilename(string filename)`. `filename.Split('/').Last()` (Dart-fidelity forward-slash hard-coding; NOT `Path.GetFileName`). `if (baseName.EndsWith(".glp")) return baseName.Substring(0, baseName.Length - 4);` else return as-is.

24. **`_findProjectRoot(String)`** → `private string? _FindProjectRoot(string filePath)`. DirectoryInfo walk-up: `var dir = new DirectoryInfo(System.IO.Path.GetDirectoryName(filePath) ?? ".");`. Loop: probe `var selfGlp = new FileInfo(System.IO.Path.Combine(dir.FullName, "self.glp")); if (selfGlp.Exists) root = dir.FullName;`. Terminate on `parent == null || parent.FullName == dir.FullName`. Returns the TOPMOST (last-match-wins overwrite) directory with self.glp.

25. **`_buildAncestorScope(List<String>)`** → `private TypeEnvironment _BuildAncestorScope(IReadOnlyList<string> chain)`. `var env = BuildPreludeEnvironment();`. If root self.glp exists, merge: `env = _MergeModuleIntoEnv(env, System.IO.File.ReadAllText(_rootSelfGlpPath));`. Iterate chain, skip entry equal to root self.glp by `FileInfo.FullName` equality, merge each.

26. **`_mergeModuleIntoEnv(TypeEnvironment, String)`** → `private TypeEnvironment _MergeModuleIntoEnv(TypeEnvironment env, string source)`. Parse via `Lexer`+`Parser.ParseModule`. Collect parameterised TypeDef templates BEFORE expansion. Call `ExpandParameterizedTypes(selfModule, knownTypeNames: new HashSet<string>(env.Types.Keys), externalTemplates: env.TypeTemplates);`. Collect types/procs/paramProcs from expanded module. Return `env.Merge(new TypeEnvironment(types, procs, paramProcDecls: paramProcs, typeTemplates: selfTemplates));`.

27. **`_findModuleForProcedure(String)`** → `private ModuleInfo? _FindModuleForProcedure(string procedureLabel)`. Linear scan over `_loadedModules.Values`; first-match-wins; return `null` on no match. Both languages preserve insertion-order iteration.

28. **`_buildModuleContext(ModuleInfo, BytecodeProgram)`** → `private ReplModuleContext? _BuildModuleContext(ModuleInfo module, BytecodeProgram combinedProg)`. Early-return `null` if no imports. Build `Dictionary<int, ReplModuleTarget>` indexed `i + 1` (1-based per `ImportTable.addImport` order — LOAD-BEARING). Gap-tolerant (skips `_loadedModules` misses). Construct `new ReplModuleContext(moduleName: module.Name, imports: imports, combinedProgram: combinedProg, programKey: "main")`.

29. **`_setupArgument(...)`** → `private void _SetupArgument(GlpRuntime runtime, Term arg, int argSlot, IDictionary<int, RtTerm> argSlots, IDictionary<string, int> queryVarWriters, IDictionary<string, int> varNameToId)`. Four-branch pattern-match with declaration: `if (arg is VarTerm varTerm) { ... } else if (arg is ListTerm listArg) { ... } else if (arg is ConstTerm constArg) { ... } else if (arg is StructTerm structArg) { ... } else throw new InvalidOperationException($"Unsupported argument type: {arg.GetType().Name}");`. VarTerm has alias-or-allocate sub-paths via `varNameToId.TryGetValue`; queryVarWriters recorded only when `!varTerm.IsReader`. ListTerm dispatches on returned `RtConstTerm` vs `RtStructTerm` from `_BuildListTerm`. Record-destructuring `var (writerId, readerId) = runtime.Heap.AllocateVariable();`.

30. **`_setupConjunctionArg(...)`** → `private void _SetupConjunctionArg(...)`. Body shape MIRRORED from `_SetupArgument` with delegations to `_BuildListTermForConj` / `_BuildStructTermForConj`. VarTerm branch byte-identical. NOT consolidated with `_SetupArgument` — preserved separate.

31. **`_buildStructTerm(...)`** → `private RtTerm _BuildStructTerm(GlpRuntime runtime, StructTerm structArg, IDictionary<string, int> queryVarWriters, IDictionary<string, int> varNameToId)`. Reserved keyword `struct` renamed `structArg`. Recursive walk over `structArg.Args`; per arg: allocate `(writer, reader)` + bind writer (ground const / nil-list / list-cell-recurse / sub-struct-recurse / aliased var) + append `new RtVarRef(readerId)` to `argTerms`. ListTerm nil branch binds `"nil"` const; non-nil delegates to `_BuildListTerm`. Returns `new RtStructTerm(structArg.Functor, argTerms);`.

32. **`_buildStructTermForConj(...)`** → `private RtTerm _BuildStructTermForConj(GlpRuntime runtime, StructTerm structArg, ...)`. Mirror with Conj delegations; ternary-outside-RtVarRef-constructor form preserved verbatim. Observably equivalent to `_BuildStructTerm` at the VarTerm fresh-allocate path; preserved separate per Dart-source fidelity.

33. **`_buildListTerm(...)`** → `private RtTerm _BuildListTerm(GlpRuntime runtime, ListTerm list, IDictionary<string, int> queryVarWriters, IDictionary<string, int> varNameToId)`. NIL: `return new RtConstTerm("nil");`. Else build head (covers ConstTerm/VarTerm/ListTerm/StructTerm; throws on other) and tail (covers ListTerm-recurse/VarTerm-alias-or-allocate/fallthrough `new RtConstTerm(null)`). Returns `new RtStructTerm(".", new List<RtTerm> { headTerm, tailTerm });`. List-cell functor `"."` preserved verbatim.

34. **`_buildListTermForConj(...)`** → `private RtTerm _BuildListTermForConj(GlpRuntime runtime, ListTerm list, ...)`. Mirror with Conj delegations; ternary-outside form preserved verbatim. Preserved separate.

## 3. Decomposed Task Units

- T1. Emit namespace declaration `namespace <root>.Engine;` with file-header XML-doc (preserves the leading `///` block — "GLP Engine - Embeddable GLP Execution Core / Extracted from glp_repl.dart ... This is the ONE way to run GLP programs."). Emit `using` block including the four per-symbol aliases `RtTerm` / `RtVarRef` / `RtConstTerm` / `RtStructTerm`. — done.
- T2. Emit `public sealed class ExecutionResult` with three get-only properties (Status / Bindings / Error), positional ctor with two optional defaults, three computed bool projections. — done.
- T3. Emit `public sealed class ModuleInfo` with six get-only properties, positional ctor with two optional defaults, XML-doc remarks on `IsTopLevel`. — done.
- T4. Emit `public sealed class GlpEngine` declaration with all fields/properties (two readonly inline-initialised, two readonly Dictionary inline-initialised, two readonly assigned-in-ctor, one `int _goalId = 1`, four public auto-properties with initialisers, nullable MadContext property with full-qualification, three computed get-only getters). — done.
- T5. Emit two `private const string` fields `_ServeSource` and `_MadPredicatesSource` byte-identical to Dart raw triple-quote strings using C# verbatim `@"..."` (with `""` for embedded `"`) or C# 11+ raw `"""..."""`. — done.
- T6. Emit `public GlpEngine(string rootSelfGlpPath)` ctor preserving the six-step ordering verbatim (assign, conditional-file-read-and-set-prelude-state, register-standard-predicates, `_LoadRootSelf`, compile-`_ServeSource`). — done.
- T7. Emit `public void Clear()` with TryGetValue + clear + restore three-step. — done.
- T8. Emit `private void _LoadRootSelf()` with bare `catch { }` and FRESH `new GlpCompiler()` local. — done.
- T9. Emit `public bool LoadFile(string path)` throwing `FileNotFoundException("File not found", path)` on missing; delegate to `LoadSource(source, filename: path)`. — done.
- T10. Emit `public bool LoadSource(string source, string? filename = null)` with six-stage pipeline: parse → ancestor-scope discovery (guarded by three magic-name skips + `_FindProjectRoot` + `DiscoverSelfChain` + `_BuildAncestorScope`) → PE+type-check (with strictTypes throw `InvalidOperationException` vs `Console.WriteLine` warning) → compile + register both registries → optional auto-activate (merge with root-self if present + `ActivateModule`). — done.
- T11. Emit `public bool LoadProject(string projectDir, string? topModuleName = null)` with five-stage pipeline: `DiscoverProject` → empty-check throw → `TypeCheckProject` → `_DetectTopModule` or caller-supplied → `LinkProject` → `_compiler.CompileProgram` → register `__project__`. — done.
- T12. Emit `private string _DetectTopModule(List<DiscoveredModule> modules)` with LINQ Where+Any filter + exact-one-match preference + fallback mutate-sort-descending via `List<T>.Sort((a, b) => b.X.CompareTo(a.X))`. — done.
- T13. Emit `public async Task<ExecutionResult> RunGoalAsync(string goalText)` with try/catch wrapping; strip trailing `.`; dispatch on `_IsConjunction`. — done.
- T14. Emit `public void ActivateDynamicModule(string moduleName)` with idempotent ContainsKey short-circuit + TryGetValue missing-throw + HasExports throw + merge-with-root-self + `ActivateModule` call. — done.
- T15. Emit `public void EnableMadGlp(string agentId)` with `LoadSource` + full-qualified `new <root>.Multiagent.MadContext(agentId, _runtime)` + dual injection (property + `_runtime.MadContext`). Acronym `MadGlp` per .NET capitalisation. — done.
- T16. Emit `public BytecodeProgram CombinedProgram` getter with concat-ops + build-allowedLabels (root-self + project + per-module top-level-or-exported) + collect-then-remove prune. Spec §19.3/§19.6 citation in XML-doc `<remarks>`. — done.
- T17. Emit `private async Task<ExecutionResult> _RunSingleGoalAsync(string trimmed)` with parse + abort-if-empty checks + entry-PC TryGetValue + setup args via `_SetupArgument` + wire env/program/module-context + create scheduler/runner + drain via `DrainAsyncWithStatusAsync` + collect bindings. — done.
- T18. Emit `private async Task<ExecutionResult> _RunConjunctionAsync(string trimmed)` with synthetic-wrapper parse + per-goal loop with short-circuit-on-failure + suspended-tracking + post-loop bindings collection + final lattice-status. Scheduler+runner created ONCE before loop. — done.
- T19. Emit `private bool _IsConjunction(string query)` with depth-tracking scanner; rename `char` → `ch`. — done.
- T20. Emit `private ModuleInfo _ExtractModuleInfo(string source, BytecodeProgram program, string filename)` with three regex scrapes; `Regex.Match.Success` replaces null-check; verbatim regex patterns preserve backslashes; ordered `List<string>` imports + `HashSet<string>` exportedLabels. — done.
- T21. Emit `private string _ModuleNameFromFilename(string filename)` with `Split('/').Last()` + `.glp` strip (Dart-fidelity forward-slash hard-coding). — done.
- T22. Emit `private string? _FindProjectRoot(string filePath)` with DirectoryInfo walk-up + `Path.Combine` self.glp probe + null-safe parent termination + topmost-self.glp wins. — done.
- T23. Emit `private TypeEnvironment _BuildAncestorScope(IReadOnlyList<string> chain)` with `BuildPreludeEnvironment` start + root-self merge + chain iteration with self-glp dedup via `FileInfo.FullName` equality. — done.
- T24. Emit `private TypeEnvironment _MergeModuleIntoEnv(TypeEnvironment env, string source)` with parse + parameterised-template collection BEFORE expansion + `ExpandParameterizedTypes` + collect types/procs/paramProcs + `env.Merge`. — done.
- T25. Emit `private ModuleInfo? _FindModuleForProcedure(string procedureLabel)` linear scan; first-match-wins. — done.
- T26. Emit `private ReplModuleContext? _BuildModuleContext(ModuleInfo module, BytecodeProgram combinedProg)` with early-return-null + 1-based-import-index dictionary build + gap-tolerant TryGetValue. — done.
- T27. Emit `private void _SetupArgument(...)` with four-branch pattern-match dispatch; alias-or-allocate VarTerm; record-destructuring `var (writerId, readerId)`. — done.
- T28. Emit `private void _SetupConjunctionArg(...)` mirror with Conj delegations; preserved separate. — done.
- T29. Emit `private RtTerm _BuildStructTerm(GlpRuntime runtime, StructTerm structArg, ...)` recursive walk; reserved `struct` → `structArg`; ListTerm nil-vs-non-nil sub-branch. — done.
- T30. Emit `private RtTerm _BuildStructTermForConj(...)` mirror; preserved separate. — done.
- T31. Emit `private RtTerm _BuildListTerm(GlpRuntime runtime, ListTerm list, ...)` with NIL return + head four-case + tail three-case (fallthrough `new RtConstTerm(null)`); list-cell functor `"."`. — done.
- T32. Emit `private RtTerm _BuildListTermForConj(...)` mirror; preserved separate. — done.

## 4. Research Findings

none required — every construct's decision is verbatim-derivable from the ratified convspec at `.codeconv/conversion-specs/lib/engine/glp_engine.dart.md` (30 yaml-block construct entries + 22 rationale research-finding sections, all sibling-convspec carry-forwards or directly cited Microsoft Learn / Dart-doc URLs). All idiomatic decisions are reused from prior convspecs (heap_fcp.dart.md, mad_context.dart.md, scheduler.dart.md, runtime.dart.md, external_io.dart.md, glp_activation.dart.md, claude_adapter.dart.md, mad_helpers.dart.md, project_linker.dart.md, module_hierarchy.dart.md, partial_evaluator.dart.md, type_environment_builder.dart.md, type_checker.dart.md, param_expansion.dart.md, system_predicates_impl.dart.md, compiler.dart.md, lexer.dart.md, parser.dart.md, ast.dart.md, bytecode/runner.dart.md, terms.dart.md). Two new idioms introduced by this file — (a) per-symbol type aliases for the `as rt;` aliased import collision; (b) collect-then-remove pattern for `Map.removeWhere` — are both grounded in authoritative Microsoft Learn URLs cited in the convspec's `## Rationale and research provenance` section.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/lib/engine/glp_engine.dart.md` (RATIFIED).

- All 30 yaml `constructs:` entries map 1-1 to the 34 task units in §3 (six near-clone method pairs counted as six tasks T27/T28, T29/T30, T31/T32 reflecting the convspec's explicit "preserved separate" decision).
- All 22 `research_finding_id` references are reused-verbatim or directly cited Microsoft Learn / Dart-doc URLs in the convspec's rationale section; no unresolved research gap.
- Threading-model decision INHERITED from heap_fcp.dart.md escalations[0]; not re-escalated per FR-013 ("don't double-escalate a previously-escalated decision").
- The four sibling-set escalations (compiler/error.dart exception-naming, glp_printer.dart `_isAtom`, heap_fcp.dart threading, analyzer.dart duplicate UnifyResult) are inherited indirectly; convspec analyses each as either non-applicable here or transitively resolved.
- Name-collision resolution for `MadContext` (property-vs-type) handled by full-qualification at construction site — verbatim derivable from convspec construct 7 (glp_engine_central_class) and construct 17 (enable_madglp).
- One-based ImportTable indexing in `_BuildModuleContext` (T26) preserved verbatim per LOAD-BEARING annotation in convspec construct 28 (build_module_context_imports_map_one_based_index).
- Byte-fidelity preservation of embedded GLP source constants (T5) via C# verbatim/raw-string-literal — verbatim derivable from convspec constructs 3, 4 (raw_string_*_embedded_glp_program).
- Async surface (T13, T17, T18) limited to three methods only; `-Async` suffix per Microsoft Framework Design Guidelines — verbatim derivable from convspec construct 12 (run_goal_async_entry_point) and `## Notes` bullet "Async surface limited to RunGoalAsync...".
- Near-clone preservation of six mirrored methods (T27-T32) NOT consolidated — verbatim derivable from convspec constructs 30, 32, 34 and `## Notes` bullet "The two near-clone method pairs are PRESERVED separately."

## 6. Escalations

None.
