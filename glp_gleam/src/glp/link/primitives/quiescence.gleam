//// glp/link/primitives/quiescence — the network-quiescence oracle (feature 059,
//// T089; the T054 quiescence predicate ported as a reusable Gleam module).
////
//// Decides whether a link-carrying run has reached NETWORK QUIESCENCE: no goal is
//// runnable AND nothing is in flight on any link. This is the termination oracle the
//// two-process link programs (and the T083 PI:17 adversarial suite + T066) consult to
//// distinguish "genuinely done / deadlocked" from "still exchanging" — a suspended-on-
//// inbound consumer with frames still in flight is NOT quiescent (a frame will wake it),
//// whereas a suspended consumer with every link closed and nothing in flight IS.
////
//// PURE + REUSABLE by construction: the core `decide` is a pure function of three
//// observable counts, so the adversarial suite can drive it with synthesized states
//// without a live engine. The adapters (`link_activity`, `is_run_quiescent`) source
//// those counts from a `LinkRuntime` + a scheduler runnable count, but the predicate
//// itself imports neither, so it stays trivially testable and engine-agnostic.
////
//// Definition (network-quiescent iff ALL three are zero):
////   - `runnable_goals`   — goals ready to reduce (scheduler queue length); the engine
////                          has work to do while this is > 0.
////   - `frames_in_flight` — links still exchanging data (an endpoint open with either
////                          direction not yet closed) — a frame may still arrive/depart.
////   - `open_pending`     — accepted-but-unadopted path-B connections parked awaiting an
////                          `_link_accept` (a handshake in progress).

import gleam/dict
import gleam/list
import gleam/option.{None, Some}
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime.{type LinkRuntime}

/// The quiescence verdict. `Active` names WHY the run is not quiescent (which count is
/// non-zero) so the adversarial suite / a diagnostic can report the blocking cause.
pub type Quiescence {
  /// No runnable goal and nothing in flight — the run has terminated (or deadlocked
  /// with every link closed; the caller distinguishes via the scheduler status).
  Quiescent
  /// Still active: at least one of the counts is non-zero.
  Active(runnable_goals: Int, frames_in_flight: Int, open_pending: Int)
}

/// The pure oracle: network-quiescent iff every count is zero (T089, T054). This is the
/// function the T083/T066 adversarial suite drives with synthesized counts.
pub fn decide(
  runnable_goals: Int,
  frames_in_flight: Int,
  open_pending: Int,
) -> Quiescence {
  case runnable_goals == 0 && frames_in_flight == 0 && open_pending == 0 {
    True -> Quiescent
    False -> Active(runnable_goals, frames_in_flight, open_pending)
  }
}

/// Convenience boolean form of `decide`.
pub fn is_quiescent(
  runnable_goals: Int,
  frames_in_flight: Int,
  open_pending: Int,
) -> Bool {
  case decide(runnable_goals, frames_in_flight, open_pending) {
    Quiescent -> True
    Active(..) -> False
  }
}

// ── adapters: source the counts from live link state ──────────────────────────

/// The link-layer activity of a `LinkRuntime`: `#(frames_in_flight, open_pending)`.
/// `frames_in_flight` counts established links still exchanging (endpoint present and
/// NOT both directions closed); `open_pending` counts parked path-B connections
/// awaiting `_link_accept`.
pub fn link_activity(runtime: LinkRuntime) -> #(Int, Int) {
  let in_flight =
    link_registry.handles(runtime.links)
    |> count(is_active_link)
  let pending = pending_count(runtime)
  #(in_flight, pending)
}

/// Decide network quiescence for a whole run: the scheduler's runnable-goal count
/// combined with the link runtime's activity. The caller passes
/// `scheduler.runnable_count(engine)` (kept out of this module to avoid an engine
/// import — the oracle stays link/engine-agnostic and unit-testable).
pub fn is_run_quiescent(runnable_goals: Int, runtime: LinkRuntime) -> Bool {
  let #(in_flight, pending) = link_activity(runtime)
  is_quiescent(runnable_goals, in_flight, pending)
}

/// A link is "still in flight" while its endpoint is established and at least one
/// direction is still open (data may still cross).
fn is_active_link(handle: LinkHandle) -> Bool {
  case handle.endpoint {
    Some(_) -> !{ handle.in_closed && handle.out_closed }
    None -> False
  }
}

fn pending_count(runtime: LinkRuntime) -> Int {
  dict.size(runtime.pending)
}

fn count(items: List(a), pred: fn(a) -> Bool) -> Int {
  list.fold(items, 0, fn(acc, x) {
    case pred(x) {
      True -> acc + 1
      False -> acc
    }
  })
}
