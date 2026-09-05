// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Resolving the EXISTING board root (feature 102, codex round-2 finding
// `attach-federation-to-the-existing-board-log`).
//
// Contract federation-wire.md W4 / FR-011.
//
// WHAT WAS WRONG, AND WHY IT WAS THE WORST OF THE FOURTEEN. The console built its own JSONL file
// under the federation CONFIG directory. Nothing read the scheduler's real per-actor op-logs and
// nothing wrote back to them. So the broker, guardian and oracle lanes went on reading the board
// they have always read, while federation converged a second, parallel, invisible board — which is
// precisely the second oracle this feature exists to prevent. Two boards that never disagree
// because they never meet is not agreement.
//
// THE REAL SUBSTRATE, measured on this host:
//   <root>/root.json                                   {"root_id": "...", "schema_version": "1"}
//   <root>/<kind>/<actor>/<actor>-<kind>-NNNNNN.jsonl  grow-only, one JSON object per line
//   <root>/<kind>/<actor>/heartbeat.json
// with kinds `ops`, `caps`, `cards`, `calendar`, `signals`, `views`, `replication`.
//
// A ROOT IS REFUSED IF IT IS NOT A BOARD. Silently creating `root.json` under a mistyped path
// produces a brand-new empty board that looks perfectly healthy and shares nothing with anyone —
// the same failure in a new costume. So resolution REFUSES and names what it looked for.

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>The board root could not be resolved. Named, and it says what it looked for.</summary>
public sealed class BoardRootException : InvalidOperationException
{
    public BoardRootException(string message) : base(message) { }
}

/// <summary>Locates the existing scheduler board root and this actor's log within it.</summary>
public static class BoardRoot
{
    /// <summary>The marker that makes a directory a board rather than an empty directory.</summary>
    public const string RootMarker = "root.json";

    /// <summary>The op-log kind federation reads and appends to.</summary>
    public const string OpsKind = "ops";

    /// <summary>Environment override, for tests and for a host whose coop lives elsewhere.</summary>
    public const string RootEnvVar = "YNET_SCHED_ROOT";

    /// <summary>
    /// Resolve the board root, in order: explicit argument, <c>YNET_SCHED_ROOT</c>, then the
    /// configured value. REFUSES rather than inventing one.
    /// </summary>
    public static string Resolve(string? explicitRoot, string? configuredRoot)
    {
        foreach (var candidate in new[] { explicitRoot,
                                          Environment.GetEnvironmentVariable(RootEnvVar),
                                          configuredRoot })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            string root = candidate.Trim();

            if (!Directory.Exists(root))
                throw new BoardRootException(
                    $"board root '{root}' does not exist. Federation attaches to the EXISTING board; " +
                    $"it does not create a second one.");

            if (!File.Exists(Path.Combine(root, RootMarker)))
                throw new BoardRootException(
                    $"'{root}' is not a board root — no {RootMarker} in it. Creating one here would " +
                    $"produce a new empty board that shares nothing with the lanes, which is the " +
                    $"second-oracle failure this refuses to cause.");

            return root;
        }

        throw new BoardRootException(
            $"no board root configured. Set `board_root` in the federation config, or {RootEnvVar}. " +
            $"On this estate the scheduler board is the coop sched root (e.g. D:\\coop\\buildkit\\sched).");
    }

    /// <summary>This actor's op-log directory inside the board root: <c>&lt;root&gt;/ops/&lt;actor&gt;</c>.</summary>
    public static string ActorDirectory(string root, string actor) =>
        Path.Combine(root, OpsKind, actor);

    /// <summary>
    /// The actor's CURRENT log segment, matching the existing <c>&lt;actor&gt;-ops-NNNNNN.jsonl</c>
    /// convention: the highest-numbered existing segment, or <c>000001</c> when there is none.
    /// Federation APPENDS to the lane's own live segment; it never starts a parallel file.
    /// </summary>
    public static string ActorLogPath(string root, string actor)
    {
        string dir = ActorDirectory(root, actor);
        if (Directory.Exists(dir))
        {
            string? newest = Directory.EnumerateFiles(dir, $"{actor}-{OpsKind}-*.jsonl")
                                      .OrderBy(p => p, StringComparer.Ordinal)
                                      .LastOrDefault();
            if (newest is not null) return newest;
        }
        return Path.Combine(dir, $"{actor}-{OpsKind}-000001.jsonl");
    }

    /// <summary>
    /// Every actor log under the root — the WHOLE board, which is what a fold has to be built from.
    /// Reading only this host's own segment would fold a board consisting of this host's own
    /// operations, and report it as the board.
    /// </summary>
    public static IReadOnlyList<string> AllActorLogs(string root)
    {
        string opsDir = Path.Combine(root, OpsKind);
        if (!Directory.Exists(opsDir)) return Array.Empty<string>();
        return Directory.EnumerateDirectories(opsDir)
                        .SelectMany(d => Directory.EnumerateFiles(d, $"*-{OpsKind}-*.jsonl"))
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .ToList();
    }
}
