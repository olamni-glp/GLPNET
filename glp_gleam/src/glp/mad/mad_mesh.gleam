//// glp/mad/mad_mesh — Phase-B process-per-agent mesh (feature 050, T050.B).
////
//// Normative design: `specs/050-full-gleam-combined/contracts/madglp-port.md`
//// (§ Distinguished-channel registry, Gabi-approved). Phase A proved the madGLP
//// semantics with an IN-TEST routing loop (`mad_multiagent_test`); Phase B replaces
//// that loop with a real process mesh **without touching the semantics**: every
//// transaction still runs through the SAME `MadEngine` (Reduce/Send/Receive, spec §8)
//// — this module owns only WHERE each agent runs and HOW its messages travel.
////
//// Shape:
////   * **Agent = one BEAM process** owning its `MadEngine` (the engine value moves
////     into the process at spawn and never leaves except as `Inspect` snapshots —
////     immutable copies, so no shared mutable state exists anywhere).
////   * **Send = inter-process message.** After each Reduce, the drained M_p is routed:
////     `Message.dest` → the destination's INPUT channel in the registry →
////     `process.send(inbox, Deliver(msg))`. BEAM guarantees per-sender-pair mailbox
////     order, which is exactly spec §13's per-pair FIFO (and no more — no cross-pair
////     order is promised, matching the spec).
////   * **Receive = the ctl loop.** A `Deliver` runs `mad_engine.receive` (the spec §8.3
////     three-case transaction, unchanged) and then pumps the engine to quiescence,
////     routing any newly drained M_p.
////   * **no-OTP** (T048/T050.B pin): `spawn_unlinked`/`new_subject`/`receive` only —
////     no supervisors, no gen_server. Unlinked for the same two reasons as the C6
////     pump: an agent crash must surface as a fault DATUM, never as an exit signal
////     into the embedder; and `Stop`/kill must not cascade.
////
//// **Distinguished-channel registry (D-6 twin).** Identity here is the lightweight
//// `(agent, role, channel-tag)` namespace ABOVE transports — NOT a `LinkId` (a LinkId
//// names one physical bilateral link; its nonce is a carrier fact, meaningless as a
//// logical channel identity — do not merge the two registries). Roles are
//// {input, output, monitor} with room for several channels per role via the tag; the
//// tag preserves the int≠string distinction (`TagInt(5)` ≠ `TagStr("5")`), mirroring
//// `NonceInt`≠`NonceStr`. The base mesh registers each agent's default input channel
//// at `TagInt(0)`; `Output` exists in the namespace (an agent's outbound side is
//// first-class addressable) even though the base routes writes directly to the
//// destination's input.
////
//// **stderr-equivalent = the monitor channel** (contract ruling): route misses,
//// Receive failures and step faults are `MeshFault` DATA on the mesh monitor subject
//// — independently observable, never a crash and never a logical failure of the
//// victim's goals (the FR-043/044 discipline, applied above the transports).

import gleam/dict.{type Dict}
import gleam/erlang/process.{type Pid, type Subject}
import gleam/list
import gleam/string
import glp/bytecode/program.{type XRegs}
import glp/engine/runner
import glp/engine/scheduler.{
  StepErrored, StepFailed, StepIdle, StepReduced, StepSuspended,
}
import glp/mad/mad_engine.{type MadEngine}
import glp/mad/message.{type Message, Message}
import glp/runtime/terms.{type Term}

// ── the (role, tag) channel namespace ────────────────────────────────────────

/// Channel role. `Output` is part of the namespace by contract ("roles ⊇ {input,
/// output, fault/monitor}") even though the base mesh routes directly to inputs.
pub type Role {
  Input
  Output
  Monitor
}

/// Channel tag — the "multiple channels per role" axis. Int and string tags are
/// DISTINCT (the NonceInt≠NonceStr discipline carried up a layer).
pub type Tag {
  TagInt(Int)
  TagStr(String)
}

/// A registered channel end. Input/Output carry agent-ctl inboxes; Monitor carries
/// the fault sink.
pub type Channel {
  CtlChannel(Subject(AgentCtl))
  FaultChannel(Subject(MeshFault))
}

/// The distinguished-channel registry: `(agent, role, tag)` → channel. Immutable —
/// built once at mesh boot and handed to every agent in `Start`.
pub opaque type MeshRegistry {
  MeshRegistry(channels: Dict(#(Term, Role, Tag), Channel))
}

pub fn registry_new() -> MeshRegistry {
  MeshRegistry(channels: dict.new())
}

pub fn register(
  registry: MeshRegistry,
  agent: Term,
  role: Role,
  tag: Tag,
  channel: Channel,
) -> MeshRegistry {
  MeshRegistry(channels: dict.insert(
    registry.channels,
    #(agent, role, tag),
    channel,
  ))
}

/// The default input inbox for `agent` (`(agent, Input, TagInt(0))`).
pub fn input(registry: MeshRegistry, agent: Term) -> Result(Subject(AgentCtl), Nil) {
  case dict.get(registry.channels, #(agent, Input, TagInt(0))) {
    Ok(CtlChannel(subject)) -> Ok(subject)
    _ -> Error(Nil)
  }
}

// ── the ctl protocol ─────────────────────────────────────────────────────────

/// A fault datum on the mesh monitor — the stderr-equivalent. Data, never a crash.
pub type MeshFault {
  MeshFault(agent: Term, detail: String)
}

/// An agent's state as of one `Inspect` — an immutable copy; reading it can neither
/// race nor mutate the live agent.
pub type Snapshot {
  Snapshot(me: MadEngine)
}

/// The agent control protocol. `Start` is always first (the spawn handshake ends
/// before anything else can learn the inbox); everything after runs the SAME Phase-A
/// transactions.
pub type AgentCtl {
  /// The mesh wiring: the registry to route by + the monitor to fault to. On receipt
  /// the agent pumps its pre-booted goals to quiescence (its first Reduce/Send round).
  Start(registry: MeshRegistry, monitor: Subject(MeshFault))
  /// One inbound assignment — runs the spec §8.3 Receive, then pumps.
  Deliver(msg: Message)
  /// Boot a goal into the running agent (the embedder seam — Dart agent_runtime
  /// boots goals the same way), then pump.
  Boot(procedure: String, entry_pc: Int, regs: XRegs)
  /// Snapshot request (test/inspection seam).
  Inspect(reply: Subject(Snapshot))
  /// End the agent process. Unlinked, so nothing cascades.
  Stop
}

// ── spawn + loop ─────────────────────────────────────────────────────────────

/// Spawn one agent process owning `me` (pre-booted by the caller — goals may also be
/// injected later via `Boot`). The process creates its OWN inbox (a subject may only
/// be received on by its owner) and hands it back over `ready`; the caller collects
/// every agent's inbox, builds the registry, and sends `Start`.
pub fn spawn_agent(
  me: MadEngine,
  agent: Term,
  ready: Subject(#(Term, Subject(AgentCtl))),
) -> Pid {
  process.spawn_unlinked(fn() {
    let inbox = process.new_subject()
    process.send(ready, #(agent, inbox))
    // First ctl is Start by construction; anything else is a wiring bug — loud.
    case process.receive_forever(inbox) {
      Start(registry, monitor) -> {
        let me = pump(me, agent, registry, monitor)
        agent_loop(me, agent, inbox, registry, monitor)
      }
      _ -> panic as "mad_mesh: agent received ctl before Start"
    }
  })
}

fn agent_loop(
  me: MadEngine,
  agent: Term,
  inbox: Subject(AgentCtl),
  registry: MeshRegistry,
  monitor: Subject(MeshFault),
) -> Nil {
  case process.receive_forever(inbox) {
    Start(_, _) -> {
      process.send(monitor, MeshFault(agent, "duplicate Start ignored"))
      agent_loop(me, agent, inbox, registry, monitor)
    }
    Deliver(Message(name, value, _dest)) ->
      case mad_engine.receive(me, name, value) {
        // A Receive failure is surfaced as monitor DATA (a missing W_p entry is a
        // protocol violation by the sender — dedup is T052) and the agent LIVES ON:
        // its own goals are not the violator (FR-044 discipline).
        Error(detail) -> {
          process.send(monitor, MeshFault(agent, "Receive failed: " <> detail))
          agent_loop(me, agent, inbox, registry, monitor)
        }
        Ok(me) ->
          agent_loop(
            pump(me, agent, registry, monitor),
            agent,
            inbox,
            registry,
            monitor,
          )
      }
    Boot(procedure, entry_pc, regs) -> {
      let #(me, _id) = mad_engine.boot(me, procedure, entry_pc, regs)
      agent_loop(
        pump(me, agent, registry, monitor),
        agent,
        inbox,
        registry,
        monitor,
      )
    }
    Inspect(reply) -> {
      process.send(reply, Snapshot(me))
      agent_loop(me, agent, inbox, registry, monitor)
    }
    Stop -> Nil
  }
}

/// Reduce to quiescence, routing every drained outgoing message as it appears —
/// the Phase-A `run_to_quiescence` + `deliver_all` fused, with the delivery now a
/// `process.send`. A `StepErrored` faults to the monitor and stops pumping (the
/// engine value is kept as-was for inspection); `StepFailed` is an ordinary GLP
/// outcome and pumping continues.
fn pump(
  me: MadEngine,
  agent: Term,
  registry: MeshRegistry,
  monitor: Subject(MeshFault),
) -> MadEngine {
  let #(me, outcome, msgs) = mad_engine.step(me, 5000)
  route_all(msgs, agent, registry, monitor)
  case outcome {
    StepIdle -> me
    StepErrored(fault) -> {
      process.send(
        monitor,
        MeshFault(agent, "step errored: " <> describe_fault(fault)),
      )
      me
    }
    StepReduced(..) -> pump(me, agent, registry, monitor)
    StepSuspended(..) -> pump(me, agent, registry, monitor)
    StepFailed(..) -> pump(me, agent, registry, monitor)
  }
}

/// Route one step's drained M_p. An unroutable destination is a monitor fault, not a
/// crash — and NOT a failure of the sending agent's goals.
fn route_all(
  msgs: List(Message),
  agent: Term,
  registry: MeshRegistry,
  monitor: Subject(MeshFault),
) -> Nil {
  list.each(msgs, fn(msg) {
    let Message(_name, _value, dest) = msg
    case input(registry, dest) {
      Ok(inbox) -> process.send(inbox, Deliver(msg))
      Error(Nil) ->
        process.send(
          monitor,
          MeshFault(
            agent,
            "no input channel for destination " <> string.inspect(dest),
          ),
        )
    }
  })
}

fn describe_fault(fault: runner.RunnerFault) -> String {
  case fault {
    runner.Unimplemented(m) -> "unimplemented " <> m
    runner.StructuralViolation(d) -> "structural " <> d
    runner.Malformed(d) -> "malformed " <> d
  }
}

// ── mesh boot (the coordinator half) ─────────────────────────────────────────

/// One running mesh, from the coordinator's side: the registry, every agent's inbox,
/// the fault monitor (owned by the CALLER — create the mesh on the process that will
/// read faults), and the agent Pids.
pub type Mesh {
  Mesh(
    registry: MeshRegistry,
    inboxes: Dict(Term, Subject(AgentCtl)),
    monitor: Subject(MeshFault),
    pids: List(Pid),
  )
}

/// Boot a mesh over pre-booted agents: spawn each, collect inboxes over the ready
/// handshake, build the registry (each agent's default input at `TagInt(0)` + the
/// mesh monitor at `(mesh, Monitor, TagInt(0))`), and `Start` everyone. Agents begin
/// pumping their pre-booted goals immediately on `Start`.
pub fn boot_mesh(agents: List(#(Term, MadEngine))) -> Mesh {
  let monitor = process.new_subject()
  let ready = process.new_subject()
  let pids =
    list.map(agents, fn(pair) {
      let #(agent, me) = pair
      spawn_agent(me, agent, ready)
    })
  let inboxes = collect_ready(ready, list.length(agents), dict.new())
  let registry =
    dict.fold(inboxes, registry_new(), fn(registry, agent, inbox) {
      register(registry, agent, Input, TagInt(0), CtlChannel(inbox))
    })
    |> register(
      terms.ConstTerm(terms.ConstAtom("mesh")),
      Monitor,
      TagInt(0),
      FaultChannel(monitor),
    )
  dict.each(inboxes, fn(_agent, inbox) {
    process.send(inbox, Start(registry, monitor))
  })
  Mesh(registry: registry, inboxes: inboxes, monitor: monitor, pids: pids)
}

fn collect_ready(
  ready: Subject(#(Term, Subject(AgentCtl))),
  remaining: Int,
  acc: Dict(Term, Subject(AgentCtl)),
) -> Dict(Term, Subject(AgentCtl)) {
  case remaining <= 0 {
    True -> acc
    False -> {
      let assert Ok(#(agent, inbox)) = process.receive(ready, 5000)
      collect_ready(ready, remaining - 1, dict.insert(acc, agent, inbox))
    }
  }
}

/// Stop every agent (unlinked processes — nothing cascades).
pub fn stop_mesh(mesh: Mesh) -> Nil {
  dict.each(mesh.inboxes, fn(_agent, inbox) { process.send(inbox, Stop) })
}

/// Synchronous snapshot of one agent (round-trips through its ctl FIFO, so the
/// snapshot reflects every Deliver/Boot the agent had already received).
pub fn inspect(mesh: Mesh, agent: Term) -> Result(Snapshot, Nil) {
  case dict.get(mesh.inboxes, agent) {
    Error(_) -> Error(Nil)
    Ok(inbox) -> {
      let reply = process.new_subject()
      process.send(inbox, Inspect(reply))
      case process.receive(reply, 5000) {
        Ok(snapshot) -> Ok(snapshot)
        Error(_) -> Error(Nil)
      }
    }
  }
}

/// Boot a goal into a running agent.
pub fn boot_goal(
  mesh: Mesh,
  agent: Term,
  procedure: String,
  entry_pc: Int,
  regs: XRegs,
) -> Result(Nil, Nil) {
  case dict.get(mesh.inboxes, agent) {
    Error(_) -> Error(Nil)
    Ok(inbox) -> Ok(process.send(inbox, Boot(procedure, entry_pc, regs)))
  }
}
