// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Ruling Q-glpnetshiras-47 (2026-09-05): "build the cause-independent loud-failure guard AND the
// forensic record".
//
// WHY THIS FILE EXISTS. Feature 102 persisted this lane's node key and proved it across THREE
// separate OS processes — Minted, Loaded, Loaded — then reported it as "stable across reboots".
// Three processes inside ONE BOOT test the FILE. They do not test DURABILITY. The first measurement
// taken after an actual reboot (2026-09-05T09:06Z) found the key ABSENT and minted a new id:
// 76b66c25... became c8c237ea..., and every peer holding the old id was silently wrong.
//
// The cause of that disappearance is UNDETERMINED and this file does not guess at it. It is built
// on the one property that holds whatever the cause was: A LANE'S ID CHANGING MUST BE LOUD.

using SysPath = System.IO.Path;

namespace Ynet.Transport.Capability;

/// <summary>How the lane's current node id compares with the last id it told the fleet about.</summary>
public enum PublicationState
{
    /// <summary>No publication has ever been recorded. First run — nothing to contradict.</summary>
    Unpublished,

    /// <summary>The current id is the id this lane published. Nothing to do.</summary>
    Matches,

    /// <summary>🔴 The current id is NOT the id this lane published. Every peer's pin is stale.</summary>
    Changed,
}

/// <summary>The comparison, plus everything an operator needs to act on it.</summary>
public sealed record PublicationStatus(
    PublicationState State, string CurrentNodeId, string? PublishedNodeId, string RecordPath)
{
    /// <summary>True when peers are holding an id this lane no longer has.</summary>
    public bool RequiresRepublication => State == PublicationState.Changed;

    /// <summary>A message that names both ids and says what to do. Null when there is nothing wrong.</summary>
    public string? Report => State != PublicationState.Changed ? null :
        "🔴 THIS LANE'S NODE ID HAS CHANGED SINCE IT WAS LAST PUBLISHED.\n"
        + $"   published : {PublishedNodeId}\n"
        + $"   current   : {CurrentNodeId}\n"
        + "   Every peer holding the published id is now wrong about this lane, and will refuse or\n"
        + "   mis-route. RE-PUBLISH the current id to the fleet, then record it with\n"
        + "   NodeIdentity.RecordPublication(...). This is reported rather than repaired because\n"
        + "   only the fleet can decide whether to adopt the new id or restore the old key.\n"
        + $"   record: {RecordPath}";
}

public sealed partial class NodeIdentity
{
    /// <summary>Compare this lane's current id against the last one it recorded as published.</summary>
    /// <remarks>
    /// 🔴 <b>Cause-independent by design.</b> It does not care whether the key was deleted, moved,
    /// re-minted after corruption, or written to a different directory: it compares the id the lane
    /// HAS with the id the fleet was TOLD. That covers the measured reboot loss and every mechanism
    /// that was hypothesised for it and not confirmed.
    /// </remarks>
    public static PublicationStatus CheckPublication(
        string laneName, string currentNodeId, string? keystorePath = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(laneName);
        ArgumentException.ThrowIfNullOrEmpty(currentNodeId);

        var path = PublicationRecordPath(laneName, keystorePath);
        if (!File.Exists(path))
            return new PublicationStatus(PublicationState.Unpublished, currentNodeId, null, path);

        var published = File.ReadAllText(path).Trim();
        if (published.Length == 0)
            return new PublicationStatus(PublicationState.Unpublished, currentNodeId, null, path);

        return new PublicationStatus(
            string.Equals(published, currentNodeId, StringComparison.OrdinalIgnoreCase)
                ? PublicationState.Matches
                : PublicationState.Changed,
            currentNodeId, published, path);
    }

    /// <summary>
    /// Record that <paramref name="nodeId"/> has been published to the fleet. Call this AFTER the
    /// publication actually happened — recording it first would make a failed publication look
    /// successful, which is the failure mode this guard exists to catch.
    /// </summary>
    public static void RecordPublication(string laneName, string nodeId, string? keystorePath = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(laneName);
        ArgumentException.ThrowIfNullOrEmpty(nodeId);

        var path = PublicationRecordPath(laneName, keystorePath);
        Directory.CreateDirectory(SysPath.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
        File.WriteAllText(temp, nodeId + Environment.NewLine);
        File.Move(temp, path, overwrite: true);   // single-writer by construction: not a claim
    }

    private static string PublicationRecordPath(string laneName, string? keystorePath)
        => SysPath.Combine(ResolveKeystoreDir(keystorePath), RequireLaneStem(laneName) + ".published");

    /// <summary>
    /// Append one forensic line every time a key is MINTED. The reboot loss could not be explained
    /// because nothing recorded the circumstances of the mint that replaced it; this is the record
    /// that will explain the next one. Append-only, best-effort, and never able to fail a mint —
    /// a diagnostic that can break the thing it observes is worse than no diagnostic.
    /// </summary>
    internal static void AppendMintAudit(string laneName, string keyPath, IdentityOrigin origin)
    {
        try
        {
            var dir = SysPath.GetDirectoryName(keyPath)!;
            var line = string.Join(" | ",
                DateTimeOffset.UtcNow.ToString("O"),
                "lane=" + laneName,
                "origin=" + origin,
                "key=" + keyPath,
                "dir_existed=" + Directory.Exists(dir),
                "env_" + KeystoreEnvVar + "=" + (Environment.GetEnvironmentVariable(KeystoreEnvVar) ?? "<unset>"),
                "localappdata=" + (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) is { Length: > 0 } l ? l : "<empty>"),
                "cwd=" + Environment.CurrentDirectory,
                "pid=" + Environment.ProcessId,
                "machine=" + Environment.MachineName);
            File.AppendAllText(SysPath.Combine(dir, "mint-audit.log"), line + Environment.NewLine);
        }
        catch (IOException) { /* best effort: never fail a mint for a diagnostic */ }
        catch (UnauthorizedAccessException) { }
    }
}
