// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Configuration and its validation (feature 102, T008).
//
// Contract federation-config.md G1..G3, G6 / data-model I-29..I-32 / FR-002, FR-003, FR-004, FR-026.
//
// DEFAULTS ARE THE SAFE STATE: enabled=false, peers=[]. A host that has never been configured
// federates with nobody and serves its own lanes normally — federation is never on the local
// critical path (FR-004).
//
// EVERY REFUSAL NAMES THE FIELD AND THE REASON. "Invalid config" is not a reason; it is the same
// unhelpful generic that FR-008 and FR-023 exist to stamp out one layer up.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>A configured peer, as it appears in config.json (addresses as literal text).</summary>
public sealed record PeerConfig
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("node_id")] public string NodeId { get; init; } = "";
    [JsonPropertyName("endpoints")] public List<string> Endpoints { get; init; } = new();
    [JsonPropertyName("pin")] public string Pin { get; init; } = "";
}

/// <summary>The operator's surface. Readable back for verification (FR-002).</summary>
public sealed record FederationConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("bind_address")] public string BindAddress { get; init; } = "0.0.0.0";
    [JsonPropertyName("bind_port")] public int BindPort { get; init; } = 47890;
    [JsonPropertyName("space_id")] public string SpaceId { get; init; } = "";
    [JsonPropertyName("identity_path")] public string IdentityPath { get; init; } = "";
    [JsonPropertyName("push_on_append")] public bool PushOnAppend { get; init; } = true;
    [JsonPropertyName("pull_interval_seconds")] public int PullIntervalSeconds { get; init; } = 60;
    [JsonPropertyName("peers")] public List<PeerConfig> Peers { get; init; } = new();

    /// <summary>The default federation port, authorised for a scoped inbound rule by ruling Q-GLPNETG27-04.</summary>
    public const int DefaultPort = 47890;

    /// <summary>
    /// Validate. Returns the list of refusals — empty means conformant. Refusals are RETURNED
    /// rather than thrown so that `config show` can print every problem at once instead of making
    /// the operator rediscover them one exception at a time.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (Enabled)
        {
            // A loopback bind is THE failure mode that looks exactly like success: everything comes
            // up green and no peer can ever reach you (FR-001 / I-31).
            if (IPAddress.TryParse(BindAddress, out var addr) && IPAddress.IsLoopback(addr))
                problems.Add("bind_address: loopback bind is not peer-reachable — a listener bound to loopback looks healthy and admits nobody");

            if (string.IsNullOrWhiteSpace(SpaceId))
                problems.Add("space_id: empty — an unminted space cannot order anything (FR-026)");
            else if (TermSpaceRegistry.LooksClockDerived(SpaceId))
                problems.Add($"space_id: '{SpaceId}' looks clock-derived — this is exactly how the fossil term was born (FR-015)");
        }

        if (BindPort is < 1 or > 65535)
            problems.Add($"bind_port: {BindPort} is out of range");

        if (PullIntervalSeconds < 1)
            problems.Add($"pull_interval_seconds: {PullIntervalSeconds} — the reconciliation pull is the only repair path for an op lost to a dropped link (FR-028)");

        var seenNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Peers)
        {
            if (string.IsNullOrWhiteSpace(p.NodeId))
            {
                problems.Add($"peers[{p.Name}].node_id: missing — a participant is identified by node id, never by address (FR-007)");
                continue;
            }
            if (!seenNodeIds.Add(p.NodeId))
                problems.Add($"peers[{p.Name}].node_id: duplicate '{p.NodeId}' — one participant, one entry (FR-007)");

            foreach (var ep in p.Endpoints)
            {
                // A hostname is a WARNING, not a refusal: names on this estate resolve to fe80::
                // link-local only, so a dial by name fails for a reason that is not QUIC and gets
                // misread as a transport failure (FR-003).
                var host = ep.Contains(':') ? ep[..ep.LastIndexOf(':')] : ep;
                if (!IPAddress.TryParse(host, out _))
                    problems.Add($"peers[{p.Name}].endpoints: '{ep}' is not a literal address — names on this estate resolve to link-local only; use a literal IPv4 address (FR-003)");
            }
        }

        return problems;
    }

    /// <summary>True iff there are no refusals. Warnings about hostnames are refusals here by design.</summary>
    public bool IsValid => Validate().Count == 0;

    /// <summary>Build the runtime peer set from configuration.</summary>
    public PeerSet ToPeerSet()
    {
        var set = new PeerSet();
        foreach (var p in Peers)
        {
            var eps = new List<IPEndPoint>();
            foreach (var e in p.Endpoints)
                if (IPEndPoint.TryParse(e, out var ip)) eps.Add(ip);
            set.Add(new PeerEntry(p.Name, p.NodeId, eps, p.Pin));
        }
        return set;
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>The default per-host config path, OUTSIDE the repo (it carries host-specific data).</summary>
    public static string DefaultPath()
    {
        string root = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
              ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(root, "ynet", "federation", "config.json");
    }

    /// <summary>
    /// Load, or return defaults when absent. An absent config is not an error: it is a host that has
    /// not been configured, which federates with nobody and serves its lanes normally (FR-004).
    /// </summary>
    public static FederationConfig Load(string? path = null)
    {
        path ??= DefaultPath();
        if (!File.Exists(path)) return new FederationConfig();
        return JsonSerializer.Deserialize<FederationConfig>(File.ReadAllText(path), Json)
               ?? new FederationConfig();
    }

    /// <summary>Persist, creating the directory. Callers record the reversal separately (FR-025).</summary>
    public void Save(string? path = null)
    {
        path ??= DefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }

    /// <summary>
    /// The EFFECTIVE configuration as text — what the service will actually use, not the file's
    /// literal bytes. The gap between those two is where configuration bugs live (contract G2).
    /// </summary>
    public string RenderEffective()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"enabled               : {Enabled}");
        sb.AppendLine($"bind                  : {BindAddress}:{BindPort}");
        sb.AppendLine($"space_id              : {(string.IsNullOrWhiteSpace(SpaceId) ? "(unset)" : SpaceId)}");
        sb.AppendLine($"push_on_append        : {PushOnAppend}");
        sb.AppendLine($"pull_interval_seconds : {PullIntervalSeconds}");
        sb.AppendLine($"peers                 : {Peers.Count} participant(s)");
        foreach (var p in Peers)
            sb.AppendLine($"  - {p.Name} [{p.NodeId}] {string.Join(", ", p.Endpoints)}");
        var problems = Validate();
        sb.AppendLine(problems.Count == 0 ? "validation            : OK" : "validation            : REFUSED");
        foreach (var pr in problems) sb.AppendLine($"  ! {pr}");
        return sb.ToString();
    }
}
