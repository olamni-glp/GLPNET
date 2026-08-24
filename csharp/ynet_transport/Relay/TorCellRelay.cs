using System.Buffers.Binary;
using Ynet.Transport.Capability;
using Ynet.Transport.Link;

namespace Ynet.Transport.Relay;

/// <summary>
/// Tor-style <b>fixed-size cell</b> relay — the default mechanism for <c>internet</c> and
/// <c>critical</c> traffic classes (T030, FR-007, clarify §5.2).
///
/// Every cell on the wire is exactly <see cref="CellSize"/> bytes: the payload is padded out, so a
/// relay (or an observer sitting at one) learns neither the payload nor <i>its length</i> — the
/// traffic-analysis property a raw verbatim forward cannot give. A frame larger than
/// <see cref="MaxPayload"/> is fragmented across cells and reassembled at the far endpoint, so a
/// large frame is never silently dropped or truncated (FR-018).
///
/// <b>Ciphertext-only (SC-004).</b> The relay reads the cell <i>header</i> (the circuit id — its
/// routing demux) and nothing else: it holds no session key, and copies each cell downstream
/// verbatim. REAL + TESTED (T028/T033).
///
/// Cell layout: <c>[16B circuitId][1B flags][2B payloadLen][payload…][zero padding]</c>.
/// The padding is outside the endpoints' AEAD, so its content carries no signal — zeros keep the
/// mechanism deterministic under test without weakening the fixed-size property.
/// </summary>
public sealed class TorCellRelay
{
    /// <summary>Classic Tor cell width. Every forwarded cell is exactly this many bytes.</summary>
    public const int CellSize = 512;

    private const int CircuitIdSize = 16; // Guid
    private const int FlagsSize = 1;
    private const int LengthSize = 2;
    private const int HeaderSize = CircuitIdSize + FlagsSize + LengthSize;

    /// <summary>Payload bytes carried by a single cell; a larger frame fragments across cells.</summary>
    public const int MaxPayload = CellSize - HeaderSize;

    [Flags]
    private enum CellFlags : byte
    {
        None = 0,
        MoreFragments = 1 << 0,
    }

    private long _forwarded;

    public RelayMechanism Mechanism => RelayMechanism.TorCell;

    /// <summary>Cells this relay has forwarded (introspection, FR-023).</summary>
    public long ForwardedCount => Interlocked.Read(ref _forwarded);

    /// <summary>
    /// Forward one cell downstream verbatim. The relay validates only the fixed cell width — it never
    /// opens the payload (SC-004). A non-conforming cell is refused, never forwarded, so a malformed
    /// frame cannot be laundered through the relay onto the next hop.
    /// </summary>
    public Result<Ack> Forward(IWireChannel downstream, ReadOnlyMemory<byte> cell)
    {
        ArgumentNullException.ThrowIfNull(downstream);

        if (cell.Length != CellSize)
            return Result<Ack>.Refuse(RefusalReason.AuthorizedButUnreachable);

        try { downstream.WriteFrame(cell.Span); }
        catch (IOException) { return Result<Ack>.Refuse(RefusalReason.AuthorizedButUnreachable); }

        return Result<Ack>.Success(new Ack(Interlocked.Increment(ref _forwarded)));
    }

    /// <summary>
    /// The relay's routing demux: read the circuit id from a cell header. This is the ONLY field a
    /// relay reads — the payload stays sealed to it (SC-004).
    /// </summary>
    public static bool TryPeekCircuit(ReadOnlySpan<byte> cell, out Guid circuitId)
    {
        circuitId = default;
        if (cell.Length != CellSize) return false;
        circuitId = new Guid(cell[..CircuitIdSize]);
        return true;
    }

    /// <summary>
    /// Fragment a sealed frame into fixed-size cells bound to <paramref name="circuitId"/>. An empty
    /// frame still yields exactly one cell, so a zero-length send is observable end-to-end rather
    /// than vanishing.
    /// </summary>
    public static IReadOnlyList<byte[]> Encode(Guid circuitId, ReadOnlySpan<byte> sealedFrame)
    {
        var cells = new List<byte[]>();
        int offset = 0;
        do
        {
            int take = Math.Min(MaxPayload, sealedFrame.Length - offset);
            bool more = offset + take < sealedFrame.Length;

            var cell = new byte[CellSize]; // zero-filled => padding is already in place
            var s = cell.AsSpan();
            circuitId.TryWriteBytes(s[..CircuitIdSize]);
            s[CircuitIdSize] = (byte)(more ? CellFlags.MoreFragments : CellFlags.None);
            BinaryPrimitives.WriteUInt16BigEndian(s[(CircuitIdSize + FlagsSize)..], (ushort)take);
            sealedFrame.Slice(offset, take).CopyTo(s[HeaderSize..]);

            cells.Add(cell);
            offset += take;
        }
        while (offset < sealedFrame.Length);

        return cells;
    }

    /// <summary>
    /// Reassembles a cell stream back into whole frames at an endpoint. One instance per circuit
    /// direction; not thread-safe (a channel end is read by one reader, matching
    /// <see cref="IWireChannel"/>).
    /// </summary>
    public sealed class Reassembler
    {
        private readonly List<byte> _pending = new();

        /// <summary>
        /// Accept one cell. Returns the completed frame when this cell ends it, else null (more
        /// fragments outstanding). Returns null and drops the partial on a malformed cell — the
        /// endpoint then observes a closed/unusable channel rather than a corrupted frame (fail
        /// closed; a tampered payload additionally fails the endpoints' AEAD).
        /// </summary>
        public byte[]? Accept(ReadOnlySpan<byte> cell)
        {
            if (cell.Length != CellSize) { _pending.Clear(); return null; }

            var flags = (CellFlags)cell[CircuitIdSize];
            int len = BinaryPrimitives.ReadUInt16BigEndian(cell[(CircuitIdSize + FlagsSize)..]);
            if (len > MaxPayload) { _pending.Clear(); return null; }

            _pending.AddRange(cell.Slice(HeaderSize, len));

            if (flags.HasFlag(CellFlags.MoreFragments)) return null;

            var frame = _pending.ToArray();
            _pending.Clear();
            return frame;
        }
    }
}

/// <summary>
/// Terminates the Tor-style cell layer at an <b>endpoint</b> (T030): frames written by the endpoint
/// are fragmented into fixed-size cells, and cells read from the wire are reassembled back into
/// frames. Decorating both endpoints of a relayed path means every cell crossing the relay is
/// exactly <see cref="TorCellRelay.CellSize"/> bytes while the endpoints keep their unchanged
/// whole-frame <see cref="IWireChannel"/> contract — the relay stays a verbatim cell forwarder that
/// holds no session key (SC-004).
/// </summary>
public sealed class CellChannel : IWireChannel
{
    private readonly IWireChannel _inner;
    private readonly Guid _circuitId;
    private readonly TorCellRelay.Reassembler _reassembler = new();

    public CellChannel(IWireChannel inner, Guid circuitId)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _circuitId = circuitId;
    }

    public Guid CircuitId => _circuitId;

    public void WriteFrame(ReadOnlySpan<byte> frame)
    {
        foreach (var cell in TorCellRelay.Encode(_circuitId, frame))
            _inner.WriteFrame(cell);
    }

    public byte[]? ReadFrame()
    {
        // Drain cells until one completes a frame; null once the peer closed and the buffer drained.
        while (true)
        {
            var cell = _inner.ReadFrame();
            if (cell is null) return null;

            var frame = _reassembler.Accept(cell);
            if (frame is not null) return frame;
        }
    }

    public void Close() => _inner.Close();

    public void Dispose() => _inner.Dispose();
}
