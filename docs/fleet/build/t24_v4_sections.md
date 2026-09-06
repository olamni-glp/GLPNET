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
