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
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/result
import gleam/string
import glp/analysis/type_checker/type_checker.{type TypeWarning}
import glp/bytecode/program.{type BytecodeProgram}
import glp/codec/result_envelope.{type ResultEnvelope}
import glp/codec/result_envelope_builder as builder
import gleam/erlang/charlist.{type Charlist}
import glp/compiler/loader
import glp/compiler/project_linker
import glp/diagnostics.{type StagedError}
import glp/engine/goal_boot
import glp/engine/scheduler
import glp/link/primitives/link_loop
import glp/link/primitives/link_runtime
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/transport.{type Transport}
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

/// serve/2 system predicate (embedded — verbatim Dart `_serveSource`,
/// glp_engine.dart:71-82). The dynamic-dispatch service loop: reads goals off a
/// module's GLP channel and dispatches each via `'_activate'`. Compiled once at
/// engine construction through the NON-typechecked path (`compile_prelude`) —
/// Dart compiles it with `CompileOptions()` where `typeCheck = false`
/// (compiler.dart:27), and `_activate/2` is deliberately absent from the
/// type-check prelude in both runtimes — then merged into the runnable program
/// so `activate_module` can spawn `serve/2` from it.
const serve_source = "-mode(system).

procedure serve(_?, Stream(_)?).
serve(Module, [Goal | In]) :-
    ground(Module?) |
    '_activate'(Module?, Goal?),
    serve(Module?, In?).
serve(_, []) :-
    otherwise |
    true.
"

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
    /// An in-progress interactive run (the `start`/`step` seam), or `None` between
    /// runs. One-shot `run` never touches it (its scheduler state is internal).
    session: Option(RunSession),
    /// Constructor-injected transport leaves (wave-3 T007, gap G6: the 059
    /// engine-composition-root verdict was PARTIAL — kernels compiled-in, no
    /// transport injection seam). The link layer (US4) selects a leaf by scheme
    /// via `transport_for`; an engine with `[]` simply holds no links. Injection
    /// here — never a compiled-in registry — keeps the composition root the ONE
    /// place an instance's capabilities are assembled.
    transports: List(Transport),
    /// Loaded user programs keyed by load name, in first-load order (wave-3
    /// T012, FR-015 — Dart `_loadedPrograms[name] = program`: a LinkedHashMap
    /// overwrite REPLACES in place, preserving the original position). The
    /// runnable `program` is always rebuilt from this registry, so a re-load's
    /// stale definitions are unreachable by construction.
    loaded: List(#(String, BytecodeProgram)),
    /// The compiled embedded `serve/2` program (Dart `_serveBytecode`), merged
    /// into every runnable program so activation can spawn the service loop.
    serve_program: BytecodeProgram,
    /// Modules auto-activated for dynamic dispatch, in load order: module name →
    /// its dispatchable bytecode (the module merged with the prelude, Dart
    /// glp_engine.dart:310 `program.merge(rootSelf)`). Applied to each run's
    /// scheduler at goal boot — Dart activates once on its persistent runtime;
    /// the Gleam scheduler is per-run, so activation replays per run.
    activations: List(#(String, BytecodeProgram)),
  )
}

/// A live, steppable run: the in-progress scheduler + the query variables to
/// deep-resolve once it reaches quiescence (facade `step`, contract Engine
/// surface). Held on the `Engine` as the "live run-state" the interactive step
/// needs (deferred from T029 to US2).
type RunSession {
  RunSession(sched: scheduler.Engine, query_var_writers: List(#(String, Int)))
}

/// One `step` of an interactive run — the stepping counterpart to one-shot `run`
/// (contract `step(Engine) -> #(Engine, Event)`). `Reduced`/`Suspended` are
/// intermediate per-goal outcomes (the run continues); `Done` carries the finished
/// run's envelope + captured output (the queue drained, or a goal failed — read
/// `envelope.status`); `Idle` means no active session (call `start` first);
/// `Errored` is a surfaced runner fault.
pub type Event {
  Reduced(procedure: String, woken: List(Int), spawned: List(Int))
  Suspended(procedure: String, on: List(Int))
  Done(envelope: ResultEnvelope, output: List(String))
  Idle
  Errored(detail: String)
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
  case loader.compile_prelude(prelude_source), loader.compile_prelude(serve_source) {
    Ok(prelude_program), Ok(serve_program) ->
      Engine(
        prelude_program: prelude_program,
        prelude_source: prelude_source,
        program: program.merge(serve_program, prelude_program),
        warnings: [],
        session: None,
        transports: [],
        loaded: [],
        serve_program: serve_program,
        activations: [],
      )
    Error(staged), _ ->
      panic as {
        "engine.new: prelude (programs/self.glp) failed to compile: "
        <> string.inspect(staged)
      }
    _, Error(staged) ->
      panic as {
        "engine.new: embedded serve/2 failed to compile: "
        <> string.inspect(staged)
      }
  }
}

/// Run the full load pipeline over `source` and, on success, store it in the
/// loaded-programs registry under `name` (Dart `GlpEngine.loadSource(name, source)`:
/// `_loadedPrograms[name] = program`) and rebuild the runnable program. Distinct
/// names ACCUMULATE in first-load order — a multi-file Dart REPL session (Section-A
/// blocks that co-load several files) reproduces on the Gleam instance. Re-loading
/// an EXISTING name REPLACES that entry in place (FR-015, wave-3 T012): the
/// combined program is rebuilt from the registry, so the replaced file's stale
/// definitions are unreachable — exactly Dart's LinkedHashMap overwrite.
///
/// Op/label order is `[prelude, file1, …, fileN]` and `from_ops` is first-occurrence-
/// wins, matching Dart `combinedProgram`'s insertion order (a predicate defined in an
/// earlier-loaded file wins over a later redefinition — e.g. a1's `merge/3` from
/// `merge_simple` loaded before `run1`). Each file is still type-checked independently
/// against the prelude (Dart checks each `loadSource` against its self.glp ancestor
/// scope, not against other loaded files), so self-contained corpus files load cleanly.
/// A staged rejection propagates unchanged; the engine is left untouched on failure.
pub fn load(
  engine: Engine,
  name: String,
  source: String,
) -> Result(Engine, StagedError) {
  use outcome <- result.try(loader.load(source, engine.prelude_source))
  let loaded = upsert(engine.loaded, name, outcome.program)
  // Auto-activate modules with exports for dynamic dispatch (Dart
  // glp_engine.dart:306-317). The dispatchable bytecode is the module merged
  // with the prelude, so dispatched procedures find `:=`/2 etc. The module
  // name comes from `-module(...)`, falling back to the load name (Dart
  // `_moduleNameFromFilename`). A re-load replaces its activation in place.
  let activations = case outcome.exported_signatures {
    [] -> engine.activations
    _ ->
      upsert(
        engine.activations,
        option.unwrap(outcome.module_name, name),
        program.merge(outcome.program, engine.prelude_program),
      )
  }
  Ok(
    Engine(
      ..engine,
      loaded: loaded,
      program: rebuild_program(
        engine.prelude_program,
        engine.serve_program,
        loaded,
      ),
      warnings: outcome.warnings,
      activations: activations,
    ),
  )
}

/// Replace `name`'s entry in place (preserving its position — the Dart
/// LinkedHashMap overwrite), or append a first-time load.
fn upsert(
  loaded: List(#(String, BytecodeProgram)),
  name: String,
  prog: BytecodeProgram,
) -> List(#(String, BytecodeProgram)) {
  case list.any(loaded, fn(p) { p.0 == name }) {
    True ->
      list.map(loaded, fn(p) {
        case p.0 == name {
          True -> #(name, prog)
          False -> p
        }
      })
    False -> list.append(loaded, [#(name, prog)])
  }
}

/// The runnable program, rebuilt from the registry: `[prelude, serve, file1, …,
/// fileN]` in first-load order (Dart `combinedProgram` folds
/// `_loadedPrograms.values`; serve rides along so activation can spawn it).
fn rebuild_program(
  prelude: BytecodeProgram,
  serve: BytecodeProgram,
  loaded: List(#(String, BytecodeProgram)),
) -> BytecodeProgram {
  list.fold(loaded, program.merge(serve, prelude), fn(acc, pair) {
    program.merge(pair.1, acc)
  })
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

/// The loaded user programs in first-load order (name, program) — the REPL
/// `:bytecode` surface iterates this (Dart `engine.loadedPrograms.entries`).
pub fn loaded_programs(engine: Engine) -> List(#(String, BytecodeProgram)) {
  engine.loaded
}

// ── project loading: static linking (wave-3 T009, FR-008; Dart loadProject) ──

/// Load an entire project directory via static linking (Dart
/// `GlpEngine.loadProject`, glp_engine.dart:331): recursively discover `.glp`
/// modules (skipping `boot_direct.glp` / `mad_boot.glp` / `mad_boot/`),
/// type-check each independently against its ancestor `self.glp` scope, link
/// into ONE flat program (top module auto-detected), compile, and store it
/// under `"__project__"` in the loaded registry (participates in
/// `rebuild_program`; re-load replaces). Any stage failure returns
/// `Error(reason)` with the engine untouched.
pub fn load_project(engine: Engine, dir: String) -> Result(Engine, String) {
  let dir = string.replace(dir, "\\", "/")
  let rel_paths =
    erl_wildcard(charlist.from_string(dir <> "/**/*.glp"))
    |> list.map(charlist.to_string)
    |> list.map(fn(p) { string.replace(p, "\\", "/") })
    |> list.sort(string.compare)
    |> list.filter_map(fn(p) {
      case string.starts_with(p, dir <> "/") {
        True -> Ok(string.drop_start(p, string.length(dir) + 1))
        False -> Error(Nil)
      }
    })
    |> list.filter(fn(p) { !skip_project_file(p) })
  use <- bool_guard(
    rel_paths == [],
    "no modules found in " <> dir,
  )
  use files <- result.try(
    list.try_map(rel_paths, fn(rel) {
      case read_file(dir <> "/" <> rel) {
        Ok(bits) ->
          case bit_array.to_string(bits) {
            Ok(source) -> Ok(#(rel, source))
            Error(_) -> Error("project read: " <> rel <> " is not valid UTF-8")
          }
        Error(_) -> Error("project read: cannot read " <> rel)
      }
    }),
  )
  let root_name = last_component(dir)
  use modules <- result.try(project_linker.discover(
    files,
    root_name,
    engine.prelude_source,
  ))
  use _ <- result.try(project_linker.type_check_project(
    modules,
    loader.prelude_units(engine.prelude_source),
  ))
  let top = project_linker.detect_top_module(modules)
  let linked = project_linker.link_project(modules, top)
  use prog <- result.try(
    loader.compile_linked(linked, engine.prelude_source)
    |> result.map_error(fn(staged) {
      "project compile: " <> string.inspect(staged)
    }),
  )
  let loaded = upsert(engine.loaded, "__project__", prog)
  Ok(
    Engine(
      ..engine,
      loaded: loaded,
      program: rebuild_program(
        engine.prelude_program,
        engine.serve_program,
        loaded,
      ),
    ),
  )
}

/// Dart discoverProject's exclusions: `boot_direct.glp` (direct-call copy of
/// boot.glp), `mad_boot.glp`, and anything under a `mad_boot/` directory
/// (madGLP boot procedures, loaded on top of the linked project).
fn skip_project_file(rel: String) -> Bool {
  let parts = string.split(rel, "/")
  let base = case list.last(parts) {
    Ok(b) -> b
    Error(_) -> rel
  }
  base == "boot_direct.glp"
  || base == "mad_boot.glp"
  || list.contains(parts, "mad_boot")
}

fn last_component(path: String) -> String {
  case list.last(string.split(path, "/")) {
    Ok(last) -> last
    Error(_) -> path
  }
}

fn bool_guard(
  condition: Bool,
  reason: String,
  continue: fn() -> Result(a, String),
) -> Result(a, String) {
  case condition {
    True -> Error(reason)
    False -> continue()
  }
}

@external(erlang, "filelib", "wildcard")
fn erl_wildcard(pattern: Charlist) -> List(Charlist)

// ── transport injection seam (wave-3 T007, gap G6) ───────────────────────────

/// Inject the transport leaves this instance may hold links over (replacing any
/// previously injected set). The composition-root seam: transports arrive as
/// constructed values — the engine never instantiates one itself, so a test can
/// inject loopback only, an embedded host can inject none, and US4's link layer
/// injects the acceptance set (loopback + tcp) without touching this module.
pub fn with_transports(engine: Engine, transports: List(Transport)) -> Engine {
  Engine(..engine, transports: transports)
}

/// The injected transport leaves (`[]` for a fresh engine).
pub fn transports(engine: Engine) -> List(Transport) {
  engine.transports
}

/// Select the injected leaf serving `scheme` (first match wins, mirroring the
/// registry's selection-by-membership — seam/transport.serves). `Error(Nil)`
/// means no injected leaf serves the scheme: the caller reports an unsupported
/// scheme; nothing is ever auto-instantiated (FR-025's seam stays open — the
/// unproven schemes remain selectable exactly here, with no link-layer change).
pub fn transport_for(engine: Engine, scheme: LinkScheme) -> Result(Transport, Nil) {
  list.find(engine.transports, fn(t) { transport.serves(t, scheme) })
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

/// A run's scheduler over the engine's program + boot heap, with every
/// auto-activated module registered and its `serve/2` loop spawned (Dart
/// activates once on its persistent runtime, glp_activation.dart; the Gleam
/// scheduler is per-run, so activation replays at each goal boot). A missing
/// `serve/2` label with activations pending is a broken engine invariant —
/// panic loudly, never a user diagnostic.
fn new_sched(engine: Engine, boot_heap: heap.Heap) -> scheduler.Engine {
  let sched = scheduler.new(engine.program, boot_heap)
  case engine.activations {
    [] -> sched
    _ -> {
      let serve_entry = case program.label_pc(engine.program, "serve/2") {
        Ok(pc) -> pc
        Error(_) ->
          panic as "engine: serve/2 missing from the runnable program"
      }
      list.fold(engine.activations, sched, fn(sched, act) {
        let #(mod_name, mod_prog) = act
        let #(sched, idx) = scheduler.register_module(sched, mod_prog)
        scheduler.activate_module(sched, mod_name, idx, "serve/2", serve_entry)
      })
    }
  }
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
    new_sched(engine, boot.heap)
    |> scheduler.with_trace(trace)
  let #(sched, _goal_id) = scheduler.boot(sched, label, entry, boot.regs)
  // With transport leaves injected, run under the link-aware loop (T050.C2):
  // the `_link_*` kernels become reachable and quiescence stays live while
  // link I/O is outstanding. Without leaves, the pure run is unchanged.
  let #(sched, status) = case engine.transports {
    [] -> scheduler.run(sched, default_reduction_budget, fuel)
    leaves ->
      link_loop.drive(
        sched,
        default_reduction_budget,
        fuel,
        link_runtime.new_state(leaves),
      )
  }

  use #(envelope, output) <- result.try(finish_run(
    sched,
    boot.query_var_writers,
    status,
  ))
  Ok(#(envelope, output, scheduler.trace_lines(sched)))
}

/// Build the result envelope + captured output from a finished scheduler run
/// (shared by one-shot `run` and the interactive `step` at quiescence). `status`
/// is the run's terminal status (map_status derives the envelope status + blocking
/// readers + error).
fn finish_run(
  sched: scheduler.Engine,
  query_var_writers: List(#(String, Int)),
  status: scheduler.RunStatus,
) -> Result(#(ResultEnvelope, List(String)), String) {
  let #(exec_status, blocking_readers, error) = map_status(status)
  let output = scheduler.captured_output(sched)
  case
    builder.build_result_envelope(
      scheduler.heap(sched),
      query_var_writers,
      exec_status,
      blocking_readers,
      instance_id,
      <<>>,
      error,
    )
  {
    Ok(#(_heap, envelope)) -> Ok(#(envelope, output))
    Error(build_error) ->
      Error("result-envelope build failed: " <> string.inspect(build_error))
  }
}

// ── interactive stepping: start / step / Event (contract Engine surface) ──────

/// Boot `goal` into an interactive run session on the engine (the `step`
/// counterpart to one-shot `run`). A parse / missing-predicate / goal-boot failure
/// returns `Error(reason)` with the engine's session unchanged. Any prior session
/// is replaced.
pub fn start(engine: Engine, goal: String) -> Result(Engine, String) {
  use atom <- result.try(parse_goal(goal))
  let label = atom.functor <> "/" <> int.to_string(ast.atom_arity(atom))
  use entry <- result.try(
    program.label_pc(engine.program, label)
    |> result.replace_error("predicate " <> label <> " not found"),
  )
  use boot <- result.try(goal_boot.setup_goal(heap.new(), atom))
  let sched = new_sched(engine, boot.heap)
  let #(sched, _goal_id) = scheduler.boot(sched, label, entry, boot.regs)
  Ok(Engine(..engine, session: Some(RunSession(sched, boot.query_var_writers))))
}

/// Advance an interactive run one reduction (contract `step`). Without a session,
/// returns `Idle`. `Reduced`/`Suspended` continue the session; a drained queue or a
/// failed goal ends it, clearing the session and returning `Done(envelope, output)`
/// (inspect `envelope.status` for success vs suspended vs failed).
pub fn step(engine: Engine) -> #(Engine, Event) {
  case engine.session {
    None -> #(engine, Idle)
    Some(session) -> {
      let #(sched, outcome) =
        scheduler.step(session.sched, default_reduction_budget)
      case outcome {
        scheduler.StepReduced(_, procedure, woken, spawned) -> #(
          Engine(..engine, session: Some(RunSession(..session, sched: sched))),
          Reduced(procedure, woken, spawned),
        )
        scheduler.StepSuspended(_, procedure, on) -> #(
          Engine(..engine, session: Some(RunSession(..session, sched: sched))),
          Suspended(procedure, on),
        )
        scheduler.StepFailed(_, _procedure) ->
          finish_step(engine, sched, session, scheduler.Failed)
        scheduler.StepIdle ->
          finish_step(engine, sched, session, scheduler.status(sched))
        scheduler.StepErrored(fault) -> #(
          Engine(..engine, session: None),
          Errored(string.inspect(fault)),
        )
      }
    }
  }
}

fn finish_step(
  engine: Engine,
  sched: scheduler.Engine,
  session: RunSession,
  status: scheduler.RunStatus,
) -> #(Engine, Event) {
  let cleared = Engine(..engine, session: None)
  case finish_run(sched, session.query_var_writers, status) {
    Ok(#(envelope, output)) -> #(cleared, Done(envelope, output))
    Error(reason) -> #(cleared, Errored(reason))
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
