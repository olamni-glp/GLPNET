//// glp/split/wire — the FE/BE split-protocol wire codec (feature 064, T026;
//// contracts/febe-split.md "single seam definition").
////
//// Byte-parity port of `csharp/glp_split_protocol/WireProtocol.cs` +
//// `RequestResponseCodec.cs` (the C# code is normative). One split-protocol
//// payload rides inside ONE FrameCodec `Whole` frame (loopback MVP never
//// fragments); the payload layout (big-endian, C# house style) is:
////
////   [0]       payload_type  (0x40 REQUEST / 0x41 RESPONSE)
////   [1..9)    request_id    u64 BE (client-monotonic, echoed verbatim)
////   [9]       kind          u8
////   [10..14)  body_len      u32 BE
////   [14..)    body          body_len bytes
////
//// Loud-fail discipline (wire rule 3 / 038 convention): unknown payload type,
//// unknown kind, body-length overrun/underrun, trailing bytes, a body on a
//// body-less kind, and an empty body on an IL kind are all `Error(SplitError)`
//// — never a silent mis-decode. Checks run in the same order as the C# codec so
//// identical input is accepted/rejected identically across runtimes.
////
//// The IL kinds (LOAD_IL 0x07 / RUN_GOAL_IL 0x08, IL_REFUSED 0x86) are decoded
//// here (the wire table is the C# one, verbatim) but the Gleam BE does not
//// serve them — it refuses loudly with PROTOCOL_ERROR (T026 text-kind scope).

import gleam/bit_array
import gleam/int
import gleam/option
import gleam/string
import glp/link/reliability/frame_codec

/// Loud-fail error for any malformed/unknown split-protocol frame (the C#
/// `SplitProtocolException`, as data).
pub type SplitError {
  SplitError(message: String)
}

/// Payload-type discriminants (C# `SplitPayloadType`): 0x40 block, disjoint
/// from the glp_wire_registry 0x10–0x12 table.
pub const payload_type_request = 0x40

pub const payload_type_response = 0x41

/// Request kinds (client→engine; C# `RequestKind`, wire-stable bytes).
pub type RequestKind {
  /// 0x01 — UTF-8 program source; the engine runs the full pipeline (FR-001).
  LoadSource
  /// 0x02 — UTF-8 goal text; one goal per request.
  RunGoal
  /// 0x03 — on-demand snapshot trigger; empty body.
  Snapshot
  /// 0x04 — engine state summary; empty body.
  Status
  /// 0x05 — graceful shutdown; empty body.
  Shutdown
  /// 0x06 — supervisor liveness probe; empty body (wire rule 7).
  Ping
  /// 0x07 — 064 US3 IL envelope bytes (opaque here; the Gleam BE refuses it).
  LoadIl
  /// 0x08 — 064 US3 goal_ref (+ optional inline envelope; refused by this BE).
  RunGoalIl
}

/// Response kinds (engine→client; C# `ResponseKind`, wire-stable bytes).
pub type ResponseKind {
  /// 0x81 — body = 038 ResultEnvelope bytes.
  ResultKind
  /// 0x82 — body = UTF-8 status string (ACK for STATUS/SHUTDOWN/PING/…).
  Ack
  /// 0x83 — snapshot parked pending quiescence (wire rule 5); empty body.
  Deferred
  /// 0x84 — body = UTF-8 reason; the engine keeps serving (FR-006).
  ProtocolError
  /// 0x85 — restore in progress, only STATUS/PING served (wire rule 4).
  EngineBusy
  /// 0x86 — 064 US3 typed IL refusal (body = [code u8][reason UTF-8]).
  IlRefused
}

/// One client→engine request: id (echoed), kind, body bytes.
pub type RequestFrame {
  RequestFrame(request_id: Int, kind: RequestKind, body: BitArray)
}

/// One engine→client response: echoed id, kind, body bytes.
pub type ResponseFrame {
  ResponseFrame(request_id: Int, kind: ResponseKind, body: BitArray)
}

/// Convenience: a request with a UTF-8 text body (C# `RequestFrame.Text`).
pub fn request_text(request_id: Int, kind: RequestKind, body: String) -> RequestFrame {
  RequestFrame(request_id, kind, bit_array.from_string(body))
}

/// Convenience: a body-less request (SNAPSHOT/STATUS/SHUTDOWN/PING).
pub fn request_empty(request_id: Int, kind: RequestKind) -> RequestFrame {
  RequestFrame(request_id, kind, <<>>)
}

/// Convenience: a response with a UTF-8 text body (C# `ResponseFrame.Text`).
pub fn response_text(
  request_id: Int,
  kind: ResponseKind,
  body: String,
) -> ResponseFrame {
  ResponseFrame(request_id, kind, bit_array.from_string(body))
}

// ---------------------------------------------------------------------------
// kind <-> wire byte (C# enum values, verbatim)
// ---------------------------------------------------------------------------

fn request_kind_byte(kind: RequestKind) -> Int {
  case kind {
    LoadSource -> 0x01
    RunGoal -> 0x02
    Snapshot -> 0x03
    Status -> 0x04
    Shutdown -> 0x05
    Ping -> 0x06
    LoadIl -> 0x07
    RunGoalIl -> 0x08
  }
}

fn request_kind_from_byte(b: Int) -> Result(RequestKind, SplitError) {
  case b {
    0x01 -> Ok(LoadSource)
    0x02 -> Ok(RunGoal)
    0x03 -> Ok(Snapshot)
    0x04 -> Ok(Status)
    0x05 -> Ok(Shutdown)
    0x06 -> Ok(Ping)
    0x07 -> Ok(LoadIl)
    0x08 -> Ok(RunGoalIl)
    _ -> Error(SplitError("unknown request kind " <> hex(b)))
  }
}

fn response_kind_byte(kind: ResponseKind) -> Int {
  case kind {
    ResultKind -> 0x81
    Ack -> 0x82
    Deferred -> 0x83
    ProtocolError -> 0x84
    EngineBusy -> 0x85
    IlRefused -> 0x86
  }
}

fn response_kind_from_byte(b: Int) -> Result(ResponseKind, SplitError) {
  case b {
    0x81 -> Ok(ResultKind)
    0x82 -> Ok(Ack)
    0x83 -> Ok(Deferred)
    0x84 -> Ok(ProtocolError)
    0x85 -> Ok(EngineBusy)
    0x86 -> Ok(IlRefused)
    _ -> Error(SplitError("unknown response kind " <> hex(b)))
  }
}

// ---------------------------------------------------------------------------
// payload level (the FrameCodec chunk bytes) — C# RequestResponseCodec parity
// ---------------------------------------------------------------------------

/// type(1) + request_id(8) + kind(1) + body_len(4).
const header_len = 14

/// Encode a request into split-protocol payload bytes.
pub fn encode_request_payload(request: RequestFrame) -> BitArray {
  encode_payload(
    payload_type_request,
    request.request_id,
    request_kind_byte(request.kind),
    request.body,
  )
}

/// Encode a response into split-protocol payload bytes.
pub fn encode_response_payload(response: ResponseFrame) -> BitArray {
  encode_payload(
    payload_type_response,
    response.request_id,
    response_kind_byte(response.kind),
    response.body,
  )
}

/// Decode payload bytes that must be a request (engine side). Validation order
/// mirrors C# `DecodeRequestPayload`: header/type/length first, then the kind
/// table, then the per-kind body rules (wire contract "bodies empty unless
/// noted"; IL kinds must carry a body — contracts/il-request-kind.md).
pub fn decode_request_payload(
  payload: BitArray,
) -> Result(RequestFrame, SplitError) {
  case decode_payload(payload) {
    Error(e) -> Error(e)
    Ok(#(ptype, request_id, kind_byte, body)) ->
      case ptype == payload_type_request {
        False ->
          Error(SplitError(
            "expected REQUEST payload type "
            <> hex(payload_type_request)
            <> ", got "
            <> hex(ptype),
          ))
        True ->
          case request_kind_from_byte(kind_byte) {
            Error(e) -> Error(e)
            Ok(kind) -> {
              let body_len = bit_array.byte_size(body)
              case kind, body_len {
                Snapshot, n | Status, n | Shutdown, n | Ping, n if n > 0 ->
                  Error(SplitError(
                    "request kind "
                    <> hex(kind_byte)
                    <> " must carry an empty body, got "
                    <> int_to_string(n)
                    <> " byte(s)",
                  ))
                LoadIl, 0 | RunGoalIl, 0 ->
                  Error(SplitError(
                    "request kind "
                    <> hex(kind_byte)
                    <> " must carry a non-empty body",
                  ))
                _, _ -> Ok(RequestFrame(request_id, kind, body))
              }
            }
          }
      }
  }
}

/// Decode payload bytes that must be a response (client side).
pub fn decode_response_payload(
  payload: BitArray,
) -> Result(ResponseFrame, SplitError) {
  case decode_payload(payload) {
    Error(e) -> Error(e)
    Ok(#(ptype, request_id, kind_byte, body)) ->
      case ptype == payload_type_response {
        False ->
          Error(SplitError(
            "expected RESPONSE payload type "
            <> hex(payload_type_response)
            <> ", got "
            <> hex(ptype),
          ))
        True ->
          case response_kind_from_byte(kind_byte) {
            Error(e) -> Error(e)
            Ok(kind) -> Ok(ResponseFrame(request_id, kind, body))
          }
      }
  }
}

// ---------------------------------------------------------------------------
// frame level (whole FrameCodec frames, as carried on the wire)
// ---------------------------------------------------------------------------

/// Encode a request as ONE whole FrameCodec frame (loopback MVP never
/// fragments; an over-limit payload loud-fails inside FrameCodec).
pub fn encode_request_frame(
  request: RequestFrame,
  message_id: Int,
) -> Result(BitArray, SplitError) {
  whole_frame(encode_request_payload(request), message_id)
}

/// Encode a response as ONE whole FrameCodec frame.
pub fn encode_response_frame(
  response: ResponseFrame,
  message_id: Int,
) -> Result(BitArray, SplitError) {
  whole_frame(encode_response_payload(response), message_id)
}

/// Parse a FrameCodec frame and decode its chunk as a request.
pub fn decode_request_frame(frame: BitArray) -> Result(RequestFrame, SplitError) {
  case whole_chunk(frame) {
    Error(e) -> Error(e)
    Ok(chunk) -> decode_request_payload(chunk)
  }
}

/// Parse a FrameCodec frame and decode its chunk as a response.
pub fn decode_response_frame(
  frame: BitArray,
) -> Result(ResponseFrame, SplitError) {
  case whole_chunk(frame) {
    Error(e) -> Error(e)
    Ok(chunk) -> decode_response_payload(chunk)
  }
}

// ---------------------------------------------------------------------------
// internals
// ---------------------------------------------------------------------------

fn whole_frame(payload: BitArray, message_id: Int) -> Result(BitArray, SplitError) {
  // max_frame_bytes = None → always ONE Whole frame (C# FrameCodec.Encode(...)[0]).
  case frame_codec.encode(payload, message_id, option_none()) {
    Ok([frame]) -> Ok(frame)
    Ok(_) ->
      Error(SplitError(
        "split protocol carries whole frames only (loopback MVP never fragments)",
      ))
    Error(fe) ->
      Error(SplitError("bad FrameCodec frame: " <> string.inspect(fe)))
  }
}

fn whole_chunk(frame: BitArray) -> Result(BitArray, SplitError) {
  case frame_codec.parse_frame(frame) {
    Error(fe) ->
      Error(SplitError("bad FrameCodec frame: " <> string.inspect(fe)))
    Ok(parsed) ->
      case parsed.kind {
        frame_codec.Whole -> Ok(parsed.chunk)
        frame_codec.Fragment ->
          Error(SplitError(
            "split protocol carries whole frames only (loopback MVP never fragments)",
          ))
      }
  }
}

fn encode_payload(
  payload_type: Int,
  request_id: Int,
  kind_byte: Int,
  body: BitArray,
) -> BitArray {
  let body_len = bit_array.byte_size(body)
  <<
    payload_type:int,
    request_id:int-size(64)-big,
    kind_byte:int,
    body_len:int-size(32)-big,
    body:bits,
  >>
}

fn decode_payload(
  payload: BitArray,
) -> Result(#(Int, Int, Int, BitArray), SplitError) {
  let size = bit_array.byte_size(payload)
  case size < header_len {
    True ->
      Error(SplitError(
        "payload "
        <> int_to_string(size)
        <> " bytes is shorter than the "
        <> int_to_string(header_len)
        <> "-byte header",
      ))
    False ->
      case payload {
        <<
          ptype:int,
          request_id:int-size(64)-big,
          kind_byte:int,
          body_len:int-size(32)-big,
          rest:bits,
        >> -> {
          case ptype == payload_type_request || ptype == payload_type_response {
            False ->
              Error(SplitError("unknown split payload type " <> hex(ptype)))
            True -> {
              let expected = header_len + body_len
              case size < expected, size > expected {
                True, _ ->
                  Error(SplitError(
                    "payload "
                    <> int_to_string(size)
                    <> " bytes shorter than header+body "
                    <> int_to_string(expected),
                  ))
                _, True ->
                  Error(SplitError(
                    "trailing bytes: payload "
                    <> int_to_string(size)
                    <> " > header+body "
                    <> int_to_string(expected)
                    <> " (038 convention)",
                  ))
                False, False -> Ok(#(ptype, request_id, kind_byte, rest))
              }
            }
          }
        }
        // Unreachable: size >= 14 guarantees the header pattern matches.
        _ -> Error(SplitError("payload header unreadable"))
      }
  }
}

fn int_to_string(n: Int) -> String {
  int.to_string(n)
}

/// C#-style 0xNN rendering (uppercase, two digits) for error-message parity.
fn hex(b: Int) -> String {
  "0x" <> pad2(int.to_base16(b))
}

fn pad2(s: String) -> String {
  case string.length(s) {
    1 -> "0" <> s
    _ -> s
  }
}

fn option_none() -> option.Option(Int) {
  option.None
}
