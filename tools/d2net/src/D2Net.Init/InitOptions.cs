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

/// <summary>
/// Parsed CLI options for the inspection paths (--list / --Exclusions / --current-phase).
/// FR-012 / Q3: <see cref="BridgePortOverride"/> is non-null only when the user passed
/// <c>--bridge-port</c> on this invocation; otherwise the inspection runner reads
/// the persisted port from settings.
/// </summary>
public sealed record InspectOptions(
    string RepoRoot,
    InspectMode Mode,
    bool Json,
    int? BridgePortOverride);

public enum InspectMode
{
    List,
    Exclusions,
    CurrentPhase,
}
