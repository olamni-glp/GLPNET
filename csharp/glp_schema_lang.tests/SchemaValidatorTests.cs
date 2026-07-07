// T010 (feature 043-xsd-schema-language): parser + schema-document validator tests.
//
// Contracts: schema-dsl.md (grammar + well-formedness), validation-api.md (all errors in one
// pass, construct + line:col on every error, cycle errors name the FULL path — clarification 2).

using GlpRuntime.SchemaLang;

namespace GlpRuntime.SchemaLang.Tests;

public class SchemaValidatorTests
{
    // The SC-006 walkthrough schema (contracts/schema-dsl.md §Example).
    internal const string ChatSchema = """
        schema chat version 1

        type UserName: str { minLength 1  maxLength 64  pattern "[a-z][a-z0-9_]*" }
        type Priority: int { min 0  max 9 }
        type Attachment { sequence { name: UserName  size: int } }
        type Body { choice { text: str  attachments: Attachment occurs 1..8 } }

        message chat_message {
          sequence {
            from:     UserName
            priority: Priority occurs 0..1     // optional
            body:     Body
          }
        }
        """;

    private static SchemaDocument ValidateOk(string text)
    {
        var result = SchemaValidator.Validate(text);
        Assert.True(result.IsValid,
            "expected valid, got: " + string.Join("; ", result.Errors.Select(e => e.ToString())));
        return result.Document!;
    }

    private static IReadOnlyList<SchemaValidationError> ValidateErr(string text)
    {
        var result = SchemaValidator.Validate(text);
        Assert.False(result.IsValid, "expected errors, but the document validated");
        Assert.Null(result.Document);
        Assert.NotEmpty(result.Errors);
        return result.Errors;
    }

    // ------------------------------------------------------------------
    // Parse round-trip of the quickstart chat schema
    // ------------------------------------------------------------------

    [Fact]
    public void Chat_schema_parses_and_validates()
    {
        var doc = ValidateOk(ChatSchema);

        Assert.Equal("chat", doc.Name);
        Assert.Equal(1, doc.Version);
        Assert.Equal(ChatSchema, doc.Source); // FR-004: source retained verbatim
        Assert.Equal(new[] { "UserName", "Priority", "Attachment", "Body" }, doc.Types.Select(t => t.Name));
        Assert.Equal(new[] { "chat_message" }, doc.Messages.Select(m => m.Functor));

        var userName = Assert.IsType<SimpleType>(doc.Types[0]);
        Assert.Equal(PrimitiveKind.Str, userName.Base);
        Assert.Collection(userName.Facets,
            f => Assert.Equal(1, Assert.IsType<MinLengthFacet>(f).Value),
            f => Assert.Equal(64, Assert.IsType<MaxLengthFacet>(f).Value),
            f => Assert.Equal("[a-z][a-z0-9_]*", Assert.IsType<PatternFacet>(f).Pattern));

        var priority = Assert.IsType<SimpleType>(doc.Types[1]);
        Assert.Equal(PrimitiveKind.Int, priority.Base);

        var attachment = Assert.IsType<ComplexType>(doc.Types[2]);
        Assert.Equal(CompositionKind.Sequence, attachment.Composition.Kind);
        Assert.Equal(2, attachment.Composition.Elements.Count);
        Assert.Equal("UserName", Assert.IsType<NamedRef>(attachment.Composition.Elements[0].Type).Name);
        Assert.Equal(PrimitiveKind.Int, Assert.IsType<PrimitiveRef>(attachment.Composition.Elements[1].Type).Kind);

        var body = Assert.IsType<ComplexType>(doc.Types[3]);
        Assert.Equal(CompositionKind.Choice, body.Composition.Kind);
        Assert.Equal(new Occurs(1, 8), body.Composition.Elements[1].Occurs);

        var message = doc.Messages[0];
        Assert.Equal(CompositionKind.Sequence, message.Body.Kind);
        Assert.Equal(Occurs.One, message.Body.Elements[0].Occurs);
        Assert.Equal(Occurs.Optional, message.Body.Elements[1].Occurs);
    }

    [Fact]
    public void Optional_sugar_and_list_refs_parse()
    {
        var doc = ValidateOk("""
            schema s version 2
            message m {
              sequence {
                cap?: bytes
                targets: [str]
                nested: [[int]]
              }
            }
            """);
        var elements = doc.Messages[0].Body.Elements;
        Assert.Equal(Occurs.Optional, elements[0].Occurs);
        var targets = Assert.IsType<ListRef>(elements[1].Type);
        Assert.Equal(PrimitiveKind.Str, Assert.IsType<PrimitiveRef>(targets.Element).Kind);
        var nested = Assert.IsType<ListRef>(elements[2].Type);
        Assert.IsType<ListRef>(nested.Element);
    }

    [Fact]
    public void Unbounded_occurs_parses()
    {
        var doc = ValidateOk("""
            schema s version 1
            message m { sequence { items: int occurs 2..* } }
            """);
        Assert.Equal(new Occurs(2, null), doc.Messages[0].Body.Elements[0].Occurs);
    }

    [Fact]
    public void Optional_sugar_conflicting_with_occurs_is_a_parse_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            message m { sequence { e?: int occurs 0..1 } }
            """);
        Assert.Contains(errors, e => e.Operation == "parse" && e.Message.Contains("both"));
    }

    [Fact]
    public void Multiple_parse_errors_are_reported_via_recovery()
    {
        var errors = ValidateErr("""
            schema s version 1
            type lower: int { }
            type Ok { sequence { a: int } }
            message BadName { sequence { a: int } }
            """);
        Assert.Contains(errors, e => e.Construct == "lower");
        Assert.Contains(errors, e => e.Construct == "BadName");
    }

    // ------------------------------------------------------------------
    // Rule 1: uniqueness (positive covered by chat schema)
    // ------------------------------------------------------------------

    [Fact]
    public void Duplicate_type_name_is_an_error_with_location()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: int { }
            type T: str { }
            message m { sequence { a: int } }
            """);
        var error = Assert.Single(errors);
        Assert.Equal("T", error.Construct);
        Assert.Equal(3, error.Location.Line);
        Assert.Contains("duplicate type name", error.Message);
    }

    [Fact]
    public void Duplicate_element_name_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            message m { sequence { a: int  a: str } }
            """);
        var error = Assert.Single(errors);
        Assert.Equal("a", error.Construct);
        Assert.Contains("duplicate element name", error.Message);
    }

    [Fact]
    public void Duplicate_functor_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            message m { sequence { a: int } }
            message m { sequence { b: int } }
            """);
        var error = Assert.Single(errors);
        Assert.Equal("m", error.Construct);
        Assert.Contains("duplicate message functor", error.Message);
    }

    // ------------------------------------------------------------------
    // Rule 2: reference resolution
    // ------------------------------------------------------------------

    [Fact]
    public void Unresolved_reference_is_an_error_naming_the_reference()
    {
        var errors = ValidateErr("""
            schema s version 1
            message m { sequence { a: Missing } }
            """);
        var error = Assert.Single(errors);
        Assert.Equal("Missing", error.Construct);
        Assert.Contains("unresolved type reference", error.Message);
    }

    [Fact]
    public void Unresolved_reference_inside_list_is_found()
    {
        var errors = ValidateErr("""
            schema s version 1
            message m { sequence { a: [Missing] } }
            """);
        Assert.Contains(errors, e => e.Construct == "Missing");
    }

    // ------------------------------------------------------------------
    // Rule 3: facet consistency
    // ------------------------------------------------------------------

    [Fact]
    public void Min_above_max_is_a_facet_contradiction()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: int { min 9 max 0 }
            message m { sequence { a: T } }
            """);
        var error = Assert.Single(errors);
        Assert.Equal("min", error.Construct);
        Assert.Contains("min 9 exceeds max 0", error.Message);
    }

    [Fact]
    public void MinLength_above_maxLength_is_a_facet_contradiction()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: str { minLength 10 maxLength 2 }
            message m { sequence { a: T } }
            """);
        Assert.Contains(errors, e => e.Construct == "minLength" && e.Message.Contains("exceeds maxLength"));
    }

    [Fact]
    public void Facet_on_wrong_base_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: int { minLength 1 }
            message m { sequence { a: T } }
            """);
        Assert.Contains(errors, e => e.Construct == "minLength" && e.Message.Contains("not applicable"));
    }

    [Fact]
    public void Pattern_on_non_str_base_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: bytes { pattern "a" }
            message m { sequence { a: T } }
            """);
        Assert.Contains(errors, e => e.Construct == "pattern" && e.Message.Contains("not applicable"));
    }

    [Fact]
    public void Empty_enumeration_is_a_facet_contradiction()
    {
        // `enum()` is a parse error (grammar requires ≥1 member) — the parse layer reports it.
        var errors = ValidateErr("""
            schema s version 1
            type T: str { enum() }
            message m { sequence { a: T } }
            """);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Duplicate_enum_member_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: str { enum(a, b, a) }
            message m { sequence { a: T } }
            """);
        Assert.Contains(errors, e => e.Construct == "enum" && e.Message.Contains("repeats member 'a'"));
    }

    [Fact]
    public void Enum_member_violating_cofacet_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: int { min 0 max 5 enum(1, 9) }
            message m { sequence { a: T } }
            """);
        Assert.Contains(errors, e => e.Construct == "enum" && e.Message.Contains("violates co-facet max 5"));
    }

    [Fact]
    public void Enum_member_not_matching_pattern_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: str { pattern "[a-z]+" enum(ok, Bad) }
            message m { sequence { a: T } }
            """);
        Assert.Contains(errors, e => e.Construct == "enum" && e.Message.Contains("does not match the co-facet pattern"));
    }

    [Fact]
    public void Empty_language_pattern_is_a_facet_contradiction()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: str { pattern "[]" }
            message m { sequence { a: T } }
            """);
        Assert.Contains(errors, e => e.Construct == "pattern" && e.Message.Contains("empty language"));
    }

    [Fact]
    public void Pattern_outside_the_subset_names_the_construct()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: str { pattern "a\d+" }
            message m { sequence { a: T } }
            """);
        Assert.Contains(errors, e => e.Construct == "\\d");
    }

    [Fact]
    public void Duplicate_facet_kind_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: int { min 0 min 1 max 5 }
            message m { sequence { a: T } }
            """);
        Assert.Contains(errors, e => e.Construct == "min" && e.Message.Contains("duplicate facet"));
    }

    // ------------------------------------------------------------------
    // Rule 4: DAG — cycle errors name the FULL path (clarification 2)
    // ------------------------------------------------------------------

    [Fact]
    public void Two_type_cycle_names_full_path()
    {
        var errors = ValidateErr("""
            schema s version 1
            type A { sequence { b: B } }
            type B { sequence { a: A } }
            message m { sequence { x: A } }
            """);
        var error = Assert.Single(errors);
        Assert.Equal("A → B → A", error.Construct);
        Assert.Contains("cyclic type reference A → B → A", error.Message);
    }

    [Fact]
    public void Self_reference_is_a_cycle()
    {
        var errors = ValidateErr("""
            schema s version 1
            type A { sequence { a: A } }
            message m { sequence { x: A } }
            """);
        var error = Assert.Single(errors);
        Assert.Equal("A → A", error.Construct);
    }

    [Fact]
    public void Cycle_through_a_list_ref_is_detected()
    {
        var errors = ValidateErr("""
            schema s version 1
            type A { sequence { items: [A] } }
            message m { sequence { x: A } }
            """);
        Assert.Contains(errors, e => e.Construct == "A → A");
    }

    [Fact]
    public void Three_type_cycle_names_full_path()
    {
        var errors = ValidateErr("""
            schema s version 1
            type A { sequence { b: B } }
            type B { sequence { c: C } }
            type C { sequence { a: A } }
            message m { sequence { x: A } }
            """);
        var error = Assert.Single(errors);
        Assert.Equal("A → B → C → A", error.Construct);
    }

    [Fact]
    public void Diamond_reuse_is_not_a_cycle()
    {
        ValidateOk("""
            schema s version 1
            type Leaf: int { }
            type L { sequence { x: Leaf } }
            type R { sequence { x: Leaf } }
            message m { sequence { l: L  r: R } }
            """);
    }

    // ------------------------------------------------------------------
    // Rule 5: occurs bounds
    // ------------------------------------------------------------------

    [Fact]
    public void Occurs_min_above_finite_max_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            message m { sequence { a: int occurs 5..2 } }
            """);
        var error = Assert.Single(errors);
        Assert.Equal("a", error.Construct);
        Assert.Contains("minimum above maximum", error.Message);
    }

    [Fact]
    public void Negative_occurs_min_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            message m { sequence { a: int occurs -1..2 } }
            """);
        Assert.Contains(errors, e => e.Construct == "a" && e.Message.Contains("must be ≥ 0"));
    }

    // ------------------------------------------------------------------
    // Rule 6: composition arity
    // ------------------------------------------------------------------

    [Fact]
    public void Single_element_choice_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            type C { choice { only: int } }
            message m { sequence { c: C } }
            """);
        Assert.Contains(errors, e => e.Construct == "C" && e.Message.Contains("at least 2"));
    }

    [Fact]
    public void Empty_sequence_is_an_error()
    {
        var errors = ValidateErr("""
            schema s version 1
            message m { sequence { } }
            """);
        Assert.Contains(errors, e => e.Construct == "m" && e.Message.Contains("at least 1"));
    }

    // ------------------------------------------------------------------
    // All errors in one pass (validation-api.md)
    // ------------------------------------------------------------------

    [Fact]
    public void All_wellformedness_errors_reported_in_one_pass()
    {
        var errors = ValidateErr("""
            schema s version 1
            type T: int { min 9 max 0 }
            type A { sequence { a: A } }
            message m { sequence { x: Missing  y: T } }
            """);
        Assert.Contains(errors, e => e.Construct == "min");
        Assert.Contains(errors, e => e.Construct == "A → A");
        Assert.Contains(errors, e => e.Construct == "Missing");
        Assert.True(errors.Count >= 3);
    }

    [Fact]
    public void Errors_carry_line_and_column()
    {
        var errors = ValidateErr("schema s version 1\ntype T: int { min 9 max 0 }\nmessage m { sequence { a: T } }");
        var error = Assert.Single(errors);
        Assert.Equal(2, error.Location.Line);
        Assert.True(error.Location.Col > 0);
    }

    // ------------------------------------------------------------------
    // Bounded, deterministic behavior on adversarial schema TEXT (spec edge case)
    // ------------------------------------------------------------------

    [Fact]
    public void Adversarially_long_reference_chain_validates_without_stack_overflow()
    {
        // A 50k-type chain A1 → A2 → … → A50000: the acyclicity walk must be iterative —
        // recursion depth proportional to the chain length would kill the process.
        var sb = new System.Text.StringBuilder("schema deep version 1\n");
        for (var i = 1; i < 50000; i++)
            sb.Append("type A").Append(i).Append(" { sequence { a: A").Append(i + 1).Append(" } }\n");
        sb.Append("type A50000 { sequence { a: int } }\n");
        sb.Append("message m { sequence { x: A1 } }\n");
        ValidateOk(sb.ToString());
    }

    [Fact]
    public void Type_reference_nesting_beyond_the_limit_is_a_located_parse_error()
    {
        var errors = ValidateErr(
            "schema s version 1\nmessage m { sequence { a: "
            + new string('[', 100) + "int" + new string(']', 100) + " } }");
        var error = errors.First(e => e.Message.Contains("type reference nesting too deep"));
        Assert.Equal(2, error.Location.Line);
        Assert.True(error.Location.Col >= 1);
    }
}
