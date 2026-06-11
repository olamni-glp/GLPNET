using GlpRuntime.Bytecode;
using GlpRuntime.Runtime;

namespace GlpRuntime.IlCodec;

/// <summary>The round-trip of one heap-embedded program: its address/name, the bytes, and the decoded twin.</summary>
public sealed record EmbeddedRoundTrip(int Address, string Name, byte[] Payload, BytecodeProgram Decoded);

/// <summary>
/// PHASE b (FR-009b) — round-trip every <see cref="ModuleTerm"/>-embedded program
/// reachable on the engine heap through <see cref="IlCodec"/>. Reuses the exact same
/// codec as the per-module path (no separate format); it only adds the heap-location
/// step. Does NOT define #7's snapshot envelope (D5).
/// </summary>
public static class IlHeapCodec
{
    public static IReadOnlyList<EmbeddedRoundTrip> RoundTripEmbedded(HeapFCP heap)
    {
        var results = new List<EmbeddedRoundTrip>();
        foreach (var e in HeapWalk.FindEmbeddedPrograms(heap))
        {
            var bytes = IlCodec.Encode(e.Program);
            var decoded = IlCodec.Decode(bytes).Program;
            results.Add(new EmbeddedRoundTrip(e.Address, e.Name, bytes, decoded));
        }
        return results;
    }
}
