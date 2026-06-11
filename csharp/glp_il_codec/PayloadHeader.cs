namespace GlpRuntime.IlCodec;

/// <summary>
/// The 2-byte payload header that prefixes an encoded IL program (data-model
/// "Byte payload (top level)"). The <see cref="PayloadType"/> byte is the ONLY new
/// discriminant surface and lives in the payload — the transport <c>FrameKind</c>
/// enum (Whole/Fragment) is untouched (A4 / seed T2 option 3). A version or
/// payload-type mismatch fails loud (<see cref="IlCodecException"/>), never silently.
/// </summary>
public static class PayloadHeader
{
    /// <summary>Codec format version.</summary>
    public const byte Version = 0x01;

    /// <summary>Payload-type discriminant: an IL/bytecode program (distinguishes it from result envelopes).</summary>
    public const byte IlProgram = 0x10;

    public static void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(IlProgram);
    }

    public static void Read(BinaryReader r)
    {
        byte v = r.ReadByte();
        if (v != Version)
            throw new IlCodecException($"Unsupported IL payload version 0x{v:X2} (expected 0x{Version:X2})");
        byte t = r.ReadByte();
        if (t != IlProgram)
            throw new IlCodecException($"Unexpected payload type 0x{t:X2} (expected IL_PROGRAM 0x{IlProgram:X2})");
    }
}
