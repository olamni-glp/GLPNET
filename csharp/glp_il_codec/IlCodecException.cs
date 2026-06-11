namespace GlpRuntime.IlCodec;

/// <summary>
/// The single, loud failure type for the IL codec (FR-006 / SC-004).
/// Raised on: an out-of-whitelist constant value, an unknown/out-of-family
/// instruction, a known-family instruction whose concrete class has no
/// discriminant-table entry (F1), a truncated/corrupt payload on decode, or a
/// version / payload-type mismatch. Never swallowed; never a silent drop.
/// </summary>
public sealed class IlCodecException : Exception
{
    public IlCodecException(string message) : base(message) { }
    public IlCodecException(string message, Exception inner) : base(message, inner) { }
}
