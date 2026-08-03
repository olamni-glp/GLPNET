//// glp/link/primitives/link_pump — the inbound delivery path (wave-3 US4,
//// T032; amended contracts/link-handshake.md rules 4/5).
////
//// Port of the `glp_runtime/lib/link/primitives/link_pump.dart` recv-loop
//// discipline over the synchronous Gleam seam: receive one frame → parse
//// (version byte + CRC — a nonconforming frame is REJECTED, rule 2/5) →
//// reassemble fragments (partials never delivered, rule 5) → restore per-link
//// FIFO by the frame's `MessageId` (rule 4) → deliver the in-order payloads.
//// `pump_once` is one turn of the reference's `while` recv loop; the caller
//// (a per-link receive process, or a test) drives it. Closing our own sender
//// never stops receiving — the loop ends only on the PEER's end-of-stream
//// (bilateral links).
////
//// A malformed frame or a reliability-bound violation surfaces as a
//// `PumpFault` carrying the reason — the frame is discarded, never delivered
//// as complete, and the link's monitor observes the fault (FR-024 fan-out is
//// the caller's: the `Endpoint.faults` Subject + this event).

import gleam/option.{None, Some}
import gleam/string
import glp/link/primitives/link_handle.{type LinkHandle, LinkHandle}
import glp/link/reliability/frame_codec
import glp/link/reliability/frame_reassembler
import glp/link/reliability/inbound_ordering
import glp/link/seam/link_fault.{
  type LinkFaultSignal, LinkFaultSignal, Transient,
}

/// One pump turn's outcome.
pub type PumpEvent {
  /// Zero or more payloads became deliverable IN ORDER (zero when the frame
  /// was a buffered fragment / out-of-order / duplicate).
  Delivered(payloads: List(BitArray))
  /// The peer cleanly ended the stream (graceful close upstream).
  PeerClosed
  /// A transport or framing fault — the offending frame was NOT delivered.
  PumpFault(signal: LinkFaultSignal)
}

/// Drive ONE receive on the link: blocks until a frame, end-of-stream, or a
/// transport fault. Returns the advanced handle + what happened.
pub fn pump_once(handle: LinkHandle) -> #(LinkHandle, PumpEvent) {
  case handle.endpoint.recv() {
    Error(signal) -> #(handle, PumpFault(signal))
    Ok(None) -> #(LinkHandle(..handle, closed: True), PeerClosed)
    Ok(Some(frame)) ->
      // Parse: version byte + CRC + structure — a nonconforming frame is
      // rejected here and NEVER delivered (rules 2/5).
      case frame_codec.parse_frame(frame) {
        Error(frame_error) -> #(
          handle,
          PumpFault(LinkFaultSignal(
            handle.id,
            Transient,
            "frame rejected: " <> string.inspect(frame_error),
          )),
        )
        Ok(parsed) ->
          case frame_reassembler.accept(handle.reassembler, parsed) {
            Error(reason) -> #(
              handle,
              PumpFault(LinkFaultSignal(
                handle.id,
                Transient,
                "reassembly rejected: " <> reason,
              )),
            )
            Ok(#(reassembler, maybe_payload)) -> {
              let handle = LinkHandle(..handle, reassembler: reassembler)
              case maybe_payload {
                // Awaiting more fragments — nothing deliverable yet.
                None -> #(handle, Delivered([]))
                Some(payload) ->
                  case
                    inbound_ordering.accept(
                      handle.ordering,
                      parsed.message_id,
                      payload,
                    )
                  {
                    Error(reason) -> #(
                      handle,
                      PumpFault(LinkFaultSignal(
                        handle.id,
                        Transient,
                        "ordering rejected: " <> reason,
                      )),
                    )
                    Ok(#(ordering, deliverable)) -> #(
                      LinkHandle(..handle, ordering: ordering),
                      Delivered(deliverable),
                    )
                  }
              }
            }
          }
      }
  }
}
