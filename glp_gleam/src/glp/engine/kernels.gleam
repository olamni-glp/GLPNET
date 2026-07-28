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
//// The extended math kernels (`_abs`/`_sqrt`/trig/`_exp`/`_log*`/`_pow`) and the
//// type-conversions (`_integer`/`_real`/`_round`/`_floor`/`_ceil`) ARE registered
//// here — faithful ports of the Dart `body_kernels.dart` math/conversion kernels,
//// with the transcendental functions bridged to Erlang `:math` (a BEAM builtin, no
//// external package). The effectful `_now`/`_send` kernels remain unregistered
//// (they need wall-clock/IO the standalone engine does not have); a BODY Spawn to
//// an unregistered kernel surfaces as a runner error — loud, never guessed
//// (Language Authority §1.14) — rather than a wrong result.
////
//// `_output`/1 (T034) IS registered: it renders its ground argument (Dart
//// `formatGroundTerm`, glp/engine/output_capture) into a captured output LINE that
//// `KSuccess` threads out as data (→ runner.Reduced → scheduler → engine → REPL),
//// leaving the heap untouched. This is a faithful port of an already-approved
//// system predicate (self.glp `procedure _output(_?)`, Dart `bodyKernels` `_output`
//// /1), not a §1.14 language extension.

import gleam/erlang/atom
import gleam/float
import gleam/int
import gleam/list
import gleam/result
import glp/engine/arith.{type NumV, NInt, NReal}
import glp/engine/output_capture
import glp/runtime/heap.{type Heap, Bound, Unbound}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstTerm, StructTerm, VarRef,
}

/// The functor of the GLP mutual-reference (O(1) stream append) sentinel term.
/// The Dart runtime has a dedicated `MutualRefTerm` variant; Gleam's `Term` has
/// no custom cell, so a mutual ref is `'$mutual_ref'(<currentWriterAddr:int>)` —
/// a ground struct threaded immutably through the GLP `stream_append` chain (the
/// Dart mutation of `currentWriterAddr` is an optimization the GLP code does not
/// rely on: `mwm_copy` threads `Ref → Ref1`).
const mutual_ref_functor = "$mutual_ref"

/// The two-valued outcome of a body kernel (Dart `BodyKernelResult`), carrying the
/// updated heap, any goals reactivated by binding the output writer, and any
/// captured program-output lines (`_output`/1 — empty for the arithmetic kernels).
pub type KernelOutcome {
  KSuccess(heap: Heap, woken: List(GoalRef), output: List(String))
  KAbort(detail: String)
}

/// Is `name/arity` a registered native body kernel? (Dart `bodyKernels.has`.)
pub fn is_kernel(name: String, arity: Int) -> Bool {
  case name, arity {
    "_add", 3 | "_sub", 3 | "_mul", 3 | "_div", 3 | "_idiv", 3 | "_mod", 3 ->
      True
    "_neg", 2 -> True
    "_pow", 3 -> True
    // Unary math + type-conversion kernels (Dart body_kernels.dart math/conv).
    "_abs", 2
    | "_sqrt", 2
    | "_sin", 2
    | "_cos", 2
    | "_tan", 2
    | "_exp", 2
    | "_ln", 2
    | "_log10", 2
    | "_asin", 2
    | "_acos", 2
    | "_atan", 2
    | "_integer", 2
    | "_real", 2
    | "_round", 2
    | "_floor", 2
    | "_ceil", 2 -> True
    // Univ (=../2) — list↔compound (Dart listToTupleKernel / tupleToListKernel).
    "_list_to_tuple", 2 | "_tuple_to_list", 2 -> True
    // Mutual-reference (O(1) stream append; mwm/2) — Dart mutualRef kernels.
    "_allocate_mutual_reference", 2
    | "_stream_append", 3
    | "_close_mutual_reference", 1 -> True
    "_output", 1 -> True
    // Effectful external-io body kernel: current wall-clock time (Dart `nowKernel`).
    "_now", 1 -> True
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
    // `_copy(A, A_copy)` — snapshot: deref A and bind A_copy to it (parity port of
    // Dart `copyKernel`, body_kernels.dart:527: `_deref(args[0])` then bind arg[1]).
    // The metainterpreter idiom (`tracing_meta.glp`) copies a goal before reducing it.
    "_copy", 2, [source, out] ->
      Ok(bind_term(heap, out, deref_term(heap, source)))
    "_neg", 2, [a, out] ->
      case eval_num(heap, a) {
        Ok(x) -> Ok(bind_result(heap, out, arith.neg(x)))
        Error(_) -> Ok(KAbort(name <> ": operand must be a number"))
      }
    // ── math + type-conversion kernels (Dart body_kernels.dart) ───────────────
    "_abs", 2, [a, out] -> Ok(unary(heap, a, out, fn(x) { Ok(abs_num(x)) }))
    "_sqrt", 2, [a, out] -> Ok(unary(heap, a, out, sqrt_num))
    "_sin", 2, [a, out] -> Ok(unary(heap, a, out, real_fn(erl_sin)))
    "_cos", 2, [a, out] -> Ok(unary(heap, a, out, real_fn(erl_cos)))
    "_tan", 2, [a, out] -> Ok(unary(heap, a, out, real_fn(erl_tan)))
    "_exp", 2, [a, out] -> Ok(unary(heap, a, out, real_fn(erl_exp)))
    "_ln", 2, [a, out] -> Ok(unary(heap, a, out, ln_num))
    "_log10", 2, [a, out] -> Ok(unary(heap, a, out, log10_num))
    "_asin", 2, [a, out] -> Ok(unary(heap, a, out, arc_fn(erl_asin)))
    "_acos", 2, [a, out] -> Ok(unary(heap, a, out, arc_fn(erl_acos)))
    "_atan", 2, [a, out] -> Ok(unary(heap, a, out, real_fn(erl_atan)))
    "_integer", 2, [a, out] ->
      Ok(unary(heap, a, out, fn(x) { Ok(NInt(arith_trunc(x))) }))
    "_real", 2, [a, out] ->
      Ok(unary(heap, a, out, fn(x) { Ok(NReal(num_to_float(x))) }))
    "_round", 2, [a, out] ->
      Ok(unary(heap, a, out, fn(x) { Ok(NInt(round_num(x))) }))
    "_floor", 2, [a, out] ->
      Ok(unary(heap, a, out, fn(x) { Ok(NInt(floor_num(x))) }))
    "_ceil", 2, [a, out] ->
      Ok(unary(heap, a, out, fn(x) { Ok(NInt(ceil_num(x))) }))
    "_pow", 3, [a, b, out] -> Ok(binary(heap, a, b, out, pow_num))
    // ── univ (=../2): list↔compound (Dart list/tuple kernels) ─────────────────
    // Compose: [Functor, Arg1, …] → Functor(Arg1, …). First element must be an
    // atom; the list must be non-empty.
    "_list_to_tuple", 2, [lst, out] ->
      case glp_list_to_terms(heap, lst) {
        Ok([ConstTerm(ConstAtom(functor)), ..sargs]) ->
          Ok(bind_term(heap, out, StructTerm(functor, sargs)))
        _ ->
          Ok(KAbort(
            "_list_to_tuple: first element must be an atom functor of a non-empty list",
          ))
      }
    // Decompose: Functor(Arg1, …) → [Functor, Arg1, …].
    "_tuple_to_list", 2, [tup, out] ->
      case deref_term(heap, tup) {
        StructTerm(functor, sargs) ->
          Ok(bind_term(
            heap,
            out,
            terms_to_glp_list([
              ConstTerm(ConstAtom(functor)),
              ..list.map(sargs, fn(t) { deref_term(heap, t) })
            ]),
          ))
        _ -> Ok(KAbort("_tuple_to_list: first argument must be a structure"))
      }
    // ── mutual reference (O(1) stream append) — Dart mutualRef kernels ────────
    // allocate(Ref, Out): Out is the (unbound writer) stream head; Ref becomes a
    // sentinel capturing Out's writer addr as the current append point.
    "_allocate_mutual_reference", 2, [ref_out, stream_w] ->
      case deref_term(heap, stream_w) {
        VarRef(addr) ->
          case heap.is_writer(heap, addr) {
            True -> Ok(bind_term(heap, ref_out, make_mutual_ref(addr)))
            False ->
              Ok(KAbort(
                "_allocate_mutual_reference: 2nd arg must be an unbound writer",
              ))
          }
        _ ->
          Ok(KAbort(
            "_allocate_mutual_reference: 2nd arg must be an unbound writer",
          ))
      }
    // stream_append(RefIn, Value, RefOut): WALK to the stream's open tail and bind
    // it to a cons cell '.'(Value, NewTail?). RefOut is the SAME ref (the head addr)
    // — the sentinel is immutable and always walked from, so a ref shared across
    // streams (mwm1 passes the original Ref to each stream's mwm_copy) keeps
    // appending after the previous stream, matching Dart's in-place mutation.
    "_stream_append", 3, [ref_in, value, ref_out] ->
      case mutual_ref_addr(heap, ref_in) {
        Ok(cur) ->
          case find_open_tail(heap, cur) {
            Ok(tail) -> {
              let v = deref_term(heap, value)
              let #(h1, _w2, r2) = heap.allocate_variable(heap)
              let cons = StructTerm(".", [v, VarRef(r2)])
              case heap.bind_writer(h1, tail, cons) {
                Ok(#(h2, woken1)) ->
                  Ok(carry_woken(
                    woken1,
                    bind_term(h2, ref_out, make_mutual_ref(cur)),
                  ))
                Error(_) ->
                  Ok(KAbort("_stream_append: open tail already bound"))
              }
            }
            Error(_) -> Ok(KAbort("_stream_append: not an open stream"))
          }
        Error(_) ->
          Ok(KAbort("_stream_append: 1st arg must be a mutual reference"))
      }
    // close(Ref): terminate the stream by binding its OPEN tail to nil. The Dart
    // MutualRefTerm mutates `currentWriterAddr` in place, so `close_when_done` —
    // which holds the ORIGINAL ref, not the threaded one — sees the final tail. The
    // Gleam `$mutual_ref` is immutable, so `close` instead WALKS the cons chain from
    // the ref's (original) addr to the still-open tail and binds THAT (outcome-
    // identical: the final open writer becomes nil).
    "_close_mutual_reference", 1, [ref] ->
      case mutual_ref_addr(heap, ref) {
        Ok(start) ->
          case find_open_tail(heap, start) {
            Ok(tail) ->
              case heap.bind_writer(heap, tail, ConstTerm(ConstAtom("nil"))) {
                Ok(#(h2, woken)) -> Ok(KSuccess(h2, woken, []))
                Error(_) ->
                  Ok(KAbort("_close_mutual_reference: open tail already bound"))
              }
            Error(_) ->
              Ok(KAbort("_close_mutual_reference: not an open stream"))
          }
        Error(_) ->
          Ok(KAbort("_close_mutual_reference: argument must be a mutual reference"))
      }
    // '_output'(T): render the ground term to one captured line; heap unchanged,
    // no writer bound, no reactivations (Dart `outputKernel` — the side effect is
    // the output, threaded as data here rather than `print`ed inline).
    "_output", 1, [t] ->
      Ok(KSuccess(heap, [], [output_capture.format_ground_term(heap, t)]))
    // '_now'(T): bind T to the current wall-clock time in milliseconds since the
    // epoch (Dart `nowKernel`: `DateTime.now().millisecondsSinceEpoch`). The one
    // external-io kernel here — its result varies per call (excluded from the byte-
    // parity corpus), so a test asserts an integer is bound, not a fixed value.
    "_now", 1, [out] -> Ok(bind_term(heap, out, ConstTerm(ConstInt(now_millis()))))
    _, _, _ -> Error(Nil)
  }
}

/// Current wall-clock time in milliseconds since the Unix epoch — the `_now/1`
/// external-io source (Dart `DateTime.now().millisecondsSinceEpoch`). `system_time/1`
/// is a BIF (AtomVM-safe, no OTP).
fn now_millis() -> Int {
  erl_system_time(atom.create("millisecond"))
}

@external(erlang, "erlang", "system_time")
fn erl_system_time(unit: atom.Atom) -> Int

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
  bind_term(heap, out, arith.to_term(value))
}

/// Bind the output writer to an arbitrary result term (Dart `_bindResult` for a
/// `Term` value); enqueue any reactivations. Non-writer / already-bound aborts.
fn bind_term(heap: Heap, out: Term, value: Term) -> KernelOutcome {
  case out {
    VarRef(addr) ->
      case heap.is_writer(heap, addr) {
        True ->
          case heap.bind_writer(heap, addr, value) {
            Ok(#(h2, woken)) -> KSuccess(h2, woken, [])
            Error(_) -> KAbort("body kernel: output writer already bound")
          }
        False -> KAbort("body kernel: output argument is not a writer")
      }
    _ -> KAbort("body kernel: output argument is not a writer")
  }
}

/// Deref a term through the heap to its bound value (Dart `_deref`); an unbound
/// or non-var term is returned as-is.
fn deref_term(heap: Heap, term: Term) -> Term {
  case term {
    VarRef(addr) ->
      case heap.deref(heap, addr) {
        Ok(#(_, Bound(v))) -> deref_term(heap, v)
        _ -> term
      }
    _ -> term
  }
}

/// Walk a GLP list (`'.'/2` cells terminated by `nil`) into a Gleam list of its
/// deref'd element terms (Dart `_glpListToDartList`). A non-list → `Error`.
fn glp_list_to_terms(heap: Heap, term: Term) -> Result(List(Term), Nil) {
  case deref_term(heap, term) {
    ConstTerm(ConstAtom("nil")) -> Ok([])
    StructTerm(".", [h, t]) ->
      result.map(glp_list_to_terms(heap, t), fn(rest) {
        [deref_term(heap, h), ..rest]
      })
    _ -> Error(Nil)
  }
}

/// Build a GLP list (`'.'/2` cells + `nil`) from a Gleam list of terms (Dart
/// `_dartListToGlpList`).
fn terms_to_glp_list(items: List(Term)) -> Term {
  case items {
    [] -> ConstTerm(ConstAtom("nil"))
    [h, ..t] -> StructTerm(".", [h, terms_to_glp_list(t)])
  }
}

/// The mutual-ref sentinel capturing `addr` as its current append point.
fn make_mutual_ref(addr: Int) -> Term {
  StructTerm(mutual_ref_functor, [ConstTerm(ConstInt(addr))])
}

/// Extract the current-writer addr from a mutual-ref sentinel term.
fn mutual_ref_addr(heap: Heap, term: Term) -> Result(Int, Nil) {
  case deref_term(heap, term) {
    StructTerm(f, [ConstTerm(ConstInt(a))]) if f == mutual_ref_functor -> Ok(a)
    _ -> Error(Nil)
  }
}

/// Is `term` a mutual-ref sentinel? (the `is_mutual_ref/1` guard; exported so the
/// runner's guard evaluator can test it without duplicating the representation).
pub fn is_mutual_ref(term: Term) -> Bool {
  case term {
    StructTerm(f, [ConstTerm(ConstInt(_))]) if f == mutual_ref_functor -> True
    _ -> False
  }
}

/// Walk a partially-built stream (`'.'/2` cons cells) from `addr` to the terminal
/// writer of its still-open tail (Dart's mutated `currentWriterAddr`). An empty
/// stream (addr itself unbound) returns addr's terminal; a non-cons/closed value
/// is an error.
fn find_open_tail(heap: Heap, addr: Int) -> Result(Int, Nil) {
  case heap.deref(heap, addr) {
    Ok(#(_, Bound(StructTerm(".", [_, VarRef(tail)])))) ->
      find_open_tail(heap, tail)
    Ok(#(_, Unbound(terminal))) -> Ok(terminal)
    _ -> Error(Nil)
  }
}

/// Prepend earlier reactivations to a kernel outcome (threading `bind_writer`
/// wakes across the two binds `_stream_append` performs).
fn carry_woken(earlier: List(GoalRef), outcome: KernelOutcome) -> KernelOutcome {
  case outcome {
    KSuccess(h, woken, output) ->
      KSuccess(h, list.append(earlier, woken), output)
    KAbort(_) -> outcome
  }
}

/// A unary math/conversion kernel (Dart `absKernel`/`sqrtKernel`/… `_getNum` +
/// `_bindResult`). A domain error (`Error` from `op`) aborts, as Dart does for
/// `sqrt(x<0)` / `ln(x<=0)` / `asin`|`acos`(|x|>1).
fn unary(
  heap: Heap,
  a: Term,
  out: Term,
  op: fn(NumV) -> Result(NumV, Nil),
) -> KernelOutcome {
  case eval_num(heap, a) {
    Ok(x) ->
      case op(x) {
        Ok(r) -> bind_result(heap, out, r)
        Error(_) -> KAbort("math kernel: domain error / bad operand")
      }
    Error(_) -> KAbort("math kernel: operand must be a number")
  }
}

fn num_to_float(n: NumV) -> Float {
  case n {
    NInt(i) -> int.to_float(i)
    NReal(f) -> f
  }
}

/// `abs` preserves int/float (Dart `x.abs()`).
fn abs_num(n: NumV) -> NumV {
  case n {
    NInt(i) -> NInt(int.absolute_value(i))
    NReal(f) -> NReal(float.absolute_value(f))
  }
}

/// `sqrt` → double; aborts on a negative operand (Dart `x < 0`).
fn sqrt_num(n: NumV) -> Result(NumV, Nil) {
  let x = num_to_float(n)
  case x <. 0.0 {
    True -> Error(Nil)
    False -> Ok(NReal(erl_sqrt(x)))
  }
}

/// `ln` → double; aborts on a non-positive operand (Dart `x <= 0`).
fn ln_num(n: NumV) -> Result(NumV, Nil) {
  let x = num_to_float(n)
  case x <=. 0.0 {
    True -> Error(Nil)
    False -> Ok(NReal(erl_log(x)))
  }
}

/// `log10` → double; aborts on a non-positive operand (Dart `x <= 0`).
fn log10_num(n: NumV) -> Result(NumV, Nil) {
  let x = num_to_float(n)
  case x <=. 0.0 {
    True -> Error(Nil)
    False -> Ok(NReal(erl_log10(x)))
  }
}

/// A total real→real function (`sin`/`cos`/`tan`/`exp`/`atan`).
fn real_fn(f: fn(Float) -> Float) -> fn(NumV) -> Result(NumV, Nil) {
  fn(n) { Ok(NReal(f(num_to_float(n)))) }
}

/// `asin`/`acos`: aborts when |x| > 1 (Dart `x < -1 || x > 1`).
fn arc_fn(f: fn(Float) -> Float) -> fn(NumV) -> Result(NumV, Nil) {
  fn(n) {
    let x = num_to_float(n)
    case x <. -1.0 || x >. 1.0 {
      True -> Error(Nil)
      False -> Ok(NReal(f(x)))
    }
  }
}

/// `pow`: int base ^ non-negative int exp → int (Dart `math.pow(int,int>=0)`),
/// else float (Dart promotes to double).
fn pow_num(a: NumV, b: NumV) -> Result(NumV, Nil) {
  case a, b {
    NInt(base), NInt(exp) ->
      case exp >= 0 {
        True -> Ok(NInt(int_pow(base, exp)))
        False -> Ok(NReal(erl_pow(int.to_float(base), int.to_float(exp))))
      }
    _, _ -> Ok(NReal(erl_pow(num_to_float(a), num_to_float(b))))
  }
}

fn int_pow(base: Int, exp: Int) -> Int {
  case exp <= 0 {
    True -> 1
    False -> base * int_pow(base, exp - 1)
  }
}

/// `integer` truncates toward zero (Dart `x.toInt()`).
fn arith_trunc(n: NumV) -> Int {
  case n {
    NInt(i) -> i
    NReal(f) -> float.truncate(f)
  }
}

/// `round` (Dart `x.round()`); an int is unchanged.
fn round_num(n: NumV) -> Int {
  case n {
    NInt(i) -> i
    NReal(f) -> float.round(f)
  }
}

/// `floor`/`ceil` → int (Dart `x.floor()`/`x.ceil()`); an int is unchanged. The
/// `floor`/`ceiling` result is already whole, so `float.round` converts exactly.
fn floor_num(n: NumV) -> Int {
  case n {
    NInt(i) -> i
    NReal(f) -> float.round(float.floor(f))
  }
}

fn ceil_num(n: NumV) -> Int {
  case n {
    NInt(i) -> i
    NReal(f) -> float.round(float.ceiling(f))
  }
}

// Erlang `:math` FFI for the transcendental functions Gleam's stdlib lacks
// (BEAM builtin — no external package, so the deps-policy guard is unaffected).
@external(erlang, "math", "sqrt")
fn erl_sqrt(x: Float) -> Float

@external(erlang, "math", "sin")
fn erl_sin(x: Float) -> Float

@external(erlang, "math", "cos")
fn erl_cos(x: Float) -> Float

@external(erlang, "math", "tan")
fn erl_tan(x: Float) -> Float

@external(erlang, "math", "exp")
fn erl_exp(x: Float) -> Float

@external(erlang, "math", "log")
fn erl_log(x: Float) -> Float

@external(erlang, "math", "log10")
fn erl_log10(x: Float) -> Float

@external(erlang, "math", "asin")
fn erl_asin(x: Float) -> Float

@external(erlang, "math", "acos")
fn erl_acos(x: Float) -> Float

@external(erlang, "math", "atan")
fn erl_atan(x: Float) -> Float

@external(erlang, "math", "pow")
fn erl_pow(x: Float, y: Float) -> Float

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
