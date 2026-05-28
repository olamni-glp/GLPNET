/// Boot Loader for maGLP Isolate Spawning
///
/// Parses GLP files with <c>boot/0</c> clauses containing <c>@</c> spawn directives.
/// Extracts spawn configuration without modifying the GLP parser.
///
/// See: docs/ma/isolate-boot-spec.md (v0.4)

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GlpRuntime.Multiagent;

/// <summary>
/// A single spawn directive extracted from the boot clause.
/// </summary>
/// <remarks>
/// Represents <c>goalFunctor(agentId, ...)@agentId</c>
///
/// For <c>parent_init(alice, carol, 4, _)@alice</c>:
///   agentId = "alice", goalFunctor = "parent_init", goalArity = 4,
///   constantArgs = ["carol", "4"]
///
/// The isolate entry point always passes: arg0 = agentId (constant),
/// args 1..n-2 = constantArgs, arg n-1 = netInReader.
/// </remarks>
public class SpawnDirective
{
    /// <summary>The agent identifier (e.g., "alice", "bob")</summary>
    public string AgentId { get; }

    /// <summary>The goal functor to spawn (e.g., "agent_init", "parent_init")</summary>
    public string GoalFunctor { get; }

    /// <summary>The arity of the goal (e.g., 2 for agent_init/2, 4 for parent_init/4)</summary>
    public long GoalArity { get; }

    /// <summary>
    /// Constant arguments between agentId and the final netIn variable.
    /// For <c>parent_init(alice, carol, 4, _)@alice</c>: ["carol", "4"]
    /// For <c>agent_init(alice, _)@alice</c>: [] (empty)
    /// </summary>
    public IReadOnlyList<string> ConstantArgs { get; }

    public SpawnDirective(
        string agentId,
        string goalFunctor,
        long goalArity,
        IReadOnlyList<string>? constantArgs = null)
    {
        AgentId = agentId;
        GoalFunctor = goalFunctor;
        GoalArity = goalArity;
        ConstantArgs = constantArgs ?? Array.Empty<string>();
    }

    public override string ToString() =>
        $"SpawnDirective({GoalFunctor}/{GoalArity}({AgentId}, ...)@{AgentId})";
}

/// <summary>Configuration extracted from a GLP boot file.</summary>
public class BootConfig
{
    /// <summary>The spawn directives from the boot clause</summary>
    public IReadOnlyList<SpawnDirective> Directives { get; }

    /// <summary>The full source code (original, including boot clause)</summary>
    public string FullSource { get; }

    /// <summary>
    /// Source code with boot clause stripped (for GLP compilation).
    /// The boot clause contains @ which the GLP parser doesn't understand.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Optional shared source files to load before the boot file
    /// (e.g., social_agent.glp containing agent/4, actors, helpers).
    /// Each entry is loaded separately to preserve per-file -mode() directives.
    /// </summary>
    public List<string>? SharedSources { get; set; }

    /// <summary>
    /// Optional project directory for static linking.
    /// When set, each isolate loads the project via loadProject() instead of
    /// loading individual shared sources. The boot source is loaded on top.
    /// </summary>
    public string? ProjectDir { get; set; }

    /// <summary>Absolute path to programs/self.glp</summary>
    public string RootSelfGlpPath { get; set; }

    public BootConfig(
        IReadOnlyList<SpawnDirective> directives,
        string fullSource,
        string source,
        List<string>? sharedSources = null,
        string? projectDir = null,
        string rootSelfGlpPath = "")
    {
        Directives = directives;
        FullSource = fullSource;
        Source = source;
        SharedSources = sharedSources;
        ProjectDir = projectDir;
        RootSelfGlpPath = rootSelfGlpPath;
    }
}

/// <summary>
/// Loader for GLP files with isolate boot clauses.
/// </summary>
/// <remarks>
/// Parses the boot clause to extract spawn directives, then provides
/// the source for compilation.
/// </remarks>
public class BootLoader
{
    /// <summary>
    /// Load a GLP file and extract boot configuration.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="BootLoaderException"/> if:
    /// <list type="bullet">
    /// <item><description>File doesn't start with <c>procedure boot.</c></description></item>
    /// <item><description>Boot clause is malformed</description></item>
    /// <item><description>Agent IDs don't match between goal and @target</description></item>
    /// <item><description>Duplicate agent IDs</description></item>
    /// </list>
    /// </remarks>
    public BootConfig Load(string source)
    {
        var directives = ParseBootClause(source);
        var compilableSource = StripBootClause(source);
        return new BootConfig(
            directives: directives,
            fullSource: source,
            source: compilableSource
        );
    }

    /// <summary>Load from file path (convenience method)</summary>
    public BootConfig LoadFile(string filePath)
    {
        var file = ReadFile(filePath);
        return Load(file);
    }

    /// <summary>Parse the boot clause and extract spawn directives.</summary>
    private IReadOnlyList<SpawnDirective> ParseBootClause(string source)
    {
        // Remove comments for parsing
        var noComments = RemoveComments(source);

        // Check for procedure boot declaration
        if (!HasProcedureBoot(noComments))
        {
            throw new BootLoaderException("First procedure must be boot/0. " +
                "Expected \"procedure boot.\" declaration.");
        }

        // Find boot clause
        var bootClause = ExtractBootClause(noComments);
        if (bootClause == null)
        {
            throw new BootLoaderException("Could not find boot clause. " +
                "Expected \"boot :- ... .\"");
        }

        // Parse spawn directives from boot clause body
        var directives = ParseSpawnDirectives(bootClause);
        if (directives.Count == 0)
        {
            throw new BootLoaderException("Boot clause contains no spawn directives. " +
                "Expected \"goal(agent, _)@agent\"");
        }

        // Validate no duplicate agent IDs
        var agentIds = new HashSet<string>();
        foreach (var d in directives)
        {
            if (agentIds.Contains(d.AgentId))
            {
                throw new BootLoaderException($"Duplicate agent ID: {d.AgentId}");
            }
            agentIds.Add(d.AgentId);
        }

        return directives;
    }

    /// <summary>Remove GLP comments (lines starting with %)</summary>
    private string RemoveComments(string source)
    {
        return string.Join("\n", source.Split('\n').Where(line => !line.TrimStart().StartsWith("%")));
    }

    /// <summary>Check if source has "procedure boot." declaration</summary>
    private bool HasProcedureBoot(string source)
    {
        // Match "procedure boot." with flexible whitespace
        var pattern = new Regex(@"procedure\s+boot\s*\.", RegexOptions.Multiline);
        return pattern.IsMatch(source);
    }

    /// <summary>Extract the boot clause body (between "boot :-" and ".")</summary>
    private string? ExtractBootClause(string source)
    {
        // Match "boot :- ... ." capturing the body
        // This handles multi-line boot clauses
        var pattern = new Regex(
            @"boot\s*:-\s*(.*?)\.\s*(?=\n|procedure|$)",
            RegexOptions.Multiline | RegexOptions.Singleline
        );
        var match = pattern.Match(source);
        if (!match.Success) return null;
        return match.Groups[1].Value.Trim();
    }

    /// <summary>
    /// Parse spawn directives from boot clause body.
    /// </summary>
    /// <remarks>
    /// Looks for patterns like:
    /// <c>functor(agentId, ...)@agentId</c>
    ///
    /// The first argument must be the agent ID (an atom).
    /// Remaining arguments can be arbitrary terms (e.g., ch(_?,_)).
    /// The @target must match the first argument.
    /// </remarks>
    private IReadOnlyList<SpawnDirective> ParseSpawnDirectives(string clauseBody)
    {
        var directives = new List<SpawnDirective>();

        // Strategy: find each @target, then work backwards to find the matching
        // goal(agentId, ...) by balancing parentheses.
        var atPattern = new Regex(@"@\s*(\w+)");

        foreach (Match atMatch in atPattern.Matches(clauseBody))
        {
            var targetAgentId = atMatch.Groups[1].Value;
            var beforeAt = clauseBody.Substring(0, atMatch.Index).TrimEnd();

            // The character before @ should be ')' (end of goal arguments)
            if (beforeAt.Length == 0 || beforeAt[beforeAt.Length - 1] != ')')
            {
                continue; // Not a goal@target pattern
            }

            // Walk backwards from ')' to find matching '('
            var depth = 0;
            var parenStart = -1;
            for (var i = beforeAt.Length - 1; i >= 0; i--)
            {
                if (beforeAt[i] == ')') depth++;
                if (beforeAt[i] == '(') depth--;
                if (depth == 0)
                {
                    parenStart = i;
                    break;
                }
            }

            if (parenStart < 0) continue;

            // Extract functor name before '('
            var beforeParen = beforeAt.Substring(0, parenStart).TrimEnd();
            var functorMatch = new Regex(@"(\w+)$").Match(beforeParen);
            if (!functorMatch.Success) continue;
            var functor = functorMatch.Groups[1].Value;

            // Extract all arguments from inside the parentheses, splitting at depth-0 commas.
            var argsStr = beforeAt.Substring(parenStart + 1, (beforeAt.Length - 1) - (parenStart + 1));
            var allArgs = SplitArgs(argsStr);

            if (allArgs.Count == 0) continue;

            // First arg is the agent ID (must be a simple atom)
            var goalAgentId = allArgs[0].Trim();
            if (!new Regex(@"^\w+$").IsMatch(goalAgentId))
            {
                throw new BootLoaderException(
                    "First argument of spawn goal must be an agent ID (atom), " +
                    $"got \"{goalAgentId}\"");
            }

            // Validate agent IDs match
            if (goalAgentId != targetAgentId)
            {
                throw new BootLoaderException(
                    $"Agent ID mismatch: goal has \"{goalAgentId}\" but @target is \"{targetAgentId}\". " +
                    "They must match.");
            }

            // Middle args (between agentId and last arg which is netIn) are constants.
            // For parent_init(alice, carol, 4, _)@alice: constantArgs = ["carol", "4"]
            // For agent_init(alice, _)@alice: constantArgs = []
            var constantArgs = new List<string>();
            for (var i = 1; i < allArgs.Count - 1; i++)
            {
                constantArgs.Add(allArgs[i].Trim());
            }

            directives.Add(new SpawnDirective(
                agentId: goalAgentId,
                goalFunctor: functor,
                goalArity: allArgs.Count,
                constantArgs: constantArgs
            ));
        }

        return directives;
    }

    /// <summary>Split argument string at depth-0 commas, respecting nested parens/brackets.</summary>
    private IReadOnlyList<string> SplitArgs(string argsStr)
    {
        var args = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < argsStr.Length; i++)
        {
            if (argsStr[i] == '(' || argsStr[i] == '[') depth++;
            if (argsStr[i] == ')' || argsStr[i] == ']') depth--;
            if (argsStr[i] == ',' && depth == 0)
            {
                args.Add(argsStr.Substring(start, i - start));
                start = i + 1;
            }
        }
        args.Add(argsStr.Substring(start));
        return args;
    }

    /// <summary>
    /// Strip the boot clause and procedure declaration from source.
    /// Returns source that can be compiled by the GLP compiler.
    /// </summary>
    private string StripBootClause(string source)
    {
        // Remove "procedure boot." line
        var result = new Regex(@"procedure\s+boot\s*\.\s*\n?", RegexOptions.Multiline)
            .Replace(source, "", 1);

        // Remove "boot :- ... ." clause (possibly multi-line)
        // Match from "boot :-" to the closing "." before next procedure or end
        result = new Regex(@"boot\s*:-\s*.*?\.\s*\n?", RegexOptions.Multiline | RegexOptions.Singleline)
            .Replace(result, "", 1);

        return result.Trim() + "\n";
    }

    /// <summary>Read file contents (platform-specific)</summary>
    private string ReadFile(string filePath)
    {
        // This will be implemented differently for different platforms
        // For now, we assume platform-specific implementation is required
        throw new NotImplementedException("Use Load(source) directly or implement file reading");
    }
}

/// <summary>Exception thrown by BootLoader for parse errors.</summary>
public class BootLoaderException : Exception
{
    public BootLoaderException(string message) : base(message) { }

    public override string ToString() => $"BootLoaderException: {Message}";
}
