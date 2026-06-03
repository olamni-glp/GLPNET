// lib/compiler/pmt/checker.cs
//
// PMT SRSW Checker: Verifies Single-Reader/Single-Writer constraint
//
// SRSW Rules:
//   - Each variable must have exactly 1 writer occurrence
//   - Each variable must have at least 1 reader occurrence
//   - Multiple reader occurrences allowed only if variable is grounded by a guard
//
// Supports multiple mode alternatives (union of mode declarations).
// A clause is valid if it satisfies SRSW for at least one declared mode.
//
// Converted from Dart source: lib/compiler/pmt/checker.dart
// source_sha256: 2cdf947748a1e9b0f92210357cda90b7f453ebb6b9111c75db0445a7ade131ef

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using GlpRuntime.Compiler;

namespace GlpRuntime.Compiler.Pmt;

/// <summary>
/// PMT SRSW Checker: Verifies Single-Reader/Single-Writer constraint.
/// </summary>
public class PmtChecker
{
    // -----------------------------------------------------------------------
    // Static readonly FrozenSet fields (from Dart const Set<String> locals)
    // -----------------------------------------------------------------------

    /// <summary>Type-checking guard predicates that imply groundness (arity 1).</summary>
    private static readonly FrozenSet<string> TypeCheckOps = new[]
    {
        "ground", "number", "integer", "float", "atom", "string",
        "list", "tuple", "compound", "var", "nonvar",
        "is_mutual_ref", "unknown",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Comparison guard predicates that imply groundness (arity 2).</summary>
    private static readonly FrozenSet<string> ComparisonOps = new[]
    {
        "<", ">", "=<", ">=", "=:=", "=\\=", "=?=",
    }.ToFrozenSet(StringComparer.Ordinal);

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    /// <summary>The mode table used for mode declarations.</summary>
    public ModeTable ModeTable { get; }

    /// <summary>The owned occurrence classifier.</summary>
    public OccurrenceClassifier Classifier { get; }

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public PmtChecker(ModeTable modeTable)
    {
        ModeTable  = modeTable;
        Classifier = new OccurrenceClassifier(modeTable);
    }

    // -----------------------------------------------------------------------
    // CheckProcedure
    // -----------------------------------------------------------------------

    /// <summary>Check all clauses in a procedure.</summary>
    public List<PmtError> CheckProcedure(Procedure proc)
    {
        var errors   = new List<PmtError>();
        var allModes = ModeTable.GetAllModes(proc.Name, proc.Arity);
        // No mode declaration — skip checking
        if (allModes is null || allModes.Count == 0) return errors;
        foreach (var clause in proc.Clauses)
        {
            errors.AddRange(CheckClauseAgainstModes(clause, allModes));
        }
        return errors;
    }

    // -----------------------------------------------------------------------
    // CheckClauseAgainstModes
    // -----------------------------------------------------------------------

    /// <summary>
    /// Check a single clause against multiple mode alternatives.
    /// Clause is valid if it satisfies SRSW for at least one mode.
    /// </summary>
    public List<PmtError> CheckClauseAgainstModes(Clause clause, List<List<Mode>> allModes)
    {
        // Single mode — use standard checking
        if (allModes.Count == 1) return CheckClause(clause, allModes[0]);

        // Multiple modes — try each until one succeeds
        List<PmtError>? bestErrors = null;

        foreach (var modes in allModes)
        {
            var errors = CheckClause(clause, modes);
            // This mode works — clause is valid
            if (errors.Count == 0) return new List<PmtError>();
            // Track the mode with fewest errors
            if (bestErrors is null || errors.Count < bestErrors.Count) bestErrors = errors;
        }

        // No mode matched — return errors from best match, with note about alternatives
        if (bestErrors is not null && bestErrors.Count > 0)
        {
            // Add a note that this is a multimodal predicate
            var modeStrings = string.Join(" | ",
                allModes.Select(m => $"({string.Join(", ", m.Select(mode => mode == Mode.Reader ? "?" : ""))})"));
            var result = new List<PmtError>
            {
                new PmtError($"Clause does not match any declared mode. Available modes: {modeStrings}", clause.Line, clause.Column),
            };
            result.AddRange(bestErrors);
            return result;
        }

        return bestErrors ?? new List<PmtError>();
    }

    // -----------------------------------------------------------------------
    // CheckClause
    // -----------------------------------------------------------------------

    /// <summary>Check a single clause against a specific mode declaration.</summary>
    public List<PmtError> CheckClause(Clause clause, IReadOnlyList<Mode> modes)
    {
        var errors = new List<PmtError>();

        // Classify all variable occurrences
        var occurrences = Classifier.ClassifyClause(clause, modes);

        // Group by variable name
        var byVar = OccurrenceExtras.GroupByVariable(occurrences);

        // Extract grounded variables from guards
        var groundedVars = _ExtractGroundedVars(clause);

        // Verify SRSW for each variable
        foreach (var entry in byVar)
        {
            var varName = entry.Key;
            var occs    = entry.Value;
            var counts  = OccurrenceExtras.CountOccurrences(occs);

            // Check writer occurrences
            if (counts.Writers == 0)
            {
                errors.Add(new PmtError($"Variable {varName} has no writer occurrence", occs[0].Line, occs[0].Column));
            }
            else if (counts.Writers > 1)
            {
                // Find the second writer for error location
                var secondWriter = occs.Where(o => o.Type == OccurrenceType.Writer).Skip(1).First();
                errors.Add(new PmtError($"Variable {varName} has {counts.Writers} writer occurrences (expected 1)", secondWriter.Line, secondWriter.Column));
            }

            // Check reader occurrences
            if (counts.Readers == 0)
            {
                errors.Add(new PmtError($"Variable {varName} has no reader occurrence", occs[0].Line, occs[0].Column));
            }
            else if (counts.Readers > 1 && !groundedVars.Contains(varName))
            {
                // Find the second reader for error location
                var secondReader = occs.Where(o => o.Type == OccurrenceType.Reader).Skip(1).First();
                errors.Add(new PmtError($"Variable {varName} has {counts.Readers} reader occurrences; add ground({varName}) guard", secondReader.Line, secondReader.Column));
            }
        }

        return errors;
    }

    // -----------------------------------------------------------------------
    // _ExtractGroundedVars
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extract variables that are grounded by guards.
    /// Guards that imply groundness:
    ///   - ground/1: explicit groundness
    ///   - Type guards (arity 1): number, integer, float, atom, string, list, tuple, compound, var, nonvar
    ///   - is_mutual_ref/1, unknown/1
    ///   - Comparison guards (arity 2): &lt;, &gt;, =&lt;, &gt;=, =:=, =\=
    ///   - =?=/2: ground equality
    /// </summary>
    private HashSet<string> _ExtractGroundedVars(Clause clause)
    {
        var grounded = new HashSet<string>(StringComparer.Ordinal);
        if (clause.Guards is null) return grounded;
        foreach (var guard in clause.Guards)
        {
            // Arity-1 guards that imply groundness
            if (TypeCheckOps.Contains(guard.Predicate) && guard.Args.Count == 1)
            {
                _CollectVarNames(guard.Args[0], grounded);
            }
            // Arity-2 guards that imply groundness (both args)
            if (ComparisonOps.Contains(guard.Predicate) && guard.Args.Count == 2)
            {
                _CollectVarNames(guard.Args[0], grounded);
                _CollectVarNames(guard.Args[1], grounded);
            }
        }
        return grounded;
    }

    // -----------------------------------------------------------------------
    // _CollectVarNames
    // -----------------------------------------------------------------------

    /// <summary>Collect variable names from a term (recursively).</summary>
    private void _CollectVarNames(Term term, HashSet<string> @out)
    {
        if (term is VarTerm varTerm)
        {
            @out.Add(varTerm.Name);
        }
        else if (term is StructTerm structTerm)
        {
            foreach (var arg in structTerm.Args)
            {
                _CollectVarNames(arg, @out);
            }
        }
        else if (term is ListTerm listTerm)
        {
            if (listTerm.Head is not null) _CollectVarNames(listTerm.Head, @out);
            if (listTerm.Tail is not null) _CollectVarNames(listTerm.Tail, @out);
        }
        // ConstTerm, UnderscoreTerm — no variables
    }
}
