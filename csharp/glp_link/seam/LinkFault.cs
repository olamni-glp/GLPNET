namespace GlpRuntime.Link.Seam;

/// <summary>
/// The raw, transport-level fault classification a leaf reports out-of-band of the
/// data path (FR-008). This is the SEAM-level signal, deliberately coarser than
/// the GLP monitor lattice: the reliability sublayer (architecture-context.md §5)
/// refines these into the <c>ok</c> / <c>tempFail</c> / <c>permFail</c> /
/// <c>closed</c> ground terms the program reads, applying the bounded-silence
/// heuristic that the seam cannot know about. A fault is NEVER a unification
/// verdict (FR-043) and a disconnect NEVER maps to a logical Fail (FR-044).
/// </summary>
public enum LinkFaultKind
{
    /// <summary>
    /// The peer/transport closed the link cleanly. Refined by the sublayer to a
    /// terminal <c>closed(LinkId, Reason)</c> term (<c>eos</c> for graceful;
    /// contracts/link-primitives.md §1, lines 67-70).
    /// </summary>
    Closed,

    /// <summary>
    /// A recoverable transport error (reset, timeout, dropped connection). The
    /// DEFAULT classification for silence; the sublayer surfaces <c>tempFail</c>
    /// and may recover via idempotent reconnect-redelivery (FR-045).
    /// </summary>
    Transient,

    /// <summary>
    /// An unrecoverable transport error (auth/cert failure, connection refused,
    /// protocol violation). The sublayer surfaces <c>permFail</c> and runs
    /// distributed GC (FR-024, FR-045).
    /// </summary>
    Permanent,
}

/// <summary>
/// An out-of-band fault notification raised by an <see cref="ILinkEndpoint"/>
/// (the <see cref="ILinkEndpoint.OnFault"/> event). Carries only what the seam
/// knows: which link, the coarse <see cref="LinkFaultKind"/>, and a human-readable
/// reason. The sublayer maps it onto the per-link monitor stream.
/// </summary>
/// <param name="Link">The link this fault concerns.</param>
/// <param name="Kind">The coarse transport-level classification.</param>
/// <param name="Reason">
/// A human-readable cause (e.g. "connection reset", "tls handshake failed"). It
/// becomes the <c>Reason</c> argument of the refined GLP fault term.
/// </param>
public sealed record LinkFaultSignal(LinkId Link, LinkFaultKind Kind, string Reason);
