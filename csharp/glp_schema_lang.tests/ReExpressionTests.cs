// T033 (feature 043-xsd-schema-language): the SC-001 re-expression acceptance.
//
// SC-001: every message kind registered by the 041 MVP set can be re-expressed in the new
// language, and each re-expression lowers to registry entries that accept and reject the same
// instance corpus as the original entries (zero divergence over the corpus). The 041 MVP set
// is the 3 seeded entries; the DSL-formed kind is crdt_message (its T022 re-expression + the
// T020 corpus carry the divergence check); the two byte-only kinds (il_program,
// result_envelope) carry no shape to re-express and are covered by explicit partial-fidelity
// reports (the SC-004 path — plan Scale/Scope).

using GlpRuntime.SchemaLang;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang.Tests;

public class ReExpressionTests
{
    [Fact]
    public void The_041_mvp_denominator_is_pinned()
    {
        // If the seeded set ever grows, SC-001's coverage below must grow with it.
        Assert.Equal(
            new[] { "il_program", "result_envelope", "crdt_message" },
            GlpRuntime.WireRegistry.WireRegistry.All.Select(e => e.Functor));
    }

    [Fact]
    public void Crdt_message_reexpression_lowers_to_registry_accepted_entries()
    {
        var doc = CorpusAgreementTests.ReExpressedCrdtMessage();
        var lowered = Lowering.Lower(doc);
        Assert.Null(lowered.Error);
        var registration = Assert.Single(lowered.Artifacts!.Registrations);
        Assert.Equal("crdt_message", registration.Functor);
        Assert.False(string.IsNullOrWhiteSpace(lowered.Artifacts.Cddl));
    }

    [Fact]
    public void Crdt_message_reexpression_has_zero_divergence_over_the_shared_corpus()
    {
        var doc = CorpusAgreementTests.ReExpressedCrdtMessage();
        foreach (string name in CorpusAgreementTests.CorpusCases)
        {
            var message = CorpusAgreementTests.CorpusMessage(name);
            var registryAccepts = CorpusAgreementTests.RegistryLevelAccepts(message);
            var verdict = InstanceValidator.Validate(
                doc, "crdt_message", MessageInstanceAdapter.ToInstance(message));
            Assert.False(!registryAccepts && verdict.IsPass,
                $"[{name}] SC-001 divergence: registry-level REJECT but re-expression PASS");
        }
    }

    [Theory]
    [InlineData("il_program")]
    [InlineData("result_envelope")]
    public void Byte_only_kinds_are_fidelity_report_covered(string functor)
    {
        // No shape to re-express: SC-001 coverage for these kinds is the explicit
        // partial-fidelity report (never a silent approximation).
        var result = Lifter.Lift(new SchemaLangRegistry(), functor);
        Assert.Equal(FidelityOutcome.Partial, result.Fidelity.Outcome);
        Assert.Contains(result.Fidelity.Unexpressible, u => u.Reason.Contains("no CDDL artifact"));
    }
}
