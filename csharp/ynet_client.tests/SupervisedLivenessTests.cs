// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using GlpRuntime.ReplClient;
using GlpRuntime.SplitProtocol;
using Ynet.Client;

namespace Ynet.Client.Tests;

/// <summary>
/// M6-d — FR-024..FR-029, SC-010/SC-011/SC-012.
///
/// <para>
/// 🔴 These tests drive the <b>real client channel the real supervisor uses</b>
/// (<see cref="ClientChannel"/> from <c>glp_repl_client</c>, which is exactly what
/// <c>Supervisor.TryConnectAndPingAsync</c> constructs), not a hand-rolled socket. A test that
/// spoke a private protocol would prove this endpoint answers <i>something</i>, and prove nothing
/// about whether the supervisor can host the client — which is the entire claim being made.
/// </para>
/// </summary>
public class SupervisedLivenessTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>
    /// FR-024/FR-025 — the supervisor's own probe reaches this client and gets an Ack.
    ///
    /// This is the test that turns "the client could be supervised" into "the supervisor's actual
    /// probe succeeds against it".
    /// </summary>
    [Fact]
    public async Task The_supervisors_own_probe_gets_an_ack_from_a_healthy_client()
    {
        var port = FreePort();
        using var liveness = new SupervisedLiveness("127.0.0.1", port, isHealthy: () => true);
        liveness.Start();
        Assert.True(liveness.Bound.Wait(Budget), "the liveness listener never bound");

        await using var channel = await ClientChannel.ConnectAsync("127.0.0.1", port, Budget);
        var response = await channel.RoundTripAsync(
            RequestFrame.Empty(channel.NextRequestId(), RequestKind.Ping));

        Assert.Equal(ResponseKind.Ack, response.Kind);
        Assert.Equal(1, liveness.Acked);
    }

    /// <summary>
    /// 🔴 SC-011 — THE CRITERION THAT PROVES SUPERVISION IS REAL.
    ///
    /// The process is alive (this test is running inside it) and the listener is bound and
    /// accepting connections. Only the receiver's health has changed. A supervisor checking process
    /// existence — or reading a self-declared status, or trusting an unexpired lease — would call
    /// this client healthy and never restart it. The round trip does not: the ping goes unanswered,
    /// the supervisor's PingTimeout elapses, and it acts.
    ///
    /// <b>The lapse is the feature.</b>
    /// </summary>
    [Fact]
    public async Task A_process_that_is_alive_but_unhealthy_does_not_get_an_ack()
    {
        var healthy = true;
        var port = FreePort();
        using var liveness = new SupervisedLiveness("127.0.0.1", port, isHealthy: () => healthy);
        liveness.Start();
        Assert.True(liveness.Bound.Wait(Budget), "the liveness listener never bound");

        await using (var ok = await ClientChannel.ConnectAsync("127.0.0.1", port, Budget))
        {
            var alive = await ok.RoundTripAsync(RequestFrame.Empty(ok.NextRequestId(), RequestKind.Ping));
            Assert.Equal(ResponseKind.Ack, alive.Kind);
        }

        healthy = false;   // the state machine stops or degrades. The PROCESS is untouched.

        await using var sick = await ClientChannel.ConnectAsync("127.0.0.1", port, Budget);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // No Ack arrives. Whether that surfaces as a transport break or a timeout is the
        // supervisor's business; what matters is that it is NOT an Ack.
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await sick.RoundTripAsync(RequestFrame.Empty(sick.NextRequestId(), RequestKind.Ping), cts.Token));

        Assert.True(liveness.Refused >= 1,
            "an unhealthy client must RECORD that it refused to claim health — 'we refused' and " +
            "'nobody asked' are different states and a supervisor's verdict depends on which.");
    }

    /// <summary>A health probe that throws is not healthy. Fail closed: an exception inside the
    /// health computation is the state in which the client is least trustworthy.</summary>
    [Fact]
    public async Task A_health_check_that_throws_gets_no_ack()
    {
        var port = FreePort();
        using var liveness = new SupervisedLiveness(
            "127.0.0.1", port, isHealthy: () => throw new InvalidOperationException("boom"));
        liveness.Start();
        Assert.True(liveness.Bound.Wait(Budget));

        await using var channel = await ClientChannel.ConnectAsync("127.0.0.1", port, Budget);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await channel.RoundTripAsync(RequestFrame.Empty(channel.NextRequestId(), RequestKind.Ping), cts.Token));
        Assert.Equal(0, liveness.Acked);
    }

    /// <summary>
    /// The endpoint is a liveness probe, not a control channel. A client that answered arbitrary
    /// request kinds here would be a second, unauthenticated control surface — and the one the
    /// fleet audits is the CLI.
    /// </summary>
    [Fact]
    public async Task Only_ping_is_answered()
    {
        var port = FreePort();
        using var liveness = new SupervisedLiveness("127.0.0.1", port, isHealthy: () => true);
        liveness.Start();
        Assert.True(liveness.Bound.Wait(Budget));

        await using var channel = await ClientChannel.ConnectAsync("127.0.0.1", port, Budget);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await channel.RoundTripAsync(
                RequestFrame.Empty(channel.NextRequestId(), RequestKind.Status), cts.Token));
        Assert.Equal(0, liveness.Acked);
    }

    /// <summary>FR-027 — one bad connection must not end the accept loop. A peer able to kill it
    /// could make the supervisor kill a healthy client: a denial-of-service primitive aimed at our
    /// own supervision.</summary>
    [Fact]
    public async Task The_responder_survives_a_peer_that_connects_and_drops()
    {
        var port = FreePort();
        using var liveness = new SupervisedLiveness("127.0.0.1", port, isHealthy: () => true);
        liveness.Start();
        Assert.True(liveness.Bound.Wait(Budget));

        using (var rude = new System.Net.Sockets.TcpClient())
        {
            rude.Connect("127.0.0.1", port);
        }

        await using var channel = await ClientChannel.ConnectAsync("127.0.0.1", port, Budget);
        var response = await channel.RoundTripAsync(
            RequestFrame.Empty(channel.NextRequestId(), RequestKind.Ping));
        Assert.Equal(ResponseKind.Ack, response.Kind);
    }

    /// <summary>
    /// 🔴 THE TEST I DID NOT WRITE, added from codexreview finding P1 (2026-09-06).
    ///
    /// A peer that connects and then simply HOLDS the socket — sending nothing, closing nothing —
    /// used to park the sole accept loop forever. The supervisor's next connection was never
    /// accepted, its ping went unanswered, and it would kill and restart a perfectly healthy client:
    /// a denial-of-service primitive against our own supervision, reachable by anyone who can open
    /// a socket to the probe port.
    ///
    /// My earlier "survives a rude peer" test passed against that defect, because its rude client
    /// DISPOSED the socket and the FIN made the read return promptly. Connect-and-hold is the case
    /// the test never wrote, and it is the one that mattered.
    /// </summary>
    [Fact]
    public async Task A_peer_that_connects_and_holds_does_not_starve_the_probe()
    {
        var port = FreePort();
        using var liveness = new SupervisedLiveness("127.0.0.1", port, isHealthy: () => true);
        liveness.Start();
        Assert.True(liveness.Bound.Wait(Budget));

        // Connect and hold. No send, no close, no dispose until the test ends.
        using var squatter = new System.Net.Sockets.TcpClient();
        await squatter.ConnectAsync("127.0.0.1", port);

        // The supervisor must still get its answer.
        await using var channel = await ClientChannel.ConnectAsync("127.0.0.1", port, Budget);
        var response = await channel.RoundTripAsync(
            RequestFrame.Empty(channel.NextRequestId(), RequestKind.Ping));

        Assert.Equal(ResponseKind.Ack, response.Kind);
    }

    /// <summary>
    /// 🔴 REPEATED PINGS ON ONE CHANNEL — codexreview finding P2 (2026-09-06).
    ///
    /// The supervisor RETAINS a successful ClientChannel and sends every later ping over it
    /// (Supervisor.cs:94,145). An earlier version of this responder answered exactly one request per
    /// connection, so every check after the first met a deliberately broken channel and leaned on
    /// the supervisor's single reconnect allowance — turning one transient reconnect failure into a
    /// false death verdict for a healthy client.
    /// </summary>
    [Fact]
    public async Task One_channel_answers_many_pings_the_way_the_supervisor_uses_it()
    {
        var port = FreePort();
        using var liveness = new SupervisedLiveness("127.0.0.1", port, isHealthy: () => true);
        liveness.Start();
        Assert.True(liveness.Bound.Wait(Budget));

        await using var channel = await ClientChannel.ConnectAsync("127.0.0.1", port, Budget);

        for (var i = 0; i < 5; i++)
        {
            var response = await channel.RoundTripAsync(
                RequestFrame.Empty(channel.NextRequestId(), RequestKind.Ping));
            Assert.Equal(ResponseKind.Ack, response.Kind);
        }

        Assert.Equal(5, liveness.Acked);
    }

    /// <summary>The answer is recomputed on every ping. A cached answer is a LEASE, and a lease
    /// renews whether or not anything is working — which seats a zombie forever.</summary>
    [Fact]
    public async Task Health_is_recomputed_on_every_ping()
    {
        var calls = 0;
        var port = FreePort();
        using var liveness = new SupervisedLiveness("127.0.0.1", port, isHealthy: () => { calls++; return true; });
        liveness.Start();
        Assert.True(liveness.Bound.Wait(Budget));

        await using (var channel = await ClientChannel.ConnectAsync("127.0.0.1", port, Budget))
        {
            for (var i = 0; i < 3; i++)
                await channel.RoundTripAsync(RequestFrame.Empty(channel.NextRequestId(), RequestKind.Ping));
        }

        Assert.Equal(3, calls);
        Assert.Equal(3, liveness.Acked);
    }

    /// <summary>Readiness is published on a SUCCESSFUL BIND, never on intent. A token published on
    /// intent is how a supervisor starts pinging a port nothing is listening on and concludes the
    /// child is dead.</summary>
    [Fact]
    public void Readiness_is_not_signalled_before_the_bind_succeeds()
    {
        var port = FreePort();
        using var blocker = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
        blocker.Start();
        try
        {
            using var liveness = new SupervisedLiveness("127.0.0.1", port, isHealthy: () => true);
            liveness.Start();

            // The port is taken, so the bind cannot succeed and Bound must stay closed.
            Assert.False(liveness.Bound.Wait(TimeSpan.FromSeconds(2)));
        }
        finally { blocker.Stop(); }
    }

    /// <summary>FR-021 — Dispose releases the port and the thread; no process-lifetime leak.</summary>
    [Fact]
    public void Dispose_releases_the_port_and_is_idempotent()
    {
        var port = FreePort();
        var liveness = new SupervisedLiveness("127.0.0.1", port, isHealthy: () => true);
        liveness.Start();
        Assert.True(liveness.Bound.Wait(Budget));

        liveness.Dispose();
        liveness.Dispose();   // idempotent

        var rebind = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
        rebind.Start();       // throws if the port were still held
        rebind.Stop();
    }

    /// <summary>
    /// SC-012 — this feature adds NO second supervisor.
    ///
    /// Measured by counting process-supervision implementations in the repo's C# tree. The count
    /// must be unchanged by this era: the whole M6-d design is to make the client speak the wire
    /// the EXISTING supervisor already speaks, because writing a second one would mint a third
    /// instance of the very defect this era closes (FR-029).
    /// </summary>
    [Fact]
    public void This_feature_adds_no_second_supervisor()
    {
        // ynet_client contains no type whose name declares it a supervisor. The supervision
        // capability stays in csharp/glp_supervisor, where it already was and already worked.
        var supervisorTypes = typeof(SupervisedLiveness).Assembly
            .GetTypes()
            .Where(t => t.Name.Contains("Supervisor", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(supervisorTypes);
    }
}
