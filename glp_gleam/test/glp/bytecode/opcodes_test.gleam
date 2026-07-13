//// Opcode-table integrity (feature 050, T012): every v2.16 instruction variant
//// renders a mnemonic; the unified variable instructions flip writer/reader
//// mnemonics on `is_reader`; mnemonics are pairwise distinct across distinct
//// instructions; the program model indexes labels first-occurrence-wins and
//// merges stdlib-in-front (Dart BytecodeProgram semantics).

import gleam/dict
import gleam/list
import gleam/set
import gleeunit/should
import glp/bytecode/opcodes.{type Op}
import glp/bytecode/program
import glp/runtime/terms.{ConstAtom, ConstInt}

/// One representative instance of EVERY instruction variant (unified
/// instructions appear once per mode). Compile-time exhaustiveness lives in the
/// `Op` type itself; this list is the runtime table the integrity checks walk.
fn representative_ops() -> List(Op) {
  [
    opcodes.Label("p/1"),
    opcodes.ClauseTry,
    opcodes.ClauseNext("c2"),
    opcodes.TryNextClause,
    opcodes.NoMoreClauses,
    opcodes.Commit,
    opcodes.Proceed,
    opcodes.Spawn("q/2", 2),
    opcodes.Requeue("p/1", 1),
    opcodes.Allocate(3),
    opcodes.Deallocate,
    opcodes.Nop,
    opcodes.Halt,
    opcodes.HeadStructure("f", 2, 0),
    opcodes.HeadVariable(0, False),
    opcodes.HeadVariable(0, True),
    opcodes.HeadConstant(ConstInt(42), 0),
    opcodes.HeadNil(0),
    opcodes.HeadList(0),
    opcodes.GetVariable(0, 0, False),
    opcodes.GetVariable(0, 0, True),
    opcodes.GetValue(1, 0, False),
    opcodes.GetValue(1, 0, True),
    opcodes.PutStructure("f", 2, 0),
    opcodes.PutVariable(0, 0, False),
    opcodes.PutVariable(0, 0, True),
    opcodes.PutConstant(ConstAtom("a"), 0),
    opcodes.PutNil(0),
    opcodes.PutList(0),
    opcodes.PutBoundConst(ConstInt(7), 0),
    opcodes.PutBoundNil(0),
    opcodes.UnifyVariable(0, False),
    opcodes.UnifyVariable(0, True),
    opcodes.UnifyConstant(ConstAtom("a")),
    opcodes.SetVariable(0, False),
    opcodes.SetVariable(0, True),
    opcodes.SetConstant(ConstAtom("a")),
    opcodes.UnifyVoid(1),
    opcodes.Push(2),
    opcodes.Pop(2),
    opcodes.UnifyStructure("g", 1),
    opcodes.Guard("ground/1", 1, False),
    opcodes.Ground(0, False),
    opcodes.Known(0, False),
    opcodes.Unknown(0),
    opcodes.Otherwise,
    opcodes.NoReaders(0, False),
    opcodes.GroundEqual(0, 1, False),
    opcodes.Distribute(1, "double", 2),
    opcodes.Transmit(0, "double", 2),
  ]
}

pub fn every_variant_has_a_mnemonic_test() {
  representative_ops()
  |> list.each(fn(op) {
    opcodes.mnemonic(op)
    |> should.not_equal("")
  })
}

pub fn mnemonics_are_pairwise_distinct_test() {
  let mnemonics = list.map(representative_ops(), opcodes.mnemonic)
  set.size(set.from_list(mnemonics))
  |> should.equal(list.length(mnemonics))
}

pub fn unified_instructions_flip_on_is_reader_test() {
  opcodes.mnemonic(opcodes.HeadVariable(0, False))
  |> should.equal("head_writer")
  opcodes.mnemonic(opcodes.HeadVariable(0, True))
  |> should.equal("head_reader")
  opcodes.mnemonic(opcodes.GetVariable(0, 0, False))
  |> should.equal("get_writer_variable")
  opcodes.mnemonic(opcodes.GetVariable(0, 0, True))
  |> should.equal("get_reader_variable")
  opcodes.mnemonic(opcodes.GetValue(0, 0, False))
  |> should.equal("get_writer_value")
  opcodes.mnemonic(opcodes.GetValue(0, 0, True))
  |> should.equal("get_reader_value")
  opcodes.mnemonic(opcodes.PutVariable(0, 0, False))
  |> should.equal("put_writer")
  opcodes.mnemonic(opcodes.PutVariable(0, 0, True))
  |> should.equal("put_reader")
  opcodes.mnemonic(opcodes.UnifyVariable(0, False))
  |> should.equal("unify_writer")
  opcodes.mnemonic(opcodes.UnifyVariable(0, True))
  |> should.equal("unify_reader")
  opcodes.mnemonic(opcodes.SetVariable(0, False))
  |> should.equal("set_writer")
  opcodes.mnemonic(opcodes.SetVariable(0, True))
  |> should.equal("set_reader")
}

// --- program model (T007) ---------------------------------------------------

pub fn labels_index_first_occurrence_test() {
  let prog =
    program.from_ops(
      [
        opcodes.Label("p/1"),
        opcodes.ClauseTry,
        opcodes.Label("p/1"),
        opcodes.Proceed,
      ],
      dict.new(),
    )
  program.label_pc(prog, "p/1")
  |> should.equal(Ok(0))
  program.size(prog)
  |> should.equal(4)
  program.op_at(prog, 1)
  |> should.equal(Ok(opcodes.ClauseTry))
  program.op_at(prog, 4)
  |> should.equal(Error(Nil))
}

pub fn merge_prepends_other_test() {
  let stdlib = program.from_ops([opcodes.Label("std/0"), opcodes.Proceed], dict.new())
  let user = program.from_ops([opcodes.Label("main/0"), opcodes.Halt], dict.new())
  let merged = program.merge(user, stdlib)
  // Dart merge: [...other.ops, ...ops] — stdlib in front.
  program.op_at(merged, 0)
  |> should.equal(Ok(opcodes.Label("std/0")))
  program.label_pc(merged, "main/0")
  |> should.equal(Ok(2))
  program.size(merged)
  |> should.equal(4)
}
