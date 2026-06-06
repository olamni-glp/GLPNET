// lib/compiler/analyzer.cs
//
// GLP Semantic Analyzer — SRSW validation, defined-guard partial eval,
// optional reduce/2 generation, and register assignment.
// Converted from Dart source: lib/compiler/analyzer.dart
// source_sha256: 531b9f57edc68a07f95f78381c3c38b6953c8506cc799a21dfec8bc73dca32d7

using System.Collections.Frozen;
using System.Text.RegularExpressions;
using GlpRuntime.Analysis.TypeChecker;

namespace GlpRuntime.Compiler;

// ============================================================================
// T2 — VariableInfo
// ============================================================================

/// <summary>
/// Per-variable bookkeeping for semantic analysis of a single clause.
/// Mutable reference class (NOT a record — reference-identity is load-bearing
/// for identity-keyed side tables).
/// </summary>
public sealed class VariableInfo
{
    /// <summary>Variable name (writer name; reader X? maps to writer X).</summary>
    public string Name { get; }

    /// <summary>True if this VariableInfo slot was created as a writer slot.</summary>
    public bool IsWriter { get; }

    // Occurrence tracking (total — for register allocation, etc.)
    public int WriterOccurrences { get; set; } = 0;
    public int ReaderOccurrences { get; set; } = 0;

    // Head/body occurrences only (for SRSW validation).
    // Guard occurrences do NOT count toward SRSW satisfaction per spec.
    public int WriterOccurrencesHeadBody { get; set; } = 0;
    public int ReaderOccurrencesHeadBody { get; set; } = 0;

    // First occurrence location
    public AstNode? FirstOccurrence { get; set; }

    // Register assignment (filled during analysis)
    public int? RegisterIndex { get; set; }

    // For readers: the paired writer name
    public string? PairedWriter { get; set; }

    // Variable classification
    public bool IsTemporary { get; set; } = false;  // X register (doesn't cross calls)
    public bool IsPermanent { get; set; } = false;  // Y register (survives across calls)

    public VariableInfo(string name, bool isWriter)
    {
        Name     = name;
        IsWriter = isWriter;
    }

    /// <summary>True if this variable is anonymous (name starts with '_').</summary>
    public bool IsAnonymous => Name.StartsWith('_');

    /// <summary>
    /// Partial SRSW validity check (anonymous + writer-count + reader-count).
    /// Does NOT consult the enclosing VariableTable's grounded sets.
    /// The complete check is <see cref="VariableTable.CollectSRSWViolations"/>.
    /// </summary>
    public bool IsSRSWValid
    {
        get
        {
            // Anonymous variables (names starting with _) are exempt from SRSW
            if (IsAnonymous) return true;

            // Must have exactly one writer occurrence
            if (WriterOccurrences != 1) return false;

            // Must have at least one reader occurrence
            if (ReaderOccurrences == 0) return false;

            return true;
        }
    }

    public override string ToString() =>
        $"VariableInfo({Name}, writer×{WriterOccurrences}, reader×{ReaderOccurrences}, reg={RegisterIndex})";
}

// ============================================================================
// T3 — VariableTable
// ============================================================================

/// <summary>
/// Per-clause variable bookkeeping: SRSW validation, grounded sets, register assignment.
/// </summary>
public sealed class VariableTable
{
    private readonly Dictionary<string, VariableInfo> _vars
        = new(StringComparer.Ordinal);

    // Track guard context
    // NOTE: _hasGroundGuard is never read in the current source; retained for parity.
    // TODO: remove only via the preserve-working-code discipline §0.
#pragma warning disable CS0414
    private bool _hasGroundGuard = false;
#pragma warning restore CS0414
    private readonly HashSet<string> _groundedVars     = new(StringComparer.Ordinal);
    private readonly HashSet<string> _typeGroundedVars = new(StringComparer.Ordinal);

    /// <summary>
    /// Record a writer occurrence.
    /// <paramref name="inHeadOrBody"/>: true if in head or body (counts toward SRSW),
    /// false if in guard. Anonymous variables (starting with '_') are silently skipped.
    /// </summary>
    public void RecordWriterOccurrence(string name, AstNode node, bool inHeadOrBody = true)
    {
        if (name.StartsWith('_')) return;

        if (!_vars.TryGetValue(name, out var info))
        {
            info = new VariableInfo(name, true);
            _vars[name] = info;
        }
        info.WriterOccurrences++;
        if (inHeadOrBody) info.WriterOccurrencesHeadBody++;
        info.FirstOccurrence ??= node;
    }

    /// <summary>
    /// Record a reader occurrence.
    /// <paramref name="inHeadOrBody"/>: true if in head or body (counts toward SRSW),
    /// false if in guard. Anonymous variables (starting with '_') are silently skipped.
    /// Reader X? pairs with writer X — keyed by the bare writer name.
    /// </summary>
    public void RecordReaderOccurrence(string name, AstNode node, bool inHeadOrBody = true)
    {
        if (name.StartsWith('_')) return;

        var writerName = name;  // Reader X? pairs with writer X
        if (!_vars.TryGetValue(writerName, out var writerInfo))
        {
            writerInfo = new VariableInfo(writerName, true);
            _vars[writerName] = writerInfo;
        }
        writerInfo.ReaderOccurrences++;
        if (inHeadOrBody) writerInfo.ReaderOccurrencesHeadBody++;
        writerInfo.FirstOccurrence ??= node;
    }

    public void MarkGrounded(string varName)     => _groundedVars.Add(varName);
    public bool IsGrounded(string varName)       => _groundedVars.Contains(varName);
    public void MarkTypeGrounded(string varName) => _typeGroundedVars.Add(varName);
    public bool IsTypeGrounded(string varName)   => _typeGroundedVars.Contains(varName);

    /// <summary>True if the variable allows multiple occurrences (guard-grounded or type-grounded).</summary>
    public bool AllowsMultipleOccurrences(string varName) =>
        IsGrounded(varName) || IsTypeGrounded(varName);

    /// <summary>
    /// Collect SRSW violations for this clause's variable table.
    /// Returns user-visible strings in the exact format expected by the REPL test suite.
    /// </summary>
    public IReadOnlyList<string> CollectSRSWViolations()
    {
        var violations = new List<string>();

        foreach (var info in _vars.Values)
        {
            if (info.IsAnonymous) continue;

            if (info.WriterOccurrences > 1 && !AllowsMultipleOccurrences(info.Name))
            {
                var line = info.FirstOccurrence?.Line ?? 0;
                violations.Add(
                    $"Line {line}: Writer variable \"{info.Name}\" occurs {info.WriterOccurrences} times without ground guard or constant type"
                );
            }

            if (info.ReaderOccurrencesHeadBody > 1 && !AllowsMultipleOccurrences(info.Name))
            {
                var line = info.FirstOccurrence?.Line ?? 0;
                violations.Add(
                    $"Line {line}: Reader variable \"{info.Name}?\" occurs {info.ReaderOccurrencesHeadBody} times without ground guard or constant type"
                );
            }

            if (info.WriterOccurrences == 0)
            {
                var line = info.FirstOccurrence?.Line ?? 0;
                violations.Add(
                    $"Line {line}: Variable \"{info.Name}\" has no writer (must have exactly one)"
                );
            }

            if (info.ReaderOccurrences == 0 && info.WriterOccurrences > 0)
            {
                var line = info.FirstOccurrence?.Line ?? 0;
                violations.Add(
                    $"Line {line}: Variable \"{info.Name}\" has no reader (must have exactly one)"
                );
            }
        }

        return violations;
    }

    /// <summary>
    /// Legacy wrapper — throws <see cref="CompileError"/> on the first SRSW violation.
    /// Searches for the first offending variable independently of the violation list order.
    /// </summary>
    public void VerifySRSW()
    {
        var violations = CollectSRSWViolations();
        if (violations.Count == 0) return;

        var firstVar = _vars.Values.FirstOrDefault(v =>
        {
            if (v.WriterOccurrences > 1) return true;
            if (v.ReaderOccurrences > 1 && !AllowsMultipleOccurrences(v.Name)) return true;
            if (v.WriterOccurrences == 0) return true;
            if (v.ReaderOccurrences == 0 && v.WriterOccurrences > 0) return true;
            return false;
        }) ?? _vars.Values.First();

        throw new CompileError(
            "SRSW violation: " + RegExSrswLinePrefix.Replace(violations[0], ""),
            firstVar.FirstOccurrence?.Line ?? 0,
            firstVar.FirstOccurrence?.Column ?? 0,
            phase: "analyzer");
    }

    private static readonly Regex RegExSrswLinePrefix =
        new(@"^Line \d+: ", RegexOptions.Compiled);

    /// <summary>Returns a snapshot of all VariableInfo records for this clause.</summary>
    public IReadOnlyList<VariableInfo> GetAllVars() => _vars.Values.ToList();

    /// <summary>Look up a VariableInfo by writer name; null if absent.</summary>
    public VariableInfo? GetVar(string name) => _vars.TryGetValue(name, out var v) ? v : null;

    public override string ToString() => $"VariableTable({_vars.Count} vars)";
}

// ============================================================================
// T4 — Annotated AST wrappers
// ============================================================================

/// <summary>Annotated program: original AST + per-procedure annotations.</summary>
public sealed class AnnotatedProgram
{
    public Program Ast { get; }
    public IReadOnlyList<AnnotatedProcedure> Procedures { get; }

    public AnnotatedProgram(Program ast, IReadOnlyList<AnnotatedProcedure> procedures)
    {
        Ast        = ast;
        Procedures = procedures;
    }
}

/// <summary>Annotated procedure: original AST + per-clause annotations + codegen back-fill fields.</summary>
public sealed class AnnotatedProcedure
{
    public Procedure Ast { get; }
    public string Name { get; }
    public int Arity { get; }
    public IReadOnlyList<AnnotatedClause> Clauses { get; }

    // Code generation metadata (back-filled during codegen)
    public int?    EntryPC    { get; set; }
    public string? EntryLabel { get; set; }

    public AnnotatedProcedure(Procedure ast, string name, int arity, IReadOnlyList<AnnotatedClause> clauses)
    {
        Ast     = ast;
        Name    = name;
        Arity   = arity;
        Clauses = clauses;
    }

    public string Signature => $"{Name}/{Arity}";

    public override string ToString() => $"AnnotatedProcedure({Signature}, {Clauses.Count} clauses)";
}

/// <summary>Annotated clause: original AST + variable table + metadata flags.</summary>
public sealed class AnnotatedClause
{
    public Clause Ast { get; }
    public VariableTable VarTable { get; }
    public bool HasGuards { get; }
    public bool HasBody   { get; }

    public AnnotatedClause(Clause ast, VariableTable varTable, bool hasGuards = false, bool hasBody = false)
    {
        Ast       = ast;
        VarTable  = varTable;
        HasGuards = hasGuards;
        HasBody   = hasBody;
    }

    public override string ToString() =>
        $"AnnotatedClause(guards={HasGuards}, body={HasBody}, vars={VarTable.GetAllVars().Count})";
}

// ============================================================================
// T5/T6 — Analyzer (coordinator class)
// ============================================================================

/// <summary>
/// Semantic analyzer for GLP programs.
/// Runs the 4-step pipeline: SRSW-validate → partial-eval defined guards →
/// optional reduce/2 generation → per-procedure register assignment.
/// </summary>
public sealed class Analyzer
{
    private readonly DefinedGuardEvaluator _definedGuardEvaluator = new();

    // Mutable per-call state (reset at the top of every Analyze call)
    private Dictionary<string, ProcDecl> _procDecls = new(StringComparer.Ordinal);
    private CompileMode _compileMode = CompileMode.User;

    public Analyzer() { }

    // ────────────────────────────────────────────────────────────────────────
    // T5 — constant-type helpers
    // ────────────────────────────────────────────────────────────────────────

    private static readonly FrozenSet<string> ConstantTypes =
        new[] { "Integer", "Real", "Number", "String", "Constant" }
        .ToFrozenSet(StringComparer.Ordinal);

    private static bool IsConstantType(string? typeName) =>
        typeName is not null && ConstantTypes.Contains(typeName);

    // ────────────────────────────────────────────────────────────────────────
    // T13 — guard dispatch tables (class-scoped FrozenSet, hoisted from local)
    // ────────────────────────────────────────────────────────────────────────

    private static readonly FrozenSet<string> NegatableGuards = new[]
    {
        "ground", "known", "unknown", "integer", "number", "atom", "string",
        "constant", "compound", "tuple", "list", "is_list", "module",
        "is_mutual_ref", "no_readers", "=?="
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> NonNegatableGuards = new[]
    {
        "<", ">", "=<", ">=", "=:=", "=\\=",
        // Standard-order term comparison (T011/FR-037, OQ-G2 RULED non-negatable)
        "@<", "@>", "@=<", "@>=",
        "otherwise", "wait", "wait_until"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> InvalidInGuardPosition = new[]
    {
        "true", "false", "fail"
    }.ToFrozenSet(StringComparer.Ordinal);

    // Hoisted from local lists inside _AnalyzeGuard
    private static readonly FrozenSet<string> TypeCheckOps = new[]
    {
        "number", "integer", "atom", "string", "list", "tuple", "compound", "constant"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ComparisonOps = new[]
    {
        "<", ">", "=<", ">=", "=:=", "=\\=",
        // @< family is ground-implying (order defined only over ground terms) — SC-006
        "@<", "@>", "@=<", "@>="
    }.ToFrozenSet(StringComparer.Ordinal);

    // ────────────────────────────────────────────────────────────────────────
    // T7 — public Analyze entry point (4-step pipeline)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Run the 4-step GLP analysis pipeline and return an annotated program.
    /// Step ordering is load-bearing: SRSW validation MUST run on the original program
    /// BEFORE partial eval (which removes defined guards whose readers are counted for SRSW).
    /// </summary>
    public AnnotatedProgram Analyze(
        Program program,
        bool generateReduce = false,
        IReadOnlyList<ProcDecl>? procDeclarations = null,
        CompileMode compileMode = CompileMode.User,
        bool skipGlobalSRSW = false)
    {
        // Reset per-call mutable state
        _compileMode = compileMode;
        _procDecls = new Dictionary<string, ProcDecl>(StringComparer.Ordinal);
        if (procDeclarations is not null)
        {
            foreach (var decl in procDeclarations)
                _procDecls[decl.Key] = decl;
        }

        // STEP 1: Run SRSW validation on the ORIGINAL program.
        // This must happen BEFORE partial evaluation, because partial eval removes defined guards.
        // Guard readers (like X? in send(X?, Ch?, Ch1)) must be counted for SRSW pairing.
        //
        // skipGlobalSRSW: Linked programs skip this check because each module was already
        // type-checked independently, and the generated alias clauses use a forwarding
        // pattern that doesn't satisfy SRSW locally but is safe at the program level.
        if (!skipGlobalSRSW)
        {
            var allViolations = new List<string>();
            foreach (var proc in program.Procedures)
                allViolations.AddRange(_CollectSRSWViolationsForProcedure(proc));

            if (allViolations.Count > 0)
            {
                var message = "SRSW violations found:\n" +
                              string.Join("\n", allViolations.Select(v => $"  • {v}"));
                throw new CompileError(message, 0, 0, phase: "analyzer");
            }
        }

        // STEP 2: Transform defined guards via partial evaluation.
        // After SRSW validation passes, we can safely transform defined guards.
        var transformed = _definedGuardEvaluator.TransformDefinedGuards(program);

        // STEP 3: Auto-generate reduce/2 clauses for metainterpretation (optional).
        var withReduce = generateReduce ? _GenerateReduceClauses(transformed) : transformed;

        // STEP 4: Annotate clauses (register assignment) on the transformed program.
        // SRSW already validated above, so skip SRSW checking here.
        var annotatedProcs = new List<AnnotatedProcedure>();
        foreach (var proc in withReduce.Procedures)
        {
            (var annotatedProc, _) = _AnalyzeProcedureCollectingErrors(proc, skipSRSW: true);
            annotatedProcs.Add(annotatedProc);
        }

        return new AnnotatedProgram(withReduce, annotatedProcs);
    }

    // ────────────────────────────────────────────────────────────────────────
    // T8 — SRSW violation collector (per-procedure, per-clause)
    // ────────────────────────────────────────────────────────────────────────

    private IReadOnlyList<string> _CollectSRSWViolationsForProcedure(Procedure proc)
    {
        var allViolations = new List<string>();

        foreach (var clause in proc.Clauses)
        {
            // Fresh VariableTable per clause — isolation is critical for SRSW
            var varTable = new VariableTable();

            _MarkConstantTypeVars(clause.Head, proc.Name, proc.Arity, varTable);
            _AnalyzeAtom(clause.Head, varTable);

            if (clause.Guards is { Count: > 0 })
                foreach (var guard in clause.Guards)
                    _AnalyzeGuard(guard, varTable);

            if (clause.Body is { Count: > 0 })
                foreach (var goal in clause.Body)
                    _AnalyzeGoal(goal, varTable);

            var contextViolations = varTable.CollectSRSWViolations()
                .Select(v => $"{proc.Name}/{proc.Arity}: {v}");
            allViolations.AddRange(contextViolations);
        }

        return allViolations;
    }

    // ────────────────────────────────────────────────────────────────────────
    // T9 — reduce/2 clause generator
    // ────────────────────────────────────────────────────────────────────────

    private Program _GenerateReduceClauses(Program program)
    {
        // Don't generate reduce for reduce/2 itself (avoid infinite recursion)
        var sourceClauses = new List<Clause>();
        foreach (var proc in program.Procedures)
        {
            if (proc.Name == "reduce" && proc.Arity == 2) continue;
            sourceClauses.AddRange(proc.Clauses);
        }

        if (sourceClauses.Count == 0) return program;

        var reduceClauses = sourceClauses.Select(c => _GenerateReduceClause(c)).ToList();

        // Check if reduce/2 already exists (user-defined); manual indexed loop for -1 sentinel
        int existingReduceIdx = -1;
        for (int i = 0; i < program.Procedures.Count; i++)
        {
            if (program.Procedures[i].Name == "reduce" && program.Procedures[i].Arity == 2)
            {
                existingReduceIdx = i;
                break;
            }
        }

        var newProcedures = new List<Procedure>(program.Procedures);

        if (existingReduceIdx >= 0)
        {
            var existing = newProcedures[existingReduceIdx];
            var mergedClauses = new List<Clause>(existing.Clauses);
            mergedClauses.AddRange(reduceClauses);
            newProcedures[existingReduceIdx] = new Procedure(
                "reduce", 2, mergedClauses, existing.Line, existing.Column);
        }
        else
        {
            var firstClause = reduceClauses[0];
            newProcedures.Add(new Procedure(
                "reduce", 2, reduceClauses, firstClause.Line, firstClause.Column));
        }

        return new Program(newProcedures, program.Line, program.Column);
    }

    // ────────────────────────────────────────────────────────────────────────
    // T10 — single reduce clause + term-lift helpers
    // ────────────────────────────────────────────────────────────────────────

    private Clause _GenerateReduceClause(Clause source)
    {
        var head   = source.Head;
        var guards = source.Guards;
        var body   = source.Body;
        var line   = head.Line;
        var col    = head.Column;

        var headTerm = _AtomToTerm(head);

        Term bodyTerm;
        if (body is null || body.Count == 0)
            bodyTerm = new ConstTerm("true", line, col);
        else
            bodyTerm = _GoalsToTerm(body, line, col);

        var reduceHead = new Atom("reduce", new List<Term> { headTerm, bodyTerm }, line, col);

        // If original had guards, keep them with 'true' body
        List<Goal>? reduceBody = null;
        if (guards is { Count: > 0 })
            reduceBody = new List<Goal> { new Goal("true", Array.Empty<Term>(), line, col) };

        return new Clause(reduceHead, guards: guards, body: reduceBody, line: line, column: col);
    }

    private static Term _AtomToTerm(Atom atom)
    {
        if (atom.Args.Count == 0)
            return new ConstTerm(atom.Functor, atom.Line, atom.Column);
        return new StructTerm(atom.Functor, atom.Args, atom.Line, atom.Column);
    }

    private Term _GoalsToTerm(IReadOnlyList<Goal> goals, int line, int col)
    {
        if (goals.Count == 0)
            return new ConstTerm("true", line, col);
        if (goals.Count == 1)
            return _GoalToTerm(goals[0]);

        // Right-associative conjunction: (A, (B, C))
        Term result = _GoalToTerm(goals[goals.Count - 1]);
        for (int i = goals.Count - 2; i >= 0; i--)
            result = new StructTerm(",", new List<Term> { _GoalToTerm(goals[i]), result }, line, col);
        return result;
    }

    private static Term _GoalToTerm(Goal goal)
    {
        if (goal.Args.Count == 0)
            return new ConstTerm(goal.Functor, goal.Line, goal.Column);
        return new StructTerm(goal.Functor, goal.Args, goal.Line, goal.Column);
    }

    // ────────────────────────────────────────────────────────────────────────
    // T11 — throwing and collecting procedure/clause analysers
    //       (preserved duplication — the throw/collect split is load-bearing)
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Throwing variant (retained for parity per preserve-working-code §0,
    /// even though currently unreferenced — only the collecting variant is
    /// called from Analyze).
    /// </summary>
    private AnnotatedProcedure _AnalyzeProcedure(Procedure proc)
    {
        var annotatedClauses = new List<AnnotatedClause>();
        foreach (var clause in proc.Clauses)
            annotatedClauses.Add(_AnalyzeClause(clause));
        return new AnnotatedProcedure(proc, proc.Name, proc.Arity, annotatedClauses);
    }

    private (AnnotatedProcedure, IReadOnlyList<string>) _AnalyzeProcedureCollectingErrors(
        Procedure proc, bool skipSRSW = false)
    {
        var annotatedClauses = new List<AnnotatedClause>();
        var allViolations    = new List<string>();

        foreach (var clause in proc.Clauses)
        {
            (var annotatedClause, var violations) =
                _AnalyzeClauseCollectingErrors(clause, proc.Name, proc.Arity, skipSRSW: skipSRSW);
            annotatedClauses.Add(annotatedClause);
            allViolations.AddRange(violations);
        }

        return (new AnnotatedProcedure(proc, proc.Name, proc.Arity, annotatedClauses), allViolations);
    }

    /// <summary>Throwing variant of clause analyser (SRSW violation → CompileError).</summary>
    private AnnotatedClause _AnalyzeClause(Clause clause)
    {
        var varTable = new VariableTable();

        _AnalyzeAtom(clause.Head, varTable);

        bool hasGuards = clause.Guards is { Count: > 0 };
        if (hasGuards)
            foreach (var guard in clause.Guards!)
                _AnalyzeGuard(guard, varTable);

        bool hasBody = clause.Body is { Count: > 0 };
        if (hasBody)
            foreach (var goal in clause.Body!)
                _AnalyzeGoal(goal, varTable);

        varTable.VerifySRSW();
        _AssignRegisters(varTable);

        return new AnnotatedClause(clause, varTable, hasGuards: hasGuards, hasBody: hasBody);
    }

    /// <summary>
    /// Collecting variant of clause analyser (non-throwing).
    /// Register assignment runs AFTER collection even if violations were found
    /// (for partial-error tooling).
    /// </summary>
    private (AnnotatedClause, IReadOnlyList<string>) _AnalyzeClauseCollectingErrors(
        Clause clause, string procName, int procArity, bool skipSRSW = false)
    {
        var varTable = new VariableTable();

        _MarkConstantTypeVars(clause.Head, procName, procArity, varTable);
        _AnalyzeAtom(clause.Head, varTable);

        bool hasGuards = clause.Guards is { Count: > 0 };
        if (hasGuards)
            foreach (var guard in clause.Guards!)
                _AnalyzeGuard(guard, varTable);

        bool hasBody = clause.Body is { Count: > 0 };
        if (hasBody)
            foreach (var goal in clause.Body!)
                _AnalyzeGoal(goal, varTable);

        var violations = skipSRSW
            ? (IReadOnlyList<string>)Array.Empty<string>()
            : varTable.CollectSRSWViolations();
        var contextViolations = violations.Select(v => $"{procName}/{procArity}: {v}").ToList();

        // Assign register indices even if there are violations (for partial analysis)
        _AssignRegisters(varTable);

        return (new AnnotatedClause(clause, varTable, hasGuards: hasGuards, hasBody: hasBody),
                contextViolations);
    }

    // ────────────────────────────────────────────────────────────────────────
    // T12 — trivial atom/goal argument dispatchers
    // ────────────────────────────────────────────────────────────────────────

    private void _AnalyzeAtom(Atom atom, VariableTable varTable)
    {
        foreach (var arg in atom.Args)
            _AnalyzeTerm(arg, varTable);
    }

    private void _AnalyzeGoal(Goal goal, VariableTable varTable)
    {
        foreach (var arg in goal.Args)
            _AnalyzeTerm(arg, varTable);
    }

    // ────────────────────────────────────────────────────────────────────────
    // T14 — guard dispatcher
    // ────────────────────────────────────────────────────────────────────────

    private void _AnalyzeGuard(Guard guard, VariableTable varTable)
    {
        // Reject body-only constructs in guard position
        if (InvalidInGuardPosition.Contains(guard.Predicate))
            throw new CompileError(
                $"\"{guard.Predicate}\" is not a guard - it can only appear in body position",
                guard.Line, guard.Column, phase: "analyzer");

        // Validate guard negation
        if (guard.Negated)
        {
            if (NonNegatableGuards.Contains(guard.Predicate))
                throw new CompileError(
                    $"Guard \"{guard.Predicate}\" cannot be negated (type-error semantics)",
                    guard.Line, guard.Column, phase: "analyzer");
        }

        // ground(X?) implies the argument is fully ground
        if (guard.Predicate == "ground" && guard.Args.Count == 1)
        {
            var arg = guard.Args[0];
            if (arg is VarTerm v) varTable.MarkGrounded(v.Name);
        }

        // Type-checking guards implicitly test groundness
        if (TypeCheckOps.Contains(guard.Predicate) && guard.Args.Count == 1)
        {
            var arg = guard.Args[0];
            if (arg is VarTerm v) varTable.MarkGrounded(v.Name);
        }

        // module/1
        if (guard.Predicate == "module" && guard.Args.Count == 1)
        {
            var arg = guard.Args[0];
            if (arg is VarTerm v) varTable.MarkGrounded(v.Name);
        }

        // is_mutual_ref/1
        if (guard.Predicate == "is_mutual_ref" && guard.Args.Count == 1)
        {
            var arg = guard.Args[0];
            if (arg is VarTerm v) varTable.MarkGrounded(v.Name);
        }

        // unknown/1
        if (guard.Predicate == "unknown" && guard.Args.Count == 1)
        {
            var arg = guard.Args[0];
            if (arg is VarTerm v) varTable.MarkGrounded(v.Name);
        }

        // wait_until/1
        if (guard.Predicate == "wait_until" && guard.Args.Count == 1)
        {
            var arg = guard.Args[0];
            if (arg is VarTerm v) varTable.MarkGrounded(v.Name);
        }

        // wait/1
        if (guard.Predicate == "wait" && guard.Args.Count == 1)
        {
            var arg = guard.Args[0];
            if (arg is VarTerm v) varTable.MarkGrounded(v.Name);
        }

        // Comparison guards implicitly test groundness of both operands
        if (ComparisonOps.Contains(guard.Predicate) && guard.Args.Count == 2)
        {
            foreach (var arg in guard.Args)
                _ExtractAndMarkGroundedVars(arg, varTable);
        }

        // =?=/2 marks both arguments as grounded
        if (guard.Predicate == "=?=" && guard.Args.Count == 2)
        {
            foreach (var arg in guard.Args)
            {
                if (arg is VarTerm v) varTable.MarkGrounded(v.Name);
            }
        }

        // Analyze guard arguments — guard occurrences do NOT count toward SRSW.
        // Pass inHeadOrBody: false so occurrences are tracked but don't count for SRSW.
        foreach (var arg in guard.Args)
            _AnalyzeTerm(arg, varTable, inHeadOrBody: false);
    }

    // ────────────────────────────────────────────────────────────────────────
    // T15 — recursive term walkers
    // ────────────────────────────────────────────────────────────────────────

    private void _ExtractAndMarkGroundedVars(Term term, VariableTable varTable)
    {
        switch (term)
        {
            case VarTerm v:
                varTable.MarkGrounded(v.Name);
                break;
            case StructTerm s:
                foreach (var arg in s.Args)
                    _ExtractAndMarkGroundedVars(arg, varTable);
                break;
            case ListTerm l:
                if (l.Head is not null) _ExtractAndMarkGroundedVars(l.Head, varTable);
                if (l.Tail is not null) _ExtractAndMarkGroundedVars(l.Tail, varTable);
                break;
            // ConstTerm / UnderscoreTerm: no variables to extract
        }
    }

    private void _MarkVarsInTermAsTypeGrounded(Term term, VariableTable varTable)
    {
        switch (term)
        {
            case VarTerm v:
                varTable.MarkTypeGrounded(v.Name);
                break;
            case StructTerm s:
                foreach (var arg in s.Args)
                    _MarkVarsInTermAsTypeGrounded(arg, varTable);
                break;
            case ListTerm l:
                if (l.Head is not null) _MarkVarsInTermAsTypeGrounded(l.Head, varTable);
                if (l.Tail is not null) _MarkVarsInTermAsTypeGrounded(l.Tail, varTable);
                break;
            // ConstTerm / UnderscoreTerm: no variables
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // T16 — central term walker
    // ────────────────────────────────────────────────────────────────────────

    private void _AnalyzeTerm(Term term, VariableTable varTable, bool inHeadOrBody = true)
    {
        switch (term)
        {
            case VarTerm v:
                // Skip anonymous variables — each occurrence is a fresh writer with no reader
                if (v.Name.StartsWith('_')) return;
                if (v.IsReader)
                    varTable.RecordReaderOccurrence(v.Name, v, inHeadOrBody);
                else
                    varTable.RecordWriterOccurrence(v.Name, v, inHeadOrBody);
                break;

            case StructTerm s:
                foreach (var arg in s.Args)
                    _AnalyzeTerm(arg, varTable, inHeadOrBody);
                break;

            case ListTerm l:
                if (l.Head is not null) _AnalyzeTerm(l.Head, varTable, inHeadOrBody);
                if (l.Tail is not null) _AnalyzeTerm(l.Tail, varTable, inHeadOrBody);
                break;

            case ConstTerm c:
                // Validate reserved constants in user mode
                var value = c.Value;
                if (_compileMode == CompileMode.User && value is string s2 && s2.StartsWith('_'))
                    throw new CompileError(
                        $"Constants starting with '_' are reserved for system use: '{value}'. " +
                        "Use -mode(system). directive for system code.",
                        c.Line, c.Column, phase: "analyzer");
                break;

            // UnderscoreTerm: no variables to track
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // T17 — register assignment
    // ────────────────────────────────────────────────────────────────────────

    private static void _AssignRegisters(VariableTable varTable)
    {
        int nextIndex = 0;
        foreach (var info in varTable.GetAllVars())
        {
            // For now, all variables are temporaries (X registers)
            // TODO: Analyze variable lifetimes to distinguish X vs Y registers
            info.IsTemporary   = true;
            info.RegisterIndex = nextIndex++;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // T18 — constant-type grounding from proc decl
    // ────────────────────────────────────────────────────────────────────────

    private void _MarkConstantTypeVars(Atom head, string procName, int procArity, VariableTable varTable)
    {
        var key = $"{procName}/{procArity}";
        if (!_procDecls.TryGetValue(key, out var procDecl)) return;

        for (int i = 0; i < head.Args.Count && i < procDecl.ArgTypes.Count; i++)
        {
            var typeName = procDecl.GetTypeName(i);
            if (IsConstantType(typeName))
                _MarkVarsInTermAsTypeGrounded(head.Args[i], varTable);
        }
    }
}

// ============================================================================
// T19 — DefinedGuardEvaluator (strict compile-time guard evaluator)
// ============================================================================

/// <summary>
/// Strict compile-time defined-guard evaluator.
/// Distinct from <see cref="PartialEvaluator"/> in partial_evaluator.cs (the lenient
/// variant used by the engine for reduce/2 unfolding). This class runs in the main
/// compile pipeline and rejects programs whose defined guards do not fully reduce
/// (throws <see cref="CompileError"/> on <see cref="UnifyFail"/> and
/// <see cref="UnifySuspend"/>).
/// </summary>
public sealed class DefinedGuardEvaluator
{
    private int _varCounter = 0;

    // ────────────────────────────────────────────────────────────────────────
    // Entry point
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>Transform all defined guards in a program (strict — throws on non-reducible guards).</summary>
    public Program TransformDefinedGuards(Program program)
    {
        // Merge prelude unit clauses with user unit clauses.
        // User definitions override prelude (second merge overrides on key collision).
        var unitClauses = new Dictionary<string, IReadOnlyList<Term>>(StringComparer.Ordinal);
        foreach (var kv in PreludeUnitClauses.GetPreludeUnitClauses()) unitClauses[kv.Key] = kv.Value;
        foreach (var kv in _CollectUnitClauses(program))                unitClauses[kv.Key] = kv.Value;

        if (unitClauses.Count == 0) return program;

        var transformedProcedures = new List<Procedure>();
        foreach (var procedure in program.Procedures)
        {
            var transformedClauses = new List<Clause>();
            foreach (var clause in procedure.Clauses)
                transformedClauses.Add(_TransformClause(clause, unitClauses));
            transformedProcedures.Add(new Procedure(
                procedure.Name, procedure.Arity, transformedClauses,
                procedure.Line, procedure.Column));
        }

        return new Program(transformedProcedures, program.Line, program.Column);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Unit clause collection
    // ────────────────────────────────────────────────────────────────────────

    private static Dictionary<string, IReadOnlyList<Term>> _CollectUnitClauses(Program program)
    {
        var result = new Dictionary<string, IReadOnlyList<Term>>(StringComparer.Ordinal);
        foreach (var proc in program.Procedures)
        {
            if (proc.Clauses.Count != 1) continue;
            var clause = proc.Clauses[0];

            // No guards
            if (clause.Guards is { Count: > 0 }) continue;

            // No body, empty body, or body is just `true`
            if (clause.Body is { Count: > 0 })
            {
                if (clause.Body.Count == 1 &&
                    clause.Body[0].Functor == "true" &&
                    clause.Body[0].Args.Count == 0)
                {
                    // body is just `true` — still a unit clause
                }
                else
                {
                    continue;
                }
            }

            result[$"{proc.Name}/{proc.Arity}"] = clause.Head.Args;
        }
        return result;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Fixpoint clause transformer (STRICT — throws on fail/suspend)
    // ────────────────────────────────────────────────────────────────────────

    private Clause _TransformClause(Clause clause, IReadOnlyDictionary<string, IReadOnlyList<Term>> unitClauses)
    {
        if (clause.Guards is null || clause.Guards.Count == 0) return clause;

        var currentHead   = clause.Head;
        var currentGuards = new List<Guard>(clause.Guards);
        var currentBody   = clause.Body is not null ? new List<Goal>(clause.Body) : null;
        bool changed = true;

        while (changed)
        {
            changed = false;
            var remainingGuards = new List<Guard>();

            for (int i = 0; i < currentGuards.Count; i++)
            {
                var guard = currentGuards[i];
                var key   = $"{guard.Predicate}/{guard.Args.Count}";

                if (unitClauses.TryGetValue(key, out var unitArgs))
                {
                    if (guard.Negated)
                        throw new CompileError(
                            $"Defined guard \"{guard.Predicate}\" cannot be negated",
                            guard.Line, guard.Column, phase: "analyzer");

                    var renamedArgs = _RenameUnitClauseVars(unitArgs);
                    var result      = _GlpUnifyForPE(guard.Args, renamedArgs);

                    switch (result)
                    {
                        case UnifyFail fail:
                            throw new CompileError(
                                $"Defined guard \"{guard.Predicate}({string.Join(", ", guard.Args)})\" can never succeed.\n" +
                                $"  Unit clause: {guard.Predicate}({string.Join(", ", unitArgs)})\n" +
                                $"  Reason: {fail.Reason}\n" +
                                "  This clause is unreachable.",
                                guard.Line, guard.Column, phase: "analyzer");

                        case UnifySuspend suspend:
                            throw new CompileError(
                                $"Cannot reduce defined guard \"{guard.Predicate}({string.Join(", ", guard.Args)})\" at compile time.\n" +
                                $"  Unit clause: {guard.Predicate}({string.Join(", ", unitArgs)})\n" +
                                $"  Unbound readers: {string.Join(", ", suspend.UnboundReaders.Select(r => r + "?"))}\n" +
                                "  Defined guards must be fully reducible at compile time.",
                                guard.Line, guard.Column, phase: "analyzer");

                        case UnifySuccess success:
                            currentHead = _ApplySubstitutionToAtom(currentHead, success.Substitution);
                            var restGuards = currentGuards.GetRange(i + 1, currentGuards.Count - i - 1)
                                .Select(g => _ApplySubstitutionToGuard(g, success.Substitution)).ToList();
                            remainingGuards = remainingGuards
                                .Select(g => _ApplySubstitutionToGuard(g, success.Substitution)).ToList();
                            if (currentBody is not null)
                                currentBody = currentBody
                                    .Select(g => _ApplySubstitutionToGoal(g, success.Substitution)).ToList();
                            currentGuards = new List<Guard>(remainingGuards.Count + restGuards.Count);
                            currentGuards.AddRange(remainingGuards);
                            currentGuards.AddRange(restGuards);
                            changed = true;
                            break;

                        default:
                            throw new InvalidOperationException("UnifyResult: unreachable subtype.");
                    }
                    if (changed) break;
                }
                else
                {
                    remainingGuards.Add(guard);
                }
            }

            if (!changed) currentGuards = remainingGuards;
        }

        return new Clause(
            currentHead,
            guards: currentGuards.Count == 0 ? null : currentGuards,
            body:   currentBody,
            line:   clause.Line,
            column: clause.Column);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Variable renaming
    // ────────────────────────────────────────────────────────────────────────

    private List<Term> _RenameUnitClauseVars(IReadOnlyList<Term> args)
    {
        var varNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var arg in args)
            _CollectVarNames(arg, varNames);

        var renaming = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in varNames)
            if (!name.StartsWith('_'))
                renaming[name] = $"PE_{_varCounter++}";

        return args.Select(arg => _ApplyRenaming(arg, renaming)).ToList();
    }

    private static void _CollectVarNames(Term term, HashSet<string> names)
    {
        switch (term)
        {
            case VarTerm v:
                names.Add(v.Name);
                break;
            case StructTerm s:
                foreach (var arg in s.Args) _CollectVarNames(arg, names);
                break;
            case ListTerm l:
                if (l.Head is not null) _CollectVarNames(l.Head, names);
                if (l.Tail is not null) _CollectVarNames(l.Tail, names);
                break;
        }
    }

    private static Term _ApplyRenaming(Term term, IReadOnlyDictionary<string, string> renaming)
    {
        switch (term)
        {
            case VarTerm v when v.Name == "_":
                return new UnderscoreTerm(v.Line, v.Column);
            case VarTerm v when renaming.TryGetValue(v.Name, out var newName):
                return new VarTerm(newName, v.IsReader, v.Line, v.Column);
            case VarTerm:
                return term;
            case StructTerm s:
                return new StructTerm(s.Functor,
                    s.Args.Select(a => _ApplyRenaming(a, renaming)).ToList(),
                    s.Line, s.Column);
            case ListTerm l:
                return new ListTerm(
                    l.Head is not null ? _ApplyRenaming(l.Head, renaming) : null,
                    l.Tail is not null ? _ApplyRenaming(l.Tail, renaming) : null,
                    l.Line, l.Column);
            case UnderscoreTerm:
                return term;
            default:
                return term;  // ConstTerm passthrough
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // GLP three-valued compile-time unification
    // ────────────────────────────────────────────────────────────────────────

    private UnifyResult _GlpUnifyForPE(IReadOnlyList<Term> callArgs, IReadOnlyList<Term> unitArgs)
    {
        if (callArgs.Count != unitArgs.Count)
            return new UnifyFail($"Arity mismatch: {callArgs.Count} vs {unitArgs.Count}");

        var substitution  = new Dictionary<string, Term>(StringComparer.Ordinal);
        var suspensionSet = new HashSet<string>(StringComparer.Ordinal);

        // Phase 1: Collection
        for (int i = 0; i < callArgs.Count; i++)
        {
            var result = _UnifyTerms(callArgs[i], unitArgs[i], substitution, suspensionSet);
            if (result is not null) return result;
        }

        // Phase 2: Resolution
        var unresolvedReaders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var readerName in suspensionSet)
            if (!substitution.ContainsKey(readerName)) unresolvedReaders.Add(readerName);
        if (unresolvedReaders.Count > 0) return new UnifySuspend(unresolvedReaders);

        return new UnifySuccess(_ResolveSubstitution(substitution));
    }

    private static void _SubstSet(Dictionary<string, Term> subst, string key, Term value)
    {
        if (subst.TryGetValue(key, out var old)
            && old is VarTerm oldVar && !oldVar.IsReader
            && value is not VarTerm)
        {
            if (!subst.ContainsKey(oldVar.Name)) subst[oldVar.Name] = value;
        }
        subst[key] = value;
    }

    private UnifyResult? _UnifyTerms(
        Term callArg, Term unitArg,
        Dictionary<string, Term> subst, HashSet<string> suspSet)
    {
        if (_IsAnonymous(callArg) || _IsAnonymous(unitArg)) return null;

        // Call arg is writer
        if (callArg is VarTerm callVar && !callVar.IsReader)
        {
            if      (unitArg is VarTerm unitW && !unitW.IsReader) subst[unitW.Name] = callVar;
            else if (unitArg is VarTerm unitR &&  unitR.IsReader) subst[unitR.Name] = callVar;
            else                                                   subst[callVar.Name] = unitArg;
            return null;
        }

        // Call arg is reader
        if (callArg is VarTerm callReader && callReader.IsReader)
        {
            var writerName = callReader.Name;
            if (unitArg is VarTerm uW && !uW.IsReader)
            {
                subst[uW.Name] = new VarTerm(writerName, false, callReader.Line, callReader.Column);
            }
            else if (unitArg is VarTerm uR && uR.IsReader)
            {
                subst[uR.Name] = new VarTerm(writerName, false, callReader.Line, callReader.Column);
                suspSet.Add(writerName);
            }
            else
            {
                suspSet.Add(writerName);
                if (subst.TryGetValue(writerName, out var existing))
                {
                    var compat = _CheckCompatible(existing, unitArg, subst, suspSet);
                    if (compat is not null) return compat;
                }
                else
                {
                    subst[writerName] = unitArg;
                }
            }
            return null;
        }

        // Call arg is constant
        if (callArg is ConstTerm callConst)
        {
            if (unitArg is ConstTerm unitConst)
                return object.Equals(callConst.Value, unitConst.Value)
                    ? null
                    : new UnifyFail($"Constant mismatch: {callConst.Value} vs {unitConst.Value}");
            if (unitArg is VarTerm uW2 && !uW2.IsReader) { _SubstSet(subst, uW2.Name, callArg); return null; }
            if (unitArg is VarTerm uR2 &&  uR2.IsReader) { _SubstSet(subst, uR2.Name, callArg); return null; }
            return new UnifyFail($"Constant {callConst.Value} cannot match structure {unitArg}");
        }

        // Call arg is structure
        if (callArg is StructTerm callStruct)
        {
            if (unitArg is StructTerm unitStruct)
            {
                if (callStruct.Functor != unitStruct.Functor || callStruct.Args.Count != unitStruct.Args.Count)
                    return new UnifyFail($"Functor mismatch: {callStruct.Functor}/{callStruct.Args.Count} vs {unitStruct.Functor}/{unitStruct.Args.Count}");
                for (int i = 0; i < callStruct.Args.Count; i++)
                {
                    var r = _UnifyTerms(callStruct.Args[i], unitStruct.Args[i], subst, suspSet);
                    if (r is not null) return r;
                }
                return null;
            }
            if (unitArg is VarTerm uW3 && !uW3.IsReader) { _SubstSet(subst, uW3.Name, callArg); return null; }
            if (unitArg is VarTerm uR3 &&  uR3.IsReader) { _SubstSet(subst, uR3.Name, callArg); return null; }
            return new UnifyFail($"Structure {callStruct.Functor} cannot match {unitArg}");
        }

        // Call arg is list
        if (callArg is ListTerm callList)
        {
            if (unitArg is ListTerm unitList)
            {
                if (callList.IsNil && unitList.IsNil) return null;
                if (callList.IsNil != unitList.IsNil) return new UnifyFail("List structure mismatch: nil vs non-nil");
                if (callList.Head is not null && unitList.Head is not null)
                {
                    var r = _UnifyTerms(callList.Head, unitList.Head, subst, suspSet);
                    if (r is not null) return r;
                }
                if (callList.Tail is not null && unitList.Tail is not null)
                {
                    var r = _UnifyTerms(callList.Tail, unitList.Tail, subst, suspSet);
                    if (r is not null) return r;
                }
                return null;
            }
            if (unitArg is VarTerm uW4 && !uW4.IsReader) { _SubstSet(subst, uW4.Name, callArg); return null; }
            if (unitArg is VarTerm uR4 &&  uR4.IsReader) { _SubstSet(subst, uR4.Name, callArg); return null; }
            return new UnifyFail($"List cannot match {unitArg}");
        }

        return new UnifyFail($"Unhandled case: {callArg.GetType().Name} vs {unitArg.GetType().Name}");
    }

    private UnifyResult? _CheckCompatible(
        Term existing, Term newTerm,
        Dictionary<string, Term> subst, HashSet<string> suspSet)
    {
        if (existing is ConstTerm e && newTerm is ConstTerm n)
        {
            if (!object.Equals(e.Value, n.Value))
                return new UnifyFail($"Incompatible bindings: {e.Value} vs {n.Value}");
            return null;
        }
        if (existing is StructTerm es && newTerm is StructTerm ns)
        {
            if (es.Functor != ns.Functor || es.Args.Count != ns.Args.Count)
                return new UnifyFail($"Incompatible structures: {es.Functor} vs {ns.Functor}");
            return null;
        }
        return null;
    }

    private static bool _IsAnonymous(Term term) =>
        term is UnderscoreTerm || (term is VarTerm v && v.Name.StartsWith('_'));

    // ────────────────────────────────────────────────────────────────────────
    // Substitution resolution (chain flattening + cycle protection)
    // ────────────────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, Term> _ResolveSubstitution(
        Dictionary<string, Term> subst)
    {
        var resolved = new Dictionary<string, Term>(StringComparer.Ordinal);
        foreach (var entry in subst)
            resolved[entry.Key] = _ResolveTerm(entry.Value, subst,
                new HashSet<string>(StringComparer.Ordinal));
        return resolved;
    }

    private static Term _ResolveTerm(
        Term term, IReadOnlyDictionary<string, Term> subst, HashSet<string> visited)
    {
        if (term is VarTerm varTerm)
        {
            if (visited.Contains(varTerm.Name)) return term;
            if (subst.TryGetValue(varTerm.Name, out var bound))
            {
                visited.Add(varTerm.Name);
                var resolved = _ResolveTerm(bound, subst, visited);
                if (varTerm.IsReader && resolved is VarTerm rv && !rv.IsReader)
                    return new VarTerm(rv.Name, true, rv.Line, rv.Column);
                return resolved;
            }
            return term;
        }
        if (term is StructTerm s)
            return new StructTerm(s.Functor,
                s.Args.Select(a => _ResolveTerm(a, subst,
                    new HashSet<string>(visited, StringComparer.Ordinal))).ToList(),
                s.Line, s.Column);
        if (term is ListTerm l)
        {
            if (l.IsNil) return l;
            return new ListTerm(
                l.Head is not null ? _ResolveTerm(l.Head, subst,
                    new HashSet<string>(visited, StringComparer.Ordinal)) : null,
                l.Tail is not null ? _ResolveTerm(l.Tail, subst,
                    new HashSet<string>(visited, StringComparer.Ordinal)) : null,
                l.Line, l.Column);
        }
        return term;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Substitution application family
    // ────────────────────────────────────────────────────────────────────────

    private static Term _ApplySubstitution(Term term, IReadOnlyDictionary<string, Term> subst)
    {
        if (term is VarTerm varTerm)
        {
            if (varTerm.Name == "_") return term;
            if (subst.TryGetValue(varTerm.Name, out var replacement))
            {
                if (varTerm.IsReader && replacement is VarTerm rv && !rv.IsReader)
                    return new VarTerm(rv.Name, true, rv.Line, rv.Column);
                return _ApplySubstitution(replacement, subst);
            }
            return term;
        }
        if (term is StructTerm s)
            return new StructTerm(s.Functor,
                s.Args.Select(a => _ApplySubstitution(a, subst)).ToList(),
                s.Line, s.Column);
        if (term is ListTerm l)
        {
            if (l.IsNil) return l;
            return new ListTerm(
                l.Head is not null ? _ApplySubstitution(l.Head, subst) : null,
                l.Tail is not null ? _ApplySubstitution(l.Tail, subst) : null,
                l.Line, l.Column);
        }
        if (term is UnderscoreTerm) return term;
        return term;  // ConstTerm passthrough
    }

    private static Atom _ApplySubstitutionToAtom(Atom atom, IReadOnlyDictionary<string, Term> subst) =>
        new Atom(atom.Functor,
            atom.Args.Select(a => _ApplySubstitution(a, subst)).ToList(),
            atom.Line, atom.Column);

    private static Guard _ApplySubstitutionToGuard(Guard guard, IReadOnlyDictionary<string, Term> subst) =>
        new Guard(guard.Predicate,
            guard.Args.Select(a => _ApplySubstitution(a, subst)).ToList(),
            guard.Line, guard.Column, negated: guard.Negated);

    private static Goal _ApplySubstitutionToGoal(Goal goal, IReadOnlyDictionary<string, Term> subst)
    {
        if (goal is RemoteGoal rg)
        {
            var newModule = _ApplySubstitution(rg.Module, subst);
            var newInner  = _ApplySubstitutionToGoal(rg.Goal, subst);
            return new RemoteGoal(newModule, newInner, rg.Line, rg.Column);
        }
        if (goal is SpawnGoal sg)
        {
            var newInner = _ApplySubstitutionToGoal(sg.InnerGoal, subst);
            return new SpawnGoal(newInner, sg.AgentId, sg.Line, sg.Column);
        }
        return new Goal(goal.Functor,
            goal.Args.Select(a => _ApplySubstitution(a, subst)).ToList(),
            goal.Line, goal.Column);
    }
}
