//// T050.B — the Phase-B process mesh, proven by re-running the Phase-A parity
//// scenarios (spec §10.1 client-monitor, §10.3 friend-intro) with the in-test routing
//// loop replaced by REAL agent processes. The assertions are the SAME value-flow
//// outcomes `mad_multiagent_test` pins — that identity is the whole point: Phase B
//// changed where agents run and how messages travel, and nothing else.
////
//// Test mechanics forced by the process boundary:
////   * heap reads go through `Inspect` snapshots (immutable copies) instead of
////     direct engine access;
////   * cross-agent effects are ASYNCHRONOUS, so assertions poll bounded-retry until
////     the flow settles (a hang = test failure by budget, never a silent pass);
////   * mid-scenario goals (`client/1`, `send_val/1`) are injected with `Boot` — the
////     embedder seam — instead of `mad_engine.boot` on a locally-held engine.
////
//// Every scenario ends by asserting the mesh monitor is EMPTY: a route miss, a
//// Receive failure or a step fault anywhere would have surfaced there as data.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/erlang/process
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/scheduler
import glp/mad/global_name.{WriterName, to_term}
import glp/mad/global_writers_table.{LocalizeEntry} as wt
import glp/mad/mad_engine
import glp/mad/mad_mesh.{type Mesh, type Snapshot, Snapshot}
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

// Same scenario clauses as Phase A (closed-list variant — see mad_multiagent_test on
// the void-slot gap).
const client_clause = "procedure client(_).
client([add]).
procedure send_val(_).
send_val(hi)."

/// Poll `check` over fresh snapshots of `agent` until it yields, or fail by budget.
/// The retry is what absorbs the mesh's asynchrony; the budget is what keeps a broken
/// flow a FAILURE rather than a hang.
fn poll(mesh: Mesh, agent: Term, budget: Int, check: fn(Snapshot) -> Result(a, Nil)) -> a {
  case mad_mesh.inspect(mesh, agent) {
    Ok(snapshot) ->
      case check(snapshot) {
        Ok(value) -> value
        Error(Nil) ->
          case budget <= 0 {
            True -> panic as "mesh scenario did not settle within budget"
            False -> {
              process.sleep(20)
              poll(mesh, agent, budget - 1, check)
            }
          }
      }
    Error(Nil) -> panic as "mesh inspect failed (agent gone?)"
  }
}

fn monitor_is_empty(mesh: Mesh) -> Nil {
  case process.receive(mesh.monitor, 0) {
    Error(Nil) -> Nil
    Ok(mad_mesh.MeshFault(agent, detail)) ->
      panic as {
        "mesh monitor not empty: " <> string_of(agent) <> ": " <> detail
      }
  }
}

fn string_of(t: Term) -> String {
  case t {
    ConstTerm(ConstAtom(a)) -> a
    _ -> "?"
  }
}

// ── §10.1 client-monitor over the mesh ───────────────────────────────────────

pub fn mesh_client_monitor_value_flows_p_to_q_test() {
  let prog = boot_program(client_clause)
  let gs = kappa(prog, "global_send/3")
  let client_pc = kappa(prog, "client/1")

  // p pre-booted with the cold-call export (writer Xs stays with p); q bare.
  let p = mad_engine.new(prog, atom("p"))
  let #(p, xs_writer, xs_reader) = mad_engine.alloc_local(p)
  let regs =
    program.new_regs()
    |> program.set_reg(0, StructTerm("req", [VarRef(xs_reader)]))
    |> program.set_reg(1, to_term(WriterName(atom("q"), 0)))
    |> program.set_reg(2, atom("q"))
  let #(p, _id) = mad_engine.boot(p, "global_send/3", gs, regs)
  let q = mad_engine.new(prog, atom("q"))

  let mesh = mad_mesh.boot_mesh([#(atom("p"), p), #(atom("q"), q)])

  // The cold-call crossed the mesh: q localized `_r(p,1)` → entry (Xs_q, p, 1).
  let #(xs_q_writer, xs_q_reader) =
    poll(mesh, atom("q"), 100, fn(snapshot) {
      let Snapshot(q) = snapshot
      case wt.find_localize(mad_engine.writers_table(q), atom("p"), 1) {
        Ok(LocalizeEntry(w, _, _)) ->
          Ok(#(w, heap.paired_reader(scheduler.heap(mad_engine.engine(q)), w)))
        _ -> Error(Nil)
      }
    })

  // Stage 1 — inject `client(Xs)` into RUNNING p: Xs := [add], the watching
  // global_send fires, `_r(p,1) := [add]` crosses to q.
  let regs2 = program.new_regs() |> program.set_reg(0, VarRef(xs_writer))
  let assert Ok(Nil) = mad_mesh.boot_goal(mesh, atom("p"), "client/1", client_pc, regs2)

  // Stage 2 — the value lands on q's monitor reader; the entry is consumed.
  poll(mesh, atom("q"), 100, fn(snapshot) {
    let Snapshot(q) = snapshot
    let hq = scheduler.heap(mad_engine.engine(q))
    case heap.deref(hq, xs_q_reader) {
      Ok(#(_, Bound(cell))) ->
        case cell == cons(atom("add"), terms.nil()) {
          True -> {
            wt.localize_count(mad_engine.writers_table(q)) |> should.equal(0)
            Ok(Nil)
          }
          False -> Error(Nil)
        }
      _ -> Error(Nil)
    }
  })
  // Silence the unused-writer read: xs_q_writer was needed only to derive the reader.
  let _ = xs_q_writer

  monitor_is_empty(mesh)
  mad_mesh.stop_mesh(mesh)
}

// ── §10.3 friend-intro over the mesh (three agents, two hops) ────────────────

pub fn mesh_friend_intro_value_flows_charlie_to_alice_test() {
  let prog = boot_program(client_clause)
  let gs = kappa(prog, "global_send/3")
  let send_val_pc = kappa(prog, "send_val/1")

  // Bob pre-booted with BOTH exports, in the Phase-A order so the indices match:
  // reader X? → alice first (`_r(bob,1)`), writer X → charlie second (`_w(bob,2)`).
  let bob = mad_engine.new(prog, atom("bob"))
  let #(bob, x_writer, x_reader) = mad_engine.alloc_local(bob)
  let #(bob, _a1) =
    mad_engine.boot(bob, "global_send/3", gs, send_regs(
      StructTerm("intro", [VarRef(x_reader)]),
      WriterName(atom("alice"), 0),
      atom("alice"),
    ))
  let #(bob, _a2) =
    mad_engine.boot(bob, "global_send/3", gs, send_regs(
      StructTerm("deal", [VarRef(x_writer)]),
      WriterName(atom("charlie"), 0),
      atom("charlie"),
    ))
  let alice = mad_engine.new(prog, atom("alice"))
  let charlie = mad_engine.new(prog, atom("charlie"))

  let mesh =
    mad_mesh.boot_mesh([
      #(atom("bob"), bob),
      #(atom("alice"), alice),
      #(atom("charlie"), charlie),
    ])

  // Alice localized `_r(bob,1)`.
  let xa_reader =
    poll(mesh, atom("alice"), 100, fn(snapshot) {
      let Snapshot(alice) = snapshot
      case wt.find_localize(mad_engine.writers_table(alice), atom("bob"), 1) {
        Ok(LocalizeEntry(w, _, _)) ->
          Ok(heap.paired_reader(scheduler.heap(mad_engine.engine(alice)), w))
        _ -> Error(Nil)
      }
    })

  // Charlie localized `_w(bob,2)` → holds writer X_c inside `deal(X_c)` on net-in.
  let xc_writer =
    poll(mesh, atom("charlie"), 100, fn(snapshot) {
      let Snapshot(charlie) = snapshot
      let hc = scheduler.heap(mad_engine.engine(charlie))
      case heap.deref(hc, mad_engine.net_in_reader(charlie)) {
        Ok(#(_, Bound(StructTerm(".", [StructTerm("deal", [VarRef(w)]), _])))) ->
          Ok(w)
        _ -> Error(Nil)
      }
    })

  // Charlie assigns X_c := hi; the value flows charlie → bob → alice on its own —
  // bob's forwarding hop is HIS process reacting to the Receive, nobody drives it.
  let assert Ok(Nil) =
    mad_mesh.boot_goal(mesh, atom("charlie"), "send_val/1", send_val_pc,
      program.new_regs() |> program.set_reg(0, VarRef(xc_writer)))

  poll(mesh, atom("alice"), 200, fn(snapshot) {
    let Snapshot(alice) = snapshot
    let ha = scheduler.heap(mad_engine.engine(alice))
    case heap.deref(ha, xa_reader) {
      Ok(#(_, Bound(v))) ->
        case v == atom("hi") {
          True -> Ok(Nil)
          False -> Error(Nil)
        }
      _ -> Error(Nil)
    }
  })

  monitor_is_empty(mesh)
  mad_mesh.stop_mesh(mesh)
}

fn send_regs(t: Term, g: global_name.GlobalName, dest: Term) -> program.XRegs {
  program.new_regs()
  |> program.set_reg(0, t)
  |> program.set_reg(1, to_term(g))
  |> program.set_reg(2, dest)
}

// ── §13 per-pair FIFO across the mesh ────────────────────────────────────────

/// Two cold-calls p→q must extend q's network-input stream IN SEND ORDER: BEAM
/// per-sender-pair mailbox order + the agent ctl FIFO is the spec §13 guarantee.
pub fn mesh_preserves_per_pair_fifo_test() {
  let prog = boot_program(client_clause)
  let gs = kappa(prog, "global_send/3")

  let p = mad_engine.new(prog, atom("p"))
  let #(p, _b1) =
    mad_engine.boot(p, "global_send/3", gs, send_regs(
      atom("first"),
      WriterName(atom("q"), 0),
      atom("q"),
    ))
  let #(p, _b2) =
    mad_engine.boot(p, "global_send/3", gs, send_regs(
      atom("second"),
      WriterName(atom("q"), 0),
      atom("q"),
    ))
  let q = mad_engine.new(prog, atom("q"))
  let mesh = mad_mesh.boot_mesh([#(atom("p"), p), #(atom("q"), q)])

  // q's net-in stream reads [first, second | _] — send order, exactly.
  poll(mesh, atom("q"), 100, fn(snapshot) {
    let Snapshot(q) = snapshot
    let hq = scheduler.heap(mad_engine.engine(q))
    case heap.deref(hq, mad_engine.net_in_reader(q)) {
      Ok(#(hq, Bound(StructTerm(".", [head1, VarRef(tail1)])))) ->
        case head1 == atom("first") {
          False -> Error(Nil)
          True ->
            case heap.deref(hq, tail1) {
              Ok(#(_, Bound(StructTerm(".", [head2, _])))) ->
                case head2 == atom("second") {
                  True -> Ok(Nil)
                  False -> Error(Nil)
                }
              _ -> Error(Nil)
            }
        }
      _ -> Error(Nil)
    }
  })

  monitor_is_empty(mesh)
  mad_mesh.stop_mesh(mesh)
}

// ── faults are DATA on the monitor (stderr-equivalent) ───────────────────────

/// A message to an agent nobody registered is a route-miss FAULT on the monitor —
/// the sender lives on; nothing crashes (FR-043/044 discipline above transports).
pub fn mesh_route_miss_is_monitor_data_test() {
  let prog = boot_program(client_clause)
  let gs = kappa(prog, "global_send/3")

  let p = mad_engine.new(prog, atom("p"))
  let #(p, _b) =
    mad_engine.boot(p, "global_send/3", gs, send_regs(
      atom("lost"),
      WriterName(atom("ghost"), 0),
      atom("ghost"),
    ))
  let mesh = mad_mesh.boot_mesh([#(atom("p"), p)])

  let assert Ok(mad_mesh.MeshFault(agent, _detail)) =
    process.receive(mesh.monitor, 5000)
  agent |> should.equal(atom("p"))

  // p is alive and inspectable after the miss.
  let assert Ok(Snapshot(_)) = mad_mesh.inspect(mesh, atom("p"))
  mad_mesh.stop_mesh(mesh)
}
