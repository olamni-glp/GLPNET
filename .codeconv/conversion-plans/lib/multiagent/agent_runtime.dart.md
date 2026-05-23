---
path: lib/multiagent/agent_runtime.dart
cycle_group_id: 65
scc_siblings: []
generated_at: 2026-05-21T16:45:39Z
source_sha256: 16fa2e171ac7e27ce67020b5a539c9e42355fb38c61a305c827133ddbf373b8f
schema_version: 1
---

# Conversion Plan: lib/multiagent/agent_runtime.dart

## 1. Source Analysis

Inspection of `glp_runtime_net/lib/multiagent/agent_runtime.dart` (515 lines, sha256 16fa2e17…):

- **Header**: file-level `///` doc comment block describing `AgentRuntime`'s role (encapsulates GLP agent runtime for UI integration; extracted from `glp_multiagent/lib/main.dart`; uses `GlpEngine`, `MadContext`, `Scheduler`; boot via `agent_init(Id, UserIn, NetIn)` with network output through `send_to_net` → `global_send` → `MadContext` and user output through `send_to_user` → `_output/1` kernel → `outputCallback`).
- **Library directive**: `library;` (Dart 2.19+ bare form).
- **Imports** (11 total): `dart:typed_data` (for `Uint8List`); ten `package:glp_runtime/...` imports spanning compiler (`parser.dart`, `lexer.dart`, `ast.dart` as `ast`), bytecode (`runner.dart`), engine (`glp_engine.dart`), runtime (`runtime.dart`, `machine_state.dart`, `scheduler.dart`, `terms.dart` as `rt`, `external_io.dart`), multiagent (`mad_context.dart`, `message_queue.dart`, `payload_serializer.dart`). TWO library prefix aliases: `ast` (compiler-side AST terms) and `rt` (runtime-side heap terms) — both required to disambiguate `Term`/`VarRef`/`StructTerm`/`ConstTerm`/`ListTerm` collisions.
- **Top-level**: ONE class `AgentRuntime`.
- **Class shape** (struct count verified directly against source):
  - 7 `final` ctor-bound fields: `agentId`, `glpSources`, `rootSelfGlpPath`, `friends` (`const []` default), `goalLabel` (`'agent_init/3'` default), `extraArgs` (`const []` default), `projectDir` (nullable).
  - 3 nullable callback fields: `onOutput: void Function(String)?`, `onLog: void Function(String, String)?`, `onSendMadMessage: Future<void> Function(String, Uint8List)?`.
  - 5 nullable private runtime-state fields: `_runtime: GlpRuntime?`, `_ctx: MadContext?`, `_scheduler: Scheduler?`, `_userInput: InputInjector?`, `_netInput: InputInjector?`.
  - 1 private bool `_initialized = false`.
  - 4 public mutable int stats counters: `goalCount`, `heapVars`, `wpSize`, `mpSize` (all `= 0`).
  - 1 public mutable bool `glpTraceEnabled = true`.
  - 3 public get-only computed getters: `initialized`, `runtime`, `ctx`.
  - 1 private get-only computed getter `_tag` (uppercases `agentId`).
  - Constructor: named-required + named-optional-with-defaults.
- **Methods** (verbatim from source):
  - `_log(String)`, `_output(String)` — private nullable-callback dispatch helpers.
  - `updateStats()` — pulls three derived counts from `_runtime!.heap.HP`, `_ctx!.wp.{globalize,localize}EntryCount`, `_ctx!.mp.totalLength`; does NOT touch `goalCount`.
  - `Future<void> initialize() async` — ~135-line orchestrator: lowercase id; construct `GlpEngine(...)..strictTypes = false` (cascade); `enableMadGLP`; branch on `projectDir != null` (project mode = `loadProject` + diagnostic 6-label probe `parent_init/4`, `child_init/3`, `agent/4`, `ui_mediator/5`, `merge/3`, `tee/3` vs legacy mode = source-only loop); capture `_runtime`/`_ctx`; wire `outputCallback` lambda; wire `onMessageReady` ASYNC lambda assigned to void-typed callback; allocate netIn writer+reader, `initializeSerializerEntry(netInWriter)` (Spec 4.1 comment); allocate userIn writer+reader; construct `_userInput`/`_netInput` (the latter reusing `netInWriter`); construct `BytecodeRunner(program)`; construct `Scheduler` with `{'main': runner}` map literal + trace-sink lambda; lookup `entryPC = program.labels[goalLabel]` with early-return on null; build args `Map<int, rt.Term>` with arg0=agentIdLower, optional userInReader (only for `agent_init/3`), extraArgs via `int.tryParse(extra) ?? extra` (null-or-string fallback), last=netInReader; wire `CallEnv` + `setGoalEnv` + `setGoalProgram` + `gq.enqueue`; emit `[GOAL] Started ...`; `await _runUntilQuiescent()`; set `_initialized = true`; `updateStats()`; emit four ready-message lines (uses `friends.isNotEmpty ? friends.first.toLowerCase() : 'friend'`).
  - `Future<void> injectUserInput(String)` — empty-or-uninitialized guard; echo; try/catch wrapping `parseTerm` + `_userInput!.inject(term)` + `gq.enqueue` loop + `await _runUntilQuiescent()`; two-binding catch `(e, st)` logs both, emits `[ERROR]`.
  - `Future<void> onMadMessageReceived(String from, Uint8List payload)` — uninitialized guard; construct fresh `PayloadSerializer(agentId.toLowerCase())`; `deserializeMessage`; dispatch on `msg.type`: `MessageType.assignment` → try-catch around `deserializeGlobalSendPayload` (inline lambda allocator) + `_ctx!.handleMadAssignment(globalName:, value:, fromAgent:)` (reactivation-handled-inside comment); `MessageType.agentMessage` → `deserializeAgentMessagePayload` (same inline lambda) + `formatTerm` log + `_netInput!.inject(term)` + `gq.enqueue` loop; else → unknown-type log; finally `updateStats()` + `await _runUntilQuiescent()`.
  - `Future<void> onLegacyMessageReceived(String from, dynamic payload)` — backwards-compat; build `rt.StructTerm('msg', [ConstTerm(from), ConstTerm(agentId), ConstTerm(payload)])`; `_netInput!.inject` + `gq.enqueue` loop + `await _runUntilQuiescent()`.
  - `Future<void> _sendMadPayload(String to, Uint8List payload)` — log + `await onSendMadMessage?.call(to, payload)` (Dart-special `await null` no-op).
  - `Future<String?> runUntilQuiescent()` — public async-by-delegation wrapper around `_runUntilQuiescent()`; doc-comment cites agent-runtime-spec Section 3.
  - `Future<String?> _runUntilQuiescent()` — declared `async` but contains NO `await` (drain + flush are synchronous): `_log` start; uninitialized early-return null; try { `_scheduler!.drainWithStatus(debug: glpTraceEnabled)`; accumulate `goalCount`; `_ctx!.flushMessages()` + conditional log; `updateStats()`; return `result.status.name` } catch (e, st) { log; return `'error'` }.
  - `rt.Term parseTerm(String)` — wraps as `'_temp_($termStr).'`; `Lexer.tokenize`; `Parser.parse`; two-step validation throws (`'Could not parse term'` / `'No term to inject'`); delegate to `_astToRuntimeTerm`.
  - `rt.Term _astToRuntimeTerm(ast.Term)` — recursive `is`-test dispatch over `ast.ConstTerm`/`VarTerm`/`StructTerm`/`ListTerm`; `_runtime!.heap.allocateVariable()` record destructuring for var case; method-group tear-off `_astToRuntimeTerm` in `.map(...).toList()`; default throws `'Unknown AST term type: ${astTerm.runtimeType}'`.
  - `rt.Term _astListToRuntimeTerm(ast.ListTerm)` — `isNil` early-return `ConstTerm('nil')`; recurse on `list.head!`; tail handled via NESTED TERNARY (`tail is ast.ListTerm ? recurse-list : tail != null ? recurse-term : ConstTerm('nil')`); return `rt.StructTerm('.', [head, tail])`.
  - `rt.Term derefTerm(rt.Term)` — uninitialized early-return; `is rt.VarRef` → `heap.getValue(addr)`, if non-null AND `value is! rt.VarRef` recurse on value, else return term unchanged; `is rt.StructTerm` → `args.map(derefTerm).toList()` + rebuild; else fall-through.
  - `String formatTerm(rt.Term)` — `ConstTerm` (nil/null → `'[]'`, else `.value.toString()`); `VarRef` → `'X${addr}'` or `'X${addr}?'` based on reader-flag (via `_runtime?.heap.isReader(addr) ?? false`); `StructTerm` with `.`/2 → inner while-loop flattens cons-cell chain emitting `[a, b, c]` or `[a, b | T]`; other `StructTerm` → `'functor(arg1, arg2)'`; fallback `.toString()`.
  - `void dispose()` — body contains ONLY the comment `// No OutputObservers to dispose — output goes through _output/1 kernel`.

No try-catch outside `injectUserInput`, `onMadMessageReceived` (Assignment-arm only), and `_runUntilQuiescent`. No `==`/`hashCode` override. No `toString` override. No `late final` fields. No factory constructor. No mixins. No extension methods. No top-level functions or constants.

## 2. Dart → C#/.NET Conversion Plan

Each construct maps verbatim per the ratified convspec at `.codeconv/conversion-specs/lib/multiagent/agent_runtime.dart.md`. The convspec is the authority; this plan mirrors it.

### Library + imports

- `library;` → DROPPED (no .NET counterpart; render the file-level `///` doc-comment block as `//` file-header comments above the `namespace lib.multiagent` declaration).
- `dart:typed_data` `Uint8List` → C# `byte[]` (canonical .NET counterpart; pinned by `payload_serializer.dart`'s convspec for round-trip fidelity across the multiagent family).
- Intra-`lib/multiagent/` imports (`mad_context.dart`, `message_queue.dart`, `payload_serializer.dart`) → NO `using` directive emitted (intra-namespace).
- Cross-namespace imports → `using GlpRuntime.Compiler;`, `using GlpRuntime.Runtime;`, `using GlpRuntime.Bytecode;`, `using GlpRuntime.Engine;`.
- TWO library prefix aliases (LOAD-BEARING) → `using ast = GlpRuntime.Compiler.Ast;` AND `using rt = GlpRuntime.Runtime.Terms;`. Every `ast.X` and `rt.Y` reference preserved verbatim at the use site (Microsoft Learn 'using directive — alias').

### Class shape — `AgentRuntime`

Reference `class AgentRuntime` declared in `lib.multiagent`. NO `record` (mutable state + identity equality required). NO `sealed` (Dart source not sealed). NO `IDisposable` interface (`Dispose()` is project-local convention, not the `using`-statement contract).

- 7 `final` ctor-bound fields → 7 get-only auto-properties: `AgentId` (`string`), `GlpSources` (`IReadOnlyList<string>`), `RootSelfGlpPath` (`string`), `Friends` (`IReadOnlyList<string>`), `GoalLabel` (`string`), `ExtraArgs` (`IReadOnlyList<string>`), `ProjectDir` (`string?`). `List<string>` → `IReadOnlyList<string>` widening (consumer-immutable, matching `boot_loader.dart`/`global_send.dart`/`mad_context.dart` family convention).
- 3 nullable callback fields → 3 public nullable fields: `public Action<string>? OnOutput;`, `public Action<string, string>? OnLog;`, `public Func<string, byte[], Task>? OnSendMadMessage;`. `Future<void> Function(...)` → `Func<..., Task>` (Task is the documented async-void return per Microsoft Learn 'Task'). Plain fields (NOT properties) to match Dart-field shape allowing post-construction assignment from coordinator (`init` accessors REJECTED — forbid reassignment).
- 5 nullable private runtime-state fields → `private GlpRuntime? _runtime;`, `private MadContext? _ctx;`, `private Scheduler? _scheduler;`, `private InputInjector? _userInput;`, `private InputInjector? _netInput;`. Non-readonly (set inside `InitializeAsync`).
- `bool _initialized = false;` → `private bool _initialized = false;` (declaration-site initialiser).
- 4 stats counters → 4 public mutable fields: `public int GoalCount = 0; public int HeapVars = 0; public int WpSize = 0; public int MpSize = 0;`.
- `glpTraceEnabled` → `public bool GlpTraceEnabled = true;`.
- 3 public get-only computed getters → 3 expression-bodied properties: `public bool Initialized => _initialized;`, `public GlpRuntime? Runtime => _runtime;`, `public MadContext? Ctx => _ctx;`.
- `_tag` getter → `private string _Tag => AgentId.ToUpperInvariant();` (LOAD-BEARING: `ToUpperInvariant` NOT `ToUpper` — culture-insensitivity required to match Dart `toUpperCase`; Microsoft Learn 'String.ToUpper' recommends invariant for non-display use).
- Constructor → single positional ctor `public AgentRuntime(string agentId, IReadOnlyList<string> glpSources, string rootSelfGlpPath, IReadOnlyList<string>? friends = null, string goalLabel = "agent_init/3", IReadOnlyList<string>? extraArgs = null, string? projectDir = null)` with ctor-body coalesce for the two list defaults: `Friends = friends ?? Array.Empty<string>(); ExtraArgs = extraArgs ?? Array.Empty<string>();` (Dart `const []` → shared interned empty array per `System.Array.Empty<T>()`).

### Method-by-method mapping

- `_log` → `private void _Log(string message) => OnLog?.Invoke(_Tag, message);` (canonical nullable-delegate `?.Invoke` pattern; REUSED from `mad_context.dart`'s `_trace`).
- `_output` → `private void _Output(string text) => OnOutput?.Invoke(text);` (identical pattern, single-arg).
- `updateStats` → `public void UpdateStats()` with `if (_runtime != null && _ctx != null) { HeapVars = _runtime.Heap.HP; WpSize = _ctx.Wp.GlobalizeEntryCount + _ctx.Wp.LocalizeEntryCount; MpSize = _ctx.Mp.TotalLength; }`. Dart `_runtime!.heap.HP` bang-asserts DROPPED — C# NRT propagates the upfront `_runtime != null && _ctx != null` narrowing through the `&&` chain (Microsoft Learn 'Nullable reference types').
- `initialize` → `public async Task InitializeAsync()` (Async suffix per Microsoft FrameworkDesignGuidelines):
  - `var agentIdLower = AgentId.ToLowerInvariant();` (culture-insensitive — token construction, NOT display).
  - `_Log("INIT: Starting"); _Output("[INIT] Creating MadContext...");`
  - `var engine = new GlpEngine(rootSelfGlpPath: RootSelfGlpPath) { StrictTypes = false };` — cascade `..strictTypes = false` → OBJECT INITIALIZER (Microsoft Learn 'Object and Collection Initializers').
  - `engine.EnableMadGlp(agentIdLower);` (`GLP` 3-letter acronym → `Glp` PascalCase per Microsoft FrameworkDesignGuidelines).
  - Branch on `ProjectDir != null`:
    - Project mode: `_Log($"INIT: Loading project from {ProjectDir}"); engine.LoadProject(ProjectDir);` (NRT narrowing inside branch); `_Log($"INIT: Project loaded, loading {GlpSources.Count} boot source(s)"); for (int i = 0; i < GlpSources.Count; i++) { engine.LoadSource(GlpSources[i], filename: $"source_{i}"); }`; diagnostic block: `var program = engine.CombinedProgram; var keyLabels = new[] { "parent_init/4", "child_init/3", "agent/4", "ui_mediator/5", "merge/3", "tee/3" }; foreach (var key in keyLabels) { var pc = program.Labels.TryGetValue(key, out var p) ? (int?)p : null; _Log($"INIT: Label {key} -> {(pc != null ? $"PC={pc}" : "NOT FOUND")}"); }` (six-label list preserved VERBATIM; CGSP/maGLP entry-points). `_Log($"INIT: Program loaded via project linking ({ProjectDir}) + {GlpSources.Count} boot source(s), {program.Labels.Count} labels");`
    - Legacy mode: `for (int i = 0; i < GlpSources.Count; i++) { engine.LoadSource(GlpSources[i], filename: $"source_{i}"); } _Log($"INIT: Program loaded via GlpEngine (stdlib + madPredicates + {GlpSources.Count} source files)");`
  - `_runtime = engine.Runtime; _ctx = engine.MadContext; _Log("INIT: MadContext created");`
  - `_runtime.OutputCallback = (text) => _Output($"< {text}");`
  - `_ctx.OnMessageReady = async (destination, msg) => { var serializer = new PayloadSerializer(agentIdLower); var payload = serializer.SerializeMessage(msg); await _SendMadPayloadAsync(destination, payload); };` — **async-void lambda on void-returning `MessageDeliveryCallback` delegate** (LOAD-BEARING; preserves Dart fire-and-forget; Microsoft Learn 'async (C# Reference)' covers event-handler-style async-void; exception-cannot-be-caught caveat INHERITED from Dart; decision LOCAL — does NOT cascade to `mad_context.dart`'s delegate signature).
  - `var (netInWriter, netInReader) = _runtime.Heap.AllocateVariable(); _ctx.Wp.InitializeSerializerEntry(netInWriter); _Log($"INIT: Serializer entry initialized, netIn=({netInWriter},{netInReader})");` — Spec 4.1 comment preserved.
  - `var (userInWriter, userInReader) = _runtime.Heap.AllocateVariable(); _userInput = new InputInjector(_runtime.Heap, "user", userInWriter);`
  - `_netInput = new InputInjector(_runtime.Heap, "net", netInWriter);` (REUSES `netInWriter` from above — LOAD-BEARING).
  - `_Output("[INIT] Loaded GLP program");`
  - `var program = engine.CombinedProgram; var runner = new BytecodeRunner(program); _scheduler = new Scheduler(rt: _runtime, runners: new Dictionary<string, BytecodeRunner> { ["main"] = runner }, traceSink: line => _Log($"GLP: {line}")); _scheduler.ResetDisplayNumbering();` — Dart `{'main': runner}` map literal → C# indexer-based collection initialiser (Microsoft Learn 'Collection Initializers').
  - `var entryPC = program.Labels.TryGetValue(GoalLabel, out var pc) ? (int?)pc : null; _Log($"INIT: {GoalLabel} entryPC={entryPC}"); if (entryPC == null) { _Output($"[ERROR] Predicate {GoalLabel} not found"); return; }`
  - `var heap = _runtime.Heap; var args = new Dictionary<int, rt.Term>(); var argIdx = 0; var (arg0Writer, arg0Reader) = heap.AllocateVariable(); heap.BindVariable(arg0Writer, new rt.ConstTerm(agentIdLower)); args[argIdx++] = new rt.VarRef(arg0Reader);`
  - `if (GoalLabel == "agent_init/3") { var (userWriter, userReader) = heap.AllocateVariable(); heap.BindVariable(userWriter, new rt.VarRef(userInReader)); args[argIdx++] = new rt.VarRef(userReader); }` — legacy back-compat.
  - `foreach (var extra in ExtraArgs) { var (eWriter, eReader) = heap.AllocateVariable(); var intVal = int.TryParse(extra, out var parsed) ? (int?)parsed : null; heap.BindVariable(eWriter, new rt.ConstTerm((object?)intVal ?? extra)); args[argIdx++] = new rt.VarRef(eReader); }` — `int.tryParse(extra) ?? extra` (Dart polymorphic `Object?`) → `(object?)intVal ?? extra` (C# `??` requires same-type operands; the `(object?)` cast is REQUIRED).
  - `var (netWriter, netReader) = heap.AllocateVariable(); heap.BindVariable(netWriter, new rt.VarRef(netInReader)); args[argIdx++] = new rt.VarRef(netReader);`
  - `var env = new CallEnv(args: args); _runtime.SetGoalEnv(1, env); _runtime.SetGoalProgram(1, "main"); _runtime.Gq.Enqueue(new GoalRef(1, entryPC.Value));`
  - `var argsDesc = string.Join(", ", ExtraArgs.Prepend(agentIdLower).Append("NetIn")); var goalName = GoalLabel.Split('/')[0]; _Output($"[GOAL] Started {goalName}({argsDesc})"); _Log($"INIT: GQ length before initial run: {_runtime.Gq.Length}");` — Dart spread-literal `[a, ...xs, b].join(', ')` → LINQ `Prepend`/`Append` + `string.Join` (Microsoft Learn `Enumerable.Prepend`/`Append`).
  - `var initStatus = await _RunUntilQuiescentAsync(); _Log($"INIT: Initial run status: {initStatus}, GQ after: {_runtime.Gq.Length}");`
  - `_initialized = true; UpdateStats();`
  - `var firstFriend = Friends.Count > 0 ? Friends[0].ToLowerInvariant() : "friend"; _Output("[INIT] Ready! Commands:"); _Output($"  connect({firstFriend})         - cold-call {firstFriend}"); _Output($"  send({firstFriend}, hello)     - send text message"); _Output($"  decision(yes, {firstFriend}, 1) - accept befriend (req ID from output)"); _Output("  introduce(alice, charlie)     - introduce two friends");` — Dart `friends.isNotEmpty` → `Count > 0`; `friends.first` → `Friends[0]`.

- `injectUserInput` → `public async Task InjectUserInputAsync(string text)`:
  - `_Log($"USER_INPUT: {text}");`
  - `if (string.IsNullOrEmpty(text) || _userInput == null || _runtime == null) { _Log("USER_INPUT: early return (empty or not initialized)"); return; }` — `text.isEmpty` → `string.IsNullOrEmpty(text)` (Microsoft Learn 'String.IsNullOrEmpty'; NOT `IsNullOrWhiteSpace` which would change semantics).
  - `_Output($"> {text}");`
  - `try { var term = ParseTerm(text); _Log($"USER_INPUT: parsed -> {FormatTerm(term)}"); var activations = _userInput.Inject(term); _Log($"USER_INPUT: {activations.Count} activations"); foreach (var goal in activations) { _runtime.Gq.Enqueue(goal); } await _RunUntilQuiescentAsync(); } catch (Exception e) { _Log($"USER_INPUT ERROR: {e.Message}\n{e.StackTrace}"); _Output($"[ERROR] {e.Message}"); }` — two-binding `catch (e, st)` → `catch (Exception e)` + `e.StackTrace` (Microsoft Learn 'Exception.StackTrace').

- `onMadMessageReceived` → `public async Task OnMadMessageReceivedAsync(string from, byte[] payload)`:
  - `_Log($"MAD_RECV from {from} ({payload.Length} bytes)");`
  - `if (_runtime == null || _ctx == null || _netInput == null) { _Log("MAD_RECV: ERROR - runtime/ctx/netInput is null"); return; }`
  - Local-capture for closure-narrowing through NRT: `var runtime = _runtime; var ctx = _ctx; var netInput = _netInput;` (matches `mad_context.dart` `FlushMessages` convention).
  - `var serializer = new PayloadSerializer(AgentId.ToLowerInvariant()); var msg = serializer.DeserializeMessage(payload); _Log($"MAD_RECV: type={msg.Type}, dest={msg.Destination}");`
  - `if (msg.Type == MessageType.Assignment) { try { var (globalName, value) = serializer.DeserializeGlobalSendPayload(msg.Payload, isReader => { var (w, r) = runtime.Heap.AllocateVariable(); return isReader ? r : w; }); _Log($"MAD_ASSIGN: {globalName} := {FormatTerm(value)}"); ctx.HandleMadAssignment(globalName, value, from.ToLowerInvariant()); /* Reactivation handled by BindVariable() inside MadContext.HandleMadAssignment */ } catch (Exception e) { _Log($"MAD_ERROR: {e.Message}"); } } else if (msg.Type == MessageType.AgentMessage) { var term = serializer.DeserializeAgentMessagePayload(msg.Payload, isReader => { var (w, r) = runtime.Heap.AllocateVariable(); return isReader ? r : w; }); var formatted = FormatTerm(term); _Log($"AGENT_MSG: {formatted}"); _Log("INJECT into netInput"); var activations = netInput.Inject(term); _Log($"INJECT: {activations.Count} activations"); foreach (var goal in activations) { runtime.Gq.Enqueue(goal); } } else { _Log($"MAD_RECV: Unknown message type {msg.Type}"); }` — NOT a `switch` (per-arm side effects + try-catch on one arm + recursion make `if/else if/else` more readable). Reactivation-handled-inside comment preserved VERBATIM.
  - `UpdateStats(); await _RunUntilQuiescentAsync();`

- `onLegacyMessageReceived` → `public async Task OnLegacyMessageReceivedAsync(string from, object? payload)`:
  - Dart `dynamic` → C# `object?` (NOT C# `dynamic` — no method dispatch on payload; DLR overhead unwarranted).
  - `_Output($"[RECV from {from}] {payload}"); if (_netInput == null || _runtime == null) return; var msgTerm = new rt.StructTerm("msg", new List<rt.Term> { new rt.ConstTerm(from.ToLowerInvariant()), new rt.ConstTerm(AgentId.ToLowerInvariant()), new rt.ConstTerm(payload), }); var activations = _netInput.Inject(msgTerm); foreach (var goal in activations) { _runtime.Gq.Enqueue(goal); } await _RunUntilQuiescentAsync();`

- `_sendMadPayload` → `private async Task _SendMadPayloadAsync(string to, byte[] payload)`:
  - `_Log($"SEND_MAD to {to} ({payload.Length} bytes)"); var cb = OnSendMadMessage; if (cb != null) { await cb(to, payload); }` — Dart `await onSendMadMessage?.call(to, payload)` (where `await null` is a Dart-special no-op) → explicit local-capture + null-check + await (LOAD-BEARING: C# `await null` throws NRE; Microsoft Learn 'Asynchronous programming' canonical pattern).

- `runUntilQuiescent` → `public Task<string?> RunUntilQuiescentAsync() => _RunUntilQuiescentAsync();` — expression-bodied pass-through; NO `async`/`await` to avoid state-machine allocation (Microsoft Learn 'Async Performance'). Doc-comment Section-3 citation preserved as XML `<remarks>`.

- `_runUntilQuiescent` → `private async Task<string?> _RunUntilQuiescentAsync()`:
  - **Async-without-internal-await**: declared `async` for Dart shape fidelity (CS1998 acceptable; alternative is `Task.FromResult` at each return which obscures shape).
  - `_Log($"RUN: start (GQ={_runtime?.Gq.Length ?? 0})");` — Dart `?.` + `??` byte-for-byte equivalent in C#.
  - `if (_scheduler == null || _runtime == null) { _Log("RUN: early return (not initialized)"); return null; }`
  - `try { var result = _scheduler.DrainWithStatus(debug: GlpTraceEnabled); _Log($"RUN: status={result.Status}, goals={result.GoalsRan.Count}"); GoalCount += result.GoalsRan.Count; var messagesFlushed = _ctx!.FlushMessages(); if (messagesFlushed > 0) { _Log($"RUN: flushed {messagesFlushed} messages"); } UpdateStats(); _Log($"RUN: done (status={result.Status.ToString()})"); return result.Status.ToString(); } catch (Exception e) { _Log($"RUN ERROR: {e.Message}\n{e.StackTrace}"); return "error"; }` — Dart enum `.name` → C# `.ToString()` (default enum stringification per Microsoft Learn 'Enum.ToString' returns the member name; per `scheduler.dart` convspec, `ExecutionStatus` has no name customisation so byte-for-byte identical). Literal `"error"` preserved VERBATIM.

- `parseTerm` → `public rt.Term ParseTerm(string termStr)`:
  - `var parseInput = $"_temp_({termStr})."; var lexer = new Lexer(parseInput); var tokens = lexer.Tokenize(); var parser = new Parser(tokens); var parsedAst = parser.Parse(); if (parsedAst.Procedures.Count == 0 || parsedAst.Procedures[0].Clauses.Count == 0) { throw new Exception("Could not parse term"); } var clause = parsedAst.Procedures[0].Clauses[0]; if (clause.Head.Args.Count == 0) { throw new Exception("No term to inject"); } return _AstToRuntimeTerm(clause.Head.Args[0]);` — Both error messages VERBATIM. Base-`Exception` preserved for Dart fidelity (a future refactor MAY introduce a typed exception class; not in scope here).

- `_astToRuntimeTerm` → `private rt.Term _AstToRuntimeTerm(ast.Term astTerm)`:
  - `if (astTerm is ast.ConstTerm constTerm) { return new rt.ConstTerm(constTerm.Value); } else if (astTerm is ast.VarTerm varTerm) { var (writerAddr, readerAddr) = _runtime!.Heap.AllocateVariable(); return new rt.VarRef(varTerm.IsReader ? readerAddr : writerAddr); } else if (astTerm is ast.StructTerm structTerm) { var args = structTerm.Args.Select(_AstToRuntimeTerm).ToList(); return new rt.StructTerm(structTerm.Functor, args); } else if (astTerm is ast.ListTerm listTerm) { return _AstListToRuntimeTerm(listTerm); } throw new Exception($"Unknown AST term type: {astTerm.GetType().Name}");` — `is X x` declaring narrowed locals (REUSED idiom); `.map(...).toList()` → LINQ `.Select(_AstToRuntimeTerm).ToList()` with method-group conversion; Dart `runtimeType` → `.GetType().Name` (unqualified type name).

- `_astListToRuntimeTerm` → `private rt.Term _AstListToRuntimeTerm(ast.ListTerm list)`:
  - `if (list.IsNil) { return new rt.ConstTerm("nil"); } var head = _AstToRuntimeTerm(list.Head!); rt.Term tail; if (list.Tail is ast.ListTerm tailList) { tail = _AstListToRuntimeTerm(tailList); } else if (list.Tail != null) { tail = _AstToRuntimeTerm(list.Tail); } else { tail = new rt.ConstTerm("nil"); } return new rt.StructTerm(".", new List<rt.Term> { head, tail });` — Dart NESTED TERNARY rewritten as `if/else if/else` chain (more readable with `is X x` pattern-narrowed locals). `"nil"` atom and `"."` cons-cell functor LOAD-BEARING for heap recognition (REUSED from `mad_context.dart`'s list-cell construction). `list.Head!` preserves the Dart bang-assert shape (C# null-forgiving operator).

- `derefTerm` → `public rt.Term DerefTerm(rt.Term term)`:
  - `if (_runtime == null) return term; if (term is rt.VarRef varRef) { var value = _runtime.Heap.GetValue(varRef.Addr); if (value != null && value is not rt.VarRef) { return DerefTerm(value); } return term; } if (term is rt.StructTerm structTerm) { var derefArgs = structTerm.Args.Select(DerefTerm).ToList(); return new rt.StructTerm(structTerm.Functor, derefArgs); } return term;` — Dart `is! rt.VarRef` → C# `is not rt.VarRef` (C# 9+ negated pattern; Microsoft Learn 'Pattern matching').

- `formatTerm` → `public string FormatTerm(rt.Term term)`:
  - `if (term is rt.ConstTerm constTerm) { object? val = constTerm.Value; if (val == null || (val is string s && s == "nil")) { return "[]"; } return val.ToString() ?? ""; } if (term is rt.VarRef varRef) { var isReader = _runtime?.Heap.IsReader(varRef.Addr) ?? false; return isReader ? $"X{varRef.Addr}?" : $"X{varRef.Addr}"; } if (term is rt.StructTerm structTerm) { if (structTerm.Functor == "." && structTerm.Args.Count == 2) { var elements = new List<string>(); rt.Term current = structTerm; while (current is rt.StructTerm cs && cs.Functor == "." && cs.Args.Count == 2) { elements.Add(FormatTerm(cs.Args[0])); current = cs.Args[1]; } if (current is rt.ConstTerm tail && (tail.Value == null || (tail.Value is string ts && ts == "nil"))) { return $"[{string.Join(", ", elements)}]"; } return $"[{string.Join(", ", elements)} | {FormatTerm(current)}]"; } var args = string.Join(", ", structTerm.Args.Select(FormatTerm)); return $"{structTerm.Functor}({args})"; } return term.ToString() ?? "";` — pattern-in-while-condition declares narrowed local per iteration (C# 8+; Microsoft Learn 'Patterns'); `?? ""` guards against `Object.ToString()` returning `string?` under NRT; `X<addr>` and `X<addr>?` formats preserved VERBATIM (diagnostic contract).

- `dispose` → `public void Dispose() { // No OutputObservers to dispose — output goes through _output/1 kernel }` — NOT implementing `IDisposable`; comment preserved VERBATIM (LOAD-BEARING documentation: prevents future maintainers from re-adding OutputObserver disposal logic that no longer applies).

### Threading model (INHERITED, NOT re-decided)

All state reachable through `AgentRuntime` is per-instance and isolate-local. The single-owning-thread invariant recorded once on `global_writers_table.dart.md` is REUSED here. `AgentRuntime` methods MUST run on the agent's owning execution context (per escalation #4 — heap_fcp single-owning-context; per escalation #5 — `isolate_manager` uses `Channel<T>` mailbox; both ratified, NOT re-decided here). NO `lock`/`SemaphoreSlim`/`Monitor`/`ConcurrentDictionary`/`Interlocked` inside any method.

## 3. Decomposed Task Units

- T1. `using` directives + dual library aliases (`using ast = ...; using rt = ...;`). Done.
- T2. `namespace lib.multiagent { ... }` declaration with file-header comments. Done.
- T3. `public class AgentRuntime` declaration (reference class, NOT record/sealed). Done.
- T4. Seven get-only auto-properties for `final` ctor-bound fields with `IReadOnlyList<string>` widening. Done.
- T5. Three nullable public callback fields (`Action<string>?`, `Action<string, string>?`, `Func<string, byte[], Task>?`). Done.
- T6. Five nullable private runtime-state fields. Done.
- T7. `private bool _initialized = false` declaration-site initialiser. Done.
- T8. Four public mutable int stats fields + one public mutable bool. Done.
- T9. Three public get-only computed properties + one private `_Tag` (using `ToUpperInvariant`). Done.
- T10. Single positional ctor with optional defaults + ctor-body coalesce to `Array.Empty<string>()`. Done.
- T11. `_Log` / `_Output` expression-bodied `?.Invoke` helpers. Done.
- T12. `UpdateStats()` with NRT-narrowed dereference (no bang-asserts). Done.
- T13. `InitializeAsync()` ~19-step orchestration (cascade → object-initializer, dictionary-literal → collection-initializer, spread-list → Prepend/Append + string.Join, `int.TryParse` null-or-string fallback with `(object?)` cast, async-void lambda on void-typed delegate, Spec 4.1 comment preserved, 6-label diagnostic). Done.
- T14. `InjectUserInputAsync` with `string.IsNullOrEmpty` guard + try/catch + two-binding catch (`Exception e` + `e.StackTrace`). Done.
- T15. `OnMadMessageReceivedAsync` with if/else if/else enum dispatch + local-capture for NRT through closure + reactivation-comment preservation. Done.
- T16. `OnLegacyMessageReceivedAsync` with `dynamic` → `object?`. Done.
- T17. `_SendMadPayloadAsync` with local-capture + null-check + await (NOT `await cb?.Invoke`). Done.
- T18. `RunUntilQuiescentAsync` expression-bodied pass-through (no async/await). Done.
- T19. `_RunUntilQuiescentAsync` async-without-internal-await with try/catch + literal `"error"` + `.ToString()` for enum name. Done.
- T20. `ParseTerm` lex-parse-validate-extract pipeline with verbatim error messages. Done.
- T21. `_AstToRuntimeTerm` recursive `is X x` dispatch + method-group LINQ Select + `GetType().Name` for diagnostic. Done.
- T22. `_AstListToRuntimeTerm` nested-ternary → if/else if/else chain + `"nil"`/`"."` LOAD-BEARING literals. Done.
- T23. `DerefTerm` recursive dispatch with `is not rt.VarRef` (C# 9+ negated pattern). Done.
- T24. `FormatTerm` with pattern-in-while-condition + `?? ""` ToString guard + `X<addr>` / `X<addr>?` format preservation. Done.
- T25. `Dispose()` no-op with VERBATIM explanatory comment (NOT implementing IDisposable). Done.

## 4. Research Findings

None required. Every non-trivial construct is grounded in either (a) authoritative Microsoft Learn documentation cited verbatim in the convspec's `## Rationale and research provenance` section, OR (b) REUSED from sibling convspecs in `lib/multiagent/` (`global_writers_table.dart.md`, `global_send.dart.md`, `message_queue.dart.md`, `mad_helpers.dart.md`, `payload_serializer.dart.md`, `mad_context.dart.md`), `lib/runtime/` (`external_io.dart.md`, `runtime.dart.md`, `terms.dart.md`, `heap_fcp.dart.md`, `scheduler.dart.md`, `machine_state.dart.md`), `lib/engine/` (`glp_engine.dart.md`), `lib/compiler/` (`ast.dart.md`, `lexer.dart.md`, `parser.dart.md`). The threading-model decisions (escalations #4 heap_fcp single-owning-context, #5 isolate_manager Channel<T>) are RATIFIED at the project level and INHERITED here verbatim.

## 5. Consistency Pass

Fixed — derived from the ratified convspec at `.codeconv/conversion-specs/lib/multiagent/agent_runtime.dart.md` (sha256-pinned via `source_sha256: 16fa2e171ac7e27ce67020b5a539c9e42355fb38c61a305c827133ddbf373b8f`; zero open escalations per the convspec's `escalations: []` field and its closing `## Notes` "Zero escalations" entry). All cross-file references (`MadContext`/`MessageDeliveryCallback`/`MessageType`/`PayloadSerializer`/`InputInjector`/`GlpRuntime`/`GlpEngine`/`Scheduler`/`BytecodeRunner`/`CallEnv`/`GoalRef`/`Lexer`/`Parser`/`ast.*`/`rt.*`) consistent with the sibling convspecs in `.codeconv/conversion-specs/lib/{multiagent,runtime,engine,bytecode,compiler}/`. Threading-model assumptions (single-owning-context for `AgentRuntime` per-agent state) consistent with the project-ratified escalation #4 (heap_fcp/mad_context single-owning-context, Gabi 2026-05-21) and escalation #5 (`isolate_manager.dart` `Channel<T>` mailbox actor model, Gabi 2026-05-21).

## 6. Escalations

None.
