namespace GlpRuntime.Link.Seam;

/// <summary>
/// One established, bilateral link end (FR-058). Carries opaque, self-delimiting
/// frames in FIFO order; the reliability sublayer sits ABOVE this (sequence/dedup,
/// reorder, framing, epoch/fence, GC, backpressure) and <c>MadContext</c> above
/// that. The endpoint itself knows nothing about terms or SRSW
/// (architecture-context.md §3).
/// </summary>
/// <remarks>
/// Wiring (architecture-context.md §3): <see cref="SendBytesAsync"/> is driven by
/// <c>MadContext.onMessageReady</c> (the serialized header + term as one frame);
/// a frame from <see cref="RecvBytesAsync"/> is decoded and dispatched to
/// <c>MadContext.handleMadAssignment</c>. The async <c>recv</c> shape is exactly
/// the convspec-escalated interface; native bodies are hand-authored per leaf, not
/// auto-converted (FR-058).
/// </remarks>
public interface ILinkEndpoint : IAsyncDisposable
{
    /// <summary>
    /// The stable, never-reused identity of this link (FR-007). Both establishment
    /// paths (listen/connect and request/accept) yield endpoints with an
    /// equivalent <see cref="LinkId"/> (FR-002).
    /// </summary>
    LinkId Id { get; }

    /// <summary>
    /// Send exactly one self-delimiting frame (an opaque <c>PayloadSerializer</c>
    /// blob). Per-link FIFO is preserved by the sublayer above; a leaf MUST deliver
    /// frames it accepts in submission order or report a fault (FR-018).
    /// </summary>
    /// <param name="frame">One complete frame. Treated as opaque bytes.</param>
    /// <param name="ct">Cancels the send.</param>
    Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default);

    /// <summary>
    /// Receive the next self-delimiting frame, awaiting one if none is buffered.
    /// One call yields exactly one frame; the sublayer reconstructs per-link FIFO
    /// from these (FR-018/FR-020).
    /// </summary>
    /// <param name="ct">Cancels the receive.</param>
    /// <returns>
    /// One complete frame, or <c>null</c> when the peer has cleanly ended the
    /// stream (graceful close → <c>closed(LinkId, eos)</c> upstream).
    /// </returns>
    Task<byte[]?> RecvBytesAsync(CancellationToken ct = default);

    /// <summary>
    /// Tear this end down. Graceful close lets in-flight frames drain and ends the
    /// peer's <see cref="RecvBytesAsync"/> with <c>null</c>; the sublayer emits the
    /// terminal <c>closed(LinkId, Reason)</c> term and runs distributed GC (FR-024).
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Raised out-of-band of the data path when the transport faults (FR-008). The
    /// sublayer turns the <see cref="LinkFaultSignal"/> into the
    /// <c>ok</c>/<c>tempFail</c>/<c>permFail</c> monitor-stream terms. A fault here
    /// never becomes a unification verdict (FR-043) and never fails the reader's
    /// data goal (FR-044).
    /// </summary>
    event Action<LinkFaultSignal> OnFault;
}
