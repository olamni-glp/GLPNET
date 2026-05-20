# Conversion Spec — test/bytecode/inspect_bytecode_test.dart

> Conversion-spec artifact for test/bytecode/inspect_bytecode_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> A tiny (15-line) xUnit-shaped diagnostic test that constructs a
> `GlpCompiler`, compiles the single GLP source string `r(a,[b]).`
> into a `BytecodeProgram`, and prints (`print(...)`) every op in
> `prog.ops` together with its index and `runtimeType` for human
> inspection. No assertions — this is a print-only inspection test
> (the framework reports test pass on completion). Every construct
> below is a cache hit in the idiom KB (FR-012 / SC-007 — REUSE, do
> not re-derive): xUnit framework + `[Fact]` shape + `using Xunit;` +
> `using Xunit.Abstractions;` for `ITestOutputHelper` (sibling test
> convspecs — `test/smoke_test.dart.md`, `test/glp_runtime_test.dart.md`,
> `test/test_channel_construction.dart.md`,
> `test/bytecode/utility_instructions_test.dart.md`,
> `test/bytecode/fairness_scheduler_loop_test.dart.md`,
> `test/conformance/fairness_26_test.dart.md`,
> `test/heap/suspension_pointer_test.dart.md`); the `GlpCompiler`
> facade + `BytecodeProgram.ops` surface from `lib/compiler/compiler.dart.md`
> and `lib/compiler/codegen.dart.md`; the `print` → `ITestOutputHelper.
> WriteLine` row (`rf-dart-print-to-xunit-itestoutputhelper-writeline`);
> the C-style for-loop row
> (`rf-dart-c-style-for-loop-to-csharp-verbatim`); the string-interp row
> (`rf-dart-string-interpolation-to-csharp-dollar-string`); the
> `op.runtimeType` row
> (`rf-dart-runtimetype-in-interpolation-to-csharp-gettype-name`); the
> `.length` + `[i]` list-indexer row
> (`rf-dart-list-indexing-to-csharp-list-indexer`); and the `final`
> local-variable row
> (`rf-dart-final-local-to-csharp-var`). No escalations.

```yaml
schema_version: 1
source_path: test/bytecode/inspect_bytecode_test.dart
source_sha256: 6437ae016ce3d7f8290f83b6488b7f11ebb1c68caae09fccbddd32b73e788d8b
target_code_unit: test/bytecode/InspectBytecodeTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop `import 'package:test/test.dart';` and emit `using Xunit;`
      plus (because the test body calls `print(...)`) inject an
      `ITestOutputHelper output` parameter into the test class
      constructor with `using Xunit.Abstractions;`. Per FR-012 /
      SC-007 this construct is a KB cache hit — REUSE the batch-wide
      xUnit choice settled in `test/smoke_test.dart.md` and reused
      across every prior test convspec in the batch; do NOT re-derive.
      The .NET test project's `.csproj` (`xunit`,
      `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) is a
      langpair-level emission concern, OUT OF SCOPE for this per-file
      artifact.
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice reuse (explicitly addressed, no re-derivation):
      xUnit settled batch-wide; `test('name', fn)` ⇒ `[Fact]
      public void Name() { ... }` on a fresh test-class instance per
      `[Fact]` (xunit.net "Shared Context between Tests"). The body
      is synchronous — `[Fact]` returns `void`, not `async Task`
      (no `await` / `Future` / `Stream` / isolate surface in the
      source). Strict-bool / strict-equality semantics unaffected by
      the import itself. The `print(...)` call inside the test pulls
      `using Xunit.Abstractions;` into this file (see
      `dart.core.print` construct below).
  - construct_key: dart.internal_package_import.same_package_single
    source_form: "import 'package:glp_runtime/compiler/compiler.dart';"
    target_decision: >-
      Drop the Dart `import 'package:glp_runtime/compiler/compiler.dart';`
      directive and emit one file-level C# `using <RootNs>.Compiler;`
      directive. The converted `GlpCompiler` facade lives in the
      `<RootNs>.Compiler` sub-namespace per
      `lib/compiler/compiler.dart.md` (and the transitively-referenced
      `BytecodeProgram` per `lib/compiler/codegen.dart.md` reachable
      via that same namespace or via `<RootNs>.Bytecode` re-export —
      langpair-level concern; for THIS file only `<RootNs>.Compiler`
      is required because the source only names `GlpCompiler` and
      uses `prog.ops` / `prog.ops[i]` member-access on the returned
      value). Per FR-012 / SC-007 this is a KB cache hit — REUSE the
      `rf-dart-internal-package-import-to-csharp-using` row settled
      across sibling test convspecs; do NOT re-research. The test
      assembly's `.csproj` must reference the converted-SUT assembly —
      langpair-level concern, OUT OF SCOPE.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed, KB reuse):
      Dart `package:` imports are per-file path-based; C# `using` is
      per-namespace. One Dart import that brings `GlpCompiler` into
      scope ⇒ one C# `using <RootNs>.Compiler;`. `GlpCompiler` is
      `public` on the Dart side (no leading underscore) ⇒ `public`
      on the C# side per the SUT spec
      `lib/compiler/compiler.dart.md`. No cross-isolate,
      cross-package, or transitive-export semantics apply. No
      `using static` is needed — `GlpCompiler` is named qualified at
      its single call-site `GlpCompiler()` constructor invocation.
  - construct_key: dart.test_file.void_main_with_single_test_call_no_group
    source_form: >-
      "void main() {
         test('Inspect bytecode for r(a,[b])', () { ... });
       }"
    target_decision: >-
      Eliminate the Dart `void main()` entirely (xUnit has no
      file-level entry point; discovery is reflection-driven over
      `[Fact]`). Emit `public class InspectBytecodeTest` (file-name
      `inspect_bytecode_test.dart` ⇒ `InspectBytecodeTest.cs` ⇒
      `class InspectBytecodeTest`) with ONE `[Fact] public void
      InspectBytecodeForRAB()` instance method whose body is the
      verbatim translation of the single Dart `test()` closure body.
      The descriptive Dart test name `'Inspect bytecode for r(a,[b])'`
      becomes the xUnit attribute `DisplayName` —
      `[Fact(DisplayName = "Inspect bytecode for r(a,[b])")]` —
      because C# method-name identifier rules forbid the `(`, `,`,
      `[`, `]` punctuation in the original Dart string (the same
      decision recorded for the discriminating-character / GLP-syntax
      test names in the sibling convspecs
      `test/conformance/fairness_26_test.dart.md` and
      `test/bytecode/utility_instructions_test.dart.md`). Per
      FR-012 / SC-007 this is a KB cache hit; do NOT re-derive.
    idiom_id: rf-dart-test-main-to-xunit-class-with-facts
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Discovery model nuance (explicitly addressed): Dart
      `package:test` discovers tests by executing `main()` which
      registers closures via `test(...)`; xUnit discovers via
      reflection over `[Fact]`. No `setUp` / `tearDown` is present in
      the source — no `IClassFixture<T>` / `IAsyncLifetime`
      machinery is needed. xUnit instantiates a fresh test-class
      instance per `[Fact]` (xunit.net "Shared Context between
      Tests"), so the implicit per-test isolation Dart's
      `test('name', fn)` provides is preserved without extra ceremony.
      The class is NOT marked `sealed` (xUnit prefers a non-sealed
      public test class so the discovery + runner reflection can
      construct it).
  - construct_key: dart.local_var.final_typed_via_constructor_call
    source_form: >-
      "final compiler = GlpCompiler();
       final prog = compiler.compile('r(a,[b]).');
       final op = prog.ops[i];"
    target_decision: >-
      Three `final`-inferred local-variable declarations ⇒ three C#
      `var` local-variable declarations (`var compiler = new
      GlpCompiler(); var prog = compiler.Compile("r(a,[b])."); var
      op = prog.Ops[i];`). Dart `final` on a local + type-inference
      from the initializer ⇒ C# `var` (compiler-inferred static type,
      no reassignment in the source). C# `var` is non-reassignable
      via convention here but the C# language itself permits
      reassignment of `var`-typed locals — the Dart source happens
      not to reassign any of these three, so the surface behavioural
      difference is moot (no `readonly` keyword exists for locals in
      C#). Per FR-012 / SC-007 this is a KB cache hit — REUSE the
      `rf-dart-final-local-to-csharp-var` row settled across sibling
      test + lib convspecs; do NOT re-derive.
    idiom_id: rf-dart-final-local-to-csharp-var
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Local-immutability nuance (explicitly addressed): Dart `final`
      on a LOCAL means "assigned once, single-assignment"; C# has no
      equivalent local-modifier keyword (`readonly` is field-only).
      Mapping to `var` is the established convention because (a) the
      single-assignment property is observable at code-review time
      in the converted method body (none of the three locals is
      reassigned), and (b) static type-inference is preserved
      (Microsoft Learn — "Implicitly typed local variables"). No
      `late` / `late final` / null-safety inflation here — all three
      initializers are non-null returns. No
      reference-vs-value-type confusion either: `GlpCompiler` and
      `BytecodeProgram` are reference types in both languages; the
      `op` local is the per-iteration element of `prog.ops` whose
      static type comes from the converted ops collection's element
      type (settled in `lib/compiler/codegen.dart.md`).
  - construct_key: dart.method_invocation.glp_compiler_compile_source_string
    source_form: "compiler.compile('r(a,[b]).')"
    target_decision: >-
      Map the Dart instance-method call `compiler.compile(source)`
      ⇒ C# `compiler.Compile(source)` (Dart camelCase ⇒ C#
      PascalCase per the SUT spec
      `lib/compiler/compiler.dart.md` "BytecodeProgram compile(String
      source, [CompileOptions? options])" ⇒ `public BytecodeProgram
      Compile(string source, CompileOptions? options = null)`). The
      single-quoted Dart string literal `'r(a,[b]).'` ⇒ C# regular
      double-quoted string literal `"r(a,[b])."` (no escape required
      — no `"`, no `\`, no `$`, no `{` characters present). The
      optional positional `CompileOptions?` second parameter is
      defaulted (the Dart call omits it; C# default-argument value
      `= null` is the equivalent shape, settled in
      `lib/compiler/compiler.dart.md`).
    idiom_id: rf-dart-method-camelcase-to-csharp-pascal-call
    research_finding_id: rf-dart-method-camelcase-to-csharp-pascal-call
    nuance: >-
      API-rename nuance (explicitly addressed, KB reuse): every
      instance-method call into converted SUT code in this file
      (`compiler.compile(...)`) follows the project-wide camelCase ⇒
      PascalCase rule recorded in `lib/compiler/compiler.dart.md`
      (and reused across every prior call-site convspec). String
      semantics: Dart and C# string literals are both UTF-16 — no
      encoding nuance. No `Future` / `Stream` / `async` surface —
      `compile` is synchronous on both sides (settled in
      `lib/compiler/compiler.dart.md`). Optional-positional Dart
      param ⇒ default-valued C# param (no `params`, no overload
      proliferation).
  - construct_key: dart.member_access.bytecode_program_ops_property_and_indexer
    source_form: >-
      "prog.ops.length
       prog.ops[i]"
    target_decision: >-
      `prog.ops` ⇒ `prog.Ops` (Dart camelCase property ⇒ C#
      PascalCase property — same rename rule as above). The
      `.length` getter on a Dart `List<...>` ⇒ the `.Count` property
      on the converted C# collection (per the SUT-side `Ops`
      collection type settled in `lib/compiler/codegen.dart.md` —
      `IReadOnlyList<Op>` / `List<Op>` exposes `.Count`, NOT
      `.Length`; `.Length` would be wrong because that is the array
      / string property name, not the `List<T>` property name —
      Microsoft Learn `System.Collections.Generic.List<T>.Count`).
      The Dart `prog.ops[i]` indexer ⇒ C# `prog.Ops[i]` indexer
      (both languages support the `[i]` element-access syntax on
      `List<T>` / `IReadOnlyList<T>` — Microsoft Learn
      `List<T>.this[int]` indexer). Per FR-012 / SC-007 this is a KB
      cache hit; do NOT re-derive.
    idiom_id: rf-dart-list-indexing-to-csharp-list-indexer
    research_finding_id: rf-dart-list-indexing-to-csharp-list-indexer
    nuance: >-
      Property-name nuance (explicitly addressed): Dart's
      `List<T>.length` is UNIVERSAL across `List`, `String`, and
      arrays (`dart:core` `Iterable.length`). C# splits the spelling:
      arrays + `string` use `.Length`, generic collections
      `List<T>` / `IReadOnlyList<T>` / `ICollection<T>` use `.Count`.
      The converted `Ops` is a generic collection (not an array, not
      a string) ⇒ `.Count` is correct, NOT `.Length`. Indexer
      semantics are equivalent: 0-based, bounds-checked at runtime
      (`IndexOutOfRangeException` on the C# side vs Dart's
      `RangeError` — same exception family in spirit; not relevant
      to this happy-path inspection test which always stays in
      `[0, prog.ops.length)`). No value-vs-reference confusion —
      `Op` is a reference type in both languages.
  - construct_key: dart.statement.classical_for_loop_int_index
    source_form: "for (int i = 0; i < prog.ops.length; i++) { ... }"
    target_decision: >-
      Emit a verbatim-shape C# `for` statement: `for (int i = 0; i <
      prog.Ops.Count; i++) { ... }`. Dart and C# share the same
      C-style three-clause `for` syntax — init / condition / update.
      The loop body is two interpolated `print(...)` statements
      (handled by the two constructs below). Per FR-012 / SC-007
      this is a KB cache hit — REUSE the
      `rf-dart-c-style-for-loop-to-csharp-verbatim` row settled in
      `test/conformance/fairness_26_test.dart.md` and reused in
      `test/heap/suspension_pointer_test.dart.md`; do NOT re-derive.
    idiom_id: rf-dart-c-style-for-loop-to-csharp-verbatim
    research_finding_id: rf-dart-c-style-for-loop-to-csharp-verbatim
    nuance: >-
      Loop-shape nuance (explicitly addressed, KB reuse): Dart's
      classical `for (init; cond; update)` is syntactically and
      semantically identical to C#'s — no `for-in` ⇒ `foreach`
      rewrite is needed (this is an index-based loop, not an
      iteration-based one). Integer arithmetic (`i++`) is identical
      in both languages on `int` (Dart `int` is arbitrary-precision
      but bounded operations on small indices fit C# `int` with
      identical semantics for `[0, prog.ops.length)`). The
      `prog.Ops.Count` property access in the condition is
      re-evaluated each iteration in both languages (no caching
      change). No collection mutation inside the loop — bounds
      stable.
  - construct_key: dart.core.print
    source_form: >-
      "print('\n=== r(a,[b]). DETAILED ===');
       print('  $i: $op (${op.runtimeType})');"
    target_decision: >-
      Map every `print(...)` call ⇒ `_output.WriteLine(...)` on an
      `ITestOutputHelper _output` field initialized via constructor
      injection on the test class (xunit.net "Capturing Output").
      Two `print(...)` sites in this file ⇒ two `_output.WriteLine`
      sites. The class constructor `public InspectBytecodeTest
      (ITestOutputHelper output) { _output = output; }` is added per
      the batch-wide convention (see `test/smoke_test.dart.md`
      pulling the same constructor shape into every test class with
      `print` calls). Per FR-012 / SC-007 this is a KB cache hit —
      do NOT re-derive.
    idiom_id: rf-dart-print-to-xunit-itestoutputhelper-writeline
    research_finding_id: rf-dart-print-to-xunit-itestoutputhelper-writeline
    nuance: >-
      Output-capture nuance (explicitly addressed): Dart `print(...)`
      writes to the process stdout, which `package:test` captures
      per-test. xUnit explicitly forbids `Console.WriteLine` for
      per-test diagnostic output because xUnit parallelizes test
      execution and the shared `Console` would interleave output
      from concurrent tests (xunit.net "Capturing Output"). The
      `ITestOutputHelper` injection is the official xUnit-sanctioned
      replacement — output is associated with the specific
      `[Fact]` invocation, surfaced by the runner in the test's
      output pane / log. The Dart string `'\n=== r(a,[b]). DETAILED
      ===\n'` (leading `\n` preserves blank-line diagnostic spacing)
      maps to the C# `"\n=== r(a,[b]). DETAILED ==="` literal —
      `WriteLine` itself emits a trailing newline, so the leading
      `\n` stays inside the literal to preserve the original
      blank-line-before-header look.
  - construct_key: dart.string_interpolation.with_member_access_and_runtimetype
    source_form: "'  $i: $op (${op.runtimeType})'"
    target_decision: >-
      Dart single-quoted interpolated string `'  $i: $op
      (${op.runtimeType})'` ⇒ C# interpolated string `$"  {i}: {op}
      ({op.GetType().Name})"`. Three interpolation slots, two
      decisions: (1) the bare identifier slots `$i` and `$op` ⇒
      `{i}` and `{op}` (compiler-driven `ToString()` of the int /
      `Op` value — same semantics as Dart's implicit
      `.toString()` invocation inside `$...`); (2) the
      member-access slot `${op.runtimeType}` ⇒
      `{op.GetType().Name}` because Dart's `Object.runtimeType`
      (dart.dev language spec — `Object.runtimeType` returns the
      run-time `Type` whose `toString()` yields the unqualified
      class name) maps to .NET's `System.Type.Name` (Microsoft
      Learn — "the simple name of the type without the namespace"),
      NOT `op.GetType().FullName` (which would include the
      namespace prefix and diverge from Dart output). Per
      FR-012 / SC-007 BOTH constructs are KB cache hits —
      `rf-dart-string-interpolation-to-csharp-dollar-string` (the
      `$"..."` shape) AND
      `rf-dart-runtimetype-in-interpolation-to-csharp-gettype-name`
      (the `runtimeType` ⇒ `GetType().Name` decision); do NOT
      re-derive. The first idiom is the primary key recorded for
      this construct row; the second is referenced explicitly in
      this nuance to preserve provenance.
    idiom_id: rf-dart-string-interpolation-to-csharp-dollar-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-dollar-string
    nuance: >-
      Type-name shape nuance (explicitly addressed, KB reuse): Dart
      `Object.runtimeType.toString()` returns the UNQUALIFIED class
      name (e.g. `ClauseTry`, not
      `package:glp_runtime/bytecode/opcodes.dart::ClauseTry`); the
      .NET counterpart is `System.Type.Name` (NOT `FullName`, NOT
      `AssemblyQualifiedName`) — this is the cached decision
      recorded for `lib/lint/linter.dart.md`
      (`rf-dart-runtimetype-in-interpolation-to-csharp-gettype-name`)
      and is REUSED verbatim here so the converted print output
      matches the Dart output byte-for-byte (modulo whitespace).
      `ToString()` semantics for the bare `{i}` and `{op}` slots:
      Dart's `int.toString()` is locale-independent base-10; C#'s
      `int.ToString()` in an interpolated string also uses the
      invariant culture's base-10 by default (Microsoft Learn —
      `Int32.ToString()` no-arg overload uses
      `CultureInfo.CurrentCulture` BUT for an `int` with no format
      specifier the output is identical to the invariant culture).
      For `Op`-valued slot `{op}`, the converted `Op` type
      overrides `ToString()` so the C# interpolation surfaces the
      same diagnostic shape Dart's implicit `op.toString()` does
      (settled in `lib/bytecode/opcodes.dart.md` / sibling op-type
      specs).
conversion_units:
  - "cu-1 — file-level using directives — `using Xunit;`, `using Xunit.Abstractions;`, `using <RootNs>.Compiler;` (and any transitively-needed `using` for the `Op` element type per `lib/compiler/codegen.dart.md` — langpair-level concern)"
  - "cu-2 — top-level test class `InspectBytecodeTest` (public, non-sealed) — file-name to class-name mapping `inspect_bytecode_test.dart` → `InspectBytecodeTest.cs` → `class InspectBytecodeTest`"
  - "cu-3 — constructor `public InspectBytecodeTest(ITestOutputHelper output) { _output = output; }` + private readonly field `private readonly ITestOutputHelper _output;` — injected per xUnit Capturing Output"
  - "cu-4 — one `[Fact(DisplayName = \"Inspect bytecode for r(a,[b])\")] public void InspectBytecodeForRAB()` method — body is the verbatim translation of the single Dart `test()` closure"
  - "cu-5 — method-body unit 1 — `var compiler = new GlpCompiler();` (replaces Dart `final compiler = GlpCompiler();`)"
  - "cu-6 — method-body unit 2 — `_output.WriteLine(\"\\n=== r(a,[b]). DETAILED ===\");` (replaces Dart `print('\\n=== r(a,[b]). DETAILED ===');`)"
  - "cu-7 — method-body unit 3 — `var prog = compiler.Compile(\"r(a,[b]).\");` (replaces Dart `final prog = compiler.compile('r(a,[b]).');`)"
  - "cu-8 — method-body unit 4 — `for (int i = 0; i < prog.Ops.Count; i++) { var op = prog.Ops[i]; _output.WriteLine($\"  {i}: {op} ({op.GetType().Name})\"); }` (replaces the Dart for-loop verbatim)"
escalations: []
```

## Embedded human-readable rationale + provenance

Every construct in this file is a cache hit in the conversion-idiom KB
(FR-012 / SC-007 — REUSE, do not re-derive); no construct triggers a
fresh research call (FR-024 cache hit; offline-reproducible). The
authoritative sources backing each REUSED idiom (recorded in the
parent rows of the KB) are listed below for provenance:

### rf-dart-package-test-to-dotnet-xunit — `package:test` ⇒ xUnit (REUSED)

- **Authoritative source (Dart side)**: dart.dev `package:test`
  package documentation (`test('name', fn)` registration model;
  function-style top-level API).
- **Authoritative source (.NET side)**: Microsoft Learn — "Unit
  testing C# in .NET Core with dotnet test and xUnit"
  (learn.microsoft.com /en-us/dotnet/core/testing/unit-testing-csharp-with-xunit)
  + xunit.net official documentation (`[Fact]` discovery,
  per-test-class-instance lifecycle).
- **Conclusion**: emit `using Xunit;` and the `[Fact]`-on-instance-
  method shape for every Dart `test('name', fn)` call. Batch-wide
  default for the codeconv test-file conversion. REUSED here.

### rf-dart-internal-package-import-to-csharp-using — `import 'package:glp_runtime/...';` ⇒ `using <RootNs>....;` (REUSED)

- **Authoritative source (Dart side)**: dart.dev language spec —
  `import 'package:...';` directive.
- **Authoritative source (.NET side)**: Microsoft Learn — C#
  language reference `using` directive.
- **Conclusion**: collapse same-package imports into a single
  `using <RootNs>.<SubNamespace>;` per converted sub-namespace
  (`Compiler` here, per `lib/compiler/compiler.dart.md`). REUSED
  here.

### rf-dart-test-main-to-xunit-class-with-facts — `void main() { test(...); }` ⇒ class with `[Fact]` methods (REUSED)

- **Authoritative source**: xunit.net "Shared Context between
  Tests" (per-test-class instantiation lifecycle); Microsoft Learn
  xUnit unit-testing tutorial.
- **Conclusion**: drop `void main()`, lift each `test(...)` call
  into a `[Fact]` instance method on a public test class named
  after the source file. REUSED here.

### rf-dart-final-local-to-csharp-var — `final x = expr;` ⇒ `var x = expr;` (REUSED)

- **Authoritative source (Dart side)**: dart.dev language tour —
  "Final and const" + type-inference rules.
- **Authoritative source (.NET side)**: Microsoft Learn —
  "Implicitly typed local variables" (`var`).
- **Conclusion**: emit C# `var` for every Dart `final`-inferred
  local — observable single-assignment property at code-review
  time, static-type-inference preserved. REUSED here.

### rf-dart-method-camelcase-to-csharp-pascal-call — `obj.someMethod(...)` ⇒ `obj.SomeMethod(...)` (REUSED)

- **Authoritative source (Dart side)**: dart.dev "Effective Dart:
  Style — `lowerCamelCase` for member names".
- **Authoritative source (.NET side)**: Microsoft Learn — "C#
  identifier naming rules and conventions" (`PascalCase` for
  public members).
- **Conclusion**: rename every camelCase Dart method/property to
  PascalCase on the C# side. REUSED here for `compile` ⇒ `Compile`
  and `ops` ⇒ `Ops` (the latter is also covered by
  `rf-dart-list-indexing-to-csharp-list-indexer` for the indexer
  decision).

### rf-dart-list-indexing-to-csharp-list-indexer — `list[i]` + `list.length` ⇒ `list[i]` + `list.Count` (REUSED)

- **Authoritative source (Dart side)**: dart.dev `dart:core`
  `List<E>` — `operator []`, `length`.
- **Authoritative source (.NET side)**: Microsoft Learn —
  `System.Collections.Generic.List<T>` — `Count` property,
  `this[int]` indexer.
- **Conclusion**: indexer syntax is identical; the only rename is
  `.length` ⇒ `.Count` for generic collections (NOT `.Length`,
  which is array/string only). REUSED here for `prog.ops.length`
  and `prog.ops[i]`.

### rf-dart-c-style-for-loop-to-csharp-verbatim — `for (init; cond; update)` ⇒ verbatim (REUSED)

- **Authoritative source (Dart side)**: dart.dev language spec —
  C-style `for` statement.
- **Authoritative source (.NET side)**: Microsoft Learn — C# `for`
  statement.
- **Conclusion**: verbatim translation of the three-clause form;
  no `foreach` rewrite for index-based loops. REUSED here.

### rf-dart-print-to-xunit-itestoutputhelper-writeline — `print(...)` ⇒ `_output.WriteLine(...)` (REUSED)

- **Authoritative source**: xunit.net "Capturing Output"
  (`ITestOutputHelper`); xUnit explicitly forbids
  `Console.WriteLine` in parallel-executed tests.
- **Conclusion**: inject `ITestOutputHelper output` into the test
  class constructor; route every `print(...)` to
  `_output.WriteLine(...)`. REUSED here for both `print` sites.

### rf-dart-string-interpolation-to-csharp-dollar-string — `'$x'` / `'${e}'` ⇒ `$"{x}"` / `$"{e}"` (REUSED)

- **Authoritative source (Dart side)**: dart.dev language spec —
  string interpolation `$identifier` and `${expression}`.
- **Authoritative source (.NET side)**: Microsoft Learn — "$ — string
  interpolation" (C# language reference).
- **Conclusion**: `'  $i: $op (...)'` ⇒ `$"  {i}: {op} (...)"`.
  REUSED here.

### rf-dart-runtimetype-in-interpolation-to-csharp-gettype-name — `${obj.runtimeType}` ⇒ `{obj.GetType().Name}` (REUSED)

- **Authoritative source (Dart side)**: dart.dev `dart:core`
  `Object.runtimeType` — returns the run-time `Type`; its
  `toString()` is the UNQUALIFIED class name.
- **Authoritative source (.NET side)**: Microsoft Learn —
  `System.Type.Name` ("the simple name of the type without the
  namespace"), explicitly distinguished from `FullName` and
  `AssemblyQualifiedName`.
- **Conclusion**: `${op.runtimeType}` ⇒ `{op.GetType().Name}` —
  unqualified-name semantics preserved byte-for-byte. REUSED here.

## Cross-file dependencies (reuse provenance)

- `lib/compiler/compiler.dart.md` — `GlpCompiler` facade,
  `Compile(string, CompileOptions?)` signature, camelCase ⇒
  PascalCase rename rule.
- `lib/compiler/codegen.dart.md` — `BytecodeProgram` shape, `Ops`
  collection element type, indexer semantics.
- `lib/bytecode/opcodes.dart.md` (and sibling op-type specs) —
  `Op.ToString()` override behaviour driving the `{op}`
  interpolation slot.
- `test/smoke_test.dart.md`, `test/glp_runtime_test.dart.md`,
  `test/test_channel_construction.dart.md`,
  `test/bytecode/utility_instructions_test.dart.md`,
  `test/bytecode/fairness_scheduler_loop_test.dart.md`,
  `test/conformance/fairness_26_test.dart.md`,
  `test/heap/suspension_pointer_test.dart.md` — batch-wide xUnit
  decision, `ITestOutputHelper` injection, `[Fact(DisplayName=...)]`
  shape, C-style for-loop verbatim mapping.
- `lib/lint/linter.dart.md` — `runtimeType` ⇒ `GetType().Name`
  decision (single authoritative recording, REUSED here).

No escalations: every construct in this file is decidable from
existing idioms (FR-012 / SC-007 cache hits; FR-024 offline-
reproducible).
