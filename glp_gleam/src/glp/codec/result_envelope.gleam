//// glp/codec/result_envelope — the heap-independent result envelope + its byte frame
//// (feature 038-result-codec-and-framecodec-ride).
////
//// The result side of the ratified ED-1 seam: a transportable
//// `{status, resolvedBindings, varToWriter, suspended, captured, error}` value and its
//// wire frame over the Section-15 term sub-codec (glp/codec/term_codec). The 2-byte
//// header (version 0x01 + payloadType 0x11 RESULT_ENVELOPE) mirrors the 029
//// PayloadHeader discipline (029 IL programs are 0x10; the envelope takes 0x11) so
//// envelopes and IL programs are never confusable on a shared wire (R3).
////
//// Dart is the byte source of truth (R9); this Gleam encoder is byte-for-byte identical
//// to glp_runtime/lib/codec/result_envelope_codec.dart. `decode` MUST consume every
//// byte or fail loudly (FR-005, SC-004): bad version, bad payloadType, bad status, bad
//// errorPresent, truncation, trailing bytes, unknown term tag, or an over-64-bit varint
//// all return `Error(CodecError)`.
////
//// Ordering: `resolved_bindings`, `var_to_writer`, `suspended` are List(...) (NOT a
//// Dict) so the producing engine's canonical declaration/binding order is preserved on
//// the wire — map iteration order MUST NOT leak (the parity invariant, SC-002).
//// AtomVM (0.6.6): gleam stdlib + bit-syntax only; no gleam_otp.

import gleam/bit_array
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/result
import glp/codec/term_codec.{
  type CodecError, type GlobalVarId, type Term, BadErrorPresent, BadPayloadType,
  BadStatus, BadVersion, TrailingBytes, Truncated,
}

/// Codec format version (matches shipped 029 PayloadHeader.Version).
const envelope_version = 0x01

/// Payload-type discriminant for a result envelope (029 IL program = 0x10; next = 0x11).
const payload_type_result_envelope = 0x11

/// Execution status of a result (data-model §1; wire byte in contract §4).
pub type ExecutionStatus {
  Success
  Suspended
  Failed
}

/// The heap-independent result envelope (FR-001, data-model §1). Immutable once emitted;
/// no field, transitively, holds a live heap address (SC-003 — structural: the only
/// variable reference type reachable is GlobalVarId, never a heap addr).
pub type ResultEnvelope {
  ResultEnvelope(
    status: ExecutionStatus,
    resolved_bindings: List(#(String, Term)),
    var_to_writer: List(#(String, GlobalVarId)),
    suspended: List(GlobalVarId),
    captured: BitArray,
    error: Option(String),
  )
}

// ---------------------------------------------------------------------------
// status <-> wire byte
// ---------------------------------------------------------------------------

fn status_byte(s: ExecutionStatus) -> Int {
  case s {
    Success -> 0x00
    Suspended -> 0x01
    Failed -> 0x02
  }
}

fn status_from_byte(b: Int) -> Result(ExecutionStatus, CodecError) {
  case b {
    0x00 -> Ok(Success)
    0x01 -> Ok(Suspended)
    0x02 -> Ok(Failed)
    _ -> Error(BadStatus(b))
  }
}

// ---------------------------------------------------------------------------
// encode — byte-for-byte identical to result_envelope_codec.dart
// ---------------------------------------------------------------------------

pub fn encode(env: ResultEnvelope) -> BitArray {
  let sb = status_byte(env.status)

  let bindings_bytes =
    env.resolved_bindings
    |> list.map(fn(pair) {
      let #(name, term) = pair
      bit_array.append(term_codec.write_string(name), term_codec.encode_term(term))
    })
    |> bit_array.concat

  let var_to_writer_bytes =
    env.var_to_writer
    |> list.map(fn(pair) {
      let #(name, id) = pair
      bit_array.append(
        term_codec.write_string(name),
        term_codec.encode_global_var_id(id),
      )
    })
    |> bit_array.concat

  let suspended_bytes =
    env.suspended
    |> list.map(term_codec.encode_global_var_id)
    |> bit_array.concat

  let error_bytes = case env.error {
    None -> <<0x00:int>>
    Some(s) -> bit_array.append(<<0x01:int>>, term_codec.write_string(s))
  }

  // 0x01 0x11 == envelope_version, payload_type_result_envelope (literal so the byte
  // layout is unmistakable; the consts above are the single source of those values).
  bit_array.concat([
    <<0x01:int, 0x11:int>>,
    <<sb:int>>,
    term_codec.write_varuint(list.length(env.resolved_bindings)),
    bindings_bytes,
    term_codec.write_varuint(list.length(env.var_to_writer)),
    var_to_writer_bytes,
    term_codec.write_varuint(list.length(env.suspended)),
    suspended_bytes,
    term_codec.write_varuint(bit_array.byte_size(env.captured)),
    env.captured,
    error_bytes,
  ])
}

// ---------------------------------------------------------------------------
// decode — consumes ALL bytes or fails loudly
// ---------------------------------------------------------------------------

pub fn decode(bytes: BitArray) -> Result(ResultEnvelope, CodecError) {
  use #(version, r1) <- result.try(read_byte(bytes, "version"))
  use _ <- result.try(case version == envelope_version {
    True -> Ok(Nil)
    False -> Error(BadVersion(version))
  })

  use #(ptype, r2) <- result.try(read_byte(r1, "payloadType"))
  use _ <- result.try(case ptype == payload_type_result_envelope {
    True -> Ok(Nil)
    False -> Error(BadPayloadType(ptype))
  })

  use #(status_b, r3) <- result.try(read_byte(r2, "status"))
  use status <- result.try(status_from_byte(status_b))

  use #(bcount, r4) <- result.try(term_codec.read_varuint(r3))
  use #(bindings, r5) <- result.try(read_bindings(r4, bcount, []))

  use #(vcount, r6) <- result.try(term_codec.read_varuint(r5))
  use #(var_to_writer, r7) <- result.try(read_var_to_writer(r6, vcount, []))

  use #(scount, r8) <- result.try(term_codec.read_varuint(r7))
  use #(suspended, r9) <- result.try(read_suspended(r8, scount, []))

  use #(clen, r10) <- result.try(term_codec.read_varuint(r9))
  use #(captured, r11) <- result.try(term_codec.take_bytes(r10, clen))

  use #(error_present, r12) <- result.try(read_byte(r11, "errorPresent"))
  use #(error, r13) <- result.try(read_error(error_present, r12))

  case bit_array.byte_size(r13) {
    0 ->
      Ok(ResultEnvelope(
        status,
        bindings,
        var_to_writer,
        suspended,
        captured,
        error,
      ))
    n -> Error(TrailingBytes(n))
  }
}

// ---------------------------------------------------------------------------
// decode helpers
// ---------------------------------------------------------------------------

fn read_byte(
  input: BitArray,
  what: String,
) -> Result(#(Int, BitArray), CodecError) {
  case input {
    <<b:int, rest:bits>> -> Ok(#(b, rest))
    _ -> Error(Truncated(what))
  }
}

fn read_bindings(
  input: BitArray,
  count: Int,
  acc: List(#(String, Term)),
) -> Result(#(List(#(String, Term)), BitArray), CodecError) {
  case count <= 0 {
    True -> Ok(#(list.reverse(acc), input))
    False -> {
      use #(name, r1) <- result.try(term_codec.read_string(input))
      use #(term, r2) <- result.try(term_codec.decode_term(r1))
      read_bindings(r2, count - 1, [#(name, term), ..acc])
    }
  }
}

fn read_var_to_writer(
  input: BitArray,
  count: Int,
  acc: List(#(String, GlobalVarId)),
) -> Result(#(List(#(String, GlobalVarId)), BitArray), CodecError) {
  case count <= 0 {
    True -> Ok(#(list.reverse(acc), input))
    False -> {
      use #(name, r1) <- result.try(term_codec.read_string(input))
      use #(id, r2) <- result.try(term_codec.decode_global_var_id(r1))
      read_var_to_writer(r2, count - 1, [#(name, id), ..acc])
    }
  }
}

fn read_suspended(
  input: BitArray,
  count: Int,
  acc: List(GlobalVarId),
) -> Result(#(List(GlobalVarId), BitArray), CodecError) {
  case count <= 0 {
    True -> Ok(#(list.reverse(acc), input))
    False -> {
      use #(id, r1) <- result.try(term_codec.decode_global_var_id(input))
      read_suspended(r1, count - 1, [id, ..acc])
    }
  }
}

fn read_error(
  present: Int,
  input: BitArray,
) -> Result(#(Option(String), BitArray), CodecError) {
  case present {
    0x00 -> Ok(#(None, input))
    0x01 -> {
      use #(s, rest) <- result.try(term_codec.read_string(input))
      Ok(#(Some(s), rest))
    }
    _ -> Error(BadErrorPresent(present))
  }
}
