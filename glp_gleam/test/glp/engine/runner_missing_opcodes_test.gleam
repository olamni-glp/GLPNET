//// T063 regression — `close-bytecode-runner-missing-opcodes` (Tier A).
////
//// Three v2.16 opcodes previously fell through the runner dispatch to
//// `RunnerError(Unimplemented(..))`: `HeadList`, `PutList`, `HeadVariable`.
//// The Gleam codegen never emits them (lists lower to `"."/2` via
//// Head/PutStructure; HEAD structure-vars to UnifyVariable), and neither does
//// the Dart codegen — but the Dart runner dispatches them defensively
//// (runner.dart:4381/4490/1044). The Gleam runner now routes them to its
//// already-tested structure handlers:
////   HeadList(slot)          == HeadStructure(".", 2, slot)   (§6.6)
////   PutList(slot)           == PutStructure(".", 2, slot)    (§7.6)
////   HeadVariable(v, reader) == UnifyVariable(v, reader)      (§8.1/§8.2, S-pos)
////
//// Strategy: load a real list program (whose codegen output uses the LONG
//// forms), rewrite those instructions to the SHORTHAND opcodes, rebuild the
//// program, and assert the reduction produces the SAME ground result. If any
//// shorthand op were still unhandled the reduction would be
//// `RunnerError(Unimplemented(..))`, not `Reduced`.

import gleam/list
import gleeunit/should
import glp/bytecode/opcodes
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/runner
import glp/runtime/heap
import glp/runtime/terms.{
  type Term, ConstAtom, ConstTerm, StructTerm, VarRef, cons, nil,
}

// ── helpers ──────────────────────────────────────────────────────────────────

/// The full op stream, in PC order.
fn ops_of(prog: program.BytecodeProgram) -> List(opcodes.Op) {
  collect_ops(prog, 0, [])
}

fn collect_ops(
  prog: program.BytecodeProgram,
  pc: Int,
  acc: List(opcodes.Op),
) -> List(opcodes.Op) {
  case program.op_at(prog, pc) {
    Ok(op) -> collect_ops(prog, pc + 1, [op, ..acc])
    Error(_) -> list.reverse(acc)
  }
}

/// Rewrite each long-form list/structure instruction to its shorthand opcode.
fn to_shorthand(op: opcodes.Op) -> opcodes.Op {
  case op {
    opcodes.HeadStructure(".", 2, slot) -> opcodes.HeadList(slot)
    opcodes.PutStructure(".", 2, slot) -> opcodes.PutList(slot)
    opcodes.UnifyVariable(v, is_reader) -> opcodes.HeadVariable(v, is_reader)
    _ -> op
  }
}

/// Rebuild `prog` with every long-form instruction replaced by its shorthand.
fn shorthand_program(
  prog: program.BytecodeProgram,
) -> program.BytecodeProgram {
  ops_of(prog)
  |> list.map(to_shorthand)
  |> program.from_ops(program.defined_guards(prog))
}

/// #(head_list, put_list, head_variable) occurrence counts.
fn shorthand_counts(prog: program.BytecodeProgram) -> #(Int, Int, Int) {
  list.fold(ops_of(prog), #(0, 0, 0), fn(acc, op) {
    let #(hl, pl, hv) = acc
    case op {
      opcodes.HeadList(_) -> #(hl + 1, pl, hv)
      opcodes.PutList(_) -> #(hl, pl + 1, hv)
      opcodes.HeadVariable(_, _) -> #(hl, pl, hv + 1)
      _ -> acc
    }
  })
}

/// Fully resolve a term through the heap (follow VarRefs, recurse into args).
fn resolve(h: heap.Heap, term: Term) -> Term {
  case term {
    VarRef(addr) ->
      case heap.deref(h, addr) {
        Ok(#(_, heap.Bound(v))) -> resolve(h, v)
        _ -> term
      }
    StructTerm(f, args) -> StructTerm(f, list.map(args, fn(a) { resolve(h, a) }))
    _ -> term
  }
}

const list_source = "Elem ::= a ; b ; c.
List ::= [] ; [Elem | List].
procedure headof(List?, Elem).
headof([], a).
headof([H|_], H?).
procedure cont(List?).
cont([]).
cont([_|_]).
procedure emit(Elem?).
emit(X) :- cont([X?])."

fn load_prog() -> program.BytecodeProgram {
  let assert Ok(outcome) = loader.load(list_source, "")
  outcome.program
}

// ── HeadList + HeadVariable (head-side list decomposition) ───────────────────

// headof([a], X): the head `[H|_]` compiles to HeadStructure(".",2) + a HEAD
// structure variable. Rewritten to HeadList + HeadVariable it must still bind
// X to `a` — proving both opcodes dispatch to the structure handlers.
pub fn head_list_and_variable_bind_head_test() {
  let prog = load_prog()
  let short = shorthand_program(prog)
  // The rewrite actually inserted the shorthand head opcodes.
  let #(hl, _pl, hv) = shorthand_counts(short)
  should.be_true(hl >= 1)
  should.be_true(hv >= 1)

  let a = ConstTerm(ConstAtom("a"))
  let run = fn(p: program.BytecodeProgram) -> Term {
    let assert Ok(kappa) = program.label_pc(p, "headof/2")
    let #(h, x_writer, _) = heap.allocate_variable(heap.new())
    let regs =
      program.new_regs()
      |> program.set_reg(0, cons(a, nil()))
      |> program.set_reg(1, VarRef(x_writer))
    let assert runner.Reduced(heap: h2, ..) =
      runner.reduce(p, runner.new_context(h, regs), kappa, 1000)
    resolve(h2, VarRef(x_writer))
  }

  // Long form and shorthand form yield the identical ground result: X = a.
  run(prog) |> should.equal(a)
  run(short) |> should.equal(a)
}

// ── PutList (body-side list construction) ────────────────────────────────────

// emit(a): the body goal `cont([X?])` passes a list literal argument, which
// compiles to PutStructure(".",2). Rewritten to PutList, the body must still
// construct the argument and the reduction must complete (Reduced, not
// RunnerError(Unimplemented("put_list"))).
pub fn put_list_builds_body_arg_test() {
  let prog = load_prog()
  let short = shorthand_program(prog)
  // The rewrite actually inserted a PutList.
  let #(_hl, pl, _hv) = shorthand_counts(short)
  should.be_true(pl >= 1)

  let a = ConstTerm(ConstAtom("a"))
  let run = fn(p: program.BytecodeProgram) -> runner.ReduceOutcome {
    let assert Ok(kappa) = program.label_pc(p, "emit/1")
    let regs =
      program.new_regs()
      |> program.set_reg(0, a)
    runner.reduce(p, runner.new_context(heap.new(), regs), kappa, 1000)
  }

  // Long form and shorthand both reduce to completion — PutList dispatches.
  let assert runner.Reduced(..) = run(prog)
  let assert runner.Reduced(..) = run(short)
}
