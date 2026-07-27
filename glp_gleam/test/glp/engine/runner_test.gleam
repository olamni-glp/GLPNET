//// Runner reduction tests (feature 050, T021 slice 21a/b).
////
//// End-to-end: load a real program through the T020 pipeline, then drive one
//// goal reduction through the three-phase runner and assert the observable
//// outcome (writer bound / goal suspended / definitive failure). This is the
//// engine-value unit seam the proof-obligations contract names.

import gleam/list
import gleam/set
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/runner
import glp/engine/scheduler
import glp/runtime/heap
import glp/runtime/terms.{
  type Term, ConstAtom, ConstTerm, StructTerm, VarRef, cons, nil,
}

const flip_source = "Bit ::= zero ; one.
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero)."

fn load_flip() -> program.BytecodeProgram {
  let assert Ok(outcome) = loader.load(flip_source, "")
  outcome.program
}

// flip(zero, X) → clause 1 commits, X is bound to `one`.
pub fn flip_first_clause_binds_writer_test() {
  let prog = load_flip()
  let assert Ok(kappa) = program.label_pc(prog, "flip/2")
  // Fresh query variable X.
  let #(h, x_writer, _x_reader) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("zero")))
    |> program.set_reg(1, VarRef(x_writer))
  let outcome = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, ..) = outcome
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("one"))))) =
    heap.deref(h2, x_writer)
}

// flip(one, X) → clause 1 head-mismatches and soft-fails; clause 2 commits,
// X is bound to `zero`. Exercises soft-fail + forward clause scan.
pub fn flip_second_clause_binds_writer_test() {
  let prog = load_flip()
  let assert Ok(kappa) = program.label_pc(prog, "flip/2")
  let #(h, x_writer, _x_reader) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("one")))
    |> program.set_reg(1, VarRef(x_writer))
  let outcome = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, ..) = outcome
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("zero"))))) =
    heap.deref(h2, x_writer)
}

// flip(A?, Y): the first argument is an unbound READER — no clause's HEAD can be
// determined, so the goal suspends on the reader's paired writer.
pub fn flip_suspends_on_unbound_reader_test() {
  let prog = load_flip()
  let assert Ok(kappa) = program.label_pc(prog, "flip/2")
  // A: an unbound variable; pass its READER half as arg 0.
  let #(h, a_writer, a_reader) = heap.allocate_variable(heap.new())
  // Y: a fresh writer for the output slot.
  let #(h, y_writer, _y_reader) = heap.allocate_variable(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(a_reader))
    |> program.set_reg(1, VarRef(y_writer))
  let outcome = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  // Suspends on A's writer (deref of A's reader terminates there).
  let assert runner.Suspended(heap: _, on: on, ..) = outcome
  should.be_true(set.contains(on, a_writer))
}

// ── Slice 21c: HEAD structures (writer-MGU crux) ─────────────────────────────

/// Fully resolve a term for assertions: follow VarRefs through the heap to their
/// ground value (or leave an unbound ref), recursing into structure args.
fn resolve(h: heap.Heap, term: Term) -> Term {
  case term {
    VarRef(addr) ->
      case heap.deref(h, addr) {
        Ok(#(_, heap.Bound(v))) -> resolve(h, v)
        _ -> term
      }
    StructTerm(f, args) ->
      StructTerm(f, list_map(args, fn(a) { resolve(h, a) }))
    _ -> term
  }
}

fn list_map(xs: List(a), f: fn(a) -> b) -> List(b) {
  case xs {
    [] -> []
    [x, ..rest] -> [f(x), ..list_map(rest, f)]
  }
}

const swap_source = "Bit ::= zero ; one.
Pair ::= p(Bit, Bit).
procedure swap(Pair?, Pair).
swap(p(X, Y), p(Y?, X?))."

// swap(p(zero, one), R) → R = p(one, zero). Exercises HEAD-structure READ
// (matching p(zero,one)), WRITE (building the output p in σ̂w as a tentative
// struct), UnifyVariable in both modes, and the writer-MGU commit conversion.
pub fn swap_builds_output_structure_test() {
  let assert Ok(outcome) = loader.load(swap_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "swap/2")
  let #(h, r_writer, _r_reader) = heap.allocate_variable(heap.new())
  let input =
    StructTerm("p", [
      ConstTerm(ConstAtom("zero")),
      ConstTerm(ConstAtom("one")),
    ])
  let regs =
    program.new_regs()
    |> program.set_reg(0, input)
    |> program.set_reg(1, VarRef(r_writer))
  let out = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, ..) = out
  let assert Ok(#(_, heap.Bound(result))) = heap.deref(h2, r_writer)
  resolve(h2, result)
  |> should.equal(
    StructTerm("p", [
      ConstTerm(ConstAtom("one")),
      ConstTerm(ConstAtom("zero")),
    ]),
  )
}

const first_bit_source = "Bit ::= zero ; one.
Bits ::= [] ; [Bit | Bits].
Maybe ::= some(Bit) ; none.
procedure first_bit(Bits?, Maybe).
first_bit([H|_], some(H?)).
first_bit([], none)."

// first_bit([one, zero], R) → R = some(one). Exercises HEAD list matching
// (HeadStructure './2' READ), UnifyVariable READ first-occurrence, UnifyVoid
// (the anonymous tail), a subsequent GetValue reader occurrence, and building
// the output `some(H?)` structure in WRITE mode.
pub fn first_bit_reads_list_head_test() {
  let assert Ok(outcome) = loader.load(first_bit_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "first_bit/2")
  let #(h, r_writer, _r_reader) = heap.allocate_variable(heap.new())
  let input =
    cons(ConstTerm(ConstAtom("one")), cons(ConstTerm(ConstAtom("zero")), nil()))
  let regs =
    program.new_regs()
    |> program.set_reg(0, input)
    |> program.set_reg(1, VarRef(r_writer))
  let out = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, ..) = out
  let assert Ok(#(_, heap.Bound(result))) = heap.deref(h2, r_writer)
  resolve(h2, result)
  |> should.equal(StructTerm("some", [ConstTerm(ConstAtom("one"))]))
}

// first_bit([], R) → R = none. Exercises HEAD nil matching + the second clause
// (soft-fail off clause 1's list match, then a constant-output HEAD bind).
pub fn first_bit_empty_list_test() {
  let assert Ok(outcome) = loader.load(first_bit_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "first_bit/2")
  let #(h, r_writer, _r_reader) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, nil())
    |> program.set_reg(1, VarRef(r_writer))
  let out = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, ..) = out
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("none"))))) =
    heap.deref(h2, r_writer)
}

// first_bit(Xs?, B): arg 0 is an unbound READER — the HEAD list match cannot be
// determined, so the goal suspends (structure match defers via Si → U).
pub fn first_bit_suspends_on_unbound_list_test() {
  let assert Ok(outcome) = loader.load(first_bit_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "first_bit/2")
  let #(h, xs_writer, xs_reader) = heap.allocate_variable(heap.new())
  let #(h, b_writer, _b_reader) = heap.allocate_variable(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(xs_reader))
    |> program.set_reg(1, VarRef(b_writer))
  let out = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Suspended(heap: _, on: on, ..) = out
  should.be_true(set.contains(on, xs_writer))
}

// ── Slice 21d: BODY construction + Spawn ─────────────────────────────────────

const spawn_source = "Bit ::= zero ; one.
G ::= g(Bit).
procedure start(Bit?).
start(X) :- sink(g(X?)).
procedure sink(G?).
sink(g(_))."

// start(zero) commits, then its body builds `g(X?)` on the heap (PutStructure +
// a body element + structure completion) and spawns sink/1 with that structure
// as argument 0. Exercises the whole BODY-construction + Spawn path; the spawn
// request's built register resolves to g(zero).
pub fn body_spawns_goal_with_built_structure_test() {
  let assert Ok(outcome) = loader.load(spawn_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "start/1")
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("zero")))
  let out =
    runner.reduce(prog, runner.new_context(heap.new(), regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, woken: _, spawned: [req], ..) = out
  req.procedure
  |> should.equal("sink/1")
  let assert Ok(arg0) = program.get_reg(req.regs, 0)
  resolve(h2, arg0)
  |> should.equal(StructTerm("g", [ConstTerm(ConstAtom("zero"))]))
}

// ═══════════════════════════════════════════════════════════════════════════
// T025 — engine semantics: three-phase (HEAD→GUARD→BODY) ordering,
// suspend/reactivate-exactly-once (FR-005 dedup), otherwise-after-FAILURE-not-
// suspension. (feature 050, US1). These pin the semantic invariants the reduction
// tests above only touch incidentally.
// ═══════════════════════════════════════════════════════════════════════════

// Minimal prelude declaring the system guards these programs call (the real
// declarations live in programs/self.glp).
const t025_guard_prelude = "procedure ground(_?).
procedure =?=(_?, _?)."

fn reduce_guarded(
  src: String,
  entry: String,
  regs: program.XRegs,
  h: heap.Heap,
) -> runner.ReduceOutcome {
  let assert Ok(outcome) = loader.load(src, t025_guard_prelude)
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, entry)
  runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)
}

// ── Three-phase ordering: HEAD binds before GUARD reads ──────────────────────

const eqpair_source = "Bit ::= zero ; one.
Pair ::= pr(Bit, Bit).
Answer ::= match ; nomatch.
procedure eqpair(Pair?, Answer).
eqpair(pr(A, B), match) :- A? =?= B? | true.
eqpair(_, nomatch) :- otherwise | true."

// eqpair(pr(zero, zero), R): the HEAD decomposes pr(A, B), tentatively binding
// A = zero and B = zero in σ̂w; only THEN does the GUARD `A? =?= B?` read those
// captured variables and succeed → R = match. The guard can only see A and B
// because HEAD unification ran first — this pins HEAD-before-GUARD ordering.
pub fn head_binds_before_guard_reads_test() {
  let #(h, r, _) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(
      0,
      StructTerm("pr", [
        ConstTerm(ConstAtom("zero")),
        ConstTerm(ConstAtom("zero")),
      ]),
    )
    |> program.set_reg(1, VarRef(r))
  let assert runner.Reduced(heap: h2, ..) =
    reduce_guarded(eqpair_source, "eqpair/2", regs, h)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("match"))))) =
    heap.deref(h2, r)
}

// ── Three-phase ordering: GUARD passes before BODY runs ──────────────────────

const guarded_body_source = "Bit ::= zero ; one.
G ::= g(Bit).
procedure guarded(Bit?).
guarded(X) :- X? =?= zero | sink(g(X?)).
procedure sink(G?).
sink(g(_))."

// guarded(zero): guard `zero =?= zero` SUCCEEDS, so the BODY runs and spawns
// sink/1 with the built structure g(zero) — BODY executes because GUARD passed.
pub fn guard_passes_then_body_spawns_test() {
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("zero")))
  let assert runner.Reduced(spawned: [req], ..) =
    reduce_guarded(guarded_body_source, "guarded/1", regs, heap.new())
  req.procedure
  |> should.equal("sink/1")
}

// guarded(one): guard `one =?= zero` FAILS. The clause is the only one, so the
// goal fails outright — with NO body effect: the `sink` spawn never happens
// (Failed carries no `spawned`). This is GUARD-before-BODY: a failed guard means
// the body did not run.
pub fn guard_fails_then_body_does_not_run_test() {
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("one")))
  let assert runner.Failed(..) =
    reduce_guarded(guarded_body_source, "guarded/1", regs, heap.new())
}

// ── otherwise fires after FAILURE, not after SUSPENSION ──────────────────────

const chk_ground_source = "Bit ::= zero ; one.
Answer ::= grounded ; not_grounded.
procedure chk(Bit?, Answer).
chk(X, grounded) :- ground(X?) | true.
chk(_, not_grounded) :- otherwise | true."

// chk(A?, R) with A unbound: clause 1's guard `ground(A?)` SUSPENDS (A is not yet
// ground). Because clause 1 suspended rather than failed, the `otherwise` in
// clause 2 must NOT fire — the whole goal suspends on A's writer. (Contrast
// guards_test.otherwise_fires_after_failure, where clause 1 truly FAILS.)
pub fn otherwise_does_not_fire_after_suspension_test() {
  let #(h, a_writer, a_reader) = heap.allocate_variable(heap.new())
  let #(h, r, _) = heap.allocate_variable(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(a_reader))
    |> program.set_reg(1, VarRef(r))
  let assert runner.Suspended(on: on, ..) =
    reduce_guarded(chk_ground_source, "chk/2", regs, h)
  should.be_true(set.contains(on, a_writer))
}

// ── suspend / reactivate exactly once (FR-005 generation-scoped dedup) ────────

const rendezvous_source = "Bit ::= zero ; one.
procedure pc(Bit).
pc(R?) :- flip(X?, R), set_zero(X).
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero).
procedure set_zero(Bit).
set_zero(zero)."

/// Step the engine to quiescence, collecting every non-idle StepOutcome in order.
fn drain(
  engine: scheduler.Engine,
  acc: List(scheduler.StepOutcome),
) -> #(scheduler.Engine, List(scheduler.StepOutcome)) {
  case scheduler.step(engine, 1000) {
    #(engine, scheduler.StepIdle) -> #(engine, list.reverse(acc))
    #(engine, outcome) -> drain(engine, [outcome, ..acc])
  }
}

// Boot pc(Out); its body spawns flip(X?, Out) and set_zero(X). flip runs first
// and SUSPENDS on the unbound X; set_zero then binds X = zero and wakes flip,
// which reduces and binds Out. Across the whole run flip/2 suspends exactly once
// and reduces exactly once — the reactivation fires a single time. A stale-
// generation double-wake (FR-005 regression) would surface as a SECOND flip/2
// reduction; the counts below would then be 2, not 1.
pub fn suspended_goal_reactivates_exactly_once_test() {
  let assert Ok(outcome) = loader.load(rendezvous_source, "")
  let prog = outcome.program
  let assert Ok(entry) = program.label_pc(prog, "pc/1")
  let #(h, out_writer, _) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(out_writer))
  let engine = scheduler.new(prog, h)
  let #(engine, _pc_id) = scheduler.boot(engine, "pc/1", entry, regs)
  let #(engine, outcomes) = drain(engine, [])

  let flip_suspends =
    list.filter(outcomes, fn(o) {
      case o {
        scheduler.StepSuspended(_, "flip/2", _) -> True
        _ -> False
      }
    })
  let flip_reduces =
    list.filter(outcomes, fn(o) {
      case o {
        scheduler.StepReduced(_, "flip/2", _, _) -> True
        _ -> False
      }
    })
  // Exactly one suspension and exactly one (re)activation of flip.
  list.length(flip_suspends)
  |> should.equal(1)
  list.length(flip_reduces)
  |> should.equal(1)

  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("one"))))) =
    heap.deref(scheduler.heap(engine), out_writer)
}
