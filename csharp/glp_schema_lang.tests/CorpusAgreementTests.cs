// T020 (feature 043-xsd-schema-language): the SC-003 / FR-007 corpus agreement harness —
// written FIRST; T021/T022 make it green.
//
// Contract: specs/043-xsd-schema-language/contracts/validation-api.md §Agreement law:
// for every shape expressible at both levels, `registry-level reject ⇒ XSD-level reject`
// (the XSD layer may only NARROW the accepted set, never widen it). Scope (analyze A1): the
// corpus runs on the dual-level kind crdt_message; "registry level" = MessageCodec.Decode +
// DecodeGuard.Check over the canonical binary surface. The must-understand section policy is
// receiver-dependent and not expressible at the XSD level — outside the law's quantification,
// so no such mutation is derived here.
//
// The golden corpus mirrors the three 041 golden messages (glp_crdtmsg.tests SampleMessages —
// internal to that assembly, rebuilt here via the public model API so the substrate stays
// untouched) plus derived conforming and non-conforming mutations.

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.SchemaLang;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang.Tests;

public class CorpusAgreementTests
{
    /// <summary>Section types the 041 test receiver understands (mirrors SampleMessages.Understood).</summary>
    private static readonly IReadOnlySet<long> Understood =
        new HashSet<long> { 0x12, 0x40, TlvSection.GreaseTypeNumber };

    private static SchemaDocument ReExpressedCrdtMessage()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "schemas", "crdt_message.043.txt");
        var result = SchemaValidator.Validate(File.ReadAllText(path));
        Assert.True(result.IsValid,
            "the T022 re-expression schema must validate: " +
            string.Join("; ", result.Errors.Select(e => e.ToString())));
        return result.Document!;
    }

    // ------------------------------------------------------------------
    // Corpus: 041 golden messages + derived mutations
    // ------------------------------------------------------------------

    private static Message Golden(string name) => name switch
    {
        // Mirrors glp_crdtmsg.tests/SampleMessages.cs (internal there).
        "minimal" => new Message(
            SchemaVersion: VersionPolicy.EmitSchemaVersion,
            PayloadType: PayloadType.CrdtMessage,
            Header: new Header("m-0", "alice", "bob", 0, RoutingPolicy.Empty),
            Sections: Array.Empty<Section>(),
            CrdtModel: CrdtModel.None),
        "rich" => new Message(
            SchemaVersion: VersionPolicy.EmitSchemaVersion,
            PayloadType: PayloadType.CrdtMessage,
            Header: new Header(
                MsgId: "m-42",
                From: "alice",
                To: "@editors",
                Seq: 7,
                Policy: new RoutingPolicy(
                    Targets: new[] { "bob", "carol" },
                    Waypoints: new[] { "relay1" },
                    Excludes: new[] { "mallory" })),
            Sections: new[]
            {
                new Section(0x12, new byte[] { 0x00, 0x01, 0x02, 0xFF, 0x80 }),
                new Section(0x40, Enumerable.Range(0, 256).Select(i => (byte)i).ToArray()),
                TlvSection.Grease(),
            },
            CrdtModel: CrdtModel.OpBased),
        "v2-no-cap" => new Message(
            SchemaVersion: VersionPolicy.MaxAcceptSchemaVersion,
            PayloadType: PayloadType.CrdtMessage,
            Header: new Header("m-2", "él", "bøb", 3, RoutingPolicy.Empty),
            Sections: new[] { TlvSection.Grease() },
            CrdtModel: CrdtModel.StateBased),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    public static TheoryData<string> CorpusCases => new()
    {
        // golden (conforming)
        "minimal",
        "rich",
        "v2-no-cap",
        // derived conforming mutations
        "mut-cap-slot-present",
        // derived non-conforming mutations (shape-level, expressible at both levels)
        "mut-version-out-of-range",
        "mut-unknown-payload-byte",
        "mut-foreign-known-payload-byte",
        "mut-invalid-crdt-model",
    };

    private static Message CorpusMessage(string name) => name switch
    {
        "mut-cap-slot-present" => Golden("v2-no-cap") with
        {
            Header = Golden("v2-no-cap").Header with { CapabilitySlot = new byte[] { 0xC0, 0x01 } },
        },
        "mut-version-out-of-range" => Golden("minimal") with { SchemaVersion = 99 },
        "mut-unknown-payload-byte" => Golden("minimal") with { PayloadType = 0x99 },
        "mut-foreign-known-payload-byte" => Golden("minimal") with { PayloadType = PayloadType.IlProgram },
        "mut-invalid-crdt-model" => Golden("minimal") with { CrdtModel = (CrdtModel)9 },
        _ => Golden(name),
    };

    private static bool RegistryLevelAccepts(Message message)
    {
        try
        {
            // The binary surface defers the v2 capability slot to envelope v2 (T048) and
            // loud-fails on it by design — a surface limitation, not a shape rejection — so
            // slot-carrying messages run the registry level through the CBOR surface.
            var surface = message.Header.CapabilitySlot is null ? MessageCodec.Binary : MessageCodec.Cbor;
            var bytes = surface.Encode(message);
            var decoded = MessageCodec.Decode(surface, bytes, Understood);
            DecodeGuard.Check(decoded, Understood);
            return true;
        }
        catch (CrdtMsgException)
        {
            return false; // loud registry-level reject anywhere in encode/decode/guard
        }
    }

    // ------------------------------------------------------------------
    // SC-003: zero polarity contradictions, narrowing-only
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(CorpusCases))]
    public void Registry_reject_implies_xsd_reject(string name)
    {
        var message = CorpusMessage(name);
        var doc = ReExpressedCrdtMessage();

        var registryAccepts = RegistryLevelAccepts(message);
        var verdict = InstanceValidator.Validate(doc, "crdt_message", MessageInstanceAdapter.ToInstance(message));

        // The agreement law (FR-007): nothing the lower layer rejects may pass the XSD layer.
        Assert.False(!registryAccepts && verdict.IsPass,
            $"[{name}] registry-level REJECT but XSD-level PASS — agreement law violated");
    }

    [Theory]
    [InlineData("minimal")]
    [InlineData("rich")]
    [InlineData("v2-no-cap")]
    [InlineData("mut-cap-slot-present")]
    public void Golden_and_conforming_mutations_pass_both_levels(string name)
    {
        var message = CorpusMessage(name);
        Assert.True(RegistryLevelAccepts(message), $"[{name}] must pass the registry level");
        var verdict = InstanceValidator.Validate(
            ReExpressedCrdtMessage(), "crdt_message", MessageInstanceAdapter.ToInstance(message));
        Assert.True(verdict.IsPass,
            $"[{name}] must pass the XSD level; violations: " +
            string.Join("; ", verdict.Violations.Select(v => $"{v.ConstructName}@{v.InstancePath}: {v.Message}")));
    }

    [Theory]
    [InlineData("mut-version-out-of-range", "max")]
    [InlineData("mut-unknown-payload-byte", "enum")]
    [InlineData("mut-invalid-crdt-model", "enum")]
    public void Non_conforming_mutations_fail_xsd_naming_the_construct(string name, string construct)
    {
        var verdict = InstanceValidator.Validate(
            ReExpressedCrdtMessage(), "crdt_message", MessageInstanceAdapter.ToInstance(CorpusMessage(name)));
        Assert.False(verdict.IsPass, $"[{name}] must fail the XSD level");
        Assert.Contains(verdict.Violations, v => v.ConstructName == construct);
    }

    [Fact]
    public void Foreign_known_byte_shows_narrowing_only_never_widening()
    {
        // payload_type 0x10 (il_program) is a KNOWN wire byte, so the registry level accepts
        // the decoded message — the XSD re-expression narrows to the crdt_message byte and
        // rejects. Narrowing is the permitted direction (FR-007).
        var message = CorpusMessage("mut-foreign-known-payload-byte");
        Assert.True(RegistryLevelAccepts(message));
        var verdict = InstanceValidator.Validate(
            ReExpressedCrdtMessage(), "crdt_message", MessageInstanceAdapter.ToInstance(message));
        Assert.False(verdict.IsPass);
    }
}
