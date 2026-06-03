// lib/analysis/type_checker/type_checker.cs
//
// Main type checker implementing GLP well-typed program checking.
// Specification: docs/modules/well-typed-program.md v0.7
// Paper Reference: Definition 4.10 (lines 351-357)
//
// A typed GLP program P = (Cs, D) is well-typed if:
// 1. Covariance: Every clause C ∈ Cs is well-typed by D
// 2. Contravariance: Every input path in every procedure type in D has
//    a clause C ∈ Cs that accepts it

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clause = GlpRuntime.Compiler.Clause;
using Term = GlpRuntime.Compiler.Term;
using VarTerm = GlpRuntime.Compiler.VarTerm;
using StructTerm = GlpRuntime.Compiler.StructTerm;
using ListTerm = GlpRuntime.Compiler.ListTerm;
using GlpRuntime.Compiler;

namespace GlpRuntime.Analysis.TypeChecker;

// =============================================================================
// Result Types
// =============================================================================

/// <summary>Result of type checking a program.</summary>
public sealed class TypeCheckResult
{
    /// <summary>Type errors found during checking.</summary>
    public IReadOnlyList<TypeError> Errors { get; }

    /// <summary>Type warnings found during checking.</summary>
    public IReadOnlyList<TypeWarning> Warnings { get; }

    public TypeCheckResult(IReadOnlyList<TypeError> errors, IReadOnlyList<TypeWarning> warnings)
    {
        Errors   = errors;
        Warnings = warnings;
    }

    /// <summary>True if no errors were found.</summary>
    public bool IsWellTyped => Errors.Count == 0;

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Errors.Count > 0)
        {
            sb.Append("Type Errors:").Append('\n');
            foreach (var e in Errors)
            {
                sb.Append($"  {e}").Append('\n');
            }
        }
        if (Warnings.Count > 0)
        {
            sb.Append("Warnings:").Append('\n');
            foreach (var w in Warnings)
            {
                sb.Append($"  {w}").Append('\n');
            }
        }
        if (IsWellTyped && Warnings.Count == 0)
        {
            sb.Append("Program is well-typed.").Append('\n');
        }
        return sb.ToString();
    }
}

/// <summary>A type error (diagnostic value object — never thrown).</summary>
public sealed class TypeError
{
    public string Message { get; }
    public int Line { get; }
    public int Column { get; }
    public string? ClauseText { get; }

    public TypeError(string message, int line, int column, string? clauseText = null)
    {
        Message    = message;
        Line       = line;
        Column     = column;
        ClauseText = clauseText;
    }

    public override string ToString()
    {
        var loc = $"line {Line.ToString(CultureInfo.InvariantCulture)}, column {Column.ToString(CultureInfo.InvariantCulture)}";
        return ClauseText is not null
            ? $"{Message} at {loc}\n    in: {ClauseText}"
            : $"{Message} at {loc}";
    }
}

/// <summary>A type warning (diagnostic value object — never thrown).</summary>
public sealed class TypeWarning
{
    public string Message { get; }
    public int Line { get; }
    public int Column { get; }

    public TypeWarning(string message, int line, int column)
    {
        Message = message;
        Line    = line;
        Column  = column;
    }

    public override string ToString() =>
        $"{Message} at line {Line.ToString(CultureInfo.InvariantCulture)}, column {Column.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>Error for uncovered input alternative.</summary>
public sealed class CoverageError
{
    public string Procedure { get; }
    public int ArgIndex { get; }
    public string UncoveredLabel { get; }
    public string Path { get; }

    public CoverageError(string procedure, int argIndex, string uncoveredLabel, string path)
    {
        Procedure       = procedure;
        ArgIndex        = argIndex;
        UncoveredLabel  = uncoveredLabel;
        Path            = path;
    }

    public override string ToString() =>
        $"{Procedure} argument {ArgIndex.ToString(CultureInfo.InvariantCulture)}: uncovered alternative \"{UncoveredLabel}\" at path: {Path}";
}

// =============================================================================
// Main Type Checker
// =============================================================================

/// <summary>
/// The main type checker implementing Definition 4.10.
/// </summary>
public sealed class TypeChecker
{
    public TypeEnvironment TypeEnv { get; }
    public ProgramDFA Dfa { get; }

    // Static readonly Regex fields (compiled once per class, not per call)
    private static readonly Regex ArgIndexRegex = new(@"\((\d+),(\d+)\)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DiffArityRegex = new(@"\\\((\d+),",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FunctorArityRegex = new(@"(\w+)\((\d+),",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public TypeChecker(TypeEnvironment typeEnv)
    {
        TypeEnv = typeEnv;
        Dfa     = ProgramDfaBuilder.Build(typeEnv);
    }

    // =========================================================================
    // Public entry point
    // =========================================================================

    /// <summary>
    /// Check a program (list of clauses) against declared types.
    ///
    /// Implements Definition 4.10:
    /// 1. Covariance: Every clause is well-typed
    /// 2. Contravariance: Every input path is covered
    /// </summary>
    public TypeCheckResult Check(IReadOnlyList<Clause> clauses)
    {
        var errors   = new List<TypeError>();
        var warnings = new List<TypeWarning>();

        // =====================================================================
        // Phase 0: Validate clause terms (anonymous variable restrictions)
        // Per spec: clause-validation.md - reject _? everywhere, reject _ in bodies
        // =====================================================================
        foreach (var clause in clauses)
        {
            try
            {
                // Validate head arguments
                foreach (var arg in clause.Head.Args)
                {
                    ClauseValidation.ValidateClauseHead(arg);
                }
                // Validate guard arguments
                if (clause.Guards is not null)
                {
                    foreach (var guard in clause.Guards)
                    {
                        foreach (var arg in guard.Args)
                        {
                            ClauseValidation.ValidateGuard(arg);
                        }
                    }
                }
                // Validate body arguments
                if (clause.Body is not null)
                {
                    foreach (var goal in clause.Body)
                    {
                        foreach (var arg in goal.Args)
                        {
                            ClauseValidation.ValidateClauseBody(arg);
                        }
                    }
                }
            }
            catch (CompileError e)
            {
                errors.Add(new TypeError(
                    e.Message,
                    (int)e.Line,
                    (int)e.Column,
                    ClauseToString(clause)));
            }
        }

        // If validation errors, return early
        if (errors.Count > 0)
        {
            return new TypeCheckResult(errors, warnings);
        }

        // =====================================================================
        // Phase 1: Group clauses by procedure (name/arity)
        // =====================================================================
        var procedureClauses = new Dictionary<string, List<Clause>>(StringComparer.Ordinal);
        foreach (var clause in clauses)
        {
            var key = $"{clause.Head.Functor}/{clause.Head.Arity.ToString(CultureInfo.InvariantCulture)}";
            if (!procedureClauses.TryGetValue(key, out var list))
            {
                list = new List<Clause>();
                procedureClauses[key] = list;
            }
            list.Add(clause);
        }

        // =====================================================================
        // Phase 2: Check each declared procedure
        // =====================================================================
        foreach (var procDecl in TypeEnv.Procedures.Values)
        {
            var key        = procDecl.Key;
            var procClauses = procedureClauses.TryGetValue(key, out var cl) ? cl : null;

            if (procClauses is null || procClauses.Count == 0)
            {
                // Skip warning for builtins (implemented in runtime, no GLP clauses)
                if (!procDecl.IsBuiltin)
                {
                    warnings.Add(new TypeWarning(
                        $"Procedure {procDecl.Name}/{procDecl.Arity.ToString(CultureInfo.InvariantCulture)} declared but not defined",
                        procDecl.Line,
                        procDecl.Column));
                }
                continue;
            }

            // Check this procedure
            var procResult = CheckProcedure(procDecl, procClauses);
            errors.AddRange(procResult.Errors);
            warnings.AddRange(procResult.Warnings);
        }

        // =====================================================================
        // Phase 3: Warn about undefined procedures (clauses without type declarations)
        // =====================================================================
        foreach (var entry in procedureClauses)
        {
            if (!TypeEnv.Procedures.ContainsKey(entry.Key))
            {
                var firstClause = entry.Value[0];
                warnings.Add(new TypeWarning(
                    $"Procedure {entry.Key} has no type declaration",
                    firstClause.Line,
                    firstClause.Column));
            }
        }

        return new TypeCheckResult(errors, warnings);
    }

    // =========================================================================
    // Private instance methods
    // =========================================================================

    /// <summary>Check a single procedure against its declared type.</summary>
    private TypeCheckResult CheckProcedure(ProcDecl decl, IReadOnlyList<Clause> clauses)
    {
        var errors   = new List<TypeError>();
        var warnings = new List<TypeWarning>();

        // Condition 1: Covariance — check each clause is well-typed
        foreach (var clause in clauses)
        {
            errors.AddRange(CheckClauseCovariance(clause, decl));
        }

        // Condition 2: Contravariance — check input coverage
        for (int argIndex = 1; argIndex <= decl.Arity; argIndex++)
        {
            if (decl.IsInputArg(argIndex - 1))
            {
                errors.AddRange(CheckInputCoverage(clauses, decl, argIndex));
            }
        }

        return new TypeCheckResult(errors, warnings);
    }

    // =========================================================================
    // Covariance Checking (Condition 1)
    // =========================================================================

    /// <summary>Check covariance for a single clause using WellTypedClause module.</summary>
    private List<TypeError> CheckClauseCovariance(Clause clause, ProcDecl decl)
    {
        var errors = new List<TypeError>();

        try
        {
            var result = WellTypedClause.CheckClauseFromAst(clause, Dfa, TypeEnv);
            if (!result.IsWellTyped)
            {
                // Convert ClauseErrors to TypeErrors
                foreach (var error in result.Errors)
                {
                    errors.Add(new TypeError(
                        error.Message,
                        clause.Line,
                        clause.Column,
                        ClauseToString(clause)));
                }
            }
        }
        catch (UndeclaredProcedureError e)
        {
            errors.Add(new TypeError(
                $"Undeclared procedure: {e.Functor}/{e.Arity.ToString(CultureInfo.InvariantCulture)}",
                clause.Line,
                clause.Column,
                ClauseToString(clause)));
        }
        catch (Exception e)
        {
            // Catch other errors (type compilation failures, etc.)
            errors.Add(new TypeError(
                $"Error checking clause: {e.Message}",
                clause.Line,
                clause.Column,
                ClauseToString(clause)));
        }

        return errors;
    }

    // =========================================================================
    // Contravariance Checking (Condition 2) - Structural Coverage
    // =========================================================================

    /// <summary>
    /// Check that all input alternatives are covered by clause heads.
    ///
    /// For input argument at position argIndex, we traverse the input type DFA
    /// and verify each transition (alternative) is covered by some clause.
    /// </summary>
    private List<TypeError> CheckInputCoverage(
        IReadOnlyList<Clause> clauses,
        ProcDecl decl,
        int argIndex)
    {
        var errors = new List<TypeError>();

        // Get the input type
        var argType = decl.ArgTypes[argIndex - 1];

        // Handle primitive/wildcard types (_ or _?)
        // Per spec v0.7: Wildcard types are FINAL STATES requiring NO coverage checking.
        // The type _? means "accept any consumed term" - it does NOT mean
        // "clauses must cover all possible terms".
        if (argType is PrimitiveModeAlt)
        {
            return new List<TypeError>(); // No coverage check needed for wildcards
        }

        // Handle named type references
        var typeRef = (TypeRef)argType;

        // For input arguments, we need the complement automaton (T?)
        // to check what values the argument can receive
        var inputTypeName = typeRef.IsInput ? $"{typeRef.Name}?" : typeRef.Name;

        // Get automaton for the input type
        Automaton inputAutomaton;
        try
        {
            inputAutomaton = Dfa.LookupAutomaton(inputTypeName);
        }
        catch (Exception e)
        {
            errors.Add(new TypeError(
                $"Cannot get automaton for type {inputTypeName}: {e.Message}",
                decl.Line,
                decl.Column));
            return errors;
        }

        // Track visited states to handle recursive types
        var visited = new HashSet<string>(StringComparer.Ordinal);

        // Check coverage starting from the start state
        var coverageErrors = CheckStateCoverage(
            inputAutomaton.StartState,
            clauses,
            argIndex,
            typeRef.Name,
            visited,
            inputAutomaton,
            decl);

        foreach (var coverageError in coverageErrors)
        {
            errors.Add(new TypeError(
                coverageError.ToString(),
                decl.Line,
                decl.Column));
        }

        return errors;
    }

    /// <summary>
    /// Recursively check coverage at a DFA state, tracking structural path.
    ///
    /// structPath: list of argument indices taken from root (e.g., [1, 2] means
    /// "arg 1 of root, then arg 2 of that substructure")
    /// </summary>
    private List<CoverageError> CheckStateCoverage(
        DFAState state,
        IReadOnlyList<Clause> clauses,
        int argIndex,
        string pathPrefix,
        HashSet<string> visited,
        Automaton automaton,
        ProcDecl decl,
        IReadOnlyList<int>? structPath = null)
    {
        var errors = new List<CoverageError>();
        var path   = structPath ?? Array.Empty<int>();

        // Prevent infinite recursion on recursive types
        if (visited.Contains(state.Name))
        {
            return errors;
        }
        visited.Add(state.Name);

        // Leaf states (primitives) don't need structural coverage
        // They're covered by variables matching the appropriate mode
        // Primitive states are _ or _?
        if (state.BaseName == "_")
        {
            return errors;
        }

        // Final states also don't need further coverage
        if (state.IsFinal)
        {
            return errors;
        }

        // Check if any clause has a VARIABLE at this path position
        // If so, the variable covers ALL alternatives - no need to check further
        if (AnyClauseHasVariableAtPath(clauses, argIndex, path))
        {
            return errors; // Variable covers everything
        }

        // Get all transitions (alternatives) from this state
        var transitions = GetTransitionsFromState(state, automaton);

        foreach (var entry in transitions)
        {
            var label       = entry.Key;
            var targetState = entry.Value;

            // Check if some clause accepts this transition at the current path
            if (ClauseAcceptsLabelAtPath(clauses, argIndex, path, label))
            {
                // Recursively check the target state with extended path
                var newPath = $"{pathPrefix} → {label}";
                // Extract arg index from symbol like "f(2,1)" or "\(2,1)" or "[|](2,1)"
                var argIdxFromLabel = ExtractArgIndex(label);
                IReadOnlyList<int> newStructPath = argIdxFromLabel is not null
                    ? new List<int>(path) { argIdxFromLabel.Value }
                    : path;
                var nestedErrors = CheckStateCoverage(
                    targetState,
                    clauses,
                    argIndex,
                    newPath,
                    visited,
                    automaton,
                    decl,
                    structPath: newStructPath);
                errors.AddRange(nestedErrors);
            }
            else
            {
                // No clause covers this alternative
                errors.Add(new CoverageError(
                    procedure:       decl.Name,
                    argIndex:        argIndex,
                    uncoveredLabel:  label,
                    path:            $"{pathPrefix} → {label}"));
            }
        }

        return errors;
    }

    /// <summary>Check if any clause has a variable at the given structural path.</summary>
    private bool AnyClauseHasVariableAtPath(
        IReadOnlyList<Clause> clauses,
        int argIndex,
        IReadOnlyList<int> structPath)
    {
        foreach (var clause in clauses)
        {
            if (argIndex > clause.Head.Args.Count) continue;
            var topArg    = clause.Head.Args[argIndex - 1];
            var termAtPath = NavigateToPath(topArg, structPath);
            if (termAtPath is VarTerm or UnderscoreTerm)
            {
                return true; // Variable at this path covers all alternatives
            }
        }
        return false;
    }

    /// <summary>Check if any clause accepts the label at the given structural path.</summary>
    private bool ClauseAcceptsLabelAtPath(
        IReadOnlyList<Clause> clauses,
        int argIndex,
        IReadOnlyList<int> structPath,
        string labelStr)
    {
        foreach (var clause in clauses)
        {
            if (argIndex > clause.Head.Args.Count) continue;
            var topArg    = clause.Head.Args[argIndex - 1];
            var termAtPath = NavigateToPath(topArg, structPath);

            if (termAtPath is null) continue;

            // Variable accepts anything
            if (termAtPath is VarTerm or UnderscoreTerm)
            {
                return true;
            }

            // Get labels from the term at this path
            var labels = WellTypedClause.GetLabelsFromTerm(termAtPath);
            if (labels is null)
            {
                return true; // null means wildcard
            }

            if (LabelsMatch(labels, labelStr))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Check if any clause accepts the given label at the argument position (UNUSED — retained for source-parity).</summary>
    private bool ClauseAcceptsLabel(IReadOnlyList<Clause> clauses, int argIndex, string labelStr)
    {
        foreach (var clause in clauses)
        {
            var acceptedLabels = WellTypedClause.GetAcceptedLabels(clause, argIndex, TypeEnv);

            // null means wildcard (variable) - accepts anything
            if (acceptedLabels is null)
            {
                return true;
            }

            // Check if clause explicitly matches this label
            if (LabelsMatch(acceptedLabels, labelStr))
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================================
    // Static helpers (no instance state)
    // =========================================================================

    /// <summary>
    /// Navigate into a term following a structural path.
    /// structPath is a list of 1-based argument indices.
    /// </summary>
    private static Term? NavigateToPath(Term term, IReadOnlyList<int> structPath)
    {
        Term? current = term;
        foreach (var idx in structPath)
        {
            switch (current)
            {
                case null:
                    return null;
                case StructTerm s when idx >= 1 && idx <= s.Args.Count:
                    current = s.Args[idx - 1];
                    break;
                case StructTerm:
                    return null;
                case ListTerm l when !l.IsNil && idx == 1:
                    current = l.Head;
                    break;
                case ListTerm l when !l.IsNil && idx == 2:
                    current = l.Tail;
                    break;
                default:
                    return null;
            }
        }
        return current;
    }

    /// <summary>
    /// Extract argument index from a path element symbol.
    /// Symbols like "f(2,1)" or "\(2,1)" or "[|](2,1)" have format functor(arity,argIndex).
    /// </summary>
    private static int? ExtractArgIndex(string symbol)
    {
        var match = ArgIndexRegex.Match(symbol);
        if (match.Success && int.TryParse(
                match.Groups[2].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var idx))
        {
            return idx;
        }
        return null;
    }

    /// <summary>Get all transitions from a state.</summary>
    private static Dictionary<string, DFAState> GetTransitionsFromState(
        DFAState state,
        Automaton automaton)
    {
        var result = new Dictionary<string, DFAState>(StringComparer.Ordinal);
        foreach (var entry in automaton.Transitions)
        {
            var (fromState, label) = entry.Key;
            if (ReferenceEquals(fromState, state))
            {
                // Convert TransitionLabel to string for label matching
                result[label.ToString()!] = entry.Value;
            }
        }
        return result;
    }

    /// <summary>Check if any of the accepted labels match the DFA transition label.</summary>
    private static bool LabelsMatch(IReadOnlySet<string> acceptedLabels, string labelStr)
    {
        // Direct match
        if (acceptedLabels.Contains(labelStr))
        {
            return true;
        }

        // Handle list labels: [|](2,1) and [|](2,2) should match [|]
        if (labelStr.StartsWith("[|](", StringComparison.Ordinal))
        {
            if (acceptedLabels.Contains("[|]"))
            {
                return true;
            }
        }

        // Handle nil: [] is both a label and constant
        if (string.Equals(labelStr, "[]", StringComparison.Ordinal))
        {
            return acceptedLabels.Contains("[]");
        }

        // Handle DiffList labels: \(2,1) and \(2,2) should match \/2
        if (labelStr.StartsWith("\\(", StringComparison.Ordinal))
        {
            // Extract arity from \(2,1) format -> 2
            var diffMatch = DiffArityRegex.Match(labelStr);
            if (diffMatch.Success)
            {
                var arity = diffMatch.Groups[1].Value;
                if (acceptedLabels.Contains($"\\/{arity}"))
                {
                    return true;
                }
            }
            // Also check raw \ or \\
            if (acceptedLabels.Contains(@"\") || acceptedLabels.Contains(@"\\"))
            {
                return true;
            }
        }

        // Handle functor labels: f(2,1) should match f/2
        var match = FunctorArityRegex.Match(labelStr);
        if (match.Success)
        {
            var functor = match.Groups[1].Value;
            var arity   = match.Groups[2].Value;
            if (acceptedLabels.Contains($"{functor}/{arity}"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Convert clause to string for error messages.</summary>
    private static string ClauseToString(Clause clause)
    {
        var head = $"{clause.Head.Functor}({clause.Head.Args.Count.ToString(CultureInfo.InvariantCulture)} args)";
        if (clause.Body is null || clause.Body.Count == 0)
        {
            return $"{head}.";
        }
        return $"{head} :- {clause.Body.Count.ToString(CultureInfo.InvariantCulture)} goals.";
    }
}

// =============================================================================
// Convenience functions for type checking
// =============================================================================

/// <summary>
/// Static driver hosting the two free orchestrator functions.
/// Dart top-level functions → C# static class methods per project convention.
/// </summary>
public static class TypeCheckerDriver
{
    /// <summary>
    /// Type-check a parsed Module.
    ///
    /// This is the primary entry point for type checking.
    /// The module should be parsed using the main parser.
    ///
    /// If transformedProcedures is provided, uses those instead of module.procedures.
    /// This allows running partial evaluation (defined guard expansion) before type checking.
    ///
    /// If ancestorScope is provided, it is used as the base type environment
    /// (prelude + ancestor self.glp definitions) instead of just the prelude.
    /// See module_hierarchy for how ancestor scopes are assembled.
    /// </summary>
    public static TypeCheckResult CheckModule(
        Module module,
        IReadOnlyList<GlpRuntime.Compiler.Procedure>? transformedProcedures = null,
        TypeEnvironment? ancestorScope = null)
    {
        // Build base environment first so we know all type names for expansion.
        // This avoids mistaking prelude type names for type parameters.
        var baseEnv = ancestorScope ?? TypeEnvironmentBuilder.BuildPreludeEnvironment();

        // Expand parameterized types to monomorphic equivalents before type checking.
        // Pass prelude/ancestor templates so downstream modules can expand references
        // to parameterized types defined in ancestor scopes.
        var expandedModule = ParamExpansion.ExpandParameterizedTypes(
            module,
            knownTypeNames:    baseEnv.Types.Keys.ToHashSet(StringComparer.Ordinal),
            externalTemplates: baseEnv.TypeTemplates);

        // Build type environment from expanded module (reuses baseEnv)
        var typeEnv = TypeEnvironmentBuilder.BuildTypeEnvironment(expandedModule, ancestorScope: baseEnv);

        // Extract clauses — from transformed procedures if provided, otherwise from module
        var clauses    = new List<Clause>();
        var procedures = transformedProcedures ?? module.Procedures;
        foreach (var proc in procedures)
        {
            clauses.AddRange(proc.Clauses);
        }

        // Run type checker
        return new TypeChecker(typeEnv).Check(clauses);
    }

    /// <summary>
    /// Parse and type-check GLP source code.
    ///
    /// Convenience function that parses source and runs type checker.
    /// Synchronous — NOT async Task&lt;TypeCheckResult&gt;.
    /// </summary>
    public static TypeCheckResult CheckSource(string source)
    {
        var lexer  = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var module = parser.ParseModule();
        return CheckModule(module);
    }
}
