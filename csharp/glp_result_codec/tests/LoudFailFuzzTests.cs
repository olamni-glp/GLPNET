// T038 loud-fail fuzz (SC-004, V4): trailing/garbage bytes, unknown term tags, corrupt
// version/payloadType/status/errorPresent, and EVERY truncation of a valid encoding MUST
// be rejected — asserts ZERO silent acceptances across the non-gated corpus.

using System.Linq;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.ResultCodec.Tests;

public class LoudFailFuzzTests
{
    // A decode that never throws to the caller — true iff the bytes were REJECTED.
    private static bool Rejects(byte[] b)
    {
        try { ResultEnvelopeCodec.Decode(b); return false; }
        catch { return true; }
    }

    [Fact]
    public void Trailing_garbage_and_every_truncation_reject()
    {
        int silent = 0;
        foreach (var kv in Corpus.NonGated)
        {
            var valid = ResultEnvelopeCodec.Encode(kv.Value);
            Assert.False(Rejects(valid)); // the valid encoding must decode
            if (!Rejects(valid.Append((byte)0xFF).ToArray())) silent++;
            if (!Rejects(valid.Concat(new byte[] { 0x00, 0x01 }).ToArray())) silent++;
            for (int k = 1; k < valid.Length; k++)
                if (!Rejects(valid.Take(k).ToArray())) silent++;
        }
        Assert.Equal(0, silent);
    }

    [Fact]
    public void Corrupt_header_bytes_reject()
    {
        // empty_success: [ver, ptype, status, 0,0,0,0, errPresent].
        var b = ResultEnvelopeCodec.Encode(Corpus.ByName("empty_success"));
        byte[] With(int i, byte v) { var c = (byte[])b.Clone(); c[i] = v; return c; }
        int silent = 0;
        foreach (var v in new byte[] { 0x00, 0x02, 0x10, 0xFF }) if (!Rejects(With(0, v))) silent++;
        foreach (var p in new byte[] { 0x00, 0x10, 0x12, 0xFF }) if (!Rejects(With(1, p))) silent++;
        foreach (var s in new byte[] { 0x03, 0x04, 0xFF }) if (!Rejects(With(2, s))) silent++;
        foreach (var e in new byte[] { 0x02, 0x05, 0xFF }) if (!Rejects(With(7, e))) silent++;
        Assert.Equal(0, silent);
    }

    [Fact]
    public void Unknown_or_reserved_term_tags_reject()
    {
        // success_atom: the term tag is at byte index 6 (0x05 atom).
        var b = ResultEnvelopeCodec.Encode(Corpus.ByName("success_atom"));
        Assert.Equal(0x05, b[6]); // guard the layout assumption
        int silent = 0;
        foreach (var t in new byte[] { 0x00, 0x08, 0x09, 0x20, 0xFF })
        {
            var c = (byte[])b.Clone(); c[6] = t;
            if (!Rejects(c)) silent++;
        }
        Assert.Equal(0, silent);
    }
}
