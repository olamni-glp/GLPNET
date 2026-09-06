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

        for (var i = 0; i < 3; i++)
        {
            await using var channel = await ClientChannel.ConnectAsync("127.0.0.1", port, Budget);
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
