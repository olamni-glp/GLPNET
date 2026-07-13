//// glp/engine/arith — the shared numeric core for GLP arithmetic (T024).
////
//// A single, phase-agnostic combination layer used by BOTH consumers of GLP
//// arithmetic:
////   * the generic `Guard` opcode's `evaluate_numeric` (σ̂w + heap deref, in
////     `runner.gleam`), and
////   * the native body kernels `'_add'`/`'_sub'`/… (heap-only deref, in
////     `kernels.gleam`).
//// The two consumers differ ONLY in how they dereference a `VarRef` to a leaf
//// value (pre-commit reads σ̂w; post-commit reads the heap) — the promotion and
//// operator semantics live here, once (restart doc: "do the evaluator once").
////
//// Frozen semantics — a faithful port of the Dart `num` behaviour in
//// `runner.dart` `_evaluateArithmetic`/`evaluateNumeric` and
//// `body_kernels.dart` `_evaluateArithmetic`:
////   +,-,* : int if both operands int, else promote to float (Dart `num` +/-/*)
////   /     : ALWAYS float (Dart `a / b` returns double)
////   //    : truncating integer division toward zero → int (Dart `a ~/ b`)
////   mod   : `a.toInt() % b.toInt()` — euclidean (Dart int `%`) → int
////   neg   : negate, preserving int/float
//// Division / integer-division / modulo by zero → `None` (Dart returns null).

import gleam/float
import gleam/int
import gleam/order.{type Order}
import glp/runtime/terms.{type Term, ConstInt, ConstReal, ConstTerm}

/// A GLP numeric value — the int/float split of Dart's `num` (R-002).
pub type NumV {
  NInt(Int)
  NReal(Float)
}

/// Extract a `NumV` from a numeric constant term; anything else is not numeric.
pub fn of_term(term: Term) -> Result(NumV, Nil) {
  case term {
    ConstTerm(ConstInt(i)) -> Ok(NInt(i))
    ConstTerm(ConstReal(f)) -> Ok(NReal(f))
    _ -> Error(Nil)
  }
}

/// The numeric value back as a ground constant term (kernel output binding).
pub fn to_term(n: NumV) -> Term {
  case n {
    NInt(i) -> ConstTerm(ConstInt(i))
    NReal(f) -> ConstTerm(ConstReal(f))
  }
}

fn to_float(n: NumV) -> Float {
  case n {
    NInt(i) -> int.to_float(i)
    NReal(f) -> f
  }
}

// ── binary / unary operators (Dart `num` promotion) ─────────────────────────

pub fn add(a: NumV, b: NumV) -> NumV {
  case a, b {
    NInt(x), NInt(y) -> NInt(x + y)
    _, _ -> NReal(to_float(a) +. to_float(b))
  }
}

pub fn sub(a: NumV, b: NumV) -> NumV {
  case a, b {
    NInt(x), NInt(y) -> NInt(x - y)
    _, _ -> NReal(to_float(a) -. to_float(b))
  }
}

pub fn mul(a: NumV, b: NumV) -> NumV {
  case a, b {
    NInt(x), NInt(y) -> NInt(x * y)
    _, _ -> NReal(to_float(a) *. to_float(b))
  }
}

/// `/` — always float (Dart `a / b`). Zero divisor → `None`.
pub fn div(a: NumV, b: NumV) -> Result(NumV, Nil) {
  case is_zero(b) {
    True -> Error(Nil)
    False -> Ok(NReal(to_float(a) /. to_float(b)))
  }
}

/// `//` — truncating integer division toward zero → int (Dart `a ~/ b`). Zero
/// divisor → `None`. Gleam int `/` already truncates toward zero.
pub fn idiv(a: NumV, b: NumV) -> Result(NumV, Nil) {
  case is_zero(b) {
    True -> Error(Nil)
    False ->
      case a, b {
        NInt(x), NInt(y) -> Ok(NInt(x / y))
        _, _ -> Ok(NInt(float.truncate(to_float(a) /. to_float(b))))
      }
  }
}

/// `mod` — `a.toInt() % b.toInt()`, euclidean (Dart int `%` == Gleam
/// `int.modulo`) → int. Zero divisor → `None`.
pub fn modulo(a: NumV, b: NumV) -> Result(NumV, Nil) {
  case is_zero(b) {
    True -> Error(Nil)
    False ->
      case int.modulo(to_int(a), to_int(b)) {
        Ok(r) -> Ok(NInt(r))
        Error(_) -> Error(Nil)
      }
  }
}

pub fn neg(a: NumV) -> NumV {
  case a {
    NInt(i) -> NInt(-i)
    NReal(f) -> NReal(0.0 -. f)
  }
}

fn to_int(n: NumV) -> Int {
  case n {
    NInt(i) -> i
    NReal(f) -> float.truncate(f)
  }
}

fn is_zero(n: NumV) -> Bool {
  case n {
    NInt(i) -> i == 0
    NReal(f) -> f == 0.0
  }
}

/// Numeric comparison (Dart `num` `<`/`>`/`==`). Same-kind int compares exactly;
/// a mixed pair promotes to float (Dart `1 == 1.0` → true).
pub fn compare(a: NumV, b: NumV) -> Order {
  case a, b {
    NInt(x), NInt(y) -> int.compare(x, y)
    _, _ -> float.compare(to_float(a), to_float(b))
  }
}

/// Combine an arithmetic functor over already-evaluated operands — the shared
/// StructTerm dispatch of the Dart arithmetic evaluators. A non-arithmetic
/// functor / bad arity / zero divisor yields `None` (the caller then treats the
/// term as non-numeric).
pub fn combine(functor: String, operands: List(NumV)) -> Result(NumV, Nil) {
  case functor, operands {
    "+", [a, b] -> Ok(add(a, b))
    "-", [a] -> Ok(neg(a))
    "-", [a, b] -> Ok(sub(a, b))
    "neg", [a] -> Ok(neg(a))
    "*", [a, b] -> Ok(mul(a, b))
    "/", [a, b] -> div(a, b)
    "//", [a, b] -> idiv(a, b)
    "mod", [a, b] -> modulo(a, b)
    _, _ -> Error(Nil)
  }
}
