using System.Collections.Generic;

namespace D2Net.Init;

/// <summary>
/// Parsed inputs for the incremental --add-exclude mode (feature 007).
/// Constructed by <see cref="ArgParser"/>; consumed by
/// <see cref="AddExcludeRunner"/>.
/// </summary>
public sealed record AddExcludeOptions(
    string RepoRoot,
    IReadOnlyList<string> RawPaths,
    bool Json,
    int? BridgePortOverride);
