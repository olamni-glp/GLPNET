# Feature: Persistent, Embeddable GLP Engine — REPL Front-end / Execution-Engine Separation over a Binary Wire Protocol

**Epic:** `epic-separation-of-repl-front-end-from-engine-execution-scheduler`
**Roadmap feature:** `repl-engine-split-mvp-binary-wire-format-intermediate-language-c`
**Authored:** 2026-06-08 — a faithful restructuring of the owner's (Gabi's)
points into one coherent feature. Raw capture: [`requirements.md`](requirements.md).
**Implementation order:** C#-first reference (`out/csharp`); a Dart mirror
(`glp_runtime`) is later / optional / not necessarily required.

This feature is itself a **comprehensive, detailed review AND refactoring**. It is
intended to be driven as a **marathon** whose first deliverables are (1) the
detailed engine review, (2) the refactoring design, and (3) the fleshed-out
feature (all normal feature details) — which then proceeds through the buildkit
pipeline (specify → clarify → plan → tasks → analyze → implement → codex-review →
ship). The marathon sequencing is defined in §8.

---

## 0. 🔴 HARD CONSTRAINT — build as SEPARATE components; do not touch the existing integrated REPLs

**Imperative (owner):** every feature in this epic is built in a **separate
instance / a separate set of components** that can be run separately. **Do NOT
overwrite or modify the existing single-image *integrated* REPLs — BOTH:**
- the **C#** single-image integrated REPL (`out/csharp` + `out/csharp/glp_repl` /
  `glp_repl.cs`), and
- the **Dart** single-image integrated REPL (`glp_runtime` + `bin/glp_repl.dart`).

The infrastructure we have right now (the integrated, everything-in-the-REPL C#
and Dart images) **must stay exactly the same**. This epic's persistent,
separated, embeddable engine is an **additive, parallel set of components**, never
an in-place edit of the integrated images.

**Implication for the §7 feature breakdown / the design:** the MVP and PREP items
that the investigation framed as engine edits (e.g. promoting result fields into
`ExecutionResult`, routing output through callbacks) must be realized in a
**separate engine component** (a forked/extracted/wrapping copy used by the new
process-split host) — **not** by mutating the existing integrated `GlpEngine` /
REPL. The separated engine may *reuse/share* code, but the existing integrated
single-image REPLs remain untouched and fully working. (Contrast: feature-025's
link layer was an additive package + null-guarded hooks on the *current* runtime;
this epic instead stands up a *separate* engine instance.)

## 1. Vision

Turn the GLP engine into a **long-running, OS-level, persistent, embeddable
execution service**. The interactive REPL becomes just one client of it. The
engine runs as a permanent operating-system task — for a very long time — holding
its **code and goals persistently** in database-backed storage, **bootstrapping**
itself on first start, and **resuming from its persisted current state** after any
power-down or crash. Clients (an interactive REPL, or another program in the OS
that drives it) connect to it over a documented **binary wire protocol** that
carries the intermediate language.

---

## 2. Front-end / back-end separation (the split)

- Run the **REPL front-end** — the parser, the REPL loop / command handling, the
  result display, the pausing, and all front-end work — as a **separate process
  instance** from the **back-end engine-execution + scheduler**, which becomes
  **embeddable** and independently runnable.
- The two halves communicate **only** over the binary wire protocol (§3). All the
  pausing and front-end work lives in the front-end; the engine owns execution +
  scheduling.
- The refactoring is, in particular, a **detailed review and refactoring of the
  execution and scheduling engine** so it can be a standalone, embeddable,
  persistent back-end (see §6, §7).

### 2a. Factor out the COMPILER; ANTLR4 shared-grammar, multi-target
- As part of the front-end separation, **factor the COMPILER out of the REPL** —
  the compiler is its own component, distinct from the interactive REPL.
- **Explore an ANTLR4-based compiler design:** define the GLP grammar ONCE as a
  **well-defined, shared ANTLR4 grammar** that is the **authoritative definition
  shared across different implementations** of this version of GLP. Use the
  ANTLR4 front-end to parse/compile, and **generate the compiler for multiple
  targets** from that one grammar:
  - **C#** (the C#-first runtime),
  - other **.NET** implementations if wanted,
  - a **Dart** implementation,
  - and **C++** (ANTLR4 has a C++ target).
- One grammar → many runtime implementations, each generated from the shared
  ANTLR4 definition, keeping the language definition single-sourced. (The compiled
  output remains the binary intermediate language of §3.)

### 2b. C++ engine — likely required as a separate additional version
- **Most likely we WILL need a C++ implementation of the engine + scheduler**, as
  a **separate version, IN ADDITION to** the C#/.NET one (not replacing it).
  Explore the requirement + feasibility for the engine, and likewise for the
  **factored-out compiler** and the **REPL front-end** in C++ — driven by
  performance, footprint for the many-instance §7a goal, and portability.
- This **multi-implementation reality is exactly why** a compiler built on a
  **standard, shared language definition is essential** — and is the decisive
  reason **ANTLR4 is the choice** (§2a): one standard ANTLR4 grammar generates the
  compiler front-end for **both C#/.NET and C++** (and Dart), so every engine
  implementation consumes the same language definition and produces the same
  binary IL (§3).

---

## 3. Binary wire protocol carrying the intermediate language

- A **clearly-encoded, well-documented binary wire format** — an assembly-style
  encoding of the intermediate language — is the sole interface between front-end
  and engine.
- **client → engine:** the **compiled intermediate-language representation** of
  clauses and goals (the parsed/compiled program), sent to the execution engine +
  scheduler.
- **engine → client:** the **results** — bindings, the variable-name → writer
  mapping, the success / suspend / fail status, streamed output, and errors — in a
  wire representation to be designed.
- The engine **occasionally generates new intermediate language at runtime**:
  some clauses may generate their own IL. The protocol and the engine boundary
  **must handle engine-generated IL** crossing back/forth, not only the
  client-supplied IL.

---

## 3a. Textual IL representation + decompiler (binary ⇄ text)

- The intermediate language is **binary in real use** (the wire + persistence format,
  §3), but we also want a **textual representation** of it: the ability to **export
  the binary IL to a human-readable textual form** (an assembly-style text that
  corresponds to the binary encoding).
- This is effectively a **decompiler** (binary IL → textual IL), and ideally
  **round-trippable** (a textual assembler back to binary), so the IL can be:
  - **read by humans**, and
  - **reasoned over by agents and machine learning** (the textual form is the
    surface LLM/ML tooling works against).
- This is **part of the provable-design roadmap** (§10b): a specified textual IL +
  decompiler supports inspection, reasoning, and verification. The existing C#
  `ToDisassembly()` (human-readable disassembly, not a format) is a starting point
  but must become a **proper, specified textual IL format**.

## 4. Connection model — a control program that accepts clients

- The front-end must **NOT** have its own bespoke connection mechanism. Instead,
  **when the engine starts, it runs a pre-compiled control program** that **listens
  for and accepts client connections** and then serves them. The front-end REPL is
  **one kind of client**; nothing about the connection is REPL-specific.
- **Clients are either:**
  1. an **interactive REPL**; or
  2. a **programmatic client** — **another program running in the operating
     system** that connects and **initiates work** by sending messages (compiled
     goals / clauses). This is not an interactive REPL; it is program-driven.
- Explore **multiple clients connecting to one engine** (one engine, N clients),
  and whether/how that is feasible for the MVP vs a follow-up — including the
  single-owner-heap (Option-C) implications of N clients sharing one engine.

---

## 5. Long-running, OS-level engine: liveness, crash signaling, restart

- The embedded engine is a **long-running, OS-level permanent task** — it is meant
  to run live for a very, very long time, hosted under an operating system that
  **monitors its liveness**.
- **Liveness signaling:** the engine must **signal liveness to the hosting OS**
  (heartbeat / health / watchdog-style).
- **Crash signaling:** the engine must also **signal when it is crashing**. If it
  hits errors it **cannot fix / cannot live with**, it must **signal the hosting
  OS that it needs to be restarted**.
- **Restart / supervision:** the OS supervisor restarts it; on restart it comes
  back up and **resumes from its persisted state** (§6).

---

## 6. Persistent full-state storage (a first-class concern of the refactoring)

- The engine must **store its full current state in permanent, persistent
  storage** — a **database underneath**, fronted by an **API that hides the
  detail** (an abstraction over some database).
- **Bootstrap on start:** on startup the engine runs a **predefined bootstrap
  script** (at minimum).
- **Restore-and-resume:** when it has been powered down — temporarily, or because
  of an issue / bug / failure — and resumes, it must **start up again and run with
  the current (persisted) state**. This is crash-recovery to the live state, not a
  cold reset.
- The **storage of the current compiled program and all relevant state** must be
  **factored into** the front-end/back-end refactoring **from the outset** — not
  bolted on afterwards. The engine is refactored to be **persistent**: an engine
  with persistent code and goals (persistent constructs), backed by the DB
  abstraction, with **automatic reload on restart**.

### 6a. Persistent vs ephemeral state — the central distinction

The engine must distinguish what **survives a restart** from what **cannot**:

- **Persistent constructs** (stored in the DB, reloaded on restart):
  - the **compiled code / clauses** and the **goals / facts** that must persist;
  - **definitions**, at a higher level than live resources — e.g. a **channel**
    treated as a persistent construct, and a **persistent link definition** such
    as *"always listening on this socket"*. The definition / intent persists.

- **Ephemeral constructs** (do **not** survive — the underlying OS resource is
  gone after a power-down or crash):
  - a **live link instance / connection** — the actual socket drops when the
    engine is powered down or crashes; that link instance obviously fails and
    cannot be carried across the restart;
  - close / context that is bound to a now-defunct underlying resource.

- **Restart behaviour:** on resume, reload the persistent constructs, then
  **re-establish the ephemeral resources from their persistent definitions** —
  e.g. the persistent **channel / link definition** sets up a **fresh link
  instance** (a new socket) again after the restart, rather than resurrecting the
  dead one. A higher-level **channel can therefore appear "always set up"** across
  restarts even though its concrete link instances are rebuilt each time. The
  always-listening socket, being part of the persistent definition, is
  re-established so it is there again after a restart.

---

## 7. The relay as a "mailbox" (OS-level vs in-GLP)

- The relay / connection could be a **mailbox**. Both realizations are to be
  investigated and a recommendation made:
  - **OS-level mailbox:** a named pipe, a unix/domain socket, an OS message queue,
    the feature-025 `TcpTransport`, or a file mailbox.
  - **GLP-language mailbox:** a mailbox **programmed in GLP itself** — built on
    existing GLP constructs (channels / streams, `send` / `receive` / `merge`, the
    `multiagent` relay + mad-context channels, the feature-025 link In/Out
    streams). The control program (§4) and the per-client mailbox could
    potentially be **written in GLP**, on top of the link layer.

---

## 7a. Many engine instances, shared/instance memory, cooperative scheduling

- Run **several instances of the backend engine on a fixed machine** — all working
  with the intermediate language + a memory — but built from **ONE codebase**.
- **Memory structure to investigate (two tiers):**
  - **Instance memory** — specific to one instance (its changing/dynamic state).
  - **Shared memory** — the **static, non-changeable parts**: the **wrappers for
    the constructs** and similar internal machinery (much internal code just wraps
    internal things), shared across all instances.
- Goal: a **minimal memory footprint** so a **significant number of different
  engine instances run at the same time**, monitored by the OS, and potentially
  **cooperating with each other**.
- Each instance keeps its **single-threadedness**. Several instances run in
  parallel under the OS; ideally they **share the static memory**.
- **Safe preemption + resumption:** explore how an instance can be **safely
  preempted (without issues) and then resumed**, if the host OS needs that.
- **Cooperative run-to-completion scheduling (option):** each **overall atomic
  reduction CHAIN** (not tiny per-step reductions) runs, then **returns control to
  the OS** — a cooperation between the OS and the engine+scheduler instance, so the
  instance can be scheduled in **cooperative run-to-completion mode**.

## 7b. Research programme + prior-art scan (internet research)

Because several of these dimensions (binary IL design, shared/instance memory,
cooperative scheduling, preempt/resume, orthogonal persistence) have deep prior
art, the work includes an **internet-research programme** carried out by
sub-agents: formulate the overall research programme, then drill into specific
questions with concrete code examples. It must include a **scan of logic-language
intermediate languages that compile to a binary format** — **Ehud Shapiro's**
Concurrent Prolog / Flat Concurrent Prolog (FCP) abstract machines, the WAM
(Warren Abstract Machine), KL1/KLIC (FGCS), BinProlog, and other related work —
plus relevant IL/bytecode formats and VM techniques from other mechanisms. Output:
[`research-programme.md`](research-programme.md).

- **LLVM exploration (staged):** as part of the refactoring review, explore whether
  **LLVM compiler technology** (LLVM IR, code generation, optimization passes, JIT,
  MLIR) would be useful for **generating or modifying the intermediate language /
  binary format** — for **optimization or other purposes**. Do it in stages:
  (1) **scout** what is available in web resources; (2) **conditionally** go deeper
  if it looks promising; (3) **potentially a verification spike** to try it out and
  confirm feasibility. Output: [`llvm-feasibility.md`](llvm-feasibility.md).

## 8. Marathon execution sequence (how this feature is run)

Driven as a marathon, this feature's to-do proceeds in this order:

1. **Detailed engine review** — the comprehensive, multi-agent, read-only review
   of the C# implementation: the REPL-frontend ↔ engine seam, the intermediate
   language + compile pipeline (incl. runtime-generated IL), the
   front-end↔engine interchange contract, the existing reusable wire/durability
   infrastructure, and the resilience/liveness/mailbox/persistence surface.
   *(This is the investigation defined by* `requirements.md` *→* `investigation.md`*.)*
2. **A design for the refactoring** — the seam, the binary wire protocol, the
   control-program startup + client model, the long-running/liveness/crash/restart
   model, the persistent-vs-ephemeral state model + DB-abstraction + bootstrap +
   restore-and-resume, and the mailbox decision; with a recommended MVP slice.
3. **Add the feature with all normal feature details** — turn the design into a
   full feature ready for the pipeline, then run it through buildkit:
   **specify → clarify → plan → tasks → analyze → implement → codex-review →
   ship**.

---

## 9. Reuse & relationships

- **Feature 025 (multi-protocol-link-layer):** the wire/frame codec
  (`FrameCodec`: version + CRC + fragmentation), the transport seam +
  `TcpTransport`, the `LinkPump`, and the `PayloadSerializer` already exist and are
  candidate building blocks for the wire protocol and the transport/mailbox.
- **marathon-stage-harness / DBOS-on-PGLite:** the repo already has durable,
  restart-safe state machinery (DBOS on PGLite, plus the bridge-daemon-coordination
  research) that should inform the persistent-state store, the DB abstraction, and
  supervised restart.

---

## 10. Success themes (to be sharpened into acceptance criteria during specify)

- The REPL front-end and the engine run as **separate process instances**, the
  engine **embeddable**, communicating only over the **documented binary wire
  protocol**; the front-end is just one client.
- A **programmatic OS client** can connect to the engine's control-program listener
  and **initiate work** with the same protocol.
- The engine **bootstraps** on first start, **signals liveness** to the OS,
  **signals "restart me"** on unrecoverable failure, and on restart **resumes from
  persisted current state** (persistent code + goals + definitions reloaded;
  ephemeral link instances re-established from their persistent definitions).
- Persistent vs ephemeral state is **explicitly modelled** and correctly handled
  across a kill-and-restart cycle.

---

## 10a. Related / adjacent future direction (minor context, later)

Likely later: restructure the existing REPL to offer — **in addition to** the
interactive REPL — a new **agentic layer**. It would let a user **formulate logic
programmes in natural language + some formulas + a design process**, and then
**generate the actual GLP strictly** from that input, and **explore mathematical
verification** of the generated programme. This is an additional front-end client
mode (it would ride the same control-program/wire/client architecture as the REPL),
**not** part of the persistence-refactoring MVP — captured here so the separation
design leaves room for additional client kinds beyond the interactive REPL and the
programmatic OS client.

**Long-term challenge target:** generate — from **natural language + some formulas +
some designs** — particular **GLP programmes** that then **run for a long time in
these (persistent) engine instances**; and for **each such programme**, ideally
**verify a number of criteria — stability, robustness, reliability, etc. —
mathematically, in some form**. I.e. not only generate GLP strictly from the input,
but **prove properties of the generated long-running programme**. This is a
long-term target, building on the provable-engine/provable-language goal (§10b).

## 10c. Methodology — GEPA/DSPy improvement loops (cross-cutting)

**All of this work must be wrapped in our usual GEPA / DSPy optimization +
improvement-loop methodology** (the same approach used in `codeconv` codegen-opt),
rather than ad-hoc loops — and this must be **factored into the refactoring
programme** itself (the generation, the verification, and the multi-target codegen
should each be driven through GEPA/DSPy improvement loops). NB project HARD RULE:
GEPA/DSPy LM work runs **in Claude (agent seams), never via an external API /
OpenAI / litellm** (see memory `project_gepa_no_api_claude_only`).

## 10b. Mathematical rigour & provability (cross-cutting principle)

A standing principle for this work and the GLP environment as a whole:
- **Be faithful, at all times, to the mathematical specifications set out by Ehud
  Shapiro in his papers** — especially the GLP ones. The implementation must not
  drift from the formal semantics.
- **Where we have EXTENDED the language** beyond Shapiro's specifications (for
  instance the feature-025 **link primitives**), **raise these as longer-term work
  items** and **create the mathematical specifications for the extensions**, so the
  rigour is uniformly high across the board — extensions are specified to the same
  standard as the core.
- **Mathematical verification:** explore doing additional / mathematical
  verification using a **suitable tool, possibly with agent support**, on the
  language and the engine.
- **Goal:** ideally a **provable engine and a provable language** — the refactored,
  separated, persistent engine should be amenable to formal specification and
  verification, not just tested.
- This applies to the refactoring here (the separated engine, the wire IL, the
  persistence model must each be specifiable/verifiable) AND retroactively to
  existing extensions (link primitives) as longer-term work.
- **Deliverable — a fully annotated, specified GLP language version:** maintain a
  fully-annotated, formally-specified version of the GLP language that includes the
  **core, all the extensions** (link primitives, cryptographic capability-control
  primitives, …), and **the mathematical verifications** performed. Paired with the
  specified **textual IL** + decompiler (§3a), this gives an inspectable,
  reason-over-able, verifiable description of the whole language + its compiled form.

## 10d. Language extension: cryptographic capability control

Part of the additional work is **extending the language with cryptographic
primitives** to enable **capability-based access control** in GLP (Grassroots
Language / Grassroots Logic Programming):
- Use **cryptography** to make **capability control** possible in GLP. When a
  **request comes in**, it can be **checked for the right capability**; when a
  **conversation is initiated**, that is part of a **wider capability-controlled /
  access-control mechanism**.
- Extend the language with **a good number of additional cryptographic primitives**
  (and related machinery) needed for the full functionality of the kinds of
  programmes we want to run (distributed, grassroots, long-running).
- Strong tie-in to the connectivity work: capability checks on link/conversation
  establishment (feature-025 link layer + the control-program client model §4).
- **Governance:** these are **new language primitives** — subject to language-
  authority approval (CLAUDE.md §1.14) — and, per the cross-cutting rigour
  principle (§10b), must be given their own **mathematical specifications** and be
  verified to the same standard as the core (capability-security models are
  formally specifiable: object-capability / cryptographic-capability semantics).

## 11. Open questions (for the investigation/design to resolve)

- Exact engine→client result wire encoding (bindings, status, output, errors).
- How runtime-generated IL is represented + transmitted both directions.
- Which DB underlies the persistent store, and the shape of the hiding API.
- Whether the control program + mailbox are best in C# or programmed in GLP.
- Multi-client feasibility for the MVP vs follow-up (single-owner-heap).
- What precisely constitutes "full current state" that must be persisted (heap?
  goal queue? scheduler state? suspended goals? — vs what is rebuilt from code).
