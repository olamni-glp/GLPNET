using System.Security.Cryptography;
using Bc = GlpRuntime.Bytecode;

namespace GlpRuntime.IlCodec;

/// <summary>
/// The compiled-IL wire envelope (feature 062 US3, contract
/// <c>contracts/compiled-il-wire-envelope.md</c>). Frames the factored-out
/// compiler's output (<see cref="IlCodec.Encode"/> bytes) for transmission between
/// an engine that compiles (A) and one that executes (B). Four fields, per contract:
/// <list type="bullet">
///   <item><c>IlVersion</c> — semantic version of the IL format the sender produced.</item>
///   <item><c>CompiledForm</c> — the opaque compiled bytes (the factored-out compiler output).</item>
///   <item><c>IntegrityDigest</c> — SHA-256 over <c>CompiledForm</c> for corruption detection.</item>
///   <item><c>SourceMetadata</c> — minimal provenance (module/program id) for diagnostics.</item>
/// </list>
/// This wrapper adds NO language semantics (contract Non-goals): the IL is the
/// existing compiler's output, merely factored out and framed for the wire.
/// </summary>
public sealed record CompiledIlEnvelope(
    string IlVersion,
    byte[] CompiledForm,
    byte[] IntegrityDigest,
    string SourceMetadata);

/// <summary>
/// Codec for <see cref="CompiledIlEnvelope"/>. <see cref="Decode"/> realizes the
/// receiver obligations of the contract that live in the frame itself: obligation 1
/// (reject an unknown/incompatible <c>il_version</c>) and obligation 2 (verify the
/// integrity digest, reject on mismatch). Both fail loud via <see cref="IlCodecException"/>;
/// neither returns a program to execute. Obligations 3 (mid-transfer abort leaves engine
/// state unchanged) and 4 (execute-on-B == local) are integration-level and proven by the
/// receiver path (T017/T018), which decodes ONLY after a whole frame has arrived.
/// </summary>
public static class CompiledIlEnvelopeCodec
{
    /// <summary>Envelope frame version — distinct from the inner IL payload version
    /// (<see cref="PayloadHeader.Version"/>) and from <see cref="CurrentIlVersion"/>.</summary>
    public const byte EnvelopeVersion = 0x01;

    /// <summary>The IL-format semantic version this build produces and accepts (major-compatible).</summary>
    public const string CurrentIlVersion = "1.0.0";

    /// <summary>SHA-256 over the compiled form.</summary>
    public static byte[] ComputeDigest(byte[] compiledForm)
    {
        ArgumentNullException.ThrowIfNull(compiledForm);
        return SHA256.HashData(compiledForm);
    }

    /// <summary>Frame an already-compiled IL payload with provenance + integrity.</summary>
    public static byte[] Encode(byte[] compiledForm, string sourceMetadata, string? ilVersion = null)
    {
        ArgumentNullException.ThrowIfNull(compiledForm);
        ArgumentNullException.ThrowIfNull(sourceMetadata);
        var version = ilVersion ?? CurrentIlVersion;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(EnvelopeVersion);
        ByteIo.WriteString(w, version);
        ByteIo.WriteString(w, sourceMetadata);

        var digest = ComputeDigest(compiledForm);
        ByteIo.WriteVarUInt(w, (ulong)digest.Length);
        w.Write(digest);

        ByteIo.WriteVarUInt(w, (ulong)compiledForm.Length);
        w.Write(compiledForm);

        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Decode + verify an envelope. Rejects (loud) an unknown envelope-frame version, an
    /// incompatible <c>il_version</c> (major differs from <see cref="CurrentIlVersion"/>),
    /// a truncated frame, or a digest mismatch — WITHOUT surfacing a program to execute.
    /// </summary>
    public static CompiledIlEnvelope Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var ms = new MemoryStream(bytes, writable: false);
        using var r = new BinaryReader(ms);

        byte env;
        try { env = r.ReadByte(); }
        catch (EndOfStreamException) { throw new IlCodecException("Truncated compiled-IL envelope: empty frame"); }
        if (env != EnvelopeVersion)
            throw new IlCodecException($"Unsupported compiled-IL envelope version 0x{env:X2} (expected 0x{EnvelopeVersion:X2})");

        string ilVersion = ByteIo.ReadString(r);
        RejectIncompatibleIlVersion(ilVersion); // obligation 1

        string sourceMetadata = ByteIo.ReadString(r);

        int digestLen = checked((int)ByteIo.ReadVarUInt(r));
        byte[] digest = r.ReadBytes(digestLen);
        if (digest.Length != digestLen)
            throw new IlCodecException("Truncated compiled-IL envelope: integrity digest shorter than declared length");

        int formLen = checked((int)ByteIo.ReadVarUInt(r));
        byte[] compiledForm = r.ReadBytes(formLen);
        if (compiledForm.Length != formLen)
            throw new IlCodecException("Truncated compiled-IL envelope: compiled form shorter than declared length");

        // obligation 2: verify the digest over the compiled form; reject on mismatch.
        byte[] recomputed = ComputeDigest(compiledForm);
        if (!CryptographicOperations.FixedTimeEquals(recomputed, digest))
            throw new IlCodecException(
                $"Compiled-IL integrity digest mismatch (source '{sourceMetadata}'): frame corrupted in transit; not executing");

        return new CompiledIlEnvelope(ilVersion, compiledForm, digest, sourceMetadata);
    }

    /// <summary>Compile-side convenience: encode a program to IL and frame it in one step.</summary>
    public static byte[] Wrap(Bc.BytecodeProgram program, string sourceMetadata,
        IReadOnlyDictionary<string, long>? variableMap = null, string? ilVersion = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        byte[] compiledForm = IlCodec.Encode(program, variableMap);
        return Encode(compiledForm, sourceMetadata, ilVersion);
    }

    /// <summary>Receiver-side convenience: verify the envelope, then decode the inner IL to a program.</summary>
    public static (DecodedProgram Program, CompiledIlEnvelope Envelope) Unwrap(byte[] bytes)
    {
        var envelope = Decode(bytes);              // verifies version + digest first
        var decoded = IlCodec.Decode(envelope.CompiledForm);
        return (decoded, envelope);
    }

    private static void RejectIncompatibleIlVersion(string ilVersion)
    {
        int major = ParseMajor(ilVersion);
        int currentMajor = ParseMajor(CurrentIlVersion);
        if (major < 0)
            throw new IlCodecException($"Unrecognized compiled-IL il_version '{ilVersion}'; not executing");
        if (major != currentMajor)
            throw new IlCodecException(
                $"Incompatible compiled-IL il_version '{ilVersion}' (this engine accepts major {currentMajor}.x); not executing");
    }

    private static int ParseMajor(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return -1;
        int dot = version.IndexOf('.');
        string head = dot < 0 ? version : version.Substring(0, dot);
        return int.TryParse(head, out int m) && m >= 0 ? m : -1;
    }
}
