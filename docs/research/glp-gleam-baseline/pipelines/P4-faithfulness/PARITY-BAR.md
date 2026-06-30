# P4 PARITY BAR — Faithfulness Criteria for a Gleam/AtomVM GLP instance

- **Feature:** 036-glp-gleam-baseline-program
- **Marathon run:** mrun-5611c436ba95
- **Pipeline / task:** P4 (faithfulness), T004
- **Date:** 2026-06-29
- **Contract:** This artifact honors `contracts/pipeline-contract.md` — every tabled criterion carries a primary-source citation; nothing uncited is asserted as a criterion (uncited / refuted-for-cause items are moved to *Faithfulness risks*, never dropped); sibling repos + corpus PDFs are read-only; "faithful = identical OBSERVABLE execution semantics vs the Dart/C# runtime" (no fastest-path rubric).

**Definition of the bar.** A Gleam/AtomVM GLP instance is *faithful* iff, for the same loaded program + goal, it reproduces the same **observable** outcomes as the Dart/C# reference runtime: deref-result · unify-verdict · activation-set · committed-binding · top-level status · (for M2) link message. Internal heap layout (addresses, tags, cell shapes) is explicitly **excluded** from parity (034 Clarification 2026-06-25); only observable outcomes are pinned. The judging method is **outcome-equivalence** (Thm 3.34 / Remark 3.35, `GLP_IMPLEMENTATION.pdf` p.10), not a transition-by-transition match — see *Faithfulness risks → judging rubrics*.

**ED-1 (fixed input).** M1 = single combined instance; its front/back seam is **bytecode-on-wire**. M2 = linked multi-instance; its seam is the **term-level** maGLP agent-link (`global_send`/`globalize`/`localize` + term codec), NOT bytecode. The two seams are confirmed distinct in live code and must stay separate.

---

## File / source legend (all paths relative to repo root `D:/bstdev/research/glp/glpnet/`)

Live Dart parity source-of-truth:
- **HEAP** = `glp_runtime/lib/runtime/heap_fcp.dart`
- **SUSP** = `glp_runtime/lib/runtime/suspension.dart`
- **SOPS** = `glp_runtime/lib/runtime/suspend_ops.dart`
- **SCHED** = `glp_runtime/lib/runtime/scheduler.dart`
- **RUN** = `glp_runtime/lib/bytecode/runner.dart`
- **ENG** = `glp_runtime/lib/engine/glp_engine.dart`
- **ANZ** = `glp_runtime/lib/compiler/analyzer.dart`
- **OPS** = `glp_runtime/lib/bytecode/opcodes.dart`
- **OPS2** = `glp_runtime/lib/bytecode/opcodes_v2.dart`
- **MADCTX** = `glp_runtime/lib/multiagent/mad_context.dart`
- **GSEND** = `glp_runtime/lib/multiagent/global_send.dart`
- **GWT** = `glp_runtime/lib/multiagent/global_writers_table.dart`
- **PAYLOAD** = `glp_runtime/lib/multiagent/payload_serializer.dart`

Live Dart parity tests:
- **BPT** = `glp_runtime/test/heap/binding_pointer_test.dart`
- **SPT** = `glp_runtime/test/heap/suspension_pointer_test.dart`
- **CTP** = `glp_runtime/test/heap/circular_term_pointer_test.dart`
- **pe** = `specs/034-glp-gleam-core-terms-and-heap/parity-evidence.md`

Normative spec / corpus primaries:
- **PDF** = `D:/bstdev/research/glp/GLP/GLP_IMPLEMENTATION.pdf` (dGLP single-agent semantics)
- **core** = `D:/bstdev/research/glp/Art-of-GLP-2025/chapters/glp_core.tex`
- **cat** = `D:/bstdev/research/glp/Art-of-GLP-2025/appendices/appendix_unification_catalog.tex`
- **spec** = `docs/glp-runtime-spec.txt`
- **cheat** = `docs/glp-cheat-sheet.md`

`PROOFS/INDEX.md` = the P4 Proof Artifact register; `proof_status` cells link there.

---

## M1 — single (combined) instance criteria

Only criteria the P4 review marked **CONFIRM** appear here. Each is observable vs the Dart/C# runtime. `proof_status` links to `PROOFS/INDEX.md` only where a constructed proof artifact exists; "— (cite only)" means the criterion is grounded by its primary cite but has no separate proof obligation discharged yet.

| id | testable statement (observable vs Dart/C#) | primary_source | proof_status |
|---|---|---|---|
| FB-M1-01 | A clause reduction runs HEAD → GUARD → BODY in order; HEAD performs NO heap mutation (builds σ̂w only); the single `inBody` flag is the phase boundary; no committed binding exists before commit | spec:279,303,409-413; PDF p.5 Def 3.10 | — (cite only) |
| FB-M1-02 | Committed-choice: clauses tried in source order, FIRST succeeding reduction commits, no backtracking / no trail | PDF p.8 Def 3.25; RUN:769-783,220 | — (cite only) |
| FB-M1-03 | Writer outputs are constructed in the clause HEAD only; an `=`/`:=` binding of a writer-mode output in the BODY is rejected | PDF p.2,p.4; cheat:9,264 | — (cite only) |
| FB-M1-04 | `commit` applies σ̂w to the heap atomically and flips `inBody=true`; one commit = one reduction | RUN:2401,2277-2447 | — (cite only) |
| FB-M1-05 | BODY goals mutate the heap only post-commit (BodySetConst/PutStructure/SetVariable bind writers, return activations) | RUN:2502-2508; OPS:46-73 | — (cite only) |
| FB-M1-06 | Each clause variable gets a sequential, positional X-register (`registerIndex = nextIndex++`) — required for bytecode-on-wire identity | ANZ:823-831 | — (cite only) |
| FB-M1-07 | Commit materializes `_TentativeStruct`/`_ClauseVar` placeholders into real `StructTerm`s (args resolved to writer or paired-reader VarRef) | RUN:2309-2447 | — (cite only) |
| FB-M1-08 | A unit-goal-vs-head reduction yields exactly one of {Success(σ), Suspend(W), Fail} (`GuardResult{success｜failure｜suspend}`) | PDF p.7 Def 3.21; RUN:42-45 | PROOFS/INDEX.md — three-valued-unify (proved) |
| FB-M1-09 | Conjunction outcome priority: any Fail ⇒ fail; else any Suspend ⇒ suspend; else succeed (union of writer assignments) | core:75 | — (cite only) |
| FB-M1-10 | Per-vertex term-match table: WxW→fail; Writer×Reader / Writer×Term→assign the writer; Reader×Reader→fail; Reader×Term→suspend; Term×Term→recurse (mismatch⇒fail) | core:60-73; PDF p.5 Def 3.8; OPS:113-117 | — (cite only) |
| FB-M1-11 | Guards are pure tests; `Otherwise` succeeds iff both Si and U are empty, else it suspends | RUN:450-460; OPS:174-177 | — (cite only) |
| FB-M1-12 | System tests `no_readers` / `=?=` are three-valued (Success/Suspend/Fail) | OPS:242-272 | — (cite only) |
| FB-M1-13 | v2 HEAD ops carry an `isReader` flag selecting writer-mode (tentative σ̂w bind) vs reader-mode (add to Si if unbound) | OPS2:24-94 | — (cite only) |
| FB-M1-14 | Writer-MGU assigns ONLY writers; readers are left unchanged | PDF p.4 Def 3.7 / Ex 3.9; core:52-58 | PROOFS/INDEX.md — writer-MGU (OPEN) |
| FB-M1-15 | Writer→writer binding (direct or discovered via deref) is reported loudly (Dart `StateError`), never silent | HEAP:274-276,671-682; core:78-82 | PROOFS/INDEX.md — writer-MGU (OPEN) |
| FB-M1-16 | Reader×Reader match fails (neither can be assigned without violating SO) | core:78-82; PDF p.5 Def 3.8 | — (cite only) |
| FB-M1-17 | `deref` returns exactly one of {Bound(Term), Unbound-local VarRef, Unbound-imported VariableEntry} | HEAP:259-336 | — (cite only) |
| FB-M1-18 | deref of a fresh (just-allocated) var = Unbound(writer) i.e. `VarRef(writerAddr)` | HEAP:307-310; BPT:167 | — (cite only) |
| FB-M1-19 | deref after bind-to-value = Bound (const or struct); embedded VarRefs preserved; deref does NOT recurse into struct args | HEAP:331-333; BPT:19,71,98 | — (cite only) |
| FB-M1-20 | deref through a bound bind-to-variable chain = Bound (final value) | HEAP:324-327; BPT:127,148 | — (cite only) |
| FB-M1-21 | deref through an UNBOUND bind-to-variable chain = Unbound(final writer) | HEAP:307-310; BPT:167 | — (cite only) |
| FB-M1-22 | Self-bind: a writer bound onward to its OWN paired reader (which points back) derefs to Unbound(w), NOT a cycle error | HEAP:312-323; pe (#11) | — (cite only) |
| FB-M1-23 | A genuine multi-hop pointer cycle (revisited addr) is caught loudly as an SRSW-violation error ⚠ boundary vs GAP-G5 | HEAP:265-266 | — (cite only); see risk GAP-G5 |
| FB-M1-24 | A structural self-reference `f(X?)` bound into X's writer derefs to the Bound struct and terminates | HEAP:331-333; CTP | — (cite only) |
| FB-M1-25 | No occurs-check: self-pairs and circular terms are formable (occurs-check infeasible) | core:166 | — (cite only) |
| FB-M1-26 | Derived `isFullyBound` / `getValue` agree with the deref three-way partition | HEAP:562-576 | — (cite only) |
| FB-M1-27 | SRSW / SO is preserved across reductions; an SRSW-violating program is rejected | PDF p.4 Def 3.3,3.4; core:40-46,162-164 | PROOFS/INDEX.md — SRSW-preservation (proved) |
| FB-M1-28 | A HEAD match needing an unbound reader SUSPENDS (reader→Si), never FAILS | RUN:760-762; PDF p.7 Ex 3.22 | PROOFS/INDEX.md — three-valued-unify (proved) |
| FB-M1-29 | Goals suspend on readers, never on writers | RUN:309; PDF p.5 Def 3.8 | — (cite only) |
| FB-M1-30 | On soft-fail, Si is unioned into U; U (goal-level set) survives across clause attempts | RUN:282-287 | — (cite only) |
| FB-M1-31 | At commit, each Si reader whose paired writer is in σ̂w is resolved/dropped; an unresolved reader → U + soft-fail | RUN:2277-2296; cat:352-369 | — (cite only) |
| FB-M1-32 | Two-phase collection prevents spurious suspension: `p(X?,X)` vs `p(a,a)` SUCCEEDS | cat:352-369 | — (cite only) |
| FB-M1-33 | At NoMoreClauses: U≠∅ ⇒ suspend(U); U=∅ ⇒ fail | RUN:2449-2468 | — (cite only) |
| FB-M1-34 | One shared SuspensionRecord per suspended goal, attached to each blocking reader's writer cell | SOPS:20-33; HEAP:467-506 | PROOFS/INDEX.md — suspension-reactivation (verified) |
| FB-M1-35 | Single-fire/disarm: a goal suspended on N readers fires EXACTLY once (value-copy port must dedupe activations by goal_id) | SUSP:1-24; HEAP:544-553 | PROOFS/INDEX.md — suspension-reactivation (verified) |
| FB-M1-36 | A disarmed suspension record yields NO activation | HEAP:544-553; SPT:72 | PROOFS/INDEX.md — suspension-reactivation (verified) |
| FB-M1-37 | Reactivation is disjunctive (any blocking reader binding); one `GoalRef(goal_id, kappa)` per armed record | PDF p.8 Def 3.24; HEAP:350-390; SPT:21,48 | PROOFS/INDEX.md — suspension-reactivation (verified) |
| FB-M1-38 | Resume PC `kappa` = procedure entry / clause 1 (wake-and-retry, not resume-at-suspension-point) | SOPS:18-19; cat:381-384 | PROOFS/INDEX.md — suspension-reactivation (verified) |
| FB-M1-39 | bind-to-variable forwards suspensions and returns `[]` now; they fire on the TARGET writer's later bind | HEAP:413-450; SPT:94,122 | PROOFS/INDEX.md — suspension-reactivation (verified) |
| FB-M1-40 | FR-008: forwarding a suspension onto an already-WriterBound target must NOT drop it — forward to the terminal writer ⚠ HEAP `forward_to_terminal` line not pinned | SPT:94,122; pe:52-78 | — (cite only); see risk RISK-CITE-1 |
| FB-M1-41 | Top-level `ExecutionStatus ∈ {succeeded, failed, suspended}` classification | SCHED:319-344 | — (cite only) |
| FB-M1-42 | Infrastructure/serve goals excluded from the verdict; `blockingReaders = rt.suspended.keys` when suspended | SCHED:322-323,340-342 | — (cite only) |
| FB-M1-43 | A terminated goal = success iff ≥1 reduction occurred, else failed | SCHED:300-314 | — (cite only) |
| FB-M1-44 | Single-goal driver: absent `$functor/$arity` label ⇒ failed "Predicate not found" | ENG:514-522 | — (cite only) |
| FB-M1-45 | Monotonicity (safety): a reducible goal stays reducible — only readers further instantiated, bindings never retracted | core:198-208 (Prop GLP-monotonicity) | — (cite only) |

**M1 CONFIRM count: 45.**

---

## M2 — linked (multi-instance) criteria

ED-1: M2's seam is the **term-level** maGLP agent-link, not bytecode. Only **CONFIRM** criteria appear here; the madGLP-protocol cluster (FB-M2-02/05/06/07/08/09/10/15/19/20/21) was **recovered 2026-06-29** by re-grounding to live `multiagent/*.dart` + `madGLP-spec.md` (the draft `corpus/` cites replaced) — see *Faithfulness risks → M2 recovered*.

| id | testable statement (observable vs Dart/C#) | primary_source | proof_status |
|---|---|---|---|
| FB-M2-01 | deref / path-compression / WxW-check stay LOCAL to one in-memory `cells[]` per side; nothing crosses the wire | HEAP:259-336 (verified local `cells[]`) | PROOFS/INDEX.md — distributed-deref-M2 (OPEN) |
| FB-M2-03 | Heap cell model is unchanged by distribution; remoteness lives only in the routing overlay, not cell tags | GWT:15-53 | — (cite only) |
| FB-M2-04 | An un-arrived remote operand reads as an unbound local reader → SUSPEND (never spurious FAIL); reactivates via the single `bindAny` seam (FR-051) ⚠ verify gate recurses into compound args | MADCTX:316,376,435 | PROOFS/INDEX.md — distributed-deref-M2 (OPEN); see risk RISK-M2-CONTEST |
| FB-M2-11 | `global_send` fires on `known/1` (NOT `ground/1`), one-shot per writer (`_goals.remove(writerAddr)`) | GSEND:134-164 | — (cite only) |
| FB-M2-12 | Per-hop link rebuild: a fresh global link for EVERY embedded variable; localize mints a fresh local pair per global name | MADCTX:190-219; GSEND:146-164 | — (cite only) |
| FB-M2-13 | Directional correspondence (v5.3): writer-globalized@p ⇒ ENTRY@p; reader-globalized@p ⇒ `global_send`@p | GWT:15-53 | — (cite only) |
| FB-M2-14 | Index allocation: one per-agent counter; index 0 reserved (permanent serializer); starts at 1; never reused | GWT:79-100,147 | — (cite only) |
| FB-M2-16 | SRSW holds per instance, as a static clause property, NOT re-checked on the wire | PDF p.4 Def 3.4; core:43-46 | — (cite only) |
| FB-M2-17 | Each global-writers-table entry: created exactly once, removed exactly once (on Receive) | GWT:211-227 | — (cite only) |
| FB-M2-18 | Single-writer: a redelivered assignment keyed by the never-reused (agent,index) is a verified NO-OP (FR-021) | MADCTX:41-49,341-344,398-402,445 | — (cite only) |
| FB-M2-02 | A maGLP shared pair `(X, X?)` globalized across instances becomes TWO local pairs + ONE global link: sender keeps its local pair and substitutes a global name (`_w/_r(agent,index)`); receiver's `localize` allocates a fresh second local pair per global name; the single link is one GWT row | `mad_helpers.dart:187-214,246-278,296-321`; `global_writers_table.dart:15-54,147-179`; madGLP-spec.md §1.1:16-20 | — (cite only) |
| FB-M2-05 | A cross-instance value arrives as a decoded `MessageType.assignment` and is applied as a LOCAL bind of the GWT-named local writer (`bindAny`+`enqueueReactivatedGoal`); thereafter it is ordinary local heap state (plain deref / suspension reactivation), explicitly NOT a special remote read | `agent_runtime.dart:302-317`; `mad_context.dart:318,376,435`; madGLP-spec.md §8.3:259-269 | — (cite only) |
| FB-M2-06 | The wire carries only serialized TERMS + ground global names — never IL/bytecode: the sole encoder emits `_tagConstant/_tagVariable/_tagStruct` (else `throw UnsupportedError`); vars become ground `creator:localId` strings; global names ride as `_w/_r(agent,index)` StructTerms; envelope = type-byte+agent+index+term; no opcode path exists | `payload_serializer.dart:407-467,196-220`; `mad_helpers.dart:308-321`; madGLP-spec.md §11.4:431-438 | — (cite only) |
| FB-M2-07 | Polarity is first-class — exactly one byte per variable (`isReaderVar?1:0`) after the global-id bytes; a transmitted WRITER additionally announces its paired reader's local id (`pairedReaderLocalId = creatorLocalId+1`); decode mirrors byte-for-byte | `payload_serializer.dart:440-441,443-451,620-630`; madGLP-spec.md §11.4:437,§15.1:668-669 | — (cite only) |
| FB-M2-08 | An imported var SHOULD keep the original creator's id across multi-hop forwarding. CONFIRM-mechanism / PARTIAL end-to-end: the codec supports it (imported vars take `(creator,creatorLocalId)` from `lookupVariable`), but live `MadContext._lookupVariableForSerialization` is a "simplified version" returning `creator: agentId` (the relayer) — so the invariant holds at the serializer layer only, not end-to-end | `payload_serializer.dart:424-434,632-644`; `mad_context.dart:180-184`; madGLP-spec.md §10.3:393-406 | PROOFS/INDEX.md — distributed-deref-M2 (OPEN) |
| FB-M2-09 | RPC-seam result is delivered as a SHARED logic variable with wake-on-bind, not request/response copy. CONFIRM-mechanism, scope caveat: intra-instance `M # goal` (`RemoteGoal`) compiles the result var into registers + `Distribute`/`Transmit` and wakes via heap suspensions; cross-instance result-as-shared-var is the global_send/`handleMadAssignment` path (ED-1), not bytecode RPC | `ast.dart:193-220`; `codegen.dart:503-535`; `heap_fcp.dart:350-390`; `agent_runtime.dart:302-317`; madGLP-spec.md §9.1:282-283 | — (cite only) |
| FB-M2-10 | The codec round-trips with structural identity: functor+arity+recursive args, global-name (type-tag+agent+index), and constants (nil/int/double/string/bool) all preserved; variable identity = isReader tag byte + ground global id | `payload_serializer.dart:452-463↔649-672,469-490↔679-714,436-441↔609-647`; madGLP-spec.md §11.4:433-438 | — (cite only) |
| FB-M2-15 | The index-0 serializer is a many-to-one merge: cold-calls wrap content in a list cell `_w(q,0):=[T↑|_w(q,0)]` and receive EXTENDS the stream + keeps the entry (never removed; `_nextIndex` starts at 1, indices never reused); index>0 links bind-then-REMOVE → one-to-one single-use | `global_writers_table.dart:63-80,211-217,125-130`; `payload_serializer.dart:235-276`; `mad_context.dart:264-330,384-386,443-445`; madGLP-spec.md §4.1:99-120 | — (cite only) |
| FB-M2-19 | Send atomicity: when a `global_send` writer is bound, globalizing the value (incl. spawning sub-`global_send` goals) and enqueueing the outbound message to `mp` happen synchronously within ONE Reduce; the network flush is the separate post-drain Send phase | `global_send.dart:131-166`; `mad_context.dart:144-176,535-599`; `agent_runtime.dart:391-395`; madGLP-spec.md §13:644,§8.1:249 | — (cite only) |
| FB-M2-20 | Transport gives per-PEER FIFO (one per-destination ordered queue, drained in order, at-most-once) plus forward-only/never-retracted delivery. CONFIRM per-peer FIFO; monotonicity EMERGENT: no retract/unbind primitive and a redelivered frame is absorbed as a verified no-op (dedup); epoch/fence sublayer documented as future | `message_queue.dart:45-66,72-86`; `mad_context.dart:97-115,343-350,41-47`; madGLP-spec.md §13:648,§9.2:287 | — (cite only) |
| FB-M2-21 | The binary maGLP Communicate is realized as THREE local unary transitions: Reduce (`drainWithStatus` to quiescence → assigns writer → triggers global_send) → Send (`flushMessages` drains `mp`) → Receive (`onMadMessageReceived`→`handleMadAssignment`→re-run), never one coupled rendezvous | `agent_runtime.dart:382-402,290-344`; madGLP-spec.md §9.2:287,§9.1:282 | — (cite only) |

**M2 CONFIRM count: 21.** (10 original + 11 recovered 2026-06-29 from live `multiagent/*.dart` + `madGLP-spec.md`)

---

## Faithfulness risks

Each item below is a first-class risk: a criterion refuted for cause, an open/refuted proof, a weak cite, a contested cite, or a corpus/lens GAP. None is silently dropped. Format: **ID** — risk — *what is needed*.

### Judging rubrics (retained as method, not as test points)
- **RISK-RUBRIC-M1 (FB-M1-R1)** — Outcome-equivalence (Thm 3.34 / Remark 3.35, PDF p.10) is the governing M1 comparison method, not an observable per-run criterion. *Needed:* keep it as the judging rule for the whole M1 table; do not list it as a discrete criterion.
- **RISK-RUBRIC-M2 (FB-M2-R1)** — Theorem 5.7 N-agent correctness is the governing M2 yardstick, not a test point; its supporting lemmas C.41/C.45 are truncated in the arXiv HTML. *Needed:* re-ground the proof from a complete primary (arXiv 2602.06934 App. C source / `docs/ma/madGLP-spec.md`) before relying on it; meanwhile judge M2 per-invariant.

### Open / refuted proof obligations
- **RISK-PROOF-writerMGU** — `writer-MGU binds only writers` (FB-M1-14, FB-M1-15) has **no constructed proof** (outcome OPEN); only a code-cited invariant + the 034 parity audit exist. *Needed:* author `WriterMguBindsOnlyWriters.lean` modelling bindWriter/bindWriterToReader/bindWriterToWriter, discharge via the Lean harness (`spikes/lean/run.sh`), record under `PROOFS/writer_mgu_binds_only_writers/`.
- **RISK-PROOF-distDeref** — `distributed-deref M2 faithfulness` (FB-M2-01, FB-M2-04) is **OPEN**: only a minimal single-message front↔back SPIN handshake passes; no Promela model of the multi-message, bidirectional, suspend/reactivate deref protocol with a no-lost/no-duplicated-binding safety property exists. *Needed:* build the full Promela model (5 steps recorded in `PROOFS/INDEX.md` note), model-check `errors: 0`, record outcome.

### Weak / contested live cites
- **RISK-CITE-1** — FB-M1-40 (FR-008 forward-onto-WriterBound not dropped) cites SPT + pe but the `HEAP.forward_to_terminal` line is **not pinned**. *Needed:* pin the exact `heap_fcp.dart` line for `forward_to_terminal` / the terminal-writer deref on forward.
- **RISK-M2-CONTEST** — FB-M2-04 rests on a `mad_context.dart` snapshot that the 2026-06-06 decision doc contests (it claimed "no dedup/FIFO/fence machinery"; live code since added FR-021 dedup + FR-035 `bindAny`). The universal gate historically did NOT recurse into compound args, and an `allocateImportedReader` reader's suspension reactivated only via a path the assignment never called. *Needed:* re-verify against CURRENT `heap_fcp.dart`/`mad_context.dart` that the gate recurses into compound args and that an imported writerless reader reactivates its suspended goals.

### M2 recovered (was REFUTE-as-cited; re-grounded 2026-06-29 — RESOLVED)
All 11 (FB-M2-02/05/06/07/08/09/10/15/19/20/21) were re-grounded to live `multiagent/*.dart` — the parity source-of-truth: `payload_serializer.dart`, `global_writers_table.dart`, `global_send.dart`, `mad_context.dart`, `mad_helpers.dart`, `agent_runtime.dart`, `message_queue.dart` — plus `madGLP-spec.md` sections, replacing the draft `corpus/` cites, and adversarially re-confirmed 11/11. They are now in the M2 table. Honest residual scope caveats (carried in their rows, NOT dropped):
- **FB-M2-08** — original-creator-id holds at the SERIALIZER layer only: live `MadContext._lookupVariableForSerialization` is a "simplified version" returning the relayer's id, so end-to-end multi-hop preservation is unproven — folded into the distributed-deref OPEN proof (RISK-PROOF-distDeref).
- **FB-M2-09** — mechanism-confirmed; intra-instance `#` is bytecode RPC, cross-instance result-as-shared-var is the global_send path (ED-1) — not a single unified RPC.
- **FB-M2-20** — per-peer FIFO confirmed; monotonicity is EMERGENT (no retract primitive + dedup no-op), and the epoch/fence sublayer remains future (see FB-M2-R2).

### Refuted (recommendation / unsettled — no Dart/C# behavior to compare yet)
- **FB-M2-R2** — epoch/fencing token for a CONFLICTING double-bind (stale+reconnected writer, different values) is a recommendation, NOT in code. *Needed:* a design decision (owner) + implementation before it can be a parity criterion.
- **FB-M2-R3** — wire-framing hardening (version byte / outer CRC / fragmentation / two opposite polarity-byte conventions / in-band `#serializer:` sentinel) is unsettled in the live format; no normative spec fixes it. *Needed:* an M2 codec framing spec settling each.
- **FB-M2-R4** — long forwarding-chain A→B→C…→N end-to-end parity: no enumerated-chain / local N-instance correctness theorem exists. *Needed:* a chained-forwarding proof (or reliance on "each hop is the same unary mechanism" recorded as an explicit assumption).

### Corpus / lens GAPS (load-bearing invariants absent from BOTH parity lenses)
- **GAP-G1 (M1, strong)** — `ground/1` SRSW relaxation: the single-*reader* constraint is relaxed for a ground `X?`; multiple ground reader occurrences are legal. A clause using them must LOAD + RUN, not be rejected. *Source* core:139. *Needed:* add as an M1 criterion + a load/run test; ensure the Gleam SRSW checker honors the relaxation.
- **GAP-G2 (M1, strong)** — clause-head renaming / standardize-apart per reduction: each clause use matches a freshly-renamed head; recursion must not cross-bind sibling invocations. *Source* PDF p.5 Def 3.10; core:118. *Needed:* add as a criterion + a recursion non-aliasing test.
- **GAP-G3 (M1, strong)** — fairness/liveness: a perpetually-reducible goal is eventually reduced (distinct from monotonicity-safety FB-M1-45). *Source* core:112. *Needed:* a scheduler liveness obligation/test.
- **GAP-G4 (M1, proof-phase)** — computed answer is a logical consequence: `(G0:-Gn)σ` is a logical consequence of `L(M)` (soundness, not a per-run observable). *Source* core:153-157 (Thm GLP-computation-deduction). *Needed:* P4 faithfulness-proof obligation.
- **GAP-G5 (M1/M2, strong + TENSION)** — cross-goal communication-formed circular terms must be handled gracefully (occurs-check infeasible across agents); `p(X?,X)` with `p(X,f(Y?)), p(Y,f(X?))` legitimately forms `X=f(f(X?))`. This **conflicts** with FB-M1-23 (HEAP:265-266 loud cycle error). *Source* core:166. *Needed:* surface the boundary — which cross-goal cycles are legal-and-graceful vs which are the SRSW-violation loud-fail; reconcile FB-M1-23 vs FB-M1-24/FB-M1-25. **(Genuine fork — see below.)**
- **GAP-G6 (M2, strong)** — distributed top-level status / quiescence oracle: neither lens (nor any on-disk primary) defines how a LINKED N-instance run's overall succeeded/failed/suspended is classified. Required for "M2 linked parity". *Needed:* define + ground a distributed quiescence/termination oracle before M2 parity is testable.
- **GAP-G7 (M1, minor)** — suspension-set MINIMALITY: Def 3.21 suspends with W *minimal*; lenses pin "suspend on readers" but not minimality of the reported set. *Source* PDF p.7 Def 3.21. *Needed:* decide whether minimality is an observable parity requirement or implementation latitude.
- **GAP-G8 (M1, minor)** — guard-predicate coverage: only `no_readers`/`=?=`/`Otherwise`/`ground` are pinned; the rest of the three-valued guard set (`=:=`, `<`, `==`/`\==`, type tests, `known`) is unspecified for parity. *Source* cheat §guards. *Needed:* enumerate + pin the full guard three-valued behavior.

---

## Genuine forks (owner options)

Only one decision is genuinely open at the criterion level (everything else above is "do the work", not "choose a direction"):

- **FORK-1 — circular-term boundary (GAP-G5 vs FB-M1-23).** The book mandates graceful handling of cross-goal-communication-formed circular terms (core:166), while the live deref raises a loud SRSW-violation error on a revisited address (HEAP:265-266). These can coexist (the loud error is for a *genuine multi-hop pointer cycle*; the graceful case is a *cross-goal term* `X=f(f(X?))`), but the exact boundary is not pinned in code or spec.
  - *Option A:* Treat all deref-visited-set hits as the loud SRSW error (current Dart behavior); declare cross-goal circular terms out-of-scope for M1 single-instance and defer to M2. Risk: may be unfaithful to core:166.
  - *Option B:* Distinguish structural/cross-goal circular terms (graceful, terminate at the Bound struct per FB-M1-24) from genuine pointer cycles (loud error); pin the discriminator in both Dart and Gleam.
  - *Recommendation:* Option B, with a constructed test corpus exercising `p(X,f(Y?)), p(Y,f(X?))`, before either runtime is declared faithful — but this is an owner call (read-only until the migration gate; FR-010/FR-011).

All other open items have a determined direction (build the proof / pin the cite / re-ground from the primary spec); they are tracked as risks above, not forks.
