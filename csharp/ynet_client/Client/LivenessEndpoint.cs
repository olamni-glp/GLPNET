// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Ynet.Client;

/// <summary>
/// The endpoint a supervisor pings to learn whether this client is actually alive.
///
/// <para>
/// <b>Why this exists</b> (M6-d; engineer reversal of ruling Q-G34-01, 2026-09-06). The M6 mandate
/// requires the client's main part to be a kernel-managed process. The root cause of it not being
/// one was measured before anything was built: <c>csharp/glp_supervisor</c> is a working, tested
/// supervisor in this very repo — child hosting, backoff, restart, crash-loop taxonomy — and it
/// hosts <c>glp_engine_host</c> while never hosting this client. The capability existed; the
/// consumer was never written. Exactly the same shape as the QUIC carrier this era also closes.
/// </para>
///
/// <para>
/// A supervisor cannot manage what it cannot interrogate. The one thing that supervisor requires
/// and this client lacked is an endpoint that ANSWERS. Hence this type: it is the missing half of
/// the consumer, not a second supervisor (FR-029 forbids writing one — that would mint a third
/// instance of the very defect being closed).
/// </para>
///
/// <para>
/// 🔴 <b>Liveness is a round-trip ANSWER, never process existence, never a self-declared status,
/// never an unexpired lease.</b> A timer that renews regardless of health seats a zombie forever
/// and destroys the signal the watcher needs — the lapse is the feature. So:
/// </para>
/// <list type="number">
///   <item>the answer is computed from the RECEIVER'S health, not from a bare socket accept.
///         A client answering "alive" from an accept loop while its plane is dead is a zombie
///         wearing a heartbeat, and it is the exact failure a process-existence check cannot see;</item>
///   <item>the responder runs on a DEDICATED THREAD. Parked on a starved thread pool it would answer
///         "unavailable" and the supervisor would kill a healthy client — measured on this host
///         2026-09-06, aimed here at the one component whose whole job is to be believed;</item>
///   <item>an unhealthy client answers UNHEALTHY rather than not answering, so a supervisor can
///         distinguish "sick" from "gone" — and a broken channel from a dead process (FR-027).</item>
/// </list>
/// </summary>
public sealed class LivenessEndpoint : IDisposable
{
    /// <summary>Sent when the client is receiving normally.</summary>
    public const string Healthy = "ALIVE";

    /// <summary>Sent when the client is running but its plane is not delivering. Deliberately a
    /// DIFFERENT answer from silence: silence means gone, this means sick.</summary>
    public const string Unhealthy = "UNHEALTHY";

    private readonly Func<bool> _isHealthy;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Thread? _thread;
    private long _answered;
    private bool _disposed;

    public LivenessEndpoint(IPEndPoint bind, Func<bool> isHealthy)
    {
        ArgumentNullException.ThrowIfNull(bind);
        ArgumentNullException.ThrowIfNull(isHealthy);
        _isHealthy = isHealthy;
        _listener = new TcpListener(bind);
    }

    /// <summary>The address actually bound — read off the socket, never the requested one. Port 0
    /// is legitimate and useful (the OS chooses), and reporting the requested port in that case
    /// would name a port nothing is listening on.</summary>
    public IPEndPoint? BoundEndPoint => _listener.Server.IsBound
        ? (IPEndPoint?)_listener.Server.LocalEndPoint
        : null;

    /// <summary>How many pings were answered. Exposed because a supervisor that never pings and a
    /// client that never answers look identical from the client's side.</summary>
    public long Answered => Interlocked.Read(ref _answered);

    public void Start()
    {
        _listener.Start();

        // 🔴 A dedicated thread, not the pool. AcceptTcpClient parks for an unbounded time, and the
        // pool injects roughly one thread per second once its minimum is exhausted. Parking the
        // liveness responder there means that under load the supervisor's ping goes unanswered and
        // it kills a client that was perfectly healthy.
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "ynet-client-liveness",
        };
        _thread.Start();
    }

    private void Loop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = _listener.AcceptTcpClient();
                using var stream = client.GetStream();

                // The answer is computed HERE, from the receiver, at the moment of asking.
                // Precomputing it — or letting a timer set it — is what turns a heartbeat into a
                // lease, and a lease renews whether or not anything is working.
                var healthy = false;
                try { healthy = _isHealthy(); }
                catch { healthy = false; }

                var answer = Encoding.UTF8.GetBytes((healthy ? Healthy : Unhealthy) + "\n");
                stream.Write(answer, 0, answer.Length);
                stream.Flush();
                Interlocked.Increment(ref _answered);
            }
            catch (SocketException) when (_cts.IsCancellationRequested)
            {
                return;   // ordinary shutdown
            }
            catch (ObjectDisposedException)
            {
                return;   // ordinary shutdown
            }
            catch (Exception)
            {
                // One bad connection must never stop the responder: a peer that can make this loop
                // exit can make the supervisor kill a healthy client, which is a denial-of-service
                // primitive against our own supervision.
            }
            finally
            {
                client?.Dispose();
            }
        }
    }

    /// <summary>Ask a liveness endpoint whether it is alive. Used by tests and by any watcher that
    /// is not the supervisor itself.</summary>
    public static string? Ping(IPEndPoint endpoint, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        try
        {
            using var client = new TcpClient();
            if (!client.ConnectAsync(endpoint.Address, endpoint.Port).Wait(timeout)) return null;
            client.ReceiveTimeout = (int)timeout.TotalMilliseconds;
            using var stream = client.GetStream();
            var buf = new byte[64];
            var n = stream.Read(buf, 0, buf.Length);
            return n <= 0 ? null : Encoding.UTF8.GetString(buf, 0, n).Trim();
        }
        catch (Exception)
        {
            // No answer is "gone". It is deliberately NOT the same value as UNHEALTHY: a supervisor
            // must be able to tell a sick client from an absent one, or it cannot tell a broken
            // channel from a dead process (FR-027).
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* already stopped */ }
        _thread?.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }
}
