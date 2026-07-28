//// glp/engine host-embedding API — the prelude-injection + host-kernel seam
//// (feature 059, T068 `close-embeddability-host-api` · T069
//// `close-engine-engine-composition-root`).
////
//// T069 composition-root seam: a host registers its OWN body kernel onto the engine
//// value via `register_kernel`; the runner dispatches to it at a BODY Spawn label-miss
//// by table lookup, WITHOUT the engine ever naming it (verify-engine-engine-composition-
//// root #1 — "kernels injected onto a live engine, never referenced by it"). The
//// negative control proves the "never referenced" half: a bare engine does NOT know it.
////
//// T068 host API + prelude-injection seam: the calling wrapper is injected through the
//// engine's PRELUDE (`new_with_prelude`) — the sanctioned host seam. The prelude is
//// compiled WITHOUT a user-style type check (loader.compile_prelude), exactly as the
//// root self.glp calls host kernels the type checker does not recognise (`'_now'`,
//// `'_add'`); the wrapper `double(X, Y?) :- '_host_double'(X?, Y).` mirrors self.glp's
//// `now(T?) :- '_now'(T).`. The whole flow is driven through the `glp/engine` surface
//// alone — new_with_prelude / configure / register_kernel / run — importing NO
//// `glp/repl/*` module (the decoupled-from-UI requirement).

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleeunit/should
import glp/codec/result_envelope
import glp/codec/term_codec
import glp/engine
import glp/engine/runner.{HostKernelOutcome}
import glp/runtime/heap.{Bound}
import glp/runtime/terms.{ConstInt, ConstTerm, VarRef}

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_self_glp() -> String {
  let assert Ok(bits) = read_file("../programs/self.glp")
  let assert Ok(text) = bit_array.to_string(bits)
  text
}

// The engine's prelude = the real root self.glp PLUS a host-supplied wrapper that calls
// the injected kernel `'_host_double'`, mirroring self.glp's own `now(T?) :- '_now'(T).`
// The prelude is not type-checked, so the unrecognised kernel call passes.
fn host_prelude() -> String {
  read_self_glp()
  <> "

procedure double(Integer?, Integer).
double(X, Y?) :- '_host_double'(X?, Y)."
}

// A host-supplied body kernel `_host_double(In?, Out)`: bind `Out` to `2 * In`. Defined
// HERE (in the host / test), never in the engine — the engine reaches it only through
// the injected table. Mirrors the pure `:=` kernel's bind-writer + carry-woken shape.
fn double_kernel(
  h: heap.Heap,
  args: List(terms.Term),
) -> Result(runner.HostKernelOutcome, String) {
  case args {
    [input, VarRef(out_addr)] ->
      case resolve_int(h, input) {
        Ok(n) ->
          case heap.bind_writer(h, out_addr, ConstTerm(ConstInt(n * 2))) {
            Ok(#(h2, woken)) -> Ok(HostKernelOutcome(h2, woken, []))
            Error(_) -> Error("_host_double: output writer already bound")
          }
        Error(why) -> Error(why)
      }
    _ -> Error("_host_double/2: expected (Integer?, Out)")
  }
}

fn resolve_int(h: heap.Heap, t: terms.Term) -> Result(Int, String) {
  case t {
    ConstTerm(ConstInt(n)) -> Ok(n)
    VarRef(addr) ->
      case heap.deref(h, addr) {
        Ok(#(_, Bound(ConstTerm(ConstInt(n))))) -> Ok(n)
        _ -> Error("_host_double: input is not a bound integer")
      }
    _ -> Error("_host_double: input is not an integer")
  }
}

// ── T069: an injected host kernel runs through the engine ──────────────────────
pub fn injected_host_kernel_runs_over_engine_test() {
  let eng =
    engine.new_with_prelude(host_prelude())
    |> engine.register_kernel("_host_double", 2, double_kernel)

  let #(_eng, env) = engine.run(eng, "double(21, R)")

  // The host kernel fired at the body Spawn and bound R = 2 * 21.
  env.status |> should.equal(result_envelope.Success)
  env.resolved_bindings
  |> should.equal([#("R", term_codec.ConstTerm(term_codec.ConstInt(42)))])
}

// ── T069 negative control: the engine does NOT reference the kernel ────────────
//
// The SAME prelude on a BARE engine (no `register_kernel`) does not succeed — the
// `_host_double` label misses every built-in seam and falls through to a non-fatal
// failure. This is the "never referenced by the engine" half: the engine only knows the
// kernel when the host injects it.
pub fn bare_engine_does_not_know_the_injected_kernel_test() {
  let eng = engine.new_with_prelude(host_prelude())
  let #(_eng, env) = engine.run(eng, "double(21, R)")
  env.status |> should.equal(result_envelope.Failed)
}

// ── T068: embed + drive the engine exclusively through the engine API ──────────
//
// A host builds an engine over its own prelude, configures it (a custom fuel + the
// injected kernel), and drives a goal — all through `glp/engine` alone (no repl). The
// configured state round-trips and the run produces the expected binding.
pub fn host_harness_embeds_and_drives_engine_test() {
  let base = engine.default_config()
  let cfg = engine.EngineConfig(..base, fuel: 500_000)
  let eng =
    engine.new_with_prelude(host_prelude())
    |> engine.configure(cfg)
    |> engine.register_kernel("_host_double", 2, double_kernel)

  // The configuration is observable on the pure value (T068 configure surface).
  engine.config(eng).fuel |> should.equal(500_000)

  let #(_eng, env) = engine.run(eng, "double(7, R)")
  env.status |> should.equal(result_envelope.Success)
  env.resolved_bindings
  |> should.equal([#("R", term_codec.ConstTerm(term_codec.ConstInt(14)))])
}

// A plain arithmetic goal drives cleanly through the API with no injected kernel — the
// ordinary embedding path (T068), untouched by the seam.
pub fn plain_goal_drives_through_the_api_test() {
  let #(_eng, env) = engine.run(engine.new(), "X := 2 + 3")
  env.status |> should.equal(result_envelope.Success)
  env.resolved_bindings
  |> should.equal([#("X", term_codec.ConstTerm(term_codec.ConstInt(5)))])
}

// A host kernel that ABORTS surfaces loudly (a Failed run) — never a silent success
// (the built-in KAbort discipline, extended to injected kernels).
pub fn aborting_host_kernel_surfaces_loudly_test() {
  let abort_kernel = fn(_h, _args) { Error("host said no") }
  let eng =
    engine.new_with_prelude(host_prelude())
    |> engine.register_kernel("_host_double", 2, abort_kernel)
  let #(_eng, env) = engine.run(eng, "double(21, R)")
  env.status |> should.equal(result_envelope.Failed)
}
