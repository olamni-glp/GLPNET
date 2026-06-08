namespace GlpRuntime.Link.Reliability;

/// <summary>
/// Assigns the per-link monotone outbound sequence number (FR-020) that becomes a
/// frame's <c>MessageId</c> (the dedup + reorder key on the receive side). One
/// sequencer per link per direction; not thread-safe (a link's egress drainer is
/// single-threaded).
/// </summary>
/// <remarks>
/// The sequence is the transport-level ordering key. It is paired on the wire with
/// the never-reused <c>(agent,index)</c> global name carried inside the payload;
/// together they form the dedup key (architecture-context.md §4.2). No wraparound
/// handling: a single link session will not emit 2^32 messages, and a reconnect
/// starts a fresh sequence (the global-name idempotency backstop in
/// <c>mad_context</c> covers cross-session replay).
/// </remarks>
public sealed class LinkSequencer
{
    private uint _next;

    public LinkSequencer(uint start = 0) => _next = start;

    /// <summary>The next sequence number, advancing the counter.</summary>
    public uint Next() => _next++;

    /// <summary>The value that <see cref="Next"/> will return next (no side effect).</summary>
    public uint Peek => _next;
}
