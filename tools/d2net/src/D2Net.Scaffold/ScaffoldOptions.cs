namespace D2Net.Scaffold;

/// <summary>
/// Resolved CLI inputs for a single <c>d2net-scaffold</c> invocation.
/// Source / target / extension / exclusions are read from the workspace
/// (<c>.D2NET/</c>), not from CLI arguments.
/// </summary>
/// <param name="RepoRoot">Absolute path to the repository root (CWD).</param>
/// <param name="Json">Emit success summary / error envelope as JSON.</param>
/// <param name="ForceDeleteTarget">
/// Set when the operator supplied the <c>--FORCE --DELETE-TARGET</c> flag pair.
/// Authorises destruction of a pre-existing non-scaffold-managed target,
/// gated by an interactive confirmation prompt (FR-012a).
/// </param>
/// <param name="BridgePortOverride">
/// Optional override for the persisted PGLite bridge port. Defaults to the
/// port persisted in <c>D2NET-Settings.json</c>, or
/// <see cref="D2Net.Init.BridgeOptions.DefaultPort"/> if absent.
/// </param>
public sealed record ScaffoldOptions(
    string RepoRoot,
    bool Json,
    bool ForceDeleteTarget,
    int? BridgePortOverride);
