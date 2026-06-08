using GlpRuntime.Link.Reliability;
using Xunit;

namespace GlpRuntime.Link.Tests;

/// <summary>T021 cycle-guard tests (FR-022/FR-028): cyclic term traversal terminates cleanly.</summary>
public class CycleGuardTests
{
    [Fact]
    public void NestedSameNode_Throws()
    {
        var guard = new CycleGuard();
        var node = new object();
        using (guard.Enter(node))
            Assert.Throws<CyclicTermException>(() => guard.Enter(node));
    }

    [Fact]
    public void Dag_SharedSubterm_Permitted()
    {
        // The same node visited under two siblings (not on the same active path) is fine.
        var guard = new CycleGuard();
        var shared = new object();
        using (guard.Enter(new object()))
        {
            using (guard.Enter(shared)) { }
            using (guard.Enter(shared)) { } // re-enter after leaving — DAG, not a cycle
        }
        Assert.Equal(0, guard.Depth);
    }

    [Fact]
    public void Depth_TracksActivePath()
    {
        var guard = new CycleGuard();
        Assert.Equal(0, guard.Depth);
        using (guard.Enter(new object()))
        {
            Assert.Equal(1, guard.Depth);
            using (guard.Enter(new object()))
                Assert.Equal(2, guard.Depth);
            Assert.Equal(1, guard.Depth);
        }
        Assert.Equal(0, guard.Depth);
    }
}
