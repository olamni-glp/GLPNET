using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using Xunit;

namespace GlpRuntime.Link.Tests;

/// <summary>T024 distributed-GC tests (FR-024, SC-014).</summary>
public class ReclaimTests
{
    private static LinkId Link(int n) =>
        new(LinkScheme.Loopback, LinkAddress.Path($"c{n}"), LinkNonce.Int(n));

    [Fact]
    public void Reclaim_RunsHooks_InOrder()
    {
        var r = new LinkReclaimer();
        var order = new List<int>();
        var link = Link(1);
        r.Register(link, () => order.Add(1));
        r.Register(link, () => order.Add(2));
        r.Register(link, () => order.Add(3));

        Assert.True(r.Reclaim(link, "permFail"));
        Assert.Equal(new[] { 1, 2, 3 }, order);
        Assert.True(r.IsReclaimed(link));
        Assert.Equal(0, r.PendingLinkCount);
    }

    [Fact]
    public void Reclaim_IsIdempotent()
    {
        var r = new LinkReclaimer();
        int count = 0;
        var link = Link(2);
        r.Register(link, () => count++);

        Assert.True(r.Reclaim(link, "permFail"));   // close after permFail must not re-run
        Assert.False(r.Reclaim(link, "close"));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Links_AreIndependent()
    {
        var r = new LinkReclaimer();
        bool a = false, b = false;
        r.Register(Link(1), () => a = true);
        r.Register(Link(2), () => b = true);

        r.Reclaim(Link(1), "close");
        Assert.True(a);
        Assert.False(b);
        Assert.Equal(1, r.PendingLinkCount);
    }

    [Fact]
    public void Reclaim_RunsAllHooks_EvenIfOneThrows()
    {
        var r = new LinkReclaimer();
        bool first = false, third = false;
        var link = Link(3);
        r.Register(link, () => first = true);
        r.Register(link, () => throw new InvalidOperationException("boom"));
        r.Register(link, () => third = true);

        var ex = Assert.Throws<ReclaimException>(() => r.Reclaim(link, "permFail"));
        Assert.True(first);
        Assert.True(third); // ran despite the middle hook throwing — no leak
        Assert.Single(ex.InnerExceptions);
    }

    [Fact]
    public void LateRegistration_AfterReclaim_RunsImmediately()
    {
        var r = new LinkReclaimer();
        var link = Link(4);
        r.Reclaim(link, "permFail");

        bool ran = false;
        r.Register(link, () => ran = true); // straggler allocation after teardown
        Assert.True(ran);
    }

    [Fact]
    public void Snapshot_ReturnsToBaseline_AfterReclaim()
    {
        // A fake subsystem whose counters mirror W_p / send / onBind / reply.
        var baseline = ResourceSnapshot.Zero;
        int wp = 0, send = 0, onBind = 0, reply = 0;
        var r = new LinkReclaimer();
        var link = Link(5);

        // open: allocate one of each, each with its reclamation hook
        wp++; send++; onBind++; reply++;
        r.Register(link, () => wp--);
        r.Register(link, () => send--);
        r.Register(link, () => onBind--);
        r.Register(link, () => reply--);

        var afterOpen = new ResourceSnapshot(wp, send, onBind, reply);
        Assert.False(afterOpen.IsBaseline(baseline));

        r.Reclaim(link, "permFail");

        var afterReclaim = new ResourceSnapshot(wp, send, onBind, reply);
        Assert.True(afterReclaim.IsBaseline(baseline));
    }
}
