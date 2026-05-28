// lib/analysis/type_checker/well_typed_term.cs
//
// Well-typed moded term checking for GLP type system.
// Specification: docs/type system/well-typed-term.md v0.7
// Paper Reference: Definition 5.4 (Well-Typed Moded Term)
//
// Determines when a moded term is well-typed by an automaton by checking path
// consistency via automaton traversal.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace GlpRuntime.Analysis.TypeChecker;

// =============================================================================
// Result Types
// =============================================================================

/// <summary>Result of checking if a moded term is well-typed.</summary>
public sealed class WellTypedResult
{
    /// <summary>Whether the moded term is well-typed by the automaton.</summary>
    public bool IsWellTyped { get; }

    /// <summary>
    /// Type assignments for each variable occurrence.
    /// Key format: "X" for writers, "X?" for readers.
    /// </summary>
    public IReadOnlyDictionary<string, VariableTypeInfo> VariableTypes { get; }

    /// <summary>List of errors found during checking.</summary>
    public IReadOnlyList<WellTypedError> Errors { get; }

    public WellTypedResult(
        bool isWellTyped,
        IReadOnlyDictionary<string, VariableTypeInfo> variableTypes,
        IReadOnlyList<WellTypedError> errors)
    {
        IsWellTyped = isWellTyped;
        VariableTypes = variableTypes;
        Errors = errors;
    }

    public static WellTypedResult Success(IReadOnlyDictionary<string, VariableTypeInfo> variableTypes)
        => new WellTypedResult(
            isWellTyped: true,
            variableTypes: variableTypes,
            errors: Array.Empty<WellTypedError>());

    public static WellTypedResult Failure(
        IReadOnlyList<WellTypedError> errors,
        IReadOnlyDictionary<string, VariableTypeInfo>? variableTypes = null)
        => new WellTypedResult(
            isWellTyped: false,
            variableTypes: variableTypes ?? new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal),
            errors: errors);
}

/// <summary>Type information for a variable occurrence.</summary>
public sealed class VariableTypeInfo : IEquatable<VariableTypeInfo>
{
    /// <summary>The DFA state where this variable appears (includes isComplement).</summary>
    public DFAState TypeState { get; }

    /// <summary>The mode at this position (consume for readers, produce for writers).</summary>
    public Mode Mode { get; }

    /// <summary>Whether this is a reader variable.</summary>
    public bool IsReader { get; }

    public VariableTypeInfo(DFAState typeState, Mode mode, bool isReader)
    {
        TypeState = typeState;
        Mode = mode;
        IsReader = isReader;
    }

    public override string ToString()
        => $"({TypeState.Name}, {(Mode == ModeAliases.Consume ? "↓" : "↑")})";

    public bool Equals(VariableTypeInfo? other)
        => other is not null
            && TypeState == other.TypeState
            && Mode == other.Mode
            && IsReader == other.IsReader;

    public override bool Equals(object? obj) => Equals(obj as VariableTypeInfo);

    public override int GetHashCode() => HashCode.Combine(TypeState, Mode, IsReader);

    public static bool operator ==(VariableTypeInfo? left, VariableTypeInfo? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(VariableTypeInfo? left, VariableTypeInfo? right)
        => !(left == right);
}

/// <summary>Base class for well-typing errors.</summary>
public abstract class WellTypedError
{
    public abstract string Message { get; }
}

/// <summary>Error: path has no matching automaton transition.</summary>
public sealed class InconsistentPathError : WellTypedError
{
    public ModedPath Path { get; }
    public string Reason { get; }

    public InconsistentPathError(ModedPath path, string reason)
    {
        Path = path;
        Reason = reason;
    }

    public override string Message => $"Inconsistent path: {Reason}\n  Path: {Path}";

    public override string ToString() => Message;
}

/// <summary>Error: same variable has different types at different occurrences.</summary>
public sealed class InconsistentVariableError : WellTypedError
{
    public string VariableName { get; }
    public VariableTypeInfo FirstOccurrence { get; }
    public VariableTypeInfo SecondOccurrence { get; }

    public InconsistentVariableError(
        string variableName,
        VariableTypeInfo firstOccurrence,
        VariableTypeInfo secondOccurrence)
    {
        VariableName = variableName;
        FirstOccurrence = firstOccurrence;
        SecondOccurrence = secondOccurrence;
    }

    public override string Message
        => $"Variable {VariableName} has inconsistent types: {FirstOccurrence} vs {SecondOccurrence}";

    public override string ToString() => Message;
}

/// <summary>Error: variable pair (X, X?) not dual.</summary>
public sealed class NonDualError : WellTypedError
{
    public string BaseName { get; }
    public VariableTypeInfo? WriterType { get; }
    public VariableTypeInfo? ReaderType { get; }
    public string? Reason { get; }

    public NonDualError(
        string baseName,
        VariableTypeInfo? writerType,
        VariableTypeInfo? readerType,
        string? reason = null)
    {
        BaseName = baseName;
        WriterType = writerType;
        ReaderType = readerType;
        Reason = reason;
    }

    public override string Message
    {
        get
        {
            var reasonStr = Reason != null ? $": {Reason}" : "";
            return $"Variable pair ({BaseName}, {BaseName}?) not dual{reasonStr}: writer={WriterType}, reader={ReaderType}";
        }
    }

    public override string ToString() => Message;
}

/// <summary>Result of checking a single path against automaton.</summary>
public sealed class PathCheckResult
{
    public bool IsConsistent { get; }
    public string? Reason { get; }
    public VariableTypeInfo? VariableAssignment { get; }

    public PathCheckResult(bool isConsistent, string? reason, VariableTypeInfo? variableAssignment)
    {
        IsConsistent = isConsistent;
        Reason = reason;
        VariableAssignment = variableAssignment;
    }

    public static PathCheckResult Consistent(VariableTypeInfo? assignment = null)
        => new PathCheckResult(isConsistent: true, reason: null, variableAssignment: assignment);

    public static PathCheckResult Inconsistent(string reason)
        => new PathCheckResult(isConsistent: false, reason: reason, variableAssignment: null);
}

// =============================================================================
// Public Functions
// =============================================================================

/// <summary>
/// Host static class for well-typed moded term checking functions.
/// </summary>
public static class WellTypedTerm
{
    /// <summary>
    /// Check if a moded term is well-typed by an automaton.
    ///
    /// Per Definition 4.5: A moded term T is well-typed by a GLP type D if:
    /// 1. For each term path x ∈ paths(T) there is a consistent path in the automaton
    /// 2. For every pair of variables in T, their types are complementary
    ///
    /// Returns WellTypedResult with:
    /// - IsWellTyped: true iff all paths consistent and variable pairs complementary
    /// - VariableTypes: type assignments for each variable
    /// - Errors: list of all violations found
    /// </summary>
    public static WellTypedResult CheckModedTerm(ModedTerm term, Automaton automaton, ProgramDFA dfa)
    {
        var errors = new List<WellTypedError>();
        var variableTypes = new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal);

        // Step 1: Extract and check all paths
        var termPaths = ModedTermOps.Paths(term);

        foreach (var path in termPaths)
        {
            var result = CheckPathAgainstAutomaton(path, automaton, dfa);

            if (!result.IsConsistent)
            {
                errors.Add(new InconsistentPathError(path, result.Reason ?? "Unknown"));
            }
            else if (result.VariableAssignment != null)
            {
                // Record variable type
                var varKey = VariableKey(path.Leaf);

                if (variableTypes.ContainsKey(varKey))
                {
                    // Same variable appears multiple times - types must match
                    var existing = variableTypes[varKey];
                    if (existing.TypeState.Name != result.VariableAssignment.TypeState.Name)
                    {
                        errors.Add(new InconsistentVariableError(varKey, existing, result.VariableAssignment));
                    }
                }
                else
                {
                    variableTypes[varKey] = result.VariableAssignment;
                }
            }
        }

        // Step 2: Check variable pair duality
        var dualityErrors = CheckDuality(variableTypes);
        errors.AddRange(dualityErrors);

        return new WellTypedResult(
            isWellTyped: errors.Count == 0,
            variableTypes: variableTypes,
            errors: errors);
    }

    /// <summary>
    /// Check if a single moded path is consistent with the automaton.
    ///
    /// Per Definition 4.3: A moded path is consistent with the automaton if:
    /// 1. Structure matches: each non-leaf step corresponds to valid transition
    /// 2. Leaf consistency: variable/constant at leaf matches DFA state
    ///
    /// Fix 4.1: Switches automata at type boundaries when entering user-defined types.
    /// </summary>
    public static PathCheckResult CheckPathAgainstAutomaton(ModedPath path, Automaton automaton, ProgramDFA dfa)
    {
        var state = automaton.StartState;
        var currentAutomaton = automaton; // Track current automaton for type switching

        // Handle single-step paths (just a variable or constant at root)
        if (path.Length == 1)
        {
            return CheckLeafConsistencyForPath(path.Leaf, state, dfa);
        }

        // Traverse path, following automaton transitions
        for (int i = 0; i < path.Length - 1; i++)
        {
            var step = path.Steps[i];
            var nextStep = path.Steps[i + 1];

            // Build transition label from path step
            var label = BuildTransitionLabel(step, nextStep);

            // Try to follow transition
            DFAState? nextState = currentAutomaton.Transition(state, label);

            if (nextState == null)
            {
                // Case 3 (Type path shorter - wildcard in type): Per Definition 4.5 v0.7
                // When at a wildcard state with no outgoing transition, check ONLY the
                // structural mode at this position. The remainder of the term path beyond
                // the wildcard is NOT examined - the wildcard accepts the entire subterm.
                if (state.IsWildcard)
                {
                    // The structural mode at position |y| is nextStep.mode (the mode at
                    // the first position beyond where the type path can follow)
                    var structuralModeAtWildcard = nextStep.Mode;
                    var expectedMode = state.IsDual ? ModeAliases.Consume : ModeAliases.Produce;

                    if (structuralModeAtWildcard == expectedMode)
                    {
                        // Mode matches - wildcard accepts entire subterm, path is consistent.
                        // We don't record variable assignments here because the variables
                        // inside the wildcard-accepted subterm are handled separately.
                        return PathCheckResult.Consistent();
                    }

                    // Mode mismatch at wildcard position
                    return PathCheckResult.Inconsistent(
                        $"Mode mismatch at wildcard {state.Name}: expected {(expectedMode == ModeAliases.Consume ? "↓" : "↑")}, got {(structuralModeAtWildcard == ModeAliases.Consume ? "↓" : "↑")}");
                }

                return PathCheckResult.Inconsistent(
                    $"No transition for {label} from state {state.Name}");
            }

            // Fix 4.1: Switch automata at type boundaries
            // When entering a user-defined type, switch to that type's automaton
            if (nextState.IsUserDefinedType && nextState.BaseName != state.BaseName)
            {
                try
                {
                    currentAutomaton = dfa.LookupAutomaton(nextState.Name);
                }
                catch (Exception)
                {
                    return PathCheckResult.Inconsistent(
                        $"Cannot get automaton for type {nextState.Name}");
                }
            }

            state = nextState;
        }

        // Check leaf consistency
        return CheckLeafConsistencyForPath(path.Leaf, state, dfa);
    }

    // =============================================================================
    // Internal Helpers
    // =============================================================================

    /// <summary>Build transition label from path steps.</summary>
    private static TransitionLabel BuildTransitionLabel(PathStep currentStep, PathStep nextStep)
    {
        // Parse functor/arity from current step symbol (e.g., "[|]/2" → "[|]", 2)
        var parts = currentStep.Symbol.Split('/');
        if (parts.Length != 2)
        {
            // Leaf node (variable or constant) - shouldn't happen for non-leaf steps
            return TransitionLabel.Functor(currentStep.Symbol, 0, nextStep.ArgIndex, mode: nextStep.Mode);
        }

        var functor = parts[0];
        var arity = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedArity)
            ? parsedArity
            : 0;

        // Label encodes: functor(arity, argIndex) with mode from next step
        return TransitionLabel.Functor(functor, arity, nextStep.ArgIndex, mode: nextStep.Mode);
    }

    /// <summary>Check leaf consistency with DFA state using program_dfa.cs's CheckLeafConsistency.</summary>
    private static PathCheckResult CheckLeafConsistencyForPath(PathStep leaf, DFAState state, ProgramDFA dfa)
    {
        // Convert PathStep to LeafTerm for CheckLeafConsistency
        var leafTerm = PathStepToLeafTerm(leaf);

        var result = ProgramDfaBuilder.CheckLeafConsistency(leafTerm, state, dfa);

        if (result.IsConsistent)
        {
            if (leaf.IsVariable)
            {
                var isReader = leaf.IsReader;
                var mode = isReader ? ModeAliases.Consume : ModeAliases.Produce;
                return PathCheckResult.Consistent(new VariableTypeInfo(
                    typeState: result.Type ?? state,
                    mode: mode,
                    isReader: isReader));
            }
            return PathCheckResult.Consistent();
        }
        else
        {
            return PathCheckResult.Inconsistent(result.Reason ?? "Leaf inconsistent");
        }
    }

    /// <summary>
    /// Convert PathStep to LeafTerm for CheckLeafConsistency.
    /// Fix 4.2: Properly detects real literals (floating-point numbers).
    /// Fix Bug 1: Atoms are strings per paper Section 4.1.
    /// </summary>
    private static LeafTerm PathStepToLeafTerm(PathStep step)
    {
        if (step.IsVariable)
        {
            // Use the actual structural mode from the path step, not a hardcoded mode
            if (step.IsReader)
            {
                return LeafTerm.Reader(step.Symbol, mode: step.Mode);
            }
            else
            {
                return LeafTerm.Writer(step.Symbol, mode: step.Mode);
            }
        }
        else
        {
            // Constant - pass structural mode for wildcard checking (spec v0.6)
            var value = step.Symbol;

            // Check for integer first (more specific - no decimal point)
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
            {
                return LeafTerm.IntegerConstant(intVal, mode: step.Mode);
            }

            // Fix 4.2: Check for real (floating-point) - includes scientific notation
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleVal))
            {
                return LeafTerm.RealConstant(doubleVal, mode: step.Mode);
            }

            // Check for string (quoted)
            if ((value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))
                || (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)))
            {
                return LeafTerm.StringConstant(value.Substring(1, value.Length - 2), mode: step.Mode);
            }

            // Fix Bug 1: Atoms (unquoted identifiers) are strings per paper Section 4.1
            // Paper line 49: "String ::= ... ; a ; b ; ... ; foo ; bar ; ..."
            // Therefore atoms like 'user', 'net', 'foo' etc. are strings
            return LeafTerm.StringConstant(value, mode: step.Mode);
        }
    }

    /// <summary>Get variable key for the variable types map.</summary>
    private static string VariableKey(PathStep leaf) => leaf.Symbol;

    /// <summary>
    /// Check duality of variable pairs.
    /// Per spec v0.4: uses DFAState.BaseName and IsDual.
    /// </summary>
    private static List<NonDualError> CheckDuality(IReadOnlyDictionary<string, VariableTypeInfo> variableTypes)
    {
        var errors = new List<NonDualError>();

        // Group by base name (X and X? share base "X")
        var baseNames = new Dictionary<string, Dictionary<string, VariableTypeInfo>>(StringComparer.Ordinal);

        foreach (var entry in variableTypes)
        {
            var varKey = entry.Key;
            var info = entry.Value;

            var baseName = varKey.EndsWith("?", StringComparison.Ordinal)
                ? varKey.Substring(0, varKey.Length - 1)
                : varKey;

            if (!baseNames.TryGetValue(baseName, out var variants))
            {
                variants = new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal);
                baseNames[baseName] = variants;
            }
            variants[varKey] = info;
        }

        // Check each base name
        foreach (var entry in baseNames)
        {
            var baseName = entry.Key;
            var variants = entry.Value;

            var writerKey = baseName;
            var readerKey = $"{baseName}?";

            if (variants.ContainsKey(writerKey) && variants.ContainsKey(readerKey))
            {
                var writerInfo = variants[writerKey];
                var readerInfo = variants[readerKey];

                // Check modes
                if (writerInfo.Mode != ModeAliases.Produce)
                {
                    errors.Add(new NonDualError(baseName, writerInfo, readerInfo,
                        "Writer must have produce mode"));
                    continue;
                }
                if (readerInfo.Mode != ModeAliases.Consume)
                {
                    errors.Add(new NonDualError(baseName, writerInfo, readerInfo,
                        "Reader must have consume mode"));
                    continue;
                }

                // Special case: wildcards are universal
                var writerIsWildcard = writerInfo.TypeState.BaseName == "_";
                var readerIsWildcard = readerInfo.TypeState.BaseName == "_";

                if (writerIsWildcard || readerIsWildcard)
                {
                    // Wildcards are dual to anything
                    if (writerIsWildcard && writerInfo.TypeState.IsDual)
                    {
                        errors.Add(new NonDualError(baseName, writerInfo, readerInfo,
                            "Writer wildcard must be _ (non-dual), not _?"));
                        continue;
                    }
                    if (readerIsWildcard && !readerInfo.TypeState.IsDual)
                    {
                        errors.Add(new NonDualError(baseName, writerInfo, readerInfo,
                            "Reader wildcard must be _? (dual), not _"));
                        continue;
                    }
                    // Wildcards are OK - no error
                    continue;
                }

                // Check states are duals (same baseName, opposite isDual)
                if (writerInfo.TypeState.BaseName != readerInfo.TypeState.BaseName)
                {
                    errors.Add(new NonDualError(baseName, writerInfo, readerInfo,
                        $"Types must have same base: {writerInfo.TypeState.Name} vs {readerInfo.TypeState.Name}"));
                    continue;
                }

                if (writerInfo.TypeState.IsDual == readerInfo.TypeState.IsDual)
                {
                    errors.Add(new NonDualError(baseName, writerInfo, readerInfo,
                        $"One must be dual, other not: {writerInfo.TypeState.Name} vs {readerInfo.TypeState.Name}"));
                }
            }
        }

        return errors;
    }
}
