using GlpRuntime.Bytecode;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// A3 — the two <c>[Obsolete]</c> opcodes (<c>UnionSiAndGoto</c>, <c>ResetAndGoto</c>)
/// are round-tripped EXACTLY, label included; they are encoded, not dropped.
/// </summary>
[Trait("Category", "Obsolete")]
public class ObsoleteOpcodeTests
{
    [Fact]
    public void Obsolete_opcodes_round_trip_exactly()
    {
        var c = Corpus.ByName("obsolete");
        var decoded = IlCodec.Decode(IlCodec.Encode(c.Program)).Program;

        ProgramEquality.AssertStructurallyEqual(c.Program, decoded);

#pragma warning disable CS0612
        Assert.Contains(decoded.Instructions, x => x is UnionSiAndGoto u && u.Label == "p/0");
        Assert.Contains(decoded.Instructions, x => x is ResetAndGoto r && r.Label == "p/0");
#pragma warning restore CS0612
    }
}
