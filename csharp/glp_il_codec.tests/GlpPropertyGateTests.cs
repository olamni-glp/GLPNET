using System.Reflection;
using GlpRuntime.Bytecode;
using V2 = GlpRuntime.Bytecode.V2;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// FR-004 / SC-005 — the five GLP-property gates. Each verifies that a specific
/// GLP-defining property survives the round trip, framed as the named gate the
/// correctness contract references (data-model property→gate map). They run over a
/// real compiled program (prelude + source, so all phases/opcodes appear) and the
/// synthetic all-opcodes program (which guarantees every named class is present).
/// </summary>
[Trait("Category", "Property")]
public class GlpPropertyGateTests
{
    private static (BytecodeProgram orig, BytecodeProgram dec) RoundTrip(string corpusName)
    {
        var c = Corpus.ByName(corpusName);
        var dec = IlCodec.Decode(IlCodec.Encode(c.Program, c.VariableMap)).Program;
        return (c.Program, dec);
    }

    private static bool? ReaderOf(object op) =>
        op.GetType().GetProperty("IsReader", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(op) as bool?;

    private static List<int> IndicesWhere(BytecodeProgram p, Func<object, bool> pred) =>
        Enumerable.Range(0, p.Instructions.Count).Where(i => pred(p.Instructions[i])).ToList();

    [Theory]
    [InlineData("all_opcodes")]
    [InlineData("suspend")]
    public void Srsw_PolarityPreserved(string corpus)
    {
        var (o, d) = RoundTrip(corpus);
        var ov2 = o.Instructions.Where(x => x is V2.IOpV2).ToList();
        var dv2 = d.Instructions.Where(x => x is V2.IOpV2).ToList();
        Assert.Equal(ov2.Count, dv2.Count);
        for (int i = 0; i < ov2.Count; i++)
            Assert.Equal(ReaderOf(ov2[i]), ReaderOf(dv2[i])); // reader/writer polarity is the SRSW witness
    }

    [Theory]
    [InlineData("all_opcodes")]
    [InlineData("suspend")]
    public void PhaseOrderingPreserved(string corpus)
    {
        var (o, d) = RoundTrip(corpus);
        // HEAD→GUARD→BODY ordering is carried verbatim: the type sequence is identical.
        Assert.Equal(
            o.Instructions.Select(x => x.GetType().FullName).ToList(),
            d.Instructions.Select(x => x.GetType().FullName).ToList());
    }

    [Theory]
    [InlineData("all_opcodes")]
    [InlineData("suspend")]
    public void CommitPositionPreserved(string corpus)
    {
        var (o, d) = RoundTrip(corpus);
        Assert.Equal(IndicesWhere(o, x => x is Commit), IndicesWhere(d, x => x is Commit));
    }

    [Theory]
    [InlineData("all_opcodes")]
    [InlineData("suspend")]
    public void SuspensionPreserved(string corpus)
    {
        var (o, d) = RoundTrip(corpus);
        static bool IsSusp(object x) =>
            x is ClauseNext or TryNextClause or NoMoreClauses or SuspendEnd;
        Assert.Equal(IndicesWhere(o, IsSusp), IndicesWhere(d, IsSusp));
    }

    [Theory]
    [InlineData("all_opcodes")]
    [InlineData("suspend")]
    public void ThreeValuedOpcodesPreserved(string corpus)
    {
        var (o, d) = RoundTrip(corpus);
        static bool IsThreeValued(object x) =>
            x is GuardNeedReader or GuardFail or Otherwise or ClauseNext or NoMoreClauses;
        Assert.Equal(IndicesWhere(o, IsThreeValued), IndicesWhere(d, IsThreeValued));
    }
}
