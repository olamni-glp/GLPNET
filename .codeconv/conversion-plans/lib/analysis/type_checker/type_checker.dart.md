---
path: lib/analysis/type_checker/type_checker.dart
cycle_group_id: 18
scc_siblings: []
generated_at: 2026-05-21T16:14:32Z
source_sha256: 1a6728683d8d3b0f7ae0e912eb459829b529ddbd1444a687da1ebb9cd560d28a
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/type_checker.dart

## 1. Source Analysis

The Dart source (672 lines) is the **driver/coordinator** of the
`lib/analysis/type_checker/` family, implementing Definition 4.10 of the
typed-GLP "well-typed program" check (spec
`docs/modules/well-typed-program.md` v0.7). Inventory derived by direct
inspection of the .dart file at sha256
`1a6728683d8d3b0f7ae0e912eb459829b529ddbd1444a687da1ebb9cd560d28a`:

**Imports** (10): `type_ast.dart`, `param_expansion.dart`, `program_dfa.dart`,
`type_environment_builder.dart`, `well_typed_clause.dart as wtc`,
`clause_validation.dart`, `../../compiler/ast.dart as ast`,
`../../compiler/lexer.dart`, `../../compiler/parser.dart`,
`../../compiler/error.dart`.

**Top-level diagnostic value classes** (4):
- `TypeCheckResult` — two-list holder (errors / warnings) + `bool get isWellTyped` + `toString()` using `StringBuffer` + `writeln`.
- `TypeError` — message + line + column + nullable optional `String? clauseText` + `toString()` with conditional clauseText branch.
- `TypeWarning` — message + line + column + expression-bodied `toString()`.
- `CoverageError` — four required-named fields (procedure / argIndex / uncoveredLabel / path) + `toString()` with embedded `"…"` quote pair around the label.

**Main class** `TypeChecker` (lines 110-615) with two final fields
(`typeEnv`, `dfa`) — `dfa` built in the ctor initializer-list from
`buildProgramDFA(typeEnv)`. Public instance method `check(List<ast.Clause>)`
runs a four-phase pipeline: (Phase 0) per-clause AST validation that catches
`CompileError` and projects to `TypeError`; early-return if any errors;
(Phase 1) grouping into `Map<String, List<ast.Clause>>` keyed
`"functor/arity"`; (Phase 2) iterate `typeEnv.procedures.values`, call
`_checkProcedure` for each declared procedure (warning for missing-non-builtin);
(Phase 3) warn on clause-groups with no type declaration.

Private instance methods:
- `_checkProcedure(decl, clauses)` — covariance loop + contravariance loop.
- `_checkClauseCovariance(clause, decl)` — typed-catch cascade
  (`on wtc.UndeclaredProcedureError catch (e)` then bare `catch (e)`).
- `_checkInputCoverage(clauses, decl, argIndex)` — `is PrimitiveModeAlt`
  short-circuit; `as TypeRef` downcast; build `inputTypeName`; try/catch
  around `dfa.getAutomaton`; recurse into `_checkStateCoverage`.
- `_checkStateCoverage(state, …, {List<int> structPath = const []})` —
  four early-return guards (visited, base `_`, isFinal, variable covers all),
  then transition loop, recursing with extended structural path; emits
  `CoverageError` for uncovered alternatives; uses literal U+2192
  RIGHT-ARROW character in interpolated paths.
- `_anyClauseHasVariableAtPath` / `_clauseAcceptsLabelAtPath` — variable-
  detection predicates using `is VarTerm || is UnderscoreTerm`.
- `_navigateToPath(term, structPath)` — typed dispatch over `StructTerm` /
  `ListTerm` with explicit 1-based head=1 / tail=2 GLP semantics.
- `_extractArgIndex(symbol)` — regex `r'\((\d+),(\d+)\)$'` + `int.tryParse`.
- `_getTransitionsFromState(state, automaton)` — filter `automaton.transitions`
  by reference-equality `fromState == state`, destructure record key
  `final (fromState, label) = entry.key;`, key by `label.toString()`.
- `_clauseAcceptsLabel(clauses, argIndex, labelStr)` — UNUSED helper
  retained verbatim (called by no in-file site; kept for source-parity).
- `_labelsMatch(acceptedLabels, labelStr)` — six sequential checks (direct
  contains; `[|](…)` prefix; `[]` equals; `\(…` prefix with arity regex
  branch; raw `\` / `\\` containment; `f(arity,…)` functor-arity regex).
- `_clauseToString(clause)` — diagnostic-summary helper used by error sites.

**Free / top-level functions** (2):
- `checkModule(Module, {transformedProcedures, ancestorScope})` —
  parse-stage orchestrator: build base env → expand parameterized types →
  build typed env → flatten clauses → run `TypeChecker.check`.
- `checkSource(String)` — full pipeline parse-then-check.

**Salient nuances called out by direct inspection:**

1. **`StringBuffer.writeln`** writes a literal `'\n'` (Dart-platform-invariant)
   — `TypeCheckResult.toString()` uses it 3× to assemble diagnostic output.
2. **Reference equality** `fromState == state` in `_getTransitionsFromState`
   uses Dart's default `Object.==` (which is reference identity for classes
   without `==` override; `DFAState` per program_dfa.dart spec does NOT
   override `==`).
3. **U+2192 RIGHT-ARROW** appears as a literal character in the
   `_checkStateCoverage` interpolations (`'$pathPrefix → $label'`) — the
   only non-ASCII codepoint in the file.
4. **The `_clauseAcceptsLabel` private method (lines 537-553) is UNUSED** —
   no in-file caller — but is retained verbatim because the convspec
   §`construct_key`s emit it for source-parity (its lone purpose is to
   document the simpler non-path variant of the path-aware predicate).
5. **`PrimitiveModeAlt` short-circuit** is a wildcard final-state per
   spec v0.7 — coverage check skipped for `_` / `_?`-typed input args.
6. **Dart `clause.body!` bang** after `clause.body == null` check —
   under C# NRT flow analysis the bang vanishes (narrowing is automatic).

## 2. Dart → C#/.NET Conversion Plan

The convspec at `.codeconv/conversion-specs/lib/analysis/type_checker/
type_checker.dart.md` is RATIFIED with **0 escalations**; this plan mirrors
its construct-by-construct directives verbatim (FR-024).

**Namespace + file structure** — `namespace Glp.Analysis.TypeChecker { … }`
in `lib/analysis/type_checker/type_checker.cs`. Same-namespace siblings
auto-visible; cross-namespace types reached via
`using Glp.Compiler;` (for `Lexer`, `Parser`, `CompileError`) and
`using Ast = Glp.Compiler.Ast;` for the prefixed `ast.` references (the
`wtc.` prefix is dropped per the convspec's "no-alias for two call sites"
decision; emit fully-qualified `WellTypedClause.X` at the two call sites).

**TypeCheckResult** → `public sealed class TypeCheckResult` with positional
ctor `public TypeCheckResult(IReadOnlyList<TypeError> errors,
IReadOnlyList<TypeWarning> warnings)`, two read-only auto-properties
(`Errors`, `Warnings`), expression-bodied `public bool IsWellTyped =>
Errors.Count == 0;`, and `public override string ToString()` body using
`var sb = new StringBuilder();` plus `sb.Append(text).Append('\n')`
(NOT `AppendLine` — preserve Dart's platform-invariant `\n` per the
RATIFIED finding `rf-dart-stringbuffer-writeln-to-csharp-stringbuilder-
appendline`). The Dart interpolation `'  $e'` maps to `$"  {e}"`.

**TypeError** → `public sealed class TypeError` (NOT `: Exception` — a
diagnostic value object aggregated into `TypeCheckResult.Errors`, never
thrown). Positional ctor `public TypeError(string message, int line, int
column, string? clauseText = null)` + four read-only auto-properties.
`public override string ToString()` body: `var loc = $"line
{Line.ToString(CultureInfo.InvariantCulture)}, column
{Column.ToString(CultureInfo.InvariantCulture)}"; return ClauseText is not
null ? $"{Message} at {loc}\n    in: {ClauseText}" : $"{Message} at
{loc}";`. InvariantCulture on every int interpolation hole per cached
`rf-csharp-int-interp-culture-invariant`.

**TypeWarning** → `public sealed class TypeWarning` with positional ctor
+ three read-only auto-properties + expression-bodied
`public override string ToString() => $"{Message} at line
{Line.ToString(CultureInfo.InvariantCulture)}, column
{Column.ToString(CultureInfo.InvariantCulture)}";`. NOT a `record` (Dart
source declares no `==`/`hashCode` override).

**CoverageError** → `public sealed class CoverageError` with primary ctor
`public CoverageError(string procedure, int argIndex, string uncoveredLabel,
string path)` + four read-only auto-properties. Call sites use C#
named-argument syntax `new CoverageError(procedure: decl.Name, argIndex:
argIndex, uncoveredLabel: label, path: …)`. Expression-bodied
`public override string ToString() => $"{Procedure} argument
{ArgIndex.ToString(CultureInfo.InvariantCulture)}: uncovered alternative
\"{UncoveredLabel}\" at path: {Path}";` (escape `\"` for embedded
double-quotes per Microsoft Learn interpolated-string-escape rules).

**TypeChecker** → `public sealed class TypeChecker` with two `readonly`
fields (`TypeEnvironment TypeEnv`, `ProgramDfa Dfa`) and primary ctor
`public TypeChecker(TypeEnvironment typeEnv) { TypeEnv = typeEnv; Dfa =
ProgramDfa.BuildProgramDfa(typeEnv); }`. The Dart initializer-list `:
dfa = …` maps to C# ctor-body assignment (no analog of the `:` syntax in
C#). Eager DFA build preserved (NOT `Lazy<ProgramDfa>`).

**Check(IReadOnlyList<Clause>)** → public instance method preserving the
four-phase structure 1:1:
- Phase 0: `foreach (var clause in clauses) try { foreach (var arg in
  clause.Head.Args) ClauseValidation.ValidateClauseHead(arg); … } catch
  (CompileError e) { errors.Add(new TypeError(e.Message, e.Line, e.Column,
  ClauseToString(clause))); }`. The Dart `try { … } on CompileError catch`
  maps to a C# typed `catch (CompileError e)` per cached
  `dart-on-typed-catch-to-csharp-typed-catch`. The `clause.Guards is not
  null` check narrows the property to non-null via NRT flow analysis (no
  bang needed).
- Phase 0-finish: `if (errors.Count > 0) return new TypeCheckResult(errors,
  warnings);` — preserves Dart's early-return short-circuit.
- Phase 1: `var procedureClauses = new Dictionary<string, List<Clause>>(
  StringComparer.Ordinal);` then `foreach (var clause in clauses)` build key
  `$"{clause.Head.Functor}/{clause.Head.Arity.ToString(CultureInfo.
  InvariantCulture)}"` and use `Dictionary.TryGetValue + new List + Add`
  (cached `dart-map-putifabsent-to-csharp-trygetvalue-or-add`) to mirror
  Dart's `putIfAbsent`.
- Phase 2: `foreach (var procDecl in TypeEnv.Procedures.Values) { … }` —
  if no clauses + `!procDecl.IsBuiltin` → add warning; else dispatch to
  `CheckProcedure` and AddRange both error/warning lists. `Dictionary.
  Values` collection iteration order matches Dart `Map.values` (cached
  `rf-dart-map-iteration-order-to-csharp-dictionary`).
- Phase 3: `foreach (var entry in procedureClauses) if (!TypeEnv.
  Procedures.ContainsKey(entry.Key)) warnings.Add(new TypeWarning(…));`.

**CheckProcedure(ProcDecl, IReadOnlyList<Clause>)** → `private` instance
method. Covariance: `foreach (var clause in clauses) errors.AddRange(
CheckClauseCovariance(clause, decl));`. Contravariance: `for (int argIndex
= 1; argIndex <= decl.Arity; argIndex++) if (decl.IsInputArg(argIndex - 1))
errors.AddRange(CheckInputCoverage(clauses, decl, argIndex));`. The 1-based
loop bound + `-1` adjustment for `IsInputArg`'s 0-based index is preserved
verbatim (the convspec mandates "do NOT renumber").

**CheckClauseCovariance(Clause, ProcDecl)** → `private List<TypeError>`
instance method with the typed-catch cascade:
```
try { var result = WellTypedClause.CheckClauseFromAst(clause, Dfa, TypeEnv);
      if (!result.IsWellTyped) foreach (var error in result.Errors)
          errors.Add(new TypeError(error.Message, clause.Line, clause.Column,
                                   ClauseToString(clause))); }
catch (UndeclaredProcedureError e) { errors.Add(new TypeError(
      $"Undeclared procedure: {e.Functor}/{e.Arity.ToString(
          CultureInfo.InvariantCulture)}", clause.Line, clause.Column,
      ClauseToString(clause))); }
catch (Exception e) { errors.Add(new TypeError(
      $"Error checking clause: {e.Message}", clause.Line, clause.Column,
      ClauseToString(clause))); }
```
Bare Dart `catch (e)` → `catch (Exception e)` with `{e.Message}` (NOT
`{e}`, which would include the stack trace via `Exception.ToString()` —
convspec finding `rf-dart-general-catch-to-csharp-catch-exception-with-
tostring`).

**CheckInputCoverage(IReadOnlyList<Clause>, ProcDecl, int)** → `private
List<TypeError>` instance method. Body preserves the four-step structure:
(a) `var argType = decl.ArgTypes[argIndex - 1];`
(b) `if (argType is PrimitiveModeAlt) return new List<TypeError>();` —
    type-pattern positive-test short-circuit.
(c) `var typeRef = (TypeRef)argType;` — explicit downcast (the negative
    check on `PrimitiveModeAlt` does NOT auto-narrow under NRT flow
    analysis; cached `dart-as-downcast-to-csharp-explicit-cast`).
    `var inputTypeName = typeRef.IsInput ? $"{typeRef.Name}?" :
    typeRef.Name;` — trailing `?` is GLP-syntactic, NOT C#-nullable-marker;
    preserve as literal char.
(d) `Automaton inputAutomaton; try { inputAutomaton = Dfa.GetAutomaton(
    inputTypeName); } catch (Exception e) { errors.Add(new TypeError(
    $"Cannot get automaton for type {inputTypeName}: {e.Message}",
    decl.Line, decl.Column)); return errors; }`.
(e) `var visited = new HashSet<string>(StringComparer.Ordinal);` then
    recurse into `CheckStateCoverage(...)`; project each returned
    `CoverageError` via `coverageError.ToString()` into a `TypeError`.

**CheckStateCoverage(DfaState, IReadOnlyList<Clause>, int, string,
HashSet<string>, Automaton, ProcDecl, IReadOnlyList<int>? structPath =
null)** → `private List<CoverageError>` instance method. The Dart
optional-named `{List<int> structPath = const []}` maps to nullable
`IReadOnlyList<int>? structPath = null` with body `var path = structPath
?? Array.Empty<int>();` per cached `dart-const-empty-list-default-to-
csharp-static-empty-array`. Body preserves the four early-return guards
verbatim, then loops over `transitions` (returned by
`GetTransitionsFromState`). Recursive call uses
`new List<int>(path) { argIdxFromLabel.Value }` for the extended
struct-path (chosen over `.Append(...).ToList()` for diff-readability).
The interpolation `$"{pathPrefix} → {label}"` preserves the literal
U+2192 RIGHT-ARROW codepoint verbatim (cached
`rf-dart-unicode-string-literal-to-csharp-unicode-string-literal` + the
emitted C# source file MUST be UTF-8-BOM-encoded per cached
`rf-csharp-source-utf8-bom-for-unicode-literals`).

**AnyClauseHasVariableAtPath(IReadOnlyList<Clause>, int,
IReadOnlyList<int>)** → `private bool` instance method. Body uses C# 9
type-pattern combinator `is VarTerm or UnderscoreTerm` (cached
`rf-csharp-type-pattern-or-combinator`) to collapse the two Dart `is`
tests joined by `||`. The `argIndex > clause.Head.Args.Count` skip-guard
preserves Dart's `continue` semantics; `IList<T>.Count` ↔ Dart `List.length`
per cached `dart-list-length-to-csharp-list-count`.

**ClauseAcceptsLabelAtPath(IReadOnlyList<Clause>, int,
IReadOnlyList<int>, string)** → `private bool` instance method. Body
preserves all four early-exit branches in order. `termAtPath is null
continue;` (NOT `== null` — cached `rf-csharp-is-null-vs-equals-null`).
The `WellTypedClause.GetLabelsFromTerm(termAtPath)` cross-module call
returns `IReadOnlySet<string>?`; `null` sentinel preserves Dart's
"wildcard accepts anything" semantics. Final `return LabelsMatch(labels,
labelStr);`.

**NavigateToPath(Term, IReadOnlyList<int>)** → `private static Term?`
method (touches no instance state). Body uses a type-pattern `switch`
with guards (cached `rf-dart-if-else-if-typed-dispatch-to-csharp-switch-
with-when`):
```
foreach (var idx in structPath) {
    switch (current) {
        case null: return null;
        case StructTerm s when idx >= 1 && idx <= s.Args.Count:
            current = s.Args[idx - 1]; break;
        case StructTerm: return null;
        case ListTerm l when !l.IsNil && idx == 1: current = l.Head; break;
        case ListTerm l when !l.IsNil && idx == 2: current = l.Tail; break;
        default: return null;
    }
}
return current;
```
1-based head/tail indexing is GLP-semantic — preserve verbatim, do NOT
zero-base.

**ExtractArgIndex(string)** → `private static int?` method backed by a
`private static readonly Regex ArgIndexRegex = new(@"\((\d+),(\d+)\)$",
RegexOptions.Compiled | RegexOptions.CultureInvariant);`. Body: `var
match = ArgIndexRegex.Match(symbol); if (match.Success && int.TryParse(
match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture,
out var idx)) return idx; return null;`. Four-arg `int.TryParse` overload
mandated (cached `rf-csharp-int-parse-invariant-culture`) to match Dart's
culture-invariant `int.tryParse`.

**GetTransitionsFromState(DfaState, Automaton)** → `private static
Dictionary<string, DfaState>` method. Body: `var result = new
Dictionary<string, DfaState>(StringComparer.Ordinal); foreach (var entry
in automaton.Transitions) { var (fromState, label) = entry.Key; if
(ReferenceEquals(fromState, state)) result[label.ToString()!] =
entry.Value; } return result;`. Tuple-deconstruction over the record key
(cached `dart-record-destructure-to-csharp-tuple-deconstruct`).
`ReferenceEquals` pins reference-identity comparison (cached
`rf-dart-object-eq-default-to-csharp-referenceequals` — survives future
`==` overload changes on `DfaState`). The `label.ToString()!` bang maps
to C# `!` forgiveness per cached `dart-bang-assert-to-csharp-null-
forgiveness`.

**ClauseAcceptsLabel(IReadOnlyList<Clause>, int, string)** → `private`
instance method retained verbatim (matches Dart source). NOT called by
any in-file site — preserved for source-parity per convspec.

**LabelsMatch(IReadOnlySet<string>, string)** → `private static bool`
method. Six sequential checks preserved in order (the Dart `if` cascade
is NOT mutually exclusive, NOT a switch). All `String.StartsWith`
comparisons take `StringComparison.Ordinal`; `string.Equals` for
`"[]"` takes `StringComparison.Ordinal`. Two further `private static
readonly Regex` fields: `DiffArityRegex = new(@"\\\((\d+),", …)` and
`FunctorArityRegex = new(@"(\w+)\((\d+),", …)`. Raw `\` containment
preserves Dart `r'\'` semantics via C# verbatim `@"\"`.

**ClauseToString(Clause)** → `private static string` method. Body: `var
head = $"{clause.Head.Functor}({clause.Head.Args.Count.ToString(
CultureInfo.InvariantCulture)} args)"; if (clause.Body is null ||
clause.Body.Count == 0) return $"{head}."; return $"{head} :-
{clause.Body.Count.ToString(CultureInfo.InvariantCulture)} goals.";`.
The Dart `clause.body!` after the null-check vanishes under NRT flow
narrowing.

**TypeCheckerDriver static class** (separate from the `TypeChecker`
instance class to avoid name collision) hosts the two free orchestrator
functions:
- `public static TypeCheckResult CheckModule(Module module,
  IReadOnlyList<Procedure>? transformedProcedures = null,
  TypeEnvironment? ancestorScope = null)` — `??` null-coalesce for both
  defaults; `baseEnv.Types.Keys.ToHashSet(StringComparer.Ordinal)` for
  the known-type-names set passed to `ExpandParameterizedTypes`. Eager
  `var clauses = new List<Clause>(); foreach (var proc in procedures)
  clauses.AddRange(proc.Clauses);` then `new TypeChecker(typeEnv).
  Check(clauses);`.
- `public static TypeCheckResult CheckSource(string source)` — `new
  Lexer(source) + Tokenize() + new Parser(tokens) + ParseModule() +
  CheckModule(module)`. Sync, NOT async (cached
  `rf-dart-sync-pipeline-to-csharp-sync-not-async`).

**TypeEnvironment.getType cross-file shadowing.** This file does NOT
call `TypeEnvironment.getType(String)`; the `TypeEnv` references in
`Check`/`CheckProcedure`/etc. are limited to `.Procedures` (a
dictionary view) and `.Types` (via the driver only). The cross-file
decision recorded in `type_ast.dart E1` regarding `object.GetType`
shadowing is therefore NOT triggered by this conversion — preserve the
decision verbatim where applicable in upstream `type_ast.cs`, no local
override required.

**XML-doc port** — every Dart `///` doc-comment ports verbatim to C#
`/// <summary>…</summary>` blocks, preserving the GLP-spec-v0.7 anchors
(Definition 4.10 line 351-357 reference; `clause-validation.md`
reference; `well-typed-program.md` reference; the in-line phase
comments such as "Wildcard types are FINAL STATES requiring NO coverage
checking" preserved verbatim).

## 3. Decomposed Task Units

T1: Emit `namespace Glp.Analysis.TypeChecker` + `using` directives (`Glp.Compiler`, `Ast = Glp.Compiler.Ast`, `System.Collections.Generic`, `System.Globalization`, `System.Text`, `System.Text.RegularExpressions`).
T2: Emit `public sealed class TypeCheckResult` with positional ctor, two `IReadOnlyList<…>` auto-properties, `IsWellTyped` expression-bodied bool property, and `ToString()` using `StringBuilder.Append(text).Append('\n')`.
T3: Emit `public sealed class TypeError` with positional ctor (optional `string? clauseText = null`), four read-only auto-properties, `ToString()` with two-branch interpolation + InvariantCulture int formatting.
T4: Emit `public sealed class TypeWarning` with positional ctor, three read-only auto-properties, expression-bodied `ToString()` + InvariantCulture int formatting.
T5: Emit `public sealed class CoverageError` with primary ctor (named-arg-friendly positionals), four read-only auto-properties, `ToString()` with `\"…\"` escaped quotes + InvariantCulture int formatting.
T6: Emit `public sealed class TypeChecker` with two `readonly` instance fields and the primary ctor that assigns `TypeEnv` and eagerly builds `Dfa` via `ProgramDfa.BuildProgramDfa(typeEnv)`.
T7: Emit `public TypeCheckResult Check(IReadOnlyList<Clause> clauses)` with the four-phase body (Phase 0 validation try/catch CompileError + early-return; Phase 1 ordinal Dictionary grouping; Phase 2 typeEnv-driven check + undefined-warning; Phase 3 undeclared-warning).
T8: Emit `private TypeCheckResult CheckProcedure(ProcDecl decl, IReadOnlyList<Clause> clauses)` — covariance foreach + 1-based contravariance for-loop with `IsInputArg(argIndex - 1)` gate.
T9: Emit `private List<TypeError> CheckClauseCovariance(Clause, ProcDecl)` with the typed-catch cascade (`UndeclaredProcedureError` → `Exception`) and `{e.Message}` interpolation.
T10: Emit `private List<TypeError> CheckInputCoverage(IReadOnlyList<Clause>, ProcDecl, int)` with `is PrimitiveModeAlt` short-circuit, explicit `(TypeRef)` cast, `inputTypeName` ternary, try/catch around `Dfa.GetAutomaton`, and recursion driver into `CheckStateCoverage`.
T11: Emit `private List<CoverageError> CheckStateCoverage(…)` with `IReadOnlyList<int>? structPath = null` default, four early-return guards, transition loop with `new List<int>(path) { argIdxFromLabel.Value }` extended path and U+2192 literal in interpolations.
T12: Emit `private bool AnyClauseHasVariableAtPath(IReadOnlyList<Clause>, int, IReadOnlyList<int>)` using C# 9 `is VarTerm or UnderscoreTerm` combinator.
T13: Emit `private bool ClauseAcceptsLabelAtPath(IReadOnlyList<Clause>, int, IReadOnlyList<int>, string)` with `is null continue;` + `WellTypedClause.GetLabelsFromTerm` + null-sentinel-as-wildcard semantics.
T14: Emit `private static Term? NavigateToPath(Term, IReadOnlyList<int>)` with type-pattern `switch` (StructTerm-with-guard + bare StructTerm; ListTerm-head and ListTerm-tail arms).
T15: Emit `private static int? ExtractArgIndex(string)` + `private static readonly Regex ArgIndexRegex = new(@"\((\d+),(\d+)\)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);` + four-arg `int.TryParse` with InvariantCulture.
T16: Emit `private static Dictionary<string, DfaState> GetTransitionsFromState(DfaState, Automaton)` with ordinal Dictionary + tuple destructure + `ReferenceEquals` reference-identity test + `label.ToString()!` bang.
T17: Emit `private bool ClauseAcceptsLabel(IReadOnlyList<Clause>, int, string)` UNUSED-but-retained helper preserved verbatim for source-parity.
T18: Emit `private static bool LabelsMatch(IReadOnlySet<string>, string)` with six sequential ordinal-comparison checks; emit the two `private static readonly Regex DiffArityRegex` + `FunctorArityRegex` fields.
T19: Emit `private static string ClauseToString(Clause)` with InvariantCulture int interpolation; null-check on `clause.Body` narrows via NRT.
T20: Emit `public static class TypeCheckerDriver` hosting the two free functions.
T21: Emit `public static TypeCheckResult CheckModule(Module, IReadOnlyList<Procedure>? = null, TypeEnvironment? = null)` with `??` null-coalesce + `ToHashSet(StringComparer.Ordinal)` + cross-module driver calls.
T22: Emit `public static TypeCheckResult CheckSource(string)` — five-line sync pipeline.
T23: Port all Dart `///` doc-comments verbatim into C# `/// <summary>…</summary>` blocks, including the GLP-spec-v0.7 anchors (Definition 4.10, clause-validation.md, well-typed-program.md cite-text).
T24: Ensure the emitted C# source file is UTF-8-BOM-encoded so the literal U+2192 codepoint round-trips through MSBuild correctly.

## 4. Research Findings

none required — convspec is RATIFIED with 0 escalations; every construct
either reuses a cached idiom (FR-024) or carries an authoritative-Microsoft-
Learn / dart.dev finding already merged into the conversion-idiom KB by
the nine prior sibling specs in this directory. Specifically the convspec
explicitly enumerates 17/21 cached-idiom reuses plus the following fresh
findings already absorbed into the KB:
`rf-dart-stringbuffer-writeln-to-csharp-stringbuilder-appendline`,
`rf-csharp-type-pattern-or-combinator`,
`rf-csharp-int-parse-invariant-culture`,
`rf-dart-unicode-string-literal-to-csharp-unicode-string-literal`,
`rf-dart-object-eq-default-to-csharp-referenceequals`,
`rf-csharp-is-null-vs-equals-null`,
`rf-dart-general-catch-to-csharp-catch-exception-with-tostring`,
`rf-dart-sync-pipeline-to-csharp-sync-not-async`,
`rf-csharp-flow-analysis-narrows-on-isnotnull`.

## 5. Consistency Pass

fixed — derived from the RATIFIED convspec at
`.codeconv/conversion-specs/lib/analysis/type_checker/type_checker.dart.md`
(21 constructs, 0 escalations) cross-referenced against the nine sibling
RATIFIED convspecs in `lib/analysis/type_checker/` (`type_ast.dart`,
`moded_term.dart`, `well_typed_term.dart`, `prelude.dart`,
`param_expansion.dart`, `program_dfa.dart`, `clause_validation.dart`,
`well_typed_clause.dart`, `type_environment_builder.dart`).

Specifically verified:
- `TypeEnvironment.getType` cross-file shadowing decision in
  `type_ast.dart E1` is NOT triggered by this conversion (this file only
  references `.Procedures` / `.Types` views, never `.getType(String)`).
- `CompileError` retained verbatim per project policy (CLAUDE.md
  appendix; closure of escalation #1 in commit `e3abe921`).
- `Channel<T>` / isolate-mailbox primitives not used here (no
  concurrency surface in the type-checker; runs single-threaded by
  invariant).
- `IReadOnlyList<T>` / `IReadOnlySet<T>` / `IReadOnlyDictionary<…>`
  surface choices match the nine sibling specs' precedents.
- `StringComparer.Ordinal` discipline applied to every `Dictionary<string,
  …>` / `HashSet<string>` constructed in this file (4 sites).
- `InvariantCulture` discipline applied to every int interpolation hole
  and to every `int.TryParse` call.
- Literal U+2192 codepoint preserved + UTF-8-BOM file encoding mandated.

## 6. Escalations

None.
