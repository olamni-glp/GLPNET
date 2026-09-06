// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using GlpRuntime.Link;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.SplitProtocol;

namespace Ynet.Client;

/// <summary>
/// The liveness responder <b>in the wire format this repo's supervisor already speaks</b>, so that
/// <c>csharp/glp_supervisor</c> can host the M6 client <b>without a single line changing in the
/// supervisor</b>.
///
/// <para>
/// 🔴 <b>This is the whole point, and it is FR-029.</b> The measured root cause of "this client is
/// not kernel-managed" was never a missing capability: <c>Supervisor</c> already does child
/// hosting, round-trip liveness, ping-timeout zombie detection, backoff, restart-with-restore and
/// a crash-loop taxonomy that stops loudly. It hosts <c>glp_engine_host</c>. It did not host this
/// client. The capability existed; <b>the consumer was never written</b>.
/// </para>
///
/// <para>
/// There were three ways to close that, and two of them were wrong:
/// </para>
/// <list type="number">
///   <item><b>Write a second supervisor.</b> Rejected — that would mint a THIRD instance of the
///         exact defect class this era exists to close, and SC-012 measures the count.</item>
///   <item><b>Refactor <c>Supervisor</c> to accept an injectable probe.</b> Rejected for now — its
///         liveness is a stateful <c>ClientChannel</c> session threaded through six sites, and it is
///         load-bearing for another project. Changing a shared component to accommodate a new caller
///         is the expensive direction.</item>
///   <item><b>Make the new caller speak the wire the supervisor already speaks.</b> Chosen. Zero
///         change to the shared component, zero risk to its existing consumer, and the bind is a
///         bind rather than a rewrite.</item>
/// </list>
///
/// <para>
/// The supervisor's probe is: dial TCP, send <c>RequestKind.Ping</c>, expect
/// <c>ResponseKind.Ack</c> within <c>PingTimeout</c>. That is all this type answers — and it
/// answers it from the receiver's <b>actual state</b>, never from the fact that a socket accepted.
/// </para>
///
/// <para>
/// 🔴 <b>A ping is answered only while the receiver is genuinely healthy.</b> An unhealthy client
/// answers nothing on this endpoint, which the supervisor reads as death and acts on. That is
/// deliberate and it is the difference between supervision and theatre: a client that answered
/// "alive" from its accept loop while its state machine had stopped would be a zombie wearing a
/// heartbeat, and the supervisor would never restart it. <b>The lapse is the feature.</b>
/// (<see cref="LivenessEndpoint"/> keeps the richer three-valued answer —
/// healthy / unhealthy / gone — for watchers that can use it; this endpoint speaks the supervisor's
/// two-valued protocol because that is the protocol the supervisor has.)
/// </para>
/// </summary>
public sealed class SupervisedLiveness : IDisposable
{
    private readonly Func<bool> _isHealthy;
    private readonly string _host;
    private readonly int _port;
    private readonly CancellationTokenSource _cts = new();
    private Thread? _thread;
    private long _acked;
    private long _refused;
    private bool _disposed;

    public SupervisedLiveness(string host, int port, Func<bool> isHealthy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(isHealthy);
        _host = host;
        _port = port;
        _isHealthy = isHealthy;
    }

    /// <summary>Pings answered with an Ack. </summary>
    public long Acked => Interlocked.Read(ref _acked);

    /// <summary>Pings deliberately NOT answered because the receiver was unhealthy. Counted rather
    /// than silent, because "we refused to claim health" and "nobody ever asked" are different
    /// states and a supervisor's verdict depends on which one it is.</summary>
    public long Refused => Interlocked.Read(ref _refused);

    /// <summary>Set once the OS listener is genuinely bound — never before. A readiness token
    /// published on intent rather than on a successful bind is how a supervisor starts pinging a
    /// port nothing is listening on and concludes the child is dead.</summary>
    public ManualResetEventSlim Bound { get; } = new(false);

    public void Start()
    {
        // 🔴 A DEDICATED THREAD, not the pool. The accept loop parks for an unbounded time, and this
        // host measured on 2026-09-06 what that does: once the pool's minimum is exhausted it
        // injects roughly one thread per second, blocked threads never return, and every
        // wall-clock-timeout operation then answers the honest-but-wrong "unavailable". Pointing
        // that at the one component whose entire job is to be believed would make a healthy client
        // look dead and get it killed and restarted in a loop.
        _thread = new Thread(() => Loop().GetAwaiter().GetResult())
        {
            IsBackground = true,
            Name = "ynet-client-supervised-liveness",
        };
        _thread.Start();
    }

    private async Task Loop()
    {
        var transport = new TcpTransport();
        try
        {
            await foreach (var endpoint in transport.AcceptLoopAsync(
                               LinkScheme.Tcp,
                               LinkAddress.Endpoint(_host, _port),
                               LinkOptions.Default,
                               _cts.Token,
                               onBound: () => Bound.Set()))
            {
                // 🔴 EACH CONNECTION IS SERVED OFF THE ACCEPT LOOP — codexreview finding P1,
                // 2026-09-06. Previously this awaited ServeOne inline, so ONE peer that connected
                // and then sent nothing parked the sole accept loop forever: the supervisor's next
                // connection was never accepted, its ping went unanswered, and it would kill and
                // restart a perfectly healthy client. A denial-of-service primitive aimed at our own
                // supervision, reachable by anyone who can open a socket to the probe port.
                //
                // My own test did not catch it, and the reason is worth keeping: the rude client in
                // that test DISPOSED its socket, sending FIN, so the read returned null promptly. A
                // peer that connects and simply HOLDS is the case the test never wrote.
                var accepted = endpoint;
                var worker = new Thread(() => ServeConnection(accepted).GetAwaiter().GetResult())
                {
                    IsBackground = true,
                    Name = "ynet-liveness-conn",
                };
                worker.Start();
            }
        }
        catch (OperationCanceledException)
        {
            // Ordinary shutdown.
        }
        catch (Exception)
        {
            // A failed bind is reported by the Bound gate never opening, which the caller checks.
            // Throwing out of a background thread would take the whole client down for a liveness
            // port — the receiving is the product, the supervision is a convenience.
        }
    }

    /// <summary>How long one connection may stay silent before it is dropped. Bounded because an
    /// unbounded wait is a socket a peer can hold forever, and holding sockets is free.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Serve one connection for as long as the peer keeps using it.
    ///
    /// 🔴 <b>Repeated pings on ONE channel</b> — codexreview finding P2, 2026-09-06. The supervisor
    /// RETAINS a successful <c>ClientChannel</c> and sends every later ping over it
    /// (<c>Supervisor.cs:94,145</c>). An earlier version of this method answered exactly one request
    /// and closed, so every health check after the first met a deliberately broken connection and
    /// depended on the supervisor's single reconnect allowance — turning one transient reconnect
    /// failure into a false death verdict. Serve until the peer closes.
    /// </summary>
    private async Task ServeConnection(ILinkEndpoint endpoint)
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                // A per-read deadline, so a peer that connects and says nothing costs one idle
                // thread for a bounded time rather than a socket held forever.
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                idle.CancelAfter(IdleTimeout);

                byte[]? bytes;
                try
                {
                    bytes = await endpoint.RecvBytesAsync(idle.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;   // idle too long, or shutting down
                }

                if (bytes is null) return;   // peer closed — the ordinary end of a channel

                RequestFrame request;
                try { request = RequestResponseCodec.DecodeRequestFrame(bytes); }
                catch (Exception) { return; }   // not our protocol; drop this connection only

                // Health is read HERE, at the moment of asking, from the receiver's own state.
                var healthy = false;
                try { healthy = _isHealthy(); }
                catch { healthy = false; }

                if (!healthy)
                {
                    // Say nothing and close. The supervisor's PingTimeout elapses and it treats this
                    // client as dead — which it effectively is. Answering "Ack" to be polite is
                    // exactly how a zombie survives supervision forever.
                    Interlocked.Increment(ref _refused);
                    return;
                }

                // Only Ping is answered. This endpoint is a liveness probe, not a control channel:
                // a client that served arbitrary request kinds here would be a second,
                // unauthenticated control surface, and the one the fleet audits is the CLI.
                if (request.Kind != RequestKind.Ping) continue;

                var response = ResponseFrame.Text(request.RequestId, ResponseKind.Ack, "pong");
                await endpoint.SendBytesAsync(
                    RequestResponseCodec.EncodeResponseFrame(response, 1), _cts.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _acked);
            }
        }
        catch (Exception)
        {
            // One bad connection must never take anything else down with it.
        }
        finally
        {
            try { await endpoint.DisposeAsync().ConfigureAwait(false); } catch { /* closing */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(3));
        Bound.Dispose();
        _cts.Dispose();
    }
}
