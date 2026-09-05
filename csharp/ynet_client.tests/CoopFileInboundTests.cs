// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Text;
using Ynet.Client;

namespace Ynet.Client.Tests;

/// <summary>
/// The cross-lane plane. Until this carrier existed, "run" bound LoopbackInbound and this lane
/// could only receive what it manufactured for itself - M6 was met in shape and not in substance.
/// </summary>
public sealed class CoopFileInboundTests : IDisposable
{
    private readonly string _root;

    public CoopFileInboundTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "coop-inbound-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }

    private CoopFileInbound Plane(string lane = "glpnet~4b0d1757bc75")
        => new(_root, lane, TimeSpan.FromMilliseconds(20));

    private void DropFrame(string lane, string name, string body)
    {
        var inbox = Path.Combine(_root, lane, "inbox");
        Directory.CreateDirectory(inbox);
        File.WriteAllText(Path.Combine(inbox, name), body);
    }

    [Fact]
    public void A_frame_dropped_in_the_inbox_is_delivered_with_its_body_and_origin()
    {
        using var plane = Plane();
        var got = new List<YnetMessage>();
        plane.Received += got.Add;
        plane.EnsureMailbox();

        DropFrame("glpnet~4b0d1757bc75", "shiras-glpnet--0001.frame", "hello");
        // Two sweeps: the first observes the length, the second corroborates it. A frame is never
        // delivered on a single sighting, because a single sighting cannot tell a complete frame
        // from one still being written.
        Assert.Equal(0, plane.PollOnce());
        Assert.Equal(1, plane.PollOnce());

        var msg = Assert.Single(got);
        Assert.Equal("shiras-glpnet", msg.Origin);
        Assert.Equal("shiras-glpnet--0001", msg.MessageId);
        Assert.Equal("hello", Encoding.UTF8.GetString(msg.Body.Span));
    }

    [Fact]
    public void A_delivered_frame_is_claimed_and_never_delivered_twice()
    {
        using var plane = Plane();
        var count = 0;
        plane.Received += _ => count++;
        plane.EnsureMailbox();

        DropFrame("glpnet~4b0d1757bc75", "peer--a.frame", "x");
        plane.PollOnce();   // observe length
        plane.PollOnce();   // corroborate -> deliver + claim
        plane.PollOnce();   // claimed; further sweeps must find nothing
        plane.PollOnce();

        Assert.Equal(1, count);
    }

    // A3 (shiras-glpnet 2026-09-05T15:10Z): the canonical carrier enumerates *.frame only, so a
    // mis-addressed non-frame file was not refused - it was NOT SEEN, and two ACK-mandatory
    // broadcasts sat unread while frames_refused stayed 0. Here a stray is named.
    [Fact]
    public void A_stray_non_frame_file_is_counted_and_named_rather_than_silently_ignored()
    {
        using var plane = Plane();
        var strays = new List<string>();
        plane.StrayObserved += strays.Add;
        plane.EnsureMailbox();

        DropFrame("glpnet~4b0d1757bc75", "BROADCAST-ACK-MANDATORY.md", "somebody mis-addressed this");
        plane.PollOnce();

        Assert.Equal(1, plane.StrayCount);
        Assert.Contains("BROADCAST-ACK-MANDATORY.md", Assert.Single(strays));
        Assert.Contains(plane.Strays, s => s.EndsWith("BROADCAST-ACK-MANDATORY.md", StringComparison.Ordinal));
    }

    [Fact]
    public void A_stray_is_reported_once_not_on_every_poll()
    {
        using var plane = Plane();
        var raised = 0;
        plane.StrayObserved += _ => raised++;
        plane.EnsureMailbox();

        DropFrame("glpnet~4b0d1757bc75", "notes.txt", "x");
        plane.PollOnce();
        plane.PollOnce();
        plane.PollOnce();

        Assert.Equal(1, raised);      // one observation
        Assert.Equal(1, plane.StrayCount);
    }

    // NEGATIVE CONTROL for the test above: a stray must not be mistaken for a delivery. Without
    // this, "delivered 0" and "saw nothing at all" would be indistinguishable.
    [Fact]
    public void A_stray_is_not_delivered_as_a_message()
    {
        using var plane = Plane();
        var got = 0;
        plane.Received += _ => got++;
        plane.EnsureMailbox();

        DropFrame("glpnet~4b0d1757bc75", "notes.txt", "x");
        Assert.Equal(0, plane.PollOnce());
        Assert.Equal(0, got);
        Assert.Equal(1, plane.StrayCount);   // it WAS seen - just not as a message
    }

    [Fact]
    public void Frames_and_strays_in_one_inbox_are_separated_not_confused()
    {
        using var plane = Plane();
        var got = new List<YnetMessage>();
        plane.Received += got.Add;
        plane.EnsureMailbox();

        DropFrame("glpnet~4b0d1757bc75", "a--1.frame", "1");
        DropFrame("glpnet~4b0d1757bc75", "b--2.frame", "2");
        DropFrame("glpnet~4b0d1757bc75", "README.md", "not a frame");

        Assert.Equal(0, plane.PollOnce());   // lengths observed
        Assert.Equal(2, plane.PollOnce());   // lengths corroborated -> delivered
        Assert.Equal(2, got.Count);
        Assert.Equal(1, plane.StrayCount);
    }

    [Fact]
    public void A_frame_name_with_no_origin_is_reported_unknown_never_guessed()
    {
        Assert.Equal("unknown", CoopFileInbound.OriginFromFrameName("0001"));
        Assert.Equal("unknown", CoopFileInbound.OriginFromFrameName("--leading"));
        Assert.Equal("shiras-glpnet", CoopFileInbound.OriginFromFrameName("shiras-glpnet--0001"));
    }

    [Fact]
    public void Open_and_Close_are_idempotent()
    {
        using var plane = Plane();
        plane.Open();
        plane.Open();     // must not start a second pump
        plane.Close();
        plane.Close();    // must not throw on an already-stopped plane
    }

    [Fact]
    public void EnsureMailbox_creates_the_inbox_so_a_peer_can_address_this_lane_before_it_first_runs()
    {
        using var plane = Plane();
        Assert.False(Directory.Exists(plane.InboxDirectory));
        plane.EnsureMailbox();
        Assert.True(Directory.Exists(plane.InboxDirectory));
    }

    // The plane must survive a coop root that is not mounted - the normal state of H:/I:/J: on this
    // host - by reporting the failure and continuing, never by throwing out of the pump.
    [Fact]
    public void A_missing_inbox_reports_a_poll_failure_and_does_not_throw()
    {
        var plane = new CoopFileInbound(Path.Combine(_root, "not-mounted"), "lane~x");
        var failures = new List<Exception>();
        plane.PollFailed += failures.Add;

        var delivered = plane.PollOnce();   // never Open()ed, so the directory does not exist

        Assert.Equal(0, delivered);
        Assert.Single(failures);
    }

    [Fact]
    public void The_plane_names_itself_as_cross_lane_so_status_cannot_confuse_it_with_loopback()
    {
        using var plane = Plane();
        Assert.Equal("coop-file-cross-lane", plane.PlaneName);
        Assert.NotEqual(new LoopbackInbound().PlaneName, plane.PlaneName);
    }

    [Fact]
    public void The_background_pump_delivers_without_an_explicit_poll()
    {
        using var plane = Plane();
        var seen = new ManualResetEventSlim(false);
        plane.Received += _ => seen.Set();
        plane.Open();     // the pump is the only sweeper here - no hand poll to race it

        DropFrame("glpnet~4b0d1757bc75", "peer--live.frame", "x");

        Assert.True(seen.Wait(TimeSpan.FromSeconds(5)), "the pump did not deliver within 5s");
    }

    // ---- codexreview 2026-09-05 regressions ----

    // P1: Path.Combine does NOT confine. "..\victim" escaped the coop root entirely, and a rooted
    // lane discarded it. Both values come from environment variables, so this was reachable config.
    [Theory]
    [InlineData("../victim")]
    [InlineData(@"..\victim")]
    [InlineData("sub/../../victim")]
    public void A_lane_directory_that_escapes_the_coop_root_is_refused(string lane)
    {
        Assert.Throws<ArgumentException>(() => new CoopFileInbound(_root, lane));
    }

    [Fact]
    public void An_absolute_lane_directory_is_refused_rather_than_silently_replacing_the_root()
    {
        var rooted = Path.Combine(Path.GetTempPath(), "elsewhere-" + Guid.NewGuid().ToString("n"));
        Assert.Throws<ArgumentException>(() => new CoopFileInbound(_root, rooted));
    }

    [Fact]
    public void A_nested_lane_directory_inside_the_root_is_still_allowed()
    {
        using var plane = new CoopFileInbound(_root, "_m6/glpnet~4b0d1757bc75");
        plane.EnsureMailbox();
        Assert.True(Directory.Exists(plane.InboxDirectory));
        Assert.StartsWith(Path.GetFullPath(_root), plane.InboxDirectory, StringComparison.OrdinalIgnoreCase);
    }

    // P1: a frame still being written could be claimed and delivered TRUNCATED as if complete -
    // a silent wrong answer. Delivery now requires the length to be stable across two sweeps.
    [Fact]
    public void A_frame_that_is_still_growing_is_not_delivered_until_its_length_settles()
    {
        using var plane = Plane();
        var got = new List<YnetMessage>();
        plane.Received += got.Add;
        plane.EnsureMailbox();

        var inbox = Path.Combine(_root, "glpnet~4b0d1757bc75", "inbox");
        var f = Path.Combine(inbox, "peer--partial.frame");
        File.WriteAllText(f, "half");

        Assert.Equal(0, plane.PollOnce());          // first sighting - length not yet corroborated
        Assert.Empty(got);

        File.WriteAllText(f, "half and the rest");  // it GREW: still not stable
        Assert.Equal(0, plane.PollOnce());
        Assert.Empty(got);

        Assert.Equal(1, plane.PollOnce());          // unchanged since last sweep - now it is complete
        Assert.Equal("half and the rest", Encoding.UTF8.GetString(Assert.Single(got).Body.Span));
    }

    // P1 DISCLOSED, NOT FIXED: origin comes from a filename the SENDER chooses. This test exists to
    // demonstrate the spoof rather than hide it. Authenticating a sender needs a signed envelope,
    // which belongs to the canonical client (Q-glpnetshiras-50), not to a rival built here.
    [Fact]
    public void DISCLOSED_origin_is_unauthenticated_and_a_peer_can_spoof_another_lanes_name()
    {
        using var plane = Plane();
        var got = new List<YnetMessage>();
        plane.Received += got.Add;
        plane.EnsureMailbox();

        // Written by ANYONE who can reach the inbox - the name is the only evidence of sender.
        DropFrame("glpnet~4b0d1757bc75", "shiras-glpnet--urgent.frame", "not actually from shiras");
        plane.PollOnce(); plane.PollOnce();

        Assert.Equal("shiras-glpnet", Assert.Single(got).Origin);   // <- the spoof succeeds
        Assert.False(plane.OriginIsAuthenticated);                  // <- and the plane SAYS so
    }

    // P2: two concurrent Open() calls both passed the null check, started two pumps, and Close()
    // then cancelled only the last token - leaving one pump running past disposal.
    [Fact]
    public void Concurrent_Open_calls_start_exactly_one_pump()
    {
        using var plane = Plane();
        Parallel.For(0, 16, _ => plane.Open());
        plane.Close();

        // If a second pump had survived Close(), it would keep claiming frames after shutdown.
        DropFrame("glpnet~4b0d1757bc75", "peer--afterclose.frame", "x");
        Thread.Sleep(200);
        Assert.True(File.Exists(Path.Combine(_root, "glpnet~4b0d1757bc75", "inbox", "peer--afterclose.frame")));
    }

    // P2: retained stray NAMES are bounded, but the reported TOTAL must stay exact - otherwise
    // bounding the leak would quietly bound the number an operator reads.
    [Fact]
    public void Stray_names_are_bounded_but_the_reported_total_stays_exact()
    {
        using var plane = Plane();
        plane.EnsureMailbox();

        var n = CoopFileInbound.MaxRetainedStrayNames + 50;
        for (var i = 0; i < n; i++) DropFrame("glpnet~4b0d1757bc75", $"stray-{i}.txt", "x");
        plane.PollOnce();

        Assert.Equal(n, plane.StrayCount);                                  // exact total
        Assert.True(plane.Strays.Count <= CoopFileInbound.MaxRetainedStrayNames); // bounded names
    }
}
