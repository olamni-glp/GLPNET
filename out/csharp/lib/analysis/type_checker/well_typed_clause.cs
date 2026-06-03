// lib/analysis/type_checker/well_typed_clause.cs
//
// Well-typed clause checking for GLP type system.
// Specification: docs/type system/well-typed-clause.md v0.9
// Paper Reference: Definition 5.7 (Well-Typed Clause)
//
// A clause H :- G | B is well-typed if:
// 1. The moded head H is well-typed by the procedure's type
// 2. Each body atom is well-typed by its procedure's type
// 3. Variable pairs (X, X?) have:
//    - dual types if both in head or both in body
//    - same type if one in head and one in body

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Clause = GlpRuntime.Compiler.Clause;
using Goal = GlpRuntime.Compiler.Goal;
using Term = GlpRuntime.Compiler.Term;
using VarTerm = GlpRuntime.Compiler.VarTerm;
using StructTerm = GlpRuntime.Compiler.StructTerm;
using ListTerm = GlpRuntime.Compiler.ListTerm;
using ConstTerm = GlpRuntime.Compiler.ConstTerm;
using UnderscoreTerm = GlpRuntime.Compiler.UnderscoreTerm;
using SpawnGoal = GlpRuntime.Compiler.SpawnGoal;
using RemoteGoal = GlpRuntime.Compiler.RemoteGoal;

namespace GlpRuntime.Analysis.TypeChecker;

// =============================================================================
// Result Types
// =============================================================================

/// <summary>Result of checking if a clause is well-typed.</summary>
public sealed class ClauseCheckResult
{
    /// <summary>Whether the clause is well-typed.</summary>
    public bool IsWellTyped { get; }

    /// <summary>All variable type assignments from head and body.</summary>
    public IReadOnlyDictionary<string, VariableTypeInfo> VariableTypes { get; }

    /// <summary>List of errors found during checking.</summary>
    public IReadOnlyList<ClauseError> Errors { get; }

    /// <summary>The constructed moded head term (if available).</summary>
    public ModedTerm? ModedHead { get; }

    /// <summary>The constructed moded body atom terms.</summary>
    public IReadOnlyList<ModedTerm> ModedBodyAtoms { get; }

    public ClauseCheckResult(
        bool isWellTyped,
        IReadOnlyDictionary<string, VariableTypeInfo> variableTypes,
        IReadOnlyList<ClauseError> errors,
        ModedTerm? modedHead = null,
        IReadOnlyList<ModedTerm>? modedBodyAtoms = null)
    {
        IsWellTyped = isWellTyped;
        VariableTypes = variableTypes;
        Errors = errors;
        ModedHead = modedHead;
        ModedBodyAtoms = modedBodyAtoms ?? Array.Empty<ModedTerm>();
    }

    public static ClauseCheckResult Success(
        IReadOnlyDictionary<string, VariableTypeInfo> variableTypes,
        ModedTerm? modedHead = null,
        IReadOnlyList<ModedTerm>? modedBodyAtoms = null)
        => new ClauseCheckResult(
            isWellTyped: true,
            variableTypes: variableTypes,
            errors: Array.Empty<ClauseError>(),
            modedHead: modedHead,
            modedBodyAtoms: modedBodyAtoms ?? Array.Empty<ModedTerm>());

    public static ClauseCheckResult Failure(
        IReadOnlyList<ClauseError> errors,
        IReadOnlyDictionary<string, VariableTypeInfo>? variableTypes = null,
        ModedTerm? modedHead = null,
        IReadOnlyList<ModedTerm>? modedBodyAtoms = null)
        => new ClauseCheckResult(
            isWellTyped: false,
            variableTypes: variableTypes ?? new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal),
            errors: errors,
            modedHead: modedHead,
            modedBodyAtoms: modedBodyAtoms ?? Array.Empty<ModedTerm>());
}

/// <summary>Base class for clause checking errors.</summary>
public abstract class ClauseError
{
    public abstract string Message { get; }
}

/// <summary>Error in head checking.</summary>
public sealed class HeadError : ClauseError
{
    public string ProcedureName { get; }
    public IReadOnlyList<WellTypedError> TermErrors { get; }

    public HeadError(string procedureName, IReadOnlyList<WellTypedError> termErrors)
    {
        ProcedureName = procedureName;
        TermErrors = termErrors;
    }

    public override string Message
        => $"Head of {ProcedureName} is not well-typed:\n  {string.Join("\n  ", TermErrors.Select(e => e.Message))}";

    public override string ToString() => Message;
}

/// <summary>Error in body atom checking.</summary>
public sealed class BodyAtomError : ClauseError
{
    public string ProcedureName { get; }
    public int AtomIndex { get; }
    public IReadOnlyList<WellTypedError> TermErrors { get; }

    public BodyAtomError(string procedureName, int atomIndex, IReadOnlyList<WellTypedError> termErrors)
    {
        ProcedureName = procedureName;
        AtomIndex = atomIndex;
        TermErrors = termErrors;
    }

    public override string Message
        => $"Body atom {AtomIndex.ToString(CultureInfo.InvariantCulture)} ({ProcedureName}) is not well-typed:\n  {string.Join("\n  ", TermErrors.Select(e => e.Message))}";

    public override string ToString() => Message;
}

/// <summary>Error: variable pair not dual across clause.</summary>
public sealed class ClauseDualityError : ClauseError
{
    public string BaseName { get; }
    public VariableTypeInfo? WriterType { get; }
    public VariableTypeInfo? ReaderType { get; }
    public string WriterLocation { get; }
    public string ReaderLocation { get; }
    public string? Reason { get; }

    public ClauseDualityError(
        string baseName,
        VariableTypeInfo? writerType,
        VariableTypeInfo? readerType,
        string writerLocation,
        string readerLocation,
        string? reason = null)
    {
        BaseName = baseName;
        WriterType = writerType;
        ReaderType = readerType;
        WriterLocation = writerLocation;
        ReaderLocation = readerLocation;
        Reason = reason;
    }

    public override string Message
    {
        get
        {
            var reasonStr = Reason != null ? $": {Reason}" : "";
            return $"Variable pair ({BaseName}, {BaseName}?) not dual across clause{reasonStr}: " +
                   $"writer at {WriterLocation}={WriterType?.ToString() ?? "null"}, reader at {ReaderLocation}={ReaderType?.ToString() ?? "null"}";
        }
    }

    public override string ToString() => Message;
}

/// <summary>Error: undefined procedure.</summary>
public sealed class UndefinedProcedureError : ClauseError
{
    public string ProcedureName { get; }
    public int Arity { get; }

    public UndefinedProcedureError(string procedureName, int arity)
    {
        ProcedureName = procedureName;
        Arity = arity;
    }

    public override string Message
        => $"Undefined procedure: {ProcedureName}/{Arity.ToString(CultureInfo.InvariantCulture)}";

    public override string ToString() => Message;
}

/// <summary>Error: arity mismatch.</summary>
public sealed class ArityMismatchClauseError : ClauseError
{
    public string ProcedureName { get; }
    public int ExpectedArity { get; }
    public int ActualArity { get; }

    public ArityMismatchClauseError(string procedureName, int expectedArity, int actualArity)
    {
        ProcedureName = procedureName;
        ExpectedArity = expectedArity;
        ActualArity = actualArity;
    }

    public override string Message
        => $"Arity mismatch for {ProcedureName}: expected {ExpectedArity.ToString(CultureInfo.InvariantCulture)}, got {ActualArity.ToString(CultureInfo.InvariantCulture)}";

    public override string ToString() => Message;
}

/// <summary>Exception thrown when a procedure is not declared.</summary>
public sealed class UndeclaredProcedureError : Exception
{
    public string Functor { get; }
    public int Arity { get; }

    public UndeclaredProcedureError(string functor, int arity)
        : base($"UndeclaredProcedureError: {functor}/{arity.ToString(CultureInfo.InvariantCulture)}")
    {
        Functor = functor;
        Arity = arity;
    }

    public override string ToString()
        => $"UndeclaredProcedureError: {Functor}/{Arity.ToString(CultureInfo.InvariantCulture)}";
}

// =============================================================================
// Clause Representation
// =============================================================================

/// <summary>A parsed clause structure for type checking.</summary>
public sealed class TypedClause
{
    public Goal Head { get; }
    public IReadOnlyList<Goal> BodyAtoms { get; }
    public IReadOnlyList<Goal> GuardAtoms { get; }

    public TypedClause(
        Goal head,
        IReadOnlyList<Goal>? bodyAtoms = null,
        IReadOnlyList<Goal>? guardAtoms = null)
    {
        Head = head;
        BodyAtoms = bodyAtoms ?? Array.Empty<Goal>();
        GuardAtoms = guardAtoms ?? Array.Empty<Goal>();
    }

    public string HeadFunctor => Head.Functor;
    public int HeadArity => Head.Arity;
}

// =============================================================================
// Host Static Class
// =============================================================================

/// <summary>
/// Host static class for well-typed clause checking functions.
/// </summary>
public static class WellTypedClause
{
    // Shared empty dictionary used in success returns for body atoms.
    private static readonly IReadOnlyDictionary<string, VariableTypeInfo> EmptyDict =
        new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal);

    // =============================================================================
    // Public Functions
    // =============================================================================

    /// <summary>
    /// Check if a clause is well-typed in the given environment.
    ///
    /// Per Definition 4.8: A clause H :- G | B is well-typed if:
    /// 1. modedHead(H, procType) is well-typed by the procedure's type
    /// 2. For each body atom A, producedTerm(A, atomType) is well-typed
    /// 3. All variable pairs (X, X?) across head and body are complementary
    /// </summary>
    public static ClauseCheckResult CheckClause(TypedClause clause, ProgramDFA dfa, TypeEnvironment env)
    {
        var errors = new List<ClauseError>();
        var allVariableTypes = new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal);
        var variableLocations = new Dictionary<string, string>(StringComparer.Ordinal);
        ModedTerm? constructedModedHead = null;
        var constructedModedBodyAtoms = new List<ModedTerm>();

        // Look up procedure declaration for head
        var procDecl = env.GetProcedure(clause.HeadFunctor, clause.HeadArity);
        if (procDecl == null)
        {
            return ClauseCheckResult.Failure(new ClauseError[]
            {
                new UndefinedProcedureError(clause.HeadFunctor, clause.HeadArity)
            });
        }

        // Check arity match
        if (procDecl.Arity != clause.HeadArity)
        {
            return ClauseCheckResult.Failure(new ClauseError[]
            {
                new ArityMismatchClauseError(clause.HeadFunctor, procDecl.Arity, clause.HeadArity)
            });
        }

        // Step 1: Check head well-typing
        var (headResult, modedHeadTerm) = CheckHeadWithTerm(clause, procDecl, dfa, env);
        constructedModedHead = modedHeadTerm;
        if (!headResult.IsWellTyped)
        {
            errors.Add(new HeadError(clause.HeadFunctor, headResult.Errors));
        }
        foreach (var entry in headResult.VariableTypes)
        {
            allVariableTypes[entry.Key] = entry.Value;
            variableLocations[entry.Key] = "head";
        }

        // Step 2: Check each body atom
        for (int i = 0; i < clause.BodyAtoms.Count; i++)
        {
            var atom = clause.BodyAtoms[i];
            var (atomResult, modedAtomTerm) = CheckBodyAtomWithTerm(atom, i, dfa, env,
                callerVarTypes: allVariableTypes);

            if (modedAtomTerm != null)
            {
                constructedModedBodyAtoms.Add(modedAtomTerm);
            }

            if (!atomResult.IsWellTyped)
            {
                errors.Add(new BodyAtomError(atom.Functor, i, atomResult.Errors));
            }

            // Merge variable types with consistency checking
            foreach (var entry in atomResult.VariableTypes)
            {
                var varKey = entry.Key;
                var newInfo = entry.Value;

                if (allVariableTypes.ContainsKey(varKey))
                {
                    var existing = allVariableTypes[varKey];
                    // Same variable at different positions - types must match
                    if (existing.TypeState.Name != newInfo.TypeState.Name)
                    {
                        // This will be caught by complementarity check below
                    }
                }
                else
                {
                    allVariableTypes[varKey] = newInfo;
                    variableLocations[varKey] = $"body atom {i.ToString(CultureInfo.InvariantCulture)}";
                }
            }
        }

        // Step 3: Check variable pair duality across clause
        var dualityErrors = CheckClauseDuality(allVariableTypes, variableLocations, dfa);
        errors.AddRange(dualityErrors);

        return new ClauseCheckResult(
            isWellTyped: errors.Count == 0,
            variableTypes: allVariableTypes,
            errors: errors,
            modedHead: constructedModedHead,
            modedBodyAtoms: constructedModedBodyAtoms);
    }

    /// <summary>
    /// Convenience overload: Check if an ast.Clause is well-typed.
    ///
    /// Throws <see cref="UndeclaredProcedureError"/> if the procedure is not declared.
    ///
    /// Per spec: For type checking, H :- G | B is treated as H :- G, B (conjunction).
    /// Guards are procedure calls with predefined type signatures.
    /// </summary>
    public static ClauseCheckResult CheckClauseFromAst(Clause clause, ProgramDFA dfa, TypeEnvironment env)
    {
        // Convert ast.Clause to TypedClause
        var head = new Goal(clause.Head.Functor, clause.Head.Args, clause.Line, clause.Column);

        // Convert guards to goals
        var guardGoals = new List<Goal>();
        if (clause.Guards != null)
        {
            foreach (var guard in clause.Guards)
            {
                guardGoals.Add(new Goal(guard.Predicate, guard.Args, guard.Line, guard.Column));
            }
        }

        // Convert body goals (or empty list)
        var bodyGoals = clause.Body ?? Array.Empty<Goal>();

        // Combine guards and body: H :- G | B is treated as H :- G, B
        IReadOnlyList<Goal> allBodyAtoms = [..guardGoals, ..bodyGoals];

        var typedClause = new TypedClause(
            head: head,
            bodyAtoms: allBodyAtoms,
            guardAtoms: guardGoals);

        // Check if procedure is declared
        if (!env.HasProcedure(typedClause.HeadFunctor, typedClause.HeadArity))
        {
            throw new UndeclaredProcedureError(typedClause.HeadFunctor, typedClause.HeadArity);
        }

        return CheckClause(typedClause, dfa, env);
    }

    /// <summary>
    /// Get the set of labels (functor/arity or constant) that a clause accepts
    /// at a given argument position (1-indexed).
    ///
    /// Returns null if the argument is a variable (wildcard - accepts everything).
    /// Returns a set of strings like "[]", "[|]", "s/1", "0" etc.
    /// </summary>
    public static IReadOnlySet<string>? GetAcceptedLabels(Clause clause, int argIndex, TypeEnvironment env)
    {
        // argIndex is 1-indexed
        if (argIndex < 1 || argIndex > clause.Head.Args.Count)
        {
            return new HashSet<string>(StringComparer.Ordinal); // Out of bounds - accepts nothing
        }

        var arg = clause.Head.Args[argIndex - 1];
        return GetLabelsFromTerm(arg);
    }

    /// <summary>Extract labels from a term (public for coverage checking).</summary>
    public static IReadOnlySet<string>? GetLabelsFromTerm(Term term)
    {
        return term switch
        {
            VarTerm or UnderscoreTerm => null,
            ConstTerm c => new HashSet<string>(StringComparer.Ordinal) { c.Value?.ToString() ?? "" },
            ListTerm l => new HashSet<string>(StringComparer.Ordinal) { l.IsNil ? "[]" : "[|]" },
            StructTerm s => new HashSet<string>(StringComparer.Ordinal) { $"{s.Functor}/{s.Arity.ToString(CultureInfo.InvariantCulture)}" },
            _ => new HashSet<string>(StringComparer.Ordinal)
        };
    }

    // =============================================================================
    // Helper Functions
    // =============================================================================

    /// <summary>Get the full type name including ? if input mode.</summary>
    public static string GetFullTypeName(TypeExpr typeExpr)
    {
        return typeExpr switch
        {
            PrimitiveModeAlt p => p.IsInput ? "_?" : "_",
            TypeRef r => r.IsInput ? $"{r.Name}?" : r.Name,
            _ => throw new ArgumentException($"Unknown type expression: {typeExpr}")
        };
    }

    // =============================================================================
    // Internal Functions
    // =============================================================================

    /// <summary>Check head well-typing by checking each argument against its declared type's automaton.</summary>
    private static WellTypedResult CheckHead(TypedClause clause, ProcDecl procDecl, ProgramDFA dfa, TypeEnvironment env)
    {
        var (result, _) = CheckHeadWithTerm(clause, procDecl, dfa, env);
        return result;
    }

    /// <summary>Check head well-typing and return the constructed moded term.</summary>
    private static (WellTypedResult result, ModedTerm? term) CheckHeadWithTerm(
        TypedClause clause, ProcDecl procDecl, ProgramDFA dfa, TypeEnvironment env)
    {
        try
        {
            var modedHeadTerm = ModedHead.Build(clause.Head, procDecl, typeEnv: env);
            var result = CheckModedTermPerArg(modedHeadTerm, procDecl, dfa);
            return (result, modedHeadTerm);
        }
        catch (ArityMismatchError e)
        {
            return (WellTypedResult.Failure(new WellTypedError[]
            {
                new InconsistentPathError(
                    new ModedPath(new List<PathStep>
                    {
                        new PathStep(symbol: e.Message, argIndex: 0, mode: ModeAliases.Produce)
                    }),
                    e.Message)
            }), null);
        }
    }

    /// <summary>Check body atom well-typing.</summary>
    private static WellTypedResult CheckBodyAtom(
        Goal atom, int atomIndex, ProgramDFA dfa, TypeEnvironment env,
        IReadOnlyDictionary<string, VariableTypeInfo>? callerVarTypes = null)
    {
        var (result, _) = CheckBodyAtomWithTerm(atom, atomIndex, dfa, env, callerVarTypes: callerVarTypes);
        return result;
    }

    /// <summary>Check body atom well-typing and return the constructed moded term.</summary>
    private static (WellTypedResult result, ModedTerm? term) CheckBodyAtomWithTerm(
        Goal atom, int atomIndex, ProgramDFA dfa, TypeEnvironment env,
        IReadOnlyDictionary<string, VariableTypeInfo>? callerVarTypes = null)
    {
        // Handle SpawnGoal (Goal@Agent) - type-check the inner goal
        if (atom is SpawnGoal spawn)
        {
            return CheckBodyAtomWithTerm(spawn.InnerGoal, atomIndex, dfa, env, callerVarTypes: callerVarTypes);
        }

        // Handle RemoteGoal (M # proc(...)) - type-check against imported declaration
        if (atom is RemoteGoal remote)
        {
            return CheckRemoteGoal(remote, atomIndex, dfa, env);
        }

        // Skip builtin goals (true, otherwise, :=)
        if (Prelude.IsBuiltinGoal(atom.Functor))
        {
            return (WellTypedResult.Success(EmptyDict), null);
        }

        // Look up procedure declaration
        var procDecl = env.GetProcedure(atom.Functor, atom.Arity);
        if (procDecl == null)
        {
            return (WellTypedResult.Failure(new WellTypedError[]
            {
                new InconsistentPathError(
                    new ModedPath(new List<PathStep>
                    {
                        new PathStep(
                            symbol: $"{atom.Functor}/{atom.Arity.ToString(CultureInfo.InvariantCulture)}",
                            argIndex: 0,
                            mode: ModeAliases.Produce)
                    }),
                    $"Undefined procedure: {atom.Functor}/{atom.Arity.ToString(CultureInfo.InvariantCulture)}")
            }), null);
        }

        // Case B: Call-site instantiation for parameterized procedures.
        var paramTemplate = env.ParamProcDecls.GetValueOrDefault(procDecl.Key);
        if (paramTemplate != null)
        {
            if (callerVarTypes != null && callerVarTypes.Count > 0)
            {
                var inferredDecl = InferConcreteDecl(paramTemplate, atom, callerVarTypes, dfa, env);
                if (inferredDecl != null)
                {
                    procDecl = inferredDecl;
                }
                else
                {
                    return (WellTypedResult.Success(EmptyDict), null);
                }
            }
            else
            {
                return (WellTypedResult.Success(EmptyDict), null);
            }
        }

        // Build produced term (no variable flip for body atoms)
        try
        {
            var modedAtomTerm = ModedHead.ProducedTerm(atom, procDecl, typeEnv: env);
            var result = CheckModedTermPerArg(modedAtomTerm, procDecl, dfa);
            return (result, modedAtomTerm);
        }
        catch (ArityMismatchError e)
        {
            return (WellTypedResult.Failure(new WellTypedError[]
            {
                new InconsistentPathError(
                    new ModedPath(new List<PathStep>
                    {
                        new PathStep(symbol: e.Message, argIndex: 0, mode: ModeAliases.Produce)
                    }),
                    e.Message)
            }), null);
        }
    }

    /// <summary>
    /// Check a remote goal (M # proc(...)) against the imported procedure declaration.
    ///
    /// Per spec Section 5.1: type checking is local — we look up the imported
    /// declaration in the local TypeEnvironment, not the remote module.
    ///
    /// Dynamic dispatch (variable module) is skipped — can't resolve at compile time.
    /// </summary>
    private static (WellTypedResult result, ModedTerm? term) CheckRemoteGoal(
        RemoteGoal remote, int atomIndex, ProgramDFA dfa, TypeEnvironment env)
    {
        // Dynamic dispatch (variable module) — skip type checking
        if (remote.IsDynamic)
        {
            return (WellTypedResult.Success(EmptyDict), null);
        }

        // Flatten nested RemoteGoals to extract full module path and actual goal.
        var pathParts = new List<string>();
        Goal innerGoal = remote;
        while (innerGoal is RemoteGoal rg)
        {
            if (rg.IsDynamic)
            {
                return (WellTypedResult.Success(EmptyDict), null);
            }
            pathParts.Add(rg.StaticModuleName!);
            innerGoal = rg.Goal;
        }
        var modulePath = string.Join("#", pathParts);
        var goalFunctor = innerGoal.Functor;
        var goalArity = innerGoal.Arity;

        // Look up: 'modulePath#goalFunctor/arity'
        var qualifiedKey = $"{modulePath}#{goalFunctor}/{goalArity.ToString(CultureInfo.InvariantCulture)}";
        var procDecl = env.Procedures.GetValueOrDefault(qualifiedKey);

        if (procDecl == null)
        {
            return (WellTypedResult.Failure(new WellTypedError[]
            {
                new InconsistentPathError(
                    new ModedPath(new List<PathStep>
                    {
                        new PathStep(symbol: qualifiedKey, argIndex: 0, mode: ModeAliases.Produce)
                    }),
                    $"No imported declaration for {modulePath}#{goalFunctor}/{goalArity.ToString(CultureInfo.InvariantCulture)} — " +
                    $"add \"imported procedure {modulePath}#{goalFunctor}(...)\" to this module")
            }), null);
        }

        // Type-check the inner goal's arguments against the imported declaration
        try
        {
            var modedAtomTerm = ModedHead.ProducedTerm(innerGoal, procDecl, typeEnv: env);
            var result = CheckModedTermPerArg(modedAtomTerm, procDecl, dfa);
            return (result, modedAtomTerm);
        }
        catch (ArityMismatchError e)
        {
            return (WellTypedResult.Failure(new WellTypedError[]
            {
                new InconsistentPathError(
                    new ModedPath(new List<PathStep>
                    {
                        new PathStep(symbol: e.Message, argIndex: 0, mode: ModeAliases.Produce)
                    }),
                    e.Message)
            }), null);
        }
    }

    /// <summary>Check moded term per argument against declared type automata.</summary>
    private static WellTypedResult CheckModedTermPerArg(ModedTerm modedTerm, ProcDecl decl, ProgramDFA dfa)
    {
        var errors = new List<WellTypedError>();
        var variableTypes = new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal);

        // modedTerm should be a ModedCompound with args
        if (modedTerm is not ModedCompound compound)
        {
            return WellTypedResult.Failure(new WellTypedError[]
            {
                new InconsistentPathError(
                    new ModedPath(new List<PathStep>
                    {
                        new PathStep(symbol: "not-compound", argIndex: 0, mode: ModeAliases.Produce)
                    }),
                    "Expected compound term for procedure")
            });
        }

        // Check each argument
        for (int i = 0; i < decl.Arity; i++)
        {
            var argType = decl.ArgTypes[i];

            // Get the automaton for the declared type directly
            var argTypeName = GetFullTypeName(argType);

            Automaton argAutomaton;
            try
            {
                argAutomaton = dfa.LookupAutomaton(argTypeName);
            }
            catch (InvalidOperationException)
            {
                errors.Add(new InconsistentPathError(
                    new ModedPath(new List<PathStep>
                    {
                        new PathStep(symbol: argTypeName, argIndex: i + 1, mode: ModeAliases.Produce)
                    }),
                    $"Unknown type: {argTypeName}"));
                continue;
            }

            // Extract paths from this argument and check against automaton
            var argTerm = compound.Args[i];
            var argPaths = ModedTermOps.Paths(argTerm);

            foreach (var path in argPaths)
            {
                var result = WellTypedTerm.CheckPathAgainstAutomaton(path, argAutomaton, dfa);

                if (!result.IsConsistent)
                {
                    errors.Add(new InconsistentPathError(path, result.Reason ?? "Unknown"));
                }
                else if (result.VariableAssignment != null)
                {
                    var varKey = path.Leaf.Symbol;
                    if (variableTypes.ContainsKey(varKey))
                    {
                        var existing = variableTypes[varKey];
                        var assignment = result.VariableAssignment!;
                        if (existing.TypeState.Name != assignment.TypeState.Name)
                        {
                            errors.Add(new InconsistentVariableError(varKey, existing, assignment));
                        }
                    }
                    else
                    {
                        variableTypes[varKey] = result.VariableAssignment!;
                    }
                }
            }
        }

        // Check duality within this term
        var dualityErrors = CheckTermDuality(variableTypes);
        errors.AddRange(dualityErrors);

        return new WellTypedResult(
            isWellTyped: errors.Count == 0,
            variableTypes: variableTypes,
            errors: errors);
    }

    /// <summary>Check duality within a term (same logic as WellTypedTerm.CheckDuality).</summary>
    private static IReadOnlyList<NonDualError> CheckTermDuality(
        IReadOnlyDictionary<string, VariableTypeInfo> variableTypes)
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

                if (!AreDualTypes(writerInfo, readerInfo))
                {
                    errors.Add(new NonDualError(baseName, writerInfo, readerInfo));
                }
            }
        }

        return errors;
    }

    /// <summary>Normalize location to 'head' or 'body'.</summary>
    private static string NormalizeLocation(string location) =>
        location == "head" ? "head" : location.StartsWith("body", StringComparison.Ordinal) ? "body" : location;

    /// <summary>
    /// Check variable pair type consistency across the entire clause.
    ///
    /// Per Definition 4.10 (spec v0.9):
    /// - If both occur in head, or both in body: require DUAL types
    /// - If one in head and one in body: require SAME type
    /// </summary>
    private static IReadOnlyList<ClauseDualityError> CheckClauseDuality(
        IReadOnlyDictionary<string, VariableTypeInfo> variableTypes,
        IReadOnlyDictionary<string, string> variableLocations,
        ProgramDFA dfa)
    {
        var errors = new List<ClauseDualityError>();

        // Group by base name (X and X? share base "X")
        var baseNames = new Dictionary<string, Dictionary<string, VariableTypeInfo>>(StringComparer.Ordinal);
        var baseLocations = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var entry in variableTypes)
        {
            var varKey = entry.Key;
            var info = entry.Value;
            var location = variableLocations.TryGetValue(varKey, out var loc) ? loc : "unknown";

            var baseName = varKey.EndsWith("?", StringComparison.Ordinal)
                ? varKey.Substring(0, varKey.Length - 1)
                : varKey;

            if (!baseNames.TryGetValue(baseName, out var variants))
            {
                variants = new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal);
                baseNames[baseName] = variants;
            }
            variants[varKey] = info;

            if (!baseLocations.TryGetValue(baseName, out var locs))
            {
                locs = new Dictionary<string, string>(StringComparer.Ordinal);
                baseLocations[baseName] = locs;
            }
            locs[varKey] = location;
        }

        // Check each base name
        foreach (var entry in baseNames)
        {
            var baseName = entry.Key;
            var variants = entry.Value;
            var locations = baseLocations[baseName];

            var writerKey = baseName;
            var readerKey = $"{baseName}?";

            if (variants.ContainsKey(writerKey) && variants.ContainsKey(readerKey))
            {
                var writerInfo = variants[writerKey];
                var readerInfo = variants[readerKey];
                var writerLoc = locations.TryGetValue(writerKey, out var wl) ? wl : "unknown";
                var readerLoc = locations.TryGetValue(readerKey, out var rl) ? rl : "unknown";

                // Normalize locations to 'head' or 'body'
                var writerNormLoc = NormalizeLocation(writerLoc);
                var readerNormLoc = NormalizeLocation(readerLoc);

                // Apply location-dependent rule (spec v0.9, Definition 4.10 condition 3)
                if (writerNormLoc == readerNormLoc)
                {
                    if (writerNormLoc == "head")
                    {
                        // Both in head: require exact DUAL types (unchanged)
                        var (isCompat, reason) = AreDualTypesWithReason(writerInfo, readerInfo);
                        if (!isCompat)
                        {
                            errors.Add(new ClauseDualityError(
                                baseName,
                                writerInfo,
                                readerInfo,
                                writerLoc,
                                readerLoc,
                                $"Variables in same clause part (head) must have dual types: {reason}"));
                        }
                    }
                    else
                    {
                        // Both in body: require subtyping S <: T (Definition 4.8)
                        var writerOutputState = writerInfo.TypeState;
                        var readerDualState = readerInfo.TypeState;
                        var readerOutputState = dfa.LookupState(readerDualState.BaseName);
                        var isSub = Subtyping.IsSubtype(writerOutputState, readerOutputState, dfa);
                        if (!isSub)
                        {
                            errors.Add(new ClauseDualityError(
                                baseName,
                                writerInfo,
                                readerInfo,
                                writerLoc,
                                readerLoc,
                                $"Body variable pair: writer type {writerOutputState.Name} is not a subtype of {readerOutputState.Name}"));
                        }
                    }
                }
                else
                {
                    // One in head, one in body: require SAME type
                    var (isSame, reason) = AreSameTypeWithReason(writerInfo, readerInfo);
                    if (!isSame)
                    {
                        errors.Add(new ClauseDualityError(
                            baseName,
                            writerInfo,
                            readerInfo,
                            writerLoc,
                            readerLoc,
                            $"Variables across head/body must have same type: {reason}"));
                    }
                }
            }
        }

        return errors;
    }

    /// <summary>Check if writer and reader types are dual.</summary>
    private static bool AreDualTypes(VariableTypeInfo writerInfo, VariableTypeInfo readerInfo)
    {
        var (isCompat, _) = AreDualTypesWithReason(writerInfo, readerInfo);
        return isCompat;
    }

    /// <summary>
    /// Check if two variable types are the SAME type.
    /// Per spec v0.9: For head-body pairs, types must have same BASE type.
    /// </summary>
    private static (bool isSame, string? reason) AreSameTypeWithReason(
        VariableTypeInfo writerInfo, VariableTypeInfo readerInfo)
    {
        if (writerInfo.TypeState.BaseName != readerInfo.TypeState.BaseName)
        {
            return (false, $"{writerInfo.TypeState.Name} (base: {writerInfo.TypeState.BaseName}) != {readerInfo.TypeState.Name} (base: {readerInfo.TypeState.BaseName})");
        }
        return (true, null);
    }

    /// <summary>
    /// Check duality with reason for failure.
    /// Per paper Definition 5.6: head-head and body-body pairs must have dual types.
    /// </summary>
    private static (bool isCompat, string? reason) AreDualTypesWithReason(
        VariableTypeInfo writerInfo, VariableTypeInfo readerInfo)
    {
        // Mode check: writer must produce, reader must consume
        if (writerInfo.Mode != ModeAliases.Produce)
        {
            return (false, "Writer must have produce mode");
        }
        if (readerInfo.Mode != ModeAliases.Consume)
        {
            return (false, "Reader must have consume mode");
        }

        // States must be duals: same baseName, opposite isDual
        if (writerInfo.TypeState.BaseName != readerInfo.TypeState.BaseName)
        {
            return (false, $"Types must have same base: {writerInfo.TypeState.Name} vs {readerInfo.TypeState.Name}");
        }

        // One must be dual, the other not
        if (writerInfo.TypeState.IsDual == readerInfo.TypeState.IsDual)
        {
            return (false, $"One must be dual, other not: {writerInfo.TypeState.Name} vs {readerInfo.TypeState.Name}");
        }

        return (true, null);
    }

    // =============================================================================
    // Case B: Call-site instantiation for parameterized procedures
    // =============================================================================

    /// <summary>
    /// Infer a concrete proc decl by matching a parameterized template against
    /// the actual argument types at a call site.
    ///
    /// Returns null if inference fails.
    /// </summary>
    private static ProcDecl? InferConcreteDecl(
        ProcDecl paramTemplate,
        Goal atom,
        IReadOnlyDictionary<string, VariableTypeInfo> callerVarTypes,
        ProgramDFA dfa,
        TypeEnvironment env)
    {
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);

        // For each argument, try to infer type param bindings
        for (int i = 0; i < paramTemplate.Arity && i < atom.Args.Count; i++)
        {
            var declaredType = paramTemplate.ArgTypes[i];
            var actualArg = atom.Args[i];

            // Get the actual variable's type from callerVarTypes
            string? actualTypeName = null;
            if (actualArg is VarTerm v)
            {
                var varKey = v.IsReader ? $"{v.Name}?" : v.Name;
                if (callerVarTypes.TryGetValue(varKey, out var info))
                {
                    actualTypeName = info.TypeState.BaseName;
                }
            }
            if (actualTypeName == null) continue;

            // Match declared type against actual type to extract bindings
            MatchTypeForInference(declaredType, actualTypeName, paramTemplate.TypeParams, bindings);
        }

        // If no bindings found, can't infer — fall back to wildcard version
        if (bindings.Count == 0) return null;

        // Check all type params are bound
        foreach (var tp in paramTemplate.TypeParams)
        {
            if (!bindings.ContainsKey(tp)) return null;
        }

        // Create concrete arg types by substituting bindings
        var concreteArgTypes = new List<TypeExpr>();
        foreach (var argType in paramTemplate.ArgTypes)
        {
            concreteArgTypes.Add(SubstituteTypeParams(argType, bindings));
        }

        // Verify all referenced types exist in the DFA
        foreach (var argType in concreteArgTypes)
        {
            var typeName = GetFullTypeName(argType);
            if (!dfa.Automata.ContainsKey(typeName))
            {
                return null;
            }
        }

        return new ProcDecl(paramTemplate.Name, concreteArgTypes,
            paramTemplate.Line, paramTemplate.Column,
            exported: paramTemplate.Exported,
            imported: paramTemplate.Imported,
            modulePath: paramTemplate.ModulePath);
    }

    /// <summary>
    /// Match a declared type expression against an actual type name to infer
    /// type parameter bindings.
    /// </summary>
    private static void MatchTypeForInference(
        TypeExpr declaredType,
        string actualTypeName,
        IReadOnlyList<string> typeParams,
        IDictionary<string, string> bindings)
    {
        if (declaredType is TypeRef declRef)
        {
            if (declRef.TypeArgs.Count == 0 && typeParams.Contains(declRef.Name))
            {
                // Bare type parameter: X → actualTypeName
                if (!bindings.ContainsKey(declRef.Name)) bindings[declRef.Name] = actualTypeName;
                return;
            }

            if (declRef.TypeArgs.Count > 0)
            {
                // Parameterized type ref: Stream(X) vs Stream<AgentMsg>
                var ltIdx = actualTypeName.IndexOf('<');
                if (ltIdx < 0) return;

                var actualTemplate = actualTypeName.Substring(0, ltIdx);
                if (actualTemplate != declRef.Name) return;

                // Extract actual type args from "Stream<AgentMsg>" format
                var argsStr = actualTypeName.Substring(ltIdx + 1, actualTypeName.Length - ltIdx - 2);
                var actualArgs = SplitTypeArgs(argsStr);

                if (actualArgs.Count != declRef.TypeArgs.Count) return;

                for (int j = 0; j < actualArgs.Count; j++)
                {
                    var declArg = declRef.TypeArgs[j];
                    if (declArg is TypeRef declArgRef && declArgRef.TypeArgs.Count == 0 && typeParams.Contains(declArgRef.Name))
                    {
                        if (!bindings.ContainsKey(declArgRef.Name)) bindings[declArgRef.Name] = actualArgs[j];
                    }
                }
            }
        }
    }

    /// <summary>Split comma-separated type args, respecting nested angle brackets.</summary>
    private static List<string> SplitTypeArgs(string s)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '<') depth++;
            if (s[i] == '>') depth--;
            if (s[i] == ',' && depth == 0)
            {
                result.Add(s.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        if (start < s.Length)
        {
            result.Add(s.Substring(start).Trim());
        }
        return result;
    }

    /// <summary>Substitute type parameter names in a TypeExpr with concrete type names.</summary>
    private static TypeExpr SubstituteTypeParams(TypeExpr expr, IReadOnlyDictionary<string, string> bindings)
    {
        if (expr is TypeRef r)
        {
            if (r.TypeArgs.Count == 0 && bindings.ContainsKey(r.Name))
            {
                // Bare type param → concrete type name
                return new TypeRef(bindings[r.Name], r.Line, r.Column, isInput: r.IsInput);
            }
            if (r.TypeArgs.Count > 0)
            {
                // Parameterized ref: substitute args and create expanded name
                var newArgs = r.TypeArgs.Select(a => SubstituteTypeParams(a, bindings)).ToList();
                // Check if all args are now concrete (no more type params)
                var allConcrete = newArgs.All(a =>
                    a is TypeRef tr && tr.TypeArgs.Count == 0 && !bindings.ContainsKey(tr.Name));
                if (allConcrete)
                {
                    // Create expanded name: Stream<AgentMsg>
                    var expandedName = $"{r.Name}<{string.Join(",", newArgs.Cast<TypeRef>().Select(a => a.Name))}>";
                    return new TypeRef(expandedName, r.Line, r.Column, isInput: r.IsInput);
                }
                return new TypeRef(r.Name, r.Line, r.Column, isInput: r.IsInput, typeArgs: newArgs);
            }
            return expr;
        }
        if (expr is PrimitiveModeAlt) return expr;
        return expr;
    }
}
