using System.Net;
using Ynet.Transport.Link;

namespace Ynet.Transport.Listener;

/// <summary>
/// What actually happened when a named service tried to listen.
/// </summary>
/// <remarks>
/// 🔴 This is deliberately NOT a boolean. A boolean is exactly what allows
/// <see cref="BoundUnreachable"/> to collapse into <see cref="Ok"/> — a socket that opened and
/// receives nothing reported as health. That collapse is the measured cause of
/// "yng-broker RUNNING, no TCP listener, no UDP endpoint" being invisible from inside the process
/// (spec B4/B5), and it is the false-green class this fleet has paid for seven times.
/// </remarks>
public enum ListenerOutcome
{
    /// <summary>Bound AND a peer completed a handshake and exchanged bytes with it.</summary>
    Ok,

    /// <summary>
    /// The socket bound, and nothing could reach it. This is the Windows per-binary inbound
    /// <c>Block</c> case: invisible from inside the process, and it beats a port <c>Allow</c>.
    /// </summary>
    BoundUnreachable,

    /// <summary>A provider was available but the bind itself failed (port in use, permission).</summary>
    BindFailed,

    /// <summary>No provider could serve. The service must not start.</summary>
    Refused,
}

/// <summary>
/// The record of one named service's listener. Everything here is OBSERVED, never inferred from
/// configuration — <see cref="Provider"/> in particular is read off the listener handle
/// (FR-003), because "which stack bound this" is precisely the fact configuration lies about.
/// </summary>
public sealed record ListenerReport(
    string ServiceName,
    ListenerOutcome Outcome,
    IPEndPoint? BoundEndPoint,
    string? Provider,
    IReadOnlyList<(string Provider, QuicProviderTier Tier, QuicAvailability Availability)> SkippedTiers,
    IReadOnlyList<(string Provider, QuicProviderTier Tier, QuicAvailability Availability)> Diagnoses,
    string Detail)
{
    /// <summary>True only for <see cref="ListenerOutcome.Ok"/> — every other outcome is not health.</summary>
    public bool IsHealthy => Outcome == ListenerOutcome.Ok;

    /// <summary>
    /// A tier was passed over. Per FR-008 a fallback that is not reported is a defect, not a
    /// degradation, so this is printed by <see cref="Describe"/> and never merely logged.
    /// </summary>
    public bool FellBack => SkippedTiers.Count > 0;

    /// <summary>Operator-facing summary. Print this at service start.</summary>
    public string Describe()
    {
        var lines = new List<string>
        {
            $"listener {ServiceName}: {Outcome}"
              + (BoundEndPoint is null ? "" : $" at {BoundEndPoint}")
              + (Provider is null ? "" : $" via {Provider}")
              + $" — {Detail}",
        };

        foreach (var s in SkippedTiers)
            lines.Add($"  SKIPPED tier {(int)s.Tier} {s.Provider}: {s.Availability.Detail}");

        if (Outcome == ListenerOutcome.Refused)
            foreach (var d in Diagnoses)
                lines.Add($"  tier {(int)d.Tier} {d.Provider}: {d.Availability.Detail}");

        return string.Join(Environment.NewLine, lines);
    }
}
