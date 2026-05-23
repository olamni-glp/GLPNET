---
path: lib/compiler/codegen.dart
cycle_group_id: 42
scc_siblings: []
generated_at: 2026-05-21T16:29:34Z
source_sha256: fdeeb685673893129e721409ea2b4ceb0e6f356d406efd526ae32d4cae64d3fd
schema_version: 1
---

# Conversion Plan: lib/compiler/codegen.dart

## 1. Source Analysis

Verified by direct inspection of `glp_runtime_net/lib/compiler/codegen.dart` (766 lines) and the ratified convspec.

Top-level shape:

- **9 imports** — five `package:glp_runtime/...` cross-package (four `bytecode/*`, one `runtime/terms.dart`), four sibling-relative (`ast.dart`, `analyzer.dart`, `error.dart`, `result.dart`). Three prefix aliases (`as bc` for v1 opcodes, `as bcv2` for v2 opcodes, `as rt` for runtime-side terms) plus one `show BytecodeProgram` filter on `runner.dart`. The two-family disambiguation is load-bearing — both opcode families have identically-named `PutStructure` / `UnifyVariable` symbols.

- **3 top-level classes**:
  1. `ImportTable` (lines 17-48) — 1-indexed insertion-order module name table. Six members: `_indices` (Map<String,int>), `_nextIndex` (int, init 1), `addImport`, `getIndex`, `size` (getter), `orderedImports` (sorted-by-value snapshot), `contains`, `toString` override.
  2. `CodeGenContext` (lines 51-98) — per-invocation mutable emit state. Twelve fields: `instructions: List<dynamic>`, `labels`, `pendingLabels`, `nextTempVar = 10`, `tempAllocation`, `currentProcedure: String?`, `currentClauseIndex = 0`, `inHead/inGuard/inBody = false`, `seenHeadVars: Set<String>`, `importTable: ImportTable`. Four methods: `currentPC` (expression getter), `emit`, `emitLabel`, `allocateTemp`, `resetTemps`.
  3. `CodeGenerator` (lines 101-766) — stateless dispatcher. Two public methods (`generate`, `generateWithMetadata`) and thirteen private helpers: `_generateProcedure`, `_generateClause`, `_generateHead`, `_generateHeadArgument`, `_generateStructureElement`, `_generateGuard`, `_generateBody`, `_generateRemoteGoal`, `_generatePutArgument`, `_isGroundTerm`, `_groundTermToValue`, `_generateArgumentStructureElement`, `_generateStructureElementInBody`, `_generateListTailInBody`.

- **2 nested local functions** (lines 635-651) inside `_generateArgumentStructureElement`: `convertListToStructTerm(ListTerm l) → rt.Term` and its nested `convertTerm(Term t) → rt.Term`. Both capture nothing (pure functions over args); both recurse to produce runtime-side `rt.StructTerm` / `rt.ConstTerm` trees from the ground-list compile-side input.

- **Heterogeneous bytecode list** — `List<dynamic> instructions` holds BOTH v1 `bc.*` instructions (positional argSlot) AND v2 `bcv2.*` instructions (named `isReader`), consumed by runner.dart via `is`-pattern dispatch.

- **Three-phase emit invariant** in `_generateClause`: `ClauseTry()` → HEAD → GUARDS (if present) → `Commit()` → BODY (or `Proceed()` for fact/`true/0`).

- **Debug-print fence** (lines 164-187): `if (proc.signature == 'foo/1')` guards a `print()` block with nine `is`-test branches dumping each instruction's details. Preserved per CLAUDE.md "Preserve Working Code".

- **First-occurrence head-var dispatch** (lines 263-273): strips `?` reader-suffix to compute `baseVarName`, tracks via `ctx.seenHeadVars`; first occurrence ⇒ `bcv2.GetVariable`, subsequent ⇒ `bcv2.GetValue`.

- **Push/Pop save-register pattern** (lines 345-385): FCP AM nested-structure HEAD matching. `allocateTemp` → `bc.Push(saveReg)` → `bc.UnifyStructure(...)` → recurse → `bc.Pop(saveReg)` → `bcv2.UnifyVariable(saveReg, isReader: false)`.

- **HEAD vs BODY dual mode** in `_generateStructureElement`: `inHead: true` uses Push/UnifyStructure/Pop; `inHead: false` uses `bc.PutStructure(., ., tempReg)` + `bcv2.UnifyVariable(tempReg, isReader: false)`.

- **Struct-as-direct-HEAD-arg temp extract** (lines 303-313): source comment "FIX: For structures as direct HEAD arguments, extract first then match" — `bc.GetVariable(tempReg, argSlot)` then `bc.HeadStructure(functor, arity, tempReg)`.

- **Guard fast-path table** (lines 393-459): five hand-coded predicate-name + arity dispatch cases (`ground/1`, `known/1`, `no_readers/1`, `otherwise/0`, `=?=/2`) emitting dedicated opcodes, then a generic fallback emitting `PutArgument` setup + `bc.Guard(name, arity, negated:)`.

- **Body emission three-way dispatch** (lines 461-497): `RemoteGoal` ⇒ `_generateRemoteGoal`; `SpawnGoal` ⇒ strip `@AgentId` then args+Spawn; default ⇒ args+Spawn. Terminal `bc.Proceed()`.

- **Static vs dynamic RPC** in `_generateRemoteGoal` (lines 503-535): `isDynamic` ⇒ `bc.Transmit(regIndex, functor, arity)`; static ⇒ `ctx.importTable.addImport(moduleName)` then `bc.Distribute(index, functor, arity)`. Source cites FCP rpc.cp:164-175.

- **Ground-list constant lowering** (lines 633-653) inside `_generateArgumentStructureElement`: when `_isGroundTerm(term)` ⇒ lower entire list to a single `rt.StructTerm` tree via nested `convertListToStructTerm` / `convertTerm` local functions, emit one `bc.UnifyConstant(listStructTerm)`.

- **`-1` argSlot sentinel** appears in multiple PutStructure calls (lines 657, 674, 710, 725, 748): documented as "building inside parent structure" — S-cursor-relative not slot-absolute.

- **Mutual recursion** between `_generateStructureElementInBody` and `_generateListTailInBody` (lines 686-764): asymmetric list/tail descent at each cons cell.

- **Six `throw CompileError(..., phase: 'codegen')` sites** at lines 257, 327, 518, 541, 618, 693 — each with distinct message ("Undefined variable:", "Unknown variable in dynamic RPC:", "Undefined variable in structure:") for precise origin attribution.

- **Dead but preserved**: `_groundTermToValue` (lines 593-610) — no call site in the file; preserved per CLAUDE.md "Preserve Working Code". Phase flags `inHead/inGuard/inBody` are SET by `_generateClause` but never READ in this file — preserved as advisory contract surface.

## 2. Dart → C#/.NET Conversion Plan

### Imports / namespace

- Four sibling relative imports (`ast.dart`, `analyzer.dart`, `error.dart`, `result.dart`) → ZERO `using` directives because `lib/compiler/*` collapses into one C# namespace `Glp.Runtime.Compiler`. Microsoft Learn: "All types in the same namespace are accessible without a using directive."
- `package:glp_runtime/bytecode/{opcodes,opcodes_v2,asm,runner}.dart` → `using Glp.Runtime.Bytecode;` plus C# `using` alias directives to preserve the v1/v2 disambiguation:
  - `using Bc = Glp.Runtime.Bytecode.OpcodesV1;`
  - `using Bcv2 = Glp.Runtime.Bytecode.OpcodesV2;`
- `package:glp_runtime/runtime/terms.dart as rt` → `using Rt = Glp.Runtime.Runtime.Terms;`
- `show BytecodeProgram` filter satisfied implicitly — only the `BytecodeProgram` type is referenced.
- Reuse `rf-dart-relative-import-to-csharp-using-or-same-namespace` (cached, lib/compiler/* family).

### `ImportTable` → `public sealed class ImportTable`

- `final Map<String, int> _indices = {}` → `private readonly Dictionary<string, int> _indices = new();`
- `int _nextIndex = 1` → `private int _nextIndex = 1;` (1-indexed FCP convention preserved verbatim)
- `int addImport(String moduleName)` → `public int AddImport(string moduleName) { if (!_indices.ContainsKey(moduleName)) { _indices[moduleName] = _nextIndex++; } return _indices[moduleName]; }`. Dart `!` post-condition (key just inserted) ⇒ C# indexer access — `Dictionary` indexer throws `KeyNotFoundException` on miss, matching the throwing contract.
- `int? getIndex(String moduleName) => _indices[moduleName]` → `public int? GetIndex(string moduleName) => _indices.TryGetValue(moduleName, out var v) ? v : (int?)null;` (C# `Dictionary` indexer throws on miss; `TryGetValue` is the miss-tolerant idiom).
- `int get size => _indices.length` → `public int Size => _indices.Count;`
- `List<String> get orderedImports` → `public List<string> OrderedImports => _indices.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();` — explicit `OrderBy(value)` preserves insertion-order independence from dictionary iteration order.
- `bool contains(String moduleName) => _indices.containsKey(moduleName)` → `public bool Contains(string moduleName) => _indices.ContainsKey(moduleName);`
- `@override String toString() => 'ImportTable($_indices)'` → `public override string ToString() => $"ImportTable({{{string.Join(", ", _indices.Select(kv => $"{kv.Key}: {kv.Value}"))}}})";` (Dart Map.toString shape `{a: 1, b: 2}` explicitly reproduced).
- Cite: `rf-dart-mutable-dictionary-class` (carry-forward from analyzer.dart's VariableTable).

### `CodeGenContext` → `public sealed class CodeGenContext`

- `final List<dynamic> instructions = []` → `public List<object> Instructions { get; } = new();` (Dart `dynamic` ⇒ C# `object`, NOT `dynamic` — Dart `dynamic` means "any type, no static check"; C# `dynamic` is late-bound DLR dispatch which is not needed here; consumer `is`-pattern dispatch matches `object`). Cite `rf-dart-dynamic-to-csharp-object`.
- `final Map<String, int> labels = {}` → `public Dictionary<string, int> Labels { get; } = new();`
- `final List<String> pendingLabels = []` → `public List<string> PendingLabels { get; } = new();`
- `int nextTempVar = 10` → `public int NextTempVar { get; set; } = 10;`
- `final Map<String, int> tempAllocation = {}` → `public Dictionary<string, int> TempAllocation { get; } = new();`
- `String? currentProcedure` → `public string? CurrentProcedure { get; set; }` (under `#nullable enable`)
- `int currentClauseIndex = 0` → `public int CurrentClauseIndex { get; set; } = 0;`
- `bool inHead/inGuard/inBody = false` → three `public bool InHead/InGuard/InBody { get; set; } = false;` auto-properties (advisory only, preserved per CLAUDE.md preserve-working-code).
- `final Set<String> seenHeadVars = {}` → `public HashSet<string> SeenHeadVars { get; } = new();`
- `final ImportTable importTable = ImportTable()` → `public ImportTable ImportTable { get; } = new();`
- `int get currentPC => instructions.length` → `public int CurrentPC => Instructions.Count;`
- `void emit(dynamic instruction) { instructions.add(instruction); }` → `public void Emit(object instruction) => Instructions.Add(instruction);`
- `void emitLabel(String label)` → `public void EmitLabel(string label) { Labels[label] = CurrentPC; Instructions.Add(new Bc.Label(label)); }`
- `int allocateTemp() => nextTempVar++` → `public int AllocateTemp() => NextTempVar++;` (postfix `++` returns old value then increments — identical semantics).
- `void resetTemps(int variableCount)` → `public void ResetTemps(int variableCount) { NextTempVar = variableCount > 10 ? variableCount : 10; TempAllocation.Clear(); }` (clamp `max(N, 10)` preserved verbatim — load-bearing register allocator invariant).

### `CodeGenerator` → `public sealed class CodeGenerator` (no fields, stateless)

- `BytecodeProgram generate(AnnotatedProgram program)` → `public BytecodeProgram Generate(AnnotatedProgram program) => GenerateWithMetadata(program).Program;`
- `CompilationResult generateWithMetadata(AnnotatedProgram program)`:
  ```
  public CompilationResult GenerateWithMetadata(AnnotatedProgram program) {
      var ctx = new CodeGenContext();
      var variableMap = new Dictionary<string, int>();
      foreach (var proc in program.Procedures) {
          _GenerateProcedure(proc, ctx);
          if (proc == program.Procedures[0]) {
              foreach (var clause in proc.Clauses) {
                  foreach (var varInfo in clause.VarTable.GetAllVars()) {
                      if (varInfo.RegisterIndex.HasValue) {
                          variableMap[varInfo.Name] = varInfo.RegisterIndex.Value;
                      }
                  }
              }
          }
      }
      var bytecode = new BytecodeProgram(ctx.Instructions);
      return new CompilationResult(bytecode, variableMap);
  }
  ```
  Reference-identity `proc == program.Procedures[0]` matches Dart `proc == program.procedures.first` (C# `==` on reference types is identity by default). Cite `rf-dart-list-first-to-csharp-zero-indexer`, `rf-dart-foreach-final-to-csharp-foreach-var`.

### `_GenerateProcedure` (lines 135-188)

- `ctx.currentProcedure = proc.signature;` → `ctx.CurrentProcedure = proc.Signature;`
- `final entryLabel = proc.signature;` → `var entryLabel = proc.Signature;`
- `ctx.emitLabel(entryLabel);` → `ctx.EmitLabel(entryLabel);`
- `proc.entryPC = ctx.currentPC; proc.entryLabel = entryLabel;` → mutate AnnotatedProcedure properties (settable per analyzer.dart spec).
- For loop preserved verbatim. String interpolation `'${entryLabel}_end'` → `$"{entryLabel}_end"`, `'${entryLabel}_c${i + 1}'` → `$"{entryLabel}_c{i + 1}"`.
- Terminator pair: `ctx.EmitLabel($"{entryLabel}_end"); ctx.Emit(new Bc.NoMoreClauses());`
- **Debug-print fence preserved verbatim** at `if (proc.Signature == "foo/1")`:
  ```
  if (proc.Signature == "foo/1") {
      Console.WriteLine($"\n=== BYTECODE FOR {proc.Signature} ===");
      for (int i = 0; i < ctx.Instructions.Count; i++) {
          var instr = ctx.Instructions[i];
          string details = "";
          if (instr is Bc.HeadStructure hs) {
              details = $" HeadStructure(\"{hs.Functor}\", {hs.Arity}, argSlot: {hs.ArgSlot})";
          } else if (instr is Bc.UnifyConstant uc) {
              details = $" UnifyConstant({uc.Value})";
          } else if (instr is Bc.PutStructure ps) {
              details = $" PutStructure(\"{ps.Functor}\", {ps.Arity}, {ps.ArgSlot})";
          } else if (instr is Bcv2.PutVariable pv) {
              details = $" PutVariable(reg={pv.VarIndex}, slot={pv.ArgSlot}, reader={pv.IsReader})";
          } else if (instr is Bcv2.GetVariable gv) {
              details = $" GetVariable(reg={gv.VarIndex}, slot={gv.ArgSlot}, reader={gv.IsReader})";
          } else if (instr is Bcv2.UnifyVariable uv) {
              details = $" UnifyVariable(reg={uv.VarIndex}, reader={uv.IsReader})";
          } else if (instr is Bc.Spawn sp) {
              details = $" Spawn(\"{sp.ProcedureLabel}\", arity={sp.Arity})";
          }
          Console.WriteLine($"  {i}: {instr.GetType().Name}{details}");
      }
      Console.WriteLine("=== END BYTECODE ===\n");
  }
  ```
  Dart `instr.runtimeType` ⇒ C# `instr.GetType().Name`. Cite `rf-dart-runtime-type-check-to-csharp-is-pattern`.

### `_GenerateClause` (lines 190-242)

- `ctx.resetTemps(clause.varTable.getAllVars().length);` → `ctx.ResetTemps(clause.VarTable.GetAllVars().Count);`
- `ctx.seenHeadVars.clear();` → `ctx.SeenHeadVars.Clear();`
- Early-label-emit `if (ctx.currentClauseIndex > 0)` preserved with `$"{ctx.CurrentProcedure}_c{ctx.CurrentClauseIndex}"`.
- `ctx.emit(bc.ClauseTry());` → `ctx.Emit(new Bc.ClauseTry());`
- Phase flags assigned (advisory): `ctx.InHead = true; ctx.InGuard = false; ctx.InBody = false;` etc.
- `_GenerateHead(clause.Ast.Head, clause.VarTable, ctx);`
- Guard phase: `if (clause.HasGuards && clause.Ast.Guards is not null) { ctx.InHead = false; ctx.InGuard = true; foreach (var guard in clause.Ast.Guards) { _GenerateGuard(guard, clause.VarTable, ctx); } }` (Dart `guards!` ⇒ C# `is not null` pattern then flow-typed access). Cite `rf-dart-bang-to-csharp-not-null-pattern`.
- `ctx.Emit(new Bc.Commit());`
- BODY phase: `if (clause.HasBody && clause.Ast.Body is not null) { if (clause.Ast.Body.Count == 1 && clause.Ast.Body[0].Functor == "true" && clause.Ast.Body[0].Arity == 0) { ctx.Emit(new Bc.Proceed()); } else { _GenerateBody(clause.Ast.Body, clause.VarTable, ctx); } } else { ctx.Emit(new Bc.Proceed()); }` — `true/0` fast-path preserved exactly (do NOT collapse to `Any`).

### `_GenerateHead` (lines 244-250)

```
private void _GenerateHead(Atom head, VariableTable varTable, CodeGenContext ctx) {
    for (int i = 0; i < head.Args.Count; i++) {
        _GenerateHeadArgument(head.Args[i], i, varTable, ctx);
    }
}
```

### `_GenerateHeadArgument` (lines 252-319)

Five-way `is`-pattern cascade:
- `VarTerm vt`: lookup `varInfo = varTable.GetVar(vt.Name);` null-guard ⇒ `throw new CompileError($"Undefined variable: {vt.Name}", vt.Line, vt.Column, phase: "codegen");`. Compute `regIndex = varInfo.RegisterIndex!.Value;`. Compute `baseVarName = vt.Name.EndsWith("?") ? vt.Name.Substring(0, vt.Name.Length - 1) : vt.Name;` (start=0 makes Dart end-exclusive and C# length-based identical). Compute `isFirstOccurrence = !ctx.SeenHeadVars.Contains(baseVarName);`. First-occurrence ⇒ `ctx.Emit(new Bcv2.GetVariable(regIndex, argSlot, isReader: vt.IsReader)); ctx.SeenHeadVars.Add(baseVarName);`. Else ⇒ `ctx.Emit(new Bcv2.GetValue(regIndex, argSlot, isReader: vt.IsReader));`.
- `ConstTerm ct`: `ctx.Emit(new Bc.HeadConstant(ct.Value, argSlot));`
- `ListTerm lt`: if `lt.IsNil` ⇒ `ctx.Emit(new Bc.HeadNil(argSlot));`. Else ⇒ `ctx.Emit(new Bc.HeadStructure(".", 2, argSlot));` + recurse `_GenerateStructureElement(lt.Head, varTable, ctx, inHead: true)` and `_GenerateStructureElement(lt.Tail, varTable, ctx, inHead: true)` (null-guarded).
- `StructTerm st`: temp-extract bug-fix preserved verbatim — `int tempReg = ctx.AllocateTemp(); ctx.Emit(new Bc.GetVariable(tempReg, argSlot)); ctx.Emit(new Bc.HeadStructure(st.Functor, st.Arity, tempReg)); foreach (var subArg in st.Args) { _GenerateStructureElement(subArg, varTable, ctx, inHead: true); }`.
- `UnderscoreTerm`: NO-OP (empty branch with preserved comment).

Cite `rf-dart-is-typetest-cascade-to-csharp-is-pattern-cascade`, `rf-dart-string-endswith-to-csharp-string-endswith`, `rf-dart-substring-to-csharp-substring-with-length`, `rf-dart-bang-to-csharp-nullable-value`.

### `_GenerateStructureElement` (lines 321-391, dual-mode)

Named-required `{required bool inHead}` ⇒ positional `bool inHead` (cite `rf-dart-named-required-param-to-csharp-positional-arg`).

- `VarTerm vt`: null-guard + `ctx.Emit(new Bcv2.UnifyVariable(varInfo.RegisterIndex!.Value, isReader: vt.IsReader));`
- `ConstTerm ct`: `ctx.Emit(new Bc.UnifyConstant(ct.Value));`
- `ListTerm lt`:
  - `lt.IsNil` ⇒ `ctx.Emit(new Bc.UnifyConstant("nil"));`
  - `inHead == true` ⇒ Push/Pop save-register dance: `int saveReg = ctx.AllocateTemp(); ctx.Emit(new Bc.Push(saveReg)); ctx.Emit(new Bc.UnifyStructure(".", 2));` + recurse head/tail with `inHead: true`; `ctx.Emit(new Bc.Pop(saveReg)); ctx.Emit(new Bcv2.UnifyVariable(saveReg, isReader: false));`
  - `inHead == false` (WRITE mode) ⇒ `int tempReg = ctx.AllocateTemp(); ctx.Emit(new Bc.PutStructure(".", 2, tempReg));` + recurse head/tail with `inHead: false`; `ctx.Emit(new Bcv2.UnifyVariable(tempReg, isReader: false));`
- `StructTerm st`: identical dual-mode shape — Push/UnifyStructure/Pop in HEAD, PutStructure in BODY; recurse subargs.
- `UnderscoreTerm`: `ctx.Emit(new Bc.UnifyVoid(count: 1));` (C# named arg `count: 1` preserved per `rf-dart-named-default-param-to-csharp-optional-arg`).

### `_GenerateGuard` (lines 393-459)

Cascade-of-special-cases preserved verbatim — DO NOT collapse to a hash-map lookup (the early-return cascade is the documented optimisation):

```
if (guard.Predicate == "ground" && guard.Args.Count == 1) {
    if (guard.Args[0] is VarTerm vt) {
        var varInfo = varTable.GetVar(vt.Name);
        if (varInfo is not null) {
            ctx.Emit(new Bc.Ground(varInfo.RegisterIndex!.Value, negated: guard.Negated));
            return;
        }
    }
}
// known/1, no_readers/1 mirror identically with Bc.Known / Bc.NoReaders
if (guard.Predicate == "otherwise" && guard.Args.Count == 0) {
    ctx.Emit(new Bc.Otherwise());
    return;
}
if (guard.Predicate == "=?=" && guard.Args.Count == 2) {
    if (guard.Args[0] is VarTerm leftVt && guard.Args[1] is VarTerm rightVt) {
        var leftInfo = varTable.GetVar(leftVt.Name);
        var rightInfo = varTable.GetVar(rightVt.Name);
        if (leftInfo is not null && rightInfo is not null) {
            ctx.Emit(new Bc.GroundEqual(leftInfo.RegisterIndex!.Value, rightInfo.RegisterIndex!.Value, negated: guard.Negated));
            return;
        }
    }
}
// Generic fallback
for (int i = 0; i < guard.Args.Count; i++) {
    _GeneratePutArgument(guard.Args[i], i, varTable, ctx);
}
ctx.Emit(new Bc.Guard(guard.Predicate, guard.Args.Count, negated: guard.Negated));
```

Cite `rf-dart-string-equality-to-csharp-string-equality`, `rf-dart-named-default-param-to-csharp-optional-arg`.

### `_GenerateBody` (lines 461-497)

```
private void _GenerateBody(IReadOnlyList<Goal> goals, VariableTable varTable, CodeGenContext ctx) {
    for (int i = 0; i < goals.Count; i++) {
        var goal = goals[i];
        if (goal is RemoteGoal rg) { _GenerateRemoteGoal(rg, varTable, ctx); continue; }
        if (goal is SpawnGoal sg) {
            var innerGoal = sg.InnerGoal;
            for (int j = 0; j < innerGoal.Args.Count; j++) {
                _GeneratePutArgument(innerGoal.Args[j], j, varTable, ctx);
            }
            var procedureLabel = $"{innerGoal.Functor}/{innerGoal.Arity}";
            ctx.Emit(new Bc.Spawn(procedureLabel, innerGoal.Arity));
            continue;
        }
        for (int j = 0; j < goal.Args.Count; j++) {
            _GeneratePutArgument(goal.Args[j], j, varTable, ctx);
        }
        var procLabel = $"{goal.Functor}/{goal.Arity}";
        ctx.Emit(new Bc.Spawn(procLabel, goal.Arity));
    }
    ctx.Emit(new Bc.Proceed());
}
```

Preserve indexed `for` (not `foreach`) to match Dart source structurally. All-goals-spawn (no TCO) preserved. `@AgentId` in SpawnGoal intentionally dropped (dGLP-naive emitter). Terminal `Proceed` unconditional. Cite `rf-dart-string-interpolation-to-csharp-string-interpolation`.

### `_GenerateRemoteGoal` (lines 503-535)

```
private void _GenerateRemoteGoal(RemoteGoal remote, VariableTable varTable, CodeGenContext ctx) {
    var innerGoal = remote.Goal;
    for (int j = 0; j < innerGoal.Args.Count; j++) {
        _GeneratePutArgument(innerGoal.Args[j], j, varTable, ctx);
    }
    if (remote.IsDynamic) {
        if (remote.Module is not VarTerm moduleTerm) {
            throw new CompileError("Module must be a variable in dynamic RPC", remote.Line, remote.Column, phase: "codegen");
        }
        var varInfo = varTable.GetVar(moduleTerm.Name);
        if (varInfo is null || varInfo.RegisterIndex is null) {
            throw new CompileError($"Unknown variable in dynamic RPC: {moduleTerm.Name}", remote.Line, remote.Column, phase: "codegen");
        }
        ctx.Emit(new Bc.Transmit(varInfo.RegisterIndex.Value, innerGoal.Functor, innerGoal.Arity));
    } else {
        var moduleName = remote.StaticModuleName!;
        var index = ctx.ImportTable.AddImport(moduleName);
        ctx.Emit(new Bc.Distribute(index, innerGoal.Functor, innerGoal.Arity));
    }
}
```

`remote.module as VarTerm` ⇒ pattern-matched cast `remote.Module is not VarTerm moduleTerm`. `staticModuleName!` ⇒ C# null-forgiving postfix `!`. Cite `rf-dart-cast-as-to-csharp-pattern-cast`, `rf-dart-bang-to-csharp-null-forgiving-operator`.

### `_GeneratePutArgument` (lines 537-575)

Five-way `is`-pattern cascade:
- `VarTerm vt`: null-guard + `ctx.Emit(new Bcv2.PutVariable(varInfo.RegisterIndex!.Value, argSlot, isReader: vt.IsReader));`
- `ConstTerm ct`: `ctx.Emit(new Bc.PutBoundConst(ct.Value, argSlot));`
- `ListTerm lt`: `lt.IsNil` ⇒ `ctx.Emit(new Bc.PutBoundNil(argSlot));`. Else ⇒ `ctx.Emit(new Bc.PutStructure(".", 2, argSlot));` + recurse head/tail via `_GenerateArgumentStructureElement`.
- `StructTerm st`: `ctx.Emit(new Bc.PutStructure(st.Functor, st.Arity, argSlot));` + `foreach (var arg in st.Args) { _GenerateArgumentStructureElement(arg, varTable, ctx); }`
- `UnderscoreTerm` (DISTINCT from head no-op): `int tempReg = ctx.AllocateTemp(); ctx.Emit(new Bcv2.PutVariable(tempReg, argSlot, isReader: false));`

### `_IsGroundTerm` (lines 578-590)

```
private bool _IsGroundTerm(Term term) {
    if (term is VarTerm) return false;
    if (term is ConstTerm) return true;
    if (term is ListTerm lt) {
        if (lt.IsNil) return true;
        return (lt.Head is null || _IsGroundTerm(lt.Head)) && (lt.Tail is null || _IsGroundTerm(lt.Tail));
    }
    if (term is StructTerm st) return st.Args.All(arg => _IsGroundTerm(arg));
    return false;
}
```

Cite `rf-dart-every-to-csharp-all` (LINQ `All` is short-circuit on false).

### `_GroundTermToValue` (lines 593-610) — preserved-dead-code

```
// Currently unused — preserved per CLAUDE.md preserve-working-code
private object? _GroundTermToValue(Term term) {
    if (term is ConstTerm ct) return ct.Value;
    if (term is ListTerm lt) {
        if (lt.IsNil) return "nil";
        var head = lt.Head is not null ? _GroundTermToValue(lt.Head) : null;
        var tail = lt.Tail is not null ? _GroundTermToValue(lt.Tail) : null;
        return new List<object?> { head, tail };
    }
    if (term is StructTerm st) {
        return new Dictionary<string, object?> {
            ["functor"] = st.Functor,
            ["args"] = st.Args.Select(arg => _GroundTermToValue(arg)).ToList()
        };
    }
    return null;
}
```

Cite `rf-dart-map-literal-to-csharp-dictionary-initialiser`.

### `_GenerateArgumentStructureElement` (lines 614-684)

Five-way cascade with ground-list shortcut and nested static local functions:

- `VarTerm vt`: null-guard + `ctx.Emit(new Bcv2.UnifyVariable(varInfo.RegisterIndex!.Value, isReader: vt.IsReader));`
- `ConstTerm ct`: `ctx.Emit(new Bc.UnifyConstant(ct.Value));`
- `ListTerm lt`:
  - `lt.IsNil` ⇒ `ctx.Emit(new Bc.UnifyConstant("nil"));`
  - `_IsGroundTerm(lt)` ⇒ lower via static local functions:
    ```
    static Rt.Term ConvertListToStructTerm(ListTerm l) {
        if (l.IsNil) return new Rt.ConstTerm("nil");
        static Rt.Term ConvertTerm(Term t) {
            if (t is ConstTerm ct) return new Rt.ConstTerm(ct.Value);
            if (t is ListTerm lt2) return ConvertListToStructTerm(lt2);
            if (t is StructTerm st) {
                var rtArgs = st.Args.Select(ConvertTerm).ToList();
                return new Rt.StructTerm(st.Functor, rtArgs);
            }
            return new Rt.ConstTerm(null);  // Fallback for unexpected cases
        }
        var head = l.Head is not null ? ConvertTerm(l.Head) : new Rt.ConstTerm("nil");
        var tail = l.Tail is not null ? ConvertTerm(l.Tail) : new Rt.ConstTerm("nil");
        return new Rt.StructTerm(".", new List<Rt.Term> { head, tail });
    }
    var listStructTerm = ConvertListToStructTerm(lt);
    ctx.Emit(new Bc.UnifyConstant(listStructTerm));
    ```
    Cite `rf-dart-local-function-to-csharp-local-function`. NOTE: C# requires `ConvertTerm` and `ConvertListToStructTerm` mutually recursive — declare as nested `static` local functions inside the parent method, ordering: forward-declare `ConvertTerm` via static-local-function-after-use (C# 8+ supports out-of-order local function calls within the same enclosing scope).
  - Non-ground list ⇒ `ctx.Emit(new Bc.PutStructure(".", 2, -1));` + recurse head via `_GenerateStructureElementInBody`, tail via `_GenerateListTailInBody`. The `-1` argSlot sentinel preserved verbatim. Cite `rf-dart-sentinel-magic-int-to-csharp-sentinel-magic-int`.
- `StructTerm st`: NO ground-shortcut (asymmetric vs list — preserved per source): `ctx.Emit(new Bc.PutStructure(st.Functor, st.Arity, -1));` + `foreach (var arg in st.Args) { _GenerateStructureElementInBody(arg, varTable, ctx); }`
- `UnderscoreTerm`: `ctx.Emit(new Bc.UnifyVoid(count: 1));`

### `_GenerateStructureElementInBody` (lines 688-737)

Five-way cascade with BODY-only `Set*` opcodes:
- `VarTerm vt`: null-guard (message "Undefined variable in structure:") + `ctx.Emit(new Bcv2.SetVariable(varInfo.RegisterIndex!.Value, isReader: vt.IsReader));`
- `ConstTerm ct`: `ctx.Emit(new Bc.SetConstant(ct.Value));`
- `ListTerm lt`:
  - `lt.IsNil` ⇒ `ctx.Emit(new Bc.SetConstant("nil"));`
  - else ⇒ `ctx.Emit(new Bc.PutStructure(".", 2, -1));` + recurse head via this same method, tail via `_GenerateListTailInBody` (asymmetric — load-bearing).
- `StructTerm st`: `ctx.Emit(new Bc.PutStructure(st.Functor, st.Arity, -1));` + per-arg recurse via this method.
- `UnderscoreTerm`: `int tempReg = ctx.AllocateTemp(); ctx.Emit(new Bcv2.SetVariable(tempReg, isReader: false));`

### `_GenerateListTailInBody` (lines 741-764)

```
private void _GenerateListTailInBody(Term term, VariableTable varTable, CodeGenContext ctx) {
    if (term is ListTerm lt) {
        if (lt.IsNil) {
            ctx.Emit(new Bc.SetConstant("nil"));
        } else {
            ctx.Emit(new Bc.PutStructure(".", 2, -1));
            if (lt.Head is not null) _GenerateStructureElementInBody(lt.Head, varTable, ctx);
            if (lt.Tail is not null) _GenerateListTailInBody(lt.Tail, varTable, ctx);
        }
    } else {
        _GenerateStructureElementInBody(term, varTable, ctx);
    }
}
```

Cite `rf-dart-mutually-recursive-methods-to-csharp-mutually-recursive-methods`.

### `CompileError` throw sites (six total)

All six preserve `throw new CompileError($"<msg>", term.Line, term.Column, phase: "codegen");` with distinct message strings. CompileError type per error.dart spec (`rf-dart-implements-exception-to-csharp-derive-system-exception`).

## 3. Decomposed Task Units

- T1: Emit C# namespace + alias `using` directives (Bc, Bcv2, Rt) — done
- T2: Port `ImportTable` class with `Dictionary<string,int>`, 1-indexed insertion, TryGetValue lookup, OrderBy(value) snapshot — done
- T3: Port `CodeGenContext` class with `List<object>` instructions + Dictionary + HashSet + auto-properties + `Emit` / `EmitLabel` / `AllocateTemp` / `ResetTemps` — done
- T4: Port `CodeGenerator` shell + public `Generate` + `GenerateWithMetadata` with first-procedure variable-map collection — done
- T5: Port `_GenerateProcedure` with entry label + clause walk + NoMoreClauses terminator — done
- T6: Port debug-print fence `if (proc.Signature == "foo/1")` verbatim with nine type-test branches — done
- T7: Port `_GenerateClause` with three-phase ClauseTry/HEAD/GUARDS/Commit/BODY framing + `true/0` fast-path — done
- T8: Port `_GenerateHead` + `_GenerateHeadArgument` with five-way cascade + first-occurrence GetVariable/GetValue dispatch + struct temp-extract — done
- T9: Port `_GenerateStructureElement` dual-mode HEAD/BODY with Push/Pop save-register dance — done
- T10: Port `_GenerateGuard` with five-special-case dispatch table + generic Guard fallback — done
- T11: Port `_GenerateBody` with RemoteGoal / SpawnGoal / default Spawn dispatch + terminal Proceed — done
- T12: Port `_GenerateRemoteGoal` with static Distribute + dynamic Transmit dispatch + import-table side-effect-on-lookup — done
- T13: Port `_GeneratePutArgument` with five-way cascade + fresh-writer-for-underscore — done
- T14: Port `_IsGroundTerm` with short-circuit `.All` predicate — done
- T15: Port `_GroundTermToValue` (preserved-dead-code) with annotated comment — done
- T16: Port `_GenerateArgumentStructureElement` with ground-list shortcut + nested static local functions `ConvertListToStructTerm` / `ConvertTerm` — done
- T17: Port `_GenerateStructureElementInBody` + `_GenerateListTailInBody` mutually recursive pair with BODY-only Set* opcodes + `-1` argSlot sentinel — done
- T18: Six `CompileError` throw sites with distinct messages preserved verbatim — done

## 4. Research Findings

None required — every construct is decided by cached `research_finding_id` references (twenty-six in total, table in convspec §B "Reuse summary") sourced from prior compiler/* + bytecode/* convspecs. No new research spawned.

## 5. Consistency Pass

- Imports + alias directives — fixed — derived from `rf-dart-relative-import-to-csharp-using-or-same-namespace` (cached) + convspec §1 import construct.
- `ImportTable` — fixed — derived from `rf-dart-mutable-dictionary-class` (cached) + convspec ImportTable construct.
- `CodeGenContext` heterogeneous list — fixed — derived from `rf-dart-dynamic-to-csharp-object` (cached, runner.dart spec) + convspec CodeGenContext construct.
- `CodeGenerator` two-method surface + reference-identity first-procedure check — fixed — derived from `rf-dart-list-first-to-csharp-zero-indexer` + `rf-dart-foreach-final-to-csharp-foreach-var` (cached) + convspec CodeGenerator construct.
- `_GenerateProcedure` debug-print fence — fixed — derived from CLAUDE.md "Preserve Working Code" + `rf-dart-runtime-type-check-to-csharp-is-pattern` (cached) + convspec debug-fence nuance.
- `_GenerateClause` three-phase + `true/0` fast-path — fixed — derived from CLAUDE.md "GLP Quick Reference" three-phase invariant + convspec _generateClause construct + `rf-dart-bang-to-csharp-not-null-pattern` (cached).
- `_GenerateHeadArgument` first-occurrence + struct temp-extract — fixed — derived from convspec _generateHeadArgument construct nuances (1)–(5) + `rf-dart-is-typetest-cascade-to-csharp-is-pattern-cascade` (cached).
- `_GenerateStructureElement` Push/Pop save-register — fixed — derived from FCP AM design invariant cited in convspec + `rf-dart-named-required-param-to-csharp-positional-arg` (cached).
- `_GenerateGuard` cascade preservation — fixed — derived from convspec _generateGuard construct + `rf-dart-string-equality-to-csharp-string-equality` (cached).
- `_GenerateBody` all-goals-spawn + intentional `@AgentId` drop — fixed — derived from convspec _generateBody nuances (1)–(4) + `rf-dart-string-interpolation-to-csharp-string-interpolation` (cached).
- `_GenerateRemoteGoal` static/dynamic RPC discriminator — fixed — derived from FCP rpc.cp:164-175 citation in convspec + `rf-dart-cast-as-to-csharp-pattern-cast` (cached).
- `_GeneratePutArgument` underscore-vs-head asymmetry — fixed — derived from convspec _generatePutArgument nuance (1) + CLAUDE.md SRSW underscore semantics.
- `_IsGroundTerm` short-circuit — fixed — derived from `rf-dart-every-to-csharp-all` (cached).
- `_GroundTermToValue` dead-code preservation — fixed — derived from CLAUDE.md "Preserve Working Code — NEVER remove without explicit approval" + `rf-dart-map-literal-to-csharp-dictionary-initialiser` (cached).
- `_GenerateArgumentStructureElement` ground-list shortcut + nested local functions — fixed — derived from convspec _generateArgumentStructureElement construct + `rf-dart-local-function-to-csharp-local-function` (cached) + `rf-dart-sentinel-magic-int-to-csharp-sentinel-magic-int` (cached).
- `_GenerateStructureElementInBody` / `_GenerateListTailInBody` mutual recursion — fixed — derived from `rf-dart-mutually-recursive-methods-to-csharp-mutually-recursive-methods` (cached).
- Six `CompileError` throw sites — fixed — derived from `rf-dart-implements-exception-to-csharp-derive-system-exception` (cached, error.dart) + convspec CompileError-throwing-parity nuance.

## 6. Escalations

None.
