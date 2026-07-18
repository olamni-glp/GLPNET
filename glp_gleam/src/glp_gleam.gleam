//// glp_gleam — the `gleam run` entry point (feature 050, T031; US2).
////
//// A thin shell over the REPL loop, so the loop + command semantics stay
//// unit-testable (glp/repl/repl, glp/repl/commands) without a live stdin. Run the
//// standalone Gleam GLP instance with `gleam run` (scripted: pipe newline-fed
//// `load …` + goals on stdin).

import glp/repl/repl

pub fn main() -> Nil {
  repl.run()
}
