# Reconciliation Memo — Seed #15: many-instances-shared-static-memory-cooperative-scheduling

**Feature ID:** `many-instances-shared-static-memory-cooperative-scheduling`
**Dossier kind:** EXPERIMENT/FOLLOW-UP
**Roadmap state:** captured (WSJF=1.38, RICE=450)
**Date:** 2026-06-09
**Branch:** `026-engine-review-dossier`

---

## Dossier cross-references

| §-anchor | What it says about this seed |
|---|---|
| §10.10 | "§7a two-tier shared/instance memory + cooperative run-to-completion — deferred to EXPERIMENT features (#15). Does not gate the MVP; must be on the roadmap." |
| §11 #15 | Kind `EXPERIMENT/FOLLOW-UP`; Scope: "Two-tier memory (instance vs shared-static), minimal footprint, safe preempt/resume, cooperative run-to-completion of reduction CHAINS returning control to the OS"; Why: "deep design unknown; informs C++ + persistence"; depends_on: #7, #14. |
| §6.2 | Persistence table — engine live state (Heap `Cells`+`Hp`, `Gq`, `Suspended`, per-goal tables) is PERSISTENT; `Scheduler`+`RunnerContext` is EPHEMERAL. This is the same seam as the two-tier memory split: persistent state is the per-instance dynamic tier, ephemeral execution scaffolding is rebuilt. |
| §6.3 (note) | "snapshot **at quiescence / between reductions only** … the consistency point is the quiescence boundary in `DrainAsyncWithStatus`" — exactly the safe-preempt boundary that cooperative run-to-completion exploits. |
| §0.4 | Classification table row for `Engine-state serialization / persistence` (net-new) and `Persistent-vs-ephemeral *definition/instance* seam` (reuse). The same classification applies to the two-tier memory split. |
| §12 risk 2 | Heap snapshot scale/cost at every quiescence — directly related to the footprint budget for many instances. |
| §8.2 | Slice B adds snapshot persistence to Slice A — the quiescence-boundary observation is shared with #15. |

Inverse traceability: dossier Appendix B row #15 → this memo.

---

## Seed-vs-dossier-vs-code

### Seed as stored (roadmap brief)

"Two-tier memory (instance vs shared-static construct-wrappers), minimal footprint, safe preempt/resume, cooperative run-to-completion of reduction CHAINS returning control to the OS. Deep design unknown; informs C++ + persistence."

### Dossier §11 #15 entry

Identical in substance. Depends on #7 (`engine-state-snapshot-and-persistence-api`) and #14 (`cpp-engine-feasibility`). No addition needed.

### Divergence from the dossier's §10.10 framing

§10.10 groups this with "§7a" from `feature-definition.md`. The feature-definition §7a is richer than the dossier §11 #15 one-liner; specifically it names:
- "several instances run in parallel under the OS, share the static memory"
- "safe preemption + resumption: explore how an instance can be safely preempted"
- "cooperative run-to-completion scheduling (option): each overall atomic reduction CHAIN runs, then returns control to the OS"

These sub-dimensions are NOT separately broken down in the dossier — they are folded into one seed. The reconciliation must surface this as a potential under-specification.

### As-built code checks

**What EXISTS today (directly relevant to this seed):**

- **Per-instance single-owner heap:** `heap_fcp.cs:133-141` — "NOT static — the runtime supports multiple concurrent heaps (one per agent/MadContext). Single-owning-context invariant … Every HeapFCP instance is accessed by exactly one execution context. Fields are plain (no lock, Interlocked, ConcurrentDictionary, or volatile)." This is the per-instance dynamic memory tier confirmed in code.

- **Tail-recursion budget as the proto-cooperative yield signal:** `runtime.cs:231-246` (`TailReduce`), `fairness.cs` (`NextTailBudget`/`ResetTailBudget`), `machine_state.cs:18` (`TailRecursionBudgetInit=26`). The existing scheduler already tracks a per-goal budget that returns `true` (yield) at zero. This IS a cooperative yield mechanism — but it is goal-granular (per tail step), not chain-granular (per complete reduction chain). `feature-definition.md:241-244` specifies chain-granular.

- **DrainAsyncWithStatus as the quiescence boundary:** `glp_engine.cs:545` — the drain loop runs until quiescence; the InboundPump driver (`:555-569`) re-enters the drain loop after servicing link frames. This boundary is exactly where cooperative run-to-completion would "return control to the OS": between drain invocations.

- **Runners map is per-GlpRuntimeEngine (not global/static):** `runtime.cs:46` — `Dictionary<object?, BytecodeRunner> Runners` is an instance field. `BytecodeRunner` instances are NOT shared across `GlpRuntimeEngine` instances today. This is a gap vs the "shared static construct-wrappers" design target — compiled programs/runners are per-instance today.

- **BodyKernels/SystemPredicates registries:** `runtime.cs:33-36` — `SystemPredicateRegistry`/`BodyKernelRegistry` are per-instance but recreated deterministically from static factories. This is the definition that is persistent, the object that is ephemeral — exactly the dossier §6.2 pattern. However, the dispatch tables themselves are instantiated fresh per engine (no sharing). The "shared static" tier would hoist these to a shared region.

- **No shared-static region, no instance-relative addressing, no pooling allocator:** confirmed zero by code inspection. The code is pure per-instance CLR heap allocation; there is no root-register pattern, no memory-mapped shared segment, no CoW pool.

- **No preempt/resume hook at the chain boundary:** `glp_engine.cs:555-569` has an InboundPump loop that re-drains, but there is no "OS preempt flag" checked between chains, no epoch counter, no external signal that stops the drain mid-run. The only stop-signal is `MaxCycles` (`:547`) which is a reduction-count cap, not a cooperative boundary.

**What is MISSING (confirmed zero):**

- Two-tier memory split (shared-static code region + per-instance dynamic heap): zero.
- Instance-relative addressing (V8-root-register style): zero. Heap addresses are plain `int` indices (`heap_fcp.cs:72`, `Pointer.TargetAddr`).
- Pooling allocator / CoW instantiation lifecycle: zero.
- Cooperative run-to-completion chain boundary (epoch-style preempt flag between chains): zero.
- Safe-preempt protocol at the chain boundary: zero.
- Any multi-instance orchestrator: zero.

**Key dossier claim confirmed:**

§11 #15 says "deep design unknown" — this is accurate. The seed represents a pure research/experiment scope with zero implementation substrate for the two-tier memory split or cooperative chain-level scheduling. The per-instance heap isolation exists but is not exploited.

---

## Classification check

**Kind:** EXPERIMENT/FOLLOW-UP — correct. The seed has no implementation today; its output is a design spike that:
- confirms (or refutes) the two-tier memory architecture for GLP instances,
- specifies the safe-preempt boundary at the chain level, and
- informs the C++ engine (#14) and the persistence API (#7).

**Scope vs code:** the dossier scope "Two-tier memory (instance vs shared-static construct-wrappers), minimal footprint, safe preempt/resume, cooperative run-to-completion of reduction chains returning control to the OS" is accurately scoped. Code confirms the gap: per-instance heap isolation exists (`heap_fcp.cs:133-141`), but shared-static construct wrappers, instance-relative addressing, and cooperative chain-boundary preemption are entirely absent.

**As-FOLLOW-UP:** the seed additionally appears in the §11 kind as EXPERIMENT/FOLLOW-UP. The FOLLOW-UP aspect — embedding the results into the engine once the experiment concludes — is not addressed by the dossier entry. The scope needs a sub-step: "if the experiment finds a viable design, produce a follow-up implementation spec." This is an underspecification.

**File:line anchors:**
- `out/csharp/lib/runtime/heap_fcp.cs:133-141` — per-instance heap confirmed
- `out/csharp/lib/runtime/runtime.cs:231-246` — existing tail-budget yield signal (goal-granular, not chain-granular)
- `out/csharp/lib/runtime/fairness.cs:8-13` — NextTailBudget/ResetTailBudget (the proto-cooperative yield hook)
- `out/csharp/lib/runtime/machine_state.cs:18` — TailRecursionBudgetInit=26
- `out/csharp/lib/engine/glp_engine.cs:545-569` — DrainAsyncWithStatus + InboundPump loop (the quiescence boundary)
- `out/csharp/lib/runtime/runtime.cs:46` — Runners map is per-instance (not shared-static today)

---

## Tensions

### T1 — Goal-granular yield vs chain-granular cooperative scheduling

**Summary:** The existing `TailReduce`/`Fairness` mechanism yields per tail-reduction-step (budget=26), not per complete atomic reduction chain. Feature-definition §7a specifies that cooperative run-to-completion means "each overall atomic reduction CHAIN runs, then returns control to the OS."

**Evidence:** `runtime.cs:231-246` (TailReduce yields at budget=0, per goal/step); `fairness.cs:8-13`; `machine_state.cs:18` (TailRecursionBudgetInit=26). Feature-definition `§7a:241-244`.

**Options:**
1. Re-define "cooperative run-to-completion" to mean the existing per-step budget mechanism — chain-level is over-specified.
2. Add a second, coarser cooperative hook at the `DrainAsyncWithStatus` level that returns control after each complete chain, distinct from the per-step tail budget.
3. Define "chain" precisely (a complete HEAD→GUARD→BODY cycle + all resulting reactivations until no new reactivations remain) and instrument the drain loop to detect and yield at that boundary.

### T2 — In-process isolates vs OS-process-per-instance

**Summary:** "Many instances running in parallel under the OS" (feature-definition §7a) could mean (a) one OS process with N in-process `GlpRuntimeEngine` objects, or (b) N OS processes each with one engine. The choice affects how "shared static memory" is achieved (CLR-level sharing vs OS `mmap`/CoW), whether §5 liveness supervision applies per-instance or per-OS-process, and how crash isolation works.

**Evidence:** `feature-definition.md:§7a:236-238`; dossier §0.4 (OS-liveness row — net-new); research-programme.md Axis 2 (recommends "lean toward many-processes for the §5 supervised-restart story" but notes in-process sharing is cheapest). Dossier §8.2 (Slice B pulls snapshot persistence forward — tightly coupled to this decision).

**Options:**
1. Many OS processes (CLR processes), each with one engine; shared static achieved by OS page-sharing of R2R/AOT `.text`. Matches §5 liveness-per-instance; heavier per-process overhead.
2. One OS process with N `GlpRuntimeEngine` objects; shared-static is in-process CLR static (single copy of compiled methods, shared BodyKernel/SystemPredicate dispatch tables). Cheaper; crash of one engine does not crash its OS process (unless uncaught CLR exception).
3. Hybrid: N OS processes, each holding M in-process engines.

### T3 — "Shared static construct-wrappers" is under-defined

**Summary:** The dossier and feature-definition both mention "shared-static wrappers for constructs" without specifying which C# objects qualify. Today `BodyKernelRegistry`, `SystemPredicateRegistry`, and `BytecodeRunner` instances are per-`GlpRuntimeEngine`. Sharing them would require making them immutable and safe for concurrent-read by N single-threaded engines.

**Evidence:** `runtime.cs:33-36` (per-instance registries); `runtime.cs:46` (per-instance Runners); heap_fcp.cs:133 (NOT static by design).

**Options:**
1. Hoist `BodyKernelRegistry`/`SystemPredicateRegistry` and all compiled `BytecodeRunner`s to a static/shared factory; each engine receives read-only references. Heap and goal-queue remain per-instance.
2. Keep everything per-instance; accept the duplication; "shared static" is aspirational only for C++.
3. Define sharing at the OS level (shared object / R2R AOT `.text`), not at the CLR object level — instances share the code JIT-compiled from the same methods, not the CLR objects wrapping them.

---

## Under-specifications

### U1 — "Reduction chain" boundary not formally defined

**Question:** What exactly constitutes one "atomic reduction CHAIN" in GLP's execution model?

**Why it matters:** The cooperative run-to-completion design turns on this definition. If "chain" = one three-phase HEAD/GUARD/BODY reduction for one goal (a single `BytecodeRunner.RunUntilSuspend` call), then the chain boundary is every `DrainAsyncWithStatus` inner loop iteration. If "chain" = all goals that run to completion or suspension before the queue is empty (i.e. the whole drain), the boundary is between successive `DrainAsyncWithStatus` calls. The two interpretations have radically different scheduling granularity and footprint trade-offs.

**Options:**
- Define "chain" as one goal's full HEAD→GUARD→BODY reduction step (finest granularity; cheapest per-chain; many yields per drain).
- Define "chain" as the maximal connected reactivation cascade (a writer binds → all suspended readers reactivate → they reduce → their writers bind → cascade terminates; coarser, semantically meaningful unit).
- Define "chain" as "one call to DrainAsyncWithStatus" (one drain epoch); coarsest; simplest cooperative boundary.

### U2 — What the FOLLOW-UP half of EXPERIMENT/FOLLOW-UP delivers

**Question:** If the experiment confirms a viable two-tier + cooperative-scheduling design, what artifact does the FOLLOW-UP deliver and to which feature does it feed?

**Why it matters:** Without a defined output gate, the EXPERIMENT half has no criterion for completion. The follow-up could feed #7 (persistence snapshot boundary), #14 (C++ engine architecture), or a new #17 (implement two-tier C++ engine). The dossier does not specify.

**Options:**
- EXPERIMENT delivers: (a) a formal definition of the two-tier memory boundary, (b) a safe-preempt protocol spec, (c) a cooperative-scheduling spec. FOLLOW-UP = a new spec that feeds into the C++ engine (#14) and/or a revised snapshot design for #7.
- EXPERIMENT delivers only a feasibility verdict + open questions; FOLLOW-UP = a separate specification feature to be created on go-decision.
- Fold FOLLOW-UP into #14 (C++ engine feasibility) rather than treating #15 as separate.

### U3 — Footprint target

**Question:** What is the minimal-footprint target — a concrete number of concurrent instances at what per-instance memory budget?

**Why it matters:** GEPA/DSPy refinement and formal metrics need a threshold. "A significant number of different engine instances" is not testable. The BEAM reference (327 words / ~2.6 KB per process, `research-programme.md:80`) suggests a target range but is not adopted as a formal budget.

**Options:**
- Adopt BEAM's 2.6 KB process footprint as an aspirational (not binding) target and define "significant" as ≥100 concurrent instances on a 1 GB embedded device.
- Define footprint in terms of shared-static overhead: the shared region is O(1) regardless of N, the per-instance overhead is bounded by a stated constant (e.g. ≤ 64 KB per instance including heap + goal queue + suspension tables at quiescence).
- Defer footprint target to the C++ feasibility spike (#14) where measurement is possible.

### U4 — Relationship to the existing tail-budget cooperative mechanism

**Question:** Is the existing `TailReduce`/`Fairness` mechanism (budget=26 per goal, `fairness.cs`) an implementation of "cooperative scheduling" that counts, or is it a separate mechanism that must be replaced/augmented?

**Why it matters:** If the existing tail-budget mechanism already provides the cooperative guarantee, the seed's scope is narrower (just the chain-boundary formalization + shared-static design). If it does not (because it is too fine-grained or does not return control to the OS), the seed must add a new mechanism.

**Options:**
- The tail-budget mechanism is a fairness knob for the goal-level FIFO scheduler, not a cooperative OS-yield; the seed must add an OS-yield at the DrainAsyncWithStatus boundary.
- The tail-budget mechanism is a proto-cooperative hook that can be extended to emit an OS-yield signal after N chains, making the chain-boundary check incremental.
- Replace the tail-budget mechanism entirely with a chain-boundary epoch signal (Wasmtime epoch model).

---

## GEPA/DSPy refinement

### Applicability

**methodological** — this is a systems design and scheduling experiment, not an LM/codegen program that GEPA/DSPy literally optimizes. GEPA/DSPy applies as the iterate-against-a-metric discipline: produce a design candidate → evaluate against the metrics combination → reflect on gaps (GEPA reflective mutation) → refine the candidate → repeat until thresholds hold. The design artifacts produced by this seed ARE the candidates being refined.

### Seed definition

"Design and validate a two-tier memory model (shared-static construct-wrappers + per-instance dynamic heap) and a cooperative run-to-completion scheduling protocol (chain-boundary preemption) for GLP engine instances, such that: (1) N instances can run concurrently on one machine with sub-linear per-instance memory growth; (2) an instance can be safely preempted at the chain boundary and resumed with no semantic difference; (3) the chain boundary is the shared safe-persist point; (4) Shapiro's committed-choice semantics (SRSW, three-valued unification, suspension/reactivation, monotone binding) are preserved across preempt/resume; (5) the design is compatible with the C++ engine (#14) and with the persistence API (#7)."

### Metrics combination

| Name | Kind | Tool / Harness | Threshold |
|---|---|---|---|
| Per-instance memory at quiescence | pragmatic | benchmark: N instances loaded with `self.glp`, measure CLR heap delta per instance | per-instance overhead ≤ 64 KB; shared region ≤ O(1) |
| Semantic equivalence across preempt/resume | pragmatic | REPL suite (`test/run_all_tests.sh`): run each test case with a forced preempt at the chain boundary; compare result vs uninterrupted result | 384/384 (all) pass |
| Chain-boundary quiescence (no in-flight HEAD-phase artifacts) | pragmatic | assertion harness: at the proposed yield point, assert `_TentativeStruct`/`_ClauseVar` count == 0 and `sigmaHat` is empty | 0 violations in full suite |
| SRSW preservation across preempt/resume | formal | in-repo SRSW type-checker (`test/run_all_tests.sh` section D: SRSW violations) applied to re-entered goals post-resume | 0 SRSW violations |
| Suspension reactivation correctness | formal | mechanized semantics: prove that the preempt/resume boundary preserves the suspension invariant (writer binds → all suspended readers reactivate, exactly once). Tool: Lean 4 via Lean-LSP-MCP. Property: ∀ goal g suspended on reader r, if writer of r is bound post-resume, g is reactivated with the same heap addr as pre-preempt. | proof closes with no `sorry` |
| Footprint sub-linearity | formal | Lean 4: prove that per-instance memory usage is bounded by a constant K independent of N (where the shared region contributes O(1) and each instance contributes O(K)). | symbolic bound proven |
| Cooperative boundary safety: no partial-phase state leaked | pragmatic | instrument `DrainAsyncWithStatus` inner loop; log all preemptions; check each preemption occurs only when `Pc` is at a clause entry point (kappa), not mid-HEAD or mid-GUARD | 0 mid-phase preemptions in suite |

### Interactive spec step

At the start of `/buildkit-specify` for this seed, owner confirms:
1. Granularity of "reduction chain": per-goal step, per-cascade, or per-drain-epoch.
2. In-process vs OS-process-per-instance (T2) — determines whether shared-static is CLR-static or OS mmap.
3. Footprint target (U3): adopt BEAM 2.6 KB / ≥100 instances as the threshold, or defer measurement to #14.
4. Which formal proof is in scope for this EXPERIMENT: just the suspension-reactivation correctness lemma, or also the footprint sub-linearity bound.
5. Whether the FOLLOW-UP half gets scoped NOW (new feature #17) or on go-decision after the experiment.
6. Confirmation that the formal metrics (SRSW + suspension reactivation) will be verified with Lean 4 via Lean-LSP-MCP (Claude-run, no API).

### Refinement loop

1. **Seed → candidate:** produce a written two-tier memory design document and cooperative-scheduling protocol spec (the experiment artifact).
2. **Evaluate:** run the metrics combination — pragmatic (benchmark, REPL suite with forced preempts, assertion harness) + formal (SRSW checker, Lean 4 mechanized suspension proof).
3. **GEPA reflective mutation:** identify which metrics fail; if the suspension invariant proof fails, refine the chain-boundary definition; if the footprint threshold fails, refine the shared-static classification; if mid-phase preemptions occur, refine the yield-point selection.
4. **DSPy compile-time optimization:** once the design is stable, compile the chain-boundary detection into a concrete set of bytecode-PC rules (which opcodes are safe yield points?) — this is a small optimization of the specification that DSPy can refine by generating and checking candidate rule sets against the assertion harness.
5. **Repeat** until all metric thresholds hold.
6. **Terminate** when: all pragmatic thresholds hold AND the Lean 4 proofs close without `sorry` AND the design document is accepted by the owner.

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** Strong for this seed's formal properties. The key mechanizable properties — suspension reactivation correctness (a monotone-binding + suspension-chain property), SRSW preservation under preempt/resume (a structural invariant on clause variables), and the footprint sub-linearity bound (a simple size bound on a data structure) — are all inductive properties over the heap + goal-queue + suspension-table structure. Lean 4's mathlib has the necessary combinatorial libraries (finsets, bounded maps). Lean-LSP-MCP enables the Claude-run tactic loop. APOLLO (model-agnostic) and Lean Copilot provide premise suggestion and sorry-repair — both compatible with the project's no-API/Claude-only rule.

**Rocq fit:** Also capable — the WAM verification literature (TWAM, verified Prolog→WAM compiler) and Vellvm use Coq/Rocq, so the idiom for "prove a logic machine's invariant" is well-established in Rocq. AutoRocq provides a tactic loop but requires adaptation off its GPT-4 API dependency. The footprint and suspension properties here are simpler than full compiler correctness, so Rocq is not needed to draw on its specialized verified-compiler corpus for this seed.

**Primary:** `lean4` — the suspension and SRSW invariants are inductive properties that Lean 4 handles well, and the Claude-run Lean-LSP-MCP loop is available without API adaptation.

**Alternative when:** If the proof requires reusing an existing Coq formalization of the WAM or FCP semantics from the TWAM/Vellvm line (where Rocq's corpus is richer), use Rocq. Specifically: if the formal semantics of the three-phase reduction step is formalized in Coq first (e.g. by extending TWAM), the Rocq formalization of suspension reactivation is the natural continuation.

### IL verification

**n/a** — this seed is a design/scheduling experiment, not an IL-codec or wire-contract seed. It does not define or validate a binary wire format. The bytecode ISA is treated as a given; the seed operates at the scheduling and memory-layout level, above the IL. IL verification applies to seeds #4, #5, #11.

---

## Shapiro criteria preserved

The following original GLP/Shapiro design criteria must be preserved by the two-tier memory split and the cooperative-preempt/resume protocol, framed for the embedded-switch purpose (connectivity-switch + OS actions + QHSM/HSM actors):

1. **Committed-choice concurrency:** after a clause commits (GUARD phase succeeds, BODY executes), the choice is irrevocable. A preemption at the chain boundary must not expose any partial-commit state to a sibling instance — the boundary must be AFTER the BODY phase completes and all writer binds are flushed to the heap. This is the "quiescent boundary" invariant the seed must guarantee. For the embedded switch: a QHSM state transition triggered by an OS action must commit atomically before any preemption.

2. **SRSW (Single-Reader/Single-Writer):** the two-tier design must not introduce aliasing between shared-static wrappers and per-instance heap variables. Shared static code/dispatch tables are read-only (no SRSW issue); the per-instance heap maintains SRSW. Any inter-instance communication (if cooperative instances share pointers into the shared segment) must route through a defined boundary that preserves the single-writer invariant per logical variable.

3. **Suspension correctness:** when a goal suspends on an unbound reader (mid-chain, waiting for a writer from another goal or a network event), the preempt/resume protocol must preserve the suspension record exactly — the `SuspensionRecord.ResumePC` (`suspension.cs:13`) and the `SuspensionListNode` chain (`heap_fcp.cs:688`) must be intact post-resume. This is the primary formal property (suspension reactivation correctness in the Lean 4 proof target).

4. **Monotone variable binding:** bindings are permanent; a committed writer bind is never undone. The snapshot-at-quiescence discipline (`heap_fcp.cs:148,154` atomically snapshotted) preserves this — there is no in-flight partial bind at the chain boundary. For the embedded switch: link variables (`link_send`/`link_recv` terms, `self.glp:536,548`) must be monotonically bound before the instance yields, so a sibling or OS-level listener sees a consistent view.

5. **Three-valued unification (Success|Suspend|Fail):** a preempted instance that was about to attempt unification in the HEAD phase must not re-execute the HEAD from a state where some writer binds from a partial σ̂w are visible. The safe-yield boundary must be BEFORE the next HEAD phase begins (i.e. at a `kappa` — clause entry point) so that re-entry re-executes the full HEAD from a clean state.

---

## Recommendation

The seed is **correctly classified and scoped** as EXPERIMENT/FOLLOW-UP. It is a genuine deep-design unknown with zero implementation substrate for two-tier memory or chain-level cooperative preemption. The research-programme.md (Axis 2 + Axis 3) provides strong prior-art anchoring (BEAM, Wasmtime, V8, KL1/KLIC, GraalVM Espresso, FCP sequential machine) that the experiment can draw on directly.

**Proceed**, but resolve U1 (chain boundary definition) and T2 (in-process vs OS-process) at the start of `/buildkit-specify` — these are the two decisions that determine almost everything else about the design. U3 (footprint target) should be set as a provisional threshold at spec time, revisited after the #14 C++ spike.

The FOLLOW-UP half of the seed must be given a concrete output gate: the experiment delivers a design document + formal suspension-invariant proof + pragmatic preempt/resume correctness evidence; the follow-up feeds a new implementation spec (to be seeded into the roadmap on go-decision, or folded into #14). Without this gate, the EXPERIMENT has no termination criterion.

---

## Options for owner

1. **Resolve T2 (in-process vs OS-process) before specifying:** choose the deployment model now; the two-tier memory design is fundamentally different under each choice. Consequence of deferring: the experiment designs for both, doubling its scope.

2. **Fold #15 into #14 (C++ feasibility spike):** since both are pure experiments with zero implementation substrate, and #15 depends on #14, run them as one joint spike. Consequence: simpler roadmap; risk of scope explosion.

3. **Split #15 into #15a (shared-static memory design) and #15b (cooperative chain-boundary scheduling):** these are independent enough to parallelize after #14. Consequence: each has a cleaner scope and metrics set; adds a roadmap entry.

---

## Open questions

- What is the formal definition of "one atomic reduction chain" in the GLP execution model (`DrainAsyncWithStatus` inner loop)? This must be settled before the cooperative-scheduling spec can be written.
- Should the existing `TailReduce`/`Fairness` budget mechanism (`fairness.cs`, `machine_state.cs:18`) be retained, extended, or replaced by an epoch-style chain-boundary signal? These mechanisms currently coexist without tension — the question is whether they compose correctly.
- For the embedded-switch purpose: do QHSM/HSM actor state transitions constitute "reduction chains" that must run to completion before the OS can preempt the engine? If so, the chain-boundary definition must be aware of actor boundaries, not just goal-queue drains.
- Is there a GLP-language way to express the cooperative yield (i.e. a `yield/0` primitive that is a safe preemption point), or must it be entirely in the C# host layer?
- The research-programme.md (Axis 2) recommends "lean toward many-processes for the §5 supervised-restart story" — does the owner agree? This is the T2 fork.

---

## External references

1. **BEAM Scheduler + per-process heap + shared literals** — "a process can only be suspended at certain points, such as at a receive or a function call"; per-process heap, shared literal pool, FCALLS budget. [The BEAM Book — Scheduling (theBeamBook)](https://github.com/happi/theBeamBook/blob/master/chapters/scheduling.asciidoc); [Erlang Efficiency Guide — Processes](https://www.erlang.org/doc/system/eff_guide_processes.html)

2. **Wasmtime pooling allocator + CoW fast instantiation** — pre-allocated pool; CoW-map bootstrap image; `madvise` reset; module-affine slot reuse. [Wasmtime Fast Instantiation](https://docs.wasmtime.dev/examples-fast-instantiation.html); [memfd/madvise CoW PR #3697](https://github.com/bytecodealliance/wasmtime/pull/3697)

3. **V8 embedded builtins / isolate-independent code + root register** — "from c*(1+n) to c*1"; builtins in read-only `.text` shared by OS across processes; root register for isolate-relative loads. [V8 Embedded Builtins](https://v8.dev/blog/embedded-builtins)

4. **GraalVM Espresso continuations + serialization** — safe-point suspend copies stack to heap objects; serialize/resume possibly in a different VM; suspension clears slots via liveness analysis. [Espresso Continuation API](https://www.graalvm.org/latest/reference-manual/espresso/continuations/); [Serialization of Continuations](https://www.graalvm.org/latest/reference-manual/espresso/continuations/serialization/)

5. **KL1/KLIC — portable concurrent logic on stock Unix** — KL1→C compiler; per-PE LIFO scheduling as a design knob; shared/hierarchical memory tension in the FGCS "flat global name space". [A portable and efficient implementation of KL1 (Springer)](https://link.springer.com/chapter/10.1007/3-540-58402-1_4)

6. **FCP sequential abstract machine (Houri & Shapiro, 1986)** — CARMEL-2 (29 instructions); FIFO resolvent; two-cell writer/reader vars; suspension lists; direct ancestor of GLP bytecode. [FCP References (nongnu.org/efcp)](http://www.nongnu.org/efcp/references)

7. *(In-repo)* `docs/research/repl-engine-separation/research-programme.md` — Axes 2 (shared/instance memory) + 3 (cooperative scheduling), with concrete borrow recommendations for this seed.

8. *(In-repo)* `docs/research/repl-engine-separation/llvm-feasibility.md` — §1.3 (MLIR as the stronger LLVM-family option for a GLP dialect); §2.4 (LLVM footprint cost against §7a); scouts the MLIR-based IL architecture that may host the shared-static code tier.
