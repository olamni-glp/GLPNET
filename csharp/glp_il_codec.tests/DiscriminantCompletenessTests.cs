using GlpRuntime.Bytecode;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// F1 (strengthens SC-004 / FR-008) — discriminant completeness, independent of any
/// corpus program. By reflection over <c>glp_runtime_net</c>, EVERY concrete
/// <c>IOp</c>/<c>IOpV2</c> subtype must encode (i.e. have a registry entry); and a
/// synthesized instruction whose class has NO entry must fail loud. This closes the
/// silent gap a corpus-only coverage gate would leave for an opcode no program uses.
/// </summary>
[Trait("Category", "Completeness")]
public class DiscriminantCompletenessTests
{
    // A concrete IOp that the codec's registry deliberately does not know about.
    private sealed class UnregisteredOp : IOp { }

    [Fact]
    public void Every_concrete_engine_opcode_type_has_a_registry_entry()
    {
        foreach (var t in OpcodeSamples.AllConcreteOpcodeTypes())
        {
            var program = new BytecodeProgram(new List<object> { OpcodeSamples.Construct(t) });
            var ex = Record.Exception(() => IlCodec.Encode(program));
            Assert.True(ex is null, $"{t.FullName} has no discriminant entry: {ex?.Message}");
        }
    }

    [Fact]
    public void Unregistered_opcode_class_fails_loud()
    {
        var program = new BytecodeProgram(new List<object> { new UnregisteredOp() });
        var ex = Assert.Throws<IlCodecException>(() => IlCodec.Encode(program));
        Assert.Contains("discriminant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
