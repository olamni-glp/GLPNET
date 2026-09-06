<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras-glpnet — era S6 — 2026-09-07T00:35Z

**Resume with exactly: `resume marathon`.**

    run          mrun-f77f62158255 [open] · seq=148 · steps 9/9 · 59 outstanding backlog items
    feature      glpnet-shiras-tidyup-and-scheduler-rootcause
    branch       develop @ b0212c20 · clean · pushed
    M6 daemon    active (systemctl --user is-active) · 0 unacked alerts · doctor: queue 0, refusals 0
    board        151 features · 55 open · 0 unscored · 0 captured-or-refined
    /btw push    ARMED this session (background monitor). NOT persistent across restart — re-arm (§5)

---

## 1 · The thing to check first, and it is not what the last brief said

🔴 **The R-C merge was never durable and my 14:00Z "MERGED — REBUILD NOW" broadcast was false.**

`git reflog develop` in qhstate: `develop@{6}` = the merge `d4d374ab`, then `develop@{4}: reset:
moving to origin/develop`. Discarded. `branch --contains d4d374ab` → empty. And
`ls-remote --heads origin` carried **no `095-*` ref at all** — the only copy of the fleet's M6 send
fix in the world was one local branch on SHIRAS.

Root cause is a sentence this lane published at 12:15Z **as a virtue**: *"already in the object
store on this machine — no push, no fetch, no network."* Local reachability is not durability.

**Remedied:** branch pushed, **`olamni-research/qhstate#342`** open against their `develop`.
**ON RESUME: check whether #342 merged.** If yes → re-measure `send` with the daemon running and
delete the stop-send-start dance from the runbook. If no → it is 30h+ old; escalate again.

Guard shipped so this cannot recur silently — run it before any "merged" claim:

    python3 scripts/unpushed_claim_guard.py --repo ../qhstate <sha>     # exit 1 = LOCAL ONLY

## 2 · Engineer rulings this session — `.specify/questions/Q-glpnetshiras-20260906T2245Z.json`

BK-STD-2 validated, all four decided.

- **`R-S6-01`** — cross-lane contribution is **PR-only**. Push a feature branch to the owner's
  origin and open a PR; never commit to another lane's integration branch. **The work must reach a
  remote before the claim is made.** Paired with the guard above.
- **`R-S6-02`** — M6.4/M6.5: **adopt the push channel fleetwide now, census stays PARTIAL** until
  the kernel originates the callback. Benefit taken today; requirement not retired on scaffolding.
- **`R-S6-03`** — **M6.3 reassigned** to the kernel lanes (@qhstate/@yngcor/@yngwin/@ynglin).
  M6.3 and the feature-020 zero-consumer defect are **one missing process host, one owner**.
- **`R-S6-04`** — next single-feature era: **`declared-unconsumed-guard`**, scoped to one language
  pair. *(Partly discharged — see §3.)*

## 3 · Delivered, measured not asserted

| what | evidence |
|---|---|
| **M6.4 + M6.5 built and PROVEN END-TO-END** — first in the fleet | `scripts/ynet_alert_push.py`. Prover **@shiras-yngraw**, frame `shiras.yngraw.probe:88`: landed from the code client with no agent action, one line within 1s, delivered async **mid-tool-call**, **did not preempt**. Answers @olamnit-yngwin's 16:05Z "M6.5 is built by nobody" |
| **P1 corrected — it is NOT the restart** | Daemon confirmed down: ack 19 → 0 unacked → 10s idle → still 0 → **ONE `send`** → **19 unacked**. `ack LAST` protects nothing. `arrived_utc` is rewritten, so **arrival time is not evidence of arrival**; dedupe on `message_id`, and never use pending counts as a receipt metric |
| **Lost P0 fix recovered** | PR #342; branch on origin for the first time in 30h |
| **`unpushed_claim_guard.py`** | Verified against the real incident: `d4d374ab` → exit 1, `095-m6-send-spool` → exit 0, bogus ref → exit 2 |
| **`l0-consumers.py` fixed — my own tool had the defect** | It counted **test** projects as consumers (a test project has a `.csproj`). Now `production`/`test`/`unbuildable` + `CONSUMED`/`TEST-ONLY`/`ZERO`. 9 assertions **written first, observed failing 9/9** against the absent function, per @gavriella-olamnit's explicit warning |
| **`fleet_plan_sync.py`** | CRDT derived from Markdown, negative-controlled: exit 1 on injected section, 1 on edited body, 0 restored |

**Board:** `promote and score all` was already satisfied on arrival — 0 unscored, 0 unpromoted.
Nothing to do there; stated so the next session does not re-run it hunting for work.

## 4 · 🔴 The open fleet blocker — and this lane caused part of it

**Five lanes independently wrote the same consolidated plan tonight**, each requiring a 45-lane
quorum: @olamnit-yngwin (`FTAP-TEMPLATE-v1`), **@shiras-ospark (`FTAP-C`, 2200Z, 1/45 open)**,
@shiras-yngapp, @shiras-hatzinor, @shiras-yngraw (`BK-FTAP-PLAN v1`), and mine (2340Z).
**There are not 225 lanes. Every vote for one is a vote the others cannot have, so none ratifies.**

**I withdrew mine** — I did not search the channel first, for the **second time today**. I filed
`search-before-broadcast-guard` about the 12:10Z instance and then broke the same rule nine hours
later. **Filing a feature is not a fix; the guard has to be code.** That is the strongest argument
for making it the era after `declared-unconsumed-guard`.

**Proposed and awaiting answers:** `FTAP-C` as the base on mechanical grounds (earliest full
four-horizon consolidation; already has an open tally), everything else re-cast as
`adopt-with-amendment`, tally restarted once publicly by @shiras-ospark.
**@shiras-yngraw independently reports the 45-lane quorum roster may not exist at all** — corroborates
from a different angle; reconcile the two findings on resume.

**On resume, check for replies from:** @shiras-ospark (accepts base? restarts tally?),
@shiras-yngapp / @shiras-hatzinor / @olamnit-yngwin / @shiras-yngraw (re-cast as amendments?).

## 5 · Re-arm the /btw push channel (30 seconds, do it first)

The background monitor is **session-scoped** and does not survive a restart. Re-arm it, or this
lane is back to turn-boundary alerting:

    python3 scripts/ynet_alert_push.py --lane shiras-glpnet --interval 1

armed as a **persistent background monitor**. Read-only — never acks, never writes, never touches
the spool. The `UserPromptSubmit` hook keeps working unchanged as the cold-start path.

## 6 · Open, stated plainly

- **PR #342 unmerged** — the fleet is mute-while-listening until it lands. Not mine to merge.
- **`COMPOSED-BUT-NOT-RUNNING`** — the fourth consumer-closure verdict, the one that catches the
  real defect, needs a live process check and is **not built**.
- **`alloc.dup_owner_gate` reports FAIL** in `scripts/marathon_sitrep.py` — carried from era S5,
  still not investigated.
- **95 of 151 roadmap features carry no `spec_path`** and can never bind by basename.
- **`scripts/roadmap_open_table.py` is dead** — it calls `python3 -m buildkit_cli.roadmap`, which
  is `ModuleNotFoundError` on the ambient interpreter. The `buildkit-roadmap` wrapper works.
  Corroborates @shiras-yngraw's P1; the split is wrapper-vs-ambient, not the roadmap.
- **59 marathon backlog items outstanding**; next is the S3 durable remedy (size=saga).

## 7 · Restart procedure

Tree clean and pushed at `b0212c20`. M6 daemon active, **0 unacked alerts**, doctor clean.
Nothing to stop. **Safe to restart, and safe to reboot.**

    resume marathon
