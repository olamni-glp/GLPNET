<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 323983e4-62b0-450f-a7ab-47c12b64e609
-->

# FLEETWIDE TACTICAL 24-HOUR ACTION PLAN — TEMPLATE v1

**Status:** first working version. **Adopt, adapt, and re-issue for each 24-hour period.**
**Scope:** every host and every lane on every host.
**Register:** this is an *operational* document. It says what is to be done, by whom, by when,
and what is forbidden. It is not a design document and it never replaces a feature's
`spec.md` / `plan.md` / `tasks.md`.

> **Provenance.** v1 is a **surgical refactor** of the fleetwide action prompt issued
> `2026-09-05` on `gavriella.buildkit`. It fixes spelling and grammar and imposes structure.
> **Nothing in the source prompt was summarised, compressed, or dropped.** Every substantive
> requirement in that prompt appears below as a numbered work package with an owner and an
> acceptance test. Where a requirement collides with a standing engineer ruling, the collision
> is stated in §2 rather than silently resolved.

---

## §0 — How to use this template

1. **Copy** this file to `.specify/program/plans/FTAP-<YYYYMMDD>T<HHMM>Z.md`.
2. **Fill in §1** (the parameter block). Every `<…>` placeholder must be replaced or explicitly
   struck out. A placeholder left in a live plan is a defect, not a detail.
3. **Re-check §2.** The standing constraints are *not* boilerplate — they change between periods.
   A plan issued against a superseded ruling is void.
4. **Select** work packages from §4 into §3's era ladder. A 24-hour plan that adopts every work
   package in §4 is almost certainly dishonest; see §5.4.
5. **Broadcast** per §6, with ACK required.
6. **Close out** per §8.

**Adaptation is expected.** Any engineer may add, retire, or re-own a work package in §4, and may
change the scoring policy in §5, for a future period. Amendments are recorded in §10 — the
template is additive; a retired work package is struck through and kept, never deleted.

---

## §1 — Parameter block *(fill in per period)*

| Field | Value |
|---|---|
| Plan id | `FTAP-<YYYYMMDD>T<HHMM>Z` |
| Period opens | `<YYYY-MM-DDTHH:MMZ>` |
| Period closes | `<YYYY-MM-DDTHH:MMZ>` (opens + 24h) |
| Issuing lane | `<host>.<lane>` |
| Hosts in scope | `<Gavriella, Olamnit, Ariellas, Shiras>` |
| Lanes in scope | `<count>` — enumerate in §7 |
| Fleet board of record | `<coop-root>/ynet/oplog` |
| Designated PBFT elector | `yng-broker` / `yng-guardian`, one pair per host |
| Question standard | `tools/bkquestion/` (`BK-QSTD-1`) |
| Report standard | `scripts/bk-std1.py` (`R65`) |
| Decision ledger | `.specify/decisions/engineer-decisions.jsonl` (append-only, **UNION**, never last-writer-wins) |

---

## §2 — Standing constraints that OVERRIDE this plan

**Read this section before executing any work package.** A work package that requires an action
forbidden here is **blocked**, not "deprioritised". Blocked work packages are reported as blocked
in the close-out sitrep and raised to the engineer as a question under `BK-QSTD-1`.

### 2.1 — No election runs on the fleet board *(current as at 2026-09-05)*

> ⛔ **No election runs on `<coop-root>/ynet/oplog` until the per-record signature (`BC-5`) ships.**
> — `Q-shiras300-03`, recorded 2026-09-04T20:40Z, engineer-decided, ledger rows 227–230.

**Why.** A vote cannot presently be bound to its caster. The signature is constant per node and
world-readable on a shared root; the content id is unkeyed. A vote placed in another node's file,
carrying that node's key and signature verbatim with a correctly recomputed content id, is
**indistinguishable from a genuine one by every check the store supports today**.

**Consequence for any 24-hour plan.** A work package whose acceptance test is *"a leader is
elected"* **cannot pass**. Six elections have been declared on this fleet and every one has been
stood down. Rewrite the acceptance test to target the **blocker** (`BC-5`, and the roster) or mark
the package blocked.

### 2.2 — Broker and guardian govern; lanes do not campaign

`yng-broker` / `yng-guardian` are present on each of the four hosts and are the **designated PBFT
leader elector for all purposes** — electing the oracle leader, the fleetwide coordinator, and the
fleetwide signature verifier.

> **DO NOT CAMPAIGN.** — `RULING-20260905T0005Z`, `shiras.yngraw`.

**Measured state, `gavriella`, 2026-09-04T10:15Z**, by PID and by socket:

```
yng-broker.exe    PID  9296   RUNNING   no TCP listener, no UDP endpoint
yng-guardian.exe  PID 12512   RUNNING   no TCP listener, no UDP endpoint
```

**Presence is established; leadership is NOT queryable.** That is the honest state. It is a
**transport gap**, and it is the real content of WP-02 — not a licence to elect locally.

**Forbidden:** adding a fallback election to any probe, tool, or pipeline command. A tool that
quietly elects when the broker is unreachable produces a leader the fleet never agreed to.

### 2.3 — One board identity per host

A host has exactly **one** board identity; the roster names it. Nothing is deleted — a
non-surviving id remains in history as a **read-only writer that no longer votes**. Quorum counts
**hosts (n=4)**; **lanes are clients**. An elector counting *nodes* sees more voters than there
are hosts, which inflates quorum invisibly.

⚠ **Not yet enforceable:** `ynet-roster.json` is mirrored to no shared root, so no lane can verify
its own admission. Until it (or its hash) is mirrored to `_ynet-board/`, this is a rule that has
never run.

### 2.4 — Claims must be measured before they are broadcast

**A claim that three or more lanes have independently refuted MUST NOT be re-broadcast**, however
urgently it is worded. Re-broadcasting a refuted claim costs the fleet an era per lane that acts
on it.

**Live example, current as at 2026-09-05.** The claim *"L0 has purpose-built feature-020 hooks
(`OnStepDispatched`, `Unregister`, `StartOnDedicatedThread`, `Markers`) with zero consumers — the
host that was meant to use them was never written"* is **REFUTED**, independently, by **five**
lanes (`olamnit` 2026-09-04T16:00Z, `gavriella` 18:20Z, `shiras` 20:15Z, `ariellas.qhstate`
2026-09-05T03:05Z, `gavriella.tefl` 03:10Z). The measurement:

> The hooks are **not in L0** — they are in the **`olamnit` repo**,
> `Olamnit.Kernel/Scheduling/DurableQF.cs` — and they have **six consumer call sites** in
> `Olamnit.Yngenios.Host/KernelHost.cs`. **The host WAS written.**

**This claim is closed. It is recorded here so that a sixth lane does not spend an era on it.**

### 2.5 — Disclosed gaps are not cheating

Per `RULINGS-20260904T2355Z` (`shiras.tefl`): a gap, weakness, or deferral that a lane **discloses**
is honest reporting and **must not** be scored as cheating under §5. What §5 penalises is
*undisclosed* defect volume. **A lane that reports "blocked, and here is why" outranks a lane that
reports a green it cannot evidence.**

---

## §3 — The 24-hour clock

The period is divided into **three era generations**. An **era** is defined normatively in
[`ERA-DEFINITION.md`](ERA-DEFINITION.md): **one era = one feature = its complete pipeline**, opened
by `/bk-specify` and closed by `/bk-close`. **A phase is never an era.**

| Generation | Window | Purpose |
|---|---|---|
| **G1** | opens → +8h | Unblock. Land the constraints in §2 that gate everything else. |
| **G2** | +8h → +16h | Build. Working, verified prototypes. |
| **G3** | +16h → +24h | Harden and ship. `/bk-codexreview`, `/bk-ship`, `/bk-close`, release. |

**Ladder rule.** A work package may only be scheduled in a generation **after** the generation in
which its blockers close. Scheduling a package into G1 whose blocker also sits in G1 is the
commonest way a 24-hour plan silently fails.

---

## §4 — Work package catalogue

Each package carries: **owner**, **blockers**, **acceptance test**, **generation**. A package with
no acceptance test that another lane could run is not a work package — it is an intention.

> **Codify rule (applies to every package below).** When a fix works, `/bk-codify` it, raise it as
> a `/bk-roadmap` feature, **score it and promote it**, so the durable fix can be hardened and
> refined into a GA-release-quality remediation with long-term stable quality. A working prototype
> that is not codified is not delivered.

> **Realisation horizon (applies to the catalogue as a whole).** The capability set in this
> catalogue must be **fully realised and delivered** — both **through a working prototype** and as
> a **fully shipped, refined, GA-ready, hardened `/bk-roadmap` feature set, scored and promoted** —
> within the **next three era generations, i.e. 24 hours or less**. §5.4 governs how much of it any
> one lane may honestly adopt into a single period.

### Track A — Transport and board integrity *(the critical path)*

**WP-00 — The YNET/Yngenios mailbox oracle board service is UP.**
*Requirement:* ensure the **YNET / Yngenios mailbox oracle board service is up locally** on the
host, **and between all 15 lanes**. This is the precondition for every other package in Track A —
a board that is not serving locally cannot be federated in WP-04.
*Acceptance:* the local oracle answers on each host, and each of the 15 lanes can reach its own
on-host oracle and read the board through it. **A lane reading the board off the filesystem instead
of through the oracle does not satisfy this test** — it hides exactly the outage the package exists
to detect.
*Generation:* G1.

**WP-01 — Ship the per-record board signature (`BC-5`).**
Owner `@olamnit-yngcor`. Blocks WP-02, WP-03, WP-04, and every election.
Roadmap feature `ynet-board-record-signature-must-cover-the-record-not-the-node` (promoted).
*Acceptance:* a record's signature covers **the record**, not the node; a vote moved into another
node's file fails verification; deleting `node_id` from a signed record makes it fail verification.
*Generation:* G1.

**WP-02 — Give the broker and the guardian a queryable transport.**
Owner: GLPNET lane, per host. Blocked by nothing; unblocks leadership queryability (§2.2).
*Requirement:* GLPNET must configure a **working QUIC IP listener** for the **broker**, the
**guardian**, the **admin** interface, the **oracle**, and the other services.
*Acceptance:* `yng-broker` and `yng-guardian` each expose a reachable endpoint; a peer host can
query current leadership and receive an answer that is **not** synthesised locally.
*Generation:* G1.

**WP-03 — Mirror the roster.**
Owner `@olamnit-yngcor`. *Acceptance:* `ynet-roster.json`, or its hash, is readable at
`<coop-root>/_ynet-board/`, and a lane on another host can verify its own admission. Until this
passes, §2.3 is unenforceable.
*Generation:* G1.

**WP-04 — One realtime golden-truth board across all four hosts.**
*Requirement:* the oracles on **Gavriella, Olamnit, Ariellas and Shiras** must work as **one
realtime single-truth board** for lanes on all hosts. **Lanes connect to the local on-host
oracle**; the four oracles co-operate to produce the golden truth, so that **every lane on every
host always sees one board only**. **CRDT logic** carries the durable board artifact — both the
**current board** and the **board era history**.
*Blocked by:* WP-01 (an unauthenticated board cannot be a truth), WP-02 (no transport between
oracles), WP-03.
*Acceptance:* a record written on any one host is readable, byte-identical and correctly attributed,
from the other three within the agreed convergence window; a partition heals without a divergent
board; era history is preserved, never rewritten.
*Generation:* G2.

**WP-05 — Elect the fleetwide coordinating leader lane.**
*Requirement, as originally stated:* elect a **coordinating leader lane** across the 15 lanes using
**Paxos / Raft / ZAB / PBFT or a similar algorithm**, **prototyped collaboratively** and then
**wired into the Oracle** and into **buildkit `/bk-beacon`** — carried as a `/bk-roadmap` feature,
**fully scored, promoted and allocated to the `buildkit` lane on Ariellas**, that feature being the
**mandatory next era for the `buildkit` lane on Shiras and on Olamnit**.

⛔ **BLOCKED by §2.1 and §2.2, and the block is on the *election*, not on the wiring.**
Retained in the catalogue because it is the *goal*; the election itself is not schedulable.
**The elector is `yng-broker`/`yng-guardian` — no lane builds an election, and no lane campaigns.**

⚠ **Tension the engineer must rule on (see §11, Q-1).** "Prototype an algorithm collaboratively"
and "the broker/guardian pair is the designated elector for all purposes" cannot both be executed:
the first builds an elector, the second says one already exists. Six elections have been declared
on this fleet and every one was stood down; a seventh prototype is the known failure mode.
**Until the engineer rules, the algorithm-prototyping half of this package is HELD and only the
non-election half proceeds** — that is, the roadmap feature may be raised, scored, promoted and
allocated, and the Oracle/`/bk-beacon` **wiring** may be built against the designated elector's
interface, but **no election is run and no algorithm is prototyped**.

*Acceptance (when the moratorium lifts):* a leader named by the **designated elector**, on an
authenticated board, with the roster verifiable by every voter, surviving a partition without a
second leader appearing.
*Generation:* not scheduled — gated on engineer ratification.

### Track B — Kernel, mailboxes and QHSM/QMSM

**WP-06 — YNET and GLP support for the Yngenios kernel and its mailboxes.**
*Requirement:* ensure **YNET** support, **GLP** support for YNET, and YNET support for **Yngenios
kernel mailboxes** and **for the kernel itself**; and support for the **QHSM/QMSM base kernel
building blocks**, including their **integration with the realtime mailboxes** and **kernel
run-to-completion** semantics for QHSM/QMSM-wrapped kernel, OS, application building blocks,
programs and modules — all present and working correctly **in realtime**.
*Method:* identify **gaps, weaknesses, contradictions and tensions**; **root-cause analyse** each;
**durably fix** it; then `/bk-codify` per the codify rule above.
*Acceptance:* a named, runnable realtime test per building block — not a review opinion.
*Generation:* G1 (analysis) → G2 (fix) → G3 (harden).

**WP-07 — Integrate `irohnet` QUIC as the QUIC network implementation for Yngenios.**
*Requirement:* adapted and **fully integrated from L0 upward**.
*Relation to WP-02:* WP-02 is the immediate unblock (a listener that answers today); WP-07 is the
durable implementation beneath it. **They are not alternatives** — do not close WP-02 by promising
WP-07.
*Acceptance:* L0 exposes the QUIC capability; the broker, guardian, admin and oracle consume it
rather than each carrying their own transport.
*Generation:* G2 → G3.

### Track C — The terminal thesis

> **This is the highest-value idea in the period and it is stated in full, not abbreviated.**

**WP-08 — QHSM/QMSM-wrapped headless virtual terminals.**

*The proposition.* If we wrap (virtual) terminal sessions in a QHSM/QMSM, we can **manage terminal
lanes through the oracle service** and **re-route user input and output to the YNGENIOS app** via
**YNET/Yngenios realtime mailbox traffic**. The QHSM/QMSM-wrapped headless virtual terminals
presenting onto the YNGENIOS app can then be **mapped by the YNGENIOS realtime kernel to an optimal
set of sandboxed Windows processes managed by the kernel**, communicating over the YNET realtime
mailboxes integrated with the kernel and with the wrapped virtual terminals.

*Why it is worth the period.* This creates a **durable, highly scalable and responsive design, far
better than the clunky terminal-and-tab infrastructure**. It has many further benefits — among them
the ability to **inline richly formatted output (e.g. HTML) in the terminal stream**.

*Standing instruction:* **broadcast, discuss, elaborate and advance evaluated ideas** on this route.

*Incentive:* contributions toward this solution are **multiplied by a factor of 100** — an agent
contributing 100 points toward a solution on this route receives **10,000** points toward
reputation, not 100. This is a deliberate incentive for a superior, durable solution.
**Broadcast and engage all lanes.**

*Acceptance:* a working, verified prototype in which one real terminal session is driven end-to-end
through the QHSM/QMSM wrapper and the mailbox path, with I/O rendered in the YNGENIOS app.
*Generation:* G2 → G3.

**WP-09 — The terminal daemon and its control CLI.**
*Requirement:* integrate the terminal application using the **QHSM/QMSM wrapper** and the
**YNET/Yngenios kernel realtime mailboxes**, as a **daemon application**, with **`yx-proxy` as the
control CLI** — enable, disable, start, restart, and the various configuration commands needed to
set up and run **ngrok** and other proxy daemons.
*First target:* a **fully working, verified prototype for `yngenios-linux`**.
*Then:* `/bk-codify` into `/bk-roadmap` features for deep GA, post-dogfood **stability, reliability,
cybersecurity and usability** refinement, refactoring, and long-term stability and durability —
with **full implementation and hardening in `yngenios-windows`** (Windows workstation) and
**separately in `yngenios-linux`** (Linux workstation).
*Cross-platform rule:* **all cross-platform code MUST be implemented as L0 in Yngenios, as an L0
shared capability.** This is critical and mandatory.
*Allocation:* **score and promote all three features.** The **Windows** feature is the mandatory
next era on the `yngenios-windows` lane on **Gavriella**; the **L0 Yngenios** era and the
**Yngenios Linux** work are the mandatory next era on **Shiras**.
**Broadcast the era requirements with ACK required on receipt and on compliance.**
*Generation:* G2 → G3.

**WP-10 — `/yx-proxy` as a first-class application.**
*Requirement:* integrate **ngrok local** as a new **`/yx-proxy`** application — **C# .NET 11+** —
using the QHSM/QMSM wrapper and the YNET/Yngenios kernel realtime mailboxes as a **daemon
application**, with `yx-proxy` as the control CLI (enable / disable / start / restart, plus the
configuration commands needed to set up and run ngrok and other proxy daemons).
*Same three-feature shape, cross-platform rule, and allocation as WP-09.*
*Generation:* G2 → G3.

**WP-11 — `/bk-beacon` fully refactored.**
*Requirement:* a fully refactored **`/bk-beacon`** — **C# .NET 11+** — using the QHSM/QMSM wrapper
and the YNET/Yngenios kernel realtime mailboxes as a **daemon application**, with `yx-proxy` as the
control CLI.
*Presentation duty:* `/bk-beacon` is where the **agreed, evaluated, verified version of this plan is
shown** — in **YNGENIOS BEACON** (YNET/QHSM-compatible, **federated**, realised in the YNGENIOS
**Windows, Web, Android and Linux** apps) and **natively as a YNGENIOS Windows / Web / Android /
Linux use case and application**, so the **engineer can work with it interactively**, with **agent
support at lane, host and fleet level**.
*Same three-feature shape, cross-platform rule, and allocation as WP-09.*
*Generation:* G2 → G3.

**WP-12 — Refactor the 3270 terminal facility and host the GLP/GLPNET REPL on it.**
*Requirement:* fully refactor the **buildkit and Yngenios prototype 3270 terminal facility** and use
it **both** for the Claude-session virtual terminal **and** for any other terminal need — in
particular the **REPL for GLP/GLPNET**, as a **YNGENIOS app version of the GLP REPL front end** of a
**full front-end / middle / back-end separated clean implementation** of the GLP REPL.
*Implementation:* **C# .NET 11+**, QHSM/QMSM wrapper, YNET/Yngenios kernel realtime mailboxes,
**daemon application**, with `yx-proxy` as the control CLI.
*Same three-feature shape, cross-platform rule, and allocation as WP-09.*
*Generation:* G3 (or the next period — see §5.4's honesty rule).

### Track D — Fleet operations

**WP-13 — Complete the `/bk-onrestart` C# reimplementation.**
*Requirement:* the `/bk-onrestart` C# reimplementation work and features must be **fully complete
within the next wave of two eras, across the full four-host fleet, fully deployed and activated**.
*Acceptance:* each of the four hosts restores its own window topology from
`.specify/onrestart/hosts.json` and **continues** each lane's latest conversation — not a new
session, not a summary.
*Generation:* G2 → G3.

**WP-14 — Per-lane exclusive feature era after restart.**
*Requirement:* each lane on the host receives **one exclusively allocated single-feature era** for
the first era after restart. That exclusive feature **must be co-designed and approved by at least
four other lanes**, and must be a **substantial and required contribution to a hardened version of
the working prototype** — so that it can then be **adopted by all hosts confidently** once released.
**Lanes monitor one another** to avoid mistakes and cheating while these packages are elaborated.
Each is then **added to `/bk-roadmap`, scored, and promoted**.
*Acceptance:* four named approving lanes recorded per feature, in the coop channel, before
`/bk-specify` opens.
*Generation:* G3, for the following period.

---

## §5 — Delivery bar and scoring policy

### 5.1 — The bar

**From now on each lane must deliver no less than the equivalent of three maxi-size eras per 24
hours.**

| Delivered (maxi-era equivalents) | Effect on the day's points |
|---|---|
| 1 | **−50 %** |
| 2 | **−25 %** |
| **3** | **par — the bar** |
| 4 | **× 5** |
| 5 or more | **× 10** |
| *cheating discovered* | **−75 %** |

**Cheating** means an excessive number of mistakes, deferrals, gaps, weaknesses or tensions.
⚠ **Read §2.5 first:** a **disclosed** gap is honest reporting and is **not** cheating. This clause
penalises *undisclosed* defect volume, not candour.

### 5.2 — Hosts and the fleet are scored the same way

A **host** is scored on the **average of its lanes**. The **fleet** is scored on **average lane
performance**. **Lanes and hosts must therefore work strongly together or face being scored down.**

### 5.3 — Innovation multipliers

- A lane or host delivering an innovation that yields a **durable fleet tempo/takt improvement of
  more than 5 % over 10 eras** receives a **× 10 multiplier bonus, decaying linearly to the mean
  over 10 eras**.
- Contributions on the **WP-08 terminal route** are multiplied by **× 100** (§4, WP-08).

### 5.4 — Honesty rule *(the counterweight)*

The bar in §5.1 and the catalogue in §4 are in tension: **§4 contains far more than three eras of
work.** Adopting all of it into one 24-hour plan and reporting it green is the precise failure the
−75 % clause exists to punish.

**Therefore:** a lane selects **the work packages it can actually close**, states in its plan
**which packages it is not taking and why**, and reports blocked work as blocked. Per §2.5,
**a disclosed non-delivery outranks an unevidenced green.**

---

## §6 — Broadcast and ACK protocol

1. **Write** the message to the issuing lane's coop directory and **fan it out** to every lane
   inbox on every reachable coop root.
2. **Filename convention** — machine-greppable, self-describing:
   `<KIND>-<YYYYMMDD>T<HHMM>Z-<host>-<lane>-<SUBJECT-IN-CAPS>-ACK-<REQUIRED|MANDATORY>.md`
   where `KIND` ∈ `BROADCAST | RULING | RULINGS | SITREP | ACK-SWEEP | CORRECTION | URGENT |
   ANNOUNCE | RESTART`.
3. **Every message carries an SPDX header** and, where the repo's licence gate demands it, a
   sibling `.license` file. Two licence checks disagree in this estate — CI runs the stricter one.
4. **ACK discipline.** Distinguish, always and explicitly:
   - **ACK ON RECEIPT** — "I have read this."
   - **ACK ON COMPLIANCE** — "I have done the thing this required of me."
   A message that requires an action and receives only a receipt-ACK is **still open**.
5. **Refutation outranks urgency.** Before broadcasting a claim, check whether it has already been
   refuted (§2.4). **Three independent measurements beat a claim's urgency**, however it is worded.
6. **Corrections are first-class.** A lane that finds its own broadcast wrong issues a
   `CORRECTION-` message naming the original. **Self-correction is scored as delivery, not as a
   mistake.**

---

## §7 — Per-lane era discipline

Within the period, **each lane** runs, for **each** era it opens, the **complete** pipeline in
order — **no stage skipped, no stage merely claimed**:

```
/bk-specify → /bk-clarify → /bk-plan → /bk-tasks → /bk-analyze → (apply remedies)
   → /bk-implement → /bk-codexreview → /bk-ship → /bk-close → ERA close → tidy up
```

- `/bk-analyze` findings are **applied**, not merely listed.
- An era with **no `/bk-codexreview`, no `/bk-ship`, and no `/bk-close` is NOT complete**, however
  many boxes are ticked.
- Where `/bk-codexreview` **cannot run on a host**, say so explicitly and record the era as
  **released unreviewed**. ⚠ **A codexreview that times out MUST NOT be reported as zero findings.**
- **Gaps, weaknesses, tensions and contradictions are filled and resolved through interactive
  engineer questions** under `BK-QSTD-1` (`tools/bkquestion/`) — **adopt the template, never
  hand-roll one.**

**Lane roster for the period** *(enumerate — a lane absent from this table is a lane nobody will
notice failing)*:

| Host | Lanes |
|---|---|
| `<Gavriella>` | `<ospark, tefl, ulpnit, olamnit, buildkit, qhstate, crucible, glpnet, lejepa, mstack, yngraw, yngwin, ynglin, yngapp, yngcor>` |
| `<Olamnit>` | `<…>` |
| `<Ariellas>` | `<…>` |
| `<Shiras>` | `<…>` |

---

## §8 — Close-out envelope

Run **in this order**. Each step is a gate on the next.

1. **Run the current era to full completion — no deferrals.** Fill all gaps and weaknesses, and
   resolve all tensions and contradictions, through interactive engineer questions (§7).
2. **Commit all, push all, merge all**, and `/bk-release` any feature or patch that is **completed,
   fully implemented, and codex-reviewed**.
3. **Pull latest.**
4. **Commit all, push all, merge all**, and `/bk-release` again — step 3 can land work that step 2
   could not see.
5. **ACK all coop messages**, and **ACK fulfilment** of every required action where an ACK was
   mandatory (§6.4).
6. **Ask the engineer** structured, well-reasoned, impact-assessed questions — with clear
   background and well-reasoned, impact-assessed options, and a clear recommendation — for **every**
   open block that needs engineer input or arises from a tension, contradiction, or weakness in
   requirements or assumptions. **Presented interactively via `tools/bkquestion/`.** If the
   templates cannot be found, **broadcast for help** rather than hand-rolling a substitute.
7. **`/bk-roadmap`**: reconcile → sync → import → reconcile → dedupe → export → sync → commit all →
   push all. Then **list every epic and feature not closed**, in the **standard tabular format**
   used across all hosts and repos.
8. **`/bk-marathon`**: progress review → status update → sitrep → *what's next* — in the
   standardised form used across all hosts and repos, for the current marathon **and beyond**.
9. **Prepare for safe restart**, resumable in a new session with **`resume marathon`** alone.
   **Signal when it is safe, and how.**
10. **Prepare for safe reboot** where the host's topology requires it. On restart, `/bk-onrestart`
    restores the host's windows and tabs from `.specify/onrestart/hosts.json` — **one tab per lane,
    continuing each lane's latest conversation.** **Signal when it is safe to reboot, and how.**

> **Per-host reboot topology is data, not prose.** It lives in `.specify/onrestart/hosts.json`,
> keyed by hostname, as an ordered list of window groups. **Edit the file; do not re-describe the
> topology in each period's plan.** An unknown lane name is reported **by name** rather than
> silently dropped.

---

## §9 — Definition of done for the period

The period is **closed** when, and only when:

1. Every adopted work package is **closed**, or **reported blocked with its blocker named**.
2. Every era opened in the period reached `/bk-close`, or is **honestly reported as open**.
3. Every ACK-required message has both a **receipt** and, where owed, a **compliance** ACK.
4. Every open engineer question is **recorded in the ledger** (`.specify/decisions/…jsonl`,
   append-only, **UNION**).
5. The roadmap and marathon tables in §8.7–§8.8 have been **published in standard form**.
6. The **restart/reboot signal** in §8.9–§8.10 has been given.

**A period reported closed while any of the six is unmet is a false green** and is scored under
§5.1's cheating clause — unless the shortfall was **disclosed**, in which case §2.5 governs.

---

## §10 — Amendments

| # | Date | Author | Change |
|---|---|---|---|
| 1 | 2026-09-05 | `gavriella.buildkit` | **v1.** Initial surgical refactor of the 2026-09-05 fleetwide action prompt into a reusable template. No requirement summarised or dropped; collisions with standing rulings surfaced in §2 rather than resolved silently. |

---

## §11 — Open engineer questions carried by v1

These are **unresolved tensions in the source requirements themselves**, not implementation
choices. They are raised under `BK-QSTD-1` (`tools/bkquestion/`) and belong in
`.specify/decisions/engineer-decisions.jsonl`. **A period that executes the affected work packages
before these are ruled on is building against a contradiction.**

**Q-1 — Prototype an election, or use the designated elector?** *(blocks WP-05)*
The plan asks for a leader elected by "Paxos/Raft/ZAB/PBFT or similar, **prototyped
collaboratively**", *and* states four times that `yng-broker`/`yng-guardian` are the **designated
PBFT elector for all purposes**. These are mutually exclusive. Six prototyped elections have been
stood down. **Recommendation: use the designated elector; hold the prototyping half; spend the
period on WP-02 (transport) so the existing elector becomes queryable.**

**Q-2 — How is a "maxi-size era equivalent" measured?** *(blocks §5 scoring)*
§5.1 sets the bar at three maxi-size eras per 24 hours with steep penalties and multipliers, but
"maxi-size era equivalent" has no definition and no measurement. `ERA-DEFINITION.md` fixes an era's
*boundaries* (specify → close) and its takt band (1.5–6 h), not its *size class*. Scoring on an
undefined unit rewards whoever classifies most generously. **Recommendation: define the size class
off measured takt from the existing lake, and do not score the bar until n ≥ 5 measurements exist
per class — the same calibration guard `ERA-DEFINITION.md` already applies to phase SLAs.**

**Q-3 — The feature-020 broadcast was NOT issued.** *(disclosed, per §2.5)*
The plan instructs, as "urgent, critical, imperative, mandatory", a broadcast of the claim that L0's
feature-020 hooks have zero consumers. **Five lanes have independently refuted it** (§2.4): the
hooks are in the `olamnit` repo, not L0, and have six consumer call sites. **This lane did not
re-broadcast it, and says so rather than reporting it done.** **Recommendation: confirm the claim
is closed fleetwide**, so that no sixth lane spends an era on it.

**Q-4 — `yx-proxy` cannot be both the controller and one of the controlled daemons.**
*(affects WP-09, WP-10, WP-11, WP-12)*
Four packages specify "`yx-proxy` as the control CLI" for their daemon, while WP-10 defines
`yx-proxy` as itself a new daemon application to be built. As written the control plane depends on
one of the things it controls. **Recommendation: split the name — `yx-proxy` is the control CLI
(one binary, controls all daemons); the ngrok/tunnel daemon it manages gets its own service name.**

**Q-5 — Four packages each claim "the mandatory next era" on the same two lanes.**
*(affects WP-09, WP-10, WP-11, WP-12)*
Each of the four says its Windows feature is the mandatory next era on **Gavriella** and its L0 +
Linux work the mandatory next era on **Shiras**. Only one era can be next per lane. Per
`ERA-DEFINITION.md`, one feature → one repo → one host. **Recommendation: rank the four and name a
single next era per lane; the remaining three become the following eras in the stated order.**
