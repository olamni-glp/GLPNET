---
path: test/module/module_typecheck_test.dart
cycle_group_id: 139
scc_siblings: []
generated_at: 2026-05-21T16:34:50Z
source_sha256: 3235d5f0f1363c1e269992793135758ad0fedb64b58307f06f71c3badfab9e68
schema_version: 1
---

# Conversion Plan: test/module/module_typecheck_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/module/module_typecheck_test.dart`
(104 lines, sha256 `3235d5f0…fab9e68`) confirms the following constructs are
present:

- **Line 1**: `import 'package:test/test.dart';` — Dart test-framework
  import.
- **Line 2**: `import 'package:glp_runtime/analysis/type_checker/type_checker.dart';`
  — intra-package import of the SUT module.
- **Lines 4–7**: file-level doc-commented top-level arrow-bodied helper
  `List<TypeError> bodyErrors(TypeCheckResult result) =>
  result.errors.where((e) => e.message.contains('Body atom')).toList();`
  — two `///`-doc-comment lines preserved verbatim, single LINQ-style
  chain inside the body.
- **Line 9**: `void main() { ... }` — file entrypoint, body is exactly
  seven sibling top-level `group(...)` calls (no nesting, no
  `setUp`/`tearDown`, no shared state, no `late` fields).
- **Lines 10–21** (group 2a), **23–36** (2b), **38–50** (2c),
  **52–63** (2d), **65–76** (2e), **78–90** (2f), **92–102** (2g):
  seven sibling `group('Phase 3 - 2<letter>: <description>', () { test(...); })`
  blocks, each containing exactly one synchronous `test(label, () { ... })`
  call.
- Per `test(...)` body: a single `final result = checkSource('''<glp source>''');`
  arrange (triple-single-quoted multi-line string literal of GLP source);
  in tests 2b and 2c an additional `final errors = bodyErrors(result);`
  local; one or two `expect(...)` assertions using matchers `isEmpty`,
  `isNotEmpty`, `isTrue` — each carrying a `reason:` named argument
  string.
- Specific matcher counts: `isEmpty` in tests 2a, 2d, 2e, 2f, 2g (five
  uses); `isNotEmpty` in tests 2b, 2c (two uses); `isTrue` with
  `errors.any((e) => e.message.contains('math#check'))` in test 2b
  (one use).
- Two arrow lambdas in total: `(e) => e.message.contains('Body atom')`
  inside `bodyErrors` (line 7) and `(e) => e.message.contains('math#check')`
  inside the test 2b `Assert.True`-equivalent (line 33).
- All string literals are single-quoted; all GLP-source fixtures are
  triple-single-quoted with leading newline + eight-space indentation.
- No `async`/`Future`, no `setUp`/`tearDown`, no `late` field captures,
  no `expect(... throwsA ...)`, no Dart-specific escape sequences
  (`\$`, `\u{...}`) inside any literal.

The source file's mtime + sha256 match the tombstone
(`mtime: 2026-04-27T09:23:50.000Z`, sha `3235d5f0…fab9e68`,
`topo_level: 6`, `cycle_group_id: 99`). The convspec is RATIFIED with
`escalations: []`. The convspec's 13 constructs cover every Dart-side
syntactic element above; no extra construct was discovered during this
re-inspection.

## 2. Dart → C#/.NET Conversion Plan

The plan mirrors the RATIFIED convspec construct-by-construct. The
arrow `→` is U+2192.

1. **`import 'package:test/test.dart';` → file-scope `using Xunit;`**
   plus the codegen-side companions `using System.Collections.Generic;`
   (for the `List<TypeError>` return-type of the lifted helper) and
   `using System.Linq;` (for `.Where(...).ToList()` / `.Any(...)`).
   Cached idiom `rf-dart-package-test-import-to-xunit-using` (xUnit is
   batch-wide project policy; same idiom as
   module_parser_test.dart.md and module_syntax_v2_test.dart.md). No
   `using System;` required (no `throwsA(anything)` / no
   `Assert.Throws<Exception>` here).

2. **`import 'package:glp_runtime/analysis/type_checker/type_checker.dart';`
   → `using <RootNs>.Analysis.TypeChecker;`** per cached idiom
   `rf-dart-internal-package-import-to-csharp-using`. The langpair-level
   directory-to-namespace mapping PascalCases each path segment and
   drops the file name. This single `using` exposes `checkSource`,
   `TypeCheckResult`, and `TypeError` (per
   `.codeconv/conversion-specs/lib/analysis/type_checker/type_checker.dart.md`,
   all three live in the same namespace). `checkSource` (Dart top-level
   function) becomes a `public static TypeCheckResult CheckSource(string
   source)` method on a `<RootNs>.Analysis.TypeChecker.TypeChecker`
   static facade per the SUT convspec; called as
   `TypeChecker.CheckSource(...)`.

3. **`namespace <RootNs>.Test.Module { ... }`** wrapping the entire
   file. Directory `test/module` → namespace suffix `.Test.Module`
   per the langpair convention.

4. **File-level helper `List<TypeError> bodyErrors(TypeCheckResult
   result) => …` → `internal static class
   ModuleTypecheckTestHelpers` hosting `internal static List<TypeError>
   BodyErrors(TypeCheckResult result) =>
   result.Errors.Where(e => e.Message.Contains("Body atom")).ToList();`**
   Expression-bodied member preserves the Dart `=>` shape. The two
   `///` lines wrap in `<summary>...</summary>` XML element to silence
   CS1591-family warnings while preserving the verbatim Dart prose.
   First-seen idiom `rf-dart-toplevel-arrow-fn-to-csharp-private-static-expression-bodied`.

5. **`void main() { ... }` → omitted entirely.** xUnit discovers
   `[Fact]` methods via reflection; there is no per-file entrypoint.
   Cached idiom `rf-dart-package-test-main-omit-in-xunit`. The
   omission is lossless because `main` here is just seven sibling
   `group(...)` calls with no file-level setup.

6. **Seven sibling `group(label, () { test(...); })` → seven
   independent xUnit test classes** in the same `.cs` file, each
   following the precedent of module_parser_test.dart.md (sibling
   topology → one class per group, NOT flatten-with-[Trait]). Class
   names with non-identifier characters mangled to camel-join:
   - `group('Phase 3 - 2a: remote goal type-checks against imported declaration', …)`
     → `public class Phase3_2aRemoteGoalAgainstImportedDeclarationTests`
   - `group('Phase 3 - 2b: remote goal fails without imported declaration', …)`
     → `public class Phase3_2bRemoteGoalWithoutImportedDeclarationTests`
   - `group('Phase 3 - 2c: remote goal fails on arity mismatch', …)`
     → `public class Phase3_2cRemoteGoalArityMismatchTests`
   - `group('Phase 3 - 2d: deep module path', …)`
     → `public class Phase3_2dDeepModulePathTests`
   - `group('Phase 3 - 2e: imported ancestor procedure (no path)', …)`
     → `public class Phase3_2eImportedAncestorProcedureTests`
   - `group('Phase 3 - 2f: multiple imported procedures', …)`
     → `public class Phase3_2fMultipleImportedProceduresTests`
   - `group('Phase 3 - 2g: dynamic remote goal skipped', …)`
     → `public class Phase3_2gDynamicRemoteGoalSkippedTests`
   Cached idiom `rf-dart-package-test-group-to-xunit-class`.

7. **Each `test(label, () { … })` → `public void <PascalCaseLabel>()`
   instance method** decorated with `[Fact(DisplayName = "<original
   label>")]` to preserve the Dart sentence form (with `#` characters)
   in reporter output. All seven callbacks are synchronous, so NO
   method is `async Task`. Method-name mangling drops `#`, spaces,
   colons, parentheses, dashes; e.g. `'math # check(N?) passes with
   matching imported declaration'` →
   `MathCheckNPassesWithMatchingImportedDeclaration`. Cached idiom
   `rf-dart-test-callback-to-xunit-method-body`.

8. **Each `final result = checkSource('''…''');` → `var result =
   TypeChecker.CheckSource("""…""");`** and each `final errors =
   bodyErrors(result);` → `var errors =
   ModuleTypecheckTestHelpers.BodyErrors(result);`. Cached idiom
   `rf-dart-final-local-to-csharp-var-local`. (`var` is type-inferred
   but mutable; observably equivalent to Dart `final` here because no
   local is ever reassigned.)

9. **Each triple-single-quoted `'''…'''` GLP-source fixture → C# 11
   raw-string `"""…"""`** with closing-`"""` column aligned to
   eight-space indentation so the common-leading-whitespace strip
   yields the same content the Dart `'''…'''` carries. Codegen MUST
   normalise interior line endings to LF. The leading newline after
   `'''` is preserved-by-Dart / stripped-by-C#11 — semantically
   irrelevant because GLP's lexer discards leading whitespace. Cached
   idiom `rf-dart-triple-quoted-string-to-csharp-raw-string`. Fallback
   to verbatim `@"…"` is acceptable for pre-C#11 targets (no fixture
   contains a `"` so no escape doubling is needed).

10. **Every single-quoted `'…'` literal → C# double-quoted `"…"`**
    (every label, every matcher substring, every `reason:` value).
    `#` characters pass through unchanged. Cached idiom
    `rf-dart-single-quoted-string-to-csharp-double-quoted-string`.

11. **Single-parameter arrow lambdas `(e) => e.message.contains(...)`
    → C# `e => e.Message.Contains(...)`** (parens dropped per C#
    canonical style; `message` → `Message` per langpair public-member
    PascalCase). Two occurrences (line 7 inside `.where(...)`; line
    33 inside `.any(...)`). First-seen idiom
    `rf-dart-arrow-lambda-to-csharp-lambda`.

12. **`String.contains(String)` → `string.Contains(string)`** — both
    default to ordinal Unicode-code-unit comparison; neither receiver
    is nullable in this file. Two call sites. First-seen idiom
    `rf-dart-string-contains-to-csharp-string-contains`.

13. **`expect(<list>, isEmpty, reason: '<msg>')` →
    `// <msg>` source-comment + `Assert.Empty(<list>);`** Five uses
    (tests 2a, 2d, 2e, 2f, 2g). xUnit's `Assert.Empty(IEnumerable)`
    has NO custom-message overload, so the `reason:` text is
    preserved as a source-comment immediately above the assertion
    line — preserves documentation intent without altering semantics.
    Cached idiom `rf-dart-expect-isEmpty-to-xunit-assert-empty`.

14. **`expect(<list>, isNotEmpty, reason: '<msg>')` →
    `// <msg>` source-comment + `Assert.NotEmpty(<list>);`** Two
    uses (tests 2b, 2c). Same reason-as-comment treatment as the
    `isEmpty` mirror. First-seen idiom
    `rf-dart-expect-isNotEmpty-to-xunit-assert-notempty`.

15. **`expect(errors.any((e) => e.message.contains('math#check')),
    isTrue, reason: '<msg>')` → `Assert.True(errors.Any(e =>
    e.Message.Contains("math#check")), "<msg>");`** One use (test
    2b, line 33). `Assert.True(bool, string)` DOES accept a custom
    message — so the Dart `reason:` text passes through verbatim as
    the second argument (faithful translation, unlike the
    `Assert.Empty` / `Assert.NotEmpty` cases). Cached idiom
    `rf-dart-expect-isTrue-to-xunit-assert-true`.

## 3. Decomposed Task Units

- **T1** done — Emit file-scope `using` directives: `using Xunit;`,
  `using System.Collections.Generic;`, `using System.Linq;`,
  `using <RootNs>.Analysis.TypeChecker;`.
- **T2** done — Emit `namespace <RootNs>.Test.Module { ... }` wrapper.
- **T3** done — Emit `internal static class ModuleTypecheckTestHelpers`
  hosting expression-bodied `internal static List<TypeError>
  BodyErrors(TypeCheckResult result) => …` with preserved
  `<summary>` XML doc-comment derived from the two Dart `///` lines.
- **T4** done — Emit `public class
  Phase3_2aRemoteGoalAgainstImportedDeclarationTests` with one
  `[Fact(DisplayName = "math # check(N?) passes with matching
  imported declaration")]` method; body assigns `var result =
  TypeChecker.CheckSource("""…""")` and asserts
  `Assert.Empty(ModuleTypecheckTestHelpers.BodyErrors(result))`
  preceded by `// Remote call should type-check against imported
  declaration` source-comment.
- **T5** done — Emit `public class
  Phase3_2bRemoteGoalWithoutImportedDeclarationTests` with one
  `[Fact(DisplayName = "math # check(N?) fails without imported
  declaration")]` method; body assigns `var result =
  TypeChecker.CheckSource("""…""")`, `var errors =
  ModuleTypecheckTestHelpers.BodyErrors(result)`, asserts
  `Assert.NotEmpty(errors)` preceded by `// Remote call without
  imported declaration should produce type error` comment, then
  `Assert.True(errors.Any(e => e.Message.Contains("math#check")),
  "Error should mention the missing imported declaration");`.
- **T6** done — Emit `public class
  Phase3_2cRemoteGoalArityMismatchTests` with one
  `[Fact(DisplayName = "arity mismatch between call and imported
  declaration")]` method; body assigns `var result = …`,
  `var errors = …`, asserts `Assert.NotEmpty(errors)` preceded by
  `// Calling with 2 args when declaration has 1 should be a type
  error` comment.
- **T7** done — Emit `public class Phase3_2dDeepModulePathTests`
  with one `[Fact(DisplayName = "ui#actors # render type-checks
  against deep imported declaration")]` method; body assigns
  `var result = …`, asserts `Assert.Empty(BodyErrors(result))`
  preceded by `// Deep module path should type-check correctly`
  comment.
- **T8** done — Emit `public class
  Phase3_2eImportedAncestorProcedureTests` with one
  `[Fact(DisplayName = "imported procedure without path
  type-checks local calls")]` method; assertions and reason-comment
  per construct rows.
- **T9** done — Emit `public class
  Phase3_2fMultipleImportedProceduresTests` with one
  `[Fact(DisplayName = "multiple imported declarations each checked
  independently")]` method; assertions and reason-comment per
  construct rows.
- **T10** done — Emit `public class
  Phase3_2gDynamicRemoteGoalSkippedTests` with one
  `[Fact(DisplayName = "M # goal(X) where M is a variable is not
  type-checked")]` method; assertions and reason-comment per
  construct rows.
- **T11** done — For every embedded `.glp`-source fixture, emit a
  C# 11 raw-string literal `"""…"""` with closing-`"""` aligned to
  eight-space indentation and interior line endings normalised to
  LF so the payload is byte-identical to the Dart `'''…'''` source.
- **T12** done — For every Dart `reason:`-argument value: pass
  through verbatim as `Assert.True`'s second argument where the
  assertion overload supports it (one site, test 2b line 33);
  otherwise emit `// <reason text>` as a source-comment immediately
  above the `Assert.Empty(...)` / `Assert.NotEmpty(...)` call (seven
  sites).
- **T13** done — Apply Dart-camelCase → C#-PascalCase rename on every
  public-member access: `result.errors` → `result.Errors`,
  `e.message` → `e.Message`, `.where(...)` → `.Where(...)`,
  `.toList()` → `.ToList()`, `.any(...)` → `.Any(...)`,
  `.contains(...)` → `.Contains(...)`.

## 4. Research Findings

none required — all 13 constructs derive verbatim from the RATIFIED
convspec at
`.codeconv/conversion-specs/test/module/module_typecheck_test.dart.md`
(13 construct rows, `escalations: []`, ratified with 10 cached idioms
+ 3 first-seen idioms + 1 sibling-mirror first-seen idiom, all
research-cited inline). The convspec's "Rationale + research
provenance" section cites authoritative Dart and .NET documentation
for every first-seen idiom (xUnit getting-started, Microsoft Learn
lambda-expressions / expression-bodied-members / string.Contains /
Enumerable.Where / Enumerable.ToList / Enumerable.Any / Assert.True /
Assert.Empty / Assert.NotEmpty, Dart api.dart.dev String.contains /
Iterable.where / Iterable.isNotEmpty / Iterable.any, Dart
dart.dev/language/functions). No additional research is required at
the plan stage; the plan mirrors construct rows verbatim.

## 5. Consistency Pass

fixed — derived from
`.codeconv/conversion-specs/test/module/module_typecheck_test.dart.md`
(RATIFIED convspec, schema_version 1, source_sha256
`3235d5f0f1363c1e269992793135758ad0fedb64b58307f06f71c3badfab9e68`
matching the live file, `escalations: []`). Every §2 construct
target_decision is reproduced from the convspec's `target_decision`
field with no divergence; every §3 task unit corresponds to either
a single convspec construct row or a convspec `conversion_units`
entry (cu-1 … cu-12). The `reason:`-handling policy (custom-message
overload pass-through where supported; source-comment where not) is
the convspec's documented policy; the matcher-routing for `isEmpty` /
`isNotEmpty` / `isTrue` mirrors the convspec's matcher rows verbatim.
Companion SUT-side conversion-spec
`.codeconv/conversion-specs/lib/analysis/type_checker/type_checker.dart.md`
is cited consistently for `TypeChecker.CheckSource`, `TypeCheckResult.Errors`,
and `TypeError.Message` PascalCase shapes. No idiom-vs-idiom conflict
and no construct-vs-CLAUDE.md conflict was found.

## 6. Escalations

None.
