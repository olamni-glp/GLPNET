// lib/analysis/type_checker/param_expansion.cs
//
// Expand parameterized types to monomorphic equivalents.
// Runs after parsing and before type automaton construction.
//
// Spec: docs/type system/typed-program.md, section "Parameterized Types"
// Paper: Section 8, Definition 8.1

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GlpRuntime.Compiler;

namespace GlpRuntime.Analysis.TypeChecker
{
    /// <summary>
    /// Mono-morphises parameterised type templates in a GLP Module AST.
    /// Pure static helper — no mutable shared state.
    /// </summary>
    public static class ParamExpansion
    {
        /// <summary>
        /// Expand all parameterized types in a module to monomorphic equivalents.
        /// Returns a new Module with only monomorphic type definitions and
        /// procedure declarations. The original Module is not modified.
        ///
        /// If the module has no parameterized types, returns it unchanged.
        /// </summary>
        public static Module ExpandParameterizedTypes(
            Module module,
            IReadOnlySet<string>? knownTypeNames = null,
            IReadOnlyDictionary<string, TypeDef>? externalTemplates = null)
        {
            knownTypeNames ??= ImmutableHashSet<string>.Empty;
            externalTemplates ??= ImmutableDictionary<string, TypeDef>.Empty;

            // Step 1: Separate templates from monomorphic types
            var templates = new Dictionary<string, TypeDef>(StringComparer.Ordinal);
            var monoTypeDefs = new List<TypeDef>();

            foreach (var td in module.TypeDefs)
            {
                if (td.IsParameterized)
                    templates[td.Name] = td;
                else
                    monoTypeDefs.Add(td);
            }

            // Merge external templates (from prelude/ancestor scopes).
            // Local templates take precedence over external ones.
            foreach (var entry in externalTemplates)
                templates.TryAdd(entry.Key, entry.Value);

            // Note: don't return early if templates is empty — proc decls may reference
            // prelude templates (e.g., Stream(X)) and still need type param detection.

            // Known monomorphic type names: used to collapse all-wildcard expansions
            // (e.g., Stream(_) → Stream) when a monomorphic version exists.
            var monoNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var td in monoTypeDefs)
                monoNames.Add(td.Name);
            monoNames.UnionWith(knownTypeNames);

            // Step 2: Collect all instantiations from type defs and proc decls
            var instantiations = new Dictionary<string, IReadOnlyList<TypeExpr>>(StringComparer.Ordinal); // expanded name -> type args

            // Scan monomorphic type def bodies
            foreach (var td in monoTypeDefs)
                foreach (var alt in td.Alternatives)
                    CollectInstantiations(alt, templates, instantiations);

            // Scan procedure declarations.
            // For parameterized proc decls (those with bare type params), skip
            // instantiations that contain type parameter names — those are templates,
            // not concrete instantiations.
            foreach (var pd in module.ProcDeclarations)
            {
                var procTypeParams = DetectProcTypeParams(pd, templates, monoTypeDefs, knownTypeNames);
                if (procTypeParams.Count == 0)
                {
                    // Non-parameterized proc decl: collect all instantiations normally
                    foreach (var arg in pd.ArgTypes)
                        CollectInstantiations(arg, templates, instantiations);
                }
                else
                {
                    // Parameterized proc decl: only collect instantiations with concrete args
                    foreach (var arg in pd.ArgTypes)
                        CollectInstantiationsInTemplate(arg, templates, instantiations, procTypeParams);
                }
            }

            // Scan template bodies for cross-references
            foreach (var td in templates.Values)
                foreach (var alt in td.Alternatives)
                    CollectInstantiationsInTemplate(alt, templates, instantiations, td.TypeParams);

            // Step 3: Expand each instantiation using a worklist
            var expandedDefs = new List<TypeDef>();
            var expanded = new HashSet<string>(StringComparer.Ordinal);

            while (instantiations.Count > expanded.Count)
            {
                foreach (var entry in instantiations.ToList())
                {
                    if (expanded.Contains(entry.Key)) continue;
                    var expandedName = entry.Key;
                    var typeArgs = entry.Value;
                    var templateName = TemplateNameFromExpanded(expandedName);
                    if (!templates.TryGetValue(templateName, out var template))
                    {
                        // Referenced template not found — skip (will be caught by type checker)
                        expanded.Add(expandedName);
                        continue;
                    }
                    // Check arity
                    if (template.TypeParams.Count != typeArgs.Count)
                    {
                        expanded.Add(expandedName);
                        continue;
                    }
                    // Substitute parameters
                    var substitution = template.TypeParams
                        .Zip(typeArgs, (k, v) => new KeyValuePair<string, TypeExpr>(k, v))
                        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                    var newAlts = template.Alternatives
                        .Select(alt => SubstituteTypeExpr(alt, substitution, templates, instantiations, monoNames: monoNames))
                        .ToList();
                    var processedAlts = newAlts
                        .Select(alt => ReplaceParamRefs(alt, templates, monoNames: monoNames))
                        .ToList();
                    expandedDefs.Add(new TypeDef(expandedName, processedAlts, template.Line, template.Column));
                    expanded.Add(expandedName);
                }
            }

            // Step 4: Replace references in monomorphic type defs
            var replacedTypeDefs = monoTypeDefs
                .Select(td =>
                {
                    var newAlts = td.Alternatives
                        .Select(alt => ReplaceParamRefs(alt, templates, monoNames: monoNames))
                        .ToList();
                    return new TypeDef(td.Name, newAlts, td.Line, td.Column);
                })
                .ToList();

            // Step 5: Replace references in procedure declarations.
            // Parameterized proc decls: generate wildcard-instantiated concrete version
            // (for checking own clauses) AND preserve the parameterized template
            // (for call-site inference in Case B).
            var replacedProcDecls = new List<ProcDecl>();
            var paramProcDeclTemplates = new List<ProcDecl>();

            foreach (var pd in module.ProcDeclarations)
            {
                var procTypeParams = DetectProcTypeParams(pd, templates, monoTypeDefs, knownTypeNames);
                if (procTypeParams.Count > 0)
                {
                    // Preserve parameterized template for call-site inference
                    var paramTemplate = new ProcDecl(pd.Name, pd.ArgTypes, pd.Line, pd.Column,
                        typeParams: procTypeParams,
                        exported: pd.Exported, imported: pd.Imported, modulePath: pd.ModulePath);
                    paramProcDeclTemplates.Add(paramTemplate);

                    // Generate wildcard-instantiated concrete version:
                    // Substitute each type param with PrimitiveModeAlt(false) (i.e., _)
                    var wildcardSubst = new Dictionary<string, TypeExpr>(StringComparer.Ordinal);
                    foreach (var tp in procTypeParams)
                        wildcardSubst[tp] = new PrimitiveModeAlt(false, 0, 0);

                    var wildcardArgTypes = pd.ArgTypes
                        .Select(arg =>
                        {
                            var substituted = SubstituteTypeExpr(arg, wildcardSubst, templates, instantiations, monoNames: monoNames);
                            return ReplaceParamRefs(substituted, templates, monoNames: monoNames);
                        })
                        .ToList();

                    replacedProcDecls.Add(new ProcDecl(pd.Name, wildcardArgTypes, pd.Line, pd.Column,
                        exported: pd.Exported, imported: pd.Imported, modulePath: pd.ModulePath));
                }
                else
                {
                    // Non-parameterized: expand as before
                    var newArgTypes = pd.ArgTypes
                        .Select(arg => ReplaceParamRefs(arg, templates, monoNames: monoNames))
                        .ToList();
                    replacedProcDecls.Add(new ProcDecl(pd.Name, newArgTypes, pd.Line, pd.Column,
                        exported: pd.Exported, imported: pd.Imported, modulePath: pd.ModulePath));
                }
            }

            // Expand any new instantiations generated by wildcard substitution
            while (instantiations.Count > expanded.Count)
            {
                foreach (var entry in instantiations.ToList())
                {
                    if (expanded.Contains(entry.Key)) continue;
                    var expandedName = entry.Key;
                    var typeArgs = entry.Value;
                    var templateName = TemplateNameFromExpanded(expandedName);
                    if (!templates.TryGetValue(templateName, out var template))
                    {
                        expanded.Add(expandedName);
                        continue;
                    }
                    if (template.TypeParams.Count != typeArgs.Count)
                    {
                        expanded.Add(expandedName);
                        continue;
                    }
                    var substitution = template.TypeParams
                        .Zip(typeArgs, (k, v) => new KeyValuePair<string, TypeExpr>(k, v))
                        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                    var newAlts = template.Alternatives
                        .Select(alt => SubstituteTypeExpr(alt, substitution, templates, instantiations, monoNames: monoNames))
                        .ToList();
                    var processedAlts = newAlts
                        .Select(alt => ReplaceParamRefs(alt, templates, monoNames: monoNames))
                        .ToList();
                    expandedDefs.Add(new TypeDef(expandedName, processedAlts, template.Line, template.Column));
                    expanded.Add(expandedName);
                }
            }

            return new Module(
                declaration: module.Declaration,
                typeDefs: replacedTypeDefs.Concat(expandedDefs).ToList(),
                procDeclarations: replacedProcDecls,
                paramProcDecls: paramProcDeclTemplates,
                procedures: module.Procedures,
                compileMode: module.CompileMode,
                line: module.Line,
                column: module.Column);
        }

        /// <summary>
        /// Detect type parameters in a procedure declaration.
        /// A type parameter is a name that:
        ///  1. Appears as a bare typeArg inside a TypeRef with typeArgs (e.g., X in Stream(X))
        ///  2. Is not a known defined type
        /// Bare top-level unknown types (e.g., MyUndefinedType) are NOT type params —
        /// they are only type params if the same name also appears inside a typeArg.
        /// </summary>
        private static List<string> DetectProcTypeParams(ProcDecl pd,
            IDictionary<string, TypeDef> templates,
            IList<TypeDef> monoTypeDefs,
            IReadOnlySet<string> externalKnownTypes)
        {
            var knownTypes = new HashSet<string>(StringComparer.Ordinal);
            knownTypes.UnionWith(templates.Keys);
            knownTypes.UnionWith(monoTypeDefs.Select(td => td.Name));
            knownTypes.UnionWith(TypeRef.Builtins);
            knownTypes.UnionWith(externalKnownTypes);
            knownTypes.Add("Constant"); // also a known type

            // Pass 1: collect names that appear as typeArgs of any TypeRef with typeArgs.
            // These are the "inner" type parameter candidates.
            var innerCandidates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var arg in pd.ArgTypes)
                CollectInnerTypeParamCandidates(arg, knownTypes, innerCandidates);
            return innerCandidates.ToList();
        }

        /// <summary>
        /// Collect type parameter names from inside parameterized type refs.
        /// A candidate is a bare TypeRef name that appears as a typeArg of any
        /// TypeRef with typeArgs, and is not a known type.
        /// </summary>
        private static void CollectInnerTypeParamCandidates(TypeExpr expr,
            ISet<string> knownTypes, ISet<string> candidates)
        {
            switch (expr)
            {
                case TypeRef r:
                    if (r.TypeArgs.Count > 0)
                    {
                        // Check each typeArg for bare unknown names
                        foreach (var arg in r.TypeArgs)
                        {
                            if (arg is TypeRef inner && inner.TypeArgs.Count == 0 && !knownTypes.Contains(inner.Name))
                                candidates.Add(inner.Name);
                            // Recurse into nested type args
                            CollectInnerTypeParamCandidates(arg, knownTypes, candidates);
                        }
                    }
                    break;
                // Recurse into structural types
                case StructAlt s:
                    foreach (var arg in s.Args)
                        CollectInnerTypeParamCandidates(arg, knownTypes, candidates);
                    break;
                case ListConsAlt c:
                    CollectInnerTypeParamCandidates(c.Head, knownTypes, candidates);
                    CollectInnerTypeParamCandidates(c.Tail, knownTypes, candidates);
                    break;
                case DiffListAlt d:
                    CollectInnerTypeParamCandidates(d.Content, knownTypes, candidates);
                    CollectInnerTypeParamCandidates(d.Hole, knownTypes, candidates);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Generate expanded name: Stream + [Integer] -> "Stream&lt;Integer&gt;"
        /// For nested parameterized refs, recursively uses expanded notation.
        /// </summary>
        private static string ExpandedName(string templateName, IReadOnlyList<TypeExpr> typeArgs) =>
            $"{templateName}<{string.Join(",", typeArgs.Select(TypeExprToCanonical))}>";

        /// <summary>
        /// Convert a TypeExpr to its canonical string for expanded-name purposes.
        /// Nested parameterized TypeRefs use angle-bracket notation recursively.
        /// </summary>
        private static string TypeExprToCanonical(TypeExpr expr) =>
            expr switch
            {
                TypeRef { TypeArgs: { Count: > 0 } } r =>
                    $"{r.Name}<{string.Join(",", r.TypeArgs.Select(TypeExprToCanonical))}>{(r.IsInput ? "?" : "")}",
                _ => expr.ToString() ?? string.Empty
            };

        /// <summary>
        /// Extract template name from expanded name: "Stream&lt;Integer&gt;" -> "Stream"
        /// </summary>
        private static string TemplateNameFromExpanded(string expandedName)
        {
            // ORDINAL via char-overload of IndexOf
            var idx = expandedName.IndexOf('<');
            return idx < 0 ? expandedName : expandedName.Substring(0, idx);
        }

        /// <summary>
        /// Check if a TypeRef references a template with matching arity.
        /// Returns true only if the name is a template AND the type arg count matches.
        /// </summary>
        private static bool IsTemplateRef(TypeRef expr, IDictionary<string, TypeDef> templates)
        {
            if (expr.TypeArgs.Count == 0) return false;
            if (!templates.TryGetValue(expr.Name, out var template)) return false;
            return expr.TypeArgs.Count == template.TypeParams.Count;
        }

        /// <summary>
        /// Collect parameterized type references from a TypeExpr.
        /// </summary>
        private static void CollectInstantiations(TypeExpr expr,
            IDictionary<string, TypeDef> templates,
            IDictionary<string, IReadOnlyList<TypeExpr>> instantiations)
        {
            switch (expr)
            {
                case TypeRef r:
                    if (IsTemplateRef(r, templates))
                    {
                        var name = ExpandedName(r.Name, r.TypeArgs);
                        instantiations.TryAdd(name, r.TypeArgs);
                        // Recurse into type args (for nested parameterized types)
                        foreach (var arg in r.TypeArgs)
                            CollectInstantiations(arg, templates, instantiations);
                    }
                    // Also recurse into typeArgs even if not a template (might contain nested refs)
                    foreach (var arg in r.TypeArgs)
                        CollectInstantiations(arg, templates, instantiations);
                    break;
                case StructAlt s:
                    foreach (var arg in s.Args)
                        CollectInstantiations(arg, templates, instantiations);
                    break;
                case ListConsAlt c:
                    CollectInstantiations(c.Head, templates, instantiations);
                    CollectInstantiations(c.Tail, templates, instantiations);
                    break;
                case DiffListAlt d:
                    CollectInstantiations(d.Content, templates, instantiations);
                    CollectInstantiations(d.Hole, templates, instantiations);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Collect instantiations from template bodies, skipping type parameter names.
        /// For example, in Stream(X) ::= [] ; [X | Stream(X)], Stream(X) in the body
        /// is a recursive self-reference with parameter X, not an instantiation to collect.
        /// But Pair(Integer, String) in a template body IS an instantiation.
        /// </summary>
        private static void CollectInstantiationsInTemplate(TypeExpr expr,
            IDictionary<string, TypeDef> templates,
            IDictionary<string, IReadOnlyList<TypeExpr>> instantiations,
            IReadOnlyList<string> templateParams)
        {
            switch (expr)
            {
                case TypeRef r:
                    if (IsTemplateRef(r, templates))
                    {
                        // Check if all args are just bare type parameters — if so, it's a recursive
                        // self-reference, not an instantiation to collect separately.
                        var allParamRefs = r.TypeArgs.All(arg =>
                            arg is TypeRef a && a.TypeArgs.Count == 0 && templateParams.Contains(a.Name));
                        if (!allParamRefs)
                        {
                            // Contains concrete types — collect any nested instantiations
                            // Use template-aware version to preserve param awareness through nesting
                            foreach (var arg in r.TypeArgs)
                                CollectInstantiationsInTemplate(arg, templates, instantiations, templateParams);
                        }
                    }
                    foreach (var arg in r.TypeArgs)
                        CollectInstantiationsInTemplate(arg, templates, instantiations, templateParams);
                    break;
                case StructAlt s:
                    foreach (var arg in s.Args)
                        CollectInstantiationsInTemplate(arg, templates, instantiations, templateParams);
                    break;
                case ListConsAlt c:
                    CollectInstantiationsInTemplate(c.Head, templates, instantiations, templateParams);
                    CollectInstantiationsInTemplate(c.Tail, templates, instantiations, templateParams);
                    break;
                case DiffListAlt d:
                    CollectInstantiationsInTemplate(d.Content, templates, instantiations, templateParams);
                    CollectInstantiationsInTemplate(d.Hole, templates, instantiations, templateParams);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Substitute type parameters in a TypeExpr.
        /// monoNames: set of known monomorphic type names. When all substituted args
        /// are wildcards AND the base name is in monoNames, use the base name directly.
        /// </summary>
        private static TypeExpr SubstituteTypeExpr(TypeExpr expr,
            IDictionary<string, TypeExpr> substitution,
            IDictionary<string, TypeDef> templates,
            IDictionary<string, IReadOnlyList<TypeExpr>> instantiations,
            IReadOnlySet<string>? monoNames = null)
        {
            monoNames ??= ImmutableHashSet<string>.Empty;
            switch (expr)
            {
                case TypeRef r:
                    // If this is a type parameter, substitute it
                    if (substitution.TryGetValue(r.Name, out var replacement) && r.TypeArgs.Count == 0)
                    {
                        // Apply isInput from the original reference
                        if (r.IsInput && replacement is TypeRef rep)
                            return new TypeRef(rep.Name, rep.Line, rep.Column,
                                isInput: true, typeArgs: rep.TypeArgs);
                        if (r.IsInput && replacement is PrimitiveModeAlt prim)
                            return new PrimitiveModeAlt(true, prim.Line, prim.Column);
                        return replacement;
                    }
                    // If this is a parameterized reference to a template with matching arity, record and replace
                    if (IsTemplateRef(r, templates))
                    {
                        var substArgs = r.TypeArgs
                            .Select(a => SubstituteTypeExpr(a, substitution, templates, instantiations, monoNames: monoNames))
                            .ToList();
                        // If all substituted args are wildcards (_) AND a monomorphic type with
                        // the base name exists, use the base name directly.
                        // Stream(_) ≡ Stream when a monomorphic Stream type exists.
                        var allWildcards = substArgs.All(a => a is PrimitiveModeAlt);
                        if (allWildcards && monoNames.Contains(r.Name))
                            return new TypeRef(r.Name, r.Line, r.Column, isInput: r.IsInput);
                        var expandedName = ExpandedName(r.Name, substArgs);
                        instantiations.TryAdd(expandedName, substArgs);
                        return new TypeRef(expandedName, r.Line, r.Column, isInput: r.IsInput);
                    }
                    return expr;
                case StructAlt s:
                    return new StructAlt(s.Functor,
                        s.Args.Select(a => SubstituteTypeExpr(a, substitution, templates, instantiations, monoNames: monoNames)).ToList(),
                        s.Line, s.Column);
                case ListConsAlt c:
                    return new ListConsAlt(
                        SubstituteTypeExpr(c.Head, substitution, templates, instantiations, monoNames: monoNames),
                        SubstituteTypeExpr(c.Tail, substitution, templates, instantiations, monoNames: monoNames),
                        c.Line, c.Column);
                case DiffListAlt d:
                    return new DiffListAlt(
                        SubstituteTypeExpr(d.Content, substitution, templates, instantiations, monoNames: monoNames),
                        SubstituteTypeExpr(d.Hole, substitution, templates, instantiations, monoNames: monoNames),
                        d.Line, d.Column);
                default:
                    // PrimitiveModeAlt, ConstantAlt, ListNilAlt — no substitution needed
                    return expr;
            }
        }

        /// <summary>
        /// Replace parameterized type refs with expanded names (for non-template types and proc decls).
        /// monoNames: when all args are wildcards AND base name is in monoNames, use base name.
        /// </summary>
        private static TypeExpr ReplaceParamRefs(TypeExpr expr,
            IDictionary<string, TypeDef> templates,
            IReadOnlySet<string>? monoNames = null)
        {
            monoNames ??= ImmutableHashSet<string>.Empty;
            switch (expr)
            {
                case TypeRef r:
                    if (IsTemplateRef(r, templates))
                    {
                        // Replace args recursively first
                        var replacedArgs = r.TypeArgs
                            .Select(a => ReplaceParamRefs(a, templates, monoNames: monoNames))
                            .ToList();
                        // If all args are wildcards (_) AND base name exists as monomorphic, use base name.
                        var allWildcards = replacedArgs.All(a => a is PrimitiveModeAlt);
                        if (allWildcards && monoNames.Contains(r.Name))
                            return new TypeRef(r.Name, r.Line, r.Column, isInput: r.IsInput);
                        var expandedName = ExpandedName(r.Name, replacedArgs);
                        return new TypeRef(expandedName, r.Line, r.Column, isInput: r.IsInput);
                    }
                    // Recurse into typeArgs even if not a template ref
                    if (r.TypeArgs.Count > 0)
                    {
                        var replacedArgs = r.TypeArgs
                            .Select(a => ReplaceParamRefs(a, templates, monoNames: monoNames))
                            .ToList();
                        return new TypeRef(r.Name, r.Line, r.Column, isInput: r.IsInput, typeArgs: replacedArgs);
                    }
                    return expr;
                case StructAlt s:
                    return new StructAlt(s.Functor,
                        s.Args.Select(a => ReplaceParamRefs(a, templates, monoNames: monoNames)).ToList(),
                        s.Line, s.Column);
                case ListConsAlt c:
                    return new ListConsAlt(
                        ReplaceParamRefs(c.Head, templates, monoNames: monoNames),
                        ReplaceParamRefs(c.Tail, templates, monoNames: monoNames),
                        c.Line, c.Column);
                case DiffListAlt d:
                    return new DiffListAlt(
                        ReplaceParamRefs(d.Content, templates, monoNames: monoNames),
                        ReplaceParamRefs(d.Hole, templates, monoNames: monoNames),
                        d.Line, d.Column);
                default:
                    return expr;
            }
        }
    }
}
