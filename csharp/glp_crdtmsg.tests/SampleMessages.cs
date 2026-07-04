// Golden sample corpus for the US1 conformance matrix + loud-fail tests (feature 041-crdtmsg-mvp).
// Authored from ONE truth (the abstract model); each surface must round-trip these identically.

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.CrdtMsg.Tests;

internal static class SampleMessages
{
    /// <summary>Section types the test receiver "understands" (all ignorable/even here so the
    /// must-understand guard never fires in the conformance path).</summary>
    public static readonly IReadOnlySet<long> Understood = new HashSet<long> { 0x12, 0x40, TlvSection.GreaseTypeNumber };

    public static IEnumerable<(string name, Message msg)> All()
    {
        yield return ("minimal", new Message(
            SchemaVersion: VersionPolicy.EmitSchemaVersion,
            PayloadType: PayloadType.CrdtMessage,
            Header: new Header("m-0", "alice", "bob", 0, RoutingPolicy.Empty),
            Sections: Array.Empty<Section>(),
            CrdtModel: CrdtModel.None));

        yield return ("rich", new Message(
            SchemaVersion: VersionPolicy.EmitSchemaVersion,
            PayloadType: PayloadType.CrdtMessage,
            Header: new Header(
                MsgId: "m-42",
                From: "alice",
                To: "@editors",
                Seq: 7,
                Policy: new RoutingPolicy(
                    Targets: new[] { "bob", "carol" },
                    Waypoints: new[] { "relay1" },
                    Excludes: new[] { "mallory" })),
            Sections: new[]
            {
                new Section(0x12, new byte[] { 0x00, 0x01, 0x02, 0xFF, 0x80 }), // known-ish payload
                new Section(0x40, AllBytes()),                                   // unknown-ignorable, full byte range
                TlvSection.Grease(),                                             // greasing (FR-006 skip path)
            },
            CrdtModel: CrdtModel.OpBased));

        // v2 (upper accept-range) with cap slot ABSENT — exercises TIER-1 accept-range tolerance.
        yield return ("v2-no-cap", new Message(
            SchemaVersion: VersionPolicy.MaxAcceptSchemaVersion,
            PayloadType: PayloadType.CrdtMessage,
            Header: new Header("m-2", "él", "bøb", 3, RoutingPolicy.Empty), // non-ASCII UTF-8
            Sections: new[] { TlvSection.Grease() },
            CrdtModel: CrdtModel.StateBased));
    }

    private static byte[] AllBytes()
    {
        var b = new byte[256];
        for (int i = 0; i < 256; i++) b[i] = (byte)i;
        return b;
    }
}
