// T024 (feature 043-xsd-schema-language): drift-detection tests — written FIRST; T026 makes
// them green.
//
// Contract: specs/043-xsd-schema-language/contracts/lift-fidelity.md §Drift detection
// (FR-013, research R9): registration stores sha256(cddl) + sha256(qmedit); on every lift of
// an entry with stored XSD-level source the CURRENT forms are re-hashed and compared —
// mismatch attaches a DriftReport naming the diverged form, and the rendering is produced
// from CURRENT registry truth, never the stale stored XSD source.

using GlpRuntime.SchemaLang;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang.Tests;

public class DriftTests
{
    private static SchemaLangRegistry RegisteredChat()
    {
        var registry = new SchemaLangRegistry();
        var doc = LoweringTests.Doc(SchemaValidatorTests.ChatSchema);
        var lowered = Lowering.Lower(doc, registry);
        Assert.Null(lowered.Error);
        Assert.Null(registry.Register(doc, lowered.Artifacts!, CompatMode.Full).Error);
        return registry;
    }

    [Fact]
    public void Untouched_entry_lifts_with_no_drift()
    {
        var result = Lifter.Lift(RegisteredChat(), "chat_message");
        Assert.Null(result.Drift);
    }

    [Fact]
    public void Out_of_band_cddl_edit_is_flagged_and_current_truth_rendered()
    {
        var registry = RegisteredChat();
        var record = registry.LookupByFunctor("chat_message");

        // Out-of-band E9-level edit (test seam): drop the optional priority element from the
        // registered CDDL without touching the stored hashes or the stored XSD source.
        var mutated = record.Cddl!
            .Replace("  ? priority: priority,\n", string.Empty)
            .Replace("priority = 0..9\n", string.Empty);
        Assert.NotEqual(record.Cddl, mutated);
        registry.MutateOverlayCddlOutOfBand("chat_message", mutated);

        var result = Lifter.Lift(registry, "chat_message");

        // The divergence is surfaced, naming the diverged form (FR-013).
        Assert.NotNull(result.Drift);
        Assert.Equal(RegistryForm.Cddl, result.Drift!.Form);
        Assert.Equal(record.CddlSha256, result.Drift.StoredSha256);
        Assert.Equal(SchemaLangRegistry.Sha256Hex(mutated), result.Drift.CurrentSha256);
        Assert.NotEqual(result.Drift.StoredSha256, result.Drift.CurrentSha256);

        // The rendering reflects CURRENT registry truth (no priority element) — while the
        // stale XSD source, still retrievable, does mention it (flagged stale, not current).
        var elements = result.Rendering!.Messages.Single().Body.Elements;
        Assert.DoesNotContain(elements, e => e.Name == "priority");
        Assert.Contains("priority", registry.LookupByFunctor("chat_message").XsdSource!);
    }

    [Fact]
    public void Out_of_band_qmedit_edit_is_flagged()
    {
        var registry = RegisteredChat();
        registry.MutateOverlayQmeditOutOfBand("chat_message", "// edited out-of-band\n");

        var result = Lifter.Lift(registry, "chat_message");
        Assert.NotNull(result.Drift);
        Assert.Equal(RegistryForm.Qmedit, result.Drift!.Form);
    }

    [Fact]
    public void Seeded_entries_without_xsd_source_report_no_drift()
    {
        // Drift detection is defined over entries with stored XSD-level source; the seeded
        // 041 entry has none, so its lift carries no drift report.
        var result = Lifter.Lift(new SchemaLangRegistry(), "crdt_message");
        Assert.Null(result.Drift);
    }
}
