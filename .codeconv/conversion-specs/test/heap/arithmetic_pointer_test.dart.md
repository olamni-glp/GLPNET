> Conversion-spec artifact for test/heap/arithmetic_pointer_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart -> C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/heap/arithmetic_pointer_test.dart
source_sha256: 3c0766cd3af29d5ad4f05a498adc3d91c60ee5dddd6562415d53c34402d01404
target_code_unit: test/heap/ArithmeticPointerTest.cs
constructs:
  - construct_key: dart.library_directive.top_of_file_no_name
    source_form: "library;"
    target_decision: >-
      Drop the bare `library;` directive entirely; C# files have no
      per-file library-declaration syntax. The doc-comment block above
      `library;` (the four-line lead -- "Tests for arithmetic body
      kernels with Pointer Architecture Heap" + the `Adapted from:
      test/bytecode/arithmetic_test.dart` provenance + the `For spec:
      docs/heap-pointer-architecture-spec.md v3.0` citation + the
      one-line rationale) is preserved as the XML doc-comment on the
      OUTER test-container class `ArithmeticPointerTest` (carry-forward
      from binding_pointer_test.dart.md and the bytecode-sibling
      arithmetic_test.dart.md -- same heap-pointer-architecture family;
      same convention applied). Project the file into the file-scoped
      `namespace <RootNs>.Test.Heap;` mirroring the Dart `test/heap`
      directory shape (precedent: binding_pointer_test.dart.md /
      varref_pointer_test.dart.md / suspension_pointer_test.dart.md /
      circular_term_pointer_test.dart.md).
    idiom_id: rf-dart-library-directive-to-csharp-namespace-elision
    research_finding_id: rf-dart-library-directive-to-csharp-namespace-elision
    nuance: >-
      Compilation-unit nuance (carry-forward KB cache hit per FR-012 /
      SC-007 -- REUSE, NO re-research). Dart 2.12+ requires `library;`
      (un-named) only as a marker for file-level doc-comments; no name,
      no `part`, no `part of`. C# elides the construct entirely and
      uses the file-scoped `namespace ...;` shape instead. No value-vs-
      reference, async, isolate, or null-safety surface implicated by
      the directive. The provenance comment IS load-bearing (cites the
      heap-pointer-architecture spec v3.0 -- a precondition for
      understanding `bindWriter`/`bindWriterToReader` vs the
      legacy-architecture `bindVariableConst` -- the bytecode-sibling
      arithmetic_test.dart.md used `bindVariableConst`; this file uses
      `bindWriter` because of the pointer-architecture cutover) and
      MUST survive the conversion as the test-class XML doc-comment.
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. xUnit pinned project-wide
      (precedent: binding_pointer_test.dart.md, varref_pointer_test.
      dart.md, suspension_pointer_test.dart.md, circular_term_pointer_
      test.dart.md, the bytecode-sibling arithmetic_test.dart.md).
      Codegen MUST also add `using System;` (for `Exception`-derived
      types referenced by `throws*` matchers, none in this file, plus
      `Math.*` -- actually unused here; Xunit covers `Assert.*`);
      `using System.IO;` for `File.ReadAllText` + `Path.Combine` (the
      End-to-end group's stdlib compile); `using
      System.Collections.Generic;` for the `Dictionary<int, VarRef>`
      literal used by the End-to-end group's `CallEnv(args: {...})`
      construct; and `using Xunit.Abstractions;` for the `ITestOutput
      Helper` injection that captures the test's `print(...)` lines.
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is a project-wide policy nuance (carry-
      forward KB cache hit per FR-012 -- REUSE). Every `package:test`
      file in the inventory MUST map to the SAME .NET framework so
      test discovery, runner config, and attribute vocabulary stay
      consistent. No re-research; no re-derivation. The
      bytecode-sibling arithmetic_test.dart.md picked the same set of
      auxiliary using-directives (System.IO, Xunit.Abstractions) for
      the same setUpAll+print+File-read+CallEnv combination; reuse
      verbatim here.
  - construct_key: dart.package_under_test.import_directive_compiler_bytecode_runtime
    source_form: >-
      "import 'package:glp_runtime/compiler/compiler.dart';
       import 'package:glp_runtime/bytecode/runner.dart';
       import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';
       import 'package:glp_runtime/runtime/terms.dart';
       import 'package:glp_runtime/runtime/body_kernels.dart';
       import 'package:glp_runtime/runtime/scheduler.dart';
       import 'dart:io';"
    target_decision: >-
      Seven `package:glp_runtime/...` imports plus one `dart:io`
      directive collapse to FOUR C# `using` directives:
      `using <RootNs>.Compiler;` (from compiler/compiler.dart -- brings
      in `GlpCompiler`),
      `using <RootNs>.Bytecode;` (from bytecode/runner.dart -- brings
      in `BytecodeProgram`, `BytecodeRunner`),
      `using <RootNs>.Runtime;` (from runtime/runtime.dart,
      machine_state.dart, terms.dart, body_kernels.dart,
      scheduler.dart -- brings in `GlpRuntime`, `BodyKernelResult`,
      `VarRef`, `ConstTerm`, `CallEnv`, `GoalRef`, `Scheduler`,
      `HeapFCP`-resident `allocateVariable`/`bindWriter`/
      `bindWriterToReader`/`derefAddr`/`isFullyBound`/`getValue`),
      `using System.IO;` (from dart:io -- brings in `File.ReadAllText`
      and `Path.Combine` for the prelude-file read). Five Dart runtime
      imports collapse to ONE C# `using <RootNs>.Runtime;` because all
      five SUT files land in the SAME namespace per their convspec
      decisions (heap_fcp.dart.md, terms.dart.md, machine_state.dart.
      md, body_kernels.dart.md, scheduler.dart.md, runtime.dart.md).
      Five Dart imports collapse to one `using` -- C# `using` is per-
      namespace, not per-file. Test assembly references the SUT
      assembly via .csproj (project-system idiom; out of scope).
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (carry-forward KB cache hit per
      FR-012 -- REUSE, verbatim shape of the bytecode-sibling
      arithmetic_test.dart.md's seven-imports-collapse-to-three +
      System.IO decision, plus the runtime sub-collapse shared with
      the heap-test-family siblings). In Dart each `package:` URI is
      a separate import; in C# all imports that target the same
      target sub-namespace collapse. No `as` alias, no `show`/`hide`
      clauses in this file (cleaner than suspension_pointer_test.
      dart.md which had a `show` clause).
  - construct_key: dart.import_directive.dart_io_to_csharp_using_system_io
    source_form: "import 'dart:io';"
    target_decision: >-
      Already covered by the package_under_test construct above
      (collapsed alongside the other imports). Restated separately for
      idiom-KB traceability: emit `using System.IO;` (covers `File`,
      `Path` for `File.ReadAllText` + `Path.Combine`). The narrow
      `dart:io` surface used here is identical to the bytecode-sibling
      arithmetic_test.dart.md (`File('<path>').readAsStringSync()`
      only -- no `Directory`, no async I/O, no temp dirs). REUSE the
      cached idiom verbatim.
    idiom_id: rf-dart-dart-io-to-csharp-system-io
    research_finding_id: rf-dart-dart-io-to-csharp-system-io
    nuance: >-
      I/O surface nuance (carry-forward KB cache hit per FR-012 --
      REUSE from arithmetic_test.dart.md / module_hierarchy_test.dart.
      md / external_io.dart.md). The only `dart:io` use here is
      `File('../programs/self.glp').readAsStringSync()`, called THREE
      times: once inside `setUpAll`, once inside the "assign.glp
      compiles and merges correctly" test, and once inside the "Z :=
      5 + 3 executes and binds Z to 8" test (the prelude is re-read
      per-test in those two -- the test author opted not to reuse
      `stdlibProg` from `setUpAll` in those two cases; the conversion
      preserves the per-test re-read). C# counterpart is the static
      `File.ReadAllText(<path>)` (UTF-8 default, sync, identical
      semantics). Relative-path nuance (LOAD-BEARING, carry-forward):
      Dart resolves `'../programs/self.glp'` against the test-runner
      CWD (`glp_runtime/`); C# resolves against the process CWD
      (which `dotnet test` does NOT set to the test project root).
      Faithful conversion routes through `Path.Combine(AppContext.
      BaseDirectory, "..", "programs", "self.glp")` -- same mechanism
      recorded in arithmetic_test.dart.md and module_hierarchy_test.
      dart.md.
  - construct_key: dart.package_test.main_entrypoint
    source_form: >-
      "void main() {
         late BytecodeProgram stdlibProg;
         setUpAll(() { ... });
         group('Arithmetic via := system predicate - Pointer Architecture', () { ... });
         group('End-to-end := system predicate - Pointer Architecture', () { ... });
         group('Variable Chain Dereferencing', () { ... });
       }"
    target_decision: >-
      Eliminate `void main()` entirely; xUnit discovers `[Fact]`
      methods by reflection. The file-level `late BytecodeProgram
      stdlibProg;` declaration + the file-level `setUpAll(() { ... })`
      hook + the THREE top-level `group(...)` calls become an outer
      test container `public class ArithmeticPointerTest` containing
      THREE nested classes (one per group) sharing a class-fixture
      `StdlibProgFixture` via xUnit's
      `ICollectionFixture<StdlibProgFixture>` + `[CollectionDefinition]`
      + `[Collection]` mechanism -- carry-forward verbatim from the
      bytecode-sibling arithmetic_test.dart.md (which had the SAME
      `late + setUpAll + group + group` shape, here extended to a
      third group). The outer-class wrapper preserves the file-level
      lift-target for the doc-comment (see library_directive
      construct above).
    idiom_id: rf-dart-test-main-to-xunit-class-with-facts
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Lifecycle + group nuance (carry-forward KB cache hit per FR-012
      -- REUSE; identical shape to arithmetic_test.dart.md). xUnit
      Shared Context reference
      (`https://xunit.net/docs/shared-context`): "Class Fixtures
      (shared object instance across tests in a single class);
      Collection Fixtures (shared object instances across multiple
      test classes)". Three nested classes -> Collection Fixture
      (one-time setup before any `[Fact]` runs in any class in the
      collection). The two diagnostic-only `print(...)` callsites in
      the source `setUpAll` body get routed through `_output.
      WriteLine($"...")` IF the fixture is wired with an
      `ITestOutputHelper`, or omitted entirely (canonical-simplest
      choice -- the `print` is non-load-bearing). Identifier-
      legalisation nuance: the `:=` glyph in the group labels is NOT
      a legal C# identifier character -- spell out as `Assign`
      (semantic spelling, matching the SUT spec
      `lib/runtime/system_predicates_impl.dart.md`'s `:=` assignment
      system predicate); preserve the original label verbatim via
      `[Trait("Group", "<original label>")]`.
  - construct_key: dart.package_test.setUpAll_lifecycle_hook
    source_form: |-
      "late BytecodeProgram stdlibProg;
       setUpAll(() {
         final stdlibSource = File('../programs/self.glp').readAsStringSync();
         final stdlibCompiler = GlpCompiler();
         stdlibProg = stdlibCompiler.compile(stdlibSource);
         print('Stdlib compiled: ${stdlibProg.ops.length} instructions');
       });"
    target_decision: >-
      Lift the `late BytecodeProgram stdlibProg;` file-scoped variable
      and its `setUpAll` initialiser into the ctor of a class fixture
      `public class StdlibProgFixture` whose constructor performs the
      compile ONCE per `dotnet test` invocation:
      `public BytecodeProgram StdlibProg { get; }` -- a get-only auto-
      property set in the ctor body via
      `var stdlibSource = File.ReadAllText(Path.Combine(AppContext.
      BaseDirectory, "..", "programs", "self.glp"));
      var stdlibCompiler = new GlpCompiler();
      StdlibProg = stdlibCompiler.Compile(stdlibSource);`
      The `print(...)` is OMITTED in canonical emission (diagnostic
      only; no assertion role). The THREE nested test classes each
      declare a fixture-parameter ctor `(StdlibProgFixture fixture,
      ITestOutputHelper output)`, store `_fixture` / `_output`
      fields, and reference `_fixture.StdlibProg` (in the End-to-end
      group's "assign.glp compiles and merges correctly" + "Z := 5 +
      3 executes and binds Z to 8" tests -- noting that the Dart
      source itself re-reads + re-compiles the prelude inside those
      two test bodies rather than reading the `setUpAll` value;
      codegen MAY collapse to `_fixture.StdlibProg` to avoid
      duplicate work, OR preserve the per-test re-read for fidelity).
      Wire the three nested classes via `[CollectionDefinition(
      "ArithmeticPreludePointer")]` + `[Collection(
      "ArithmeticPreludePointer")]` so the fixture instance is shared
      across all three (`ICollectionFixture<StdlibProgFixture>`).
      REUSE `rf-dart-setupall-to-xunit-class-fixture` verbatim from
      the bytecode-sibling arithmetic_test.dart.md.
    idiom_id: rf-dart-setupall-to-xunit-class-fixture
    research_finding_id: rf-dart-setupall-to-xunit-class-fixture
    nuance: >-
      Setup-lifecycle nuance (carry-forward KB cache hit per FR-012
      -- REUSE; identical to arithmetic_test.dart.md). Dart `setUpAll`
      = ONE-time setup before all tests in this scope (pub.dev
      `https://pub.dev/documentation/test_api/latest/test_api/
      setUpAll.html`). xUnit's nearest analogue at FILE-level scope
      is `ICollectionFixture<T>` + `[CollectionDefinition]` +
      `[Collection]` -- one-time setup before any `[Fact]` in any
      class in the collection. `late` initialisation maps to a get-
      only auto-property on the fixture (set once in ctor; never
      reassigned). The diagnostic `print` is OMITTED (non-load-
      bearing). DECISION-RECAP: re-read-vs-cache (the two End-to-end
      tests re-read the prelude inside their own bodies in the Dart
      source) -- spec prefers PRESERVE FIDELITY by emitting the
      per-test re-read in those two `[Fact]`s but a future codegen
      may collapse safely because the prelude file is immutable and
      `GlpCompiler.Compile` is deterministic; recorded as a
      conversion-time optimisation, not a semantic divergence.
  - construct_key: dart.local.late_typed_variable_declaration
    source_form: "late BytecodeProgram stdlibProg;"
    target_decision: >-
      The Dart `late BytecodeProgram stdlibProg;` file-scope variable
      is ELIMINATED -- replaced by the fixture get-only auto-property
      `public BytecodeProgram StdlibProg { get; }` on
      `StdlibProgFixture` (set once in ctor; consumed via
      `_fixture.StdlibProg`). No standalone C# `late` modifier exists;
      the get-only auto-property pattern is the canonical replacement.
      REUSE the cached idiom verbatim from the bytecode-sibling
      arithmetic_test.dart.md.
    idiom_id: rf-dart-late-variable-to-csharp-init-only-property
    research_finding_id: rf-dart-late-variable-to-csharp-init-only-property
    nuance: >-
      Late-binding nuance (carry-forward KB cache hit per FR-012 --
      REUSE). Dart `late` permits declare-now / initialise-later with
      runtime non-null assertion on first read; C# get-only auto-
      property preserves the once-set semantic (Microsoft Learn
      `learn.microsoft.com/en-us/dotnet/csharp/properties` -- "Get-
      only auto-implemented properties can be initialized only in
      the constructor"). The runtime non-null assertion of Dart
      `late` is replaced by C# flow-analysis at compile-time IF the
      property is declared non-nullable (`BytecodeProgram`, not
      `BytecodeProgram?`) and assigned in the ctor. Identifier:
      Dart `stdlibProg` (camelCase) -> C# `StdlibProg` (PascalCase
      for public property).
  - construct_key: dart.package_test.group_block.three_sibling_top_level_groups_one_shared_fixture
    source_form: >-
      "group('Arithmetic via := system predicate - Pointer Architecture', () { ... 7 tests });
       group('End-to-end := system predicate - Pointer Architecture', () { ... 3 tests });
       group('Variable Chain Dereferencing', () { ... 1 test });"
      // three SIBLING top-level groups, no nested groups, sharing a
      // file-level `setUpAll`/`late BytecodeProgram stdlibProg`
    target_decision: >-
      Three sibling top-level groups WITH a shared `setUpAll` fixture
      -> THREE nested public test classes inside the outer
      `ArithmeticPointerTest`, each tagged
      `[Collection("ArithmeticPreludePointer")]`, each consuming
      `StdlibProgFixture` + `ITestOutputHelper` via ctor injection:
      (1) `public class ArithmeticViaAssignSystemPredicatePointer
           Architecture` -- 7 `[Fact]` methods (one per `test()` inside
           the first group; carries `[Trait("Group", "Arithmetic via
           := system predicate - Pointer Architecture")]`);
      (2) `public class EndToEndAssignSystemPredicatePointer
           Architecture` -- 3 `[Fact]` methods (assign.glp compiles
           and merges correctly / user program with := compiles
           correctly with SRSW / Z := 5 + 3 executes and binds Z to 8;
           carries the analogous `[Trait]`);
      (3) `public class VariableChainDereferencing` -- 1 `[Fact]`
           method (arithmetic through variable chain; carries
           `[Trait("Group", "Variable Chain Dereferencing")]`).
      Class-name shape: PascalCase, derived by dropping hyphens /
      spaces / colons from the Dart group label and spelling out the
      `:=` glyph as `Assign` (precedent: arithmetic_test.dart.md).
      Per-test fresh-instance lifecycle (xUnit constructs one
      instance per `[Fact]`); the only shared state is the
      `StdlibProgFixture` (immutable).
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Group-topology nuance (carry-forward KB cache hit per FR-012
      -- REUSE the with-shared-setUpAll sub-case decided in
      arithmetic_test.dart.md). xUnit has NO first-class nested-
      group construct -- the per-group-becomes-a-class shape applies
      because (a) the topology is flat (siblings, no nesting) and
      (b) the file-level `setUpAll` decoration crosses groups and
      so MUST be lifted to a Collection Fixture spanning all three
      classes. Alternative single-class+`[Trait]` (binding_pointer_
      test.dart.md shape) was REJECTED because that shape applied
      only when there was NO shared `setUpAll`/`late` field. xUnit
      lifecycle: classes constructed once per `[Fact]`; Collection
      Fixture constructed exactly once. No async test methods (every
      closure is synchronous -- `setUpAll` is sync; tests are sync).
      Identifier-legalisation: `:=` -> `Assign` (semantic); `-` and
      spaces dropped; preserve original label verbatim via
      `[Trait("Group", "...")]` AND `[Fact(DisplayName = "...")]`.
  - construct_key: dart.package_test.test_call_simple
    source_form: >-
      "test('add/3 body kernel executes directly', () { ... });
       test('sub/3 body kernel', () { ... });
       test('mul/3 body kernel', () { ... });
       test('div/3 body kernel', () { ... });
       test('div/3 body kernel aborts on division by zero', () { ... });
       test('neg/2 body kernel', () { ... });
       test('sqrt_kernel/2 body kernel', () { ... });
       test('all standard body kernels are registered', () { ... });
       test('assign.glp compiles and merges correctly', () { ... });
       test('user program with := compiles correctly with SRSW', () { ... });
       test('Z := 5 + 3 executes and binds Z to 8', () { ... });
       test('arithmetic through variable chain', () { ... });"
    target_decision: >-
      One `[Fact(DisplayName = "<original Dart label>")] public void
      <PascalCasedIdentifier>() { <body> }` method per Dart `test()`,
      on the enclosing nested test class. Each method is sync `public
      void` (no `async`/`Future` surface in this file). Identifier-
      legalisation (carry-forward from arithmetic_test.dart.md, same
      11 names + 1 new): slashes/periods/spaces dropped; underscores
      in identifier-fragments dropped + PascalCase-joined
      (`sqrt_kernel` -> `SqrtKernel`); digits preserved + prefixed
      only if leading (none in this file); `:=` -> `Assign`
      (semantic); `+` -> `Plus` (semantic). Method-identifier
      manglings (12 total):
      - 'add/3 body kernel executes directly' ->
        `Add3BodyKernelExecutesDirectly`
      - 'sub/3 body kernel' -> `Sub3BodyKernel`
      - 'mul/3 body kernel' -> `Mul3BodyKernel`
      - 'div/3 body kernel' -> `Div3BodyKernel`
      - 'div/3 body kernel aborts on division by zero' ->
        `Div3BodyKernelAbortsOnDivisionByZero`
      - 'neg/2 body kernel' -> `Neg2BodyKernel`
      - 'sqrt_kernel/2 body kernel' -> `SqrtKernel2BodyKernel`
      - 'all standard body kernels are registered' ->
        `AllStandardBodyKernelsAreRegistered`
      - 'assign.glp compiles and merges correctly' ->
        `AssignGlpCompilesAndMergesCorrectly`
      - 'user program with := compiles correctly with SRSW' ->
        `UserProgramWithAssignCompilesCorrectlyWithSRSW`
      - 'Z := 5 + 3 executes and binds Z to 8' ->
        `ZAssign5Plus3ExecutesAndBindsZTo8`
      - 'arithmetic through variable chain' ->
        `ArithmeticThroughVariableChain`
      DisplayName preserves the exact original glyph sequence
      verbatim including `:=`, `+`, slashes -- so VS Test Explorer
      and `dotnet test --logger trx` report the original Dart name.
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Test-registration + identifier-shape nuance (carry-forward KB
      cache hit per FR-012 -- REUSE the same translation table from
      arithmetic_test.dart.md). All 12 test bodies are synchronous;
      no `skip:` / `timeout:` / `retry:` argument on any
      `test()` call. Per-test fresh-instance lifecycle (xUnit) means
      each `[Fact]` gets a fresh test-class instance; the ONLY shared
      state is the read-only `StdlibProg` from the Collection
      Fixture. No closure-captured mutable outer state in any test
      body -- each starts with `final rt = GlpRuntime();` (the
      runtime is per-test, not shared).
  - construct_key: dart.local.final_typed_constructor_invocation_no_new
    source_form: >-
      "final rt = GlpRuntime();
       final stdlibCompiler = GlpCompiler();
       final userCompiler = GlpCompiler();
       final runner = BytecodeRunner(mergedProg);
       final sched = Scheduler(rt: rt, runner: runner);"
    target_decision: >-
      Each `final <name> = <Ctor>(...);` local -> `var <name> = new
      <Ctor>(...);`. C# requires `new` on constructor invocations.
      Named-arg call `Scheduler(rt: rt, runner: runner)` -> `new
      Scheduler(rt: rt, runner: runner)` -- C# 4.0+ supports
      identical `name: value` colon-form named-arg syntax. All target
      classes (`GlpRuntime`, `GlpCompiler`, `BytecodeRunner`,
      `Scheduler`) live in the collapsed `using <RootNs>.{Compiler,
      Bytecode, Runtime};` directives. Applies to every such local
      across all 12 `[Fact]`s; in this file every `[Fact]` of the
      first and third group declares `var rt = new GlpRuntime();` as
      its first line and the End-to-end group adds compiler/runner/
      scheduler locals.
    idiom_id: rf-dart-final-local-to-csharp-var
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Local-mutability + named-argument nuance (carry-forward KB
      cache hit per FR-012 -- REUSE from arithmetic_test.dart.md /
      varref_pointer_test.dart.md). Dart `final` local -> C# `var`
      (single-assignment intent lost at the language level but
      preserved structurally -- no body reassigns these locals). All
      target classes are reference types in both languages per the
      SUT specs. Named-arg syntax direct 1:1 (Microsoft Learn
      `learn.microsoft.com/en-us/dotnet/csharp/programming-guide/
      classes-and-structs/named-and-optional-arguments`). C# `new`
      mandatory per `rf-dart-constructor-invocation-implicit-new-to-
      csharp-new` (cached).
  - construct_key: dart.record_destructuring.positional_pair_writer_reader
    source_form: >-
      "final (xWriter, xReader) = rt.heap.allocateVariable();
       final (yWriter, yReader) = rt.heap.allocateVariable();
       final (resultWriter, resultReader) = rt.heap.allocateVariable();
       final (resultWriter, _) = rt.heap.allocateVariable();
       final (zWriter, zReader) = rt.heap.allocateVariable();"
    target_decision: >-
      Translate Dart positional-record destructuring `final (a, b) =
      expr;` to C# value-tuple deconstruction `var (a, b) = expr;`.
      Emit (typical per-test pattern):
      `var (xWriter, xReader) = rt.Heap.AllocateVariable();
       var (yWriter, yReader) = rt.Heap.AllocateVariable();
       var (resultWriter, resultReader) = rt.Heap.AllocateVariable();`
      The SUT spec `lib/runtime/heap_fcp.dart.md` records
      `Heap.allocateVariable()` as returning `(int writerAddr, int
      readerAddr)` -> C# `(int, int)` value tuple. The `_` discard
      preserves verbatim in both languages (Microsoft Learn discards
      reference). In the `sub/3`, `mul/3`, `div/3`, `div/3 abort`,
      `neg/2`, `sqrt_kernel/2` tests the second tuple slot is
      discarded (`(resultWriter, _)`) -- preserved verbatim.
    idiom_id: rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction
    research_finding_id: rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction
    nuance: >-
      Record-destructuring nuance (carry-forward KB cache hit per
      FR-012 -- REUSE; same shape as binding_pointer_test.dart.md /
      varref_pointer_test.dart.md / suspension_pointer_test.dart.md
      / arithmetic_test.dart.md). Width nuance: the SUT-side
      `AllocateVariable` returns `(int, int)` per heap_fcp.dart.md
      and the precedent heap-test family kept the SUT-side `int`
      (not `long`) -- preserved here. Discard `_` is structural in
      both languages (no variable allocated). Field-position
      semantics: Dart positional `$1`/`$2` <-> C# `Item1`/`Item2`;
      with named records (recorded SUT-side), the named tuple-field
      names preserve when both sides use the named form.
  - construct_key: dart.method_call.heap_writer_bind_constterm
    source_form: >-
      "rt.heap.bindWriter(xWriter, ConstTerm(5));
       rt.heap.bindWriter(yWriter, ConstTerm(3));
       rt.heap.bindWriter(xWriter, ConstTerm(10));
       rt.heap.bindWriter(yWriter, ConstTerm(4));
       rt.heap.bindWriter(xWriter, ConstTerm(7));
       rt.heap.bindWriter(yWriter, ConstTerm(6));
       rt.heap.bindWriter(xWriter, ConstTerm(15));
       rt.heap.bindWriter(yWriter, ConstTerm(0));
       rt.heap.bindWriter(xWriter, ConstTerm(42));
       rt.heap.bindWriter(xWriter, ConstTerm(16));
       rt.heap.bindWriter(yWriter, ConstTerm(5));
       rt.heap.bindWriter(zWriter, ConstTerm(3));"
    target_decision: >-
      Translate `rt.heap.bindWriter(<addr>, ConstTerm(<lit>));` to
      `rt.Heap.BindWriter(<addr>, new ConstTerm(<lit>));` -- direct
      verbatim transliteration with PascalCasing (`heap` -> `Heap`,
      `bindWriter` -> `BindWriter`) per the SUT specs `lib/runtime/
      runtime.dart.md` (records the `Heap` property on `GlpRuntime`)
      and `lib/runtime/heap_fcp.dart.md` (records the `BindWriter`
      method on `Heap`/`HeapFCP`). The return value `List<Suspension
      Record>` is DISCARDED at every call site in this file (no
      `final activations = ...` capture -- unlike binding_pointer_
      test.dart.md and suspension_pointer_test.dart.md where the
      activation list was asserted). Codegen emits the call as a
      statement-expression discarding the returned `List<Suspension
      Record>`. This is the heap-pointer-architecture replacement
      for the legacy `bindVariableConst(<writer>, <int>)` call used
      in the bytecode-sibling arithmetic_test.dart.md -- per the
      file lead doc-comment "Adapted from: test/bytecode/arithmetic_
      test.dart ... Tests that arithmetic operations work correctly
      with the new pointer-based heap architecture".
    idiom_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Bind-writer semantics + return-discard nuance (carry-forward
      KB cache hit per FR-012 -- REUSE from binding_pointer_test.
      dart.md / suspension_pointer_test.dart.md). `BindWriter`
      returns `List<SuspensionRecord>` of activations per heap_fcp.
      dart.md construct `dart.bind_writer_family.callback_control_
      with_in_place_mutation_returning_activation_list`. Discarding
      a non-void return in Dart is silent (just don't capture the
      result); C# is identical (statement-expression discards
      automatically -- no `_ =` needed unless the analyzer is set
      to warn on unused return values; spec default omits the `_=`
      for readability). Integer literal width: Dart `int` is 64-bit
      on the VM; literal `5`/`3`/`10`/`4`/`7`/`6`/`15`/`0`/`42`/
      `16` fit `int` and `long`; the SUT spec records `ConstTerm`'s
      payload as `object?` (per terms.dart.md), so the literal
      boxes transparently. No literal-width nuance fires here (any
      width works for the boxed payload).
  - construct_key: dart.constructor_call.const_term_with_int_or_double_literal
    source_form: >-
      "ConstTerm(5), ConstTerm(3), ConstTerm(10), ConstTerm(4),
       ConstTerm(7), ConstTerm(6), ConstTerm(15), ConstTerm(0),
       ConstTerm(42), ConstTerm(16)"
    target_decision: >-
      `new ConstTerm(<lit>)` at every site. SUT type per terms.
      dart.md (idiom rf-dart-sumleaf-no-eq-to-csharp-class-no-record)
      is `sealed class ConstTerm : Term` with a single nullable
      `object? Value` field; C# integer literals box transparently
      into `object?`. Carry-forward from binding_pointer_test.dart.
      md / suspension_pointer_test.dart.md.
    idiom_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      Literal-typing nuance (carry-forward KB cache hit per FR-012
      -- REUSE). All ConstTerm payloads in this file are integer
      literals (Dart `int`); the boxed `object?` C# representation
      handles them transparently. The expected RESULT values
      asserted later are mixed: integer (8, 6, 42, -42), double
      (3.75, 4.0). Equality nuance for ConstTerm payloads is
      handled at the `(value as ConstTerm).value` assertion site
      (see expect_isA_then_equals construct below), not at the
      constructor site.
  - construct_key: dart.constructor_call.var_ref_single_int_addr
    source_form: >-
      "VarRef(xReader), VarRef(yReader), VarRef(resultWriter),
       VarRef(xReader), VarRef(yReader), VarRef(resultWriter),
       VarRef(xReader), VarRef(yReader), VarRef(resultWriter),
       VarRef(xReader), VarRef(yReader), VarRef(resultWriter),
       VarRef(xReader), VarRef(yReader), VarRef(resultWriter),
       VarRef(xReader), VarRef(resultWriter),
       VarRef(xReader), VarRef(resultWriter),
       VarRef(resultWriter)  // inside CallEnv args literal
       VarRef(xReader), VarRef(zReader), VarRef(resultWriter)"
    target_decision: >-
      `new VarRef(<addr>)` at every site. SUT type per terms.dart.md
      (idiom rf-dart-class-eq-on-single-int-field-to-csharp-
      iequatable) is `sealed class VarRef : Term, IEquatable<VarRef>`
      with a single `int Addr` field (carry-forward from binding_
      pointer_test.dart.md / varref_pointer_test.dart.md). The
      addresses `xReader`, `yReader`, `resultWriter`, `xReader` (the
      bound-to-Y chain case), `zReader`, `resultReader` are all `int`
      locals returned by deconstruction of `AllocateVariable`'s
      `(int, int)` tuple.
    idiom_id: rf-dart-class-eq-on-single-int-field-to-csharp-iequatable
    research_finding_id: rf-dart-class-eq-on-single-int-field-to-csharp-iequatable
    nuance: >-
      Value-equality + reference-allocation nuance (carry-forward KB
      cache hit per FR-012 -- REUSE from binding_pointer_test.dart.
      md / varref_pointer_test.dart.md). `VarRef` is a reference
      type in both languages but with structural equality (over
      `Addr`) via the C# `IEquatable<VarRef>` contract. This file
      does NOT exercise `Assert.Equal` on `VarRef`-typed values
      (only on the `Value` field of the bound `ConstTerm`) -- so
      the equality contract is not the load-bearing assertion here;
      it's exercised in varref_pointer_test.dart.md instead. The
      VarRef instances ARE used as `VarRef[]` kernel arguments (see
      next construct).
  - construct_key: dart.expression.list_literal_of_VarRef_constructors
    source_form: >-
      "[xRef, yRef, resultRef]
       [VarRef(xReader), VarRef(yReader), VarRef(resultWriter)]
       [VarRef(xReader), VarRef(resultWriter)]
       [VarRef(xReader), VarRef(zReader), VarRef(resultWriter)]"
    target_decision: >-
      The kernel-call array-literal `[ref1, ref2, ref3]` maps to a
      C# array initializer `new[] { ref1, ref2, ref3 }` (or the
      explicitly-typed `new VarRef[] { ref1, ref2, ref3 }`). For
      the inline-constructor form `[VarRef(xReader), VarRef(yReader),
      VarRef(resultWriter)]` -> `new[] { new VarRef(xReader), new
      VarRef(yReader), new VarRef(resultWriter) }`. The kernel
      delegate signature per the SUT spec
      `lib/runtime/body_kernels.dart.md` takes `(GlpRuntime runtime,
      List<VarRef> args)` -> C# `(GlpRuntime runtime, IReadOnlyList<
      VarRef> args)` or `List<VarRef>` -- the array implicitly
      converts to `IReadOnlyList<T>` (C# array-to-IReadOnlyList
      implicit conversion). Spec default emits the simplest
      `new[] { ... }` form. Eight call sites in this file (one per
      kernel test except `all standard body kernels are registered`
      which doesn't invoke a kernel).
    idiom_id: rf-dart-list-literal-of-constructors-to-csharp-array-init
    research_finding_id: rf-dart-list-literal-of-constructors-to-csharp-array-init
    nuance: >-
      List-literal nuance (carry-forward KB cache hit per FR-012 --
      REUSE from arithmetic_test.dart.md / fairness_scheduler_loop_
      test.dart.md). Dart `[a, b, c]` of homogeneous element type
      maps to C# `new[] { a, b, c }` (type-inferred from elements)
      OR `new List<T> { a, b, c }` collection-initialiser. The
      array form is preferred at kernel call sites because the
      kernel signature accepts `IReadOnlyList<VarRef>` (per body_
      kernels.dart.md) and array conversion is implicit. C# 12
      collection-expression `[a, b, c]` is the modern alternative
      but spec default keeps the LCD-portable `new[] { ... }`
      form.
  - construct_key: dart.method_call.body_kernels_lookup_then_invoke
    source_form: >-
      "final kernel = rt.bodyKernels.lookup('_add', 3);
       expect(kernel, isNotNull, reason: '_add/3 kernel should be registered');
       final result = kernel!(rt, [xRef, yRef, resultRef]);

       final kernel = rt.bodyKernels.lookup('_sub', 3);
       expect(kernel, isNotNull);
       final result = kernel!(rt, [...]);

       // (and three more sub/mul/div/div-abort/neg/sqrt sites without reason:)"
    target_decision: >-
      Three composed sub-translations carried out together
      (carry-forward verbatim from arithmetic_test.dart.md):
      (1) `rt.bodyKernels.lookup('_add', 3)` -> `rt.BodyKernels.Lookup
      ("_add", 3);` -- PascalCase member-access chain. Returns
      `BodyKernel?` (nullable delegate) per body_kernels.dart.md.
      Single-quoted Dart string -> C# double-quoted string.
      (2) `expect(kernel, isNotNull, reason: '<msg>')` ->
      `Assert.NotNull(kernel); // <msg>` (with inline `//` comment for
      the `reason:` text -- `Assert.NotNull` has NO user-message
      overload). For the `sub/3`/`mul/3`/`div/3`/etc. test sites
      that pass `expect(kernel, isNotNull)` without a `reason:`
      argument -> `Assert.NotNull(kernel);` (no comment).
      (3) `kernel!(rt, [xRef, yRef, resultRef])` -> `kernel!(rt,
      new[] { xRef, yRef, resultRef })` -- the SUT spec records
      `BodyKernel` as a Dart function-typedef -> C# `delegate`
      (callable directly via `kernel!(args)` syntax -- delegates
      are callable in C#). The Dart `!` non-null assertion (runtime
      throw on null) -> C# `!` null-forgiving (compile-time only),
      but the immediately preceding `Assert.NotNull(kernel)`
      GUARANTEES non-null so the runtime-throw behaviour is
      faithfully reproduced via the assertion + forgiving-operator
      composition.
    idiom_id: rf-dart-expect-isnotnull-to-xunit-assertnotnull
    research_finding_id: rf-dart-expect-isnotnull-to-xunit-assertnotnull
    nuance: >-
      Three composed idioms (all cached, no re-research -- carry-
      forward verbatim from arithmetic_test.dart.md):
      (a) `expect(x, isNotNull)` -> `Assert.NotNull(x)`; `reason:`
      text -> inline `//` comment (xunit.net Assert API:
      `Assert.NotNull(object)` -- no userMessage overload);
      (b) Dart `!` null-assertion (runtime-throw) vs C# `!`
      null-forgiving (compile-time-only) -- LOAD-BEARING SEMANTIC
      DIVERGENCE explicitly addressed via the preceding
      Assert.NotNull guarantee (the recorded
      `rf-dart-bang-null-assertion-to-csharp-null-forgiving` idiom
      documents this composition);
      (c) Delegate invocation: `f!(args)` works on C# delegates
      identically to Dart `Function?` invocation (Microsoft Learn
      delegates reference). The kernel-delegate's argument list
      shape is `(GlpRuntime, IReadOnlyList<VarRef>)` per body_
      kernels.dart.md.
  - construct_key: dart.package_test.expect_equality_against_enum_BodyKernelResult
    source_form: >-
      "expect(result, equals(BodyKernelResult.success));
       expect(result, equals(BodyKernelResult.success));
       expect(result, equals(BodyKernelResult.success));
       expect(result, equals(BodyKernelResult.success));
       expect(result, equals(BodyKernelResult.abort));
       expect(result, equals(BodyKernelResult.success));
       expect(result, equals(BodyKernelResult.success));
       expect(result, equals(BodyKernelResult.success));"
    target_decision: >-
      Translate `expect(result, equals(BodyKernelResult.<member>))`
      to `Assert.Equal(BodyKernelResult.<Member>, result);` --
      EXPECTED-FIRST argument-order swap (the well-known footgun:
      xUnit reverses Dart's ACTUAL-FIRST order). Enum-member
      PascalCase: `success` -> `Success`, `abort` -> `Abort` -- the
      enum type itself is owned by SUT spec `lib/runtime/body_
      kernels.dart.md` which records `public enum BodyKernelResult
      { Success, Abort }`. Eight callsites in this file (7 success
      + 1 abort for the div-by-zero test + 1 success for the
      variable-chain test).
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order + enum-PascalCase nuance (carry-forward KB
      cache hit per FR-012 -- REUSE from arithmetic_test.dart.md).
      xUnit `Assert.Equal<T>(T expected, T actual)` reverses Dart's
      `expect(actual, matcher)` order; smoke_test.dart-spec
      recorded this footgun; codegen ALWAYS swaps. Enum-member
      casing: Microsoft Learn coding conventions ("Use PascalCase
      for ... enums and enum values"). The SUT enum spec is
      authoritative.
  - construct_key: dart.package_test.expect_isA_then_value_field_equals
    source_form: |-
      "final value = rt.heap.getValue(resultWriter);
       expect(value, isNotNull);
       expect(value, isA<ConstTerm>());
       expect((value as ConstTerm).value, equals(8));

       // (plus 6 more sites with expected 6, 42, 3.75, -42, 4.0, 8)"
    target_decision: >-
      Translate the three-step verification (read value, assert
      non-null, assert exact type, cast and compare scalar) to the
      xUnit-idiomatic two-step compound form:
      `var value = rt.Heap.GetValue(resultWriter);
       Assert.NotNull(value);
       var constValue = Assert.IsType<ConstTerm>(value);
       Assert.Equal(8, constValue.Value);`
      The `Assert.IsType<T>(object)` overload RETURNS the typed
      value, so the Dart `(value as ConstTerm).value` downcast +
      property access COLLAPSES into the typed-return -- eliminating
      the separate cast statement. EXACT-type semantic; subtypes
      would use `Assert.IsAssignableFrom<T>` (NOT this case -- SUT
      spec terms.dart.md records `ConstTerm` as `sealed`). Some
      tests SKIP the `isNotNull` and `isA<ConstTerm>` checks and
      jump straight to `expect((value as ConstTerm).value, equals(
      <expected>))` -- in those cases the compound form is still
      correct: `var constValue = Assert.IsType<ConstTerm>(value);
      Assert.Equal(<expected>, constValue.Value);` (the
      `Assert.IsType` throw-on-mismatch subsumes both the type and
      the non-null check). Eight callsites in this file (`add/3` =
      8; `sub/3` = 6; `mul/3` = 42; `div/3` = 3.75; `neg/2` = -42;
      `sqrt_kernel/2` = 4.0; `Z := 5 + 3` = 8; `arithmetic through
      variable chain` = 8).
    idiom_id: rf-dart-expect-isa-to-xunit-istype
    research_finding_id: rf-dart-expect-isa-to-xunit-istype
    nuance: >-
      Compound matcher + floating-point nuance (carry-forward KB
      cache hit per FR-012 -- REUSE from arithmetic_test.dart.md).
      xUnit `Assert.IsType<T>(object)` returns the typed value
      (xunit.net Assert API reference); Dart `(v as T).field`
      collapses into the typed-return. Exact-type vs subtype-
      tolerant: `ConstTerm` is sealed per terms.dart.md -- exact-
      type is correct. Integer-literal width: Dart `int` payload
      box value (`8`, `6`, `42`, `-42`) compares as `object` via
      xUnit's `Assert.Equal<T>` which routes through
      `EqualityComparer<T>.Default` -- works on boxed ints
      transparently. Floating-point literals (`3.75`, `4.0`):
      Dart `equals(<num>)` does strict numeric equality;
      `Assert.Equal(double, double)` also does strict-equality;
      `Assert.Equal(double, double, int precision)` is the
      tolerance-safe variant. Spec default for THIS file: emit the
      strict-equality form (matching Dart's `equals` matcher
      semantics); recommend the precision overload `Assert.Equal(
      3.75, constValue.Value, 10)` / `Assert.Equal(4.0,
      constValue.Value, 10)` as the floating-point-safe choice IF
      the boxed payload is unboxed as `double` rather than as
      `object`. The unary-minus literal `-42` (neg/2 test) is a
      compile-time integer literal in both languages. PAYLOAD-
      BOXING nuance: `ConstTerm.Value` is `object?` (per terms.
      dart.md); the boxed double `3.75` compares as `object`
      against the C# literal `3.75` which is `double`; xUnit
      auto-boxes and the `Equals(object, object)` call dispatches
      to `double.Equals(double)` -- bit-pattern equality. The 3.75
      = 15/4 case is exact in IEEE-754 (no rounding); 4.0 from
      sqrt(16) is also exact. No tolerance needed for THIS file.
  - construct_key: dart.method_call.heap_query_get_value_returning_nullable_term
    source_form: >-
      "final value = rt.heap.getValue(resultWriter);
       // also: rt.heap.isFullyBound(resultWriter)"
    target_decision: >-
      `rt.Heap.GetValue(<addr>)` returns `Term?` per heap_fcp.dart.md
      construct `dart.method.get_value_nullable_term_from_deref`.
      `rt.Heap.IsFullyBound(<addr>)` returns `bool` per heap_fcp.
      dart.md construct `dart.method.is_fully_bound_via_deref_
      returning_bool`. Used twice in this file: (a) inside each
      kernel test to read back the result and assert via the
      `isA<ConstTerm>`/cast/`equals` chain; (b) in the "Z := 5 + 3
      executes and binds Z to 8" test to gate the `if (isBound) {
      ... } else { fail(...); }` control flow. Carry-forward
      verbatim from binding_pointer_test.dart.md.
    idiom_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Nullable-return + bool-return nuance (carry-forward KB cache
      hit per FR-012 -- REUSE from binding_pointer_test.dart.md).
      `GetValue` returning `Term?` is mandatory -- the value may be
      null (unbound) -- which the Z-end-to-end test relies on for
      the failure-path. `IsFullyBound` returning `bool` per heap_
      fcp.dart.md. PascalCase casing: Dart `getValue` ->
      `GetValue`; `isFullyBound` -> `IsFullyBound`. THREADING
      DEPENDENCY NOTE: heap_fcp.dart.md is currently `escalated` on
      the HeapFCP threading model (single-owning-context vs
      ConcurrentDictionary/Interlocked) -- this file does NOT
      exercise threading (every test allocates its OWN `rt = new
      GlpRuntime()` and performs only synchronous calls; no `await`,
      no `Isolate.spawn`, no cross-thread aliasing); the
      `GetValue`/`IsFullyBound` call-site shape mapped here is
      orthogonal to that ruling. When the heap_fcp ruling lands,
      this file's specced call sites remain valid; the only
      potential change is the SUT method dispatch (e.g. whether
      `GetValue` is `[ThreadStatic]`-scoped or lock-free) which is
      internal to heap_fcp.dart.md, not visible at the test-spec
      level. Recorded as inherited dependency, NOT a new escalation.
  - construct_key: dart.method_call.heap_writer_bind_writer_to_reader
    source_form: "rt.heap.bindWriterToReader(xWriter, yReader);"
    target_decision: >-
      `rt.Heap.BindWriterToReader(xWriter, yReader);` per heap_fcp.
      dart.md (idiom rf-dart-bind-writer-family-callsite-to-csharp-
      pascalcase-methods). Used ONCE in this file -- the
      `arithmetic through variable chain` test of the "Variable
      Chain Dereferencing" group, which sets up `X -> Y -> 5` by
      binding `Y` to `ConstTerm(5)`, then binding `X` to `Y` via
      `bindWriterToReader(xWriter, yReader)`. The return value
      (`List<SuspensionRecord>`) is discarded (no `final acts = ...`
      capture).
    idiom_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Variable-chain semantics nuance (carry-forward KB cache hit
      per FR-012 -- REUSE from binding_pointer_test.dart.md /
      suspension_pointer_test.dart.md). `BindWriterToReader(w, r)`
      sets `cells[w].Content = new Pointer(r)` keeping `Tag = WrtTag`;
      forwards suspensions; returns empty activation list. The
      pointer-architecture file lead doc-comment ("Tests that
      arithmetic operations work correctly with the new pointer-
      based heap architecture") DOCUMENTS the purpose: arithmetic
      kernels MUST dereference through the chain -- this test
      asserts that `_add` correctly walks `X -> Y -> 5` and binds
      `result = 5 + 3 = 8`. Codegen MUST preserve the call shape
      verbatim.
  - construct_key: dart.method_call.body_kernels_has_string_int
    source_form: >-
      "rt.bodyKernels.has('_add', 3);
       rt.bodyKernels.has('_sub', 3);
       rt.bodyKernels.has('_mul', 3);
       rt.bodyKernels.has('_div', 3);
       rt.bodyKernels.has('_idiv', 3);
       rt.bodyKernels.has('_mod', 3);
       rt.bodyKernels.has('_neg', 2);
       rt.bodyKernels.has('_abs', 2);
       rt.bodyKernels.has('_sqrt', 2);
       rt.bodyKernels.has('_sin', 2);
       rt.bodyKernels.has('_cos', 2);
       rt.bodyKernels.has('_tan', 2);
       rt.bodyKernels.has('_exp', 2);
       rt.bodyKernels.has('_ln', 2);
       rt.bodyKernels.has('_log10', 2);
       rt.bodyKernels.has('_pow', 3);
       rt.bodyKernels.has('_asin', 2);
       rt.bodyKernels.has('_acos', 2);
       rt.bodyKernels.has('_atan', 2);
       rt.bodyKernels.has('_integer', 2);
       rt.bodyKernels.has('_real', 2);
       rt.bodyKernels.has('_round', 2);
       rt.bodyKernels.has('_floor', 2);
       rt.bodyKernels.has('_ceil', 2);"
    target_decision: >-
      `rt.BodyKernels.Has(<name>, <arity>);` returning `bool` per
      body_kernels.dart.md. PascalCase rename (`has` -> `Has`). 24
      callsites in this file, all wrapped by `expect(..., isTrue)`
      (see expect_isTrue construct below). Single-quoted Dart
      strings -> C# double-quoted. Integer arity literals
      preserved as `int`.
    idiom_id: rf-dart-camel-to-csharp-pascal-method-rename
    research_finding_id: rf-dart-camel-to-csharp-pascal-method-rename
    nuance: >-
      Method-renaming nuance (carry-forward KB cache hit per FR-012
      -- REUSE from arithmetic_test.dart.md verbatim, same 24-call
      pattern). The integer arity literals (`2`, `3`) are well
      within both `int` and `long` ranges; spec emits `int` to
      match the SUT-side `Has(string name, int arity)` signature
      recorded in body_kernels.dart.md.
  - construct_key: dart.package_test.expect_boolean_predicate_istrue
    source_form: >-
      "expect(rt.bodyKernels.has('_add', 3), isTrue);
       // and 23 more such calls in 'all standard body kernels are registered'
       expect(prog.ops.isNotEmpty, isTrue);
       expect(prog.labels.containsKey('compute_sum/1'), isTrue);
       expect(mergedProg.labels.containsKey(':=/2'), isTrue,
           reason: 'Merged program should contain :=/2 from stdlib');
       expect(mergedProg.labels.containsKey('hello/0'), isTrue,
           reason: 'Merged program should contain hello/0 from user code');"
    target_decision: >-
      Translate `expect(<bool-expr>, isTrue);` -> `Assert.True(<bool-
      expr>);` -- xUnit `Assert.True(bool)`. For the variant with
      `reason:` argument -> `Assert.True(<bool-expr>, "<msg>");` --
      `Assert.True` HAS a user-message overload (unlike `Assert.
      NotNull` / `Assert.Equal`). Member-access PascalCase chain:
      `bodyKernels.has` -> `BodyKernels.Has`; `prog.ops.isNotEmpty`
      -> `prog.Ops.Count > 0` (C# `IReadOnlyCollection<T>` has no
      `IsNotEmpty` property; `Any()` requires `using System.Linq;`;
      `Count > 0` is the portable LCD choice -- carry-forward from
      arithmetic_test.dart.md); `prog.labels.containsKey('k')` ->
      `prog.Labels.ContainsKey("k")` (Dart `Map.containsKey` -> C#
      `IDictionary.ContainsKey`, 1:1 semantic match).
    idiom_id: rf-dart-expect-istrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-istrue-to-xunit-assert-true
    nuance: >-
      Boolean-predicate + user-message nuance (carry-forward KB
      cache hit per FR-012 -- REUSE from arithmetic_test.dart.md).
      `Assert.True(bool)` (no message) and `Assert.True(bool,
      string)` (with user message) -- xunit.net Assert API. The
      Dart `reason:` text routes to the `userMessage` parameter
      verbatim. Map-key existence: Dart `Map<K,V>.containsKey(K)`
      maps 1:1 to C# `IDictionary<K,V>.ContainsKey(K)` (Microsoft
      Learn). String-glyph nuance: the `:=/2` literal contains the
      `:=` glyph, which is a legal C# string-literal content
      (Unicode); preserve verbatim as `":=/2"`.
  - construct_key: dart.method_call.bytecode_program_merge_then_labels_indexer
    source_form: |-
      "final mergedProg = userProg.merge(stdlibProg);
       expect(mergedProg.labels.containsKey(':=/2'), isTrue, reason: '...');
       expect(mergedProg.labels.containsKey('hello/0'), isTrue, reason: '...');
       final entryPc = mergedProg.labels['compute_sum/1'];
       expect(entryPc, isNotNull, reason: 'compute_sum/1 should exist');"
    target_decision: >-
      Translate the merge + label-lookup chain to:
      `var mergedProg = userProg.Merge(stdlibProg);
       Assert.True(mergedProg.Labels.ContainsKey(":=/2"),
           "Merged program should contain :=/2 from stdlib");
       Assert.True(mergedProg.Labels.ContainsKey("hello/0"),
           "Merged program should contain hello/0 from user code");
       Assert.True(mergedProg.Labels.ContainsKey("compute_sum/1"),
           "compute_sum/1 should exist");
       var entryPc = mergedProg.Labels["compute_sum/1"];`
      (the `!`-asserted `mergedProg.labels['compute_sum/1']` becomes
      a `ContainsKey`-gate plus indexer access; the C# indexer
      throws `KeyNotFoundException` on miss -- matching Dart's `!`
      runtime-throw intent. Spec default emits the LCD-portable
      indexer-throws form rather than `TryGetValue`-then-null since
      the Dart code asserts the key present immediately above).
      Method PascalCase (`merge` -> `Merge`); `BytecodeProgram.
      Merge` returns a new `BytecodeProgram` (immutable merge per
      lib/bytecode/runner.dart.md).
    idiom_id: rf-dart-camel-to-csharp-pascal-method-rename
    research_finding_id: rf-dart-camel-to-csharp-pascal-method-rename
    nuance: >-
      Indexer + dictionary-lookup nuance (carry-forward KB cache
      hit per FR-012 -- REUSE from arithmetic_test.dart.md). LOAD-
      BEARING SEMANTIC DIVERGENCE: Dart `Map<K,V>['<key>']` returns
      `V?` (nullable; returns null on miss); C# `IDictionary<K,V>.
      this[<key>]` THROWS `KeyNotFoundException` on miss. Faithful
      conversion: (a) precede the indexer with `ContainsKey`
      (eager throw -> explicit) or (b) use `TryGetValue` (no throw
      -> nullable). The Dart source writes BOTH styles: explicit
      `labels.containsKey('...')` and indexer-with-`!`-assert. Spec
      emits the indexer-throws form when the Dart code already
      asserts `containsKey` immediately above (matching the Dart
      `!` runtime-throw intent and the C# `KeyNotFoundException`
      throw).
  - construct_key: dart.method_call.gq_enqueue_with_goalref_constructor
    source_form: "rt.gq.enqueue(GoalRef(goalId, entryPc!));"
    target_decision: >-
      Translate to `rt.Gq.Enqueue(new GoalRef(goalId, entryPc!));` --
      composed cached idioms: property-chain PascalCase + `new`-on-
      ctor + null-forgiving. The SUT specs `lib/runtime/runtime.
      dart.md` (the `Gq` property = `GoalQueue` reference on
      `GlpRuntime`) and `lib/runtime/goal_queue.dart.md` (the
      `Enqueue(GoalRef)` method) own the names. `entryPc` is `int?`
      from the labels-indexer (after the `ContainsKey` gate); the
      `!` is C#'s null-forgiving. `GoalRef` is `readonly record
      struct` per machine_state.dart.md.
    idiom_id: rf-dart-property-chain-method-call-to-csharp
    research_finding_id: rf-dart-property-chain-method-call-to-csharp
    nuance: >-
      Carry-forward KB cache hit per FR-012 -- REUSE from
      arithmetic_test.dart.md. The composed nuances (Dart `!`
      runtime-throw vs C# `!` compile-time annotation) are recorded
      in arithmetic_test.dart.md. The preceding `ContainsKey` +
      indexer access guarantees non-null at runtime, so the C# `!`
      -> `int` unwrap is faithful.
  - construct_key: dart.method_call.scheduler_drain_with_named_args
    source_form: "final ran = sched.drain(maxCycles: 100, debug: true, debugOutput: true);"
    target_decision: >-
      `var ran = sched.Drain(maxCycles: 100, debug: true,
      debugOutput: true);` -- direct named-argument transcription
      preserving each parameter (C# `name: value` colon-form,
      identical to Dart). Method PascalCase (`drain` -> `Drain`);
      parameter names stay lowerCamelCase per the SUT spec
      `lib/runtime/scheduler.dart.md`. Boolean literals `true`/
      `false` map verbatim. REUSE
      `rf-dart-named-arg-to-csharp-named-arg` cached.
    idiom_id: rf-dart-named-arg-to-csharp-named-arg
    research_finding_id: rf-dart-named-arg-to-csharp-named-arg
    nuance: >-
      Named-argument carry-forward KB cache hit per FR-012 -- REUSE
      from arithmetic_test.dart.md verbatim, same `Drain(maxCycles:
      100, debug: true, debugOutput: true)` call shape. The Drain
      return type is owned by the SUT scheduler spec; `ran.length`
      later interpolates to `ran.Count` (or `.Length`) per the SUT
      collection-shape decision.
  - construct_key: dart.map_literal.int_to_varref_arg_map
    source_form: |-
      "final env = CallEnv(args: {
         0: VarRef(resultWriter),
       });"
    target_decision: >-
      Translate the Dart constructor-with-map-literal call
      `CallEnv(args: {0: VarRef(resultWriter)})` to C#:
      `var env = new CallEnv(args: new Dictionary<int, VarRef> {
       [0] = new VarRef(resultWriter) });`
      (index-initialiser form for integer-literal keys; cleaner
      than the `{ {0, new VarRef(resultWriter)} }` collection-
      initialiser form). The SUT spec `lib/runtime/machine_state.
      dart.md` records `CallEnv.args` as `Map<int, VarRef>` -> C#
      `IReadOnlyDictionary<int, VarRef>` or `Dictionary<int,
      VarRef>` per the SUT spec's recorded collection-shape
      decision.
    idiom_id: rf-dart-map-literal-to-csharp-dictionary-initializer
    research_finding_id: rf-dart-map-literal-to-csharp-dictionary-initializer
    nuance: >-
      Map-literal nuance (carry-forward KB cache hit per FR-012 --
      REUSE from arithmetic_test.dart.md / partial_evaluator_test.
      dart.md). Dart `{<K>: <V>, ...}` is a literal `Map<K,V>`. C#
      offers two idiomatic forms: collection-initialiser
      `new Dictionary<K,V> { {k, v}, ... }` and index-initialiser
      `new Dictionary<K,V> { [k] = v, ... }`. Spec default emits
      the index-initialiser form because the source's key (`0`) is
      an integer literal (cleaner). Inline-comment preservation:
      the Dart `// Pass writer so callee can write via :=` comment
      translates verbatim to C#. Mutability: both Dart map literals
      and C# `Dictionary<K,V>` are mutable -- semantic match.
  - construct_key: dart.method_call.runtime_set_goal_env
    source_form: "rt.setGoalEnv(goalId, env);"
    target_decision: >-
      `rt.SetGoalEnv(goalId, env);` -- PascalCase rename per
      runtime.dart.md. Returns `void`.
    idiom_id: rf-dart-camel-to-csharp-pascal-method-rename
    research_finding_id: rf-dart-camel-to-csharp-pascal-method-rename
    nuance: >-
      Carry-forward KB cache hit per FR-012 -- REUSE. Single use
      in the "Z := 5 + 3 ..." test.
  - construct_key: dart.const_local.typed_int_literal
    source_form: "final goalId = 1;"
    target_decision: >-
      `var goalId = 1;` (C# `var` with `int` literal). The Dart
      `final` is reassign-prohibition; C# `var` is semantic
      equivalent at method-local scope. The `goalId` flows into
      `rt.SetGoalEnv(goalId, env)` and `rt.Gq.Enqueue(new GoalRef(
      goalId, entryPc!))`. SUT records `GoalId` as a typedef-`int`
      per machine_state.dart.md.
    idiom_id: rf-dart-final-local-to-csharp-var
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Carry-forward KB cache hit per FR-012 -- REUSE from
      arithmetic_test.dart.md. The literal `1` fits both `int` and
      `long`; spec emits the typedef-alias name (`GoalId`) per
      machine_state.dart.md's `rf-dart-typedef-int-to-csharp-global-
      using-alias` idiom.
  - construct_key: dart.string_interpolation.simple_expression
    source_form: >-
      "print('Stdlib compiled: ${stdlibProg.ops.length} instructions');
       print('Merged program has ${mergedProg.ops.length} instructions');
       print('Labels: ${mergedProg.labels.keys.toList()}');
       print('compute_sum/1 compiled to ${prog.ops.length} instructions');
       print('\n=== END-TO-END ARITHMETIC TEST (Pointer Architecture) ===');
       print('Merged program: ${mergedProg.ops.length} instructions');
       print('Allocated result variable: writer=$resultWriter, reader=$resultReader');
       print('compute_sum/1 entry at PC \$entryPc');
       print('\nRunning scheduler to drain all goals...');
       print('Goals executed: ${ran.length}');
       print('Result variable bound: \$isBound');
       print('Result value: \$value');
       print('Result = \${value.value}');
       print('✓ Z := 5 + 3 correctly evaluates to 8!');"
    target_decision: >-
      Translate Dart interpolated strings `'...${expr}...'` to C#
      interpolated `$"...{expr}..."`. The 14 `print(...)` callsites
      in this file all route through `_output.WriteLine($"...{expr}
      ...")` -- the test classes' ctors take `(StdlibProgFixture
      fixture, ITestOutputHelper output)` and store
      `private readonly ITestOutputHelper _output;`. Property-name
      PascalCasing applies inside interpolation: `stdlibProg.ops.
      length` -> `_fixture.StdlibProg.Ops.Count` (or `.Length` if
      the SUT records `Ops` as `T[]`); `mergedProg.labels.keys.
      toList()` -> `mergedProg.Labels.Keys.ToList()` (requires
      `using System.Linq;` for the `ToList()` extension method or
      `new List<...>(mergedProg.Labels.Keys)` constructor form);
      `ran.length` -> `ran.Count`; `value.value` (within an
      `Assert.IsType<ConstTerm>` block) collapses to
      `constValue.Value` per the cached compound idiom. Simple `$var`
      interpolation: `$resultWriter` -> `{resultWriter}`;
      `$entryPc` -> `{entryPc}`; `$isBound` -> `{isBound}`;
      `$value` -> `{value}`; `${value.value}` -> `{value.value}` or
      `{constValue.Value}` per the typed-IsType collapse. The `\n`
      escape is processed identically by both string-literal
      parsers; the UTF-8 checkmark glyph `✓` survives unchanged.
    idiom_id: rf-dart-string-interpolation-to-csharp-dollar-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-dollar-string
    nuance: >-
      Interpolation + diagnostic-output compound nuance (carry-
      forward KB cache hit per FR-012 -- REUSE from
      arithmetic_test.dart.md verbatim, same `print`-heavy pattern).
      `print` -> `ITestOutputHelper.WriteLine` (xunit.net
      `https://xunit.net/docs/capturing-output` -- "xUnit.net
      captures output via the `ITestOutputHelper` interface
      injected into the constructor"); `Console.WriteLine` is an
      INFERIOR fallback (xUnit does NOT capture Console.Out).
      Identifier renaming for inner `keys.toList()`: Dart `keys` is
      an iterable view returned by `Map.keys` -> C# `Dictionary.
      Keys` (a `KeyCollection`, also an `IEnumerable<TKey>`);
      `toList()` -> `ToList()` (System.Linq) OR `new List<>(...)`.
      Spec default: emit `ToList()` (LINQ; ubiquitous). UTF-8 glyph
      `✓` is a legal C# string-literal content (both Dart and C#
      accept literal Unicode code points). `\n` escape: identical
      processing both sides.
  - construct_key: dart.package_test.fail_call
    source_form: "fail('Result variable should be bound after execution');"
    target_decision: >-
      Translate the Dart top-level `fail(<msg>)` (from `package:
      test`) to `Assert.Fail(<msg>);` -- xUnit `Assert.Fail(string)`
      (xUnit.net v2.4.2+). The single call here, in the `else`
      branch of `if (isBound) { ... } else { fail('...'); }`,
      becomes the `else`-branch of the converted `if (isBound) { ...
      } else { Assert.Fail("Result variable should be bound after
      execution"); }`. `Assert.Fail` throws `XunitException`,
      matching Dart's `TestFailure` throw.
    idiom_id: rf-dart-fail-call-to-xunit-assert-fail
    research_finding_id: rf-dart-fail-call-to-xunit-assert-fail
    nuance: >-
      Unconditional-fail nuance (carry-forward KB cache hit per
      FR-012 -- REUSE from arithmetic_test.dart.md verbatim). Both
      `fail(...)` (Dart) and `Assert.Fail(...)` (xUnit) are
      immediate test-failure signals with a user-supplied message;
      both propagate up to the test reporter. Control-flow:
      `Assert.Fail` throws so any code after is unreachable
      (identical to Dart's `TestFailure` throw). Reachability:
      `Assert.Fail` is NOT marked `[DoesNotReturn]` in stock xUnit
      v2.4.2, but the `else`-branch pattern is the only structural
      fit here.
  - construct_key: dart.expression.if_else_with_is_check
    source_form: |-
      "if (isBound) {
         final value = rt.heap.getValue(resultWriter);
         print('Result value: $value');
         expect(value, isA<ConstTerm>());
         if (value is ConstTerm) {
           print('Result = ${value.value}');
           expect(value.value, equals(8), reason: '5 + 3 should equal 8');
           print('✓ Z := 5 + 3 correctly evaluates to 8!');
         }
       } else {
         fail('Result variable should be bound after execution');
       }"
    target_decision: >-
      Translate the if-else with nested `is`-check + `(value as
      ConstTerm).value` follow-up to C# `if (isBound) { ... } else
      { Assert.Fail(...); }` with the nested `if (value is ConstTerm
      constValue) { ... }` using C# 7+ TYPE PATTERN syntax. The
      Dart `value is ConstTerm` `is`-check + flow-typing of `value`
      to `ConstTerm` inside the `if`-body maps to C# `is T name`
      pattern (Microsoft Learn `https://learn.microsoft.com/dotnet/
      csharp/language-reference/operators/is`) which combines the
      type test and the typed-local binding in one step:
      `if (value is ConstTerm constValue) { ... constValue.Value ...
      }`. The outer `expect(value, isA<ConstTerm>())` becomes
      `Assert.IsType<ConstTerm>(value)` (or could be merged into
      the `is`-pattern check above -- spec preference: keep them
      separate to match the Dart structure literally). The inner
      `expect(value.value, equals(8), reason: '5 + 3 should equal
      8')` -> `Assert.Equal(8, constValue.Value); // 5 + 3 should
      equal 8` -- `Assert.Equal<T>` has NO user-message overload
      (xunit.net Assert API), so `reason:` routes to inline comment
      (carry-forward from arithmetic_test.dart.md / bytecode
      siblings).
    idiom_id: null
    research_finding_id: rf-dart-is-flow-typing-to-csharp-is-pattern
    nuance: >-
      Flow-typing + is-pattern nuance (FIRST-SEEN in heap-test-
      family -- carry-forward from broader corpus likely; promoted
      to active idiom). Dart `value is ConstTerm` inside an `if`
      condition NARROWS `value`'s static type to `ConstTerm` inside
      the if-body (Dart language tour `https://dart.dev/language/
      pattern-types#type-pattern` -- "When the type check
      succeeds, the variable is promoted to the matched type
      inside the block"). C# 7+ `is T name` pattern combines the
      type test and the local-binding in one syntactic position
      (Microsoft Learn `https://learn.microsoft.com/dotnet/csharp/
      language-reference/operators/is` -- "Type pattern: tests
      whether the runtime type of an expression is compatible
      with a given type, and if so, binds the result to a new
      variable"). Authoritative both sides; no escalation.
      Argument-order + reason-comment nuance: `expect(actual,
      equals(expected), reason: msg)` -> `Assert.Equal(expected,
      actual); // msg` (REUSE the cached `rf-dart-expect-equals-
      to-xunit-assertequal` and the cached `reason:`-to-comment
      idiom from arithmetic_test.dart.md).
  - construct_key: dart.user_source.glp_program_triple_quoted_string
    source_form: |-
      "final userSource = '''
         hello.
       ''';

       final userSource = '''
         compute_sum(Z?) :- Z := 5 + 3.
       ''';

       final userSource = '''
         compute_sum(Z?) :- Z := 5 + 3.
       ''';"
    target_decision: >-
      Translate the Dart triple-single-quoted multi-line string
      literal `'''...'''` to C# verbatim string `@"..."` (or C# 11+
      raw string literal `"""..."""`). Spec preference: C# 11+ raw
      string literal `"""<text>"""` (preserves whitespace, no
      escape processing, requires C# 11 / .NET 7+ language version
      -- Microsoft Learn `https://learn.microsoft.com/dotnet/
      csharp/language-reference/tokens/raw-string`). Fallback: C#
      verbatim string `@"<text>"` (works on all C# versions but
      requires escaping internal `"` as `""` -- not needed here).
      Three such literals in this file (the GLP source for `hello.`
      and twice for `compute_sum(Z?) :- Z := 5 + 3.`). The `:=`
      glyph and the `?` glyph are legal string-literal content.
    idiom_id: null
    research_finding_id: rf-dart-triple-string-to-csharp-raw-string
    nuance: >-
      Multi-line string nuance (FIRST-SEEN in heap-test-family --
      promoted to active idiom). Dart triple-quoted strings
      `'''<text>'''` preserve newlines and whitespace literally
      (Dart language tour `https://dart.dev/language/built-in-
      types#strings` -- "Multiline strings"). C# offers TWO
      analogues: (a) verbatim string `@"<text>"` (preserves
      newlines; `""` escapes a quote; supported since C# 1.0); (b)
      raw string literal `"""<text>"""` (no escape processing;
      whitespace-aware closing-delimiter alignment; supported
      since C# 11 / .NET 7). Both preserve the `:=`/`?` glyphs
      and the indentation literally. Spec preference: raw string
      literal `"""..."""` (cleaner; no escape footguns); fallback
      to verbatim `@"..."` if project targets pre-C#11. Whitespace
      nuance: Dart triple-quoted strings include the leading
      whitespace on each line; C# raw strings ALSO preserve
      leading whitespace, but the C# 11 raw string OPTIONALLY
      strips uniform leading whitespace aligned with the closing
      delimiter (Microsoft Learn raw-string reference). For
      `GlpCompiler.Compile`'s purposes the leading whitespace is
      irrelevant -- GLP source is whitespace-insensitive
      (statement terminators are periods). Authoritative both
      sides; no escalation. NEW idiom registered (active).
  - construct_key: dart.method_call.glp_compiler_compile_with_string
    source_form: >-
      "final stdlibProg = stdlibCompiler.compile(stdlibSource);
       final userProg = userCompiler.compile(userSource);"
    target_decision: >-
      `var stdlibProg = stdlibCompiler.Compile(stdlibSource);`
      `var userProg = userCompiler.Compile(userSource);` -- PascalCase
      rename (`compile` -> `Compile`) per lib/compiler/compiler.
      dart.md. Returns `BytecodeProgram`. Reference type both sides.
    idiom_id: rf-dart-camel-to-csharp-pascal-method-rename
    research_finding_id: rf-dart-camel-to-csharp-pascal-method-rename
    nuance: >-
      Carry-forward KB cache hit per FR-012 -- REUSE from
      arithmetic_test.dart.md verbatim. The `Compile` method's
      signature is `(string source) -> BytecodeProgram` per
      compiler.dart.md.
  - construct_key: dart.method_call.userprog_merge_stdlibprog
    source_form: "final mergedProg = userProg.merge(stdlibProg);"
    target_decision: >-
      `var mergedProg = userProg.Merge(stdlibProg);` -- PascalCase
      rename (`merge` -> `Merge`) per lib/bytecode/runner.dart.md.
      `BytecodeProgram.Merge(other)` returns a new `BytecodeProgram`
      (immutable merge -- new merged copy, original unchanged).
    idiom_id: rf-dart-camel-to-csharp-pascal-method-rename
    research_finding_id: rf-dart-camel-to-csharp-pascal-method-rename
    nuance: >-
      Carry-forward KB cache hit per FR-012 -- REUSE from
      arithmetic_test.dart.md. The merge is in-place-vs-immutable
      decision pinned in runner.dart.md (immutable: returns new
      `BytecodeProgram`); the test's `mergedProg` local captures
      the new instance and `stdlibProg`/`userProg` survive
      unchanged.
conversion_units:
  - "namespace <RootNs>.Test.Heap; (file-scoped namespace mirroring the test/heap directory)"
  - "using Xunit; (file-level)"
  - "using Xunit.Abstractions; (file-level; for ITestOutputHelper)"
  - "using System.IO; (file-level; for File.ReadAllText + Path.Combine)"
  - "using System.Collections.Generic; (file-level; for Dictionary<int, VarRef>)"
  - "using System.Linq; (file-level; for .ToList() on Dictionary.Keys)"
  - "using <RootNs>.Compiler; (collapsed from compiler.dart import)"
  - "using <RootNs>.Bytecode; (collapsed from bytecode/runner.dart import)"
  - "using <RootNs>.Runtime; (collapsed from five runtime/*.dart imports)"
  - "public class StdlibProgFixture { public BytecodeProgram StdlibProg { get; } public StdlibProgFixture() { ... } } (class fixture; ctor compiles ../programs/self.glp via File.ReadAllText(Path.Combine(AppContext.BaseDirectory, \"..\", \"programs\", \"self.glp\")) + new GlpCompiler().Compile(...); assigns the get-only auto-property)"
  - "[CollectionDefinition(\"ArithmeticPreludePointer\")] public class ArithmeticPreludePointerCollection : ICollectionFixture<StdlibProgFixture> { } (xUnit collection-fixture marker)"
  - "public class ArithmeticPointerTest { ... } (outer file-level test container; doc-comment lifted from the Dart file lead; mirrors arithmetic_pointer_test.dart -> ArithmeticPointerTest.cs)"
  - "[Collection(\"ArithmeticPreludePointer\")] public class ArithmeticViaAssignSystemPredicatePointerArchitecture { private readonly StdlibProgFixture _fixture; private readonly ITestOutputHelper _output; public ArithmeticViaAssignSystemPredicatePointerArchitecture(StdlibProgFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; } ... } (nested test class for the first group; carries [Trait(\"Group\", \"Arithmetic via := system predicate - Pointer Architecture\")])"
  - "[Fact(DisplayName = \"add/3 body kernel executes directly\")] public void Add3BodyKernelExecutesDirectly() { ... } (body: var rt = new GlpRuntime(); var (xWriter, xReader) = rt.Heap.AllocateVariable(); var (yWriter, yReader) = rt.Heap.AllocateVariable(); var (resultWriter, resultReader) = rt.Heap.AllocateVariable(); rt.Heap.BindWriter(xWriter, new ConstTerm(5)); rt.Heap.BindWriter(yWriter, new ConstTerm(3)); var xRef = new VarRef(xReader); var yRef = new VarRef(yReader); var resultRef = new VarRef(resultWriter); var kernel = rt.BodyKernels.Lookup(\"_add\", 3); Assert.NotNull(kernel); // _add/3 kernel should be registered  var result = kernel!(rt, new[] { xRef, yRef, resultRef }); Assert.Equal(BodyKernelResult.Success, result); var value = rt.Heap.GetValue(resultWriter); Assert.NotNull(value); var constValue = Assert.IsType<ConstTerm>(value); Assert.Equal(8, constValue.Value);)"
  - "[Fact(DisplayName = \"sub/3 body kernel\")] public void Sub3BodyKernel() { ... } (operands 10/4, expected 6; per-test heap; (resultWriter, _) discard)"
  - "[Fact(DisplayName = \"mul/3 body kernel\")] public void Mul3BodyKernel() { ... } (operands 7/6, expected 42; (resultWriter, _) discard)"
  - "[Fact(DisplayName = \"div/3 body kernel\")] public void Div3BodyKernel() { ... } (operands 15/4, expected 3.75; (resultWriter, _) discard; consider precision overload for double)"
  - "[Fact(DisplayName = \"div/3 body kernel aborts on division by zero\")] public void Div3BodyKernelAbortsOnDivisionByZero() { ... } (operands 10/0, expected BodyKernelResult.Abort; NO post-result value check)"
  - "[Fact(DisplayName = \"neg/2 body kernel\")] public void Neg2BodyKernel() { ... } (unary; operand 42, expected -42; only 2 allocateVariable calls -- no Y)"
  - "[Fact(DisplayName = \"sqrt_kernel/2 body kernel\")] public void SqrtKernel2BodyKernel() { ... } (unary; operand 16, expected 4.0)"
  - "[Fact(DisplayName = \"all standard body kernels are registered\")] public void AllStandardBodyKernelsAreRegistered() { ... } (24 Assert.True(rt.BodyKernels.Has(\"_<name>\", <arity>)) calls verbatim)"
  - "[Collection(\"ArithmeticPreludePointer\")] public class EndToEndAssignSystemPredicatePointerArchitecture { ... ctor with fixture + output ... } (nested test class for the second group; carries [Trait(\"Group\", \"End-to-end := system predicate - Pointer Architecture\")])"
  - "[Fact(DisplayName = \"assign.glp compiles and merges correctly\")] public void AssignGlpCompilesAndMergesCorrectly() { ... } (re-reads prelude per-test; compiles \"hello.\" user source; Asserts on labels.ContainsKey(\":=/2\") + labels.ContainsKey(\"hello/0\") with user-messages)"
  - "[Fact(DisplayName = \"user program with := compiles correctly with SRSW\")] public void UserProgramWithAssignCompilesCorrectlyWithSRSW() { ... } (compiles compute_sum(Z?) :- Z := 5 + 3.; Assert.True(prog.Ops.Count > 0); Assert.True(prog.Labels.ContainsKey(\"compute_sum/1\")))"
  - "[Fact(DisplayName = \"Z := 5 + 3 executes and binds Z to 8\")] public void ZAssign5Plus3ExecutesAndBindsZTo8() { ... } (full end-to-end; var rt = new GlpRuntime(); var (resultWriter, resultReader) = rt.Heap.AllocateVariable(); creates BytecodeRunner + Scheduler; CallEnv with new Dictionary<int, VarRef> { [0] = new VarRef(resultWriter) }; SetGoalEnv; Gq.Enqueue(new GoalRef(goalId, entryPc!)); sched.Drain(maxCycles: 100, debug: true, debugOutput: true); var isBound = rt.Heap.IsFullyBound(resultWriter); if (isBound) { ... is-pattern + Assert.Equal(8, constValue.Value) ... } else { Assert.Fail(\"Result variable should be bound after execution\"); })"
  - "[Collection(\"ArithmeticPreludePointer\")] public class VariableChainDereferencing { ... ctor with fixture + output ... } (nested test class for the third group; carries [Trait(\"Group\", \"Variable Chain Dereferencing\")])"
  - "[Fact(DisplayName = \"arithmetic through variable chain\")] public void ArithmeticThroughVariableChain() { ... } (X -> Y -> 5 chain via BindWriterToReader(xWriter, yReader); BindWriter(zWriter, new ConstTerm(3)); _add kernel invoke; Assert.Equal(8, constValue.Value))"
  - "all print(...) callsites routed through _output.WriteLine($\"...\"); the setUpAll print omitted in fixture ctor (canonical-silent)"
  - "NO equivalent of Dart's void main() -- xUnit discovery is attribute-driven; the file-level setUpAll + late variable lift entirely into the ICollectionFixture<StdlibProgFixture> + [Collection] mechanism"
  - "NO equivalent of Dart's library; directive -- file-scoped namespace replaces it"
escalations: []
```

## Rationale + research provenance (per non-trivial construct)

This artifact converts a sibling adaptation of the bytecode-family
`test/bytecode/arithmetic_test.dart.md` to the heap-pointer-
architecture surface. Every non-trivial construct REUSES a prior
sibling idiom; only TWO first-seen idioms appear (the is-pattern
flow-typing and the triple-quoted string literal). Authoritative
basis carried forward from the precedent specs and re-cited only
where load-bearing.

### Why xUnit (FR-024 official-docs authoritative, KB cache hit)

`rf-dart-package-test-import-to-xunit-using` -- pinned project-wide
(boot_loader_test.dart.md / smoke_test.dart.md / arithmetic_test.
dart.md / every heap-test-family sibling). xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / `[Trait]` / `DisplayName` / `Assert.*`; Dart
`package:test` docs (`https://pub.dev/packages/test`) for `group` /
`test` / `expect` / matcher semantics. No re-research.

### Three-group + setUpAll lift to Collection Fixture (carry-forward)

Identical shape to arithmetic_test.dart.md (two groups + setUpAll
-> two `[Collection]`-tagged nested classes + `ICollectionFixture<
StdlibProgFixture>` + `[CollectionDefinition]`). This file extends
the precedent to THREE nested classes (the third group "Variable
Chain Dereferencing" has only 1 test). xunit.net Shared Context
reference is the authoritative basis (cached); `Path.Combine(
AppContext.BaseDirectory, ...)` routing for the relative path is
the load-bearing fidelity preserver (carry-forward from
module_hierarchy_test.dart.md). The doc-comment lift to the outer
container class preserves the heap-pointer-architecture spec v3.0
citation -- a precondition for understanding `bindWriter`/
`bindWriterToReader` vs the legacy `bindVariableConst`.

### Pointer-architecture cutover (load-bearing semantic note)

Per the file lead doc-comment: "Adapted from: test/bytecode/
arithmetic_test.dart ... Tests that arithmetic operations work
correctly with the new pointer-based heap architecture". The
DELTA from the bytecode-sibling arithmetic_test.dart.md is:
- The bytecode-sibling used `heap.bindVariableConst(<writer-addr>,
  <int>)` (legacy single-cell-per-variable architecture);
- THIS file uses `heap.allocateVariable() -> (writer, reader)`
  returning a writer/reader cell PAIR, then `heap.bindWriter(<wr>,
  ConstTerm(<n>))` (pointer-architecture two-cell model).
- THIS file adds the "Variable Chain Dereferencing" group (1
  test) that exercises `BindWriterToReader` -- a heap-pointer-
  architecture-specific method (didn't exist in the legacy model).
All SUT-side method names (`AllocateVariable`, `BindWriter`,
`BindWriterToReader`, `DerefAddr`, `IsFullyBound`, `GetValue`) are
owned by heap_fcp.dart.md; this test artifact REFERENCES those
decisions but does not duplicate them.

### Inherited heap_fcp threading-model escalation (DEFERRED, no new escalation here)

`lib/runtime/heap_fcp.dart.md` is currently `escalated` on the
HeapFCP threading model (single-owning-context vs ConcurrentDict
ionary/Interlocked) -- Gabi has not yet ruled. THIS file does
NOT exercise threading semantics: every test allocates its OWN
`var rt = new GlpRuntime()` and performs only SYNCHRONOUS calls;
no `await`, no `Isolate.spawn`, no cross-thread aliasing of the
heap state. The dependency is recorded in the
`heap_query_get_value_returning_nullable_term` construct's nuance
field. When the heap_fcp ruling lands, the test's specced call-
site shapes remain valid; the only potential change is internal
to heap_fcp.dart.md (e.g. whether `GetValue` is `[ThreadStatic]`-
scoped or lock-free), invisible at the test-spec level. No new
escalation introduced here -- the inherited dependency is the
correct discipline (escalations propagate only when the depending
file exercises the unresolved surface, which this one does not).

### Argument-order swap + Assert.IsType compound (carry-forward)

`Assert.IsType<T>(actual)` returns the typed value (xunit.net
Assert API); Dart's three-step `isNotNull` + `isA<T>` + downcast +
field-equals collapses to two-step `Assert.IsType<T>` + `Assert.
Equal`. The argument-order swap (`Assert.Equal(expected, actual)`)
is the load-bearing footgun pinned in smoke_test.dart.md;
codegen ALWAYS flips. Floating-point literals `3.75` (= 15/4,
exact in IEEE-754) and `4.0` (= sqrt(16), exact): spec emits
strict equality; precision overload `Assert.Equal(double, double,
int)` is the recommended floating-point-safe form when the SUT
records the payload as `double` (vs `object?` boxed).

### `is`-pattern flow-typing (FIRST-SEEN in heap-test-family)

Dart `value is ConstTerm` inside an `if` narrows `value` to
`ConstTerm` (Dart language tour `https://dart.dev/language/
pattern-types#type-pattern`). C# 7+ `is T name` combines the type
test with the typed-local binding (Microsoft Learn
`https://learn.microsoft.com/dotnet/csharp/language-reference/
operators/is`). Both are authoritative; the C# shape
`if (value is ConstTerm constValue) { ... constValue.Value ... }`
is the canonical one-step equivalent. NEW idiom registered
(active) so future tests that use the same flow-typing pattern
reuse via the KB.

### Triple-quoted Dart string -> C# raw string (FIRST-SEEN)

Dart `'''<text>'''` -> C# 11+ raw string literal `"""<text>"""`
(Microsoft Learn `https://learn.microsoft.com/dotnet/csharp/
language-reference/tokens/raw-string`). C# verbatim string
`@"<text>"` is the pre-C#11 fallback. Both preserve the `:=`/`?`
glyphs and newlines literally. Three triple-quoted GLP source
literals in this file; the GLP source is whitespace-insensitive so
indentation does not affect semantics. NEW idiom registered
(active) for any future test convspec containing inline GLP/Dart
multi-line source.

### Zero escalations

Every construct has a clear single-decision target shape grounded
in official Dart and .NET/C# documentation. Eleven idioms are KB
cache-hit reuses (FR-012 / SC-007 -- no re-research); TWO are
first-seen (the `is`-pattern flow-typing and the triple-quoted
string mapping). The inherited heap_fcp threading-model escalation
is recorded in the `GetValue`/`IsFullyBound` construct nuance but
does NOT propagate (this file does not exercise threading).
`escalations: []` is therefore intentional and disciplined.

## Notes

- No `async` / `Future` / `Stream` / `Completer` / `Timer` /
  isolate surface in this file -- every `[Fact]` is sync `public
  void` (not `async Task`); `setUpAll` is sync (-> sync fixture
  ctor). The Stream-vs-IAsyncEnumerable nuance is well-known but
  does not apply (US2 AS4 -- recorded as not applicable).
- No `mixin`, `extension`, generics-with-bounds, sealed/abstract
  declarations (the SUT types ARE sealed per terms.dart.md /
  heap_fcp.dart.md, but this test file declares no types of its
  own).
- Null-safety surface fires at three sites: the three
  `expect(..., isNotNull, ...)` matchers (mapped to `Assert.NotNull
  ` + inline-comment `reason:`); the `kernel!` non-null assertion
  in each of the seven kernel-invocation tests (mapped to C#
  `kernel!` null-forgiving after a preceding `Assert.NotNull(
  kernel)`); the `entryPc!` non-null assertion (mapped after a
  preceding `Assert.True(Labels.ContainsKey(...))` gate). All
  three sites compose the cached
  `rf-dart-bang-null-assertion-to-csharp-null-forgiving` idiom
  with a preceding xUnit assertion to preserve Dart's runtime-
  throw semantic.
- Reference-vs-value: every type referenced (`GlpRuntime`,
  `GlpCompiler`, `BytecodeRunner`, `Scheduler`, `BytecodeProgram`,
  `CallEnv`, `VarRef`, `ConstTerm`, `Pointer`, `HeapFCP`) is a
  reference type on both sides per the SUT specs; `GoalRef` is
  `readonly record struct` (value-typed) per machine_state.dart.md;
  the `int`-typed addresses and `BodyKernelResult` enum are value
  types in both languages. No struct-vs-class promotion/demotion.
- Heap-pointer-architecture invariant (load-bearing, file-level):
  every test allocates its OWN `var rt = new GlpRuntime()` --
  there is NO shared `Heap` across tests. The Collection Fixture
  shares only the IMMUTABLE compiled `BytecodeProgram` (the
  stdlib). This isolates per-test heap state, eliminates cross-
  test aliasing, and orthogonalises the test conversion from
  the heap_fcp threading-model decision (recorded above).
- The bytecode-sibling arithmetic_test.dart.md's recorded
  identifier-mangling rules apply verbatim: `:=` -> `Assign`,
  `+` -> `Plus`, slashes/periods/spaces dropped, underscores
  PascalCase-joined, digits preserved (no leading-digit guard
  fires for the 12 test names here).
