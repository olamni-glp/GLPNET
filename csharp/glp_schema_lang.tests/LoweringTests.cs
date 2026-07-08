// T012 (feature 043-xsd-schema-language): golden lowering tests — written FIRST; T015/T016
// make them green.
//
// Contract: specs/043-xsd-schema-language/contracts/lowering.md. The quickstart `chat` schema
// lowers to byte-identical golden CDDL (FR-005; golden/chat.cddl); lowering is self-identical
// across runs; every mapping-table row is exercised; payload types allocate 0x13-onwards in
// message declaration order over seed ∪ overlay (research R3); an unlowerable document's
// error lists EVERY offending construct (registration law 4).

using GlpRuntime.SchemaLang;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang.Tests;

public class LoweringTests
{
    internal static SchemaDocument Doc(string text)
    {
        var result = SchemaValidator.Validate(text);
        Assert.True(result.IsValid,
            "test schema must validate: " + string.Join("; ", result.Errors.Select(e => e.ToString())));
        return result.Document!;
    }

    internal static LoweringArtifactSet LowerOk(SchemaDocument doc)
    {
        var result = Lowering.Lower(doc);
        Assert.Null(result.Error);
        return result.Artifacts!;
    }

    private static string Golden(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "golden", name);
        return File.ReadAllText(path).ReplaceLineEndings("\n");
    }

    // ------------------------------------------------------------------
    // Golden CDDL (FR-005)
    // ------------------------------------------------------------------

    [Fact]
    public void Chat_schema_lowers_to_the_golden_cddl()
    {
        var artifacts = LowerOk(Doc(SchemaValidatorTests.ChatSchema));
        Assert.Equal(Golden("chat.cddl"), artifacts.Cddl);
    }

    [Fact]
    public void Lowering_is_self_identical_across_runs()
    {
        var doc = Doc(SchemaValidatorTests.ChatSchema);
        var first = LowerOk(doc);
        var second = LowerOk(doc);
        Assert.Equal(first.Cddl, second.Cddl);
        Assert.Equal(first.Registrations, second.Registrations);
    }

    // ------------------------------------------------------------------
    // Every remaining mapping row (contracts/lowering.md table)
    // ------------------------------------------------------------------

    internal const string KitchenSchema = """
        schema kitchen version 1

        type Color: str { enum(red, green, blue) }
        type Level: int { enum(1, 2, 4) }
        type Count: int { min 0 }
        type Tag: str { minLength 1 }
        type Small: str { maxLength 9 }

        message kitchen_sink {
          sequence {
            color: Color
            level: Level
            tags: [str]
            blobs: bytes occurs 2..*
            flag: bool
            count: Count
            tag: Tag
            small: Small
          }
        }
        """;

    [Fact]
    public void Remaining_mapping_rows_lower_as_contracted()
    {
        var artifacts = LowerOk(Doc(KitchenSchema));
        var expected = """
            kitchen-sink = {
              color: color,
              level: level,
              tags: [* tstr],
              blobs: [2* bstr],
              flag: bool,
              count: count,
              tag: tag,
              small: small,
            }
            color = &( red: 0, green: 1, blue: 2 )
            level = 1 / 2 / 4
            count = uint
            tag = tstr .size (1..18446744073709551615)
            small = tstr .size (0..9)

            """.ReplaceLineEndings("\n");
        Assert.Equal(expected, artifacts.Cddl);
    }

    [Fact]
    public void Determinism_sweep_repeated_runs_are_byte_identical()
    {
        // T034 (FR-005, research R11): lowering, unlowerable-error ordering, and
        // schema-validation error ordering are all pure functions of their inputs — repeated
        // runs (fresh parses each time) must produce identical outputs.
        string LowerText(string text) => LowerOk(Doc(text)).Cddl;
        var chatBaseline = LowerText(SchemaValidatorTests.ChatSchema);
        var kitchenBaseline = LowerText(KitchenSchema);

        const string defective = """
            schema bad version 1
            type A: int { min 5 }
            type B: str { enum(ok, "not ok") }
            type Loop { sequence { x: Loop } }
            message bad_kind { sequence { a: A  b: B } }
            """;
        var errorBaseline = string.Join("|", SchemaValidator.Validate(defective).Errors);

        const string clashing = """
            schema clash version 1
            type UserName: str { minLength 1 maxLength 4 }
            type Username: str { minLength 2 maxLength 8 }
            message clash_kind { sequence { a: UserName  b: Username } }
            """;
        var clashBaseline = string.Join("|", Lowering.Lower(Doc(clashing)).Error!.Constructs);

        for (var run = 0; run < 50; run++)
        {
            Assert.Equal(chatBaseline, LowerText(SchemaValidatorTests.ChatSchema));
            Assert.Equal(kitchenBaseline, LowerText(KitchenSchema));
            Assert.Equal(errorBaseline, string.Join("|", SchemaValidator.Validate(defective).Errors));
            Assert.Equal(clashBaseline, string.Join("|", Lowering.Lower(Doc(clashing)).Error!.Constructs));
        }
    }

    // ------------------------------------------------------------------
    // Payload-type allocation (research R3)
    // ------------------------------------------------------------------

    [Fact]
    public void First_free_byte_is_0x13()
    {
        var artifacts = LowerOk(Doc(SchemaValidatorTests.ChatSchema));
        var registration = Assert.Single(artifacts.Registrations);
        Assert.Equal("chat_message", registration.Functor);
        Assert.Equal(0x13, registration.PayloadType);
    }

    [Fact]
    public void Allocation_follows_message_declaration_order()
    {
        var artifacts = LowerOk(Doc("""
            schema multi version 1
            message first_kind { sequence { a: int } }
            message second_kind { sequence { b: str } }
            """));
        Assert.Equal(new (string, byte)[] { ("first_kind", 0x13), ("second_kind", 0x14) },
            artifacts.Registrations.Select(r => (r.Functor, r.PayloadType)));
    }

    [Fact]
    public void Allocation_skips_bytes_taken_in_the_overlay()
    {
        var registry = new SchemaLangRegistry();
        var chat = Doc(SchemaValidatorTests.ChatSchema);
        var register = registry.Register(chat, LowerOk(chat), CompatMode.Full);
        Assert.Null(register.Error);

        var next = Lowering.Lower(Doc("""
            schema other version 1
            message other_kind { sequence { a: int } }
            """), registry);
        Assert.Null(next.Error);
        Assert.Equal(0x14, next.Artifacts!.Registrations[0].PayloadType);
    }

    [Fact]
    public void Version_document_reuses_registered_bytes_and_new_kinds_get_the_lowest_free_byte()
    {
        // Allocation reuse law (lowering.md §Functor registrations): a functor already in
        // seed ∪ overlay REUSES its registered byte, so a v2 declaring the existing kind
        // BEFORE a new kind gives the new kind the LOWEST free byte, not the second-lowest.
        var registry = new SchemaLangRegistry();
        var v1 = Doc("""
            schema evo version 1
            message evo_kind { sequence { id: int } }
            """);
        Assert.Null(registry.Register(v1, Lowering.Lower(v1, registry).Artifacts!, CompatMode.Backward).Error);
        Assert.Equal(0x13, registry.LookupByFunctor("evo_kind").PayloadType);

        var v2 = Doc("""
            schema evo version 2
            message evo_kind { sequence { id: int  extra?: int } }
            message side_kind { sequence { note: str } }
            """);
        var artifacts = Lowering.Lower(v2, registry).Artifacts!;
        Assert.Equal(new (string, byte)[] { ("evo_kind", 0x13), ("side_kind", 0x14) },
            artifacts.Registrations.Select(r => (r.Functor, r.PayloadType)));
    }

    // ------------------------------------------------------------------
    // Unlowerable constructs — the error lists EVERY offending construct
    // ------------------------------------------------------------------

    [Fact]
    public void Unlowerable_error_lists_every_offending_construct()
    {
        var result = Lowering.Lower(Doc("""
            schema bad version 1
            type A: int { min 5 }
            type B: str { enum(ok, "not ok") }
            message bad_kind { sequence { a: A  b: B } }
            """));
        Assert.Null(result.Artifacts);
        Assert.NotNull(result.Error);
        Assert.Equal(LoweringErrorKind.Unlowerable, result.Error!.Kind);
        Assert.Equal(2, result.Error.Constructs.Count);
        Assert.Contains(result.Error.Constructs, c => c.Contains("A") && c.Contains("min"));
        Assert.Contains(result.Error.Constructs, c => c.Contains("not ok"));
    }

    [Fact]
    public void Lowered_rule_name_collision_is_unlowerable()
    {
        // 'UserName' and 'Username' both lower to rule name 'username' (lowering.md: rule
        // name lowercased, _→-) — broken CDDL, so the document is unlowerable, loudly.
        var result = Lowering.Lower(Doc("""
            schema clash version 1
            type UserName: str { minLength 1 maxLength 4 }
            type Username: str { minLength 2 maxLength 8 }
            message clash_kind { sequence { a: UserName  b: Username } }
            """));
        Assert.NotNull(result.Error);
        Assert.Equal(LoweringErrorKind.Unlowerable, result.Error!.Kind);
        Assert.Contains(result.Error.Constructs, c => c.Contains("UserName") && c.Contains("Username"));
    }
}
