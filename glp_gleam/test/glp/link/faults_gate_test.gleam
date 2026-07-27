//// T052 — the untrusted-frame ingress gate (`glp/link/faults`).
////
//// FR-015 smoke: every violation class maps to a VALUE (`permFail/2` fault term, or
//// the classified token-abort detail) and NOTHING panics — the systematic adversarial
//// corpus (malformed/truncated/oversized/type-confused sweeps) is T053's separate
//// file; these pin the gate's classification contract and its end-to-end delivery
//// through the pump.

import gleam/bit_array
import gleam/erlang/process
import gleam/option.{None, Some}
import gleam/string
import gleeunit/should
import glp/link/faults
import glp/link/primitives/link_kernels
import glp/link/primitives/link_pump
import glp/link/primitives/link_runtime
import glp/link/primitives/link_wire
import glp/link/reliability/frame_codec
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/link/seam/transport.{Transport}
import glp/runtime/heap
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef,
}

fn an_id() -> LinkId {
  LinkId(
    scheme: link_scheme.tcp(),
    endpoint: link_address.endpoint("127.0.0.1", 9000),
    nonce: NonceInt(1),
  )
}

/// One clean, gate-passing frame carrying `hello(world)` at message id 7.
fn clean_frame() -> BitArray {
  let assert Ok([frame]) =
    link_wire.encode_frames(
      StructTerm("hello", [ConstTerm(ConstAtom("world"))]),
      7,
      None,
    )
  frame
}

/// Replace the byte at `index` with `value` (test-only byte surgery).
fn poke(frame: BitArray, index: Int, value: Int) -> BitArray {
  let assert Ok(before) = bit_array.slice(frame, 0, index)
  let assert Ok(after) =
    bit_array.slice(frame, index + 1, bit_array.byte_size(frame) - index - 1)
  bit_array.concat([before, <<value:int>>, after])
}

fn expect_perm_fail(result: Result(a, Term), stage: String) -> Nil {
  let assert Error(StructTerm("permFail", [_, ConstTerm(ConstAtom(reason))])) =
    result
  string.starts_with(reason, stage) |> should.be_true
}

// ── classification: each violation class → permFail, stage-attributed ────────

pub fn clean_frame_gates_through_test() {
  let assert Ok(#(7, term)) = faults.gate_frame(an_id(), clean_frame())
  term |> should.equal(StructTerm("hello", [ConstTerm(ConstAtom("world"))]))
}

pub fn truncated_header_is_a_framing_fault_test() {
  let assert Ok(stub) = bit_array.slice(clean_frame(), 0, 5)
  expect_perm_fail(faults.gate_frame(an_id(), stub), "frame validation failed")
}

pub fn corrupt_payload_fails_crc_test() {
  // Flip one payload byte (past the fixed header): CRC-32 must catch it BEFORE the
  // term decoder ever sees the chunk.
  let frame = clean_frame()
  let index = bit_array.byte_size(frame) - 1
  let assert Ok(last) = bit_array.slice(frame, index, 1)
  let assert <<b:int>> = last
  let corrupted = poke(frame, index, { b + 1 } % 256)
  expect_perm_fail(faults.gate_frame(an_id(), corrupted), "frame validation failed")
}

pub fn unknown_kind_byte_is_rejected_test() {
  expect_perm_fail(
    faults.gate_frame(an_id(), poke(clean_frame(), 1, 9)),
    "frame validation failed",
  )
}

pub fn forged_oversized_length_is_bounded_test() {
  // Bytes 6..9 are total_length (32-bit big-endian): claim ~4 GiB. The bound check
  // fires on the DECLARED value — no allocation happens first.
  let forged =
    clean_frame() |> poke(6, 255) |> poke(7, 255) |> poke(8, 255) |> poke(9, 255)
  expect_perm_fail(faults.gate_frame(an_id(), forged), "frame validation failed")
}

pub fn type_confused_payload_is_a_decode_fault_test() {
  // A frame that PASSES framing (correct CRC over the chunk) whose chunk is not a
  // term encoding: framing accepts, the term decoder rejects, stage says so.
  let assert Ok([frame]) =
    frame_codec.encode(<<255, 254, 253>>, 3, option.None)
  expect_perm_fail(faults.gate_frame(an_id(), frame), "payload decode failed")
}

pub fn embedded_variable_is_a_ground_relay_fault_test() {
  // A VarRef inside an otherwise-valid encoding cannot be produced by encode_frames
  // (it refuses non-ground), so this violation class is only reachable at DECODE
  // time via a hand-built payload — 038 term_codec tag 0x07 is VarRef.
  let assert Ok([frame]) = frame_codec.encode(<<0x07, 1>>, 4, option.None)
  case faults.gate_frame(an_id(), frame) {
    Error(StructTerm("permFail", [_, ConstTerm(ConstAtom(reason))])) ->
      // Either the codec rejects the tag outright or the ground gate catches the
      // VarRef — both are correct rejections; pin only that it IS rejected with a
      // classified reason.
      { string.starts_with(reason, "payload decode failed")
        || string.starts_with(reason, "ground-relay violation") }
      |> should.be_true
    other -> panic as { "expected permFail, got " <> string.inspect(other) }
  }
}

pub fn stray_fragment_is_rejected_test() {
  // Force fragmentation with a tiny MTU, then present ONE fragment to the base gate.
  let payload = big_payload(<<>>, 64)
  let assert Ok(frames) = frame_codec.encode(payload, 5, option.Some(40))
  let assert [first, ..] = frames
  { frames != [first] } |> should.be_true
  expect_perm_fail(faults.gate_frame(an_id(), first), "fragment received")
}

fn big_payload(acc: BitArray, remaining: Int) -> BitArray {
  case remaining <= 0 {
    True -> acc
    False -> big_payload(bit_array.concat([acc, <<7:int>>]), remaining - 1)
  }
}

// ── token gate: same classes, abort-detail channel ───────────────────────────

pub fn token_gate_classifies_without_a_link_test() {
  let assert Error(detail) = faults.gate_token(<<1, 2, 3>>)
  string.starts_with(detail, "frame validation failed") |> should.be_true
}

// ── end-to-end: a corrupt frame becomes monitor DATA through the pump ────────

/// The full FR-015 path: a peer ships garbage → the pump's gate refines it to
/// `permFail` → `apply_item` fans it onto the establishment `Faults` stream. The
/// engine never crashes and the `In` data stream is untouched.
pub fn corrupt_frame_reaches_the_monitor_as_data_test() {
  let corrupt =
    Transport(
      supported_schemes: [link_scheme.tcp()],
      listen: fn(_s, _a, _o) { Ok(one_shot_garbage_endpoint(an_id())) },
      connect: fn(_s, _a, _o) { Ok(one_shot_garbage_endpoint(an_id())) },
    )
  let state = link_runtime.new() |> link_runtime.with_transport(corrupt)
  let #(h, in_w, in_r) = heap.allocate_variable(heap.new())
  let #(h, _, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, faults_r) = heap.allocate_variable(h)
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_setup", 5, [
      StructTerm("link_id", [
        ConstTerm(ConstAtom("tcp")),
        StructTerm("ep", [
          ConstTerm(ConstString("127.0.0.1")),
          ConstTerm(ConstInt(9000)),
        ]),
        ConstTerm(ConstInt(1)),
      ]),
      ConstTerm(ConstAtom("connector")),
      VarRef(in_w),
      VarRef(out_r),
      VarRef(faults_w),
    ])

  // The pump reads the garbage frame and reports the refined fault.
  let assert [item] = drain_wait_all(state, 100)
  let assert link_pump.Faulted(_, StructTerm("permFail", _)) = item

  // Applying it fans onto the establishment Faults stream; In stays unbound.
  let link_pump.Applied(h, _links, _woken) =
    link_pump.apply_item(h, state.links, item)
  let assert Ok(#(h, heap.Bound(StructTerm(".", [StructTerm("permFail", _), _])))) =
    heap.deref(h, faults_r)
  let assert Ok(#(_, heap.Unbound(_))) = heap.deref(h, in_r)
}

/// One endpoint that serves a single garbage frame then parks (the fault stops the
/// pump loop, so nothing ever reads twice).
fn one_shot_garbage_endpoint(id: LinkId) -> Endpoint {
  Endpoint(
    id: id,
    send: fn(_f) { Ok(Nil) },
    recv: fn() { Ok(Some(<<222, 173, 190, 239>>)) },
    close: fn() { Nil },
    faults: process.new_subject(),
  )
}

fn drain_wait_all(
  state: link_runtime.LinkState,
  budget: Int,
) -> List(link_pump.InboundItem) {
  case link_pump.drain_wait(state.inbox, 100) {
    [] ->
      case budget <= 0 {
        True -> []
        False -> drain_wait_all(state, budget - 1)
      }
    items -> items
  }
}
