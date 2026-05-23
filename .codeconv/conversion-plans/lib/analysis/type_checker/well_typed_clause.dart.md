---
path: lib/analysis/type_checker/well_typed_clause.dart
cycle_group_id: 17
scc_siblings: []
generated_at: 2026-05-21T15:25:14Z
source_sha256: 66445ae92069c7cdf6bc5871f1666b696eabd8a80a08118cb5114b32fe6cc918
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/well_typed_clause.dart

## 1. Source Analysis

Direct inspection of the 1046-line source confirms the convspec inventory. The file is the GLP type-checker's well-typed-clause decision procedure (Definition 4.8 / 5.7 of the GLP paper), purely synchronous.

Constituents (matching the convspec's 22 constructs):

1. `ClauseCheckResult` — value class (named-required ctor + two factory ctors `success`/`failure`), five fields: `isWellTyped`, `variableTypes`, `errors`, `modedHead?`, `modedBodyAtoms` (default `const []`).
2. `ClauseError` — abstract base with `String get message` contract.
3. `HeadError extends ClauseError` — positional ctor (`procedureName`, `termErrors: List<WellTypedError>`); message via `termErrors.map(e=>e.message).join('\n  ')`.
4. `BodyAtomError extends ClauseError` — positional ctor (`procedureName`, `atomIndex`, `termErrors`); same `.map(...).join(...)` pattern.
5. `ClauseDualityError extends ClauseError` — six-positional ctor with optional `[reason]`; multi-statement getter with intermediate `reasonStr` local + interpolation of nullable `VariableTypeInfo?` writer/reader.
6. `UndefinedProcedureError extends ClauseError` — `procedureName`/`arity`; interpolated message.
7. `ArityMismatchClauseError extends ClauseError` — `procedureName`/`expectedArity`/`actualArity`.
8. `UndeclaredProcedureError implements Exception` — marker-interface exception with `functor`/`arity`; `toString` only.
9. `TypedClause` — named-required `head`, defaulted `bodyAtoms = const []`, `guardAtoms = const []`; derived `headFunctor`/`headArity` getters.
10. `checkClause(TypedClause, ProgramDFA, TypeEnvironment) → ClauseCheckResult` — 85-line orchestrator: proc lookup, arity check, head check, body-atom loop with merge, duality check.
11. `checkClauseFromAst(ast.Clause, ProgramDFA, TypeEnvironment) → ClauseCheckResult` — converts AST; uses spread `[...guardGoals, ...bodyGoals]`; throws `UndeclaredProcedureError`.
12. `getAcceptedLabels(ast.Clause, int, TypeEnvironment) → Set<String>?` — public; tri-state (null=wildcard, `{}`=empty, set=concrete).
13. `getLabelsFromTerm(ast.Term) → Set<String>?` — type-test chain over `VarTerm|UnderscoreTerm|ConstTerm|ListTerm|StructTerm`.
14. `getFullTypeName(TypeExpr) → String` — type-test chain over `PrimitiveModeAlt|TypeRef`; throws `ArgumentError`.
15. `_checkHead` / `_checkHeadWithTerm` — tuple-returning private helpers; `on ArityMismatchError catch (e)`.
16. `_checkBodyAtom` / `_checkBodyAtomWithTerm` — case-dispatch over `SpawnGoal`/`RemoteGoal`/builtin/parameterized template; tuple return; named-optional `{callerVarTypes}`.
17. `_checkRemoteGoal` — while-`is`-test loop unfolding `RemoteGoal`s; bang operator on `staticModuleName`.
18. `_checkModedTermPerArg` — per-arg automaton walk; `is!` test; `try ... on StateError` continue.
19. `_checkTermDuality` — same logic as `well_typed_term.dart._checkDuality` (group by base, check pair duality).
20. `_normalizeLocation` — string classifier with two branches.
21. `_checkClauseDuality` — triple-branch dispatch (head/head exact-dual, body/body subtyping, mixed same-type); two tuple-destructured calls.
22. `_areDualTypes` / `_areSameTypeWithReason` / `_areDualTypesWithReason` — thin tuple predicates + discard pattern.
23. `_inferConcreteDecl` — case-B parameterized-proc inference; mixed positional+named `ProcDecl(...)` ctor call.
24. `_matchTypeForInference` — recursive `is TypeRef` match with substring extraction.
25. `_splitTypeArgs` — depth-tracking comma split; two `Substring` shapes (two-arg + one-arg).
26. `_substituteTypeParams` — recursive `TypeExpr` substitution; `Iterable.map(...).toList()`, `every`, `.map((a)=>(a as TypeRef).name).join(',')`, fall-through `if (expr is PrimitiveModeAlt) return expr; return expr;` (literal preserved per FR-023).

Imports: `mode.dart`, `moded_term.dart`, `moded_head.dart`, `well_typed_term.dart`, `program_dfa.dart`, `subtyping.dart`, `type_ast.dart`, `prelude.dart`, `../../compiler/ast.dart as ast`.

## 2. Dart → C#/.NET Conversion Plan

Each construct mirrors the ratified convspec verbatim. Decisions are listed below per construct (one per source-file element); detailed rationale lives in the convspec.

### 2.1 `ClauseCheckResult` (value class with factory helpers)

→ `public sealed class ClauseCheckResult` with read-only auto-properties: `IsWellTyped` (`bool`), `VariableTypes` (`IReadOnlyDictionary<string, VariableTypeInfo>`), `Errors` (`IReadOnlyList<ClauseError>`), `ModedHead` (`ModedTerm?`), `ModedBodyAtoms` (`IReadOnlyList<ModedTerm>`). Primary ctor takes all five; call sites use C# named-argument syntax to mirror Dart `{required …}` shape (cached `rf-dart-named-required-params-to-csharp-named-positional`). The two `factory` ctors become `public static ClauseCheckResult Success(...)` / `Failure(...)` static methods (cached `rf-dart-factory-ctor-const-default-to-csharp-static-factory`). The Dart `const []` parameter default maps to `IReadOnlyList<ModedTerm>? modedBodyAtoms = null` + body coalesce `modedBodyAtoms ?? Array.Empty<ModedTerm>()` (cached `rf-dart-const-empty-list-default-to-csharp-static-empty-array`). No equality override (transient return-vehicle).

### 2.2 `ClauseError` (abstract pure-contract base)

→ `public abstract class ClauseError` with `public abstract string Message { get; }`. Abstract class (not interface) — open-ended error-ADT extension model (cached `rf-dart-abstract-class-pure-contract-to-csharp-interface` overridden to abstract class per project convention).

### 2.3 `HeadError` / `BodyAtomError` (positional-ctor + `.map(...).join(...)` message)

→ `public sealed class HeadError : ClauseError` / `public sealed class BodyAtomError : ClauseError`, each with positional ctor + read-only auto-properties + expression-bodied `public override string Message => $"...{string.Join("\n  ", TermErrors.Select(e => e.Message))}";` + `public override string ToString() => Message;`. Cached `rf-dart-iterable-map-join-to-csharp-linq-select-string-join`.

### 2.4 `ClauseDualityError` (nullable-interpolation in multi-statement getter)

→ `public sealed class ClauseDualityError : ClauseError` with positional ctor including optional `string? reason = null`. Body-bodied property getter with intermediate `var reasonStr = Reason != null ? $": {Reason}" : "";` local + final `$"Variable pair ({BaseName}, {BaseName}?) not dual across clause{reasonStr}: writer at {WriterLocation}={WriterType?.ToString() ?? "null"}, reader at {ReaderLocation}={ReaderType?.ToString() ?? "null"}"` interpolation. The `?.ToString() ?? "null"` coalescence preserves Dart's `"null"` literal output for null nullable holes (cached `rf-csharp-interpolation-null-vs-dart-null-tostring`).

### 2.5 `UndefinedProcedureError` / `ArityMismatchClauseError` (int-interp)

→ `public sealed class UndefinedProcedureError : ClauseError` / `... ArityMismatchClauseError : ClauseError`. Expression-bodied `Message` overrides with `{Arity.ToString(CultureInfo.InvariantCulture)}` etc. (cached `rf-csharp-int-interp-culture-invariant`).

### 2.6 `UndeclaredProcedureError implements Exception`

→ `public sealed class UndeclaredProcedureError : Exception`. Ctor passes the formatted message to `base(string message)` and sets `Functor`/`Arity` properties. `public override string ToString() => $"UndeclaredProcedureError: {Functor}/{Arity.ToString(CultureInfo.InvariantCulture)}";` — bare formatted string, no `.NET`-default type-name prefix (cached `rf-dart-implements-exception-to-csharp-extends-system-exception`).

### 2.7 `TypedClause` (data class with getters + ast prefix)

→ `public sealed class TypedClause` with `Head` (`Goal`), `BodyAtoms` (`IReadOnlyList<Goal>`), `GuardAtoms` (`IReadOnlyList<Goal>`); ctor `(Goal head, IReadOnlyList<Goal>? bodyAtoms = null, IReadOnlyList<Goal>? guardAtoms = null)` with body coalesce to `Array.Empty<Goal>()`. Computed `public string HeadFunctor => Head.Functor;` and `public int HeadArity => Head.Arity;` expression-bodied. File-head `using Goal = Glp.Compiler.Ast.Goal;` alias (and one for each `ast.Term`/`ast.Clause`/etc.) per cached `rf-dart-import-prefix-as-to-csharp-using-alias`.

### 2.8 `WellTypedClause` host static class — all top-level functions become static methods

→ `public static class WellTypedClause` (file-name PascalCase; mirrors `WellTypedTerm`). Cached `rf-csharp-static-class-no-toplevel-members`.

### 2.9 `CheckClause` (orchestrator)

→ `public static ClauseCheckResult CheckClause(TypedClause clause, ProgramDFA dfa, TypeEnvironment env)`. Local-variable mappings: `var errors = new List<ClauseError>();`, `var allVariableTypes = new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal);`, `var variableLocations = new Dictionary<string, string>(StringComparer.Ordinal);`, `ModedTerm? constructedModedHead = null;`, `var constructedModedBodyAtoms = new List<ModedTerm>();`. The Dart record-destructuring `final (headResult, modedHeadTerm) = _checkHeadWithTerm(...)` → C# `var (headResult, modedHeadTerm) = CheckHeadWithTerm(...)` (cached `rf-dart-record-destructure-to-csharp-tuple-deconstruct`). The C-style `for (int i = 0; i < clause.BodyAtoms.Count; i++)` (`Count`, not `Length`, on `IReadOnlyList<T>`). Map indexer + ContainsKey calls map 1:1. The `'body atom $i'` → `$"body atom {i.ToString(CultureInfo.InvariantCulture)}"`. `errors.isEmpty` → `errors.Count == 0`. Returns via primary-ctor with named args.

### 2.10 `CheckClauseFromAst` (convenience overload with spread + throw)

→ `public static ClauseCheckResult CheckClauseFromAst(Clause clause, ProgramDFA dfa, TypeEnvironment env)`. Spread `[...guardGoals, ...bodyGoals]` → C# 12 collection expression `[..guardGoals, ..bodyGoals]` typed as `IReadOnlyList<Goal>` (cached `rf-dart-collection-spread-to-csharp-collection-expression-spread`). `clause.guards != null` followed by `clause.guards!` → C# narrows automatically via nullable flow analysis (bang token dropped). `clause.body ?? []` → `clause.Body ?? Array.Empty<Goal>()`. `throw UndeclaredProcedureError(...)` → `throw new UndeclaredProcedureError(...)` (cached `rf-dart-throw-bare-constructor-to-csharp-throw-new`).

### 2.11 `GetAcceptedLabels` + `GetLabelsFromTerm`

→ Both `public static IReadOnlySet<string>? Get...` returning nullable set. Dart contextual `{}` empty set → `new HashSet<string>(StringComparer.Ordinal)` (cached `rf-csharp-string-set-ordinal`). `GetLabelsFromTerm` body emits a C# 9 switch expression with type patterns + or-pattern combinator: `term switch { VarTerm or UnderscoreTerm => null, ConstTerm c => new HashSet<string>(StringComparer.Ordinal) { c.Value.ToString() ?? "" }, ListTerm l => new HashSet<string>(StringComparer.Ordinal) { l.IsNil ? "[]" : "[|]" }, StructTerm s => new HashSet<string>(StringComparer.Ordinal) { $"{s.Functor}/{s.Arity.ToString(CultureInfo.InvariantCulture)}" }, _ => new HashSet<string>(StringComparer.Ordinal) }` (cached `rf-dart-type-test-chain-to-csharp-switch-expression-or-pattern`).

### 2.12 `GetFullTypeName`

→ `public static string GetFullTypeName(TypeExpr typeExpr)`. Same switch-expression idiom: `typeExpr switch { PrimitiveModeAlt p => p.IsInput ? "_?" : "_", TypeRef r => r.IsInput ? $"{r.Name}?" : r.Name, _ => throw new ArgumentException($"Unknown type expression: {typeExpr}") }`. `ArgumentError` → `ArgumentException` (cached `rf-dart-argumenterror-to-csharp-argumentexception`).

### 2.13 `_CheckHead` / `_CheckHeadWithTerm`

→ `private static WellTypedResult CheckHead(...)` (delegates to `CheckHeadWithTerm`, discards the `term` element). `private static (WellTypedResult result, ModedTerm? term) CheckHeadWithTerm(TypedClause clause, ProcDecl procDecl, ProgramDFA dfa, TypeEnvironment env)` — named ValueTuple return. `try { var modedHeadTerm = ModedHead(clause.Head, procDecl, typeEnv: env); var result = CheckModedTermPerArg(modedHeadTerm, procDecl, dfa); return (result, modedHeadTerm); } catch (ArityMismatchError e) { return (WellTypedResult.Failure(new[] { new InconsistentPathError(new ModedPath(new[] { new PathStep(symbol: e.Message, argIndex: 0, mode: Mode.Produce) }), e.Message) }), null); }`. Cached `rf-dart-on-typed-catch-to-csharp-typed-catch`. List literals → C# 12 collection expressions where targeted at `IReadOnlyList<T>`.

### 2.14 `_CheckBodyAtom` / `_CheckBodyAtomWithTerm`

→ `private static (WellTypedResult result, ModedTerm? term) CheckBodyAtomWithTerm(Goal atom, int atomIndex, ProgramDFA dfa, TypeEnvironment env, IReadOnlyDictionary<string, VariableTypeInfo>? callerVarTypes = null)` (cached `rf-dart-optional-named-param-to-csharp-default-named`; named-arg call style preserved at sites). Early-return chain with `is`-pattern-capture: `if (atom is SpawnGoal spawn) return CheckBodyAtomWithTerm(spawn.InnerGoal, atomIndex, dfa, env, callerVarTypes: callerVarTypes);`, `if (atom is RemoteGoal remote) return CheckRemoteGoal(remote, atomIndex, dfa, env);`, `if (Prelude.IsBuiltinGoal(atom.Functor)) return (WellTypedResult.Success(EmptyDict), null);`. Dictionary lookup via `env.ParamProcDecls.GetValueOrDefault(procDecl.Key)`. Mutable local `var procDecl = ...; procDecl = inferredDecl;` mirrors Dart shape. Cached `rf-dart-stateerror-to-csharp-invalidoperationexception` does NOT apply here (no `dfa.GetAutomaton` call in this method).

### 2.15 `_CheckRemoteGoal`

→ `private static (WellTypedResult result, ModedTerm? term) CheckRemoteGoal(RemoteGoal remote, int atomIndex, ProgramDFA dfa, TypeEnvironment env)`. The while-type-test loop becomes C# `while (innerGoal is RemoteGoal rg) { if (rg.IsDynamic) { return (WellTypedResult.Success(EmptyDict), null); } pathParts.Add(rg.StaticModuleName!); innerGoal = rg.Goal; }` — consolidating Dart's separate cast (cached `rf-dart-while-istest-with-cast-to-csharp-while-pattern-bind`). The bang `rg.StaticModuleName!` preserved literally (cached `rf-dart-bang-runtime-throw-vs-csharp-null-forgiving-static-only` — recorded divergence; safe here because `!rg.IsDynamic` guarantees non-null). String join: `string.Join("#", pathParts)`. Lookup: `var procDecl = env.Procedures.GetValueOrDefault(qualifiedKey);`. Then same `try { ... CheckModedTermPerArg(...) } catch (ArityMismatchError e) { ... }` as `CheckHeadWithTerm`.

### 2.16 `_CheckModedTermPerArg`

→ `private static WellTypedResult CheckModedTermPerArg(ModedTerm modedTerm, ProcDecl decl, ProgramDFA dfa)`. The `is!` test → C# `if (modedTerm is not ModedCompound compound) { return WellTypedResult.Failure(...); }` (cached `rf-dart-is-not-test-to-csharp-is-not-pattern`) — binds `compound` to the narrowed value in the falling-through arm. The `try { argAutomaton = dfa.GetAutomaton(argTypeName); } catch (InvalidOperationException) { errors.Add(...); continue; }` (cached `rf-dart-stateerror-to-csharp-invalidoperationexception`). The chained `variableTypes[varKey]!.typeState.name != result.variableAssignment!.typeState.name` → restructured to `var existing = variableTypes[varKey]; var assignment = result.VariableAssignment!; if (existing.TypeState.Name != assignment.TypeState.Name) { ... }` eliminating one bang (the post-ContainsKey indexer is non-null under C# flow analysis).

### 2.17 `_CheckTermDuality`

→ `private static IReadOnlyList<NonDualError> CheckTermDuality(IReadOnlyDictionary<string, VariableTypeInfo> variableTypes)`. Literal-identical to `WellTypedTerm.CheckDuality` (FR-023 mandates the duplication is preserved, not refactored). Cached `rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add`.

### 2.18 `_NormalizeLocation`

→ `private static string NormalizeLocation(string location) => location == "head" ? "head" : (location.StartsWith("body", StringComparison.Ordinal) ? "body" : location);` — expression-bodied with nested ternaries. Cached `rf-csharp-string-equality-ordinal-by-default`.

### 2.19 `_CheckClauseDuality`

→ `private static IReadOnlyList<ClauseDualityError> CheckClauseDuality(IReadOnlyDictionary<string, VariableTypeInfo> variableTypes, IReadOnlyDictionary<string, string> variableLocations, ProgramDFA dfa)`. Same group-by-base-name shape as `CheckTermDuality`. Triple-branch dispatch: `if (writerNormLoc == readerNormLoc) { if (writerNormLoc == "head") { var (isCompat, reason) = AreDualTypesWithReason(...); if (!isCompat) { errors.Add(new ClauseDualityError(...)); } } else { ... isSub = Subtyping.IsSubtype(...); ... } } else { var (isSame, reason) = AreSameTypeWithReason(...); if (!isSame) { errors.Add(new ClauseDualityError(...)); } }`. The cross-file `isSubtype(...)` callsite becomes `Subtyping.IsSubtype(...)` (explicit host-class qualifier).

### 2.20 `_AreDualTypes` / `_AreSameTypeWithReason` / `_AreDualTypesWithReason`

→ Three private static helpers: `private static bool AreDualTypes(VariableTypeInfo writerInfo, VariableTypeInfo readerInfo) { var (isCompat, _) = AreDualTypesWithReason(writerInfo, readerInfo); return isCompat; }` (cached `rf-dart-discard-pattern-to-csharp-discard`); `private static (bool isSame, string? reason) AreSameTypeWithReason(...)` and `private static (bool isCompat, string? reason) AreDualTypesWithReason(...)` — named-tuple-element returns. Enum compares: `writerInfo.Mode != Mode.Produce`, `readerInfo.Mode != Mode.Consume`. State.BaseName comparison + isDual XOR check.

### 2.21 `_InferConcreteDecl`

→ `private static ProcDecl? InferConcreteDecl(ProcDecl paramTemplate, Goal atom, IReadOnlyDictionary<string, VariableTypeInfo> callerVarTypes, ProgramDFA dfa, TypeEnvironment env)`. `var bindings = new Dictionary<string, string>(StringComparer.Ordinal);`. Loop uses `Math.Min`-equivalent dual-bound `for (int i = 0; i < paramTemplate.Arity && i < atom.Args.Count; i++)`. `if (actualArg is VarTerm v) { var varKey = v.IsReader ? $"{v.Name}?" : v.Name; ... }`. Mixed positional + named constructor call: `new ProcDecl(paramTemplate.Name, concreteArgTypes, paramTemplate.Line, paramTemplate.Column, exported: paramTemplate.Exported, imported: paramTemplate.Imported, modulePath: paramTemplate.ModulePath)` (cached `rf-dart-mixed-positional-and-named-ctor-call-to-csharp-mixed-args`). `dfa.Automata.ContainsKey(typeName)` for type-existence check.

### 2.22 `_MatchTypeForInference`

→ `private static void MatchTypeForInference(TypeExpr declaredType, string actualTypeName, IReadOnlyList<string> typeParams, IDictionary<string, string> bindings)`. NOTE `IDictionary<,>` (NOT `IReadOnly...`) because the method mutates the dict. `if (declaredType is TypeRef ref)` declaration pattern. The substring extraction is the load-bearing site: `actualTypeName.Substring(0, ltIdx)` (start=0, direct mapping) and `actualTypeName.Substring(ltIdx + 1, actualTypeName.Length - ltIdx - 2)` (length-based with the off-by-one adjustment for `Substring(ltIdx+1, actualName.length-1)` semantics) (cached `rf-dart-string-substring-end-exclusive-to-csharp-substring-length`). `putIfAbsent(name, () => value)` → `if (!bindings.ContainsKey(name)) bindings[name] = value;`. `typeParams.Contains(name)` (LINQ extension over `IReadOnlyList<string>`).

### 2.23 `_SplitTypeArgs`

→ `private static List<string> SplitTypeArgs(string s) { var result = new List<string>(); var depth = 0; var start = 0; for (int i = 0; i < s.Length; i++) { if (s[i] == '<') depth++; if (s[i] == '>') depth--; if (s[i] == ',' && depth == 0) { result.Add(s.Substring(start, i - start).Trim()); start = i + 1; } } if (start < s.Length) { result.Add(s.Substring(start).Trim()); } return result; }`. Two substring shapes: two-arg `Substring(start, i - start)` (length-based adjustment) and one-arg `Substring(start)` (no length needed). `char` indexer comparison `s[i] == '<'` uses value-type char equality.

### 2.24 `_SubstituteTypeParams`

→ `private static TypeExpr SubstituteTypeParams(TypeExpr expr, IReadOnlyDictionary<string, string> bindings)`. `if (expr is TypeRef r) { ... }`. `r.TypeArgs.Select(a => SubstituteTypeParams(a, bindings)).ToList()` (cached `rf-dart-iterable-map-to-csharp-linq-select`). `newArgs.All(a => a is TypeRef tr && tr.TypeArgs.Count == 0 && !bindings.ContainsKey(tr.Name))` (LINQ `All` for Dart `every`). Inner `string.Join(",", newArgs.Cast<TypeRef>().Select(a => a.Name))` (cached `rf-dart-iterable-cast-to-csharp-enumerable-cast`). `bindings[expr.Name]!` on the post-ContainsKey indexer becomes direct indexer in C# (the bang is dropped). Constructors: `new TypeRef(bindings[r.Name], r.Line, r.Column, isInput: r.IsInput)` and `new TypeRef(r.Name, r.Line, r.Column, isInput: r.IsInput, typeArgs: newArgs)`. The fall-through `if (expr is PrimitiveModeAlt) return expr; return expr;` preserved literally per FR-023.

## 3. Decomposed Task Units

- T1: emit `ClauseCheckResult` sealed class with three positional + two factory ctors (§2.1) — done in spec
- T2: emit `ClauseError` abstract base (§2.2) — done in spec
- T3: emit `HeadError` + `BodyAtomError` sealed leaves with `string.Join(...)` message (§2.3) — done in spec
- T4: emit `ClauseDualityError` sealed leaf with `?.ToString() ?? "null"` interpolation (§2.4) — done in spec
- T5: emit `UndefinedProcedureError` + `ArityMismatchClauseError` sealed leaves with invariant-culture int interp (§2.5) — done in spec
- T6: emit `UndeclaredProcedureError : Exception` sealed (§2.6) — done in spec
- T7: emit `TypedClause` sealed class + file-head `using` aliases for `ast.*` (§2.7) — done in spec
- T8: emit `WellTypedClause` static host class (§2.8) — done in spec
- T9: emit `CheckClause` orchestrator static method with `var (headResult, modedHeadTerm) = ...` deconstruct (§2.9) — done in spec
- T10: emit `CheckClauseFromAst` with C# 12 collection-expression spread `[..a, ..b]` and `throw new UndeclaredProcedureError(...)` (§2.10) — done in spec
- T11: emit `GetAcceptedLabels` + `GetLabelsFromTerm` with switch-expression + or-pattern (§2.11) — done in spec
- T12: emit `GetFullTypeName` with switch-expression + `ArgumentException` (§2.12) — done in spec
- T13: emit `CheckHead` + `CheckHeadWithTerm` private statics returning named ValueTuple (§2.13) — done in spec
- T14: emit `CheckBodyAtom` + `CheckBodyAtomWithTerm` with default-named param and is-pattern-capture chain (§2.14) — done in spec
- T15: emit `CheckRemoteGoal` with while-pattern-bind loop and bang preservation (§2.15) — done in spec
- T16: emit `CheckModedTermPerArg` with `is not ...` pattern and `InvalidOperationException` catch (§2.16) — done in spec
- T17: emit `CheckTermDuality` private static (literal-identical to `WellTypedTerm.CheckDuality`) (§2.17) — done in spec
- T18: emit `NormalizeLocation` expression-bodied with `StringComparison.Ordinal` (§2.18) — done in spec
- T19: emit `CheckClauseDuality` with triple-branch dispatch and tuple destructuring (§2.19) — done in spec
- T20: emit `AreDualTypes` + `AreSameTypeWithReason` + `AreDualTypesWithReason` with discard pattern and named tuples (§2.20) — done in spec
- T21: emit `InferConcreteDecl` with mixed positional+named `new ProcDecl(...)` (§2.21) — done in spec
- T22: emit `MatchTypeForInference` with `IDictionary<,>` mutable param and substring off-by-one (§2.22) — done in spec
- T23: emit `SplitTypeArgs` with two `Substring` shapes (length-based + single-arg) (§2.23) — done in spec
- T24: emit `SubstituteTypeParams` with `Cast<TypeRef>()` + literal-preserved fall-through (§2.24) — done in spec

## 4. Research Findings

none required — every Dart→C# decision in this file resolves to a cached `rf-*` finding in the ratified convspec (16 cache hits) or a fresh inline-research finding recorded in the convspec itself (11 fresh constructs, each with Microsoft Learn / dart.dev citations: `rf-dart-record-destructure-to-csharp-tuple-deconstruct`, `rf-dart-iterable-map-join-to-csharp-linq-select-string-join`, `rf-csharp-interpolation-null-vs-dart-null-tostring`, `rf-csharp-int-interp-culture-invariant`, `rf-dart-implements-exception-to-csharp-extends-system-exception`, `rf-dart-collection-spread-to-csharp-collection-expression-spread`, `rf-dart-type-test-chain-to-csharp-switch-expression-or-pattern`, `rf-dart-on-typed-catch-to-csharp-typed-catch`, `rf-dart-argumenterror-to-csharp-argumentexception`, `rf-dart-while-istest-with-cast-to-csharp-while-pattern-bind`, `rf-dart-bang-runtime-throw-vs-csharp-null-forgiving-static-only`, `rf-dart-is-not-test-to-csharp-is-not-pattern`, `rf-dart-optional-named-param-to-csharp-default-named`, `rf-dart-mixed-positional-and-named-ctor-call-to-csharp-mixed-args`, `rf-dart-iterable-cast-to-csharp-enumerable-cast`, `rf-dart-discard-pattern-to-csharp-discard`, `rf-dart-const-empty-list-default-to-csharp-static-empty-array`). FR-024 (no re-research) is honoured.

## 5. Consistency Pass

fixed — derived from .codeconv/conversion-specs/lib/analysis/type_checker/well_typed_clause.dart.md (RATIFIED). Every construct decision mirrors the convspec verbatim. Three nuances explicitly addressed in the convspec (US2 AS4) carried through:

1. **Value vs. reference.** Every emitted class is a reference type. Collections (`IReadOnlyList<>`, `IReadOnlyDictionary<>`) aliased through callers without defensive copies (matches Dart).
2. **Null-safety.** `Map<>?` / `List<>?` / `VariableTypeInfo?` / `ModedTerm?` / `String?` → exact 1:1. Two recorded divergences: (a) Dart `$nullValue` → `"null"` vs C# `$"{x}"` → `""` resolved by mandatory `?.ToString() ?? "null"` in `ClauseDualityError.Message`; (b) Dart `!` runtime-throw vs C# `!` static-only — preserved verbatim because construction-site invariant (`!rg.IsDynamic`) guarantees non-null.
3. **Stream / async / isolate.** N/A — file is pure synchronous.

Cross-file references (consistency with sibling specs):
- `Subtyping.IsSubtype(...)` qualifier matches `subtyping.dart` host-class spec.
- `Mode.Produce` / `Mode.Consume` enum naming matches `mode.dart` spec.
- `WellTypedResult` / `WellTypedError` / `NonDualError` / `InconsistentPathError` / `InconsistentVariableError` shapes match `well_typed_term.dart` spec.
- `ModedTerm` / `ModedCompound` / `ModedPath` / `PathStep` / `paths(...)` mappings match `moded_term.dart` spec.
- `ModedHead(...)` / `ProducedTerm(...)` callsites match `moded_head.dart` spec (named-arg `typeEnv:` parameter preserved).
- `ProgramDFA` / `Automaton` / `DFAState` / `dfa.GetAutomaton(string)` / `dfa.GetState(string)` / `dfa.Automata` references match `program_dfa.dart` spec.
- `ProcDecl` / `TypeExpr` / `TypeRef` / `PrimitiveModeAlt` / `TypeEnvironment.GetProcedure(string, int)` / `TypeEnvironment.HasProcedure(string, int)` / `TypeEnvironment.Procedures` / `TypeEnvironment.ParamProcDecls` mappings match `type_ast.dart` spec — NOTE: per the directive, any `TypeEnvironment.GetType(String)` touch would refer to type_ast.dart E1; this plan does NOT touch `GetType(String)` (it touches `GetProcedure` / `HasProcedure` / `Procedures` / `ParamProcDecls` only).
- `IsBuiltinGoal(...)` callsite matches `prelude.dart` spec.
- `ast.Goal` / `ast.Clause` / `ast.Term` / `ast.VarTerm` / `ast.UnderscoreTerm` / `ast.ConstTerm` / `ast.ListTerm` / `ast.StructTerm` / `ast.SpawnGoal` / `ast.RemoteGoal` references resolved via file-head `using` aliases (cached `rf-dart-import-prefix-as-to-csharp-using-alias`).
- The duplicated `CheckTermDuality` logic preserved literally per FR-023 (no refactor into a shared helper).

## 6. Escalations

None.
