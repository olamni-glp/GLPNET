using System.Collections.Generic;

namespace D2Net.Init;

/// <summary>
/// Parsed CLI options for the init / force-delete-init paths.
/// FR-005: any of source/target/extension may be null when interactive prompts will fill them in.
/// </summary>
public sealed record InitOptions(
    string RepoRoot,
    string? SourceDir,
    string? TargetExtension,
    string? TargetDir,
    IReadOnlyList<string> ManualExclusions,
    bool AcceptSuggestedExclusions,
    bool Force,
    bool DeleteExisting,
    bool NonInteractive,
    int BridgePort)
{
    public bool ForceDeleteRequested => Force && DeleteExisting;
}

public sealed record InspectOptions(
    string RepoRoot,
    InspectMode Mode,
    bool Json,
    int BridgePort);

public enum InspectMode
{
    List,
    Exclusions,
    CurrentPhase,
}
