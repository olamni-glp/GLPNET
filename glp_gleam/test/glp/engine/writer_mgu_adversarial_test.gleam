//// Adversarial writer-MGU suite (feature 050, T026 — discharges PI:14 alongside
//// the Lean proof `glp_gleam/lean/WriterMguBindsOnlyWriters/` and the prose
//// `docs/.../PROOFS/writer_mgu_binds_only_writers/PROOF.md`; contract
//// `specs/050-full-gleam-combined/contracts/proof-obligations.md`).
////
//// Where `unify_test.gleam` pins the shallow SC-003 verdict table (outcomes),
//// this suite adversarially attacks the writer-MGU INVARIANT — "binds only
//// writers, never a reader cell, never writer↔writer" — and asserts it on the
//// post-heap STATE (readers stay reader-tagged, writers stay unbound) after
//// reader/reader, writer/writer, nested-structure, and tentative-HEAD inputs,
//// plus the rejection paths (Error/Fail/Suspend, each distinct).

import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/runner
import glp/runtime/heap.{Bound, Unbound, WriterToWriter}
import glp/runtime/terms.{ConstAtom, ConstTerm, StructTerm, VarRef}
import glp/runtime/unify.{Fail, Success, Suspend, unify}

// A fresh unbound variable: returns the heap plus its writer and reader address.
fn fresh(h: heap.Heap) -> #(heap.Heap, Int, Int) {
  heap.allocate_variable(h)
}

fn c(name: String) -> terms.Term {
  ConstTerm(ConstAtom(name))
}

// True iff `addr`'s writer cell is unbound (deref terminates on Unbound).
fn is_unbound(h: heap.Heap, addr: Int) -> Bool {
  case heap.deref(h, addr) {
    Ok(#(_, Unbound(_))) -> True
    _ -> False
  }
}

// ── reader × reader → Suspend, never Fail, binds NOTHING ─────────────────────

// Two unbound readers meeting: the canonical GLP suspend-not-fail point. The
// verdict is Suspend (on the first reader's paired writer) and NEITHER reader is
// mutated and NEITHER writer is bound — readers are waited on, never bound.
pub fn reader_reader_suspends_and_binds_nothing_test() {
  let #(h, w1, r1) = fresh(heap.new())
  let #(h, w2, r2) = fresh(h)
  let assert Ok(Suspend(h2, on)) = unify(h, VarRef(r1), VarRef(r2))
  // Suspends on r1's paired writer.
  on |> should.equal(w1)
  // Both reader cells untouched; both writers still unbound.
  heap.is_reader(h2, r1) |> should.be_true
  heap.is_reader(h2, r2) |> should.be_true
  is_unbound(h2, w1) |> should.be_true
  is_unbound(h2, w2) |> should.be_true
}

// ── writer × writer → loud Error, never Fail, binds NOTHING ──────────────────

// Two unbound writers meeting: a structural violation surfaced LOUDLY as
// `WriterToWriter` (SC-004) — never collapsed to an ordinary Fail — and no cell
// is bound on this path.
pub fn writer_writer_errors_and_binds_nothing_test() {
  let #(h, w1, _r1) = fresh(heap.new())
  let #(h, w2, _r2) = fresh(h)
  unify(h, VarRef(w1), VarRef(w2))
  |> should.equal(Error(WriterToWriter(w1, w2)))
  // No binding was produced (Error carries no heap; the pre-heap is unchanged).
  is_unbound(h, w1) |> should.be_true
  is_unbound(h, w2) |> should.be_true
}

// ── writer × reader → links the WRITER, never touches the reader ─────────────

// A bindable writer against an unbound reader binds the writer (writer→reader
// link); the READER cell is never re-tagged or bound — the invariant's core
// claim, asserted on heap state.
pub fn writer_reader_links_and_never_mutates_reader_test() {
  let #(h, w1, _r1) = fresh(heap.new())
  let #(h, _w2, r2) = fresh(h)
  let assert Ok(Success(h2)) = unify(h, VarRef(w1), VarRef(r2))
  // The reader half is still a reader cell — never bound.
  heap.is_reader(h2, r2) |> should.be_true
}

// ── reader × value → Suspend, not Fail; reader never bound ───────────────────

// A ground value cannot bind a reader: the reader is waited on, not bound and
// not failed.
pub fn reader_value_suspends_not_fails_test() {
  let #(h, w1, r1) = fresh(heap.new())
  let assert Ok(Suspend(h2, on)) = unify(h, VarRef(r1), c("zero"))
  on |> should.equal(w1)
  heap.is_reader(h2, r1) |> should.be_true
  is_unbound(h2, w1) |> should.be_true
}

// ── nested structures: decomposition reaches — and binds — only the deep writer

// A writer buried two levels deep (p(_, q(W))) unified against a matching ground
// shape binds exactly that writer and nothing else.
pub fn nested_struct_binds_only_deep_writer_test() {
  let #(h, w, _r) = fresh(heap.new())
  let a = StructTerm("p", [c("zero"), StructTerm("q", [VarRef(w)])])
  let b = StructTerm("p", [c("zero"), StructTerm("q", [c("one")])])
  let assert Ok(Success(h2)) = unify(h, a, b)
  let assert Ok(#(_, Bound(ConstTerm(ConstAtom("one"))))) = heap.deref(h2, w)
}

// A reader buried two levels deep suspends the whole unification — the deep
// reader is never bound.
pub fn nested_struct_deep_reader_suspends_test() {
  let #(h, w, r) = fresh(heap.new())
  let a = StructTerm("p", [c("zero"), StructTerm("q", [VarRef(r)])])
  let b = StructTerm("p", [c("zero"), StructTerm("q", [c("one")])])
  let assert Ok(Suspend(h2, on)) = unify(h, a, b)
  on |> should.equal(w)
  heap.is_reader(h2, r) |> should.be_true
  is_unbound(h2, w) |> should.be_true
}

// Two writers meeting inside a structure surface the same loud WxW error via the
// pairwise-arg recursion — the error is not masked by the enclosing struct.
pub fn nested_struct_writer_writer_errors_test() {
  let #(h, w1, _r1) = fresh(heap.new())
  let #(h, w2, _r2) = fresh(h)
  unify(h, StructTerm("p", [VarRef(w1)]), StructTerm("p", [VarRef(w2)]))
  |> should.equal(Error(WriterToWriter(w1, w2)))
}

// A mismatch in an EARLIER argument short-circuits before a later writer is ever
// reached — the later writer is left unbound (no partial-unify leak).
pub fn nested_struct_earlier_fail_leaves_later_writer_unbound_test() {
  let #(h, w, _r) = fresh(heap.new())
  let a = StructTerm("p", [c("zero"), VarRef(w)])
  let b = StructTerm("p", [c("one"), c("nine")])
  unify(h, a, b) |> should.equal(Ok(Fail))
  is_unbound(h, w) |> should.be_true
}

// ── tentative-HEAD paths (three-phase runner) ────────────────────────────────

fn reduce(
  src: String,
  entry: String,
  regs: program.XRegs,
  h: heap.Heap,
) -> runner.ReduceOutcome {
  let assert Ok(outcome) = loader.load(src, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, entry)
  runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)
}

const wrap_source = "Bit ::= zero ; one.
W ::= w(Bit).
procedure wrap(Bit?, W).
wrap(X, w(X?))."

// wrap(A?, R) with A unbound: the HEAD captures A's reader into the clause writer
// X and builds `w(X?)` for the output R. The tentative-HEAD commit binds only the
// OUTPUT writer R — the INPUT reader (A) is passed on as a link, never bound. This
// pins the writer-MGU invariant on the three-phase HEAD-commit path (PROOF.md
// `head_unify_step_preserves_writer_only`).
pub fn tentative_head_commit_never_binds_input_reader_test() {
  let #(h, a_writer, a_reader) = fresh(heap.new())
  let #(h, r_writer, _r_reader) = fresh(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(a_reader))
    |> program.set_reg(1, VarRef(r_writer))
  let assert runner.Reduced(heap: h2, ..) =
    reduce(wrap_source, "wrap/2", regs, h)
  // The output writer committed to a `w(...)` structure.
  let assert Ok(#(_, Bound(StructTerm("w", _)))) = heap.deref(h2, r_writer)
  // The input reader was passed on, NOT bound — A's writer is still unbound.
  is_unbound(h2, a_writer) |> should.be_true
}

const flip_source = "Bit ::= zero ; one.
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero)."

// flip(two, R): `two` matches no clause HEAD (soft-fail on each), so the goal
// fails outright. A failed HEAD match must leave the output writer R untouched —
// no clause's tentative σ̂w may leak into the committed heap. (Adversarial: a
// naive impl that applied σ̂w before verifying the match would bind R.)
pub fn tentative_head_failed_match_leaves_output_unbound_test() {
  let #(h, r_writer, _r_reader) = fresh(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, c("two"))
    |> program.set_reg(1, VarRef(r_writer))
  let assert runner.Failed(heap: h2, ..) = reduce(flip_source, "flip/2", regs, h)
  is_unbound(h2, r_writer) |> should.be_true
}
