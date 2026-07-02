// US3 deref + var→writer fidelity (T033/T034/T037) — C# reproduces the Dart-referenced
// resolved outcomes (deref-corpus.md, T035). Pins the depth-32 boundary EXACTLY: a
// 32-deep struct chain resolves; a 33-deep chain yields the explicit $truncated marker
// at depth 33 (no over/under-resolve). var→writer identity preserved by GlobalVarId.

using System.Collections.Generic;
using System.Linq;
using Xunit;
using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.ResultCodec.Builder.Tests;

// Minimal IHeapView backed by an address→term map (the file-scoped FakeHeap in
// ResultEnvelopeBuilderTests.cs is not visible here). Follows VarRef→VarRef chains,
// stops at a struct/const; an absent address is unbound.
file sealed class MapHeap : IHeapView
{
    private readonly Dictionary<int, Rt.Term> _bound;
    public MapHeap(Dictionary<int, Rt.Term>? bound = null) { _bound = bound ?? new(); }

    public Rt.Term Dereference(Rt.Term term)
    {
        while (term is Rt.VarRef v && _bound.TryGetValue(v.Addr, out var b))
            term = b;
        return term;
    }

    public bool IsBound(int varId) => _bound.ContainsKey(varId);
}

public class DerefFidelityTests
{
    private const string Inst = "glpnet-test-0001";

    // addr 0 = leaf ConstInt(0); addr k = s(VarRef(k-1)); top VarRef sits at addr n.
    private static (IHeapView heap, Rt.Term top) Chain(int n)
    {
        var map = new Dictionary<int, Rt.Term> { [0] = new Rt.ConstTerm(0L) };
        for (int k = 1; k <= n; k++)
            map[k] = new Rt.StructTerm("s", new Rt.Term[] { new Rt.VarRef(k - 1) });
        return (new MapHeap(map), new Rt.VarRef(n));
    }

    private static int DepthToMarker(Term t)
    {
        if (t is StructTerm marker && marker.Functor == "$truncated") return 0;
        if (t is StructTerm s && s.Args.Count == 1)
        {
            int inner = DepthToMarker(s.Args[0]);
            return inner < 0 ? -1 : 1 + inner;
        }
        return -1;
    }

    private static bool ContainsTruncated(Term t) =>
        t is StructTerm s && (s.Functor == "$truncated" || s.Args.Any(ContainsTruncated));

    [Fact]
    public void T033_bound_nested_struct_resolves_fully_args_in_order()
    {
        var heap = new MapHeap(new()
        {
            [1] = new Rt.ConstTerm(1L),
            [2] = new Rt.ConstTerm(2L),
            [3] = new Rt.StructTerm("point", new Rt.Term[] { new Rt.VarRef(1), new Rt.VarRef(2) }),
        });
        var r = ResultEnvelopeBuilder.DeepResolveTerm(heap, new Rt.VarRef(3), Inst);
        Assert.Equal(
            new StructTerm("point",
                new Term[] { new ConstTerm(new ConstInt(1)), new ConstTerm(new ConstInt(2)) }),
            r);
    }

    [Fact]
    public void T033_depth32_leaf_resolves_no_truncation()
    {
        var (heap, top) = Chain(32);
        var r = ResultEnvelopeBuilder.DeepResolveTerm(heap, top, Inst);
        Assert.False(ContainsTruncated(r));
        Assert.Equal(-1, DepthToMarker(r));
    }

    [Fact]
    public void T037_depth33_truncated_marker_at_exact_depth()
    {
        var (heap, top) = Chain(33);
        var r = ResultEnvelopeBuilder.DeepResolveTerm(heap, top, Inst);
        Assert.True(ContainsTruncated(r));
        Assert.Equal(33, DepthToMarker(r));
    }

    [Fact]
    public void T037_truncated_marker_is_a_normal_decodable_term()
    {
        var env = new ResultEnvelope(ExecutionStatus.Success,
            resolvedBindings: new[]
            {
                new KeyValuePair<string, Term>("T", ResultEnvelopeBuilder.TruncatedMarker()),
            });
        var decoded = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(env));
        Assert.Equal(ResultEnvelopeBuilder.TruncatedMarker(),
            decoded.ResolvedBindings.Single(kv => kv.Key == "T").Value);
    }

    [Fact]
    public void T034_multiple_unbound_query_vars_ordered_var_to_writer_by_global_id()
    {
        var heap = new MapHeap(); // everything unbound
        var drain = new Rt.DrainResult(
            new List<int>(), Rt.ExecutionStatus.Succeeded, new List<string>());
        var env = ResultEnvelopeBuilder.BuildResultEnvelope(
            heap,
            new[]
            {
                new KeyValuePair<string, int>("X", 10),
                new KeyValuePair<string, int>("Y", 20),
                new KeyValuePair<string, int>("Z", 30),
            },
            drain, Inst);

        Assert.Equal(new[] { "X", "Y", "Z" }, env.VarToWriter.Select(kv => kv.Key));
        Assert.Equal(new GlobalVarId(Inst, 10), env.VarToWriter.Single(kv => kv.Key == "X").Value);
        Assert.Equal(new GlobalVarId(Inst, 20), env.VarToWriter.Single(kv => kv.Key == "Y").Value);
        Assert.Equal(new GlobalVarId(Inst, 30), env.VarToWriter.Single(kv => kv.Key == "Z").Value);

        var decoded = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(env));
        Assert.Equal(env, decoded); // identity survives the codec round-trip
    }

    [Fact]
    public void T034_unbound_var_in_bound_struct_keeps_global_id()
    {
        var heap = new MapHeap(new()
        {
            [1] = new Rt.ConstTerm("a"), // string constant → atom
            [3] = new Rt.StructTerm("pair", new Rt.Term[] { new Rt.VarRef(1), new Rt.VarRef(2) }),
        }); // addr 2 absent → unbound
        var r = (StructTerm)ResultEnvelopeBuilder.DeepResolveTerm(heap, new Rt.VarRef(3), Inst);
        var v = Assert.IsType<VarRef>(r.Args[1]);
        Assert.Equal(Inst, v.Id.AgentId); // global id, not a raw heap addr

        var env = new ResultEnvelope(ExecutionStatus.Success,
            resolvedBindings: new[] { new KeyValuePair<string, Term>("P", r) });
        var decoded = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(env));
        Assert.Equal(r, decoded.ResolvedBindings.Single(kv => kv.Key == "P").Value);
    }
}
