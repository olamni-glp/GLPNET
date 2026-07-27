//// Envelope-identity test (feature 050, T036; US2 — FR-009 / SC-004 corollary).
////
//// The ED-1 seam guarantee: a goal's `ResultEnvelope` is byte-identical whether
//// consumed in-process (the REPL) or over the wire (encode→decode). We run a goal
//// through the engine, then assert (1) the wire round-trip is byte-stable
//// (`encode(decode(encode(env))) == encode(env)`) and (2) the in-process rendering
//// equals the decoded-from-wire rendering — so the REPL user and a link peer see
//// the same result for the same computation.
////
//// GOLDEN SEAM CORPUS (059 wave-1 guard-fe-be-envelope-seam, b2-c1-001).
//// The two identity tests above prove the seam is *internally* consistent, but they
//// pass for ANY wire format as long as encode/decode/render agree — so a silent
//// envelope-shape change ahead of the wave-4 FE/BE process split would not trip them.
//// The tests below close that gap: they pin the actual encoded bytes for a corpus of
//// engine-produced envelopes to a checked-in golden file
//// (`test/glp/repl/envelope_seam_golden.hex`). Any drift in the delivered ED-1 seam —
//// header, status byte, term encoding, error framing — fails the suite. The golden
//// bytes are the delivered Gleam engine+codec output (the freeze baseline); an
//// intentional seam change must regenerate them through an explicit unfreeze ruling,
//// never a silent edit. The corpus covers the two engine-producible seam shapes:
//// Success carrying a bound integer, and Failed carrying an error string; Suspended is
//// frozen on the codec side by codec/golden_corpus_test over hand-built envelopes.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/int
import gleam/list
import gleam/string
import gleeunit/should
import glp/codec/result_envelope
import glp/engine
import glp/repl/results

// A bound result (X := 2+3 → X = 5) is byte-identical across the wire, and renders
// the same in-process and after decode.
pub fn success_envelope_identity_test() {
  let e = engine.new()
  let #(_e, env) = engine.run(e, "X := 2+3")

  let bytes = result_envelope.encode(env)
  let assert Ok(decoded) = result_envelope.decode(bytes)

  // Wire round-trip is byte-stable.
  result_envelope.encode(decoded)
  |> should.equal(bytes)
  // In-process vs decoded-from-wire render identically (the ED-1 seam).
  results.render_outcome(decoded)
  |> should.equal(results.render_outcome(env))
  // And that shared rendering is the expected outcome.
  results.render_outcome(env)
  |> should.equal(["X = 5", "→ succeeds"])
}

// A failure envelope (unknown predicate) carries its error string identically
// across the wire — the error is part of the seam, not a local REPL string.
pub fn failure_envelope_identity_test() {
  let e = engine.new()
  let #(_e, env) = engine.run(e, "no_such_pred(1)")

  let bytes = result_envelope.encode(env)
  let assert Ok(decoded) = result_envelope.decode(bytes)

  result_envelope.encode(decoded)
  |> should.equal(bytes)
  results.render_outcome(decoded)
  |> should.equal(results.render_outcome(env))
}

// --- golden seam corpus: (name, goal) -------------------------------------
//
// Each goal is run through the engine; the encoded envelope bytes are pinned in
// envelope_seam_golden.hex under the same name. Goals are chosen to be
// deterministic and to exercise the two engine-producible envelope shapes:
//   * Success with a bound ConstInt (small value, and a wider value)
//   * Failed with an error string (arity 1, and arity 2 — a different string)
fn seam_corpus() -> List(#(String, String)) {
  [
    #("success_arith_bind", "X := 2+3"),
    #("success_arith_mul", "X := 6*7"),
    #("failure_unknown_pred_1", "no_such_pred(1)"),
    #("failure_unknown_pred_2", "no_such_pred(1, 2)"),
  ]
}

// --- golden-file loading (house style; mirrors codec/golden_corpus_test) ----
//
// glp_gleam/ is the package root at test time, so the golden file sits at
// test/glp/repl/. Read via a direct OTP file:read_file FFI (no new dependency);
// hex is parsed with int.base_parse (case-insensitive).

const golden_path = "test/glp/repl/envelope_seam_golden.hex"

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

fn golden_bytes(golden: List(#(String, BitArray)), name: String) -> BitArray {
  let assert Ok(#(_, bytes)) = list.find(golden, fn(g) { g.0 == name })
  bytes
}

// --- the drift guard: engine-produced bytes reproduce the pinned golden ------

pub fn encode_reproduces_pinned_golden_test() {
  let golden = load_golden()
  list.each(seam_corpus(), fn(entry) {
    let #(name, goal) = entry
    let e = engine.new()
    let #(_e, env) = engine.run(e, goal)
    // The delivered seam bytes for this goal are exactly the frozen golden.
    result_envelope.encode(env)
    |> should.equal(golden_bytes(golden, name))
    // And the golden bytes decode back to precisely the engine-produced envelope.
    result_envelope.decode(golden_bytes(golden, name))
    |> should.equal(Ok(env))
  })
}

// --- the golden name set is exactly the corpus (no silent add/drop) ----------

pub fn golden_covers_seam_corpus_test() {
  let golden_names =
    load_golden() |> list.map(fn(g) { g.0 }) |> list.sort(string.compare)
  let corpus_names =
    seam_corpus() |> list.map(fn(e) { e.0 }) |> list.sort(string.compare)
  golden_names
  |> should.equal(corpus_names)
}
