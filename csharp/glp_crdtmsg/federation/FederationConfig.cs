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

    /// <summary>
    /// The transport pin: base64(SHA-256(SPKI)). NOT the node id, which is the same bytes in hex.
    /// Left empty it is DERIVED from <see cref="NodeId"/>, which is the only correct relationship
    /// between the two and removes the operator's opportunity to get it wrong.
    /// </summary>
    [JsonPropertyName("pin")] public string Pin { get; init; } = "";

    /// <summary>
    /// The peer's published base64 SubjectPublicKeyInfo, for verifying its operation signatures.
    /// Optional: without it the peer's ops are <c>UnverifiedOrigin</c>, never assumed valid.
    /// </summary>
    [JsonPropertyName("spki")] public string Spki { get; init; } = "";

    /// <summary>The effective pin — configured, or derived from the node id when absent.</summary>
    public string EffectivePin =>
        !string.IsNullOrWhiteSpace(Pin) ? Pin
        : NodeIdentityStore.IsNodeId(NodeId) ? NodeIdentityStore.PinFromNodeId(NodeId)
        : "";
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

    /// <summary>
    /// The EXISTING scheduler board root federation attaches to (e.g. <c>D:\coop\buildkit\sched</c>).
    /// Federation reads and appends here; it does not create a board of its own. Empty means
    /// unconfigured, and `serve` refuses rather than inventing a second board.
    /// </summary>
    [JsonPropertyName("board_root")] public string BoardRootPath { get; init; } = "";

    /// <summary>
    /// The actor name whose op-log this host appends to — the lane identity on the board, which is
    /// NOT the node id (that is the transport identity).
    /// </summary>
    [JsonPropertyName("board_actor")] public string BoardActor { get; init; } = "";

    /// <summary>
    /// When true (THE DEFAULT, engineer ruling 2026-09-04), federated operations are appended into
    /// the lane's own <c>ops</c> segment, where every existing scheduler reader already looks.
    /// <para>
    /// This was off while the interop question was open, and the consequence was the finding two
    /// consecutive review rounds ranked first: a host could ACK a claim it had folded while the lane
    /// ON THAT HOST still could not see it. Federation that converges a board nobody reads is the
    /// second oracle in a different costume. The engineer ruled to accept the interop risk, because
    /// it is the only option that delivers a single truth board.
    /// </para>
    /// <para>
    /// Set it false to restore the federation-owned <c>fedops</c> kind if a scheduler reader turns
    /// out to be strict about line shape.
    /// </para>
    /// </summary>
    [JsonPropertyName("write_into_lane_segment")] public bool WriteIntoLaneSegment { get; init; } = true;

    /// <summary>
    /// When true, an operation whose origin cannot be cryptographically verified is refused rather
    /// than folded-and-counted. Off until peers have published their keys, so that turning it on is
    /// a deliberate tightening rather than a silent outage.
    /// </summary>
    [JsonPropertyName("require_verified_attribution")] public bool RequireVerifiedAttribution { get; init; }

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

            // Without a board root, federation has nothing to attach to — and the previous fallback
            // (a private file under the config directory) silently produced a SECOND board that the
            // lanes never read. Refusing is the only safe answer.
            if (string.IsNullOrWhiteSpace(BoardRootPath))
                problems.Add("board_root: empty — federation attaches to the EXISTING board; without a root it would converge a second, invisible one");

            if (string.IsNullOrWhiteSpace(BoardActor))
            {
                problems.Add("board_actor: empty — an operation must be appended to a named actor's log to be attributable on the board (FR-009)");
            }
            else if (BoardActor.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                     || BoardActor.Contains("..", StringComparison.Ordinal)
                     || Path.IsPathRooted(BoardActor)
                     || BoardActor.Contains('/') || BoardActor.Contains('\\'))
            {
                // It is used as a PATH SEGMENT. A rooted value or one containing traversal resolves
                // the write path OUTSIDE the validated board root — which creates, by typo or by
                // hostile config, exactly the second-board condition this feature exists to prevent.
                problems.Add($"board_actor: '{BoardActor}' is not a single safe path segment — it names a directory under the board root, so it must not be rooted or contain separators or '..'");
            }

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

            // The node id is SHA-256(SPKI) in hex. A value of any other shape cannot produce a pin,
            // so the peer would be refused at the TLS callback for a reason that presents as a pin
            // mismatch — a configuration fault reported as a security event.
            if (!NodeIdentityStore.IsNodeId(p.NodeId))
            {
                problems.Add($"peers[{p.Name}].node_id: '{p.NodeId}' is not 64 hex characters — a node id is SHA-256(SPKI) in hex (FR-007)");
            }
            else if (!string.IsNullOrWhiteSpace(p.Pin))
            {
                // A pin given explicitly must be the SAME BYTES as the node id. Silently preferring
                // one over the other is how a peer ends up pinned to a key it does not hold.
                string derived;
                try { derived = NodeIdentityStore.PinFromNodeId(p.NodeId); }
                catch (FormatException) { derived = ""; }
                if (derived.Length > 0 && !string.Equals(derived, p.Pin.Trim(), StringComparison.Ordinal))
                    problems.Add($"peers[{p.Name}].pin: does not match node_id — the pin is base64 of the SAME SHA-256(SPKI) the node id is hex of; leave it empty to derive it");
            }

            // A published key must belong to the identity it is filed under, and this is checkable:
            // SHA-256 of the SPKI must be the node id. Otherwise a wrong key installs quietly and
            // every signature from that peer fails as a forgery.
            if (!string.IsNullOrWhiteSpace(p.Spki))
            {
                string implied;
                try { implied = NodeIdentityStore.NodeIdFromSpki(p.Spki.Trim()); }
                catch (FormatException) { implied = ""; }
                if (implied.Length == 0)
                    problems.Add($"peers[{p.Name}].spki: not valid base64 — expected a base64 SubjectPublicKeyInfo");
                else if (!string.Equals(implied, p.NodeId.Trim(), StringComparison.OrdinalIgnoreCase))
                    problems.Add($"peers[{p.Name}].spki: hashes to '{implied}', not to node_id '{p.NodeId}' — this key does not belong to this participant");
            }

            foreach (var ep in p.Endpoints)
            {
                // A hostname is a WARNING, not a refusal: names on this estate resolve to fe80::
                // link-local only, so a dial by name fails for a reason that is not QUIC and gets
                // misread as a transport failure (FR-003).
                // VALIDATE THE WHOLE ENDPOINT, PORT INCLUDED. Checking only the host half let
                // "192.0.2.1:notaport" pass; ToPeerSet then silently dropped the unparsable entry
                // and the configuration error resurfaced much later as a name-resolution or
                // reachability failure — pointing the operator at the network for a typo.
                if (!IPEndPoint.TryParse(ep, out var parsed) || parsed.Port is < 1 or > 65535)
                {
                    var host = ep.Contains(':') ? ep[..ep.LastIndexOf(':')] : ep;
                    problems.Add(IPAddress.TryParse(host, out _)
                        ? $"peers[{p.Name}].endpoints: '{ep}' has no valid port — an endpoint is <ip>:<port>, and an unparsable one is silently dropped and later misreported as unreachability"
                        : $"peers[{p.Name}].endpoints: '{ep}' is not a literal address — names on this estate resolve to link-local only; use a literal IPv4 address (FR-003)");
                }
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
            set.Add(new PeerEntry(p.Name, p.NodeId, eps, p.EffectivePin,
                                  string.IsNullOrWhiteSpace(p.Spki) ? null : p.Spki.Trim()));
        }
        return set;
    }

    /// <summary>
    /// Where this host's own identity lives: <c>identity_path</c> when configured, else the default.
    /// <para>
    /// Honouring this is not cosmetic. A deployment that pre-provisions a key and is silently given a
    /// freshly-minted one instead federates under an identity no peer has pinned — every peer
    /// refuses it, and the configured setting is inert while appearing effective.
    /// </para>
    /// </summary>
    public string EffectiveIdentityPath =>
        string.IsNullOrWhiteSpace(IdentityPath) ? NodeIdentityStore.DefaultPath() : IdentityPath;

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
        sb.AppendLine($"identity_path         : {EffectiveIdentityPath}{(string.IsNullOrWhiteSpace(IdentityPath) ? " (default)" : " (configured)")}");
        sb.AppendLine($"board_root            : {(string.IsNullOrWhiteSpace(BoardRootPath) ? "(unset - federation will refuse)" : BoardRootPath)}");
        sb.AppendLine($"board_actor           : {(string.IsNullOrWhiteSpace(BoardActor) ? "(unset)" : BoardActor)}");
        sb.AppendLine($"writes into           : {(WriteIntoLaneSegment ? "the lane's own ops/ segment (lanes see federated ops)" : "federation's fedops/ kind (lanes do NOT see federated ops)")}");
        sb.AppendLine($"attribution           : {(RequireVerifiedAttribution ? "VERIFIED signature required" : "unverified origins folded and counted")}");
        sb.AppendLine($"push_on_append        : {PushOnAppend}");
        sb.AppendLine($"pull_interval_seconds : {PullIntervalSeconds}");
        sb.AppendLine($"peers                 : {Peers.Count} participant(s)");
        foreach (var p in Peers)
            sb.AppendLine($"  - {p.Name} [{p.NodeId}] {string.Join(", ", p.Endpoints)}"
                          + $"  key: {(string.IsNullOrWhiteSpace(p.Spki) ? "NOT PUBLISHED (ops unverified)" : "published")}");
        var problems = Validate();
        sb.AppendLine(problems.Count == 0 ? "validation            : OK" : "validation            : REFUSED");
        foreach (var pr in problems) sb.AppendLine($"  ! {pr}");
        return sb.ToString();
    }
}
