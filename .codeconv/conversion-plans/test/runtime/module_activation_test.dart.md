---
path: test/runtime/module_activation_test.dart
cycle_group_id: 156
scc_siblings: []
generated_at: 2026-05-21T16:44:28Z
source_sha256: 9fd5f3ec7705dda8012f88f4637e0ab09b4fbd78d284f1855867ca8736cd10fb
schema_version: 1
---

# Conversion Plan: test/runtime/module_activation_test.dart

## 1. Source Analysis

The Dart source (`glp_runtime_net/test/runtime/module_activation_test.dart`,
253 lines, SHA-256 `9fd5f3ec…cd10fb`) is a `package:test`-based
integration suite for the GLP module-activation pipeline. Structure
observed by direct file inspection:

- **Imports (lines 1–8)**: one `package:test/test.dart` import + seven
  `package:glp_runtime/...` SUT imports (`compiler/compiler.dart`,
  `runtime/runtime.dart`, `runtime/terms.dart`, `runtime/machine_state.dart`,
  `runtime/scheduler.dart`, `runtime/glp_activation.dart`,
  `bytecode/runner.dart`).
- **Top-level const `serveSource`** (lines 10–24): a triple-quoted
  multi-line GLP source string carrying the `serve/2` system-predicate
  source (`-mode(system).`, `procedure serve(Any?, Any?).`, two clauses
  — the recursive `serve(Module, [Goal | In])` clause guarded by
  `ground(Module?)` and the terminating `serve(_, [])` `otherwise` clause).
  No backslash escapes; no double-quotes; no `$`-interpolation.
- **Top-level helper function `compileModules`** (lines 26–34):
  returns a Dart-3 NAMED record `({BytecodeProgram serve,
  BytecodeProgram target})` by compiling `serveSource` + a caller-
  supplied `targetSource`. Synchronous, no `async`/`Future`.
- **`void main()`** (lines 36–253): contains exactly ONE outer
  `group('Module activation via GLP', () { … })` enclosing FIVE
  synchronous `test(...)` calls — no `setUp` / `setUpAll` / `late`
  / shared state.
- **Test 1** `activateModule spawns serve on channel (suspends waiting
  for input)` (lines 38–68): compiles, calls `activateModule`, asserts
  `rt.runners.containsKey(mods.serve)` is true, `rt.gq.length == 1`,
  drain succeeds, `channel.writerAddr` is non-negative.
- **Test 2** `send single RPC goal on channel, verify it executes`
  (lines 70–106): drain → send `StructTerm('process', [ConstTerm(42)])`
  → enqueue activations via `for-in` → drain again → assert success.
- **Test 3** `send multiple RPC goals on channel` (lines 108–156):
  drain → three sequential send-then-enqueue-then-drain cycles
  (`greet(alice)`, `farewell(bob)`, `greet(carol)`).
- **Test 4** `close channel after sending goals, serve terminates`
  (lines 158–196): drain → send `process(1)` → drain → `channel.close()`
  → drain → assert success (no more suspension).
- **Test 5** `full end-to-end: activate, send RPC, close, verify
  dispatch chain` (lines 198–251): adds `traceSink: (s) => trace.add(s)`
  + `final trace = <String>[]` + `debug: true` drain → sends
  `process(42)` against a 2-clause target module with reduction
  `process(X) :- ground(X?) | consume(X?).` → asserts
  `traceStr.contains('serve')` → closes channel → asserts clean
  termination.
- **Surface used**: `GlpCompiler()`, `compiler.compile(String)`,
  `GlpRuntime()`, `activateModule({rt, serveBytecode, moduleBytecode,
  moduleName})`, `Scheduler({rt, traceSink?})`,
  `scheduler.drainWithStatus({maxCycles, debug})`, `result.status`,
  `ExecutionStatus.succeeded`, `StructTerm(String, List<Term>)`,
  `ConstTerm(Object?)`, `channel.send(Term) -> List<GoalRef>`,
  `channel.close() -> List<GoalRef>`, `channel.writerAddr (int)`,
  `rt.runners (Map<Object?, BytecodeRunner>).containsKey`,
  `rt.gq.length (int)`, `rt.gq.enqueue(GoalRef)`, `expect`,
  `equals`, `isTrue`, `isNonNegative`, `contains`, `reason:`.
- **Concurrency-model inheritance**: every test owns its own
  `GlpRuntime` (therefore its own `HeapFCP`) on its own thread of
  control — no threading-relevant behaviour is exercised
  (per convspec INHERITED-but-NON-blocking determination).

## 2. Dart → C#/.NET Conversion Plan

Each row mirrors a `constructs:` entry from the ratified convspec
verbatim. The `→` character is U+2192.

1. **`import 'package:test/test.dart';` → `using Xunit;`** (cached
   idiom `rf-dart-package-test-import-to-xunit-using`; project-wide
   xUnit pinning). Codegen also adds `using System.Collections.Generic;`
   (for `var trace = new List<string>();` in the e2e test) and emits
   the file inside a namespace mirroring the Dart `test/runtime`
   directory (e.g. `<RootNs>.Test.Runtime`).
2. **Seven `import 'package:glp_runtime/...';` → three C# `using`
   directives** under the per-SUT namespace mapping pinned by each
   lib spec: `using <RootNs>.Compiler;` (carries `GlpCompiler`),
   `using <RootNs>.Runtime;` (carries `GlpRuntime`, `GoalRef`,
   `GoalQueue`, `Term`/`StructTerm`/`ConstTerm`, `Scheduler`,
   `ExecutionStatus`, `DrainResult`, `activateModule`/`GlpActivation`,
   `GlpChannelHandle`), `using <RootNs>.Bytecode;` (carries
   `BytecodeProgram`). C# `using` is per-namespace, not per-file —
   so seven Dart imports compress whenever their converted files
   share a namespace (cached idiom
   `rf-dart-internal-package-import-to-csharp-using`).
3. **`const serveSource = '''…GLP…''';` → `internal static class
   ModuleActivationTestHelpers { internal const string ServeSource
   = @"…"; }`** at file scope (verbatim string literal `@"…"`
   preserves embedded newlines, single quotes `'_activate'`, `Module?`
   byte-for-byte; no `"`-doubling needed; no `$`-interpolation; doc-
   comment `///` becomes `/// <summary>Source for the serve/2 system
   predicate</summary>`). Carry-forward of static-helper-class idiom
   `rf-dart-toplevel-const-multiline-string-to-csharp-helper-class-
   verbatim-const`.
4. **`({BytecodeProgram serve, BytecodeProgram target})
   compileModules(String targetSource)` → `internal static
   (BytecodeProgram Serve, BytecodeProgram Target) CompileModules(
   string targetSource)`** on the same `ModuleActivationTestHelpers`
   class; body `var compiler = new GlpCompiler(); return (compiler.
   Compile(ServeSource), compiler.Compile(targetSource));`. Named
   ValueTuple field names PascalCased (`serve`→`Serve`, `target`→
   `Target`) per .NET naming guidelines (idiom
   `rf-dart-named-record-return-to-csharp-named-valuetuple`).
5. **`void main()` → omitted** (no per-file entrypoint in xUnit —
   `[Fact]` discovery is reflective; idiom
   `rf-dart-package-test-main-omit-in-xunit`).
6. **`group('Module activation via GLP', () { …5 tests… })` → single
   `public class ModuleActivationTests`** with five method members;
   no `[Trait]` partition required for a single-outer-group file
   (precedent `module_parser_test.dart.md`). Class-level XML doc-
   comment preserves the group label verbatim (idiom
   `rf-dart-package-test-group-to-xunit-class`).
7. **`test('<label>', () { …sync body… })` × 5 → `[Fact(DisplayName =
   "<original label>")] public void <PascalName>()`** on the test
   class. Method names: `ActivateModuleSpawnsServeOnChannelSuspends
   WaitingForInput`, `SendSingleRpcGoalOnChannelVerifyItExecutes`,
   `SendMultipleRpcGoalsOnChannel`, `CloseChannelAfterSendingGoals
   ServeTerminates`, `FullEndToEndActivateSendRpcCloseVerifyDispatch
   Chain`. All five callbacks are synchronous (no `async Task`).
   Idiom `rf-dart-test-callback-to-xunit-method-body`.
8. **`final <local> = <expr>;` → `var <local> = <expr>;`** for every
   single-assignment local (`mods`, `rt`, `channel`, `scheduler`,
   `trace`, `goal`); `var result = …;`/`var activations = …;` use
   the same `var` declaration with subsequent reassignment via plain
   `result = …;`/`activations = …;` (both Dart `var` and C# `var`
   permit reassignment). Idiom
   `rf-dart-final-local-to-csharp-var-local`.
9. **`GlpCompiler()` → `new GlpCompiler()`** (C# requires explicit
   `new`; idiom `rf-dart-constructor-call-no-new-to-csharp-new-keyword`).
10. **`compiler.compile(<src>)` → `compiler.Compile(<src>)`**
    (PascalCase instance method; idiom
    `rf-dart-instance-method-camel-to-pascal`).
11. **`GlpRuntime()` → `new GlpRuntime()`** (zero-arg default-
    construction; lib spec pins `GlpRuntime(HeapFCP? heap = null,
    GoalQueue? gq = null, …)` with `??`-default bodies). Idiom
    `rf-dart-constructor-call-no-new-to-csharp-new-keyword`.
12. **`activateModule(rt: rt, serveBytecode: mods.serve,
    moduleBytecode: mods.target, moduleName: 'test_module')` →
    `GlpActivation.ActivateModule(rt: rt, serveBytecode: mods.Serve,
    moduleBytecode: mods.Target, moduleName: "test_module")`**
    (top-level Dart function → static method on static helper class;
    named-argument labels preserved at callsite for clarity; idiom
    `rf-dart-required-named-args-to-csharp-named-args`).
13. **`StructTerm('<functor>', [ConstTerm(<lit>)])` → `new StructTerm
    ("<functor>", new List<Term> { new ConstTerm(<lit>) })`** for
    all six callsites (`process(42)`, `greet('alice')`,
    `farewell('bob')`, `greet('carol')`, `process(1)`, `process(42)`).
    Single-quoted Dart strings → double-quoted C# strings; idiom
    `rf-dart-list-literal-to-csharp-list-of-T` +
    `rf-dart-sumleaf-with-list-no-eq-to-csharp-class-ireadonlylist`.
14. **`Scheduler(rt: rt)` and `Scheduler(rt: rt, traceSink: (s) =>
    trace.add(s))` → `new Scheduler(rt: rt)` and `new Scheduler(rt:
    rt, traceSink: s => trace.Add(s))`** (per scheduler.dart.md:
    `public Scheduler(GlpRuntime rt, BytecodeRunner? runner = null,
    Dictionary<object?, BytecodeRunner>? runners = null,
    Action<string>? traceSink = null)`). Idiom
    `rf-dart-required-named-args-to-csharp-named-args`.
15. **`final trace = <String>[];` → `var trace = new List<string>();`**
    (empty typed mutable list literal; idiom
    `rf-dart-typed-empty-list-literal-to-csharp-new-list-of-T`).
16. **`(s) => trace.add(s)` → `s => trace.Add(s)`** (single-arg arrow
    lambda; closure captures `trace` by reference in both languages;
    method-group simplification `trace.Add` also valid). Idiom
    `rf-dart-arrow-lambda-to-csharp-lambda`.
17. **`scheduler.drainWithStatus(maxCycles: <N>)` and `scheduler.
    drainWithStatus(maxCycles: 500, debug: true)` → `scheduler.
    DrainWithStatus(maxCycles: 100)` / `scheduler.DrainWithStatus(
    maxCycles: 200)` / `scheduler.DrainWithStatus(maxCycles: 500,
    debug: true)`** (PascalCase + optional-named args preserved; idiom
    `rf-dart-instance-method-camel-to-pascal`).
18. **`result.status` → `result.Status`** (PascalCase property;
    `DrainResult.Status { get; }` per scheduler.dart.md). Idiom
    `rf-dart-instance-method-camel-to-pascal`.
19. **`ExecutionStatus.succeeded` → `ExecutionStatus.Succeeded`**
    (PascalCase enum member per scheduler.dart.md
    `enum ExecutionStatus { Succeeded, Failed, Suspended }`). Used
    seven times across the five tests. Idiom
    `rf-dart-plain-enum-to-csharp-enum`.
20. **`channel.send(goal)` / `channel.send(StructTerm(...))` →
    `channel.Send(goal)` / `channel.Send(new StructTerm(...))`**
    (returns `List<GoalRef>`; reference-typed `GlpChannelHandle`
    mutates `_writerAddr` in place; idiom
    `rf-dart-instance-method-camel-to-pascal`).
21. **`channel.close()` → `channel.Close()`** (terminal mutation —
    binds writer to nil; returns `List<GoalRef>`; idiom
    `rf-dart-instance-method-camel-to-pascal`).
22. **`channel.writerAddr` → `channel.WriterAddr`** (`int` → `long`
    width per cells.dart.md heap-address policy; idiom
    `rf-dart-int-to-csharp-long-width`).
23. **`rt.runners.containsKey(mods.serve)` → `rt.Runners.ContainsKey
    (mods.Serve)`** (`Dictionary<object?, BytecodeRunner>.ContainsKey`;
    reference-equality on `BytecodeProgram` keys via
    `EqualityComparer<TKey>.Default`; idiom
    `rf-dart-map-containskey-to-csharp-dictionary-containskey`).
24. **`rt.gq.length` → `rt.Gq.Length`** (`Length` property on
    `GoalQueue` per machine_state.dart.md; idiom
    `rf-dart-instance-method-camel-to-pascal`).
25. **`rt.gq.enqueue(act)` → `rt.Gq.Enqueue(act)`** (PascalCase;
    `GoalRef` is `readonly record struct` so no boxing into
    `Queue<GoalRef>`; idiom
    `rf-dart-instance-method-camel-to-pascal`).
26. **`for (final act in activations) { rt.gq.enqueue(act); }` →
    `foreach (var act in activations) { rt.Gq.Enqueue(act); }`**
    (five occurrences total; idiom
    `rf-dart-for-in-final-to-csharp-foreach-var`).
27. **`expect(rt.gq.length, equals(1))` → `Assert.Equal(1L, rt.Gq.
    Length);`** (argument-order FLIP; `1L` literal disambiguates the
    `long` overload of `Assert.Equal<T>`; idiom
    `rf-dart-expect-equals-to-xunit-assertequal`).
28. **`expect(result.status, equals(ExecutionStatus.succeeded))` →
    `Assert.Equal(ExecutionStatus.Succeeded, result.Status);`**
    (argument-order FLIP; seven occurrences across the five tests;
    same cached idiom).
29. **`expect(result.status, equals(ExecutionStatus.succeeded),
    reason: '<msg>')` → `Assert.Equal(ExecutionStatus.Succeeded,
    result.Status); // reason: <msg>`** (inline comment preserves
    the reason because `Assert.Equal<T>` has no user-message
    overload; idiom
    `rf-dart-expect-with-reason-to-xunit-comment-or-asserttrue`).
30. **`expect(traceStr, contains('serve'), reason: 'Trace should show
    serve reduction')` → `Assert.Contains("serve", traceStr); //
    reason: Trace should show serve reduction`** (String-pair
    `Assert.Contains` overload; substring-first arg order is identical
    to Dart; idiom
    `rf-dart-expect-contains-substring-to-xunit-assert-contains`).
31. **`expect(rt.runners.containsKey(mods.serve), isTrue)` →
    `Assert.True(rt.Runners.ContainsKey(mods.Serve));`** (no
    `reason:` here, no message overload needed; idiom
    `rf-dart-expect-istrue-to-xunit-asserttrue`).
32. **`expect(channel.writerAddr, isNonNegative)` → `Assert.True(
    channel.WriterAddr >= 0);`** (`isNonNegative` has no direct
    xUnit equivalent; canonical mapping is `Assert.True(<actual> >=
    0)`; literal `0` widens to `long` automatically — `0L` optional
    for clarity; idiom
    `rf-dart-expect-isNonNegative-to-xunit-asserttrue-ge-zero`).

## 3. Decomposed Task Units

- **T1**: Emit file-scope `using` directives (`using Xunit;`, `using
  System.Collections.Generic;`, `using <RootNs>.Compiler;`, `using
  <RootNs>.Runtime;`, `using <RootNs>.Bytecode;`) and the
  `<RootNs>.Test.Runtime` namespace declaration. Mirrors §2 #1, #2.
- **T2**: Emit `internal static class ModuleActivationTestHelpers`
  containing (a) the `ServeSource` `internal const string` initialised
  from the verbatim multi-line literal `@"…"` and (b) the
  `CompileModules(string targetSource)` `internal static` method
  returning the named ValueTuple. Mirrors §2 #3, #4, #9, #10.
- **T3**: Emit `public class ModuleActivationTests` (no constructor,
  no fields) with class-level XML doc-comment preserving the
  `'Module activation via GLP'` group label. Mirrors §2 #5, #6.
- **T4**: Emit `ActivateModuleSpawnsServeOnChannelSuspendsWaitingFor
  Input` `[Fact]` method — arrange (`CompileModules` + `new
  GlpRuntime()` + `GlpActivation.ActivateModule(...)` with named
  args), assert `Assert.True(rt.Runners.ContainsKey(mods.Serve))`,
  `Assert.Equal(1L, rt.Gq.Length)`, drain `new Scheduler(rt: rt).
  DrainWithStatus(maxCycles: 100)`, `Assert.Equal(ExecutionStatus.
  Succeeded, result.Status); // reason: …`, `Assert.True(channel.
  WriterAddr >= 0)`. Mirrors §2 #7, #11–#14, #17–#19, #20, #22, #27,
  #28, #29, #31, #32.
- **T5**: Emit `SendSingleRpcGoalOnChannelVerifyItExecutes` `[Fact]`
  method — drain, send `new StructTerm("process", new List<Term> {
  new ConstTerm(42) })`, `foreach` enqueue, drain again, assert.
  Mirrors §2 #7, #13, #17, #20, #25, #26, #28, #29.
- **T6**: Emit `SendMultipleRpcGoalsOnChannel` `[Fact]` method —
  three send-enqueue-drain cycles for `greet("alice")`,
  `farewell("bob")`, `greet("carol")` with intermediate assertions.
  Mirrors §2 #7, #13, #17, #20, #25, #26, #28.
- **T7**: Emit `CloseChannelAfterSendingGoalsServeTerminates`
  `[Fact]` method — send `process(1)`, drain, `channel.Close()`,
  drain, assert with reason comment. Mirrors §2 #7, #13, #17, #20,
  #21, #25, #26, #28, #29.
- **T8**: Emit `FullEndToEndActivateSendRpcCloseVerifyDispatchChain`
  `[Fact]` method — initialise `var trace = new List<string>();`,
  construct `new Scheduler(rt: rt, traceSink: s => trace.Add(s))`,
  drain, send `process(42)`, `foreach` enqueue, drain with
  `maxCycles: 500, debug: true`, `Assert.Contains("serve",
  traceStr); // reason: …`, `channel.Close()`, `foreach` enqueue,
  drain, final assertion. Mirrors §2 #7, #13, #14, #15, #16, #17,
  #19, #20, #21, #25, #26, #28, #29, #30.
- **T9**: Verify NO `using System.Linq;` or `using System;` directives
  are emitted (cu-7 — only foreach + ContainsKey + List.Add surface
  is exercised; the namespace surface needed is covered by `Xunit`
  + `System.Collections.Generic` + the three SUT namespaces).
  Mirrors convspec `conversion_units` cu-7.

## 4. Research Findings

none required. Every construct in §2 cites either a cached idiom
from a prior convspec (xUnit pinning, internal-package-import to
`using`, naming-convention camel→Pascal, constructor `new`-keyword,
list-literal, `Assert.Equal` argument-order flip, `Assert.True`,
`Assert.Contains`, `for-in` → `foreach`, `int` → `long` width,
`Map.containsKey` → `Dictionary.ContainsKey`, plain-enum →
`enum`, `void Function(T)` → `Action<T>`, final-local → `var`-local)
OR a precedent decision pinned by a peer convspec for the SUT type
touched (`GlpCompiler`/`compiler.dart.md`, `GlpRuntime`/
`runtime.dart.md`, `Scheduler`/`ExecutionStatus`/`DrainResult`/
`scheduler.dart.md`, `GoalQueue`/`GoalRef`/`machine_state.dart.md`,
`StructTerm`/`ConstTerm`/`Term`/`terms.dart.md`, `BytecodeProgram`/
`runner.dart.md`, `activateModule`/`GlpChannelHandle`/
`glp_activation.dart.md`). The FIRST-SEEN-here idiom rows
(`rf-dart-toplevel-const-multiline-string-to-csharp-helper-class-
verbatim-const`, `rf-dart-named-record-return-to-csharp-named-
valuetuple`, `rf-dart-required-named-args-to-csharp-named-args`,
`rf-dart-typed-empty-list-literal-to-csharp-new-list-of-T`,
`rf-dart-arrow-lambda-to-csharp-lambda`, `rf-dart-for-in-final-to-
csharp-foreach-var`, `rf-dart-expect-with-reason-to-xunit-comment-
or-asserttrue`, `rf-dart-expect-contains-substring-to-xunit-assert-
contains`, `rf-dart-expect-isNonNegative-to-xunit-asserttrue-ge-
zero`) are each ratified inside the convspec with citations to
Microsoft Learn, xunit.net, and dart.dev official documentation;
no additional research is required at the plan stage.

The convspec-documented INHERITED concurrency escalation on
`HeapFCP` (FR-013 single-owning-context, resolved upstream — see
.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md) is
NON-blocking for this file: every test owns its own `GlpRuntime` on
its own thread of control, so the C# port preserves the single-
owner invariant trivially through xUnit's per-`[Fact]` class
instantiation and per-test-method stack-local ownership. The
upstream escalation is recorded in nuance fields but NOT re-
escalated here (per the agent instructions and the convspec's
explicit non-re-escalation determination).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/runtime/
module_activation_test.dart.md` (constructs table + conversion_units
+ Rationale section). Every §2 row cites the same idiom_id /
research_finding_id used in the corresponding convspec construct.
Every §3 task unit composes a contiguous subset of §2 rows. No
construct in the source file (verified by reading
`glp_runtime_net/test/runtime/module_activation_test.dart` lines
1–253) is unaccounted for. The convspec's `escalations: []`
determination is honoured: the plan has zero new escalations and
defers to the upstream heap_fcp ruling (already closed —
`494428c8` — single-owning-context per memory entry
`project_018_codeconv_builder_status.md`).

## 6. Escalations

None.
