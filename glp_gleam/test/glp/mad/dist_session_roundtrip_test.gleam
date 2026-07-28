//// glp/mad/dist_session — two-MadEngine goal/result round-trip over a REAL link
//// (feature 059, T066 `close-distribution-engine-sessions`, slice S3 — the acceptance).
////
//// This is the wire-borne counterpart of `mad_multiagent_test`'s
//// `client_monitor_value_flows_p_to_q_test`: the identical spec §10.1 client-monitor
//// value flow (writer Xs@p, reader Xs?@q, established by cold-call; p assigns Xs :=
//// [add]; the value reaches q's reader), but every assignment message now crosses a
//// genuine loopback `Endpoint` pair — encoded to a wire frame (S1 `message_codec`),
//// shipped over the transport seam (S2 `dist_session`), received + decoded on the far
//// end, and bound by `mad_engine.receive` (spec §8.3). The convergence outcome MUST
//// match the direct-delivery test (and the T083 Lean `deliver`/`deliver_binds_owner`
//// proof): q's monitor reader derefs to `[add]`.
////
//// The drive loop mirrors `link_pump.drive` (run each agent to quiescence → drain M_p →
//// ship over the session → peer recv + Receive → re-drive), lifted from the link-goal
//// engine to the MadEngine. The transport in the middle is real: the loopback leaf's
//// ordered-channel processes behind the T045 seam, the same seam TCP/QUIC ride.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/erlang/process
import gleam/list
import gleam/option.{Some}
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/runner
import glp/engine/scheduler.{StepErrored, StepIdle}
import glp/link/seam/link_address
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/link/transports/loopback
import glp/mad/dist_session.{type DistSession}
import glp/mad/global_name.{WriterName, to_term}
import glp/mad/global_writers_table.{LocalizeEntry} as wt
import glp/mad/mad_engine.{type MadEngine}
import glp/mad/message.{type Message}
import glp/runtime/heap.{Bound}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstTerm, StructTerm, VarRef, cons,
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

// Step `me` to quiescence, accumulating every drained outgoing message.
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

/// Rendezvous a loopback endpoint pair on `channel` (server, client) — the connector is
/// spawned in a child process because the seam's listen/connect are synchronous.
fn loopback_pair(channel: String) {
  let t = loopback.new()
  let addr = link_address.path(channel)
  let opts = link_options.default()
  let back = process.new_subject()
  process.spawn(fn() {
    let conn = t.connect(link_scheme.loopback(), addr, opts)
    process.send(back, conn)
  })
  let assert Ok(server) = t.listen(link_scheme.loopback(), addr, opts)
  let assert Ok(conn_result) = process.receive(back, 5000)
  let assert Ok(client) = conn_result
  #(server, client)
}

/// Ship every message in `msgs` from `sender`'s session, then receive + Receive each on
/// the destination engine `dest` over `dest_session` (peer key `sender_agent`). Every
/// frame crosses the real endpoint. Returns the advanced sessions + destination engine.
fn route_all(
  sender_session: DistSession,
  dest_session: DistSession,
  dest: MadEngine,
  sender_agent: Term,
  msgs: List(Message),
) -> #(DistSession, MadEngine) {
  // Send all (buffered FIFO on the loopback channel), then drain that many on the far end.
  let sender_session =
    list.fold(msgs, sender_session, fn(s, msg) {
      let assert Ok(s) = dist_session.send(s, msg)
      s
    })
  let dest =
    list.fold(msgs, dest, fn(dest, _msg) {
      let assert Ok(Some(#(name, term))) =
        dist_session.recv(dest_session, sender_agent)
      let assert Ok(dest) = mad_engine.receive(dest, name, term)
      dest
    })
  #(sender_session, dest)
}

// ── Acceptance: §10.1 value flow, p → q, across a genuine loopback link ─────────
pub fn client_monitor_value_flows_over_real_link_test() {
  let prog = boot_program(client_clause)
  let gs = kappa(prog, "global_send/3")
  let client_pc = kappa(prog, "client/1")

  let p = mad_engine.new(prog, atom("p"))
  let q = mad_engine.new(prog, atom("q"))

  // The real transport between p and q: p ships on `p_ep`, q receives on `q_ep`.
  let #(q_ep, p_ep) = loopback_pair("t066-p-to-q")
  let p_session = dist_session.connect(dist_session.new(), atom("q"), p_ep)
  let q_session = dist_session.connect(dist_session.new(), atom("p"), q_ep)

  // Stage 0 — cold-call: p exports reader Xs? to q's serializer. p holds writer Xs.
  let #(p, xs_writer, xs_reader) = mad_engine.alloc_local(p)
  let regs =
    program.new_regs()
    |> program.set_reg(0, StructTerm("req", [VarRef(xs_reader)]))
    |> program.set_reg(1, to_term(WriterName(atom("q"), 0)))
    |> program.set_reg(2, atom("q"))
  let #(p, _id) = mad_engine.boot(p, "global_send/3", gs, regs)
  let #(p, cold_call) = run_to_quiescence(p)

  // Route the cold-call over the real link → q localizes _r(p,1).
  let #(p_session, q) = route_all(p_session, q_session, q, atom("p"), cold_call)
  let assert Ok(LocalizeEntry(xs_q_writer, _, _)) =
    wt.find_localize(mad_engine.writers_table(q), atom("p"), 1)
  let xs_q_reader =
    heap.paired_reader(scheduler.heap(mad_engine.engine(q)), xs_q_writer)

  // Stage 1 — p's client assigns Xs := [add]; the watching global_send forwards
  // `_r(p,1) := [add]` to q.
  let regs2 = program.new_regs() |> program.set_reg(0, VarRef(xs_writer))
  let #(p, _id2) = mad_engine.boot(p, "client/1", client_pc, regs2)
  let #(_p, forwarded) = run_to_quiescence(p)

  // Route the forwarded assignment over the real link → q binds Xs_q := [add].
  let #(_p_session, q) =
    route_all(p_session, q_session, q, atom("p"), forwarded)

  // The value [add] has flowed to q's monitor reader over the wire.
  let hq = scheduler.heap(mad_engine.engine(q))
  let assert Ok(#(_, Bound(cell))) = heap.deref(hq, xs_q_reader)
  cell |> should.equal(cons(atom("add"), terms.nil()))
  // The (Xs_q, p, 1) entry was consumed; ground value → no new entry.
  wt.localize_count(mad_engine.writers_table(q)) |> should.equal(0)
}

// ── Adversarial: a duplicate wire delivery of the same assignment is refused ─────
//
// The owner-only-bind discipline holds across the seam: once q's writer is bound, a
// SECOND arrival of the same `_r(p,1) := [add]` frame is refused loudly by Receive (no
// duplicated / lost binding). Mirrors the T083 direct-delivery adversarial case, now
// with the duplicate crossing the real link.
pub fn duplicate_wire_delivery_is_refused_test() {
  let prog = boot_program(client_clause)
  let gs = kappa(prog, "global_send/3")
  let client_pc = kappa(prog, "client/1")

  let p = mad_engine.new(prog, atom("p"))
  let q = mad_engine.new(prog, atom("q"))

  let #(q_ep, p_ep) = loopback_pair("t066-dup")
  let p_session = dist_session.connect(dist_session.new(), atom("q"), p_ep)
  let q_session = dist_session.connect(dist_session.new(), atom("p"), q_ep)

  let #(p, xs_writer, xs_reader) = mad_engine.alloc_local(p)
  let regs =
    program.new_regs()
    |> program.set_reg(0, StructTerm("req", [VarRef(xs_reader)]))
    |> program.set_reg(1, to_term(WriterName(atom("q"), 0)))
    |> program.set_reg(2, atom("q"))
  let #(p, _id) = mad_engine.boot(p, "global_send/3", gs, regs)
  let #(p, cold_call) = run_to_quiescence(p)
  let #(p_session, q) = route_all(p_session, q_session, q, atom("p"), cold_call)

  let regs2 = program.new_regs() |> program.set_reg(0, VarRef(xs_writer))
  let #(p, _id2) = mad_engine.boot(p, "client/1", client_pc, regs2)
  let #(_p, forwarded) = run_to_quiescence(p)
  let #(p_session, q) =
    route_all(p_session, q_session, q, atom("p"), forwarded)

  // Ship the SAME forwarded assignment a second time and deliver it: Receive refuses it
  // (the owner writer is already bound) — surfaced, never a silent second bind.
  let assert [msg, ..] = forwarded
  let assert Ok(_p_session) = dist_session.send(p_session, msg)
  let assert Ok(Some(#(name, term))) =
    dist_session.recv(q_session, atom("p"))
  mad_engine.receive(q, name, term)
  |> should.be_error
}
