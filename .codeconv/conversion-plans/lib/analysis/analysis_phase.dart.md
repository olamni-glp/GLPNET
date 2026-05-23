---
path: lib/analysis/analysis_phase.dart
cycle_group_id: 52
scc_siblings: []
generated_at: 2026-05-21T14:30:00Z
source_sha256: d322a2608cddcee827d4c360ba15b5ac5c7a8a2c5e43b2a690da8b2711e51d78
schema_version: 1
---

# Conversion Plan: lib/analysis/analysis_phase.dart

## 1. Source Analysis

Grounded in direct inspection of the 210-line Dart source.

**File role.** A self-contained analysis-phase contract module declaring (a) an error/warning value-object hierarchy, (b) a shared mutable context, (c) a result wrapper, (d) a phase interface, (e) a sequential runner, (f) three placeholder phase implementations, and (g) a factory function. No I/O, no async, no isolates, no streams.

**Imports.** None. The file is dependency-free at the Dart import level (tombstone `dependencies: []` confirms this), which is why it sits at `topo_level: 0` and forms its own singleton SCC (`cycle_group_id: 52`).

**Public surface (8 top-level declarations).**

1. `class AnalysisError` (lines 7-31) — five `final` fields (`String phase`, `String message`, `int line`, `int column`, `String? context`); single named-required constructor; `@override String toString()`; computed getter `bool get isError => true`.
2. `class AnalysisWarning extends AnalysisError` (lines 34-45) — super-parameter-forwarding constructor (`required super.phase`, etc.); overrides `isError` to `false`. Adds no state.
3. `class AnalysisContext` (lines 48-60) — mutable holder with `dynamic typeEnvironment`, two reassignable `Map<String,dynamic>` fields (`variableInfo`, `expandedGuards`) initialised to `{}`, and one `final Map<String,dynamic> data = {}` (final reference to a mutable map).
4. `class AnalysisResult` (lines 63-100) — wraps `List<AnalysisError> errors` and `AnalysisContext context`; getters `success`, `actualErrors`, `warnings` filter via `errors.where(...)`; `toString()` formats with `StringBuffer`.
5. `abstract class AnalysisPhase` (lines 103-110) — pure contract: `String get name` + `List<AnalysisError> analyze(dynamic ast, AnalysisContext ctx)`. No fields, no constructor, no method bodies. Consumed only through `implements` (lines 157, 177, 190).
6. `class AnalysisRunner` (lines 113-150) — `final List<AnalysisPhase> phases`; two methods: `run(ast, {bool stopOnError = false})` and `runPhases(ast, List<String> phaseNames)`. Both iterate phases sequentially, accumulate errors into `<AnalysisError>[]` via `addAll`, build a fresh `AnalysisContext`, and return `AnalysisResult`. `run` honours early-exit on `errors.any((e) => e.isError)` when `stopOnError`.
7. Three phase implementations `TypeCheckPhase` / `SRSWCheckPhase` / `DefinedGuardsPhase` (lines 157-200) — all `implements AnalysisPhase`; `TypeCheckPhase` has one optional `String? sourceCode` ctor param; all three `analyze` bodies are placeholders returning `[]`.
8. `AnalysisRunner createStandardRunner()` (lines 203-209) — top-level factory returning a runner pre-populated with the three placeholder phases.

**Language features in use.**

- Null safety: one nullable reference type (`String? context`); rest non-nullable.
- `dynamic`: typeEnvironment field, two `Map<String,dynamic>` fields, one `final Map<String,dynamic>` field, and the `ast` parameter on every `analyze`.
- Implicit interface (`abstract class` consumed via `implements`).
- Super-parameter forwarding (`required super.phase`).
- Collection literals (`{}`, `<AnalysisError>[]`).
- Iterable `where` / `toList` / `isEmpty` / `isNotEmpty` / `any` / `addAll`.
- `StringBuffer` accumulation with `writeln`.
- Top-level function (`createStandardRunner`).

**Language features NOT in use (asserted by reading every line).** No `Future`/`async`/`await`, no `Stream`, no isolates/`SendPort`, no `late`, no `sealed`, no `mixin`, no `extension`, no generics on declared types, no operator overloads, no enums, no records, no codegen annotations, no `factory` constructors, no static fields/methods, no closures captured beyond the inline lambdas in `where`/`any`.

**Identity / mutability semantics.** `AnalysisError` instances are reference objects (collected in lists, filtered by identity-bearing polymorphism). `AnalysisContext` is mutable and shared across phases (each `run`/`runPhases` invocation constructs a fresh one). `AnalysisResult.errors` is a single mutable list captured by reference at construction; the `actualErrors`/`warnings` getters materialise NEW lists per call via `toList()` — eager snapshot semantics, not lazy views.

## 2. Dart → C#/.NET Conversion Plan

This section mirrors the convspec's six ratified per-construct decisions (`construct_key`s) verbatim. Inline citations point to convspec `construct_key` + `research_finding_id`.

### 2.1 `AnalysisError` value type — REFERENCE class, virtual `IsError`

Per convspec construct *"class AnalysisError final-fields named-required-ctor toString-override bool-get-isError"* (`rf-dart-nullsafety-to-csharp-nrt`):

- Emit as a **reference-type C# `class`** (not `struct`, not `record struct`). Identity matters because `AnalysisResult` holds a `List<AnalysisError>` by reference and `AnalysisWarning` extends it polymorphically.
- The five `final` Dart fields become **get-only auto-properties** initialised from the constructor — immutability preserved without exposing setters.
- One C# constructor taking the same logical parameters; Dart's `required` named-arg obligation is captured by simply having the parameter be non-optional (Dart `required` is a must-supply contract, not a runtime semantics).
- `toString()` override → `public override string ToString()`. Format string preserves the Dart literal exactly: `"[$phase] $message at line $line, column $column"` with optional `"\n    $context"` appended when `context` is non-null.
- `bool get isError => true` → **`public virtual bool IsError => true;`** — declaring it virtual is load-bearing: `AnalysisWarning.IsError` must override polymorphically, otherwise the LINQ `Where(e => e.IsError)` filters in §2.4 would bind statically and misclassify warnings (convspec nuance).
- **Nullability mapping** (`rf-dart-nullsafety-to-csharp-nrt`): under an enabled nullable context, `String? context` → `string? Context`; the other three reference fields (`phase`, `message`) → non-nullable `string`; `line`/`column` → `int` (Dart `int` ≈ .NET `int`/`long`; convspec leaves the integer width to the standard mapping — `int` is the conventional Dart→C# default).

### 2.2 `AnalysisWarning` — base-ctor forwarding + `IsError` override

Per convspec construct *"class AnalysisWarning extends AnalysisError super-param-forwarding-ctor override-bool-get-isError"* (`rf-dart-superparams-to-csharp-basector`):

- C# `class AnalysisWarning : AnalysisError` with a constructor that uses `: base(phase, message, line, column, context)` to chain to the base — direct counterpart of Dart `super.phase`, `super.message`, etc.
- No fields of its own (the subclass adds zero state, just polymorphic behaviour).
- `public override bool IsError => false;` overriding the virtual base getter. The convspec's nuance explicitly flags that without `virtual`/`override` the polymorphic dispatch breaks — non-negotiable.

### 2.3 `AnalysisContext` — `dynamic` + mutable `Dictionary` fields

Per convspec construct *"dynamic-typed fields and parameters typeEnvironment Map-String-dynamic analyze-dynamic-ast"* (`rf-dart-dynamic-to-csharp-dynamic`):

- C# `class AnalysisContext` (reference type — shared across phases).
- `dynamic typeEnvironment` → **C# `dynamic TypeEnvironment { get; set; }`** (NOT `object` — convspec is explicit: `object` would force casts that Dart accepts, changing semantics).
- `Map<String,dynamic> variableInfo = {}` → `public Dictionary<string, dynamic> VariableInfo { get; set; } = new();` (settable: the Dart field is reassignable).
- `Map<String,dynamic> expandedGuards = {}` → same pattern as `VariableInfo`.
- `final Map<String,dynamic> data = {}` → `public Dictionary<string, dynamic> Data { get; } = new();` — get-only property (reference is final) over a mutable `Dictionary` (contents are mutable). Convspec nuance: do not use an immutable collection here.

### 2.4 `AnalysisResult` — list wrapper + LINQ getters + StringBuilder `ToString`

Per convspec constructs *"collection-literal-init Iterable-where-toList-isEmpty-isNotEmpty-any-addAll"* (`rf-dart-iterable-where-to-linq`) and *"StringBuffer accumulation in toString StringBuffer-sb sb-writeln"* (`rf-dart-stringbuffer-to-csharp-stringbuilder`):

- C# `class AnalysisResult` with `public List<AnalysisError> Errors { get; }` and `public AnalysisContext Context { get; }`, both set in the constructor.
- `bool get success => errors.where((e) => e.isError).isEmpty` → `public bool Success => !Errors.Any(e => e.IsError);` (convspec equivalence: `.where(...).isEmpty` ↔ `!Any(...)`; the `.where + .isEmpty` shortcut maps directly to `!Any(pred)`).
- `List<AnalysisError> get actualErrors => errors.where((e) => e.isError).toList()` → `public List<AnalysisError> ActualErrors => Errors.Where(e => e.IsError).ToList();` — convspec mandates `.ToList()` (NOT returning an `IEnumerable`) so eager-snapshot semantics survive (Dart `toList()` materialises eagerly).
- `List<AnalysisError> get warnings => errors.where((e) => !e.isError).toList()` → `public List<AnalysisError> Warnings => Errors.Where(e => !e.IsError).ToList();`.
- `toString()` builder: `StringBuffer` → `System.Text.StringBuilder`; `sb.writeln(x)` → `sb.AppendLine(x)`; `sb.toString()` → `sb.ToString()`. The early-exit `if (errors.isEmpty) return 'Analysis successful ...'` becomes `if (Errors.Count == 0) return "Analysis successful (no errors or warnings)";` (or `!Errors.Any()` — `.isEmpty` ↔ `.Count == 0` for `List<T>`; either is faithful; prefer `.Count == 0` for `List<T>` since it's O(1)).
- **Newline nuance** (`rf-dart-stringbuffer-to-csharp-stringbuilder`): Dart `writeln` appends `\n`; `StringBuilder.AppendLine` appends `Environment.NewLine`. Convspec records this as a documented platform-newline difference, semantically a line break in both — preserved as the conventional .NET behaviour.

### 2.5 `AnalysisPhase` — pure interface, NOT abstract class

Per convspec construct *"abstract class AnalysisPhase pure-interface abstract-getter abstract-method implements-conformance"* (`rf-dart-abstract-interface-to-csharp-interface`):

- Emit as **C# `interface IAnalysisPhase`** (the convspec's `conversion_units` entry uses the `I`-prefixed name `IAnalysisPhase`, the .NET convention).
- Members: `string Name { get; }` and `List<AnalysisError> Analyze(dynamic ast, AnalysisContext ctx);`.
- Concrete phases use `: IAnalysisPhase` interface conformance.
- `AnalysisRunner` stores `List<IAnalysisPhase> Phases` — runtime polymorphic dispatch.
- Convspec nuance: a C# `abstract class` would be **wrong** because (a) it would consume the single base-class slot of any implementor and (b) it would imply shared state — neither is the case in the Dart source.

### 2.6 `AnalysisRunner` — sequential iteration + early-exit

Per convspec construct *"collection-literal-init Iterable-where-toList-isEmpty-isNotEmpty-any-addAll"* (and structurally a straightforward class):

- `final List<AnalysisPhase> phases` → `public List<IAnalysisPhase> Phases { get; }`, set in the constructor.
- `run(ast, {bool stopOnError = false})` → `public AnalysisResult Run(dynamic ast, bool stopOnError = false)` (default-parameter direct map). Body:
  - `var ctx = new AnalysisContext();`
  - `var allErrors = new List<AnalysisError>();`
  - `foreach (var phase in Phases) { var errors = phase.Analyze(ast, ctx); allErrors.AddRange(errors); if (stopOnError && errors.Any(e => e.IsError)) break; }`
  - `return new AnalysisResult(allErrors, ctx);`
- `runPhases(ast, List<String> phaseNames)` → `public AnalysisResult RunPhases(dynamic ast, List<string> phaseNames)`. Body is the same iteration with the per-phase `if (phaseNames.Contains(phase.Name)) { ... }` gate.
- All collection idioms map per convspec: `<AnalysisError>[]` → `new List<AnalysisError>()`, `.addAll` → `.AddRange`, `.any(pred)` → `.Any(pred)`, `.contains(x)` (implicit on `List<String>`) → `.Contains(x)` (`List<string>.Contains` is O(n); preserves Dart `List.contains` semantics — both are linear).

### 2.7 Placeholder phase implementations

Per convspec note ("the three placeholder analyze bodies return an empty list; conversion preserves them verbatim"):

- `TypeCheckPhase` — `public class TypeCheckPhase : IAnalysisPhase` with `public string? SourceCode { get; }` (nullable, optional), one constructor `public TypeCheckPhase(string? sourceCode = null) { SourceCode = sourceCode; }`, `public string Name => "type";`, and `public List<AnalysisError> Analyze(dynamic ast, AnalysisContext ctx) => new List<AnalysisError>();` (the empty-list placeholder is preserved verbatim — convspec is explicit that "no semantic decision required, hence no escalation").
- `SRSWCheckPhase` — same skeleton, no constructor params, `Name => "srsw"`.
- `DefinedGuardsPhase` — same skeleton, no constructor params, `Name => "guards"`.

### 2.8 `createStandardRunner` top-level factory

Per convspec `conversion_units` entry *"createStandardRunner factory"*:

- Dart top-level functions don't exist in C#. Per the project's standard convention for top-level helpers in this conversion (host as a `public static` member on a sibling/companion static class), emit as:
  - `public static class AnalysisPhaseFactory { public static AnalysisRunner CreateStandardRunner() => new AnalysisRunner(new List<IAnalysisPhase> { new TypeCheckPhase(), new SRSWCheckPhase(), new DefinedGuardsPhase() }); }`
- This preserves the function's behaviour (returns a fresh `AnalysisRunner` with the three placeholder phases) without inventing new design surface. Hosting it on a static class is the conventional Dart-top-level-function → C# mapping; the convspec `conversion_units` enumerate this factory but do not prescribe a host name — the host-class name is the only non-convspec-derived detail, and it is a routine naming choice within the established convention (no design decision required, hence no escalation).

### 2.9 File / namespace layout

- One C# file `lib/analysis/analysis_phase.cs` (per convspec `target_code_unit`), containing in this order: `IAnalysisPhase`, `AnalysisError`, `AnalysisWarning`, `AnalysisContext`, `AnalysisResult`, `AnalysisRunner`, `TypeCheckPhase`, `SRSWCheckPhase`, `DefinedGuardsPhase`, `AnalysisPhaseFactory`.
- Namespace follows the standard subtree-derived mapping for this conversion (project-wide convention; `lib/analysis/` → the conventional `Analysis` segment within the project root namespace). The convspec does not pin a namespace name; conformance to the existing project-wide convention is the only requirement (no novel decision).
- `using` directives: `System.Collections.Generic` (for `List<>`/`Dictionary<>`), `System.Linq` (for LINQ Where/Any/ToList), `System.Text` (for `StringBuilder`). `dynamic` is built-in, no using needed.
- Nullable context enabled (`#nullable enable` or project-wide setting) — required so `string?` is meaningful and the three non-nullable reference fields are enforced as non-null.

## 3. Decomposed Task Units

Each task is independently implementable and verifiable. Definition-of-done is one line.

- **T1 — Emit `IAnalysisPhase` interface.** DoD: `IAnalysisPhase` declared with `string Name { get; }` and `List<AnalysisError> Analyze(dynamic ast, AnalysisContext ctx);`, compiles in isolation.
- **T2 — Emit `AnalysisError` reference class.** DoD: class with five get-only auto-properties (4 non-nullable + 1 `string? Context`), single constructor assigning all five, `public virtual bool IsError => true;`, `public override string ToString()` producing the Dart-equivalent format string; compiles.
- **T3 — Emit `AnalysisWarning : AnalysisError`.** DoD: subclass with base-chaining constructor `: base(phase, message, line, column, context)`, `public override bool IsError => false;`, no own fields; compiles and polymorphic dispatch verified.
- **T4 — Emit `AnalysisContext` class.** DoD: `dynamic TypeEnvironment` settable, two `Dictionary<string, dynamic>` settable properties initialised to `new()`, one get-only `Data` property initialised to `new()`; compiles.
- **T5 — Emit `AnalysisResult` class.** DoD: ctor takes `List<AnalysisError>` + `AnalysisContext`, stores both as get-only properties, `Success`/`ActualErrors`/`Warnings` LINQ getters in place, `ToString()` uses `StringBuilder` with `AppendLine` and the early-empty-errors return; compiles.
- **T6 — Emit `AnalysisRunner` class.** DoD: constructor takes `List<IAnalysisPhase>`, both `Run(dynamic, bool=false)` and `RunPhases(dynamic, List<string>)` methods iterate phases sequentially, `AddRange`-accumulate, honour `stopOnError` early-exit; returns `AnalysisResult`; compiles.
- **T7 — Emit `TypeCheckPhase` placeholder.** DoD: `: IAnalysisPhase` with optional ctor param `string? sourceCode = null`, `Name => "type"`, `Analyze => new List<AnalysisError>()`; compiles.
- **T8 — Emit `SRSWCheckPhase` placeholder.** DoD: `: IAnalysisPhase`, no ctor params, `Name => "srsw"`, `Analyze => new List<AnalysisError>()`; compiles.
- **T9 — Emit `DefinedGuardsPhase` placeholder.** DoD: `: IAnalysisPhase`, no ctor params, `Name => "guards"`, `Analyze => new List<AnalysisError>()`; compiles.
- **T10 — Emit `AnalysisPhaseFactory.CreateStandardRunner`.** DoD: static class hosting a static method that returns `new AnalysisRunner(new List<IAnalysisPhase> { new TypeCheckPhase(), new SRSWCheckPhase(), new DefinedGuardsPhase() })`; compiles.
- **T11 — File assembly: `analysis_phase.cs`.** DoD: single file with `using` directives (`System.Collections.Generic`, `System.Linq`, `System.Text`), `#nullable enable` (or project-wide equivalent), all ten types in declaration order from §2.9; file compiles standalone with no project references (matches Dart source's zero imports).

## 4. Research Findings

None required. All six non-trivial constructs have authoritative ratifications already recorded in the convspec under `research_finding_id`s `rf-dart-nullsafety-to-csharp-nrt`, `rf-dart-superparams-to-csharp-basector`, `rf-dart-dynamic-to-csharp-dynamic`, `rf-dart-abstract-interface-to-csharp-interface`, `rf-dart-stringbuffer-to-csharp-stringbuilder`, `rf-dart-iterable-where-to-linq` — each backed by WebFetched Dart official / Microsoft Learn documentation (verbatim quotes preserved in the convspec). No remaining open questions; web research was completed at convspec time and is forbidden at plan time per the §6 instructions.

## 5. Consistency Pass

Cross-check of §2 vs §3 vs §4 vs convspec vs tombstone.

- **Construct coverage.** Convspec lists 6 ratified `construct_key`s + 8 `conversion_units`. §2 covers all 6 constructs (sections 2.1-2.7 map each `construct_key` to its concrete C# emission; 2.8 handles the top-level factory which is a `conversion_units` entry but not a separate `construct_key`). §3 decomposes into 11 tasks that together produce all 8 `conversion_units`. **Consistent — derived from convspec.**
- **Polymorphism load-bearing on `virtual`/`override` `IsError`.** Convspec nuance for constructs 1 + 2 explicitly requires `virtual`/`override`; §2.1 and §2.2 enforce it; §3 T2 and T3 verify it. **Consistent — derived from convspec construct 1+2 nuance.**
- **Eager-vs-lazy LINQ.** Convspec nuance for construct 6 requires terminal `.ToList()` on `ActualErrors`/`Warnings` (NOT `IEnumerable`); §2.4 enforces; §3 T5 DoD requires the LINQ getters to be present (assumed terminal per §2.4). **Consistent — derived from convspec construct 6 nuance.**
- **`dynamic` (not `object`).** Convspec construct 3 mandates `dynamic`; §2.3 emits `dynamic`; §2.5 keeps `analyze(dynamic ast, ...)` signature; §2.6 keeps `dynamic ast` parameter. **Consistent — derived from convspec construct 3.**
- **Interface (not abstract class).** Convspec construct 4 mandates `IAnalysisPhase` interface; §2.5 emits interface; §2.6 stores `List<IAnalysisPhase>`; §3 T1 produces the interface; §3 T7/T8/T9 implement it. **Consistent — derived from convspec construct 4.**
- **Tombstone agreement.** Tombstone `target_path: lib/analysis/analysis_phase.cs` matches convspec `target_code_unit` and §2.9 file name. Tombstone `dependencies: []` matches the source file having zero imports; §2.9 emits only standard `System.*` usings. **Consistent — derived from tombstone + source inspection.**
- **SHA agreement.** Computed `source_sha256` = `d322a2608cddcee827d4c360ba15b5ac5c7a8a2c5e43b2a690da8b2711e51d78` matches convspec `source_sha256`. **Consistent — directly verified.**
- **Gaps potentially needing escalation.**
  - *Top-level `createStandardRunner` host-class name (`AnalysisPhaseFactory`)* — naming is a routine choice within the established Dart-top-level-function → C# static-class convention; no design decision, no scope growth. **Fixed (pre-specified, incremental) — derived from convspec `conversion_units` entry "createStandardRunner factory" + standard project naming convention.**
  - *Namespace name for the emitted file* — derived from the project-wide subtree-to-namespace mapping convention (not novel for this file). **Fixed (pre-specified, incremental) — derived from project convention.**
  - *Newline difference between Dart `writeln` (`\n`) and .NET `AppendLine` (`Environment.NewLine`)* — convspec construct 5 explicitly records this as a "documented newline difference ... semantically a line break in both"; ratified, no further decision. **Fixed (pre-specified, incremental) — derived from convspec construct 5 nuance.**
  - *Integer width (Dart `int` is 64-bit on native VM)* — convspec implicitly accepts the standard mapping (`int` Dart → `int` C#); both store small line/column counts well within `Int32` range; no realistic overflow for source locations. **Fixed (pre-specified, incremental) — standard Dart `int` → C# `int` mapping.**

No remaining gaps. No escalations required.

## 6. Escalations

None.