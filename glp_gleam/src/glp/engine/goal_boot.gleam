//// glp/engine/goal_boot — build a boot goal's argument registers from its AST
//// (feature 050, T029 Slice 2).
////
//// Faithful port of the Dart REPL goal setup (glp_runtime/lib/engine/glp_engine.dart
//// `_setupArgument` :926, `_buildStructTerm` :1028, `_buildListTerm` :1142): materialise
//// each goal argument into the heap and produce the positional X-register file the
//// scheduler boots on, plus the ordered `query_var_writers` the result envelope reports.
////
//// MVP scope (restart doc 2026-07-12): single-atom goals only (Dart
//// `_setupConjunctionArg`/conjunction path is DEFERRED to the REPL). Argument shapes:
//// VarTerm / ConstTerm / StructTerm (with nested structs) / proper lists (lists-of-consts,
//// nested lists, struct/var elements).
////
//// UPDATED 2026-09-04 (feature 101 — front-end goal-term acceptance completeness):
////   - An anonymous `_` in argument position is now ACCEPTED at every position a named
////     variable is (top-level argument, structure argument, list element, list tail).
////     Engineer ruling R-3: this is COMPLETENESS, not a §1.14 language change — `_` was
////     already handled by the parser, SRSW checker, type checker and compiler in every
////     runtime; only goal-argument materialisation omitted it. Dart failed at four
////     positions (eight code sites); this module refused loudly instead, which is why it
////     never returned a wrong answer. `_?` (anonymous READER) remains INVALID — the
////     language permits only anonymous writers (manual §9.1) — with a legible message.
////   - An improper-list tail is still REFUSED, and that refusal was always correct: Dart
////     and C# silently coerced it to nil and ANSWERED THE GOAL, so `[a|foo]` returned
////     byte-identically to `[a|[]]`. This port was the only runtime that never produced
////     that wrong answer, and Dart/C# have now been brought up to this behaviour rather
////     than the reverse. RULED 2026-09-04 (Q-101-02): the refusal is not a placeholder for a
////     pending decision — an improper tail in a goal term is PERMANENTLY INVALID and this
////     refusal IS the specification. Scope is goal terms only; FR-012 is unchanged.
////
//// STILL DEFERRED, surfaced LOUDLY (never a wrong result): the conjunction path. Feature
//// 101 FR-008a makes this a DECLARED, TESTED divergence rather than a silent one —
//// parity with Dart/C# is bounded to shapes all three runtimes can express (SC-003a).
////
//// The Dart runtime threads a MUTABLE heap + maps; here the equivalent state is threaded
//// immutably through a `BootState` (heap + name→writer table + ordered writer list).
////
//// The two-builder asymmetry is Dart's, ported verbatim: `_setupArgument` /
//// `_buildStructTerm` MATERIALISE a const/struct/list arg (allocate → bind the writer →
//// pass `VarRef(reader)`), whereas `_buildListTerm` places a const head INLINE. Only bare
//// variables are placed directly (writer if produced, reader if consumed).

import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/option.{None, Some}
import gleam/result
import glp/bytecode/program.{type XRegs}
import glp/parser/ast
import glp/runtime/heap.{type Heap}
import glp/runtime/terms

/// The threaded goal-boot state: the heap being populated, the name→writer-id table
/// (`varNameToId` — first occurrence of a variable name allocates its pair), and the
/// query-variable writers in first-occurrence order, writers only (the parity invariant —
/// order is preserved, never a map order; Dart `queryVarWriters` insertion order).
type BootState {
  BootState(
    heap: Heap,
    var_name_to_id: Dict(String, Int),
    query_var_writers: List(#(String, Int)),
  )
}

/// The result of booting a goal: the populated heap, the positional argument registers,
/// and the ordered query-variable writers (name → writer addr) for the result envelope.
pub type BootResult {
  BootResult(heap: Heap, regs: XRegs, query_var_writers: List(#(String, Int)))
}

/// Materialise `atom`'s arguments into `heap`, returning the boot registers + ordered
/// query writers. `Error(reason)` names a deferred/unsupported argument shape (surfaced,
/// never guessed) — the facade maps it to a failed result.
pub fn setup_goal(heap: Heap, atom: ast.Atom) -> Result(BootResult, String) {
  let state = BootState(heap, dict.new(), [])
  use #(state, regs) <- result.try(setup_args(
    state,
    atom.args,
    0,
    program.new_regs(),
  ))
  Ok(BootResult(state.heap, regs, state.query_var_writers))
}

/// The result of booting a CONJUNCTION of goals over ONE shared heap + variable
/// environment (Dart `_runConjunction` / `_setupConjunctionArg`, glp_engine.dart:612,
/// :977). A variable NAME shared across the goals — e.g. `P` produced in goal 1 and
/// consumed as `P?` in goal 2 — resolves to the SAME heap pair, so the goals
/// communicate exactly as a clause-body conjunction does. `goal_regs` are the per-goal
/// positional argument registers in goal order (one `XRegs` per goal); `query_var_writers`
/// accumulate in first-occurrence order ACROSS all goals (the parity invariant — Dart's
/// single shared `queryVarWriters`/`varNameToId` maps threaded through every goal).
pub type ConjunctionBoot {
  ConjunctionBoot(
    heap: Heap,
    goal_regs: List(XRegs),
    query_var_writers: List(#(String, Int)),
  )
}

/// Materialise a list of conjunction goals over one shared heap + variable environment
/// (Dart `_runConjunction`'s goal loop). Each atom's arguments are set up with the SAME
/// `_setupArgument` builders the single-goal path uses (Dart's `_setupConjunctionArg` is
/// byte-for-byte the same var/const/struct/list logic), but the `BootState` — heap,
/// name→writer table, and query-writer list — is THREADED across every goal instead of
/// reset per goal. `Error(reason)` names the first deferred/unsupported argument shape.
pub fn setup_goals(
  heap: Heap,
  atoms: List(ast.Atom),
) -> Result(ConjunctionBoot, String) {
  let state = BootState(heap, dict.new(), [])
  use #(state, regs_rev) <- result.try(setup_goals_loop(state, atoms, []))
  Ok(ConjunctionBoot(
    state.heap,
    list.reverse(regs_rev),
    state.query_var_writers,
  ))
}

fn setup_goals_loop(
  state: BootState,
  atoms: List(ast.Atom),
  acc: List(XRegs),
) -> Result(#(BootState, List(XRegs)), String) {
  case atoms {
    [] -> Ok(#(state, acc))
    [atom, ..rest] -> {
      use #(state, regs) <- result.try(setup_args(
        state,
        atom.args,
        0,
        program.new_regs(),
      ))
      setup_goals_loop(state, rest, [regs, ..acc])
    }
  }
}

fn setup_args(
  state: BootState,
  args: List(ast.Term),
  index: Int,
  regs: XRegs,
) -> Result(#(BootState, XRegs), String) {
  case args {
    [] -> Ok(#(state, regs))
    [arg, ..rest] -> {
      use #(state, slot) <- result.try(setup_argument(state, arg))
      setup_args(state, rest, index + 1, program.set_reg(regs, index, slot))
    }
  }
}

/// A top-level goal argument (Dart `_setupArgument`): a bare variable is placed directly
/// (writer / reader by mode); a const / struct / list is MATERIALISED — allocate a fresh
/// variable, bind its writer to the built value, and pass the paired reader.
fn setup_argument(
  state: BootState,
  arg: ast.Term,
) -> Result(#(BootState, terms.Term), String) {
  case arg {
    ast.VarTerm(name, is_reader, _) -> Ok(resolve_var(state, name, is_reader))
    ast.ConstTerm(value, _) -> materialize(state, terms.ConstTerm(value))
    ast.StructTerm(functor, args, _) -> {
      use #(state, value) <- result.try(build_struct(state, functor, args))
      materialize(state, value)
    }
    ast.ListTerm(head, tail, _) -> {
      use #(state, value) <- result.try(build_list(state, head, tail))
      materialize(state, value)
    }
    // feature 101 FR-001: `_` is accepted at a top-level goal argument.
    ast.UnderscoreTerm(True, _) -> refuse_anonymous_reader("a goal argument")
    ast.UnderscoreTerm(False, _) -> Ok(anonymous_writer(state))
  }
}

/// A struct argument (Dart `_buildStructTerm` arg loop): same materialisation as the top
/// level — a bare variable is direct, everything else is allocated + bound + passed by
/// reader.
fn build_struct_arg(
  state: BootState,
  arg: ast.Term,
) -> Result(#(BootState, terms.Term), String) {
  case arg {
    ast.ConstTerm(value, _) -> materialize(state, terms.ConstTerm(value))
    ast.VarTerm(name, is_reader, _) -> Ok(resolve_var(state, name, is_reader))
    ast.StructTerm(functor, args, _) -> {
      use #(state, value) <- result.try(build_struct(state, functor, args))
      materialize(state, value)
    }
    ast.ListTerm(head, tail, _) ->
      case ast.is_nil(arg) {
        True -> materialize(state, terms.nil())
        False -> {
          use #(state, value) <- result.try(build_list(state, head, tail))
          materialize(state, value)
        }
      }
    // feature 101 FR-001: `_` is accepted inside a structure in a goal.
    ast.UnderscoreTerm(True, _) ->
      refuse_anonymous_reader("a structure argument")
    ast.UnderscoreTerm(False, _) -> Ok(anonymous_writer(state))
  }
}

/// Build a struct value (Dart `_buildStructTerm`): materialise each argument, then wrap in
/// a runtime `StructTerm`. The result is a value to bind (the caller materialises it).
fn build_struct(
  state: BootState,
  functor: String,
  args: List(ast.Term),
) -> Result(#(BootState, terms.Term), String) {
  use #(state, arg_terms) <- result.try(build_struct_args(state, args, []))
  Ok(#(state, terms.StructTerm(functor, arg_terms)))
}

fn build_struct_args(
  state: BootState,
  args: List(ast.Term),
  acc: List(terms.Term),
) -> Result(#(BootState, List(terms.Term)), String) {
  case args {
    [] -> Ok(#(state, list.reverse(acc)))
    [arg, ..rest] -> {
      use #(state, term) <- result.try(build_struct_arg(state, arg))
      build_struct_args(state, rest, [term, ..acc])
    }
  }
}

/// Build a list value (Dart `_buildListTerm`): a cons chain over `.`/nil. The Gleam AST
/// carries `[h]` as `ListTerm(Some(h), None)` (a `None` tail is the nil terminator) and
/// `[h|t]` as `ListTerm(Some(h), Some(t))`. Const heads are INLINE (Dart's asymmetry with
/// `_buildStructTerm`); the tail is a list (recurse) or a bare var; any other tail is the
/// Dart `ConstTerm(null)` void case — surfaced, never invented.
fn build_list(
  state: BootState,
  head: option.Option(ast.Term),
  tail: option.Option(ast.Term),
) -> Result(#(BootState, terms.Term), String) {
  case head, tail {
    None, None -> Ok(#(state, terms.nil()))
    Some(h), None -> {
      use #(state, head_term) <- result.try(build_list_head(state, h))
      Ok(#(state, terms.cons(head_term, terms.nil())))
    }
    Some(h), Some(t) -> {
      use #(state, head_term) <- result.try(build_list_head(state, h))
      use #(state, tail_term) <- result.try(build_list_tail(state, t))
      Ok(#(state, terms.cons(head_term, tail_term)))
    }
    None, Some(_) -> Error("goal-boot: malformed list term (tail without head)")
  }
}

fn build_list_head(
  state: BootState,
  head: ast.Term,
) -> Result(#(BootState, terms.Term), String) {
  case head {
    ast.ConstTerm(value, _) -> Ok(#(state, terms.ConstTerm(value)))
    ast.VarTerm(name, is_reader, _) -> Ok(resolve_var(state, name, is_reader))
    ast.ListTerm(h, t, _) -> build_list(state, h, t)
    ast.StructTerm(functor, args, _) -> build_struct(state, functor, args)
    // feature 101 FR-001: `_` is accepted as a list element in a goal.
    ast.UnderscoreTerm(True, _) -> refuse_anonymous_reader("a list element")
    ast.UnderscoreTerm(False, _) -> Ok(anonymous_writer(state))
  }
}

fn build_list_tail(
  state: BootState,
  tail: ast.Term,
) -> Result(#(BootState, terms.Term), String) {
  case tail {
    ast.ListTerm(h, t, _) -> build_list(state, h, t)
    ast.VarTerm(name, is_reader, _) -> Ok(resolve_var(state, name, is_reader))
    // feature 101 FR-001: `_` is a legal list tail — a writer nobody reads.
    ast.UnderscoreTerm(False, _) -> Ok(anonymous_writer(state))
    ast.UnderscoreTerm(True, _) -> refuse_anonymous_reader("a list tail")
    // feature 101 FR-005: an improper tail is REFUSED, never silently coerced.
    // Gleam already refused here and was the ONLY runtime that never returned a
    // wrong answer for this shape — Dart and C# discarded the tail and answered.
    // The refusal stays; only the wording changes, to name what was typed (FR-006).
    // RULED 2026-09-04 (Q-101-02): an improper tail in a GOAL TERM is permanently
    // invalid and this refusal IS the specification. No longer an open §1.14 question.
    other ->
      Error(
        "goal-boot: list tail is neither a list nor a variable: "
        <> ast.term_to_string(other)
        <> " — the goal was not run. A list must end in [] or a variable.",
      )
  }
}

// ── shared helpers ───────────────────────────────────────────────────────────

/// Resolve a variable occurrence (Dart var branch, shared by every builder): the first
/// occurrence of a name allocates its writer/reader pair and (if produced) records the
/// writer as a query variable; later occurrences reuse it. A produced occurrence yields
/// the writer, a consumed occurrence the reader (`X` vs `X?`).
/// Materialise an anonymous variable `_` in a goal term (feature 101, FR-001/FR-003/FR-004).
///
/// Mirrors Dart `_anonymousGoalWriter`. Returns the WRITER half: the manual (§9.1) defines
/// `_` as "a fresh writer with no paired reader, so that a value assigned to it is
/// discarded". A reader would suspend forever on a writer that does not exist.
///
/// Deliberately does NOT extend `query_var_writers` — `_` has no name to report a binding
/// against (FR-004) — and does NOT touch `var_name_to_id`, so two occurrences of `_` can
/// never alias one another (FR-003). Both hold by construction, not by filtering later.
fn anonymous_writer(state: BootState) -> #(BootState, terms.Term) {
  let #(h, writer_id, _reader_id) = heap.allocate_variable(state.heap)
  #(
    BootState(h, state.var_name_to_id, state.query_var_writers),
    terms.VarRef(writer_id),
  )
}

/// `_?` is not a valid goal term: the language permits only anonymous WRITERS
/// (manual §9.1). Feature 101 does not change that (FR-012) — it only makes the
/// refusal name what the programmer typed instead of an internal class (FR-006).
fn refuse_anonymous_reader(position: String) -> Result(a, String) {
  Error(
    "goal-boot: anonymous reader `_?` is not a valid term in "
    <> position
    <> " — an anonymous variable is a writer nobody reads, so `_?` reads nothing. "
    <> "Use `_` to discard a value, or a named variable to keep it.",
  )
}

fn resolve_var(
  state: BootState,
  name: String,
  is_reader: Bool,
) -> #(BootState, terms.Term) {
  case dict.get(state.var_name_to_id, name) {
    Ok(writer_id) -> {
      let addr = case is_reader {
        True -> heap.paired_reader(state.heap, writer_id)
        False -> writer_id
      }
      #(state, terms.VarRef(addr))
    }
    Error(_) -> {
      let #(h, writer_id, reader_id) = heap.allocate_variable(state.heap)
      let var_name_to_id = dict.insert(state.var_name_to_id, name, writer_id)
      let query_var_writers = case is_reader {
        True -> state.query_var_writers
        False -> list.append(state.query_var_writers, [#(name, writer_id)])
      }
      let addr = case is_reader {
        True -> reader_id
        False -> writer_id
      }
      #(BootState(h, var_name_to_id, query_var_writers), terms.VarRef(addr))
    }
  }
}

/// Materialise a built value: allocate a fresh variable, bind its writer to `value`, and
/// return the paired reader (Dart allocate + bindWriter{Const,Struct} + `VarRef(readerId)`).
/// The fresh writer is unbound, so the bind cannot fail in correct use — a heap error is a
/// broken invariant, surfaced loudly, never swallowed.
fn materialize(
  state: BootState,
  value: terms.Term,
) -> Result(#(BootState, terms.Term), String) {
  let #(h, writer_id, reader_id) = heap.allocate_variable(state.heap)
  case heap.bind_writer(h, writer_id, value) {
    Ok(#(h2, _woken)) ->
      Ok(#(BootState(..state, heap: h2), terms.VarRef(reader_id)))
    Error(e) ->
      Error(
        "goal-boot: binding a fresh argument writer failed: " <> heap_error(e),
      )
  }
}

fn heap_error(e: heap.HeapError) -> String {
  case e {
    heap.WriterToWriter(a, b) ->
      "writer↔writer(" <> int.to_string(a) <> "," <> int.to_string(b) <> ")"
    heap.AlreadyBound(a) -> "already-bound(" <> int.to_string(a) <> ")"
    heap.NotAWriter(a) -> "not-a-writer(" <> int.to_string(a) <> ")"
    heap.Cycle(a) -> "cycle(" <> int.to_string(a) <> ")"
  }
}
