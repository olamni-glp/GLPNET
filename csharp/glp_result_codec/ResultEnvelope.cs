// Result-Envelope value types — feature 038-result-codec-and-framecodec-ride.
//
// The heap-independent result envelope (the result side of the ratified ED-1 seam)
// and the wire-level term model it carries. These are pure, immutable value types:
// no field, transitively, holds a live heap address (SC-003 / data-model §1 — the
// invariant is structural: there is simply no heap-handle type reachable from here).
//
// The term model mirrors the canonical 034 Gleam model (glp/runtime/terms.gleam,
// data-model §2): Constant = ConstAtom|ConstInt|ConstReal|ConstString;
// Term = ConstTerm(Constant) | StructTerm(functor,args) | VarRef(GlobalVarId).
// Unlike 034's local VarRef(addr:Int), the envelope's VarRef carries a global
// identity (GlobalVarId, data-model §3 tag 0x07) so it is meaningful cross-process.
//
// Dart is the source of truth for the cross-runtime byte codec (R9); this C# slice
// mirrors glp_runtime/lib/codec/result_envelope.dart byte-for-byte. All deep value
// equality below mirrors the Dart operator== overrides exactly (ordered-map equality,
// bit-exact double equality so NaN / -0.0 compare like the Dart reference).

using System;
using System.Collections.Generic;

namespace GlpRuntime.ResultCodec;

/// <summary>
/// Loud-fail exception for any malformed input or out-of-model value (FR-005, SC-004).
/// Mirrors Dart <c>ResultCodecException</c> / Gleam <c>CodecError</c>.
/// </summary>
public sealed class ResultCodecException : Exception
{
    public ResultCodecException(string message) : base(message) { }
    public ResultCodecException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Execution status of a result (data-model §1; wire byte in contract §4).</summary>
public enum ExecutionStatus : byte
{
    Success = 0x00,
    Suspended = 0x01,
    Failed = 0x02,
}

/// <summary>
/// Global, heap-independent identity of a GLP variable's writer (data-model §4, R7).
/// Identity is <c>(AgentId, LocalId)</c> — never a local heap address (meaningless
/// cross-process, DISPOSITIONS #15). Scheme <c>agentId:localId</c> (FB-M1-14). The
/// record gives value equality on BOTH members, matching the Dart class.
/// </summary>
public sealed record GlobalVarId(string AgentId, long LocalId)
{
    public override string ToString() => $"{AgentId}:{LocalId}";
}

// --- Constant: the four GLP ground-constant kinds (mirrors 034 terms.gleam) ---

/// <summary>A GLP ground constant — atom / int / real / string (FR-001).</summary>
public abstract class Constant
{
    private protected Constant() { }
}

/// <summary>An atom (GLP identifier; GLP booleans true/false are atoms). Wire tag 0x05.</summary>
public sealed class ConstAtom : Constant
{
    public string Name { get; }
    public ConstAtom(string name) { Name = name; }

    public override bool Equals(object? obj) => obj is ConstAtom o && o.Name == Name;
    public override int GetHashCode() => HashCode.Combine(typeof(ConstAtom), Name);
    public override string ToString() => $"ConstAtom('{Name}')";
}

/// <summary>A 64-bit integer. Wire tag 0x02 (fixed 8-byte little-endian).</summary>
public sealed class ConstInt : Constant
{
    public long Value { get; }
    public ConstInt(long value) { Value = value; }

    public override bool Equals(object? obj) => obj is ConstInt o && o.Value == Value;
    public override int GetHashCode() => HashCode.Combine(typeof(ConstInt), Value);
    public override string ToString() => $"ConstInt({Value})";
}

/// <summary>A double. Wire tag 0x03 (IEEE-754 bit pattern). **GATED** (ED-6 AtomVM /float).</summary>
public sealed class ConstReal : Constant
{
    public double Value { get; }
    public ConstReal(double value) { Value = value; }

    // Bit-exact equality so NaN / -0.0 round-trip compares correctly (mirrors the Dart
    // _doubleBitsEqual: +0.0 and -0.0 have different bits and are NOT equal).
    public override bool Equals(object? obj) =>
        obj is ConstReal o &&
        BitConverter.DoubleToInt64Bits(o.Value) == BitConverter.DoubleToInt64Bits(Value);
    public override int GetHashCode() =>
        HashCode.Combine(typeof(ConstReal), BitConverter.DoubleToInt64Bits(Value));
    public override string ToString() => $"ConstReal({Value})";
}

/// <summary>A string literal. Wire tag 0x04 (varint length + UTF-8).</summary>
public sealed class ConstString : Constant
{
    public string Value { get; }
    public ConstString(string value) { Value = value; }

    public override bool Equals(object? obj) => obj is ConstString o && o.Value == Value;
    public override int GetHashCode() => HashCode.Combine(typeof(ConstString), Value);
    public override string ToString() => $"ConstString('{Value}')";
}

// --- Term: the codec's wire-level term model (mirrors 034 terms.gleam) ---

/// <summary>
/// A GLP term. A deep-resolved binding never holds a *bound* VarRef (it is resolved
/// away); a remaining VarRef is an **unbound** variable, encoded by its GlobalVarId.
/// </summary>
public abstract class Term
{
    private protected Term() { }
}

/// <summary>A constant term (atom/int/real/string). Wire tags 0x02–0x05 by inner kind.</summary>
public sealed class ConstTerm : Term
{
    public Constant Value { get; }
    public ConstTerm(Constant value) { Value = value; }

    public override bool Equals(object? obj) => obj is ConstTerm o && o.Value.Equals(Value);
    public override int GetHashCode() => HashCode.Combine(typeof(ConstTerm), Value);
    public override string ToString() => $"ConstTerm({Value})";
}

/// <summary>
/// A compound term: functor + arity + recursive args. Wire tag 0x06.
/// Lists are <c>StructTerm(".", [head, tail])</c>; nil is <c>ConstTerm(ConstAtom("nil"))</c>.
/// </summary>
public sealed class StructTerm : Term
{
    public string Functor { get; }
    public IReadOnlyList<Term> Args { get; }
    public StructTerm(string functor, IReadOnlyList<Term> args) { Functor = functor; Args = args; }

    public override bool Equals(object? obj)
    {
        if (obj is not StructTerm o) return false;
        if (o.Functor != Functor) return false;
        if (o.Args.Count != Args.Count) return false;
        for (int i = 0; i < Args.Count; i++)
            if (!Args[i].Equals(o.Args[i])) return false;
        return true;
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(typeof(StructTerm));
        hc.Add(Functor);
        foreach (var a in Args) hc.Add(a);
        return hc.ToHashCode();
    }

    public override string ToString() => $"{Functor}({string.Join(",", Args)})";
}

/// <summary>An unbound variable reference by global identity. Wire tag 0x07.</summary>
public sealed class VarRef : Term
{
    public GlobalVarId Id { get; }
    public VarRef(GlobalVarId id) { Id = id; }

    public override bool Equals(object? obj) => obj is VarRef o && o.Id.Equals(Id);
    public override int GetHashCode() => HashCode.Combine(typeof(VarRef), Id);
    public override string ToString() => $"Var({Id})";
}

/// <summary>
/// The heap-independent result envelope (FR-001, data-model §1).
///
/// <para><c>ResolvedBindings</c> / <c>VarToWriter</c> are **ordered** (insertion order
/// is the producing engine's canonical declaration/binding order — the parity invariant;
/// map iteration order MUST NOT leak). They are modeled as ordered key/value lists rather
/// than <c>Dictionary</c> precisely so the serialization order is deterministic and
/// byte-identical across runtimes (SC-002). <c>Suspended</c> is an ordered, goal_id-deduped
/// list of blocking-reader / suspended-goal ids (R10). <c>Captured</c> is carried but
/// EXCLUDED from the byte-parity criterion (R4). <c>Error</c> is present iff status==Failed.</para>
/// </summary>
public sealed class ResultEnvelope
{
    public ExecutionStatus Status { get; }
    public IReadOnlyList<KeyValuePair<string, Term>> ResolvedBindings { get; }
    public IReadOnlyList<KeyValuePair<string, GlobalVarId>> VarToWriter { get; }
    public IReadOnlyList<GlobalVarId> Suspended { get; }
    public byte[] Captured { get; }
    public string? Error { get; }

    public ResultEnvelope(
        ExecutionStatus status,
        IReadOnlyList<KeyValuePair<string, Term>>? resolvedBindings = null,
        IReadOnlyList<KeyValuePair<string, GlobalVarId>>? varToWriter = null,
        IReadOnlyList<GlobalVarId>? suspended = null,
        byte[]? captured = null,
        string? error = null)
    {
        Status = status;
        ResolvedBindings = resolvedBindings ?? Array.Empty<KeyValuePair<string, Term>>();
        VarToWriter = varToWriter ?? Array.Empty<KeyValuePair<string, GlobalVarId>>();
        Suspended = suspended ?? Array.Empty<GlobalVarId>();
        Captured = captured ?? Array.Empty<byte>();
        Error = error;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ResultEnvelope o) return false;
        if (o.Status != Status) return false;
        if (!OrderedBindingsEqual(ResolvedBindings, o.ResolvedBindings)) return false;
        if (!OrderedVarsEqual(VarToWriter, o.VarToWriter)) return false;
        if (!ListEqual(Suspended, o.Suspended)) return false;
        if (!BytesEqual(Captured, o.Captured)) return false;
        return o.Error == Error;
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Status);
        hc.Add(ResolvedBindings.Count);
        hc.Add(VarToWriter.Count);
        hc.Add(Suspended.Count);
        hc.Add(Captured.Length);
        hc.Add(Error);
        return hc.ToHashCode();
    }

    public override string ToString() =>
        $"ResultEnvelope(status: {Status}, bindings: {ResolvedBindings.Count}, " +
        $"varToWriter: {VarToWriter.Count}, suspended: {Suspended.Count}, " +
        $"capturedLen: {Captured.Length}, error: {Error})";

    // --- ordered value-equality helpers (canonical order is part of the value / parity) ---

    private static bool OrderedBindingsEqual(
        IReadOnlyList<KeyValuePair<string, Term>> a,
        IReadOnlyList<KeyValuePair<string, Term>> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Key != b[i].Key) return false;
            if (!a[i].Value.Equals(b[i].Value)) return false;
        }
        return true;
    }

    private static bool OrderedVarsEqual(
        IReadOnlyList<KeyValuePair<string, GlobalVarId>> a,
        IReadOnlyList<KeyValuePair<string, GlobalVarId>> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Key != b[i].Key) return false;
            if (!a[i].Value.Equals(b[i].Value)) return false;
        }
        return true;
    }

    private static bool ListEqual(IReadOnlyList<GlobalVarId> a, IReadOnlyList<GlobalVarId> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!a[i].Equals(b[i])) return false;
        return true;
    }

    private static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
