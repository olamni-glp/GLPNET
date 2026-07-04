// SC-008 — v1 reader skips the v2 additive capability slot (feature 041-crdtmsg-mvp, T044).

using GlpRuntime.CrdtMsg.Headers;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class VersionSkipTests
{
    private static Message BaseMsg() => new(
        1, PayloadType.CrdtMessage,
        new Header("m", "a", "b", 0, RoutingPolicy.Empty),
        new[] { new Section(0x12, new byte[] { 1, 2 }), new Section(0x40, new byte[] { 3 }) },
        CrdtModel.OpBased);

    [Fact]
    public void V1_reader_processes_known_fields_and_skips_v2_capability_slot()
    {
        byte[] cap = { 9, 9, 9 };
        Message v2 = CapabilitySlot.Attach(BaseMsg(), cap);
        Assert.Equal(2, v2.SchemaVersion);

        byte[] bytes = MessageCodec.Binary.Encode(v2);

        // a v1 reader decodes a v2 envelope (accept-range tolerance) and carries the unknown-ignorable
        // capability section verbatim, processing every known field unimpeded.
        var understoodByV1 = new HashSet<long> { 0x12, 0x40 };
        Message v1read = MessageCodec.Decode(MessageCodec.Binary, bytes, understoodByV1); // no loud-fail on 0x20

        Assert.Equal("m", v1read.Header.MsgId);
        Assert.Contains(v1read.Sections, s => s.TypeNumber == 0x12);
        Assert.Contains(v1read.Sections, s => s.TypeNumber == 0x40);

        // the additive slot survived verbatim — a v2 reader can extract it
        Assert.Equal(cap, CapabilitySlot.Extract(v1read));
    }

    [Fact]
    public void Base_message_has_no_capability_slot()
    {
        Assert.False(CapabilitySlot.Present(BaseMsg()));
        Assert.Null(CapabilitySlot.Extract(BaseMsg()));
    }
}
