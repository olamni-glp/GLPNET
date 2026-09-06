// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using Ynet.Client;

namespace Ynet.Client.Tests;

/// <summary>
/// FR-022/023/023a/023b, SC-008 — both planes bound at once, de-duplicated by message id.
///
/// <para>
/// 🔴 <b>The positive test alone proves nothing here.</b> "Exactly one alert for a message that
/// arrived twice" is also what a de-duplicator that suppresses EVERYTHING produces for the first
/// message. That is why <see cref="One_plane_one_delivery_still_alerts"/> exists: it is the
/// negative control, and without it a suppressor passes the suite.
/// </para>
/// </summary>
public class CompositeInboundTests
{
    private static YnetMessage Msg(string id, string origin = "peer/actor") =>
        new(id, origin, "SIGNAL", ReadOnlyMemory<byte>.Empty);

    [Fact]
    public void Fans_out_open_and_close_to_every_inner_plane()
    {
        var a = new LoopbackInbound();
        var b = new LoopbackInbound();
        using var composite = new CompositeInbound("a+b", a, b);

        composite.Open();
        // Deliver returns false on a closed plane, so a successful deliver proves it was opened.
        Assert.True(a.Deliver(Msg("1")));
        Assert.True(b.Deliver(Msg("2")));

        composite.Close();
        Assert.False(a.Deliver(Msg("3")));
        Assert.False(b.Deliver(Msg("4")));
    }

    [Fact]
    public void Fans_in_arrivals_from_every_inner_plane()
    {
        var a = new LoopbackInbound();
        var b = new LoopbackInbound();
        using var composite = new CompositeInbound("a+b", a, b);

        var got = new List<string>();
        composite.Received += m => got.Add(m.MessageId);
        composite.Open();

        a.Deliver(Msg("from-a"));
        b.Deliver(Msg("from-b"));

        Assert.Equal(["from-a", "from-b"], got);
    }

    // ---- FR-023 / SC-008: the same id on both planes alerts once ----

    [Fact]
    public void Same_id_on_both_planes_alerts_exactly_once()
    {
        var file = new LoopbackInbound();
        var wire = new LoopbackInbound();
        using var composite = new CompositeInbound("file+wire", file, wire);

        var got = new List<string>();
        composite.Received += m => got.Add(m.MessageId);
        composite.Open();

        file.Deliver(Msg("shared-id"));
        wire.Deliver(Msg("shared-id"));

        Assert.Single(got);
        Assert.Equal(1, composite.Suppressed);
    }

    // ---- FR-023b: THE NEGATIVE CONTROL ----

    /// <summary>
    /// One plane, one delivery, one alert.
    ///
    /// 🔴 This is the test that catches a de-duplicator suppressing a FIRST sighting. Without it,
    /// an <c>IsFirstSighting</c> that always returned false would pass
    /// <see cref="Same_id_on_both_planes_alerts_exactly_once"/>'s intent in spirit (never more than
    /// one alert) while losing every message in the system. De-duplication must never be the reason
    /// a message is lost.
    /// </summary>
    [Fact]
    public void One_plane_one_delivery_still_alerts()
    {
        var only = new LoopbackInbound();
        using var composite = new CompositeInbound("file", only);

        var got = new List<string>();
        composite.Received += m => got.Add(m.MessageId);
        composite.Open();

        only.Deliver(Msg("solitary"));

        Assert.Single(got);
        Assert.Equal(0, composite.Suppressed);
    }

    [Fact]
    public void Distinct_ids_all_alert()
    {
        var file = new LoopbackInbound();
        var wire = new LoopbackInbound();
        using var composite = new CompositeInbound("file+wire", file, wire);

        var got = new List<string>();
        composite.Received += m => got.Add(m.MessageId);
        composite.Open();

        for (var i = 0; i < 50; i++) file.Deliver(Msg($"f{i}"));
        for (var i = 0; i < 50; i++) wire.Deliver(Msg($"w{i}"));

        Assert.Equal(100, got.Count);
        Assert.Equal(0, composite.Suppressed);
    }

    // ---- FR-023a: the mutation proof, driven directly at the neuterable seam ----

    /// <summary>
    /// FR-023a asks that neutering the de-duplicator make a test fail. <c>IsFirstSighting</c> is
    /// the seam to neuter, and these two assertions are what would fail in each direction:
    ///
    /// <list type="bullet">
    ///   <item>always-true (de-duplication off) → the second call returns true → first assert fails</item>
    ///   <item>always-false (suppress everything) → the first call returns false → second assert fails</item>
    /// </list>
    ///
    /// Both directions are asserted because a mutation proof that only covers one of them cannot
    /// distinguish "works" from "suppresses everything".
    /// </summary>
    [Fact]
    public void Deduplicator_is_true_once_then_false_for_the_same_id()
    {
        using var composite = new CompositeInbound("solo", new LoopbackInbound());

        Assert.True(composite.IsFirstSighting("id-1"));   // fails under always-false
        Assert.False(composite.IsFirstSighting("id-1"));  // fails under always-true
        Assert.True(composite.IsFirstSighting("id-2"));   // fails under always-false
    }

    [Fact]
    public void An_id_less_message_is_delivered_rather_than_swallowed()
    {
        using var composite = new CompositeInbound("solo", new LoopbackInbound());

        // A message with no id cannot be de-duplicated. Delivering it is the safe direction:
        // a duplicate alert is visible; a dropped message is not.
        Assert.True(composite.IsFirstSighting(""));
        Assert.True(composite.IsFirstSighting(""));
    }

    // ---- the seen-set is bounded ----

    [Fact]
    public void The_seen_set_is_bounded_and_evicts_oldest_first()
    {
        using var composite = new CompositeInbound("solo", new LoopbackInbound());

        Assert.True(composite.IsFirstSighting("oldest"));
        for (var i = 0; i < CompositeInbound.SeenCapacity; i++)
            composite.IsFirstSighting($"filler-{i}");

        // "oldest" has been evicted, so it reads as a first sighting again. That is the honest,
        // stated consequence of a bounded set (see the class doc): a duplicate is visible and
        // recoverable, an unbounded set is a remote memory-exhaustion primitive.
        Assert.True(composite.IsFirstSighting("oldest"));

        // The most recent filler is still remembered — eviction is oldest-first, not a wipe.
        Assert.False(composite.IsFirstSighting($"filler-{CompositeInbound.SeenCapacity - 1}"));
    }

    // ---- refusals ----

    [Fact]
    public void A_composite_with_no_inner_planes_is_refused()
    {
        // Such a composite receives nothing while reporting that it is a plane — the exact shape of
        // defect this whole feature exists to remove.
        Assert.Throws<ArgumentException>(() => new CompositeInbound("empty"));
    }

    [Fact]
    public void A_null_inner_plane_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new CompositeInbound("bad", new LoopbackInbound(), null!));
    }

    [Fact]
    public void Plane_name_and_inner_planes_are_observable()
    {
        var a = new LoopbackInbound();
        var b = new LoopbackInbound();
        using var composite = new CompositeInbound("file+wire", a, b);

        Assert.Equal("file+wire", composite.PlaneName);
        Assert.Equal(2, composite.Planes.Count);
        // Status output must be able to name what is ACTUALLY live, not just the composite label.
        Assert.Equal("in-memory-intercom", composite.Planes[0].PlaneName);
    }

    [Fact]
    public void Open_and_close_are_idempotent()
    {
        var a = new LoopbackInbound();
        using var composite = new CompositeInbound("solo", a);

        composite.Open();
        composite.Open();
        Assert.True(a.Deliver(Msg("x")));

        composite.Close();
        composite.Close();
        Assert.False(a.Deliver(Msg("y")));
    }
}
