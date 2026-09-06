// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using Ynet.Client;

namespace Ynet.Client.Tests;

/// <summary>
/// FR-001..FR-005 and FR-004a/b/c — which plane is live, said out loud, and what happens when the
/// requested one cannot be bound.
/// </summary>
public class PlaneSelectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ynet-plane-" + Guid.NewGuid().ToString("N"));

    public PlaneSelectionTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp cleanup */ }
        GC.SuppressFinalize(this);
    }

    private PlaneCatalog.Binding FileOnly() => new() { CoopRoot = _root, LaneDirectory = "lane-under-test" };

    // ---- FR-001: the request is parsed, and a typo is REFUSED rather than defaulted ----

    [Theory]
    [InlineData(null, PlaneCatalog.Plane.File)]
    [InlineData("", PlaneCatalog.Plane.File)]
    [InlineData("file", PlaneCatalog.Plane.File)]
    [InlineData("coop", PlaneCatalog.Plane.File)]
    [InlineData("wire", PlaneCatalog.Plane.Wire)]
    [InlineData("quic", PlaneCatalog.Plane.Wire)]
    [InlineData("both", PlaneCatalog.Plane.Both)]
    [InlineData("loopback", PlaneCatalog.Plane.Loopback)]
    [InlineData("  WIRE  ", PlaneCatalog.Plane.Wire)]
    public void Known_plane_requests_parse(string? text, PlaneCatalog.Plane expected) =>
        Assert.Equal(expected, PlaneCatalog.Parse(text));

    [Fact]
    public void An_unknown_plane_is_refused_not_silently_defaulted()
    {
        // A typo that silently selects the default is how a host ends up on a plane nobody chose —
        // which is the same family of defect as a client that reports "running" while bound to
        // loopback.
        var ex = Assert.Throws<ArgumentException>(() => PlaneCatalog.Parse("wrie"));
        Assert.Contains("wrie", ex.Message, StringComparison.Ordinal);
        Assert.Contains("file, wire, both, loopback", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_default_is_the_file_plane_so_no_deployment_moves_silently()
    {
        // The current behaviour must stay the default: this change must not move an existing
        // deployment onto a plane its operator did not ask for.
        Assert.Equal(PlaneCatalog.Plane.File, PlaneCatalog.Parse(null));
    }

    // ---- FR-002: the reported plane comes from the live object ----

    [Fact]
    public void The_reported_plane_is_read_from_the_bound_carrier()
    {
        var b = PlaneSelection.Bind(PlaneCatalog.Plane.File, FileOnly());
        try
        {
            Assert.Equal("coop-file-cross-lane", b.Inbound.PlaneName);
            Assert.Contains("plane=coop-file-cross-lane", b.RunningLine(), StringComparison.Ordinal);
        }
        finally { (b.Inbound as IDisposable)?.Dispose(); }
    }

    [Fact]
    public void Loopback_names_itself_distinctly_from_the_file_plane()
    {
        var loop = PlaneSelection.Bind(PlaneCatalog.Plane.Loopback, FileOnly());
        var file = PlaneSelection.Bind(PlaneCatalog.Plane.File, FileOnly());
        try
        {
            Assert.NotEqual(loop.Inbound.PlaneName, file.Inbound.PlaneName);
        }
        finally
        {
            (loop.Inbound as IDisposable)?.Dispose();
            (file.Inbound as IDisposable)?.Dispose();
        }
    }

    // ---- FR-004 / FR-004a: wire that cannot bind degrades, and SAYS SO on the running line ----

    [Fact]
    public void A_wire_request_that_cannot_bind_degrades_to_the_file_plane()
    {
        // No identity and no listener => the wire cannot be constructed. This is exactly the state
        // of a host whose QUIC certificate material has been destroyed — which has happened here
        // four separate times, so this is the common path, not the exotic one.
        var b = PlaneSelection.Bind(PlaneCatalog.Plane.Wire, FileOnly());
        try
        {
            Assert.True(b.IsDegraded);
            Assert.Equal(PlaneCatalog.Plane.Wire, b.Requested);
            Assert.Equal(PlaneCatalog.Plane.File, b.Live);
            Assert.Equal("coop-file-cross-lane", b.Inbound.PlaneName);
        }
        finally { (b.Inbound as IDisposable)?.Dispose(); }
    }

    [Fact]
    public void The_degradation_is_stated_on_the_line_that_says_running()
    {
        var b = PlaneSelection.Bind(PlaneCatalog.Plane.Wire, FileOnly());
        try
        {
            var line = b.RunningLine();
            // FR-004a: on the SAME line. A fallback the operator has to go looking for in a log is
            // a silent fallback.
            Assert.Contains("running", line, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DEGRADED", line, StringComparison.Ordinal);
            Assert.Contains("Wire", line, StringComparison.Ordinal);
            Assert.Contains("could NOT be bound", line, StringComparison.Ordinal);
        }
        finally { (b.Inbound as IDisposable)?.Dispose(); }
    }

    /// <summary>THE NEGATIVE CONTROL for the running line: a healthy bind must not shout.</summary>
    [Fact]
    public void A_healthy_bind_says_running_and_nothing_about_degradation()
    {
        var b = PlaneSelection.Bind(PlaneCatalog.Plane.File, FileOnly());
        try
        {
            Assert.False(b.IsDegraded);
            Assert.DoesNotContain("DEGRADED", b.RunningLine(), StringComparison.Ordinal);
        }
        finally { (b.Inbound as IDisposable)?.Dispose(); }
    }

    // ---- FR-004b: the fleet-visible notice ----

    [Fact]
    public void A_degradation_writes_a_fleet_visible_notice()
    {
        var notifier = new DegradedNotice(_root, "lane-under-test", host: "TESTHOST");
        var b = PlaneSelection.Bind(PlaneCatalog.Plane.Wire, FileOnly(), notifier);
        try
        {
            Assert.True(b.IsDegraded);
            Assert.NotNull(notifier.LastNoticePath);
            Assert.True(File.Exists(notifier.LastNoticePath));

            var json = File.ReadAllText(notifier.LastNoticePath!);
            Assert.Contains("ynet-plane-degraded", json, StringComparison.Ordinal);
            Assert.Contains("TESTHOST", json, StringComparison.Ordinal);
            Assert.Contains("\"requested\":\"Wire\"", json, StringComparison.Ordinal);
            Assert.Contains("\"live\":\"File\"", json, StringComparison.Ordinal);
        }
        finally { (b.Inbound as IDisposable)?.Dispose(); }
    }

    /// <summary>THE NEGATIVE CONTROL for the notice: a healthy bind must write nothing.</summary>
    [Fact]
    public void A_healthy_bind_writes_no_notice()
    {
        var notifier = new DegradedNotice(_root, "lane-under-test", host: "TESTHOST");
        var b = PlaneSelection.Bind(PlaneCatalog.Plane.File, FileOnly(), notifier);
        try
        {
            Assert.Null(notifier.LastNoticePath);
            Assert.False(Directory.Exists(Path.Combine(_root, DegradedNotice.DirectoryName)));
        }
        finally { (b.Inbound as IDisposable)?.Dispose(); }
    }

    [Fact]
    public void Two_degradations_in_the_same_second_both_survive()
    {
        // Never overwrite (the 2026-08-16 fan-out incident lost 2990 lines). Losing the earlier
        // event would hide exactly the flapping this record exists to make visible.
        var frozen = DateTimeOffset.Parse("2026-09-06T12:00:00Z");
        var notifier = new DegradedNotice(_root, "lane", host: "H", now: () => frozen);

        notifier.Degraded(PlaneCatalog.Plane.Wire, PlaneCatalog.Plane.File, "first");
        var first = notifier.LastNoticePath;
        notifier.Degraded(PlaneCatalog.Plane.Wire, PlaneCatalog.Plane.File, "second");
        var second = notifier.LastNoticePath;

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.Contains("first", File.ReadAllText(first!), StringComparison.Ordinal);
        Assert.Contains("second", File.ReadAllText(second!), StringComparison.Ordinal);
    }

    [Fact]
    public void A_notice_that_cannot_be_written_never_stops_the_client()
    {
        // The whole purpose of degrading is to keep a damaged host receiving. A notifier pointed at
        // an unwritable root must not be the reason it stops.
        var notifier = new DegradedNotice(
            Path.Combine(_root, "does", "not", "exist", "\0invalid"), "lane", host: "H");

        var b = PlaneSelection.Bind(PlaneCatalog.Plane.Wire, FileOnly(), notifier);
        try
        {
            Assert.True(b.IsDegraded);
            Assert.Equal(PlaneCatalog.Plane.File, b.Live);   // still running
            Assert.Null(notifier.LastNoticePath);            // and honest that nothing was written
        }
        finally { (b.Inbound as IDisposable)?.Dispose(); }
    }

    [Fact]
    public void A_client_with_no_coop_root_has_no_fleet_to_notify_and_says_so()
    {
        var notifier = new DegradedNotice(null, "lane");
        Assert.False(notifier.CanNotify);
        notifier.Degraded(PlaneCatalog.Plane.Wire, PlaneCatalog.Plane.File, "reason");
        Assert.Null(notifier.LastNoticePath);
    }

    // ---- FR-004c: the file plane has nothing below it ----

    [Fact]
    public void A_file_plane_that_cannot_bind_is_refused_not_degraded()
    {
        // No COOP root => no file plane. There is nothing lower to fall back to, so this must be a
        // refusal, never a quiet substitution.
        var ex = Assert.Throws<InvalidOperationException>(
            () => PlaneSelection.Bind(PlaneCatalog.Plane.File, new PlaneCatalog.Binding()));
        Assert.Contains("COOP root", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void When_neither_the_wire_nor_the_file_plane_can_bind_the_client_refuses_to_start()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PlaneSelection.Bind(PlaneCatalog.Plane.Wire, new PlaneCatalog.Binding()));
        Assert.Contains("no plane left", ex.Message, StringComparison.Ordinal);
        Assert.Contains("NOT STARTED", ex.Message, StringComparison.Ordinal);
    }

    // ---- FR-005: listener parsing ----

    [Fact]
    public void A_listen_address_is_parsed_into_a_config()
    {
        var cfg = PlaneCatalog.ParseListen("svc", "127.0.0.1:44300");
        Assert.NotNull(cfg);
        Assert.Equal(44300, cfg!.Value.Port);
        Assert.Equal("127.0.0.1", cfg.Value.BindAddress.ToString());
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.1:notaport")]
    [InlineData("not-an-ip:443")]
    public void A_malformed_listen_address_is_refused(string bad) =>
        Assert.Throws<ArgumentException>(() => PlaneCatalog.ParseListen("svc", bad));

    [Fact]
    public void No_listen_address_means_no_listener_rather_than_a_guessed_one() =>
        Assert.Null(PlaneCatalog.ParseListen("svc", null));

    // ---- the wire plane's own constructor validation (the CA2264 finding) ----

    [Fact]
    public void A_default_constructed_listener_config_is_refused_by_the_wire_plane()
    {
        // Until 2026-09-06 this constructor "validated" its config with
        // ArgumentNullException.ThrowIfNull on a readonly record struct — a check that CANNOT
        // throw. CA2264 had said so since the commit that introduced it. The dead check was hiding
        // the fact that a default-constructed config (null address, port 0) sailed straight
        // through to a listener that then failed far away from the caller who supplied it.
        var self = Ynet.Transport.Capability.NodeIdentity.Generate();
        Assert.Throws<ArgumentException>(
            () => new QuicInbound(self, default));
    }
}
