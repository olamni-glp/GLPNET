// T036 canonical serialization order: bindings / varToWriter / suspended serialize in the
// producing engine's declaration/insertion order — deterministically and identically across
// runtimes (data-model §1 parity invariant; map iteration order MUST NOT leak). Cross-runtime
// identity is additionally pinned by the golden multi_binding / var_to_writer vectors.

using System.Collections.Generic;
using System.Linq;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.ResultCodec.Tests;

public class CanonicalOrderTests
{
    // Non-alphabetical insertion order — a leaked sort would reorder these.
    private static ResultEnvelope Env() => new(
        ExecutionStatus.Success,
        resolvedBindings: new[]
        {
            new KeyValuePair<string, Term>("C", new ConstTerm(new ConstInt(3))),
            new KeyValuePair<string, Term>("A", new ConstTerm(new ConstInt(1))),
            new KeyValuePair<string, Term>("B", new ConstTerm(new ConstInt(2))),
        },
        varToWriter: new[]
        {
            new KeyValuePair<string, GlobalVarId>("Y", new GlobalVarId("a", 2)),
            new KeyValuePair<string, GlobalVarId>("X", new GlobalVarId("a", 1)),
        });

    [Fact]
    public void T036_encode_is_deterministic()
    {
        Assert.Equal(ResultEnvelopeCodec.Encode(Env()), ResultEnvelopeCodec.Encode(Env()));
    }

    [Fact]
    public void T036_serializes_in_declaration_order()
    {
        var decoded = ResultEnvelopeCodec.Decode(ResultEnvelopeCodec.Encode(Env()));
        Assert.Equal(new[] { "C", "A", "B" }, decoded.ResolvedBindings.Select(kv => kv.Key));
        Assert.Equal(new[] { "Y", "X" }, decoded.VarToWriter.Select(kv => kv.Key));
    }
}
