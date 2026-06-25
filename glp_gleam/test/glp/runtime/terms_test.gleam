//// US1 (T009) — construct, structurally inspect, and equality-compare all 9 term kinds
//// (SC-001): atom, int, real, string, compound struct, empty list, non-empty list,
//// nested struct, var ref. Dart source of truth: glp_runtime/lib/runtime/terms.dart.

import gleam/list
import gleeunit/should
import glp/runtime/terms.{
  ConstAtom, ConstInt, ConstReal, ConstString, ConstTerm, StructTerm, VarRef, cons,
  nil,
}

// 1 — atom
pub fn const_atom_test() {
  let t = ConstTerm(ConstAtom("hello"))
  t |> should.equal(ConstTerm(ConstAtom("hello")))
  t |> should.not_equal(ConstTerm(ConstAtom("world")))
  let assert ConstTerm(ConstAtom(name)) = t
  name |> should.equal("hello")
}

// 2 — int
pub fn const_int_test() {
  let t = ConstTerm(ConstInt(42))
  t |> should.equal(ConstTerm(ConstInt(42)))
  t |> should.not_equal(ConstTerm(ConstInt(43)))
  let assert ConstTerm(ConstInt(i)) = t
  i |> should.equal(42)
}

// 3 — real
pub fn const_real_test() {
  let t = ConstTerm(ConstReal(3.14))
  t |> should.equal(ConstTerm(ConstReal(3.14)))
  t |> should.not_equal(ConstTerm(ConstReal(2.71)))
  let assert ConstTerm(ConstReal(f)) = t
  f |> should.equal(3.14)
}

// 4 — string
pub fn const_string_test() {
  let t = ConstTerm(ConstString("hi"))
  t |> should.equal(ConstTerm(ConstString("hi")))
  t |> should.not_equal(ConstTerm(ConstString("ho")))
  let assert ConstTerm(ConstString(s)) = t
  s |> should.equal("hi")
}

// A constant is NOT equal across kinds even with the "same" surface value.
pub fn constant_kinds_distinct_test() {
  ConstTerm(ConstAtom("1")) |> should.not_equal(ConstTerm(ConstString("1")))
}

// 5 — compound struct
pub fn compound_struct_test() {
  let t = StructTerm("foo", [ConstTerm(ConstInt(1)), ConstTerm(ConstAtom("b"))])
  t
  |> should.equal(StructTerm("foo", [ConstTerm(ConstInt(1)), ConstTerm(ConstAtom("b"))]))
  // functor mismatch and arity mismatch are observably different terms
  t
  |> should.not_equal(StructTerm("bar", [ConstTerm(ConstInt(1)), ConstTerm(ConstAtom("b"))]))
  t |> should.not_equal(StructTerm("foo", [ConstTerm(ConstInt(1))]))
  let StructTerm(functor, args) = t
  functor |> should.equal("foo")
  list.length(args) |> should.equal(2)
}

// 6 — empty list (the nil atom)
pub fn nil_list_test() {
  nil() |> should.equal(ConstTerm(ConstAtom("nil")))
}

// 7 — non-empty list (cons cell ".")
pub fn cons_list_test() {
  let t = cons(ConstTerm(ConstInt(1)), nil())
  t |> should.equal(StructTerm(".", [ConstTerm(ConstInt(1)), ConstTerm(ConstAtom("nil"))]))
  // a two-element list [1, 2] is cons(1, cons(2, nil))
  let two = cons(ConstTerm(ConstInt(1)), cons(ConstTerm(ConstInt(2)), nil()))
  let assert StructTerm(".", [head, tail]) = two
  head |> should.equal(ConstTerm(ConstInt(1)))
  tail |> should.equal(cons(ConstTerm(ConstInt(2)), nil()))
}

// 8 — nested struct
pub fn nested_struct_test() {
  let t =
    StructTerm("outer", [StructTerm("inner", [ConstTerm(ConstAtom("x"))])])
  let assert StructTerm("outer", [StructTerm("inner", [inner_arg])]) = t
  inner_arg |> should.equal(ConstTerm(ConstAtom("x")))
}

// 9 — var ref (role NOT in the term; equality by addr only)
pub fn var_ref_test() {
  VarRef(7) |> should.equal(VarRef(7))
  VarRef(7) |> should.not_equal(VarRef(8))
  let VarRef(addr) = VarRef(7)
  addr |> should.equal(7)
}
