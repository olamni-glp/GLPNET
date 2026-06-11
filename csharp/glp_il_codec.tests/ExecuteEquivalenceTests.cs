namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// FR-003 / SC-002 — the execute-equivalence gate (independent of structural
/// identity). The same nullary goal is run against the original program <c>p</c>
/// and its decoded twin <c>Decode(Encode(p))</c>; the two <c>ExecutionStatus</c>
/// values must match (incl. <c>Suspended</c>). <c>ExpectedStatus</c> is asserted as
/// a sanity anchor so a codec that trivially preserves a wrong-but-equal result is
/// still caught. The empty program is exempt (no defined goal/result — F2).
/// </summary>
[Trait("Category", "Execute")]
public class ExecuteEquivalenceTests
{
    public static IEnumerable<object[]> RunnableCases =>
        Corpus.Runnable().Select(c => new object[] { c.Name });

    [Theory]
    [MemberData(nameof(RunnableCases))]
    public async Task Decoded_program_executes_equivalently(string name)
    {
        var c = Corpus.ByName(name);

        var decoded = IlCodec.Decode(IlCodec.Encode(c.Program, c.VariableMap)).Program;

        var original = await GlpExecutor.RunNullaryAsync(c.Program, c.Goal!);
        var roundTripped = await GlpExecutor.RunNullaryAsync(decoded, c.Goal!);

        Assert.Equal(original, roundTripped);
        Assert.Equal(c.ExpectedStatus, original); // anchor: the goal really reaches the intended status
    }
}
