//// glp/engine — the engine-as-typed-value facade (feature 050, T029 Slice 1).
////
//// Contract: specs/050-full-gleam-combined/contracts/gleam-instance-surface.md
//// §"Engine as typed value" — `Engine` is an opaque value threaded through
//// `new()` → `load()` → `run()` → `step()`, with NO global/process-dictionary
//// engine state (FR-009). This slice delivers construction + load; `run`/`step`
//// (goal-boot → ResultEnvelope) land in Slice 2.
////
//// Dart source of truth: glp_runtime/lib/engine/glp_engine.dart —
////   - `new()` mirrors `GlpEngine._loadRootSelf` (glp_engine.dart:230): read the
////     root `programs/self.glp`, compile it WITHOUT type-checking
////     (`loader.compile_prelude`), and hold the prelude program + its source. A
////     missing / broken self.glp is a trusted-invariant failure — Dart throws a
////     StateError; here `new()` panics LOUDLY (never a user diagnostic).
////   - `load(engine, source)` mirrors `loadSource` + `program.merge(rootSelf)`
////     (glp_engine.dart:310): run the full load pipeline over the user source,
////     then prepend the prelude (`program.merge`, user labels winning) to get the
////     runnable program. A staged rejection propagates unchanged.
////   - The runnable `program` always includes the prelude, so a prelude-only goal
////     (`X := 2+3`, whose `:=/2` lives in self.glp) runs with no user `load` — Dart
////     `combinedProgram` always folds in `__root_self__`.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/result
import gleam/string
import glp/analysis/type_checker/type_checker.{type TypeWarning}
import glp/bytecode/program.{type BytecodeProgram}
import glp/compiler/loader
import glp/diagnostics.{type StagedError}

/// The root prelude path, relative to the `glp_gleam/` package root (the CWD both
/// `gleam test` and `gleam run` use — the same convention as `golden_corpus_test`
/// reading `../specs/...`).
const prelude_path = "../programs/self.glp"

/// The engine as an opaque typed value (FR-009). Holds the compiled prelude
/// (kept for re-merge on each `load`), the prelude source (the load pipeline
/// threads it into PE + the type environment), the current runnable program
/// (prelude alone until a `load` merges user code in front), and the warnings from
/// the last successful load (the REPL surface renders them — they never reject).
pub opaque type Engine {
  Engine(
    prelude_program: BytecodeProgram,
    prelude_source: String,
    program: BytecodeProgram,
    warnings: List(TypeWarning),
  )
}

/// A fresh engine reading the root `programs/self.glp` from disk. A missing /
/// unreadable / non-UTF-8 / uncompilable prelude panics LOUDLY — it is a trusted
/// engine invariant, not a user-facing diagnostic (Dart `_loadRootSelf` throws a
/// StateError on the same conditions).
pub fn new() -> Engine {
  new_with_prelude(read_prelude_from_disk())
}

/// A fresh engine over an explicitly-supplied prelude source (the CWD-independent
/// test/embedding seam; `new()` is this over the on-disk self.glp). A prelude that
/// fails to compile panics LOUDLY — same trusted-invariant contract as `new()`.
pub fn new_with_prelude(prelude_source: String) -> Engine {
  case loader.compile_prelude(prelude_source) {
    Ok(prelude_program) ->
      Engine(
        prelude_program: prelude_program,
        prelude_source: prelude_source,
        program: prelude_program,
        warnings: [],
      )
    Error(staged) ->
      panic as {
        "engine.new: prelude (programs/self.glp) failed to compile: "
        <> string.inspect(staged)
      }
  }
}

/// Run the full load pipeline over `source` and, on success, prepend the prelude
/// to get the runnable program (Dart `program.merge(rootSelf)` — user labels win).
/// A staged rejection (parse / SRSW / type / guard) propagates unchanged; the
/// engine is left untouched on failure.
pub fn load(engine: Engine, source: String) -> Result(Engine, StagedError) {
  use outcome <- result.try(loader.load(source, engine.prelude_source))
  Ok(
    Engine(
      ..engine,
      program: program.merge(outcome.program, engine.prelude_program),
      warnings: outcome.warnings,
    ),
  )
}

/// The current runnable program (prelude alone, or prelude + last loaded module).
/// The execution seam for `run`/`step` (Slice 2) and the parity tests.
pub fn program(engine: Engine) -> BytecodeProgram {
  engine.program
}

/// The prelude source the load pipeline threads into PE + the type environment.
pub fn prelude_source(engine: Engine) -> String {
  engine.prelude_source
}

/// Type-check warnings from the last successful `load` ([] for a fresh engine).
/// The reference prints `[TYPE WARNING] …` but never rejects on them; the REPL
/// surface renders these.
pub fn warnings(engine: Engine) -> List(TypeWarning) {
  engine.warnings
}

// ── prelude read (038 FFI pattern) ───────────────────────────────────────────

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

/// Read + UTF-8-decode the on-disk prelude, panicking loudly on any failure
/// (trusted invariant — Dart `_loadRootSelf` throws a StateError, not a
/// user diagnostic).
fn read_prelude_from_disk() -> String {
  case read_file(prelude_path) {
    Ok(bits) ->
      case bit_array.to_string(bits) {
        Ok(source) -> source
        Error(_) ->
          panic as {
            "engine.new: prelude (" <> prelude_path <> ") is not valid UTF-8"
          }
      }
    Error(reason) ->
      panic as {
        "engine.new: cannot read prelude ("
        <> prelude_path
        <> "): "
        <> string.inspect(reason)
      }
  }
}
