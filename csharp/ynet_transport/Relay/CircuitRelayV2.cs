using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Ynet.Transport.Capability;
using Ynet.Transport.Link;

namespace Ynet.Transport.Relay;

/// <summary>
/// A circuit-relay-v2 RESERVATION voucher (libp2p circuit-relay-v2, clarify §5.2): the relay's own
/// gate on a circuit. It is minted ONLY for a peer whose 056 <see cref="AdmissionProof"/> admits THIS
/// relay, and is bound to (relay, peer, traffic class, expiry) under the relay's voucher secret — so
/// a peer can neither forge a voucher nor transplant one minted for a different relay/peer onto
/// another circuit.
/// </summary>
public readonly record struct ReservationVoucher(
    NodeId Relay,
    NodeId Peer,
    string TrafficClass,
    DateTimeOffset ExpiresAt,
    byte[] Mac);

/// <summary>
/// libp2p <b>circuit-relay-v2</b> (voucher-gated) forward for <c>mesh</c> traffic (T029, FR-007,
/// clarify §5.2). Two-phase, exactly as the libp2p mechanism: <see cref="Reserve"/> (the relay mints
/// a reservation voucher for an admitted peer) then <see cref="Forward"/> (the voucher gates every
/// forwarded frame).
///
/// This tier ENFORCES the 056 admission decision and never decides it (FR-024): the reservation is
/// refused unless <see cref="AdmissionEnforcer.IsSelectable"/> accepts the proof for this relay.
///
/// <b>Ciphertext-only (SC-004).</b> The relay holds NO session key: a forwarded frame is opaque
/// bytes copied downstream verbatim. The end-to-end <see cref="SessionSeal"/> is negotiated between
/// the two endpoints, so a relay — even a malicious admitted one — cannot read the payload.
/// REAL + TESTED (T028/T033).
/// </summary>
public sealed class CircuitRelayV2
{
    /// <summary>Reservation lifetime; a libp2p relay reservation is short-lived and re-reserved.</summary>
    public static readonly TimeSpan DefaultReservationTtl = TimeSpan.FromMinutes(15);

    private static readonly byte[] VoucherDomain = "ynet-circuit-relay-v2-voucher"u8.ToArray();

    private readonly NodeId _self;
    private readonly byte[] _voucherSecret;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _ttl;
    private long _forwarded;

    /// <param name="self">This relay's node id — a voucher is bound to it, so one minted here is
    /// never valid at another relay.</param>
    /// <param name="clock">Time source for reservation expiry; defaults to wall clock.</param>
    /// <param name="reservationTtl">Reservation lifetime; defaults to <see cref="DefaultReservationTtl"/>.</param>
    /// <param name="voucherSecret">The relay's voucher MAC secret; a fresh random secret by default
    /// (a restarted relay invalidates outstanding vouchers — fail-closed, never fail-open).</param>
    public CircuitRelayV2(
        NodeId self,
        Func<DateTimeOffset>? clock = null,
        TimeSpan? reservationTtl = null,
        byte[]? voucherSecret = null)
    {
        _self = self;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _ttl = reservationTtl ?? DefaultReservationTtl;
        _voucherSecret = voucherSecret ?? RandomNumberGenerator.GetBytes(32);
    }

    public RelayMechanism Mechanism => RelayMechanism.CircuitRelayV2;

    /// <summary>Frames this relay has forwarded (introspection, FR-023).</summary>
    public long ForwardedCount => Interlocked.Read(ref _forwarded);

    /// <summary>
    /// RESERVE: mint a reservation voucher for <paramref name="peer"/>. Refuses with
    /// <see cref="RefusalReason.RelayNotAdmitted"/> unless the 056 proof admits THIS relay — the
    /// relay enforces, never decides (FR-007/FR-008). The voucher's traffic class is copied from the
    /// proof, so a mesh reservation can never be replayed to gate an internet/critical circuit.
    /// </summary>
    public Result<ReservationVoucher> Reserve(NodeId peer, AdmissionProof proof)
    {
        if (!AdmissionEnforcer.IsSelectable(_self, proof))
            return Result<ReservationVoucher>.Refuse(RefusalReason.RelayNotAdmitted);

        var expiresAt = _clock() + _ttl;
        var voucher = new ReservationVoucher(_self, peer, proof.TrafficClass, expiresAt, Mac: []);
        return Result<ReservationVoucher>.Success(voucher with { Mac = ComputeMac(voucher) });
    }

    /// <summary>
    /// Verify a voucher presented to this relay: MAC-authentic (constant-time), bound to this relay,
    /// and unexpired. Returns the distinct refusal reason, or null when the circuit is gated open.
    /// </summary>
    public RefusalReason? VerifyVoucher(ReservationVoucher voucher)
    {
        // Bound to THIS relay: a voucher minted by another relay never gates a circuit here.
        if (voucher.Relay != _self)
            return RefusalReason.RelayNotAdmitted;

        var expected = ComputeMac(voucher);
        if (voucher.Mac is null || !CryptographicOperations.FixedTimeEquals(voucher.Mac, expected))
            return RefusalReason.RelayNotAdmitted; // forged / tampered / transplanted

        // An elapsed reservation no longer authorizes the circuit — re-reserve against a fresh proof.
        if (_clock() >= voucher.ExpiresAt)
            return RefusalReason.RelayNotAdmitted;

        return null;
    }

    /// <summary>
    /// CONNECT + forward one frame downstream, gated by <paramref name="voucher"/>. The frame is
    /// <b>opaque</b>: this relay has no session key and copies the sealed bytes verbatim (SC-004).
    /// A torn-down / closed downstream surfaces <see cref="RefusalReason.AuthorizedButUnreachable"/>
    /// (R3) — never a silent drop (FR-018).
    /// </summary>
    public Result<Ack> Forward(ReservationVoucher voucher, IWireChannel downstream, ReadOnlyMemory<byte> sealedFrame)
    {
        ArgumentNullException.ThrowIfNull(downstream);

        if (VerifyVoucher(voucher) is { } refusal)
            return Result<Ack>.Refuse(refusal); // zero side-effects: nothing was written downstream

        try { downstream.WriteFrame(sealedFrame.Span); }
        catch (IOException) { return Result<Ack>.Refuse(RefusalReason.AuthorizedButUnreachable); }

        return Result<Ack>.Success(new Ack(Interlocked.Increment(ref _forwarded)));
    }

    // mac = HMAC-SHA256(secret, domain || relay || peer || trafficClass || expiresAtUnixMs), each
    // field length-prefixed so no two distinct voucher tuples share a MAC pre-image.
    private byte[] ComputeMac(ReservationVoucher v)
    {
        var relay = Encoding.UTF8.GetBytes(v.Relay.Value);
        var peer = Encoding.UTF8.GetBytes(v.Peer.Value);
        var cls = Encoding.UTF8.GetBytes(v.TrafficClass ?? string.Empty);

        var buf = new byte[VoucherDomain.Length + 4 + relay.Length + 4 + peer.Length + 4 + cls.Length + 8];
        var s = buf.AsSpan();
        int o = 0;
        VoucherDomain.CopyTo(s[o..]); o += VoucherDomain.Length;
        o += WriteLengthPrefixed(s[o..], relay);
        o += WriteLengthPrefixed(s[o..], peer);
        o += WriteLengthPrefixed(s[o..], cls);
        BinaryPrimitives.WriteInt64BigEndian(s[o..], v.ExpiresAt.ToUnixTimeMilliseconds());

        return HMACSHA256.HashData(_voucherSecret, buf);
    }

    private static int WriteLengthPrefixed(Span<byte> dest, ReadOnlySpan<byte> field)
    {
        BinaryPrimitives.WriteUInt32BigEndian(dest, (uint)field.Length);
        field.CopyTo(dest[4..]);
        return 4 + field.Length;
    }
}
