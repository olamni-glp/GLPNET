// T032 V5 oracle cross-check (FR-007): the 038 result-codec term bytes reproduce the shipped
// 029 GlpRuntime.IlCodec ConstantCodec byte conventions for SHARED constant inputs. This is a
// cross-check ONLY — 029 is the C# byte oracle; it is NOT proof for the Dart/Gleam paths.
//
// The two term MODELS diverge where the GLP envelope model legitimately extends 029:
//   • 038 flattens primitives (ConstInt → 0x02) whereas 029 WRAPS them (Rt.ConstTerm → 0x05),
//     so the 0x05 tag and struct ARGS are model-divergent by design;
//   • 029 tags 0x00 null / 0x01 bool have no representation in the GLP term model (atoms → 0x05).
// The genuinely shared, byte-identical subset — int64 (0x02), double (0x03), string (0x04) and
// the struct-header framing (0x06 functor + arity varint) — is what V5 pins: it proves the
// shared ByteIo conventions (LEB128 varint, int64 LE, IEEE-754 bits, len+UTF-8) never drift.

using System;
using System.Collections.Generic;
using System.IO;
using GlpRuntime.IlCodec;
using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.ResultCodec.Tests;

public class OracleConsistencyTests
{
    private static byte[] Oracle029(object? value)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms))
        {
            ConstantCodec.Write(bw, value);
            bw.Flush();
        }
        return ms.ToArray();
    }

    private static byte[] Codec038(Term t)
    {
        var w = new ByteWriter();
        TermCodec.EncodeTerm(w, t);
        return w.TakeBytes();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    [InlineData(-7L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void Int64_bytes_match_the_029_oracle(long n) =>
        Assert.Equal(Oracle029(n), Codec038(new ConstTerm(new ConstInt(n))));

    [Theory]
    [InlineData(0.0)]
    [InlineData(2.5)]
    [InlineData(-3.14159)]
    public void Double_bytes_match_the_029_oracle(double d) =>
        Assert.Equal(Oracle029(d), Codec038(new ConstTerm(new ConstReal(d))));

    [Theory]
    [InlineData("")]
    [InlineData("hello, world")]
    [InlineData("λ→utf8")]
    public void String_bytes_match_the_029_oracle(string s) =>
        Assert.Equal(Oracle029(s), Codec038(new ConstTerm(new ConstString(s))));

    [Fact]
    public void Struct_header_framing_matches_the_029_oracle()
    {
        // arity-0 struct: 0x06 + functor(string) + arity(varint), no args → no wrapper
        // divergence. Pins the shared struct-header byte framing.
        var oracle = Oracle029(new Rt.StructTerm("nil_like", new List<Rt.Term>()));
        var codec = Codec038(new StructTerm("nil_like", Array.Empty<Term>()));
        Assert.Equal(oracle, codec);
    }

    [Fact]
    public void Model_diverges_at_0x05_wrapper_but_the_inner_primitive_bytes_are_shared()
    {
        // 029 wraps a primitive in Rt.ConstTerm (0x05); the 038 flat ConstInt is 0x02, so the
        // whole-term bytes differ — but 029's inner primitive (drop the 0x05 wrapper) is exactly
        // the 038 flat encoding. This is the intended FR-007 boundary, made explicit.
        var wrapped029 = Oracle029(new Rt.ConstTerm(1L)); // 05 02 01 00 00 00 00 00 00 00
        var flat038 = Codec038(new ConstTerm(new ConstInt(1L))); // 02 01 00 00 00 00 00 00 00
        Assert.NotEqual(wrapped029, flat038);
        Assert.Equal(0x05, wrapped029[0]);
        Assert.Equal(0x02, flat038[0]);
        Assert.Equal(flat038, wrapped029[1..]); // inner bytes identical
    }
}
