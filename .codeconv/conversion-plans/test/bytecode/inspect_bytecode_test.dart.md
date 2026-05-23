---
path: test/bytecode/inspect_bytecode_test.dart
cycle_group_id: 113
scc_siblings: []
generated_at: 2026-05-21T16:39:49Z
source_sha256: 6437ae016ce3d7f8290f83b6488b7f11ebb1c68caae09fccbddd32b73e788d8b
schema_version: 1
---

# Conversion Plan: test/bytecode/inspect_bytecode_test.dart

## 1. Source Analysis

The Dart source file is a 15-line xUnit-shaped diagnostic / inspection test.
Actual inspection of `glp_runtime_net/test/bytecode/inspect_bytecode_test.dart`
(sha256 `6437ae01…e788d8b`) confirms exactly the following constructs:

- L1: `import 'package:test/test.dart';` — Dart test framework import.
- L2: `import 'package:glp_runtime/compiler/compiler.dart';` — internal
  same-package import bringing `GlpCompiler` (and transitively
  `BytecodeProgram`) into scope.
- L4: `void main() { ... }` — single top-level entry point that registers
  exactly ONE `test(...)` closure (no `group(...)`, no `setUp` /
  `tearDown`).
- L5: `test('Inspect bytecode for r(a,[b])', () { ... });` — one
  registration call with a descriptive string containing parentheses,
  comma and brackets (`(`, `,`, `[`, `]`) that are not valid in C#
  identifiers.
- L6: `final compiler = GlpCompiler();` — `final`-inferred local-variable
  declaration via constructor invocation.
- L8: `print('\n=== r(a,[b]). DETAILED ===');` — single-quoted string with
  leading `\n` newline escape; non-interpolated literal.
- L9: `final prog = compiler.compile('r(a,[b]).');` — `final` local
  initialized by instance-method call; the GLP-source argument
  `'r(a,[b]).'` is a plain ASCII single-quoted string (no escape).
- L10: `for (int i = 0; i < prog.ops.length; i++) { ... }` — classical
  C-style three-clause for-loop using `int` index variable, reading
  `prog.ops.length` each iteration; no caching, no mutation inside the
  loop.
- L11: `final op = prog.ops[i];` — `final`-inferred local from list
  indexer.
- L12: `print('  $i: $op (${op.runtimeType})');` — interpolated string
  with two bare-identifier slots `$i`, `$op` and one member-access slot
  `${op.runtimeType}`.

No assertions. No `expect(...)`. No `async`. No `Future` / `Stream`. No
isolate / multi-agent surface. No exception handling. Single-file body
flows top-to-bottom inside the closure, entirely synchronous.

## 2. Dart → C#/.NET Conversion Plan

Each Dart construct below maps to its C#/.NET counterpart per the
ratified convspec. All nine constructs are KB cache hits (FR-012 /
SC-007) — REUSE, do not re-derive.

- **dart.package_test.import_directive** (`import 'package:test/test.dart';`)
  → Drop the Dart directive; emit file-level `using Xunit;` plus
  `using Xunit.Abstractions;` (because the body calls `print(...)`,
  which routes to `ITestOutputHelper.WriteLine`). xUnit framework
  choice is the batch-wide default settled in `test/smoke_test.dart.md`
  and reused across every prior test convspec — REUSED here.
  Idiom: `rf-dart-package-test-to-dotnet-xunit`.

- **dart.internal_package_import.same_package_single**
  (`import 'package:glp_runtime/compiler/compiler.dart';`)
  → Emit one file-level `using <RootNs>.Compiler;` to bring
  `GlpCompiler` (and the indirectly-referenced `BytecodeProgram` /
  `Op` element type per `lib/compiler/codegen.dart.md`) into scope.
  `<RootNs>` is the langpair-level root namespace (out-of-scope for
  this per-file artefact). Idiom:
  `rf-dart-internal-package-import-to-csharp-using`.

- **dart.test_file.void_main_with_single_test_call_no_group**
  (`void main() { test('Inspect bytecode for r(a,[b])', () { ... }); }`)
  → Drop `void main()`. Emit `public class InspectBytecodeTest`
  (file-name `inspect_bytecode_test.dart` → `InspectBytecodeTest.cs`
  → class `InspectBytecodeTest`, non-sealed, public, instance — xUnit
  reflection-driven discovery instantiates a fresh instance per
  `[Fact]`). Emit ONE `[Fact(DisplayName = "Inspect bytecode for
  r(a,[b])")] public void InspectBytecodeForRAB()` instance method
  whose body is the verbatim translation of the single Dart `test()`
  closure body. Display-name attribute is required because the
  original Dart string `'Inspect bytecode for r(a,[b])'` contains
  `(`, `,`, `[`, `]` punctuation that is not valid in a C# method
  identifier (decision shared with sibling convspecs
  `test/conformance/fairness_26_test.dart.md` and
  `test/bytecode/utility_instructions_test.dart.md`). Idiom:
  `rf-dart-test-main-to-xunit-class-with-facts`.

- **dart.local_var.final_typed_via_constructor_call**
  (`final compiler = ...; final prog = ...; final op = ...;`)
  → Three `var`-typed local-variable declarations:
  `var compiler = new GlpCompiler();`,
  `var prog = compiler.Compile("r(a,[b]).");`,
  `var op = prog.Ops[i];`. Dart `final` on a local + initializer-driven
  type-inference → C# `var` (compiler-inferred static type; the source
  does not reassign any of these three). Idiom:
  `rf-dart-final-local-to-csharp-var`.

- **dart.method_invocation.glp_compiler_compile_source_string**
  (`compiler.compile('r(a,[b]).')`)
  → `compiler.Compile("r(a,[b]).")`. camelCase → PascalCase rename
  per `lib/compiler/compiler.dart.md`. Dart single-quoted literal →
  C# double-quoted literal verbatim (no `"`, `\`, `$`, or `{` chars in
  the source string require escaping). The optional positional
  `CompileOptions?` parameter on the SUT signature is defaulted —
  the call omits it; C# default-argument `= null` is the equivalent
  shape. Idiom: `rf-dart-method-camelcase-to-csharp-pascal-call`.

- **dart.member_access.bytecode_program_ops_property_and_indexer**
  (`prog.ops.length`, `prog.ops[i]`)
  → `prog.Ops.Count` and `prog.Ops[i]`. camelCase property →
  PascalCase property (`ops` → `Ops`); `.length` getter on a Dart
  `List<...>` → `.Count` property on the C# generic collection
  (`IReadOnlyList<Op>` / `List<Op>` per `lib/compiler/codegen.dart.md`
  — NOT `.Length`, which is array/string only). Indexer syntax `[i]`
  is identical in both languages on `List<T>` / `IReadOnlyList<T>`.
  Idiom: `rf-dart-list-indexing-to-csharp-list-indexer`.

- **dart.statement.classical_for_loop_int_index**
  (`for (int i = 0; i < prog.ops.length; i++) { ... }`)
  → Verbatim shape: `for (int i = 0; i < prog.Ops.Count; i++) { ... }`.
  Three-clause C-style `for` syntax is identical in Dart and C# —
  no `foreach` rewrite because this is an index-based loop.
  `prog.Ops.Count` is re-evaluated each iteration; integer arithmetic
  on small `int` indices is identical in both languages. Idiom:
  `rf-dart-c-style-for-loop-to-csharp-verbatim`.

- **dart.core.print** (two `print(...)` sites)
  → Two `_output.WriteLine(...)` calls. Add a private field
  `private readonly ITestOutputHelper _output;` to the test class
  plus a constructor `public InspectBytecodeTest(ITestOutputHelper
  output) { _output = output; }`. xUnit's `ITestOutputHelper` is the
  framework-sanctioned per-test output channel (xunit.net "Capturing
  Output"); `Console.WriteLine` is explicitly forbidden because xUnit
  parallelizes tests and would interleave shared `Console` output.
  Site 1: `_output.WriteLine("\n=== r(a,[b]). DETAILED ===");`
  (leading `\n` preserved inside the literal so the runner emits the
  pre-header blank line — `WriteLine` itself appends a trailing
  newline). Site 2: see next construct row. Idiom:
  `rf-dart-print-to-xunit-itestoutputhelper-writeline`.

- **dart.string_interpolation.with_member_access_and_runtimetype**
  (`'  $i: $op (${op.runtimeType})'`)
  → C# interpolated string `$"  {i}: {op} ({op.GetType().Name})"`.
  Bare-identifier slots `$i`, `$op` → `{i}`, `{op}` (compiler-driven
  `ToString()` of the int / `Op` value, identical to Dart's implicit
  `.toString()` inside `$...`). Member-access slot
  `${op.runtimeType}` → `{op.GetType().Name}` because Dart
  `Object.runtimeType.toString()` returns the UNQUALIFIED class name
  and the matching .NET property is `System.Type.Name` (NOT
  `FullName`, NOT `AssemblyQualifiedName`) — settled in
  `lib/lint/linter.dart.md`. Primary idiom:
  `rf-dart-string-interpolation-to-csharp-dollar-string`; secondary
  idiom: `rf-dart-runtimetype-in-interpolation-to-csharp-gettype-name`.

## 3. Decomposed Task Units

- T1 — Emit file-level `using` directives: `using Xunit;`, `using
  Xunit.Abstractions;`, `using <RootNs>.Compiler;` (and any transitively-
  needed `using` for the `Op` element type per
  `lib/compiler/codegen.dart.md` — langpair-level concern).
- T2 — Emit top-level test class `public class InspectBytecodeTest`
  (non-sealed, public; file-name → class-name `inspect_bytecode_test.
  dart` → `InspectBytecodeTest.cs`).
- T3 — Emit `private readonly ITestOutputHelper _output;` field + the
  constructor `public InspectBytecodeTest(ITestOutputHelper output)
  { _output = output; }`.
- T4 — Emit one `[Fact(DisplayName = "Inspect bytecode for r(a,[b])")]
  public void InspectBytecodeForRAB()` instance method.
- T5 — Method-body unit 1: `var compiler = new GlpCompiler();`.
- T6 — Method-body unit 2: `_output.WriteLine("\n=== r(a,[b]). DETAILED
  ===");`.
- T7 — Method-body unit 3: `var prog = compiler.Compile("r(a,[b]).");`.
- T8 — Method-body unit 4: `for (int i = 0; i < prog.Ops.Count; i++) {
  var op = prog.Ops[i]; _output.WriteLine($"  {i}: {op} ({op.GetType().
  Name})"); }`.

## 4. Research Findings

none required — every construct row is a KB cache hit (FR-012 / SC-007
REUSE; FR-024 offline-reproducible). The convspec records nine idiom
rows, all marked REUSED, with authoritative-source provenance preserved
inline. No fresh research is needed for this artefact.

## 5. Consistency Pass

fixed — derived from the ratified convspec at
`.codeconv/conversion-specs/test/bytecode/inspect_bytecode_test.dart.md`
(every construct, target decision, idiom_id and nuance is mirrored
verbatim), the SUT specs `lib/compiler/compiler.dart.md` and
`lib/compiler/codegen.dart.md` (`Compile` signature, `Ops` collection
shape, camelCase → PascalCase rename rule), `lib/lint/linter.dart.md`
(`runtimeType` → `GetType().Name` decision), the batch-wide test-
convspec siblings (`test/smoke_test.dart.md`,
`test/glp_runtime_test.dart.md`,
`test/test_channel_construction.dart.md`,
`test/bytecode/utility_instructions_test.dart.md`,
`test/bytecode/fairness_scheduler_loop_test.dart.md`,
`test/conformance/fairness_26_test.dart.md`,
`test/heap/suspension_pointer_test.dart.md`), and `CLAUDE.md` (project-
wide naming + conversion conventions). No conflicts; convspec records
zero escalations and this plan preserves that count.

## 6. Escalations

None.
