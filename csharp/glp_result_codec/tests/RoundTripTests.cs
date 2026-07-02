// SC-001 — the round-trip gate: Decode(Encode(R)) == R, field-by-field, for every
// in-scope corpus entry (incl. the gated float / 64-bit-int edge entries, which
// round-trip even though their bytes are not asserted byte-final).

using System.Collections.Generic;
using System.Linq;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.ResultCodec.Tests;

public class RoundTripTests
{
    public static IEnumerable<object[]> AllCases =>
        Corpus.All().Select(kv => new object[] { kv.Key });

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Decode_of_Encode_equals_original(string name)
    {
        var r = Corpus.ByName(name);

        var bytes = ResultEnvelopeCodec.Encode(r);
        var decoded = ResultEnvelopeCodec.Decode(bytes);

        Assert.Equal(r, decoded);

        // field-by-field explicitness (beyond the value-equality above)
        Assert.Equal(r.Status, decoded.Status);
        Assert.Equal(r.ResolvedBindings.Count, decoded.ResolvedBindings.Count);
        for (int i = 0; i < r.ResolvedBindings.Count; i++)
        {
            Assert.Equal(r.ResolvedBindings[i].Key, decoded.ResolvedBindings[i].Key);
            Assert.Equal(r.ResolvedBindings[i].Value, decoded.ResolvedBindings[i].Value);
        }
        Assert.Equal(r.VarToWriter.Count, decoded.VarToWriter.Count);
        for (int i = 0; i < r.VarToWriter.Count; i++)
        {
            Assert.Equal(r.VarToWriter[i].Key, decoded.VarToWriter[i].Key);
            Assert.Equal(r.VarToWriter[i].Value, decoded.VarToWriter[i].Value);
        }
        Assert.Equal(r.Suspended, decoded.Suspended);
        Assert.Equal(r.Captured, decoded.Captured); // captured INCLUDED in value round-trip
        Assert.Equal(r.Error, decoded.Error);
    }

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Encode_is_deterministic(string name)
    {
        var r = Corpus.ByName(name);
        Assert.Equal(ResultEnvelopeCodec.Encode(r), ResultEnvelopeCodec.Encode(r));
    }

    [Fact]
    public void Captured_round_trips_by_value_even_though_excluded_from_parity()
    {
        var r = Corpus.ByName("with_captured");
        var decoded = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(r));
        Assert.Equal("line one\nline two\n", System.Text.Encoding.UTF8.GetString(decoded.Captured));
    }

    [Fact]
    public void Corpus_is_non_trivial()
    {
        Assert.True(Corpus.NonGated.Count >= 10,
            $"non-gated corpus has {Corpus.NonGated.Count} entries (<10)");
    }
}
