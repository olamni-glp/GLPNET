//// glp/mad/mad_engine — the madGLP engine wrapping `scheduler.Engine` with the
//// per-agent global state W_p / M_p and the Send + Receive transactions
//// (feature 050, T050.A3).
////
//// A `MadEngine` is one agent p's local state s_p = (R_p, W_p, M_p) (spec §6): the
//// wrapped `scheduler.Engine` IS the resolvent R_p (its run queue + suspensions +
//// heap); `w_p` is the immutable global writers table (T050.A0); M_p is drained
//// per step (the Send transaction, §8.2 — enabled whenever M_p ≠ ∅, so it never
//// accumulates in the value). The index counter lives inside W_p (T050.A0), so it
//// is not a separate field (the contract's `index` field folded there).
////
//// Faithful port of glp_runtime/lib/multiagent/mad_context.dart (`handleMadAssignment`
//// + `flushMessages`) and agent_runtime.dart (boot serializer entry). The three
//// Receive cases (spec §8.3) reproduce the Dart transaction EXACTLY, on the immutable
//// engine: Localize the arriving term, bind the target local writer, reactivate the
//// goals the binding wakes, and update/remove the W_p entry.
////
//// Boot (c₀, spec §7): each agent installs a permanent index-0 serializer entry
//// mapping `_r(p,0)` to the local writer N_p of its network-input stream (§4.1). The
//// resolvent's initial agent goal `agent(p, …)` is booted by the caller (`boot`) — A4
//// wires the prelude `agent/3` + `send_to_net/1`.

import gleam/int
import gleam/option.{None, Some}
import gleam/result
import glp/bytecode/program.{type BytecodeProgram, type XRegs}
import glp/engine/scheduler.{type Engine, type StepOutcome}
import glp/mad/global_name.{type GlobalName, ReaderName, WriterName}
import glp/mad/global_writers_table.{type WritersTable}
import glp/mad/localize
import glp/mad/mad_kernels.{MadState}
import glp/mad/message.{type Message}
import glp/runtime/heap
import glp/runtime/terms.{type Term, StructTerm, VarRef, cons}

/// One agent's madGLP local state: the wrapped resolvent engine, the persistent global
/// writers table, and the reader end of the agent's network-input stream (the serializer
/// extends this on every cold-call — spec §4.1). Opaque; every transaction returns a new
/// value (the engine's discipline).
pub opaque type MadEngine {
  MadEngine(engine: Engine, w_p: WritersTable, net_in_reader: Int)
}

/// Boot agent `agent`'s madGLP local state over `program` (spec §7 c₀): a fresh heap
/// with an allocated network-input variable N_p, and W_p holding ONLY the permanent
/// index-0 serializer entry `(N_p, *)` (spec §4.1). The resolvent starts empty — the
/// caller boots the agent goal with `boot`.
pub fn new(program: BytecodeProgram, agent: Term) -> MadEngine {
  let #(heap, net_in_writer, net_in_reader) = heap.allocate_variable(heap.new())
  let w_p =
    global_writers_table.new(agent)
    |> global_writers_table.init_serializer(net_in_writer)
  MadEngine(
    engine: scheduler.new(program, heap),
    w_p: w_p,
    net_in_reader: net_in_reader,
  )
}

/// The reader end of this agent's network-input stream (the serializer writes cold-call
/// content here). The booted agent goal reads it as its `NetIn` channel.
pub fn net_in_reader(me: MadEngine) -> Int {
  me.net_in_reader
}

/// This agent's global writers table (test/inspection hook).
pub fn writers_table(me: MadEngine) -> WritersTable {
  me.w_p
}

/// The wrapped resolvent engine (for output / status / trace inspection).
pub fn engine(me: MadEngine) -> Engine {
  me.engine
}

/// Allocate a fresh local variable pair `(writer, reader)` in this agent's heap — used
/// to materialise the input-channel streams the booted agent goal reads (Dart
/// agent_runtime allocates the user / net input streams this way).
pub fn alloc_local(me: MadEngine) -> #(MadEngine, Int, Int) {
  let #(heap, writer, reader) = heap.allocate_variable(scheduler.heap(me.engine))
  #(MadEngine(..me, engine: scheduler.set_heap(me.engine, heap)), writer, reader)
}

/// Enqueue a fresh boot goal for `procedure` at `entry_pc` with argument registers
/// `regs` (delegates to `scheduler.boot`). Returns the engine and the minted goal id.
pub fn boot(
  me: MadEngine,
  procedure: String,
  entry_pc: Int,
  regs: XRegs,
) -> #(MadEngine, Int) {
  let #(engine, id) = scheduler.boot(me.engine, procedure, entry_pc, regs)
  #(MadEngine(..me, engine: engine), id)
}

/// One madGLP step: Reduce one goal with W_p injected, then Send (drain M_p). Returns
/// the advanced engine, the reduction outcome, and this step's outgoing message set
/// (the drained M_p — spec §8.2). W_p persists in the returned `MadEngine`.
pub fn step(me: MadEngine, reduction_budget: Int) -> #(MadEngine, StepOutcome, List(Message)) {
  let mad_in = MadState(w_p: me.w_p, m_p: [], mad_spawns: [])
  let #(engine, outcome, mad_out) =
    scheduler.step_mad(me.engine, reduction_budget, mad_in)
  #(
    MadEngine(..me, engine: engine, w_p: mad_out.w_p),
    outcome,
    mad_out.m_p,
  )
}

// ── Receive transaction (spec §8.3) ────────────────────────────────────────────
//
// Process one arriving assignment message `name := value`. Three cases keyed by the
// message's global name (Dart `handleMadAssignment`):
//   - `_w(p,0)` (serializer, i=0): a cold-call to p's network input. Extract the
//     content T↑ from `[T↑ | _w(p,0)]`, Localize it, extend the network-input stream
//     `N_p := [T_p↓ | N'_p]`, and update (never remove) the index-0 entry to N'_p.
//   - `_w(p,i)` (i>0): p globalized a writer; look the entry up by its OWN index i,
//     Localize the value, bind the writer, and REMOVE the entry.
//   - `_r(p,i)`: q localized this reader name; SEARCH for the entry by (p, i),
//     Localize the value, bind the writer, and REMOVE the entry.
// A missing entry is surfaced (not silently absorbed) — duplicate-delivery dedup is
// the reliability sublayer (T052), out of the spec-v5.3-PURE Phase-A scope.

/// Receive one arriving assignment `name := value` (spec §8.3). `Error` names a
/// missing entry / bind conflict / unwired `global_send/3` — never a silent drop.
pub fn receive(
  me: MadEngine,
  name: GlobalName,
  value: Term,
) -> Result(MadEngine, String) {
  case name {
    WriterName(_, 0) -> receive_serializer(me, value)
    WriterName(_, index) -> receive_writer(me, index, value)
    ReaderName(agent, index) -> receive_reader(me, agent, index, value)
  }
}

/// Serializer cold-call `_w(p,0) := [T↑ | _w(p,0)]` (spec §8.3 serializer case).
fn receive_serializer(me: MadEngine, value: Term) -> Result(MadEngine, String) {
  case global_writers_table.serializer_addr(me.w_p) {
    None -> Error("madGLP Receive: serializer entry not initialized at index 0")
    Some(current_writer) -> {
      // Extract the content T↑ from the list cell `[T↑ | _w(p,0)]`; the tail (the
      // reused serializer name) is ignored (Dart _handleSerializerAssignment).
      let content = case value {
        StructTerm(".", [head, _tail]) -> head
        _ -> value
      }
      use #(engine, w_p, content) <- result.try(localize_value(
        me.engine,
        me.w_p,
        content,
      ))
      // Fresh stream continuation `N'_p`, extend `N_p := [T_p↓ | N'_p?]`.
      let #(heap, fresh_writer, fresh_reader) =
        heap.allocate_variable(scheduler.heap(engine))
      let engine = scheduler.set_heap(engine, heap)
      let list_cell = cons(content, VarRef(fresh_reader))
      use engine <- result.try(scheduler.bind_and_wake(
        engine,
        current_writer,
        list_cell,
      ))
      // Update (never remove) the permanent index-0 entry to the fresh writer.
      let w_p = global_writers_table.update_serializer(w_p, fresh_writer)
      Ok(MadEngine(..me, engine: engine, w_p: w_p))
    }
  }
}

/// Globalize-writer assignment `_w(p,i) := T↑`, i>0 (spec §8.3 writer case).
fn receive_writer(me: MadEngine, index: Int, value: Term) -> Result(MadEngine, String) {
  case global_writers_table.lookup(me.w_p, index) {
    Error(_) ->
      Error(
        "madGLP Receive: no GlobalizeEntry at index "
        <> int.to_string(index)
        <> " for _w(p," <> int.to_string(index) <> ")",
      )
    Ok(entry) -> {
      use #(engine, w_p, value) <- result.try(localize_value(
        me.engine,
        me.w_p,
        value,
      ))
      use engine <- result.try(scheduler.bind_and_wake(
        engine,
        entry.writer_addr,
        value,
      ))
      let w_p = global_writers_table.remove_globalize(w_p, index)
      Ok(MadEngine(..me, engine: engine, w_p: w_p))
    }
  }
}

/// Localize-reader assignment `_r(p,i) := T↑` (spec §8.3 reader case). The entry is
/// found by SEARCHING for (remote agent p, remote index i).
fn receive_reader(
  me: MadEngine,
  agent: Term,
  index: Int,
  value: Term,
) -> Result(MadEngine, String) {
  case global_writers_table.find_localize(me.w_p, agent, index) {
    Error(_) ->
      Error(
        "madGLP Receive: no LocalizeEntry for _r(p," <> int.to_string(index) <> ")",
      )
    Ok(entry) -> {
      use #(engine, w_p, value) <- result.try(localize_value(
        me.engine,
        me.w_p,
        value,
      ))
      use engine <- result.try(scheduler.bind_and_wake(
        engine,
        entry.writer_addr,
        value,
      ))
      let w_p = global_writers_table.remove_localize(w_p, agent, index)
      Ok(MadEngine(..me, engine: engine, w_p: w_p))
    }
  }
}

/// Localize the nested global names in an arriving `value` (spec §8.3 "Localize T↑"):
/// allocate fresh local pairs (threading the heap), add W_p entries for `_r` names,
/// enqueue `global_send` goals for `_w` names, and substitute the local vars back in.
/// A value with no global names is returned as-is (no allocation). Returns the
/// heap-advanced engine, the updated W_p, and the localized value.
fn localize_value(
  engine: Engine,
  w_p: WritersTable,
  value: Term,
) -> Result(#(Engine, WritersTable, Term), String) {
  case localize.extract_global_names(value) {
    [] -> Ok(#(engine, w_p, value))
    names -> {
      let #(heap, w_p, result) =
        localize.localize(names, scheduler.heap(engine), w_p)
      let engine = scheduler.set_heap(engine, heap)
      use engine <- result.try(scheduler.enqueue_global_sends(engine, result.spawns))
      let value = localize.localize_term(value, names, result)
      Ok(#(engine, w_p, value))
    }
  }
}
