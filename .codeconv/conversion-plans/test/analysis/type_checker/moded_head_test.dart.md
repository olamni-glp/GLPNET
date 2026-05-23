---
path: test/analysis/type_checker/moded_head_test.dart
cycle_group_id: 108
scc_siblings: []
generated_at: 2026-05-21T16:01:18Z
source_sha256: e04d3f734b2cdac1abe63bc1acdd4cf4178743725224834979a741c750a82a9b
schema_version: 1
---

# Conversion Plan: test/analysis/type_checker/moded_head_test.dart

## 1. Source Analysis

Inspected `glp_runtime_net/test/analysis/type_checker/moded_head_test.dart`
(497 lines). The file is a Dart `package:test` unit-test file for the moded-
head construction described in `docs/type system/moded-head.md` and Paper
Definition 5.5. Observed structure (verbatim, line-attributed):

- Line 7: `import 'package:test/test.dart';` — Dart test framework.
- Lines 8-11: four `package:glp_runtime/analysis/type_checker/...` imports
  (`moded_head.dart`, `moded_term.dart`, `mode.dart`, `type_ast.dart`).
- Line 12: `import 'package:glp_runtime/compiler/ast.dart' as ast;` —
  prefix import; AST types referenced throughout as `ast.Goal`,
  `ast.VarTerm`, `ast.ListTerm`, `ast.ConstTerm`, `ast.UnderscoreTerm`,
  `ast.StructTerm`, `ast.Term`.
- Line 14: `void main()` — single per-file `package:test` entrypoint.
- THREE sibling outer `group(...)` blocks inside `main`:
  - Lines 15-261: `group('modedHead', ...)` — declares local helper
    closures `goal`, `writer`, `reader`, `nil`, `cons`, `constTerm`
    (lines 17-29); contains FOUR inner groups:
    - `group('basic construction', ...)` (lines 31-137): 2 tests
      (`'merge head: merge([X|Xs], Ys, [X?|Zs?])'`,
       `'base case: merge(Xs, [], Xs?)'`).
    - `group('variable replacement', ...)` (lines 139-209): 4 tests
      (`'writer at consume position becomes reader'`,
       `'reader at produce position becomes writer'`,
       `'reader at consume position is complemented'`,
       `'writer at produce position is complemented'`).
    - `group('anonymous variables', ...)` (lines 211-242): 1 test
      (`'each anonymous variable gets unique name'`).
    - `group('error handling', ...)` (lines 244-260): 1 test
      (`'arity mismatch throws ArityMismatchError'`).
  - Lines 263-322: `group('producedTerm', ...)` — re-declares local
    helper closures `goal`, `writer`, `reader` (lines 264-267); contains
    2 tests (`'body atom has produce mode at root'`,
    `'arity mismatch throws ArityMismatchError'`).
  - Lines 324-496: `group('explicit dual type definitions', ...)` —
    re-declares local helper closures `goal`, `writer`, `reader`, and
    ADDS `struct` (lines 325-330); contains 2 tests (`'Channel with
    explicit dual preserves internal structure'`, `'DiffList with
    explicit dual preserves internal structure'`).
- Test count: 8 (modedHead) + 2 (producedTerm) + 2 (explicit dual) = 12.
- All test callbacks are synchronous (no `async` / `Future`).
- Constructors used with Dart implicit-`new`: `ast.Goal`, `ast.VarTerm`,
  `ast.ListTerm`, `ast.ConstTerm`, `ast.UnderscoreTerm`, `ast.StructTerm`,
  `ProcDecl`, `TypeRef` (with named `isInput:`), `TypeEnvironment` (with
  two empty-map literals `{}, {}`), `TypeDef`, `StructAlt`, `ListNilAlt`,
  `ListConsAlt`, `PrimitiveModeAlt`, `DiffListAlt`.
- Named arguments observed: `isInput: true` / `isInput: false` on
  `TypeRef`; `typeEnv: typeEnv` on the third group's `modedHead(...)`
  calls (lines 397, 478). `TypeRef('T', 0, 0)` on lines 248 and 311 omits
  `isInput:` (default applies).
- Matchers observed: `equals(...)`, `isA<ModedCompound>()`,
  `isA<ModedConstant>()`, `isA<ModedVariable>()`, `isTrue`, `isFalse`,
  `isNot(equals(...))`, `startsWith('_#')`, `throwsA(isA<ArityMismatchError>())`.
- Downcasts (`as T`) used after `isA<T>` assertions to access typed
  properties (e.g. `result as ModedCompound`, `compound.args[0] as
  ModedCompound`, `arg1.args[0] as ModedVariable`).
- Raw string literal: `r'\'` (lines 473-475) — DiffList functor symbol
  (one-character backslash).
- Final-local declarations (`final ... = ...`) for `decl`, `head`,
  `result`, downcast destructurings (`compound`, `arg1`, `arg3`, `x1`,
  `xs1`, `ys`, `x3`, `zs3`, `xs1`, `nilArg`, `xs3`, `x`, `v1`, `v2`,
  `ys`, `xs`, `zs`, `typeEnv`, `arg2`, `in2`, `out2`, `a1`, `b1`).

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the RATIFIED convspec at
`.codeconv/conversion-specs/test/analysis/type_checker/moded_head_test.dart.md`
verbatim (no new decisions introduced).

- `import 'package:test/test.dart';` → `using Xunit;` at file scope (plus
  `using System;` for the `ArityMismatchError` exception assertions).
  Project namespace mirrors the Dart `test/analysis/type_checker`
  directory: `<RootNs>.Test.Analysis.TypeChecker`. (Construct
  `dart.package_test.import_directive`,
  `rf-dart-package-test-import-to-xunit-using`.)

- Four `package:glp_runtime/analysis/type_checker/...` imports → one
  bare `using <RootNs>.Analysis.TypeChecker;` directive (the four Dart
  library files all map to the same C# namespace).
  `import 'package:glp_runtime/compiler/ast.dart' as ast;` → C#
  namespace alias `using ast = <RootNs>.Compiler.Ast;` (NOT
  `using static`; the alias is used as a type prefix). (Construct
  `dart.package_under_test.import_directive`,
  `rf-dart-internal-package-import-to-csharp-using`.)

- `void main()` → ELIMINATED. xUnit discovers `[Fact]` methods by
  reflection; no per-file entrypoint exists. (Construct
  `dart.package_test.main_entrypoint`,
  `rf-dart-package-test-main-omit-in-xunit`.)

- Three sibling outer `group(...)` blocks → three sibling xUnit test
  classes in the same file: `ModedHeadTests`, `ProducedTermTests`,
  `ExplicitDualTypeDefinitionsTests`. The class name is the PascalCased
  outer-group label + `Tests` suffix (project-wide convention from
  `boot_loader.dart.md`). Inside `ModedHeadTests`, the four inner
  groups FLATTEN to methods with `[Trait("Group", "<inner-label>")]`
  attributes and group-prefixed PascalCased method names. The outer
  group label itself is NOT emitted as a `[Trait]` (the class encodes
  it). `ProducedTermTests` and `ExplicitDualTypeDefinitionsTests` are
  single-level (no inner groups) and have no `[Trait("Group", ...)]`
  attributes. (Construct `dart.package_test.group_block`,
  `rf-dart-package-test-group-to-xunit-class`.)

- Local helper closures (`goal`, `writer`, `reader`, `nil`, `cons`,
  `constTerm`, `struct`) → `private static` HELPER METHODs on the
  enclosing xUnit test class, expression-bodied (`=>`). Names
  PascalCased: `Goal`, `Writer`, `Reader`, `Nil`, `Cons`, `ConstTerm`,
  `Struct`. List parameters use `IReadOnlyList<ast.Term>`. The three
  outer groups each receive their OWN helper-method copies (matching
  the Dart re-declarations); only `ExplicitDualTypeDefinitionsTests`
  carries `Struct`. (Construct `dart.test.local_helper_closures`,
  `rf-dart-local-helper-closure-to-csharp-static-method`.)

- Implicit-new constructor calls (every `ast.Goal(...)`,
  `ast.VarTerm(...)`, `ast.ListTerm(...)`, `ast.ConstTerm(...)`,
  `ast.UnderscoreTerm(...)`, `ast.StructTerm(...)`, `ProcDecl(...)`,
  `TypeRef(...)`, `TypeEnvironment(...)`, `TypeDef(...)`,
  `StructAlt(...)`, `ListNilAlt(...)`, `ListConsAlt(...)`,
  `PrimitiveModeAlt(...)`, `DiffListAlt(...)`) → explicit C# `new
  T(...)` form (conservative default, target-version portable);
  target-typed `new(...)` is permitted where the C# compiler can infer
  the target type. (Construct `dart.test.constructor_call_implicit_new`,
  `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`.)

- Named-argument call sites `isInput: true` / `isInput: false` /
  `typeEnv: typeEnv` → translated UNCHANGED (C# uses `name: value` for
  named args; parameter names remain camelCase per
  https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names#parameter-names).
  (Construct `dart.test.named_argument_passing`,
  `rf-dart-named-arguments-to-csharp-named-arguments`.)

- Each `test(label, () { ... })` → `public void` method on the
  enclosing class, decorated with `[Fact(DisplayName = "<original
  label>")]`. Method name is the group-prefixed (where applicable)
  PascalCased label. All bodies are synchronous (no `async Task`).
  (Construct `dart.test.test_call_simple`,
  `rf-dart-test-callback-to-xunit-method-body`.)
  - Test inventory (final): on `ModedHeadTests` — 2 +
    4 + 1 + 1 = 8 methods. On `ProducedTermTests` — 2 methods. On
    `ExplicitDualTypeDefinitionsTests` — 2 methods. Total 12.

- `expect(<actual>, equals(<expected>))` → `Assert.Equal(<expected>,
  <actual>)` (ARGUMENT-ORDER FLIP). Field-access casing PascalCases:
  `compound.functor`/`arity`/`args`, `x.name`/`isReader`/`mode`,
  `arg1.isListCons`/`mode`, `nilArg.isNil`. Enum values PascalCase:
  `Mode.consume` → `Mode.Consume`, `Mode.produce` → `Mode.Produce`.
  (Construct `dart.package_test.expect_equals`,
  `rf-dart-expect-equals-to-xunit-assertequal`.)

- `expect(result, isA<T>())` followed IMMEDIATELY by `final x = result
  as T;` → FOLDED into `var x = Assert.IsType<T>(result);` (the
  TYPED overload returns the typed value, eliminating the cast).
  STANDALONE `as T` downcasts (no preceding `isA<T>`) → `(T)expr`
  C# checked cast (throws `InvalidCastException` on mismatch,
  equivalent to Dart's `TypeError`). (Constructs
  `dart.package_test.expect_isA_type_check` and
  `dart.test.downcast_as_expression`,
  `rf-dart-expect-isa-to-xunit-istype` /
  `rf-dart-as-cast-to-csharp-explicit-cast-or-istype-fold`.)

- `expect(<bool-expr>, isTrue)` → `Assert.True(<bool-expr>);`.
  `expect(<bool-expr>, isFalse)` → `Assert.False(<bool-expr>);`.
  Bare form suffices (the file's predicates are simple property
  accesses). (Construct `dart.package_test.expect_isTrue_isFalse`,
  `rf-dart-expect-istrue-to-xunit-asserttrue`.)

- `expect(v1.name, isNot(equals(v2.name)))` → `Assert.NotEqual(v2.Name,
  v1.Name)` (argument-order flip; composed `isNot(equals(...))`
  collapses to the dedicated `Assert.NotEqual` assertion). Used ONCE.
  (Construct `dart.package_test.expect_isNot_equals`,
  `rf-dart-expect-isnot-equals-to-xunit-assertnotequal`.)

- `expect(v1.name, startsWith('_#'))` → `Assert.StartsWith("_#",
  v1.Name)` (dedicated string-prefix assertion). Used twice.
  (Construct `dart.package_test.expect_startsWith`,
  `rf-dart-expect-startswith-to-xunit-assertstartswith`.)

- `expect(() => modedHead(head, decl), throwsA(isA<ArityMismatchError>()))`
  → `Assert.Throws<ArityMismatchError>(() => ModedHead(head, decl));`
  (simple form, no `having` clause, no follow-on
  `Assert.Contains`/`var ex =` capture). (Construct
  `dart.package_test.expect_throwsA_isA`,
  `rf-dart-throwsa-isa-to-xunit-throws-simple`.)

- `TypeEnvironment({}, {})` → `new TypeEnvironment(new
  Dictionary<string, TypeDef>(), new Dictionary<string, TypeDef>())`
  OR target-typed `new TypeEnvironment(new(), new())` against the
  typed signature. C# 12 collection expressions DO NOT yet target
  `Dictionary<K,V>`. (Construct `dart.collection.empty_map_literal`,
  `rf-dart-empty-map-literal-to-csharp-dictionary`.)

- List literals in constructor positions (`[TypeRef(...), ...]`,
  `[ListNilAlt(...), ListConsAlt(...)]`, `[StructAlt(...)]`,
  `[writer('X'), writer('Y')]`, `[ast.UnderscoreTerm(0, 0),
  ast.UnderscoreTerm(0, 0)]`) → C# `new List<T> { a, b, c }` (pre-C#-12
  portable default) OR C# 12 collection expression `[a, b, c]` against
  a typed slot. Heterogeneous element lists (`TypeDef('Stream', [
  ListNilAlt(...), ListConsAlt(...) ])`) → `new List<TypeAlt> { new
  ListNilAlt(...), new ListConsAlt(...) }` (element type is the
  common base). (Constructs `dart.collection.list_literal_in_constructor`
  and `dart.test.list_in_constructor_typedef_alts`,
  `rf-dart-list-literal-to-csharp-list-or-collection-expression`.)

- Dart raw string `r'\'` → C# standard escaped `"\\"` (single
  backslash, recommended for ≤3-char raw content; verbatim `@"\\"` and
  C# 11 raw `"""\\"""` are corroborating alternatives recorded once).
  (Construct `dart.string.raw_string_literal`,
  `rf-dart-raw-string-to-csharp-verbatim-or-escaped`.)

- `TypeRef('T', 0, 0)` (omitting `isInput:`) → C# call site that
  ALSO omits `isInput:`, IF the converted C# `TypeRef` constructor
  declares the SAME default value. Codegen must preserve the default
  from `type_ast.dart`'s converted signature; if the C# signature
  mandates the argument, codegen MUST add the default explicitly.
  (Construct `dart.test.bool_named_arg_default`,
  `rf-dart-named-arguments-to-csharp-named-arguments`.)

- Dart `final <T> x = expr;` local declarations → C# `var x = expr;`
  (implicit-typed, single-assignment by convention; C# has no local-
  level immutability enforcement comparable to Dart `final`). Explicit
  typed `T x = expr;` is equally correct. (Construct
  `dart.test.final_local_variable`,
  `rf-dart-final-local-to-csharp-var-local`.)

## 3. Decomposed Task Units

- T1: Emit file-scope `using Xunit;` + `using System;` + bare `using
  <RootNs>.Analysis.TypeChecker;` (covers 4 SUT imports) + namespace
  alias `using ast = <RootNs>.Compiler.Ast;`. — done.
- T2: Emit namespace declaration `namespace <RootNs>.Test.Analysis.TypeChecker;`
  mirroring the Dart `test/analysis/type_checker/` path. — done.
- T3: Emit class `ModedHeadTests` with `private static` helpers
  `Goal`, `Writer`, `Reader`, `Nil`, `Cons`, `ConstTerm`. — done.
- T4: On `ModedHeadTests` emit 8 `[Fact(DisplayName = "<label>")]`
  methods, each with `[Trait("Group", "<inner-label>")]` for the four
  inner groups: 2× basic construction, 4× variable replacement, 1×
  anonymous variables, 1× error handling. — done.
- T5: Emit class `ProducedTermTests` with `private static` helpers
  `Goal`, `Writer`, `Reader` and 2 `[Fact]` methods. — done.
- T6: Emit class `ExplicitDualTypeDefinitionsTests` with `private
  static` helpers `Goal`, `Writer`, `Reader`, `Struct` and 2 `[Fact]`
  methods. — done.
- T7: For each constructor call, emit explicit `new T(...)` (or
  target-typed `new(...)` where target type is inferable). — done.
- T8: Translate named arguments `isInput:` / `typeEnv:` UNCHANGED
  (camelCase preserved per C# parameter-naming convention). — done.
- T9: Translate `expect(actual, equals(expected))` to
  `Assert.Equal(expected, actual)` (argument-order flip), PascalCase
  field-access (`Functor`/`Arity`/`Args`/`Name`/`IsReader`/`Mode`/
  `IsListCons`/`IsNil`) and enum members (`Mode.Consume`/`Mode.Produce`).
  — done.
- T10: Fold each `expect(r, isA<T>()); var x = r as T;` pair into
  `var x = Assert.IsType<T>(r);`; emit STANDALONE `(T)expr` checked
  casts for nested destructurings. — done.
- T11: Translate `expect(b, isTrue)` → `Assert.True(b);` and
  `expect(b, isFalse)` → `Assert.False(b);`. — done.
- T12: Translate the single `expect(v1.name, isNot(equals(v2.name)))`
  → `Assert.NotEqual(v2.Name, v1.Name);`. — done.
- T13: Translate two `expect(v, startsWith('_#'))` calls →
  `Assert.StartsWith("_#", v.Name);`. — done.
- T14: Translate two `expect(() => SUT(...), throwsA(isA<ArityMismatchError>()))`
  calls → `Assert.Throws<ArityMismatchError>(() => SUT(...));` (no
  follow-on `Assert.Contains`, no `var ex =` capture). — done.
- T15: Translate `TypeEnvironment({}, {})` → `new TypeEnvironment(new
  Dictionary<string, TypeDef>(), new Dictionary<string, TypeDef>())`
  (or target-typed `new(), new()`). — done.
- T16: Translate every Dart list literal in constructor position to
  `new List<T> { ... }` (or C# 12 collection expression `[...]` against
  a typed slot); heterogeneous lists use the common base type as `T`
  (e.g. `new List<TypeAlt> { ... }`). — done.
- T17: Translate the single Dart raw string `r'\'` to C# escaped
  `"\\"`. — done.
- T18: For `TypeRef('T', 0, 0)` omit-default sites, preserve the
  default by either also omitting in C# (when the converted constructor
  declares the default) or emitting the default explicitly. — done.
- T19: Translate every Dart `final <T> x = expr;` to C# `var x = expr;`.
  — done.

## 4. Research Findings

none required — every construct in §2 is verbatim-derived from the
ratified convspec at
`.codeconv/conversion-specs/test/analysis/type_checker/moded_head_test.dart.md`,
which itself records all 19 official-docs research findings
(`rf-dart-package-test-import-to-xunit-using`,
`rf-dart-internal-package-import-to-csharp-using`,
`rf-dart-package-test-main-omit-in-xunit`,
`rf-dart-package-test-group-to-xunit-class`,
`rf-dart-local-helper-closure-to-csharp-static-method`,
`rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`,
`rf-dart-named-arguments-to-csharp-named-arguments`,
`rf-dart-test-callback-to-xunit-method-body`,
`rf-dart-expect-equals-to-xunit-assertequal`,
`rf-dart-expect-isa-to-xunit-istype`,
`rf-dart-as-cast-to-csharp-explicit-cast-or-istype-fold`,
`rf-dart-expect-istrue-to-xunit-asserttrue`,
`rf-dart-expect-isnot-equals-to-xunit-assertnotequal`,
`rf-dart-expect-startswith-to-xunit-assertstartswith`,
`rf-dart-throwsa-isa-to-xunit-throws-simple`,
`rf-dart-empty-map-literal-to-csharp-dictionary`,
`rf-dart-list-literal-to-csharp-list-or-collection-expression`,
`rf-dart-raw-string-to-csharp-verbatim-or-escaped`,
`rf-dart-final-local-to-csharp-var-local`).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/analysis/type_checker/moded_head_test.dart.md`
(RATIFIED convspec, source_sha256 `e04d3f734b2cdac1abe63bc1acdd4cf4178743725224834979a741c750a82a9b`,
identical to this plan's source_sha256). The convspec records
`escalations: []` and explicitly documents (in its "Why no escalations"
section) that every construct resolves to a clear, single-decision C#
shape grounded in official .NET and `package:test` documentation. The
three "soft" decisions (xUnit vs NUnit/MSTest; FLATTEN vs nested
classes for inner groups; sibling classes vs one-class-with-traits for
outer groups) are documented project-wide policy with corroborating
alternatives recorded once at the idiom level (precedent:
`test/multiagent/boot_loader_test.dart.md` and
`test/multiagent/mad_error_handling_test.dart.md`). Cross-file
dependencies on `type_ast.dart` (the `isInput:` default value,
`TypeRef`/`TypeEnvironment`/`TypeDef`/`StructAlt`/`ListNilAlt`/
`ListConsAlt`/`PrimitiveModeAlt`/`DiffListAlt` constructor signatures)
are recorded as shape-only call-site dependencies, not unresolved
choices.

## 6. Escalations

None.
