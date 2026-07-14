//// glp/mad/global_name — madGLP global variable names `_w(p,i)` / `_r(p,i)`
//// (feature 050, T050.A0).
////
//// Faithful port of the global-name vocabulary in docs/ma/madGLP-spec.md §2/§15 and
//// the Dart oracle glp_runtime/lib/multiagent. Global names appear ONLY in
//// inter-agent messages, NEVER in a resolvent (spec §2). The `_w`/`_r` struct shape
//// (functor `_w`/`_r`, arity 2) is load-bearing: verified in
//// glp_runtime/lib/runtime/body_kernels.dart, `'_send'` ABORTS unless its global-name
//// argument is a `_w/2` or `_r/2` struct.
////
//// The WriterName/ReaderName POLARITY is load-bearing for Receive routing (spec
//// §5/§8): `_w(p,i)` — a writer globalized at p (p is the RECEIVER; entry at p's own
//// index i) vs `_r(p,i)` — a reader globalized at p (p keeps the writer, sends
//// forward; the localizer searches by remote agent+index). A flat id would lose it
//// (3rtask run 20260714T072542Z-a84b REFUTE eac13851).
////
//// `agent` is the ground agent-id term p ∈ Π (spec §2: `String ; Integer ; peer(...)`);
//// `index` is the ground index i ∈ ℕ allocated by p.

import glp/runtime/terms.{type Term, ConstInt, ConstTerm, StructTerm}

/// A madGLP global variable name. Polarity distinguishes the two link directions
/// (spec §2).
pub type GlobalName {
  /// `_w(p, i)` — a writer globalized at agent p.
  WriterName(agent: Term, index: Int)
  /// `_r(p, i)` — a reader globalized at agent p.
  ReaderName(agent: Term, index: Int)
}

const writer_functor = "_w"

const reader_functor = "_r"

/// Build the GLP term `_w(p,i)` / `_r(p,i)` — functor `_w`/`_r`, arity 2, the exact
/// shape body_kernels.dart `'_send'` requires of its global-name argument.
pub fn to_term(name: GlobalName) -> Term {
  case name {
    WriterName(agent, index) ->
      StructTerm(writer_functor, [agent, ConstTerm(ConstInt(index))])
    ReaderName(agent, index) ->
      StructTerm(reader_functor, [agent, ConstTerm(ConstInt(index))])
  }
}

/// Parse a ground `_w(p,i)` / `_r(p,i)` term back to a GlobalName. `Error(Nil)` for
/// anything that is not a two-arg `_w`/`_r` struct with an integer index.
pub fn of_term(term: Term) -> Result(GlobalName, Nil) {
  case term {
    StructTerm(f, [agent, ConstTerm(ConstInt(index))]) if f == writer_functor ->
      Ok(WriterName(agent, index))
    StructTerm(f, [agent, ConstTerm(ConstInt(index))]) if f == reader_functor ->
      Ok(ReaderName(agent, index))
    _ -> Error(Nil)
  }
}

/// True if this is a `_w(p,i)` (writer-globalized) name.
pub fn is_writer(name: GlobalName) -> Bool {
  case name {
    WriterName(_, _) -> True
    ReaderName(_, _) -> False
  }
}
