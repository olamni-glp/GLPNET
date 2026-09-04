// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The four-state observability surface (feature 102, T019/T020/T021/T023).
//
// Contract federation-status.md S1..S7 / data-model I-24..I-28 / FR-019..FR-022.
//
// THIS ESTATE RECORDED SIX FALSE GREENS IN ONE WEEK, one of which survived CI. This type exists so
// that "it looks like it is working" and "it is working" cannot produce the same output.
//
// THREE MISREADS ON THE RECORD, each of which this type forbids structurally:
//   1. "no listening TCP port  => no QUIC"     — QUIC is UDP and has no TCP socket BY DESIGN.
//   2. "ping times out         => host down"   — ICMP is filtered here; a second, DIFFERENT probe
//                                                (tcp/445) answered True 12 minutes later and the
//                                                first claim had to be retracted.
//   3. "two roots exchanged an op => federation" — two processes on ONE machine prove the
//                                                MECHANISM, not the network (FR-022).
//
// There is deliberately NO aggregate `IsFederated` boolean here, and none may be added: an
// aggregate is exactly how four honest states become one dishonest one.

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>
/// Three-valued state. <see cref="Unknown"/> means COULD NOT BE MEASURED and is a different fact
/// from <see cref="No"/>, which means measured-and-absent (FR-021 / SC-010).
/// </summary>
public enum Tri { Unknown, No, Yes }

/// <summary>A startup blocked by host software policy — named, never generalised (FR-023).</summary>
public sealed record PolicyRefusal(string Policy, int HResult, string Detail)
{
    /// <summary>Smart App Control's block HRESULT, measured on GAVRIELLA 2026-09-04.</summary>
    public const int SmartAppControlHResult = unchecked((int)0x800711C7);

    /// <summary>
    /// Recognise a host-policy refusal in an exception, or return null. Detecting this is NOT a
    /// workaround for the policy — the policy stands (turning it off was declined as one-way by
    /// ruling Q-GLPNETG27-02). It is reported so that the failure stops presenting as a healthy
    /// build and a passing suite followed by a daemon that never runs.
    /// </summary>
    public static PolicyRefusal? Detect(Exception ex) =>
        ex.HResult == SmartAppControlHResult
            ? new PolicyRefusal("Smart App Control", SmartAppControlHResult, ex.Message)
            : null;
}

/// <summary>
/// The four independently-measured federation states, plus two qualifiers.
/// Each field is set ONLY by its own measurement (FR-020).
/// </summary>
public sealed record FederationStatus
{
    /// <summary>The QUIC stack is available in THIS process. Measured from the BCL, nothing else.</summary>
    public Tri StackSupported { get; init; } = Tri.Unknown;

    /// <summary>A listener is bound to a peer-reachable address. NOT "a port is configured".</summary>
    public Tri ListenerBound { get; init; } = Tri.Unknown;

    /// <summary>
    /// At least one peer completed MUTUAL verification. Never set from reachability, a ping, or an
    /// open port — those measure something else.
    /// </summary>
    public Tri PeerAdmitted { get; init; } = Tri.Unknown;

    /// <summary>
    /// At least one operation has ACTUALLY crossed. Never inferred from a connection being
    /// established: a link that carried nothing carried nothing.
    /// </summary>
    public Tri OpReceivedFromPeer { get; init; } = Tri.Unknown;

    /// <summary>
    /// Whether an observed crossing was between two processes on ONE machine (FR-022). When
    /// <see cref="Tri.Yes"/> the surface MUST NOT be read as cross-host federation, however green
    /// the other four states look.
    /// <para>
    /// THREE-VALUED, PLUS NULL, and the distinction is load-bearing:
    ///   <c>null</c>          — no crossing has been observed, so the question does not arise;
    ///   <see cref="Tri.Unknown"/> — a crossing WAS observed but the peer's address was not captured
    ///                        (the passive/listener side, and the reconciliation-pull path, learn of
    ///                        an op without learning where it came from);
    ///   <see cref="Tri.Yes"/>/<see cref="Tri.No"/> — actually measured.
    /// </para>
    /// Collapsing <c>null</c> and <see cref="Tri.Unknown"/> makes a listener that HAS received an op
    /// render "no crossing observed" beside "op received from peer: yes" — a surface contradicting
    /// itself, and rendering identically to the genuine no-crossing case.
    /// </summary>
    public Tri? SameMachine { get; init; }

    /// <summary>Host software policy blocked startup (FR-023). Null when it did not.</summary>
    public PolicyRefusal? PolicyRefused { get; init; }

    /// <summary>Why a state is what it is — e.g. "peer set is empty - no pins configured".</summary>
    public IReadOnlyDictionary<string, string> Reasons { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Bound endpoint, when bound. Rendering detail only; never a state in its own right.</summary>
    public string? BoundEndpoint { get; init; }

    /// <summary>Admitted participant count — by NODE ID, so a two-NIC host counts once (SC-006).</summary>
    public int AdmittedParticipants { get; init; }

    /// <summary>
    /// Render per contract S7: one line per state, NO summary verdict, and `unknown` as the literal
    /// word with its reason — never blank, never a dash, and never silently degraded to `no`.
    /// </summary>
    public string Render()
    {
        string Line(string label, Tri v, string? extra = null)
        {
            string val = v switch { Tri.Yes => "yes", Tri.No => "no", _ => "unknown" };
            string reason = Reasons.TryGetValue(label, out var r) ? $"   ({r})" : "";
            string tail = extra is null ? "" : $"   {extra}";
            return $"{label,-23}: {val}{tail}{reason}";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Line("stack supported", StackSupported));
        sb.AppendLine(Line("listener bound", ListenerBound, BoundEndpoint));
        sb.AppendLine(Line("peer admitted", PeerAdmitted,
            PeerAdmitted == Tri.Yes ? $"({AdmittedParticipants} participant{(AdmittedParticipants == 1 ? "" : "s")})" : null));
        sb.AppendLine(Line("op received from peer", OpReceivedFromPeer));
        string same = SameMachine switch
        {
            null => "n/a   (no crossing observed)",
            Tri.Unknown => "unknown   (a crossing was observed but the peer address was not captured)",
            Tri.Yes => "yes",
            _ => "no",
        };
        sb.AppendLine($"{"same machine",-23}: {same}");
        sb.AppendLine($"{"policy refusal",-23}: {(PolicyRefused is null ? "none" : $"{PolicyRefused.Policy} (0x{PolicyRefused.HResult:X8})")}");
        return sb.ToString();
    }
}

/// <summary>
/// Measures the four states. Each probe is separate on purpose: an inference from one to another is
/// the defect (FR-020), so there is no code path in which one probe's result feeds another's.
/// </summary>
public static class FederationStatusProbe
{
    /// <summary>
    /// Measure whether the QUIC stack is supported in this process. Delegates to the BCL rather
    /// than caching or assuming — a prior session's measurement is a hypothesis, not a fact.
    /// </summary>
    public static Tri MeasureStackSupported()
    {
        try
        {
            return System.Net.Quic.QuicListener.IsSupported && System.Net.Quic.QuicConnection.IsSupported
                ? Tri.Yes : Tri.No;
        }
        catch
        {
            // Could not measure. That is UNKNOWN, not No — reporting a clean negative for an
            // unmeasured condition is exactly what FR-021 forbids.
            return Tri.Unknown;
        }
    }

    /// <summary>
    /// Whether two endpoints are on the same machine (FR-022). Decided by ADDRESS, not by node id —
    /// two node ids on one machine are still two node ids, and `I:` being an SMB loopback of this
    /// host's own `D:\` is the same error one layer down: a share name is not a host.
    /// </summary>
    public static bool IsSameMachine(System.Net.IPAddress local, System.Net.IPAddress remote)
    {
        if (System.Net.IPAddress.IsLoopback(remote)) return true;
        if (local.Equals(remote)) return true;
        try
        {
            var mine = System.Net.NetworkInformation.NetworkInterface
                .GetAllNetworkInterfaces()
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address);
            return mine.Any(a => a.Equals(remote));
        }
        catch
        {
            return false;
        }
    }
}
