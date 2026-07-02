// T041 cyclic-term defer-to-runtime (FR-008, D5/FORK-1 OPEN): a cyclic term encodes via the
// depth-bounded deref and NEVER loops. This test asserts consistency with the runtime deref +
// the existing depth bound (R5); it does NOT define a codec-local cycle policy — that is an
// OWNER decision (D5/FORK-1), deliberately left open. The C# FakeHeap has no pointer-cycle
// guard, so the cycle is a self-referential STRUCT caught by the depth bound. The test
// terminating IS the no-loop proof.

using System.Collections.Generic;
using System.Linq;
using Xunit;
using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.ResultCodec.Builder.Tests;

file sealed class CycleHeap : IHeapView
{
    private readonly Dictionary<int, Rt.Term> _bound;
    public CycleHeap(Dictionary<int, Rt.Term> bound) { _bound = bound; }

    public Rt.Term Dereference(Rt.Term term)
    {
        // Follows VarRef→VarRef chains; a self-referential struct is returned in one hop
        // (the struct is not a VarRef), so Dereference itself does not loop.
        while (term is Rt.VarRef v && _bound.TryGetValue(v.Addr, out var b)) term = b;
        return term;
    }

    public bool IsBound(int varId) => _bound.ContainsKey(varId);
}

public class CyclicTermTests
{
    private const string Inst = "glpnet-test-0001";

    private static bool ContainsTruncated(Term t) =>
        t is StructTerm s && (s.Functor == "$truncated" || s.Args.Any(ContainsTruncated));

    [Fact]
    public void Self_referential_struct_resolves_to_truncated_at_depth_bound()
    {
        // addr 1 = s(VarRef 1): the struct references itself → a cycle.
        var heap = new CycleHeap(new()
        {
            [1] = new Rt.StructTerm("s", new Rt.Term[] { new Rt.VarRef(1) }),
        });
        var resolved = ResultEnvelopeBuilder.DeepResolveTerm(heap, new Rt.VarRef(1), Inst);
        Assert.True(ContainsTruncated(resolved)); // terminated + explicit marker, not a silent cut

        var env = new ResultEnvelope(ExecutionStatus.Success,
            resolvedBindings: new[] { new KeyValuePair<string, Term>("C", resolved) });
        var decoded = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(env));
        Assert.Equal(resolved, decoded.ResolvedBindings.Single(kv => kv.Key == "C").Value);
    }
}
