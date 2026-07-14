using Ynet.Transport.Capability;

namespace Ynet.Transport.HolePunch;

/// <summary>Gathers this node's local ICE candidates (host/srflx/relay). UDP+STUN in prod; the NAT
/// simulation supplies them in tests.</summary>
public interface ICandidateGatherer
{
    IReadOnlyList<Candidate> Gather(NodeId self);
}

/// <summary>A monotonic clock for the punch budget (real = Stopwatch-backed; sim = controllable).</summary>
public interface IPunchClock
{
    TimeSpan Now { get; }
}

/// <summary>
/// Deterministic relay fallback (T021, FR-005/FR-007): when no direct punch succeeds within budget,
/// establish a relay path through a 056-ADMITTED relay. The admission decision itself is 056's
/// (enforced by Relay/RelayPolicy.cs); this seam only opens the path once admitted. Returns a not-ok
/// result when no admitted relay exists (→ Unreachable, FR-018).
/// </summary>
public interface IRelayFallback
{
    RelayResult Open(NodeId peer);
}

/// <summary>Result of a relay-fallback attempt: an opaque channel token, or not-ok (no admitted relay).</summary>
public readonly record struct RelayResult(bool Ok, object? Channel)
{
    public static RelayResult Relayed(object channel) => new(true, channel);
    public static RelayResult None => new(false, null);
}

/// <summary>
/// The outcome of establishing a path (FR-005/FR-018): a distinct path type (direct|relayed) with a
/// live channel token, OR a distinct refusal — never a silent drop.
/// </summary>
public readonly record struct PunchOutcome(PathType? PathType, RefusalReason? Refusal, object? Channel)
{
    public bool Ok => PathType.HasValue;
    public static PunchOutcome Direct(object channel) => new(Capability.PathType.Direct, null, channel);
    public static PunchOutcome Relayed(object channel) => new(Capability.PathType.Relayed, null, channel);
    public static PunchOutcome Refuse(RefusalReason reason) => new(null, reason, null);
}

/// <summary>
/// Coordinates NAT traversal end-to-end (T021, FR-005/FR-018): gather candidates → rendezvous to
/// exchange them → attempt an ICE/DCUtR coordinated open under a BOUNDED budget (≤5 s) → on failure,
/// fall back DETERMINISTICALLY to an admitted relay, surfacing whether the active path is direct or
/// relayed. When neither a direct punch nor an admitted relay yields a path, it returns the distinct
/// <see cref="RefusalReason.Unreachable"/> (FR-018) — traffic is never silently dropped. REAL +
/// TESTED over the NAT simulation; the UDP/STUN sockets are the injected seams.
/// </summary>
public sealed class PunchOrchestrator
{
    /// <summary>Bounded punch budget before deterministic relay fallback (research R1).</summary>
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(5);

    private readonly ICandidateGatherer _gatherer;
    private readonly RendezvousService _rendezvous;
    private readonly IPunchProbe _probe;
    private readonly IRelayFallback _relay;
    private readonly IPunchClock _clock;

    public PunchOrchestrator(
        ICandidateGatherer gatherer, RendezvousService rendezvous, IPunchProbe probe,
        IRelayFallback relay, IPunchClock clock)
    {
        _gatherer = gatherer;
        _rendezvous = rendezvous;
        _probe = probe;
        _relay = relay;
        _clock = clock;
    }

    /// <summary>
    /// Establish a path from <paramref name="self"/> to <paramref name="peer"/>. Publishes our own
    /// candidates for the mutual punch, resolves the peer's, attempts a direct punch within
    /// <paramref name="budget"/>, and falls back to a relay on miss.
    /// </summary>
    public PunchOutcome Establish(
        NodeIdentity self, NodeId peer, RendezvousMode mode, DateTimeOffset now,
        IceRole role = IceRole.Controlling, TimeSpan? budget = null, TimeSpan? rtt = null)
    {
        var span = budget ?? DefaultBudget;
        var round = rtt ?? TimeSpan.FromMilliseconds(40);

        var local = _gatherer.Gather(self.NodeId);
        _rendezvous.Publish(self, local, mode, now); // symmetric: let the peer resolve us too

        var advert = _rendezvous.Resolve(peer, mode, now);
        var start = _clock.Now;

        if (advert is { } peerAdvert)
        {
            var agent = new IceDcutrAgent(role, _probe);
            var punched = agent.Punch(
                local, peerAdvert.Candidates, round,
                hasTimeLeft: () => _clock.Now - start < span);
            if (punched is { } direct)
                return PunchOutcome.Direct(direct.Channel);
        }

        // No direct path within budget (or peer unresolvable directly) → deterministic relay fallback.
        var relay = _relay.Open(peer);
        return relay.Ok
            ? PunchOutcome.Relayed(relay.Channel!)
            : PunchOutcome.Refuse(RefusalReason.Unreachable); // FR-018: distinct, never a silent drop
    }
}
