//// glp/mad — multi-agent madGLP parity harness + scenario §10.1 (feature 050 T050.A4b).
////
//// Drives TWO real `MadEngine`s exchanging messages over an in-test routing loop (the
//// Phase-A stand-in for the Phase-B process mesh, T050.B): each agent is stepped to
//// quiescence, its drained outgoing M_p is routed to the destination agent's
//// `receive`, and the cycle repeats. Reproduces the spec §10.1 client-monitor worked
//// scenario end-to-end and asserts the value-flow outcome the Dart oracle
//// (`mad_context.dart`, same spec) produces.
////
//// The boot program is the shipped madGLP network prelude (global_send/3 +
//// send_to_net/1 from programs/system/mad_predicates.glp, over programs/self.glp for
//// `known`/`_send`) PLUS a tiny `client/1` agent clause appended.
////
//// (§10.3 friend-intro — value flow charlie→bob→alice — additionally needs an agent
//// to assign a writer pulled from its net-input stream; deferred within T050.A4b.)

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/list
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/runner
import glp/engine/scheduler.{StepErrored, StepIdle}
import glp/mad/global_name.{ReaderName, WriterName, to_term}
import glp/mad/global_writers_table.{LocalizeEntry} as wt
import glp/mad/mad_engine.{type MadEngine}
import glp/mad/message.{type Message, Message}
import glp/runtime/heap.{Bound}
import glp/runtime/terms.{type Term, ConstAtom, ConstTerm, StructTerm, VarRef, cons}

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

// The boot program: the shipped network prelude + `extra` scenario clauses, over the
// root prelude.
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

// Step `me` to quiescence, accumulating every drained outgoing message (bounded by a
// generous fuel to catch a non-terminating scenario as a test failure, never a hang).
fn run_to_quiescence(me: MadEngine) -> #(MadEngine, List(Message)) {
  drain(me, [], 10_000)
}

fn drain(me: MadEngine, acc: List(Message), fuel: Int) -> #(MadEngine, List(Message)) {
  case fuel <= 0 {
    True -> panic as "multi-agent scenario did not quiesce within fuel"
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

// Route each message to `me`'s Receive (all messages here are destined for the one
// receiving agent under test).
fn deliver_all(me: MadEngine, msgs: List(Message)) -> MadEngine {
  list.fold(msgs, me, fn(me, msg) {
    let Message(name, term, _dest) = msg
    let assert Ok(me) = mad_engine.receive(me, name, term)
    me
  })
}

// ── Scenario §10.1: Direct Communication (client-monitor) ──────────────────────
//
// `client(Xs)@p, monitor(Xs?)@q` — a shared pair, writer Xs at p, reader Xs? at q,
// established by cold-call. p assigns Xs := [add | Xs1]; the value flows to q's
// reader. (Boot cold-call → Stage 1 assign+forward → Stage 2 receive+localize.)

// p's `client(Xs)` assigns Xs := [add] (a CLOSED list `[add | nil]`). The spec's
// open-stream form `Xs := [add | Xs1]` needs an anonymous WRITER in the output tail,
// which hits the known Gleam void-slot gap (goal_boot.gleam header: Dart
// `ConstTerm(null)` — a frozen-semantics gap surfaced loudly, NOT introduced here);
// a closed list exercises the identical value-flow path without a void writer.
const client_clause = "procedure client(_).
client([add]).
procedure send_val(_).
send_val(hi)."

pub fn client_monitor_value_flows_p_to_q_test() {
  let prog = boot_program(client_clause)
  let gs = kappa(prog, "global_send/3")
  let client_pc = kappa(prog, "client/1")

  let p = mad_engine.new(prog, atom("p"))
  let q = mad_engine.new(prog, atom("q"))

  // Stage 0 — cold-call: p exports reader Xs? to q's serializer. p holds writer Xs.
  let #(p, xs_writer, xs_reader) = mad_engine.alloc_local(p)
  let regs =
    program.new_regs()
    |> program.set_reg(0, StructTerm("req", [VarRef(xs_reader)]))
    |> program.set_reg(1, to_term(WriterName(atom("q"), 0)))
    |> program.set_reg(2, atom("q"))
  let #(p, _id) = mad_engine.boot(p, "global_send/3", gs, regs)
  let #(p, cold_call) = run_to_quiescence(p)
  // The cold-call is the serializer-wrapped `_w(q,0) := [req(_r(p,1)) | _w(q,0)]`.
  cold_call
  |> should.equal([
    Message(
      WriterName(atom("q"), 0),
      cons(
        StructTerm("req", [to_term(ReaderName(atom("p"), 1))]),
        to_term(WriterName(atom("q"), 0)),
      ),
      atom("q"),
    ),
  ])

  // q receives the cold-call → localizes _r(p,1): entry (Xs_q, p, 1), q holds Xs_q?.
  let q = deliver_all(q, cold_call)
  let assert Ok(LocalizeEntry(xs_q_writer, _, _)) =
    wt.find_localize(mad_engine.writers_table(q), atom("p"), 1)
  let xs_q_reader =
    heap.paired_reader(scheduler.heap(mad_engine.engine(q)), xs_q_writer)

  // Stage 1 — p's client assigns Xs := [add]; Xs? becomes known → the watching
  // global_send fires → forwards `_r(p,1) := [add]` to q ([add|nil] has no writer to
  // globalize, so no `_w(p,2)` sub-link — the closed-list variant of the spec stage 1).
  let regs2 = program.new_regs() |> program.set_reg(0, VarRef(xs_writer))
  let #(p, _id2) = mad_engine.boot(p, "client/1", client_pc, regs2)
  let #(_p, forwarded) = run_to_quiescence(p)
  forwarded
  |> should.equal([
    Message(ReaderName(atom("p"), 1), cons(atom("add"), terms.nil()), atom("q")),
  ])

  // Stage 2 — q receives `_r(p,1) := [add]`: finds (Xs_q, p, 1), assigns Xs_q := [add]
  // (no nested global names → nothing to localize), removes the entry.
  let q = deliver_all(q, forwarded)

  // The value [add] has flowed to q's monitor reader Xs_q?.
  let hq = scheduler.heap(mad_engine.engine(q))
  let assert Ok(#(_, Bound(cell))) = heap.deref(hq, xs_q_reader)
  cell |> should.equal(cons(atom("add"), terms.nil()))
  // The (Xs_q, p, 1) entry was consumed; no new entry added (ground value) — so q holds
  // no localize entries now.
  wt.localize_count(mad_engine.writers_table(q)) |> should.equal(0)
}

// ── Scenario §10.3: Friend-Mediated Introduction (charlie → bob → alice) ────────
//
// Bob introduces Alice to Charlie: exports reader X? to Alice (she receives) and
// writer X to Charlie (he sends). Charlie assigns X_c := hi; the value flows
// Charlie → Bob (Bob's local pair (X,X?) forwards) → Alice — three agents, two hops.

pub fn friend_intro_value_flows_charlie_to_alice_test() {
  let prog = boot_program(client_clause)
  let gs = kappa(prog, "global_send/3")
  let send_val_pc = kappa(prog, "send_val/1")

  let bob = mad_engine.new(prog, atom("bob"))
  let alice = mad_engine.new(prog, atom("alice"))
  let charlie = mad_engine.new(prog, atom("charlie"))

  // Bob's local pair (X, X?).
  let #(bob, x_writer, x_reader) = mad_engine.alloc_local(bob)

  // Step A — Bob exports reader X? to Alice's serializer → `_r(bob,1)`, no entry.
  let #(bob, _a1) =
    mad_engine.boot(bob, "global_send/3", gs, send_regs(
      StructTerm("intro", [VarRef(x_reader)]),
      WriterName(atom("alice"), 0),
      atom("alice"),
    ))
  let #(bob, to_alice_0) = run_to_quiescence(bob)
  let alice = deliver_all(alice, to_alice_0)
  let assert Ok(LocalizeEntry(xa_writer, _, _)) =
    wt.find_localize(mad_engine.writers_table(alice), atom("bob"), 1)
  let xa_reader =
    heap.paired_reader(scheduler.heap(mad_engine.engine(alice)), xa_writer)

  // Step B — Bob exports writer X to Charlie's serializer → entry (X, charlie) at
  // index 2, `_w(bob,2)`.
  let #(bob, _a2) =
    mad_engine.boot(bob, "global_send/3", gs, send_regs(
      StructTerm("deal", [VarRef(x_writer)]),
      WriterName(atom("charlie"), 0),
      atom("charlie"),
    ))
  let #(bob, to_charlie_0) = run_to_quiescence(bob)
  let charlie = deliver_all(charlie, to_charlie_0)
  // Charlie localized `_w(bob,2)` → holds writer X_c inside `deal(X_c)` on his net-in.
  let hc = scheduler.heap(mad_engine.engine(charlie))
  let assert Ok(#(_, Bound(deal_cell))) =
    heap.deref(hc, mad_engine.net_in_reader(charlie))
  let xc_writer = extract_deal_writer(deal_cell)

  // Step C — Charlie assigns X_c := hi (unit-clause head, same mechanism as `client`);
  // X_c? becomes known → the watching global_send fires → `_w(bob,2) := hi` to Bob.
  let #(charlie, _c1) =
    mad_engine.boot(charlie, "send_val/1", send_val_pc,
      program.new_regs() |> program.set_reg(0, VarRef(xc_writer)))
  let #(_charlie, to_bob) = run_to_quiescence(charlie)
  to_bob
  |> should.equal([Message(WriterName(atom("bob"), 2), atom("hi"), atom("bob"))])

  // Step D — Bob receives `_w(bob,2) := hi`: binds X := hi, removes the entry. X?
  // becomes known → Bob's watching global_send(X?, _r(bob,1), alice) fires →
  // `_r(bob,1) := hi` to Alice (Bob's local pair is the forwarding point).
  let bob = deliver_all(bob, to_bob)
  let #(_bob, to_alice_1) = run_to_quiescence(bob)
  to_alice_1
  |> should.equal([Message(ReaderName(atom("bob"), 1), atom("hi"), atom("alice"))])

  // Step E — Alice receives `_r(bob,1) := hi`: finds (X_a, bob, 1), assigns X_a := hi.
  let alice = deliver_all(alice, to_alice_1)
  let ha = scheduler.heap(mad_engine.engine(alice))
  let assert Ok(#(_, Bound(v))) = heap.deref(ha, xa_reader)
  v |> should.equal(atom("hi"))
}

fn send_regs(t: Term, g: global_name.GlobalName, dest: Term) -> program.XRegs {
  program.new_regs()
  |> program.set_reg(0, t)
  |> program.set_reg(1, to_term(g))
  |> program.set_reg(2, dest)
}

// Extract the writer addr from a localized `deal(X_c)` net-input cell `[deal(X_c) | _]`.
fn extract_deal_writer(net_cell: Term) -> Int {
  let assert StructTerm(".", [StructTerm("deal", [VarRef(w)]), _]) = net_cell
  w
}
