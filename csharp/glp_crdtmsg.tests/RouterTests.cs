// Router payload-opacity + dedup + fixed-policy fail-loud (feature 041-crdtmsg-mvp, T045).

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Headers;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.CrdtMsg.Route;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class RouterTests
{
    [Fact]
    public void Payload_is_forwarded_verbatim_not_re_encoded()
    {
        Op op = LwwMap.Set("a", 1, "k", "v");
        byte[] opBytes = OpCodec.Encode(op);
        var msg = new Message(1, PayloadType.CrdtMessage,
            new Header("m", "a", "b", 0, RoutingPolicy.Empty),
            new[] { new Section(UnifiedHeader.OpSectionType, opBytes) }, CrdtModel.OpBased);

        Message decoded = MessageCodec.Binary.Decode(MessageCodec.Binary.Encode(msg));
        Assert.Equal(opBytes, UnifiedHeader.OpBytes(decoded)); // byte-for-byte, relay never re-encodes
    }

    [Fact]
    public void Dedup_suppresses_duplicate_msg_id()
    {
        var d = new Dedup();
        Assert.True(d.Accept("m1", "A", 0));
        Assert.False(d.Accept("m1", "A", 0)); // duplicate msg_id
        Assert.True(d.Accept("m2", "A", 1));
        Assert.Equal(1, d.HighWater("A"));
    }

    [Fact]
    public void Unsatisfiable_policy_fails_loud()
    {
        var policy = new RoutingPolicy(new[] { "X" }, Array.Empty<string>(), new[] { "X" }); // target excluded
        var reachable = new HashSet<string> { "X" };
        var decision = PolicyMatcher.Evaluate(policy, reachable);
        Assert.False(decision.Deliver);
        Assert.Equal(DropReason.NoRoute, decision.Drop); // logged, never silent
        Assert.Throws<CrdtMsgException>(() => PolicyMatcher.EvaluateOrThrow(policy, reachable));
    }

    [Fact]
    public void No_reachable_target_is_a_no_route_drop()
    {
        var policy = new RoutingPolicy(new[] { "B" }, Array.Empty<string>(), Array.Empty<string>());
        Assert.False(PolicyMatcher.Evaluate(policy, new HashSet<string> { "C" }).Deliver);
        Assert.True(PolicyMatcher.Evaluate(policy, new HashSet<string> { "B" }).Deliver);
    }
}
