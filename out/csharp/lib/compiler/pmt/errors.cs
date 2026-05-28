using System.Collections.Generic;
using System.Linq;

namespace GlpRuntime.Compiler.Pmt;

/// <summary>PMT Error: Represents a mode/SRSW violation detected during PMT checking.</summary>
public sealed class PmtError : IEquatable<PmtError>
{
    public string Message { get; }
    public int Line { get; }
    public int Column { get; }

    public PmtError(string message, int line, int column)
    {
        Message = message;
        Line = line;
        Column = column;
    }

    public override string ToString() => $"PMT Error at {Line}:{Column}: {Message}";

    public bool Equals(PmtError? other)
    {
        if (other is null) return false;
        return Message == other.Message && Line == other.Line && Column == other.Column;
    }

    public override bool Equals(object? obj) => Equals(obj as PmtError);

    public override int GetHashCode() => HashCode.Combine(Message, Line, Column);

    public static bool operator ==(PmtError? a, PmtError? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    public static bool operator !=(PmtError? a, PmtError? b) => !(a == b);
}

/// <summary>Exception thrown when PMT checking fails.</summary>
public class PmtErrors : Exception
{
    public IReadOnlyList<PmtError> Errors { get; }

    public PmtErrors(IReadOnlyList<PmtError> errors) : base(FormatSummary(errors))
    {
        Errors = errors;
    }

    private static string FormatSummary(IReadOnlyList<PmtError> errors)
    {
        if (errors.Count == 0) return "PmtErrors: (none)";
        return "PmtErrors:\n" + string.Join("\n", errors.Select(e => "  " + e.ToString()));
    }

    public override string ToString()
    {
        if (Errors.Count == 0) return "PmtErrors: (none)";
        return "PmtErrors:\n" + string.Join("\n", Errors.Select(e => "  " + e.ToString()));
    }
}
