---
path: test/multiagent/ui_mediator_test.dart
cycle_group_id: 155
scc_siblings: []
generated_at: 2026-05-21T16:50:29Z
source_sha256: ccd3b832f06620db74e8876962e3f8dfdd080591d2dd03e1fd0f16fe9c4281aa
schema_version: 1
---

# Conversion Plan: test/multiagent/ui_mediator_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/multiagent/ui_mediator_test.dart`
(124 lines, sha256 `ccd3b832...81aa`) yields the following construct inventory:

- **Header comment block (lines 1-4)**: triple-slash `///` doc-comment
  describing the test file's purpose ("Tests for ui_mediator.glp — ground-term
  mediator between agent/4 and Dart").
- **Imports (lines 5-7)**:
  - `import 'dart:io';` — core-library import for `File`.
  - `import 'package:test/test.dart';` — Dart test framework
    (`group`, `test`, `setUp`, `expect`, `contains`).
  - `import 'package:glp_runtime/engine/glp_engine.dart';` — in-repo
    SUT import for `GlpEngine`.
- **`void main()` entry-point (line 9 onward)** containing:
  - Two `final` path-string locals (`socialAgentPath` /
    `uiMediatorPath`) used by all three tests.
  - One `group('ui_mediator', () { ... })` block enclosing:
    - Two `late`-declared closure-captured variables:
      `late GlpEngine engine;` and `late List<String> outputLines;`.
    - One `setUp(() { ... })` block constructing a fresh `GlpEngine`
      (with the cascade `..strictTypes = false`), resetting
      `outputLines = []`, and wiring
      `engine.runtime.outputCallback = (line) => outputLines.add(line);`.
    - Three `test(<label>, () async { ... })` calls, each with the
      same arrange-act-assert shape but different inner GLP payloads:
      - Test 1: `'grounds befriend output with request ID'` →
        expects `'befriend(bob, req(1))'` in outputLines.
      - Test 2: `'passes ground connected message through'` →
        expects `'connected(bob)'` in outputLines.
      - Test 3: `'passes ground received message through'` →
        expects `'received(bob, hello)'` in outputLines.
- **Per-test body** (lines 25-56, 58-89, 91-122) contains:
  - `File(socialAgentPath).readAsStringSync()` — synchronous file read
    into `socialSource`.
  - `File(uiMediatorPath).readAsStringSync().replaceAll(RegExp(r'-mode\s*\(\s*system\s*\)\s*\.'), '')`
    — synchronous file read + inline-constructed `RegExp` stripping
    the `-mode(system).` directive into `mediatorSource`.
  - `engine.loadSource('''<triple-quoted multi-line string with
    `$socialSource` + `$mediatorSource` interpolation and an embedded
    GLP program defining `send_to_user/1`, `consume/1`, `test/0`>''')`.
  - `final result = await engine.runGoal('test');` — async-await on
    the engine's goal-runner returning an `ExecutionResult`.
  - Two diagnostic `print(...)` calls (`'Status: ${result.status}'`,
    `'Output: $outputLines'`).
  - One `expect(outputLines, contains('<expected ground term>'))`
    assertion.

Async surface: every test body is `() async { ... }` with exactly one
`await` on `engine.runGoal('test')`. No `Stream` / `await for` / multi-
await sequences. SUT cross-file dependencies: `GlpEngine` ctor (named-
required `rootSelfGlpPath`), `StrictTypes` setter, `Runtime` getter
(returns `GlpRuntime`), `LoadSource(string)`, `RunGoal(string) → Task<ExecutionResult>`;
`GlpRuntime.OutputCallback` (nullable `Action<string>`-shaped delegate
property); `ExecutionResult.Status`. No additional concurrency primitives
introduced beyond what the engine SUT already pins.

## 2. Dart → C#/.NET Conversion Plan

The plan mirrors the ratified convspec verbatim; every construct row from
the convspec maps to the same C# decision recorded here.

1. **`import 'package:test/test.dart';`** → drop the directive and emit
   `using Xunit;` at file scope (project-wide xUnit framework,
   `rf-dart-package-test-to-dotnet-xunit`). Codegen also emits
   `using System;`, `using System.IO;`, `using System.Collections.Generic;`,
   `using System.Text.RegularExpressions;`, `using System.Threading.Tasks;`
   to surface all transitive .NET symbols touched by this file.

2. **`import 'dart:io';`** → drop the directive entirely; load-bearing
   symbols (`File`/`File.absolute.path`) route to `System.IO.File` and
   `System.IO.Path` at first use, surfaced via the `using System.IO;`
   added above (`rf-dart-dart-io-to-dotnet-system-io`).

3. **`import 'package:glp_runtime/engine/glp_engine.dart';`** → emit
   `using <RootNs>.Engine;` (for `GlpEngine` + `ExecutionResult`) AND
   `using <RootNs>.Runtime;` (for `GlpRuntime`, transitively reached
   via `engine.runtime`). The `<RootNs>` placeholder is workspace-level
   and pinned by the langpair registry. `rf-dart-package-sut-import-to-csharp-using`.

4. **`void main() { ... }`** → drop the Dart `void main()` entry-point
   entirely (xUnit discovers `[Fact]` methods by reflection — no
   per-file hook). The two enclosed `final` path locals
   (`socialAgentPath` / `uiMediatorPath`) migrate to private
   class-scoped `const string` fields (`SocialAgentPath` / `UiMediatorPath`)
   on the test class. `rf-dart-test-main-to-xunit-class-with-facts`.

5. **`group('ui_mediator', () { ... })`** → `public class UiMediatorTests`
   (PascalCased label + `Tests` suffix; underscore-strip rule). MAY
   carry `[Trait("Group", "ui_mediator")]` on the class for reporter
   parity. Single-group → one class containing all three `[Fact]` methods.
   `rf-dart-package-test-group-to-xunit-class`.

6. **`late GlpEngine engine;` / `late List<String> outputLines;`** →
   `private GlpEngine _engine = null!;` and
   `private List<string> _outputLines = null!;` instance fields
   (xUnit per-test instance lifecycle preserves Dart `late + setUp`
   semantics). `rf-dart-late-field-to-csharp-nullforgiving-field`.

7. **`setUp(() { ... })`** → `public UiMediatorTests() { ... }` ctor
   body (xUnit instantiates the test class once per `[Fact]`, matching
   `package:test` per-test isolation). Three sub-translations:
   (a) `engine = GlpEngine(rootSelfGlpPath: File('../programs/self.glp').absolute.path)..strictTypes = false`
   → `_engine = new GlpEngine(rootSelfGlpPath: Path.GetFullPath("../programs/self.glp")) { StrictTypes = false };`
   (cascade collapsed into object initializer);
   (b) `outputLines = []` → `_outputLines = new List<string>();`;
   (c) `engine.runtime.outputCallback = (line) => outputLines.add(line);`
   → `_engine.Runtime.OutputCallback = line => _outputLines.Add(line);`.
   `rf-dart-setup-to-xunit-constructor`.

8. **`GlpEngine(...)..strictTypes = false`** (cascade on ctor) →
   `new GlpEngine(...) { StrictTypes = false }` (C# object initializer).
   `rf-dart-cascade-operator-to-csharp-object-initializer-or-method-chain`.

9. **`test('<label>', () async { ... })`** (×3) → three
   `[Fact(DisplayName = "<original label>")] public async Task <PascalName>()`
   methods. Method names: `GroundsBefriendOutputWithRequestId`,
   `PassesGroundConnectedMessageThrough`,
   `PassesGroundReceivedMessageThrough` (PascalCase + strip non-identifier
   chars). Each method carries `/// <summary>` doc-comment block
   preserving the narrative purpose. NEVER emit `async void` (would
   break xUnit's await-and-report contract).
   `rf-dart-test-callback-to-xunit-method-body` +
   `rf-dart-future-async-await-to-csharp-task-async-await`.

10. **`File(socialAgentPath).readAsStringSync()`** →
    `File.ReadAllText(SocialAgentPath)` (static-helper collapse).
    `rf-dart-file-readasstringsync-to-csharp-file-readalltext`.

11. **`File(uiMediatorPath).readAsStringSync().replaceAll(RegExp(r'-mode\s*\(\s*system\s*\)\s*\.'), '')`**
    → `Regex.Replace(File.ReadAllText(UiMediatorPath), @"-mode\s*\(\s*system\s*\)\s*\.", "")`.
    Pattern operators (`\s*`, `\(`, `\)`, `\.`) are byte-identical across
    Dart `RegExp` (JS-flavour) and .NET `Regex` (PCRE+). Dart raw-string
    `r'...'` maps to C# verbatim `@"..."`. Requires
    `using System.Text.RegularExpressions;`.
    `rf-dart-string-replaceall-regexp-to-csharp-regex-replace`.

12. **`engine.loadSource('''<multi-line GLP source with $socialSource +
    $mediatorSource interpolation>''')`** → C# 11+ interpolated raw
    string literal `_engine.LoadSource($"""<...{socialSource}...{mediatorSource}...>""");`.
    (FALLBACK for pre-C# 11: `$@"..."` verbatim interpolated.)
    No literal `{` or `}` in the GLP payload, so single-dollar form is
    safe. The closing `"""` MUST sit at column 0 (or be carefully
    indented) to avoid C#'s common-indent stripping; embedded newlines
    MUST be preserved byte-identically for the GLP lexer.
    `rf-dart-triple-quoted-with-interpolation-to-csharp-raw-string-interpolated`.

13. **`final socialSource = ...;` / `final mediatorSource = ...;` /
    `final result = await ...;`** → `var socialSource = ...;` /
    `var mediatorSource = ...;` / `var result = await ...;` (Dart `final`
    on a local has no exact C# locals-modifier equivalent; `var` accepts
    the minor semantic loss — IDENTICAL to sibling specs).
    `rf-dart-final-local-to-csharp-var-local`.

14. **`await engine.runGoal('test')`** → `await _engine.RunGoal("test")`
    (enclosing method `async Task`). Awaited type is `ExecutionResult`
    per the engine SUT spec; `var result` infers `ExecutionResult`.
    `rf-dart-future-async-await-to-csharp-task-async-await`.

15. **`engine.runtime.outputCallback` / `result.status` / `outputLines.add(line)`
    / `engine.loadSource(...)` / `engine.runGoal(...)`** — Dart
    member access → C# PascalCased member access. `_engine.Runtime`,
    `_engine.Runtime.OutputCallback`, `result.Status`,
    `_outputLines.Add(line)`, `_engine.LoadSource(...)`, `_engine.RunGoal(...)`.
    `rf-dart-member-access-to-csharp-member-access-pascalcase`.

16. **`(line) => outputLines.add(line)`** → `line => _outputLines.Add(line)`
    (single-arg arrow lambda assigned to `Action<string>?` delegate
    property; `List<T>.Add` returns `void` matching `Action<string>`'s
    void return). `rf-dart-arrow-lambda-to-csharp-lambda`.

17. **`expect(outputLines, contains('<term>'))`** →
    `Assert.Contains("<term>", _outputLines);` (argument-order FLIP —
    expected-element first, then collection). The element-membership
    `Assert.Contains<T>(T expected, IEnumerable<T> collection)` overload
    matches `outputLines`'s `List<String>` shape; ordinal-case-sensitive
    `EqualityComparer<string>.Default` matches Dart's ordinal `String.==`.
    `rf-dart-expect-contains-to-xunit-assert-contains`.

18. **`print('Status: ${result.status}'); print('Output: $outputLines');`**
    → `Console.WriteLine($"Status: {result.Status}");`
    `Console.WriteLine($"Output: [{string.Join(", ", _outputLines)}]");`.
    The explicit `string.Join` + bracket wrapping is REQUIRED to match
    Dart's `List<T>.toString()` shape (`[a, b, c]`); C# `List<T>.ToString()`
    by default emits the type name only. Diagnostic-only (not load-
    bearing for pass/fail); codegen MAY substitute
    `ITestOutputHelper.WriteLine(...)` with ctor-injection for the
    xUnit-idiomatic alternative, but the direct Console form is the
    faithful shape. `rf-dart-print-to-csharp-console-writeline`.

19. **`outputLines = [];`** → `_outputLines = new List<string>();`
    (constructor-call form; `new List<string> { }` collection-initializer
    accepted but unidiomatic for empty case).
    `rf-dart-list-literal-to-csharp-list-initializer`.

20. **`'Status: ${result.status}'` / `'Output: $outputLines'`** —
    Dart string interpolation (both `${expr}` and `$identifier` forms)
    → C# interpolated `$"..."` strings using `{expr}` for both cases
    (C# has no syntactic distinction). PascalCase rename applies inside
    the interpolation hole (`result.status` → `result.Status`).
    `rf-dart-string-interpolation-to-csharp-interpolated-string`.

21. **Two consecutive diagnostic `print(...)` pair per test** — recorded
    explicitly so codegen translates BOTH (or replaces BOTH together);
    ordering MUST survive (status first, output second). Not a new
    research finding — covered by #18. `rf-dart-print-to-csharp-console-writeline`.

22. **Triple-slash `///` header doc-comment** — preserved as C#
    `///`-style XML doc-comment on the test class (xUnit/C# share the
    `///` syntax for doc-comments). Narrative purpose carries forward.

## 3. Decomposed Task Units

- **T1**: Emit file-scope `using` directives (Xunit + System + System.IO +
  System.Collections.Generic + System.Text.RegularExpressions +
  System.Threading.Tasks + `<RootNs>.Engine` + `<RootNs>.Runtime`). done
- **T2**: Emit file-scoped namespace `<RootNs>.Test.Multiagent;`
  (mirrors `test/multiagent` source path, .NET 6+ file-scoped form). done
- **T3**: Declare `public class UiMediatorTests` (single class from the
  single `group('ui_mediator', ...)`); MAY add `[Trait("Group", "ui_mediator")]`;
  carry the file-header `///` doc-comment onto the class. done
- **T4**: Declare class-scoped `private const string SocialAgentPath = "../programs/typed_book/social_graph/typed_social_agent.glp";`
  and analogous `UiMediatorPath`. done
- **T5**: Declare `private GlpEngine _engine = null!;` and
  `private List<string> _outputLines = null!;` instance fields. done
- **T6**: Implement `public UiMediatorTests()` ctor body
  (engine new + cascade-to-object-initializer; empty list; lambda
  assignment to `Runtime.OutputCallback`). done
- **T7**: Emit `[Fact(DisplayName="grounds befriend output with request ID")] public async Task GroundsBefriendOutputWithRequestId()`
  with arrange-act-diagnostic-assert body; expected element
  `"befriend(bob, req(1))"`. done
- **T8**: Emit `[Fact(DisplayName="passes ground connected message through")] public async Task PassesGroundConnectedMessageThrough()`
  with the same body shape; expected element `"connected(bob)"`. done
- **T9**: Emit `[Fact(DisplayName="passes ground received message through")] public async Task PassesGroundReceivedMessageThrough()`
  with the same body shape; expected element `"received(bob, hello)"`. done
- **T10**: For each method body: emit
  `var socialSource = File.ReadAllText(SocialAgentPath);` and
  `var mediatorSource = Regex.Replace(File.ReadAllText(UiMediatorPath), @"-mode\s*\(\s*system\s*\)\s*\.", "");`. done
- **T11**: For each method body: emit
  `_engine.LoadSource($"""<embedded GLP program with `{socialSource}` and `{mediatorSource}` interpolations and the per-test inner payload>""");`
  with the closing `"""` at column 0; preserve embedded newlines exactly. done
- **T12**: For each method body: emit
  `var result = await _engine.RunGoal("test");`. done
- **T13**: For each method body: emit two `Console.WriteLine(...)` diagnostic
  calls — status interpolation + `string.Join`-wrapped output buffer. done
- **T14**: For each method body: emit
  `Assert.Contains("<expected term>", _outputLines);` (arg-order flip,
  element-membership overload). done
- **T15**: Per method, emit `/// <summary>...</summary>` doc-comment
  preserving the test's narrative purpose
  (befriend grounding / connected pass-through / received pass-through). done

## 4. Research Findings

none required (all idioms cited in the ratified convspec are KB cache
hits or first-use rows defined within the convspec itself with
authoritative dart.dev + Microsoft Learn citations — no new external
research required at the plan stage).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/multiagent/ui_mediator_test.dart.md`
(ratified-mirror), `lib/engine/glp_engine.dart.md` (SUT — `GlpEngine`
ctor + `StrictTypes` + `Runtime` + `LoadSource(string)` +
`RunGoal(string) → Task<ExecutionResult>`),
`lib/runtime/runtime.dart.md` (SUT — `GlpRuntime.OutputCallback` typed
`Action<string>?`), `lib/runtime/heap_fcp.dart.md` (threading-model
escalation — INHERITED, NOT re-escalated per FR-013), and CLAUDE.md
(project-wide xUnit framework choice; PGLite-data-dir convention; spec-
first discipline). Every construct row in §2 has a one-to-one mirror in
the convspec's `constructs:` list, and every cross-file dependency
recorded in `conversion_units:` (cu-1 through cu-11) is reflected in
the corresponding plan task (T1-T15). Argument-order flip on
`Assert.Contains` is explicitly recorded (LOAD-BEARING footgun per
convspec); cascade-to-object-initializer collapse is explicitly recorded;
triple-quoted-raw-string-with-interpolation byte-identical-payload
discipline is explicitly recorded; xUnit `async Task` (NOT `async void`)
discipline is explicitly recorded. No idiom conflicts, no spec
contradictions, no unresolved cross-file references.

## 6. Escalations

None.
