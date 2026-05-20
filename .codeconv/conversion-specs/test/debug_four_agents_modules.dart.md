# Conversion Spec — test/debug_four_agents_modules.dart

> Conversion-spec artifact for test/debug_four_agents_modules.dart (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> A `void main() async` **debug/diagnostic script** that boots FOUR
> `AgentRuntime` instances (alice/bob/carol/dave) with the `cssg_modules`
> project + `mad_boot.glp`, wires `onMadMessageReceived` cross-agent
> routing via a shared `pendingMessages` dictionary, drains messages in
> bounded rounds (≤30), and prints a per-agent tagged-output summary
> using a captured-group regex. The doc comment classifies this file as
> the multi-agent Play-4 mirror of `main_cssg_mad_modules.dart`. NO
> `package:test` import, NO `test(...)`, NO `group(...)`, NO `expect(...)`,
> NO matchers — exclusively `print(...)` diagnostics. Host shape is
> therefore `static Main` console-exe (the
> `debug_negative.dart`/`test_constant_compile.dart`/`test_agent_init_goal.dart`
> precedent), NOT `[Fact]`.
>
> Inherited escalations: this file exercises the multi-agent boot
> pathway (`AgentRuntime`, which in turn drives `GlpRuntime`,
> `Scheduler`, `MadContext`, `HeapFCP`). Per FR-013 and the
> sibling-multiagent precedent
> (`mad_context.dart.md` / `global_send.dart.md` / `message_queue.dart.md`
> / `scheduler.dart.md` / `system_predicates_impl.dart.md` /
> `body_kernels.dart.md` / `runner.dart.md` / `agent_runtime.dart.md`
> all INHERIT `heap_fcp.dart` escalations[0]), THIS file INHERITS
> without re-escalating. The four-agent fan-out exercises FOUR owning
> contexts simultaneously, but the diagnostic harness runs them ALL on
> the process's main thread serially (the `pendingMessages` map is
> drained inside a single-threaded loop; no thread-marshalling occurs).
> Whatever C# threading discipline `AgentRuntime` adopts in its SUT
> convspec (a `private readonly object _lock` field per
> `agent_runtime.dart.md`) is INHERITED here. NO genuinely-LOCAL
> undecidable point in THIS file.

```yaml
schema_version: 1
source_path: test/debug_four_agents_modules.dart
source_sha256: dec856d5b7a059a974c1fa9df57847e4b65dd6cf2a51e2e4f4eba8da78e0db7a
target_code_unit: test/DebugFourAgentsModules.cs
constructs:
  - construct_key: dart.doc_comment.file_header_triple_slash_multiline
    source_form: >-
      "/// Diagnostic: Four agents (Alice, Bob, Carol, Dave) with project modules.
       /// Simulates what main_cssg_mad_modules.dart does — linked project + mad_boot.
       ///
       /// Run: dart test/debug_four_agents_modules.dart"
    target_decision: >-
      Multi-line Dart `///` file-header doc comment maps to a multi-line
      C# `///` XML-doc comment placed immediately above the host class
      declaration `public static class DebugFourAgentsModules`. Each
      `///` line is preserved verbatim (including the em-dash `—` and
      the blank `///` separator line). No `<summary>` wrapping is added
      because the original is plain prose, not a structured doc comment
      — REUSE the cached idiom from
      `test/test_agent_init_goal.dart.md` (`rf-dart-tripleslash-doc-to-
      csharp-xml-doc`); no re-research per FR-024.
    idiom_id: null
    research_finding_id: rf-dart-tripleslash-doc-to-csharp-xml-doc
    nuance: >-
      Doc-comment-target nuance (KB cache hit per FR-012 / SC-007 —
      REUSED from `test/test_agent_init_goal.dart.md` and
      `lib/multiagent/agent_runtime.dart.md`): Dart `///` attaches to
      the FOLLOWING declaration. With no `library;` directive in this
      file the comment attaches to the implicit top-level `main`; in
      C# it attaches to the host static class. No semantic change.
      Em-dash nuance (explicitly addressed): the literal `—` (U+2014)
      is a non-ASCII character; both Dart `.dart` files and C# `.cs`
      files default to UTF-8 (Dart per `dart.dev` Tour; C# per .NET 5+
      compiler default — Microsoft Learn 'C# language specification —
      Lexical structure'). Preservation is byte-identical. The
      "Run: dart test/debug_four_agents_modules.dart" line is a
      developer-instruction comment ONLY; the C# port's equivalent
      invocation (`dotnet run --project <test-project> --
      --debug-four-agents-modules` OR a separate console exe) is a
      langpair-level concern recorded in conversion_units (cu-15),
      NOT asserted in the doc-comment text.

  - construct_key: dart.import.dart_io_unused_in_body
    source_form: "import 'dart:io';"
    target_decision: >-
      Dart `dart:io` is imported BUT only the `File` constructor +
      `readAsStringSync` is exercised (one call site on line 12). Maps
      to `using System.IO;` at file scope — the .NET counterpart that
      provides `File.ReadAllText(string path)` per the cached idiom
      `rf-dart-dart-io-file-readasstringsync-to-system-io-file-readalltext`
      recorded in `test/test_agent_init_goal.dart.md`. No other
      `dart:io` members (`Platform`, `stdin`, `Process`, `Directory`)
      are exercised — single member, single `using`.
    idiom_id: rf-dart-dart-io-file-readasstringsync-to-system-io-file-readalltext
    research_finding_id: rf-dart-dart-io-file-readasstringsync-to-system-io-file-readalltext
    nuance: >-
      Single-member-import nuance (KB cache hit per FR-012 / SC-007 —
      REUSED from `test/test_agent_init_goal.dart.md`): Dart imports
      every public symbol from `dart:io` unconditionally; C# `using
      System.IO;` brings the full namespace into scope. Both
      languages elide the unused-symbol cost (the JIT/linker drops
      unreferenced members). Encoding nuance: `File.ReadAllText`
      defaults to UTF-8 in BOTH Dart-io and .NET Core 1.0+ — agrees
      with the source-file UTF-8 assumption. Sync nuance: the call
      is `readAsStringSync` (synchronous) — C# port uses
      `File.ReadAllText` (synchronous), NOT `await
      File.ReadAllTextAsync` (which would change observable timing).

  - construct_key: dart.import.dart_typed_data_for_uint8list
    source_form: "import 'dart:typed_data';"
    target_decision: >-
      Dart `dart:typed_data` exposes `Uint8List` (a typed byte buffer);
      the only symbol referenced in this file is `Uint8List` in the
      `List<(String, Uint8List)>` type annotation of `pendingMessages`.
      Per the pinned project-wide rule recorded in
      `lib/multiagent/agent_runtime.dart.md` ('Uint8List → byte[]'),
      `Uint8List` maps to the .NET primitive `byte[]` (NOT
      `ReadOnlyMemory<byte>`, NOT `Span<byte>` — the project pins
      `byte[]` for payload contract compatibility with the
      `payload_serializer.dart` convspec). The Dart `dart:typed_data`
      import has NO corresponding C# `using` directive — `byte[]` is
      a primitive array type living in the implicit `System` namespace
      (covered by the existing `using System;` already required for
      `Console.WriteLine`). The C# port DROPS the `import 'dart:
      typed_data';` directive with NO replacement using.
    idiom_id: null
    research_finding_id: rf-dart-uint8list-import-to-csharp-byte-array-no-using-needed
    nuance: >-
      Primitive-array-vs-typed-buffer nuance (explicitly addressed and
      LOAD-BEARING per the project pin in
      `lib/multiagent/agent_runtime.dart.md` — REUSED): Dart
      `Uint8List` is a typed buffer with O(1) length, indexed `byte`
      access, and BYTE-ARRAY semantics; C# `byte[]` is the canonical
      .NET counterpart with identical O(1) length + indexed `byte`
      access. NO `Span<byte>` / `ReadOnlySpan<byte>` / `Memory<byte>`
      / `ReadOnlyMemory<byte>` substitution — those forms would
      ripple-change `AgentRuntime.OnSendMadMessage` /
      `OnMadMessageReceivedAsync` signatures across the entire
      multi-agent SUT cluster, which `agent_runtime.dart.md` has
      pinned to `byte[]`. Microsoft Learn authoritative basis: 'Arrays
      (C# Programming Guide)'
      (`https://learn.microsoft.com/dotnet/csharp/programming-guide/arrays/`).
      Import-erasure nuance: this is the FIRST file in the inventory
      where a `dart:typed_data` import drops with NO C# replacement
      — recorded as a NEW idiom row
      `rf-dart-uint8list-import-to-csharp-byte-array-no-using-needed`;
      will be a KB cache hit for subsequent files that import
      `dart:typed_data` only for `Uint8List`.

  - construct_key: dart.import.package_internal_single_agent_runtime
    source_form: "import 'package:glp_runtime/multiagent/agent_runtime.dart';"
    target_decision: >-
      Dart `package:glp_runtime/multiagent/agent_runtime.dart` import
      maps to ONE C# `using` directive naming the namespace produced
      by the converted SUT file. Per the SUT convspec
      `lib/multiagent/agent_runtime.dart.md` (which folds the
      multi-agent module under `<RootNs>.Multiagent`), the directive
      becomes `using <RootNs>.Multiagent;`. The brought-into-scope
      symbol is `AgentRuntime` (the C# class decided by the SUT
      convspec). No `as` alias / `show` narrowing / `hide` exclusion
      on the Dart side — simple unqualified `using` suffices. REUSE
      the cached idiom `rf-dart-internal-package-import-to-csharp-using`
      from `test/test_agent_init_goal.dart.md`,
      `test/debug_negative.dart.md`, and every prior internal-package
      import; no re-research per FR-012 / SC-007.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (KB cache hit per FR-012 / SC-007 —
      REUSED): Dart `package:` URIs are pubspec-anchored file-level
      references; C# `using` names a namespace, not a file. The single
      Dart import maps to the single C# `using <RootNs>.Multiagent;`.
      Symbol-visibility nuance: `AgentRuntime` is library-public on
      the Dart side (no leading underscore) so it maps to `public`
      C# per `agent_runtime.dart.md`. Project-file (assembly-reference)
      emission is a langpair-level concern, OUT OF SCOPE for this
      per-file artifact.

  - construct_key: dart.test_file.void_main_async_with_await_calls
    source_form: >-
      "void main() async {
         final projectDir = '../programs/cssg_modules';
         final bootSource = File('../programs/cssg_modules/mad_boot.glp').readAsStringSync();
         ...
         for (final entry in agents.entries) {
           print('--- Initializing ${entry.key} ---');
           await entry.value.initialize();
         }
         ...
         while (pendingMessages.isNotEmpty && rounds < 30) {
           ...
           await agent.onMadMessageReceived(from, payload);
         }
         ...
       }"
    target_decision: >-
      LOAD-BEARING DECISION (explicitly addressed): this file is NOT a
      `package:test` file — see file-header rationale. It is a `dart
      run`-invoked diagnostic script with `void main() async`; the
      `async` keyword IS REAL HERE (UNLIKE
      `test/test_agent_init_goal.dart.md` where the `async` carried
      no `await`). Multiple `await` calls appear in the body:
      `await entry.value.initialize()` inside the per-agent
      initialization loop, and `await agent.onMadMessageReceived(from,
      payload)` inside the message-routing loop. The xUnit `[Fact]`
      conversion shape is NOT APPLICABLE. The conversion target is the
      **debug_negative.dart precedent extended for genuine async**: a
      single static C# class `public static class
      DebugFourAgentsModules` with `public static async Task<int>
      Main(string[] args) { ... return 0; }`. The async Main signature
      is .NET 7.0+ canonical (Microsoft Learn 'Main method and
      command-line arguments' — async Main was added in C# 7.1+ /
      .NET Core 2.0; .NET 5+ universally supported). Each Dart top-
      level `print(...)` maps to `Console.WriteLine(...)` per the
      cached console-exe routing idiom. Codegen MUST keep the `async`
      keyword on Main because the body contains `await` — the
      verification gate that LET `test_agent_init_goal.dart`'s C#
      port DROP `async` (zero `await` occurrences) DOES NOT pass
      here. Adding `[Fact]` is forbidden: NO assertions, NO `expect`,
      NO matchers — this is purely diagnostic.
    idiom_id: rf-dart-debug-script-main-to-csharp-static-main
    research_finding_id: rf-dart-debug-script-async-main-to-csharp-async-task-main
    nuance: >-
      Async-with-real-await nuance (NEW for this file vs the
      `test_agent_init_goal.dart.md` precedent — explicitly
      addressed): Dart `void main() async { ... await foo(); ... }`
      with REAL `await` calls MUST map to a C# `async Task<int>
      Main(string[] args)` (.NET 7.0+ stable since .NET Core 2.0).
      Dropping `async` would force every `await agent.foo()` to either
      (a) `agent.Foo().GetAwaiter().GetResult()` (blocking,
      deadlock-prone in some sync-contexts) or (b) `agent.Foo().Wait()`
      (also blocking) — both would CHANGE the threading-model contract
      the SUT convspec relies on. The faithful translation preserves
      `async` + `await` semantics across the boundary. Return-type
      choice: `Task<int>` (NOT `Task` alone) for explicit exit-code
      reporting consistent with the debug_negative precedent. Final
      statement: `return 0;` after the "=== Done ===" print preserves
      the Dart-source's implicit-success exit code. Threading nuance:
      .NET async-Main runs continuations on the thread-pool by
      default UNLESS a `SynchronizationContext` is installed
      (Microsoft Learn 'Asynchronous programming with async and
      await'). For a console-exe with NO sync-context, the
      `pendingMessages` Map mutations after each `await` resume on
      a thread-pool worker — but because the loop is single-shot
      sequential (one agent's `await onMadMessageReceived` completes
      before the next iteration begins), there is NO concurrent
      access; the Dart single-threaded event-loop semantics are
      preserved through the C# port's sequential `await` chain.
      LOAD-BEARING — see Notes section for the latent
      `SynchronizationContext` consideration.

  - construct_key: dart.core.print
    source_form: >-
      "print('=== Four-agent modules diagnostic (Play 4) ===\\n');
       print('--- Initializing ${entry.key} ---');
       print('\\n--- Routing messages ---');
       print('  Round $rounds: Unknown destination: $dest');
       print('  Round $rounds: $from -> $dest (${payload.length} bytes)');
       print('\\n--- Summary ---');
       print('Rounds: $rounds');
       print('\\n$id tagged output (${tagged.length}):');
       print('  ${m.group(2)}: ${m.group(3)}');
       print('  $l');
       print('\\n=== Done ===');"
    target_decision: >-
      REUSED from `test/debug_negative.dart.md` and
      `test/test_agent_init_goal.dart.md` via the cached idiom
      `rf-dart-print-in-console-exe-to-console-writeline`: because
      the host is `static async Task<int> Main` (NOT `[Fact]`), every
      `print(<string>)` maps to `Console.WriteLine(<string>)`. The
      embedded `\\n` literals stay literal in the C# port (the C#
      string interpolation `$"...\n..."` preserves `\n` as one
      newline char). `using System;` is the only requirement.
    idiom_id: rf-dart-print-in-console-exe-to-console-writeline
    research_finding_id: rf-dart-print-in-console-exe-to-console-writeline
    nuance: >-
      Routing nuance (KB cache hit — REUSED from precedent specs):
      `print` routing depends on the HOST shape (static Main vs
      [Fact]). THIS file is a console-exe host → Console.WriteLine.
      Tab-vs-space-vs-leading-spaces nuance: the literal leading
      spaces in `'  Round $rounds: ...'` and `'  ${m.group(2)}: ...'`
      preserve byte-identically across both languages — both Dart
      and C# treat literal whitespace inside string literals as
      part of the runtime value. Encoding nuance: ALL print
      strings in this file are ASCII (including the em-dash `—`
      in the file header, which is NOT inside any `print` body —
      the prints use plain dashes `===`/`---`); no Console.OutputEncoding
      ceremony required.

  - construct_key: dart.string.interpolation
    source_form: >-
      "'--- Initializing ${entry.key} ---';
       '  Round $rounds: Unknown destination: $dest';
       '  Round $rounds: $from -> $dest (${payload.length} bytes)';
       '[$id] $msg';
       'Rounds: $rounds';
       '\\n$id tagged output (${tagged.length}):';
       '  ${m.group(2)}: ${m.group(3)}';
       '  $l';"
    target_decision: >-
      Map Dart string interpolation `'... $name ...'` and `'... ${expr}
      ...'` to C# interpolated string literals `$"... {Name} ..."` and
      `$"... {Expr} ..."`. Concrete mappings:
      - `'--- Initializing ${entry.key} ---'` → `$"--- Initializing
        {entry.Key} ---"` (KeyValuePair.Key is PascalCase).
      - `'  Round $rounds: Unknown destination: $dest'` → `$"  Round
        {rounds}: Unknown destination: {dest}"`.
      - `'  Round $rounds: $from -> $dest (${payload.length} bytes)'`
        → `$"  Round {rounds}: {from} -> {dest} ({payload.Length}
        bytes)"` (byte-array `Length` PascalCased; `byte[].Length`
        is the canonical .NET property).
      - `'[$id] $msg'` → `$"[{id}] {msg}"` (used in the `onLog`
        lambda — see lambda construct below).
      - `'\\n$id tagged output (${tagged.length}):'` →
        `$"\n{id} tagged output ({tagged.Count}):"` (Dart `List<T>
        .length` → C# `List<T>.Count`; per the KB cache hit
        `rf-dart-list-isnotempty-and-length-to-csharp-count`).
      - `'  ${m.group(2)}: ${m.group(3)}'` → `$"  {m.Groups[2]
        .Value}: {m.Groups[3].Value}"` (Dart `RegExp.Match.group(int)`
        → C# `Match.Groups[int].Value`).
      - `'  $l'` → `$"  {l}"`.
    idiom_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      Field-name-casing nuance (KB cache hit per FR-012 / SC-007 —
      REUSED from `test/test_agent_init_goal.dart.md`): each
      interpolated expression must be RE-EMITTED with the PascalCased
      property name from the owning SUT convspec. ToString nuance:
      `$rounds` (int), `$id` (string), `$dest` (string), `$from`
      (string), `$msg` (string), `$l` (string) — all forward to the
      embedded type's `ToString()`; identical observable shape across
      both languages for built-in types. List-length-vs-count nuance
      (LOAD-BEARING and explicitly addressed): `tagged.length` is
      Dart `List<String>.length` returning `int`; C# `List<T>` has
      `Count` (NOT `Length` — `Length` exists on arrays/strings but
      not generic `List<T>`). Codegen MUST emit `.Count`, NOT
      `.Length`, for the Dart `tagged.length` interpolation. The
      `payload.length` interpolation IS `byte[].Length` (array,
      Length is correct). Codegen MUST consult the static type at
      each interpolation site to pick `.Length` (array, string) vs
      `.Count` (List<T>) — this is the same per-call discipline as
      the `isNotEmpty` mapping recorded in `test/debug_negative.dart.md`.

  - construct_key: dart.local_var.final_inferred_type
    source_form: >-
      "final projectDir = '../programs/cssg_modules';
       final bootSource = File('../programs/cssg_modules/mad_boot.glp').readAsStringSync();
       final rootSelfGlpPath = File('../programs/self.glp').absolute.path;
       final pendingMessages = <String, List<(String, Uint8List)>>{};
       final outputs = <String, List<String>>{ ... };
       final alice = makeAgent('alice', 'parent_init/4', ['carol', '4']);
       final bob = makeAgent('bob', 'parent_init/4', ['dave', '4']);
       final carol = makeAgent('carol', 'child_init/3', ['4']);
       final dave = makeAgent('dave', 'child_init/3', ['4']);
       final agents = {'alice': alice, 'bob': bob, 'carol': carol, 'dave': dave};
       final snapshot = Map<String, List<(String, Uint8List)>>.from(pendingMessages);
       final taggedRegex = RegExp(r'^< tagged\\((\\w+), (cmd|notify)\\((.+)\\)\\)$');
       final tagged = outputs[id]!.where((l) => l.contains('tagged(')).toList();"
    target_decision: >-
      Each `final <name> = <expr>;` Dart local maps to `var <name> =
      <expr>;` in C#. REUSE the cached idiom
      `rf-dart-final-local-to-csharp-var-local` from precedent specs.
      No local in this file is reassigned (verified by inspection;
      `rounds` IS the only mutable local — declared with `var`, not
      `final` — see the dart.var_local_mutable construct below).
      Single-assignment is preserved across the conversion: every
      `final` becomes `var`; the C# compiler does not enforce
      `readonly`-local but the source convention is preserved by
      lack of any later re-assignment.
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Single-assignment nuance (KB cache hit per FR-012 / SC-007 —
      REUSED). Type-inference nuance: every `final` here uses
      inferred typing; C# `var` likewise infers. The two
      `File(...).readAsStringSync()` lines and `File(...).absolute
      .path` line collapse per the `dart:io` import construct above.
      `File('...').absolute.path` is the Dart-io pattern for
      resolving a relative path to an absolute path. The C# port
      MAPS to `System.IO.Path.GetFullPath('../programs/self.glp')`
      (Microsoft Learn 'Path.GetFullPath' —
      `https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath`)
      — the canonical .NET counterpart. The Dart `File.absolute`
      getter returns a `File` reference whose `path` field is the
      absolute path string; the C# port collapses the two-step
      (construct File handle + read `.absolute.path` getter) into
      the one-step static `Path.GetFullPath` call. Recorded as a
      NEW row `rf-dart-file-absolute-path-to-csharp-path-getfullpath`
      below in the dart.member_access construct cluster.

  - construct_key: dart.local_var.mutable_int_counter_var
    source_form: "var rounds = 0;"
    target_decision: >-
      Dart `var rounds = 0;` is a mutable local with inferred type
      `int`; the later `rounds++;` reassigns. Maps 1:1 to C# `var
      rounds = 0;` (C# `var` is mutable by default; inferred type
      `int`). REUSE the cached idiom `rf-dart-var-mutable-local-to-
      csharp-var-local` from `test/debug_negative.dart.md`.
    idiom_id: rf-dart-var-mutable-local-to-csharp-var-local
    research_finding_id: rf-dart-var-mutable-local-to-csharp-var-local
    nuance: >-
      Mutability nuance (KB cache hit — REUSED): Dart `var` allows
      reassignment; C# `var` allows reassignment. The C# `int rounds
      = 0;` explicit-type form is also valid; codegen prefers `var`
      to match Dart's omitted-type idiom. Increment nuance: Dart
      `rounds++` and C# `rounds++` are identical post-increment
      operators on `int`. Bounded-loop-counter nuance (explicitly
      addressed): the `while (pendingMessages.isNotEmpty && rounds
      < 30)` loop guards against runaway message routing — the `30`
      magic number is preserved verbatim in the C# port (same
      .NET-preserves-magic-numbers discipline from
      `lib/runtime/scheduler.dart.md`).

  - construct_key: dart.constructor_call.implicit_new_external_and_sut
    source_form: >-
      "File('../programs/cssg_modules/mad_boot.glp');   // dart:io
       File('../programs/self.glp');                    // dart:io
       AgentRuntime(
         agentId: id,
         glpSources: [bootSource],
         rootSelfGlpPath: rootSelfGlpPath,
         goalLabel: goal,
         extraArgs: extra,
         projectDir: projectDir,
       );
       Map<String, List<(String, Uint8List)>>.from(pendingMessages);"
    target_decision: >-
      Dart 2+ implicit-`new` constructor calls map to C# explicit
      `new T(...)` (or target-typed `new()` where the LHS type is
      known). Concrete emissions:
      - `File('../programs/cssg_modules/mad_boot.glp')` is the SOLE
        Dart-io File constructor; per the dart_io import construct
        above the Dart two-step (construct + `.readAsStringSync()`)
        COLLAPSES into the C# one-step `File.ReadAllText(path)` — NO
        `new File(...)` emitted.
      - `File('../programs/self.glp').absolute.path` likewise
        COLLAPSES to `Path.GetFullPath("../programs/self.glp")` —
        NO `new File(...)` emitted.
      - `AgentRuntime(...)` with named arguments → `new AgentRuntime(
        agentId: id, glpSources: new List<string> { bootSource },
        rootSelfGlpPath: rootSelfGlpPath, goalLabel: goal,
        extraArgs: extra, projectDir: projectDir)` (named-argument
        call site preferred for readability per the named-arguments
        idiom). Per `lib/multiagent/agent_runtime.dart.md` the C#
        constructor exposes positional parameters `(string agentId,
        List<string> glpSources, string rootSelfGlpPath, ...)`; C#
        named-argument syntax (Microsoft Learn 'Named and optional
        arguments') applies to positional parameters freely. Codegen
        MUST consult the SUT convspec for the canonical C# constructor
        parameter list and apply the named-argument call-site form
        for the same readability reason as
        `test/test_agent_init_goal.dart.md`.
      - `Map<String, List<(String, Uint8List)>>.from(pendingMessages)`
        is a NAMED-CONSTRUCTOR call — see the dedicated
        `dart.map.named_constructor_from` construct below.
    idiom_id: rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new
    research_finding_id: rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new
    nuance: >-
      Implicit-new nuance (KB cache hit per FR-012 / SC-007 — REUSED
      from prior specs): Dart 2+ allows omitting `new`; C# requires
      it. The `AgentRuntime(...)` named-argument-heavy call site is
      preserved as C# named arguments (NOT object-initialiser
      `{ AgentId = id, ... }` — that would force `init`-only
      property setters on the SUT, changing the immutability contract
      `agent_runtime.dart.md` decided). Cross-file authority nuance:
      codegen MUST consult `agent_runtime.dart.md` for the canonical
      C# constructor signature — DO NOT mechanically alphabetise the
      Dart named arguments; the C# positional parameter ORDER is
      whatever the SUT convspec decides.

  - construct_key: dart.map.typed_literal_with_record_value_type
    source_form: >-
      "final pendingMessages = <String, List<(String, Uint8List)>>{};
       final outputs = <String, List<String>>{
         'alice': [], 'bob': [], 'carol': [], 'dave': [],
       };"
    target_decision: >-
      Dart `<K, V>{}` typed empty map literal → C# `new Dictionary<K,
      V>()` (collection-initialiser empty form). Concrete emissions:
      - `<String, List<(String, Uint8List)>>{}` → `new
        Dictionary<string, List<(string from, byte[] payload)>>()`
        (the value type is `List<ValueTuple<string, byte[]>>` —
        Dart positional records `(String, Uint8List)` map to .NET
        `ValueTuple<string, byte[]>` per the dart.record_type
        construct below; named-tuple labels are added at the C# call
        site for documentation, NOT load-bearing).
      - `<String, List<String>>{ 'alice': [], 'bob': [], 'carol':
        [], 'dave': [] }` → `new Dictionary<string, List<string>> {
        { "alice", new List<string>() }, { "bob", new
        List<string>() }, { "carol", new List<string>() }, { "dave",
        new List<string>() } }` (curly-pair collection-initialiser
        form, per the cached idiom from
        `test/test_agent_init_goal.dart.md` —
        `rf-dart-typed-map-literal-to-csharp-dictionary-collection-init`).
      Per the cached idiom; no re-research.
    idiom_id: rf-dart-typed-map-literal-to-csharp-dictionary-collection-init
    research_finding_id: rf-dart-typed-map-literal-to-csharp-dictionary-collection-init
    nuance: >-
      Empty-map-literal nuance (explicitly addressed): Dart `<K,V>{}`
      with explicit type parameters maps to `new Dictionary<K, V>()`
      — the type parameters MUST appear because there is no
      initialiser to drive inference. The `{ 'alice': [], ... }`
      populated form maps to the `{ { K, V }, ... }` collection-
      initialiser. Type-parameter-explicitness nuance: codegen
      prefers explicit type parameters to preserve the Dart-source
      reader-clue (C# 9+ `new()` target-typed form is also valid
      when the LHS type is known). Inner-empty-list nuance: the
      Dart `[]` empty list literal on the value side maps to `new
      List<string>()` (NOT `Array.Empty<string>()` — the map's
      value type is `List<string>`, and `Array.Empty<string>()`
      returns `string[]`, a different type). Codegen MUST emit
      `new List<string>()`. Element-type for pendingMessages:
      `List<(string, byte[])>` is `new List<(string, byte[])>()`.

  - construct_key: dart.record_type.positional_two_field_string_uint8list
    source_form: >-
      "<String, List<(String, Uint8List)>>{};
       (id, payload);          // record value construction in the lambda
       for (final (from, payload) in entry.value) { ... }"
    target_decision: >-
      Dart 3 positional record type `(String, Uint8List)` maps to C#
      `ValueTuple<string, byte[]>` (the canonical generic value-tuple
      type) usable via the C# 7+ tuple syntax `(string, byte[])`
      (Microsoft Learn 'Tuple types' —
      `https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/value-tuples`).
      For DOCUMENTATION (NOT load-bearing) the C# port MAY emit
      named tuple labels `(string from, byte[] payload)` at the
      List<>-element type declaration site — this is the same shape
      already used in `lib/multiagent/agent_runtime.dart.md` for the
      `OnSendMadMessage` callback signature. Value construction
      `(id, payload)` maps 1:1 to C# `(id, payload)` value-tuple
      construction; destructuring `final (from, payload) = entry`
      maps 1:1 to C# `var (from, payload) = entry`.
    idiom_id: rf-dart-record-return-to-csharp-valuetuple
    research_finding_id: rf-dart-record-return-to-csharp-valuetuple
    nuance: >-
      Positional-record nuance (KB cache hit per FR-012 / SC-007 —
      REUSED from `test/test_agent_init_goal.dart.md` and
      `test/heap/binding_pointer_test.dart.md` —
      `rf-dart-record-return-to-csharp-valuetuple`): Dart 3
      positional records and C# `ValueTuple<,>` are
      stack-allocated value types with identical positional-element
      access semantics. The element types are `string` (Dart String)
      + `byte[]` (Dart Uint8List per the pinned project rule). NO
      heap-allocation difference, NO Boxing concern (the
      `ValueTuple<string, byte[]>` puts only one reference field
      `byte[]` on the heap, identical to the Dart record's reference
      to the underlying typed buffer). Element-name nuance: Dart
      positional records have NO element names — destructuring binds
      positional indices. C# `var (from, payload)` likewise binds
      positionally regardless of declared element names; the
      SAME-NAME identifiers `from` and `payload` are valid C#
      variable names (neither is a reserved keyword — `from` IS a
      LINQ contextual keyword but legal as an identifier).
      Conversion-direction nuance: Dart `Uint8List` element is
      reference-semantics under the hood (a typed buffer); C#
      `byte[]` element is reference-semantics (an array on the heap).
      Identical observable behavior across the conversion.

  - construct_key: dart.map.named_constructor_from
    source_form: >-
      "final snapshot = Map<String, List<(String, Uint8List)>>.from(pendingMessages);"
    target_decision: >-
      Dart `Map<K,V>.from(otherMap)` is a NAMED-CONSTRUCTOR that
      creates a SHALLOW copy of `otherMap` (the new map shares the
      SAME value-references with the original — per Dart
      `dart-core/Map/Map.from.html`). Maps to C# `new Dictionary<K,
      V>(otherMap)` — the canonical copy-constructor (Microsoft Learn
      'Dictionary<TKey,TValue>(IDictionary<TKey,TValue>)' —
      `https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.-ctor#system-collections-generic-dictionary-2-ctor(system-collections-generic-idictionary((-0-1)))`).
      Concrete: `var snapshot = new Dictionary<string,
      List<(string, byte[])>>(pendingMessages);`. SHALLOW-COPY
      semantics agree between both languages: the new dictionary's
      VALUE-references are the SAME `List<(string, byte[])>`
      instances as the original; clearing or mutating one
      dictionary's KEY SET does not affect the other, BUT mutating
      the shared-value lists DOES affect both. This file relies on
      that exact shape (the immediate `pendingMessages.clear();`
      after `snapshot = Map.from(pendingMessages);` empties the
      key set on the original while keeping the snapshot intact —
      load-bearing).
    idiom_id: null
    research_finding_id: rf-dart-map-from-named-ctor-to-csharp-dictionary-copy-ctor
    nuance: >-
      Shallow-copy semantics nuance (LOAD-BEARING and explicitly
      addressed): both Dart `Map.from` and .NET `Dictionary<K,V>(
      IDictionary)` ctor produce SHALLOW copies — the value
      references are shared between the original and the copy. The
      `pendingMessages.clear()` immediately after creating the
      snapshot empties only the original's key set; the snapshot's
      Lists (and the bytes inside each `byte[]`) are untouched. The
      C# port MUST preserve this contract because the routing loop
      iterates `snapshot.entries` AFTER `pendingMessages.clear()` —
      a deep copy would waste allocation and a reference-only alias
      would corrupt the iteration. NEW idiom row
      `rf-dart-map-from-named-ctor-to-csharp-dictionary-copy-ctor`
      recorded; will be a KB cache hit for subsequent files that
      exercise `Map<K,V>.from`. Authoritative basis (Dart):
      `https://api.dart.dev/stable/dart-core/Map/Map.from.html`
      (Map.from documentation — 'Creates a Map instance in which
      the keys and values are computed from the iterable.').
      Authoritative basis (.NET): Microsoft Learn Dictionary
      constructor cited above.

  - construct_key: dart.closure.local_function_returning_agent_runtime
    source_form: >-
      "AgentRuntime makeAgent(String id, String goal, List<String> extra) {
         final agent = AgentRuntime( ... );
         agent.onOutput = (line) { outputs[id]!.add(line); };
         agent.onLog = (tag, msg) { if (...) print('[$id] $msg'); };
         agent.onSendMadMessage = (to, payload) async {
           pendingMessages.putIfAbsent(to, () => []).add((id, payload));
         };
         return agent;
       }"
    target_decision: >-
      Dart LOCAL FUNCTION `makeAgent(...)` is a function-scoped helper
      declared inside `main` that captures the enclosing scope's
      `outputs`, `pendingMessages`, and `bootSource`/`rootSelfGlpPath`/
      `projectDir` locals. C# supports local functions since C# 7.0
      (Microsoft Learn 'Local functions'
      —`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/local-functions`)
      with FULL closure semantics over enclosing locals. Maps 1:1 to:
      `AgentRuntime MakeAgent(string id, string goal, List<string>
      extra) { var agent = new AgentRuntime(...); agent.OnOutput =
      line => { outputs[id].Add(line); }; agent.OnLog = (tag, msg) =>
      { if (...) Console.WriteLine($"[{id}] {msg}"); };
      agent.OnSendMadMessage = async (to, payload) => { if
      (!pendingMessages.TryGetValue(to, out var list)) {
      pendingMessages[to] = list = new List<(string, byte[])>(); }
      list.Add((id, payload)); await Task.CompletedTask; }; return
      agent; }`. The `async` keyword on the `OnSendMadMessage` lambda
      preserves the Dart `async` (the callback's return type is
      `Future<void>` Dart-side, `Task` C#-side); since the lambda
      body has no actual `await` operation, `await Task.CompletedTask;`
      MAY be appended for explicit-completion, OR the lambda MAY be
      declared without `async` returning `Task.CompletedTask` directly
      (more idiomatic). Codegen preference: declare the lambda
      `async` to preserve the Dart source's `async` keyword
      verbatim — this is the LOAD-BEARING precedent recorded in
      `lib/multiagent/agent_runtime.dart.md` for the
      `OnSendMadMessage` field type (`Func<string, byte[], Task>`).
    idiom_id: null
    research_finding_id: rf-dart-local-function-with-captures-to-csharp-local-function
    nuance: >-
      Local-function nuance (NEW for this file, LOAD-BEARING —
      explicitly addressed): C# local functions (C# 7.0+) are
      semantically identical to Dart local functions — both capture
      enclosing locals by reference, both are nested in the
      enclosing method's scope, both can be `async`. Lambda-vs-local-
      function nuance: codegen prefers C# LOCAL FUNCTION syntax
      `AgentRuntime MakeAgent(...) { ... }` over a C# lambda `var
      MakeAgent = (string id, ...) => { ... };` because (a) local
      functions support recursion (lambdas do not before assignment
      completes), (b) local functions have slightly better
      performance (no delegate allocation when not captured), and
      (c) the C# syntax mirrors the Dart `AgentRuntime makeAgent(
      String id, ...)` declaration more closely. Async-lambda
      nuance: the `agent.onSendMadMessage = (to, payload) async {
      ... };` Dart async lambda MUST be a C# `async` lambda
      `agent.OnSendMadMessage = async (to, payload) => { ... };`
      because `OnSendMadMessage` is typed `Func<string, byte[],
      Task>` per `agent_runtime.dart.md`'s pinned signature. Adding
      `await Task.CompletedTask;` at the end is OPTIONAL (the
      compiler emits a warning CS1998 'This async method lacks
      `await` operators and will run synchronously' if omitted —
      but the warning is non-blocking; suppressing it is a langpair
      decision). Capture nuance: the lambdas capture `id` (the
      enclosing local-function parameter), `outputs` (the enclosing
      `main` local), `pendingMessages` (the enclosing `main` local
      Dictionary). C# closures over local-function parameters and
      enclosing-method locals are first-class (Microsoft Learn
      'Closures with local functions') — no `[Closure]` attribute
      or `static` qualifier is added; the capture is implicit.
      NEW idiom row `rf-dart-local-function-with-captures-to-
      csharp-local-function` recorded.

  - construct_key: dart.lambda.callback_with_implicit_arrow
    source_form: >-
      "agent.onOutput = (line) {
         outputs[id]!.add(line);
       };
       agent.onLog = (tag, msg) {
         if (msg.contains('RUN:') || msg.contains('ERROR') || msg.contains('SEND_MAD')) {
           print('[$id] $msg');
         }
       };
       agent.onSendMadMessage = (to, payload) async {
         pendingMessages.putIfAbsent(to, () => []).add((id, payload));
       };"
    target_decision: >-
      Dart lambda assignment `agent.onOutput = (line) { ... };` →
      C# delegate-property assignment `agent.OnOutput = line => {
      ... };` (Microsoft Learn 'Lambda expressions' —
      `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/lambda-expressions`).
      Per `lib/multiagent/agent_runtime.dart.md` the C# property
      types are `Action<string> OnOutput`, `Action<string, string>
      OnLog`, `Func<string, byte[], Task> OnSendMadMessage`. Concrete
      emissions are described in the local-function construct above.
      The Dart lambda parameter lists are SINGLE-IDENTIFIER (no type
      annotation, no parentheses-around-single-arg required for
      lambdas in Dart 2+) — C# allows the equivalent shorthand
      `line => ...` for single-arg lambdas.
    idiom_id: null
    research_finding_id: rf-dart-callback-assignment-lambda-to-csharp-delegate-property-lambda
    nuance: >-
      Single-arg-lambda-parenthesis nuance (explicitly addressed):
      Dart `(line) { ... }` has the parentheses around the single
      arg `line`; C# can OMIT the parentheses for a single arg:
      `line => { ... }`. Both forms compile in C#; codegen prefers
      the parentheses-less form for visual parity with C# idiom.
      Two-arg-lambda nuance: `(tag, msg) { ... }` Dart →
      `(tag, msg) => { ... }` C# (parentheses REQUIRED for
      multi-arg lambdas in BOTH languages). Async-lambda nuance:
      `(to, payload) async { ... }` Dart → `async (to, payload) =>
      { ... }` C# — see the local-function construct above. Capture
      nuance (KB cache hit — REUSED from
      `lib/multiagent/agent_runtime.dart.md`'s LOAD-BEARING async
      lambda decision): the lambda captures `id`, `outputs`, and
      `pendingMessages` from the enclosing scope; both languages
      bind those by reference; mutation of `outputs[id]` and
      `pendingMessages` is visible to the enclosing scope across
      the conversion boundary. NEW idiom row
      `rf-dart-callback-assignment-lambda-to-csharp-delegate-
      property-lambda` recorded.

  - construct_key: dart.list_indexer_with_null_assertion
    source_form: >-
      "outputs[id]!.add(line);
       outputs[id]!.where((l) => l.contains('tagged(')).toList();"
    target_decision: >-
      Dart `outputs[id]!` is a Map<K, V?>-indexer-followed-by-null-
      assertion: `outputs[id]` returns `List<String>?`; `!` narrows
      to non-null at runtime (throwing `TypeError` if null). C#
      `Dictionary<string, List<string>>` indexer `outputs[id]` returns
      `List<string>` directly, but THROWS `KeyNotFoundException` on
      missing key — semantically NEARLY the SAME (both throw on
      missing-key; only the EXCEPTION TYPE differs). For THIS file,
      every `outputs[id]!` callsite uses a key that was definitely
      inserted at outputs construction time (one of 'alice'/'bob'/
      'carol'/'dave'); a runtime missing-key is impossible by
      construction. The faithful translation is `outputs[id].Add(
      line);` and `outputs[id].Where(l => l.Contains("tagged(")
      ).ToList();` — drop the `!` because the C# indexer is
      already non-nullable, and the missing-key case is structurally
      impossible. NO `TryGetValue` ceremony required (unlike
      `test/test_agent_init_goal.dart.md`'s map-lookup case where
      the missing-key was the actual branch under inspection).
    idiom_id: null
    research_finding_id: rf-dart-bang-assert-on-map-indexer-to-csharp-dictionary-indexer-direct
    nuance: >-
      Null-assertion-erasure nuance (LOAD-BEARING and explicitly
      addressed): Dart `Map<K, V>.operator[]` returns `V?`; the `!`
      narrows-or-throws. C# `Dictionary<K, V>.this[K]` returns `V`
      directly and throws on miss. The two exception types differ
      (`TypeError` Dart vs `KeyNotFoundException` C#) but the
      observable behaviour ("throws on missing key") agrees.
      Codegen MUST consult each `outputs[id]!` site for whether the
      `!` was load-bearing (a real null was possible) or
      ceremonially-by-construction (key inserted at map creation).
      In THIS file every callsite is by-construction safe — the
      `!` erases. NEW idiom row
      `rf-dart-bang-assert-on-map-indexer-to-csharp-dictionary-
      indexer-direct` recorded. List-vs-array nuance: `.Add(line)`
      is C# `List<string>.Add` (PascalCase); `.where(...).toList()`
      is LINQ `.Where(...).ToList()` (requires `using System.Linq;`).

  - construct_key: dart.linq.where_tolist_with_lambda_predicate
    source_form: >-
      "outputs[id]!.where((l) => l.contains('tagged(')).toList();"
    target_decision: >-
      Dart `Iterable<T>.where(bool Function(T))` + `.toList()` → C#
      LINQ `IEnumerable<T>.Where(Func<T, bool>).ToList()` (Microsoft
      Learn 'Enumerable.Where' —
      `https://learn.microsoft.com/dotnet/api/system.linq.enumerable.where`
      and 'Enumerable.ToList' —
      `https://learn.microsoft.com/dotnet/api/system.linq.enumerable.tolist`).
      Concrete: `outputs[id].Where(l => l.Contains("tagged(")).
      ToList()` returning `List<string>`. Requires `using System.
      Linq;` at file scope.
    idiom_id: null
    research_finding_id: rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist
    nuance: >-
      LINQ-namespace nuance (LOAD-BEARING): codegen MUST add `using
      System.Linq;` to cu-1 — without it, `.Where` and `.ToList`
      are unresolved. Predicate-lambda nuance: Dart `(l) => l
      .contains('tagged(')` and C# `l => l.Contains("tagged(")`
      are syntactically near-identical; the only differences are
      (a) Dart parenthesises the single-arg, C# does not, (b) Dart
      uses single-quoted strings, C# double-quoted, (c) Dart
      `String.contains` → C# `string.Contains` (PascalCase). NEW
      idiom row recorded for the where-tolist pair; this row is
      structurally identical to the `.expand(...).toList()` row
      recorded in `test/debug_negative.dart.md` but exercises
      `Where` (filter) rather than `SelectMany` (flatten).

  - construct_key: dart.method_call.map_putifabsent_default_factory
    source_form: >-
      "pendingMessages.putIfAbsent(to, () => []).add((id, payload));"
    target_decision: >-
      Dart `Map<K, V>.putIfAbsent(K, V Function())` is a documented
      method that (a) returns the existing value if `K` is present,
      (b) otherwise calls the factory, inserts the result under `K`,
      and returns the inserted value (per `dart-core/Map/putIfAbsent
      .html`). The .NET counterpart is C# .NET 6+
      `CollectionExtensions.GetValueOrDefault` or the canonical
      `TryGetValue` + Add idiom OR the C# .NET 6+
      `Dictionary<K,V>.TryAdd` + `[K]` pattern. The MOST FAITHFUL
      translation is the explicit `TryGetValue`-out-Add idiom:
      `if (!pendingMessages.TryGetValue(to, out var list)) {
      pendingMessages[to] = list = new List<(string, byte[])>(); }
      list.Add((id, payload));`. Microsoft Learn authoritative
      basis: 'Dictionary<TKey,TValue>.TryGetValue' —
      `https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.trygetvalue`.
      Alternative form (LESS faithful — does not lazy-construct):
      `pendingMessages.TryAdd(to, new List<(string, byte[])>());
      pendingMessages[to].Add((id, payload));` — this ALWAYS
      allocates a `List<>` even on a hit, violating Dart
      `putIfAbsent`'s lazy-factory contract. Codegen MUST use the
      `TryGetValue`-out form.
    idiom_id: null
    research_finding_id: rf-dart-map-putifabsent-to-csharp-trygetvalue-out-with-lazy-init
    nuance: >-
      Lazy-factory nuance (LOAD-BEARING and explicitly addressed):
      Dart `putIfAbsent` does NOT call the factory if the key is
      present — only on the absent branch. The C# `TryGetValue`
      pattern reproduces this contract exactly: the `new List<...>
      ()` allocation only happens inside the `!TryGetValue` branch.
      Using `TryAdd` would NOT preserve the lazy contract because
      C# `TryAdd(K, V)` evaluates V before checking presence —
      forcing the `new List` allocation EVERY time. Codegen MUST
      pick the `TryGetValue`-out form. Reference-aliasing nuance:
      after the `if-then` block, `list` is a non-null reference
      pointing at the same `List<>` instance stored in the
      dictionary at key `to` (either the pre-existing one OR the
      newly-allocated one); `list.Add((id, payload))` mutates the
      dictionary-held list in place. This matches Dart's
      `putIfAbsent(...).add(...)` semantics exactly. NEW idiom
      row `rf-dart-map-putifabsent-to-csharp-trygetvalue-out-with-
      lazy-init` recorded; will become a KB cache hit for
      subsequent files exercising `Map.putIfAbsent`.

  - construct_key: dart.regexp.raw_literal_with_capture_groups
    source_form: >-
      "final taggedRegex = RegExp(r'^< tagged\\((\\w+), (cmd|notify)\\((.+)\\)\\)$');"
    target_decision: >-
      Dart `RegExp(r'<pattern>')` raw-string literal (the `r` prefix
      disables backslash escape processing) maps to C# `new
      System.Text.RegularExpressions.Regex(@"<pattern>")` — `@"..."`
      verbatim string is the canonical .NET raw-regex literal
      (Microsoft Learn 'Regex Class' —
      `https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex`
      and 'Regular expression language - quick reference' —
      `https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-language-quick-reference`).
      Concrete: `var taggedRegex = new Regex(@"^< tagged\((\w+),
      (cmd|notify)\((.+)\)\)$");` — Microsoft Learn confirms
      `\w` = `[a-zA-Z0-9_]`, `\(` = literal `(`, `(...)` =
      capture group, `(cmd|notify)` = alternation — IDENTICAL
      semantics to Dart's RE2-ish `\w` / `\(` / alternation.
      STATIC PRE-COMPILED REGEX (LOAD-BEARING — REUSED from
      `lib/runtime/scheduler.dart.md`'s RegEx idiom row): declare
      as `private static readonly Regex TaggedRegex = new(@"^<
      tagged\((\w+), (cmd|notify)\((.+)\)\)$", RegexOptions.Compiled);`
      at class scope to avoid recompilation on every iteration of
      the per-agent tagged-output loop. `RegexOptions.Compiled`
      is recommended for repeated use (Microsoft Learn 'Best
      practices for regular expressions' —
      `https://learn.microsoft.com/dotnet/standard/base-types/best-practices-for-regular-expressions`).
      Requires `using System.Text.RegularExpressions;` at file
      scope.
    idiom_id: null
    research_finding_id: rf-dart-regexp-raw-literal-to-csharp-regex-verbatim-static-readonly
    nuance: >-
      Raw-string-vs-verbatim nuance (KB cache hit per FR-012 / SC-007
      — REUSED from `lib/runtime/scheduler.dart.md` regex construct):
      Dart `r'pattern'` raw string + C# `@"pattern"` verbatim string
      both disable backslash-escape processing. The regex BODY is
      preserved byte-identically (`\w`, `\d`, `\(`, `\)` all reach
      the engine unescaped on both sides). Engine-semantics nuance:
      Dart regex uses RE2-style semantics (limited backtracking,
      linear-time guarantees for common patterns); .NET regex uses
      a backtracking NFA engine by default. For THIS file's pattern
      `^< tagged\((\w+), (cmd|notify)\((.+)\)\)$` — anchored at
      both ends, simple alternation, three capture groups, no
      catastrophic-backtracking risk — both engines produce
      identical match results. RegexOptions.Compiled flag:
      authoritative recommendation per Microsoft Learn 'Best
      practices for regular expressions'; tradeoff is a one-time
      compilation cost vs. faster repeated matches. Capture-group
      indexing nuance (LOAD-BEARING — explicitly addressed): Dart
      `Match.group(int)` is 0-indexed where 0 = whole match, 1 =
      first capture group. C# `Match.Groups[int].Value` is the
      same indexing model (0 = whole match, 1 = first capture
      group). `m.group(2)` Dart → `m.Groups[2].Value` C#;
      `m.group(3)` Dart → `m.Groups[3].Value` C#. Identical
      semantics. NEW idiom row recorded; will become a KB cache
      hit for subsequent files using `RegExp(r'...')`.

  - construct_key: dart.regexp.firstmatch_nullable
    source_form: >-
      "final m = taggedRegex.firstMatch(l);
       if (m != null) {
         print('  ${m.group(2)}: ${m.group(3)}');
       } else {
         print('  $l');
       }"
    target_decision: >-
      Dart `RegExp.firstMatch(String)` returns `Match?` (nullable —
      null if no match). C# `Regex.Match(string)` returns `Match`
      (NON-nullable) with `Success` property that distinguishes
      match-vs-no-match. Faithful translation:
      `var m = TaggedRegex.Match(l);
       if (m.Success) {
         Console.WriteLine($"  {m.Groups[2].Value}: {m.Groups[3].Value}");
       } else {
         Console.WriteLine($"  {l}");
       }`
      Microsoft Learn authoritative basis: 'Regex.Match(String)'
      (`https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex.match`)
      and 'Match.Success' (`https://learn.microsoft.com/dotnet/api/
      system.text.regularexpressions.match.success`). The
      null-vs-Success distinction is a NUANCE — C# `Regex.Match`
      never returns null; it always returns a Match instance whose
      `Success` is false on no-match. The Dart `m != null` check
      becomes C# `m.Success`; same observable semantics.
    idiom_id: null
    research_finding_id: rf-dart-regexp-firstmatch-to-csharp-regex-match-with-success
    nuance: >-
      Nullable-vs-Success-property nuance (LOAD-BEARING — explicitly
      addressed): Dart and C# adopt DIFFERENT conventions for
      "no-match": Dart returns `null`, C# returns a non-null Match
      object with `Success == false`. Codegen MUST translate `m
      != null` to `m.Success`, NOT to `m != null` (which would
      always be true in C#). Group-access nuance: `m.group(int)`
      Dart → `m.Groups[int].Value` C#. The `.Value` property is
      the matched substring (Microsoft Learn 'Group.Value' —
      `https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.capture.value`).
      Group-not-matched nuance: if a capture group did NOT match
      (e.g., an optional `?` group missed), Dart returns null
      from `group(int)`; C# returns `string.Empty` from
      `Groups[int].Value` with `Groups[int].Success == false`.
      For THIS file's pattern (all three capture groups are
      mandatory — no `?` quantifier), this divergence cannot be
      observed; the codegen does not need to compensate. NEW idiom
      row recorded.

  - construct_key: dart.for_in.iterate_map_entries
    source_form: >-
      "for (final entry in agents.entries) {
         print('--- Initializing ${entry.key} ---');
         await entry.value.initialize();
       }
       for (final entry in snapshot.entries) {
         final dest = entry.key;
         final agent = agents[dest];
         if (agent == null) {
           print('  Round $rounds: Unknown destination: $dest');
           continue;
         }
         for (final (from, payload) in entry.value) {
           print('  Round $rounds: $from -> $dest (${payload.length} bytes)');
           await agent.onMadMessageReceived(from, payload);
         }
       }"
    target_decision: >-
      Dart `for (final entry in <Map>.entries)` → C# `foreach (var
      entry in <Dictionary>)` (the `.entries` selector DROPS because
      C# `Dictionary<K,V>` enumerates as `KeyValuePair<K,V>`
      natively). REUSE the cached idiom
      `rf-dart-map-entries-iteration-to-csharp-dictionary-foreach`
      from `test/test_agent_init_goal.dart.md`. Concrete emissions:
      - `for (final entry in agents.entries)` → `foreach (var
        entry in agents)` (entry is `KeyValuePair<string,
        AgentRuntime>`); body uses `entry.Key` and `entry.Value
        .InitializeAsync()` with `await` preserved.
      - `for (final entry in snapshot.entries)` → `foreach (var
        entry in snapshot)`; body destructures dest = entry.Key,
        looks up agents[dest] with null-check, continues on miss.
      - `for (final (from, payload) in entry.value)` → `foreach
        (var (from, payload) in entry.Value)` (C# 7+ tuple
        destructuring in foreach — Microsoft Learn 'Deconstructing
        tuples and other types' —
        `https://learn.microsoft.com/dotnet/csharp/fundamentals/functional/deconstruct`).
        The element type is `(string from, byte[] payload)` per
        the dart.record_type construct.
    idiom_id: rf-dart-map-entries-iteration-to-csharp-dictionary-foreach
    research_finding_id: rf-dart-map-entries-iteration-to-csharp-dictionary-foreach
    nuance: >-
      Iteration-shape nuance (KB cache hit per FR-012 / SC-007 —
      REUSED): Dart REQUIRES `.entries`; C# does NOT (Dictionary IS
      IEnumerable<KeyValuePair>). The first foreach AWAITS inside
      the body — preserved as `await entry.Value.InitializeAsync();`
      per the SUT convspec
      `lib/multiagent/agent_runtime.dart.md`'s pinned
      `Task InitializeAsync()` method name (the Dart
      `initialize` becomes `InitializeAsync` due to its `Future<void>`
      return type plus .NET naming convention for async methods).
      The second foreach is NESTED with tuple destructuring on the
      inner loop — C# 7+ supports this directly. Continue-statement
      nuance: `continue;` Dart → `continue;` C# — identical syntax.
      Per-iteration-fresh-variable nuance: C# 5+ guarantees a fresh
      foreach variable per iteration (`agent` is captured fresh each
      iteration); the C# port preserves Dart's per-iteration capture
      semantics.

  - construct_key: dart.method_call.dictionary_indexer_returning_nullable
    source_form: >-
      "final agent = agents[dest];
       if (agent == null) {
         print('  Round $rounds: Unknown destination: $dest');
         continue;
       }"
    target_decision: >-
      Dart `Map<K, V>.operator[]` returns `V?` (nullable). C#
      `Dictionary<K, V>.this[K]` THROWS `KeyNotFoundException` on
      miss (NOT null) — the SAME divergence as the cached idiom
      `rf-dart-map-indexer-nullable-to-csharp-trygetvalue` from
      `test/test_agent_init_goal.dart.md`. Faithful translation:
      `if (!agents.TryGetValue(dest, out var agent)) {
         Console.WriteLine($"  Round {rounds}: Unknown destination: {dest}");
         continue;
       }` Microsoft Learn authoritative basis: same Dictionary.
      TryGetValue page cited in the precedent.
    idiom_id: rf-dart-map-indexer-nullable-to-csharp-trygetvalue
    research_finding_id: rf-dart-map-indexer-nullable-to-csharp-trygetvalue
    nuance: >-
      Missing-key nuance (LOAD-BEARING — REUSED from
      `test/test_agent_init_goal.dart.md`): the mechanical
      `var agent = agents[dest];` would THROW on missing key,
      losing the early-continue semantics. `TryGetValue` is the
      canonical null-safe lookup. The `out var agent` form
      narrows `agent` to non-null `AgentRuntime` in the
      remainder of the iteration. NRT-vs-NNBD nuance: Dart NNBD
      makes `agents[dest]` return `AgentRuntime?`; C# NRT makes
      `TryGetValue` return `bool` with `out var agent` narrowed.
      Both languages enforce safety at the type-system level.
      Routing-loop-resilience nuance (explicitly addressed): the
      "Unknown destination" diagnostic is the fault-tolerance
      branch — agents named 'eve'/'frank' could appear in
      `pendingMessages` if the GLP program incorrectly sends to
      a nonexistent peer; the loop prints a diagnostic and
      continues. Both languages preserve this contract exactly.

  - construct_key: dart.method_call.is_not_empty_on_map
    source_form: "pendingMessages.isNotEmpty"
    target_decision: >-
      Dart `Map<K, V>.isNotEmpty` is a getter (no parentheses); C#
      `Dictionary<K, V>` has NO `IsNotEmpty` property — the
      idiomatic check is `.Count > 0` (Microsoft Learn
      'Dictionary<TKey,TValue>.Count' —
      `https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.count`).
      Concrete: `pendingMessages.Count > 0` inside the `while`
      condition `while (pendingMessages.Count > 0 && rounds < 30)`.
      REUSE the cached idiom from `test/debug_negative.dart.md` —
      `rf-dart-string-and-iterable-members-to-dotnet` —
      the `isNotEmpty` mapping; no re-research.
    idiom_id: rf-dart-string-and-iterable-members-to-dotnet
    research_finding_id: rf-dart-string-and-iterable-members-to-dotnet
    nuance: >-
      Getter-vs-property nuance (KB cache hit per FR-012 / SC-007 —
      REUSED): Dart `isNotEmpty` is a documented getter on `Map`,
      `List`, `Set`, `Iterable`, `String`; C# has no equivalent
      property. The Dart Map `isNotEmpty` is true iff `length > 0`;
      C# `Dictionary.Count > 0` is the direct equivalent. Both
      check key-count, not value-emptiness; both are O(1).

  - construct_key: dart.method_call.list_isnotempty_on_filtered_list
    source_form: >-
      "final tagged = outputs[id]!.where((l) => l.contains('tagged(')).toList();
       print('\\n$id tagged output (${tagged.length}):');"
    target_decision: >-
      Dart `List<T>.length` (getter, returns int) → C# `List<T>.
      Count` (property, returns int) — Microsoft Learn 'List<T>.
      Count' (`https://learn.microsoft.com/dotnet/api/system.
      collections.generic.list-1.count`). Per the string-interpolation
      construct above the call site becomes `$"\n{id} tagged output
      ({tagged.Count}):"`. REUSE the cached idiom (same row as
      isNotEmpty mapping).
    idiom_id: rf-dart-string-and-iterable-members-to-dotnet
    research_finding_id: rf-dart-string-and-iterable-members-to-dotnet
    nuance: >-
      List-length-vs-array-Length nuance (KB cache hit — REUSED):
      `payload.length` IS `byte[].Length` (array, .Length is
      canonical); `tagged.length` IS `List<string>.Count`
      (generic List, .Count is canonical). Codegen MUST consult
      the static type at each interpolation site. NO unification
      attempt is made — the C# language enforces these as distinct
      property names per type.

  - construct_key: dart.method_call.string_contains
    source_form: >-
      "msg.contains('RUN:');
       msg.contains('ERROR');
       msg.contains('SEND_MAD');
       l.contains('tagged(');"
    target_decision: >-
      Dart `String.contains(Pattern)` → C# `string.Contains(string)`
      (Microsoft Learn 'String.Contains' —
      `https://learn.microsoft.com/dotnet/api/system.string.contains`).
      Both default to ordinal+case-sensitive comparison. Concrete:
      `msg.Contains("RUN:")`, `msg.Contains("ERROR")`,
      `msg.Contains("SEND_MAD")`, `l.Contains("tagged(")`. REUSE
      the cached idiom from `test/debug_negative.dart.md`.
    idiom_id: rf-dart-string-and-iterable-members-to-dotnet
    research_finding_id: rf-dart-string-and-iterable-members-to-dotnet
    nuance: >-
      Pattern-vs-string nuance (KB cache hit per FR-012 / SC-007 —
      REUSED): Dart `String.contains` accepts a `Pattern` (string
      OR regex); the call sites here pass `String` literals only,
      so the C# `string.Contains(string)` overload is the direct
      counterpart. Case-sensitivity nuance: both default to
      ordinal+case-sensitive — identical observable matching.

  - construct_key: dart.method_call.list_add_with_record_value
    source_form: >-
      "outputs[id]!.add(line);
       pendingMessages.putIfAbsent(to, () => []).add((id, payload));"
    target_decision: >-
      Dart `List<T>.add(T)` → C# `List<T>.Add(T)` (Microsoft Learn
      'List<T>.Add' — `https://learn.microsoft.com/dotnet/api/
      system.collections.generic.list-1.add`). The second call
      site's argument is a record `(id, payload)` → C# value-tuple
      construction `(id, payload)` of type `(string, byte[])`.
      Per the dart.method_call.map_putifabsent_default_factory
      construct above, the `putIfAbsent` expression decomposes
      into a `TryGetValue`+early-init block; the final `.add(...)`
      becomes `list.Add((id, payload));`. Per the
      dart.list_indexer_with_null_assertion construct above, the
      first call site collapses to `outputs[id].Add(line);`.
    idiom_id: rf-dart-string-and-iterable-members-to-dotnet
    research_finding_id: rf-dart-string-and-iterable-members-to-dotnet
    nuance: >-
      Reference-mutation nuance (KB cache hit — REUSED): both Dart
      `List<T>.add` and C# `List<T>.Add` mutate the underlying list
      in place (no new list allocated, no return-value); both
      amortise O(1). The C# `Add` returns void, same as Dart `add`.
      Value-vs-reference nuance: the tuple `(id, payload)` is a
      value-type `ValueTuple<string, byte[]>` in C# — added BY
      VALUE to the list, but the `byte[]` field inside the tuple
      is a reference to the same buffer. Identical observable
      behaviour as the Dart record-into-list `add`.

  - construct_key: dart.method_call.agent_initialize_awaited
    source_form: "await entry.value.initialize();"
    target_decision: >-
      Dart `AgentRuntime.initialize()` returns `Future<void>` (per
      `lib/multiagent/agent_runtime.dart` line 114); maps to C#
      `Task InitializeAsync()` per the pinned SUT convspec
      `lib/multiagent/agent_runtime.dart.md`. Concrete: `await
      entry.Value.InitializeAsync();`. Per .NET naming convention
      (Microsoft Learn 'Async Programming Guidance — async-await
      naming convention'), Task-returning methods are suffixed
      `Async`; the SUT convspec applies that rule. The `await`
      preserves single-threaded sequential semantics — each
      agent's `InitializeAsync` completes before the next iteration
      begins.
    idiom_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    research_finding_id: rf-dart-async-method-future-void-to-csharp-async-task-with-asyncsuffix
    nuance: >-
      Async-naming-convention nuance (KB cache hit per FR-012 /
      SC-007 — REUSED from `lib/multiagent/agent_runtime.dart.md`):
      Dart `Future<T>`-returning methods do NOT have an `Async`
      suffix; .NET convention DOES — `Task<T>` and `Task`-returning
      methods are suffixed `Async`. Codegen MUST consult the SUT
      convspec for the canonical PascalCased name; for `initialize`
      the decision is `InitializeAsync` (NOT `Initialize`). Await-
      semantics nuance: Dart `await` and C# `await` are
      semantically aligned — both pause the enclosing async
      method, schedule continuation on completion, resume with
      the awaited value or rethrow on error. Threading-model
      nuance (INHERITED — not re-escalated per FR-013): the
      `await entry.Value.InitializeAsync()` continuation may
      resume on a thread-pool worker in C# absent a
      SynchronizationContext; the SUT convspec
      `agent_runtime.dart.md` decided this is acceptable for
      the diagnostic-script host. Each agent has its own owning
      context — the four-agent fan-out exercises four contexts —
      but the AWAIT chain serialises them on the main thread.

  - construct_key: dart.method_call.agent_on_mad_message_received_awaited
    source_form: "await agent.onMadMessageReceived(from, payload);"
    target_decision: >-
      Dart `AgentRuntime.onMadMessageReceived(String from, Uint8List
      payload)` returns `Future<void>` (per
      `lib/multiagent/agent_runtime.dart` line 290); maps to C#
      `Task OnMadMessageReceivedAsync(string from, byte[] payload)`
      per the pinned SUT convspec `lib/multiagent/agent_runtime.
      dart.md`. Concrete: `await agent.OnMadMessageReceivedAsync(
      from, payload);`. The `Async` suffix and PascalCased
      identifier follow the cached
      `rf-dart-async-method-future-void-to-csharp-async-task-
      with-asyncsuffix` idiom.
    idiom_id: rf-dart-instance-method-camelcase-to-csharp-pascalcase
    research_finding_id: rf-dart-async-method-future-void-to-csharp-async-task-with-asyncsuffix
    nuance: >-
      Single-message-routing nuance (LOAD-BEARING — explicitly
      addressed and INHERITED from `lib/multiagent/agent_runtime
      .dart.md`'s OnMadMessageReceivedAsync nuance): each call
      delivers ONE message to ONE agent; the agent's internal
      MadContext processes it synchronously within the awaited
      Task. The Dart source's sequential `await` chain
      (per-snapshot, per-message) is preserved verbatim in C#.
      Multi-agent threading nuance (INHERITED): four agents,
      each with their own owning context, but only ONE message
      processed at a time — no concurrent access to any agent's
      heap. The heap_fcp threading-model escalation INHERITS as
      documented in the file-header — no re-escalation.

  - construct_key: dart.string_literal.list_of_string_with_int_args
    source_form: >-
      "['carol', '4']; ['dave', '4']; ['4'];"
    target_decision: >-
      Dart `<String>['carol', '4']` (implicit) → C# `new
      List<string> { "carol", "4" }` (collection-initialiser).
      Concrete emissions: `new List<string> { "carol", "4" }`,
      `new List<string> { "dave", "4" }`, `new List<string> { "4" }`.
      The implicit type parameter on the Dart side maps to the
      explicit C# type. Per the cached idiom
      `rf-dart-list-literal-to-csharp-list-or-collection-expression`
      from `test/debug_negative.dart.md`.
    idiom_id: rf-dart-list-literal-to-csharp-list-or-collection-expression
    research_finding_id: rf-dart-list-literal-to-csharp-list-or-collection-expression
    nuance: >-
      Numeric-string nuance (explicitly addressed): the `'4'`
      element is a STRING literal, not an int — the Dart side
      passes `'4'` because `AgentRuntime.extraArgs` is typed
      `List<String>`, and the GLP runtime converts the string to
      an arity number downstream. Codegen MUST preserve the
      string-form; emitting `4` (int) would change the C#
      list-element type to `int` and break the `AgentRuntime`
      ctor's `List<string> extraArgs` parameter contract.

  - construct_key: dart.member_access.field_chain_simple
    source_form: >-
      "entry.key; entry.value;
       payload.length; tagged.length;
       File(...).absolute.path;"
    target_decision: >-
      Dart camelCase field/property reads map to C# PascalCase per
      the owning SUT convspec OR the .NET-stdlib convention:
      - `entry.key`, `entry.value` → `entry.Key`, `entry.Value`
        (KeyValuePair<,> properties).
      - `payload.length` → `payload.Length` (byte[].Length is
        canonical .NET array Length).
      - `tagged.length` → `tagged.Count` (List<T>.Count is
        canonical .NET, NOT Length — see
        dart.method_call.list_isnotempty_on_filtered_list).
      - `File('../programs/self.glp').absolute.path` →
        `Path.GetFullPath("../programs/self.glp")` (the Dart
        two-step `File.absolute.path` collapses into the C#
        one-step `Path.GetFullPath` per Microsoft Learn 'Path.
        GetFullPath' —
        `https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath`).
        Requires `using System.IO;` (already added for
        `File.ReadAllText`).
    idiom_id: rf-dart-camelcase-field-to-csharp-pascalcase-property
    research_finding_id: rf-dart-file-absolute-path-to-csharp-path-getfullpath
    nuance: >-
      Field-vs-property nuance (KB cache hit per FR-012 / SC-007 —
      REUSED). Path.absolute.path nuance (NEW for this file —
      explicitly addressed): Dart `File('path').absolute` returns
      a NEW `File` whose path has been resolved to absolute; the
      `.path` getter then returns the absolute path string. C#
      `Path.GetFullPath(string)` does the SAME in one call —
      resolves a relative path to absolute using the current
      working directory. Both default to `Environment.
      CurrentDirectory` for resolution (Microsoft Learn:
      'Path.GetFullPath(String) resolves the path relative to
      the current directory.'). NEW idiom row
      `rf-dart-file-absolute-path-to-csharp-path-getfullpath`
      recorded. CWD-dependency nuance: the resolved path depends
      on where the process was launched (Dart source assumes
      `glp_runtime/`; C# port preserves the same assumption).

  - construct_key: dart.if_statement.unbraced_continue_with_diagnostic
    source_form: >-
      "if (agent == null) {
         print('  Round $rounds: Unknown destination: $dest');
         continue;
       }
       if (m != null) {
         print('  ${m.group(2)}: ${m.group(3)}');
       } else {
         print('  $l');
       }"
    target_decision: >-
      Dart `if (cond) { ... }` → C# `if (cond) { ... }` (1:1
      syntax). The `continue;` statement preserves verbatim. The
      `if/else` pair preserves verbatim. Concrete:
      - `if (agent == null) { ... }` → `if (!agents.TryGetValue
        (dest, out var agent)) { ... }` (see the
        dart.method_call.dictionary_indexer_returning_nullable
        construct above for the null-check-via-TryGetValue
        substitution).
      - `if (m != null) { ... } else { ... }` → `if (m.Success)
        { ... } else { ... }` (see the
        dart.regexp.firstmatch_nullable construct above for the
        Success-vs-null substitution).
    idiom_id: rf-dart-if-else-to-csharp-if-else
    research_finding_id: rf-dart-if-else-to-csharp-if-else
    nuance: >-
      Block-required nuance (KB cache hit per FR-012 / SC-007 —
      REUSED from `test/debug_negative.dart.md`): both Dart and
      C# allow brace-less single-statement bodies; this file uses
      braces consistently. Continue-vs-break nuance: Dart and C#
      have identical `continue` and `break` semantics in loops;
      the `continue` exits the current iteration of the enclosing
      `for/foreach/while` and proceeds with the next iteration.
      Null-check-conversion nuance (LOAD-BEARING — explicitly
      addressed): the original `if (agent == null)` and `if (m !=
      null)` conditions transform under the indexer-returning-
      nullable + regex-Success substitutions described in the
      respective constructs above. Codegen MUST apply those
      substitutions BEFORE this generic if/else mapping — the
      generic mapping alone would produce broken code.

  - construct_key: dart.while_loop.bounded_with_count
    source_form: >-
      "while (pendingMessages.isNotEmpty && rounds < 30) {
         rounds++;
         final snapshot = Map<String, ...>.from(pendingMessages);
         pendingMessages.clear();
         ...
       }"
    target_decision: >-
      Dart `while (cond) { body }` → C# `while (cond) { body }`
      (syntactically identical). Per the constructs above:
      - `pendingMessages.isNotEmpty` → `pendingMessages.Count > 0`.
      - `&&` short-circuit operator preserves 1:1.
      - `rounds < 30` int comparison preserves 1:1.
      Concrete: `while (pendingMessages.Count > 0 && rounds < 30)
      { rounds++; var snapshot = new Dictionary<string,
      List<(string, byte[])>>(pendingMessages); pendingMessages.
      Clear(); ... }`.
    idiom_id: null
    research_finding_id: rf-dart-while-loop-to-csharp-while-loop
    nuance: >-
      Bounded-loop-counter nuance (explicitly addressed): the
      `rounds < 30` guard prevents infinite message routing if
      the GLP program enters a send loop. The `30` is a
      diagnostic-tool-only bound; for a production-grade test it
      might be tighter or higher. Codegen preserves verbatim.
      Map-clear-then-fill nuance (LOAD-BEARING — explicitly
      addressed): the loop pattern is "snapshot the current
      pendingMessages, clear it, then route ALL snapshotted
      messages (which may re-populate pendingMessages with new
      messages for the next iteration)". This is the classic
      "actor-model round" pattern. Both Dart `Map.clear()` and
      C# `Dictionary<K,V>.Clear()` are O(n) and preserve the
      same observable behaviour (Microsoft Learn 'Dictionary.
      Clear' — `https://learn.microsoft.com/dotnet/api/system.
      collections.generic.dictionary-2.clear`). NEW idiom row
      `rf-dart-while-loop-to-csharp-while-loop` recorded (trivial
      but captures the bounded-loop discipline).

conversion_units:
  - "cu-1: file-scope using directives (System; System.IO; System.Linq; System.Text.RegularExpressions; System.Collections.Generic; <RootNs>.Multiagent) — NO using Xunit, NO using System.IO.File static-using"
  - "cu-2: namespace declaration mirroring test/ (e.g. <RootNs>.Test) — single top-level namespace"
  - "cu-3: file-header multi-line XML doc comment '/// Diagnostic: Four agents (Alice, Bob, Carol, Dave) with project modules. /// Simulates what main_cssg_mad_modules.dart does — linked project + mad_boot. /// /// Run: dart test/debug_four_agents_modules.dart' above the host static class"
  - "cu-4: top-level `public static class DebugFourAgentsModules` host (the debug-script idiom — NO test class, NO [Fact] attribute, NO ITestOutputHelper injection, NO constructor); `private static readonly Regex TaggedRegex = new(@\"^< tagged\\((\\w+), (cmd|notify)\\((.+)\\)\\)$\", RegexOptions.Compiled);` static field at class scope"
  - "cu-5: `public static async Task<int> Main(string[] args)` entrypoint — async because the body genuinely awaits AgentRuntime.InitializeAsync and AgentRuntime.OnMadMessageReceivedAsync"
  - "cu-6: Main body header — var projectDir, var bootSource (via File.ReadAllText), var rootSelfGlpPath (via Path.GetFullPath), one Console.WriteLine banner"
  - "cu-7: var pendingMessages = new Dictionary<string, List<(string from, byte[] payload)>>(); var outputs = new Dictionary<string, List<string>> { { \"alice\", new List<string>() }, { \"bob\", new List<string>() }, { \"carol\", new List<string>() }, { \"dave\", new List<string>() } };"
  - "cu-8: local function `AgentRuntime MakeAgent(string id, string goal, List<string> extra)` — constructs the AgentRuntime with named arguments, wires three callbacks (OnOutput, OnLog, OnSendMadMessage with async lambda + TryGetValue+lazy-init+Add for the putIfAbsent equivalent), returns agent. Captures `outputs`, `pendingMessages`, `bootSource`, `rootSelfGlpPath`, `projectDir` from enclosing scope"
  - "cu-9: four MakeAgent calls building alice/bob/carol/dave with their (goalLabel, extraArgs) pairs; var agents = new Dictionary<string, AgentRuntime> { { \"alice\", alice }, ... }"
  - "cu-10: per-agent initialization foreach — `foreach (var entry in agents) { Console.WriteLine($\"--- Initializing {entry.Key} ---\"); await entry.Value.InitializeAsync(); }`"
  - "cu-11: message-routing while-loop — `var rounds = 0; while (pendingMessages.Count > 0 && rounds < 30) { rounds++; var snapshot = new Dictionary<string, List<(string, byte[])>>(pendingMessages); pendingMessages.Clear(); foreach (var entry in snapshot) { var dest = entry.Key; if (!agents.TryGetValue(dest, out var agent)) { Console.WriteLine($\"  Round {rounds}: Unknown destination: {dest}\"); continue; } foreach (var (from, payload) in entry.Value) { Console.WriteLine($\"  Round {rounds}: {from} -> {dest} ({payload.Length} bytes)\"); await agent.OnMadMessageReceivedAsync(from, payload); } } }`"
  - "cu-12: per-agent tagged-output summary — `foreach (var id in new[] { \"alice\", \"bob\", \"carol\", \"dave\" }) { var tagged = outputs[id].Where(l => l.Contains(\"tagged(\")).ToList(); Console.WriteLine($\"\\n{id} tagged output ({tagged.Count}):\"); foreach (var l in tagged) { var m = TaggedRegex.Match(l); if (m.Success) { Console.WriteLine($\"  {m.Groups[2].Value}: {m.Groups[3].Value}\"); } else { Console.WriteLine($\"  {l}\"); } } }`"
  - "cu-13: final Console.WriteLine(\"\\n=== Done ===\"); return 0;"
  - "cu-14: NO xUnit attributes, NO [Fact], NO [Trait], NO DisplayName — this file is a console-exe diagnostic harness, NOT a test fixture"
  - "cu-15: DOWNSTREAM GATE / LANGPAIR concern (recorded, not asserted): csproj orchestration — whether to compile this file as a SEPARATE diagnostic exe, an auxiliary entrypoint inside the test exe, or include it as a [Fact(Skip = \"manual diagnostic\")] no-op — is a langpair-level decision; this artifact records the static-Main shape and lets the langpair finalize the .csproj wiring"

escalations: []
```

## Rationale and research provenance

### Why static-Main console-exe (not [Fact]) — host-shape decision (KB cache hit)

This file is NOT a `package:test` file: no `package:test` import, no
`test(...)`, no `group(...)`, no `expect(...)`, no matchers — exclusively
`print(...)` diagnostics across 12+ call sites. The host shape decision
REUSES the cached idiom `rf-dart-debug-script-main-to-csharp-static-main`
established in `test/debug_negative.dart.md` and reapplied in
`test/test_constant_compile.dart.md` and `test/test_agent_init_goal.dart.md`.
The single NEW wrinkle vs. those precedents is that the Dart `async`
keyword on `main()` carries REAL `await` calls (`await entry.value
.initialize()` and `await agent.onMadMessageReceived(from, payload)`).
Per `test_agent_init_goal.dart.md`'s nuance, the `async` keyword DROPS
only if the body has zero `await` — here that drop-check does NOT pass,
so the C# port MUST preserve `async`: `public static async Task<int>
Main(string[] args)` (.NET 7.0+; Microsoft Learn 'Main method and
command-line arguments' confirms async Main is supported since C# 7.1
/ .NET Core 2.0). The new sub-idiom row
`rf-dart-debug-script-async-main-to-csharp-async-task-main` is recorded
for this variant; it's a CLOSE COUSIN of the precedent, not a conflict
— both rows are first-class entries in the KB, selected by per-file
inspection of `await`-presence.

### Console.WriteLine, not ITestOutputHelper (host-shape-conditional, KB cache hit)

REUSED from `test/debug_negative.dart.md` /
`test/test_constant_compile.dart.md` /
`test/test_agent_init_goal.dart.md` via the idiom
`rf-dart-print-in-console-exe-to-console-writeline`. Since the host is
`static async Task<int> Main` (NOT `[Fact]`), every `print(...)` routes
to `Console.WriteLine(...)`. Authoritative basis: Microsoft Learn
`Console.WriteLine` (`https://learn.microsoft.com/dotnet/api/system.
console.writeline`).

### File.ReadAllText and Path.GetFullPath for dart:io File operations (KB cache hit + NEW)

Two dart:io patterns are exercised:
1. `File('path').readAsStringSync()` → `File.ReadAllText(path)` —
   REUSED from `test/test_agent_init_goal.dart.md` via the cached
   idiom `rf-dart-dart-io-file-readasstringsync-to-system-io-file-
   readalltext`.
2. `File('path').absolute.path` → `Path.GetFullPath(path)` — NEW idiom
   row `rf-dart-file-absolute-path-to-csharp-path-getfullpath`
   recorded. Authoritative basis: Microsoft Learn 'Path.GetFullPath'
   (`https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath`).
   The Dart two-step (construct File handle + `.absolute` + `.path`)
   collapses into the C# one-step static call.

### Dictionary.TryGetValue for Map<K,V> indexer (KB cache hit)

REUSED from `test/test_agent_init_goal.dart.md` via the cached idiom
`rf-dart-map-indexer-nullable-to-csharp-trygetvalue`. The lookup
`agents[dest]` where `agents` is `Map<String, AgentRuntime>` returns
`AgentRuntime?` Dart-side but THROWS C#-side without `TryGetValue`.
Microsoft Learn authoritative basis: 'Dictionary<TKey,TValue>.
TryGetValue' (`https://learn.microsoft.com/dotnet/api/system.
collections.generic.dictionary-2.trygetvalue`).

### Map.putIfAbsent → TryGetValue + lazy-init + Add (NEW idiom)

LOAD-BEARING new idiom for this file:
`rf-dart-map-putifabsent-to-csharp-trygetvalue-out-with-lazy-init`.
Dart `Map.putIfAbsent(K, V Function())` returns the existing value
OR lazily constructs and inserts. The .NET counterpart is the
explicit `TryGetValue`-out + early-init block, NOT `TryAdd` (which
would always allocate). Authoritative basis: Microsoft Learn
'Dictionary<TKey,TValue>.TryGetValue' (cited above). The lazy-factory
contract is preserved: the `new List<>()` allocation only happens
on the absent branch.

### Map<K,V>.from → Dictionary<K,V>(IDictionary) (NEW idiom)

LOAD-BEARING new idiom for this file:
`rf-dart-map-from-named-ctor-to-csharp-dictionary-copy-ctor`. Dart
`Map<K,V>.from(other)` is a SHALLOW copy named-constructor; .NET
`new Dictionary<K,V>(other)` is the canonical copy-constructor.
Both produce shallow copies — key set freshly allocated, values
shared by reference. The file's pattern (snapshot + clear original
+ iterate snapshot) relies on this exact shape; codegen MUST preserve.
Authoritative basis: Microsoft Learn Dictionary copy-constructor
(`https://learn.microsoft.com/dotnet/api/system.collections.generic.
dictionary-2.-ctor#system-collections-generic-dictionary-2-ctor(system-
collections-generic-idictionary((-0-1)))`).

### RegExp(r'...') + Match.group(int) → Regex(@"...") + Match.Groups[int].Value (NEW idiom)

Two NEW idiom rows:
1. `rf-dart-regexp-raw-literal-to-csharp-regex-verbatim-static-readonly` —
   Dart raw `r'^< tagged\(...\)$'` regex → C# verbatim `@"^< tagged
   \(...\)$"`; declared as `private static readonly Regex TaggedRegex
   = new(@"...", RegexOptions.Compiled);` for repeated use.
   Authoritative basis: Microsoft Learn 'Regex Class' + 'Regular
   expression language quick reference' + 'Best practices for regular
   expressions'.
2. `rf-dart-regexp-firstmatch-to-csharp-regex-match-with-success` —
   Dart `RegExp.firstMatch` returns `Match?`; C# `Regex.Match` returns
   non-null `Match` with `.Success` property. The `m != null` Dart
   check becomes `m.Success` C#. Group access: `m.group(int)` →
   `m.Groups[int].Value`. Authoritative basis: Microsoft Learn
   'Regex.Match(String)' + 'Match.Success' + 'Group.Value'.

### Uint8List → byte[] with import-erasure (KB cache hit + NEW import-side row)

REUSED the project-pinned `Uint8List → byte[]` rule from
`lib/multiagent/agent_runtime.dart.md`. NEW for this file is the
import-side decision: `import 'dart:typed_data';` drops with NO C#
counterpart `using` (since `byte[]` lives in implicit `System`).
NEW idiom row `rf-dart-uint8list-import-to-csharp-byte-array-no-
using-needed`. Authoritative basis: Microsoft Learn 'Arrays (C#
Programming Guide)' + dart.dev `Uint8List` documentation.

### Positional record types → ValueTuple (KB cache hit)

REUSED from `test/test_agent_init_goal.dart.md` /
`test/heap/binding_pointer_test.dart.md` via the cached idiom
`rf-dart-record-return-to-csharp-valuetuple`. Dart `(String,
Uint8List)` positional record → C# `(string, byte[])` value tuple
(`ValueTuple<string, byte[]>`). For documentation the C# port adds
named tuple labels `(string from, byte[] payload)`. Foreach
destructuring `for (final (from, payload) in list)` → `foreach (var
(from, payload) in list)`.

### Local function with closures (NEW idiom)

LOAD-BEARING new idiom for this file:
`rf-dart-local-function-with-captures-to-csharp-local-function`. Dart
local function `AgentRuntime makeAgent(...)` declared inside `main` →
C# local function `AgentRuntime MakeAgent(...)` declared inside `Main`
(C# 7.0+ feature). Both support closure capture of enclosing locals.
Authoritative basis: Microsoft Learn 'Local functions'
(`https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-
and-structs/local-functions`).

### Lambda callback assignments + async lambda (NEW idiom)

Multiple callback assignments (`agent.onOutput = (line) { ... };`,
`agent.onLog = (tag, msg) { ... };`, `agent.onSendMadMessage = (to,
payload) async { ... };`) map to C# delegate-property lambda
assignments. The async lambda preserves `async` per the pinned
`OnSendMadMessage: Func<string, byte[], Task>` signature in
`agent_runtime.dart.md`. NEW idiom row
`rf-dart-callback-assignment-lambda-to-csharp-delegate-property-lambda`
recorded. Authoritative basis: Microsoft Learn 'Lambda expressions'.

### Inherited multi-agent threading-model escalation (FR-013)

LOAD-BEARING per FR-013: this file drives FOUR `AgentRuntime`
instances simultaneously through their `InitializeAsync` and
`OnMadMessageReceivedAsync` lifecycles. Each agent has its own
`HeapFCP` owning context. The `heap_fcp.dart` escalations[0]
(threading model) propagates INTO `agent_runtime.dart.md`, which
INTO this file. Per the sibling-multiagent precedent
(`mad_context.dart.md` / `global_send.dart.md` / `message_queue.
dart.md` / `scheduler.dart.md` / `system_predicates_impl.dart.md` /
`body_kernels.dart.md` / `runner.dart.md` / `agent_runtime.dart.md`
all INHERIT WITHOUT RE-ESCALATING), THIS file INHERITS. The
sequential `await` chain serialises all multi-agent activity on
the main thread; no concurrent access to any agent's heap arises.
No genuinely-LOCAL undecidable point. `escalations: []`.

### Why no escalations

Every construct has a single-decision target shape grounded in
official Dart and .NET / Microsoft Learn documentation. The KB
cache hits
(`rf-dart-internal-package-import-to-csharp-using`,
`rf-dart-tripleslash-doc-to-csharp-xml-doc`,
`rf-dart-dart-io-file-readasstringsync-to-system-io-file-readalltext`,
`rf-dart-debug-script-main-to-csharp-static-main`,
`rf-dart-print-in-console-exe-to-console-writeline`,
`rf-dart-string-interpolation-to-csharp-interpolated-string`,
`rf-dart-final-local-to-csharp-var-local`,
`rf-dart-var-mutable-local-to-csharp-var-local`,
`rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`,
`rf-dart-typed-map-literal-to-csharp-dictionary-collection-init`,
`rf-dart-record-return-to-csharp-valuetuple`,
`rf-dart-string-and-iterable-members-to-dotnet`,
`rf-dart-map-indexer-nullable-to-csharp-trygetvalue`,
`rf-dart-map-entries-iteration-to-csharp-dictionary-foreach`,
`rf-dart-list-literal-to-csharp-list-or-collection-expression`,
`rf-dart-camelcase-field-to-csharp-pascalcase-property`,
`rf-dart-if-else-to-csharp-if-else`,
`rf-dart-instance-method-camelcase-to-csharp-pascalcase`)
are stable project-wide pins. The NEW idioms introduced
(`rf-dart-uint8list-import-to-csharp-byte-array-no-using-needed`,
`rf-dart-debug-script-async-main-to-csharp-async-task-main`,
`rf-dart-map-from-named-ctor-to-csharp-dictionary-copy-ctor`,
`rf-dart-local-function-with-captures-to-csharp-local-function`,
`rf-dart-callback-assignment-lambda-to-csharp-delegate-property-lambda`,
`rf-dart-bang-assert-on-map-indexer-to-csharp-dictionary-indexer-direct`,
`rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist`,
`rf-dart-map-putifabsent-to-csharp-trygetvalue-out-with-lazy-init`,
`rf-dart-regexp-raw-literal-to-csharp-regex-verbatim-static-readonly`,
`rf-dart-regexp-firstmatch-to-csharp-regex-match-with-success`,
`rf-dart-file-absolute-path-to-csharp-path-getfullpath`,
`rf-dart-async-method-future-void-to-csharp-async-task-with-asyncsuffix`,
`rf-dart-while-loop-to-csharp-while-loop`)
each have a single authoritative target shape from official docs and
will become KB cache hits for subsequent files exercising the same
patterns. The threading-model question is INHERITED (not re-escalated)
per FR-013 + the documented sibling-multiagent precedent.
`escalations: []` is therefore intentional, not a placeholder.

## Notes

- This file is the first in the inventory to exercise the multi-agent
  fan-out pattern (4 simultaneously-bootstrapped `AgentRuntime`
  instances). All threading-discipline decisions are INHERITED from
  `lib/multiagent/agent_runtime.dart.md` and ultimately from
  `lib/runtime/heap_fcp.dart` escalations[0]. NO new escalation arises
  at the file level — the test driver runs all four agents sequentially
  on the main thread via `await` serialisation.
- Latent codegen-fidelity nuances NOT asserted as load-bearing:
  (a) `SynchronizationContext` consideration: console-exe async Main
  runs continuations on the thread-pool by default; the
  `pendingMessages` Dictionary mutations after `await` could in
  principle race if multiple agents' `OnMadMessageReceivedAsync` ran
  concurrently — but the loop awaits each call sequentially, so no
  race exists. Codegen MAY add a `// single-threaded sequential —
  no race` comment for diagnostic clarity.
  (b) `RegexOptions.Compiled` flag: recommended for repeated use per
  Microsoft Learn 'Best practices for regular expressions'; included
  in cu-4 but may be removed if cold-start time matters more than
  per-match cost.
  (c) `TaggedRegex` static field naming: codegen MAY rename to
  `_taggedRegex` for underscore-private convention, OR keep PascalCased
  per Microsoft .NET naming conventions for `private static readonly`
  fields (both forms are accepted; the SDK guideline prefers PascalCase
  for static fields).
  (d) The Dart-source `outputs[id]!` non-null-assertion on a definite-
  key map indexer translates to a plain `outputs[id]` indexer in C#
  (the `!` ceremony erases — see
  `rf-dart-bang-assert-on-map-indexer-to-csharp-dictionary-indexer-
  direct` idiom row). A future Dart source that uses the `!` on a
  POTENTIALLY-absent key would translate differently (would require
  `TryGetValue`); the codegen MUST consult the call-site context to
  decide between the two forms.
- The `agent.onLog` lambda's filter `msg.contains('RUN:') ||
  msg.contains('ERROR') || msg.contains('SEND_MAD')` is a diagnostic
  rate-limiter — only emit log lines mentioning these three tokens.
  Preserved verbatim. The `||` short-circuit semantics agree across
  both languages; the three contains-checks evaluate left-to-right
  until the first hit.
- The Dart `String.toUpperCase()` is NOT exercised in this file (it
  IS exercised inside `AgentRuntime._tag` — handled by
  `agent_runtime.dart.md` already). No `ToUpperInvariant` decision
  required at this file's scope.
- The `// Initialize all agents` and `// Route messages` Dart comments
  preserve as C# `//` comments verbatim — no idiom row needed (1:1
  syntax).
