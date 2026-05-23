---
path: lib/analysis/type_checker/well_typed_term.dart
cycle_group_id: 16
scc_siblings: []
generated_at: 2026-05-21T15:00:21Z
source_sha256: 66cb54044610eb389ff23edc327067588022b814dd99a51a5e100e6515d9442f
schema_version: 1
---

# Conversion Plan: lib/analysis/type_checker/well_typed_term.dart

## 1. Source Analysis

The Dart file (`well_typed_term.dart`, 470 lines) is the GLP type-checker's
*well-typing decision procedure* (paper Definition 5.4 / 4.5). It imports
three sibling files (`mode.dart`, `moded_term.dart`, `program_dfa.dart`)
and ships:

**Result/error value classes (7 total, one abstract):**
- `WellTypedResult` (lines 19–52) — three-field result with two `factory`
  constructors (`success`, `failure`); `{required …}` named-required ctor;
  NO `==`/`hashCode` override.
- `VariableTypeInfo` (lines 55–83) — three-field value object with
  manual `toString()`, `operator ==`, and `hashCode` override; named-
  required ctor. The `toString()` interpolation uses the Unicode glyphs
  `↓` (consume) and `↑` (produce).
- `WellTypedError` (lines 86–88) — abstract base with one abstract `String
  get message` getter.
- `InconsistentPathError` (lines 91–102) — positional-ctor leaf carrying
  `ModedPath path` + `String reason`; overrides `message` (interpolated
  with `\n` escape) and `toString`.
- `InconsistentVariableError` (lines 105–119) — positional-ctor leaf
  with `String variableName` + two `VariableTypeInfo` occurrences;
  `message` interpolates them.
- `NonDualError` (lines 122–138) — positional-ctor leaf with `String
  baseName`, two nullable `VariableTypeInfo?` (writerType, readerType),
  and an optional positional `[String? reason]`. `message` uses an
  inline ternary `reason != null ? ': $reason' : ''`.
- `PathCheckResult` (lines 141–165) — three-field ephemeral result with
  two `factory` ctors (`consistent`, `inconsistent`); `consistent` takes
  an optional positional `[VariableTypeInfo? assignment]`. NO equality
  override.

**Public top-level functions (2):**
- `checkModedTerm(ModedTerm, Automaton, ProgramDFA): WellTypedResult`
  (lines 181–219) — extracts term paths, checks each against the
  automaton, records variable type assignments, detects inconsistent
  same-variable typings, then runs `_checkDuality` and returns a
  `WellTypedResult`. Builds two local mutable collections (`<WellTypedError>[]`
  and `<String, VariableTypeInfo>{}`); calls `paths(term)`,
  `checkPathAgainstAutomaton`, `_variableKey`, `_checkDuality`,
  `errors.addAll`.
- `checkPathAgainstAutomaton(ModedPath, Automaton, ProgramDFA):
  PathCheckResult` (lines 228–295) — walks the path's steps, builds
  transition labels, follows the DFA, switches automata at type
  boundaries (Fix 4.1, via `dfa.getAutomaton(...)` inside a `try/catch
  (e)`), handles wildcards (Definition 4.5 v0.7 wildcard-accepts-subterm
  case), and falls through to leaf consistency. Uses `var state =
  automaton.startState`, `var currentAutomaton = automaton` (mutable
  locals reassigned in the loop), C-style `for (int i = 0; ...)` loop.

**Private helpers (5):**
- `_buildTransitionLabel(PathStep, PathStep): TransitionLabel` (lines
  302–315) — splits `currentStep.symbol` on `'/'`, parses the arity via
  `int.tryParse(...) ?? 0`, constructs `TransitionLabel.functor(...)`.
- `_checkLeafConsistencyForPath(PathStep, DFAState, ProgramDFA):
  PathCheckResult` (lines 318–342) — delegates to
  `checkLeafConsistency` (from `program_dfa.dart`), then on consistent
  results either builds a `VariableTypeInfo` (variable leaf) or returns
  bare consistent (constant leaf); uses Dart `??` for null fallback.
- `_pathStepToLeafTerm(PathStep): LeafTerm` (lines 347–382) — type-
  classifies the symbol: variable→reader/writer, integer (`int.tryParse`),
  real (`double.tryParse`), quoted string (`startsWith/endsWith` for
  `'…'` or `"…"`, then `substring(1, value.length - 1)`), else atom-as-
  string. Constructs via `LeafTerm.reader/writer/integerConstant/
  realConstant/stringConstant`.
- `_variableKey(PathStep): String` (lines 385–388) — returns `leaf.symbol`
  verbatim (trivial one-liner).
- `_checkDuality(Map<String, VariableTypeInfo>): List<NonDualError>`
  (lines 392–470) — groups variable keys by their base name (stripping
  any trailing `?` via `varKey.substring(0, varKey.length - 1)`) into a
  `Map<String, Map<String, VariableTypeInfo>>` using `putIfAbsent`. For
  each base name where both writer and reader are present, runs five
  pairwise checks (writer mode, reader mode, wildcard-universal case,
  same base name, opposite isDual) and accumulates `NonDualError`
  entries (`continue` after each violation).

**File-level character.** Pure procedural type-check code with mutable
local collections; no I/O, no async, no isolates. All public types are
either reference values (heap-allocated classes) or aliased into the
returned `WellTypedResult`.

## 2. Dart → C#/.NET Conversion Plan

The convspec lists thirteen constructs; the C# emission mirrors them
verbatim. Each construct's target_decision is restated here in its
canonical compact form; full rationale lives in the convspec sections
referenced by the rf-* idiom IDs.

**C1. `WellTypedResult`** (convspec construct
`dart.value_class.named_required_ctor_three_field_result_type`) →
`public sealed class WellTypedResult` with three read-only auto-properties
`IsWellTyped` (bool), `VariableTypes` (`IReadOnlyDictionary<string,
VariableTypeInfo>`), `Errors` (`IReadOnlyList<WellTypedError>`); single
positional ctor consumed via C# named-argument call style; two static
factories `Success(IReadOnlyDictionary<string, VariableTypeInfo>
variableTypes)` and `Failure(IReadOnlyList<WellTypedError> errors,
IReadOnlyDictionary<string, VariableTypeInfo>? variableTypes = null)`
mapping Dart `factory WellTypedResult.success` / `.failure`. The Dart
`?? {}` default for the optional dict becomes `?? new Dictionary<string,
VariableTypeInfo>()`. NO equality override (Dart source omits it). NOT a
`record` (per convspec §rf-dart-value-class-manual-eq-to-csharp-iequatable-
objectequals reasoning — preserve source's choice not to define
equality). Empty literals `[]` / `{}` map to `Array.Empty<WellTypedError>()`
/ `new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal)`.

**C2. `VariableTypeInfo`** (convspec
`dart.value_class.three_field_with_eq_and_hashcode`) → `public sealed
class VariableTypeInfo : IEquatable<VariableTypeInfo>` with read-only
auto-properties `TypeState` (DFAState), `Mode` (Mode), `IsReader` (bool);
single ctor with named-arg call style; hand-written `Equals(VariableTypeInfo?)`,
`Equals(object?)`, `GetHashCode()` via `HashCode.Combine(TypeState, Mode,
IsReader)`, `==` and `!=` operators mirroring the Dart three-field
equality verbatim. `ToString()` override: `$"({TypeState.Name}, {(Mode
== Mode.Consume ? "↓" : "↑")})"` — parentheses around the ternary inside
the interpolation hole are required (convspec §rf-dart-string-interp-
unicode-to-csharp-interpolated-string-utf8 nuance). Unicode glyphs
preserved verbatim; UTF-8 source mandated.

**C3. `WellTypedError`** (convspec
`dart.abstract_pure_contract_base_for_error_hierarchy`) → `public
abstract class WellTypedError` declaring `public abstract string
Message { get; }`. Explicitly NOT an `interface` (convspec §rf-dart-
abstract-class-pure-contract-to-csharp-interface — opposite conclusion
applied here: the hierarchy is an error/exception ADT with open
extension model permitting shared default behaviour, not a pure
structural contract). NOT `sealed` (open subclassing).

**C4. `InconsistentPathError`** (convspec
`dart.value_class.error_subtype_with_message_getter_override`) →
`public sealed class InconsistentPathError : WellTypedError` with
read-only auto-properties `Path` (ModedPath), `Reason` (string);
positional ctor `(ModedPath path, string reason)`; `public override
string Message => $"Inconsistent path: {Reason}\n  Path: {Path}";`
(verbatim character-for-character mapping of Dart `'Inconsistent path:
$reason\n  Path: $path'`); `public override string ToString() => Message;`.

**C5. `InconsistentVariableError`** (same convspec construct as C4) →
`public sealed class InconsistentVariableError : WellTypedError` with
`VariableName` (string), `FirstOccurrence` (VariableTypeInfo),
`SecondOccurrence` (VariableTypeInfo); positional ctor; `Message =>
$"Variable {VariableName} has inconsistent types: {FirstOccurrence} vs
{SecondOccurrence}";` (the interpolated `VariableTypeInfo` calls the
overridden `ToString` from C2). `ToString() => Message;`.

**C6. `NonDualError`** (same convspec construct as C4 + C5) → `public
sealed class NonDualError : WellTypedError` with `BaseName` (string),
`WriterType` (`VariableTypeInfo?`), `ReaderType` (`VariableTypeInfo?`),
`Reason` (`string?`); positional ctor `(string baseName,
VariableTypeInfo? writerType, VariableTypeInfo? readerType, string?
reason = null)` — the optional positional Dart `[String? reason]` maps
to a C# default-valued positional parameter. `Message` getter emits:
```
public override string Message
{
    get
    {
        var reasonStr = Reason != null ? $": {Reason}" : "";
        return $"Variable pair ({BaseName}, {BaseName}?) not dual{reasonStr}: writer={WriterType}, reader={ReaderType}";
    }
}
```
mirroring the Dart `final reasonStr = reason != null ? ': $reason' : '';
return 'Variable pair ($baseName, $baseName?) not dual$reasonStr:
writer=$writerType, reader=$readerType';` verbatim. `ToString() =>
Message;`.

**C7. `PathCheckResult`** (convspec
`dart.value_class.optional_field_factory_result_carrier`) → `public
sealed class PathCheckResult` with read-only auto-properties
`IsConsistent` (bool), `Reason` (`string?`), `VariableAssignment`
(`VariableTypeInfo?`); single ctor via named-arg call style; two static
factories `public static PathCheckResult Consistent(VariableTypeInfo?
assignment = null)` and `public static PathCheckResult
Inconsistent(string reason)`. NO equality override (Dart omits it,
ephemeral return vehicle). NOT a record.

**C8. `WellTypedTerm` host static class — `checkModedTerm`** (convspec
`dart.toplevel_function.public_orchestrator_with_local_mutable_collections`)
→ `public static class WellTypedTerm` carrying `public static
WellTypedResult CheckModedTerm(ModedTerm term, Automaton automaton,
ProgramDFA dfa)`. Locals: `var errors = new List<WellTypedError>();`,
`var variableTypes = new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal);`.
`final termPaths = paths(term);` → `var termPaths = ModedTerm.Paths(term);`
(or wherever the `paths` top-level function is hosted in moded_term.cs).
`for (final path in termPaths)` → `foreach (var path in termPaths)`.
`!result.isConsistent` → `!result.IsConsistent`. `variableTypes.containsKey(varKey)`
→ `variableTypes.ContainsKey(varKey)`. `variableTypes[varKey]!` →
`variableTypes[varKey]` (C# flow analysis after `ContainsKey` narrows
the indexer return for non-nullable value-type entries; the indexer's
KeyNotFoundException matches Dart's `Map[k]!` null-then-throw at the
runtime contract). `errors.addAll(dualityErrors)` →
`errors.AddRange(dualityErrors)`. Final `return new WellTypedResult(
isWellTyped: errors.Count == 0, variableTypes: variableTypes,
errors: errors);`. The Dart `errors.isEmpty` getter → C# `errors.Count
== 0`.

**C9. `CheckPathAgainstAutomaton`** (convspec
`dart.toplevel_function.public_path_traversal_with_mutable_state`) →
`public static PathCheckResult CheckPathAgainstAutomaton(ModedPath
path, Automaton automaton, ProgramDFA dfa)` on the host static class.
Mutable locals: `var state = automaton.StartState;`, `var
currentAutomaton = automaton;`. `if (path.length == 1)` → `if
(path.Length == 1)`. C-style `for (int i = 0; i < path.Length - 1;
i++)` is identical syntax. `currentAutomaton.transition(state, label)`
→ `currentAutomaton.Transition(state, label)` returning `DFAState?`;
the `if (nextState == null) { ... return ...; }` arm narrows
`nextState` to non-null in subsequent code under C# nullable flow
analysis. The `try { currentAutomaton = dfa.getAutomaton(nextState.name);
} catch (e) { return PathCheckResult.inconsistent(...); }` maps to:
```
try { currentAutomaton = dfa.GetAutomaton(nextState.Name); }
catch (Exception) { return PathCheckResult.Inconsistent($"Cannot get automaton for type {nextState.Name}"); }
```
Dart unused `e` → C# `catch (Exception)` (no binding). Underlying
exception `StateError` → `InvalidOperationException` per program_dfa
spec, which is a subclass of `Exception` (the catch sees it).

**C10. Wildcard short-circuit branch** (convspec
`dart.boolean_conditional_branch_on_property_chain`) → transliterated
verbatim inside `CheckPathAgainstAutomaton`'s `if (nextState == null)`
arm. `state.isWildcard` → `state.IsWildcard`; ternary `state.isDual ?
Mode.consume : Mode.produce` → `state.IsDual ? Mode.Consume :
Mode.Produce`; `mode == Mode.consume` → `mode == Mode.Consume` (enum
value equality). The embedded interpolation ternaries require
parentheses: `$"Mode mismatch at wildcard {state.Name}: expected
{(expectedMode == Mode.Consume ? "↓" : "↑")}, got
{(structuralModeAtWildcard == Mode.Consume ? "↓" : "↑")}"`. Unicode
glyphs `↓`/`↑` preserved verbatim.

**C11. `BuildTransitionLabel` private helper** (convspec
`dart.private_helper_fn.string_split_with_int_parse_fallback`) →
`private static TransitionLabel BuildTransitionLabel(PathStep
currentStep, PathStep nextStep)`. `currentStep.symbol.split('/')` →
`currentStep.Symbol.Split('/')` (returns `string[]`); `.length != 2` →
`.Length != 2`. The Dart `int.tryParse(parts[1]) ?? 0` becomes
`int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture,
out var arity) ? arity : 0` (invariant-culture parsing mandated per
convspec §rf-dart-string-substring-end-exclusive-... nuance 6 — match
Dart's invariant-by-default semantics). Final return: `TransitionLabel.Functor(
functor, arity, nextStep.ArgIndex, mode: nextStep.Mode)` (named-arg
syntax for `mode` parameter).

**C12. `CheckLeafConsistencyForPath` private helper** (convspec
`dart.private_helper_fn.delegation_with_branch_on_polymorphic_field`)
→ `private static PathCheckResult CheckLeafConsistencyForPath(PathStep
leaf, DFAState state, ProgramDFA dfa)`. `result.type ?? state` →
`result.Type ?? state` (`??` token-for-token identical). `result.reason
?? 'Leaf inconsistent'` → `result.Reason ?? "Leaf inconsistent"`. The
`new VariableTypeInfo(typeState: result.Type ?? state, mode: mode,
isReader: isReader)` uses named-arg call style (mirrors Dart). The
`PathCheckResult.consistent(...)` / `.inconsistent(...)` factory calls
map to `Consistent` / `Inconsistent` per C7.

**C13. `PathStepToLeafTerm` private helper** (convspec
`dart.private_helper_fn.path_step_to_leafterm_with_type_classification`)
→ `private static LeafTerm PathStepToLeafTerm(PathStep step)`. Branches
on `step.IsVariable`; for variables emits `LeafTerm.Reader(step.Symbol,
mode: step.Mode)` / `.Writer(step.Symbol, mode: step.Mode)`. For
constants, sequential probes:
```
if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
    return LeafTerm.IntegerConstant(intVal, mode: step.Mode);
if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleVal))
    return LeafTerm.RealConstant(doubleVal, mode: step.Mode);
if ((value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))
 || (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)))
    return LeafTerm.StringConstant(value.Substring(1, value.Length - 2), mode: step.Mode);
return LeafTerm.StringConstant(value, mode: step.Mode);
```
The Substring length-vs-end-index off-by-one is load-bearing (convspec
§rf-dart-string-substring-end-exclusive-to-csharp-substring-length):
Dart `value.substring(1, value.length - 1)` is end-exclusive →
C# `value.Substring(1, value.Length - 2)` (start=1, length=L-2).
`StringComparison.Ordinal` overload mandated on every StartsWith/EndsWith.
TryParse with `CultureInfo.InvariantCulture` mandated to match Dart.

**C14. `VariableKey` private helper** (convspec
`dart.private_helper_fn.trivial_one_liner_pure`) → `private static
string VariableKey(PathStep leaf) => leaf.Symbol;` — expression-bodied
member.

**C15. `CheckDuality` private helper** (convspec
`dart.private_helper_fn.groupby_then_pairwise_check_returning_error_list`)
→ `private static List<NonDualError> CheckDuality(IReadOnlyDictionary<string,
VariableTypeInfo> variableTypes)` on the host static class. Locals:
`var errors = new List<NonDualError>();`, `var baseNames = new
Dictionary<string, Dictionary<string, VariableTypeInfo>>(StringComparer.Ordinal);`.
`for (final entry in variableTypes.entries)` → `foreach (var entry in
variableTypes)` (C# `KeyValuePair<TK,TV>`). The Dart `putIfAbsent`
idiom translates to TryGetValue-then-Add (convspec §rf-dart-map-
putifabsent-to-csharp-trygetvalue-or-add):
```
if (!baseNames.TryGetValue(baseName, out var variants))
{
    variants = new Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal);
    baseNames[baseName] = variants;
}
variants[varKey] = info;
```
`varKey.endsWith('?')` → `varKey.EndsWith("?", StringComparison.Ordinal)`.
`varKey.substring(0, varKey.length - 1)` → `varKey.Substring(0,
varKey.Length - 1)` (start=0 case: length and end-exclusive index
coincide; NO adjustment — distinct from the C13 case). The five
pairwise duality checks (writer mode, reader mode, wildcard-universal,
same base name, opposite isDual) transliterate verbatim using
`continue` after each violation (identical keyword). String
interpolation in error messages uses `$"…"` per the cached unicode-
interp nuance.

**File-level emission shape.** One C# file `well_typed_term.cs` (under
the working-directory convention) containing: namespace `Glp.Analysis.TypeChecker`
(matching the directory mirror); `using` directives for `System`,
`System.Collections.Generic`, `System.Collections.Immutable` (if used
for empty-collection defaults), `System.Globalization`; the seven
classes (`WellTypedResult`, `VariableTypeInfo`, `WellTypedError`,
three error leaves, `PathCheckResult`); the static host class
`WellTypedTerm` carrying the two public + five private static methods.

## 3. Decomposed Task Units

- T1: Emit C# file header + namespace + using directives (System,
  System.Collections.Generic, System.Globalization).
- T2: Emit `sealed class WellTypedResult` with three read-only
  auto-properties, named-arg ctor, two static factories
  `Success`/`Failure`, no equality override.
- T3: Emit `sealed class VariableTypeInfo : IEquatable<VariableTypeInfo>`
  with three read-only auto-properties, ctor with named-arg call style,
  `ToString` (parenthesised ternary inside interpolation, Unicode
  arrows verbatim), hand-written `Equals(VariableTypeInfo?)` /
  `Equals(object?)` / `GetHashCode` via `HashCode.Combine` / `==` /
  `!=` operators.
- T4: Emit `abstract class WellTypedError` with one abstract `string
  Message { get; }` property (explicitly NOT an interface, NOT sealed).
- T5: Emit `sealed class InconsistentPathError : WellTypedError` with
  positional ctor, `Message` override (interpolated with `\n` escape
  preserved), `ToString() => Message;`.
- T6: Emit `sealed class InconsistentVariableError : WellTypedError`
  with positional ctor, `Message` override, `ToString() => Message;`.
- T7: Emit `sealed class NonDualError : WellTypedError` with positional
  ctor accepting two `VariableTypeInfo?` plus optional `string?
  reason = null`, `Message` override with inline reason-ternary,
  `ToString() => Message;`.
- T8: Emit `sealed class PathCheckResult` with three read-only auto-
  properties (Reason and VariableAssignment nullable), ctor, two
  static factories `Consistent(VariableTypeInfo? = null)` /
  `Inconsistent(string)`, no equality override.
- T9: Open `public static class WellTypedTerm`; emit public method
  `CheckModedTerm(ModedTerm, Automaton, ProgramDFA): WellTypedResult`
  building `List<WellTypedError>` + `Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal)`,
  the foreach-paths loop, the same-variable-different-type check (via
  ContainsKey + indexer), the AddRange of duality errors, and the
  final `new WellTypedResult(isWellTyped: errors.Count == 0, ...)`
  return.
- T10: Emit public method `CheckPathAgainstAutomaton(ModedPath,
  Automaton, ProgramDFA): PathCheckResult` with mutable locals
  `state`/`currentAutomaton`, single-step early return, C-style for
  loop, transition-null branch (with wildcard short-circuit and
  parenthesised ternary inside interpolated error messages), try/catch
  (Exception) around `dfa.GetAutomaton(...)`, automaton-switch on
  user-defined-type boundary, final leaf-consistency tail call.
- T11: Emit private helper `BuildTransitionLabel(PathStep, PathStep):
  TransitionLabel` using `Split('/')`, `int.TryParse(s, NumberStyles.Integer,
  CultureInfo.InvariantCulture, out var)` with `? v : 0` ternary
  fallback, and `TransitionLabel.Functor(..., mode: ...)` named-arg
  factory call.
- T12: Emit private helper `CheckLeafConsistencyForPath(PathStep,
  DFAState, ProgramDFA): PathCheckResult` delegating to
  `CheckLeafConsistency`, branching on `leaf.IsVariable`, using `??`
  for nullable fallbacks, building `VariableTypeInfo` via named-arg
  ctor.
- T13: Emit private helper `PathStepToLeafTerm(PathStep): LeafTerm`
  classifying variable/integer/real/quoted-string/atom; use
  `int.TryParse` / `double.TryParse` with invariant culture; use
  `StartsWith` / `EndsWith` with `StringComparison.Ordinal`; use
  `Substring(1, value.Length - 2)` for quote-stripping (off-by-one
  load-bearing).
- T14: Emit private helper `VariableKey(PathStep): string` as
  expression-bodied member returning `leaf.Symbol`.
- T15: Emit private helper `CheckDuality(IReadOnlyDictionary<string,
  VariableTypeInfo>): List<NonDualError>` using
  `Dictionary<string, Dictionary<string, VariableTypeInfo>>(StringComparer.Ordinal)`,
  the `TryGetValue`-then-`Add` idiom for putIfAbsent, `EndsWith("?",
  StringComparison.Ordinal)` for suffix test, `Substring(0, varKey.Length
  - 1)` for `?`-strip (start=0 no off-by-one adjustment), and the five
  pairwise duality checks producing `NonDualError` entries via
  `continue` after each violation.
- T16: Close the static class and namespace; verify the file is saved
  as UTF-8 (no BOM required, BOM acceptable) to preserve the `↓`/`↑`
  glyphs.

## 4. Research Findings

None required.

All thirteen non-trivial constructs are resolved against cached rf-*
findings already recorded in the convspec (eleven cached/reused from
sibling files in this directory per FR-024 — mode.dart, moded_term.dart,
program_dfa.dart, clause_validation.dart, prelude.dart, type_ast.dart;
two fresh — `rf-dart-string-substring-end-exclusive-to-csharp-substring-
length` and `rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add` —
each with verbatim authoritative dart.dev + learn.microsoft.com
citations preserved in the convspec). No new external research is
required for this plan; the convspec is the deep-analysis ledger and
this plan mirrors it.

## 5. Consistency Pass

Fixed — derived from the ratified convspec
`.codeconv/conversion-specs/lib/analysis/type_checker/well_typed_term.dart.md`
(0 open escalations) plus the in-directory sibling specs cited by FR-024
(moded_term.dart, program_dfa.dart, type_ast.dart, mode.dart,
clause_validation.dart, prelude.dart). All thirteen construct
target-decisions cite their convspec `idiom_id` + `research_finding_id`
verbatim; no Dart→C# decision is invented in this plan beyond literal
mechanical assembly. The eight constructs (C1–C7 data classes + C3
abstract base) + seven static-method translations (C8–C15) tile the
file completely:

- Every Dart class, function, and helper from the 470-line source maps
  to exactly one task unit (T2–T15) or to in-line code inside an
  enclosing task (the wildcard short-circuit C10 lives inside T10, etc.).
- The two off-by-one Substring sites are explicitly distinguished
  (C13 needs `Length - 2`, C15 needs `Length - 1`) — both grounded in
  the same authoritative rf-* finding.
- The two Dictionary<string, …> instances both demand
  `StringComparer.Ordinal` (C8 outer dict in `CheckModedTerm`; C15
  outer + inner dicts in `CheckDuality`).
- The `Try…` parsing pattern (C11, C13) mandates invariant culture per
  convspec nuance #6.
- The abstract-vs-interface choice for `WellTypedError` (C3) is the
  documented opposite conclusion of the cached rf rule, with the
  rationale fully traced in the convspec.

The `TypeEnvironment.getType(String)` cross-file decision (type_ast.dart
E1 on `object.GetType` shadowing) is **not applicable here** — no method
named `GetType` is introduced or referenced in this file; the
convspec's host-class name is `WellTypedTerm`, the public methods are
`CheckModedTerm` / `CheckPathAgainstAutomaton`, and the private
helpers are `BuildTransitionLabel`, `CheckLeafConsistencyForPath`,
`PathStepToLeafTerm`, `VariableKey`, `CheckDuality`. No collision with
`System.Object.GetType()`.

## 6. Escalations

None.
