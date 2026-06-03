// lib/compiler/ast.cs
//
// Abstract Syntax Tree nodes for GLP.
// Converted from Dart source: lib/compiler/ast.dart
// source_sha256: a8a6493e11d47ec727c829d4d595f3b77f27ae5d7f95122c26a0090b3dec81d6

using GlpRuntime.Analysis.TypeChecker;

namespace GlpRuntime.Compiler;

// ============================================================================
// T1 — CompileMode enum
// ============================================================================

/// <summary>Compilation mode: controls compiler restrictions.</summary>
public enum CompileMode
{
    /// <summary>User mode (default): underscore-prefixed constants are rejected.</summary>
    User,
    /// <summary>System mode: underscore-prefixed constants are allowed.</summary>
    System,
}

// ============================================================================
// T2 — AstNode abstract base
// ============================================================================

// Base class for all AST nodes
public abstract class AstNode
{
    /// <summary>Line number in source.</summary>
    public int Line { get; }

    /// <summary>Column number in source.</summary>
    public int Column { get; }

    protected AstNode(int line, int column)
    {
        Line   = line;
        Column = column;
    }
}

// ============================================================================
// T3 — Term abstract intermediate base
// ============================================================================

// Terms (expressions)
public abstract class Term : AstNode
{
    protected Term(int line, int column) : base(line, column) { }
}

// ============================================================================
// T4 — Goal concrete (non-abstract, non-sealed) base
// ============================================================================

// Goal: predicate call in clause body
public class Goal : AstNode
{
    public string Functor { get; }
    public IReadOnlyList<Term> Args { get; }

    public Goal(string functor, IReadOnlyList<Term> args, int line, int column)
        : base(line, column)
    {
        Functor = functor;
        Args    = args;
    }

    public int Arity => Args.Count;

    public override string ToString() => $"{Functor}({string.Join(", ", Args)})";
}

// ============================================================================
// T5 — Program sealed leaf
// ============================================================================

// Top-level program
public sealed class Program : AstNode
{
    public IReadOnlyList<Procedure> Procedures { get; }

    public Program(IReadOnlyList<Procedure> procedures, int line, int column)
        : base(line, column)
    {
        Procedures = procedures;
    }

    public override string ToString() => $"Program({Procedures.Count} procedures)";
}

// ============================================================================
// T6 — Procedure sealed leaf
// ============================================================================

// Procedure: all clauses with same functor/arity
public sealed class Procedure : AstNode
{
    public string Name { get; }
    public int Arity { get; }
    public IReadOnlyList<Clause> Clauses { get; }

    public Procedure(string name, int arity, IReadOnlyList<Clause> clauses, int line, int column)
        : base(line, column)
    {
        Name    = name;
        Arity   = arity;
        Clauses = clauses;
    }

    public string Signature => $"{Name}/{Arity}";

    public override string ToString() => $"Procedure({Signature}, {Clauses.Count} clauses)";
}

// ============================================================================
// T7 — Clause sealed leaf
// ============================================================================

// Clause: Head :- Guards | Body.
public sealed class Clause : AstNode
{
    public Atom Head { get; }
    // Optional guard list before |
    public IReadOnlyList<Guard>? Guards { get; }
    // Optional body goals after |
    public IReadOnlyList<Goal>? Body { get; }

    public Clause(Atom head, IReadOnlyList<Guard>? guards, IReadOnlyList<Goal>? body, int line, int column)
        : base(line, column)
    {
        Head   = head;
        Guards = guards;
        Body   = body;
    }

    public override string ToString()
    {
        var guardsStr = Guards is { Count: > 0 } ? $" :- {string.Join(", ", Guards)}" : "";
        var bodyStr   = Body   is { Count: > 0 } ? $" | {string.Join(", ", Body)}"   : "";
        return $"Clause({Head}{guardsStr}{bodyStr})";
    }
}

// ============================================================================
// T8 — Atom sealed leaf
// ============================================================================

// Atom: predicate in clause head
public sealed class Atom : AstNode
{
    public string Functor { get; }
    public IReadOnlyList<Term> Args { get; }

    public Atom(string functor, IReadOnlyList<Term> args, int line, int column)
        : base(line, column)
    {
        Functor = functor;
        Args    = args;
    }

    public int Arity => Args.Count;

    public override string ToString() => $"{Functor}({string.Join(", ", Args)})";
}

// ============================================================================
// T9 — Guard sealed leaf
// ============================================================================

// Guard: pure test in guard section
public sealed class Guard : AstNode
{
    public string Predicate { get; }
    public IReadOnlyList<Term> Args { get; }
    // true if ~G (guard negation)
    public bool Negated { get; }

    public Guard(string predicate, IReadOnlyList<Term> args, int line, int column, bool negated = false)
        : base(line, column)
    {
        Predicate = predicate;
        Args      = args;
        Negated   = negated;
    }

    public override string ToString() =>
        Negated
            ? $"~{Predicate}({string.Join(", ", Args)})"
            : $"{Predicate}({string.Join(", ", Args)})";
}

// ============================================================================
// T10 — VarTerm sealed leaf
// ============================================================================

public sealed class VarTerm : Term
{
    public string Name { get; }
    // true for X?, false for X
    public bool IsReader { get; }

    public VarTerm(string name, bool isReader, int line, int column)
        : base(line, column)
    {
        Name     = name;
        IsReader = isReader;
    }

    public override string ToString() => IsReader ? $"{Name}?" : Name;
}

// ============================================================================
// T11 — StructTerm sealed leaf
// ============================================================================

public sealed class StructTerm : Term
{
    public string Functor { get; }
    public IReadOnlyList<Term> Args { get; }

    public StructTerm(string functor, IReadOnlyList<Term> args, int line, int column)
        : base(line, column)
    {
        Functor = functor;
        Args    = args;
    }

    public int Arity => Args.Count;

    public override string ToString() => $"{Functor}({string.Join(", ", Args)})";
}

// ============================================================================
// T12 — ListTerm sealed leaf
// ============================================================================

public sealed class ListTerm : Term
{
    // [H|T] -> ListTerm(H, T)
    // []    -> ListTerm(null, null)
    public Term? Head { get; }
    public Term? Tail { get; }

    public ListTerm(Term? head, Term? tail, int line, int column)
        : base(line, column)
    {
        Head = head;
        Tail = tail;
    }

    public bool IsNil => Head is null && Tail is null;

    public override string ToString()
    {
        if (IsNil) return "[]";
        if (Tail is null) return $"[{Head}]";
        return $"[{Head}|{Tail}]";
    }
}

// ============================================================================
// T13 — ConstTerm sealed leaf
// ============================================================================

public sealed class ConstTerm : Term
{
    // String, int, double, or atom name
    public object? Value { get; }

    public ConstTerm(object? value, int line, int column)
        : base(line, column)
    {
        Value = value;
    }

    public override string ToString()
    {
        if (Value is string s)
        {
            // Don't double-quote if already quoted (string literals)
            if ((s.StartsWith("\"", StringComparison.Ordinal) && s.EndsWith("\"", StringComparison.Ordinal))
                || (s.StartsWith("'", StringComparison.Ordinal) && s.EndsWith("'", StringComparison.Ordinal)))
            {
                return s;
            }
            return $"\"{s}\"";
        }
        return Value?.ToString() ?? "null";
    }
}

// ============================================================================
// T14 — UnderscoreTerm sealed leaf
// ============================================================================

public sealed class UnderscoreTerm : Term
{
    // Anonymous variable _ or _?
    // false for _, true for _?
    public bool IsReader { get; }

    public UnderscoreTerm(int line, int column, bool isReader = false)
        : base(line, column)
    {
        IsReader = isReader;
    }

    public override string ToString() => IsReader ? "_?" : "_";
}

// ============================================================================
// Module System AST Nodes
// ============================================================================

/// <summary>Module declaration: -module(name).</summary>
public sealed class ModuleDeclaration : AstNode
{
    public string Name { get; }

    public ModuleDeclaration(string name, int line, int column)
        : base(line, column)
    {
        Name = name;
    }

    public override string ToString() => $"-module({Name}).";
}

// ExportDeclaration, ImportDeclaration, and ProcRef removed in Phase 1.
// Visibility is now declared per-procedure via 'exported procedure'.

/// <summary>
/// Remote goal: Module # Goal.
/// Used for cross-module procedure calls.
/// </summary>
public sealed class RemoteGoal : Goal
{
    // Can be ConstTerm (atom) or VarTerm (variable)
    public Term Module { get; }
    public Goal Goal { get; }

    public RemoteGoal(Term module, Goal goal, int line, int column)
        : base("#", new Term[] { module, GoalToTerm(goal) }, line, column)
    {
        Module = module;
        Goal   = goal;
    }

    /// <summary>Get module name if statically known, null if dynamic (variable).</summary>
    public string? StaticModuleName => Module is ConstTerm ct ? (string?)ct.Value : null;

    /// <summary>Check if module is dynamically resolved (variable).</summary>
    public bool IsDynamic => Module is VarTerm;

    public override string ToString() => $"{Module} # {Goal}";

    /// <summary>Convert a Goal to a StructTerm for storage in args.</summary>
    private static Term GoalToTerm(Goal g) => new StructTerm(g.Functor, g.Args, g.Line, g.Column);
}

/// <summary>
/// Spawn goal: Goal@AgentId.
/// Used for isolate spawning in boot clauses.
/// In dGLP mode: the @AgentId annotation is ignored, goal runs in single isolate.
/// In madGLP mode: the goal is spawned in a separate isolate named AgentId.
/// </summary>
public sealed class SpawnGoal : Goal
{
    public Goal InnerGoal { get; }
    public string AgentId { get; }

    public SpawnGoal(Goal innerGoal, string agentId, int line, int column)
        : base("@", new Term[] { GoalToTerm(innerGoal), new ConstTerm(agentId, line, column) }, line, column)
    {
        InnerGoal = innerGoal;
        AgentId   = agentId;
    }

    public override string ToString() => $"{InnerGoal}@{AgentId}";

    /// <summary>Convert a Goal to a StructTerm for storage in args.</summary>
    private static Term GoalToTerm(Goal g) => new StructTerm(g.Functor, g.Args, g.Line, g.Column);
}

// ============================================================================
// Type Declarations (Yardeni-Shapiro syntax)
// ============================================================================
// Note: Type definitions and procedure declarations use types from
// analysis/type_checker/type_ast.dart (TypeDef, ProcDecl).
// These are imported by the parser and stored in Module.

/// <summary>Complete module structure.</summary>
public sealed class Module : AstNode
{
    public ModuleDeclaration? Declaration { get; }
    // Type definitions: Name ::= alt ; alt.
    public IReadOnlyList<TypeDef> TypeDefs { get; }
    // Procedure declarations (each with exported flag)
    public IReadOnlyList<ProcDecl> ProcDeclarations { get; }
    // Parameterized proc decl templates (for call-site inference)
    public IReadOnlyList<ProcDecl> ParamProcDecls { get; }
    public IReadOnlyList<Procedure> Procedures { get; }
    // user (default) or system
    public CompileMode CompileMode { get; }

    public Module(
        ModuleDeclaration? declaration,
        IReadOnlyList<TypeDef>? typeDefs,
        IReadOnlyList<ProcDecl>? procDeclarations,
        IReadOnlyList<ProcDecl>? paramProcDecls,
        IReadOnlyList<Procedure>? procedures,
        CompileMode compileMode,
        int line,
        int column)
        : base(line, column)
    {
        Declaration      = declaration;
        TypeDefs         = typeDefs         ?? System.Array.Empty<TypeDef>();
        ProcDeclarations = procDeclarations ?? System.Array.Empty<ProcDecl>();
        ParamProcDecls   = paramProcDecls   ?? System.Array.Empty<ProcDecl>();
        Procedures       = procedures       ?? System.Array.Empty<Procedure>();
        CompileMode      = compileMode;
    }

    /// <summary>Get module name, or null if anonymous.</summary>
    public string? Name => Declaration?.Name;

    /// <summary>Get all exported procedure signatures (from procedure declarations with exported=true).</summary>
    public ISet<string> ExportedSignatures
    {
        get
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var decl in ProcDeclarations)
            {
                if (decl.Exported) result.Add(decl.Key);
            }
            return result;
        }
    }

    public override string ToString() => $"Module({Name ?? "_anonymous"}, {Procedures.Count} procedures)";
}
