//// glp/mad/mad_engine tests (feature 050 T050.A3) — the MadEngine wrapping
//// scheduler.Engine: boot serializer entry c₀ (spec §7/§4.1), the Send drain
//// (`step` returning outgoing M_p), and the three Receive cases (spec §8.3):
//// serializer cold-call, globalize-writer (i>0), localize-reader. Plus the
//// reduce-side `global_send` spawn-lowering (present vs absent global_send/3).

import gleam/option.{None, Some}
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/scheduler.{StepErrored, StepReduced}
import glp/mad/global_name.{ReaderName, WriterName, to_term}
import glp/mad/global_writers_table.{GlobalizeEntry, LocalizeEntry} as wt
import glp/mad/mad_engine
import glp/mad/message.{Message}
import glp/runtime/heap.{Bound}
import glp/runtime/terms.{ConstAtom, ConstTerm, StructTerm, VarRef, cons}

fn p() {
  ConstTerm(ConstAtom("p"))
}

fn q() {
  ConstTerm(ConstAtom("q"))
}

fn hello() {
  ConstTerm(ConstAtom("hello"))
}

// A minimal system-mode seam module: `snd/3` forwards to the `_send` host kernel.
const seam_source = "-mode(system).
procedure _send(_?, _?, _?).
procedure snd(_?, _?, _?).
snd(T, G, Q) :- '_send'(T?, G?, Q?)."

// As above, plus a forwarding `global_send/3` LABEL so the reduce-side reader-spawn
// lowering (T050.A3) has a target to lower into. (The canonical `known`-guarded
// prelude clause is T050.A4.)
const gs_source = "-mode(system).
procedure _send(_?, _?, _?).
procedure snd(_?, _?, _?).
snd(T, G, Q) :- '_send'(T?, G?, Q?).
procedure global_send(_?, _?, _?).
global_send(T, G, Q) :- '_send'(T?, G?, Q?)."

fn load(source: String) -> program.BytecodeProgram {
  let assert Ok(outcome) = loader.load(source, "")
  outcome.program
}

// ── Boot: c₀ serializer entry (spec §7 / §4.1) ─────────────────────────────────

pub fn boot_installs_serializer_entry_test() {
  let me = mad_engine.new(load(seam_source), p())
  let w_p = mad_engine.writers_table(me)
  // Permanent index-0 serializer entry present; no globalize/localize entries; the
  // shared counter starts at 1 (0 reserved for the serializer — spec §3.2).
  wt.serializer_addr(w_p) |> should.not_equal(None)
  wt.globalize_count(w_p) |> should.equal(0)
  wt.localize_count(w_p) |> should.equal(0)
  wt.next_index(w_p) |> should.equal(1)
}

// ── Receive case 1: serializer cold-call (spec §8.3 serializer) ────────────────

pub fn receive_serializer_extends_network_input_test() {
  let me = mad_engine.new(load(seam_source), p())
  let w_p = mad_engine.writers_table(me)
  let assert Some(old_serializer) = wt.serializer_addr(w_p)
  let net_r = mad_engine.net_in_reader(me)

  // Cold-call `_w(p,0) := [hello | _w(p,0)]`.
  let assert Ok(me) =
    mad_engine.receive(
      me,
      WriterName(p(), 0),
      cons(hello(), to_term(WriterName(p(), 0))),
    )

  // The old serializer writer is now bound to a list cell `[hello | N'?]`; the agent's
  // net-input reader sees the extended stream; the serializer advanced to the fresh
  // writer (permanent entry updated, NOT removed).
  let h = scheduler.heap(mad_engine.engine(me))
  let assert Ok(#(_, Bound(cell))) = heap.deref(h, old_serializer)
  let assert StructTerm(".", [head, VarRef(_fresh_r)]) = cell
  head |> should.equal(hello())
  let assert Ok(#(_, Bound(net_cell))) = heap.deref(h, net_r)
  net_cell |> should.equal(cell)
  let w_p2 = mad_engine.writers_table(me)
  wt.serializer_addr(w_p2) |> should.not_equal(Some(old_serializer))
  // Still exactly the serializer, no globalize/localize entries added for a ground
  // cold-call.
  wt.globalize_count(w_p2) |> should.equal(0)
}

// ── Receive case 2: globalize-writer, i>0 (spec §8.3 writer) ────────────────────

pub fn send_then_receive_writer_binds_and_removes_entry_test() {
  let prog = load(seam_source)
  let me = mad_engine.new(prog, p())
  // Allocate the writer X we export inside `pkg(X)` (p keeps the writer; q sends back).
  let #(me, x_writer, _x_reader) = mad_engine.alloc_local(me)

  let assert Ok(kappa) = program.label_pc(prog, "snd/3")
  let regs =
    program.new_regs()
    |> program.set_reg(0, StructTerm("pkg", [VarRef(x_writer)]))
    |> program.set_reg(1, to_term(WriterName(q(), 5)))
    |> program.set_reg(2, q())
  let #(me, _id) = mad_engine.boot(me, "snd/3", kappa, regs)
  let #(me, outcome, msgs) = mad_engine.step(me, 1000)

  // Send: message carries the LINK name `_w(q,5)`, term `pkg(_w(p,1))`; W_p gains a
  // GlobalizeEntry (X, q) at index 1.
  outcome |> is_reduced |> should.equal(True)
  msgs
  |> should.equal([
    Message(WriterName(q(), 5), StructTerm("pkg", [to_term(WriterName(p(), 1))]), q()),
  ])
  wt.lookup(mad_engine.writers_table(me), 1)
  |> should.equal(Ok(GlobalizeEntry(x_writer, q())))

  // Receive `_w(p,1) := answer` → bind X, remove the entry.
  let assert Ok(me) =
    mad_engine.receive(me, WriterName(p(), 1), ConstTerm(ConstAtom("answer")))
  let h = scheduler.heap(mad_engine.engine(me))
  let assert Ok(#(_, Bound(v))) = heap.deref(h, x_writer)
  v |> should.equal(ConstTerm(ConstAtom("answer")))
  wt.lookup(mad_engine.writers_table(me), 1) |> should.equal(Error(Nil))
  wt.globalize_count(mad_engine.writers_table(me)) |> should.equal(0)
}

// ── Receive case 3: localize-reader (spec §8.3 reader) ──────────────────────────

pub fn localize_then_receive_reader_binds_and_removes_entry_test() {
  let me = mad_engine.new(load(seam_source), p())

  // A cold-call carrying a nested reader name `_r(sender,3)` — localization mints a
  // LocalizeEntry (Z, sender, 3) and substitutes the fresh local reader.
  let assert Ok(me) =
    mad_engine.receive(
      me,
      WriterName(p(), 0),
      cons(
        StructTerm("req", [to_term(ReaderName(ConstTerm(ConstAtom("sender")), 3))]),
        to_term(WriterName(p(), 0)),
      ),
    )
  let w_p = mad_engine.writers_table(me)
  wt.localize_count(w_p) |> should.equal(1)
  let assert Ok(LocalizeEntry(z_writer, _, _)) =
    wt.find_localize(w_p, ConstTerm(ConstAtom("sender")), 3)

  // Receive `_r(sender,3) := reply` → search by (sender,3), bind Z, remove entry.
  let assert Ok(me) =
    mad_engine.receive(me, ReaderName(ConstTerm(ConstAtom("sender")), 3), ConstTerm(ConstAtom("reply")))
  let h = scheduler.heap(mad_engine.engine(me))
  let assert Ok(#(_, Bound(v))) = heap.deref(h, z_writer)
  v |> should.equal(ConstTerm(ConstAtom("reply")))
  wt.localize_count(mad_engine.writers_table(me)) |> should.equal(0)
}

// ── Reduce-side spawn lowering (spec §5.1 reader branch → global_send/3) ─────────

pub fn reader_send_lowers_spawn_when_global_send_loaded_test() {
  let prog = load(gs_source)
  let me = mad_engine.new(prog, p())
  // Export a READER Y? inside `pkg(Y?)` — globalization emits a `global_send` spawn.
  let #(me, _y_writer, y_reader) = mad_engine.alloc_local(me)

  let assert Ok(kappa) = program.label_pc(prog, "snd/3")
  let regs =
    program.new_regs()
    |> program.set_reg(0, StructTerm("pkg", [VarRef(y_reader)]))
    |> program.set_reg(1, to_term(WriterName(q(), 5)))
    |> program.set_reg(2, q())
  let #(me, _id) = mad_engine.boot(me, "snd/3", kappa, regs)
  let #(_me, outcome, msgs) = mad_engine.step(me, 1000)

  // With global_send/3 loaded the reader spawn is lowered into a runnable goal — the
  // step REDUCES (no error), and the message carries `pkg(_r(p,1))`.
  outcome |> is_reduced |> should.equal(True)
  msgs
  |> should.equal([
    Message(WriterName(q(), 5), StructTerm("pkg", [to_term(ReaderName(p(), 1))]), q()),
  ])
}

pub fn reader_send_errors_when_global_send_absent_test() {
  let prog = load(seam_source)
  let me = mad_engine.new(prog, p())
  let #(me, _y_writer, y_reader) = mad_engine.alloc_local(me)

  let assert Ok(kappa) = program.label_pc(prog, "snd/3")
  let regs =
    program.new_regs()
    |> program.set_reg(0, StructTerm("pkg", [VarRef(y_reader)]))
    |> program.set_reg(1, to_term(WriterName(q(), 5)))
    |> program.set_reg(2, q())
  let #(me, _id) = mad_engine.boot(me, "snd/3", kappa, regs)
  let #(_me, outcome, _msgs) = mad_engine.step(me, 1000)

  // No global_send/3 to lower into → surfaced as StepErrored, never a silent drop.
  case outcome {
    StepErrored(_) -> True
    _ -> False
  }
  |> should.equal(True)
}

// ── Receive on a missing entry is surfaced (spec-v5.3-PURE; dedup is T052) ───────

pub fn receive_missing_entry_is_error_test() {
  let me = mad_engine.new(load(seam_source), p())
  mad_engine.receive(me, WriterName(p(), 99), ConstTerm(ConstAtom("x")))
  |> is_error
  |> should.equal(True)
}

// helpers -----------------------------------------------------------------------

fn is_reduced(outcome) -> Bool {
  case outcome {
    StepReduced(..) -> True
    _ -> False
  }
}

fn is_error(r) -> Bool {
  case r {
    Error(_) -> True
    Ok(_) -> False
  }
}
