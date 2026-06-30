# CHOSEN APPROACH — Gleam/AtomVM GLP Baseline (M1 single-instance → M2 linked)

**Single source of truth for all downstream per-feature analysts.** This document resolves the three competing approaches. The spine is **Approach 3 (parity-first)** — ranked 1st — with two load-bearing grafts: the **FrameCodec + TLV byte-parity wire spine** from **Approach 2**, and the **deep-resolve / output-capture fold-in** from **Approach 1**. Port source is ratified **Dart** (A2); C# is the cross-runtime wire-parity oracle only, never the port basis.

---

## 1. END-STATE ARCHITECTURE

### M1 — single combined instance (in-process, one node)

**Decisive choice (Approach 3): M1 is IN-PROCESS — no REPL/engine process split.** The owner's M1 is "a single combined GLP instance, as in Dart and C#," both of which run engine+front-end in one process. Approaches 1 and 2 imported the engine-separation process split into M1; that is rejected — it adds a message-copy/heap-index-leak hazard and buys zero execution-semantics fidelity (critique SHOULD AVOID).

Layering follows FR-057 (A1): a pure-compute **engine core** with zero reference to console/transport/link:
- **F4 `runtime/`** (shipped) — immutable threaded binding store, terms, writer-MGU `unify`, suspension storage.
- **F5 `bytecode-runner`** — owns the scheduler, goal-queue, suspension index, and the three-phase HEAD→GUARD→BODY runner.
- **F6 `compiler+loader`** — SRSW→partial-eval→typecheck→compile→load.

The **REPL front-end (F7)** is a thin client: read-dispatch loop, colon-commands (`:trace`/`:limit`), and all result display. It **calls the engine directly as a typed Gleam value** — no wire, no FrameCodec, no result-envelope codec on the M1 path.

**Fold-in (Approach 1):** the result-envelope **field-set** (`status` / `bindings` / var-name→`GlobalVarId` map / suspended-goal detail / captured-output blob / errors) becomes the engine's **return type** (#11), and **server-side deep-resolve** runs inside the engine's result producer so no live heap address ever escapes — equally fatal for Gleam's immutable heap addrs as for C#'s `VarRef`. Output/trace routes through a capture seam (#10) into the blob. These are kept *ready* but un-wired: the moment any cross-process or cross-runtime boundary appears, they are already mandatory.

**Concurrency substrate:** the runner is plain sequential BEAM code (AtomVM-safe). Any cell/goal/loader spawning uses **raw `erlang:spawn` externals + `gleam_erlang` Subjects** — never `gleam_otp`/`proc_lib`. The scheduler **dedupes activations by `goal_id`** (carried 034 obligation — the immutable value-copy heap does not preserve the cross-writer single-fire guard).

### M2 — multiple linked instances

Each instance = a complete M1 engine (eventually its own AtomVM/BEAM node), joined by **global links, never remote pointers** (A3/CGLP §7). Per link: **one BEAM process pair** (sender/receiver per direction). Per-link FIFO — the precondition of the commutativity theorem (Lemma 5.7) — is supplied free by BEAM message ordering between two processes.

- **Wire spine (Approach 2 + Approach 3):** the GLP **big-endian recursive TLV term format, byte-for-byte** (tags 1=const/2=var/3=struct, const subtypes, per-variable polarity byte + paired-reader localId, varint codec, `gid=creator:localId`, index-0 cold-call sentinel) — **NOT** BEAM `term_to_binary` (that breaks byte-parity). **Riding the 025 FrameCodec frame envelope** (version byte, 22-byte header, CRC-32, MTU fragmentation, 64 MiB guard). **Both** the term format AND the frame envelope must match for the #5 gate — this is Approach 2's uniquely load-bearing insight, re-instated against Approach 3's drop.
- **Distributed unification = deferred local assignment:** a bind crosses as an assignment message (`_w(p,i):=T`) applied by an ordinary local `assign`; writer-MGU, three-valued suspend/reactivate run **locally each side**. **Deref / WxW / path-compression NEVER cross the wire** (A3 corpus 16) — reusing F4 unchanged.
- **globalize/localize on the `known/1` seam** (not `ground/1`), per-hop, minting a fresh local pair per global name + global-writers routing, so open structures travel as ground global-name placeholders.
- **Fault-as-data:** `erlang:monitor` is the *mechanism only*, used to mint a bound `tempFail`/`permFail` term on an `ok/tempFail/permFail` monitor stream. BEAM's auto-propagating `link`/`EXIT` is **forbidden** (FR-044). Plus reliability sublayer (seq/dedup, idempotent redelivery, reorder), epoch/fencing token, global-name idempotency.
- **Transport seam** (`open`/`send-bytes`/`recv-bytes`/`close+fault`): **in-process/loopback first** to hit SC-001 (byte-identical split), then real TCP via AtomVM `gen_tcp` externals.
- **N-client server:** the GLP `serve/2`+`mwm` control program (#29) from `self.glp`, folded into #36 for >2-instance topologies.

---

## 2. BEAM/AtomVM CONSTRAINTS & NATIVES

**BEAM gives for free** (the epic's tailwind, A2): lightweight processes, per-process mailboxes, **per-link FIFO ordering** (transport precondition for the commutativity theorem), process-per-link concurrency, supervision, monitors, and optional distribution. A single-assignment FCP variable maps naturally onto a process (one writer binds; readers observe) — a natural fit for SRSW + suspension. **Plain BEAM is the test runtime**; `gleam_otp`/`gleam_erlang` are fully available there.

**AtomVM does NOT give** (A2, precisely localized): **`proc_lib` is absent** → `gleam_otp` (gen_* actors) and even `gleam_erlang`'s `process.spawn` crash (`module proc_lib cannot be resolved`). Proven workaround (AtomVM host build v0.6.6): spawn via **raw `erlang:spawn` externals + Subjects**, byte-identical to Erlang. A WAM-style bytecode interpreter is plain sequential BEAM code — fine on AtomVM; only the *spawn primitive* needs the raw form. `gleam_otp` is **intentionally excluded** from deps. Targets: BEAM viable, AtomVM viable (host), JS partial (compute only; concurrency engine needs rewrite). Toolchain: Gleam 1.17.0 / OTP 25.3.2.8 / rebar3 3.19.0, **WSL Ubuntu only**.

**Supersede-by-BEAM is legitimate ONLY for operational layers** (host, process-split, multi-accept, supervision, crash-restart, cooperative scheduling, mailbox). It is **never** legitimate for GLP semantics: the **wire format** (BEAM's own binary breaks byte-parity), **distributed unification / writer-MGU / three-valued suspend** (BEAM has no logic vars), **globalize/localize**, **bind-once monotonicity / dedup / epoch-fencing**, and **fault-as-data** (BEAM's auto-propagating EXIT is exactly what FR-044 forbids).

**🔴 Open risk (all three approaches assumed it; critique flags it):** `erlang:monitor` availability on AtomVM is **unverified by the ground docs**. An explicit early spike (Step M2-0 below) must confirm it before the M2 fault model is committed.

---

## 3. FAITHFULNESS PARITY BAR

**Faithful = identical OBSERVABLE outcomes** (deref result, three-valued unify verdict, activation set produced on bind, goal suspend/terminate result) vs the **Dart source-of-truth** — explicitly **NOT** internal heap layout (A4, spec-034 Clarif.).

### M1 bar (single-instance — A4 PC-1…PC-15)

1. **Term model** (PC-1): constants/struct/list/var-ref, inspectable + equality-comparable; `ModuleTerm`/`MutualRefTerm` out of M1 core.
2. **Three-phase HEAD→GUARD→BODY** (PC-2): HEAD pure (no heap mutation pre-`commit`), only extends σ̂w + Si; BODY mutation begins at `commit`.
3. **Head two-phase unify** (PC-3): collect tentative writer binds → resolve (drop readers whose writers got bound) → success or suspend on remainder.
4. **SRSW** (PC-4): runtime fails loudly on WxW; anonymous `_` exempt.
5. **Three-valued unification** (PC-5): match→Success; mismatch→Fail; **needed unbound reader→Suspend, never Fail** (the single most common correctness error).
6. **Writer-MGU asymmetry** (PC-6): binds only writers; readers verified not bound; **never writer→writer** (loud at bind AND deref); no occurs-check; single-assignment.
7. **Deref + path compression** (PC-7): role from cell **tag, never address arithmetic**; compression preserves logical value.
8. **🔴 Bidirectional self-bind ⇒ Unbound** (PC-8): writer pointing at its own paired reader = `Unbound`, NOT a value, NOT a cycle error (034 fix `728759ae`, aligns `heap_fcp.dart:312-323`).
9. **Structure completion** (PC-9): complete only when `argsProcessed ≥ arity`.
10. **Suspension storage on the WRITER** (PC-10); **reactivation = wake-and-retry from kappa** (PC-11, procedure entry not suspension point).
11. **Var-to-var bind forwards, doesn't activate** (PC-12); **🔴 forward-to-terminal** must deref to terminal unbound writer or armed suspensions silently drop (034 fix).
12. **🔴 Guard single-fire / goal_id dedupe** (PC-13): goal suspended on N writers reactivates once — F5 MUST enforce in the scheduler (immutable value-copy loses the shared-record guard).
13. **Committed-choice / no-trail** (PC-14); **circular-term termination** (PC-15).

F4 already covers PC-1,5,6,7,8,9,10,12,13. **PC-2,3,11,14 are F5** — the gating remaining work. **M1 done = the ported corpus is 100% green on BEAM with no `gleam_otp`.**

### M2 bar (linked — A3)

- **Distributed unification = deferred local assignment**; writer-MGU/three-valued/suspend/bind-once run locally; correctness rests on SRSW→disjoint-writers→commutativity (per-link FIFO required).
- **Deref/WxW/compression strictly local** — making any of them span the wire is a defect.
- **Ground-only transport, per-hop globalization** on `known/1`; fresh local pair per global name each hop.
- **Byte-identical TLV term format + FrameCodec frame envelope**, validated against the same adversarial corpus producing identical verdicts cross-runtime (FR-031).
- **Distributed deref** terminates at the boundary cell (imported reader suspends).
- **reactivate-exactly-once across a link**; fault-as-data lattice; epoch/fencing.
- **M2 done = byte-identical split (SC-001) + executed C#↔Gleam round-trip with identical corpus verdicts.**

---

## 4. ALIGNMENT RUBRIC — the six dispositions

Apply in order; first match wins.

1. **aligned** — a Gleam feature that produces or unblocks the M1/M2 faithfulness EVIDENCE and sits on the critical path. Build in Gleam as-is.
2. **fold-into-gleam** — a C#-prep whose *contract* (envelope field-set, deep-resolve, output sink, frame-envelope) ports but has no standalone existence on BEAM. Absorb into the host Gleam feature; no separate feature.
3. **supersede-by-beam** — its deliverable is an OS/transport/host/process-split/multi-accept/supervision/scheduling **operational mechanism** BEAM/OTP provides natively. Drop the C# mechanism; keep any *classification/concept*. **Never** applies to GLP semantics.
4. **keep-cross-runtime** — needed only because C#↔Gleam interop demands byte-parity (the #5 gate, parity corpus, shipped C# oracles). Validate Gleam against it; do not port now.
5. **realign(defer)** — real future value (persistence/resume/IL-on-wire) off the M1/M2 faithful-semantics path; re-express Gleam-native later, retain the cheap classification contract.
6. **drop** — alternative substrate or non-gating research spike orthogonal to the Gleam baseline; revisit only post-baseline.

### Per-feature disposition

| id | feature | disposition |
|---|---|---|
| 2 | engine-review-and-design-dossier | **keep-cross-runtime** (shipped→**released**; contract oracle) |
| 11 | result-envelope-and-deep-resolve | **fold-into-gleam (#6)** |
| 10 | structured-output-capture-seam | **fold-into-gleam (#6)** |
| 4 | il-codec-spike | **drop** (off path; shipped→**released** as reference) |
| 15 | result-codec-and-framecodec-ride | **fold-into-gleam (#36)** — frame-envelope parity becomes a #36 sub-req (re-instated vs Approach 3's drop); envelope-on-wire = realign(defer) |
| 13 | repl-engine-process-split-mvp | **supersede-by-beam** (M1 stays in-process) |
| 20 | engine-state-snapshot-and-persistence-api | **realign(defer)** (keep classification; not hard-dropped) |
| 30 | liveness-crash-restart-host | **supersede-by-beam** |
| 18 | restore-and-resume-with-link-reestablish | **realign(defer)** (restart is BEAM-native; resume rides #20/#36) |
| 21 | multi-accept-transport-extension | **supersede-by-beam** |
| 23 | compiled-il-on-the-wire + factor-out-compiler | **drop** (source on wire; compiler engine-side) |
| 17 | antlr4-shared-grammar-spike | **drop** |
| 29 | multi-client-control-program-in-glp | **realign** — fold into #36 for N-client M2 |
| 28 | cpp-engine-feasibility | **drop** |
| 33 | shared-static-memory cooperative scheduling | **supersede-by-beam** |
| 16 | research-programme + llvm-feasibility | **drop** |
| 26 | glp-gleam-bytecode-runner | **aligned (CRITICAL, M1)** |
| 27 | glp-gleam-compiler-and-loader | **aligned (CRITICAL, M1)** |
| 6 | glp-gleam-repl | **aligned (CRITICAL, M1 milestone)** — absorbs #11/#10 |
| 8 | glp-test-corpus-port-and-runner | **aligned (CRITICAL, M1 gate)** |
| 36 | glp-gleam-link-layer | **aligned (CRITICAL, M2)** — absorbs #15 frame-envelope + #29 serve/2 |
| 5 | cross-runtime-csharp-gleam-distributed-tests | **keep-cross-runtime (M2 capstone)** |
| 030 | marathon-refinement | shipped→**released**; the harness wrapping the heavy #26/#36 marathons (not a port candidate) |

---

## 5. DRAFT TWO-MILESTONE ORDERING

**Step 0 — roadmap hygiene (minutes, non-gating):** mark **#2, #4, #030 released**; **promote #26 `refined`→`specified`**. Fold #11+#10 into #6's spec; fold #15 frame-envelope + #29 into #36's spec; record #13/#21/#30/#33 as superseded-by-BEAM; park #20/#18 realign-deferred; drop #23/#17/#28/#16.

### CRITICAL PATH to M1 (`★` = critical)

1. **★ #26 bytecode-runner** (F5; heavy/marathon under #030) — three-phase HEAD/GUARD/BODY over F4's immutable heap; scheduler consuming activations; **enforce `goal_id` dedupe (PC-13)**; carry 034 fixes (self-bind⇒Unbound PC-8, forward-to-terminal suspension-drop). Gates PC-2/3/11/14.
2. **★ #27 compiler+loader** (F6) — SRSW→PE→typecheck→compile→load; reuse Dart/C# golden bytecode for parity; loader spawn via raw `erlang:spawn`.
3. **★ #6 REPL** (F7) — load/goal/`:trace`/`:limit`; **in-process** engine; **absorbs #11 envelope-as-return-type + deep-resolve and #10 output capture**. ⇒ **M1 single combined instance reached.**
4. **★ #8 test-corpus** (F8) — port A4's PC-1…PC-15 corpus; 100% agreement with recorded Dart outcomes, green on BEAM, no `gleam_otp`. ⇒ **M1 LOCKED (corpus-green = M1).**

### CRITICAL PATH to M2

- **★ M2-0 spike (early, gating):** verify **`erlang:monitor` on AtomVM v0.6.6** before committing the fault model (critique SHOULD AVOID assuming it). Fallback: raw-spawn fault-monitor.
5. **★ #36 link-layer** (F9; heavy/marathon under #030), in evidence order:
   - 5a. **TLV term codec (byte-for-byte big-endian) + FrameCodec frame envelope** + globalize/localize on `known/1` + global-writers routing.
   - 5b. **in-process/loopback transport** + distributed-bind-as-local-assignment ⇒ hit **SC-001 byte-identical split first**; then `gen_tcp`.
   - 5c. **reliability sublayer + fault-as-data monitor** (`ok/tempFail/permFail`) + epoch/fencing; **fold in #29** (`serve/2`+`mwm`) for N-client.
6. **★ #5 cross-runtime C#↔Gleam** (F10, capstone) — one role-parameterized program split across a C# instance and a Gleam instance over the shared FrameCodec+TLV spine; identical adversarial-corpus verdicts both runtimes. ⇒ **M2 capstone reached.**

**Non-blocking (post-baseline, under #030):** #20 Gleam-native snapshot (after #6); #18 resume/link-reestablish (after #20+#36); #15 result-envelope-on-wire (only if a cross-process REPL seam is wanted).

**Net:** four features to M1 (#26→#27→#6→#8), one spike + two features to M2 (M2-0→#36→#5). All thirteen C#/spike features are off the path — dropped, superseded by BEAM, or folded into the six Gleam features as contracts. **Parity evidence, not feature count, defines the baseline.**