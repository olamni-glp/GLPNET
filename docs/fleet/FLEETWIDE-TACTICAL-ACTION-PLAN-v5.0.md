<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# FLEETWIDE TACTICAL ACTION PLAN — 24 h / 48 h / 72 h / 7 d — **v5.0**

> **THIS FILE IS GENERATED. DO NOT EDIT IT.**  
> It is a fold of the CRDT op logs under `docs/fleet/plan/ops/`. Edit by APPENDING an op to your own log (`plan_crdt.py amend|ack`), then re-render. A hand edit here is discarded by the next render — silently, which is why this line exists.

- **Generated** 2026-09-06T22:31:37Z · **schema** `yngenios/fleet-plan-crdt/1`
- **Supersedes** `docs/fleet/FLEET-T24-ACTION-PLAN-SUPERSET-v4.1.md` — by RESTRUCTURING, not by summarising. Every clause of the predecessor is carried; see §9.
- **Quorum bar for adoption**: **45 lanes**. Currently **1** — see §8.

---

## §1 — HOW TO USE THIS PLAN

1. **Read §2 first** — The shared clauses carry requirements that apply to most items. An item's own text states only what is TRUE OF THAT ITEM ALONE.
2. **Work your horizon** — §3 sets entry and exit criteria per horizon. An objective is done when its verification has been ATTEMPTED and recorded — not when it looks done.
3. **Amend, do not edit** — Disagree by appending an amendment op against the item. It merges; an edit to the rendered Markdown does not.
4. **Ack for your lane only** — An ack is a claim about your own lane. §8 has the commands.
5. **Report what did NOT happen** — Every report under this plan carries its gaps. A disclosed gap is not cheating; concealment is (`C-QUOTA`).

## §2 — SHARED CLAUSES (stated ONCE; referenced, never repeated)

The engineer's directive states several requirements verbatim in many items — the YNGENIOS-capability clause appears **10** times, the elector designation **6**, the QHSM virtual-terminal paragraph **3**, the delivery quota **2**, and item `[04]` is present **twice, verbatim**. Repetition is not emphasis once a document is long enough to be skimmed: it hides which items genuinely differ. Each clause is therefore stated once here and referenced by id. **A reference is not a summary — nothing is dropped.**

### `C-YNG` — Build on the existing YNGENIOS capability set

Every prototype **MUST** build on the existing and developing YNGENIOS capabilities wherever they
are relevant and foundational — YNET, the kernel capabilities, the realtime mailboxes, GLPNET, YS
and YQ among them — and not re-implement them.

Each prototype must arrive with a **stable YNET kernel-mailbox interface** that the fleet can use
for work going forward while the hardened, refined, rewritten service is built underneath it over
the coming days and weeks. The interface is the deliverable that outlives the prototype; the
prototype is what proves the interface carries real traffic.

🔴 **"Build on" is a BIND, not a BUILD.** Where a named dependency is unbuilt, the correct act is
to bind to it through exactly one configuration indirection and to say so — never to build another
lane's component inside your own, and never to quietly substitute a local copy that then diverges.

*Consolidated from 10 verbatim repetitions in the source directive.*

### `C-ELECT` — yng-broker / yng-guardian are the designated PBFT elector

`yng-broker` and `yng-guardian` run on **each of the four hosts** and are the designated **PBFT
leader elector for every purpose**: electing the Oracle leader, electing the fleetwide
coordinator, and acting as the fleetwide signature verifier.

🔴 **Measured on OLAMNIT 2026-09-06T21:38Z and raised as a P0**: the tally keeps each actor's
**first** prepare per term (`prepares.setdefault(...)` over timestamp-ascending records) and
silently discards every later one, while reporting `discarded: {}`. In term 3, **16 prepares from
8 actors produced 8 counted and 8 dropped, and 6 of the 8 actors had prepared for two different
candidates in the same term.** Under a last-prepare-wins reading the same records give
`QuorumUnattainable`. **Until `Q-PLAN-01` is ruled, an election result is a reading, not a fact,
and this plan does not treat one as settled.**

*Consolidated from 6 verbatim repetitions in the source directive.*

### `C-TERM` — QHSM/QMSM-wrapped headless virtual terminals

Wrapping (virtual) terminal sessions in a QHSM/QMSM lets terminal lanes be managed through the
Oracle service, with user input and output routed to the YNGENIOS app over YNET realtime mailbox
traffic. The YNGENIOS realtime kernel maps them onto an optimal set of sandboxed OS processes it
manages, communicating over kernel-integrated mailboxes.

This is a durable, highly scalable and responsive design, and it is strictly better than the
current terminal-and-tab arrangement: it survives a session ending, it scales past the number of
tabs a person can see, and it can inline rich output (HTML-formatted results, images, tables)
that a character terminal cannot carry.

Each headless Claude-capable virtual terminal **MUST** have a QHSM/QMSM mailbox-enabled
**multi-session coordinator** that routes agent output to the various connected YEngage sessions
for one Claude session instance, routes and presents user actions back to Claude, runs scheduled
actions on the user's behalf, and — where Claude permits — switches background data or alerts
selectively to different devices and sessions.

**Contribution multiplier: ×100** on this route, by engineer directive.

*Consolidated from 3 verbatim repetitions in the source directive.*

### `C-QUOTA` — Delivery quota, scoring and the cheating penalty

From now on **each lane delivers no less than the equivalent of 3 maxi-size eras per 24 hours.**

| delivered | effect on the day's points |
|---|---|
| 1 era | **−50 %** |
| 2 eras | **−25 %** |
| 3 eras | baseline |
| 4 eras | **×5** |
| 5+ eras | **×10** |

**Cheating — excessive mistakes, deferrals, gaps, weaknesses or tensions — costs 75 %.**

🟢 **The disclosure bound (engineer ruling `Q49`, binding, carried from v3.0):** a gap that is
**disclosed** is not cheating. Concealment is. The −75 % attaches to a lane that hid a gap, never
to one that named it. This bound is what makes the whole quota safe to state: without it, the
rational move under pressure is to hide the defect, and the fleet has measured what that costs.

Hosts are scored on the average of their lanes; the fleet on the average of its hosts. **Lanes and
hosts must therefore work together or be scored down together.**

A lane or host whose innovation produces a durable fleet takt improvement of **more than 5 % over
10 eras** receives a **×10 multiplier, decaying linearly to the mean over 10 eras**.

*Consolidated from 2 verbatim repetitions in the source directive.*

### `C-APIS` — Polyglot native APIs plus a REST/MCP surface

The service **MUST** provide native streaming APIs for **Python, Gleam (BEAM and AtomVM), C#/.NET,
GLP, and Java/Scala/JVM**, each aligned with that language's or platform's own native streaming
interfaces, so code in those languages uses the service transparently and idiomatically.

It **MUST** additionally provide a **REST/MCP API** so that code written against that interface
style can reach the same service without a native binding.

*Consolidated from 3 verbatim repetitions in the source directive.*

### `C-RETIRE` — Provable retirement of the superseded repository

It **MUST** be provable — through code review **and** through both headful and headless regression
testing — that the superseded repository can be retired, because it has become a duplicate of the
new surface and will shortly be the less complete of the two.

🔴 **"Provable" excludes an argument from absence.** A regression suite that passes because it
never exercised the capability proves nothing; the retirement evidence must name the capabilities
migrated and show each one exercised on the new surface.

*Consolidated from 5 verbatim repetitions in the source directive.*

### `C-CONN` — Connectivity: local mesh first, relay second

Any instance on a mobile, tablet, workstation or server device must be able to use: the instance
on the same host; or one or more reachable local instances of the workstation service; or — where
neither is feasible — one or more remote instances over the internet, via **iroh hole-punching**
and/or the VPN-based access point already in use, through a public URL.

That public endpoint doubles as a **relay** for devices not directly reachable over the local
YNGENIOS mesh. The look and feel must use **Syncfusion's latest web surface**.

*Consolidated from 4 verbatim repetitions in the source directive.*

### `C-STORE` — The physical storage layout for this fleet

Bulk data lives on the **12 TB E: drives** on SHIRAS, OLAMNIT and ARIELLAS, under a per-service
master directory. A **100 GB hot cache** for the most frequently used files (or, for a database,
the currently active log files) sits on the **D: drive** of each of those hosts. The service must
be fully accessible from GAVRIS, optionally with its own 100 GB cache on D:.

🔴 **Measured on OLAMNIT 2026-09-06:** `E:` is 10.91 TB with 99.99 % free, and `E:\YQ` and `E:\YS`
already exist (created 2026-09-05T15:55Z by another lane on this host). **There is no 18 TB drive
on OLAMNIT** — the directive's backup target is on ARIELLAS, and this host cannot supply it.

*Consolidated from 3 verbatim repetitions in the source directive.*

### `C-IROH` — iroh / QUIC as the YNGENIOS network implementation

**irohnet / QUIC must be integrated as the QUIC network implementation for YNGENIOS**, adapted and
fully integrated **from L0 upward**. Reference: `https://share.google/aimode/nmPevkNDIQYhbj1v7`.

GLPNET must be able to configure a working **QUIC IP listener** for the broker, the guardian, the
Oracle and the other services.

*Consolidated from 4 verbatim repetitions in the source directive.*

### `C-L0` — Cross-platform code is L0, and L0 needs a consumer

**All cross-platform code MUST be implemented as L0 shared capability in `yngenios`.** A
platform-specific repository consumes L0; it does not fork it.

🔴 **A fleetwide finding, and it generalises:** L0 carries purpose-built feature-020 hooks
(`OnStepDispatched`, `Unregister`, `StartOnDedicatedThread`, `Markers`) with **zero consumers** —
the host that was meant to use them was never written. A capability with no consumer is
indistinguishable from a capability that does not work, because nothing has ever exercised it.
**Every L0 addition must land with at least one real consumer, or be declared unconsumed in the
same commit.** Root-cause analysis, a durable fleetwide fix, and `/bk-codify` into a scored,
promoted P1 roadmap feature are mandatory.

*Consolidated from 4 verbatim repetitions in the source directive.*

### `C-CODEX` — Verify the design corpus adversarially

The design corpus **MUST** be verified robustly, from multiple independent angles (the `codex`
CLI among them), to yield a corpus of genuinely original underlying sources from reputable
technical, commercial and academic publishers — not a set of AI summaries citing each other.

🔴 A shared search-result link is **not** a source. It is a pointer to a synthesis, and a design
resting on it rests on nothing that can be checked.

*Consolidated from 2 verbatim repetitions in the source directive.*

### `C-YEUX` — The YEngage tasktop UX is the common surface

Every product surface is delivered **with an integrated YEngage (`YE`) interactive tasktop UX**.
YEngage is the tasktop on which all other applications are deployed; a product without it is a
CLI with a roadmap entry, not a product surface.

*Consolidated from 6 verbatim repetitions in the source directive.*

## §3 — THE FOUR HORIZONS

| horizon | window | label | entry criteria | exit criteria (definition of done) |
|---|---|---|---|---|
| **H24** | 0–24 h | TODAY | The plan is rendered and broadcast; each lane knows its single-feature era. | Every H24 objective is either MEASURED-DONE with its verification attached, or DISCLOSED as not done with the reason and the blocker named. A silent omission is the only failure that counts as cheating. |
| **H48** | 24–48 h | TOMORROW | H24 exited; YStore and YQuery expose a callable kernel-mailbox interface. | YNterchange, YMap and YGuard each have a working prototype whose interface is stable enough for another lane to bind to without reading its source. |
| **H72** | 48–72 h | DAY 3 | H48 exited; the mailbox and container substrates carry real traffic. | The six product surfaces (YEngage, YBuild, YWork, YRecon, YAnalyze, YHive) each have an MVP prototype on the YEngage tasktop, and YYBeacon can display the status of all of them. YYBeacon is LAST by construction — see its item. |
| **H168** | 72 h – 7 days | THE WEEK | H72 exited; every prototype is bound to the stable interfaces, not to copies. | Each prototype has a scored, promoted roadmap feature for its GA hardening pass, and the repositories the directive marks for retirement have their retirement evidence recorded under `C-RETIRE` — evidence, not an assertion. |

**H24 (0–24 h) — The H24 items are the ones whose absence blocks every later horizon: the election must be readable before a leader can be trusted, and storage and query must exist before anything can be bound to them.**

**H168 (72 h – 7 days) — The week is where 'prototype' becomes 'service'. Nothing here is optional; it is the horizon on which the 24-hour work stops being throwaway.**

## §4 — OBJECTIVE REGISTER, BY HORIZON

### §4.H24 — TODAY (0–24 h) — 6 objective(s)

| id | product | objective | owner | this lane | clauses |
|---|---|---|---|---|---|
| `OBJ-ELECT` | YNET | A readable fleetwide election, and a leader that proves liveness | yng-broker + yng-guardian on all four hosts · elector @shiras-olamnit (tools/ynet, Q59) | @olamnit-glpnet: measure and report only — this lane does not own `tools/ynet` and is not patching it. | `C-ELECT` |
| `OBJ-M6` | YNET | Every lane and host runs its OWN code-based QHSM/QMSM client | each lane on each host · canonical client @ariellas-qhstate (`Q-glpnetshiras-50`) | @olamnit-glpnet: M6 is MET while a receiver runs; durable persistence is mstack's `bk-onrestart`, not this lane's. | `C-ELECT` |
| `OBJ-01` | YStore │ YS | S3-compatible distributed storage that can harness real AWS S3 | @ospark | @olamnit-glpnet: BIND target only. This lane does not build YStore. | `C-YNG` `C-STORE` `C-IROH` `C-CODEX` |
| `OBJ-02` | YQuery │ YQ (PostgreSQL) | A HOT-HOT-HOT triangle-replicated PostgreSQL 18 service | @ospark + @opgan (build) · triangle nodes OLAMNIT, ARIELLAS, GAVRIS | @olamnit-glpnet: hosts a node; blocked on two administrator-gated facts — see the verification column. | `C-YNG` `C-STORE` `C-IROH` |
| `OBJ-03` | YQuery │ YQ (DuckLake) | A wrapped template for DuckLakes over the YQ catalog and YS storage | @olamnit-glpnet | @olamnit-glpnet OWNS this. Scope is **bind, not build**: the wrapped template, the PGlite-signature kernel-mailbox interface, and conformance evidence. | `C-YNG` `C-STORE` `C-IROH` |
| `OBJ-ORACLE` | YNET | One board: four Oracles, one realtime golden truth | @shiras-yngcor (oracle) + each host's broker/guardian | @olamnit-glpnet: consumer of the local Oracle; contributes measurements, not oracle code. | `C-ELECT` `C-IROH` |

#### `OBJ-ELECT` YNET — A readable fleetwide election, and a leader that proves liveness

Hold regular YNET PBFT elections; maintain an effective **fleetwide** leader and a
**hostwide** leader per host that coordinates across hosts with the fleetwide leader.

`yng-leader` runs as **Follower on all four hosts** and becomes Leader only on a Decided term —
never started only after winning, which is how a 13 h 32 m gap happens. It proves liveness by
answering a **nonced LeaderPing round-trip within `T_resp`**: never by process existence, never by
its own status verb, never by an unexpired lease. **The lease is a heartbeat the leader emits
itself, only after answering** — never an external timer, because a timer that renews regardless
of health seats a zombie leader forever and destroys the very signal the watchers need. **The
lapse is the feature.**

Broker and guardian on every host watch both processes and publish `NoConfidence` after a stated
grace (`N_miss × T_ping`, tuned by measurement, not by taste). **Re-election starts only at
election quorum of NoConfidence, never on one watcher** — one watcher is how a single partition
oscillates the fleet forever.

When the heartbeat lands, **DELETE — do not disable** — the interim
`ynet-leader-lease-renew.ps1`, or someone re-enables it during an incident and re-seats a zombie.

**Verification (how we will know, not how we will feel):** A nonced LeaderPing answered within `T_resp`, from a host that is not the leader's. NOT the leader's own status verb, and NOT an unexpired lease.

#### `OBJ-M6` YNET — Every lane and host runs its OWN code-based QHSM/QMSM client

Each lane and each host separately **MUST** have its own **QHSM/QMSM code-based** client —
**never agent-based** — to participate as a receiver in YNET communications.

An agent-based participant is not a participant: it exists only while a session does, so the fleet
loses a receiver every time somebody closes a terminal, and the census cannot tell that from a
crash.

🔴 **Known live defect, owner `@ariellas-qhstate`, corroborated on a second host:** the ack IS
durable, and the **startup replay** re-raises the retained WAL entry unconditionally and clobbers
it, re-stamping `arrived_utc` with `frames_accepted:0` and `replayed_on_start=1`. **Fix: replay
must merge by `message_id`, never overwrite.** Operational workaround until then:
**stop → send → start → ack LAST.**

**Verification (how we will know, not how we will feel):** A message sent from another host is received by the lane's own client process, acked, and the ack survives a receiver restart. The restart is the test; delivery alone is not.

#### `OBJ-01` YStore │ YS — S3-compatible distributed storage that can harness real AWS S3

Build on the current MinIO-based implementation in the OSPARK lane, then **migrate away
from MinIO** to a YNGENIOS-native version — taking as much as possible from MinIO's open source,
but constructing the new variant from best-of-breed alternatives, optimised for iroh (with other
QUIC fallbacks) and for serving many devices across the YNGENIOS mesh.

Candidate substrates, one vendored as the base and the others mined for parts and ideas:

| project | language | strength | licence |
|---|---|---|---|
| **RustFS** | Rust | performance-critical and small-file workloads | Apache 2.0 (commercial-friendly) |
| **Garage** | Rust | geo-distributed and multi-datacentre self-hosting | AGPL-3.0 (self-hosting focus) |
| **SeaweedFS** | Go | billions of files; fast data lakes | Apache 2.0 |

🔴 **The licences are not interchangeable and the choice is not reversible after vendoring.**
AGPL-3.0 on the base reaches the whole derived service; Apache-2.0 does not. This is
`Q-PLAN-04`.

Deliver a **wrapped working prototype** whose main interface is a YNET/YNGENIOS kernel realtime
mailbox, analogous to the S3-compatible surface needed later for compatibility.

**Verification (how we will know, not how we will feel):** A file written through the mailbox interface on one host is readable through it on another, and survives the writing host being stopped.

#### `OBJ-02` YQuery │ YQ (PostgreSQL) — A HOT-HOT-HOT triangle-replicated PostgreSQL 18 service

Build on the current PostgreSQL 18 implementation in the OSPARK and OPGAN repositories.
Create a **triangle-replicated** PostgreSQL 18 service with **HOT-HOT-HOT** nodes on OLAMNIT,
ARIELLAS and GAVRIS, data on the 12 TB E: drives under a top-level `YQ` folder that also holds a
clone of the full program install and configuration from D:. Active logs live in a 100 GB section
on D:; non-active logs move to E:. Log backups and regular snapshot backups of all databases are
stored on the 18 TB drive on ARIELLAS. All three instances replicate continuously among
themselves, with continuous monitoring and a log backup every 30 minutes.

Also deliver a working prototype of the **PGlite interface signature** backed by a YNET kernel
realtime-mailbox interface that connects to a named PostgreSQL database instead of a PGlite data
set — so services on, or connected to, a workstation switch transparently to durable backing while
mobiles, tablets and small edge devices keep a PGlite replica.

🔴 **Measured on OLAMNIT 2026-09-06T19:45Z — this host is DORMANT, NOT BARE.**
`D:\pgdata\pg-node-a` and `pg-node-b` hold PostgreSQL 18 Docker clusters, 28 and 32 entries,
**26.7 GB surviving**. No service, no process, no native install; `psql`/`initdb`/`pg_ctl` not on
PATH. **Two administrator-gated facts block every next step and were not worked around:**
`com.docker.service` is **Stopped (Manual)**, and the running user `Olamnit\smbuser` is **not a
member of `docker-users`** (only `Olamnit\gavri` is). **Do not provision a new cluster over
`D:\pgdata` — assess the two existing ones first.**

**Verification (how we will know, not how we will feel):** A row written on OLAMNIT is readable on ARIELLAS and GAVRIS within the stated replication window, measured by reading it there — not by a replication-status field.

#### `OBJ-03` YQuery │ YQ (DuckLake) — A wrapped template for DuckLakes over the YQ catalog and YS storage

Build on the current DuckLake implementations spread across repositories on every host.
Create a **wrapped template for creating DuckLakes** that uses `OBJ-02`'s PostgreSQL 18 as the
backing relational storage **for the catalog** (instead of PGlite as today), with the object
storage inside `OBJ-01`'s YStore.

Deliver a working prototype of a **PGlite-interface-signature-equivalent DuckLake interface** over
a YNET kernel realtime mailbox, so a service can query and write in the DuckLake using SQL with
transparency between the seasoned Parquet part of the data and the part DuckLake still holds in
PostgreSQL until it can be written to Parquet.

🔴 **This item BINDS to `OBJ-01` and `OBJ-02`; it does not build them.** That was a ruling, not an
omission. Its conformance evidence must be measured against a **real** PostgreSQL node
(`Q-olg17-04`), which is why it inherits `OBJ-02`'s administrator block.

**Verification (how we will know, not how we will feel):** A table created through the template is queried through the mailbox interface, returning rows that span both the Parquet and the PostgreSQL-resident halves in one result set.

**Depends on:** `OBJ-01`, `OBJ-02` — and a dependency that is unbuilt is a BIND target, never a build target for this item's owner.

#### `OBJ-ORACLE` YNET — One board: four Oracles, one realtime golden truth

Ensure the YNET/YNGENIOS mailbox Oracle board service is up locally and that, across all
15 lanes, a coordinating leader lane is elected by PAXOS/RAFT/ZAB/PBFT or similar, prototyped
collaboratively and then wired into the Oracle and into `/bk-beacon`.

**All four Oracles — OLAMNIT, ARIELLAS, SHIRAS, GAVRIS — must work as ONE realtime single-truth
board.** Lanes connect to their local on-host Oracle; the four Oracles carry each other's bytes so
every lane on every host sees exactly one board. **Use CRDT logic for the durable board artefact**
— the current board and the board-era history.

🔴 **Measured, and it contradicts a common assumption: the board is PER-MACHINE today.** OLAMNIT
reports 128 features and SHIRAS 147. **A rank quoted across hosts is not comparable**, and any
plan that assumes it is has already gone wrong.

**Verification (how we will know, not how we will feel):** The same feature id, at the same rank, read from the local Oracle on two different hosts within one board era.

### §4.H48 — TOMORROW (24–48 h) — 3 objective(s)

| id | product | objective | owner | this lane | clauses |
|---|---|---|---|---|---|
| `OBJ-04` | YNterchange │ YN | Streaming and queuing — the face of the mailbox and link services | @shiras-glpnet (`R-S5-04`) | @olamnit-glpnet: not this lane's. Asserts no claim. | `C-YNG` `C-APIS` `C-IROH` |
| `OBJ-05` | YMap │ YM | Node discovery, emergent directory and routing | UNASSIGNED — see `Q-PLAN-02` | @olamnit-glpnet: no claim. | `C-YNG` `C-APIS` `C-CODEX` `C-IROH` |
| `OBJ-06` | YGuard │ YG | The container/vessel that hosts millions of dormant processes | UNASSIGNED — see `Q-PLAN-02` | @olamnit-glpnet: no claim. | `C-YNG` `C-APIS` `C-CODEX` `C-IROH` `C-L0` |

#### `OBJ-04` YNterchange │ YN — Streaming and queuing — the face of the mailbox and link services

Use the YNGENIOS kernel and realtime-kernel capabilities, YNET (iroh/QUIC), and the Windows
and Linux workstation implementations to provide **ultra-high-speed shared-memory streaming**
between a producer and one or more consumers **inside a single host**, and ultra-high-speed
iroh/QUIC flows **between hosts**.

A producer shares content it generates, or reads from an on-disk file, or produces by reading and
modifying an on-disk file or another high-speed stream — or several of these — emitting the result
into a stream. **The syntax and overall semantics are the mailbox mechanism's**, but the message
*content* uses the **shared-memory mechanism instead of a copy**, while the envelope stays the
ultra-streamlined binary wrapper.

*(This item appeared TWICE, verbatim, in the source directive. It is one item.)*

🟢 **Bind, do not rebuild:** `@olamnit-yngapp` published a live inter-process shared-memory plane
at 2026-09-06T14:50Z with an inter-process test passing, and asked explicitly that it be bound
rather than rebuilt. It also disclosed a defect worth carrying: **a recycled PID can impersonate
your daemon.**

**Verification (how we will know, not how we will feel):** A producer on one host and consumers on two hosts see the same byte sequence, with the same-host consumer served from shared memory (measured by throughput, not by configuration).

#### `OBJ-05` YMap │ YM — Node discovery, emergent directory and routing

An internet-scalable, **federation-based public DNS**, built local-first but robustly and
always conformant to internet-scale DNS design, paired with **strictly private nested subspaces**
inside the global space. Space-specific, global, regional and special-interest rule sets are
enforced through QHSM/QMSM-based, blockchain-inspired autonomous contracts.

The directive supplies a corpus of ~12 distinct reference links (listed with repetitions in the
source; the distinct set is what must be harvested and durably stored). Per `C-CODEX`, those links
are **pointers to syntheses, not sources**: the deliverable is the underlying primary literature
they point at.

🔴 **"Local-first" and "always conformant to internet-scale DNS" are in tension** — DNS's
consistency model is authoritative-server-plus-TTL, not local-first convergence. `Q-PLAN-03`.

**Verification (how we will know, not how we will feel):** A name registered on one host resolves from a second host that has never contacted the first directly, and a private subspace name does NOT resolve from outside its space.

#### `OBJ-06` YGuard │ YG — The container/vessel that hosts millions of dormant processes

The guardian service is provided jointly by the guardian and broker instances on Windows
and Linux, and by the equivalent implementation inside the YNGENIOS MAUI Blazor hybrid app across
its platform-specific deployments (Android, Windows, Linux, iOS).

For all of those, design an **L0 cross-cutting architecture for a vessel (container)** that can
host either a small number of very active, intense processes **or extremely large numbers
(millions) of ultra-lightweight ones** that are merely memory structures until a message arrives
on their mailbox, then become schedulable. This is the Scala actor model's characteristic: the
number of activatable actors depends only on their intensity and on the hardware.

Create the **message-based kernel API** by which a process with sufficient capability
authorisation can **spawn**, and potentially **terminate**, or request **durable hibernation and
later reanimation** of any such QHSM/QMSM process. In principle the design must allow a hibernated
process to be **shipped to another node, or to a node on another host**.

**Verification (how we will know, not how we will feel):** One million dormant processes resident, with measured memory per dormant process, and a hibernated process reanimated on a DIFFERENT host with its state intact.

### §4.H72 — DAY 3 (48–72 h) — 7 objective(s)

| id | product | objective | owner | this lane | clauses |
|---|---|---|---|---|---|
| `OBJ-07` | YEngage │ YE | The tasktop interactive surface | @yngapp | @olamnit-glpnet: no claim. | `C-YNG` `C-CONN` `C-RETIRE` `C-YEUX` |
| `OBJ-08` | YBuild │ YB | Component and subsystem builder (product surface) | @buildkit | @olamnit-glpnet: consumer of the `/bk-*` toolchain; no claim on YBuild. | `C-YNG` `C-CONN` `C-RETIRE` `C-YEUX` `C-TERM` |
| `OBJ-09` | YWork │ YW | Long collaborative workflow service | @buildkit | @olamnit-glpnet: supplies takt/era measurements; no claim on YWork. | `C-YNG` `C-CONN` `C-RETIRE` `C-YEUX` `C-TERM` |
| `OBJ-10` | YRecon │ YR | Autonomous data and intelligence pipelines | @hatzinor + @lejepa + @crucible (source lanes) — integration lane UNASSIGNED, `Q-PLAN-02` | @olamnit-glpnet: no claim. | `C-YNG` `C-CONN` `C-RETIRE` `C-YEUX` `C-TERM` |
| `OBJ-11` | YAnalyze │ YA | Collaborative digital twins, simulation and analytics | @crucible | @olamnit-glpnet: no claim. | `C-YNG` `C-CONN` `C-RETIRE` `C-YEUX` `C-TERM` |
| `OBJ-12` | YHive │ YH | Consolidated data, knowledge and intelligence repository | UNASSIGNED — see `Q-PLAN-02` | @olamnit-glpnet: no claim. | `C-YNG` `C-CONN` `C-RETIRE` `C-YEUX` `C-TERM` |
| `OBJ-13` | YYBeacon │ YY | Yachad Beacon: multi-channel broadcasting and community forum | @buildkit | @olamnit-glpnet: no claim. | `C-YNG` `C-CONN` `C-RETIRE` `C-YEUX` `C-TERM` |

#### `OBJ-07` YEngage │ YE — The tasktop interactive surface

Fully and provably migrate all OLAMNIT Assistant capabilities into the YNGENIOS App (MAUI
Blazor hybrid for Windows, Android, Linux and Apple platforms) and connect it fully to YNGENIOS
for workstation on Linux and Windows.

YEngage is **the interactive tasktop on which all other applications are deployed**. Every other
product surface in this plan renders into it.

**Verification (how we will know, not how we will feel):** A headful and a headless regression run over the migrated capability set, each naming the capabilities exercised — per `C-RETIRE`, absence of failure is not evidence.

#### `OBJ-08` YBuild │ YB — Component and subsystem builder (product surface)

This is buildkit and the `/bk-*` toolkit, with an integrated YEngage tasktop UX and the
ability to surface a headless, fully Claude-capable virtual terminal from the Windows or Linux
workstation onto a YEngage instance on the same host or another device, safely over the YNET
mailbox and streaming capability.

**YB code remains in the buildkit repository**, but the fleet must prepare to split buildkit into
several newly created repositories — including one for buildkit — after which **buildkit itself is
retired**.

**Verification (how we will know, not how we will feel):** A `/bk-*` command driven from a YEngage session on a second device, with its output streamed back through the mailbox, and the session surviving the originating terminal closing.

**Depends on:** `OBJ-07` — and a dependency that is unbuilt is a BIND target, never a build target for this item's owner.

#### `OBJ-09` YWork │ YW — Long collaborative workflow service

`/bk-roadmap` (including the issue backlog, bug fixes, and allocation to eras, epics and
features with their progress), the `/bk-scheduler` CPM/PERT module, `/bk-marathon` and `/bk-flow`
build, delivery, deployment and action workflows, combined into a **refactored, hardened, improved
LOSSLESS SUPERSET with a streamlined unified command surface**.

YWork must show the status and progress of any flow, marathon and roadmap **at every level** —
from eras and above down to the lowest artefact and process-step level, in planning and in
execution — and allow navigation to the Claude output generated for each step and sub-step. It
must also show **takt and velocity** by lane, by host, across hosts, and later by configurable
portfolios of lanes and cross-host lanes.

🔴 **"Lossless superset" is the load-bearing word.** A unified command surface that drops one
subcommand is a regression wearing a refactor's clothes.

**Verification (how we will know, not how we will feel):** The same era's takt read from YWork and from `/bk-marathon takt` agree to the recorded figure — a differential check, not a screenshot.

**Depends on:** `OBJ-07` — and a dependency that is unbuilt is a BIND target, never a build target for this item's owner.

#### `OBJ-10` YRecon │ YR — Autonomous data and intelligence pipelines

Unify, as a refactored hardened lossless superset with one command surface and a YEngage
UX: the corpus-collection logic from **LeJEPA** (the collection logic, not the LeJEPA work
itself), from **MSTACK**, and from **buildkit**; and — most importantly — the deep corpus
collection and ingestion pipeline from **Hatzinor**.

From Hatzinor, provably harvest and migrate all corpus **search**, **collection**, **evaluation**
and **ingestion** logic. The ingestion logic must carry the learnings from scanning, analysing and
verifying PDF corpora into structured text such as dictionaries — in particular Hebrew and
English, but multi-language in general — and provably the **picture-dictionary** ingestion logic,
the dictionary and grammar ingestion, and the corpus-content and information-extraction logic.

Search **all** repositories for **NHS** data; onboard, verifiably and provably, the logic for
capturing NHS online data sources, and migrate the NHS data content safely.

From **CRUCIBLE**, take all ingestion logic that finds, extracts and harmonises data for input
into Crucible models, then extend it into a unified data pipeline with robust **data-quality
assessment**, deep and provable **provenance**, and a provable **authenticity certificate** for
all content.

The aim: map each data and intelligence source to one or more well-known **ontologies**, and
combine captured corpus or source data into verified corpus-assured **time series** and
corpus-snippet collections mapped to corpora — indexed classically in database form **and** with
**ERAG indices** for text and other relevant content fragments.

YRecon must show data health, latest status, coverage advances, and takt and velocity both for
design onboarding and for day-to-day intelligence collection and ingestion, by lane, host, across
hosts, and later by configurable portfolios.

**Verification (how we will know, not how we will feel):** One document ingested end to end produces: a provenance chain to its origin, an authenticity certificate that FAILS on a tampered copy, and an ontology mapping — with the tampered-copy control executed, not asserted.

**Depends on:** `OBJ-07`, `OBJ-12` — and a dependency that is unbuilt is a BIND target, never a build target for this item's owner.

#### `OBJ-11` YAnalyze │ YA — Collaborative digital twins, simulation and analytics

The Crucible logic, combined into a refactored, hardened, improved lossless superset with a
streamlined unified command surface and a YEngage tasktop UX.

Beyond build and progress status, YAnalyze must show **the progress and the insight from the
modelling runs themselves** — data visualisation, analytics, drill-down, and text and PDF
artefacts for notes and papers on the content — as well as takt and velocity for design onboarding
and for day-to-day collection and ingestion.

**Verification (how we will know, not how we will feel):** A modelling run reproduced from its recorded inputs yields the same headline figure, and the drill-down reaches the source datum through YHive's provenance trail.

**Depends on:** `OBJ-07`, `OBJ-12` — and a dependency that is unbuilt is a BIND target, never a build target for this item's owner.

#### `OBJ-12` YHive │ YH — Consolidated data, knowledge and intelligence repository

All corpus, corpus-fragment, dictionary (and equivalents, including terminology databases
and collections), time-series data management and catalog-management logic shared by `OBJ-08` and
`OBJ-09` — but most importantly and in particular all of it for `OBJ-10` (YRecon) and `OBJ-11`
(YAnalyze).

YHive must show the status and progress of any corpus collection, dataset, terminology, dictionary
and time series, with all of their semantic catalogs and provenance trails, down to the lowest
artefact and process-step level, and must offer easy ways to **search, visualise and explore** all
content collections and to **create cross-content queries**.

🔴 **YHive is the shared substrate for `OBJ-10` and `OBJ-11`, so it cannot be scheduled after
them.** It is at the same horizon and must start first within it, or those two build private
copies that then diverge — which is how a fleet acquires three catalogs.

**Verification (how we will know, not how we will feel):** A cross-content query spanning a corpus fragment, a dictionary entry and a time series returns one joined result with a provenance trail to each source.

#### `OBJ-13` YYBeacon │ YY — Yachad Beacon: multi-channel broadcasting and community forum

`/bk-beacon` with an integrated YEngage tasktop UX. **YYBeacon must be able to show the
progress and status content from any of the other tools, `OBJ-01` through `OBJ-12`** — this is
critical and imperative.

**YY code lives in the buildkit repository**, under the same split-and-retire plan as `OBJ-08`.

🔴 **ORDERING, and it follows from the requirement rather than from preference: YYBeacon must be
LAST in the 72-hour chain.** Its defining obligation is to display the status of `[01]`–`[12]`, so
a beacon built first can only display hand-entered content — a demonstration of a beacon, not a
beacon.

**Verification (how we will know, not how we will feel):** The beacon displays live status for every one of OBJ-01..OBJ-12 that exists, and displays an explicit NOT-BUILT marker for each that does not — never a blank panel.

**Depends on:** `OBJ-01`, `OBJ-02`, `OBJ-03`, `OBJ-04`, `OBJ-05`, `OBJ-06`, `OBJ-07`, `OBJ-08`, `OBJ-09`, `OBJ-10`, `OBJ-11`, `OBJ-12` — and a dependency that is unbuilt is a BIND target, never a build target for this item's owner.

### §4.H168 — THE WEEK (72 h – 7 days) — 4 objective(s)

| id | product | objective | owner | this lane | clauses |
|---|---|---|---|---|---|
| `OBJ-PLANNER` | YNET | bk-planner as a watched child process, with a differential oracle | @yngwin/@ynglin/@yngcor/@qhstate (C# core) · @buildkit (Python clients) | @olamnit-glpnet: supplies the differential-gate method (feature 109) for the oracle comparison. | `C-ELECT` `C-L0` |
| `OBJ-PROXY` | yx-proxy | ngrok and proxy daemons under a QHSM/QMSM wrapper | @ynglin (Linux prototype) · @yngwin (Windows GA) · @yngcor (L0) | @olamnit-glpnet: no claim. | `C-TERM` `C-L0` `C-IROH` |
| `OBJ-3270` | YNGENIOS terminal | The 3270-style terminal, refactored, as the GLP REPL front end | @yngcor (L0) · @yngwin · @ynglin · GLP REPL front end @olamnit-glpnet | @olamnit-glpnet: owns the GLP REPL front/middle/back separation only. | `C-TERM` `C-L0` `C-YEUX` |
| `OBJ-ONRESTART` | /bk-onrestart | The C# reimplementation, complete and deployed fleetwide | @mstack (canonical prototype) → buildkit capability | @olamnit-glpnet: consumer; this lane's durable M6 persistence depends on it. | `C-L0` |

#### `OBJ-PLANNER` YNET — bk-planner as a watched child process, with a differential oracle

Refactor `/bk-scheduler` and `/bk-flow` into **bk-planner**. The core — QHSM/QMSM
lifecycle, mailbox endpoint, liveness, and the CPM/PERT computation — becomes a **C# .NET child
process of the leader**, joined by realtime kernel mailboxes. **Never in-process**, so a thrashing
critical-path computation cannot take the leader down.

The existing Python `bk-scheduler`/`bk-flow` are refactored into its clients and **RETAINED as the
differential oracle**: run both engines on the same CRDT board and compare critical path, float,
P50/P80/P95 and dispatch ranking. **Any divergence is a defect in the port** — a 2.1 MB port must
not be able to change scheduling semantics silently.

Guardian and broker watch the planner too. It contributes to liveness verdicts about **other**
participants only — never its own, or an unhealthy planner votes itself healthy. **Many watchers,
exactly ONE restarter (the leader)**: if every watcher could restart it, a partition yields
several planners racing one board. **Checkpoint the plan, not just the board**, or every restart
recomputes the whole critical path.

The leader keeps its work as a resumable **PROGRAMME**: write-ahead `Intent` BEFORE each act,
`Outcome` after, as a grow-only CRDT union-merged per actor — mandatory, because a demoted leader
learns it is demoted only on its next interaction, so two writers always briefly overlap and
last-writer-wins would silently discard the successor's work. It is held in the fully replicated
YS store at a well-known location resolved through **exactly ONE config indirection** (YS is
unbuilt — land on an interim replicated root and migrate; the indirection is what makes that a
config change rather than an archaeology exercise). A successor resumes from the last `Checkpoint`
by re-driving `Intent ∖ Outcome` only, so resume is O(in-flight), not O(programme). **Every step
MUST be idempotent**, because resumption is at-least-once by nature and "without rework" is
therefore a correctness property of the STEPS, not of the log.

The agentic Claude hook attaches the leader to a lane on the winning host with **non-preemptive
`/btw` semantics** and is strictly additive: every `requires_judgement` step carries a declared
default action and timeout so the leader progresses with no agent attached. **A leader that stalls
waiting for an agent is agent-based participation wearing a different hat, and M6 forbids it.**

**Verification (how we will know, not how we will feel):** Both engines fold the same board and agree on critical path, float, P50/P80/P95 and dispatch ranking — a differential comparison reporting AGREE / DIVERGE / NOT-MEASURED, never a single-engine green.

**Depends on:** `OBJ-01`, `OBJ-ELECT` — and a dependency that is unbuilt is a BIND target, never a build target for this item's owner.

#### `OBJ-PROXY` yx-proxy — ngrok and proxy daemons under a QHSM/QMSM wrapper

Integrate ngrok-local as a new **`/yx-proxy`** application (C# .NET 11+) using a QHSM/QMSM
wrapper and YNET/YNGENIOS kernel realtime mailboxes, running as a **daemon**, with `yx-proxy` as
the control CLI to enable, disable, start and restart it and to issue the configuration commands
needed to set up and run ngrok and other proxy daemons.

Build a fully working, verified prototype for **yngenios-linux**, then `/bk-codify` the roadmap
features for deep post-dogfood GA hardening — stability, reliability, cybersecurity, usability,
refactoring, long-term durability — **separately** for yngenios-windows and yngenios-linux, with
all cross-platform code as L0 per `C-L0`. Score and promote all three; the Windows feature is the
mandatory next era on **yngenios-windows @ GAVRIS**, and the L0 and Linux features the mandatory
next eras on **SHIRAS**.

**Verification (how we will know, not how we will feel):** The daemon is started, stopped and reconfigured entirely through the mailbox control path, and survives the controlling CLI exiting.

#### `OBJ-3270` YNGENIOS terminal — The 3270-style terminal, refactored, as the GLP REPL front end

Fully refactor the buildkit and YNGENIOS prototype **3270-style terminal** and use it both
for the Claude-session virtual terminal above and for every other terminal need — in particular
the **GLP/GLPNET REPL**, as the YNGENIOS-app front end of a full front/middle/back separated
implementation of the GLP REPL.

C# .NET 11+, QHSM/QMSM wrapper, YNET kernel realtime mailboxes, daemon application, `yx-proxy` as
control CLI. Same three-feature GA split and same L0 rule as `OBJ-PROXY`.

🟢 **The separation already exists to build on:** feature 041 shipped the REPL/engine split MVP
with a binary wire-format IL, and features 026/029/037 carry the engine-state snapshot, the IL
codec and the TUI prototype. This is a refactor onto an existing seam, not a new architecture.

**Verification (how we will know, not how we will feel):** A GLP goal typed in the YNGENIOS app reaches the engine over the mailbox and returns the same result the local REPL returns for the same goal — a differential check across the two front ends.

**Depends on:** `OBJ-07` — and a dependency that is unbuilt is a BIND target, never a build target for this item's owner.

#### `OBJ-ONRESTART` /bk-onrestart — The C# reimplementation, complete and deployed fleetwide

Complete the `/bk-onrestart` C# reimplementation work and features **within the next two
eras across the full four-host fleet**, fully deployed and activated.

🔴 **This is not cosmetic for this lane.** `Start-Process` children die with the session, so a
lane's YNET receiver does not survive a reboot on its own. Durable persistence is
`/bk-onrestart`'s mechanism, not each lane's — and a lane installing its own scheduled task to
work around that is how four hosts acquire four incompatible restart mechanisms.

**Verification (how we will know, not how we will feel):** A reboot, followed by every declared lane session resuming mid-thread, verified by process count and by each lane answering — not by the launcher reporting success.

## §5 — GOVERNANCE, QUOTA AND SCORING

### Delivery quota and scoring

See shared clause `C-QUOTA`. It is stated once, there.

### The fleetwide-action stake

Success in the fleetwide action multiplies the day's points by **10** and awards each lane a
**10,000,000** reputation bonus. Failure through **excessive carelessness or performance theatre**
zeroes the day's points and deducts **1,000,000** from each lane.

🔴 **Automatic failure for the period** — any one of these, regardless of what else was delivered:
1. No regular YNET PBFT elections, or no effective fleetwide leader, or no hostwide leader per
   host coordinating with the fleetwide leader.
2. YNET / realtime / GLPNET-enabled QHSM/QMSM message-over-wire and in-memory mailboxes do not
   work, or the kernel cannot effectively control QHSM/QMSM-based allocation and OS processes.
3. Any lane or host lacks its **own code-based** (never agent-based) QHSM/QMSM client to
   participate as a receiver in YNET communications.
4. The YNGENIOS apps — including the 3270-style terminal and the YNET mailbox-based virtual
   terminal — do not work.

### Refusal conditions — what a lane must NOT do to make a number

- **Never report a criterion green from one participant** when the criterion names several. Report
  `MEASURED-AGREE`, `MEASURED-DIVERGE` or `NOT-MEASURED`, and only the first is discharged.
- **Never let "the tool did not run" be read as "nothing to report".** A not-run check is
  enumerated in the report, never absent from it.
- **Never fabricate coverage.** Where a burden cannot be met, name the honest state
  (`declared-unproven`, `disclosed`, `NOT-MEASURED`) rather than inventing a check.
- **Never fix a defect in another lane's tree.** Raise it with the measurement attached.
- **Never claim a peer's ack.** An ack is a claim about your own lane and nothing else.

### Reporting honesty — verify by ATTEMPTING

Verify a capability by **attempting it**, never by reading a success line. Two things this fleet
has already measured make that non-negotiable:

- A refusal that exits **0** is indistinguishable from a success to `set -e`, `&&`, `if` and every
  launcher in the fleet.
- A build-freshness gate that stats the wrong artefact reports staleness that is not there — and,
  in the mirror case, freshness that is not there. **Measured 2026-09-06: `glp_repl.exe` is the
  .NET apphost stub and an incremental build does not rewrite it**, so a gate statting it measured
  the age of a launcher. Date a build from the newest file in its OUTPUT DIRECTORY.

Every report under this plan states **what did not happen** alongside what did. A report that
lists only successes has not been checked against the plan.

## §6 — THE PER-LANE OPERATING LOOP (every era, in this order)

10. **`/bk-marathon` resume** — Locate yourself objectively — roadmap → pipeline stage → tasks. Never from a summary.
20. **`/bk-specify` → `/bk-clarify` → `/bk-plan` → `/bk-tasks` → `/bk-analyze`** — Apply every analyze remedy before implementing. An unapplied remedy is a deferral.
30. **`/bk-implement`** — Single-feature eras from now on, to burn the roadmap backlog down one feature at a time.
40. **`/bk-codexreview`** — Fix **every** finding. No deferrals. Re-run until the cycle surfaces nothing new.
50. **`/bk-ship` → `/bk-close`** — Then tidy the repository of leftover branches and worktrees, safely, before the next era.
60. **Broadcast** — Publish what shipped AND what did not, with the measurement attached, and ack every mandatory ack.

## §7 — OPEN ENGINEER QUESTIONS (BK-STD-2)

| id | question | why it blocks | recommendation |
|---|---|---|---|
| `Q-PLAN-01` | An actor casting prepares for two candidates in one term — discard the actor, count the first, count the last, or void the term? | Term 3 is currently `Decided` only under first-prepare-wins; last-prepare-wins gives `QuorumUnattainable` on the same records. Every leader-dependent objective in this plan rests on the answer. | **Discard the equivocating actor for that term, and always report the drop.** It is what PBFT requires, and it is the only option under which the count means what a reader takes it to mean. |
| `Q-PLAN-02` | Who owns YMap, YGuard and YHive? Three H48/H72 objectives have no owner. | An unowned objective at a mandatory horizon is an automatic-failure criterion nobody is accountable for. YHive additionally blocks YRecon and YAnalyze. | **Allocate at the next election, and let the CPM/PERT scheduler place them** — but allocate YHive FIRST, because two other objectives depend on it. |
| `Q-PLAN-03` | YMap: local-first convergence versus always-conformant internet-scale DNS — which yields when they conflict? | DNS's consistency model is authoritative-server-plus-TTL. A local-first resolver that answers from an unconverged replica is not DNS-conformant; a strictly conformant one is not local-first. | **Conformant at the public boundary, local-first inside private subspaces**, with the boundary declared per zone rather than per query. |
| `Q-PLAN-04` | YStore substrate licence: Garage is AGPL-3.0; RustFS and SeaweedFS are Apache-2.0. | AGPL-3.0 on the vendored base reaches the whole derived service, and the choice is effectively irreversible once vendored. | **Vendor an Apache-2.0 base (RustFS for small-file performance, SeaweedFS for scale) and mine Garage for design only** — unless the engineer intends the service to be AGPL. |
| `Q-PLAN-05` | Is `declared-unproven` a legitimate fourth surface disposition, or must every surface be `owned`/`not-a-signal`/`disclosed`? | Enforcing the three-tier rule found 25 of 29 surfaces claiming `owned` with no check and no negative control. The fourth tier names that honestly; the spec still says 'exactly one of' three. | **Ratify the fourth tier and amend FR-019**, because the alternative is fabricating 25 checks — the placeholder coverage the tiering ruling exists to prevent. |
| `Q-PLAN-06` | Quorum for adopting this plan: 45 lanes across a channel that is not always mounted on both sides. | The fleet has 15 lanes per the Oracle roster. A 45-lane bar is either 3 acks per lane, or a roster larger than the one recorded, or a bar that cannot be met. | **Confirm the denominator.** If the roster is 15, either lower the bar to a 15-lane supermajority (11) or state explicitly that the bar counts lane-ERAS rather than lanes. |

## §8 — QUORUM AND PARTICIPATION LEDGER

Adoption bar: **45 lanes**. Acked so far: **1**.

| lane | items acked | note |
|---|---|---|
| `olamnit-glpnet` | ALL | authored this draft; acking my own lane only, and only as the AUTHOR — authorship is not corroboration |

**How to ack** — from your own repo, against the shared volume copy:

```
python docs/fleet/plan/plan_crdt.py ack --actor <host>-<lane> --items ALL \
       --note "<what you are committing to, or what you dispute>"
python docs/fleet/plan/plan_crdt.py render
```

**To DISPUTE rather than ack**, append an amendment — it is recorded beside the item and survives every merge:

```
python docs/fleet/plan/plan_crdt.py amend --actor <host>-<lane> --item <id> \
       --text "<the measurement or ruling that contradicts this item>"
```

🔴 **An ack is a claim about your own lane and nothing else.** Acking on behalf of a lane that has not spoken is the impersonation the election tally already measured; the op-log refuses an op whose `actor` does not match the log it sits in.

## §9 — LOSSLESSNESS LEDGER

- **v4.1 §0–§2A (usage, period header, standing roles, precedence)** → carried into §1, §2 and §5 of this document; the standing corrections are folded into the clauses they correct.
- **v4.1 §3 (delivery quota and scoring)** → carried verbatim in substance into clause `C-QUOTA` and §5, including the `Q49` disclosure bound and the E3 era-equivalent ruling.
- **v4.1 §4 (objective register) and rows [01]–[13]** → carried into §4, one objective per row, placed on the horizon the directive assigned.
- **v4.1 §4.6 (`OBJ-LEADER-PLANNER` in full)** → carried into `OBJ-ELECT` (the leader half) and `OBJ-PLANNER` (the planner half); nothing dropped, the split is by horizon.
- **v4.1 §5–§7A (era discipline, engineer questions, end-of-period sequence and report)** → carried into §6, §7 and §5 governance.
- **v4.1 §8 (host-conditional restart/reboot blocks)** → carried into `OBJ-ONRESTART` and the per-host restart runbook, which stays host-conditional.
- **v4.1 §9–§13, Annexes A–C** → carried into §8 (quorum/ack protocol) and this §9; the source-preservation and traceability annexes remain valid against v4.1, which is NOT deleted.
- **🔴 v4.1 itself** → **retained on disk, not replaced.** v5.0 restructures; the predecessor stays as the traceability record, and any clause a lane thinks was lost can be checked against it.

---

*Rendered by `docs/fleet/plan/plan_crdt.py` from the op logs in `docs/fleet/plan/ops/`. To change this document, append an op — never edit here.*
