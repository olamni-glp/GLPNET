//// glp/mad/global_name tests (feature 050 T050.A0) — `_w(p,i)`/`_r(p,i)` term
//// round-trip + polarity. The `_w`/`_r` struct shape is what body_kernels.dart
//// `'_send'` requires (spec §2/§15).

import gleeunit/should
import glp/mad/global_name.{ReaderName, WriterName}
import glp/runtime/terms.{ConstAtom, ConstInt, ConstTerm, StructTerm}

fn alice() {
  ConstTerm(ConstAtom("alice"))
}

pub fn writer_to_term_test() {
  global_name.to_term(WriterName(alice(), 3))
  |> should.equal(StructTerm("_w", [alice(), ConstTerm(ConstInt(3))]))
}

pub fn reader_to_term_test() {
  global_name.to_term(ReaderName(alice(), 1))
  |> should.equal(StructTerm("_r", [alice(), ConstTerm(ConstInt(1))]))
}

pub fn writer_round_trip_test() {
  let n = WriterName(alice(), 7)
  global_name.of_term(global_name.to_term(n))
  |> should.equal(Ok(n))
}

pub fn reader_round_trip_test() {
  // agent may itself be a non-atom ground term (spec §2 AgentId ::= ... ; Integer).
  let n = ReaderName(ConstTerm(ConstInt(42)), 2)
  global_name.of_term(global_name.to_term(n))
  |> should.equal(Ok(n))
}

pub fn of_term_rejects_non_global_test() {
  // wrong functor
  global_name.of_term(StructTerm("foo", [alice(), ConstTerm(ConstInt(1))]))
  |> should.equal(Error(Nil))
  // bare atom, not a struct
  global_name.of_term(ConstTerm(ConstAtom("_w")))
  |> should.equal(Error(Nil))
  // non-integer index
  global_name.of_term(StructTerm("_w", [alice(), alice()]))
  |> should.equal(Error(Nil))
}

pub fn polarity_test() {
  global_name.is_writer(WriterName(alice(), 1))
  |> should.be_true
  global_name.is_writer(ReaderName(alice(), 1))
  |> should.be_false
}
