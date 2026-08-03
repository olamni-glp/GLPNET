//// glp/link/primitives/link_egress — the outbound ship path (wave-3 US4;
//// FR-021 send-order + backpressure).
////
//// Port of the `glp_runtime/lib/link/primitives/link_egress.dart` ship core
//// over the reliability bundle: acquire one backpressure credit, assign the
//// per-link monotone sequence (→ frame `MessageId`, the peer's reorder/dedup
//// key), encode into version-stamped CRC frames (fragmenting under the
//// options' MTU), and send each frame on the endpoint in order. A full window
//// is the SUSPENSION signal (`EgressWouldBlock`) — the producer parks, never
//// an unbounded buffer. A transport fault mid-ship surfaces as the fault; the
//// credit releases once the frames are accepted by the transport (the
//// synchronous seam's acceptance = consumption).

import gleam/list
import gleam/string
import glp/link/primitives/link_handle.{type LinkHandle, LinkHandle}
import glp/link/reliability/frame_codec
import glp/link/reliability/link_sequencer
import glp/link/reliability/send_window
import glp/link/seam/link_fault.{type LinkFaultSignal}

pub type EgressError {
  /// The backpressure window is full: the producer must suspend/retry after
  /// the peer consumes (FCP producer suspension, never an unbounded queue).
  EgressWouldBlock
  /// The payload could not be framed (over-size / bad MTU) — loud, not silent.
  EgressEncodeError(detail: String)
  /// The transport refused a frame (peer closed / transport fault).
  EgressFault(signal: LinkFaultSignal)
}

/// Ship one opaque payload blob on the link, in submission order. Returns the
/// advanced handle (sequence + window state) on success.
pub fn ship(
  handle: LinkHandle,
  payload: BitArray,
) -> Result(LinkHandle, EgressError) {
  case send_window.try_acquire(handle.window) {
    Error(_) -> Error(EgressWouldBlock)
    Ok(window) -> {
      let #(sequencer, message_id) = link_sequencer.next(handle.sequencer)
      case
        frame_codec.encode(payload, message_id, handle.options.max_frame_bytes)
      {
        Error(frame_error) ->
          Error(EgressEncodeError(string.inspect(frame_error)))
        Ok(frames) ->
          case
            list.try_each(frames, fn(frame) { handle.endpoint.send(frame) })
          {
            Error(signal) -> Error(EgressFault(signal))
            Ok(Nil) -> {
              // Frames accepted by the (synchronous) transport = consumed:
              // release the credit. Over-release is impossible here (we hold
              // the credit we just acquired).
              let window = case send_window.release(window) {
                Ok(w) -> w
                Error(_) -> window
              }
              Ok(
                LinkHandle(..handle, sequencer: sequencer, window: window),
              )
            }
          }
      }
    }
  }
}
