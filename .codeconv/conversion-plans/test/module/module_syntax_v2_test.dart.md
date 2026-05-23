---
path: test/module/module_syntax_v2_test.dart
cycle_group_id: 138
scc_siblings: []
generated_at: 2026-05-21T16:14:11Z
source_sha256: fb04dca7a515ac9c443e9a2a0e24262ce22a82dfee26382aae9b5131e153363a
schema_version: 1
---

# Conversion Plan: test/module/module_syntax_v2_test.dart

## 1. Source Analysis

Parser unit-test file (Dart, 251 lines) covering Phase 1 of the v2 module
syntax. Asserts AST shape produced by `Lexer` + `Parser` for six grouped
scenarios: exported procedure parsing (2a), imported procedure parsing
(2b), rejection of legacy `-export([...])` / `-import([...])` syntax
(2c), `-module(name)` parsing (2d), `Module # Goal` remote-goal parsing
(2e), and type-only files (2f).

Imports (5):
- `package:test/test.dart` — test framework surface (`test`, `group`, `expect`, matchers).
- `package:glp_runtime/compiler/lexer.dart` — `Lexer`.
- `package:glp_runtime/compiler/parser.dart` — `Parser`.
- `package:glp_runtime/compiler/ast.dart` — `Module`, `RemoteGoal`.
- `package:glp_runtime/compiler/error.dart` — `CompileError`.
- `package:glp_runtime/analysis/type_checker/type_ast.dart` — `TypeRef`.

Top-level shape:
- Single `void main()` entry containing one local helper closure and six
  `group(...)` blocks holding twelve `test(...)` calls in total.
- Local helper: `Module parseModule(String source) { ... }` — builds a
  `Lexer` from source, calls `tokenize()`, builds a `Parser`, returns
  `parser.parseModule()`.

Constructs exercised (mirrored from convspec):
1. `package:test` import → drop in favour of xUnit.
2. Intra-package `package:glp_runtime/...` imports → C# `using` directives.
3. `void main()` registration root → xUnit `[Fact]`-attributed class.
4. Triple-single-quoted multi-line literals (`'''...'''`).
5. `expect(actual, value)` implicit-equals matcher.
6. `expect(actual, true/false/isNull)` boolean/null matchers.
7. `expect(actual, isA<T>())` type matcher.
8. `expect(() => fn(), throwsA(isA<T>().having((e) => e.message, 'message', contains('...'))))` exception-with-property matcher.
9. `expect(collection, isEmpty)` empty matcher.
10. Null-forgiving `!` operator (`module.declaration!.name`).
11. List indexer + property access (`module.procDeclarations[0].name`).
12. Throwing cast `expr as T` (folded into `Assert.IsType<T>`).
13. `final` locals (`final lexer = Lexer(source);` etc.).

No async surface, no streams, no isolates, no mixins, no extensions, no
generic type parameters declared at the file level. The Dart-source
snippets inside triple-quoted literals are GLP source code — opaque to
the conversion, must remain verbatim string content in the C# raw-string
literals (the test still parses GLP source on the C# side).

## 2. Dart → C#/.NET Conversion Plan

Each construct maps verbatim to the convspec ratification. The `→` below
is U+2192.

1. `import 'package:test/test.dart';` → drop directive; replace surface
   with `using Xunit;` (file-level). NuGet refs (`xunit`,
   `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) live in the
   `.csproj` — out of scope for this per-file artefact.
2. `import 'package:glp_runtime/compiler/{lexer,parser,ast,error}.dart';`
   (four imports) → collapse to single `using GlpRuntime.Compiler;` per
   the langpair's directory→namespace coarsening rule.
   `import 'package:glp_runtime/analysis/type_checker/type_ast.dart';`
   → `using GlpRuntime.Analysis.TypeChecker;`. Net: three `using`
   directives (1 xUnit + 2 GlpRuntime) replacing five Dart imports.
3. `void main() { Module parseModule(...) { ... } group(...); ... }` →
   eliminate `void main()` entirely (xUnit is attribute-driven, no
   registration entry). Emit:
   `namespace GlpRuntime.Test.Module { public class ModuleSyntaxV2Test { ... } }`.
   The local closure `parseModule` lifts to a private instance helper
   method `private Module ParseModule(string source) { var lexer = new Lexer(source); var tokens = lexer.Tokenize(); var parser = new Parser(tokens); return parser.ParseModule(); }`.
   Each `test('<name>', () { <body> })` inside a `group('<label>', ...)`
   block lifts to one `[Fact(DisplayName = "<name>")] [Trait("Group", "<label>")] public void <PascalCasedTestName>() { <body> }`
   method on the outer class. Groups are FLATTENED (not nested classes)
   — `[Trait]` carries the original group label as filtering metadata.
   Twelve `[Fact]` methods emitted in total.
4. Dart `'''<newline>...<newline>'''` → C# 11+ raw-string literal
   `"""<newline>...<newline>"""`, closing `"""` at column 0 to preserve
   the no-indentation content. Eight literals total. Leading-newline
   semantic divergence (C# raw strings discard the newline immediately
   after the opening `"""`) is documented; no observable effect on the
   test (the Dart lexer skips leading whitespace during tokenisation).
   Verbatim-string fallback `@"..."` documented for C# ≤ 10 targets.
5. `expect(actual, value)` (implicit-equals sugar) → `Assert.Equal(value, actual)`
   with ARGUMENT-ORDER SWAP. Every call in the file MUST be swapped.
   Examples:
   - `expect(module.procDeclarations.length, 1)` → `Assert.Equal(1, module.ProcDeclarations.Count);`
   - `expect(decl.name, 'factorial')` → `Assert.Equal("factorial", decl.Name);`
   - `expect(decl.argTypes.length, 2)` → `Assert.Equal(2, decl.ArgTypes.Count);`
   `.length` (Dart `List.length`) → `.Count` on `IList<T>` /
   `IReadOnlyCollection<T>`. camelCase identifiers → PascalCase per
   langpair public-property rule.
6. Boolean / null matchers via the routing table:
   - `expect(decl.exported, true)` → `Assert.True(decl.Exported);`
   - `expect(decl.exported, false)` → `Assert.False(decl.Exported);`
   - `expect(decl.imported, true)` → `Assert.True(decl.Imported);`
   - `expect(decl.modulePath, isNull)` → `Assert.Null(decl.ModulePath);`
7. `expect(decl.argTypes[1], isA<TypeRef>())` → `Assert.IsType<TypeRef>(decl.ArgTypes[1]);`
   (exact-type; returns the cast value). For the single occurrence in
   this file, FOLD with the immediately-following `final typeRef = decl.argTypes[1] as TypeRef;`
   into a single `var typeRef = Assert.IsType<TypeRef>(decl.ArgTypes[1]);`
   — eliminates the redundant standalone `as` cast and sidesteps the
   C# `as`-keyword null-on-failure footgun.
8. `expect(() => parseModule(source), throwsA(isA<CompileError>().having((e) => e.message, 'message', contains('no longer supported'))))`
   → decompose into the two-call shape:
   `var ex = Assert.Throws<CompileError>(() => ParseModule(source)); Assert.Contains("no longer supported", ex.Message);`
   `Assert.Throws<T>` is exact-type (rejects subtypes) — matches the
   Dart leaf-most `CompileError` case. Two occurrences in the file
   (-export and -import rejection tests).
9. `expect(module.procedures, isEmpty)` → `Assert.Empty(module.Procedures);`
   Mirror form `isNotEmpty` → `Assert.NotEmpty(...)` recorded for
   completeness (not used in this file).
10. `module.declaration!.name` → `module.Declaration!.Name;` (identical
    surface and semantics — static-analysis-only suppression on both
    sides; runtime exception on the FOLLOWING member access if null).
11. `module.procDeclarations[0].name` → `module.ProcDeclarations[0].Name;`
    1-to-1 structural translation with uniform camelCase ⇒ PascalCase
    on public properties. Zero-based indexer + `ArgumentOutOfRangeException`-on-overflow
    semantics identical on both sides.
12. `decl.argTypes[1] as TypeRef` → folded into `Assert.IsType<TypeRef>(...)`
    (see construct 7). Standalone form (not exercised here) would
    translate to `(TypeRef)expr` — NEVER mechanically to C# `as` which
    has opposite (null-on-failure) semantics.
13. `final lexer = Lexer(source); final tokens = lexer.tokenize(); ...`
    → `var lexer = new Lexer(source); var tokens = lexer.Tokenize(); ...`
    `final` single-assignment compile-time enforcement is LOST in
    translation (documented langpair limitation); `var` is the
    idiomatic type-inference target. Note the C#-side `new` keyword
    on constructor calls.

Additional structural element produced (per convspec `conversion_units`):

- File-level namespace: `namespace GlpRuntime.Test.Module { ... }` —
  mirrors the test file's source directory (`test/module/`) under the
  langpair's test-namespace convention.
- Target file name: `ModuleSyntaxV2Test.cs` (snake-to-Pascal from
  `module_syntax_v2_test.dart`).

The Dart-source-snippet strings inside the triple-quoted literals
(GLP source code: `exported procedure factorial(...).`,
`-export([factorial/2]).`, `-module(math).`, etc.) are NOT translated
— they remain verbatim string content in the C# raw-string literals.

## 3. Decomposed Task Units

- T1. Emit file header: `using Xunit; using GlpRuntime.Compiler; using GlpRuntime.Analysis.TypeChecker;` plus `namespace GlpRuntime.Test.Module { ... }` wrapper.
- T2. Emit `public class ModuleSyntaxV2Test { ... }` outer class shell.
- T3. Lift Dart local closure `parseModule` to `private Module ParseModule(string source)` on the test class, with body translated per construct 13.
- T4. Lift Phase-1-2a tests (4 tests in group `'Phase 1 - 2a: exported procedure parsing'`) to `[Fact]` methods with `[Trait("Group", "Phase 1 - 2a: exported procedure parsing")]`, including raw-string source snippets and `Assert.Equal` / `Assert.True` / `Assert.False` calls with argument-order swap.
- T5. Lift Phase-1-2b tests (5 tests in group `'Phase 1 - 2b: imported procedure parsing'`) to `[Fact]` methods with `[Trait("Group", ...)]`, including the `Assert.IsType<TypeRef>` cast-fold for the `argTypes[1] as TypeRef` case and `Assert.Null` for `modulePath`.
- T6. Lift Phase-1-2c tests (2 tests in group `'Phase 1 - 2c: rejection of old syntax'`) to `[Fact]` methods using `Assert.Throws<CompileError>` + `Assert.Contains` decomposition.
- T7. Lift Phase-1-2d test (1 test in group `'Phase 1 - 2d: -module(name) still works'`) to `[Fact]` method using `Assert.NotNull` + null-forgiving `!` access on `module.Declaration!.Name`.
- T8. Lift Phase-1-2e test (1 test in group `'Phase 1 - 2e: Module # Goal still works'`) to `[Fact]` method including the `parser.Parse()` call path, `RemoteGoal` cast (folded into `Assert.IsType<RemoteGoal>`), and property assertions on `staticModuleName` / `goal.functor`.
- T9. Lift Phase-1-2f test (1 test in group `'Phase 1 - 2f: type-only file'`) to `[Fact]` method using `Assert.Empty` for `module.Procedures` / `module.ProcDeclarations`.
- T10. Apply uniform camelCase ⇒ PascalCase on every public-property access (`procDeclarations` → `ProcDeclarations`, `argTypes` → `ArgTypes`, `modulePath` → `ModulePath`, `typeDefs` → `TypeDefs`, `declaration` → `Declaration`, `procedures` → `Procedures`, `body` → `Body`, `staticModuleName` → `StaticModuleName`, `functor` → `Functor`, `clauses` → `Clauses`, `name` → `Name`, `exported` → `Exported`, `imported` → `Imported`, `isInput` → `IsInput`).
- T11. Apply uniform `.length` → `.Count` for collection-length checks.
- T12. Verify every triple-single-quoted Dart literal `'''...'''` translated to a C# 11+ raw-string `"""..."""` with the closing `"""` at column 0 to preserve no-indent content.
- T13. Verify every `expect(actual, expected)` call has been emitted as `Assert.Equal(expected, actual)` (argument-order SWAP) — the highest-risk per-call transformation.

## 4. Research Findings

none required — every construct is authoritative-supported on both
sides per the ratified convspec (`.codeconv/conversion-specs/test/module/module_syntax_v2_test.dart.md`).
The convspec cites Microsoft Learn (xUnit tutorial, using directive,
namespaces, capitalization conventions, null-forgiving operator,
cast and type tests, implicitly typed locals, raw string literals,
selective unit tests / Trait filtering, `IList<T>.Item[Int32]`),
pub.dev (`package:test` / `expect` / `throwsA` / `package:matcher`
TypeMatcher), xunit.net (`Assert.Equal` / `Assert.True` / `Assert.False` /
`Assert.Null` / `Assert.NotNull` / `Assert.Empty` / `Assert.IsType<T>` /
`Assert.IsAssignableFrom<T>` / `Assert.Throws<T>` / `Assert.ThrowsAny<T>` /
`Assert.Contains` / `TraitAttribute`, "Shared Context between Tests"),
and dart.dev (language tour: null safety, type operators, variables,
libraries, built-in-types#strings) as authoritative bases. Idiom reuse
from `test/smoke_test.dart`'s convspec applies (KB hit, no re-research):
`rf-dart-package-test-to-dotnet-xunit`,
`rf-dart-test-main-to-xunit-class-with-facts` (extended for groups via
`[Trait]`), and the matcher routing table
`rf-dart-expect-isTrue-to-xunit-assert-true` (extended for
`isFalse`/`isNull`/`isNotNull`/`isEmpty`).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/module/module_syntax_v2_test.dart.md`
(ratified convspec, `source_sha256: fb04dca7a515ac9c443e9a2a0e24262ce22a82dfee26382aae9b5131e153363a`
matching the source). All thirteen constructs above mirror the convspec
`constructs:` entries verbatim; all task units derive from the
convspec's `conversion_units:` list. No deviations.

## 6. Escalations

None.
