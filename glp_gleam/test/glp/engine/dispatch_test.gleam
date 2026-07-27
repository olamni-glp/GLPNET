//// Module-dispatch B2 tests (wave-3, feature 060): the `_activate/2` kernel →
//// `RemoteSpawn` data path → scheduler registry resolution.
////
//// Dart reference: activateKernel (body_kernels.dart:820) enqueues the goal
//// directly; the Gleam kernel is heap-only, so the spawn rides out as
//// `KSuccess.spawns` → `runner.Reduced.remote` → `scheduler.apply_remote_spawns`
//// (research.md §"Concrete Gleam mapping", points 1–3).

import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/kernels
import glp/engine/scheduler
import glp/runtime/heap
import glp/runtime/terms.{
  ConstAtom, ConstInt, ConstTerm, StructTerm, VarRef,
}

// ── kernel level: `_activate/2` emits the spawn as data ──────────────────────

pub fn activate_is_a_kernel_test() {
  kernels.is_kernel("_activate", 2) |> should.be_true
}

// `_activate('$module'(3), double(5, W))` → one RemoteSpawn(3, "double/2", args),
// heap untouched, args passed through with the writer VarRef preserved.
pub fn activate_emits_remote_spawn_test() {
  let #(h, w, _r) = heap.allocate_variable(heap.new())
  let goal =
    StructTerm("double", [ConstTerm(ConstInt(5)), VarRef(w)])
  let assert Ok(kernels.KSuccess(
    _h,
    [],
    [],
    [kernels.RemoteSpawn(3, "double/2", [ConstTerm(ConstInt(5)), VarRef(w2)])],
  )) = kernels.dispatch(h, "_activate", 2, [kernels.module_sentinel(3), goal])
  w2 |> should.equal(w)
}

// A first argument that is not a '$module' sentinel aborts (Dart: not a
// ModuleTerm → abort).
pub fn activate_non_module_aborts_test() {
  let h = heap.new()
  let assert Ok(kernels.KAbort(_)) =
    kernels.dispatch(h, "_activate", 2, [
      ConstTerm(ConstAtom("nope")),
      StructTerm("g", [ConstTerm(ConstInt(1))]),
    ])
}

// A non-struct goal silently succeeds with no spawn (Dart: `goalArg is!
// StructTerm` → success fallback).
pub fn activate_non_struct_goal_succeeds_silently_test() {
  let h = heap.new()
  let assert Ok(kernels.KSuccess(_h, [], [], [])) =
    kernels.dispatch(h, "_activate", 2, [
      kernels.module_sentinel(1),
      ConstTerm(ConstAtom("just_an_atom")),
    ])
}

// ── end-to-end: `_activate` in a clause body runs the goal in the MODULE's
// bytecode via the scheduler registry ────────────────────────────────────────

// The serve-shaped dispatcher: the goal term arrives WHOLE via a reader (its
// inner writers are heap VarRefs inside the received term, exactly as a
// channel-delivered goal's are — Dart serve/2's `'_activate'(Module?, Goal?)`).
// Compiled via `compile_prelude` (parse → SRSW → PE → codegen, NO type check):
// `_activate/2` is deliberately absent from the type-check prelude in BOTH
// runtimes — only engine-embedded system source calls it, and Dart compiles
// that with `CompileOptions()` where `typeCheck = false` (compiler.dart:27,
// glp_engine.dart:179).
const main_source = "-mode(system).
procedure act(_?, _?).
act(M, G) :- '_activate'(M?, G?)."

const module_source = "Ans ::= hi ; done.
procedure echo(Ans?, Ans).
echo(hi, done).
echo(done, hi)."

// act('$module'(idx), echo(hi, Out)): the body's `_activate` dispatches the goal
// into the registered module program; echo's clause commits Out = done. Proves
// the kernel→runner→scheduler data path AND that the dispatched goal reduces in
// the module's bytecode (its entry PC is only meaningful there), with writer
// polarity preserved through the goal term.
pub fn activate_end_to_end_runs_goal_in_module_test() {
  let assert Ok(main_prog) = loader.compile_prelude(main_source)
  let assert Ok(mod_out) = loader.load(module_source, "")
  let assert Ok(entry) = program.label_pc(main_prog, "act/2")

  let #(h, out_writer, _out_reader) = heap.allocate_variable(heap.new())
  let engine = scheduler.new(main_prog, h)
  let #(engine, idx) = scheduler.register_module(engine, mod_out.program)

  let goal =
    StructTerm("echo", [ConstTerm(ConstAtom("hi")), VarRef(out_writer)])
  let regs =
    program.new_regs()
    |> program.set_reg(0, scheduler.module_sentinel(idx))
    |> program.set_reg(1, goal)

  let #(engine, _goal_id) = scheduler.boot(engine, "act/2", entry, regs)
  let #(engine, status) = scheduler.run(engine, 1000, 1000)

  status |> should.equal(scheduler.Success)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("done"))))) =
    heap.deref(scheduler.heap(engine), out_writer)
}

// A goal whose label is missing from the module silently succeeds — no spawn,
// run completes (Dart activateKernel: "procedure not found — silently succeed").
pub fn activate_label_miss_silently_succeeds_test() {
  let assert Ok(main_prog) = loader.compile_prelude(main_source)
  // Module WITHOUT echo/2: only a stray procedure.
  let assert Ok(mod_out) =
    loader.load(
      "T ::= x.
procedure other(T?).
other(x).",
      "",
    )
  let assert Ok(entry) = program.label_pc(main_prog, "act/2")

  let #(h, out_writer, _out_reader) = heap.allocate_variable(heap.new())
  let engine = scheduler.new(main_prog, h)
  let #(engine, idx) = scheduler.register_module(engine, mod_out.program)

  let goal =
    StructTerm("echo", [ConstTerm(ConstAtom("hi")), VarRef(out_writer)])
  let regs =
    program.new_regs()
    |> program.set_reg(0, scheduler.module_sentinel(idx))
    |> program.set_reg(1, goal)

  let #(engine, _goal_id) = scheduler.boot(engine, "act/2", entry, regs)
  let #(engine, status) = scheduler.run(engine, 1000, 1000)

  // act/2 itself reduced; the dispatched goal was dropped, so Out stays unbound
  // and no user goal remains — the run reports Success.
  status |> should.equal(scheduler.Success)
  let assert Ok(#(_, heap.Unbound(_))) =
    heap.deref(scheduler.heap(engine), out_writer)
}

// An unregistered '$module' index is a structural break — surfaced as an
// engine error, never guessed (sentinels are only minted by register_module).
pub fn activate_unregistered_module_errors_test() {
  let assert Ok(main_prog) = loader.compile_prelude(main_source)
  let assert Ok(entry) = program.label_pc(main_prog, "act/2")

  let #(h, out_writer, _out_reader) = heap.allocate_variable(heap.new())
  let engine = scheduler.new(main_prog, h)

  let goal =
    StructTerm("echo", [ConstTerm(ConstAtom("hi")), VarRef(out_writer)])
  let regs =
    program.new_regs()
    |> program.set_reg(0, scheduler.module_sentinel(99))
    |> program.set_reg(1, goal)

  let #(engine, _goal_id) = scheduler.boot(engine, "act/2", entry, regs)
  let #(_engine, status) = scheduler.run(engine, 1000, 1000)

  let assert scheduler.Errored(_) = status
}
