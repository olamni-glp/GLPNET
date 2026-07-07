// T028 (feature 043-xsd-schema-language): the SC-005 evolution suite — written FIRST; T030
// makes it green.
//
// Contract: specs/043-xsd-schema-language/contracts/compat-evolution.md — the construct-level
// rule table under the closed-world law, checked pairwise under backward / forward / full
// (full = backward ∧ forward); pattern widen/narrow decided by NFA language inclusion (†);
// transitive = the full check against EVERY stored version in the chain, first failing pair
// named. Every verdict names the breaking construct AND the rule-table row (US4 AS-2).

using GlpRuntime.SchemaLang;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang.Tests;

public class CompatEvolutionTests
{
    private static SchemaDocument Doc(int version, string body) =>
        LoweringTests.Doc($"schema evo version {version}\n{body}");

    // ------------------------------------------------------------------
    // SC-005 rule-table suite (≥ 10 cases spanning every row)
    // ------------------------------------------------------------------

    public static TheoryData<string, string, string, bool, bool, string, string> Cases => new()
    {
        // id, v1 body, v2 body, backwardCompatible, forwardCompatible, rule row substring, construct substring
        {
            "add-optional-element",
            "message evo_kind { sequence { id: int } }",
            "message evo_kind { sequence { id: int  extra?: int } }",
            true, true, "", ""
        },
        {
            "add-mandatory-element",
            "message evo_kind { sequence { id: int } }",
            "message evo_kind { sequence { id: int  extra: int } }",
            false, true, "add mandatory element", "extra"
        },
        {
            "remove-optional-element",
            "message evo_kind { sequence { id: int  note?: str } }",
            "message evo_kind { sequence { id: int } }",
            false, true, "remove optional element", "note"
        },
        {
            "remove-mandatory-element",
            "message evo_kind { sequence { id: int  name: str } }",
            "message evo_kind { sequence { id: int } }",
            false, false, "remove mandatory element", "name"
        },
        {
            "facet-widen-length",
            "type Name: str { minLength 1 maxLength 10 }\nmessage evo_kind { sequence { name: Name } }",
            "type Name: str { minLength 1 maxLength 20 }\nmessage evo_kind { sequence { name: Name } }",
            true, false, "facet widening", "maxLength"
        },
        {
            "facet-narrow-range",
            "type Num: int { min 0 max 9 }\nmessage evo_kind { sequence { n: Num } }",
            "type Num: int { min 0 max 5 }\nmessage evo_kind { sequence { n: Num } }",
            false, true, "facet narrowing", "max"
        },
        {
            "enum-add-member",
            "type Color: str { enum(red, green) }\nmessage evo_kind { sequence { c: Color } }",
            "type Color: str { enum(red, green, blue) }\nmessage evo_kind { sequence { c: Color } }",
            true, false, "facet widening", "enum"
        },
        {
            "enum-remove-member",
            "type Color: str { enum(red, green, blue) }\nmessage evo_kind { sequence { c: Color } }",
            "type Color: str { enum(red, green) }\nmessage evo_kind { sequence { c: Color } }",
            false, true, "facet narrowing", "enum"
        },
        {
            "pattern-superset",
            "type Tag: str { pattern \"[a-c]\" }\nmessage evo_kind { sequence { t: Tag } }",
            "type Tag: str { pattern \"[a-z]\" }\nmessage evo_kind { sequence { t: Tag } }",
            true, false, "facet widening", "pattern"
        },
        {
            "pattern-subset",
            "type Tag: str { pattern \"[a-z]\" }\nmessage evo_kind { sequence { t: Tag } }",
            "type Tag: str { pattern \"[a-c]\" }\nmessage evo_kind { sequence { t: Tag } }",
            false, true, "facet narrowing", "pattern"
        },
        {
            "pattern-disjoint-breaks-both",
            "type Tag: str { pattern \"[a-c]\" }\nmessage evo_kind { sequence { t: Tag } }",
            "type Tag: str { pattern \"[x-z]\" }\nmessage evo_kind { sequence { t: Tag } }",
            false, false, "facet", "pattern"
        },
        {
            "occurs-widen",
            "message evo_kind { sequence { items: int occurs 1..3 } }",
            "message evo_kind { sequence { items: int occurs 0..5 } }",
            true, false, "occurs widening", "items"
        },
        {
            "occurs-narrow",
            "message evo_kind { sequence { items: int occurs 1..3 } }",
            "message evo_kind { sequence { items: int occurs 2..3 } }",
            false, true, "occurs narrowing", "items"
        },
        {
            // Boundary crossing 1..1 → 1..2: scalar becomes list (representation shift ‡) —
            // breaking BOTH directions even though the bounds only "widened".
            "occurs-representation-shift-widen",
            "message evo_kind { sequence { items: int } }",
            "message evo_kind { sequence { items: int occurs 1..2 } }",
            false, false, "representation shift", "items"
        },
        {
            // Boundary crossing 1..2 → 1..1: list becomes scalar — breaking BOTH directions.
            "occurs-representation-shift-narrow",
            "message evo_kind { sequence { items: int occurs 1..2 } }",
            "message evo_kind { sequence { items: int } }",
            false, false, "representation shift", "items"
        },
        {
            // Boundary crossing 0..1 → 0..2: optional scalar becomes list.
            "occurs-representation-shift-from-optional",
            "message evo_kind { sequence { items?: int } }",
            "message evo_kind { sequence { items: int occurs 0..2 } }",
            false, false, "representation shift", "items"
        },
        {
            "add-choice-branch",
            "type Body { choice { a: int  b: str } }\nmessage evo_kind { sequence { body: Body } }",
            "type Body { choice { a: int  b: str  c: bytes } }\nmessage evo_kind { sequence { body: Body } }",
            true, false, "add choice branch", "c"
        },
        {
            "remove-choice-branch",
            "type Body { choice { a: int  b: str  c: bytes } }\nmessage evo_kind { sequence { body: Body } }",
            "type Body { choice { a: int  b: str } }\nmessage evo_kind { sequence { body: Body } }",
            false, true, "remove choice branch", "c"
        },
        {
            "type-change",
            "message evo_kind { sequence { id: int } }",
            "message evo_kind { sequence { id: str } }",
            false, false, "type change", "id"
        },
        {
            "sequence-reorder",
            "message evo_kind { sequence { id: int  name: str } }",
            "message evo_kind { sequence { name: str  id: int } }",
            false, false, "sequence reorder", ""
        },
    };

    [Fact]
    public void Suite_has_at_least_10_cases()
    {
        Assert.True(Cases.Count >= 10, $"SC-005 requires ≥ 10 cases, have {Cases.Count}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Rule_table_verdicts(
        string id, string v1Body, string v2Body,
        bool backwardCompatible, bool forwardCompatible,
        string rule, string construct)
    {
        var v1 = Doc(1, v1Body);
        var v2 = Doc(2, v2Body);

        var backward = CompatChecker.Check(v1, v2, CompatMode.Backward);
        var forward = CompatChecker.Check(v1, v2, CompatMode.Forward);
        var full = CompatChecker.Check(v1, v2, CompatMode.Full);

        Assert.True(backwardCompatible == backward.IsCompatible,
            $"[{id}] backward expected {backwardCompatible}; breaks: {Render(backward)}");
        Assert.True(forwardCompatible == forward.IsCompatible,
            $"[{id}] forward expected {forwardCompatible}; breaks: {Render(forward)}");
        Assert.True((backwardCompatible && forwardCompatible) == full.IsCompatible,
            $"[{id}] full expected {backwardCompatible && forwardCompatible}; breaks: {Render(full)}");

        // Every breaking verdict names the breaking construct and the rule-table row.
        if (!backwardCompatible)
            AssertNamed(backward, CompatDirection.Backward, rule, construct, id);
        if (!forwardCompatible)
            AssertNamed(forward, CompatDirection.Forward, rule, construct, id);
    }

    private static void AssertNamed(CompatVerdict verdict, CompatDirection direction, string rule, string construct, string id)
    {
        Assert.True(verdict.Breaks.Any(b =>
                b.Direction == direction
                && b.Rule.Contains(rule, StringComparison.OrdinalIgnoreCase)
                && b.Construct.Contains(construct)),
            $"[{id}] no {direction} break naming rule '{rule}' + construct '{construct}'; got: {Render(verdict)}");
        Assert.All(verdict.Breaks, b => Assert.False(string.IsNullOrWhiteSpace(b.Location)));
    }

    private static string Render(CompatVerdict verdict) =>
        string.Join("; ", verdict.Breaks.Select(b => $"{b.Direction}:{b.Rule}:{b.Construct}@{b.Location}"));

    // ------------------------------------------------------------------
    // Transitive: the full check over the WHOLE version chain (3 versions)
    // ------------------------------------------------------------------

    [Fact]
    public void Transitive_checks_every_stored_version_in_the_chain()
    {
        var registry = new SchemaLangRegistry();

        // v1 (mode Transitive) has a mandatory element old_field.
        var v1 = Doc(1, "message evo_kind { sequence { id: int  old_field: int } }");
        Assert.Null(registry.Register(v1, LoweringTests.LowerOk(v1), CompatMode.Transitive).Error);

        // v2 removes old_field — incompatible; registered only via a recorded override.
        var v2 = Doc(2, "message evo_kind { sequence { id: int  new_field?: int } }");
        var v2Result = registry.RegisterVersion(v2, Lowering.Lower(v2, registry).Artifacts!);
        Assert.NotNull(v2Result.RequiresOverride);
        registry.RegisterVersionWithOverride(v2, Lowering.Lower(v2, registry).Artifacts!,
            new OverrideRecord(v2Result.RequiresOverride!, "gabi", "planned break, peers migrated"));

        // v3 only adds an optional element to v2 — pairwise-compatible with v2, but the
        // TRANSITIVE check runs against v1 too, where the old_field removal still breaks.
        var v3 = Doc(3, "message evo_kind { sequence { id: int  new_field?: int  extra?: int } }");
        var v3Result = registry.RegisterVersion(v3, Lowering.Lower(v3, registry).Artifacts!);
        Assert.Null(v3Result.Records);
        Assert.NotNull(v3Result.RequiresOverride);
        Assert.Contains(v3Result.RequiresOverride!.Breaks, b => b.Construct.Contains("old_field"));

        // Pairwise against v2 alone it is fully compatible.
        var v2Doc = Doc(2, "message evo_kind { sequence { id: int  new_field?: int } }");
        Assert.True(CompatChecker.Check(v2Doc, v3, CompatMode.Full).IsCompatible);
    }

    // ------------------------------------------------------------------
    // † Pattern inclusion beyond the state cap → conservatively breaking, said explicitly
    // ------------------------------------------------------------------

    [Fact]
    public void Unestablishable_pattern_inclusion_is_conservatively_breaking_in_both_directions()
    {
        // Equal languages spelled differently: each product walk exceeds the inclusion state
        // cap (≥ 2^17 subset pairs), so inclusion is Unknown in BOTH directions and the
        // verdict must say so — never a silent Compatible.
        var v1 = Doc(1,
            "type Tag: str { pattern \"(a|b)*a(a|b){17}\" }\nmessage evo_kind { sequence { t: Tag } }");
        var v2 = Doc(2,
            "type Tag: str { pattern \"(a|b)*a(a|b){9}(a|b){8}\" }\nmessage evo_kind { sequence { t: Tag } }");

        var verdict = CompatChecker.Check(v1, v2, CompatMode.Full);
        Assert.False(verdict.IsCompatible);
        Assert.Contains(verdict.Breaks, b => b.Direction == CompatDirection.Backward
            && b.Rule.Contains("pattern inclusion not established (conservatively breaking)"));
        Assert.Contains(verdict.Breaks, b => b.Direction == CompatDirection.Forward
            && b.Rule.Contains("pattern inclusion not established (conservatively breaking)"));
    }

    // ------------------------------------------------------------------
    // Precondition breach: compat checking is defined over VALIDATED documents (FR-014)
    // ------------------------------------------------------------------

    [Fact]
    public void Unvalidated_document_with_unresolved_reference_is_a_named_error()
    {
        var v1 = Doc(1, "message evo_kind { sequence { id: int } }");
        var v2 = SchemaDslParser.Parse(
            "schema evo version 2\nmessage evo_kind { sequence { id: Gone } }").Document!;
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CompatChecker.Check(v1, v2, CompatMode.Full));
        Assert.Contains("Gone", ex.Message);
        Assert.Contains("not validated", ex.Message);
    }
}
