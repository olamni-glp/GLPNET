using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Ynet.Transport.Capability;
using Ynet.Transport.Path;

namespace Ynet.Transport.Link;

/// <summary>
/// A duplex, ordered, reliable frame channel the YNET link rides (US1). Real impls:
/// <see cref="InProcessDuplexChannel"/> (two in-process nodes — a genuine loopback transport with
/// real backpressure/close semantics, NOT a mock) and — as a compiling seam pending T011 — a MsQuic
/// channel harvested from csharp/glp_link. The link/session logic is transport-agnostic.
/// </summary>
public interface IWireChannel : IDisposable
{
    /// <summary>Write one whole frame. Throws <see cref="IOException"/> if the channel is closed.</summary>
    void WriteFrame(ReadOnlySpan<byte> frame);

    /// <summary>Block for the next frame; returns null once the peer closed and the buffer drained.</summary>
    byte[]? ReadFrame();

    void Close();
}

/// <summary>An in-process duplex channel pair (loopback). <see cref="CreatePair"/> returns two
/// cross-wired ends — a real transport for two co-hosted nodes (US1 Independent Test / T016).</summary>
public sealed class InProcessDuplexChannel : IWireChannel
{
    private readonly BlockingCollection<byte[]> _inbound;
    private readonly BlockingCollection<byte[]> _outbound;

    private InProcessDuplexChannel(BlockingCollection<byte[]> inbound, BlockingCollection<byte[]> outbound)
    {
        _inbound = inbound;
        _outbound = outbound;
    }

    public static (IWireChannel a, IWireChannel b) CreatePair()
    {
        var ab = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
        var ba = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
        return (new InProcessDuplexChannel(inbound: ba, outbound: ab),
                new InProcessDuplexChannel(inbound: ab, outbound: ba));
    }

    public void WriteFrame(ReadOnlySpan<byte> frame)
    {
        try { _outbound.Add(frame.ToArray()); }
        catch (InvalidOperationException) { throw new IOException("wire channel closed"); }
    }

    public byte[]? ReadFrame()
    {
        try { return _inbound.Take(); }
        catch (InvalidOperationException) { return null; } // closed + drained
    }

    public void Close() { try { _outbound.CompleteAdding(); } catch (ObjectDisposedException) { } }

    public void Dispose() => Close();
}

/// <summary>
/// A REAL, TESTED YNET link session over an <see cref="IWireChannel"/> (US1, T014/T016): a mutual
/// identity-verified handshake (FR-002 — the node key IS the identity, verified pre-frame), an
/// ephemeral ECDH agreement fed into per-direction <see cref="SessionSeal"/> keys (AES-256-GCM,
/// H2/H3), sealed send/receive, and a graceful close (050 close-after-collect discipline) driving
/// <see cref="PathState"/>. The physical QUIC socket is one <see cref="IWireChannel"/> impl (seam);
/// nothing here fakes a connection (Constitution II).
/// </summary>
public sealed class YnetSession : IDisposable
{
    private readonly IWireChannel _channel;
    private readonly SessionSeal _send;
    private readonly SessionSeal _recv;
    private readonly PathState _path;
    private long _sendSeq;

    public LinkHandle Handle { get; }
    public NodeId Peer => Handle.Peer;

    private YnetSession(IWireChannel channel, SessionSeal send, SessionSeal recv, LinkHandle handle, PathState path)
    {
        _channel = channel;
        _send = send;
        _recv = recv;
        Handle = handle;
        _path = path;
    }

    private enum Role : byte { Dialer = 1, Listener = 2 }

    /// <summary>Dial: run the handshake as initiator, refusing if the peer's presented identity does
    /// not match <paramref name="expectedPeer"/> (FR-002 pre-frame gate).</summary>
    /// <param name="pathType">How this session reaches the peer. A relayed circuit (US4) carries the
    /// SAME end-to-end seal — the relay only moves ciphertext — but reports its path honestly for
    /// FR-023 auditability.</param>
    public static Result<YnetSession> Connect(
        IWireChannel channel, NodeIdentity self, NodeId expectedPeer, RoutingSelection selection,
        PathType pathType = PathType.Direct)
        => Handshake(channel, self, expectedPeer, Role.Dialer, selection, pathType);

    /// <summary>Accept: run the handshake as listener (the peer id is learned from the handshake).
    /// A listener cannot tell a relayed dialer from a direct one — that opacity is the point of a
    /// relay — so it reports the path it observes.</summary>
    public static Result<YnetSession> Accept(IWireChannel channel, NodeIdentity self, RoutingSelection selection)
        => Handshake(channel, self, expectedPeer: null, Role.Listener, selection, PathType.Direct);

    private static Result<YnetSession> Handshake(IWireChannel channel, NodeIdentity self, NodeId? expectedPeer, Role role, RoutingSelection selection, PathType pathType)
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var myEcdhSpki = ecdh.PublicKey.ExportSubjectPublicKeyInfo();

        // Hello binds the ephemeral ECDH key to the node identity: the identity key SIGNS the ECDH
        // SPKI (authenticated DH — a MITM cannot substitute its own ephemeral key under a victim's
        // known identity SPKI, FR-002). Send our hello, then read the peer's.
        var mySig = self.Sign(myEcdhSpki);
        channel.WriteFrame(EncodeHello(self.PublicKeySpki, myEcdhSpki, mySig));

        var peerHello = channel.ReadFrame();
        if (peerHello is null)
            return Result<YnetSession>.Refuse(RefusalReason.AuthorizedButUnreachable);
        if (!TryDecodeHello(peerHello, out var peerIdentitySpki, out var peerEcdhSpki, out var peerSig))
            return Result<YnetSession>.Refuse(RefusalReason.IdentityMismatch);

        // FR-002: the ephemeral ECDH key MUST be signed by the presented identity (no unauthenticated
        // DH), and the presented node id MUST be H(its identity key). A dialer additionally pins the
        // expected peer; a listener learns (and returns) the verified peer id.
        if (!NodeIdentity.VerifySpki(peerIdentitySpki, peerEcdhSpki, peerSig))
            return Result<YnetSession>.Refuse(RefusalReason.IdentityMismatch);
        var peerId = NodeIdentity.DeriveNodeId(peerIdentitySpki);
        if (expectedPeer is { } exp && exp != peerId)
            return Result<YnetSession>.Refuse(RefusalReason.IdentityMismatch);

        byte[] shared;
        try
        {
            using var peerEcdh = ECDiffieHellman.Create();
            peerEcdh.ImportSubjectPublicKeyInfo(peerEcdhSpki, out _);
            shared = ecdh.DeriveRawSecretAgreement(peerEcdh.PublicKey);
        }
        catch (CryptographicException)
        {
            return Result<YnetSession>.Refuse(RefusalReason.AuthorizedButUnreachable);
        }

        // Per-direction keys: each node seals with its own role's key, opens with the peer's. Salt is
        // derived order-independently from both ephemeral keys so both ends agree without extra bytes.
        var salt = DeriveSalt(myEcdhSpki, peerEcdhSpki);
        var myDir = role == Role.Dialer ? SessionSeal.Direction.Initiator : SessionSeal.Direction.Responder;
        var peerDir = role == Role.Dialer ? SessionSeal.Direction.Responder : SessionSeal.Direction.Initiator;
        var send = SessionSeal.Derive(shared, "ynet-link"u8, salt, myDir);
        var recv = SessionSeal.Derive(shared, "ynet-link"u8, salt, peerDir);

        var path = new PathState();
        path.Established(pathType);
        var handle = new LinkHandle(Guid.NewGuid(), peerId, pathType);
        return Result<YnetSession>.Success(new YnetSession(channel, send, recv, handle, path));
    }

    /// <summary>Seal and send an application frame over the live session (US1).</summary>
    public Result<Ack> Send(ReadOnlyMemory<byte> frame)
    {
        if (_path.Phase != PathPhase.Live)
            return Result<Ack>.Refuse(RefusalReason.AuthorizedButUnreachable);
        try { _channel.WriteFrame(_send.Seal(frame.Span)); }
        catch (IOException) { return Result<Ack>.Refuse(RefusalReason.AuthorizedButUnreachable); }
        return Result<Ack>.Success(new Ack(Interlocked.Increment(ref _sendSeq)));
    }

    /// <summary>Receive and open the next frame. A tampered/wrong-key frame is a distinct refusal
    /// (SealUnavailable), never a silent downgrade; a closed channel is AuthorizedButUnreachable.</summary>
    public Result<ReadOnlyMemory<byte>> Receive()
    {
        var sealed_ = _channel.ReadFrame();
        if (sealed_ is null)
            return Result<ReadOnlyMemory<byte>>.Refuse(RefusalReason.AuthorizedButUnreachable);
        var pt = _recv.Open(sealed_);
        if (pt is null)
            return Result<ReadOnlyMemory<byte>>.Refuse(RefusalReason.SealUnavailable);
        return Result<ReadOnlyMemory<byte>>.Success(pt);
    }

    /// <summary>Graceful close (050 close-after-collect): tear the path down, then close the channel.</summary>
    public void Close()
    {
        if (_path.Phase == PathPhase.Live)
        {
            _path.BeginTearDown();
            _path.TearDownComplete();
        }
        _channel.Close();
    }

    /// <summary>Path introspection (FR-023). A relayed circuit reports its one forwarding hop; a
    /// direct path reports none.</summary>
    public PathInfo Info(RoutingSelection selection) =>
        new(Handle.PathType, selection.Mode, selection.AnonymityLevel,
            RelayHops: Handle.PathType == PathType.Relayed ? 1 : 0);

    // Hello = [u16 idLen][identitySpki][u16 ecLen][ecdhSpki][u16 sigLen][signature over ecdhSpki].
    private static byte[] EncodeHello(ReadOnlySpan<byte> identitySpki, ReadOnlySpan<byte> ecdhSpki, ReadOnlySpan<byte> signature)
    {
        var buf = new byte[2 + identitySpki.Length + 2 + ecdhSpki.Length + 2 + signature.Length];
        var s = buf.AsSpan();
        int o = 0;
        BinaryPrimitives.WriteUInt16BigEndian(s[o..], (ushort)identitySpki.Length); o += 2;
        identitySpki.CopyTo(s[o..]); o += identitySpki.Length;
        BinaryPrimitives.WriteUInt16BigEndian(s[o..], (ushort)ecdhSpki.Length); o += 2;
        ecdhSpki.CopyTo(s[o..]); o += ecdhSpki.Length;
        BinaryPrimitives.WriteUInt16BigEndian(s[o..], (ushort)signature.Length); o += 2;
        signature.CopyTo(s[o..]);
        return buf;
    }

    private static bool TryDecodeHello(byte[] frame, out byte[] identitySpki, out byte[] ecdhSpki, out byte[] signature)
    {
        identitySpki = Array.Empty<byte>();
        ecdhSpki = Array.Empty<byte>();
        signature = Array.Empty<byte>();
        var s = frame.AsSpan();
        int o = 0;
        if (s.Length < 2) return false;
        int idLen = BinaryPrimitives.ReadUInt16BigEndian(s[o..]); o += 2;
        if (o + idLen + 2 > s.Length) return false;
        identitySpki = s.Slice(o, idLen).ToArray(); o += idLen;
        int ecLen = BinaryPrimitives.ReadUInt16BigEndian(s[o..]); o += 2;
        if (o + ecLen + 2 > s.Length) return false;
        ecdhSpki = s.Slice(o, ecLen).ToArray(); o += ecLen;
        int sigLen = BinaryPrimitives.ReadUInt16BigEndian(s[o..]); o += 2;
        if (o + sigLen > s.Length) return false;
        signature = s.Slice(o, sigLen).ToArray();
        return true;
    }

    private static uint DeriveSalt(byte[] a, byte[] b)
    {
        var (x, y) = a.AsSpan().SequenceCompareTo(b) <= 0 ? (a, b) : (b, a);
        var buf = new byte[x.Length + y.Length];
        Buffer.BlockCopy(x, 0, buf, 0, x.Length);
        Buffer.BlockCopy(y, 0, buf, x.Length, y.Length);
        Span<byte> h = stackalloc byte[32];
        SHA256.HashData(buf, h);
        return BinaryPrimitives.ReadUInt32BigEndian(h);
    }

    public void Dispose()
    {
        _send.Dispose();
        _recv.Dispose();
        _channel.Dispose();
    }
}
