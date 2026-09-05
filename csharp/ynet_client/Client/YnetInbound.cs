// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

namespace Ynet.Client;

/// <summary>One YNET message as the receiver sees it, independent of which plane carried it.</summary>
public sealed record YnetMessage(string MessageId, string Origin, string Summary, ReadOnlyMemory<byte> Body);

/// <summary>
/// The seam the two planes plug into.
///
/// The ruled architecture has two planes that must present the SAME contract: cross-host over YNET,
/// and intra-host over an in-memory kernel intercom. Having one interface here is what stops a
/// same-host message being routed through the wire, and it is why the receiver machine cannot tell
/// the planes apart.
///
/// This mirrors ITransportCarrier in YngeniOS.Mailbox.Unified, whose named realizations are the
/// in-process loopback, TCP/TLS disterl and alt-carriers. Measured 2026-09-05: that block has NO
/// QUIC realization, while glpnet's csharp/ynet_transport is a QUIC transport that builds and
/// passes 121 tests. The adapter between them is claimed by this lane and is the next commitment
/// after this client (broadcast 2026-09-05T10:50Z, section 3.3).
/// </summary>
public interface IYnetInbound
{
    /// <summary>A human-readable name for the plane, used in status output.</summary>
    string PlaneName { get; }

    /// <summary>Raised for each inbound message. Handlers must not block the carrier.</summary>
    event Action<YnetMessage>? Received;

    /// <summary>Begin delivering. Idempotent.</summary>
    void Open();

    /// <summary>Stop delivering. Idempotent.</summary>
    void Close();
}

/// <summary>
/// The in-memory plane, and the fault-injectable test double. Deliberately the same class: the
/// intra-host intercom and the loopback used in tests are the same code path, so a test cannot
/// pass against a fiction the production path does not share.
/// </summary>
public sealed class LoopbackInbound : IYnetInbound
{
    private bool _open;

    public string PlaneName => "in-memory-intercom";

    public event Action<YnetMessage>? Received;

    public void Open() => _open = true;

    public void Close() => _open = false;

    /// <summary>Deliver a message on this plane. Returns false when the plane is closed.</summary>
    public bool Deliver(YnetMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!_open) return false;
        Received?.Invoke(message);
        return true;
    }
}
