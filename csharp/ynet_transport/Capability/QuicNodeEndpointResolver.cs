using System.Net;
using System.Net.Quic;
using System.Net.Sockets;
using Ynet.Transport.Link;

namespace Ynet.Transport.Capability;

/// <summary>
/// The QUIC endpoint resolver the seam always anticipated: <c>Connect</c> over a REAL wire.
/// </summary>
/// <remarks>
/// <see cref="INodeEndpointResolver"/>'s own doc comment says "the QUIC endpoint resolver swaps in
/// behind the same seam (P3/P4) — the capability code above it does not change". This is that swap.
/// It was not writable before feature 102 for one reason: <b>nothing could map an id to an
/// address</b>. <see cref="INodeAddressResolver"/> supplies the missing half, so this class is the
/// composition of the two and holds no resolution policy of its own.
///
/// <para>🔴 <b>Every refusal is passed through, never flattened.</b> A caller that cannot dial needs
/// to know whether the id was unknown, the lease had lapsed, the record failed verification, the
/// address was un-dialable, or the host has no QUIC at all — those have four different remedies and
/// three different owners.</para>
///
/// <para><b>The TLS certificate here is deliberately ephemeral</b> and that is NOT the defect fixed
/// in feature 102. This tier's TLS provides transport confidentiality only; YNET identity is
/// verified app-layer by <see cref="YnetSession"/> against <c>nodeId = H(pubkey)</c> (FR-002). The
/// identity that must persist is the NODE key (<see cref="NodeIdentity.LoadOrMint"/>), and it does.
/// Do not "fix" the listener cert by pinning it — that would move the identity decision to the wrong
/// layer.</para>
/// </remarks>
public sealed class QuicNodeEndpointResolver : INodeEndpointResolver
{
    private readonly INodeAddressResolver _addresses;
    private readonly TimeSpan _dialTimeout;

    /// <param name="addresses">the id→address half (feature 102)</param>
    /// <param name="dialTimeout">bounds the handshake; default 10s. An unbounded dial turns an
    /// unreachable peer into a hung caller, which reads as a deadlock rather than a refusal.</param>
    public QuicNodeEndpointResolver(INodeAddressResolver addresses, TimeSpan? dialTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        _addresses = addresses;
        _dialTimeout = dialTimeout ?? TimeSpan.FromSeconds(10);
    }

    /// <summary>True iff this host can perform a genuine QUIC handshake in both roles.</summary>
    public static bool IsSupported => QuicWireChannel.IsSupported;

    public Result<IWireChannel> OpenChannel(NodeId peer)
    {
        // Resolve FIRST, and it is free: Resolve does no I/O. An unknown id is the CALLER's error and
        // is true whether or not this host has QUIC, so reporting it beats reporting the host's
        // capability — the nearer cause is the actionable one.
        var address = _addresses.Resolve(peer);
        if (!address.Ok)
            return Result<IWireChannel>.Refuse(address.Reason);

        if (!TryDialableEndpoint(address.Value, out var endpoint))
            return Result<IWireChannel>.Refuse(RefusalReason.FurtherResolverRequired);

        // 050 gate: refuse, never downgrade to a cleartext or emulated transport.
        if (!QuicWireChannel.IsSupported)
            return Result<IWireChannel>.Refuse(RefusalReason.TransportUnsupported);

        using var cts = new CancellationTokenSource(_dialTimeout);
        try
        {
            var channel = QuicWireChannel.ConnectAsync(endpoint!, cts.Token).GetAwaiter().GetResult();
            return Result<IWireChannel>.Success(channel);
        }
        catch (Exception ex) when (ex is OperationCanceledException or QuicException
                                      or SocketException or IOException
                                      or System.Security.Authentication.AuthenticationException)
        {
            // The address was well-formed and the peer did not complete a handshake: authorized (as
            // far as this tier knows) but unreachable — distinct from "no such node" (research R3).
            return Result<IWireChannel>.Refuse(RefusalReason.AuthorizedButUnreachable);
        }
        catch (PlatformNotSupportedException)
        {
            // Support was true a moment ago and the platform refused anyway. Still never downgrade.
            return Result<IWireChannel>.Refuse(RefusalReason.TransportUnsupported);
        }
    }

    /// <summary>
    /// A dialable endpoint is a <c>ynet-quic</c> address whose host is an IP LITERAL.
    /// </summary>
    /// <remarks>
    /// A DNS name is refused rather than looked up, and that is deliberate and consistent: FR-017
    /// says a human-memorable name is not something this tier resolves — it "MUST NOT fabricate a
    /// resolution". A hostname is exactly such a name, and trusting the host's resolver for it would
    /// put a peer's identity-to-address binding in DNS, outside the self-certified overlay. Bind the
    /// literal through <see cref="INodeAddressResolver"/>, or put a further resolver in front.
    /// </remarks>
    internal static bool TryDialableEndpoint(NodeAddress address, out IPEndPoint? endpoint)
    {
        endpoint = null;
        if (!string.Equals(address.Scheme, NodeAddress.QuicScheme, StringComparison.Ordinal))
            return false;

        var host = address.Host;
        if (host.Length > 1 && host[0] == '[' && host[^1] == ']')
            host = host[1..^1]; // bracketed IPv6, as NodeAddress.TryParse renders it

        if (!IPAddress.TryParse(host, out var ip)) return false;
        if (address.Port is < 1 or > 65535) return false;

        endpoint = new IPEndPoint(ip, address.Port);
        return true;
    }
}
