using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace D2Net.Init;

public sealed class PromptCancelledException : Exception
{
    public PromptCancelledException(string message) : base(message) { }
}

/// <summary>
/// FR-005, FR-006, FR-008: prompt-based exclusion approval cycle (display →
/// optional remove → redisplay → accept), and prompts for any of source /
/// target-extension / target name that the caller did not supply on the CLI.
/// In <c>--non-interactive</c> mode missing inputs throw and 'q' is unavailable.
/// </summary>
public sealed class InteractivePrompter
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly bool _nonInteractive;

    public InteractivePrompter(TextReader input, TextWriter output, bool nonInteractive)
    {
        _input = input;
        _output = output;
        _nonInteractive = nonInteractive;
    }

    public InitOptions FillMissingInputs(InitOptions opts)
    {
        var src = opts.SourceDir
            ?? PromptForString("Source directory name (e.g. 'glp_runtime'):", "--source");
        var ext = opts.TargetExtension
            ?? PromptForString("Target directory extension (e.g. '_net'):", "--target-extension", allowEmpty: true);
        var tgt = opts.TargetDir
            ?? PromptForString("Target directory name (e.g. 'glp_runtime_net'):", "--target");
        return opts with { SourceDir = src, TargetExtension = ext, TargetDir = tgt };
    }

    /// <summary>
    /// Drives the suggested-list/remove/redisplay/approve loop.
    /// Returns the final approved list. Throws <see cref="PromptCancelledException"/>
    /// if the user types 'q' (only possible in interactive mode).
    /// </summary>
    public IReadOnlyList<ProposedExclusion> ApproveExclusions(
        IReadOnlyList<ProposedExclusion> suggested,
        bool acceptSuggestedFlag)
    {
        if (acceptSuggestedFlag || _nonInteractive)
            return suggested;

        var current = new List<ProposedExclusion>(suggested);
        Display(current);
        while (true)
        {
            _output.Write("> ");
            _output.Flush();
            var line = _input.ReadLine();
            if (line is null)
                throw new PromptCancelledException("Interactive prompt closed (EOF) without approval.");
            line = line.Trim();
            if (line.Length == 0) continue;

            if (line.Equals("a", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("accept", StringComparison.OrdinalIgnoreCase))
            {
                _output.WriteLine("Approved.");
                return current;
            }
            if (line.Equals("q", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("quit", StringComparison.OrdinalIgnoreCase))
            {
                throw new PromptCancelledException("User aborted at exclusion-approval prompt.");
            }
            if (line.Equals("l", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("list", StringComparison.OrdinalIgnoreCase))
            {
                Display(current);
                continue;
            }
            if (line.StartsWith("r ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("remove ", StringComparison.OrdinalIgnoreCase))
            {
                var arg = line.Substring(line.IndexOf(' ') + 1).Trim();
                if (int.TryParse(arg, out var idx) && idx >= 1 && idx <= current.Count)
                {
                    current.RemoveAt(idx - 1);
                    Display(current);
                }
                else
                {
                    _output.WriteLine($"Invalid row number '{arg}'. Try again.");
                }
                continue;
            }
            _output.WriteLine("Unknown command. Use [a]ccept, [r]emove <n>, [l]ist, [q]uit.");
        }
    }

    private void Display(IReadOnlyList<ProposedExclusion> items)
    {
        _output.WriteLine($"Suggested exclusions ({items.Count}):");
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var k = it.Kind switch
            {
                ExclusionKind.Tool => "tool   ",
                ExclusionKind.Pattern => "pattern",
                ExclusionKind.Manual => "manual ",
                _ => "?      ",
            };
            _output.WriteLine($"  {i + 1,3}. [{k}] {it.Path}");
        }
        _output.WriteLine("Actions:");
        _output.WriteLine("  [a]ccept all       — approve the list as-is");
        _output.WriteLine("  [r]emove <n>       — remove item by row number");
        _output.WriteLine("  [l]ist             — redisplay current list");
        _output.WriteLine("  [q]uit             — abort init");
    }

    private string PromptForString(string question, string flagName, bool allowEmpty = false)
    {
        if (_nonInteractive)
            throw new ArgumentException(
                $"Missing required input '{flagName}'. Run with --non-interactive only when every required flag is supplied.");
        while (true)
        {
            _output.WriteLine(question);
            _output.Write("> ");
            _output.Flush();
            var line = _input.ReadLine();
            if (line is null)
                throw new PromptCancelledException("Interactive prompt closed (EOF) without value.");
            var trimmed = line.Trim();
            if (trimmed.Length > 0 || allowEmpty) return trimmed;
            _output.WriteLine("(empty) — please supply a value.");
        }
    }
}
