using System.Collections.Generic;

namespace GlpRuntime.Runtime;

public abstract class Term
{
    protected Term() { }
}

public sealed class ConstTerm : Term
{
    public object? Value { get; }

    public ConstTerm(object? value)
    {
        Value = value;
    }

    public override string ToString() => $"Const({Value?.ToString() ?? "null"})";
}

public sealed class StructTerm : Term
{
    public string Functor { get; }
    public IReadOnlyList<Term> Args { get; }

    public StructTerm(string functor, IReadOnlyList<Term> args)
    {
        Functor = functor;
        Args = args;
    }

    public override string ToString() => $"{Functor}({string.Join(",", Args)})";
}

/// <summary>
/// Variable reference - holds heap address only.
/// <para>
/// Per irmaGLP-spec.md Section 3.2.1:
/// A variable's reader/writer identity is determined by its heap cell tag
/// (RoTag or WrtTag), NOT by address arithmetic. Use heap.IsWriter(addr)
/// or heap.IsReader(addr) to check.
/// </para>
/// <para>
/// MUST NOT: Code must not assume reader_addr == writer_addr + 1 or
/// derive reader/writer identity from address parity.
/// </para>
/// </summary>
public sealed class VarRef : Term, IEquatable<VarRef>
{
    /// <summary>The heap address of this variable reference.</summary>
    public int Addr { get; }

    public VarRef(int addr)
    {
        Addr = addr;
    }

    // NOTE: isReader and varId computed properties have been REMOVED per
    // irmaGLP-spec.md Section 3.2.1. Use heap.IsReader(addr) to check type
    // and raw addr as the identifier.

    public override string ToString() => $"Var@{Addr}";

    public override bool Equals(object? other) => other is VarRef v && v.Addr == Addr;

    public bool Equals(VarRef? other) => other is not null && other.Addr == Addr;

    public override int GetHashCode() => Addr.GetHashCode();

    public static bool operator ==(VarRef? left, VarRef? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(VarRef? left, VarRef? right) => !(left == right);
}

/// <summary>
/// Mutable reference to an unbound writer — enables O(1) stream append.
/// <para>
/// MutualRef holds a mutable pointer to the current "end" of a stream.
/// Multiple goals can share a MutualRef and append to the same stream
/// in constant time, without traversing the stream.
/// </para>
/// <para>
/// Usage:
/// <list type="bullet">
/// <item>Create with mutual_ref(StreamEnd, Ref) where StreamEnd is unbound writer</item>
/// <item>Append with stream_append(Ref, Value, NewEnd) — O(1) operation</item>
/// <item>Close with mutual_ref_close(Ref) to terminate stream with []</item>
/// </list>
/// </para>
/// <para>SRSW: MutualRefTerm is treated as ground (can be read multiple times).</para>
/// <para>
/// Per heap-pointer-architecture-spec.md v3.0:
/// _currentWriterAddr holds the heap address of the current unbound tail writer.
/// </para>
/// </summary>
public sealed class MutualRefTerm : Term, IEquatable<MutualRefTerm>
{
    private int _currentWriterAddr; // heap address of current unbound tail writer
    public int Id { get; }          // unique ID for this MutualRef

    private static int _nextId = 0;

    public MutualRefTerm(int currentWriterAddr)
    {
        _currentWriterAddr = currentWriterAddr;
        Id = _nextId++;
    }

    /// <summary>Get/set the current writer address.</summary>
    public int CurrentWriterAddr
    {
        get => _currentWriterAddr;
        set => _currentWriterAddr = value;
    }

    public override string ToString() => $"MutualRef#{Id}(@{_currentWriterAddr})";

    public override bool Equals(object? other) => other is MutualRefTerm m && m.Id == Id;

    public bool Equals(MutualRefTerm? other) => other is not null && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(MutualRefTerm? left, MutualRefTerm? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(MutualRefTerm? left, MutualRefTerm? right) => !(left == right);
}

/// <summary>
/// Module reference term — wraps a compiled BytecodeProgram.
/// <para>
/// Used by the '_activate' body kernel to resolve goals against a module's
/// _select/1 dispatch table. The module binary is represented as a ground
/// term on the heap, following FCP's convention.
/// </para>
/// </summary>
public sealed class ModuleTerm : Term
{
    /// <summary>The compiled bytecode program for this module. Typed as object to avoid circular import.</summary>
    public object Bytecode { get; } // BytecodeProgram (untyped to avoid circular import)

    /// <summary>Module name (for display/debugging).</summary>
    public string Name { get; }

    public ModuleTerm(object bytecode, string name = "")
    {
        Bytecode = bytecode;
        Name = name;
    }

    public override string ToString() => $"Module({Name})";
}
