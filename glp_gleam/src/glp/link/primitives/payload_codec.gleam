//// glp/link/primitives/payload_codec — the GROUND-term link payload codec
//// (T050.C5/C6; contracts/link-primitives-port.md D-4 ground-relay).
////
//// Byte-identical to the Dart `PayloadSerializer` V2 term format
//// (`glp_runtime/lib/multiagent/payload_serializer.dart`, mirrored by the C#
//// peer per T082) — the format the reference link egress ships
//// (`serializeAgentMessage`) and the pump decodes. GROUND SUBSET ONLY: the
//// base link is ground-relay (R-7), so tag 2 (variable) is refused loudly on
//// both encode (the `ground(Msg?)` gate upstream should have excluded it) and
//// decode (a peer shipping variables over the base link is nonconforming).
////
//// Wire grammar (Dart `_serializeTermRecursiveV2` / `_serializeConstant`):
////   term      := 0x01 constant | 0x03 struct
////   constant  := 0x00                        (the `nil` atom)
////              | 0x01 int64-BE
////              | 0x02 float64-BE
////              | 0x03 varlen utf8-bytes      (string/atom text)
////   struct    := varlen utf8-functor varlen-arity term*
////   varlen    := 1 byte  n            (n < 0x80)
////              | 2 bytes 0x80|hi lo   (n < 0x4000)
////              | 4 bytes 0xC0|b3 b2 b1 b0
////
//// Quote mapping: the Dart/C# term models store a GLP STRING constant with its
//// surrounding double-quotes inside the one string-valued constant kind, so the
//// WIRE text of a GLP string carries quotes and a bare atom does not. The Gleam
//// model keeps `ConstString`/`ConstAtom` as distinct variants storing raw text —
//// so encode adds the quotes for `ConstString` and decode strips them back.

import gleam/bit_array
import gleam/int
import gleam/list
import gleam/result
import gleam/string
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstReal, ConstString, ConstTerm, StructTerm,
  VarRef,
}

/// Encode one GROUND term as a link payload. `Error` on any variable (the
/// ground-relay gate) or an unencodable shape — loud, never a partial payload.
pub fn encode_ground(term: Term) -> Result(BitArray, String) {
  case term {
    ConstTerm(c) -> Ok(<<1:size(8), encode_constant(c):bits>>)
    StructTerm(functor, args) -> {
      use encoded_args <- result.try(
        list.try_map(args, encode_ground)
        |> result.map(bit_array.concat),
      )
      let f = <<functor:utf8>>
      Ok(<<
        3:size(8),
        varlen(bit_array.byte_size(f)):bits,
        f:bits,
        varlen(list.length(args)):bits,
        encoded_args:bits,
      >>)
    }
    VarRef(_) ->
      Error("non-ground term reached the link payload encoder (ground-relay)")
  }
}

fn encode_constant(c: terms.Constant) -> BitArray {
  case c {
    ConstAtom("nil") -> <<0:size(8)>>
    ConstInt(v) -> <<1:size(8), v:int-size(64)-big>>
    ConstReal(v) -> <<2:size(8), v:float-size(64)-big>>
    ConstString(s) -> {
      let quoted = <<{ "\"" <> s <> "\"" }:utf8>>
      <<3:size(8), varlen(bit_array.byte_size(quoted)):bits, quoted:bits>>
    }
    ConstAtom(s) -> {
      let raw = <<s:utf8>>
      <<3:size(8), varlen(bit_array.byte_size(raw)):bits, raw:bits>>
    }
  }
}

/// Decode one link payload into a GROUND term. The whole payload must be one
/// term (trailing bytes are a wire violation, reported).
pub fn decode_ground(payload: BitArray) -> Result(Term, String) {
  use #(term, rest) <- result.try(decode_term(payload))
  case bit_array.byte_size(rest) {
    0 -> Ok(term)
    n -> Error("link payload: " <> int.to_string(n) <> " trailing byte(s)")
  }
}

fn decode_term(input: BitArray) -> Result(#(Term, BitArray), String) {
  case input {
    <<1:size(8), rest:bits>> -> decode_constant(rest)
    <<3:size(8), rest:bits>> -> {
      use #(functor, rest) <- result.try(decode_string(rest, "struct functor"))
      use #(arity, rest) <- result.try(decode_varlen(rest, "struct arity"))
      decode_args(rest, arity, [])
      |> result.map(fn(pair) {
        let #(args, rest) = pair
        #(StructTerm(functor, list.reverse(args)), rest)
      })
    }
    <<2:size(8), _:bits>> ->
      Error("link payload: variable (tag 2) on the base ground-relay link")
    <<tag:size(8), _:bits>> ->
      Error("link payload: unknown term tag " <> int.to_string(tag))
    _ -> Error("link payload: truncated (empty term)")
  }
}

fn decode_args(
  input: BitArray,
  remaining: Int,
  acc: List(Term),
) -> Result(#(List(Term), BitArray), String) {
  case remaining {
    0 -> Ok(#(acc, input))
    _ -> {
      use #(arg, rest) <- result.try(decode_term(input))
      decode_args(rest, remaining - 1, [arg, ..acc])
    }
  }
}

fn decode_constant(input: BitArray) -> Result(#(Term, BitArray), String) {
  case input {
    <<0:size(8), rest:bits>> -> Ok(#(ConstTerm(ConstAtom("nil")), rest))
    <<1:size(8), v:int-size(64)-big-signed, rest:bits>> ->
      Ok(#(ConstTerm(ConstInt(v)), rest))
    <<2:size(8), v:float-size(64)-big, rest:bits>> ->
      Ok(#(ConstTerm(ConstReal(v)), rest))
    <<3:size(8), rest:bits>> -> {
      use #(text, rest) <- result.try(decode_string(rest, "string constant"))
      // Quoted wire text = a GLP STRING; bare = an atom (see module header).
      let term = case
        string.starts_with(text, "\"") && string.ends_with(text, "\"")
        && string.length(text) >= 2
      {
        True ->
          ConstTerm(ConstString(string.slice(text, 1, string.length(text) - 2)))
        False -> ConstTerm(ConstAtom(text))
      }
      Ok(#(term, rest))
    }
    <<sub:size(8), _:bits>> ->
      Error("link payload: unknown constant subtag " <> int.to_string(sub))
    _ -> Error("link payload: truncated constant")
  }
}

// ---- varlen (the Dart _encodeLength / _decodeLength scheme) ----

fn varlen(n: Int) -> BitArray {
  case n < 128 {
    True -> <<n:size(8)>>
    False ->
      case n < 16_384 {
        True -> <<
          int.bitwise_or(0x80, int.bitwise_shift_right(n, 8)):size(8),
          int.bitwise_and(n, 0xFF):size(8),
        >>
        False -> <<
          int.bitwise_or(0xC0, int.bitwise_shift_right(n, 24)):size(8),
          int.bitwise_and(int.bitwise_shift_right(n, 16), 0xFF):size(8),
          int.bitwise_and(int.bitwise_shift_right(n, 8), 0xFF):size(8),
          int.bitwise_and(n, 0xFF):size(8),
        >>
      }
  }
}

fn decode_varlen(
  input: BitArray,
  what: String,
) -> Result(#(Int, BitArray), String) {
  case input {
    <<b0:size(8), rest:bits>> ->
      case b0 < 0x80 {
        True -> Ok(#(b0, rest))
        False ->
          case b0 < 0xC0 {
            True ->
              case rest {
                <<b1:size(8), rest:bits>> ->
                  Ok(#(
                    int.bitwise_shift_left(int.bitwise_and(b0, 0x3F), 8) + b1,
                    rest,
                  ))
                _ -> Error("link payload: truncated 2-byte length in " <> what)
              }
            False ->
              case rest {
                <<b1:size(8), b2:size(8), b3:size(8), rest:bits>> ->
                  Ok(#(
                    int.bitwise_shift_left(int.bitwise_and(b0, 0x3F), 24)
                      + int.bitwise_shift_left(b1, 16)
                      + int.bitwise_shift_left(b2, 8)
                      + b3,
                    rest,
                  ))
                _ -> Error("link payload: truncated 4-byte length in " <> what)
              }
          }
      }
    _ -> Error("link payload: truncated length in " <> what)
  }
}

fn decode_string(
  input: BitArray,
  what: String,
) -> Result(#(String, BitArray), String) {
  use #(len, rest) <- result.try(decode_varlen(input, what))
  case bit_array.slice(rest, 0, len) {
    Error(_) -> Error("link payload: truncated " <> what)
    Ok(bytes) ->
      case bit_array.to_string(bytes) {
        Error(_) -> Error("link payload: invalid UTF-8 in " <> what)
        Ok(text) ->
          case
            bit_array.slice(rest, len, bit_array.byte_size(rest) - len)
          {
            Ok(remaining) -> Ok(#(text, remaining))
            Error(_) -> Error("link payload: bad slice after " <> what)
          }
      }
  }
}
