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
        plane.PollOnce();
        plane.PollOnce();   // the frame is claimed; a second sweep must find nothing
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

        Assert.Equal(2, plane.PollOnce());
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
}
