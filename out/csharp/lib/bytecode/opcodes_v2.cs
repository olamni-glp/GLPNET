/// Phase 2: Unified Instruction Set
///
/// This file contains the v2 instruction set with unified variable instructions.
/// Key change: Instead of separate Writer/Reader instructions, use a single
/// instruction with an isReader flag.
///
/// Benefits:
/// - Simpler instruction set (fewer opcodes)
/// - Better code reuse in interpreter
/// - Clearer semantics
/// - Foundation for register allocation optimizations

namespace GlpRuntime.Bytecode.V2;

/// Base interface for v2 instructions.
/// All v2 instructions implement this to distinguish them from v1 Op.
public interface IOpV2 { }

// ============================================================================
// HEAD PHASE - Unified Instructions
// ============================================================================

/// Match variable in clause head (unifies HeadWriter and HeadReader).
/// Behavior depends on IsReader flag:
/// - IsReader=false (writer): Tentatively bind in σ̂w
/// - IsReader=true (reader): Add to Si if unbound
public class HeadVariable : IOpV2
{
    /// <summary>Clause variable index.</summary>
    public long VarIndex { get; }

    /// <summary>True for reader mode, false for writer mode.</summary>
    public bool IsReader { get; }

    public HeadVariable(long varIndex, bool isReader)
    {
        VarIndex = varIndex;
        IsReader = isReader;
    }

    public string Mnemonic => IsReader ? "head_reader" : "head_writer";

    public override string ToString() => $"{Mnemonic}({VarIndex})";
}

/// Get variable from argument register - first occurrence (unifies GetWriterVariable and GetReaderVariable).
/// Used in HEAD phase to load argument into clause variable for first occurrence.
/// Behavior depends on IsReader flag:
/// - IsReader=false (writer): Load argument as writer into VarIndex
/// - IsReader=true (reader): Load argument as reader into VarIndex
public class GetVariable : IOpV2
{
    /// <summary>Clause variable index.</summary>
    public long VarIndex { get; }

    /// <summary>Argument register.</summary>
    public long ArgSlot { get; }

    /// <summary>True for reader mode, false for writer mode.</summary>
    public bool IsReader { get; }

    public GetVariable(long varIndex, long argSlot, bool isReader)
    {
        VarIndex = varIndex;
        ArgSlot = argSlot;
        IsReader = isReader;
    }

    public string Mnemonic => IsReader ? "get_reader_variable" : "get_writer_variable";

    public override string ToString() => $"{Mnemonic}(X{VarIndex}, A{ArgSlot})";
}

/// Get value from argument register - subsequent occurrence (unifies GetWriterValue and GetReaderValue).
/// Used in HEAD phase to unify argument with existing clause variable.
/// Behavior depends on IsReader flag:
/// - IsReader=false (writer): Unify argument with writer in VarIndex
/// - IsReader=true (reader): Unify argument with reader in VarIndex
public class GetValue : IOpV2
{
    /// <summary>Clause variable index.</summary>
    public long VarIndex { get; }

    /// <summary>Argument register.</summary>
    public long ArgSlot { get; }

    /// <summary>True for reader mode, false for writer mode.</summary>
    public bool IsReader { get; }

    public GetValue(long varIndex, long argSlot, bool isReader)
    {
        VarIndex = varIndex;
        ArgSlot = argSlot;
        IsReader = isReader;
    }

    public string Mnemonic => IsReader ? "get_reader_value" : "get_writer_value";

    public override string ToString() => $"{Mnemonic}(X{VarIndex}, A{ArgSlot})";
}

// ============================================================================
// STRUCTURE TRAVERSAL - Unified Instructions
// ============================================================================

/// Match variable at current S position in structure (unifies UnifyWriter and UnifyReader).
/// Operates in READ or WRITE mode based on HeadStructure/PutStructure context.
/// Behavior depends on IsReader flag:
/// - IsReader=false (writer): Unify with writer variable
/// - IsReader=true (reader): Unify with reader variable (may add to Si)
public class UnifyVariable : IOpV2
{
    /// <summary>Clause variable index.</summary>
    public long VarIndex { get; }

    /// <summary>True for reader mode, false for writer mode.</summary>
    public bool IsReader { get; }

    public UnifyVariable(long varIndex, bool isReader)
    {
        VarIndex = varIndex;
        IsReader = isReader;
    }

    public string Mnemonic => IsReader ? "unify_reader" : "unify_writer";

    public override string ToString() => $"{Mnemonic}({VarIndex})";
}

// ============================================================================
// BODY PHASE - Unified Instructions
// ============================================================================

/// Place variable into argument register (unifies PutWriter and PutReader).
/// Used in BODY phase to pass variables to spawned goals.
/// Behavior depends on IsReader flag:
/// - IsReader=false (writer): Place writer from VarIndex into ArgSlot
/// - IsReader=true (reader): Derive reader from writer, place into ArgSlot
public class PutVariable : IOpV2
{
    /// <summary>Clause variable index holding writer ID.</summary>
    public long VarIndex { get; }

    /// <summary>Target argument register.</summary>
    public long ArgSlot { get; }

    /// <summary>True for reader mode, false for writer mode.</summary>
    public bool IsReader { get; }

    public PutVariable(long varIndex, long argSlot, bool isReader)
    {
        VarIndex = varIndex;
        ArgSlot = argSlot;
        IsReader = isReader;
    }

    public string Mnemonic => IsReader ? "put_reader" : "put_writer";

    public override string ToString() => $"{Mnemonic}(X{VarIndex}, A{ArgSlot})";
}

/// Build structure argument (unifies SetWriter and SetReader).
/// Used in BODY phase WRITE mode to construct structure subterms.
/// Behavior depends on IsReader flag:
/// - IsReader=false (writer): Create writer, store in VarIndex, add WriterTerm to heap
/// - IsReader=true (reader): Derive reader from writer in VarIndex, add ReaderTerm to heap
public class SetVariable : IOpV2
{
    /// <summary>Clause variable index.</summary>
    public long VarIndex { get; }

    /// <summary>True for reader mode, false for writer mode.</summary>
    public bool IsReader { get; }

    public SetVariable(long varIndex, bool isReader)
    {
        VarIndex = varIndex;
        IsReader = isReader;
    }

    public string Mnemonic => IsReader ? "set_reader" : "set_writer";

    public override string ToString() => $"{Mnemonic}(X{VarIndex})";
}

// ============================================================================
// GUARD PHASE - Guard Instructions
// ============================================================================

/// Test if variable is unbound (value unknown).
/// Succeeds if the variable is unbound, fails if bound to any value.
/// Used for dispatch based on binding status.
public class Unknown : IOpV2
{
    /// <summary>Clause variable index to test.</summary>
    public long VarIndex { get; }

    public Unknown(long varIndex)
    {
        VarIndex = varIndex;
    }

    public string Mnemonic => "unknown";

    public override string ToString() => $"unknown(X{VarIndex})";
}

// ============================================================================
// Note: V1 migration functions removed - codegen now emits V2 directly
// ============================================================================
