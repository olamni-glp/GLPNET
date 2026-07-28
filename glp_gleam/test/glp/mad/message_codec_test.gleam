//// glp/mad/message_codec — Message ↔ wire-frame round-trip (feature 059, T066 slice
//// S1). Every `Message` the Send transaction produces must survive encode→frame→decode
//// back to the exact `#(GlobalName, Term)` a peer's `mad_engine.receive` consumes; a
//// corrupt / non-assignment frame is refused loudly.

import gleeunit/should
import glp/link/primitives/link_wire
import glp/mad/global_name.{ReaderName, WriterName, to_term}
import glp/mad/message.{Message}
import glp/mad/message_codec
import glp/runtime/terms.{ConstAtom, ConstInt, ConstTerm, StructTerm, cons, nil}

fn atom(a: String) {
  ConstTerm(ConstAtom(a))
}

// A ground scalar value assignment round-trips: `_r(p,1) := add`.
pub fn scalar_assignment_round_trips_test() {
  let msg = Message(ReaderName(atom("p"), 1), atom("add"), atom("q"))
  let assert Ok(frame) = message_codec.encode(msg, 0)
  message_codec.decode(frame)
  |> should.equal(Ok(#(ReaderName(atom("p"), 1), atom("add"))))
}

// The forwarded stage-1 assignment from the §10.1 scenario: `_r(p,1) := [add]`.
pub fn list_assignment_round_trips_test() {
  let msg = Message(ReaderName(atom("p"), 1), cons(atom("add"), nil()), atom("q"))
  let assert Ok(frame) = message_codec.encode(msg, 3)
  message_codec.decode(frame)
  |> should.equal(Ok(#(ReaderName(atom("p"), 1), cons(atom("add"), nil()))))
}

// The serializer cold-call wrap `_w(q,0) := [req(_r(p,1)) | _w(q,0)]` — a value that
// itself carries nested global names — round-trips unchanged (they are ground `_w`/`_r`
// structs, ordinary ground data on the wire).
pub fn serializer_coldcall_with_nested_names_round_trips_test() {
  let value =
    cons(
      StructTerm("req", [to_term(ReaderName(atom("p"), 1))]),
      to_term(WriterName(atom("q"), 0)),
    )
  let msg = Message(WriterName(atom("q"), 0), value, atom("q"))
  let assert Ok(frame) = message_codec.encode(msg, 0)
  message_codec.decode(frame)
  |> should.equal(Ok(#(WriterName(atom("q"), 0), value)))
}

// A writer-globalize assignment `_w(bob,2) := hi` (the §10.3 charlie→bob hop) round-trips
// with its polarity intact (writer name, not reader).
pub fn writer_name_polarity_preserved_test() {
  let msg = Message(WriterName(atom("bob"), 2), atom("hi"), atom("bob"))
  let assert Ok(frame) = message_codec.encode(msg, 0)
  message_codec.decode(frame)
  |> should.equal(Ok(#(WriterName(atom("bob"), 2), atom("hi"))))
}

// Integer index and integer value both survive the term codec.
pub fn integer_payload_round_trips_test() {
  let msg = Message(WriterName(atom("p"), 7), ConstTerm(ConstInt(42)), atom("q"))
  let assert Ok(frame) = message_codec.encode(msg, 1)
  message_codec.decode(frame)
  |> should.equal(Ok(#(WriterName(atom("p"), 7), ConstTerm(ConstInt(42)))))
}

// A garbage byte string is not a decodable frame — refused loudly, never a fabricated
// assignment.
pub fn garbage_bytes_are_refused_test() {
  message_codec.decode(<<0xDE, 0xAD, 0xBE, 0xEF>>)
  |> should.be_error
}

// A well-formed frame whose payload is NOT an `_assign(Name, Value)` wrapper is refused
// (a non-distribution frame reaching the distribution decoder is an error, not silently
// reinterpreted).
pub fn non_assign_wrapper_is_refused_test() {
  // Encode a bare (non-wrapped) ground term through the same frame codec, then try to
  // decode it as an assignment.
  let assert Ok(frame) =
    link_wire.encode_term_frame(StructTerm("hello", [atom("world")]), 0)
  message_codec.decode(frame)
  |> should.be_error
}
