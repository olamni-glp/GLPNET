//// Round-trip + no-heap + loud-fail tests for the result-envelope codec (feature 038).
////
//// The corpus SHAPES mirror the Dart reference corpus
//// (glp_runtime/test/codec/corpus.dart). Dart is the byte source of truth (R9); the
//// Gleam encodings here are byte-for-byte identical to the Dart encoder — anchored by
//// explicit golden byte vectors (golden_*_test) hand-derived from the Dart layout.
////
//// SC-001 round-trip: decode(encode(R)) == Ok(R), field-by-field (incl. captured).
//// SC-003 no-heap: a remaining variable is a VarRef carrying a GlobalVarId
////   (agent_id:local_id) — never a raw heap address.
//// SC-004 loud-fail: bad version/payloadType/status/errorPresent, unknown tag,
////   trailing bytes, truncation, and over-64-bit varints all reject.

import gleam/bit_array
import gleam/list
import gleam/option.{None, Some}
import gleeunit/should
import glp/codec/result_envelope.{
  type ResultEnvelope, Failed, ResultEnvelope, Success, Suspended, decode, encode,
}
import glp/codec/term_codec.{
  type Term, BadErrorPresent, BadPayloadType, BadStatus, BadVersion, ConstAtom,
  ConstInt, ConstString, ConstTerm, GlobalVarId, StructTerm, TrailingBytes,
  UnknownTag, VarRef, VarintTooLong,
}

// --- term builders mirroring corpus.dart -----------------------------------

fn atom(a: String) -> Term {
  ConstTerm(ConstAtom(a))
}

fn int_(n: Int) -> Term {
  ConstTerm(ConstInt(n))
}

fn str(s: String) -> Term {
  ConstTerm(ConstString(s))
}

fn var_ref(agent: String, local: Int) -> Term {
  VarRef(GlobalVarId(agent, local))
}

/// `[a,b,c]` == `.(a, .(b, .(c, nil)))` (034 cons/nil convention).
fn glp_list(elems: List(Term)) -> Term {
  list.fold_right(elems, atom("nil"), fn(acc, e) { StructTerm(".", [e, acc]) })
}

/// A success envelope carrying only bindings (no var→writer / suspended / captured / error).
fn success(bindings: List(#(String, Term))) -> ResultEnvelope {
  ResultEnvelope(Success, bindings, [], [], <<>>, None)
}

// --- the non-gated in-scope corpus (1:1 with corpus.dart shapes) -----------

fn corpus() -> List(#(String, ResultEnvelope)) {
  [
    #("empty_success", ResultEnvelope(Success, [], [], [], <<>>, None)),
    #("success_atom", success([#("X", atom("foo"))])),
    #("success_int", success([#("N", int_(42))])),
    #("success_int_negative", success([#("N", int_(-7))])),
    #("success_string", success([#("S", str("hello, world"))])),
    #(
      "success_nested_struct",
      success([#("T", StructTerm("point", [int_(1), int_(2)]))]),
    ),
    #("success_list", success([#("L", glp_list([int_(1), int_(2), int_(3)]))])),
    #(
      "success_deep_nested",
      success([
        #(
          "Tree",
          StructTerm("node", [
            StructTerm("node", [atom("leaf"), int_(1)]),
            StructTerm("node", [atom("leaf"), int_(2)]),
          ]),
        ),
      ]),
    ),
    #(
      "multi_binding",
      success([
        #("A", atom("alpha")),
        #("B", int_(2)),
        #("C", str("gamma")),
      ]),
    ),
    #(
      "var_to_writer",
      ResultEnvelope(
        Success,
        [#("Z", StructTerm("pair", [atom("a"), var_ref("agent1", 9)]))],
        [#("W", GlobalVarId("agent1", 9))],
        [],
        <<>>,
        None,
      ),
    ),
    #(
      "suspended",
      ResultEnvelope(
        Suspended,
        [],
        [],
        [GlobalVarId("agent1", 3), GlobalVarId("agent2", 5)],
        <<>>,
        None,
      ),
    ),
    #(
      "suspended_with_binding",
      ResultEnvelope(
        Suspended,
        [#("Partial", StructTerm("waiting_on", [var_ref("agent1", 11)]))],
        [#("Q", GlobalVarId("agent1", 11))],
        [GlobalVarId("agent1", 11)],
        <<>>,
        None,
      ),
    ),
    #(
      "failed",
      ResultEnvelope(
        Failed,
        [],
        [],
        [],
        <<>>,
        Some("no matching clause for goal foo/2"),
      ),
    ),
    #(
      "with_captured",
      ResultEnvelope(
        Success,
        [#("R", atom("done"))],
        [],
        [],
        bit_array.from_string("line one\nline two\n"),
        None,
      ),
    ),
  ]
}

// --- SC-001: round-trip every corpus shape ---------------------------------

pub fn corpus_roundtrip_test() {
  list.each(corpus(), fn(entry) {
    let #(_name, env) = entry
    decode(encode(env)) |> should.equal(Ok(env))
  })
}

// --- SC-003: a remaining variable carries a GlobalVarId, never a heap addr --

pub fn no_heap_var_ref_test() {
  let env =
    ResultEnvelope(
      Success,
      [#("Z", var_ref("agent7", 42))],
      [#("W", GlobalVarId("agent7", 42))],
      [],
      <<>>,
      None,
    )
  let assert Ok(decoded) = decode(encode(env))
  // The decoded binding is a VarRef wrapping a GlobalVarId(agent_id, local_id) —
  // there is no Int-addr VarRef variant reachable in the envelope at all (SC-003).
  let assert ResultEnvelope(
    _,
    [#("Z", VarRef(GlobalVarId(agent, local)))],
    [#("W", GlobalVarId(w_agent, w_local))],
    _,
    _,
    _,
  ) = decoded
  agent |> should.equal("agent7")
  local |> should.equal(42)
  w_agent |> should.equal("agent7")
  w_local |> should.equal(42)
}

// --- byte-layout anchors: exact Dart bytes (proves byte-for-byte parity) ----

pub fn golden_empty_success_test() {
  // version, payloadType, status=success, bindings=0, vtw=0, susp=0, capLen=0, errPresent=0
  encode(ResultEnvelope(Success, [], [], [], <<>>, None))
  |> should.equal(<<0x01, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00>>)
}

pub fn golden_success_int_test() {
  encode(success([#("N", int_(42))]))
  |> should.equal(<<
    0x01, 0x11, 0x00,
    // version, payloadType, status
    0x01,
    // bindingsCount = 1
    0x01, 0x4E,
    // name "N" (len 1, 'N')
    0x02, 0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    // tag int64 + 42 little-endian
    0x00,
    // varToWriterCount
    0x00,
    // suspendedCount
    0x00,
    // capturedLen
    0x00,
    // errorPresent = 0
  >>)
}

pub fn golden_success_int_negative_test() {
  // -7 as two's-complement 64-bit little-endian == F9 FF FF FF FF FF FF FF
  encode(success([#("N", int_(-7))]))
  |> should.equal(<<
    0x01, 0x11, 0x00, 0x01, 0x01, 0x4E,
    0x02, 0xF9, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
    0x00, 0x00, 0x00, 0x00,
  >>)
}

// --- SC-004: loud-fail on every malformed input ----------------------------

pub fn reject_bad_version_test() {
  decode(<<0x02, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00>>)
  |> should.equal(Error(BadVersion(0x02)))
}

pub fn reject_bad_payload_type_test() {
  decode(<<0x01, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00>>)
  |> should.equal(Error(BadPayloadType(0x10)))
}

pub fn reject_bad_status_test() {
  decode(<<0x01, 0x11, 0x07>>)
  |> should.equal(Error(BadStatus(0x07)))
}

pub fn reject_bad_error_present_test() {
  decode(<<0x01, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05>>)
  |> should.equal(Error(BadErrorPresent(0x05)))
}

pub fn reject_unknown_term_tag_test() {
  // bindings=1, name "X" (len 1, 'X'=0x58), then term tag 0x09 (unknown)
  decode(<<0x01, 0x11, 0x00, 0x01, 0x01, 0x58, 0x09>>)
  |> should.equal(Error(UnknownTag(0x09)))
}

pub fn reject_reserved_null_tag_test() {
  // 0x00 is 029-reserved (null) — no GLP-term representation ⇒ loud-fail
  decode(<<0x01, 0x11, 0x00, 0x01, 0x01, 0x58, 0x00>>)
  |> should.equal(Error(UnknownTag(0x00)))
}

pub fn reject_trailing_bytes_test() {
  decode(<<0x01, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF>>)
  |> should.equal(Error(TrailingBytes(1)))
}

pub fn reject_truncated_test() {
  // just the version byte: reading payloadType hits end-of-input
  decode(<<0x01>>) |> should.be_error
}

pub fn reject_varint_too_long_test() {
  // bindingsCount as 10 continuation bytes (>64 bits) ⇒ VarintTooLong
  decode(<<
    0x01, 0x11, 0x00, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
  >>)
  |> should.equal(Error(VarintTooLong))
}
