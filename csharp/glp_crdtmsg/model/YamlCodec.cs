// YAML surface codec (feature 041-crdtmsg-mvp, T016).
//
// Contract C1 / research R3: round-trip through the abstract model (MessageDto), NOT free-form YAML —
// lossless because we serialize the model. Underscored naming keeps keys identical to the JSON surface.
// Unknown keys are ignored (additive-optional forward tolerance). Decode is loud-fail: YamlDotNet
// raises YamlException on malformed input, and MessageDto.ToModel() re-validates.

using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Model;

public sealed class YamlCodec : ISurfaceCodec
{
    public string Name => "yaml";

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()   // additive-optional forward tolerance (FR-007)
        .Build();

    public byte[] Encode(Message message) =>
        Encoding.UTF8.GetBytes(Serializer.Serialize(MessageDto.From(message)));

    public Message Decode(byte[] bytes)
    {
        MessageDto? dto;
        try
        {
            dto = Deserializer.Deserialize<MessageDto>(Encoding.UTF8.GetString(bytes));
        }
        catch (YamlException e)
        {
            throw new CrdtMsgException($"malformed YAML message: {e.Message}", e);
        }
        if (dto is null)
            throw new CrdtMsgException("YAML message decoded to null");
        return dto.ToModel();
    }
}
