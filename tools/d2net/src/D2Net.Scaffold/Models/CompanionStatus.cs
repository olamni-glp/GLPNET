namespace D2Net.Scaffold.Models;

public static class CompanionStatus
{
    public const string Todo = "todo";
    public const string InProgress = "in-progress";
    public const string Done = "done";
    public const string Blocked = "blocked";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Todo, InProgress, Done, Blocked,
    };
}
