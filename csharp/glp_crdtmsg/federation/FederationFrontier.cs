// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The hole-preserving causal frontier (feature 102, codex round-2 finding
// `preserve-holes-in-the-reconciliation-frontier`).
//
// Contract federation-wire.md W5 / FR-028.
//
// WHY THIS EXISTS RATHER THAN Crdt.VersionVector.
//
// VersionVector documents itself as "per-peer highest CONTIGUOUSLY-known counter" but `With` is a
// plain max-merge. Feed it 5 then 7 and it reports 7 — and `Contains(6)` then answers TRUE for an
// operation that was never delivered. On the pull leg that answer is not merely wrong, it is
// ABSORBING: this host advertises a frontier claiming 6, the peer computes "nothing missing below
// 7", and operation 6 is suppressed for the life of the board. The reconciliation pull is the ONLY
// repair path for an op lost to a dropped link (FR-028), so a frontier that cannot express a hole
// converts a transient loss into a permanent divergence — silently, on both sides, with every
// status surface reporting healthy.
//
// VersionVector is shared CRDT code with other callers; it is reused UNCHANGED elsewhere and is not
// modified here. This type is federation-local and says what it means: a contiguous prefix, plus the
// dots seen ABOVE that prefix. `Contains` is then exact rather than optimistic.
//
// The prefix is not an optimisation, it is the bound: without it a long-lived board's frontier is
// the whole op set and the "frontier first, never the whole log" property of W5 is lost.

using System.Text;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>
/// What this host has actually seen, per originating peer: a contiguous run <c>1..Contiguous</c>
/// plus the individual counters seen above it. A gap is REPRESENTABLE, which is the whole point.
/// </summary>
public sealed class FederationFrontier
{
    private readonly Dictionary<string, long> _contiguous;
    private readonly Dictionary<string, SortedSet<long>> _above;

    public FederationFrontier()
    {
        _contiguous = new Dictionary<string, long>(StringComparer.Ordinal);
        _above = new Dictionary<string, SortedSet<long>>(StringComparer.Ordinal);
    }

    private FederationFrontier(Dictionary<string, long> contiguous, Dictionary<string, SortedSet<long>> above)
    {
        _contiguous = contiguous;
        _above = above;
    }

    /// <summary>Every peer this frontier knows anything about, in ordinal order (canonical output).</summary>
    public IEnumerable<string> Peers =>
        _contiguous.Keys.Concat(_above.Keys).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal);

    /// <summary>The highest counter below which EVERY counter has been seen. 0 when nothing has.</summary>
    public long ContiguousUpTo(string peer) => _contiguous.TryGetValue(peer, out var v) ? v : 0;

    /// <summary>Counters seen above the contiguous run — the other side of a hole.</summary>
    public IReadOnlyCollection<long> Above(string peer) =>
        _above.TryGetValue(peer, out var s) ? s : (IReadOnlyCollection<long>)Array.Empty<long>();

    /// <summary>
    /// Exact membership. Never optimistic: a counter inside a gap answers FALSE, so the peer sends
    /// it and the hole is repaired.
    /// </summary>
    public bool Contains(Dot dot) =>
        dot.Counter <= ContiguousUpTo(dot.PeerName)
        || (_above.TryGetValue(dot.PeerName, out var s) && s.Contains(dot.Counter));

    /// <summary>
    /// A copy that has seen <paramref name="dot"/>. Adding the counter that closes a gap absorbs the
    /// run above it into the contiguous prefix, so the frontier stays bounded on a healthy link.
    /// </summary>
    public FederationFrontier With(Dot dot)
    {
        var contiguous = new Dictionary<string, long>(_contiguous, StringComparer.Ordinal);
        var above = new Dictionary<string, SortedSet<long>>(StringComparer.Ordinal);
        foreach (var kv in _above) above[kv.Key] = new SortedSet<long>(kv.Value);

        long run = contiguous.TryGetValue(dot.PeerName, out var c) ? c : 0;
        if (dot.Counter <= run) return new FederationFrontier(contiguous, above);

        if (!above.TryGetValue(dot.PeerName, out var set))
            above[dot.PeerName] = set = new SortedSet<long>();
        set.Add(dot.Counter);

        // Absorb: while the very next counter is present above the run, extend the run over it.
        while (set.Contains(run + 1))
        {
            run++;
            set.Remove(run);
        }

        contiguous[dot.PeerName] = run;
        if (set.Count == 0) above.Remove(dot.PeerName);
        return new FederationFrontier(contiguous, above);
    }

    /// <summary>
    /// Canonical JSON: peers in ordinal order, each as <c>{"c":&lt;run&gt;,"a":[…]}</c>. Two hosts
    /// holding the same knowledge emit identical bytes, so a frontier can be compared by content.
    /// </summary>
    public string ToCanonicalJson()
    {
        var sb = new StringBuilder("{");
        bool first = true;
        foreach (var p in Peers)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append(JsonSerializer.Serialize(p)).Append(":{\"c\":").Append(ContiguousUpTo(p)).Append(",\"a\":[");
            sb.Append(string.Join(",", Above(p)));
            sb.Append("]}");
        }
        return sb.Append('}').ToString();
    }

    /// <summary>
    /// Parse the canonical form. Tolerates the older bare-number shape (<c>"peer":N</c>) by reading
    /// it as a contiguous run of N — a peer still speaking the old encoding degrades to the previous
    /// behaviour for its OWN advertisement rather than failing the exchange outright.
    /// </summary>
    public static FederationFrontier FromCanonicalJson(string json)
    {
        var f = new FederationFrontier();
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number)
            {
                f._contiguous[prop.Name] = prop.Value.GetInt64();
                continue;
            }
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;

            if (prop.Value.TryGetProperty("c", out var c) && c.ValueKind == JsonValueKind.Number)
                f._contiguous[prop.Name] = c.GetInt64();

            if (prop.Value.TryGetProperty("a", out var a) && a.ValueKind == JsonValueKind.Array)
            {
                var set = new SortedSet<long>();
                foreach (var el in a.EnumerateArray())
                    if (el.ValueKind == JsonValueKind.Number) set.Add(el.GetInt64());
                if (set.Count > 0) f._above[prop.Name] = set;
            }
        }
        return f;
    }
}
