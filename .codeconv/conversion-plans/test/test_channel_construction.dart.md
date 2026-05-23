---
path: test/test_channel_construction.dart
cycle_group_id: 161
scc_siblings: []
generated_at: 2026-05-21T16:14:15Z
source_sha256: d74fde5cacb1398422070b6ca4a11ad7325c200fef17c0d9b2f7d76a96fc8b90
schema_version: 1
---

# Conversion Plan: test/test_channel_construction.dart

## 1. Source Analysis

Inspection of `glp_runtime_net/test/test_channel_construction.dart` (43 lines, sha256 `d74fde5c…6fc8b90`) reveals:

- **Import directives (4):**
  - `package:test/test.dart` — Dart test framework (top of file).
  - `package:glp_runtime/runtime/heap_fcp.dart` — SUT for `HeapFCP`.
  - `package:glp_runtime/runtime/external_io.dart` — SUT for `createExternalChannel`, `buildChannelTerm`.
  - `package:glp_runtime/runtime/terms.dart` — SUT for `StructTerm`, `VarRef`.
- **Entrypoint:** `void main()` containing a single `test(...)` call, NO `group(...)` wrapper.
- **Test (1):** `test('buildChannelTerm creates ch(Reader, Writer)', () { ... })` — synchronous closure body, no `skip:`, no `async`.
- **Local declarations (6) — all `final`:**
  - `heap = HeapFCP();` (constructor invocation, no args).
  - `userChannel = createExternalChannel(heap, 'user');` (top-level function call, single-quoted string literal).
  - `term = buildChannelTerm(userChannel);` (top-level function call).
  - `st = term as StructTerm;` (downcast).
  - `arg0 = st.args[0] as VarRef;` (list indexer + downcast).
  - `arg1 = st.args[1] as VarRef;` (list indexer + downcast).
- **`print(...)` calls (9):** all with interpolated strings; two use `'\n…'` escapes; one uses the literal `✓` glyph (U+2713).
- **Field/property reads (8):** `userChannel.inputWriterAddr`, `userChannel.inputReaderAddr`, `userChannel.outputWriterAddr`, `userChannel.outputReaderAddr`, `st.functor`, `st.args` (twice), `arg0.addr`, `arg1.addr`.
- **List `.length` read (1):** `st.args.length` (in an `expect`).
- **Method invocations returning `bool` (4):** `heap.isReader(arg0.addr)` (twice — once in `print`, once in `expect`), `heap.isReader(arg1.addr)` (twice).
- **`expect(...)` assertions (8):**
  - 3× `expect(<expr>, isA<T>())` (one for `StructTerm`, two for `VarRef`); each is IMMEDIATELY followed by the matching `as`-cast on the same `<expr>` — eligible for the IsType-return fold.
  - 1× `expect(st.functor, equals('ch'))`.
  - 1× `expect(st.args.length, equals(2))`.
  - 1× `expect(heap.isReader(arg0.addr), isTrue, reason: 'First arg should be reader')`.
  - 1× `expect(arg0.addr, equals(userChannel.inputReaderAddr))`.
  - 1× `expect(heap.isReader(arg1.addr), isFalse, reason: 'Second arg should be writer')`.
  - 1× `expect(arg1.addr, equals(userChannel.outputWriterAddr))`.

No `setUp` / `tearDown` / `group` / `async` / `await` / `Future` / `Stream` constructs. No exception expectations. No closure captures beyond method locals.

## 2. Dart → C#/.NET Conversion Plan

Each construct below is recorded VERBATIM from the ratified convspec (`.codeconv/conversion-specs/test/test_channel_construction.dart.md`) — this plan mirrors that source-of-truth.

- **`dart.package_test.import_directive`** (`import 'package:test/test.dart';`) → `using Xunit;` at file scope + `using System;` baseline. Project to a single namespace mirroring the Dart `test/` directory (e.g. `<RootNs>.Test`). idiom `rf-dart-package-test-import-to-xunit-using` (KB cache hit, REUSE).

- **`dart.package_under_test.import_directive`** (3 × `import 'package:glp_runtime/runtime/<x>.dart';`) → one `using <Ns>;` per imported SUT library (collapsed to a single `using` if the three SUT files end up in the same namespace). Exact namespace strings are decided by the owning SUT convspecs (`heap_fcp.dart.md`, `external_io.dart.md`, `terms.dart.md`). idiom `rf-dart-internal-package-import-to-csharp-using` (KB cache hit, REUSE).

- **`dart.package_test.main_entrypoint`** (`void main() { test(...); }`) → ELIMINATE `main`. xUnit discovers `[Fact]` methods by reflection; the single inner `test(...)` becomes a single `[Fact]` method on the enclosing class. idiom `rf-dart-package-test-main-omit-in-xunit` (KB cache hit, REUSE).

- **`dart.package_test.test_file_no_group`** (no `group(...)` wrapper) → emit ONE PascalCase test class `TestChannelConstructionTests`, no `[Trait]`. The Dart `test()` label survives via `[Fact(DisplayName = "buildChannelTerm creates ch(Reader, Writer)")]`. research-finding `rf-dart-package-test-no-group-to-xunit-class-per-file` (new first-class no-group degenerate case).

- **`dart.package_test.test_call_simple`** (single synchronous `test(label, body)` callback) → `[Fact(DisplayName = "buildChannelTerm creates ch(Reader, Writer)")] public void BuildChannelTermCreatesChReaderWriter()` — synchronous (no `async Task` because the closure is synchronous). Closure body translates statement-for-statement. idiom `rf-dart-test-callback-to-xunit-method-body` (KB cache hit, REUSE).

- **`dart.final_local`** (6 occurrences) → `var <name> = <expr>;`. No re-assignment in the method body, so `var` is observably equivalent to Dart `final` (no `readonly` keyword exists for method-locals in C#). idiom `rf-dart-final-local-to-csharp-var-local` (KB cache hit, REUSE).

- **`dart.constructor_invocation_no_args`** (`HeapFCP()`) → `new HeapFCP()` (C# pre-9 form on RHS of `var` since target type is inferred). idiom `rf-dart-constructor-invocation-implicit-new-to-csharp-new`.

- **`dart.function_call_top_level`** (`createExternalChannel(heap, 'user')`, `buildChannelTerm(userChannel)`) → `ExternalIo.CreateExternalChannel(heap, "user")` and `ExternalIo.BuildChannelTerm(userChannel)` (host class name decided by `external_io.dart.md`, applied verbatim). idiom `rf-dart-top-level-function-callsite-to-csharp-static-method`.

- **`dart.string.single_quoted_literal`** (`'user'`) → `"user"`. No raw/verbatim treatment (no escapes, no `"`). idiom `rf-dart-single-quoted-string-to-csharp-double-quoted-string`.

- **`dart.core.print`** (9 occurrences) → constructor-injected `ITestOutputHelper output` stored in private `_output` field; every `print(...)` becomes `_output.WriteLine(...)`. `Console.WriteLine` is the inferior fallback (xUnit does NOT capture `Console.Out`). research-finding `rf-dart-print-to-xunit-itestoutputhelper-writeline`.

- **`dart.string.interpolation`** (`$name` / `${expr}`) → C# interpolated string `$"...{name}...{expr}..."`; field-reference camelCase identifiers RE-EMITTED as PascalCase per SUT convspecs (e.g. `userChannel.InputWriterAddr`, `heap.IsReader(arg0.Addr)`). research-finding `rf-dart-string-interpolation-to-csharp-interpolated-string`.

- **`dart.downcast.as_expression`** (3 occurrences: `term as StructTerm`, `st.args[0] as VarRef`, `st.args[1] as VarRef`) → explicit C# cast `(T)expr` (throws `InvalidCastException` on mismatch — direct semantic match for Dart `as`'s `TypeError`). However each `as`-cast in this file is paired with an `expect(<expr>, isA<T>())` on the SAME `<expr>` — the fold below SUPERSEDES the standalone explicit cast for all three pairs. idiom `rf-dart-as-cast-to-csharp-explicit-cast` (KB cache hit, REUSE; folded here).

- **`dart.package_test.expect_isA_T`** (3 occurrences) → standalone form would be `Assert.IsType<T>(<expr>)`; subsumed by the fold below for all three callsites because each is immediately followed by an `as`-cast on the same expression. idiom `rf-dart-expect-isA-to-xunit-assert-istype` (KB cache hit, REUSE; folded here).

- **`dart.package_test.expect_isA_T_with_as_fold`** (3 occurrences) → SINGLE emission per pair: `var st = Assert.IsType<StructTerm>(term);`, `var arg0 = Assert.IsType<VarRef>(st.Args[0]);`, `var arg1 = Assert.IsType<VarRef>(st.Args[1]);`. `Assert.IsType<T>(actual)` both asserts AND returns the value cast to T. Fold is referentially safe (all three `<expr>` are pure field/indexer reads on already-bound locals). research-finding `rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`.

- **`dart.package_test.expect_equals`** (4 occurrences) → `Assert.Equal(<expected>, <actual>)` with argument-order flip applied:
  - `Assert.Equal("ch", st.Functor);`
  - `Assert.Equal(2, st.Args.Count);`
  - `Assert.Equal(userChannel.InputReaderAddr, arg0.Addr);`
  - `Assert.Equal(userChannel.OutputWriterAddr, arg1.Addr);`
  idiom `rf-dart-expect-equals-to-xunit-assertequal` (KB cache hit, REUSE).

- **`dart.list.length`** (`st.args.length`) → `st.Args.Count`. Disambiguation rule: `List<T>.length` → `.Count`; `String.length` → `.Length`. Here `st.args` is `List<Term>` per `terms.dart.md`. idiom `rf-dart-list-length-to-csharp-list-count`.

- **`dart.list.indexer`** (`st.args[0]`, `st.args[1]`) → `st.Args[0]`, `st.Args[1]` (direct syntactic correspondence). idiom `rf-dart-list-indexing-to-csharp-list-indexer` (KB cache hit, REUSE).

- **`dart.method_invocation.bool_return`** (`heap.isReader(arg0.addr)`, `heap.isReader(arg1.addr)`) → `heap.IsReader(arg0.Addr)`, `heap.IsReader(arg1.Addr)` (PascalCase rename per `heap_fcp.dart.md`). Return type `bool` in both languages. idiom `rf-dart-instance-method-call-camelcase-to-csharp-pascalcase`.

- **`dart.package_test.expect_isTrue_with_reason`** (`expect(heap.isReader(arg0.addr), isTrue, reason: 'First arg should be reader')`) → `Assert.True(heap.IsReader(arg0.Addr), "First arg should be reader");` (xUnit overload `Assert.True(bool, string userMessage)`). idiom `rf-dart-expect-isTrue-to-xunit-assert-true` (KB cache hit, REUSE) + userMessage addendum.

- **`dart.package_test.expect_isFalse_with_reason`** (`expect(heap.isReader(arg1.addr), isFalse, reason: 'Second arg should be writer')`) → `Assert.False(heap.IsReader(arg1.Addr), "Second arg should be writer");` (xUnit overload `Assert.False(bool, string userMessage)`). idiom `rf-dart-expect-isFalse-to-xunit-assert-false` (KB cache hit, REUSE) + userMessage addendum.

- **`dart.member_access.field`** (8 occurrences) → PascalCase property access per owning SUT convspecs (`ExternalChannel.{InputWriterAddr, InputReaderAddr, OutputWriterAddr, OutputReaderAddr}`, `StructTerm.{Functor, Args}`, `VarRef.Addr`). Field-vs-property choice (public field vs auto-property) is the SUT convspec's responsibility; read-side syntax is identical. idiom `rf-dart-camelcase-field-to-csharp-pascalcase-property`.

## 3. Decomposed Task Units

- **T1:** cu-1 — emit file-scope `using Xunit;` + `using System;` + `using <Ns>;` lines for the three SUT namespaces decided by `heap_fcp.dart.md`, `external_io.dart.md`, `terms.dart.md` (collapsed to fewer `using` lines if shared namespace).
- **T2:** cu-2 — emit `namespace <RootNs>.Test;` declaration (file-scoped or block-scoped) mirroring the Dart `test/` directory; no group nesting.
- **T3:** cu-3 — emit `public class TestChannelConstructionTests` with `private readonly ITestOutputHelper _output;` field and constructor `public TestChannelConstructionTests(ITestOutputHelper output) { _output = output; }`.
- **T4:** cu-4 — emit single `[Fact(DisplayName = "buildChannelTerm creates ch(Reader, Writer)")] public void BuildChannelTermCreatesChReaderWriter()` method shell.
- **T5:** cu-5 — emit method body prologue: `var heap = new HeapFCP();`, `var userChannel = ExternalIo.CreateExternalChannel(heap, "user");`, five `_output.WriteLine($"...");` diagnostic lines (`"User channel: {userChannel}"`, four `"  <pascal>Addr: {userChannel.<Pascal>Addr}"` lines), then `var term = ExternalIo.BuildChannelTerm(userChannel);`, then `_output.WriteLine($"\nChannel term: {term}");`.
- **T6:** cu-6 — emit folded `var st = Assert.IsType<StructTerm>(term);` (single statement supersedes the `expect_isA_T` + `as`-cast pair for `term`/`StructTerm`).
- **T7:** cu-7 — emit two `Assert.Equal` lines with argument-order flip: `Assert.Equal("ch", st.Functor);` and `Assert.Equal(2, st.Args.Count);`.
- **T8:** cu-8 — emit folded `var arg0 = Assert.IsType<VarRef>(st.Args[0]);`, one `_output.WriteLine($"  arg[0]: VarRef({arg0.Addr}) isReader={heap.IsReader(arg0.Addr)}");`, `Assert.True(heap.IsReader(arg0.Addr), "First arg should be reader");`, `Assert.Equal(userChannel.InputReaderAddr, arg0.Addr);`.
- **T9:** cu-9 — emit folded `var arg1 = Assert.IsType<VarRef>(st.Args[1]);`, one `_output.WriteLine($"  arg[1]: VarRef({arg1.Addr}) isReader={heap.IsReader(arg1.Addr)}");`, `Assert.False(heap.IsReader(arg1.Addr), "Second arg should be writer");`, `Assert.Equal(userChannel.OutputWriterAddr, arg1.Addr);`.
- **T10:** cu-10 — emit final `_output.WriteLine($"\n✓ Channel term is ch(Reader, Writer) as expected");` diagnostic line.

## 4. Research Findings

none required (all idioms KB cache hits or first-class new idioms recorded by the convspec; FR-012 / SC-007 satisfied — no re-research per FR-024). Authoritative bases recorded in the convspec: xUnit v3 docs (`https://xunit.net/docs/getting-started/v3/getting-started`, `https://xunit.net/docs/capturing-output`, `https://xunit.net/docs/comparisons`), `Assert.True`/`Assert.False` `userMessage` overloads (`https://learn.microsoft.com/dotnet/api/xunit.assert.true`), and `package:test` README (`https://pub.dev/packages/test`).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/test_channel_construction.dart.md` (RATIFIED). Every construct in §2 mirrors the convspec verbatim (decision + idiom_id + nuance); §3 task units mirror the convspec's `conversion_units` cu-1…cu-10 1:1; §1 source analysis is grounded in actual `.dart` inspection (verified line-by-line against the 43-line source). Cross-file SUT identifier choices are delegated (verbatim) to the owning SUT convspecs (`heap_fcp.dart.md`, `external_io.dart.md`, `terms.dart.md`) — no override here. Convspec records `escalations: []` and `open_escalation_count: 0` in the tombstone — no decision is left unresolved.

## 6. Escalations

None.
