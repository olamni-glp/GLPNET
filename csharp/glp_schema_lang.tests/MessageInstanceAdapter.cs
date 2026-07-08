// The reference Message → InstanceValue adapter (feature 043, T020; research R7).
//
// Documented in quickstart.md: production consumers map THEIR decoded form to the neutral
// InstanceValue tree the same way — glp_schema_lang itself never references glp_crdtmsg (the
// dependency arrow stays production → wire_registry only). Symbolic enums map to Str
// (data-model §4); the optional capability slot maps to an absent field when null.

using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.SchemaLang;

namespace GlpRuntime.SchemaLang.Tests;

internal static class MessageInstanceAdapter
{
    public static InstanceValue ToInstance(Message message)
    {
        var headerFields = new List<(string, InstanceValue)>
        {
            ("msg_id", new InstanceValue.Str(message.Header.MsgId)),
            ("from", new InstanceValue.Str(message.Header.From)),
            ("to", new InstanceValue.Str(message.Header.To)),
            ("seq", new InstanceValue.Int(message.Header.Seq)),
            ("policy", InstanceValue.OfStruct("policy",
                ("targets", Strings(message.Header.Policy.Targets)),
                ("waypoints", Strings(message.Header.Policy.Waypoints)),
                ("excludes", Strings(message.Header.Policy.Excludes)))),
        };
        if (message.Header.CapabilitySlot is byte[] slot)
            headerFields.Add(("capability_slot", new InstanceValue.Bytes(slot)));

        return InstanceValue.OfStruct("crdt_message",
            ("schema_version", new InstanceValue.Int(message.SchemaVersion)),
            ("payload_type", new InstanceValue.Int(message.PayloadType)),
            ("crdt_model", new InstanceValue.Str(CrdtModelTag(message.CrdtModel))),
            ("header", InstanceValue.OfStruct("header", headerFields.ToArray())),
            ("sections", new InstanceValue.List(message.Sections
                .Select(s => (InstanceValue)InstanceValue.OfStruct("section",
                    ("type_number", new InstanceValue.Int(s.TypeNumber)),
                    ("value", new InstanceValue.Bytes(s.Value))))
                .ToList())));
    }

    private static string CrdtModelTag(CrdtModel model) => model switch
    {
        CrdtModel.None => "none",
        CrdtModel.StateBased => "state_based",
        CrdtModel.OpBased => "op_based",
        _ => ((byte)model).ToString(), // out-of-range discriminator — fails the enum facet, loudly
    };

    private static InstanceValue Strings(IReadOnlyList<string> values) =>
        new InstanceValue.List(values.Select(v => (InstanceValue)new InstanceValue.Str(v)).ToList());
}
