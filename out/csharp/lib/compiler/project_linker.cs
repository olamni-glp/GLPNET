/// <summary>
/// Project linker: static linking of multi-module GLP projects.
///
/// Given a project root directory, discovers all modules, type-checks each
/// independently, then produces a single flat Program AST where all
/// inter-module calls are resolved to renamed local procedures.
///
/// Specification: docs/modules/glp-project-compilation-spec.md
/// Plan: docs/modules/project-compilation-implementation-plan.md
/// </summary>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlpRuntime.Analysis.TypeChecker;
using GlpRuntime.Runtime;
using Ast = GlpRuntime.Compiler;

namespace GlpRuntime.Compiler;

// ============================================================================
// DiscoveredModule — immutable data carrier for a discovered project module.
// Class (NOT record) because LinkProject uses ReferenceEquals(s, mod) for
// identity-based self-skip in the ancestor self.glp walk.
// ============================================================================

/// <summary>A discovered module in the project tree.</summary>
public sealed class DiscoveredModule
{
    public string FilePath { get; }
    public string ModuleName { get; }
    // Renamed from Dart field 'ast' to avoid collision with the Ast namespace alias.
    public Ast.Module ModuleAst { get; }
    public TypeEnvironment AncestorScope { get; }
    public bool IsSelfGlp { get; }

    public DiscoveredModule(
        string filePath,
        string moduleName,
        Ast.Module ast,
        TypeEnvironment ancestorScope,
        bool isSelfGlp = false)
    {
        FilePath      = filePath;
        ModuleName    = moduleName;
        ModuleAst     = ast;
        AncestorScope = ancestorScope;
        IsSelfGlp     = isSelfGlp;
    }
}

// ============================================================================
// LinkResult — two-field immutable carrier for the linked program.
// ============================================================================

/// <summary>Result of linking a project.</summary>
public sealed class LinkResult
{
    public Program Program { get; }
    public IReadOnlyList<ProcDecl> ProcDeclarations { get; }

    public LinkResult(Program program, IReadOnlyList<ProcDecl> procDeclarations)
    {
        Program          = program;
        ProcDeclarations = procDeclarations;
    }
}

// ============================================================================
// ProjectLinker — hosts all top-level functions as public/private static methods.
// ============================================================================

/// <summary>
/// Static host for the three-stage GLP project linker.
/// Dart top-level free functions migrate to C# static class methods per project convention.
/// </summary>
public static class ProjectLinker
{
    // ========================================================================
    // Public entry points
    // ========================================================================

    /// <summary>
    /// Walk the project directory tree and discover all modules.
    ///
    /// For each .glp file (excluding boot_direct.glp, mad_boot.glp, and files
    /// under mad_boot/ subdirectories):
    /// - Parse into Module AST
    /// - Extract module name (from -module(M). directive, or filename; for self.glp
    ///   without -module(), derives name from parent directory)
    /// - Build ancestor type scope chain
    ///
    /// self.glp files contribute both types AND procedures to the ancestor scope.
    /// Their procedures are compiled to bytecode and renamed like any other module.
    /// </summary>
    public static IReadOnlyList<DiscoveredModule> DiscoverProject(
        string rootDir,
        string? rootSelfGlpPath = null)
    {
        if (!Directory.Exists(rootDir))
            throw new ArgumentException($"Project root directory not found: {rootDir}");

        var modules = new List<DiscoveredModule>();

        // Recursively find all .glp files — collapses Dart's three-stage pipeline
        // (listSync + whereType<File> + where endsWith) to a single BCL call.
        var glpFiles = Directory.EnumerateFiles(rootDir, "*.glp", SearchOption.AllDirectories)
                                .ToList();

        foreach (var filePath in glpFiles)
        {
            var filename = Path.GetFileName(filePath);

            // Skip boot_direct.glp (copy of boot.glp with direct calls, not a module)
            if (filename == "boot_direct.glp") continue;

            // Skip mad_boot.glp and files in mad_boot/ directory
            // (madGLP boot procedures, loaded on top of linked project)
            if (filename == "mad_boot.glp") continue;

            // Dual-separator check: preserve BOTH platform-separator suffix AND literal '/'
            // suffix for cross-platform defensive behaviour (Dart source uses both).
            var parent = Path.GetDirectoryName(filePath);
            if (parent != null &&
                (parent.EndsWith(Path.DirectorySeparatorChar + "mad_boot", StringComparison.Ordinal) ||
                 parent.EndsWith("/mad_boot", StringComparison.Ordinal)))
                continue;

            // Parse the module
            var source  = File.ReadAllText(filePath);
            var lexer   = new Lexer(source);
            var tokens  = lexer.Tokenize();
            var parser  = new Parser(tokens);
            var module  = parser.ParseModule();

            // Extract module name: for self.glp without -module(), derive from parent dir
            var moduleName = module.Name ??
                (filename == "self.glp"
                    ? ModuleNameFromDirPath(parent!)
                    : ModuleNameFromFilename(filename));

            // Build ancestor scope chain
            var chain = ModuleHierarchy.DiscoverSelfChain(
                targetFile: Path.GetFullPath(filePath),
                rootDir:    Path.GetFullPath(rootDir));
            var ancestorScope = BuildAncestorScope(chain, rootSelfGlpPath: rootSelfGlpPath);

            modules.Add(new DiscoveredModule(
                filePath:      filePath,
                moduleName:    moduleName,
                ast:           module,
                ancestorScope: ancestorScope,
                isSelfGlp:     filename == "self.glp"));
        }

        return modules;
    }

    /// <summary>
    /// Type-check each module independently against its ancestor scope.
    ///
    /// Throws on type errors with module name and error details.
    /// </summary>
    public static void TypeCheckProject(IReadOnlyList<DiscoveredModule> modules)
    {
        foreach (var mod in modules)
        {
            // Skip modules with no procedure declarations (untyped orchestration)
            if (mod.ModuleAst.ProcDeclarations.Count == 0) continue;

            // Only type-check modules that have non-imported declarations
            var hasOwnDecls = mod.ModuleAst.ProcDeclarations.Any(d => !d.Imported);
            if (!hasOwnDecls) continue;

            // Run partial evaluation before type checking (same pipeline as compiler)
            var program     = new Program(mod.ModuleAst.Procedures, mod.ModuleAst.Line, mod.ModuleAst.Column);
            var pe          = new PartialEvaluator();
            var transformed = pe.TransformDefinedGuards(program);

            var result = TypeCheckerDriver.CheckModule(
                mod.ModuleAst,
                transformedProcedures: transformed.Procedures,
                ancestorScope:         mod.AncestorScope);

            if (!result.IsWellTyped)
            {
                var errors = string.Join("\n", result.Errors.Select(e => $"  {e.Message} at line {e.Line}"));
                throw new Exception($"Type checking failed for {mod.ModuleName} ({mod.FilePath}):\n{errors}");
            }
        }
    }

    /// <summary>
    /// Link all modules into a single flat Program.
    ///
    /// Renames procedures (p/n → M:p/n), resolves all calls, and generates
    /// entry point aliases for the top module's procedures.
    ///
    /// Returns a LinkResult with the linked program and renamed proc declarations
    /// (needed for SRSW type-based relaxation during compilation).
    /// </summary>
    public static LinkResult LinkProject(IReadOnlyList<DiscoveredModule> modules, string topModuleName)
    {
        // Phase 1: Build procedure registry: module name → set of procedure signatures.
        var registry = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var mod in modules)
        {
            var sigs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var proc in mod.ModuleAst.Procedures)
                sigs.Add($"{proc.Name}/{proc.Arity}");
            registry[mod.ModuleName] = sigs;
        }

        // Phase 2: Build ancestor self.glp procedure map for each module.
        // Maps module name → { sig → ancestorModuleName } (inner-most ancestor wins).
        var selfGlpModules  = modules.Where(m => m.IsSelfGlp).ToList();
        var ancestorSelfProcs = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var mod in modules)
        {
            var modDir = Path.GetDirectoryName(Path.GetFullPath(mod.FilePath)) ?? string.Empty;
            var procs  = new Dictionary<string, string>(StringComparer.Ordinal); // sig → ancestorModuleName

            // Walk self.glp modules from inner-most to outer-most.
            // Inner-most wins (first entry via TryAdd / ContainsKey-then-assign).
            // Sort by path length descending (longer path = more nested = inner).
            var ancestors = selfGlpModules
                .Where(s => !ReferenceEquals(s, mod) &&
                            modDir.StartsWith(
                                Path.GetDirectoryName(Path.GetFullPath(s.FilePath)) ?? string.Empty,
                                StringComparison.Ordinal))
                .OrderByDescending(s => s.FilePath.Length)
                .ToList();

            foreach (var selfMod in ancestors)
            {
                foreach (var proc in selfMod.ModuleAst.Procedures)
                {
                    var sig = $"{proc.Name}/{proc.Arity}";
                    // putIfAbsent semantics: insert only if absent (inner-most-wins).
                    // NOT procs[sig] = ... which would overwrite and break the invariant.
                    if (!procs.ContainsKey(sig)) procs[sig] = selfMod.ModuleName;
                }
            }

            ancestorSelfProcs[mod.ModuleName] = procs;
        }

        // Phase 3: Per-module clause rewrite.
        var allProcedures = new List<Procedure>();

        foreach (var mod in modules)
        {
            var localSigs       = registry[mod.ModuleName];
            var modAncestorProcs = ancestorSelfProcs.TryGetValue(mod.ModuleName, out var aps)
                ? aps
                : new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var proc in mod.ModuleAst.Procedures)
            {
                var renamedName    = $"{mod.ModuleName}:{proc.Name}";
                var renamedClauses = new List<Clause>();

                foreach (var clause in proc.Clauses)
                {
                    // Rename clause head
                    var renamedHead = new Atom(
                        $"{mod.ModuleName}:{clause.Head.Functor}",
                        clause.Head.Args,    // args passed by reference (no copy)
                        clause.Head.Line,
                        clause.Head.Column);

                    // Resolve calls in body (null-conditional preserves nullable body)
                    IReadOnlyList<Goal>? resolvedBody =
                        clause.Body?.Select(g => ResolveGoal(g, mod.ModuleName, localSigs, modAncestorProcs))
                                    .ToList();

                    renamedClauses.Add(new Clause(
                        renamedHead,
                        guards: clause.Guards,   // guards passed by reference (no copy)
                        body:   resolvedBody,
                        line:   clause.Line,
                        column: clause.Column));
                }

                allProcedures.Add(new Procedure(renamedName, proc.Arity, renamedClauses, proc.Line, proc.Column));
            }
        }

        // Phase 4: Build project-wide procedure declaration index for mode-aware aliases.
        // Maps 'name/arity' → ProcDecl, collecting non-imported decls (first-wins).
        var declIndex = new Dictionary<string, ProcDecl>(StringComparer.Ordinal);
        foreach (var mod in modules)
        {
            foreach (var d in mod.ModuleAst.ProcDeclarations)
            {
                if (d.Imported) continue;
                var sig = $"{d.Name}/{d.Arity}";
                // putIfAbsent: first declaration wins.
                if (!declIndex.ContainsKey(sig)) declIndex[sig] = d;
            }
        }

        // Phase 5: Generate entry point aliases.
        // Top module: ALL procedures get aliases (for REPL invocation).
        // Other modules: only EXPORTED procedures get aliases.
        var aliasedSigs = new Dictionary<string, string>(StringComparer.Ordinal); // sig → owning module

        foreach (var mod in modules)
        {
            var isTop = mod.ModuleName == topModuleName;
            foreach (var proc in mod.ModuleAst.Procedures)
            {
                var sig = $"{proc.Name}/{proc.Arity}";

                // Top module: alias all. Others: only exported.
                if (!isTop)
                {
                    var isExported = mod.ModuleAst.ProcDeclarations
                        .Any(d => d.Exported && d.Name == proc.Name && d.Arity == proc.Arity);
                    if (!isExported) continue;
                }

                // Skip if an alias already exists (top module wins).
                if (aliasedSigs.ContainsKey(sig)) continue;

                aliasedSigs[sig] = mod.ModuleName;

                // Look up ProcDecl for mode-aware alias generation.
                // First check the owning module, then the project-wide index.
                var decl = FindProcDecl(mod, proc.Name, proc.Arity)
                    ?? (declIndex.TryGetValue(sig, out var d2) ? d2 : null);

                var aliasClause = MakeAliasClause(
                    proc.Name,
                    proc.Arity,
                    $"{mod.ModuleName}:{proc.Name}",
                    declaration: decl);

                allProcedures.Add(new Procedure(
                    proc.Name,
                    proc.Arity,
                    new List<Clause> { aliasClause },
                    0, 0));
            }
        }

        // Collect and rename proc declarations for SRSW relaxation.
        var allDecls = new List<ProcDecl>();
        foreach (var mod in modules)
        {
            foreach (var decl in mod.ModuleAst.ProcDeclarations)
            {
                if (decl.Imported) continue; // Skip imported — they're in other modules
                allDecls.Add(new ProcDecl(
                    $"{mod.ModuleName}:{decl.Name}",
                    decl.ArgTypes,
                    decl.Line,
                    decl.Column,
                    isBuiltin: decl.IsBuiltin));
            }
        }

        return new LinkResult(new Program(allProcedures, 0, 0), allDecls);
    }

    // ========================================================================
    // Private helpers
    // ========================================================================

    /// <summary>
    /// Resolve a single goal in a clause body.
    ///
    /// Resolution order: local procedure → ancestor self.glp chain → prelude/stdlib.
    /// Identity-preserving: returns the SAME goal instance when no rewrite applies.
    /// </summary>
    private static Goal ResolveGoal(
        Goal goal,
        string moduleName,
        IReadOnlyCollection<string> localSigs,
        IReadOnlyDictionary<string, string> ancestorSelfProcs)
    {
        // RemoteGoal: M' # p(...) → M':p(...)
        if (goal is RemoteGoal rg)
        {
            var targetModule = rg.StaticModuleName;
            if (targetModule != null)
            {
                // Static dispatch: replace with renamed goal.
                // Dart field 'goal.goal.functor' → C# 'rg.Goal.Functor' (the inner Goal property).
                return new Goal($"{targetModule}:{rg.Goal.Functor}", rg.Goal.Args, rg.Line, rg.Column);
            }
            // Dynamic dispatch — can't resolve statically, leave as-is.
            return goal;
        }

        // SpawnGoal: resolve inner goal, keep wrapper.
        if (goal is SpawnGoal sg)
        {
            var resolvedInner = ResolveGoal(sg.InnerGoal, moduleName, localSigs, ancestorSelfProcs);
            // Allocate a new SpawnGoal ONLY if the inner goal was actually rewritten.
            // ReferenceEquals = Dart's identical() — identity-preserving on no-op.
            if (!ReferenceEquals(resolvedInner, sg.InnerGoal))
                return new SpawnGoal(resolvedInner, sg.AgentId, sg.Line, sg.Column);
            return goal;
        }

        // Regular goal: check if it matches a local procedure.
        var sig = $"{goal.Functor}/{goal.Arity}";
        if (localSigs.Contains(sig))
            return new Goal($"{moduleName}:{goal.Functor}", goal.Args, goal.Line, goal.Column);

        // Check ancestor self.glp procedures.
        // TryGetValue = Dart Map[k] (returns null on absent); C# indexer throws on absent.
        if (ancestorSelfProcs.TryGetValue(sig, out var ancestorModule))
            return new Goal($"{ancestorModule}:{goal.Functor}", goal.Args, goal.Line, goal.Column);

        // Prelude/stdlib/body kernel — leave unchanged.
        return goal;
    }

    /// <summary>Find the ProcDecl for a procedure in a module (non-imported only).</summary>
    private static ProcDecl? FindProcDecl(DiscoveredModule mod, string name, int arity)
    {
        foreach (var d in mod.ModuleAst.ProcDeclarations)
        {
            if (!d.Imported && d.Name == name && d.Arity == arity) return d;
        }
        return null;
    }

    /// <summary>
    /// Create an alias clause with mode-aware argument forwarding.
    ///
    /// Given a procedure declaration, generates:
    ///   p(V0, V1, V2) :- M:p(V0?, V1, V2).
    /// where input args (declared with ?) get reader annotation in the body,
    /// and output args (no ?) get writer annotation (pass-through).
    ///
    /// Without a declaration, falls back to all-reader body args:
    ///   p(V0, V1, V2) :- M:p(V0?, V1?, V2?).
    /// </summary>
    private static Clause MakeAliasClause(
        string name,
        int arity,
        string targetName,
        ProcDecl? declaration = null)
    {
        if (arity == 0)
        {
            // Zero-arity: p :- M:p.
            var head0 = new Atom(name, Array.Empty<Term>(), 0, 0);
            var body0 = new List<Goal> { new Goal(targetName, Array.Empty<Term>(), 0, 0) };
            return new Clause(head0, guards: null, body: body0, line: 0, column: 0);
        }

        // Generate variable names (V prefix — underscore prefix causes issues in codegen).
        // Term[] pre-allocation avoids Dart's List.generate; 'as Term' cast elided (element type declared upfront).
        var headArgs = new Term[arity];
        for (int i = 0; i < arity; i++)
            headArgs[i] = new VarTerm($"V{i}", false, 0, 0);

        // Body args: use mode from declaration if available.
        // Input args (T?) → isReader: true  (forward the value as reader)
        // Output args (T)  → isReader: false (forward the writer so callee can write)
        var bodyArgs = new Term[arity];
        for (int i = 0; i < arity; i++)
        {
            var isInput = declaration != null && i < declaration.ArgTypes.Count
                ? declaration.IsInputArg(i)
                : true; // Fallback: assume input (reader) when no declaration
            bodyArgs[i] = new VarTerm($"V{i}", isInput, 0, 0);
        }

        var head = new Atom(name, headArgs, 0, 0);
        var body = new List<Goal> { new Goal(targetName, bodyArgs, 0, 0) };
        return new Clause(head, guards: null, body: body, line: 0, column: 0);
    }

    /// <summary>Extract module name from filename (without .glp extension).</summary>
    private static string ModuleNameFromFilename(string filename)
    {
        if (filename.EndsWith(".glp", StringComparison.Ordinal))
            return filename[..^4];
        return filename;
    }

    /// <summary>Extract module name from directory path (last component).</summary>
    private static string ModuleNameFromDirPath(string dirPath) => Path.GetFileName(dirPath);

    /// <summary>
    /// Build ancestor type scope from self.glp chain.
    ///
    /// If rootSelfGlpPath is provided, it is received but NOT consumed in the body
    /// (the chain already includes root self.glp transitively via the prelude environment).
    /// Parameter preserved for API stability.
    ///
    /// Structural subset of ModuleHierarchy.AssembleTypeScope: same per-self.glp loop body,
    /// omits the final module-itself merge (that layer is applied by TypeCheckerDriver.CheckModule
    /// at the TypeCheckProject call site via ancestorScope).
    /// </summary>
    private static TypeEnvironment BuildAncestorScope(
        IReadOnlyList<string> chain,
        string? rootSelfGlpPath = null) // rootSelfGlpPath intentionally unused: prelude covers root self.glp
    {
        // Start from prelude (which includes root self.glp), matching
        // the individual-load path in AssembleTypeScope.
        var env = TypeEnvironmentBuilder.BuildPreludeEnvironment();

        // Chain contains only project-local self.glp files.
        // Root self.glp is already included in the prelude environment.
        foreach (var selfGlpPath in chain)
        {
            var source     = File.ReadAllText(selfGlpPath);
            var lexer      = new Lexer(source);
            var tokens     = lexer.Tokenize();
            var parser     = new Parser(tokens);
            var selfModule = parser.ParseModule();

            // Extract templates before expansion removes them.
            var selfTemplates = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
            foreach (var td in selfModule.TypeDefs)
                if (td.IsParameterized) selfTemplates[td.Name] = td;

            // Expand parameterized types before building scope.
            var expandedSelfModule = ParamExpansion.ExpandParameterizedTypes(
                selfModule,
                knownTypeNames:    new HashSet<string>(env.Types.Keys, StringComparer.Ordinal),
                externalTemplates: env.TypeTemplates);

            // Build environment from this self.glp (shadowing allowed).
            var selfEnv = ModuleHierarchy.BuildScopeFromModule(expandedSelfModule);

            // Merge: later entries shadow earlier ones. Include templates for descendants.
            env = env.Merge(new TypeEnvironment(
                selfEnv.Types,
                selfEnv.Procedures,
                paramProcDecls: selfEnv.ParamProcDecls,
                typeTemplates:  selfTemplates));
        }

        return env;
    }
}
