// T020/T021 acceptance — C# deep-resolve + envelope builder, exercised against a
// FAKE IHeapView (owner decision B) so no live engine is required. Mirrors the
// Dart reference behaviour in glp_runtime/lib/codec/result_envelope_builder.dart.

using System.Collections.Generic;
using System.Linq;

using Xunit;

using GlpRuntime.ResultCodec;         // codec value types (Term/Constant/GlobalVarId/ResultEnvelope)
using Rt = GlpRuntime.Runtime;        // runtime term model + DrainResult/ExecutionStatus

namespace GlpRuntime.ResultCodec.Builder.Tests;

/// <summary>A minimal in-memory heap: <c>bound[addr]</c> present ⇒ that writer is bound to the term.</summary>
file sealed class FakeHeap : IHeapView
{
    private readonly Dictionary<int, Rt.Term> _bound;
    public FakeHeap(Dictionary<int, Rt.Term>? bound = null) => _bound = bound ?? new();

    public bool IsBound(int varId) => _bound.ContainsKey(varId);

    public Rt.Term Dereference(Rt.Term term)
    {
        if (term is Rt.VarRef v)
        {
            var cur = v;
            while (_bound.TryGetValue(cur.Addr, out var bt))
            {
                if (bt is Rt.VarRef nv) { cur = nv; continue; } // follow chained bindings
                return bt;
            }
            return cur; // unbound
        }
        return term; // struct/const deref to self
    }
}

public class ResultEnvelopeBuilderTests
{
    private const string Inst = "inst-A";

    [Fact]
    public void DeepResolve_UnboundVar_YieldsGlobalVarId()
    {
        var heap = new FakeHeap();
        var got = ResultEnvelopeBuilder.DeepResolveTerm(heap, new Rt.VarRef(5), Inst);
        Assert.Equal(new VarRef(new GlobalVarId(Inst, 5)), got);
    }

    [Fact]
    public void DeepResolve_BoundToInt_YieldsConstInt()
    {
        var heap = new FakeHeap(new() { [7] = new Rt.ConstTerm(3L) });
        var got = ResultEnvelopeBuilder.DeepResolveTerm(heap, new Rt.VarRef(7), Inst);
        Assert.Equal(new ConstTerm(new ConstInt(3)), got);
    }

    [Fact]
    public void DeepResolve_StringConst_IsAtom()
    {
        var heap = new FakeHeap(new() { [3] = new Rt.ConstTerm("foo") });
        var got = ResultEnvelopeBuilder.DeepResolveTerm(heap, new Rt.VarRef(3), Inst);
        Assert.Equal(new ConstTerm(new ConstAtom("foo")), got);
    }

    [Fact]
    public void DeepResolve_NestedStruct_WithUnboundArg()
    {
        var heap = new FakeHeap();
        var term = new Rt.StructTerm("f", new Rt.Term[] { new Rt.ConstTerm(1L), new Rt.VarRef(9) });
        var got = ResultEnvelopeBuilder.DeepResolveTerm(heap, term, Inst);

        var expected = new StructTerm("f", new Term[]
        {
            new ConstTerm(new ConstInt(1)),
            new VarRef(new GlobalVarId(Inst, 9)),
        });
        Assert.Equal(expected, got);
    }

    [Fact]
    public void DeepResolve_BeyondDepthBound_EmitsTruncatedMarker()
    {
        // 40-deep single-arg struct chain over the depth-32 bound.
        Rt.Term t = new Rt.ConstTerm(0L);
        for (int k = 0; k < 40; k++) t = new Rt.StructTerm("s", new[] { t });

        var got = ResultEnvelopeBuilder.DeepResolveTerm(new FakeHeap(), t, Inst);

        Assert.True(ContainsTruncated(got), "deep-resolve must emit $truncated at the depth bound");
        // a shallow term must NOT be truncated
        var shallow = ResultEnvelopeBuilder.DeepResolveTerm(new FakeHeap(), new Rt.ConstTerm(0L), Inst);
        Assert.False(ContainsTruncated(shallow));
    }

    private static bool ContainsTruncated(Term t) => t switch
    {
        StructTerm s => s.Functor == "$truncated" || s.Args.Any(ContainsTruncated),
        _ => false,
    };

    [Fact]
    public void Build_BoundAndUnbound_OrdersBindingsAndSuspended_AndRoundTrips()
    {
        var heap = new FakeHeap(new() { [7] = new Rt.ConstTerm(3L) }); // X bound; Y (9) unbound
        var qvw = new[]
        {
            new KeyValuePair<string, int>("X", 7),
            new KeyValuePair<string, int>("Y", 9),
        };
        // BlockingReaders deliberately out of order ⇒ builder must sort {5,2} → 2,5.
        var drain = new Rt.DrainResult(
            new List<int>(), Rt.ExecutionStatus.Succeeded, new List<string>(),
            new HashSet<int> { 5, 2 });

        var env = ResultEnvelopeBuilder.BuildResultEnvelope(heap, qvw, drain, Inst);

        var expected = new ResultEnvelope(
            ExecutionStatus.Success,
            new[] { new KeyValuePair<string, Term>("X", new ConstTerm(new ConstInt(3))) },
            new[] { new KeyValuePair<string, GlobalVarId>("Y", new GlobalVarId(Inst, 9)) },
            new[] { new GlobalVarId(Inst, 2), new GlobalVarId(Inst, 5) });
        Assert.Equal(expected, env);

        // T021: the built envelope round-trips through the shipped codec byte-for-byte.
        var back = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(env));
        Assert.Equal(env, back);
    }

    [Fact]
    public void Build_SuspendedStatus_CarriesBlockingReaders()
    {
        var drain = new Rt.DrainResult(
            new List<int>(), Rt.ExecutionStatus.Suspended, new List<string>(),
            new HashSet<int> { 2 });

        var env = ResultEnvelopeBuilder.BuildResultEnvelope(
            new FakeHeap(), System.Array.Empty<KeyValuePair<string, int>>(), drain, Inst);

        Assert.Equal(ExecutionStatus.Suspended, env.Status);
        Assert.Equal(new[] { new GlobalVarId(Inst, 2) }, env.Suspended);
        var back = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(env));
        Assert.Equal(env, back);
    }
}
