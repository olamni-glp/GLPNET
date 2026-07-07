// T019 (feature 043-xsd-schema-language): instance-validation tests — written FIRST; T021
// makes them green.
//
// Contract: specs/043-xsd-schema-language/contracts/validation-api.md — check order kind →
// structure → facets; Pass or Fail with every violation naming the violated element/facet AND
// its instance path (US2 AS-2); unregistered functor throws NoSchemaRegisteredError, never a
// silent pass (FR-008); bounded/deterministic on adversarially deep instances (edge case).

using GlpRuntime.SchemaLang;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang.Tests;

public class InstanceValidationTests
{
    private static SchemaLangRegistry RegistryWithChatAndKitchen()
    {
        var registry = new SchemaLangRegistry();
        foreach (var text in new[] { SchemaValidatorTests.ChatSchema, LoweringTests.KitchenSchema })
        {
            var doc = LoweringTests.Doc(text);
            var lowered = Lowering.Lower(doc, registry);
            Assert.Null(lowered.Error);
            var registered = registry.Register(doc, lowered.Artifacts!, CompatMode.Full);
            Assert.Null(registered.Error);
        }
        return registry;
    }

    private static InstanceValue ChatInstance(
        string from = "gabi",
        long? priority = 5,
        InstanceValue? body = null)
    {
        var fields = new List<(string, InstanceValue)> { ("from", new InstanceValue.Str(from)) };
        if (priority is long p) fields.Add(("priority", new InstanceValue.Int(p)));
        fields.Add(("body", body ?? InstanceValue.OfStruct("body", ("text", new InstanceValue.Str("hello")))));
        return InstanceValue.OfStruct("chat_message", fields.ToArray());
    }

    private static ValidationVerdict Validate(InstanceValue instance, string functor = "chat_message") =>
        InstanceValidator.Validate(RegistryWithChatAndKitchen(), functor, instance);

    // ------------------------------------------------------------------
    // Pass verdicts
    // ------------------------------------------------------------------

    [Fact]
    public void Conforming_instance_passes()
    {
        Assert.True(Validate(ChatInstance()).IsPass);
    }

    [Fact]
    public void Optional_element_may_be_absent()
    {
        Assert.True(Validate(ChatInstance(priority: null)).IsPass);
    }

    [Fact]
    public void Occurs_bounded_choice_branch_passes_within_bounds()
    {
        var attachment = InstanceValue.OfStruct("attachment",
            ("name", new InstanceValue.Str("gabi")),
            ("size", new InstanceValue.Int(10)));
        var body = InstanceValue.OfStruct("body",
            ("attachments", InstanceValue.OfList(attachment, attachment)));
        Assert.True(Validate(ChatInstance(body: body)).IsPass);
    }

    // ------------------------------------------------------------------
    // Facet violations — verdict names the facet AND the instance path
    // ------------------------------------------------------------------

    [Fact]
    public void Out_of_range_value_names_facet_and_path()
    {
        var verdict = Validate(ChatInstance(priority: 42));
        Assert.False(verdict.IsPass);
        var violation = Assert.Single(verdict.Violations);
        Assert.Equal(ConstructKind.Facet, violation.ConstructKind);
        Assert.Equal("max", violation.ConstructName);
        Assert.Equal("priority", violation.InstancePath);
    }

    [Fact]
    public void Length_violation_names_facet_and_path()
    {
        var verdict = Validate(ChatInstance(from: ""));
        Assert.False(verdict.IsPass);
        Assert.Contains(verdict.Violations, v =>
            v.ConstructKind == ConstructKind.Facet && v.ConstructName == "minLength" && v.InstancePath == "from");
    }

    [Fact]
    public void Pattern_violation_names_facet_and_path()
    {
        var verdict = Validate(ChatInstance(from: "Gabi"));
        Assert.False(verdict.IsPass);
        Assert.Contains(verdict.Violations, v =>
            v.ConstructKind == ConstructKind.Facet && v.ConstructName == "pattern" && v.InstancePath == "from");
    }

    [Fact]
    public void Enum_violation_names_facet_and_path()
    {
        var instance = InstanceValue.OfStruct("kitchen_sink",
            ("color", new InstanceValue.Str("purple")),
            ("level", new InstanceValue.Int(2)),
            ("tags", InstanceValue.OfList()),
            ("blobs", InstanceValue.OfList(
                new InstanceValue.Bytes(new byte[] { 1 }), new InstanceValue.Bytes(new byte[] { 2 }))),
            ("flag", new InstanceValue.Bool(true)),
            ("count", new InstanceValue.Int(0)),
            ("tag", new InstanceValue.Str("x")),
            ("small", new InstanceValue.Str("ok")));
        var verdict = Validate(instance, "kitchen_sink");
        Assert.False(verdict.IsPass);
        Assert.Contains(verdict.Violations, v =>
            v.ConstructKind == ConstructKind.Facet && v.ConstructName == "enum" && v.InstancePath == "color");
    }

    [Fact]
    public void Nested_paths_use_dot_and_index_notation()
    {
        var badAttachment = InstanceValue.OfStruct("attachment",
            ("name", new InstanceValue.Str("BAD")), // pattern violation deep inside
            ("size", new InstanceValue.Int(1)));
        var body = InstanceValue.OfStruct("body", ("attachments", InstanceValue.OfList(badAttachment)));
        var verdict = Validate(ChatInstance(body: body));
        Assert.False(verdict.IsPass);
        Assert.Contains(verdict.Violations, v => v.InstancePath == "body.attachments[0].name");
    }

    // ------------------------------------------------------------------
    // Composition violations
    // ------------------------------------------------------------------

    [Fact]
    public void Missing_mandatory_element_names_the_element()
    {
        var instance = InstanceValue.OfStruct("chat_message",
            ("from", new InstanceValue.Str("gabi"))); // body missing
        var verdict = Validate(instance);
        Assert.False(verdict.IsPass);
        Assert.Contains(verdict.Violations, v =>
            v.ConstructKind == ConstructKind.Element && v.ConstructName == "body" && v.Message.Contains("missing"));
    }

    [Fact]
    public void Wrong_choice_branch_arity_is_a_composition_violation()
    {
        var body = InstanceValue.OfStruct("body",
            ("text", new InstanceValue.Str("hi")),
            ("attachments", InstanceValue.OfList(InstanceValue.OfStruct("attachment",
                ("name", new InstanceValue.Str("gabi")),
                ("size", new InstanceValue.Int(1))))));
        var verdict = Validate(ChatInstance(body: body));
        Assert.False(verdict.IsPass);
        Assert.Contains(verdict.Violations, v =>
            v.ConstructKind == ConstructKind.Composition && v.InstancePath == "body" && v.Message.Contains("exactly one"));
    }

    [Fact]
    public void Occurs_out_of_bounds_names_the_element()
    {
        var attachment = InstanceValue.OfStruct("attachment",
            ("name", new InstanceValue.Str("gabi")),
            ("size", new InstanceValue.Int(1)));
        var body = InstanceValue.OfStruct("body",
            ("attachments", new InstanceValue.List(Enumerable.Repeat((InstanceValue)attachment, 9).ToList())));
        var verdict = Validate(ChatInstance(body: body));
        Assert.False(verdict.IsPass);
        Assert.Contains(verdict.Violations, v =>
            v.ConstructName == "attachments" && v.Message.Contains("occurs"));
    }

    [Fact]
    public void Sequence_order_violation_is_named()
    {
        var instance = InstanceValue.OfStruct("chat_message",
            ("priority", new InstanceValue.Int(3)),
            ("from", new InstanceValue.Str("gabi")), // out of order: from must precede priority
            ("body", InstanceValue.OfStruct("body", ("text", new InstanceValue.Str("hi")))));
        var verdict = Validate(instance);
        Assert.False(verdict.IsPass);
        Assert.Contains(verdict.Violations, v =>
            v.ConstructKind == ConstructKind.Composition && v.Message.Contains("order"));
    }

    [Fact]
    public void Undeclared_element_is_a_violation_closed_world()
    {
        var instance = InstanceValue.OfStruct("chat_message",
            ("from", new InstanceValue.Str("gabi")),
            ("smuggled", new InstanceValue.Int(1)),
            ("body", InstanceValue.OfStruct("body", ("text", new InstanceValue.Str("hi")))));
        var verdict = Validate(instance);
        Assert.False(verdict.IsPass);
        Assert.Contains(verdict.Violations, v =>
            v.ConstructKind == ConstructKind.Element && v.ConstructName == "smuggled");
    }

    [Fact]
    public void Base_type_mismatch_is_a_violation_with_path()
    {
        var verdict = Validate(ChatInstance(body: new InstanceValue.Int(7)));
        Assert.False(verdict.IsPass);
        Assert.Contains(verdict.Violations, v => v.InstancePath == "body");
    }

    // ------------------------------------------------------------------
    // FR-008: unregistered kind is an explicit error, never a silent pass
    // ------------------------------------------------------------------

    [Fact]
    public void Unregistered_functor_throws_NoSchemaRegisteredError()
    {
        var registry = RegistryWithChatAndKitchen();
        var error = Assert.Throws<NoSchemaRegisteredError>(() =>
            InstanceValidator.Validate(registry, "no_such_kind", ChatInstance()));
        Assert.Equal("no_such_kind", error.Functor);
    }

    [Fact]
    public void Kind_without_xsd_level_schema_throws_not_passes()
    {
        // crdt_message is seeded at the registry level but carries no XSD-level source until
        // re-registered through this layer — validation must loud-fail, never silently pass.
        var registry = RegistryWithChatAndKitchen();
        Assert.Throws<NoSchemaRegisteredError>(() =>
            InstanceValidator.Validate(registry, "crdt_message", ChatInstance()));
    }

    // ------------------------------------------------------------------
    // Boundedness edge case
    // ------------------------------------------------------------------

    [Fact]
    public void Adversarially_deep_instance_is_bounded_and_deterministic()
    {
        // 100k-deep nesting: traversal is schema-directed, so depth is bounded by the schema
        // DAG, not the instance — no stack overflow, just a located violation.
        InstanceValue deep = new InstanceValue.Int(0);
        for (var i = 0; i < 100_000; i++)
            deep = InstanceValue.OfStruct("n", ("x", deep));
        var verdict = Validate(ChatInstance(body: deep));
        Assert.False(verdict.IsPass);
        Assert.NotEmpty(verdict.Violations);
    }
}
