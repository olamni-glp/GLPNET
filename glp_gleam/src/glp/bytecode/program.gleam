//// glp/bytecode/program — the compiled program model + loader types (feature 050, T007).
////
//// Dart source of truth: glp_runtime/lib/bytecode/runner.dart (`BytecodeProgram`);
//// builder ref: glp_runtime/lib/bytecode/asm.dart (`BC.prog`).
//// Data model: specs/050-full-gleam-combined/data-model.md §BytecodeProgram —
//// procedure table (label → entry PC), instruction stream (v2.16 opcodes), and the
//// positional X-register file each activation owns (FR-006).
////
//// The Dart `ops` is a `List<dynamic>` indexed by PC. Gleam stores the stream as a
//// PC-keyed dict (immutable random access) plus the size; the label index keeps the
//// FIRST occurrence of each label (multi-clause procedures — runner.dart:71).

import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/string
import glp/bytecode/guard_defs.{type GuardProcSpec}
import glp/bytecode/opcodes.{type LabelName, type Op, Label}
import glp/runtime/terms.{type Term}

/// A loaded v2.16 bytecode program: instruction stream + procedure/label table +
/// the runtime-defined guard side table ("name/arity" keyed — 049 (a1); empty for
/// programs without defined guards).
pub opaque type BytecodeProgram {
  BytecodeProgram(
    by_pc: Dict(Int, Op),
    size: Int,
    labels: Dict(LabelName, Int),
    defined_guards: Dict(String, GuardProcSpec),
    /// The module RPC import table: a `Distribute` opcode's 1-based `import_index`
    /// → the target module name (codegen's `add_import` table, inverted; feature
    /// 059 T078). Empty for programs with no `M # goal(...)` static cross-module
    /// calls. Resolves `Distribute` at runtime, mirroring Dart `replCtx.imports`.
    imports: Dict(Int, String),
  )
}

/// Build a program from an instruction list (Dart `BytecodeProgram(ops)` — label
/// index built here, first occurrence of each label wins).
pub fn from_ops(
  ops: List(Op),
  defined_guards: Dict(String, GuardProcSpec),
) -> BytecodeProgram {
  let #(by_pc, labels, size) =
    list.fold(ops, #(dict.new(), dict.new(), 0), fn(acc, op) {
      let #(by_pc, labels, pc) = acc
      let labels = case op {
        Label(name) ->
          case dict.has_key(labels, name) {
            True -> labels
            False -> dict.insert(labels, name, pc)
          }
        _ -> labels
      }
      #(dict.insert(by_pc, pc, op), labels, pc + 1)
    })
  BytecodeProgram(by_pc:, size:, labels:, defined_guards:, imports: dict.new())
}

/// Install the module-RPC import table (`import_index` → module name) surfaced from
/// codegen (feature 059 T078). Returns the program carrying its imports.
pub fn with_imports(
  program: BytecodeProgram,
  imports: Dict(Int, String),
) -> BytecodeProgram {
  BytecodeProgram(..program, imports: imports)
}

/// Resolve a `Distribute` opcode's 1-based `import_index` to the target module
/// name, or `Error(Nil)` if the index is not in the import table.
pub fn import_name(program: BytecodeProgram, index: Int) -> Result(String, Nil) {
  dict.get(program.imports, index)
}

/// Number of instructions in the stream.
pub fn size(program: BytecodeProgram) -> Int {
  program.size
}

/// The runtime-defined guard side table (Dart `prog.definedGuards`) — the
/// multi-clause test-only guard procedures the runner interprets three-valued.
pub fn defined_guards(program: BytecodeProgram) -> Dict(String, GuardProcSpec) {
  program.defined_guards
}

/// Fetch the instruction at `pc` (Dart `ops[pc]`); `Error(Nil)` past the end.
pub fn op_at(program: BytecodeProgram, pc: Int) -> Result(Op, Nil) {
  dict.get(program.by_pc, pc)
}

/// Entry PC of a label (procedure entry labels are "name/arity" by convention).
pub fn label_pc(program: BytecodeProgram, name: LabelName) -> Result(Int, Nil) {
  dict.get(program.labels, name)
}

/// The runtime-defined guard spec for side-table key "name/arity" (049 (a1)).
pub fn defined_guard(
  program: BytecodeProgram,
  key: String,
) -> Result(GuardProcSpec, Nil) {
  dict.get(program.defined_guards, key)
}

/// The instruction stream in PC order (disassembly / merge support).
pub fn to_ops(program: BytecodeProgram) -> List(Op) {
  dict.to_list(program.by_pc)
  |> list.sort(fn(a, b) { int.compare(a.0, b.0) })
  |> list.map(fn(entry) { entry.1 })
}

/// Merge `other` in FRONT of `program` (Dart `merge`: prepend stdlib —
/// `[...other.ops, ...ops]`; guard tables union, `program`'s entries winning).
pub fn merge(
  program: BytecodeProgram,
  other: BytecodeProgram,
) -> BytecodeProgram {
  from_ops(
    list.append(to_ops(other), to_ops(program)),
    dict.merge(other.defined_guards, program.defined_guards),
  )
  |> with_imports(dict.merge(other.imports, program.imports))
}

/// Human-readable disassembly, one "PC n: instruction" line per op (Dart
/// `toDisassembly`). Debug output only — never a parity surface (v2.16 doc §0.5:
/// display format is not bytecode structure).
pub fn to_disassembly(program: BytecodeProgram) -> String {
  to_ops(program)
  |> list.index_map(fn(op, pc) {
    "PC " <> int.to_string(pc) <> ": " <> op_to_string(op)
  })
  |> string.join("\n")
}

/// Render one instruction: the unified variable instructions show their
/// writer/reader mode explicitly (Dart `_instructionToString` special cases);
/// everything else renders as its constructor.
pub fn op_to_string(op: Op) -> String {
  case op {
    opcodes.PutVariable(var_index, arg_slot, _) ->
      "PutVariable(X"
      <> int.to_string(var_index)
      <> " -> A"
      <> int.to_string(arg_slot)
      <> ", "
      <> mode_word(op)
      <> ")"
    opcodes.HeadVariable(var_index, _) ->
      "HeadVariable(X" <> int.to_string(var_index) <> ", " <> mode_word(op) <> ")"
    opcodes.UnifyVariable(var_index, _) ->
      "UnifyVariable(X" <> int.to_string(var_index) <> ", " <> mode_word(op) <> ")"
    opcodes.SetVariable(var_index, _) ->
      "SetVariable(X" <> int.to_string(var_index) <> ", " <> mode_word(op) <> ")"
    _ -> string.inspect(op)
  }
}

fn mode_word(op: Op) -> String {
  case opcodes.mnemonic(op) |> string.contains("reader") {
    True -> "reader"
    False -> "writer"
  }
}

// ==============================================================================
// Positional X-register file (FR-006)
// ==============================================================================

/// The positional X-register file of one activation: register index → term
/// (argument registers A0..An-1 and clause variables Xi share the positional
/// model of the canonical bytecode). Immutable — `set` returns a new file.
pub opaque type XRegs {
  XRegs(slots: Dict(Int, Term))
}

/// An empty register file.
pub fn new_regs() -> XRegs {
  XRegs(slots: dict.new())
}

/// Read register `index`; `Error(Nil)` if never written.
pub fn get_reg(regs: XRegs, index: Int) -> Result(Term, Nil) {
  dict.get(regs.slots, index)
}

/// Write register `index`.
pub fn set_reg(regs: XRegs, index: Int, value: Term) -> XRegs {
  XRegs(slots: dict.insert(regs.slots, index, value))
}
