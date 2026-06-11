using GlpRuntime.Bytecode;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// FR-008 / SC-003 — coverage. Every concrete opcode class is exercised by ≥1
/// encode+decode (via the synthetic all-opcodes program), and the D7 constant-type
/// sweep over real compiled bytecode confirms the D1 whitelist is empirically
/// complete (no real constant value falls outside it).
/// </summary>
[Trait("Category", "Coverage")]
public class CoverageGateTests
{
    [Fact]
    public void Every_concrete_opcode_class_is_exercised_by_round_trip()
    {
        var allTypes = OpcodeSamples.AllConcreteOpcodeTypes();
        var program = new BytecodeProgram(OpcodeSamples.AllConcreteOpcodes().ToList());

        var decoded = IlCodec.Decode(IlCodec.Encode(program)).Program;
        ProgramEquality.AssertStructurallyEqual(program, decoded);

        var exercised = decoded.Instructions.Select(x => x.GetType()).ToHashSet();
        foreach (var t in allTypes)
            Assert.Contains(t, exercised);
        Assert.Equal(allTypes.Count, exercised.Count);
    }

    [Fact]
    public void Constant_type_sweep_round_trips_every_ctag()
    {
        // const_sweep carries one operand of every whitelist runtime kind, incl. a
        // recursively nested StructTerm — round-trip identity proves each ctag works.
        var c = Corpus.ByName("const_sweep");
        var decoded = IlCodec.Decode(IlCodec.Encode(c.Program)).Program;
        ProgramEquality.AssertStructurallyEqual(c.Program, decoded);
        Assert.Equal(7, c.Program.Instructions.Count); // null,bool,int64,double,string,ConstTerm,StructTerm
    }

    [Fact]
    public void D7_sweep_real_bytecode_constants_are_all_whitelisted()
    {
        // Empirical completeness (D7): every constant in real compiled programs
        // (prelude + source) is whitelist-encodable — Encode never throws.
        foreach (var c in Corpus.Runnable())
        {
            var ex = Record.Exception(() => IlCodec.Encode(c.Program, c.VariableMap));
            Assert.True(ex is null,
                $"corpus '{c.Name}' contains a non-whitelisted constant: {ex?.Message}");
        }
    }
}
