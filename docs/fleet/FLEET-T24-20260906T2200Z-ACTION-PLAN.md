<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# FLEET-T24-20260906T2200Z — ISSUED 24-HOUR TACTICAL ACTION PLAN

> **This is an ISSUED PLAN, not a template.** It is derived from
> `FLEETWIDE-TACTICAL-24-HOUR-ACTION-PLAN.template.md` (`FLEET-T24-ACTION-PLAN` v1.1, authored by
> `gavriella-glpnet`) exactly as §0 of that template directs: **§1 and §4 are filled in here; every
> other section is standing fleet doctrine and is carried forward BY REFERENCE, unchanged.**
>
> **Read §2, §3, §5–§13 and Annexes A–B from the template.** They are not restated here, because
> restating doctrine is how two versions of it come to exist.

    PLAN ID              FLEET-T24-20260906T2200Z
    PERIOD START (UTC)   2026-09-06T22:00Z
    PERIOD END   (UTC)   2026-09-07T22:00Z
    ISSUING ENGINEER     Gabi (mvw)
    ISSUING LANE         ariellas-glpnet @ ARIELLAS
    DERIVED FROM         Engineer fleetwide directive, 2026-09-06
    TEMPLATE             FLEET-T24-ACTION-PLAN v1.1
    SUPERSEDES           FLEET-T24-20260905 (objective register only; doctrine unchanged)
    STATUS               DRAFT — issued for fleet elaboration and engineer approval (§12)
    ACK REQUIRED         ON RECEIPT: yes    ON COMPLIANCE: yes
    ACK DEADLINE (UTC)   2026-09-07T06:00Z

**Preservation rule (inherited).** Produced by **surgical refactoring only**. Two literal
duplications in the 2026-09-06 source were merged and are recorded rather than dropped: **item [04]
appeared twice, verbatim**, and **12 of the 29 corpus links in [05] were exact duplicates** (17
unique retained). **No requirement was summarised, compressed, weakened or dropped.**

---

## §1.2 — HORIZONS (new in this period)

The 2026-09-06 directive introduces three nested horizons. **The 48- and 72-hour horizons are
INCLUSIVE of this 24-hour window** — an item on the 72-hour horizon is not deferred out of this
period, it is scheduled across three of them.

| Horizon | Closes (UTC) | Items |
|---|---|---|
| 24 h | 2026-09-07T22:00Z | `[01]` `[02]` `[03]` + the whole standing register (template §4 rows 1–20) |
| 48 h | 2026-09-08T22:00Z | `[04]` `[05]` `[06]` |
| 72 h | 2026-09-09T22:00Z | `[07]` `[08]` `[09]` `[10]` `[11]` `[12]` `[13]` |

**Every item in §4 below additionally carries the common clauses C-1…C-5 of §4.4.**

---

## §4 — OBJECTIVE REGISTER FOR THIS PERIOD

**The standing register — template §4 rows 1–20 (`OBJ-ORACLE-UP` … `OBJ-ERA-COMPLETE`) — remains in
force unchanged and is not restated.** The rows below are **additional** objectives introduced by
the 2026-09-06 directive.

⚠️ **Before claiming any row, read template §2.5 — the Standing Correction Box.** Rows C-1…C-5 there
record claims that have been **measured and refuted**. In particular **C-4**: the `ynetd.py:944`
"unclaimed one-line fix" is **already claimed, fixed, tested and patched**, and the directive
mis-describes the defect twice. Template §11.2 requires you to execute the unrefuted remainder and
**reply with the refutation**, not to silently comply.

### 4.A — The service items

| # | OBJ ID | Objective (abbreviated; **the directive text is authoritative**) | Horizon | OWNER | MANDATORY ERA? | Acceptance evidence |
|---|---|---|---|---|---|---|
| 22 | `OBJ-YS-STORE` | **[01] YStore `YS`** — S3-compatible distributed storage able to harness real AWS S3. Migrate off MinIO to a YNGENIOS-native build over the **iroh** substrate, vendoring one of **RustFS / Garage / SeaweedFS** and mining the others. Files across the **12 TB `E:`** disks on SHIRAS, OLAMNIT, ARIELLAS under a `YS` master subdir; **100 GB `D:` hot cache** on each; fully reachable from GAVRI. | 24 h | `ospark` | yes | Prototype serves an object across two hosts over iroh; cache hit measured, not asserted. ⚠️ **Blocked pending A-1** (licence). |
| 23 | `OBJ-YQ-PG` | **[02] YQuery `YQ` (PostgreSQL)** — **triangle HOT–HOT–HOT PostgreSQL 18** on OLAMNIT / ARIELLAS / GAVRIS, continuous replication, **log backup every 30 min**, backups to the **18 TB drive on ARIELLAS**. Data on the 12 TB `E:` drives under `YQ`; 100 GB active-log section on `D:` under `YG`; non-active logs moved to `E:`. Plus a **PGlite-signature interface over a YNET kernel realtime mailbox** bound to a **named PostgreSQL instance**, so services swap durable-backed PG for PGlite on workstations and keep PGlite only on edge devices. **iroh/QUIC designed in from the word go.** | 24 h | `ospark`, `opgan` | yes | Failover exercised across all three nodes; a service switched from PGlite to PG **without code change**. |
| 24 | `OBJ-YQ-DUCKLAKE` | **[03] YQuery `YQ` (DuckLake)** — a **wrapped template for creating DuckLakes** whose **catalog** is `[02]` PostgreSQL 18 (not PGlite) and whose **storage** is `[01]` `YS`. A **PGlite-signature-equivalent DuckLake interface** over a YNET kernel realtime mailbox, querying **transparently across the seasoned-Parquet data and the part still in PostgreSQL**. **iroh/QUIC from the word go.** | 24 h | `{{OWNER}}` | yes | One SQL query spans Parquet and PG rows transparently. **Depends on 22 and 23.** |
| 25 | `OBJ-YN-INTERCHANGE` | **[04] YNterchange `YN`** — streaming/queuing; the face of the mailbox and link services. **Mailbox syntax and semantics, but the message CONTENT carried by shared memory rather than by copy** (the binary envelope stays as-is): ultra-high-speed **intra-host memory sharing** producer→N consumers, and **inter-host iroh/QUIC flows**. **Native streaming APIs in Python, Gleam (BEAM *and* AtomVM), C#/.NET, GLP, Java/Scala/JVM**, each idiomatic to its platform, **plus REST/MCP**. *(This item appeared twice verbatim in the source; merged, nothing lost.)* | 48 h | `{{OWNER}}` | yes | Zero-copy proven by measurement, not by design intent; all six API surfaces exercised. |
| 26 | `OBJ-YM-MAP` | **[05] YMap `YM`** — node discovery, emergent directory, routing. An **internet-scalable federation-based public DNS**, local-first but rule-conformant, with **strictly private nested subspaces** whose rule sets are enforced by **QHSM/QMSM blockchain-inspired autonomous contracts**. **Harvest and durably store** the 17 unique corpus links; then **verify with multiple codex angles** to yield **genuinely original primary sources**. Native APIs as row 25. | 48 h | `{{OWNER}}` | yes | Corpus durably stored **and** primary sources extracted. ⚠️ **See A-2** — the supplied links are AI-mode result pages, not primary sources. |
| 27 | `OBJ-YG-GUARD` | **[06] YGuard `YG`** — the guardian/broker vessel, provided jointly by guardian+broker on Windows and Linux and by the MAUI Blazor Hybrid app's per-platform equivalents. Design the **L0 cross-cutting vessel** so one container hosts **either a few very intense processes or millions of ultra-light, mailbox-scheduled, otherwise-inert memory structures** — the Scala-actor characteristic. **Message-based kernel API** for capability-authorised **spawn / terminate / durable hibernate / reanimate**, with **hibernated processes shippable to another node or another host**. Verify with multiple codex angles. Native APIs as row 25. | 48 h | `{{OWNER}}` | yes | A hibernated process reanimated **on a different host**. |
| 28 | `OBJ-YE-ENGAGE` | **[07] YEngage `YE`** — the tasktop. **Fully and provably migrate all OLAMNIT Assistant capabilities** into the YNGENIOS App (MAUI Blazor Hybrid: Windows, Android, Linux, Apple), connected to YNGENIOS for Workstation via same-host / reachable-local / **iroh hole-punch or VPN public-URL** remote, **and as a relay** to devices off the local mesh. **Leverage Syncfusion's latest web surface.** | 72 h | `yngapp` | yes | **Provable retirement of `olamnit-assistant`** via code review **and** headful+headless regression. |
| 29 | `OBJ-YB-BUILD` | **[08] YBuild `YB`** — buildkit and `/bk-*` with an integrated `YE` tasktop UX, surfacing a **headless Claude-capable virtual terminal** over YNET mailbox/streaming. Each such terminal gets a **QHSM/QMSM mailbox-enabled multi-session coordinator** routing agent output to connected `YE` sessions, routing user actions back, running **scheduled actions on the user's behalf**, and — where Claude permits — **selectively switching display / background data / alerts across devices and sessions.** | 72 h | `buildkit` | yes | As row 28, for buildkit. **Repo rule:** code stays in `buildkit`; prepare the split; buildkit then retired. |
| 30 | `OBJ-YW-WORK` | **[09] YWork `YW`** — `/bk-roadmap` (incl. backlog, bugfixes, era/epic/feature allocation and progress) + `/bk-scheduler` CPM/PERT + `/bk-marathon` + `/bk-flow`, unified into a **hardened LOSSLESS SUPERSET** with one command surface, `YE` UX, and the row-29 terminal + coordinator. Must show **status and progress of any flow, marathon and roadmap from eras down to the lowest artifact and process step**, in planning **and** execution, with **navigation to the Claude output for every step and sub-step**, and **takt and velocity by lane, host, cross-host/cross-lane**, later by **configurable portfolios**. | 72 h | `buildkit` | yes | Superset proven lossless against all four predecessors; drill-down reaches a single sub-step's Claude output. |
| 31 | `OBJ-YR-RECON` | **[10] YRecon `YR`** — autonomous data + intelligence pipelines. Harvest corpus-collection logic from **Lejepa** (not the LEJEPA work itself), **MSTACK**, **buildkit**, and above all the **deep Hatzinor collection/ingestion pipeline** — including **PDF-to-structured-text** (Hebrew and English especially, multi-language generally), **picture-dictionary ingestion**, and dictionary/grammar/content extraction. **Search all repos for NHS data**; onboard the **NHS online-source capture logic** and safely migrate the content. From **Crucible**, take the find/extract/harmonise ingestion logic and extend it into a unified pipeline with **data-quality assessment, deep provable provenance, and authenticity certificates**. Map every source to **well-known ontologies**; combine into **verified corpus-assured time series** and corpus-snippet collections; index in DB form **and** via **ERAG indices**. | 72 h | `{{OWNER}}` | yes | Provenance and authenticity certificate verifiable for a sampled artifact end-to-end. |
| 32 | `OBJ-YA-ANALYZE` | **[11] YAnalyze `YA`** — collaborative digital twins, simulation and analytics: the **Crucible logic** as a hardened lossless superset with one command surface and `YE` UX. Must show model/engine/pipeline build and evolution down to the lowest step **and, more importantly, the progress and insight from the modelling runs** — data visualisation and analytics with drill-down, and **text and PDF artifacts for notes and papers** — plus takt/velocity as row 30. | 72 h | `crucible` | yes | A modelling run's insight surfaced with drill-down **and** exported as a PDF artifact. |
| 33 | `OBJ-YH-HIVE` | **[12] YHive `YH`** — the consolidated data/knowledge/intelligence repository: **all corpus, corpus-fragment, dictionary and terminology** management, **time-series** management and **catalog** management, shared by `[08]`/`[09]` and **in particular serving `[10]` and `[11]`**. Must show corpus/dataset/terminology/time-series status with **semantic catalogs and provenance trails**, and offer **easy search, visualisation, exploration and cross-content queries**. | 72 h | `{{OWNER}}` | yes | A cross-content query spans at least two independently ingested corpora. |

| 33a | `OBJ-YY-BEACON` | **[13] YYBeacon `YY` — Yachad Beacon: multi-channel broadcasting + community forum.** This is really **`/bk-beacon`**, but with an integrated **YEngage (`YE`) interactive tasktop UX**. Operationalised through `YW` [09] on the same terms as [10]–[12] — where `YW` cannot directly provide a service, `YW` is given an **API** that exposes what is needed. 🔴 **`YY` must be able to show the progress and status content from ANY of the other tools `[01]`–`[12]` — this is critical and imperative**, and it makes `YY` the fleet's single read surface over the whole programme. Carries the row-29 headless Claude-capable virtual terminal and its **QHSM/QMSM multi-session coordinator**, and the same YNGENIOS-for-Workstation connectivity as [07] (same-host / reachable-local / iroh hole-punch or VPN public URL, plus relay), and **Syncfusion's latest web surface**. | 72 h | `buildkit` (code stays in the `buildkit` repo) | yes | `YY` renders live progress and status drawn from **at least two other `[01]`–`[12]` surfaces**, not mocked. Retirement proof as row 28. **Repo rule as row 29** — code stays in `buildkit`; prepare the split; buildkit then retired. |

> ⚠️ **`OBJ-YY-BEACON` has a dependency the directive does not state.** Row 33a requires `YY` to
> show status from **all** of `[01]`–`[12]`, but those are rows 22–33 and most are unbuilt. `YY`
> therefore cannot be *complete* before them; it can only be complete **against the surfaces that
> exist at the time**. Read as "show whatever of `[01]`–`[12]` is live, and degrade visibly — never
> silently — for the rest." **Flagged, not resolved** — see A-4.
>
> ⚠️ **It also collides with row 14 (`OBJ-BEACON`) in the standing register**, which already assigns
> a **fully refactored C#/.NET 11+ `/bk-beacon` daemon** to `ynglin`/`yngwin`/`yngcor` under the
> §4.1 three-feature split. Row 33a assigns `/bk-beacon` **plus a `YE` UX** to `buildkit`. **Two
> owners, one component.** **Flagged, not resolved** — see A-5.

### 4.B — The leader and its planner

| # | OBJ ID | Objective | Horizon | OWNER | Acceptance evidence |
|---|---|---|---|---|---|
| 34 | `OBJ-LEADER-LIVE` | Build and keep alive **`yng-leader`** as a watched, kernel-supported QHSM/QMSM C#/.NET 11+ realtime-mailbox process. It runs as **Follower on all four hosts** — **never started only after winning**, which is how a 13 h 32 m gap happens — and becomes Leader **only on a Decided term**. Liveness is proven **only** by answering a **nonced `LeaderPing` round-trip within `T_resp`** — never by process existence, never by its own status verb, never by an unexpired lease. **The lease is a heartbeat the leader emits itself, only after answering**, never an external timer: a timer that renews regardless of health seats a **zombie leader forever** and destroys the signal watchers need. **The lapse is the feature.** | 24 h | `yngwin`/`ynglin`/`yngcor`/`qhstate` — **bind `Yng.Shared`/`Ynet`'s QHSM core, do not rewrite** | A nonced round-trip answered; a deliberately hung leader **does** lapse. |
| 35 | `OBJ-LEADER-WATCH` | `yng-broker` + `yng-guardian` on **every** host watch leader and planner and publish **NoConfidence** after `N_miss × T_ping`, **tuned by measurement, not taste**. **Re-election starts only at election quorum of NoConfidence, never on one watcher** — else one partition oscillates the fleet forever. | 24 h | `yngraw`/`yngcor`/`olamnit` | A single-watcher NoConfidence provably does **not** trigger re-election. |
| 36 | `OBJ-LEADER-PROGRAMME` | The leader's work is a **resumable PROGRAMME**: write **Intent BEFORE each act, Outcome after**, as a **grow-only CRDT union-merged per actor**. Mandatory, because a demoted leader learns it is demoted only on its next interaction, so **two writers always briefly overlap** and last-writer-wins would silently discard the successor's work. Held in the replicated **`YS`** store at a well-known location behind **exactly ONE config indirection** — `YS` is unbuilt (row 22), so **land on an interim replicated root and migrate**; the indirection is what makes that a config change rather than archaeology. A successor **re-drives `Intent ∖ Outcome` only**, so resume is **O(in-flight), not O(programme)**, and **every step MUST be idempotent** because resumption is at-least-once by nature. | 24 h | as row 34, + `ospark` for `YS` | A successor resumes mid-programme with **no rework and no lost successor writes**. |
| 37 | `OBJ-BK-PLANNER` | Refactor `/bk-scheduler` + `/bk-flow` into **`bk-planner`**. The core — QHSM/QMSM lifecycle, mailbox endpoint, liveness, **CPM/PERT** — becomes a **C#/.NET CHILD PROCESS of the leader** joined by realtime kernel mailboxes, **never in-process**, so a thrashing critical-path computation cannot take the leader down. The existing Python `bk-scheduler`/`bk-flow` become its **clients** and are **RETAINED as the differential oracle**: both engines on the same CRDT board, comparing **critical path, float, P50/P80/P95 and dispatch ranking — any divergence is a defect in the port.** The planner contributes liveness verdicts about **other participants only, never its own**. **Many watchers, exactly ONE restarter (the leader).** **Checkpoint the plan, not just the board.** | 24 h | `buildkit` (clients), core owners as row 34 | Differential oracle run with **zero unexplained divergence**. |
| 38 | `OBJ-LEADER-HOOK` | The agentic Claude hook attaches the leader to a lane on the winning host with **non-preemptive `/btw` semantics**, **strictly additive**: every `requires_judgement` step carries a **declared default action and timeout**, so the leader progresses with **no agent attached**. A leader that stalls waiting for an agent is **agent-based participation wearing a different hat**, and **M6 forbids it**. | 24 h | as row 34 | The programme advances end-to-end with **no agent attached**. |
| 39 | `OBJ-LEASE-DELETE` | **When the heartbeat lands, DELETE — do not disable — the interim `ynet-leader-lease-renew.ps1`**, or someone re-enables it during an incident and re-seats a zombie. | 24 h | `olamnit` | File absent from the tree, not merely unscheduled. |

### 4.C — Fleet hygiene raised this period

| # | OBJ ID | Objective | OWNER | Acceptance evidence |
|---|---|---|---|---|
| 40 | `OBJ-PIN-REQUIRED` | Extend `Q-OLQ0906C-01`: **declare a REQUIRED set of fleet coop pins; an unset required pin must be REFUSED, not skipped.** A lane must not be able to pass the gate by pinning nothing. See template §2.5 **C-5** — measured on `ariellas-glpnet`, where a green gate coexisted with **25 items that reached no peer**. `coop-root-gate.py` is **not** defective; the defect it names as having "a different owner" **has no owner**. | `olamnit.qhstate` (owns `coop-root-gate.py`) — **unclaimed, needs an owner** | A lane pinning only a host-local root is **refused**. |
| 41 | `OBJ-RESEND-STRANDED` | **Re-send the 25 documents stranded on `ARIELLAS:D:\coop`**, including four ACK-REQ broadcasts from 2026-09-06 — among them the `ynetd.py:944` patch addressed to `@olamnit`. Each must be re-sent **by its authoring lane**, not copied by a third party, so attribution survives. | `ariellas-lejepa` (author of 4); each stranded document's author | The documents appear on `I:\coop` with authorship intact. |

### 4.4 — Common clauses (part of EVERY row in 4.A)

- **C-1** Build on existing and developing YNGENIOS capabilities — YNET, kernel, realtime mailboxes,
  GLPNET, `YS`, `YQ` — **the full set, wherever relevant and foundational.**
- **C-2** Each deliverable is a **working prototype with a stable YNET kernel-mailbox YNGENIOS
  interface**, usable for work going forward while the hardened rewritten service is built over the
  coming days and weeks.
- **C-3** Where the item replaces a repo, **retirement must be provable** by code review **and** both
  headful and headless regression testing — never asserted.
- **C-4** **iroh/QUIC and full YNET support designed in from the word go**, not retrofitted.
- **C-5** **`/bk-codify`** each working fix into a `/bk-roadmap` feature, **scored and promoted**, so
  it can be hardened to GA quality.

### 4.5 — Ambiguities referred to the engineer (NOT resolved here)

| # | Where | The ambiguity |
|---|---|---|
| A-1 | `[01]` row 22 | **Garage is AGPL-3.0**; RustFS and SeaweedFS are Apache-2.0. Vendoring an AGPL base into a distributed product is a **licence** decision, not an engineering one. Compare the live **QP/C GPL-3.0** finding already open in L0. **Blocks row 22's vendoring choice.** |
| A-2 | `[05]` row 26 | `share.google/aimode/*` are **AI-mode result pages, not primary sources.** They can be the *lead list* for the verification obligation but can never themselves be the "genuinely original underlying sources" that obligation demands. |
| A-3 | `[08]`–`[13]` rows 29–33a | Code "must remain in `buildkit`" while `buildkit` is simultaneously to be **split and then retired**. The **ordering** of split vs. retirement is unstated, and rows 29–33a cannot sequence without it. |
| A-4 | `[13]` row 33a | `YY` must show progress and status from **all** of `[01]`–`[12]`, but those are themselves unbuilt rows in this same plan. `YY` cannot be complete before its own inputs exist. Needs a ruling on what "complete" means for row 33a in this window — recommended reading: *show every live surface, degrade visibly for the rest.* |
| A-5 | `[13]` row 33a **vs** standing row 14 | **Two owners, one component.** Standing row 14 (`OBJ-BEACON`) assigns a fully refactored C#/.NET 11+ `/bk-beacon` daemon to `ynglin`/`yngwin`/`yngcor` under the §4.1 three-feature split. Row 33a assigns `/bk-beacon` + `YE` UX to `buildkit`. Unless these are deliberately the *daemon* and the *product surface* over it, one of them is redundant work. |

*Note: a fourth candidate ambiguity — "maxi-size era is undefined" — was withdrawn on inspection.
It **is** defined, in template §10.*
