// Surface-codec interface + the multi-surface facade (feature 041-crdtmsg-mvp, T014 support / C1).
//
// One abstract Message, four surface codecs. The BINARY surface is the CANONICAL form: it is the
// deterministic encoding used for semantic equality AND (later) for signing (data-model §8 —
// "canonical = deterministic binary term encoding"). Two messages are semantically equal iff their
// canonical bytes match — this is the C1 conformance oracle the 16-cell matrix (T010) checks.

using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Model;

/// <summary>A reversible codec for one wire surface. Decode is structural + version + payload_type +
/// consume-all (loud-fail); the model-level must-understand policy is applied by the facade.</summary>
public interface ISurfaceCodec
{
    /// <summary>Stable surface name (binary-term / json / yaml / cbor).</summary>
    string Name { get; }
    byte[] Encode(Message message);
    Message Decode(byte[] bytes);
}

/// <summary>The four surfaces + the canonical-form / semantic-equality helpers.</summary>
public static class MessageCodec
{
    public static readonly ISurfaceCodec Binary = new BinaryTermCodec();
    public static readonly ISurfaceCodec Json = new JsonCodec();
    public static readonly ISurfaceCodec Yaml = new YamlCodec();
    public static readonly ISurfaceCodec Cbor = new CborCodec();

    /// <summary>All four surfaces (the conformance-matrix rows/columns).</summary>
    public static IReadOnlyList<ISurfaceCodec> Surfaces { get; } = new[] { Binary, Json, Yaml, Cbor };

    /// <summary>The canonical deterministic byte form (binary surface) — signing + equality oracle.</summary>
    public static byte[] Canonical(Message message) => Binary.Encode(message);

    /// <summary>Semantic equality = identical canonical bytes (C1).</summary>
    public static bool SemanticEqual(Message a, Message b) =>
        Canonical(a).AsSpan().SequenceEqual(Canonical(b));

    /// <summary>Decode + apply the receiver's must-understand policy (FR-005 full loud-fail path).</summary>
    public static Message Decode(ISurfaceCodec surface, byte[] bytes, IReadOnlySet<long> understoodSectionTypes)
    {
        Message m = surface.Decode(bytes);
        DecodeGuard.CheckMustUnderstand(m, understoodSectionTypes);
        return m;
    }
}
