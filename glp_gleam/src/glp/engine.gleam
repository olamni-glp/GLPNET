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
import gleam/int
import gleam/option.{type Option, None, Some}
import gleam/result
import gleam/string
import glp/analysis/type_checker/type_checker.{type TypeWarning}
import glp/bytecode/program.{type BytecodeProgram}
import glp/codec/result_envelope.{type ResultEnvelope}
import glp/codec/result_envelope_builder as builder
import glp/compiler/loader
import glp/diagnostics.{type StagedError}
import glp/engine/goal_boot
import glp/engine/scheduler
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser
import glp/runtime/heap

/// The root prelude path, relative to the `glp_gleam/` package root (the CWD both
/// `gleam test` and `gleam run` use — the same convention as `golden_corpus_test`
/// reading `../specs/...`).
const prelude_path = "../programs/self.glp"

/// Per-instance id stamped on every `GlobalVarId` this engine mints (the C#
/// `GlobalVarId.agentId` — a per-glpnet-instance unique id). PROVISIONAL: the Dart
/// `buildResultEnvelope` is uncalled (no live oracle) and it appears only in
/// var→writer / suspended entries (never in a bound-only envelope such as `X := 2+3`);
/// pin a parity value here if a suspended-var corpus case is later recorded.
const instance_id = "gleam"

/// Instruction budget per goal reduction (REPL `:limit` overrides this later).
const default_reduction_budget = 1_000_000

/// Total-reduction backstop for one `run` (loop non-termination guard).
const default_fuel = 1_000_000

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

// ── run: goal → ResultEnvelope (T029 Slice 2) ────────────────────────────────

/// Execute `goal` against the engine's runnable program and return the result
/// envelope (the ED-1 seam — every goal result is a `ResultEnvelope`). One-shot: a
/// fresh heap is populated by goal-boot, the goal reduced to quiescence, and the
/// query variables deep-resolved (Dart `_runSingleGoal`). The engine is returned
/// unchanged (the run's scheduler state is internal). `captured` is `<<>>` —
/// output capture is deferred (R4 excludes it from parity; see the restart note).
///
/// A parse failure, a missing predicate, an unsupported argument shape, or a heap
/// build error yields a `Failed` envelope carrying the reason (Dart catches these
/// into `ExecutionResult(status: failed, error: …)`).
pub fn run(engine: Engine, goal: String) -> #(Engine, ResultEnvelope) {
  run_with_limit(engine, goal, default_fuel)
}

/// As `run`, but with an explicit total-reduction budget (`fuel`) — the seam the
/// REPL `:limit <n>` sets (Dart `engine.maxCycles`). Exhaustion surfaces as an
/// `OutOfFuel` run → a `Failed` envelope carrying the exhaustion reason.
pub fn run_with_limit(
  engine: Engine,
  goal: String,
  fuel: Int,
) -> #(Engine, ResultEnvelope) {
  let #(engine, envelope, _output) =
    run_with_limit_capturing(engine, goal, fuel)
  #(engine, envelope)
}

/// As `run_with_limit`, but ALSO returns the captured `_output/1` program-output
/// lines in emission order (T034 — the REPL prints them ahead of the outcome
/// block; they are NOT placed in the R4-excluded envelope `captured` field). The
/// engine is unchanged (the run's scheduler state, including its output buffer, is
/// internal to the one-shot run).
pub fn run_with_limit_capturing(
  engine: Engine,
  goal: String,
  fuel: Int,
) -> #(Engine, ResultEnvelope, List(String)) {
  let #(engine, envelope, output, _traces) =
    run_with_limit_traced(engine, goal, fuel, False)
  #(engine, envelope, output)
}

/// As `run_with_limit_capturing`, but ALSO returns the reduction-trace lines
/// (`head :- body` / `→ suspended` / `→ failed`) when `trace` is on — the REPL
/// `:trace` seam. Trace lines are empty when `trace` is off.
pub fn run_with_limit_traced(
  engine: Engine,
  goal: String,
  fuel: Int,
  trace: Bool,
) -> #(Engine, ResultEnvelope, List(String), List(String)) {
  let #(envelope, output, traces) = case run_goal(engine, goal, fuel, trace) {
    Ok(#(env, out, tr)) -> #(env, out, tr)
    Error(reason) -> #(failed_envelope(reason), [], [])
  }
  #(engine, envelope, output, traces)
}

fn run_goal(
  engine: Engine,
  goal: String,
  fuel: Int,
  trace: Bool,
) -> Result(#(ResultEnvelope, List(String), List(String)), String) {
  use atom <- result.try(parse_goal(goal))
  let label = atom.functor <> "/" <> int.to_string(ast.atom_arity(atom))
  use entry <- result.try(
    program.label_pc(engine.program, label)
    |> result.replace_error("predicate " <> label <> " not found"),
  )
  use boot <- result.try(goal_boot.setup_goal(heap.new(), atom))

  let sched =
    scheduler.new(engine.program, boot.heap)
    |> scheduler.with_trace(trace)
  let #(sched, _goal_id) = scheduler.boot(sched, label, entry, boot.regs)
  let #(sched, status) = scheduler.run(sched, default_reduction_budget, fuel)

  let #(exec_status, blocking_readers, error) = map_status(status)
  let output = scheduler.captured_output(sched)
  let traces = scheduler.trace_lines(sched)
  case
    builder.build_result_envelope(
      scheduler.heap(sched),
      boot.query_var_writers,
      exec_status,
      blocking_readers,
      instance_id,
      <<>>,
      error,
    )
  {
    Ok(#(_heap, envelope)) -> Ok(#(envelope, output, traces))
    Error(build_error) ->
      Error("result-envelope build failed: " <> string.inspect(build_error))
  }
}

/// Parse a goal string into its head atom. The goal is a unit-clause head (Dart
/// `_runSingleGoal`: strip any trailing `.`, re-append exactly one, then take
/// `procedures[0].clauses[0].head`). Only `parse_module` exists — a goal is a
/// one-clause module.
fn parse_goal(goal: String) -> Result(ast.Atom, String) {
  let trimmed = string.trim(goal)
  let base = case string.ends_with(trimmed, ".") {
    True -> string.drop_end(trimmed, 1)
    False -> trimmed
  }
  use tokens <- result.try(
    lexer.tokenize(base <> ".")
    |> result.map_error(fn(e) { "parse: " <> string.inspect(e) }),
  )
  use module <- result.try(
    parser.parse_module(tokens)
    |> result.map_error(fn(e) { "parse: " <> string.inspect(e) }),
  )
  case module.procedures {
    [ast.Procedure(clauses: [clause, ..], ..), ..] -> Ok(clause.head)
    [ast.Procedure(clauses: [], ..), ..] -> Error("no clauses in goal")
    [] -> Error("no goal found")
  }
}

/// Map the scheduler run status to the envelope status + blocking readers + error
/// (Dart `_mapStatus`; the envelope's `error` is present iff status is Failed —
/// C# `ResultEnvelope` invariant). Blocking readers are non-empty only on Suspended.
fn map_status(
  status: scheduler.RunStatus,
) -> #(result_envelope.ExecutionStatus, List(Int), Option(String)) {
  case status {
    scheduler.Success -> #(result_envelope.Success, [], None)
    scheduler.Suspended(readers) -> #(result_envelope.Suspended, readers, None)
    scheduler.Failed -> #(
      result_envelope.Failed,
      [],
      Some("goal failed: no matching clause"),
    )
    scheduler.OutOfFuel -> #(
      result_envelope.Failed,
      [],
      Some("reduction fuel exhausted"),
    )
    scheduler.Errored(fault) -> #(
      result_envelope.Failed,
      [],
      Some("runner error: " <> string.inspect(fault)),
    )
  }
}

/// A `Failed` envelope carrying `reason` (the goal-setup / parse failure path).
fn failed_envelope(reason: String) -> ResultEnvelope {
  result_envelope.ResultEnvelope(
    status: result_envelope.Failed,
    resolved_bindings: [],
    var_to_writer: [],
    suspended: [],
    captured: <<>>,
    error: Some(reason),
  )
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
