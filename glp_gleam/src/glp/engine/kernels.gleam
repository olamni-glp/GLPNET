//// glp/engine/kernels — native body kernels (T024).
////
//// Body kernels are the runtime-implemented predicates that GLP's arithmetic
//// bottoms out in: `programs/self.glp` defines `:=`/2 as recursive GLP clauses
//// that decompose an `Exp` and, at the leaves, call the native kernels
//// `'_add'`/`'_sub'`/`'_mul'`/`'_div'`/`'_idiv'`/`'_mod'`/`'_neg'` (arity 3, and
//// `_neg`/2). They execute INLINE at a BODY `Spawn` whose label misses the
//// program's own labels (Dart runner.dart:3220-3256 `bodyKernels.lookup`), NOT
//// as spawned goals, with two-valued semantics: success (bind the output writer,
//// carry any reactivations) or abort (a fatal type error / already-bound writer).
////
//// Kernels are pure over the HEAP: they run in the BODY phase, AFTER Commit has
//// applied σ̂w, so there is no σ̂w to consult (this also keeps the module free of
//// any `runner` dependency — the runner imports `kernels` for the Spawn
//// dispatch, so the reverse edge would cycle). Frozen semantics — a faithful
//// port of `glp_runtime/lib/runtime/body_kernels.dart` (`_getNum`,
//// `_evaluateArithmetic`, `_bindResult`), sharing the numeric core in
//// `glp/engine/arith`.
////
//// The extended math kernels (`_abs`/`_sqrt`/trig/`_exp`/`_log*`/`_pow`),
//// type-conversions, and the effectful `_now`/`_send`/`_output` kernels are NOT
//// registered here: they are outside the pure-arithmetic MVP (some need a math
//// library or wall-clock/IO the standalone engine does not have). A BODY Spawn
//// to an unregistered kernel surfaces as a runner error — loud, never guessed
//// (Language Authority §1.14) — rather than a wrong result.

import gleam/list
import gleam/result
import glp/engine/arith.{type NumV, NInt}
import glp/runtime/heap.{type Heap, Bound}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{type Term, ConstTerm, StructTerm, VarRef}

/// The two-valued outcome of a body kernel (Dart `BodyKernelResult`), carrying
/// the updated heap and any goals reactivated by binding the output writer.
pub type KernelOutcome {
  KSuccess(heap: Heap, woken: List(GoalRef))
  KAbort(detail: String)
}

/// Is `name/arity` a registered native body kernel? (Dart `bodyKernels.has`.)
pub fn is_kernel(name: String, arity: Int) -> Bool {
  case name, arity {
    "_add", 3 | "_sub", 3 | "_mul", 3 | "_div", 3 | "_idiv", 3 | "_mod", 3 ->
      True
    "_neg", 2 -> True
    _, _ -> False
  }
}

/// Execute a native body kernel over `args` (heap-only). `Error(Nil)` means
/// `name/arity` is not a kernel (the caller then reports the unresolved Spawn).
pub fn dispatch(
  heap: Heap,
  name: String,
  arity: Int,
  args: List(Term),
) -> Result(KernelOutcome, Nil) {
  case name, arity, args {
    "_add", 3, [a, b, out] ->
      Ok(binary(heap, a, b, out, fn(x, y) { Ok(arith.add(x, y)) }))
    "_sub", 3, [a, b, out] ->
      Ok(binary(heap, a, b, out, fn(x, y) { Ok(arith.sub(x, y)) }))
    "_mul", 3, [a, b, out] ->
      Ok(binary(heap, a, b, out, fn(x, y) { Ok(arith.mul(x, y)) }))
    "_div", 3, [a, b, out] -> Ok(binary(heap, a, b, out, arith.div))
    "_idiv", 3, [a, b, out] -> Ok(binary(heap, a, b, out, idiv_ints))
    "_mod", 3, [a, b, out] -> Ok(binary(heap, a, b, out, mod_ints))
    "_neg", 2, [a, out] ->
      case eval_num(heap, a) {
        Ok(x) -> Ok(bind_result(heap, out, arith.neg(x)))
        Error(_) -> Ok(KAbort(name <> ": operand must be a number"))
      }
    _, _, _ -> Error(Nil)
  }
}

/// `//` requires integer operands (Dart `idivKernel`: `x is! int || y is! int`
/// aborts); zero divisor aborts.
fn idiv_ints(x: NumV, y: NumV) -> Result(NumV, Nil) {
  case x, y {
    NInt(_), NInt(_) -> arith.idiv(x, y)
    _, _ -> Error(Nil)
  }
}

/// `mod` requires integer operands (Dart `modKernel`); zero divisor aborts.
fn mod_ints(x: NumV, y: NumV) -> Result(NumV, Nil) {
  case x, y {
    NInt(_), NInt(_) -> arith.modulo(x, y)
    _, _ -> Error(Nil)
  }
}

fn binary(
  heap: Heap,
  a: Term,
  b: Term,
  out: Term,
  op: fn(NumV, NumV) -> Result(NumV, Nil),
) -> KernelOutcome {
  case eval_num(heap, a), eval_num(heap, b) {
    Ok(x), Ok(y) ->
      case op(x, y) {
        Ok(r) -> bind_result(heap, out, r)
        // Zero divisor / non-integer operand (Dart aborts).
        Error(_) -> KAbort("arithmetic kernel: bad operands / divide by zero")
      }
    _, _ -> KAbort("arithmetic kernel: operands must be numbers")
  }
}

/// Bind the output writer to the numeric result (Dart `_bindResult`); enqueue
/// any reactivations. A non-writer / already-bound output aborts.
fn bind_result(heap: Heap, out: Term, value: NumV) -> KernelOutcome {
  case out {
    VarRef(addr) ->
      case heap.is_writer(heap, addr) {
        True ->
          case heap.bind_writer(heap, addr, arith.to_term(value)) {
            Ok(#(h2, woken)) -> KSuccess(h2, woken)
            Error(_) -> KAbort("body kernel: output writer already bound")
          }
        False -> KAbort("body kernel: output argument is not a writer")
      }
    _ -> KAbort("body kernel: output argument is not a writer")
  }
}

/// Evaluate an operand numerically (Dart `_getNum`): a numeric constant, a heap
/// VarRef dereferenced to a number, or an arithmetic StructTerm.
fn eval_num(heap: Heap, term: Term) -> Result(NumV, Nil) {
  case term {
    ConstTerm(_) -> arith.of_term(term)
    VarRef(addr) ->
      case heap.deref(heap, addr) {
        Ok(#(_, Bound(v))) -> eval_num(heap, v)
        _ -> Error(Nil)
      }
    StructTerm(functor, args) ->
      case result.all(list.map(args, fn(a) { eval_num(heap, a) })) {
        Ok(nums) -> arith.combine(functor, nums)
        Error(_) -> Error(Nil)
      }
  }
}
