//// glp/bytecode/opcodes — the v2.16 GLP bytecode instruction set (feature 050, T006).
////
//// Normative spec: docs/glp-bytecode-v216-complete.md (v2.16.3).
//// Port refs: glp_runtime/lib/bytecode/opcodes.dart + opcodes_v2.dart.
////
//// The Dart reference splits the set across two class hierarchies (v1 `Op`,
//// v2 `OpV2`) and mixes both in one `List<dynamic>` instruction stream. Gleam
//// models the SAME instruction inventory as a single union — no instruction is
//// added, renamed, or renumbered (dossier discipline D4).
////
//// Deliberately NOT ported (these are removals the spec/reference already made,
//// not port choices):
////   - the §12.8/§13.1 REMOVED v1 forms (2-arg GetVariable/GetValue,
////     Get/Put/Set/Unify Writer|Reader pairs) — subsumed by the unified
////     `is_reader` instructions below;
////   - `UnionSiAndGoto`/`ResetAndGoto` — @deprecated in the reference, subsumed
////     by `ClauseNext` (§2.2);
////   - `HeadBindWriter`/`GuardNeedReader` (+ `*Arg` variants),
////     `RequireWriterArg`/`RequireReaderArg`, `BodySetConst`/
////     `BodySetStructConstArgs`/`BodySetConstArg`, `GuardFail`, `SuspendEnd`,
////     `TailStep` — reachable only from the Dart `asm.dart` test helpers; never
////     emitted by production codegen; absent from the normative spec.
////
//// Spec-gap note (REPORTED, feature 050): `NoReaders`, `GroundEqual`,
//// `PutBoundConst`, `PutBoundNil`, `Distribute`, `Transmit` are live in the
//// reference (emitted by glp_runtime/lib/compiler/codegen.dart, dispatched by
//// runner.dart) but have no section in docs/glp-bytecode-v216-complete.md.
//// They are ported here because corpus parity requires them; the doc gap is
//// escalated separately, not patched silently.

import glp/runtime/terms.{type Constant}

/// Symbolic jump-target name (Dart `typedef LabelName = String`).
pub type LabelName =
  String

/// One v2.16 bytecode instruction.
pub type Op {
  // ===== Control (spec §2, §3, §9, §14) =====
  /// §13.3 — mark jump target; no runtime effect. Indexed at program build
  /// (first occurrence wins, for multi-clause procedures).
  Label(name: LabelName)
  /// §2.1 clause_try — begin a clause attempt.
  ClauseTry
  /// §2.2 clause_next — discard σ̂w; jump to the label of the next clause.
  ClauseNext(label: LabelName)
  /// §2.3 try_next_clause — head/guard failure during selection: discard σ̂w,
  /// try the next clause.
  TryNextClause
  /// §2.4 no_more_clauses — all clauses exhausted: suspend if the goal-level
  /// suspension set is non-empty, else permanent failure.
  NoMoreClauses
  /// §3.1 commit — resolve deferred suspensions (S' = Si \ dom σ̂w), then apply
  /// σ̂w to the heap and enter BODY phase.
  Commit
  /// §9.3 proceed — clause body complete; return control to the scheduler.
  Proceed
  /// §9.1 spawn P/n — schedule a new goal for procedure P with args A1..An.
  Spawn(procedure_label: LabelName, arity: Int)
  /// §9.2 requeue P/n — tail call reusing the current goal frame (fairness via
  /// the reduction budget).
  Requeue(procedure_label: LabelName, arity: Int)
  /// §9.4 allocate n — push an environment frame with n permanent slots.
  Allocate(slots: Int)
  /// §9.5 deallocate — pop the current environment frame.
  Deallocate
  /// §14.1 nop — advance PC only.
  Nop
  /// §14 halt — mark the goal completed; return to the scheduler.
  Halt

  // ===== HEAD phase (spec §6) =====
  /// §6.1 head_structure f/n, Ai — match structure functor/arity at argument
  /// register Ai; sets READ/WRITE mode and the S register.
  HeadStructure(functor: String, arity: Int, arg_slot: Int)
  /// §6.2/§6.3 head_writer/head_reader Xi — unified HeadVariable: writer
  /// (is_reader: False) tentatively binds in σ̂w; reader (is_reader: True) adds
  /// to Si if unbound.
  HeadVariable(var_index: Int, is_reader: Bool)
  /// §6.4 head_constant c, Ai — match constant c against Ai (writer → σ̂w;
  /// unbound reader → Si; ground → equality check).
  HeadConstant(value: Constant, arg_slot: Int)
  /// §6.5 head_nil Ai — optimized head_constant for [].
  HeadNil(arg_slot: Int)
  /// §6.6 head_list Ai — optimized head_structure for './2' list cells.
  HeadList(arg_slot: Int)

  // ===== Mode-aware argument loading (spec §12) =====
  /// §12.1/§12.2 — first occurrence: load argument Ai into clause variable Xi
  /// (writer or reader view per is_reader).
  GetVariable(var_index: Int, arg_slot: Int, is_reader: Bool)
  /// §12.3/§12.4 — subsequent occurrence: unify argument Ai with clause
  /// variable Xi under the writer-MGU discipline.
  GetValue(var_index: Int, arg_slot: Int, is_reader: Bool)

  // ===== BODY construction (spec §7) =====
  /// §7.1 put_structure f/n, Ai — create structure on the heap, ref into Ai,
  /// enter WRITE mode.
  PutStructure(functor: String, arity: Int, arg_slot: Int)
  /// §7.2/§7.3 put_writer/put_reader Xi, Ai — unified PutVariable: place the
  /// writer from Xi (or a derived reader view) into Ai.
  PutVariable(var_index: Int, arg_slot: Int, is_reader: Bool)
  /// §7.4 put_constant c, Ai.
  PutConstant(value: Constant, arg_slot: Int)
  /// §7.5 put_nil Ai — optimized put_constant for [].
  PutNil(arg_slot: Int)
  /// §7.6 put_list Ai — optimized put_structure for './2'.
  PutList(arg_slot: Int)
  /// Reader-to-bound-constant argument (REPL/query building; spec-gap: not in
  /// the v2.16 doc — see module note).
  PutBoundConst(value: Constant, arg_slot: Int)
  /// Reader-to-bound-nil argument (REPL/query building; spec-gap — see module
  /// note).
  PutBoundNil(arg_slot: Int)

  // ===== Structure building at S (spec §8) =====
  /// §8.1/§8.2 unify_writer/unify_reader Xi — unified UnifyVariable at the
  /// current S position (READ or WRITE mode).
  UnifyVariable(var_index: Int, is_reader: Bool)
  /// §8.3 constant c (READ-mode match at S).
  UnifyConstant(value: Constant)
  /// §8.1/§8.2 WRITE-mode variant used by BODY structure construction
  /// (set_writer/set_reader — Dart SetVariable).
  SetVariable(var_index: Int, is_reader: Bool)
  /// §8.3 constant c (WRITE-mode store at S — Dart SetConstant).
  SetConstant(value: Constant)
  /// §8.4 void n — skip (READ) or create fresh (WRITE) n positions.
  UnifyVoid(count: Int)
  /// §8.6 push Xi — save (S, mode, current structure) before a nested
  /// structure.
  Push(reg_index: Int)
  /// §8.6 pop Xi — restore the state saved by the matching Push.
  Pop(reg_index: Int)
  /// §8.7 unify_structure f/n — match/create a nested structure at S and enter
  /// it.
  UnifyStructure(functor: String, arity: Int)

  // ===== Guards (spec §11; three-valued: succeed/suspend/fail) =====
  /// §11.1 guard P, Args — call guard predicate P/n (negated: invert
  /// success/failure; suspension unchanged).
  Guard(procedure_label: LabelName, arity: Int, negated: Bool)
  /// §11.2 ground X — succeed iff X contains no unbound variables.
  Ground(var_index: Int, negated: Bool)
  /// §11.3 known X — succeed iff X is not an unbound variable.
  Known(var_index: Int, negated: Bool)
  /// §11.5 unknown X — succeed iff X is unbound.
  Unknown(var_index: Int)
  /// §11.4 otherwise — succeed iff all previous clauses FAILED (not
  /// suspended).
  Otherwise
  /// no_readers X — succeed iff the term contains no readers; suspend if it
  /// does; never fails (spec-gap: not in the v2.16 doc — see module note).
  NoReaders(var_index: Int, negated: Bool)
  /// X =?= Y — ground structural equality; suspend while either side has
  /// unbound readers (spec-gap — see module note).
  GroundEqual(left_var_index: Int, right_var_index: Int, negated: Bool)

  // ===== Module system (FCP distribute/transmit; spec-gap — see module note) =====
  /// Static RPC to the imported module at 1-based import_index.
  Distribute(import_index: Int, functor: String, arity: Int)
  /// Dynamic RPC to a module resolved at runtime from register
  /// module_var_index.
  Transmit(module_var_index: Int, functor: String, arity: Int)
}

/// The spec mnemonic for an instruction, as used by the v2.16 doc and the Dart
/// disassembler (`is_reader` selects the writer/reader mnemonic of the unified
/// instructions). Total over the inventory — the T012 table-integrity test
/// asserts every variant renders.
pub fn mnemonic(op: Op) -> String {
  case op {
    Label(..) -> "label"
    ClauseTry -> "clause_try"
    ClauseNext(..) -> "clause_next"
    TryNextClause -> "try_next_clause"
    NoMoreClauses -> "no_more_clauses"
    Commit -> "commit"
    Proceed -> "proceed"
    Spawn(..) -> "spawn"
    Requeue(..) -> "requeue"
    Allocate(..) -> "allocate"
    Deallocate -> "deallocate"
    Nop -> "nop"
    Halt -> "halt"
    HeadStructure(..) -> "head_structure"
    HeadVariable(_, is_reader) ->
      case is_reader {
        True -> "head_reader"
        False -> "head_writer"
      }
    HeadConstant(..) -> "head_constant"
    HeadNil(..) -> "head_nil"
    HeadList(..) -> "head_list"
    GetVariable(_, _, is_reader) ->
      case is_reader {
        True -> "get_reader_variable"
        False -> "get_writer_variable"
      }
    GetValue(_, _, is_reader) ->
      case is_reader {
        True -> "get_reader_value"
        False -> "get_writer_value"
      }
    PutStructure(..) -> "put_structure"
    PutVariable(_, _, is_reader) ->
      case is_reader {
        True -> "put_reader"
        False -> "put_writer"
      }
    PutConstant(..) -> "put_constant"
    PutNil(..) -> "put_nil"
    PutList(..) -> "put_list"
    PutBoundConst(..) -> "put_bound_const"
    PutBoundNil(..) -> "put_bound_nil"
    UnifyVariable(_, is_reader) ->
      case is_reader {
        True -> "unify_reader"
        False -> "unify_writer"
      }
    UnifyConstant(..) -> "unify_constant"
    SetVariable(_, is_reader) ->
      case is_reader {
        True -> "set_reader"
        False -> "set_writer"
      }
    SetConstant(..) -> "set_constant"
    UnifyVoid(..) -> "unify_void"
    Push(..) -> "push"
    Pop(..) -> "pop"
    UnifyStructure(..) -> "unify_structure"
    Guard(..) -> "guard"
    Ground(..) -> "ground"
    Known(..) -> "known"
    Unknown(..) -> "unknown"
    Otherwise -> "otherwise"
    NoReaders(..) -> "no_readers"
    GroundEqual(..) -> "ground_equal"
    Distribute(..) -> "distribute"
    Transmit(..) -> "transmit"
  }
}
