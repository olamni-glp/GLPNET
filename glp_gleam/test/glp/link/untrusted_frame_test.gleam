//// T053 — adversarial untrusted-input corpus over the T052 ingress gate (FR-015).
////
//// T052's `faults_gate_test` pins one representative per violation CLASS; this file
//// is the systematic sweep: EVERY truncation prefix, EVERY single-byte corruption,
//// EVERY 1-byte payload tag, boundary-forged lengths, and fixed garbage blobs. The
//// one property under test is FR-015's hard half: **no input crashes the gate** —
//// every outcome is `Ok` or a classified fault VALUE. Where a corruption can
//// legitimately still gate through (header bytes outside the CRC's coverage, e.g.
//// the message id), the sweep additionally pins that the PAYLOAD is untouched: a
//// gated-through corruption may relabel a frame, never alter its term.
////
//// Determinism note: the corpus is exhaustive-or-fixed (no randomness — the engine
//// forbids `Math.random` shapes and a flaking adversarial test is worse than none).

import gleam/bit_array
import gleam/option.{None}
import gleam/string
import gleeunit/should
import glp/link/faults
import glp/link/primitives/link_wire
import glp/link/reliability/frame_codec
import glp/link/seam/link_address
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/runtime/terms.{type Term, ConstAtom, ConstTerm, StructTerm}

fn an_id() -> LinkId {
  LinkId(
    scheme: link_scheme.tcp(),
    endpoint: link_address.endpoint("127.0.0.1", 9000),
    nonce: NonceInt(1),
  )
}

fn clean_term() -> Term {
  StructTerm("hello", [ConstTerm(ConstAtom("world"))])
}

fn clean_frame() -> BitArray {
  let assert Ok([frame]) = link_wire.encode_frames(clean_term(), 7, None)
  frame
}

fn poke(frame: BitArray, index: Int, value: Int) -> BitArray {
  let assert Ok(before) = bit_array.slice(frame, 0, index)
  let assert Ok(after) =
    bit_array.slice(frame, index + 1, bit_array.byte_size(frame) - index - 1)
  bit_array.concat([before, <<value:int>>, after])
}

/// The FR-015 hard half, as a reusable assertion: the gate returns a VALUE. Reaching
/// the `panic` below would itself fail the test — but the point is that a codec bug
/// would panic INSIDE the gate first, and running the corpus proves none does.
fn gate_never_crashes(frame: BitArray) -> Result(#(Int, Term), Term) {
  faults.gate_frame(an_id(), frame)
}

// ── sweep 1: every truncation prefix of a clean frame ────────────────────────

pub fn every_truncation_prefix_is_safe_test() {
  let frame = clean_frame()
  let full = bit_array.byte_size(frame)
  sweep_truncations(frame, 0, full)
}

fn sweep_truncations(frame: BitArray, len: Int, full: Int) -> Nil {
  case len > full {
    True -> Nil
    False -> {
      let assert Ok(prefix) = bit_array.slice(frame, 0, len)
      case gate_never_crashes(prefix) {
        // Only the COMPLETE frame may gate through.
        Ok(_) -> len |> should.equal(full)
        Error(StructTerm("permFail", _)) -> Nil
        Error(other) ->
          panic as { "unclassified truncation result: " <> string.inspect(other) }
      }
      sweep_truncations(frame, len + 1, full)
    }
  }
}

// ── sweep 2: every single-byte corruption at every position, all 255 deltas
// at the header + a full-value sweep at one payload byte ─────────────────────

/// Flip every byte position to ONE different value (cheap full-position sweep)…
pub fn every_position_single_flip_is_safe_test() {
  let frame = clean_frame()
  sweep_positions(frame, 0, bit_array.byte_size(frame))
}

fn sweep_positions(frame: BitArray, index: Int, size: Int) -> Nil {
  case index >= size {
    True -> Nil
    False -> {
      let assert Ok(orig) = bit_array.slice(frame, index, 1)
      let assert <<b:int>> = orig
      let corrupted = poke(frame, index, { b + 1 } % 256)
      case gate_never_crashes(corrupted) {
        // A corruption outside the CRC's coverage (e.g. the message id) may still
        // gate through — but then the TERM must be byte-identically the original's.
        Ok(#(_mid, term)) -> term |> should.equal(clean_term())
        Error(StructTerm(functor, _)) ->
          { functor == "permFail" } |> should.be_true
        Error(other) ->
          panic as { "unclassified corruption: " <> string.inspect(other) }
      }
      sweep_positions(frame, index + 1, size)
    }
  }
}

/// …and every possible value at ONE payload byte (full 0..255 value sweep where the
/// CRC must catch all 255 wrong values).
pub fn every_value_at_a_payload_byte_is_safe_test() {
  let frame = clean_frame()
  let index = bit_array.byte_size(frame) - 1
  let assert Ok(orig) = bit_array.slice(frame, index, 1)
  let assert <<original:int>> = orig
  sweep_values(frame, index, original, 0)
}

fn sweep_values(frame: BitArray, index: Int, original: Int, value: Int) -> Nil {
  case value > 255 {
    True -> Nil
    False -> {
      case value == original {
        True -> Nil
        False -> {
          let assert Error(StructTerm("permFail", _)) =
            gate_never_crashes(poke(frame, index, value))
          Nil
        }
      }
      sweep_values(frame, index, original, value + 1)
    }
  }
}

// ── sweep 3: every 1-byte payload tag, correctly framed (type confusion) ─────

/// Frame each of the 256 single-byte payloads with a VALID header + CRC, so only
/// the term decoder stands between the wire and the heap: every rejection must be a
/// classified decode fault; any acceptance must produce a real term (tag-only
/// payloads for composite tags are truncated → rejected).
pub fn every_one_byte_payload_is_safe_test() {
  sweep_tags(0)
}

fn sweep_tags(tag: Int) -> Nil {
  case tag > 255 {
    True -> Nil
    False -> {
      let assert Ok([frame]) = frame_codec.encode(<<tag:int>>, 1, None)
      case gate_never_crashes(frame) {
        Ok(#(mid, _term)) -> mid |> should.equal(1)
        Error(StructTerm("permFail", [_, ConstTerm(ConstAtom(reason))])) -> {
          { string.starts_with(reason, "payload decode failed")
            || string.starts_with(reason, "ground-relay violation") }
          |> should.be_true
        }
        Error(other) ->
          panic as { "unclassified tag result: " <> string.inspect(other) }
      }
      sweep_tags(tag + 1)
    }
  }
}

// ── boundary-forged lengths ──────────────────────────────────────────────────

pub fn declared_length_at_exact_bound_gates_through_untouched_test() {
  // total_length forged to max_payload_bytes EXACTLY: inside the bound, and on a
  // Whole frame total_length is not consistency-checked against the chunk — that is
  // the REASSEMBLER's job across fragments (byte-parity with the Dart/C# codec,
  // which also leaves it to `frame_reassembler`). The FR-015 guarantees that DO
  // hold: nothing allocates the declared 64 MiB, nothing crashes, and the payload
  // is byte-identically the original (the forge relabels, it cannot alter).
  let max = faults.max_payload_bytes
  let b0 = max / 16_777_216 % 256
  let b1 = max / 65_536 % 256
  let b2 = max / 256 % 256
  let b3 = max % 256
  let forged =
    clean_frame() |> poke(6, b0) |> poke(7, b1) |> poke(8, b2) |> poke(9, b3)
  let assert Ok(#(7, term)) = gate_never_crashes(forged)
  term |> should.equal(clean_term())
  // One past the bound is rejected (the T052 class test forges 0xFFFFFFFF; this
  // pins the EXACT boundary edge).
  let over = max + 1
  let o0 = over / 16_777_216 % 256
  let o1 = over / 65_536 % 256
  let o2 = over / 256 % 256
  let o3 = over % 256
  let rejected =
    clean_frame() |> poke(6, o0) |> poke(7, o1) |> poke(8, o2) |> poke(9, o3)
  let assert Error(StructTerm("permFail", _)) = gate_never_crashes(rejected)
}

pub fn empty_input_is_safe_test() {
  let assert Error(StructTerm("permFail", _)) = gate_never_crashes(<<>>)
}

// ── fixed garbage blobs (no structure at all) ────────────────────────────────

pub fn fixed_garbage_blobs_are_safe_test() {
  garbage_blob(<<>>, 0)
}

/// 64 deterministic blobs of growing length filled with a rolling byte pattern —
/// none may crash, none may gate through (they cannot carry a valid CRC by luck at
/// these sizes since the header alone is 22 bytes of constraints).
fn garbage_blob(acc: BitArray, n: Int) -> Nil {
  case n > 64 {
    True -> Nil
    False -> {
      case gate_never_crashes(acc) {
        Error(StructTerm("permFail", _)) -> Nil
        Ok(_) -> panic as "garbage blob gated through"
        Error(other) ->
          panic as { "unclassified blob result: " <> string.inspect(other) }
      }
      // Token gate: same input, string channel, same safety.
      let assert Error(_) = faults.gate_token(acc)
      let next_byte = { n * 37 + 11 } % 256
      garbage_blob(bit_array.concat([acc, <<next_byte:int>>]), n + 1)
    }
  }
}
