namespace GlpRuntime.Compiler;

/// <summary>Error categories for compiler diagnostics</summary>
public enum ErrorCategory
{
    Lexical,    // Invalid characters, unterminated strings
    Syntax,     // Malformed clauses, missing delimiters
    Semantic,   // SRSW violations, undefined variables
    Codegen,    // Internal compiler errors during bytecode generation
}

/// <summary>Compilation error with source location</summary>
public class CompileError : Exception
{
    public long Line { get; }
    public long Column { get; }
    public string? Source { get; }
    public ErrorCategory? Category { get; }

    public CompileError(
        string message,
        long line,
        long column,
        string? source = null,
        string? phase = null) : base(message)
    {
        Line = line;
        Column = column;
        Source = source;
        Category = phase != null ? CategoryFromPhase(phase) : null;
    }

    private static ErrorCategory? CategoryFromPhase(string phase) =>
        phase switch
        {
            "lexer"    => ErrorCategory.Lexical,
            "parser"   => ErrorCategory.Syntax,
            "analyzer" => ErrorCategory.Semantic,
            "codegen"  => ErrorCategory.Codegen,
            _          => null,
        };

    public override string ToString()
    {
        var categoryName = Category != null ? $"[{Category}] " : "";
        var loc = $"Line {Line}, Column {Column}";

        if (Source != null)
        {
            var lines = Source.Split('\n');
            if (Line > 0 && Line <= lines.Length)
            {
                var sourceLine = lines[Line - 1];
                var pointer = new string(' ', (int)(Column - 1)) + "^";
                return $"{categoryName}{Message}\n{loc}:\n{sourceLine}\n{pointer}";
            }
        }

        return $"{categoryName}{Message} at {loc}";
    }
}
