---
path: test/module/module_parser_test.dart
cycle_group_id: 137
scc_siblings: []
generated_at: 2026-05-21T16:13:54Z
source_sha256: 474ecc4372558bbbf10fc78a96bb1fb3f4eaf64c5b23d034bf0d40b3096689dc
schema_version: 1
---

# Conversion Plan: test/module/module_parser_test.dart

## 1. Source Analysis

Dart file `glp_runtime_net/test/module/module_parser_test.dart` (304 lines,
sha256 `474ecc43…6689dc`). A `package:test` test file that exercises the GLP
compiler's module-aware lexer and parser. Imports:

- `package:test/test.dart` (external test framework)
- `package:glp_runtime/compiler/lexer.dart` (SUT — `Lexer`)
- `package:glp_runtime/compiler/parser.dart` (SUT — `Parser`)
- `package:glp_runtime/compiler/ast.dart` (SUT — `RemoteGoal`, `VarTerm`, etc.)
- `package:glp_runtime/compiler/token.dart` (SUT — `TokenType` enum)

Top-level shape: a single `void main()` whose body is exactly SIX sibling
top-level `group(...)` calls (no nesting, no `setUp`, no shared `late` field):

1. `'Module Parser - Lexer'` — 3 `test(...)` callbacks (lines 9–45).
2. `'Module Parser - Module Declaration'` — 2 `test(...)` callbacks (lines 49–73).
3. `'Module Parser - Remote Goal'` — 5 `test(...)` callbacks (lines 77–165).
4. `'Module Parser - Complete Module'` — 3 `test(...)` callbacks (lines 169–229).
5. `'Module Parser - Procedure Declarations'` — 3 `test(...)` callbacks (lines 233–276).
6. `'Module Parser - Remote Goal in Module'` — 1 `test(...)` callback (lines 280–303).

Total: 17 `test(...)` callbacks (matches the convspec count of 16 + 1
arithmetic discrepancy — the convspec text in §3 of the spec says "sixteen
`test(...)` calls" and the cu-list enumerates 3+2+5+3+3+1 = 17; the actual
file has 17 — counted directly: lines 9, 22, 37, 49, 65, 77, 101, 121, 139,
156, 169, 200, 216, 233, 248, 263, 280. Verified by direct line-by-line
inspection of the source above. The plan emits 17 `[Fact]` methods; the
convspec's "~16/sixteen" prose is a one-off rounding, not an idiom
discrepancy — the construct rules apply identically per test).

All callbacks are SYNCHRONOUS (no `async`/`Future`). Every test body uses
the same pattern: arrange (construct `Lexer` → `tokenize` → `Parser` → call
`parseModule()` or `parse()`), then a sequence of `expect(...)` assertions.
Locals are `final`. String fixtures are triple-quoted `'''...'''`. Matchers
used: implicit-equals (literal second arg), `isNotNull`, `isNull`,
`isEmpty`, `isA<T>()`, and `throwsA(anything)` (once, line 164). Three Dart
`as`-casts (lines 94, 118, 136). Two `Set<String>` literal comparisons
(lines 194, 296). Seven `TokenType.*` enum-member accesses. Pervasive
`tokens[i]`, `procedures[i]`, `clauses[i]`, `body![i]` indexer access.
`body!`-style null-assertions (8 occurrences across lines 87, 91, 151–153,
299–301).

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec `.codeconv/conversion-specs/test/module/module_parser_test.dart.md`.
Each construct row below maps the Dart construct to its C#/.NET target.
The `→` arrow is U+2192.

### 2.1 `dart.package_test.import_directive` → xUnit + System + Generic usings

`import 'package:test/test.dart';` → file-scope `using Xunit;` plus
`using System;` (for `Assert.Throws<Exception>` in §2.11) and
`using System.Collections.Generic;` (for `HashSet<string>` in §2.12).
Project-wide xUnit policy. Cached idiom
`rf-dart-package-test-import-to-xunit-using` (no re-research).

### 2.2 `dart.package_under_test.import_directive` → single `using <RootNs>.Compiler;`

The four `package:glp_runtime/compiler/{lexer,parser,ast,token}.dart`
imports collapse to ONE `using` line when the four target SUT files share
the same C# namespace (the conventional outcome — `<RootNs>.Compiler`).
Final namespace string deferred to the SUT convspecs
(`lib/compiler/lexer.dart.md`, `parser.dart.md`, `ast.dart.md`,
`token.dart.md`). Cached idiom
`rf-dart-internal-package-import-to-csharp-using`.

### 2.3 `dart.package_test.main_entrypoint` → omit

`void main() { group(...); group(...); ... }` is eliminated entirely;
xUnit discovers `[Fact]` methods by reflection. THIS file's `main` body is
exactly six sibling `group()` calls with no file-level `setUp` and no
shared state → omission is lossless. No `IClassFixture<>` migration.
Cached idiom `rf-dart-package-test-main-omit-in-xunit`.

### 2.4 `dart.package_test.group_block` → six independent xUnit classes

The six sibling top-level `group(...)` calls become six PascalCase xUnit
classes within the same `.cs` file (one class per group; no shared base,
no `IClassFixture<>` — no shared state to migrate):

- `'Module Parser - Lexer'`               → `ModuleParserLexerTests`
- `'Module Parser - Module Declaration'`  → `ModuleParserModuleDeclarationTests`
- `'Module Parser - Remote Goal'`         → `ModuleParserRemoteGoalTests`
- `'Module Parser - Complete Module'`     → `ModuleParserCompleteModuleTests`
- `'Module Parser - Procedure Declarations'` → `ModuleParserProcedureDeclarationsTests`
- `'Module Parser - Remote Goal in Module'`  → `ModuleParserRemoteGoalInModuleTests`

Original group label preserved on every method via
`[Fact(DisplayName = "<original label>")]`. Cached idiom
`rf-dart-package-test-group-to-xunit-class`. SIBLING topology (not nested)
explicitly addressed in the convspec — distinguishes from the
boot_loader_test FLATTEN precedent.

### 2.5 `dart.package_test.test_call_simple` → `[Fact(DisplayName=...)] public void <Name>()`

Each Dart `test(label, () { ... })` synchronous callback becomes a
`public void` instance method on the enclosing xUnit class, decorated
with `[Fact(DisplayName = "<original Dart label>")]`. Method name = label
PascalCased with non-identifier chars stripped. All 17 callbacks are
synchronous → no `async Task`. Closure body translates statement-for-
statement. Cached idiom `rf-dart-test-callback-to-xunit-method-body`.

Method names (verbatim per group):

- `ModuleParserLexerTests`:
  - `LexerRecognizesHashToken`
  - `LexerHandlesModuleDeclarationTokens`
  - `LexerHandlesExportedKeywordTokens`
- `ModuleParserModuleDeclarationTests`:
  - `ParserParsesModuleDeclaration`
  - `ParserParsesHierarchicalModuleName`
- `ModuleParserRemoteGoalTests`:
  - `ParserParsesStaticRemoteGoal`
  - `ParserParsesDynamicRemoteGoal`
  - `ParserParsesReaderVariableRemoteGoal`
  - `ParserParsesChainedRemoteGoals`
  - `ParserRejectsModuleWithArguments`
- `ModuleParserCompleteModuleTests`:
  - `ParserParsesCompleteModuleFileWithExportedProcedure`
  - `ParserHandlesModuleWithoutDeclarations`
  - `LegacyParseSkipsModuleDeclaration`
- `ModuleParserProcedureDeclarationsTests`:
  - `ParserParsesNullaryProcedureWithoutParentheses`
  - `ParserParsesNullaryProcedureWithEmptyParentheses`
  - `ParserParsesProcedureWithArguments`
- `ModuleParserRemoteGoalInModuleTests`:
  - `ParserParsesModuleWithRemoteGoals`

### 2.6 `dart.local.final_var_declaration` → `var`

Every `final <name> = <expr>;` local → `var <name> = <expr>;`. Dart 2.x
implicit-`new` constructor calls (`Lexer(source)`, `Parser(tokens)`) gain
the C#-required `new` keyword (`new Lexer(source)`, `new Parser(tokens)`).
Cached idiom `rf-dart-final-local-to-csharp-var-local`. No local in this
file is ever reassigned → `var` is observably equivalent to Dart `final`.

### 2.7 `dart.package_test.expect_equals_implicit` → `Assert.Equal(expected, actual)`

Every `expect(<actual>, <literal-value>)` (implicit-equals form) →
`Assert.Equal(<expected>, <actual>)` with ARGUMENT-ORDER FLIP. Applies to
all `.length`/`.arity` (int), `.lexeme`/`.name`/`.functor` (string),
`isDynamic`/`isReader`/`false` (bool), and `TokenType.*` (enum)
comparisons. Cached idiom `rf-dart-expect-equals-to-xunit-assertequal`.

### 2.8 `dart.package_test.expect_isNotNull_matcher` → `Assert.NotNull(x)`

`expect(x, isNotNull)` → `Assert.NotNull(x);`. Two uses in this file
(lines 59, 87). After the assertion, xUnit's `[NotNull]` parameter
attribute narrows null-flow analysis on `x` for the rest of the method
body → subsequent `x!.member` accesses become plain `x.Member`. Cached
idiom `rf-dart-expect-isNotNull-to-xunit-assert-notnull`.

### 2.9 `dart.package_test.expect_isNull_matcher` → `Assert.Null(x)`

`expect(x, isNull)` → `Assert.Null(x);`. Three uses (lines 115, 210, 211).
Cached idiom `rf-dart-expect-isNull-to-xunit-assert-null`.

### 2.10 `dart.package_test.expect_isEmpty_matcher` → `Assert.Empty(x)`

`expect(x, isEmpty)` → `Assert.Empty(x);`. One use (line 212 —
`module.procDeclarations`, a `List<ProcedureDeclaration>`). Cached idiom
`rf-dart-expect-isEmpty-to-xunit-assert-empty`.

### 2.11 `dart.package_test.expect_isA_matcher` → `Assert.IsType<T>(x)`

`expect(x, isA<T>())` → `Assert.IsType<T>(x);`. Six uses (lines 92, 112,
132, 152, 153, 300, 301 — convspec lists six because lines 152+153 are
two adjacent uses; counting yields seven `isA<...>()` references but the
convspec rolls 152/153/300/301 together — emit a separate
`Assert.IsType<T>` for each Dart occurrence; codegen emits per-line, not
per-class). All targets are `RemoteGoal` or `VarTerm` (leaf concrete
classes per ast.dart) → `Assert.IsType<T>` exact-match coincides with
`Assert.IsAssignableFrom<T>` subtype-match. Cached idiom
`rf-dart-expect-isA-to-xunit-assert-istype`.

### 2.12 `dart.package_test.expect_throws_anything` → `Assert.Throws<Exception>(() => …)`

`expect(() => parser.parse(), throwsA(anything))` →
`Assert.Throws<Exception>(() => parser.Parse());`. One use (line 164,
`parser rejects module with arguments`). `System.Exception` as the root
.NET user-throwable type matches Dart `anything`'s "any thrown thing"
semantics. FIRST-SEEN idiom
`rf-dart-throwsa-anything-to-xunit-assert-throws-exception`.

### 2.13 `dart.collections.set_literal_string_equality` → `Assert.Equal(new HashSet<string> {...}, x)`

`expect(module.exportedSignatures, {'factorial/2', 'gcd/3'})` →
`Assert.Equal(new HashSet<string> { "factorial/2", "gcd/3" }, module.ExportedSignatures);`.
Two uses (lines 194, 296). xUnit `Assert.Equal(IEnumerable, IEnumerable)`
on `HashSet<string>` uses set-equality (membership-based, NOT order-
sensitive) via `HashSet.SetEquals`. Alternative
`Assert.True(expected.SetEquals(actual))` is equally valid and recorded
as a corroborating shape. Cached idiom
`rf-dart-set-literal-typed-to-csharp-hashset-initializer`.

### 2.14 `dart.nullable_bang.property_access` → flow-narrowed `.Member` or preserved `!.Member`

`module.declaration!.name`, `clause.body!.length`, `clause.body![0]`,
`bootClause.body![1]`, etc. — eight occurrences total. If the C# property
is non-nullable (per the converted ast.dart's `Module.Declaration` /
`Clause.Body` typing), the `!` simply drops. If nullable AND preceded by
`Assert.NotNull(x)` on the same path, xUnit's `[NotNull]` narrowing
allows the `!` to drop. Otherwise, codegen preserves the C# null-
forgiving `!` (same syntax both sides). Final shape decided when
`lib/compiler/ast.dart.md` converts. FIRST-SEEN idiom
`rf-dart-nullable-bang-after-assertnotnull-flow-narrowed`.

### 2.15 `dart.as_cast.downcast_to_subtype` → `(T)x`

`final remote = goal as RemoteGoal;` →
`var remote = (RemoteGoal)goal;`.
`(remote.module as VarTerm).name` → `((VarTerm)remote.Module).Name`.
`(remote.module as VarTerm).isReader` → `((VarTerm)remote.Module).IsReader`.
Three uses (lines 94, 118, 136). Each is paired with a PRECEDING
`Assert.IsType<T>` on the same value → exception-type difference
(`TypeError` vs `InvalidCastException`) is unreachable. Codegen prefers
the explicit-cast form `((T)x).Member` over `(x as T)!.Member`. Cached
idiom `rf-dart-as-cast-to-csharp-explicit-cast`.

### 2.16 `dart.string.triple_quoted_raw_literal` → C# 11 raw string `""" ... """`

Every `final source = '''...'''` triple-quoted multi-line fixture →
C# 11 raw string literal `var source = """ ... """;`. 13 occurrences
across the 17 test bodies (every parser-test arrange step except the
three pure-lexer tests in §1 which use single-line literals). Closing
`"""` at column 0 (or aligned with the desired indent strip); LF line
endings preserved (codegen MUST normalise CRLF → LF inside the raw
string payload to maintain byte-identity with the Dart fixture). Cached
idiom `rf-dart-triple-quoted-string-to-csharp-raw-string`.

### 2.17 `dart.string.single_quoted_literal` → C# double-quoted `"..."`

Every Dart `'<text>'` single-line literal (labels, identifiers, lexeme
fragments — `'a'`, `'#'`, `'math'`, `'module'`, `'exported'`, etc.) →
C# `"<text>"`. No literal in this file uses Dart-specific escapes
(`\$`, `\u{...}`) or contains a `"`. FIRST-SEEN idiom
`rf-dart-single-quoted-string-to-csharp-double-quoted-string`.

### 2.18 `dart.enum.value_access` → identical C# enum value-access

`TokenType.HASH`, `TokenType.ATOM`, `TokenType.MINUS`, `TokenType.LPAREN`,
`TokenType.PROCEDURE`, `TokenType.RPAREN`, `TokenType.DOT` — all seven
translate to identical C# syntax (`TokenType.HASH`, etc.). Final case
convention (SCREAMING_SNAKE vs PascalCase) inherited from
`lib/compiler/token.dart.md`. FIRST-SEEN idiom
`rf-dart-enum-value-access-to-csharp-enum-value-access`.

### 2.19 `dart.list.indexer_access` → identical C# `[i]`

Every `xs[i]` (`tokens[0..5]`, `module.procedures[0..1]`,
`program.procedures[0]`, `clauses[0]`, `body![0..1]`, `bootClause.body![0..1]`)
→ C# `xs[i]` on `List<T>`/`IList<T>`. Identical syntax both sides.
FIRST-SEEN idiom `rf-dart-list-indexer-to-csharp-list-indexer`.

## 3. Decomposed Task Units

- T1: emit file-scope `using Xunit; using System; using System.Collections.Generic; using <RootNs>.Compiler;` per §2.1 + §2.2 — done.
- T2: emit `namespace <RootNs>.Test.Module` declaration mirroring the Dart subtree path — done.
- T3: omit Dart `void main()` per §2.3 — done.
- T4: emit six classes `ModuleParserLexerTests`, `ModuleParserModuleDeclarationTests`, `ModuleParserRemoteGoalTests`, `ModuleParserCompleteModuleTests`, `ModuleParserProcedureDeclarationsTests`, `ModuleParserRemoteGoalInModuleTests` per §2.4 — done.
- T5: emit 3 `[Fact(DisplayName=...)]` methods on `ModuleParserLexerTests` per §2.5 — done.
- T6: emit 2 `[Fact(DisplayName=...)]` methods on `ModuleParserModuleDeclarationTests` per §2.5 — done.
- T7: emit 5 `[Fact(DisplayName=...)]` methods on `ModuleParserRemoteGoalTests` per §2.5 — done.
- T8: emit 3 `[Fact(DisplayName=...)]` methods on `ModuleParserCompleteModuleTests` per §2.5 — done.
- T9: emit 3 `[Fact(DisplayName=...)]` methods on `ModuleParserProcedureDeclarationsTests` per §2.5 — done.
- T10: emit 1 `[Fact(DisplayName=...)]` method on `ModuleParserRemoteGoalInModuleTests` per §2.5 — done.
- T11: rewrite every `final <x> = <expr>;` local as `var <x> = <expr>;` and add `new` keyword to every constructor call per §2.6 — done.
- T12: route every implicit-equals `expect(actual, literal)` → `Assert.Equal(literal, actual)` per §2.7 (argument-order flip) — done.
- T13: route `isNotNull` × 2 → `Assert.NotNull` per §2.8 — done.
- T14: route `isNull` × 3 → `Assert.Null` per §2.9 — done.
- T15: route `isEmpty` × 1 → `Assert.Empty` per §2.10 — done.
- T16: route `isA<T>()` × 6 → `Assert.IsType<T>` per §2.11 — done.
- T17: route `throwsA(anything)` × 1 → `Assert.Throws<Exception>(() => …)` per §2.12 — done.
- T18: route `Set<String>` literal compare × 2 → `Assert.Equal(new HashSet<string> { ... }, actual)` per §2.13 — done.
- T19: handle `<expr>!.<member>` × 8 per §2.14 (drop `!` when flow-narrowed by `Assert.NotNull` or when target property is non-nullable; otherwise preserve `!`) — done.
- T20: rewrite `<expr> as T` × 3 → `(T)<expr>` (block-stored as `var remote = (RemoteGoal)goal;` for the line-94 case; inline as `((VarTerm)remote.Module).Name`/`.IsReader` for lines 118/136) per §2.15 — done.
- T21: rewrite every `'''...'''` fixture × 13 → `"""..."""` raw string with column-0 closing delimiter and LF line endings per §2.16 — done.
- T22: rewrite every `'...'` single-line literal → `"..."` per §2.17 — done.
- T23: emit every `TokenType.<MEMBER>` access verbatim per §2.18 (final case inherited from token.dart.md) — done.
- T24: emit every `xs[i]` indexer access verbatim per §2.19 — done.

## 4. Research Findings

none required. All 19 construct rows in the ratified convspec
(`.codeconv/conversion-specs/test/module/module_parser_test.dart.md`)
carry a `research_finding_id` and either a cached `idiom_id` (14 rows
reused verbatim) or a FIRST-SEEN idiom row (5 rows) with authoritative
Dart + .NET documentation bases recorded in the spec's "Rationale +
research provenance" section. No deferred research, no
`Open question` markers. Cached-idiom reuse profile (per convspec
§"Cached-idiom reuse profile"):

- `rf-dart-package-test-import-to-xunit-using` (§2.1)
- `rf-dart-internal-package-import-to-csharp-using` (§2.2)
- `rf-dart-package-test-main-omit-in-xunit` (§2.3)
- `rf-dart-package-test-group-to-xunit-class` (§2.4)
- `rf-dart-test-callback-to-xunit-method-body` (§2.5)
- `rf-dart-final-local-to-csharp-var-local` (§2.6)
- `rf-dart-expect-equals-to-xunit-assertequal` (§2.7)
- `rf-dart-expect-isNotNull-to-xunit-assert-notnull` (§2.8)
- `rf-dart-expect-isNull-to-xunit-assert-null` (§2.9)
- `rf-dart-expect-isEmpty-to-xunit-assert-empty` (§2.10)
- `rf-dart-expect-isA-to-xunit-assert-istype` (§2.11)
- `rf-dart-set-literal-typed-to-csharp-hashset-initializer` (§2.13)
- `rf-dart-as-cast-to-csharp-explicit-cast` (§2.15)
- `rf-dart-triple-quoted-string-to-csharp-raw-string` (§2.16)

FIRST-SEEN idiom rows (already researched and ratified in the convspec):

- `rf-dart-throwsa-anything-to-xunit-assert-throws-exception` (§2.12)
- `rf-dart-nullable-bang-after-assertnotnull-flow-narrowed` (§2.14)
- `rf-dart-single-quoted-string-to-csharp-double-quoted-string` (§2.17)
- `rf-dart-enum-value-access-to-csharp-enum-value-access` (§2.18)
- `rf-dart-list-indexer-to-csharp-list-indexer` (§2.19)

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/module/module_parser_test.dart.md`.

Every plan section §2.1–§2.19 mirrors a construct row in the ratified
convspec's `constructs:` list verbatim — same `target_decision`, same
`idiom_id` / `research_finding_id`, same nuance handling. The convspec's
`escalations: []` (deliberate, not a placeholder per §"Why no
escalations") composes with this plan's §6 → `None.`. The `cu-1..cu-9`
list in the convspec maps 1-to-1 to the T1..T24 task decomposition in
§3 above (T1+T2 = cu-1; T2 = cu-2; T4+T5..T10 = cu-3..cu-8; T21 = cu-9;
remaining T11..T24 are per-construct refinements that the convspec's
cu-list collapses into the class-level units).

Cross-spec consistency: T2 (namespace) and T1 (SUT-namespace `using`)
defer the final namespace string to four SUT convspecs
(`lib/compiler/lexer.dart.md`, `parser.dart.md`, `ast.dart.md`,
`token.dart.md`) — this is a documented deferred-decision pattern in the
convspec (§2.2 "The exact SUT namespace string is decided when those
four files are converted"), NOT an inconsistency or open question.
Similarly T19 (nullable-bang handling) and T23 (enum case convention)
inherit final shape from `lib/compiler/ast.dart.md` and
`lib/compiler/token.dart.md` respectively — documented in the convspec.

No idiom-vs-research conflict, no idiom-vs-idiom conflict, no
undecidable construct. The two "soft" decisions (one-class-per-group
vs FLATTEN; `Assert.Throws<T>` vs `Assert.ThrowsAny<T>`) are resolved by
documented project-wide policy in the convspec's research findings.

## 6. Escalations

None.
