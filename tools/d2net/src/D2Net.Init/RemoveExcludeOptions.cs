using System.Collections.Generic;

namespace D2Net.Init;

/// <summary>
/// Parsed inputs for the incremental --remove-exclude mode (feature 008).
/// Constructed by <see cref="ArgParser"/>; consumed by
/// <see cref="RemoveExcludeRunner"/>.
/// </summary>
public sealed record RemoveExcludeOptions(
    string RepoRoot,
    IReadOnlyList<string> RawPaths,
    bool AllowSystemExclusions,
    bool Json,
    int? BridgePortOverride);
