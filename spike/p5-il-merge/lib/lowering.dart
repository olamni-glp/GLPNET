/// Lowering: glp_runtime AST (analyzer-annotated) -> 4-primitive IL -> v2.16.3
/// BytecodeProgram. The IL->bytecode step REUSES glp_runtime's opcode classes
/// and mirrors CodeGenerator's emit logic for HEAD/COMMIT/BODY so the result is
/// byte-equivalent to the stock emitter for this clause. The front-end (lexer/
/// parser/analyzer/codegen) is used read-only.
library;

import 'package:glp_runtime/bytecode/opcodes.dart' as bc;
import 'package:glp_runtime/bytecode/opcodes_v2.dart' as bcv2;
import 'package:glp_runtime/bytecode/runner.dart' show BytecodeProgram;
import 'package:glp_runtime/compiler/ast.dart';
import 'package:glp_runtime/compiler/analyzer.dart' show AnnotatedClause, VariableTable;

import 'il.dart';

// ===========================================================================
// AST (annotated clause) -> IL
// ===========================================================================

/// Lower one analyzer-annotated clause into the 4-primitive IL.
IlClause lowerClauseToIl(String procSig, int clauseIndex, AnnotatedClause clause) {
  final ast = clause.ast;
  final ops = <IlOp>[];

  // head_unify, one per head argument position
  for (int i = 0; i < ast.head.args.length; i++) {
    ops.add(HeadUnify(i, ast.head.args[i]));
  }
  // guard_test, one per guard (none for merge/3 clause 1)
  if (clause.hasGuards && ast.guards != null) {
    for (final g in ast.guards!) {
      ops.add(GuardTest(g));
    }
  }
  // body_spawn, one per body goal
  if (clause.hasBody && ast.body != null) {
    for (final goal in ast.body!) {
      ops.add(BodySpawn(goal));
    }
  }

  // suspend_reactivate metadata: head readers + whether any head arg is a struct
  final readers = <String>[];
  var hasStruct = false;
  for (final arg in ast.head.args) {
    if (arg is ListTerm && !arg.isNil) hasStruct = true;
    if (arg is StructTerm) hasStruct = true;
    _collectHeadReaders(arg, readers);
  }

  return IlClause(
    procSig: procSig,
    clauseIndex: clauseIndex,
    ops: ops,
    suspend: SuspendReactivate(readers, hasStruct),
  );
}

void _collectHeadReaders(Term t, List<String> out) {
  if (t is VarTerm) {
    if (t.isReader) out.add(t.name);
  } else if (t is ListTerm) {
    if (t.head != null) _collectHeadReaders(t.head!, out);
    if (t.tail != null) _collectHeadReaders(t.tail!, out);
  } else if (t is StructTerm) {
    for (final a in t.args) {
      _collectHeadReaders(a, out);
    }
  }
}

// ===========================================================================
// IL -> v2.16.3 bytecode (mirrors CodeGenerator)
// ===========================================================================

class _Emit {
  final List<dynamic> instructions = [];
  final VariableTable varTable;
  final Set<String> seenHeadVars = {};
  int nextTempVar;

  _Emit(this.varTable) : nextTempVar = 0 {
    final count = varTable.getAllVars().length;
    nextTempVar = count > 10 ? count : 10; // mirrors CodeGenContext.resetTemps
  }

  void emit(dynamic op) => instructions.add(op);
  int allocateTemp() => nextTempVar++;

  int reg(String name) {
    final info = varTable.getVar(name);
    if (info == null) {
      throw StateError('IL lowering: undefined variable "$name"');
    }
    return info.registerIndex!;
  }
}

/// Lower an IL clause to a complete single-procedure BytecodeProgram, wrapped
/// exactly as CodeGenerator._generateProcedure / _generateClause do:
///   Label(sig); ClauseTry; <head>; [<guards>]; Commit; <body|Proceed>;
///   Label(sig_end); NoMoreClauses
BytecodeProgram lowerIlToBytecode(IlClause clause, VariableTable varTable) {
  final e = _Emit(varTable);
  final sig = clause.procSig;

  e.emit(bc.Label(sig)); // procedure entry label
  // (clauseIndex 0 ⇒ no per-clause label, matching CodeGenerator)
  e.emit(bc.ClauseTry());

  // HEAD phase
  for (final h in clause.headOps) {
    _emitHeadArgument(e, h.term, h.argSlot);
  }

  // GUARD phase
  for (final g in clause.guardOps) {
    _emitGuard(e, g);
  }

  // COMMIT
  e.emit(bc.Commit());

  // BODY phase
  final body = clause.bodyOps;
  if (body.isEmpty) {
    e.emit(bc.Proceed());
  } else if (body.length == 1 &&
      body.first.goal.functor == 'true' &&
      body.first.goal.arity == 0) {
    e.emit(bc.Proceed()); // body "true" ⇒ fact, mirrors CodeGenerator
  } else {
    for (final s in body) {
      for (int j = 0; j < s.goal.args.length; j++) {
        _emitPutArgument(e, s.goal.args[j], j);
      }
      e.emit(bc.Spawn('${s.goal.functor}/${s.goal.arity}', s.goal.arity));
    }
    e.emit(bc.Proceed());
  }

  e.emit(bc.Label('${sig}_end'));
  e.emit(bc.NoMoreClauses());

  return BytecodeProgram(e.instructions);
}

// --- HEAD argument (mirrors CodeGenerator._generateHeadArgument) ---
void _emitHeadArgument(_Emit e, Term term, int argSlot) {
  if (term is VarTerm) {
    final regIndex = e.reg(term.name);
    final isFirst = !e.seenHeadVars.contains(term.name);
    if (isFirst) {
      e.emit(bcv2.GetVariable(regIndex, argSlot, isReader: term.isReader));
      e.seenHeadVars.add(term.name);
    } else {
      e.emit(bcv2.GetValue(regIndex, argSlot, isReader: term.isReader));
    }
  } else if (term is ConstTerm) {
    e.emit(bc.HeadConstant(term.value, argSlot));
  } else if (term is ListTerm) {
    if (term.isNil) {
      e.emit(bc.HeadNil(argSlot));
    } else {
      e.emit(bc.HeadStructure('.', 2, argSlot));
      if (term.head != null) _emitStructureElement(e, term.head!, inHead: true);
      if (term.tail != null) _emitStructureElement(e, term.tail!, inHead: true);
    }
  } else if (term is StructTerm) {
    final tempReg = e.allocateTemp();
    e.emit(bc.GetVariable(tempReg, argSlot));
    e.emit(bc.HeadStructure(term.functor, term.arity, tempReg));
    for (final subArg in term.args) {
      _emitStructureElement(e, subArg, inHead: true);
    }
  } else if (term is UnderscoreTerm) {
    // anonymous direct head arg: not extracted (mirrors CodeGenerator)
  }
}

// --- structure element (mirrors CodeGenerator._generateStructureElement) ---
void _emitStructureElement(_Emit e, Term term, {required bool inHead}) {
  if (term is VarTerm) {
    e.emit(bcv2.UnifyVariable(e.reg(term.name), isReader: term.isReader));
  } else if (term is ConstTerm) {
    e.emit(bc.UnifyConstant(term.value));
  } else if (term is ListTerm) {
    if (term.isNil) {
      e.emit(bc.UnifyConstant('nil'));
    } else if (inHead) {
      final saveReg = e.allocateTemp();
      e.emit(bc.Push(saveReg));
      e.emit(bc.UnifyStructure('.', 2));
      if (term.head != null) _emitStructureElement(e, term.head!, inHead: true);
      if (term.tail != null) _emitStructureElement(e, term.tail!, inHead: true);
      e.emit(bc.Pop(saveReg));
      e.emit(bcv2.UnifyVariable(saveReg, isReader: false));
    } else {
      final tempReg = e.allocateTemp();
      e.emit(bc.PutStructure('.', 2, tempReg));
      if (term.head != null) _emitStructureElement(e, term.head!, inHead: false);
      if (term.tail != null) _emitStructureElement(e, term.tail!, inHead: false);
      e.emit(bcv2.UnifyVariable(tempReg, isReader: false));
    }
  } else if (term is StructTerm) {
    if (inHead) {
      final saveReg = e.allocateTemp();
      e.emit(bc.Push(saveReg));
      e.emit(bc.UnifyStructure(term.functor, term.arity));
      for (final subArg in term.args) {
        _emitStructureElement(e, subArg, inHead: true);
      }
      e.emit(bc.Pop(saveReg));
      e.emit(bcv2.UnifyVariable(saveReg, isReader: false));
    } else {
      final tempReg = e.allocateTemp();
      e.emit(bc.PutStructure(term.functor, term.arity, tempReg));
      for (final subArg in term.args) {
        _emitStructureElement(e, subArg, inHead: false);
      }
      e.emit(bcv2.UnifyVariable(tempReg, isReader: false));
    }
  } else if (term is UnderscoreTerm) {
    e.emit(bc.UnifyVoid(count: 1));
  }
}

// --- guard (mirrors CodeGenerator._generateGuard, common built-ins) ---
void _emitGuard(_Emit e, GuardTest g) {
  final guard = g.guard;
  if (guard.predicate == 'ground' && guard.args.length == 1) {
    final a = guard.args[0];
    if (a is VarTerm) {
      e.emit(bc.Ground(e.reg(a.name), negated: guard.negated));
      return;
    }
  }
  if (guard.predicate == 'known' && guard.args.length == 1) {
    final a = guard.args[0];
    if (a is VarTerm) {
      e.emit(bc.Known(e.reg(a.name), negated: guard.negated));
      return;
    }
  }
  if (guard.predicate == 'otherwise' && guard.args.isEmpty) {
    e.emit(bc.Otherwise());
    return;
  }
  if (guard.predicate == '=?=' && guard.args.length == 2) {
    final l = guard.args[0], r = guard.args[1];
    if (l is VarTerm && r is VarTerm) {
      e.emit(bc.GroundEqual(e.reg(l.name), e.reg(r.name), negated: guard.negated));
      return;
    }
  }
  // generic guard call
  for (int i = 0; i < guard.args.length; i++) {
    _emitPutArgument(e, guard.args[i], i);
  }
  e.emit(bc.Guard(guard.predicate, guard.args.length, negated: guard.negated));
}

// --- body put-argument (mirrors CodeGenerator._generatePutArgument) ---
void _emitPutArgument(_Emit e, Term term, int argSlot) {
  if (term is VarTerm) {
    e.emit(bcv2.PutVariable(e.reg(term.name), argSlot, isReader: term.isReader));
  } else if (term is ConstTerm) {
    e.emit(bc.PutBoundConst(term.value, argSlot));
  } else if (term is ListTerm) {
    if (term.isNil) {
      e.emit(bc.PutBoundNil(argSlot));
    } else {
      e.emit(bc.PutStructure('.', 2, argSlot));
      if (term.head != null) _emitArgStructureElement(e, term.head!);
      if (term.tail != null) _emitArgStructureElement(e, term.tail!);
    }
  } else if (term is StructTerm) {
    e.emit(bc.PutStructure(term.functor, term.arity, argSlot));
    for (final a in term.args) {
      _emitArgStructureElement(e, a);
    }
  } else if (term is UnderscoreTerm) {
    final tempReg = e.allocateTemp();
    e.emit(bcv2.PutVariable(tempReg, argSlot, isReader: false));
  }
}

// --- arg structure element (mirrors CodeGenerator._generateArgumentStructureElement,
//     variable + constant cases; merge/3 clause 1 needs no nested arg structures) ---
void _emitArgStructureElement(_Emit e, Term term) {
  if (term is VarTerm) {
    e.emit(bcv2.UnifyVariable(e.reg(term.name), isReader: term.isReader));
  } else if (term is ConstTerm) {
    e.emit(bc.UnifyConstant(term.value));
  } else {
    throw UnsupportedError(
        'arg structure element ${term.runtimeType} not needed for this spike clause');
  }
}

// ===========================================================================
// Field-level disassembler (used for BOTH programs so the diff is meaningful)
// ===========================================================================

String disasm(List<dynamic> ops) {
  final b = StringBuffer();
  for (int i = 0; i < ops.length; i++) {
    b.writeln('PC ${i.toString().padLeft(2)}: ${_fmt(ops[i])}');
  }
  return b.toString();
}

String _mode(bool isReader) => isReader ? 'reader' : 'writer';

String _fmt(dynamic op) {
  if (op is bc.Label) return 'Label("${op.name}")';
  if (op is bc.ClauseTry) return 'ClauseTry';
  if (op is bc.Commit) return 'Commit';
  if (op is bc.Proceed) return 'Proceed';
  if (op is bc.NoMoreClauses) return 'NoMoreClauses';
  if (op is bc.TryNextClause) return 'TryNextClause';
  if (op is bc.ClauseNext) return 'ClauseNext("${op.label}")';
  if (op is bc.HeadStructure) {
    return 'HeadStructure("${op.functor}", ${op.arity}, A${op.argSlot})';
  }
  if (op is bc.HeadNil) return 'HeadNil(A${op.argSlot})';
  if (op is bc.HeadConstant) return 'HeadConstant(${op.value}, A${op.argSlot})';
  if (op is bc.HeadList) return 'HeadList(A${op.argSlot})';
  if (op is bc.UnifyConstant) return 'UnifyConstant(${op.value})';
  if (op is bc.UnifyVoid) return 'UnifyVoid(${op.count})';
  if (op is bc.UnifyStructure) return 'UnifyStructure("${op.functor}", ${op.arity})';
  if (op is bc.Push) return 'Push(X${op.regIndex})';
  if (op is bc.Pop) return 'Pop(X${op.regIndex})';
  if (op is bc.Spawn) return 'Spawn("${op.procedureLabel}", ${op.arity})';
  if (op is bc.Requeue) return 'Requeue("${op.procedureLabel}", ${op.arity})';
  if (op is bc.PutBoundConst) return 'PutBoundConst(${op.value}, A${op.argSlot})';
  if (op is bc.PutBoundNil) return 'PutBoundNil(A${op.argSlot})';
  if (op is bc.PutStructure) {
    return 'PutStructure("${op.functor}", ${op.arity}, A${op.argSlot})';
  }
  if (op is bc.SetConstant) return 'SetConstant(${op.value})';
  if (op is bc.Ground) return 'Ground(X${op.varIndex}${op.negated ? ", ~" : ""})';
  if (op is bc.Known) return 'Known(X${op.varIndex}${op.negated ? ", ~" : ""})';
  if (op is bc.Otherwise) return 'Otherwise';
  if (op is bc.GroundEqual) return op.toString();
  // v1 GetVariable/GetValue (used for struct-as-head-arg extraction)
  if (op is bc.GetVariable) return 'GetVariable.v1(X${op.varIndex}, A${op.argSlot})';
  if (op is bc.GetValue) return 'GetValue.v1(X${op.varIndex}, A${op.argSlot})';
  // v2 unified ops
  if (op is bcv2.HeadVariable) return 'HeadVariable(X${op.varIndex}, ${_mode(op.isReader)})';
  if (op is bcv2.GetVariable) {
    return 'GetVariable(X${op.varIndex}, A${op.argSlot}, ${_mode(op.isReader)})';
  }
  if (op is bcv2.GetValue) {
    return 'GetValue(X${op.varIndex}, A${op.argSlot}, ${_mode(op.isReader)})';
  }
  if (op is bcv2.UnifyVariable) return 'UnifyVariable(X${op.varIndex}, ${_mode(op.isReader)})';
  if (op is bcv2.PutVariable) {
    return 'PutVariable(X${op.varIndex}, A${op.argSlot}, ${_mode(op.isReader)})';
  }
  if (op is bcv2.SetVariable) return 'SetVariable(X${op.varIndex}, ${_mode(op.isReader)})';
  if (op is bcv2.Unknown) return 'Unknown(X${op.varIndex})';
  return 'UNKNOWN(${op.runtimeType})';
}
