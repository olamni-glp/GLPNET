// Feature 058 T035 (contract s4-policy-service.md §A/§B) — the network-callable S4 mint/attenuate
// endpoint at the pre-cut seam, over a REAL in-repo transport pair: client and service are separate
// endpoint objects on the InMemoryLinkFabric, the genuine in-process implementation of the same
// ILinkTransport seam the QUIC adapter drops into (route/LinkTransport.cs). The macaroon-v2 chain is
// re-verified by an INDEPENDENT HMAC recomputation in the tests (never via the production Verify
// alone), and tampered/forged tokens are refused fail-closed.

using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

using GlpRuntime.CrdtMsg.Cap;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Policy;
using GlpRuntime.CrdtMsg.Route;

namespace GlpRuntime.CrdtMsg.Tests;

public class PolicyServiceTests
{
    /// <summary>A fixed, distinctive root key (32 bytes — HMAC-SHA256 strength) so the
    /// never-on-the-wire scan below cannot false-negative on a short pattern.</summary>
    private static readonly byte[] RootKey = Enumerable.Range(0, 32).Select(i => (byte)(0xA0 + i)).ToArray();

    private static readonly string[] MintCaveats = { "op=get", "keyspace=mesh" };
    private const string AttenuationCaveat = "time<9999999999";
    private const string TokenIdentifier = "s4-consumer";
    private const string TokenLocation = "glpnet-policy";

    /// <summary>Recording decorator over a fabric endpoint: captures every outbound frame so a test
    /// can assert what actually crossed the wire (e.g. that the root key never did).</summary>
    private sealed class RecordingTransport : ILinkTransport
    {
        private readonly ILinkTransport _inner;
        public List<byte[]> Sent { get; } = new();

        public RecordingTransport(ILinkTransport inner) => _inner = inner;

        public string LocalPeer => _inner.LocalPeer;
        public IReadOnlyCollection<string> Members => _inner.Members;
        public ChannelReader<LinkInbound> Inbound => _inner.Inbound;

        public ValueTask SendAsync(string toPeer, ReadOnlyMemory<byte> frameBytes, CancellationToken ct = default)
        {
            Sent.Add(frameBytes.ToArray());
            return _inner.SendAsync(toPeer, frameBytes, ct);
        }
    }

    private static (PolicyService service, PolicyClient client, RecordingTransport serviceWire, RecordingTransport clientWire)
        Pair(byte[]? rootKey = null)
    {
        var fabric = new InMemoryLinkFabric();
        var serviceWire = new RecordingTransport(fabric.Connect("policy"));
        var clientWire = new RecordingTransport(fabric.Connect("consumer"));
        var service = new PolicyService(serviceWire, rootKey ?? RootKey);
        var client = new PolicyClient(clientWire, "policy");
        return (service, client, serviceWire, clientWire);
    }

    /// <summary>The INDEPENDENT verification oracle: recompute the 056 C2 chain from first
    /// principles — sig0 = HMAC(root, identifier_utf8), then sig = HMAC(sig_prev, caveat_utf8) —
    /// without touching MacaroonV2.VerifySignature.</summary>
    private static byte[] RecomputeChain(byte[] rootKey, string identifier, IEnumerable<string> caveats)
    {
        byte[] sig = HMACSHA256.HashData(rootKey, Encoding.UTF8.GetBytes(identifier));
        foreach (var caveat in caveats)
            sig = HMACSHA256.HashData(sig, Encoding.UTF8.GetBytes(caveat));
        return sig;
    }

    [Fact] // AS-1 shape: mint via the endpoint, attenuate the result, verify by independent recompute.
    public async Task MintThenAttenuate_OverTransportPair_VerifiesByIndependentRecompute()
    {
        var (service, client, _, _) = Pair();
        var serving = service.ServeAsync(2);

        byte[] minted = await client.MintAsync(TokenIdentifier, TokenLocation, MintCaveats);
        byte[] attenuated = await client.AttenuateAsync(minted, AttenuationCaveat);
        await serving;

        MacaroonV2 token = MacaroonV2Codec.Decode(attenuated);
        Assert.Equal(TokenIdentifier, token.Identifier);
        Assert.Equal(TokenLocation, token.Location);
        Assert.Equal(new[] { "op=get", "keyspace=mesh", AttenuationCaveat }, token.Caveats);

        // Independent HMAC-chain recomputation — the tail must match exactly.
        byte[] expected = RecomputeChain(RootKey, TokenIdentifier, token.Caveats);
        Assert.Equal(expected, token.Signature);
        Assert.True(token.VerifySignature(RootKey));

        // And the minted (pre-attenuation) token also chains correctly with one caveat fewer.
        MacaroonV2 mintedToken = MacaroonV2Codec.Decode(minted);
        Assert.Equal(RecomputeChain(RootKey, TokenIdentifier, MintCaveats), mintedToken.Signature);
    }

    [Fact] // a tampered token (altered caveat, carried signature) fails verify AND the endpoint refuses to attenuate it.
    public async Task TamperedToken_FailsVerify_AndEndpointRefusesAttenuation()
    {
        var (service, client, _, _) = Pair();
        var serving = service.ServeAsync(2);

        byte[] minted = await client.MintAsync(TokenIdentifier, TokenLocation, MintCaveats);
        MacaroonV2 token = MacaroonV2Codec.Decode(minted);

        // Tamper: privilege-escalate "op=get" → "op=put" while keeping the wire's signature claim.
        var escalated = MacaroonV2.FromWire(
            token.Location, token.Identifier, new[] { "op=put", "keyspace=mesh" }, token.Signature);
        Assert.False(escalated.VerifySignature(RootKey));

        var refusal = await Assert.ThrowsAsync<PolicyRefusedException>(
            () => client.AttenuateAsync(MacaroonV2Codec.Encode(escalated), AttenuationCaveat));
        Assert.Contains("fail closed", refusal.Message);
        await serving;
    }

    [Fact] // a forged token (minted under a different root key) is refused fail-closed.
    public async Task ForgedToken_WrongRootKey_Refused()
    {
        var (service, client, _, _) = Pair();
        var serving = service.ServeAsync(1);

        byte[] wrongKey = Enumerable.Repeat((byte)0x55, 32).ToArray();
        MacaroonV2 forged = MacaroonV2.Mint(wrongKey, TokenLocation, TokenIdentifier, MintCaveats);
        Assert.False(forged.VerifySignature(RootKey));

        await Assert.ThrowsAsync<PolicyRefusedException>(
            () => client.AttenuateAsync(MacaroonV2Codec.Encode(forged), AttenuationCaveat));
        await serving;
    }

    [Fact] // an out-of-vocabulary caveat is refused at mint AND at attenuate (C3 closed vocabulary).
    public async Task OutOfVocabularyCaveat_RefusedFailClosed()
    {
        var (service, client, _, _) = Pair();
        var serving = service.ServeAsync(3);

        var mintRefusal = await Assert.ThrowsAsync<PolicyRefusedException>(
            () => client.MintAsync(TokenIdentifier, TokenLocation, new[] { "admin=true" }));
        Assert.Contains("closed vocabulary", mintRefusal.Message);

        byte[] minted = await client.MintAsync(TokenIdentifier, TokenLocation, MintCaveats);
        await Assert.ThrowsAsync<PolicyRefusedException>(
            () => client.AttenuateAsync(minted, "op=frobnicate"));   // op outside {get,put,del,subscribe}
        await serving;
    }

    [Fact] // the root key never crosses the wire in either direction — callers receive tokens, not keys.
    public async Task RootKey_NeverOnTheWire()
    {
        var (service, client, serviceWire, clientWire) = Pair();
        var serving = service.ServeAsync(2);

        byte[] minted = await client.MintAsync(TokenIdentifier, TokenLocation, MintCaveats);
        await client.AttenuateAsync(minted, AttenuationCaveat);
        await serving;

        var allFrames = serviceWire.Sent.Concat(clientWire.Sent).ToList();
        Assert.Equal(4, allFrames.Count);   // 2 requests + 2 replies — the whole conversation
        foreach (var frame in allFrames)
            Assert.False(ContainsSubsequence(frame, RootKey), "root key bytes leaked onto the wire");
    }

    [Fact] // wire codec discipline: round-trip fidelity + consume-all-or-throw on trailing bytes.
    public void MacaroonV2Codec_RoundTrips_And_TrailingBytes_LoudFail()
    {
        MacaroonV2 token = MacaroonV2.Mint(RootKey, TokenLocation, TokenIdentifier, MintCaveats)
            .Attenuate(AttenuationCaveat);
        byte[] wire = MacaroonV2Codec.Encode(token);

        MacaroonV2 decoded = MacaroonV2Codec.Decode(wire);
        Assert.Equal(token.Identifier, decoded.Identifier);
        Assert.Equal(token.Location, decoded.Location);
        Assert.Equal(token.Caveats, decoded.Caveats);
        Assert.Equal(token.Signature, decoded.Signature);
        Assert.True(decoded.VerifySignature(RootKey));

        byte[] trailing = wire.Concat(new byte[] { 0x00 }).ToArray();
        Assert.Throws<CrdtMsgException>(() => MacaroonV2Codec.Decode(trailing));

        byte[] truncated = wire.Take(wire.Length - 1).ToArray();
        Assert.Throws<CrdtMsgException>(() => MacaroonV2Codec.Decode(truncated));
    }

    [Fact] // the legacy in-process static-root-key mint stays available INTERNALLY beside the endpoint.
    public void InProcessMint_StaysAvailable_BesideTheEndpoint()
    {
        // The 058 macaroon-v2 in-process path…
        MacaroonV2 v2 = MacaroonV2.Mint(RootKey, TokenLocation, TokenIdentifier, MintCaveats);
        Assert.True(v2.VerifySignature(RootKey));

        // …and the legacy 050 static-macaroon path (the {action,peer,expires} dialect the QUIC gate
        // uses) both still mint in-process — cutover semantics are a later task.
        Macaroon legacy = Macaroon.Create(RootKey, "glpquick", "glpnet-mesh");
        Assert.NotNull(legacy);
        Assert.Empty(legacy.Caveats);
    }

    private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                return true;
        }
        return false;
    }
}
