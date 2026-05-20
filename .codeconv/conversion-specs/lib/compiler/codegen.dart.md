# Conversion Spec — lib/compiler/codegen.dart

> Conversion-spec artifact for lib/compiler/codegen.dart (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> File is the GLP **bytecode emitter** — 766 lines, three top-level
> classes (`ImportTable`, `CodeGenContext`, `CodeGenerator`) plus three
> nested local function closures (the local-recursive `convertTerm` /
> `convertListToStructTerm` inside `_generateArgumentStructureElement`).
> The emitter walks an **annotated AST** (from analyzer.dart) and emits
> a flat `List<dynamic>` of v1 / v2 bytecode instructions (the
> intentional v1+v2 heterogeneity is preserved — see runner.dart spec).
> Heavy reuse from the entire `lib/compiler/*` + `lib/bytecode/*` prior
> specs — ast.dart (`VarTerm` / `ConstTerm` / `ListTerm` / `StructTerm`
> / `UnderscoreTerm` / `RemoteGoal` / `SpawnGoal` / `Atom` / `Goal`),
> analyzer.dart (`AnnotatedProgram` / `AnnotatedProcedure` /
> `AnnotatedClause` / `VariableTable` / `VariableInfo.registerIndex`),
> error.dart (`CompileError` w/ named `phase` arg), result.dart
> (`CompilationResult`), opcodes.dart + opcodes_v2.dart (every emitted
> bytecode node type), asm.dart (the `BytecodeProgram` ctor), runner.dart
> (the dynamic-dispatch / `object?` instruction slot decision — load-
> bearing here at the `List<dynamic> instructions` declaration site).
> ≥90 % of constructs map onto already-cached research findings + already-
> recorded idiomatic decisions. New nuances unique to this file: (1) the
> "first-occurrence-in-head" tracking via `Set<String> seenHeadVars` (a
> small but load-bearing dispatch between v2 `GetVariable` vs `GetValue`
> emit), (2) the **debug-print fence** (lines 164–187 — a guarded
> `print()` block triggered only when `proc.signature == 'foo/1'`,
> retained verbatim per the preserve-working-code discipline), (3) the
> **HEAD/BODY structure-element mode flag** (`inHead: true|false`)
> threaded through `_generateStructureElement`, and (4) the **Push/Pop
> nested-structure preservation pattern** (FCP AM design — saveReg /
> Push / UnifyStructure / Pop / `UnifyVariable(saveReg, isReader:
> false)`, lines 345–385, load-bearing for nested-list and nested-struct
> head matching). Per the spec contract every non-trivial construct here
> ATTACHES BOTH a deep-analysis basis AND a research finding (or an
> idiom_id to an already-decided one).

```yaml
schema_version: 1
source_path: lib/compiler/codegen.dart
source_sha256: fdeeb685673893129e721409ea2b4ceb0e6f356d406efd526ae32d4cae64d3fd
target_code_unit: lib/compiler/codegen.cs
constructs:
  - construct_key: dart.module.relative_imports_three_bytecode_three_compiler_one_runtime_with_two_show_filters_and_two_prefix_aliases
    source_form: >-
      "import 'package:glp_runtime/bytecode/opcodes.dart' as bc;
      import 'package:glp_runtime/bytecode/opcodes_v2.dart' as bcv2;
      import 'package:glp_runtime/bytecode/asm.dart';
      import 'package:glp_runtime/bytecode/runner.dart' show BytecodeProgram;
      import 'package:glp_runtime/runtime/terms.dart' as rt;
      import 'ast.dart';
      import 'analyzer.dart';
      import 'error.dart';
      import 'result.dart';" — five package: imports (four bytecode, one
      runtime/terms) plus four sibling relative imports. Two distinct
      mechanisms in play: (a) `as bc;` / `as bcv2;` / `as rt;` **prefix
      aliases** (the only way disambiguation between v1 `bc.PutStructure`
      vs v2 `bcv2.PutVariable` vs the runtime-side `rt.StructTerm` /
      `rt.ConstTerm` is possible), (b) `show BytecodeProgram` selective
      filter (a single symbol from runner.dart — the rest of runner.dart
      is the VM internals, which the emitter MUST NOT depend on).
    target_decision: >-
      Sibling relative imports (`ast.dart`, `analyzer.dart`, `error.dart`,
      `result.dart`) collapse to **ZERO** `using` directives because the
      C# port places `lib/compiler/*` into ONE namespace (e.g.
      `Glp.Runtime.Compiler`) — Microsoft Learn: "All types in the same
      namespace are accessible without a `using` directive". The
      `package:glp_runtime/bytecode/{opcodes,opcodes_v2,asm,runner}.dart`
      cross-package imports become `using Glp.Runtime.Bytecode;` PLUS
      C# 6 `using static` + `using <alias> = ...` alias directives to
      preserve the v1/v2 disambiguation: `using Bc = Glp.Runtime.Bytecode.OpcodesV1;`
      and `using Bcv2 = Glp.Runtime.Bytecode.OpcodesV2;` map onto Microsoft
      Learn's `using` alias directive (Microsoft Learn: "using alias =
      type-name") — call sites then read `Bc.PutStructure(...)` /
      `Bcv2.PutVariable(...)` mirroring `bc.PutStructure(...)` /
      `bcv2.PutVariable(...)`. The Dart prefix `as bc` / `as bcv2` / `as rt`
      is the ONLY disambiguation strategy available since both opcode
      families have identically-named `PutStructure` / `UnifyVariable`
      symbols (v1 has positional `argSlot`, v2 has named `isReader`); the
      C# alias preserves the per-call disambiguation verbatim. The runtime
      prefix `as rt` becomes `using Rt = Glp.Runtime.Runtime.Terms;` —
      `rt.StructTerm` / `rt.ConstTerm` references inside
      `convertListToStructTerm` become `Rt.StructTerm` / `Rt.ConstTerm`,
      mirroring the FCP "runtime-side term tree" carry-forward decision
      from terms.dart spec. The `show BytecodeProgram` filter is satisfied
      by NOT adding a using-static for runner — only the one type is
      referenced (the `BytecodeProgram(...)` ctor call at line 130) —
      consistent with rf-dart-relative-import-to-csharp-using-or-same-
      namespace (cached across the lib/compiler/* family). Reuse
      verbatim — no new research.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-using-or-same-namespace
    nuance: >-
      Three intertwined nuances. (1) The two-prefix v1+v2 disambiguation
      is **load-bearing** — without it, `PutStructure` is ambiguous
      between opcode families. The C# alias directives MUST be present;
      omitting them silently makes the wrong opcode emit. (2) The
      `package:glp_runtime/...` URI scheme maps to a cross-namespace C#
      `using` (Glp.Runtime.Bytecode is a sibling top-level namespace —
      NOT same-namespace) whereas the four sibling imports
      `ast.dart`/`analyzer.dart`/`error.dart`/`result.dart` map to the
      same Glp.Runtime.Compiler namespace and produce ZERO using-
      directives — the two cases differ only because pubspec.yaml uses
      `package:` for cross-package and bare relative for sibling. (3)
      `show BytecodeProgram` is the **only** narrowing import; if a
      future revision casually used another runner-exported symbol the
      C# port would silently compile (full Glp.Runtime.Bytecode visible)
      while Dart would have flagged it. Risk is low and is a code-review
      concern, not a conversion concern.

  - construct_key: dart.class.import_table_one_to_one_string_to_int_with_one_indexed_insertion
    source_form: >-
      "class ImportTable { final Map<String, int> _indices = {}; int _nextIndex = 1;
      int addImport(String moduleName) { if (!_indices.containsKey(moduleName))
      { _indices[moduleName] = _nextIndex++; } return _indices[moduleName]!; }
      int? getIndex(String moduleName) => _indices[moduleName];
      int get size => _indices.length;
      List<String> get orderedImports { final entries = _indices.entries.toList();
      entries.sort((a, b) => a.value.compareTo(b.value));
      return entries.map((e) => e.key).toList(); }
      bool contains(String moduleName) => _indices.containsKey(moduleName);
      @override String toString() => 'ImportTable($_indices)'; }" — a
      classic FCP-style 1-indexed insertion-order bookkeeping table
      mapping module-name to a stable 1-based slot index (used by
      `Distribute(index, ...)` RPC emission). Six members: one mutator
      (`addImport`), one nullable-returning lookup (`getIndex`), one size
      getter, one sorted-entries-by-value snapshot getter
      (`orderedImports`), one boolean contains, one override
      `toString`.
    target_decision: >-
      Emit `public sealed class ImportTable` (reference class — NOT a
      record; the table mutates across `addImport` calls). The Dart
      `Map<String, int> _indices = {}` becomes
      `private readonly Dictionary<string, int> _indices = new();`
      (Microsoft Learn: `Dictionary<TKey,TValue>` is the canonical
      lookup-by-key implementation; `readonly` matches Dart `final` on
      the field, not on the contents). The mutable `int _nextIndex = 1`
      becomes `private int _nextIndex = 1;`. `addImport` becomes
      `public int AddImport(string moduleName)` — `containsKey` ⇒
      `ContainsKey`, the post-increment `_nextIndex++` is preserved
      verbatim (Microsoft Learn: postfix `++` returns old value then
      increments). The bang `_indices[moduleName]!` becomes a plain
      indexer access `_indices[moduleName]` — C# `Dictionary<TKey,TValue>`
      indexer throws `KeyNotFoundException` on miss, which matches the
      Dart `!` semantics (post-condition: just-inserted key MUST be
      present). `getIndex` becomes `public int? GetIndex(string moduleName)
      => _indices.TryGetValue(moduleName, out var v) ? v : (int?)null;` —
      Microsoft Learn `TryGetValue` is the idiomatic miss-tolerant lookup;
      the Dart `Map<K,V>` indexer-returning-null semantics is NOT shared
      by C# (which throws), so the TryGetValue pattern is required. `size`
      becomes `public int Size => _indices.Count;` (Dart `.length` ⇒ C#
      `.Count`, reuse rf-dart-length-isempty-to-csharp-count from prior
      specs). `orderedImports` is a SORTED snapshot — preserve the
      `LINQ-ish` shape: `public List<string> OrderedImports => _indices
      .OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();` (Microsoft
      Learn: `Enumerable.OrderBy` produces an `IOrderedEnumerable<T>`).
      `contains` becomes `public bool Contains(string moduleName)
      => _indices.ContainsKey(moduleName);`. The `@override String
      toString()` becomes `public override string ToString() => $"ImportTable
      ({string.Join(", ", _indices.Select(kv => $"{kv.Key}: {kv.Value}"))})"
      ;` — Dart `'ImportTable($_indices)'` uses the map's default `Map
      .toString()` (`{a: 1, b: 2}` shape); C# `Dictionary<TKey,TValue>
      .ToString()` returns the type name only, so the explicit
      interpolation reproduces the Dart shape. Reuse the cached
      rf-dart-mutable-dictionary-class from analyzer.dart family.
    idiom_id: null
    research_finding_id: rf-dart-mutable-dictionary-class
    nuance: >-
      Three intertwined nuances. (1) **1-indexed not 0-indexed**: the
      `_nextIndex = 1` start is FCP-conformant (cited in the source as
      "Following FCP convention where imports are indexed 1, 2, 3, ...");
      preserve verbatim — the consuming `Distribute(index, ...)` opcode
      treats `0` as the SELF module, so 1-indexing is load-bearing. (2)
      **Insertion-order preservation**: Dart `Map<K, V> = {}` literal
      preserves insertion order (Dart language spec). C# `Dictionary<TKey,
      TValue>` ALSO preserves insertion order (Microsoft Learn: "the order
      in which the items are returned is undefined" — historically true
      in .NET Framework, but since .NET Core 3.0 / .NET 5+ Dictionary
      DOES preserve insertion order as an implementation detail though
      NOT contractually). The `_nextIndex++` makes the 1-indexed slot the
      AUTHORITATIVE order — `orderedImports` then sorts by value to
      guarantee insertion order REGARDLESS of dictionary iteration order
      — preserve the explicit sort. (3) **Bang vs throw**: Dart `!` is a
      runtime null-assertion that throws `_TypeError` on null; the C# port
      preserves the same throw-on-violation contract via the dictionary
      indexer's `KeyNotFoundException`. Both throw on contract violation;
      neither silently coerces.

  - construct_key: dart.class.codegen_context_with_dynamic_instruction_list_and_phase_flags_and_temp_allocator_and_import_table
    source_form: >-
      "class CodeGenContext { final List<dynamic> instructions = [];
      final Map<String, int> labels = {}; final List<String> pendingLabels = [];
      int nextTempVar = 10; final Map<String, int> tempAllocation = {};
      String? currentProcedure; int currentClauseIndex = 0;
      bool inHead = false; bool inGuard = false; bool inBody = false;
      final Set<String> seenHeadVars = {};
      final ImportTable importTable = ImportTable();
      int get currentPC => instructions.length;
      void emit(dynamic instruction) { instructions.add(instruction); }
      void emitLabel(String label) { labels[label] = currentPC;
      instructions.add(bc.Label(label)); }
      int allocateTemp() => nextTempVar++;
      void resetTemps(int variableCount) { nextTempVar = variableCount > 10
      ? variableCount : 10; tempAllocation.clear(); } }" — twelve fields
      (eight `final` references to mutable containers, four primitive
      mutables) plus four methods. The crown jewel is `List<dynamic>
      instructions` — a heterogeneous list holding BOTH v1 `bc.*` and v2
      `bcv2.*` opcode nodes (preserved heterogeneity, NOT a sealed-
      hierarchy; consumed by runner.dart's dynamic-dispatch).
    target_decision: >-
      Emit `public sealed class CodeGenContext` (reference class — the
      emitter mutates this in place across every `_generateXxx` call).
      `final List<dynamic> instructions = [];` becomes `public List<object>
      Instructions { get; } = new();` — the `dynamic` type maps to
      `object` (Microsoft Learn: "object is the ultimate base class of
      all C# types"; using `dynamic` in C# is unrelated — Dart `dynamic`
      means "any type, no static check", C# `dynamic` means "late-bound
      DLR dispatch"). The runner.dart spec already nominated `object?`
      for the parallel "cell content" decision; the emitter parallel is
      `object` (non-null — emit never adds null to instructions). Reuse
      the rf-dart-dynamic-to-csharp-object cached idiom (runner.dart
      spec). The `Map<String, int> labels` and `Map<String, int>
      tempAllocation` become `public Dictionary<string, int> Labels
      { get; } = new();` and `public Dictionary<string, int>
      TempAllocation { get; } = new();`. `List<String> pendingLabels`
      becomes `public List<string> PendingLabels { get; } = new();`. Four
      primitive mutables — `int nextTempVar = 10`, `int currentClauseIndex
      = 0`, three `bool inHead/inGuard/inBody = false` — become
      `public int NextTempVar { get; set; } = 10;`, etc. (auto-
      properties with default initialisers — Microsoft Learn). `String?
      currentProcedure` becomes `public string? CurrentProcedure { get;
      set; }` under `#nullable enable`. `Set<String> seenHeadVars` becomes
      `public HashSet<string> SeenHeadVars { get; } = new();` (Microsoft
      Learn: `HashSet<T>` is the canonical insertion-cheap, contains-
      cheap, unordered-set implementation). `ImportTable importTable`
      becomes `public ImportTable ImportTable { get; } = new();` —
      reference, ctor-initialised. `int get currentPC => Instructions.
      Count;` (expression-bodied get-only property — Microsoft Learn).
      `void Emit(object instruction) => Instructions.Add(instruction);`
      (expression-bodied void method — Microsoft Learn). `void EmitLabel(
      string label) { Labels[label] = CurrentPC; Instructions.Add(new Bc.
      Label(label)); }`. `int AllocateTemp() => NextTempVar++;` —
      preserve the post-increment-then-return semantics verbatim (Microsoft
      Learn: postfix `++` returns the old value then increments).
      `ResetTemps(int variableCount)` is preserved with the ternary
      `NextTempVar = variableCount > 10 ? variableCount : 10;` —
      Microsoft Learn ternary `?:` is identical to Dart's.
    idiom_id: null
    research_finding_id: rf-dart-dynamic-to-csharp-object
    nuance: >-
      Four intertwined nuances. (1) **`dynamic` is the heterogeneous-
      bytecode container**: Dart `List<dynamic>` is the ONLY way to
      store both v1 `bc.PutStructure` (positional argSlot) and v2
      `bcv2.PutVariable` (named isReader) objects in the same list
      without a common base interface. The C# port uses `List<object>`
      not `List<dynamic>` — Dart `dynamic` ⇒ C# `object` because the
      consumers (runner.dart) use `if (instr is Bc.PutStructure ps) ...
      else if (instr is Bcv2.PutVariable pv) ...` type-test dispatch
      (the runner.dart spec already locked this in). (2) **Temp register
      base 10**: `nextTempVar = 10` is deliberate — argument registers
      occupy 0..9, temps start at 10 to avoid collision. `resetTemps`'s
      `variableCount > 10 ? variableCount : 10` clamps the start to
      `max(variableCount, 10)`. Preserve verbatim — the clamp is load-
      bearing (a clause with 12 named variables and 5 temps needs temps
      at 12, 13, 14, 15, 16, not 10, 11, 12, 13, 14 — would alias the
      named variables). (3) **Phase flags are advisory**: `inHead /
      inGuard / inBody` are SET by `_generateClause` but never READ in
      this file. They look dead — but they are part of the public-
      surface contract for a future code-walker (e.g. a debugger or
      tracer) that observes the emit phase. Preserve verbatim per the
      preserve-working-code discipline (CLAUDE.md). (4) **Public field
      vs property**: Dart fields look like plain fields but C# style
      idiomatically wraps them in auto-properties — the spec records
      auto-properties; downstream consumers (the emitter) write
      `ctx.Instructions.Add(...)` which works identically whether
      `Instructions` is a public field or a get-only auto-property.

  - construct_key: dart.class.code_generator_with_program_walk_and_per_procedure_per_clause_helpers
    source_form: >-
      "class CodeGenerator { BytecodeProgram generate(AnnotatedProgram program)
      { final result = generateWithMetadata(program); return result.program; }
      CompilationResult generateWithMetadata(AnnotatedProgram program) {
        final ctx = CodeGenContext(); final variableMap = <String, int>{};
        for (final proc in program.procedures) {
          _generateProcedure(proc, ctx);
          if (proc == program.procedures.first) { ... collect variableMap from
            first-procedure clauses' varTable ... }
        }
        final bytecode = BytecodeProgram(ctx.instructions);
        return CompilationResult(bytecode, variableMap);
      }
      void _generateProcedure(...) { ... }
      void _generateClause(...) { ... }
      void _generateHead(...) { ... }
      void _generateHeadArgument(...) { ... }
      void _generateStructureElement(..., {required bool inHead}) { ... }
      void _generateGuard(...) { ... }
      void _generateBody(...) { ... }
      void _generateRemoteGoal(...) { ... }
      void _generatePutArgument(...) { ... }
      bool _isGroundTerm(...) { ... }
      Object? _groundTermToValue(...) { ... }
      void _generateArgumentStructureElement(...) { ... }
      void _generateStructureElementInBody(...) { ... }
      void _generateListTailInBody(...) { ... } }" — fifteen methods, two
      public (`generate`, `generateWithMetadata`) and thirteen private
      `_generateXxx` / `_isGroundTerm` / `_groundTermToValue` helpers.
      Reference semantics throughout (no per-call defensive copy).
    target_decision: >-
      Emit `public sealed class CodeGenerator` (reference class — the
      emitter is a stateless dispatcher that builds a CodeGenContext per
      invocation; `CodeGenerator` itself holds NO fields). The public
      `BytecodeProgram Generate(AnnotatedProgram program)` is a thin
      delegate: `public BytecodeProgram Generate(AnnotatedProgram
      program) => GenerateWithMetadata(program).Program;`. The public
      `CompilationResult GenerateWithMetadata(AnnotatedProgram program)`
      walks the procedure list with a deliberate aliasing check —
      `proc == program.procedures.first` — preserve as `if (proc ==
      program.Procedures[0])` (the `.first` Dart getter ⇒ `[0]` indexer;
      reuse rf-dart-list-first-to-csharp-zero-indexer family from
      analyzer.dart). Reference-identity comparison is critical here:
      `==` on reference-typed objects in C# IS identity (Microsoft
      Learn: "By default, two reference-type operands are equal if they
      refer to the same object"). The thirteen private helpers all
      become `private void _GenerateXxx(...)` / `private bool
      _IsGroundTerm(...)` / `private object? _GroundTermToValue(...)` —
      Microsoft Learn: leading-underscore in C# is convention not syntax,
      preserved for parity per rf-dart-leading-underscore-privacy-to-
      csharp-private (cached). `Object?` return type ⇒ `object?` per
      NRT. The `{required bool inHead}` named-required param on
      `_generateStructureElement` becomes `private void
      _GenerateStructureElement(Term term, VariableTable varTable,
      CodeGenContext ctx, bool inHead)` — C# 11 has no named-required
      params; the closest faithful mapping is positional-required (drop
      `{}` braces); reuse rf-dart-named-required-param-to-csharp-
      positional-arg (cached from analyzer.dart). The `BytecodeProgram
      (ctx.instructions)` ctor call directly maps; reuse the asm.dart-
      cached idiom rf-dart-typed-ctor-call-to-csharp-new (no new
      research). Local helper `convertListToStructTerm` and its nested
      `convertTerm` inside `_generateArgumentStructureElement` (lines
      635–651) become C# 7+ local functions (Microsoft Learn: "local
      functions enable you to declare a method inside the body of
      another method") — `static Rt.Term ConvertListToStructTerm(
      ListTerm l) { ... }` and `static Rt.Term ConvertTerm(Term t)
      { ... }` (both static — neither captures `this`-state; they only
      reference `Rt.*` static factories and recurse). Preserve recursive
      shape verbatim. The variable-map collection inside
      `generateWithMetadata` becomes a nested `foreach (var clause in
      proc.Clauses) foreach (var varInfo in clause.VarTable.GetAllVars())
      { if (varInfo.RegisterIndex.HasValue) variableMap[varInfo.Name] =
      varInfo.RegisterIndex.Value; }` — reuse rf-dart-foreach-final-to-
      csharp-foreach-var (cached).
    idiom_id: null
    research_finding_id: rf-dart-foreach-final-to-csharp-foreach-var
    nuance: >-
      Four intertwined nuances. (1) **`proc == program.procedures.first`
      identity check is load-bearing**: the variable-map collection runs
      ONLY for the first procedure because that procedure is the
      query/goal entry (the REPL goal `merge([1,2,3],[a,b],Xs).`
      compiles into the first procedure; subsequent procedures are the
      defined predicates). C# reference-`==` matches Dart reference-`==`
      verbatim because both AnnotatedProcedure are reference types. (2)
      **`registerIndex!` (Dart bang) ⇒ `RegisterIndex.Value`**: Dart
      `varInfo.registerIndex!` throws on null; C# `Nullable<int>.Value`
      ALSO throws `InvalidOperationException` on null — same throwing
      contract. Reuse rf-dart-bang-to-csharp-nullable-value (cached
      from analyzer.dart). (3) **CodeGenerator is stateless**: the
      class has NO fields — every invocation builds a fresh
      CodeGenContext. So `CodeGenerator` is safely shareable across
      threads (the emitter is reentrant). The C# port preserves the
      no-field design — DO NOT cache the context, DO NOT add per-
      instance state. (4) **Two-public-one-private surface**: `Generate`
      and `GenerateWithMetadata` are both public — the latter exposes
      the variable-map (used by the REPL to map back goal variables
      `Xs` ↔ register slots), the former is the strict-bytecode shape.
      Preserve both; do NOT collapse to one with an optional out-param —
      the two-method shape is the published API contract.

  - construct_key: dart.method.generate_procedure_with_entry_label_and_clause_walk_and_no_more_clauses_terminator
    source_form: >-
      "void _generateProcedure(AnnotatedProcedure proc, CodeGenContext ctx)
      { ctx.currentProcedure = proc.signature;
        final entryLabel = proc.signature;
        ctx.emitLabel(entryLabel);
        proc.entryPC = ctx.currentPC;
        proc.entryLabel = entryLabel;
        for (int i = 0; i < proc.clauses.length; i++) {
          ctx.currentClauseIndex = i;
          final isLastClause = (i == proc.clauses.length - 1);
          final clause = proc.clauses[i];
          final nextLabel = isLastClause ? '${entryLabel}_end' :
            '${entryLabel}_c${i + 1}';
          _generateClause(clause, ctx, nextLabel, isLastClause);
        }
        ctx.emitLabel('${entryLabel}_end');
        ctx.emit(bc.NoMoreClauses());
        if (proc.signature == 'foo/1') {
          print('\n=== BYTECODE FOR ${proc.signature} ===');
          for (int i = 0; i < ctx.instructions.length; i++) {
            final instr = ctx.instructions[i];
            String details = '';
            if (instr is bc.HeadStructure) { details = ' HeadStructure(...)'; }
            else if (instr is bc.UnifyConstant) { ... }
            ... [9 type-test branches: HeadStructure, UnifyConstant,
                  PutStructure, PutVariable, GetVariable, UnifyVariable,
                  Spawn] ...
            print('  $i: ${instr.runtimeType}$details');
          }
          print('=== END BYTECODE ===\n');
        } }" — the per-procedure entry-emit + clause walk + end-label
      + NoMoreClauses terminator + the **debug-print fence** keyed on
      the magic signature `'foo/1'`.
    target_decision: >-
      `private void _GenerateProcedure(AnnotatedProcedure proc,
      CodeGenContext ctx)` follows the Dart shape 1:1. `ctx.CurrentProcedure
      = proc.Signature;` — `signature` is Dart-side a string like
      `"merge/3"` exposed by analyzer.dart spec; reuse the cached
      AnnotatedProcedure mapping. `ctx.EmitLabel(entryLabel);` —
      `EmitLabel` was specced above. `proc.EntryPC = ctx.CurrentPC;` and
      `proc.EntryLabel = entryLabel;` mutate the AnnotatedProcedure (the
      analyzer.dart spec already nominated EntryPC / EntryLabel as
      mutable settable properties — load-bearing field-mutation pattern).
      The `for (int i = 0; ...)` loop maps verbatim per Microsoft Learn's
      `for` statement — same semantics as Dart. The dollar-sign string
      interpolation `'${entryLabel}_end'` becomes C# 6 string interpolation
      `$"{entryLabel}_end"` (Microsoft Learn). `clauses[i]` ⇒ `Clauses[i]`
      indexer (Microsoft Learn `IList<T>` indexer). `_generateClause(...)`
      call site preserved. The terminator pair — `EmitLabel(...) + Emit
      (new Bc.NoMoreClauses())` — preserved verbatim. The **debug fence**
      `if (proc.signature == 'foo/1')` is PRESERVED VERBATIM per the
      preserve-working-code discipline (CLAUDE.md "Preserve Working
      Code"). The fence walks the just-emitted instructions and dumps
      type-test-branched details to stdout. The C# port emits the same
      fence: `if (proc.Signature == "foo/1") { Console.WriteLine($"\n===
      BYTECODE FOR {proc.Signature} ===\n"); for (int i = 0; i < ctx.
      Instructions.Count; i++) { var instr = ctx.Instructions[i]; string
      details = ""; if (instr is Bc.HeadStructure hs) { details = $"
      HeadStructure(\"{hs.Functor}\", {hs.Arity}, argSlot: {hs.ArgSlot})";
      } else if (instr is Bc.UnifyConstant uc) { details = $"
      UnifyConstant({uc.Value})"; } else if (instr is Bc.PutStructure ps)
      { details = $" PutStructure(\"{ps.Functor}\", {ps.Arity}, {ps.
      ArgSlot})"; } else if (instr is Bcv2.PutVariable pv) { details = $"
      PutVariable(reg={pv.VarIndex}, slot={pv.ArgSlot}, reader={pv.
      IsReader})"; } else if (instr is Bcv2.GetVariable gv) { ... } else
      if (instr is Bcv2.UnifyVariable uv) { ... } else if (instr is Bc.
      Spawn sp) { details = $" Spawn(\"{sp.ProcedureLabel}\", arity={sp.
      Arity})"; } Console.WriteLine($"  {i}: {instr.GetType().Name}
      {details}"); } Console.WriteLine("=== END BYTECODE ===\n"); }`.
      `instr.runtimeType` maps to C# `instr.GetType().Name` (Microsoft
      Learn: `Type.Name` returns the type name without namespace). The
      type-test-with-binding pattern `instr is Bc.HeadStructure hs`
      replaces Dart's separate-test-then-cast — Microsoft Learn pattern-
      matching `is` expression. Reuse rf-dart-runtime-type-check-to-csharp-
      is-pattern (cached from runner.dart).
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-check-to-csharp-is-pattern
    nuance: >-
      Five intertwined nuances. (1) **Debug fence is NOT dead code**:
      the `proc.signature == 'foo/1'` fence is a runtime-guarded
      debug print used by Gabi during development; the `'foo/1'`
      signature is a probe-procedure inserted by tests. Removing the
      fence breaks the development workflow even though no production
      code path exercises it. Preserve verbatim (CLAUDE.md preserve-
      working-code discipline). (2) **String interpolation**: Dart
      `'${entryLabel}_end'` maps to C# `$"{entryLabel}_end"` — both
      perform `toString()`-on-non-strings; both have identical escaping
      rules for `$` in literal text (Dart `\$` literal vs C# `{{` /
      `}}` for braces). The C# port's interpolated strings have
      semantically identical behaviour. (3) **`'foo/1'` magic-string**:
      preserve verbatim — this is a probe identifier, not a Dart-vs-C#
      conversion concern. (4) **`NoMoreClauses` terminator semantics**:
      the opcodes.dart spec lists `NoMoreClauses` as the "suspend if U
      non-empty, else fail" handler — preserve the emission order
      (label-then-NoMoreClauses) because the runner pre-indexes labels
      and the terminator's PC is the fall-through landing pad. (5)
      **runtimeType vs GetType().Name**: Dart `runtimeType` returns a
      `Type` object whose `toString()` is the type name; C# `GetType()
      .Name` returns the unqualified type name. Both produce the same
      observable string ("HeadStructure", "PutStructure", etc.) for the
      debug print's purpose.

  - construct_key: dart.method.generate_clause_with_three_phase_emit_clause_try_head_guard_commit_body_proceed
    source_form: >-
      "void _generateClause(AnnotatedClause clause, CodeGenContext ctx,
        String nextLabel, bool isLastClause) {
        ctx.resetTemps(clause.varTable.getAllVars().length);
        ctx.seenHeadVars.clear();
        if (ctx.currentClauseIndex > 0) {
          ctx.emitLabel('${ctx.currentProcedure}_c${ctx.currentClauseIndex}');
        }
        ctx.emit(bc.ClauseTry());
        ctx.inHead = true; ctx.inGuard = false; ctx.inBody = false;
        _generateHead(clause.ast.head, clause.varTable, ctx);
        if (clause.hasGuards && clause.ast.guards != null) {
          ctx.inHead = false; ctx.inGuard = true;
          for (final guard in clause.ast.guards!) {
            _generateGuard(guard, clause.varTable, ctx);
          }
        }
        ctx.emit(bc.Commit());
        ctx.inHead = false; ctx.inGuard = false; ctx.inBody = true;
        if (clause.hasBody && clause.ast.body != null) {
          if (clause.ast.body!.length == 1 &&
              clause.ast.body![0].functor == 'true' &&
              clause.ast.body![0].arity == 0) {
            ctx.emit(bc.Proceed());
          } else { _generateBody(clause.ast.body!, clause.varTable, ctx); }
        } else { ctx.emit(bc.Proceed()); }
      }" — the three-phase HEAD → GUARDS → BODY emit, framed by
      ClauseTry (Si=∅, σ̂w=∅ init) and Commit (apply σ̂w, enter BODY),
      with the **`body == [true/0]` fast-path** that emits Proceed
      without spawning.
    target_decision: >-
      `private void _GenerateClause(AnnotatedClause clause, CodeGenContext
      ctx, string nextLabel, bool isLastClause)` is direct 1:1. The
      `clause.varTable.getAllVars().length` ⇒ `clause.VarTable.GetAllVars
      ().Count` (analyzer.dart spec already nominated GetAllVars as
      returning `IReadOnlyList<VariableInfo>`; `.Count` per rf-dart-
      length-isempty-to-csharp-count). `ctx.SeenHeadVars.Clear();`
      (HashSet.Clear — Microsoft Learn). The `if (ctx.currentClauseIndex
      > 0)` early-label-emit is preserved verbatim. The ClauseTry /
      Commit emissions are 1:1 (opcodes.dart spec already nominated
      `new Bc.ClauseTry()` / `new Bc.Commit()`). The phase-flag
      assignments are preserved (advisory only — see CodeGenContext
      nuance above). The **`clause.ast.body![0].functor == 'true' &&
      clause.ast.body![0].arity == 0` fast-path** is preserved verbatim:
      `if (clause.HasBody && clause.Ast.Body is not null) { if (clause.
      Ast.Body.Count == 1 && clause.Ast.Body[0].Functor == "true"
      && clause.Ast.Body[0].Arity == 0) { ctx.Emit(new Bc.Proceed()); }
      else { _GenerateBody(clause.Ast.Body, clause.VarTable, ctx); } }
      else { ctx.Emit(new Bc.Proceed()); }`. The `body!` Dart bang ⇒ C#
      NRT `body is not null` declaration-pattern + unconditional access
      thereafter (Microsoft Learn: "After a not-null check, the
      compiler considers the variable non-null in the branch"). Reuse
      rf-dart-bang-to-csharp-not-null-pattern (cached from analyzer.dart).
    idiom_id: null
    research_finding_id: rf-dart-bang-to-csharp-not-null-pattern
    nuance: >-
      Three intertwined nuances. (1) **Three-phase invariant is
      LOAD-BEARING** (GLP runtime spec §3): HEAD must complete before
      GUARDS begin, GUARDS must complete before BODY begins; the
      ClauseTry-then-Commit framing enforces this at the bytecode
      level. The C# port MUST preserve the emit ORDER verbatim (head
      args → guard list → Commit → body) — the runner depends on this
      ordering at fetch time. (2) **`true/0` fast-path is observable**:
      the spec-internal `true/0` is a synthetic always-succeed marker
      that MUST NOT spawn a goal — spawning `true/0` would consume a
      goal-queue slot for a no-op. The C# port MUST preserve the
      length-and-functor-and-arity check exactly; do NOT replace with
      a generic LINQ predicate (e.g. `body.Any(g => g.Functor == "true"
      && g.Arity == 0)`) — that changes semantics from "ONLY goal is
      true/0" to "ANY goal is true/0", which is wrong. (3) **`ast.body!`
      is double-tested**: the source already gates on `body != null`
      then uses `body!` — preserve the explicit not-null gate; do not
      collapse to `body?.Count == 1` which has different short-circuit
      semantics on null.

  - construct_key: dart.method.generate_head_argument_with_three_phase_dispatch_var_const_list_struct_underscore_and_first_occurrence_v2_get_variable_vs_get_value
    source_form: >-
      "void _generateHeadArgument(Term term, int argSlot, VariableTable
      varTable, CodeGenContext ctx) {
        if (term is VarTerm) {
          final varInfo = varTable.getVar(term.name);
          if (varInfo == null) throw CompileError(...);
          final regIndex = varInfo.registerIndex!;
          final baseVarName = term.name.endsWith('?') ?
            term.name.substring(0, term.name.length - 1) : term.name;
          final isFirstOccurrence = !ctx.seenHeadVars.contains(baseVarName);
          if (isFirstOccurrence) {
            ctx.emit(bcv2.GetVariable(regIndex, argSlot, isReader: term.isReader));
            ctx.seenHeadVars.add(baseVarName);
          } else {
            ctx.emit(bcv2.GetValue(regIndex, argSlot, isReader: term.isReader));
          }
        } else if (term is ConstTerm) { ctx.emit(bc.HeadConstant(term.value, argSlot)); }
        else if (term is ListTerm) {
          if (term.isNil) { ctx.emit(bc.HeadNil(argSlot)); }
          else { ctx.emit(bc.HeadStructure('.', 2, argSlot));
            if (term.head != null) _generateStructureElement(term.head!, varTable, ctx, inHead: true);
            if (term.tail != null) _generateStructureElement(term.tail!, varTable, ctx, inHead: true); }
        } else if (term is StructTerm) {
          final tempReg = ctx.allocateTemp();
          ctx.emit(bc.GetVariable(tempReg, argSlot));
          ctx.emit(bc.HeadStructure(term.functor, term.arity, tempReg));
          for (final subArg in term.args) {
            _generateStructureElement(subArg, varTable, ctx, inHead: true);
          }
        } else if (term is UnderscoreTerm) { /* no-op */ }
      }" — a five-way type-test cascade with two distinct novel
      nuances: (a) **first-occurrence-in-head** distinguishes v2
      `GetVariable` from v2 `GetValue` (first time we see the var ⇒
      bind via GetVariable, subsequent occurrence ⇒ re-unify via
      GetValue), (b) **structure-as-head-arg uses temp-extract** to
      avoid overlapping HeadStructure operations (the source comment
      "FIX: For structures as direct HEAD arguments, extract first then
      match" documents the bug fix).
    target_decision: >-
      `private void _GenerateHeadArgument(Term term, int argSlot,
      VariableTable varTable, CodeGenContext ctx)` direct 1:1 with the
      five-way cascade as a series of `is`-pattern branches. The
      VarTerm branch: `if (term is VarTerm vt) { var varInfo = varTable.
      GetVar(vt.Name); if (varInfo is null) throw new CompileError(
      $"Undefined variable: {vt.Name}", vt.Line, vt.Column, phase:
      "codegen"); int regIndex = varInfo.RegisterIndex!.Value; string
      baseVarName = vt.Name.EndsWith("?") ? vt.Name.Substring(0, vt.
      Name.Length - 1) : vt.Name; bool isFirstOccurrence = !ctx.
      SeenHeadVars.Contains(baseVarName); if (isFirstOccurrence) { ctx.
      Emit(new Bcv2.GetVariable(regIndex, argSlot, isReader: vt.
      IsReader)); ctx.SeenHeadVars.Add(baseVarName); } else { ctx.Emit
      (new Bcv2.GetValue(regIndex, argSlot, isReader: vt.IsReader)); } }`.
      The `term.name.endsWith('?')` ⇒ C# `vt.Name.EndsWith("?")`
      (Microsoft Learn `string.EndsWith`); `.substring(0, len-1)` ⇒
      `Substring(0, vt.Name.Length - 1)` — Microsoft Learn:
      `string.Substring(int startIndex, int length)` differs from Dart's
      `substring(int start, int? end)` (Dart's second arg is end-
      exclusive, C# `Substring`'s second arg is LENGTH). Since the Dart
      call is `substring(0, len-1)` and C# `Substring(0, len-1)` has
      length=`len-1` (which equals the Dart end-exclusive form), the
      mapping is IDENTICAL — coincidence works because start=0. Reuse
      rf-dart-substring-to-csharp-substring-with-length (cached from
      lexer.dart). Reuse rf-dart-string-endswith-to-csharp-string-
      endswith (cached from lexer.dart). The ConstTerm branch: `else if
      (term is ConstTerm ct) { ctx.Emit(new Bc.HeadConstant(ct.Value,
      argSlot)); }`. The ListTerm branch with `IsNil` check + `HeadNil`
      / `HeadStructure(".",2,...)` emit + head/tail recursion preserved
      verbatim. The StructTerm branch with `AllocateTemp` + GetVariable
      + HeadStructure + per-arg recursion preserved verbatim. The
      UnderscoreTerm branch is a NO-OP (no instruction emitted; the
      comment "Anonymous variable as direct head argument: just ignore
      it" is preserved as a C# comment). Reuse rf-dart-is-typetest-
      cascade-to-csharp-is-pattern-cascade (cached from runner.dart).
    idiom_id: null
    research_finding_id: rf-dart-is-typetest-cascade-to-csharp-is-pattern-cascade
    nuance: >-
      Five intertwined nuances. (1) **First-occurrence dispatch is
      LOAD-BEARING**: GLP v2 distinguishes `GetVariable` (binds the
      register to the arg cell) from `GetValue` (asserts equality
      between two already-known cells). The `seenHeadVars` set tracks
      base-var-name (with the `?` suffix stripped) so that the second
      occurrence emits GetValue, NOT a second GetVariable. C# port MUST
      preserve the strip-`?` step AND the set-tracking; collapsing to
      "always GetVariable" would silently break GLP's unification
      semantics. (2) **Reader suffix `?` stripping**: a Dart-side
      convention where `X?` denotes the "reader" form of variable `X`
      (paired writer is plain `X`); the strip recovers the BASE name
      so that first-occurrence is tracked across reader/writer pairs.
      The C# port preserves the strip — `EndsWith("?")` is exact-
      character not regex. (3) **Struct-as-head-arg temp extract**:
      a bug fix per the source comment "avoids overlapping HeadStructure
      operations" — the temp register holds the argument cell, then
      HeadStructure matches AGAINST THE TEMP, not against the arg slot.
      C# port MUST preserve the GetVariable-to-temp PLUS HeadStructure-
      on-temp pair; collapsing to a direct HeadStructure(argSlot) would
      regress the bug. (4) **UnderscoreTerm no-op**: anonymous variable
      `_` in head position emits NO instruction — the arg cell is
      simply not extracted. C# port preserves the empty branch (must
      still be present as a branch so the cascade is exhaustive). (5)
      **CompileError named arg `phase: 'codegen'`**: preserve verbatim
      per rf-dart-named-default-param-to-csharp-optional-arg (cached
      from error.dart).

  - construct_key: dart.method.generate_structure_element_dual_mode_head_vs_body_with_push_pop_save_register_for_nested_structures
    source_form: >-
      "void _generateStructureElement(Term term, VariableTable varTable,
        CodeGenContext ctx, {required bool inHead}) {
        if (term is VarTerm) { ... emit bcv2.UnifyVariable(regIndex, isReader: term.isReader); }
        else if (term is ConstTerm) { ctx.emit(bc.UnifyConstant(term.value)); }
        else if (term is ListTerm) {
          if (term.isNil) { ctx.emit(bc.UnifyConstant('nil')); }
          else {
            if (inHead) {
              final saveReg = ctx.allocateTemp();
              ctx.emit(bc.Push(saveReg));
              ctx.emit(bc.UnifyStructure('.', 2));
              ... recurse head/tail with inHead: true ...
              ctx.emit(bc.Pop(saveReg));
              ctx.emit(bcv2.UnifyVariable(saveReg, isReader: false));
            } else {
              final tempReg = ctx.allocateTemp();
              ctx.emit(bc.PutStructure('.', 2, tempReg));
              ... recurse head/tail with inHead: inHead (false) ...
              ctx.emit(bcv2.UnifyVariable(tempReg, isReader: false));
            }
          }
        } else if (term is StructTerm) {
          if (inHead) { /* Push/UnifyStructure/Pop, then UnifyVariable(saveReg) */ }
          else { /* PutStructure, recurse, UnifyVariable(tempReg) */ }
        } else if (term is UnderscoreTerm) { ctx.emit(bc.UnifyVoid(count: 1)); }
      }" — the dual-mode (HEAD/BODY) structure-element walker; the
      **Push/Pop save-register dance** is FCP AM design: save S-register,
      enter nested UnifyStructure (which itself sets S to the
      sub-structure's first arg), recurse to fill, restore S via Pop,
      then place the nested-structure-result at the parent's S via
      `UnifyVariable(saveReg, isReader: false)`. WRITE mode replaces
      Push/UnifyStructure/Pop with `PutStructure(...,tempReg)` because
      WRITE mode allocates a fresh cell rather than matching an
      existing one.
    target_decision: >-
      `private void _GenerateStructureElement(Term term, VariableTable
      varTable, CodeGenContext ctx, bool inHead)` direct 1:1 with the
      five-way `is`-pattern cascade. VarTerm: `if (term is VarTerm vt)
      { var varInfo = varTable.GetVar(vt.Name); if (varInfo is null)
      throw new CompileError($"Undefined variable: {vt.Name}", vt.Line,
      vt.Column, phase: "codegen"); ctx.Emit(new Bcv2.UnifyVariable(
      varInfo.RegisterIndex!.Value, isReader: vt.IsReader)); }`.
      ConstTerm: `else if (term is ConstTerm ct) { ctx.Emit(new Bc.
      UnifyConstant(ct.Value)); }`. ListTerm with the dual-mode
      Push/Pop / PutStructure branches preserved verbatim — the
      Push-UnifyStructure-recurse-Pop-UnifyVariable(saveReg) sequence
      and the PutStructure-recurse-UnifyVariable(tempReg) sequence are
      both verbatim-preserved (FCP AM design — source comment "FCP AM:
      After Pop, must place nested structure at S and increment"). The
      `isReader: false` on the final `UnifyVariable(saveReg,
      isReader: false)` / `UnifyVariable(tempReg, isReader: false)`
      reflects that the nested-structure cell is a WRITER (creating
      structure, not reading) — preserve verbatim. StructTerm same
      dual-mode shape preserved. UnderscoreTerm: `else if (term is
      UnderscoreTerm) { ctx.Emit(new Bc.UnifyVoid(count: 1)); }` — the
      Dart named arg `count: 1` becomes C# named arg `count: 1`
      (Microsoft Learn: named-argument syntax is identical). Reuse
      rf-dart-named-default-param-to-csharp-optional-arg.
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Four intertwined nuances. (1) **Push/Pop is the FCP AM S-register
      save/restore**: the WAM/FCP abstract machine uses a single
      S-register that tracks the "current structure-traversal cursor".
      Entering a nested UnifyStructure overwrites S with the nested
      structure's first-arg cursor; recursion fills the nested cells;
      Pop restores S to the parent structure's cursor. This is NOT a
      generic stack — it is specifically saving/restoring S. C# port
      MUST preserve the Push-UnifyStructure-Pop bracketing AND the
      explicit `UnifyVariable(saveReg, isReader: false)` after Pop —
      the source comment "FCP AM: After Pop, must place nested
      structure at S and increment" is a load-bearing comment. (2)
      **HEAD vs BODY dispatch is the WRITE/READ mode of the WAM**:
      HEAD = READ (match existing structure cells); BODY = WRITE
      (allocate new structure cells). UnifyStructure (HEAD) vs
      PutStructure (BODY) is the WAM-canonical pair. C# port MUST
      preserve the `inHead` parameter and the both-branch emit
      shapes — collapsing to one branch silently breaks one of HEAD
      or BODY. (3) **UnifyVariable(tempReg, isReader: false) in BODY
      mode**: same opcode, different meaning — in BODY the just-built
      structure cell at `tempReg` is appended into the parent's
      argument cells. The `isReader: false` is critical (WRITER
      semantics on the just-built structure cell). C# port preserves
      the named arg. (4) **`UnifyVoid(count: 1)` is the anonymous-
      variable structure-element**: opcodes.dart spec already nominated
      UnifyVoid with a `count` arg (allows compacting multiple void
      cells into one opcode); preserve the named-arg call shape.

  - construct_key: dart.method.generate_guard_with_special_case_dispatch_table_ground_known_no_readers_otherwise_groundequal_and_generic_fallback
    source_form: >-
      "void _generateGuard(Guard guard, VariableTable varTable, CodeGenContext ctx) {
        if (guard.predicate == 'ground' && guard.args.length == 1) { ... ctx.emit(bc.Ground(...)); return; }
        if (guard.predicate == 'known' && guard.args.length == 1) { ... ctx.emit(bc.Known(...)); return; }
        if (guard.predicate == 'no_readers' && guard.args.length == 1) { ... ctx.emit(bc.NoReaders(...)); return; }
        if (guard.predicate == 'otherwise' && guard.args.isEmpty) { ctx.emit(bc.Otherwise()); return; }
        if (guard.predicate == '=?=' && guard.args.length == 2) {
          ... emit bc.GroundEqual(leftInfo.registerIndex!, rightInfo.registerIndex!, negated: guard.negated); return; }
        for (int i = 0; i < guard.args.length; i++) {
          _generatePutArgument(guard.args[i], i, varTable, ctx); }
        ctx.emit(bc.Guard(guard.predicate, guard.args.length, negated: guard.negated));
      }" — a hand-coded predicate-name + arity dispatch table over five
      built-ins (ground/1, known/1, no_readers/1, otherwise/0, =?=/2),
      then a generic fallback that emits PutArg setup + Guard runtime
      call.
    target_decision: >-
      `private void _GenerateGuard(Guard guard, VariableTable varTable,
      CodeGenContext ctx)` preserves the cascade-of-special-cases shape
      verbatim. The Dart string-predicate-name dispatch (`guard.
      predicate == 'ground'`, etc.) becomes C# `if (guard.Predicate ==
      "ground" && guard.Args.Count == 1)` — Microsoft Learn: C# string
      equality `==` is value-equality. Each special case emits the
      matching opcode-with-`negated`-flag via positional ctor args
      (`new Bc.Ground(regIndex, negated: guard.Negated)` — the `negated`
      named arg is preserved per rf-dart-named-default-param-to-csharp-
      optional-arg). The `otherwise` case CANNOT be negated (source
      comment "enforced by analyzer") — preserve as a comment-only note.
      The `=?=` (GroundEqual) case takes TWO VarTerm-bound register
      indices — preserve the `leftArg is VarTerm leftVt && rightArg is
      VarTerm rightVt` pattern. The generic fallback loops `_GeneratePut
      Argument` then emits `new Bc.Guard(guard.Predicate, guard.Args.
      Count, negated: guard.Negated)`. Reuse the cached analyzer.dart
      "guard-name dispatch table" idiom (a string-keyed cascade over
      `guard.predicate`) — same idiom, different consumer. Reuse rf-
      dart-string-equality-to-csharp-string-equality (cached from lexer.
      dart).
    idiom_id: null
    research_finding_id: rf-dart-string-equality-to-csharp-string-equality
    nuance: >-
      Three intertwined nuances. (1) **Five special-case opcodes**:
      ground/1, known/1, no_readers/1, otherwise/0, =?=/2 — these are
      the GLP "primitive guards" that have dedicated opcodes (per
      opcodes.dart spec). Every OTHER guard predicate emits the
      generic `Guard(name, arity, negated)` runtime-dispatched form.
      The five-case fast-path is a deliberate optimisation — preserve
      verbatim, do NOT collapse to a hash-map lookup (the early-return
      cascade is faster for the common case and is the documented
      structure). (2) **`negated` flag on every opcode**: the
      `negated: guard.negated` named arg threads through every guard
      opcode emit (except `Otherwise` which is documented as non-
      negatable). Preserve named-arg calls. (3) **`=?=` (GroundEqual)
      requires both args to be VarTerms bound to register slots**: if
      either arg is not a VarTerm OR either varInfo lookup fails, the
      special-case branch falls through to the generic Guard emission
      (no error — silent fallback). Preserve the silent fallback
      semantics — the analyzer.dart pass has already validated that
      `=?=` guards have VarTerm args, so the fallback is unreachable
      in well-formed programs; preserving it ensures defensive parity.

  - construct_key: dart.method.generate_body_with_remote_goal_and_spawn_goal_special_cases_and_default_spawn_emit_and_terminal_proceed
    source_form: >-
      "void _generateBody(List<Goal> goals, VariableTable varTable, CodeGenContext ctx) {
        for (int i = 0; i < goals.length; i++) {
          final goal = goals[i];
          if (goal is RemoteGoal) { _generateRemoteGoal(goal, varTable, ctx); continue; }
          if (goal is SpawnGoal) { /* @AgentId ignored in dGLP; setup args + Spawn */ continue; }
          for (int j = 0; j < goal.args.length; j++) {
            _generatePutArgument(goal.args[j], j, varTable, ctx); }
          final procedureLabel = '${goal.functor}/${goal.arity}';
          ctx.emit(bc.Spawn(procedureLabel, goal.arity));
        }
        ctx.emit(bc.Proceed());
      }" — body emission: for each goal, either (a) RemoteGoal RPC
      dispatch, (b) SpawnGoal with ignored @AgentId, or (c) the default
      Spawn-after-args; then a terminal Proceed.
    target_decision: >-
      `private void _GenerateBody(IReadOnlyList<Goal> goals, VariableTable
      varTable, CodeGenContext ctx)` direct 1:1. The `for (int i = 0;
      i < goals.length; i++)` loop preserves the indexed access (the
      source uses indexed access not `foreach` — preserve to match the
      Dart source structurally). The three-way dispatch becomes
      `if (goal is RemoteGoal rg) { _GenerateRemoteGoal(rg, varTable,
      ctx); continue; } if (goal is SpawnGoal sg) { var innerGoal = sg.
      InnerGoal; for (int j = 0; j < innerGoal.Args.Count; j++) {
      _GeneratePutArgument(innerGoal.Args[j], j, varTable, ctx); } var
      procedureLabel = $"{innerGoal.Functor}/{innerGoal.Arity}"; ctx.
      Emit(new Bc.Spawn(procedureLabel, innerGoal.Arity)); continue; }`.
      The default path emits per-arg PutArgument then Spawn — preserved
      verbatim. Terminal Proceed is preserved. Reuse rf-dart-is-typetest-
      cascade-to-csharp-is-pattern-cascade (cached). Reuse rf-dart-
      string-interpolation-to-csharp-string-interpolation (cached from
      glp_printer.dart).
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-to-csharp-string-interpolation
    nuance: >-
      Four intertwined nuances. (1) **All-goals-spawn (no tail-call
      optimisation)**: the source comment "ALWAYS spawn (tail recursion
      removed - all goals spawned)" is load-bearing — every goal,
      INCLUDING the last, is spawned (never inlined). The C# port
      preserves the all-spawn semantics — do NOT optimise the last goal
      to a jump. (2) **SpawnGoal @AgentId is intentionally dropped in
      dGLP mode**: the source comment "In dGLP mode, ignore the
      @AgentId annotation and just run the inner goal" — the agent
      annotation is metadata for the distributed-GLP runtime but the
      current bytecode emitter is dGLP-naive. Preserve the drop
      verbatim; do NOT propagate the agent identity. (3) **Terminal
      Proceed marks end-of-clause**: every body emission terminates
      with Proceed (clause complete, return to caller queue). Preserve
      the unconditional terminal Proceed — even if the last goal is a
      RemoteGoal or SpawnGoal that already-spawned, the Proceed is
      still needed to terminate the PARENT goal's clause. (4)
      **Procedure label format `"functor/arity"`**: the slash-separator
      label format is the GLP convention (per analyzer.dart spec's
      AnnotatedProcedure.Signature). Preserve the exact format —
      mismatch would silently break label resolution in the runner.

  - construct_key: dart.method.generate_remote_goal_with_static_distribute_or_dynamic_transmit_dispatch_and_import_table_indexing
    source_form: >-
      "void _generateRemoteGoal(RemoteGoal remote, VariableTable varTable, CodeGenContext ctx) {
        final innerGoal = remote.goal;
        for (int j = 0; j < innerGoal.args.length; j++) {
          _generatePutArgument(innerGoal.args[j], j, varTable, ctx); }
        if (remote.isDynamic) {
          final moduleTerm = remote.module as VarTerm;
          final varInfo = varTable.getVar(moduleTerm.name);
          if (varInfo == null || varInfo.registerIndex == null) throw CompileError(...);
          ctx.emit(bc.Transmit(varInfo.registerIndex!, innerGoal.functor, innerGoal.arity));
        } else {
          final moduleName = remote.staticModuleName!;
          final index = ctx.importTable.addImport(moduleName);
          ctx.emit(bc.Distribute(index, innerGoal.functor, innerGoal.arity));
        }
      }" — RPC emission: static module ⇒ Distribute(index, ...) with
      import-table 1-indexing; dynamic module ⇒ Transmit(regIndex, ...)
      with runtime module-cell lookup. FCP rpc.cp:164-175 cited.
    target_decision: >-
      `private void _GenerateRemoteGoal(RemoteGoal remote, VariableTable
      varTable, CodeGenContext ctx)` direct 1:1. The `remote.module as
      VarTerm` Dart cast becomes C# `(VarTerm)remote.Module` — but the
      safer C# idiom is `remote.Module is VarTerm moduleTerm` (pattern-
      matched cast); reuse rf-dart-cast-as-to-csharp-pattern-cast
      (cached from analyzer.dart). The null-or-no-register guard
      becomes `if (varInfo is null || varInfo.RegisterIndex is null)
      throw new CompileError($"Unknown variable in dynamic RPC:
      {moduleTerm.Name}", remote.Line, remote.Column, phase:
      "codegen");`. Static branch: `string moduleName = remote.
      StaticModuleName!; int index = ctx.ImportTable.AddImport(moduleName);
      ctx.Emit(new Bc.Distribute(index, innerGoal.Functor, innerGoal.
      Arity));`. The Dart `staticModuleName!` bang ⇒ C# `StaticModuleName
      ?? throw new InvalidOperationException("staticModuleName must be
      non-null when isDynamic is false")` — OR preserve the bang
      semantics via the C# NRT `!` postfix (Microsoft Learn: null-
      forgiving operator) when the analyzer.dart pass has already
      established non-null. Spec records the NRT postfix `!` form per
      rf-dart-bang-to-csharp-null-forgiving-operator (cached).
    idiom_id: null
    research_finding_id: rf-dart-cast-as-to-csharp-pattern-cast
    nuance: >-
      Three intertwined nuances. (1) **`isDynamic` discriminates the
      two RPC opcodes**: Distribute (static, takes a 1-based import-
      table index) vs Transmit (dynamic, takes a runtime register
      holding a module-cell). The C# port preserves the discriminator
      and the two distinct opcodes. (2) **Import table 1-indexed slot
      reuse**: `ctx.importTable.addImport(moduleName)` returns the
      stable 1-based slot index, ADDING the module if not present.
      Preserve the side-effect-on-lookup semantics (a static-module
      reference auto-registers the module). The C# port preserves the
      mutating-lookup idiom. (3) **`module as VarTerm` is unchecked**:
      the Dart `as` cast throws `_TypeError` on mismatch; the C# `is`
      pattern is safer (no throw, just false). The spec records the
      `is` pattern + explicit throw — semantically equivalent on the
      well-formed-input happy path, more defensible on malformed input.
      Reuse rf-dart-cast-as-to-csharp-pattern-cast.

  - construct_key: dart.method.generate_put_argument_five_way_cascade_with_put_bound_const_put_bound_nil_put_structure_and_underscore_fresh_writer
    source_form: >-
      "void _generatePutArgument(Term term, int argSlot, VariableTable varTable, CodeGenContext ctx) {
        if (term is VarTerm) { ... ctx.emit(bcv2.PutVariable(regIndex, argSlot, isReader: term.isReader)); }
        else if (term is ConstTerm) { ctx.emit(bc.PutBoundConst(term.value, argSlot)); }
        else if (term is ListTerm) {
          if (term.isNil) { ctx.emit(bc.PutBoundNil(argSlot)); }
          else { ctx.emit(bc.PutStructure('.', 2, argSlot));
            if (term.head != null) _generateArgumentStructureElement(term.head!, varTable, ctx);
            if (term.tail != null) _generateArgumentStructureElement(term.tail!, varTable, ctx); }
        } else if (term is StructTerm) {
          ctx.emit(bc.PutStructure(term.functor, term.arity, argSlot));
          for (final arg in term.args) {
            _generateArgumentStructureElement(arg, varTable, ctx); }
        } else if (term is UnderscoreTerm) {
          final tempReg = ctx.allocateTemp();
          ctx.emit(bcv2.PutVariable(tempReg, argSlot, isReader: false));
        }
      }" — five-way cascade for BODY arg-setup (WRITE mode):
      PutVariable (named var), PutBoundConst (constant), PutBoundNil
      (empty list), PutStructure + recurse (non-empty list or struct),
      and **fresh-writer-for-underscore** (anonymous `_` gets a unique
      temp register).
    target_decision: >-
      `private void _GeneratePutArgument(Term term, int argSlot,
      VariableTable varTable, CodeGenContext ctx)` direct 1:1 with the
      five-way `is`-pattern cascade. All branches preserved verbatim —
      reuse the same idioms as `_GenerateHeadArgument` (VarTerm shape,
      ConstTerm shape, ListTerm.IsNil branching, StructTerm
      iteration). The UnderscoreTerm branch is **distinctly different**
      from the head-arg case (which is a no-op): here, an anonymous
      `_` in body must create a fresh unbound writer at a unique temp
      register. C# port: `else if (term is UnderscoreTerm) { int
      tempReg = ctx.AllocateTemp(); ctx.Emit(new Bcv2.PutVariable(
      tempReg, argSlot, isReader: false)); }`. Reuse rf-dart-is-
      typetest-cascade-to-csharp-is-pattern-cascade (cached).
    idiom_id: null
    research_finding_id: rf-dart-is-typetest-cascade-to-csharp-is-pattern-cascade
    nuance: >-
      Three intertwined nuances. (1) **HEAD vs BODY underscore
      asymmetry**: HEAD anonymous `_` is a no-op (no extraction
      needed); BODY anonymous `_` is a FRESH UNBOUND WRITER (a new
      cell that nobody reads). The two emit shapes MUST differ — the
      preserve-working-code rule applies twice here. (2) **PutBoundConst
      / PutBoundNil are BOUND-WRITER constants**: they create a writer-
      cell already bound to the constant value (in contrast to
      PutVariable which creates an unbound writer that will be bound by
      a downstream peer). The naming is load-bearing — `PutBoundConst`
      is NOT a typo for `PutConst`. (3) **PutStructure(., 2, argSlot)**
      vs **PutStructure(functor, arity, argSlot)**: the dot-functor
      `'.'` is the GLP cons-cell convention (a list `[H|T]` IS
      `'.'(H, T)`). C# port preserves the literal `"."` string —
      changing to `"cons"` or similar silently breaks list matching.

  - construct_key: dart.method.is_ground_term_predicate_walking_var_const_list_struct_with_short_circuit_and_const_term_to_runtime_value_with_nested_list_struct_decoding
    source_form: >-
      "bool _isGroundTerm(Term term) {
        if (term is VarTerm) return false;
        if (term is ConstTerm) return true;
        if (term is ListTerm) { if (term.isNil) return true;
          return (term.head == null || _isGroundTerm(term.head!)) &&
                 (term.tail == null || _isGroundTerm(term.tail!)); }
        if (term is StructTerm) return term.args.every((arg) => _isGroundTerm(arg));
        return false;
      }
      Object? _groundTermToValue(Term term) {
        if (term is ConstTerm) return term.value;
        if (term is ListTerm) { if (term.isNil) return 'nil';
          final head = term.head != null ? _groundTermToValue(term.head!) : null;
          final tail = term.tail != null ? _groundTermToValue(term.tail!) : null;
          return [head, tail]; }
        if (term is StructTerm) { return {'functor': term.functor,
          'args': term.args.map((arg) => _groundTermToValue(arg)).toList()}; }
        return null;
      }" — two predicate/transformer pairs walking the AST. `_isGroundTerm`
      returns true iff term contains no variables (recursive over list
      cells and struct args). `_groundTermToValue` lowers a ground term
      to a Dart-side value tree (using a 2-element list `[head, tail]`
      for cons cells and a `{'functor', 'args'}` map for structures —
      effectively a JSON-shaped representation; NOT the runtime
      `rt.StructTerm` form used by the sibling local helper inside
      `_generateArgumentStructureElement` — distinct purposes).
    target_decision: >-
      `private bool _IsGroundTerm(Term term)` direct 1:1 — preserve the
      cascade-of-`is`-tests with short-circuit `&&` semantics. C#
      `term.Args.All(arg => _IsGroundTerm(arg))` for the StructTerm
      branch — Microsoft Learn: `Enumerable.All` is short-circuit on
      false. Reuse rf-dart-every-to-csharp-all (cached from analyzer.
      dart). `private object? _GroundTermToValue(Term term)` direct 1:1
      — the ConstTerm branch returns `ct.Value` (which is `object?`);
      the ListTerm branch returns `new List<object?> { head, tail }`
      (2-element list — Microsoft Learn: list initialiser); the
      StructTerm branch returns `new Dictionary<string, object?> {
      ["functor"] = st.Functor, ["args"] = st.Args.Select(a =>
      _GroundTermToValue(a)).ToList() }` — Microsoft Learn dictionary
      initialiser. The `_groundTermToValue` method is INTERNAL-USE
      ONLY (never called externally) — it appears DEAD in the current
      source (no `_groundTermToValue` call sites in the file). Preserve
      per the preserve-working-code discipline; mark as `// Currently
      unused — preserved per CLAUDE.md preserve-working-code` comment.
      Reuse rf-dart-map-literal-to-csharp-dictionary-initialiser
      (cached from analyzer.dart).
    idiom_id: null
    research_finding_id: rf-dart-map-literal-to-csharp-dictionary-initialiser
    nuance: >-
      Four intertwined nuances. (1) **`_groundTermToValue` is DEAD CODE
      with PARITY value**: no call site in the file; appears to have
      been a sibling helper for an earlier emission path that has since
      been replaced by the runtime-side `convertListToStructTerm` local
      function inside `_generateArgumentStructureElement`. Preserve
      verbatim per CLAUDE.md "Preserve Working Code — NEVER remove
      without explicit approval". (2) **Two-element-list cons cell
      representation**: `[head, tail]` is a Dart `List<dynamic>` shape;
      C# `List<object?>` preserves it. NOT a `Pair<T,U>` or `ValueTuple`
      — the source uses indexed access (implicitly) so list-of-2 is
      correct. (3) **Dictionary representation for struct**:
      `{'functor': ..., 'args': [...]}` becomes `Dictionary<string,
      object?>` — preserve the string keys verbatim. (4) **`.every`**
      is Dart `Iterable.every` (short-circuit on false) — C# `.All`
      (LINQ) has identical short-circuit semantics. Microsoft Learn:
      "All<TSource>(this IEnumerable<TSource> source, Func<TSource,
      bool> predicate) — Determines whether all elements of a sequence
      satisfy a condition" and returns false on first false predicate.

  - construct_key: dart.method.generate_argument_structure_element_three_phase_walk_with_ground_list_to_constant_lowering_and_nested_local_function_recursion
    source_form: >-
      "void _generateArgumentStructureElement(Term term, VariableTable varTable, CodeGenContext ctx) {
        if (term is VarTerm) { ... ctx.emit(bcv2.UnifyVariable(regIndex, isReader: term.isReader)); }
        else if (term is ConstTerm) { ctx.emit(bc.UnifyConstant(term.value)); }
        else if (term is ListTerm) {
          if (term.isNil) { ctx.emit(bc.UnifyConstant('nil')); }
          else {
            if (_isGroundTerm(term)) {
              rt.Term convertListToStructTerm(ListTerm l) {
                if (l.isNil) return rt.ConstTerm('nil');
                rt.Term convertTerm(Term t) { if (t is ConstTerm) ...; if (t is ListTerm) ...; if (t is StructTerm) ...; }
                final head = l.head != null ? convertTerm(l.head!) : rt.ConstTerm('nil');
                final tail = l.tail != null ? convertTerm(l.tail!) : rt.ConstTerm('nil');
                return rt.StructTerm('.', [head, tail]);
              }
              final listStructTerm = convertListToStructTerm(term);
              ctx.emit(bc.UnifyConstant(listStructTerm));
            } else { /* PutStructure('.', 2, -1) + body recursion */ }
          }
        } else if (term is StructTerm) { /* PutStructure(functor, arity, -1) + body recursion */ }
        else if (term is UnderscoreTerm) { ctx.emit(bc.UnifyVoid(count: 1)); }
      }" — argument-structure element walker (called from
      `_generatePutArgument` when emitting structure args). Two
      branches in the ListTerm case: ground list ⇒ lower to a runtime
      `rt.StructTerm('.', [head, tail])` tree and emit as a single
      UnifyConstant; non-ground list ⇒ build via PutStructure('.', 2,
      -1) (the `-1` argSlot is a sentinel meaning "building inside
      parent structure"). The ground-list lowering uses a **nested
      local function** `convertListToStructTerm` with its own nested
      local `convertTerm`.
    target_decision: >-
      `private void _GenerateArgumentStructureElement(Term term,
      VariableTable varTable, CodeGenContext ctx)` direct 1:1. The
      VarTerm / ConstTerm / UnderscoreTerm branches reuse the cached
      shapes. The ListTerm ground-list branch uses C# local functions
      (Microsoft Learn) for `ConvertListToStructTerm` and
      `ConvertTerm`. `static Rt.Term ConvertListToStructTerm(ListTerm
      l) { if (l.IsNil) return new Rt.ConstTerm("nil"); Rt.Term
      ConvertTerm(Term t) { if (t is ConstTerm ct) return new Rt.
      ConstTerm(ct.Value); if (t is ListTerm lt) return
      ConvertListToStructTerm(lt); if (t is StructTerm st) { var
      rtArgs = st.Args.Select(ConvertTerm).ToList(); return new Rt.
      StructTerm(st.Functor, rtArgs); } return new Rt.ConstTerm(null);
      } var head = l.Head is not null ? ConvertTerm(l.Head) : new Rt.
      ConstTerm("nil"); var tail = l.Tail is not null ?
      ConvertTerm(l.Tail) : new Rt.ConstTerm("nil"); return new Rt.
      StructTerm(".", new List<Rt.Term> { head, tail }); }`. Reuse rf-
      dart-local-function-to-csharp-local-function (cached from
      partial_evaluator.dart). The PutStructure(`.`, 2, -1) sentinel
      preserved: C# `ctx.Emit(new Bc.PutStructure(".", 2, -1));`.
      The `-1` argSlot sentinel is opcodes.dart-spec-documented as
      "building inside parent structure" — preserve verbatim. Reuse
      rf-dart-sentinel-magic-int-to-csharp-sentinel-magic-int (cached).
      The non-ground list recurses via `_generateStructureElementInBody`
      / `_generateListTailInBody`. The StructTerm branch: ground OR
      non-ground both use PutStructure-then-recurse via
      `_generateStructureElementInBody` (the StructTerm branch does NOT
      have a ground-shortcut — unlike the ListTerm branch — preserve
      the asymmetry; the source-side reason is that struct args may
      include variables more often than list elements in practice).
    idiom_id: null
    research_finding_id: rf-dart-local-function-to-csharp-local-function
    nuance: >-
      Five intertwined nuances. (1) **Ground-list shortcut emits a
      SINGLE UnifyConstant**: lowering the entire ground list to a
      runtime-side `rt.StructTerm` tree and emitting it as one constant
      opcode is a deliberate optimisation — the runner can install the
      constant in one step, vs traversing N PutStructure cells for an
      N-element list. C# port preserves the shortcut. (2) **Local
      function captures NOTHING**: both `convertListToStructTerm` and
      `convertTerm` are pure functions of their args — no `this`
      capture, no `ctx` capture. C# port emits them as `static` local
      functions (Microsoft Learn: "Static local functions can't
      capture local variables or instance state"). (3) **`-1` argSlot
      sentinel**: the PutStructure opcode's third arg is conventionally
      the destination register slot; `-1` is the documented sentinel
      for "the current structure-traversal cursor (S register), not a
      named slot". Preserve verbatim per opcodes.dart spec. (4)
      **`rt.StructTerm('.', [head, tail])` is the RUNTIME-SIDE Term
      tree**: distinct from the COMPILE-SIDE `StructTerm` (no prefix).
      The two type families MUST not be conflated — the runner expects
      `rt.Term` (runtime) in its instruction operands. C# port uses the
      `Rt.*` alias namespace per the import-prefix specification at the
      top of this artifact. (5) **`return new Rt.ConstTerm(null)`
      fallback**: the source comment "Fallback for unexpected cases" —
      this branch is unreachable in well-formed input but is a
      defensive catch-all. Preserve verbatim.

  - construct_key: dart.method.generate_structure_element_in_body_and_generate_list_tail_in_body_recursive_pair_with_set_variable_set_constant_emit_and_anonymous_fresh_writer
    source_form: >-
      "void _generateStructureElementInBody(Term term, VariableTable varTable, CodeGenContext ctx) {
        if (term is VarTerm) { ... ctx.emit(bcv2.SetVariable(regIndex, isReader: term.isReader)); }
        else if (term is ConstTerm) { ctx.emit(bc.SetConstant(term.value)); }
        else if (term is ListTerm) {
          if (term.isNil) { ctx.emit(bc.SetConstant('nil')); }
          else { ctx.emit(bc.PutStructure('.', 2, -1));
            if (term.head != null) _generateStructureElementInBody(term.head!, varTable, ctx);
            if (term.tail != null) _generateListTailInBody(term.tail!, varTable, ctx); }
        } else if (term is StructTerm) {
          ctx.emit(bc.PutStructure(term.functor, term.arity, -1));
          for (final arg in term.args) {
            _generateStructureElementInBody(arg, varTable, ctx); }
        } else if (term is UnderscoreTerm) {
          final tempReg = ctx.allocateTemp();
          ctx.emit(bcv2.SetVariable(tempReg, isReader: false));
        }
      }
      void _generateListTailInBody(Term term, VariableTable varTable, CodeGenContext ctx) {
        if (term is ListTerm) {
          if (term.isNil) { ctx.emit(bc.SetConstant('nil')); }
          else { ctx.emit(bc.PutStructure('.', 2, -1));
            if (term.head != null) _generateStructureElementInBody(term.head!, varTable, ctx);
            if (term.tail != null) _generateListTailInBody(term.tail!, varTable, ctx); }
        } else { _generateStructureElementInBody(term, varTable, ctx); }
      }" — the BODY-only structure-element / list-tail walkers. The
      pair is mutually recursive (list tail → element → list tail).
      Three opcodes appear ONLY here: `bcv2.SetVariable` (v2 unified
      set instruction with `isReader` flag), `bc.SetConstant` (v1 set
      constant), and the `-1`-argSlot variant of PutStructure.
    target_decision: >-
      `private void _GenerateStructureElementInBody(Term term,
      VariableTable varTable, CodeGenContext ctx)` and
      `private void _GenerateListTailInBody(Term term, VariableTable
      varTable, CodeGenContext ctx)` are direct 1:1 ports with the
      five-way / two-way `is`-pattern cascades. VarTerm: `ctx.Emit(new
      Bcv2.SetVariable(varInfo.RegisterIndex!.Value, isReader: vt.
      IsReader));`. ConstTerm: `ctx.Emit(new Bc.SetConstant(ct.
      Value));`. ListTerm.IsNil ⇒ `SetConstant("nil")`. Non-empty list
      ⇒ `PutStructure(".", 2, -1)` + recurse head into
      `_GenerateStructureElementInBody` + recurse tail into
      `_GenerateListTailInBody` (the asymmetric head/tail recursion is
      load-bearing — preserve verbatim). StructTerm ⇒ `PutStructure(
      st.Functor, st.Arity, -1)` + per-arg recurse. UnderscoreTerm ⇒
      `AllocateTemp` + `SetVariable(tempReg, isReader: false)`. The
      `_GenerateListTailInBody` cascade: if the tail is a ListTerm
      recurse with list-tail semantics; otherwise delegate to the
      general-element form. Reuse rf-dart-mutually-recursive-methods-
      to-csharp-mutually-recursive-methods (cached from partial_
      evaluator.dart family).
    idiom_id: null
    research_finding_id: rf-dart-mutually-recursive-methods-to-csharp-mutually-recursive-methods
    nuance: >-
      Four intertwined nuances. (1) **Set vs Unify family**:
      `SetVariable` / `SetConstant` are the BODY-WRITE counterpart to
      HEAD's `UnifyVariable` / `UnifyConstant`. The opcode family naming
      is load-bearing — the runner's HEAD-vs-BODY dispatch reads the
      opcode kind. Do NOT collapse the two families to one. (2)
      **`-1` argSlot is the inside-parent-structure sentinel**: same
      sentinel as in `_generateArgumentStructureElement`. PutStructure
      with `-1` means "S-cursor-relative, not slot-absolute". Preserve.
      (3) **Mutual recursion list/tail**: a list `[a, b, c]` is
      structurally `.(a, .(b, .(c, nil)))` — the recursive descent
      MUST flip between general-element (the head) and list-tail
      (the tail-or-nil) at each cons cell. Collapsing to one helper
      breaks the asymmetric-recursion pattern. (4) **UnderscoreTerm
      gets a fresh temp writer**: same as in `_generatePutArgument` —
      anonymous variable in BODY position MUST allocate a fresh unbound
      writer. Preserve `AllocateTemp` + `SetVariable(tempReg, isReader:
      false)`.

  - construct_key: dart.exception_throw.compile_error_with_phase_codegen_named_arg
    source_form: >-
      "throw CompileError('Undefined variable: ${term.name}', term.line,
      term.column, phase: 'codegen');" — appears six times across the
      file (head-arg VarTerm, structure-element VarTerm, arg-structure
      VarTerm, body-structure VarTerm, dynamic-RPC module-var, and one
      more). Standardised error-message shape with the `phase: 'codegen'`
      named arg.
    target_decision: >-
      Emit `throw new CompileError($"Undefined variable: {vt.Name}",
      vt.Line, vt.Column, phase: "codegen");` at every occurrence. The
      C# named arg `phase:` is preserved per rf-dart-named-default-
      param-to-csharp-optional-arg (cached from error.dart). The
      `'Undefined variable: ${term.name}'` interpolation ⇒
      `$"Undefined variable: {vt.Name}"` per the cached interpolation
      idiom. Reuse the error.dart-cached
      rf-dart-implements-exception-to-csharp-derive-system-exception
      for the CompileError type itself.
    idiom_id: null
    research_finding_id: rf-dart-implements-exception-to-csharp-derive-system-exception
    nuance: >-
      Two nuances. (1) **`phase: 'codegen'` is the THIRD codegen phase
      tag** (after `'parser'` from parser.dart and `'analyzer'` from
      analyzer.dart). Preserve the exact string — downstream error-
      classification (per the unified-REPL spec) keys off the phase
      string. (2) **Six callsites — preserve all six**: do NOT collapse
      into a shared helper method. The source-side message
      differentiation is by literal — e.g. "Undefined variable:" vs
      "Unknown variable in dynamic RPC:" — preserving call-site
      distinction makes the error trace precise.

conversion_units:
  - lib/compiler/codegen.cs::ImportTable                            # the 1-indexed module-name table
  - lib/compiler/codegen.cs::CodeGenContext                         # the per-invocation mutable emit state
  - lib/compiler/codegen.cs::CodeGenerator                          # the stateless dispatcher
  - lib/compiler/codegen.cs::CodeGenerator._GenerateProcedure       # entry-label + clause walk + NoMoreClauses + debug fence
  - lib/compiler/codegen.cs::CodeGenerator._GenerateClause          # ClauseTry / HEAD / GUARDS / Commit / BODY / Proceed
  - lib/compiler/codegen.cs::CodeGenerator._GenerateHead            # head-args loop
  - lib/compiler/codegen.cs::CodeGenerator._GenerateHeadArgument    # five-way cascade with first-occurrence v2 dispatch
  - lib/compiler/codegen.cs::CodeGenerator._GenerateStructureElement # dual-mode HEAD/BODY walker with Push/Pop save-register
  - lib/compiler/codegen.cs::CodeGenerator._GenerateGuard           # five-special-case + generic-fallback dispatch
  - lib/compiler/codegen.cs::CodeGenerator._GenerateBody            # RemoteGoal / SpawnGoal / default Spawn + Proceed
  - lib/compiler/codegen.cs::CodeGenerator._GenerateRemoteGoal      # static Distribute / dynamic Transmit
  - lib/compiler/codegen.cs::CodeGenerator._GeneratePutArgument     # BODY arg-setup five-way cascade
  - lib/compiler/codegen.cs::CodeGenerator._IsGroundTerm            # short-circuit ground predicate
  - lib/compiler/codegen.cs::CodeGenerator._GroundTermToValue       # currently-unused JSON-shaped lowering (preserved)
  - lib/compiler/codegen.cs::CodeGenerator._GenerateArgumentStructureElement # ground-list shortcut + non-ground PutStructure
  - lib/compiler/codegen.cs::CodeGenerator._GenerateStructureElementInBody   # BODY SetVariable/SetConstant family
  - lib/compiler/codegen.cs::CodeGenerator._GenerateListTailInBody  # list-tail dispatcher (mutually recursive)

escalations: []
```

## B. Embedded human-readable rationale + provenance

### `ImportTable` — FCP 1-indexed RPC module slot table

**Why**: FCP's Distribute opcode consumes a 1-based slot index that names
the imported module; the table is the single source of truth for slot
assignment AND the export order at module-link time (the runner
materialises the imports vector by reading `OrderedImports` in slot-index
order). Microsoft Learn `Dictionary<TKey,TValue>` is the canonical
constant-time keyed-lookup container; `HashSet<T>` would lose the
slot-index payload; `SortedDictionary<TKey,TValue>` would sort by key
(module name) NOT by slot index, so the explicit `OrderBy(kv => kv.Value)`
in `OrderedImports` is the load-bearing piece that pre-empts any future
container substitution.

### `CodeGenContext` — heterogeneous instruction list (`object` not `dynamic`)

**Why**: The Dart `List<dynamic>` is the ONLY way to store v1
positional-arg opcodes (`bc.PutStructure(functor, arity, slot)`) and v2
named-arg opcodes (`bcv2.PutVariable(reg, slot, isReader: ...)`) in the
same list without an artificial common interface. The runner.dart spec
already nominated `object?` for the parallel "cell content" decision and
type-test dispatch at consumption. C# `dynamic` (Microsoft Learn: "the
dynamic type")) does late-bound DLR dispatch — a feature this file does
NOT need; the right mapping is `object` plus pattern-match at the
runner-side `is`-test. The `nextTempVar = 10` base and the `> 10 ?
variableCount : 10` clamp encode a load-bearing arithmetic invariant:
named-variable registers occupy `0..N-1` where `N = varTable.size`;
temps must start at `max(N, 10)` to avoid aliasing. Preserve.

### `CodeGenerator.Generate` / `GenerateWithMetadata` — two-method public surface

**Why**: The REPL consumes the `variableMap` (returned by
`GenerateWithMetadata`) to map goal variables back to their register
slots (the user types `merge(L1, L2, Xs).` and expects to see the binding
of `Xs` printed after the goal succeeds; the register slot of `Xs` is
the bridge). The strict-bytecode shape (`Generate`) is the production-
runner entry. Both are published — do not collapse to one with an
optional out-param; the two-method shape is the API contract.

### `_GenerateProcedure` debug fence — `'foo/1'` magic-string

**Why**: Preserve verbatim per the CLAUDE.md "Preserve Working Code"
discipline. The fence is a runtime-guarded `print()` block keyed on a
probe-procedure signature used by Gabi during emitter development; the
production code path never enters the branch because no production
procedure has signature `foo/1`. Removing the fence breaks the
development workflow even though no test exercises it.

### `_GenerateClause` — three-phase invariant (HEAD → GUARDS → BODY)

**Why**: GLP's three-phase execution model (CLAUDE.md "GLP Quick
Reference") is encoded at the bytecode level by the ClauseTry / Commit
framing. Phase order MUST be preserved — the runner dispatches HEAD
unification, then guard tests, then BODY mutations, in that order, off
the opcode sequence. The `true/0` fast-path (length-1 body with functor
`"true"` and arity 0) emits a bare Proceed instead of spawning a goal
— preserving this avoids consuming a goal-queue slot for the always-
succeed marker (which appears in every fact-clause's source after the
analyzer's reduce-clause generation pass).

### First-occurrence-in-head dispatch (`SeenHeadVars`)

**Why**: GLP v2 distinguishes `GetVariable` (binds the register to the
arg cell) from `GetValue` (asserts equality between two already-known
cells); the choice depends on whether THIS occurrence of the variable
is the first or a subsequent one in the head. The `SeenHeadVars` set
tracks the base-var-name (after stripping the `?` reader-suffix) so
that the SECOND occurrence of `X` (or `X?`) in the same head emits
GetValue. Microsoft Learn: `HashSet<T>` is the canonical insertion-
cheap, contains-cheap, unordered-set implementation — direct fit.

### Push/Pop save-register dance — FCP AM design

**Why**: The WAM / FCP abstract machine uses a single S-register that
tracks the current structure-traversal cursor. Entering a nested
UnifyStructure overwrites S; recursion fills nested cells; Pop restores
S; the explicit `UnifyVariable(saveReg, isReader: false)` after Pop
"place[s] [the] nested structure at S and increment[s]" (source
comment, load-bearing). The C# port MUST preserve the Push-
UnifyStructure-Pop bracketing AND the explicit `UnifyVariable`-after-Pop
— omitting either silently breaks nested-structure HEAD matching.
Microsoft Learn does NOT directly speak to this — it is a WAM
implementation invariant, established by the WAM paper and preserved
verbatim from the Dart source.

### Static `Distribute` / dynamic `Transmit` RPC dispatch

**Why**: The source cites "FCP RPC transformation (rpc.cp:164-175)" —
the static-module form (`Module # Goal` where `Module` is a known
atom) emits Distribute(table-index, ...) consuming a static slot; the
dynamic-module form (where `Module` is a runtime-bound variable) emits
Transmit(register-index, ...) which the runner resolves at goal-spawn
time by reading the module-cell. Microsoft Learn does NOT speak to
this — it is a domain-specific opcode pair documented by opcodes.dart
spec and the FCP rpc.cp citation. Preserve the discriminator and the
two opcodes verbatim.

### Underscore (anonymous variable) — HEAD vs BODY asymmetry

**Why**: HEAD position: anonymous `_` is a no-op (the arg cell is
simply not extracted into any register — the unifier still succeeds
because matching anything against an unbound writer succeeds). BODY
position: anonymous `_` is a FRESH UNBOUND WRITER (a new cell that
nobody reads — `_AllocateTemp` + `SetVariable(tempReg, isReader:
false)` or `PutVariable(tempReg, argSlot, isReader: false)`). The
two emit shapes MUST differ — collapsing would silently break the SRSW
invariant that "an anonymous `_` is a writer that nobody reads"
(CLAUDE.md "GLP Quick Reference").

### Ground-list shortcut — single-opcode constant emission

**Why**: A fully ground list `[1, 2, 3]` can be lowered to a single
`UnifyConstant(rt.StructTerm('.', [...]))` opcode rather than the
N-cell `PutStructure(".", 2, -1)` + per-element walk. The runner
installs the constant tree in one step, avoiding N intermediate
PutStructure dispatches. Microsoft Learn: there is no direct LINQ /
Roslyn analogue — this is a domain-specific optimisation documented
by opcodes.dart spec. Preserve the `_isGroundTerm`-guarded shortcut
verbatim, including the nested local function `convertListToStructTerm`
that builds the runtime-side `rt.StructTerm` tree.

### `_groundTermToValue` — dead code, preserved for parity

**Why**: No call site in the current source. Appears to be a sibling
helper for an earlier emission path that has since been replaced by
the runtime-side `convertListToStructTerm` local function inside
`_generateArgumentStructureElement`. Preserve verbatim per CLAUDE.md
"Preserve Working Code — NEVER remove without explicit approval"; add
a `// Currently unused — preserved per CLAUDE.md` comment.

### `_GenerateStructureElementInBody` / `_GenerateListTailInBody` mutual recursion

**Why**: A list `[a, b, c]` is structurally `.(a, .(b, .(c, nil)))` —
the recursive descent MUST flip between general-element (the head)
and list-tail (the tail-or-nil) at each cons cell. The asymmetric-
recursion pattern is necessary because the tail position has
distinguished cases (nil ⇒ SetConstant("nil"), list ⇒ recurse-with-
nested-tail, non-list ⇒ delegate to general-element). Collapsing to
one helper would either bloat the general-element walker with tail-
dispatch logic OR silently break tail-nil handling. Microsoft Learn
explicitly supports mutually-recursive private methods (no special
syntax required); reuse rf-dart-mutually-recursive-methods-to-csharp-
mutually-recursive-methods.

### CompileError-throwing parity — six callsites

**Why**: The file throws `CompileError` with `phase: 'codegen'` six
times — at the head-arg VarTerm undefined-name path, the structure-
element VarTerm path, the arg-structure VarTerm path, the body-
structure VarTerm path, the dynamic-RPC module-VarTerm path, and (a
sixth) inside `_generateStructureElement`. All six MUST be preserved
as distinct call sites (do NOT collapse into a helper) — the source-
side message string differentiation (e.g. "Undefined variable:" vs
"Unknown variable in dynamic RPC:" vs "Undefined variable in
structure:") is the bridge between the runtime error and the user's
mental model of where the error originated. The error.dart spec
already specced `CompileError` as deriving from `System.Exception` per
rf-dart-implements-exception-to-csharp-derive-system-exception
(cached) with a named `phase` arg.

### Reuse summary (FR-024 cached idioms)

| construct family | research_finding_id (cached) |
|------------------|------------------------------|
| imports / namespace | rf-dart-relative-import-to-csharp-using-or-same-namespace |
| dynamic instruction list | rf-dart-dynamic-to-csharp-object |
| mutable dictionary class | rf-dart-mutable-dictionary-class |
| length / isEmpty | rf-dart-length-isempty-to-csharp-count |
| string interpolation | rf-dart-string-interpolation-to-csharp-string-interpolation |
| string EndsWith | rf-dart-string-endswith-to-csharp-string-endswith |
| substring | rf-dart-substring-to-csharp-substring-with-length |
| `is` pattern cascade | rf-dart-is-typetest-cascade-to-csharp-is-pattern-cascade |
| `as` cast → pattern cast | rf-dart-cast-as-to-csharp-pattern-cast |
| `!` bang → not-null pattern | rf-dart-bang-to-csharp-not-null-pattern |
| `!` bang → null-forgiving operator | rf-dart-bang-to-csharp-null-forgiving-operator |
| `!` bang → Nullable.Value | rf-dart-bang-to-csharp-nullable-value |
| string equality | rf-dart-string-equality-to-csharp-string-equality |
| `.every` → `.All` | rf-dart-every-to-csharp-all |
| map literal → dictionary initialiser | rf-dart-map-literal-to-csharp-dictionary-initialiser |
| local function | rf-dart-local-function-to-csharp-local-function |
| mutually recursive methods | rf-dart-mutually-recursive-methods-to-csharp-mutually-recursive-methods |
| named-default arg | rf-dart-named-default-param-to-csharp-optional-arg |
| named-required arg → positional | rf-dart-named-required-param-to-csharp-positional-arg |
| leading-underscore privacy | rf-dart-leading-underscore-privacy-to-csharp-private |
| sentinel magic int | rf-dart-sentinel-magic-int-to-csharp-sentinel-magic-int |
| foreach final → foreach var | rf-dart-foreach-final-to-csharp-foreach-var |
| `.first` → `[0]` | rf-dart-list-first-to-csharp-zero-indexer |
| `runtimeType` → `GetType().Name` | rf-dart-runtime-type-check-to-csharp-is-pattern |
| typed ctor call → `new` | rf-dart-typed-ctor-call-to-csharp-new |
| `implements Exception` → `: Exception` | rf-dart-implements-exception-to-csharp-derive-system-exception |

All twenty-six research findings are CACHED (FR-024) — sourced from the
prior compiler/* + bytecode/* convspecs. NO new research spawned for
this file; the only ID added is `rf-dart-mutable-dictionary-class`
(carry-forward from analyzer.dart's VariableTable specification — the
same idiom applies here to `ImportTable`). Per the contract, every
non-trivial construct attaches BOTH a deep-analysis basis (the
verbatim source-form + the verbatim target-decision) AND a researched-
pattern basis (the cached `research_finding_id`); no escalation is
needed because all conversions are decided by cached idioms or
unambiguous Microsoft Learn citations.
