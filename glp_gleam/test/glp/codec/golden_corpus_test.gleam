//// Golden byte-identity — Gleam reproduces the PINNED corpus.hex (SC-002, T027) and
//// decodes the Dart-authored golden bytes back to the corpus envelope (Acceptance #2
//// cross-decode, T028). corpus.hex is authored from the Dart source-of-truth encoder
//// (R9); this test READS the file (not inlined bytes) so it is a drift guard — if Dart's
//// bytes change, Gleam fails here. Only NON-gated entries are byte-final (gated float /
//// 64-bit-int edges excluded — R11/R6); captured is masked (empty) per R4.
////
//// The golden file is read via a direct OTP `file:read_file/1` FFI (no new dependency);
//// hex is parsed with `int.base_parse` (case-insensitive).

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/int
import gleam/list
import gleam/option.{None, Some}
import gleam/string
import gleeunit/should
import glp/codec/result_envelope.{
  type ResultEnvelope, Failed, ResultEnvelope, Success, Suspended, decode, encode,
}
import glp/codec/term_codec.{
  type Term, ConstAtom, ConstInt, ConstString, ConstTerm, GlobalVarId, StructTerm,
  VarRef,
}

// glp_gleam/ and specs/ are siblings; `gleam test` runs from the package root.
const golden_path = "../specs/038-result-codec-and-framecodec-ride/contracts/golden/corpus.hex"

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

// --- term builders (mirror corpus.dart) ------------------------------------

fn atom(a: String) -> Term {
  ConstTerm(ConstAtom(a))
}

fn int_(n: Int) -> Term {
  ConstTerm(ConstInt(n))
}

fn str(s: String) -> Term {
  ConstTerm(ConstString(s))
}

fn var_ref(agent: String, local: Int) -> Term {
  VarRef(GlobalVarId(agent, local))
}

fn glp_list(elems: List(Term)) -> Term {
  list.fold_right(elems, atom("nil"), fn(acc, e) { StructTerm(".", [e, acc]) })
}

fn success(bindings: List(#(String, Term))) -> ResultEnvelope {
  ResultEnvelope(Success, bindings, [], [], <<>>, None)
}

// --- the non-gated corpus (1:1 with corpus.dart, minus the masked `with_captured`) --

fn non_gated_corpus() -> List(#(String, ResultEnvelope)) {
  [
    #("empty_success", ResultEnvelope(Success, [], [], [], <<>>, None)),
    #("success_atom", success([#("X", atom("foo"))])),
    #("success_int", success([#("N", int_(42))])),
    #("success_int_negative", success([#("N", int_(-7))])),
    #("success_string", success([#("S", str("hello, world"))])),
    #(
      "success_nested_struct",
      success([#("T", StructTerm("point", [int_(1), int_(2)]))]),
    ),
    #("success_list", success([#("L", glp_list([int_(1), int_(2), int_(3)]))])),
    #(
      "success_deep_nested",
      success([
        #(
          "Tree",
          StructTerm("node", [
            StructTerm("node", [atom("leaf"), int_(1)]),
            StructTerm("node", [atom("leaf"), int_(2)]),
          ]),
        ),
      ]),
    ),
    #(
      "multi_binding",
      success([
        #("A", atom("alpha")),
        #("B", int_(2)),
        #("C", str("gamma")),
      ]),
    ),
    #(
      "var_to_writer",
      ResultEnvelope(
        Success,
        [#("Z", StructTerm("pair", [atom("a"), var_ref("agent1", 9)]))],
        [#("W", GlobalVarId("agent1", 9))],
        [],
        <<>>,
        None,
      ),
    ),
    #(
      "suspended",
      ResultEnvelope(
        Suspended,
        [],
        [],
        [GlobalVarId("agent1", 3), GlobalVarId("agent2", 5)],
        <<>>,
        None,
      ),
    ),
    #(
      "suspended_with_binding",
      ResultEnvelope(
        Suspended,
        [#("Partial", StructTerm("waiting_on", [var_ref("agent1", 11)]))],
        [#("Q", GlobalVarId("agent1", 11))],
        [GlobalVarId("agent1", 11)],
        <<>>,
        None,
      ),
    ),
    #(
      "failed",
      ResultEnvelope(
        Failed,
        [],
        [],
        [],
        <<>>,
        Some("no matching clause for goal foo/2"),
      ),
    ),
  ]
}

// --- golden-file loading ----------------------------------------------------

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

// --- T027: encode(corpus[name]) reproduces the pinned golden bytes ----------

pub fn encode_reproduces_pinned_golden_test() {
  let golden = load_golden()
  list.each(non_gated_corpus(), fn(entry) {
    let #(name, env) = entry
    encode(env) |> should.equal(golden_bytes(golden, name))
  })
}

// --- T027: the golden name set is exactly the non-gated corpus (no drift) ----

pub fn golden_covers_non_gated_corpus_test() {
  let golden_names =
    load_golden() |> list.map(fn(g) { g.0 }) |> list.sort(string.compare)
  let corpus_names =
    non_gated_corpus() |> list.map(fn(e) { e.0 }) |> list.sort(string.compare)
  golden_names |> should.equal(corpus_names)
}

// --- T028: cross-decode — Dart-authored golden bytes decode to the envelope --

pub fn decode_of_golden_equals_corpus_test() {
  let golden = load_golden()
  list.each(non_gated_corpus(), fn(entry) {
    let #(name, env) = entry
    decode(golden_bytes(golden, name)) |> should.equal(Ok(env))
  })
}
