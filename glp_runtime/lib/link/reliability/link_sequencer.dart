/// Assigns the per-link monotone outbound sequence number (FR-020) that becomes a
/// frame's `MessageId` (the dedup + reorder key on the receive side). One
/// sequencer per link per direction; not thread-safe (a link's egress drainer is
/// single-threaded).
///
/// The sequence is the transport-level ordering key. It is paired on the wire with
/// the never-reused `(agent,index)` global name carried inside the payload;
/// together they form the dedup key (architecture-context.md §4.2). No wraparound
/// handling: a single link session will not emit 2^32 messages, and a reconnect
/// starts a fresh sequence (the global-name idempotency backstop in
/// `mad_context` covers cross-session replay).
class LinkSequencer {
  int _next;

  LinkSequencer([int start = 0]) : _next = start;

  /// The next sequence number, advancing the counter.
  int next() {
    final value = _next;
    _next = (_next + 1) & 0xFFFFFFFF;
    return value;
  }

  /// The value that [next] will return next (no side effect).
  int get peek => _next;
}
