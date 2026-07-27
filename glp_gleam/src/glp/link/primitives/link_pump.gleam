//// glp/link/primitives/link_pump — the INGRESS pump (feature 050, T050.C6).
////
//// Port of `csharp/glp_link/primitives/LinkPump.cs` (mirror
//// `glp_runtime/lib/link/primitives/link_pump.dart`).
////
//// **The discipline the oracle establishes and this port keeps verbatim** (025 design
//// ref `research/inbound-pump-and-isolate-manager.md` §1.3): a per-link background
//// receive loop decodes arriving frames and does nothing but ENQUEUE; it NEVER touches
//// the heap. The runner-side `try_apply_next` dequeues one item and extends that link's
//// `In` stream — mint a fresh pair, cons `[value | reader]`, bind the writer, wake the
//// suspended readers, advance the cursor. The queue is the sole cross-process structure.
//// That split is the whole point: the heap has a single owner, and an inbound frame
//// arriving mid-reduction must not race it.
////
//// **D-3 (RESOLVED by the T049 precedent).** The C#/Dart oracles use an async
//// `Task<byte[]?> RecvBytesAsync` + background tasks + a thread-safe inbox. The Gleam
//// `endpoint.recv` seam is SYNCHRONOUS BLOCKING (T045/T049), so the background loop is a
//// BEAM process (`process.spawn` + a `Subject`, **no `gleam_otp`** — the AtomVM subset
//// T012 pins), and the "thread-safe inbox" is that Subject's mailbox. Consistent with the
//// ratified madGLP Phase-B process model.
////
//// **Not in C6, deliberately:**
////   * *Reassembly / dedup / reorder* — T052. The base MTU is `None`, so every payload
////     ships as ONE `Whole` frame (D-8) and the far side needs no reassembler; a
////     `Fragment` arriving here is a wire-contract violation, surfaced by
////     `link_wire.decode_frame`.
////   * *In C7 (landed):* a recv-time transport fault ends this link's loop and is
////     reported as `Faulted` carrying the refined lattice term; the applier fans it to
////     every monitor cursor via `link_faults.deliver_fault` (FR-008). The endpoint's
////     out-of-band `faults` Subject stays untouched — in the SYNC seam every transport
////     fault surfaces as a `recv`/`send` return value on the call that hit it (`send`
////     errors reach the kernels as `EgressError`), so there is no async side-channel
////     left to subscribe to; the Subject exists for transport-internal use.
////   * *Path-B request surfacing* — the Dart pump carries a `requestWriterAddr` inbox
////     item because its listen/token-read are async. Gleam's C4 `'_link_listen'` does
////     both SYNCHRONOUSLY on the runner thread, so that item has no counterpart here.
////   * *Shutdown* — the oracle's `dispose()` (a cancel token every recv loop races) is
////     C8 `link_teardown`. Until then a loop ends only on the peer's FIN or a transport
////     fault, so a link torn down from THIS end leaves its loop parked in `recv` until
////     the peer FINs. Recorded here rather than half-built: cancelling a blocking
////     `process.receive` needs the teardown path C8 owns.

import gleam/erlang/process.{type Subject}
import gleam/list
import gleam/option.{None, Some}
import gleam/string
import glp/link/primitives/link_faults
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_registry.{type LinkRegistry}
import glp/link/primitives/link_wire
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_id.{type LinkId}
import glp/runtime/heap.{type Heap}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{type Term, ConstAtom, ConstTerm, VarRef}

/// One decoded inbound event awaiting application on the runner side. Every variant
/// carries its `LinkId` because ONE queue serves every link (the oracle's single shared
/// inbox) — the applier looks the handle up in the registry rather than trusting a
/// captured copy, which would be stale the moment egress advanced the sequencer.
pub type InboundItem {
  /// A ground term the peer shipped, to extend this link's `In` stream with.
  /// `message_id` is the frame's — unused by the base applier (arrival order IS the
  /// order), carried for the T052 dedup/reorder sublayer.
  Data(link: LinkId, message_id: Int, value: Term)
  /// The peer cleanly ended its send side (`recv` → `Ok(None)`): end `In` with `[]`.
  Closed(link: LinkId)
  /// The receive loop stopped on a transport fault or a malformed frame. `fault` is the
  /// ALREADY-REFINED lattice term (`closed`/`tempFail`/`permFail` — refinement is pure
  /// data, so the pump does it off the runner); the applier fans it out to every
  /// monitor cursor (C7, FR-008). Carried as a bound term, never a crash and never a
  /// logical Fail (FR-043/044).
  Faulted(link: LinkId, fault: Term)
}

/// The runner's end of the inbox. One per engine, shared by every link's loop.
pub type Inbox =
  Subject(InboundItem)

/// A fresh inbox. The engine facade holds this and hands it to `start` for each
/// established link.
pub fn new_inbox() -> Inbox {
  process.new_subject()
}

/// Start the background receive loop for an established link.
///
/// 🔴 Call this only AFTER the handle's `in_writer` ingress cursor is wired — the oracle
/// throws on `addLink before the In-stream ingress cursor was wired`, and for the same
/// reason: an item that arrives before the cursor exists has nowhere to go and would be
/// dropped silently. Establishment wires the cursors and then arms the pump.
///
/// The loop owns the blocking `endpoint.recv` and only ever sends to `inbox`. It ends on
/// the PEER's FIN or a transport fault — NOT on this end closing its own sender, because
/// a link is bilateral (FR-003): closing our send side must never stop us receiving.
pub fn start(inbox: Inbox, handle: LinkHandle) -> Result(Nil, String) {
  case handle.in_writer {
    None ->
      Error(
        "link_pump.start before the In-stream ingress cursor was wired for "
        <> string.inspect(handle.id),
      )
    Some(_) -> {
      let endpoint = handle.endpoint
      let id = handle.id
      process.spawn(fn() { recv_loop(inbox, id, endpoint) })
      Ok(Nil)
    }
  }
}

/// Pull one frame, decode it, enqueue it, repeat. NEVER touches the heap — the entire
/// reason the pump is a separate process at all.
fn recv_loop(inbox: Inbox, id: LinkId, endpoint: Endpoint) -> Nil {
  case endpoint.recv() {
    // Peer FIN: end the stream and stop. Nothing more can arrive on this link.
    Ok(None) -> process.send(inbox, Closed(id))
    Ok(Some(frame)) ->
      case link_wire.decode_frame(frame) {
        // A frame that will not decode is a wire-contract violation (a non-ground
        // payload, a Fragment with no reassembler, a bad CRC) — a PROTOCOL violation,
        // which the seam classifies `Permanent` (link_fault.gleam), so it refines to
        // `permFail/2`. Report it and STOP: the stream position is no longer
        // trustworthy, so continuing would silently splice the peer's later frames
        // onto a stream that lost one.
        Error(e) ->
          process.send(
            inbox,
            Faulted(
              id,
              link_faults.perm_fail(
                id,
                "undecodable inbound frame: " <> string.inspect(e),
              ),
            ),
          )
        Ok(#(message_id, term)) -> {
          process.send(inbox, Data(id, message_id, term))
          recv_loop(inbox, id, endpoint)
        }
      }
    // A transport-level recv fault: refine the seam signal to its lattice term
    // (`from_signal` is pure data — no heap) and report it.
    Error(signal) -> process.send(inbox, Faulted(id, link_faults.from_signal(signal)))
  }
}

/// Take every item currently buffered in the inbox, in arrival order, without blocking.
/// The runner-side driver calls this once per step; an empty result means nothing has
/// arrived and every suspended `link_recv` stays safely suspended.
pub fn drain(inbox: Inbox) -> List(InboundItem) {
  drain_loop(inbox, [])
}

/// As `drain`, but WAIT up to `timeout_ms` for the first item before giving up (the rest
/// are then taken without blocking). The oracle's `waitForInbound` / the C#
/// `tryApplyNext(Duration wait)`: a driver that has nothing runnable left can park here
/// instead of spinning, since a link with no traffic produces no items at all.
///
/// 🔴 NOT for `step_link`, which uses the non-blocking `drain` — the runner must never
/// block on a peer. This is for an outer driver that has already reached quiescence and
/// is deciding whether to wait for more input or stop.
pub fn drain_wait(inbox: Inbox, timeout_ms: Int) -> List(InboundItem) {
  case process.receive(inbox, timeout_ms) {
    Error(_) -> []
    Ok(item) -> drain_loop(inbox, [item])
  }
}

fn drain_loop(inbox: Inbox, acc: List(InboundItem)) -> List(InboundItem) {
  case process.receive(inbox, 0) {
    Error(_) -> list.reverse(acc)
    Ok(item) -> drain_loop(inbox, [item, ..acc])
  }
}

/// What applying one inbound item did to the runner state.
pub type Applied {
  Applied(heap: Heap, links: LinkRegistry, woken: List(GoalRef))
}

/// Apply ONE inbound item on the RUNNER side (the oracle's `tryApplyNext`), extending the
/// link's `In` stream and returning the goals the binding woke.
///
/// The `Data` case is the design ref's §1.6 B6 worked example verbatim: mint a fresh
/// (writer, reader) pair, cons `[value | fresh_reader]`, bind the CURRENT `in_writer`,
/// then ADVANCE the handle's cursor to the fresh writer. That advance is why ingress is
/// stateful across steps in a way egress is not, and it is threaded through the handle in
/// the registry — the same discipline `link_handle.take_seq` uses for the outbound
/// sequencer — rather than any mutable side-channel.
///
/// `Closed` binds `In` to `[]` (nil) so a consumer reduces its end-of-stream clause, and
/// CLEARS the cursor: the stream is terminated and must never be extended again. A second
/// `Closed`, or data after close, is therefore a no-op rather than a double-bind.
///
/// An item naming a link that is not in the registry is a no-op: the link was torn down
/// (C8) while a frame was already in flight.
///
/// `Faulted` (C7) fans the refined lattice term out to EVERY monitor cursor of the link
/// — the establishment `Faults` stream and each `link_monitor` stream (FR-008) — via
/// `link_faults.deliver_fault`, threading the cursor-advanced handle back through the
/// registry. It deliberately does NOT fail the reader's data goal (FR-044) and does NOT
/// end the `In` stream: whether a fault is terminal is C8 teardown's call, not the
/// pump's.
pub fn apply_item(
  heap: Heap,
  links: LinkRegistry,
  item: InboundItem,
) -> Applied {
  case item {
    Faulted(id, fault) ->
      case link_registry.try_get(links, id) {
        Error(_) -> Applied(heap, links, [])
        Ok(handle) -> {
          let #(heap, handle, woken) =
            link_faults.deliver_fault(heap, handle, fault)
          Applied(heap, link_registry.put(links, handle), woken)
        }
      }
    Data(id, _message_id, value) ->
      case link_registry.try_get(links, id) {
        Error(_) -> Applied(heap, links, [])
        Ok(handle) ->
          case handle.in_writer {
            None -> Applied(heap, links, [])
            Some(cursor) -> {
              let #(heap, fresh_writer, fresh_reader) =
                heap.allocate_variable(heap)
              let cons = terms.cons(value, VarRef(fresh_reader))
              case heap.bind_writer(heap, cursor, cons) {
                // The cursor was already bound — the stream was closed or extended by
                // someone else. Nothing to do, and nothing to hide: the item is dropped
                // rather than double-binding the cell.
                Error(_) -> Applied(heap, links, [])
                Ok(#(heap, woken)) ->
                  Applied(
                    heap,
                    link_registry.put(
                      links,
                      link_handle.advance_in_cursor(handle, fresh_writer),
                    ),
                    woken,
                  )
              }
            }
          }
      }
    Closed(id) ->
      case link_registry.try_get(links, id) {
        Error(_) -> Applied(heap, links, [])
        Ok(handle) ->
          case handle.in_writer {
            None -> Applied(heap, links, [])
            Some(cursor) ->
              case heap.bind_writer(heap, cursor, ConstTerm(ConstAtom("nil"))) {
                Error(_) -> Applied(heap, links, [])
                Ok(#(heap, woken)) ->
                  Applied(
                    heap,
                    link_registry.put(links, link_handle.end_in_stream(handle)),
                    woken,
                  )
              }
          }
      }
  }
}
