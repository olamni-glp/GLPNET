using GlpRuntime.Bytecode;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// Loud-failure regression gate for decode corruption (cycle-1 review finding
/// `trailing-bytes-not-rejected`): a payload with extra trailing bytes after a
/// structurally-valid program must fail loud, matching the Lean model's
/// exact-consumption contract and the "nothing silently dropped" guarantee.
/// </summary>
[Trait("Category", "Corruption")]
public class DecodeCorruptionTests
{
    [Fact]
    public void Trailing_bytes_after_a_valid_program_fail_loud()
    {
        var p = new BytecodeProgram(new List<object> { new Commit(), new Proceed() });
        var good = IlCodec.Encode(p);

        // Sanity: the clean payload decodes.
        ProgramEquality.AssertStructurallyEqual(p, IlCodec.Decode(good).Program);

        // Append garbage — decode must now throw, not silently ignore it.
        var withTrailer = good.Concat(new byte[] { 0xFF, 0x00, 0x42 }).ToArray();
        var ex = Assert.Throws<IlCodecException>(() => IlCodec.Decode(withTrailer));
        Assert.Contains("trailing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
