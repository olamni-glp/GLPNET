using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Seam;

/// <summary>
/// The default per-link payload codec (feature 050): the 025 ground-relay blob via
/// <see cref="PayloadSerializer"/> — byte-for-byte the behaviour loopback/tcp links had before the
/// <see cref="IPayloadCodec"/> seam (<c>LinkEgress</c>/<c>LinkPump</c> previously hard-coded
/// <c>PayloadSerializer</c>). Each call constructs a fresh serializer (as the egress path always did)
/// so the codec is stateless — a single shared <see cref="Instance"/> is safe across links/threads.
/// </summary>
public sealed class DefaultPayloadCodec : IPayloadCodec
{
    /// <summary>The shared stateless default (loopback/tcp and any unregistered scheme).</summary>
    public static readonly DefaultPayloadCodec Instance = new();

    public byte[] Encode(Term ground) =>
        new PayloadSerializer(string.Empty).SerializeAgentMessage(ground);

    public Term Decode(byte[] payload) =>
        new PayloadSerializer(string.Empty).DeserializeAgentMessagePayload(
            payload,
            allocateImportedVar: _ => throw new InvalidOperationException(
                "ground-relay base received a non-ground payload (embedded variable)"));
}
