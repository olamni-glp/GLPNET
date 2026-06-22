//// hello-glp-term — spike F1 smoke (031-gleam-port-spike, epic `gleam-atomvm`).
////
//// Purpose (FR-004, SC-006): prove the Gleam→BEAM toolchain end-to-end AND give the
//// architectural-fit risk *running* evidence by demonstrating:
////   1. construction of a representative GLP term — a compound/structure term plus an
////      unbound-variable analogue — whose representation is printed/observable;
////   2. EXACTLY ONE unbound→bound transition, observed by a reader, modelled two ways:
////        - PRIMARY: process/state-holder ("logic variable = BEAM process") — a Gleam
////          actor holds the cell; a separate writer process binds it; a separate reader
////          process observes the bound value. This exercises core BEAM message passing,
////          the FCP-concurrency/BEAM-process fit (SC-006), on the most AtomVM-portable
////          substrate.
////        - FUNCTIONAL SIBLING: the same single bind as immutable threaded state, making
////          the mutable-heap-vs-immutability contrast explicit (the old "unbound" value is
////          never mutated; binding yields a NEW value).
////
//// OUT OF SCOPE — deliberately NOT implemented (Assumptions; FR-004; hello-glp-term.contract.md):
//// full unification of two terms, suspension/reactivation SCHEDULING, bytecode execution,
//// any performance measurement. The single bind is the *bounded* mutable-variable demo.

import gleam/erlang/process.{type Subject}
import gleam/int
import gleam/io
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/otp/actor
import gleam/string

// --------------------------------------------------------------------------
// Representative GLP term
// --------------------------------------------------------------------------

/// A minimal but representative GLP term universe: constants, integers, a
/// compound/structure (functor + args), and an unbound logic-variable analogue
/// identified by an integer "address" (the cell id).
pub type Term {
  Atom(name: String)
  Int(value: Int)
  Struct(functor: String, args: List(Term))
  Var(id: Int)
}

/// The representative term: `pair(label, _G0)` — one compound/structure (`pair/2`)
/// and one unbound-variable analogue (`_G0`, id 0). (FR-004, US2 acceptance #2)
pub fn representative_term() -> Term {
  Struct("pair", [Atom("label"), Var(0)])
}

/// Render a term in a documented, WAM-style representation. Unbound variables
/// print as `_G<id>`.
pub fn term_to_string(term: Term) -> String {
  case term {
    Atom(name) -> name
    Int(value) -> int.to_string(value)
    Var(id) -> "_G" <> int.to_string(id)
    Struct(functor, args) ->
      functor
      <> "("
      <> args |> list.map(term_to_string) |> string.join(", ")
      <> ")"
  }
}

/// Display-only dereference of the single bound cell into the term — substitutes
/// `Var(var_id)` with its binding for rendering. This is NOT unification (no
/// two-term matching, no binding propagation); it only reflects the one observed
/// bind into the printed term. (Keeps the smoke within scope.)
fn resolve(term: Term, var_id: Int, binding: Option(Term)) -> Term {
  case term {
    Var(id) ->
      case id == var_id, binding {
        True, Some(value) -> value
        _, _ -> term
      }
    Struct(functor, args) ->
      Struct(functor, list.map(args, fn(arg) { resolve(arg, var_id, binding) }))
    _ -> term
  }
}

fn show_opt(value: Option(Term)) -> String {
  case value {
    None -> "unbound"
    Some(term) -> term_to_string(term)
  }
}

// --------------------------------------------------------------------------
// PRIMARY model: logic variable = BEAM process (process/state-holder)
// --------------------------------------------------------------------------

/// Messages to a logic-variable cell. The cell holds `Option(Term)`
/// (None = unbound). `Bind` performs the one-shot write; `Read` replies with the
/// current binding to a reader.
type CellMsg {
  Bind(value: Term)
  Read(reply: Subject(Option(Term)))
}

/// The cell's message handler. EXACTLY ONE unbound→bound transition is enforced:
/// `Bind` takes effect only while the cell is still unbound; a second `Bind` is a
/// no-op (single-assignment, the SRSW spirit).
fn handle_cell(
  state: Option(Term),
  msg: CellMsg,
) -> actor.Next(Option(Term), CellMsg) {
  case msg {
    Bind(value) ->
      case state {
        None -> actor.continue(Some(value))
        Some(_) -> actor.continue(state)
      }
    Read(reply) -> {
      process.send(reply, state)
      actor.continue(state)
    }
  }
}

/// Synchronous read of the cell's current binding.
fn read_cell(cell: Subject(CellMsg)) -> Option(Term) {
  actor.call(cell, waiting: 1000, sending: Read)
}

/// A separate WRITER process: binds the cell, then signals completion. The
/// synchronous read round-trip guarantees the bind has been applied (the actor
/// processes messages in order) before `done` is signalled — deterministic, no sleeps.
fn writer_process(cell: Subject(CellMsg), value: Term, done: Subject(Nil)) -> Nil {
  process.send(cell, Bind(value))
  let _ = read_cell(cell)
  process.send(done, Nil)
}

/// A separate READER process: observes the cell and reports the value back.
fn reader_process(cell: Subject(CellMsg), out: Subject(Option(Term))) -> Nil {
  process.send(out, read_cell(cell))
}

/// Runs the process/state-holder bind demo. Returns `#(before, after)`:
/// the cell value observed BEFORE the bind (unbound) and AFTER it (bound),
/// each read across a process boundary via message passing.
pub fn process_bind_demo() -> #(Option(Term), Option(Term)) {
  let assert Ok(started) =
    actor.new(None) |> actor.on_message(handle_cell) |> actor.start
  let cell = started.data

  // reader (main) observes the cell BEFORE any bind -> unbound
  let before = read_cell(cell)

  // a separate WRITER process performs the single unbound->bound bind
  let done = process.new_subject()
  let _ = process.spawn(fn() { writer_process(cell, Atom("bound_atom"), done) })
  let _ = process.receive(done, 1000)

  // a separate READER process observes the cell AFTER the bind -> bound
  let out = process.new_subject()
  let _ = process.spawn(fn() { reader_process(cell, out) })
  let after = case process.receive(out, 1000) {
    Ok(value) -> value
    Error(Nil) -> None
  }

  #(before, after)
}

// --------------------------------------------------------------------------
// FUNCTIONAL SIBLING: one bind via immutable threaded state (contrast)
// --------------------------------------------------------------------------

/// A single-cell "heap": None = unbound. The "writer" is a pure function that
/// returns a NEW heap; it does not mutate its input — the immutability contrast
/// with the process model, where the cell's identity persists across the transition.
fn functional_write(_heap: Option(Term), value: Term) -> Option(Term) {
  Some(value)
}

fn functional_read(heap: Option(Term)) -> Option(Term) {
  heap
}

/// Runs the functional sibling. Returns `#(heap0_read, heap1_read)`: reading the
/// original (still-unbound) heap and the new (bound) heap. `heap0` is observed
/// AFTER `heap1` is produced, proving it was never mutated.
pub fn functional_bind_demo() -> #(Option(Term), Option(Term)) {
  let heap0 = None
  let heap1 = functional_write(heap0, Atom("bound_atom"))
  #(functional_read(heap0), functional_read(heap1))
}

// --------------------------------------------------------------------------
// main — prints the observable evidence
// --------------------------------------------------------------------------

pub fn main() -> Nil {
  let term = representative_term()
  io.println("== hello-glp-term : Gleam smoke on Erlang/BEAM ==")
  io.println("representative term       : " <> term_to_string(term))
  io.println("  compound/structure      : pair/2")
  io.println("  unbound-variable        : _G0")

  let #(before, after) = process_bind_demo()
  io.println("")
  io.println("[process/state-holder model: logic variable = BEAM process]")
  io.println("  cell before bind (read by main)     : " <> show_opt(before))
  io.println("  writer process binds _G0            : _G0 := bound_atom")
  io.println("  cell after bind (read by reader)    : " <> show_opt(after))
  io.println(
    "  resolved term                       : "
    <> term_to_string(resolve(term, 0, after)),
  )

  let #(fheap0, fheap1) = functional_bind_demo()
  io.println("")
  io.println("[functional sibling model: immutable threaded state]")
  io.println("  heap0 (unbound)                     : " <> show_opt(fheap0))
  io.println("  heap1 = write(heap0, bound_atom)    : " <> show_opt(fheap1))
  io.println("  heap0 re-read (immutable, unchanged): " <> show_opt(fheap0))
  Nil
}
