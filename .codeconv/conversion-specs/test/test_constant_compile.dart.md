# Conversion Spec — test/test_constant_compile.dart

> Conversion-spec artifact for test/test_constant_compile.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> A tiny (12-line) standalone `dart run`-style diagnostic script that
> constructs a `GlpCompiler`, compiles the single GLP source string
> `'test_nil([]).'` into a `BytecodeProgram`, and prints (via Dart
> `print(...)`) every op in `result.ops` together with its index for
> human inspection. **Crucially, this file is NOT a `package:test`
> test** — it imports ONLY the SUT facade (`package:glp_runtime/
> compiler/compiler.dart`), has NO `import 'package:test/test.dart';`,
> NO `test(...)` calls, NO `expect(...)`, NO `group(...)`, NO matchers,
> NO `[Fact]`-eligible registration. The host shape is therefore the
> SAME shape recorded by `.codeconv/conversion-specs/test/
> debug_negative.dart.md`: a `void main()` console-exe diagnostic
> harness. The xUnit conversion idiom used by every OTHER
> `test/**.dart` file in the inventory (drop `main`, emit `[Fact]`
> methods, route `print` to `ITestOutputHelper.WriteLine`) is
> **INAPPLICABLE** here — applying it would force the file into a
> `[Fact]` whose body prints to the runner log without asserting
> anything, polluting test reports and miscategorising the file. The
> correct .NET target is a `public static class TestConstantCompile`
> with a `public static int Main(string[] args)` entrypoint and
> `Console.WriteLine` as the print sink. Every per-construct decision
> below REUSES idioms recorded by the prior batch (notably
> `.codeconv/conversion-specs/test/debug_negative.dart.md` for the
> debug-script host shape and Console.WriteLine routing; the lib
> specs `.codeconv/conversion-specs/lib/compiler/compiler.dart.md`
> for the `GlpCompiler` facade + `BytecodeProgram` shape;
> `.codeconv/conversion-specs/test/bytecode/inspect_bytecode_test.dart.md`
> for the C-style-for-loop + string-interpolation + list-indexer
> rows even though THAT file is xUnit-hosted). No escalations.

```yaml
schema_version: 1
source_path: test/test_constant_compile.dart
source_sha256: 4bcbea2e88df85d7e670dc297e7fa64a3241e200f07da360f84f36715d19aca6
target_code_unit: test/TestConstantCompile.cs
constructs:
  - construct_key: dart.internal_package_import.same_package_single
    source_form: "import 'package:glp_runtime/compiler/compiler.dart';"
    target_decision: >-
      Drop the Dart `import 'package:glp_runtime/compiler/compiler.dart';`
      directive and emit ONE file-level C# `using <RootNs>.Compiler;`
      directive. The converted `GlpCompiler` facade and the
      `BytecodeProgram` (returned by `GlpCompiler.compile(String)`,
      whose `ops` list this file iterates) BOTH live in the
      `<RootNs>.Compiler` sub-namespace per the lib spec
      `lib/compiler/compiler.dart.md` (which folds `compiler.dart`,
      `codegen.dart`, `result.dart`, etc. into one C# namespace and
      re-exports `BytecodeProgram` via the same compiler namespace).
      No `using <RootNs>.Bytecode;` is required at THIS file's scope
      because the only `BytecodeProgram` member accessed here
      (`result.ops` plus `result.ops.length` and `result.ops[i]`) is
      reachable via the compiler-namespace re-export — the lib spec
      decides whether to expose the type via re-export or direct
      using; codegen consults the SUT-side decision at emit time.
      Per FR-012 / SC-007 this construct is a KB cache hit — REUSE
      the `rf-dart-internal-package-import-to-csharp-using` row
      settled by `test/bytecode/inspect_bytecode_test.dart.md`,
      `test/compiler/reserved_constant_test.dart.md`,
      `test/compiler/partial_evaluator_test.dart.md`, and every
      prior internal-package-import construct in the batch; do NOT
      re-research. The test-project `.csproj` must reference the
      converted-SUT assembly — langpair-level concern, OUT OF SCOPE
      for this per-file artifact.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (KB cache hit per FR-012 / SC-007 —
      REUSE from `test/debug_negative.dart.md`, `test/bytecode/
      inspect_bytecode_test.dart.md`, `test/compiler/
      reserved_constant_test.dart.md`): Dart `package:` URIs are
      pubspec-anchored file-level references; C# `using` names a
      namespace, not a file — so a Dart import that brings ONE symbol
      (`GlpCompiler`) into scope maps to ONE C# `using
      <RootNs>.Compiler;` directive. `GlpCompiler` is library-public
      on the Dart side (no leading underscore) so it maps to `public`
      C# per the SUT spec — no accessibility relaxation needed. No
      `as` alias / `show` narrowing on the Dart side, so no C# alias
      or filter is needed. SYMBOL-VISIBILITY nuance for `result.ops`:
      `BytecodeProgram.ops` is a public `List<Op>`-shaped field/getter
      per `lib/compiler/codegen.dart.md` and the bytecode-runner lib
      spec — the C# side surfaces it as a `public IReadOnlyList<Op>`
      (or `public List<Op>`) property `Ops` (PascalCased) per the
      SUT-side conversion of `lib/compiler/result.dart` and
      `lib/bytecode/runner.dart`. THIS spec records only the
      call-site shape; the precise property type and casing are
      owned by the SUT specs. NO cross-isolate, cross-package,
      transitive-export, deferred-loading semantics apply. NO
      `using static` is needed — `GlpCompiler` is named qualified at
      its single call-site `GlpCompiler()` constructor invocation.

  - construct_key: dart.diag_script.void_main_no_package_test_no_assertions
    source_form: >-
      "void main() {
         final compiler = GlpCompiler();
         print('=== Testing: test_nil([]) ===');
         final result = compiler.compile('test_nil([]).');
         print('Bytecode:');
         for (int i = 0; i < result.ops.length; i++) {
           print('  $i: ${result.ops[i]}');
         }
       }"
    target_decision: >-
      LOAD-BEARING DECISION (explicitly addressed): this file is NOT a
      `package:test` file — it has NO `import 'package:test/test.dart';`,
      NO `test(...)` calls, NO `expect(...)` calls, NO `group(...)`
      blocks, NO matchers, NO `setUp`/`tearDown`. It is a `dart run`-
      invoked diagnostic script whose `void main()` performs a fixed
      sequence: construct `GlpCompiler` -> print banner -> compile a
      single hardcoded GLP source string -> print "Bytecode:" header
      -> iterate `result.ops` with a C-style for-loop, printing index
      and op. NO assertions, NO error checks — the script's value is
      the human-readable bytecode trace it emits. The xUnit conversion
      idiom used by every OTHER `test/**.dart` file in the inventory
      (`rf-dart-package-test-to-dotnet-xunit` + `rf-dart-test-main-to-
      xunit-class-with-facts`) is NOT APPLICABLE here — applying it
      would force this file into a `[Fact]`-attributed method whose
      body prints to a runner log without asserting anything,
      polluting test reports and miscategorising the file. The
      correct counterpart on the .NET side (REUSE from
      `test/debug_negative.dart.md`) is a single `public static class
      TestConstantCompile` host (filename-PascalCased, mirroring
      `debug_negative.dart` -> `DebugNegative`) with a `public static
      int Main(string[] args)` entrypoint that preserves the
      diagnostic-script semantics statement-for-statement. The
      file-scoped top-level-statements form (C# 9+) is an
      authoritative alternative for a single-file program (Microsoft
      Learn "Top-level statements"
      `https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements`)
      — codegen MAY choose either form per langpair preference;
      the spec records the classic `static Main` shape as the default
      to match the precedent set by `debug_negative.dart.md`. Each
      Dart `print(...)` call maps to `Console.WriteLine(...)` (NOT
      `ITestOutputHelper.WriteLine` — see `dart.core.print` construct
      below). The Dart `final compiler = GlpCompiler();` and `final
      result = compiler.compile(...);` locals map to C# `var
      compiler = new GlpCompiler();` and `var result = compiler.
      Compile(...);` via the cached
      `rf-dart-final-local-to-csharp-var-local`,
      `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`,
      and
      `rf-dart-instance-method-camelcase-to-csharp-pascalcase`
      idioms (see separate construct rows below for the locals,
      constructor call, and method call). The C-style for-loop
      preserves verbatim (see `dart.c_style_for_loop` construct).
      Per the convspec scope, the .csproj orchestration (whether to
      compile this file as the TEST exe's auxiliary entrypoint, a
      SEPARATE diagnostic exe, or include it as a `[Fact(Skip =
      "manual diagnostic")]` no-op) is a LANGPAIR-level concern
      recorded in conversion_units but not asserted here.
    idiom_id: rf-dart-debug-script-main-to-csharp-static-main
    research_finding_id: rf-dart-debug-script-main-to-csharp-static-main
    nuance: >-
      Discovery-model nuance (cached idiom from
      `test/debug_negative.dart.md`, REUSE verbatim): Dart treats
      every `.dart` file with a `void main()` as a runnable program
      (`dart run <file>`); xUnit discovers tests by REFLECTION over
      `[Fact]` attributes — the two models are NOT interchangeable.
      Host-shape classification rule: the presence/absence of the
      `package:test` import (and the presence/absence of `test(...)`
      / `group(...)` / `expect(...)` calls) determines which idiom
      applies per file. This file misses BOTH signals so it maps to
      the debug-script idiom, not the xUnit-test idiom. Return-type
      nuance: Dart `void main()` returns nothing; C# `Main` MAY return
      `void`, `int`, `Task`, or `Task<int>` (Microsoft Learn
      `https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/main-command-line`).
      The conversion picks `int Main` returning `0` at the end so the
      diagnostic exit code is explicit and consistent with
      `debug_negative.dart.md`. Side-effect-ordering nuance: Dart and
      C# both guarantee top-to-bottom statement evaluation, so the
      construct/print/compile/print/for-loop sequence preserves
      observable ordering verbatim. NO `package:test` attributes are
      emitted on the host class — adding `[Fact]` here would WRONGLY
      register a test method that prints without asserting,
      polluting reports. NO async / `Future` / `Stream` / isolate /
      `Completer` / `Timer` surface anywhere in this file — `Main`
      returns `int`, not `async Task<int>`. NO `dart:io` import, NO
      file-IO, NO prelude load, NO command-line arguments consumed
      (the `string[] args` parameter is required by the C# `Main`
      signature but is unused — same as `debug_negative.dart.md`).
      Authoritative basis (Dart): `https://dart.dev/language#hello-world`
      ("Every app requires the top-level `main()` function, where
      execution starts."). Authoritative basis (C#): Microsoft Learn
      Main-method documentation cited above. NEW facet (vs
      `debug_negative.dart.md`): NO local-variable mutability
      reassignment (`debug_negative.dart` reassigned `var result =
      ...`; THIS file uses `final` exclusively — see
      `dart.final_local_immutable` construct below).

  - construct_key: dart.final_local_immutable_with_implicit_new
    source_form: >-
      "final compiler = GlpCompiler();
       final result = compiler.compile('test_nil([]).');"
    target_decision: >-
      Two Dart `final` locals with no explicit type annotation map to
      two C# `var` locals with inferred types (REUSE the cached
      `rf-dart-final-local-to-csharp-var-local` idiom from
      `test/analysis/type_checker/well_typed_clause_test.dart.md`
      and `test/debug_negative.dart.md`). The first local
      `final compiler = GlpCompiler();` translates to `var compiler =
      new GlpCompiler();` — the `new` keyword is REQUIRED on the C#
      side per the cached
      `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`
      idiom (target-typed `new()` is an authoritative alternative
      when the target type is known from context but the simple `new
      GlpCompiler()` form is preferred here for symmetry with
      `debug_negative.dart.md` and the absence of explicit
      type-context). The second local `final result = compiler.
      compile('test_nil([]).');` translates to `var result =
      compiler.Compile("test_nil([]).");` — the camelCased Dart
      method `compile` PascalCases to C# `Compile` per the cached
      `rf-dart-instance-method-camelcase-to-csharp-pascalcase`
      idiom (used by `lib/compiler/compiler.dart.md` and
      `test/compiler/reserved_constant_test.dart.md`); the Dart
      single-quoted string `'test_nil([]).'` becomes a C#
      double-quoted string `"test_nil([])."` per the cached
      `rf-dart-single-quoted-string-to-csharp-double-quoted-string`
      idiom (C# strings use ONLY double quotes; single-quotes are
      for `char`). NO reassignment of either local elsewhere in the
      file — both are write-once, observably equivalent to Dart
      `final`. The inferred local types on the C# side are
      `GlpCompiler` and `BytecodeProgram` respectively (per the lib
      spec `lib/compiler/compiler.dart.md` which decides the return
      type of `GlpCompiler.Compile(string)`).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Mutability nuance (explicitly addressed, KB cache hit per
      FR-012 / SC-007 — REUSE from
      `test/debug_negative.dart.md`'s `rf-dart-final-local-to-csharp-
      var-local` row): Dart `final <local>` declares a write-once
      local (the variable binding is immutable; the OBJECT it
      references is NOT made deeply immutable). C# `var <local>`
      declares a mutable local by default — C# has NO first-class
      `let`/`readonly`-local keyword (C# `readonly` is field-only;
      `in` is parameter-only). The mutability annotation is LOST in
      the conversion (a known asymmetry, recorded for future review
      if a future C# language version adds `let`/`readonly`-local
      syntax). Observably equivalent here because neither local is
      reassigned elsewhere in `Main`. Implicit-new nuance: Dart 2+
      allows omitting the `new` keyword at constructor call sites
      (Dart language tour "The `new` keyword is optional"); C#
      REQUIRES `new` for constructor invocations — codegen MUST
      emit `new` (Microsoft Learn `https://learn.microsoft.com/
      dotnet/csharp/language-reference/operators/new-operator`).
      camelCase-to-PascalCase nuance: Dart instance methods/fields
      are camelCase; C# methods/properties are PascalCase per
      Microsoft's C# Identifier-Names guide
      (`https://learn.microsoft.com/dotnet/csharp/fundamentals/
      coding-style/identifier-names`). `compile` -> `Compile`;
      class name `GlpCompiler` already PascalCased (no change).
      String-literal quoting nuance: Dart single-quoted `'test_nil
      ([]).'` and Dart double-quoted `"test_nil([])."` are
      INTERCHANGEABLE in Dart (both are regular string literals);
      C# strings use ONLY double quotes (single-quotes are `char`
      literals). The conversion converts to `"test_nil([])."`
      verbatim; the embedded characters (parentheses, brackets,
      period) require no escaping in either language. Value-vs-
      reference nuance: `GlpCompiler` and `BytecodeProgram` are
      both reference types on both sides (class in Dart, class in
      C#) per the lib specs — no `record struct` / `record class`
      decision applies at the call site.

  - construct_key: dart.core.print
    source_form: >-
      "print('=== Testing: test_nil([]) ===');
       print('Bytecode:');
       print('  $i: ${result.ops[i]}');"
    target_decision: >-
      LOAD-BEARING DEVIATION from `test/bytecode/inspect_bytecode_test.
      dart.md` (explicitly addressed): there, Dart `print(...)` inside
      a `[Fact]` test maps to `ITestOutputHelper.WriteLine(...)`
      because xUnit captures per-test stdout via the
      `Xunit.Abstractions.ITestOutputHelper` constructor-injected
      sink. HERE the host is NOT a `[Fact]` (see
      `dart.diag_script.void_main_no_package_test_no_assertions`
      above) — it is a `static Main` entrypoint. The canonical target
      is therefore `Console.WriteLine(...)` per the cached
      `rf-dart-print-in-console-exe-to-console-writeline` idiom
      registered by `test/debug_negative.dart.md` (Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.console.writeline`).
      NO `ITestOutputHelper` injection, NO test-class constructor,
      NO `using Xunit.Abstractions;` — codegen MUST NOT add an xUnit
      dependency for `Console.WriteLine` (it lives in `System`
      which is reachable via `using System;`, the only file-scope
      requirement for the print sink). Argument-shape mapping: Dart
      `print(String)` is a single-string sink; C# `Console.WriteLine
      (string)` is the matching overload. Concrete mappings:
        - `print('=== Testing: test_nil([]) ===');`
            -> `Console.WriteLine("=== Testing: test_nil([]) ===");`
            (plain string literal, no interpolation)
        - `print('Bytecode:');`
            -> `Console.WriteLine("Bytecode:");`
            (plain string literal, no interpolation)
        - `print('  $i: ${result.ops[i]}');`
            -> `Console.WriteLine($"  {i}: {result.Ops[i]}");`
            (interpolated string — see
            `dart.string_interpolation_with_list_indexer` construct
            below for the interpolation conversion details).
    idiom_id: rf-dart-print-in-console-exe-to-console-writeline
    research_finding_id: rf-dart-print-in-console-exe-to-console-writeline
    nuance: >-
      Routing nuance (KB cache hit per FR-012 / SC-007 — REUSE from
      `test/debug_negative.dart.md`): the routing decision for
      `print(...)` depends on the HOST shape, not on the `print`
      call itself. In a `[Fact]` host, `ITestOutputHelper.WriteLine`
      is correct (per-test capture, no console bleed — Microsoft
      Learn xUnit "Capturing Output" at `https://xunit.net/docs/
      capturing-output`). In a `static Main` console-exe host,
      `Console.WriteLine` is correct (stdout goes to the process's
      stdout, no injection required). Both rows are FIRST-CLASS
      idioms in the KB and codegen selects between them based on
      the per-file host classification (host-shape lookup happens
      at the file-level, not per-call). Empty-line nuance (NOT
      EXERCISED here — no `print('')` in this file): Dart
      `print('')` would emit a single `\n` (empty body plus
      print's trailing newline); the equivalent C# would be
      `Console.WriteLine()` (no args) which emits
      `Environment.NewLine`. Encoding nuance: both Dart strings and
      C# strings are UTF-16 internally; `Console.WriteLine`
      defaults to the console's active code page on Windows (UTF-16
      on modern terminals; ASCII-only payload in THIS file so the
      nuance is not load-bearing). Trailing-newline nuance: Dart
      `print(s)` ALWAYS appends a newline; C# `Console.WriteLine
      (s)` ALWAYS appends `Environment.NewLine` — semantically
      equivalent.

  - construct_key: dart.c_style_for_loop_over_list_length_with_indexer
    source_form: >-
      "for (int i = 0; i < result.ops.length; i++) {
         print('  $i: ${result.ops[i]}');
       }"
    target_decision: >-
      Dart C-style for-loop `for (int i = 0; i < <list>.length; i++)
      { ... }` maps 1:1 to C# `for (int i = 0; i < <list>.Count; i++)
      { ... }` (or `<list>.Length` if the converted SUT property is
      named `Length` — owned by the lib spec
      `lib/compiler/result.dart.md` / `lib/bytecode/runner.dart.md`).
      REUSE the cached `rf-dart-c-style-for-loop-to-csharp-verbatim`
      idiom from `test/bytecode/inspect_bytecode_test.dart.md`. Loop
      header syntax is byte-identical between Dart and C# for the
      `int i = 0`, `i++`, and bracketed-body forms (Microsoft Learn
      `https://learn.microsoft.com/dotnet/csharp/language-reference/
      statements/iteration-statements#the-for-statement`). The
      property-name conversion `result.ops.length` -> `result.Ops.
      Count` (or `result.Ops.Length`) PascalCases BOTH `ops` and
      `length` per the cached
      `rf-dart-instance-field-camelcase-to-csharp-property-pascalcase`
      idiom — the precise SUT-side property name (`Count` for
      `IReadOnlyList<T>` / `List<T>`, `Length` for arrays) is decided
      by the SUT specs and consulted at codegen emit time. The Dart
      list-indexer `result.ops[i]` translates to C# `result.Ops[i]`
      via the cached `rf-dart-list-indexer-to-csharp-list-indexer`
      idiom from `test/bytecode/inspect_bytecode_test.dart.md` —
      `List<T>` / `IReadOnlyList<T>` / array indexing uses identical
      `[i]` syntax in both languages (Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1.item`).
      The loop body (single `print` statement) translates per the
      `dart.core.print` and
      `dart.string_interpolation_with_list_indexer` constructs.
    idiom_id: rf-dart-c-style-for-loop-to-csharp-verbatim
    research_finding_id: rf-dart-c-style-for-loop-to-csharp-verbatim
    nuance: >-
      Loop-header nuance (KB cache hit — REUSE from
      `test/bytecode/inspect_bytecode_test.dart.md`): Dart and C#
      C-style `for (init; cond; update) body` are byte-identical in
      syntax; the only conversion is the per-property casing for
      `.length` -> `.Count` (or `.Length`) and the indexer-target
      property `ops` -> `Ops`. Bounds-evaluation nuance (explicitly
      addressed): both Dart and C# evaluate the loop CONDITION on
      EVERY iteration (Microsoft Learn for-statement reference cited
      above) — so `result.ops.length` / `result.Ops.Count` is
      computed each iteration. For an immutable list this is a
      constant-cost check; for a mutable list (not the case here —
      the loop body does not mutate `result.ops`) this would be
      relevant. Code-generator MAY hoist `var n = result.Ops.Count;`
      outside the loop for readability/performance but is NOT
      REQUIRED (the .NET JIT typically hoists invariant
      property-loads automatically). Foreach alternative (NOT
      chosen): `foreach (var op in result.Ops.Select((op, i) =>
      (op, i)))` would express the same loop more idiomatically in
      C# but LOSES the explicit `int i` counter shape and adds
      LINQ overhead — keeping the C-style `for` preserves the
      one-to-one shape and matches the precedent set by
      `inspect_bytecode_test.dart.md`. Iteration-variable scope
      nuance: Dart `int i = 0;` declares `i` with loop-statement
      scope; C# `int i = 0;` likewise — both languages release `i`
      at the closing brace of the loop. No shadowing risk in this
      file (no outer `i`). Off-by-one nuance: `i < length` and
      `i < Count` produce identical inclusive-of-0, exclusive-of-N
      ranges on both sides — no boundary conversion needed.

  - construct_key: dart.string_interpolation_with_list_indexer
    source_form: "'  $i: ${result.ops[i]}'"
    target_decision: >-
      Map Dart string interpolation `'  $i: ${result.ops[i]}'` to a C#
      interpolated-string literal `$"  {i}: {result.Ops[i]}"` (Microsoft
      Learn `https://learn.microsoft.com/dotnet/csharp/language-reference/
      tokens/interpolated`). REUSE the cached
      `rf-dart-string-interpolation-to-csharp-interpolated-string`
      idiom from `test/debug_negative.dart.md` and the cached
      `rf-dart-list-indexer-to-csharp-list-indexer` idiom from
      `test/bytecode/inspect_bytecode_test.dart.md`. Two
      interpolation slots in this literal:
        - Dart `$i` (bare-identifier shorthand) -> C# `{i}` — C#
          has NO bare-identifier shorthand; the curly braces are
          mandatory.
        - Dart `${result.ops[i]}` (full-brace expression with
          property access + indexer) -> C# `{result.Ops[i]}` — the
          PascalCased property `Ops` per the cached identifier-
          casing idiom; the indexer `[i]` is byte-identical.
      Leading whitespace (`'  '` — two spaces) and the literal `:`
      separator preserve verbatim. The Dart literal has no escape
      characters; the C# interpolated literal has no escape
      characters either. NO need to use a C# verbatim
      interpolated string (`$@"..."`) or a C# 11+ raw interpolated
      string (`$"""..."""`) because the payload contains no `"` or
      `\` characters and no embedded newlines.
    idiom_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      Syntax-prefix nuance (KB cache hit — REUSE from
      `test/debug_negative.dart.md` and `test/bytecode/
      inspect_bytecode_test.dart.md`): Dart prefixes the literal
      with `'` (or `"`); C# REQUIRES the `$` prefix BEFORE the
      literal opener (`$"..."`). Dart's `${expr}` becomes C#'s
      `{expr}` (curly braces only, no `$` inside the braces);
      Dart's shorthand `$name` (only valid for a bare identifier)
      becomes C#'s full-braced `{name}` (C# has no shorthand).
      Brace-escape nuance (NOT EXERCISED here — no literal braces
      in this file's interpolation): a literal `{` or `}` in a C#
      interpolated string MUST be doubled (`{{` / `}}`).
      ToString-conversion nuance (load-bearing, explicitly
      addressed): both Dart and C# interpolate via the embedded
      expression's `toString()` (Dart) / `ToString()` (C#)
      method. `int.toString()` (Dart) and `int.ToString()` (C#)
      both produce the integer's decimal representation. The
      `result.ops[i]` element is an `Op`-shaped SUT type per
      `lib/bytecode/runner.dart.md` — codegen MUST ensure the
      converted `Op` type overrides `ToString()` consistently
      with the Dart `Op.toString()` override (decided by the SUT
      spec). If the SUT-side `Op.ToString()` is NOT consistent
      with `Op.toString()` from the Dart side, the bytecode
      output of this diagnostic differs across the conversion —
      recorded here as a DOWNSTREAM CONSISTENCY GATE (not an
      escalation, because the per-construct decision at THIS
      call-site is fully determined: emit `{result.Ops[i]}`
      verbatim and rely on the SUT-side `ToString()` override).
      Format-provider nuance: C# `$"..."` uses the CURRENT
      CULTURE's `IFormatProvider` for `IFormattable` arguments by
      default — for integers (`i`) this is irrelevant
      (`int.ToString()` is culture-invariant for the default
      format); for the `Op` element it depends on whether the
      converted `Op.ToString()` uses culture-sensitive formatting
      (SUT decision). The conversion does NOT need to emit
      `string.Format(CultureInfo.InvariantCulture, ...)` here —
      the diagnostic-output context tolerates the default
      culture. Format-specifier nuance (NOT EXERCISED): Dart
      interpolation has NO format-specifier syntax (no `${expr:
      format}`); C# interpolation DOES (`{expr:format}`). Codegen
      MUST emit `{i}` and `{result.Ops[i]}` with NO format
      specifier to preserve the Dart `toString()`-default
      formatting.

conversion_units:
  - "cu-1: file-scope using directives — `using System;` (for `Console.WriteLine`), `using <RootNs>.Compiler;` (for `GlpCompiler` and `BytecodeProgram`). NO `using Xunit;` (this file is NOT a [Fact] file). NO `using Xunit.Abstractions;` (no `ITestOutputHelper`). NO `using System.Linq;` (no LINQ surface)."
  - "cu-2: namespace declaration mirroring `test/` — `namespace <RootNs>.Test;` (single top-level namespace; mirrors `test/test_constant_compile.dart`'s position in `test/`, the test-root)."
  - "cu-3: host class `public static class TestConstantCompile` (PascalCased from `test_constant_compile.dart`) — NOT a test class, NO public test-method visibility, NO xUnit attributes. Identical shape to `DebugNegative` host class from `test/debug_negative.dart.md`."
  - "cu-4: `public static int Main(string[] args)` entrypoint hoisted from the Dart top-level `void main` — body translated statement-for-statement: `var compiler = new GlpCompiler();` -> `Console.WriteLine(\"=== Testing: test_nil([]) ===\");` -> `var result = compiler.Compile(\"test_nil([]).\");` -> `Console.WriteLine(\"Bytecode:\");` -> `for (int i = 0; i < result.Ops.Count; i++) { Console.WriteLine($\"  {i}: {result.Ops[i]}\"); }` -> `return 0;`. The `string[] args` parameter is required by the C# `Main` signature but is unused."
  - "cu-5: NO xUnit attributes, NO [Fact], NO [Trait], NO DisplayName — this file is a console-exe diagnostic harness, NOT a test fixture (see the `dart.diag_script.void_main_no_package_test_no_assertions` construct rationale)."
  - "cu-6: NO constructor (instance or static) — no `late` field, no `setUp`, no pre-loop initialization to seed. The host class holds ONLY the `Main` entrypoint."
  - "cu-7: NO `ITestOutputHelper` injection, NO `using Xunit.Abstractions;` — `print(...)` routes to `Console.WriteLine(...)` because the host shape is `static Main`, not `[Fact]` (see the `dart.core.print` construct rationale)."
  - "cu-8: DOWNSTREAM CONSISTENCY GATE (recorded, not asserted by this artifact): the diagnostic output depends on the SUT-side `Op.ToString()` override being consistent with the Dart-side `Op.toString()` override. The SUT spec `lib/bytecode/runner.dart.md` owns that decision; codegen MUST consult it at emit time but the call-site shape `{result.Ops[i]}` is fully determined here. NOT an escalation — every per-construct decision at this file's scope is grounded in cached authoritative idioms."
  - "cu-9: alternative-host-shape NOT chosen (recorded for langpair-level review): C# 9+ top-level statements would render this file as a bare program WITHOUT a wrapping class. Authoritative per Microsoft Learn `https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements`. Default keeps the explicit `static class` + `static int Main` shape for symmetry with `test/debug_negative.dart.md` — codegen MAY switch to top-level statements per langpair preference."
escalations: []
```

## Rationale + research provenance

### Why this file is NOT an xUnit `[Fact]` conversion

Every other `test/**.dart` file specced so far in this conversion (smoke_test, glp_runtime_test, test_channel_construction, the multiagent/, conformance/, heap/, module/, compiler/, bytecode/, and analysis/type_checker/ peers) imports `package:test/test.dart` and calls `test(...)` / `expect(...)`. This file imports NEITHER — its sole import is the SUT facade (`package:glp_runtime/compiler/compiler.dart`), and its `main()` body calls `print(...)` only. There is no `test()` registration, no `expect()` assertion, no matcher, no `group()`, no `setUp`/`tearDown`. The host shape on the Dart side is a `dart run <file>` diagnostic script — invoked manually by a developer to print a bytecode trace for `test_nil([])`, NOT discovered by `dart test`.

The xUnit-conversion idiom recorded across the peer specs (`rf-dart-package-test-to-dotnet-xunit`, `rf-dart-test-main-to-xunit-class-with-facts`) is therefore INAPPLICABLE — applying it would force this file into a `[Fact]`-attributed method whose body prints to a runner log without asserting anything, polluting test reports and miscategorising the file. The correct counterpart on the .NET side (REUSE from `test/debug_negative.dart.md`'s `rf-dart-debug-script-main-to-csharp-static-main` idiom) is a `public static class TestConstantCompile` host with a `public static int Main(string[] args)` entrypoint (Microsoft Learn "Main method in C# programs", `https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/main-command-line`). The classification rule (presence/absence of `package:test` import + `test(...)` calls) is the SAME host-shape-lookup rule documented in `debug_negative.dart.md`.

### Why `Console.WriteLine`, not `ITestOutputHelper.WriteLine`

`test/bytecode/inspect_bytecode_test.dart.md` (which is xUnit-hosted) routes Dart `print(...)` to xUnit's `ITestOutputHelper.WriteLine` because the host there is a `[Fact]` method on a test class that takes the helper through constructor injection (xUnit "Capturing Output", `https://xunit.net/docs/capturing-output`). HERE the host is a `static Main` — there is no test-class instance, no constructor injection, no per-test capture model. The correct .NET sink (REUSE from `test/debug_negative.dart.md`'s `rf-dart-print-in-console-exe-to-console-writeline` idiom) is `Console.WriteLine` (Microsoft Learn `https://learn.microsoft.com/dotnet/api/system.console.writeline`), which writes to the process's stdout stream. Both rows are first-class entries in the KB; the host-shape lookup decides which row applies per file — the SAME routing-by-host-shape pattern that the test-framework idiom uses (xUnit framework choice depends on the file BEING a `package:test` file).

### Why `for (int i = 0; ...)` survives as `for (int i = 0; ...)` (not `foreach`)

The Dart C-style for-loop is byte-identical to its C# counterpart in surface syntax (Microsoft Learn `https://learn.microsoft.com/dotnet/csharp/language-reference/statements/iteration-statements#the-for-statement`). REUSE the cached `rf-dart-c-style-for-loop-to-csharp-verbatim` idiom recorded by `test/bytecode/inspect_bytecode_test.dart.md` (which had the SAME loop shape: `for (int i = 0; i < prog.ops.length; i++)`). The explicit `int i` counter is preserved because the print body uses `$i` — switching to `foreach` would require the LINQ `Select((op, i) => ...)` shape, which adds idiom drift without preserving the one-to-one statement-for-statement shape that the rest of this file's conversion units use. The `.length` -> `.Count` (or `.Length`) PascalCasing and the `.ops` -> `.Ops` PascalCasing are owned by the SUT lib specs (`lib/compiler/result.dart.md`, `lib/bytecode/runner.dart.md`) — this artifact records only the call-site shape.

### Why `final` -> `var`, and the lost-immutability asymmetry

Dart `final <local>` declares a write-once binding; C# has no first-class `let`/`readonly`-local keyword (C# `readonly` is field-only, `in` is parameter-only). REUSE the cached `rf-dart-final-local-to-csharp-var-local` idiom from `test/analysis/type_checker/well_typed_clause_test.dart.md` and `test/debug_negative.dart.md`. Both Dart locals (`compiler`, `result`) become C# `var` locals — observably equivalent because neither is reassigned. The lost-immutability annotation is a known asymmetry recorded across the batch; not escalable because both Dart `final` and C# `var` describe write-once semantics in this file's call-site reality (the C# compiler does not enforce write-once for `var` locals but the conversion does not require it for correctness here).

### Why string interpolation maps verbatim

Dart `$i` (bare-identifier shorthand) and `${expr}` (full-brace) both map to C# `{i}` / `{expr}` inside a `$"..."` prefixed interpolated string (Microsoft Learn "$ — string interpolation" at `https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/interpolated`). REUSE the cached `rf-dart-string-interpolation-to-csharp-interpolated-string` idiom from `test/debug_negative.dart.md`. C# has NO shorthand for bare identifiers — the curly braces are mandatory. The payload contains no `"` or `\` characters and no embedded newlines, so no verbatim or raw interpolated form is needed. The `ToString()`-by-default convention is byte-identical on both sides; the diagnostic output's faithfulness depends on the SUT-side `Op.ToString()` override (recorded as a downstream consistency gate in cu-8, not an escalation).

### Why no escalations

Every construct has a single-decision target shape grounded in CACHED authoritative idioms recorded by prior batch specs:

- `rf-dart-internal-package-import-to-csharp-using` (REUSED from `test/debug_negative.dart.md`, `test/bytecode/inspect_bytecode_test.dart.md`, `test/compiler/reserved_constant_test.dart.md`)
- `rf-dart-debug-script-main-to-csharp-static-main` (REUSED from `test/debug_negative.dart.md`)
- `rf-dart-print-in-console-exe-to-console-writeline` (REUSED from `test/debug_negative.dart.md`)
- `rf-dart-c-style-for-loop-to-csharp-verbatim` (REUSED from `test/bytecode/inspect_bytecode_test.dart.md`)
- `rf-dart-list-indexer-to-csharp-list-indexer` (REUSED from `test/bytecode/inspect_bytecode_test.dart.md`)
- `rf-dart-final-local-to-csharp-var-local` (REUSED from `test/analysis/type_checker/well_typed_clause_test.dart.md` and `test/debug_negative.dart.md`)
- `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new` (REUSED from the lib `lib/compiler/*.dart.md` specs and `test/debug_negative.dart.md`)
- `rf-dart-instance-method-camelcase-to-csharp-pascalcase` and `rf-dart-instance-field-camelcase-to-csharp-property-pascalcase` (REUSED across the batch — Microsoft C# Identifier-Names guide)
- `rf-dart-string-interpolation-to-csharp-interpolated-string` (REUSED from `test/debug_negative.dart.md`)
- `rf-dart-single-quoted-string-to-csharp-double-quoted-string` (REUSED across the batch)

All well-known nuances (value-vs-reference, async/`Future` -> `Task`, null-safety, `Stream` -> `IAsyncEnumerable`, isolate, format-provider, brace-escape, encoding, trailing-newline, ToString-consistency) are addressed in the construct rows where they apply — value-vs-reference is stable (both reference types), no async surface, no null-safety surface beyond the default NNBD, no Stream/isolate surface. The ToString-consistency between SUT-side `Op.ToString()` and Dart-side `Op.toString()` is recorded as a downstream consistency gate (cu-8) rather than an escalation because the per-construct call-site decision here (`{result.Ops[i]}`) is fully determined; the gate is owned by the SUT lib spec.

## Notes

- File is 12 lines, single `void main()`, single import, no async surface, no error handling, no test framework — a minimal `dart run` diagnostic harness.
- Identical host-shape classification to `test/debug_negative.dart.md`: NOT a `package:test` file, maps to C# `static Main` console-exe entrypoint.
- The Dart hardcoded GLP source string `'test_nil([]).'` is a one-off literal — not a triple-quoted multi-line fixture (contrast with `test/debug_negative.dart.md`'s `'''...'''` clause-source literals) — so no verbatim/raw-string nuance applies here.
- No `package:test` import means the conversion does NOT pull in `using Xunit;` or `using Xunit.Abstractions;` — only `using System;` (for `Console.WriteLine`) and `using <RootNs>.Compiler;` (for `GlpCompiler` and `BytecodeProgram`).
- The `result.ops.length` property chain PascalCases on BOTH the `.ops` field (`Ops`) and the `.length` getter (`Count` for `List<T>` / `IReadOnlyList<T>`, or `Length` for arrays) — owned by the SUT lib specs `lib/compiler/result.dart.md` and `lib/bytecode/runner.dart.md`.
- The single-quoted Dart string `'test_nil([]).'` becomes the double-quoted C# string `"test_nil([])."` — no escaping required for the parentheses/brackets/period payload.
- Zero escalations. Every construct REUSES a cached idiom recorded by a prior-batch spec (predominantly `test/debug_negative.dart.md`, with cross-references to `test/bytecode/inspect_bytecode_test.dart.md`, `test/compiler/reserved_constant_test.dart.md`, and the lib `lib/compiler/*.dart.md` specs). The downstream consistency gate (SUT `Op.ToString()` faithfulness) is recorded but not escalated — the per-construct decision at this file's scope is fully determined.
