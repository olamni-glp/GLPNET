//// Split-protocol wire-codec tests (feature 064, T026; glp/split/wire).
////
//// Byte parity with the normative C# codec (csharp/glp_split_protocol/
//// RequestResponseCodec.cs): the payload layout is pinned against hand-built
//// expected bytes (the exact bytes the C# writer emits), round-trips are
//// exercised at both the payload and the FrameCodec-frame level, and every
//// loud-fail rejection the C# codec throws on is rejected here too — short
//// payload, unknown payload type, unknown kind, length underrun/overrun,
//// a body on a body-less kind, and an empty body on an IL kind.

import gleam/bit_array
import gleeunit/should
import glp/split/wire

// ── byte layout (C# writer parity) ───────────────────────────────────────────

// RequestFrame(1, RUN_GOAL, "a.") →
//   [0x40][request_id=1 u64 BE][0x02][body_len=2 u32 BE]["a."]
pub fn request_payload_byte_layout_test() {
  wire.encode_request_payload(wire.request_text(1, wire.RunGoal, "a."))
  |> should.equal(<<
    0x40, 0, 0, 0, 0, 0, 0, 0, 1, 0x02, 0, 0, 0, 2, 0x61, 0x2E,
  >>)
}

// ResponseFrame(7, ACK, "pong") →
//   [0x41][request_id=7 u64 BE][0x82][body_len=4 u32 BE]["pong"]
pub fn response_payload_byte_layout_test() {
  wire.encode_response_payload(wire.response_text(7, wire.Ack, "pong"))
  |> should.equal(<<
    0x41, 0, 0, 0, 0, 0, 0, 0, 7, 0x82, 0, 0, 0, 4, 0x70, 0x6F, 0x6E, 0x67,
  >>)
}

// A body-less STATUS request is exactly the 14-byte header.
pub fn empty_body_request_is_header_only_test() {
  let payload = wire.encode_request_payload(wire.request_empty(2, wire.Status))
  bit_array.byte_size(payload) |> should.equal(14)
}

// ── round trips (payload + frame level) ──────────────────────────────────────

pub fn request_frame_round_trip_test() {
  let request = wire.request_text(42, wire.LoadSource, "p(a).")
  let assert Ok(frame) = wire.encode_request_frame(request, 9)
  wire.decode_request_frame(frame) |> should.equal(Ok(request))
}

pub fn response_frame_round_trip_test() {
  let response = wire.ResponseFrame(42, wire.ResultKind, <<1, 2, 3>>)
  let assert Ok(frame) = wire.encode_response_frame(response, 10)
  wire.decode_response_frame(frame) |> should.equal(Ok(response))
}

// Every request kind survives its round trip (IL kinds carry a stand-in body —
// they may not be EMPTY on the wire).
pub fn all_request_kinds_round_trip_test() {
  [
    wire.request_text(1, wire.LoadSource, "x"),
    wire.request_text(2, wire.RunGoal, "g"),
    wire.request_empty(3, wire.Snapshot),
    wire.request_empty(4, wire.Status),
    wire.request_empty(5, wire.Shutdown),
    wire.request_empty(6, wire.Ping),
    wire.RequestFrame(7, wire.LoadIl, <<0xAA>>),
    wire.RequestFrame(8, wire.RunGoalIl, <<0xBB>>),
  ]
  |> list_each_round_trip
}

fn list_each_round_trip(requests: List(wire.RequestFrame)) -> Nil {
  case requests {
    [] -> Nil
    [request, ..rest] -> {
      wire.decode_request_payload(wire.encode_request_payload(request))
      |> should.equal(Ok(request))
      list_each_round_trip(rest)
    }
  }
}

// ── loud-fail discipline (wire rule 3 / 038 convention) ──────────────────────

pub fn short_payload_is_rejected_test() {
  let assert Error(wire.SplitError(_)) =
    wire.decode_request_payload(<<0x40, 0, 0>>)
}

pub fn unknown_payload_type_is_rejected_test() {
  let assert Error(wire.SplitError(_)) =
    wire.decode_request_payload(<<
      0x50, 0, 0, 0, 0, 0, 0, 0, 1, 0x02, 0, 0, 0, 0,
    >>)
}

pub fn unknown_request_kind_is_rejected_test() {
  // Kind 0x09 is beyond RUN_GOAL_IL — refused, never silently accepted.
  let assert Error(wire.SplitError(_)) =
    wire.decode_request_payload(<<
      0x40, 0, 0, 0, 0, 0, 0, 0, 1, 0x09, 0, 0, 0, 0,
    >>)
}

pub fn unknown_response_kind_is_rejected_test() {
  let assert Error(wire.SplitError(_)) =
    wire.decode_response_payload(<<
      0x41, 0, 0, 0, 0, 0, 0, 0, 1, 0x87, 0, 0, 0, 0,
    >>)
}

// A REQUEST decoded as a response (and vice versa) is a type violation.
pub fn request_as_response_is_rejected_test() {
  let payload = wire.encode_request_payload(wire.request_empty(1, wire.Ping))
  let assert Error(wire.SplitError(_)) = wire.decode_response_payload(payload)
}

pub fn body_length_underrun_is_rejected_test() {
  // Declared body_len 5, only 2 bytes present.
  let assert Error(wire.SplitError(_)) =
    wire.decode_request_payload(<<
      0x40, 0, 0, 0, 0, 0, 0, 0, 1, 0x02, 0, 0, 0, 5, 0x61, 0x62,
    >>)
}

pub fn trailing_bytes_are_rejected_test() {
  // Declared body_len 1, two body bytes present (038 convention).
  let assert Error(wire.SplitError(_)) =
    wire.decode_request_payload(<<
      0x40, 0, 0, 0, 0, 0, 0, 0, 1, 0x02, 0, 0, 0, 1, 0x61, 0x62,
    >>)
}

// Wire contract "bodies empty unless noted": a body on PING is malformed.
pub fn body_on_bodyless_kind_is_rejected_test() {
  let assert Error(wire.SplitError(_)) =
    wire.decode_request_payload(
      wire.encode_request_payload(wire.request_text(1, wire.Ping, "x")),
    )
}

// contracts/il-request-kind.md: an IL request without a body is malformed.
pub fn empty_body_on_il_kind_is_rejected_test() {
  let assert Error(wire.SplitError(_)) =
    wire.decode_request_payload(
      wire.encode_request_payload(wire.request_empty(1, wire.LoadIl)),
    )
}

// A non-FrameCodec blob at the frame level fails in the FrameCodec layer.
pub fn bad_frame_is_rejected_test() {
  let assert Error(wire.SplitError(_)) = wire.decode_request_frame(<<0xFF>>)
}
