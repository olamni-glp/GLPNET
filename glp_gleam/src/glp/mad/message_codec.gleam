//// glp/mad/message_codec — a pending madGLP `Message` ↔ one link wire-frame
//// (feature 059, T066 `close-distribution-engine-sessions`, slice S1).
////
//// The Send transaction (spec §8.2) drains a MadEngine's outgoing set M_p as
//// `List(Message)` — each `Message(name, term, dest)` an assignment `name := term`
//// bound for agent `dest`. To carry that assignment to a peer engine over a REAL link
//// (`dist_session`, slice S2), it must become one self-delimiting wire frame and come
//// back as `#(GlobalName, Term)` for `mad_engine.receive` (spec §8.3).
////
//// The assignment's TWO wire-carried halves — the global name and the globalized value
//// — are wrapped as ONE ground struct `_assign(NameTerm, T↑)` and shipped through the
//// shared ground-relay codec (`link_wire`: `term_codec` TLV inside one `frame_codec`
//// Whole frame). Both halves are GROUND: `global_name.to_term` mints a `_w`/`_r` struct
//// over a ground agent+index, and `_send` globalizes every var of `term` to a ground
//// `_w`/`_r` name (mad_kernels — the "globalize to ground names" gate). So the wrapper
//// is ground and passes `link_egress.serialize_ground` unchanged. The `dest` header is
//// NOT wire-carried — it selects the link (S2), and the receiver IS the destination.
////
//// This is host-side plumbing over the existing transport seam — NOT a language change
//// (no new kernel/guard/directive; self.glp untouched). The wire shape is Gleam→Gleam
//// (both ends this stack); byte-parity with the Dart PayloadSerializer is out of scope
//// (T064 owns term/envelope byte-parity, which `term_codec` already satisfies).

import glp/link/primitives/link_wire
import glp/mad/global_name.{type GlobalName, of_term, to_term}
import glp/mad/message.{type Message, Message}
import glp/runtime/terms.{type Term, StructTerm}

/// The wire wrapper functor for a madGLP assignment. Underscore-led (system-internal,
/// never a resolvent/user term); the receiver splits it back into (name, value).
const assign_functor = "_assign"

/// Encode one pending `Message` as a single wire frame: wrap its (global name, value)
/// as the ground struct `_assign(NameTerm, T↑)` and frame it (`message_id` seeds the
/// frame's sequence — the sender's per-link counter). `Error` names a non-ground value
/// slipping the `_send` globalize gate (surfaced, never a silent malformed frame).
pub fn encode(msg: Message, message_id: Int) -> Result(BitArray, String) {
  let Message(name, term, _dest) = msg
  let wire = StructTerm(assign_functor, [to_term(name), term])
  link_wire.encode_term_frame(wire, message_id)
}

/// Decode one inbound wire frame back to the assignment `#(name, value)` a peer's
/// `mad_engine.receive` consumes. `Error` for an undecodable frame, a wrapper that is
/// not `_assign(Name, Value)`, or a name half that is not a `_w`/`_r` global name —
/// each surfaced loudly (a corrupt distribution frame is never silently absorbed).
pub fn decode(bytes: BitArray) -> Result(#(GlobalName, Term), String) {
  case link_wire.decode_term_frame(bytes) {
    Error(why) -> Error(why)
    Ok(StructTerm(f, [name_t, value])) if f == assign_functor ->
      case of_term(name_t) {
        Ok(name) -> Ok(#(name, value))
        Error(Nil) ->
          Error("distribution frame name is not a _w/_r global name")
      }
    Ok(_) -> Error("distribution frame is not an _assign(Name, Value) wire term")
  }
}
