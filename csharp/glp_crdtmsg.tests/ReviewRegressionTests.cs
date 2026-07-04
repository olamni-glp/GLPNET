// Regression tests for confirmed code-review findings (feature 041-crdtmsg-mvp).
//   #1 VarInt LEB128 overlong/overflow masking (IMPORTANT)
//   #3 TlvSection type_number > Int64 → CrdtMsgException, not OverflowException (nit)
//   #5 Macaroon Caveat.Bytes() injective (nit)

using GlpRuntime.CrdtMsg.Cap;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class ReviewRegressionTests
{
    private static ulong Read(byte[] b)
    {
        var r = new ByteReader(b);
        ulong v = VarInt.ReadU64(r);
        Assert.True(r.AtEnd);
        return v;
    }

    private static byte[] Write(ulong v)
    {
        var w = new ByteWriter();
        VarInt.WriteU64(w, v);
        return w.TakeBytes();
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(127UL)]
    [InlineData(128UL)]
    [InlineData(300UL)]
    [InlineData(uint.MaxValue)]
    [InlineData(long.MaxValue)]
    [InlineData(1UL << 63)]        // bit 63 only — the 10-group boundary case
    [InlineData(ulong.MaxValue)]   // all bits — 10 groups ending 0x01
    public void Leb128_roundtrips(ulong v) => Assert.Equal(v, Read(Write(v)));

    [Fact]
    public void Leb128_overlong_value_bits_are_rejected()
    {
        // nine 0xFF + 0x02: the 10th group carries bit 1 (would be shifted past bit 63) → overflow.
        var bytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x02 };
        Assert.Throws<CrdtMsgException>(() => new ByteReader(bytes).Let(VarInt.ReadU64));
    }

    [Fact]
    public void Leb128_eleventh_continuation_is_rejected()
    {
        var bytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        Assert.Throws<CrdtMsgException>(() => new ByteReader(bytes).Let(VarInt.ReadU64));
    }

    [Fact]
    public void Leb128_non_canonical_trailing_zero_is_rejected()
    {
        Assert.Throws<CrdtMsgException>(() => new ByteReader(new byte[] { 0x80, 0x00 }).Let(VarInt.ReadU64));
    }

    [Fact]
    public void Leb128_truncation_is_rejected()
    {
        Assert.Throws<CrdtMsgException>(() => new ByteReader(new byte[] { 0x80 }).Let(VarInt.ReadU64));
    }

    [Fact]
    public void Section_type_number_over_int64_loud_fails_as_crdtmsg_exception()
    {
        var w = new ByteWriter();
        VarInt.WriteU64(w, 1);               // count = 1
        VarInt.WriteU64(w, ulong.MaxValue);  // type_number > long.MaxValue
        VarInt.WriteU64(w, 0);               // length = 0
        var r = new ByteReader(w.TakeBytes());
        Assert.Throws<CrdtMsgException>(() => TlvSection.DecodeSections(r));
    }

    [Fact]
    public void Caveat_encoding_is_injective()
    {
        var a = new Caveat("a b", "=", "c").Bytes();
        var b = new Caveat("a", "b", "= c").Bytes();
        Assert.NotEqual(a, b); // a plain space-join would collide these
    }
}

internal static class ByteReaderExt
{
    // small helper so a throwing call is inline in Assert.Throws
    public static ulong Let(this ByteReader r, Func<ByteReader, ulong> f) => f(r);
}
