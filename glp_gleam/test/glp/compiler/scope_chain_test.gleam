//// T078 Part B acceptance — the directory `self.glp` scope chain (§19.6). Two
//// properties, both to Dart `module_hierarchy` parity:
////   1. NEARER-WINS shadowing — a `self.glp` closer to the target module overrides a
////      type it redefines from an outer `self.glp`.
////   2. SIBLING ISOLATION — a `self.glp` in a sibling directory is never in scope.
////
//// Fixtures under `programs/tests/scope_chain/`:
////   self.glp          Widget ::= button ; dial.     (root — outer)
////   sub/self.glp      Widget ::= slider ; knob.      (nearer — shadows root Widget)
////   sub/client.glp    use_widget(slider).            (the target module)
////   other/self.glp    Gadget ::= lever.              (sibling — must be invisible)

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/list
import gleam/string
import gleeunit/should
import glp/compiler/loader
import glp/compiler/module_hierarchy
import glp/engine

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> String {
  let assert Ok(bytes) = read_file(path)
  let assert Ok(text) = bit_array.to_string(bytes)
  text
}

const root = "../programs/tests/scope_chain"

// ── the walk: root-first, siblings excluded ─────────────────────────────────

pub fn discover_self_chain_is_root_first_and_excludes_siblings_test() {
  let chain =
    module_hierarchy.discover_self_chain(root <> "/sub/client.glp", root)
  // Exactly the ancestor self.glp files, ROOT-FIRST.
  list.length(chain) |> should.equal(2)
  let assert [outer, nearer] = chain
  string.ends_with(fwd(outer), "scope_chain/self.glp") |> should.be_true
  string.ends_with(fwd(nearer), "scope_chain/sub/self.glp") |> should.be_true
  // The sibling other/self.glp is NOT collected (sibling isolation).
  list.any(chain, fn(p) { string.contains(fwd(p), "/other/") })
  |> should.be_false
}

fn fwd(p: String) -> String {
  string.replace(p, "\\", "/")
}

// ── nearer-wins shadowing, end to end through the type checker ──────────────

// `sub/client.glp` uses `slider`, which is a `Widget` ONLY under the nearer
// `sub/self.glp`. With the discovered chain merged (root-first), the nearer Widget
// wins, so the module type-checks.
pub fn nearer_self_glp_shadows_outer_and_module_typechecks_test() {
  let prelude = read_source(root <> "/../../self.glp")
  let chain = module_hierarchy.discover_self_chain(root <> "/sub/client.glp", root)
  let ancestor_sources = list.map(chain, read_source)
  let client = read_source(root <> "/sub/client.glp")

  loader.load_with_scope(client, prelude, ancestor_sources)
  |> should.be_ok
}

// Control: WITHOUT the scope chain (`[]`), `Widget` is wholly undefined, so the load
// is rejected with a CLEAN staged error (T073 fix — was a `program_dfa.gleam:580`
// panic; now `UnknownTypeError`, matching Dart's `Error loading …: UnknownTypeError`).
pub fn without_scope_chain_module_is_rejected_test() {
  let prelude = read_source(root <> "/../../self.glp")
  let client = read_source(root <> "/sub/client.glp")

  loader.load_with_scope(client, prelude, [])
  |> should.be_error
}

// Shadowing is ORDER-SENSITIVE: with the outer self.glp merged LAST (reversed
// chain), the root Widget (button/dial) wins and `slider` is rejected — proving the
// root-first order is what makes the nearer definition win.
pub fn reversed_chain_lets_outer_win_and_rejects_test() {
  let prelude = read_source(root <> "/../../self.glp")
  let chain = module_hierarchy.discover_self_chain(root <> "/sub/client.glp", root)
  let reversed = list.reverse(list.map(chain, read_source))
  let client = read_source(root <> "/sub/client.glp")

  loader.load_with_scope(client, prelude, reversed)
  |> should.be_error
}

// ── facade integration (T078 Part B / residual #1b): the REPL's path-aware
//    `engine.load_file` applies the discovered scope chain automatically ──────────

pub fn facade_load_file_applies_scope_chain_test() {
  let eng = engine.new_with_prelude(read_source(root <> "/../../self.glp"))
  let client = read_source(root <> "/sub/client.glp")
  // Path-aware load discovers sub/self.glp (+ scope_chain/self.glp) → `slider` is a
  // valid Widget, so the load succeeds.
  engine.load_file(eng, client, root <> "/sub/client.glp") |> should.be_ok
}

pub fn facade_plain_load_ignores_scope_chain_test() {
  let eng = engine.new_with_prelude(read_source(root <> "/../../self.glp"))
  let client = read_source(root <> "/sub/client.glp")
  // Plain `load` (no path) sees NO scope chain → `Widget` is undefined → cleanly
  // rejected (T073 fix: a staged error, not a panic).
  engine.load(eng, client) |> should.be_error
}
