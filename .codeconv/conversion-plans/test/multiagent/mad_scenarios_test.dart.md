---
path: test/multiagent/mad_scenarios_test.dart
cycle_group_id: 150
scc_siblings: []
generated_at: 2026-05-21T16:25:26Z
source_sha256: 59bbfd23496686b05f542804fbe56eb5e7e02e8154753ead5456b7d2f71d61a1
schema_version: 1
---

# Conversion Plan: test/multiagent/mad_scenarios_test.dart

## 1. Source Analysis

Dart inspection of `glp_runtime_net/test/multiagent/mad_scenarios_test.dart` (365 LOC, synchronous end-to-end scenario tests).

File-level structure:
- Header doc-comment (lines 1-6) describes the file as end-to-end madGLP scenario tests validating multi-agent scenarios from `madGLP-spec.md` Sections 5.4, 10.1-10.3.
- Seven `import` directives (lines 8-14): one for `package:test/test.dart`, six for SUT libraries — `package:glp_runtime/runtime/runtime.dart`, `runtime/terms.dart`, `multiagent/mad_context.dart`, `multiagent/message_queue.dart`, `multiagent/mad_helpers.dart`, `multiagent/global_send.dart`.
- One `void main()` (line 16) containing exactly FOUR sibling `group(label, () { test(...); })` calls — no nested groups, no `setUp`/`tearDown`, no `setUpAll`/`tearDownAll`.

The four groups (one `test` each):
1. Section 10.1 (lines 17-87) — "p sends stream X to q, p assigns X := [add|Xs1], q receives". Constructs two `GlpRuntime` + two `MadContext` (`ctxP`/`ctxQ`), allocates two heap variable pairs (`(writerXs, readerXs)`, `(writerXs1, readerXs1)`), globalizes `Xs?` (reader) for q, asserts `spawns.length == 1` and `globalizeEntryCount == 0`, localizes at q, assigns `ctxP.onMessageReady` to a statement-body lambda that routes to `ctxQ.handleMadAssignment`, binds `writerXs := [add|Xs1]`, fires `onWriterBound` + `flushMessages`, then dereferences `writerZq` at q and asserts the cons-cell structure.
2. Section 10.2 (lines 89-150) — "p sends [value(V?)|...] to q, q assigns V_q := Sum, p receives Sum". Constructs two runtimes/contexts, allocates `(writerV, readerV)`, globalizes V (writer) at p — asserts `lookupByIndex(1)` not null and `spawns` empty. Localizes at q (q gets writer), assigns `ctxQ.onMessageReady`, binds `writerVq := ConstTerm(100)`, fires `onWriterBound` + `flushMessages`, dereferences `writerV` at p, asserts value `100`.
3. Section 10.3 (lines 152-261) — "Bob forwards X from Alice to Charlie, Charlie assigns, Alice receives". Constructs three `GlpRuntime` + three `MadContext` (`ctxAlice`/`ctxBob`/`ctxCharlie`), allocates `(writerXBob, readerXBob)`. Bob globalizes reader for Alice (spawn, no entry). Alice localizes — gets reader, no spawn. Bob globalizes writer for Charlie (no spawn, entry). Charlie localizes — gets writer + spawn. Two `onMessageReady` lambdas (Charlie's also calls `onWriterBound` + `flushMessages` on Bob inside its body). Charlie binds `writerYCharlie := 'hello_from_charlie'`, fires onWriterBound + flushMessages on Charlie. Asserts Alice's `writerZAlice` deref equals `'hello_from_charlie'`.
4. Section 5.4 (lines 263-364) — "p exports [X, X?] to q, q assigns Y_q := T, T flows back to p". Constructs two runtimes/contexts, allocates `(writerX, readerX)`, globalizes BOTH writer and reader in a TWO-element list. Asserts `globalNames.length == 2`, asserts each `globalNames[i] == GlobalName.writer/reader('p', i+1)` (value-equality), `spawns.length == 1`, `globalizeEntryCount == 1`. Localizes at q — `useReader[0] == false`, `useReader[1] == true`, `spawns.length == 1`. Two `onMessageReady` lambdas wire q→p (handles `_w(p,1)`, binds X at p, fires onWriterBound + flushMessages on p) and p→q (handles `_r(p,2)`, binds Z_q at q). q binds `writerYq := 'value_from_q'`, fires onWriterBound + flushMessages on q. Asserts both `writerX` at p AND `writerZq` at q deref to `'value_from_q'`.

Helper / type surface used (drawn from sibling SUT convspecs):
- `GlpRuntime()` zero-arg ctor.
- `runtime.heap` property returning a heap object exposing `allocateVariable()`, `bindVariable(int writer, Term value)`, `derefAddr(int addr) -> Term`.
- `MadContext(agentId: String, runtime: GlpRuntime)` named-required ctor.
- `ctx.wp` (`GlobalWritersTable` reference) with `lookupByIndex(int)` and `globalizeEntryCount` property.
- `ctx.onMessageReady` field (delegate-typed) taking `(String dest, OutboundMessage msg)`.
- `ctx.registerGlobalSendSpawns(List<GlobalSendSpawn>)`, `ctx.handleMadAssignment(globalName: GlobalName, value: Term, fromAgent: String)`, `ctx.onWriterBound(int writer, Term value)`, `ctx.flushMessages()`.
- Top-level helpers `globalize(variables:, localAgent:, remoteAgent:, table:)` and `localize(globalNames:, localAgent:, table:, freshAddrAllocator:)` returning a result record exposing `spawns`, `globalNames`, `freshPairs` (with `.writerAddr` field), `useReader` (List<bool>).
- `TermVar.reader(int reader, {int writerAddr})` and `TermVar.writer(int writer, {int readerAddr})` named-constructor factories.
- `GlobalName.writer(String agent, int index)` / `GlobalName.reader(String agent, int index)` named-constructor factories (value-equality required for line 296-297 assertions).
- Term ADT: `StructTerm(String functor, List<Term> args)` (positional ctor + `functor` / `args` getters), `ConstTerm(Object? value)`, `VarRef(int addr)`.

Notable Dart-3 features:
- Positional record destructuring `final (a, b) = heap.allocateVariable();` — five occurrences.
- Statement-body anonymous functions `(dest, msg) { if (dest == ...) { ... } }` — four occurrences, assigned to delegate-typed field `onMessageReady`.
- Zero-arg arrow lambda `() => runtime.heap.allocateVariable()` — three occurrences (one per agent that localises).
- Named-required parameter call sites for `MadContext`, `globalize`, `localize`, `handleMadAssignment`.
- `as` cast `final list = derefed as StructTerm;` plus inline `(x as ConstTerm).value` — five occurrences (one per scenario, the Section 10.1 one binds to a local).
- `isA<T>()` matcher inside `expect`, plus `isEmpty`, `isNotNull`, boolean-literal matchers.

No `async`, no `await`, no `Future`, no `Stream`, no `Completer`, no isolate APIs, no `Timer`, no concurrent-collection or lock primitive. All execution is single-threaded heap+callback orchestration per the agent-ownership invariant pinned by the `mad_context.dart` convspec.

Numeric literal: `ConstTerm(100)` (lines 132 + 139) — Dart `int` literal `100` passed through an `Object?`-typed ctor parameter.

String literals: all single-quoted; agent IDs `'p'`/`'q'`/`'alice'`/`'bob'`/`'charlie'`; payloads `'add'`/`'.'`/`'hello_from_charlie'`/`'value_from_q'`.

Identifiers using underscores in comments only: `X_c`, `Y_q`, `V_q`, `Z_a`, `Z_q`, `Xs1`, `_w(p,1)`, `_r(p,2)` etc.; executable code uses camelCase Dart locals (`writerXBob`, `writerYCharlie`, `readerXs1`).

## 2. Dart → C#/.NET Conversion Plan

Each construct mirrors the ratified convspec verbatim. The structured-block `constructs:` rows are the authority; this section restates them in plan form.

### C1. File header + using directives
- Drop `import 'package:test/test.dart';` → emit `using Xunit;` at file scope.
- Drop the six `package:glp_runtime/...` SUT imports → collapse to TWO directives:
  - `using <RootNs>.Runtime;` (covers `runtime.dart` + `terms.dart`)
  - `using <RootNs>.Multiagent;` (covers `mad_context.dart` + `message_queue.dart` + `mad_helpers.dart` + `global_send.dart`)
- Add `using System.Collections.Generic;` (the test bodies materialise `List<TermVar>` and `List<Term>` literals).
- Emit `using static <RootNs>.Multiagent.MadHelpers;` so the call sites `Globalize(...)` / `Localize(...)` read unqualified, mirroring the Dart shape.
- (idiom: `rf-dart-package-test-to-dotnet-xunit`, `rf-dart-package-sut-import-to-csharp-using`.)

### C2. Namespace declaration
- Emit `namespace <RootNs>.Test.Multiagent;` (file-scoped namespace, .NET 6+ convention) mirroring the Dart `test/multiagent` directory path.

### C3. Drop `void main()`; emit four sibling test classes
- Dart `void main() { group(...); group(...); group(...); group(...); }` has no C# equivalent; xUnit discovers `[Fact]` methods by reflection.
- The four sibling `group(...)` calls become FOUR sibling `public class` declarations:
  - `Section101DirectCommunicationClientMonitorTests` — `[Trait("Group", "Section 10.1: Direct Communication (Client-Monitor)")]`
  - `Section102ReturnValueScenarioTests` — `[Trait("Group", "Section 10.2: Return Value Scenario")]`
  - `Section103FriendMediatedIntroductionTests` — `[Trait("Group", "Section 10.3: Friend-Mediated Introduction")]`
  - `Section54BothEndsExportedTests` — `[Trait("Group", "Section 5.4: Both Ends Exported")]`
- No nested classes; no constructor / `IDisposable.Dispose` (no `setUp`/`tearDown` in source). xUnit's per-test fresh-instance lifecycle gives the same isolation as Dart's per-test callback.
- (idiom: `rf-dart-test-main-to-xunit-class-with-facts`, `rf-dart-package-test-group-to-xunit-class`.)

### C4. Each `test(label, body)` → `[Fact(DisplayName = "<original label>")] public void <Method>()`
- Method-name mangling (strip non-identifier chars, PascalCase remaining tokens, preserve underscores AS-IS for `X_c`/`Y_q`/`V_q`/`Xs1`):
  - `'p sends stream X to q, p assigns X := [add|Xs1], q receives'` → `PSendsStreamXToQPAssignsXAddXs1QReceives`
  - `'p sends [value(V?)|...] to q, q assigns V_q := Sum, p receives Sum'` → `PSendsValueVToQQAssignsV_qSumPReceivesSum`
  - `'Bob forwards X from Alice to Charlie, Charlie assigns, Alice receives'` → `BobForwardsXFromAliceToCharlieCharlieAssignsAliceReceives`
  - `'p exports [X, X?] to q, q assigns Y_q := T, T flows back to p'` → `PExportsXXToQQAssignsY_qTTFlowsBackToP`
- Each method returns `void` (sources are synchronous; no async/Task).
- The Dart `//` "Corrected definitions:" / "Corrected scenario per spec Section X.Y:" comments at the head of each test body emit as a `/// <summary>` doc-comment block (FR-024 doc-level — preserves `madGLP-spec.md` section traceability).
- (idiom: `rf-dart-test-callback-to-xunit-method-body`.)

### C5. `final <local> = <expr>;` → `var <local> = <expr>;`
- `final runtimeP = GlpRuntime();` → `var runtimeP = new GlpRuntime();` (C# requires `new`).
- `final ctxP = MadContext(agentId: 'p', runtime: runtimeP);` → `var ctxP = new MadContext("p", runtimeP);` — positional ctor per the SUT spec `lib/multiagent/mad_context.dart.md` (named-arg labels dropped).
- `final globalizeResult = globalize(...)` → `var globalizeResult = Globalize(...);` (under `using static MadHelpers`) or `MadHelpers.Globalize(...)` (qualified).
- `final localizeResult = localize(...)` → `var localizeResult = Localize(...);` similarly.
- `final writerZq = localizeResult.freshPairs[0].writerAddr;` → `var writerZq = localizeResult.FreshPairs[0].WriterAddr;`.
- `final streamValue = StructTerm('.', [ConstTerm('add'), VarRef(readerXs1)]);` → `var streamValue = new StructTerm(".", new List<Term> { new ConstTerm("add"), new VarRef(readerXs1) });`.
- `final derefed = runtimeQ.heap.derefAddr(writerZq);` → `var derefed = runtimeQ.Heap.DerefAddr(writerZq);`.
- `final list = derefed as StructTerm;` → `var list = (StructTerm)derefed;` OR the folded form `var list = Assert.IsType<StructTerm>(derefed);` (preferred).
- (idiom: `rf-dart-final-local-to-csharp-var-local`.)

### C6. Positional record destructuring `final (a, b) = expr;` → `var (a, b) = expr;`
- Five occurrences (lines 31, 32, 103, 173, 282). All reuse `rf-dart-record-destructuring-to-csharp-valuetuple-deconstruction` verbatim.
- `final (writerXs, readerXs) = runtimeP.heap.allocateVariable();` → `var (writerXs, readerXs) = runtimeP.Heap.AllocateVariable();` (both elements inferred as `int`; `AllocateVariable` returns `(int writerAddr, int readerAddr)`).
- Same shape for the other four (`(writerXs1, readerXs1)`, `(writerV, readerV)`, `(writerXBob, readerXBob)`, `(writerX, readerX)`).

### C7. Named-constructor factory `Foo.bar(...)` → static factory `Foo.Bar(...)`
- `TermVar.reader(readerXs, writerAddr: writerXs)` → `TermVar.Reader(readerXs, writerAddr: writerXs)`.
- `TermVar.writer(writerV, readerAddr: readerV)` → `TermVar.Writer(writerV, readerAddr: readerV)`.
- Same for `TermVar.reader(readerXBob, writerAddr: writerXBob)`, `TermVar.writer(writerXBob, readerAddr: readerXBob)`, `TermVar.writer(writerX, readerAddr: readerX)`, `TermVar.reader(readerX, writerAddr: writerX)`.
- `GlobalName.writer('p', 1)` → `GlobalName.Writer("p", 1)`; `GlobalName.reader('p', 2)` → `GlobalName.Reader("p", 2)`.
- (idiom: `rf-dart-named-constructor-to-csharp-static-factory`.)

### C8. Named-required parameter invocation — call shape per callee SUT spec
- `MadContext(agentId: ..., runtime: ...)` → positional `new MadContext(<agentId>, <runtime>)` (SUT spec pins positional C# ctor).
- `globalize(variables: ..., localAgent: ..., remoteAgent: ..., table: ...)` → preserves named args: `Globalize(variables: ..., localAgent: ..., remoteAgent: ..., table: ...);`.
- `localize(globalNames: ..., localAgent: ..., table: ..., freshAddrAllocator: ...)` → preserves named args: `Localize(globalNames: ..., localAgent: ..., table: ..., freshAddrAllocator: () => ...);`.
- `ctx.handleMadAssignment(globalName: ..., value: ..., fromAgent: ...)` → `ctx.HandleMadAssignment(globalName: ..., value: ..., fromAgent: ...);` (PascalCased method, camelCase named-arg labels preserved).
- (idiom: `rf-dart-named-argument-to-csharp-named-argument`.)

### C9. Zero-arg arrow lambda `() => <expr>`
- `() => runtimeQ.heap.allocateVariable()` → `() => runtimeQ.Heap.AllocateVariable()` (identical shape; both languages require `()`).
- Three occurrences (one per agent that localises — `runtimeQ`, `runtimeAlice`, `runtimeCharlie`).
- Lambda assigned to `freshAddrAllocator` parameter of type `Func<(int writerAddr, int readerAddr)>`.
- (idiom: `rf-dart-arrow-lambda-to-csharp-lambda`.)

### C10. Statement-body lambda assigned to delegate-typed field
- `ctxP.onMessageReady = (dest, msg) { if (dest == 'q') { ... } };` → `ctxP.OnMessageReady = (dest, msg) => { if (dest == "q") { ... } };` (C# requires `=>` arrow even for statement-body form).
- Four occurrences (one per test). The Section 10.3 Charlie→Bob lambda also issues `ctxBob.OnWriterBound(...)` + `ctxBob.FlushMessages()` inside the body — demonstrates multi-statement lambda body.
- Parameter types inferred from `MessageDeliveryCallback` delegate signature `(string destination, OutboundMessage message)`; codegen MAY leave parameters inferred (`(dest, msg) => { ... }`) or explicit (`(string dest, OutboundMessage msg) => { ... }`) — inferred form matches Dart shape.
- Direct assignment `=` preserved (delegate-typed FIELD per SUT spec, NOT `event`; `+=` multicast is grammatically allowed but not the source convention).
- (idiom: `rf-dart-statement-body-lambda-to-csharp-statement-body-lambda`.)

### C11. List literal → `new List<T> { ... }` collection initializer
- `[TermVar.reader(readerXs, writerAddr: writerXs)]` → `new List<TermVar> { TermVar.Reader(readerXs, writerAddr: writerXs) }`.
- Two-element `[TermVar.writer(writerX, readerAddr: readerX), TermVar.reader(readerX, writerAddr: writerX)]` → `new List<TermVar> { TermVar.Writer(writerX, readerAddr: readerX), TermVar.Reader(readerX, writerAddr: writerX) }`.
- Polymorphic `[ConstTerm('add'), VarRef(readerXs1)]` → `new List<Term> { new ConstTerm("add"), new VarRef(readerXs1) }` (explicit `<Term>` mandatory — `new List { ... }` does not compile).
- (idiom: `rf-dart-list-literal-to-csharp-list-initializer`.)

### C12. `expect(actual, isA<T>())` → `Assert.IsType<T>(actual)` (with optional fold)
- Five occurrences: lines 81 (`StructTerm`), 84 (`ConstTerm`), 147 (`ConstTerm`), 258 (`ConstTerm`), 356 (`ConstTerm`), 361 (`ConstTerm`).
- Simple form: `Assert.IsType<T>(derefed);` (return value discarded).
- Folded form (preferred): `var list = Assert.IsType<StructTerm>(derefed);` — eliminates both the simple assert AND the subsequent `as` cast.
- (idiom: `rf-dart-expect-isA-to-xunit-assert-istype`, `rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`.)

### C13. `expr as T` → `(T)expr` (or fold into `Assert.IsType<T>`)
- `final list = derefed as StructTerm;` → `var list = (StructTerm)derefed;` OR folded `var list = Assert.IsType<StructTerm>(derefed);`.
- `(list.args[0] as ConstTerm).value` → `((ConstTerm)list.Args[0]).Value`.
- `(derefed as ConstTerm).value` → `((ConstTerm)derefed).Value`. Same shape for `derefedP`, `derefedQ`.
- The preceding `Assert.IsType<T>` ensures runtime safety; both `(T)expr` and `Assert.IsType<T>(...)` throw on mismatch (Dart `as` throws `_CastError`, C# unconditional cast throws `InvalidCastException`).
- (idiom: `rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return`.)

### C14. `expect(actual, expected_literal)` → `Assert.Equal(expected, actual)` (argument order flips)
- `expect(globalizeResult.spawns.length, 1)` → `Assert.Equal(1, globalizeResult.Spawns.Count);` (`.length` → `.Count`).
- `expect(ctxP.wp.globalizeEntryCount, 0)` → `Assert.Equal(0, ctxP.Wp.GlobalizeEntryCount);`.
- `expect(list.functor, '.')` → `Assert.Equal(".", list.Functor);`.
- `expect((list.args[0] as ConstTerm).value, 'add')` → `Assert.Equal("add", ((ConstTerm)list.Args[0]).Value);`.
- `expect((derefed as ConstTerm).value, 100)` → `Assert.Equal(100, ((ConstTerm)derefed).Value);` (Dart `int 100` → C# `int 100` boxed to `object?` at the `Value` boundary).
- `expect((derefed as ConstTerm).value, 'hello_from_charlie')` → `Assert.Equal("hello_from_charlie", ((ConstTerm)derefed).Value);`.
- `expect((derefedP as ConstTerm).value, 'value_from_q')` / `expect((derefedQ as ConstTerm).value, 'value_from_q')` → `Assert.Equal("value_from_q", ((ConstTerm)derefedP).Value);` / `Assert.Equal("value_from_q", ((ConstTerm)derefedQ).Value);`.
- `expect(globalizeResult.globalNames[0], GlobalName.writer('p', 1))` → `Assert.Equal(GlobalName.Writer("p", 1), globalizeResult.GlobalNames[0]);` (value-equality required on the C# `GlobalName` type — pinned by `mad_helpers.dart.md` SUT spec).
- `expect(globalizeResult.globalNames[1], GlobalName.reader('p', 2))` → `Assert.Equal(GlobalName.Reader("p", 2), globalizeResult.GlobalNames[1]);`.
- `expect(globalizeResult.globalNames.length, 2)` → `Assert.Equal(2, globalizeResult.GlobalNames.Count);`.
- (idiom: `rf-dart-expect-equals-to-xunit-assertequal`.)

### C15. Boolean / presence matchers → typed xUnit assertions
- `expect(x, true)` → `Assert.True(x);` (covers `useReader[0]` true and `useReader[1]` true cases).
- `expect(x, false)` → `Assert.False(x);` (covers `useReader[0]` false cases).
- `expect(x, isNotNull)` → `Assert.NotNull(x);` (`ctxP.wp.lookupByIndex(1)`).
- `expect(x, isEmpty)` → `Assert.Empty(x);` (`globalizeResult.spawns` / `aliceFromBob.spawns` / `bobToCharlieGlobal.spawns`).
- (idioms: `rf-dart-expect-istrue-to-xunit-asserttrue`, `rf-dart-expect-isfalse-to-xunit-assertfalse`, `rf-dart-expect-isnotnull-to-xunit-assertnotnull`, `rf-dart-expect-isempty-to-xunit-assert-empty`.)

### C16. Instance-method invocation `receiver.camelCaseMethod(args)` → `receiver.PascalCaseMethod(args)`
- `ctxP.registerGlobalSendSpawns(globalizeResult.spawns)` → `ctxP.RegisterGlobalSendSpawns(globalizeResult.Spawns);`.
- `ctxP.onWriterBound(writerXs, streamValue)` → `ctxP.OnWriterBound(writerXs, streamValue);`.
- `ctxP.flushMessages()` → `ctxP.FlushMessages();`.
- `runtimeP.heap.bindVariable(writerXs, streamValue)` → `runtimeP.Heap.BindVariable(writerXs, streamValue);` (two PascalCased members on the chain — `Heap` property AND `BindVariable` method).
- `runtimeP.heap.derefAddr(writerV)` → `runtimeP.Heap.DerefAddr(writerV);`.
- `ctx.handleMadAssignment(globalName: ..., value: ..., fromAgent: ...)` → `ctx.HandleMadAssignment(globalName: ..., value: ..., fromAgent: ...);`.
- (idiom: `rf-dart-instance-method-call-to-csharp-pascalcase-call`.)

### C17. Indexed property access `receiver.list[i]` → `receiver.List[i]`
- `localizeResult.freshPairs[0].writerAddr` → `localizeResult.FreshPairs[0].WriterAddr` (the trailing `.writerAddr` is the SUT-spec-determined field name on the FreshPair record/tuple — PascalCased if a named record per `mad_helpers.dart.md`).
- `globalizeResult.globalNames[0]` / `[1]` → `globalizeResult.GlobalNames[0]` / `[1]`.
- `localizeResult.useReader[0]` / `[1]` → `localizeResult.UseReader[0]` / `[1]`.
- `list.args[0]` → `list.Args[0]`.
- Same shape for `aliceFromBob.freshPairs[0].writerAddr`, `charlieFromBob.freshPairs[0].writerAddr`, `bobToAliceGlobal.globalNames[0]`, `bobToCharlieGlobal.globalNames[0]`.
- (idiom: `rf-dart-list-indexer-to-csharp-list-indexer`.)

### C18. Spec-notation identifiers in comments preserved verbatim
- `X_c`, `Y_q`, `V_q`, `Z_a`, `Z_q`, `Xs1`, `_w(p,1)`, `_r(p,2)`, `X?`, `[value(V?)|...]`, `:=` survive intact inside `///` doc-comments — no executable translation required.
- (idiom: `rf-dart-identifier-spec-notation-in-comments-preserved`.)

### C19. Cross-cutting member-naming discipline
- Dart camelCase instance methods + properties → C# PascalCase. Apply to every receiver-chain (`runtime.heap.bindVariable` → `runtime.Heap.BindVariable`).
- Local variables, parameters, named-argument labels stay camelCase (`writerZq`, `globalName:`, `variables:`).

### C20. String literal conversion
- Every Dart single-quoted string `'...'` → C# double-quoted `"..."`. Codegen MUST emit `"p"` not `'p'` (single quotes would select non-existent `char`-arg ctors on `MadContext`/`GlobalName`/`ConstTerm`).

## 3. Decomposed Task Units

- T1. Emit file header: `using Xunit;`, `using System.Collections.Generic;`, `using <RootNs>.Runtime;`, `using <RootNs>.Multiagent;`, `using static <RootNs>.Multiagent.MadHelpers;`, plus file-scoped `namespace <RootNs>.Test.Multiagent;`. (done one-liner)
- T2. Emit `[Trait("Group", "Section 10.1: Direct Communication (Client-Monitor)")] public class Section101DirectCommunicationClientMonitorTests` with the single `[Fact(DisplayName = "p sends stream X to q, p assigns X := [add|Xs1], q receives")] public void PSendsStreamXToQPAssignsXAddXs1QReceives()` method whose body translates lines 18-86 verbatim per C5–C17. (done one-liner)
- T3. Emit `[Trait("Group", "Section 10.2: Return Value Scenario")] public class Section102ReturnValueScenarioTests` with the single `[Fact(DisplayName = "p sends [value(V?)|...] to q, q assigns V_q := Sum, p receives Sum")] public void PSendsValueVToQQAssignsV_qSumPReceivesSum()` method whose body translates lines 90-149 verbatim per C5–C17. (done one-liner)
- T4. Emit `[Trait("Group", "Section 10.3: Friend-Mediated Introduction")] public class Section103FriendMediatedIntroductionTests` with the single `[Fact(DisplayName = "Bob forwards X from Alice to Charlie, Charlie assigns, Alice receives")] public void BobForwardsXFromAliceToCharlieCharlieAssignsAliceReceives()` method whose body translates lines 153-260 verbatim per C5–C17, including the two `OnMessageReady` lambdas (Charlie→Bob lambda issues `OnWriterBound` + `FlushMessages` inside its body). (done one-liner)
- T5. Emit `[Trait("Group", "Section 5.4: Both Ends Exported")] public class Section54BothEndsExportedTests` with the single `[Fact(DisplayName = "p exports [X, X?] to q, q assigns Y_q := T, T flows back to p")] public void PExportsXXToQQAssignsY_qTTFlowsBackToP()` method whose body translates lines 264-363 verbatim per C5–C17, asserting BOTH `writerX` at p and `writerZq` at q deref to `"value_from_q"`. (done one-liner)
- T6. For each test method, emit `/// <summary>` doc-comment block carrying the `madGLP-spec.md` Section X.Y reference + the verbatim "Corrected definitions:" / "Corrected scenario per spec Section X.Y:" annotations from the Dart source so spec-traceability survives FR-024. (done one-liner)
- T7. Apply C20 string-literal conversion uniformly: every `'…'` → `"…"`. (done one-liner)
- T8. Apply C19 member-naming discipline uniformly: PascalCase every method + property name, leave locals/params/named-arg labels camelCase. (done one-liner)

## 4. Research Findings

none required — every construct row in the ratified convspec carries either a pinned `idiom_id` (KB cache hit) or a `research_finding_id` with authoritative Dart + .NET citations. The convspec's KB-cache-hits enumeration (lines 1093-1110 of the convspec) lists 15 reused pinned rf-ids; the four newly-recorded rf-ids (`rf-dart-statement-body-lambda-to-csharp-statement-body-lambda`, `rf-dart-instance-method-call-to-csharp-pascalcase-call`, `rf-dart-list-indexer-to-csharp-list-indexer`, `rf-dart-identifier-spec-notation-in-comments-preserved`) cite both dart.dev and Microsoft Learn URLs inside the convspec's `nuance:` text. No re-research performed (FR-024 reproducibility-offline rule).

## 5. Consistency Pass

- C1 (using directives + namespace) — fixed — derived from convspec rows `dart.package_test.import_directive` + `dart.package_test.import_sut_relative_package` (lines 13-118) and human-readable rationale § `rf-dart-package-sut-import-to-csharp-using`.
- C2 (namespace declaration) — fixed — derived from convspec `dart.package_test.import_directive` `target_decision` ("Codegen projects to a single namespace mirroring the Dart `test/multiagent` directory") and `conversion_units: namespace_declaration`.
- C3 (four sibling test classes) — fixed — derived from convspec rows `dart.package_test.main_entrypoint` + `dart.package_test.group_block` (lines 119-191) plus human-readable rationale § "Four sibling classes".
- C4 (`[Fact]` method per test) — fixed — derived from convspec row `dart.package_test.test_call_executable` (lines 192-244) plus the four method-name mangling rules listed verbatim there.
- C5 (`final` → `var`) — fixed — derived from convspec row `dart.expression.final_local_variable_with_initializer` (lines 245-329).
- C6 (record destructuring) — fixed — derived from convspec row `dart.expression.record_destructuring_pattern_assignment` (lines 330-385).
- C7 (named-ctor factory) — fixed — derived from convspec row `dart.class.named_constructor_factory` (lines 386-424).
- C8 (named-required-arg call shape per SUT) — fixed — derived from convspec row `dart.class.named_required_parameter_constructor_invocation` (lines 425-484) plus human-readable § "MadContext ctor: positional C# despite Dart named-required".
- C9 (zero-arg arrow lambda) — fixed — derived from convspec row `dart.expression.lambda_zero_arg_arrow` (lines 485-513).
- C10 (statement-body lambda → delegate field) — fixed — derived from convspec row `dart.expression.statement_bodied_lambda_assigned_to_delegate_field` (lines 514-574) plus human-readable § "rf-dart-statement-body-lambda-to-csharp-statement-body-lambda".
- C11 (typed list literal) — fixed — derived from convspec row `dart.expression.list_literal_typed_polymorphic` (lines 575-626).
- C12 (`isA<T>` matcher) — fixed — derived from convspec row `dart.expression.expect_isA_to_xunit_assert_istype` (lines 627-669).
- C13 (`as` cast + fold) — fixed — derived from convspec row `dart.expression.as_cast_after_isA_assertion` (lines 670-703) plus human-readable § "rf-dart-expect-isA-plus-ascast-fold-to-xunit-istype-return".
- C14 (`Assert.Equal` arg-order flip + value-equality on `GlobalName`) — fixed — derived from convspec row `dart.expression.expect_equals_to_xunit_assertequal` (lines 704-772).
- C15 (`Assert.True`/`False`/`NotNull`/`Empty`) — fixed — derived from convspec row `dart.expression.expect_istrue_isfalse_isempty_isnotnull` (lines 773-808).
- C16 (instance-method PascalCase) — fixed — derived from convspec row `dart.expression.method_invocation_on_owned_madcontext` (lines 809-866).
- C17 (indexed property access) — fixed — derived from convspec row `dart.expression.indexed_property_access` (lines 867-914).
- C18 (spec notation in comments) — fixed — derived from convspec row `dart.expression.dart_3_numeric_underscore_in_identifier` (lines 915-943).
- C19 (cross-cutting member-naming discipline) — fixed — derived from convspec row `dart.expression.method_invocation_on_owned_madcontext` `nuance:` "Member-naming-PascalCase nuance (project-wide rule, IDENTICAL to all sibling test specs)".
- C20 (string literal conversion) — fixed — derived from convspec row `dart.expression.final_local_variable_with_initializer` `nuance:` "String-literal nuance: Dart `'p'` … C# uses ONLY `"…"` for `string`."

## 6. Escalations

None.
