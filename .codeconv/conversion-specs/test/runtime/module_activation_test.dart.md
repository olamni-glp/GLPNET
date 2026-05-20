# Conversion Spec — test/runtime/module_activation_test.dart

> Conversion-spec artifact for test/runtime/module_activation_test.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.
>
> File is a `package:test`-based integration suite (253 lines, 5
> `test()` cases inside ONE outer `group('Module activation via GLP',
> ...)` in `void main()`). It exercises the end-to-end GLP module-
> activation pipeline: compile the `serve/2` system predicate +
> a target module, call `activateModule(...)` to obtain a
> `GlpChannelHandle`, then drive a `Scheduler` while sending /
> closing RPC goals on the channel and asserting `ExecutionStatus`
> outcomes plus (in one test) a trace-string substring. Every
> non-trivial construct REUSES an idiom recorded by prior
> runtime / test specs (heap/binding_pointer_test, compiler/
> partial_evaluator_test, module/module_compiler_test, lib/runtime/
> glp_activation, lib/runtime/scheduler, lib/runtime/runtime,
> lib/runtime/machine_state, lib/runtime/terms, lib/bytecode/runner,
> lib/compiler/compiler). The file inherits the `HeapFCP`
> concurrency-model escalation transitively through `Scheduler`
> (operates on `GlpRuntime.heap`) — flagged in nuance below and
> deferred to the heap_fcp ruling (NOT re-escalated here, FR-013).

```yaml
schema_version: 1
source_path: test/runtime/module_activation_test.dart
source_sha256: 9fd5f3ec7705dda8012f88f4637e0ab09b4fbd78d284f1855867ca8736cd10fb
target_code_unit: test/runtime/ModuleActivationTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` and replace at
      file scope with `using Xunit;`. REUSE the project-wide xUnit
      pinning established by smoke_test.dart.md and every subsequent
      `package:test` convspec (heap/binding_pointer_test.dart.md,
      compiler/partial_evaluator_test.dart.md, module/*, multiagent/*,
      analysis/type_checker/*). FR-012 / SC-007 cache hit — no
      re-research. Codegen MUST also add `using System.Collections.
      Generic;` (for the `var trace = new List<string>();` collection
      in the e2e test) and project to a namespace mirroring the Dart
      `test/runtime` directory (e.g. `<RootNs>.Test.Runtime`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Framework-choice reuse (explicitly addressed): every
      `package:test` file in the inventory maps to the SAME .NET
      framework (xUnit) so test discovery, runner config, and
      attribute vocabulary stay consistent (SC-007). Lifecycle nuance
      carries forward: xUnit creates a FRESH instance of the test
      class per `[Fact]` (xunit.net "Shared Context between Tests")
      — every `test()` body in this file constructs its OWN
      `GlpRuntime`, `Scheduler`, `GlpChannelHandle`, etc., so the
      per-instance freshness matches the source.

  - construct_key: dart.package_under_test.import_directive_multi_runtime
    source_form: |-
      "import 'package:glp_runtime/compiler/compiler.dart';
       import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/terms.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';
       import 'package:glp_runtime/runtime/scheduler.dart';
       import 'package:glp_runtime/runtime/glp_activation.dart';
       import 'package:glp_runtime/bytecode/runner.dart';"
    target_decision: >-
      Seven `package:glp_runtime/...` imports collapse to TWO C# `using`
      directives (one per target namespace) under the conventional
      namespace mapping pinned by the per-SUT specs: `using <RootNs>.
      Compiler;` (carries `GlpCompiler` from lib/compiler/compiler.
      dart.md), `using <RootNs>.Runtime;` (carries `GlpRuntime`,
      `GoalRef`/`GoalQueue` re-exported via machine_state, `Term` /
      `StructTerm` / `ConstTerm` from terms, `Scheduler` /
      `ExecutionStatus` from scheduler, `activateModule` /
      `GlpChannelHandle` from glp_activation), and `using <RootNs>.
      Bytecode;` (carries `BytecodeProgram` from bytecode/runner.dart).
      C# `using` is per-namespace, not per-file — the seven Dart
      imports compress whenever their converted files share a
      namespace. This spec records the SHAPE of the cross-file
      dependency; the namespace string is owned by the SUT specs.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cached idiom (precedent: binding_pointer_test.dart.md, module_
      compiler_test.dart.md, partial_evaluator_test.dart.md). Import-
      unit nuance: Dart imports a library/file; C# imports a
      namespace. No `show`/`hide`/`as` clauses appear, so plain
      `using <Ns>;` per distinct target namespace. The test assembly
      must reference the SUT assembly via the project file (langpair-
      level concern — out of scope for THIS artifact).

  - construct_key: dart.toplevel.const_string_triple_quoted_serve_source
    source_form: |-
      "/// Source for the serve/2 system predicate
       const serveSource = '''
       -mode(system).

       procedure serve(Any?, Any?).

       serve(Module, [Goal | In]) :-
           ground(Module?) |
           '_activate'(Module?, Goal?),
           serve(Module?, In?).

       serve(_, []) :-
           otherwise |
           true.
       ''';"
    target_decision: >-
      Dart top-level `const String serveSource = '''...''';` (multi-
      line raw-ish triple-quoted string, no interpolation, no
      backslash escapes inside the GLP source) maps to a C#
      file-scope helper-class static `const`. Because C# forbids
      true top-level fields, emit `internal static class
      ModuleActivationTestHelpers { internal const string
      ServeSource = @"..."; }` — sibling to the test class within
      the same namespace (same shape as the module_compiler_test
      `ModuleCompilerTestHelpers` precedent). Codegen MUST use a C#
      verbatim string literal `@"..."` to preserve the embedded
      newlines and single-quote characters (`'_activate'`,
      `Module?`) without escape processing; the only special
      character in a verbatim string is `"` which doubles to `""`.
      None appear in this payload, so the body is a 1:1 transcript
      with the leading newline preserved. Doc-comment `///` becomes
      C# `///` XML-doc `<summary>Source for the serve/2 system
      predicate</summary>` above the const field.
    idiom_id: null
    research_finding_id: rf-dart-toplevel-const-multiline-string-to-csharp-helper-class-verbatim-const
    nuance: >-
      Top-level-vs-class nuance (explicitly addressed): Dart permits
      true file-scope `const String foo = '''...''';`; C# does NOT
      — every member belongs to a type. The canonical C# shape is
      `internal static class <File>Helpers { internal const string
      <Name> = @"..."; }` (Microsoft naming guideline: "Use a static
      class that contains a set of static methods" —
      https://learn.microsoft.com/dotnet/standard/design-guidelines/
      static-class). Triple-quoted-vs-verbatim nuance: Dart `'''…'''`
      is a multiline literal where backslash IS an escape character;
      C# `@"…"` is a verbatim literal where backslash is NOT an
      escape character but `"` doubles to `""`. The payload here
      contains neither backslashes nor double-quotes, so both
      interpretations agree byte-for-byte. Interpolation nuance:
      Dart `'''$x'''` interpolates; C# `@"…"` does NOT (need `$@"…"`
      for that). The source has NO `$`-interpolation; preserve the
      verbatim form. Leading-newline nuance: Dart `'''<newline>-mode
      (system).<newline>…<newline>'''` carries the leading newline
      after `'''`; the C# verbatim literal preserves the same
      leading newline literally. The GLP parser does not depend on
      this leading newline (the source is parsed line-by-line) — the
      mapping is semantics-preserving.

  - construct_key: dart.toplevel.helper_function_returning_record
    source_form: |-
      "/// Compile serve.glp and a target module, returning both bytecodes.
       ({BytecodeProgram serve, BytecodeProgram target}) compileModules(
           String targetSource) {
         final compiler = GlpCompiler();
         return (
           serve: compiler.compile(serveSource),
           target: compiler.compile(targetSource),
         );
       }"
    target_decision: >-
      Dart top-level function with a Dart-3 NAMED RECORD return type
      `({BytecodeProgram serve, BytecodeProgram target})`. Map the
      function itself per the file-scope-helper idiom (precedent
      module_compiler_test.dart.md): place inside the same
      `internal static class ModuleActivationTestHelpers` next to
      `ServeSource`, as `internal static (BytecodeProgram Serve,
      BytecodeProgram Target) CompileModules(string targetSource)
      { var compiler = new GlpCompiler(); return (compiler.Compile(
      ServeSource), compiler.Compile(targetSource)); }`. The C#
      named-tuple syntax `(BytecodeProgram Serve, BytecodeProgram
      Target)` is the canonical mapping for Dart-3 named records
      with all-typed-named fields (C# 7+ tuples — Microsoft Learn
      "tuple types": https://learn.microsoft.com/dotnet/csharp/
      language-reference/builtin-types/value-tuples). Return-site
      uses the parenthesised-positional tuple constructor `(a, b)`
      assigned to the named-tuple return type — the field order
      matches the declaration order. Callsites de-structure as
      `var mods = ModuleActivationTestHelpers.CompileModules(...);
      ... mods.Serve ... mods.Target`. The doc-comment `///`
      becomes a C# `///` `<summary>` block.
    idiom_id: null
    research_finding_id: rf-dart-named-record-return-to-csharp-named-valuetuple
    nuance: >-
      FIRST-SEEN idiom row (named records as return types, distinct
      from the positional-record case `(long, long)` already pinned
      by heap_fcp.dart.md — rf-dart-record-return-to-csharp-
      valuetuple). Dart-3 records are immutable value types
      (https://dart.dev/language/records); C# named tuples are also
      immutable value types (System.ValueTuple under the hood);
      semantics align. Field-naming nuance (explicitly addressed):
      Dart record field names are camelCase (`serve`, `target`);
      C# named-tuple element names follow .NET PascalCase
      (`Serve`, `Target`). Callsite access (`mods.serve` ->
      `mods.Serve`) follows the casing convention uniformly.
      Allocation nuance: both Dart record-of-references and C#
      named-tuple-of-references carry two object references — no
      heap allocation for the tuple itself (value type), only for
      the two `BytecodeProgram` instances inside. Helper-placement
      nuance: file-scope Dart helper -> `internal static` method on
      a sibling helper class (carry-forward from module_compiler_
      test.dart.md). Async nuance: ABSENT — the helper is
      synchronous (no `async`/`Future`); the C# method stays
      synchronous (no `Task<>` wrap).

  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('Module activation via GLP', () { test(...); ... ×5 tests }); }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint;
      xUnit discovers `[Fact]` methods by reflection — there is NO
      per-file entrypoint to emit. Eliminate `main` entirely; its
      body (one outer `group(...)` call containing five `test(...)`
      calls) becomes a single enclosing test class (see
      `dart.package_test.group_block` below).
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Cached idiom (precedent: every prior test convspec).
      Lifecycle nuance: Dart `main` is invoked once per test-file
      process; xUnit has no per-file hook — only per-class
      constructor + `IDisposable.Dispose`, and per-collection
      fixtures. THIS file's `main` body is a single `group(...)`
      with no other statements (no file-level `setUp`, no `setUpAll`,
      no top-of-`main` initialisation as partial_evaluator_test
      had with the prelude bootstrap) — the omission is lossless.

  - construct_key: dart.package_test.group_block_single_outer
    source_form: |-
      "group('Module activation via GLP', () {
         test('activateModule spawns serve on channel (suspends waiting for input)', () { ... });
         test('send single RPC goal on channel, verify it executes', () { ... });
         test('send multiple RPC goals on channel', () { ... });
         test('close channel after sending goals, serve terminates', () { ... });
         test('full end-to-end: activate, send RPC, close, verify dispatch chain', () { ... });
       });"
    target_decision: >-
      ONE outer `group(...)` with five flat sibling `test(...)`
      calls (no nesting, no `setUp`/`setUpAll`, no `late` shared
      state). Map to a SINGLE PascalCase xUnit test class
      `ModuleActivationTests` containing all five test methods.
      Because there is exactly one group and no shared per-test
      state, NO `[Trait("Group", "...")]` partition is required
      (rf-dart-package-test-group-to-xunit-class permits omitting
      the trait when only one group exists — precedent: module_
      parser_test.dart.md single-group case). Codegen MAY still
      add `[Trait("Group", "Module activation via GLP")]` for
      consistency with the multi-group precedent; this spec
      records the omission as DEFAULT. The original group label
      "Module activation via GLP" is preserved verbatim only via
      the class-level XML doc-comment (the `[Trait]` is optional;
      the per-test `[Fact(DisplayName = ...)]` already preserves
      the per-test label).
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Cached idiom (precedent: binding_pointer_test.dart.md flat
      groups, module_compiler_test.dart.md sibling groups,
      partial_evaluator_test.dart.md single outer group). Single-
      outer-group nuance (explicitly addressed): unlike binding_
      pointer_test (six sibling groups requiring `[Trait]`
      partition), this file has exactly ONE outer group and no
      `late` field, so the FLATTEN produces one class with no
      trait fragmentation. Name-mangling nuance: the label "Module
      activation via GLP" PascalCases to a class-internal display
      string only; the class name `ModuleActivationTests` follows
      the file-name-derived convention shared by every prior
      test convspec.

  - construct_key: dart.package_test.test_call_simple_synchronous
    source_form: >-
      "test('<label>', () { /* arrange (compileModules + GlpRuntime
       + activateModule + Scheduler), act (drainWithStatus / send /
       close + gq.enqueue loop), assert (expect ExecutionStatus,
       contains, etc.) */ });"
    target_decision: >-
      Each Dart `test(label, body)` with no `skip:` / no `timeout:`
      and a SYNCHRONOUS closure body becomes a `public void`
      instance method on `ModuleActivationTests`, decorated with
      `[Fact(DisplayName = "<original label>")]`. Method names are
      label-derived PascalCase with non-identifier characters
      stripped: `'activateModule spawns serve on channel (suspends
      waiting for input)'` ->
      `ActivateModuleSpawnsServeOnChannelSuspendsWaitingForInput`;
      `'send single RPC goal on channel, verify it executes'` ->
      `SendSingleRpcGoalOnChannelVerifyItExecutes`; `'send multiple
      RPC goals on channel'` ->
      `SendMultipleRpcGoalsOnChannel`; `'close channel after
      sending goals, serve terminates'` ->
      `CloseChannelAfterSendingGoalsServeTerminates`; `'full
      end-to-end: activate, send RPC, close, verify dispatch chain'`
      -> `FullEndToEndActivateSendRpcCloseVerifyDispatchChain`. All
      FIVE callbacks in this file are synchronous (no `async`, no
      `Future`, no `await`) — NO target method is `async Task`.
      Bodies translate statement-for-statement (see the per-call
      construct rows below).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Cached idiom (precedent: every prior test convspec). No-
      shared-state nuance (explicitly addressed, follows binding_
      pointer_test precedent): every test allocates its own
      `compileModules(...)` result, its own `GlpRuntime`, its own
      `activateModule(...)` channel, its own `Scheduler`. Codegen
      MUST emit per-method-local `var` declarations, NOT instance
      fields — xUnit's per-test class instantiation provides the
      isolation guarantee. Async nuance: explicitly NOT exercised
      in this file (no `async`/`await`/`Future`) — recorded as
      "carry-forward documented in scheduler.dart.md async nuance,
      not surfaced here". Naming-mangling nuance: parenthesised
      and special characters in labels are stripped, but the
      `DisplayName` preserves the literal label including
      parentheses, commas, and colons.

  - construct_key: dart.local_var.final_constructor_invocation_chain
    source_form: |-
      "final mods = compileModules('''…GLP source…''');
       final rt = GlpRuntime();
       final channel = activateModule(rt: rt, serveBytecode: mods.serve, moduleBytecode: mods.target, moduleName: 'test_module');
       final scheduler = Scheduler(rt: rt);
       final scheduler = Scheduler(rt: rt, traceSink: (s) => trace.add(s));
       final trace = <String>[];
       final goal = StructTerm('process', [ConstTerm(42)]);"
    target_decision: >-
      Each Dart `final <local> = <expr>;` with type inferred from
      the RHS maps to C# method-local `var <local> = <expr>;`.
      Single-assignment locals — `mods`, `rt`, `channel`,
      `scheduler`, `trace`, `goal`. `var result` / `var activations`
      below are reassigned across `drainWithStatus` / `send` /
      `close` calls — those use the same `var` declaration with
      subsequent plain assignments. Carry-forward from rf-dart-
      final-local-to-csharp-var-local (binding_pointer_test.dart.md
      construct `dart.local_var.final_constructor_instance`).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Reassignment nuance: Dart `var result = scheduler.
      drainWithStatus(...)` is later reassigned via `result =
      scheduler.drainWithStatus(...)` again — Dart `var` allows
      reassignment (unlike `final`). C# `var` ALSO allows
      reassignment; same shape. Per-test arena nuance: every test
      method emits its own `var rt = new GlpRuntime();` (and
      `var scheduler = new Scheduler(rt: rt);`) — NEVER hoist to an
      instance field, NEVER share across tests; xUnit per-test
      instantiation supplies the isolation. Concurrency nuance:
      INHERITED (see escalation note at end of file) — the
      `GlpRuntime`/`HeapFCP`/`Scheduler` triple is owned by a
      single test method's call-stack, so the per-test single-
      thread model matches the Dart isolate-owned-state contract.

  - construct_key: dart.constructor_call.glp_compiler_zero_arg
    source_form: "GlpCompiler()"
    target_decision: >-
      `new GlpCompiler()`. SUT type per lib/compiler/compiler.dart.md
      construct `dart.facade_class.glp_compiler_four_final_fields_
      three_methods` — the C# mapping is `public sealed class
      GlpCompiler` with a default constructor. No constructor
      arguments in any of the four callsites here (one direct in
      the helper, three inside `compileModules` via that helper).
    idiom_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    research_finding_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    nuance: >-
      Cached idiom (precedent: module_compiler_test.dart.md
      `final lexer = Lexer(source);` etc.). `new` keyword nuance
      explicitly addressed: Dart 2.x made `new` optional and
      idiomatic to drop; C# REQUIRES `new` at every constructor
      invocation. Codegen MUST insert `new` at every constructor
      callsite.

  - construct_key: dart.method_call.glp_compiler_compile_string
    source_form: "compiler.compile(serveSource), compiler.compile(targetSource)"
    target_decision: >-
      Map to PascalCase C# instance method calls per lib/compiler/
      compiler.dart.md construct `dart.method.compile_string_to_
      bytecode_program`: `compiler.Compile(ServeSource)` and
      `compiler.Compile(targetSource)`. Return type `BytecodeProgram`
      (carry-forward from lib/bytecode/runner.dart.md). The single
      `String` positional argument maps to `string` — Dart `String`
      <-> C# `string` is the trivial naming-convention idiom.
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Naming-convention nuance (explicitly addressed, well-known
      footgun): Dart `lowerCamelCase` instance methods become C#
      `PascalCase`. The mapping must be consistent across every
      callsite in this file matching the public surface decided in
      compiler.dart.md. No optional-positional / optional-named
      arguments in the source — only the required `String source`
      positional.

  - construct_key: dart.constructor_call.glp_runtime_zero_arg
    source_form: "GlpRuntime()"
    target_decision: >-
      `new GlpRuntime()`. SUT type per lib/runtime/runtime.dart.md
      `class GlpRuntime` with the named-optional all-defaulted
      constructor `GlpRuntime({HeapFCP? heap, GoalQueue? gq,
      SystemPredicateRegistry? systemPredicates, BodyKernelRegistry?
      bodyKernels})`. The zero-arg call binds every parameter to
      its `??`-defaulted value (a fresh `HeapFCP()`, `GoalQueue()`,
      etc.). C# mapping is `new GlpRuntime()` — the lib spec pins
      the constructor as `public GlpRuntime(HeapFCP? heap = null,
      GoalQueue? gq = null, ...)` with `??`-default bodies, so the
      zero-arg call is well-defined.
    idiom_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    research_finding_id: rf-dart-constructor-call-no-new-to-csharp-new-keyword
    nuance: >-
      Default-construction nuance (explicitly addressed): Dart
      named-optional parameters with `??`-default bodies map to C#
      optional parameters with `= null` defaults + in-body `??`
      assignment (carry-forward from runtime.dart.md). The
      zero-arg callsite has no overload-resolution ambiguity in
      either language. The aggregated heap/gq/registries are all
      freshly allocated per `GlpRuntime` instance — concurrency-
      INHERITED nuance: each `GlpRuntime` therefore owns a private
      `HeapFCP`, satisfying the isolate-owned-state contract from
      heap_fcp.dart.md (see file-level escalation note).

  - construct_key: dart.function_call.activate_module_named_args
    source_form: |-
      "final channel = activateModule(
         rt: rt,
         serveBytecode: mods.serve,
         moduleBytecode: mods.target,
         moduleName: 'test_module',
       );"
    target_decision: >-
      Dart top-level function `activateModule({required GlpRuntime
      rt, required BytecodeProgram serveBytecode, required
      BytecodeProgram moduleBytecode, required String moduleName})`
      returns `GlpChannelHandle`. Per lib/runtime/glp_activation.
      dart.md the C# host is a static method on a static class in
      the runtime namespace (Dart top-level function -> C# static
      method on a static helper class — same rule as module_
      compiler_test.dart.md). Spec form: `GlpActivation.
      ActivateModule(rt: rt, serveBytecode: mods.Serve,
      moduleBytecode: mods.Target, moduleName: "test_module")` —
      C# named-argument syntax preserves call-site clarity (Dart
      named arguments map to C# named arguments verbatim; both
      are positional-bypassing). The static-class wrapper name
      (`GlpActivation`) is decided by lib/runtime/glp_activation.
      dart.md; this spec records the callsite SHAPE only.
    idiom_id: null
    research_finding_id: rf-dart-required-named-args-to-csharp-named-args
    nuance: >-
      FIRST-SEEN-here idiom row (named-required args at callsite).
      Required-named nuance (explicitly addressed): Dart `required
      <Type> <name>` enforces presence at compile time; C# has no
      `required` modifier on parameters — instead the SUT-side
      method declares the parameter as a non-optional positional or
      defaults-to-throw. Per lib/runtime/glp_activation.dart.md
      the converted signature is positional (`ActivateModule(
      GlpRuntime rt, BytecodeProgram serveBytecode, BytecodeProgram
      moduleBytecode, string moduleName)`) — C# named-argument
      syntax STILL works at callsite because C# 4+ supports
      `MethodName(paramName: value, ...)` for any non-optional
      positional parameter (Microsoft Learn "Named and optional
      arguments": https://learn.microsoft.com/dotnet/csharp/
      programming-guide/classes-and-structs/named-and-optional-
      arguments). Codegen MAY drop the named-argument labels at
      callsite (Dart `moduleName: 'test_module'` -> C#
      `"test_module"`) — preserving them gives readability parity
      with the source. THIS spec recommends preserving the labels
      (clarity wins; C# permits it). Field-casing nuance:
      callsite `serveBytecode: mods.serve` reads the Dart record
      field `serve` -> C# tuple field `Serve` (per the named-
      record construct above). Module-name string nuance: Dart
      `'test_module'` -> C# `"test_module"` (single-quote vs
      double-quote — Dart permits both; C# uses double-quote only).

  - construct_key: dart.constructor_call.struct_term_with_const_term_args
    source_form: |-
      "StructTerm('process', [ConstTerm(42)]);
       StructTerm('greet', [ConstTerm('alice')]);
       StructTerm('farewell', [ConstTerm('bob')]);
       StructTerm('greet', [ConstTerm('carol')]);
       StructTerm('process', [ConstTerm(1)]);
       StructTerm('process', [ConstTerm(42)]);"
    target_decision: >-
      Map to `new StructTerm("<functor>", new List<Term> { new
      ConstTerm(<lit>) })` per lib/runtime/terms.dart.md construct
      `dart.sum_type_leaf.functor_args_list_reference_identity`
      and `dart.sum_type_leaf.value_carrying_no_eq_override_
      reference_identity`. Carry-forward from binding_pointer_
      test.dart.md (`dart.constructor_call.const_term_with_value`
      and `dart.constructor_call.struct_term_with_functor_and_
      args_list`). Each `ConstTerm(<lit>)` wraps a heterogeneous
      boxed value (Dart `int 42`, `String 'alice'/'bob'/'carol'`,
      `int 1`) into `object? Value` (cached idiom rf-dart-sumleaf-
      no-eq-to-csharp-class-no-record).
    idiom_id: rf-dart-sumleaf-with-list-no-eq-to-csharp-class-ireadonlylist
    research_finding_id: rf-dart-list-literal-to-csharp-list-of-T
    nuance: >-
      List-literal nuance (cached): Dart `[ConstTerm(42)]` -> C#
      `new List<Term> { new ConstTerm(42) }`. Assignable to
      `IReadOnlyList<Term>` (the SUT `Args` field type) covariantly.
      Literal-boxing nuance: Dart `42` is `int` (64-bit on VM); for
      `ConstTerm` payload the value is `object?`, so the C# literal
      `42` (int32) boxes into `object?` directly — no `long`
      widening (different from address-arithmetic widening pinned by
      cells.dart.md). String literal `'alice'` -> `"alice"` (single
      to double quote). Reference-identity nuance (carry-forward):
      `StructTerm` and `ConstTerm` are sealed C# classes (NOT
      records) preserving reference identity per terms.dart.md.

  - construct_key: dart.constructor_call.scheduler_named_args
    source_form: |-
      "final scheduler = Scheduler(rt: rt);
       final scheduler = Scheduler(rt: rt, traceSink: (s) => trace.add(s));"
    target_decision: >-
      Dart `Scheduler({required GlpRuntime rt, BytecodeRunner?
      runner, Map<Object?, BytecodeRunner>? runners, void
      Function(String)? traceSink})` maps to C# `new Scheduler(rt,
      traceSink: <lambda>)` per lib/runtime/scheduler.dart.md
      construct `dart.class.scheduler_master_drain_loop`. The
      converted constructor is `public Scheduler(GlpRuntime rt,
      BytecodeRunner? runner = null, Dictionary<object?,
      BytecodeRunner>? runners = null, Action<string>? traceSink
      = null)` (scheduler.dart.md pins the parameter shape).
      Callsite #1 (`Scheduler(rt: rt)`) -> `new Scheduler(rt: rt)`
      (or positional `new Scheduler(rt)`). Callsite #2 with
      `traceSink:` -> `new Scheduler(rt: rt, traceSink: s =>
      trace.Add(s))`.
    idiom_id: rf-dart-required-named-args-to-csharp-named-args
    research_finding_id: rf-dart-required-named-args-to-csharp-named-args
    nuance: >-
      Required-named-arg nuance (cached, see activateModule
      construct above). traceSink nuance: scheduler.dart.md pins
      `traceSink` as `Action<string>?` (Dart `void Function(String)?`
      -> .NET `Action<string>?` per the nullable delegate idiom).
      The lambda `(s) => trace.add(s)` maps to C# `s => trace.Add(s)`
      (identical arrow syntax modulo PascalCase on `Add`). Subscription
      model nuance: scheduler.dart.md construct (e) records "single-
      subscriber delegate; setter replaces" — this file only ever
      ASSIGNS once at construction time, so the single-subscriber
      contract is honoured. Trace-collection nuance: `trace.add(s)`
      mutates a `List<String>` held by closure capture — see the
      list-literal/list-add construct below.

  - construct_key: dart.local_var.empty_typed_list_literal
    source_form: "final trace = <String>[];"
    target_decision: >-
      Dart `<String>[]` (empty typed list literal) -> C# `new
      List<string>()` (the canonical empty mutable list). Used
      ONCE (in the e2e test). The `final` keyword maps to `var`
      per the local-var idiom; the variable is never reassigned
      but the LIST CONTENTS are mutated via `.Add(...)`.
    idiom_id: rf-dart-typed-empty-list-literal-to-csharp-new-list-of-T
    research_finding_id: rf-dart-typed-empty-list-literal-to-csharp-new-list-of-T
    nuance: >-
      FIRST-SEEN-here idiom row (distinguish from the populated
      list literal `[ConstTerm(42)]` cached above). Empty-literal
      nuance (explicitly addressed): Dart `<T>[]` is the empty
      typed-mutable-list literal; C# `new List<T>()` is the
      canonical empty mutable list (Microsoft Learn "List<T>" —
      https://learn.microsoft.com/dotnet/api/system.collections.
      generic.list-1). Alternative `[] as List<string>` works in C#
      12+ collection-expressions but is not yet uniform across the
      conversion target — codegen sticks with `new List<string>()`.
      Mutation nuance: `trace.add(s)` (Dart) -> `trace.Add(s)` (C#)
      preserves the list-mutating contract; the closure captures
      `trace` by reference in both languages.

  - construct_key: dart.lambda.single_arg_arrow_invoking_list_add
    source_form: "(s) => trace.add(s)"
    target_decision: >-
      Dart `(s) => trace.add(s)` (single-arg arrow lambda) maps to
      C# `s => trace.Add(s)` — identical arrow syntax; the
      argument type is INFERRED in both languages (Dart `dynamic`
      / contextual `String`; C# from the `Action<string>` parameter
      type at the callsite). Body is a single expression (`trace.
      add(s)`) — both languages permit expression-bodied lambdas.
      The closure captures `trace` by reference (Dart closures
      capture by reference; C# also captures by reference for
      reference-type variables — explicitly addressed below).
    idiom_id: null
    research_finding_id: rf-dart-arrow-lambda-to-csharp-lambda
    nuance: >-
      Closure-capture nuance (explicitly addressed): Dart closures
      capture local variables by REFERENCE (so mutations from
      inside the lambda are visible to outer scope). C# closures
      also capture by reference (the compiler hoists the captured
      local into a synthetic closure-class field) — Microsoft Learn
      "Lambda expressions" + "capture of outer variables":
      https://learn.microsoft.com/dotnet/csharp/language-reference/
      operators/lambda-expressions. The `trace.add(s)` mutation
      from inside the scheduler's `traceSink` callback is therefore
      observable on the outer-scope `trace` list — semantics
      preserved. Lambda-vs-method-group nuance: Dart `(s) =>
      trace.add(s)` could be writtin in C# as the method-group
      `trace.Add` directly (when the delegate type aligns:
      `Action<string>` matches `void Add(string)`); codegen MAY
      simplify to `trace.Add` — both forms compile equivalently.

  - construct_key: dart.method_call.scheduler_drain_with_status
    source_form: |-
      "scheduler.drainWithStatus(maxCycles: 100);
       scheduler.drainWithStatus(maxCycles: 200);
       scheduler.drainWithStatus(maxCycles: 500, debug: true);"
    target_decision: >-
      Dart `drainWithStatus({int maxCycles = ..., bool debug =
      false})` returns the `DrainResult` record. Per lib/runtime/
      scheduler.dart.md the C# mapping is `scheduler.
      DrainWithStatus(maxCycles: 100)` (preserve named argument
      for clarity) returning `DrainResult` (reference-type
      record class per scheduler.dart.md). All three callsites:
      `scheduler.DrainWithStatus(maxCycles: 100)`,
      `scheduler.DrainWithStatus(maxCycles: 200)`,
      `scheduler.DrainWithStatus(maxCycles: 500, debug: true)`.
      Method casing PascalCased (rf-dart-instance-method-camel-to-
      pascal).
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Optional-named nuance (carry-forward): Dart optional-named
      parameters with defaults -> C# optional named parameters
      with defaults (parameter signatures pinned by scheduler.dart.
      md). Reassignment nuance: every test reassigns `var result =
      scheduler.DrainWithStatus(...)` multiple times — both
      languages permit reassignment of a `var`-typed local.
      Concurrency-INHERITED nuance: `DrainWithStatus` mutates the
      `Scheduler` instance state and operates on `rt.heap` /
      `rt.gq` — see file-level escalation note (single test method,
      single thread per scheduler instance — satisfies the
      heap_fcp single-owner contract).

  - construct_key: dart.member_access.drain_result_status
    source_form: "result.status"
    target_decision: >-
      Dart `DrainResult.status` -> C# `DrainResult.Status` per
      scheduler.dart.md construct (b) `enum ExecutionStatus`
      and DrainResult class shape with `public ExecutionStatus
      Status { get; }`. PascalCase property access.
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Property-casing nuance (cached): Dart getter `result.status`
      (lowerCamelCase) -> C# property `result.Status` (PascalCase).
      Read-only-by-design nuance: scheduler.dart.md pins
      `DrainResult.Status` as `{ get; }` (immutable record) —
      the source code only reads it; mapping is read-only-
      consistent.

  - construct_key: dart.enum_member_access.execution_status_succeeded
    source_form: "ExecutionStatus.succeeded"
    target_decision: >-
      Dart `ExecutionStatus.succeeded` (enum-value access) -> C#
      `ExecutionStatus.Succeeded` per scheduler.dart.md construct
      `enum ExecutionStatus { succeeded, failed, suspended }` ->
      C# `public enum ExecutionStatus { Succeeded, Failed,
      Suspended }`. PascalCase on enum members per Microsoft
      naming guidelines (Microsoft Learn "Enum design":
      https://learn.microsoft.com/dotnet/standard/design-guidelines/
      enum). NOTE: this casing rule differs from cells.dart.md /
      binding_pointer_test where `CellTag.ValueTag` was preserved
      verbatim — `CellTag` members were ALREADY PascalCase in the
      Dart source (`ValueTag`, `WrtTag`); here the Dart members
      are LOWER-case (`succeeded`, `failed`, `suspended`) per Dart
      enum convention. The lib spec is authoritative.
    idiom_id: rf-dart-plain-enum-to-csharp-enum
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Casing-correction nuance (explicitly addressed): Dart enum
      members idiomatically lowerCase (`succeeded`); C# enum
      members PascalCase (`Succeeded`). The codegen casing rule
      applies WHEN the Dart source uses lowerCase (per Dart
      convention) — preserve PascalCase if the Dart source
      ALREADY uses PascalCase (as `CellTag.ValueTag` did).
      scheduler.dart.md pins the PascalCase C# rename. All seven
      uses in this file (`equals(ExecutionStatus.succeeded)` ×7
      across the five tests) collapse to the same C# enum literal
      `ExecutionStatus.Succeeded`.

  - construct_key: dart.member_access.glp_channel_handle_send
    source_form: |-
      "channel.send(goal);
       channel.send(StructTerm('greet', [ConstTerm('alice')]));
       channel.send(StructTerm('farewell', [ConstTerm('bob')]));
       channel.send(StructTerm('greet', [ConstTerm('carol')]));
       channel.send(StructTerm('process', [ConstTerm(1)]));
       channel.send(StructTerm('process', [ConstTerm(42)]));"
    target_decision: >-
      Dart `GlpChannelHandle.send(Term goal) -> List<GoalRef>`
      mapped to C# `channel.Send(goal) -> List<GoalRef>` per lib/
      runtime/glp_activation.dart.md `GlpChannelHandle` class. The
      method MUTATES the handle (`_writerAddr` advanced per
      glp_activation.dart.md) and returns the activation list.
      Callsite shape: `var activations = channel.Send(goal);` (or
      `var activations = channel.Send(new StructTerm(...));`).
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Method-mutation nuance (explicitly addressed): `send`
      mutates `_writerAddr` in place; the C# `class GlpChannel
      Handle` is a reference type (NOT a `struct` / `record
      struct` — pinned by glp_activation.dart.md to preserve
      reference identity). Codegen MUST keep `channel` as a
      single reference-typed local across all `Send` calls within
      a test method; multiple `Send` calls observe the advanced
      writer position naturally. Return-type nuance: `List<GoalRef>`
      maps to .NET `List<GoalRef>` (cached idiom rf-dart-list-of-T-
      to-csharp-list-of-T from heap_fcp.dart.md). Carry-forward
      from glp_activation.dart.md.

  - construct_key: dart.member_access.glp_channel_handle_close
    source_form: "channel.close();"
    target_decision: >-
      Dart `GlpChannelHandle.close() -> List<GoalRef>` -> C#
      `channel.Close() -> List<GoalRef>` per glp_activation.dart.
      md. Callsite shape: `var activations = channel.Close();`.
      Used in two tests (the close-channel test + the e2e test).
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Terminal-mutation nuance (carry-forward from glp_activation.
      dart.md): `close()` binds the current writer to the nil
      tail constant and is "terminal" — the runtime drops the
      handle after `close()` per the docstring. The test code
      does not call `Send` after `Close` (correct usage); the
      C# spec records this contract but does NOT add a runtime
      guard (the docstring is the source of truth — no test
      asserts post-close behaviour).

  - construct_key: dart.member_access.glp_channel_handle_writer_addr_getter
    source_form: "channel.writerAddr"
    target_decision: >-
      Dart `int get writerAddr => _writerAddr;` -> C# `public long
      WriterAddr { get; }` per glp_activation.dart.md. The
      callsite `channel.writerAddr` -> `channel.WriterAddr`.
      Address-width nuance: `writerAddr` is an `int` in Dart
      (64-bit on VM) -> `long` in C# per the cells.dart.md
      `dart.int.fixed_width_identity_field` precedent (rf-dart-int-
      to-csharp-long-width).
    idiom_id: rf-dart-int-to-csharp-long-width
    research_finding_id: rf-dart-int-to-csharp-long-width
    nuance: >-
      Address-width nuance (cached): every heap-address field
      narrows to C# `long` (not `int`) to preserve 64-bit
      pointer arithmetic. The single use in this file (`expect(
      channel.writerAddr, isNonNegative)`) is a value test — see
      `dart.package_test.expect_isNonNegative` below — and is
      `long`-aware.

  - construct_key: dart.member_access.glp_runtime_runners_contains_key
    source_form: "rt.runners.containsKey(mods.serve)"
    target_decision: >-
      Dart `Map<Object?, BytecodeRunner>.containsKey(Object?)` ->
      C# `Dictionary<object?, BytecodeRunner>.ContainsKey(object?)`
      per lib/runtime/runtime.dart.md construct `final Map<Object?,
      BytecodeRunner> runners = {}`. Callsite: `rt.Runners.
      ContainsKey(mods.Serve)` — `Runners` property name
      PascalCased; argument is the `BytecodeProgram` reference
      from the tuple field `Serve`. The Dart `Map.containsKey`
      and C# `Dictionary.ContainsKey` both use reference-equality
      for `Object?`/`object?` keys (since `BytecodeProgram` has
      no `==` override per lib/bytecode/runner.dart.md).
    idiom_id: rf-dart-map-containskey-to-csharp-dictionary-containskey
    research_finding_id: rf-dart-map-containskey-to-csharp-dictionary-containskey
    nuance: >-
      Map-vs-Dictionary nuance (explicitly addressed): Dart
      `Map<K,V>.containsKey(K? key)` accepts a nullable key; C#
      `Dictionary<K,V>.ContainsKey(K key)` requires non-null
      (throws `ArgumentNullException` on null). Per runtime.dart.
      md the `runners` map allows `null` keys, so a small wrapper
      may be needed at the SUT side — at THIS callsite the key
      is a non-null `BytecodeProgram` reference (`mods.serve`),
      so direct `ContainsKey` is safe. Key-equality nuance:
      Dart `Map` uses `==`/`hashCode`; C# `Dictionary` uses
      `IEqualityComparer<TKey>` (default `EqualityComparer<TKey>.
      Default` -> `object.Equals` -> reference identity for
      non-overridden classes). Both languages observe reference
      identity here — semantics preserved.

  - construct_key: dart.member_access.glp_runtime_goal_queue_length
    source_form: "rt.gq.length"
    target_decision: >-
      Dart `GoalQueue.length` -> C# `GoalQueue.Length` per lib/
      runtime/machine_state.dart.md construct `class GoalQueue`
      surface (`length` getter -> `Length` property mapped to
      `_q.Count`). Per machine_state.dart.md the chosen mapping
      is `public int Length => _q.Count;` (NOT `Count` — the
      machine_state spec explicitly keeps the Dart `length`
      naming as `Length` to honour minimum surface diversion).
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Length-vs-Count nuance (explicitly addressed, differs from
      `Args.length -> Args.Count` in binding_pointer_test where
      the SUT field type was `IReadOnlyList<T>` which exposes
      `Count`): for `GoalQueue` the SUT spec (machine_state.dart.
      md) pins the public surface as `Length`, NOT `Count`. The
      property delegates internally to `_q.Count` on the wrapped
      `Queue<GoalRef>`. Codegen MUST match the lib-spec surface
      (`Length`, not `Count`).

  - construct_key: dart.method_call.glp_runtime_goal_queue_enqueue
    source_form: "rt.gq.enqueue(act);"
    target_decision: >-
      Dart `GoalQueue.enqueue(GoalRef r)` -> C# `GoalQueue.
      Enqueue(GoalRef r)` per lib/runtime/machine_state.dart.md
      construct `class GoalQueue` (Dart `enqueue` -> .NET
      `Enqueue` — semantic match for FIFO insertion). Used inside
      `for (final act in activations) { rt.gq.enqueue(act); }`
      blocks following every `channel.send`/`channel.close`.
    idiom_id: rf-dart-instance-method-camel-to-pascal
    research_finding_id: rf-dart-instance-method-camel-to-pascal
    nuance: >-
      Goal-ref value-type nuance (carry-forward): `GoalRef` is a
      `readonly record struct` per machine_state.dart.md — the
      `Enqueue(act)` call boxes nothing because the queue is
      `Queue<GoalRef>` (concrete value-type element). Loop nuance
      (see below): the `for-in` traversal iterates the activation
      list and copies each `GoalRef` value into the queue.

  - construct_key: dart.statement.for_in_loop_over_iterable
    source_form: |-
      "for (final act in activations) {
         rt.gq.enqueue(act);
       }"
    target_decision: >-
      Dart `for (final <var> in <iterable>) { ... }` maps to C#
      `foreach (var <var> in <iterable>) { ... }`. The Dart `final`
      iteration variable is non-reassignable; C# `foreach` ALSO
      makes the loop variable effectively read-only inside the
      loop body. Used FIVE times in this file (one in each of the
      four send/close tests, twice in the e2e test). Spec form:
      `foreach (var act in activations) { rt.Gq.Enqueue(act); }`.
    idiom_id: null
    research_finding_id: rf-dart-for-in-final-to-csharp-foreach-var
    nuance: >-
      FIRST-SEEN-here idiom row (statement-level loop, distinct
      from LINQ expression-level forms). For-in-vs-foreach nuance
      (explicitly addressed): Dart `for (final x in xs)` and C#
      `foreach (var x in xs)` are semantically equivalent —
      iterator-driven, no index, read-only loop variable in the
      body (Microsoft Learn "foreach statement":
      https://learn.microsoft.com/dotnet/csharp/language-reference/
      statements/iteration-statements#the-foreach-statement). The
      `activations` collection is `List<GoalRef>` (from the
      glp_activation `Send`/`Close` return type); both `for-in`
      (Dart) and `foreach` (C#) iterate it sequentially. Empty-
      collection nuance: when `activations` is empty (always for
      the e2e test's `close()` after drain, sometimes for `send`
      depending on suspension state), neither loop executes its
      body — semantics preserved.

  - construct_key: dart.package_test.expect_equals
    source_form: |-
      "expect(rt.gq.length, equals(1));
       expect(result.status, equals(ExecutionStatus.succeeded));    // ×7 across five tests"
    target_decision: >-
      Dart `expect(<actual>, equals(<expected>))` -> xUnit
      `Assert.Equal(<expected>, <actual>)` — ARGUMENT-ORDER FLIP.
      The `equals` matcher uses Dart `==` equality; `Assert.Equal`
      uses `IEquatable<T>.Equals` / `Object.Equals`. For
      `int`/`long`/enum comparisons in this file, both are
      observably equivalent.
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order nuance (cached, well-known footgun):
      `expect(actual, equals(expected))` -> `Assert.Equal(expected,
      actual)`. Reused verbatim from every prior test convspec
      (binding_pointer_test, partial_evaluator_test, etc.).
      Per-callsite mapping:
        `expect(rt.gq.length, equals(1))` ->
          `Assert.Equal(1, rt.Gq.Length);`
        `expect(result.status, equals(ExecutionStatus.succeeded))` ->
          `Assert.Equal(ExecutionStatus.Succeeded, result.Status);`
      Value-equality nuance: `int 1` vs `long Length` would
      typically widen — but `Assert.Equal<int>` and
      `Assert.Equal<long>` differ; codegen MUST use a `long` literal
      `1L` to match the SUT-side `long Length` return type and
      avoid the xUnit type-parameter overload mismatch (Microsoft
      Learn "xunit.Assert.Equal" overloads). Recommended C# form:
      `Assert.Equal(1L, rt.Gq.Length);`.

  - construct_key: dart.package_test.expect_equals_with_reason
    source_form: |-
      "expect(result.status, equals(ExecutionStatus.succeeded),
         reason: 'serve should suspend waiting for channel input');
       expect(result.status, equals(ExecutionStatus.succeeded),
         reason: 'serve should process goal and suspend on next channel element');
       expect(result.status, equals(ExecutionStatus.succeeded),
         reason: 'serve should terminate when channel is closed');
       expect(result.status, equals(ExecutionStatus.succeeded),
         reason: 'serve should terminate cleanly when channel closes');
       expect(traceStr, contains('serve'), reason: 'Trace should show serve reduction');"
    target_decision: >-
      Dart `expect(actual, matcher, reason: 'msg')` -> xUnit
      `Assert.Equal(<expected>, <actual>, <message?>)` — but
      xUnit's `Assert.Equal` does NOT accept a user-message
      parameter (Microsoft Learn confirms: only the `<expected>,
      <actual>` overloads exist for `Assert.Equal<T>`). To
      preserve the diagnostic message, codegen MUST use the
      xUnit `Assert.Equal(<expected>, <actual>)` form FOLLOWED
      by a comment OR switch to `Assert.True(<actual>.Equals(
      <expected>), "<reason>");` which DOES accept a message.
      PREFERRED form for THIS file: `Assert.Equal(<expected>,
      <actual>); // reason: <reason text>` — keeps the canonical
      assertion idiom and preserves the documentation as a
      sibling comment. Alternative form (Assert.True with
      message) is acceptable but loses xUnit's structured
      type-aware diff in failure output.
    idiom_id: null
    research_finding_id: rf-dart-expect-with-reason-to-xunit-comment-or-asserttrue
    nuance: >-
      FIRST-SEEN idiom row (reason-message handling). Reason-
      vs-message nuance (explicitly addressed): Dart `package:
      test` accepts a `reason:` parameter on `expect(...)`
      surfaced ONLY on failure; xUnit `Assert.Equal<T>(T
      expected, T actual)` has NO message overload (xunit/xunit
      issue tracker confirms this is intentional: messages on
      `Assert.Equal` were removed in v2 because the structured
      diff output was deemed more useful — see https://github.
      com/xunit/xunit/issues/350). Two recovery strategies are
      industry-standard: (a) inline `//` comment retaining the
      reason as code documentation; (b) `Assert.True(actual.
      Equals(expected), userMessage)` which DOES accept a
      message but loses the diff. This spec RECOMMENDS strategy
      (a) (comment + canonical Assert.Equal) because the reason
      strings in this file are documentation-grade ("serve should
      suspend waiting for channel input") — failure-output diff
      already conveys the same information in xUnit. The
      `contains` matcher case (last bullet) — `expect(traceStr,
      contains('serve'), reason: 'Trace should show serve
      reduction')` — uses `Assert.Contains(<substring>, <string>)`
      which ALSO has no message overload; same comment-retention
      strategy applies. Codegen MUST emit the reason comment so
      the documentation survives the conversion.

  - construct_key: dart.package_test.expect_contains_substring
    source_form: "expect(traceStr, contains('serve'), reason: 'Trace should show serve reduction');"
    target_decision: >-
      Dart `contains(<substring>)` matcher on a `String` actual
      -> xUnit `Assert.Contains(<substring>, <string>)` (Microsoft
      Learn xunit.Assert.Contains:
      https://learn.microsoft.com/dotnet/api/xunit.assert.contains).
      Used ONCE (the e2e test). Spec form: `Assert.Contains(
      "serve", traceStr); // reason: Trace should show serve
      reduction`.
    idiom_id: null
    research_finding_id: rf-dart-expect-contains-substring-to-xunit-assert-contains
    nuance: >-
      FIRST-SEEN idiom row (substring contains on a `String`,
      distinct from `Iterable` contains). String-contains nuance
      (explicitly addressed): Dart `contains(<sub>)` on a String
      reuses `String.contains(Pattern)`; xUnit `Assert.Contains
      (String expectedSubstring, String actualString)` is the
      String-pair overload. Case-sensitivity is preserved
      (default-case-sensitive in both languages). Iterable
      contains (`Assert.Contains<T>(T expected, IEnumerable<T>
      collection)`) is a DIFFERENT overload — codegen MUST
      route based on the actual's static type (here `String` ->
      String overload). Argument-order nuance (different from
      `equals`): both Dart `contains('serve')` and xUnit
      `Assert.Contains("serve", ...)` put the substring FIRST —
      no flip needed.

  - construct_key: dart.package_test.expect_isTrue
    source_form: "expect(rt.runners.containsKey(mods.serve), isTrue);"
    target_decision: >-
      Dart `expect(<bool-expr>, isTrue)` -> xUnit `Assert.True(
      <bool-expr>)`. Used ONCE (the first test). Spec form:
      `Assert.True(rt.Runners.ContainsKey(mods.Serve));`.
    idiom_id: rf-dart-expect-istrue-to-xunit-asserttrue
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      Cached idiom (precedent: binding_pointer_test.dart.md
      construct `dart.package_test.expect_isFalse_isTrue`). No
      message overload nuance: `Assert.True(bool condition,
      string userMessage)` DOES exist (unlike `Assert.Equal`) —
      so reason messages on `isTrue` callsites COULD be preserved
      directly. The single callsite here has NO `reason:` argument,
      so the simpler `Assert.True(...)` form is correct.

  - construct_key: dart.package_test.expect_isNonNegative
    source_form: "expect(channel.writerAddr, isNonNegative);"
    target_decision: >-
      Dart `isNonNegative` is a `Matcher` constant from
      `package:matcher` (re-exported by `package:test`) that
      asserts `actual >= 0`. xUnit has NO direct `Assert.
      NonNegative` equivalent — the canonical mapping is
      `Assert.True(<actual> >= 0)`. For the file's single use
      (`channel.writerAddr` of type `long`), the spec form is
      `Assert.True(channel.WriterAddr >= 0);`. Alternative
      `Assert.InRange<long>(channel.WriterAddr, 0L,
      long.MaxValue)` ALSO works (Microsoft Learn xunit.Assert.
      InRange — generic) but is more verbose; PREFERRED form is
      `Assert.True(... >= 0)` for parsimony.
    idiom_id: null
    research_finding_id: rf-dart-expect-isNonNegative-to-xunit-asserttrue-ge-zero
    nuance: >-
      FIRST-SEEN idiom row. Matcher-vs-assertion nuance
      (explicitly addressed): Dart `package:matcher`
      (https://pub.dev/packages/matcher) provides a rich
      vocabulary of constant matchers (`isNonNegative`,
      `isPositive`, `isNegative`, `isZero`); xUnit's vocabulary
      is narrower and prefers explicit `Assert.True(<bool>)`
      with a relational expression. The four sibling matchers
      all map to `Assert.True(actual <op> 0)` with the
      appropriate operator. No diagnostic-message loss because
      the single use has no `reason:` argument. Width nuance:
      `channel.writerAddr` is `long` (per glp_activation.dart.
      md); the comparison `>= 0` uses C# integer-promotion
      rules — the literal `0` widens to `long` automatically.
      Codegen MAY emit `0L` for clarity; the default `0` works.

conversion_units:
  - "cu-1: file-scope using directives (using Xunit; using System.Collections.Generic; using <RootNs>.Compiler; using <RootNs>.Runtime; using <RootNs>.Bytecode;)"
  - "cu-2: namespace declaration mirroring test/runtime (e.g. <RootNs>.Test.Runtime)"
  - "cu-3: helper static class `internal static class ModuleActivationTestHelpers` (sibling to the test class, same file/namespace) — holds (a) the `ServeSource` const string (verbatim multi-line literal of the GLP serve/2 source, emitted as `@\"…\"`) and (b) the `CompileModules` static method returning a named ValueTuple (BytecodeProgram Serve, BytecodeProgram Target); both with their /// <summary> doc-comments"
  - "cu-4: top-level test class `public class ModuleActivationTests` — no constructor (no shared state, no `late` field, no Dart `setUp`); class-level XML doc-comment preserves the outer group label 'Module activation via GLP'"
  - "cu-5: 5 [Fact] instance methods, each with [Fact(DisplayName = '<original label>')] — ActivateModuleSpawnsServeOnChannelSuspendsWaitingForInput, SendSingleRpcGoalOnChannelVerifyItExecutes, SendMultipleRpcGoalsOnChannel, CloseChannelAfterSendingGoalsServeTerminates, FullEndToEndActivateSendRpcCloseVerifyDispatchChain"
  - "cu-6: per-method body — arrange (per-test-local `var mods = ModuleActivationTestHelpers.CompileModules(@\"…\");`, `var rt = new GlpRuntime();`, `var channel = GlpActivation.ActivateModule(...)` with named args, `var scheduler = new Scheduler(...)` with optional `traceSink` lambda + `var trace = new List<string>();` in the e2e test); act (sequence of `var result = scheduler.DrainWithStatus(maxCycles=N);`, `var activations = channel.Send(...);`, `var activations = channel.Close();`, `foreach (var act in activations) rt.Gq.Enqueue(act);`); assert (Assert.Equal with reason-comments, Assert.True ContainsKey/>=0, Assert.Contains substring)"
  - "cu-7: NO `using System.Linq;` required (no LINQ surface — only foreach + ContainsKey + Add); NO `using System;` required at file scope (xUnit + collections cover the namespace surface used here)"

escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative — cached)

`package:test` -> xUnit pinning is the project-wide policy; every
prior `package:test` convspec REUSES `rf-dart-package-test-import-
to-xunit-using` without re-research (FR-012 / SC-007). Authoritative
basis: xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / `DisplayName`; Dart `package:test` docs
(`https://pub.dev/packages/test`) for `group` / `test` / `expect` /
matcher semantics. Single-group FLATTEN is the simpler half of the
group-block idiom (precedent: `partial_evaluator_test.dart.md`,
`module_parser_test.dart.md` single-group case).

### Multi-line GLP source as a C# verbatim const string

Dart `const String serveSource = '''…''';` at file scope -> C# `internal
const string ServeSource = @"…";` inside `internal static class
ModuleActivationTestHelpers`. The triple-quoted Dart literal is
non-interpolating, contains no backslash escapes, contains no
double-quotes — perfectly compatible with the C# verbatim form
`@"…"` (Microsoft Learn "String and verbatim string literals":
`https://learn.microsoft.com/dotnet/csharp/language-reference/
tokens/verbatim`). Leading newline preserved as a literal newline in
the verbatim form — the downstream GLP parser tolerates leading
whitespace. The file-scope vs. class-scope nuance reuses the static-
helper-class idiom established by `module_compiler_test.dart.md`
construct `dart.toplevel.helper_function`.

### Named record return type — `({BytecodeProgram serve, BytecodeProgram target})`

This is the FIRST-SEEN named-record return in the inventory (the
prior heap_fcp `dart.tuple_return.record_two_int_addresses_
allocate_variable` was a POSITIONAL `(int, int)` record, mapped to
`(long, long)` via `rf-dart-record-return-to-csharp-valuetuple`).
Dart-3 named records map to C# named ValueTuples cleanly: the
declaration `({BytecodeProgram serve, BytecodeProgram target}) f(…)`
becomes `(BytecodeProgram Serve, BytecodeProgram Target) F(…)`
(Microsoft Learn "Tuple types — Named field names":
`https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-
types/value-tuples#tuple-field-names`). Field-name casing follows
PascalCase (.NET capitalisation guidelines). Callsite access
(`mods.serve` -> `mods.Serve`) follows the same casing convention.
Allocation is identical — both are value-type tuples carrying two
references.

### `activateModule` named-required args -> C# named args

Dart `activateModule({required …})` -> C# positional method on the
static helper class `GlpActivation` (per glp_activation.dart.md).
C# supports named arguments at callsites even for non-optional
positional parameters (Microsoft Learn "Named and optional arguments":
`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-
and-structs/named-and-optional-arguments`). The codegen preserves the
named labels at callsite for clarity. `required` modifier nuance:
C# 11 introduced a `required` modifier — but ONLY for properties /
fields, NOT parameters. Parameter-required enforcement comes from
non-defaulted positional parameters; the lib spec already pins this.

### `traceSink` lambda + closure-captured List

The e2e test's `(s) => trace.add(s)` lambda + `final trace = <String>[];`
list combine to test the `Scheduler.traceSink` subscription path. The
C# mapping uses `Action<string>` (Dart `void Function(String)` ->
.NET `Action<string>`) — pinned by scheduler.dart.md. Closure capture
of `trace` is by-reference in both languages, so the lambda mutates
the outer-scope list as expected (Microsoft Learn "Lambda expressions
— Capture of outer variables": `https://learn.microsoft.com/dotnet/
csharp/language-reference/operators/lambda-expressions#capture-of-
outer-variables`). Codegen MAY simplify `s => trace.Add(s)` to the
method-group `trace.Add` because the signatures align — both forms
are correct.

### Dart `containsKey` and the `Runners` map

`rt.runners.containsKey(mods.serve)` -> `rt.Runners.ContainsKey(mods.
Serve)` uses `Dictionary<object?, BytecodeRunner>.ContainsKey` per
runtime.dart.md. Reference-identity comparison (no `==` override on
`BytecodeProgram` per bytecode/runner.dart.md) is honoured by C#
default-equality (`EqualityComparer<TKey>.Default` falls back to
`object.Equals` which is reference identity for non-overridden classes
— Microsoft Learn "EqualityComparer<T>.Default":
`https://learn.microsoft.com/dotnet/api/system.collections.generic.
equalitycomparer-1.default`).

### `expect(..., equals(...), reason: ...)` — diagnostic-message loss handling

xUnit `Assert.Equal<T>(T expected, T actual)` has NO user-message
overload — the structured type-aware diff is considered more useful
(xunit/xunit issue tracker confirms this is intentional design:
`https://github.com/xunit/xunit/issues/350`). To preserve the Dart
`reason:` documentation, codegen emits an inline `//` comment
retaining the reason string immediately after the assertion. Alternative
strategy (`Assert.True(actual.Equals(expected), message)`) is recorded
for completeness but rejected for THIS file because the loss of
xUnit's structured diff outweighs the message preservation —
file-level documentation comment is the parsimonious choice. The
same comment-retention strategy applies to `Assert.Contains(...)`
(which also has no message overload). This is a FIRST-SEEN idiom
(`rf-dart-expect-with-reason-to-xunit-comment-or-asserttrue`) and
becomes the precedent for any future test convspec encountering
`expect(..., matcher, reason: ...)`.

### `isNonNegative` -> `Assert.True(actual >= 0)`

The Dart `isNonNegative` matcher constant (from `package:matcher`,
re-exported by `package:test`) asserts `actual >= 0`. xUnit has no
direct equivalent — the canonical mapping is `Assert.True(<actual>
>= 0)`. Alternative `Assert.InRange<T>(actual, 0, T.MaxValue)` is
recorded but rejected for parsimony. FIRST-SEEN idiom row
(`rf-dart-expect-isNonNegative-to-xunit-asserttrue-ge-zero`).
Address-width nuance: `channel.writerAddr` is `long` per glp_
activation.dart.md, so the comparison uses long-arithmetic; the
literal `0` widens automatically; codegen MAY emit `0L` for clarity.

### `for (final act in activations) { rt.gq.enqueue(act); }`

The five `for-in` loops in this file (one per send/close + two in
the e2e test) all map to C# `foreach (var act in activations) {
rt.Gq.Enqueue(act); }`. Dart `for-in-with-final-binding` ->
C# `foreach-with-implicit-readonly` is semantically equivalent
(Microsoft Learn "foreach statement":
`https://learn.microsoft.com/dotnet/csharp/language-reference/
statements/iteration-statements#the-foreach-statement`). FIRST-SEEN
idiom row (`rf-dart-for-in-final-to-csharp-foreach-var`) — statement-
level loop, distinct from LINQ expression-level traversal patterns
(`Select`, `First`, etc.).

### Module-activation pipeline: deep-analysis on the runtime model

The five tests in this file exercise the full GLP module-activation
pipeline:

1. **Test 1** (`activateModule spawns serve on channel`) — validates
   the post-`activateModule` runtime state: `Runners` map contains
   the `serve` bytecode, `GoalQueue` length is 1 (the spawned serve
   goal), `WriterAddr` is non-negative (valid heap address), and the
   scheduler drain succeeds while serve is suspended on an empty
   channel.

2. **Tests 2–4** (`send single`, `send multiple`, `close channel`)
   — validate the channel send/receive protocol: each `channel.send
   (StructTerm('<proc>', [...args]))` returns activations, those are
   enqueued, the scheduler drains, serve processes the goal and
   suspends on the new channel tail. Multiple sends iterate this
   step. `channel.close()` binds the writer to nil, serve matches
   the `serve(_, [])` clause and terminates.

3. **Test 5** (full e2e) — adds a `traceSink` to inspect the
   dispatch chain; asserts `traceStr.Contains("serve")` to verify
   serve appears in the reduction trace.

The C# spec for each test method is **method-local** — no shared
class-level state. xUnit's per-test class instantiation gives the
isolation guarantee already; per-method `var` declarations match the
Dart `final` declarations. NO `IClassFixture<>` needed (no shared
state); NO `[Trait]` partitioning needed (single outer group).

### Module activation: library / part of / isolate-boot scope nuance (US2 AS4 explicit address)

**`library` / `part of` directives**: ABSENT from this file. The
source contains only `import` directives; no `library;` keyword
(unlike heap_fcp.dart and other lib files that carry the explicit
`library;` for doc-comment anchoring). No `part of` directive. The
namespace mapping is therefore driven purely by the
file-path-to-namespace convention (`test/runtime/module_activation_
test.dart` -> `<RootNs>.Test.Runtime.ModuleActivationTest.cs`). No
multi-file C# `partial class` synthesis is needed.

**Isolate boot**: NOT exercised — every test allocates its own
`GlpRuntime` (a single Dart isolate's runtime facade in the source
model). The Dart code does NOT use `Isolate.spawn`, `ReceivePort`,
`SendPort`, or any `dart:isolate` surface. The C# host therefore
does NOT need to model multi-isolate orchestration here — every
test is a single-isolate single-threaded equivalent that operates
on one `GlpRuntime` from one test method's call-stack. This satisfies
the heap_fcp single-owner contract (concurrency-model escalation —
see next section).

**Module-activation runtime semantics**: the `activateModule(...)`
function (per glp_activation.dart.md) creates a GLP channel
(writer/reader pair on `HeapFCP`), constructs a `ModuleTerm`, spawns
a `serve/2` goal, registers a serve runner on the `GlpRuntime.
runners` map, tags the goal as infrastructure, and returns a
`GlpChannelHandle`. The C# host preserves each step verbatim — the
SUT-side mapping is owned by glp_activation.dart.md. The TEST code
is a callsite-only client.

### INHERITED escalation: HeapFCP threading model (FR-013 — defer, do not re-escalate)

The transitively-included `lib/runtime/heap_fcp.dart` has an OPEN
escalation: `dart.heap_fcp.concurrency_model_thread_safety_for_
multiagent_hosting` (kind: `undecidable`) — see `.codeconv/conversion-
specs/lib/runtime/heap_fcp.dart.md:1420`. This test indirectly
exercises `HeapFCP` via the chain `Scheduler -> GlpRuntime.heap ->
HeapFCP` (per scheduler.dart.md, runtime.dart.md). Per the agent
instructions for this file:

> If this test depends on `runtime/scheduler.dart`, `runtime/
> system_predicates_impl.dart`, or `multiagent/mad_context.dart`,
> those modules INHERIT from the escalated `runtime/heap_fcp.dart`
> (HeapFCP threading model). If the test exercises threading-relevant
> behaviour, flag the dependency in `nuance` and DEFER to the heap_
> fcp ruling.

**This test does NOT exercise threading-relevant behaviour**: each
of the five tests runs on a single thread, owns its own `GlpRuntime`
(therefore its own `HeapFCP`), and never shares the heap across
threads. The C# port preserves this discipline trivially because
xUnit creates a fresh class instance per `[Fact]` and the test
method's local-stack ownership is single-threaded. **The escalation
is therefore INHERITED but NON-blocking for this file** — the test-
code conversion is fully decidable; only the future multi-agent
hosting concurrency model (which decides whether `HeapFCP` is
shared across .NET threads) remains escalated. Per FR-013 ("no
naive fallback") the spec records the inheritance in the relevant
construct nuances (`scheduler` / `glp_runtime` / `goal_queue`
construct rows above) and defers to the heap_fcp ruling. **DO NOT
re-escalate here** — the same undecidable point would duplicate the
escalation without adding information.

### Why no NEW escalations

Every construct has a clear, single-decision target shape grounded in
official Dart / .NET documentation, and every reuse cites either a
cached idiom (xUnit pinning, naming-convention, list-literal,
`Assert.Equal` argument-order flip, `Assert.True`/`Assert.Contains`,
`for-in` -> `foreach`) or a precedent-spec decision (every SUT type
touched here — `GlpCompiler`, `GlpRuntime`, `Scheduler`,
`ExecutionStatus`, `DrainResult`, `GoalQueue`, `GlpChannelHandle`,
`activateModule`, `BytecodeProgram`, `StructTerm`, `ConstTerm` —
is already convspec'd). The four FIRST-SEEN-here idiom rows
(`rf-dart-toplevel-const-multiline-string-to-csharp-helper-class-
verbatim-const`, `rf-dart-named-record-return-to-csharp-named-
valuetuple`, `rf-dart-required-named-args-to-csharp-named-args`,
`rf-dart-typed-empty-list-literal-to-csharp-new-list-of-T`,
`rf-dart-arrow-lambda-to-csharp-lambda`, `rf-dart-for-in-final-to-
csharp-foreach-var`, `rf-dart-expect-with-reason-to-xunit-comment-
or-asserttrue`, `rf-dart-expect-contains-substring-to-xunit-assert-
contains`, `rf-dart-expect-isNonNegative-to-xunit-asserttrue-ge-
zero`) are each grounded in official Microsoft Learn / xunit.net /
dart.dev documentation citations and decided in-spec; none are
undecidable. `escalations: []` is therefore intentional. The
INHERITED heap_fcp concurrency escalation is documented in the
relevant nuance fields and deferred to the heap_fcp ruling (NOT
duplicated here).
