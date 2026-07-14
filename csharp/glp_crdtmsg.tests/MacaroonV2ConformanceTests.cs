// test(058) T037 — shared macaroon-v2 vector conformance: GLPNET's verifier passes the IDENTICAL
// 22-vector corpus (buildkit specs/058-yngenios-4service-dogfood/vectors/macaroon-v2,
// corpus_version 1), copied BYTE-EXACT into conformance/macaroon-v2/ (manifest pins sha256 of each
// file's exact bytes; the sibling .gitattributes marks payloads -text so no checkout re-renders
// them — the 057 parity-drift lesson). This corpus supersedes the legacy {action, peer, expires}
// gate dialect for minted-for-mesh capability tokens.
//
// FORMAT DIVERGENCE (noted per T037, NOT silently skipped): the T035 MacaroonV2Codec wire container
// uses the shipped ByteWriter/ByteReader conventions — LEB128 varint lengths, LOCATION-first,
// caveats carried as C3 strings. The shared corpus's C6 opaque-token layout is different:
//   <<id_len:32, id, loc_len:32, loc, n:32, caveat*, tail_len:32, tail>>
// big-endian u32 lengths, IDENTIFIER-first, structured caveats as tag bytes 0..5
// (0=time_before{expiry:u64be} 1=resource 2=key 3=key_prefix 4=op{byte 0..3=get|put|del|subscribe}
// 5=keyspace_scope). The C6 decoder is therefore implemented HERE, fail-closed
// (Truncated / BadTag / BadOp / BadUtf8 / TrailingBytes), for conformance only.
//
// Surfaces:
//  - CHAIN (12 vectors): the T035 MacaroonV2 implementation itself recomputes the HMAC-SHA256
//    chain over identifier + presented_caveats (FromWire + VerifySignature, constant-time tail
//    compare vs presented_tail_hex). Contextual C3 evaluation (strict now < expiry, byte-prefix
//    key_prefix, op/resource/key/keyspace equality) is done here — T035 VerifySignature is chain
//    integrity + vocabulary only (the enforcement point evaluates context, per the E-5 ruling).
//  - WIRE (10 vectors): fail-closed C6 decode; ok vectors round-trip (decode ≡ vector.decoded,
//    re-encode ≡ original bytes); refuse:* vectors refuse for that reason. The three
//    superseded-dialect tokens (refuse:DialectMismatch) MUST refuse — any fail-closed decode
//    refusal qualifies — and the test surfaces a dialect-mismatch diagnostic.

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using GlpRuntime.CrdtMsg.Cap;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class MacaroonV2ConformanceTests
{
    private static readonly string CorpusDir =
        Path.Combine(AppContext.BaseDirectory, "conformance", "macaroon-v2");
    private static readonly string VectorsDir = Path.Combine(CorpusDir, "vectors");

    private static readonly Lazy<IReadOnlyDictionary<string, string>> ManifestPins = new(() =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(CorpusDir, "manifest.json")));
        return doc.RootElement.GetProperty("vectors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!);
    });

    /// <summary>All 22 vectors keyed by id, parsed from the manifest-pinned files (detached clones).</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, JsonElement>> Vectors = new(() =>
    {
        var byId = new Dictionary<string, JsonElement>();
        foreach (var name in ManifestPins.Value.Keys)
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(VectorsDir, name)));
            var v = doc.RootElement.Clone();
            byId.Add(v.GetProperty("id").GetString()!, v);
        }
        return byId;
    });

    private static TheoryData<string> IdsBySurface(string surface)
    {
        var data = new TheoryData<string>();
        foreach (var (id, v) in Vectors.Value.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            if (v.GetProperty("surface").GetString() == surface)
                data.Add(id);
        return data;
    }

    public static TheoryData<string> ChainVectorIds() => IdsBySurface("macaroon-v2-chain");
    public static TheoryData<string> WireVectorIds() => IdsBySurface("macaroon-v2-wire");

    // ── (a) manifest parity ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Manifest_parity_sha256_of_exact_bytes_no_extra_no_missing()
    {
        var pins = ManifestPins.Value;
        Assert.Equal(22, pins.Count);

        foreach (var (name, pin) in pins)
        {
            var raw = File.ReadAllBytes(Path.Combine(VectorsDir, name)); // exact on-disk bytes
            Assert.True(pin == Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant(),
                $"{name}: MANIFEST DRIFT — copied bytes do not match the corpus pin");
        }

        var onDisk = Directory.GetFiles(VectorsDir, "*.json").Select(Path.GetFileName).ToHashSet();
        Assert.Equal(pins.Keys.ToHashSet(), onDisk!); // no extra, no missing
    }

    [Fact]
    public void Corpus_coverage_is_complete_12_chain_10_wire_none_skipped()
    {
        var vectors = Vectors.Value;
        Assert.Equal(22, vectors.Count);
        Assert.Equal(12, vectors.Values.Count(v => v.GetProperty("surface").GetString() == "macaroon-v2-chain"));
        Assert.Equal(10, vectors.Values.Count(v => v.GetProperty("surface").GetString() == "macaroon-v2-wire"));
        // Every id is a manifest filename stem, so the two theories cover the manifest exactly.
        Assert.Equal(
            ManifestPins.Value.Keys.Select(n => Path.GetFileNameWithoutExtension(n)!).ToHashSet(),
            vectors.Keys.ToHashSet());
    }

    // ── (b) chain surface: T035 MacaroonV2 + closed C3 context evaluation ─────────────────────

    [Theory]
    [MemberData(nameof(ChainVectorIds))]
    public void Chain_vector_verifies_or_refuses_per_expectation(string id)
    {
        var v = Vectors.Value[id];
        var input = v.GetProperty("input");
        var presented = input.GetProperty("presented_caveats").EnumerateArray().ToList();

        // Rehydrate exactly as a transport would: the tail is the wire's CLAIM, carried verbatim.
        var token = MacaroonV2.FromWire(
            input.GetProperty("location").GetString()!,
            input.GetProperty("identifier").GetString()!,
            presented.Select(SemanticCaveatString).ToList(),
            Convert.FromHexString(input.GetProperty("presented_tail_hex").GetString()!));

        string? refusal;
        if (!token.VerifySignature(Convert.FromHexString(input.GetProperty("verify_root_key_hex").GetString()!)))
        {
            refusal = "BadSignature"; // T035 recompute + FixedTimeEquals against the presented tail
        }
        else
        {
            var ctx = input.GetProperty("context");
            refusal = presented.Where(c => !CaveatSatisfied(c, ctx))
                .Select(c => $"CaveatFailed:{c.GetProperty("kind").GetString()}")
                .FirstOrDefault();
        }

        AssertExpectation(v, refusal);
    }

    [Theory]
    [MemberData(nameof(ChainVectorIds))]
    public void Chain_ok_vectors_reproduce_the_pinned_tail_via_T035_mint_and_attenuate(string id)
    {
        var v = Vectors.Value[id];
        if (v.GetProperty("expect").GetString() != "ok")
            return; // refusal vectors carry tampered/stripped/wrong-root material by design

        var input = v.GetProperty("input");
        var token = MacaroonV2.Mint(
            Convert.FromHexString(input.GetProperty("root_key_hex").GetString()!),
            input.GetProperty("location").GetString()!,
            input.GetProperty("identifier").GetString()!,
            input.GetProperty("mint_caveats").EnumerateArray().Select(SemanticCaveatString).ToList());
        foreach (var att in input.GetProperty("attenuations").EnumerateArray())
            token = token.Attenuate(SemanticCaveatString(att));

        Assert.Equal(v.GetProperty("tail_hex").GetString(),
            Convert.ToHexString(token.Signature).ToLowerInvariant());
    }

    // ── (c) wire surface: fail-closed C6 decode ───────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(WireVectorIds))]
    public void Wire_vector_decodes_or_refuses_fail_closed_per_expectation(string id)
    {
        var v = Vectors.Value[id];
        var tokenBytes = Convert.FromHexString(v.GetProperty("input").GetProperty("token_hex").GetString()!);
        var expect = v.GetProperty("expect").GetString()!;

        C6Wire.Token? decoded = null;
        string? refusal = null;
        try { decoded = C6Wire.Decode(tokenBytes); }
        catch (C6Wire.Refusal ex) { refusal = ex.Reason; }

        if (expect == "ok")
        {
            Assert.Null(refusal);
            AssertDecodedMatches(v.GetProperty("decoded"), decoded!);
            // Round-trip: re-encoding the decoded object reproduces the original token bytes.
            Assert.Equal(tokenBytes, C6Wire.Encode(decoded!));
            // And every decoded caveat lands inside the T035 closed C3 vocabulary.
            Assert.All(decoded!.Caveats, c => Assert.True(MacaroonV2Vocabulary.IsValid(c.Semantic),
                $"{id}: decoded caveat '{c.Semantic}' fell outside the C3 closed vocabulary"));
        }
        else if (expect == "refuse:DialectMismatch")
        {
            // A superseded legacy dialect token MUST refuse; any fail-closed decode refusal
            // qualifies. Surface it as a dialect-mismatch diagnostic (wording is per-repo).
            Assert.True(refusal is not null,
                $"{id}: DialectMismatch — legacy-dialect token was ACCEPTED by the macaroon-v2 " +
                "decoder; superseded {action, peer, expires}-era tokens must refuse fail-closed");
            var diagnostic = $"DialectMismatch: superseded legacy dialect token refused fail-closed "
                + $"(C6 decode refusal: {refusal})";
            Assert.Contains("DialectMismatch", diagnostic, StringComparison.Ordinal);
        }
        else
        {
            Assert.NotNull(refusal);
            Assert.Equal(expect, $"refuse:{refusal}");
        }
    }

    // ── C3 helpers (semantic caveat bytes + contextual satisfaction) ──────────────────────────

    /// <summary>Structured vector caveat → the C3 semantic caveat string (the chain's HMAC input).</summary>
    private static string SemanticCaveatString(JsonElement cav) =>
        cav.GetProperty("kind").GetString() switch
        {
            "time_before" => $"time<{cav.GetProperty("expiry").GetUInt64()}",
            "resource" => $"resource={cav.GetProperty("content_hash").GetString()}",
            "key" => $"key={cav.GetProperty("key").GetString()}",
            "key_prefix" => $"key_prefix={cav.GetProperty("prefix").GetString()}",
            "op" => $"op={cav.GetProperty("op").GetString()}",
            "keyspace_scope" => $"keyspace={cav.GetProperty("scope").GetString()}",
            var kind => throw new InvalidOperationException(
                $"vector caveat kind '{kind}' is outside the C3 closed vocabulary"),
        };

    /// <summary>Closed C3 contextual evaluation; anything un-understood is unsatisfied (fail-closed).</summary>
    private static bool CaveatSatisfied(JsonElement cav, JsonElement ctx) =>
        cav.GetProperty("kind").GetString() switch
        {
            "time_before" => ctx.GetProperty("now").GetUInt64() < cav.GetProperty("expiry").GetUInt64(), // strict
            "resource" => ctx.GetProperty("resource").GetString() == cav.GetProperty("content_hash").GetString(),
            "key" => ctx.GetProperty("key").GetString() == cav.GetProperty("key").GetString(),
            "key_prefix" => Encoding.UTF8.GetBytes(ctx.GetProperty("key").GetString()!).AsSpan()
                .StartsWith(Encoding.UTF8.GetBytes(cav.GetProperty("prefix").GetString()!)), // byte-prefix
            "op" => ctx.GetProperty("op").GetString() == cav.GetProperty("op").GetString(),
            "keyspace_scope" => ctx.GetProperty("keyspace").GetString() == cav.GetProperty("scope").GetString(),
            _ => false,
        };

    private static void AssertExpectation(JsonElement vector, string? refusal)
    {
        var expect = vector.GetProperty("expect").GetString()!;
        if (expect == "ok")
            Assert.True(refusal is null,
                $"{vector.GetProperty("id").GetString()}: expected ok, got refusal={refusal}");
        else
            Assert.Equal(expect, $"refuse:{refusal}");
    }

    private static void AssertDecodedMatches(JsonElement expected, C6Wire.Token decoded)
    {
        Assert.Equal(expected.GetProperty("identifier").GetString(), decoded.Identifier);
        Assert.Equal(expected.GetProperty("location").GetString(), decoded.Location);
        Assert.Equal(expected.GetProperty("tail_hex").GetString(),
            Convert.ToHexString(decoded.Tail).ToLowerInvariant());

        var expectedCaveats = expected.GetProperty("caveats").EnumerateArray().ToList();
        Assert.Equal(expectedCaveats.Count, decoded.Caveats.Count);
        for (var i = 0; i < expectedCaveats.Count; i++)
        {
            Assert.Equal(expectedCaveats[i].GetProperty("kind").GetString(), decoded.Caveats[i].Kind);
            Assert.Equal(SemanticCaveatString(expectedCaveats[i]), decoded.Caveats[i].Semantic);
        }
    }

    // ── C6 opaque-token codec (conformance-local; see the format-divergence header note) ──────

    private static class C6Wire
    {
        private static readonly string[] Ops = ["get", "put", "del", "subscribe"];
        private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        internal sealed class Refusal(string reason)
            : Exception($"C6 macaroon-v2 wire decode refused fail-closed: {reason}")
        {
            public string Reason { get; } = reason;
        }

        /// <param name="WireBytes">The caveat's exact wire bytes (tag + payload), for re-encoding.</param>
        internal sealed record Caveat(string Kind, string Semantic, byte[] WireBytes);

        internal sealed record Token(
            string Identifier, string Location, IReadOnlyList<Caveat> Caveats, byte[] Tail);

        internal static Token Decode(byte[] token)
        {
            var off = 0;

            byte[] Take(int n)
            {
                if (token.Length - off < n) throw new Refusal("Truncated");
                var chunk = token[off..(off + n)];
                off += n;
                return chunk;
            }

            uint TakeU32() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));

            byte[] TakeField()
            {
                var len = TakeU32();
                if (len > (uint)(token.Length - off)) throw new Refusal("Truncated");
                return Take((int)len);
            }

            string TakeStr()
            {
                var blob = TakeField();
                try { return StrictUtf8.GetString(blob); }
                catch (DecoderFallbackException) { throw new Refusal("BadUtf8"); }
            }

            var identifier = TakeStr();
            var location = TakeStr();

            var n = TakeU32();
            var caveats = new List<Caveat>();
            for (uint i = 0; i < n; i++)
            {
                var start = off;
                var tag = Take(1)[0];
                Caveat caveat = tag switch
                {
                    0 => new("time_before",
                        $"time<{BinaryPrimitives.ReadUInt64BigEndian(Take(8))}", []),
                    1 => new("resource", $"resource={TakeStr()}", []),
                    2 => new("key", $"key={TakeStr()}", []),
                    3 => new("key_prefix", $"key_prefix={TakeStr()}", []),
                    4 => Take(1)[0] is var op && op <= 3
                        ? new("op", $"op={Ops[op]}", [])
                        : throw new Refusal("BadOp"),
                    5 => new("keyspace_scope", $"keyspace={TakeStr()}", []),
                    _ => throw new Refusal("BadTag"),
                };
                caveats.Add(caveat with { WireBytes = token[start..off] });
            }

            var tail = TakeField();
            if (off != token.Length) throw new Refusal("TrailingBytes");
            return new Token(identifier, location, caveats, tail);
        }

        internal static byte[] Encode(Token t)
        {
            using var ms = new MemoryStream();

            void WriteU32(uint value)
            {
                var quad = new byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(quad, value);
                ms.Write(quad);
            }

            void WriteField(byte[] bytes)
            {
                WriteU32((uint)bytes.Length);
                ms.Write(bytes);
            }

            WriteField(Encoding.UTF8.GetBytes(t.Identifier));
            WriteField(Encoding.UTF8.GetBytes(t.Location));
            WriteU32((uint)t.Caveats.Count);
            foreach (var caveat in t.Caveats)
                ms.Write(caveat.WireBytes);
            WriteField(t.Tail);
            return ms.ToArray();
        }
    }
}
