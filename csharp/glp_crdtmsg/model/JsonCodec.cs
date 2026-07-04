// JSON surface codec (feature 041-crdtmsg-mvp, T015).
//
// Contract C1: a generic-object encoding of the ONE model (via MessageDto). Unknown top-level keys are
// additive-optional and skipped (forward tolerance, FR-007); unknown SECTIONS are preserved verbatim
// because the whole sections array (type_number + base64 value) always round-trips. Decode is loud-fail:
// System.Text.Json rejects trailing content and malformed JSON, and MessageDto.ToModel() re-validates.

using System.Text.Json;
using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Model;

public sealed class JsonCodec : ISurfaceCodec
{
    public string Name => "json";

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        // unknown members are ignored by default → additive-optional forward tolerance (FR-007).
        WriteIndented = false,
    };

    public byte[] Encode(Message message) =>
        JsonSerializer.SerializeToUtf8Bytes(MessageDto.From(message), Opts);

    public Message Decode(byte[] bytes)
    {
        MessageDto? dto;
        try
        {
            // Deserialize over the full buffer: trailing non-whitespace content throws (loud-fail).
            dto = JsonSerializer.Deserialize<MessageDto>(bytes, Opts);
        }
        catch (JsonException e)
        {
            throw new CrdtMsgException($"malformed JSON message: {e.Message}", e);
        }
        if (dto is null)
            throw new CrdtMsgException("JSON message decoded to null");
        return dto.ToModel();
    }
}
