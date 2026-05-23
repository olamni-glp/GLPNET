---
path: test/multiagent/multiagent_modules_test.dart
cycle_group_id: 153
scc_siblings: []
generated_at: 2026-05-21T16:56:15Z
source_sha256: b7dd09684ae1c0f399f6137c4dff0c3f37a624a83e6a703a16ad7161eb094b78
schema_version: 1
---

# Conversion Plan: test/multiagent/multiagent_modules_test.dart

## 1. Source Analysis

The source is a 130-line `package:test`-based multi-isolate integration test. Inspection of the .dart file confirms:

- **Imports (lines 7–10)**: `dart:io` (for `File`/`Directory`); `package:test/test.dart` (xUnit-counterpart framework); two SUT imports `package:glp_runtime/multiagent/boot_loader.dart` + `package:glp_runtime/multiagent/isolate_manager.dart`.
- **Top-level const (lines 19–86)**: `const cssgPlay4BootSource = r'''...''';` — a raw triple-quoted madGLP boot source carrying the CSSG play 4 boot clauses (`boot`, `parent_init/4`, `child_init/3`, `extract_child_stream/2`, `ui_actor/3` dispatch for alice/bob/carol/dave). The raw `r` prefix disables backslash escapes; the literal contains no `"` characters.
- **`void main()` (lines 88–130)** with structure:
  - `final projectDir = '../programs/cssg_modules';` — relative fixture path.
  - Skip-if-missing guard: `if (!Directory(projectDir).existsSync()) { print(...); return; }` — silently returns before registering any test.
  - One `group('Multi-isolate with project-compiled modules', () { ... })` containing:
    - `late IsolateManager manager;` — late-init field closed over by setUp/tearDown/test.
    - `setUp(() { manager = IsolateManager(); });` — synchronous per-test allocation.
    - `tearDown(() async { await manager.shutdown(); });` — async per-test cleanup.
    - One `test('boots CSSG play 4 with project-linked modules', () async { ... }, timeout: Timeout(Duration(seconds: 30)));` containing:
      - `final loader = BootLoader();`
      - `final config = loader.load(cssgPlay4BootSource);`
      - `config.rootSelfGlpPath = File('../programs/self.glp').absolute.path;`
      - `config.projectDir = projectDir;`
      - `expect(config.directives.length, equals(4));`
      - `expect(config.directives.map((d) => d.agentId).toSet(), equals({'alice', 'bob', 'carol', 'dave'}));`
      - `await manager.boot(config, traceConfig: TraceConfig(glp: false, mad: true));`
      - `manager.start();`
      - `await Future.delayed(Duration(seconds: 5));`
      - No explicit `expect(...)` after the sleep — final inline comment ("If we reach here without crash, boot + project loading + execution works") declares the smoke-test pass criterion.
- **Inherited escalation site**: file boots `IsolateManager`, whose dart:isolate threading-model escalation is recorded ONCE at `lib/multiagent/isolate_manager.dart.md` escalations[0]. THIS test's external call sites (`new IsolateManager()`, `Boot(...)`, `Start()`, `Shutdown()`) are option-INDEPENDENT.

## 2. Dart → C#/.NET Conversion Plan

Each Dart construct maps to its C# counterpart per the ratified convspec. Listed in source order:

1. **`import 'dart:io';`** → file-scope `using System.IO;`. `File`/`Directory` resolve via `System.IO.File` / `System.IO.Directory`. Cached idiom `rf-dart-dart-io-to-csharp-system-io`.
2. **`import 'package:test/test.dart';`** → file-scope `using Xunit;`. Project-wide pin: every `package:test` file → xUnit. Cached idiom `rf-dart-package-test-import-to-xunit-using`.
3. **`import 'package:glp_runtime/multiagent/boot_loader.dart';` + `import 'package:glp_runtime/multiagent/isolate_manager.dart';`** → ONE coalesced `using <RootNs>.Multiagent;` (both SUTs share the same C# namespace per the sibling SUT specs). Cached idiom `rf-dart-internal-package-import-to-csharp-using`.
4. **Additional file-scope usings** (implied by codegen needs): `using System.Linq;` (for `.Select(...).ToHashSet()`); `using System.Threading.Tasks;` (for `Task`, `ValueTask`, `Task.Delay`); `using System.Collections.Generic;` (for `HashSet<T>`).
5. **Namespace declaration** mirroring the `test/multiagent` directory: `namespace <RootNs>.Test.Multiagent;`.
6. **`const cssgPlay4BootSource = r'''...''';`** → file-scope helper class:
   ```
   internal static class MultiagentModulesTestHelpers {
       internal const string CssgPlay4BootSource = @"...";
       internal const string ProjectDir = "../programs/cssg_modules";
   }
   ```
   C# verbatim `@"..."` preserves the raw payload byte-identically (no `"` chars in source). The `///` doc-comment lines above the Dart const elevate to `/// <summary>` XML-doc on the C# const. Idiom `rf-dart-toplevel-const-multiline-string-to-csharp-helper-class-verbatim-const`.
7. **`void main()`** → dropped entirely (xUnit discovers `[Fact]`s by reflection; no per-file entrypoint). Cached idiom `rf-dart-package-test-main-omit-in-xunit`.
8. **`final projectDir = '../programs/cssg_modules';`** → hoisted to `MultiagentModulesTestHelpers.ProjectDir` const (see step 6).
9. **`if (!Directory(projectDir).existsSync()) { print(...); return; }`** → relocated to the FIRST statement of every `[Fact]` body as `Assert.SkipUnless(Directory.Exists(MultiagentModulesTestHelpers.ProjectDir), "cssg_modules not found at " + MultiagentModulesTestHelpers.ProjectDir + ", skipping tests");` (xUnit v3). Idiom `rf-dart-package-test-runtime-skip-to-xunit-assert-skip`.
10. **`group('Multi-isolate with project-compiled modules', () { ... });`** → `public class MultiIsolateWithProjectCompiledModulesTests : IAsyncLifetime { ... }`, decorated with `[Trait("Group", "Multi-isolate with project-compiled modules")]`. Cached idiom `rf-dart-package-test-group-to-xunit-class`.
11. **`late IsolateManager manager;`** → `private IsolateManager _manager = null!;` instance field. Cached idiom `rf-dart-late-field-to-csharp-nullforgiving-field`.
12. **`setUp(() { manager = IsolateManager(); });`** → public ctor body: `public MultiIsolateWithProjectCompiledModulesTests() { _manager = new IsolateManager(); }`. Cached idiom `rf-dart-setup-to-xunit-constructor`.
13. **`IAsyncLifetime.InitializeAsync`** (required by interface; no Dart counterpart in this file) → `public ValueTask InitializeAsync() => ValueTask.CompletedTask;` (no-op).
14. **`tearDown(() async { await manager.shutdown(); });`** → `public async ValueTask DisposeAsync() { await _manager.Shutdown(); }`. Idiom `rf-dart-async-teardown-to-xunit-iasynclifetime-disposeasync`. The SUT's `Shutdown` method name (Dart `shutdown` → PascalCase `Shutdown`) is pinned by `lib/multiagent/isolate_manager.dart.md`.
15. **`test('boots CSSG play 4 with project-linked modules', () async { ... }, timeout: Timeout(Duration(seconds: 30)));`** → `[Fact(DisplayName = "boots CSSG play 4 with project-linked modules", Timeout = 30000)] public async Task BootsCssgPlay4WithProjectLinkedModules() { ... }`. Cached idiom `rf-dart-test-callback-to-xunit-method-body`. Unit conversion 30 s → 30000 ms is mandatory.
16. **`final loader = BootLoader();`** → `var loader = new BootLoader();`. Cached idiom `rf-dart-final-local-to-csharp-var-local` + mandatory C# `new`.
17. **`final config = loader.load(cssgPlay4BootSource);`** → `var config = loader.Load(MultiagentModulesTestHelpers.CssgPlay4BootSource);`. Same idiom; Dart `load` → C# `Load` (PascalCase).
18. **`config.rootSelfGlpPath = File('../programs/self.glp').absolute.path;`** → `config.RootSelfGlpPath = Path.GetFullPath("../programs/self.glp");`. Idiom `rf-dart-bootconfig-mutable-field-to-csharp-getset-property` + sub-mapping `file.absolute.path → Path.GetFullPath` from `rf-dart-dart-io-to-csharp-system-io`.
19. **`config.projectDir = projectDir;`** → `config.ProjectDir = MultiagentModulesTestHelpers.ProjectDir;`. Same property idiom; relies on `BootConfig.ProjectDir` being `{ get; set; }` per the SUT spec.
20. **`expect(config.directives.length, equals(4));`** → `Assert.Equal(4, config.Directives.Count);` (mandatory arg-order flip; `.length` → `.Count` for `IReadOnlyList<T>`). Cached idiom `rf-dart-expect-equals-to-xunit-assertequal`.
21. **`expect(config.directives.map((d) => d.agentId).toSet(), equals({'alice', 'bob', 'carol', 'dave'}));`** → `Assert.Equal(new HashSet<string> { "alice", "bob", "carol", "dave" }, config.Directives.Select(d => d.AgentId).ToHashSet(), HashSet<string>.CreateSetComparer());`. Idiom `rf-dart-iterable-map-toset-equals-to-xunit-assertequal-with-setcomparer`. Set-equality (order-independent) via `HashSet<T>.CreateSetComparer()`.
22. **`await manager.boot(config, traceConfig: TraceConfig(glp: false, mad: true));`** → `await _manager.Boot(config, new TraceConfig { Glp = false, Mad = true });`. Dart `traceConfig:` named-arg label DROPPED (SUT signature is positional per `isolate_manager.dart.md`). `TraceConfig` constructor → object-initializer syntax (SUT pins init-only properties). Idiom `rf-dart-namedargs-call-with-traceconfig-named-ctor-to-csharp-positional-call-with-object-initializer`.
23. **`manager.start();`** → `_manager.Start();` (synchronous, void-returning; no `await`). Idiom `rf-dart-instance-method-void-call-to-csharp-pascal-method-call`.
24. **`await Future.delayed(Duration(seconds: 5));`** → `await Task.Delay(TimeSpan.FromSeconds(5));`. Idiom `rf-dart-future-delayed-duration-to-csharp-task-delay-timespan`. `Thread.Sleep` REJECTED (blocking, deadlock risk).
25. **Inline `//` comments** ("Verify directives parsed correctly", "Boot all 4 agents", "Start and let the protocol run", "If we reach here without crash, boot + project loading + execution works") → preserved verbatim as C# `//` comments (1:1 syntax). Idiom `rf-dart-line-comment-to-csharp-line-comment`. NO synthetic `Assert.True(true)` injected — absence-of-exception IS the pass criterion.

## 3. Decomposed Task Units

- **T1**: Emit file-scope `using` directives (`Xunit`, `System.IO`, `System.Linq`, `System.Threading.Tasks`, `System.Collections.Generic`, `<RootNs>.Multiagent`).
- **T2**: Emit `namespace <RootNs>.Test.Multiagent;` declaration mirroring the Dart `test/multiagent` directory.
- **T3**: Emit `internal static class MultiagentModulesTestHelpers` carrying `CssgPlay4BootSource` (verbatim `@"..."`) and `ProjectDir` consts; preserve Dart `///` doc-comment as `/// <summary>` on `CssgPlay4BootSource`.
- **T4**: Emit `public class MultiIsolateWithProjectCompiledModulesTests : IAsyncLifetime` with `[Trait("Group", "Multi-isolate with project-compiled modules")]`.
- **T5**: Emit `private IsolateManager _manager = null!;` instance field.
- **T6**: Emit public constructor body `_manager = new IsolateManager();`.
- **T7**: Emit `public ValueTask InitializeAsync() => ValueTask.CompletedTask;`.
- **T8**: Emit `public async ValueTask DisposeAsync() { await _manager.Shutdown(); }`.
- **T9**: Emit `[Fact(DisplayName = "boots CSSG play 4 with project-linked modules", Timeout = 30000)] public async Task BootsCssgPlay4WithProjectLinkedModules()` skeleton.
- **T10**: Inside T9 body, emit `Assert.SkipUnless(Directory.Exists(MultiagentModulesTestHelpers.ProjectDir), "cssg_modules not found at " + MultiagentModulesTestHelpers.ProjectDir + ", skipping tests");` as first statement.
- **T11**: Inside T9 body, emit `var loader = new BootLoader();` then `var config = loader.Load(MultiagentModulesTestHelpers.CssgPlay4BootSource);`.
- **T12**: Inside T9 body, emit `config.RootSelfGlpPath = Path.GetFullPath("../programs/self.glp"); config.ProjectDir = MultiagentModulesTestHelpers.ProjectDir;`.
- **T13**: Inside T9 body, emit `// Verify directives parsed correctly` comment then `Assert.Equal(4, config.Directives.Count);`.
- **T14**: Inside T9 body, emit `Assert.Equal(new HashSet<string> { "alice", "bob", "carol", "dave" }, config.Directives.Select(d => d.AgentId).ToHashSet(), HashSet<string>.CreateSetComparer());`.
- **T15**: Inside T9 body, emit `// Boot all 4 agents` comment then `await _manager.Boot(config, new TraceConfig { Glp = false, Mad = true });`.
- **T16**: Inside T9 body, emit `// Start and let the protocol run` comment then `_manager.Start();` then `await Task.Delay(TimeSpan.FromSeconds(5));`.
- **T17**: Inside T9 body, emit `// If we reach here without crash, boot + project loading + execution works` final comment (NO synthetic assertion).
- **T18**: Verify no escalations leak from the SUT-side dart:isolate threading-model decision; ensure all SUT call-site shapes (`new IsolateManager()`, `Boot`, `Start`, `Shutdown`) are emitted option-independently per the convspec's inheritance note.

## 4. Research Findings

None required — every construct reuses an idiom already pinned by sibling test convspecs (boot_loader_test.dart.md, mad_scenarios_test.dart.md, module_activation_test.dart.md, mad_error_handling_test.dart.md, global_writers_table_test.dart.md, smoke_test.dart.md) and/or SUT convspecs (project_linker.dart.md, glp_engine.dart.md, boot_loader.dart.md, isolate_manager.dart.md). The convspec lists 12 cached + 8 first-recorded-here research findings; all 8 first-recorded findings cite official Microsoft Learn / xUnit / Dart documentation directly (FR-024 satisfied at the convspec layer). The dart:isolate threading-model escalation is INHERITED, not re-derived (FR-013).

## 5. Consistency Pass

Fixed — derived from `.codeconv/conversion-specs/test/multiagent/multiagent_modules_test.dart.md` (RATIFIED). Each plan step (T1–T18) is a 1:1 lift from a convspec construct row or the convspec's `conversion_units` summary (cu-1 through cu-9). The smoke-test pass criterion (absence of exception = pass) is preserved per the convspec's explicit instruction. Timeout unit conversion (30 s → 30000 ms) and `TimeSpan.FromSeconds(5)` (not `Thread.Sleep`) are emitted per convspec load-bearing nuances. Set-equality via `HashSet<T>.CreateSetComparer()` is emitted per the convspec's chosen mapping (not the rejected `Assert.True(SetEquals)` alternative). All SUT call-site shapes (`new IsolateManager()`, `Boot(config, new TraceConfig{...})`, `Start()`, `Shutdown()`) are option-independent per the inherited-escalation note in the convspec.

## 6. Escalations

None.
