//// glp/runtime/terms — the GLP term model (constants, structures, variable refs).
////
//// Faithful port of the data + binding core's term layer (feature 034, F4). The
//// reader/writer ROLE of a `VarRef` is NOT encoded in the term — it is determined
//// solely by the heap cell's tag at `addr` (see glp/runtime/heap.is_writer /
//// is_reader). No `reader == writer + 1` arithmetic anywhere (FR-002 / data-model §1).
////
//// Dart source of truth: glp_runtime/lib/runtime/terms.dart

/// A GLP constant — the four ground constant kinds (FR-001). Mirrors the four kinds
/// Dart's `ConstTerm(Object? value)` erases to at the heap level (R-002). Gleam derives
/// structural equality, so "comparable for equality" (FR-001) is free.
pub type Constant {
  ConstAtom(String)
  ConstInt(Int)
  ConstReal(Float)
  ConstString(String)
}

/// A GLP term. Lists are NOT a separate variant — they are the cons/nil structure
/// (R-003), built with `nil`/`cons` below. `MutualRefTerm`/`ModuleTerm` are
/// intentionally excluded from the F4 core set (R-002/R-008).
///
/// `VarRef(a) == VarRef(b)` iff `a == b` — derived structural equality matches the
/// Dart model's overridden `==`/`hashCode` on `addr`.
pub type Term {
  ConstTerm(value: Constant)
  StructTerm(functor: String, args: List(Term))
  VarRef(addr: Int)
}

/// The empty list — the `nil` atom (Dart heap lowering, R-003): `ConstTerm(ConstAtom("nil"))`.
pub fn nil() -> Term {
  ConstTerm(ConstAtom("nil"))
}

/// A non-empty list cell — `StructTerm(".", [head, tail])` (Dart heap lowering, R-003).
pub fn cons(head: Term, tail: Term) -> Term {
  StructTerm(".", [head, tail])
}
