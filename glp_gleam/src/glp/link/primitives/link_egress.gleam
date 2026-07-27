//// glp/link/primitives/link_egress — the ground-relay egress ship pipeline
//// (feature 059, T076 — port of glp_runtime/lib/link/primitives/link_egress.dart,
//// mirror csharp/glp_link/primitives/LinkEgress.cs).
////
//// The ONE ground-relay ship path shared by BOTH sender faces (T031): the channel
//// face (`link_send/3` → the `Out`-stream egress drainer) and the LinkId face
//// (`out_relay/3` → the `_link_send/3` kernel). Keeping a single ship path closes
//// risk R-5 (two egress paths diverging): both resolve a GROUND copy (FR-010),
//// serialize it (the 038 TLV term blob — `glp/codec/term_codec`), frame + sequence
//// it (`frame_codec`, FR-018/053), and hand the fragments to the endpoint in send
//// order.
////
//// GROUND-RELAY GATE (FR-010): `resolve_ground` deep-derefs the term and FAILS
//// (`Error`) on any unbound cell at any depth — an `_w`/`_r`/embedded reader must
//// NEVER cross the wire. Only ground terms serialize.
////
//// GLEAM MAPPING NOTE: the Dart `shipGround` performs the async `endpoint.sendBytes`
//// per fragment (gated on the deferred connect). Here `ship_ground` is SYNCHRONOUS
//// and PURE: it returns the frames + the seq-advanced handle; the (T074) link driver
//// pumps them to `endpoint.send`. This keeps the gate + framing + monotone
//// sequencing (the T076 primitives) testable without a live endpoint. Backpressure
//// (the send window, FR-025) lands with T077, exactly as the Dart source defers it.

import gleam/int
import gleam/list
import gleam/result
import gleam/string
import glp/codec/term_codec
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_terms
import glp/link/reliability/frame_codec
import glp/runtime/heap.{type Heap}
import glp/runtime/terms.{
  type Term, ConstTerm, StructTerm, VarRef,
}

/// Resolve `msg` to a ground (VarRef-free) term against `heap`, serialize it to the
/// TLV payload blob, frame + sequence it, and return the wire frames with the
/// seq-advanced handle. `Error(String)` if the term is not ground (the ground-relay
/// gate — an unbound reader/writer reached the wire) or framing rejects it.
pub fn ship_ground(
  heap: Heap,
  handle: LinkHandle,
  msg: Term,
) -> Result(#(LinkHandle, List(BitArray)), String) {
  // Ground gate + deep resolve (link_terms.ground_resolve throws on any unbound
  // cell at any depth — never a placeholder on the wire).
  use ground <- result.try(link_terms.ground_resolve(heap, msg))
  use payload <- result.try(serialize_ground(ground))

  let #(handle, seq) = link_handle.next_seq(handle)
  case frame_codec.encode(payload, seq, handle.options.max_frame_bytes) {
    Ok(frames) -> Ok(#(handle, frames))
    Error(e) -> Error("egress framing rejected: " <> string.inspect(e))
  }
}

/// Serialize an already-ground `terms.Term` to the TLV payload blob via the shared
/// `term_codec` (the format `frame_codec` wraps). Maps the runtime term model onto
/// the codec term model; a `VarRef` at any depth is the ground-relay gate violation
/// (`Error`), never encoded.
pub fn serialize_ground(ground: Term) -> Result(BitArray, String) {
  use codec_term <- result.try(to_codec_term(ground))
  Ok(term_codec.encode_term(codec_term))
}

/// Map a GROUND `terms.Term` onto the `term_codec.Term` wire model. A `VarRef`
/// (an unbound cell) is the ground-relay gate violation.
fn to_codec_term(t: Term) -> Result(term_codec.Term, String) {
  case t {
    ConstTerm(c) -> Ok(term_codec.ConstTerm(to_codec_const(c)))
    StructTerm(functor, args) -> {
      use mapped <- result.try(list.try_map(args, to_codec_term))
      Ok(term_codec.StructTerm(functor, mapped))
    }
    VarRef(addr) ->
      Error(
        "non-ground term reached egress: unbound cell "
        <> int.to_string(addr)
        <> " (ground-relay gate)",
      )
  }
}

fn to_codec_const(c: terms.Constant) -> term_codec.Constant {
  case c {
    terms.ConstAtom(s) -> term_codec.ConstAtom(s)
    terms.ConstInt(v) -> term_codec.ConstInt(v)
    terms.ConstReal(v) -> term_codec.ConstReal(v)
    terms.ConstString(s) -> term_codec.ConstString(s)
  }
}
