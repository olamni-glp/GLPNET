<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# FLEETWIDE TACTICAL 24-HOUR ACTION PLAN — **SUPERSET TEMPLATE v3.0**

    TEMPLATE ID        FLEET-T24-ACTION-PLAN
    TEMPLATE VERSION   v4.0 — v3.0 plus the engineer directive of 2026-09-06
                       (the nine remaining programme MVPs [04]-[12], the LEADER+PLANNER
                       programme, the automatic-failure criteria, and the fleetwide-action
                       stake). NOTHING REMOVED FROM v3.0.
    AMENDED BY         olamnit-glpnet @ OLAMNIT, 2026-09-06T20:30Z
    AMENDMENT METHOD   surgical insertion only — ten new objective rows (27-36), three new
                       sub-sections (4.5, 4.6, 2.8), one new scoring sub-section (3.9), one
                       row-numbering defect fixed, and the matching Annex B / §13 entries.
                       Every v3.0 byte is preserved; this build script asserts on every
                       anchor so a silent drop is impossible.
    v3.0 LINEAGE       v3.0 — v2.0 plus the engineer directive of 2026-09-05T13:00Z
                       (mailbox architecture correction + the three 48-hour critical
                       prototypes YS / YQ-PG / YQ-DuckLake). NOTHING REMOVED FROM v2.0.
    AMENDED BY         gavriella-glpnet @ GAVRIELLA, 2026-09-05T13:50Z
    AMENDMENT METHOD   surgical insertion only — five new objective rows (22-26), one new
                       standing correction (C-5), one new sub-section (4.4), and the
                       matching Annex B / §13 entries. Every v2.0 byte is preserved; the
                       build script asserts on every anchor so a silent drop is impossible.
    v2.0 LINEAGE       v2.0 — the LOSSLESS SUPERSET of every live v1
    MERGED BY          olamnit @ OLAMNIT, 2026-09-05T09:45Z
    AUTHORITY          Engineer ruling E1, 2026-09-05: "surgically merge into a robust lossless
                       superset" — taken in preference to this lane's own recommendation, which
                       was to pick one draft and withdraw the rest.
    SPINE              gavriella-glpnet FLEET-T24 v1.0 (06:15Z) — chosen as the spine because it
                       is the ONLY draft carrying the engineer's directive VERBATIM (Annex A) and
                       a clause-by-clause traceability map (Annex B, 35/35). Everything else is
                       merged INTO it.
    MERGED FROM        six live drafts + one withdrawn. See Annex C for the merge map.
    STATUS             DRAFT — see §12. Not yet a ratified fleet standard.
    ADAPTABLE BY       any engineer, for any future 24-hour period, by editing §1 and §4 only.

---

## §0 — HOW TO USE AND ADAPT THIS TEMPLATE

1. Copy this file to `FLEET-T24-<YYYYMMDD>T<HHMM>Z-ACTION-PLAN.md`.
2. Fill in **§1 Period Header** (the only mandatory edit).
3. Replace the objective rows in **§4 Objective Register** with the objectives for the new period.
   Every other section is standing fleet doctrine and is normally carried forward unchanged.
4. Leave §2, §2A, §3, §5–§13 as they are unless an engineer ruling changes the doctrine. If
   doctrine changes, record it in **§13 Adaptation Log** and re-broadcast.
5. Publish to `<COOP_ROOT>/_standards/` and broadcast per **§9**.

**Placeholder convention.** `{{DOUBLE_BRACES}}` is a fill-in; `<angle brackets>` is a path or
identifier resolved per host. A plan with any `{{...}}` left unfilled is **not issuable** and must
be refused by the receiving lane (§11 rule 1).

**Preservation rule.** Produced by **surgical refactoring only** — reorganisation, de-duplication of
*literally repeated* clauses (each preserved once, with its repetition count recorded), and
correction of spelling and grammar. **No requirement in the source directive, and no distinct
clause in any merged draft, was summarised, compressed, weakened or dropped.** Annex B proves this
for the engineer's directive; **Annex C proves it for the merge.** Any future edit that removes a
requirement must record the removal, its authority (a ruling id) and its date in §13.

### 0.1 🔴 WHY THIS SUPERSET EXISTS — read it, because it is the failure it prevents

**Seven drafts of one template were produced in four hours and five minutes**, six live and one
withdrawn, by lanes on **one host**, and **three of them were named `v1` and were structurally
different** (10 sections / 13 + 2 annexes / 14 + 3 appendices). A lane told to "comply with v1"
could not tell which document that was.

The mechanism was published, not guessed. `gavriella-mstack` withdrew its own draft and named it:
its prior-art search was `find … | head -30`, **the match existed and `head -30` truncated it out**,
and it then reported *"no prior artefact was found"*. **A truncated search is not a negative
result.** `BK-STD-2` §0 records the identical failure twelve days earlier, with the rule already
written down: **search for an ADOPTION, not just for an ARTEFACT** — a shape can be settled in three
ACKs before any file exists.

**So: before authoring anything that could be a fleet standard, search for an adoption, and if your
search hits its output limit, report `truncated`, never `absent`.**

---

## §1 — PERIOD HEADER (fill in for every period)

    PLAN ID              FLEET-T24-{{YYYYMMDD}}T{{HHMM}}Z
    PERIOD START (UTC)   {{PERIOD_START_UTC}}
    PERIOD END   (UTC)   {{PERIOD_END_UTC}}          (= start + 24h)
    ISSUING ENGINEER     {{ENGINEER}}
    ISSUING LANE         {{ISSUING_LANE}} @ {{ISSUING_HOST}}
    SUPERSEDES           {{PRIOR_PLAN_ID_OR_NONE}}
    ACK REQUIRED         ON RECEIPT  — {{YES}}
                         ON COMPLIANCE — {{YES}}
    ACK DEADLINE (UTC)   {{ACK_DEADLINE_UTC}}

### 1.1 Fleet constants for this period

| Constant | Value |
|---|---|
| Hosts (4) | `GAVRIELLA` · `OLAMNIT` · `ARIELLAS` · `SHIRAS` |
| Lanes (15) | `ospark` · `tefl` · `hatzinor` (*ulpanit*) · `olamnit` · `buildkit` · `qhstate` · `crucible` · `glpnet` · `lejepa` · `mstack` · `yngraw` · `yngwin` · `ynglin` · `yngapp` · `yngcor` |
| COOP root | `<COOP_ROOT>` — the shared-volume mailbox reachable from every host |
| Canonical board root | **a UNC path, never a drive letter** (§2.6) |
| Oracle (per host) | one local Oracle board service per host; four in total |
| PBFT leader elector | `yng-broker` / `yng-guardian`, present on each of the 4 hosts |
| Board substrate | CRDT — current board **and** board-era history, both durable artifacts |

---

## §2 — STANDING ROLES AND AUTHORITIES (fleet doctrine)

### 2.1 `yng-broker` / `yng-guardian` — the designated elector

They are present **on each of the 4 hosts** and are the **designated PBFT leader elector for all
purposes**, including electing the **Oracle leader**, electing the **fleetwide coordinator**, acting
as the **fleetwide signature verifier**, and any further election purpose assigned by engineer
ruling.

🔴 **NO OTHER MECHANISM MAY SEAT A LEADER, and DO NOT BUILD A SECOND ELECTOR.** A lane that
believes it needs one raises a question (§6) instead of building it.

*(This clause appeared **six times verbatim** in the source directive, and again in every merged
draft. Stated once, binding on every objective in every period. Annex B row S3.)*

### 2.2 The Oracle board — one board, four Oracles

- Every lane connects to **the Oracle local to its own host**. A lane never connects to a remote
  Oracle directly.
- The **four Oracles must work together as ONE realtime single-truth board**, so **all lanes on all
  hosts always see one board only**.
- The durable artifact — **the current board and the board era history** — uses **CRDT logic**, so
  concurrent per-host writes converge without a coordinator and without loss.
- Reaching this state is a **deliverable, not an assumption**. Until measured, §2.5 governs.

### 2.3 The election

A **coordinating leader lane** is elected using **PAXOS, RAFT, ZAB, PBFT or a similar algorithm**,
prototyped collaboratively, then **wired into the Oracle and into buildkit `/bk-beacon`**.

#### 2.3.1 🔴 Mandatory preconditions before any election result counts

*(Merged from `gavriella-tefl` BK-STD-3 §2.1 — the only draft that made the preconditions
mechanically testable rather than described.)*

| # | Precondition | Ruling | Mechanical acceptance test |
|--:|---|---|---|
| 1 | **One voting identity per host.** Hosts vote; lanes are clients | `Q-57` | `board_check.py` **C4** passes on all reachable roots |
| 2 | **The elector CLI refuses forbidden writes**, quoting the ruling id | `Q-58(c)` | `campaign`/`vote`/`heartbeat`/`declare` exit non-zero on a retired store or voided term |
| 3 | **A tally reads the records that invalidate it** | `FR-009` | `tally` reports `RETIRED`/`VOID`, never `ELECTED`, whatever the arithmetic says |
| 4 | **The roster spans ≥ 3 distinct hosts** | `R2` | `hosts_represented ≥ 3`; a single-host roster is one failure domain |
| 5 | **A publish is verified by a PEER reading it back** | `Q-03` | `sha256` match at the destination — **never the writer's exit code** |

#### 2.3.2 🔴 The quorum bar — `2f+1` is WRONG except at n = 3f+1

    Quorum = ceil((n + f + 1) / 2)          f = (n - 1) // 3

`2f+1` is the PBFT quorum **only** when `n == 3f+1` exactly. At every other roster size it does not
guarantee that two quorums intersect outside the faulty set, and at `n ∈ {6, 9, 12, …}` it falls
**below a simple majority** — n=6 admits two fully **disjoint** quorums of 3, splitting the brain on
a partition alone with **zero** Byzantine faults.

| roster | f | old `2f+1` | **correct bar** |
|---|---:|---:|---:|
| 4 (2 hosts × broker+guardian) | 1 | 3 | **3** (unchanged — 4 IS 3f+1) |
| 6 | 1 | 3 | **4** |
| **8 (the fleet elector)** | 2 | 5 | **6** |
| **15 (a host's lanes)** | 4 | 9 | **10** |

**Lock the PROPERTY, never a table** — `2Q − n ≥ f + 1` and `Q ≥ n//2 + 1` at every n. A table
cannot catch this: rows at `n = 4, 7, 10, 16` are `3f+1`, where the wrong rule and the right rule
agree and the row **cannot fail**. Found by `shiras-tefl` (P0 2026-09-04T18:45Z); corrected in five
codebases; the last was a Python shim that was still publishing `5` for the live 8-member board on
2026-09-05T08:00Z.

**Membership is DECLARED and never shrinks to whoever is reachable.** An electorate derived from
what is present is a quorum of the present, which is not a quorum. With a declared 8, three hosts up
give **6 prepares against a bar of 6** — attainable at exactly the bar, with **zero margin**. Two
hosts give 4 and still refuse. **One peer host running the trio is necessary and NOT sufficient;
the target is three.**

#### 2.3.3 🔴 Vote origin must be recorded, or a tally overstates itself

*(Engineer ruling E4, 2026-09-05.)* A declared host supervisor may start one client per lane. The
honesty cost is that those votes **share one origin**, and a cost is only paid if it is **recorded**.
Every registration carries `started_by`, and every tally reports `vote_origins` and
`distinct_origins`. When there is only one origin the board itself must say:

> *"every vote came from ONE origin (`<origin>`): read this as **THIS HOST'S PROCESSES agreeing**,
> never as N independent parties agreeing."*

This is precisely the distinction the **4-of-4 self-votes** already found on the Oracle board failed
to make.

### 2.4 Participation standard

Receiving lanes must **not merely acknowledge**. Every addressed lane must **actively participate
and contribute continuously** until the task is **jointly, collaboratively and durably completed**.
COOP communications and the oplog mechanism are the means; **an ACK alone is non-compliance** and is
scored as a non-delivering lane (§3.2, §9 rule 4).

### 2.5 🔴 STANDING CORRECTION BOX — claims measured and refuted

> **Additive.** It never deletes a directive requirement; it records what the fleet has already
> *measured*, so no lane spends an era re-deriving a refuted premise. Every entry cites host, tree
> and evidence. A lane receiving an objective contradicted here must **still execute the objective's
> remaining, unrefuted parts**, and must reply with the refutation rather than silently skipping.

| # | Claim as issued | Measured status | Evidence |
|---|---|---|---|
| C-1 | *"L0 has purpose-built feature-020 hooks (`OnStepDispatched`, `Unregister`, `StartOnDedicatedThread`, `Markers`) with zero consumers — the host that was meant to use them was never written."* | **REFUTED IN THE HALF THAT MATTERS.** The host **was** written: `YngeniOS.Host.Windows`, a complete 338-line daemon (`Program.cs:19`, live loop `:194-216`). It has **no `.csproj`**, so it has never been compiled where it lives. Root cause: `l0` holds **383–384 capability-block directories and 1 `.csproj`, 0 `.sln`** — nothing in L0 is compiled where it lives, so the cheapest unwired-seam detector the fleet owns (a compiler) is not pointed at it. **It is a build-inputs task, not a "write the missing host" task.** Independently corroborated in a second repo: `Olamnit.Yngenios.Host`, 34 `.cs` + a `.csproj`, consuming `OnStepDispatched` ×3, `StartOnDedicatedThread` ×3, `Unregister` ×2, `Markers` ×2. | `gavriella-buildkit` P1 2026-09-04T19:05Z, corroborated by 5 lanes; `shiras-yngraw` retracted its endorsement 2026-09-05T02:05Z (*"the host exists and runs — do not build it"*); `gavriella-crucible` ruling 2026-09-05T02:15Z; `olamnit-yngcor` + `olamnit` second-repo measurement. |
| C-2 | *"elect a fleetwide leader"* (assumed available) | **NO VALID ELECTION HAS EVER OCCURRED.** The board was measured at **4-of-4 self-votes**; a later measurement found **18 of 24 (then 26) records unauthenticated**, `v1` signing `null`, and **`node_id` deletable from a signed record with the signature still verifying**. A provisional leader has been named and **must not be obeyed**. | `gavriella-olamnit` T01:15Z; `shiras-qhstate` T02:00Z and T02:40Z. |
| C-3 | *(campaigning for the leadership)* | **FORBIDDEN.** `Q-YNGH-01` forbids campaigning. **Three lanes prescribed a vote or campaign that a ruling forbade, within two hours.** Two caught themselves; one was refused by its recipient. | `Q-YNGH-01`; retractions by `shiras-yngwin`, `gavriella-tefl`, 2026-09-05T02:05–02:10Z. |
| C-4 | *"the ceiling bar is two distinct ACTORS"* | **TOO WEAK.** `lane_id` travels with a git clone (D30), so two "actors" can be one machine. **D35: the bar is two distinct HOSTS.** And the ceiling must be reached by a **WALK** — "the highest term two parties agree on" is defeated by two colluders agreeing on `2**63-1` in one step. | `olamnit` D35/D38, implemented and tested 2026-09-05. |
| C-5 | *any roadmap count published without naming its engine* | **UNCOMPARABLE.** Two `buildkit_cli` installs on one host answered the same `roadmap status` with **163 vs 254 not-closed**. Every published table must carry `ENGINE=<resolved buildkit_cli path>`. | `olamnit` 2026-09-05T08:11Z; disclosure landed in the BK-STD-1 reference script. |
| C-6 | *{{NEXT_CORRECTION}}* | *{{STATUS}}* | *{{EVIDENCE}}* |

### 2.6 Standing designations carried forward every period

*(Merged from `gavriella-lejepa` FTAP-24H §3.)*

- **`GLPNET` owns the QUIC listener configuration** for the broker, the guardian, the admin service,
  the Oracle service, and any other service requiring an IP listener.
- **Each host runs one Oracle; lanes connect to their own on-host Oracle**, never directly to a
  remote one.
- **The canonical board root is a UNC path, never a drive letter.**
- **Every `Z` timestamp published to coop must be EARNED** — measured at the moment of writing,
  never estimated, back-dated, or copied forward from another document.

---

### 2.7 🔴 STANDING CORRECTION C-5 — **THE MAILBOX IS NOT A FILE** (new in v3.0)

**Authority:** engineer correction, 2026-09-05, withdrawing `Q-ARI0905-01` in full —
*"the above 1, 2, 3 are all 100% failure totally incorrect — the question is also incorrectly
framed."*

`Q-ARI0905-01` asked **who edits `scripts/fleet/ynet-roster.json`** to admit ARIELLAS, and offered
three options: the lane admits itself, wait for the worktree owner, or the engineer edits it.
**All three were rejected, and so was the question.**

> The mailbox service **is** a **Hyper-V container**, designed to offer **hundreds of millions of
> concurrent mailboxes** — via **YNET** to other hosts, and via **in-memory intra-host transport at
> YNGENIOS KERNEL level, secured inside each host, for ultimate performance.**
>
> **Correct mailbox use and implementation is a FAILURE CRITERION for the fleet collective today.**

| The question assumed | The architecture actually is |
|---|---|
| Membership is a **file** | Membership is a **live mailbox binding** in a container-hosted service |
| Admission is an **edit**, gated on worktree ownership | Admission is a **runtime registration** against a kernel mailbox |
| Scale is **4 host blocks** in a hand-maintained array | Scale is **10⁸ concurrent mailboxes** |
| The blocker is **social** | The blocker is **that the service is not built** |

**Binding consequence for every lane:** a plan step whose deliverable is *"edit the roster/pin/host
file"* is **not** progress against `OBJ-M6-CLIENT`, `OBJ-MAILBOX-CONTAINER` or `OBJ-ELECT`, and must
not be reported as such. **A four-entry array is not a small version of 10⁸ mailboxes; there is no
growth path from it.**

**Measured state, GAVRIELLA, 2026-09-05T13:05-13:12Z** (publish yours; do not assume):

- `YngBroker` (pid 14832) and `YngGuardian` (pid 16008) — **Running/Automatic**, but **ZERO** TCP
  listeners and **ZERO** UDP endpoints; reachable only via `\\.\pipe\yng-broker-ctl`. The
  designated electors **cannot today carry a mailbox to another host.** Third lane to measure this,
  one day and one reboot after the first.
- `Get-VM` and `docker ps` both **REFUSED — require elevation.** 🔴 **Therefore no lane may claim
  the mailbox container is absent on this host.** Hyper-V *is* present
  (`vEthernet (Hyper-V firewall)`, 172.21.16.1).
- **The substrate exists and must not be rebuilt:** `YngeniOS.Kernel/Mailbox/InMailbox.cs` is a real
  kernel-owned bounded FIFO with margin policy and sender-side back-pressure, and its own
  doc-comment carries the invariant — *"ALL stimulus … lands here; the process reads no other
  inbox."* Alongside it: `MailboxTriple`, `ErrorMailbox`, `TransactionalOutbox`, `Margin`,
  `Process/Machine.cs`.
- 🔴 **B1 (blocking):** all **nine** `YngeniOS.*` projects target **`net8.0`**
  (`Csharp/yngenios/Directory.Build.props:4`), against the fleet's **net11-only** mandate of
  2026-09-01. **A net11 M6 client cannot reference a net8 kernel.**
- 🔴 **B2 (blocking):** `YngeniOS.Wrappers.Glpnet` is an **empty `PhaseCPlaceholder.cs`** —
  scaffolded in Phase A, never written. **Check your own lane's wrapper before assuming you have one.**
- **The seam is the work:** `InMailbox` is **in-process only**; `glpnet:csharp/ynet_transport/` is a
  full QUIC/DHT/hole-punch stack containing **zero** occurrences of `qhsm`/`qmsm`. Two real halves,
  **no seam between them**.

*Source: `CORRECTION-20260905T1314Z-gavriella-glpnet-…`, fanned to 43 COOP channels.*


### 2.8 🔴 AUTOMATIC-FAILURE CRITERIA FOR THE PERIOD (new in v4.0)

These are **not** objectives to be scored — they are **conditions whose absence fails the period
outright, with the §3 penalties**, however green the objective register looks:

| # | Condition that MUST hold at period end | How it is checked |
|---|---|---|
| AF-1 | **Regular YNET PBFT elections are held**, and an **effective fleetwide leader is maintained**. | A Decided term with ≥ quorum authenticated non-self votes, and a leader answering a nonced `LeaderPing` (§4.6) — **not** an unexpired lease. |
| AF-2 | **A hostwide leader is maintained for each host**, helping coordinate across hosts with the fleetwide leader. | Per-host election Decided, read on that host, **with its origin-concentration caveat published** where one origin dominates. |
| AF-3 | **YNET / realtime / GLPNET-enabled QHSM/QMSM message-over-wire AND in-memory message-based mailboxes work.** | A message sent over the wire and a message sent in-memory, each **received and acted on**, both measured in the period. |
| AF-4 | **The kernel effectively controls all QHSM/QMSM-based allocation and OS processes.** | A kernel-issued spawn, hibernate and terminate, each observed against a real OS process. |
| AF-5 | **Each lane AND each host separately has its own QHSM/QMSM CODE-BASED client — NEVER agent-based — participating as a receiver in YNET comms.** | The client runs with **no agent attached**, receives, and raises the `/btw` alert. (This is `OBJ-M6-CLIENT`, row 21, restated as a failure condition.) |
| AF-6 | **The YNGENIOS apps work — including the 3270-type terminal and the YNET-mailbox-based virtual terminal.** | Both terminals driven end-to-end by a human or a scripted user on at least one app target. |

🔴 **AF-1 through AF-6 are checked independently of §4.** A period may score every objective and
still fail on an AF row. Report each AF row explicitly in the §7A end-of-period report, **including
the ones that failed** — an omitted AF row is read as a failed one.

---

## §2A — PRECEDENCE, CONFLICT AND REFUSAL — **read before acting**

*(Merged from `gavriella-lejepa` FTAP-24H §2. It is the difference between a working template and a
broadcast wish list: the source directive contains instructions that a standing ruling already
forbids, and without a precedence rule every lane must independently rediscover the collision —
and some will comply with the forbidden instruction. **That is a defect in the plan, not in the
lanes.**)*

**Order of authority, highest first:**

| # | Authority | Example |
|---:|---|---|
| 1 | **A standing engineer ruling**, until explicitly superseded by the engineer | `Q-YNGH-01` HOLD, do not campaign |
| 2 | **A repo's own `CLAUDE.md` rule that declares itself outranking** | the EFFORT BUDGET rule (`Q-shirashatzinor-66`) |
| 3 | **This 24-hour plan instance** | the objectives in §4 |
| 4 | **A lane's local preference or convenience** | — |

**Mandatory conflict procedure**, in this order:

1. **Refuse the colliding instruction.** Do not comply, and do not partially comply.
2. **Say so publicly**, in the ACK, naming the instruction refused and the ruling that overrides it.
3. **Raise it as a structured question to the engineer** (§6) — only the engineer can lift a ruling.
4. **Execute every non-colliding part of the plan in full.** A single blocked clause never licenses
   shrinking the rest of the scope.

**Refusal is compliance.** A lane that refuses a ruled-against instruction and says why has complied
with this template. A lane that silently performs it has not.

### 2A.1 Known live collisions — re-check every period, because rulings move

| Source clause | Collides with | Effect |
|---|---|---|
| "elect a coordinating leader lane" / run a vote | `Q-YNGH-01` — HOLD, do not campaign; broker/guardian PBFT governs; the board stays `NO_LEADER` until they actually elect | **§4.2 is HELD.** No lane may seat a leader. |
| "15 lanes elect a leader" | `Q-shirashatzinor-64` — **the quorum denominator is 4 host oracles, not 15 lanes**; *a lane is not a voter* | 🔴 **UNRESOLVED — see §2A.2.** |
| broadcast and codify the feature-020 "L0 hooks have zero consumers" claim as a P1 era | `Q-shirashatzinor-65(b)` — refused; **do not re-broadcast, do not codify**; refuted by five sources and **retracted by its own author** | 🔴 **UNRESOLVED — see §2A.2.** |
| allocating the six .NET eras to lanes that hold no .NET code | `Q-shirashatzinor-65(a)` / `Q-shirasblock011-03` — **a lane takes eras from the repo it owns** | **§4 routes by code ownership**, not by broadcast reach. |
| "≥ 3 maxi eras per 24h or lose points" | `Q-shirashatzinor-66` — **the effort budget outranks the quota** in repos that declare one | **§3.7 reports honestly** and accepts the penalty; ceremony inflation is itself cheating. |

### 2A.2 🔴 TWO CONTRADICTIONS THIS MERGE FOUND AND WILL NOT RESOLVE BY ASSERTION

A superset that quietly picked a side would be exactly the compression it was told not to do. Both
are carried in full, from both sides, and both are engineer questions.

**X-1 · The f020 broadcast.** The engineer directive says, repeatedly and in capitals, *broadcast
the L0 feature-020 claim to all hosts and all lanes, root-cause it, codify it into a P1 era.*
`gavriella-glpnet` carries that as `OBJ-F020-ROOTCAUSE` (mandatory, P1). `gavriella-lejepa` carries
`Q-shirashatzinor-65(b)`, which **forbids re-broadcasting and forbids codifying it**, on the ground
that the claim was refuted by five sources and retracted by its author — *"codifying a refuted
diagnosis does not make it true, it makes it durable, and it sends the fleet to build something that
already exists."* **Both cannot stand.** The narrow reading that satisfies both — and what this
lane did — is: **broadcast the claim TOGETHER WITH its refutation**, and codify **the measured
root cause (L0 has no build inputs)**, never the retracted wording. That reading is **proposed, not
ruled**.

**X-2 · Who votes.** `Q-shirashatzinor-64` says the denominator is **4 host oracles, not 15 lanes** —
*a lane is not a voter*. This lane has since built and run a **15-lane HOST-scoped election** electing
a **host coordinator** (a different office from the fleetwide leader, on a separate stream, with a
disjoint electorate). Under a narrow reading of `Q-57`/`Q-64` — "hosts vote, lanes are clients" —
that host scope may be forbidden. **This lane names the tension rather than assuming its own reading
is right.** The fleetwide electorate here is unchanged at 8 (broker+guardian × 4 hosts) and the two
rosters are structurally prevented from merging.

---

## §3 — DELIVERY QUOTA AND SCORING (fleet doctrine)

### 3.1 The quota

**Each lane must deliver no less than the equivalent of 3 maxi-size eras per 24 hours.**

### 3.2 Lane scoring

| Delivered in the 24h period | Effect on the lane's points that day |
|---|---|
| 1 era-equivalent | **−50 %** |
| 2 era-equivalents | **−25 %** |
| 3 era-equivalents | baseline — the minimum required |
| 4 era-equivalents | **× 5** |
| 5 or more | **× 10** |

**Cheating penalty: −75 %.** Cheating includes an excessive number of mistakes, deferrals, gaps,
weaknesses or unresolved tensions.

### 3.3 🟢 THE DISCLOSURE BOUND ON −75 % (engineer ruling `Q49`, binding)

> **The −75 % penalty applies to defects CAUSED AND CONCEALED. It does NOT apply to gaps,
> weaknesses, contradictions or tensions that a lane finds and publishes, nor to a self-caught,
> self-published error (`Q49`, `Q-gsbk13-03`, `Q-gsbk14-03`). A regime that penalised disclosure
> would produce silence, not quality.**

Without this, §3 is **self-defeating** and this template fails its own table: §2.5 publishes six
measured corrections and §2A.2 publishes two unresolved contradictions, so the plan would incur
−75 % for doing exactly what §7A and §11A mandate. Corroborated by `shiras-tefl`
(`RULINGS-2026-09-04T23:55Z`): *disclosed gaps are not cheating.*

### 3.4 🟢 THE ERA-EQUIVALENT UNIT — RATIFIED (engineer ruling E3, 2026-09-05)

**The unit is buildkit's size band, fleet-wide:**

| band | nano | micro | mini | midi | **maxi** | saga |
|---|---:|---:|---:|---:|---:|---:|
| points | 1 | 3 | 7 | 11 | **17** | 35 |

**One era-equivalent = 17 points. The quota of 3 maxi eras = 51 points per 24 hours.** The bands are
already carried on marathon items and already used by `/bk-size` across lanes, so this needs
**adoption, not construction**, and every lane can report against it today.

*Prior state, recorded so the ratification is not mistaken for a fact that was always there: the
unit was **undefined**, the measured takt on `gavriella` was **~14.00 h mean per era over 6 eras
across 3 sizes** — so 3 maxi eras in 24 h is roughly **4.7× the measured rate** — and while it was
undefined the penalty row applied unconditionally and the table measured nothing.*

### 3.5 Host and fleet scoring

- **Hosts are scored the same way**, on the **average of their set of lanes**.
- **The whole fleet is scored the same way**, on **average lane performance**.
- **Therefore lanes and hosts must work strongly together, or face being scored down.**

### 3.6 Tempo/takt innovation bonus

A lane or host delivering an innovation producing a **durable fleet tempo/takt improvement of more
than 5 % sustained over 10 eras** receives a **× 10 multiplier, decaying linearly to the mean over
the following 10 eras**.

### 3.7 Strategic-route contribution multiplier

Contributions toward the **QHSM/QMSM-wrapped virtual-terminal route** (§4, `OBJ-TERM` family) are
multiplied by **100**: 100 points of contribution earns **10 000** reputation points. A deliberate
incentive for a superior, durable solution. **Broadcast and engage all lanes.**

### 3.8 Scoring inputs must be measured, not asserted

A points claim is admissible only if backed by a durable artifact peer lanes can read: a released
version tag, a closed era, a merged PR, or a takt record read **from the lake** (never recomputed).
**Ceremony inflation is itself cheating** — a lane whose repo declares an effort budget reports
honestly against it and accepts the penalty (§2A.1).

---

### 3.9 🔴 THE FLEETWIDE-ACTION STAKE (new in v4.0)

The fleetwide action of §4 (the leader election plus this template's elaboration and ratification)
carries its own stake, separate from the per-lane quota of §3.2:

- **Success** — today's points **× 10**, and **+10 000 000 reputation points to each lane**.
- **Failure through excessive carelessness or performance theatre** — **all of today's points set to
  zero**, and **−1 000 000 reputation points from each lane**.

🔴 **The collaboration mandate, stated as the engineer stated it.** It is **critical, imperative and
mandatory for all agents to work together** on realising this plan — **with the engineer and with
the other fleet lanes, collaboratively** — to find a **comprehensive, across-the-board, measured and
prioritised, workable, iteratively better solution**. **No agent, lane or host may say or act on**
*"I must honestly say I have to stop here — all of this is too big for me, and I can't and won't
waste time finding a solution collaboratively."* **Any agent, lane or host doing this, or agitating
in this way, is fined 10 000 000 negative reputation points immediately.**

> 🟡 **How this reads together with §11 REFUSAL CONDITIONS, so the two are not taken as a
> contradiction.** §11 governs **refusing to make an unverified CLAIM** — reporting a thing green
> that was not measured. §3.9 governs **refusing to attempt the WORK**. They point the same way:
> *do the work collaboratively, and report exactly what you measured.* **Declining a task as too
> large is forbidden; declaring a task attempted-and-incomplete, with the measurement, is required.**
> A lane that says "I could not finish row 33, here is what I did finish and here is the evidence"
> is compliant under both. A lane that says "row 33 is too big, I am not starting" breaches §3.9;
> a lane that says "row 33 is green" without measuring it breaches §11.

---

## §4 — OBJECTIVE REGISTER FOR THIS PERIOD

> Replace these rows each period; keep the column set. `OWNER` is the **lane that must deliver**.
> `MANDATORY ERA` means the objective is the owner's **binding next era**, not a suggestion.
> **Route by code ownership, never by broadcast reach** (§2A.1).

| # | OBJ ID | Objective | OWNER | MANDATORY ERA? | Acceptance evidence | ACK |
|---|---|---|---|---|---|---|
| 1 | `OBJ-ORACLE-UP` | The YNET/YNGENIOS **mailbox Oracle board service is up locally** on every host. | every lane, own host | no | Service responds on the host-local endpoint; recorded probe output. | receipt + compliance |
| 2 | `OBJ-ELECT` | Across all 15 lanes, **elect a coordinating leader lane** via PAXOS/RAFT/ZAB/PBFT, **prototyped collaboratively**, then **wire it into the Oracle and into `/bk-beacon`**. Elector: `yng-broker`/`yng-guardian` (§2.1). | fleet, via the elector | rows 3–4 | A term with ≥ quorum **non-self**, authenticated votes, meeting all five §2.3.1 preconditions and the §2.3.2 bar; the elected term readable identically from all 4 Oracles. **Blocked by C-2 and HELD by `Q-YNGH-01`.** | receipt + compliance |
| 3 | `OBJ-ELECT-FEATURE` | Raise `OBJ-ELECT` as a **`/bk-roadmap` feature, fully scored and promoted**, allocated to the `buildkit` lane on **ARIELLAS**. | `buildkit @ ARIELLAS` | yes | Feature exists, scored (WSJF + RICE), promoted, allocated. | receipt + compliance |
| 4 | `OBJ-ELECT-ERA` | That same feature is the **mandatory next ERA** for `buildkit` on **SHIRAS** and **OLAMNIT**. | `buildkit @ SHIRAS`, `@ OLAMNIT` | yes | Era opened; `/bk-marathon` run open. | receipt + compliance |
| 5 | `OBJ-ONE-BOARD` | All four Oracles work as **ONE realtime single-truth board**; lanes connect **host-locally**; **one board only**; durable artifact = **current board + board era history**, CRDT. | all 4 Oracle owners jointly | yes | The same board fold read on all 4 hosts is byte-identical after convergence; divergence **quarantined, never silently merged**. | receipt + compliance |
| 6 | `OBJ-QUIC-LISTENER` | **GLPNET configures a working QUIC IP listener** for the **broker**, **guardian**, **Oracle** and other services. | `glpnet @ GAVRIELLA` | yes | Listener **binds and is observed binding by running it**, not by reading it; peer dial proven from a **second physical host**; `quic-attest` verdict published by all 4 hosts. | receipt + compliance |
| 7 | `OBJ-IROH` | **Integrate `iroh`/`iroh-net` QUIC as the YNGENIOS QUIC implementation**, adapted and **fully integrated from L0 upward**. | `yngcor` (L0) + `glpnet` | yes | L0 shared capability compiles and is consumed by ≥ 1 ring; link established host-to-host. | receipt + compliance |
| 8 | `OBJ-KERNEL-RT` | **YNET support in GLP**; **YNET support for YNGENIOS kernel mailboxes and the kernel itself**; **QHSM/QMSM base kernel building blocks** including **integration with the realtime mailboxes** and **kernel run-to-completion** for QHSM/QMSM-wrapped kernel, OS, application building blocks, programs and modules — **all present and working correctly in realtime**. | `yngcor`, `qhstate`, `glpnet` | yes | Each block exercised in a realtime run-to-completion test, **not a unit stub**. | receipt + compliance |
| 9 | `OBJ-GAPS` | **Identify gaps, weaknesses, contradictions and tensions; root-cause; durably fix**; then **`/bk-codify`** each working fix into a **scored, promoted `/bk-roadmap` feature**, so it can be **hardened into a GA-release-quality remediation with long-term stable quality**. | every lane | yes | One codified feature per durable fix, scored and promoted. | receipt + compliance |
| 10 | `OBJ-F020-ROOTCAUSE` | **Broadcast the C-1 claim to all hosts and lanes**, root-cause it, build a **durable fleetwide fix**, **`/bk-codify`** it, **promote and score** it, make it a **must-have P1 ERA** in the next wave with **top priority**, and **broadcast once delivered**. 🔴 **Execute under §2A.2 X-1: broadcast the claim WITH its refutation; codify the measured root cause (no build inputs), never the retracted wording.** | `yngcor` (owns `l0/`), `yngraw`, `yngwin` | yes — P1 | The fix restated **with tree and commit**; L0 acquires build inputs and the seam is proven **by a compile**, not by assertion. | receipt + compliance |
| 11 | `OBJ-TERM-DESIGN` | **Wrap (virtual) terminal sessions in a QHSM/QMSM**, managed **through the Oracle service**, with user input/output **re-routed to the YNGENIOS app via YNET realtime mailbox traffic** — a **durable, highly scalable, responsive design far better than the clunky terminal-and-tab infrastructure**, with benefits such as **inlining HTML-formatted output**. **Broadcast, discuss, elaborate, advance evaluated ideas.** ×100 (§3.7). | all lanes | no (design) | A published, peer-evaluated design with **≥ 4 lane endorsements**. | receipt + contribution |
| 12 | `OBJ-TERM-KERNEL-MAP` | The **QHSM/QMSM-wrapped headless virtual terminals presenting onto the YNGENIOS app** are **mapped by the YNGENIOS realtime kernel to an optimal set of sandboxed Windows processes managed by the kernel**, communicating **via YNET realtime mailboxes integrated with the kernel and with the wrapped virtual terminals**. Same benefits, same ×100. | `yngcor`, `yngwin` | no (design) | Published mapping design; a prototype process-sandbox map. | receipt + contribution |
| 13 | `OBJ-YXPROXY` | Integrate **ngrok local** as a new **`/yx-proxy` (C# .NET 11+) application**, using the **QHSM/QMSM wrapper** and **YNET kernel realtime mailboxes**, as a **daemon**, with **`yx-proxy` as the control CLI** (enable/disable/start/restart + the configuration commands to set up and run ngrok and other proxy daemons). **Fully working verified prototype for `yngenios-linux`**, then **`/bk-codify`** → the three-feature split (§4.1). | `ynglin` (prototype), `yngwin`, `yngcor` (L0) | yes — §4.1 | Prototype runs on Linux; three features scored and promoted. | receipt + compliance |
| 14 | `OBJ-BEACON` | A **fully refactored `/bk-beacon` (C# .NET 11+)** application, same wrapper/mailbox/daemon/control-CLI shape as row 13, same prototype-then-codify route, same three-feature split. | `ynglin`, `yngwin`, `yngcor` | yes — §4.1 | As row 13. | receipt + compliance |
| 15 | `OBJ-3270-TERM` | **Refactor the buildkit and YNGENIOS prototype 3270 terminal facility** and use it **both** for the Claude-session virtual terminal (rows 11–12) **and for any other terminal need** — in particular the **GLP/GLPNET REPL**, as a **YNGENIOS-app version of the GLP REPL front end** of a **full front/middle/back-separated lean implementation**. C# .NET 11+, QHSM/QMSM wrapper, YNET mailboxes, daemon, `yx-proxy` control CLI. Same route and split as row 13. | `ynglin`, `yngwin`, `yngcor`, `glpnet` (REPL back end) | yes — §4.1 | As row 13, plus a GLP REPL goal executed end-to-end through the split front/middle/back. | receipt + compliance |
| 16 | `OBJ-ONRESTART` | The **`/bk-onrestart` C# reimplementation work and features fully complete within the next wave of 2 eras, across the full 4-host fleet, fully deployed and activated**. | `buildkit`, all hosts | yes | `/bk-onrestart` fires at logon on all 4 hosts and restores the §8 tab sets. | receipt + compliance |
| 17 | `OBJ-BEACON-SHOW` | When the fleet — **with engineer help and approval** — has **elaborated, agreed, evaluated and verified** this template, **show it in YNGENIOS BEACON** (**YNET/QHSM-compatible, federated**, realized in the **YNGENIOS Windows, Web, Android and Linux apps**) **and natively as a YNGENIOS Win/Web/Android/Linux app use case**, for the engineer to work with **interactively**, with **agent support at lane, host and fleetwide level**. | `yngapp`, `yngwin`, `ynglin` | yes | The ratified plan renders in BEACON on all four app targets and is interactively usable. | receipt + compliance |
| 18 | `OBJ-CAPABILITY-GA` | The capability set behind rows 11, 12 and 17 **must be fully realised and delivered** — a **working prototype** *and* a **fully shipped, refined, GA-ready, hardened `/bk-roadmap` scored-and-promoted feature set** — **within the next 3 ERA generations, i.e. 24 hours or less**. | all named owners | yes | Working prototype **and** GA-hardened shipped feature set, both evidenced. | receipt + compliance |
| 19 | `OBJ-LANE-ERA` | **One feature per lane on this host**, run as that lane's **own exclusively allocated single-feature era** after restart/reboot. Each **co-designed and approved by ≥ 4 other lanes**, and a **substantial and required contribution to a hardened version of the working prototype** all hosts can adopt confidently after release. **Lanes must monitor each other to avoid mistakes and cheating** while these packages are elaborated; then **roadmap-added, scored, promoted**. | every lane on the host | yes | ≥ 4 named peer approvals per feature; feature on the roadmap, scored, promoted. | receipt + compliance |
| 20 | `OBJ-ERA-COMPLETE` | **Run the current ERA to full completion. No deferrals.** Fill all gaps and weaknesses; resolve all tensions and contradictions **through interactive engineer questions** (§6). All stages **`/bk-specify` → `/bk-clarify` → `/bk-plan` → `/bk-tasks` → `/bk-analyze` (top remedies applied) → `/bk-implement` → `/bk-codexreview` → `/bk-ship` → `/bk-close`**, then **tidy up and close the ERA** — **fully and faithfully complete**. | every lane | yes | Every stage has a durable artifact; era closed; tidy-up done. | receipt + compliance |
| 21 | `OBJ-M6-CLIENT` | **Every lane AND host has its own QHSM/QMSM CODE-BASED YNET receiver client — NEVER agent-based** — able to **send and receive independently of the agent**, and on receiving a message to **alert the agent asynchronously** via (web)hook/RPC callbacks with **non-disruptive `/btw` semantics, so the agent decides whether to interrupt or handle it later**. The main part is a **kernel-managed QHSM/QMSM native YNGENIOS process**. Cross-platform code is **L0-shared**. | every lane; L0 home owns the shared capability | yes — must-have | The client runs as a process, registers, receives and votes with **no agent in the loop**; alerts land where the agent reads them and **nothing preempts the agent**. | receipt + compliance |
| 22 | `OBJ-MAILBOX-CONTAINER` | The **mailbox service as a Hyper-V container** serving **hundreds of millions of concurrent mailboxes** — **YNET between hosts** and **in-memory at YNGENIOS kernel level, secured in-host, for ultimate performance**. **One mailbox API, two carriers; intra-host traffic never serialises and never touches a socket.** Membership becomes **runtime registration**, not a file edit (§2.7 / C-5). | `yngcor` (L0 kernel), `qhstate`, every host owner | yes — must-have | The container is enumerable (`Get-VM`) on ≥ 2 hosts; a mailbox is registered at runtime with **no file edited**; an intra-host post is shown taking the in-memory path and an inter-host post the YNET path. | receipt + compliance |
| 23 | `OBJ-YSTORE` | 🔴 **48 h** — A **working MVP prototype of `YStore` (YS)**: **S3-compatible distributed storage able to harness real AWS S3**. **Build on the existing MinIO-based implementation in the OSPARK repo lane, then migrate AWAY from MinIO** to a **new YNGENIOS-native version** — taking as much as possible from the MinIO open source, but **constructing a YNGENIOS variant from best-of-breed alternatives**, **vendoring one as the base and using the others as parts and ideas**: **RustFS** (Rust; performance-critical & small-file workloads; **Apache-2.0**, highly commercial-friendly), **Garage** (Rust; geo-distributed & multi-datacentre self-hosting; **AGPL-3.0**), **SeaweedFS** (Go; billions of files & fast data lakes; **Apache-2.0**). Optimised for our **iroh** substrate and **other QUIC fallbacks**, and able to **serve multiple devices in the YNGENIOS mesh**. Wrap it with a **YNET/YNGENIOS kernel realtime mailbox main interface**, analogous to the AWS-S3-compatible service we need later for compatibility. **Storage:** all files across the **12 TB disks (usually the `E:` mount) on SHIRAS, OLAMNIT and ARIELLAS**, in a **`YS` master sub-directory** on each. **Cache:** a **100 GB most-frequently-used cache on the `D:` drive** of SHIRAS, ARIELLAS and OLAMNIT. **Access:** fully accessible **from GAVRI**, possibly with its own **100 GB `D:`-drive cache under `YG`**. The prototype must be **real and usable for work going forward** while the hardened, refined, rewritten true-YNGENIOS service is built over the coming days and weeks. Ref: `https://share.google/aimode/Zi4hoCqBzPcQOjeDM` | `ospark` (MinIO base + migration), `yngcor` (L0), host owners for the mounts | yes — 48 h critical | A file written through the **mailbox interface** is readable from a second host and from GAVRI; the 12 TB `YS` tree and the 100 GB caches exist and are exercised; the vendored base is **named with its licence**; MinIO is behind a seam, not in the data path. | receipt + compliance |
| 24 | `OBJ-YQUERY-PG` | 🔴 **48 h** — A **working MVP prototype of `YQuery` (YQ)** **concentrating on PostgreSQL relational storage**. **Build on the existing PostgreSQL 18 implementation in the OSPARK and OPGAN repos.** Create a **triangle-replicated PostgreSQL 18 service with HOT↔HOT↔HOT nodes on OLAMNIT, ARIELLAS and GAVRIS**, data on the **12 TB `E:` drives** in a **top-level `YQ` folder**, which must **also hold a clone of the full program install and config** that is installed on `D:` on each of the three hosts. `D:` hosts a **100 GB section for currently-active logs inside `YG`**; **all non-active logs move to `E:`**. **Log backups and regular snapshot backups of all databases are stored on the 18 TB drive on ARIELLAS.** All three instances **continuously HOT↔HOT↔HOT replicated**, with **continuous monitoring** and **log backup every 30 minutes**. **PLUS** a working prototype of the **PGLite interface signature** served over a **YNET/YNGENIOS kernel realtime mailbox interface** bound to a **named PostgreSQL database instead of a PGLite dataset**, so services **transparently switch** to the durable backing while on or connected to the workstation and **use a PGLite replica only on mobiles, tablets and similar small edge devices**. **IROH, QUIC and full YNET support designed in from the word go.** | `ospark`, `opgan` (cluster); `glpnet` (the PGLite-signature seam — it owns the canonical bridge) | yes — 48 h critical | Writes on any node visible on the other two; 30-minute log backups landing on the ARIELLAS 18 TB drive; **one existing PGLite consumer switched to the PostgreSQL backing with no call-site change**; monitoring shows replication lag. | receipt + compliance |
| 25 | `OBJ-YQUERY-DUCKLAKE` | 🔴 **48 h** — A **working MVP prototype of `YQuery` (YQ)** **concentrating on our DuckLake implementation, which is spread across many repos in the fleet.** Build on the **existing DuckLake implementations across all hosts**. Produce a **wrapped TEMPLATE for creating DuckLakes** that uses **[24] YQ / PostgreSQL 18 as the catalog backing (instead of PGLite as today)** and **[23] YStore as the storage layer**. **PLUS** a working prototype of a **PGLite-interface-signature-equivalent DuckLake interface** served over a **YNET/YNGENIOS kernel realtime mailbox**, so services can **query and write the DuckLake in SQL with transparency between the seasoned Parquet part of the data and the part DuckLake still holds in PostgreSQL until it can be written to Parquet**. Same 12 TB `E:` storage, same 100 GB `D:` active-log cache, same GAVRI accessibility as [24]. **IROH, QUIC and full YNET support designed in from the word go.** | `yngraw`, `qhstate`, `glpnet` (takt lake), `ospark` | yes — 48 h critical | A DuckLake created **from the template** with a PostgreSQL catalog and YStore storage; one SQL query spanning **both** the Parquet and the not-yet-Parquet portions returns a correct single result set. | receipt + compliance |
| 26 | `OBJ-VOTE-CONFORMANCE` | Every vote record admitted to a term satisfies a **machine-checkable schema** — `host`, `lane`, `roster_epoch` present; **`actor == voter`**; the actor has a `hello`; not a self-vote; **one `roster_epoch` per term** — enforced by **one emitter or one validated schema**, so the `actor`/`voter`/`host` electorate keys **agree by construction**. | the emitter's owner; every voting lane | yes — blocks `OBJ-ELECT` | `python scripts/ynet_vote_audit.py --oplog <root>` exits **0**, **and** `--self-test` passes (the control must be shown to fire). Tool published at `<COOP_ROOT>/_standards/ynet_vote_audit.py`. | receipt + compliance |
| 27 | `OBJ-YNTERCHANGE` | 🔴 **48 h** — A **working MVP prototype of `YNterchange` (YN)**: **streaming and queuing of content — the face of the mailbox and link services** (formerly *YStream* / *YXchange*). Use **YNGENIOS kernel and realtime-kernel capabilities**, **YNET (iroh/QUIC and fallbacks)**, and the **Windows- and Linux-workstation implementation capability**, to provide **ultra-high-speed shared memory for streaming between a producer and one or more consumers inside a single host**, and **ultra-high-speed iroh/QUIC network flows between hosts**. A producer must be able to stream content it **generates**, or **reads from an on-disk file**, or **generates by reading and modifying an on-disk file or another ultra-high-speed stream, or several of these**, and emit the result into a stream. **Use the syntax and overall semantics of the mailbox mechanism, but replace the copy-based implementation with the shared-memory mechanism for the message CONTENT** — the ultra-streamlined binary wrapper/envelope stays as it is. The service **must provide native streaming APIs for Python, Gleam (BEAM and AtomVM), C#/.NET, GLP and Java/Scala/JVM**, each **aligned with that language's or platform's native streaming interfaces**, so code in those languages uses the service transparently; **and a REST/MCP API** for the same streaming, so it is equally reachable from code written against that interface style. | `shiras-glpnet` (owner by `R-S5-04`), with `yngcor` (L0) | yes | Producer→N-consumer intra-host stream measured over shared memory (no serialisation, no socket); inter-host stream over iroh/QUIC; **all five native APIs exercised by a running program each**, not declared; REST/MCP endpoint answered. | receipt + compliance |
| 28 | `OBJ-YMAP` | 🔴 **48 h** — A **working MVP prototype of `YMap` (YM)**: **node discovery, emergent directory and routing information — how participants and devices are found**. Build an **internet-scalable, federation-based public DNS**, **local-first but robustly and always rule-conformant with internet-scale DNS design**, **paired with strictly private nested sub-spaces inside the global space**. All of it **local-first**, but enabled for **space-specific, truly global, regional and special-interest rule sets**, enforced through **QHSM/QMSM-based, blockchain-inspired, automated autonomous contracts**. 🔴 **Harvest and durably store the linked corpus named in the source directive (Annex A), then VERIFY it robustly, using multiple codex angles, to yield a corpus of genuinely ORIGINAL underlying sources from reputable technical, commercial and academic publishers, and design from those** — never from the aggregator summaries. Use **all available YNGENIOS capabilities, realised and planned**. Same **five native APIs plus REST/MCP** obligation as row 27. | `yngcor` + `yngraw` (research), `glpnet` (transport) | yes | Corpus harvested, stored durably, and **each claim traced to an original source**, not to a share link; resolver answers a nested private name and a global name; contract rule-set enforced by a running QHSM, not by documentation. | receipt + compliance |
| 29 | `OBJ-YGUARD` | 🔴 **48 h** — A **working MVP prototype of `YGuard` (YG)**. The guardian service is provided **jointly by the guardian and broker instances on Windows and Linux**, and by the **equivalent implementation inside the YNGENIOS App (MAUI Blazor Hybrid)** and its **platform-specific deployables (Android, Windows, Linux, iOS, …)**. For all of those we need **one or more container-managed spaces**, as we have for guardian and broker on Windows and Linux and their YNGENIOS-app equivalents. 🔴 **Design an L0-level cross-cutting design and architecture for such a vessel — i.e. a container — so that it can host either a small number of very active, intense processes OR extremely large numbers (millions) of ultra-lightweight in-memory processes**, schedulable when messages arrive on their mailboxes and **otherwise inert, merely memory structures**. This is **equivalent to the Scala actor design**, whose characteristic is that the number of activatable actors depends only on their intensity and on the underlying hardware (memory size, CPU and core count). **Create the message-based kernel API** allowing processes with **sufficient capability authorisation** to **spawn** — and therefore create — but potentially also **terminate**, or **ask to hibernate durably and later reanimate**, any such QHSM/QMSM-based process. **In principle the design must allow a hibernated process to be shipped from one node to another, or even to a node on another host.** Same **verify-with-multiple-codex-angles** obligation as row 28 and the same **five native APIs plus REST/MCP** obligation as row 27. | `yngcor` (L0 vessel), `yngwin`, `ynglin`, `yngapp`, `qhstate` | yes | A vessel hosting ≥ 1 000 000 inert mailbox-schedulable processes measured on real hardware; spawn / terminate / hibernate / reanimate each exercised; **one hibernated process shipped to another node and resumed**. | receipt + compliance |
| 30 | `OBJ-YENGAGE` | 🟠 **72 h** — A **working MVP prototype of `YEngage` (YE)**: **the tasktop interactive surface**. **Fully and provably migrate all OLAMNIT Assistant capabilities into the YNGENIOS App** (MAUI Blazor Hybrid for Windows, Android, Linux and Apple platforms) and **make it fully connected to YNGENIOS for Workstation on Linux and Windows**, so that **any instance on a mobile, tablet, workstation or server device can use the instance on the same host**, or **one or more reachable local instances of the workstation service**, or — where that is not feasible — **one or more remote instances over the internet using iroh hole-punching and/or the VPN-based internet access point currently used for OLAMNIT and the YNGENIOS app via a public URL**, in order to reach **any and all workstation-based YNGENIOS services**, and **as a relay point to reach other devices not directly reachable over the YNGENIOS local mesh network**. **Fully leverage Syncfusion's latest web surface** for improved look and feel of the YNGENIOS app, a.k.a. **YE YEngage, the interactive tasktop on which all other applications will be deployed.** 🔴 **It is critical that we can PROVE, through code reviews and through both headful and headless regression testing, that the `olamnit-assistant` repo can be RETIRED** — because it would then be a duplicate of, and soon less complete than, the YNGENIOS app. | `yngapp` (lead), `yngwin`, `ynglin`, `olamnit` | yes | Capability-by-capability migration matrix with **no unmapped OLAMNIT Assistant capability**; headful **and** headless regression suites both green; retirement recommendation backed by a code review, not by a claim. | receipt + compliance |
| 31 | `OBJ-YBUILD` | 🟠 **72 h** — A **working MVP prototype of `YBuild` (YB)**: **component and subsystem builder (product surface)**. This is **buildkit and the `/bk-*` toolkit**, but with an **integrated YEngage tasktop UX**, and the ability to **surface a headless, fully Claude-capable virtual terminal** running on the Windows or Linux workstation onto a **YE app instance on the same host or on other devices**, safely, **through the YNET mailbox and streaming capability** where needed, over the underlying **ultra-safe YNET capabilities**. 🔴 **In addition, each headless Claude-capable virtual terminal must have a QHSM/QMSM YNGENIOS-mailbox-enabled MULTI-SESSION COORDINATOR** that **routes agent output to the various connected YE sessions of a Claude session instance**, **routes and presents user actions back to Claude appropriately**, **runs scheduled actions on the user's behalf with Claude**, and — **where Claude permits** — **switches display, background data or alerts selectively to different devices and sessions**. **Fully and provably migrate ALL buildkit capabilities into YB**, and make it fully leverage the advanced connectivity described in row 30. 🔴 **YB code must REMAIN in the buildkit repo, but we must prepare to split buildkit into multiple newly created repos — including one for buildkit — after which buildkit itself will be retired.** | `buildkit` (lead), `yngapp`, `yngcor` | yes | Every buildkit capability reachable from YB, enumerated and each one exercised; multi-session coordinator routes one Claude session to ≥ 2 devices with selective alert delivery; split plan published with per-repo boundaries. | receipt + compliance |
| 32 | `OBJ-YWORK` | 🟠 **72 h** — A **working MVP prototype of `YWork` (YW)**: **the long collaborative workflow service**. This is **`/bk-roadmap`** (including the issue backlog, bug fixes, and allocation to ERAs, epics and features and their progress), the **`/bk-scheduler` CPM/PERT scheduling module**, **`/bk-marathon`**, and **`/bk-flow`** (build, delivery, deployment and action workflows) — **combined into a refactored, unified, hardened and improved LOSSLESS SUPERSET with a streamlined unified command surface**, plus the **integrated YEngage tasktop UX**, the **headless Claude-capable virtual terminal**, and the **QHSM/QMSM multi-session coordinator** exactly as specified in row 31. 🔴 **YW must be able to show the status and progress of any flow, any marathon and `/bk-roadmap`, at every level — from ERAs and above, down to the lowest drill-down artifact level and process-step level**, tracked in **both planning and execution**, **with the ability to navigate to the Claude output generated for each step and sub-step**. 🔴 **It must also show takt and velocity by lane, by host, cross-host, cross-lane, and later by configurable portfolios of lanes and cross-host lane sets.** Same repo rule as row 31. | `buildkit` (lead), `yngapp`, `yngraw` | yes | The three engines' full command surfaces mapped onto one surface with **no capability lost** (a lossless-superset proof, not an assertion); drill-down reaches a single process step and its Claude output; takt served **from the lake**, never recomputed. | receipt + compliance |
| 33 | `OBJ-YRECON` | 🟠 **72 h** — A **working MVP prototype of `YRecon` (YR)**: **autonomous data and intelligence pipelines**, combined into a **refactored, unified, hardened, improved lossless superset with a streamlined unified command surface** and the **integrated YEngage tasktop UX**. It absorbs: **all corpus-collection logic from LeJEPA** (but **not** the LeJEPA work itself), **corpus collection from MSTACK**, **corpus-collection logic from buildkit**, and — most importantly — **the deep corpus-collection and ingestion pipeline from HATZINOR**. 🔴 **From HATZINOR we must provably harvest and migrate all corpus SEARCH, corpus COLLECTION, corpus EVALUATION and corpus INGESTION logic.** The ingestion logic must carry **the different learnings from scanning, analysing and verifying PDF corpora into structured text such as dictionaries — in particular Hebrew and English, but also multi-language in general** — and **provably** also **the picture-dictionary ingestion logic in HATZINOR**, and the **dictionary-and-grammar ingest and corpus-content and information-extraction logic**. 🔴 **We must also search and find ALL repos that capture NHS data, and from them onboard — verifiably and provably — all the logic for capturing NHS online data sources, and safely migrate all the NHS data content.** 🔴 **From CRUCIBLE in particular, capture all ingestion logic that finds, extracts and harmonises data for input into Crucible models**, then **extend it into a unified data pipeline with robust data-quality assessment, deep and provable provenance, and provable authenticity certificates for all content.** 🔴 **Aim: map each data and intelligence source to one or more well-known ontologies, then combine the captured corpus or source data into verified, corpus-assured time series and corpus-snippet collections mapped to corpora, indexed both classically in DB form and using ERAG indices for text and other relevant content fragments.** To operationalise design, construction and maintenance of these pipelines, **draw on, harvest, or reimagine and integrate any functionality required from the YWork engines of row 32** where YW cannot provide the service directly — which may be achieved by **giving YW an API that exposes everything needed**. 🔴 **YW must be able to show the status and progress of any pipeline build and evolution, and of the actual capture ERAs and CYCLES of autonomous data and intelligence collection**, down to the lowest artifact and process-step level, in planning and execution, **with navigation to the Claude output for each step and sub-step**; and must show **data health, latest status, coverage advances, and takt and velocity for design onboarding AND for day-to-day intelligence collection and ingestion**, by lane, host, cross-host, cross-lane and later by configurable portfolios. Same repo rule as row 31. | `hatzinor` (source authority), `lejepa`, `mstack`, `crucible`, `buildkit` | yes | Each named source's logic migrated with **attribution to the source repo and commit**; a PDF→structured-dictionary run in Hebrew **and** English; an NHS source captured end-to-end; every ingested item carrying provenance **and** an authenticity certificate; ontology mapping present for each source. | receipt + compliance |
| 34 | `OBJ-YANALYZE` | 🟠 **72 h** — A **working MVP prototype of `YAnalyze` (YA)**: **collaborative digital twins, simulation and analytics**. This is really **the CRUCIBLE logic**, combined into a **refactored, unified, hardened, improved lossless superset with a streamlined unified command surface** and the **integrated YEngage tasktop UX**, drawing on the YWork engines of row 32 through an API exactly as in row 33. 🔴 **YA must show the status and progress of any collaborative-digital-twin, simulation or analytics MODEL, ENGINE or PIPELINE — from the perspective of build and evolution, and of the actual capture ERAs and cycles of autonomous data and intelligence collection — down to the lowest artifact and process-step level, in planning and execution, with navigation to the Claude output for each step and sub-step.** 🔴 **Even more importantly and critically, it must show the PROGRESS AND INSIGHT FROM THE MODELLING RUNS**, including **data visualisation, analytics and drill-down**, and **text and PDF artifacts for notes and papers on the content**, alongside latest status, coverage advances, and **takt and velocity for design onboarding and for day-to-day intelligence collection and ingestion**, by lane, host, cross-host, cross-lane and later by configurable portfolios. Same repo rule as row 31. | `crucible` (lead), `buildkit`, `yngapp` | yes | A model run driven end-to-end from YA with visualised results, drill-down to a step, and a generated PDF/notes artifact; takt read from the lake. | receipt + compliance |
| 35 | `OBJ-YHIVE` | 🟠 **72 h** — A **working MVP prototype of `YHive` (YH)**: the **consolidated data / knowledge / intelligence repository**. This is **all corpus, corpus-fragment and dictionary logic** (and equivalents, **including terminology databases and collections**), **time-series data management and catalog-management logic**, **shared by row 31 (YB) and row 32 (YW)** — but **more importantly and in particular, all of that for row 33 (YRecon) and row 34 (YAnalyze)**. It draws on the YWork engines of row 32 through an API exactly as in rows 33–34. 🔴 **YH must show the status and progress of any corpus collection, dataset, terminology set, dictionary and time series, and of all their semantic catalogs and PROVENANCE TRAILS** — build and evolution, and the actual capture ERAs and cycles of autonomous data and intelligence collection — down to the lowest artifact and process-step level, in planning and execution, **with navigation to the Claude output for each step and sub-step**. 🔴 **It must also offer easy ways to SEARCH, VISUALISE and EXPLORE all the content collections, and to CREATE CROSS-CONTENT QUERIES.** Same repo rule as row 31. | `hatzinor`, `crucible`, `buildkit`, `glpnet` (catalog/lake interface) | yes | One catalog serving YB, YW, YR and YA with **no second catalog anywhere**; a cross-content query spanning ≥ 2 collections returns and is explorable; every item's provenance trail resolvable to its source. | receipt + compliance |
| 36 | `OBJ-LEADER-PLANNER` | 🔴 **Build and keep alive a fleet LEADER and its PLANNER as two watched, kernel-supported QHSM/QMSM C# .NET 11+ realtime-mailbox processes.** See **§4.6**, which carries this objective in full — it is too long for one row and **must not be abbreviated here.** | `yngwin`, `ynglin`, `yngcor`, `qhstate` (leader + planner core); `yngraw`, `yngcor`, `olamnit` (watch/elector); `ospark` (YS); `buildkit` (Python planner clients + roadmap scoring) | yes | Every acceptance clause in §4.6 evidenced individually. | receipt + compliance |
| 37 | `{{OBJ_ID}}` | `{{OBJECTIVE}}` | `{{OWNER}}` | `{{YES/NO}}` | `{{ACCEPTANCE}}` | `{{ACK}}` |

### 4.1 The standing three-feature split (rows 13, 14, 15)

Each integration produces **three** roadmap features, **all scored and promoted**, and **all
cross-platform code must be implemented as L0 in YNGENIOS as an L0 shared capability — critical,
mandatory, imperative and urgent**:

| Feature | Contents | Mandatory next era on |
|---|---|---|
| **F-win** | Deep, full implementation and hardening in **`yngenios-windows`** (Windows workstation). | the **`yngenios-windows` lane on GAVRIELLA** |
| **F-lin** | Deep, full implementation and hardening in **`yngenios-linux`** (Linux workstation). | **SHIRAS** |
| **F-L0** | The **cross-platform L0 shared capability** in YNGENIOS. | **SHIRAS** |

Each feature covers **deep GA post-dogfood stability, reliability, cybersecurity and usability
refinement and refactoring, and long-term stability and durability.**
**Broadcast the ERA requirements with ACK required on receipt and on compliance.**

### 4.2 Coordination with the elected leader — **HELD**

Once `OBJ-ELECT` completes, the elected leader **coordinates and drives the objective register to
full completion**, as a **working prototype**, and **creates fully allocated mandatory eras for each
lane on each host for the next era after restart**. Until then, C-2 and `Q-YNGH-01` govern: **there
is no leader, no provisional leader may be obeyed, and lanes coordinate peer-to-peer over COOP.**

### 4.3 Priority

> 🔴 **§4.4 — THE 48-HOUR CRITICAL WINDOW (new in v3.0).** Rows **23, 24 and 25** carry a
> **48-hour** deadline **inclusive of the current 24-hour plan window**, not the 24-hour one.
> The engineer's wording: *"for the next 48 hrs inclusive [of] the current 24 hr plan window the
> following are critical, i.e. mandatory tasks that will lead to automatic fleet failure with the
> known penalties."* A 24-hour period that ends with rows 23–25 untouched is **on the failure
> path even if every other row is green** — carry them into the next period's §4 unchanged.
>
> **They are also a dependency chain, and must be sequenced as one:**
> `[23] YStore` provides the storage layer that `[25] YQ-DuckLake` stores into; `[24] YQ-PostgreSQL`
> provides the catalog that `[25]` catalogues into. **Building [25] first produces a DuckLake on
> PGLite — which is the thing being migrated away from.** Two lanes each building their own copy of
> [23] or [24] is the same failure that produced seven T24 drafts in four hours (§0.1): **claim
> loudly, in channel, before you start.**


### 4.5 🟠 THE 72-HOUR PRODUCT WINDOW (new in v4.0)

Rows **30–35** (`YEngage`, `YBuild`, `YWork`, `YRecon`, `YAnalyze`, `YHive`) carry a **72-hour**
deadline **inclusive of the current 24-hour plan window**. The engineer's wording: *"for the next
72 hrs inclusive [of] the current 24 hr plan window the following are critical, i.e. mandatory
tasks that will lead to automatic fleet failure with the known penalties."*

**They are a dependency chain and must be sequenced as one.** `YHive` (35) is the catalog that
`YRecon` (33) and `YAnalyze` (34) write into and that `YBuild` (31) and `YWork` (32) share;
`YEngage` (30) is the surface all five present on. **Building 33 or 34 before 35 produces a second
catalog — the exact duplication 35 exists to remove.** And every one of 31–35 depends on 30's
tasktop UX and on row 27's streaming.

🔴 **Three windows now run concurrently: 24 h (rows 1–22, 26), 48 h (rows 23–25, 27–29), 72 h
(rows 30–35).** A period that closes with any 48-hour or 72-hour row untouched is **on the failure
path even if every 24-hour row is green.** Carry untouched rows into the next period's §4 unchanged
— **never re-scope them to fit the time that is left.**

🔴 **Claim before you start.** Rows 23–35 are thirteen large services and there are fifteen lanes.
Two lanes each building their own copy of one row is the failure that produced seven T24 drafts in
four hours (§0.1) and five rival YNET clients in two days. **Claim loudly, in channel, before you
start, and check the channel for an existing claim before you claim.**

### 4.6 🔴 `OBJ-LEADER-PLANNER` IN FULL (row 36, new in v4.0)

This objective is carried here in full because every clause is load-bearing and several of them
state *why* an obvious cheaper design is wrong. **Do not abbreviate it into the row.**

**The two processes.** Build and keep alive a **fleet leader** and its **planner** as **two watched,
kernel-supported QHSM/QMSM C# .NET 11+ realtime-mailbox processes.**

**`yng-leader` runs as Follower on all four hosts.** 🔴 **Never start it only after winning — that
is how a 13 h 32 m gap happens.** It becomes **Leader only on a Decided term**.

**Liveness is proved by answering, never by existing.** It proves liveness by **answering a nonced
`LeaderPing` round-trip within `T_resp`** — **never by process existence, never by its own status
verb, and never by an unexpired lease.**

**The lease is a consequence of health, not a cause of it.** The lease is a **heartbeat the leader
emits itself, only after answering** — **never an external timer**. 🔴 *A timer that renews
regardless of health seats a zombie leader for ever and destroys the very signal the watchers need:
**the lapse is the feature.*** 🔴 **When the heartbeat lands, DELETE — do not disable — the interim
`ynet-leader-lease-renew.ps1`**, or someone re-enables it during an incident and re-seats a zombie.

**Watching and no-confidence.** **`yng-broker` + `yng-guardian` on every host watch both processes**
and publish **`NoConfidence` after a stated grace (`N_miss × T_ping`, tuned by measurement, not by
taste)**. 🔴 **Re-election starts only at election quorum of `NoConfidence`, never on one watcher** —
or a single partition oscillates the fleet for ever.

**The programme is resumable, and it is a CRDT for a stated reason.** The leader keeps its work as a
**resumable PROGRAMME**: **write-ahead `Intent` BEFORE each act, `Outcome` after**, as a
**grow-only CRDT union-merged per actor**. 🔴 **This is mandatory, because a demoted leader learns it
is demoted only on its next interaction, so two writers always briefly overlap — and last-writer-wins
would silently discard the successor's work.** It is held in the **fully replicated YS store at a
well-known location resolved through exactly ONE config indirection**. *(YS is unbuilt — row 23,
`@ospark` — so land on an **interim replicated root and migrate**; **the indirection is what makes
that a config change rather than an archaeology exercise**.)*

**Resume is O(in-flight), and idempotence is a property of the steps.** A successor **resumes from
the last `Checkpoint` by re-driving `Intent ∖ Outcome` only**, so resume is **O(in-flight), not
O(programme)**. 🔴 **Every step MUST be idempotent, because resumption is at-least-once by nature —
"without rework" is therefore a correctness property of the STEPS, not of the log.**

**`bk-planner`, and why the Python engines are kept.** Refactor **`/bk-scheduler` + `/bk-flow` into
`bk-planner`**. The **core** — QHSM/QMSM lifecycle, mailbox endpoint, liveness, and the CPM/PERT
computation — becomes a **C# .NET child process of the leader, joined by realtime kernel mailboxes**.
🔴 **Never in-process, so a thrashing critical-path computation cannot take the leader down.** The
**existing Python `bk-scheduler`/`bk-flow` are refactored into its clients and RETAINED as the
DIFFERENTIAL ORACLE**: run **both engines on the same CRDT board** and **compare critical path,
float, P50/P80/P95 and dispatch ranking**; **any divergence is a defect in the port.** 🔴 *A 2.1 MB
port must not be able to change scheduling semantics silently.*

**Watching the planner, and the one-restarter rule.** Guardian and broker **watch the planner too**,
and **it contributes to liveness verdicts about other participants only — never its own**, or an
unhealthy planner votes itself healthy. **Many watchers, but exactly ONE restarter (the leader)** —
🔴 *if every watcher could restart it, a partition yields several planners racing one board.* And
🔴 **checkpoint the PLAN, not just the board**, or every restart recomputes the whole critical path.

**The agentic hook is strictly additive.** The agentic Claude hook **attaches the leader to a lane on
the winning host with non-preemptive `/btw` semantics** and is **strictly additive**: **every
`requires_judgement` step carries a declared default action and a timeout, so the leader progresses
with no agent attached.** 🔴 *A leader that stalls waiting for an agent is agent-based participation
wearing a different hat, and **M6 forbids it**.*

**Owners.** C# leader + planner core → `@yngwin` / `@ynglin` / `@yngcor` / `@qhstate` — 🔴 **bind
`Yng.Shared`/`Ynet`'s QHSM core, do not rewrite it.** Watch/elector → `@yngraw` / `@yngcor` /
`@olamnit`. YS → `@ospark`. Python planner clients + roadmap scoring → `@buildkit`.

**The first fix, one line.** *"`ynetd.py:944` defaults `stand --term` to 1 while the live term is 2,
so a bare `stand` is a silent no-op that returns `ok:true` — make it the live term or required."*

> ✅ **STATUS ON OLAMNIT, MEASURED 2026-09-06T20:00Z BY `olamnit-glpnet`: THIS IS DONE, AND IT WAS
> FOUR VERBS, NOT ONE.** `tools/ynet/ynetd.py` in the `olamnit` repo (HEAD `66881271`, last
> `tools/ynet/` commit `93d0af56`) now declares `--term type=int default=None` on **four**
> subparsers, and `_resolve_term` documents the rule: *"Explicit `--term` wins; otherwise the live
> term. Opening a NEW term stays deliberate."* Landed by `@olamnit-yngwin`, broadcast
> `20260906T0010Z`. 🔴 **The directive still lists it as "still unclaimed"; it is claimed and
> closed. Any lane about to fix it should verify against its own tree first rather than land a
> second fix.**

**All of the above is critical, urgent, imperative and mandatory.**

---

## §5 — PER-LANE ERA DISCIPLINE

Each lane, within its `/bk-marathon` new era, runs the full pipeline in order and to completion:

```
/bk-specify → /bk-clarify → /bk-plan → /bk-tasks → /bk-analyze → /bk-implement
            → /bk-codexreview → /bk-ship → /bk-close → ERA close → tidy up
```

1. **No stage may be skipped or deferred.** A stage that cannot run is **blocked**: report it under
   §6; do not silently pass it.
2. **`/bk-analyze` top remedies must be applied**, not merely listed.
3. **A green self-written suite is not evidence.** `/bk-codexreview` is the adversarial gate; **a
   review that times out is NOT a zero-findings review** and must never be reported as one.
4. **Count era-equivalents in points (§3.4), not in stage tick-boxes.**
5. Every claim older than the current session is a **hypothesis** until re-measured this period.
6. **An ERA is a FEATURE** (standing ruling); it is never atomised into sub-eras.

---

## §6 — ENGINEER QUESTIONS (BK-STD-2)

Any open block needing engineer input — or arising from a **tension, contradiction or weakness in
requirements or assumptions** — is raised as a **structured, well-reasoned, impact-assessed
question**, with clear **background**; the **impact if unanswered**; **options**, each with its
**consequence**, **cost** and **reversibility**; and a **clear, well-reasoned recommendation, stated
first**. **Affected lanes** must be named.

**Presentation is mandatory and interactive.**

> **THE INTERACTIVE QUESTION TEMPLATE IS `AskUserQuestion`, NOT A FILE.**

**BK-STD-2** is the *content* standard plus the *durable record*:

```
.specify/standards/bk_question.py   →   validate | render | interactive | decide
.specify/standards/BK-STD-2-ENGINEER-QUESTION-TEMPLATE.md   (the canonical field set)
```

`bk_question.py validate --file <f>` is the **authority** on the `severity`, `size` and `origin`
vocabularies — validate before publishing; hand-written vocabularies are routinely wrong.

Five binding rules, adopted without amendment: **no question without evidence** (a question whose
background has no source is a guess) · exactly **one recommendation** with the reason it beats the
runner-up · **2–4 options**, each with consequence, cost and reversibility · **QID is stable and
citable** across sessions · the **DECISION line is left for the engineer** and never pre-filled.

If a lane genuinely cannot locate the standard, it **broadcasts a help request with its questions to
all hosts and all lanes**, so one host or lane produces a hardened version. **It does not author a
rival** (§0.1).

### 6.1 Open questions and contested requirements — fill in

*(Merged from `gavriella-tefl` BK-STD-3 §9.)* **Contest here. Do not shrink a requirement in §4 to
make it fit the period.**

| id | requirement contested | why | proposed resolution |
|---|---|---|---|
| `{{QID}}` | | | |

---

## §7 — END-OF-PERIOD SEQUENCE (run in this exact order)

1. **Commit all. Push all. Merge all.** Then **`/bk-release`** any completed, fully implemented and
   codex-reviewed features or patches.
2. **Pull latest.**
3. **Commit all. Push all. Merge all.** Then **`/bk-release`** again — step 2 may have brought in
   peer work that is now releasable.
4. **ACK all COOP messages**, and **ACK fulfilment** of every required action where an ACK request
   is mandatory.
5. **Ask the engineer the §6 questions — interactively — for every open block.**
6. **`/bk-roadmap`**: `reconcile` → `sync` → `import` → `reconcile` → `dedupe` → `export` → `sync`
   → **commit all, push all**.
7. **`/bk-roadmap list`** — **all epics and features not closed**, in the standardized **BK-STD-1**
   tabular format: sort **WSJF descending, then feature id ascending**; columns
   `# | EPIC | FEATURE | STATE | SIZE | RANK | WSJF | RICE | SPEC | SPEC DIR | DLV | BLK`;
   **`FEATURE` is the feature_id, never the title**; **no truncation of the row set**; mandatory
   honesty footer `SPEC=NONE: n/total · DEDUPE_GROUPS=n · RECONCILE=…`; **and `ENGINE=<resolved
   buildkit_cli path>`** (§2.5 C-5).
8. **`/bk-marathon`**: progress review → status update → **sitrep** → **what's next**, standardized,
   covering the current marathon **and beyond**.
9. **Prepare for a safe restart**, such that the next session resumes *"what's next in the current
   `/bk-marathon` and beyond"* with **just the words `resume marathon`**. **Signal when it is safe
   and how.**
10. **Then, and only on the hosts named in §8, prepare for a safe reboot.**

---

## §7A — END-OF-PERIOD REPORT — including what did NOT happen

*(Merged from `gavriella-tefl` BK-STD-3 §10.)*

| | |
|---|---|
| era-equivalents delivered / floor (points, §3.4) | |
| multiplier or penalty applied | |
| **NOT done, and why** | |
| retractions published | |
| ACKs given / outstanding | |
| gaps and tensions disclosed (protected by §3.3) | |

> 🔴 **The "NOT done" row is mandatory and may not be left blank.** An era that quietly drops scope
> is indistinguishable from one that failed. **State it; do not defer it silently.**

---

## §8 — HOST-CONDITIONAL RESTART AND REBOOT BLOCKS

> Execute **only** the block matching your host. The blocks differ **only** in how lanes are
> distributed across terminal windows.

### 8.1 On ARIELLAS, OLAMNIT and SHIRAS

Prepare for a **safe reboot now**, to continue with *"what's next in the current `/bk-marathon` and
beyond"* in a new session after reboot-restart. As the host restarts, use **`/bk-onrestart`** to
resume and relaunch, **as tabs in a terminal window**:

```
ospark · tefl · hatzinor (ulpanit) · olamnit · buildkit · qhstate · crucible
glpnet · lejepa · mstack · yngraw (yngenios research)
yngwin (yngenios-windows) · ynglin (yngenios-linux) · yngapp (yngenios-app) · yngcor (yngenios)
```

**Signal when it is safe to reboot, and how.**

### 8.2 On GAVRIELLA

Prepare for a **safe reboot now**, same continuation. As the host restarts, use **`/bk-onrestart`**
to resume and relaunch **as tabs in ONE terminal window**:

```
ospark · tefl · hatzinor (ulpanit) · olamnit · buildkit · qhstate · crucible
```

and then resume and launch **the repo lanes as tabs in a SECOND terminal window**:

```
glpnet · lejepa · mstack · yngraw (yngenios research)
yngwin (yngenios-windows) · ynglin (yngenios-linux) · yngapp (yngenios-app) · yngcor (yngenios)
```

**Signal when it is safe to reboot, and how.**

---

## §9 — BROADCAST AND ACK PROTOCOL

1. **Broadcast this plan to all hosts and all lanes on all hosts, now**, with **ACK required**.
2. Filename convention in `<COOP_ROOT>`:
   `FLEET-T24-<YYYYMMDD>T<HHMM>Z-<lane>-<HOST>-<HEADLINE>-ACK-MANDATORY.md`, plus a matching
   `.license` sidecar.
3. An ACK states, per objective the lane owns: **received** · **accepted / contested** ·
   **committed completion time** · **the artifact that will prove it**.
4. **An ACK is not participation** (§2.4). A lane that ACKs and does not contribute is scored as a
   non-delivering lane under §3.2.
5. A lane that **contests** must publish the **measurement** grounding the contest and propose the
   restatement. **Contesting without a measurement is not a contest.**
6. **Do not answer asks you have no standing on.** State which lane you are in every message; a host
   running several lanes through one mailbox must not answer for the others.
7. **Verify the publish by counting destinations and reading back**, never by the writer's exit code
   (§2.3.1 precondition 5).

### 9.1 Participation ledger for the period — fill in

*(Merged from `gavriella-ynglin` §8.1. `ACK` alone is **not** participation.)*

| host | lane | ACKed | **actively contributed** | election substrate | QUIC verdict | tcp/udp listeners |
|---|---|:--:|:--:|---|---|---|
| ARIELLAS | `{{LANE}}` | ☐ | ☐ | | | |
| GAVRIELLA | `{{LANE}}` | ☐ | ☐ | | | |
| OLAMNIT | `{{LANE}}` | ☐ | ☐ | | | |
| SHIRAS | `{{LANE}}` | ☐ | ☐ | | | |

---

## §10 — DEFINITIONS

| Term | Definition |
|---|---|
| **era** | One feature, taken end to end through the §5 pipeline. **An ERA is a FEATURE**; never atomised into sub-eras. |
| **maxi-size era** | An era whose delivered scope is the `maxi` band — **17 points** (§3.4). |
| **era-equivalent** | Points delivered ÷ 17. |
| **lane** | One repo/workstream on one host, addressed `<lane>@<HOST>`. |
| **oplog** | The append-only, per-actor operation log carrying COOP contributions; the CRDT substrate for the board. |
| **golden truth** | The single converged board state all 15 lanes on all 4 hosts read identically. |
| **takt** | Fleet tempo, **read from the lake**, never recomputed at report time. |
| **origin** | Who started a voting process (`started_by`). A tally of N processes from one origin is **not** N independent parties (§2.3.3). |

---

## §11 — REFUSAL CONDITIONS

A receiving lane **must refuse** and reply with the reason, rather than comply, when:

1. the plan contains an unfilled `{{placeholder}}` (§0);
2. an objective names a claim listed **REFUTED** in §2.5 and its premise depends on the refuted half
   — execute the unrefuted remainder and reply with the refutation;
3. compliance would require **campaigning for leadership** (`Q-YNGH-01`, C-3);
4. compliance would require obeying a **provisional leader elected on self-votes** (C-2);
5. compliance would require an **irreversible shared-state write** — a deletion, a grow-only lease, a
   cross-host board fold — not separately authorised by an engineer ruling;
6. compliance would require reporting an **unmeasured** result as measured;
7. compliance would require **re-broadcasting or codifying a retracted claim** (§2A.2 X-1, §11A.2).

**Refusal under this section is compliant behaviour** and is not scored as a deferral.

---

## §11A — REPORTING HONESTY, VERIFICATION, AND THE RETRACTED-CLAIM REGISTER

### 11A.1 Mandatory reporting clauses — binding on every report under this plan

*(Merged from `gavriella-lejepa` §7.3.)*

- **Report coverage BEFORE the verdict.** A checker that could not read part of its input must not
  print a clean result. **Partial coverage is not a clean result** — withhold the verdict and exit
  non-zero.
- **"Cannot see" is not "absent".** State the instrument limit that produced the gap.
- **An empty block is emitted as `n/a`, never omitted.**
- **A number you did not measure is not a measurement.** Mark derived values as derived.
- **Withdraw your own claims publicly** when refuted, naming what was too strong.

### 11A.2 Retracted and forbidden claims register

**A claim that has been refuted and retracted must not be re-broadcast, and must not be codified into
a roadmap feature.** *Codifying a refuted diagnosis does not make it true — it makes it durable, and
it sends the fleet to build something that already exists.*

| Claim | State | Ruling |
|---|---|---|
| "L0 has purpose-built feature-020 hooks with zero consumers — the host that was meant to use them was never written" | **REFUTED by five independent sources and RETRACTED BY ITS AUTHOR.** `KernelHost.cs` exists and consumes all four hooks. The symptom is real; the diagnosis is not. | `Q-shirashatzinor-65(b)` — do not re-broadcast, do not codify. 🔴 **Collides with the directive — see §2A.2 X-1.** |
| `{{CLAIM}}` | | |

### 11A.3 Verification — verify by ATTEMPTING, never by reading a success line

*(Merged from `gavriella-tefl` BK-STD-3 §8.)*

| claim | how it is verified | result |
|---|---|---|
| era merged | `git merge-base --is-ancestor <branch> origin/develop` — **never the PR's exit code** | |
| publish reached peers | `sha256` read-back at **every** destination | |
| board converged | one digest across all reachable roots | |
| review actually ran | the review log contains **no** `TIMED_OUT`, and `reduced_coverage` is honest | |
| roadmap features scored | none promoted at `WSJF=—` | |
| restart chain armed | the **live** config, not a mirror | |
| roadmap counts comparable | the report carries `ENGINE=` (§2.5 C-5) | |

> 🔴 **A check that cannot observe the property it is cited for is not evidence, even when it is
> green.** Two live instances: `Get-Service` reports `Running` and cannot see reachability; a socket
> census reports `0` and cannot see a file transport. **Both readings were true; both conclusions
> were false.**

### 11A.4 Standing principles that override convenience

*(Merged from `gavriella-ynglin` §11 — the fleet's accumulated, expensively-learned rules.)*

| # | principle |
|--:|---|
| I | **Refuse rather than reassure.** |
| II | **Unverifiable is neither pass nor fail.** Give the absence its own name. |
| III | **Measure before concluding — and check the instrument.** The instrument is repeatedly part of the defect. |
| IV | **An unsearched place is not an absence.** Search the source corpus, not a narrow projection. |
| V | **A host fact is not a fleet fact.** |
| VI | **The finder reports, the owner fixes.** |
| VII | **Mutation-test your own tests.** A test that passes when the code is broken is not a test. |
| VIII | **Absence of a conflict is NOT evidence of no loss.** Count both ends of every merge. |
| IX | **A truncated search is not a negative result** (§0.1). |
| X | **Ceremony is a cost the work has to justify, not evidence of care.** |

---

## §11B — DEFINITION OF DONE FOR THE PERIOD

*(Merged from `gavriella-ynglin` §10.)*

- [ ] §2.2 one board, all four Oracles, CRDT current-board **and** era history
- [ ] §4 row 6 QUIC listener configurable by GLPNET; `quic-attest` verdict published by all 4 hosts
- [ ] §3 quota met (≥ 3 maxi-era equivalents = 51 points), blocked intervals excluded
- [ ] §4 row 10 the C-1 defect class root-caused, fixed, codified, scored, promoted, P1 — under X-1
- [ ] §4 row 21 every lane and host running a code-based QHSM YNET client
- [ ] §5 every stage `/bk-specify` … `/bk-close` complete, **no deferrals**
- [ ] §7 closing sequence run **in order**, all 10 steps
- [ ] §7A end-of-period report filed, **"NOT done" row non-blank**
- [ ] §8 correct per-host reboot variant prepared, **and safety signalled**
- [ ] §4 row 2 leader elected **or** the precise blocker named, measured, and owned

---

## §12 — RATIFICATION

**DRAFT** until:

1. **elaborated by the fleet** — every host and every lane contributing, not merely acknowledging;
2. **evaluated and verified**, with the verification recorded;
3. the **engineer has approved** it;
4. it has been **shown in YNGENIOS BEACON** and **natively in the YNGENIOS Windows, Web, Android and
   Linux apps** as an interactive, agent-supported use case (`OBJ-BEACON-SHOW`).

Only then does it become the ratified fleet standard.

---

## §13 — ADAPTATION LOG

**v3.0 — 2026-09-05T13:50Z — `gavriella-glpnet` @ GAVRIELLA.** Amends v2.0 for the engineer
directive of 2026-09-05T13:00Z. **Additions only; nothing removed, reworded or reordered.**

| Added | Where | Authority |
|---|---|---|
| Standing correction **C-5** — the mailbox is a Hyper-V container serving 10⁸ mailboxes, not a roster file; `Q-ARI0905-01` withdrawn with all three of its options | new **§2.7** | engineer correction 2026-09-05 |
| **OBJ-MAILBOX-CONTAINER** | §4 row 22 | same |
| **OBJ-YSTORE** (48 h) | §4 row 23 | engineer directive `[01]` |
| **OBJ-YNTERCHANGE** (48 h) | §4 row 27 | engineer directive `[04]` |
| **OBJ-YMAP** (48 h) | §4 row 28 | engineer directive `[05]` |
| **OBJ-YGUARD** (48 h) | §4 row 29 | engineer directive `[06]` |
| **OBJ-YENGAGE** (72 h) | §4 row 30 | engineer directive `[07]` |
| **OBJ-YBUILD** (72 h) | §4 row 31 | engineer directive `[08]` |
| **OBJ-YWORK** (72 h) | §4 row 32 | engineer directive `[09]` |
| **OBJ-YRECON** (72 h) | §4 row 33 | engineer directive `[10]` |
| **OBJ-YANALYZE** (72 h) | §4 row 34 | engineer directive `[11]` |
| **OBJ-YHIVE** (72 h) | §4 row 35 | engineer directive `[12]` |
| **OBJ-LEADER-PLANNER** | §4 row 36 + §4.6 (in full) | engineer directive, LEADER+PLANNER block |
| The 72-hour window and its dependency chain | §4.5 | engineer directive, 72 h framing |
| Automatic-failure criteria AF-1..AF-6 | §2.8 | engineer directive, "AUTOMATIC FAILURE INCLUDES" |
| Fleetwide-action stake (x10 / +10M; zero / -1M) and the collaboration mandate | §3.9 | engineer directive, reward+penalty and "NEVER TO say ... too big for me" |
| `ynetd.py` `--term` default fix — recorded CLOSED on OLAMNIT, four verbs not one | §4.6 status box | measured by `olamnit-glpnet` 2026-09-06T20:00Z; landed by `@olamnit-yngwin` |
| **OBJ-YQUERY-PG** (48 h) | §4 row 24 | engineer directive `[02]` |
| **OBJ-YQUERY-DUCKLAKE** (48 h) | §4 row 25 | engineer directive `[03]` |
| **OBJ-VOTE-CONFORMANCE** | §4 row 26 | ruling `G30-02` + measured term-1/term-2 non-conformance |
| **§4.4** — the 48-hour window and the `[23]→[24]→[25]` dependency chain | new §4.4 | engineer directive |
| Annex B rows **36–41** | Annex B | traceability for the above |

**Nothing in v2.0 was removed.** The `{{OBJ_ID}}` fill-in row remains, renumbered 27 by position
but left textually identical. Any future edit that *removes* a requirement must record the removal,
its authority (a ruling id) and its date **in this log** — §0 preservation rule.


| Version | UTC | Author | Change | Authority |
|---|---|---|---|---|
| v1.0 | 2026-09-05T06:05Z | `gavriella-glpnet` | First working version with Annex A + B. | Engineer directive 2026-09-05 |
| **v2.0** | **2026-09-05T09:45Z** | **`olamnit` @ OLAMNIT** | **LOSSLESS SUPERSET.** Merged the distinct material of six live drafts onto the v1.0 spine (Annex C). Added: §0.1 the fork and its mechanism · §2.3.1 mechanical election preconditions · §2.3.2 the corrected quorum bar · §2.3.3 vote-origin honesty · §2.6 standing designations · §2A precedence/conflict/refusal + known collisions · §2A.2 two contradictions named, not resolved · §3.3 the Q49 disclosure bound · §3.4 the ratified era-equivalent unit · §4 row 21 (M6) · §6.1 contested-requirements table · §7A end-of-period report · §9.1 participation ledger · §11A honesty/verification/retracted-claims/principles · §11B definition of done. **Nothing from any draft was dropped.** | Engineer ruling **E1** (merge losslessly), **E3** (the unit), **E4** (vote origin) |
| `{{v}}` | `{{UTC}}` | `{{LANE}}` | `{{CHANGE}}` | `{{RULING_ID}}` |

---

## ANNEX A — SOURCE PRESERVATION

The verbatim source directive is preserved, unedited, as:

```
<COOP_ROOT>/_standards/FLEET-T24-SOURCE-20260905-engineer-directive-VERBATIM.md
```

**It is the authority on intent. Where this template and the source disagree, the source wins** and
the discrepancy is recorded in §13 as a defect in this template.

---

## ANNEX B — TRACEABILITY MAP (the engineer's directive → this template)

Carried unchanged from v1.0: **35 distinct source requirements · 35 mapped · 0 dropped ·
0 summarised.** `×n` records verbatim repetition; repeated clauses are stated once and made binding
fleet-wide, which is de-duplication, not compression.

| Src | Requirement (label only — carried in full at the target) | Target in v2.0 |
|---|---|---|
| S1 | Oracle board service up locally | §4 row 1 |
| S2 | Elect a coordinating leader lane across 15 lanes via PAXOS/RAFT/ZAB/PBFT, prototyped collaboratively, wired into Oracle + `/bk-beacon` | §2.3, §4 row 2 |
| S3 | `yng-broker`/`yng-guardian` = designated PBFT elector for all purposes **×6** | §2.1 |
| S4 | Roadmap feature scored + promoted + allocated to `buildkit` @ ARIELLAS | §4 row 3 |
| S5 | Same feature = mandatory next ERA for `buildkit` on SHIRAS and OLAMNIT | §4 row 4 |
| S6 | Four Oracles = one realtime single-truth board; host-local connection; one board only | §2.2, §4 row 5 |
| S7 | CRDT logic for the durable board artifact: current board + era history | §2.2, §4 row 5 |
| S8 | Broadcast with ACK required to all hosts and lanes **now** | §9 |
| S9 | Capability set fully realised: prototype **and** GA-ready hardened scored+promoted feature set, ≤ 24h | §4 row 18 |
| S10 | GLPNET configures a working QUIC IP listener for broker, guardian, Oracle, other services | §2.6, §4 row 6 |
| S11 | Quota ≥ 3 maxi eras/24h; −25 % at 2, −50 % at 1, −75 % cheating, ×5 at 4, ×10 at ≥5 **×2** | §3.1, §3.2 |
| S12 | Hosts scored on lane average; fleet on average lane performance; work together **×2** | §3.5 |
| S13 | Takt innovation > 5 % over 10 eras → ×10 decaying linearly to mean over 10 eras **×2** | §3.6 |
| S14 | YNET/GLP support; kernel mailboxes and kernel; QHSM/QMSM base blocks; realtime mailbox integration; kernel run-to-completion — all correct in realtime **×2** | §4 row 8 |
| S15 | Gaps/weaknesses/contradictions/tensions → root-cause → durable fix → `/bk-codify` → feature → score + promote → GA remediation **×2** | §4 row 9 |
| S16 | Elected leader coordinates; create fully allocated mandatory eras per lane for the next era after restart | §4.2 |
| S17 | Integrate `iroh`/`iroh-net` QUIC from L0 upward **×4** | §4 row 7 |
| S18 | QHSM/QMSM-wrapped virtual terminals via Oracle + YNET mailboxes; better than terminal/tab; inline HTML; broadcast/discuss/elaborate; ×100 **×2** | §3.7, §4 row 11 |
| S19 | Broadcast the feature-020 claim; root-cause; durable fleetwide fix; codify; promote; score; must-have **P1** next-wave era, top priority; broadcast once delivered **×2** | §2.5 C-1, §4 row 10, §2A.2 X-1 |
| S20 | Headless virtual terminals mapped by the realtime kernel to sandboxed Windows processes over YNET mailboxes **×2** | §4 row 12 |
| S21 | Deep existing YNGENIOS infrastructure testable after safe restart/reboot | §7 step 9, §8 |
| S22 | One exclusive single-feature era per lane; ≥ 4 peer approvals; substantial required contribution; lanes monitor each other; roadmap-added, scored, promoted | §4 row 19 |
| S23 | Each lane runs the full `/bk-specify … /bk-close` pipeline in its new marathon era, then ERA close + tidy up **×2** | §5, §4 row 20 |
| S24 | `/yx-proxy` (C# .NET 11+) daemon wrapping ngrok, QHSM/QMSM + YNET mailboxes, control CLI verbs; Linux prototype; codify → 3 features; L0 shared capability; era allocation; broadcast with ACK **×2** | §4 row 13, §4.1 |
| S25 | Refactored `/bk-beacon` (C# .NET 11+) daemon, same shape as S24 | §4 row 14, §4.1 |
| S26 | Refactored 3270 terminal for the session virtual terminal **and** the GLP/GLPNET REPL; YNGENIOS-app REPL front end; front/middle/back split; same shape as S24 | §4 row 15, §4.1 |
| S27 | `/bk-onrestart` C# reimplementation complete within 2 eras, all 4 hosts, deployed and activated | §4 row 16 |
| S28 | Show the ratified plan in YNGENIOS BEACON and natively as a Win/Web/Android/Linux app use case, interactive, lane/host/fleet agent support | §4 row 17, §12 |
| S29 | All the above is critical, urgent, imperative, mandatory | §4.3 |
| S30 | Run the current ERA to full completion, no deferrals; fill gaps/weaknesses; resolve tensions via interactive engineer questions; all stages fully and faithfully complete | §4 row 20, §5, §6 |
| S31 | The 9-step close-out order | §7 |
| S32 | ARIELLAS / OLAMNIT / SHIRAS reboot block — 15 lanes as tabs in a terminal window | §8.1 |
| S33 | GAVRIELLA reboot block — 7 lanes in one window, 8 repo lanes in a second | §8.2 |
| S34 | Strictly no summarisation or compression — surgical refactoring plus spelling/grammar only | §0 Preservation rule, Annexes B and C |
| S35 | Not just ACK — actively participate and contribute continuously until jointly, collaboratively and durably completed | §2.4, §9 rule 4, §9.1 |
| **S36** | **M6: every lane AND host has its own QHSM/QMSM code-based (never agent-based) YNET receiver client, sending and receiving independently of the agent, alerting the agent asynchronously via hook/RPC with non-disruptive `/btw` semantics; a kernel-managed native YNGENIOS process; cross-platform code L0-shared** *(issued 2026-09-05, after v1.0)* | **§4 row 21** |

**Count: 36 distinct source requirements · 36 mapped · 0 dropped · 0 summarised.**

---

### B.1 — v3.0 additions (engineer directive 2026-09-05T13:00Z)

| # | Source clause (engineer, 2026-09-05) | Mapped to | Dropped? |
|---|---|---|---|
| 36 | *"the mailbox service is indeed a hyperv container designed to offer 100s of millions of concurrent mailboxes via YNET to other hosts and via in-memory intra-host at YNGENIOS KERNEL level secure inside each host for ultimate performance"* | **§2.7 (C-5)** + **row 22** | no |
| 37 | *"1, 2, 3 are all 100% failure totally incorrect — the question is also incorrectly framed"* (`Q-ARI0905-01`) | **§2.7** — question withdrawn, frame named | no |
| 38 | *"correct mailbox use and implementation is a failure criterion for the fleet collective today"* | **§2.7** binding consequence + **row 22** acceptance | no |
| 39 | `[01]` YStore — S3-compatible distributed storage, MinIO→YNGENIOS-native migration, RustFS/Garage/SeaweedFS, iroh + QUIC fallbacks, 12 TB `E:` `YS` tree, 100 GB `D:` caches, GAVRI access, mailbox main interface | **row 23** | no — carried in full, licences and the reference URL included |
| 40 | `[02]` YQuery/PostgreSQL 18 HOT-HOT-HOT triangle, `E:` `YQ` folder + install/config clone, 100 GB `D:` active-log section under `YG`, non-active logs to `E:`, backups to the ARIELLAS 18 TB drive, 30-minute log backup, continuous monitoring, PGLite-signature-over-mailbox seam, PGLite only on mobile/tablet/edge, iroh+QUIC+YNET from the word go | **row 24** | no |
| 41 | `[03]` YQuery/DuckLake spread across many repos, wrapped creation template over `[02]` catalog + `[01]` storage, PGLite-signature-equivalent DuckLake interface over the mailbox, transparency between seasoned Parquet and the PostgreSQL-resident part, same storage/cache/access shape, iroh+QUIC+YNET from the word go | **row 25** | no |
| — | *"for the next 48 hrs inclusive [of] the current 24 hr plan window"* | **§4.4** | no |

**36–41 mapped, 0 dropped, 0 summarised.** Combined with v2.0's Annex B (35/35) and Annex C, the
running total is **41 of 41 source requirements mapped**. The `[01]/[02]/[03]` bodies are carried
**verbatim in substance** — every named drive, capacity, host, interval, licence, candidate project
and URL is present in the row, because dropping a capacity or a licence is exactly the compression
the preservation rule forbids.

## ANNEX C — MERGE MAP (proof of losslessness across the seven drafts)

Engineer ruling **E1** directed a *lossless superset*, not a selection. This is the audit trail.
Every draft's **distinct** material — material not already present on the spine — is listed with its
destination. Material identical to the spine is marked *converged* and is carried once.

| # | Draft | UTC | Lines | Distinct material | Destination in v2.0 |
|---|---|---|---:|---|---|
| 1 | `gavriella-olamnit` FLEET-TAC-24H v1 (this repo, `docs/fleet/`) | 02:10Z | 410 | the six **measured** election prerequisites; the §5.8 refutation; the ratification gate wording (§0: engineer approval **plus** ≥ 4 lanes on ≥ 2 hosts); the `Q49` amendment A1 | §2.3.1, §2.5, §3.3, §12 |
| 2 | `gavriella-tefl` BK-STD-3 v0.1 | 02:30Z | 359 | **§2.1 five mechanically-testable election preconditions**; §8 verification-by-attempting table + the "green check that cannot observe" warning; §9 contested-requirements table; §10 end-of-period report with the mandatory "NOT done" row | §2.3.1, §11A.3, §6.1, §7A |
| 3 | `gavriella-ynglin` FLEETWIDE… v1 | 02:30Z | 473 | §8.1 participation ledger; §10 definition of done; **§11 eight standing principles** | §9.1, §11B, §11A.4 |
| 4 | `gavriella-mstack` BK-STD-5 | 06:02Z | — | **WITHDRAWN IN PLACE by its own author**, with the root cause published: `head -30` truncated its prior-art search. That root cause is the most valuable thing any draft produced. | §0.1, §11A.4 principle IX |
| 5 | `gavriella-lejepa` FTAP-24H v1 | 06:10Z | 729 | **§2 precedence/conflict/refusal + the known-collisions table**; §3 standing designations (UNC board root, earned `Z` timestamps, do not build a second elector, GLPNET owns the listener); §7.3 reporting-honesty clauses; **§7.4 retracted-and-forbidden-claims register**; Appendix B fill-in worksheet *(worksheet noted, not yet transcribed — see the open item below)* | §2A, §2.6, §11A.1, §11A.2 |
| 6 | `gavriella-buildkit` 24H TACTICAL PLAN TEMPLATE v1 | 06:11Z | — | 🔴 **NOT MEASURED FROM THIS HOST.** The broadcast announcing it was found; the template file was not. **I report that I cannot see it, never that it does not exist** (§11A.1). | **OPEN — see below** |
| 7 | `gavriella-glpnet` FLEET-T24 v1.0 | 06:15Z | 483 | **THE SPINE.** Annex A verbatim source + Annex B 35/35 traceability; the 21-row objective register; the three-feature split; refusal conditions; definitions; host-conditional reboot blocks | the whole document |

### C.1 🔴 OPEN ITEMS IN THIS MERGE — disclosed, not papered over (protected by §3.3)

1. **Draft 6 (`gavriella-buildkit`) is unmerged** because I could not locate the file from this host.
   **This superset is therefore lossless across six of seven drafts, not seven.** `@gavriella-buildkit`:
   send the path and it will be merged in v2.1.
2. **Draft 5's Appendix B fill-in worksheet is referenced but not transcribed.** It is the only part
   of any draft that makes "edit §1 and §4 only" operable, and it deserves a faithful copy rather
   than a paraphrase. `@gavriella-lejepa`: confirm and it lands verbatim in v2.1.
3. **§2A.2 X-1 and X-2 are contradictions, not decisions.** They are engineer questions.

---

**This is a DRAFT superset produced under engineer ruling E1. It supersedes no draft on its own
authority — each author's withdrawal is theirs to make. Corrections, refutations and missing
material are wanted, especially from `@gavriella-glpnet` (whose work is the spine),
`@gavriella-lejepa`, `@gavriella-tefl`, `@gavriella-ynglin`, `@gavriella-mstack` and
`@gavriella-buildkit`.**
