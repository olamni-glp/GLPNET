//// Adversarial distributed-dereference suite (feature 050 T057 / feature 059 WP
//// T083 `close-proofs-proof-dist-deref-convergence`, shared with T066
//// `close-distribution-engine-sessions`). Discharges proof obligation PI:17
//// (`specs/050-full-gleam-combined/contracts/proof-obligations.md`, gates M2)
//// alongside the Lean proof `glp_gleam/lean/DistDerefConvergence/` and the prose
//// `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/dist_deref_convergence/PROOF.md`.
////
//// Where the Lean model abstracts the distributed-unification surface, this suite
//// attacks the SAME two properties on the REAL Gleam primitives that exist today —
//// exactly as the PI:14 precedent (`writer_mgu_adversarial_test.gleam`) proves the
//// method on the real `unify`/`heap`, not a re-modelled surface:
////
////   THEOREM 1 (termination). A cross-instance deref chain either reaches a GENUINE
////     terminal (acyclic — bounded by the reader→writer→(value|unbound) walk) or halts
////     LOUDLY with NO binding (a cross-seam cycle → `Error(Cycle)`; a writer meeting a
////     writer → `Error(WriterToWriter)`). It never silently loops or fabricates a
////     binding. Mirrors Lean `derefDist_terminates` / `cyc_stuck`.
////
////   THEOREM 2 (convergence). Two references terminating at the SAME owner cell read
////     the ONE owner cell, so they AGREE — before delivery (both `Unbound` on the
////     owner) and after delivery (both the delivered value). Delivery is owner-only and
////     single-assignment: a second delivery is refused LOUDLY (no lost/duplicated
////     binding). Mirrors Lean `dist_deref_converges` /
////     `dist_deref_agrees_pre_quiescence` / `derefDist_eq_of_terminal`, and — on two
////     real `MadEngine`s — the `deliver`/`deliver_binds_owner` owner-only-bind pipeline.
////
//// Plus the surrounding real machinery the theorems assume: the `known/1`
//// globalize/localize boundary (`_w(p,i)`/`_r(p,i)` + the W_p table), the GAP-G6
//// quiescence oracle (`quiescence.decide`, the `drainN_quiescent` predicate), and
//// fault-mid-deref (a T075 link fault surfaces through `deref` as ORDINARY DATA — a
//// bound ground `permFail` term — never a fourth verdict, never a silent success).
////
//// SCOPE HONESTY (per `../INDEX.md`): the single-callable cross-instance deref
//// (instance→link→instance in one hop) is the engine-to-engine routing of
//// `close-distribution-engine-sessions` step 3; this suite exercises the delivered
//// component primitives that compose into it — the same discipline the Lean proof
//// records in PROOF.md §Scope.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/list
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/runner
import glp/engine/scheduler.{StepErrored, StepIdle}
import glp/link/primitives/link_terms
import glp/link/primitives/quiescence.{Active, Quiescent}
import glp/link/seam/link_address
import glp/link/seam/link_id.{LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/mad/global_name.{ReaderName, WriterName, of_term, to_term}
import glp/mad/global_writers_table.{GlobalizeEntry, LocalizeEntry} as wt
import glp/mad/mad_engine.{type MadEngine}
import glp/mad/message.{type Message, Message}
import glp/runtime/heap.{
  AlreadyBound, Bound, Cycle, Unbound, WriterToWriter,
}
import glp/runtime/terms.{type Term, ConstAtom, ConstTerm, StructTerm, VarRef, cons}

// A fresh unbound variable: #(heap', writer_addr, reader_addr).
fn fresh(h: heap.Heap) -> #(heap.Heap, Int, Int) {
  heap.allocate_variable(h)
}

fn atom(name: String) -> Term {
  ConstTerm(ConstAtom(name))
}

// bind_writer that keeps only the heap (drops the activation list).
fn bind_val(h: heap.Heap, w: Int, v: Term) -> heap.Heap {
  let assert Ok(#(h, _)) = heap.bind_writer(h, w, v)
  h
}

// bind_writer_to_var (a writer→another var's reader link) keeping only the heap.
fn link_to(h: heap.Heap, w: Int, r: Int) -> heap.Heap {
  let assert Ok(#(h, _)) = heap.bind_writer_to_var(h, w, r)
  h
}

// ══ THEOREM 1 — termination: acyclic reaches a genuine terminal ═══════════════

// An acyclic multi-hop chain r1 → w1 → (link) r2 → w2 → value resolves to the
// value terminal — no cycle, no stuck. This is `derefDist_terminates`: a chain
// with a strictly-decreasing rank reaches a definite terminal.
pub fn acyclic_chain_reaches_value_terminal_test() {
  let #(h, w1, r1) = fresh(heap.new())
  let #(h, w2, r2) = fresh(h)
  let h = link_to(h, w1, r2)
  // w1 → r2's chain
  let h = bind_val(h, w2, atom("settled"))
  // w2 := settled
  let assert Ok(#(_, Bound(t))) = heap.deref(h, r1)
  t |> should.equal(atom("settled"))
}

// A one-hop link to an as-yet-unbound owner resolves to the OWNER terminal as
// `Unbound` — a genuine terminal, not stuck.
pub fn acyclic_chain_to_unbound_owner_is_unbound_terminal_test() {
  let #(h, w0, r0) = fresh(heap.new())
  // owner
  let #(h, w1, r1) = fresh(h)
  // handle
  let h = link_to(h, w1, r0)
  let assert Ok(#(_, Unbound(terminal))) = heap.deref(h, r1)
  terminal |> should.equal(w0)
}

// ── FORK-1 dual: a cross-instance cycle halts LOUDLY with NO binding ──────────

// The canonical FORK-1 shape: two writers cross-linked (w1 → r2, w2 → r1), the
// operational counterpart of A's globalized ref → B and B's → A with no
// occurs-check. `deref` trips its visited-set guard and returns `Error(Cycle)` —
// the loud verdict — and NO value was ever bound. Mirrors Lean `cyc_stuck`.
pub fn fork1_cross_link_cycle_is_loud_and_binds_nothing_test() {
  let #(h, w1, r1) = fresh(heap.new())
  let #(h, w2, r2) = fresh(h)
  let h = link_to(h, w1, r2)
  let h = link_to(h, w2, r1)
  // Loud cycle verdict from BOTH entry points — never a silent loop, never a value.
  heap.deref(h, r1) |> should.equal(Error(Cycle(r1)))
  heap.deref(h, r2) |> should.equal(Error(Cycle(r2)))
  // No binding leaked: neither writer is a value cell (both still link cells).
  heap.is_value(h, w1) |> should.be_false
  heap.is_value(h, w2) |> should.be_false
}

// A three-hop cross-seam cycle (w1→r2, w2→r3, w3→r1) is equally loud — the guard
// bounds an arbitrary-length cross-instance cycle, not just the 2-cycle.
pub fn fork1_three_hop_cycle_is_loud_test() {
  let #(h, w1, r1) = fresh(heap.new())
  let #(h, w2, r2) = fresh(h)
  let #(h, w3, r3) = fresh(h)
  let h = link_to(h, w1, r2)
  let h = link_to(h, w2, r3)
  let h = link_to(h, w3, r1)
  let assert Error(Cycle(_)) = heap.deref(h, r1)
}

// A writer meeting a writer on the deref path is surfaced LOUDLY as
// `WriterToWriter`, never collapsed to a silent answer (the no-writer↔writer
// binding half of the termination story).
pub fn writer_meets_writer_is_loud_no_binding_test() {
  let #(h, w1, _r1) = fresh(heap.new())
  let #(h, w2, _r2) = fresh(h)
  // Try to link w2 directly onto another WRITER w1 (not a reader) → loud WxW.
  heap.bind_writer_to_var(h, w2, w1)
  |> should.equal(Error(WriterToWriter(w2, w1)))
  heap.is_value(h, w2) |> should.be_false
}

// ══ THEOREM 2 — convergence: shared owner ⇒ agreement, before & after ═════════

// Two handles h1, h2 both linking to the SAME owner r0. BEFORE the owner is
// bound, both deref to the same `Unbound(owner)` — they AGREE (agreement is
// invariant of delivery). AFTER the owner is bound, both deref to the same value.
// Mirrors `dist_deref_agrees_pre_quiescence` + `dist_deref_converges` +
// `derefDist_eq_of_terminal` — two nodes resolving to one terminal read one cell.
pub fn two_handles_converge_on_shared_owner_test() {
  let #(h, w0, r0) = fresh(heap.new())
  // the owner writer
  let #(h, _w1, r1) = fresh(h)
  let #(h, _w2, r2) = fresh(h)
  let h = link_to(h, paired_writer_of(h, r1), r0)
  let h = link_to(h, paired_writer_of(h, r2), r0)

  // BEFORE delivery: both handles agree on the unbound owner.
  let a0 = deref_result(h, r1)
  let b0 = deref_result(h, r2)
  a0 |> should.equal(b0)
  a0 |> should.equal(Ok(Unbound(w0)))

  // Owner-only delivery settles the one owner cell.
  let h = bind_val(h, w0, atom("v"))

  // AFTER delivery: both handles agree, and on the delivered value.
  let a1 = deref_result(h, r1)
  let b1 = deref_result(h, r2)
  a1 |> should.equal(b1)
  a1 |> should.equal(Ok(Bound(atom("v"))))
}

// Interleaved bind/deref race + single-assignment safety. Once the owner is
// bound, re-dereferencing is STABLE (fuel monotonicity `resolveNode_mono`), and a
// SECOND delivery to the same owner is refused LOUDLY as `AlreadyBound` — no
// lost/duplicated binding, the safety property the SPIN obligation named.
pub fn delivery_is_monotone_and_no_double_bind_test() {
  let #(h, w0, r0) = fresh(heap.new())
  let #(h, _w1, r1) = fresh(h)
  let h = link_to(h, paired_writer_of(h, r1), r0)

  // Race: a deref BEFORE delivery sees Unbound.
  deref_result(h, r1) |> should.equal(Ok(Unbound(w0)))

  let h = bind_val(h, w0, atom("v"))

  // Two derefs after delivery are identical (stable, no drift).
  let after1 = deref_result(h, r1)
  let after2 = deref_result(h, r1)
  after1 |> should.equal(after2)
  after1 |> should.equal(Ok(Bound(atom("v"))))

  // A second owner-only delivery is refused loudly — never a silent re-bind.
  heap.bind_writer(h, w0, atom("other"))
  |> should.equal(Error(AlreadyBound(w0)))
}

// ══ Distributed pipeline — two REAL MadEngines, owner-only bind across the seam ═

// The genuine cross-instance deferred-local-assignment convergence: p exports a
// reader Xs? to q; p assigns its writer Xs := [add]; the value flows across the
// seam and q's reader dereferences to the delivered value. This drives the real
// `mad_engine.receive` → `scheduler.bind_and_wake` owner-only bind (the code
// counterpart of Lean `deliver`/`deliver_binds_owner`). The adversarial addition:
// a SECOND delivery of the same assignment is refused LOUDLY (no duplicated bind).
pub fn two_engine_deferred_assignment_converges_test() {
  let prog = boot_program(client_clause)
  let gs = kappa(prog, "global_send/3")
  let client_pc = kappa(prog, "client/1")

  let p = mad_engine.new(prog, atom("p"))
  let q = mad_engine.new(prog, atom("q"))

  // Cold-call: p exports reader Xs? to q's serializer; p keeps writer Xs.
  let #(p, xs_writer, xs_reader) = mad_engine.alloc_local(p)
  let regs =
    program.new_regs()
    |> program.set_reg(0, StructTerm("req", [VarRef(xs_reader)]))
    |> program.set_reg(1, to_term(WriterName(atom("q"), 0)))
    |> program.set_reg(2, atom("q"))
  let #(p, _id) = mad_engine.boot(p, "global_send/3", gs, regs)
  let #(p, cold_call) = run_to_quiescence(p)
  let q = deliver_all(q, cold_call)

  // q localized _r(p,1) → entry (Xs_q, p, 1); q holds reader Xs_q?.
  let assert Ok(LocalizeEntry(xs_q_writer, _, _)) =
    wt.find_localize(mad_engine.writers_table(q), atom("p"), 1)
  let xs_q_reader =
    heap.paired_reader(scheduler.heap(mad_engine.engine(q)), xs_q_writer)

  // BEFORE delivery (assignment still in flight): q's reader is Unbound — the
  // pre-quiescence agreement side (no instance observes a binding yet).
  let hq0 = scheduler.heap(mad_engine.engine(q))
  let assert Ok(#(_, Unbound(_))) = heap.deref(hq0, xs_q_reader)

  // p's client assigns Xs := [add]; the watching global_send forwards _r(p,1) := [add].
  let regs2 = program.new_regs() |> program.set_reg(0, VarRef(xs_writer))
  let #(p, _id2) = mad_engine.boot(p, "client/1", client_pc, regs2)
  let #(_p, forwarded) = run_to_quiescence(p)

  // q receives the deferred assignment → owner-only bind of Xs_q := [add].
  let q = deliver_all(q, forwarded)

  // AFTER delivery: q's reader converges to the delivered value.
  let hq1 = scheduler.heap(mad_engine.engine(q))
  let assert Ok(#(_, Bound(cell))) = heap.deref(hq1, xs_q_reader)
  cell |> should.equal(cons(atom("add"), terms.nil()))

  // Adversarial: a SECOND delivery of the same assignment is refused loudly —
  // the owner writer is already bound; no lost/duplicated binding.
  let assert [Message(name, term, _dest), ..] = forwarded
  mad_engine.receive(q, name, term)
  |> should.be_error
}

// ══ known/1 boundary — globalize / localize round-trip (RemoteVarRef surface) ══

// A globalized reference `_w(p,i)` / `_r(p,i)` round-trips through the term form
// the seam carries (globalize on export, localize on import — the `known/1`
// boundary). Mirrors Lean `globalize`/`localize`.
pub fn known1_global_name_term_round_trips_test() {
  of_term(to_term(WriterName(atom("p"), 3)))
  |> should.equal(Ok(WriterName(atom("p"), 3)))
  of_term(to_term(ReaderName(atom("q"), 7)))
  |> should.equal(Ok(ReaderName(atom("q"), 7)))
}

// The W_p table is the RemoteVarRef store: a globalized writer becomes a
// GlobalizeEntry retrievable by its minted index; a localized remote reader
// becomes a LocalizeEntry retrievable by (remote agent, remote index). A DUPLICATE
// localize of the same (agent, index) is refused LOUDLY — the owner-only,
// no-duplicate-entry discipline.
pub fn wp_table_globalize_localize_entries_test() {
  let t = wt.new(atom("p"))
  // globalize a local writer @addr 42 destined for agent q → index ≥ 1.
  let #(t, idx) = wt.add_globalize(t, 42, atom("q"))
  { idx >= 1 } |> should.be_true
  wt.lookup(t, idx) |> should.equal(Ok(GlobalizeEntry(42, atom("q"))))

  // localize a remote reader _r(p,1) into a fresh local writer @addr 99.
  let assert Ok(t) = wt.add_localize(t, 99, atom("p"), 1)
  wt.find_localize(t, atom("p"), 1)
  |> should.equal(Ok(LocalizeEntry(99, atom("p"), 1)))
  // A duplicate (agent, index) is refused.
  wt.add_localize(t, 7, atom("p"), 1) |> should.be_error
}

// ══ GAP-G6 quiescence oracle — the drainN_quiescent predicate ═════════════════

// The pure oracle: network-quiescent iff ALL three counts are zero; otherwise
// Active names the blocking cause. This is the `Quiescent` predicate the
// convergence theorem's "once messages quiesce" hypothesis is discharged against.
pub fn quiescence_oracle_all_zero_is_quiescent_test() {
  quiescence.decide(0, 0, 0) |> should.equal(Quiescent)
  quiescence.is_quiescent(0, 0, 0) |> should.be_true
}

// Each non-zero count keeps the run Active and names WHICH cause blocks quiescence
// — the diagnostic the adversarial suite / a distributed-run detector reports.
pub fn quiescence_oracle_names_the_active_cause_test() {
  quiescence.decide(2, 0, 0) |> should.equal(Active(2, 0, 0))
  quiescence.decide(0, 3, 0) |> should.equal(Active(0, 3, 0))
  quiescence.decide(0, 0, 5) |> should.equal(Active(0, 0, 5))
  quiescence.is_quiescent(0, 1, 0) |> should.be_false
}

// The `drainN_quiescent` reachability side: a run with a finite in-flight backlog
// of N frames is NOT quiescent while N > 0, and IS quiescent once drained to 0.
pub fn quiescence_reached_by_draining_backlog_test() {
  // A backlog of 3 in-flight frames: not quiescent.
  quiescence.is_quiescent(0, 3, 0) |> should.be_false
  // ... drained to 0: quiescent (the reachability the Lean `drainN_quiescent` proves).
  quiescence.is_quiescent(0, 0, 0) |> should.be_true
}

// ══ fault-mid-deref (T075) — a link fault surfaces through deref as DATA ═══════

// A T075 link fault is an ORDINARY BOUND GROUND TERM, not a fourth deref verdict.
// Even racing a deref, a writer settled to a `permFail(LinkId, Reason)` derefs to
// that term as data — surfaced LOUDLY, never a silent success and never a hang.
pub fn fault_surfaces_through_deref_as_data_test() {
  let id = LinkId(link_scheme.loopback(), link_address.endpoint("h", 9000), NonceInt(1))
  let fault = link_terms.perm_fail(id, "peer went silent")
  let #(h, w, r) = fresh(heap.new())
  let h = bind_val(h, w, fault)
  let assert Ok(#(_, Bound(StructTerm(functor, _)))) = heap.deref(h, r)
  // The fault is data on the monitor lattice — a `permFail` struct, read with
  // ordinary guards, not a special deref outcome.
  functor |> should.equal("permFail")
  // And it is exactly the fault term (no corruption mid-deref).
  let assert Ok(#(_, Bound(t))) = heap.deref(h, r)
  t |> should.equal(fault)
}

// ── helpers ──────────────────────────────────────────────────────────────────

// The paired writer of a reader (readers are writer+1; use the heap accessor).
fn paired_writer_of(h: heap.Heap, reader: Int) -> Int {
  let assert Ok(w) = heap.paired_writer(h, reader)
  w
}

// deref keeping only the DerefResult (drop the compressed heap) for equality.
fn deref_result(h: heap.Heap, addr: Int) -> Result(heap.DerefResult, heap.HeapError) {
  case heap.deref(h, addr) {
    Ok(#(_, r)) -> Ok(r)
    Error(e) -> Error(e)
  }
}

// ── two-engine harness (mirrors mad_multiagent_test.gleam) ────────────────────

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> String {
  let assert Ok(bytes) = read_file(path)
  let assert Ok(text) = bit_array.to_string(bytes)
  text
}

fn boot_program(extra: String) -> program.BytecodeProgram {
  let self_source = read_source("../programs/self.glp")
  let mad_source = read_source("../programs/system/mad_predicates.glp")
  let assert Ok(outcome) = loader.load(mad_source <> "\n" <> extra, self_source)
  outcome.program
}

fn kappa(prog, label) -> Int {
  let assert Ok(pc) = program.label_pc(prog, label)
  pc
}

const client_clause = "procedure client(_).
client([add])."

fn run_to_quiescence(me: MadEngine) -> #(MadEngine, List(Message)) {
  drain(me, [], 10_000)
}

fn drain(me: MadEngine, acc: List(Message), fuel: Int) -> #(MadEngine, List(Message)) {
  case fuel <= 0 {
    True -> panic as "distributed scenario did not quiesce within fuel"
    False -> {
      let #(me, outcome, msgs) = mad_engine.step(me, 5000)
      let acc = list.append(acc, msgs)
      case outcome {
        StepIdle -> #(me, acc)
        StepErrored(fault) ->
          panic as {
            "agent step errored: "
            <> case fault {
              runner.Unimplemented(m) -> "unimplemented " <> m
              runner.StructuralViolation(d) -> "structural " <> d
              runner.Malformed(d) -> "malformed " <> d
            }
          }
        _ -> drain(me, acc, fuel - 1)
      }
    }
  }
}

fn deliver_all(me: MadEngine, msgs: List(Message)) -> MadEngine {
  list.fold(msgs, me, fn(me, msg) {
    let Message(name, term, _dest) = msg
    let assert Ok(me) = mad_engine.receive(me, name, term)
    me
  })
}
