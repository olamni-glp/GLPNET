# Reconciliation Memo — #2 result-envelope-and-deep-resolve

**Feature ID:** result-envelope-and-deep-resolve
**Dossier kind (§11 entry):** PREP
**Date:** 2026-06-09
**Branch:** 026-engine-review-dossier
**Status:** reconciliation complete; owner decision pending

---

## Dossier cross-references

| Anchor | Content mapped |
|---|---|
| §0.4 row "Server-side deep result resolve" | `reuse` — `glp_engine.cs:607-619` (`_ResolveDeepForTrace`) |
| §0.4 row "Result envelope codec (engine→client)" | `net-new` — the full envelope field set |
| §1.3 "the seam's biggest leak" | shallow `Dereference` values in `ExecutionResult.Bindings`; three dropped components |
| §2.3 "The result envelope (engine→client) is NET-NEW" | complete field-set specification for the envelope |
| §10.2 Output model fork | streaming vs terminal |
| §10.3 Encoding of unbound VarRef fork | display-only vs full round-trip |
| §10.4 var-name→writer identity scheme fork | `GlobalVarId` vs local heap int |
| §12 risk 4 | suspended results contain unbound vars that PayloadSerializer rejects |

---

## Seed-vs-dossier-vs-code

### Stored roadmap brief (buildkit-roadmap brief output)

The stored profile is thin: title "Self-contained result envelope + server-side deep-resolve"; kind PREP; effort M; notes reference `_ResolveDeepForTrace` and the three dropped components; problem/value/target-user fields are empty. WSJF=3.6, RICE=2250. The notes refer to "(§7 #2)" — a stale §-reference (it is §11 #2 in the current dossier, not §7). This divergence is cosmetic but confirms the seed was captured before the dossier received its final section numbering.

### Dossier §11 entry (the baseline)

Scope: "Promote var->writer map + suspended-goal detail + captured output into `ExecutionResult`; server-side deep-resolver (reuse `_ResolveDeepForTrace`)." §-refs: §1.3, §2.3. Depends on: #1.

### As-built code — confirming and extending

**`ExecutionResult` (glp_engine.cs:51-80):** exactly three fields: `Status`, `Bindings` (`IReadOnlyDictionary<string, RtTerm?>`), `Error`. The dossier's claim is confirmed.

**Bindings are shallow, not deep-resolved (glp_engine.cs:578, 733):** both `_RunSingleGoalAsync` and `_RunConjunctionAsync` populate bindings via `_runtime.Heap.Dereference(new RtVarRef(writerId))` — a SINGLE-LEVEL deref. `_ResolveDeepForTrace` (glp_engine.cs:607-619) is called only for `EquivTrace.Out` at lines 584 and 743, NOT for the returned `ExecutionResult.Bindings`. The REPL compensates by doing multi-level deref during display (glp_repl.cs:479,483,508,514,561 in `FormatTerm`). This gap is WIDER than the dossier states — even in-process use, the bindings values are a thin pointer into the heap, not a resolved value tree.

**`queryVarWriters` is a local variable (glp_engine.cs:515, 641):** built in `_RunSingleGoalAsync` and `_RunConjunctionAsync`, passed to `scheduler.SetQueryVarNames` for trace display (glp_engine.cs:539, 684), and used to populate `ExecutionResult.Bindings` — but the writer-id map is NOT a field of `ExecutionResult` and is not accessible to the caller. Confirmed dropped.

**`DrainResult` (scheduler.cs:58-91):** carries `SuspendedGoals` (`IReadOnlyList<string>`) and `BlockingReaders` (`IReadOnlySet<int>`), both populated by the scheduler. The engine's `_RunSingleGoalAsync` and `_RunConjunctionAsync` consume `DrainResult.Status` only and discard `SuspendedGoals`/`BlockingReaders`. Confirmed dropped.

**Output (body_kernels.cs:952-959, runtime.cs:135):** `OutputKernel` checks `rt.OutputCallback`; if set, calls it; otherwise `Console.WriteLine`. `OutputCallback` is declared as `Action<string>? OutputCallback { get; set; }` on `GlpRuntimeEngine` (runtime.cs:135). The engine itself DOES already have the hook — it is never set in the non-multiagent path and not wired to any capture buffer. No output field in `ExecutionResult`. Confirmed dropped.

**`TraceSink` (scheduler.cs:138):** `Action<string>? TraceSink` on `Scheduler`. Trace output goes there if set, otherwise to `Console.WriteLine` (scheduler.cs:394-401). Not wired to a capture buffer in the standard path. Part of the output-capture surface.

**`_ResolveDeepForTrace` (glp_engine.cs:607-619):** private method, recurses up to depth 32, handles `RtStructTerm` args by re-resolving through the heap. Suitable as the server-side deep-resolve basis for the envelope. The dossier's reuse claim is confirmed.

**One code finding the dossier missed:** `_ResolveDeepForTrace` has a `depth > 32` guard that silently returns the partially-resolved term at depth 32 without signalling truncation. For the wire envelope, callers need to know if a term was truncated. The depth limit is adequate for trace purposes but requires an explicit policy decision for the result codec: error on depth overflow, signal truncation in the envelope, or raise the limit.

**`CompilationResult.VariableMap` (compiler/result.cs:9):** `Dictionary<string, long>` (variable name → register index). This is the compiler-level map. At runtime, `queryVarWriters` is `Dictionary<string, int>` (variable name → heap writer address), built separately in `_SetupArgument`. These are distinct. The dossier's reference to `CompilationResult.VariableMap` (§2.2) is for the IL codec feature (#4), not this feature. For #2, the relevant artifact is `queryVarWriters`.

---

## Classification check

Dossier kind: **PREP** — correct. This is a prerequisite refactor/promotion, not a shipped user feature. No wire codec is built here (that is #5). The scope "promote dropped components + server-side deep-resolver" is exactly a preparatory structural change to `ExecutionResult` and the engine's result-collection logic. The classification is accurate.

Dossier scope support in code:
- "Promote var→writer map into ExecutionResult" — supported: `queryVarWriters` local at glp_engine.cs:515,641 is NOT in `ExecutionResult` (glp_engine.cs:51-80). Scope is accurate.
- "Promote suspended-goal detail" — supported: `DrainResult.SuspendedGoals`/`BlockingReaders` at scheduler.cs:67,73 are NOT propagated to `ExecutionResult`. Scope is accurate.
- "Promote captured output" — supported: `OutputCallback` at runtime.cs:135 exists but is not wired to an output buffer in `ExecutionResult`. Scope is accurate.
- "Server-side deep-resolver (reuse `_ResolveDeepForTrace`)" — confirmed at glp_engine.cs:607-619, private, currently trace-only. Extension to production bindings is feasible.

---

## Tensions

### T1: `_ResolveDeepForTrace` depth-32 truncation silences failures

**Evidence:** glp_engine.cs:609 `if (term == null || depth > 32) return term;` — returns partially-resolved term at depth 32 with no signal. For EquivTrace this is benign. For a self-contained wire result it is a silent corruption.

**Options:**
1. Raise the limit to a larger bound (e.g. 256) and accept that pathologically deep terms are still silently truncated — adequate for the MVP where ground terms dominate.
2. Return a `(RtTerm?, bool truncated)` pair from the deep-resolve and include a `truncation_flag` in the envelope field for each binding — fully correct, slightly more complex codec.
3. Throw an exception on depth overflow — forces the caller to handle it explicitly but breaks the "self-contained result" promise if the caller propagates it as an error.

### T2: Scope boundary with #3 (structured-output-capture-seam)

**Evidence:** dossier §11 shows #2 and #3 as separate seeds both depending on #1. #2 promotes captured output into `ExecutionResult`. #3 routes `Console.WriteLine`/trace through `OutputCallback`/`TraceSink`. But #2 cannot promote a captured-output field into `ExecutionResult` unless #3 has first wired `OutputCallback`/`TraceSink` to a capture buffer — otherwise the field is always empty. The dossier lists them as parallel (#2 and #3 both depend-on #1, not on each other), but #2 is functionally blocked by #3 for the output field.

**Options:**
1. Make #2 depend-on #3 (add a dep edge) — reflects the real dependency; delays #2.
2. Collapse the output-routing work into #2 — simpler dep graph; may be the right call given the output field is small.
3. Exclude the output field from #2's promoted `ExecutionResult` and leave it as a separate field added by #5 (the codec feature) after #3 is done — defers the output-field promotion, keeps #2 and #3 independent.

### T3: Bindings are shallow deref, not deep-resolved

**Evidence:** glp_engine.cs:578/733: `_runtime.Heap.Dereference(new RtVarRef(writerId))` is single-level. The dossier states "server-side deep-resolver" is the goal (§1.3, §2.3). But promoting `_ResolveDeepForTrace` to produce the actual binding values returned in `ExecutionResult` changes the semantics of the existing REPL interaction path (the REPL currently does its own multi-level deref in `FormatTerm`). If #2 makes `ExecutionResult.Bindings` contain DEEP-resolved values, the REPL's `FormatTerm` heap-deref becomes a double-deref (it already has deep resolution logic).

**Options:**
1. Add a parallel deep-resolved binding dict to `ExecutionResult` (e.g. `ResolvedBindings`) while keeping the existing shallow `Bindings` unchanged — backward-compatible; both are available; the wire codec uses `ResolvedBindings`.
2. Replace `ExecutionResult.Bindings` values with deep-resolved terms — simpler interface; requires auditing all callers (REPL, tests, multiagent) that do their own deref and now double-deref (benign for ground terms, incorrect for suspension paths).
3. Make deep-resolve a separate method on `ExecutionResult` or `GlpEngine` (e.g. `engine.DeepResolve(result)`) rather than storing in the result — caller controls when to pay the resolution cost.

---

## Under-specifications

### U1: Identity scheme for the promoted var→writer map

**Why it matters:** the wire envelope field "var-name→writer-id" is only meaningful if the writer-id is stable across the process boundary. A heap address (`queryVarWriters` values are ints) is meaningless outside the process. The dossier defers this to §10.4 (GlobalVarId vs local int) but #2 must choose a representation for the promoted field.

**Options:**
1. Store the raw heap int in the promoted `ExecutionResult` field now (#2), with the GlobalVarId mapping deferred to #5 (the codec). The field is internal-use-only until the codec is built.
2. Promote using `GlobalVarId(agentId:localId)` scheme from `PayloadSerializer` (payload_serializer.cs:85-88) immediately — ensures the field is already wire-ready when #5 lands.
3. Skip the var-map field in `ExecutionResult` entirely and add it only in the wire-codec (#5) — keeps #2 strictly about deep-resolve and dropped components without the identity-scheme decision.

### U2: Unbound variable representation in the promoted bindings

**Why it matters:** `ExecutionResult.Bindings` currently holds `null` for unbound variables (glp_engine.cs:580). A deep-resolved envelope for a Suspended result may contain terms with unbound sub-variables that are not top-level nulls. The unbound-VarRef encoding decision (§10.3) gates whether the promoted `ExecutionResult` can carry a fully self-contained Suspended result.

**Options:**
1. Keep the convention that `null` = "top-level unbound" and add a separate field for partial-suspension detail (SuspendedGoals + BlockingReaders) — adequate for display, not for remote resume.
2. Define an explicit sentinel term (`UnboundVarTerm(heapAddr)`) in the `RtTerm` hierarchy and use it in deep-resolve — makes the result structurally self-contained without null, but modifies the runtime term hierarchy.
3. Keep `null` for MVP; document the gap; defer full unbound encoding to #5.

### U3: `_ResolveDeepForTrace` access modifier

**Why it matters:** `_ResolveDeepForTrace` is `private` (glp_engine.cs:607). Promoting it to production use for result bindings requires changing the access modifier or extracting it to a helper. The dossier says "reuse" but does not specify whether this means in-place promotion, extraction, or a new method.

**Options:**
1. Make it `internal` or `protected` so the engine + tests can use it, keeping it engine-side.
2. Extract it as a static helper on `HeapFCP` or a new `HeapResolver` utility (since it operates entirely on `Heap.Dereference`) — cleaner architecture, reusable by tests and the future codec.
3. Duplicate the logic in a new `_BuildDeepResolvedBindings` method that also handles the Suspended case and the depth-truncation signal — avoids touching the trace-only method.

---

## GEPA/DSPy refinement

### Applicability

**methodological** — this seed produces C# structural changes (new fields on `ExecutionResult`, promotion of dropped components, extraction of a deep-resolve helper). There is no LM program or codegen artifact that GEPA/DSPy directly optimizes. GEPA/DSPy applies as the iterate-against-a-metric discipline: each candidate implementation is evaluated against the metric combination, and Claude-driven refinement iterates on the C# design until thresholds hold.

### Seed definition

Given the in-process GLP engine (`GlpEngine`), promote three dropped result components (var-name→writer-id map, suspended-goal detail `SuspendedGoals`+`BlockingReaders`, captured/streamed output) into `ExecutionResult` and produce a server-side deep-resolver that makes every binding value fully resolved (no live heap pointer in the result). The deep-resolve basis is `_ResolveDeepForTrace` (glp_engine.cs:607-619). The resulting `ExecutionResult` must be self-contained: a caller holding the result object can read all bindings, inspect suspension cause, and retrieve output WITHOUT accessing `engine.Runtime.Heap` or any engine-internal state.

### Metrics combination table

| Name | Kind | Tool/Harness | Threshold |
|---|---|---|---|
| REPL test suite green | pragmatic | `bash test/run_all_tests.sh` (DART=... env) | 384/384 pass; no regressions |
| Self-containment: REPL display uses ONLY ExecutionResult fields | pragmatic | Code review / grep: no `engine.Runtime.Heap` call in `glp_repl.cs` binding-display path after the change | Zero `engine.Runtime.Heap` dereference calls in FormatTerm / PrintStatus |
| Round-trip identity: deep-resolved binding ≡ REPL-displayed value | pragmatic | Diff test: run N goals in-process; compare REPL-displayed output before vs. after; must be identical | 100% identical for the corpus (programs/tests/ suite goals) |
| Suspension detail completeness | pragmatic | Test: run a suspending goal; assert `ExecutionResult` carries non-empty `SuspendedGoals` and non-empty `BlockingReaders` | All test-suite suspending goals produce populated fields |
| Output capture completeness | pragmatic | Test: run a goal with `_output/1`; assert the output field in `ExecutionResult` is non-empty and equals the Console output from the pre-change baseline | 100% output captured; no Console.WriteLine fallback in non-test paths |
| SRSW preservation | formal | Type-checker + SRSW validator (`well_typed_clause.cs`; re-run on modified engine files) | 0 new SRSW violations introduced |
| Depth-truncation signal correctness | formal | Lean 4 proposition: for all terms T with depth ≤ 32, `deepResolve(T)` returns a fully-ground resolved term (or a term with only unbound-var leaves), with `truncated = false` | Proof over the finite depth bound using induction on the term structure |

### Interactive spec step

At the start of `/buildkit-specify`, the owner confirms:
1. Which T3 option for bindings representation (parallel field vs. replace vs. lazy method).
2. Whether the output field is in scope for #2 or deferred to #3/#5 (resolves T2).
3. The depth-truncation policy (T1 option).
4. Whether to prove the depth-truncation proposition in Lean 4 at this stage or defer to #5.
5. The identity scheme for the var-map field (U1 option).

### Refinement loop

Seed definition → candidate C# diff (new `ExecutionResult` fields; extracted `BuildDeepResolvedBindings`; `OutputCallback` wired to a capture buffer; `DrainResult` fields promoted) → evaluate: run REPL suite, run self-containment grep, run round-trip diff test → GEPA reflective mutation: if REPL suite regresses, locate the double-deref path in FormatTerm and remove client-side deref; if round-trip fails, check depth-32 truncation; if suspension fields empty, verify DrainResult propagation → repeat until all pragmatic thresholds hold and the Lean 4 depth-bound proposition is stated (proof in #5). No external API; all LM work via Claude Agent-tool seams.

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** good fit for the one mechanized property this seed needs — the depth-truncation correctness proposition (for all terms with depth ≤ 32, `deepResolve` is complete). Lean 4's dependent types and the mathlib term-induction library make structural induction over a depth-bounded recursive function straightforward. Lean-LSP-MCP (Claude-native) and Lean Copilot make the tactic loop Claude-driven without an external API. The property is decidable and finite-bounded, so proof automation (omega, simp) will handle most sub-goals.

**Rocq fit:** equally capable for this specific property; the Coq `Fixpoint` with a fuel/depth argument is a standard pattern (`Equations` or `Program Fixpoint`). AutoRocq provides an autonomous tactic loop but requires adapting away its GPT-4 dependency. No decisive advantage over Lean 4 for this seed.

**Primary:** `lean4` — preferred by the owner as the primary across the board (SEED-RECONCILIATION-BRIEF §3.2a), equally capable here, and the Lean-LSP-MCP / Lean Copilot integration makes the Claude-driven tactic loop more immediately available.

**Alternative when:** if the Lean 4 proof gets stuck on a specific sub-goal related to the heap-address arithmetic (the `Pointer.TargetAddr` int-indexing into `Cells`) and Rocq's `omega`/`lia` tactic set handles integer-arithmetic sub-goals more smoothly — use Rocq as a fallback for that sub-goal only.

### IL verification

**n/a** — this seed does not touch the wire format, bytecode, or IL codec. It promotes in-memory C# objects. No byte-contract, wire-parity, or MLIR-dialect verification applies. The result of this seed is an enriched in-memory `ExecutionResult`; IL/wire verification enters in #5 (result-codec-and-framecodec-ride), which depends on #2.

---

## Shapiro criteria preserved

This step must preserve the following original GLP/Shapiro design criteria, framed for the embedded-switch purpose:

1. **SRSW (single-reader / single-writer) correctness:** the deep-resolve step reads binding values off the heap but does NOT bind any variable. `_ResolveDeepForTrace` is read-only on the heap (calls only `Heap.Dereference`). The promoted method must maintain this read-only invariant — no side-effect on the heap, no variable binding, no suspension-list mutation.

2. **Suspension correctness (monotone binding):** the promoted `SuspendedGoals`/`BlockingReaders` fields capture a snapshot of suspension state at quiescence. They must be a faithful read of `DrainResult` — not filtered, not mutated. The engine must NOT re-activate or resolve suspended goals as a side-effect of building the result envelope.

3. **Committed-choice concurrency:** the result is collected AFTER the drain reaches quiescence (`DrainAsyncWithStatus` returns). Deep-resolve happens post-drain, not mid-reduction. This preserves the committed-choice property — no choice-point is re-opened by the result-collection step.

4. **Three-valued unification preservation:** the `Status` field (Succeeded/Failed/Suspended) must reflect the actual post-drain status faithfully. The deep-resolve step must not alter `Status`. An unbound variable in a Suspended result must remain unbound in the envelope (null / sentinel, NOT forced to a default value).

5. **Embedded-switch correctness:** as a SWITCH for external connectivity and internal OS/actor actions, the engine's result must be self-contained so the SWITCH layer (the host) can route results without reaching into engine-internal heap state. This seed delivers that property — a host process or a QHSM/HSM actor reading the result does not need a heap reference.

---

## Recommendation

Proceed. The dossier's scope and classification are accurate. The code confirms all three dropped components and the shallow-deref gap. The additional code finding (depth-32 truncation without signal) is a real issue but is resolvable with one of the T1 options before or during implementation. The seed is straightforward (a C# structural change with one reuse of an existing private method), appropriately sized for an M effort, and is a genuine prerequisite for #5 (result-codec-and-framecodec-ride).

**Key owner decision before /buildkit-specify:** resolve T2 (scope boundary with #3 — add a dep edge, or collapse output into #2, or exclude output from #2). The other tensions and under-specifications are best settled at the spec step. If T2 is resolved as "add dep #2→#3", update the dossier §11 #2 depends_on field.

---

## Options for owner

1. **Add dep #2→#3 (T2 Opt 1):** reflect the real sequential dependency; #3 must wire output capture before #2 can promote the output field. Consequence: #2 is delayed until #3 ships; cleanest dependency graph.
2. **Collapse output routing into #2 (T2 Opt 2):** do the `OutputCallback` capture-buffer wiring as part of #2; eliminates the gap between the seeds. Consequence: #2 scope grows slightly; #3 becomes smaller or is merged with #2.
3. **Exclude output field from #2 (T2 Opt 3):** #2 promotes only var-map + suspended detail; output field is added by #5 after #3. Consequence: #2 ships faster; output gap persists between #2 and #5; the "self-contained result" guarantee is partial until #5.
4. **Deep-resolve option for bindings (T3):** choose from parallel-field (Opt 1), replace (Opt 2), or lazy method (Opt 3). Advisory: Opt 1 (parallel `ResolvedBindings`) is safest — no regression risk on existing callers.

---

## Open questions

1. Is depth-32 the right truncation bound for production bindings, or should it be configurable / unlimited for ground terms? (T1 — affects the Lean 4 proof scope.)
2. Should #2 and #3 be merged into a single feature, given that #2 functionally depends on #3 for the output field? (T2 resolution.)
3. What is the access-modifier policy for the extracted deep-resolve helper — engine-internal `internal`, extracted to `HeapFCP`, or a standalone `HeapResolver` class? (U3.)
4. Does the Dart mirror (`glp_runtime/lib/engine/glp_engine.dart:34-37`) need to be updated in lockstep, or is the C# `ExecutionResult` enrichment the sole target? (Cross-runtime parity per §2.5 / §12 risk 7 — affects whether #2 needs a Dart counterpart.)

---

## External refs

- `out/csharp/lib/engine/glp_engine.cs:51-80` — `ExecutionResult` definition
- `out/csharp/lib/engine/glp_engine.cs:515-586` — `_RunSingleGoalAsync` binding collection + `_ResolveDeepForTrace` call
- `out/csharp/lib/engine/glp_engine.cs:607-619` — `_ResolveDeepForTrace` private method
- `out/csharp/lib/engine/glp_engine.cs:641-745` — `_RunConjunctionAsync` binding collection
- `out/csharp/lib/runtime/scheduler.cs:58-91` — `DrainResult` with `SuspendedGoals`/`BlockingReaders`
- `out/csharp/lib/runtime/scheduler.cs:138` — `TraceSink`
- `out/csharp/lib/runtime/runtime.cs:135` — `OutputCallback`
- `out/csharp/lib/runtime/body_kernels.cs:940-963` — `OutputKernel` using `OutputCallback`
- `out/csharp/lib/multiagent/payload_serializer.cs:85-88,511,447` — tag scheme, unbound-VarRef throw, NotSupported
- `out/csharp/lib/compiler/result.cs:9` — `CompilationResult.VariableMap`
- `out/csharp/bin/glp_repl.cs:379-388,432-584` — `PrintStatus`, `FormatTerm` (client-side deref)
- `glp_runtime/lib/engine/glp_engine.dart:34-37` — Dart `ExecutionResult` mirror
- Design dossier: `docs/research/repl-engine-separation/design-dossier.md` §0.4, §1.3, §2.3, §10.2-10.4
- Methodology: `docs/research/repl-engine-separation/reconciliation/SEED-RECONCILIATION-BRIEF.md` §3, §3.2a, §3.5
