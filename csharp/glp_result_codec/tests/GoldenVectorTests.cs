// SC-002 byte-layout lock. The golden hex vectors below are computed BY HAND from
// the wire contract (contracts/result-envelope-codec.md §3–§4) — which is the same
// spec the Dart source-of-truth encoder implements, so golden(R) == encode_Dart(R).
// Asserting Encode(R) == golden(R) catches any endianness / tag / framing drift
// independently of self-round-trip. Only NON-gated entries appear here (gated float /
// 64-bit edges are not asserted byte-final — R11/R6). `captured` is masked-out per R4
// by choosing vectors with empty captured.

using System;
using System.Linq;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.ResultCodec.Tests;

public class GoldenVectorTests
{
    private static byte[] Hex(string s)
    {
        var clean = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
        return bytes;
    }

    [Theory]
    // empty_success: 01 ver | 11 ptype | 00 status | 00 bind | 00 v2w | 00 susp | 00 caplen | 00 errpresent
    [InlineData("empty_success",
        "01 11 00 00 00 00 00 00")]
    // success_atom X=foo: ...| 01 bind | 01 58 "X" | 05 atom | 03 66 6F 6F "foo" | 00 00 00 00
    [InlineData("success_atom",
        "01 11 00 01 01 58 05 03 66 6F 6F 00 00 00 00")]
    // success_int N=42: ...| 01 bind | 01 4E "N" | 02 int64 | 2A 00 00 00 00 00 00 00 | 00 00 00 00
    [InlineData("success_int",
        "01 11 00 01 01 4E 02 2A 00 00 00 00 00 00 00 00 00 00 00")]
    // success_int_negative N=-7: int64 LE of -7 = F9 FF FF FF FF FF FF FF
    [InlineData("success_int_negative",
        "01 11 00 01 01 4E 02 F9 FF FF FF FF FF FF FF 00 00 00 00")]
    // success_nested_struct T=point(1,2)
    [InlineData("success_nested_struct",
        "01 11 00 01 01 54 06 05 70 6F 69 6E 74 02 " +
        "02 01 00 00 00 00 00 00 00 02 02 00 00 00 00 00 00 00 " +
        "00 00 00 00")]
    // var_to_writer: Z = pair(a, Var(agent1:9)); W -> agent1:9  (locks tag 0x07 + GlobalVarId)
    [InlineData("var_to_writer",
        "01 11 00 " +
        "01 01 5A 06 04 70 61 69 72 02 05 01 61 07 06 61 67 65 6E 74 31 09 00 00 00 00 00 00 00 " +
        "01 01 57 06 61 67 65 6E 74 31 09 00 00 00 00 00 00 00 " +
        "00 00 00")]
    // suspended: status 0x01, no bindings/v2w, 2 suspended GlobalVarIds (agent1:3, agent2:5)
    [InlineData("suspended",
        "01 11 01 00 00 " +
        "02 06 61 67 65 6E 74 31 03 00 00 00 00 00 00 00 06 61 67 65 6E 74 32 05 00 00 00 00 00 00 00 " +
        "00 00")]
    // failed: status 0x02, empty sections, errorPresent 0x01 + string-len 0x21 (33) + error bytes
    [InlineData("failed",
        "01 11 02 00 00 00 00 01 21 " +
        "6E 6F 20 6D 61 74 63 68 69 6E 67 20 63 6C 61 75 73 65 20 66 6F 72 20 67 6F 61 6C 20 66 6F 6F 2F 32")]
    public void Encode_matches_golden_bytes(string name, string goldenHex)
    {
        var actual = ResultEnvelopeCodec.Encode(Corpus.ByName(name));
        var expected = Hex(goldenHex);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Payload_header_is_version_1_and_type_0x11()
    {
        var bytes = ResultEnvelopeCodec.Encode(Corpus.ByName("empty_success"));
        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(0x11, bytes[1]); // RESULT_ENVELOPE, distinct from 029 IL_PROGRAM 0x10
    }
}
