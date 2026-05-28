// lib/analysis/analysis_phase.cs
//
// Interface for analysis phases that can run independently or as part of compilation.
// Phases include: Type Checking, SRSW Checking, Defined Guards expansion.
// Converted from lib/analysis/analysis_phase.dart (source_sha256: d322a2608cddcee827d4c360ba15b5ac5c7a8a2c5e43b2a690da8b2711e51d78)

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlpRuntime.Analysis;

/// <summary>Interface for analysis phases.</summary>
public interface IAnalysisPhase
{
    /// <summary>Name of this phase (for error reporting).</summary>
    string Name { get; }

    /// <summary>Run this analysis phase on the AST. Returns list of errors/warnings found.</summary>
    List<AnalysisError> Analyze(dynamic ast, AnalysisContext ctx);
}

/// <summary>Base class for analysis errors.</summary>
public class AnalysisError
{
    public string Phase { get; }
    public string Message { get; }
    public int Line { get; }
    public int Column { get; }
    public string? Context { get; }

    public AnalysisError(string phase, string message, int line, int column, string? context = null)
    {
        Phase = phase;
        Message = message;
        Line = line;
        Column = column;
        Context = context;
    }

    public override string ToString()
    {
        var loc = $"line {Line}, column {Column}";
        var ctx = Context != null ? $"\n    {Context}" : "";
        return $"[{Phase}] {Message} at {loc}{ctx}";
    }

    /// <summary>Error severity.</summary>
    public virtual bool IsError => true;
}

/// <summary>Analysis warning (non-fatal).</summary>
public class AnalysisWarning : AnalysisError
{
    public AnalysisWarning(string phase, string message, int line, int column, string? context = null)
        : base(phase, message, line, column, context)
    {
    }

    public override bool IsError => false;
}

/// <summary>Shared context passed between analysis phases.</summary>
public class AnalysisContext
{
    /// <summary>Type environment (populated by type parsing).</summary>
    public dynamic TypeEnvironment { get; set; } = null!;

    /// <summary>Variable information (populated by SRSW analysis).</summary>
    public Dictionary<string, dynamic> VariableInfo { get; set; } = new();

    /// <summary>Expanded guards (populated by defined guards phase).</summary>
    public Dictionary<string, dynamic> ExpandedGuards { get; set; } = new();

    /// <summary>Any additional phase-specific data. Reference is final; contents are mutable.</summary>
    public Dictionary<string, dynamic> Data { get; } = new();
}

/// <summary>Result of running analysis.</summary>
public class AnalysisResult
{
    public List<AnalysisError> Errors { get; }
    public AnalysisContext Context { get; }

    public AnalysisResult(List<AnalysisError> errors, AnalysisContext context)
    {
        Errors = errors;
        Context = context;
    }

    /// <summary>True if no errors (warnings are OK).</summary>
    public bool Success => !Errors.Any(e => e.IsError);

    /// <summary>Just the actual errors (not warnings). Materialised eagerly per call.</summary>
    public List<AnalysisError> ActualErrors => Errors.Where(e => e.IsError).ToList();

    /// <summary>Just the warnings. Materialised eagerly per call.</summary>
    public List<AnalysisError> Warnings => Errors.Where(e => !e.IsError).ToList();

    public override string ToString()
    {
        if (Errors.Count == 0)
        {
            return "Analysis successful (no errors or warnings)";
        }

        var sb = new StringBuilder();
        var errs = ActualErrors;
        var warns = Warnings;

        if (errs.Count > 0)
        {
            sb.AppendLine($"Errors ({errs.Count}):");
            foreach (var e in errs)
            {
                sb.AppendLine($"  {e}");
            }
        }

        if (warns.Count > 0)
        {
            sb.AppendLine($"Warnings ({warns.Count}):");
            foreach (var w in warns)
            {
                sb.AppendLine($"  {w}");
            }
        }

        return sb.ToString();
    }
}

/// <summary>Runs multiple analysis phases.</summary>
public class AnalysisRunner
{
    public List<IAnalysisPhase> Phases { get; }

    public AnalysisRunner(List<IAnalysisPhase> phases)
    {
        Phases = phases;
    }

    /// <summary>
    /// Run all phases on the AST.
    /// If stopOnError is true, stops at first phase with errors.
    /// </summary>
    public AnalysisResult Run(dynamic ast, bool stopOnError = false)
    {
        var ctx = new AnalysisContext();
        var allErrors = new List<AnalysisError>();

        foreach (var phase in Phases)
        {
            List<AnalysisError> errors = phase.Analyze(ast, ctx);
            allErrors.AddRange(errors);

            if (stopOnError && errors.Any(e => e.IsError))
            {
                break;
            }
        }

        return new AnalysisResult(allErrors, ctx);
    }

    /// <summary>Run only specific phases by name.</summary>
    public AnalysisResult RunPhases(dynamic ast, List<string> phaseNames)
    {
        var ctx = new AnalysisContext();
        var allErrors = new List<AnalysisError>();

        foreach (var phase in Phases)
        {
            if (phaseNames.Contains(phase.Name))
            {
                List<AnalysisError> errors = phase.Analyze(ast, ctx);
                allErrors.AddRange(errors);
            }
        }

        return new AnalysisResult(allErrors, ctx);
    }
}

// =============================================================================
// Standard analysis phases
// =============================================================================

/// <summary>Type checking phase.</summary>
public class TypeCheckPhase : IAnalysisPhase
{
    /// <summary>Optional source for type parsing.</summary>
    public string? SourceCode { get; }

    public TypeCheckPhase(string? sourceCode = null)
    {
        SourceCode = sourceCode;
    }

    public string Name => "type";

    public List<AnalysisError> Analyze(dynamic ast, AnalysisContext ctx)
    {
        // Import the type checker components
        // This is a placeholder - actual implementation requires imports

        // For now, return empty list
        // Full implementation in separate integration file
        return new List<AnalysisError>();
    }
}

/// <summary>SRSW checking phase.</summary>
public class SRSWCheckPhase : IAnalysisPhase
{
    public string Name => "srsw";

    public List<AnalysisError> Analyze(dynamic ast, AnalysisContext ctx)
    {
        // Implementation would wrap existing SRSW checker
        // For now, placeholder
        return new List<AnalysisError>();
    }
}

/// <summary>Defined guards expansion phase.</summary>
public class DefinedGuardsPhase : IAnalysisPhase
{
    public string Name => "guards";

    public List<AnalysisError> Analyze(dynamic ast, AnalysisContext ctx)
    {
        // Implementation would expand defined guards
        // For now, placeholder
        return new List<AnalysisError>();
    }
}

/// <summary>Factory for creating standard analysis runners.</summary>
public static class AnalysisPhaseFactory
{
    /// <summary>Create the standard analysis runner with all phases.</summary>
    public static AnalysisRunner CreateStandardRunner() =>
        new AnalysisRunner(new List<IAnalysisPhase>
        {
            new TypeCheckPhase(),
            new SRSWCheckPhase(),
            new DefinedGuardsPhase(),
        });
}
