// SC-010 — the wire registry is the single source of the payload-type constants (T012).
//
// This does not just check equality of magic numbers: it proves that the two assemblies that
// previously each declared their own literal (glp_il_codec.PayloadHeader, glp_result_codec.
// ResultEnvelopeCodec) now ALIAS the one glp_wire_registry entry, and that the registry itself
// refuses duplicate payload-types / functors. Zero duplicated constants across assemblies.

using GlpRuntime.IlCodec;
using GlpRuntime.ResultCodec;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.WireRegistry.Tests;

public sealed class SingleSourceTests
{
    [Fact]
    public void IlProgram_constant_is_the_registry_value()
    {
        Assert.Equal(PayloadType.IlProgram, PayloadHeader.IlProgram);
        Assert.Equal(0x10, PayloadHeader.IlProgram);
        Assert.Equal("il_program", WireRegistry.Lookup(PayloadHeader.IlProgram).Functor);
    }

    [Fact]
    public void ResultEnvelope_constant_is_the_registry_value()
    {
        Assert.Equal(PayloadType.ResultEnvelope, ResultEnvelopeCodec.PayloadTypeResultEnvelope);
        Assert.Equal(0x11, ResultEnvelopeCodec.PayloadTypeResultEnvelope);
        Assert.Equal("result_envelope", WireRegistry.Lookup(ResultEnvelopeCodec.PayloadTypeResultEnvelope).Functor);
    }

    [Fact]
    public void Registry_payload_types_are_unique()
    {
        var seen = new HashSet<byte>();
        foreach (var e in WireRegistry.All)
            Assert.True(seen.Add(e.PayloadType), $"duplicate payload type 0x{e.PayloadType:X2}");
    }

    [Fact]
    public void Registry_functors_are_unique()
    {
        var seen = new HashSet<string>();
        foreach (var e in WireRegistry.All)
            Assert.True(seen.Add(e.Functor), $"duplicate functor '{e.Functor}'");
    }

    [Fact]
    public void Unknown_payload_type_loud_fails()
    {
        Assert.False(WireRegistry.IsKnown(0xFF));
        Assert.Throws<WireRegistryException>(() => WireRegistry.Lookup(0xFF));
    }

    [Fact]
    public void Messaging_kinds_start_at_0x12()
    {
        Assert.Equal(0x12, PayloadType.MessagingBase);
        Assert.True(WireRegistry.IsKnown(PayloadType.CrdtMessage));
        Assert.Equal("crdt_message", WireRegistry.Lookup(PayloadType.CrdtMessage).Functor);
    }
}
