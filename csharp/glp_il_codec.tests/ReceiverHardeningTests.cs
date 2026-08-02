using GlpRuntime.Link.Reliability;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// T018 (feature 062 US3, FR-005a) — receiver hardening at the wire boundary. The
/// frame-level obligations 1-2 (reject incompatible <c>il_version</c> / digest
/// mismatch) are enforced inside <see cref="CompiledIlEnvelopeCodec.Decode"/> and
/// pinned by <c>CompiledIlEnvelopeTests</c>; here they are re-proven where they
/// matter — a corrupted or version-incompatible frame arriving over the wire is
/// rejected BEFORE any execution. Obligation 3: a transport failure mid-transfer
/// (missing fragments) never yields a whole frame, so B never decodes or executes —
/// the engine is untouched — and a subsequent valid transfer still executes to the
/// same result as local.
/// </summary>
[Trait("Category", "ReceiverHardening")]
public class ReceiverHardeningTests
{
    private static byte[] WireEnvelope(CorpusCase c, string? ilVersion = null) =>
        CompiledIlEnvelopeCodec.Wrap(c.Program, "wire/" + c.Name, c.VariableMap, ilVersion);

    // Reassemble a (possibly partial) frame list; null while not yet whole.
    private static byte[]? Reassemble(IReadOnlyList<byte[]> frames)
    {
        var reasm = new FrameReassembler();
        byte[]? whole = null;
        foreach (var f in frames)
            whole = reasm.Accept(FrameCodec.ParseFrame(f)) ?? whole;
        return whole;
    }

    [Fact]
    public void Mid_transfer_failure_never_reaches_execution()
    {
        var c = Corpus.ByName("succeed");
        var frames = FrameCodec.Encode(WireEnvelope(c), messageId: 0x6001,
            maxFrameBytes: FrameCodec.HeaderSize + 16);
        Assert.True(frames.Count > 1, "envelope should fragment under a small MTU");

        // Drop the last fragment: the transfer fails mid-flight.
        var partial = frames.Take(frames.Count - 1).ToList();

        // No whole frame is ever produced → B must not decode or execute.
        Assert.Null(Reassemble(partial));
    }

    [Fact]
    public void Corrupted_frame_in_transit_is_rejected_before_execution()
    {
        var c = Corpus.ByName("succeed");
        var whole = Reassemble(FrameCodec.Encode(WireEnvelope(c), messageId: 0x6002));
        Assert.NotNull(whole);

        whole![^1] ^= 0xFF; // bit-rot in the compiled_form region
        Assert.Throws<IlCodecException>(() => CompiledIlEnvelopeCodec.Unwrap(whole!));
    }

    [Fact]
    public void Incompatible_version_in_transit_is_rejected_before_execution()
    {
        var c = Corpus.ByName("succeed");
        var whole = Reassemble(FrameCodec.Encode(WireEnvelope(c, ilVersion: "9.9.9"), messageId: 0x6003));
        Assert.NotNull(whole);
        Assert.Throws<IlCodecException>(() => CompiledIlEnvelopeCodec.Unwrap(whole!));
    }

    [Fact]
    public async Task A_valid_transfer_after_a_rejected_one_still_executes_to_local()
    {
        var c = Corpus.ByName("succeed");

        // A rejected transfer (corrupt) — throws, no execution.
        var bad = Reassemble(FrameCodec.Encode(WireEnvelope(c), messageId: 0x6004))!;
        bad[^1] ^= 0xFF;
        Assert.Throws<IlCodecException>(() => CompiledIlEnvelopeCodec.Unwrap(bad));

        // A subsequent valid transfer is unaffected and executes == local.
        var good = Reassemble(FrameCodec.Encode(WireEnvelope(c), messageId: 0x6005))!;
        var (decoded, _) = CompiledIlEnvelopeCodec.Unwrap(good);
        var onB = await GlpExecutor.RunNullaryAsync(decoded.Program, c.Goal!);
        var local = await GlpExecutor.RunNullaryAsync(c.Program, c.Goal!);
        Assert.Equal(local, onB);
    }
}
