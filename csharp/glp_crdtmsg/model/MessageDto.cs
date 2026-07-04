// Shared DTO for the three object surfaces (feature 041-crdtmsg-mvp, support for T015/T016/T017).
//
// JSON / YAML / CBOR are "generic-object encodings of the ONE model" (research R1). They all map to
// this DTO, so the three object codecs stay DRY and agree field-for-field; only the binary surface
// (T014, the canonical/signing form) is hand-rolled. ToModel() re-validates on the way in (loud-fail):
// schema-version range, payload_type registered, crdt_model in enum, base64 well-formed.

using System.Collections.Generic;
using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Model;

internal sealed class MessageDto
{
    public int SchemaVersion { get; set; }
    public int PayloadType { get; set; }
    public int CrdtModel { get; set; }
    public HeaderDto Header { get; set; } = new();
    public List<SectionDto> Sections { get; set; } = new();

    public static MessageDto From(Message m) => new()
    {
        SchemaVersion = m.SchemaVersion,
        PayloadType = m.PayloadType,
        CrdtModel = (int)m.CrdtModel,
        Header = HeaderDto.Of(m.Header),
        Sections = m.Sections.Select(SectionDto.From).ToList(),
    };

    public Message ToModel()
    {
        VersionPolicy.CheckSchemaVersionAccepted(SchemaVersion);
        if (PayloadType is < 0 or > 255)
            throw new CrdtMsgException($"payload_type {PayloadType} out of byte range");
        byte pt = (byte)PayloadType;
        DecodeGuard.CheckPayloadTypeKnown(pt);
        if (CrdtModel is < 0 or > 2)
            throw new CrdtMsgException($"crdt_model {CrdtModel} not in {{0,1,2}}");
        if (Header is null)
            throw new CrdtMsgException("missing header");
        var sections = (Sections ?? new List<SectionDto>()).Select(s => s.ToModel()).ToList();
        return new Message(SchemaVersion, pt, Header.ToModel(), sections, (CrdtModel)CrdtModel);
    }
}

internal sealed class HeaderDto
{
    public string MsgId { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public long Seq { get; set; }
    public PolicyDto Policy { get; set; } = new();
    public string? CapabilitySlot { get; set; }  // base64, or null when absent (v2 additive-optional)

    public static HeaderDto Of(Header h) => new()
    {
        MsgId = h.MsgId,
        From = h.From,
        To = h.To,
        Seq = h.Seq,
        Policy = PolicyDto.From(h.Policy),
        CapabilitySlot = h.CapabilitySlot is null ? null : Convert.ToBase64String(h.CapabilitySlot),
    };

    public Header ToModel()
    {
        byte[]? cap = CapabilitySlot is null ? null : DecodeBase64(CapabilitySlot, "capability_slot");
        return new Header(MsgId ?? "", From ?? "", To ?? "", Seq,
            (Policy ?? new PolicyDto()).ToModel(), cap);
    }

    internal static byte[] DecodeBase64(string s, string field)
    {
        try { return Convert.FromBase64String(s); }
        catch (FormatException e) { throw new CrdtMsgException($"malformed base64 in {field}", e); }
    }
}

internal sealed class PolicyDto
{
    public List<string> Targets { get; set; } = new();
    public List<string> Waypoints { get; set; } = new();
    public List<string> Excludes { get; set; } = new();

    public static PolicyDto From(RoutingPolicy p) => new()
    {
        Targets = p.Targets.ToList(),
        Waypoints = p.Waypoints.ToList(),
        Excludes = p.Excludes.ToList(),
    };

    public RoutingPolicy ToModel() => new(
        Targets ?? new List<string>(),
        Waypoints ?? new List<string>(),
        Excludes ?? new List<string>());
}

internal sealed class SectionDto
{
    public long TypeNumber { get; set; }
    public string Value { get; set; } = "";  // base64 of the opaque section bytes

    public static SectionDto From(Section s) => new()
    {
        TypeNumber = s.TypeNumber,
        Value = Convert.ToBase64String(s.Value),
    };

    public Section ToModel() => new(TypeNumber, HeaderDto.DecodeBase64(Value ?? "", $"section {TypeNumber}"));
}
