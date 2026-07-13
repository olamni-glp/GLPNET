//// glp/engine/output_capture — the `_output/1` program-output seam (feature 050,
//// T034; US2).
////
//// `_output(T)` (Dart body_kernels.dart:756 `outputKernel`) renders a ground term
//// as one line of program output. Ports Dart `formatGroundTerm` (body_kernels.dart:
//// 779): deep-resolve the heap term, then render as GLP surface syntax — lists
//// `[a, b]` (improper tail `[a | b]`, unlike the REPL's `_formatTerm` which drops
//// it — the two Dart formatters differ, each ported faithfully), atoms/numbers/
//// strings bare, structs `f(a, b)`.
////
//// The captured line is threaded out as DATA — `KernelOutcome` → `runner.Reduced`
//// → scheduler → engine — and printed by the REPL (glp/repl), NEVER stuffed into
//// the R4-excluded envelope `captured` field (no live Dart producer / parity
//// oracle for it). This module depends only on the heap + runtime terms, so it
//// introduces no import cycle with the runner/kernels.

import gleam/float
import gleam/int
import gleam/list
import gleam/string
import glp/runtime/heap.{type Heap, Bound}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstReal, ConstString, ConstTerm, StructTerm,
  VarRef,
}

/// Render the `_output/1` argument as the reference kernel prints it (Dart
/// `formatGroundTerm` after `_deepDeref`): resolve VarRefs through the heap at each
/// level, then format. `_output` is guarded ground upstream (self.glp
/// `send_to_user` calls it under `ground(T?)`), so an unresolved var is degenerate
/// (rendered `_`, never reached in a well-formed program).
pub fn format_ground_term(heap: Heap, term: Term) -> String {
  case resolve(heap, term) {
    ConstTerm(ConstAtom("nil")) -> "[]"
    ConstTerm(ConstAtom(a)) -> a
    ConstTerm(ConstInt(i)) -> int.to_string(i)
    ConstTerm(ConstReal(f)) -> float.to_string(f)
    ConstTerm(ConstString(s)) -> s
    StructTerm(".", [head, tail]) -> format_list(heap, head, tail, [])
    StructTerm(functor, args) ->
      functor
      <> "("
      <> string.join(
        list.map(args, fn(a) { format_ground_term(heap, a) }),
        ", ",
      )
      <> ")"
    VarRef(_) -> "_"
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

fn format_list(
  heap: Heap,
  head: Term,
  tail: Term,
  acc_rev: List(String),
) -> String {
  let acc_rev = [format_ground_term(heap, head), ..acc_rev]
  case resolve(heap, tail) {
    ConstTerm(ConstAtom("nil")) -> close(acc_rev, "")
    StructTerm(".", [h, t]) -> format_list(heap, h, t, acc_rev)
    other -> close(acc_rev, " | " <> format_ground_term(heap, other))
  }
}

fn close(acc_rev: List(String), tail_suffix: String) -> String {
  "[" <> string.join(list.reverse(acc_rev), ", ") <> tail_suffix <> "]"
}
