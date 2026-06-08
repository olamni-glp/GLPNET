/// The fencing decision for a writer attempting to bind a global name.
enum FenceVerdict {
  /// This writer holds the highest epoch seen for the name; it may bind.
  admit,

  /// A newer (higher-epoch) writer has already taken over this name; this writer
  /// is stale and is fenced out. The link layer surfaces `permFail` for it —
  /// never a silent overwrite, never a `StateError` (FR-047).
  fenced,
}

/// Split-brain defense via fencing tokens (FR-047, SC-011). Each link
/// establishment carries a monotonically increasing `epoch` (the fencing
/// token, from [EpochAllocator]). This registry — the "resource" in
/// the Kleppmann fencing model — remembers the highest epoch admitted per global
/// name and FENCES any later write that carries a lower epoch. So when a
/// partitioned writer resumes after a newer writer has taken over, its stale
/// (lower-epoch) binding is rejected rather than silently overwriting the live
/// one; exactly one writer wins.
///
/// This is IN ADDITION TO global-name idempotency (the `mad_context` dedup
/// gate, T002): idempotency absorbs a re-delivered SAME binding; fencing rejects a
/// CONFLICTING stale one. In the real topology the stale writer always holds the
/// lower epoch (the higher epoch only exists after takeover, by which time the
/// stale end is partitioned away), so the highest-epoch-wins rule fences exactly
/// the stale writer. One registry per instance; not thread-safe.
class FencingRegistry {
  // epoch is the GLP/C# `ulong` fencing token; mirrored as a 64-bit Dart int
  // (monotone, drawn from EpochAllocator).
  final Map<String, int> _highestEpoch = <String, int>{};

  /// Decide whether a writer carrying [epoch] may bind
  /// [globalName]. Admits the first writer and any writer whose
  /// epoch is >= the highest admitted so far (an equal epoch is the same/idempotent
  /// writer; a higher epoch is a legitimate takeover). Fences a lower epoch.
  FenceVerdict admit(String globalName, int epoch) {
    final highest = _highestEpoch[globalName];
    if (highest != null && epoch < highest) {
      return FenceVerdict.fenced;
    }
    _highestEpoch[globalName] = epoch;
    return FenceVerdict.admit;
  }

  /// The highest epoch admitted for a name, or `null` if none yet.
  int? highestEpochFor(String globalName) => _highestEpoch[globalName];

  /// Drop all fencing state for a name (distributed GC after the link's writers
  /// are reclaimed, T024). After this the name is fresh again.
  void forget(String globalName) => _highestEpoch.remove(globalName);

  /// Number of global names currently tracked.
  int get trackedCount => _highestEpoch.length;
}

/// Monotone source of per-establishment fencing epochs (FR-047). Each new link
/// establishment (or writer takeover) draws the next epoch; higher always means
/// newer. One allocator per instance covers all its outbound establishments.
class EpochAllocator {
  int _next;

  EpochAllocator([int start = 1]) : _next = start;

  /// The next establishment epoch, advancing the counter.
  int next() => _next++;
}
