/// A count of the per-link runtime resources distributed GC must reclaim (FR-024,
/// SC-014): the global-writers table (`W_p`) entries, the
/// `GlobalSendRegistry` goals, the heap `onBind` callbacks, and the
/// request/reply table entries. Used by the SC-014 / T074 probe to assert that,
/// after N `permFail`s, every counter returns to its pre-link baseline — no
/// leak on peer death (today removal is one-shot only and leaks).
///
/// The link layer (`csharp/glp_link`) does not own these subsystems — they
/// live in the `out/csharp` runtime. The actual counts are read through an
/// [IResourceProbe] the Phase-3 wiring binds to the real structures;
/// this type is the transport-agnostic shape they report in.
class ResourceSnapshot {
  final int globalWriters;
  final int sendRegistryGoals;
  final int bindCallbacks;
  final int replyTableEntries;

  const ResourceSnapshot(
    this.globalWriters,
    this.sendRegistryGoals,
    this.bindCallbacks,
    this.replyTableEntries,
  );

  static const ResourceSnapshot zero = ResourceSnapshot(0, 0, 0, 0);

  /// True when every counter equals the supplied baseline (reclaimed to baseline).
  bool isBaseline(ResourceSnapshot baseline) => this == baseline;

  @override
  bool operator ==(Object other) =>
      other is ResourceSnapshot &&
      globalWriters == other.globalWriters &&
      sendRegistryGoals == other.sendRegistryGoals &&
      bindCallbacks == other.bindCallbacks &&
      replyTableEntries == other.replyTableEntries;

  @override
  int get hashCode => Object.hash(
      globalWriters, sendRegistryGoals, bindCallbacks, replyTableEntries);

  @override
  String toString() =>
      '{W_p=$globalWriters, send=$sendRegistryGoals, onBind=$bindCallbacks, reply=$replyTableEntries}';
}

/// Reads the current [ResourceSnapshot] from the live runtime. Bound
/// in Phase 3 to `GlobalWritersTable`, the `GlobalSendRegistry`, the heap
/// `_bindCallbacks`, and the reply table; the SC-014 test takes a baseline
/// before opening links and asserts return-to-baseline after reclamation.
abstract class IResourceProbe {
  ResourceSnapshot snapshot();
}
