import '../seam/link_id.dart';

/// Thrown when one or more reclamation steps failed. The reclaimer still runs
/// EVERY registered step (best-effort), then raises this so no subsystem is left
/// un-reclaimed because an earlier one threw (FR-024: return to baseline, no leak).
///
/// The mirror of .NET's `AggregateException`: [inner] holds the individual
/// hook failures, in the order they were caught.
class ReclaimException implements Exception {
  final String message;
  final List<Object> inner;
  ReclaimException(this.message, Iterable<Object> inner)
      : inner = List<Object>.unmodifiable(inner);

  @override
  String toString() => 'ReclaimException: $message';
}

/// Distributed GC coordinator (FR-024, SC-014). On a link's `permFail` or
/// close, every subsystem holding per-link state — the LinkId→handle registry
/// (T030), the send-registry goals, the heap `onBind` callbacks, the
/// reply/CorrId table (T033), and the [FencingRegistry] (T023) — runs
/// its reclamation hook so the runtime returns to its pre-link baseline. Today
/// removal is one-shot only and LEAKS when the peer dies; this makes teardown
/// explicit and idempotent.
///
/// "Seam now, wire later": the subsystems register their hooks here in Phase 3
/// (just as the cycle-guard and seam interfaces are consumed later). Reclamation
/// is idempotent — a `permFail` followed by a `close` for the same link
/// reclaims exactly once. Hooks run in registration order and are then dropped, so
/// the reclaimer retains no reference to link state (FR-024 "no unreclaimable
/// cycle"). One reclaimer per instance; not thread-safe.
class LinkReclaimer {
  final Map<LinkId, List<void Function()>> _hooks =
      <LinkId, List<void Function()>>{};
  final Set<LinkId> _reclaimed = <LinkId>{};

  /// Register a reclamation hook for a link. A subsystem calls this when it
  /// allocates per-link state; the hook frees that state on
  /// [reclaim]. No-op-registers nothing for an already-reclaimed link
  /// (the hook would never fire) — instead it runs immediately so late allocations
  /// after teardown do not leak.
  void register(LinkId link, void Function() reclaim) {
    // ignore: unnecessary_null_comparison, dead_code
    if (reclaim == null) throw ArgumentError.notNull('reclaim');
    if (_reclaimed.contains(link)) {
      reclaim(); // link already torn down — reclaim this straggler now
      return;
    }
    final list = _hooks.putIfAbsent(link, () => <void Function()>[]);
    list.add(reclaim);
  }

  /// Reclaim all state for a link. Runs every registered hook (best-effort,
  /// continuing past a thrower), drops them, and marks the link reclaimed.
  /// Returns `true` if this call performed the reclamation, `false` if
  /// the link was already reclaimed (idempotent). Throws
  /// [ReclaimException] if any hook failed — after all have run.
  bool reclaim(LinkId link, String reason) {
    if (!_reclaimed.add(link)) {
      return false; // already reclaimed → idempotent no-op
    }

    final hooks = _hooks.remove(link);
    if (hooks == null) {
      return true;
    }

    List<Object>? failures;
    for (final hook in hooks) {
      try {
        hook();
      } catch (ex) {
        (failures ??= <Object>[]).add(ex);
      }
    }

    if (failures != null) {
      throw ReclaimException(
          'link $link reclamation ($reason): ${failures.length} hook(s) failed',
          failures);
    }
    return true;
  }

  /// True once [reclaim] has run for the link.
  bool isReclaimed(LinkId link) => _reclaimed.contains(link);

  /// Links with registered hooks not yet reclaimed.
  int get pendingLinkCount => _hooks.length;
}
