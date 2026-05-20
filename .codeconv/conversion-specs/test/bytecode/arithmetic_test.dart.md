> Conversion-spec artifact for test/bytecode/arithmetic_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart -> C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/bytecode/arithmetic_test.dart
source_sha256: 6c536bfb10977451326c73eaa01a2b0537043da88cfc65d6f3c36fe05b39c11a
target_code_unit: test/bytecode/ArithmeticTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and emit
      `using Xunit;` at file scope. REUSE the batch-wide framework-choice
      idiom recorded in the sibling specs `.codeconv/conversion-specs/
      test/smoke_test.dart.md`, `.codeconv/conversion-specs/test/
      glp_runtime_test.dart.md`, and the bytecode siblings
      `.codeconv/conversion-specs/test/bytecode/
      fairness_scheduler_loop_test.dart.md` /
      `.codeconv/conversion-specs/test/bytecode/
      utility_instructions_test.dart.md`. Per FR-012 / SC-007 this
      construct is NOT re-researched here; the
      `rf-dart-package-test-to-dotnet-xunit` finding carries forward
      verbatim. The .NET test project's `.csproj` (referencing `xunit`,
      `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) is OUT OF
      SCOPE for this per-file artifact -- langpair-level concern.
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice reuse (explicitly addressed, no re-derivation):
      the framework decision (xUnit vs MSTest vs NUnit) was settled in
      smoke_test.dart and reused throughout the batch. The Dart
      top-level `test()` -> `[Fact]` instance method shape and the Dart
      `group()` -> enclosing test-class lift both carry forward
      verbatim from the bytecode siblings. The async-vs-sync
      `[Fact]`-shape nuance (sync `void` `[Fact]` vs `async Task`) is
      decided per-test below: this file has NO `async`/`Future`/
      `Stream` surface, so every `[Fact]` is `public void`.
  - construct_key: dart.internal_package_import.same_package
    source_form: >-
      "import 'package:glp_runtime/compiler/compiler.dart';
       import 'package:glp_runtime/bytecode/runner.dart';
       import 'package:glp_runtime/runtime/runtime.dart';
       import 'package:glp_runtime/runtime/machine_state.dart';
       import 'package:glp_runtime/runtime/terms.dart';
       import 'package:glp_runtime/runtime/body_kernels.dart';
       import 'package:glp_runtime/runtime/scheduler.dart';"
    target_decision: >-
      Drop all seven Dart `import 'package:glp_runtime/...';` directives
      and collapse them into THREE C# `using` directives: `using
      <RootNs>.Compiler;` (for `compiler/compiler.dart`),
      `using <RootNs>.Bytecode;` (for `bytecode/runner.dart`), and
      `using <RootNs>.Runtime;` (for `runtime/runtime.dart`,
      `machine_state.dart`, `terms.dart`, `body_kernels.dart`,
      `scheduler.dart`). Target sub-namespaces come from the SUT specs
      `.codeconv/conversion-specs/lib/compiler/compiler.dart.md`,
      `.codeconv/conversion-specs/lib/bytecode/runner.dart.md`, and the
      five `lib/runtime/*.dart.md` SUT specs. This brings into scope:
      `GlpCompiler` (Compiler); `BytecodeProgram`, `BytecodeRunner`
      (Bytecode); `GlpRuntime`, `BodyKernelResult`, `VarRef`,
      `ConstTerm`, `CallEnv`, `GoalRef`, `Scheduler` (Runtime). The
      test assembly's `.csproj` must reference the converted-SUT
      assembly -- project-system wiring is OUT OF SCOPE.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed, reused from
      bytecode/fairness_scheduler_loop_test.dart.md): in Dart each
      `package:` URI is a separate import; in C# all sub-paths under
      the same converted namespace collapse to ONE `using` (C# `using`
      is per-namespace, not per-file -- Microsoft Learn
      `using-directive` reference). Seven Dart imports collapse to
      three C# `using` directives because the Dart files span three
      target sub-namespaces (Compiler, Bytecode, Runtime). No `using
      static` needed -- the test body names types only. Visibility:
      every imported identifier is library-public on the Dart side
      (no leading underscore) -> `public` on the C# side per SUT
      specs. No cross-isolate or transitive-export semantics apply.
  - construct_key: dart.import_directive.dart_io_to_csharp_using_system_io
    source_form: "import 'dart:io';"
    target_decision: >-
      Emit `using System.IO;` at the top of the target file (covers
      `File`, `Path`). Cached idiom -- reused verbatim from the
      sibling test/module/module_hierarchy_test.dart.md and the lib
      siblings lib/runtime/module_hierarchy.dart.md /
      lib/runtime/external_io.dart.md
      (`rf-dart-dart-io-to-csharp-system-io`). This test exercises a
      narrow `dart:io` surface (`File('<path>').readAsStringSync()`
      only -- no `Directory`, no async I/O, no temp dirs); the cached
      finding subsumes it.
    idiom_id: rf-dart-dart-io-to-csharp-system-io
    research_finding_id: rf-dart-dart-io-to-csharp-system-io
    nuance: >-
      I/O surface nuance (explicitly addressed): the only `dart:io`
      use here is `File('../programs/self.glp').readAsStringSync()`,
      called three times inside `setUpAll` (once) and inside two
      `[Fact]` bodies of the `End-to-end := system predicate` group.
      Dart `File('<path>')` is a path wrapper (no I/O at ctor time);
      `readAsStringSync()` reads the file synchronously and returns
      `String` (Dart API docs `api.dart.dev/stable/dart-io/File-
      class.html`). The C# counterpart is the STATIC
      `File.ReadAllText(<path>)` (Microsoft Learn
      `learn.microsoft.com/en-us/dotnet/api/system.io.file.readalltext`
      -- "Opens a text file, reads all the text in the file, and then
      closes the file"). Both are synchronous + UTF-8-by-default
      (Dart `readAsStringSync` defaults to UTF-8 per `Encoding utf8`;
      .NET `File.ReadAllText(string)` overload defaults to UTF-8 with
      BOM detection). No instance-method routing needed: emit
      `File.ReadAllText("../programs/self.glp")` directly (no `new
      File(...)` ctor wrapper). Relative-path nuance (LOAD-BEARING,
      carry-forward from module_hierarchy_test): Dart resolves the
      relative path against the CWD (which the project's test runner
      sets to `glp_runtime/`); C# resolves against the process CWD
      (which `dotnet test` does NOT necessarily set to the test
      project root). The faithful conversion routes the relative path
      through `AppContext.BaseDirectory` -- emit `Path.Combine(
      AppContext.BaseDirectory, "..", "programs", "self.glp")` (or
      `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
      "../programs/self.glp"))`) so the prelude file resolves
      identically to the Dart test. This relative-path indirection is
      the same mechanism recorded in
      module_hierarchy_test.dart.md (cached idiom row).
  - construct_key: dart.test_file.void_main_as_test_registration_root
    source_form: |-
      "void main() {
         late BytecodeProgram stdlibProg;
         setUpAll(() { ... });
         group('Arithmetic via := system predicate', () { ... });
         group('End-to-end := system predicate', () { ... });
       }"
    target_decision: >-
      Eliminate Dart's `void main()` and lift the file's TWO `group()`
      blocks into TWO public sibling NESTED test classes inside an
      outer `public class ArithmeticTest` (file name
      `arithmetic_test.dart` -> `ArithmeticTest.cs`). The outer
      class is the file-level container; the two nested classes
      mirror the two `group(...)` labels per the
      `rf-dart-package-test-group-to-xunit-class` idiom (REUSED from
      mad_transactions_test.dart.md, globalize_test.dart.md,
      module_hierarchy_test.dart.md). PRECISE LIFT: (a) Dart
      `group('Arithmetic via := system predicate', ...)` -> `public
      class ArithmeticViaAssignSystemPredicate` (PascalCased; `:=`
      glyph -- not a C# identifier character per Microsoft Learn
      `learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-
      style/identifier-names` -- spelled out as `AssignSystemPredicate`
      to remain identifier-legal; preserve the original label
      verbatim via `[Trait("Group", "Arithmetic via := system
      predicate")]`); (b) Dart `group('End-to-end := system
      predicate', ...)` -> `public class
      EndToEndAssignSystemPredicate` with `[Trait("Group",
      "End-to-end := system predicate")]`. The outer class
      `ArithmeticTest` carries the SHARED `setUpAll` fixture via
      xUnit's IClassFixture mechanism (next construct). The two
      `[Fact]` groups become NESTED classes that share the fixture
      via `IClassFixture<StdlibProgFixture>` -- xunit.net "Shared
      Context between Tests" `https://xunit.net/docs/shared-context`.
    idiom_id: rf-dart-test-main-to-xunit-class-with-facts
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Lifecycle + group nuance (load-bearing, explicitly addressed):
      Dart `main()` here does TWO things -- (1) declare a `late
      BytecodeProgram stdlibProg;` file-scoped variable, (2) call
      `setUpAll(() { stdlibProg = stdlibCompiler.compile(
      File('../programs/self.glp').readAsStringSync()); })` to
      populate it once per process, then (3) register two sibling
      `group(...)` calls. xUnit has NO per-file hook (xunit.net
      `https://xunit.net/docs/shared-context` -- "xUnit.net offers
      several methods for sharing this setup and cleanup code,
      depending on the scope of things to be shared, as well as the
      expense associated with the setup and cleanup code:
      Constructor and Dispose (shared setup/cleanup code WITHOUT
      sharing object instances); Class Fixtures (shared object
      instance across tests in a single class); Collection Fixtures
      (shared object instances across multiple test classes)"). The
      `setUpAll` semantics (ONE-time setup across ALL `test()` calls
      in the file) map to xUnit's `IClassFixture<T>` (one-time setup
      across all tests in a class) or `ICollectionFixture<T>` (one-
      time setup across multiple classes). Because this file has TWO
      sibling groups (which become two nested classes), the correct
      mapping is a COLLECTION fixture or a CLASS fixture lifted to
      the OUTER `ArithmeticTest` class shared with both nested
      classes; spec emits a `public class StdlibProgFixture { public
      BytecodeProgram StdlibProg { get; } public StdlibProgFixture()
      { StdlibProg = new GlpCompiler().Compile(File.ReadAllText(
      Path.Combine(AppContext.BaseDirectory, "..", "programs",
      "self.glp"))); } }` and routes the two nested classes through
      `[CollectionDefinition("ArithmeticPrelude")]` +
      `[Collection("ArithmeticPrelude")]` so the fixture instance is
      shared (Microsoft Learn / xunit.net Collection Fixtures
      reference). Identifier-legalisation nuance (LOAD-BEARING): the
      `:=` glyph in the group labels is NOT a legal C# identifier
      character; the spec spells it `Assign` (the language-level
      semantic of `:=` in GLP -- the SUT spec `lib/runtime/
      system_predicates.dart.md` records `:=` as the assignment
      system predicate). Identifier collision avoided: the two
      classes' English-language renderings differ only in their
      prefix (`ArithmeticVia...` vs `EndToEnd...`), so no collision.
      No `tearDownAll` in this file -- no `IDisposable.Dispose`
      content needed on the fixture.
  - construct_key: dart.package_test.setUpAll_lifecycle_hook
    source_form: |-
      "late BytecodeProgram stdlibProg;
       setUpAll(() {
         final stdlibSource = File('../programs/self.glp')
             .readAsStringSync();
         final stdlibCompiler = GlpCompiler();
         stdlibProg = stdlibCompiler.compile(stdlibSource);
         print('Stdlib compiled: ${stdlibProg.ops.length} instructions');
       });"
    target_decision: >-
      Lift the `late` file-scoped variable and its `setUpAll`
      initialisation into the body of a class fixture
      `public class StdlibProgFixture` whose constructor performs the
      compile ONCE: `public BytecodeProgram StdlibProg { get; }`
      (read-only property, set in ctor). Constructor body:
      `var stdlibSource = File.ReadAllText(Path.Combine(
      AppContext.BaseDirectory, "..", "programs", "self.glp"));
      var stdlibCompiler = new GlpCompiler();
      StdlibProg = stdlibCompiler.Compile(stdlibSource);
      _output?.WriteLine($"Stdlib compiled: {StdlibProg.Ops.Count}
      instructions");` -- the `print(...)` call lifts to an
      `ITestOutputHelper.WriteLine` IF the fixture is wired with one,
      OR to `Console.WriteLine` if not (xUnit fixtures CAN accept an
      `IMessageSink` via the `IClassFixture<T>` / `ICollectionFixture
      <T>` pattern + `[CollectionDefinition]` constructor injection,
      but the canonical-simplest emission omits the diagnostic and
      lets the fixture be silent -- the `print` is a non-load-bearing
      observer line). Spec default: OMIT the print in the fixture
      (the source's purpose is one-time setup, not diagnostic
      reporting; the message has no assertion role). The two nested
      test classes each declare a fixture constructor parameter
      `(StdlibProgFixture fixture, ITestOutputHelper output)` and
      store `_fixture` / `_output` fields, accessing the compiled
      stdlib via `_fixture.StdlibProg`. REUSE the cached
      `rf-dart-setupall-to-xunit-class-fixture` idiom from the
      sibling test/multiagent/* specs (mad_transactions_test.dart.md
      and siblings) -- KB cache hit per FR-012 / SC-007, NO
      re-research.
    idiom_id: rf-dart-setupall-to-xunit-class-fixture
    research_finding_id: rf-dart-setupall-to-xunit-class-fixture
    nuance: >-
      Setup-lifecycle nuance (load-bearing, explicitly addressed):
      Dart `setUpAll` semantics from pub.dev `package:test`
      `https://pub.dev/documentation/test_api/latest/test_api/
      setUpAll.html` -- "Registers a function to be run once before
      all tests in this group". xUnit's nearest analogue is
      `IClassFixture<T>` (one-time setup before any `[Fact]` runs in
      that test class -- xunit.net "Shared Context between Tests").
      Because THIS file's `setUpAll` is at the FILE level (not inside
      either of the two `group()` blocks), the lifecycle scope is
      ALL tests in ALL groups in the file -- which maps to xUnit
      `ICollectionFixture<T>` + `[CollectionDefinition]` +
      `[Collection]` (one-time setup before any `[Fact]` runs in
      ANY class in the collection -- Microsoft Learn / xunit.net
      Collection Fixtures reference). `late` initialisation nuance
      (Dart `late` variable, lazy-or-eager-set; here set by the
      `setUpAll` callback before any `[Fact]` runs) maps to a
      get-only auto-property on the fixture (`public T Prop { get; }`)
      that is set exactly once in the fixture constructor -- C#
      get-only auto-properties (Microsoft Learn `learn.microsoft.com/
      en-us/dotnet/csharp/properties` -- "Get-only auto-implemented
      properties can be initialized only in the constructor") preserve
      the `late`/`final` once-set semantics. Idempotence: the
      collection-fixture instance is shared across all test classes in
      the collection, so the compile runs ONCE per `dotnet test`
      invocation -- matching the Dart `setUpAll` once-per-test-file-
      process semantics. Diagnostic `print` is OMITTED in the
      canonical emission (non-load-bearing). NOT mapped to a static
      ctor (xUnit fixtures own their lifetime via `IClassFixture` /
      `ICollectionFixture`).
  - construct_key: dart.local.late_typed_variable_declaration
    source_form: "late BytecodeProgram stdlibProg;"
    target_decision: >-
      The Dart `late BytecodeProgram stdlibProg;` declaration is
      ELIMINATED at the file level -- it is replaced by the fixture
      property `public BytecodeProgram StdlibProg { get; }` on the
      `StdlibProgFixture` class (set in the fixture ctor; consumed in
      each nested test class via `_fixture.StdlibProg`). No standalone
      C# `late` modifier exists; the get-only auto-property pattern
      is the canonical replacement (Microsoft Learn `learn.microsoft.
      com/en-us/dotnet/csharp/properties` -- "Get-only auto-
      implemented properties"). REUSE the cached
      `rf-dart-late-variable-to-csharp-init-only-property` idiom from
      the sibling test/multiagent/mad_transactions_test.dart.md and
      lib/runtime/runtime.dart.md SUT spec (where `late` fields on
      `GlpRuntime` are mapped to `init`-only or get-only properties).
    idiom_id: rf-dart-late-variable-to-csharp-init-only-property
    research_finding_id: rf-dart-late-variable-to-csharp-init-only-property
    nuance: >-
      Late-binding nuance (explicitly addressed): Dart `late` variables
      (Dart language tour `https://dart.dev/language/variables#late-
      variables` -- "Use `late` when you know that a variable will
      be initialized before it's used, but it cannot be initialized
      where it's declared") permit declare-now / initialise-later
      with runtime non-null assertion on first read. C# get-only
      auto-properties (one-time assignment from the ctor only)
      preserve the once-set semantics; the runtime non-null
      assertion is replaced by C# nullability analysis at
      compile-time IF the property is declared `BytecodeProgram`
      (non-nullable) -- the fixture ctor MUST assign it, satisfying
      flow analysis. Alternative considered: `BytecodeProgram?`
      (nullable) + null-forgiving access at each call site -- inferior
      because it loses the once-set guarantee that the Dart `late`
      provides. Spec default: non-nullable get-only auto-property.
      Identifier renaming: Dart `stdlibProg` (camelCase) ->
      C# `StdlibProg` (PascalCase) per Microsoft's C# coding
      conventions for public properties.
  - construct_key: dart.test_callback.parameterless_arrow_or_block
    source_form: "test('add/3 body kernel executes directly', () { ... });"
    target_decision: >-
      Each Dart `test('<name>', () { ... })` callback inside a
      `group(...)` block lifts to one `[Fact(DisplayName = "<name>")]`
      `public void <PascalCaseName>()` method on the corresponding
      nested test class. DisplayName preserves the original label
      verbatim (xunit.net `[Fact]` reference -- "DisplayName: Marks the
      test method, so when run with the test runner, the test will use
      the given DisplayName instead of the default of using the class
      name plus method name"). Method-identifier translation:
      - 'add/3 body kernel executes directly' ->
        `Add3BodyKernelExecutesDirectly` (slash dropped; leading
        identifier `add` -> `Add`, integer `3` stays as a digit but
        the leading-digit guard does not fire because `Add` precedes
        it -- carry-forward from utility_instructions_test naming
        nuance);
      - 'sub/3 body kernel' -> `Sub3BodyKernel`;
      - 'mul/3 body kernel' -> `Mul3BodyKernel`;
      - 'div/3 body kernel' -> `Div3BodyKernel`;
      - 'div/3 body kernel aborts on division by zero' ->
        `Div3BodyKernelAbortsOnDivisionByZero`;
      - 'neg/2 body kernel' -> `Neg2BodyKernel`;
      - 'sqrt_kernel/2 body kernel' -> `SqrtKernel2BodyKernel`
        (underscore in `sqrt_kernel` dropped; PascalCased);
      - 'all standard body kernels are registered' ->
        `AllStandardBodyKernelsAreRegistered`;
      - 'assign.glp compiles and merges correctly' ->
        `AssignGlpCompilesAndMergesCorrectly` (period dropped);
      - 'user program with := compiles correctly with SRSW' ->
        `UserProgramWithAssignCompilesCorrectlyWithSRSW` (`:=` ->
        `Assign` per the `:=`-identifier-legalisation nuance recorded
        in the group construct above);
      - 'Z := 5 + 3 executes and binds Z to 8' ->
        `ZAssign5Plus3ExecutesAndBindsZTo8` (`:=` -> `Assign`; `+` ->
        `Plus`; `5`/`3`/`8` digits remain; the leading character `Z`
        avoids the leading-digit guard).
      All emit `[Fact(DisplayName = "<original Dart name>")]` -- the
      original human-readable label survives in the test reporter
      verbatim including the `:=` glyph and the slash/period. REUSE
      `rf-dart-test-callback-to-xunit-method-body` idiom (precedent:
      test_channel_construction.dart.md, smoke_test.dart.md).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Identifier-legalisation nuance (load-bearing, explicitly
      addressed): Dart test-name strings can contain ANY character;
      C# method identifiers are restricted to letter/digit/underscore
      and must not start with a digit (Microsoft Learn
      `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/
      coding-style/identifier-names`). The eleven test names in this
      file contain: slashes (`add/3`), periods (`assign.glp`),
      underscores (`sqrt_kernel`), the `:=` glyph (`Z := 5 + 3`), the
      `+` operator glyph, spaces, and digits. Translation rules
      (carry-forward from bytecode/fairness_scheduler_loop and
      utility_instructions siblings):
      (1) slashes/periods/spaces -> dropped;
      (2) underscores in identifier-fragments dropped (the C# coding
          convention prefers PascalCase joining; `sqrt_kernel` -> 
          `SqrtKernel`);
      (3) digits preserved, prefixed only if they would otherwise lead
          the identifier (does not fire for any test in this file);
      (4) `:=` -> `Assign` (semantic spelling, matching the lib SUT
          spec `lib/runtime/system_predicates.dart.md`'s `:=`
          assignment system predicate);
      (5) `+` -> `Plus` (semantic spelling).
      DisplayName preserves the EXACT original glyph sequence
      including `:=`, `+`, slashes, and Unicode -- so test reporter
      output (`dotnet test --logger trx`, VS Test Explorer) shows the
      original Dart name verbatim. Each method is sync `public void`
      (no `async`/`Future` surface in this file). Per-test fresh-
      instance lifecycle (xunit.net Shared Context -- "test classes
      are constructed once per test method") means each `[Fact]` gets
      a fresh test-class instance; ALL per-method locals are method-
      scoped (no shared mutable state across methods -- the only
      shared state is the read-only `StdlibProg` from the fixture).
  - construct_key: dart.record_destructuring.positional_pair
    source_form: "final (xWriter, xReader) = rt.heap.allocateVariable();"
    target_decision: >-
      Translate the Dart positional-record destructuring `final (a, b)
      = expr;` to C# value-tuple deconstruction `var (a, b) = expr;`.
      Emit (typical first-line of each kernel test):
      `var (xWriter, xReader) = rt.Heap.AllocateVariable();
      var (yWriter, yReader) = rt.Heap.AllocateVariable();
      var (resultWriter, resultReader) = rt.Heap.AllocateVariable();`
      The SUT spec `lib/runtime/heap_fcp.dart.md` records
      `Heap.allocateVariable()` as returning a positional record
      `(int writerAddr, int readerAddr)` -> C# `(int, int)` value
      tuple per the cached
      `rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction`
      idiom (precedent: test/multiagent/mad_transactions_test.dart.md
      `dart.record_destructuring.positional_pair`, applied verbatim).
      `final` on the LHS -> `var` (C# has no method-local single-
      assignment modifier; cached idiom). Discard nuance: in three
      tests the source uses `final (resultWriter, _) =
      rt.heap.allocateVariable();` (the `_` reader is unused) ->
      `var (resultWriter, _) = rt.Heap.AllocateVariable();` (C#
      supports the same `_` discard pattern in tuple deconstruction
      -- Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/
      csharp/fundamentals/functional/discards` -- "Discards are
      placeholder variables that are intentionally unused"). For one
      `[Fact]` (`add/3 body kernel executes directly`) the reader is
      named and BOUND (`xReader`/`yReader`/`resultReader`) and reused
      below as a `VarRef` argument -- preserved verbatim.
    idiom_id: rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction
    research_finding_id: rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction
    nuance: >-
      Record-destructuring nuance (load-bearing, explicitly addressed):
      Dart positional records `(T1, T2)` (Dart language tour
      `https://dart.dev/language/records`) map 1:1 to C# value tuples
      `(T1, T2)` (Microsoft Learn `https://learn.microsoft.com/en-us/
      dotnet/csharp/language-reference/builtin-types/value-tuples`
      -- "Tuple types support multiple ways to deconstruct: var (x,
      y) = pt;"). Field-position semantics: Dart positional `$1`/`$2`
      <-> C# `Item1`/`Item2` (the SUT spec records the converted
      method as returning `(int writerAddr, int readerAddr)` with
      NAMED fields; C# named-tuple-field names preserve the Dart
      names when both sides use the named form). Discard `_` is a
      structural placeholder in BOTH languages -- semantically
      identical (no variable allocated). Reference/value: `int` is a
      primitive value type in both -- no boxing. The `_` discard
      reader appears in three of the seven kernel tests where the
      reader is never read; identical structural drop in C#.
  - construct_key: dart.local.final_typed_constructor_invocation
    source_form: |-
      "final rt = GlpRuntime();
       final stdlibCompiler = GlpCompiler();
       final userCompiler = GlpCompiler();
       final runner = BytecodeRunner(mergedProg);
       final sched = Scheduler(rt: rt, runner: runner);"
    target_decision: >-
      Each `final <name> = <Ctor>(...);` local -> `var <name> = new
      <Ctor>(...);` per the cached
      `rf-dart-final-local-to-csharp-var` idiom (precedent throughout
      the batch including bytecode/fairness_scheduler_loop and
      utility_instructions siblings). C# requires `new` on
      constructor invocations. The named-argument call site
      `Scheduler(rt: rt, runner: runner)` -> `new Scheduler(rt: rt,
      runner: runner)` per the cached
      `rf-dart-named-arg-to-csharp-named-arg` idiom (verbatim 1:1
      `name: value` syntax). All target classes (`GlpRuntime`,
      `GlpCompiler`, `BytecodeRunner`, `Scheduler`) live in the
      collapsed `using` directives brought in by the first import
      construct above. Applies to all such locals across all eleven
      `[Fact]`s (a `GlpRuntime`, sometimes a `GlpCompiler`/two, a
      `BytecodeProgram` via merge, a `BytecodeRunner`, a `Scheduler`,
      and a `CallEnv`).
    idiom_id: rf-dart-final-local-to-csharp-var
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Local-mutability + named-argument nuance (carry-forward, cache
      hit, explicitly addressed): Dart `final` local -> C# `var`
      (single-assignment intent lost at the language level but
      structurally honored: no body reassigns these locals).
      Reference-vs-value: every class here is a reference type in
      both Dart and C# per the SUT specs (`GlpRuntime`, `GlpCompiler`,
      `BytecodeRunner`, `Scheduler`, `BytecodeProgram`, `CallEnv` are
      all `class`, not `struct`). Named-arguments: C# supports the
      identical `name: value` colon-form at the call site (Microsoft
      Learn `https://learn.microsoft.com/en-us/dotnet/csharp/
      programming-guide/classes-and-structs/named-and-optional-
      arguments`) -- direct 1:1 transcription. Constructor parameter
      identifiers (`rt`, `runner` on `Scheduler`) are owned by the
      SUT spec `lib/runtime/scheduler.dart.md` and stay lowerCamelCase
      per C# coding conventions for parameter names.
  - construct_key: dart.method_call.heap_writer_const_bind
    source_form: "rt.heap.bindVariableConst(xWriter, 5);"
    target_decision: >-
      Translate the Dart method-call chain `rt.heap.bindVariableConst(
      <writer>, <int>);` to `rt.Heap.BindVariableConst(<writer>,
      <int>);` -- direct verbatim transliteration with PascalCasing
      applied to public members (`heap` -> `Heap`,
      `bindVariableConst` -> `BindVariableConst`) per the SUT specs
      `lib/runtime/runtime.dart.md` (records the `Heap` property on
      `GlpRuntime`) and `lib/runtime/heap_fcp.dart.md` (records the
      `BindVariableConst` method on `Heap`). Integer-literal widths
      (5, 3, 10, 4, 7, 6, 15, 0, 42, 16): the SUT `BindVariableConst`
      signature determines whether literals stay `int` or widen to
      `long` / `double` (per the SUT spec's recorded width decision).
      Spec default: integer literals route to whichever overload the
      SUT spec records as the canonical binding for the `ConstTerm`
      wire-up. REUSE
      `rf-dart-camel-to-csharp-pascal-method-rename` cached batch-
      wide idiom -- no re-research.
    idiom_id: rf-dart-camel-to-csharp-pascal-method-rename
    research_finding_id: rf-dart-camel-to-csharp-pascal-method-rename
    nuance: >-
      Method-renaming + integer-width nuance (carry-forward): Dart
      camelCase method names PascalCase per Microsoft's C# coding
      conventions. Integer-literal width: Dart `int` is 64-bit signed
      on the VM (Dart language tour `https://dart.dev/language/built-
      in-types#numbers` -- "Integers are 64-bit on the Dart VM");
      `5`, `42`, `16` etc are well within both `int` and `long`
      ranges, but the SUT spec's recorded integer-width decision
      (`rf-dart-int-to-csharp-long-width` carry-forward) drives
      whether the C# literal stays `5` (int) or becomes `5L` (long).
      The `0` literal in the division-by-zero test stays as the SUT-
      decided width regardless -- the abort semantics depend on the
      kernel's runtime check, not the literal width.
  - construct_key: dart.constructor_invocation.implicit_new_varref
    source_form: "final xRef = VarRef(xReader);"
    target_decision: >-
      Translate Dart `VarRef(<int>)` (implicit-new constructor call)
      to C# `new VarRef(<int>)`. `VarRef` is a record/class on the
      SUT side per `.codeconv/conversion-specs/lib/runtime/
      machine_state.dart.md` (it wraps an `int` reader address; per
      the SUT spec it's a reference type via `record class` or
      `class`). Three explicit `VarRef` locals (`xRef`, `yRef`,
      `resultRef`) in the `add/3` test; in the other six kernel
      tests the `VarRef(...)` calls appear INLINE inside the kernel
      invocation argument list `[VarRef(xReader), VarRef(yReader),
      VarRef(resultWriter)]` -- those become `new VarRef(...)` inline
      arguments inside the C# array initializer. The kernel-call
      array-literal `[ref1, ref2, ref3]` maps to a C# array
      initializer `new[] { new VarRef(xReader), new VarRef(yReader),
      new VarRef(resultWriter) }` per the cached
      `rf-dart-list-literal-of-constructors-to-csharp-array-init`
      idiom (precedent: bytecode/fairness_scheduler_loop_test). REUSE
      `rf-dart-constructor-invocation-implicit-new-to-csharp-new`
      from test_channel_construction.dart.md.
    idiom_id: rf-dart-constructor-invocation-implicit-new-to-csharp-new
    research_finding_id: rf-dart-constructor-invocation-implicit-new-to-csharp-new
    nuance: >-
      Implicit-new nuance (carry-forward): Dart 2+ permits omitting
      the `new` keyword on constructor calls (Dart language tour
      `https://dart.dev/language/classes#using-constructors` --
      "Optional `new` keyword"); C# REQUIRES the `new` operator on
      constructor calls (Microsoft Learn `https://learn.microsoft.
      com/en-us/dotnet/csharp/language-reference/operators/new-
      operator` -- "Used to create a new instance of a type"). The
      C# 12 collection-expression form `[new VarRef(xReader), ...]`
      also works for the kernel-args list; spec default emits the
      LCD-portable `new[] { ... }` array initializer unless the SUT
      `BodyKernels.Lookup` return-type signature records an explicit
      `IReadOnlyList<VarRef>` or similar. Element-type inference:
      `VarRef` is a single concrete type, so no LUB problem; the
      array is `VarRef[]`. The kernel signature's argument list is
      `(GlpRuntime runtime, List<VarRef> args)` per the SUT spec
      `lib/runtime/body_kernels.dart.md` -- the C# emission MUST
      thread the array as `IReadOnlyList<VarRef>` /
      `IList<VarRef>` per the SUT spec's recorded collection-shape.
  - construct_key: dart.method_call.body_kernels_lookup_then_invoke
    source_form: |-
      "final kernel = rt.bodyKernels.lookup('_add', 3);
       expect(kernel, isNotNull, reason: '_add/3 kernel should be registered');
       final result = kernel!(rt, [xRef, yRef, resultRef]);"
    target_decision: >-
      Three sub-translations carried out together:
      (1) `rt.bodyKernels.lookup('_add', 3)` -> `rt.BodyKernels.Lookup(
      "_add", 3);` -- PascalCased member-access chain
      (`bodyKernels` -> `BodyKernels`, `lookup` -> `Lookup`); the
      lookup-table's `Lookup(<name>, <arity>)` shape is owned by the
      SUT spec `lib/runtime/body_kernels.dart.md` (returns
      `BodyKernel?` -- a nullable delegate-like callable). Dart's
      single-quoted string `'_add'` -> C# double-quoted `"_add"`.
      (2) The `expect(kernel, isNotNull, reason: '<msg>')` call ->
      `Assert.NotNull(kernel); // <msg>` (xUnit
      `Assert.NotNull(object)` -- xunit.net Assert API reference) plus
      the `reason:` text routed to an inline `// ...` comment, since
      `Assert.NotNull` has NO `userMessage` overload (carry-forward
      from bytecode/fairness_scheduler_loop_test.dart.md's `reason:`-
      to-inline-comment nuance). REUSE the
      `rf-dart-expect-isnotnull-to-xunit-assertnotnull` idiom from
      mad_transactions_test.dart.md.
      (3) The Dart `kernel!(rt, [xRef, yRef, resultRef])` -- Dart `!`
      non-null assertion followed by callable invocation -- maps to
      C# `kernel!.Invoke(rt, new[] { xRef, yRef, resultRef });` OR
      `kernel!(rt, new[] { xRef, yRef, resultRef });` depending on
      whether the SUT spec records `BodyKernel` as a `delegate` (then
      C# `delegate` instances ARE callable via the call syntax
      `kernel!(rt, args)`) or as an interface with `Invoke(...)`. Spec
      default: emit the `delegate`-call form `kernel!(rt, new[] { ...
      })` since the SUT spec `lib/runtime/body_kernels.dart.md`
      records `BodyKernel` as a function-typedef -> C# `delegate`. The
      `!` is the cached null-forgiving operator
      (`rf-dart-bang-null-assertion-to-csharp-null-forgiving`).
    idiom_id: rf-dart-expect-isnotnull-to-xunit-assertnotnull
    research_finding_id: rf-dart-expect-isnotnull-to-xunit-assertnotnull
    nuance: >-
      Three composed idioms here (all cached, no re-research):
      (a) `expect(x, isNotNull)` -> `Assert.NotNull(x)` (xunit.net
      `https://xunit.net/docs/getting-started` -- `Assert.NotNull`).
      `reason:` text -> inline `//` comment (no `userMessage`
      overload on `Assert.NotNull` -- xunit.net Assert API reference);
      (b) Dart `!` null-assertion (throws `TypeError` at runtime if
      null -- Dart language tour `https://dart.dev/language/operators
      #null-aware-operators`) vs C# `!` null-forgiving (compile-time
      annotation -- Microsoft Learn `https://learn.microsoft.com/en-
      us/dotnet/csharp/language-reference/operators/null-forgiving`).
      LOAD-BEARING SEMANTIC DIVERGENCE: Dart `!` DOES throw at
      runtime; C# `!` does NOT. The IMMEDIATELY preceding
      `Assert.NotNull(kernel)` in the converted C# code GUARANTEES
      `kernel` is non-null before the call, so the runtime-throw
      behaviour is faithfully reproduced even though the C# `!`
      alone would not. The semantic intent (assert + use) is
      preserved by the assert+forgiving-operator combination;
      (c) Delegate-invocation: Dart `Function?`-typed local invoked
      via `f!(args)` maps to C# delegate invocation via
      `f!(args)` (delegates ARE callable directly in C# -- Microsoft
      Learn `https://learn.microsoft.com/en-us/dotnet/csharp/
      language-reference/builtin-types/reference-types` --
      "Delegates are similar to function pointers in C/C++"; invoke
      syntax is identical to a method call).
  - construct_key: dart.package_test.expect_equality_against_enum
    source_form: "expect(result, equals(BodyKernelResult.success));"
    target_decision: >-
      Translate `expect(result, equals(<enum-value>))` to
      `Assert.Equal(<enum-value>, result);` -- EXPECTED-FIRST per the
      smoke_test.dart.md spec's recorded argument-order swap (the
      load-bearing footgun: xUnit reverses Dart's ACTUAL-FIRST order).
      The enum value `BodyKernelResult.success` -> C# `BodyKernelResult.
      Success` (enum-member PascalCase per
      `rf-dart-enum-member-access-pascalcase` idiom, precedent
      mad_transactions_test.dart.md). The enum type itself is owned
      by the SUT spec `lib/runtime/body_kernels.dart.md` -- which
      records the Dart enum `BodyKernelResult { success, abort }` as
      C# `public enum BodyKernelResult { Success, Abort }`. REUSE
      `rf-dart-expect-equals-to-xunit-assertequal` idiom (precedent:
      multiple test/* siblings).
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order + enum-PascalCase nuance (load-bearing,
      explicitly addressed). xUnit `Assert.Equal<T>(T expected, T
      actual)` reverses Dart's `expect(actual, matcher)` order
      (xunit.net Assert API reference -- "Assert.Equal<T>(T expected,
      T actual)"). The smoke_test.dart.md spec already recorded this
      footgun; codegen ALWAYS swaps. Enum-member casing: Dart enum
      members can be lowerCamelCase (`success`, `abort`) per Dart
      style; C# enum members are PascalCase per Microsoft's C# coding
      conventions (Microsoft Learn `https://learn.microsoft.com/en-
      us/dotnet/csharp/fundamentals/coding-style/identifier-names`
      -- "Use PascalCase for ... enums and enum values"). The SUT
      enum-spec recorded in `lib/runtime/body_kernels.dart.md` is
      authoritative.
  - construct_key: dart.package_test.expect_get_value_then_isA
    source_form: |-
      "final value = rt.heap.getValue(resultWriter);
       expect(value, isNotNull);
       expect(value, isA<ConstTerm>());
       expect((value as ConstTerm).value, equals(8));"
    target_decision: >-
      Translate the three-step verification (read value, assert
      non-null, assert exact type, cast and compare scalar) to the
      xUnit-IDIOMATIC compound form:
      `var value = rt.Heap.GetValue(resultWriter);
       Assert.NotNull(value);
       var constValue = Assert.IsType<ConstTerm>(value);
       Assert.Equal(8, constValue.Value);`
      The `Assert.IsType<T>(object)` overload returns the typed
      value, so the Dart `(value as ConstTerm).value` downcast +
      property access COLLAPSES into the typed `Assert.IsType<T>`
      return value -- eliminating the separate cast statement. This
      is the exact-type semantic; if subtypes are intended use
      `Assert.IsAssignableFrom<T>` instead (NOT the case here per the
      SUT spec `lib/runtime/terms.dart.md` -- `ConstTerm` is a
      concrete `sealed` term class). REUSE
      `rf-dart-expect-isa-to-xunit-istype` (precedent: moded_head_
      test.dart.md). The integer-literal `8` -> `8` (or `8L` if the
      SUT spec records `ConstTerm.Value` as `long` rather than `int`
      / `object`); same routing for `6`, `42`, `3.75`, `-42`, `4.0`
      across the seven kernel tests. The `(value as ConstTerm).value
      == -42` case (the `neg/2` test) -> `Assert.Equal(-42,
      constValue.Value);` -- the unary-minus literal `-42` is a
      compile-time integer literal in both languages. The `4.0`
      double literal (the `sqrt_kernel/2` test) -> `Assert.Equal(4.0,
      constValue.Value);` -- but a tolerance overload should be
      preferred if the SUT records the value as floating-point
      (`Assert.Equal(double, double, int precision)` -- xunit.net
      Assert API reference). Spec default: emit the exact-equality
      form; if codegen detects a `double` payload, switch to the
      precision overload `Assert.Equal(4.0, constValue.Value, 10)`.
      The `3.75` literal (the `div/3` test) is the same -- exact
      equality on a power-of-two-ratio result is exact in IEEE-754,
      so the precision overload is not strictly needed, but the spec
      RECOMMENDS it as the floating-point-safe choice.
    idiom_id: rf-dart-expect-isa-to-xunit-istype
    research_finding_id: rf-dart-expect-isa-to-xunit-istype
    nuance: >-
      Compound matcher nuance (load-bearing, explicitly addressed):
      Dart's three-step pattern (`isNotNull` + `isA<T>` + downcast +
      scalar `equals`) collapses to xUnit's two-step idiomatic form
      (`Assert.NotNull` + typed `Assert.IsType<T>` returning the
      typed value + scalar `Assert.Equal`). xUnit `Assert.IsType<T>`
      (xunit.net Assert API reference -- "Verifies that an object is
      of the given type. Returns the typed value"). Exact-type vs
      subtype-tolerant: `Assert.IsType<T>` is EXACT-type;
      `Assert.IsAssignableFrom<T>` is subtype-tolerant. Per the SUT
      `lib/runtime/terms.dart.md` spec, `ConstTerm` is the leaf
      `Term` subtype for constant values -- exact-type is correct.
      Floating-point nuance (the `4.0`, `3.75`, `-42`, `42`, `8`,
      `6`, etc literal-equality cases): Dart `equals(<num>)` does
      strict numeric equality. xUnit's `Assert.Equal(double, double)`
      WITHOUT a precision argument also does strict-equality; the
      `Assert.Equal(double, double, int precision)` overload is the
      tolerance-safe variant. Spec default: emit the strict-equality
      form (matching Dart semantics); recommend the precision
      overload only when the SUT spec records the operand as a
      computed-floating-point value (e.g. `4.0` from `sqrt(16)`,
      `3.75` from `15/4`). Boxing: Dart `int`/`double` are runtime-
      boxed under `num`; C# value-tuple/object-boxing routing is
      handled by `Assert.Equal<T>`'s generic constraint. Negative
      literal `-42`: handled identically by both languages'
      tokenisers (unary-minus followed by integer literal); no escape
      issue.
  - construct_key: dart.package_test.expect_boolean_predicate_istrue
    source_form: "expect(rt.bodyKernels.has('_add', 3), isTrue);"
    target_decision: >-
      Translate the `expect(<bool-expr>, isTrue);` pattern to
      `Assert.True(<bool-expr>);` -- xUnit `Assert.True(bool)`
      (xunit.net Assert API reference). Applied to all 25 `expect(...,
      isTrue)` calls in the `all standard body kernels are registered`
      test and 4 more in the End-to-end group, plus the chained
      `expect(prog.ops.isNotEmpty, isTrue);` and
      `expect(prog.labels.containsKey('compute_sum/1'), isTrue);`. The
      argument expression PascalCases per member (`bodyKernels.has(
      '_add', 3)` -> `BodyKernels.Has("_add", 3)`,
      `labels.containsKey('compute_sum/1')` -> 
      `Labels.ContainsKey("compute_sum/1")`, `ops.isNotEmpty` ->
      `Ops.Count > 0` since C# IReadOnlyCollection<T> has no
      `IsNotEmpty` property and `Ops.Any()` requires `using System.
      Linq;` -- spec default emits `Ops.Count > 0` for the
      portability). REUSE `rf-dart-expect-istrue-to-xunit-assert-true`
      idiom (precedent: well_typed_clause_test.dart.md,
      boot_loader_test.dart.md). REUSE
      `rf-dart-camel-to-csharp-pascal-method-rename` for the chained
      method names.
    idiom_id: rf-dart-expect-istrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-istrue-to-xunit-assert-true
    nuance: >-
      Boolean-predicate nuance (carry-forward): Dart `isTrue` is the
      package:test matcher for `Matcher.isTrue` (pub.dev
      `https://pub.dev/documentation/test_api/latest/expect/isTrue-
      constant.html` -- "A matcher that matches the boolean value
      true"). xUnit `Assert.True(bool)` (xunit.net Assert API
      reference) is the canonical idiomatic mapping. `Assert.True`
      has an OPTIONAL `userMessage` overload (`Assert.True(bool,
      string)`) -- xunit.net Assert API; this file's `isTrue` calls
      lack a `reason:` so no message is emitted. The dual matcher
      `expect(<bool-expr>, isTrue, reason: '<msg>');` ->
      `Assert.True(<bool-expr>, "<msg>");` -- the End-to-end group's
      two such calls (`reason: 'Merged program should contain :=/2
      from stdlib'`, `reason: 'Merged program should contain hello/0
      from user code'`, `reason: 'compute_sum/1 should exist'` --
      wait, the last is an `isNotNull` not `isTrue`) get the message
      threaded through. Map-key existence: Dart `Map<K,V>.
      containsKey(K)` -> C# `IDictionary<K,V>.ContainsKey(K)` (1:1
      semantic match; Microsoft Learn `https://learn.microsoft.com/
      en-us/dotnet/api/system.collections.generic.idictionary-2.
      containskey`).
  - construct_key: dart.string_interpolation.simple_expression
    source_form: "print('Stdlib compiled: ${stdlibProg.ops.length} instructions');"
    target_decision: >-
      Translate Dart interpolated strings `'... ${expr} ...'` to C#
      `$"... {expr} ..."` per the cached
      `rf-dart-string-interpolation-to-csharp-dollar-string` idiom
      (precedent: bytecode/utility_instructions_test.dart.md,
      test_channel_construction.dart.md). The eight `print(...)`
      callsites in this file all emit interpolated or plain strings;
      each lifts to `_output.WriteLine($"...{expr}...");` per the
      cached `rf-dart-print-to-xunit-itestoutputhelper-writeline`
      idiom (precedent: utility_instructions_test). Property-name
      PascalCasing applies in the interpolation: `stdlibProg.ops.
      length` -> `_fixture.StdlibProg.Ops.Count` (or `.Length` if the
      SUT spec records `Ops` as `T[]`); `resultWriter` /
      `resultReader` stay lowerCamelCase (locals). `ran.length` ->
      `ran.Count` per the SUT scheduler-spec carry-forward. The
      `value.value` interpolation in the `Result = ${value.value}`
      print collapses with the typed `Assert.IsType<T>` return
      (already used above) -- emit `_output.WriteLine($"Result =
      {constValue.Value}");` after the `var constValue =
      Assert.IsType<ConstTerm>(value);` step.
    idiom_id: rf-dart-string-interpolation-to-csharp-dollar-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-dollar-string
    nuance: >-
      Interpolation + diagnostic-output compound nuance (carry-
      forward, well-known xUnit footgun): `print` -> `ITestOutputHelper
      .WriteLine` (xunit.net `https://xunit.net/docs/capturing-output`
      -- "xUnit.net captures output via the `ITestOutputHelper`
      interface injected into the constructor"). `Console.WriteLine`
      is a viable but INFERIOR fallback (xUnit does NOT capture
      Console.Out). The nested test classes' ctors take
      `(StdlibProgFixture fixture, ITestOutputHelper output)`; store
      `_output` and route every `print` callsite through
      `_output.WriteLine(...)`. UTF-8 checkmark glyph `✓` (in `'✓ Z
      := 5 + 3 correctly evaluates to 8!'`) survives unchanged (both
      Dart and C# string literals accept the literal UTF-16 code
      point). The `\n` escape in `'\n=== END-TO-END ARITHMETIC TEST
      ==='` is processed identically by both string-literal parsers.
  - construct_key: dart.package_test.fail_call
    source_form: "fail('Result variable should be bound after execution');"
    target_decision: >-
      Translate the Dart `fail(<msg>)` call (from `package:test`'s
      `fail` top-level -- api.dart.dev `https://pub.dev/documentation/
      test_api/latest/test_api/fail.html` -- "Throws a TestFailure
      that signals the test has failed") to xUnit `Assert.Fail(<msg>);`
      per the cached `rf-dart-fail-call-to-xunit-assert-fail` idiom
      (precedent: cssg_modules_test.dart.md, mad_cold_call_isolate_
      test.dart.md). The single call here, in the `else` branch of
      `if (isBound) { ... } else { fail('...'); }`, becomes the
      `else` branch of the converted `if (isBound) { ... } else {
      Assert.Fail("Result variable should be bound after
      execution"); }`. xUnit `Assert.Fail(string)` (xunit.net Assert
      API reference -- added in xUnit.net v2.4.2) throws
      `XunitException`, matching the Dart `TestFailure` throw.
    idiom_id: rf-dart-fail-call-to-xunit-assert-fail
    research_finding_id: rf-dart-fail-call-to-xunit-assert-fail
    nuance: >-
      Unconditional-fail nuance (carry-forward, explicitly addressed):
      Dart `fail(String message)` from `package:test` -- throws
      `TestFailure` (a `package:test` exception subclass). xUnit
      `Assert.Fail(string)` throws `XunitException` (xunit.net Assert
      API reference). Both are immediate test-failure signals with a
      user-supplied message; both propagate up to the test reporter.
      Control-flow nuance: the `fail(...)` call sits in the `else`
      branch of the `if (isBound) { ... } else { fail(...); }`
      structure; the C# `Assert.Fail(...)` THROWS so any code after
      it is unreachable -- identical to the Dart semantic. Reachability
      analysis: C# compiler treats `Assert.Fail` as a normal method
      call (NOT marked `[DoesNotReturn]` in the stock xUnit signature
      in v2.4.2), so post-`Assert.Fail` code is technically reachable
      from the compiler's perspective; in practice the `else`-branch
      pattern is the only structural fit (no fall-through past it).
  - construct_key: dart.method_call.bytecode_program_merge_then_label_lookup
    source_form: |-
      "final mergedProg = userProg.merge(stdlibProg);
       expect(mergedProg.labels.containsKey(':=/2'), isTrue, reason: '...');
       expect(mergedProg.labels.containsKey('hello/0'), isTrue, reason: '...');
       final entryPc = mergedProg.labels['compute_sum/1'];
       expect(entryPc, isNotNull, reason: 'compute_sum/1 should exist');"
    target_decision: >-
      Translate the merge + label-lookup chain to: `var mergedProg =
      userProg.Merge(stdlibProg); Assert.True(mergedProg.Labels.
      ContainsKey(":=/2"), "<msg>"); Assert.True(mergedProg.Labels.
      ContainsKey("hello/0"), "<msg>"); var entryPc = mergedProg.
      Labels.TryGetValue("compute_sum/1", out var pc) ? (int?)pc :
      null; Assert.NotNull(entryPc); // compute_sum/1 should exist`
      -- OR more idiomatically, since the test then uses
      `mergedProg.labels['compute_sum/1']!` as an `int`, emit:
      `Assert.True(mergedProg.Labels.ContainsKey("compute_sum/1"),
      "compute_sum/1 should exist"); var entryPc = mergedProg.
      Labels["compute_sum/1"];` (the `!` non-null assertion is
      redundant since the indexer throws on miss). Spec default
      emits the latter form (cleaner; matches the C# indexer
      semantics). String literals containing the `:=/2` glyph stay
      verbatim: `":=/2"` is a legal C# string literal (no escape
      needed). REUSE `rf-dart-camel-to-csharp-pascal-method-rename`,
      `rf-dart-expect-istrue-to-xunit-assert-true`, and
      `rf-dart-bang-null-assertion-to-csharp-null-forgiving` -- all
      cached.
    idiom_id: rf-dart-camel-to-csharp-pascal-method-rename
    research_finding_id: rf-dart-camel-to-csharp-pascal-method-rename
    nuance: >-
      Indexer + dictionary-lookup nuance (explicitly addressed): Dart
      `Map<K,V>['<key>']` returns `V?` (nullable; returns null on
      miss); C# `IDictionary<K,V>.this[<key>]` THROWS
      `KeyNotFoundException` on miss (Microsoft Learn
      `https://learn.microsoft.com/en-us/dotnet/api/system.
      collections.generic.idictionary-2.item`). LOAD-BEARING
      SEMANTIC DIVERGENCE: faithful conversion either (a) precedes
      the indexer with `ContainsKey` (eager throw -> explicit) or
      (b) uses `TryGetValue` (no throw -> nullable). The Dart source
      writes both styles: `labels['compute_sum/1']` (which is then
      `!`-asserted) vs `labels.containsKey('...')`. Spec default
      emits the indexer-throws form for the first style (matches
      Dart `!` runtime-throw intent) and the `TryGetValue` form
      only when the Dart code explicitly tests `containsKey`. Method-
      name: Dart `merge(BytecodeProgram other)` -> C# `Merge(
      BytecodeProgram other)` per the SUT spec
      `lib/bytecode/runner.dart.md` (`BytecodeProgram.Merge`
      returns a new `BytecodeProgram` -- immutable merge).
  - construct_key: dart.method_call.gq_enqueue_with_goalref
    source_form: "rt.gq.enqueue(GoalRef(goalId, entryPc!));"
    target_decision: >-
      Translate `rt.gq.enqueue(GoalRef(<int>, <int>!))` to
      `rt.Gq.Enqueue(new GoalRef(<int>, entryPc!));` -- the cached
      property-chain + null-forgiving + `new`-on-ctor idioms compose.
      The SUT specs `lib/runtime/runtime.dart.md` (the `Gq` property
      / `GoalQueue` reference on `GlpRuntime`) and
      `lib/runtime/goal_queue.dart.md` (the `Enqueue(GoalRef)`
      method) own the names. The `entryPc` variable here is `int?`
      (the result of the dictionary lookup -- see prior construct);
      `entryPc!` is C#'s null-forgiving. REUSE
      `rf-dart-property-chain-method-call-to-csharp` and
      `rf-dart-bang-null-assertion-to-csharp-null-forgiving`
      (both cached, precedent bytecode/fairness_scheduler_loop).
    idiom_id: rf-dart-property-chain-method-call-to-csharp
    research_finding_id: rf-dart-property-chain-method-call-to-csharp
    nuance: >-
      Carry-forward (KB cache hit per FR-012 / SC-007); the precise
      nuances (Dart `!` runtime-throw vs C# `!` compile-time
      annotation) are recorded in bytecode/fairness_scheduler_loop_
      test.dart.md and bytecode/utility_instructions_test.dart.md.
      For the `entryPc!` case, the preceding `Assert.NotNull(entryPc)`
      in the same `[Fact]` body guarantees non-null at runtime, so
      the C# `!` -> `int` (unwrap) is faithful.
  - construct_key: dart.method_call.scheduler_drain_with_debug
    source_form: "final ran = sched.drain(maxCycles: 100, debug: true, debugOutput: true);"
    target_decision: >-
      Translate the named-argument scheduler call to `var ran = sched.
      Drain(maxCycles: 100, debug: true, debugOutput: true);` --
      direct verbatim transcription preserving each named argument
      (C# `name: value` colon-form, identical to Dart). Method name
      PascalCases (`drain` -> `Drain`); parameter names stay
      lowerCamelCase (`maxCycles`, `debug`, `debugOutput`) per the
      SUT spec `lib/runtime/scheduler.dart.md` (parameter-naming
      carry-forward). Boolean literals `true` map verbatim. REUSE
      `rf-dart-named-arg-to-csharp-named-arg` (cached).
    idiom_id: rf-dart-named-arg-to-csharp-named-arg
    research_finding_id: rf-dart-named-arg-to-csharp-named-arg
    nuance: >-
      Named-argument carry-forward; precedent in bytecode/
      fairness_scheduler_loop_test.dart.md. The `Drain` return type
      is owned by the SUT scheduler spec; the `ran.length` later
      interpolation routes to `.Count` (or `.Length`) per the SUT
      collection-shape decision.
  - construct_key: dart.for_loop.c_style_int_index
    source_form: |-
      "for (var id = 10000; id < rt.nextGoalId; id++) { ... }"
    target_decision: >-
      Translate the Dart C-style `for (var id = <init>; <test>; <inc>)
      { ... }` loop to the IDENTICAL C# syntax: `for (var id = 10000;
      id < rt.NextGoalId; id++) { ... }`. Both languages share C-
      derived for-loop syntax (Dart language tour `https://dart.dev/
      language/loops#for-loops`, Microsoft Learn `https://learn.
      microsoft.com/en-us/dotnet/csharp/language-reference/
      statements/iteration-statements#the-for-statement`). Property
      `nextGoalId` PascalCases -> `NextGoalId` per the SUT spec
      `lib/runtime/runtime.dart.md`. The loop body uses
      `rt.getGoalEnv(id)` -> `rt.GetGoalEnv(id)` and tests the result
      `if (env != null) { ... }` -> C# `if (env != null) { ... }`
      (identical). The body's `print` calls route through
      `_output.WriteLine($"...")`. REUSE
      `rf-dart-c-style-for-loop-to-csharp-for-loop` if recorded in a
      sibling; FIRST-SEEN idiom on this file otherwise (record under
      that key).
    idiom_id: rf-dart-c-style-for-loop-to-csharp-for-loop
    research_finding_id: rf-dart-c-style-for-loop-to-csharp-for-loop
    nuance: >-
      For-loop nuance (explicitly addressed): Dart's C-style
      `for` loop has IDENTICAL syntax and semantics to C#'s. Both
      use a declaration-init, test, and post-iteration expression.
      No iterator-vs-range nuance (both languages also have
      `for-in`/`foreach`, not used here). The post-increment `id++`
      is integer addition in both languages (Dart `int` is 64-bit
      VM-side; C# `int` is 32-bit -- the SUT spec's recorded width
      decision drives whether the loop variable is `int` or `long`).
      `nextGoalId` getter is read-only on the SUT side.
  - construct_key: dart.map_literal.int_to_varref_arg_map
    source_form: |-
      "final env = CallEnv(args: {
         0: VarRef(resultWriter),  // Pass writer to head position Z
       });"
    target_decision: >-
      Translate the Dart constructor-with-map-literal call `CallEnv(
      args: {0: VarRef(resultWriter)})` to C#: `var env = new
      CallEnv(args: new Dictionary<int, VarRef> { { 0, new VarRef(
      resultWriter) } });` -- OR using C# 12 collection-expression:
      `new CallEnv(args: new Dictionary<int, VarRef> { [0] = new
      VarRef(resultWriter) });` (the index-initialiser form,
      Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/
      csharp/programming-guide/classes-and-structs/object-and-
      collection-initializers#object-initializers-with-collection-
      read-only-property-initializing`). The key type (`int` -> `int`)
      and value type (`VarRef` -> `VarRef`) are inferred. The SUT
      spec `lib/runtime/machine_state.dart.md` records `CallEnv.args`
      as `Map<int, VarRef>` -> C# `IReadOnlyDictionary<int, VarRef>`
      or `Dictionary<int, VarRef>` per the SUT spec's recorded
      collection-shape decision. REUSE
      `rf-dart-map-literal-to-csharp-dictionary-initializer` (cached
      idiom, precedent partial_evaluator_test.dart.md and
      mad_transactions_test.dart.md).
    idiom_id: rf-dart-map-literal-to-csharp-dictionary-initializer
    research_finding_id: rf-dart-map-literal-to-csharp-dictionary-initializer
    nuance: >-
      Map-literal nuance (explicitly addressed): Dart `{<K>: <V>,
      ...}` is a literal `Map<K,V>` (Dart language tour `https://
      dart.dev/language/collections#maps`). C# has TWO idiomatic
      forms: (a) collection-initialiser `new Dictionary<K,V> { {k,
      v}, ... }` (Microsoft Learn `https://learn.microsoft.com/en-
      us/dotnet/csharp/programming-guide/classes-and-structs/object-
      and-collection-initializers#collection-initializers`); (b)
      index-initialiser `new Dictionary<K,V> { [k] = v, ... }`
      (Microsoft Learn same page, index-initialisers section). Both
      forms are semantically equivalent; the index-initialiser is
      preferred when keys are integer literals (cleaner). Spec
      default emits the index-initialiser form because the source's
      key (`0`) is an integer literal. Mutability: Dart map literals
      are mutable; C# `Dictionary<K,V>` is mutable -- semantic match.
      Comment preservation: the inline Dart `// Pass writer to head
      position Z` comment translates verbatim to C# `// Pass writer
      to head position Z`.
  - construct_key: dart.const_local.typed_int_literal
    source_form: "final goalId = 1;"
    target_decision: >-
      Translate `final goalId = 1;` to `var goalId = 1;` (C# `var`
      with an `int` literal). The Dart `final` here is reassign-
      prohibition; C# `var` is semantic equivalent at the method-
      local level. The `goalId` value flows into `rt.setGoalEnv(
      goalId, env)` and `rt.gq.enqueue(GoalRef(goalId, entryPc!))`;
      the SUT spec records `GoalId` as a typedef-`int` (or `long`) --
      spec emission uses whichever width the SUT spec records. REUSE
      `rf-dart-final-local-to-csharp-var` (cached).
    idiom_id: rf-dart-final-local-to-csharp-var
    research_finding_id: rf-dart-final-local-to-csharp-var
    nuance: >-
      Width-routing carry-forward (`rf-dart-int-to-csharp-long-width`
      from the lib SUT specs); the literal `1` fits both `int` and
      `long`. No `const` keyword on the source (would map to C#
      `const`, but the source uses `final`); single-assignment
      intent preserved by absence of body reassignment.
conversion_units:
  - "using Xunit; (file-level using directive replacing `import 'package:test/test.dart';`)"
  - "using System.IO; (file-level using directive for `File.ReadAllText` and `Path.Combine`)"
  - "using <RootNs>.Compiler; (file-level using directive collapsing `import 'package:glp_runtime/compiler/compiler.dart';` -- namespace owned by the compiler SUT specs)"
  - "using <RootNs>.Bytecode; (file-level using directive collapsing `import 'package:glp_runtime/bytecode/runner.dart';` -- namespace owned by the bytecode SUT specs)"
  - "using <RootNs>.Runtime; (file-level using directive collapsing the five `package:glp_runtime/runtime/...` imports -- namespace owned by the runtime SUT specs)"
  - "public class StdlibProgFixture { public BytecodeProgram StdlibProg { get; } public StdlibProgFixture() { ... } } (class fixture; constructor compiles `../programs/self.glp` via `File.ReadAllText(Path.Combine(AppContext.BaseDirectory, \"..\", \"programs\", \"self.glp\"))` and `new GlpCompiler().Compile(...)`; assigns to get-only auto-property)"
  - "[CollectionDefinition(\"ArithmeticPrelude\")] public class ArithmeticPreludeCollection : ICollectionFixture<StdlibProgFixture> { } (xUnit collection-fixture marker class)"
  - "public class ArithmeticTest { ... } (outer file-level test container; mirrors arithmetic_test.dart -> ArithmeticTest.cs)"
  - "[Collection(\"ArithmeticPrelude\")] public class ArithmeticViaAssignSystemPredicate { private readonly StdlibProgFixture _fixture; private readonly ITestOutputHelper _output; public ArithmeticViaAssignSystemPredicate(StdlibProgFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; } ... } (nested test class for `group('Arithmetic via := system predicate', ...)`; carries `[Trait(\"Group\", \"Arithmetic via := system predicate\")]`)"
  - "[Fact(DisplayName = \"add/3 body kernel executes directly\")] public void Add3BodyKernelExecutesDirectly() { ... }"
  - "method body of Add3BodyKernelExecutesDirectly: var rt = new GlpRuntime(); var (xWriter, xReader) = rt.Heap.AllocateVariable(); var (yWriter, yReader) = rt.Heap.AllocateVariable(); var (resultWriter, resultReader) = rt.Heap.AllocateVariable(); rt.Heap.BindVariableConst(xWriter, 5); rt.Heap.BindVariableConst(yWriter, 3); var xRef = new VarRef(xReader); var yRef = new VarRef(yReader); var resultRef = new VarRef(resultWriter); var kernel = rt.BodyKernels.Lookup(\"_add\", 3); Assert.NotNull(kernel); // _add/3 kernel should be registered  var result = kernel!(rt, new[] { xRef, yRef, resultRef }); Assert.Equal(BodyKernelResult.Success, result); var value = rt.Heap.GetValue(resultWriter); Assert.NotNull(value); var constValue = Assert.IsType<ConstTerm>(value); Assert.Equal(8, constValue.Value);"
  - "[Fact(DisplayName = \"sub/3 body kernel\")] public void Sub3BodyKernel() { ... } (structurally identical to Add3 with operands 10/4 and expected 6)"
  - "[Fact(DisplayName = \"mul/3 body kernel\")] public void Mul3BodyKernel() { ... } (operands 7/6, expected 42)"
  - "[Fact(DisplayName = \"div/3 body kernel\")] public void Div3BodyKernel() { ... } (operands 15/4, expected 3.75 -- consider Assert.Equal(3.75, constValue.Value, 10) precision overload per SUT-recorded float width)"
  - "[Fact(DisplayName = \"div/3 body kernel aborts on division by zero\")] public void Div3BodyKernelAbortsOnDivisionByZero() { ... } (operands 10/0, expected BodyKernelResult.Abort; NO post-result value check -- abort short-circuits)"
  - "[Fact(DisplayName = \"neg/2 body kernel\")] public void Neg2BodyKernel() { ... } (unary; operand 42, expected -42)"
  - "[Fact(DisplayName = \"sqrt_kernel/2 body kernel\")] public void SqrtKernel2BodyKernel() { ... } (unary; operand 16, expected 4.0 -- consider precision overload)"
  - "[Fact(DisplayName = \"all standard body kernels are registered\")] public void AllStandardBodyKernelsAreRegistered() { ... } (25 Assert.True(rt.BodyKernels.Has(\"_<name>\", <arity>)) calls verbatim)"
  - "[Collection(\"ArithmeticPrelude\")] public class EndToEndAssignSystemPredicate { ... constructor with fixture + output ... } (nested test class for `group('End-to-end := system predicate', ...)`; carries `[Trait(\"Group\", \"End-to-end := system predicate\")]`)"
  - "[Fact(DisplayName = \"assign.glp compiles and merges correctly\")] public void AssignGlpCompilesAndMergesCorrectly() { ... } (uses _fixture.StdlibProg for the stdlib side; compiles `hello.` user source; merges; Assert.True on labels.ContainsKey(\":=/2\") and labels.ContainsKey(\"hello/0\") with user-message)"
  - "[Fact(DisplayName = \"user program with := compiles correctly with SRSW\")] public void UserProgramWithAssignCompilesCorrectlyWithSRSW() { ... } (compiles `compute_sum(Z?) :- Z := 5 + 3.`; Assert.True(prog.Ops.Count > 0); Assert.True(prog.Labels.ContainsKey(\"compute_sum/1\")))"
  - "[Fact(DisplayName = \"Z := 5 + 3 executes and binds Z to 8\")] public void ZAssign5Plus3ExecutesAndBindsZTo8() { ... } (full end-to-end; compiles stdlib, compiles user, merges, allocates result var, sets up CallEnv with arg index 0 -> new VarRef(resultWriter), creates BytecodeRunner + Scheduler, enqueues goal, drains, iterates for (var id = 10000; id < rt.NextGoalId; id++) { ... }, asserts isBound true and value is ConstTerm with .Value == 8; else-branch Assert.Fail(\"Result variable should be bound after execution\"))"
  - "NO equivalent of Dart's void main() -- xUnit discovery is attribute-driven (smoke_test.dart-spec recorded carry-forward); the file-level setUpAll + late variable lift entirely into the ICollectionFixture<StdlibProgFixture> + [Collection] mechanism"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-to-dotnet-xunit -- `package:test` => xUnit framework choice (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: framework decision settled in smoke_test.dart-spec; reused verbatim across every test-file spec including the bytecode siblings. Authoritative sources cited verbatim in the originating spec: Microsoft Learn `unit-testing-csharp-with-xunit`, xunit.net, pub.dev/package:test.
- **Conclusion**: drop `import 'package:test/test.dart';`, emit `using Xunit;`. `.csproj`-level NuGet wiring is OUT OF SCOPE. Zero escalation.

### rf-dart-internal-package-import-to-csharp-using -- `package:glp_runtime/...` => collapsed `using` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in the bytecode siblings; seven Dart imports collapse to three C# `using` directives (three distinct target sub-namespaces: Compiler, Bytecode, Runtime). Authoritative .NET citation: Microsoft Learn C# `using-directive` reference.
- **Conclusion**: emit three collapsed `using` directives. Zero escalation.

### rf-dart-dart-io-to-csharp-system-io -- `dart:io` => `System.IO` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in module_hierarchy_test.dart-spec, repl_play_runner.dart-spec, runtime.dart-spec. Dart `File('<p>').readAsStringSync()` is the only `dart:io` surface; maps to C# `File.ReadAllText(<p>)`.
- **Relative-path nuance (load-bearing)**: Dart resolves against the test-runner CWD (`glp_runtime/`); C# resolves against the process CWD (set by `dotnet test`). Faithful conversion routes through `Path.Combine(AppContext.BaseDirectory, ...)`. Zero escalation.

### rf-dart-test-main-to-xunit-class-with-facts + rf-dart-package-test-group-to-xunit-class -- `void main() { setUpAll + group + group }` => nested-classes + collection-fixture (REUSED + composed)

- **KB reuse (FR-012 / SC-007)**: recorded in smoke_test.dart-spec, mad_transactions_test.dart-spec, globalize_test.dart-spec.
- **File-specific composition**: TWO sibling `group()` calls + a file-level `setUpAll` -> outer `ArithmeticTest` container with TWO nested `[Collection("ArithmeticPrelude")]` test classes sharing a `StdlibProgFixture` via xUnit's `ICollectionFixture<T>` + `[CollectionDefinition]` mechanism (xunit.net Shared Context reference).
- **Identifier-legalisation nuance (load-bearing)**: `:=` glyph -> `Assign` (per the SUT spec `lib/runtime/system_predicates.dart.md`'s `:=` assignment system predicate); `+` -> `Plus`; preserved verbatim via `[Fact(DisplayName = "...")]`. Zero escalation.

### rf-dart-setupall-to-xunit-class-fixture + rf-dart-late-variable-to-csharp-init-only-property -- `late T x; setUpAll(() { x = ...; })` => class/collection fixture + get-only auto-property (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in mad_transactions_test.dart-spec and the runtime.dart SUT spec for `late` fields.
- **Authoritative**: xunit.net Shared Context reference; Microsoft Learn get-only auto-properties.
- **Conclusion**: lift to `ICollectionFixture<StdlibProgFixture>`; the `late BytecodeProgram stdlibProg` becomes `public BytecodeProgram StdlibProg { get; }` on the fixture. The diagnostic `print` in the `setUpAll` body is OMITTED in canonical emission. Zero escalation.

### rf-dart-test-callback-to-xunit-method-body -- `test('<name>', () { ... })` => `[Fact(DisplayName = "<name>")] public void <Method>()` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in smoke_test.dart-spec, test_channel_construction.dart-spec, and every test-file sibling.
- **Identifier-legalisation per-test (load-bearing)**: eleven Dart test names contain `/`, `.`, `_`, `:=`, `+`, spaces, digits, Unicode. Translation rules carry forward from the bytecode siblings; `:=`-renderings spelled as `Assign`. DisplayName preserves the exact original glyph sequence verbatim. Zero escalation.

### rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction -- `final (a, b) = expr` => `var (a, b) = expr` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in mad_transactions_test.dart-spec; applied verbatim. SUT-spec `lib/runtime/heap_fcp.dart.md` records `Heap.AllocateVariable()` as returning `(int writerAddr, int readerAddr)`.
- **Authoritative**: Dart records language tour; Microsoft Learn value-tuple deconstruction. Discard `_` is a structural placeholder in both languages.
- **Conclusion**: emit `var (xWriter, xReader) = rt.Heap.AllocateVariable();` and analogous lines; `_` discards preserved verbatim. Zero escalation.

### rf-dart-final-local-to-csharp-var + rf-dart-constructor-invocation-implicit-new-to-csharp-new + rf-dart-named-arg-to-csharp-named-arg -- `final x = Ctor(arg: ...)` => `var x = new Ctor(arg: ...)` (REUSED, composed)

- **KB reuse (FR-012 / SC-007)**: recorded throughout the batch. Authoritative sources: Dart language tour `variables#final-and-const` and `language/classes#using-constructors`; Microsoft Learn C# statements/declarations, `new` operator, and named-arguments programming guide. C# 4.0+ supports identical `name: value` colon-form named-argument syntax. Zero escalation.

### rf-dart-camel-to-csharp-pascal-method-rename -- `obj.camelCaseMember(...)` => `obj.PascalCaseMember(...)` (REUSED, batch-wide)

- **KB reuse (FR-012 / SC-007)**: recorded throughout the lib + test specs. Authoritative: Microsoft Learn C# Coding Conventions ("Use PascalCase for ... methods"). Applies to `bindVariableConst` -> `BindVariableConst`, `lookup` -> `Lookup`, `getValue` -> `GetValue`, `setGoalEnv` -> `SetGoalEnv`, `enqueue` -> `Enqueue`, `drain` -> `Drain`, `compile` -> `Compile`, `merge` -> `Merge`, `containsKey` -> `ContainsKey`, `has` -> `Has`, etc. Parameter-names stay lowerCamelCase. Zero escalation.

### rf-dart-expect-isnotnull-to-xunit-assertnotnull -- `expect(x, isNotNull, reason: msg)` => `Assert.NotNull(x); // msg` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in mad_transactions_test.dart-spec. Authoritative: xunit.net Assert API reference for `Assert.NotNull(object)`. `Assert.NotNull` has NO `userMessage` overload, so the Dart `reason:` text routes to an inline `// ...` comment (carry-forward from bytecode/fairness_scheduler_loop's `reason:`-to-comment nuance). Zero escalation.

### rf-dart-expect-isa-to-xunit-istype -- `expect(v, isA<T>()); expect((v as T).field, equals(x));` => `var typed = Assert.IsType<T>(v); Assert.Equal(x, typed.Field);` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in moded_head_test.dart-spec. Authoritative: xunit.net Assert API for `Assert.IsType<T>(object)` (returns the typed value -- so the Dart `(v as T).field` downcast COLLAPSES into the typed-return). SUT spec `lib/runtime/terms.dart.md` records `ConstTerm` as a concrete leaf term-subclass -> exact-type is correct.
- **Floating-point nuance**: recommend `Assert.Equal(expected, actual, precision)` overload for `4.0` / `3.75` operands (xunit.net Assert API). Zero escalation.

### rf-dart-expect-equals-to-xunit-assertequal -- `expect(actual, equals(expected))` => `Assert.Equal(expected, actual)` (REUSED, EXPECTED-FIRST swap)

- **KB reuse (FR-012 / SC-007)**: recorded in smoke_test.dart-spec, mad_transactions_test.dart-spec, every test-file sibling. The EXPECTED-FIRST argument-order swap is the load-bearing footgun documented in smoke_test.dart-spec. Enum-member PascalCase (`success` -> `Success`, `abort` -> `Abort`) per Microsoft's C# coding conventions; SUT-recorded in `lib/runtime/body_kernels.dart.md`. Zero escalation.

### rf-dart-expect-istrue-to-xunit-assert-true -- `expect(b, isTrue, reason: msg)` => `Assert.True(b, msg)` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in well_typed_clause_test.dart-spec and boot_loader_test.dart-spec. Authoritative: xunit.net Assert API `Assert.True(bool)` + `Assert.True(bool, string)` (with user-message overload, unlike `Assert.NotNull` / `Assert.Equal`). The Dart `reason:` text routes to the `userMessage` parameter verbatim where present. Zero escalation.

### rf-dart-string-interpolation-to-csharp-dollar-string + rf-dart-print-to-xunit-itestoutputhelper-writeline -- `print('...${e}...')` => `_output.WriteLine($"...{e}...")` (REUSED, composed)

- **KB reuse (FR-012 / SC-007)**: recorded in utility_instructions_test.dart-spec and test_channel_construction.dart-spec. Authoritative: xunit.net `https://xunit.net/docs/capturing-output` ("xUnit.net captures output via the `ITestOutputHelper` interface injected into the constructor"). UTF-8 glyph `✓` survives unchanged in both Dart and C# string literals. The eight `print(...)` callsites in this file all route through `_output.WriteLine($"...")`. Zero escalation.

### rf-dart-fail-call-to-xunit-assert-fail -- `fail(msg)` => `Assert.Fail(msg)` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in cssg_modules_test.dart-spec and mad_cold_call_isolate_test.dart-spec. Authoritative: pub.dev `package:test` `fail` reference; xunit.net Assert API `Assert.Fail(string)` (xUnit.net v2.4.2+). Single use in the `Z := 5 + 3 ...` test's else-branch -- semantically identical control-flow lift. Zero escalation.

### rf-dart-property-chain-method-call-to-csharp + rf-dart-bang-null-assertion-to-csharp-null-forgiving -- `rt.gq.enqueue(GoalRef(id, prog.labels['k']!))` => `rt.Gq.Enqueue(new GoalRef(id, prog.Labels["k"]!))` (REUSED, composed)

- **KB reuse (FR-012 / SC-007)**: recorded in bytecode/fairness_scheduler_loop_test.dart-spec and bytecode/utility_instructions_test.dart-spec. LOAD-BEARING SEMANTIC NUANCE (carry-forward, explicitly addressed): Dart `!` DOES throw at runtime on null; C# `!` does NOT (compile-time only). The C# `IDictionary<K,V>` indexer throws `KeyNotFoundException` on missing key (Microsoft Learn IDictionary indexer reference), matching the runtime-throw intent. Zero escalation.

### rf-dart-c-style-for-loop-to-csharp-for-loop -- `for (var i = init; test; inc) { ... }` (FIRST-SEEN here)

- **Deep analysis**: Dart C-style `for` loop -> identical C# C-style `for` loop. Both languages share C-derived for-loop syntax with no semantic divergence.
- **Authoritative Dart**: language tour `dart.dev/language/loops#for-loops`. **Authoritative .NET**: Microsoft Learn `dotnet/csharp/language-reference/statements/iteration-statements#the-for-statement`.
- **Conclusion**: emit `for (var id = 10000; id < rt.NextGoalId; id++) { ... }` verbatim. Loop-variable width per SUT-recorded `int`/`long` decision. Zero escalation.

### rf-dart-map-literal-to-csharp-dictionary-initializer -- `{<K>: <V>, ...}` => `new Dictionary<K,V> { [k] = v, ... }` (REUSED)

- **KB reuse (FR-012 / SC-007)**: recorded in partial_evaluator_test.dart-spec and mad_transactions_test.dart-spec. Authoritative: Dart collections language tour; Microsoft Learn collection-initialisers + index-initialisers. Index-initialiser form preferred for integer-literal keys (cleaner). Zero escalation.

## Notes

- No `async` / `Future` / `Stream` / `Completer` / `Timer` / isolate surface in this file -- every `[Fact]` is sync `public void` (not `async Task`). The well-known async-Dart-vs-.NET-async nuance is deliberately not asserted here (does not apply to this file's source surface). The only file-level lifecycle hook is `setUpAll` (sync) -> sync `IClassFixture`/`ICollectionFixture` ctor.
- No `mixin`, `extension`, generics-declaration, sealed/abstract, bitwise/shift -- all absent. Null-safety surface fires three times (the three `expect(..., isNotNull, ...)` calls plus the three `!` non-null assertions on `kernel!`, `entryPc!`, and inside the seven kernel-arg lists); all addressed via the cached idiom + `Assert.NotNull` + null-forgiving compound.
- The file exercises the runtime's body-kernel + heap + scheduler + compiler surface (`GlpRuntime`, `GlpCompiler`, `BytecodeProgram.merge`, `BytecodeProgram.labels[...]`, `BytecodeRunner`, `Scheduler.drain`, `CallEnv`, `GoalRef`, `Heap.allocateVariable`, `Heap.bindVariableConst`, `Heap.getValue`, `Heap.isWriterBound`, `BodyKernels.lookup`, `BodyKernels.has`, `BodyKernelResult.success`/`abort`, `Runtime.setGoalEnv`/`getGoalEnv`/`nextGoalId`/`gq`, `VarRef`, `ConstTerm`). The SUT-side conversion shape (class names, method names, parameter names, enum members, return types, indexer behaviour, term hierarchy, integer-width decision, collection-shape decision) is owned by the SUT specs at `.codeconv/conversion-specs/lib/compiler/compiler.dart.md`, `.codeconv/conversion-specs/lib/bytecode/runner.dart.md`, `.codeconv/conversion-specs/lib/runtime/runtime.dart.md`, `.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md`, `.codeconv/conversion-specs/lib/runtime/machine_state.dart.md`, `.codeconv/conversion-specs/lib/runtime/terms.dart.md`, `.codeconv/conversion-specs/lib/runtime/body_kernels.dart.md`, `.codeconv/conversion-specs/lib/runtime/scheduler.dart.md`, `.codeconv/conversion-specs/lib/runtime/goal_queue.dart.md` -- this test convspec references their decisions but does not duplicate them.
- The `:=` glyph in the GLP `assign` system predicate and in two of the test display names: identifier-renderings spell it as `Assign` (semantic spelling); DisplayName preserves it verbatim. Recorded as a reusable consideration for any future test conversion involving the `:=` glyph.
- The `setUpAll` + `late`-variable + file-level `group + group` structural composition recorded here (lift to `ICollectionFixture<T>` + `[CollectionDefinition]` + two `[Collection]`-tagged nested test classes) is a reusable composed shape for any future test file with the same shape.
- The Dart `!` non-null assertion vs. C# `!` null-forgiving semantic-divergence nuance is recorded as load-bearing -- SUT-side indexers / property getters must throw at runtime on missing/null to preserve the Dart runtime-throw guarantee. The C# `IDictionary` indexer (`BytecodeProgram.Labels`, `Heap.GetValue`-routing) satisfies that intent.
- Zero escalations: every construct is authoritative-supported on both sides; the overwhelming majority REUSE idioms/findings from sibling specs (bytecode/fairness_scheduler_loop_test.dart.md, bytecode/utility_instructions_test.dart.md, smoke_test.dart.md, glp_runtime_test.dart.md, mad_transactions_test.dart.md, moded_head_test.dart.md, module_hierarchy_test.dart.md, cssg_modules_test.dart.md, boot_loader_test.dart.md, well_typed_clause_test.dart.md, partial_evaluator_test.dart.md, test_channel_construction.dart.md) per FR-012 / SC-007 KB-reuse decision order. The one FIRST-SEEN row (`rf-dart-c-style-for-loop-to-csharp-for-loop`) carries authoritative citations from both Dart and Microsoft Learn references.
