//// glp_embed — the GLP engine as an embeddable BEAM component (feature 064,
//// T030; US4, G3-A).
////
//// Contract: specs/064-post-wave-gap-closure/contracts/febe-split.md
//// §Embeddability — "`glp_embed` public surface: `load(project_path) ->
//// EmbedHandle`, `run(handle, goal) -> ResultStream`, `observe(handle) ->
//// events`. … No REPL, no stdin." Ruling: research.md D5 (G3-A) — the surface
//// wraps the SAME engine API the REPL uses (glp/repl/commands drives
//// `engine.load` / `engine.load_project` / `engine.run_with_limit_capturing`);
//// nothing here reaches past the `glp/engine` facade.
////
//// Host contract:
////   - `load(project_path)`: a path ending `.glp` is a single-file load
////     (engine.load — full staged pipeline, `StagedError` taxonomy on
////     rejection); any other path is a project-directory load
////     (engine.load_project — static linking, Dart `loadProject` parity). A
////     missing/unreadable path is a typed `EmbedError`, never a crash.
////   - `run(handle, goal)`: one-shot goal execution (single goal or top-level
////     conjunction, exactly the REPL goal surface) returning the terminal
////     `ResultEnvelope` — the ED-1 result seam (status + deep-resolved
////     bindings + error). A parse failure / missing predicate / failed goal
////     is a `Failed` envelope carrying the reason, never a crash.
////   - `observe(handle)`: drain the events the handle accumulated since the
////     last observe — each run appends its `_output/1` program-output lines
////     (emission order) then its terminal `Outcome`. Draining is the pure-value
////     analogue of an event subscription: run → observe → handle is empty.
////
//// The handle is an immutable value (FR-009 — no global/process-dictionary
//// engine state): every operation returns the successor handle, so a host
//// process can thread it through its own loop (e.g. a gen_server state).
////
//// Prelude: `load` reads the root prelude from `../programs/self.glp` — the
//// `glp_gleam/` package-root convention `gleam test` / `gleam run` use. A host
//// installed elsewhere gets the typed `PreludeNotFound` (never a crash — the
//// panic in `engine.new` is not a host-facing diagnostic) and supplies the
//// prelude source itself through `load_with_prelude`, the CWD-independent seam
//// (`engine.new_with_prelude`). No transports are injected: an embedded engine
//// holds no links (the composition-root seam stays with the host's own entry
//// point).

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/list
import gleam/string
import glp/codec/result_envelope.{type ResultEnvelope}
import glp/diagnostics.{type StagedError}
import glp/engine

/// An embedded engine instance: the engine value plus the events accumulated
/// since the last `observe`. Opaque — hosts interact only via `load` / `run` /
/// `observe`.
pub opaque type EmbedHandle {
  EmbedHandle(engine: engine.Engine, pending: List(EmbedEvent))
}

/// Typed load failures (the "missing file → typed error, not a crash" leg of
/// the host contract). A staged pipeline rejection carries the loader's
/// existing `StagedError` taxonomy unchanged (stage + class + pos + reason).
pub type EmbedError {
  /// The `.glp` file at `path` does not exist / cannot be read as UTF-8.
  SourceNotFound(path: String)
  /// The staged load pipeline rejected the source (parse / SRSW / type /
  /// compile — the `StagedError` names the stage and reason).
  LoadRejected(path: String, error: StagedError)
  /// A project-directory load failed (no modules found, unreadable module,
  /// link/type/compile failure — `reason` is engine.load_project's diagnostic).
  ProjectRejected(path: String, reason: String)
  /// The root prelude could not be read from `path` — the host is not running
  /// from the `glp_gleam/` package root of a glpnet checkout. Use
  /// `load_with_prelude` to supply the prelude source directly.
  PreludeNotFound(path: String)
}

/// One observable event of an embedded run, in emission order: each `run`
/// appends its captured `_output/1` lines, then its terminal `Outcome`.
pub type EmbedEvent {
  /// One `_output/1` program-output line.
  ProgramOutput(line: String)
  /// A run's terminal result envelope (status + bindings + error).
  Outcome(envelope: ResultEnvelope)
}

/// Reduction budget per run (the engine default the REPL's `:limit` also
/// starts from).
const default_limit = 1_000_000

/// The root prelude's path under the `glp_gleam/` package-root convention (the
/// same path `engine.new` reads).
const default_prelude_path = "../programs/self.glp"

/// Load a project into a fresh embedded engine. `project_path` ending `.glp`
/// loads that single file through the full staged pipeline; any other path is
/// loaded as a project directory (static linking). The returned handle carries
/// the prelude + the loaded program and an empty event queue. The prelude is
/// read from `../programs/self.glp`; a host whose working directory is not the
/// `glp_gleam/` package root gets `PreludeNotFound` (never a crash) and should
/// call `load_with_prelude` instead.
pub fn load(project_path: String) -> Result(EmbedHandle, EmbedError) {
  case read_source(default_prelude_path) {
    Error(_) -> Error(PreludeNotFound(default_prelude_path))
    Ok(prelude) -> load_with_prelude(project_path, prelude)
  }
}

/// `load` over an explicitly-supplied root-prelude source — the CWD-independent
/// embedding seam (`engine.new_with_prelude`), for a host installed anywhere on
/// the machine (it may carry the prelude in its own release/priv dir).
pub fn load_with_prelude(
  project_path: String,
  prelude_source: String,
) -> Result(EmbedHandle, EmbedError) {
  let eng = engine.new_with_prelude(prelude_source)
  case string.ends_with(project_path, ".glp") {
    True -> load_file(eng, project_path)
    False -> load_directory(eng, project_path)
  }
}

fn load_file(
  eng: engine.Engine,
  path: String,
) -> Result(EmbedHandle, EmbedError) {
  case read_source(path) {
    Error(_) -> Error(SourceNotFound(path))
    Ok(source) ->
      case engine.load(eng, path, source) {
        Ok(eng) -> Ok(EmbedHandle(engine: eng, pending: []))
        Error(staged) -> Error(LoadRejected(path, staged))
      }
  }
}

fn load_directory(
  eng: engine.Engine,
  dir: String,
) -> Result(EmbedHandle, EmbedError) {
  case engine.load_project(eng, dir) {
    Ok(eng) -> Ok(EmbedHandle(engine: eng, pending: []))
    Error(reason) -> Error(ProjectRejected(dir, reason))
  }
}

/// Execute `goal` (single goal or top-level conjunction — the REPL goal
/// surface) against the handle's program and return the terminal result
/// envelope. The run's `_output/1` lines and its `Outcome` are appended to the
/// handle's event queue for `observe`. Every failure mode (parse error,
/// missing predicate, failed goal, fuel exhaustion) is a `Failed` envelope
/// carrying the reason — `run` never crashes the host.
pub fn run(
  handle: EmbedHandle,
  goal: String,
) -> #(EmbedHandle, ResultEnvelope) {
  let #(eng, envelope, output) =
    engine.run_with_limit_capturing(handle.engine, goal, default_limit)
  let events =
    list.append(list.map(output, ProgramOutput), [Outcome(envelope)])
  #(
    EmbedHandle(engine: eng, pending: list.append(handle.pending, events)),
    envelope,
  )
}

/// Drain the events accumulated since the last `observe` (run order; within a
/// run: `ProgramOutput` lines in emission order, then the run's `Outcome`).
/// Returns the drained events and the successor handle with an empty queue.
pub fn observe(handle: EmbedHandle) -> #(EmbedHandle, List(EmbedEvent)) {
  #(EmbedHandle(..handle, pending: []), handle.pending)
}

// ── file read (the commands.gleam FFI convention) ────────────────────────────

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> Result(String, Nil) {
  case read_file(path) {
    Ok(bits) -> bit_array.to_string(bits)
    Error(_) -> Error(Nil)
  }
}
