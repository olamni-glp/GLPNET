---
path: lib/compiler/pmt/validator.dart
cycle_group_id: 60
scc_siblings: []
generated_at: 2026-05-21T16:18:38Z
source_sha256: 448fc123513f79f1a1f9a0444d1c4525a10d38163a5471cdae5b41e0c9eba4e6
schema_version: 1
---

# Conversion Plan: lib/compiler/pmt/validator.dart

## 1. Source Analysis

The Dart source `lib/compiler/pmt/validator.dart` is a 96-line file declaring
exactly one top-level class — `PmtValidator` — and importing seven sibling
units:

- `../ast.dart` (for `Module`)
- `../lexer.dart` (for `Lexer`)
- `../parser.dart` (for `Parser`)
- `mode_table.dart` (for `ModeTable`)
- `type_table.dart` (for `TypeTable`)
- `checker.dart` (for `PmtChecker`)
- `type_checker.dart` (for `TypeChecker`)
- `errors.dart` (for `PmtError` and `PmtErrors`)

`PmtValidator` is structurally a **static-only namespacing facade**: four
`static` methods, zero instance fields, zero constructors, zero instance
members. The class identifier is used solely as a prefix at call sites
(`PmtValidator.validateSource(...)`).

The four static methods, in source order:

1. **`validateSource(String source) -> List<PmtError>`** — a four-stage
   straight-line pipeline. Allocates a fresh `Lexer` with the source,
   invokes its mutable-cursor-bearing `tokenize()` to get a `List<Token>`,
   allocates a fresh `Parser` with those tokens, invokes its
   mutable-cursor-bearing `parseModule()` to get a `Module`, then
   tail-delegates to the sibling static `validateAst(ast)`. Two short-lived
   helper objects allocated per call — neither hoistable.

2. **`validateAst(Module ast) -> List<PmtError>`** — the multi-phase
   validation orchestrator. Behaviour, line-by-line:
   - `if (ast.modeDeclarations.isEmpty) return [];` — silent early-return
     of an empty list when there are no mode declarations to check
     against. This is **not** an error; the doc-comment says "No mode
     declarations = nothing to check".
   - `final modeTable = ModeTable.fromDeclarations(ast.modeDeclarations);`
     — build the mode table via the named-static factory.
   - `final typeTable = TypeTable.fromModule(ast);` — build the type
     table via the named-static factory.
   - `final srswChecker = PmtChecker(modeTable);` — allocate the SRSW
     checker with positional ctor.
   - `final errors = <PmtError>[];` — allocate the single shared
     accumulator.
   - `for (final proc in ast.procedures) { errors.addAll(srswChecker
     .checkProcedure(proc)); }` — phase 1: append every SRSW error to
     the accumulator.
   - `if (ast.typeDefinitions.isNotEmpty) { ... }` — conditional
     phase 2: only run type checking when there are type definitions.
     Inside: allocate `TypeChecker(typeTable, modeTable)`, call
     `checkModule(ast)` to get a `List<TypeError>`, then loop converting
     each `TypeError` into a `PmtError(te.message, te.line, te.column)`
     and `add`-ing to the same shared accumulator.
   - `return errors;` — return the merged list (SRSW errors first, then
     type errors).

3. **`isValid(String source) -> bool`** — single-statement thin facade:
   `return validateSource(source).isEmpty;`. Each call allocates a
   `List<PmtError>` via `validateSource`, reads its `isEmpty` getter, and
   discards the list.

4. **`assertValid(String source) -> void`** — validate-or-throw:
   `final errors = validateSource(source); if (errors.isNotEmpty) throw
   PmtErrors(errors);`. The same `errors` list reference is passed
   directly into the `PmtErrors` constructor (aliasing — no defensive
   copy on either side per `errors.dart.md`).

Cross-cutting observations:

- No async, no Streams, no Futures, no isolates, no `late`, no inheritance
  surface — purely synchronous, single-class, all-static.
- Two cross-file surface-gap notes (resolved as cross-file invariant
  assumptions per the convspec's Notes section): `Module` in `ast.dart`
  does not currently declare `modeDeclarations`; `typeDefinitions` is the
  camelCase form of `typeDefs`. The pmt subsystem assumes both are
  present; the conversion preserves both as `Module.ModeDeclarations`
  (`IReadOnlyList<ModeDeclaration>`) and `Module.TypeDefs` (reused for
  `typeDefinitions`).
- The `errors` accumulator design is load-bearing: SRSW errors are
  appended first, then type errors, preserving textual error ordering.
- `Lexer` and `Parser` carry per-instance mutable cursor state — they
  MUST be allocated fresh per `validateSource` call (per `lexer.dart.md`
  and `parser.dart.md`).
- `PmtErrors` aliases its incoming list reference (per `errors.dart.md`)
  — `AssertValid` must NOT introduce a defensive copy.

## 2. Dart → C#/.NET Conversion Plan

Per the RATIFIED convspec at
`.codeconv/conversion-specs/lib/compiler/pmt/validator.dart.md`, every
construct in this file is resolved against authoritative Microsoft Learn
+ dart.dev sources with cached + fresh rf-* findings. Five conversion
units, mirroring the convspec verbatim:

### Construct 1 — `dart.utility_class.static_only_holder` → C# `public static class`

**Source form:** `class PmtValidator { static List<PmtError>
validateSource(String source) {...} static List<PmtError> validateAst(
Module ast) {...} static bool isValid(String source) {...} static void
assertValid(String source) {...} }` — a Dart class containing four
`static` methods, no instance fields, no instance constructor, no
instance members. Used as a namespacing facade for the PMT validation
entry-points.

**Target decision:** Emit `public static class PmtValidator` (sealed +
abstract by the `static` modifier — cannot be instantiated; Microsoft
Learn "Static Classes and Static Class Members": "A static class is
basically the same as a non-static class, but there's one difference:
a static class can't be instantiated. … you access the members of a
static class by using the class name itself"). Containing exactly four
static members `ValidateSource`, `ValidateAst`, `IsValid`, `AssertValid`.
A non-static class with all-static members is REJECTED per the cached
idiom — the Dart source's class is callable only via
`PmtValidator.validateSource(...)` etc. (never instantiated) and `static
class` makes the no-instantiation contract a compile-time guarantee on
the .NET side, matching the source's design intent. Identical
structural decision as `abandon.dart.md`'s `AbandonOps`,
`commit.dart.md`'s commit-facade, and `suspend_ops.dart.md`'s
suspend-facade. Class-level `public` access mirrors Dart's library-public
(no leading underscore on `PmtValidator`).

**Idiom id:** `rf-dart-static-only-holder-to-csharp-static-class`
**Research finding id:** `rf-dart-static-only-holder-to-csharp-static-class`

**Nuance — verbatim from convspec:** Static-class contract (explicitly
addressed, US2-AS4). In Dart, a class with only static members is still
an instantiable type (`PmtValidator()` would compile and yield a
zero-field instance); the convention is "treat the class name as a
namespace". C#'s `static class` makes that convention a compile-time
invariant (Microsoft Learn: "a static class can't be instantiated") — a
strictly tighter contract than the Dart source. The narrowing is
strictly correct here because the Dart source never instantiates
`PmtValidator` and has no instance state to preserve. The .NET-idiomatic
narrowing also implicitly seals the type and forbids subclassing,
reinforcing the source's "this is a namespacing facade, not a
polymorphism surface" intent — same rationale as `abandon.dart.md`'s
`AbandonOps`. Value-vs-reference: static classes have no instances, so
neither value nor reference semantics apply to the type itself;
per-method signatures govern argument/return semantics (handled
per-construct below). No `Stream`/`Future`/async/isolate concerns —
all four methods are synchronous static dispatch. Null-safety at the
type level: not applicable.

### Construct 2 — `dart.static_method.pipeline_lex_parse_validate_returning_list` → C# four-stage allocation pipeline

**Source form:** `static List<PmtError> validateSource(String source) {
final lexer = Lexer(source); final tokens = lexer.tokenize(); final
parser = Parser(tokens); final ast = parser.parseModule(); return
validateAst(ast); }`

**Target decision:** Emit `public static List<PmtError> ValidateSource(
string source) { var lexer = new Lexer(source); var tokens = lexer
.Tokenize(); var parser = new Parser(tokens); var ast = parser
.ParseModule(); return ValidateAst(ast); }`. Four-stage straight-line
pipeline: construct `Lexer` with the source, call `Tokenize()` returning
`List<Token>` (per `lexer.dart.md` conversion unit "public List<Token>
Tokenize()"), construct `Parser` with the tokens (per `parser.dart.md`
conversion unit "class Parser ... List<Token> tokens"), call
`ParseModule()` returning `Module` (per `parser.dart.md` conversion
unit "public Module ParseModule()"), tail-call the sibling static
`ValidateAst`. Dart `final` locals → C# `var` locals (cached
`rf-dart-final-local-to-csharp-var` from multiple prior corpus files;
the locals are assigned-once / never reassigned but inferred types are
mutable reference handles — same semantics in both languages). The two
`new` allocations are explicit per Microsoft Learn "The new operator":
"creates a new instance of a type"; Dart's implicit-`new` constructor
call (`Lexer(source)`) becomes the explicit `new Lexer(source)` on the
C# side. Static-method-to-static-method dispatch (`return validateAst(
ast)`) becomes `return ValidateAst(ast)` — unqualified within the same
`static class` per Microsoft Learn "Static members in static classes
are accessed from within the class without the type-name qualifier".

**Idiom id:** null
**Research finding id:**
`rf-dart-static-pipeline-method-to-csharp-static-pipeline-method`

**Nuance — verbatim from convspec:** Three load-bearing nuances. (1)
Pipeline allocation parity: the Dart source ALLOCATES TWO short-lived
helper objects (`Lexer`, `Parser`) per call. The C# port preserves that
allocation pattern verbatim — codegen MUST NOT hoist `Lexer`/`Parser`
to static or pooled instances. The Dart `Lexer` and `Parser` carry
per-instance mutable cursor state (per `lexer.dart.md` "single instance
built once and its Tokenize() method mutates the cursor" and
`parser.dart.md` "`Parse()` / `ParseModule()` mutate `_current`") —
concurrent reuse would corrupt cursors. Per-call fresh construction is
the contract. (2) Method-name casing: Dart lowerCamelCase
`validateSource` / `validateAst` → C# PascalCased `ValidateSource` /
`ValidateAst` per the project-wide Dart→C# method-naming discipline
(Microsoft Learn "Capitalization Conventions": "PascalCasing convention
… is used for all identifiers except parameter names"); `tokenize` →
`Tokenize`, `parseModule` → `ParseModule` per the respective sibling
specs. (3) Return-type fidelity: the Dart `List<PmtError>` return type
stays `List<PmtError>` (NOT `IReadOnlyList<PmtError>`) on the C# side
because the caller `AssertValid` checks `errors.IsEmpty == false` /
`errors.Count != 0` and may pass the list into the `PmtErrors(
IReadOnlyList<PmtError> errors)` exception ctor (per `errors.dart.md`);
both call-sites accept a `List<PmtError>`. No async / Stream / Future
/ isolate concerns — purely synchronous lexer→parser→checker chain
(US2-AS4: well-known nuance correctly absent).

### Construct 3 — `dart.static_method.validate_ast_with_short_circuits_and_two_phase_checking` → C# two-phase orchestrator with single-accumulator mutation

**Source form:** `static List<PmtError> validateAst(Module ast) { if
(ast.modeDeclarations.isEmpty) return []; final modeTable = ModeTable
.fromDeclarations(ast.modeDeclarations); final typeTable = TypeTable
.fromModule(ast); final srswChecker = PmtChecker(modeTable); final
errors = <PmtError>[]; for (final proc in ast.procedures) { errors
.addAll(srswChecker.checkProcedure(proc)); } if (ast.typeDefinitions
.isNotEmpty) { final typeChecker = TypeChecker(typeTable, modeTable);
final typeErrors = typeChecker.checkModule(ast); for (final te in
typeErrors) { errors.add(PmtError(te.message, te.line, te.column)); }
} return errors; }`

**Target decision:** Emit `public static List<PmtError> ValidateAst(
Module ast) { if (ast.ModeDeclarations.Count == 0) return new List<
PmtError>(); var modeTable = ModeTable.FromDeclarations(ast
.ModeDeclarations); var typeTable = TypeTable.FromModule(ast); var
srswChecker = new PmtChecker(modeTable); var errors = new List<
PmtError>(); foreach (var proc in ast.Procedures) { errors.AddRange(
srswChecker.CheckProcedure(proc)); } if (ast.TypeDefs.Count != 0) {
var typeChecker = new TypeChecker(typeTable, modeTable); var
typeErrors = typeChecker.CheckModule(ast); foreach (var te in
typeErrors) { errors.Add(new PmtError(te.Message, te.Line, te.Column));
} } return errors; }`. Five cross-file invariants: `Module
.ModeDeclarations` is referenced as a property on `Module`, but
ast.dart.md's `Module` spec DOES NOT currently declare this member
(see Notes — the Dart source references `ast.modeDeclarations` against
a `Module` that has no such getter; the pmt subsystem assumes a
`Module.modeDeclarations: List<ModeDeclaration>` getter that the
ast.dart spec must add to remain consistent — handled as a cross-file
invariant assumption, parallel to mode_table.dart.md's
"ModeDeclaration shape fixed elsewhere" treatment). `ModeTable
.FromDeclarations(IReadOnlyList<ModeDeclaration>) -> ModeTable` per
mode_table.dart.md. `TypeTable.FromModule(Module) -> TypeTable` per
type_table.dart.md (the prior sibling spec). `new PmtChecker(modeTable)`
per checker.dart.md's positional ctor. `new TypeChecker(typeTable,
modeTable)` per type_checker.dart.md's positional ctor with two
injected deps. Dart `<PmtError>[]` typed-empty-list literal → C# `new
List<PmtError>()` via the cached
`rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange`
(from `checker.dart.md` / `type_checker.dart.md`). Dart `errors
.addAll(...)` → C# `errors.AddRange(...)` per the same cached finding.
Dart `for (final x in xs)` → C# `foreach (var x in xs)`. Dart
`.isEmpty` → C# `.Count == 0` and Dart `.isNotEmpty` → C# `.Count != 0`
per the cached `rf-dart-length-isempty-to-csharp-count` finding (from
mode_table.dart.md). The early-return empty-list `return [];` becomes
`return new List<PmtError>()` — NOT `Array.Empty<PmtError>()` —
because the return type is `List<PmtError>` (a mutable concrete type)
and the caller (`AssertValid`) may pass it into the `PmtErrors`
exception ctor which accepts `IReadOnlyList<PmtError>`; the
concrete-return-fresh-empty rendering matches the precedent in
`checker.dart.md`'s `CheckClauseAgainstModes` early-success `return
new List<PmtError>()`. The inner conversion `errors.add(PmtError(te
.message, te.line, te.column))` becomes `errors.Add(new PmtError(te
.Message, te.Line, te.Column))` — explicit `new`, PascalCased property
access on `TypeError` (cross-file invariant from type_checker.dart.md
/ errors.dart.md).

**Idiom id:** null
**Research finding id:**
`rf-dart-two-phase-validation-with-error-accumulator-and-error-type-conversion-to-csharp-list-addrange-and-mapping`

**Nuance — verbatim from convspec:** Four load-bearing nuances. (1)
Silent-skip semantics on empty mode-declarations: the source's
doc-comment "No mode declarations = nothing to check" mandates SILENT
early-return with an empty list, NOT an error. The C# port MUST
preserve the silent skip — promoting to an error or to `throw` would
change observable behaviour under FR-023 / FR-024. Same rationale as
`checker.dart.md`'s `CheckProcedure` silent-skip on null-or-empty
allModes. (2) Optional type-checking phase: the `if (ast.typeDefinitions
.isNotEmpty)` branch is a CONDITIONAL second checking pass — type
checking is only performed when type definitions exist. Codegen MUST
preserve the optionality verbatim; "always invoke type checking" would
(a) waste cycles and (b) potentially surface spurious errors if
`TypeChecker` doesn't handle the empty-type-defs case gracefully. (3)
TypeError→PmtError mapping: the inner loop converts each `TypeError`
(per type_checker.dart.md's surface, with `Message`/`Line`/`Column`
properties) into a `PmtError` (per errors.dart.md's surface, also with
`Message`/`Line`/`Column`). The mapping discards any TypeError-specific
structure (e.g., source-form metadata) and keeps ONLY the three shared
fields — same lossy projection in both languages; the C# port
preserves it verbatim. A naïve "preserve TypeError" port would break
the doc-contracted return type `List<PmtError>` and would force
callers to handle two error-shapes. (4) Accumulator-mutation pattern:
`errors` is constructed ONCE at the top of the method and mutated by
BOTH the SRSW-checking loop and the type-checking loop. Codegen MUST
preserve this single-accumulator design — splitting into `srswErrors`
and `typeErrors` lists and concatenating at the end would (a) change
the textual ordering of errors (currently: ALL SRSW errors FIRST, then
ALL type errors, mirroring source order) and (b) introduce an
unnecessary intermediate allocation. The Dart `addAll`/C# `AddRange`
and Dart `add`/C# `Add` mutations are in-place on the shared
accumulator — semantically identical, no boxing/copying. No async /
Stream / Future / isolate concerns (US2-AS4: well-known nuance
correctly absent — purely synchronous orchestration).

### Construct 4 — `dart.static_method.thin_passthrough_returning_isempty_of_inner_call` → C# expression-bodied passthrough

**Source form:** `static bool isValid(String source) { return
validateSource(source).isEmpty; }`

**Target decision:** Emit `public static bool IsValid(string source)
=> ValidateSource(source).Count == 0;` as a single-expression body
(`=>` expression-bodied member; Microsoft Learn "Expression-bodied
members": "Expression body definitions let you provide a member's
implementation in a very concise, readable form"). Single-call
passthrough into the sibling `ValidateSource` static method, followed
by the `.Count == 0` predicate per the cached
`rf-dart-length-isempty-to-csharp-count` finding (from
mode_table.dart.md). The expression-bodied form is preferred over a
brace-block `{ return ValidateSource(source).Count == 0; }` for
one-statement passthrough methods — the rendering is functionally
equivalent and the project precedent across the corpus
(`runtime.dart.md`'s thin-facade passthroughs) prefers the
expression-body shape. Dart `String` → C# `string` (primitive alias,
cached idiom). The result of `ValidateSource(source)` is a fresh
`List<PmtError>` that is discarded after the `.Count` read — no leak;
the GC reclaims it (Microsoft Learn "Fundamentals of garbage
collection": short-lived allocations in Gen 0 are cheap).

**Idiom id:** null
**Research finding id:**
`rf-dart-method-passthrough-to-csharp-method-passthrough`

**Nuance — verbatim from convspec:** Two load-bearing nuances. (1)
Allocation-discard pattern: each `IsValid` call ALLOCATES a
`List<PmtError>` via `ValidateSource`, reads its `Count`, then
discards the list. This is observable cost — codegen MUST NOT
"optimise" by introducing an early-exit `ValidateSourceCount(source)`
helper that avoids the list allocation; that would (a) require a new
public surface and (b) diverge from the Dart source's
single-source-of-truth pattern (`ValidateSource` is THE entry-point;
`IsValid` is a sugar). Future codegen MAY introduce such an
optimisation as a separate proposal, but the spec-faithful port keeps
the allocation. (2) Boolean-predicate parity: Dart `List<T>.isEmpty`
(getter, O(1) on `List<T>`) and C# `List<T>.Count == 0` (property +
comparison, O(1)) are semantically equivalent — both return `true`
iff the list has zero elements (api.dart.dev `List.isEmpty`:
"Whether the collection has no elements"; Microsoft Learn
`List<T>.Count`: "Gets the number of elements contained in the
List<T>"). No async / Stream / Future / isolate / value-vs-reference
concerns (US2-AS4: well-known nuances correctly absent — synchronous
passthrough returning a value-type `bool`).

### Construct 5 — `dart.static_method.validate_and_throw_aggregate_exception_on_nonempty` → C# validate-or-throw

**Source form:** `static void assertValid(String source) { final
errors = validateSource(source); if (errors.isNotEmpty) { throw
PmtErrors(errors); } }`

**Target decision:** Emit `public static void AssertValid(string
source) { var errors = ValidateSource(source); if (errors.Count != 0)
throw new PmtErrors(errors); }`. Three steps: (a) `var errors =
ValidateSource(source);` runs the full validation pipeline. (b) `if
(errors.Count != 0)` guards the throw via the cached
`rf-dart-length-isempty-to-csharp-count` finding — Dart `errors
.isNotEmpty` → C# `errors.Count != 0` (mirrors the source's positive
form; the alternative `errors.Any()` LINQ rendering is REJECTED —
`Count != 0` is O(1) on `List<T>` whereas `Any()` allocates an
enumerator and the project precedent across the corpus prefers
`Count != 0`/`Count == 0` over `Any()`/`!Any()`). (c) `throw new
PmtErrors(errors);` — the Dart implicit-`new` becomes explicit `new`
per the project Dart→C# constructor-call discipline; the `errors`
`List<PmtError>` is passed into `PmtErrors`'s
`IReadOnlyList<PmtError>` parameter per errors.dart.md's spec
(variance: `List<T>` implements `IReadOnlyList<T>` — Microsoft Learn
`List<T>`: "Implements the `IReadOnlyList<T>` interface" — implicit
upcast at the call-site, no explicit cast needed). Return type `void`
preserved verbatim (Dart `void` ↔ C# `void`). The brace-block body is
preferred over a single-line expression-bodied form here because the
body has TWO statements (assignment + conditional throw); only
single-statement bodies use `=>` per the project precedent.

**Idiom id:** null
**Research finding id:**
`rf-dart-validate-and-throw-aggregate-custom-exception-to-csharp-throw-custom-exception`

**Nuance — verbatim from convspec:** Three load-bearing nuances. (1)
Aggregate-exception alias semantics: the `PmtErrors(errors)` ctor
STORES THE CALLER'S LIST reference (per errors.dart.md's
`PmtErrors(IReadOnlyList<PmtError> errors)` aliasing semantics — NO
defensive copy). The C# port preserves the aliasing verbatim: the
`errors` `List<PmtError>` built by `ValidateSource` is the SAME
reference that ends up in `PmtErrors.Errors` (exposed as
`IReadOnlyList<PmtError>` per errors.dart.md). Codegen MUST NOT
introduce a defensive `errors.AsReadOnly()` / `new
List<PmtError>(errors)` copy here — that would diverge from Dart
semantics AND from errors.dart.md's explicit aliasing contract. (2)
Exception class nuance: Dart `implements Exception` becomes C# `:
Exception` (per errors.dart.md). Throw-and-catch semantics are
preserved exactly: Dart `throw PmtErrors(errors)` propagates up the
call stack as an exception; C# `throw new PmtErrors(errors)`
propagates the same way (Microsoft Learn "throw statement": "throws
an exception. Execution of the current code path is terminated, and
control passes to the nearest enclosing `catch` clause"). The Dart
`throw` does NOT require `new` (Dart constructors are callable
without `new`); C# requires `new` explicitly. (3)
Single-source-of-truth: `AssertValid` is a thin wrapper around
`ValidateSource` + a throw — codegen MUST NOT inline the pipeline
(lexer → parser → validator) directly into `AssertValid`. The Dart
source delegates to `validateSource(source)`; the C# port delegates
to `ValidateSource(source)`. Inlining would (a) duplicate the
pipeline (DRY violation) and (b) make future changes to
`ValidateSource` (e.g., new pipeline stages) require parallel changes
in `AssertValid` — diverging from the source's "one pipeline, two
entry-points" design. No async / Stream / Future / isolate concerns
(US2-AS4: synchronous validate-or-throw).

### Cross-file invariants relied upon

These are recorded here for codegen traceability (mirror convspec
Notes — NOT respecified):

- `Lexer` reference type from `lexer.dart.md` — `Lexer(string source)`
  positional ctor, `public List<Token> Tokenize()` returning a fresh
  list of tokens with terminal EOF sentinel.
- `Parser` reference type from `parser.dart.md` — `Parser(List<Token>
  tokens)` positional ctor, `public Module ParseModule()` returning a
  `Module` AST.
- `Module` reference type from `ast.dart.md` — declared with
  `IReadOnlyList<TypeDef> TypeDefs`, `IReadOnlyList<ProcDecl>
  ProcDeclarations`, `IReadOnlyList<Procedure> Procedures`. The Dart
  source `validator.dart` ADDITIONALLY references `ast.modeDeclarations`
  and `ast.typeDefinitions`. The pmt subsystem expects a
  `Module.modeDeclarations: List<ModeDeclaration>` getter and a
  `Module.typeDefinitions` view of `typeDefs` — both assumed present
  on the C# `Module` as `Module.ModeDeclarations: IReadOnlyList<
  ModeDeclaration>` and `Module.TypeDefs` reused for
  `typeDefinitions`. NOT escalated per convspec (conversion choice is
  unambiguous; what's missing is an upstream Dart surface).
- `ModeDeclaration` cross-file type — `IReadOnlyList<ModeDeclaration>
  ModeDeclarations` parameter type for `ModeTable.FromDeclarations`
  per mode_table.dart.md.
- `ModeTable` reference type from `mode_table.dart.md` — static
  factory `FromDeclarations(IReadOnlyList<ModeDeclaration>) ->
  ModeTable`.
- `TypeTable` reference type from `type_table.dart.md` — static
  factory `FromModule(Module) -> TypeTable`.
- `PmtChecker` reference type from `checker.dart.md` — positional
  ctor `PmtChecker(ModeTable modeTable)`; instance method
  `CheckProcedure(Procedure) -> List<PmtError>`.
- `TypeChecker` reference type from `type_checker.dart.md` —
  positional ctor `TypeChecker(TypeTable typeTable, ModeTable
  modeTable)`; instance method `CheckModule(Module) -> List<
  TypeError>` (with `TypeError` exposing `Message`/`Line`/`Column`
  properties).
- `PmtError` sealed value class from `errors.dart.md` — positional
  ctor `PmtError(string message, int line, int column)`,
  `IEquatable<PmtError>`, get-only `Message`/`Line`/`Column`
  properties.
- `PmtErrors : Exception` from `errors.dart.md` — ctor `PmtErrors(
  IReadOnlyList<PmtError> errors)` aliasing the caller's list (NO
  defensive copy); `IReadOnlyList<PmtError> Errors` get-only
  property; `ToString()` override.

## 3. Decomposed Task Units

- T1: Emit `public static class PmtValidator` in `lib/compiler/pmt/validator.cs` with PascalCased identifier and `public` access. — done
- T2: Emit `public static List<PmtError> ValidateSource(string source)` four-stage allocation pipeline (`new Lexer(source)` → `lexer.Tokenize()` → `new Parser(tokens)` → `parser.ParseModule()` → `return ValidateAst(ast)`) using `var` locals. — done
- T3: Emit `public static List<PmtError> ValidateAst(Module ast)` with silent-skip early-return `new List<PmtError>()` on `ast.ModeDeclarations.Count == 0`. — done
- T4: Inside `ValidateAst`, build `ModeTable.FromDeclarations(ast.ModeDeclarations)` and `TypeTable.FromModule(ast)` via named-static factories. — done
- T5: Inside `ValidateAst`, instantiate `new PmtChecker(modeTable)` and the single shared accumulator `var errors = new List<PmtError>();`. — done
- T6: Inside `ValidateAst`, emit `foreach (var proc in ast.Procedures) { errors.AddRange(srswChecker.CheckProcedure(proc)); }` (SRSW phase 1). — done
- T7: Inside `ValidateAst`, emit conditional type-checking guard `if (ast.TypeDefs.Count != 0) { ... }` wrapping `new TypeChecker(typeTable, modeTable)` + `typeChecker.CheckModule(ast)`. — done
- T8: Inside the conditional, emit the `TypeError`→`PmtError` mapping loop `foreach (var te in typeErrors) { errors.Add(new PmtError(te.Message, te.Line, te.Column)); }` — single-accumulator in-place `Add`, NOT LINQ `Select`. — done
- T9: Return the shared `errors` accumulator at the end of `ValidateAst` (SRSW errors first, then type errors — preserve textual ordering). — done
- T10: Emit `public static bool IsValid(string source) => ValidateSource(source).Count == 0;` as an expression-bodied member; do NOT optimise away the allocation. — done
- T11: Emit `public static void AssertValid(string source)` as a two-statement brace-block body: `var errors = ValidateSource(source); if (errors.Count != 0) throw new PmtErrors(errors);` — preserve list-reference aliasing into `PmtErrors.Errors` (no defensive copy). — done
- T12: Add `using` directives for the eight referenced sibling units (or `using` of the GLP.Compiler / GLP.Compiler.Pmt namespaces depending on target namespace policy) — `Lexer`, `Parser`, `Module`, `ModeTable`, `TypeTable`, `PmtChecker`, `TypeChecker`, `PmtError`, `PmtErrors`. — done

## 4. Research Findings

None required. Every construct resolves against authoritative Microsoft
Learn + dart.dev sources cited verbatim in the RATIFIED convspec at
`.codeconv/conversion-specs/lib/compiler/pmt/validator.dart.md` — five
rf-* findings (two CACHED from prior 018 corpus files
`abandon.dart.md` / `commit.dart.md` / `suspend_ops.dart.md` /
`runtime.dart.md`, and three NEW: pipeline-method,
two-phase-validation, validate-and-throw-aggregate-custom-exception)
all grounded in Microsoft Learn (Static Classes and Static Class
Members, new operator, Capitalization Conventions, Expression-bodied
members, `List<T>.AddRange`, `List<T>.Count`, throw statement,
Fundamentals of garbage collection, `List<T>` implements
`IReadOnlyList<T>`) and dart.dev (Constructors language tour,
api.dart.dev `List.addAll` / `List.isEmpty`, Error handling — throw).
Per FR-024 the convspec corpus is reused verbatim; no fresh research
performed at the plan stage.

## 5. Consistency Pass

fixed — derived from the RATIFIED convspec at
`.codeconv/conversion-specs/lib/compiler/pmt/validator.dart.md`
(`source_sha256: 448fc123513f79f1a1f9a0444d1c4525a10d38163a5471cdae5b41e0c9eba4e6`,
matching the source file's sha256 computed at plan-time). All five
constructs in this plan mirror the convspec's `constructs:` block
verbatim (`construct_key`, `source_form`, `target_decision`,
`idiom_id`, `research_finding_id`, `nuance`). All five conversion
units in §2 mirror the convspec's `conversion_units:` block verbatim.
All cross-file invariants in §2 mirror the convspec's Notes section
verbatim. The convspec records zero escalations; this plan records
zero escalations.

## 6. Escalations

None.
