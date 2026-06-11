namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// FR-002 / SC-001 — the structural-identity gate. For every corpus program,
/// <c>Decode(Encode(p)) ≡ p</c>: same family, concrete class, operands, IsReader
/// polarity and order, with an equal recomputed Labels map; and the optional
/// per-module VariableMap round-trips exactly.
/// </summary>
[Trait("Category", "RoundTrip")]
public class RoundTripIdentityTests
{
    public static IEnumerable<object[]> Cases =>
        Corpus.All().Select(c => new object[] { c.Name });

    [Theory]
    [MemberData(nameof(Cases))]
    public void RoundTrip_preserves_structure(string name)
    {
        var c = Corpus.ByName(name);

        var bytes = IlCodec.Encode(c.Program, c.VariableMap);
        var decoded = IlCodec.Decode(bytes);

        ProgramEquality.AssertStructurallyEqual(c.Program, decoded.Program);

        if (c.VariableMap is null)
        {
            Assert.Null(decoded.VariableMap);
        }
        else
        {
            Assert.NotNull(decoded.VariableMap);
            Assert.Equal(c.VariableMap.Count, decoded.VariableMap!.Count);
            foreach (var kv in c.VariableMap)
            {
                Assert.True(decoded.VariableMap.TryGetValue(kv.Key, out var reg) && reg == kv.Value,
                    $"VariableMap entry '{kv.Key}' did not round-trip");
            }
        }
    }

    [Fact]
    public void Corpus_meets_the_ten_program_floor()
    {
        // FR-007 / F3 — the floor is met by construction, not by counting assertions.
        Assert.True(Corpus.All().Count >= 10, $"corpus has {Corpus.All().Count} programs (<10)");
    }
}
