// lib/lint/linter.cs
//
// Bytecode linter for GLP — checks structural invariants of a compiled predicate.
// Converted from Dart source: lib/lint/linter.dart
// source_sha256: 257a66f29065ce82f55ec45df025e87aba1bcaeee0deaf93c42d93f27335bff7

using System.Linq;
using GlpRuntime.Bytecode;

namespace GlpRuntime.Lint;

// ============================================================================
// LintIssue — one diagnostic produced by the linter
// ============================================================================

/// <summary>
/// A single lint diagnostic.  Immutable; stored in <see cref="LintResult.Issues"/>.
/// </summary>
public class LintIssue
{
    /// <summary>Machine-readable lint code (e.g. "BODY_BEFORE_COMMIT").</summary>
    public string Code { get; }

    /// <summary>Human-readable description of the problem.</summary>
    public string Message { get; }

    /// <summary>Index of the offending opcode in <see cref="BytecodeProgram.Instructions"/>.</summary>
    public long Index { get; }

    public LintIssue(string code, string message, long index)
    {
        Code    = code;
        Message = message;
        Index   = index;
    }

    public override string ToString() => $"[{Code}] @op#{Index}: {Message}";
}

// ============================================================================
// LintResult — outcome returned by Linter.Lint
// ============================================================================

/// <summary>
/// Lint outcome: the list of issues found and a convenience <see cref="Ok"/> flag.
/// </summary>
public class LintResult
{
    /// <summary>All issues found (may be empty).</summary>
    public List<LintIssue> Issues { get; }

    public LintResult(List<LintIssue> issues)
    {
        Issues = issues;
    }

    /// <summary><c>true</c> when no issues were found.</summary>
    public bool Ok => Issues.Count == 0;
}

// ============================================================================
// Linter — stateless linting service
// ============================================================================

/// <summary>
/// Lints a single compiled predicate/program.
///
/// Checks (subset of compiler contract, semantics-preserving):
///  - BODY_BEFORE_COMMIT:    No body ops (BodySet*) before Commit in a clause.
///  - ILLEGAL_PRECOMMIT_OP: Only head/guard ops allowed pre-commit within clause.
///  - SUSPEND_ONCE_AT_END:  Exactly one predicate-level SuspendEnd, after all clauses.
///  - ILLEGAL_BODY_OP:      Control-flow or SuspendEnd found inside a clause body.
///  - REDUNDANT_COMMIT:     Second Commit encountered in body.
/// </summary>
public class Linter
{
    /// <summary>Lint <paramref name="p"/> and return the full issue list.</summary>
    public LintResult Lint(BytecodeProgram p)
    {
        var issues = new List<LintIssue>();

        bool inClause       = false;
        bool inBody         = false;
        bool seenSuspendEnd = false;
        int  suspendEndIndex = -1;

        // Local static predicate: ops that are legal during the head/guard phase.
        static bool IsHeadGuardOp(IOp op) =>
            op is ClauseTry      ||
            op is HeadBindWriter ||
            op is GuardNeedReader||
            op is GuardFail      ||
            op is UnionSiAndGoto ||
            op is ResetAndGoto   ||
            op is Label;

        for (int i = 0; i < p.Instructions.Count; i++)
        {
            IOp op = (IOp)p.Instructions[i];

            // ARM 1: Label — skip silently.
            if (op is Label) continue;

            // ARM 2: After a final SuspendEnd, no new clauses are allowed.
            if (seenSuspendEnd)
            {
                if (op is ClauseTry)
                {
                    issues.Add(new LintIssue(
                        "SUSPEND_ONCE_AT_END",
                        "ClauseTry found after SuspendEnd; end-of-predicate suspend must be last predicate-level action.",
                        i));
                }
                continue;
            }

            // ARM 3: Predicate level (not inside a clause).
            if (!inClause)
            {
                if (op is ClauseTry)
                {
                    inClause = true;
                    inBody   = false;
                    continue;
                }
                if (op is SuspendEnd)
                {
                    seenSuspendEnd  = true;
                    suspendEndIndex = i;
                    continue;
                }
                // Any other op is illegal at predicate level.
                issues.Add(new LintIssue(
                    "ILLEGAL_PRECOMMIT_OP",
                    $"Operation not allowed at predicate level outside a clause: {op.GetType().Name}",
                    i));
                continue;
            }

            // ARM 4: Inside a clause — head/guard phase.
            if (!inBody)
            {
                if (op is Commit)
                {
                    inBody = true;
                    continue;
                }
                if (op is UnionSiAndGoto || op is ResetAndGoto)
                {
                    // Clause ends; next clause (if any) must start with ClauseTry.
                    inClause = false;
                    inBody   = false;
                    continue;
                }
                if (op is SuspendEnd)
                {
                    // Predicate-level suspend encountered (after finishing clauses).
                    seenSuspendEnd  = true;
                    suspendEndIndex = i;
                    inClause        = false;
                    inBody          = false;
                    continue;
                }
                // Body ops before commit are illegal.
                if (op is BodySetConst || op is BodySetStructConstArgs || op is Proceed)
                {
                    issues.Add(new LintIssue(
                        "BODY_BEFORE_COMMIT",
                        $"Body operation {op.GetType().Name} appears before Commit in this clause.",
                        i));
                    continue;
                }
                // Only head/guard ops are allowed pre-commit.
                if (!IsHeadGuardOp(op))
                {
                    issues.Add(new LintIssue(
                        "ILLEGAL_PRECOMMIT_OP",
                        $"Pre-commit operation not permitted in head/guards: {op.GetType().Name}",
                        i));
                    continue;
                }
            }
            else
            {
                // ARM 5: Inside a clause — body phase.
                if (op is UnionSiAndGoto || op is ResetAndGoto)
                {
                    issues.Add(new LintIssue(
                        "ILLEGAL_BODY_OP",
                        $"Control-flow back to clause scan inside body: {op.GetType().Name}",
                        i));
                    continue;
                }
                if (op is SuspendEnd)
                {
                    issues.Add(new LintIssue(
                        "ILLEGAL_BODY_OP",
                        "SuspendEnd cannot appear in a body; it is predicate-level after all clauses.",
                        i));
                    seenSuspendEnd  = true;
                    suspendEndIndex = i;
                    inClause        = false;
                    inBody          = false;
                    continue;
                }
                if (op is Commit)
                {
                    issues.Add(new LintIssue(
                        "REDUNDANT_COMMIT",
                        "Commit encountered in body; compiler should not emit this.",
                        i));
                    continue;
                }
                // BodySet* and Proceed are allowed here — fall through.
            }
        }

        // Post-loop: ensure SuspendEnd occurs at most once.
        int suspendCount = p.Instructions.OfType<SuspendEnd>().Count();
        if (suspendCount > 1)
        {
            issues.Add(new LintIssue(
                "SUSPEND_ONCE_AT_END",
                $"Multiple SuspendEnd opcodes found ({suspendCount}). Expect a single final suspend per predicate.",
                suspendEndIndex >= 0 ? suspendEndIndex : 0));
        }

        return new LintResult(issues);
    }
}
