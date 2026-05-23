---
path: test/heap/arithmetic_pointer_test.dart
cycle_group_id: 126
scc_siblings: []
generated_at: 2026-05-21T16:40:09Z
source_sha256: 3c0766cd3af29d5ad4f05a498adc3d91c60ee5dddd6562415d53c34402d01404
schema_version: 1
---

# Conversion Plan: test/heap/arithmetic_pointer_test.dart

## 1. Source Analysis

Source file `glp_runtime_net/test/heap/arithmetic_pointer_test.dart` (358 lines, sha256 `3c0766cd...01404`) is a Dart `package:test` unit-test suite covering arithmetic body kernels under the heap-pointer-architecture (`docs/heap-pointer-architecture-spec.md v3.0`). It is the heap-pointer-architecture sibling of the legacy bytecode-family `test/bytecode/arithmetic_test.dart` (load-bearing provenance per the file lead doc-comment lines 1-8).

Structural inventory of the actual source:

- Lines 1-8: file lead doc-comment (4 prose lines + provenance citation + spec citation + rationale) + bare `library;` directive.
- Lines 10-18: 8 imports — `package:test/test.dart`; six `package:glp_runtime/...` SUT imports (`compiler/compiler.dart`, `bytecode/runner.dart`, `runtime/runtime.dart`, `runtime/machine_state.dart`, `runtime/terms.dart`, `runtime/body_kernels.dart`, `runtime/scheduler.dart`); `dart:io`.
- Lines 20-29: `void main()` body with a single `late BytecodeProgram stdlibProg;` file-scoped variable + a `setUpAll(() { ... })` hook that reads `'../programs/self.glp'`, compiles via `GlpCompiler()`, and prints the op count.
- Lines 31-223: top-level `group('Arithmetic via := system predicate - Pointer Architecture', () { ... })` containing 8 `test()` calls (`add/3`, `sub/3`, `mul/3`, `div/3`, `div/3 abort`, `neg/2`, `sqrt_kernel/2`, `all standard body kernels are registered`).
- Lines 225-323: top-level `group('End-to-end := system predicate - Pointer Architecture', () { ... })` containing 3 `test()` calls (`assign.glp compiles and merges correctly`, `user program with := compiles correctly with SRSW`, `Z := 5 + 3 executes and binds Z to 8`).
- Lines 325-356: top-level `group('Variable Chain Dereferencing', () { ... })` containing 1 `test()` call (`arithmetic through variable chain`).

Total: 3 sibling top-level groups, 12 `test()` calls, 1 `setUpAll` hook, 1 `late` file-scoped variable. No nested groups, no `setUp` (only `setUpAll`), no `tearDown*`, no `skip:` / `timeout:` / `retry:` / `tags:` arguments.

Constructs actually used (verbatim source surface):
- `final (xWriter, xReader) = rt.heap.allocateVariable();` — positional record destructuring, 14 occurrences across tests (some with `_` discard in the second slot).
- `rt.heap.bindWriter(xWriter, ConstTerm(<int>));` — 12 occurrences (literals: 5, 3, 10, 4, 7, 6, 15, 0, 42, 16, 5, 3).
- `rt.heap.bindWriterToReader(xWriter, yReader);` — 1 occurrence (the variable-chain test).
- `VarRef(<addr>)` — kernel argument constructors across all kernel tests.
- `rt.bodyKernels.lookup('_<name>', <arity>);` — 7 occurrences (`_add`, `_sub`, `_mul`, `_div`, `_div` again for abort, `_neg`, `_sqrt`).
- `kernel!(rt, [...])` — 8 delegate invocations (including variable-chain).
- `rt.bodyKernels.has('_<name>', <arity>);` — 24 occurrences inside `all standard body kernels are registered`.
- `expect(result, equals(BodyKernelResult.success));` — 7 occurrences; `expect(result, equals(BodyKernelResult.abort));` — 1.
- `expect(kernel, isNotNull, reason: '...');` — 1; `expect(kernel, isNotNull);` — 1 (sub/3 only); other tests skip the `expect(kernel, isNotNull)` entirely.
- `expect(value, isNotNull); expect(value, isA<ConstTerm>()); expect((value as ConstTerm).value, equals(<n>));` — full triple in `add/3`; shorter `expect((value as ConstTerm).value, equals(<n>));` in 6 other tests.
- `expect(rt.bodyKernels.has(...), isTrue);` — 24 times in the registered-kernels test.
- `expect(prog.ops.isNotEmpty, isTrue);` — 1; `expect(prog.labels.containsKey('<k>'), isTrue);` — 2 with `reason:` + 2 without.
- `final stdlibSource = File('../programs/self.glp').readAsStringSync();` — 3 sites (in `setUpAll` and in 2 of the End-to-end tests, which re-read).
- Triple-quoted GLP source literals: `'''hello.'''` (1) and `'''compute_sum(Z?) :- Z := 5 + 3.'''` (2).
- `userProg.merge(stdlibProg);` — 2 sites.
- `final mergedProg.labels['compute_sum/1']` indexer — 1 site (Z-end-to-end).
- `BytecodeRunner(mergedProg);` `Scheduler(rt: rt, runner: runner);` `CallEnv(args: {0: VarRef(resultWriter)});` `rt.setGoalEnv(goalId, env);` `rt.gq.enqueue(GoalRef(goalId, entryPc!));` `sched.drain(maxCycles: 100, debug: true, debugOutput: true);` `rt.heap.isFullyBound(resultWriter);` `if (value is ConstTerm) { ... }` `fail('...');` — Z-end-to-end test only.
- 14 `print(...)` callsites with `${...}` and `$var` interpolation; the UTF-8 glyph `✓` appears in one print literal.

Dependencies (from tombstone): `lib/bytecode/runner.dart`, `lib/compiler/compiler.dart`, `lib/runtime/body_kernels.dart`, `lib/runtime/machine_state.dart`, `lib/runtime/runtime.dart`, `lib/runtime/scheduler.dart`, `lib/runtime/terms.dart`. No callers (test file).

## 2. Dart → C#/.NET Conversion Plan

The conversion mirrors the ratified convspec verbatim. Each construct maps to a single canonical C# shape:

1. **`library;` directive** → eliminated; file-scoped `namespace <RootNs>.Test.Heap;` mirrors the `test/heap` directory shape; the 4-line file lead doc-comment + provenance citation + spec citation + rationale are lifted onto the outer test-container class `ArithmeticPointerTest` as XML doc-comment.
2. **`import 'package:test/test.dart';`** → `using Xunit;`.
3. **`package:glp_runtime/...` imports (7 total)** → collapse to `using <RootNs>.Compiler;` + `using <RootNs>.Bytecode;` + `using <RootNs>.Runtime;` (five runtime/*.dart files collapse to one `using` because they share the Runtime namespace).
4. **`import 'dart:io';`** → `using System.IO;`.
5. **Auxiliary `using`s**: `using Xunit.Abstractions;` (for `ITestOutputHelper`); `using System.Collections.Generic;` (for `Dictionary<int, VarRef>`); `using System.Linq;` (for `.ToList()` on `Dictionary.Keys`).
6. **`void main() { ... }`** → eliminated; xUnit discovers `[Fact]`s by reflection.
7. **`late BytecodeProgram stdlibProg;`** → eliminated as a file-level variable; replaced by `public BytecodeProgram StdlibProg { get; }` get-only auto-property on `public class StdlibProgFixture`.
8. **`setUpAll(() { ... })`** → fixture ctor `public StdlibProgFixture()` that reads `Path.Combine(AppContext.BaseDirectory, "..", "programs", "self.glp")` via `File.ReadAllText`, compiles via `new GlpCompiler().Compile(...)`, assigns the auto-property; diagnostic `print(...)` omitted (non-load-bearing).
9. **Three sibling top-level `group()` calls + shared `setUpAll`** → three nested `public class` declarations inside the outer `ArithmeticPointerTest`, each tagged `[Collection("ArithmeticPreludePointer")]`, each ctor injecting `(StdlibProgFixture fixture, ITestOutputHelper output)`. The fixture is shared via `[CollectionDefinition("ArithmeticPreludePointer")] public class ArithmeticPreludePointerCollection : ICollectionFixture<StdlibProgFixture> { }`. Class names: `ArithmeticViaAssignSystemPredicatePointerArchitecture`, `EndToEndAssignSystemPredicatePointerArchitecture`, `VariableChainDereferencing`. Each carries `[Trait("Group", "<original Dart label>")]` to preserve the verbatim label.
10. **`test('<label>', () { ... })`** → `[Fact(DisplayName = "<original Dart label>")] public void <PascalCasedIdentifier>() { ... }`. Method-identifier mangling per convspec (12 total): `Add3BodyKernelExecutesDirectly`, `Sub3BodyKernel`, `Mul3BodyKernel`, `Div3BodyKernel`, `Div3BodyKernelAbortsOnDivisionByZero`, `Neg2BodyKernel`, `SqrtKernel2BodyKernel`, `AllStandardBodyKernelsAreRegistered`, `AssignGlpCompilesAndMergesCorrectly`, `UserProgramWithAssignCompilesCorrectlyWithSRSW`, `ZAssign5Plus3ExecutesAndBindsZTo8`, `ArithmeticThroughVariableChain`.
11. **`final <name> = <Ctor>(<args>);`** → `var <name> = new <Ctor>(<args>);`. Named-arg `Scheduler(rt: rt, runner: runner)` → `new Scheduler(rt: rt, runner: runner)` (colon-form).
12. **`final (a, b) = rt.heap.allocateVariable();`** → `var (a, b) = rt.Heap.AllocateVariable();`. `_` discard preserved verbatim.
13. **`rt.heap.bindWriter(<addr>, ConstTerm(<lit>));`** → `rt.Heap.BindWriter(<addr>, new ConstTerm(<lit>));`. Return `List<SuspensionRecord>` discarded.
14. **`ConstTerm(<int-lit>)`** → `new ConstTerm(<int-lit>)`. Payload boxes into `object?`.
15. **`VarRef(<addr>)`** → `new VarRef(<addr>)`.
16. **`[ref1, ref2, ref3]`** (kernel argument list) → `new[] { ref1, ref2, ref3 }`.
17. **`rt.bodyKernels.lookup('_<name>', <arity>)`** → `rt.BodyKernels.Lookup("_<name>", <arity>)`.
18. **`expect(kernel, isNotNull, reason: '<msg>')`** → `Assert.NotNull(kernel); // <msg>` (no user-message overload on `Assert.NotNull`); `expect(kernel, isNotNull)` → `Assert.NotNull(kernel);`.
19. **`kernel!(rt, [...])`** → `kernel!(rt, new[] { ... })`. Dart `!` runtime-throw is faithfully reproduced via the preceding `Assert.NotNull(kernel)` plus the C# `!` null-forgiving operator (composition documented in `rf-dart-bang-null-assertion-to-csharp-null-forgiving`).
20. **`expect(result, equals(BodyKernelResult.success))`** → `Assert.Equal(BodyKernelResult.Success, result);` (argument-order swap; enum-member PascalCase). Same for `.abort` → `.Abort`.
21. **`final value = rt.heap.getValue(resultWriter);`** → `var value = rt.Heap.GetValue(resultWriter);` returning `Term?`.
22. **`expect(value, isNotNull); expect(value, isA<ConstTerm>()); expect((value as ConstTerm).value, equals(<n>));`** → collapse to `Assert.NotNull(value); var constValue = Assert.IsType<ConstTerm>(value); Assert.Equal(<n>, constValue.Value);` (the `IsType<T>` typed-return eliminates the explicit cast). The shorter Dart form `expect((value as ConstTerm).value, equals(<n>))` → `var constValue = Assert.IsType<ConstTerm>(value); Assert.Equal(<n>, constValue.Value);` (the `IsType` throw-on-mismatch subsumes the missing `isNotNull` + `isA` checks).
23. **`rt.bodyKernels.has('_<name>', <arity>)`** → `rt.BodyKernels.Has("_<name>", <arity>)`.
24. **`expect(<bool>, isTrue)`** → `Assert.True(<bool>);`; with `reason:` → `Assert.True(<bool>, "<msg>");` (`Assert.True` HAS a user-message overload).
25. **`prog.ops.isNotEmpty`** → `prog.Ops.Count > 0`.
26. **`prog.labels.containsKey('<k>')`** → `prog.Labels.ContainsKey("<k>")`.
27. **`userProg.merge(stdlibProg)`** → `userProg.Merge(stdlibProg)`; immutable merge returning a new `BytecodeProgram`.
28. **`mergedProg.labels['<k>']`** → `mergedProg.Labels["<k>"]`; C# indexer throws `KeyNotFoundException` on miss (matches Dart `!`-assert intent); the preceding `Assert.True(Labels.ContainsKey(...))` gate guarantees the key exists.
29. **`rt.gq.enqueue(GoalRef(goalId, entryPc!))`** → `rt.Gq.Enqueue(new GoalRef(goalId, entryPc!));`. `GoalRef` is `readonly record struct`.
30. **`sched.drain(maxCycles: 100, debug: true, debugOutput: true)`** → `sched.Drain(maxCycles: 100, debug: true, debugOutput: true);` (named-args verbatim).
31. **`CallEnv(args: {0: VarRef(resultWriter)})`** → `new CallEnv(args: new Dictionary<int, VarRef> { [0] = new VarRef(resultWriter) });` (index-initialiser).
32. **`rt.setGoalEnv(goalId, env)`** → `rt.SetGoalEnv(goalId, env);`.
33. **`final goalId = 1;`** → `var goalId = 1;` (typedef-alias `GoalId` per machine_state.dart.md).
34. **`rt.heap.isFullyBound(resultWriter)`** → `rt.Heap.IsFullyBound(resultWriter);` returning `bool`.
35. **`print('...${expr}...')` and `'...$var...'`** → `_output.WriteLine($"...{expr}...");` and `_output.WriteLine($"...{var}...");`. UTF-8 glyph `✓` survives verbatim. `\n` escape identical.
36. **`if (value is ConstTerm) { ... value.value ... }`** → `if (value is ConstTerm constValue) { ... constValue.Value ... }` (C# 7+ type pattern combining test + binding).
37. **`fail('<msg>')`** → `Assert.Fail("<msg>");`.
38. **`'''<text>'''` triple-quoted GLP source literal** → `"""<text>"""` (C# 11+ raw string literal; pre-C#11 fallback is `@"<text>"`).
39. **`File('<rel-path>').readAsStringSync()`** → `File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "programs", "self.glp"))` (CWD-faithful relative-path routing).
40. **`stdlibCompiler.compile(stdlibSource)` / `userCompiler.compile(userSource)`** → `stdlibCompiler.Compile(stdlibSource)` / `userCompiler.Compile(userSource)` returning `BytecodeProgram`.

## 3. Decomposed Task Units

- T1: Emit file header (file-scoped `namespace <RootNs>.Test.Heap;` + 9 `using` directives).
- T2: Emit `public class StdlibProgFixture` with get-only auto-property and ctor reading + compiling the prelude.
- T3: Emit `[CollectionDefinition("ArithmeticPreludePointer")] public class ArithmeticPreludePointerCollection : ICollectionFixture<StdlibProgFixture> { }`.
- T4: Emit outer `public class ArithmeticPointerTest { ... }` with XML doc-comment lifted from the Dart file lead.
- T5: Emit nested `[Collection("ArithmeticPreludePointer")] public class ArithmeticViaAssignSystemPredicatePointerArchitecture` with `[Trait("Group", "Arithmetic via := system predicate - Pointer Architecture")]`, ctor `(StdlibProgFixture fixture, ITestOutputHelper output)`, private readonly fields `_fixture` + `_output`.
- T6: Emit `[Fact(DisplayName = "add/3 body kernel executes directly")] Add3BodyKernelExecutesDirectly()` body per construct mappings (heap allocate + bind + lookup + invoke + assert).
- T7: Emit `Sub3BodyKernel()` (operands 10/4, expected 6, `(resultWriter, _)` discard).
- T8: Emit `Mul3BodyKernel()` (operands 7/6, expected 42).
- T9: Emit `Div3BodyKernel()` (operands 15/4, expected 3.75).
- T10: Emit `Div3BodyKernelAbortsOnDivisionByZero()` (operands 10/0, expected `BodyKernelResult.Abort`, no post-result value check).
- T11: Emit `Neg2BodyKernel()` (unary; operand 42, expected -42; only 2 `AllocateVariable` calls).
- T12: Emit `SqrtKernel2BodyKernel()` (unary; operand 16, expected 4.0).
- T13: Emit `AllStandardBodyKernelsAreRegistered()` with 24 `Assert.True(rt.BodyKernels.Has("_<name>", <arity>));` calls.
- T14: Emit nested `[Collection("ArithmeticPreludePointer")] public class EndToEndAssignSystemPredicatePointerArchitecture` with `[Trait("Group", "End-to-end := system predicate - Pointer Architecture")]`, ctor + fields.
- T15: Emit `AssignGlpCompilesAndMergesCorrectly()` (re-read prelude, compile `"""hello."""`, merge, assert labels `":=/2"` + `"hello/0"` with user-messages).
- T16: Emit `UserProgramWithAssignCompilesCorrectlyWithSRSW()` (compile `"""compute_sum(Z?) :- Z := 5 + 3."""`, assert `Ops.Count > 0` + `Labels.ContainsKey("compute_sum/1")`).
- T17: Emit `ZAssign5Plus3ExecutesAndBindsZTo8()` (full end-to-end: alloc + runner + scheduler + `CallEnv` + `SetGoalEnv` + `Gq.Enqueue` + `Drain` + `IsFullyBound` + is-pattern + `Assert.Equal(8, constValue.Value)` else `Assert.Fail`).
- T18: Emit nested `[Collection("ArithmeticPreludePointer")] public class VariableChainDereferencing` with `[Trait("Group", "Variable Chain Dereferencing")]`, ctor + fields.
- T19: Emit `ArithmeticThroughVariableChain()` (4 `AllocateVariable` calls, `BindWriter(yWriter, new ConstTerm(5))`, `BindWriterToReader(xWriter, yReader)`, `BindWriter(zWriter, new ConstTerm(3))`, `_add` kernel invoke, `Assert.Equal(8, constValue.Value)`).
- T20: Route all 14 `print(...)` callsites through `_output.WriteLine($"...");` (omit the one inside the `setUpAll`-equivalent fixture ctor).
- T21: Apply argument-order swap to all `Assert.Equal(expected, actual)` calls.
- T22: Apply `Assert.IsType<T>` typed-return collapse at every `(value as ConstTerm).value` site.
- T23: Apply identifier-mangling rules (`:=` → `Assign`, `+` → `Plus`, slashes/periods/spaces dropped, underscores PascalCase-joined).
- T24: Preserve every Dart group/test label verbatim via `[Trait]` and `[Fact(DisplayName=...)]`.

## 4. Research Findings

none required — every construct in this file resolves via KB cache hits documented in the ratified convspec. Eleven of thirteen idioms are reuses from prior sibling specs (`rf-dart-library-directive-to-csharp-namespace-elision`, `rf-dart-package-test-import-to-xunit-using`, `rf-dart-internal-package-import-to-csharp-using`, `rf-dart-dart-io-to-csharp-system-io`, `rf-dart-test-main-to-xunit-class-with-facts`, `rf-dart-setupall-to-xunit-class-fixture`, `rf-dart-late-variable-to-csharp-init-only-property`, `rf-dart-package-test-group-to-xunit-class`, `rf-dart-test-callback-to-xunit-method-body`, `rf-dart-final-local-to-csharp-var`, `rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction`, `rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods`, `rf-dart-sumleaf-no-eq-to-csharp-class-no-record`, `rf-dart-class-eq-on-single-int-field-to-csharp-iequatable`, `rf-dart-list-literal-of-constructors-to-csharp-array-init`, `rf-dart-expect-isnotnull-to-xunit-assertnotnull`, `rf-dart-expect-equals-to-xunit-assertequal`, `rf-dart-expect-isa-to-xunit-istype`, `rf-dart-camel-to-csharp-pascal-method-rename`, `rf-dart-expect-istrue-to-xunit-assert-true`, `rf-dart-property-chain-method-call-to-csharp`, `rf-dart-named-arg-to-csharp-named-arg`, `rf-dart-map-literal-to-csharp-dictionary-initializer`, `rf-dart-string-interpolation-to-csharp-dollar-string`, `rf-dart-fail-call-to-xunit-assert-fail`, `rf-dart-bang-null-assertion-to-csharp-null-forgiving`). Two are first-seen idioms registered in the convspec (`rf-dart-is-flow-typing-to-csharp-is-pattern` and `rf-dart-triple-string-to-csharp-raw-string`), both with authoritative Microsoft Learn + Dart language-tour citations recorded therein. No additional research required at the planning stage.

## 5. Consistency Pass

fixed — derived from the ratified convspec `.codeconv/conversion-specs/test/heap/arithmetic_pointer_test.dart.md` (sha256-matched on `3c0766cd...01404`), which carries `escalations: []` and explicitly notes that the inherited `heap_fcp.dart.md` threading-model escalation does NOT propagate here (this file uses only synchronous calls and per-test fresh `GlpRuntime` instances). The convspec also cites the bytecode-sibling `test/bytecode/arithmetic_test.dart.md` as the structural precedent for the three-group + setUpAll + collection-fixture topology, the 12-test identifier-mangling table, the relative-path routing through `AppContext.BaseDirectory`, the argument-order swap on `Assert.Equal`, and the `Assert.IsType<T>` typed-return collapse. The two first-seen idioms (is-pattern flow-typing, triple-quoted string → raw string) are derived from authoritative Microsoft Learn and Dart language-tour pages cited inline in the convspec. CLAUDE.md disciplines (spec-first, single-source-of-truth, no-fabrication) are satisfied: every conversion decision quotes back to the convspec or to a named SUT spec it references.

## 6. Escalations

None.
