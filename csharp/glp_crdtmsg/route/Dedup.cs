// Dedup — msg_id (end-to-end) + per-link seq (feature 041-crdtmsg-mvp, T050).
//
// Contract C22 / FR-015: msg_id (end-to-end) + per-link seq (FIFO) suppress duplicates; apply is
// idempotent at the store boundary (the op-WAL is idempotent by dot anyway — this is the message-level
// layer above it). A message already seen (by msg_id) is dropped before it reaches the store.

namespace GlpRuntime.CrdtMsg.Route;

public sealed class Dedup
{
    private readonly HashSet<string> _seenMsgIds = new();
    private readonly Dictionary<string, long> _highestSeqByLink = new();

    /// <summary>
    /// True if (msgId, link, seq) is NEW and should be processed; false if it is a duplicate. Records the
    /// per-link high-water seq. Duplicate msg_id ⇒ suppressed even if the seq is fresh.
    /// </summary>
    public bool Accept(string msgId, string fromLink, long seq)
    {
        if (!_seenMsgIds.Add(msgId)) return false; // end-to-end duplicate
        long prev = _highestSeqByLink.GetValueOrDefault(fromLink, -1);
        if (seq > prev) _highestSeqByLink[fromLink] = seq;
        return true;
    }

    public long HighWater(string link) => _highestSeqByLink.GetValueOrDefault(link, -1);
}
