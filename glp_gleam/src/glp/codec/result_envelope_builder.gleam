//// glp/codec/result_envelope_builder — engine→envelope builder + server-side
//// deep-resolve for the Gleam port (feature 038, T022/T023).
////
//// Ports glp_runtime/lib/codec/result_envelope_builder.dart (the Dart source of
//// truth) onto the 034 runtime: it walks the 034 heap (glp/runtime/heap.deref)
//// and maps 034 runtime terms (glp/runtime/terms) to the heap-independent codec
//// term model (glp/codec/term_codec), producing a `ResultEnvelope`.
////
//// Kept in its OWN module (not result_envelope.gleam) so the pure codec stays
//// free of the runtime heap/terms imports — consistent with the ratified C#
//// decision (separate `glp_result_codec_builder` project) and the Dart reference's
//// own file split (`result_envelope_builder.dart`).
////
//// 034 heap specifics vs the Dart reference: `heap.deref(heap, addr)` is
//// ADDRESS-based and THREADS the `Heap` (path compression) — every resolve step
//// returns the (possibly-updated) heap. `deref` yields `Bound(term)` (a runtime
//// terms.Term) or `Unbound(writer)`; a heap error (cycle / writer-to-writer) is
//// surfaced loudly as `DerefFailed`, never masked.
////
//// U1 (no Gleam engine yet): the builder takes `agent_id: String` and the run's
//// `status` + `blocking_readers` as explicit parameters (the Dart/C# engine
//// supplies these from agent context / DrainResult; Gleam supplies them at the
//// call site until the F-series engine lands).

import gleam/int
import gleam/list
import gleam/option
import gleam/result
import glp/codec/result_envelope
import glp/codec/term_codec
import glp/runtime/heap.{type Heap}
import glp/runtime/terms

/// Deep-resolve depth bound — matches the Dart/C# reference (`_ResolveDeepForTrace`).
pub const deep_resolve_depth = 32

/// Loud-fail error surfaced from the builder (heap traversal failed). Mirrors the
/// Dart `ResultCodecException` on an unresolvable heap.
pub type BuildError {
  DerefFailed(heap.HeapError)
}

/// The explicit truncation marker emitted at the depth bound (R5, contract §6) —
/// a normal decodable term, deterministic + parity-stable, never a silent cut.
pub fn truncated_marker() -> term_codec.Term {
  term_codec.StructTerm("$truncated", [])
}

/// Map a 034 runtime constant to the codec Constant model (structural; 034 already
/// distinguishes atom vs string, so the mapping is faithful per-kind).
fn map_const(c: terms.Constant) -> term_codec.Constant {
  case c {
    terms.ConstAtom(a) -> term_codec.ConstAtom(a)
    terms.ConstInt(i) -> term_codec.ConstInt(i)
    terms.ConstReal(f) -> term_codec.ConstReal(f)
    terms.ConstString(s) -> term_codec.ConstString(s)
  }
}

/// Server-side deep-resolve of a 034 runtime term to the heap-independent codec
/// Term, threading the heap. Recurses into StructTerm args to `deep_resolve_depth`
/// (an EXPLICIT `$truncated` marker at the bound); an unbound var → its global
/// writer identity `VarRef(GlobalVarId(agent_id, writer))`.
pub fn deep_resolve(
  h: Heap,
  term: terms.Term,
  agent_id: String,
  depth: Int,
) -> Result(#(Heap, term_codec.Term), BuildError) {
  case depth > deep_resolve_depth {
    True -> Ok(#(h, truncated_marker()))
    False ->
      case term {
        terms.ConstTerm(c) -> Ok(#(h, term_codec.ConstTerm(map_const(c))))
        terms.StructTerm(functor, args) -> {
          use #(h2, rargs) <- result.try(resolve_args(
            h,
            args,
            agent_id,
            depth + 1,
            [],
          ))
          Ok(#(h2, term_codec.StructTerm(functor, rargs)))
        }
        terms.VarRef(addr) ->
          case heap.deref(h, addr) {
            // A bound writer/reader resolves to its value term; resolve that term
            // at the SAME depth (the deref itself is not a structural descent).
            Ok(#(h2, heap.Bound(inner))) ->
              deep_resolve(h2, inner, agent_id, depth)
            Ok(#(h2, heap.Unbound(writer))) ->
              Ok(#(h2, term_codec.VarRef(term_codec.GlobalVarId(agent_id, writer))))
            Error(e) -> Error(DerefFailed(e))
          }
      }
  }
}

fn resolve_args(
  h: Heap,
  args: List(terms.Term),
  agent_id: String,
  depth: Int,
  acc: List(term_codec.Term),
) -> Result(#(Heap, List(term_codec.Term)), BuildError) {
  case args {
    [] -> Ok(#(h, list.reverse(acc)))
    [a, ..rest] -> {
      use #(h2, r) <- result.try(deep_resolve(h, a, agent_id, depth))
      resolve_args(h2, rest, agent_id, depth, [r, ..acc])
    }
  }
}

/// Build the heap-independent `ResultEnvelope` from a finished run (T023).
///
/// `query_var_writers` is name→writer addr in the engine's canonical declaration
/// order (the parity invariant — order is preserved, never a map order). A bound
/// writer → a deep-resolved binding; an unbound writer → a var→writer global-id
/// entry (using the INPUT writer addr, mirroring the Dart reference).
/// `blocking_readers` → the suspended GlobalVarId set (sorted, deduped).
pub fn build_result_envelope(
  h: Heap,
  query_var_writers: List(#(String, Int)),
  status: result_envelope.ExecutionStatus,
  blocking_readers: List(Int),
  agent_id: String,
  captured: BitArray,
  error: option.Option(String),
) -> Result(#(Heap, result_envelope.ResultEnvelope), BuildError) {
  use #(h2, bindings, vars) <- result.try(collect_writers(
    h,
    query_var_writers,
    agent_id,
    [],
    [],
  ))
  let suspended =
    blocking_readers
    |> list.unique
    |> list.sort(int.compare)
    |> list.map(fn(a) { term_codec.GlobalVarId(agent_id, a) })
  Ok(#(
    h2,
    result_envelope.ResultEnvelope(
      status: status,
      resolved_bindings: bindings,
      var_to_writer: vars,
      suspended: suspended,
      captured: captured,
      error: error,
    ),
  ))
}

fn collect_writers(
  h: Heap,
  qvw: List(#(String, Int)),
  agent_id: String,
  bindings_acc: List(#(String, term_codec.Term)),
  vars_acc: List(#(String, term_codec.GlobalVarId)),
) -> Result(
  #(
    Heap,
    List(#(String, term_codec.Term)),
    List(#(String, term_codec.GlobalVarId)),
  ),
  BuildError,
) {
  case qvw {
    [] -> Ok(#(h, list.reverse(bindings_acc), list.reverse(vars_acc)))
    [#(name, writer), ..rest] ->
      case heap.deref(h, writer) {
        Ok(#(h2, heap.Bound(_))) -> {
          use #(h3, t) <- result.try(deep_resolve(
            h2,
            terms.VarRef(writer),
            agent_id,
            0,
          ))
          collect_writers(h3, rest, agent_id, [#(name, t), ..bindings_acc], vars_acc)
        }
        Ok(#(h2, heap.Unbound(_))) ->
          collect_writers(h2, rest, agent_id, bindings_acc, [
            #(name, term_codec.GlobalVarId(agent_id, writer)),
            ..vars_acc
          ])
        Error(e) -> Error(DerefFailed(e))
      }
  }
}
