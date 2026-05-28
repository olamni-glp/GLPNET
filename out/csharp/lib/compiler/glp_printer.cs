// GLP Printer - Converts AST back to GLP source code
//
// This serializer produces valid GLP source from AST nodes,
// preserving SRSW annotations (X vs X?) and all term types.
//
// Converted from Dart source: lib/compiler/glp_printer.dart
// source_sha256: 5c424c589cb0b27fd7b8b784177837bf743aacd3c6cf239b136201a3483a6def

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GlpRuntime.Compiler;

/// <summary>Converts GLP AST back to source code</summary>
public sealed class GlpPrinter
{
    // -------------------------------------------------------------------------
    // Static sets — infix operators (FrozenSet for immutable O(1) lookup)
    // -------------------------------------------------------------------------

    private static readonly FrozenSet<string> InfixOps =
        new[] { ":=", "=", "\\=", "=..", "+", "-", "*", "/", "//", "mod",
                "<", ">", "=<", ">=", "=:=", "=\\=", "=?=" }
        .ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> InfixGuards =
        new[] { "<", ">", "=<", ">=", "=:=", "=\\=", "=?=" }
        .ToFrozenSet(StringComparer.Ordinal);

    // -------------------------------------------------------------------------
    // Regex instances for IsAtom — hoisted to avoid per-call allocation
    // -------------------------------------------------------------------------

    private static readonly Regex AtomStart =
        new Regex("[a-z]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AtomTail =
        new Regex(@"^[a-zA-Z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // -------------------------------------------------------------------------
    // Public methods
    // -------------------------------------------------------------------------

    /// <summary>Print a complete program</summary>
    public string PrintProgram(Program program)
    {
        var buffer = new StringBuilder();

        foreach (var procedure in program.Procedures)
        {
            buffer.Append(PrintProcedure(procedure)).Append('\n');
        }

        return buffer.ToString();
    }

    /// <summary>Print a procedure (all clauses)</summary>
    public string PrintProcedure(Procedure procedure)
    {
        var buffer = new StringBuilder();

        foreach (var clause in procedure.Clauses)
        {
            buffer.Append(PrintClause(clause)).Append('\n');
        }

        return buffer.ToString();
    }

    /// <summary>Print a single clause</summary>
    public string PrintClause(Clause clause)
    {
        var buffer = new StringBuilder();

        // Head
        buffer.Append(PrintAtom(clause.Head));

        // Guards and body
        bool hasGuards = clause.Guards is { Count: > 0 };
        bool hasBody   = clause.Body   is { Count: > 0 };

        if (hasGuards || hasBody)
        {
            buffer.Append(" :- ");

            // Guards
            if (hasGuards)
            {
                buffer.Append(string.Join(", ", clause.Guards!.Select(PrintGuard)));
            }

            // Body separator - only use | if there are guards
            if (hasBody)
            {
                if (hasGuards)
                {
                    buffer.Append(" | ");
                }
                buffer.Append(string.Join(", ", clause.Body!.Select(PrintGoal)));
            }
        }

        buffer.Append('.');
        return buffer.ToString();
    }

    /// <summary>Print an atom (clause head)</summary>
    public string PrintAtom(Atom atom)
    {
        if (atom.Args.Count == 0)
        {
            return atom.Functor;
        }

        // Handle special infix operators
        if (IsInfixOperator(atom.Functor) && atom.Args.Count == 2)
        {
            return $"{PrintTerm(atom.Args[0])} {atom.Functor} {PrintTerm(atom.Args[1])}";
        }

        return $"{atom.Functor}({string.Join(", ", atom.Args.Select(PrintTerm))})";
    }

    /// <summary>Print a goal (body call)</summary>
    public string PrintGoal(Goal goal)
    {
        return goal switch
        {
            // Handle remote goals
            RemoteGoal r => $"{PrintTerm(r.Module)} # {PrintGoal(r.Goal)}",
            // Handle spawn goals
            SpawnGoal s  => $"{PrintGoal(s.InnerGoal)}@{s.AgentId}",
            // Generic Goal branch
            _            => GenericGoalString(goal),
        };
    }

    /// <summary>Print a guard</summary>
    public string PrintGuard(Guard guard)
    {
        string prefix = guard.Negated ? "~" : string.Empty;

        if (guard.Args.Count == 0)
        {
            return $"{prefix}{guard.Predicate}";
        }

        // Handle special infix operators — parenthesised for round-trip
        if (IsInfixGuardOperator(guard.Predicate) && guard.Args.Count == 2)
        {
            return $"{prefix}({PrintTerm(guard.Args[0])} {guard.Predicate} {PrintTerm(guard.Args[1])})";
        }

        return $"{prefix}{guard.Predicate}({string.Join(", ", guard.Args.Select(PrintTerm))})";
    }

    /// <summary>Print a term</summary>
    public static string PrintTerm(Term term)
    {
        return term switch
        {
            VarTerm v        => v.IsReader ? $"{v.Name}?" : v.Name,
            UnderscoreTerm _ => "_",
            ConstTerm c      => PrintConstValue(c.Value),
            ListTerm l       => PrintList(l),
            StructTerm s     => PrintStruct(s),
            _                => term.ToString() ?? string.Empty,
        };
    }

    // -------------------------------------------------------------------------
    // Private methods
    // -------------------------------------------------------------------------

    /// <summary>Print a constant value</summary>
    private static string PrintConstValue(object? value)
    {
        return value switch
        {
            null                    => "null",
            string s when IsAtom(s) => s,
            string s                => $"\"{EscapeString(s)}\"",
            int i                   => i.ToString(CultureInfo.InvariantCulture),
            double d                => d.ToString(CultureInfo.InvariantCulture),
            _                       => value.ToString() ?? "null",
        };
    }

    /// <summary>Print a list term</summary>
    private static string PrintList(ListTerm list)
    {
        if (list.IsNil)
        {
            return "[]";
        }

        // Collect elements if it's a proper list
        var elements = new List<Term>();
        Term? current = list;
        Term? tail = null;

        while (current is ListTerm cur && !cur.IsNil)
        {
            if (cur.Head is not null) elements.Add(cur.Head);
            if (cur.Tail is null) break;
            if (cur.Tail is ListTerm)
            {
                current = cur.Tail;
            }
            else
            {
                // Improper list with non-list tail
                tail = cur.Tail;
                break;
            }
        }

        return tail is not null
            ? $"[{string.Join(", ", elements.Select(PrintTerm))} | {PrintTerm(tail)}]"
            : $"[{string.Join(", ", elements.Select(PrintTerm))}]";
    }

    /// <summary>Print a structure term</summary>
    private static string PrintStruct(StructTerm structArg)
    {
        // Handle comma/conjunction specially - no functor prefix
        if (structArg.Functor == "," && structArg.Args.Count == 2)
        {
            return $"({PrintTerm(structArg.Args[0])}, {PrintTerm(structArg.Args[1])})";
        }

        // Handle special infix operators — parenthesised
        if (IsInfixOperator(structArg.Functor) && structArg.Args.Count == 2)
        {
            return $"({PrintTerm(structArg.Args[0])} {structArg.Functor} {PrintTerm(structArg.Args[1])})";
        }

        if (structArg.Args.Count == 0)
        {
            return structArg.Functor;
        }

        return $"{structArg.Functor}({string.Join(", ", structArg.Args.Select(PrintTerm))})";
    }

    /// <summary>Check if a functor is an infix operator</summary>
    private static bool IsInfixOperator(string functor) => InfixOps.Contains(functor);

    /// <summary>Check if a guard predicate is infix</summary>
    private static bool IsInfixGuardOperator(string predicate) => InfixGuards.Contains(predicate);

    /// <summary>Check if a string is a valid atom (lowercase start, alphanumeric)</summary>
    private static bool IsAtom(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        // Atoms start with lowercase letter
        if (!AtomStart.IsMatch(s[0].ToString())) return false;
        // Rest is alphanumeric or underscore
        return AtomTail.IsMatch(s.Substring(1));
    }

    /// <summary>Escape special characters in a string</summary>
    private static string EscapeString(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    /// <summary>Generic Goal branch for PrintGoal (empty-args / infix / fallthrough)</summary>
    private string GenericGoalString(Goal goal)
    {
        if (goal.Args.Count == 0)
        {
            return goal.Functor;
        }

        // Handle special infix operators
        if (IsInfixOperator(goal.Functor) && goal.Args.Count == 2)
        {
            return $"{PrintTerm(goal.Args[0])} {goal.Functor} {PrintTerm(goal.Args[1])}";
        }

        return $"{goal.Functor}({string.Join(", ", goal.Args.Select(PrintTerm))})";
    }
}
