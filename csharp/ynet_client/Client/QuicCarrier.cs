// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Ynet.Transport.Capability;
using Ynet.Transport.Link;
using Ynet.Transport.Listener;

namespace Ynet.Client;

/// <summary>
/// The QUIC realization of the receive seam — <b>path 1, the YNET wire</b>.
///
/// <para>
/// Until now <see cref="IYnetInbound"/> had two realizations: <see cref="LoopbackInbound"/>
/// (in-memory) and <see cref="CoopFileInbound"/> (a file drop on a shared volume). Cross-host
/// delivery therefore travelled over a mounted disk. This class is the missing third: the same
/// contract, carried by <c>csharp/ynet_transport</c>'s authenticated QUIC session.
/// </para>
///
/// <para>
/// 🔴 <b>The frame on the wire is BYTE-IDENTICAL to the file plane's.</b> Both encode
/// <see cref="YnetFrame"/> as UTF-8 JSON. That is the whole point of having one seam: a message must
/// not change shape depending on which plane carried it, or the two planes drift into two protocols
/// that share a name, and every cross-plane bug becomes unreproducible on the other plane.
/// </para>
///
/// <para>
/// 🔴 <b>Every loop here runs on a DEDICATED THREAD, never the thread pool.</b>
/// <c>YnetSession.Receive()</c> bottoms out in <c>BlockingCollection.Take()</c> and
/// <c>IQuicListenerHandle.AcceptAsync</c> waits for a peer — both park a thread for an unbounded
/// time. Parking those on the pool is exactly the defect measured in this repo on 2026-09-06: the
/// pool injects ~1 thread/sec once its minimum is exhausted, blocked threads never return, and
/// unrelated timeout-bearing work then fails with an honest-but-wrong "unavailable". A carrier that
/// starves the pool would inflict that on every provider probe in the process.
/// </para>
///
/// <para>
/// <b>Origin is bound to the AUTHENTICATED PEER, which the file plane cannot do.</b> On the coop
/// plane, <c>Origin</c> is checked for internal consistency against <c>SenderNode</c>/<c>SenderActor</c>
/// because nothing else is available — a file carries no proof of who wrote it. Here the session has
/// already completed a signed handshake, so a frame whose claimed sender is not the peer that sent it
/// is refused as a <b>security event</b>, not tidied up. Presence on a wire is not identity; the
/// handshake is.
/// </para>
/// </summary>
public sealed class QuicInbound : IYnetInbound, IDisposable
{
    /// <summary>Frames larger than this are refused rather than buffered. A carrier with no ceiling
    /// is a memory-exhaustion primitive for anyone who can complete a handshake.</summary>
    public const int MaxFrameBytes = 1 << 20;

    private readonly YnetListenerService _listener;
    private readonly ListenerConfig _config;
    private readonly NodeIdentity _self;
    private readonly RoutingSelection _selection;
    private readonly ConcurrentDictionary<Guid, YnetSession> _sessions = new();
    private readonly List<Thread> _threads = [];
    private readonly Lock _gate = new();

    private CancellationTokenSource? _cts;
    private IQuicListenerHandle? _handle;
    private bool _open;
    private long _refusedFrames;

    /// <summary>How many frames were refused (bad JSON, oversize, or a sender that is not the peer).
    /// Exposed because a carrier that silently drops is indistinguishable from one nobody is using.</summary>
    public long RefusedFrames => Interlocked.Read(ref _refusedFrames);

    /// <summary>The address actually bound. Null until <see cref="Open"/> succeeds.</summary>
    public IPEndPoint? BoundEndPoint => _handle?.LocalEndPoint;

    /// <summary>The provider that bound the listener — read off the handle, never inferred.</summary>
    public string? ProviderName => _handle?.ProviderName;

    public QuicInbound(
        NodeIdentity self,
        ListenerConfig config,
        QuicProviderChain? chain = null,
        RoutingSelection? selection = null)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(config);
        _self = self;
        _config = config;
        _listener = new YnetListenerService(chain);
        _selection = selection ?? RoutingSelection.SafeDefault;
    }

    public string PlaneName => "quic";

    public event Action<YnetMessage>? Received;

    /// <summary>
    /// Bind and begin accepting. Idempotent.
    ///
    /// This is SYNCHRONOUS because <see cref="IYnetInbound.Open"/> is, and it is the seam that
    /// decides — widening the interface to async for one realization would force the in-memory plane
    /// to pretend it has latency it does not have.
    /// </summary>
    public void Open()
    {
        lock (_gate)
        {
            if (_open) return;

            var (report, handle) = _listener.BindAsync(_config).GetAwaiter().GetResult();
            if (handle is null)
                throw new InvalidOperationException(
                    "QUIC inbound could not bind, and every tier said why: " + report.Describe());

            _handle = handle;
            _cts = new CancellationTokenSource();
            _open = true;

            StartThread($"quic-accept-{_config.ServiceName}", () => AcceptLoop(_cts.Token));
        }
    }

    /// <summary>Stop delivering. Idempotent. Safe to call from a Received handler.</summary>
    public void Close()
    {
        CancellationTokenSource? cts;
        IQuicListenerHandle? handle;
        Thread[] threads;

        lock (_gate)
        {
            if (!_open) return;
            _open = false;
            cts = _cts; _cts = null;
            handle = _handle; _handle = null;
            threads = [.. _threads];
            _threads.Clear();
        }

        // Cancel BEFORE disposing sessions, so a loop that wakes mid-teardown sees "stopping" rather
        // than treating a closed channel as a peer fault and retrying into a disposed handle.
        try { cts?.Cancel(); } catch (ObjectDisposedException) { }

        foreach (var s in _sessions.Values) { try { s.Close(); } catch (IOException) { } }
        _sessions.Clear();

        if (handle is not null)
        {
            try { handle.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
        }

        // Join with a bound: a thread parked in a blocking Take() unblocks when its channel closes,
        // but a hung peer must not hold shutdown open forever.
        foreach (var t in threads) { try { t.Join(TimeSpan.FromSeconds(2)); } catch (ThreadStateException) { } }

        cts?.Dispose();
    }

    private void StartThread(string name, Action body)
    {
        var t = new Thread(() => { try { body(); } catch (OperationCanceledException) { } })
        {
            IsBackground = true,
            Name = name,
        };
        _threads.Add(t);
        t.Start();
    }

    private void AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            IWireChannel channel;
            try
            {
                channel = _handle!.AcceptAsync(ct).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (IOException) { return; }

            var accepted = YnetSession.Accept(channel, _self, _selection);
            if (!accepted.Ok)
            {
                // A refused handshake is an expected outcome (wrong key, tampered frame), not an
                // error: drop this peer and keep accepting. Refusing to serve one peer must never
                // take the listener down.
                try { channel.Dispose(); } catch (ObjectDisposedException) { }
                Interlocked.Increment(ref _refusedFrames);
                continue;
            }

            var session = accepted.Value!;
            _sessions[session.Handle.Id] = session;

            lock (_gate)
            {
                if (!_open) { try { session.Close(); } catch (IOException) { } return; }
                StartThread($"quic-recv-{session.Handle.Id:N}", () => ReceiveLoop(session, ct));
            }
        }
    }

    private void ReceiveLoop(YnetSession session, CancellationToken ct)
    {
        var peer = session.Peer.ToString();

        while (!ct.IsCancellationRequested)
        {
            Result<ReadOnlyMemory<byte>> got;
            try { got = session.Receive(); }
            catch (ObjectDisposedException) { break; }
            catch (IOException) { break; }

            if (!got.Ok) break;   // AuthorizedButReachable-no-more, or a seal that would not open

            var message = Decode(got.Value, peer);
            if (message is null) { Interlocked.Increment(ref _refusedFrames); continue; }

            // Handlers must not block the carrier — that is the interface's stated contract. A
            // handler that throws must not kill the loop and silently stop delivery for this peer.
            try { Received?.Invoke(message); }
            catch (Exception) { Interlocked.Increment(ref _refusedFrames); }
        }

        _sessions.TryRemove(session.Handle.Id, out _);
        try { session.Close(); } catch (IOException) { }
    }

    /// <summary>
    /// Decode one wire frame, or return null to refuse it. Null is never "empty message": a frame
    /// this method cannot vouch for is not delivered at all.
    /// </summary>
    internal static YnetMessage? Decode(ReadOnlyMemory<byte> payload, string authenticatedPeer)
    {
        if (payload.Length == 0 || payload.Length > MaxFrameBytes) return null;

        YnetFrame? frame;
        try { frame = JsonSerializer.Deserialize<YnetFrame>(payload.Span); }
        catch (JsonException) { return null; }

        // `{}` deserializes to a YnetFrame with every field at its default. That is not a message,
        // and delivering it as one was a real defect on the file plane (codexreview 2026-09-05).
        if (frame is null || string.IsNullOrWhiteSpace(frame.Origin)) return null;

        // 🔴 The claimed sender must BE the peer that sent it. On this plane that is provable, so a
        // mismatch is a security event and the frame is refused — not normalized, not logged-and-
        // delivered. Strip-a-field-and-it-becomes-yours was exactly the shape of the worst finding
        // in era 105's review.
        var claimed = $"{frame.SenderNode}/{frame.SenderActor}";
        if (!string.IsNullOrEmpty(frame.SenderNode) &&
            !string.Equals(claimed, authenticatedPeer, StringComparison.Ordinal) &&
            !string.Equals(frame.Origin, authenticatedPeer, StringComparison.Ordinal))
            return null;

        var id = $"{authenticatedPeer}#{frame.Sequence}";
        return new YnetMessage(id, frame.Origin, frame.Signal, Encoding.UTF8.GetBytes(frame.Body));
    }

    public void Dispose() => Close();
}

/// <summary>
/// The send half over QUIC. One instance is a <b>route to one peer</b>, for the same reason
/// <see cref="CoopFileOutbound"/> is: <see cref="YnetMessage"/> carries no destination, so binding
/// the peer at construction is what lets <c>Send</c> honour <see cref="IYnetOutbound"/> rather than
/// widen it.
/// </summary>
public sealed class QuicOutbound : IYnetOutbound, IDisposable
{
    /// <summary>How long a dial may take before it is refused. Bounded because <c>Send</c> is
    /// synchronous, so an unbounded dial parks the caller's thread for the QUIC idle timeout.</summary>
    public static readonly TimeSpan DialTimeout = TimeSpan.FromSeconds(3);

    private readonly QuicProviderChain _chain;
    private readonly IPEndPoint _remote;
    private readonly NodeIdentity _self;
    private readonly NodeId _peerNode;
    private readonly PeerIdentity _peer;
    private readonly RoutingSelection _selection;
    private readonly Lock _gate = new();

    private YnetSession? _session;
    private long _sequence;

    public QuicOutbound(
        NodeIdentity self,
        NodeId peerNode,
        PeerIdentity peer,
        IPEndPoint remote,
        QuicProviderChain? chain = null,
        RoutingSelection? selection = null)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(peer);
        ArgumentNullException.ThrowIfNull(remote);
        _self = self;
        _peerNode = peerNode;
        _peer = peer;
        _remote = remote;
        _chain = chain ?? QuicProviderChain.Default;
        _selection = selection ?? RoutingSelection.SafeDefault;
    }

    /// <summary>
    /// Send one message. Returns false when the plane refused it; never throws for a dead peer —
    /// that is the interface's contract, and a carrier that throws on an unreachable peer turns a
    /// routine partition into a crash.
    /// </summary>
    public bool Send(YnetMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var frame = new YnetFrame
        {
            Origin = _self.NodeId.ToString(),
            Sequence = Interlocked.Increment(ref _sequence),
            SenderNode = _self.NodeId.ToString(),
            SenderActor = _peer.Actor,
            Signal = message.Summary,
            Body = Encoding.UTF8.GetString(message.Body.Span),
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame);
        if (bytes.Length > QuicInbound.MaxFrameBytes) return false;

        var session = EnsureSession();
        if (session is null) return false;

        var sent = session.Send(bytes);
        if (sent.Ok) return true;

        // One reconnect, then give up. A refused send is usually a peer that went away between the
        // handshake and now; retrying forever here would hide a partition from the caller, who is
        // the only one that can decide to spool, escalate or drop.
        DropSession();
        session = EnsureSession();
        return session is not null && session.Send(bytes).Ok;
    }

    private YnetSession? EnsureSession()
    {
        lock (_gate)
        {
            if (_session is not null) return _session;

            IWireChannel channel;
            try
            {
                // A dial is BOUNDED. Without this the QUIC stack waits out its own handshake idle
                // timeout — measured 10 s to a dead port — and because Send is synchronous by
                // contract, that parks whatever thread called it for the whole 10 s. On a pool
                // thread that is the same starvation this repo measured in ynet_transport today,
                // and I reproduced it here before catching it.
                using var dialCts = new CancellationTokenSource(DialTimeout);
                channel = _chain.ConnectAsync(_remote, dialCts.Token).GetAwaiter().GetResult();
            }
            // 🔴 The catch list is WIDE ON PURPOSE and each entry is a measured escape, not a guess.
            // IYnetOutbound.Send's contract is "returns false when the plane refused it; NEVER throws
            // for a dead peer" — so turning a failed dial into `null` IS the specified behaviour, not
            // a swallowed error. The first version caught only QuicUnavailableException/IOException/
            // OperationCanceledException and a bare SocketException went straight through it:
            // SocketException derives from SystemException, NOT from IOException. It passed in
            // isolation (the dead port timed out) and threw under load (the OS answered "unreachable
            // host" immediately) — a contract violation that only appears when the network is fast
            // at saying no.
            catch (QuicUnavailableException) { return null; }
            catch (System.Net.Quic.QuicException) { return null; }
            catch (System.Net.Sockets.SocketException) { return null; }
            catch (System.Security.Authentication.AuthenticationException) { return null; }
            catch (IOException) { return null; }
            catch (OperationCanceledException) { return null; }

            var connected = YnetSession.Connect(channel, _self, _peerNode, _selection);
            if (!connected.Ok)
            {
                try { channel.Dispose(); } catch (ObjectDisposedException) { }
                return null;
            }

            _session = connected.Value!;
            return _session;
        }
    }

    private void DropSession()
    {
        lock (_gate)
        {
            if (_session is null) return;
            try { _session.Close(); } catch (IOException) { }
            _session = null;
        }
    }

    public void Dispose() => DropSession();
}
