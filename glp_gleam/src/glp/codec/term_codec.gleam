//// glp/codec/term_codec — the Section-15 *term* portion of the result-envelope codec
//// (feature 038-result-codec-and-framecodec-ride).
////
//// Byte primitives + Term encode/decode. The byte conventions are IDENTICAL to the
//// shipped 029 `GlpRuntime.IlCodec` (`ByteIo` + `ConstantCodec`) and to the Dart
//// reference (the SOURCE OF TRUTH, R9): unsigned LEB128 varints, fixed 8-byte
//// little-endian int64, IEEE-754 double bit pattern (8 bytes little-endian),
//// varint+UTF-8 strings, and term tags 0x02..0x06. The one added tag is 0x07
//// (unbound VarRef → GlobalVarId), outside 029's IL scope (data-model §3, contract §3).
////
//// This is a PARALLEL implementation that reproduces the Dart bytes byte-for-byte — it
//// does NOT reuse 029 code (FR-007; 029 is the C# oracle only). All malformed input
//// fails loudly with a `CodecError` (FR-005, SC-004): truncated reads, varints over
//// 64 bits, invalid UTF-8, and unknown / 029-reserved (0x00/0x01) term tags.
////
//// Dart source mirrored: glp_runtime/lib/codec/{result_envelope,term_codec}.dart.
//// AtomVM (0.6.6) note: encode/decode use only gleam stdlib + bit-syntax (no gleam_otp).
//// The double branch (tag 0x03) is GATED (ED-6 AtomVM `/float`); it runs fine on full
//// BEAM (where `gleam test` executes) and is exercised only by the gated corpus.

import gleam/bit_array
import gleam/int
import gleam/list
import gleam/result

// ---------------------------------------------------------------------------
// Value types (the term model carried by the envelope). Mirrors 034
// glp/runtime/terms.gleam in shape, BUT the codec's VarRef carries a global
// identity (GlobalVarId, tag 0x07) rather than 034's local `VarRef(addr: Int)`,
// so it is meaningful cross-process (data-model §3/§4).
// ---------------------------------------------------------------------------

/// Global, heap-independent identity of a GLP variable's writer (data-model §4, R7).
/// Identity is `(agent_id, local_id)` — never a local heap address. Wire: `agent_id`
/// (string body) + `local_id` (fixed 8-byte little-endian int64).
pub type GlobalVarId {
  GlobalVarId(agent_id: String, local_id: Int)
}

/// A GLP ground constant — the four kinds (mirrors 034 `terms.gleam` Constant, FR-001).
pub type Constant {
  ConstAtom(String)
  ConstInt(Int)
  ConstReal(Float)
  ConstString(String)
}

/// A GLP term. Lists are the cons/nil structure: `[h|t]` = `StructTerm(".", [h, t])`,
/// `nil` = `ConstTerm(ConstAtom("nil"))` (034 convention). A deep-resolved binding never
/// holds a *bound* VarRef; a remaining VarRef is an UNBOUND variable carrying its
/// GlobalVarId.
pub type Term {
  ConstTerm(Constant)
  StructTerm(functor: String, args: List(Term))
  VarRef(GlobalVarId)
}

/// Loud-fail result for any malformed input or out-of-model value (FR-005, SC-004).
/// Mirrors Dart `ResultCodecException` / C# `ResultCodecException`.
pub type CodecError {
  /// Reached end-of-input before a required field could be read.
  Truncated(what: String)
  /// A LEB128 varint did not terminate within 64 bits (contract §5.7).
  VarintTooLong
  /// A term tag not in {0x02..0x07}; includes the 029-reserved null/bool tags
  /// (0x00/0x01) which have no representation in the GLP term model (contract §5.2).
  UnknownTag(tag: Int)
  /// A string field's bytes were not valid UTF-8.
  InvalidUtf8
  /// Envelope `version` byte was not 0x01 (contract §5.1).
  BadVersion(found: Int)
  /// Envelope `payloadType` byte was not 0x11 RESULT_ENVELOPE (contract §5.1).
  BadPayloadType(found: Int)
  /// `status` byte not in {0x00,0x01,0x02} (contract §5.3).
  BadStatus(found: Int)
  /// `errorPresent` flag not in {0x00,0x01} (contract §5.4).
  BadErrorPresent(found: Int)
  /// Input not fully consumed after a complete envelope (contract §5.6).
  TrailingBytes(count: Int)
}

// ---------------------------------------------------------------------------
// Term tag table (contract §3 / data-model §3). 0x00/0x01 are reserved by 029
// but have no GLP-term representation → loud-fail on decode (see decode_term_body).
//   0x02 int64 | 0x03 double (gated) | 0x04 string | 0x05 atom | 0x06 struct | 0x07 varref
// ---------------------------------------------------------------------------

const tag_int64 = 0x02

const tag_double = 0x03

const tag_string = 0x04

const tag_atom = 0x05

const tag_struct = 0x06

const tag_varref = 0x07

// ---------------------------------------------------------------------------
// Byte primitives — encode
// ---------------------------------------------------------------------------

/// Unsigned LEB128 varint (counts / lengths). Mirrors Dart `BytesWriter.writeVarUInt`.
/// Only ever called with non-negative counts/lengths.
pub fn write_varuint(v: Int) -> BitArray {
  case v < 0x80 {
    True -> <<v:int>>
    False -> {
      let low = int.bitwise_or(int.bitwise_and(v, 0x7F), 0x80)
      bit_array.append(<<low:int>>, write_varuint(int.bitwise_shift_right(v, 7)))
    }
  }
}

/// Fixed 8-byte little-endian int64 (two's-complement; low 64 bits). Mirrors Dart
/// `ByteData.setInt64(0, v, Endian.little)`. The `:64` size masks BEAM bignums to the
/// low 64 bits and emits two's-complement for negatives — identical bytes to Dart.
fn write_int64(v: Int) -> BitArray {
  <<v:int-size(64)-little>>
}

/// IEEE-754 double, 8 bytes little-endian. Mirrors Dart `setFloat64(0, d, Endian.little)`.
/// GATED (ED-6 AtomVM `/float`); fine on full BEAM.
fn write_double(d: Float) -> BitArray {
  <<d:float-size(64)-little>>
}

/// Varint length + UTF-8 bytes (the "0x04-body"). Mirrors Dart `writeString`.
pub fn write_string(s: String) -> BitArray {
  let bytes = bit_array.from_string(s)
  bit_array.append(write_varuint(bit_array.byte_size(bytes)), bytes)
}

// ---------------------------------------------------------------------------
// Byte primitives — decode (each returns the value + the unconsumed remainder)
// ---------------------------------------------------------------------------

/// Unsigned LEB128 varint; loud-fail if it exceeds 64 bits (contract §5.7).
pub fn read_varuint(input: BitArray) -> Result(#(Int, BitArray), CodecError) {
  read_varuint_loop(input, 0, 0)
}

fn read_varuint_loop(
  input: BitArray,
  acc: Int,
  shift: Int,
) -> Result(#(Int, BitArray), CodecError) {
  case input {
    <<b:int, rest:bits>> -> {
      let acc2 =
        int.bitwise_or(
          acc,
          int.bitwise_shift_left(int.bitwise_and(b, 0x7F), shift),
        )
      case int.bitwise_and(b, 0x80) == 0 {
        True -> Ok(#(acc2, rest))
        False -> {
          let shift2 = shift + 7
          case shift2 >= 64 {
            True -> Error(VarintTooLong)
            False -> read_varuint_loop(rest, acc2, shift2)
          }
        }
      }
    }
    _ -> Error(Truncated("varint"))
  }
}

/// Signed fixed 8-byte little-endian int64. Mirrors Dart `getInt64(0, Endian.little)`.
fn read_int64(input: BitArray) -> Result(#(Int, BitArray), CodecError) {
  case input {
    <<v:int-size(64)-little-signed, rest:bits>> -> Ok(#(v, rest))
    _ -> Error(Truncated("int64"))
  }
}

/// IEEE-754 double, 8 bytes little-endian. GATED (ED-6). Mirrors Dart `getFloat64`.
fn read_double(input: BitArray) -> Result(#(Float, BitArray), CodecError) {
  case input {
    <<v:float-size(64)-little, rest:bits>> -> Ok(#(v, rest))
    _ -> Error(Truncated("double"))
  }
}

/// Read exactly `n` bytes; loud-fail (truncation) if fewer remain. `n` is always a
/// non-negative varint-decoded length.
pub fn take_bytes(
  input: BitArray,
  n: Int,
) -> Result(#(BitArray, BitArray), CodecError) {
  let total = bit_array.byte_size(input)
  case n >= 0 && n <= total {
    True ->
      case bit_array.slice(input, 0, n), bit_array.slice(input, n, total - n) {
        Ok(taken), Ok(rest) -> Ok(#(taken, rest))
        _, _ -> Error(Truncated("bytes"))
      }
    False -> Error(Truncated("bytes"))
  }
}

/// Varint length + UTF-8 bytes → String; loud-fail on invalid UTF-8 (mirrors Dart's
/// throwing `utf8.decode`).
pub fn read_string(input: BitArray) -> Result(#(String, BitArray), CodecError) {
  use #(len, rest) <- result.try(read_varuint(input))
  use #(strbytes, rest2) <- result.try(take_bytes(rest, len))
  case bit_array.to_string(strbytes) {
    Ok(s) -> Ok(#(s, rest2))
    Error(_) -> Error(InvalidUtf8)
  }
}

// ---------------------------------------------------------------------------
// GlobalVarId wire form (data-model §4): agent_id (string body) + local_id (int64 LE)
// ---------------------------------------------------------------------------

pub fn encode_global_var_id(id: GlobalVarId) -> BitArray {
  bit_array.append(write_string(id.agent_id), write_int64(id.local_id))
}

pub fn decode_global_var_id(
  input: BitArray,
) -> Result(#(GlobalVarId, BitArray), CodecError) {
  use #(agent_id, rest) <- result.try(read_string(input))
  use #(local_id, rest2) <- result.try(read_int64(rest))
  Ok(#(GlobalVarId(agent_id, local_id), rest2))
}

// ---------------------------------------------------------------------------
// Term encode / decode (contract §3) — recursive over StructTerm args.
// ---------------------------------------------------------------------------

pub fn encode_term(t: Term) -> BitArray {
  case t {
    ConstTerm(ConstInt(n)) -> bit_array.append(<<tag_int64:int>>, write_int64(n))
    ConstTerm(ConstReal(d)) ->
      bit_array.append(<<tag_double:int>>, write_double(d))
    ConstTerm(ConstString(s)) ->
      bit_array.append(<<tag_string:int>>, write_string(s))
    ConstTerm(ConstAtom(a)) -> bit_array.append(<<tag_atom:int>>, write_string(a))
    StructTerm(functor, args) ->
      bit_array.concat([
        <<tag_struct:int>>,
        write_string(functor),
        write_varuint(list.length(args)),
        ..list.map(args, encode_term)
      ])
    VarRef(id) -> bit_array.append(<<tag_varref:int>>, encode_global_var_id(id))
  }
}

pub fn decode_term(input: BitArray) -> Result(#(Term, BitArray), CodecError) {
  case input {
    <<tag:int, rest:bits>> -> decode_term_body(tag, rest)
    _ -> Error(Truncated("term tag"))
  }
}

fn decode_term_body(
  tag: Int,
  input: BitArray,
) -> Result(#(Term, BitArray), CodecError) {
  case tag {
    0x02 -> {
      use #(n, rest) <- result.try(read_int64(input))
      Ok(#(ConstTerm(ConstInt(n)), rest))
    }
    0x03 -> {
      use #(d, rest) <- result.try(read_double(input))
      Ok(#(ConstTerm(ConstReal(d)), rest))
    }
    0x04 -> {
      use #(s, rest) <- result.try(read_string(input))
      Ok(#(ConstTerm(ConstString(s)), rest))
    }
    0x05 -> {
      use #(a, rest) <- result.try(read_string(input))
      Ok(#(ConstTerm(ConstAtom(a)), rest))
    }
    0x06 -> {
      use #(functor, r1) <- result.try(read_string(input))
      use #(arity, r2) <- result.try(read_varuint(r1))
      use #(args, r3) <- result.try(decode_terms(r2, arity, []))
      Ok(#(StructTerm(functor, args), r3))
    }
    0x07 -> {
      use #(id, rest) <- result.try(decode_global_var_id(input))
      Ok(#(VarRef(id), rest))
    }
    // 0x00/0x01 (029-reserved null/bool) and any tag >= 0x08 ⇒ loud-fail (contract §5.2).
    _ -> Error(UnknownTag(tag))
  }
}

fn decode_terms(
  input: BitArray,
  count: Int,
  acc: List(Term),
) -> Result(#(List(Term), BitArray), CodecError) {
  case count <= 0 {
    True -> Ok(#(list.reverse(acc), input))
    False -> {
      use #(term, rest) <- result.try(decode_term(input))
      decode_terms(rest, count - 1, [term, ..acc])
    }
  }
}
