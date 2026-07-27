//// glp/link/primitives/link_teardown — the ONE link-teardown core both close paths
//// converge on (feature 050, T050.C8).
////
//// Port of `csharp/glp_link/primitives/LinkTeardown.cs` (mirror
//// `glp_runtime/lib/link/primitives/link_teardown.dart`).
////
//// The ABRUPT `'_link_close'/2` kernel (RST_STREAM-equiv teardown by ground LinkId,
//// regardless of stream state) and the GRACEFUL stream-end close both land here. In the
//// Gleam mapping the graceful path is even more literal than the oracle's: `link_drain/3`'s
//// `[]` clause (self.glp:562-564) dispatches `'_link_close'(LinkId?, eos)` — the D-2
//// egress drainer replaced the oracle's `onBind`-observed `[]`, so BOTH paths arrive
//// through the SAME K7 kernel and cannot diverge (the R-5 argument, applied to close).
////
//// Teardown order (the oracle's, verbatim):
////   1. terminal `closed(LinkId, Reason)` on EVERY monitor stream (`deliver_fault`,
////      FR-008) — an INTENTIONAL terminal, distinct from `tempFail`/`permFail`, riding
////      the ordinary bound-term delivery (never a fourth verdict — FR-043);
////   2. END those streams with `[]` (`end_all`) — a close is terminal, so the monitor
////      streams close too;
////   3. close the transport endpoint (the seam's idempotent `close`);
////   4. distributed GC (FR-024): remove the registry entry, returning the runtime to
////      its pre-link baseline. Repeat close then finds nothing → non-fatal abort at the
////      kernel (close OBSERVES a link; "robustness is a workaround").
////
//// **The data path is left untouched**: a goal suspended on `In` stays suspended after
//// a close — it learns of the teardown through the monitor stream (FR-044) — so
//// teardown never binds the `In` cursor.
////
//// **Sync-seam simplifications vs the oracle (D-3 corollaries, not semantic changes):**
////   * No flush-then-close ordering: the oracle chains the graceful close on its async
////     egress tail so the FIN cannot race queued sends. Gleam `endpoint.send` is
////     synchronous-blocking — every prior `ship_ground` has already completed by the
////     time teardown runs — so there is nothing to flush.
////   * No deferred-connect gate: establishment is synchronous, so the handle always
////     holds an open endpoint.
////   * **Residual (recorded, not solved): the pump process.** The oracle's `dispose`
////     cancels the recv loop via a token the async recv races. The Gleam loop is parked
////     INSIDE the blocking `endpoint.recv`, and the ratified no-OTP subset
////     (spawn/new_subject/receive — T048/T050.B) offers no way to interrupt it without
////     extending that subset (`process.kill`) or adopting OTP selectors — a §1.14-adjacent
////     surface call, not one this step makes silently. After teardown the loop drains
////     naturally: the seam's `close` FINs our side, a well-behaved peer FINs back, the
////     loop sends `Closed`, and the applier's registry miss makes it a no-op. Until the
////     peer FINs, the process stays parked — bounded by the peer, leaked if the peer
////     never answers. Escalate if that bound is unacceptable.

import gleam/list
import glp/link/primitives/link_faults
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_registry.{type LinkRegistry}
import glp/runtime/heap.{type Heap}
import glp/runtime/suspension.{type GoalRef}

/// Tear `handle`'s link down for `reason`: terminal `closed/2` fan-out, monitor streams
/// ended, endpoint closed, registry entry removed (distributed GC, FR-024). Returns the
/// woken goals from both binds — monitors reading `[closed(..)|..]` and watchers of the
/// stream ends — for the runner to re-enqueue.
pub fn teardown(
  heap: Heap,
  links: LinkRegistry,
  handle: LinkHandle,
  reason: String,
) -> #(Heap, LinkRegistry, List(GoalRef)) {
  // 1. The terminal close term on every observer, 2. then end their streams. The
  //    handle threading matters: `deliver_fault` advances every cursor, and `end_all`
  //    must bind those ADVANCED tails — ending the pre-delivery cursors would try to
  //    double-bind the cells the delivery just consed.
  let closed_term = link_faults.closed(handle.id, reason)
  let #(heap, handle, woken1) = link_faults.deliver_fault(heap, handle, closed_term)
  let #(heap, handle, woken2) = link_faults.end_all(heap, handle)
  // 3. Transport teardown (idempotent per the seam contract).
  let Nil = handle.endpoint.close()
  // 4. Distributed GC: the registry entry is the base layer's only per-link state
  //    (FR-024). The pump's parked recv loop drains via the peer's FIN — see the
  //    module-header residual — and its late items no-op on the registry miss.
  #(heap, link_registry.remove(links, handle.id), list.append(woken1, woken2))
}
