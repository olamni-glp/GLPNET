# Seed #9 — restore-and-resume-with-link-reestablish

**Reconciliation memo** · Feature `restore-and-resume-with-link-reestablish` · Dossier §11 entry #9  
**Date:** 2026-06-09 · **Branch:** `026-engine-review-dossier`  
**Methodology authority:** `reconciliation/SEED-RECONCILIATION-BRIEF.md`

---

## Dossier cross-references

Primary: **§6.4** (Bootstrap + restore-and-resume)  
Supporting: **§6.2** (persistent-vs-ephemeral classification table), **§6.3** (DB + API shape), **§5** (liveness/crash/restart model), **§0.4** row "Persistent-vs-ephemeral definition/instance seam", **§12** risks 3 and 5, **§10.7** (where snapshot/resume driver lives — FR-057)  
Successor in Appendix B: row #9

---

## Seed-vs-dossier-vs-code

### Roadmap brief (as fetched)

> "MVP. On restart reload persistent constructs, re-establish ephemeral links from LinkId/listen definitions via GetOrEstablish, re-wire cursors, resume the drain; pass a kill-and-restart correctness test. Proves the persistent-vs-ephemeral distinction end-to-end. depends-on: #7,#8 + feature-025 link-establish core."

### Dossier §11 entry #9 (verbatim)

> "On restart reload persistent constructs, re-establish links from `LinkId`/listen defs via `GetOrEstablish`, re-wire cursors, resume the drain; kill-and-restart correctness test — proves the §6 distinction end-to-end. depends_on: 7, 8. §ref: §6.4."

### Dossier §6.4 (authoritative description)

> "WARM restart: reload IL into `_loadedPrograms` (or recompile from source); re-register kernels; rebuild the transport registry; restore heap + goal-queue + suspension snapshot; re-establish links from persisted `LinkId`/listen defs via `GetOrEstablish`/`WireEstablishedLink` (fresh sockets, fresh cursors re-wired to restored heap addrs); resume the drain — suspended goals reactivate when `LinkPump.TryApplyNext` re-extends the In-stream (`LinkPump.cs:104-124`). Corpus 06 (`docs/research/multi-protocol-link-layer/corpus/06-heap-fcp-live-implementation.md:255-274`) confirms the imported-variable path is the live remote seam and that resume must rebuild suspension chains so reactivations are identical."

> "Where the resume driver lives (FR-057): the engine may own heap+goal snapshot; link re-establishment must be above it (in `glp_link` or the composition root), OR the engine gains a new resume-hook injection seam analogous to `rt.InboundPump` (`runtime.cs:129`)."

### Brief-vs-dossier delta

The roadmap brief matches the dossier §11 entry almost verbatim. Minor delta: the brief says "feature-025 link-establish core" as an explicit dep that the dossier's §11 entry elides (it is implied by §6.4's `GetOrEstablish`/`WireEstablishedLink` references). This is not a contradiction — it is a clarification. No divergence.

### Code baseline (as-built, verified)

| What dossier claims | As-built code | Verdict |
|---|---|---|
| `GetOrEstablish` in `LinkRegistry.cs:25-34` — idempotent-at-identity reuse-or-rebuild seam | `csharp/glp_link/primitives/LinkRegistry.cs:25-34` — confirmed, `Func<LinkHandle> establish` runs only on first call | CONFIRMED |
| `WireEstablishedLink` in `LinkEstablish.cs:29` — single canonical establish-and-wire core | `csharp/glp_link/primitives/LinkEstablish.cs:29-88` — confirmed; wires `InWriterAddr`, `OutReaderAddr`, `FaultsWriterAddr`, arms egress (`ArmEgress`), calls `link.Pump.AddLink(handle)`, sets `rt.InboundPump` | CONFIRMED |
| `LinkPump.TryApplyNext` at `LinkPump.cs:104-124` — extends In-stream and reactivates suspended goals | `csharp/glp_link/primitives/LinkPump.cs:104-124` — confirmed, allocates fresh `(writer, reader)` pair, binds current writer, enqueues reactivated goals | CONFIRMED |
| `heap_fcp.cs:148,154` — Cells + Hp are the persistent heap state | `out/csharp/lib/runtime/runtime.cs:27` (Heap), `out/csharp/lib/runtime/heap_fcp.cs` — dossier citations not re-verified line-for-line here but consistent with the class structure found | CONFIRMED |
| `LinkHandle.Endpoint` + Sequencer/Window/Reassembler/Ordering are EPHEMERAL | `csharp/glp_link/primitives/LinkHandle.cs:17-30` — confirmed; `InWriterAddr`/`OutReaderAddr`/`FaultsWriterAddr` are nullable `int?` (`:35-41`) set during establishment, NULL until wired | CONFIRMED |
| `LinkRuntime.Pending` is EPHEMERAL | `csharp/glp_link/primitives/LinkRuntime.cs:50` — `Dictionary<LinkId, ILinkEndpoint> Pending` — confirmed, in-memory only | CONFIRMED |
| `ArmEgress` re-arms the egress callback (`_bindCallbacks`) on resume | `LinkEstablish.cs:95-98` — `ArmEgress` registers `OnBind` callback; this is listed in `ResourceSnapshot.cs:17` (`BindCallbacks` counter) as a reclaim target; callback is set at `heap.OnBind(outWriterAddr, ...)` — confirmed ephemeral, must be re-armed | CONFIRMED |
| `rt.InboundPump` set by `WireEstablishedLink` | `LinkEstablish.cs:85`: `rt.InboundPump ??= link.Pump` — confirmed; set on first `AddLink`, already null-guarded | CONFIRMED |
| FR-057: composition root is `Program.cs:30-35`; link layer never referenced by engine | `out/csharp/glp_repl/Program.cs:30-35` — confirmed; `GlpRuntime.Repl.Program.AfterEngineCreated` hook is the sole place both `glp_runtime_net` and `GlpLink` are referenced | CONFIRMED |
| No snapshot/persist path exists today | `out/csharp/lib/engine/glp_engine.cs` — no `SaveSnapshot`/`LoadSnapshot`/`persist`/`resume` anywhere; confirmed by grep | CONFIRMED |
| No liveness/watchdog/IHostedService exists today | `out/csharp/bin/glp_repl.cs`, `out/csharp/glp_repl/Program.cs` — no such references; confirmed by grep | CONFIRMED |

### What the dossier MISSED / what #9 must handle that is not fully specified

1. **Heap-address cursor rebinding.** After a heap snapshot is restored, the `InWriterAddr`/`OutReaderAddr`/`FaultsWriterAddr` in every `LinkHandle` must be re-wired to the restored heap addresses. Today `WireEstablishedLink` populates these from the *live goal's unbound writer/reader args*. On resume, the heap is pre-populated — the args are already bound to specific heap addresses. A call to `WireEstablishedLink` will see bound cells, not unbound writers/readers, and will `Abort` at `LinkEstablish.cs:38-43`. **The re-establish path needs a dedicated `RewireHandle` variant or a new protocol** that bypasses the "must be unbound" guard for warm-restart. This is not mentioned in §6.4.

2. **`_bindCallbacks` heap leak on re-establish.** `ArmEgress` installs a C# delegate via `heap.OnBind(outWriterAddr, ...)`. The heap snapshot restores the `Cells` array; it does NOT restore `_bindCallbacks` (they are C# closures, not serializable). On resume, egress callbacks must be re-armed against the restored heap addresses. If egress is NOT re-armed, a resumed goal writing to `Out` will never ship frames. **This re-arm step is implicit in §6.4's "re-wire cursors" but not spelled out.**

3. **`MonitorCursors` on `LinkHandle`.** `LinkHandle.MonitorCursors` (`:51`) is a `List<int>` of heap writer addresses for fault fan-out. These addresses come from the heap snapshot and must be transferred to the new `LinkHandle` built during re-establish. §6.4 does not address this.

4. **`LinkRuntime.Pending` on re-establish.** For listen-role links, the original establishment went through `_link_listen` → `LinkListenKernel` → `Pending` → `_link_accept` → `WireEstablishedLink`. On a warm restart with a persisted listen-rendezvous, the question is whether to re-run `_link_listen` (which re-binds the rendezvous and surfaces a new `Requests` stream) or to re-run `_link_accept` against a previously-stored `LinkId`. The dossier says "re-bind [the rendezvous] at boot" (§6.2 table row "listen rendezvous") but says `Pending` is EPHEMERAL and "dropped, re-accepted". The sequencing — who triggers the re-listen and when in the resume sequence the peers reconnect — is unspecified.

5. **OS file/FFI handle open goals (§12 risk 3).** `runtime.cs:64,69` — goals holding stale file/FFI handle ints will resume against garbage handles. This is flagged as an OPEN PROBLEM in §6.2 but #9 is expected to "decide per-construct." The actual decision mechanics are not specified.

---

## Classification check

**Dossier kind: MVP.**  
Is it right? Yes. This seed delivers a concrete end-to-end correctness criterion (kill-and-restart test) that proves the §6 persistent/ephemeral model actually works. It is not a PREP (it ships a testable capability) and not a FOLLOW-UP (it gates durability's success theme alongside #7/#8). MVP classification is correct.

**Code supports scope?**  
The scope "re-establish links from `LinkId`/listen defs via `GetOrEstablish`, re-wire cursors, resume the drain" is grounded in real code:
- `GetOrEstablish` at `csharp/glp_link/primitives/LinkRegistry.cs:25`  
- `WireEstablishedLink` at `csharp/glp_link/primitives/LinkEstablish.cs:29`  
- `TryApplyNext` at `csharp/glp_link/primitives/LinkPump.cs:86`  
- `rt.InboundPump` at `out/csharp/lib/runtime/runtime.cs:129`  

BUT: the "re-wire cursors" part of the scope hits a gap — `WireEstablishedLink` requires unbound writer/reader cells (`LinkEstablish.cs:38-43`), which is false after heap restoration. A dedicated warm-restart re-wire path does not exist today. The scope is achievable but requires net-new logic not noted in the dossier's §6.4 narrative. This widens the net-new surface slightly.

---

## Tensions

### T1 — `WireEstablishedLink` guard blocks warm restart

**Summary:** `WireEstablishedLink` (`LinkEstablish.cs:38-43`) aborts if In/Faults are not unbound writers and Out is not an unbound reader. After heap restoration from a snapshot, these cells ARE already bound (they hold the stream state from before the crash). Calling `WireEstablishedLink` on a restored handle will fail.

**Evidence:** `csharp/glp_link/primitives/LinkEstablish.cs:38-43` (`if (inArg is not VarRef inVr || !heap.IsWriter(inVr.Addr)) return Abort(...)`); §6.4 says "re-establish links … via `GetOrEstablish`/`WireEstablishedLink` (fresh sockets, fresh cursors re-wired to restored heap addrs)."

**Options:**
1. Add a `RewireHandle(handle, inAddr, outAddr, faultsAddr)` method to `LinkEstablish` that takes already-resolved heap addresses and skips the unbound-guard; replays `ArmEgress`, `link.Pump.AddLink`, `LinkFaults.Register`, GC hook.
2. Extend `WireEstablishedLink` with an explicit `bool warmRestart` parameter that bypasses the unbound checks and instead validates the heap address is a writer (not necessarily unbound).
3. Treat re-establish as a "new goal execution" that re-runs `_link_setup` against fresh unbound holes, then splices the restored In-stream tail onto the fresh holes after establishment.

### T2 — cursor re-bind vs heap-address stability across snapshot/restore

**Summary:** If the snapshot preserves `Cells` verbatim (§6.3 — "snapshot whole array atomically at quiescence"), heap addresses are stable by construction. But if any compaction or address-reassignment happens (e.g., for a definition-log + checkpoint hybrid, §10.6 Opt 2), the heap addresses in `InWriterAddr`/`OutReaderAddr`/`FaultsWriterAddr` must be remapped. The dossier is silent on whether snapshot-restore is address-preserving.

**Evidence:** §12 risk 5 ("heap-address stability across resume — external refs break if addresses shift"); §6.3 ("snapshot `Cells` verbatim (int self-consistency permits exact-address resume) or add a stable logical-id layer (§10.4 Opt 1)").

**Options:**
1. Mandate verbatim `Cells` restore (no compaction, exact-address semantics); document as a constraint on the snapshot API in #7.
2. Add a stable `GlobalVarId`-like logical-id layer (§10.4 Opt 1) so link cursor addresses survive compaction; #7 must produce a remapping table consumed in #9.
3. Prohibit compaction until after #9 ships; revisit in #15 (shared-static-memory experiment).

### T3 — re-listen sequencing for accepted links

**Summary:** For links that were accepted (path-B: `_link_listen` → `_link_accept`), the listen rendezvous must be re-bound at boot (§6.2), but the remote peer must also re-connect for `GetOrEstablish` to produce a live socket. The ordering is: restart host → re-bind listen → peer reconnects → `_link_accept` re-runs → handle wired. If the peer crashes/reconnects before the listen rendezvous is re-bound, the connection is lost. §6.4 is silent on which side drives reconnection timing and whether the test scenario covers asymmetric restart.

**Evidence:** `csharp/glp_link/transports/TcpTransport.cs:46-48` (one-accept-then-Stop); `csharp/glp_link/primitives/LinkRuntime.cs:50` (Pending is in-memory); §6.4 "re-bind [rendezvous] at boot."

**Options:**
1. Scope the kill-and-restart test to single-client/single-link only; both sides restart together (controlled test environment); note the asymmetric restart case as a follow-up.
2. Add a client-side reconnect-retry loop so the client keeps retrying after server restart; the server's re-listen makes the handshake self-healing.
3. Document as an open protocol question gated by #10 (multi-accept) and defer asymmetric restart to post-#10.

---

## Under-specifications

### U1 — What "resume the drain" means after heap+goal snapshot restore

**Why it matters:** The drain loop (`glp_engine.cs:545`, `DrainAsyncWithStatus`) drives the scheduler. On resume, the goal queue (`Gq`) is non-empty (loaded from snapshot), and the heap already has suspension chains. But the scheduler and runner are rebuilt fresh per drain-step (§6.2: "EPHEMERAL (rebuilt per drain-step)"). How the first drain-step is triggered after restore — and whether the scheduler's quiescence detection sees the restored `Gq` as "not yet drained" — is not specified.

**Options:**
1. After `LoadLatestSnapshot`, call `RunGoalAsync` with a synthetic "resume" goal that does nothing but re-trigger the drain; the restored `Gq` items then proceed.
2. Expose a `ResumeDrainAsync()` entry point on `GlpEngine` that enters the drain loop without compiling/enqueuing a new goal; processes the restored `Gq` directly.
3. Treat the snapshot as containing only definitions + link state; replay each persisted goal from its source text (recompile on resume).

### U2 — Kill-and-restart test specification

**Why it matters:** The dossier names "a kill-and-restart correctness test" as the deliverable but does not specify: (a) what goal/program state is established before the kill; (b) what "kill" means (graceful shutdown? SIGKILL? process exit with non-zero code?); (c) what observable post-resume equivalence must hold (same bindings? same output? suspended goals reactivate and produce identical results?).

**Options:**
1. A minimal test: establish a single link, run a goal that suspends on an inbound stream value, kill the engine, restore from snapshot, re-establish the link, re-deliver the value, assert the goal produces the same binding as a no-kill baseline run.
2. A broader test: multiple goals at different stages (succeeded, failed, suspended), multiple links; assert that each goal's post-resume outcome matches its pre-kill expected state.
3. Focus the test on the link-reestablish path only (not full goal-state resume); defer full goal-state correctness to a later test milestone.

### U3 — How `_bindCallbacks` survive (or don't) across the snapshot boundary

**Why it matters:** `ArmEgress` registers `heap.OnBind(outWriterAddr, ...)` — a C# delegate. The heap `Cells` snapshot does NOT include C# delegates. On resume, the Out-stream egress is dead unless re-armed. The resume sequence must include a step that re-arms egress for every re-established link. The order (restore heap → re-arm egress → resume drain) matters: if the drain runs before egress is armed, an Out-stream bind fires without shipping frames.

**Options:**
1. Make the resume sequence in the composition root explicit: restore snapshot → re-establish links (which calls `ArmEgress` per `WireEstablishedLink`/`RewireHandle`) → then resume drain; document the ordering constraint.
2. Add a post-restore hook to `GlpEngine` that fires after heap load and before drain, where the link layer can re-arm all egress callbacks given the restored `LinkId` list.
3. Store the `outWriterAddr` in the snapshot's `linkDefs[]` blob (§6.3) so the resume path can re-arm egress without re-running establishment.

---

## GEPA/DSPy refinement

### Applicability

**`methodological`.**  
This seed is systems C# code + a test harness; GEPA/DSPy does not directly optimize a language model program here. The methodological interpretation applies: use the iterate-against-a-metric discipline — seed (re-establish protocol) → candidate implementation → evaluate against the kill-and-restart correctness test + Shapiro-criteria metrics → reflective mutation of the design decisions (T1/T2/T3 above) → repeat until thresholds hold.

### Seed definition

The seed is the **warm-restart re-establish protocol**: a procedure that, given a heap+goal snapshot and a set of persisted `LinkId`/listen definitions, reconstructs a live `LinkRuntime` (fresh sockets, cursors re-wired to restored heap addresses, egress re-armed, pump re-started) such that the resumed `GlpEngine.DrainAsyncWithStatus` loop produces outcomes indistinguishable from a continuous run — specifically, suspended goals that were awaiting inbound link data reactivate correctly when `TryApplyNext` re-extends their In-streams.

The GEPA reflective questions are: (a) Does the re-establish protocol satisfy `WireEstablishedLink`'s caller contract? (b) Are egress callbacks re-armed in the right order? (c) Does `ResourceSnapshot.IsBaseline` return true after the first link reclamation post-resume, confirming no state leak?

### Metrics combination

| Name | Kind | Tool / Harness | Threshold |
|---|---|---|---|
| Kill-and-restart correctness | pragmatic | C# xUnit test: establish link → run goal → kill → restore → re-establish → resume | Post-resume goal outcome (bindings/status) byte-identical to no-kill baseline |
| ResourceSnapshot baseline return | pragmatic | `ResourceSnapshot.IsBaseline` probe at `csharp/glp_link/reliability/ResourceSnapshot.cs:23`; assert after re-establish teardown | All four counters (GlobalWriters, SendRegistryGoals, BindCallbacks, ReplyTableEntries) return to baseline |
| REPL test suite (non-regression) | pragmatic | `bash test/run_all_tests.sh` | 384/384 — no regressions from the new re-establish path |
| Snapshot round-trip identity | pragmatic | Encode heap snapshot → restore → re-snapshot; assert decoded state equals original | All persistent fields (Cells+Hp, Gq, Suspended, per-goal tables, NextGoalId, linkDefs) compare equal |
| SRSW validity on resumed goals | formal | In-repo type-checker + SRSW validator (from GLP pipeline) applied to the GLP program loaded at resume | 0 SRSW violations reported on the resumed program |
| Suspension correctness (heap invariant after cursor re-wire) | formal | Lean 4 mechanized property: after `RewireHandle`, every goal in `Gq` that was suspended on `InWriterAddr` prior to kill is still in `Suspended` index with the same reader-varId mapping | Proof obligation discharged (see Formal tooling) |
| Monotone variable binding across snapshot boundary | formal | Lean 4: property that restoring a quiescence-point snapshot and then re-extending the In-stream is monotone — no binding is un-done; the heap binding function is still monotone on the restored cells | Proof obligation discharged |

### Interactive spec step

At the start of `/buildkit-specify` for this seed, the owner confirms:
- Which of U1 options (drain trigger after restore) is adopted — this determines the `GlpEngine` API surface for resume.
- Whether T2 Opt 1 (verbatim-address semantics) is mandated as a constraint on #7's snapshot API, or whether a remapping layer is required.
- The exact kill-and-restart test scenario (U2 options) — minimal single-link or broader multi-goal.
- Whether the formal Lean 4 suspension-correctness property (above) is in scope for the MVP sprint or deferred to the formal-verification track.

### Refinement loop

1. Draft the `RewireHandle` method (or `WireEstablishedLink` warm-restart extension) for the composition-root resume sequence.
2. Evaluate: does the C# xUnit kill-and-restart test pass? Does `ResourceSnapshot.IsBaseline` hold after the first re-established link is torn down?
3. GEPA reflective mutation: if the test fails due to egress callback ordering, refine the resume sequence (U3 options); if it fails due to heap-address mismatch, resolve T2.
4. Formal check: run the in-repo SRSW validator on the GLP program being resumed; confirm no violations introduced by the re-establish path.
5. Repeat until all pragmatic thresholds pass AND the SRSW formal gate is green. Lean 4 suspension-correctness proofs proceed in parallel on the mechanized-semantics track (seeded by #1a).

All evaluation steps run in Claude via Agent-tool seams / MCP; no OpenAI/litellm/OPENAI_API_KEY.

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** Strong. The two key proof obligations (suspension-correctness after cursor re-wire; monotone binding across snapshot boundary) are straightforwardly expressed as Lean 4 propositions over an inductive `HeapState` type. Lean 4's `Mathlib` has well-developed monotone/order-theory infrastructure directly applicable to "monotone binding." The APOLLO/Lean-LSP-MCP/Lean-Copilot agentic toolchain (all model-agnostic) drives tactic generation in Claude. The TWAM precedent (certified abstract machine for logic programs, `arxiv 1801.00471`) is WAM-lineage and directly applicable as a proof template for "the resumed machine state is semantically equivalent to the pre-crash state."

**Rocq fit:** Also strong for this seed — Rocq/Coq has a longer track record for verified abstract machines (WAM compiler correctness, `ScienceDirect 0743106692900547`; Vellvm for LLVM IR). The `WireEstablishedLink` idempotency property (FR-007: re-invoking with the same `LinkId` yields the same handle) is a classic "idempotent registry" invariant well-suited to Rocq's `Program` + `Obligation` framework. AutoRocq is available but must be adapted off its GPT-4 dependency (per the NO-API resolution: drive tactic generation with Claude via MCP, not a fixed API).

**Primary: `lean4`.**  
Rationale: the suspension-correctness and monotone-binding properties are the heart of this seed's formal obligation; Lean 4's `Mathlib` order-theory + the APOLLO model-agnostic agentic loop is the most direct path. The TWAM/WAM-lineage precedent maps cleanly.

**Alternative when:** if the project's formal-verification track chooses Rocq for the IL-correctness work in #4/#7 (WAM compiler correctness precedent is primarily Rocq/Coq), then the idempotency proof for `GetOrEstablish` and the GC-hook correctness should be done in Rocq to share a proof context, and Lean 4 kept for the suspension/binding properties. Otherwise: "none."

### IL verification

`n/a`. This seed does not touch the IL/bytecode wire codec — it operates above the IL layer (restoring already-compiled `BytecodeProgram` objects via `_loadedPrograms`, §6.3). The IL round-trip verification (byte-parity, MLIR-dialect) belongs to seeds #4, #7 (where `ModuleTerm`-embedded `BytecodeProgram` serialization is in scope). The closest analog here is the snapshot round-trip identity metric (pragmatic), not an IL/wire byte-contract.

---

## Shapiro criteria preserved

1. **Committed-choice concurrency (no backtracking).** The resume path must not replay goal executions from the beginning; it resumes from the quiescence snapshot, honoring all committed choices already in the heap. The kill-and-restart test must verify that no previously committed binding is un-done.

2. **SRSW (single-reader/single-writer).** Re-wiring heap stream cursors to a fresh `LinkHandle` must preserve the SRSW discipline: each `InWriterAddr` is a writer cell with exactly one consumer (the In-stream reader); the re-established link must not install a second writer on the same cell. Validated by the SRSW formal gate on the resumed program.

3. **Suspension correctness.** After cursor re-wire, every goal that was suspended on `InWriterAddr` (reader-varId in `Suspended`) must remain correctly suspended and reactivate exactly when `TryApplyNext` extends the In-stream. The Lean 4 suspension-correctness proof obligation captures this. The pragmatic signal is the kill-and-restart test: a suspended goal must produce the same binding post-resume as in the continuous case.

4. **Monotone variable binding.** Restoring a snapshot and resuming must not "unwind" any binding already in the heap — the heap binding function is monotone (once a writer is bound, it stays bound). This is guaranteed by the snapshot-at-quiescence discipline (§6.3) and is the subject of the formal monotone-binding proof obligation.

5. **Three-valued unification (Success / Suspend / Fail) preserved across the restart boundary.** A goal that was `Suspended` before the kill must still be `Suspended` after restore (not `Failed` or spuriously `Succeeded`). The kill-and-restart test scenario covers this for the inbound-link-stream case.

**Embedded-switch framing:** in the embedded grassroots-logic-as-switch context, these criteria ensure that after a crash/restart, the engine's routing decisions for incoming connectivity events (external link frames) and internal OS/actor (QHSM/HSM) action dispatches are identical to what they would have been in a continuous run — the switch is semantically transparent across a restart boundary.

---

## Recommendation

Proceed with #9 as classified (MVP). The seed is well-grounded in existing `GetOrEstablish`/`WireEstablishedLink`/`TryApplyNext` infrastructure. The principal net-new work — beyond what #7/#8 deliver — is:

1. A `RewireHandle` variant (or warm-restart extension to `WireEstablishedLink`) that accepts pre-resolved heap addresses and re-arms egress + pump without the unbound-cell guard.
2. An explicit resume-sequence contract in the composition root specifying the order: restore-snapshot → re-establish-links (RewireHandle) → re-arm-egress → resume-drain.
3. A single-link kill-and-restart xUnit test as the correctness gate.

Resolve T2 (heap-address stability) by adopting verbatim-address snapshot semantics as a constraint on #7's API (Option 1) before #9 begins — this is the cheapest correctness path and avoids a remapping layer. The formal Lean 4 suspension-correctness proof can proceed in parallel on the mechanized-semantics track, but it does not gate the MVP delivery.

---

## Options for owner

1. **Adopt verbatim-address snapshot (T2 Opt 1)** and add a `RewireHandle` method to `LinkEstablish` (T1 Opt 1) — simplest correctness path, recommended.
2. **Add a stable `GlobalVarId` logical-id layer (T2 Opt 2)** — enables future heap compaction, at the cost of a remapping table in #7 and additional complexity in #9. Defer to post-#9 if not required by #15.
3. **Scope the kill-and-restart test to single-link/single-goal (U2 Opt 1)** — ships the MVP faster; broaden in a follow-up test sprint. Recommended for the MVP milestone.
4. **Defer the Lean 4 suspension-correctness proof to the formal-verification track** (seeded by #1a) — does not block the pragmatic MVP delivery; SRSW validator + kill-and-restart test are the gates.

---

## Open questions

1. Does `HeapFCP.OnBind` allow re-registering a callback on an already-bound writer (for the case where `ArmEgress` is called during resume on an address that is already bound to a stream cons)? If not, the resume sequence must walk the stream tail and arm egress on the first unbound tail writer.
2. What is the agreed "kill" in the kill-and-restart test — graceful shutdown (the `BackgroundService` cancellation path in #8) or abrupt process kill (SIGKILL/`taskkill /F`)? The distinction matters for whether the snapshot was committed before the kill.
3. For the `MonitorCursors` list (`LinkHandle.cs:51`) — are these heap addresses part of the snapshot blob in #7, or are they reconstructed by re-running `link_monitor` goals after resume?
4. Does the listen-rendezvous re-bind at boot (§6.2) use the same `TcpTransport.ListenAsync` one-accept path (which stops after one connection)? If so, the listen must be re-armed after each accepted connection — is that handled by #10 (multi-accept) or by #9 for the restart case?
5. The `InboundPump` setter on `runtime.cs:129` is set via `??=` in `WireEstablishedLink` (`LinkEstablish.cs:85`). On resume, if a fresh `LinkPump` is created (as `LinkRuntime.Pump` is), the setter must be updated to point to the new pump. Is `InboundPump` reassigned on resume, or must `GlpRuntimeEngine` be reconstructed?

---

## External refs

- `csharp/glp_link/primitives/LinkRegistry.cs:25-34` — `GetOrEstablish`
- `csharp/glp_link/primitives/LinkEstablish.cs:29-88` — `WireEstablishedLink`, `ArmEgress`
- `csharp/glp_link/primitives/LinkHandle.cs:17,21-30,35-41,51` — handle fields + cursor addresses + MonitorCursors
- `csharp/glp_link/primitives/LinkPump.cs:86-125` — `TryApplyNext`, In-stream extension
- `csharp/glp_link/primitives/LinkRuntime.cs:50` — `Pending` (ephemeral)
- `csharp/glp_link/reliability/ResourceSnapshot.cs:17-37` — `ResourceSnapshot`, `IResourceProbe`
- `out/csharp/lib/runtime/runtime.cs:129` — `InboundPump` seam
- `out/csharp/lib/runtime/runtime.cs:22-152` — `GlpRuntimeEngine` full state
- `out/csharp/lib/engine/glp_engine.cs:202-217` — cold boot sequence
- `out/csharp/lib/engine/glp_engine.cs:545` — `DrainAsyncWithStatus` quiescence point
- `out/csharp/glp_repl/Program.cs:30-35` — composition root (FR-057)
- `docs/research/multi-protocol-link-layer/corpus/06-heap-fcp-live-implementation.md:255-274` — imported-variable path / resume note
- `docs/research/repl-engine-separation/design-dossier.md` — §6.2, §6.3, §6.4, §5, §10.7, §12 risks 3+5
- [TWAM: Certifying Abstract Machine for Logic Programs](https://arxiv.org/pdf/1801.00471) — WAM-lineage verified-IL precedent
- [APOLLO: model-agnostic agentic Lean proving](https://arxiv.org/abs/2505.05758) — model-agnostic Lean tactic loop
- [Lean-LSP-MCP / Lean Copilot](https://lean-lang.org/papers/lean4.pdf) — Claude-native Lean toolchain
