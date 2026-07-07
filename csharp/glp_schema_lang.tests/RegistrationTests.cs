// T013 (feature 043-xsd-schema-language): registration law tests — written FIRST; T017 makes
// them green.
//
// Contract: specs/043-xsd-schema-language/contracts/lowering.md §Registration laws —
// all-or-nothing, collision over seed ∪ overlay never overwrites (US1 AS-3), stored forms
// {qmedit, cddl, xsd_source verbatim, sha256s} retrievable together (FR-004, research R9),
// CompatMode mandatory at registration (clarification 3).

using System.Security.Cryptography;
using System.Text;
using GlpRuntime.SchemaLang;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang.Tests;

public class RegistrationTests
{
    private static string Sha256Hex(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static (SchemaLangRegistry Registry, SchemaDocument Doc, LoweringArtifactSet Artifacts) RegisterChat()
    {
        var registry = new SchemaLangRegistry();
        var doc = LoweringTests.Doc(SchemaValidatorTests.ChatSchema);
        var artifacts = LoweringTests.LowerOk(doc);
        var result = registry.Register(doc, artifacts, CompatMode.Full);
        Assert.Null(result.Error);
        Assert.NotNull(result.Records);
        return (registry, doc, artifacts);
    }

    // ------------------------------------------------------------------
    // FR-004: all stored forms retrievable together
    // ------------------------------------------------------------------

    [Fact]
    public void Registered_record_stores_all_forms_and_hashes_together()
    {
        var (registry, doc, artifacts) = RegisterChat();
        var record = registry.LookupByFunctor("chat_message");

        Assert.Equal(0x13, record.PayloadType);
        Assert.Equal("chat", record.SchemaName);
        Assert.Equal(1, record.Version);
        Assert.Equal(CompatMode.Full, record.CompatMode);

        // For 043-authored entries the document text IS the qmedit-family authoring form:
        // one verbatim text under both retrieval keys (lowering.md registration law 3).
        Assert.Equal(doc.Source, record.XsdSource);
        Assert.Equal(doc.Source, record.QmeditDsl);
        Assert.Equal(artifacts.Cddl, record.Cddl);
        Assert.Equal(Sha256Hex(artifacts.Cddl), record.CddlSha256);
        Assert.Equal(Sha256Hex(doc.Source), record.QmeditSha256);
        Assert.False(record.IsSeeded);
    }

    [Fact]
    public void Registered_entry_sits_side_by_side_with_seeded_041_entries()
    {
        var (registry, _, _) = RegisterChat();
        Assert.Equal(4, registry.All.Count); // il_program, result_envelope, crdt_message + chat_message
        var seeded = registry.LookupByFunctor("crdt_message");
        Assert.True(seeded.IsSeeded);
        Assert.Equal(0x12, seeded.PayloadType);
        Assert.NotNull(seeded.Cddl);   // 041 dual-DSL forms present
        Assert.NotNull(seeded.QmeditDsl);
        Assert.Null(seeded.XsdSource); // never authored through this layer
    }

    [Fact]
    public void Seeded_byte_only_kinds_have_no_dsl_forms()
    {
        var registry = new SchemaLangRegistry();
        var il = registry.LookupByFunctor("il_program");
        Assert.Null(il.Cddl);
        Assert.Null(il.QmeditDsl);
        Assert.Null(il.CddlSha256);
    }

    // ------------------------------------------------------------------
    // Collision laws (US1 AS-3): explicit error, nothing written, never overwrite
    // ------------------------------------------------------------------

    [Fact]
    public void Functor_collision_with_seeded_entry_registers_nothing()
    {
        var registry = new SchemaLangRegistry();
        var before = registry.All.Count;
        var doc = LoweringTests.Doc("""
            schema clash version 1
            message crdt_message { sequence { a: int } }
            """);
        var result = registry.Register(doc, LoweringTests.LowerOk(doc), CompatMode.Full);

        Assert.Null(result.Records);
        Assert.NotNull(result.Error);
        Assert.Equal(LoweringErrorKind.Collision, result.Error!.Kind);
        Assert.Contains(result.Error.Constructs, c => c.Contains("crdt_message"));
        Assert.Equal(before, registry.All.Count);              // nothing written
        Assert.True(registry.LookupByFunctor("crdt_message").IsSeeded); // never overwritten
    }

    [Fact]
    public void Functor_collision_with_overlay_entry_registers_nothing()
    {
        var (registry, doc, artifacts) = RegisterChat();
        var before = registry.All.Count;
        var result = registry.Register(doc, artifacts, CompatMode.Full);
        Assert.NotNull(result.Error);
        Assert.Equal(LoweringErrorKind.Collision, result.Error!.Kind);
        Assert.Equal(before, registry.All.Count);
    }

    [Fact]
    public void Payload_type_collision_registers_nothing()
    {
        var (registry, _, _) = RegisterChat(); // 0x13 now taken in the overlay
        var doc = LoweringTests.Doc("""
            schema stale version 1
            message stale_kind { sequence { a: int } }
            """);
        // Lowered against a FRESH registry, so it also allocates 0x13 — stale by the time
        // it reaches the populated registry: explicit collision, nothing written.
        var stale = LoweringTests.LowerOk(doc);
        Assert.Equal(0x13, stale.Registrations[0].PayloadType);
        var before = registry.All.Count;
        var result = registry.Register(doc, stale, CompatMode.Full);
        Assert.NotNull(result.Error);
        Assert.Equal(LoweringErrorKind.Collision, result.Error!.Kind);
        Assert.Contains(result.Error.Constructs, c => c.Contains("0x13"));
        Assert.Equal(before, registry.All.Count);
    }

    [Fact]
    public void Multi_message_document_with_one_colliding_kind_registers_nothing()
    {
        var registry = new SchemaLangRegistry();
        var before = registry.All.Count;
        var doc = LoweringTests.Doc("""
            schema partial version 1
            message brand_new_kind { sequence { a: int } }
            message crdt_message { sequence { b: str } }
            """);
        var result = registry.Register(doc, LoweringTests.LowerOk(doc), CompatMode.Full);

        Assert.NotNull(result.Error);
        Assert.Equal(before, registry.All.Count);
        Assert.False(registry.HasFunctor("brand_new_kind")); // all-or-nothing
    }

    // ------------------------------------------------------------------
    // Duplicates WITHIN one artifact set: same collision refusal, nothing written
    // ------------------------------------------------------------------

    [Fact]
    public void Duplicate_functor_within_one_artifact_set_registers_nothing()
    {
        var registry = new SchemaLangRegistry();
        var before = registry.All.Count;
        var doc = LoweringTests.Doc("""
            schema dup version 1
            message dup_kind { sequence { a: int } }
            """);
        var artifacts = new LoweringArtifactSet("dup-kind = {\n  a: int,\n}\n", new[]
        {
            new FunctorRegistration("dup_kind", 0x13),
            new FunctorRegistration("dup_kind", 0x14),
        });
        var result = registry.Register(doc, artifacts, CompatMode.Full);

        Assert.Null(result.Records);
        Assert.NotNull(result.Error);
        Assert.Equal(LoweringErrorKind.Collision, result.Error!.Kind);
        Assert.Contains(result.Error.Constructs, c => c.Contains("dup_kind") && c.Contains("more than once"));
        Assert.Equal(before, registry.All.Count);
        Assert.False(registry.HasFunctor("dup_kind")); // neither row registered
    }

    [Fact]
    public void Duplicate_payload_type_within_one_artifact_set_registers_nothing()
    {
        var registry = new SchemaLangRegistry();
        var before = registry.All.Count;
        var doc = LoweringTests.Doc("""
            schema dup version 1
            message kind_a { sequence { a: int } }
            message kind_b { sequence { b: int } }
            """);
        var artifacts = new LoweringArtifactSet(
            "kind-a = {\n  a: int,\n}\nkind-b = {\n  b: int,\n}\n", new[]
        {
            new FunctorRegistration("kind_a", 0x13),
            new FunctorRegistration("kind_b", 0x13),
        });
        var result = registry.Register(doc, artifacts, CompatMode.Full);

        Assert.Null(result.Records);
        Assert.NotNull(result.Error);
        Assert.Equal(LoweringErrorKind.Collision, result.Error!.Kind);
        Assert.Contains(result.Error.Constructs, c => c.Contains("0x13") && c.Contains("more than once"));
        Assert.Equal(before, registry.All.Count);
        Assert.False(registry.HasFunctor("kind_a"));
        Assert.False(registry.HasFunctor("kind_b"));
    }

    // ------------------------------------------------------------------
    // Mandatory CompatMode (clarification 3)
    // ------------------------------------------------------------------

    [Fact]
    public void Declared_mode_is_stamped_on_every_registration()
    {
        var registry = new SchemaLangRegistry();
        var doc = LoweringTests.Doc("""
            schema pair version 1
            message kind_one { sequence { a: int } }
            message kind_two { sequence { b: str } }
            """);
        var result = registry.Register(doc, LoweringTests.LowerOk(doc), CompatMode.Backward);
        Assert.Null(result.Error);
        Assert.All(result.Records!, r => Assert.Equal(CompatMode.Backward, r.CompatMode));
    }
}
