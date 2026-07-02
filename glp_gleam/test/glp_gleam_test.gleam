import gleam/int
import gleeunit
import gleeunit/should

pub fn main() {
  gleeunit.main()
}

/// Smoke (FR-003, SC-002): proves the glp_gleam subtree builds to Erlang/BEAM and
/// its gleeunit suite runs green end-to-end. It exercises the build-and-run path
/// only — there are no ported GLP runtime semantics to test yet (those land in F4+).
/// The 8 placeholder modules under src/glp/ compile as part of the build regardless
/// of whether anything imports them.
pub fn build_and_run_smoke_test() {
  int.to_string(40 + 2)
  |> should.equal("42")
}
