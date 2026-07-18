//// glp/repl/results — render a finished run's ResultEnvelope as reference-REPL
//// text (feature 050, T033; US2).
////
//// ED-1 in-process binding: the REPL consumes the SAME `ResultEnvelope` the 038
//// builder produces for the wire (deep-resolved, canonically ordered), so the
//// in-process and over-the-wire renderings share one source (SC-004 corollary,
//// exercised by T036). This module is the reference REPL's output shape (Dart
//// `glp_runtime/bin/glp_repl.dart` `_formatTerm` + `_printStatus`) over the codec
//// term model — NOT over the live heap. The envelope is already heap-resolved, so
//// formatting is a pure structural print: an unbound query var is its global
//// writer identity `X<id>` (the heap-only reader `?` distinction is outside the
//// ED-1 seam and is reconciled at corpus-parity time, US3).

import gleam/float
import gleam/int
import gleam/list
import gleam/option.{None, Some}
import gleam/string
import glp/codec/result_envelope.{
  type ResultEnvelope, Failed, Success, Suspended,
}
import glp/codec/term_codec.{
  type Term, ConstAtom, ConstInt, ConstReal, ConstString, ConstTerm, GlobalVarId,
  StructTerm, VarRef,
}

/// Format a deep-resolved codec term exactly as the reference REPL prints it
/// (Dart `_formatTerm`): `nil` const → `[]`, atom/int/real/string const printed
/// bare, `'.'/2` chains as list sugar `[a, b]` (unbound tail `[a | X<id>]`,
/// improper non-var tail dropped as the reference does), `f(a, b)` structs, and an
/// unbound var as `X<id>` (its global writer id).
pub fn format_term(term: Term) -> String {
  case term {
    ConstTerm(ConstAtom("nil")) -> "[]"
    ConstTerm(ConstAtom(a)) -> a
    ConstTerm(ConstInt(i)) -> int.to_string(i)
    ConstTerm(ConstReal(f)) -> float.to_string(f)
    ConstTerm(ConstString(s)) -> s
    StructTerm(".", [head, tail]) -> format_list(head, tail, [])
    StructTerm(functor, args) ->
      functor <> "(" <> string.join(list.map(args, format_term), ", ") <> ")"
    VarRef(GlobalVarId(_, id)) -> "X" <> int.to_string(id)
  }
}

/// Walk a cons chain (`'.'/2`), accumulating formatted heads (reversed), then close
/// the list at the terminal. Mirrors the Dart list walk: `nil` closes cleanly, a
/// var tail prints `| X<id>`, any other improper tail is dropped (Dart `break`).
fn format_list(head: Term, tail: Term, acc_rev: List(String)) -> String {
  let acc_rev = [format_term(head), ..acc_rev]
  case tail {
    ConstTerm(ConstAtom("nil")) -> close_list(acc_rev, "")
    StructTerm(".", [h, t]) -> format_list(h, t, acc_rev)
    VarRef(GlobalVarId(_, id)) ->
      close_list(acc_rev, " | X" <> int.to_string(id))
    _ -> close_list(acc_rev, "")
  }
}

fn close_list(acc_rev: List(String), tail_suffix: String) -> String {
  "[" <> string.join(list.reverse(acc_rev), ", ") <> tail_suffix <> "]"
}

/// Render a finished run's envelope as the reference REPL's output block: one
/// `Name = <term>` line per query binding in canonical order (bound bindings, then
/// any unbound query vars as `X<id>`), then the status line (`→ succeeds` /
/// `→ suspended` / `→ failed`), then `Error: <e>` if present. The REPL loop prints
/// each line and a trailing blank line (Dart `runGoal` print block).
pub fn render_outcome(env: ResultEnvelope) -> List(String) {
  let bound_lines =
    list.map(env.resolved_bindings, fn(b) {
      let #(name, term) = b
      name <> " = " <> format_term(term)
    })
  let unbound_lines =
    list.map(env.var_to_writer, fn(v) {
      let #(name, id) = v
      let GlobalVarId(_, local) = id
      name <> " = X" <> int.to_string(local)
    })
  let status_line = case env.status {
    Success -> "→ succeeds"
    Suspended -> "→ suspended"
    Failed -> "→ failed"
  }
  let error_lines = case env.error {
    Some(e) -> ["Error: " <> e]
    None -> []
  }
  list.flatten([bound_lines, unbound_lines, [status_line], error_lines])
}
