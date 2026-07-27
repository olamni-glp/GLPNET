//// glp/link/reliability/link_reclaimer — distributed-GC coordinator (feature 059,
//// T077 — port of glp_runtime/lib/link/reliability/link_reclaimer.dart, mirror
//// csharp/glp_link/reliability/LinkReclaimer.cs).
////
//// On a link's `permFail` or close, every subsystem holding per-link state — the
//// LinkId→handle registry, the global-send goals, the heap `onBind` callbacks, the
//// reply table, the `fencing_registry` — runs its reclamation hook so the runtime
//// returns to its pre-link baseline (FR-024, SC-014). Reclamation is IDEMPOTENT (a
//// `permFail` followed by a `close` for the same link reclaims exactly once); hooks
//// run in registration order and are then dropped, so the reclaimer retains no
//// reference to link state. Registering a hook for an ALREADY-reclaimed link runs it
//// immediately (a late allocation after teardown must not leak).
////
//// GLEAM MAPPING NOTE: the Dart reclaimer mutates a hook map in place and its hooks are
//// `void Function()`; here it is an IMMUTABLE value threaded through `register`/
//// `reclaim`, and hooks are `fn() -> Nil` effect closures (the driver supplies e.g. a
//// `link_registry.remove` wrapper). The Dart `ReclaimException` best-effort aggregation
//// (a hook that throws) has no BEAM analogue for pure `fn() -> Nil` closures — a hook
//// that must report failure carries it out-of-band — so `reclaim` runs every hook and
//// returns whether it performed the reclamation (the idempotency signal), faithful to
//// the Dart return contract.

import gleam/dict.{type Dict}
import gleam/list
import gleam/set.{type Set}
import glp/link/seam/link_id.{type LinkId}

pub opaque type LinkReclaimer {
  LinkReclaimer(hooks: Dict(LinkId, List(fn() -> Nil)), reclaimed: Set(LinkId))
}

/// An empty reclaimer.
pub fn new() -> LinkReclaimer {
  LinkReclaimer(hooks: dict.new(), reclaimed: set.new())
}

/// Register a reclamation hook for `link`. If the link is ALREADY reclaimed the hook
/// runs immediately (a straggler after teardown must not leak) and the reclaimer is
/// unchanged; otherwise the hook is appended in registration order.
pub fn register(
  reclaimer: LinkReclaimer,
  link: LinkId,
  hook: fn() -> Nil,
) -> LinkReclaimer {
  case set.contains(reclaimer.reclaimed, link) {
    True -> {
      hook()
      reclaimer
    }
    False -> {
      let existing = case dict.get(reclaimer.hooks, link) {
        Ok(hooks) -> hooks
        Error(Nil) -> []
      }
      LinkReclaimer(
        ..reclaimer,
        hooks: dict.insert(reclaimer.hooks, link, list.append(existing, [hook])),
      )
    }
  }
}

/// Reclaim all state for `link`: run every registered hook (in registration order),
/// drop them, and mark the link reclaimed. Returns the reclaimer + `True` if THIS call
/// performed the reclamation, `False` if the link was already reclaimed (idempotent
/// no-op).
pub fn reclaim(
  reclaimer: LinkReclaimer,
  link: LinkId,
) -> #(LinkReclaimer, Bool) {
  case set.contains(reclaimer.reclaimed, link) {
    True -> #(reclaimer, False)
    False -> {
      let hooks = case dict.get(reclaimer.hooks, link) {
        Ok(hooks) -> hooks
        Error(Nil) -> []
      }
      list.each(hooks, fn(hook) { hook() })
      #(
        LinkReclaimer(
          hooks: dict.delete(reclaimer.hooks, link),
          reclaimed: set.insert(reclaimer.reclaimed, link),
        ),
        True,
      )
    }
  }
}

/// True once `reclaim` has run for the link.
pub fn is_reclaimed(reclaimer: LinkReclaimer, link: LinkId) -> Bool {
  set.contains(reclaimer.reclaimed, link)
}

/// Links with registered hooks not yet reclaimed.
pub fn pending_link_count(reclaimer: LinkReclaimer) -> Int {
  dict.size(reclaimer.hooks)
}
