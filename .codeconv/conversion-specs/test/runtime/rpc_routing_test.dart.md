# Conversion Spec — test/runtime/rpc_routing_test.dart

> Conversion-spec artifact for test/runtime/rpc_routing_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> THREADING-MODEL INHERITANCE NOTICE — this file exercises the
> `Scheduler`/`GlpRuntime`/`HeapFCP`/`GlpChannelHandle` cross-isolate
> RPC-routing surface. The .NET threading-model decision (single-
> owning-context vs `ConcurrentDictionary`/`Interlocked` etc.) is
> ESCALATED in `lib/runtime/heap_fcp.dart.md` escalations[0] and
> INHERITED through every dependent runtime spec
> (`lib/runtime/scheduler.dart.md`, `lib/runtime/runtime.dart.md`,
> `lib/runtime/glp_activation.dart.md`, `lib/bytecode/runner.dart.md`).
> Per FR-013 + the scheduler precedent, this file does NOT
> re-escalate the same question; it inherits the parent ruling.
> However, because the test directly observes the synchronous
> "drain → suspend / drain → succeed" contract of `drainWithStatus`
> AND directly observes the `rt.glpChannels` Map identity invariant,
> a deferred sub-escalation IS added (`escalations[0]` below):
> the `same(channel)` reference-identity assertion is only meaningful
> if the inherited threading-model ruling preserves reference identity
> of the channel handle stored in `rt.glpChannels`. That is the
> ONE undecidable point introduced at THIS file's boundary, gated on
> the heap_fcp.dart threading-model ruling.

```yaml
schema_version: 1
source_path: test/runtime/rpc_routing_test.dart
source_sha256: 3dedc5b118a3b9b0a1a2e94a6ddc7abceb28811e6c7d07f381ff1493ae5a98bb
target_code_unit: test/runtime/RpcRoutingTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. xUnit is the project-pinned
      .NET test framework (precedent: every prior `test/**` convspec —
      smoke_test.dart.md, binding_pointer_test.dart.md, every
      test/multiagent/*.dart.md, every test/module/*.dart.md, every
      test/bytecode/*.dart.md). Reuse verbatim; no re-research
      (FR-024 cache hit; FR-012 SC-007 reuse).
    idiom_id: null
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Project-wide policy nuance — every `package:test` file in the
      inventory MUST map to the SAME .NET framework so test discovery,
      runner config, and attribute vocabulary stay consistent. THIS
      file is synchronous (no `async` / `Future` in any callback) so
      no `[Fact] async Task` shape is required.
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/compiler/compiler.dart';
       import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/terms.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';
       import 'package:glp_runtime/runtime/scheduler.dart';
       import 'package:glp_runtime/runtime/glp_activation.dart';
       import 'package:glp_runtime/bytecode/runner.dart';"
    target_decision: >-
      Seven `package:glp_runtime/...` imports collapse to ≤3 `using`
      directives in the converted file because the converted target
      namespaces are shared across multiple Dart files (one `using`
      per namespace per C# convention). Expected collapse: the four
      `runtime/*.dart` imports (runtime, terms, machine_state,
      scheduler, glp_activation — five Dart files) → ONE `using
      <RootNs>.Runtime;`; the `compiler/compiler.dart` import → ONE
      `using <RootNs>.Compiler;`; the `bytecode/runner.dart` import
      → ONE `using <RootNs>.Bytecode;`. The exact namespace strings
      are owned by the SUT specs:
      `.codeconv/conversion-specs/lib/runtime/{runtime,terms,
      machine_state,scheduler,glp_activation}.dart.md`,
      `.codeconv/conversion-specs/lib/compiler/compiler.dart.md`,
      `.codeconv/conversion-specs/lib/bytecode/runner.dart.md`.
      THIS spec records only the shape of the cross-file dependency.
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance — Dart `package:` imports are
      per-file; C# `using` is per-namespace. Multiple Dart imports
      that converge into the same target namespace collapse. The
      five `runtime/` imports collapsing to one `using` is the
      load-bearing case here. ReplModuleContext / ReplModuleTarget
      (used unqualified in the body) live in the Bytecode namespace
      per bytecode/runner.dart.md — the `using <RootNs>.Bytecode;`
      covers them.
  - construct_key: dart.top_level_const_string.triple_quoted_glp_source_template
    source_form: >-
      "const serveSource = '''
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
      Dart top-level `const String` initialised from a triple-quoted
      multi-line string maps to C# `private const string serveSource
      = @"..."` (verbatim string literal) declared as a `private const`
      field on the converted test class `RpcRoutingTest`. The Dart
      triple-quoted literal preserves embedded newlines and single-
      quote inner characters (the GLP source contains single-quoted
      atoms like `'_activate'`); the C# `@"..."` verbatim form
      preserves embedded newlines AND requires the doubled-quote
      escape `""` for inner double quotes — BUT the GLP source uses
      only SINGLE quotes inside, so the C# verbatim form needs NO
      escapes at all. Codegen MUST emit the GLP body byte-for-byte
      (same line breaks, same whitespace) so the at-runtime compile
      of the GLP source produces an identical bytecode artefact.
      `const` (vs `static readonly`) is correct because the value is
      a compile-time string literal — C# `const string` is the
      documented counterpart (Microsoft Learn:
      `https://learn.microsoft.com/dotnet/csharp/programming-guide/
      classes-and-structs/constants`).
    idiom_id: null
    research_finding_id: rf-dart-top-level-const-string-multiline-to-csharp-const-verbatim
    nuance: >-
      Top-level-const placement nuance (explicitly addressed): Dart
      `const serveSource = '''...''';` is a top-level (file-level)
      declaration; C# has no top-level fields outside a type — codegen
      MUST host the constant either as a `private const string` on
      the test class (preferred — keeps it co-located with the only
      callers, the five `[Fact]` methods that all reference it) or
      as a static readonly on a co-file `internal static class`
      holder (rejected — adds boilerplate and the constant is used
      ONLY in this file). Newline-preservation nuance: the embedded
      GLP source is parsed by `GlpCompiler` at test runtime; ANY
      change in line breaks or leading whitespace would alter the
      compiled bytecode and INVALIDATE the assertions. `@"..."`
      verbatim preserves exactly. Quote-escape nuance: GLP atoms
      use single quotes (`'_activate'`); C# verbatim strings only
      escape double quotes via `""` — therefore no escape needed.
      Interpolation nuance: the Dart string is NOT interpolated
      (no `$`), so codegen MUST use the verbatim form `@"..."` and
      NOT the interpolated form `$@"..."` (interpolated would have
      to escape `{` / `}` if any GLP brace appeared in a future
      revision — currently none).
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('Phase 5: RPC routing via GLP channels', () { test(...) ×5 }); }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint;
      xUnit discovers `[Fact]` methods by reflection — there is NO
      per-file entrypoint to emit. Eliminate `main` entirely; its
      body (one `group(...)` call containing five `test(...)` calls)
      becomes the enclosing test class (see `dart.package_test.
      group_block` below).
    idiom_id: null
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Cached idiom (precedent: every prior `package:test` file).
      Lifecycle nuance: Dart `main` is invoked once per test-file
      process; xUnit has no per-file hook. THIS file's `main` body is
      exactly one `group(...)` call with no file-level `setUp`/no
      shared state across tests, so the omission is lossless — no
      migration into `IClassFixture<>` needed.
  - construct_key: dart.package_test.group_block_single_with_label_phase_prefix
    source_form: >-
      "group('Phase 5: RPC routing via GLP channels', () {
         test('Distribute routes via GLP channel when target is activated', () { ... });
         test('Distribute routes via GLP channel with debug trace', () { ... });
         test('multiple Distribute RPCs route through GLP channel', () { ... });
         test('activateModule registers channel in glpChannels', () { ... });
         test('close channel after RPC routing, serve terminates', () { ... });
       });"
    target_decision: >-
      ONE top-level `group(...)` containing five tests maps to ONE
      PascalCase xUnit test class `RpcRoutingTest` (the file-name
      mirror) containing five `[Fact]`-decorated methods. The single
      group's label `'Phase 5: RPC routing via GLP channels'` is
      preserved as `[Trait("Group", "Phase 5: RPC routing via GLP
      channels")]` on every test method belonging to the group (one
      group ⇒ uniform trait across all five methods; the trait is
      strictly redundant for THIS file because there is only one
      group, but it preserves the original label verbatim and
      remains consistent with the `[Trait]` convention used in every
      sibling test convspec). Per-test method names are PascalCased,
      identifier-safe forms of each test label, with the original
      label preserved via `[Fact(DisplayName = "<original label>")]`.
      Method-name proposals:
      `DistributeRoutesViaGlpChannelWhenTargetIsActivated`,
      `DistributeRoutesViaGlpChannelWithDebugTrace`,
      `MultipleDistributeRpcsRouteThroughGlpChannel`,
      `ActivateModuleRegistersChannelInGlpChannels`,
      `CloseChannelAfterRpcRoutingServeTerminates`.
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Single-group-no-nesting nuance (explicitly addressed, differs
      from boot_loader_test which had nested groups, matches the
      simple single-group precedent of fairness_scheduler_loop_test).
      Label-mangling nuance: the colon (`Phase 5:`), spaces, and
      hyphens are all stripped to PascalCase; `DisplayName` preserves
      the colon and original casing. The "Phase 5" prefix is
      preserved in the display name (provenance: ties this group to
      a specific implementation phase of dynamic-module dispatch,
      docs/modules/dynamic-dispatch-implementation-plan.md) — codegen
      MUST NOT silently strip it.
  - construct_key: dart.package_test.test_call_synchronous_closure
    source_form: "test('<label>', () { /* compile GLP sources, set up runtime, drain scheduler, assert */ });"
    target_decision: >-
      Each Dart `test(label, body)` with a synchronous closure body
      (no `async` / `Future`) becomes a `public void` instance
      method on `RpcRoutingTest`, decorated with `[Fact(DisplayName
      = "<original label>")]` plus the single shared `[Trait("Group",
      "Phase 5: RPC routing via GLP channels")]`. The method body
      converts the closure body statement-for-statement. All five
      tests are synchronous (no awaits anywhere) — every method
      returns `void`, NOT `async Task`. Each test starts with its
      OWN `var compiler = new GlpCompiler();` and `var rt = new
      GlpRuntime();` — NO shared `_compiler` / `_rt` constructor
      field — preserving per-test isolation via xUnit's per-test
      class instantiation contract.
    idiom_id: null
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      No-shared-state nuance (explicitly addressed): every test
      allocates its own `GlpCompiler` and `GlpRuntime` inline.
      Codegen MUST NOT hoist these into a constructor field — the
      per-test isolation is structurally documented by the inline
      construction, and the heap_fcp.dart threading-model concern
      (escalations[0]) is LOCALLY moot because each test runs on a
      fresh `HeapFCP` instance owned exclusively by the test method
      (and xUnit creates a new test-class instance per test by
      default). Synchronous-body nuance: NO `async Task` signature
      is emitted; `drainWithStatus` is defined as a SYNCHRONOUS
      method on `Scheduler` per scheduler.dart.md — preserving
      that contract is load-bearing for this file's assertions
      (`var result = scheduler.drainWithStatus(maxCycles: 100);`
      is a blocking call; `result.Status` is observed synchronously
      on the very next line).
  - construct_key: dart.local_var.final_constructor_glp_compiler
    source_form: "final compiler = GlpCompiler();"
    target_decision: >-
      Dart `final compiler = GlpCompiler();` (type inferred) maps to
      C# `var compiler = new GlpCompiler();` (method-local lifetime).
      The SUT type is decided in `lib/compiler/compiler.dart.md`;
      this spec records only the call-site shape (no-args ctor,
      single use to call `.Compile(string source)`).
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      `final`-vs-`var` semantics nuance (cached idiom): Dart `final`
      is enforced single-assignment; C# `var` is not. Each test
      reassigns nothing — the single-assignment pattern is preserved
      by the converted body. No `readonly` keyword on a local
      (illegal in C#). PascalCase nuance on the constructor:
      Dart `GlpCompiler()` already PascalCase → identical
      `new GlpCompiler()` in C#.
  - construct_key: dart.method_call.glp_compiler_compile_triple_quoted_source
    source_form: >-
      "compiler.compile('''
       exported procedure process(Any?).
       process(_) :- otherwise | true.
       ''');"
    target_decision: >-
      `compiler.Compile(@"...")` — PascalCase method name and verbatim
      string literal preserving embedded newlines. The SUT method's
      signature decision (return type, parameter type) is owned by
      `lib/compiler/compiler.dart.md`. From the test's usage the
      converted signature must satisfy: `BytecodeProgram Compile
      (string source)` — synchronous, returning a `BytecodeProgram`
      that exposes a `Labels` collection (`labels.containsKey(...)`)
      and a `Labels[...]` indexer returning a `long` PC.
    idiom_id: null
    research_finding_id: rf-dart-method-call-snake-to-pascal
    nuance: >-
      Naming-convention nuance: Dart `lowerCamelCase` method →
      C# `PascalCase`; `compile` → `Compile`. Triple-quoted argument
      nuance (cross-reference): the inline `'''...'''` GLP source
      is a Dart triple-quoted literal — at every call site (4
      occurrences in this file, plus the top-level `serveSource`
      constant) it must convert to a C# verbatim `@"..."` preserving
      newlines exactly (see top-level-const construct above for the
      analysis). Calls in tests 1, 3, 5 share the same `process(_)
      :- otherwise | true.` pattern (de-duplication is OUT of scope
      — codegen MUST emit verbatim per-call literals to keep
      per-test isolation explicit). Test 3's `compiler.compile`
      call defines TWO exported procedures (`greet/1` and
      `farewell/1`) in one source string — the multi-procedure
      compilation is supported per compiler.dart.md.
  - construct_key: dart.package_test.expect_member_exists_containsKey
    source_form: >-
      "expect(aBytecode.labels.containsKey('caller/1'), isTrue);
       expect(rt.glpChannels.containsKey('target_b'), isTrue);
       expect(rt.glpChannels.isEmpty, isTrue);
       expect(rt.glpChannels.containsKey('my_module'), isTrue);"
    target_decision: >-
      Dart `expect(<bool-expr>, isTrue)` → xUnit `Assert.True(
      <bool-expr>)`. Dart `Map.containsKey(K)` → C# `IDictionary.
      ContainsKey(TKey)` (or `IReadOnlyDictionary.ContainsKey(TKey)`
      depending on the SUT property type, decided by
      machine_state.dart.md for `BytecodeProgram.Labels` and by
      runtime.dart.md for `GlpRuntime.GlpChannels`). `Map.isEmpty`
      → C# `IDictionary.Count == 0` OR `!IDictionary.Any()` —
      preferred form is `Assert.True(rt.GlpChannels.Count == 0)`
      or simply `Assert.Empty(rt.GlpChannels)` (xUnit's dedicated
      `Empty` matcher — clearer diagnostic; see
      `rf-dart-expect-isEmpty-to-xunit-assert-empty`).
    idiom_id: null
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      Map-existence-check nuance (cached idiom): Dart
      `Map.containsKey` and C# `IDictionary.ContainsKey` have
      identical observable contracts on Dart `String` ↔ C# `string`
      keys (both reference-typed-with-value-equality semantics for
      string). Diagnostic nuance: bare `Assert.True(b)` gives a
      generic failure — adequate for the simple boolean assertions
      here; codegen MAY refactor `expect(rt.glpChannels.isEmpty,
      isTrue)` to `Assert.Empty(rt.GlpChannels)` for the strictly
      tighter assertion (already an accepted optimisation per the
      binding_pointer_test convspec).
  - construct_key: dart.package_test.expect_same_reference_identity
    source_form: "expect(rt.glpChannels['target_b'], same(channel)); expect(rt.glpChannels['my_module'], same(channel));"
    target_decision: >-
      Dart `expect(<actual>, same(<expected>))` → xUnit
      `Assert.Same(<expected>, <actual>)` — REFERENCE-IDENTITY
      assertion (argument-order flipped, as documented in
      rf-dart-expect-equals-to-xunit-assertequal). xunit.net
      `Assert.Same` checks reference identity, NOT value equality
      (https://xunit.net/docs/comparisons#assertions, Microsoft
      Learn `xunit.Assert.Same`). The Dart `same` matcher is
      `identical(actual, expected)` (Dart core `identical` —
      reference identity for non-canonicalised values); the C#
      counterpart is `Assert.Same(expected, actual)`. THE
      ASSERTION IS LOAD-BEARING for the GLP-channel registry
      contract: `rt.glpChannels['target_b']` MUST be the SAME
      object reference returned by the `activateModule(...)` call.
    idiom_id: null
    research_finding_id: rf-dart-expect-same-to-xunit-assert-same
    nuance: >-
      Reference-identity nuance (explicitly addressed) AND
      THREADING-MODEL DEPENDENCY (LOAD-BEARING, see escalations[0]
      below): the `same(channel)` assertion is meaningful ONLY if
      the converted `GlpRuntime.GlpChannels` dictionary preserves
      reference identity of stored handles. Under the inherited
      threading-model invariant from heap_fcp.dart.md
      escalations[0] (single-owning-context — recommended option
      A in the parent escalation), `Dictionary<string,
      GlpChannelHandle>` trivially preserves reference identity
      (every read returns the same stored reference). Under the
      ALTERNATIVE inherited rulings (e.g. option C: replace
      mutable internals with concurrent primitives, OR a future
      decision to clone handles across context boundaries) the
      reference-identity contract could be violated. The
      `escalations[0]` entry below records the LOCAL
      undecidable-point gated on the inherited ruling, per
      FR-013. Codegen choice: emit `Assert.Same(channel, rt.
      GlpChannels["target_b"]);` (argument order flipped) and
      DEFER the ruling — the spec is correct under the
      recommended (single-owning-context) reading.
  - construct_key: dart.indexer_access.map_string_to_value_returning_nullable
    source_form: "rt.glpChannels['target_b']  // returns GlpChannelHandle?"
    target_decision: >-
      Dart `Map<K,V>[K]` returns `V?` (nullable lookup). C#
      `Dictionary<TKey, TValue>[TKey]` THROWS `KeyNotFoundException`
      on miss — NOT the same contract. The faithful counterpart is
      `Dictionary<TKey, TValue>.TryGetValue(TKey, out TValue)` (or
      `IDictionary.TryGetValue`). HOWEVER, for the specific
      assertion shape `expect(rt.glpChannels['target_b'],
      same(channel))` (immediately preceded by `expect(rt.
      glpChannels.containsKey('target_b'), isTrue)`), the value
      is GUARANTEED present and the throw cannot fire — codegen
      MAY emit the indexer access `rt.GlpChannels["target_b"]`
      directly (matches the byte-for-byte Dart shape) OR
      `rt.GlpChannels.TryGetValue("target_b", out var actual);
      Assert.Same(channel, actual);` (matches the Dart nullable-
      lookup shape). PREFERRED: the indexer form (one-line,
      matches Dart shape; the preceding `ContainsKey` guard makes
      it safe).
    idiom_id: null
    research_finding_id: rf-dart-map-indexer-nullable-to-csharp-dictionary-indexer-or-trygetvalue
    nuance: >-
      Missing-key-contract nuance (explicitly addressed and well-
      known footgun): Dart `Map[K]` returns `V?` on miss (no
      throw); C# `Dictionary<TKey,TValue>[TKey]` throws
      `KeyNotFoundException` on miss. The semantic divergence is
      observable IFF the key is absent. For this file every
      indexer access is guarded by an immediately-preceding
      `ContainsKey` assertion, so the divergence is hidden. If a
      future test exercised an absent-key path (the "no goal"
      branch), codegen MUST translate to `TryGetValue` instead.
      Both forms are recorded in the research finding so the
      precedent is available.
  - construct_key: dart.constructor_call.glp_runtime_no_args
    source_form: "final rt = GlpRuntime();"
    target_decision: >-
      `var rt = new GlpRuntime();` — the converted no-args
      constructor per `lib/runtime/runtime.dart.md`. Each test
      allocates its own `GlpRuntime` instance — per-test isolation.
    idiom_id: null
    research_finding_id: rf-dart-no-args-constructor-call-to-csharp-new
    nuance: >-
      Reference-type identity nuance: `GlpRuntime` is a reference
      `class` per runtime.dart.md (NOT a record; NOT a struct).
      Per-test isolation by allocation. Async/Future/Stream:
      ABSENT.
  - construct_key: dart.named_arg_call.activate_module_required_named_params
    source_form: >-
      "activateModule(
         rt: rt,
         serveBytecode: serveBytecode,
         moduleBytecode: bBytecode,
         moduleName: 'target_b',
       );"
    target_decision: >-
      Dart top-level function `activateModule` with named-required
      params maps to a `public static GlpChannelHandle ActivateModule
      (GlpRuntime rt, BytecodeProgram serveBytecode, BytecodeProgram
      moduleBytecode, string moduleName)` static method on a host
      class (per `lib/runtime/glp_activation.dart.md`). Dart named-
      argument call sites translate to C# named-argument call sites
      (C# supports `name: value` syntax, identical to Dart). The
      converted call shape:
      `var channel = GlpActivation.ActivateModule(
         rt: rt,
         serveBytecode: serveBytecode,
         moduleBytecode: bBytecode,
         moduleName: "target_b");` (host class name decided by
      glp_activation.dart.md). Argument naming convention preserved
      lowerCamelCase per
      `rf-dart-named-arg-to-csharp-named-arg`.
    idiom_id: null
    research_finding_id: rf-dart-named-arg-to-csharp-named-arg
    nuance: >-
      Named-required-params nuance (explicitly addressed): Dart's
      `required` keyword on named params is enforced at compile
      time; C# has no `required` on positional parameters, but
      named-argument call syntax is fully supported (C# 7.2+) —
      callers can use `name: value` to mirror Dart shape exactly.
      C# 11 `required` keyword applies to PROPERTIES, not method
      parameters — irrelevant here. Function-vs-method nuance:
      Dart `activateModule` is a TOP-LEVEL function; C# requires
      a containing type — the conventional host is a static
      class named for the file (`GlpActivation` per
      glp_activation.dart.md). The call site here uses unqualified
      `activateModule(...)` (because Dart imported it via
      `package:.../glp_activation.dart`); the C# call site uses
      `GlpActivation.ActivateModule(...)` (qualified static
      method call). NOT async — synchronous return per
      glp_activation.dart.md.
  - construct_key: dart.constructor_call.scheduler_with_optional_trace_sink
    source_form: >-
      "final scheduler = Scheduler(rt: rt);
       final scheduler = Scheduler(
         rt: rt,
         traceSink: (s) => trace.add(s),
       );"
    target_decision: >-
      `var scheduler = new Scheduler(rt: rt);` (positional ctor with
      one named argument). The two-argument form:
      `var scheduler = new Scheduler(rt: rt, traceSink: s => trace.
      Add(s));` — the converted ctor signature is owned by
      `lib/runtime/scheduler.dart.md` (per its construct documenting
      `Scheduler` instantiation, which uses named args
      `rt:`/`traceSink:`/`runner:`). The `traceSink` callback type
      is Dart `void Function(String)` → C# `Action<string>` (single-
      arg void-returning delegate; Microsoft Learn
      `https://learn.microsoft.com/dotnet/api/system.action-1`).
    idiom_id: null
    research_finding_id: rf-dart-named-arg-to-csharp-named-arg
    nuance: >-
      Function-typed-parameter nuance (explicitly addressed): Dart
      `void Function(String)` is a typed void-returning callable;
      the documented .NET counterpart is `Action<T>` (NOT `Func<T,
      void>` — `void` cannot be a generic-Func type argument).
      Lambda nuance: Dart `(s) => trace.add(s)` → C# `s => trace.
      Add(s)` (identical arrow syntax; parameter type inferred
      from `Action<string>`). Closure-capture nuance: the lambda
      captures the local `trace` variable from the enclosing test
      method — C# closure capture is by-reference for locals (same
      as Dart for non-primitive locals); the captured `trace`
      list is mutated through the captured reference. No
      ownership transfer; the lambda lifetime is bounded by the
      scheduler instance lifetime which is bounded by the test
      method scope.
  - construct_key: dart.method_call.scheduler_drainWithStatus_named_args
    source_form: >-
      "var result = scheduler.drainWithStatus(maxCycles: 100);
       result = scheduler.drainWithStatus(maxCycles: 500);
       result = scheduler.drainWithStatus(maxCycles: 1000);
       result = scheduler.drainWithStatus(maxCycles: 200);
       result = scheduler.drainWithStatus(maxCycles: 500, debug: true);"
    target_decision: >-
      `var result = scheduler.DrainWithStatus(maxCycles: 100);`
      etc. — PascalCase method name + C# named-argument syntax.
      The converted method signature is owned by `lib/runtime/
      scheduler.dart.md` (which decides:
      `DrainResult DrainWithStatus(int maxCycles = 1000, bool
      debug = false, bool showBindings = true, bool debugOutput
      = false)` — synchronous, returning a `DrainResult` value
      with `Status` (enum) / `SuspendedGoals` / `GoalsRan`
      properties). Variable `result` is Dart-typed `DrainResult`;
      reassignment is preserved in C# by using `var result` on
      first use and bare `result = ...` on subsequent uses (Dart
      `var` and C# `var` both allow reassignment).
    idiom_id: null
    research_finding_id: rf-dart-named-arg-to-csharp-named-arg
    nuance: >-
      Reassignment-of-`var` nuance: Dart `var result = ...;` then
      `result = ...;` is reassignable; the source uses `var` (NOT
      `final`) here so codegen MUST emit `var result = ...; result
      = ...;` (NOT a fresh `var result = ...;` second time, which
      would shadow). Synchronous-blocking-call nuance (LOAD-BEARING
      — see escalations[0]): `drainWithStatus` is SYNCHRONOUS per
      scheduler.dart.md, even though the underlying scheduler
      walks the goal queue and invokes runners. The test's
      `expect(result.status, ...)` assertions DIRECTLY observe the
      drain return value on the next line, which only works if
      the drain is synchronously complete (NOT awaiting). Codegen
      MUST NOT introduce `async Task<DrainResult>` here. The
      threading-model decision inherited from heap_fcp.dart.md
      escalations[0] determines whether the scheduler runs on the
      calling thread or on a pinned thread/scheduler — under
      either of the recommended options (A: single-owning-context;
      B: external locking), the drain remains synchronous and
      blocking from the caller's perspective. Optional-parameter
      nuance: `debug: true` (only used in test 2) maps to C#
      named-argument override of the default `bool debug = false`
      — identical shape.
  - construct_key: dart.enum_member_access.execution_status_succeeded
    source_form: "ExecutionStatus.succeeded"
    target_decision: >-
      `ExecutionStatus.Succeeded` (PascalCase member). The
      converted enum is owned by `lib/runtime/scheduler.dart.md`,
      which decides: `enum ExecutionStatus { Succeeded, Failed,
      Suspended }` (three members, declaration order preserved,
      PascalCase per .NET convention — though note: the precedent
      in cells.dart.md/heap_fcp.dart.md preserves Dart enum-member
      casing verbatim where the Dart source is already PascalCase
      or uses an acronym pattern; here the Dart source is
      `succeeded` lowercase so PascalCase conversion is the
      canonical choice; the scheduler.dart.md spec owns the final
      ruling).
    idiom_id: null
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Casing nuance (explicitly addressed): Dart enum members are
      conventionally `lowerCamelCase` (here `succeeded`); .NET
      enum members are conventionally `PascalCase` (here
      `Succeeded`). Codegen MUST apply the rename uniformly to
      every callsite — this file has FIVE occurrences of
      `ExecutionStatus.succeeded` (one per test) — and at the
      enum-definition site in scheduler.dart.md.
  - construct_key: dart.package_test.expect_equals_enum_member
    source_form: >-
      "expect(result.status, equals(ExecutionStatus.succeeded));"
    target_decision: >-
      `Assert.Equal(ExecutionStatus.Succeeded, result.Status);` —
      argument-order flipped (xUnit puts expected first, Dart
      `expect` puts actual first), PascalCase property/enum
      access. xUnit `Assert.Equal<T>(T expected, T actual)` uses
      `EqualityComparer<T>.Default` which for enum types is
      ordinal equality (same as Dart `==` on enum members).
    idiom_id: null
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order-flip nuance (cached idiom, well-known
      footgun): Dart puts actual first, xUnit puts expected first.
      Codegen MUST flip every `expect(actual, equals(expected))`
      call. THIS file has SEVEN `expect(result.status, equals(
      ExecutionStatus.succeeded))` calls — every one flips.
      `reason: '...'` (optional Dart matcher arg) translates to
      xUnit's third `userMessage` parameter on `Assert.Equal` —
      see next construct.
  - construct_key: dart.package_test.expect_equals_with_reason_message
    source_form: >-
      "expect(result.status, equals(ExecutionStatus.succeeded),
         reason: 'serve should process the RPC goal and suspend waiting for next input');
       expect(result.status, equals(ExecutionStatus.succeeded),
         reason: 'serve should process both RPCs and suspend');
       expect(result.status, equals(ExecutionStatus.succeeded),
         reason: 'serve should terminate when channel is closed');"
    target_decision: >-
      The Dart `reason:` named argument on `expect` carries a
      diagnostic message shown on assertion failure. The xUnit
      counterpart is `Assert.Equal(expected, actual)` followed by
      a manual diagnostic helper — xUnit's `Assert.Equal` does NOT
      take a custom-message overload. The IDIOMATIC translation
      is one of: (a) inline the Dart `reason` as a `// `-comment
      ABOVE the `Assert.Equal` call (preserves reviewability,
      compiler-friendly); (b) use xUnit's `Assert.True(actual ==
      expected, "<message>")` form (loses the
      `EqualityComparer<T>.Default` diagnostic richness but
      preserves the message at runtime); (c) wrap with
      `Xunit.Assert.True(result.Status == ExecutionStatus.
      Succeeded, $"<message>");`. PREFERRED: form (c) for
      THIS file — the messages are diagnostic narrative that
      should survive into runtime failure output. Codegen rule:
      `expect(<actual>, equals(<expected>), reason: <message>)`
      → `Assert.True(<actual> == <expected>, <message>);`. For
      enum types `==` is ordinal-equal in C#, observably
      equivalent to `Assert.Equal`.
    idiom_id: null
    research_finding_id: rf-dart-expect-equals-with-reason-to-xunit-assert-true-with-message
    nuance: >-
      Message-preservation nuance (explicitly addressed): xUnit
      lacks a `Assert.Equal(expected, actual, message)` overload
      (Microsoft Learn `Xunit.Assert.Equal` reference). The
      `Assert.True(<bool>, message)` form is the documented xUnit
      way to attach a custom failure message to a runtime
      assertion (xunit.net documentation). The narrative messages
      in THIS file are PROVENANCE — they explain WHY the assert is
      expected to succeed in test author intent (the `serve`
      semantics) — and MUST survive into the converted assertion
      so a regression diagnoses correctly. Trade-off: losing
      `Assert.Equal`'s rich type-diff output is acceptable for
      enum-type assertions where the diff is "Succeeded vs
      Suspended" — already legible from the values themselves.
  - construct_key: dart.field_access.runtime_state_mutable_int_counter
    source_form: "final callerGoalId = rt.nextGoalId++;"
    target_decision: >-
      Dart `rt.nextGoalId++` is a post-increment that returns the
      OLD value and increments the field in place. C# counterpart:
      `var callerGoalId = rt.NextGoalId++;` — identical semantics
      because C# post-increment on `int` / `long` is also
      post-increment-by-value. The SUT type for `nextGoalId` is
      owned by `lib/runtime/runtime.dart.md` (decided as a mutable
      `long NextGoalId { get; set; }` public property or a
      `public long NextGoalId` field — either supports
      post-increment).
    idiom_id: null
    research_finding_id: rf-dart-post-increment-mutable-field-to-csharp
    nuance: >-
      Mutable-state nuance (explicitly addressed) AND
      THREADING-MODEL DEPENDENCY (LOAD-BEARING — see
      escalations[0]): `rt.nextGoalId++` is a non-atomic
      read-modify-write on a shared int. Under the inherited
      single-owning-context option from heap_fcp.dart.md
      escalations[0] (option A — recommended), the post-increment
      is safe because the runtime is touched only by the agent's
      owning thread. Under option C (concurrent primitives), the
      faithful counterpart would be `Interlocked.Increment(ref
      rt._nextGoalId)` — but `Interlocked.Increment` returns the
      INCREMENTED value, not the pre-increment value, so the
      conversion would require `Interlocked.Increment(ref ...)
      - 1` OR a `lock` block — a non-trivial divergence from
      the Dart shape. Codegen MUST defer this decision to the
      inherited ruling. For THIS file the recommended-option (A)
      translation is byte-faithful (`rt.NextGoalId++`).
      Width nuance: `nextGoalId` is logically `long` (matches
      address-width and goal-id widths used elsewhere; see
      heap_fcp.dart.md's `dart.int.fixed_width_identity_field`).
  - construct_key: dart.method_call.heap_store_term_on_heap
    source_form: >-
      "final argAddr = rt.heap.storeTermOnHeap(ConstTerm(42));
       final arg0Addr = rt.heap.storeTermOnHeap(ConstTerm('alice'));
       final arg1Addr = rt.heap.storeTermOnHeap(ConstTerm('bob'));
       final argAddr = rt.heap.storeTermOnHeap(ConstTerm(99));"
    target_decision: >-
      `var argAddr = rt.Heap.StoreTermOnHeap(new ConstTerm(42));`
      etc. PascalCase property `Heap` (on `GlpRuntime`) per
      runtime.dart.md; PascalCase method `StoreTermOnHeap(Term
      term)` returning `long` per heap_fcp.dart.md (precedent
      construct `dart.heap_fcp.store_term_on_heap`). `ConstTerm`
      construction reuses the precedent decision from terms.dart.md
      (rf-dart-sumleaf-no-eq-to-csharp-class-no-record) — `new
      ConstTerm(<lit>)` boxing the literal into `object? Value`.
    idiom_id: null
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Literal-boxing nuance (cached idiom): `ConstTerm(42)` boxes
      `int`; `ConstTerm('alice')` boxes `string`; `ConstTerm(99)`
      boxes `int`. All map to `new ConstTerm(<lit>)` with the
      payload widening to `object?`. Single-quote vs double-quote
      nuance: Dart `'alice'` (single-quote string literal) → C#
      `"alice"` (double-quote string literal) — Dart strings are
      delimiter-agnostic, C# strings require double-quotes.
  - construct_key: dart.constructor_call.call_env_with_named_map_literal
    source_form: >-
      "final env = CallEnv(args: {0: VarRef(argAddr)});
       final env = CallEnv(args: {0: VarRef(arg0Addr), 1: VarRef(arg1Addr)});"
    target_decision: >-
      `var env = new CallEnv(args: new Dictionary<int, VarRef> {
        { 0, new VarRef(argAddr) }
      });` — Dart `{key: value}` map literal converts to C#
      collection-initialiser on `Dictionary<TKey, TValue>`. The
      SUT type (`CallEnv`) and its `args` field type are owned by
      `lib/runtime/machine_state.dart.md`. Width nuance: keys are
      Dart `int` → C# `int` (NOT `long` — these are argument-slot
      indices, not addresses; see machine_state.dart.md for the
      authoritative decision on `CallEnv.Args` key width). Values
      are `VarRef` (a Term subtype; address-typed `long` payload
      per terms.dart.md).
    idiom_id: null
    research_finding_id: rf-dart-map-literal-int-to-vref-to-csharp-dictionary-init
    nuance: >-
      Map-literal nuance (explicitly addressed): Dart `{0:
      VarRef(...)}` is unambiguously a `Map<int, VarRef>` (key:
      value pairs disambiguate from set literal). C# collection-
      initialiser syntax `new Dictionary<int, VarRef> { { 0, new
      VarRef(...) } }` is the documented counterpart (Microsoft
      Learn `https://learn.microsoft.com/dotnet/csharp/programming-
      guide/classes-and-structs/object-and-collection-
      initializers`). Modernised form (C# 9+): `new Dictionary<
      int, VarRef> { [0] = new VarRef(...) }` using indexer-
      initialiser syntax — closer to Dart shape; codegen MAY use
      either form. Width nuance: `CallEnv.Args` key width is owned
      by machine_state.dart.md; THIS spec defers to that decision.
  - construct_key: dart.method_call.runtime_setGoalEnv_setGoalProgram_setGoalModuleContext
    source_form: >-
      "rt.setGoalEnv(callerGoalId, env);
       rt.setGoalProgram(callerGoalId, aBytecode);
       rt.setGoalModuleContext(callerGoalId, replCtx);"
    target_decision: >-
      PascalCase method calls on `GlpRuntime` per runtime.dart.md:
      `rt.SetGoalEnv(callerGoalId, env);`,
      `rt.SetGoalProgram(callerGoalId, aBytecode);`,
      `rt.SetGoalModuleContext(callerGoalId, replCtx);`. Each is
      a synchronous instance method on the converted `GlpRuntime`
      class. The signature decisions (parameter types, return
      types) are owned by runtime.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Method-naming nuance (cached idiom): Dart `lowerCamelCase`
      → C# `PascalCase`; `setGoalEnv` → `SetGoalEnv`,
      `setGoalProgram` → `SetGoalProgram`,
      `setGoalModuleContext` → `SetGoalModuleContext`. Synchronous
      nuance: NONE of these is async — runtime state mutation is
      synchronous in both Dart and C# under the inherited
      threading-model. Mutable-runtime-state nuance: see the
      threading-model dependency in `dart.field_access.runtime_
      state_mutable_int_counter` above.
  - construct_key: dart.constructor_call.repl_module_context_with_named_map_literal_imports
    source_form: >-
      "final replCtx = ReplModuleContext(
         moduleName: 'caller_a',
         imports: {1: ReplModuleTarget('target_b', bBytecode)},
       );"
    target_decision: >-
      `var replCtx = new ReplModuleContext(
         moduleName: "caller_a",
         imports: new Dictionary<int, ReplModuleTarget> {
           { 1, new ReplModuleTarget("target_b", bBytecode) }
         });` — Dart named-argument ctor call → C# named-argument
      ctor call. Map literal → `Dictionary<int, ReplModuleTarget>`
      collection-initialiser. The SUT types (`ReplModuleContext`
      and `ReplModuleTarget`) and the `imports` field type are
      owned by `lib/bytecode/runner.dart.md`.
    idiom_id: null
    research_finding_id: rf-dart-map-literal-int-to-vref-to-csharp-dictionary-init
    nuance: >-
      Composite-construction nuance (explicitly addressed): four
      nested object constructions — `ReplModuleContext` (outer),
      `Dictionary<int, ReplModuleTarget>` (inner), `ReplModuleTarget`
      (entries) — each PascalCased. The `1: ReplModuleTarget('target_b',
      bBytecode)` entry uses a POSITIONAL constructor on
      `ReplModuleTarget` (not named-arg) — runner.dart.md owns
      whether `ReplModuleTarget(string, BytecodeProgram)` is
      positional or has named params. From the call shape it
      appears positional; the converted call shape MUST match.
      Three occurrences of this construct in this file (tests 1,
      2, 3, 5) — all identical shape with one entry mapping
      importIndex 1 → ReplModuleTarget('target_b', bBytecode).
  - construct_key: dart.if_not_contains_then_assign.runtime_runners_lazy_registration
    source_form: >-
      "if (!rt.runners.containsKey(aBytecode)) {
         rt.runners[aBytecode] = BytecodeRunner(aBytecode);
       }"
    target_decision: >-
      `if (!rt.Runners.ContainsKey(aBytecode)) {
         rt.Runners[aBytecode] = new BytecodeRunner(aBytecode);
       }` — Dart `Map.containsKey` → C# `Dictionary.ContainsKey`;
      Dart `map[k] = v` → C# `dict[k] = v` (indexer-assign,
      insert-or-overwrite); `BytecodeRunner` ctor PascalCased.
      Modernised form (C# 7+): `rt.Runners.TryAdd(aBytecode, new
      BytecodeRunner(aBytecode));` — single-method idiomatic
      counterpart (Microsoft Learn `Dictionary<TKey,TValue>.TryAdd`).
      PREFERRED: `TryAdd` form for cleaner C# idiom; the verbatim
      `ContainsKey + indexer-assign` form is acceptable byte-
      faithful translation.
    idiom_id: null
    research_finding_id: rf-dart-map-tryadd-pattern-to-csharp-dictionary-tryadd
    nuance: >-
      Lazy-initialisation nuance (explicitly addressed): the
      check-then-set is a well-known race-condition footgun under
      multi-threaded access. Under the inherited single-owning-
      context invariant from heap_fcp.dart.md escalations[0]
      (option A — recommended), the sequence is safe because
      `rt.Runners` is touched only by the owning thread.
      `Dictionary.TryAdd` is also non-atomic (NOT a
      `ConcurrentDictionary`) — but the ownership invariant
      makes that adequate. Codegen MUST NOT silently substitute
      `ConcurrentDictionary` here. Map-key-type nuance: the key
      is `BytecodeProgram` (a reference type) — Dart `Map<K,V>`
      uses `==`/`hashCode` for keying; C# `Dictionary<TKey,TValue>`
      uses `IEqualityComparer<TKey>.Default`, which for reference
      types defaults to reference identity. Per
      `lib/runtime/runtime.dart.md` and bytecode/runner.dart.md,
      `BytecodeProgram` does NOT override `==` — reference
      identity is the intended keying contract on BOTH sides
      (same instance compiled once and reused across the test
      lifetime).
  - construct_key: dart.method_call.goal_queue_enqueue_with_goal_ref
    source_form: >-
      "rt.gq.enqueue(GoalRef(callerGoalId, callerPc));
       rt.gq.enqueue(GoalRef(callerGoalId, aBytecode.labels['caller/1']!));
       rt.gq.enqueue(GoalRef(callerGoalId, aBytecode.labels['run_both/2']!));"
    target_decision: >-
      `rt.Gq.Enqueue(new GoalRef(callerGoalId, callerPc));` etc.
      PascalCase property `Gq` on `GlpRuntime` per runtime.dart.md
      (or possibly `GoalQueue` if runtime.dart.md decides to
      expand the abbreviation — defer to that spec). `GoalQueue.
      Enqueue(GoalRef)` per goal_queue.dart.md / machine_state.dart.md.
      `GoalRef` constructor PascalCased; positional args preserved.
    idiom_id: null
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Property-name-abbreviation nuance (defer to runtime.dart.md):
      Dart `rt.gq` is a short property name; whether C# preserves
      the abbreviation as `rt.Gq` or expands it to `rt.GoalQueue`
      is owned by runtime.dart.md. Both renames are observably
      equivalent; THIS spec uses `rt.Gq` as a working assumption
      and the codegen MUST consult runtime.dart.md.
  - construct_key: dart.indexer_access_with_bang.bytecode_labels_lookup
    source_form: >-
      "aBytecode.labels['caller/1']!
       aBytecode.labels['run_both/2']!"
    target_decision: >-
      Dart `aBytecode.labels['caller/1']!` is a Map lookup followed
      by the null-assertion operator `!` (asserts non-null at
      runtime; throws `TypeError` if null). C# `Dictionary<TKey,
      TValue>[TKey]` throws `KeyNotFoundException` on miss —
      already non-nullable semantics, NO `!` needed. Codegen
      emits `aBytecode.Labels["caller/1"]` (indexer access)
      directly. Note: a sibling construct above
      (`dart.indexer_access.map_string_to_value_returning_nullable`)
      uses the `containsKey`-guard then indexer form; here the
      Dart source uses the `!` form instead — codegen can drop
      the `!` because C# `Dictionary[]` throws on miss (the Dart
      `!` and the C# `[]` throw have the same observable contract
      under absent-key). The first usage form (line 88: `final
      callerPc = aBytecode.labels['caller/1']!;`) extracts the
      value to a local first.
    idiom_id: null
    research_finding_id: rf-dart-null-assertion-on-map-indexer-to-csharp-dictionary-indexer
    nuance: >-
      Null-assertion-operator nuance (explicitly addressed): Dart
      `!` on a nullable expression is the "null-assertion
      operator" (Dart language reference) — runtime throw on null.
      C# has a NULL-FORGIVING operator with the same `!` syntax
      but DIFFERENT semantics: C# `!` suppresses the compiler's
      nullable warning but does NOT throw at runtime. Codegen
      MUST NOT translate Dart `!` to C# `!` directly — the
      semantic mismatch would silently mask null bugs. For Dart
      `Map[K]!` specifically, the C# `Dictionary[]` indexer
      already throws on miss, so the `!` is REDUNDANT and codegen
      drops it. If a future call site applied `!` to a nullable
      LOCAL or PROPERTY (NOT a Map indexer), codegen would need
      to emit `<expr> ?? throw new InvalidOperationException(...)`
      to preserve runtime-throw semantics — recorded in the
      research finding.
  - construct_key: dart.constructor_call.goal_ref_two_args
    source_form: "GoalRef(callerGoalId, callerPc); GoalRef(callerGoalId, aBytecode.labels['caller/1']!)"
    target_decision: >-
      `new GoalRef(callerGoalId, callerPc)` — positional ctor
      preserved. The SUT type per `lib/runtime/machine_state.dart.md`
      / `lib/runtime/goal_queue.dart.md` (precedent: every
      test/bytecode/*.dart.md). `GoalRef` is a reference-type
      `class` (NOT a record/struct) per the precedent decisions.
    idiom_id: null
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      Reference-vs-value nuance (cached idiom): `GoalRef` carries
      a goal-id + PC pair; in Dart it's a `class` with no `==`
      override (identity equality). The C# port preserves
      identity equality via `class` (NOT `record` which would
      inject value-equality).
  - construct_key: dart.local_var.empty_list_typed_string
    source_form: "final trace = <String>[];"
    target_decision: >-
      `var trace = new List<string>();` — Dart `<String>[]` is a
      typed empty list literal; C# `new List<string>()` is the
      counterpart. `List<string>` is mutable; the test mutates it
      via the lambda `(s) => trace.add(s)` mapping to `s =>
      trace.Add(s)`.
    idiom_id: null
    research_finding_id: rf-dart-typed-list-literal-empty-to-csharp-list-of-T-new
    nuance: >-
      Typed-empty-literal nuance (explicitly addressed): Dart
      `<String>[]` explicitly types the empty list as
      `List<String>`. C# `new List<string>()` is equivalent.
      Alternative C# `new List<string> { }` (collection-initialiser
      with no elements) is also legal. Codegen uses the `new
      List<string>()` form for readability.
  - construct_key: dart.method_call.list_string_join
    source_form: "final traceStr = trace.join('\\n');"
    target_decision: >-
      `var traceStr = string.Join("\n", trace);` — argument
      order FLIPPED. Dart `Iterable.join(separator)` is an
      instance method on the iterable; C# `string.Join(separator,
      values)` is a static method on `string` with the separator
      first and the values second (Microsoft Learn `https://
      learn.microsoft.com/dotnet/api/system.string.join`).
    idiom_id: null
    research_finding_id: rf-dart-iterable-join-to-csharp-string-join
    nuance: >-
      Argument-order-flip nuance (explicitly addressed and
      well-known footgun, distinct from the `Assert.Equal` flip):
      Dart `<iterable>.join(separator)` → C# `string.Join(
      separator, <iterable>)`. Codegen MUST emit the FLIPPED
      order. Separator-literal nuance: Dart `'\n'` (single-
      quoted, with an embedded escape sequence) → C# `"\n"`
      (double-quoted, with an embedded escape sequence). NOT a
      verbatim string here (`@"\n"` would emit a backslash-n,
      not a newline) — the regular interpreted-escape form is
      required.
  - construct_key: dart.package_test.expect_string_contains_substring
    source_form: "expect(traceStr, contains('serve'), reason: 'Trace should show serve reduction');"
    target_decision: >-
      Dart `expect(<string>, contains(<substring>))` → xUnit
      `Assert.Contains(<substring>, <string>);` — argument-order
      flipped (xUnit puts the needle first). xUnit
      `Assert.Contains(string expectedSubstring, string actualString)`
      is documented at xunit.net. The `reason:` argument
      translates as per the message-preservation construct above
      — preferred form is `Assert.True(traceStr.Contains("serve"),
      "Trace should show serve reduction");` (preserves the
      message at runtime).
    idiom_id: null
    research_finding_id: rf-dart-expect-string-contains-to-xunit-assert-contains
    nuance: >-
      Argument-order-flip nuance: Dart puts the haystack first
      (`contains` matcher inverts the expected/actual mental model
      — `expect(actual, contains(expected_substring))`); xUnit
      `Assert.Contains` puts the NEEDLE first. With the `reason:`
      message attached, codegen prefers `Assert.True(haystack.
      Contains(needle), message)` to preserve the message —
      same trade-off as `expect_equals_with_reason_message` above.
  - construct_key: dart.method_call.glp_channel_handle_close_returns_list_of_goal_ref
    source_form: >-
      "final activations = channel.close();
       for (final act in activations) {
         rt.gq.enqueue(act);
       }"
    target_decision: >-
      `var activations = channel.Close();
       foreach (var act in activations) {
         rt.Gq.Enqueue(act);
       }` — PascalCase method `Close()` returning `List<GoalRef>`
      (the SUT method is decided in glp_activation.dart.md;
      precedent construct documents the bindVariable return type
      as `List<GoalRef>` activations). Dart `for (final x in
      iterable) { ... }` → C# `foreach (var x in iterable) {
      ... }` — identical iteration semantics (collection is
      enumerated synchronously, items are bound to local `act`).
    idiom_id: null
    research_finding_id: rf-dart-for-in-loop-to-csharp-foreach
    nuance: >-
      Iteration-semantics nuance (cached idiom): Dart `for (final
      x in iterable)` and C# `foreach (var x in iterable)` are
      observably equivalent for synchronous iteration. The loop
      body's mutation of `rt.Gq` is under the same isolate-
      ownership invariant inherited from heap_fcp.dart.md
      escalations[0]. Return-type nuance: `channel.close()`
      returns `List<GoalRef>` (NOT `IReadOnlyList<>`) per
      glp_activation.dart.md — codegen preserves mutability of
      the returned list (the consumer enqueues each element;
      enqueue does not mutate the returned list).
conversion_units:
  - "cu-1: file-scope using directives (Xunit + Linq if needed + the SUT runtime/compiler/bytecode namespaces; runtime imports collapse to one using)"
  - "cu-2: namespace declaration mirroring test/runtime path (e.g. <RootNs>.Test.Runtime)"
  - "cu-3: top-level test class RpcRoutingTest (single class, no inner classes — single flat group becomes [Trait] partition on all five methods)"
  - "cu-4: private const string field serveSource on the test class (verbatim @\"...\" preserving GLP source newlines byte-for-byte)"
  - "cu-5: NO constructor (no shared state across tests — every test allocates its own GlpCompiler + GlpRuntime)"
  - "cu-6: 5 [Fact] methods each with [Fact(DisplayName = \"<original label>\")] + [Trait(\"Group\", \"Phase 5 - RPC routing via GLP channels\")]"
  - "cu-7: per-method preserves the inline var compiler = new GlpCompiler(); ... var rt = new GlpRuntime(); ... var scheduler = new Scheduler(rt - rt); setup as method-local"
  - "cu-8: drainWithStatus call sites preserve var result = scheduler.DrainWithStatus(maxCycles - N); reassignment (NOT a fresh var declaration)"
  - "cu-9: Assert.Equal flipping (expected first) on every expect(actual, equals(expected)) call site"
  - "cu-10: Assert.True(actual == expected, message) form on every expect(actual, equals(expected), reason - <message>) call site preserving the diagnostic narrative"
  - "cu-11: Assert.Same(channel, rt.GlpChannels[...]) for reference-identity assertions on the glpChannels registry (LOAD-BEARING — depends on inherited threading-model ruling, see escalations[0])"
  - "cu-12: Assert.True / Assert.Contains forms preserving reason - messages where present (test 2 contains('serve') + reason)"
  - "cu-13: nested-construction new ReplModuleContext(moduleName - ..., imports - new Dictionary<int, ReplModuleTarget> { { 1, new ReplModuleTarget(...) } }) at every caller-goal setup (tests 1, 2, 3, 5)"
  - "cu-14: lazy-registration if (!rt.Runners.ContainsKey(...)) rt.Runners[...] = new BytecodeRunner(...); OR rt.Runners.TryAdd(...); — preferred form is TryAdd"
  - "cu-15: for (final act in activations) { rt.gq.enqueue(act); } → foreach (var act in activations) { rt.Gq.Enqueue(act); } in test 5"
escalations:
  - kind: undecidable
    construct_key: dart.test.rpc_routing.glp_channels_reference_identity_under_inherited_threading_model
    detail: >-
      The `expect(rt.glpChannels['target_b'], same(channel))`
      assertion (tests 1 + 4) requires that the converted
      `GlpRuntime.GlpChannels` dictionary preserves REFERENCE
      IDENTITY of stored `GlpChannelHandle` instances — the same
      object reference returned by `activateModule(...)` must be
      observable on subsequent `rt.GlpChannels[name]` reads. This
      identity invariant is TRIVIALLY satisfied under the
      recommended single-owning-context option (option A) in the
      heap_fcp.dart.md escalations[0] threading-model decision,
      and remains satisfied under option B (external locking
      around mutations). HOWEVER, under option C (replace mutable
      internals with concurrent primitives — `ConcurrentDictionary`,
      etc.), the identity invariant remains technically preserved
      by .NET's `ConcurrentDictionary<TKey, TValue>` indexer
      (returns the stored reference), so the test would still
      pass. The ACTUAL undecidable here is sub-secondary: if a
      future cross-context boundary requires marshalling/cloning
      handles (e.g. an actor-mailbox model that copies messages
      across mailbox boundaries), the reference-identity contract
      could be violated. THIS spec defers the ruling to the
      heap_fcp.dart.md escalations[0] resolution AND notes that
      `Assert.Same(channel, rt.GlpChannels["target_b"])` is
      correct under EVERY currently-documented option, so the
      conversion is safe to proceed under the recommended (option
      A) reading.
    needs: >-
      heap_fcp.dart threading-model ruling (escalations[0] in
      `.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md`).
      Specifically: confirm that the chosen .NET hosting model
      preserves reference identity of `GlpChannelHandle` objects
      stored in `GlpRuntime.GlpChannels` across all read paths
      observable from a single test method. The recommended
      option (A: single-owning-context via the isolate-manager
      port) satisfies this trivially; option B (external locking)
      satisfies it; option C (concurrent primitives) satisfies
      it iff `GlpChannels` is `Dictionary<...>` or
      `ConcurrentDictionary<...>` (both preserve reference
      identity on indexer read). Any future actor-mailbox model
      that copies/marshals handles across mailbox boundaries
      would VIOLATE the contract — in that case the
      `Assert.Same` assertion in tests 1 and 4 would need
      replacement (e.g. with a value-equality assertion on a
      stable identifier field of `GlpChannelHandle` — a refactor
      that crosses into glp_activation.dart.md's spec). Per
      FR-013 + the scheduler.dart.md precedent, this file
      INHERITS the parent ruling and does NOT independently
      pick a model.
```

## Rationale and research provenance

### Threading-model inheritance (FR-013 / FR-024) — load-bearing for the entire file

This test exercises the `Scheduler` / `GlpRuntime` / `HeapFCP` /
`GlpChannelHandle` runtime surface directly, with mutable shared
state (`rt.nextGoalId++`, `rt.glpChannels[name] = handle`,
`rt.runners[bytecode] = runner`, `rt.gq.enqueue(...)`) and a
synchronous `scheduler.drainWithStatus(...)` call that walks the
entire goal queue to completion. Per the spec quality bar (US2 AS4),
the threading-model nuance MUST be explicitly addressed.

The .NET threading-model decision was ESCALATED in
`lib/runtime/heap_fcp.dart.md` escalations[0] and INHERITED through
every dependent runtime/bytecode spec, including
`lib/runtime/scheduler.dart.md` (which itself records the inheritance
verbatim, not re-escalating). Per FR-013 + the scheduler.dart.md
precedent, this file follows the same convention: inherit the
parent ruling, do NOT re-escalate the broad question, surface only
the LOCAL undecidable point (reference-identity of
`GlpChannelHandle` instances under the inherited model) as a
deferred sub-escalation.

Under the recommended option A (single-owning-context — per-agent
isolate-ownership preserved via the future `isolate_manager.dart`
port), the entire converted file is byte-faithful to the Dart
shape. The convspec is correct under that reading and remains
correct under options B (external locking) and C (concurrent
primitives — `Interlocked.Increment` would change the post-
increment semantics of `rt.nextGoalId++` but that divergence is
recorded in the per-construct nuance). Authoritative basis:
heap_fcp.dart.md escalations[0] cites Dart concurrency docs
(`https://dart.dev/language/concurrency`) and .NET options
(`https://learn.microsoft.com/dotnet/core/extensions/channels`,
`https://learn.microsoft.com/dotnet/api/system.threading.tasks.
concurrentexclusiveschedulerpair`).

### Why xUnit (FR-024 cache hit)

Project-pinned. Reused verbatim from every prior `package:test`
convspec. Authoritative basis: xunit.net documentation
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / `[Trait]` / `DisplayName`; Dart `package:test` docs
(`https://pub.dev/packages/test`) for `group` / `test` / `expect`
matcher semantics.

### Triple-quoted GLP source → `@"..."` verbatim string (rf-dart-top-level-const-string-multiline-to-csharp-const-verbatim)

Dart `'''...'''` triple-quoted strings preserve newlines and inner
single quotes verbatim. C# `@"..."` verbatim strings preserve
newlines and require `""` to escape inner double quotes. The GLP
source in `serveSource` uses inner SINGLE quotes (`'_activate'`)
which require NO escape in C# verbatim form. The string is the
SOURCE TEXT of a GLP program compiled at test runtime — newline
preservation is LOAD-BEARING (any whitespace change would alter
the compiled bytecode and invalidate the assertions).
Authoritative: Microsoft Learn `string-literals` documentation
(`https://learn.microsoft.com/dotnet/csharp/language-reference/
tokens/verbatim`).

### `expect(actual, same(channel))` → `Assert.Same(channel, actual)` (rf-dart-expect-same-to-xunit-assert-same)

Dart `same` matcher = `identical(a, b)` — reference identity.
xUnit `Assert.Same(expected, actual)` — reference identity.
Argument order FLIPPED (expected first in xUnit). Reused from
varref_pointer_test.dart.md / boot_loader_test.dart.md.
Authoritative: xunit.net `Assert.Same` documentation; Dart
language reference for `identical`.

### `expect(actual, equals(expected), reason: msg)` → `Assert.True(actual == expected, msg)` (rf-dart-expect-equals-with-reason-to-xunit-assert-true-with-message)

xUnit `Assert.Equal` has NO `userMessage` overload (Microsoft
Learn `Xunit.Assert.Equal` reference). The xUnit form
`Assert.True(<bool>, message)` is the documented way to attach a
custom failure message. For enum-typed assertions
(`ExecutionStatus.Succeeded`) the `==` comparison is ordinal-equal
in C#, observably equivalent to `Assert.Equal`. The narrative
`reason:` messages in this file are PROVENANCE (they explain
WHY the assertion is expected to hold per the `serve/2`
semantics) and MUST survive into the converted assertion's
runtime failure output. Trade-off: losing `Assert.Equal`'s
type-diff is acceptable for enum types where the diff is already
legible from the values. Authoritative: xunit.net documentation.

### `traceStr.contains('serve')` → `Assert.Contains("serve", traceStr)` (rf-dart-expect-string-contains-to-xunit-assert-contains)

Dart `contains` matcher → xUnit `Assert.Contains` — argument
order flipped (xUnit puts the needle first). With `reason:`
message attached, the preferred form is `Assert.True(traceStr.
Contains("serve"), message)` for the same message-preservation
trade-off as `Assert.Equal`. Authoritative: xunit.net.

### Map indexer + null-assertion `Map[K]!` → C# `Dictionary[K]` (rf-dart-null-assertion-on-map-indexer-to-csharp-dictionary-indexer)

Dart `Map<K,V>[K]` returns `V?`; the `!` operator asserts
non-null at runtime (throw on null). C# `Dictionary<TKey,TValue>
[TKey]` throws `KeyNotFoundException` on absent key — the throw
contract is preserved IDENTICAL to Dart's `!`-on-Map-indexer
without ANY syntax counterpart. CRITICALLY: codegen MUST NOT
translate Dart `!` to C# `!` (the C# null-forgiving operator has
DIFFERENT semantics — suppresses compiler warning but does NOT
throw at runtime). For Dart `Map[K]!` the C# `Dictionary[K]`
form alone is correct; the `!` is redundant in C#.
Authoritative: Microsoft Learn `Dictionary<TKey, TValue>.this[
TKey]` documentation; Dart language reference for the null-
assertion operator.

### `string.Join` argument flip (rf-dart-iterable-join-to-csharp-string-join)

Dart `<iterable>.join(separator)` is an instance method; C# uses
the static `string.Join(separator, values)` with arguments
FLIPPED. Authoritative: Microsoft Learn `string.Join`
documentation (`https://learn.microsoft.com/dotnet/api/system.
string.join`). Newline-escape nuance: Dart `'\n'` (interpreted
escape) → C# `"\n"` (interpreted escape) — NOT `@"\n"` (verbatim
would emit two characters: backslash + n).

### `Dictionary.TryAdd` vs `ContainsKey + indexer-set` (rf-dart-map-tryadd-pattern-to-csharp-dictionary-tryadd)

Dart `if (!map.containsKey(k)) map[k] = v;` is a Dart-idiomatic
lazy-add. C# `Dictionary<TKey, TValue>.TryAdd(TKey, TValue)`
(Microsoft Learn) is the single-method counterpart — adds iff
absent, returns bool. Preferred form. Verbatim `ContainsKey +
indexer-set` translation is also correct (byte-faithful).
Authoritative: Microsoft Learn `Dictionary<TKey,TValue>.TryAdd`.

### Why exactly one escalation

Every construct in this file resolves to a single decision under
the recommended (inherited) threading-model reading — option A
in heap_fcp.dart.md escalations[0]. The ONE local undecidable
point is the reference-identity of `GlpChannelHandle` instances
in `GlpRuntime.GlpChannels` under all the alternative inherited
rulings (B and C currently preserve identity; a future actor-
mailbox-with-marshalling decision could violate it). That
sub-escalation is recorded explicitly as
`escalations[0]` per FR-013 (do not guess), gated on the parent
ruling. No new threading-model escalation is opened (FR-013 +
scheduler.dart.md precedent: inherit, do not re-escalate).

### Async/Future/Stream/Isolate nuances

ABSENT from this file's surface. Every test method body is
SYNCHRONOUS — no `async` / `Future` / `await` / `Stream` /
`Isolate.spawn` / `Completer`. The `Scheduler.drainWithStatus`
call is synchronous-blocking (per scheduler.dart.md). The
trace-sink lambda `(s) => trace.add(s)` is synchronous. Codegen
MUST NOT introduce `async Task` / `Task.Run` / `Channel<T>` /
`TaskCompletionSource` here — the threading model decision
(escalated upstream) determines the EXECUTION CONTEXT of the
synchronous calls, not their async-ness at the API surface.
