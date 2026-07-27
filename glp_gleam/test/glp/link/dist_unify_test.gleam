//// T051 — distributed unification over a REAL link (spec acceptance scenario 2):
//// "Given a distributed unification touching a remote variable, When it resolves,
//// Then assignment is deferred to the owning side and both sides converge on the
//// same binding."
////
//// The §10.1 client-monitor flow, run as TWO INSTANCES over a genuine loopback
//// LINK — cold-call and value assignment cross as `'_assign'/2` FRAMES through the
//// full stack (encode → term codec → frame codec → endpoint → pump → decode →
//// Receive), not as in-BEAM routed values:
////   * instance p (connector, child process): holds writer X, exports reader X? to
////     q, later binds X := [add] LOCALLY by its own reduction — the owning side;
////   * instance q (listener, test process): localizes `_r(p,1)`, holds reader-side
////     cell X_q; the arriving assignment binds X_q by the UNCHANGED §8.3 Receive —
////     deferred to the side that owns THAT cell, never bound remotely.
//// Convergence assertion: p reports its local X and q derefs its local X_q — both
//// must be `[add]`. Plus the codec-level unit pins (round-trip, dest dropped,
//// non-assignment pass-through, misroute surfaced).

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/erlang/process
import gleam/list
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/runner
import glp/engine/scheduler.{StepErrored, StepIdle}
import glp/link/dist_unify
import glp/link/primitives/link_kernels
import glp/link/primitives/link_pump
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/seam/link_address
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/link/transports/loopback
import glp/mad/global_name.{ReaderName, WriterName, to_term}
import glp/mad/global_writers_table.{LocalizeEntry} as wt
import glp/mad/mad_engine.{type MadEngine}
import glp/mad/message.{type Message, Message}
import glp/runtime/heap.{Bound}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef,
  cons,
}

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> String {
  let assert Ok(bytes) = read_file(path)
  let assert Ok(text) = bit_array.to_string(bytes)
  text
}

fn atom(a: String) -> Term {
  ConstTerm(ConstAtom(a))
}

const client_clause = "procedure client(_).
client([add])."

fn boot_program() -> program.BytecodeProgram {
  let self_source = read_source("../programs/self.glp")
  let mad_source = read_source("../programs/system/mad_predicates.glp")
  let assert Ok(outcome) =
    loader.load(mad_source <> "\n" <> client_clause, self_source)
  outcome.program
}

fn kappa(prog, label) -> Int {
  let assert Ok(pc) = program.label_pc(prog, label)
  pc
}

// ── codec-level pins ─────────────────────────────────────────────────────────

pub fn assignment_round_trips_and_drops_dest_test() {
  let msg =
    Message(
      WriterName(atom("q"), 0),
      cons(StructTerm("req", [to_term(ReaderName(atom("p"), 1))]), terms.nil()),
      atom("q"),
    )
  let wire = dist_unify.encode_assignment(msg)
  // The wire term is `'_assign'(Name, Value)` — two args, no dest (FR-005).
  let assert StructTerm("_assign", [_, _]) = wire
  let assert Ok(#(name, value)) = dist_unify.decode_assignment(wire)
  name |> should.equal(WriterName(atom("q"), 0))
  let Message(_, original_value, _) = msg
  value |> should.equal(original_value)
}

pub fn non_assignment_terms_pass_through_test() {
  dist_unify.decode_assignment(atom("ordinary"))
  |> should.equal(Error(Nil))
  // A malformed `_assign` (bad name) is NOT an assignment either.
  dist_unify.decode_assignment(StructTerm("_assign", [atom("junk"), atom("v")]))
  |> should.equal(Error(Nil))
}

// ── the acceptance scenario: two instances over a real loopback link ─────────

fn link_id(channel: String) -> LinkId {
  LinkId(
    scheme: link_scheme.loopback(),
    endpoint: link_address.path(channel),
    nonce: NonceInt(1),
  )
}

fn link_id_term(channel: String) -> Term {
  StructTerm("link_id", [
    ConstTerm(ConstAtom("loopback")),
    ConstTerm(ConstString(channel)),
    ConstTerm(ConstInt(1)),
  ])
}

/// Establish this side's end of the link through K1 and return the state.
fn establish(
  t,
  channel: String,
  role: String,
) -> #(link_runtime.LinkState, LinkId) {
  let state = link_runtime.new() |> link_runtime.with_transport(t)
  let #(h, in_w, _) = heap.allocate_variable(heap.new())
  let #(h, _, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, _) = heap.allocate_variable(h)
  let assert Ok(link_kernels.LinkEffect(_h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_setup", 5, [
      link_id_term(channel),
      ConstTerm(ConstAtom(role)),
      VarRef(in_w),
      VarRef(out_r),
      VarRef(faults_w),
    ])
  #(state, link_id(channel))
}

/// Step `me` to quiescence, accumulating drained outgoing messages (A4b's shape).
fn run_to_quiescence(me: MadEngine, acc: List(Message), fuel: Int) -> #(MadEngine, List(Message)) {
  case fuel <= 0 {
    True -> panic as "instance did not quiesce"
    False -> {
      let #(me, outcome, msgs) = mad_engine.step(me, 5000)
      let acc = list.append(acc, msgs)
      case outcome {
        StepIdle -> #(me, acc)
        StepErrored(fault) ->
          panic as { "step errored: " <> describe(fault) }
        _ -> run_to_quiescence(me, acc, fuel - 1)
      }
    }
  }
}

fn describe(fault: runner.RunnerFault) -> String {
  case fault {
    runner.Unimplemented(m) -> "unimplemented " <> m
    runner.StructuralViolation(d) -> "structural " <> d
    runner.Malformed(d) -> "malformed " <> d
  }
}

/// Ship every drained message as an `'_assign'` frame over this side's handle,
/// threading the advancing handle through the registry contract.
fn ship_all(
  state: link_runtime.LinkState,
  id: LinkId,
  me: MadEngine,
  msgs: List(Message),
) -> link_runtime.LinkState {
  case msgs {
    [] -> state
    [msg, ..rest] -> {
      let assert Ok(handle) = link_registry.try_get(state.links, id)
      let assert Ok(#(_h, advanced)) =
        dist_unify.ship_assignment(scheduler.heap(mad_engine.engine(me)), handle, msg)
      let state =
        link_runtime.with_links(state, link_registry.put(state.links, advanced))
      ship_all(state, id, me, rest)
    }
  }
}

pub fn distributed_assignment_converges_on_both_sides_test() {
  let prog = boot_program()
  let gs = kappa(prog, "global_send/3")
  let client_pc = kappa(prog, "client/1")
  let t = loopback.new()
  let channel = "chan-t051"
  let back = process.new_subject()

  // ── instance p (child process): the OWNING side ────────────────────────────
  process.spawn_unlinked(fn() {
    let #(state, id) = establish(t, channel, "connector")

    let p = mad_engine.new(prog, atom("p"))
    let #(p, xs_writer, xs_reader) = mad_engine.alloc_local(p)
    // Export reader X? to q (cold-call to q's serializer).
    let regs =
      program.new_regs()
      |> program.set_reg(0, StructTerm("req", [VarRef(xs_reader)]))
      |> program.set_reg(1, to_term(WriterName(atom("q"), 0)))
      |> program.set_reg(2, atom("q"))
    let #(p, _) = mad_engine.boot(p, "global_send/3", gs, regs)
    let #(p, cold_call) = run_to_quiescence(p, [], 10_000)
    let state = ship_all(state, id, p, cold_call)

    // Bind X := [add] LOCALLY (the owning side's own reduction) — the watching
    // global_send fires and the assignment ships to q.
    let regs2 = program.new_regs() |> program.set_reg(0, VarRef(xs_writer))
    let #(p, _) = mad_engine.boot(p, "client/1", client_pc, regs2)
    let #(p, forwarded) = run_to_quiescence(p, [], 10_000)
    let _state = ship_all(state, id, p, forwarded)

    // Report p's LOCAL binding of X (deref the reader half).
    let hp = scheduler.heap(mad_engine.engine(p))
    let x_local = case heap.deref(hp, xs_reader) {
      Ok(#(_, Bound(v))) -> Ok(v)
      _ -> Error(Nil)
    }
    process.send(back, x_local)
  })

  // ── instance q (test process): the reader-holding side ────────────────────
  let #(state_q, _idq) = establish(t, channel, "listener")
  let q = mad_engine.new(prog, atom("q"))

  // Phase 1 — the cold-call arrives: q localizes `_r(p,1)`. The entry must be read
  // NOW: the second assignment's Receive will CONSUME it (§8.3 removes on bind).
  let #(q, xq_reader) = await_entry(state_q, q, 200)
  // Phase 2 — the value assignment arrives and binds q's LOCAL cell.
  let q = await_bound(state_q, q, xq_reader, 200)
  let hq = scheduler.heap(mad_engine.engine(q))
  let assert Ok(#(_, Bound(q_value))) = heap.deref(hq, xq_reader)
  q_value |> should.equal(cons(atom("add"), terms.nil()))
  // The entry was consumed by the bind — the §8.3 lifecycle, observed on-wire.
  wt.localize_count(mad_engine.writers_table(q)) |> should.equal(0)

  // p's LOCAL binding is the SAME value — both sides converged (scenario 2), each
  // having bound only its own cell (deferred-local-assignment).
  let assert Ok(Ok(p_value)) = process.receive(back, 10_000)
  p_value |> should.equal(q_value)
}

/// Drain + apply until the (p,1) localize entry EXISTS, returning its reader — must
/// run before the value assignment consumes the entry (§8.3 removes on bind).
fn await_entry(
  state: link_runtime.LinkState,
  q: MadEngine,
  budget: Int,
) -> #(MadEngine, Int) {
  case budget <= 0 {
    True -> panic as "q never localized the cold-call"
    False -> {
      let q = apply_items(q, link_pump.drain_wait(state.inbox, 100))
      case wt.find_localize(mad_engine.writers_table(q), atom("p"), 1) {
        Ok(LocalizeEntry(w, _, _)) -> #(
          q,
          heap.paired_reader(scheduler.heap(mad_engine.engine(q)), w),
        )
        _ -> await_entry(state, q, budget - 1)
      }
    }
  }
}

/// Drain + apply until `reader` is bound — the convergence condition.
fn await_bound(
  state: link_runtime.LinkState,
  q: MadEngine,
  reader: Int,
  budget: Int,
) -> MadEngine {
  case budget <= 0 {
    True -> panic as "q never converged"
    False -> {
      let q = apply_items(q, link_pump.drain_wait(state.inbox, 100))
      case heap.deref(scheduler.heap(mad_engine.engine(q)), reader) {
        Ok(#(_, Bound(_))) -> q
        _ -> await_bound(state, q, reader, budget - 1)
      }
    }
  }
}

fn apply_items(q: MadEngine, items: List(link_pump.InboundItem)) -> MadEngine {
  case items {
    [] -> q
    [link_pump.Data(_, _, term), ..rest] -> {
      let assert Ok(dist_unify.Applied(q)) = dist_unify.route_inbound(q, term)
      apply_items(q, rest)
    }
    [_, ..rest] -> apply_items(q, rest)
  }
}
