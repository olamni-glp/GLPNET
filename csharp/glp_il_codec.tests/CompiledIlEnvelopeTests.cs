using GlpRuntime.Engine;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// T016 (feature 062 US3) — the compiled-IL wire envelope. Round-trip of the four
/// contract fields (il_version, compiled_form, integrity_digest, source_metadata),
/// execute-equivalence of the unwrapped program (contract obligation 4 at the codec
/// level), and the frame-level receiver obligations 1-2: an unknown/incompatible
/// il_version and a digest mismatch are both rejected loud WITHOUT surfacing a
/// program to execute. (Obligation 3 — mid-transfer abort leaves engine state
/// unchanged — is a transport-level property proven by the receiver tests, T018.)
/// Contract: specs/062-.../contracts/compiled-il-wire-envelope.md.
/// </summary>
[Trait("Category", "Envelope")]
public class CompiledIlEnvelopeTests
{
    [Fact]
    public void Wrap_then_Unwrap_round_trips_program_and_metadata()
    {
        var c = Corpus.ByName("succeed");
        byte[] frame = CompiledIlEnvelopeCodec.Wrap(c.Program, "test/succeed", c.VariableMap);

        var (decoded, env) = CompiledIlEnvelopeCodec.Unwrap(frame);

        Assert.Equal(CompiledIlEnvelopeCodec.CurrentIlVersion, env.IlVersion);
        Assert.Equal("test/succeed", env.SourceMetadata);
        Assert.NotNull(decoded.Program);
        Assert.Equal(CompiledIlEnvelopeCodec.ComputeDigest(env.CompiledForm), env.IntegrityDigest);
        Assert.Equal(32, env.IntegrityDigest.Length); // SHA-256
    }

    [Fact]
    public async Task Unwrapped_program_executes_equivalently_to_local()
    {
        var c = Corpus.ByName("succeed");
        byte[] frame = CompiledIlEnvelopeCodec.Wrap(c.Program, "test/succeed", c.VariableMap);
        var (decoded, _) = CompiledIlEnvelopeCodec.Unwrap(frame);

        var local = await GlpExecutor.RunNullaryAsync(c.Program, c.Goal!);
        var overWire = await GlpExecutor.RunNullaryAsync(decoded.Program, c.Goal!);

        Assert.Equal(local, overWire);
        Assert.Equal(c.ExpectedStatus, overWire);
    }

    [Fact]
    public void Digest_mismatch_is_rejected_without_executing()
    {
        var c = Corpus.ByName("succeed");
        byte[] frame = CompiledIlEnvelopeCodec.Wrap(c.Program, "test/succeed", c.VariableMap);
        frame[^1] ^= 0xFF; // corrupt the last byte (inside the compiled_form region)

        var ex = Assert.Throws<IlCodecException>(() => CompiledIlEnvelopeCodec.Decode(frame));
        Assert.Contains("integrity digest mismatch", ex.Message);
    }

    [Fact]
    public void Incompatible_il_version_is_rejected()
    {
        var c = Corpus.ByName("succeed");
        byte[] compiledForm = IlCodec.Encode(c.Program, c.VariableMap);
        byte[] frame = CompiledIlEnvelopeCodec.Encode(compiledForm, "test/succeed", ilVersion: "2.0.0");

        var ex = Assert.Throws<IlCodecException>(() => CompiledIlEnvelopeCodec.Decode(frame));
        Assert.Contains("Incompatible", ex.Message);
    }

    [Fact]
    public void Unrecognized_il_version_is_rejected()
    {
        var c = Corpus.ByName("succeed");
        byte[] compiledForm = IlCodec.Encode(c.Program, c.VariableMap);
        byte[] frame = CompiledIlEnvelopeCodec.Encode(compiledForm, "test/succeed", ilVersion: "not-a-version");

        Assert.Throws<IlCodecException>(() => CompiledIlEnvelopeCodec.Decode(frame));
    }

    [Fact]
    public void Truncated_frame_is_rejected()
    {
        var c = Corpus.ByName("succeed");
        byte[] frame = CompiledIlEnvelopeCodec.Wrap(c.Program, "test/succeed", c.VariableMap);
        byte[] truncated = frame[..(frame.Length / 2)];

        Assert.Throws<IlCodecException>(() => CompiledIlEnvelopeCodec.Decode(truncated));
    }
}
