//// glp/mad/localize — madGLP Localize `T_q↓` (spec §5.2), host-level term traversal
//// over heap + W_p + counter (feature 050, T050.A1).
////
//// Faithful port of glp_runtime/lib/multiagent/mad_helpers.dart (`extractGlobalNames`,
//// `localize`, `localizeTermWithResult`). Outcome — the fresh local pairs allocated,
//// the W_p entries minted, the shared counter advanced (via `add_localize`), the
//// `global_send` spawns emitted, and the local VarRefs substituted back into the term
//// — is byte-identical to the Dart oracle. The Dart passes a mutating
//// `freshAddrAllocator`; Gleam threads the immutable `heap` through `allocate_variable`
//// instead.
////
//// Spec §5.2 [Localize] — for each global name in `T_p↑` (given q=local, p=remote):
////   - `_w(p,i)` → fresh pair `(Y_q, Y_q?)`, replace with Y_q (the WRITER), spawn
////     `global_send(Y_q?, _w(p,i), p)`, NO entry (q gets the writer, sends to p).
////   - `_r(p,i)` → fresh pair `(Z_q, Z_q?)`, add entry `(Z_q, p, i)`, replace with
////     Z_q? (the READER), NO spawn (q is the RECEIVER on this link).

import gleam/dict
import gleam/list
import glp/mad/global_name.{type GlobalName, ReaderName, WriterName, of_term}
import glp/mad/globalize.{type Spawn, Spawn}
import glp/mad/global_writers_table.{type WritersTable, add_localize}
import glp/runtime/heap.{type Heap, allocate_variable}
import glp/runtime/terms.{type Term, StructTerm, VarRef}

/// A fresh local variable pair allocated during localization (spec §5.2). Mirrors the
/// Dart `FreshPair`.
pub type FreshPair {
  FreshPair(writer_addr: Int, reader_addr: Int)
}

/// The effects of localizing a global-name list (spec §5.2). Mirrors the Dart
/// `LocalizeResult`: the fresh pairs, the per-name substitution choice
/// (`False` → writer Y_q for `_w`, `True` → reader Z_q? for `_r`), and the
/// `global_send` spawns. Positionally aligned with the input `names`.
pub type LocalizeResult {
  LocalizeResult(
    fresh_pairs: List(FreshPair),
    use_reader: List(Bool),
    spawns: List(Spawn),
  )
}

/// Extract the `_w(p,i)`/`_r(p,i)` global names from `term`, in order of occurrence
/// (spec §5.2 operates "for each global name in T_p↑"). A matched global name is a
/// LEAF — its `_w`/`_r` struct args (agent, index) are NOT descended into. Mirrors
/// the Dart `extractGlobalNames` / `_extractGlobalNamesRecursive`.
pub fn extract_global_names(term: Term) -> List(GlobalName) {
  case term {
    StructTerm(_, args) ->
      case of_term(term) {
        Ok(name) -> [name]
        Error(_) -> list.flat_map(args, extract_global_names)
      }
    _ -> []
  }
}

/// Localize a list of global names: for each, allocate a fresh local pair (threading
/// the immutable heap), and either add a W_p entry (`_r`) or emit a `global_send`
/// spawn (`_w`) (spec §5.2). Mirrors the Dart `localize` with the heap threaded in
/// place of the `freshAddrAllocator` callback.
pub fn localize(
  names: List(GlobalName),
  heap: Heap,
  table: WritersTable,
) -> #(Heap, WritersTable, LocalizeResult) {
  let #(h, t, fresh_rev, use_rev, spawns_rev) =
    list.fold(names, #(heap, table, [], [], []), fn(acc, gn) {
      let #(h, t, fresh, use_r, spawns) = acc
      let #(h2, writer_addr, reader_addr) = allocate_variable(h)
      let pair = FreshPair(writer_addr, reader_addr)
      case gn {
        WriterName(agent, _) -> {
          // _w(p,i): use writer Y_q, spawn global_send(Y_q?, _w(p,i), p), NO entry.
          let spawn = Spawn(watch_addr: writer_addr, name: gn, dest: agent)
          #(h2, t, [pair, ..fresh], [False, ..use_r], [spawn, ..spawns])
        }
        ReaderName(agent, index) -> {
          // _r(p,i): entry (Z_q, p, i), use reader Z_q?, NO spawn. Created
          // exactly once (spec §13) — a duplicate here is a caller bug.
          let assert Ok(t2) = add_localize(t, writer_addr, agent, index)
          #(h2, t2, [pair, ..fresh], [True, ..use_r], spawns)
        }
      }
    })
  #(
    h,
    t,
    LocalizeResult(
      list.reverse(fresh_rev),
      list.reverse(use_rev),
      list.reverse(spawns_rev),
    ),
  )
}

/// Substitute each `_w(p,i)`/`_r(p,i)` global name in `term` with its fresh local
/// `VarRef` (writer addr for `_w`, reader addr for `_r`, per `result.use_reader`)
/// (spec §5.2). `names` and `result` are positionally aligned (as produced by
/// `localize`). Mirrors the Dart `localizeTermWithResult` / `_substituteLocalVars`.
pub fn localize_term(
  term: Term,
  names: List(GlobalName),
  result: LocalizeResult,
) -> Term {
  let mapping =
    list.zip(names, list.zip(result.fresh_pairs, result.use_reader))
    |> list.map(fn(entry) {
      let #(name, #(pair, use_reader)) = entry
      let addr = case use_reader {
        True -> pair.reader_addr
        False -> pair.writer_addr
      }
      #(name, addr)
    })
    |> dict.from_list
  substitute_locals(term, mapping)
}

fn substitute_locals(term: Term, mapping: dict.Dict(GlobalName, Int)) -> Term {
  case term {
    StructTerm(functor, args) ->
      case of_term(term) {
        Ok(name) ->
          case dict.get(mapping, name) {
            Ok(addr) -> VarRef(addr)
            Error(_) ->
              StructTerm(
                functor,
                list.map(args, fn(a) { substitute_locals(a, mapping) }),
              )
          }
        Error(_) ->
          StructTerm(
            functor,
            list.map(args, fn(a) { substitute_locals(a, mapping) }),
          )
      }
    _ -> term
  }
}
