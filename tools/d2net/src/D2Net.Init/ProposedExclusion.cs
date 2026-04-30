namespace D2Net.Init;

public enum ExclusionKind
{
    Tool,
    Pattern,
    Manual,
}

/// <summary>FR-013: <c>kind</c> records why each exclusion exists.</summary>
public sealed record ProposedExclusion(string Path, ExclusionKind Kind, string Reason);
