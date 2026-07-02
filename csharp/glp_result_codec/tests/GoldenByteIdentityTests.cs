// Golden byte-identity — C# reproduces the PINNED corpus.hex (SC-002, T027) and decodes
// the Dart-authored golden bytes back to the corpus envelope (Acceptance #2 cross-decode,
// T028). corpus.hex is authored from the Dart source-of-truth encoder (R9); this test
// READS the file (not inlined hex) so it is a drift guard — if Dart's bytes change, C#
// fails here. Only NON-gated entries are byte-final (gated float / 64-bit-int edges are
// excluded — R11/R6); `captured` is masked (empty) per R4.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.ResultCodec.Tests;

public class GoldenByteIdentityTests
{
    private static byte[] Hex(string s)
    {
        var clean = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
        return bytes;
    }

    // name -> hex, parsed from the pinned contract file (Dart source of truth).
    private static readonly IReadOnlyDictionary<string, string> Golden = LoadGolden();

    private static string GoldenPath()
    {
        // Walk up from the test binary dir until we reach the repo root (holds specs/).
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && dir != null; i++)
        {
            var candidate = Path.Combine(dir,
                "specs", "038-result-codec-and-framecodec-ride",
                "contracts", "golden", "corpus.hex");
            if (File.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException(
            "corpus.hex not found walking up from " + AppContext.BaseDirectory);
    }

    private static IReadOnlyDictionary<string, string> LoadGolden()
    {
        var map = new Dictionary<string, string>();
        foreach (var raw in File.ReadAllLines(GoldenPath()))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            int sp = line.IndexOf(' ');
            map[line.Substring(0, sp)] = line.Substring(sp + 1);
        }
        return map;
    }

    public static IEnumerable<object[]> GoldenNames =>
        Golden.Keys.Select(n => new object[] { n });

    // T027 — the golden name set is exactly the non-gated corpus minus the
    // captured-carrying entry (captured is masked per R4). Guards against silent drift.
    [Fact]
    public void Golden_covers_the_non_gated_corpus_minus_captured()
    {
        var expected = Corpus.NonGated.Keys
            .Where(n => Corpus.ByName(n).Captured.Length == 0)
            .ToHashSet();
        Assert.Equal(expected, Golden.Keys.ToHashSet());
    }

    // T027 — every runtime reproduces the pinned bytes: Encode(corpus[name]) == golden.
    [Theory]
    [MemberData(nameof(GoldenNames))]
    public void Encode_reproduces_pinned_golden(string name)
    {
        var actual = ResultEnvelopeCodec.Encode(Corpus.ByName(name));
        Assert.Equal(Hex(Golden[name]), actual);
    }

    // T028 — cross-decode: the Dart-authored golden bytes decode, in C#, back to the
    // original envelope (the golden is the shared cross-runtime byte source).
    [Theory]
    [MemberData(nameof(GoldenNames))]
    public void Decode_of_golden_equals_corpus_envelope(string name)
    {
        var decoded = ResultEnvelopeCodec.Decode(Hex(Golden[name]));
        Assert.Equal(Corpus.ByName(name), decoded);
    }
}
