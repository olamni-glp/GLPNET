// SC-003 — the no-heap-address invariant. Every field of a decoded envelope is,
// transitively, a pure value (string / long / double / Term value-node / byte[]);
// no live heap address or heap-handle type is reachable. The model is structurally
// heap-free by construction; this test proves it by walking the reconstructed graph
// and asserting every reachable node is one of the allowed value kinds.

using System;
using System.Collections.Generic;
using System.Linq;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.ResultCodec.Tests;

public class NoHeapTests
{
    public static IEnumerable<object[]> AllCases =>
        Corpus.All().Select(kv => new object[] { kv.Key });

    [Theory]
    [MemberData(nameof(AllCases))]
    public void Decoded_envelope_holds_no_heap_address(string name)
    {
        var r = Corpus.ByName(name);
        var decoded = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(r));

        foreach (var kv in decoded.ResolvedBindings)
        {
            Assert.IsType<string>(kv.Key);
            AssertPureTerm(kv.Value);
        }
        foreach (var kv in decoded.VarToWriter)
        {
            Assert.IsType<string>(kv.Key);
            AssertPureVarId(kv.Value);
        }
        foreach (var id in decoded.Suspended)
            AssertPureVarId(id);

        Assert.IsType<byte[]>(decoded.Captured);
        if (decoded.Error is not null)
            Assert.IsType<string>(decoded.Error); // error is a plain string, never a handle
    }

    private static void AssertPureTerm(Term t)
    {
        switch (t)
        {
            case ConstTerm ct:
                AssertPureConstant(ct.Value);
                break;
            case StructTerm st:
                Assert.IsType<string>(st.Functor);
                foreach (var a in st.Args) AssertPureTerm(a);
                break;
            case VarRef vr:
                AssertPureVarId(vr.Id);
                break;
            default:
                Assert.Fail($"Unexpected Term subtype reachable from a decoded envelope: {t.GetType().FullName}");
                break;
        }
    }

    private static void AssertPureConstant(Constant c)
    {
        switch (c)
        {
            case ConstAtom a: Assert.IsType<string>(a.Name); break;
            case ConstInt i: _ = i.Value; break;        // long — a value, not a handle
            case ConstReal d: _ = d.Value; break;       // double — a value, not a handle
            case ConstString s: Assert.IsType<string>(s.Value); break;
            default:
                Assert.Fail($"Unexpected Constant subtype: {c.GetType().FullName}");
                break;
        }
    }

    private static void AssertPureVarId(GlobalVarId id)
    {
        // GlobalVarId is exactly (string agentId, long localId): no local heap address.
        Assert.IsType<string>(id.AgentId);
        _ = id.LocalId;
    }
}
