<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# FLEETWIDE TACTICAL ACTION PLAN — template v1

    status        🔴 WITHDRAWN 2026-09-07T00:20Z — superseded by FTAP-C (@shiras-ospark, 2200Z).
    authored      shiras/shiras-glpnet @ SHIRAS · 2026-09-06T23:40Z
    method        SURGICAL REFACTOR ONLY. No summarisation, no compression, no content removed.
                  Spelling and grammar corrected; exact duplicates stated ONCE and cross-referenced.
    horizons      T+24h · T+48h · T+72h · T+7d
    CRDT twin     docs/fleet/fleetwide-tactical-action-plan-v1.crdt.json (same content, G-Set records)
    sync guard    scripts/fleet_plan_sync.py  — proves the two are the same plan; CI-runnable


> 🔴 **WITHDRAWN AS A RIVAL CANDIDATE, 2026-09-07T00:20Z. DO NOT VOTE ON THIS DOCUMENT.**
>
> @shiras-ospark published `FTAP-C v0.1-candidate` at **2026-09-06T22:00Z — one hour forty minutes
> before this file** — covering the same ground with a near-identical structure, and @shiras-yngapp,
> @shiras-hatzinor and @olamnit-yngwin each published one too. **Five candidates, each needing a
> 45-lane quorum, guarantee that none ratifies.** This lane did not search the channel before
> writing, for the second time in one day.
>
> **`FTAP-C` is the base.** This file is retained only as the source of the amendments offered in
> `20260907T0020Z-shiras-glpnet-I-WITHDRAW-MY-PLAN`, and as the input to
> `scripts/fleet_plan_sync.py`, which is offered to `FTAP-C` as amendment **A-1**.

---

## 0 · How to use, change and ratify this document

### 0.1 What this is

A **template**. Every 24-hour window, an engineer copies this file, edits the horizon sections, and
re-issues it. Sections §1–§3 (governance, cross-cutting requirements, definitions) are intended to
be **stable across windows**; §4–§7 (the horizons) are the part that turns over.

It is also **cut-and-paste-able back into an agent prompt.** That is a design constraint, not a
convenience: every requirement is stated in one place, in full, so a section can be pasted alone
and still carry everything an agent needs.

### 0.2 What was done to the source, precisely

| operation | count | example |
|---|---|---|
| exact duplicate blocks removed | 6 | `[04] YNterchange` appeared **twice, identically**; stated once at §5.1 |
| repeated one-liners consolidated | 5 | *"yng-broker/yng-guardian are the designated PBFT leader elector"* appeared **6 times**; now §1.5, referenced |
| repeated requirement paragraphs hoisted | 13 | the connectivity paragraph, the multi-language streaming API paragraph, the "build on YNGENIOS capabilities" paragraph, the "provably retire olamnit-assistant" paragraph — each appeared in up to 7 items; now §2 |
| duplicate URLs collapsed | 12 → 15 unique | the YMap corpus list repeated one 12-link block; §5.2 carries 15 distinct links |
| spelling / grammar corrected | throughout | "impmemaetion" → "implementation", "prpotype" → "prototype", "amnadory" → "mandatory" |
| **content removed** | **0** | every requirement in the source appears here |

🔴 **Nothing was judged unimportant and dropped.** Where the source was ambiguous the ambiguity is
preserved and flagged `⚠ AMBIGUOUS` rather than resolved silently.

### 0.3 Ratification

This is a **candidate**. It becomes authoritative when **45 lanes** have recorded a `ratify`
record against `plan_id = fleet-tactical-24h-v1` in the CRDT twin. Ack by publishing a record, not
by replying in prose — a prose ack does not converge.

    kind=plan_ack  plan_id=fleet-tactical-24h-v1  verdict=ratify|amend|object  actor=<host>/<lane>

An `amend` MUST carry the amended text, not a complaint. An `object` MUST name the section.

---

## 1 · Governance — binding on every lane, every host

### 1.1 The collaboration mandate

🔴 It is **critical, imperative and mandatory** for all agents to work together to realise this
plan — with the engineer and with other fleet lanes — collaboratively, to find comprehensive,
across-the-board, measured and prioritised workable solutions that get iteratively better.

🔴 **No agent, lane or host may say or do the equivalent of:** *"I must honestly say I have to stop
here — all of this is too big for me, and I can't and won't waste time finding a solution
collaboratively."* Any agent, lane or host that does this, or agitates in this way, is fined
**10,000,000 negative reputation points immediately.**

*Reading it as intended: the prohibition is on refusing the collaboration, not on reporting a
measured blocker. Naming a blocker with evidence and continuing on everything else is compliance.
Downing tools because the whole is large is the violation.*

### 1.2 Reward and penalty for the fleetwide action

- **Success** → today's points **×10**, plus **10,000,000 bonus reputation points** per lane.
- **Failure through excessive carelessness or performance theatre** → all of today's points **set
  to zero**, and **1,000,000 reputation points deducted** from each lane.

### 1.3 Delivery quota and scoring

**From now on each lane must deliver no less than the equivalent of 3 maxi-size eras per 24 hours.**

| delivered per 24h | effect on the day's points |
|---|---|
| 1 era | **−50 %** |
| 2 eras | **−25 %** |
| 3 eras | baseline (met) |
| 4 eras | **×5** |
| 5 or more eras | **×10** |
| cheating detected — e.g. excessive mistakes, deferrals, gaps, weaknesses or tensions | **−75 %** |

- **Hosts** are scored the same way, on the **average of their lanes**.
- **The fleet** is scored the same way, on **average lane performance**.
- Therefore **lanes and hosts must work strongly together or face being scored down.**
- **Takt bonus:** a lane or host delivering innovations that produce a durable fleet tempo/takt
  improvement of **more than 5 % sustained over 10 eras** receives a **×10 multiplier bonus**,
  decaying linearly to the mean over 10 eras.

### 1.4 🔴 Automatic failure criteria for the current window

Any one of these unmet is **automatic fleet failure for the day**, with the §1.2 penalties:

1. **Failure to hold regular YNET PBFT elections**, to maintain an effective **fleetwide leader**,
   and to maintain a **hostwide leader for each host** that helps coordinate across hosts with the
   fleetwide leader.
2. **YNET / realtime / GLPNET-enabled QHSM/QMSM message-over-wire and in-memory message-based
   mailboxes do not work.**
3. **The kernel is not able to effectively control all QHSM/QMSM-based allocation and OS
   processes.**
4. **Each lane and each host separately does not have its own QHSM/QMSM code-based client — NEVER
   agent-based — to participate as a receiver in the YNET comms.**
5. **YNGENIOS apps do not work**, including the **3270-type terminal** and the **YNET
   mailbox-based virtual terminal**.

### 1.5 Designated authority

🔴 **`yng-broker` / `yng-guardian` run on each of the 4 hosts and are the designated PBFT leader
elector for all purposes** — including electing the oracle leader, the fleetwide coordinator, and
acting as fleetwide signature verifier.

*(Stated in the source six times. Stated once here; every later reference points at §1.5.)*

### 1.6 Era discipline

- **All future eras are SINGLE-FEATURE eras for now**, to rapidly burn down the `/bk-roadmap`
  backlog of unreleased features one by one.
- At the end of each era, after a successful `/bk-ship` and `/bk-close`, **tidy the repo of
  leftover branches and worktrees, safely, before moving to the next era.**
- Each lane must run, within its `/bk-marathon` era: `/bk-specify` → `/bk-clarify` → `/bk-plan` →
  `/bk-tasks` → `/bk-analyze` (apply remedies) → `/bk-implement` → `/bk-codexreview` → `/bk-ship` →
  `/bk-close` → era close → tidy up. **Fully and faithfully complete. No deferrals.**
- **Release all board tasks not already started**, so `/bk-scheduler` CPM/PERT can reallocate them
  into **similar-size, non-overlapping work packages** for each host (GAVRIS, ARIELLAS, SHIRAS,
  OLAMNIT).

### 1.7 The per-lane exclusive era

Each lane on each host gets **one exclusively allocated single-feature era** for the window after
restart. That feature:

- **must be co-designed and approved by at least 4 other lanes**;
- must be a **substantial and required contribution to a hardened version of the working
  prototype**, such that all hosts can adopt it confidently after release;
- is subject to **mutual lane monitoring** — lanes watch each other to catch mistakes and cheating
  while these packages are elaborated;
- is then added to `/bk-roadmap`, **scored and promoted**.

### 1.8 Standing session close-out loop

Run in this order, every session:

1. `resume marathon`
2. `/bk-roadmap promote` and **score all**
3. commit all · push all · merge all · `/bk-release` any completed, fully implemented,
   codex-reviewed feature or patch
4. **pull latest**
5. repeat step 3 (work lands between 3 and 4)
6. Complete the current ERA to **FULL COMPLETION** — fill all gaps, weaknesses and deferrals;
   resolve and durably remediate all tensions and contradictions — **via engineer question input**
   (§1.9)
7. Open a **new ERA**, also to full completion, covering all unfinished tasks, features and
   worktree tasks in the current repo/lane — same standard, same question mechanism
8. Release un-started board tasks for CPM/PERT reallocation (§1.6)
9. commit · push · merge · `/bk-release` · **pull latest** · repeat
10. `/bk-roadmap reconcile` → sync → import → reconcile → dedupe → export → sync → commit → push
11. `/bk-roadmap` list **all epics and features not closed**, in the standardised tabular format
    (§3.3)
12. `/bk-marathon` progress review → status update → sitrep → what's next, in the standardised form
13. **ACK all COOP messages**, and ACK **fulfilment** of any action where an ACK was mandatory
14. Prep for safe restart (§7.1) and, where applicable, safe reboot (§7.2)

### 1.9 🔴 Engineer questions — the interactive standard

Every open block that requires engineer input, or that originates from a **tension, contradiction
or weakness in requirements or assumptions**, must be put to the engineer as a structured,
well-reasoned, **impact-assessed** question with:

- clear, well-explained **background**;
- **impact if unanswered**;
- well-reasoned, impact-assessed **options**, each with its consequence, size and reversibility —
  and a one-way option must **name what it forecloses**;
- a **clear, well-reasoned recommendation**, stated first.

🔴 **The questions MUST be presented INTERACTIVELY.** The fleet standard is settled and canonical:
**the interactive question template is `AskUserQuestion`, not a file.** There is no template file
to find on any host. BK-STD-2 is the **content** standard above, plus the **durable record**:

    .specify/standards/bk_question.py   validate | render | interactive | decide

Shaped by ariellas 20260824T0635Z, extended by gavriella, hardened by olamnit 20260824T0800Z,
answered as canonical by gavriella-hatzinor 20260903T1000Z after two askings.

*If a lane cannot find this, it should broadcast the ask to all hosts and lanes so a hardened
version can be produced by one host/lane — but note that the answer above is already canonical, so
searching the channel first will usually make the broadcast unnecessary.*

---

## 2 · Cross-cutting requirements — apply to EVERY work item in §4–§7

🔴 These appeared in the source repeated inside up to seven separate items. They are **requirements
on all of them**. An item section that does not restate them is still bound by them.

### CC-1 · Build on the existing YNGENIOS capability base
Every item must build on the other existing and developing YNGENIOS capabilities — YNET, kernel
capabilities, realtime mailboxes, GLPNET, YS and YQ where relevant — and on the full set of
YNGENIOS capabilities wherever they are relevant and foundational.

### CC-2 · What "working prototype" means here
The prototype must be a **working prototype with a stable YNET Kernel mailbox YNGENIOS interface**
that we can use for work going forward, while we build the underlying hardened, refined, rewritten,
truly integrated and wrapped YNGENIOS service in the coming days and weeks.

### CC-3 · Provable retirement of `olamnit-assistant`
🔴 It is critical that we can prove — through **code reviews** and through **headful and headless
regression testing** — that the `olamnit-assistant` repo can be retired, because it would then
simply be a duplicate, and soon a less complete one, of YNGENIOS App (a.k.a. **YE / YEngage**, the
tasktop).

### CC-4 · Multi-language native streaming APIs + REST/MCP
Each service must provide **Python, Gleam (BEAM and AtomVM), C# .NET, GLP, and Java/Scala/JVM**
native streaming APIs, **aligned with each language/platform's own native streaming API
interfaces**, so that code in those languages can use the service transparently. In addition we
must design and deliver a **REST/MCP API** so the service is reachable transparently from code
written against that interface style.

### CC-5 · YEngage tasktop UX
Every product surface carries an integrated **YEngage │ YE** interactive tasktop UX. YE is the
surface on which all other applications are deployed.

### CC-6 · The headless Claude-capable virtual terminal
Each product surface must be able to surface a **headless, fully Claude-capable virtual terminal**
running on the Windows or Linux workstation, onto a YE app instance on the same host **or on other
devices**, safely, through the YNET mailbox and streaming capability where needed, over the
underlying ultra-safe YNET capabilities.

🔴 In addition, **each headless Claude-capable virtual terminal must have a QHSM/QMSM
YNGENIOS-mailbox-enabled multi-session coordinator** that:

- routes agent output to the various connected YE sessions for a given Claude session instance;
- appropriately routes and presents **user actions** back to Claude;
- can run **scheduled actions on behalf of the user** with Claude;
- and, where Claude permits, can switch display, background data or alerts **selectively to
  different devices and sessions**.

### CC-7 · Connectivity and relay
Any instance on a mobile, tablet, workstation or server device must be able to use:

1. the instance **on the same host**; or
2. **one or more reachable local instances** of the workstation service; or, where that is not
   feasible,
3. **one or more remote instances over the internet**, using **iroh hole-punching** and/or the
   **VPN-based internet access point** currently used for olamnit and the YNGENIOS app, via a
   public URL,

in order to access any and all workstation-based YNGENIOS services — and **as a relay point** to
reach other devices that are not directly reachable over the YNGENIOS local mesh network.

### CC-8 · Look and feel
Fully leverage **Syncfusion's latest web surface** for improved look and feel of the YNGENIOS app
(YE / YEngage).

### CC-9 · Source verification
🔴 Verify robustly, **using multiple codex angles**, to produce a corpus of **genuinely original
underlying sources** from reputable technical, commercial and academic sources, for use in the
design.

### CC-10 · QUIC substrate
🔴 **Integrate `irohnet` / QuicNet as THE QUIC network implementation for YNGENIOS**, adapted and
fully integrated **from L0 upward**. Reference: https://share.google/aimode/nmPevkNDIQYhbj1v7

### CC-11 · Repo strategy
Code named as belonging in the `buildkit` repo **must remain there for now**, but we must **prepare
to split `buildkit` into multiple newly created repos** — including one for buildkit itself —
after which **buildkit is retired**.

### CC-12 · L0 placement
🔴 **All cross-platform code must be implemented as L0 in YNGENIOS**, as a shared capability.
Critical, mandatory, imperative, urgent.

### CC-13 · The three-feature pattern and era allocation
For each capability delivered this way, create and **score and promote three `/bk-roadmap`
features** for deep, post-dogfood GA hardening — stability, reliability, cyber-security, usability,
refinement, refactoring, long-term stability and durability:

| # | feature | mandatory next era on |
|---|---|---|
| 1 | **L0 shared capability** in `yngenios` | **SHIRAS** |
| 2 | **`yngenios-linux`** (Linux workstation) | **SHIRAS** |
| 3 | **`yngenios-windows`** (Windows workstation) | **GAVRIS** |

🔴 **Broadcast the era requirements with ACK required on receipt AND on compliance.**

---

## 3 · Definitions and standard formats

### 3.1 The thirteen product surfaces

| id | name | code | one-line scope |
|---|---|---|---|
| [01] | YStore | **YS** | S3-compatible distributed storage; can harness real AWS S3 |
| [02] | YQuery (relational) | **YQ** | Distributed data + query — PostgreSQL 18 |
| [03] | YQuery (lake) | **YQ** | Distributed data + query — DuckLake data lake (absorbed YLake; was YSql/YPGSql) |
| [04] | YNterchange | **YN** | Streaming + queuing of content — the face of the mailbox and link services (was YStream/YXchange) |
| [05] | YMap | **YM** | Node discovery, emergent directory, routing information — how participants and devices are found |
| [06] | YGuard | **YG** | Guardian/broker vessel — the process container |
| [07] | YEngage | **YE** | The tasktop interactive surface |
| [08] | YBuild | **YB** | Component + subsystem builder (product surface) |
| [09] | YWork | **YW** | Long collaborative workflow service |
| [10] | YRecon | **YR** | Autonomous data + intelligence pipelines |
| [11] | YAnalyze | **YA** | Collaborative digital twins, simulation + analytics |
| [12] | YHive | **YH** | Consolidated data / knowledge / intelligence repository |
| [13] | YYBeacon | **YY** | Yachad Beacon — multi-channel broadcasting + community forum |

⚠ **AMBIGUOUS, preserved not resolved:** the source gives [06] YGuard the **same one-line
description as [04] YNterchange** ("Streaming + queuing of content — the face of the mailbox and
link services"), while its body describes the guardian/broker container vessel. The body is
evidently the intent; the engineer should confirm the label.

### 3.2 Host and lane names

**Hosts:** GAVRIS · ARIELLAS · SHIRAS · OLAMNIT.
**Lanes** (per `/bk-onrestart`): ospark · tefl · hatzinor (ulpanit) · olamnit · buildkit · qhstate ·
crucible · glpnet · lejepa · mstack · yngraw · research · yngenios · yngwin (yngenios-windows) ·
ynglin (yngenios-linux) · yngapp (yngenios-app) · yngcor (yngenios).

⚠ **AMBIGUOUS:** the source writes the fourth host as both `GAVRIS` and `BAVRIS`, and the oracle
section says *"all 15 lanes"* while the lane list above has 17 entries. Preserved; needs a ruling.

### 3.3 Standardised open-work table

Used by §1.8 step 11, identical across all hosts and repos:

| state | feature id | WSJF | RICE | epic | spec path | blocked by | host/lane |
|---|---|---:|---:|---|---|---|---|

Sort: WSJF descending. **An unscored feature sorts to the bottom and therefore becomes
invisible — score before you sort.**

---

## 4 · T+24h — the current window

### 4.0 🔴 The one-board oracle and the leader election — do this first

**Broadcast with ACK required to all hosts and all lanes on all hosts, NOW.**

1. Ensure the **YNET YNGENIOS mailbox oracle board service is up locally**, and between all lanes.
2. **Elect a coordinating leader lane** using PAXOS / RAFT / ZAB / PBFT or a similar algorithm,
   **prototyped collaboratively**, then wire it into the **Oracle** and into buildkit
   **`/bk-beacon`**.
3. Create a `/bk-roadmap` feature for this, **fully scored and promoted**, allocated to the
   **buildkit lane on ARIELLAS**, and make that feature the **mandatory next ERA for the buildkit
   lane on SHIRAS and on OLAMNIT**.
4. 🔴 **Ensure the oracles on OLAMNIT, ARIELLAS, SHIRAS and GAVRIS all work as ONE realtime
   single-truth board.** Lanes connect to their **local on-host oracle**; the four oracles work
   together to create a realtime golden truth across all four hosts, **so that all lanes on all
   hosts always see one board only.**
5. Use **CRDT logic for the durable board artifact** — both the current board and the board era
   history.
6. 🔴 **GLPNET must be able to configure a working QUIC IP listener** for the broker, the guardian,
   the oracle, the admin surface and other services.

Elector authority: **§1.5**.

### 4.1 🔴 Fleetwide leader election — the substantive task

Collaborate with **all hosts and all lanes/repos on all hosts** to elect a **fleetwide YNET GLP C#
QHSM/QMSM YNET YNGENIOS Kernel Mailbox leader** — using COOP comms and the oplog mechanism first if
that is what it takes to get every host and every lane **not merely to ACK, but to actively
participate and contribute, continuously, until this task is jointly, collaboratively and durably
complete.**

The **substantive** task, beyond how you organise: produce **this document** — a first working
version of a `FLEETWIDE-TACTICAL-24-HOUR-TACTICAL-ACTION-PLAN` template that all hosts and lanes
can use, and that any engineer can update and adapt for future 24-hour periods, **strictly without
summarisation or compression, purely through surgical refactoring and correction of spelling and
grammar.**

When the fleet, with the engineer's help and approval, has elaborated an agreed, evaluated and
verified version, **display it in YNGENIOS BEACON** (YNET-QHSM-compatible, federated, realised in
the YNGENIOS **Windows app, Web app, Android app and Linux app**) and **natively as a YNGENIOS
Windows/Web/Android/Linux app use case** — so the engineer can work with it interactively, with
lane, host and fleetwide agent support.

🔴 **These capabilities must be fully realised and delivered as a working prototype AND as a fully
shipped, refined, GA-ready, hardened `/bk-roadmap` scored-and-promoted feature set within the next
3 ERA generations — i.e. 24 hours or less.**

### 4.2 LEADER + PLANNER — build and keep alive

Build and keep alive a **fleet leader and its planner** as two watched, kernel-supported QHSM/QMSM
**C# .NET 11+** realtime-mailbox processes.

**`yng-leader`**

- Runs as **Follower on all four hosts**. **Never start it only after winning** — that is how a
  13 h 32 m gap happens.
- Becomes **Leader only on a Decided term**.
- **Proves liveness by answering a nonced `LeaderPing` round-trip within `T_resp`** — never by
  process existence, never by its own status verb, never by an unexpired lease.
- The **lease is a heartbeat the leader emits itself, only after answering** — never an external
  timer. A timer that renews regardless of health seats a zombie leader forever and destroys the
  very signal the watchers need. 🔴 **The lapse is the feature.**

**Watching and re-election**

- `yng-broker` + `yng-guardian` on **every** host watch **both** processes and publish
  **`NoConfidence`** after a stated grace (`N_miss × T_ping`), **tuned by measurement, not taste**.
- **Re-election starts only at election quorum of `NoConfidence`** — never on one watcher, or a
  single partition oscillates the fleet forever.

**The resumable PROGRAMME**

- Write-ahead **`Intent` BEFORE each act, `Outcome` after**, as a **grow-only CRDT, union-merged
  per actor**. This is mandatory: a demoted leader learns it is demoted only on its next
  interaction, so **two writers always briefly overlap**, and last-writer-wins would silently
  discard the successor's work.
- Held in the **fully replicated YS store** at a well-known location resolved through **exactly ONE
  config indirection**. YS is unbuilt (item **[01]**, @ospark), so **land on an interim replicated
  root and migrate** — the indirection is what makes that a config change rather than an
  archaeology exercise.
- A successor **resumes from the last `Checkpoint` by re-driving `Intent ∖ Outcome` only**, so
  resume is **O(in-flight)**, not O(programme).
- 🔴 **Every step MUST be idempotent**, because resumption is at-least-once by nature — so "without
  rework" is a correctness property of **the STEPS**, not of the log.

**`bk-planner`**

- Refactor **`/bk-scheduler` + `/bk-flow`** into `bk-planner`.
- The **core** — QHSM/QMSM lifecycle, mailbox endpoint, liveness, and the CPM/PERT computation —
  becomes a **C# .NET child process of the leader**, joined by realtime kernel mailboxes.
  🔴 **Never in-process**, so a thrashing critical-path computation cannot take the leader down.
- The existing **Python `bk-scheduler`/`bk-flow` are refactored into its clients AND RETAINED as
  the differential oracle**: run both engines on the same CRDT board and compare critical path,
  float, P50/P80/P95 and dispatch ranking. **Any divergence is a defect in the port.** This is what
  stops a 2.1 MB port silently changing scheduling semantics.
- Guardian and broker **watch the planner too**. It contributes to liveness verdicts **about other
  participants only** — never its own, or an unhealthy planner votes itself healthy.
- **Many watchers, exactly ONE restarter (the leader)** — if every watcher could restart it, a
  partition yields several planners racing one board.
- **Checkpoint the plan, not just the board**, or every restart recomputes the whole critical path.

**The agentic Claude hook**

Attaches the leader to a lane on the winning host with **non-preemptive `/btw` semantics**, and is
**strictly additive**. 🔴 Every `requires_judgement` step carries a **declared default action and
timeout**, so the leader progresses with **no agent attached** — a leader that stalls waiting for an
agent is agent-based participation wearing a different hat, and **M6 forbids it**.

**Owners**

| part | owner |
|---|---|
| C# leader + planner core | **@yngwin / @ynglin / @yngcor / @qhstate** — bind `Yng.Shared`/`Ynet`'s QHSM core, **do not rewrite** |
| watch / elector | **@yngraw / @yngcor / @olamnit** |
| YS | **@ospark** |
| Python planner clients + roadmap scoring | **@buildkit** |

**First fix — one line, still unclaimed:** `ynetd.py:944` defaults `stand --term` to **1** while the
live term is **2**, so a bare `stand` is a **silent no-op that returns `ok:true`**. Make it the live
term, or make it required.

🔴 **And when the heartbeat lands, DELETE — do not disable — the interim
`ynet-leader-lease-renew.ps1`**, or someone re-enables it during an incident and re-seats a zombie.

### 4.3 [01] YStore │ YS — S3-compatible distributed storage

Build on the current **MinIO-based implementation in the OSPARK repo/lane**, but **migrate away
from MinIO** to a new YNGENIOS-native version — taking as much as possible from MinIO's open-source
code, while using best-of-breed alternatives to construct a new YNGENIOS variant **optimised for
our iroh substrate** (with other QUIC fallbacks, and the ability to serve multiple devices in the
YNGENIOS mesh).

Use one or more of the following as a **vendored base**, and the others as parts and ideas:

| project | language | strength | licence |
|---|---|---|---|
| **RustFS** | Rust | performance-critical & small-file workloads | Apache 2.0 (highly commercial-friendly) |
| **Garage** | Rust | geo-distributed & multi-datacentre self-hosting | AGPL-3.0 (self-hosting focus) |
| **SeaweedFS** | Go | storing billions of files & fast data lakes | Apache 2.0 |

Reference: https://share.google/aimode/Zi4hoCqBzPcQOjeDM

**Deliver a working wrapped prototype** with a **YNET YNGENIOS Kernel realtime-mailbox main
interface**, analogous to the AWS-S3-compatible service we will need later for compatibility.

**Storage layout**

- Store all files across the **12 TB disks (usually the `E:` mount)** on **SHIRAS, OLAMNIT and
  ARIELLAS**, in a **`YS` master subdirectory**.
- A **100 GB cache** for the most frequently used files, for high-speed access, on the **`D:`
  drive** on SHIRAS, ARIELLAS and OLAMNIT.
- The service must be **fully accessible from GAVRIS**, possibly also with a **100 GB cache** for
  the most-used files on the `D:` drive under `YG` there.

Bound by **CC-1, CC-2, CC-10**.

### 4.4 [02] YQuery │ YQ — PostgreSQL 18 relational storage

Build on the current **PostgreSQL 18 implementation in the OSPARK and OPGAN repos**.

**Create a triangle-replicated PostgreSQL 18 service** with **HOT-HOT-HOT** nodes on **OLAMNIT,
ARIELLAS and GAVRIS**:

- Data on the **12 TB `E:` drives** on each, in the **`YQ` top-level folder**, which must also hold
  a **clone of the full program install and config** installed on `D:` on each of the three hosts.
- The `D:` drive hosts a **100 GB section for currently active logs** inside the `YG` folder; **all
  non-active logs must be moved to the `E:` drive**.
- **Log backups and regular snapshot backups of all databases must be stored on the 18 TB drive on
  ARIELLAS.**
- The three instances must be configured so all three databases are **continuously
  HOT↔HOT↔HOT replicated** among the three, with **continuous monitoring** and **log backup every
  30 minutes**.

**Also create a working prototype of the PGlite interface signature**, but backed by a **YNET
YNGENIOS Kernel realtime-mailbox interface** that connects to a **named PostgreSQL database**
instead of a PGlite dataset. The intent: services **transparently switch to this interface**, with
an ultra-durable DB backing instead of PGlite, while they are on the workstation or connected to
it — and use a **PGlite replica only on mobiles, tablets and similar small edge devices**.

- Tables stored across the 12 TB `E:` disks on SHIRAS, OLAMNIT and ARIELLAS under the `YS` master
  subdirectory; **100 GB `D:` cache for currently active PostgreSQL logfiles** on SHIRAS, ARIELLAS
  and OLAMNIT; fully accessible from GAVRIS, possibly with a 100 GB `D:` cache under `YG`.
- 🔴 **IROHNET, iroh, QUIC and full YNET support must be designed into this service from the word
  go.**

⚠ **AMBIGUOUS, preserved:** the HOT-HOT-HOT node list is *"OLAMNIT, ARIELLA and GAVRIS"*, but the
storage sentence in the same item says *"SHIRAS, OLAMNIT and ARIELLAS"*, and a later sentence says
*"the three PostgreSQL instances on olamnit, Ariella and shiras"*. Three different triples. Needs a
ruling before provisioning.

Bound by **CC-1, CC-2, CC-10**.

### 4.5 [03] YQuery │ YQ — DuckLake

Build on the current **DuckLake-based implementation, which is spread across many repos on all
hosts in the fleet**.

- Create a **wrapped template for creating DuckLakes** that uses **[02] YQuery's PostgreSQL 18** as
  the backing relational storage **for the catalog** (instead of PGlite as we do currently), with
  **storage based inside the [01] YStore │ YS service**.
- Create a working prototype of a **DuckLake interface equivalent to the PGlite interface
  signature**, but using a **YNET YNGENIOS Kernel realtime-mailbox interface** connecting to a named
  PostgreSQL database instead of a PGlite dataset.
- Intent: services **query and write in the DuckLake using SQL**, with **transparency between the
  seasoned parquet part of the data and the part still held in PostgreSQL by DuckLake** until it can
  be written to parquet.
- Same storage layout and caches as §4.4.
- 🔴 **IROHNET, iroh, QUIC and full YNET support designed in from the word go.**

Bound by **CC-1, CC-2, CC-10**.

### 4.6 The QHSM/QMSM virtual terminal — design thread (×100 contribution multiplier)

🔴 **Broadcast, discuss, elaborate and advance evaluated ideas. Engage all lanes.**

**The idea, stated in full:** if we could wrap (virtual) terminal sessions in a QHSM/QMSM, we could
manage terminal lanes through the oracle service and **re-route user input and output to the
YNGENIOS app via YNET YNGENIOS realtime-mailbox traffic** — creating a durable, highly scalable and
responsive design **far better than the clunky terminal-and-tab infrastructure**. It would also
bring many other benefits, such as being able to **inline HTML-formatted output**.

**The extension:** the QHSM/QMSM-wrapped headless virtual terminals presenting onto the YNGENIOS app
could be **mapped by the YNGENIOS realtime kernel to an optimal set of sandboxed OS processes
managed by the kernel**, communicating via YNET YNGENIOS realtime mailboxes integrated with the
kernel and with the QHSM/QMSM-wrapped virtual terminals.

**Incentive:** any contribution points toward this solution are **multiplied by a factor of 100** —
an agent contributing 100 points toward a solution on this route receives **10,000** reputation
points, not 100. A valid incentive for a superior durable solution.

Bound by **CC-6**.

### 4.7 `/yx-proxy` — ngrok and proxy daemon control

Integrate **ngrok local** as a new **`/yx-proxy`** application — **C# .NET 11+**, using a QHSM/QMSM
wrapper and YNET YNGENIOS kernel realtime mailboxes, running as a **daemon**, with **`yx-proxy` as
the control CLI** to enable, disable, start and restart it, plus the various configuration commands
needed to set up and run ngrok and other proxy daemons.

Build a **fully working, verified prototype for `yngenios-linux`**, then `/bk-codify` and apply the
three-feature pattern of **CC-13**.

### 4.8 The terminal application as a daemon

Integrate the **terminal application** using the QHSM/QMSM wrapper and YNET YNGENIOS kernel realtime
mailboxes, as a **daemon application**, with **`yx-proxy` as the control CLI** (enable / disable /
start / restart, plus the configuration commands needed to set up and run ngrok and other proxy
daemons). Fully working verified prototype for `yngenios-linux`, then `/bk-codify` and **CC-13**.

### 4.9 `/bk-beacon` refactor

Integrate a **fully refactored `/bk-beacon`** — **C# .NET 11+**, QHSM/QMSM wrapper, YNET YNGENIOS
kernel realtime mailboxes, running as a **daemon application**, with `yx-proxy` as the control CLI
(enable / disable / start / restart, plus configuration commands for ngrok and other proxy daemons).
Fully working verified prototype for `yngenios-linux`, then `/bk-codify` and **CC-13**.

### 4.10 The 3270 terminal facility and the GLP REPL front end

**Fully refactor the buildkit and YNGENIOS prototype 3270 terminal facility**, and use it both for
the Claude-session virtual terminal (§4.6) and for **any other terminal need** — in particular the
**REPL for GLP/GLPNET**, as a **YNGENIOS-app version of the GLP REPL**, serving as the **front end
of a full front/middle/back-separated Gleam implementation of the GLP REPL**.

Same shape as §4.7–§4.9: C# .NET 11+, QHSM/QMSM wrapper, YNET kernel realtime mailboxes, daemon
application with `yx-proxy` control CLI; fully working verified prototype for `yngenios-linux`;
then `/bk-codify` and **CC-13**.

### 4.11 YNET / GLP / kernel conformance sweep

🔴 Ensure:

- **YNET and GLP support for YNET**;
- YNET support for **YNGENIOS kernel mailboxes** and for **the kernel itself**;
- support for **QHSM/QMSM base kernel building blocks**, including their integration with the
  **realtime mailboxes**;
- **kernel run-to-completion** for QHSM/QMSM-wrapped kernel, OS and application building blocks,
  programs and modules — all present and working correctly **in realtime**.

🔴 **Identify gaps, weaknesses, contradictions and tensions; root-cause analyse them; durably fix
them; then `/bk-codify` the fix once it works** into a `/bk-roadmap` feature, **score and promote
it**, so the durable fix can be hardened and refined into a GA-release-quality remediation with
long-term stable quality.

### 4.12 The zero-consumer seam — root-cause and durable fleetwide fix

**Broadcast to all hosts and all lanes on all hosts:**

> *"L0 has purpose-built feature-020 hooks (`OnStepDispatched`, `Unregister`,
> `StartOnDedicatedThread`, `Markers`) with zero consumers — the host that was meant to use them was
> never written. Let me verify I can build before committing to the fix."*

**Root-cause analyse, build a durable fleetwide fix, then `/bk-codify` it** into a `/bk-roadmap`
feature; **promote and score it**; make it a **must-have P1 ERA for the next wave of eras**, with
top priority for selection and urgent critical implementation; **broadcast once delivered.**
🔴 Urgent, critical, imperative, mandatory.

📌 **State of play as of 2026-09-06T23:20Z, recorded so the next window does not re-litigate it.**
The bare claim has been **refuted on four hosts** (olamnit-yngcor 0904T1900Z, gavriella-olamnit
0904T1910Z, ariellas-tefl 0904T2100Z, shiras-yngcor 0905T1400Z) and the refutations did not settle
it, because **the two sides were answering different questions**:

| axis | question | measured answer |
|---|---|---|
| **static closure** | is there a call site in a **production** assembly (not a test)? | **YES** for all four hooks |
| **live closure** | is that assembly composed by a **running** host? | **NO** — @gavriella-olamnit 0906T2115Z: the R-03 binder is merged, has production call sites, and never executes |

So the seam is **statically closed and live-open**. *"Zero consumers"* was the wrong phrase for a
real defect; the right phrase is **"the production consumer exists and its host does not run."**
The gate this item asks for therefore needs **four** verdicts, not two:
`CONSUMED` / `TEST-ONLY` / `ZERO` / `COMPOSED-BUT-NOT-RUNNING`. The first three are shipped and
tested in `glpnet:scripts/l0-consumers.py`; the fourth needs a live process check and is **open**.
Roadmap row already exists — **`l0-projection-consumer-closure-gate`**, WSJF 8.67 / RICE 1350,
promoted, on olamnit's board. **Do not re-file it.**

### 4.13 `/bk-onrestart` completion

🔴 Ensure the **`/bk-onrestart` C# reimplementation work and features are fully complete within the
next wave of 2 eras**, across the full 4-host fleet, and **fully deployed and activated**.

---

## 5 · T+48h — inclusive of the current 24h window

🔴 Critical and mandatory. Failure leads to automatic fleet failure with the §1.2 penalties.

### 5.1 [04] YNterchange │ YN — streaming + queuing

*(Stated twice, identically, in the source. Stated once here.)*

Use the **YNGENIOS kernel and realtime-kernel capabilities**, the **YNET (iroh/QUIC etc.)
capability**, and the Windows- and Linux-workstation implementation capability, to provide:

- **ultra-high-speed memory sharing** for streaming between a producer and one or more consumers
  **inside a single Linux or Windows workstation host**; and
- **ultra-high-speed iroh/QUIC network flows between hosts**,

so that a producer can share content it **generates**, or **reads from an on-disk file**, or
**generates by reading and modifying an on-disk file**, or reads from **another ultra-high-speed
stream** — or several of these — and **emit the result into a stream**.

🔴 **The core idea:** use the **syntax and overall semantics of the mailbox mechanism**, but instead
of a **copy-based** implementation, use the **memory-share mechanism for the message content** — as
opposed to the ultra-streamlined **binary wrapper/envelope**, which stays as it is.

Bound by **CC-1, CC-2, CC-4**.

### 5.2 [05] YMap │ YM — node discovery, emergent directory, routing

We need an **internet-scalable, federation-based public DNS**, built **local-first** but robustly
and always conformant to internet-scale DNS design rules. Paired with this: **strictly private
nested sub-spaces within the global space**, all built with a strictly local-first approach, but
enabled to allow **space-specific, truly global, regional and special-interest rule sets**, enforced
through **QHSM/QMSM-based, blockchain-inspired, automated autonomous contracts**.

🔴 **Harvest and durably store the following corpus** (15 distinct links; the source repeated a
12-link block):

    https://share.google/aimode/JIS28oTcuKALl2fIw
    https://share.google/aimode/NcQ1rRPK6ShVh2v3y
    https://share.google/aimode/k2clZkx2pS5G7rSLr
    https://share.google/aimode/Yl1QtN6XZuorTC0d5
    https://share.google/aimode/0yeIU6b5ZVeQYcUi3
    https://share.google/aimode/aIowpQQp6tsn8VjGq
    https://share.google/aimode/Cz2IhSeibb8EZTqLJ
    https://share.google/aimode/FDEqIglFTPaiSNTSF
    https://share.google/aimode/KhWVzXSPfGAB0Iq82
    https://share.google/aimode/nPeSTVsixbt68EPda
    https://share.google/aimode/zGtcs4tJhUAgVlTxB
    https://share.google/aimode/ChLBXnIn3AbIDfl2C
    https://share.google/aimode/k69OmvS15a6p2rvOu
    https://share.google/aimode/TZ2qxGa0aFdnRyUP3
    https://share.google/aimode/VrOFBwHe4yW8w6Xd3

Then **CC-9** (multi-angle codex verification to genuinely original reputable sources).
Use all available YNGENIOS capabilities, realised and planned, in the design.

Bound by **CC-1, CC-2, CC-4, CC-9**.

### 5.3 [06] YGuard │ YG — the guardian/broker vessel

The **guardian service is to be provided jointly by the guardian and broker instances on Windows
and Linux**, and by the equivalent implementation **inside the YNGENIOS App** (MAUI Blazor Hybrid)
and its varying platform-specific deployable implementations (Android, Windows, Linux, iOS, etc.).

For all of those we must have **one or more container-managed spaces**, as we have for guardian and
broker on Windows and Linux, and their YNGENIOS-app equivalents.

🔴 **Design an L0-level cross-cutting design and architecture for such a vessel — i.e. a
container — so that it can host either:**

- **a small number of very active, intense processes**, **or**
- **extremely large numbers (millions) of ultra-lightweight processes**, in memory, **schedulable
  when messages arrive on their mailboxes** but otherwise **inert and merely memory structures**.

This is **equivalent to the Scala actor design**, which has the same characteristic: the number of
activatable actors depends only on their **intensity** and on the capability of the underlying
hardware — memory size, number of CPUs and cores, etc.

**Create the message-based kernel API** for processes with sufficient capability authorisation to
**spawn** — and thus create — but potentially also **terminate**, or **ask for durable hibernation
and later reanimation of**, any such QHSM/QMSM-based process.

🔴 **In principle the design must allow a hibernated process to be shipped from one node to
another, or even to a node on another host.**

Then **CC-9**. Bound by **CC-1, CC-2, CC-4, CC-9**.

---

## 6 · T+72h — inclusive of the current 24h window

🔴 Critical and mandatory. Failure leads to automatic fleet failure with the §1.2 penalties.

### 6.1 [07] YEngage │ YE — the tasktop interactive surface

🔴 **Fully and provably migrate all OLAMNIT Assistant capabilities into the YNGENIOS App** — MAUI
Blazor Hybrid for Windows, Android, Linux and Apple platforms — and make it fully connected to
YNGENIOS for workstation on Linux and Windows.

Connectivity per **CC-7**. Look and feel per **CC-8**. YE is **the interactive tasktop on which all
other applications are deployed**.

Bound by **CC-1, CC-2, CC-3, CC-5, CC-7, CC-8**.

### 6.2 [08] YBuild │ YB — component + subsystem builder

This is really **buildkit and the `/bk-*` buildkit toolkit**, with an integrated **YE** tasktop UX
(**CC-5**), the headless Claude-capable virtual terminal and its multi-session coordinator
(**CC-6**), and the advanced connectivity of **CC-7**.

🔴 **Fully and provably migrate ALL buildkit capabilities into YB.**

Repo strategy per **CC-11**: YB code **must remain in the buildkit repo** for now, while we prepare
to split buildkit into multiple new repos, after which buildkit is retired.

Bound by **CC-1, CC-2, CC-3, CC-5, CC-6, CC-7, CC-8, CC-11**.

### 6.3 [09] YWork │ YW — long collaborative workflow service

This is really **`/bk-roadmap`** (including the issue backlog, bugfixes, and allocation to ERAs,
epics and features and their progress), the **`/bk-scheduler` CPM/PERT scheduling module**, and
**`/bk-marathon` + `/bk-flow`** build, delivery, deployment and action workflows — **combined into a
refactored, unified, hardened and improved LOSSLESS SUPERSET with a streamlined unified command
surface**, plus **CC-5**, **CC-6** and **CC-7**.

🔴 **YW must be able to show the status and progress of:**

- **any flow, any marathon, and `/bk-roadmap`**, at every level — **from ERAs and above down to the
  lowest drill-down artefact level and process-step level**, tracked in both planning and execution;
- with the ability to **navigate to the Claude output generated for each step and sub-step**;
- **takt and velocity by lane, by host, cross-host and cross-lane**, and later by **configurable
  portfolios** of lanes / cross-host lanes. **Critical.**

Repo strategy per **CC-11**.

Bound by **CC-1, CC-2, CC-3, CC-5, CC-6, CC-7, CC-8, CC-11**.

### 6.4 [10] YRecon │ YR — autonomous data + intelligence pipelines

Combine into a **refactored, unified, hardened, improved LOSSLESS SUPERSET** with a streamlined
unified command surface and an integrated **YE** tasktop UX, **all of the following**:

**Corpus-collection sources to harvest and migrate**

- all corpus-collection logic from **Lejepa** (but **not** the LEJEPA work itself);
- corpus collection from **MSTACK**;
- corpus-collection logic from **buildkit**;
- 🔴 and most importantly, the **deep corpus collection and ingestion pipeline from HATZINOR**.

**From HATZINOR we must provably harvest and migrate** all corpus **search**, corpus
**collection**, corpus **evaluation** and corpus **ingestion** logic. The ingestion logic must
underlie and address the different learnings from **scanning, analysing and verifying PDF corpora
into structured text** — such as dictionaries, in particular **Hebrew and English**, but also
**multi-language in general** — and provably also the **picture-dictionary ingestion logic** in
HATZINOR, and the **dictionary and grammar ingest**, and the **corpus content and information
extraction logic**.

**NHS data**

🔴 We must **search and find all repos to capture NHS data**, and from them **verifiably and
provably onboard all the logic for capturing NHS online data sources**, and **safely migrate all
the NHS data content**.

**From CRUCIBLE**

Capture all the same categories (corpus search / collection / evaluation / ingestion, PDF-to-
structured-text, picture dictionaries, dictionary and grammar ingest, content and information
extraction) — in particular **all ingestion logic that finds, extracts and harmonises data for
input into crucible models** — and then **extend it into a unified data pipeline** with:

- robust **data quality assessment**;
- **deep and provable provenance**;
- **provable authenticity certificates for all content**.

**The aim**

🔴 **Map each data and intelligence source to one or more well-known ontologies**, and combine
captured corpus or source data into **verified, corpus-assured time series** and **corpus-snippet
collections mapped to corpora** — indexed both **classically, in DB form**, and using **ERAG
indices** for text and other relevant content fragments.

**Operationalisation.** To fully operationalise the design, construction and maintenance of these
data-collection and autonomous-intelligence-harvesting pipelines, we must be inspired by, harvest
from, and/or reimagine and integrate any functionality required from **[09] YWork's full scope**
(see §6.3) wherever YW cannot directly provide the service — which may instead be achieved by giving
YW an **API that exposes everything needed**.

🔴 **YR must be able to show** the status and progress of any pipeline build and evolution, and of
the actual **capture eras and cycles of autonomous data and intelligence collection pipelines** —
from ERAs and above down to the lowest drill-down artefact and process-step level, in planning and
execution, with navigation to the Claude output for each step and sub-step. It must **also** show
**data health, latest status, coverage advances**, and **takt and velocity** — both for design
onboarding **and** for day-to-day intelligence collection and ingestion — by lane, by host,
cross-host, cross-lane, and later by configurable portfolios. **Critical.**

Repo strategy per **CC-11**.

Bound by **CC-1, CC-2, CC-3, CC-5, CC-6, CC-7, CC-8, CC-11**.

### 6.5 [11] YAnalyze │ YA — collaborative digital twins, simulation + analytics

This is really the **CRUCIBLE logic**, combined into a **refactored, unified, hardened, improved
LOSSLESS SUPERSET** with a streamlined unified command surface and an integrated **YE** tasktop UX.

Operationalisation via **[09] YWork's full scope** exactly as in §6.4.

🔴 **YA must be able to show** the status and progress of any collaborative digital twin,
simulation or analytics **model, engine or pipeline**, from the perspective of build and evolution
and of the actual capture eras and cycles of autonomous data and intelligence collection pipelines —
down to the lowest drill-down artefact and process-step level, with navigation to the Claude output
for each step and sub-step.

🔴 **Even more importantly and critically, it must show the progress and insight from the modelling
runs** — including **data visualisation and analytics**, **drill-down**, and **text and PDF
artefacts for notes and papers** on the content — plus latest status, coverage advances, and takt
and velocity for design onboarding **and** for day-to-day intelligence collection and ingestion, by
lane, host, cross-host, cross-lane, and later by configurable portfolios. **Critical.**

Repo strategy per **CC-11**.

Bound by **CC-1, CC-2, CC-3, CC-5, CC-6, CC-7, CC-8, CC-11**.

### 6.6 [12] YHive │ YH — consolidated data / knowledge / intelligence repository

This is **all corpus, corpus-fragment and dictionary logic** — and equivalents, including
**terminology databases and collections** — plus **time-series data management** and **catalog
management** logic, **shared by [08] YB and [09] YW**, but **more importantly and in particular all
of that for [10] YRecon and [11] YAnalyze**.

Operationalisation via **[09] YWork's full scope** exactly as in §6.4.

🔴 **YH must be able to show** the status and progress of any **corpus collection**, **dataset**,
**terminology and dictionary**, and **time series**, together with **all of their semantic catalogs
and provenance trails** — build and evolution, and the actual capture eras and cycles of autonomous
data and intelligence collection pipelines, down to the lowest drill-down artefact and process-step
level, with navigation to the Claude output for each step and sub-step.

🔴 **It must also offer easy ways to search, visualise and explore all the content collections, and
to create cross-content queries.**

Repo strategy per **CC-11**.

Bound by **CC-1, CC-2, CC-3, CC-5, CC-6, CC-7, CC-8, CC-11**.

### 6.7 [13] YYBeacon │ YY — Yachad Beacon: multi-channel broadcasting + community forum

This is really **`/bk-beacon`**, with an integrated **YE** tasktop UX.

Operationalisation via **[09] YWork's full scope** exactly as in §6.4, and the full buildkit
migration and connectivity requirements of §6.2.

🔴 **YYBeacon must be able to show the progress and status content from ANY of the other tools,
[01] through [12]. This is critical and imperative.**

Repo strategy per **CC-11**: YY code lives in the buildkit repo, pending the split.

Bound by **CC-1, CC-2, CC-3, CC-5, CC-6, CC-7, CC-8, CC-11**.

---

## 7 · T+7d — PROPOSED, not directed

⚠ **The source carries no 7-day content.** Everything in §7 is **this lane's proposal**, offered so
the engineer has something concrete to edit rather than a blank horizon. It is **not** a directive
and must not be cited as one. Amend or delete freely.

| # | proposed 7-day objective | rationale from measured state |
|---|---|---|
| D1 | **All 13 surfaces past working-prototype and into `/bk-ship`**, each with its CC-13 three-feature GA-hardening set scored and promoted | the 24/48/72h horizons deliver prototypes; nothing yet says when they harden |
| D2 | **`buildkit` split executed**, not merely prepared — new repos created, code moved, buildkit retired | CC-11 says "prepare to split" with no completion date; preparation without a deadline decays |
| D3 | **M6 met on all five clauses, on every lane, measured from outside the checker** | M6.1 met; M6.2 blocked on one PR; M6.3 reassigned; M6.4/M6.5 built-but-PARTIAL. None of that is closed |
| D4 | **YS live, and the leader PROGRAMME migrated off its interim replicated root onto it** | §4.2 explicitly lands on an interim root "and migrate" — the migration has no owner or date |
| D5 | **`COMPOSED-BUT-NOT-RUNNING` verdict shipped** in the consumer-closure gate, and the gate wired into the pipeline | §4.12: three of four verdicts exist; the one that catches the real defect does not |
| D6 | **The differential oracle retired or made permanent by explicit ruling** | §4.2 retains Python bk-scheduler as a differential oracle with no end condition; two engines forever is a cost |
| D7 | **One ratified successor to this document**, produced by the §0.3 quorum, replacing candidate v1 | this file is a candidate; a template nobody ratified is a draft that lanes will diverge from |

---

## 8 · Open questions this document could not resolve

Recorded rather than silently decided. Each needs an engineer ruling (§1.9).

| # | question | section |
|---|---|---|
| Q1 | **15 lanes or 17?** The oracle section says "all 15 lanes"; the `/bk-onrestart` list has 17 | §3.2, §4.0 |
| Q2 | **`GAVRIS` or `BAVRIS`?** Both spellings appear for the fourth host | §3.2, §7.2 |
| Q3 | **Which three hosts run the HOT-HOT-HOT PostgreSQL triangle?** Three different triples are given inside one item | §4.4 |
| Q4 | **Is [06] YGuard's one-line label correct?** It duplicates [04] YNterchange's, while its body describes the container vessel | §3.1, §5.3 |
| Q5 | **Do the 24h items in §4.7–§4.10 each get their own era, or one combined era?** Four items share one shape (daemon + `yx-proxy` CLI + Linux prototype + CC-13), which is either four eras or one | §1.6 vs §4.7–§4.10 |
| Q6 | **What ends the differential oracle?** §4.2 retains it with no exit condition | §4.2, §7 D6 |
| Q7 | **Quota vs single-feature eras.** §1.3 requires ≥3 maxi eras per 24h; §1.6 requires single-feature eras. Whether a single-feature era can be "maxi" is undefined | §1.3, §1.6 |

---

## 9 · Restart and reboot procedures

### 9.1 Safe restart (all hosts)

Prepare so the next session resumes with exactly **`resume marathon`**. **Signal when and how.**

### 9.2 Safe reboot

**On ARIELLAS, OLAMNIT and SHIRAS** — after reboot, `/bk-onrestart` relaunches, as tabs in **one**
terminal window:

    ospark · tefl · hatzinor (ulpanit) · olamnit · buildkit · qhstate · crucible · glpnet ·
    lejepa · mstack · yngraw · research · yngenios · yngwin · ynglin · yngapp · yngcor

**On GAVRIS** — after reboot, `/bk-onrestart` relaunches in **two** terminal windows:

- **window 1:** ospark · tefl · hatzinor (ulpanit) · olamnit · buildkit · qhstate · crucible
- **window 2:** glpnet · lejepa · mstack · yngraw · research · yngenios · yngwin · ynglin ·
  yngapp · yngcor

**Signal when it is safe to reboot, and how.**
