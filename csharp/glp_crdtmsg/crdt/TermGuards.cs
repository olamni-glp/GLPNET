// Ground-term + acyclic guards for op payloads (feature 041-crdtmsg-mvp, T029).
//
// Contract C7 / FR-023 (BB-CRDT-9) / FR-031:
//   - Wire values are GROUND terms only — an unbound VarRef on the wire is a fault, not a value.
//   - Op payloads are ACYCLIC — a cyclic payload faults at op-apply.
//   Both are surfaced as loud TRANSPORT FAULTS (exceptions on the CrdtMsgException hierarchy), never
//   as a GLP Fail verdict / silent drop. Immutable C# terms cannot form a reference cycle, so the
//   acyclic guard is defensive-by-construction here; it is kept explicit so the invariant is enforced
//   at the seam the spec names (op-apply), and would catch a cycle introduced by any future builder.

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.CrdtMsg.Crdt;

/// <summary>A non-ground wire value (an unbound VarRef reached the wire). Transport fault (FR-023).</summary>
public sealed class NonGroundTermException : CrdtMsgException
{
    public NonGroundTermException(string message) : base(message) { }
}

/// <summary>A cyclic op payload detected at apply time. Transport fault (FR-031), never a GLP Fail.</summary>
public sealed class CyclicTermException : CrdtMsgException
{
    public CyclicTermException(string message) : base(message) { }
}

public static class TermGuards
{
    /// <summary>Throw <see cref="NonGroundTermException"/> if any VarRef is reachable (FR-023).</summary>
    public static void EnsureGround(Term t)
    {
        switch (t)
        {
            case VarRef v:
                throw new NonGroundTermException($"non-ground wire value: unbound variable {v.Id}");
            case StructTerm s:
                foreach (var a in s.Args) EnsureGround(a);
                break;
            case ConstTerm:
                break;
        }
    }

    /// <summary>Throw <see cref="CyclicTermException"/> if the term graph contains a reference cycle (FR-031).</summary>
    public static void EnsureAcyclic(Term t)
    {
        Walk(t, new HashSet<Term>(ReferenceEqualityComparer.Instance));
    }

    private static void Walk(Term t, HashSet<Term> onPath)
    {
        if (t is not StructTerm s) return;
        if (!onPath.Add(s))
            throw new CyclicTermException($"cyclic op payload at functor '{s.Functor}'");
        foreach (var a in s.Args) Walk(a, onPath);
        onPath.Remove(s);
    }

    /// <summary>Full op-payload validation at apply time: ground + acyclic (both transport faults).</summary>
    public static void ValidateOpPayload(Term body)
    {
        EnsureGround(body);
        EnsureAcyclic(body);
    }
}
