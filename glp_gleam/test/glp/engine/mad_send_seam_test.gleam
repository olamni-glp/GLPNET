//// madGLP `_send` dispatch-seam test (feature 050 T050.A2).
////
//// Proves the BODY Spawn label-miss hook (runner `mad_spawn`) actually reaches the
//// `_send` effectful kernel and threads the updated `MadState` back out on `Reduced`
//// — the end-to-end seam, not just the kernel in isolation (that is
//// `mad_kernels_test`). `_send` is a clause-less prelude host kernel, so a body call
//// compiles to a Spawn whose label misses every program label.

import gleam/option.{Some}
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/mad/global_name.{WriterName, to_term}
import glp/mad/global_writers_table as wt
import glp/mad/mad_kernels.{MadState}
import glp/mad/message.{Message}
import glp/engine/runner
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstTerm}

// A tiny system-mode module: `snd/3` forwards its three inputs to the `_send`
// host kernel (declared clause-less, like the prelude).
const seam_source = "-mode(system).
procedure _send(_?, _?, _?).
procedure snd(_?, _?, _?).
snd(T, G, Q) :- '_send'(T?, G?, Q?)."

fn p() {
  ConstTerm(ConstAtom("p"))
}

fn q() {
  ConstTerm(ConstAtom("q"))
}

pub fn send_dispatched_through_reduce_threads_mp_test() {
  let assert Ok(outcome) = loader.load(seam_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "snd/3")
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("hello")))
    |> program.set_reg(1, to_term(WriterName(q(), 5)))
    |> program.set_reg(2, q())
  let ctx =
    runner.with_mad(runner.new_context(heap.new(), regs), MadState(wt.new(p()), [], []))

  let out = runner.reduce(prog, ctx, kappa, 1000)

  // The reduction committed AND carried the outgoing message out on `Reduced.mad`.
  let assert runner.Reduced(mad: Some(state), ..) = out
  state.m_p
  |> should.equal([Message(WriterName(q(), 5), ConstTerm(ConstAtom("hello")), q())])
}
