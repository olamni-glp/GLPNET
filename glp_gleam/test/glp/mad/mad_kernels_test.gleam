//// glp/mad/mad_kernels tests (feature 050 T050.A2) — the `_send` effectful kernel
//// (spec §11.5): serializer list-wrap (index 0) + normal direct (index>0), writer
//// entry / reader spawn via globalize (T050.A1), and non-fatal aborts on a malformed
//// G / unbound Q.

import gleeunit/should
import glp/mad/global_name.{ReaderName, WriterName, to_term}
import glp/mad/globalize.{Spawn}
import glp/mad/global_writers_table.{GlobalizeEntry} as wt
import glp/mad/mad_kernels.{
  type MadState, MadAbort, MadEffect, MadState, mad_dispatch, mad_is_kernel,
}
import glp/mad/message.{Message}
import glp/runtime/heap
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

// A fresh MadState owned by p, empty M_p / spawns.
fn state0() -> MadState {
  MadState(w_p: wt.new(p()), m_p: [], mad_spawns: [])
}

pub fn is_kernel_only_send_3_test() {
  mad_is_kernel("_send", 3) |> should.equal(True)
  mad_is_kernel("_send", 2) |> should.equal(False)
  mad_is_kernel("_add", 3) |> should.equal(False)
}

pub fn unknown_mad_kernel_is_error_test() {
  mad_dispatch(heap.new(), state0(), "_frobnicate", 3, [hello(), hello(), q()])
  |> should.equal(Error(Nil))
}

pub fn send_normal_ground_term_test() {
  // spec §11.5 normal (index>0): `_w(q,5) := hello` sent directly; no vars → no entry,
  // no spawn.
  let g = to_term(WriterName(q(), 5))
  let assert Ok(MadEffect(_h, state, [])) =
    mad_dispatch(heap.new(), state0(), "_send", 3, [hello(), g, q()])
  state.m_p |> should.equal([Message(WriterName(q(), 5), hello(), q())])
  state.mad_spawns |> should.equal([])
  wt.globalize_count(state.w_p) |> should.equal(0)
}

pub fn send_serializer_wraps_in_list_test() {
  // spec §4.1/§11.5 serializer (index 0): `_w(q,0) := [hello | _w(q,0)]` — content
  // wrapped in a list cell, serializer writer reused in the tail.
  let g = to_term(WriterName(q(), 0))
  let assert Ok(MadEffect(_h, state, [])) =
    mad_dispatch(heap.new(), state0(), "_send", 3, [hello(), g, q()])
  state.m_p
  |> should.equal([
    Message(WriterName(q(), 0), cons(hello(), to_term(WriterName(q(), 0))), q()),
  ])
}

pub fn send_writer_var_mints_entry_no_spawn_test() {
  // T = f(X) with X an unbound writer. Globalize (T050.A1) → entry (X,q) at index 1,
  // T↑ = f(_w(p,1)), NO spawn. Message name is the LINK G (=_w(q,5)), term is T↑.
  let #(h, w, _r) = heap.allocate_variable(heap.new())
  let t = StructTerm("f", [VarRef(w)])
  let g = to_term(WriterName(q(), 5))
  let assert Ok(MadEffect(_h, state, [])) =
    mad_dispatch(h, state0(), "_send", 3, [t, g, q()])
  state.m_p
  |> should.equal([
    Message(WriterName(q(), 5), StructTerm("f", [to_term(WriterName(p(), 1))]), q()),
  ])
  wt.lookup(state.w_p, 1) |> should.equal(Ok(GlobalizeEntry(w, q())))
  state.mad_spawns |> should.equal([])
}

pub fn send_reader_var_emits_spawn_no_entry_test() {
  // T = f(Y?) with Y? an unbound reader. Globalize → T↑ = f(_r(p,1)), spawn
  // global_send(Y?,_r(p,1),q), NO entry. The spawn watches the reader's WRITER addr.
  let #(h, w, r) = heap.allocate_variable(heap.new())
  let t = StructTerm("f", [VarRef(r)])
  let g = to_term(WriterName(q(), 5))
  let assert Ok(MadEffect(_h, state, [])) =
    mad_dispatch(h, state0(), "_send", 3, [t, g, q()])
  state.m_p
  |> should.equal([
    Message(WriterName(q(), 5), StructTerm("f", [to_term(ReaderName(p(), 1))]), q()),
  ])
  state.mad_spawns
  |> should.equal([Spawn(watch_addr: w, name: ReaderName(p(), 1), dest: q())])
  wt.globalize_count(state.w_p) |> should.equal(0)
}

pub fn send_abort_bad_global_name_test() {
  // G is not a `_w/2`/`_r/2` name → non-fatal abort (no message queued).
  let assert Ok(MadAbort(_)) =
    mad_dispatch(heap.new(), state0(), "_send", 3, [
      hello(),
      ConstTerm(ConstAtom("foo")),
      q(),
    ])
}

pub fn send_abort_unbound_dest_test() {
  // Q resolves to an unbound writer → non-fatal abort.
  let #(h, qw, _qr) = heap.allocate_variable(heap.new())
  let g = to_term(WriterName(q(), 5))
  let assert Ok(MadAbort(_)) =
    mad_dispatch(h, state0(), "_send", 3, [hello(), g, VarRef(qw)])
}
