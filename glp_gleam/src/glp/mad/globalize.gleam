//// glp/mad/globalize — madGLP Globalize `T_p↑` (spec §5.1), host-level term
//// traversal over heap + W_p + counter (feature 050, T050.A1).
////
//// Faithful port of glp_runtime/lib/multiagent/mad_helpers.dart (`globalize`,
//// `globalizeTermWithResult`, plus the caller-side variable collection). Outcome —
//// the global-name terms substituted, the W_p entries minted, the shared counter
//// advanced, and the `global_send` spawns emitted — is byte-identical to the Dart
//// oracle. Only the internal shape differs: the Dart passes a pre-built
//// `List<TermVar>` (each carrying writer+reader addrs) plus a mutating
//// `freshAddrAllocator`; Gleam's `VarRef(addr)` encodes NEITHER role NOR pairing
//// (terms.gleam:4-6), so the writer/reader role and the paired address are read
//// from the immutable heap here (`collect_vars`).
////
//// Spec §5.1 [Globalize] — for each variable Y in T (given p=local, q=remote):
////   - writer Y  → allocate index i, entry `(Y,q)` at i, replace with `_w(p,i)`,
////     NO spawn (p is the RECEIVER; q gets the writer, sends the value back).
////   - reader Y? → allocate index i, replace with `_r(p,i)`, spawn
////     `global_send(Y?, _r(p,i), q)`, NO entry (p keeps the writer, sends forward).
//// Index allocation: single per-agent counter, shared with Localize, starts at 1
//// (0 = serializer), never reused (spec §3.2/§13) — enforced by W_p (T050.A0).

import gleam/dict
import gleam/list
import glp/mad/global_name.{type GlobalName, ReaderName, WriterName, to_term}
import glp/mad/global_writers_table.{
  type WritersTable, add_globalize, allocate_index,
}
import glp/runtime/heap.{
  type Heap, is_reader, is_writer, paired_reader, paired_writer,
}
import glp/runtime/terms.{type Term, StructTerm, VarRef}

/// A local variable occurrence collected from a term for globalization. Mirrors the
/// Dart `TermVar`: a writer or a reader, each carrying BOTH addresses of its pair
/// (the paired address is derived from the heap since `VarRef` does not carry it).
pub type TermVar {
  /// A writer VarRef `Y`; `writer_addr` is the address as it appears in the term.
  WriterVar(writer_addr: Int, reader_addr: Int)
  /// A reader VarRef `Y?`; `reader_addr` is the address as it appears in the term.
  ReaderVar(reader_addr: Int, writer_addr: Int)
}

/// A `global_send` goal to spawn (spec §4 / §5.1 reader branch). `watch_addr` is the
/// WRITER address of the watched pair — the Dart passes `writerAddr` as the onBind
/// key (mad_helpers.dart:206-213), because reactivation keys off the writer. The
/// A2 effectful seam registers these; A1 only emits them.
pub type Spawn {
  Spawn(watch_addr: Int, name: GlobalName, dest: Term)
}

/// The effects of globalizing a variable list: the substituted global names (in
/// order of occurrence) + the `global_send` spawns (spec §5.1). Mirrors the Dart
/// `GlobalizeResult`. The updated W_p is returned alongside (see `globalize`).
pub type GlobalizeResult {
  GlobalizeResult(names: List(GlobalName), spawns: List(Spawn))
}

/// The address of a TermVar AS IT APPEARS in the term (writer for a writer, reader
/// for a reader) — the key used when substituting global names back into the term.
fn term_addr(v: TermVar) -> Int {
  case v {
    WriterVar(writer_addr, _) -> writer_addr
    ReaderVar(reader_addr, _) -> reader_addr
  }
}

/// Collect the writer/reader variables occurring in `term`, classifying each `VarRef`
/// by its heap cell tag and pairing it via the heap (spec §5.1 operates "for each
/// variable Y occurring in T").
///
/// PRECONDITION: `term` is RESOLVED — every `VarRef` is an unbound writer
/// (`WriterCell`) or a reader (`ReaderCell`). Bound writers and ground values must be
/// resolved away by the caller before globalization (as the Dart mad_context does
/// before collecting vars); a `VarRef` to a value cell is not a variable to globalize
/// and is skipped.
pub fn collect_vars(term: Term, heap: Heap) -> List(TermVar) {
  case term {
    VarRef(addr) ->
      case is_writer(heap, addr), is_reader(heap, addr) {
        True, _ -> [WriterVar(writer_addr: addr, reader_addr: paired_reader(heap, addr))]
        _, True -> {
          // A reader always has a local writer (its `ReaderCell` points back).
          let assert Ok(w0) = paired_writer(heap, addr)
          [ReaderVar(reader_addr: addr, writer_addr: w0)]
        }
        _, _ -> []
      }
    StructTerm(_, args) -> list.flat_map(args, fn(a) { collect_vars(a, heap) })
    _ -> []
  }
}

/// Globalize a collected variable list: mint W_p entries for writers, allocate indices
/// + emit `global_send` spawns for readers, and produce the global names in order
/// (spec §5.1). Pure over the W_p (index counter lives in the table, T050.A0). Mirrors
/// the Dart `globalize`.
pub fn globalize(
  vars: List(TermVar),
  local_agent: Term,
  remote_agent: Term,
  table: WritersTable,
) -> #(WritersTable, GlobalizeResult) {
  let #(t, names_rev, spawns_rev) =
    list.fold(vars, #(table, [], []), fn(acc, v) {
      let #(t, names, spawns) = acc
      case v {
        WriterVar(writer_addr, _) -> {
          // Writer: entry (Y,q) at next index i, NO spawn.
          let #(t2, i) = add_globalize(t, writer_addr, remote_agent)
          #(t2, [WriterName(local_agent, i), ..names], spawns)
        }
        ReaderVar(_, writer_addr) -> {
          // Reader: allocate index i, spawn global_send(Y?, _r(p,i), q), NO entry.
          let #(t2, i) = allocate_index(t)
          let name = ReaderName(local_agent, i)
          let spawn = Spawn(watch_addr: writer_addr, name: name, dest: remote_agent)
          #(t2, [name, ..names], [spawn, ..spawns])
        }
      }
    })
  #(t, GlobalizeResult(list.reverse(names_rev), list.reverse(spawns_rev)))
}

/// Substitute each variable's `VarRef` in `term` with its global name term
/// `_w(p,i)`/`_r(p,i)` (spec §5.1). `vars` and `result.names` are positionally
/// aligned (as produced by `globalize`). Mirrors the Dart `globalizeTermWithResult` /
/// `_substituteGlobalNames`.
pub fn globalize_term(
  term: Term,
  vars: List(TermVar),
  result: GlobalizeResult,
) -> Term {
  let mapping =
    list.zip(vars, result.names)
    |> list.map(fn(pair) {
      let #(v, name) = pair
      #(term_addr(v), name)
    })
    |> dict.from_list
  substitute_names(term, mapping)
}

fn substitute_names(term: Term, mapping: dict.Dict(Int, GlobalName)) -> Term {
  case term {
    VarRef(addr) ->
      case dict.get(mapping, addr) {
        Ok(name) -> to_term(name)
        Error(_) -> term
      }
    StructTerm(functor, args) ->
      StructTerm(functor, list.map(args, fn(a) { substitute_names(a, mapping) }))
    _ -> term
  }
}
