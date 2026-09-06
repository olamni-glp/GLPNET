// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Net;
using Ynet.Client;

namespace Ynet.Client.Tests;

/// <summary>
/// FR-024..FR-028, SC-010/SC-011 — the endpoint that makes supervised hosting real.
///
/// <para>
/// The criterion that matters here is <see cref="A_process_that_is_alive_but_stopped_answering_is_not_healthy"/>.
/// Every other test in this file would also pass against a check that merely observed the process
/// existing. That one would not — which is why it is the test that proves the check is a real one.
/// </para>
/// </summary>
public class LivenessEndpointTests
{
    private static IPEndPoint Loopback0() => new(IPAddress.Loopback, 0);
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(3);

    [Fact]
    public void A_healthy_client_answers_the_round_trip()
    {
        using var ep = new LivenessEndpoint(Loopback0(), isHealthy: () => true);
        ep.Start();

        var answer = LivenessEndpoint.Ping(ep.BoundEndPoint!, Budget);

        Assert.Equal(LivenessEndpoint.Healthy, answer);
        Assert.Equal(1, ep.Answered);
    }

    /// <summary>
    /// 🔴 THE CRITERION THAT PROVES THE CHECK IS REAL.
    ///
    /// The process is alive — this test is running inside it — and the endpoint is bound and
    /// accepting. Only the receiver's health has changed. A supervisor that checked process
    /// existence, or read a self-declared status, or trusted an unexpired lease, would call this
    /// client healthy. The round trip does not.
    /// </summary>
    [Fact]
    public void A_process_that_is_alive_but_stopped_answering_is_not_healthy()
    {
        var healthy = true;
        using var ep = new LivenessEndpoint(Loopback0(), isHealthy: () => healthy);
        ep.Start();

        Assert.Equal(LivenessEndpoint.Healthy, LivenessEndpoint.Ping(ep.BoundEndPoint!, Budget));

        healthy = false;   // the machine stops or degrades; the PROCESS is untouched

        Assert.Equal(LivenessEndpoint.Unhealthy, LivenessEndpoint.Ping(ep.BoundEndPoint!, Budget));
    }

    /// <summary>
    /// FR-027 — a sick client and an absent one must be DIFFERENT answers, or a supervisor cannot
    /// tell a broken channel from a dead process, and will restart a client that only needed its
    /// channel re-opened.
    /// </summary>
    [Fact]
    public void Sick_and_gone_are_different_answers()
    {
        var ep = new LivenessEndpoint(Loopback0(), isHealthy: () => false);
        ep.Start();
        var endpoint = ep.BoundEndPoint!;

        var sick = LivenessEndpoint.Ping(endpoint, Budget);
        Assert.Equal(LivenessEndpoint.Unhealthy, sick);

        ep.Dispose();                                     // now genuinely gone
        var gone = LivenessEndpoint.Ping(endpoint, Budget);

        Assert.Null(gone);
        Assert.NotEqual(sick, gone);
    }

    /// <summary>A health probe that throws is not "healthy". Fail closed: an exception inside the
    /// health computation is exactly the state where the client is least trustworthy.</summary>
    [Fact]
    public void A_health_check_that_throws_answers_unhealthy()
    {
        using var ep = new LivenessEndpoint(Loopback0(), isHealthy: () => throw new InvalidOperationException("boom"));
        ep.Start();

        Assert.Equal(LivenessEndpoint.Unhealthy, LivenessEndpoint.Ping(ep.BoundEndPoint!, Budget));
    }

    [Fact]
    public void The_answer_is_recomputed_on_every_ping_never_cached()
    {
        // A cached or timer-driven answer is a LEASE, and a lease renews whether or not anything is
        // working — which seats a zombie forever and destroys the very signal the watcher needs.
        var calls = 0;
        using var ep = new LivenessEndpoint(Loopback0(), isHealthy: () => { calls++; return true; });
        ep.Start();

        LivenessEndpoint.Ping(ep.BoundEndPoint!, Budget);
        LivenessEndpoint.Ping(ep.BoundEndPoint!, Budget);
        LivenessEndpoint.Ping(ep.BoundEndPoint!, Budget);

        Assert.Equal(3, calls);
        Assert.Equal(3, ep.Answered);
    }

    [Fact]
    public void The_bound_endpoint_is_read_from_the_socket_not_from_the_request()
    {
        // Port 0 is legitimate and useful — the OS chooses. Reporting the REQUESTED port would name
        // a port nothing is listening on, which is the same family of defect as reporting a plane
        // that is not live.
        using var ep = new LivenessEndpoint(Loopback0(), isHealthy: () => true);
        ep.Start();

        Assert.NotNull(ep.BoundEndPoint);
        Assert.NotEqual(0, ep.BoundEndPoint!.Port);
    }

    [Fact]
    public void A_ping_to_nothing_returns_gone_rather_than_throwing()
    {
        // A watcher must never crash because the thing it watches is absent — that is the ordinary
        // case it exists to detect.
        var nowhere = new IPEndPoint(IPAddress.Loopback, 1);   // reserved, nothing listens
        Assert.Null(LivenessEndpoint.Ping(nowhere, TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void The_responder_survives_a_client_that_connects_and_says_nothing()
    {
        using var ep = new LivenessEndpoint(Loopback0(), isHealthy: () => true);
        ep.Start();
        var endpoint = ep.BoundEndPoint!;

        // Connect and drop without reading. A peer that can make this loop exit could make a
        // supervisor kill a healthy client — a denial-of-service primitive against our own
        // supervision.
        using (var rude = new System.Net.Sockets.TcpClient())
        {
            rude.Connect(endpoint);
        }

        Assert.Equal(LivenessEndpoint.Healthy, LivenessEndpoint.Ping(endpoint, Budget));
    }

    [Fact]
    public void Dispose_releases_the_port_and_the_thread()
    {
        // FR-021: no process-lifetime leak. If the port were still held, re-binding it would throw.
        var ep = new LivenessEndpoint(Loopback0(), isHealthy: () => true);
        ep.Start();
        var port = ep.BoundEndPoint!.Port;
        ep.Dispose();

        using var rebound = new LivenessEndpoint(new IPEndPoint(IPAddress.Loopback, port), () => true);
        rebound.Start();   // throws if Dispose did not release
        Assert.Equal(port, rebound.BoundEndPoint!.Port);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var ep = new LivenessEndpoint(Loopback0(), isHealthy: () => true);
        ep.Start();
        ep.Dispose();
        ep.Dispose();
    }
}
