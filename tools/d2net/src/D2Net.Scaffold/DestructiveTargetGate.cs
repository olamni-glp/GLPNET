using System.IO;

namespace D2Net.Scaffold;

/// <summary>
/// FR-012a: hard-gated interactive confirmation for the
/// <c>--FORCE --DELETE-TARGET</c> destructive path. Prints a single prompt
/// naming the absolute target directory and waits for one line of input.
/// Returns true iff the reply is an affirmative (case-insensitive
/// <c>yes</c>, <c>y</c>, <c>confirmed</c>, or <c>proceed</c>); any other
/// reply, EOF, or empty input returns false.
/// </summary>
/// <remarks>
/// The prompt is written to stderr (not stdout) to keep <c>--json</c>
/// machine-readable success/error envelopes uncorrupted when this gate
/// happens to fire under unexpected automation.
/// </remarks>
public static class DestructiveTargetGate
{
    public static bool Confirm(string absTargetPath, TextReader stdin, TextWriter stderr)
    {
        stderr.WriteLine(
            "d2net-scaffold: --FORCE --DELETE-TARGET supplied. This will recursively delete");
        stderr.WriteLine($"{absTargetPath} and all of its contents. Proceed? (yes/no)");

        var line = stdin.ReadLine();
        if (line is null) return false;
        var trimmed = line.Trim();
        if (trimmed.Length == 0) return false;

        return string.Equals(trimmed, "yes", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "y", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "confirmed", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "proceed", System.StringComparison.OrdinalIgnoreCase);
    }
}
