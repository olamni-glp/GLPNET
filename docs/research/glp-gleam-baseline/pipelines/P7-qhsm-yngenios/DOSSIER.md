# P7 — QHSM / YngeniOS Packaging DOSSIER

**Feature:** 036-glp-gleam-baseline-program
**Run:** mrun-5611c436ba95
**Task:** T010 (P7 — QHSM / YngeniOS packaging design)
**Date:** 2026-06-29
**Contract honored:** `docs/research/glp-gleam-baseline/contracts/pipeline-contract.md` — every claim cites file:line / page / URL or is marked PROVISIONAL; READ-ONLY on all sibling repos (`qhstate`, `qhstate-Yngenios`, `mstack-coop`, `olamnit`, `GLP`); inaccessible/ambiguous sources reported as gaps, never fabricated; nothing written outside `docs/research/glp-gleam-baseline/`.

---

## 0. Scope of this dossier

This dossier answers one question: **how do we package the combined single-instance Gleam GLP runtime (the M1 parity target) as a QH state machine (QHSM), and how does that QHSM plug into the YngeniOS active-object microkernel?** It proposes a concrete, grounded packaging design plus an owner-gated fork (active-object vs port/FFI), and it isolates everything resting on absent sources into a clearly-marked PROVISIONAL section.

This is a research/verification artifact. Per FR-010/FR-011 the program is **read-only on the live roadmap/specs/code and all sibling repos until the owner migration gate**; nothing here moves the live roadmap.

---

## 1. WHAT is being packaged — the combined Gleam instance

The unit to wrap is the **M1 single-instance combined Gleam GLP runtime** (ED-1), which is a direct image of the dGLP machine and therefore already *is* a state machine:

- **It is a deterministic state machine.** The dGLP machine is deterministic-FIFO with **exactly one enabled transition per non-terminal configuration**; the configuration is the triple `(Q, S, F)` and the transition on the head goal is exactly one of **Reduce / Suspend / Fail** (`pipelines/P6-gleam-impl/DOSSIER.md:29`, citing `GLP_IMPLEMENTATION.pdf` p.8 Def 3.23/3.25, Remark 3.26). That single-enabled-transition determinism is precisely the QHSM fit.
- **Back end = the scheduler-actor engine.** One BEAM/AtomVM process threading `#(heap, Q, S, F)`; goals carried as plain data values `#(goal_id, κ, call_env)` in a FIFO `Q`, with **zero spawns** on the reduction path, so the whole reduction loop is sequential BEAM code and `gleam_otp`/`proc_lib` (forbidden on AtomVM) never touch the M1 path (`pipelines/P6-gleam-impl/DOSSIER.md:29-31, :150`). Suspension/reactivation are pure functions over the immutable heap value (`pipelines/P6-gleam-impl/DOSSIER.md:31`).
- **Heap = the F4 immutable threaded two-cell store (ED-2), kept unchanged** (`pipelines/P6-gleam-impl/DOSSIER.md:77, :151`). It serializes as a *value*, the single mechanism serving BOTH M1 determinism AND the M2 seam (`pipelines/P6-gleam-impl/DOSSIER.md:85, :151`).
- **Front end = the ratified seam pipeline.** ANTLR grammar → AST → 4-primitive front-end IL (+verifiers) → frozen **v2.16.3 bytecode** → engine, with **bytecode-on-wire as the front/back seam** (CLAUDE.md feature-036 plan block; `pipelines/P6-gleam-impl/DOSSIER.md:10`, ED-2 — the engine runs the v2.16.3 ISA). IL never crosses the seam.
- **Output verdict:** top-level `ExecutionStatus ∈ {succeeded, failed, suspended}`, with `blockingReaders = suspended.keys` (`pipelines/P6-gleam-impl/DOSSIER.md:65`, citing the parity bar FB-M1-41).
- **The M2 term-link seam (ED-1) is a SEPARATE seam, not part of M1.** BEAM processes + `gleam_erlang` Subjects appear ONLY at the M2 inter-instance link — bytecode-on-wire / term-level, **NOT disterl** (`epmd`/`disterl` unsupported on AtomVM), via the raw `erlang:spawn` + Subjects primitives F1 proved (`pipelines/P6-gleam-impl/DOSSIER.md:33, :112, :155`). The REPL cannot run on AtomVM, so for M1 the REPL stays on the Dart/BEAM host while AtomVM runs the engine (`pipelines/P6-gleam-impl/DOSSIER.md:112`).

**Packaging unit, in one line:** a single-process Gleam engine whose internal control is a fixed deterministic state machine (Reduce/Suspend/Fail over `(Q,S,F)` + immutable two-cell heap), fed by the grammar→AST→IL→v2.16.3-bytecode front seam, emitting `{succeeded|failed|suspended}` — with the M2 term-link as the only place processes/Subjects are introduced.

---

## 2. The QHSM packaging model (the wrapper)

### 2.1 Canonical QHSM model — what an Active Object packages

In the Quantum Platform (QP, Miro Samek / Quantum Leaps) model, the unit of encapsulation is the **Active Object (AO)** — class `QActive`, derived from the hierarchical state-machine base `QHsm`. An AO bundles exactly four things, and that bundle is the canonical wrapper around any reactive entity:

1. **A private event queue** — SRS_QP_AO_20: "Active Object abstraction shall provide an event queue for each Active Object instance"; capacity run-time configurable (SRS_QP_AO_23) (https://www.state-machine.com/qpc/srs-qp_ao.html).
2. **A private execution context (thread), optional** — SRS_QP_AO_30 (same URL) — optional because the same AO runs unchanged over a blocking RTOS thread or a non-blocking built-in kernel.
3. **An internal hierarchical state machine** — SRS_QP_AO_70: "Each Active Object instance shall have an internal state machine…" (same URL).
4. **Private, encapsulated data** — SRS_QP_AO_50/51 (same URL).

Posting is FIFO from outside, LIFO for self-posts (SRS_QP_AO_21/22, same URL). Processing is strictly **run-to-completion (RTC)**: "every event is processed to completion … during the RTC step, the current event remains available and unchanged" (SRS_QP_AO_60, same URL). Consequence — atomicity without locks: "As long as there is no sharing of data or resources among Active Objects … there are no concurrency hazards" (https://www.state-machine.com/qpc/struct_q_active.html). Inter-AO communication is asynchronous: direct FIFO `QActive_post_()` or publish-subscribe `QActive_publish_()`, never shared variables (https://www.state-machine.com/qpc/srs-qp.html, https://www.state-machine.com/qpc/struct_q_active.html).

### 2.2 The qhstate realization of that model

`qhstate` carries the model as QP/C 8.1.4 reference C plus faithful Python and C# ports:

- **HSM core:** a state is a function pointer returning an RTC verdict (`QStateHandler`, `include/qp.h:146-147`); event = signal + payload (`include/qp.h:99-105`); verdicts `Q_RET_SUPER/UNHANDLED/HANDLED/TRAN/…` (`include/qp.h:191-203`); reserved signals `Q_EMPTY/ENTRY/EXIT/INIT_SIG` (`include/qp.h:206-209`); class hierarchy `QAsm → QHsm → QActive` (`include/qp.h:183-220, 244-246, 452-468`).
- **RTC dispatch (QEP):** `QHsm_dispatch_` bubbles up via `Q_EMPTY_SIG` superstate probes until handled, then exits to source, computes LCA, enters to target, drills the nested-init chain (`src/qf/qep_hsm.c:241-340, 271-287, 309-330`).
- **The wrap unit, QActive:** `QHsm` + priority + an asynchronous FIFO mailbox (`include/qp.h:452-468`); posting `QActive_post_`/`postFIFO_` (`src/qf/qf_actq.c:50-118`).
- **The C# port — "the OS":** `Csharp/runtime/Qp/QActive.cs:11-23` = `QActive : QHsm` + `int Prio` + `Queue<QEvt>` FIFO mailbox; `QF` is a single-threaded scheduler draining mailboxes in descending priority, **one event (one RTC step) per pass, to quiescence**, deterministic, no threads, no wall clock (`Csharp/runtime/Qp/QActive.cs:28-62, 44-54`). The Python sibling confirms the contract (`codeconv/src/codeconv/runtime/qp/qactive.py:1-56`).

### 2.3 The wrapper design for the Gleam instance

Wrap the M1 engine as **one QActive** (`QHsm` + priority + FIFO mailbox):

- **Internal HSM = the engine lifecycle:** Booting → Idle/Suspended *(Quiescent)* ⇄ Reducing → Terminated, with behavioral inheritance for cross-cutting STOP/FAULT/STATUS handling living in a superstate (QP behavioral inheritance: SRS_QP_SM_35/37, https://www.state-machine.com/qpc/srs-qp_sm.html).
- **🔴 Load-bearing structural fact:** the GLP goal-queue `Q` is **internal encapsulated engine state, NOT the QActive mailbox**. The QActive mailbox carries only boundary events (`RUN / INBOUND_TERM / LINK_* / SNAPSHOT / STOP / STATUS / TICK`). **One QActive RTC step = receive one boundary event → run the inner reduction to quiescence (`Q = ∅`).** RTC means no boundary event interleaves a drain — which discharges the parity-bar FB-M1-35 single-fire concurrent-disarm hazard at the boundary for free. The remaining generation-scoped dedup — key `(goal_id, suspension_generation)`, NOT bare `goal_id` — lives *inside* the Reducing drain (F5; `pipelines/P6-gleam-impl/DOSSIER.md:47`).
- **Safe pause/migrate coincides with Quiescent.** The kernel's safe pause/migrate point (Drain-to-quiescence) coincides exactly with the engine's `Quiescent` states (`Q = ∅`) — the only instant the immutable heap is a clean serializable **value**, the single ED-1 mechanism serving both persistence and the M2 seam (`pipelines/P6-gleam-impl/DOSSIER.md:85`). `SNAPSHOT`/migrate is therefore legal precisely in `Quiescent`.

### 2.4 The owner fork — two embeddings of the wrapper

**Option A — Active-Object (engine re-expressed as a rich QHSM/QActive).**
- *Pros:* finer kernel observability (engine internals surface as kernel events); on **plain BEAM** an in-process zero-impedance wrap (BEAM mailbox = QActive queue, BEAM receive-loop = the RTC `Dispatch`, BEAM messages = `QEvt`s); and uniquely **kernel-composable GLP-commit** — `olamnit/Olamnit/Olamnit.Kernel/Glp/GlpUnit.cs:1-20` two-level commit lets a GLP clause-commit fold into a kernel-level/distributed transaction ("QHSM transition ≡ GLP clause success ≡ same commit point").
- *Cons:* on AtomVM the in-process selling point is factually inapplicable (zero-spawn, `gleam_otp` forbidden, `pipelines/P6-gleam-impl/DOSSIER.md:30-31, :112`); a whole-engine QHSM that does I/O would have to be `AOK_GATEWAY`, breaking PURE_ACTOR purity (§3.1); larger Gleam-side surface re-expressing a machine that is *already* a machine → higher risk of distorting the most-pinned FB-M1-35 single-fire / generation-scoped dedup.

**Option B — Port/FFI (engine = opaque service behind a small ~5-state supervisor QHSM).**
- *Pros:* lowest faithfulness risk (engine's internal Reduce/Suspend/Fail SM untouched inside Gleam); honest impedance (M1 is one zero-spawn process, not an OTP actor; host stays C#/C++); preserves PURE_ACTOR purity (only a thin GATEWAY proxy touches the wire); small new surface, reusing the M2 ED-6 codec the program needs anyway; loose coupling — a crash is contained and re-driven by the durable dead-lettering mailbox (`qhstate/specs/034-…/research/streams/beacon.md:21-23`).
- *Cons:* coarse observability (host sees only `ExecutionStatus` + `blockingReaders`); engine clause-commits are **opaque** to the kernel — **cannot** fold a GLP commit into a kernel distributed transaction (the real loss vs Option A's `GlpUnit` path); cross-boundary determinism rests on outcome-equivalence, not a unified logical-tick trace (AtomVM runs its own pre-emptive SMP scheduler, `pipelines/P6-gleam-impl/DOSSIER.md:93`).

**Recommended (owner-gated, not a Claude call):** **Option B for the M1 parity deliverable.** Option B is the better fit on every axis except (a) fine observability and (b) kernel-composable GLP-commit — neither of which M1 requires (M1's contract is just top-level `ExecutionStatus`, `pipelines/P6-gleam-impl/DOSSIER.md:65`). Choose **Option A only if/when M2 or a product feature needs kernel-level GLP-commit ACID composition** (the `GlpUnit` two-level-commit path, `olamnit/Olamnit/Olamnit.Kernel/Glp/GlpUnit.cs:9-66`). For the AtomVM target specifically, both options reduce to the out-of-process guardian (§3.3 PATH-B), so the fork is effectively "small opaque supervisor now (B), upgrade to a rich GlpUnit-composed QHSM when M2 demands it (A)" — sequenceable, not mutually exclusive.

---

## 3. YngeniOS integration points

### 3.1 Authority asymmetry — the binding structural seam

The AOK-OS C++23 microkernel encodes an immutable authority class set at AO creation:

- **QHSM/QMSM = `AOK_PURE_ACTOR` (=0)** — signals + messages only; **denied** external resource capabilities by construction (`qhstate-Yngenios/vendor/rtos-kernels-cxx23/aok/include/aok_cxx23/aok.h:82-85`). The §J resource-acquire API refuses a PURE_ACTOR with `AOK_ERR_DENIED` (`aok.h:252, 257-261`).
- **GLP = `AOK_GATEWAY`** — actor PLUS a kernel-tracked resource/routing layer (files, pipes, transports MQTT/HTTP/TCP/UDP/Bluetooth) (`aok.h:82-85`; requirement `qhstate-Yngenios/synthesis-os/REQUIREMENTS.md:69-92`).

**Consequence for packaging:** the control QHSM must be `AOK_PURE_ACTOR` (hermetic, holds no resource cap); only a thin `AOK_GATEWAY` proxy may hold the transport cap for the M2 bytecode-on-wire seam. → **M1 single-instance parity = PURE_ACTOR; M2 linked parity = GATEWAY. The §J resource/routing layer *is* the M2 transport.**

### 3.2 The QP/C plug-in port — where a QHSM literally attaches

`qhstate-Yngenios/ports/aok/qf_port.c` maps QP onto the kernel:
- `QActive_start_` creates the inbox via `aok_evtch_create(aok_root_cnode(), qLen)` and the actor via `aok_actor_create(..., AOK_PURE_ACTOR, aok_prio)` then `aok_actor_set_inbox` — **QHSM/QMSM AOs are hard-wired `AOK_PURE_ACTOR`** (`qf_port.c:259, 286-290`).
- `QActive_post_` → `aok_evtch_post` + `aok_signal` (`qf_port.c:124-125`); the AO event loop receives via `aok_evtch_get(me->eQueue, &badge_)` — the per-RTC-step boundary (`qf_port.c:198-203`); QP→MCS priority inversion `aok_prio = QF_MAX_ACTIVE - me->prio` (`qf_port.c:286`).
- Port header: `#define QACTIVE_EQUEUE_TYPE aok_cptr` and `#define QACTIVE_THREAD_TYPE aok_tcb*` (`qhstate-Yngenios/ports/aok/qp_port.h:60-61`) — an AO's queue **is** a cap-addressed EventChannel; its control block **is** an AOK Active Object.
- Transaction wrap: `aok_actor_bind_state(...,0)` may pass 0 bytes for a state-machine AO whose state lives in its own struct ("the DPP QActive case"), so the txn covers only the outbox+mints (`aok.h:182-185`); atomic commit-and-emit `aok.h:187-195`.

A second concrete QHSM plug-in exists (Zephyr port: `qhstate-Yngenios/zephyr/qp_port.h:52-53`, `QACTIVE_EQUEUE_TYPE = struct k_msgq`).

### 3.3 The two embeddings of the Gleam QActive — and their AtomVM convergence

- **PATH-A (in-process native AOK):** the engine is a QActive on the AOK C-ABI; inbox = cap-addressed EventChannel (`qf_port.c:259`), RTC boundary = `aok_evtch_get` commit-then-receive, txn brackets the step with `aok_actor_bind_state(...,0)` (`aok.h:182-185`). **Available only on plain BEAM** (where `gleam_otp` works, `pipelines/P6-gleam-impl/DOSSIER.md:93`), never on AtomVM (`proc_lib` absent).
- **PATH-B (out-of-process guardian — the beacon-wrapper, named for Gleam verbatim):** the C#/C++ host's QHSM stays `AOK_PURE_ACTOR` and supervises; the Gleam/AtomVM `.avm` is a class-B guardian reached over a durable PGlite mailbox + a canonical Envelope. Request = bytecode program / inbound M2 term; reply-as-event = `ExecutionStatus` + outbound terms. This is the only realizable AtomVM embedding (REPL/`gleam_otp` can't run there, `pipelines/P6-gleam-impl/DOSSIER.md:112`).

The beacon-wrapper recipe (spec-034, the C# QHState-RTOS "OS"):
- **PAT-01 — the scheduler that owns the wrapped lifecycle:** single-threaded round-robin active-object scheduler; each unit = a `QActive`; `Step()` dispatches exactly one event per AO per pass, `Drain()` runs to quiescence — "exactly the suspend/quiesce signal the kernel needs to safely pause/migrate the guardian between RTC events" (`qhstate/specs/034-yngenios-microkernel-research-and-distillation-pipeline/research/streams/beacon.md:12-16`).
- **PAT-02 — durable restart-safe mailbox:** body-durable-before-announce + a PGlite `DeliveryStore` running `pending→claimed→consumed|retry|dead_letter`, atomic single-winner claim, idempotent consume, `MaxAttempts=3` (`beacon.md:19-23`).
- **PAT-03 — static-macaroon verify-before-act** at the consume boundary before any side effect (`beacon.md:26-30`).
- **PAT-04 — uniform canonical Envelope + out-of-process co-runtime seam (the direct Gleam answer):** the `PythonWorkerLauncher` pattern — write a request file, spawn the subprocess, read a reply-as-event file, async over the file seam — is "how a microkernel lets a class-B guardian delegate to an external runtime (Python/C#/Gleam) WITHOUT blocking its RTC loop" (`beacon.md:33-37`, verbatim "Python/C#/Gleam" at `:37`).

### 3.4 The C# realization of the same model (the mature target)

`olamnit/Olamnit/Olamnit.Kernel/` is a working C# RTOS kernel (epic-013): AO = a `QActive` HSM, `Dispatch(QEvt)` = one RTC step (`README.md:21-26`); production scheduler `DurableQF` drains by effective priority with fault containment + WAL replay; the GLP family as a transactional unit via `Glp/GlpUnit.cs` (two-level commit, reversible `Trail`, commit-before-send `Outbox`); capability + edge/link layers (`Capabilities/`, `Edge/`, `Link/`) realize the §F CNode analogue and the §3a GLP-gateway resource/routing layer. This is the already-built realization of the same active-object model the AOK C++23 kernel (spec-023, Draft) specifies.

---

## 4. PROVISIONAL / missing-source (do not let any design rest on these unflagged)

1. **The `diana` docs — ABSENT.** `mstack-coop/dianna/application/YNGENIOS-ARCHITECTURE.md`, `…/DIFFERENTIATION.md`, and `D:/bstdev/research/diana-tender/` are **not on disk** (verified by direct `ls`: "No such file or directory"). `mstack-coop` has **no `docs/` directory at all** — its only structured dir is `COOP/` (`mstack-coop/COOP/README.md:1-11`). "diana" material exists only as **NATO-DIANA tender coordination notes** (`mstack-coop/task-diana-research.md:1-3`), not a `docs/diana` source. → The product-altitude "YngeniOS / Five Guardians / TaskTop / Crucible" stack (`mstack-coop/architecture-evidence-captured.md`, explicitly sourcing the absent `dianna/application/YNGENIOS-ARCHITECTURE.md`) is **PROVISIONAL** and is **not used by either packaging design** (informational only). Principals themselves flag "Named != fielded" (`mstack-coop/note-phaseb-gabi-named-components.md:10`). **To firm:** owner supplies the diana docs or rules them out of scope.

2. **CORPUS-INDEX §G/§H mischaracterization — must be corrected before any P7 design cites them by §G/§H.** The index's "`qhstate-Yngenios` is a stub / coordination-notes-only" claim is **FALSE by direct observation**: `qhstate-Yngenios/` is a 3820-file QHSM tree (`src/ Csharp/ specs/ synthesis-os/ zephyr/ ports/ vendor/ codeconv/ workflows/ tools/ tests/ docs/ examples/`), and its `.git` is a **worktree of `qhstate`** (`qhstate-Yngenios/.git:1` → `gitdir: …/qhstate/.git/worktrees/qhstate-Yngenios`). `specs/034-*/` carries the full pipeline artifact set. The index **transposed** the two siblings — the "coordination-notes-only" description fits `mstack-coop` (whose only structured dir is `COOP/`), and the substantive embedded/specs structure fits `qhstate-Yngenios`. The task brief's restatement "`qhstate-Yngenios` is a stub" is likewise falsified.

3. **Beacon PAT-01..04 line-level C# sources — outside the sanctioned read set, NOT opened.** `BeaconRtos.cs / DeliveryStore.cs / Macaroon.cs / PythonWorkerLauncher.cs / Envelope.cs` live at `D:/BSTDEV/research/buildkit-beacon/beacon/host` per `beacon.md:3` front-matter — not in the sanctioned set {qhstate, qhstate-Yngenios, mstack-coop, olamnit}. The PAT-01..04 patterns are cited from the **in-`qhstate` distilled spec-034 file** (independently verified 4/4 per its front-matter, `beacon.md:5-6`) and are PROVISIONAL against their primary C# sources. **To firm:** owner adds `buildkit-beacon` to the read set, or accepts the spec-034 distillation as the contract of record.

4. **AOK-OS kernel maturity (the PATH-A foundation) — Draft.** `aok.h` + `ports/aok/` are real authored code, but spec-023 is `Status: Draft` (`qhstate-Yngenios/specs/023-aok-os-synthesis/spec.md:5`); the design baseline `synthesis-os/synthesis/baseline-image.md` is a "design exploration" with 4 BLOCKER / 7 MAJOR / 8 MINOR review findings + 14 open variation options (`qhstate-Yngenios/synthesis-os/INDEX.md:45-62`); `qhstate-Yngenios/examples/aok/` is **empty** despite the substrate manifest listing a `dpp_aok-host`. → The PURE_ACTOR/GATEWAY embedding rule rests on a Draft kernel; **prefer the already-built `Olamnit.Kernel` (C#) as the mature realization** until an owner-gated kernel verification pass + a built DPP-on-AOK example exist.

5. **In-process `libAtomVM` FFI embedding — NOT grounded.** The corpus establishes only `generic_unix` + filesystem-loaded `.avm` (`pipelines/P6-gleam-impl/DOSSIER.md:112`); no stable `libAtomVM` C-ABI / engine→host upcall path is in the read corpus. The grounded mechanism is the host-driven request/reply file seam (PAT-04). FFI-direct / engine-initiated-callback variants are PROVISIONAL. **To firm:** spike a `generic_unix` `.avm` file-seam round-trip first.

6. **ED-6 float-decode on AtomVM — unverified.** Whether AtomVM honors `/float` bit-syntax extraction is NOT grounded (`pipelines/P6-gleam-impl/DOSSIER.md:139`). Affects only the GATEWAY transport's `ConstReal` payload, not the QHSM control structure. **To firm:** spike before committing the Gleam codec.

7. **FB-M1-40 parity (RISK-CITE-1) — Dart reference line unpinned.** The Dart `heap_fcp.dart forward_to_terminal` reference line is unpinned (`pipelines/P6-gleam-impl/DOSSIER.md:141`); Gleam impl self-consistent but parity not yet provable. **To firm:** pin the Dart line before declaring FB-M1-40 verified.

8. **Grounding text staleness (not load-bearing here, flagged so it doesn't propagate):** the design-altitude `baseline-image.md` lists a cap kind `REGION` and a block reason `KernelOp` that the **realized header disavows** — `aok.h:67-78` ("NO fictitious REGION/Untyped kind", adds `AOK_CAP_RESOURCE`) and `aok.h:100-104` (`aok_block_reason = {NONE, ENV_MESSAGE, ENV_RESOURCE_IO, KERNEL_TIMER}`, "NO speculative KernelOp class"). This dossier uses the realized header model; do not carry REGION/KernelOp forward.

---

## 5. Owner-gated note

This is a research/verification dossier under feature 036. Per FR-010/FR-011 the program is **read-only on the live roadmap, specs, code, and ALL sibling repos until the owner migration gate**. The Option A/Option B fork (§2.4), the PATH-A vs PATH-B embedding (§3.3), the M2 maGLP term-link seam, and the circular-term-deref / suspension-minimality choices are all **owner gates, not Claude calls**. Nothing here advances the live roadmap or mutates any repo.

---

## 6. Sources read firsthand (all absolute)

- `D:/bstdev/research/glp/glpnet/docs/research/glp-gleam-baseline/pipelines/P6-gleam-impl/DOSSIER.md`
- `D:/bstdev/research/qhstate/include/qp.h`; `D:/bstdev/research/qhstate/src/qf/qep_hsm.c`; `D:/bstdev/research/qhstate/src/qf/qf_actq.c`
- `D:/bstdev/research/qhstate/Csharp/runtime/Qp/QActive.cs`; `D:/bstdev/research/qhstate/Csharp/runtime/Qp/Qp.cs`; `D:/bstdev/research/qhstate/codeconv/src/codeconv/runtime/qp/qactive.py`
- `D:/bstdev/research/qhstate/specs/034-yngenios-microkernel-research-and-distillation-pipeline/research/streams/beacon.md`
- `D:/bstdev/research/qhstate-Yngenios/vendor/rtos-kernels-cxx23/aok/include/aok_cxx23/aok.h`; `D:/bstdev/research/qhstate-Yngenios/ports/aok/qf_port.c`; `D:/bstdev/research/qhstate-Yngenios/ports/aok/qp_port.h`; `D:/bstdev/research/qhstate-Yngenios/zephyr/qp_port.h`; `D:/bstdev/research/qhstate-Yngenios/synthesis-os/REQUIREMENTS.md`; `D:/bstdev/research/qhstate-Yngenios/synthesis-os/INDEX.md`; `D:/bstdev/research/qhstate-Yngenios/specs/023-aok-os-synthesis/spec.md`
- `D:/bstdev/research/olamnit/Olamnit/Olamnit.Kernel/Glp/GlpUnit.cs`; `D:/bstdev/research/olamnit/Olamnit/Olamnit.Kernel/README.md`
- `D:/bstdev/research/mstack-coop/COOP/README.md`; `D:/bstdev/research/mstack-coop/architecture-evidence-captured.md`; `D:/bstdev/research/mstack-coop/note-phaseb-gabi-named-components.md`; `D:/bstdev/research/mstack-coop/task-diana-research.md`
- QP/QHSM web (Quantum Leaps SRS, normative): https://www.state-machine.com/qpc/srs-qp_ao.html ; https://www.state-machine.com/qpc/srs-qp_sm.html ; https://www.state-machine.com/qpc/srs-qp.html ; https://www.state-machine.com/qpc/struct_q_active.html ; https://www.state-machine.com/qpcpp/class_q_p_1_1_q_active.html

Absences re-verified by `ls`: `D:/bstdev/research/diana-tender/`; `D:/bstdev/research/mstack-coop/dianna/application/`; `D:/bstdev/research/mstack-coop/docs/`; `D:/bstdev/research/qhstate-Yngenios/examples/aok/`.
