//// glp/runtime — the GLP `runtime` subsystem umbrella.
////
//// Stable public entry for downstream features (F5 runner, F6 compiler/loader): it
//// re-exports the runtime kernel's TYPES (via Gleam type aliases) and thin wrapper
//// FUNCTIONS over glp/runtime/{terms,suspension,heap,unify} (R-004). Replaces the F3
//// doc-only placeholder.
////
//// NOTE (R-004 escape hatch): Gleam type aliases re-export a type's name but not its
//// constructors. To pattern-match a result (`Bound`/`Unbound`, `Success`/`Suspend`/
//// `Fail`) or build a term by constructor, import the sub-module directly
//// (`glp/runtime/heap`, `glp/runtime/unify`, `glp/runtime/terms`). The lowercase term
//// constructor helpers below cover the common construction cases.
////
//// Dart source of truth: glp_runtime/lib/runtime/

import glp/runtime/heap
import glp/runtime/suspension
import glp/runtime/terms
import glp/runtime/unify

// ---- Type re-exports (aliases) --------------------------------------------------
pub type Term =
  terms.Term

pub type Constant =
  terms.Constant

pub type Heap =
  heap.Heap

pub type Cell =
  heap.Cell

pub type CellTag =
  heap.CellTag

pub type DerefResult =
  heap.DerefResult

pub type HeapError =
  heap.HeapError

pub type Suspension =
  suspension.Suspension

pub type GoalRef =
  suspension.GoalRef

pub type UnifyOutcome =
  unify.UnifyOutcome

// ---- Term constructor helpers ---------------------------------------------------
pub fn const_atom(name: String) -> Term {
  terms.ConstTerm(terms.ConstAtom(name))
}

pub fn const_int(value: Int) -> Term {
  terms.ConstTerm(terms.ConstInt(value))
}

pub fn const_real(value: Float) -> Term {
  terms.ConstTerm(terms.ConstReal(value))
}

pub fn const_string(value: String) -> Term {
  terms.ConstTerm(terms.ConstString(value))
}

pub fn var_ref(addr: Int) -> Term {
  terms.VarRef(addr)
}

pub fn struct_term(functor: String, args: List(Term)) -> Term {
  terms.StructTerm(functor, args)
}

pub fn nil() -> Term {
  terms.nil()
}

pub fn cons(head: Term, tail: Term) -> Term {
  terms.cons(head, tail)
}

// ---- Heap wrappers --------------------------------------------------------------
pub fn new_heap() -> Heap {
  heap.new()
}

pub fn allocate_variable(h: Heap) -> #(Heap, Int, Int) {
  heap.allocate_variable(h)
}

pub fn is_writer(h: Heap, addr: Int) -> Bool {
  heap.is_writer(h, addr)
}

pub fn is_reader(h: Heap, addr: Int) -> Bool {
  heap.is_reader(h, addr)
}

pub fn is_value(h: Heap, addr: Int) -> Bool {
  heap.is_value(h, addr)
}

pub fn deref(h: Heap, addr: Int) -> Result(#(Heap, DerefResult), HeapError) {
  heap.deref(h, addr)
}

pub fn bind_writer(
  h: Heap,
  writer: Int,
  value: Term,
) -> Result(#(Heap, List(GoalRef)), HeapError) {
  heap.bind_writer(h, writer, value)
}

pub fn bind_writer_to_var(
  h: Heap,
  writer: Int,
  reader: Int,
) -> Result(#(Heap, List(GoalRef)), HeapError) {
  heap.bind_writer_to_var(h, writer, reader)
}

pub fn unify(h: Heap, a: Term, b: Term) -> Result(UnifyOutcome, HeapError) {
  unify.unify(h, a, b)
}

pub fn suspend_on_writer(
  h: Heap,
  writer: Int,
  susp: Suspension,
) -> Result(Heap, HeapError) {
  heap.suspend_on_writer(h, writer, susp)
}
