// lib/compiler/pmt/occurrence.cs
//
// PMT Occurrence Classifier: Classifies variable occurrences as reader or writer
//
// Classification rules (syntactic):
//   - Variable with `?` suffix (e.g., `X?`) — reader occurrence
//   - Variable without `?` suffix (e.g., `X`) — writer occurrence
//
// The syntactic annotation in source code is authoritative for SRSW checking.
// Mode declarations are used for separate mode consistency validation.
//
// Converted from Dart source: lib/compiler/pmt/occurrence.dart
// source_sha256: cb56e5b79b12f401309ef978dd33b1fdb7ccafd1cd7a202e52f2f797905df6d1

using GlpRuntime.Compiler;

namespace GlpRuntime.Compiler.Pmt;

// ============================================================================
// OccurrenceType enum
// ============================================================================

/// <summary>Type of variable occurrence.</summary>
/// <remarks>
/// Declaration order preserved: Writer == (OccurrenceType)0, Reader == (OccurrenceType)1.
/// PascalCase per .NET enumeration naming guidelines.
/// </remarks>
public enum OccurrenceType
{
    /// <summary>The variable appears as a writer (output).</summary>
    Writer,
    /// <summary>The variable appears as a reader (input).</summary>
    Reader,
}

// ============================================================================
// Occurrence value class
// ============================================================================

/// <summary>A single variable occurrence with its classification and source location.</summary>
public sealed class Occurrence : IEquatable<Occurrence>
{
    /// <summary>Variable name.</summary>
    public string Variable { get; }

    /// <summary>Occurrence type (Writer or Reader).</summary>
    public OccurrenceType Type { get; }

    /// <summary>Source line number.</summary>
    public int Line { get; }

    /// <summary>Source column number.</summary>
    public int Column { get; }

    public Occurrence(string variable, OccurrenceType type, int line, int column)
    {
        Variable = variable;
        Type     = type;
        Line     = line;
        Column   = column;
    }

    /// <summary>Returns a string of the form <c>Variable:Type@Line:Column</c>.</summary>
    /// <remarks>
    /// Enum.ToString() returns the PascalCase member name ("Writer"/"Reader"),
    /// corresponding to Dart's Enum.name ("writer"/"reader") with a documented
    /// case drift — diagnostic-only surface, not a serialisation contract.
    /// </remarks>
    public override string ToString() => $"{Variable}:{Type}@{Line}:{Column}";

    public bool Equals(Occurrence? other)
    {
        if (other is null) return false;
        return Variable == other.Variable
            && Type     == other.Type
            && Line     == other.Line
            && Column   == other.Column;
    }

    public override bool Equals(object? obj) => Equals(obj as Occurrence);

    public override int GetHashCode() => HashCode.Combine(Variable, Type, Line, Column);

    public static bool operator ==(Occurrence? a, Occurrence? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    public static bool operator !=(Occurrence? a, Occurrence? b) => !(a == b);
}

// ============================================================================
// OccurrenceClassifier
// ============================================================================

/// <summary>Classifies variable occurrences in clauses based on mode declarations.</summary>
public class OccurrenceClassifier
{
    /// <summary>The mode table used for mode consistency validation.</summary>
    public ModeTable ModeTable { get; }

    public OccurrenceClassifier(ModeTable modeTable)
    {
        ModeTable = modeTable;
    }

    /// <summary>
    /// Classify all variable occurrences in a clause.
    /// </summary>
    /// <param name="clause">The clause to analyze.</param>
    /// <param name="headModes">The modes for the clause's predicate (from mode table).</param>
    /// <returns>A list of all variable occurrences with their classifications.</returns>
    public List<Occurrence> ClassifyClause(Clause clause, IReadOnlyList<Mode> headModes)
    {
        var occurrences = new List<Occurrence>();

        // Classify head arguments
        _ClassifyHead(clause.Head, headModes, occurrences);

        // Classify body goals
        if (clause.Body != null)
        {
            foreach (var goal in clause.Body)
                _ClassifyGoal(goal, occurrences);
        }

        // Classify guard arguments (guards are read-only, so all variables are readers)
        if (clause.Guards != null)
        {
            foreach (var guard in clause.Guards)
                _ClassifyGuard(guard, occurrences);
        }

        return occurrences;
    }

    /// <summary>Classify variables in clause head.</summary>
    private void _ClassifyHead(Atom head, IReadOnlyList<Mode> headModes, List<Occurrence> @out)
    {
        // TODO: use headModes once cross-module mode lookup lands
        _ = headModes; // headModes unused for now — kept for API stability
        for (int i = 0; i < head.Args.Count && i < headModes.Count; i++)
        {
            // Collect variables using syntactic annotations (headModes unused for now)
            _CollectVariables(head.Args[i], @out);
        }
    }

    /// <summary>Classify variables in a body goal.</summary>
    private void _ClassifyGoal(Goal goal, List<Occurrence> @out)
    {
        // Skip remote goals for now (would need cross-module mode lookup)
        if (goal is RemoteGoal)
        {
            return;
        }

        // Handle SpawnGoal by processing its inner goal
        if (goal is SpawnGoal spawn)
        {
            _ClassifyGoal(spawn.InnerGoal, @out);
            return;
        }

        // Collect variables from all arguments using syntactic annotations
        foreach (var arg in goal.Args)
            _CollectVariables(arg, @out);
    }

    /// <summary>Classify variables in a guard.</summary>
    private void _ClassifyGuard(Guard guard, List<Occurrence> @out)
    {
        foreach (var arg in guard.Args)
            _CollectVariables(arg, @out);
    }

    /// <summary>Recursively collect variable occurrences from a term using syntactic annotations.</summary>
    private void _CollectVariables(Term term, List<Occurrence> @out)
    {
        if (term is VarTerm varTerm)
        {
            // Skip named anonymous variables (Section 9 of typed-glp-manual)
            // Variables starting with _ are anonymous and exempt from SRSW
            if (varTerm.Name.StartsWith("_", StringComparison.Ordinal))
                return;
            // Use syntactic annotation: X? is reader, X is writer
            var occType = varTerm.IsReader ? OccurrenceType.Reader : OccurrenceType.Writer;
            @out.Add(new Occurrence(varTerm.Name, occType, varTerm.Line, varTerm.Column));
        }
        else if (term is StructTerm structTerm)
        {
            foreach (var arg in structTerm.Args)
                _CollectVariables(arg, @out);
        }
        else if (term is ListTerm listTerm)
        {
            if (listTerm.Head != null)
                _CollectVariables(listTerm.Head, @out);
            if (listTerm.Tail != null)
                _CollectVariables(listTerm.Tail, @out);
        }
        // ConstTerm, UnderscoreTerm — no variables to collect
    }
}

// ============================================================================
// OccurrenceExtras — host for top-level helpers
// ============================================================================

/// <summary>
/// Static helpers for occurrence analysis.
/// Hosts Dart top-level functions that have no natural home on a value or classifier type.
/// </summary>
public static class OccurrenceExtras
{
    /// <summary>Group occurrences by variable name.</summary>
    public static Dictionary<string, List<Occurrence>> GroupByVariable(
        IReadOnlyList<Occurrence> occurrences)
    {
        var result = new Dictionary<string, List<Occurrence>>(StringComparer.Ordinal);
        foreach (var occ in occurrences)
        {
            if (!result.TryGetValue(occ.Variable, out var bucket))
            {
                bucket = new List<Occurrence>();
                result[occ.Variable] = bucket;
            }
            bucket.Add(occ);
        }
        return result;
    }

    /// <summary>Count writer and reader occurrences.</summary>
    /// <returns>Named value tuple <c>(Writers, Readers)</c>.</returns>
    public static (int Writers, int Readers) CountOccurrences(
        IReadOnlyList<Occurrence> occurrences)
    {
        int writers = 0;
        int readers = 0;
        foreach (var occ in occurrences)
        {
            if (occ.Type == OccurrenceType.Writer)
                writers++;
            else
                readers++;
        }
        return (Writers: writers, Readers: readers);
    }
}
