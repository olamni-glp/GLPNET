//// glp/engine/goal_format — render a live goal (procedure + argument registers)
//// as GLP surface syntax over the heap (feature 050, US2 polish — `:trace` line
//// shape + `Event`).
////
//// The reference REPL's trace/goal shape (Dart glp_runtime/lib/runtime/
//// scheduler.dart `_formatGoal`/`_formatTerm` + the `cleanHead`/`cleanBody`
//// arity-stripping in `drainWithStatus`): a goal prints as `name(a0, a1, …)` with
//// the `/arity` label suffix stripped, each argument heap-resolved, an unbound var
//// shown as its writer id `X<id>` (reader-marked `X<id>?`). Distinct from
//// `output_capture` (ground-only, `_output/1`) and `repl/results` (post-envelope
//// codec terms): this renders LIVE runtime terms against the heap, with the
//// reader/writer mark the trace needs.

import gleam/float
import gleam/int
import gleam/list
import gleam/string
import glp/bytecode/program.{type XRegs}
import glp/runtime/heap.{type Heap, Bound, Unbound}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstReal, ConstString, ConstTerm, StructTerm,
  VarRef,
}

/// Render the goal `label` (a `name/arity` procedure label) applied to the first
/// `arity` argument registers, as `name(a0, …)` (or bare `name` for arity 0) — the
/// reference `cleanHead`/`cleanBody` shape.
pub fn format_goal(label: String, regs: XRegs, heap: Heap) -> String {
  let #(name, arity) = split_label(label)
  case arity {
    0 -> name
    _ -> {
      let args =
        indices(arity)
        |> list.map(fn(i) {
          case program.get_reg(regs, i) {
            Ok(t) -> format_term(heap, t)
            Error(_) -> "_"
          }
        })
      name <> "(" <> string.join(args, ", ") <> ")"
    }
  }
}

/// `[0, 1, …, n-1]` (gleam/list has no `range` in this stdlib pin).
fn indices(n: Int) -> List(Int) {
  case n <= 0 {
    True -> []
    False -> list.append(indices(n - 1), [n - 1])
  }
}

/// Split a `name/arity` label into its name and arity (arity 0 if no/!int suffix).
pub fn split_label(label: String) -> #(String, Int) {
  case string.split(label, "/") {
    [name, arity_str] ->
      case int.parse(arity_str) {
        Ok(n) -> #(name, n)
        Error(_) -> #(label, 0)
      }
    _ -> #(label, 0)
  }
}

/// Render a live runtime term against the heap (reference `_formatTerm`): resolve
/// through the heap, an unbound var as `X<id>` / `X<id>?` (reader-marked), lists as
/// `[a, b]` / `[a | X<id>]`, structs as `f(a, b)`.
pub fn format_term(heap: Heap, term: Term) -> String {
  case term {
    ConstTerm(ConstAtom("nil")) -> "[]"
    ConstTerm(ConstAtom(a)) -> a
    ConstTerm(ConstInt(i)) -> int.to_string(i)
    ConstTerm(ConstReal(f)) -> float.to_string(f)
    ConstTerm(ConstString(s)) -> s
    StructTerm(".", [head, tail]) -> format_list(heap, head, tail, [])
    StructTerm(functor, args) ->
      functor
      <> "("
      <> string.join(list.map(args, fn(a) { format_term(heap, a) }), ", ")
      <> ")"
    VarRef(addr) -> format_var(heap, addr)
  }
}

/// An unbound/var slot: resolve; a bound value formats as its term, an unbound var
/// as `X<writer>` (reader-marked `?` from the ORIGINAL address's role tag, Dart
/// `heap.isReader`).
fn format_var(heap: Heap, addr: Int) -> String {
  case heap.deref(heap, addr) {
    Ok(#(_, Bound(v))) -> format_term(heap, v)
    Ok(#(_, Unbound(writer))) -> {
      let mark = case heap.is_reader(heap, addr) {
        True -> "?"
        False -> ""
      }
      "X" <> int.to_string(writer) <> mark
    }
    Error(_) -> "X" <> int.to_string(addr)
  }
}

fn format_list(
  heap: Heap,
  head: Term,
  tail: Term,
  acc_rev: List(String),
) -> String {
  let acc_rev = [format_term(heap, head), ..acc_rev]
  case resolve(heap, tail) {
    ConstTerm(ConstAtom("nil")) -> close(acc_rev, "")
    StructTerm(".", [h, t]) -> format_list(heap, h, t, acc_rev)
    VarRef(addr) -> close(acc_rev, " | " <> format_var(heap, addr))
    _ -> close(acc_rev, "")
  }
}

fn resolve(heap: Heap, term: Term) -> Term {
  case term {
    VarRef(addr) ->
      case heap.deref(heap, addr) {
        Ok(#(_, Bound(v))) -> resolve(heap, v)
        _ -> term
      }
    _ -> term
  }
}

fn close(acc_rev: List(String), tail_suffix: String) -> String {
  "[" <> string.join(list.reverse(acc_rev), ", ") <> tail_suffix <> "]"
}
