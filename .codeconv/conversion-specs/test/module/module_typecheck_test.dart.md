> Conversion-spec artifact for test/module/module_typecheck_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/module/module_typecheck_test.dart
source_sha256: 3235d5f0f1363c1e269992793135758ad0fedb64b58307f06f71c3badfab9e68
target_code_unit: test/module/ModuleTypecheckTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope (xUnit pinned batch-wide as the
      project-policy test framework — same idiom as the precedent files
      .codeconv/conversion-specs/test/module/module_parser_test.dart.md and
      .codeconv/conversion-specs/test/module/module_syntax_v2_test.dart.md,
      recorded under `rf-dart-package-test-import-to-xunit-using`). Codegen
      MUST also add `using System.Collections.Generic;` (used for the
      `List<TypeError>` return-type of the file-level `bodyErrors` helper —
      see `dart.toplevel.expression_function_helper` below) and `using
      System.Linq;` (used for `.Where(...).ToList()` translation of the
      Dart `.where(...).toList()` chain in the same helper). NO
      `using System;` needed (no `Assert.Throws<Exception>` / no
      `throwsA(anything)` in this file). Project to a single namespace
      mirroring the Dart `test/module` directory (e.g.
      `<RootNs>.Test.Module`); the langpair-level `glp_runtime` ⇒
      `<RootNs>` policy is recorded in the langpair convspec, reused via
      KB hit (FR-012 / SC-007).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Cached idiom — reused verbatim, no re-research. xUnit selection is
      batch-wide project policy (NOT a file-local choice); reversing it
      would invalidate every prior test-file convspec. NUnit and MSTest
      remain corroborating alternatives recorded once at the import-idiom
      level. Module/namespace semantics: Dart
      `import 'package:test/test.dart'` exposes top-level
      `test()`/`group()`/`expect()`/`isEmpty`/`isNotEmpty` registration
      and matcher symbols; xUnit replaces them with attribute-driven
      discovery + `Assert.*` calls — the import is not 1-to-1 with
      `using Xunit;` alone (the file's `void main()` shape also
      rewrites — see `dart.package_test.main_entrypoint` below).
  - construct_key: dart.intra_package.import_directive_to_using_namespace
    source_form: "import 'package:glp_runtime/analysis/type_checker/type_checker.dart';"
    target_decision: >-
      Replace the single Dart intra-package import with the C# `using`
      directive for the namespace produced by the langpair's
      directory-to-namespace mapping (PascalCased path segments, file
      name dropped — Dart files are libraries, C# namespaces are
      coarser-grained units that contain many type declarations). So
      `package:glp_runtime/analysis/type_checker/type_checker.dart`
      ⇒ `using <RootNs>.Analysis.TypeChecker;` per the cached idiom
      `rf-dart-internal-package-import-to-csharp-using` /
      `rf-dart-package-import-to-csharp-using-namespace` (precedents:
      module_parser_test.dart.md, module_syntax_v2_test.dart.md). This
      one `using` covers `checkSource`, `TypeCheckResult`, and
      `TypeError` (the three symbols this file references) because
      they all live in the same C# namespace per
      .codeconv/conversion-specs/lib/analysis/type_checker/type_checker.dart.md.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cached idiom (precedents: module_parser_test.dart.md,
      module_syntax_v2_test.dart.md, boot_loader_test.dart.md).
      Granularity-mismatch nuance (explicitly addressed): Dart imports
      are FILE-GRAINED; C# `using` directives are NAMESPACE-GRAINED.
      Here only one file is imported, so the per-file vs per-namespace
      mismatch is invisible. No `as`-alias / `show` / `hide` directives
      in this file. Symbol-table nuance: the imported file exports a
      file-level helper function `checkSource` (top-level Dart
      function); per type_checker.dart.md that helper is converted to
      a `public static TypeCheckResult CheckSource(string source)`
      method on a `<RootNs>.Analysis.TypeChecker.TypeChecker` static
      facade class (Dart top-level function ⇒ C# static helper on a
      named host class — Dart allows free-floating top-level functions,
      C# requires every member to live inside a type). Test methods
      call it as `TypeChecker.CheckSource(...)` (qualified). An
      alternative `using static <RootNs>.Analysis.TypeChecker.TypeChecker;`
      would let the test methods call the bare `CheckSource(...)` but
      is rejected here because it pollutes the test class's symbol
      table with every other static member of the host class (e.g.
      future `CheckClauses`, `CheckProgram` helpers).
  - construct_key: dart.toplevel.expression_function_helper
    source_form: |-
      "/// Filter to only body-atom type errors (Phase 3 scope).
       /// Head errors are pre-existing and unrelated to cross-module type checking.
       List<TypeError> bodyErrors(TypeCheckResult result) =>
           result.errors.where((e) => e.message.contains('Body atom')).toList();"
    target_decision: >-
      Lift the Dart file-level helper function `bodyErrors` (an
      arrow-bodied / expression-bodied function — `=>` syntax with a
      single returning expression) into a `private static
      List<TypeError> BodyErrors(TypeCheckResult result)` static method
      on the enclosing xUnit test class
      (`ModuleTypecheckTests` — see `dart.package_test.main_entrypoint`
      below). Body is a single LINQ chain:
      `result.Errors.Where(e => e.Message.Contains("Body atom")).ToList()`.
      An expression-bodied member form is also acceptable in C# 6+:
      `private static List<TypeError> BodyErrors(TypeCheckResult result) =>
      result.Errors.Where(e => e.Message.Contains("Body atom")).ToList();`
      (this is the preferred shape because it is byte-for-byte
      structurally analogous to the Dart `=>`-bodied source). The
      Dart `///` doc-comments (two lines) map to a C# `///`
      doc-comment block (identical syntax — both use triple-slash and
      both render in editor tooltips) preserving the verbatim text
      (`Filter to only body-atom type errors (Phase 3 scope).` /
      `Head errors are pre-existing and unrelated to cross-module
      type checking.`). C# `///` doc-comments expect XML elements
      (`<summary>`, `<returns>`) for full IDE/IntelliSense support;
      codegen MUST wrap the two summary lines in `<summary>...</summary>`
      to satisfy the XML doc-comment schema documented at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/recommended-tags`.
    idiom_id: null
    research_finding_id: rf-dart-toplevel-arrow-fn-to-csharp-private-static-expression-bodied
    nuance: >-
      FIRST-SEEN idiom row (no prior test-file convspec lifts a Dart
      top-level helper FUNCTION — module_syntax_v2_test.dart.md lifts a
      local closure inside `void main()` to a private instance method;
      the file-level top-level-function case is distinct). Three
      nuances explicit: (1) Lifetime / scope — Dart top-level functions
      live at library scope and are callable from any `test()` in the
      same file; in C# they MUST live inside a type. `private static`
      on the enclosing test class is the minimal-visibility choice (no
      cross-class reuse needed); the alternative — host on a separate
      `internal static class TestHelpers` — is rejected because
      `bodyErrors` is exercised by tests in THIS file only. (2)
      Doc-comment-schema nuance — Dart `///` comments are FREE-FORM
      markdown; C# `///` comments parse as XML and produce build
      warnings (`CS1591` family) when not wrapped in known XML tags.
      Codegen MUST wrap content in `<summary>` to silence warnings
      while preserving the Dart-source semantics. (3) LINQ-vs-Iterable
      nuance — Dart `Iterable.where(predicate)` returns a LAZY
      `Iterable<T>`; C# `IEnumerable<T>.Where(predicate)` (from
      System.Linq) also returns lazy. Both materialise on `.toList()` /
      `.ToList()`. Argument-order, predicate signature, and lazy-eval
      semantics are IDENTICAL — the only surface difference is the
      identifier casing (Dart `where` ⇒ C# `Where`, Dart `toList` ⇒ C#
      `ToList`). Authoritative Dart `Iterable.where`
      (`https://api.dart.dev/stable/dart-core/Iterable/where.html`);
      authoritative .NET `Enumerable.Where<T>`
      (`https://learn.microsoft.com/dotnet/api/system.linq.enumerable.where`);
      authoritative .NET `Enumerable.ToList<T>`
      (`https://learn.microsoft.com/dotnet/api/system.linq.enumerable.tolist`).
  - construct_key: dart.lambda.single_parameter_arrow_expression
    source_form: |-
      "(e) => e.message.contains('Body atom')
       (e) => e.message.contains('math#check')"
    target_decision: >-
      Dart single-parameter arrow lambdas `(e) => <expr>` (used inside
      `.where(...)` on line 7 and inside `.any(...)` on line 33) map to
      C# lambda expressions `e => <expr>` (parens around the single
      parameter are optional in C# and idiomatically dropped). Concretely:
      `(e) => e.message.contains('Body atom')` ⇒
      `e => e.Message.Contains("Body atom")`; and
      `(e) => e.message.contains('math#check')` ⇒
      `e => e.Message.Contains("math#check")`. Codegen MUST also apply
      the Dart-camelCase ⇒ C#-PascalCase rule on the property access
      (`e.message` ⇒ `e.Message`) per the langpair's public-member
      casing convention recorded in
      .codeconv/conversion-specs/lib/analysis/type_checker/type_checker.dart.md
      (TypeError's `message` field is converted to a C# `Message`
      property).
    idiom_id: null
    research_finding_id: rf-dart-arrow-lambda-to-csharp-lambda
    nuance: >-
      FIRST-SEEN idiom row. Three nuances explicit: (1) Parameter-paren
      nuance — Dart REQUIRES parens around even a single parameter for
      arrow functions (`(e) => ...`, not `e => ...`); C# permits BOTH
      (`(e) => ...` and `e => ...`), with bare-identifier as the
      idiomatic form (Microsoft Learn "Lambda expressions"
      `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/lambda-expressions`).
      Codegen drops the parens for the single-parameter case to match
      the canonical C# style. (2) Capture-semantics nuance — Dart
      lambdas close over enclosing locals (`final result = ...; ...
      (e) => ...`); C# lambdas close over enclosing locals identically
      (delegate captures the variable, not its value); no
      `out`/`ref`/`in` capture issues arise in this file (no parameter
      is mutated through a closure). (3) Type-inference nuance — Dart
      infers `e` as `TypeError` from the `where`-receiver's element
      type; C# infers `e` as `TypeError` from the same source
      (`IEnumerable<TypeError>.Where(Func<TypeError,bool>)`). No
      explicit type annotation needed on either side. Authoritative
      Dart "Anonymous functions"
      (`https://dart.dev/language/functions#anonymous-functions`);
      authoritative .NET "Lambda expressions"
      (Microsoft Learn link above).
  - construct_key: dart.string.contains_substring_method
    source_form: |-
      "e.message.contains('Body atom')
       e.message.contains('math#check')"
    target_decision: >-
      Dart `String.contains(Pattern)` (when invoked with a String
      argument) maps DIRECTLY to C# `string.Contains(string)` —
      identical semantics: returns `true` iff the receiver string
      contains the argument as a substring. Both use ordinal
      (byte-/UTF-16-code-unit-) comparison by default. Concretely:
      `e.message.contains('Body atom')` ⇒
      `e.Message.Contains("Body atom")`; and
      `e.message.contains('math#check')` ⇒
      `e.Message.Contains("math#check")`.
    idiom_id: null
    research_finding_id: rf-dart-string-contains-to-csharp-string-contains
    nuance: >-
      FIRST-SEEN idiom row (recorded for KB completeness — every
      Dart→C# conversion needs the canonical string-substring-test
      idiom). Three nuances explicit: (1) Pattern-vs-string nuance —
      Dart's `String.contains` accepts a `Pattern` (which can be a
      `String` OR a `RegExp`); both calls in this file pass a literal
      `String` argument, so the C# overload `string.Contains(string)`
      suffices. For the RegExp case (not exercised here), codegen
      would emit `Regex.IsMatch(receiver, pattern)`. (2)
      Culture/ordinal nuance — Dart `String.contains` performs
      ORDINAL Unicode-code-unit comparison (no locale collation); C#
      `string.Contains(string)` defaults to ORDINAL (since .NET Core
      2.1) per Microsoft Learn `string.Contains`
      (`https://learn.microsoft.com/dotnet/api/system.string.contains`).
      For locale-sensitive matching, C# offers
      `Contains(string, StringComparison)` — NOT needed here because
      both literals (`"Body atom"`, `"math#check"`) are ASCII. (3)
      Null-receiver nuance — Dart `String.contains` on a non-nullable
      `String` cannot throw NPE; C# `string.Contains` on a nullable
      `string?` would throw NRE on null receiver. In this file, the
      receiver is `e.Message` where `e` is `TypeError` and `Message`
      is non-nullable per type_checker.dart.md's TypeError conversion
      — no null-check needed. Authoritative Dart `String.contains`
      (`https://api.dart.dev/stable/dart-core/String/contains.html`);
      authoritative .NET `string.Contains(string)` (Microsoft Learn
      link above).
  - construct_key: dart.package_test.main_entrypoint
    source_form: |-
      "void main() {
         group('Phase 3 - 2a: ...', () { test(...); });
         group('Phase 3 - 2b: ...', () { test(...); });
         ... (seven sibling groups)
       }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint; xUnit
      discovers `[Fact]` methods by reflection — there is NO per-file
      entrypoint to emit. Eliminate `main` entirely. Its body (seven
      top-level `group(...)` calls — Phase 3 - 2a through 2g) becomes
      seven enclosing test classes per the cached
      `rf-dart-package-test-group-to-xunit-class` idiom from
      module_parser_test.dart.md (SIBLING groups, no shared state →
      one class per group, NOT flatten-with-[Trait] as in
      module_syntax_v2_test.dart.md). Class names (PascalCase with
      non-identifier characters mangled to camel-join):
      `Phase3_2aRemoteGoalAgainstImportedDeclarationTests`,
      `Phase3_2bRemoteGoalWithoutImportedDeclarationTests`,
      `Phase3_2cRemoteGoalArityMismatchTests`,
      `Phase3_2dDeepModulePathTests`,
      `Phase3_2eImportedAncestorProcedureTests`,
      `Phase3_2fMultipleImportedProceduresTests`,
      `Phase3_2gDynamicRemoteGoalSkippedTests`. The file-level
      helper `bodyErrors` (see above) is hosted on ONE of these
      classes (idiomatically the first) and referenced by the rest
      via a fully-qualified static call — OR equivalently, lifted to
      an `internal static class ModuleTypecheckTestHelpers`. Spec
      default: host on a separate `internal static class
      ModuleTypecheckTestHelpers` in the SAME `.cs` file so each test
      class can call `ModuleTypecheckTestHelpers.BodyErrors(result)`
      without inheriting from a shared base.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Cached idiom (precedents: module_parser_test.dart.md,
      module_syntax_v2_test.dart.md, boot_loader_test.dart.md,
      mad_error_handling_test.dart.md). Lifecycle nuance (explicitly
      addressed): Dart `main` is invoked once per test-file process;
      xUnit has no per-file hook. THIS file's `main` body is exactly
      seven sibling `group()` calls plus one top-level helper
      function reference — no file-level setUp / no shared state, so
      the omission is lossless (no `IClassFixture<>` migration
      needed). Helper-hosting nuance (explicitly addressed): the
      `bodyErrors` top-level function is referenced by EVERY group's
      `test(...)` body, so the helper-class strategy (`internal
      static class ModuleTypecheckTestHelpers`) provides ONE
      authoritative location reachable from all seven test classes
      without inheritance hierarchies — preferred over per-class
      duplication.
  - construct_key: dart.package_test.group_block
    source_form: |-
      "group('Phase 3 - 2a: remote goal type-checks against imported declaration', () { test(...); });
       group('Phase 3 - 2b: remote goal fails without imported declaration', () { test(...); });
       group('Phase 3 - 2c: remote goal fails on arity mismatch', () { test(...); });
       group('Phase 3 - 2d: deep module path', () { test(...); });
       group('Phase 3 - 2e: imported ancestor procedure (no path)', () { test(...); });
       group('Phase 3 - 2f: multiple imported procedures', () { test(...); });
       group('Phase 3 - 2g: dynamic remote goal skipped', () { test(...); });"
    target_decision: >-
      Seven sibling top-level `group(...)` calls (NOT nested; not
      inside another group). Map each to its own PascalCase xUnit
      test class within the same `.cs` file. Each label has the form
      `'Phase 3 - 2<x>: <description>'`; non-identifier characters
      (spaces, dashes, colons, parentheses) MUST be stripped or
      camel-joined to produce valid C# identifiers (e.g.
      `'Phase 3 - 2a: remote goal type-checks against imported
      declaration'` ⇒ `Phase3_2aRemoteGoalAgainstImportedDeclarationTests`).
      The original label MUST be preserved verbatim via
      `[Fact(DisplayName = "<original label>")]` on every test method
      so reporter output keeps the Dart sentence form. SIBLING (not
      nested) groups with NO shared state ⇒ each group becomes a
      FULLY INDEPENDENT class — no shared base class, no
      `IClassFixture<>`. The multi-class-per-file shape is
      documented xUnit usage
      (`https://xunit.net/docs/getting-started/v3/getting-started`);
      the one-class-per-`group` decision matches the precedent
      module_parser_test.dart.md's sibling-group rule.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md applied the
      sibling-group flatten-to-class rule; module_syntax_v2_test.dart.md
      applied the alternative flatten-to-[Trait] rule — both are
      recorded in the idiom; codegen chooses per topology). Topology
      nuance EXPLICITLY addressed: this file's groups are SIBLINGS
      (seven top-level `group(...)` calls in `main`), not NESTED. No
      outer group exists; no shared `late` field; no `setUp`/`tearDown`
      → no force to single-class FLATTEN. One class per group is the
      idiomatic target. Name-mangling nuance: every group label
      contains the substring `"Phase 3 - 2<letter>: "` plus a
      hyphen-separated description; the hyphens, colons, parentheses,
      and spaces MUST be removed (or camel-joined) because none are
      C# identifier characters. Reporter-trait alternative (a single
      class with `[Trait("Phase", "3 - 2a")]` per method) is recorded
      in the research finding but rejected here because seven
      independent classes produce cleaner Visual Studio Test Explorer
      grouping for the seven-Phase test breakdown.
  - construct_key: dart.package_test.test_call_simple
    source_form: |-
      "test('math # check(N?) passes with matching imported declaration', () { ... });
       test('math # check(N?) fails without imported declaration', () { ... });
       test('arity mismatch between call and imported declaration', () { ... });
       test('ui#actors # render type-checks against deep imported declaration', () { ... });
       test('imported procedure without path type-checks local calls', () { ... });
       test('multiple imported declarations each checked independently', () { ... });
       test('M # goal(X) where M is a variable is not type-checked', () { ... });"
    target_decision: >-
      Each Dart `test(label, body)` with a synchronous closure body and
      NO `skip:` argument becomes a `public void` instance method on
      the enclosing xUnit class, decorated with
      `[Fact(DisplayName = "<original label>")]`. The method name is
      the label PascalCased with non-identifier characters stripped
      (e.g. `'math # check(N?) passes with matching imported
      declaration'` ⇒ `MathCheckNPassesWithMatchingImportedDeclaration`).
      All seven `test(...)` calls in this file are synchronous (no
      `async`/`Future`), so NO target method is `async Task`. The
      arrange/act/assert closure body translates statement-for-
      statement into the C# method body (`final result = checkSource(...)`
      ⇒ `var result = TypeChecker.CheckSource(...)`; `final errors =
      bodyErrors(result)` ⇒ `var errors =
      ModuleTypecheckTestHelpers.BodyErrors(result)`; `expect(...)`
      calls ⇒ `Assert.*` calls — see the matcher-routing constructs
      below).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Cached idiom (precedents: module_parser_test.dart.md,
      module_syntax_v2_test.dart.md, mad_error_handling_test.dart.md).
      Async nuance (explicitly addressed even though absent in this
      file): a Dart `test('...', () async { ... })` would target
      `public async Task <Name>()` (xUnit awaits the returned Task).
      None of THIS file's callbacks are async, so no target method is
      async. Closure-capture nuance: callbacks here capture NOTHING
      from `main()` scope (no `setUp` variables, no `late` field) —
      each test constructs its own `result` inside its own body via
      `checkSource(<triple-quoted-source>)`, so the xUnit translation
      needs no instance fields and no constructor. Identifier-mangling
      nuance: every label contains `#` characters (e.g.
      `'math # check'`, `'M # goal'`); these are NOT C# identifier
      characters and MUST be dropped (becoming `MathCheck`,
      `MGoal`). `DisplayName` preserves them verbatim for reporter
      output.
  - construct_key: dart.local.final_var_declaration
    source_form: |-
      "final result = checkSource('''...''');
       final errors = bodyErrors(result);"
    target_decision: >-
      Every `final <name> = <expr>;` local in this file maps to
      `var <name> = <expr>;` in C#. `final` in Dart on a LOCAL is the
      "single-assignment, type-inferred" idiom; C# `var` is identical
      except `var` is NOT single-assignment (it's just type-inferred
      and mutable). For test method locals — which are never
      reassigned in any of this file's bodies — the looser `var` is
      observably equivalent. The seven `final result = checkSource(...)`
      declarations (one per test) map to `var result =
      TypeChecker.CheckSource(...)`; the three `final errors =
      bodyErrors(result)` declarations (in tests 2b, 2c, and the
      explicit-error-variable bodies) map to `var errors =
      ModuleTypecheckTestHelpers.BodyErrors(result)`.
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Cached idiom (precedents: module_parser_test.dart.md,
      module_syntax_v2_test.dart.md, boot_loader_test.dart.md,
      varref_pointer_test.dart.md). Single-assignment nuance
      (explicitly addressed): Dart `final` enforces no-reassignment at
      the language level; C# `var` does NOT. For these test bodies
      the distinction is invisible (no local is ever reassigned in
      this file). If a future test reassigned a `final`-target local,
      codegen would need to flag it — but since `final` PROHIBITS
      reassignment in Dart, that case cannot arise from a valid Dart
      source. No `new`-keyword nuance applies here because the
      `checkSource(...)` call is a function call (not a constructor),
      and `bodyErrors(...)` likewise.
  - construct_key: dart.string.triple_quoted_raw_literal
    source_form: |-
      "'''
         imported procedure math#check(Integer?).
         procedure validate(Integer?).
         validate(N) :- true | math # check(N?).
       '''
       ... (used in all seven test bodies — every checkSource arrange step)"
    target_decision: >-
      Dart triple-single-quoted multi-line string literals (used to
      embed every `.glp` source fixture in this file — seven literals
      total, one per `test(...)`) map to C# 11 raw string literals
      (`""" ... """`) per the cached idiom
      `rf-dart-triple-quoted-string-to-csharp-raw-string` from
      module_parser_test.dart.md / module_syntax_v2_test.dart.md /
      boot_loader_test.dart.md. The literal payload is byte-identical
      across the boundary; codegen MUST emit the closing `"""` at the
      appropriate column so C#'s common-leading-whitespace stripping
      yields the SAME content as the Dart literal. Fallback to C#
      verbatim strings (`@"..."`) for pre-C#11 targets is equivalent
      here because no fixture in this file contains a `"`.
    idiom_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    research_finding_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md,
      module_syntax_v2_test.dart.md, boot_loader_test.dart.md).
      Whitespace nuance (explicitly addressed): Dart triple-quoted
      strings preserve leading whitespace exactly; C# 11 raw strings
      strip a common indent matched to the closing `"""` column.
      This file's literals begin with a `\n` immediately after `'''`
      followed by lines indented eight spaces (matching the Dart
      source indentation). For C#-side raw strings, codegen MUST
      align the closing `"""` to eight-space indentation so the
      common-leading-whitespace strip yields the SAME content
      observable to the GLP lexer (which discards leading whitespace
      during tokenisation anyway, making the strip semantically
      lossless). Newline-encoding nuance: Dart `'''...'''` uses `\n`
      line endings on all platforms; C# 11 raw strings preserve
      source-file line endings — on Windows-edited source files this
      could be `\r\n`. Codegen MUST normalise to `\n` (LF) line
      endings inside the raw string literal so the byte-identity
      invariant holds against the Dart fixture. Leading-newline
      nuance: Dart `'''\n<content>\n'''` preserves the leading `\n`;
      C# 11 raw strings discard the newline immediately after the
      opening `"""` per the C# 11 raw-string spec. For this file's
      use case the leading newline is whitespace consumed by the GLP
      lexer, so the test outcome is unaffected.
  - construct_key: dart.string.single_quoted_literal
    source_form: |-
      "'Body atom'
       'math#check'
       'Remote call should type-check against imported declaration'
       'Remote call without imported declaration should produce type error'
       'Error should mention the missing imported declaration'
       'Calling with 2 args when declaration has 1 should be a type error'
       'Deep module path should type-check correctly'
       'Imported procedure without path should work like local declaration'
       'Multiple imported procedures should each be found'
       'Dynamic module dispatch should skip type checking'"
    target_decision: >-
      Dart single-quoted single-line string literals (every label,
      matcher substring, and `reason:`-argument value in this file's
      `expect` calls and helper bodies) map to C# double-quoted string
      literals (`"Body atom"`, `"math#check"`, etc.) per the cached
      idiom `rf-dart-single-quoted-string-to-csharp-double-quoted-string`
      from module_parser_test.dart.md. Escape-sequence nuance is
      trivial here because no literal in this file uses Dart-specific
      escapes (`\$`, `\u{...}`) — all content is ASCII printable. The
      `#` character (e.g. in `'math#check'`) is NOT a Dart escape
      sequence; it's a literal `#` in both Dart and C# — passes
      through unchanged.
    idiom_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    research_finding_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md). Quote-
      character nuance (explicitly addressed): Dart accepts BOTH
      `'...'` and `"..."` for single-line literals (no semantic
      difference); C# accepts ONLY `"..."`. No embedded-quote
      transformation needed in this file (no literal contains either
      kind of quote internally). The `#` characters in `'math#check'`
      and `'M # goal(X)'` are pass-through; no Dart→C# transformation
      applies.
  - construct_key: dart.package_test.expect_isEmpty_matcher
    source_form: |-
      "expect(bodyErrors(result), isEmpty, reason: 'Remote call should type-check against imported declaration');
       expect(bodyErrors(result), isEmpty, reason: 'Deep module path should type-check correctly');
       expect(bodyErrors(result), isEmpty, reason: 'Imported procedure without path should work like local declaration');
       expect(bodyErrors(result), isEmpty, reason: 'Multiple imported procedures should each be found');
       expect(bodyErrors(result), isEmpty, reason: 'Dynamic module dispatch should skip type checking');"
    target_decision: >-
      Dart `expect(<List-valued-expression>, isEmpty, reason: '<msg>')`
      maps to xUnit `Assert.Empty(<expression>);` per the cached
      idiom `rf-dart-expect-isEmpty-to-xunit-assert-empty` from
      module_parser_test.dart.md / module_syntax_v2_test.dart.md /
      localize_test.dart.md. Five uses in this file (in tests 2a, 2d,
      2e, 2f, 2g). The `reason:` named argument carries a
      human-readable failure-explanation string — xUnit's
      `Assert.Empty(IEnumerable)` does NOT accept a custom message
      parameter (a documented xUnit design choice; assertion failures
      print the value and a fixed `Assert.Empty() Failure` line). The
      `reason` text is therefore LOST in mechanical translation; the
      faithful preservation is to inject it as either (a) the
      `[Fact(DisplayName = ...)]` value (which already carries the
      `test(...)` label, so the `reason` would supplement, not
      replace), or (b) a C# `// <reason text>` source-comment on the
      assertion line. Spec default: emit `// <reason text>` as a
      source-comment immediately above each `Assert.Empty(...)` call
      to preserve the documentation value without altering test
      semantics. The alternative — switching to
      `Assert.True(bodyErrors.Count == 0, "<reason>")` — is recorded
      as a documented fallback for tests that REQUIRE the custom
      message to appear in the failure output (not the case here:
      the seven test method names + `DisplayName` already convey the
      expected outcome).
    idiom_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    research_finding_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    nuance: >-
      Cached idiom (precedents: module_parser_test.dart.md,
      module_syntax_v2_test.dart.md, localize_test.dart.md).
      Collection-shape nuance: Dart `isEmpty` works on any
      `Iterable`/`String`/`Map` with an `isEmpty` getter; xUnit
      `Assert.Empty` requires `IEnumerable` (covers `List<T>`,
      `string`, `IDictionary<K,V>` — all the same shapes). The
      `bodyErrors(result)` return type is `List<TypeError>` (Dart) ⇒
      `List<TypeError>` (C# — implements `IEnumerable<TypeError>`),
      so `Assert.Empty` accepts it directly. REASON-PARAMETER nuance
      (explicitly addressed — load-bearing for this file): Dart
      `expect`'s `reason:` named parameter is a free-form failure-
      message string that prints alongside the matcher failure
      output. xUnit's `Assert.Empty(IEnumerable)` has NO custom-
      message overload (per
      `https://learn.microsoft.com/dotnet/api/xunit.assert.empty`).
      The faithful translation injects the `reason` as a source-
      comment (preserves intent without altering semantics) OR
      switches to `Assert.True(collection.Count == 0, message)` for
      a message-bearing alternative. Codegen MUST NOT silently drop
      the `reason` text — it is part of the test's documented
      design intent. Authoritative: xunit `Assert.Empty` API
      reference (Microsoft Learn link above); Dart
      `package:test`'s `expect(actual, matcher, {String? reason})`
      signature (`https://pub.dev/documentation/test_api/latest/expect/expect.html`).
  - construct_key: dart.package_test.expect_isNotEmpty_matcher
    source_form: |-
      "expect(errors, isNotEmpty, reason: 'Remote call without imported declaration should produce type error');
       expect(errors, isNotEmpty, reason: 'Calling with 2 args when declaration has 1 should be a type error');"
    target_decision: >-
      Dart `expect(<List-valued-expression>, isNotEmpty, reason:
      '<msg>')` maps to xUnit `Assert.NotEmpty(<expression>);` —
      mirror of the `isEmpty` ⇒ `Assert.Empty` mapping per the
      matcher-routing table extended by
      module_syntax_v2_test.dart.md. Two uses in this file (in
      tests 2b and 2c). Same `reason:` ⇒ source-comment treatment
      as the `isEmpty` case above.
    idiom_id: null
    research_finding_id: rf-dart-expect-isNotEmpty-to-xunit-assert-notempty
    nuance: >-
      FIRST-SEEN idiom row (module_parser_test.dart.md and
      module_syntax_v2_test.dart.md exercised `isEmpty` but not
      `isNotEmpty`; this is the first test-file convspec to record
      the mirror). Both Dart `isNotEmpty` and xUnit `Assert.NotEmpty`
      pass iff the iterable yields ≥1 element. Authoritative Dart
      `Iterable.isNotEmpty` (`https://api.dart.dev/stable/dart-core/Iterable/isNotEmpty.html`);
      authoritative .NET `Assert.NotEmpty`
      (`https://learn.microsoft.com/dotnet/api/xunit.assert.notempty`).
      Collection-shape and reason-parameter nuances are IDENTICAL to
      the `isEmpty` row above (same idiom family). Recorded as a
      first-seen sibling row to keep the matcher-routing table
      complete.
  - construct_key: dart.package_test.expect_isTrue_matcher_with_any
    source_form: "expect(errors.any((e) => e.message.contains('math#check')), isTrue, reason: 'Error should mention the missing imported declaration');"
    target_decision: >-
      Dart `expect(<bool-valued-expression>, isTrue, reason: '<msg>')`
      maps to xUnit `Assert.True(<expression>, "<reason text>");` per
      the cached idiom `rf-dart-expect-isTrue-to-xunit-assert-true`
      from module_syntax_v2_test.dart.md (which also covered the
      `isFalse` / `isNull` / `isNotNull` matchers in the same routing
      table). One use in this file (test 2b, line 33). The inner
      `errors.any((e) => e.message.contains('math#check'))` maps to
      C# `errors.Any(e => e.Message.Contains("math#check"))` per the
      cached LINQ-vs-Iterable nuance recorded in the
      `dart.toplevel.expression_function_helper` row above. xUnit
      `Assert.True(bool, string)` ACCEPTS a custom failure message —
      so unlike the `Assert.Empty` case above, the `reason:` text
      can (and SHOULD) be passed through verbatim as the second
      argument. Concretely:
      `Assert.True(errors.Any(e => e.Message.Contains("math#check")),
      "Error should mention the missing imported declaration");`.
    idiom_id: rf-dart-expect-isTrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Cached idiom (precedent: module_syntax_v2_test.dart.md,
      smoke_test.dart.md). Reason-parameter nuance (explicitly
      addressed — DIFFERENT from the `Assert.Empty` case): xUnit's
      `Assert.True(bool, string)` overload (Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/xunit.assert.true`)
      DOES accept a custom message — so the Dart `reason:` text is
      preserved as the second argument to `Assert.True`, NOT as a
      source-comment. This is the FAITHFUL translation; the
      same `reason` text on the `isEmpty` / `isNotEmpty` cases is
      lost only because those assertion methods have no
      message overload. Iterable.any nuance: Dart
      `Iterable<T>.any(bool Function(T))` returns `true` iff the
      predicate matches ≥1 element; .NET `IEnumerable<T>.Any(Func<T,
      bool>)` (System.Linq) has IDENTICAL semantics. Authoritative
      Dart `Iterable.any` (`https://api.dart.dev/stable/dart-core/Iterable/any.html`);
      authoritative .NET `Enumerable.Any`
      (`https://learn.microsoft.com/dotnet/api/system.linq.enumerable.any`).
conversion_units:
  - cu-1: file-scope using directives (Xunit + System.Collections.Generic + System.Linq + SUT namespace from glp_runtime/analysis/type_checker/type_checker.dart)
  - cu-2: namespace declaration mirroring the test/module path (e.g. <RootNs>.Test.Module)
  - cu-3: internal static class ModuleTypecheckTestHelpers hosting the lifted top-level helper `private static List<TypeError> BodyErrors(TypeCheckResult result) => result.Errors.Where(e => e.Message.Contains("Body atom")).ToList();` with preserved `<summary>` XML doc-comment from the Dart `///`-comments
  - cu-4: class Phase3_2aRemoteGoalAgainstImportedDeclarationTests with 1 `[Fact(DisplayName="...")]` method (uses Assert.Empty)
  - cu-5: class Phase3_2bRemoteGoalWithoutImportedDeclarationTests with 1 `[Fact]` method (uses Assert.NotEmpty + Assert.True with reason-as-message + LINQ Any)
  - cu-6: class Phase3_2cRemoteGoalArityMismatchTests with 1 `[Fact]` method (uses Assert.NotEmpty)
  - cu-7: class Phase3_2dDeepModulePathTests with 1 `[Fact]` method (uses Assert.Empty)
  - cu-8: class Phase3_2eImportedAncestorProcedureTests with 1 `[Fact]` method (uses Assert.Empty)
  - cu-9: class Phase3_2fMultipleImportedProceduresTests with 1 `[Fact]` method (uses Assert.Empty)
  - cu-10: class Phase3_2gDynamicRemoteGoalSkippedTests with 1 `[Fact]` method (uses Assert.Empty)
  - cu-11: raw-string-literal payloads (`"""..."""`) for every embedded `.glp` source fixture across the 7 test methods, with LF line endings and aligned closing-delimiter column so the literal payload is byte-identical to the Dart fixture
  - cu-12: every `reason:`-argument value preserved either as a passed `Assert.True(bool, message)` second-argument (where the assertion overload supports it) or as a source-comment immediately above the assertion line (where it does not — Assert.Empty / Assert.NotEmpty)
escalations: []
```

## Rationale + research provenance

### Cached-idiom reuse profile (SC-007 / FR-012)

10 of the 13 constructs in this file resolve via a CACHED idiom_id from
prior test-file convspecs (module_parser_test.dart.md,
module_syntax_v2_test.dart.md, boot_loader_test.dart.md,
smoke_test.dart.md, localize_test.dart.md) and from the SUT convspec
.codeconv/conversion-specs/lib/analysis/type_checker/type_checker.dart.md:

- `rf-dart-package-test-import-to-xunit-using` (precedent:
  module_parser_test.dart.md, module_syntax_v2_test.dart.md)
- `rf-dart-internal-package-import-to-csharp-using` (precedent:
  module_parser_test.dart.md, boot_loader_test.dart.md)
- `rf-dart-package-test-main-omit-in-xunit` (precedent:
  module_parser_test.dart.md, module_syntax_v2_test.dart.md,
  mad_error_handling_test.dart.md)
- `rf-dart-package-test-group-to-xunit-class` (precedent:
  module_parser_test.dart.md applied the sibling-group rule;
  module_syntax_v2_test.dart.md applied the flatten-with-[Trait] rule)
- `rf-dart-test-callback-to-xunit-method-body` (multiple precedents)
- `rf-dart-final-local-to-csharp-var-local` (precedents:
  module_parser_test.dart.md, module_syntax_v2_test.dart.md,
  boot_loader_test.dart.md, varref_pointer_test.dart.md)
- `rf-dart-triple-quoted-string-to-csharp-raw-string` (precedents:
  module_parser_test.dart.md, module_syntax_v2_test.dart.md,
  boot_loader_test.dart.md)
- `rf-dart-single-quoted-string-to-csharp-double-quoted-string`
  (precedent: module_parser_test.dart.md)
- `rf-dart-expect-isEmpty-to-xunit-assert-empty` (precedents:
  module_parser_test.dart.md, module_syntax_v2_test.dart.md,
  localize_test.dart.md)
- `rf-dart-expect-isTrue-to-xunit-assert-true` (precedents:
  module_syntax_v2_test.dart.md, smoke_test.dart.md)

Reusing these cached idioms verbatim (no re-research, no re-derivation)
satisfies the FR-012 / SC-007 consistency guarantee. The KB-lookup
decision-order from `convspec_idiom_schema.md` was applied per
construct: KB lookup hit ⇒ REUSE.

### Three FIRST-SEEN idiom rows (research-justified, NO escalation)

Three constructs require new idiom rows because no precedent test-file
convspec covers them. Each was researched against official Dart + .NET
documentation per FR-024.

1. **`rf-dart-toplevel-arrow-fn-to-csharp-private-static-expression-bodied`**
   — file-level Dart helper `List<TypeError> bodyErrors(TypeCheckResult
   result) => ...` (a top-level arrow-bodied function). No prior test
   convspec lifts a file-level helper function (module_syntax_v2_test
   lifts a LOCAL closure inside `void main()`; module_parser_test has
   no helper at all). Authoritative bases: Dart "Functions"
   (`https://dart.dev/language/functions`) — top-level functions live
   at library scope; arrow syntax `=> expr` is shorthand for `{ return
   expr; }`. C# "Expression-bodied members"
   (`https://learn.microsoft.com/dotnet/csharp/programming-guide/statements-expressions-operators/expression-bodied-members`)
   — `=> expr` is identical shorthand on methods in C# 6+. C#
   namespace rules require every method to live inside a type;
   `internal static class ModuleTypecheckTestHelpers` is the
   minimal-visibility host that all seven test classes can call into
   without inheritance.

2. **`rf-dart-arrow-lambda-to-csharp-lambda`** — single-parameter Dart
   arrow lambdas inside `.where(...)` / `.any(...)`. Authoritative
   bases: Dart "Anonymous functions"
   (`https://dart.dev/language/functions#anonymous-functions`) — Dart
   REQUIRES parens around even a single parameter for arrow
   functions; C# "Lambda expressions"
   (`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/lambda-expressions`)
   — C# permits both `(e) => ...` and `e => ...`, with bare-identifier
   as the canonical style. Capture-semantics, type-inference, and
   delegate-vs-Func dispatch are identical for the file's predicate
   usage (no `out`/`ref`/`in` parameters; no async lambdas).

3. **`rf-dart-string-contains-to-csharp-string-contains`** — Dart
   `String.contains(String)` ⇒ C# `string.Contains(string)`.
   Authoritative bases: Dart `String.contains`
   (`https://api.dart.dev/stable/dart-core/String/contains.html`); C#
   `string.Contains`
   (`https://learn.microsoft.com/dotnet/api/system.string.contains`).
   Both default to ordinal Unicode-code-unit comparison; both throw
   on null receiver (NPE / NRE — neither operand is nullable here).
   The Pattern-vs-string nuance (Dart accepts `RegExp` as well) is
   not exercised in this file (both calls pass literal strings) but
   recorded for downstream files.

The fourth construct on the `Assert.NotEmpty` mirror
(`rf-dart-expect-isNotEmpty-to-xunit-assert-notempty`) is recorded as
a FIRST-SEEN idiom row even though it sits in the same matcher
routing-table family as the cached `Assert.Empty` row: no prior
test-file convspec exercised `isNotEmpty`, so this is the first KB
entry for the mirror. Authoritative bases: Dart
`Iterable.isNotEmpty`
(`https://api.dart.dev/stable/dart-core/Iterable/isNotEmpty.html`);
xUnit `Assert.NotEmpty`
(`https://learn.microsoft.com/dotnet/api/xunit.assert.notempty`). The
two matchers are pure mirrors (pass-iff-yields-≥1-element vs
pass-iff-yields-0-elements) and share the same collection-shape and
reason-parameter nuances as `Assert.Empty`.

### Sibling-group topology (seven independent classes)

This file has SEVEN SIBLING top-level groups (Phase 3 - 2a through
2g) — same topology as module_parser_test.dart.md (which has six),
distinct from boot_loader_test.dart.md (outer + three-inner) and
distinct from module_syntax_v2_test.dart.md (flat groups with
[Trait]). Because no outer group exists, no `setUp` appears anywhere
in `main`, and no `late` field is captured, none of the per-group
test classes share state. The cleanest target shape is therefore one
C# `public class` per Dart `group`, all in the same `.cs` file —
matching the precedent applied by module_parser_test.dart.md.
Multi-class-per-file is fully supported by xUnit's reflection-based
test discovery
(`https://xunit.net/docs/getting-started/v3/getting-started`). The
alternative — FLATTEN to one class with `[Trait("Phase","3-2a")]`
per method (the choice made by module_syntax_v2_test.dart.md) — was
considered but rejected here because (i) seven classes produce
cleaner VS Test Explorer grouping for the seven-Phase test
breakdown; (ii) the Phase-3-2a-through-2g labels are themselves a
hierarchy worth surfacing in the test runner UI; (iii) one test
method per class matches the simplicity of the source file (each
group contains exactly one `test(...)` call). The idiom registry
records both targets (class-per-group / flatten-with-[Trait]) and
codegen picks per topology.

### `reason:` named-parameter handling (explicit policy)

Every `expect(...)` call in this file uses the `reason:` named
parameter to attach a human-readable failure-message string. xUnit's
assertion methods are INCONSISTENT in their support for custom
messages:

- `Assert.True(bool, string)` and `Assert.False(bool, string)`
  ACCEPT a message (Microsoft Learn
  `https://learn.microsoft.com/dotnet/api/xunit.assert.true`) — one
  use in this file (test 2b, `Assert.True(errors.Any(...),
  "<reason>")`).
- `Assert.Empty(IEnumerable)` and `Assert.NotEmpty(IEnumerable)` do
  NOT accept a custom message (Microsoft Learn
  `https://learn.microsoft.com/dotnet/api/xunit.assert.empty`) —
  five and two uses respectively in this file.

For assertions that support a custom-message overload, the Dart
`reason:` text passes through verbatim as the second argument. For
assertions that do not, the faithful preservation is to emit
`// <reason text>` as a source-comment immediately above the
assertion line — preserves the documentation intent without altering
test semantics, and the fact-method `DisplayName` already covers the
"what the test asserts" angle of the documentation. The alternative
— switching to `Assert.True(collection.Count == 0, "<reason>")` for
the message-bearing variant — is recorded in the matcher idiom as a
documented fallback but rejected as the default because it loses the
strong type signal of `Assert.Empty` (which xUnit reports as
`Assert.Empty() Failure` with the collection's actual length, more
informative than a generic `Assert.True() Failure`).

### Helper-class hosting decision

The Dart top-level `bodyErrors` helper is referenced by every group's
`test(...)` body — explicitly by `bodyErrors(result)` in tests 2b,
2c (stored in `final errors`) and inside the assertion arguments in
2a, 2d, 2e, 2f, 2g. The three viable hosting strategies for the
lifted C# helper are: (a) duplicate `private static BodyErrors` onto
each of the seven per-group test classes (DRY violation); (b)
introduce a shared base class `ModuleTypecheckTestBase` with
`protected static BodyErrors` (xUnit allows test-class inheritance
per `https://xunit.net/docs/shared-context` but discourages it for
non-fixture sharing); (c) host on a separate `internal static class
ModuleTypecheckTestHelpers` in the same `.cs` file, called as
`ModuleTypecheckTestHelpers.BodyErrors(result)` from each test
method. Spec default = (c): single source of truth, no inheritance
coupling, the `internal` visibility scopes the helper to the test
assembly. The qualified-call cost (`ModuleTypecheckTestHelpers.`
prefix) is acceptable for seven call sites and CAN be elided via
`using static <RootNs>.Test.Module.ModuleTypecheckTestHelpers;` at
file scope if codegen elects the shorter form (recorded as a
documented variant).

### LINQ vs Iterable equivalence

This file uses two Iterable→LINQ idioms: `.where(...).toList()` in
the `bodyErrors` helper and `.any(...)` in the test 2b assertion.
Both Dart `Iterable` methods have IDENTICAL semantics to their .NET
`Enumerable` counterparts (`.Where(...)`/`.ToList()` and `.Any(...)`)
per the authoritative references cited in the construct rows above.
The only surface differences are identifier casing (camelCase vs
PascalCase) and the parameter-paren convention on lambdas — both
handled by the relevant idiom rows. No semantic divergence; no
escalation.

### Why no escalations (FR-013)

Every construct has a clear, single-decision target shape grounded in
official Dart and .NET documentation. The two "soft" decisions
(one-class-per-group vs FLATTEN-with-[Trait]; helper-class vs
shared-base vs duplicate) are documented project-wide options with
this file's choice explicitly justified by topology (sibling groups,
single-method-per-group) and call-site count (seven). The
`reason:`-parameter handling is a deterministic per-assertion-method
policy (custom-message overload exists ⇒ pass through; doesn't exist
⇒ source-comment). No construct involves an idiom-vs-research
conflict or an idiom-vs-idiom conflict, and nothing is undecidable.
`escalations: []` is therefore intentional, not a placeholder.
