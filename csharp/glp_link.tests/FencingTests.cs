using GlpRuntime.Link.Reliability;
using Xunit;

namespace GlpRuntime.Link.Tests;

/// <summary>T023 epoch/fencing split-brain tests (FR-047, SC-011).</summary>
public class FencingTests
{
    [Fact]
    public void FirstWriter_Admitted()
    {
        var fence = new FencingRegistry();
        Assert.Equal(FenceVerdict.Admit, fence.Admit("_w(p,0)", epoch: 1));
        Assert.Equal(1ul, fence.HighestEpochFor("_w(p,0)"));
    }

    [Fact]
    public void StaleResumedWriter_FencedAfterTakeover()
    {
        // Newer writer (epoch 2) takes over, then the partitioned writer (epoch 1) resumes.
        var fence = new FencingRegistry();
        Assert.Equal(FenceVerdict.Admit, fence.Admit("_w(p,0)", epoch: 2));
        Assert.Equal(FenceVerdict.Fenced, fence.Admit("_w(p,0)", epoch: 1)); // stale → permFail
    }

    [Fact]
    public void HigherEpoch_LegitimateTakeover_Admitted()
    {
        var fence = new FencingRegistry();
        fence.Admit("_w(p,0)", epoch: 1);
        Assert.Equal(FenceVerdict.Admit, fence.Admit("_w(p,0)", epoch: 5)); // takeover
        Assert.Equal(5ul, fence.HighestEpochFor("_w(p,0)"));
    }

    [Fact]
    public void EqualEpoch_Idempotent_Admitted()
    {
        var fence = new FencingRegistry();
        fence.Admit("_w(p,0)", epoch: 3);
        Assert.Equal(FenceVerdict.Admit, fence.Admit("_w(p,0)", epoch: 3));
    }

    [Fact]
    public void CompetingWriters_ExactlyOneWins()
    {
        // Two writers race for one name; the higher epoch wins, the lower is fenced.
        var fence = new FencingRegistry();
        Assert.Equal(FenceVerdict.Admit, fence.Admit("_w(p,7)", epoch: 9));
        Assert.Equal(FenceVerdict.Fenced, fence.Admit("_w(p,7)", epoch: 4));
        Assert.Equal(FenceVerdict.Fenced, fence.Admit("_w(p,7)", epoch: 8));
        Assert.Equal(9ul, fence.HighestEpochFor("_w(p,7)"));
    }

    [Fact]
    public void DistinctNames_Independent()
    {
        var fence = new FencingRegistry();
        fence.Admit("_w(p,0)", epoch: 5);
        Assert.Equal(FenceVerdict.Admit, fence.Admit("_w(p,1)", epoch: 1)); // different name unaffected
    }

    [Fact]
    public void Forget_ResetsName()
    {
        var fence = new FencingRegistry();
        fence.Admit("_w(p,0)", epoch: 5);
        fence.Forget("_w(p,0)");
        Assert.Null(fence.HighestEpochFor("_w(p,0)"));
        Assert.Equal(FenceVerdict.Admit, fence.Admit("_w(p,0)", epoch: 1)); // fresh again
    }

    [Fact]
    public void EpochAllocator_IsMonotone()
    {
        var alloc = new EpochAllocator();
        Assert.Equal(1ul, alloc.Next());
        Assert.Equal(2ul, alloc.Next());
        Assert.Equal(3ul, alloc.Next());
    }
}
