// lib/compiler/codegen.cs
//
// GLP bytecode emitter — walks an AnnotatedProgram and emits a flat
// List<object> of v1 / v2 bytecode instructions (heterogeneous by design).
// Converted from Dart source: lib/compiler/codegen.dart
// source_sha256: fdeeb685673893129e721409ea2b4ceb0e6f356d406efd526ae32d4ceb0e6f356d

using Bc   = GlpRuntime.Bytecode;
using Bcv2 = GlpRuntime.Bytecode.V2;
using Rt   = GlpRuntime.Runtime;

namespace GlpRuntime.Compiler;

// ============================================================================
// Module System Support
// ============================================================================

/// <summary>
/// Import table: maps module names to 1-indexed positions.
/// Following FCP convention where imports are indexed 1, 2, 3, ...
/// Index 0 is reserved for SELF module.
/// </summary>
public sealed class ImportTable
{
    private readonly Dictionary<string, int> _indices = new();
    private int _nextIndex = 1;

    /// <summary>Add a module to the import table. Returns the index assigned to this module.</summary>
    public int AddImport(string moduleName)
    {
        if (!_indices.ContainsKey(moduleName))
        {
            _indices[moduleName] = _nextIndex++;
        }
        return _indices[moduleName];
    }

    /// <summary>Get the index for a module name, or null if not in imports.</summary>
    public int? GetIndex(string moduleName) =>
        _indices.TryGetValue(moduleName, out var v) ? v : (int?)null;

    /// <summary>Get the number of imports.</summary>
    public int Size => _indices.Count;

    /// <summary>Get all import names in index order (sorted by value, insertion-order-stable).</summary>
    public List<string> OrderedImports => _indices.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();

    /// <summary>Check if a module is in the import table.</summary>
    public bool Contains(string moduleName) => _indices.ContainsKey(moduleName);

    public override string ToString() =>
        $"ImportTable({{{string.Join(", ", _indices.Select(kv => $"{kv.Key}: {kv.Value}"))}}})";
}

/// <summary>
/// Code generation context — per-invocation mutable emit state.
/// Holds the heterogeneous instruction list (v1 Bc.* + v2 Bcv2.*), labels,
/// temp allocator, phase flags, and the module-system import table.
/// </summary>
public sealed class CodeGenContext
{
    // Bytecode accumulator - can hold both v1 (Bc.*) and v2 (Bcv2.*) instructions
    public List<object> Instructions { get; } = new();

    // Label management
    public Dictionary<string, int> Labels { get; } = new();
    public List<string> PendingLabels { get; } = new();

    // Temporary variable allocation
    // Start temps at 10 to avoid collision with argument registers 0..9
    public int NextTempVar { get; set; } = 10;
    public Dictionary<string, int> TempAllocation { get; } = new();

    // Current procedure context
    public string? CurrentProcedure { get; set; }
    public int CurrentClauseIndex { get; set; } = 0;

    // Phase tracking (advisory — preserved per preserve-working-code discipline)
    public bool InHead { get; set; } = false;
    public bool InGuard { get; set; } = false;
    public bool InBody { get; set; } = false;

    // Track variables seen in head (for GetVariable vs GetValue)
    public HashSet<string> SeenHeadVars { get; } = new();

    // Module system: import table for RPC transformation
    public ImportTable ImportTable { get; } = new();

    public int CurrentPC => Instructions.Count;

    public void Emit(object instruction) => Instructions.Add(instruction);

    public void EmitLabel(string label)
    {
        Labels[label] = CurrentPC;
        Instructions.Add(new Bc.Label(label));
    }

    /// <summary>Allocate a fresh temp register. Post-increment: returns old value then increments.</summary>
    public int AllocateTemp() => NextTempVar++;

    /// <summary>Reset temp allocator. Clamps start to max(variableCount, 10) — load-bearing.</summary>
    public void ResetTemps(int variableCount)
    {
        NextTempVar = variableCount > 10 ? variableCount : 10;
        TempAllocation.Clear();
    }
}

/// <summary>
/// Code generator — stateless dispatcher that builds a CodeGenContext per invocation.
/// Two public methods: Generate (strict bytecode) and GenerateWithMetadata (with variable map).
/// </summary>
public sealed class CodeGenerator
{
    // Feature 077 structural cycle guards (reentrancy-aware; one per walker family so a
    // same-node cross-call — e.g. _IsGroundTerm(lt) inside _GenerateArgumentStructureElement —
    // is not a false cycle). Created at the top-level entry, reused through recursion, dropped
    // on top-level exit. Turns a programmatically-cyclic AST node into a CompileError instead of
    // a StackOverflow (FR-004/SC-001); DAG-shared + deep acyclic terms still pass (SC-006).
    private StructuralGuard? _headStructGuard;
    private StructuralGuard? _bodyStructGuard;
    private StructuralGuard? _groundGuard;
    private StructuralGuard? _groundValueGuard;

    public Bc.BytecodeProgram Generate(AnnotatedProgram program) =>
        GenerateWithMetadata(program).Program;

    public CompilationResult GenerateWithMetadata(AnnotatedProgram program)
    {
        var ctx = new CodeGenContext();
        var variableMap = new Dictionary<string, long>();

        foreach (var proc in program.Procedures)
        {
            _GenerateProcedure(proc, ctx);

            // Collect variable mappings from the first procedure (for goals)
            // This captures variables used in queries like "merge([1,2,3], [a,b], Xs)."
            if (proc == program.Procedures[0])
            {
                foreach (var clause in proc.Clauses)
                {
                    foreach (var varInfo in clause.VarTable.GetAllVars())
                    {
                        if (varInfo.RegisterIndex.HasValue)
                        {
                            variableMap[varInfo.Name] = varInfo.RegisterIndex.Value;
                        }
                    }
                }
            }
        }

        var bytecode = new Bc.BytecodeProgram(ctx.Instructions);
        return new CompilationResult(bytecode, variableMap);
    }

    private void _GenerateProcedure(AnnotatedProcedure proc, CodeGenContext ctx)
    {
        ctx.CurrentProcedure = proc.Signature;

        // Entry label: "p/1", "merge/3", etc.
        var entryLabel = proc.Signature;
        ctx.EmitLabel(entryLabel);

        // Record entry PC (kappa)
        proc.EntryPC = ctx.CurrentPC;
        proc.EntryLabel = entryLabel;

        // Generate each clause
        for (int i = 0; i < proc.Clauses.Count; i++)
        {
            ctx.CurrentClauseIndex = i;
            var isLastClause = (i == proc.Clauses.Count - 1);

            var clause = proc.Clauses[i];
            var nextLabel = isLastClause
                ? $"{entryLabel}_end"
                : $"{entryLabel}_c{i + 1}";

            _GenerateClause(clause, ctx, nextLabel, isLastClause);
        }

        // End of procedure
        ctx.EmitLabel($"{entryLabel}_end");
        ctx.Emit(new Bc.NoMoreClauses()); // Suspend if U non-empty, else fail

        // DEBUG: Print bytecode for this procedure (preserved per CLAUDE.md preserve-working-code)
        if (proc.Signature == "foo/1")
        {
            Console.WriteLine($"\n=== BYTECODE FOR {proc.Signature} ===");
            for (int i = 0; i < ctx.Instructions.Count; i++)
            {
                var instr = ctx.Instructions[i];
                string details = "";
                if (instr is Bc.HeadStructure hs)
                {
                    details = $" HeadStructure(\"{hs.Functor}\", {hs.Arity}, argSlot: {hs.ArgSlot})";
                }
                else if (instr is Bc.UnifyConstant uc)
                {
                    details = $" UnifyConstant({uc.Value})";
                }
                else if (instr is Bc.PutStructure ps)
                {
                    details = $" PutStructure(\"{ps.Functor}\", {ps.Arity}, {ps.ArgSlot})";
                }
                else if (instr is Bcv2.PutVariable pv)
                {
                    details = $" PutVariable(reg={pv.VarIndex}, slot={pv.ArgSlot}, reader={pv.IsReader})";
                }
                else if (instr is Bcv2.GetVariable gv)
                {
                    details = $" GetVariable(reg={gv.VarIndex}, slot={gv.ArgSlot}, reader={gv.IsReader})";
                }
                else if (instr is Bcv2.UnifyVariable uv)
                {
                    details = $" UnifyVariable(reg={uv.VarIndex}, reader={uv.IsReader})";
                }
                else if (instr is Bc.Spawn sp)
                {
                    details = $" Spawn(\"{sp.ProcedureLabel}\", arity={sp.Arity})";
                }
                Console.WriteLine($"  {i}: {instr.GetType().Name}{details}");
            }
            Console.WriteLine("=== END BYTECODE ===\n");
        }
    }

    private void _GenerateClause(AnnotatedClause clause, CodeGenContext ctx, string nextLabel, bool isLastClause)
    {
        ctx.ResetTemps(clause.VarTable.GetAllVars().Count);
        ctx.SeenHeadVars.Clear(); // Clear head variable tracking for new clause

        // Emit label for non-first clauses
        if (ctx.CurrentClauseIndex > 0)
        {
            ctx.EmitLabel($"{ctx.CurrentProcedure}_c{ctx.CurrentClauseIndex}");
        }

        // CLAUSE_TRY: Initialize Si=empty, sigma-hat-w=empty
        ctx.Emit(new Bc.ClauseTry());

        // HEAD PHASE
        ctx.InHead = true;
        ctx.InGuard = false;
        ctx.InBody = false;

        _GenerateHead(clause.Ast.Head, clause.VarTable, ctx);

        // GUARD PHASE (if present)
        if (clause.HasGuards && clause.Ast.Guards is not null)
        {
            ctx.InHead = false;
            ctx.InGuard = true;

            foreach (var guard in clause.Ast.Guards)
            {
                _GenerateGuard(guard, clause.VarTable, ctx);
            }
        }

        // COMMIT: Apply sigma-hat-w, enter BODY phase
        ctx.Emit(new Bc.Commit());

        // BODY PHASE
        ctx.InHead = false;
        ctx.InGuard = false;
        ctx.InBody = true;

        if (clause.HasBody && clause.Ast.Body is not null)
        {
            // SPECIAL CASE: Body is just "true" — treat like a fact (no spawn)
            // Check EXACTLY: length==1 AND functor=="true" AND arity==0
            // Do NOT use Any() — that checks "any goal is true/0" not "ONLY goal is true/0"
            if (clause.Ast.Body.Count == 1 &&
                clause.Ast.Body[0].Functor == "true" &&
                clause.Ast.Body[0].Arity == 0)
            {
                ctx.Emit(new Bc.Proceed());
            }
            else
            {
                _GenerateBody(clause.Ast.Body, clause.VarTable, ctx);
            }
        }
        else
        {
            // Empty body: just proceed
            ctx.Emit(new Bc.Proceed());
        }
    }

    private void _GenerateHead(Atom head, VariableTable varTable, CodeGenContext ctx)
    {
        for (int i = 0; i < head.Args.Count; i++)
        {
            _GenerateHeadArgument(head.Args[i], i, varTable, ctx);
        }
    }

    private void _GenerateHeadArgument(Term term, int argSlot, VariableTable varTable, CodeGenContext ctx)
    {
        if (term is VarTerm vt)
        {
            var varInfo = varTable.GetVar(vt.Name);
            if (varInfo is null)
                throw new CompileError($"Undefined variable: {vt.Name}", vt.Line, vt.Column, phase: "codegen");

            int regIndex = varInfo.RegisterIndex!.Value;

            // Check if this is the first occurrence in the head
            // Strip reader suffix '?' to recover base variable name
            string baseVarName = vt.Name.EndsWith("?")
                ? vt.Name.Substring(0, vt.Name.Length - 1)
                : vt.Name;
            bool isFirstOccurrence = !ctx.SeenHeadVars.Contains(baseVarName);

            if (isFirstOccurrence)
            {
                // First occurrence: emit V2 GetVariable (binds register to arg cell)
                ctx.Emit(new Bcv2.GetVariable(regIndex, argSlot, isReader: vt.IsReader));
                ctx.SeenHeadVars.Add(baseVarName);
            }
            else
            {
                // Subsequent occurrence: emit V2 GetValue (asserts equality)
                ctx.Emit(new Bcv2.GetValue(regIndex, argSlot, isReader: vt.IsReader));
            }
        }
        else if (term is ConstTerm ct)
        {
            // Constant in head: match with head_constant
            ctx.Emit(new Bc.HeadConstant(ct.Value, argSlot));
        }
        else if (term is ListTerm lt)
        {
            if (lt.IsNil)
            {
                ctx.Emit(new Bc.HeadNil(argSlot));
            }
            else
            {
                // Non-empty list [H|T]: treat as structure '.'(H, T)
                ctx.Emit(new Bc.HeadStructure(".", 2, argSlot));

                if (lt.Head is not null)
                    _GenerateStructureElement(lt.Head, varTable, ctx, inHead: true);
                if (lt.Tail is not null)
                    _GenerateStructureElement(lt.Tail, varTable, ctx, inHead: true);
            }
        }
        else if (term is StructTerm st)
        {
            // FIX: For structures as direct HEAD arguments, extract first then match
            // This avoids overlapping HeadStructure operations

            // Step 1: Extract the argument into a temp register
            int tempReg = ctx.AllocateTemp();
            ctx.Emit(new Bc.GetVariable(tempReg, argSlot)); // Load argument into temp

            // Step 2: Match the structure at the temp register (not argSlot!)
            ctx.Emit(new Bc.HeadStructure(st.Functor, st.Arity, tempReg));

            // FCP AM: Process ALL arguments inline using Push/Pop for nested structures
            foreach (var subArg in st.Args)
            {
                _GenerateStructureElement(subArg, varTable, ctx, inHead: true);
            }
        }
        else if (term is UnderscoreTerm)
        {
            // Anonymous variable as direct head argument: just ignore it
            // No instruction needed — the argument is simply not extracted
        }
    }

    private void _GenerateStructureElement(Term term, VariableTable varTable, CodeGenContext ctx, bool inHead)
    {
        // Called during structure traversal (S register in use)
        // Feature 077 structural cycle guard (reentrancy-aware; see field decls).
        bool _top = _headStructGuard is null;
        var _sg = _headStructGuard ??= new StructuralGuard("codegen");
        try
        {
        _sg.Enter(term);

        if (term is VarTerm vt)
        {
            var varInfo = varTable.GetVar(vt.Name);
            if (varInfo is null)
                throw new CompileError($"Undefined variable: {vt.Name}", vt.Line, vt.Column, phase: "codegen");

            // Always emit v2 UnifyVariable based on syntactic mode
            ctx.Emit(new Bcv2.UnifyVariable(varInfo.RegisterIndex!.Value, isReader: vt.IsReader));
        }
        else if (term is ConstTerm ct)
        {
            ctx.Emit(new Bc.UnifyConstant(ct.Value));
        }
        else if (term is ListTerm lt)
        {
            if (lt.IsNil)
            {
                // Nil is atomic constant — same for HEAD and BODY modes
                ctx.Emit(new Bc.UnifyConstant("nil"));
            }
            else
            {
                if (inHead)
                {
                    // FCP AM: Push/UnifyStructure/Pop save-register dance
                    int saveReg = ctx.AllocateTemp();
                    ctx.Emit(new Bc.Push(saveReg));
                    ctx.Emit(new Bc.UnifyStructure(".", 2));
                    if (lt.Head is not null) _GenerateStructureElement(lt.Head, varTable, ctx, inHead: true);
                    if (lt.Tail is not null) _GenerateStructureElement(lt.Tail, varTable, ctx, inHead: true);
                    ctx.Emit(new Bc.Pop(saveReg));
                    // FCP AM: After Pop, must place nested structure at S and increment
                    ctx.Emit(new Bcv2.UnifyVariable(saveReg, isReader: false));
                }
                else
                {
                    // WRITE mode (BODY): building nested structure within argument structure
                    int tempReg = ctx.AllocateTemp();
                    ctx.Emit(new Bc.PutStructure(".", 2, tempReg));
                    if (lt.Head is not null) _GenerateStructureElement(lt.Head, varTable, ctx, inHead: inHead);
                    if (lt.Tail is not null) _GenerateStructureElement(lt.Tail, varTable, ctx, inHead: inHead);
                    ctx.Emit(new Bcv2.UnifyVariable(tempReg, isReader: false));
                }
            }
        }
        else if (term is StructTerm st)
        {
            // Nested structure: use Push/UnifyStructure/Pop pattern (FCP AM design)
            if (inHead)
            {
                int saveReg = ctx.AllocateTemp();
                ctx.Emit(new Bc.Push(saveReg));
                ctx.Emit(new Bc.UnifyStructure(st.Functor, st.Arity));
                foreach (var subArg in st.Args)
                {
                    _GenerateStructureElement(subArg, varTable, ctx, inHead: true);
                }
                ctx.Emit(new Bc.Pop(saveReg));
                // FCP AM: After Pop, must place nested structure at S and increment
                ctx.Emit(new Bcv2.UnifyVariable(saveReg, isReader: false));
            }
            else
            {
                // WRITE mode
                int tempReg = ctx.AllocateTemp();
                ctx.Emit(new Bc.PutStructure(st.Functor, st.Arity, tempReg));
                foreach (var subArg in st.Args)
                {
                    _GenerateStructureElement(subArg, varTable, ctx, inHead: inHead);
                }
                ctx.Emit(new Bcv2.UnifyVariable(tempReg, isReader: false));
            }
        }
        else if (term is UnderscoreTerm)
        {
            // Anonymous variable in structure
            ctx.Emit(new Bc.UnifyVoid(count: 1));
        }
        }
        finally { _sg.Exit(term); if (_top) _headStructGuard = null; }
    }

    private void _GenerateGuard(Guard guard, VariableTable varTable, CodeGenContext ctx)
    {
        // Special built-in guards — cascade-of-special-cases (DO NOT collapse to hash-map lookup)

        if (guard.Predicate == "ground" && guard.Args.Count == 1)
        {
            if (guard.Args[0] is VarTerm vt)
            {
                var varInfo = varTable.GetVar(vt.Name);
                if (varInfo is not null)
                {
                    ctx.Emit(new Bc.Ground(varInfo.RegisterIndex!.Value, negated: guard.Negated));
                    return;
                }
            }
        }

        if (guard.Predicate == "known" && guard.Args.Count == 1)
        {
            if (guard.Args[0] is VarTerm vt)
            {
                var varInfo = varTable.GetVar(vt.Name);
                if (varInfo is not null)
                {
                    ctx.Emit(new Bc.Known(varInfo.RegisterIndex!.Value, negated: guard.Negated));
                    return;
                }
            }
        }

        if (guard.Predicate == "no_readers" && guard.Args.Count == 1)
        {
            if (guard.Args[0] is VarTerm vt)
            {
                var varInfo = varTable.GetVar(vt.Name);
                if (varInfo is not null)
                {
                    ctx.Emit(new Bc.NoReaders(varInfo.RegisterIndex!.Value, negated: guard.Negated));
                    return;
                }
            }
        }

        if (guard.Predicate == "otherwise" && guard.Args.Count == 0)
        {
            // 'otherwise' cannot be negated (enforced by analyzer)
            ctx.Emit(new Bc.Otherwise());
            return;
        }

        // Ground equality guard: X =?= Y
        if (guard.Predicate == "=?=" && guard.Args.Count == 2)
        {
            if (guard.Args[0] is VarTerm leftVt && guard.Args[1] is VarTerm rightVt)
            {
                var leftInfo = varTable.GetVar(leftVt.Name);
                var rightInfo = varTable.GetVar(rightVt.Name);
                if (leftInfo is not null && rightInfo is not null)
                {
                    ctx.Emit(new Bc.GroundEqual(
                        leftInfo.RegisterIndex!.Value,
                        rightInfo.RegisterIndex!.Value,
                        negated: guard.Negated));
                    return;
                }
            }
        }

        // Generic guard predicate call (runtime evaluation)
        for (int i = 0; i < guard.Args.Count; i++)
        {
            _GeneratePutArgument(guard.Args[i], i, varTable, ctx);
        }

        ctx.Emit(new Bc.Guard(guard.Predicate, guard.Args.Count, negated: guard.Negated));
    }

    private void _GenerateBody(IReadOnlyList<Goal> goals, VariableTable varTable, CodeGenContext ctx)
    {
        for (int i = 0; i < goals.Count; i++)
        {
            var goal = goals[i];

            // Special handling for RemoteGoal (Module # Goal)
            if (goal is RemoteGoal rg)
            {
                _GenerateRemoteGoal(rg, varTable, ctx);
                continue;
            }

            // Special handling for SpawnGoal (Goal@AgentId)
            // In dGLP mode, ignore the @AgentId annotation and just run the inner goal
            if (goal is SpawnGoal sg)
            {
                var innerGoal = sg.InnerGoal;
                for (int j = 0; j < innerGoal.Args.Count; j++)
                {
                    _GeneratePutArgument(innerGoal.Args[j], j, varTable, ctx);
                }
                var procedureLabel = $"{innerGoal.Functor}/{innerGoal.Arity}";
                ctx.Emit(new Bc.Spawn(procedureLabel, innerGoal.Arity));
                continue;
            }

            // Setup arguments in A registers
            for (int j = 0; j < goal.Args.Count; j++)
            {
                _GeneratePutArgument(goal.Args[j], j, varTable, ctx);
            }

            // ALWAYS spawn (tail recursion removed — all goals spawned)
            var procLabel = $"{goal.Functor}/{goal.Arity}";
            ctx.Emit(new Bc.Spawn(procLabel, goal.Arity));
        }

        // After spawning all goals, emit proceed to terminate parent
        ctx.Emit(new Bc.Proceed());
    }

    /// <summary>
    /// Generate code for a remote goal (Module # Goal).
    /// Following FCP RPC transformation (rpc.cp:164-175):
    /// - Static module: distribute # {Index, Goal}
    /// - Dynamic module: transmit # {ModuleVar, Goal}
    /// </summary>
    private void _GenerateRemoteGoal(RemoteGoal remote, VariableTable varTable, CodeGenContext ctx)
    {
        var innerGoal = remote.Goal;

        // First, set up arguments for the inner goal in A registers
        for (int j = 0; j < innerGoal.Args.Count; j++)
        {
            _GeneratePutArgument(innerGoal.Args[j], j, varTable, ctx);
        }

        // Then emit the appropriate RPC opcode
        if (remote.IsDynamic)
        {
            // Dynamic module: transmit # {ModuleVar, Goal}
            if (remote.Module is not VarTerm moduleTerm)
            {
                throw new CompileError(
                    "Module must be a variable in dynamic RPC",
                    remote.Line, remote.Column, phase: "codegen");
            }
            var varInfo = varTable.GetVar(moduleTerm.Name);
            if (varInfo is null || varInfo.RegisterIndex is null)
            {
                throw new CompileError(
                    $"Unknown variable in dynamic RPC: {moduleTerm.Name}",
                    remote.Line, remote.Column, phase: "codegen");
            }
            ctx.Emit(new Bc.Transmit(varInfo.RegisterIndex.Value, innerGoal.Functor, innerGoal.Arity));
        }
        else
        {
            // Static module: distribute # {Index, Goal}
            var moduleName = remote.StaticModuleName!;

            // Look up or add to import table (side-effect-on-lookup — auto-registers the module)
            var index = ctx.ImportTable.AddImport(moduleName);

            ctx.Emit(new Bc.Distribute(index, innerGoal.Functor, innerGoal.Arity));
        }
    }

    private void _GeneratePutArgument(Term term, int argSlot, VariableTable varTable, CodeGenContext ctx)
    {
        if (term is VarTerm vt)
        {
            var varInfo = varTable.GetVar(vt.Name);
            if (varInfo is null)
                throw new CompileError($"Undefined variable: {vt.Name}", vt.Line, vt.Column, phase: "codegen");

            int regIndex = varInfo.RegisterIndex!.Value;

            // Use v2 unified instruction
            ctx.Emit(new Bcv2.PutVariable(regIndex, argSlot, isReader: vt.IsReader));
        }
        else if (term is ConstTerm ct)
        {
            // Constant: put bound writer with reader
            ctx.Emit(new Bc.PutBoundConst(ct.Value, argSlot));
        }
        else if (term is ListTerm lt)
        {
            if (lt.IsNil)
            {
                ctx.Emit(new Bc.PutBoundNil(argSlot));
            }
            else
            {
                // Build list structure as '.'(H, T)
                ctx.Emit(new Bc.PutStructure(".", 2, argSlot));
                if (lt.Head is not null) _GenerateArgumentStructureElement(lt.Head, varTable, ctx);
                if (lt.Tail is not null) _GenerateArgumentStructureElement(lt.Tail, varTable, ctx);
            }
        }
        else if (term is StructTerm st)
        {
            // Build structure
            ctx.Emit(new Bc.PutStructure(st.Functor, st.Arity, argSlot));
            foreach (var arg in st.Args)
            {
                _GenerateArgumentStructureElement(arg, varTable, ctx);
            }
        }
        else if (term is UnderscoreTerm)
        {
            // Anonymous variable in BODY: create fresh unbound writer
            // DISTINCT from head no-op — SRSW: "_" is a writer that nobody reads
            int tempReg = ctx.AllocateTemp();
            ctx.Emit(new Bcv2.PutVariable(tempReg, argSlot, isReader: false));
        }
    }

    // Check if a term is fully ground (contains no variables)
    private bool _IsGroundTerm(Term term)
    {
        bool _top = _groundGuard is null;
        var _sg = _groundGuard ??= new StructuralGuard("codegen");
        try
        {
            _sg.Enter(term);
            if (term is VarTerm) return false;
            if (term is ConstTerm) return true;
            if (term is ListTerm lt)
            {
                if (lt.IsNil) return true;
                return (lt.Head is null || _IsGroundTerm(lt.Head)) &&
                       (lt.Tail is null || _IsGroundTerm(lt.Tail));
            }
            if (term is StructTerm st)
                return st.Args.All(arg => _IsGroundTerm(arg));
            return false;
        }
        finally { _sg.Exit(term); if (_top) _groundGuard = null; }
    }

    // Currently unused — preserved per CLAUDE.md preserve-working-code
    // Convert a ground term to a value tree (JSON-shaped representation)
    private object? _GroundTermToValue(Term term)
    {
        bool _top = _groundValueGuard is null;
        var _sg = _groundValueGuard ??= new StructuralGuard("codegen");
        try
        {
            _sg.Enter(term);
            if (term is ConstTerm ct) return ct.Value;
            if (term is ListTerm lt)
            {
                if (lt.IsNil) return "nil"; // Empty list as 'nil'
                var head = lt.Head is not null ? _GroundTermToValue(lt.Head) : null;
                var tail = lt.Tail is not null ? _GroundTermToValue(lt.Tail) : null;
                return new List<object?> { head, tail }; // Represent as 2-element list for cons cell
            }
            if (term is StructTerm st)
            {
                return new Dictionary<string, object?>
                {
                    ["functor"] = st.Functor,
                    ["args"] = st.Args.Select(arg => _GroundTermToValue(arg)).ToList()
                };
            }
            return null;
        }
        finally { _sg.Exit(term); if (_top) _groundValueGuard = null; }
    }

    // Helper for building structure elements INSIDE argument structures
    // Different from _GenerateStructureElement which is for HEAD/GUARD unification
    private void _GenerateArgumentStructureElement(Term term, VariableTable varTable, CodeGenContext ctx)
    {
        bool _top = _bodyStructGuard is null;
        var _sg = _bodyStructGuard ??= new StructuralGuard("codegen");
        try
        {
        _sg.Enter(term);
        if (term is VarTerm vt)
        {
            var varInfo = varTable.GetVar(vt.Name);
            if (varInfo is null)
                throw new CompileError($"Undefined variable: {vt.Name}", vt.Line, vt.Column, phase: "codegen");
            int regIndex = varInfo.RegisterIndex!.Value;
            // Emit unify instruction to add variable to structure
            ctx.Emit(new Bcv2.UnifyVariable(regIndex, isReader: vt.IsReader));
        }
        else if (term is ConstTerm ct)
        {
            ctx.Emit(new Bc.UnifyConstant(ct.Value));
        }
        else if (term is ListTerm lt)
        {
            if (lt.IsNil)
            {
                ctx.Emit(new Bc.UnifyConstant("nil"));
            }
            else
            {
                if (_IsGroundTerm(lt))
                {
                    // Ground list: convert to runtime StructTerm and emit as single constant
                    // Deliberate optimisation — one opcode instead of N PutStructure cells
                    static Rt.Term ConvertListToStructTerm(ListTerm l)
                    {
                        if (l.IsNil) return new Rt.ConstTerm("nil");

                        static Rt.Term ConvertTerm(Term t)
                        {
                            if (t is ConstTerm ct2) return new Rt.ConstTerm(ct2.Value);
                            if (t is ListTerm lt2)  return ConvertListToStructTerm(lt2);
                            if (t is StructTerm st2)
                            {
                                var rtArgs = st2.Args.Select(ConvertTerm).ToList();
                                return new Rt.StructTerm(st2.Functor, rtArgs);
                            }
                            return new Rt.ConstTerm(null); // Fallback for unexpected cases
                        }

                        var head = l.Head is not null ? ConvertTerm(l.Head) : new Rt.ConstTerm("nil");
                        var tail = l.Tail is not null ? ConvertTerm(l.Tail) : new Rt.ConstTerm("nil");
                        return new Rt.StructTerm(".", new List<Rt.Term> { head, tail });
                    }

                    var listStructTerm = ConvertListToStructTerm(lt);
                    ctx.Emit(new Bc.UnifyConstant(listStructTerm));
                }
                else
                {
                    // Non-ground list (contains variables): build as structure './2'
                    ctx.Emit(new Bc.PutStructure(".", 2, -1)); // -1 = building inside parent structure

                    if (lt.Head is not null)
                        _GenerateStructureElementInBody(lt.Head, varTable, ctx);

                    if (lt.Tail is not null)
                        _GenerateListTailInBody(lt.Tail, varTable, ctx);
                }
            }
        }
        else if (term is StructTerm st)
        {
            // Nested structure in BODY — no ground-shortcut (asymmetric vs ListTerm — preserved)
            ctx.Emit(new Bc.PutStructure(st.Functor, st.Arity, -1)); // -1 = building inside parent structure

            foreach (var arg in st.Args)
            {
                _GenerateStructureElementInBody(arg, varTable, ctx);
            }
        }
        else if (term is UnderscoreTerm)
        {
            ctx.Emit(new Bc.UnifyVoid(count: 1));
        }
        }
        finally { _sg.Exit(term); if (_top) _bodyStructGuard = null; }
    }

    // Helper for building structure elements in BODY phase
    // Handles both ground and non-ground structures (with variables)
    private void _GenerateStructureElementInBody(Term term, VariableTable varTable, CodeGenContext ctx)
    {
        bool _top = _bodyStructGuard is null;
        var _sg = _bodyStructGuard ??= new StructuralGuard("codegen");
        try
        {
        _sg.Enter(term);
        if (term is VarTerm vt)
        {
            var varInfo = varTable.GetVar(vt.Name);
            if (varInfo is null)
                throw new CompileError($"Undefined variable in structure: {vt.Name}", vt.Line, vt.Column, phase: "codegen");

            int regIndex = varInfo.RegisterIndex!.Value;

            // V2 SetVariable with isReader flag
            ctx.Emit(new Bcv2.SetVariable(regIndex, isReader: vt.IsReader));
        }
        else if (term is ConstTerm ct)
        {
            ctx.Emit(new Bc.SetConstant(ct.Value));
        }
        else if (term is ListTerm lt)
        {
            if (lt.IsNil)
            {
                ctx.Emit(new Bc.SetConstant("nil"));
            }
            else
            {
                // Non-empty nested list: build as cons cell '.'(head, tail)
                ctx.Emit(new Bc.PutStructure(".", 2, -1)); // -1 = building inside parent structure

                if (lt.Head is not null)
                    _GenerateStructureElementInBody(lt.Head, varTable, ctx);

                // Asymmetric head/tail recursion — load-bearing: tail uses _GenerateListTailInBody
                if (lt.Tail is not null)
                    _GenerateListTailInBody(lt.Tail, varTable, ctx);
            }
        }
        else if (term is StructTerm st)
        {
            // Nested structure — build recursively
            ctx.Emit(new Bc.PutStructure(st.Functor, st.Arity, -1)); // -1 = building inside parent structure

            foreach (var arg in st.Args)
            {
                _GenerateStructureElementInBody(arg, varTable, ctx);
            }
        }
        else if (term is UnderscoreTerm)
        {
            // Anonymous variable in structure: create fresh writer
            int tempReg = ctx.AllocateTemp();
            ctx.Emit(new Bcv2.SetVariable(tempReg, isReader: false));
        }
        }
        finally { _sg.Exit(term); if (_top) _bodyStructGuard = null; }
    }

    // Helper for building list tails in BODY phase
    // List tails can be: another list (recurse with list-tail semantics), a variable, or nil
    // Mutually recursive with _GenerateStructureElementInBody
    private void _GenerateListTailInBody(Term term, VariableTable varTable, CodeGenContext ctx)
    {
        bool _top = _bodyStructGuard is null;
        var _sg = _bodyStructGuard ??= new StructuralGuard("codegen");
        try
        {
        _sg.Enter(term);
        if (term is ListTerm lt)
        {
            if (lt.IsNil)
            {
                ctx.Emit(new Bc.SetConstant("nil"));
            }
            else
            {
                // Tail is another list: build nested cons cell
                ctx.Emit(new Bc.PutStructure(".", 2, -1)); // -1 = building inside parent structure

                if (lt.Head is not null)
                    _GenerateStructureElementInBody(lt.Head, varTable, ctx);

                if (lt.Tail is not null)
                    _GenerateListTailInBody(lt.Tail, varTable, ctx);
            }
        }
        else
        {
            // Tail is a variable or other term — use standard handling
            _GenerateStructureElementInBody(term, varTable, ctx);
        }
        }
        finally { _sg.Exit(term); if (_top) _bodyStructGuard = null; }
    }
}
