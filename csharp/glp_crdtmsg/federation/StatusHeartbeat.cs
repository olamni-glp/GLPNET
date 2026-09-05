// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The serving process's published status (feature 102, codex round-2 finding
// `read-status-from-the-serving-process`).
//
// Contract federation-status.md S7 / FR-019, FR-020, FR-021.
//
// WHAT WAS WRONG. `serve` and `status` are separate processes — that is what the runbook tells the
// operator to do. `status` only read CONFIGURATION, so while federation was genuinely running it
// reported `listener bound: No` and could not see an admitted peer or a crossed operation at all.
// The runbook's own expected output was unreachable by the runbook's own procedure. Worse, it
// reported the load-bearing states as measured NEGATIVES when it had not measured them — the exact
// FR-021 violation ("a state that could not be measured MUST be reported as unknown, never as a
// negative result") that this era exists to remove.
//
// SO THE SERVING PROCESS PUBLISHES. It writes its measured status to a file beside the config, on
// bind and on every change, and refreshes it on a timer. `status` reads it.
//
// STALENESS IS THE POINT, NOT AN AFTERTHOUGHT. A file written by a process that has since been
// killed is a MEASUREMENT THAT NO LONGER HOLDS. Reading it as current is precisely how a dead
// daemon reports itself healthy. So the record is stamped, and a reader past the freshness window
// reports every live state as UNKNOWN — never as the stale value, and never as No. Absent, stale
// and corrupt all resolve to unknown, and all say so distinctly (SC-010).

using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>The serving process's last published measurement.</summary>
public sealed record StatusHeartbeat
{
    [JsonPropertyName("published_utc")] public DateTimeOffset PublishedUtc { get; init; }
    [JsonPropertyName("pid")] public int Pid { get; init; }
    [JsonPropertyName("listener_bound")] public string ListenerBound { get; init; } = "unknown";
    [JsonPropertyName("bound_endpoint")] public string? BoundEndpoint { get; init; }
    [JsonPropertyName("peer_admitted")] public string PeerAdmitted { get; init; } = "unknown";
    [JsonPropertyName("op_received")] public string OpReceived { get; init; } = "unknown";
    [JsonPropertyName("same_machine")] public string? SameMachine { get; init; }
    [JsonPropertyName("admitted_participants")] public int AdmittedParticipants { get; init; }
    [JsonPropertyName("fold_operations")] public int FoldOperations { get; init; }
    [JsonPropertyName("reasons")] public Dictionary<string, string> Reasons { get; init; } = new();

    /// <summary>
    /// The named host-policy refusal, when the bind was blocked by one (FR-023).
    /// <para>
    /// Carried across the process boundary because `serve` EXITS on this failure telling the
    /// operator to run `status` — and without it that command could only show unknown, so the one
    /// failure this surface exists to name would be the one it could not.
    /// </para>
    /// </summary>
    [JsonPropertyName("policy_refusal")] public PolicyRefusal? PolicyRefused { get; init; }

    /// <summary>
    /// How long a published measurement stays credible. The serving process refreshes well inside
    /// this; a reader outside it treats the record as no measurement at all.
    /// </summary>
    public static readonly TimeSpan Freshness = TimeSpan.FromSeconds(30);

    /// <summary>The default path — beside the config, outside the repo.</summary>
    public static string DefaultPath() =>
        Path.Combine(Path.GetDirectoryName(FederationConfig.DefaultPath())!, "serving-status.json");

    /// <summary>Age at <paramref name="now"/>. Negative ages (clock skew) count as stale, not fresh.</summary>
    public TimeSpan AgeAt(DateTimeOffset now) => now - PublishedUtc;

    /// <summary>True while the measurement still holds.</summary>
    public bool IsFreshAt(DateTimeOffset now)
    {
        var age = AgeAt(now);
        return age >= TimeSpan.Zero && age <= Freshness;
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>
    /// Publish atomically: write a temp file, then replace. A reader must never see a half-written
    /// record and resolve it to "corrupt" while the daemon is perfectly healthy.
    /// </summary>
    public void Publish(string? path = null)
    {
        path ??= DefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Read the published record, or null when there is none, it cannot be parsed, or it is stale.
    /// ALL THREE resolve to null — "I have no current measurement" — and the caller reports unknown.
    /// </summary>
    public static StatusHeartbeat? ReadFresh(DateTimeOffset now, string? path = null)
    {
        path ??= DefaultPath();
        try
        {
            if (!File.Exists(path)) return null;
            var hb = JsonSerializer.Deserialize<StatusHeartbeat>(File.ReadAllText(path));
            if (hb is null) return null;

            // A TERMINAL POLICY REFUSAL DOES NOT EXPIRE.
            //
            // The freshness window exists because a LIVE measurement from a dead process is a lie.
            // A host-policy refusal is the opposite: the daemon published it and then EXITED, by
            // design, telling the operator to run `status` — so nothing will ever refresh it, and
            // discarding it after 30 seconds destroyed the only record of the single failure FR-023
            // exists to name. An operator arriving a minute later saw "unknown" instead.
            //
            // It is still marked as terminal rather than live, so it can never be mistaken for a
            // running daemon.
            if (hb.PolicyRefused is not null) return hb;

            return hb.IsFreshAt(now) ? hb : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupted measurement is reported as unknown, never as a negative result (SC-010).
            return null;
        }
    }

    /// <summary>Build the published record from a live measurement.</summary>
    public static StatusHeartbeat From(FederationStatus s, int foldOperations, DateTimeOffset now) => new()
    {
        PublishedUtc = now,
        Pid = Environment.ProcessId,
        ListenerBound = s.ListenerBound.ToString().ToLowerInvariant(),
        BoundEndpoint = s.BoundEndpoint,
        PeerAdmitted = s.PeerAdmitted.ToString().ToLowerInvariant(),
        OpReceived = s.OpReceivedFromPeer.ToString().ToLowerInvariant(),
        SameMachine = s.SameMachine?.ToString().ToLowerInvariant(),
        AdmittedParticipants = s.AdmittedParticipants,
        FoldOperations = foldOperations,
        PolicyRefused = s.PolicyRefused,
        Reasons = new Dictionary<string, string>(s.Reasons),
    };

    /// <summary>Rebuild a <see cref="FederationStatus"/> from the published record, for the reader.</summary>
    public FederationStatus ToStatus(Tri stackSupported) => new()
    {
        StackSupported = stackSupported,
        ListenerBound = ParseTri(ListenerBound),
        PeerAdmitted = ParseTri(PeerAdmitted),
        OpReceivedFromPeer = ParseTri(OpReceived),
        SameMachine = SameMachine is null ? null : ParseTri(SameMachine),
        BoundEndpoint = BoundEndpoint,
        AdmittedParticipants = AdmittedParticipants,
        PolicyRefused = PolicyRefused,
        Reasons = new Dictionary<string, string>(Reasons),
    };

    private static Tri ParseTri(string s) => s switch
    {
        "yes" => Tri.Yes,
        "no" => Tri.No,
        _ => Tri.Unknown,
    };
}
