using GlpRuntime.Bytecode;
using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// FR-006 / SC-004 — the constant whitelist is closed and failure is loud. Any
/// value whose runtime type is outside <c>null|bool|int64|double|string|ConstTerm|
/// StructTerm</c> raises <see cref="IlCodecException"/> on encode; there is no
/// silent drop or coercion. Note: <c>int</c> (int32) is deliberately NOT in the
/// whitelist — the compiler lexes integers to <c>long</c>, so a stray int32 is a
/// real anomaly and must surface, not be silently widened.
/// </summary>
[Trait("Category", "Whitelist")]
public class ConstantWhitelistTests
{
    private static BytecodeProgram OneConst(object? value) =>
        new(new List<object> { new UnifyConstant(value) });

    [Fact]
    public void Int32_is_rejected_loudly() =>
        Assert.Throws<IlCodecException>(() => IlCodec.Encode(OneConst(5)));      // int, not long

    [Fact]
    public void Decimal_is_rejected_loudly() =>
        Assert.Throws<IlCodecException>(() => IlCodec.Encode(OneConst(1.5m)));

    [Fact]
    public void Arbitrary_object_is_rejected_loudly() =>
        Assert.Throws<IlCodecException>(() => IlCodec.Encode(OneConst(new object())));

    [Fact]
    public void Non_ground_struct_argument_is_rejected_loudly()
    {
        // A StructTerm whose arg is a VarRef (a non-ground reference) — the codec
        // covers GROUND constants only; the recursive sub-encoder must fail loud.
        var st = new Rt.StructTerm("f", new List<Rt.Term> { new Rt.VarRef(3) });
        Assert.Throws<IlCodecException>(() => IlCodec.Encode(OneConst(st)));
    }

    [Fact]
    public void Whitelisted_primitives_all_round_trip()
    {
        foreach (object? v in new object?[] { null, true, 7L, 2.5, "s", new Rt.ConstTerm(9L) })
        {
            var decoded = IlCodec.Decode(IlCodec.Encode(OneConst(v))).Program;
            ProgramEquality.AssertStructurallyEqual(OneConst(v), decoded);
        }
    }
}
