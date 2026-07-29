//// glp/mad/globalize tests (feature 050 T050.A1) — host-level Globalize `T_p↑`
//// (spec §5.1, worked example §5.4): writer → entry + `_w(p,i)` no spawn; reader →
//// `_r(p,i)` + global_send spawn no entry; single shared counter from 1.

import gleeunit/should
import glp/mad/global_name.{ReaderName, WriterName}
import glp/mad/globalize.{
  GlobalizeResult, ReaderVar, Spawn, WriterVar, collect_vars, globalize,
  globalize_term,
}
import glp/mad/global_writers_table.{GlobalizeEntry} as wt
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstInt, ConstTerm, StructTerm, VarRef, cons, nil}

fn p() {
  ConstTerm(ConstAtom("p"))
}

fn q() {
  ConstTerm(ConstAtom("q"))
}

fn gn(functor: String, agent: terms.Term, index: Int) {
  StructTerm(functor, [agent, ConstTerm(ConstInt(index))])
}

// A fresh heap with exactly one local pair. writer_addr = 0, reader_addr = 1.
fn one_pair() -> #(heap.Heap, Int, Int) {
  heap.allocate_variable(heap.new())
}

pub fn collect_vars_classifies_writer_and_reader_test() {
  let #(h, w, r) = one_pair()
  collect_vars(VarRef(w), h) |> should.equal([WriterVar(writer_addr: w, reader_addr: r)])
  collect_vars(VarRef(r), h) |> should.equal([ReaderVar(reader_addr: r, writer_addr: w)])
}

pub fn collect_vars_skips_non_vars_and_recurses_test() {
  let #(h, w, _r) = one_pair()
  // constants are not variables; struct args are traversed in order.
  collect_vars(ConstTerm(ConstAtom("a")), h) |> should.equal([])
  collect_vars(StructTerm("f", [ConstTerm(ConstAtom("a")), VarRef(w)]), h)
  |> should.equal([WriterVar(writer_addr: w, reader_addr: 1)])
}

pub fn globalize_writer_creates_entry_no_spawn_test() {
  // spec §5.1: writer Y → entry (Y,q) at index 1, `_w(p,1)`, NO spawn.
  let #(h, w, _r) = one_pair()
  let vars = collect_vars(VarRef(w), h)
  let #(t, res) = globalize(vars, p(), q(), wt.new(p()))
  res |> should.equal(GlobalizeResult(names: [WriterName(p(), 1)], spawns: []))
  wt.lookup(t, 1) |> should.equal(Ok(GlobalizeEntry(w, q())))
  wt.globalize_count(t) |> should.equal(1)
  globalize_term(VarRef(w), vars, res) |> should.equal(gn("_w", p(), 1))
}

pub fn globalize_reader_spawns_no_entry_test() {
  // spec §5.1: reader Y? → `_r(p,1)`, spawn global_send(Y?,_r(p,1),q), NO entry.
  let #(h, w, r) = one_pair()
  let vars = collect_vars(VarRef(r), h)
  let #(t, res) = globalize(vars, p(), q(), wt.new(p()))
  res
  |> should.equal(GlobalizeResult(
    names: [ReaderName(p(), 1)],
    spawns: [Spawn(watch_addr: w, name: ReaderName(p(), 1), dest: q())],
  ))
  wt.globalize_count(t) |> should.equal(0)
  globalize_term(VarRef(r), vars, res) |> should.equal(gn("_r", p(), 1))
}

pub fn globalize_export_both_ends_of_pair_test() {
  // spec §5.4: p exports `[X, X?]` to q. Index 0 is the serializer, so indices start
  // at 1. Writer X → entry (X,q) at 1, `_w(p,1)`. Reader X? → `_r(p,2)`, spawn. The
  // ONE counter is shared: 1 then 2.
  let #(h, w, r) = one_pair()
  let term = cons(VarRef(w), cons(VarRef(r), nil()))
  let vars = collect_vars(term, h)
  vars
  |> should.equal([
    WriterVar(writer_addr: w, reader_addr: r),
    ReaderVar(reader_addr: r, writer_addr: w),
  ])
  let #(t, res) = globalize(vars, p(), q(), wt.new(p()))
  res
  |> should.equal(GlobalizeResult(
    names: [WriterName(p(), 1), ReaderName(p(), 2)],
    spawns: [Spawn(watch_addr: w, name: ReaderName(p(), 2), dest: q())],
  ))
  wt.lookup(t, 1) |> should.equal(Ok(GlobalizeEntry(w, q())))
  wt.globalize_count(t) |> should.equal(1)
  // T_p↑ = [_w(p,1), _r(p,2)]
  globalize_term(term, vars, res)
  |> should.equal(cons(gn("_w", p(), 1), cons(gn("_r", p(), 2), nil())))
}
