//// T038 loud-fail fuzz (SC-004, V4): trailing/garbage bytes, unknown term tags, corrupt
//// version/payloadType/status/errorPresent, and EVERY truncation of a valid encoding MUST
//// be rejected — asserts ZERO silent acceptances. (Gleam decode returns Result, so a
//// silent acceptance is a stray `Ok`, never a thrown error.)

import gleam/bit_array
import gleam/list
import gleam/option.{None, Some}
import gleeunit/should
import glp/codec/result_envelope.{
  type ResultEnvelope, Failed, ResultEnvelope, Success, Suspended, decode, encode,
}
import glp/codec/term_codec.{ConstAtom, ConstInt, ConstTerm, GlobalVarId}

fn rejects(b: BitArray) -> Bool {
  case decode(b) {
    Error(_) -> True
    Ok(_) -> False
  }
}

fn corpus() -> List(ResultEnvelope) {
  [
    ResultEnvelope(Success, [], [], [], <<>>, None),
    ResultEnvelope(Success, [#("N", ConstTerm(ConstInt(42)))], [], [], <<>>, None),
    ResultEnvelope(Success, [#("X", ConstTerm(ConstAtom("foo")))], [], [], <<>>, None),
    ResultEnvelope(Suspended, [], [], [GlobalVarId("a", 3)], <<>>, None),
    ResultEnvelope(Failed, [], [], [], <<>>, Some("boom")),
  ]
}

// Every strict byte-prefix of `b`, lengths n-1 .. 1.
fn prefixes(b: BitArray, k: Int, acc: List(BitArray)) -> List(BitArray) {
  case k <= 0 {
    True -> acc
    False -> {
      let acc2 = case bit_array.slice(b, 0, k) {
        Ok(p) -> [p, ..acc]
        Error(_) -> acc
      }
      prefixes(b, k - 1, acc2)
    }
  }
}

fn with_byte(b: BitArray, i: Int, v: Int) -> BitArray {
  let n = bit_array.byte_size(b)
  let assert Ok(pre) = bit_array.slice(b, 0, i)
  let assert Ok(post) = bit_array.slice(b, i + 1, n - i - 1)
  <<pre:bits, v, post:bits>>
}

pub fn t038_trailing_and_truncations_reject_test() {
  let bad =
    corpus()
    |> list.flat_map(fn(env) {
      let valid = encode(env)
      rejects(valid) |> should.equal(False)
      // the valid encoding must decode
      let trailing = [<<valid:bits, 0xFF>>, <<valid:bits, 0x00, 0x01>>]
      list.append(trailing, prefixes(valid, bit_array.byte_size(valid) - 1, []))
    })
  list.filter(bad, fn(b) { rejects(b) == False }) |> should.equal([])
}

pub fn t038_corrupt_header_bytes_reject_test() {
  // empty_success: [ver, ptype, status, 0,0,0,0, errPresent].
  let base = encode(ResultEnvelope(Success, [], [], [], <<>>, None))
  let cases =
    list.flatten([
      list.map([0x00, 0x02, 0x10, 0xFF], fn(v) { with_byte(base, 0, v) }),
      list.map([0x00, 0x10, 0x12, 0xFF], fn(p) { with_byte(base, 1, p) }),
      list.map([0x03, 0x04, 0xFF], fn(s) { with_byte(base, 2, s) }),
      list.map([0x02, 0x05, 0xFF], fn(e) { with_byte(base, 7, e) }),
    ])
  list.filter(cases, fn(b) { rejects(b) == False }) |> should.equal([])
}

pub fn t038_unknown_term_tags_reject_test() {
  // success_atom: the term tag is at byte index 6 (0x05 atom).
  let base =
    encode(ResultEnvelope(
      Success,
      [#("X", ConstTerm(ConstAtom("foo")))],
      [],
      [],
      <<>>,
      None,
    ))
  let cases =
    list.map([0x00, 0x08, 0x09, 0x20, 0xFF], fn(t) { with_byte(base, 6, t) })
  list.filter(cases, fn(b) { rejects(b) == False }) |> should.equal([])
}
