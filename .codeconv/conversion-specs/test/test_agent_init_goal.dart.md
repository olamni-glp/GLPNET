# Conversion Spec — test/test_agent_init_goal.dart

> Conversion-spec artifact for test/test_agent_init_goal.dart (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> This file is a `void main() async` **debug/diagnostic script** (NOT a
> `package:test` file): no `package:test` import, no `test(...)`, no
> `group(...)`, no `expect(...)`, no matchers. It mimics the Flutter
> multiagent boot pathway end-to-end — compile a `.glp` program, build
> a `GlpRuntime` + heap, create two external channels (user / net),
> build their Channel terms, allocate three reader/writer pairs (one
> per `agent_init/3` argument), bind constants/Channels, install a
> three-slot `CallEnv`, set goal env+program, enqueue `GoalRef(100,
> entryPC)`, build a `Scheduler`, and call `drainWithStatus(maxCycles:
> 100, debug: true, debugOutput: true)`. All output is `print(...)`
> diagnostics — there are NO assertions. The host shape is therefore
> `static Main` console-exe (the **debug_negative.dart precedent**),
> NOT `[Fact]` (the test_channel_construction.dart precedent).
>
> Inherited escalations: this file transitively exercises the
> multiagent boot pathway (`GlpRuntime`, `Scheduler`, `BytecodeRunner`,
> heap callbacks) which depend on the heap_fcp.dart `escalations[0]`
> threading-model decision. Per FR-013 and the sibling-multiagent
> precedent (mad_context / global_send / message_queue / scheduler all
> INHERIT that escalation), this file INHERITS without re-escalating —
> there is no genuinely-local undecidable point in THIS file.

```yaml
schema_version: 1
source_path: test/test_agent_init_goal.dart
source_sha256: 7733bef617eea001d86bc8a9e045b14a83c5490d03ed9ba20318d1090b09d122
target_code_unit: test/TestAgentInitGoal.cs
constructs:
  - construct_key: dart.doc_comment.file_header_triple_slash
    source_form: "/// Test to debug agent_init goal setup - mimics Flutter app behavior"
    target_decision: >-
      Single-line file-header `///` Dart doc comment maps to a single-
      line `///` C# XML-doc comment placed above the host class
      declaration `public static class TestAgentInitGoal` (the host
      class chosen by the void_main construct below). The text is
      preserved byte-identical; no `<summary>` wrapping is added because
      the original is a one-line preamble, not a structured doc-comment.
    idiom_id: null
    research_finding_id: rf-dart-tripleslash-doc-to-csharp-xml-doc
    nuance: >-
      Doc-comment-target nuance: Dart `///` is a documentation comment
      attached to the FOLLOWING declaration; C# `///` is identical in
      role. With no `library;` directive in this file, the comment
      attaches to the next declaration (the implicit top-level `main`
      in Dart); in C# it attaches to the host static class. No
      semantic change. No async/Stream/Future/value-vs-reference
      surface implicated.

  - construct_key: dart.import.dart_io_file_only_sync_read
    source_form: "import 'dart:io';"
    target_decision: >-
      Dart `dart:io` is the Dart-VM platform library exposing `File`,
      `Directory`, `Platform`, `stdin`, `stdout`, `Process`, etc. In
      THIS file only ONE member is exercised: `File('path').readAsStringSync()`
      (synchronous file read returning the whole file as a String).
      Maps to `using System.IO;` at file scope — the .NET counterpart
      that provides `File.ReadAllText(string path)` (the canonical
      synchronous file-text reader per Microsoft Learn). NO `using
      System.IO.File;` static-using is emitted — the call site is
      written as `File.ReadAllText(...)`, matching the Dart `File('
      ...').readAsStringSync()` shape one-to-one.
    idiom_id: null
    research_finding_id: rf-dart-dart-io-file-readasstringsync-to-system-io-file-readalltext
    nuance: >-
      Dart-io-vs-System.IO nuance (explicitly addressed and LOAD-BEARING):
      Dart `dart:io` is unavailable on Flutter Web / dart2js / browser
      targets — it is a Dart-VM-only library. .NET `System.IO.File` is
      available on every modern .NET runtime (Core, 5+, Framework) but
      NOT on .NET Standard 1.x without the Microsoft.IO.FileSystem
      package; the target framework here is assumed to be .NET 6+
      (project-wide convention, NOT a per-file decision). Constructor-
      vs-static-method nuance: Dart `File('path')` is a CONSTRUCTOR
      (returns a `File` reference handle); `.readAsStringSync()` is
      called on the handle. .NET `File` is a STATIC class — there is
      NO instance to construct; `File.ReadAllText(path)` is the single
      static call. The transliteration collapses the Dart two-step
      (construct + call) into the C# one-step (static call) — a
      structural simplification, not a semantic change. Encoding
      nuance: Dart `readAsStringSync()` defaults to UTF-8 (per dart-io
      docs); .NET `File.ReadAllText(string)` ALSO defaults to UTF-8
      (per Microsoft Learn, since .NET Core 1.0+); a `.glp` source
      file is ASCII-only in this project so the encoding default agrees
      either way. Sync-vs-async nuance: this file uses `readAsStringSync`
      (synchronous, blocking) — the C# counterpart MUST be the
      synchronous `File.ReadAllText`, NOT `await File.ReadAllTextAsync`
      (which would change observable timing semantics and force an
      `async`/await chain). Path-separator nuance: Dart `'../programs/
      multiagent/social_agent_v2.glp'` uses forward slashes which both
      Dart-io and .NET-IO accept on Windows (the OS APIs normalise);
      the literal is preserved verbatim. ProcessSegregation nuance
      (Flutter-vs-CLI nuance from the doc-comment 'mimics Flutter app
      behavior'): Flutter on mobile cannot use `dart:io` File like this
      either; the Dart source is intentionally a CLI-style mimic, not
      a Flutter import. The C# port preserves the CLI-style assumption.
      Async/Stream/Future: ABSENT — sync read is sync read in both
      languages.

  - construct_key: dart.import.package_internal_eight_imports
    source_form: >-
      "import 'package:glp_runtime/runtime/runtime.dart';        // GlpRuntime
       import 'package:glp_runtime/runtime/terms.dart';          // Term, VarRef, ConstTerm
       import 'package:glp_runtime/runtime/external_io.dart';    // createExternalChannel, buildChannelTerm
       import 'package:glp_runtime/runtime/machine_state.dart';  // GoalRef, CallEnv (re-exported)
       import 'package:glp_runtime/runtime/scheduler.dart';      // Scheduler
       import 'package:glp_runtime/bytecode/runner.dart';        // BytecodeRunner, CallEnv (canonical)
       import 'package:glp_runtime/compiler/compiler.dart';      // GlpCompiler"
    target_decision: >-
      Each `package:glp_runtime/...` import maps to a C# `using`
      directive naming the namespace produced when the referenced SUT
      file is converted. Per the sibling SUT convspec decisions
      (lib/runtime/*.dart.md, lib/bytecode/runner.dart.md, lib/compiler/
      compiler.dart.md), the five `runtime/` imports collapse to a
      SINGLE `using <RootNs>.Runtime;` (all five SUT files share the
      target `<RootNs>.Runtime` namespace per their per-file convspecs);
      the `bytecode/runner.dart` import becomes `using <RootNs>.Bytecode;`;
      the `compiler/compiler.dart` import becomes `using <RootNs>.Compiler;`.
      Final three `using` directives in cu-1 below: `using <RootNs>.
      Runtime;`, `using <RootNs>.Bytecode;`, `using <RootNs>.Compiler;`.
      No `as` alias / partial import in this file — simple `using` suffices.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (KB cache hit per FR-012 / SC-007 —
      REUSE verbatim from precedent test/debug_negative.dart.md and
      test/test_channel_construction.dart.md): N same-namespace Dart
      `package:` imports collapse to 1 C# `using`. Project-file
      (assembly-reference) emission is a langpair-level concern, OUT OF
      SCOPE for this per-file artifact. Type-import nuance (explicitly
      addressed): `CallEnv` is referenced in the body and is exported
      from BOTH `bytecode/runner.dart` (canonical definition per the
      runner.dart.md convspec — `CallEnv { final Map<int, Term>
      argBySlot; }`) AND re-exported through `machine_state.dart`; the
      `using <RootNs>.Bytecode;` covers the canonical type. No
      ambiguous-reference resolution needed because the C# port collapses
      the re-export at the namespace level — both Dart imports resolve
      to the same C# type `<RootNs>.Bytecode.CallEnv` (the bytecode/
      runner.dart.md convspec is the single source of truth).

  - construct_key: dart.test_file.void_main_async_as_dart_run_entrypoint
    source_form: >-
      "void main() async {
         print('=== Testing agent_init goal setup ===\n');
         final glpSource = File('../programs/multiagent/social_agent_v2.glp').readAsStringSync();
         ...
         final result = scheduler.drainWithStatus(maxCycles: 100, debug: true, debugOutput: true);
         print('\n=== Result ===');
         print('Status: ${result.status}');
         ...
       }"
    target_decision: >-
      LOAD-BEARING DECISION (explicitly addressed): this file is NOT a
      `package:test` file — see the file-header rationale. It is a
      `dart run`-invoked diagnostic script with `void main() async`;
      the `async` keyword is present BUT NO `await` appears in the body
      (the entire body is synchronous — `readAsStringSync`, synchronous
      compiler, synchronous channel/heap allocation, synchronous
      `scheduler.drainWithStatus`). The xUnit `[Fact]` conversion shape
      used by `package:test` files is NOT APPLICABLE. The conversion
      target is the **debug_negative.dart precedent**: a single static
      C# class with a `Main` entrypoint preserving the diagnostic-script
      semantics: `public static class TestAgentInitGoal { public static
      int Main(string[] args) { ... return 0; } }` (or C# 9+ top-level
      statements — equally valid). Each Dart top-level `print(...)`
      call maps to `Console.WriteLine(...)` (NOT
      `ITestOutputHelper.WriteLine` — that target only applies inside
      `[Fact]` hosts). The Dart `async` keyword with NO `await` in the
      body is a Dart-source quirk (likely a future-proofing-for-Flutter
      vestige); the C# port DROPS the `async` keyword entirely because
      C# `static int Main(string[] args)` is the canonical sync entrypoint
      and `async Task<int> Main(string[] args)` would force the runtime
      to allocate a state machine for no benefit. Codegen MUST verify the
      body contains no `await` before dropping `async` — for THIS file
      that check passes (full-file inspection confirms zero `await`).
    idiom_id: rf-dart-debug-script-main-to-csharp-static-main
    research_finding_id: rf-dart-debug-script-main-to-csharp-static-main
    nuance: >-
      Async-without-await nuance (explicitly addressed and NEW for this
      file vs the debug_negative.dart precedent): Dart `void main()
      async { /* no await */ }` is legal — the `async` keyword wraps
      every return path in a `Future<void>`, but with no `await` the
      future completes synchronously on the next microtask. The
      observable behaviour for `dart run` is identical to a non-async
      `main` because the VM awaits the returned future before exiting.
      The C# canonical entrypoint signatures (`void Main`, `int Main`,
      `Task Main`, `Task<int> Main` — Microsoft Learn 'Main method and
      command-line arguments') do not require an `async` wrapper when
      no `await` is used. Codegen picks `int Main(string[] args)` with
      a final `return 0;` for explicit exit-code reporting. Promoting
      to `async Task<int> Main` would change the threading model
      (continuations on the thread-pool) and force the host into
      .NET's async-Main support (.NET 5+); the synchronous form is
      faithful AND avoids the async-state-machine allocation.
      Early-return-on-null-entryPC nuance: `if (entryPC == null) { ...;
      return; }` inside `main` maps to `return 1;` in the C# Main (or
      `return 0;` to preserve the silent-exit semantics — the Dart
      source uses bare `return` which yields exit code 0 by default;
      the C# port preserves that by emitting `return 0;` to keep
      observable exit-code parity, NOT `return 1;`). Discovery-model
      nuance (carry-forward): xUnit discovers via reflection over
      `[Fact]`; this file has NONE. NO `[Fact]` attribute is emitted —
      adding one would WRONGLY register a "test" that prints diagnostics
      without asserting anything, polluting the test report.

  - construct_key: dart.core.print
    source_form: >-
      "print('=== Testing agent_init goal setup ===\\n');
       print('Entry PC for agent_init/3: $entryPC');
       print('ERROR: agent_init/3 not found!');
       print('Available labels: ${combinedProgram.labels.keys.take(20)}...');
       print('\\nUser channel: $userChannel');
       print('  inputWriterAddr: ${userChannel.inputWriterAddr}');
       ... (~25 print calls total — banner, per-arg diagnostics, scheduler trace banner, result fields)"
    target_decision: >-
      LOAD-BEARING DEVIATION (REUSED from test/debug_negative.dart.md):
      because the host is `static Main` (NOT `[Fact]`), the canonical
      target is `Console.WriteLine(...)` (Microsoft Learn `https://
      learn.microsoft.com/dotnet/api/system.console.writeline`), NOT
      `ITestOutputHelper.WriteLine`. Every `print(<string>)` call maps
      to `Console.WriteLine(<string>)`; `print('')` (if any — none in
      this file) would map to `Console.WriteLine()` (no-arg overload).
      `using System;` is the only requirement (no extra dependency).
      Trailing-newline nuance: both `print(s)` and `Console.WriteLine(s)`
      append a newline; the body's literal `'\\n'` characters embedded
      INSIDE strings are preserved verbatim (the message banner
      `'=== Testing agent_init goal setup ===\\n'` thus emits TWO
      newlines: the embedded `\\n` plus the WriteLine-appended newline
      — identical observable behaviour in both languages).
    idiom_id: rf-dart-print-in-console-exe-to-console-writeline
    research_finding_id: rf-dart-print-in-console-exe-to-console-writeline
    nuance: >-
      Routing nuance (explicitly addressed, REUSED from debug_negative.
      dart.md): the routing decision for `print(...)` depends on the
      HOST shape, not on the `print` call itself. In a `[Fact]` host,
      `ITestOutputHelper.WriteLine` is correct; in a `static Main`
      console-exe host (THIS file), `Console.WriteLine` is correct.
      Both rows are FIRST-CLASS idioms; codegen selects based on the
      per-file host classification. Encoding nuance: `Console.WriteLine`
      defaults to the console's active code page on Windows; the
      strings in this file are pure ASCII so no `Console.OutputEncoding
      = Encoding.UTF8` ceremony is required. Interpolation forwarding:
      each `print` whose argument is an interpolated string MUST be
      emitted as `Console.WriteLine($"...")` with the interpolation
      construct below applied to the argument — the two constructs
      compose, not duplicate.

  - construct_key: dart.string.interpolation
    source_form: >-
      "'Entry PC for agent_init/3: $entryPC';
       'Available labels: ${combinedProgram.labels.keys.take(20)}...';
       'User channel: $userChannel';
       '  inputWriterAddr: ${userChannel.inputWriterAddr}';
       '\\nArg 0: writer=$arg0Writer, reader=$arg0Reader, value=alice';
       '  isWriterBound(arg0Writer): ${heap.isWriterBound(arg0Writer)}';
       '  isReaderBound(arg0Reader): ${heap.isReaderBound(arg0Reader)}';
       '  getReaderValue(arg0Reader): ${heap.getReaderValue(arg0Reader)}';
       '\\nUser channel term: $userChTerm';
       '  ${entry.key}: VarRef(${term.addr}), isReader=${heap.isReader(term.addr)}';
       'Status: ${result.status}';
       'Goals ran: ${result.goalsRan}';
       'Suspended goals: ${result.suspendedGoals}';
       'Blocking readers: ${result.blockingReaders}';"
    target_decision: >-
      Map Dart string interpolation `'... $name ...'` and `'... ${expr}
      ...'` to C# interpolated-string literals `$"... {Name} ..."` and
      `$"... {Expr} ..."`. The implicit `.toString()` invocation that
      Dart performs is matched by C#'s `IFormattable`/`Object.ToString()`
      invocation inside `$"..."`. Field-name PascalCasing rule: every
      camelCase identifier in an interpolation expression MUST be
      RE-EMITTED with the PascalCased property name decided by the
      OWNING SUT convspec — codegen MUST consult each owning convspec
      and apply verbatim:
      - `userChannel.inputWriterAddr` → `userChannel.InputWriterAddr`
        (per lib/runtime/external_io.dart.md);
      - `userChannel.inputReaderAddr` → `userChannel.InputReaderAddr`;
      - `userChannel.outputWriterAddr` → `userChannel.OutputWriterAddr`;
      - `userChannel.outputReaderAddr` → `userChannel.OutputReaderAddr`;
      - `heap.isWriterBound`, `heap.isReaderBound`, `heap.getReaderValue`,
        `heap.isReader` → `Heap.IsWriterBound`, `Heap.IsReaderBound`,
        `Heap.GetReaderValue`, `Heap.IsReader` (per lib/runtime/
        heap_fcp.dart.md);
      - `combinedProgram.labels.keys.take(20)` →
        `combinedProgram.Labels.Keys.Take(20)` (label map per
        lib/bytecode/runner.dart.md; `Take(int)` is LINQ — requires
        `using System.Linq;`);
      - `term.addr` → `term.Addr` (per lib/runtime/terms.dart.md);
      - `result.status`/`goalsRan`/`suspendedGoals`/`blockingReaders` →
        `result.Status`/`GoalsRan`/`SuspendedGoals`/`BlockingReaders`
        (per lib/runtime/scheduler.dart.md DrainResult);
      - `entry.key` → `entry.Key` (KeyValuePair<int, Term>.Key).
    idiom_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      Field-name-casing nuance (KB cache hit per FR-012 / SC-007 —
      REUSED from test/test_channel_construction.dart.md): the
      interpolation expression must be RE-EMITTED using the PascalCased
      property name decided by the owning SUT convspec; this is a
      per-construct duty of codegen, not a per-string one. ToString
      nuance: `$userChannel`, `$userChTerm`, `$entryPC` (int), and the
      `${result.status}` (enum value) all forward to the type's
      `ToString()` — Dart `ExternalChannel.toString()` and `Term.
      toString()` are overridden per their owning convspecs; the C#
      counterparts are overridden identically. Enum-`ToString()` for
      `ExecutionStatus` (the SUT convspec lib/runtime/scheduler.dart.md
      uses PascalCase enum members `Succeeded`/`Failed`/`Suspended` —
      and that ToString output differs from Dart's lowercase
      `succeeded`/`failed`/`suspended`); recorded as a DIAGNOSTIC-only
      DEVIATION nuance (not a load-bearing assertion since this file
      makes no assertion on the exact stringification — only prints it).
      LINQ-Take nuance: `combinedProgram.labels.keys.take(20)` requires
      `using System.Linq;` for `Enumerable.Take<TSource>(this
      IEnumerable<TSource>, int)`; the `.keys.Take(20)` chain
      materialises a lazy enumerable that is then forwarded to
      `IEnumerable<int>.ToString()` (the type's default ToString prints
      the type name, NOT the elements — Microsoft Learn `Enumerable.
      Take`). This is a DIAGNOSTIC-FIDELITY GAP between Dart and C#:
      Dart `Iterable.toString()` on `<int>.take(20)` returns `(k0, k1,
      ...)` (parenthesised); C# `IEnumerable<int>.ToString()` returns
      `"System.Linq.Enumerable+TakeIterator..."`. Codegen SHOULD
      preserve the Dart-observable format by emitting `string.Join(",
      ", combinedProgram.Labels.Keys.Take(20))` inside the interpolation
      (an explicit fidelity-preservation step, NOT mechanical). This
      is recorded as the LATENT nuance — it does not block conversion,
      it is a diagnostic-formatting decision the codegen MUST make.

  - construct_key: dart.local_var.final_inferred_type
    source_form: >-
      "final glpSource = File(...).readAsStringSync();
       final userCompiler = GlpCompiler();
       final userProgram = userCompiler.compile(glpSource);
       final combinedProgram = userProgram;
       final entryPC = combinedProgram.labels['agent_init/3'];
       final rt = GlpRuntime();
       final heap = rt.heap;
       final userChannel = createExternalChannel(heap, 'user');
       final netChannel = createExternalChannel(heap, 'net');
       final userChTerm = buildChannelTerm(userChannel);
       final netChTerm = buildChannelTerm(netChannel);
       final (arg0Writer, arg0Reader) = heap.allocateVariable();
       final (arg1Writer, arg1Reader) = heap.allocateVariable();
       final (arg2Writer, arg2Reader) = heap.allocateVariable();
       final argSlots = <int, Term>{ 0: VarRef(arg0Reader), 1: ..., 2: ... };
       final env = CallEnv(args: argSlots);
       final runner = BytecodeRunner(combinedProgram);
       final scheduler = Scheduler(rt: rt, runners: {'main': runner});
       final result = scheduler.drainWithStatus(...);"
    target_decision: >-
      Each `final <name> = <expr>;` local maps to `var <name> = <expr>;`
      in C#. Dart `final` on a local enforces single-assignment at
      compile time; C# `var` does not, but no local in this file is
      reassigned (verified by inspection), so `var` is faithful. The
      three RECORD-DESTRUCTURING `final (writer, reader) = heap.
      allocateVariable();` locals get the dedicated tuple-deconstruction
      construct below — they are NOT plain `var` locals.
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Single-assignment nuance (carry-forward, KB cache hit per FR-012
      / SC-007 — REUSED from test/test_channel_construction.dart.md and
      test/heap/binding_pointer_test.dart.md): Dart `final` blocks
      re-assignment at compile time; C# `var` does not. Codegen MUST
      verify no later statement re-assigns the local — for THIS file
      every local is single-assignment, so `var` is correct. No
      class-field rename to `readonly` arises (no instance fields in
      `static Main`). Nullable-local nuance: `final entryPC = combined
      Program.labels['agent_init/3'];` has Dart type `int?` (Map index
      lookup returns nullable); C# `var entryPC = combinedProgram.
      Labels["agent_init/3"];` over a `Dictionary<string, int>` would
      THROW `KeyNotFoundException` on miss, NOT return null — see the
      dedicated map-lookup construct below for the faithful translation
      (`TryGetValue` or `Labels.GetValueOrDefault`-style call required).

  - construct_key: dart.constructor_call.implicit_new
    source_form: >-
      "File('../programs/multiagent/social_agent_v2.glp');   // dart:io File ctor
       GlpCompiler();
       GlpRuntime();
       BytecodeRunner(combinedProgram);
       Scheduler(rt: rt, runners: {'main': runner});
       CallEnv(args: argSlots);
       VarRef(arg0Reader); VarRef(arg1Reader); VarRef(arg2Reader);
       ConstTerm('alice');
       GoalRef(100, entryPC);"
    target_decision: >-
      Dart 2+ implicit-`new` constructor calls map to C# `new T(...)`
      with identical positional ordering. Each owning SUT convspec
      decides the C# constructor signature; this artifact records only
      the call-site SHAPE:
      - `File(...)` is the ONE Dart-io constructor — collapsed to the
        static `File.ReadAllText(...)` form per the dart_io import
        construct above (NO `new File(...)` emitted);
      - `new GlpCompiler()` (no-arg, per lib/compiler/compiler.dart.md
        which decides `sealed class GlpCompiler` with all-optional
        constructor parameters);
      - `new GlpRuntime()` (per lib/runtime/runtime.dart.md);
      - `new BytecodeRunner(combinedProgram)` (per lib/bytecode/runner.
        dart.md);
      - `new Scheduler(rt, new Dictionary<string, BytecodeRunner> {
        { "main", runner } })` (per lib/runtime/scheduler.dart.md
        which decides positional ctor params in `rt`/`runners` order
        — see the named-arguments construct below for the call-site
        emission);
      - `new CallEnv(argSlots)` (per lib/bytecode/runner.dart.md which
        decides positional `args: Map<int, Term>` parameter);
      - `new VarRef(arg0Reader)` etc. (per lib/runtime/terms.dart.md);
      - `new ConstTerm("alice")` (per lib/runtime/terms.dart.md —
        single-arg constructor over `object?`);
      - `new GoalRef(100, entryPC.Value)` (per lib/runtime/machine_state.
        dart.md — positional `(int kappa, int pc)`; `entryPC.Value` is
        the null-check-narrowed access on the nullable int, see
        map-lookup construct below).
    idiom_id: rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new
    research_finding_id: rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new
    nuance: >-
      Implicit-new nuance (KB cache hit per FR-012 / SC-007 — REUSED
      from test/debug_negative.dart.md and test/test_channel_construction.
      dart.md): Dart 2+ allows omitting the `new` keyword at constructor
      call sites; C# requires `new` (target-typed `new()` is a C# 9+
      shorthand, but classic `new T(...)` is universally correct on
      the right-hand side of `var`). Cross-file authority nuance:
      codegen MUST consult each owning SUT convspec for the canonical
      C# constructor parameter list — DO NOT mechanically copy the Dart
      positional/named shape; e.g. `Scheduler(rt: rt, runners: {...})`
      Dart NAMED arguments map to a C# positional ctor where the order
      is decided by scheduler.dart.md; see the named-arguments construct
      below for the call-site emission rule.

  - construct_key: dart.tuple.record_destructuring_two_int_addresses
    source_form: >-
      "final (arg0Writer, arg0Reader) = heap.allocateVariable();
       final (arg1Writer, arg1Reader) = heap.allocateVariable();
       final (arg2Writer, arg2Reader) = heap.allocateVariable();"
    target_decision: >-
      Dart 3 positional-record-destructuring `final (a, b) = expr;` of
      a `(int, int)` record return maps to C# tuple-deconstruction
      `var (a, b) = expr;`. Per the heap_fcp.dart.md construct
      `dart.tuple_return.record_two_int_addresses_allocate_variable`
      (idiom `rf-dart-record-return-to-csharp-valuetuple`), `Heap.
      AllocateVariable()` returns a `(long writerAddr, long readerAddr)`
      `ValueTuple<long, long>` with named elements. The three
      destructuring locals therefore are `long`-typed (writer/reader
      addresses are 64-bit per the int-width-identity invariant pinned
      in cells.dart.md / heap_fcp.dart.md).
    idiom_id: rf-dart-record-return-to-csharp-valuetuple
    research_finding_id: rf-dart-record-return-to-csharp-valuetuple
    nuance: >-
      Address-width nuance (LOAD-BEARING — explicitly addressed and
      REUSED from test/heap/binding_pointer_test.dart.md): Dart `int`
      maps to C# `long` for heap addresses (per cells.dart.md construct
      `dart.int.fixed_width_identity_field`, idiom `rf-dart-int-to-
      csharp-long-width`). The three destructuring locals MUST be
      typed `long`, not `int`; the C# `var` infers `long` from the
      `AllocateVariable()` signature. Codegen MUST NOT silently narrow:
      the `argSlots[i] = new VarRef(arg<i>Reader)` callsite below
      requires `VarRef` to accept a `long` address (also per terms.
      dart.md). Tuple-element-name nuance: Dart positional records
      `(int, int)` have NO names — the destructuring binds positional
      indices; C# `var (a, b)` likewise binds positionally regardless
      of whether the returning `ValueTuple` has named elements. Both
      languages agree.

  - construct_key: dart.string.single_quoted_literal
    source_form: >-
      "'user'; 'net'; 'alice'; 'agent_init/3'; 'main';
       '../programs/multiagent/social_agent_v2.glp'"
    target_decision: >-
      Dart single-quoted string literals (no interpolation, no escapes
      in these literals) map to C# double-quoted string literals:
      `"user"`, `"net"`, `"alice"`, `"agent_init/3"`, `"main"`,
      `"../programs/multiagent/social_agent_v2.glp"`. The path-literal
      uses forward slashes which BOTH `File.ReadAllText` on Windows
      and Dart's `File` accept (OS-level normalisation). No raw-string
      or verbatim-string treatment required.
    idiom_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    research_finding_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    nuance: >-
      Quote-style nuance (KB cache hit per FR-012 / SC-007 — REUSED
      from test/test_channel_construction.dart.md): Dart accepts both
      `'...'` and `"..."` identically; C# accepts only `"..."` for
      plain strings. No escape-processing differences for these
      ASCII-only literals. Path-literal nuance (explicitly addressed):
      `'../programs/...'` is a RELATIVE path resolved against the
      Dart process's current working directory. The C# port's behaviour
      is identical (`File.ReadAllText` resolves relative paths against
      `Environment.CurrentDirectory`). For the Dart-source case (where
      this file lives in `glp_runtime/test/` and the program is at
      `../programs/multiagent/...`), the CWD assumption is that the
      script is run from `glp_runtime/`; the C# port preserves that
      assumption verbatim. Future codegen MAY want to surface this
      CWD-dependency as a comment or normalise via `Path.Combine` and
      `AppContext.BaseDirectory` — recorded as a LATENT enhancement,
      not asserted here.

  - construct_key: dart.method_call.compiler_compile
    source_form: "final userProgram = userCompiler.compile(glpSource);"
    target_decision: >-
      Dart instance-method call `compiler.compile(source)` maps to C#
      `compiler.Compile(source)` (PascalCase rename per the SUT
      convspec lib/compiler/compiler.dart.md construct
      `BytecodeProgram compile(String source, [CompileOptions? options])`).
      The method returns a `BytecodeProgram` (decided by compiler.dart.md).
      Optional `[CompileOptions? options]` is not exercised here —
      single-argument call site.
    idiom_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    research_finding_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    nuance: >-
      Naming-convention nuance (KB cache hit per FR-012 / SC-007 —
      REUSED from test/test_channel_construction.dart.md): Dart instance
      methods use camelCase; C# instance methods use PascalCase.
      Codegen MUST consult the SUT convspec for the canonical
      PascalCase name. Side-effect nuance: `Compile(source)` is a pure
      function of source (per compiler.dart.md) — no observable side
      effects, identical observable semantics in both languages.

  - construct_key: dart.map_lookup.nullable_return_from_string_keyed_map
    source_form: >-
      "final entryPC = combinedProgram.labels['agent_init/3'];
       if (entryPC == null) {
         print('ERROR: agent_init/3 not found!');
         print('Available labels: ${combinedProgram.labels.keys.take(20)}...');
         return;
       }"
    target_decision: >-
      LOAD-BEARING SEMANTIC CORRECTION (explicitly addressed): Dart
      `Map<K, V>.operator[]` returns `V?` (NULLABLE) — the missing-key
      case returns `null`. C# `Dictionary<TKey, TValue>.this[TKey]`
      THROWS `KeyNotFoundException` on miss, NOT null. The faithful
      translation MUST NOT use the indexer; use `TryGetValue` (the
      Microsoft Learn-documented null-safe lookup) OR
      `GetValueOrDefault` (C# 9+ / .NET dictionary extension method).
      Preferred form for this file: `if (!combinedProgram.Labels.
      TryGetValue("agent_init/3", out var entryPCValue)) { Console.
      WriteLine("ERROR: agent_init/3 not found!"); Console.WriteLine(
      $"Available labels: {string.Join(", ", combinedProgram.Labels.
      Keys.Take(20))}..."); return 0; } int entryPC = entryPCValue;`
      — the boolean return of `TryGetValue` is the null-check; the
      `out` variable receives the value (default `int` `0` on miss,
      but the early-return prevents use of the default). Alternative
      form (more parallel to the Dart source): `int? entryPC = combined
      Program.Labels.TryGetValue("agent_init/3", out var v) ? v :
      (int?)null; if (entryPC == null) { ...; return 0; }` — explicit
      nullable carry-over. Spec preference: the `TryGetValue` + `out`
      form because it produces the more idiomatic C# code; the explicit
      nullable form is recorded as a corroborating alternative.
    idiom_id: rf-dart-map-indexer-nullable-to-csharp-trygetvalue
    research_finding_id: rf-dart-map-indexer-nullable-to-csharp-trygetvalue
    nuance: >-
      Missing-key nuance (LOAD-BEARING and well-known footgun — explicitly
      addressed): a mechanical `var entryPC = combinedProgram.Labels[
      "agent_init/3"];` would THROW on missing key, completely changing
      observable semantics from "return null, branch on it, print
      diagnostic, return cleanly" to "uncaught exception". The
      `TryGetValue` mapping is the canonical Microsoft Learn pattern
      (`https://learn.microsoft.com/dotnet/api/system.collections.
      generic.dictionary-2.trygetvalue`). The bytecode/runner.dart.md
      convspec already exercises this idiom on `prog.labels[name]!` (see
      that file's line "Dart `prog.labels[name]!` (non-null assertion
      after lookup)") — the same idiom resolves THIS file's nullable
      lookup. NRT-vs-Dart-NNBD nuance: under enabled NRT the C# `out`
      variable is non-null-after-true-return; under Dart NNBD the
      `entryPC` local is `int?` and requires explicit null-check. Both
      languages enforce the safety at the type-system level. Type
      nuance: the map value type is `int` in Dart (per the labels-Map
      decision in compiler.dart.md / runner.dart.md). Per the address-
      width rule it could be `long`, but `Pc` is decided as `int` in
      runner.dart.md (program counter, not heap address) — codegen
      MUST consult that convspec; the destructured `var` infers
      whatever it returns.

  - construct_key: dart.member_access.field_chain
    source_form: >-
      "rt.heap;
       userChannel.inputWriterAddr; .inputReaderAddr; .outputWriterAddr; .outputReaderAddr;
       term.addr; entry.key; entry.value;
       result.status; .goalsRan; .suspendedGoals; .blockingReaders;
       combinedProgram.labels;
       rt.gq;"
    target_decision: >-
      Dart camelCase field/property reads map to C# PascalCase property
      reads per the owning SUT convspecs:
      - `rt.heap` → `rt.Heap` (per lib/runtime/runtime.dart.md);
      - `rt.gq` → `rt.Gq` OR `rt.GoalQueue` (the runtime.dart.md
        convspec is the authority — codegen MUST consult that
        artifact's chosen identifier; conservative emission uses the
        Dart name `Gq` PascalCased verbatim);
      - `userChannel.input{Writer,Reader}Addr`, `output{Writer,Reader}Addr`
        → `userChannel.Input{Writer,Reader}Addr` etc. (per lib/runtime/
        external_io.dart.md);
      - `term.addr` → `term.Addr` (per lib/runtime/terms.dart.md);
      - `entry.key`, `entry.value` → `entry.Key`, `entry.Value`
        (`KeyValuePair<int, Term>` — see map-iteration construct);
      - `result.status`, `result.goalsRan`, `result.suspendedGoals`,
        `result.blockingReaders` → `result.Status`, `result.GoalsRan`,
        `result.SuspendedGoals`, `result.BlockingReaders` (per
        lib/runtime/scheduler.dart.md DrainResult);
      - `combinedProgram.labels` → `combinedProgram.Labels` (per
        lib/bytecode/runner.dart.md `BytecodeProgram`).
    idiom_id: rf-dart-camelcase-field-to-csharp-pascalcase-property
    research_finding_id: rf-dart-camelcase-field-to-csharp-pascalcase-property
    nuance: >-
      Field-vs-property nuance (KB cache hit per FR-012 / SC-007 —
      REUSED from test/test_channel_construction.dart.md): in Dart,
      class members declared `final T x;` are fields with implicit
      getters; in C# the idiomatic translation is an auto-property
      `public T X { get; }`. The READ-side syntax (`obj.X`) is
      identical; this idiom records ONLY the naming rename. Codegen
      MUST consult each owning SUT convspec for the canonical target
      identifier (do NOT mechanically PascalCase if the SUT has decided
      a non-mechanical rename, e.g. `gq` → `GoalQueue` instead of
      `Gq`). For THIS file, conservative emission preserves the Dart
      identifier shape and applies PascalCase; final identifiers are
      bound by the SUT convspecs.

  - construct_key: dart.method_call.heap_query_returning_bool_or_term_or_unit
    source_form: >-
      "heap.bindVariable(arg0Writer, ConstTerm('alice'));
       heap.bindVariable(arg1Writer, userChTerm);
       heap.bindVariable(arg2Writer, netChTerm);
       heap.isWriterBound(arg0Writer);    // bool
       heap.isReaderBound(arg0Reader);    // bool
       heap.getReaderValue(arg0Reader);   // Term?
       heap.isReader(term.addr);          // bool"
    target_decision: >-
      Dart instance method calls on `HeapFCP` map to PascalCase methods
      on the converted C# `HeapFCP` type per lib/runtime/heap_fcp.dart.md:
      - `bindVariable(addr, term)` → `BindVariable(addr, term)`
        returning `List<SuspensionRecord> activations` (per heap_fcp.
        dart.md — the return value is DISCARDED at this Dart call
        site; the C# port may keep the discard `var _ = Heap.Bind
        Variable(...);` or simply call as an expression-statement);
      - `isWriterBound(addr)` → `IsWriterBound(addr)` returning `bool`;
      - `isReaderBound(addr)` → `IsReaderBound(addr)` returning `bool`;
      - `getReaderValue(addr)` → `GetReaderValue(addr)` returning `Term?`;
      - `isReader(addr)` → `IsReader(addr)` returning `bool`.
      Return-value-discard nuance: the three `heap.bindVariable(...)`
      calls do not capture their return value; the C# port emits each
      as a statement-expression `Heap.BindVariable(...);` — the
      activation list goes uncollected (the SCHEDULER is expected to
      drain the same activations via `gq` on next drain step; the
      Dart source's discard preserves that contract).
    idiom_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    research_finding_id: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods
    nuance: >-
      Naming + return-discard nuance (KB cache hit per FR-012 / SC-007
      — REUSED from test/heap/binding_pointer_test.dart.md construct
      `dart.method_call.heap_mutator_void_or_returning_activations`):
      `BindVariable` returns `List<SuspensionRecord> activations`; the
      Dart source DISCARDS the return value at these three call sites
      (the activation list is intentionally not propagated because this
      diagnostic script does not exercise the activation-reactivation
      path — the scheduler runs a separate drain). The C# port MUST
      preserve the discard (statement-expression form), NOT capture
      into an unused local. Nullable-return nuance: `GetReaderValue`
      returns `Term?` — the call site interpolates the result into a
      diagnostic print, which exercises the nullable's `ToString()`
      (returns `"null"` for a null `Term?`). C# `$"...{nullable}..."`
      handles `null` by emitting an empty string by default (not
      `"null"`); for diagnostic-fidelity codegen MAY emit `$"...{readerValue
      ?? (object)\"null\"}..."` to preserve the Dart-print shape — this
      is a DIAGNOSTIC-FORMAT decision recorded as a latent nuance, not
      a load-bearing assertion (the file makes no assertion on the
      print's exact text). Side-effect ordering nuance: Dart and C#
      both guarantee left-to-right argument evaluation, so the
      multi-arg method calls preserve observable order verbatim.

  - construct_key: dart.function_call.top_level_external_io_helpers
    source_form: >-
      "createExternalChannel(heap, 'user');
       createExternalChannel(heap, 'net');
       buildChannelTerm(userChannel);
       buildChannelTerm(netChannel);"
    target_decision: >-
      Dart top-level functions map to C# `public static` methods on
      a designated host class per the SUT convspec lib/runtime/external_io.
      dart.md. Per test/test_channel_construction.dart.md's matching
      construct, the host class is `ExternalIo` (the precise name
      decided by external_io.dart.md). Call sites become
      `ExternalIo.CreateExternalChannel(heap, "user")`,
      `ExternalIo.CreateExternalChannel(heap, "net")`,
      `ExternalIo.BuildChannelTerm(userChannel)`,
      `ExternalIo.BuildChannelTerm(netChannel)`. Codegen MUST consult
      external_io.dart.md for the host class name and apply verbatim
      for cross-file consistency with test_channel_construction.dart.md.
    idiom_id: rf-dart-top-level-function-callsite-to-csharp-static-method
    research_finding_id: rf-dart-top-level-function-callsite-to-csharp-static-method
    nuance: >-
      Top-level-function nuance (KB cache hit per FR-012 / SC-007 —
      REUSED from test/test_channel_construction.dart.md and test/
      debug_negative.dart.md): Dart permits file-level functions; C#
      does not — every method must live on a type. The host-class
      decision is made ONCE in the SUT convspec and applied UNIFORMLY
      at every call site (this file's four call sites match the two
      in test_channel_construction.dart.md so the same `ExternalIo`
      host is used). String-literal argument nuance: `'user'`/`'net'`
      are channel-name tags; their semantics in `createExternalChannel`
      are decided by external_io.dart.md (the Dart side stores them
      on the returned `ExternalChannel`).

  - construct_key: dart.named_argument.constructor_or_method_call
    source_form: >-
      "CallEnv(args: argSlots);
       Scheduler(rt: rt, runners: {'main': runner});
       scheduler.drainWithStatus(maxCycles: 100, debug: true, debugOutput: true);"
    target_decision: >-
      Dart named arguments at constructor/method call sites map to C#
      positional arguments where the parameter order is decided by the
      owning SUT convspec — OR to C# named-argument syntax (`paramName:
      value`) for readability. C# supports named arguments at any call
      site since C# 4.0 (Microsoft Learn 'Named and optional arguments').
      Concrete emissions:
      - `CallEnv(args: argSlots)` → `new CallEnv(argSlots)` (positional;
        bytecode/runner.dart.md decides single-param ctor) OR `new
        CallEnv(args: argSlots)` (named, more readable; equivalent);
      - `Scheduler(rt: rt, runners: {'main': runner})` → `new
        Scheduler(rt, new Dictionary<string, BytecodeRunner> { { "main",
        runner } })` (positional, per scheduler.dart.md declared ctor
        order `(GlpRuntime rt, IDictionary<string, BytecodeRunner>
        runners)`) OR the equivalent named-arg form `new Scheduler(rt:
        rt, runners: new Dictionary<string, BytecodeRunner> { { "main",
        runner } })`;
      - `scheduler.drainWithStatus(maxCycles: 100, debug: true,
        debugOutput: true)` → `scheduler.DrainWithStatus(maxCycles:
        100, debug: true, debugOutput: true)` (per scheduler.dart.md
        which decides the parameter names + types; named-arg call
        site preserved verbatim for readability).
      Spec preference: EMIT NAMED-ARGUMENT SYNTAX for `Scheduler` and
      `drainWithStatus` (the parameter names carry meaning the
      positional form would lose); EMIT POSITIONAL for `CallEnv` (a
      single-param ctor where naming adds no clarity).
    idiom_id: rf-dart-named-arguments-to-csharp-named-arguments-or-positional
    research_finding_id: rf-dart-named-arguments-to-csharp-named-arguments-or-positional
    nuance: >-
      Named-argument nuance (explicitly addressed and LOAD-BEARING):
      Dart named arguments are part of the parameter list at the
      declaration site (`Scheduler({required GlpRuntime rt, required
      Map<String, BytecodeRunner> runners})`); C# named arguments are
      a CALL-SITE convenience over ordinary positional parameters
      (Microsoft Learn 'Named and optional arguments'). Both languages
      bind by NAME at the call site, but the underlying mechanism
      differs: Dart REQUIRES the name at the call site if the
      parameter is declared named-required; C# allows EITHER positional
      OR named at the call site freely. The fidelity-preferred shape
      is named-argument syntax at every C# call site that uses Dart
      named arguments, because (a) it preserves the Dart-source
      reader-clue about parameter intent and (b) it survives future
      parameter-reordering refactors. NOT `init`-only properties /
      object-initialiser syntax (`new Scheduler { Rt = rt, Runners =
      ... }`) — that would force the SUT ctor to expose `init`-only
      properties, changing the immutability/identity contract of
      `Scheduler` (recorded in scheduler.dart.md as a reference-identity
      mutable container — see file-header nuance (a) of that convspec).
      Map-literal nuance: `{'main': runner}` is the Dart map literal
      passed as the `runners` argument; mapped to `new Dictionary<
      string, BytecodeRunner> { { "main", runner } }` (C# collection-
      initialiser syntax for IDictionary; alternative `new Dictionary<
      string, BytecodeRunner> { ["main"] = runner }` is equally valid
      C# 6+ syntax). Codegen picks whichever matches scheduler.dart.md's
      declared parameter type.

  - construct_key: dart.map_literal.typed_int_term_with_constructor_calls
    source_form: >-
      "final argSlots = <int, Term>{
         0: VarRef(arg0Reader),
         1: VarRef(arg1Reader),
         2: VarRef(arg2Reader),
       };"
    target_decision: >-
      Dart typed map literal `<int, Term>{0: ..., 1: ..., 2: ...}` maps
      to C# `Dictionary<int, Term>` collection-initialiser syntax:
      `var argSlots = new Dictionary<int, Term> { { 0, new VarRef(arg0
      Reader) }, { 1, new VarRef(arg1Reader) }, { 2, new VarRef(arg2
      Reader) } };` OR the C# 6+ index-initialiser form `new
      Dictionary<int, Term> { [0] = new VarRef(arg0Reader), [1] = new
      VarRef(arg1Reader), [2] = new VarRef(arg2Reader) }`. Either is
      idiomatic; codegen picks the dictionary-collection-initialiser
      form `{ { K, V }, ... }` to mirror the Dart `{K: V, ...}` shape
      more visually. The value type `Term` (per terms.dart.md sum-type
      hierarchy) is the static element type; the `VarRef` constructor
      calls return `VarRef` (a `Term` subtype) and box up transparently.
    idiom_id: rf-dart-typed-map-literal-to-csharp-dictionary-collection-init
    research_finding_id: rf-dart-typed-map-literal-to-csharp-dictionary-collection-init
    nuance: >-
      Type-parameter-explicitness nuance (explicitly addressed): Dart
      `<int, Term>{0: VarRef(...)}` has the type parameters explicit;
      omitting them (just `{0: VarRef(...)}`) would let Dart infer.
      C# `new Dictionary<int, Term> { ... }` is similarly explicit; C#
      9+ `new() { ... }` target-typed form is also valid when the LHS
      type is given. Codegen prefers explicit type parameters to
      preserve the Dart-source reader-clue. Key/value type widths
      nuance: Dart `int` literal keys (`0`, `1`, `2`) map to C# `int`
      (NOT `long`) because the map's key type is the slot-index, NOT a
      heap address. The address-width-`long` rule applies only to heap
      addresses (per cells.dart.md construct
      `dart.int.fixed_width_identity_field`). The `VarRef` constructor
      takes a `long` reader-addr argument (decoded from the `arg<i>
      Reader` destructured locals which are `long`); the Dictionary
      key type is `int` (slot index — bounded small integer). This
      is a LOAD-BEARING TYPE-WIDTH SPLIT inside the SAME data structure
      and is recorded explicitly. Collection-initialiser-syntax-flavour
      nuance: both `{ { K, V }, ... }` and `{ [K] = V, ... }` produce
      identical `Dictionary<TKey, TValue>` instances; codegen picks
      the curly-pair form for visual proximity to Dart `{K: V}`.

  - construct_key: dart.method_call.set_goal_env_and_program_on_runtime
    source_form: >-
      "rt.setGoalEnv(100, env);
       rt.setGoalProgram(100, 'main');"
    target_decision: >-
      Dart instance-method calls on `GlpRuntime` map to PascalCase
      methods per lib/runtime/runtime.dart.md (which decides the
      method names `SetGoalEnv` and `SetGoalProgram` and their
      parameter types). C# emissions: `rt.SetGoalEnv(100, env);`
      `rt.SetGoalProgram(100, "main");`. The `100` is the per-goal
      id (the Dart source uses 100 as the agent_init goal id); the
      `'main'` is the program name (used by the scheduler to look
      up the per-program runner in the map).
    idiom_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    research_finding_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    nuance: >-
      Side-effect nuance (carry-forward): both `SetGoalEnv` and
      `SetGoalProgram` mutate the runtime's per-goal tracking maps
      (per runtime.dart.md). Identical observable behaviour in both
      languages. Parameter-name nuance: `(int goalId, CallEnv env)`
      and `(int goalId, string programName)` — the goal-id is `int`
      (not `long`; goal-ids are bounded counter values, distinct from
      heap-address widths per runtime.dart.md).

  - construct_key: dart.method_call.gq_enqueue_goalref
    source_form: "rt.gq.enqueue(GoalRef(100, entryPC));"
    target_decision: >-
      Dart `rt.gq.enqueue(GoalRef(100, entryPC))` maps to C# `rt.Gq.
      Enqueue(new GoalRef(100, entryPC.Value));` (or `entryPC` directly
      after the `TryGetValue` out-binding narrowed it to non-nullable
      `int` — see map-lookup construct). `GoalRef` ctor: positional
      `(int kappa, int pc)` per machine_state.dart.md. The `gq`
      property on `GlpRuntime` returns the goal queue per runtime.dart.md;
      `enqueue(GoalRef)` is a documented method on `GoalQueue` per
      goal_queue.dart.md.
    idiom_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    research_finding_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    nuance: >-
      Nullable-int-unwrap nuance (cross-construct, references the
      map-lookup construct): the `entryPC` local arrived from the
      nullable map-indexer above. After the early-return on null, the
      Dart flow-narrows `entryPC` to non-null in the remainder of the
      function; C# `TryGetValue` likewise narrows via the `out` local.
      The `GoalRef` constructor's second argument is therefore
      non-nullable `int` in BOTH languages — no `.Value` access needed
      if the `TryGetValue` `out var` form is used; if the explicit-
      nullable form was chosen, `entryPC.Value` (or `entryPC!.Value`
      under NRT) is required.

  - construct_key: dart.foreach.iterate_map_entries_with_destructure_print
    source_form: >-
      "print('\\nArgSlots:');
       for (final entry in argSlots.entries) {
         final term = entry.value;
         if (term is VarRef) {
           print('  ${entry.key}: VarRef(${term.addr}), isReader=${heap.isReader(term.addr)}');
         }
       }"
    target_decision: >-
      Dart `Map<K,V>.entries` returns `Iterable<MapEntry<K,V>>` with
      `entry.key` / `entry.value`; C# `Dictionary<TKey, TValue>` is
      enumerable as `IEnumerable<KeyValuePair<TKey, TValue>>` with
      `entry.Key` / `entry.Value`. The Dart `for (final entry in
      argSlots.entries)` maps to C# `foreach (var entry in argSlots)`
      — the `.entries` ceremony is DROPPED because C# `Dictionary<K,V>`
      itself iterates as `KeyValuePair<K,V>`. The Dart `if (term is
      VarRef)` type-test with flow-narrowing maps to the C# type-
      pattern `if (term is VarRef varRef)` declaring a new narrowed
      local — `term.addr` inside the branch becomes `varRef.Addr`
      (PascalCase per terms.dart.md).
    idiom_id: rf-dart-map-entries-iteration-to-csharp-dictionary-foreach
    research_finding_id: rf-dart-map-entries-iteration-to-csharp-dictionary-foreach
    nuance: >-
      Iteration-shape nuance (explicitly addressed): Dart REQUIRES the
      `.entries` member to iterate K/V pairs (iterating a `Map`
      directly yields nothing in Dart 2+); C# `Dictionary<K,V>` IS
      `IEnumerable<KeyValuePair<K,V>>` natively. The C# code DROPS
      the `.entries` selector — emitting `argSlots.Entries` would not
      compile (no such member on `Dictionary<TKey, TValue>`). Type-
      pattern nuance (KB cache hit — REUSED from lib/multiagent/
      mad_context.dart.md construct
      `dart.method.private_recursive_termvar_extract_pattern_matching_
      on_term_subclasses`): Dart `if (x is T)` flow-narrows `x`
      inside the branch; C# `if (x is T t)` declares a new narrowed
      local. The branch body inside this file is a single print —
      simple enough that the C# `is T t` form is the faithful and
      tighter target. Key-vs-Value casing nuance: `entry.key` /
      `entry.value` → `entry.Key` / `entry.Value` (the `KeyValuePair<,
      >` struct's public properties are PascalCase). NO `_` discard /
      destructuring used in this file; codegen does NOT need to
      handle deconstruction.

  - construct_key: dart.var_loop_local
    source_form: "for (final entry in argSlots.entries) { final term = entry.value; ... }"
    target_decision: >-
      The two `final` LOCALS inside the foreach body (`entry` from the
      loop pattern, `term` from `entry.value`) both map to C# `var`
      — `foreach (var entry in argSlots) { var term = entry.Value;
      ... }`. Per the dart.local_var.final_inferred_type construct
      above; no separate idiom needed.
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Loop-local nuance: foreach iteration variable in BOTH Dart and
      C# is per-iteration-fresh (C# 5+ semantics); `final` on the
      loop variable is the Dart convention for emphasising no-reassign
      inside the body. C# `var` carries the same convention by
      lack-of-reassignment; no `readonly`/`in` annotation is required
      (C# `foreach (in var entry in ...)` is for ref-readonly value-
      type iteration — NOT applicable here).

  - construct_key: dart.scheduler.drain_with_status_named_args_synchronous
    source_form: >-
      "final result = scheduler.drainWithStatus(
         maxCycles: 100,
         debug: true,
         debugOutput: true,
       );"
    target_decision: >-
      Dart `scheduler.drainWithStatus(maxCycles, debug, debugOutput)`
      maps to C# `scheduler.DrainWithStatus(maxCycles: 100, debug:
      true, debugOutput: true)` — preserving the named-argument form
      at the call site per the dart.named_argument construct above.
      The Dart return type is `DrainResult` (synchronous — NOT
      `Future<DrainResult>`; the async variant is the SEPARATE
      `drainAsyncWithStatus` per scheduler.dart.md (i) — only async
      surface; not exercised here). The C# return type is the
      `DrainResult` reference class decided by scheduler.dart.md.
    idiom_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    research_finding_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    nuance: >-
      Sync-vs-async nuance (explicitly addressed and LOAD-BEARING per
      scheduler.dart.md): `drainWithStatus` is SYNCHRONOUS; the only
      async variant is `drainAsyncWithStatus`. THIS file uses the
      sync variant — the C# port MUST emit the sync `DrainWithStatus`
      call, NOT `await scheduler.DrainWithStatusAsync(...)`. The
      `async`-without-`await` on `main` does NOT change this (see
      the void_main construct above). Diagnostic-trace-flag nuance:
      `debug: true` and `debugOutput: true` enable scheduler trace
      output to stdout via the scheduler's trace sink (per scheduler.
      dart.md (g) trace-formatting helpers); the C# port preserves
      both as named bool args. Threading-model inheritance nuance
      (LOAD-BEARING, inherited from heap_fcp.dart.md escalations[0]):
      the scheduler.dart.md convspec records the single-owning-context
      invariant; this file's `Main` invocation runs on the process's
      main thread, which IS the owning context for the single-agent
      diagnostic — no thread-marshalling required. If a future
      diagnostic test were to introduce multi-agent setup, that
      threading-model decision is inherited (NOT re-escalated here).

  - construct_key: dart.early_return.bare_return_in_void_main
    source_form: >-
      "if (entryPC == null) {
         print('ERROR: agent_init/3 not found!');
         print('Available labels: ${combinedProgram.labels.keys.take(20)}...');
         return;
       }"
    target_decision: >-
      Dart `return;` inside `void main()` exits the function and
      yields exit code 0. C# `static int Main` requires an explicit
      `return <int>;` — codegen emits `return 0;` to preserve the
      Dart-source's clean-exit semantics (the diagnostic-error early
      exit is NOT a hard failure on the Dart side; it just stops
      execution and lets the VM exit with code 0). If a future
      enhancement wants exit code 1 for the missing-label case, that
      would be a SPEC change; for THIS file the faithful translation
      preserves exit code 0.
    idiom_id: rf-dart-void-main-bare-return-to-csharp-int-main-return-zero
    research_finding_id: rf-dart-void-main-bare-return-to-csharp-int-main-return-zero
    nuance: >-
      Exit-code nuance (explicitly addressed): Dart `void main()`
      with bare `return;` exits with code 0 (per Dart runtime
      behaviour); C# `int Main()` requires an explicit int return.
      The faithful mapping preserves observable exit code 0. If the
      Dart source had been `void main() { ... return; } /* but with
      stderr.writeln */` or `exit(1)` from dart:io, the C# port
      would emit `return 1;` — neither pattern appears here. Final
      method-end nuance: the Dart `main` body's natural fall-through
      at the end of the function (after the final `print('Blocking
      readers: ...');`) maps to C# `return 0;` at the bottom of
      `Main`. Codegen emits BOTH the mid-function `return 0;` (for
      the early-return) AND the final `return 0;` (for the
      fall-through).

conversion_units:
  - "cu-1: file-scope using directives (System + System.IO + System.Linq + Xunit-NOT-needed + the three SUT namespaces decided by lib/runtime/*.dart.md, lib/bytecode/runner.dart.md, lib/compiler/compiler.dart.md)"
  - "cu-2: namespace declaration mirroring the test/ path (e.g. <RootNs>.Test) — no group nesting in this file"
  - "cu-3: file-header XML doc-comment '/// Test to debug agent_init goal setup - mimics Flutter app behavior' above the host static class"
  - "cu-4: top-level `public static class TestAgentInitGoal` host (the debug-script idiom — NO test class, NO [Fact] attribute, NO ITestOutputHelper injection, NO constructor)"
  - "cu-5: `public static int Main(string[] args)` entrypoint — synchronous (NO async keyword, since the Dart `async` carries no await)"
  - "cu-6: Main body — banner Console.WriteLine, File.ReadAllText for the .glp source, new GlpCompiler() / Compile call, label TryGetValue + early `return 0;`-on-miss branch"
  - "cu-7: new GlpRuntime() + var heap = rt.Heap, two ExternalIo.CreateExternalChannel(heap, \"user\"|\"net\") calls, two ExternalIo.BuildChannelTerm(...) calls, six Console.WriteLine diagnostics with interpolation"
  - "cu-8: three tuple-deconstructing var (arg<i>Writer, arg<i>Reader) = heap.AllocateVariable() lines + three Heap.BindVariable(arg<i>Writer, <constOrChannelTerm>) statement-expressions + per-arg trio of Console.WriteLine diagnostics with IsWriterBound/IsReaderBound/GetReaderValue interpolations"
  - "cu-9: var argSlots = new Dictionary<int, Term> { { 0, new VarRef(arg0Reader) }, { 1, new VarRef(arg1Reader) }, { 2, new VarRef(arg2Reader) } };"
  - "cu-10: foreach (var entry in argSlots) { var term = entry.Value; if (term is VarRef varRef) { Console.WriteLine($\"  {entry.Key}: VarRef({varRef.Addr}), isReader={Heap.IsReader(varRef.Addr)}\"); } }"
  - "cu-11: var env = new CallEnv(argSlots); rt.SetGoalEnv(100, env); rt.SetGoalProgram(100, \"main\");"
  - "cu-12: var runner = new BytecodeRunner(combinedProgram); var scheduler = new Scheduler(rt: rt, runners: new Dictionary<string, BytecodeRunner> { { \"main\", runner } });"
  - "cu-13: rt.Gq.Enqueue(new GoalRef(100, entryPC));    // entryPC narrowed to non-null int by the earlier TryGetValue"
  - "cu-14: Console.WriteLine(\"\\n=== Running goal ===\"); var result = scheduler.DrainWithStatus(maxCycles: 100, debug: true, debugOutput: true);"
  - "cu-15: Console.WriteLine(\"\\n=== Result ===\"); 4× result-field diagnostic Console.WriteLines (Status / GoalsRan / SuspendedGoals / BlockingReaders); final `return 0;`"

escalations: []
```

## Rationale + research provenance

### Why static-Main console-exe (not [Fact]) — host-shape decision (KB cache hit)

This file is NOT a `package:test` file: no `package:test` import, no
`test(...)`, no `group(...)`, no `expect(...)`, no matchers. It is a
`dart run`-invoked diagnostic script. The xUnit `[Fact]`-conversion
shape applied to every `package:test` file in this inventory is NOT
APPLICABLE. The conversion target is the **debug_negative.dart
precedent**: `public static class TestAgentInitGoal { public static
int Main(string[] args) { ... } }`. The single new wrinkle is the
Dart source's `async` keyword on `main()` with NO `await` in the
body — the C# port DROPS the keyword (no async-state-machine
allocation, no continuation contract). Reused via the cached idiom
`rf-dart-debug-script-main-to-csharp-static-main`; no re-research per
FR-024. Authoritative basis: Microsoft Learn 'Main method and
command-line arguments'
(`https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/main-command-line`)
and Dart 'Hello, World!' on `dart.dev` (`https://dart.dev/language#hello-world`).

### Console.WriteLine, not ITestOutputHelper (host-shape-conditional)

Reused from `test/debug_negative.dart.md` via the idiom
`rf-dart-print-in-console-exe-to-console-writeline`. The routing of
`print(...)` depends on the host shape, not the call: `[Fact]` host
→ `ITestOutputHelper.WriteLine`; `static Main` host → `Console.WriteLine`.
THIS file is the second `[Fact]`-less file in the inventory; the idiom
is fully decided and reused verbatim. Authoritative basis: Microsoft
Learn `Console.WriteLine`
(`https://learn.microsoft.com/dotnet/api/system.console.writeline`).

### File.ReadAllText for dart:io File('path').readAsStringSync() (NEW idiom row)

This is the first file in the inventory to exercise `dart:io File(...).
readAsStringSync()`. The faithful counterpart is C# `File.ReadAllText
(string path)` — the .NET-canonical synchronous file-text reader
(Microsoft Learn `https://learn.microsoft.com/dotnet/api/system.io.file.readalltext`).
The Dart two-step (construct `File` handle + call `readAsStringSync`)
collapses into the C# one-step (static `File.ReadAllText` call) because
.NET `File` is a STATIC class with no instance to construct. Encoding
default (UTF-8) agrees between Dart and .NET Core 1.0+. Sync-vs-async
preserved (no `await ReadAllTextAsync` introduced).

### TryGetValue for nullable Map indexer (LOAD-BEARING semantic correction)

This is the first file in the inventory to exercise the
Dart-`Map<K,V>.operator[]`-returns-nullable / C#-`Dictionary[K]`-throws
mismatch. Dart `combinedProgram.labels['agent_init/3']` returns
`int?`; mechanically transliterating to `combinedProgram.Labels[
"agent_init/3"]` would THROW `KeyNotFoundException` on miss instead of
returning null and entering the early-return branch. The faithful
translation uses `Dictionary<TKey, TValue>.TryGetValue` (Microsoft Learn
`https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.trygetvalue`)
which both null-checks AND null-narrows in a single call. Recorded as
the NEW idiom `rf-dart-map-indexer-nullable-to-csharp-trygetvalue`;
will become a KB cache hit for subsequent files that exercise the
same pattern. The bytecode/runner.dart.md convspec uses the
`!`-after-lookup variant (`prog.labels[name]!` — bang-assert,
guaranteed-present case); THIS file uses the `?`-aware variant
(branch on null). Both reduce to `TryGetValue` in C#.

### Record-destructuring + address-width long (carry-forward)

Three `final (writer, reader) = heap.allocateVariable();` lines —
identical idiom to the binding_pointer_test precedent. Per heap_fcp.
dart.md the return is `(long, long)`; the destructured locals are
`long`. The `VarRef` constructor accepts the `long` address; the
`Dictionary<int, Term>` argSlots map has `int` keys (slot indices) —
LOAD-BEARING TYPE-WIDTH SPLIT recorded explicitly. Reused via
`rf-dart-record-return-to-csharp-valuetuple`; no re-research.

### Named-argument syntax preserved at C# call sites

`Scheduler(rt: rt, runners: {...})` and `scheduler.drainWithStatus
(maxCycles: 100, debug: true, debugOutput: true)` use Dart named
arguments. C# named arguments are a call-site convenience over
positional parameters (Microsoft Learn 'Named and optional arguments'
— `https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`).
Recording NEW idiom `rf-dart-named-arguments-to-csharp-named-arguments-or-positional`
captures the spec preference: emit named-arg syntax where the parameter
names carry meaning (Scheduler, DrainWithStatus); emit positional
where naming adds no clarity (CallEnv single-param).

### Map<K, V>.entries → Dictionary<K, V> direct foreach

Dart REQUIRES `.entries` to iterate K/V pairs; C# `Dictionary<TKey,
TValue>` IS `IEnumerable<KeyValuePair<TKey, TValue>>` natively. The
C# foreach DROPS the `.entries` selector. Recording NEW idiom
`rf-dart-map-entries-iteration-to-csharp-dictionary-foreach`.
Authoritative basis: Microsoft Learn `Dictionary<TKey, TValue>.
GetEnumerator` (`https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.getenumerator`)
and `KeyValuePair<TKey, TValue>` shape.

### Type-test flow-narrowing → C# is-pattern with capture

Reused from `lib/multiagent/mad_context.dart.md`'s recursive
TermVar extraction precedent and from `lib/runtime/external_io.dart.md`'s
file-header nuance (d) — Dart `if (x is T)` narrows; C# `if (x is T
t)` declares a new narrowed local. The lone callsite here (`if (term
is VarRef)` inside the argSlots foreach) is trivially transliterated
to `if (term is VarRef varRef)`. Reused via the existing idiom
recorded in mad_context.dart.md; no re-research per FR-012/SC-007.

### Inherited multiagent threading-model escalation (FR-013)

The `Scheduler.DrainWithStatus` call exercises the scheduler →
runner → heap chain. `scheduler.dart.md` records the single-owning-
context invariant inherited from `heap_fcp.dart.md` escalations[0]
(the threading-model escalation). Per FR-013 and the sibling-multiagent
precedent (mad_context.dart.md / global_send.dart.md / message_queue.
dart.md / scheduler.dart.md all INHERIT without re-escalating), THIS
file ALSO inherits — `Main` runs on the process's main thread which
IS the owning context for the single-agent diagnostic. No genuinely-
LOCAL undecidable point. `escalations: []`.

### Why no escalations

Every construct has a clear, single-decision target shape grounded in
official Dart and .NET/C# documentation. The five idiom-KB-cache hits
(`rf-dart-internal-package-import-to-csharp-using`,
`rf-dart-debug-script-main-to-csharp-static-main`,
`rf-dart-print-in-console-exe-to-console-writeline`,
`rf-dart-final-local-to-csharp-var-local`,
`rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`,
`rf-dart-record-return-to-csharp-valuetuple`,
`rf-dart-single-quoted-string-to-csharp-double-quoted-string`,
`rf-dart-string-interpolation-to-csharp-interpolated-string`,
`rf-dart-instance-method-camelcase-to-csharp-pascalcase`,
`rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods`,
`rf-dart-top-level-function-callsite-to-csharp-static-method`,
`rf-dart-camelcase-field-to-csharp-pascalcase-property`,
`rf-dart-tripleslash-doc-to-csharp-xml-doc`)
are stable project-wide pins, not unresolved choices. The four NEW
idioms introduced by this file
(`rf-dart-dart-io-file-readasstringsync-to-system-io-file-readalltext`,
`rf-dart-map-indexer-nullable-to-csharp-trygetvalue`,
`rf-dart-named-arguments-to-csharp-named-arguments-or-positional`,
`rf-dart-typed-map-literal-to-csharp-dictionary-collection-init`,
`rf-dart-map-entries-iteration-to-csharp-dictionary-foreach`,
`rf-dart-void-main-bare-return-to-csharp-int-main-return-zero`)
each have a single authoritative target shape from official docs
(Microsoft Learn for `File.ReadAllText` / `Dictionary.TryGetValue` /
named-and-optional-arguments / collection-initialiser / `KeyValuePair`)
and will become KB cache hits for subsequent files that exercise the
same patterns. The threading-model question that COULD have been an
escalation is INHERITED (not re-escalated) per FR-013 + the documented
sibling-multiagent precedent. `escalations: []` is therefore
intentional, not a placeholder.

## Notes

- Latent codegen-fidelity nuances NOT asserted as load-bearing
  (recorded for completeness):
  (a) `combinedProgram.Labels.Keys.Take(20)`'s `IEnumerable.ToString()`
  differs between Dart (`(k0, k1, ...)`) and C# (`"System.Linq.
  Enumerable+TakeIterator..."`); codegen MAY emit `string.Join(", ",
  ...)` inside the interpolation to preserve Dart-print fidelity.
  (b) `Heap.GetReaderValue` returns `Term?`; C# `$"...{nullable}..."`
  emits empty string on null vs Dart's `"null"`; codegen MAY emit
  `nullable?.ToString() ?? "null"` to preserve fidelity.
  (c) `ExecutionStatus` enum stringification: Dart `succeeded`
  (lowercase) vs C# `Succeeded` (PascalCase per scheduler.dart.md);
  this file's print is diagnostic-only, no assertion depends on it.
- The Dart source's `async` keyword on `main()` with NO `await` in
  the body is a quirk: it gets DROPPED in the C# port. Codegen MUST
  verify the Dart body contains no `await` before dropping — for THIS
  file the check passes (zero `await` occurrences).
- The Dart source's `combinedProgram = userProgram;` redundant rebind
  (`// Combine with empty stdlib (simplified test)`) is preserved as
  `var combinedProgram = userProgram;` — a no-op alias the C# compiler
  optimises away; preserving documents Dart-source intent.
- The threading-model escalation INHERITS from heap_fcp.dart.md
  escalations[0]; same convention as scheduler.dart.md, mad_context.
  dart.md, global_send.dart.md, message_queue.dart.md, body_kernels.
  dart.md, system_predicates_impl.dart.md, runner.dart.md. No new
  escalation for THIS file (no LOCAL undecidable point).
