# Reconciliation Memo — #7 engine-state-snapshot-and-persistence-api

**Date:** 2026-06-09
**Feature ID:** `engine-state-snapshot-and-persistence-api`
**Dossier kind (§11):** PREP/MVP
**Stored WSJF:** 2.25 · **Stored RICE:** 1800
**Depends on:** dossier #1 (this dossier), dossier #6 (`repl-engine-process-split-mvp`)

---

## Dossier cross-references

| §-anchor | Subject |
|---|---|
| §6.1 | Where full engine state lives — none serializable today |
| §6.2 | Persistent-vs-ephemeral classification table |
| §6.3 | DB + API shape (MarathonStore template) |
| §6.4 | Bootstrap + restore-and-resume |
| §0.4 | Classification table row: "Engine-state serialization / persistence" → `net-new` |
| §8.2 | Slice B (MVP with persistence) vs §8.1 Slice A (MVP without) |
| §9.2 | Premise reconciliation: runtime IL = heap data, not synthesized at runtime |
| §10.5 | DB choice fork (PGLite-primary vs JSON-only) |
| §10.6 | Snapshot granularity fork (full per-quiescence vs definition-log+checkpoint) |
| §10.7 | Resume-driver placement (FR-057) |
| §10.8 | Store as code authority (persist IL vs recompile) |
| §12 risk 2 | Heap snapshot scale/cost |
| §12 risk 3 | Ephemeral OS file/FFI handles — no definition, no re-establish path |
| §12 risk 5 | Heap-address stability across resume |

Appendix B maps this seed to `reconciliation/7-engine-state-snapshot-and-persistence-api.md`.

---

## Seed-vs-dossier-vs-code

### Roadmap brief (verbatim notes field)
> "PREP/MVP. Heap+Gq+Suspended+per-goal-tables+NextGoalId+loaded-IL snapshot at quiescence behind a MarathonStore-shaped API (PGLite-primary + JSON-fallback, monotonic seq). The persistence requirement. depends-on: #1,#6. (§7 #7)"

The stored brief is a faithful one-line distillation of §6.2–§6.3. No scope drift vs the dossier.
The `Problem/need`, `Target user`, `Value/outcome`, and `Risk` fields are all blank in the roadmap
(confirmed by `buildkit-roadmap brief` output) — they need to be filled at `/buildkit-specify` time.

### Code verification

**`GlpRuntimeEngine` (`out/csharp/lib/runtime/runtime.cs`)**

The dossier's §6.1 and §6.2 inventory is accurate. Code-verified:

| State field | `file:line` | Classification |
|---|---|---|
| `Heap.Cells` (`List<HeapCell>`) + `Heap.Hp` | `heap_fcp.cs:148,154` | PERSISTENT |
| `WriterContent.Suspensions` (on-heap suspension chains) | `heap_fcp.cs:103` | PERSISTENT (addr-coupled) |
| `Gq` `GoalQueue` | `runtime.cs:30`; `machine_state.cs:95` | PERSISTENT |
| `Suspended` (`Dictionary<int, HashSet<GoalRef>>`) | `runtime.cs:104` | PERSISTENT (int keys only) |
| `_budgets`, `_goalEnvs`, `_goalPrograms`, `_goalModuleContexts` | `runtime.cs:57-60` | PERSISTENT |
| `NextGoalId` | `runtime.cs:78` | PERSISTENT |
| `_loadedPrograms`, `_loadedModules`, `_serveBytecode` | `glp_engine.cs:150-154` | PERSISTENT (owner's code) |
| `ModuleTerm`-embedded `BytecodeProgram` on heap | `terms.cs:146-149`; `glp_activation.cs:88` | PERSISTENT (in heap) |
| `SystemPredicates`/`BodyKernels` registries | `runtime.cs:33-36` | EPHEMERAL (re-register at boot) |
| `Scheduler` + `RunnerContext` | `scheduler.cs:107`; `runner.cs:41` | EPHEMERAL (rebuilt per drain) |
| `_fileHandles` / `_libraries` | `runtime.cs:64,69` | EPHEMERAL / OPEN PROBLEM |
| `_bindCallbacks` (`Dictionary<int, Action<Term>>`) | `heap_fcp.cs:157` | EPHEMERAL (C# delegates) |

**Additional state the dossier does not explicitly list (MISSED):**

1. `_waitReaders` (`Dictionary<int, int>` at `runtime.cs:96`) — maps `goalId → readerId` for `wait()`
   guard timer state. This is PERSISTENT: a resumed goal with an armed wait() timer must know which
   readerId will be written when the timer fires. Without it, timers are silently lost on resume.
   `_pendingTimers` (int counter at `runtime.cs:82`) tracks the count; it follows from `_waitReaders`.

2. `Runners` (`Dictionary<object?, BytecodeRunner>` at `runtime.cs:46`) — populated by `_activate`
   for each active module. PERSISTENT in the sense that resumed goals need their runner re-registered.
   However, `BytecodeRunner` itself is stateless (its only init-time data is the `BytecodeProgram` it
   wraps, `runner.cs:41-53`); so restoring `Runners` reduces to re-registering each program's runner.
   This is a consequence of the IL persistence decision (§10.8), not a separate blob.

3. `InfrastructureGoalIds` (`HashSet<int>` at `runtime.cs:112`) — the set of serve/2 infrastructure
   goal IDs. PERSISTENT: classification of goals as infrastructure vs user-goals must survive resume
   so the `DrainResult` status classifier (`scheduler.cs`) behaves identically.

4. `GoalState.SigmaHat` (the per-goal tentative writer substitution, `machine_state.cs:59`) — this
   is part of a running reduction step and is EPHEMERAL at quiescence. At quiescence all pending
   head-phase substitutions are either committed or abandoned; no in-flight `SigmaHat` survives the
   drain cycle. Confirmed by the dossier's classification of `Scheduler`/`RunnerContext` as
   EPHEMERAL (rebuilt per drain-step). No action needed.

5. `GlpChannels` (`Dictionary<string, GlpChannelHandle>` at `runtime.cs:53`) — populated by
   `ActivateModule`. Each handle wraps a writer/reader heap-addr pair. PERSISTENT in the definition
   sense (the channel's heap addresses are in the snapshot); EPHEMERAL as a named registry entry
   because re-activation on resume re-registers via `ActivateModule`. Depends on whether IL is
   persisted (§10.8) or recompiled; either path re-creates the channel handles if module activation
   is replayed. The dossier mentions `GlpChannels` only in §1.2 (not in §6.1/§6.2). Worth calling
   out explicitly at `/buildkit-specify` time.

**`GlpEngine._goalId` (`glp_engine.cs:156`)**

The dossier lists `NextGoalId` (on `GlpRuntimeEngine`) but not `GlpEngine._goalId`. The engine
increments `_goalId` for each `RunGoalAsync` call (`glp_engine.cs:543`) independently of
`rt.NextGoalId++` (used by `_activate`). Both must be snapshotted to avoid goal-ID collisions
after resume; they serve different domains (query-entry goals vs spawned activation goals).

**MarathonStore substrate (`codeconv/src/codeconv/marathon/store.py:96`)**

Code-verified: `MarathonStore` uses PGLite-primary + JSON-fallback with strict-monotonic
`sequence_no`. The `active_store()` degrades to `"fallback"` on bridge unreachability
(`store.py:139`). The C# persistence API must mirror this shape but is implemented in C#, not
Python. The in-repo precedent is the Python store; the new implementation is a distinct
C# artifact calling PGLite via the bridge or a JSON file path.

**Quiescence point (`glp_engine.cs:545`)**

Dossier §6.3 cites `DrainAsyncWithStatus` at `glp_engine.cs:545` as the consistency point.
Code-verified: `scheduler.DrainAsyncWithStatus(...)` returns at `:545`; the inbound-pump loop
then runs additional drains (`:555-569`), extending quiescence to the pump's idle point. The
snapshot must be taken AFTER the full pump-drain cycle, not just after the first `DrainAsyncWithStatus`.
The dossier says "at quiescence / between reductions only" — this is correct but the pump-extended
quiescence is the precise boundary.

---

## Classification check

**Dossier kind: PREP/MVP** — Does the as-built code support this?

The dossier classifies this entry as `PREP/MVP` (§11 #7) and the §0.4 row classifies
"Engine-state serialization / persistence" as `net-new`. Both are correct:

- Zero snapshot/persist path exists anywhere in `out/csharp` (confirmed: grep for
  `Serialize|Snapshot|Persist|SaveState|LoadState` in `out/csharp/lib/engine/` returns 0 matches).
- The substrate (`MarathonStore` shape) exists in Python (`codeconv/src/codeconv/marathon/store.py`);
  the C# engine persistence API is entirely net-new.
- `PREP/MVP` is accurate: it is PREP in that it must be built before #8 (liveness) and #9
  (restore-and-resume) but is also an MVP milestone capability per §8.2 (Slice B).

**Classification is correct.** The `net-new` tag at §0.4 is accurate; PREP/MVP is accurate.

---

## Tensions

### T1 — Scope bloat: _waitReaders, GlpEngine._goalId, InfrastructureGoalIds not in the dossier scope line

**Evidence:** The dossier's one-line scope ("Heap+Gq+Suspended+per-goal-tables+NextGoalId+loaded-IL")
omits `_waitReaders` (`runtime.cs:96`), `_pendingTimers` (`runtime.cs:82`), `GlpEngine._goalId`
(`glp_engine.cs:156`), and `InfrastructureGoalIds` (`runtime.cs:112`). All four are needed for a
correct round-trip resume: timers silently vanish, goal IDs collide, and infrastructure goal
classification is lost. Treating the scope line as exhaustive leads to a subtly broken implementation.

**Options:**
1. Expand the scope line explicitly at `/buildkit-specify` to include these four fields.
2. Keep the scope line as a high-level description and capture the full field inventory
   in the spec's persistence blob definition (the implementation spec owns the exhaustive list).
3. Accept the scope line as approximate and rely on the implementation to discover the gaps
   (higher risk of missed fields, especially under incremental development).

*Recommendation:* Option 1 — explicit expansion at `/buildkit-specify`; the scope line
is the seed's contract and should be accurate.

### T2 — DB implementation language mismatch: MarathonStore is Python; engine is C#

**Evidence:** The dossier says "mirror the codeconv MarathonStore" (`store.py:96`). The engine
host is C# (`out/csharp`). A C# persistence API that "mirrors" a Python store either:
(a) calls the Python store via the codeconv bridge (process boundary, PGLite-backed),
(b) reimplements the dual-store logic in C# using the same PGLite endpoint, or
(c) calls PGLite directly from C# via an ADO.NET/Npgsql driver.

The dossier does not specify which; it only says the shape is the same.

**Options:**
1. C# calls the PGLite cluster via Npgsql/ADO.NET directly, with a JSON-fallback path — pure C#,
   no Python dependency.
2. C# hosts a thin Python subprocess calling the MarathonStore Python class — reuses logic
   but adds a Python runtime dependency in the engine host.
3. Define the persistence schema as a new PGLite schema (e.g. `glpengine`) analogous to
   `marathon`, and share the same PGLite cluster (`C:/pglite/research/glpnet`).

*Recommendation:* Option 1 + 3 — a C# dual-store (PGLite-via-Npgsql primary; JSON fallback)
with a dedicated `glpengine` schema in the existing cluster. Avoids Python runtime dependency in
the engine host; keeps the dual-store contract.

### T3 — Coupling to IL codec (#4): ModuleTerm-embedded BytecodeProgram in the heap snapshot

**Evidence:** §9.2 establishes that compiled programs circulate as runtime heap data
(`ModuleTerm` at `terms.cs:146`; `glp_activation.cs:88`). The heap snapshot therefore contains
embedded `BytecodeProgram` objects. A heap serializer must either:
(a) serialize the embedded `BytecodeProgram` (requires the IL codec from feature #4), or
(b) substitute a reference/label for each `ModuleTerm` and rely on re-registration from the
    `_loadedPrograms` table (decouples #7 from #4 but loses any dynamically-created modules
    not in `_loadedPrograms`).

The dossier acknowledges this coupling at §2.4 and §9.2 but does not resolve it for #7.

**Options:**
1. #7 depends on #4: the heap serializer uses the IL codec to embed `BytecodeProgram` bytes
   in the snapshot blob. Correct and complete; raises the IL codec to a hard dependency of #7.
2. #7 uses a label-reference scheme: `ModuleTerm` cells store a program key; on restore the
   key is looked up in `_loadedPrograms` (or recompiled from source). Decouples #7 from #4;
   acceptable only if all modules in the heap are already in `_loadedPrograms`.
3. #7 defers heap-embedded-IL serialization: snapshot fails/warns on any `ModuleTerm` cell,
   forcing the owner to recompile all modules before snapshotting. Pragmatic MVP bound.

*Recommendation:* Option 2 for the MVP (label-reference; all activated modules are registered
in `_loadedPrograms` by `ActivateModule`), Option 1 as a follow-up when #4 ships. The spec
must document which modules are guaranteed to be in `_loadedPrograms` at quiescence.

### T4 — Quiescence definition: single-drain vs pump-extended quiescence

**Evidence:** `glp_engine.cs:545` is the `DrainAsyncWithStatus` call; the pump loop at `:555-569`
runs additional drains until `!pump.HasPendingOrLive`. The dossier's "quiescence / between
reductions only" (`glp_engine.cs:545`) is under-specified: it cites only the first drain point
and does not explicitly state that the snapshot must be taken after the full pump-extended cycle.
If the snapshot is taken after the first drain but before the pump loop's exit, the heap is
in a mid-session state (link frames in flight, goals suspended on live links).

**Options:**
1. Define quiescence as "all drains complete AND pump.HasPendingOrLive == false" — correct for
   link-active scenarios; requires the persistence API to be called from the host's pump loop,
   not from inside `RunGoalAsync`.
2. Define quiescence as "after each DrainAsyncWithStatus" — simpler but incorrect when a link
   is open (mid-session snapshot with pending frames is not consistent).
3. Only snapshot when no link is live (i.e. `InboundPump == null || !pump.HasPendingOrLive`) —
   conservative; simplifies correctness; defers link-aware persistence to a follow-up.

*Recommendation:* Option 3 for MVP; Option 1 as the full design target. The spec must
state the quiescence contract precisely.

---

## Under-specifications

### U1 — _waitReaders / timer state restore semantics

**Why it matters:** `wait()` guards rely on `_waitReaders` mapping a `goalId` to a `readerId`
that a background timer will bind. On resume, the timer is gone. If `_waitReaders` is restored
but the timer is not re-armed, the goal hangs indefinitely. If it is not restored, the goal's
wait state is silently lost.

**Options:**
1. Treat `_waitReaders` as PERSISTENT; restore it; add a "re-arm timers" step in the resume
   driver that re-fires background `Task.Delay` for restored wait entries.
2. Treat `_waitReaders` as non-resumable (EPHEMERAL); goals with `wait()` in flight are
   classified as non-resumable; snapshot is rejected if any such goals exist.
3. Treat `_waitReaders` as PERSISTENT but with zero re-arming; on resume the wait readerId
   is bound immediately (simulating an expired timer), allowing the goal to continue.

### U2 — GlpChannels re-registration at resume

**Why it matters:** `runtime.cs:53` `GlpChannels` entries map channel names to heap-addr pairs.
On resume the heap is restored with the same addresses, but `GlpChannels` itself is an empty
`Dictionary` until `ActivateModule` is called. If activated modules are in `_loadedPrograms`,
the standard module-activation boot path will re-populate `GlpChannels`. If not, the channel
routing is broken and `Distribute`/`Transmit` opcodes will fail.

**Options:**
1. Persist `GlpChannels` as a name→heap-addr map; restore directly at boot before any goal
   runs. Simple, correct, requires no module re-activation.
2. Rely on IL/source reload to re-activate all modules; `GlpChannels` is re-populated as a
   side effect. Correct if all modules are in `_loadedPrograms` at snapshot time.
3. Treat `GlpChannels` as EPHEMERAL; any Distribute/Transmit that fires after resume but
   before channel re-registration fails gracefully and gets re-tried.

### U3 — Snapshot atomicity with respect to C# GC

**Why it matters:** `heap_fcp.cs:148` stores `HeapCell` objects; each cell's `Content` holds
reference-type objects (`Pointer`, `WriterContent`, `SuspensionListNode`, `Term`, etc.). A
serializer walking the heap must ensure no object is GC'd or mutated between when it is
enumerated and when it is written. On the single-owner thread this is safe during a synchronous
walk, but async I/O to PGLite (or JSON file) introduces a window.

**Options:**
1. Take the snapshot synchronously on the runner thread (no `await` during the walk phase);
   only the write-to-store is async. Correct; aligns with single-owner invariant.
2. Copy the heap array before writing (deep clone); write the copy asynchronously. Safe but
   doubles peak memory.
3. Use a dedicated "save checkpoint" goal that runs inside the GLP scheduler itself (no
   external async hazard). Conceptually clean but complex to implement.

### U4 — JSON-fallback blob format

**Why it matters:** The MarathonStore JSON fallback uses one file per checkpoint named by
`sequence_no`. A heap snapshot blob can be multi-megabyte for a large heap. JSON is not an
efficient format for binary data (e.g. the `Cells` array with typed discriminants).

**Options:**
1. Use MessagePack or CBOR for the heap/goal-table blobs; embed in the JSON checkpoint as
   a base64 field. Efficient; still single-file per checkpoint.
2. Use a side-car binary file (`.bin`) alongside the JSON metadata file.
3. Use JSON throughout (compact-representation); accept the size/speed trade-off for an MVP.

---

## GEPA/DSPy refinement

### Applicability: `methodological`

This seed is a C# systems API (a serialization/persistence layer over an object graph), not an
LM/codegen program. GEPA/DSPy is applicable in the methodological sense: use the
iterate-against-a-metric discipline to converge the snapshot spec (field inventory, blob format,
quiescence contract, resume correctness), not to optimize a DSPy program.

The primary value of the GEPA frame here is forcing explicit metric thresholds before coding
starts, and using the metrics as the convergence signal for each design-or-implementation
iteration.

### Seed definition

Design and implement a C# engine-state persistence API that:
1. Captures a consistent snapshot of `GlpRuntimeEngine` + `GlpEngine` persistent state at
   each quiescence boundary (including: `Heap.Cells`+`Hp`; `Gq`; `Suspended`; `_budgets`;
   `_goalEnvs`; `_goalPrograms`; `_goalModuleContexts`; `NextGoalId`; `GlpEngine._goalId`;
   `_waitReaders`; `InfrastructureGoalIds`; `_loadedPrograms`+`_loadedModules`+`_serveBytecode`;
   `GlpChannels` (or re-activation path); `LinkId`/listen-def list).
2. Stores the snapshot under a strictly-monotonic `sequence_no` in a PGLite-primary +
   JSON-fallback dual store (C# implementation, same dual-store shape as MarathonStore).
3. Exposes a minimal API: `SaveSnapshot(engineId, seq, blob)`, `LoadLatestSnapshot(engineId)`,
   `SaveDefinition(kind, id, blob)`.
4. Guarantees the snapshot is taken only at quiescence (no in-flight reduction, no live pump
   frames); in the MVP, restricts to quiescence with no open links.

### Metrics combination

| # | Name | Kind | Tool / harness | Threshold |
|---|---|---|---|---|
| P1 | Kill-and-restart equivalence | pragmatic | Kill the engine mid-session; restart; compare `ExecutionResult` for the same goal continuation against the no-kill baseline using the `EquivTrace` (`equiv_trace.cs`) mechanism. | 100% equivalence on the REPL test suite programs that reach quiescence (non-link runs first) |
| P2 | Snapshot round-trip identity | pragmatic | `snapshot(state) → serialize → deserialize → compare(state', state)` — field-by-field equality including heap cell count, Hp, Gq length, Suspended keys, goal tables. | 100% field equality on 50 synthetic heap states of varying sizes |
| P3 | Dual-store consistency | pragmatic | After 100 write/read cycles with simulated primary outage (fallback-only mode), verify that `LoadLatestSnapshot` returns the max-seq checkpoint and the content matches both stores when re-synchronized. | 0 fork-detection errors; 100% max-seq retrieval |
| P4 | REPL suite non-regression | pragmatic | `bash test/run_all_tests.sh` — all tests that complete at quiescence (non-link, non-timer) must still pass after the persistence API is inserted into the drain path. | 384/384 (same as baseline) |
| F1 | Monotone binding invariant across resume | formal | Type-checker / SRSW validator: after restore, all restored variable bindings are ground or unbound — no writer-to-writer bindings, no re-bound writers. Run the in-repo type/SRSW checker on any goal loaded post-restore. | 0 SRSW violations on restored heap |
| F2 | Suspension chain integrity after restore | formal | Mechanized check: for each entry in `Suspended`, the corresponding heap writer cell has a non-null `WriterContent.Suspensions` chain, and each `SuspensionListNode.GoalId` is in `_goalEnvs`. Expressed as a C# invariant assertion run at boot before any goal fires. | Assertion passes on 100% of restored states |

### Interactive spec step

At the start of `/buildkit-specify` for this seed, the owner confirms:

1. Which missing fields (T1) are in-scope: `_waitReaders`, `GlpEngine._goalId`,
   `InfrastructureGoalIds`, `GlpChannels`.
2. IL coupling strategy (T3 options 1–3): label-reference MVP or IL-codec dependency on #4.
3. Quiescence definition (T4): MVP = no open links; full = pump-extended.
4. DB language choice (T2): C# Npgsql + JSON fallback vs other.
5. JSON blob format (U4): MessagePack/CBOR vs JSON.
6. Whether F1/F2 formal metrics are boot-time assertions or proof-assistant lemmas for this
   seed (vs deferred to the methodology feature #1a).

### Refinement loop

Claude-run, no external API. Each iteration:
1. **Candidate:** draft the C# `IEngineSnapshotStore` interface + `EngineSnapshotBlob` type
   (field inventory; blob schema; quiescence guard).
2. **Evaluate P2:** synthetic round-trip test — serialize a known heap state, deserialize,
   compare field-by-field. Fail = missing field or type mismatch.
3. **Evaluate P1 (kill-restart):** run a non-link goal; snapshot at quiescence; kill; restore;
   re-run the same goal; compare with `EquivTrace`.
4. **Evaluate F2:** run the suspension-chain invariant checker on the restored state.
5. **GEPA mutation:** if any metric fails, reflect on the gap (missing field, wrong quiescence
   boundary, wrong blob encoding); mutate the spec (field list, quiescence contract, blob format)
   and repeat.
6. **Terminate** when P1+P2+P3+F2 all pass threshold AND the REPL suite (P4) is green.

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** Good fit for the monotone-binding invariant (F1) and suspension-chain integrity
(F2), both of which are straightforward safety properties over finite integer-indexed maps.
Lean 4's `mathlib` has strong support for `Finset`, `Map`, and inductive invariants. The
Lean-LSP-MCP connector enables Claude-driven tactic generation. The APOLLO model-agnostic loop
(2505.05758) is available for sub-goal repair. For this seed the proofs are not deeply
mathematical — they are structural invariants over a snapshot's internal consistency. Lean 4 is
well-suited.

**Rocq fit:** Rocq (formerly Coq) has strong prior art for verified serialization (Vellvm-style
certified encoders) and for operational-semantics preservation (verified Prolog→WAM compiler
cited in the brief). For the heap snapshot this is relevant: encoding/decoding
`List<HeapCell>` + `int Hp` + all goal tables is exactly the "round-trip identity" domain
where Rocq's certified extraction and `Int`/`Bytes` libraries shine. AutoRocq's GPT-4 dependency
is the defect to adapt away (use Claude via Agent seams instead). Rocq fits the byte-contract
and round-trip proof sub-problem better than Lean 4 if the snapshot blob has a byte-level spec.

**Primary:** `lean4`

For this seed, the formal metrics are structural invariants (monotone binding, suspension-chain
integrity) over in-memory maps with integer keys — Lean 4's `mathlib` handles this directly.
Byte-contract proofs are not a primary concern here (the blob format is an API design choice,
not a pre-existing wire contract), so Rocq's certified-serialization strength is not the decisive
factor.

**Alternative when:** If the owner chooses MessagePack/CBOR as the blob format (U4 Option 1)
and specifies a byte-level round-trip proof obligation (i.e. the blob format becomes a
formal commitment similar to FR-060/061 byte-parity), switch the byte-contract proof to Rocq
(its `Int`/`Bytes` / `ZMicromega` infrastructure is better for this). The invariant proofs
(F1/F2) remain in Lean 4.

### IL verification

**n/a** for this seed. #7 does not define a new IL or wire format; it serializes existing GLP
runtime state to a DB blob. The IL codec is #4's concern. The only IL-touching aspect is the
T3 tension (ModuleTerm-embedded BytecodeProgram in the heap snapshot), which is resolved by
the label-reference scheme (not a new wire format). If the owner chooses Option 1 for T3
(full IL embedding in the snapshot blob), the IL verification standard (MLIR-dialect /
byte-parity / round-trip) from #4 applies, and this field should be revisited.

---

## Shapiro criteria preserved

1. **Monotone variable binding** — the snapshot must encode only the monotone-binding
   state of the heap: once a writer cell is bound, it stays bound. The restore path must
   never re-bind a previously bound writer (this is the F1 formal metric). Violating this
   would allow a resumed goal to "see" a variable unbound that was already ground, breaking
   the committed-choice semantics.

2. **SRSW (Single-Reader / Single-Writer)** — the heap's writer/reader pairing is preserved
   by snapshotting `Cells` verbatim (addresses are self-consistent integers). The restore
   path must not introduce duplicate writers or aliased readers. F2 partially checks this
   for suspension chains; a full SRSW check on the restored heap is advisable.

3. **Suspension correctness** — `Suspended` index and on-heap `WriterContent.Suspensions`
   chains must be consistent after restore: every entry in `Suspended` must have a
   matching suspension node on the heap, and every armed suspension node's `GoalId` must
   be in `_goalEnvs`. F2 is the formal gate; P1 (kill-restart) is the behavioral gate.

4. **Committed-choice concurrency** — re-entrancy of the resume path must be blocked: only
   the composition root (FR-057) may call `LoadLatestSnapshot`; the engine must not be
   concurrently executing when a snapshot is taken or loaded. The single-owner
   (`heap_fcp.cs:136-141`) and single-thread invariant must be preserved across the
   save/restore boundary.

5. **Three-phase HEAD/GUARD/BODY** — snapshots are only valid at quiescence (between
   reductions). A snapshot mid-HEAD phase (while `SigmaHat` is non-empty) is corrupt.
   `SigmaHat` is EPHEMERAL and empty at quiescence (drain-loop invariant), so this is
   already satisfied if the quiescence contract (T4) is correctly specified.

---

## Recommendation

**Proceed with scope expansion before `/buildkit-specify`:**

1. At `/buildkit-specify` time, expand the scope line to include `_waitReaders`,
   `GlpEngine._goalId`, `InfrastructureGoalIds`, and `GlpChannels` (T1).
2. Adopt T2 Option 1+3: C# Npgsql+JSON dual store in a new `glpengine` PGLite schema.
3. Adopt T3 Option 2 (MVP label-reference for `ModuleTerm`); make IL-embedding a follow-up
   gated on #4.
4. Adopt T4 Option 3 (MVP quiescence = no open links); document the pump-extended quiescence
   target.
5. Resolve U1 (timer state) explicitly in the spec — Option 1 (re-arm) is safest.
6. Use F1+F2 as boot-time C# assertion invariants for the MVP; promote to Lean 4 proofs
   when the methodology feature (#1a) provides the proof infrastructure.

**WSJF=2.25 and RICE=1800 appear correctly calibrated.** The seed is a large but well-bounded
net-new implementation with clear substrate (MarathonStore pattern), no design unknowns beyond
the forks above, and hard-blocking effect on #8 and #9. The dependency on #6 is real: the
quiescence boundary is only meaningful once the process split exists.

---

## Options for owner

1. **Expand scope + proceed** (recommended): update the scope line to include all five
   additional fields (T1); resolve T2–T4 per the recommendations; proceed to `/buildkit-specify`.
   Consequence: the spec is correct and complete before any implementation begins.

2. **Proceed with current narrow scope**: treat the scope line as approximate; rely on the
   implementation to discover missing fields. Consequence: higher risk of a subtly broken
   resume (timers lost, goal-ID collisions), discovered late.

3. **Split into two seeds**: (a) `engine-heap-and-goal-snapshot` (the core snapshot machinery)
   and (b) `engine-persistence-store-api` (the dual PGLite/JSON store API). Consequence:
   finer granularity; allows the snapshot machinery to proceed independently of the DB
   implementation choice; increases roadmap bookkeeping.

---

## Open questions

1. Should `_waitReaders` timers be re-armed on resume (U1 Option 1) or treated as expired
   (Option 3)? The behavioral difference matters for programs with non-trivial `wait()` guards.
2. Is the `glpengine` PGLite schema added to the existing `C:/pglite/research/glpnet` cluster
   (alongside `codeconv`, `marathon`, `dbos`)? Or does the engine host use a separate cluster?
3. After #6 lands (process split), the persistence API is called from the engine host process.
   Is the host's PGLite connection the same bridge process as `codeconv`'s, or a separate one?
4. `GoalState.SigmaHat` (`machine_state.cs:59`) is the tentative writer substitution per goal.
   Confirm that `SigmaHat` is always empty at quiescence (i.e. no partial HEAD phase survives
   a drain); if not, it must also be snapshotted.
5. What is the expected heap size at quiescence for a typical GLP session? This determines
   whether the full-heap-per-quiescence snapshot strategy (§10.6 Opt 1) is viable for the MVP
   or whether the definition-log+checkpoint hybrid (Opt 2) must be designed in from the start.

---

## External refs

- `out/csharp/lib/runtime/runtime.cs` (GlpRuntimeEngine full state inventory)
- `out/csharp/lib/runtime/heap_fcp.cs:148,154,157` (Cells, Hp, _bindCallbacks)
- `out/csharp/lib/engine/glp_engine.cs:150-154,156,545,555-569` (_loadedPrograms, _goalId, drain+pump loop)
- `out/csharp/lib/runtime/machine_state.cs:59` (GoalState.SigmaHat)
- `out/csharp/lib/runtime/terms.cs:146-149` (ModuleTerm)
- `out/csharp/lib/runtime/glp_activation.cs:88` (ModuleTerm stored on heap)
- `out/csharp/lib/bytecode/runner.cs:41-53` (BytecodeProgram)
- `out/csharp/lib/compiler/result.cs:9` (CompilationResult.VariableMap)
- `codeconv/src/codeconv/marathon/store.py:96,139` (MarathonStore shape + active_store)
- `csharp/glp_link/primitives/LinkRegistry.cs:25-34` (GetOrEstablish — resume-or-rebuild seam)
- `csharp/glp_link/seam/LinkId.cs:53-56` (LinkId — persistent definition)
- `docs/research/repl-engine-separation/design-dossier.md §6.1–§6.4, §10.5–§10.8`
- TWAM — certifying abstract machine for logic programs: https://arxiv.org/pdf/1801.00471
- APOLLO (model-agnostic Lean proving): https://arxiv.org/abs/2505.05758
- First-Class Verification Dialects for MLIR (PLDI'25): https://users.cs.utah.edu/~regehr/papers/pldi25.pdf
