//// T039/T040 AtomVM gated-corpus conformance probe.
////
//// Runs the REAL feature-038 term codec (`glp/codec/term_codec`) on **AtomVM** — an
//// independent BEAM-alternative VM — and displays the encoded bytes + the decoded
//// round-trip for the GATED entries: the float `0x03` (Pi) and the 64-bit-int edges
//// (`long` max / min). This is the AtomVM-faithfulness signal the spec requires: a plain
//// `gleam test` runs on the full Erlang/OTP runtime, NOT AtomVM, so it is NOT an
//// AtomVM-faithfulness signal (R11 / FR-011, T039/T040). Gated entries stay quarantined
//// (NOT byte-final, R11/R6) regardless — this probe records the AtomVM result.
////
//// Run via the Node AtomVM wrapper (no browser, no distro):
////   node <AtomVM.mjs> atomvm_gated_probe.beam glp@codec@term_codec.beam \
////        gleam@int.beam gleam@bit_array.beam gleam@result.beam gleam_stdlib.beam ...
//// AtomVM selects the module exporting `start/0` (this one) as the entry point.

import glp/codec/term_codec.{
  type Term, ConstInt, ConstReal, ConstTerm, decode_term, encode_term,
}

@external(erlang, "erlang", "display")
fn display(x: a) -> Bool

fn probe(term: Term) -> Nil {
  let bytes = encode_term(term)
  let _ = display(bytes)
  // round-trip: decode must return Ok(#(term, <<>>)) — i.e. the original + no residue
  let _ = display(decode_term(bytes) == Ok(#(term, <<>>)))
  Nil
}

pub fn start() -> Nil {
  // T040 — 64-bit-int edges (BEAM/AtomVM bignum masked to 64-bit two's-complement LE)
  probe(ConstTerm(ConstInt(9_223_372_036_854_775_807)))
  probe(ConstTerm(ConstInt(-9_223_372_036_854_775_808)))
  // T039 — IEEE-754 double (the ED-6 AtomVM /float spike)
  probe(ConstTerm(ConstReal(3.141592653589793)))
  Nil
}
