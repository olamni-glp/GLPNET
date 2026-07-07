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
    // Array occurs bounds beyond int range: fidelity entries, never a silent wrap (lift law 2)
    // ------------------------------------------------------------------

    [Fact]
    public void Array_occurs_bounds_beyond_int_range_are_fidelity_entries_not_silent_wraps()
    {
        var registry = new SchemaLangRegistry();
        var cddl = "occ-kind = {\n  e: [0*4294967296 tstr],\n  f: [4294967297* tstr],\n  g: int,\n}\n";
        registry.AppendOverlay(new RegistryRecord(
            0x24, "occ_kind", CompatMode.Full,
            QmeditDsl: "-", Cddl: cddl, XsdSource: null,
            SchemaName: "occ", Version: 1,
            CddlSha256: SchemaLangRegistry.Sha256Hex(cddl), QmeditSha256: SchemaLangRegistry.Sha256Hex("-")));

        var result = Lifter.Lift(registry, "occ_kind"); // must not wrap 2^32 to occurs 0
        Assert.Equal(FidelityOutcome.Partial, result.Fidelity.Outcome);
        Assert.Contains(result.Fidelity.Unexpressible,
            u => u.CddlConstruct.Contains("[0*4294967296")
                && u.Reason.Contains("occurs bound out of representable range"));
        Assert.Contains(result.Fidelity.Unexpressible,
            u => u.CddlConstruct.Contains("[4294967297*")
                && u.Reason.Contains("occurs bound out of representable range"));

        // The omissions are report entries; the in-range entry still lifts.
        var elements = result.Rendering!.Messages.Single().Body.Elements;
        Assert.DoesNotContain(elements, e => e.Name == "e");
        Assert.DoesNotContain(elements, e => e.Name == "f");
        Assert.Contains(elements, e => e.Name == "g");
    }

    // ------------------------------------------------------------------
    // Sibling-functor exclusion is scoped to THIS record's artifact (FR-010 / lift law 1):
    // an unrelated document's functor must not suppress a same-named helper rule here
    // ------------------------------------------------------------------

    [Fact]
    public void Helper_rule_named_like_a_foreign_functor_still_lifts_as_a_type()
    {
        // Doc B registers functor 'username' (CDDL rule 'username'); doc A has a helper TYPE
        // 'Username' — also rule 'username' — inside A's OWN artifact. The helper must lift
        // with A's functor: excluding it would leave an unresolved NamedRef and mis-report a
        // legal in-subset artifact as Partial.
        const string docB = """
            schema audit version 1
            message username { sequence { who: str } }
            """;
        const string docA = """
            schema profile version 1

            type Username: str { minLength 1  maxLength 64 }

            message profile_kind {
              sequence {
                name: Username
              }
            }
            """;
        var registry = Registered(docB, docA);
        var result = Lifter.Lift(registry, "profile_kind");

        Assert.Equal(FidelityOutcome.Full, result.Fidelity.Outcome);
        Assert.Contains(result.Rendering!.Types, t => t.Name == "Username");
        SchemaEquivalence.AssertEquivalent(LoweringTests.Doc(docA), result.Rendering!);
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
    // Symmetric string escaping: pattern text with literal backslashes (including a TRAILING
    // one) round-trips through emitter/printer and all scanners — never a latched string
    // ------------------------------------------------------------------

    private const string BackslashPatternSchema = """
        schema bslash version 1

        type Tail: str { pattern "x\\\\" }
        type Escaped: str { pattern "\\\\d[0-9]" }
        type Dotted: str { pattern "[a-z]\\." }

        message bslash_kind {
          sequence {
            t: Tail
            e: Escaped
            d: Dotted
          }
        }
        """;

    [Fact]
    public void Patterns_with_literal_backslashes_roundtrip_with_full_fidelity()
    {
        var doc = LoweringTests.Doc(BackslashPatternSchema);
        // DSL `"x\\\\"` unescapes to the pattern text `x\\` — regex 'x' + escaped literal
        // backslash: the text ENDS in a backslash, the corner that used to swallow quotes.
        var tail = Assert.IsType<SimpleType>(doc.Types.Single(t => t.Name == "Tail"));
        Assert.Equal("x\\\\", tail.Facets.OfType<PatternFacet>().Single().Pattern);
        var escaped = Assert.IsType<SimpleType>(doc.Types.Single(t => t.Name == "Escaped"));
        Assert.Equal("\\\\d[0-9]", escaped.Facets.OfType<PatternFacet>().Single().Pattern);

        var registry = Registered(BackslashPatternSchema);
        var result = Lifter.Lift(registry, "bslash_kind");
        Assert.Equal(FidelityOutcome.Full, result.Fidelity.Outcome);
        SchemaEquivalence.AssertEquivalent(doc, result.Rendering!);

        // The printed Source re-tokenizes and re-validates: the trailing literal backslash
        // must not swallow the closing quote (printer/lexer escape symmetry).
        var revalidated = SchemaValidator.Validate(result.Rendering!.Source);
        Assert.True(revalidated.IsValid,
            "printed lift source must re-validate: " +
            string.Join("; ", revalidated.Errors.Select(e => e.ToString())));
        SchemaEquivalence.AssertEquivalent(result.Rendering, revalidated.Document!);
    }

    [Fact]
    public void Rule_after_a_trailing_backslash_pattern_still_splits_correctly()
    {
        // Hand-registered CDDL: `pat` carries `.regexp "x\\\\"` (escaped form of the pattern
        // text `x\\`). The scanners must consume `\\` as a unit so the string CLOSES at its
        // real quote and the following `after` rule still splits into its own rule.
        var registry = new SchemaLangRegistry();
        var cddl = "bs-kind = {\n  a: pat,\n  b: after,\n}\n"
            + "pat = tstr .regexp \"x\\\\\\\\\"\n"
            + "after = tstr .size (1..9)\n";
        registry.AppendOverlay(new RegistryRecord(
            0x23, "bs_kind", CompatMode.Full,
            QmeditDsl: "-", Cddl: cddl, XsdSource: null,
            SchemaName: "bs", Version: 1,
            CddlSha256: SchemaLangRegistry.Sha256Hex(cddl), QmeditSha256: SchemaLangRegistry.Sha256Hex("-")));

        var result = Lifter.Lift(registry, "bs_kind");
        Assert.Equal(FidelityOutcome.Full, result.Fidelity.Outcome);
        var pat = Assert.IsType<SimpleType>(result.Rendering!.Types.Single(t => t.Name == "Pat"));
        Assert.Equal("x\\\\", pat.Facets.OfType<PatternFacet>().Single().Pattern);
        var after = Assert.IsType<SimpleType>(result.Rendering.Types.Single(t => t.Name == "After"));
        Assert.Contains(after.Facets, f => f is MaxLengthFacet { Value: 9 });
    }

    // ------------------------------------------------------------------
    // DSL name alphabet on lift: CDDL names outside the DSL grammar are fidelity entries,
    // never a Full rendering whose printed Source cannot re-parse (printer invariant)
    // ------------------------------------------------------------------

    [Fact]
    public void Map_key_outside_the_dsl_elem_name_alphabet_is_a_fidelity_entry()
    {
        // 'foo-bar' is a legal CDDL map key but no DSL elem-name (lower_snake, schema-dsl.md):
        // lifting it verbatim would claim Full while the printed Source fails to re-parse.
        var registry = new SchemaLangRegistry();
        var cddl = "dash-kind = {\n  foo-bar: tstr,\n  ok_key: int,\n}\n";
        registry.AppendOverlay(new RegistryRecord(
            0x25, "dash_kind", CompatMode.Full,
            QmeditDsl: "-", Cddl: cddl, XsdSource: null,
            SchemaName: "dash", Version: 1,
            CddlSha256: SchemaLangRegistry.Sha256Hex(cddl), QmeditSha256: SchemaLangRegistry.Sha256Hex("-")));

        var result = Lifter.Lift(registry, "dash_kind");
        Assert.Equal(FidelityOutcome.Partial, result.Fidelity.Outcome);
        Assert.Contains(result.Fidelity.Unexpressible,
            u => u.CddlConstruct.Contains("foo-bar")
                && u.Reason.Contains("element name not expressible in the schema DSL"));

        // The lower_snake entry still lifts; the omission is the report entry; the printed
        // Source re-parses and re-validates.
        var elements = result.Rendering!.Messages.Single().Body.Elements;
        Assert.DoesNotContain(elements, e => e.Name == "foo-bar");
        Assert.Contains(elements, e => e.Name == "ok_key");
        var revalidated = SchemaValidator.Validate(result.Rendering!.Source);
        Assert.True(revalidated.IsValid,
            "printed lift source must re-validate: " +
            string.Join("; ", revalidated.Errors.Select(e => e.ToString())));
        SchemaEquivalence.AssertEquivalent(result.Rendering, revalidated.Document!);
    }

    [Fact]
    public void Rule_name_that_does_not_canonicalize_to_a_dsl_type_name_is_a_fidelity_entry()
    {
        // Rule STARTS may carry '.', '@', '$' (CddlSubsetParser.TryMatchRuleStart); such a
        // name canonicalizes to no DSL type name and would print as an unparseable `type`.
        var registry = new SchemaLangRegistry();
        var cddl = "dotty-kind = {\n  a: int,\n}\nweird.rule = int\n";
        registry.AppendOverlay(new RegistryRecord(
            0x26, "dotty_kind", CompatMode.Full,
            QmeditDsl: "-", Cddl: cddl, XsdSource: null,
            SchemaName: "dotty", Version: 1,
            CddlSha256: SchemaLangRegistry.Sha256Hex(cddl), QmeditSha256: SchemaLangRegistry.Sha256Hex("-")));

        var result = Lifter.Lift(registry, "dotty_kind");
        Assert.Equal(FidelityOutcome.Partial, result.Fidelity.Outcome);
        Assert.Contains(result.Fidelity.Unexpressible,
            u => u.CddlConstruct.Contains("weird.rule")
                && u.Reason.Contains("does not canonicalize to a DSL type name"));

        // The in-alphabet content still lifts and prints re-parsably.
        var elements = result.Rendering!.Messages.Single().Body.Elements;
        Assert.Contains(elements, e => e.Name == "a");
        Assert.Empty(result.Rendering.Types);
        var revalidated = SchemaValidator.Validate(result.Rendering!.Source);
        Assert.True(revalidated.IsValid,
            "printed lift source must re-validate: " +
            string.Join("; ", revalidated.Errors.Select(e => e.ToString())));
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
