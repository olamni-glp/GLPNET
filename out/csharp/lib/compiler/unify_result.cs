// lib/compiler/UnifyResult.cs
//
// Discriminated union: result of compile-time GLP unification for partial evaluation.
// Converted from Dart source: lib/compiler/unify_result.dart
// source_sha256: 34a1261c94414c63dcf23e281a8ad8b6417c58d7157f3f491d3c87b2e4f82c52

namespace GlpRuntime.Compiler;

// using GlpRuntime.Compiler; elided — same namespace as Term (ast.cs)

/// <summary>Result of compile-time GLP unification for partial evaluation.</summary>
public abstract class UnifyResult
{
    protected UnifyResult() { }
}

public sealed class UnifySuccess : UnifyResult
{
    public IReadOnlyDictionary<string, Term> Substitution { get; }

    public UnifySuccess(IReadOnlyDictionary<string, Term> substitution)
    {
        Substitution = substitution;
    }
}

public sealed class UnifyFail : UnifyResult
{
    public string Reason { get; }

    public UnifyFail(string reason)
    {
        Reason = reason;
    }
}

public sealed class UnifySuspend : UnifyResult
{
    public IReadOnlySet<string> UnboundReaders { get; }

    public UnifySuspend(IReadOnlySet<string> unboundReaders)
    {
        UnboundReaders = unboundReaders;
    }
}
