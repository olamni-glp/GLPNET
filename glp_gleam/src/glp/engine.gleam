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
import gleam/dict.{type Dict}
import gleam/dynamic.{type Dynamic}
import gleam/int
import gleam/option.{type Option, None, Some}
import gleam/result
import gleam/string
import glp/analysis/type_checker/type_checker.{type TypeWarning}
import glp/bytecode/program.{type BytecodeProgram}
import glp/codec/result_envelope.{type ResultEnvelope}
import glp/codec/result_envelope_builder as builder
import gleam/list
import glp/compiler/loader
import glp/compiler/module_hierarchy
import glp/diagnostics.{type StagedError}
import glp/engine/goal_boot
import glp/engine/module_runtime.{type ModuleRuntime}
import glp/engine/runner
import glp/engine/scheduler
import glp/runtime/terms.{ConstAtom, ConstTerm, VarRef}
import glp/link/primitives/link_pump
import glp/link/primitives/link_runtime.{type LinkRuntime}
import glp/link/primitives/transport_registry
import glp/link/transports/loopback
import glp/link/transports/tcp
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser
import glp/runtime/heap

/// The root prelude path, relative to the `glp_gleam/` package root (the CWD both
/// `gleam test` and `gleam run` use — the same convention as `golden_corpus_test`
/// reading `../specs/...`).
const prelude_path = "../programs/self.glp"

/// The module-RPC system module (`serve/2` + `_activate/2`), merged into every
/// engine's program so exported modules can be auto-activated and `#` calls routed
/// (T078 residual #1a). A missing/uncompilable system module degrades gracefully:
/// module RPC is simply unavailable, the rest of the engine is unaffected.
const system_module_path = "../programs/system/module_predicates.glp"

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

/// A host-injected body kernel — re-exported from the runner seam so an embedding host
/// registers kernels through the `glp/engine` API alone (never a repl module). See
/// `runner.HostKernel` / `register_kernel`.
pub type HostKernel =
  runner.HostKernel

/// The host-tunable engine configuration (T068 `configure` surface): the reduction
/// budget + total fuel (Dart `engine.maxCycles`), trace mode, and the host-injected
/// kernel table (T069 composition-root seam). An engine carries one; `configure`
/// replaces it, `register_kernel` adds to it. `run` (the config-driven entry) reads it;
/// the explicit `run_with_limit*` variants still take an ad-hoc fuel for the REPL
/// `:limit`.
pub type EngineConfig {
  EngineConfig(
    reduction_budget: Int,
    fuel: Int,
    trace: Bool,
    host_kernels: Dict(#(String, Int), HostKernel),
  )
}

/// The default configuration a fresh engine starts with (the historical constants: a
/// 1M per-reduction budget, a 1M total-fuel backstop, tracing off, no injected kernels).
pub fn default_config() -> EngineConfig {
  EngineConfig(default_reduction_budget, default_fuel, False, dict.new())
}

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
    /// Names of loaded modules with `exported procedure`s (T078 residual #1a). Each
    /// run activates them — spawns a `serve/2` service loop over a channel + registers
    /// it — so a `M # goal(...)` call routes to the module. Dart §19.8 auto-activation.
    activated_modules: List(String),
    /// The host-tunable configuration (T068 `configure` + T069 kernel injection).
    /// Threaded into every run — the reduction budget / fuel bound the scheduler and the
    /// injected kernel table reaches each reduction context.
    config: EngineConfig,
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
  case loader.compile_prelude(prelude_source) {
    Ok(prelude_program) ->
      Engine(
        prelude_program: prelude_program,
        prelude_source: prelude_source,
        program: merge_system_module(prelude_program, prelude_source),
        warnings: [],
        session: None,
        activated_modules: [],
        config: default_config(),
      )
    Error(staged) ->
      panic as {
        "engine.new: prelude (programs/self.glp) failed to compile: "
        <> string.inspect(staged)
      }
  }
}

/// Run the full load pipeline over `source` and, on success, ACCUMULATE it onto the
/// engine's current runnable program (Dart `GlpEngine.loadSource` stores each file in
/// `_loadedPrograms[name]` and `combinedProgram` concatenates them all). Merging into
/// `engine.program` (prelude + any prior loads) rather than the bare prelude means a
/// second `load` no longer discards the first — so a multi-file Dart REPL session
/// (Section-A blocks that co-load several files) reproduces on the Gleam instance.
///
/// Op/label order is `[prelude, file1, …, fileN]` and `from_ops` is first-occurrence-
/// wins, matching Dart `combinedProgram`'s insertion order (a predicate defined in an
/// earlier-loaded file wins over a later redefinition — e.g. a1's `merge/3` from
/// `merge_simple` loaded before `run1`). Each file is still type-checked independently
/// against the prelude (Dart checks each `loadSource` against its self.glp ancestor
/// scope, not against other loaded files), so self-contained corpus files load cleanly.
/// A staged rejection propagates unchanged; the engine is left untouched on failure.
pub fn load(engine: Engine, source: String) -> Result(Engine, StagedError) {
  use outcome <- result.try(loader.load(source, engine.prelude_source))
  Ok(
    Engine(
      ..engine,
      program: program.merge(outcome.program, engine.program),
      warnings: outcome.warnings,
      activated_modules: track_activation(
        engine.activated_modules,
        outcome.exported_module,
      ),
    ),
  )
}

/// Compile the module-RPC system module (`serve/2` + `_activate/2`) and merge it into
/// `base`. Graceful: an unreadable / uncompilable system module leaves `base`
/// unchanged (module RPC unavailable, engine otherwise fine).
fn merge_system_module(
  base: BytecodeProgram,
  prelude_source: String,
) -> BytecodeProgram {
  case read_self_source(system_module_path) {
    Ok(src) ->
      case loader.load(src, prelude_source) {
        Ok(outcome) -> program.merge(outcome.program, base)
        Error(_) -> base
      }
    Error(_) -> base
  }
}

/// Add an exported module name to the activation set (deduped), or leave it unchanged
/// for a non-exporting load.
fn track_activation(
  existing: List(String),
  exported: Option(String),
) -> List(String) {
  case exported {
    Some(name) ->
      case list.contains(existing, name) {
        True -> existing
        False -> [name, ..existing]
      }
    None -> existing
  }
}

/// As `load`, but PATH-AWARE: applies the directory `self.glp` scope chain
/// (typed-glp-manual §19.6, feature 059 T078 Part B). The ancestor `self.glp` files
/// on `path`'s directory chain are discovered (root-first, siblings excluded) and
/// merged into the type environment with nearer-wins shadowing. `source` is the
/// already-read file content; `path` is used only to locate the scope chain. The REPL
/// `load <file>` command routes here so a loaded file sees its directory's types
/// (Dart `GlpEngine.loadSource` + `discoverSelfChain` + `_buildAncestorScope`).
pub fn load_file(
  engine: Engine,
  source: String,
  path: String,
) -> Result(Engine, StagedError) {
  let ancestor_sources =
    module_hierarchy.discover_ancestor_self_chain(path)
    |> list.filter_map(read_self_source)
  use outcome <- result.try(loader.load_with_scope(
    source,
    engine.prelude_source,
    ancestor_sources,
  ))
  Ok(
    Engine(
      ..engine,
      program: program.merge(outcome.program, engine.program),
      warnings: outcome.warnings,
      activated_modules: track_activation(
        engine.activated_modules,
        outcome.exported_module,
      ),
    ),
  )
}

/// Read + UTF-8-decode an ancestor `self.glp`; an unreadable / non-UTF-8 ancestor is
/// SKIPPED (returns `Error(Nil)`, dropped by `filter_map`) — a scope-chain file that
/// cannot be read simply does not contribute (never a hard failure of the user load).
fn read_self_source(path: String) -> Result(String, Nil) {
  case read_file(path) {
    Ok(bits) -> bit_array.to_string(bits) |> result.replace_error(Nil)
    Error(_) -> Error(Nil)
  }
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

// ── configure + host kernel injection (T068 host API · T069 composition root) ──

/// This engine's current configuration (budgets, trace, injected kernels).
pub fn config(engine: Engine) -> EngineConfig {
  engine.config
}

/// Replace the engine configuration (T068 `configure`): the reduction budget + fuel the
/// config-driven `run` uses, the trace default, and the host-kernel table. The engine
/// stays a pure value — no global state is touched (FR-009).
pub fn configure(engine: Engine, config: EngineConfig) -> Engine {
  Engine(..engine, config: config)
}

/// Register a host-injected body kernel under `(name, arity)` (T069 composition-root
/// seam). The engine never references the kernel by name — a BODY Spawn label-miss on
/// `name/arity` consults the table and runs it. Re-registering `(name, arity)` replaces
/// the prior kernel. The host wires its own kernels through this API alone.
pub fn register_kernel(
  engine: Engine,
  name: String,
  arity: Int,
  kernel: HostKernel,
) -> Engine {
  let config = engine.config
  Engine(
    ..engine,
    config: EngineConfig(
      ..config,
      host_kernels: dict.insert(config.host_kernels, #(name, arity), kernel),
    ),
  )
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
  // The config-driven entry (T068): honour the configured fuel + trace default. The
  // explicit `run_with_limit*` variants keep taking an ad-hoc fuel for the REPL `:limit`.
  let #(engine, envelope, _output, _traces) =
    run_with_limit_traced(engine, goal, engine.config.fuel, engine.config.trace)
  #(engine, envelope)
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

// ── link-aware run (T074) ─────────────────────────────────────────────────────

/// Execute `goal` under the LINK DRIVER (T074): a `LinkRuntime` with the loopback +
/// tcp transports registered is threaded through the reduction so the effectful
/// `_link_*` kernels run, and the inbound pump (`link_pump.drive`) feeds arriving
/// frames onto the `In` stream + drains `Out` to the wire until the goal completes.
/// Returns the result envelope + captured `_output/1` lines. Used by the two-process
/// link REPL harness; the ordinary `run` path is unchanged (no link runtime → the
/// `_link_*` kernels fail non-fatally, exactly as before).
pub fn run_link(
  engine: Engine,
  goal: String,
  fuel: Int,
) -> #(Engine, ResultEnvelope, List(String)) {
  let #(envelope, output) = case run_link_goal(engine, goal, fuel) {
    Ok(#(env, out)) -> #(env, out)
    Error(reason) -> #(failed_envelope(reason), [])
  }
  #(engine, envelope, output)
}

fn run_link_goal(
  engine: Engine,
  goal: String,
  fuel: Int,
) -> Result(#(ResultEnvelope, List(String)), String) {
  use atom <- result.try(parse_goal(goal))
  let label = atom.functor <> "/" <> int.to_string(ast.atom_arity(atom))
  use entry <- result.try(
    program.label_pc(engine.program, label)
    |> result.replace_error("predicate " <> label <> " not found"),
  )
  use boot <- result.try(goal_boot.setup_goal(heap.new(), atom))
  use runtime <- result.try(new_link_runtime())

  let sched =
    scheduler.new(engine.program, boot.heap)
    |> scheduler.with_host_kernels(engine.config.host_kernels)
  let #(sched, _goal_id) = scheduler.boot(sched, label, entry, boot.regs)
  let #(sched, _runtime, status) =
    link_pump.drive(sched, runtime, engine.config.reduction_budget, fuel)

  finish_run(sched, boot.query_var_writers, status)
}

/// A fresh `LinkRuntime` with the loopback + tcp transports registered (the schemes
/// the acceptance link programs use). A registration conflict is a trusted-config
/// failure, surfaced as a run error.
fn new_link_runtime() -> Result(LinkRuntime, String) {
  let runtime = link_runtime.new()
  use transports <- result.try(transport_registry.register(
    runtime.transports,
    loopback.new(),
  ))
  use transports <- result.try(transport_registry.register(transports, tcp.new()))
  Ok(link_runtime.with_transports(runtime, transports))
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

  // With loaded exported modules, activate them (spawn `serve/2` service loops over
  // channels) and run under the module driver so a `M # goal(...)` call routes (T078
  // residual #1a). Otherwise the plain one-shot path (unchanged).
  case engine.activated_modules {
    [] -> {
      let sched =
        scheduler.new(engine.program, boot.heap)
        |> scheduler.with_trace(trace)
        |> scheduler.with_host_kernels(engine.config.host_kernels)
      let #(sched, _goal_id) = scheduler.boot(sched, label, entry, boot.regs)
      let #(sched, status) =
        scheduler.run(sched, engine.config.reduction_budget, fuel)
      use #(envelope, output) <- result.try(finish_run(
        sched,
        boot.query_var_writers,
        status,
      ))
      Ok(#(envelope, output, scheduler.trace_lines(sched)))
    }
    modules -> {
      use #(sched, runtime) <- result.try(activate_modules(
        engine,
        modules,
        boot.heap,
        trace,
      ))
      let #(sched, _goal_id) = scheduler.boot(sched, label, entry, boot.regs)
      let #(sched, status, _runtime) =
        scheduler.run_module(sched, engine.config.reduction_budget, fuel, runtime)
      use #(envelope, output) <- result.try(finish_run(
        sched,
        boot.query_var_writers,
        status,
      ))
      Ok(#(envelope, output, scheduler.trace_lines(sched)))
    }
  }
}

/// Stand up the module-RPC service loops for a run: allocate a channel per activated
/// module, boot each module's `serve/2` as an INFRASTRUCTURE goal (excluded from the
/// run's terminal status), and register the channel in a fresh `ModuleRuntime` (T078
/// residual #1a). `serve/2` missing (system module absent) is a run error.
fn activate_modules(
  engine: Engine,
  modules: List(String),
  heap: heap.Heap,
  trace: Bool,
) -> Result(#(scheduler.Engine, ModuleRuntime), String) {
  use serve_pc <- result.try(
    program.label_pc(engine.program, "serve/2")
    |> result.replace_error(
      "module RPC: serve/2 not found (system module missing)",
    ),
  )
  let #(heap, chans) =
    list.fold(modules, #(heap, []), fn(acc, name) {
      let #(h, cs) = acc
      let #(h, ch_w, ch_r) = heap.allocate_variable(h)
      #(h, [#(name, ch_w, ch_r), ..cs])
    })
  let sched =
    scheduler.new(engine.program, heap)
    |> scheduler.with_trace(trace)
    |> scheduler.with_host_kernels(engine.config.host_kernels)
    // `serve/2` goals (initial + recursive) are infrastructure — parked service loops
    // that must not make the run report `Suspended`.
    |> scheduler.mark_infrastructure("serve/2")
  let #(sched, runtime) =
    list.fold(chans, #(sched, module_runtime.new()), fn(acc, chan) {
      let #(sched, rt) = acc
      let #(name, ch_w, ch_r) = chan
      let regs =
        program.new_regs()
        |> program.set_reg(0, ConstTerm(ConstAtom(name)))
        |> program.set_reg(1, VarRef(ch_r))
      let #(sched, _id) = scheduler.boot(sched, "serve/2", serve_pc, regs)
      #(sched, module_runtime.activate(rt, name, ch_w))
    })
  Ok(#(sched, runtime))
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
  let sched =
    scheduler.new(engine.program, boot.heap)
    |> scheduler.with_trace(engine.config.trace)
    |> scheduler.with_host_kernels(engine.config.host_kernels)
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
        scheduler.step(session.sched, engine.config.reduction_budget)
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
