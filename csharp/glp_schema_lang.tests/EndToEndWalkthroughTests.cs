// T018 (feature 043-xsd-schema-language): the SC-006 scripted walkthrough.
//
// SC-006: a schema author can author and register a new message schema end-to-end WITHOUT
// hand-writing any CDDL or functor registration — the task is completed entirely at the XSD
// level. This test scripts quickstart steps 1–4: author → validate → lower + register →
// inspect. The only text the author writes is the 043 DSL document; every CDDL byte and every
// functor registration below is produced by the layer.

using GlpRuntime.SchemaLang;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.SchemaLang.Tests;

public class EndToEndWalkthroughTests
{
    [Fact]
    public void Author_to_registry_with_zero_handwritten_cddl_or_functor_text()
    {
        // Step 1 — author (the ONLY hand-written artifact in this walkthrough).
        var authored = SchemaValidatorTests.ChatSchema;

        // Step 2 — validate.
        var validated = SchemaValidator.Validate(authored);
        Assert.True(validated.IsValid);
        var doc = validated.Document!;

        // Step 3 — lower + register (CDDL + functor registrations produced, not written).
        var registry = new SchemaLangRegistry(); // seeded from WireRegistry + SchemaRegistry
        var lowered = Lowering.Lower(doc, registry);
        Assert.True(lowered.IsSuccess);
        var registered = registry.Register(doc, lowered.Artifacts!, CompatMode.Full);
        Assert.True(registered.IsSuccess);

        // Step 4 — inspect: the new RegistryRecord holds qmedit-family source, canonical
        // CDDL, XSD source verbatim, and sha256 hashes — side by side with the seeded 041
        // crdt_message entry.
        var record = registry.LookupByFunctor("chat_message");
        Assert.Equal(authored, record.XsdSource);
        Assert.Equal(authored, record.QmeditDsl);
        Assert.False(string.IsNullOrWhiteSpace(record.Cddl));
        Assert.NotNull(record.CddlSha256);
        Assert.NotNull(record.QmeditSha256);
        Assert.Equal(0x13, record.PayloadType);

        var seededNeighbour = registry.LookupByFunctor("crdt_message");
        Assert.True(seededNeighbour.IsSeeded);
        Assert.Contains(registry.All, r => r.Functor == "chat_message");
        Assert.Contains(registry.All, r => r.Functor == "crdt_message");

        // The lowered CDDL was never hand-written here: it is exactly the layer's output.
        Assert.Equal(lowered.Artifacts!.Cddl, record.Cddl);
    }
}
