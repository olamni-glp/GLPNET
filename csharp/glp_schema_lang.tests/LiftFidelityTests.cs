// T023 (feature 043-xsd-schema-language): lift + fidelity tests — written FIRST; T025/T026
// make them green.
//
// Contract: specs/043-xsd-schema-language/contracts/lift-fidelity.md — lift parses the
// REGISTERED CDDL artifact; never approximates (out-of-subset constructs become per-construct
// UnexpressibleConstruct entries, outcome Partial); byte-only kinds yield a whole-entry
// Partial report; Lift(Lower(doc)) is structurally equivalent modulo canonical naming
// (FR-010) and accept/reject-equivalent over the shared corpus (SC-004).

using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.SchemaLang;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang.Tests;

public class LiftFidelityTests
{
    private static SchemaLangRegistry Registered(params string[] schemas)
    {
        var registry = new SchemaLangRegistry();
        foreach (var text in schemas)
        {
            var doc = LoweringTests.Doc(text);
            var lowered = Lowering.Lower(doc, registry);
            Assert.Null(lowered.Error);
            Assert.Null(registry.Register(doc, lowered.Artifacts!, CompatMode.Full).Error);
        }
        return registry;
    }

    // ------------------------------------------------------------------
    // Lift of the seeded crdt_message (the one DSL-formed 041 kind)
    // ------------------------------------------------------------------

    [Fact]
    public void Seeded_crdt_message_lifts_to_a_full_fidelity_rendering()
    {
        var result = Lifter.Lift(new SchemaLangRegistry(), "crdt_message");
        Assert.Equal(FidelityOutcome.Full, result.Fidelity.Outcome);
        Assert.Empty(result.Fidelity.Unexpressible);
        Assert.Null(result.Drift);
        Assert.NotNull(result.Rendering);

        var message = Assert.Single(result.Rendering!.Messages);
        Assert.Equal("crdt_message", message.Functor);
        Assert.Equal(
            new[] { "schema_version", "payload_type", "crdt_model", "header", "sections" },
            message.Body.Elements.Select(e => e.Name));
    }

    [Fact]
    public void Lifted_rendering_source_reparses_and_revalidates()
    {
        var result = Lifter.Lift(new SchemaLangRegistry(), "crdt_message");
        var revalidated = SchemaValidator.Validate(result.Rendering!.Source);
        Assert.True(revalidated.IsValid,
            "printed lift source must re-validate: " +
            string.Join("; ", revalidated.Errors.Select(e => e.ToString())));
        SchemaEquivalence.AssertEquivalent(result.Rendering, revalidated.Document!);
    }

    [Fact]
    public void Lifted_crdt_message_relowers_and_relifts_to_a_fixed_point()
    {
        // Lower-then-compare (SC-004): re-lower the lifted rendering, park the new CDDL as a
        // v2 record (test seam), lift again — the two renderings must be equivalent.
        var registry = new SchemaLangRegistry();
        var lifted = Lifter.Lift(registry, "crdt_message");
        var relowered = Lowering.Lower(lifted.Rendering!, registry);
        Assert.Null(relowered.Error);

        var v1 = registry.LookupByFunctor("crdt_message");
        registry.AppendOverlay(v1 with
        {
            Version = 2,
            Cddl = relowered.Artifacts!.Cddl,
            CddlSha256 = SchemaLangRegistry.Sha256Hex(relowered.Artifacts.Cddl),
            XsdSource = lifted.Rendering!.Source,
            QmeditSha256 = v1.QmeditSha256,
            IsSeeded = false,
        });
        var relifted = Lifter.Lift(registry, "crdt_message");
        Assert.Equal(FidelityOutcome.Full, relifted.Fidelity.Outcome);
        SchemaEquivalence.AssertEquivalent(lifted.Rendering, relifted.Rendering!);
    }

    [Fact]
    public void Lifted_crdt_message_agrees_with_the_registry_level_over_the_corpus()
    {
        // Accept/reject equivalence over the shared shape-level corpus (SC-004). The corpus
        // here is scoped to what the CDDL artifact itself carries: version-policy and
        // payload-registry checks live in decode guards outside the CDDL, so their mutations
        // are excluded (same scoping as validation-api.md A1).
        var rendering = Lifter.Lift(new SchemaLangRegistry(), "crdt_message").Rendering!;
        foreach (var message in ShapeLevelCorpus())
        {
            var verdict = InstanceValidator.Validate(
                rendering, "crdt_message", MessageInstanceAdapter.ToInstance(message.Msg));
            Assert.True(verdict.IsPass == message.ShapeConforming,
                $"[{message.Name}] lifted-schema verdict {verdict.IsPass} != expected {message.ShapeConforming}; " +
                string.Join("; ", verdict.Violations.Select(v => $"{v.ConstructName}@{v.InstancePath}")));
        }
    }

    private static IEnumerable<(string Name, Message Msg, bool ShapeConforming)> ShapeLevelCorpus()
    {
        var minimal = new Message(1, PayloadType.CrdtMessage,
            new Header("m-0", "alice", "bob", 0, RoutingPolicy.Empty),
            Array.Empty<Section>(), CrdtModel.None);
        yield return ("minimal", minimal, true);
        yield return ("rich-policy", minimal with
        {
            Header = minimal.Header with
            {
                Policy = new RoutingPolicy(new[] { "bob" }, new[] { "relay1" }, new[] { "mallory" }),
            },
            Sections = new[] { new Section(0x40, new byte[] { 1, 2 }) },
            CrdtModel = CrdtModel.OpBased,
        }, true);
        yield return ("invalid-crdt-model", minimal with { CrdtModel = (CrdtModel)9 }, false);
    }

    // ------------------------------------------------------------------
    // Byte-only kinds: whole-entry partial (lift law 3)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("il_program")]
    [InlineData("result_envelope")]
    public void Byte_only_kind_lifts_to_whole_entry_partial(string functor)
    {
        var result = Lifter.Lift(new SchemaLangRegistry(), functor);
        Assert.Null(result.Rendering);
        Assert.Equal(FidelityOutcome.Partial, result.Fidelity.Outcome);
        var entry = Assert.Single(result.Fidelity.Unexpressible);
        Assert.Contains("no CDDL artifact", entry.Reason);
    }

    // ------------------------------------------------------------------
    // Out-of-subset constructs: per-construct report, zero silent approximation
    // ------------------------------------------------------------------

    [Fact]
    public void Out_of_subset_constructs_are_reported_per_construct_never_approximated()
    {
        var registry = new SchemaLangRegistry();
        var cddl = "weird-kind = {\n  a: int,\n  b: #6.32(tstr),\n}\nstrange = #6.1(int)\n";
        registry.AppendOverlay(new RegistryRecord(
            0x20, "weird_kind", CompatMode.Full,
            QmeditDsl: "-", Cddl: cddl, XsdSource: null,
            SchemaName: "weird", Version: 1,
            CddlSha256: SchemaLangRegistry.Sha256Hex(cddl), QmeditSha256: SchemaLangRegistry.Sha256Hex("-")));

        var result = Lifter.Lift(registry, "weird_kind");
        Assert.Equal(FidelityOutcome.Partial, result.Fidelity.Outcome);
        Assert.Contains(result.Fidelity.Unexpressible, u => u.CddlConstruct.Contains("#6.32(tstr)"));
        Assert.Contains(result.Fidelity.Unexpressible, u => u.CddlConstruct.Contains("#6.1(int)"));

        // The rendering carries what IS expressible; the omission of 'b' is a report entry,
        // never silent.
        Assert.NotNull(result.Rendering);
        var elements = result.Rendering!.Messages.Single().Body.Elements;
        Assert.Contains(elements, e => e.Name == "a");
        Assert.DoesNotContain(elements, e => e.Name == "b");
    }

    // ------------------------------------------------------------------
    // .size bounds beyond int range: fidelity entries, never a crash or a silent wrap (FR-009)
    // ------------------------------------------------------------------

    [Fact]
    public void Size_bounds_beyond_int_range_are_fidelity_entries_not_crashes()
    {
        var registry = new SchemaLangRegistry();
        var cddl = "big-kind = {\n  a: wide,\n  b: huge,\n}\n"
            + "wide = tstr .size (0..4294967295)\n"
            + "huge = tstr .size (4294967296..18446744073709551615)\n";
        registry.AppendOverlay(new RegistryRecord(
            0x20, "big_kind", CompatMode.Full,
            QmeditDsl: "-", Cddl: cddl, XsdSource: null,
            SchemaName: "big", Version: 1,
            CddlSha256: SchemaLangRegistry.Sha256Hex(cddl), QmeditSha256: SchemaLangRegistry.Sha256Hex("-")));

        var result = Lifter.Lift(registry, "big_kind"); // must not throw
        Assert.Equal(FidelityOutcome.Partial, result.Fidelity.Outcome);
        // Upper bound above int.MaxValue (and not the no-upper sentinel): a NAMED entry.
        Assert.Contains(result.Fidelity.Unexpressible,
            u => u.CddlConstruct.Contains(".size (0..4294967295)")
                && u.Reason.Contains("out of representable range"));
        // Lower bound above int.MaxValue: a NAMED entry, never a silently wrapped minLength.
        Assert.Contains(result.Fidelity.Unexpressible,
            u => u.CddlConstruct.Contains(".size (4294967296..")
                && u.Reason.Contains("out of representable range"));
    }

    // ------------------------------------------------------------------
    // String-aware rule splitting: literal brackets/commas inside pattern strings are TEXT
    // ------------------------------------------------------------------

    private const string BracketPatternSchema = """
        schema brkt version 1

        type OpenParen: str { pattern "[(]" }
        type NotClose: str { pattern "[^)]*" }
        type Braces: str { pattern "[{][}]" }
        type Squares: str { pattern "[[]" }

        message brkt_kind {
          sequence {
            o: OpenParen
            n: NotClose
            b: Braces
            s: Squares
          }
        }
        """;

    [Fact]
    public void Patterns_with_literal_brackets_roundtrip_with_full_fidelity()
    {
        var doc = LoweringTests.Doc(BracketPatternSchema);
        var registry = Registered(BracketPatternSchema);
        var result = Lifter.Lift(registry, "brkt_kind");
        Assert.Equal(FidelityOutcome.Full, result.Fidelity.Outcome);
        SchemaEquivalence.AssertEquivalent(doc, result.Rendering!);
    }

    [Fact]
    public void Out_of_subset_entry_with_string_delimiters_is_captured_whole()
    {
        var registry = new SchemaLangRegistry();
        var cddl = "odd-kind = {\n  a: tstr .regexp \"a,b(\" .within x,\n  b: int,\n}\n";
        registry.AppendOverlay(new RegistryRecord(
            0x21, "odd_kind", CompatMode.Full,
            QmeditDsl: "-", Cddl: cddl, XsdSource: null,
            SchemaName: "odd", Version: 1,
            CddlSha256: SchemaLangRegistry.Sha256Hex(cddl), QmeditSha256: SchemaLangRegistry.Sha256Hex("-")));

        var result = Lifter.Lift(registry, "odd_kind");
        Assert.Equal(FidelityOutcome.Partial, result.Fidelity.Outcome);
        // The captured span is the WHOLE entry value: the ',' and '(' inside the pattern
        // string are text, not delimiters.
        Assert.Contains(result.Fidelity.Unexpressible,
            u => u.CddlConstruct.Contains(".within x") && u.CddlConstruct.Contains("\"a,b(\""));
        // The entry AFTER the string-bearing one still parses into the rendering.
        var elements = result.Rendering!.Messages.Single().Body.Elements;
        Assert.DoesNotContain(elements, e => e.Name == "a");
        Assert.Contains(elements, e => e.Name == "b");
    }

    // ------------------------------------------------------------------
    // Printer/lexer alphabet: non-identifier enum members print as DSL string literals
    // ------------------------------------------------------------------

    [Fact]
    public void Enum_members_outside_the_dsl_identifier_alphabet_print_as_string_literals()
    {
        var registry = new SchemaLangRegistry();
        var cddl = "modey-kind = {\n  m: mode,\n  l: level,\n}\n"
            + "mode = &( no-op: 0, run: 1 )\n"
            + "level = 1 / 2\n";
        registry.AppendOverlay(new RegistryRecord(
            0x22, "modey_kind", CompatMode.Full,
            QmeditDsl: "-", Cddl: cddl, XsdSource: null,
            SchemaName: "modey", Version: 1,
            CddlSha256: SchemaLangRegistry.Sha256Hex(cddl), QmeditSha256: SchemaLangRegistry.Sha256Hex("-")));

        var result = Lifter.Lift(registry, "modey_kind");
        Assert.Equal(FidelityOutcome.Full, result.Fidelity.Outcome);
        Assert.NotNull(result.Rendering);

        // 'no-op' is liftable CDDL but NOT a DSL bareword; the digit-leading int-alternate
        // members are not identifiers either — the printed Source must still re-parse.
        var revalidated = SchemaValidator.Validate(result.Rendering!.Source);
        Assert.True(revalidated.IsValid,
            "printed lift source must re-validate: " +
            string.Join("; ", revalidated.Errors.Select(e => e.ToString())));
        SchemaEquivalence.AssertEquivalent(result.Rendering, revalidated.Document!);

        var mode = Assert.IsType<SimpleType>(revalidated.Document!.Types.Single(t => t.Name == "Mode"));
        Assert.Equal(new[] { "no-op", "run" }, mode.Facets.OfType<EnumerationFacet>().Single().Members);
    }

    // ------------------------------------------------------------------
    // FR-010 round-trip: Lift(Lower(doc)) ≍ doc
    // ------------------------------------------------------------------

    [Fact]
    public void Chat_roundtrip_lift_of_lower_is_structurally_equivalent()
    {
        var doc = LoweringTests.Doc(SchemaValidatorTests.ChatSchema);
        var registry = Registered(SchemaValidatorTests.ChatSchema);
        var result = Lifter.Lift(registry, "chat_message");
        Assert.Equal(FidelityOutcome.Full, result.Fidelity.Outcome);
        Assert.Null(result.Drift);
        SchemaEquivalence.AssertEquivalent(doc, result.Rendering!);
    }

    [Fact]
    public void Kitchen_roundtrip_covers_enums_lists_occurs_and_size_defaults()
    {
        var doc = LoweringTests.Doc(LoweringTests.KitchenSchema);
        var registry = Registered(LoweringTests.KitchenSchema);
        var result = Lifter.Lift(registry, "kitchen_sink");
        Assert.Equal(FidelityOutcome.Full, result.Fidelity.Outcome);
        SchemaEquivalence.AssertEquivalent(doc, result.Rendering!);
    }
}
