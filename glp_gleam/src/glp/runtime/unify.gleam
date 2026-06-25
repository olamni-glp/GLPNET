//// glp/runtime/unify — writer-MGU three-valued unification (success / suspend / fail).
////
//// The distinctive GLP semantic: unify two terms → exactly one of success / suspend /
//// fail, binding ONLY writers (never readers, never writer-to-writer), and SUSPENDING
//// (not failing) on a needed unbound reader. No occurs-check (R-008). Reduced to the
//// heap-level term×term form FR-007 specifies (the full HEAD-phase machinery is F5).
////
//// Dart source of truth: glp_runtime/lib/runtime/ (runner HEAD-phase unify; the
//// reader/writer dispatch mirrors heap_fcp.dart `bindVariable`).

import gleam/list
import glp/runtime/heap.{type Heap, type HeapError, Bound, Unbound, WriterToWriter}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{type Term, ConstTerm, StructTerm, VarRef}

/// Three-valued unification outcome (FR-007). `Suspend.on` is the address whose writer a
/// suspension would attach to. `Fail` (an ordinary mismatch) is kept distinct from
/// `HeapError` (a structural violation) — R-005.
pub type UnifyOutcome {
  Success(heap: Heap)
  Suspend(heap: Heap, on: Int)
  Fail
}

/// The role of one side after dereferencing — the writer-MGU distinction deref alone
/// loses. A `VarRef`'s reader/writer role is read from the heap tag at its ORIGINAL
/// address (FR-002), not from the deref terminal.
type Resolved {
  /// A ground value (const or struct; struct args may still hold `VarRef`s).
  RValue(Term)
  /// An unbound WRITER reference — bindable. Holds the terminal writer address.
  RWriter(Int)
  /// An unbound READER reference — not bindable (a consumer). Holds
  /// `#(original_reader_addr, terminal_paired_writer)`.
  RReader(Int, Int)
}

/// Unify two terms (FR-007). Binds only writers; suspends (never fails) on a needed
/// unbound reader; reports WxW as `HeapError` (never `Fail`); no occurs-check.
pub fn unify(h: Heap, a: Term, b: Term) -> Result(UnifyOutcome, HeapError) {
  case resolve(h, a) {
    Error(e) -> Error(e)
    Ok(#(h, ra)) ->
      case resolve(h, b) {
        Error(e) -> Error(e)
        Ok(#(h, rb)) -> unify_resolved(h, ra, rb)
      }
  }
}

/// Deref a term to a value or an unbound variable, recording its writer/reader role from
/// the heap tag at the original address. Threads the (possibly path-compressed) heap.
fn resolve(h: Heap, term: Term) -> Result(#(Heap, Resolved), HeapError) {
  case term {
    VarRef(addr) ->
      case heap.deref(h, addr) {
        Error(e) -> Error(e)
        Ok(#(h, Bound(value))) -> Ok(#(h, RValue(value)))
        Ok(#(h, Unbound(writer))) ->
          case heap.is_writer(h, addr) {
            True -> Ok(#(h, RWriter(writer)))
            False -> Ok(#(h, RReader(addr, writer)))
          }
      }
    _ -> Ok(#(h, RValue(term)))
  }
}

fn unify_resolved(
  h: Heap,
  a: Resolved,
  b: Resolved,
) -> Result(UnifyOutcome, HeapError) {
  case a, b {
    // both ground → structural unification
    RValue(va), RValue(vb) -> unify_values(h, va, vb)

    // a bindable writer vs a ground value → bind the writer (← bindWriter)
    RWriter(wa), RValue(vb) -> bound_success(heap.bind_writer(h, wa, vb))
    RValue(va), RWriter(wb) -> bound_success(heap.bind_writer(h, wb, va))

    // a bindable writer vs an unbound variable → writer→var (← bindWriterToReader)
    RWriter(wa), RReader(rb, _) ->
      bound_success(heap.bind_writer_to_var(h, wa, rb))
    RReader(ra, _), RWriter(wb) ->
      bound_success(heap.bind_writer_to_var(h, wb, ra))

    // writer vs writer → WxW, reported loudly (← bindWriterToWriter), never Fail (SC-004)
    RWriter(wa), RWriter(wb) -> Error(WriterToWriter(wa, wb))

    // a needed unbound reader with nothing to bind → Suspend, never Fail (the canonical
    // GLP correctness point). `on` = the reader's paired writer.
    RReader(_, wa), RValue(_) -> Ok(Suspend(h, wa))
    RValue(_), RReader(_, wb) -> Ok(Suspend(h, wb))
    RReader(_, wa), RReader(_, _) -> Ok(Suspend(h, wa))
  }
}

/// Map a heap bind result to a unify `Success` (the activation list is the F5 caller's
/// concern, not part of the unify verdict).
fn bound_success(
  r: Result(#(Heap, List(GoalRef)), HeapError),
) -> Result(UnifyOutcome, HeapError) {
  case r {
    Ok(#(h, _activations)) -> Ok(Success(h))
    Error(e) -> Error(e)
  }
}

/// Structural unification of two ground-headed values. (A nested `VarRef` in struct args
/// is resolved by the recursive `unify` in `unify_args`; a bare `VarRef` cannot appear
/// here in unify-driven flows — those resolve to `RWriter`/`RReader` first.)
fn unify_values(h: Heap, a: Term, b: Term) -> Result(UnifyOutcome, HeapError) {
  case a, b {
    ConstTerm(ca), ConstTerm(cb) ->
      case ca == cb {
        True -> Ok(Success(h))
        False -> Ok(Fail)
      }
    StructTerm(fa, args_a), StructTerm(fb, args_b) ->
      case fa == fb && list.length(args_a) == list.length(args_b) {
        True -> unify_args(h, args_a, args_b)
        False -> Ok(Fail)
      }
    _, _ -> Ok(Fail)
  }
}

/// Unify struct arguments pairwise, threading the heap; the first non-`Success` (Fail,
/// Suspend, or Error) short-circuits.
fn unify_args(
  h: Heap,
  xs: List(Term),
  ys: List(Term),
) -> Result(UnifyOutcome, HeapError) {
  case xs, ys {
    [], [] -> Ok(Success(h))
    [x, ..xr], [y, ..yr] ->
      case unify(h, x, y) {
        Ok(Success(h)) -> unify_args(h, xr, yr)
        other -> other
      }
    _, _ -> Ok(Fail)
  }
}
