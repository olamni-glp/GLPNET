//// glp/link/primitives/link_egress — THE one ground-relay ship path (feature 050,
//// T050.C5).
////
//// Port of `csharp/glp_link/primitives/LinkEgress.cs` (mirror
//// `glp_runtime/lib/link/primitives/link_egress.dart`).
////
//// **Why one module.** 025 risk R-5 in its egress form: there are TWO sender faces —
//// the LinkId face (`out_relay/3` → the `'_link_send'/3` kernel, K2) and the channel
//// face (`link_send/3` conses onto `Out`, drained by the egress drainer). If they each
//// grew their own serialize/frame/sequence code they would drift on the wire. Both come
//// through `ship_ground` here, exactly as the C# oracle keeps one `ShipGround`.
////
//// **Ground-relay, NOT globalize (deviation D-4, RATIFIED).** K2 is a ground-relay
//// sender: `ground_resolve` deep-derefs to a `VarRef`-free tree and FAILS if any cell
//// is unbound. No globalize, no `_w`/`_r` minting, no open structures on the wire
//// (FR-010). Routing the base sender through the madGLP globalize path would silently
//// collapse into the open-structure territory R-3 warns about.
////
//// **Reliability is deliberately partial (deviation D-8).** Sequencing here is the
//// handle's monotone `next_seq` (→ frame message id) and nothing more. Send-window
//// backpressure (FR-025), dedup, reorder and epoch fencing are **T052**; the C# oracle
//// carries the same note against its `SendWindow`. Under the default
//// `max_frame_bytes: None` a term ships as exactly one `Whole` frame, so the base needs
//// no reassembler on the far side either.
////
//// **No payload-codec registry in base scope.** The C# oracle routes bytes through
//// `handle.Codec` so a `"quic"` link can carry an 041 crdtmsg envelope. Base Gleam is
//// loopback/tcp only, where that codec is the identity — the default ground-relay blob
//// byte-for-byte — so the term goes straight through the shipped 038 `term_codec` via
//// `link_wire.encode_frames`, the same path the C4 handshake token uses. A codec
//// registry lands with the schemes that need one, not before.

import gleam/list
import gleam/option
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_terms
import glp/link/primitives/link_wire
import glp/link/seam/link_fault.{type LinkFaultSignal}
import glp/runtime/heap.{type Heap}
import glp/runtime/terms.{type Term}

/// Why an outbound term could not be shipped. Every arm is surfaced to the caller —
/// the kernel face turns it into a non-fatal `LinkAbort`, never a partial wire write.
pub type EgressError {
  /// The ground-relay gate: the term (or something under it) is not ground. The GLP
  /// `ground(Msg?)` guard should have excluded this, so it means an upstream invariant
  /// broke — surfaced, never shipped with a placeholder.
  NotGround(error: link_terms.TermError)
  /// The term could not be serialized or framed (`term_codec` / `frame_codec`).
  Encoding(error: link_wire.WireError)
  /// The transport refused a frame. Carries the frame's index in the fragment list, so
  /// a partial multi-fragment write is visible rather than silently truncated, plus the
  /// seam's own fault signal so C7 can refine it into the `tempFail`/`permFail` vocab.
  Transport(fragment: Int, signal: LinkFaultSignal)
}

/// Resolve `msg` to a `VarRef`-free ground tree, serialize + frame + sequence it, and
/// write every fragment to the link's endpoint IN SEND ORDER (FR-010 ground-relay,
/// per-link FIFO FR-018/053).
///
/// Returns the advanced handle — its sequence number was consumed, so the caller MUST
/// thread it back into the registry (`link_registry.put`) or two terms would ship under
/// the same message id — together with the heap (`ground_resolve` path-compresses).
///
/// The sequence number is taken ONLY after the ground gate and the encode both pass, so
/// a rejected term does not burn a sequence number and leave a hole on the wire.
pub fn ship_ground(
  heap: Heap,
  handle: LinkHandle,
  msg: Term,
) -> Result(#(Heap, LinkHandle), EgressError) {
  // 1. The ground gate. Deep-deref so a ground STRUCT whose args are VarRefs into
  //    bound cells still ships; an unbound cell at any depth fails here, not on the wire.
  case link_terms.ground_resolve(heap, msg) {
    Error(e) -> Error(NotGround(e))
    Ok(#(heap, ground)) -> {
      // 2. Take the sequence number and encode under it. `take_seq` is pure, so on an
      //    encode failure we simply drop the advanced handle and the counter is unmoved.
      let #(advanced, seq) = link_handle.take_seq(handle)
      case link_wire.encode_frames(ground, seq, handle.options.max_frame_bytes) {
        Error(e) -> Error(Encoding(e))
        Ok(frames) ->
          // 3. Ship every fragment in order.
          case send_all(handle, frames, 0) {
            Error(e) -> Error(e)
            Ok(Nil) -> Ok(#(heap, advanced))
          }
      }
    }
  }
}

/// Write each fragment to the endpoint in order, stopping at the first refusal and
/// reporting which fragment failed.
fn send_all(
  handle: LinkHandle,
  frames: List(BitArray),
  index: Int,
) -> Result(Nil, EgressError) {
  case frames {
    [] -> Ok(Nil)
    [frame, ..rest] ->
      case handle.endpoint.send(frame) {
        Error(sig) -> Error(Transport(index, sig))
        Ok(Nil) -> send_all(handle, rest, index + 1)
      }
  }
}

/// How many fragments `msg` would ship as under this handle's MTU, without sending
/// anything or consuming a sequence number. Used by the C5 tests to assert the base
/// single-`Whole`-frame shape (D-8: no reassembler in base scope).
pub fn fragment_count(
  heap: Heap,
  handle: LinkHandle,
  msg: Term,
) -> Result(Int, EgressError) {
  case link_terms.ground_resolve(heap, msg) {
    Error(e) -> Error(NotGround(e))
    Ok(#(_heap, ground)) ->
      case
        link_wire.encode_frames(ground, 0, handle.options.max_frame_bytes)
      {
        Error(e) -> Error(Encoding(e))
        Ok(frames) -> Ok(list.length(frames))
      }
  }
}

/// The link's MTU, or `None` for the default single-frame shape. Exposed so the kernel
/// arm can report it in a diagnostic without reaching into `options`.
pub fn mtu(handle: LinkHandle) -> option.Option(Int) {
  handle.options.max_frame_bytes
}
