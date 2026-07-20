//// glp/mad/localize tests (feature 050 T050.A1) — host-level Localize `T_q↓`
//// (spec §5.2, worked example §5.4): `_w(p,i)` → fresh writer + global_send spawn
//// no entry; `_r(p,i)` → fresh reader + entry no spawn. Closes the §5.4 loop: p's
//// `[_w(p,1), _r(p,2)]` localizes at q to `[Y_q, Z_q?]`.

import gleeunit/should
import glp/mad/global_name.{ReaderName, WriterName}
import glp/mad/globalize.{Spawn}
import glp/mad/localize.{
  FreshPair, LocalizeResult, extract_global_names, localize, localize_term,
}
import glp/mad/global_writers_table.{LocalizeEntry} as wt
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

pub fn extract_global_names_in_order_test() {
  // spec §5.2: names extracted in order; a `_w`/`_r` name is a leaf (not descended).
  let term = cons(gn("_w", p(), 1), cons(gn("_r", p(), 2), nil()))
  extract_global_names(term)
  |> should.equal([WriterName(p(), 1), ReaderName(p(), 2)])
}

pub fn extract_ignores_ordinary_structs_test() {
  extract_global_names(StructTerm("f", [ConstTerm(ConstAtom("a"))]))
  |> should.equal([])
}

pub fn localize_writer_name_fresh_writer_and_spawn_test() {
  // spec §5.2: `_w(p,1)` → fresh pair, use writer Y_q, spawn global_send(Y_q?,_w(p,1),p),
  // NO entry. Fresh heap allocates writer=0, reader=1.
  let #(h, t, res) = localize([WriterName(p(), 1)], heap.new(), wt.new(q()))
  res
  |> should.equal(LocalizeResult(
    fresh_pairs: [FreshPair(writer_addr: 0, reader_addr: 1)],
    use_reader: [False],
    spawns: [Spawn(watch_addr: 0, name: WriterName(p(), 1), dest: p())],
  ))
  wt.localize_count(t) |> should.equal(0)
  // term substitutes to the fresh WRITER Y_q (addr 0).
  localize_term(gn("_w", p(), 1), [WriterName(p(), 1)], res)
  |> should.equal(VarRef(0))
  // heap actually grew by one pair.
  heap.is_writer(h, 0) |> should.equal(True)
  heap.is_reader(h, 1) |> should.equal(True)
}

pub fn localize_reader_name_fresh_reader_and_entry_test() {
  // spec §5.2: `_r(p,2)` → fresh pair, entry (Z_q,p,2), use reader Z_q?, NO spawn.
  let #(_h, t, res) = localize([ReaderName(p(), 2)], heap.new(), wt.new(q()))
  res
  |> should.equal(LocalizeResult(
    fresh_pairs: [FreshPair(writer_addr: 0, reader_addr: 1)],
    use_reader: [True],
    spawns: [],
  ))
  // entry stores the fresh WRITER (addr 0), keyed by (remote p, remote index 2).
  wt.find_localize(t, p(), 2) |> should.equal(Ok(LocalizeEntry(0, p(), 2)))
  wt.localize_count(t) |> should.equal(1)
  // term substitutes to the fresh READER Z_q? (addr 1).
  localize_term(gn("_r", p(), 2), [ReaderName(p(), 2)], res)
  |> should.equal(VarRef(1))
}

pub fn localize_export_both_ends_of_pair_test() {
  // spec §5.4: q localizes p's `[_w(p,1), _r(p,2)]`. Two INDEPENDENT fresh pairs:
  // (0,1) for `_w` → writer Y_q; (2,3) for `_r` → reader Z_q?. Result term `[Y_q, Z_q?]`.
  let names = [WriterName(p(), 1), ReaderName(p(), 2)]
  let term = cons(gn("_w", p(), 1), cons(gn("_r", p(), 2), nil()))
  let #(_h, t, res) = localize(names, heap.new(), wt.new(q()))
  res
  |> should.equal(LocalizeResult(
    fresh_pairs: [
      FreshPair(writer_addr: 0, reader_addr: 1),
      FreshPair(writer_addr: 2, reader_addr: 3),
    ],
    use_reader: [False, True],
    spawns: [Spawn(watch_addr: 0, name: WriterName(p(), 1), dest: p())],
  ))
  wt.find_localize(t, p(), 2) |> should.equal(Ok(LocalizeEntry(2, p(), 2)))
  wt.localize_count(t) |> should.equal(1)
  // T_q↓ = [Y_q, Z_q?] = [VarRef(0), VarRef(3)]
  localize_term(term, names, res)
  |> should.equal(cons(VarRef(0), cons(VarRef(3), nil())))
}
