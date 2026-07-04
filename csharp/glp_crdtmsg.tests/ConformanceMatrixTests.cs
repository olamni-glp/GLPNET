// SC-001 — the 16-cell multi-format conformance matrix (feature 041-crdtmsg-mvp, T010).
//
// Invariant C1: for every message and surfaces s1,s2 ∈ {binary,json,yaml,cbor},
//   decode(s2, encode(s2, decode(s1, encode(s1, m)))) ≡ m   (semantic equality = identical canonical bytes),
// and same-surface re-encode is byte-identical. Unknown/opaque sections are preserved verbatim across
// every conversion (checked explicitly via the greasing section).

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Model;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class ConformanceMatrixTests
{
    private static IReadOnlyList<ISurfaceCodec> Surfaces => MessageCodec.Surfaces;

    [Fact]
    public void Sixteen_cell_matrix_preserves_semantics()
    {
        int cells = 0;
        foreach (var (name, msg) in SampleMessages.All())
        {
            byte[] canonical = MessageCodec.Canonical(msg);
            foreach (var s1 in Surfaces)
            foreach (var s2 in Surfaces)
            {
                // s1 encode → decode → s2 encode → decode
                Message viaS1 = s1.Decode(s1.Encode(msg));
                Message viaS2 = s2.Decode(s2.Encode(viaS1));
                byte[] round = MessageCodec.Canonical(viaS2);
                Assert.True(canonical.AsSpan().SequenceEqual(round),
                    $"[{name}] {s1.Name}->{s2.Name} broke semantic equality");
                cells++;
            }
        }
        // 4x4 surfaces × 3 sample messages
        Assert.Equal(16 * 3, cells);
    }

    [Fact]
    public void Same_surface_reencode_is_byte_identical()
    {
        foreach (var (name, msg) in SampleMessages.All())
        foreach (var s in Surfaces)
        {
            byte[] once = s.Encode(msg);
            byte[] twice = s.Encode(s.Decode(once));
            Assert.True(once.AsSpan().SequenceEqual(twice),
                $"[{name}] {s.Name} re-encode not byte-identical (non-deterministic surface)");
        }
    }

    [Fact]
    public void Unknown_ignorable_sections_survive_every_transcode()
    {
        var msg = SampleMessages.All().Single(t => t.name == "rich").msg;
        foreach (var s1 in Surfaces)
        foreach (var s2 in Surfaces)
        {
            Message viaS1 = s1.Decode(s1.Encode(msg));
            Message viaS2 = s2.Decode(s2.Encode(viaS1));
            // the greasing section (unknown, ignorable) must be carried verbatim through the chain
            var grease = viaS2.Sections.SingleOrDefault(x => x.TypeNumber == TlvSection.GreaseTypeNumber);
            Assert.NotNull(grease);
            Assert.Equal(TlvSection.Grease().Value, grease!.Value);
            // and the full-byte-range unknown-ignorable section survives intact
            var wide = viaS2.Sections.Single(x => x.TypeNumber == 0x40);
            Assert.Equal(256, wide.Value.Length);
        }
    }

    [Fact]
    public void V2_message_is_accepted_across_all_surfaces()
    {
        var msg = SampleMessages.All().Single(t => t.name == "v2-no-cap").msg;
        foreach (var s in Surfaces)
        {
            Message rt = s.Decode(s.Encode(msg));
            Assert.Equal(VersionPolicy.MaxAcceptSchemaVersion, rt.SchemaVersion);
        }
    }
}
