import gleam/option.{None, Some}
import gleeunit
import gleeunit/should
import hello_glp_term.{Atom, Struct, Var}

pub fn main() {
  gleeunit.main()
}

/// The representative term has a compound/structure (pair/2) and an
/// unbound-variable analogue (_G0). (FR-004)
pub fn representative_term_test() {
  hello_glp_term.representative_term()
  |> should.equal(Struct("pair", [Atom("label"), Var(0)]))
}

pub fn term_to_string_test() {
  hello_glp_term.representative_term()
  |> hello_glp_term.term_to_string
  |> should.equal("pair(label, _G0)")
}

/// Process/state-holder model: one unbound→bound transition, reader-observed.
pub fn process_bind_test() {
  let #(before, after) = hello_glp_term.process_bind_demo()
  before |> should.equal(None)
  after |> should.equal(Some(Atom("bound_atom")))
}

/// Functional sibling: heap0 stays unbound (immutability); heap1 is bound.
pub fn functional_bind_test() {
  let #(heap0, heap1) = hello_glp_term.functional_bind_demo()
  heap0 |> should.equal(None)
  heap1 |> should.equal(Some(Atom("bound_atom")))
}
