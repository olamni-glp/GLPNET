# REPL/Engine Separation — Owner Requirements (captured)

**Epic:** `epic-separation-of-repl-front-end-from-engine-execution-scheduler`
**Feature (MVP):** `repl-engine-split-mvp-binary-wire-format-intermediate-language-c`
**Captured:** 2026-06-08 (Gabi, dictated across several messages). Status: requirement-gathering — investigation pending confirmation this set is complete.

This document is the authoritative requirement capture that the multi-agent C#
investigation (→ `investigation.md`) must address. C#-first reference
implementation; a Dart mirror is later / optional / not necessarily needed.

## 1. The core split
- Run the GLP **REPL front-end** (parser, REPL loop / command handling, result
  display, pausing, all front-end work) as a **separate process instance** from
  the **back-end engine-execution + scheduler**, which becomes **embeddable**.
- The two halves communicate over a **documented binary WIRE format** — an
  assembly-style, clearly-encoded encoding of the intermediate language.
  - **client → engine:** the **compiled IL** of clauses + goals.
  - **engine → client:** results — bindings, var-name→writer mapping,
    success/suspend/fail status, streamed output, errors.
- The engine sometimes **generates new IL at runtime** (some clauses emit their
  own intermediate language); this must be handled crossing the boundary.

## 2. Connection model — control program, not a bespoke front-end link
- The front-end must **NOT** have its own special connection mechanism.
- On startup the **engine runs a PRE-COMPILED CONTROL PROGRAM** that **listens
  for and accepts client connections**, then serves them. The REPL front-end is
  just **one kind of client**.
- **Clients are either:**
  1. an **interactive REPL**, or
  2. a **programmatic client** — **another program running in the operating
     system** that connects and **initiates work** by sending messages (compiled
     goals/clauses), i.e. NOT an interactive REPL.
- Explore **multiple clients** against one engine (one engine, N clients), with
  the single-owner-heap (Option-C) implications spelled out.

## 3. Long-running, OS-level embedded engine
- The embedded engine is a **long-running, OS-level permanent task** — intended
  to run live for a very, very long time.
- **Liveness to the OS:** the engine must be able to **signal liveness to the
  operating system** it runs under (heartbeat / health / watchdog-style).
- **Restart / supervision:** if it hits a problem (bug, failure, temporary
  power-down) it must be able to **restart** and keep running.
- **Crash signaling (not just liveness):** the engine must signal the hosting OS
  **when it is crashing** — on an unrecoverable error / state it "cannot live
  with", it signals the OS that it **needs to be restarted** (the OS supervisor
  then restarts it, and it resumes from persisted state, §4).

## 4. Persistent full-state storage (factored into the refactoring)
- The engine must **store its FULL CURRENT STATE in permanent, persistent
  storage** — a **database underneath**, fronted by an **API that hides the
  detail**.
- **Bootstrap:** on startup the engine runs a **predefined bootstrap script** (at
  minimum).
- **Restore-and-resume:** when it has been powered down (temporarily, or due to a
  bug/failure) and resumes, it must **start up again and run with the current
  (persisted) state** — crash-recovery to the live state.
- The **storage of the current compiled program + all relevant state** is a
  first-class concern that **must be factored into** the front-end/back-end
  refactoring from the outset (not bolted on later).

### 4a. PERSISTENT vs EPHEMERAL state (the central distinction)
The engine must distinguish what **survives a restart** from what cannot:
- **Persistent constructs** (stored in the DB, reloaded on restart):
  - the **compiled code / clauses** and **goals / facts** that must persist;
  - **definitions** — e.g. a **channel** as a persistent construct, and a
    **persistent link definition** ("always listening on socket X"). The
    *intent* / definition persists.
- **Ephemeral constructs** (do NOT survive — the underlying OS resource is gone):
  - a **live link instance / connection** — the actual socket/fd drops when the
    engine powers down or crashes; that link instance cannot survive.
  - close/transient context bound to a now-gone resource.
- **Restart behaviour:** reload the persistent constructs, then **re-establish**
  the ephemeral resources **from their persistent definitions** — e.g. the
  persistent channel / link *definition* re-creates a fresh link *instance* (a new
  socket), rather than trying to resurrect the dead one. A higher-level channel
  can thus appear "always set up" across restarts even though its concrete link
  instances are rebuilt each time.

### 4b. Feature scope = review + REFACTOR (not just investigation)
This feature **starts** with the comprehensive multi-agent investigation
(→ `investigation.md`) but **is** a comprehensive, detailed **review and
refactoring**: (1) separate the front-end from the engine; (2) **especially a
detailed review + refactoring of the EXECUTION + SCHEDULING engine** so it holds
**persistent code** in DB-like persistent storage (an abstraction over a
database); (3) **automatic reload on restart** (restore + resume the live state).

## 5. The relay as a "mailbox"
- The relay/connection could be a **mailbox**. Investigate **both** realizations:
  - **OS-level mailbox:** named pipe, unix/domain socket, OS message queue, the
    feature-025 `TcpTransport`, or a file mailbox.
  - **GLP-language mailbox:** a mailbox **programmed in GLP itself** — built on
    existing GLP constructs (channels/streams, `send`/`receive`/`merge`,
    `multiagent` relay + mad-context channels, the feature-025 link In/Out
    streams). The control program + per-client mailbox could potentially be
    **written in GLP**.

## Cross-cutting notes
- Strong synergy with **feature 025 multi-protocol-link-layer** (the wire/frame
  codec + transports + payload serializer already built) and with the
  **marathon-stage-harness / DBOS-on-PGLite** durability already in the repo —
  the investigation must assess reuse of both.
- Investigation is **read-only**; output is a design + feasibility report with an
  MVP recommendation, not an implementation.
