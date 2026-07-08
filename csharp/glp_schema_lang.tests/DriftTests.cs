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

    // ------------------------------------------------------------------
    // Single-report precedence: CDDL first when BOTH forms drifted (lift-fidelity.md)
    // ------------------------------------------------------------------

    [Fact]
    public void Both_forms_drifted_yields_one_report_with_cddl_precedence()
    {
        var registry = RegisteredChat();
        var record = registry.LookupByFunctor("chat_message");
        registry.MutateOverlayCddlOutOfBand("chat_message", record.Cddl! + "// edited\n");
        registry.MutateOverlayQmeditOutOfBand("chat_message", "// edited out-of-band\n");

        var result = Lifter.Lift(registry, "chat_message");
        Assert.NotNull(result.Drift);
        Assert.Equal(RegistryForm.Cddl, result.Drift!.Form); // the formal form takes precedence
        Assert.Equal(record.CddlSha256, result.Drift.StoredSha256);
    }

    // ------------------------------------------------------------------
    // Evolution path: a drifted entry REFUSES before any compat comparison (FR-013)
    // ------------------------------------------------------------------

    [Fact]
    public void Evolution_paths_refuse_a_drifted_entry_before_compat_checking()
    {
        var registry = RegisteredChat();
        var record = registry.LookupByFunctor("chat_message");
        registry.MutateOverlayCddlOutOfBand("chat_message", record.Cddl! + "// edited\n");

        var v2 = LoweringTests.Doc(SchemaValidatorTests.ChatSchema.Replace("version 1", "version 2"));

        var checkEx = Assert.Throws<InvalidOperationException>(() => registry.CheckVersion(v2));
        Assert.Contains("drifted out-of-band", checkEx.Message);
        Assert.Contains("CDDL", checkEx.Message);

        var before = registry.All.Count;
        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterVersion(v2, Lowering.Lower(v2, registry).Artifacts!));
        Assert.Equal(before, registry.All.Count); // nothing written

        var overrideEx = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterVersionWithOverride(v2, Lowering.Lower(v2, registry).Artifacts!,
                new OverrideRecord(
                    new CompatVerdict(CompatMode.Full, CompatOutcome.Incompatible, Array.Empty<BreakingConstruct>()),
                    "gabi", "test")));
        Assert.Contains("drifted out-of-band", overrideEx.Message);
        Assert.Equal(before, registry.All.Count);
    }
}
