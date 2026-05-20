# Conversion Spec — lib/compiler/pmt/validator.dart

> Conversion-spec artifact (FR-011) for `lib/compiler/pmt/validator.dart`.
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block below.

```yaml
schema_version: 1
source_path: lib/compiler/pmt/validator.dart
source_sha256: 448fc123513f79f1a1f9a0444d1c4525a10d38163a5471cdae5b41e0c9eba4e6
target_code_unit: lib/compiler/pmt/validator.cs
constructs:
  - construct_key: dart.utility_class.static_only_holder
    source_form: >-
      class PmtValidator { static List<PmtError> validateSource(String source)
      {...} static List<PmtError> validateAst(Module ast) {...} static bool
      isValid(String source) {...} static void assertValid(String source)
      {...} } — a Dart class containing four `static` methods, no instance
      fields, no instance constructor, no instance members. Used as a
      namespacing facade for the PMT validation entry-points.
    target_decision: >-
      Emit a C# `public static class PmtValidator` (sealed + abstract by the
      `static` modifier — cannot be instantiated; Microsoft Learn "Static
      Classes and Static Class Members": "A static class is basically the
      same as a non-static class, but there's one difference: a static class
      can't be instantiated. … you access the members of a static class by
      using the class name itself"). Containing exactly four static
      members `ValidateSource`, `ValidateAst`, `IsValid`, `AssertValid`. A
      non-static class with all-static members is REJECTED per the cached
      idiom — the Dart source's class is callable only via
      `PmtValidator.validateSource(...)` etc. (never instantiated) and
      `static class` makes the no-instantiation contract a compile-time
      guarantee on the .NET side, matching the source's design intent.
      Identical structural decision as `abandon.dart.md`'s `AbandonOps`,
      `commit.dart.md`'s commit-facade, and `suspend_ops.dart.md`'s
      suspend-facade. Class-level `public` access mirrors Dart's
      library-public (no leading underscore on `PmtValidator`).
    idiom_id: rf-dart-static-only-holder-to-csharp-static-class
    research_finding_id: rf-dart-static-only-holder-to-csharp-static-class
    nuance: >-
      Static-class contract (explicitly addressed, US2-AS4). In Dart, a
      class with only static members is still an instantiable type
      (`PmtValidator()` would compile and yield a zero-field instance);
      the convention is "treat the class name as a namespace". C#'s
      `static class` makes that convention a compile-time invariant
      (Microsoft Learn: "a static class can't be instantiated") — a
      strictly tighter contract than the Dart source. The narrowing is
      strictly correct here because the Dart source never instantiates
      `PmtValidator` and has no instance state to preserve. The
      .NET-idiomatic narrowing also implicitly seals the type and
      forbids subclassing, reinforcing the source's "this is a
      namespacing facade, not a polymorphism surface" intent — same
      rationale as `abandon.dart.md`'s `AbandonOps`. Value-vs-reference:
      static classes have no instances, so neither value nor reference
      semantics apply to the type itself; per-method signatures govern
      argument/return semantics (handled per-construct below). No
      `Stream`/`Future`/async/isolate concerns — all four methods are
      synchronous static dispatch. Null-safety at the type level: not
      applicable.
  - construct_key: dart.static_method.pipeline_lex_parse_validate_returning_list
    source_form: >-
      static List<PmtError> validateSource(String source) { final lexer =
      Lexer(source); final tokens = lexer.tokenize(); final parser = Parser(
      tokens); final ast = parser.parseModule(); return validateAst(ast); }
    target_decision: >-
      Emit `public static List<PmtError> ValidateSource(string source) { var
      lexer = new Lexer(source); var tokens = lexer.Tokenize(); var parser =
      new Parser(tokens); var ast = parser.ParseModule(); return
      ValidateAst(ast); }`. Four-stage straight-line pipeline: construct
      `Lexer` with the source, call `Tokenize()` returning `List<Token>`
      (per `lexer.dart.md` conversion unit "public List<Token> Tokenize()"),
      construct `Parser` with the tokens (per `parser.dart.md` conversion
      unit "class Parser ... List<Token> tokens"), call `ParseModule()`
      returning `Module` (per `parser.dart.md` conversion unit "public
      Module ParseModule()"), tail-call the sibling static `ValidateAst`.
      Dart `final` locals → C# `var` locals (cached `rf-dart-final-local-
      to-csharp-var` from multiple prior corpus files; the locals are
      assigned-once / never reassigned but inferred types are mutable
      reference handles — same semantics in both languages). The two
      `new` allocations are explicit per Microsoft Learn "The new
      operator": "creates a new instance of a type"; Dart's implicit-
      `new` constructor call (`Lexer(source)`) becomes the explicit
      `new Lexer(source)` on the C# side. Static-method-to-static-method
      dispatch (`return validateAst(ast)`) becomes `return ValidateAst(
      ast)` — unqualified within the same `static class` per Microsoft
      Learn "Static members in static classes are accessed from within
      the class without the type-name qualifier".
    idiom_id: null
    research_finding_id: rf-dart-static-pipeline-method-to-csharp-static-pipeline-method
    nuance: >-
      Three load-bearing nuances. (1) Pipeline allocation parity: the
      Dart source ALLOCATES TWO short-lived helper objects (`Lexer`,
      `Parser`) per call. The C# port preserves that allocation pattern
      verbatim — codegen MUST NOT hoist `Lexer`/`Parser` to static or
      pooled instances. The Dart `Lexer` and `Parser` carry per-instance
      mutable cursor state (per `lexer.dart.md` "single instance built
      once and its Tokenize() method mutates the cursor" and
      `parser.dart.md` "`Parse()` / `ParseModule()` mutate `_current`")
      — concurrent reuse would corrupt cursors. Per-call fresh
      construction is the contract. (2) Method-name casing: Dart
      lowerCamelCase `validateSource` / `validateAst` → C# PascalCased
      `ValidateSource` / `ValidateAst` per the project-wide Dart→C#
      method-naming discipline (Microsoft Learn "Capitalization
      Conventions": "PascalCasing convention … is used for all
      identifiers except parameter names"); `tokenize` → `Tokenize`,
      `parseModule` → `ParseModule` per the respective sibling specs.
      (3) Return-type fidelity: the Dart `List<PmtError>` return type
      stays `List<PmtError>` (NOT `IReadOnlyList<PmtError>`) on the
      C# side because the caller `AssertValid` checks `errors.IsEmpty
      == false` / `errors.Count != 0` and may pass the list into the
      `PmtErrors(IReadOnlyList<PmtError> errors)` exception ctor (per
      `errors.dart.md`); both call-sites accept a `List<PmtError>`.
      No async / Stream / Future / isolate concerns — purely
      synchronous lexer→parser→checker chain (US2-AS4: well-known
      nuance correctly absent).
  - construct_key: dart.static_method.validate_ast_with_short_circuits_and_two_phase_checking
    source_form: >-
      static List<PmtError> validateAst(Module ast) { if (ast
      .modeDeclarations.isEmpty) return []; final modeTable = ModeTable
      .fromDeclarations(ast.modeDeclarations); final typeTable = TypeTable
      .fromModule(ast); final srswChecker = PmtChecker(modeTable); final
      errors = <PmtError>[]; for (final proc in ast.procedures) { errors
      .addAll(srswChecker.checkProcedure(proc)); } if (ast.typeDefinitions
      .isNotEmpty) { final typeChecker = TypeChecker(typeTable, modeTable);
      final typeErrors = typeChecker.checkModule(ast); for (final te in
      typeErrors) { errors.add(PmtError(te.message, te.line, te.column));
      } } return errors; }
    target_decision: >-
      Emit `public static List<PmtError> ValidateAst(Module ast) { if (ast
      .ModeDeclarations.Count == 0) return new List<PmtError>(); var
      modeTable = ModeTable.FromDeclarations(ast.ModeDeclarations); var
      typeTable = TypeTable.FromModule(ast); var srswChecker = new
      PmtChecker(modeTable); var errors = new List<PmtError>(); foreach
      (var proc in ast.Procedures) { errors.AddRange(srswChecker
      .CheckProcedure(proc)); } if (ast.TypeDefs.Count != 0) { var
      typeChecker = new TypeChecker(typeTable, modeTable); var typeErrors
      = typeChecker.CheckModule(ast); foreach (var te in typeErrors) {
      errors.Add(new PmtError(te.Message, te.Line, te.Column)); } }
      return errors; }`. Five cross-file invariants: `Module.
      ModeDeclarations` is referenced as a property on `Module`, but
      ast.dart.md's `Module` spec DOES NOT currently declare this
      member (see Notes — the Dart source references `ast
      .modeDeclarations` against a `Module` that has no such getter; the
      pmt subsystem assumes a `Module.modeDeclarations: List<
      ModeDeclaration>` getter that the ast.dart spec must add to remain
      consistent — handled as a cross-file invariant assumption, parallel
      to mode_table.dart.md's "ModeDeclaration shape fixed elsewhere"
      treatment). `ModeTable.FromDeclarations(IReadOnlyList<
      ModeDeclaration>) -> ModeTable` per mode_table.dart.md. `TypeTable.
      FromModule(Module) -> TypeTable` per type_table.dart.md (the prior
      sibling spec). `new PmtChecker(modeTable)` per checker.dart.md's
      positional ctor. `new TypeChecker(typeTable, modeTable)` per
      type_checker.dart.md's positional ctor with two injected deps. Dart
      `<PmtError>[]` typed-empty-list literal → C# `new List<PmtError>()`
      via the cached `rf-dart-list-typed-literal-and-addall-to-csharp-
      list-and-addrange` (from `checker.dart.md` / `type_checker.dart
      .md`). Dart `errors.addAll(...)` → C# `errors.AddRange(...)` per
      the same cached finding. Dart `for (final x in xs)` → C# `foreach
      (var x in xs)`. Dart `.isEmpty` → C# `.Count == 0` and Dart
      `.isNotEmpty` → C# `.Count != 0` per the cached
      `rf-dart-length-isempty-to-csharp-count` finding (from
      mode_table.dart.md). The early-return empty-list `return [];`
      becomes `return new List<PmtError>()` — NOT `Array.Empty<PmtError>
      ()` — because the return type is `List<PmtError>` (a mutable
      concrete type) and the caller (`AssertValid`) may pass it into
      the `PmtErrors` exception ctor which accepts `IReadOnlyList<
      PmtError>`; the concrete-return-fresh-empty rendering matches the
      precedent in `checker.dart.md`'s `CheckClauseAgainstModes`
      early-success `return new List<PmtError>()`. The inner conversion
      `errors.add(PmtError(te.message, te.line, te.column))` becomes
      `errors.Add(new PmtError(te.Message, te.Line, te.Column))` —
      explicit `new`, PascalCased property access on `TypeError`
      (cross-file invariant from type_checker.dart.md / errors.dart.md).
    idiom_id: null
    research_finding_id: rf-dart-two-phase-validation-with-error-accumulator-and-error-type-conversion-to-csharp-list-addrange-and-mapping
    nuance: >-
      Four load-bearing nuances. (1) Silent-skip semantics on empty
      mode-declarations: the source's doc-comment "No mode declarations
      = nothing to check" mandates SILENT early-return with an empty
      list, NOT an error. The C# port MUST preserve the silent skip —
      promoting to an error or to `throw` would change observable
      behaviour under FR-023 / FR-024. Same rationale as `checker.dart
      .md`'s `CheckProcedure` silent-skip on null-or-empty allModes.
      (2) Optional type-checking phase: the `if (ast.typeDefinitions
      .isNotEmpty)` branch is a CONDITIONAL second checking pass —
      type checking is only performed when type definitions exist.
      Codegen MUST preserve the optionality verbatim; "always invoke
      type checking" would (a) waste cycles and (b) potentially
      surface spurious errors if `TypeChecker` doesn't handle the
      empty-type-defs case gracefully. (3) TypeError→PmtError mapping:
      the inner loop converts each `TypeError` (per type_checker.dart
      .md's surface, with `Message`/`Line`/`Column` properties) into a
      `PmtError` (per errors.dart.md's surface, also with `Message`/
      `Line`/`Column`). The mapping discards any TypeError-specific
      structure (e.g., source-form metadata) and keeps ONLY the three
      shared fields — same lossy projection in both languages; the
      C# port preserves it verbatim. A naïve "preserve TypeError" port
      would break the doc-contracted return type `List<PmtError>` and
      would force callers to handle two error-shapes. (4) Accumulator-
      mutation pattern: `errors` is constructed ONCE at the top of the
      method and mutated by BOTH the SRSW-checking loop and the
      type-checking loop. Codegen MUST preserve this single-accumulator
      design — splitting into `srswErrors` and `typeErrors` lists and
      concatenating at the end would (a) change the textual ordering
      of errors (currently: ALL SRSW errors FIRST, then ALL type
      errors, mirroring source order) and (b) introduce an unnecessary
      intermediate allocation. The Dart `addAll`/C# `AddRange` and
      Dart `add`/C# `Add` mutations are in-place on the shared
      accumulator — semantically identical, no boxing/copying. No
      async / Stream / Future / isolate concerns (US2-AS4: well-known
      nuance correctly absent — purely synchronous orchestration).
  - construct_key: dart.static_method.thin_passthrough_returning_isempty_of_inner_call
    source_form: >-
      static bool isValid(String source) { return validateSource(source)
      .isEmpty; }
    target_decision: >-
      Emit `public static bool IsValid(string source) => ValidateSource(
      source).Count == 0;` as a single-expression body (`=>`
      expression-bodied member; Microsoft Learn "Expression-bodied
      members": "Expression body definitions let you provide a member's
      implementation in a very concise, readable form"). Single-call
      passthrough into the sibling `ValidateSource` static method,
      followed by the `.Count == 0` predicate per the cached
      `rf-dart-length-isempty-to-csharp-count` finding (from
      mode_table.dart.md). The expression-bodied form is preferred over
      a brace-block `{ return ValidateSource(source).Count == 0; }` for
      one-statement passthrough methods — the rendering is functionally
      equivalent and the project precedent across the corpus
      (`runtime.dart.md`'s thin-facade passthroughs) prefers the
      expression-body shape. Dart `String` → C# `string` (primitive
      alias, cached idiom). The result of `ValidateSource(source)` is a
      fresh `List<PmtError>` that is discarded after the `.Count` read
      — no leak; the GC reclaims it (Microsoft Learn "Fundamentals of
      garbage collection": short-lived allocations in Gen 0 are cheap).
    idiom_id: null
    research_finding_id: rf-dart-method-passthrough-to-csharp-method-passthrough
    nuance: >-
      Two load-bearing nuances. (1) Allocation-discard pattern: each
      `IsValid` call ALLOCATES a `List<PmtError>` via `ValidateSource`,
      reads its `Count`, then discards the list. This is observable
      cost — codegen MUST NOT "optimise" by introducing an early-exit
      `ValidateSourceCount(source)` helper that avoids the list
      allocation; that would (a) require a new public surface and (b)
      diverge from the Dart source's single-source-of-truth pattern
      (`ValidateSource` is THE entry-point; `IsValid` is a sugar).
      Future codegen MAY introduce such an optimisation as a separate
      proposal, but the spec-faithful port keeps the allocation. (2)
      Boolean-predicate parity: Dart `List<T>.isEmpty` (getter, O(1) on
      `List<T>`) and C# `List<T>.Count == 0` (property + comparison,
      O(1)) are semantically equivalent — both return `true` iff the
      list has zero elements (api.dart.dev `List.isEmpty`: "Whether the
      collection has no elements"; Microsoft Learn `List<T>.Count`:
      "Gets the number of elements contained in the List<T>"). No
      async / Stream / Future / isolate / value-vs-reference concerns
      (US2-AS4: well-known nuances correctly absent — synchronous
      passthrough returning a value-type `bool`).
  - construct_key: dart.static_method.validate_and_throw_aggregate_exception_on_nonempty
    source_form: >-
      static void assertValid(String source) { final errors =
      validateSource(source); if (errors.isNotEmpty) { throw PmtErrors(
      errors); } }
    target_decision: >-
      Emit `public static void AssertValid(string source) { var errors =
      ValidateSource(source); if (errors.Count != 0) throw new PmtErrors(
      errors); }`. Three steps: (a) `var errors = ValidateSource(source);`
      runs the full validation pipeline. (b) `if (errors.Count != 0)`
      guards the throw via the cached `rf-dart-length-isempty-to-csharp-
      count` finding — Dart `errors.isNotEmpty` → C# `errors.Count != 0`
      (mirrors the source's positive form; the alternative `errors.Any()`
      LINQ rendering is REJECTED — `Count != 0` is O(1) on `List<T>`
      whereas `Any()` allocates an enumerator and the project precedent
      across the corpus prefers `Count != 0`/`Count == 0` over `Any()`/
      `!Any()`). (c) `throw new PmtErrors(errors);` — the Dart implicit-
      `new` becomes explicit `new` per the project Dart→C# constructor-
      call discipline; the `errors` `List<PmtError>` is passed into
      `PmtErrors`'s `IReadOnlyList<PmtError>` parameter per errors.dart
      .md's spec (variance: `List<T>` implements `IReadOnlyList<T>` —
      Microsoft Learn `List<T>`: "Implements the `IReadOnlyList<T>`
      interface" — implicit upcast at the call-site, no explicit cast
      needed). Return type `void` preserved verbatim (Dart `void` ↔ C#
      `void`). The brace-block body is preferred over a single-line
      expression-bodied form here because the body has TWO statements
      (assignment + conditional throw); only single-statement bodies
      use `=>` per the project precedent.
    idiom_id: null
    research_finding_id: rf-dart-validate-and-throw-aggregate-custom-exception-to-csharp-throw-custom-exception
    nuance: >-
      Three load-bearing nuances. (1) Aggregate-exception alias
      semantics: the `PmtErrors(errors)` ctor STORES THE CALLER'S LIST
      reference (per errors.dart.md's `PmtErrors(IReadOnlyList<PmtError>
      errors)` aliasing semantics — NO defensive copy). The C# port
      preserves the aliasing verbatim: the `errors` `List<PmtError>`
      built by `ValidateSource` is the SAME reference that ends up in
      `PmtErrors.Errors` (exposed as `IReadOnlyList<PmtError>` per
      errors.dart.md). Codegen MUST NOT introduce a defensive
      `errors.AsReadOnly()` / `new List<PmtError>(errors)` copy here —
      that would diverge from Dart semantics AND from errors.dart.md's
      explicit aliasing contract. (2) Exception class nuance: Dart
      `implements Exception` becomes C# `: Exception` (per
      errors.dart.md). Throw-and-catch semantics are preserved exactly:
      Dart `throw PmtErrors(errors)` propagates up the call stack as
      an exception; C# `throw new PmtErrors(errors)` propagates the
      same way (Microsoft Learn "throw statement": "throws an
      exception. Execution of the current code path is terminated, and
      control passes to the nearest enclosing `catch` clause"). The
      Dart `throw` does NOT require `new` (Dart constructors are
      callable without `new`); C# requires `new` explicitly. (3)
      Single-source-of-truth: `AssertValid` is a thin wrapper around
      `ValidateSource` + a throw — codegen MUST NOT inline the
      pipeline (lexer → parser → validator) directly into
      `AssertValid`. The Dart source delegates to `validateSource(
      source)`; the C# port delegates to `ValidateSource(source)`.
      Inlining would (a) duplicate the pipeline (DRY violation) and
      (b) make future changes to `ValidateSource` (e.g., new pipeline
      stages) require parallel changes in `AssertValid` — diverging
      from the source's "one pipeline, two entry-points" design. No
      async / Stream / Future / isolate concerns (US2-AS4: synchronous
      validate-or-throw).
conversion_units:
  - "public static class PmtValidator (sealed + abstract by `static` modifier; four static members; no instance state; library-public access; namespacing facade — NOT a polymorphism surface)"
  - "public static List<PmtError> ValidateSource(string source) (four-stage allocation pipeline: new Lexer(source) → lexer.Tokenize() → new Parser(tokens) → parser.ParseModule() → return ValidateAst(ast); per-call fresh helper-object allocation preserves mutable-cursor isolation)"
  - "public static List<PmtError> ValidateAst(Module ast) (silent-skip early-return `new List<PmtError>()` on `ast.ModeDeclarations.Count == 0`; build ModeTable via ModeTable.FromDeclarations; build TypeTable via TypeTable.FromModule; instantiate `new PmtChecker(modeTable)`; foreach over ast.Procedures with AddRange of CheckProcedure results; conditional type-checking branch guarded by `ast.TypeDefs.Count != 0` instantiating `new TypeChecker(typeTable, modeTable)` then mapping each `TypeError` to `new PmtError(te.Message, te.Line, te.Column)`; single-accumulator mutation pattern preserved — SRSW errors FIRST then type errors)"
  - "public static bool IsValid(string source) => ValidateSource(source).Count == 0 (expression-bodied; allocates and discards a List<PmtError> per call — observable cost preserved verbatim, no Count-only helper introduced)"
  - "public static void AssertValid(string source) (two-statement body: `var errors = ValidateSource(source);` then `if (errors.Count != 0) throw new PmtErrors(errors);`; aliasing semantics preserved — the same List<PmtError> reference becomes PmtErrors.Errors via implicit List<T>→IReadOnlyList<T> upcast at the ctor call-site, NO defensive copy)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

Every non-trivial construct in this file is resolved against rf-* findings
from the prior 018 convspec corpus (FR-024: never re-research; cached findings
reused verbatim), with FOUR fresh rf-* findings recorded for the
validator-specific shapes (pipeline-method, two-phase-validation, method-
passthrough, validate-and-throw-aggregate-custom-exception) and ONE cached
finding reused from `abandon.dart.md` for the static-only-holder shape. Every
construct records BOTH a deep-analysis basis AND a researched-pattern basis
per SC-006 / US2-AS4. Zero escalations.

### rf-dart-static-only-holder-to-csharp-static-class (CACHED, reused from abandon.dart / commit.dart / suspend_ops.dart)

- Deep analysis: `PmtValidator` is STRUCTURALLY a static-only holder — four
  `static` methods, no instance fields, no constructor, no instance members.
  The class identifier is used solely as a namespacing prefix (callers write
  `PmtValidator.validateSource(...)`). The cached idiom from
  `abandon.dart.md` (`AbandonOps`), `commit.dart.md`, and `suspend_ops.dart
  .md` applies verbatim.
- Authoritative Dart and .NET: cached from the prior corpus files; no
  re-research per FR-024. Microsoft Learn "Static Classes and Static Class
  Members" — decisive on the `static class` rendering.
- Authoritative both sides; cached. The intent-narrowing (Dart "convention"
  → C# "compile-time invariant") is strictly correct because the source
  never instantiates `PmtValidator` and has no instance state.

### rf-dart-static-pipeline-method-to-csharp-static-pipeline-method (NEW)

- Deep analysis: `validateSource` is a four-stage straight-line pipeline
  (`new Lexer(source)` → `lexer.tokenize()` → `new Parser(tokens)` →
  `parser.parseModule()` → static-sibling-call). The pattern composes
  multiple short-lived helper objects with mutable per-instance cursor
  state; codegen MUST NOT hoist them. This is the first appearance of an
  end-to-end "lexer→parser→checker" pipeline orchestrator in the 018
  corpus — recording a fresh finding documents the canonical C# rendering.
- Authoritative Dart (api.dart.dev — `Lexer` / `Parser` are project types;
  pipeline construct itself is `final x = X(...);` chaining): dart.dev
  language tour, "Constructors": "instances of a class are created using a
  constructor … the `new` keyword became optional in Dart 2". Decisive on
  the call-site shape.
- Authoritative .NET
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/new-operator):
  Microsoft Learn — "The `new` operator creates a new instance of a type"
  / "Static members in static classes are accessed from within the class
  without the type-name qualifier". Decisive on the C# rendering: explicit
  `new` for each helper, unqualified static-sibling call.
- Authoritative both sides; recorded as a NEW finding. Composes with the
  cached `rf-dart-final-local-to-csharp-var` (multiple prior corpus files)
  for the `var` local-typing convention and with the project's PascalCased
  method-naming discipline. The "per-call fresh allocation" contract
  preserves the Dart source's mutable-cursor isolation verbatim.

### rf-dart-two-phase-validation-with-error-accumulator-and-error-type-conversion-to-csharp-list-addrange-and-mapping (NEW)

- Deep analysis: `validateAst` orchestrates TWO checker phases (SRSW via
  `PmtChecker`, optional type-checking via `TypeChecker`) into a SINGLE
  error accumulator, with a lossy TypeError→PmtError mapping in the
  conditional second phase. This is the first appearance of a multi-phase
  checker orchestrator with cross-error-type projection in the 018 corpus
  — recording a fresh finding documents the canonical C# rendering.
- Authoritative Dart (api.dart.dev `List.addAll`: "Appends all objects of
  iterable to the end of this list. Extends the length of the list by the
  number of objects in iterable"): decisive on the bulk-append semantics
  preserved by the C# port via `AddRange`.
- Authoritative .NET
  (https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.addrange):
  Microsoft Learn `List<T>.AddRange` — "Adds the elements of the specified
  collection to the end of the `List<T>`". Semantically identical to Dart
  `addAll`; both mutate in-place. The TypeError→PmtError mapping uses the
  loop-and-`Add` shape (NOT LINQ `.Select(...).ToList()`) because the
  accumulator is already-allocated and shared with the prior SRSW phase
  — direct in-place `Add` preserves the single-accumulator design.
- Authoritative both sides; recorded as a NEW finding. Composes with the
  cached `rf-dart-list-typed-literal-and-addall-to-csharp-list-and-
  addrange` (from `type_checker.dart.md` / `checker.dart.md`) for the
  `<PmtError>[]` → `new List<PmtError>()` rendering and the `addAll` →
  `AddRange` mapping. The optional-second-phase guard `if (ast
  .typeDefinitions.isNotEmpty)` is preserved verbatim with the cached
  `rf-dart-length-isempty-to-csharp-count` finding.

### rf-dart-method-passthrough-to-csharp-method-passthrough (CACHED, reused from runtime.dart)

- Deep analysis: `isValid` is a one-statement thin facade that calls
  `validateSource` and tests the result's emptiness. The pattern composes
  `rf-dart-length-isempty-to-csharp-count` (from mode_table.dart.md) with
  the cached method-passthrough finding from runtime.dart.md. The
  expression-bodied member is the C# canonical rendering for single-
  statement passthroughs.
- Authoritative Dart and .NET: cached from runtime.dart.md and mode_table
  .dart.md; no re-research per FR-024. Microsoft Learn "Expression-bodied
  members": decisive on the `=>` rendering for single-statement bodies.
- Authoritative both sides; cached. The allocation-discard nuance
  (`ValidateSource` allocates a `List<PmtError>` that `IsValid` reads
  `Count` from and discards) is preserved verbatim — codegen MUST NOT
  optimise it away to a separate `ValidateSourceCount` helper.

### rf-dart-validate-and-throw-aggregate-custom-exception-to-csharp-throw-custom-exception (NEW)

- Deep analysis: `assertValid` runs `validateSource`, then conditionally
  throws a `PmtErrors` aggregate exception (defined in errors.dart) on
  non-empty result. This is the first appearance of the validate-then-
  throw-aggregate shape in the 018 corpus — recording a fresh finding
  documents the canonical C# rendering. The pattern relies on
  errors.dart.md's `PmtErrors : Exception` (with `IReadOnlyList<PmtError>
  Errors` aliasing the caller's list) — cross-file invariant.
- Authoritative Dart
  (https://dart.dev/language/error-handling#throw): dart.dev — "Dart code
  can throw and catch exceptions. Exceptions are errors indicating that
  something unexpected happened. … You can throw any non-null object as
  an exception." Decisive on the throw-without-`new` Dart syntax.
- Authoritative .NET
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/exception-handling-statements):
  Microsoft Learn "throw statement" — "throws an exception. … Execution
  of the current code path is terminated, and control passes to the
  nearest enclosing `catch` clause." Decisive on `throw new PmtErrors(
  errors);` — the explicit `new` is mandatory in C#. The implicit
  variance-upcast from `List<PmtError>` to `IReadOnlyList<PmtError>` at
  the `PmtErrors` ctor call-site is documented by Microsoft Learn
  `List<T>`: "Implements the `IReadOnlyList<T>` interface" — no
  explicit cast or copy needed.
- Authoritative both sides; recorded as a NEW finding. The aliasing-
  preservation contract (the `errors` list reference becomes
  `PmtErrors.Errors`, no defensive copy) is load-bearing and inherited
  from errors.dart.md's explicit "aliases the caller's list" contract.

## Notes

- Cross-file invariants relied on by this spec (NOT respecified here):
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
    and `ast.typeDefinitions`. **Apparent source-level surface gap:**
    `Module` in `ast.dart` does NOT currently declare `modeDeclarations`
    (no such field/getter) and `typeDefinitions` is the camelCase form of
    `typeDefs` — the pmt subsystem expects a `Module.modeDeclarations:
    List<ModeDeclaration>` getter and a `Module.typeDefinitions` view of
    `typeDefs`. The conversion spec assumes both are present on the C#
    `Module` (`Module.ModeDeclarations: IReadOnlyList<ModeDeclaration>`
    and `Module.TypeDefs` reused for `typeDefinitions`) per the pmt
    design intent; parallel to mode_table.dart.md's treatment of the
    cross-file `ModeDeclaration` type as "shape fixed elsewhere". A
    later spec-cycle on ast.dart.md SHOULD add the `ModeDeclarations`
    property to `Module`. NOT escalated under FR-013 (the conversion
    decision is unambiguous — what's missing is an upstream Dart
    surface, not a conversion choice).
  - `ModeDeclaration` cross-file type — `IReadOnlyList<ModeDeclaration>
    ModeDeclarations` parameter type for `ModeTable.FromDeclarations`
    per mode_table.dart.md; the concrete type definition lives outside
    this file's scope (same "shape fixed elsewhere" treatment as
    mode_table.dart.md).
  - `ModeTable` reference type from `mode_table.dart.md` — static factory
    `FromDeclarations(IReadOnlyList<ModeDeclaration>) -> ModeTable`.
  - `TypeTable` reference type from `type_table.dart.md` — static factory
    `FromModule(Module) -> TypeTable`.
  - `PmtChecker` reference type from `checker.dart.md` — positional ctor
    `PmtChecker(ModeTable modeTable)`; instance method
    `CheckProcedure(Procedure) -> List<PmtError>`.
  - `TypeChecker` reference type from `type_checker.dart.md` — positional
    ctor `TypeChecker(TypeTable typeTable, ModeTable modeTable)`;
    instance method `CheckModule(Module) -> List<TypeError>` (with
    `TypeError` exposing `Message`/`Line`/`Column` properties).
  - `PmtError` sealed value class from `errors.dart.md` — positional
    ctor `PmtError(string message, int line, int column)`,
    `IEquatable<PmtError>`, get-only `Message`/`Line`/`Column`
    properties.
  - `PmtErrors : Exception` from `errors.dart.md` — ctor `PmtErrors(
    IReadOnlyList<PmtError> errors)` aliasing the caller's list (NO
    defensive copy); `IReadOnlyList<PmtError> Errors` get-only
    property; `ToString()` override.
- No async / Stream / Future / await, no isolates, no late, no
  inheritance among `PmtValidator` — the file is purely synchronous,
  single-class, all-static. The well-known nuances (value-vs-reference,
  async, isolates, null-safety, enum casing) are addressed explicitly
  per construct above (US2-AS4).
- The four-stage pipeline in `ValidateSource` allocates two short-lived
  helper objects (`Lexer`, `Parser`) per call. Codegen MUST preserve the
  per-call fresh allocation — both helpers carry mutable per-instance
  cursor state and concurrent reuse would corrupt cursors. Future codegen
  MAY introduce object-pooling as a separate proposal, but the spec-
  faithful port keeps the allocation.
- The single-accumulator mutation design in `ValidateAst` (SRSW errors
  appended FIRST, then type errors) preserves the textual ordering of
  errors verbatim — codegen MUST NOT split into two lists concatenated
  at the end.
- The `IsValid` allocation-discard is observable cost preserved
  verbatim. The `AssertValid` aliasing semantics (the same
  `List<PmtError>` reference is held by both the local and the
  thrown `PmtErrors.Errors`) is also preserved verbatim with no
  defensive copy.
- Four FRESH rf-* findings recorded
  (`rf-dart-static-pipeline-method-to-csharp-static-pipeline-method`,
  `rf-dart-two-phase-validation-with-error-accumulator-and-error-type-conversion-to-csharp-list-addrange-and-mapping`,
  `rf-dart-method-passthrough-to-csharp-method-passthrough` is CACHED
  from runtime.dart.md but ALSO documented here for the
  `IsValid`-specific allocation-discard nuance,
  `rf-dart-validate-and-throw-aggregate-custom-exception-to-csharp-throw-custom-exception`).
  TWO CACHED rf-* findings reused
  (`rf-dart-static-only-holder-to-csharp-static-class`,
  `rf-dart-method-passthrough-to-csharp-method-passthrough`).
  Each grounded in authoritative Microsoft Learn + dart.dev sources
  (deep-analysis basis AND researched-pattern basis per SC-006 / US2-AS4).
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart and/or .NET official documentation, with deep-
  analysis AND researched-pattern bases recorded (SC-006); recurring
  constructs route through cached rf-* findings (SC-007).
