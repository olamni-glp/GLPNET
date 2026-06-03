// lib/analysis/type_checker/clause_validation.cs
//
// Validates Term AST nodes in program clause contexts.
// Per spec: /Users/udi/GLP/docs/type system/clause-validation.md

using GlpRuntime.Compiler;

namespace GlpRuntime.Analysis.TypeChecker;

/// <summary>
/// Static entry points for clause-context term validation.
/// Three named methods are kept distinct (head / body / guard) per the referenced
/// spec <c>clause-validation.md</c>, which anticipates future per-context divergence.
/// </summary>
public static class ClauseValidation
{
    /// <summary>
    /// Validates a term in clause head context.
    /// <para>
    /// Rejects _? (anonymous reader) which is never permitted in programs.
    /// Allows _ (anonymous variable) to discard input values.
    /// </para>
    /// </summary>
    public static void ValidateClauseHead(Term term) => CheckNoAnonymousReader(term);

    /// <summary>
    /// Validates a term in clause body context.
    /// <para>
    /// Rejects _? (anonymous reader) which is never permitted in programs.
    /// Allows _ (anonymous variable) to discard output values.
    /// </para>
    /// </summary>
    public static void ValidateClauseBody(Term term) => CheckNoAnonymousReader(term);

    /// <summary>
    /// Validates a term in guard context.
    /// <para>
    /// Rejects _? (anonymous reader) which is never permitted in programs.
    /// Allows _ (anonymous variable) in guards.
    /// </para>
    /// </summary>
    public static void ValidateGuard(Term term) => CheckNoAnonymousReader(term);

    /// <summary>
    /// Recursively checks for anonymous readers and throws if found.
    /// Per typed-glp-manual Section 9: Anonymous writers are allowed, anonymous readers are not.
    /// This applies to both bare _? and named anonymous readers like _X?.
    /// </summary>
    private static void CheckNoAnonymousReader(Term term)
    {
        switch (term)
        {
            case UnderscoreTerm u when u.IsReader:
                throw new CompileError(
                    "_? (anonymous reader) is not permitted in program clauses",
                    u.Line,
                    u.Column,
                    phase: "validation");

            // Named anonymous readers (_X?) are also rejected
            case VarTerm v when v.Name.StartsWith("_", StringComparison.Ordinal) && v.IsReader:
                throw new CompileError(
                    $"{v.Name}? (anonymous reader) is not permitted in program clauses",
                    v.Line,
                    v.Column,
                    phase: "validation");

            case StructTerm s:
                foreach (var arg in s.Args)
                    CheckNoAnonymousReader(arg);
                break;

            case ListTerm l:
                if (l.Head is not null) CheckNoAnonymousReader(l.Head);
                if (l.Tail is not null) CheckNoAnonymousReader(l.Tail);
                break;

            default:
                break;
        }
    }
}
