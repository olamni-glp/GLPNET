namespace Ynet.Transport.HolePunch;

/// <summary>ICE candidate kinds (RFC 8445), in decreasing type preference.</summary>
public enum CandidateType { Host = 126, ServerReflexive = 100, Relayed = 0 }

/// <summary>
/// An ICE transport candidate (a reachable address a peer might use). <see cref="Priority"/> is the
/// RFC 8445 formula so both agents order pairs identically.
/// </summary>
public readonly record struct Candidate(CandidateType Type, string Address, int Port)
{
    /// <summary>RFC 8445 §5.1.2.1: (2^24)·type-pref + (2^8)·local-pref + (256 − component-id).</summary>
    public uint Priority(int localPref = 65535, int componentId = 1)
        => ((uint)Type << 24) + ((uint)localPref << 8) + (uint)(256 - componentId);
}

/// <summary>A local⇄remote candidate pairing, prioritized per RFC 8445 §6.1.2.3.</summary>
public readonly record struct CandidatePair(Candidate Local, Candidate Remote, bool ControllingIsLocal)
{
    /// <summary>2^32·min(G,D) + 2·max(G,D) + (G>D?1:0), G = controlling priority, D = controlled.</summary>
    public ulong Priority()
    {
        ulong g = ControllingIsLocal ? Local.Priority() : Remote.Priority();
        ulong d = ControllingIsLocal ? Remote.Priority() : Local.Priority();
        return (Math.Min(g, d) << 32) + (2UL * Math.Max(g, d)) + (g > d ? 1UL : 0UL);
    }
}

/// <summary>The ICE role: the controlling agent nominates the pair (DCUtR: the initiator).</summary>
public enum IceRole { Controlling, Controlled }

/// <summary>
/// Drives a single coordinated simultaneous-open attempt against one candidate pair (DCUtR
/// §"hole punching"). The REAL implementation binds a UDP socket and fires synchronized packets at
/// <paramref name="fireAt"/>; the in-process NAT simulation resolves the same call against a NAT
/// model. Returns a live channel token on a successful punch, or null when the hole did not open.
/// </summary>
public interface IPunchProbe
{
    /// <summary>Attempt to open <paramref name="pair"/> at the coordinated instant; null ⇒ no hole.</summary>
    PunchedPath? TryOpen(CandidatePair pair, TimeSpan fireAt);
}

/// <summary>A successfully punched direct path: the nominated pair + an opaque channel token.</summary>
public readonly record struct PunchedPath(CandidatePair Pair, object Channel);

/// <summary>
/// ICE/DCUtR agent (T018, FR-005; absorbs the iroh/libp2p model — invents no new mechanism): given
/// exchanged candidate sets it forms prioritized pairs and runs a DCUtR coordinated simultaneous
/// open — the controlling agent measures RTT and schedules both sides to fire at t≈RTT/2 so packets
/// cross in flight and each side's NAT sees an outbound-first packet (the precondition for an
/// endpoint-independent mapping to admit the peer). REAL + TESTED over the NAT simulation; the UDP
/// socket is the <see cref="IPunchProbe"/> seam.
/// </summary>
public sealed class IceDcutrAgent
{
    private readonly IceRole _role;
    private readonly IPunchProbe _probe;

    public IceDcutrAgent(IceRole role, IPunchProbe probe)
    {
        _role = role;
        _probe = probe;
    }

    /// <summary>The ordered candidate pairs this attempt will try (highest priority first).</summary>
    public IReadOnlyList<CandidatePair> FormCheckList(IReadOnlyList<Candidate> local, IReadOnlyList<Candidate> remote)
    {
        bool controlling = _role == IceRole.Controlling;
        return (from l in local
                from r in remote
                select new CandidatePair(l, r, controlling))
            .OrderByDescending(p => p.Priority())
            .ToList();
    }

    /// <summary>
    /// Run the check list within <paramref name="rtt"/>-coordinated timing. Returns the first pair
    /// whose coordinated open succeeds (the nominated pair) or null when no pair punches through.
    /// <paramref name="hasTimeLeft"/> (the caller's ≤5 s budget, T021) is polled before each check —
    /// once it returns false the run stops so the orchestrator can fall back to a relay deterministically.
    /// </summary>
    public PunchedPath? Punch(
        IReadOnlyList<Candidate> local, IReadOnlyList<Candidate> remote, TimeSpan rtt, Func<bool>? hasTimeLeft = null)
    {
        var fireAt = TimeSpan.FromTicks(rtt.Ticks / 2); // DCUtR: fire at ~RTT/2 so packets cross
        foreach (var pair in FormCheckList(local, remote))
        {
            if (hasTimeLeft is not null && !hasTimeLeft()) break; // budget exhausted → relay fallback
            var punched = _probe.TryOpen(pair, fireAt);
            if (punched is { } p) return p;
        }
        return null;
    }
}
