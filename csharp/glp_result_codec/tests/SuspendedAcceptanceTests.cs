// US1 Acceptance #3 (T025): a suspended goal emits Status=Suspended + the blocking-
// reader set, and no heap address leaks — the blocking readers and any remaining
// variable are GlobalVarId(AgentId, LocalId), never a bare heap address. Codec-level
// assertion over the shared corpus (survives Encode → Decode).

using System.Linq;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.ResultCodec.Tests;

public class SuspendedAcceptanceTests
{
    [Fact]
    public void Suspended_status_and_blocking_readers_survive_roundtrip()
    {
        var env = Corpus.ByName("suspended");
        var decoded = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(env));

        Assert.Equal(ExecutionStatus.Suspended, decoded.Status);
        Assert.Equal(
            new[] { new GlobalVarId("agent1", 3), new GlobalVarId("agent2", 5) },
            decoded.Suspended);
        // no heap-address leak: each blocking reader is a global id with a real agent id
        Assert.All(decoded.Suspended, id => Assert.False(string.IsNullOrEmpty(id.AgentId)));
    }

    [Fact]
    public void Suspended_with_binding_carries_partial_binding_and_var_to_writer()
    {
        var env = Corpus.ByName("suspended_with_binding");
        var decoded = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(env));

        Assert.Equal(ExecutionStatus.Suspended, decoded.Status);
        Assert.Equal(new[] { new GlobalVarId("agent1", 11) }, decoded.Suspended);
        Assert.Equal(new GlobalVarId("agent1", 11),
            decoded.VarToWriter.Single(kv => kv.Key == "Q").Value);

        // the remaining variable inside the binding is a VarRef carrying a GlobalVarId.
        var partial = Assert.IsType<StructTerm>(
            decoded.ResolvedBindings.Single(kv => kv.Key == "Partial").Value);
        var inner = Assert.IsType<VarRef>(partial.Args.Single());
        Assert.Equal(new GlobalVarId("agent1", 11), inner.Id);
    }
}
