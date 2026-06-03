/// <summary>
/// Module hierarchy: self.glp chain discovery and type scope assembly.
///
/// Implements GLP module scoping per docs/modules/glp-module-system-spec.md:
/// - Directory-based hierarchy (Section 2)
/// - Implicit ancestor scoping (Section 3.1)
/// - Shadowing (Section 3.2)
/// - Sibling isolation (Section 3.3)
///
/// Specification: docs/modules/glp-module-system-spec.md Sections 2-3
/// </summary>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlpRuntime.Analysis.TypeChecker;
using GlpRuntime.Compiler;

namespace GlpRuntime.Runtime;

/// <summary>
/// Hosts the three top-level free functions that implement GLP's directory-based
/// module hierarchy: DiscoverSelfChain, AssembleTypeScope, and BuildScopeFromModule.
/// </summary>
public static class ModuleHierarchy
{
    /// <summary>
    /// Discover the self.glp chain from root to target file's directory.
    ///
    /// Walks up from the target file's directory to the root directory,
    /// collecting self.glp files at each level. Returns them in root-first
    /// order (outermost ancestor first, innermost last).
    ///
    /// If the target file IS self.glp, the chain includes only ancestors
    /// above it (not itself — the module's own definitions come from parsing it).
    ///
    /// <param name="targetFile">absolute path to the .glp file being compiled</param>
    /// <param name="rootDir">absolute path to the project root directory</param>
    /// <returns>list of absolute paths to self.glp files, root-first order</returns>
    /// </summary>
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
            startDir = Path.GetDirectoryName(Path.GetDirectoryName(target))!;
        }
        else
        {
            // Target is a regular module — start from its directory
            startDir = Path.GetDirectoryName(target)!;
        }

        // Walk from startDir up to root, collecting self.glp files
        var chain = new List<string>();
        var currentDir = startDir;

        while (true)
        {
            // Normalize for comparison, stripping trailing slashes for consistency
            var currentNorm = Path.GetFullPath(currentDir);
            var rootNorm = Path.GetFullPath(root);
            // LOAD-BEARING: strip literal '/' only (not Path.DirectorySeparatorChar)
            // to preserve exact Dart source behaviour on Windows (Dart absolute paths
            // use '\\' on Windows, so this branch is a no-op there — identical semantics)
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
            currentDir = Path.GetDirectoryName(currentDir)!;
        }

        // Reverse: we collected from target-to-root, but want root-first
        return Enumerable.Reverse(chain).ToList();
    }

    /// <summary>
    /// Assemble the type scope for a module by layering ancestor definitions.
    ///
    /// Builds a TypeEnvironment by:
    /// 1. Starting with the prelude (root of all type chains)
    /// 2. Merging each self.glp in the chain (root-first, so children shadow parents)
    /// 3. Merging the target module's own types and declarations (shadows all ancestors)
    ///
    /// <param name="chain">list of self.glp file paths, root-first order (from DiscoverSelfChain)</param>
    /// <param name="module">the parsed Module AST of the target file</param>
    /// <returns>the assembled TypeEnvironment with all visible types and procedures</returns>
    /// </summary>
    public static TypeEnvironment AssembleTypeScope(IReadOnlyList<string> chain, Module module)
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

    /// <summary>
    /// Build a TypeEnvironment from a Module's types and procedure declarations.
    ///
    /// Unlike _buildEnvironmentFromModule in type_environment_builder.dart,
    /// this does NOT check for predefined type redefinition (because shadowing
    /// ancestor types is allowed) and does NOT resolve aliases (that happens
    /// after all scopes are assembled).
    /// </summary>
    public static TypeEnvironment BuildScopeFromModule(Module module)
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
}
