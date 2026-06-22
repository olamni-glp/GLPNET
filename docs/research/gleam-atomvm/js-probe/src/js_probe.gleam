//// js-probe — the JavaScript-backend evidence for the build-target matrix (US4, 031 spike).
////
//// The full hello-glp-term smoke does NOT compile to JS (it uses gleam_erlang BEAM processes,
//// which are Erlang/BEAM-only). This minimal sibling contains ONLY the pure functional subset
//// (representative GLP term construction + one immutable unbound->bound "bind"), using only
//// gleam_stdlib, so it DOES compile to JS and run on node. It mirrors the functional-sibling
//// model in ../hello-glp-term/src/hello_glp_term.gleam.

import gleam/int
import gleam/io
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/string

pub type Term {
  Atom(name: String)
  Int(value: Int)
  Struct(functor: String, args: List(Term))
  Var(id: Int)
}

pub fn term_to_string(term: Term) -> String {
  case term {
    Atom(name) -> name
    Int(value) -> int.to_string(value)
    Var(id) -> "_G" <> int.to_string(id)
    Struct(functor, args) ->
      functor
      <> "("
      <> args |> list.map(term_to_string) |> string.join(", ")
      <> ")"
  }
}

fn show_opt(value: Option(Term)) -> String {
  case value {
    None -> "unbound"
    Some(term) -> term_to_string(term)
  }
}

pub fn main() {
  let term = Struct("pair", [Atom("label"), Var(0)])
  io.println("== js functional subset (no BEAM processes) ==")
  io.println("representative term : " <> term_to_string(term))
  // one immutable unbound->bound transition: heap0 is never mutated
  let heap0 = None
  let heap1 = Some(Atom("bound_atom"))
  io.println("heap0 (unbound)     : " <> show_opt(heap0))
  io.println("heap1 (bound)       : " <> show_opt(heap1))
  io.println("heap0 re-read       : " <> show_opt(heap0))
}
