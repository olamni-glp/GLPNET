---
path: test/debug_four_agents_modules.dart
cycle_group_id: 121
scc_siblings: []
generated_at: 2026-05-21T16:50:52Z
source_sha256: dec856d5b7a059a974c1fa9df57847e4b65dd6cf2a51e2e4f4eba8da78e0db7a
schema_version: 1
---

# Conversion Plan: test/debug_four_agents_modules.dart

## 1. Source Analysis

Inspection of `glp_runtime_net/test/debug_four_agents_modules.dart` (100 lines, sha256 `dec856d5…0db7a`):

- File header (lines 1-4): triple-slash `///` doc comment classifying the file as a diagnostic — four agents (Alice/Bob/Carol/Dave) with project modules, simulating `main_cssg_mad_modules.dart` (linked project + mad_boot). Includes a `Run: dart test/debug_four_agents_modules.dart` invocation hint and an em-dash (U+2014).
- Imports (lines 5-8):
  - `dart:io` — used once for `File('…').readAsStringSync()` (line 12) and `File('…').absolute.path` (line 13).
  - `dart:typed_data` — used only for `Uint8List` in the value-type of `pendingMessages`.
  - `package:glp_runtime/multiagent/agent_runtime.dart` — brings `AgentRuntime` into scope.
- Top-level (lines 10-99): `void main() async { … }` with REAL `await` calls (lines 55 and 75). No `package:test` import, no `test(…)`, no `expect(…)`, no matchers — pure diagnostic with `print(…)` only.
- Body locals:
  - `final projectDir = '../programs/cssg_modules';`
  - `final bootSource = File('../programs/cssg_modules/mad_boot.glp').readAsStringSync();`
  - `final rootSelfGlpPath = File('../programs/self.glp').absolute.path;`
  - `final pendingMessages = <String, List<(String, Uint8List)>>{};` (empty typed map with positional-record value type).
  - `final outputs = <String, List<String>>{ 'alice': [], 'bob': [], 'carol': [], 'dave': [] };`
  - `var rounds = 0;` (the sole mutable local, incremented inside the routing loop).
- Local function `AgentRuntime makeAgent(String id, String goal, List<String> extra)` (lines 22-43): constructs `AgentRuntime(agentId:…, glpSources:…, rootSelfGlpPath:…, goalLabel:…, extraArgs:…, projectDir:…)` with named arguments; wires three callback assignments:
  - `agent.onOutput = (line) { outputs[id]!.add(line); };`
  - `agent.onLog = (tag, msg) { if (msg.contains('RUN:') || msg.contains('ERROR') || msg.contains('SEND_MAD')) print('[$id] $msg'); };`
  - `agent.onSendMadMessage = (to, payload) async { pendingMessages.putIfAbsent(to, () => []).add((id, payload)); };` — async lambda with positional-record construction `(id, payload)`.
- Four `makeAgent(…)` calls (lines 45-48) building alice/bob/carol/dave with `(goalLabel, extraArgs)` pairs `('parent_init/4', ['carol','4'])`, `('parent_init/4', ['dave','4'])`, `('child_init/3', ['4'])`, `('child_init/3', ['4'])`.
- `final agents = {'alice': alice, 'bob': bob, 'carol': carol, 'dave': dave};` (inferred `Map<String, AgentRuntime>`).
- Per-agent initialization loop (lines 53-56): `for (final entry in agents.entries) { print('--- Initializing ${entry.key} ---'); await entry.value.initialize(); }`.
- Message-routing while-loop (lines 60-78): bounded by `rounds < 30`; uses `Map.from` shallow-copy snapshot + `.clear()` + iterate snapshot pattern; nested foreach with tuple destructuring `for (final (from, payload) in entry.value)`; `await agent.onMadMessageReceived(from, payload)` per message; early `continue` on unknown destination.
- Per-agent tagged-output summary (lines 84-96): `RegExp(r'^< tagged\((\w+), (cmd|notify)\((.+)\)\)$')` raw-string regex; for each agent, filter `outputs[id]!` to lines containing `'tagged('` via `.where(…).toList()`; print `length`; iterate with `firstMatch(l)`; if non-null, print `'  ${m.group(2)}: ${m.group(3)}'`, else print `'  $l'`.
- Final `print('\n=== Done ===');`.

Mutable state: `pendingMessages` (cleared+repopulated each round), `outputs` (appended-to via `onOutput` callback), `rounds` (incremented). All other locals are `final`. No threading primitives; the four agents run serially through the awaited main thread.

## 2. Dart → C#/.NET Conversion Plan

Constructs from the convspec, in the order they appear (each row references its convspec idiom):

1. **File-header `///` doc comment** → multi-line C# `///` XML-doc above `public static class DebugFourAgentsModules`; preserve em-dash byte-identically (UTF-8 on both sides). Idiom `rf-dart-tripleslash-doc-to-csharp-xml-doc`.
2. **`import 'dart:io';`** → `using System.IO;` (for `File.ReadAllText` and `Path.GetFullPath`). Idiom `rf-dart-dart-io-file-readasstringsync-to-system-io-file-readalltext`.
3. **`import 'dart:typed_data';`** → DROPS with NO replacement `using` (since `byte[]` lives in implicit `System`). NEW idiom `rf-dart-uint8list-import-to-csharp-byte-array-no-using-needed`.
4. **`import 'package:glp_runtime/multiagent/agent_runtime.dart';`** → `using <RootNs>.Multiagent;`. Idiom `rf-dart-internal-package-import-to-csharp-using`.
5. **`void main() async`** with REAL `await` → `public static async Task<int> Main(string[] args) { … return 0; }`. NEW idiom `rf-dart-debug-script-async-main-to-csharp-async-task-main`. Closing `return 0;` after the final banner preserves implicit-success exit.
6. **`print(<string>)`** → `Console.WriteLine(<string>)`. Idiom `rf-dart-print-in-console-exe-to-console-writeline`.
7. **Dart string interpolation** `'${entry.key}'`, `'$rounds'`, `'$from -> $dest (${payload.length} bytes)'`, `'[$id] $msg'`, `'${m.group(2)}: ${m.group(3)}'` → C# `$"…"` with PascalCased property names (`entry.Key`, `payload.Length`, `tagged.Count`, `m.Groups[2].Value`, `m.Groups[3].Value`). Idiom `rf-dart-string-interpolation-to-csharp-interpolated-string`. LOAD-BEARING: codegen consults static type at each interpolation site to pick `.Length` (array/string) vs `.Count` (List<T>).
8. **`final` locals (inferred type)** → `var` locals. Idiom `rf-dart-final-local-to-csharp-var-local`.
9. **`var rounds = 0;`** → `var rounds = 0;` (mutable int counter, identical). Idiom `rf-dart-var-mutable-local-to-csharp-var-local`.
10. **Implicit-new constructor calls**:
    - `File('…').readAsStringSync()` → `File.ReadAllText("…")` (collapse two-step into one-step).
    - `File('…').absolute.path` → `Path.GetFullPath("…")` (collapse two-step into one-step). NEW idiom `rf-dart-file-absolute-path-to-csharp-path-getfullpath`.
    - `AgentRuntime(agentId: id, glpSources: [bootSource], rootSelfGlpPath: …, goalLabel: …, extraArgs: …, projectDir: …)` → `new AgentRuntime(agentId: id, glpSources: new List<string> { bootSource }, rootSelfGlpPath: …, goalLabel: …, extraArgs: …, projectDir: …)` (C# named-argument call site preserved; positional parameter order from `agent_runtime.dart.md`).
    Idiom `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`.
11. **Typed map literals**:
    - `<String, List<(String, Uint8List)>>{}` → `new Dictionary<string, List<(string from, byte[] payload)>>()`.
    - `<String, List<String>>{ 'alice': [], … }` → `new Dictionary<string, List<string>> { { "alice", new List<string>() }, { "bob", new List<string>() }, { "carol", new List<string>() }, { "dave", new List<string>() } }`.
    Idiom `rf-dart-typed-map-literal-to-csharp-dictionary-collection-init`.
12. **Positional record type `(String, Uint8List)`** → C# `(string, byte[])` value tuple (`ValueTuple<string, byte[]>`); destructuring `final (from, payload) in entry.value` → `var (from, payload) in entry.Value`. Idiom `rf-dart-record-return-to-csharp-valuetuple`.
13. **`Map<K,V>.from(pendingMessages)`** → `new Dictionary<string, List<(string, byte[])>>(pendingMessages)` (shallow copy). NEW idiom `rf-dart-map-from-named-ctor-to-csharp-dictionary-copy-ctor`. LOAD-BEARING shallow-copy contract — original is `.Clear()`-ed immediately after; snapshot's value-lists remain intact.
14. **Local function `AgentRuntime makeAgent(…)`** → C# local function `AgentRuntime MakeAgent(string id, string goal, List<string> extra) { … }` inside `Main`. NEW idiom `rf-dart-local-function-with-captures-to-csharp-local-function`. Captures `outputs`, `pendingMessages`, `bootSource`, `rootSelfGlpPath`, `projectDir` from enclosing scope (C# closure semantics over local-function parameters and enclosing-method locals).
15. **Lambda callback assignments**:
    - `agent.onOutput = (line) { outputs[id]!.add(line); };` → `agent.OnOutput = line => { outputs[id].Add(line); };` (type `Action<string>`).
    - `agent.onLog = (tag, msg) { if (…) print('[$id] $msg'); };` → `agent.OnLog = (tag, msg) => { if (msg.Contains("RUN:") || msg.Contains("ERROR") || msg.Contains("SEND_MAD")) Console.WriteLine($"[{id}] {msg}"); };` (type `Action<string, string>`).
    - `agent.onSendMadMessage = (to, payload) async { … };` → `agent.OnSendMadMessage = async (to, payload) => { if (!pendingMessages.TryGetValue(to, out var list)) { pendingMessages[to] = list = new List<(string, byte[])>(); } list.Add((id, payload)); await Task.CompletedTask; };` (type `Func<string, byte[], Task>`).
    NEW idiom `rf-dart-callback-assignment-lambda-to-csharp-delegate-property-lambda`.
16. **`outputs[id]!` (definite-key map indexer + null-assertion)** → plain `outputs[id]` (the `!` ceremony erases — key is definitively present at construction). NEW idiom `rf-dart-bang-assert-on-map-indexer-to-csharp-dictionary-indexer-direct`.
17. **`.where((l) => l.contains('tagged(')).toList()`** → LINQ `.Where(l => l.Contains("tagged(")).ToList()`; requires `using System.Linq;`. NEW idiom `rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist`.
18. **`pendingMessages.putIfAbsent(to, () => []).add((id, payload))`** → `if (!pendingMessages.TryGetValue(to, out var list)) { pendingMessages[to] = list = new List<(string, byte[])>(); } list.Add((id, payload));`. NEW idiom `rf-dart-map-putifabsent-to-csharp-trygetvalue-out-with-lazy-init`. LOAD-BEARING: preserves lazy-factory contract (NOT `TryAdd`, which would always allocate).
19. **`RegExp(r'^< tagged\((\w+), (cmd|notify)\((.+)\)\)$')`** → `private static readonly Regex TaggedRegex = new(@"^< tagged\((\w+), (cmd|notify)\((.+)\)\)$", RegexOptions.Compiled);` at class scope. NEW idiom `rf-dart-regexp-raw-literal-to-csharp-regex-verbatim-static-readonly`. Requires `using System.Text.RegularExpressions;`.
20. **`taggedRegex.firstMatch(l)` + `if (m != null) { … }`** → `var m = TaggedRegex.Match(l); if (m.Success) { … } else { … }`; group access `m.group(int)` → `m.Groups[int].Value`. NEW idiom `rf-dart-regexp-firstmatch-to-csharp-regex-match-with-success`.
21. **`for (final entry in <Map>.entries)`** → `foreach (var entry in <Dictionary>)` (C# `Dictionary<K,V>` enumerates as `KeyValuePair<K,V>`; `.entries` selector drops); nested tuple-destructuring `for (final (from, payload) in entry.value)` → `foreach (var (from, payload) in entry.Value)`. Idiom `rf-dart-map-entries-iteration-to-csharp-dictionary-foreach`.
22. **`final agent = agents[dest]; if (agent == null) { … continue; }`** → `if (!agents.TryGetValue(dest, out var agent)) { … continue; }`. Idiom `rf-dart-map-indexer-nullable-to-csharp-trygetvalue`.
23. **`pendingMessages.isNotEmpty`** → `pendingMessages.Count > 0`. Idiom `rf-dart-string-and-iterable-members-to-dotnet`.
24. **`tagged.length` (List<String>) interpolation** → `tagged.Count` (List<T>.Count, NOT .Length). Idiom `rf-dart-string-and-iterable-members-to-dotnet`. LOAD-BEARING per-call-site discipline.
25. **`String.contains(<literal>)`** → `string.Contains(<literal>)` (PascalCase). Idiom `rf-dart-string-and-iterable-members-to-dotnet`.
26. **`List<T>.add(T)`** → `List<T>.Add(T)`; with positional-record arg → value-tuple construction `(id, payload)`. Idiom `rf-dart-string-and-iterable-members-to-dotnet`.
27. **`await entry.value.initialize()`** → `await entry.Value.InitializeAsync()` (PascalCase + `Async` suffix per .NET naming convention; pinned in `agent_runtime.dart.md`). Idiom `rf-dart-async-method-future-void-to-csharp-async-task-with-asyncsuffix`.
28. **`await agent.onMadMessageReceived(from, payload)`** → `await agent.OnMadMessageReceivedAsync(from, payload)`. Same idiom row.
29. **List-of-string literals `['carol','4']`, `['dave','4']`, `['4']`** → `new List<string> { "carol", "4" }`, `new List<string> { "dave", "4" }`, `new List<string> { "4" }`. LOAD-BEARING string-form preservation. Idiom `rf-dart-list-literal-to-csharp-list-or-collection-expression`.
30. **Member-access PascalCasing** `entry.key`/`entry.value`/`payload.length` → `entry.Key`/`entry.Value`/`payload.Length`. Idiom `rf-dart-camelcase-field-to-csharp-pascalcase-property`.
31. **`if (…) { … }` / `if (…) { … } else { … }`** → 1:1 C# `if/else` (after null-check substitutions in §22 and §20 above). Idiom `rf-dart-if-else-to-csharp-if-else`.
32. **`while (pendingMessages.isNotEmpty && rounds < 30) { … }`** → `while (pendingMessages.Count > 0 && rounds < 30) { … }`; `&&` short-circuit preserves; `rounds++` identical; `Map.clear()` → `Dictionary.Clear()`. NEW idiom `rf-dart-while-loop-to-csharp-while-loop`.
33. **Conversion-unit assembly (cu-1…cu-15)** as enumerated in the convspec: file-scope usings (cu-1), namespace (cu-2), file-header XML doc (cu-3), `public static class DebugFourAgentsModules` host with `private static readonly Regex TaggedRegex` field (cu-4), `public static async Task<int> Main(string[] args)` (cu-5), header locals (cu-6), pendingMessages + outputs Dictionaries (cu-7), `AgentRuntime MakeAgent(…)` local function (cu-8), four `MakeAgent` calls + `agents` dictionary (cu-9), per-agent init foreach (cu-10), message-routing while-loop (cu-11), per-agent tagged-output summary (cu-12), final `Console.WriteLine("\n=== Done ===");` + `return 0;` (cu-13), NO xUnit attributes (cu-14), csproj orchestration deferred to langpair (cu-15).

INHERITED multi-agent threading-model escalation (FR-013): the file drives four `AgentRuntime` instances; the `heap_fcp.dart` escalations[0] propagates through `agent_runtime.dart.md` and INHERITS here without re-escalation. Sequential `await` chain serialises all four agents on the main thread; no concurrent heap access arises.

## 3. Decomposed Task Units

- T1: Emit file-scope `using` directives (System; System.IO; System.Linq; System.Text.RegularExpressions; System.Collections.Generic; <RootNs>.Multiagent) per cu-1. — done.
- T2: Emit namespace declaration `<RootNs>.Test` per cu-2. — done.
- T3: Emit multi-line `///` XML-doc above host class (preserve em-dash UTF-8) per cu-3. — done.
- T4: Emit `public static class DebugFourAgentsModules` host with `private static readonly Regex TaggedRegex = new(@"…", RegexOptions.Compiled);` per cu-4. — done.
- T5: Emit `public static async Task<int> Main(string[] args)` signature per cu-5. — done.
- T6: Emit header locals `var projectDir`, `var bootSource = File.ReadAllText("../programs/cssg_modules/mad_boot.glp")`, `var rootSelfGlpPath = Path.GetFullPath("../programs/self.glp")`, banner `Console.WriteLine("=== Four-agent modules diagnostic (Play 4) ===\n");` per cu-6. — done.
- T7: Emit `pendingMessages` + `outputs` Dictionary literals per cu-7. — done.
- T8: Emit local function `AgentRuntime MakeAgent(string id, string goal, List<string> extra)` with three callback assignments (OnOutput/OnLog/OnSendMadMessage with async lambda + TryGetValue+lazy-init+Add) per cu-8. — done.
- T9: Emit four `MakeAgent` invocations + `agents` Dictionary per cu-9. — done.
- T10: Emit per-agent initialization foreach with `await entry.Value.InitializeAsync()` per cu-10. — done.
- T11: Emit message-routing while-loop with `Map.from` shallow copy + `.Clear()` + nested foreach with tuple destructuring + `await agent.OnMadMessageReceivedAsync(from, payload)` per cu-11. — done.
- T12: Emit per-agent tagged-output summary with `.Where(…).ToList()` + `TaggedRegex.Match` + `Match.Success` branch per cu-12. — done.
- T13: Emit final `Console.WriteLine("\n=== Done ===");` + `return 0;` per cu-13. — done.
- T14: Verify NO xUnit attributes/imports per cu-14. — done.
- T15: Defer csproj orchestration to langpair per cu-15. — done.

## 4. Research Findings

none required (all idioms either KB cache hits — `rf-dart-tripleslash-doc-to-csharp-xml-doc`, `rf-dart-dart-io-file-readasstringsync-to-system-io-file-readalltext`, `rf-dart-internal-package-import-to-csharp-using`, `rf-dart-print-in-console-exe-to-console-writeline`, `rf-dart-string-interpolation-to-csharp-interpolated-string`, `rf-dart-final-local-to-csharp-var-local`, `rf-dart-var-mutable-local-to-csharp-var-local`, `rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new`, `rf-dart-typed-map-literal-to-csharp-dictionary-collection-init`, `rf-dart-record-return-to-csharp-valuetuple`, `rf-dart-string-and-iterable-members-to-dotnet`, `rf-dart-map-indexer-nullable-to-csharp-trygetvalue`, `rf-dart-map-entries-iteration-to-csharp-dictionary-foreach`, `rf-dart-list-literal-to-csharp-list-or-collection-expression`, `rf-dart-camelcase-field-to-csharp-pascalcase-property`, `rf-dart-if-else-to-csharp-if-else`, `rf-dart-instance-method-camelcase-to-csharp-pascalcase` — or NEW idioms grounded in single authoritative Microsoft Learn / dart.dev citations recorded inline in the convspec — `rf-dart-uint8list-import-to-csharp-byte-array-no-using-needed`, `rf-dart-debug-script-async-main-to-csharp-async-task-main`, `rf-dart-map-from-named-ctor-to-csharp-dictionary-copy-ctor`, `rf-dart-local-function-with-captures-to-csharp-local-function`, `rf-dart-callback-assignment-lambda-to-csharp-delegate-property-lambda`, `rf-dart-bang-assert-on-map-indexer-to-csharp-dictionary-indexer-direct`, `rf-dart-iterable-where-tolist-to-csharp-linq-where-tolist`, `rf-dart-map-putifabsent-to-csharp-trygetvalue-out-with-lazy-init`, `rf-dart-regexp-raw-literal-to-csharp-regex-verbatim-static-readonly`, `rf-dart-regexp-firstmatch-to-csharp-regex-match-with-success`, `rf-dart-file-absolute-path-to-csharp-path-getfullpath`, `rf-dart-async-method-future-void-to-csharp-async-task-with-asyncsuffix`, `rf-dart-while-loop-to-csharp-while-loop`).

## 5. Consistency Pass

fixed — derived from convspec `.codeconv/conversion-specs/test/debug_four_agents_modules.dart.md` (RATIFIED mirror; constructs[0..N] enumerate every Dart construct in source order; conversion_units cu-1…cu-15 enumerate the output assembly; `escalations: []` in the convspec YAML; FR-013 inheritance of `heap_fcp.dart` threading-model escalation documented in file-header rationale and reaffirmed in the Notes / "Why no escalations" sections). All §2 construct rows and all §3 task units trace 1:1 to convspec rows; no inferences added beyond convspec text. SUT cross-references (`AgentRuntime`, `InitializeAsync`, `OnMadMessageReceivedAsync`, `OnOutput`, `OnLog`, `OnSendMadMessage`, named-argument constructor parameter order) consulted via `lib/multiagent/agent_runtime.dart.md` per convspec `target_decision` text. Project-pinned rule `Uint8List → byte[]` consulted via `lib/multiagent/agent_runtime.dart.md` per convspec construct dart.import.dart_typed_data_for_uint8list. INHERITED escalations (heap_fcp threading model via agent_runtime) are documented in the convspec file-header and Notes; per FR-013, this plan inherits without re-escalating.

## 6. Escalations

None.
