using GlpRuntime.Runtime;
using GlpRuntime.Link.Reliability;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// T017 (feature 062 US3) — the receiver path: compile-on-A → frame + send →
/// reassemble + execute-on-B, with B's result EQUAL to a local execution of the same
/// program (contract obligation 4 / FR-005). The compiled IL is framed in the T016
/// <see cref="CompiledIlEnvelope"/> and rides the GlpLink <see cref="FrameCodec"/>
/// wire — both as one <see cref="FrameKind.Whole"/> frame and split into
/// <see cref="FrameKind.Fragment"/>s — exactly as a real transport carries it. B
/// verifies + decodes ONLY a fully reassembled frame before executing. Equivalence
/// holds across every runnable status (succeed / fail / suspend).
/// </summary>
[Trait("Category", "ReceiverExecute")]
public class ReceiverExecuteOnBTests
{
    // Full A → wire → B path; returns B's execution status.
    private static async Task<ExecutionStatus> ExecuteOnBAsync(CorpusCase c, int? maxFrameBytes = null)
    {
        // A: compiled program → wrap in the envelope → frame it for the wire.
        byte[] envelope = CompiledIlEnvelopeCodec.Wrap(c.Program, "wire/" + c.Name, c.VariableMap);
        var frames = maxFrameBytes is null
            ? FrameCodec.Encode(envelope, messageId: 0x5100)
            : FrameCodec.Encode(envelope, messageId: 0x5100, maxFrameBytes: maxFrameBytes.Value);

        // B: reassemble the wire frames back into the whole envelope.
        var reasm = new FrameReassembler();
        byte[]? whole = null;
        foreach (var f in frames)
            whole = reasm.Accept(FrameCodec.ParseFrame(f)) ?? whole;
        Assert.NotNull(whole);

        // B: verify + decode the envelope, then execute the recovered program.
        var (decoded, _) = CompiledIlEnvelopeCodec.Unwrap(whole!);
        return await GlpExecutor.RunNullaryAsync(decoded.Program, c.Goal!);
    }

    public static IEnumerable<object[]> RunnableCases =>
        Corpus.Runnable().Select(c => new object[] { c.Name });

    [Theory]
    [MemberData(nameof(RunnableCases))]
    public async Task Execute_on_B_equals_local_whole_frame(string name)
    {
        var c = Corpus.ByName(name);
        var local = await GlpExecutor.RunNullaryAsync(c.Program, c.Goal!);
        var onB = await ExecuteOnBAsync(c);
        Assert.Equal(local, onB);
        Assert.Equal(c.ExpectedStatus, onB); // anchor: the goal really reaches the intended status
    }

    [Theory]
    [MemberData(nameof(RunnableCases))]
    public async Task Execute_on_B_equals_local_fragmented(string name)
    {
        var c = Corpus.ByName(name);
        var local = await GlpExecutor.RunNullaryAsync(c.Program, c.Goal!);
        var onB = await ExecuteOnBAsync(c, maxFrameBytes: FrameCodec.HeaderSize + 16);
        Assert.Equal(local, onB);
        Assert.Equal(c.ExpectedStatus, onB);
    }
}
