//// Frame-codec parity tests (feature 050 T047) — byte parity with
//// `frame_codec.dart` / `FrameCodec.cs`.
////
//// Byte-for-byte cross-runtime parity is pinned by three independent anchors, the
//// same methodology as the reference `FrameCodecTests.cs` + `FrameRideTests.cs`
//// (there is no recorded frame-byte golden in the repo — the reference codecs are
//// verified by round-trip + the canonical CRC vector, not a hard-coded frame dump):
////
////  1. The canonical CRC-32 check value `0xCBF43926` for `"123456789"` — the exact
////     assertion the C# `Crc32_KnownVector` test makes, so the CRC field is provably
////     identical across runtimes.
////  2. An exact wire-layout assertion over a fixed small frame — pins the 22-byte
////     big-endian header field offsets against the shared spec, so a layout/endian
////     drift vs Dart/C# fails here.
////  3. Riding the real 038 term-codec golden payloads (`corpus.hex`, the single
////     source of truth): each payload frame-encodes, parses, and reassembles back
////     byte-for-byte — the payloads the wire actually carries (link-parity.md §Wire).
////
//// Plus the reference codec's rejection cases (bad version, bad CRC, truncated
//// header, MTU-too-small, unknown kind). The exhaustive adversarial matrix
//// (oversized / type-confused / whole-with-fragments) is T053.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/int
import gleam/list
import gleam/option.{None, Some}
import gleam/string
import gleeunit/should
import glp/link/reliability/crc32
import glp/link/reliability/frame_codec.{Fragment, Whole}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/// Deterministic byte sequence, identical to the reference tests' `Seq(n)`:
/// `b[i] = (i * 31 + 7) mod 256`.
fn seq(n: Int) -> BitArray {
  seq_loop(0, n, <<>>)
}

fn seq_loop(i: Int, n: Int, acc: BitArray) -> BitArray {
  case i >= n {
    True -> acc
    False -> seq_loop(i + 1, n, <<acc:bits, { i * 31 + 7 }:int>>)
  }
}

/// Concatenate parsed frame chunks in delivery order (the frames come from `encode`
/// already in fragment order). A minimal reassembly for the parity round-trip — the
/// real reorder/dedup reassembler is a downstream concern.
fn reassemble(frames: List(BitArray)) -> BitArray {
  list.fold(frames, <<>>, fn(acc, f) {
    let assert Ok(pf) = frame_codec.parse_frame(f)
    <<acc:bits, pf.chunk:bits>>
  })
}

fn set_byte(frame: BitArray, index: Int, value: Int) -> BitArray {
  let n = bit_array.byte_size(frame)
  let assert Ok(before) = bit_array.slice(frame, 0, index)
  let assert Ok(after) = bit_array.slice(frame, index + 1, n - index - 1)
  <<before:bits, value:int, after:bits>>
}

// ---------------------------------------------------------------------------
// Anchor 1 — CRC-32 parity
// ---------------------------------------------------------------------------

pub fn crc32_canonical_vector_test() {
  // The canonical CRC-32 check value for "123456789" — the same anchor as the C#
  // Crc32_KnownVector test, so the CRC field is byte-identical across runtimes.
  crc32.compute(bit_array.from_string("123456789"))
  |> should.equal(0xCBF43926)
}

pub fn crc32_empty_is_zero_test() {
  crc32.compute(<<>>)
  |> should.equal(0)
}

// ---------------------------------------------------------------------------
// Anchor 2 — exact wire layout (22-byte big-endian header)
// ---------------------------------------------------------------------------

pub fn whole_frame_exact_layout_test() {
  let payload = seq(4)
  let message_id = 0x01020304
  let crc = crc32.compute(payload)
  let expected = <<
    0x01:int,
    // version
    0x00:int,
    // kind = Whole
    message_id:int-size(32)-big,
    4:int-size(32)-big,
    // total length
    0:int-size(16)-big,
    // frag index
    1:int-size(16)-big,
    // frag count
    crc:int-size(32)-big,
    4:int-size(32)-big,
    // chunk length
    payload:bits,
  >>
  frame_codec.encode(payload, message_id, None)
  |> should.equal(Ok([expected]))
}

// ---------------------------------------------------------------------------
// Round-trips (mirror FrameCodecTests.cs)
// ---------------------------------------------------------------------------

pub fn whole_round_trip_test() {
  let payload = seq(200)
  let assert Ok([frame]) = frame_codec.encode(payload, 42, None)
  let assert Ok(parsed) = frame_codec.parse_frame(frame)
  parsed.kind |> should.equal(Whole)
  parsed.message_id |> should.equal(42)
  parsed.chunk |> should.equal(payload)
}

pub fn empty_round_trip_test() {
  let assert Ok(frames) = frame_codec.encode(<<>>, 1, None)
  frames |> list.length |> should.equal(1)
  reassemble(frames) |> should.equal(<<>>)
}

pub fn fragmentation_round_trip_under_mtu_test() {
  let payload = seq(1000)
  let mtu = 128
  let assert Ok(frames) = frame_codec.encode(payload, 7, Some(mtu))

  // more than one fragment, each within the MTU, each tagged Fragment
  { list.length(frames) > 1 } |> should.be_true
  list.each(frames, fn(f) {
    { bit_array.byte_size(f) <= mtu } |> should.be_true
    let assert Ok(pf) = frame_codec.parse_frame(f)
    pf.kind |> should.equal(Fragment)
  })

  reassemble(frames) |> should.equal(payload)
}

// ---------------------------------------------------------------------------
// Anchor 3 — ride the real 038 golden payloads (single source of truth)
// ---------------------------------------------------------------------------

pub fn golden_payloads_ride_whole_test() {
  load_golden()
  |> list.each(fn(entry) {
    let #(_name, payload) = entry
    let assert Ok(frames) = frame_codec.encode(payload, 0x2929, None)
    frames |> list.length |> should.equal(1)
    reassemble(frames) |> should.equal(payload)
  })
}

pub fn golden_payloads_ride_fragmented_test() {
  // Force fragmentation with a tiny MTU (header + 8 chunk bytes) so every non-trivial
  // golden payload splits, then reassembles byte-for-byte.
  let mtu = frame_codec.header_size + 8
  load_golden()
  |> list.each(fn(entry) {
    let #(_name, payload) = entry
    let assert Ok(frames) = frame_codec.encode(payload, 0x2930, Some(mtu))
    reassemble(frames) |> should.equal(payload)
  })
}

// ---------------------------------------------------------------------------
// Rejection parity (mirror FrameCodecTests.cs)
// ---------------------------------------------------------------------------

pub fn bad_version_rejected_test() {
  let assert Ok([frame]) = frame_codec.encode(seq(10), 1, None)
  frame_codec.parse_frame(set_byte(frame, 0, 0x99))
  |> should.equal(Error(frame_codec.UnsupportedVersion(0x99, 0x01)))
}

pub fn bad_crc_rejected_test() {
  let assert Ok([frame]) = frame_codec.encode(seq(10), 1, None)
  let n = bit_array.byte_size(frame)
  let assert Ok(<<last:int>>) = bit_array.slice(frame, n - 1, 1)
  let corrupted = set_byte(frame, n - 1, int.bitwise_exclusive_or(last, 0xFF))
  case frame_codec.parse_frame(corrupted) {
    Error(frame_codec.CrcMismatch(_, _)) -> should.be_true(True)
    other -> should.equal(other, Error(frame_codec.CrcMismatch(0, 0)))
  }
}

pub fn truncated_header_rejected_test() {
  frame_codec.parse_frame(<<0, 0, 0, 0, 0>>)
  |> should.equal(Error(frame_codec.FrameTooShort(5, 22)))
}

pub fn mtu_smaller_than_header_rejected_test() {
  frame_codec.encode(seq(100), 1, Some(frame_codec.header_size - 1))
  |> should.equal(Error(frame_codec.MaxFrameTooSmall(21, 22)))
}

pub fn unknown_kind_rejected_test() {
  // A valid whole frame whose kind byte (offset 1) is corrupted to 5 is rejected
  // before the CRC check (rejection order parity with the reference codec).
  let assert Ok([frame]) = frame_codec.encode(seq(10), 1, None)
  frame_codec.parse_frame(set_byte(frame, 1, 5))
  |> should.equal(Error(frame_codec.UnknownKind(5)))
}

// ---------------------------------------------------------------------------
// Golden-file loading (reuses the corpus.hex single source of truth, same
// method as test/glp/codec/golden_corpus_test.gleam)
// ---------------------------------------------------------------------------

const golden_path = "../specs/038-result-codec-and-framecodec-ride/contracts/golden/corpus.hex"

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn hex_to_bytes(hex: String) -> BitArray {
  hex
  |> string.to_graphemes
  |> list.sized_chunk(into: 2)
  |> list.fold(<<>>, fn(acc, pair) {
    let assert Ok(b) = int.base_parse(string.concat(pair), 16)
    <<acc:bits, b>>
  })
}

fn load_golden() -> List(#(String, BitArray)) {
  let assert Ok(contents) = read_file(golden_path)
  let assert Ok(text) = bit_array.to_string(contents)
  text
  |> string.split("\n")
  |> list.filter_map(fn(line) {
    case string.trim(line) {
      "" -> Error(Nil)
      t ->
        case string.split_once(t, " ") {
          Ok(#(name, hex)) -> Ok(#(name, hex_to_bytes(hex)))
          Error(_) -> Error(Nil)
        }
    }
  })
}
