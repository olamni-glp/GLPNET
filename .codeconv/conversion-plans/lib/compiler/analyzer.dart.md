---
path: lib/compiler/analyzer.dart
cycle_group_id: 39
scc_siblings: []
generated_at: 2026-05-21T16:18:37Z
source_sha256: 531b9f57edc68a07f95f78381c3c38b6953c8506cc799a21dfec8bc73dca32d7
schema_version: 1
---

# Conversion Plan: lib/compiler/analyzer.dart

## 1. Source Analysis

`lib/compiler/analyzer.dart` (1454 lines) is the GLP semantic analyzer.
It consumes the parser's AST and produces an annotated AST consumed by
codegen. The file holds two clusters of top-level declarations:

1. **Per-clause bookkeeping** — `VariableInfo` (per-variable mutable
   counters + flags + nullable register-index + nullable first-occurrence
   back-ref + derived `isAnonymous` / `isSRSWValid` getters) and
   `VariableTable` (a `Map<String, VariableInfo>` plus two `Set<String>`
   tracking grounded / type-grounded variables, plus an unused
   `_hasGroundGuard` flag retained for parity, plus
   `recordWriterOccurrence` / `recordReaderOccurrence` /
   `markGrounded` / `markTypeGrounded` / `allowsMultipleOccurrences` /
   `collectSRSWViolations` / `verifySRSW` / `getAllVars` / `getVar`).

2. **Annotated-AST wrappers + analyzer + strict defined-guard
   evaluator** — three shallow wrapper classes (`AnnotatedProgram`,
   `AnnotatedProcedure` carrying mutable codegen-back-fill fields
   `entryPC` / `entryLabel`, and `AnnotatedClause`), a top-level
   `const _constantTypes` set + `_isConstantType` helper, the public
   coordinator `Analyzer` (eagerly-constructed `DefinedGuardEvaluator`
   dependency, mutable `_procDecls` map and `_compileMode` field, and a
   public `analyze` method running the 4-step pipeline SRSW-validate →
   partial-eval-defined-guards → optional reduce/2-generate →
   per-procedure register-assign), and the file-private STRICT compile-
   time guard evaluator `DefinedGuardEvaluator` (renamed from
   `PartialEvaluator` per commit `213e5601` to disambiguate from the
   LENIENT homonymous class in `partial_evaluator.dart`; throws
   `CompileError` on `UnifyFail` / `UnifySuspend` because defined guards
   MUST reduce at compile time).

Three string-set dispatch tables (`_negatableGuards`,
`_nonNegatableGuards`, `_invalidInGuardPosition`) discriminate guard
legality. The long cascade in `_analyzeGuard` marks arguments as
grounded for SRSW relaxation. Two structurally-identical recursive Term
walkers (`_extractAndMarkGroundedVars`, `_markVarsInTermAsTypeGrounded`)
differ only in which `mark*` method they invoke. The central
`_analyzeTerm` switches on Term subtype, records occurrences for
`VarTerm`, recurses for compound, and validates ConstTerm strings
against the reserved-underscore-prefix rule (only under
`CompileMode.user`).

Five sibling imports: `ast.dart`, `error.dart`,
`partial_evaluator.dart` (filtered `show getPreludeUnitClauses` only —
the lenient `PartialEvaluator` class in that file is deliberately not
imported), `unify_result.dart` (lifted shared ADT after commit
`213e5601`), and the cross-package
`../analysis/type_checker/type_ast.dart`.

The `UnifyResult` sealed ADT (`UnifySuccess` / `UnifyFail` /
`UnifySuspend`) is NOT defined here — it lives in `unify_result.dart`
and is referenced via the new sibling import. The strict
`DefinedGuardEvaluator` consumes it through `_glpUnifyForPE` and
unpacks via Dart pattern-matching `case UnifyFail(:final reason):` etc.
in `_transformClause`.

## 2. Dart → C#/.NET Conversion Plan

For each Dart construct in source order, the convspec construct-key
governs the C#/.NET decision. The plan mirrors the ratified convspec
verbatim; reuse-only decisions cite the cached research-finding ID.

| # | Construct (convspec key) | C#/.NET decision (verbatim from convspec) |
|---|---|---|
| 1 | `dart.module.relative_imports_four_sibling_plus_one_cross_package` | Same-namespace collapse for the four sibling files (`ast.dart`, `error.dart`, `partial_evaluator.dart`, `unify_result.dart`) into `Glp.Runtime.Compiler`; the cross-package `../analysis/type_checker/type_ast.dart` becomes `using Glp.Runtime.Analysis.TypeChecker;`. `show getPreludeUnitClauses` filter becomes a static-class-qualified call site `PartialEvaluatorPrelude.GetPreludeUnitClauses()` — no narrowing `using` directive emitted. Reuse `rf-dart-relative-import-to-csharp-using-or-same-namespace`. |
| 2 | `dart.data_class.variable_info_mutable_counters_with_late_register` | `public sealed class VariableInfo` (NOT a record — reference-identity is load-bearing for identity-keyed side tables). Two `final` ctor-set fields → `public string Name { get; }`, `public bool IsWriter { get; }`. Six mutable counters → `public int X { get; set; }` auto-properties with default `0`. Two nullable reference back-refs → NRT-nullable `public AstNode? FirstOccurrence { get; set; }` / `public string? PairedWriter { get; set; }`. `int? registerIndex` → `public int? RegisterIndex { get; set; }`. Two flags → `public bool IsTemporary { get; set; }` / `IsPermanent { get; set; }`. `String.startsWith('_')` → `Name.StartsWith('_')` (char overload, ordinal). `firstOccurrence ??= node` → `FirstOccurrence ??= node`. Reuse `rf-dart-final-field-class-to-csharp-getonly-class`. |
| 3 | `dart.method.srsw_validity_derived_predicate_with_short_circuit_returns` | 1:1 multi-statement get-only property `public bool IsSRSWValid { get { ... } }` preserving the early-return short-circuit form (do NOT collapse to a single boolean expression; preserve the documented step-by-step order). Reuse `rf-dart-final-field-class-to-csharp-getonly-class`. |
| 4 | `dart.class.variable_table_with_dict_and_two_sets_and_recording_methods` | `public sealed class VariableTable` with `private readonly Dictionary<string, VariableInfo> _vars = new(StringComparer.Ordinal);` and two `private readonly HashSet<string> _groundedVars = new(StringComparer.Ordinal);` / `_typeGroundedVars = new(StringComparer.Ordinal);` (Ordinal because GLP variable names are case-sensitive). `putIfAbsent` → `TryGetValue` + `Add` (reuse `rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add`). `_hasGroundGuard` field RETAINED unused for parity. `getAllVars()` returns `IReadOnlyList<VariableInfo>` via `_vars.Values.ToList()` (snapshot semantics MUST be preserved). The two `record*Occurrence` methods preserve the leading anonymous-variable short-circuit `if (name.StartsWith('_')) return;`. |
| 5 | `dart.method.collect_srsw_violations_iterating_dictionary_emitting_formatted_strings` | `public IReadOnlyList<string> CollectSRSWViolations()` accumulator-loop with FOUR independent (not chained-else-if) `if` branches; each branch emits a verbatim user-visible message string (the unified REPL test suite greps the EXACT format including bare `"` quoting and the `?` reader suffix). `info.firstOccurrence?.line ?? 0` → `info.FirstOccurrence?.Line ?? 0`. Reuse `rf-dart-list-to-csharp-list-of-`. |
| 6 | `dart.method.verify_srsw_throws_on_first_violation_with_searched_node_for_location` | `public void VerifySRSW()` calling `CollectSRSWViolations()`, then `FirstOrDefault(...) ?? First()` to match `firstWhere(orElse:...)`, then `throw new CompileError(...)`. The regex `^Line \d+: ` is HOISTED to a class-scoped `private static readonly Regex RegExSrswLinePrefix = new(@"^Line \d+: ", RegexOptions.Compiled);` (perf optimisation, semantics-preserving). `phase: "analyzer"` named argument preserved. Reuse `rf-dart-implements-exception-to-csharp-derive-system-exception` for the `CompileError` throw. |
| 7 | `dart.ast_leaf.annotated_program_wrapper_holding_original_plus_per_node_annotations` | Three `public sealed class` types: `AnnotatedProgram`, `AnnotatedProcedure` (mutable `EntryPC` / `EntryLabel` as `{ get; set; }` so codegen can back-fill), `AnnotatedClause`. `String get signature => '$name/$arity';` → `public string Signature => $"{Name}/{Arity}";`. Named-default `{this.hasGuards = false, this.hasBody = false}` → C# positional defaults `bool hasGuards = false, bool hasBody = false`. Lists exposed as `IReadOnlyList<...>`. Reuse `rf-dart-final-field-class-to-csharp-getonly-class` and `rf-dart-named-default-param-to-csharp-optional-arg`. |
| 8 | `dart.toplevel.const_string_set_isconstanttype_test` | Hoisted to `Analyzer` static-class private surface: `private static readonly FrozenSet<string> ConstantTypes = new[] { "Integer", "Real", "Number", "String", "Constant" }.ToFrozenSet(StringComparer.Ordinal);` and `private static bool IsConstantType(string? typeName) => typeName is not null && ConstantTypes.Contains(typeName);`. Reuse `rf-dart-const-set-to-csharp-frozenset-ordinal`. |
| 9 | `dart.class.analyzer_coordinator_with_defined_guard_evaluator_field_and_proc_decl_map_and_mode` | `public sealed class Analyzer` with `private readonly DefinedGuardEvaluator _definedGuardEvaluator = new();` (eager field-init, `readonly` from Dart `final`), `private Dictionary<string, ProcDecl> _procDecls = new(StringComparer.Ordinal);` (mutable; rebuilt every `Analyze` call), `private CompileMode _compileMode = CompileMode.User;`. Public method `Analyze(Program program, bool generateReduce = false, IReadOnlyList<ProcDecl>? procDeclarations = null, CompileMode compileMode = CompileMode.User, bool skipGlobalSRSW = false)`. Reuse `rf-dart-coordinator-class-with-final-deps-to-csharp-class-with-readonly-fields`. |
| 10 | `dart.method.analyze_four_step_pipeline_with_skip_flag_and_throwing_aggregator` | 4-step body preserving comment banners. SRSW aggregation via `List<string> allViolations = new(); foreach (...) { allViolations.AddRange(_collectSRSWViolationsForProcedure(proc)); }`. Error message uses `string.Join("\n", allViolations.Select(v => $"  • {v}"))`. Step-4 tuple destructure `(var annotatedProc, _) = _AnalyzeProcedureCollectingErrors(proc, skipSRSW: true);`. Step ordering load-bearing: SRSW runs on the ORIGINAL program BEFORE partial-eval removes defined guards. Reuse `rf-dart-record-named-fields-to-csharp-value-tuple-named-fields`. |
| 11 | `dart.method.collect_srsw_violations_for_procedure_per_clause_walk_with_optional_section_analysis` | `private IReadOnlyList<string> _CollectSRSWViolationsForProcedure(Procedure proc)`. Fresh `VariableTable` per clause (NOT hoisted outside). Walk head + guards + body. Prefix violations with `$"{proc.Name}/{proc.Arity}: {v}"` (verbatim — REPL test suite greps for this format). `clause.guards != null && clause.guards!.isNotEmpty` → `clause.Guards is { Count: > 0 }` property-pattern. Reuse `rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access`. |
| 12 | `dart.method.generate_reduce_clauses_with_existing_index_lookup_and_merge_or_append` | `private Program _GenerateReduceClauses(Program program)`. Continue-on-`reduce/2`-source to prevent infinite recursion. `indexWhere` → manual indexed for-break loop, preserving `-1` sentinel semantics. Dart spread `[...a, ...b]` → `new List<T>(a)` followed by `AddRange(b)`. Reuse `rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange`. |
| 13 | `dart.method.generate_one_reduce_clause_with_atom_to_term_and_goals_to_term` | Four 1:1 helpers: `_GenerateReduceClause`, `_AtomToTerm`, `_GoalsToTerm`, `_GoalToTerm`. Right-associative conjunction build via reverse-index `for` loop (preserve order). `[Goal('true', [], line, col)]` → `new List<Goal> { new Goal("true", Array.Empty<Term>(), line, col) }`. Reuse `rf-dart-indexed-min-length-for-loop-to-csharp-imperative-for` and `rf-dart-const-empty-list-default-to-csharp-array-empty`. |
| 14 | `dart.method.analyze_procedure_and_clause_collecting_errors_with_skip_flag` | FOUR sibling methods preserving the throw/collect split: `_AnalyzeProcedure`, `_AnalyzeClause` (throwing), `_AnalyzeProcedureCollectingErrors`, `_AnalyzeClauseCollectingErrors` (collecting `(AnnotatedX, IReadOnlyList<string>)` tuples). Do NOT refactor to a shared helper — duplication is deliberate. The throwing `_AnalyzeProcedure` is retained even though currently unreferenced (preserve-working-code §0). Register assignment via `_AssignRegisters` happens AFTER collection EVEN IF violations were found (partial-analysis). Reuse `rf-dart-record-named-fields-to-csharp-value-tuple-named-fields` and `rf-dart-named-default-param-to-csharp-optional-arg`. |
| 15 | `dart.method.analyze_atom_and_goal_argument_walk` | Two trivial dispatchers `_AnalyzeAtom(Atom, VariableTable)` and `_AnalyzeGoal(Goal, VariableTable)`. Two distinct methods (NOT one generic helper) because `Atom` and `Goal` are distinct C# types per `ast.dart` spec. |
| 16 | `dart.class_static_const_string_sets_negatable_nonnegatable_invalid_guard_dispatch_tables` | Three `private static readonly FrozenSet<string>` fields on `Analyzer`: `NegatableGuards`, `NonNegatableGuards`, `InvalidInGuardPosition`, each built via `new[] { ... }.ToFrozenSet(StringComparer.Ordinal)`. Members preserved VERBATIM (the sets ARE the GLP language spec). Reuse `rf-dart-const-set-to-csharp-frozenset-ordinal`. |
| 17 | `dart.method.analyze_guard_dispatch_table_over_predicate_and_arity` | `private void _AnalyzeGuard(Guard guard, VariableTable varTable)` with the same `if`-cascade (NOT switched to `switch` — multiple `if`s can match; cascade is intentional). The two LOCAL Dart lists `typeCheckOps` / `comparisonOps` are HOISTED to module-static `FrozenSet<string>` fields. `arg is VarTerm` smart-cast → `if (arg is VarTerm v)` declaration-pattern. Final argument walk passes `inHeadOrBody: false` (named-arg) to mark guard occurrences as non-SRSW-counting. Reuse `rf-dart-is-test-smart-cast-to-csharp-declaration-pattern`. |
| 18 | `dart.method.recursive_var_walk_extract_and_mark_grounded_and_mark_type_grounded` | Two C# methods preserving the duplication (`_ExtractAndMarkGroundedVars`, `_MarkVarsInTermAsTypeGrounded`). C# type-pattern `switch` with `case VarTerm v:` / `case StructTerm s:` / `case ListTerm l:` — no default arm so `ConstTerm` / `UnderscoreTerm` fall through silently. Reuse `rf-dart-is-type-test-chain-to-csharp-pattern-switch` and `rf-dart-nullable-field-bang-after-null-check-to-csharp-flow-analysis`. |
| 19 | `dart.method.analyze_term_recursive_record_var_then_validate_const_in_user_mode` | `private void _AnalyzeTerm(Term term, VariableTable varTable, bool inHeadOrBody = true)` switching on Term subtype. The inner `value is string s && s.StartsWith('_')` C# declaration-pattern handles the null-and-non-string case correctly. Both copies of the anonymous-variable short-circuit (this one + the one inside `VariableTable.RecordXxxOccurrence`) preserved. The `_compileMode == CompileMode.User` mode-dependence preserved (under `CompileMode.System` the reserved-underscore check is disabled). Reuse `rf-dart-is-type-test-chain-to-csharp-pattern-switch`. |
| 20 | `dart.method.assign_registers_sequential_temporary_loop_with_todo` | `private void _AssignRegisters(VariableTable varTable) { int nextIndex = 0; foreach (var info in varTable.GetAllVars()) { info.IsTemporary = true; info.RegisterIndex = nextIndex++; } }`. TODO comment carried over. Snapshot iteration safe (no mutation of `_vars` during enumeration). |
| 21 | `dart.method.mark_constant_type_vars_in_head_via_proc_decl_lookup` | `private void _MarkConstantTypeVars(Atom head, string procName, int procArity, VariableTable varTable)` using `_procDecls.TryGetValue(key, out var procDecl)` early-return on miss. Indexed `for` with conjoined `i < head.Args.Count && i < procDecl.ArgTypes.Count` bound. Reuse `rf-dart-map-lookup-to-csharp-trygetvalue` and `rf-dart-indexed-min-length-for-loop-to-csharp-imperative-for`. |
| 22 | `dart.class.analyzer_internal_defined_guard_evaluator_class_with_guard_unfolding` | `public sealed class DefinedGuardEvaluator` in `Glp.Runtime.Compiler` (distinct from lenient `PartialEvaluator` — both coexist in same namespace, no clash). Every helper (`TransformDefinedGuards`, `_CollectUnitClauses`, `_TransformClause`, `_RenameUnitClauseVars`, `_CollectVarNames`, `_ApplyRenaming`, `_GlpUnifyForPE`, `_SubstSet`, `_UnifyTerms`, `_CheckCompatible`, `_IsAnonymous`, `_ResolveSubstitution`, `_ResolveTerm`, `_ApplySubstitution`, `_ApplySubstitutionToAtom`, `_ApplySubstitutionToGuard`, `_ApplySubstitutionToGoal`) DELEGATES its construct-by-construct mapping to the matching cached construct-key in `partial_evaluator.dart.md` (listed verbatim in the convspec). `_varCounter` fresh-name prefix preserved as literal `"PE_"`. STRICT throw-vs-return distinction preserved: `_TransformClause` throws `CompileError` on `UnifyFail` / `UnifySuspend` (the lenient sibling in `partial_evaluator.dart` returns failure via the same shared `UnifyResult` ADT). The shared `UnifyResult` ADT is imported from `lib/compiler/unify_result.dart` (spec'd separately at `.codeconv/conversion-specs/lib/compiler/unify_result.dart.md`). Reuse `rf-dart-coordinator-class-with-final-deps-to-csharp-class-with-readonly-fields`. |

The convspec's `conversion_units` declares ONE output unit:
`analyzer.cs` (top-level — hosts public `Analyzer`, file-private
`VariableInfo`, `VariableTable`, `AnnotatedProgram`,
`AnnotatedProcedure`, `AnnotatedClause`, and file-internal
`DefinedGuardEvaluator`). The shared `UnifyResult` ADT is NOT emitted
here — it lives in `unify_result.cs` (consumed via the same-namespace
rule).

## 3. Decomposed Task Units

- **T1** — Emit `analyzer.cs` namespace + `using` directives (one cross-package `using Glp.Runtime.Analysis.TypeChecker;` only; sibling files collapse via same-namespace rule per construct #1).
- **T2** — Emit `public sealed class VariableInfo` with two get-only identity props, six mutable counter auto-props, two nullable back-refs, one nullable `int? RegisterIndex`, two flag props, and two derived getters `IsAnonymous` / `IsSRSWValid` (constructs #2, #3).
- **T3** — Emit `public sealed class VariableTable` with the three Ordinal collections + `_hasGroundGuard` parity field + `RecordWriterOccurrence` / `RecordReaderOccurrence` (with leading underscore short-circuit) + `MarkGrounded` / `IsGrounded` / `MarkTypeGrounded` / `IsTypeGrounded` / `AllowsMultipleOccurrences` + `CollectSRSWViolations` (four independent `if` branches with verbatim user-visible message format) + `VerifySRSW` (with hoisted compiled `Regex` + `FirstOrDefault ?? First()` + named `phase: "analyzer"` throw) + `GetAllVars` (snapshot `.ToList()`) + `GetVar` + `ToString` (constructs #4, #5, #6).
- **T4** — Emit three sealed wrapper classes `AnnotatedProgram`, `AnnotatedProcedure` (with mutable `EntryPC` / `EntryLabel` + `Signature` expression-property), `AnnotatedClause` (with positional default `hasGuards = false, hasBody = false` parameters and `ToString` overrides) (construct #7).
- **T5** — Emit Analyzer-private static `FrozenSet<string> ConstantTypes` and `IsConstantType` (construct #8).
- **T6** — Emit `public sealed class Analyzer` skeleton with `readonly DefinedGuardEvaluator _definedGuardEvaluator = new();` + mutable `_procDecls` Ordinal dictionary + `_compileMode` field + explicit empty default ctor (construct #9).
- **T7** — Emit public `Analyze(...)` method with reset-then-build of `_procDecls`, 4-step pipeline preserving step ordering (SRSW BEFORE partial-eval), step-4 tuple-destructure (`(var annotatedProc, _) = ...`) (construct #10).
- **T8** — Emit `_CollectSRSWViolationsForProcedure` with fresh-per-clause `VariableTable`, property-pattern `Guards is { Count: > 0 }`, verbatim `"{proc.Name}/{proc.Arity}: {v}"` prefix (construct #11).
- **T9** — Emit `_GenerateReduceClauses` with continue-on-`reduce/2`-source, manual indexed for-break loop preserving `-1` sentinel, shallow `new List<Procedure>(program.Procedures)` copy (construct #12).
- **T10** — Emit `_GenerateReduceClause` + `_AtomToTerm` + `_GoalsToTerm` (reverse-index `for` for right-associative conjunction) + `_GoalToTerm` (construct #13).
- **T11** — Emit `_AnalyzeProcedure` (throwing, retained for parity even if unreferenced) + `_AnalyzeProcedureCollectingErrors` + `_AnalyzeClause` (throwing) + `_AnalyzeClauseCollectingErrors`, preserving the deliberate duplication; register-assignment runs AFTER violation collection (construct #14).
- **T12** — Emit `_AnalyzeAtom` + `_AnalyzeGoal` trivial dispatchers (construct #15).
- **T13** — Emit three Analyzer-private static `FrozenSet<string>` fields `NegatableGuards`, `NonNegatableGuards`, `InvalidInGuardPosition` (construct #16) AND two HOISTED static `FrozenSet<string>` fields `TypeCheckOps`, `ComparisonOps` (Dart-source-local lists hoisted per construct #17 perf rationale).
- **T14** — Emit `_AnalyzeGuard` with the `if`-cascade (NOT switched), declaration-pattern `if (arg is VarTerm v)` smart-casts, final-argument-walk passing `inHeadOrBody: false` named-arg (construct #17).
- **T15** — Emit `_ExtractAndMarkGroundedVars` + `_MarkVarsInTermAsTypeGrounded` as two separate type-pattern `switch` methods (no delegate-callback refactor; preserve duplication; no default arm so ConstTerm/UnderscoreTerm fall through) (construct #18).
- **T16** — Emit `_AnalyzeTerm` switch-on-Term with leading anonymous-var short-circuit, mode-dependent reserved-underscore-prefix check on ConstTerm, inHeadOrBody plumbed through all recursive calls (construct #19).
- **T17** — Emit `_AssignRegisters` (`foreach` with post-increment) with carried-over TODO comment (construct #20).
- **T18** — Emit `_MarkConstantTypeVars` using `TryGetValue` and `i < a && i < b` conjoined for-condition (construct #21).
- **T19** — Emit `public sealed class DefinedGuardEvaluator` with `_varCounter` int field, all 17 helpers each DELEGATING to the corresponding cached construct-key in `partial_evaluator.dart.md`; STRICT throw-on-fail/suspend semantics preserved in `_TransformClause`; literal prefix `"PE_"` preserved (construct #22).
- **T20** — Cross-check at codegen-emit time that `UnifyResult` / `UnifySuccess` / `UnifyFail` / `UnifySuspend` are referenced from the shared `unify_result.cs` unit (NOT redefined here).

## 4. Research Findings

none required — every construct is verbatim-derivable from the ratified
convspec at `.codeconv/conversion-specs/lib/compiler/analyzer.dart.md`
(which itself cites cached findings reused from `ast.dart`,
`error.dart`, `parser.dart`, `partial_evaluator.dart`, `glp_printer.dart`,
`checker.dart`, `pmt/type_table.dart`, `mode_table.dart`,
`occurrence.dart`, and `type_checker.dart` family specs).
The renaming `PartialEvaluator` → `DefinedGuardEvaluator` and the lift
of `UnifyResult` to a shared sibling file are both Dart-source-level
refactors (commit `213e5601`, Gabi-approved) that the C# port mirrors
literally; both previously-recorded escalations are resolved at the
convspec level (`escalations: []`, `open_escalation_count = 0`).

## 5. Consistency Pass

fixed — derived from the ratified convspec at
`.codeconv/conversion-specs/lib/compiler/analyzer.dart.md` (verified
internally consistent: 22 constructs, zero open escalations, single
`analyzer.cs` conversion unit, all reused research-findings cite
already-cached IDs, and the cross-references to
`partial_evaluator.dart.md` / `unify_result.dart.md` / `ast.dart.md` /
`error.dart.md` align with the per-file convspecs already ratified in
the repo).

## 6. Escalations

None.
