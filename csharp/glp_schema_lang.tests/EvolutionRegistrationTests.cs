// T029 (feature 043-xsd-schema-language): refusal + override tests — written FIRST; T031
// makes them green.
//
// Contract: specs/043-xsd-schema-language/contracts/compat-evolution.md — refusal law
// (clarification 3): a type with NO declared compatibility mode refuses both the check and
// the registration of a new version with an explicit NoCompatModeDeclaredError, never a
// silently assumed default. An incompatible version registers only with an explicit
// OverrideRecord{verdict, acknowledger, reason}, stored on the RegistryRecord (US4 AS-3).

using GlpRuntime.SchemaLang;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang.Tests;

public class EvolutionRegistrationTests
{
    private const string V1Body = "message evo_kind { sequence { id: int  name: str } }";
    private const string IncompatibleV2Body = "message evo_kind { sequence { id: int } }"; // removes mandatory name
    private const string CompatibleV2Body = "message evo_kind { sequence { id: int  name: str  extra?: int } }";

    private static SchemaDocument Doc(int version, string body) =>
        LoweringTests.Doc($"schema evo version {version}\n{body}");

    private static SchemaLangRegistry RegisteredV1(CompatMode mode = CompatMode.Backward)
    {
        var registry = new SchemaLangRegistry();
        var v1 = Doc(1, V1Body);
        Assert.Null(registry.Register(v1, LoweringTests.LowerOk(v1), mode).Error);
        return registry;
    }

    // ------------------------------------------------------------------
    // Refusal law (clarification 3): no declared mode → refuse check AND register
    // ------------------------------------------------------------------

    private static SchemaLangRegistry RegistryWithModelessKind()
    {
        // A legacy row with no declared mode (data-model §2: CompatMode nullable only for
        // seeded legacy rows) — planted through the internal seam.
        var registry = new SchemaLangRegistry();
        var v1 = Doc(1, V1Body);
        var source = v1.Source;
        registry.AppendOverlay(new RegistryRecord(
            0x30, "evo_kind", CompatMode: null,
            QmeditDsl: source, Cddl: "evo-kind = { id: int, name: tstr, }\n", XsdSource: source,
            SchemaName: "evo", Version: 1,
            CddlSha256: SchemaLangRegistry.Sha256Hex("evo-kind = { id: int, name: tstr, }\n"),
            QmeditSha256: SchemaLangRegistry.Sha256Hex(source)));
        return registry;
    }

    [Fact]
    public void Check_refuses_without_a_declared_mode()
    {
        var registry = RegistryWithModelessKind();
        var result = registry.CheckVersion(Doc(2, CompatibleV2Body));
        Assert.Null(result.Verdict);
        Assert.NotNull(result.NoModeError);
        Assert.Equal("evo_kind", result.NoModeError!.Functor);
    }

    [Fact]
    public void Register_refuses_without_a_declared_mode_and_writes_nothing()
    {
        var registry = RegistryWithModelessKind();
        var before = registry.All.Count;
        var v2 = Doc(2, CompatibleV2Body);
        var result = registry.RegisterVersion(v2, Lowering.Lower(v2, registry).Artifacts!);
        Assert.Null(result.Records);
        Assert.NotNull(result.NoModeError);
        Assert.Equal("evo_kind", result.NoModeError!.Functor);
        Assert.Equal(before, registry.All.Count);
    }

    // ------------------------------------------------------------------
    // Compatible evolution registers plainly
    // ------------------------------------------------------------------

    [Fact]
    public void Compatible_version_registers_and_extends_the_chain()
    {
        var registry = RegisteredV1();
        var v2 = Doc(2, CompatibleV2Body);
        var result = registry.RegisterVersion(v2, Lowering.Lower(v2, registry).Artifacts!);
        Assert.NotNull(result.Records);
        Assert.Null(result.NoModeError);
        Assert.Null(result.RequiresOverride);

        var record = Assert.Single(result.Records!);
        Assert.Equal(2, record.Version);
        Assert.Null(record.Override);

        var chain = registry.Versions("evo_kind");
        Assert.Equal(new[] { 1, 2 }, chain.Versions.Select(r => r.Version));
        // A new version of the SAME kind keeps the kind's payload-type byte.
        Assert.Equal(chain.Versions[0].PayloadType, chain.Versions[1].PayloadType);
    }

    // ------------------------------------------------------------------
    // US4 AS-3: incompatible registration only via a recorded override
    // ------------------------------------------------------------------

    [Fact]
    public void Incompatible_version_without_override_is_refused_and_writes_nothing()
    {
        var registry = RegisteredV1();
        var before = registry.All.Count;
        var v2 = Doc(2, IncompatibleV2Body);
        var result = registry.RegisterVersion(v2, Lowering.Lower(v2, registry).Artifacts!);

        Assert.Null(result.Records);
        Assert.NotNull(result.RequiresOverride);
        Assert.Equal(CompatOutcome.Incompatible, result.RequiresOverride!.Outcome);
        Assert.Contains(result.RequiresOverride.Breaks, b => b.Construct.Contains("name"));
        Assert.Equal(before, registry.All.Count);
        Assert.Equal(1, registry.LookupByFunctor("evo_kind").Version); // never registered silently
    }

    [Fact]
    public void Override_registers_and_the_record_is_retrievable()
    {
        var registry = RegisteredV1();
        var v2 = Doc(2, IncompatibleV2Body);
        var refused = registry.RegisterVersion(v2, Lowering.Lower(v2, registry).Artifacts!);
        Assert.NotNull(refused.RequiresOverride);

        var overrideRecord = new OverrideRecord(refused.RequiresOverride!, "gabi", "field retired after migration");
        var records = registry.RegisterVersionWithOverride(
            v2, Lowering.Lower(v2, registry).Artifacts!, overrideRecord);

        var record = Assert.Single(records);
        Assert.Equal(2, record.Version);
        Assert.NotNull(record.Override);
        Assert.Equal("gabi", record.Override!.Acknowledger);
        Assert.Equal("field retired after migration", record.Override.Reason);
        Assert.Equal(CompatOutcome.Incompatible, record.Override.Verdict.Outcome);

        // Retrievable on lookup, alongside the whole chain (US4 AS-3).
        var looked = registry.LookupByFunctor("evo_kind");
        Assert.Equal(2, looked.Version);
        Assert.NotNull(looked.Override);
    }

    // ------------------------------------------------------------------
    // Additive new kind: a v2 document may ADD a message kind (compatible by definition)
    // ------------------------------------------------------------------

    [Fact]
    public void V2_adding_a_brand_new_message_kind_registers_additively()
    {
        var registry = RegisteredV1(); // evo_kind v1, mode Backward
        var v2 = Doc(2, V1Body + "\nmessage side_kind { sequence { note: str } }");
        var artifacts = Lowering.Lower(v2, registry).Artifacts!;

        // The check must not crash on the unregistered kind; the verdict covers evo_kind.
        var check = registry.CheckVersion(v2);
        Assert.Null(check.NoModeError);
        Assert.Null(check.VersionError);
        Assert.True(check.Verdict!.IsCompatible);

        var result = registry.RegisterVersion(v2, artifacts);
        Assert.NotNull(result.Records);
        Assert.Null(result.NoModeError);
        Assert.Null(result.RequiresOverride);
        Assert.Null(result.VersionError);
        Assert.Equal(2, result.Records!.Count);

        // The new kind enters as a first registration: the artifact-allocated byte, the
        // document's inherited mode, the document's version.
        var side = registry.LookupByFunctor("side_kind");
        Assert.Equal(2, side.Version);
        Assert.Equal(CompatMode.Backward, side.CompatMode);
        Assert.Equal(
            artifacts.Registrations.Single(r => r.Functor == "side_kind").PayloadType,
            side.PayloadType);

        // The existing kind's chain extends and keeps its byte.
        var chain = registry.Versions("evo_kind");
        Assert.Equal(new[] { 1, 2 }, chain.Versions.Select(r => r.Version));
        Assert.Equal(chain.Versions[0].PayloadType, chain.Versions[1].PayloadType);
    }

    // ------------------------------------------------------------------
    // Version monotonicity: same or lower version numbers are refused, never appended
    // ------------------------------------------------------------------

    [Fact]
    public void Re_registering_the_same_version_number_is_refused_and_writes_nothing()
    {
        var registry = RegisteredV1();
        var v1Again = Doc(1, CompatibleV2Body);
        var result = registry.RegisterVersion(v1Again, Lowering.Lower(v1Again, registry).Artifacts!);

        Assert.Null(result.Records);
        Assert.NotNull(result.VersionError);
        Assert.Equal("evo_kind", result.VersionError!.Functor);
        Assert.Equal(1, result.VersionError.ProposedVersion);
        Assert.Equal(1, result.VersionError.LatestVersion);
        Assert.Single(registry.Versions("evo_kind").Versions); // nothing written
    }

    [Fact]
    public void Registering_a_lower_version_after_a_higher_one_is_refused()
    {
        var registry = RegisteredV1();
        var v2 = Doc(2, CompatibleV2Body);
        Assert.NotNull(registry.RegisterVersion(v2, Lowering.Lower(v2, registry).Artifacts!).Records);

        var v1Again = Doc(1, V1Body);
        var result = registry.RegisterVersion(v1Again, Lowering.Lower(v1Again, registry).Artifacts!);
        Assert.Null(result.Records);
        Assert.NotNull(result.VersionError);
        Assert.Equal(1, result.VersionError!.ProposedVersion);
        Assert.Equal(2, result.VersionError.LatestVersion);
        Assert.Equal(new[] { 1, 2 }, registry.Versions("evo_kind").Versions.Select(r => r.Version));

        // The override path refuses non-monotonic versions too — an override acknowledges an
        // incompatibility, it does not license ambiguous chain ordering.
        var ex = Assert.Throws<InvalidOperationException>(() => registry.RegisterVersionWithOverride(
            v1Again, Lowering.Lower(v1Again, registry).Artifacts!,
            new OverrideRecord(
                new CompatVerdict(CompatMode.Backward, CompatOutcome.Incompatible, Array.Empty<BreakingConstruct>()),
                "gabi", "attempted rollback")));
        Assert.Contains("strictly increasing", ex.Message);
        Assert.Equal(new[] { 1, 2 }, registry.Versions("evo_kind").Versions.Select(r => r.Version));
    }
}
