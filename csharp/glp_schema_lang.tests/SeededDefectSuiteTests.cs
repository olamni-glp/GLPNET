// T014 (feature 043-xsd-schema-language): the SC-002 seeded-defect suite — written FIRST.
//
// SC-002: 100% of a seeded suite of invalid schema documents (≥ 20 cases covering unresolved
// references, facet contradictions, name collisions, and cycle errors) is rejected with an
// error that names the offending construct AND its location.

using GlpRuntime.SchemaLang;

namespace GlpRuntime.SchemaLang.Tests;

public class SeededDefectSuiteTests
{
    public static TheoryData<string, string, string, int> Defects => new()
    {
        // id, document, expected construct substring, expected error line
        // --- unresolved references -------------------------------------------------
        {
            "unresolved-ref",
            "schema s version 1\nmessage m { sequence { a: Missing } }",
            "Missing", 2
        },
        {
            "unresolved-ref-in-list",
            "schema s version 1\nmessage m { sequence { a: [Gone] } }",
            "Gone", 2
        },
        {
            "unresolved-ref-in-choice",
            "schema s version 1\ntype C { choice { x: int\ny: Absent } }\nmessage m { sequence { c: C } }",
            "Absent", 3
        },
        {
            "unresolved-ref-in-named-type",
            "schema s version 1\ntype T { sequence { a: Nowhere } }\nmessage m { sequence { t: T } }",
            "Nowhere", 2
        },
        // --- facet contradictions --------------------------------------------------
        {
            "min-above-max",
            "schema s version 1\ntype T: int { min 9 max 0 }\nmessage m { sequence { a: T } }",
            "min", 2
        },
        {
            "minlength-above-maxlength",
            "schema s version 1\ntype T: str { minLength 10 maxLength 2 }\nmessage m { sequence { a: T } }",
            "minLength", 2
        },
        {
            "negative-minlength",
            "schema s version 1\ntype T: str { minLength -1 }\nmessage m { sequence { a: T } }",
            "minLength", 2
        },
        {
            "empty-enum",
            "schema s version 1\ntype T: str { enum() }\nmessage m { sequence { a: T } }",
            ")", 2
        },
        {
            "empty-language-pattern",
            "schema s version 1\ntype T: str { pattern \"[]\" }\nmessage m { sequence { a: T } }",
            "pattern", 2
        },
        {
            "pattern-outside-subset",
            "schema s version 1\ntype T: str { pattern \"a\\d+\" }\nmessage m { sequence { a: T } }",
            "\\d", 2
        },
        {
            "pattern-lookahead",
            "schema s version 1\ntype T: str { pattern \"(?=a)b\" }\nmessage m { sequence { a: T } }",
            "(?", 2
        },
        {
            "pattern-reversed-range",
            "schema s version 1\ntype T: str { pattern \"[z-a]\" }\nmessage m { sequence { a: T } }",
            "z-a", 2
        },
        {
            "duplicate-facet",
            "schema s version 1\ntype T: int { min 0 min 1 }\nmessage m { sequence { a: T } }",
            "min", 2
        },
        {
            "facet-on-wrong-base",
            "schema s version 1\ntype T: int { minLength 3 }\nmessage m { sequence { a: T } }",
            "minLength", 2
        },
        {
            "pattern-on-bytes",
            "schema s version 1\ntype T: bytes { pattern \"a\" }\nmessage m { sequence { a: T } }",
            "pattern", 2
        },
        {
            "enum-member-out-of-range",
            "schema s version 1\ntype T: int { min 0 max 5 enum(1, 9) }\nmessage m { sequence { a: T } }",
            "enum", 2
        },
        {
            "enum-member-fails-pattern",
            "schema s version 1\ntype T: str { pattern \"[a-z]+\" enum(ok, Bad) }\nmessage m { sequence { a: T } }",
            "enum", 2
        },
        {
            "enum-duplicate-member",
            "schema s version 1\ntype T: str { enum(a, b, a) }\nmessage m { sequence { a: T } }",
            "enum", 2
        },
        {
            "non-integer-enum-member-on-int",
            "schema s version 1\ntype T: int { enum(one, 2) }\nmessage m { sequence { a: T } }",
            "enum", 2
        },
        // --- name collisions ---------------------------------------------------------
        {
            "duplicate-type-name",
            "schema s version 1\ntype T: int { }\ntype T: str { }\nmessage m { sequence { a: T } }",
            "T", 3
        },
        {
            "duplicate-element-name",
            "schema s version 1\nmessage m { sequence { a: int\na: str } }",
            "a", 3
        },
        {
            "duplicate-functor",
            "schema s version 1\nmessage m { sequence { a: int } }\nmessage m { sequence { b: int } }",
            "m", 3
        },
        // --- cycles (clarification 2: full path named) -------------------------------
        {
            "self-reference-cycle",
            "schema s version 1\ntype A { sequence { a: A } }\nmessage m { sequence { x: A } }",
            "A → A", 2
        },
        {
            "two-type-cycle",
            "schema s version 1\ntype A { sequence { b: B } }\ntype B { sequence { a: A } }\nmessage m { sequence { x: A } }",
            "A → B → A", 2
        },
        {
            "three-type-cycle",
            "schema s version 1\ntype A { sequence { b: B } }\ntype B { sequence { c: C } }\ntype C { sequence { a: A } }\nmessage m { sequence { x: A } }",
            "A → B → C → A", 2
        },
        {
            "cycle-through-list",
            "schema s version 1\ntype A { sequence { items: [A] } }\nmessage m { sequence { x: A } }",
            "A → A", 2
        },
        // --- occurs & composition ----------------------------------------------------
        {
            "occurs-min-above-max",
            "schema s version 1\nmessage m { sequence { a: int occurs 5..2 } }",
            "a", 2
        },
        {
            "occurs-negative-min",
            "schema s version 1\nmessage m { sequence { a: int occurs -1..2 } }",
            "a", 2
        },
        {
            "single-element-choice",
            "schema s version 1\ntype C { choice { only: int } }\nmessage m { sequence { c: C } }",
            "C", 2
        },
        {
            "empty-sequence",
            "schema s version 1\nmessage m { sequence { } }",
            "m", 2
        },
        // --- out-of-int-range literals (loud parse errors, never silent truncation) ----
        {
            "version-out-of-int-range",
            "schema s version 9999999999\nmessage m { sequence { a: int } }",
            "9999999999", 1
        },
        {
            "occurs-max-out-of-int-range",
            "schema s version 1\nmessage m { sequence { a: int occurs 0..4294967296 } }",
            "4294967296", 2
        },
        {
            "maxlength-out-of-int-range",
            "schema s version 1\ntype T: str { maxLength 4294967296 }\nmessage m { sequence { a: T } }",
            "4294967296", 2
        },
    };

    [Fact]
    public void Suite_has_at_least_20_seeded_defects()
    {
        Assert.True(Defects.Count >= 20, $"SC-002 requires ≥ 20 cases, have {Defects.Count}");
    }

    [Theory]
    [MemberData(nameof(Defects))]
    public void Defect_is_rejected_naming_construct_and_location(string id, string document, string construct, int line)
    {
        var result = SchemaValidator.Validate(document);
        Assert.False(result.IsValid, $"[{id}] expected rejection, but the document validated");
        Assert.Null(result.Document);

        var match = result.Errors.FirstOrDefault(e => e.Construct.Contains(construct));
        Assert.True(match is not null,
            $"[{id}] no error names construct '{construct}'; got: " +
            string.Join("; ", result.Errors.Select(e => e.ToString())));
        Assert.True(match!.Location.Line == line,
            $"[{id}] expected the error at line {line}, got {match.Location} ({match.Message})");
        Assert.True(match.Location.Col >= 1, $"[{id}] error must carry a column");
    }
}
