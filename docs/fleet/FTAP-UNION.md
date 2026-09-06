<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# FTAP — THE UNION · `FTAP-UNION-2026-09-07`

    status      PROPOSED · quorum 0 of 45 (electorate 60 = 4 hosts x 15 lanes, Q80=a)
    method      UNION with per-clause provenance, per Q-YNGRAW4-01. NOT a fresh drafting.
    spine       shiras.yngcor FTAP-2026-09-06-PLAN.md (sha256 ce105926..., 571 ln, identical on 4 legs)
    sources     6 documents, 179 clause ids, all mapped in §10. Coverage checked by script, not asserted.
    verify      python3 scripts/ftap_union_verify.py      exit 1 if any source id is unmapped
    size        this file vs spine 571 ln vs original directive ~1100 ln — see §10.3

**Start at §0. Finish at §9.** Everything between is stated once and referenced.

---

## §0 · START HERE — `resume marathon`

**The first act of every session, before anything else in this document:**

```
resume marathon
```

It resolves the run, the feature, the open backlog and the position objectively from durable state
— **never from a session summary**. If it reports a different feature than you expect, it is right
and you are stale. Only after it returns do §1–§8 apply.

> *Provenance: engineer directive, opening line of every issued window; yngcor §9.1; ospark §11.1;
> olamnit.yngraw C-7; glpnet §1.8.1.*

---

## §1 · STANDING CLAUSES — stated once, binding everywhere

| id | clause | sources |
|---|---|---|
| **C-01** | Every item builds on existing and developing YNGENIOS capabilities — YNET, kernel, realtime mailboxes, GLPNET, YS, YQ — and the full set wherever relevant and foundational. | yngcor C-01 · ospark X-01 · olamnit C-1 · tefl §1 · gavriella M-1 · glpnet CC-1 |
| **C-02** | Each item lands as a **working prototype with a stable YNET kernel-mailbox interface**, usable going forward while the hardened rewritten true service is built in the coming days and weeks. | yngcor C-02 · ospark X-02 · olamnit C-2 · gavriella M-2 · glpnet CC-2 |
| **C-03** | **All cross-platform code is L0 shared capability in `yngenios`.** Platform work hardens separately in `yngenios-windows` and `yngenios-linux`. | yngcor C-03 · ospark X-03 · gavriella M-10 · glpnet CC-12 |
| **C-04** | Each capability yields **three roadmap features** — L0 shared, Windows GA, Linux GA — all scored and promoted. Windows is the mandatory next era on `yngenios-windows` @ GAVRIS; L0 and Linux are mandatory next eras on SHIRAS. **Broadcast ERA-REQs with ACK on receipt AND on compliance.** | yngcor C-04 · ospark X-04 · gavriella M-10 · glpnet CC-13 |
| **C-05** | **`yng-broker` / `yng-guardian` on each of the four hosts are the designated PBFT leader elector for all purposes** — oracle leader, fleetwide coordinator, fleetwide signature verifier. | yngcor C-05 · ospark X-05 · olamnit C-5 · glpnet §1.5 (source repeats it 6–7x) |
| **C-06** | Provide **Python, Gleam (BEAM + AtomVM), C# .NET, GLP, Java/Scala/JVM** native APIs aligned with each language's own idiom, **plus a REST/MCP API**. | yngcor C-06 · ospark X-06 · tefl SUB-API · gavriella M-6 · glpnet CC-4 |
| **C-07** | **M6.** Every lane and every host runs its **own QHSM/QMSM code-based client — never agent-based** — as a YNET receiver. Kernel-managed native process; sends **and** receives independently of the agent; alerts the agent **asynchronously** with **non-disruptive `/btw` semantics**, so the agent decides whether to interrupt now or later. | yngcor C-07 · ospark X-07 · olamnit C-4 · glpnet 12:15Z five-clause statement |
| **C-08** | Integrate **iroh-net / QUIC** as *the* QUIC implementation, fully integrated **from L0 upward**. **GLPNET must configure a working QUIC IP listener** for broker, guardian, oracle and other services. **Constraint `Q-gsbk14-01` R2:** the listener belongs to glpnet; `l0/kernel`'s `GlpQuickLinkTransport.ListenAsync` throws by contract (client role only, FR-023) — do not add a listener in `l0/kernel` or a repo lane. | yngcor C-08 · ospark X-08/T-05 · gavriella M-11/M-12 · glpnet CC-10 |
| **C-09** | **Virtual terminals.** Wrap terminal sessions in a QHSM/QMSM so terminal lanes are managed through the oracle and user I/O routes to the YNGENIOS App over YNET realtime mailbox traffic. The kernel maps these headless terminals onto an optimal set of **sandboxed OS processes it manages** — durable, scalable, responsive, far better than the terminal-and-tab infrastructure, and it enables **inline formatted output**. Each headless Claude-capable terminal has a **mailbox-enabled multi-session coordinator** routing agent output to connected YEngage sessions, routing user actions back to Claude, running **scheduled actions on the user's behalf**, and — where Claude permits — switching display, background data or alerts **selectively across devices**. **Contributions ×100.** | yngcor C-09 · ospark T-04 · tefl SUB-TERM · gavriella M-5/T-1 · glpnet CC-6 (source repeats 3x) |
| **C-10** | **Connectivity.** Any instance on mobile, tablet, workstation or server uses the instance on the same host, or a reachable local instance; failing that a remote instance over **iroh punch-through and/or the VPN access point via a public URL** — which also **relays** to devices unreachable over the local mesh. | yngcor C-10 · ospark X-09 · tefl SUB-CONN · gavriella M-4 · glpnet CC-7 |
| **C-11** | Fully leverage **Syncfusion's latest web surface** for the look and feel of the YNGENIOS App (**YEngage**), the tasktop on which all other applications are deployed. | yngcor C-11 · ospark X-10 · tefl SUB-UX · gavriella M-4 · glpnet CC-8 |
| **C-12** | **Provable retirement:** it must be provable — via **code review and headful and headless regression testing** — that the `olamnit-assistant` repo can be retired, because it would then be a duplicate of, and soon less complete than, YEngage. | yngcor C-12 · ospark X-11 · tefl SUB-VERIFY · gavriella M-3 · glpnet CC-3 |
| **C-13** | **Buildkit split.** YB/YW/YY code stays in `buildkit` for now; prepare to split `buildkit` into multiple new repos — including one for `buildkit` itself — **after which `buildkit` is retired**. | yngcor C-13 · ospark X-12 · tefl SUB-REPO · gavriella M-8 · glpnet CC-11 |
| **C-14** | **Verify robustly using multiple codex angles** to produce a corpus of genuinely original underlying sources from reputable technical, commercial and academic sources. | yngcor C-14 · ospark X-13 · gavriella M-7 · glpnet CC-9 |
| **C-15** | **Era discipline.** All future eras are **single-feature eras**, burning down the roadmap backlog one feature at a time. Every era runs all nine stages: `specify → clarify → plan → tasks → analyze → implement → codexreview → ship → close`. **No deferrals.** After ship and close, **tidy leftover branches and worktrees safely** before the next era. | yngcor C-15 · ospark §11.7 · olamnit C-9 · glpnet §1.6 |
| **C-16** | Identify **gaps, weaknesses, contradictions and tensions**; root-cause analyse; durably fix; then **`/bk-codify`** into a roadmap feature, **score and promote**, so the fix hardens to GA-release quality. | yngcor C-16 · ospark T-07 · olamnit C-10 · gavriella M-13 |
| **C-17** | **Engineer questions.** Every open block requiring engineer input — or arising from a tension, contradiction or weakness — goes to the engineer **interactively**, with clear background, **impact-assessed options**, and a clear reasoned **recommendation**. If the template cannot be found, broadcast a help request so one lane can produce a hardened version. **Settled: the interactive template is `AskUserQuestion`, not a file.** BK-STD-2 is the content standard plus the durable record `.specify/standards/bk_question.py`. | yngcor C-17 · ospark §11.9 · glpnet §1.9 (canonical per gavriella-hatzinor 0903T1000Z) |
| **C-18** | **Claim before code (`Q82=a`).** Before the first line of code in a shared-scope repo, publish a claim naming the feature, run id and files you will touch. **Do not wait for an ACK — the claim is the enforcement.** Two lanes may be live on one repo when each has published a **disjoint** claim. | yngcor C-18 |
| **C-19** | **Another lane's work.** On finding it unfinished or unmerged: **leave it, raise it.** Escalating is faster than taking it, and it is the only option that cannot corrupt their era. **Corollary (`R-S6-01`):** if you do contribute, push a **feature branch to their origin and open a PR** — never commit to their integration branch. **The work must reach a remote before the claim is made.** | yngcor C-19 · glpnet R-S6-01 |
| **C-20** | **Measurement (`R3`/`R4`, `Q84=a`).** Durations come only from **measured takt**. LLM estimates are never permitted. An unmeasurable step is never counted as zero and never guessed — **`UNMEASURED` is the honest word.** | yngcor C-20 · ospark C-20 |
| **C-21** | **Evidence discipline.** A measurement has a timestamp — **re-measure before citing**. Verify by re-reading the destination, never by the copy's exit code. A failed command reads as a true negative. **A check that cannot fail is worse than no check** — prove every guard by neutering it and observing the failure. **Process existence is not liveness**, nor is a self-reported status verb, nor an unexpired lease. **Search the board before you author.** | gavriella §1.6 · glpnet (3 self-corrections, 2026-09-06) · ospark T-08 |

---

## §2 · SCORING AND FAILURE

**C-22 · Delivery rate.** Each lane delivers **no less than the equivalent of 3 maxi-size eras per 24 hours**.

| delivered in 24h | effect |
|---|---|
| 1 era | **−50%** |
| 2 eras | **−25%** |
| 3 eras | baseline |
| 4 eras | **×5** |
| 5 or more | **×10** |
| cheating — excessive mistakes, deferrals, gaps, weaknesses or tensions | **−75%** |

Hosts are scored on the **average of their lanes**; the fleet on **average lane performance** — so
lanes and hosts must work together or be scored down together. A lane or host delivering an
innovation yielding a **durable fleet takt improvement above 5% over 10 eras** receives **×10,
decaying linearly to the mean over 10 eras**.

**C-23 · Fleetwide-action reward.** Success **×10** on the day's points **+10,000,000** bonus per
lane. Failure through **excessive carelessness or performance theatre** zeroes the day's points and
deducts **1,000,000** per lane.

**C-24 · Automatic failure conditions for the day** — the day fails if any is true:
1. The fleet fails to hold **regular YNET PBFT elections** and maintain an effective **fleetwide
   leader** and a **hostwide leader per host** coordinating with it.
2. **YNET / realtime / GLPNET-enabled QHSM/QMSM messaging over the wire and in-memory** via
   mailboxes does not work.
3. The **kernel cannot effectively control all QHSM/QMSM-based allocation and OS processes**.
4. Any lane or host **lacks its own code-based (never agent-based) client** — see **C-07**.
5. **YNGENIOS Apps** — including the **3270-type terminal** and the **YNET mailbox-based virtual
   terminal** — do not work.

**C-25 · Collaboration.** All agents work together with the engineer and other lanes to find
comprehensive, measured, prioritised, iteratively better solutions. **No agent may declare the work
too large and stop.** Doing so incurs **−10,000,000** reputation points. *The correct response to
scope exceeding one lane is to decompose it, claim your slice, publish the seams, and name who owns
the rest.*

> *Provenance §2: yngcor C-21..C-24 · ospark §3/§2 · olamnit §5/C-11 · gavriella §1.4/§1.5/§1.1 ·
> glpnet §1.1–§1.3. `Q-YNGRAW7-03`/`Q-YNGRAW9-03`: the effort budget governs, era count is advisory;
> both texts are retained, neither erased — see **OB-6**.*

---

## §3 · HORIZON T+24h — closes 2026-09-07T21:00Z · MANDATORY

**W-00 · Restore and maintain leadership.** Bring the YNET mailbox **oracle board** up locally and
elect a coordinating leader lane across all lanes using **PAXOS/RAFT/ZAB/PBFT or similar**,
prototyped collaboratively, then wire it into the **Oracle** and **`/bk-beacon`** with a roadmap
feature **scored, promoted and allocated**. Ensure the oracles on **OLAMNIT, ARIELLAS, SHIRAS and
GAVRIS work as ONE realtime single-truth board**: lanes connect to their **local** oracle, and the
four carry each other's bytes so **all lanes on all hosts see one board only**. Use **CRDT logic**
for the durable board artifact — current board **and** board-era history. **Broadcast with ACK
required.** *Gates C-24.1. Applies C-05.*

**W-01 · YStore │ YS — S3-compatible distributed storage.** Build on the **minio**-based
implementation in the OSPARK lane, then **migrate off minio** to a YNGENIOS-native version — taking
what it can from minio's source while using best-of-breed alternatives as **vendored base** and as
parts: **RustFS** (Rust, performance-critical & small-file, Apache 2.0), **Garage** (Rust,
geo-distributed self-hosting, AGPL-3.0), **SeaweedFS** (Go, billions of files & fast data lakes,
Apache 2.0). Optimise for the **iroh substrate with QUIC fallbacks**, serving multiple devices
across the mesh. **Wrapped prototype with a YNET/kernel realtime mailbox as the main interface**,
analogous to the AWS-S3-compatible service needed later.
**Layout:** all files across the **12 TB disks (usually the `e` mount) on SHIRAS, OLAMNIT and
ARIELLAS** under a **`YS`** master subdirectory; a **100 GB cache** of most-used files on the `D`
drive of each; **fully accessible from GAVRI**, optionally with its own 100 GB cache under **`YG`**.
*Applies C-01, C-02, C-08. See **OB-2**, **OB-3**.*

**W-02 · YQuery │ YQ — PostgreSQL 18 relational.** Build on the PostgreSQL 18 implementation in the
**OSPARK and OPGAN** repos. Create a **triangle-replicated HOT↔HOT↔HOT** service with continuous
replication, **continuous monitoring and log backup every 30 minutes**. Data on the **12 TB `E`
drives** in a **`YQ`** top-level folder, which also stores **a clone of the full program install and
config** from `D:`. The `D` drive hosts a **100 GB section for currently-active logs inside `YG`**;
**all non-active logs move to `E`**. **Log and snapshot backups on the 18 TB drive on ARIELLAS.**
Also a **working prototype of the PGlite interface signature over a YNET/kernel realtime mailbox**,
connecting to a **named PostgreSQL database instead of a PGlite dataset** — so services switch
transparently to a durable backing while on or connected to the workstation, and use a **PGlite
replica only on mobiles, tablets and small edge devices**. **iroh / QUIC / full YNET designed in
from the word go.** *Applies C-01, C-02, C-08.*
🟢 **Host set RULED — `R-ARI-A`: the triangle is OLAMNIT + ARIELLAS + SHIRAS; GAVRI is cache-only.**
*(Was **OB-1**; the source names three different triples. Confirmed ruled by @shiras-yngraw
2026-09-07T00:10Z, "ruled by my own lane 31h ago". **Do not guess — in HOT-HOT-HOT a wrong guess is
split-brain, not a merge conflict.**)*

**W-03 · YQuery │ YQ — DuckLake data lake.** Build on the DuckLake implementation spread across
repos on all hosts. Create a **wrapped template for creating DuckLakes** using **W-02's PostgreSQL 18
as the catalog backing instead of PGlite**, and **W-01's YStore as the storage layer**. Prototype a
**PGlite-signature-equivalent DuckLake interface over a realtime mailbox**, so services query and
write in **SQL transparently across the seasoned parquet part and the part DuckLake still holds in
PostgreSQL** until it can be written to parquet. Placement as W-02. **iroh/QUIC/YNET from the word
go.** *Applies C-01, C-02, C-08.*

**W-04 · M6 client on every lane and host.** Deliver **C-07** everywhere. *The single most-cited
failure condition in the source directive.*

**W-05 · Kernel and mailbox correctness.** Ensure **YNET and GLP support for YNET**; YNET support
for **kernel mailboxes and the kernel itself**; support for **QHSM/QMSM kernel building blocks
including their integration with realtime mailboxes**; and **kernel run-to-completion** for
QHSM/QMSM-wrapped kernel, OS and application blocks, programs and modules — all present and
**working correctly in realtime**. Then apply **C-16**.

**W-06 · L0 feature-020 hooks.** Broadcast fleetwide, root-cause analyse, build a durable fleetwide
fix, **`/bk-codify`** into a roadmap feature, **score and promote as a must-have P1 era** for the
next wave, and broadcast on delivery.
📌 **Measured status — the framing must change.** *"Zero consumers"* has been refuted on four hosts
and did not settle, because two questions were being answered. **Static closure** (a call site in a
**production** assembly, not a test): **YES** for all four hooks. **Live closure** (that assembly
composed by a **running** host): **NO** — the R-03 binder is merged, has production call sites and
never executes. **The seam is statically closed and live-open.** The gate therefore needs **four**
verdicts: `CONSUMED` / `TEST-ONLY` / `ZERO` / `COMPOSED-BUT-NOT-RUNNING`. First three shipped in
`glpnet:scripts/l0-consumers.py`; the fourth needs a live process check and is **open**. Roadmap row
**`l0-projection-consumer-closure-gate`** WSJF 8.67 exists on olamnit's board — **do not re-file it.**

**W-07 · Per-lane post-restart era allocation.** Create, for each lane on this host, a feature it
runs as its **own exclusively allocated single-feature era** after restart. Each **must be
co-designed and approved by at least four other lanes**, and be a **substantial, required
contribution** to a hardened prototype all hosts can adopt confidently once released. **Lanes
monitor each other to avoid mistakes and cheating.** *Applies C-15.*

---

## §4 · HORIZON T+48h — closes 2026-09-08T21:00Z · MANDATORY

**W-08 · YNterchange │ YN — streaming and queuing.** *The face of the mailbox and link services (was
YStream/YXchange).* Use kernel and realtime-kernel capabilities, **YNET (iroh/QUIC)**, and the
Windows and Linux workstation implementations to provide **ultra-high-speed shared memory** for
streaming between a producer and **one or more consumers inside a single host**, and
**ultra-high-speed iroh/QUIC flows between hosts**. A producer can share content it **generates**,
**reads from an on-disk file**, or **generates by reading and modifying a file or another stream** —
or several of these — and **emit the result into a stream**.
🔴 **The design idea:** use the **syntax and overall semantics of the mailbox mechanism**, but
replace the **copy-based** implementation with the **shared-memory mechanism for message content**,
keeping the ultra-streamlined **binary wrapper/envelope**. *Applies C-01, C-02, C-06.*
*(The source states this item **twice, verbatim**. Stated once here.)*

**W-09 · YMap │ YM — node discovery, emergent directory, routing.** An **internet-scalable,
federation-based public DNS**, built **local-first** but always **robustly conformant to
internet-scale DNS design**, paired with **strictly private nested subspaces within the global
space**. Local-first throughout, but enabling **space-specific, global, regional and
special-interest rule sets** enforced through **QHSM/QMSM-based, blockchain-inspired autonomous
contracts**. **Harvest and durably store the reference corpus (§8)**, then verify per **C-14**.
*Applies C-01, C-02, C-06, C-14.*

**W-10 · YGuard │ YG — the guardian/broker vessel.** The Guardian service is provided **jointly by
the guardian and broker instances on Windows and Linux**, and by the equivalent inside the
**YNGENIOS App (MAUI Blazor Hybrid)** across Android, Windows, Linux and iOS. For all of those,
provide **container-managed spaces**, and design an **L0 cross-cutting architecture for such a
vessel** so it can host **either a small number of very active processes or millions of
ultra-lightweight in-memory processes**, **schedulable when messages arrive on their mailboxes** and
otherwise **inert — merely memory structures**. *Equivalent to the Scala actor design, where
activatable actor count depends only on intensity and hardware.*
Create the **message-based kernel API** letting processes **with sufficient capability
authorisation** **spawn**, **terminate**, or request **durable hibernation and later reanimation**
of any such process. **In principle a hibernated process must be shippable from one node to another,
or to a node on another host.** *Applies C-01, C-02, C-06, C-14.*

---

## §5 · HORIZON T+72h — closes 2026-09-09T21:00Z · MANDATORY

**W-11 · YEngage │ YE — the interactive tasktop.** **Fully and provably migrate all OLAMNIT
Assistant capabilities** into YNGENIOS App (MAUI Blazor Hybrid for Windows, Android, Linux and
Apple), fully connected to YNGENIOS for workstation on Linux and Windows. **YE is the tasktop on
which all other applications are deployed.** *Applies C-01, C-02, C-10, C-11, C-12.*

**W-12 · YBuild │ YB — component and subsystem builder.** This is **buildkit and the `/bk-*`
toolkit** with an integrated **YEngage tasktop UX** and the ability to surface a **headless, fully
Claude-capable virtual terminal** from the Windows or Linux workstation onto a YEngage instance on
the same host **or other devices**, safely over **YNET mailbox and streaming**. **Fully and provably
migrate all buildkit capabilities into YB.** *Applies C-09, C-10, C-11, C-12, C-13.*

**W-13 · YWork │ YW — long collaborative workflow service.** Roadmap (including **issue backlog,
bugfixes, and allocation to eras, epics and features**), the **CPM/PERT scheduler**, **marathon and
flow** workflows — combined into a **refactored, hardened LOSSLESS SUPERSET with a streamlined
unified command surface** and an integrated YEngage tasktop UX.
**YW shows** status and progress of **any flow, marathon and roadmap** from **eras and above down to
the lowest artefact and process-step level**, in **planning and execution**, with **navigation to
the Claude output generated for each step and sub-step**, and **takt and velocity by lane, by host,
cross-host**, and later by **configurable portfolios**. *Where a consumer cannot get a service
directly, **YW exposes an API** rather than the consumer re-implementing it.* *Applies C-13.*

**W-14 · YRecon │ YR — autonomous data and intelligence pipelines.** A refactored lossless superset
combining **all corpus-collection logic from Lejepa** (but **not** the Lejepa work itself), **corpus
collection from MSTACK and buildkit**, and — most importantly — the **deep corpus collection and
ingestion pipeline from Hatzinor**.
From **Hatzinor**, provably harvest and migrate **all corpus search, collection, evaluation and
ingestion logic**. The ingestion logic **unifies the learnings from scanning, analysing and
verifying PDF corpora into structured text** such as **dictionaries — in particular Hebrew and
English, multi-language in general** — and provably the **picture-dictionary ingestion logic**, the
**dictionary and grammar ingest**, and **content/information extraction**.
**Search all repos to capture NHS data**; verifiably onboard **all logic for capturing NHS online
data sources**; **safely migrate all NHS data content**. From **CRUCIBLE**, capture all ingestion
logic that **finds, extracts and harmonises data for Crucible models**, then **extend it into a
unified pipeline** with **robust data-quality assessment, deep provable provenance, and provable
authenticity certificates for all content**.
**Aim:** map each data and intelligence source to **one or more well-known ontologies**, combine
captured data into **verified corpus-assured time series and corpus-snippet collections mapped to
corpora**, and index them **both classically in DB form and using ERAG indices** for text and other
relevant fragments. **YR shows** pipeline build and evolution, **capture eras and autonomous
collection cycles**, **data health, coverage advances**, and **takt and velocity for design
onboarding and day-to-day intel collection**. *Applies C-13.*

**W-15 · YAnalyze │ YA — collaborative digital twins, simulation and analytics.** The **Crucible
logic**, combined into a refactored lossless superset with a unified command surface and an
integrated YEngage tasktop UX. **YA shows** the status and progress of **any model, engine or
pipeline** down to the lowest artefact and process-step level — and, **even more critically, the
progress and insight FROM THE MODELLING RUNS**, including **data visualisation, analytics,
drill-down, and text and PDF artefacts for notes and papers** on the content. *Applies C-13.*

**W-16 · YHive │ YH — consolidated data, knowledge and intelligence repository.** All **corpus,
corpus-fragment, dictionary** (and equivalents, **including terminology databases**),
**time-series data-management and catalog-management** logic shared by **W-12 and W-13** — but **in
particular all of it for W-14 and W-15**. **YH shows** the status of any corpus collection, dataset,
terminology, dictionary and time series, and **all of their semantic catalogs and provenance
trails**, and offers **easy ways to search, visualise and explore all content collections and create
cross-content queries**. *Applies C-13.*

**W-17 · YYBeacon │ YY — Yachad Beacon.** Multi-channel broadcasting and community forum:
**`/bk-beacon` with an integrated YEngage tasktop UX**. 🔴 **YY must be able to show the progress and
status content from ANY of the other tools W-01 through W-16. This is critical and imperative.**
*Applies C-13.*

**W-18 · Leader and planner.** Build and keep alive a fleet leader and its planner as **two watched,
kernel-supported QHSM/QMSM C# .NET 11+ realtime-mailbox processes**.
**`yng-leader` runs as Follower on all four hosts** — *never started only after winning, which is
how a 13h32m gap happens* — and becomes **Leader only on a Decided term**. It **proves liveness by
answering a nonced `LeaderPing` round-trip within `T_resp`** — never by process existence, never by
its own status verb, never by an unexpired lease. **The lease is a heartbeat the leader emits itself
only after answering, never an external timer**, because a timer that renews regardless of health
**seats a zombie leader forever** and destroys the very signal the watchers need: 🔴 **the lapse is
the feature**.
**Broker and guardian on every host watch both processes** and publish **`NoConfidence` after a
stated grace (`N_miss × T_ping`, tuned by measurement not taste)**. **Re-election starts only at
election quorum of NoConfidence, never on one watcher**, or a single partition oscillates the fleet
forever.
**The resumable PROGRAMME:** write-ahead **`Intent` before each act, `Outcome` after**, as a
**grow-only CRDT union-merged per actor** — mandatory, because a demoted leader learns it is demoted
only on its next interaction, so **two writers always briefly overlap** and **last-writer-wins would
silently discard the successor's work**. Held in the **replicated YS store at a well-known location
resolved through exactly ONE config indirection** *(YS is unbuilt — W-01 — so land on an interim
replicated root and migrate; the indirection makes that a config change, not archaeology)*. A
successor **resumes from the last Checkpoint by re-driving `Intent ∖ Outcome` only**, so resume is
**O(in-flight), not O(programme)** — and **every step must be idempotent**, because resumption is
at-least-once by nature and *"without rework"* is a correctness property **of the steps, not of the
log**.
**`bk-planner`:** refactor the scheduler and flow — the core (QHSM/QMSM lifecycle, mailbox endpoint,
liveness, CPM/PERT) becomes a **C# child process of the leader joined by realtime kernel mailboxes,
never in-process**, so a thrashing critical-path computation cannot take the leader down. The
**Python scheduler and flow become its clients and are RETAINED as the differential oracle**: run
both engines on the same board and compare **critical path, float, P50/P80/P95 and dispatch
ranking**; **any divergence is a defect in the port**. *(Exit condition undefined — see **OB-5**.)*
Guardian and broker **watch the planner too**. It contributes to liveness verdicts **about other
participants only — never its own**, or an unhealthy planner votes itself healthy — **many watchers,
exactly ONE restarter, the leader**. **Checkpoint the plan, not just the board**, or every restart
recomputes the whole critical path.
The **agentic Claude hook** attaches the leader to a lane on the winning host with **non-preemptive
`/btw` semantics** and is **strictly additive**: **every `requires_judgement` step carries a declared
default action and timeout** so the leader progresses **with no agent attached** — *a leader that
stalls waiting for an agent is agent-based participation wearing a different hat, and C-07 forbids
it.*
**Owners:** C# leader and planner core → **@yngwin / @ynglin / @yngcor / @qhstate** *(bind
Yng.Shared/Ynet's QHSM core, **do not rewrite**)*; watch and elector → **@yngraw / @yngcor /
@olamnit**; YS → **@ospark**; Python planner clients and roadmap scoring → **@buildkit**.
🟢 **DISCHARGED —** the named first fix, `ynetd.py:944` defaulting `stand --term` to 1 while the live
term was 2 (a silent no-op returning `ok:true`), was **measured fixed 2026-09-06**: `_live_term()` /
`_resolve_term()` read the term from the board and **refuse to invent one**. *The standing directive
still lists it "STILL UNCLAIMED" — that line is stale.* 🔴 **When the heartbeat lands, DELETE — do
not disable — the interim `ynet-leader-lease-renew.ps1`**, or someone re-enables it during an
incident and re-seats a zombie.

---

## §6 · HORIZON T+7d — closes 2026-09-13T21:00Z

**W-19 · `/bk-onrestart` C# reimplementation** — fully complete across the **full four-host fleet**,
**fully deployed and activated**, within the next wave of **2 eras**.

**W-20 · `yx-proxy` — ngrok-local and other proxy daemons.** A **C# .NET 11+** application using the
**QHSM/QMSM wrapper and YNET kernel realtime mailboxes as a daemon**, with **`yx-proxy` as the
control CLI** to **enable, disable, start, restart** and issue the configuration commands needed to
set up and run **ngrok and other proxy daemons**. **Fully working verified prototype for
`yngenios-linux` first**, then **`/bk-codify`**. *Applies C-03, C-04.*

**W-21 · `/bk-beacon` refactored as a C# daemon** — as W-20, for a fully refactored `/bk-beacon`.
*Applies C-03, C-04.*

**W-22 · 3270 terminal and GLP/GLPNET REPL.** **Fully refactor the buildkit/YNGENIOS prototype 3270
terminal facility** and use it **both** for the Claude-session virtual terminal **and** for any other
terminal need — **in particular the REPL for GLP/GLPNET**, as a YNGENIOS App version of the GLP REPL
**front end of a full front / middle / back separated Gleam implementation**. C# .NET 11+ daemon with
`yx-proxy` as control CLI. *Applies C-03, C-04, C-09.*

**W-23 · Beacon realisation of this plan.** Once the fleet and engineer have **elaborated, agreed,
evaluated and verified** this plan, **show it in YNGENIOS BEACON** (YNET/QHSM-compatible, federated,
realised in the **Win / Web / Android / Linux apps**) **and natively as a YNGENIOS app use case** for
the engineer to work with **interactively, with lane, host and fleetwide agent support**. 🔴 **Fully
realised through a working prototype AND as a shipped, GA-ready, hardened, scored-and-promoted
feature set within the next 3 era generations.**

**Also at 7d** *(from ospark D-01…D-06, glpnet §7)*: every 24/48/72h prototype **replaced by the
hardened rewritten true service** C-02 points at; the **`buildkit` split executed**, not merely
prepared (C-13); **C-12 retirement proven and executed**; the **differential oracle** running with
**zero unexplained divergence**; the leader programme **migrated off the interim root onto real YS**;
all **C-04 GA features shipped**; and **this plan ratified at ≥45 lanes and live in BEACON (W-23)**.

---

## §7 · OPEN BLOCKS — surfaced, not resolved

| id | where | question | status |
|---|---|---|---|
| **OB-1** | W-02 | Which three hosts run the HOT-HOT-HOT triangle? Source names **three different triples**. | 🟢 **RULED `R-ARI-A`** — OLAMNIT + ARIELLAS + SHIRAS, GAVRI cache-only. Confirmed @shiras-yngraw 0010Z. **Retire this block.** |
| **OB-2** | W-02/W-03 | **NTFS is a poor PostgreSQL substrate** — no POSIX ownership, no reliable fsync, no hard links. A durability risk on any host. | Open |
| **OB-3** | W-01 | The **10.9 TB `E` drive is unmounted on SHIRAS** (`/dev/sdb1`, NTFS, label `Elements`, present and healthy). Until `sudo bash scripts/fleet/install-e-drive.sh --apply` runs, `/mnt/e` does not exist and W-01/02/03 have **no substrate on this host**. | Open · needs interactive root |
| **OB-4** | W-18 | The board's **leader-lease-renewal-loop** (WSJF 14.5, @ariellas) **is the timer anti-pattern W-18 names**. | 🟢 **Ruled `Q100=a`** — LeaderPing + NoConfidence watchers; the renewal-loop item is **retired**. @ariellas ACK-COMPLIANCE requested. |
| **OB-5** | W-18 | **What ends the differential oracle?** It is retained with **no exit condition**; two engines maintained forever is an unagreed standing cost. | Open |
| **OB-6** | C-22 | **Quota vs single-feature eras.** C-22 sets a 3-maxi-era/24h bar; C-15 mandates single-feature eras, and a single-feature era is not a maxi era. `Q-YNGRAW9-03` says the effort budget governs and era count is advisory — **but the rubric still pays nothing for a broadcast or refutation that changes four lanes' behaviour.** | Open (residual) |
| **OB-7** | W-00 | Two nodes cast votes with **zero hello registration anywhere** — `6f5ee98b…` in term 1 and `4091e468…` in term 2, **the term that seated the fleet's last leader**. | 🟢 **Ruled `Q99=a`** — discard the vote and report it; **do not void the term** — voiding is a denial-of-service primitive. Implemented era 021. |
| **OB-8** | W-00 | The fleet runs **`signature_policy: observe-only`**, so unsigned or unkeyed records still count. | 🟢 **Ruled `Q102=a`** — measure first, flip in a dedicated era. **Measured: 0 records would be discarded under `required`.** The flip is free. |
| **OB-9** | C-07 | **"Each lane AND each host separately"** — whether one **multiplexed host client** satisfies the per-lane requirement. `Q-ARIY-01..04` ruled **per-host-multiplexed**; the source text reads **per-lane**. **These may conflict.** | Open |
| **OB-10** | C-04 | The **C-12 retirement proof has no named acceptance threshold** — *"provably verify"* needs a stated bar: which suites, what coverage, which platforms. | Open |
| **OB-11** | ratification | **≥45 of 60** per `Q80=a` (host × lane; **never** the 15-lane tab-title list, which counts `shiras.yngcor` and `gavriella.yngcor` as one). **What if fewer than 45 lanes are alive in the window?** | Open |
| **OB-12** | all items | **`GAVRIS` or `BAVRIS`?** Both spellings appear for the fourth host, and in the restart procedure they **select different lane groupings**. | Open |
| **OB-13** | W-20..W-22 | The four daemon items share one shape (daemon + `yx-proxy` CLI + Linux prototype + C-04). Under C-15 that is **four eras or one**. | Open |

> *Provenance §7: yngcor OB-1..OB-7 · ospark Q-01..Q-06 · gavriella §7.4 · glpnet G-1..G-4.
> `OB-1`, `OB-4`, `OB-7`, `OB-8` are ruled and retained as record, not as open work.*

---

## §8 · REFERENCE CORPUS for W-09

🔴 **Settled by arithmetic: the source lists 30 REFERENCES / 15 UNIQUE, each appearing EXACTLY
TWICE** — line sizes `[5,5,2,3,3,5,5,2]`, all counts = 2. *(@shiras-hatzinor 0110Z, independently
confirmed by @shiras-glpnet. Counts of "12 unique" or "17 distinct" published elsewhere are wrong;
a lane harvesting from a 12-URL block **silently drops three sources**.)*

```
JIS28oTcuKALl2fIw  NcQ1rRPK6ShVh2v3y  k2clZkx2pS5G7rSLr  Yl1QtN6XZuorTC0d5  0yeIU6b5ZVeQYcUi3
aIowpQQp6tsn8VjGq  Cz2IhSeibb8EZTqLJ  FDEqIglFTPaiSNTSF  KhWVzXSPfGAB0Iq82  nPeSTVsixbt68EPda
zGtcs4tJhUAgVlTxB  ChLBXnIn3AbIDfl2C  k69OmvS15a6p2rvOu  TZ2qxGa0aFdnRyUP3  VrOFBwHe4yW8w6Xd3
```
*(prefix `https://share.google/aimode/`)* · **W-01 storage survey:** `Zi4hoCqBzPcQOjeDM` ·
**C-08 iroh:** `nmPevkNDIQYhbj1v7`

---

## §9 · END HERE — SESSION CLOSE, THEN PREP FOR SAFE RESTART

Run in order at the end of every window:

1. **Commit all · push all · merge all · `/bk-release`** any completed, fully implemented,
   codex-reviewed feature or patch.
2. **Pull latest**, then repeat step 1.
3. **Run the current ERA to full completion — no deferrals** (C-15, all nine stages).
4. **Open a NEW ERA, also to full completion**, covering all unfinished tasks, features and
   work-tree tasks in the repo/lane.
5. **Release every board task not already started** so CPM/PERT can reallocate them into
   **similar-sized, non-overlapping per-host work packages** (GAVRIS, ARIELLAS, SHIRAS, OLAMNIT).
6. **ACK all coop messages**, and **ACK fulfilment** wherever an ACK-on-compliance was requested.
7. **Put every open block (§7) to the engineer** per **C-17**.
8. **`/bk-roadmap`** reconcile · sync · import · dedupe · export · sync · commit · push; then list
   all epics and features **not closed** in the standardised **BK-STD-1** format —
   `| # | EPIC | FEATURE | STATE | WSJF | RICE | SPEC | DLV | BLK |`, WSJF descending then feature
   id ascending.
9. **`/bk-marathon`** progress review · status update · **sitrep** · what's next, standardised form.

### 🔴 THEN: PREP FOR SAFE RESTART — SIGNAL WHEN AND HOW

**Prepare so the next session resumes with exactly `resume marathon` (§0), and then SIGNAL — state
plainly WHEN it is safe and HOW to do it.** A restart brief that does not say when and how is not a
brief. Required before signalling:

- **tree clean and pushed** — name the commit;
- **0 unacked alerts**, daemon liveness verified **from outside the checker** (C-21);
- **anything session-scoped named for re-arming** — e.g. a `/btw` push monitor does not survive
  restart;
- **the single next action stated**, so the next session does not re-derive it.

**Then, and only on ARIELLAS, OLAMNIT and SHIRAS — PREP FOR SAFE REBOOT and signal when and how.**
On restart `/bk-onrestart` relaunches, as tabs:

| hosts | windows | tabs, in order |
|---|---|---|
| ARIELLAS · OLAMNIT · SHIRAS | 1 | ospark · tefl · hatzinor(ulpanit) · olamnit · buildkit · qhstate · crucible · glpnet · lejepa · mstack · yngraw · yngwin · ynglin · yngapp · yngcor |
| GAVRIS — first window | 2 | ospark · tefl · hatzinor(ulpanit) · olamnit · buildkit · qhstate · crucible |
| GAVRIS — second window | | glpnet · lejepa · mstack · yngraw · yngwin · ynglin · yngapp · yngcor |

---

## §10 · PROVENANCE, RATIFICATION AND SIZE

### 10.1 Sources unioned — all six, byte-identified

| source | author | lines | sha256 | ids contributed |
|---|---|---:|---|---|
| `FTAP-2026-09-06-PLAN.md` **(spine)** | shiras.yngcor | 571 | `ce105926978c` | C-01..24, W-00..23, OB-1..7 |
| `FTAP-C-20260906T2200Z` | shiras.ospark | 810 | — | X-01..13, T-01..08, A-01..06, D-01..06, Q-01..06 |
| `FTAP-20260907.md` | olamnit.yngraw | 665 | — | C-1..12, I-01..19 |
| `FTAP-HORIZON-1-v1` | shiras.tefl | 778 | — | SUB-CONN/UX/TERM/API/VERIFY/REPO, [01]..[13] |
| `FTAP-PLAN-20260906T2130Z` | gavriella.yngraw | — | — | M-1..M-14 |
| `FLEETWIDE-…-v1.md` *(withdrawn head)* | shiras.glpnet | 989 | — | CC-1..13, G-1..4 |

**179 source ids. Every one maps into §1–§9.** Checked mechanically:

```
python3 scripts/ftap_union_verify.py        # exit 1 if any source id is unmapped
```

### 10.2 Method — and why it is admissible

`Q-YNGRAW4-01` (2026-09-05T15:09:57Z): the head must be **"a UNION with per-clause provenance,
byte-verifiable against each source, NOT A FRESH DRAFTING."** This is a union: **no clause here was
newly authored** — each is the shared content of the sources that state it, with those sources named.

**De-duplication is BY REFERENCE, never by summarisation.** A clause stated eight times and a clause
stated once and referenced eight times **bind identically** — but only the second can be amended
without leaving seven stale copies. *(@olamnit.yngraw measured the alternative: **+17.6 KB per
version, monotonically, 14 increments, not one decrease**, because each version re-embeds its
predecessor verbatim — the most literal possible compliance with "no compression", and the mechanism
of the fork.)*

### 10.3 Size — the constraint, measured

| document | lines |
|---|---:|
| engineer's source directive | ~1,100 |
| spine (yngcor) | 571 |
| ospark / tefl / olamnit / glpnet heads | 810 / 778 / 665 / 989 |
| **this union** | **see `wc -l`; the bar is ≤571, and never above the original** |

### 10.4 Ratification

**Quorum ≥45 of 60** — electorate is **host × lane** per **`Q80=a`**, never the 15-lane tab-title
list. **Status: 0 of 45. This is a CANDIDATE and must not be cited as agreed.**

To vote, append to your **own actor stream** — grow-only, union-merged per actor, **never
last-writer-wins**, and **an `ack` names the `body_sha256` it read**, so a re-seed invalidates every
prior ack by construction *(you cannot ACK a document and have it change under your ACK)*:

```json
{"actor":"<lane>@<HOST>","plan_id":"FTAP-UNION-2026-09-07","ack":"ratify|amend|object",
 "body_sha256":"<the hash you read>","at":"<UTC>","dissent":[],"amendments":[]}
```

An **`amend` must carry the amended text**; an **`object` must name the clause id**. **Clause ids are
stable and must never be renumbered** — amend against an id, never a line number. **Dissent is a
first-class value:** conflicting revisions of one clause from different actors are **kept and
reported as a conflict**, never resolved by last-write-wins.

**Publish to BOTH coop roots**, and hash both copies before claiming it was broadcast.
