// Bytecode assembler facade — dual naming surface (camelCase + UPPERCASE aliases).
// Converted from lib/bytecode/asm.dart.
// SCREAMING_CASE method names are intentional (mirrors Dart source 1-to-1 for
// assembly-style test readability); IDE1006 is off by default and not suppressed.

using GlpRuntime.Bytecode;
using V2 = GlpRuntime.Bytecode.V2;

namespace GlpRuntime.Bytecode;

/// <summary>
/// Pure namespace-of-helpers: static factory methods for every GLP bytecode opcode.
/// Two parallel naming surfaces are exposed: lowerCamelCase short forms and UPPERCASE
/// aliases that forward to them. Both surfaces are load-bearing for compiler/codegen
/// and test files — do not prune either.
/// </summary>
public static class BC
{
    // lowerCamelCase helpers

    public static Label l(string name) => new Label(name);
    // Dart `try_` (trailing-underscore keyword escape) → C# `@try` (leading-@ keyword escape)
    public static ClauseTry @try() => new ClauseTry();
    public static GuardNeedReader r(long readerId) => new GuardNeedReader(readerId);
    public static HeadBindWriter w(long writerId) => new HeadBindWriter(writerId);
    public static Commit commit() => new Commit();

    // New spec-compliant control flow instructions

    public static ClauseNext clauseNext(string label) => new ClauseNext(label);
    public static TryNextClause tryNextClause() => new TryNextClause();
    public static NoMoreClauses noMoreClauses() => new NoMoreClauses();

    // Legacy (deprecated)

    [System.Obsolete]
    public static UnionSiAndGoto u(string label) => new UnionSiAndGoto(label);
    [System.Obsolete]
    public static ResetAndGoto next(string label) => new ResetAndGoto(label);

    public static SuspendEnd susp() => new SuspendEnd();
    public static Proceed proceed() => new Proceed();
    public static BodySetConst bconst(long writerId, object? v) => new BodySetConst(writerId, v);
    public static BodySetStructConstArgs bstructC(long writerId, string f, List<object?> constArgs)
        => new BodySetStructConstArgs(writerId, f, constArgs);
    public static HeadStructure headStruct(string functor, long arity, long argSlot)
        => new HeadStructure(functor, arity, argSlot);
    public static V2.HeadVariable headWriter(long varIndex)
        => new V2.HeadVariable(varIndex, isReader: false);
    public static V2.HeadVariable headReader(long varIndex)
        => new V2.HeadVariable(varIndex, isReader: true);
    public static UnifyConstant unifyConst(object? value) => new UnifyConstant(value);
    public static UnifyVoid unifyVoid(long count = 1) => new UnifyVoid(count: count);
    public static HeadConstant headConst(object? value, long argSlot)
        => new HeadConstant(value, argSlot);
    public static GetVariable getVar(long varIndex, long argSlot)
        => new GetVariable(varIndex, argSlot);
    public static GetValue getVal(long varIndex, long argSlot)
        => new GetValue(varIndex, argSlot);
    public static PutConstant putConst(object? value, long argSlot)
        => new PutConstant(value, argSlot);
    public static PutStructure putStructure(string functor, long arity, long argSlot)
        => new PutStructure(functor, arity, argSlot);
    public static SetConstant setConst(object? value) => new SetConstant(value);
    public static Otherwise otherwise() => new Otherwise();
    public static V2.Unknown unknown(long varIndex) => new V2.Unknown(varIndex);
    public static Spawn spawn(string label, long arity) => new Spawn(label, arity);
    public static Requeue requeue(string label, long arity) => new Requeue(label, arity);

    // Guard instructions

    public static Guard guard(string label, long arity) => new Guard(label, arity);
    public static Ground ground(long varIndex) => new Ground(varIndex);
    public static Known known(long varIndex) => new Known(varIndex);

    // List-specific instructions

    public static HeadNil headNil(long argSlot) => new HeadNil(argSlot);
    public static HeadList headList(long argSlot) => new HeadList(argSlot);
    public static PutNil putNil(long argSlot) => new PutNil(argSlot);
    public static PutList putList(long argSlot) => new PutList(argSlot);

    // Environment frame instructions

    public static Allocate allocate(long slots) => new Allocate(slots);
    public static Deallocate deallocate() => new Deallocate();

    // Utility instructions

    public static Nop nop() => new Nop();
    public static Halt halt() => new Halt();

    public static BytecodeProgram prog(List<IOp> ops) => new BytecodeProgram(ops);

    // UPPERCASE aliases

    public static Label L(string name) => l(name);
    public static ClauseTry TRY() => @try();
    public static GuardNeedReader R(long readerId) => r(readerId);
    public static HeadBindWriter W(long writerId) => w(writerId);
    public static Commit COMMIT() => commit();

    // New spec-compliant control flow (UPPERCASE)

    public static ClauseNext CLAUSE_NEXT(string label) => clauseNext(label);
    public static TryNextClause TRY_NEXT_CLAUSE() => tryNextClause();
    public static NoMoreClauses NO_MORE_CLAUSES() => noMoreClauses();

    // Legacy (deprecated)

    [System.Obsolete]
    public static UnionSiAndGoto U(string label) => u(label);
    [System.Obsolete]
    public static ResetAndGoto NEXT(string label) => next(label);

    public static SuspendEnd SUSP() => susp();
    public static Proceed PROCEED() => proceed();
    public static Otherwise OTHERWISE() => otherwise();
    public static V2.Unknown UNKNOWN(long varIndex) => unknown(varIndex);
    public static BodySetConst BCONST(long writerId, object? v) => bconst(writerId, v);
    public static BodySetStructConstArgs BSTRUCTC(long writerId, string f, List<object?> constArgs)
        => bstructC(writerId, f, constArgs);
}
