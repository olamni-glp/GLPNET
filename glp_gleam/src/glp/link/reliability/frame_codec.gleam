//// glp/link/reliability/frame_codec — the wire-format frame codec (feature 050 T046).
////
//// Byte-for-byte parity port of `glp_runtime/lib/link/reliability/frame_codec.dart`
//// and `csharp/glp_link/reliability/FrameCodec.cs` (FR-013, SC-004): a fixed 22-byte
//// big-endian header (version, kind, message-id, total-length, fragment index/count,
//// chunk CRC-32, chunk length) followed by the chunk bytes, plus fragmentation for
//// payloads that exceed an MTU. The wrapped payload is the opaque 038 TLV term blob
//// (`glp/codec/term_codec`); this layer knows nothing about terms.
////
//// Big-endian throughout so the Dart / C# / Gleam frames are identical on the wire
//// (FR-060/061). `message_id` is caller-supplied (the link's outbound counter), NOT
//// generated here, so encoding is deterministic and replayable for the seeded
//// loopback / parity tests.
////
//// Errors are returned as data (`Result(_, FrameError)`), never thrown — mirroring
//// the 038 `term_codec` loud-fail discipline (FR-005/FR-015). Every rejection the
//// Dart/C# codec throws on has a corresponding `FrameError` variant, checked in the
//// same order so identical input is accepted/rejected identically across runtimes.
//// (Fault-as-DATA at the GLP boundary — turning a rejection into a GLP fault term —
//// is T052; here the codec just reports the structured error.)

import gleam/bit_array
import gleam/bool
import gleam/list
import gleam/option.{type Option, None, Some}
import glp/link/reliability/crc32

/// Current wire version. A frame with any other version byte is rejected.
pub const version = 0x01

/// Fixed header size in bytes (field layout in the module doc).
pub const header_size = 22

/// Upper bound on a single payload (FR-028 deserializer hardening): a frame
/// advertising a larger `total_length` is rejected before any allocation, so a
/// forged length cannot exhaust memory. `64 * 1024 * 1024`.
pub const max_payload_bytes = 67_108_864

/// The on-wire frame kind. Byte value: `Whole` = 0, `Fragment` = 1.
pub type FrameKind {
  /// A whole payload in one frame (fits under MTU).
  Whole
  /// One fragment of a payload split across several frames (FR-022).
  Fragment
}

/// A parsed, CRC-validated frame: its header fields plus its chunk bytes.
///
/// - `message_id`: groups the fragments of one logical payload.
/// - `total_length`: full payload byte length (identical across a message's fragments).
/// - `frag_index`: 0-based fragment position (0 for `Whole`).
/// - `frag_count`: number of fragments (1 for `Whole`).
/// - `chunk`: this frame's payload slice (already CRC-verified).
pub type ParsedFrame {
  ParsedFrame(
    kind: FrameKind,
    message_id: Int,
    total_length: Int,
    frag_index: Int,
    frag_count: Int,
    chunk: BitArray,
  )
}

/// Loud-fail result for any malformed frame or out-of-range encode request.
/// Each variant mirrors a `FrameException` throw in the Dart/C# codec.
pub type FrameError {
  /// Encode: payload larger than `max_payload_bytes`.
  PayloadTooLarge(size: Int, max: Int)
  /// Encode: `max_frame_bytes` too small to hold even the header.
  MaxFrameTooSmall(max_frame_bytes: Int, header_size: Int)
  /// Encode: payload needs more than 65535 fragments under the given MTU.
  TooManyFragments(total: Int, needed: Int, max_frame_bytes: Int, max: Int)
  /// Parse: frame shorter than the fixed header.
  FrameTooShort(length: Int, header_size: Int)
  /// Parse: version byte is not `version`.
  UnsupportedVersion(found: Int, expected: Int)
  /// Parse: kind byte is neither `Whole` (0) nor `Fragment` (1).
  UnknownKind(kind: Int)
  /// Parse: advertised total length exceeds `max_payload_bytes`.
  TotalLengthExceedsMax(total: Int, max: Int)
  /// Parse: fragment count is 0.
  ZeroFragCount
  /// Parse: fragment index >= fragment count.
  FragIndexOutOfRange(index: Int, count: Int)
  /// Parse: a `Whole` frame with a fragment count other than 1.
  WholeWithMultipleFragments(count: Int)
  /// Parse: frame length != header + advertised chunk length.
  FrameLengthMismatch(actual: Int, expected: Int)
  /// Parse: chunk CRC-32 does not match the header's advertised CRC.
  CrcMismatch(got: Int, expected: Int)
}

fn kind_to_byte(k: FrameKind) -> Int {
  case k {
    Whole -> 0
    Fragment -> 1
  }
}

/// Encode `payload` into one or more self-delimiting frames: a single `Whole` frame
/// when it fits under `max_frame_bytes` (or when that is `None`), otherwise `Fragment`
/// frames each `<= max_frame_bytes` (header included). `message_id` groups this
/// payload's frames.
pub fn encode(
  payload: BitArray,
  message_id: Int,
  max_frame_bytes: Option(Int),
) -> Result(List(BitArray), FrameError) {
  let total = bit_array.byte_size(payload)
  use <- bool.guard(
    total > max_payload_bytes,
    Error(PayloadTooLarge(total, max_payload_bytes)),
  )

  let fits_whole = case max_frame_bytes {
    None -> True
    Some(max) -> total + header_size <= max
  }
  case fits_whole {
    True ->
      Ok([build_frame(Whole, message_id, total, 0, 1, slice(payload, 0, total))])
    False -> {
      // Only reachable with a Some(max) that failed the fits_whole test.
      let assert Some(max) = max_frame_bytes
      let max_chunk = max - header_size
      use <- bool.guard(
        max_chunk <= 0,
        Error(MaxFrameTooSmall(max, header_size)),
      )
      let frag_count = case total == 0 {
        True -> 1
        False -> { total + max_chunk - 1 } / max_chunk
      }
      use <- bool.guard(
        frag_count > 0xFFFF,
        Error(TooManyFragments(total, frag_count, max, 0xFFFF)),
      )
      Ok(build_fragments(payload, message_id, total, max_chunk, frag_count, 0, []))
    }
  }
}

fn build_fragments(
  payload: BitArray,
  message_id: Int,
  total: Int,
  max_chunk: Int,
  frag_count: Int,
  i: Int,
  acc: List(BitArray),
) -> List(BitArray) {
  case i >= frag_count {
    True -> list.reverse(acc)
    False -> {
      let start = i * max_chunk
      let remaining = total - start
      let len = case max_chunk < remaining {
        True -> max_chunk
        False -> remaining
      }
      let frame =
        build_frame(
          Fragment,
          message_id,
          total,
          i,
          frag_count,
          slice(payload, start, len),
        )
      build_fragments(
        payload,
        message_id,
        total,
        max_chunk,
        frag_count,
        i + 1,
        [frame, ..acc],
      )
    }
  }
}

fn build_frame(
  kind: FrameKind,
  message_id: Int,
  total_len: Int,
  frag_index: Int,
  frag_count: Int,
  chunk: BitArray,
) -> BitArray {
  let kind_byte = kind_to_byte(kind)
  let chunk_len = bit_array.byte_size(chunk)
  let chunk_crc = crc32.compute(chunk)
  <<
    version:int,
    kind_byte:int,
    message_id:int-size(32)-big,
    total_len:int-size(32)-big,
    frag_index:int-size(16)-big,
    frag_count:int-size(16)-big,
    chunk_crc:int-size(32)-big,
    chunk_len:int-size(32)-big,
    chunk:bits,
  >>
}

/// Parse and validate one frame: version byte, CRC-32 over the chunk, and
/// header/length consistency — in the same order as the Dart/C# codec. Any mismatch
/// is a `FrameError` (never a silent mis-decode).
pub fn parse_frame(frame: BitArray) -> Result(ParsedFrame, FrameError) {
  let frame_len = bit_array.byte_size(frame)
  use <- bool.guard(
    frame_len < header_size,
    Error(FrameTooShort(frame_len, header_size)),
  )
  case frame {
    <<
      version_byte:int,
      kind_byte:int,
      message_id:int-size(32)-big,
      total_len:int-size(32)-big,
      frag_index:int-size(16)-big,
      frag_count:int-size(16)-big,
      chunk_crc:int-size(32)-big,
      chunk_len:int-size(32)-big,
      rest:bits,
    >> -> {
      use <- bool.guard(
        version_byte != version,
        Error(UnsupportedVersion(version_byte, version)),
      )
      use <- bool.guard(
        kind_byte != 0 && kind_byte != 1,
        Error(UnknownKind(kind_byte)),
      )
      let kind = case kind_byte == 0 {
        True -> Whole
        False -> Fragment
      }
      use <- bool.guard(
        total_len > max_payload_bytes,
        Error(TotalLengthExceedsMax(total_len, max_payload_bytes)),
      )
      use <- bool.guard(frag_count == 0, Error(ZeroFragCount))
      use <- bool.guard(
        frag_index >= frag_count,
        Error(FragIndexOutOfRange(frag_index, frag_count)),
      )
      use <- bool.guard(
        kind == Whole && frag_count != 1,
        Error(WholeWithMultipleFragments(frag_count)),
      )
      let expected = header_size + chunk_len
      use <- bool.guard(
        frame_len != expected,
        Error(FrameLengthMismatch(frame_len, expected)),
      )
      let actual_crc = crc32.compute(rest)
      use <- bool.guard(
        actual_crc != chunk_crc,
        Error(CrcMismatch(actual_crc, chunk_crc)),
      )
      Ok(ParsedFrame(
        kind,
        message_id,
        total_len,
        frag_index,
        frag_count,
        rest,
      ))
    }
    // Unreachable: frame_len >= header_size guarantees the header pattern matches.
    _ -> Error(FrameTooShort(frame_len, header_size))
  }
}

// Slice that cannot fail given the encode-side math (indices are always in range);
// a failure here is an internal invariant violation, so it crashes loudly rather
// than silently mis-encoding (Dart throws RangeError in the same spot).
fn slice(data: BitArray, at: Int, len: Int) -> BitArray {
  let assert Ok(s) = bit_array.slice(data, at, len)
  s
}
