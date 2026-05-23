---
path: lib/runtime/glp_activation.dart
cycle_group_id: 36
scc_siblings: [lib/bytecode/runner.dart, lib/multiagent/mad_context.dart, lib/runtime/body_kernels.dart, lib/runtime/runtime.dart, lib/runtime/system_predicates.dart]
generated_at: 2026-05-21T17:00:00Z
source_sha256: ffba37a1c2ae6161898532e842040e38b1aaab8a818fe9c60bd4a001952688c4
schema_version: 1
---

# Conversion Plan: lib/runtime/glp_activation.dart

## 1. Source Analysis

Inspection of `glp_runtime_net/lib/runtime/glp_activation.dart` (92 lines, sha256 `ffba37a1…2688c4`):

- File header: triple-slash module doc-block (lines 1-6) describing "GLP-level module activation" as Phase 4 of dynamic module dispatch, followed by `library;` directive (line 7) with no library name.
- Imports (lines 9-13): five `import 'package:glp_runtime/...';` package-internal directives:
  - `runtime/runtime.dart` (provides `GlpRuntime`, `CallEnv`)
  - `runtime/terms.dart` (provides `Term`, `VarRef`, `ConstTerm`, `StructTerm`, `ModuleTerm`)
  - `runtime/heap_fcp.dart` (provides `HeapFCP`)
  - `runtime/machine_state.dart` (provides `GoalRef`)
  - `bytecode/runner.dart` (provides `BytecodeProgram`, `BytecodeRunner`)
  No `show`/`hide` narrowing.
- `class GlpChannelHandle` (lines 19-46): a mutable single-writer state container.
  - Fields: `final HeapFCP _heap` (private, immutable reference to shared heap); `int _writerAddr` (private, mutable writer-address advanced on each `send`).
  - Constructor (line 23): single positional with `this.field` shorthand binding both args directly to private fields.
  - Getter `int get writerAddr => _writerAddr;` (line 26): expression-bodied, exposes read-only view of the writer-address field.
  - Method `List<GoalRef> send(Term goal)` (lines 32-38): four statements — (1) record-destructure `(tailWriterAddr, _)` from `_heap.allocateVariable()` discarding the reader; (2) build cons-cell `StructTerm('.', [goal, VarRef(tailWriterAddr)])`; (3) call `_heap.bindVariable(_writerAddr, consCell)` returning woken activations; (4) advance `_writerAddr = tailWriterAddr;` and return activations.
  - Method `List<GoalRef> close()` (lines 43-45): single-expression body binding the current writer to `ConstTerm('nil')` sentinel and returning the resulting activations.
  - No `==`/`hashCode`/`toString` override → default reference-identity equality.
- Top-level function `GlpChannelHandle activateModule({required ...})` (lines 55-91): four `required` named parameters (`GlpRuntime rt`, `BytecodeProgram serveBytecode`, `BytecodeProgram moduleBytecode`, `String moduleName`), no defaulted args. Body emits seven numbered steps via `// 1.` … `// 7.` `//`-line comments:
  1. `(writerAddr, readerAddr) = rt.heap.allocateVariable()` — both halves bound.
  2. Build `ModuleTerm(moduleBytecode, name: moduleName)`, store on heap via `rt.heap.storeTermOnHeap(moduleTerm)`.
  3. `goalId = rt.nextGoalId++` post-increment; build `CallEnv(args: {0: VarRef(moduleAddr), 1: VarRef(readerAddr)})`; `rt.setGoalEnv(goalId, env)`; `rt.setGoalProgram(goalId, serveBytecode)`.
  4. `servePc = serveBytecode.labels['serve/2']!` null-bang assertion; `rt.gq.enqueue(GoalRef(goalId, servePc))`.
  5. `rt.infrastructureGoalIds.add(goalId)` — Set add.
  6. `if (!rt.runners.containsKey(serveBytecode)) { rt.runners[serveBytecode] = BytecodeRunner(serveBytecode); }` — guard + insert.
  7. Build `GlpChannelHandle(rt.heap, writerAddr)`, store in `rt.glpChannels[moduleName]`, return.

No async / Stream / Future / isolate surface. No null-aware operators except the single `!` post-fix on the label lookup. No conditional/loop control flow in the function body — flat seven-step linear sequence whose order is load-bearing (registration AFTER goal enqueue per spec §3.4/§3.5 reference in the source comments).

## 2. Dart → C#/.NET Conversion Plan

Each construct rendered per the ratified convspec (`.codeconv/conversion-specs/lib/runtime/glp_activation.dart.md`). All eight construct decisions below are CACHE-HIT carry-forwards from prior runtime/* convspecs; no novel research surface.

| # | Dart construct | C#/.NET target |
|---|----------------|----------------|
| 1 | `library;` directive (line 7) + leading triple-slash module doc | Elide `library;`. Emit module doc-block as XML-doc `/// <summary>…</summary>` on the namespace declaration that mirrors `lib/runtime/`. |
| 2 | Five `import 'package:glp_runtime/…';` directives | Two deduplicated `using` directives: `using <root>.Runtime;` (covers `GlpRuntime`, `CallEnv`, `Term`, `VarRef`, `ConstTerm`, `StructTerm`, `ModuleTerm`, `HeapFCP`, `GoalRef`) and `using <root>.Bytecode;` (covers `BytecodeProgram`, `BytecodeRunner`). No `show`/`hide` analogue. |
| 3 | Triple-slash doc-blocks on class/getter/methods/function; in-body `// 1.` … `// 7.` step-numbered comments | Triple-slash → C# XML-doc `///` blocks on class/property/method/static-function. In-body `//` step-numbered comments stay verbatim as `//`-line comments (NOT promoted to XML-doc — implementation notes, not API documentation). |
| 4 | `class GlpChannelHandle { final HeapFCP _heap; int _writerAddr; … }` | `public class GlpChannelHandle` (reference type; default identity equality). Fields: `private readonly HeapFCP _heap;` and `private int _writerAddr;`. Public expression-bodied get-only property `public int WriterAddr => _writerAddr;`. NOT `record` / `record class` (would inject value-equality contradicting the identity-aliasing contract with `rt.glpChannels`); NOT `struct` / `record struct` (would copy-on-assignment, splitting writer state). |
| 5 | `GlpChannelHandle(this._heap, this._writerAddr);` positional ctor with `this.field` shorthand | `public GlpChannelHandle(HeapFCP heap, int writerAddr) { _heap = heap; _writerAddr = writerAddr; }`. Explicit body assignments (Dart shorthand has no C# counterpart). Parameter names strip the leading underscore (C# convention reserves `_camelCase` for private fields, not parameters). `_heap = heap;` aliases the caller's `HeapFCP` instance — no defensive copy — preserving the shared-heap contract. |
| 6 | `List<GoalRef> send(Term goal) { final (tailWriterAddr, _) = _heap.allocateVariable(); final consCell = StructTerm('.', [goal, VarRef(tailWriterAddr)]); final activations = _heap.bindVariable(_writerAddr, consCell); _writerAddr = tailWriterAddr; return activations; }` | `public IReadOnlyList<GoalRef> Send(Term goal) { var (tailWriterAddr, _) = _heap.AllocateVariable(); var consCell = new StructTerm(".", new List<Term> { goal, new VarRef(tailWriterAddr) }); var activations = _heap.BindVariable(_writerAddr, consCell); _writerAddr = tailWriterAddr; return activations; }`. C# discard `_` in tuple deconstruction is byte-equivalent. Cons-cell `.` functor literal preserved verbatim. `new List<Term> { … }` (growable, NOT array, NOT immutable) — the `StructTerm` ctor receives the list by reference per terms.dart.md convspec. `_writerAddr` mutation is a plain field-assignment. Return type `IReadOnlyList<GoalRef>` per the `List`→`IReadOnlyList` return-value convention from boot_loader.dart.md / external_io.dart.md (callers do not mutate). |
| 7 | `List<GoalRef> close() { return _heap.bindVariable(_writerAddr, ConstTerm('nil')); }` | `public IReadOnlyList<GoalRef> Close() => _heap.BindVariable(_writerAddr, new ConstTerm("nil"));`. Expression-bodied. Sentinel string `"nil"` preserved byte-identically (load-bearing: bytecode compiler + trace-log formatters key on the literal). NO `_closed` flag, NO double-close guard, NO `InvalidOperationException` — Dart source has none; codegen MUST NOT introduce semantics absent from the source (over-translation per FR-013/FR-024). |
| 8 | `GlpChannelHandle activateModule({required GlpRuntime rt, required BytecodeProgram serveBytecode, required BytecodeProgram moduleBytecode, required String moduleName}) { … 7 steps … }` | `public static GlpChannelHandle ActivateModule(GlpRuntime rt, BytecodeProgram serveBytecode, BytecodeProgram moduleBytecode, string moduleName)` on hosting `public static class GlpActivation`. All four parameters non-nullable positional (no defaults) — C# has no method-parameter `required` keyword (the C# 11 `required` modifier is property-only). Callers may use named-arg syntax `ActivateModule(rt: …, serveBytecode: …, …)` for call-site parity with Dart. Body emits the seven steps in **source order** (channel create → ModuleTerm store → goal spawn → label lookup + enqueue → infrastructure tag → runner register → handle register + return):<br>(1) `var (writerAddr, readerAddr) = rt.Heap.AllocateVariable();` — both halves bound, no discard.<br>(2) `var moduleTerm = new ModuleTerm(moduleBytecode, name: moduleName); var moduleAddr = rt.Heap.StoreTermOnHeap(moduleTerm);` — `name:` named-arg syntax preserved byte-identically.<br>(3) `var goalId = rt.NextGoalId++; var env = new CallEnv(args: new Dictionary<int, Term> { { 0, new VarRef(moduleAddr) }, { 1, new VarRef(readerAddr) } }); rt.SetGoalEnv(goalId, env); rt.SetGoalProgram(goalId, serveBytecode);` — Dart `{0: x, 1: y}` → C# `new Dictionary<int, Term> { { 0, x }, { 1, y } }` (double-brace dictionary initialiser, NOT nested object initialiser). Post-increment `NextGoalId++` has identical read-then-store-incremented semantics.<br>(4) `var servePc = serveBytecode.Labels["serve/2"]!; rt.Gq.Enqueue(new GoalRef(goalId, servePc));` — Dart `!` post-fix → C# null-forgiving `!`. The C# `Dictionary` indexer throws `KeyNotFoundException` if absent (vs Dart `TypeError`); both are "crash if absent" — codegen MUST NOT introduce `TryGetValue`+throw to mimic Dart's exception type (over-translation).<br>(5) `rt.InfrastructureGoalIds.Add(goalId);` — `Set.add` → `HashSet<int>.Add`; discarded return value faithful in both languages.<br>(6) `if (!rt.Runners.ContainsKey(serveBytecode)) { rt.Runners[serveBytecode] = new BytecodeRunner(serveBytecode); }` — explicit two-step form preserved; codegen MUST NOT replace with `TryAdd` (would lose the source's reviewable shape).<br>(7) `var channel = new GlpChannelHandle(rt.Heap, writerAddr); rt.GlpChannels[moduleName] = channel; return channel;` — straight construction + Dictionary indexed-insert + return. |

Side-effect ordering: the seven steps MUST be emitted in source order; reordering risks observable bugs (e.g. registering the handle before the goal is enqueued).

## 3. Decomposed Task Units

- T1: Emit namespace declaration mirroring `lib/runtime/` with XML-doc carrying the module header.
- T2: Emit `using <root>.Runtime;` and `using <root>.Bytecode;` (two deduplicated `using` directives covering all five Dart imports).
- T3: Emit `public class GlpChannelHandle` with `private readonly HeapFCP _heap` and `private int _writerAddr` fields plus `public int WriterAddr => _writerAddr;` expression-bodied get-only property.
- T4: Emit `GlpChannelHandle` positional constructor with explicit body assignments.
- T5: Emit `public IReadOnlyList<GoalRef> Send(Term goal)` with tuple-deconstruction-with-discard, cons-cell `StructTerm(".", …)`, `BindVariable` call, `_writerAddr` advance, return.
- T6: Emit `public IReadOnlyList<GoalRef> Close()` expression-bodied returning `_heap.BindVariable(_writerAddr, new ConstTerm("nil"))` — no idempotence guard.
- T7: Emit hosting `public static class GlpActivation` with `public static GlpChannelHandle ActivateModule(…)` method carrying the seven body steps in source order, including `//`-line step-numbered comments verbatim.
- T8: Cross-check label string `"serve/2"`, functor strings `"."` and `"nil"`, named-arg `name:` site, and `new Dictionary<int, Term> { { 0, … }, { 1, … } }` initialiser are byte-identical to the Dart source.

## 4. Research Findings

None required. All eight construct decisions are FR-024 cache hits drawn verbatim from prior runtime/* convspecs (research_finding_ids: `rf-dart-library-directive-to-csharp-namespace-elision`, `rf-dart-import-relative-to-csharp-using-namespace`, `rf-dart-mutable-state-class-identity-equality-to-csharp-class`, `rf-dart-positional-ctor-with-this-shorthand-to-csharp-positional-ctor-with-explicit-assignment`, `rf-dart-record-destructure-to-csharp-valuetuple-deconstruction`, `rf-dart-top-level-fn-builds-sum-type-leaf`, `rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults`). The convspec's "Rationale and research provenance" section lists the authoritative Dart and .NET documentation URLs underpinning each carry-forward; no WebSearch/WebFetch/Agent consultation was needed.

## 5. Consistency Pass

- Convspec mirror. Every construct decision in §2 mirrors the corresponding `target_decision` block in `.codeconv/conversion-specs/lib/runtime/glp_activation.dart.md`. No deviations: identity-equality class (not record/struct), `private readonly`/`private` field discipline, `IReadOnlyList<GoalRef>` return type, `var (tailWriterAddr, _)` discard tuple-deconstruction, `new List<Term> { … }` growable collection, `new Dictionary<int, Term> { { 0, … }, { 1, … } }` double-brace initialiser, `name:` named-arg call site, post-increment `NextGoalId++`, null-forgiving `!` on label indexer, explicit `ContainsKey`+indexed-insert (NOT `TryAdd`), `"."` and `"nil"` and `"serve/2"` literals byte-identical, seven steps in source order, no `_closed` guard introduced.

- SCC coherence (cycle_group_id 36, 5 siblings). The convspec front-matter records `cycle_group_id: 37` while the planning brief specifies `cycle_group_id: 36`; the planning brief is authoritative for this artefact's `cycle_group_id` field per the supplied parameters — front-matter cycle-group divergence does NOT alter any construct decision (no decision in §2 depends on the cycle_group_id integer). Cross-references with each sibling are recorded in §7.

- Threading model. No re-decision: per the ratified policy from commits `497428c8` (heap_fcp / mad_context single-owning-context, escalation #4) and `12a468f5` (isolate_manager `Channel<T>` mailbox, escalation #5), this file's `GlpChannelHandle` plain `private int _writerAddr` field (no `Interlocked`, no `volatile`, no `lock`) is the faithful single-owning-context render. `_heap.BindVariable` is owned by `HeapFCP`'s single-owning-context invariant — out of scope for this file.

- Discard pattern. `_` discard in tuple deconstruction is identical in Dart 3.0+ and C# 7+; no warning, no allocation, no binding. Used in `Send()` only — `ActivateModule` binds both halves of `AllocateVariable()`.

- Sentinel-string preservation. `"."` (cons functor), `"nil"` (empty-list sentinel), `"serve/2"` (label key) are load-bearing identifiers; codegen MUST NOT introduce typed sentinels (e.g. `ConstTerm.Nil`, `Functor.Cons`) that fork the source-to-spec correspondence with the bytecode compiler and trace logs.

- Over-translation avoidance. No `_closed` flag, no double-close guard, no `TryAdd`, no `TryGetValue`+throw mimic — all explicitly rejected by the convspec to preserve reviewable source shape.

- Codestyle conventions confirmed: PascalCase public surface (`WriterAddr`, `Send`, `Close`, `ActivateModule`, `GlpActivation`), `_camelCase` private instance fields (`_heap`, `_writerAddr`), namespace mirrors `lib/runtime/`, hosting static class `GlpActivation` named after the source library.

## 6. Escalations

None.

## 7. Cycle Siblings

This file is in SCC `cycle_group_id 36` with 5 siblings. Cross-references below note which decisions are co-dependent:

- **lib/bytecode/runner.dart** — Co-dependent on the `BytecodeProgram` and `BytecodeRunner` types referenced in §2 row 8 (steps 4 and 6) and in the import-mapping (row 2: `using <root>.Bytecode;`). This file constructs `new BytecodeRunner(serveBytecode)` and indexes `serveBytecode.Labels["serve/2"]`. The `BytecodeProgram.Labels` property MUST be a `Dictionary<string, int>` (or equivalent indexable map) for the C# indexer `Labels["serve/2"]!` to compile; `BytecodeRunner` MUST be a reference-type class constructible from a single `BytecodeProgram` positional argument. No other coupling — runner.dart's internal opcode-execution surface is irrelevant to this file.

- **lib/multiagent/mad_context.dart** — Co-dependent indirectly via the shared `HeapFCP` single-owning-context invariant ratified in escalation #4 (commit `497428c8`). This file's `_heap = heap;` aliasing relies on `mad_context.dart` and `glp_activation.dart` agreeing that `HeapFCP` references are passed by reference with no defensive copy and mutations to the shared heap are observed by every holder. No direct type or symbol used from mad_context here; coupling is purely policy-level (no decision in §2 mentions mad_context types).

- **lib/runtime/body_kernels.dart** — Co-dependent on the cons-cell encoding precedent `StructTerm('.', [head, tailVarRef])` (carry-forward `rf-dart-cons-cell-encoding-to-csharp-structterm-cons`) referenced in convspec §2 row 6. body_kernels.dart's C# render of this same cons-cell shape MUST agree on (a) the `.` functor literal preserved verbatim, (b) the `new List<Term> { head, tail }` growable-list shape passed to `StructTerm`, (c) the head+tail-VarRef positional ordering. Divergence here would silently break list-construction interop between modules.

- **lib/runtime/runtime.dart** — Co-dependent on the `GlpRuntime` surface accessed in `ActivateModule`'s seven steps. The C# render assumes `GlpRuntime` exposes: `Heap` (HeapFCP), `NextGoalId` (`{ get; set; }` int — required for `NextGoalId++`), `SetGoalEnv(int, CallEnv)` method, `SetGoalProgram(int, BytecodeProgram)` method, `Gq` (`Queue<GoalRef>` or equivalent with `.Enqueue(GoalRef)`), `InfrastructureGoalIds` (`HashSet<int>` with `.Add(int)`), `Runners` (`Dictionary<BytecodeProgram, BytecodeRunner>` with `ContainsKey` + indexer-set), `GlpChannels` (`Dictionary<string, GlpChannelHandle>` with indexer-set). Also depends on `CallEnv` having a `args:` named-arg-compatible constructor accepting `Dictionary<int, Term>`. runtime.dart's convspec MUST emit these member shapes for this file's C# to compile.

- **lib/runtime/system_predicates.dart** — Co-dependent indirectly via the spawned `serve/2` goal contract: `ActivateModule` enqueues a goal whose program is `serveBytecode` (the compiled `serve(Module, ChannelReader?)` runner). system_predicates.dart hosts the system-predicate surface that the spawned `serve/2` goal ultimately drains; no direct type or symbol used from it here. Coupling is contract-level only — agreement on the `serve/2` label name (preserved byte-identically as `"serve/2"` in §2 row 8 step 4) and on the two-argument shape (`{0: VarRef(moduleAddr), 1: VarRef(readerAddr)}` — module first, reader second). No decision in §2 directly imports or constructs a system_predicates type.
