using Ynet.Transport.Capability;

namespace Ynet.Transport.Path;

public enum PathPhase { Establishing, Live, TearingDown, Unreachable }

/// <summary>
/// Path lifecycle (data-model §"State transitions (Path)"). Revocation is fail-safe: block new
/// selection immediately, tear down live paths at the next frame boundary (research R3), surfacing
/// AuthorizedButUnreachable. REAL + TESTED (supports T021/T032). Enforces the legal transitions:
///   Establishing -> Live | Unreachable
///   Live -> TearingDown
///   TearingDown -> Unreachable
/// </summary>
public sealed class PathState
{
    public PathPhase Phase { get; private set; } = PathPhase.Establishing;
    public PathType Type { get; private set; }

    public void Established(PathType type)
    {
        Require(PathPhase.Establishing);
        Type = type;
        Phase = PathPhase.Live;
    }

    /// <summary>No direct punch and no admitted relay (FR-018).</summary>
    public void MarkUnreachableFromEstablishing()
    {
        Require(PathPhase.Establishing);
        Phase = PathPhase.Unreachable;
    }

    /// <summary>Relay revoked / seal invalidated — begin graceful drain (next frame boundary).</summary>
    public void BeginTearDown()
    {
        Require(PathPhase.Live);
        Phase = PathPhase.TearingDown;
    }

    /// <summary>Drain complete — path is now unreachable; caller re-paths (AuthorizedButUnreachable).</summary>
    public void TearDownComplete()
    {
        Require(PathPhase.TearingDown);
        Phase = PathPhase.Unreachable;
    }

    private void Require(PathPhase expected)
    {
        if (Phase != expected)
            throw new InvalidOperationException($"illegal path transition from {Phase} (expected {expected}).");
    }
}
