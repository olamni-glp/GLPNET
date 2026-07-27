//// glp/link/quiescence — the distributed-run termination oracle (feature 050, T054;
//// GAP-G6, data-model.md "QuiescenceOracle").
////
//// Distributed tests need an oracle for "the computation is done" BEFORE distributed
//// acceptance can be judged (spec Edge Cases; prerequisite for FR-017's gated
//// milestones and the T056 round-trip matrix). Three verdicts:
////
////   * **Running** — someone can still make progress: a runnable goal anywhere, a
////     buffered-but-unapplied inbound item, or a frame in flight on a carrier. A
////     suspended goal under these conditions is just WAITING — a wake may arrive.
////   * **Quiescent** — clean completion: nothing runnable, nothing in flight,
////     nothing suspended. Every goal reduced away (the per-node `Success` status,
////     summed over the run).
////   * **Deadlocked** — nothing runnable and nothing in flight, but suspended goals
////     REMAIN. No frame can arrive to bind what they wait on and no local goal can
////     either, so they can never wake: the run is done in the bad way. This is the
////     quiescence/deadlock distinction the spec names — without in-flight counts, a
////     snapshot cannot tell "waiting on the network" from "waiting forever".
////
//// **The oracle judges; the harness observes.** `judge` is a PURE function over a
//// snapshot the caller assembled. That split is forced, not stylistic: a distributed
//// verdict is only meaningful over a CONSISTENT snapshot, and only the run's driver
//// can produce one — it knows when its engines are parked and what its transports
//// hold; a live prober inside this module could interleave with delivery and judge a
//// torn state. In particular `in_flight` cannot be observed from here at all (a BEAM
//// Subject's mailbox cannot be counted without consuming it, and a TCP socket's
//// queue is invisible) — the driver supplies what it knows, and a driver that cannot
//// know supplies its sent-minus-applied ledger.
////
//// **Stability discipline (the classic double-collect):** one snapshot's `Quiescent`
//// or `Deadlocked` is trustworthy only if nothing was in flight while it was taken.
//// Drivers should re-observe and re-judge; `judge_stable` encodes the rule — a
//// verdict stands only when two consecutive snapshots agree and both saw zero
//// in-flight. `Running` is always safe to report from one snapshot.

import gleam/list
import glp/engine/scheduler.{type Engine}

/// One node's contribution to a run snapshot.
///
/// - `runnable`: goals ready to reduce right now (a non-drained queue).
/// - `suspended`: goals parked on unbound readers.
/// - `inbound_buffered`: items received off the wire but not yet applied to the
///   heap (e.g. drained-but-unapplied pump items — 0 for a `step_link` driver,
///   which applies everything it drains before reducing).
pub type NodeObservation {
  NodeObservation(runnable: Int, suspended: Int, inbound_buffered: Int)
}

/// The three-way verdict (data-model.md GAP-G6).
pub type Verdict {
  Running
  Quiescent
  Deadlocked
}

/// Judge one consistent snapshot: the per-node observations plus the frames the
/// driver knows to be in flight between nodes (sent minus applied).
pub fn judge(nodes: List(NodeObservation), in_flight: Int) -> Verdict {
  let live =
    in_flight > 0
    || list.any(nodes, fn(n) { n.runnable > 0 || n.inbound_buffered > 0 })
  case live {
    True -> Running
    False ->
      case list.any(nodes, fn(n) { n.suspended > 0 }) {
        True -> Deadlocked
        False -> Quiescent
      }
  }
}

/// The double-collect rule: a terminal verdict (`Quiescent`/`Deadlocked`) stands
/// only if two CONSECUTIVE snapshots agree on it and neither saw anything in
/// flight; anything else is still `Running`. The driver takes snapshot₁, lets the
/// system breathe (its own settle interval), takes snapshot₂, and calls this.
pub fn judge_stable(
  first: #(List(NodeObservation), Int),
  second: #(List(NodeObservation), Int),
) -> Verdict {
  let #(nodes1, in_flight1) = first
  let #(nodes2, in_flight2) = second
  case judge(nodes1, in_flight1), judge(nodes2, in_flight2) {
    Quiescent, Quiescent -> Quiescent
    Deadlocked, Deadlocked -> Deadlocked
    _, _ -> Running
  }
}

/// Observe one local engine (the harness helper for the common case). The engine's
/// own queue/store are consistent by construction — it is a value. The caller adds
/// `inbound_buffered` from whatever it holds outside the engine.
///
/// The suspended count is exact precisely when it matters: with a DRAINED queue the
/// goal store holds only suspended goals (`terminal_status`'s invariant), and that
/// is the only case `judge` consults the count — a node with a non-empty queue makes
/// the whole run `Running` before suspension is ever examined, so it reports 0
/// rather than an unsplittable store total.
pub fn observe(engine: Engine, inbound_buffered: Int) -> NodeObservation {
  case scheduler.has_runnable(engine) {
    True ->
      NodeObservation(
        runnable: 1,
        suspended: 0,
        inbound_buffered: inbound_buffered,
      )
    False ->
      NodeObservation(
        runnable: 0,
        suspended: scheduler.goal_count(engine),
        inbound_buffered: inbound_buffered,
      )
  }
}
