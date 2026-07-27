//// T078 acceptance — module RPC runtime execution (feature 059
//// `close-module-system-runtime-rpc`). A module-qualified `M # goal(...)` call —
//// which previously faulted `Unimplemented` on the `Distribute` opcode — now runs
//// to completion: the goal routes onto the target module's channel stream, wakes
//// its suspended `serve/2` loop, which dispatches it via the `_activate/2` kernel to
//// the exported procedure over the shared heap cells, and the result flows back.
////
//// Faithful dynamic dispatch (§19.4/§19.7; Gabi ruling 2026-07-27) driven end to end
//// through `scheduler.run_module`.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/module_runtime
import glp/engine/scheduler
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstInt, ConstTerm, VarRef}

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> String {
  let assert Ok(bytes) = read_file(path)
  let assert Ok(text) = bit_array.to_string(bytes)
  text
}

// A client that makes a static module-qualified call (→ a `Distribute` opcode), and
// the `echo/2` procedure the target module exports (resolved by `_activate/2` in the
// merged program) — it passes its input to its output, so a bound result proves the
// input crossed the RPC and the output flowed back on the shared cell.
const modules = "imported procedure math_service#echo(Integer?, Integer).
procedure run_echo(Integer?, Integer).
run_echo(X, R?) :- math_service # echo(X?, R).
procedure echo(Integer?, Integer).
echo(X, X?)."

pub fn module_rpc_distribute_runs_to_completion_test() {
  let self_source = read_source("../programs/self.glp")
  let system = read_source("../programs/system/module_predicates.glp")
  let assert Ok(outcome) = loader.load(system <> "\n" <> modules, self_source)
  let prog = outcome.program

  // Two query cells up front: the module channel var and the client's result R.
  let #(h, ch_w, ch_r) = heap.allocate_variable(heap.new())
  let #(h, r_w, _r_r) = heap.allocate_variable(h)
  let engine = scheduler.new(prog, h)

  // Activate `math_service`: spawn its `serve/2` loop over the channel reader, and
  // register the channel writer in the module runtime.
  let assert Ok(serve_pc) = program.label_pc(prog, "serve/2")
  let serve_regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("math_service")))
    |> program.set_reg(1, VarRef(ch_r))
  let #(engine, _serve_id) =
    scheduler.boot(engine, "serve/2", serve_pc, serve_regs)
  let runtime =
    module_runtime.activate(module_runtime.new(), "math_service", ch_w)

  // Boot the client goal `run_echo(5, R)`.
  let assert Ok(rd_pc) = program.label_pc(prog, "run_echo/2")
  let client_regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstInt(5)))
    |> program.set_reg(1, VarRef(r_w))
  let #(engine, _client_id) =
    scheduler.boot(engine, "run_echo/2", rd_pc, client_regs)

  // Drive the module dispatch driver to quiescence — NO `Unimplemented` fault.
  let #(engine, status, _runtime) =
    scheduler.run_module(engine, 5000, 5000, runtime)

  // The remote call completed: the input X = 5 flowed through the RPC to the output
  // R on the shared cell (echo passes its input to its output in the head).
  let assert Ok(#(_, heap.Bound(result))) =
    heap.deref(scheduler.heap(engine), r_w)
  result |> should.equal(ConstTerm(ConstInt(5)))
  // And the run did not error.
  case status {
    scheduler.Errored(_) -> should.fail()
    _ -> Nil
  }
}
