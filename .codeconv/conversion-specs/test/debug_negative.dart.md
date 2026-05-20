> Conversion-spec artifact for test/debug_negative.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/debug_negative.dart
source_sha256: fda6e94ebaad1f79d40ea453ff0b6a856c963a9eef87b1122c1c238126874594
target_code_unit: test/DebugNegative.cs
constructs:
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/analysis/type_checker/type_checker.dart';
       import 'package:glp_runtime/analysis/type_checker/type_parser.dart';
       import 'package:glp_runtime/compiler/lexer.dart';
       import 'package:glp_runtime/compiler/parser.dart';"
    target_decision: >-
      Map each `package:glp_runtime/...` import to a `using` directive
      that names the C# namespace produced by converting the referenced
      SUT file. The two `analysis/type_checker/<file>.dart` imports
      share a single directory and (per the existing SUT-side specs at
      `.codeconv/conversion-specs/lib/analysis/type_checker/type_checker.dart.md`
      and the related cluster) collapse to ONE
      `using <RootNs>.Analysis.TypeChecker;`. The two `compiler/<file>.dart`
      imports (lexer + parser) collapse to ONE
      `using <RootNs>.Compiler;` per the SUT-side specs at
      `.codeconv/conversion-specs/lib/compiler/{lexer,parser}.dart.md`.
      REUSE existing idiom verbatim (FR-012 / SC-007); no new research.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (KB cache hit per FR-012 / SC-007 —
      REUSE from precedent
      `test/test_channel_construction.dart.md` and
      `test/analysis/type_checker/well_typed_clause_test.dart.md`): Dart
      `package:` URIs are pubspec-anchored file-level references; C#
      `using` names a namespace, not a file — so N same-directory Dart
      imports collapse to 1 C# `using`. No `as` alias / partial import
      is used in this file, so the simple `using <Ns>;` form suffices.
      MISSING-SUT-FILE nuance (explicitly addressed, NOT escalated): the
      import `'package:glp_runtime/analysis/type_checker/type_parser.dart'`
      currently has NO corresponding source file in the converted
      inventory (`glp_runtime_net/lib/analysis/type_checker/` does not
      contain `type_parser.dart`, and no peer SUT spec exists at
      `.codeconv/conversion-specs/lib/analysis/type_checker/type_parser.dart.md`).
      The Dart-side symbol `parseTypes(String)` (used once in
      `checkTypes`, see method-call construct below) is defined in that
      missing file. The spec records the CALL-SITE shape only (the import
      collapses into the same `<RootNs>.Analysis.TypeChecker` namespace
      `using`, on the assumption that when the SUT file IS converted it
      will land in that namespace alongside the rest of the
      type-checker cluster); the discovery of the missing SUT file is a
      depgraph/inventory concern, not a per-file convspec escalation.
      Codegen MUST NOT emit `DebugNegative.cs` until the SUT-side
      `type_parser.dart` (or its post-rename successor) has its own
      convspec artifact recording the namespace + `ParseTypes(string)`
      static-method shape — recorded as a downstream gate, not a
      blocking escalation here. Project-file (assembly-reference)
      emission remains OUT OF SCOPE for this single-file artifact (a
      langpair-level concern).
  - construct_key: dart.top_level_function_helper_with_return_type
    source_form: >-
      "TypeCheckResult checkTypes(String source) {
         final lines = source.split('\n');
         final clauseLines = <String>[];
         for (final line in lines) {
           final trimmed = line.trim();
           if (trimmed.contains('::=') || trimmed.startsWith('procedure ')) continue;
           if (trimmed.isNotEmpty && !trimmed.startsWith('%')) clauseLines.add(line);
         }
         final clauseSource = clauseLines.join('\n');
         final lexer = Lexer(clauseSource);
         final tokens = lexer.tokenize();
         final parser = Parser(tokens);
         final program = parser.parse();
         final clauses = program.procedures.expand((p) => p.clauses).toList();
         final typeEnv = parseTypes(source);
         final checker = TypeChecker(typeEnv);
         return checker.check(clauses);
       }"
    target_decision: >-
      Dart top-level function `TypeCheckResult checkTypes(String source)`
      maps to a C# `private static TypeCheckResult CheckTypes(string
      source)` method on the file's single host class `DebugNegative`
      (see void_main construct below). The method is `private static`
      (not `public`) because nothing outside the converted .cs file
      calls it — it is a file-local helper called only from `Main`.
      The Dart `final` locals (`lines`, `clauseLines`, `trimmed`,
      `clauseSource`, `lexer`, `tokens`, `parser`, `program`, `clauses`,
      `typeEnv`, `checker`) map to C# `var` locals per the cached
      `rf-dart-final-local-to-csharp-var-local` idiom recorded in
      `.codeconv/conversion-specs/test/analysis/type_checker/well_typed_clause_test.dart.md`.
      The Dart `<String>[]` empty typed list literal maps to C# `new
      List<string>()` (or C# 12 collection-expression `[]` if NRT/lang
      version supports it) per the cached
      `rf-dart-list-literal-to-csharp-list-or-collection-expression`
      idiom. The Dart `String.split('\n')` returns `List<String>`; the
      C# `string.Split('\n')` returns `string[]` — the iteration `for
      (final line in lines)` works identically over either shape via
      C# `foreach (var line in lines)`. The Dart `String.trim()`,
      `String.contains(String)`, `String.startsWith(String)`,
      `String.isNotEmpty` getter, `Iterable.join(String)`, and
      `Iterable<T>.expand((T) => Iterable<U>)` -> `List<U>` map per
      construct dart.string_and_iterable_member_calls below. The Dart
      default-constructor calls `Lexer(clauseSource)`, `Parser(tokens)`,
      `TypeChecker(typeEnv)` map to C# `new Lexer(clauseSource)`,
      `new Parser(tokens)`, `new TypeChecker(typeEnv)` per the cached
      `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`
      idiom. The Dart instance-method calls `lexer.tokenize()`,
      `parser.parse()`, `checker.check(clauses)` map to PascalCased
      `lexer.Tokenize()`, `parser.Parse()`, `checker.Check(clauses)`
      per the cached
      `rf-dart-instance-method-camelcase-to-csharp-pascalcase` idiom.
      The Dart top-level function call `parseTypes(source)` maps to
      `TypeParser.ParseTypes(source)` (host static class name DEFERRED
      to the missing SUT spec — see import-construct nuance) per the
      cached
      `rf-dart-top-level-function-to-csharp-static-class-method`
      idiom.
    idiom_id: rf-dart-top-level-function-to-csharp-static-class-method
    research_finding_id: rf-dart-top-level-function-to-csharp-static-class-method
    nuance: >-
      Top-level-function nuance (explicitly addressed, KB cache hit per
      FR-012 / SC-007 — REUSE from
      `test/analysis/type_checker/well_typed_clause_test.dart.md`):
      Dart permits top-level functions; C# requires every method to
      belong to a type. Because this file has BOTH a top-level helper
      `checkTypes` AND a top-level `main`, the file is converted to a
      single `public static class DebugNegative` holding both as
      `private static` members. `private static` is safe (no `this`
      capture). String-iteration nuance: Dart `for (final line in
      lines)` is a for-each over `List<String>`; C# `foreach (var line
      in lines)` is the direct equivalent — both iterate by reference
      to the underlying collection without copying. The `continue`
      statement inside the loop translates 1:1. Control-flow nuance:
      both Dart `&&` and `||` short-circuit identically to C# `&&` /
      `||`. The compound predicate
      `trimmed.contains('::=') || trimmed.startsWith('procedure ')`
      preserves left-to-right evaluation order in both targets. The
      Dart `String.isNotEmpty` GETTER (no parentheses) maps to a C#
      property: `string` has no built-in `IsNotEmpty` property — the
      conversion MUST emit `trimmed.Length > 0` OR equivalently
      `!string.IsNullOrEmpty(trimmed)` (which is documented as the
      idiomatic test on Microsoft Learn `string.IsNullOrEmpty(string)`).
      Codegen MUST pick `trimmed.Length > 0` to preserve the Dart
      `isNotEmpty` semantics exactly (Dart `isNotEmpty` is true iff
      length > 0; it does NOT treat null specially because Dart's NNBD
      makes `String` non-nullable here).
  - construct_key: dart.string_and_iterable_member_calls
    source_form: >-
      "source.split('\n');
       line.trim();
       trimmed.contains('::=');
       trimmed.startsWith('procedure ');
       trimmed.isNotEmpty;
       trimmed.startsWith('%');
       clauseLines.add(line);
       clauseLines.join('\n');
       program.procedures.expand((p) => p.clauses).toList();
       result.errors.isNotEmpty;"
    target_decision: >-
      Per-member mapping (each is a documented 1:1 idiom with the
      Dart-`dart:core` -> .NET `System.String` / `System.Collections.
      Generic.List<T>` / `System.Linq` correspondence). REUSE existing
      idiom rows; no new research.
      - `String.split(String)` -> `string.Split(string)` returning
        `string[]` (Microsoft Learn
        `https://learn.microsoft.com/dotnet/api/system.string.split`).
        Single-char form `string.Split('\n')` is also valid; codegen
        SHOULD prefer the single-char form because the Dart argument
        is a one-char string literal.
      - `String.trim()` -> `string.Trim()` (Microsoft Learn
        `https://learn.microsoft.com/dotnet/api/system.string.trim`).
        Both trim ALL Unicode whitespace by default.
      - `String.contains(String)` -> `string.Contains(string)`
        (Microsoft Learn
        `https://learn.microsoft.com/dotnet/api/system.string.contains`).
        Default ordinal comparison in both; case-sensitive in both.
      - `String.startsWith(String)` -> `string.StartsWith(string)`
        (Microsoft Learn
        `https://learn.microsoft.com/dotnet/api/system.string.startswith`).
        Default ordinal+case-sensitive in both.
      - `String.isNotEmpty` (getter) -> `<s>.Length > 0` (no direct
        property; see top_level_function_helper nuance above).
      - `List<T>.add(T)` -> `List<T>.Add(T)` (Microsoft Learn
        `https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.add`).
      - `Iterable<String>.join(String)` -> `string.Join(string,
        IEnumerable<string>)` STATIC method (Microsoft Learn
        `https://learn.microsoft.com/dotnet/api/system.string.join`).
        Argument-order swap: Dart `clauseLines.join('\n')` becomes
        `string.Join("\n", clauseLines)` — the COLLECTION and the
        SEPARATOR swap positions across the conversion.
      - `Iterable<T>.expand((T) => Iterable<U>).toList()` ->
        `IEnumerable<T>.SelectMany(Func<T, IEnumerable<U>>).ToList()`
        per LINQ (Microsoft Learn
        `https://learn.microsoft.com/dotnet/api/system.linq.enumerable.selectmany`
        and `.tolist`). Requires `using System.Linq;` at file scope.
      - `List<T>.isNotEmpty` (getter on `errors`) -> `<list>.Count > 0`
        (Dart `List` has `isNotEmpty`; C# `List<T>` does not — same
        rule as on `String`).
    idiom_id: rf-dart-string-and-iterable-members-to-dotnet
    research_finding_id: rf-dart-string-and-iterable-members-to-dotnet
    nuance: >-
      Argument-order-swap footgun (explicitly addressed): Dart
      `iterable.join(separator)` has the separator as the SOLE
      argument on the iterable; C# `string.Join(separator, iterable)`
      is a STATIC method with separator FIRST then iterable. Codegen
      MUST swap, identical to the well-known `Assert.Equal(expected,
      actual)` argument-order swap recorded in `test/smoke_test.dart.md`.
      Getter-vs-property nuance: Dart `isNotEmpty` is a documented
      getter on `String`, `List`, `Iterable`, `Map`, and `Set`; C#
      has NO equivalent property — `Length > 0` (for `string`,
      `array`, `Span`) or `Count > 0` (for `List<T>`,
      `ICollection<T>`) is idiomatic and produces identical results
      (Dart NNBD makes the receiver non-nullable so null-handling is
      not a concern). Empty-string nuance: both `''.isNotEmpty` and
      `"".Length > 0` evaluate to `false` — semantically equivalent.
      Iteration-allocation nuance: the LINQ `SelectMany(...).ToList()`
      chain allocates one intermediate enumerator and one final
      `List<U>`; the Dart `expand(...).toList()` chain allocates one
      `Iterable<U>` view and one final `List<U>` — both are
      single-pass with one terminal materialisation, observably
      identical complexity. LINQ-namespace nuance: codegen MUST
      include `using System.Linq;` at the file scope (cu-1 below);
      omitting it would render `SelectMany`/`ToList` unresolved at
      compile time.
  - construct_key: dart.constructor_call_implicit_new
    source_form: >-
      "Lexer(clauseSource);
       Parser(tokens);
       TypeChecker(typeEnv);"
    target_decision: >-
      Map each Dart default-constructor call to a C# `new <Type>(...)`
      call with identical positional-argument shape:
      `new Lexer(clauseSource)`, `new Parser(tokens)`,
      `new TypeChecker(typeEnv)`. The SUT-side specs at
      `.codeconv/conversion-specs/lib/compiler/lexer.dart.md`,
      `.../parser.dart.md`, and
      `.codeconv/conversion-specs/lib/analysis/type_checker/type_checker.dart.md`
      decide the C# constructor signatures; this artifact records only
      the call-site shape (the `new` prefix and positional ordering
      are preserved).
    idiom_id: rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new
    research_finding_id: rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new
    nuance: >-
      Implicit-new nuance (explicitly addressed, KB cache hit per
      FR-012 / SC-007 — REUSE from
      `test/analysis/type_checker/well_typed_clause_test.dart.md`):
      Dart 2+ allows omitting the `new` keyword at constructor call
      sites; C# requires `new` (target-typed `new()` is an
      alternative when the target type is known from context —
      Microsoft Learn "Target-typed new expressions" — but is
      OPTIONAL here, classic `new T(...)` is always correct).
  - construct_key: dart.test_file.void_main_as_dart_run_entrypoint
    source_form: >-
      "void main() {
         print('=== NEGATIVE test: dl_append with WRONG modes ===');
         print('Clause: my_dl_append(A?\\\\B, B?\\\\C, A\\\\C?).');
         print('');
         var result = checkTypes('''...''');
         print('isWellTyped: ${result.isWellTyped}');
         ...
       }"
    target_decision: >-
      LOAD-BEARING DECISION (explicitly addressed): this file is NOT a
      `package:test` file — it has NO `import 'package:test/test.dart';`,
      NO `test(...)` calls, NO `expect(...)` calls, NO `group(...)`
      blocks, and NO matchers. It is a `dart run`-invoked diagnostic
      script whose `void main()` performs a single sequence of:
      print banner -> call `checkTypes` -> print result -> if-else
      branch on a boolean -> conditionally print errors -> repeat for
      the positive case. The xUnit conversion shape used by every
      OTHER `test/**.dart` file in this inventory (drop `main`, emit
      `[Fact]` methods) is NOT APPLICABLE here. The conversion target
      is a single static C# class with a `Main` entrypoint that
      preserves the diagnostic-script semantics:
      `public static class DebugNegative { public static int Main(
      string[] args) { ... } }` (or the file-scoped top-level
      statements form available in C# 9+, also valid). Each Dart
      top-level `print(...)` call maps to `Console.WriteLine(...)`
      (Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.console.writeline`),
      NOT to `ITestOutputHelper.WriteLine` — because this file has
      no test-class instance to inject the helper into. The Dart
      script is meant to be invoked via `dart run <file>`; the C#
      equivalent invocation is `dotnet run --project <test-project>
      -- --debug-negative` (or compile as a separate console exe).
      Per the convspec scope, the .csproj orchestration (whether to
      compile this file as a TEST exe, a SEPARATE diagnostic exe,
      or include it as a `[Fact(Skip = "manual diagnostic")]`
      no-op) is a LANGPAIR-level concern recorded in the
      conversion_units list but not asserted here.
    idiom_id: rf-dart-debug-script-main-to-csharp-static-main
    research_finding_id: rf-dart-debug-script-main-to-csharp-static-main
    nuance: >-
      Discovery-model nuance (explicitly addressed): Dart treats every
      `.dart` file with a `void main()` as a runnable program (`dart
      run <file>`); xUnit discovers tests by REFLECTION over `[Fact]`
      attributes — the two models are NOT interchangeable. For a
      diagnostic harness like this file, the C# canonical mapping is
      a `public static int Main(string[] args)` (or `void Main` —
      both are documented entrypoint signatures, Microsoft Learn
      `https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/main-command-line`).
      Return-type nuance: Dart `void main()` returns nothing; C#
      `Main` MAY return `void`, `int`, `Task`, or `Task<int>`. The
      conversion picks `int Main` and `return 0` at the end so the
      diagnostic exit code is explicit (and `1` could be returned if
      a future enhancement promotes a "BUG: Implementation accepted
      ill-typed program!" branch to a hard-failure signal — recorded
      as a latent enhancement, not asserted now). Side-effect
      ordering nuance: Dart and C# both guarantee top-to-bottom
      statement evaluation, so the print/check/print sequence
      preserves observable ordering verbatim. NO `package:test`
      attributes are emitted on the host class — adding `[Fact]`
      here would WRONGLY register a test method that prints to the
      runner log without asserting anything, polluting reports.
      Authoritative basis (Dart): `https://dart.dev/language#hello-world`
      ("Every app requires the top-level `main()` function, where
      execution starts."). Authoritative basis (C#): Microsoft Learn
      Main-method documentation cited above.
  - construct_key: dart.core.print
    source_form: >-
      "print('=== NEGATIVE test: dl_append with WRONG modes ===');
       print('Clause: my_dl_append(A?\\\\B, B?\\\\C, A\\\\C?).');
       print('');
       print('isWellTyped: ${result.isWellTyped}');
       print('Expected: false (should FAIL because modes are wrong)');
       print('');
       print('BUG: Implementation accepted ill-typed program!');
       print('CORRECT: Implementation rejected ill-typed program!');
       print('Errors:');
       print('  - $err');
       print('');
       print('=== POSITIVE test: dl_append with CORRECT modes ===');
       print('Clause: my_dl_append(A\\\\B?, B\\\\C?, A?\\\\C).');
       print('');
       print('isWellTyped: ${result.isWellTyped}');
       print('Expected: true (should PASS)');"
    target_decision: >-
      LOAD-BEARING DEVIATION from `test/test_channel_construction.dart.md`
      (explicitly addressed): there, Dart `print(...)` inside a
      `[Fact]` test maps to `ITestOutputHelper.WriteLine(...)` because
      xUnit captures per-test stdout. HERE the host is NOT a `[Fact]`
      (see void_main construct above) — it is a `static Main`
      entrypoint. The canonical target is therefore
      `Console.WriteLine(...)` (Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.console.writeline`),
      with the string-interpolation expression converted construct
      below. NO `ITestOutputHelper` injection, NO test-class
      constructor — codegen MUST NOT add an xUnit dependency for
      `Console.WriteLine` (it lives in `System` which is implicitly
      available; `using System;` is the only requirement).
      Argument-shape mapping: Dart `print(String)` is a single-string
      sink; C# `Console.WriteLine(string)` is the matching overload.
      `Console.WriteLine()` (no args) is the documented overload for
      `print('')` — emits an empty line.
    idiom_id: rf-dart-print-in-console-exe-to-console-writeline
    research_finding_id: rf-dart-print-in-console-exe-to-console-writeline
    nuance: >-
      Routing nuance (explicitly addressed, NEW row distinct from the
      `rf-dart-print-in-xunit-test-to-itestoutputhelper` row recorded
      in `test/test_channel_construction.dart.md`): the routing
      decision for `print(...)` depends on the HOST shape, not on the
      `print` call itself. In a `[Fact]` host, `ITestOutputHelper.
      WriteLine` is correct (per-test capture, no console bleed). In
      a `static Main` console-exe host, `Console.WriteLine` is
      correct (stdout goes to the process's stdout, no injection
      required). Both rows are FIRST-CLASS idioms and codegen
      selects between them based on the per-file host classification
      (host-shape lookup happens at the file-level, not per-call).
      Empty-line nuance: Dart `print('')` emits a single `\n` (the
      empty-string body plus print's trailing newline); C#
      `Console.WriteLine("")` emits `"" + Environment.NewLine` -
      identical observable result on a standard console. Encoding
      nuance: both Dart strings and C# strings are UTF-16 internally;
      `Console.WriteLine` defaults to the console's active code page
      on Windows (UTF-16 on modern terminals via
      `Console.OutputEncoding = Encoding.UTF8` if non-ASCII is
      involved — not the case in this file's print strings, which
      are pure ASCII).
  - construct_key: dart.string_interpolation
    source_form: >-
      "'isWellTyped: ${result.isWellTyped}';
       '  - $err';"
    target_decision: >-
      Map Dart string interpolation `'... ${expr}'` and `'... $var'`
      to C# interpolated-string literals `$"... {expr}"` (Microsoft
      Learn `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/interpolated`).
      Concrete mappings in this file:
      - `'isWellTyped: ${result.isWellTyped}'` ->
        `$"isWellTyped: {result.IsWellTyped}"` (with `isWellTyped`
        PascalCased to `IsWellTyped` per the SUT-side spec at
        `.codeconv/conversion-specs/lib/analysis/type_checker/type_checker.dart.md`
        which decides the result-type properties).
      - `'  - $err'` -> `$"  - {err}"` (no expression accessor; the
        `$err` short form is the loop variable from `for (final err
        in result.errors)`, which maps to `foreach (var err in
        result.Errors)`).
    idiom_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      Syntax-prefix nuance (explicitly addressed): Dart prefixes the
      literal with `'` (or `"`); C# REQUIRES the `$` prefix BEFORE
      the literal opener (`$"..."`). Dart's `${expr}` becomes C#'s
      `{expr}` (curly braces only, no `$` inside the braces); Dart's
      shorthand `$name` (only valid for a bare identifier) becomes
      C#'s full-braced `{name}` (C# has no shorthand). Brace-escape
      nuance: a literal `{` or `}` in a C# interpolated string MUST
      be doubled (`{{` / `}}`); no such literal braces appear in
      this file's interpolations. ToString-conversion nuance: both
      Dart and C# interpolate via the embedded expression's
      `toString` (Dart) / `ToString()` (C#) method — observably
      equivalent for built-in types and for SUT types that override
      `toString()`/`ToString()` consistently. The SUT-side spec for
      `TypeCheckResult` (in `type_checker.dart.md`) decides whether
      `isWellTyped` is a property or a method; the call-site shape
      assumes a PROPERTY (no parentheses) because Dart's
      `result.isWellTyped` has no parentheses. Codegen MUST preserve
      that shape.
  - construct_key: dart.if_else_statement
    source_form: >-
      "if (result.isWellTyped) {
         print('BUG: Implementation accepted ill-typed program!');
       } else {
         print('CORRECT: Implementation rejected ill-typed program!');
       }
       if (result.errors.isNotEmpty) {
         print('Errors:');
         for (final err in result.errors) {
           print('  - $err');
         }
       }
       if (!result.isWellTyped) {
         print('Errors:');
         for (final err in result.errors) {
           print('  - $err');
         }
       }"
    target_decision: >-
      Dart `if (cond) { ... } else { ... }` maps 1:1 to C# `if (cond)
      { ... } else { ... }`. The braces, condition syntax, and
      negation operator `!` are syntactically identical. The boolean
      condition `result.isWellTyped` maps to `result.IsWellTyped`
      (property PascalCasing per SUT spec); `result.errors.isNotEmpty`
      maps to `result.Errors.Count > 0` (getter-to-property
      conversion per the string_and_iterable_member_calls construct
      above); `!result.isWellTyped` maps to `!result.IsWellTyped`.
    idiom_id: rf-dart-if-else-to-csharp-if-else
    research_finding_id: rf-dart-if-else-to-csharp-if-else
    nuance: >-
      Block-required nuance (explicitly addressed): both Dart and C#
      allow a single-statement body without braces, but this file
      uses braces consistently; codegen preserves the braces.
      Short-circuit nuance: Dart `!` and C# `!` are the same logical
      negation operator on `bool`; no fall-through to `==`/`!=`
      comparison.
  - construct_key: dart.for_in_loop_over_list
    source_form: >-
      "for (final err in result.errors) {
         print('  - $err');
       }"
    target_decision: >-
      Dart `for (final <T> in <Iterable<T>>) { ... }` maps to C#
      `foreach (var <T> in <IEnumerable<T>>) { ... }` (Microsoft
      Learn `https://learn.microsoft.com/dotnet/csharp/language-reference/statements/iteration-statements#the-foreach-statement`).
      The SUT-side decision for `TypeCheckResult.errors` (in
      `type_checker.dart.md`) determines the element type (likely
      `WellTypedError`-shaped); the loop variable `err` then has
      that type implicitly via `var`.
    idiom_id: rf-dart-for-in-to-csharp-foreach
    research_finding_id: rf-dart-for-in-to-csharp-foreach
    nuance: >-
      `final` vs `var` nuance (explicitly addressed): Dart `final
      err` declares a fresh, immutable per-iteration binding; C# the
      `foreach` variable is also a fresh per-iteration binding and
      cannot be reassigned within the loop body (in C# 5+ — Microsoft
      Learn "foreach statement" documents this scoping change). The
      two are observably equivalent. The Dart `for (var x in xs)`
      form would also map to C# `foreach (var x in xs)`; this file
      uses `final` exclusively, which the conversion records but
      does not enforce on the C# side because mutating a `foreach`
      variable is forbidden anyway.
  - construct_key: dart.local_var_with_reassignment
    source_form: >-
      "var result = checkTypes('''...''');
       ...
       result = checkTypes('''...''');"
    target_decision: >-
      Dart `var result = checkTypes(...)` declares a mutable local
      with inferred type `TypeCheckResult`; the later `result =
      checkTypes(...)` reassigns the same local. Maps 1:1 to C#
      `var result = CheckTypes(...);` followed by `result =
      CheckTypes(...);` — C# `var` is mutable by default (the C#
      counterpart of Dart `final` would be `readonly` for fields,
      but for locals there is no first-class `readonly` keyword —
      C# 7.2+ `in` is parameter-only). The local's inferred type
      `TypeCheckResult` is the same in both targets.
    idiom_id: rf-dart-var-mutable-local-to-csharp-var-local
    research_finding_id: rf-dart-var-mutable-local-to-csharp-var-local
    nuance: >-
      Mutability nuance (explicitly addressed): Dart `final` ->
      immutable local, Dart `var` -> mutable local (this file uses
      `var` for `result` BECAUSE it is reassigned). C# `var` ->
      mutable local by default. The cached `rf-dart-final-local-to-
      csharp-var-local` idiom covers the IMMUTABLE case (Dart
      `final` -> C# `var` because C# has no first-class `let`
      keyword); the present idiom covers the MUTABLE case where
      Dart explicitly uses `var` -> C# `var`. Both Dart cases land
      on C# `var`; the mutability annotation is lost in the
      conversion (a known asymmetry, recorded for future review if
      C# `readonly`-local syntax is added in a future language
      version).
  - construct_key: dart.triple_quoted_raw_clause_source_literal
    source_form: >-
      "checkTypes('''
         MyList ::= [_ | MyList] ; [].
         MyDiffList ::= MyList \\ MyList?.
         procedure my_dl_append(MyDiffList?, MyDiffList?, MyDiffList).
         my_dl_append(A?\\B, B?\\C, A\\C?).
       ''');
       checkTypes('''
         MyList ::= [_ | MyList] ; [].
         MyDiffList ::= MyList \\ MyList?.
         procedure my_dl_append(MyDiffList?, MyDiffList?, MyDiffList).
         my_dl_append(A\\B?, B\\C?, A?\\C).
       ''');"
    target_decision: >-
      Dart triple-quoted string literal `'''...'''` is a multi-line
      string with `\\` as a literal-backslash escape (single `\\` in
      source produces one backslash in the runtime value). Map to a
      C# VERBATIM string literal `@"..."` (Microsoft Learn
      `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/verbatim`)
      — C# `@"..."` treats `\\` as a single literal backslash AND
      preserves embedded newlines, matching Dart triple-quoted
      semantics for the payload in this file (the GLP-grammar
      strings contain `\\` and embedded newlines). The two Dart
      strings become:
      `@"
         MyList ::= [_ | MyList] ; [].
         MyDiffList ::= MyList \ MyList?.
         procedure my_dl_append(MyDiffList?, MyDiffList?, MyDiffList).
         my_dl_append(A?\B, B?\C, A\C?).
       "` and analogously for the positive-case clause source.
      C# 11+ raw string literals (`"""..."""`) are an authoritative
      alternative (Microsoft Learn `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`)
      that more faithfully preserves the triple-quoted shape; codegen
      MAY emit either form. For broad compatibility (C# 9/10 targets)
      `@"..."` is recorded as the default.
    idiom_id: rf-dart-triple-quoted-string-to-csharp-verbatim-or-raw-string
    research_finding_id: rf-dart-triple-quoted-string-to-csharp-verbatim-or-raw-string
    nuance: >-
      Escape-semantics nuance (explicitly addressed): Dart triple-
      quoted strings DO process `\\` as backslash (unlike Dart RAW
      strings prefixed `r'''...'''` which do not process any
      escapes). C# verbatim strings `@"..."` do NOT process most
      escapes (a single `\` is a single backslash) but DO require
      `""` to embed a single `"`. The Dart source contains `\\B`
      (intended runtime value: `\B`) — codegen MUST emit `\B` (one
      backslash) in the C# verbatim literal, NOT `\\B` (which would
      be TWO backslashes). The conversion thus REDUCES the escape
      count by one across the syntactic boundary, while PRESERVING
      the runtime string value. Newline-preservation nuance: both
      forms (Dart `'''` and C# `@"..."` / `"""..."""`) preserve
      embedded `\n` literally; codegen MUST keep the line breaks
      verbatim so the lexer downstream sees the same input. Raw-
      string-alternative nuance (recorded for future review): C# 11
      raw strings `"""..."""` would more faithfully mirror Dart's
      triple-quoted form syntactically (NO escape processing inside
      the body unless the body contains backticks of the closing
      delimiter); a future post-C#-11 codegen profile MAY switch to
      `"""..."""` per this idiom row.
conversion_units:
  - "cu-1: file-scope using directives (System; System.Collections.Generic; System.Linq; <RootNs>.Analysis.TypeChecker; <RootNs>.Compiler) — NO using Xunit (this file is NOT a [Fact] file)"
  - "cu-2: namespace declaration mirroring test/ (e.g. <RootNs>.Test) — single top-level namespace"
  - "cu-3: host class `public static class DebugNegative` (PascalCased from `debug_negative.dart`) — NOT a test class, NO public test-method visibility"
  - "cu-4: `private static TypeCheckResult CheckTypes(string source)` helper hoisted from the Dart top-level `checkTypes` function — body translated statement-for-statement using cached idiom rows (final->var, implicit-new->new, camelCase->PascalCase, isNotEmpty->Length>0/Count>0, String.split/trim/contains/startsWith/join, Iterable.expand->LINQ SelectMany, Iterable.toList->ToList)"
  - "cu-5: `public static int Main(string[] args)` entrypoint hoisted from the Dart top-level `void main` — body translated statement-for-statement: Console.WriteLine for each Dart print, var result = CheckTypes(verbatim-string) for each Dart var result = checkTypes(triple-quoted-string), if/else and foreach blocks preserved 1:1"
  - "cu-6: two C# VERBATIM string literals (@\"...\" or C# 11+ raw \"\"\"...\"\"\") holding the negative-case and positive-case GLP grammar clauses — escape count reduced by one (Dart \\\\ -> C# \\) per the triple-quoted-string idiom"
  - "cu-7: NO xUnit attributes, NO [Fact], NO [Trait], NO DisplayName — this file is a console-exe diagnostic harness, NOT a test fixture (see void_main construct rationale)"
  - "cu-8: DOWNSTREAM GATE (recorded, not asserted by this artifact): codegen MUST NOT emit DebugNegative.cs until the SUT-side type_parser.dart convspec exists (currently missing from inventory) — see import-construct nuance for details"
escalations: []
```

## Rationale + research provenance

### Why this file is NOT an xUnit `[Fact]` conversion

Every other `test/**.dart` file specced so far in this conversion
(smoke_test, test_channel_construction, glp_runtime_test, the
multiagent/, conformance/, heap/, module/, compiler/, bytecode/, and
analysis/type_checker/ peers) imports `package:test/test.dart` and
calls `test(...)` / `expect(...)`. This file imports NEITHER — it
imports only the SUT files (`type_checker.dart`, `type_parser.dart`,
`lexer.dart`, `parser.dart`) and calls `print(...)` for its output.
There is no `test()` registration, no `expect()` assertion, no
matcher, no `group()`, no `setUp`/`tearDown`. The host shape on the
Dart side is a `dart run <file>` diagnostic script — invoked
manually by a developer to print a negative/positive type-checking
trace, NOT discovered by `dart test`.

The xUnit-conversion idiom recorded across the peer specs
(`rf-dart-package-test-to-dotnet-xunit`, `rf-dart-test-main-to-xunit-
class-with-facts`) is therefore INAPPLICABLE here — applying it
would force this file into a `[Fact]`-attributed method whose body
prints to a runner log without asserting anything, polluting test
reports and miscategorising the file. The correct counterpart on
the .NET side is a `static Main` console-exe entrypoint (Microsoft
Learn "Main method in C# programs",
`https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/main-command-line`),
in a `public static class DebugNegative` host. This is recorded as
a NEW idiom row `rf-dart-debug-script-main-to-csharp-static-main`
that codegen selects PER FILE based on host-shape classification
(presence of `package:test` import + `test(...)` calls).

### Why `Console.WriteLine`, not `ITestOutputHelper.WriteLine`

`test/test_channel_construction.dart.md` routes Dart `print(...)`
to xUnit's `ITestOutputHelper.WriteLine` because the host there is
a `[Fact]` method on a test class that takes the helper through
constructor injection. HERE the host is a `static Main` — there is
no test-class instance, no constructor injection, no per-test
capture model. The correct .NET sink is `Console.WriteLine`
(Microsoft Learn `https://learn.microsoft.com/dotnet/api/system.console.writeline`),
which writes to the process's stdout stream. Both rows are
first-class entries in the KB; the host-shape lookup decides which
row applies per file. This is the SAME routing-by-host-shape pattern
that the test-framework idiom uses (xUnit framework choice depends
on the file BEING a `package:test` file).

### Why the Dart triple-quoted strings map to C# verbatim (or raw) strings

Dart `'''...'''` is a multi-line string literal that DOES process
`\\` as backslash (unlike Dart's raw form `r'''...'''`). The Dart
literal `'my_dl_append(A?\\B, ...)'` has the runtime value
`my_dl_append(A?\B, ...)` (single backslash). The C# counterpart
that preserves multi-line layout AND escape-free `\` handling is
the verbatim string `@"..."` (Microsoft Learn
`https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/verbatim`),
which treats a single `\` as a literal backslash and preserves
embedded newlines. The escape count therefore REDUCES by one
across the conversion (Dart `\\B` -> C# `\B`), but the runtime
string value is byte-identical. C# 11+ raw strings `"""..."""`
(Microsoft Learn `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`)
are an authoritative alternative recorded in the idiom row;
codegen targeting C# 11+ MAY prefer them.

### Why `isNotEmpty` maps to `Length > 0` / `Count > 0`

Dart's `String.isNotEmpty`, `Iterable<T>.isNotEmpty`, and
`List<T>.isNotEmpty` are documented getters returning `bool`
(`https://api.dart.dev/stable/dart-core/String/isNotEmpty.html`
and analogous pages for `Iterable`/`List`). C# has NO equivalent
property on `string`, `IEnumerable<T>`, or `List<T>` — the
idiomatic check is `<s>.Length > 0` for strings/arrays/`Span<T>`
and `<list>.Count > 0` for `ICollection<T>`/`List<T>`. Microsoft
Learn `string.IsNullOrEmpty(string)` is documented as the
null-tolerant alternative but is semantically wider than Dart's
`isNotEmpty` (which assumes a non-null receiver under NNBD).
Codegen MUST pick `Length > 0` / `Count > 0` to match Dart
semantics exactly. The conversion creates ONE idiom row covering
both shapes because they are the same conceptual mapping with
different host-type-specific property names.

### Missing-SUT-file recordings (not an escalation, a downstream gate)

The Dart import `'package:glp_runtime/analysis/type_checker/type_parser.dart'`
references a file that is not present in the converted-inventory
tree (`glp_runtime_net/lib/analysis/type_checker/` lacks
`type_parser.dart`, and no convspec artifact exists for it). The
symbol `parseTypes(String)` used in `checkTypes` originates there.
This artifact records the situation as a DOWNSTREAM GATE in
cu-8 — codegen MUST NOT emit `DebugNegative.cs` until the SUT
side is convspec-complete — but does NOT escalate, because the
PER-CONSTRUCT decision (collapse the missing-file import into the
same `<RootNs>.Analysis.TypeChecker` `using`, call
`TypeParser.ParseTypes(source)` PascalCased) is fully determined
by the existing idioms. The inventory gap is a depgraph/discover
concern, not a convspec one. `escalations: []` is therefore
intentional: every per-construct decision is grounded in
authoritative documentation on both sides.

### Why no other escalations

Every construct has a single-decision target shape grounded in
official Dart and .NET / Microsoft Learn documentation. The two
"new" idioms introduced here
(`rf-dart-debug-script-main-to-csharp-static-main` and
`rf-dart-print-in-console-exe-to-console-writeline`) are
authoritative on both sides (Dart language tour for `main` +
Microsoft Learn for the C# `Main` entrypoint shape; Dart
`print` API + Microsoft Learn `Console.WriteLine`). The
remaining constructs reuse cached idioms verbatim per FR-012 /
SC-007 from
`test/analysis/type_checker/well_typed_clause_test.dart.md`,
`test/test_channel_construction.dart.md`, and
`test/smoke_test.dart.md`. The well-known nuances (value-vs-
reference, async/`Future` -> `Task`, null-safety) are addressed
where they apply (string-iteration is value-type-stable; this
file has no async surface; NNBD makes `isNotEmpty` semantics
crisp on the C# side).
