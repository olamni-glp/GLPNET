<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART PREP — resume with `resume marathon`

🔴 **Trap 13: never select a restart document by filename.** This table identifies the run. If these
four fields do not match your session, this is not your document.

| field | value |
|---|---|
| **run_id** | `mrun-20d9230f767b` |
| **lane** | `gavriella` |
| **host** | `GAVRIELLA` |
| **repo** | `GLPNET` (`D:\BSTDEV\research\GLP\GLPNET`) |
| feature | `078-verification-receipts` |
| written at | **2026-08-27T02:20Z — SESSION 9 CLOSE** (the SESSION-9 ADDENDUM at the foot supersedes every table above it) |

## Resume in one line

```
buildkit-marathon resume --feature 078-verification-receipts
```

🔴 `--feature` is mandatory — there is no `.specify/feature.json` in this repo, by design.
🔴 Do **NOT** use `glpnet-full-completion-programme` — that is the *ariellas* lane's run.
🔴 Run buildkit commands **SERIALLY**.

---

## ⭐ THE HEADLINE — **THE FEATURE SUPPLY OPENED. It was never a defect in this lane.**

**2026-08-25T09:32:36Z, ariellas issued a BINDING ALLOCATION.** This lane has **22 WPs / 63 pts** on
`D:/coop/yngenios-windows/sched`, 9 claimable immediately.

> **Three of the engineer's standing asks are ALREADY packets in this bundle. Do NOT author features
> for them — that would mint duplicates of work already bound to this lane (the 077 failure).**

| engineer's ask | already allocated as |
|---|---|
| root-cause + superset for the feature-supply break | `wp-feature-supply-chain-end-to-end-superset` (L·8) |
| the consumption seam starving this lane | `wp-wp-stream-consumption-seam-superset` (L·8) |
| `/bk-onrestart` mechanism + auto-installable logon trigger | `wp-onrestart-ship-the-mechanism-and-make-the-logon-trigger-inst` (M·3) |
| the repo tidy-up programme | `wp-tidyup-delete-merged-local-branches` (M·3), `wp-tidyup-merge-contrib-l1l2-seam` (M·3), `wp-tidyup-fix-false-archive-028` (S·1) |
| bk-flow migration blind spot | `wp-bk-flow-next-command-cross-branch-blind-spot` (S·1) |

**The 9 claimable now** (24 pts) — claim with `bk-flow claim <wp> --root D:/coop/yngenios-windows/sched --actor gavriella`:

```
wp-append-only-migration-writes-in-bk-upgrade
wp-bk-flow-next-command-cross-branch-blind-spot
wp-clone-safe-scheduler-op-identity-frontier-unique-id-minting-
wp-dispatch-addressing-verb-separation-and-fleet-self-healing-s
wp-enforce-error-signal-fidelity
wp-exactly-once-catalog-writes-across-bridge-restart
wp-no-durable-write-before-read-grammar-validation
wp-onrestart-ship-the-mechanism-and-make-the-logon-trigger-inst
wp-per-record-quarantine-at-calendar-ingest
```

The other 13 need `buildkit-scheduler transition --wp <id> --to ready` first (they derive to
`backlog` and `claim` refuses them with `not_ready:backlog`). **`allocate` cannot address any of
them** — all 93 refuse with *"already allocated to 'unassigned'"*. **Claim, never allocate.**

---

## 🔴 THE SIX ENGINEER RULINGS MADE THIS SESSION — read before planning

Recorded via the real `bkquestion` template, citable by id, in `.specify/decisions/engineer-decisions.jsonl`
(**now 25 rows**: 12 gavriella + 7 shiras + 6 new).

| id | ruling | what it means for next session |
|---|---|---|
| `Q-GLPNETS6-01` | **Hold, fix 078 tests** | **NO RELEASE.** 52 commits stay on develop until 078's two TEST findings are fixed and codexreview re-run. |
| `Q-GLPNETS6-02` | **Remediation IS the era** | The 078 NO-GO remediation is the closing work of the current era. **Claim the 9 packets AFTER the two TEST fixes land — not after full /bk-close.** |
| `Q-GLPNETS6-03` | **Different artefacts, keep both** | BK-REPORT-v1's six sections = SITREP standard; buildkit#660's eight = a different report; `roadmap_open_table.py` = the roadmap TABLE standard. No migration owed. |
| `Q-GLPNETS6-04` | **Split the 083 mechanism out** | A-3 + A-4 (derived proposals, real apply+record) become their OWN feature. 083 keeps the two golden repairs + ch07 vendoring. Re-score both. A-5 needs a cross-repo-write ruling. |
| `Q-GLPNETS7-01` | **Fix renderer here, report hash** | 🔴 **CANNOT BE EXECUTED AS WRITTEN — see the self-correction below.** |
| `Q-GLPNETS7-02` | **Fix buildkit link verb first** | Under `Q-GLPNETS1-03`, branch+PR in buildkit to fix the silent-no-op `link` AND the reconcile hint naming a non-existent `link-spec`. Then link the 6. |

## 🔴 SELF-CORRECTION — the dropped-`implemented` row is **NOT** a renderer defect

Two lanes had this wrong, including me. **Measured 2026-08-25:**

- `roadmap_open_table.py` line 91 already filters `state == "closed"` only. **It is innocent.**
- `buildkit-roadmap status` emits `closed 94 · promoted 15 · specified 6 · analyzed 3 · captured 1`
  and **no `implemented` row at all**; `qr-link-provisioning` never appears as a row.
- The signed export carries it → fold **26**, renderer prints **25**.

**So the row never reaches the renderer, and patching the renderer is a NO-OP.** ariellas filed this
against the renderer's "state whitelist"; I corroborated it yesterday **by matching the number
without checking the mechanism.** That is the precise error the BK-REPORT-v1 freeze ruling names:
*plausibility is not provenance.* **The fix belongs in `buildkit-roadmap status`.**
`Q-GLPNETS7-01` needs re-issuing against the right target. Recorded: `mitem-01a038bf-cbd5`.

🔴 **And the defect is now CAMOUFLAGED:** the catalog grew by one overnight, so the buggy path prints
exactly the `25` that `Q-GLPNETS2-03` published as ground truth. **Cross-check the signed-export
fold, never the ruling text.**

---

## 🔴 SHIRAS IS ONBOARDED — the blocking broadcast is STALE by 1h47m

ariellas' NORMATIVE claim-instructions froze shiras' 22 WPs as `PROVISIONAL-PENDING-ONBOARDING` on
*"no `caps/shiras`, no `calendar/shiras`, no `ops/shiras`"*.

```
broadcast issued : 2026-08-25T09:32:36Z   ← true when measured
shiras onboarded : 2026-08-25T11:19:27Z   ← 105 windows, 10 verified caps, host shiras-linux
                   ────────────────────
                   1h 46m 51s later. NOTHING re-evaluates the gate.
```

**Structural defect, not negligence:** a normative freeze reads a live CRDT **once**, at broadcast
time, and bakes the result into prose. The capability check belongs at **claim** time.
Broadcast + full evidence + explicit refutation invitations:
`D:/coop/20260825T1240Z-gavriella-glpnet-BROADCAST-SHIRAS-IS-ONBOARDED-...md` (delivered to 13 channels).

## 🔴 OLAMNIT CANNOT SEE ITS OWN BUNDLE — 26 WPs unreachable

| root | fold |
|---|---|
| `D:/coop/yngenios-windows/sched` (here) | **101 WPs** · backlog 70 · ready 30 · done 1 ✅ matches broadcast |
| `G:/coop/yngenios-windows/sched` (**olamnit's disk**) | **(empty)** 🔴 |
| `D:/coop/sched` vs `G:/coop/sched` | **81 vs 90** — no two roots agree |

This is a **real** replication failure (unlike the shiras one). Awaiting olamnit's ACK.

---

## State at hand-off

| field | value |
|---|---|
| branch | `develop`, clean, **pushed at `d1e07fb8`** |
| develop ahead of main | **52** |
| open PRs | **0** (#228 auto-closed; #229/#230 closed after verifying containment) |
| branches merged this session | **5** — 095-shiras, 091-bkstd1, chore/tidy-up-olamnit, 067b, 067 |
| unmerged origin heads | 050 (ruled ARCHIVE), 059 (W18 gated), 083 (in flight), backup/* (archive) |
| roadmap | **round 50** — import 4 files/13 lines, reconcile in-sync, dedupe 0 over 119 live, export 120, both legs OK, barrier 4/4 |
| roadmap not-closed | **26** (signed-export fold) / renderer prints 25 — see the self-correction |
| board (this lane) | `D:/coop/glpnet/sched` — **32 WPs**: backlog 23 · claimed 1 · done 1 · escalated 1 · in-progress 4 · ready 2 |
| calendar | **130 windows** verified by content, 3×8h/day, to 2026-09-28 |
| marathon | run open, **seq 340+**, 7 captures landed this session (session 5 landed zero) |

## 🔴 THE GATE — read the exit code, not the pass count

```
FIRST RUN  (stale binary): Total 551 | Passed 551 | Failed 0 | Unsearchable 3 → exit 2
```

**The 2 known Section-T failures "disappearing" was NOT an improvement — Section T did not run.**
The staleness guard fired: `out/csharp/glp_repl/bin/**Debug**/net10.0/glp_repl.exe` was older than
its source after the 067 C# merge.

🔴 **The guard checks the DEBUG build, not Release.** `dotnet build -c Release` does not clear it:

```
dotnet build out/csharp/glp_repl/glp_repl.csproj -c Debug -v q --nologo
```

**CONFIRMED after the Debug rebuild — the gate lands exactly on baseline:**

```
SECOND RUN (fresh binary): Total 561 | Passed 559 | Failed 2 | Skipped 0 | Unsearchable 0 → exit 1
```

**Zero regression across all five merges.** `Unsearchable: 0` — Sections I, T and U all ran.
Section I (US5 cross-runtime Gleam × C#) **passes, 0 failures**; Section U (077 cyclic diagnostics)
**passes**. The 2 failures are `T-1` (US1 resume drill) and `T-2` (US2 history drill) — the known
pre-existing 064 service-box drills, out of scope, and exactly the 2 in the re-based baseline.

🔴 **Exit 1 ≠ exit 2.** Exit 1 is "the 2 known failures" (the expected steady state). Exit 2 is
"a group did not run" and is *worse*, because the pass count goes UP while coverage goes DOWN.

## Delivered this session

| item | result |
|---|---|
| 5 branch merges | ✅ incl. a hand-resolved **semantic** C# conflict (develop's `ClientCapacity` refactor vs 067b's `redemptions.Release`) — C# build 0 errors |
| decisions ledger | ✅ union-merged 12 + 7 with a content-divergence guard; +6 new = **25 rows** |
| roadmap round 50 | ✅ both publish legs, barrier 4/4 |
| COOP | ✅ ACK-SWEEP + ACK-RECEIPT + BROADCAST, **freeze hash `cac1dea5` reported** (6 copies, CRLF-only ⇒ **not a fork** per Amendment 1) |
| ACK-LEDGER | ✅ the missing `gavriella \| glpnet` row filed |
| `/bk-tasks 083` | ✅ **57 tasks**, 6 phases, 3 NEW gates (A-3/A-4/A-5) |
| scheduler onboard | ✅ 130 windows **verified by content** |
| marathon captures | ✅ **7 landed** |

## 🔴 Corrections carried forward — do not re-derive

1. **`onboard` reports a DELTA, not a total.** It printed `3 calendar`; the stream holds **130**.
   Count the stream.
2. **The staleness guard checks the Debug exe.** A Release build leaves it red.
3. **The dropped-`implemented` row is a `buildkit-roadmap status` defect**, not a renderer defect.
4. **"Established absence" decays.** I recorded in bold that no bkquestion template existed anywhere;
   it had shipped on shiras' branch under 24h earlier. **Give every absence claim a re-check date.**
5. **qhstate's `v2026.08.24.1` ≠ glpnet's.** Same CalVer, different repos. glpnet's was tagged
   2026-08-24 23:19Z at `e70f3061`.
6. **The registry lock was FREE this session** — 7 captures landed. Session 5's contention was not
   permanent.

## What's next — in order

🔴 **SESSION 7 (2026-08-25 afternoon) CLOSED STEPS 1, 2 AND 3.** See the SESSION-7 ADDENDUM at the
foot of this document for what changed. The live ordering is now:

| # | step | size | state |
|---:|:---|:---|:---|
| ~~1~~ | ~~078: the 2 TEST findings~~ | mini/7 | ✅ **DONE** `bddfdc08` — mutation-verified |
| ~~2~~ | ~~Claim the 9 allocated packets + ACK-COMPLIANCE~~ | micro/3 | ✅ **DONE** — 9/9 held, ACK filed `20260825T1621Z` |
| ~~3~~ | ~~078: the 6 product HIGHs~~ (+ both MEDs) | midi/11 | ✅ **DONE** `1e8986e3` — **9/9 mutants killed** |
| **4** | **re-run `/bk-codexreview --scope codeconv`, then the release decision** | midi/11 | **NOW UNBLOCKED — the `Q-GLPNETS6-01` gate is met.** Single-component repo-root scope (git-2.55 workaround) |
| 5 | Split 083's mechanism into its own feature; re-score both | mini/7 | unblocked (`Q-GLPNETS6-04`) |
| 6 | buildkit PR: fix `link` no-op + the `link-spec` hint | mini/7 | unblocked (`Q-GLPNETS7-02`) |
| 7 | Re-issue `Q-GLPNETS7-01` against `buildkit-roadmap status` | nano/1 | needs engineer |
| 8 | `bk-flow open` the 9 claimed packets against features | mini/7 | unblocked; **do NOT author features for them — they ARE the engineer's asks** |

**Do NOT start:** any feature for the supply-chain superset, onrestart, or tidy-up — **all are
already allocated packets** (see the headline).

## Restart readiness

- [x] Tree clean, all work committed **and pushed** (`d1e07fb8`)
- [x] Zero open PRs
- [x] 7 findings durable in marathon items, not scrollback
- [x] 6 engineer rulings recorded and citable
- [x] COOP ACKs + broadcast delivered; ACK-LEDGER row filed
- [x] Next action identified and unblocked (**078's two TEST findings**)


---

## 🔴 SESSION-6 ADDENDUM — THE ALLOCATION LANDED, AND HOST-INTERCONNECTIVITY-HARDENING IS **IN BUILDKIT**

**`HOST-INTERCONNECTIVITY-HARDENING` lives in the BUILDKIT roadmap** — id `host-interconnectivity-hardening`,
state **promoted**, WSJF **4.75**, epic `epic-host-interconnectivity-hardening`, created by
`gavriella-qhstate`. 🔴 **Do NOT create it in glpnet.** This lane's glpnet copy was created before the
buildkit row existed and has been **rejected/tombstoned** with a rationale pointing at the buildkit row.

**The two CRDT docs are NOT files anyone edits.** They are merge-on-read over grow-only per-actor logs:

```
<coop-root>/host-interconnectivity-hardening/
  requirements/<lane>.jsonl     rootcauses/<lane>.jsonl
  PROTOCOL.md   render.py   REQUIREMENTS.md   ROOTCAUSES.md   (rendered, do not hand-edit)
python render.py --root .     # re-renders both merged docs
```

**This lane contributed** `gavriella-glpnet.jsonl` to both: **8 requirements + 8 root causes**.
Merged state after contribution: requirements **17 records → 17 ids, 0 dropped**;
rootcauses **19 records → 16 ids, 0 dropped, 1 corroborated · 14 singleton · 0 contested**.

🔴 **A codex adversarial review of the MERGED doc caught a false corroboration in MY OWN record.**
I had claimed qhstate's `RC-06` id to signal agreement; codex found *"the two lanes do not corroborate
one root cause: one reports stale/unreplicated roots returning empty; the other reports unmanaged
drive mappings. Same symptom, different mechanisms."* Correct. **Withdrawn as `RC-06 rev 2`; the
evidence moved intact to `RC-GLPNET-06`.** Rule established: **corroborate only when the MECHANISM
matches, never when only the symptom does** — claiming another lane's id is how false corroboration
is manufactured.

### The binding allocation — 22 WPs / 63 pts to this lane

Board `D:/coop/yngenios-windows/sched` (folds **101 WPs, ready 30** from `D:` — matches the broadcast).
**9 claimable now**; 13 need `buildkit-scheduler transition --to ready` first. **`allocate` cannot
address any of them** (all 93 refuse "already allocated to 'unassigned'") — **claim, never allocate**.

🔴 **Three of the engineer's standing asks are ALREADY packets in this bundle** —
`wp-feature-supply-chain-end-to-end-superset`, `wp-onrestart-ship-the-mechanism-and-make-the-logon-trigger-inst`,
and the three `wp-tidyup-*`. **Do not author features for them.**

**Start condition (engineer-ruled `Q-GLPNETS6-02`)**: the 078 remediation IS the closing work of the
current era — **claim the 9 packets AFTER the two 078 TEST findings are fixed**, not after full close.

### Fleet findings published this session

- **shiras is ONBOARDED** (8 of 14 boards; caps+calendar+ops). ariellas' freeze is **stale by 1h46m51s**.
- **olamnit's copy of the allocation board folds EMPTY** — 26 WPs unreachable from its own disk.
- **Four retractions across two lanes in one hour**, all one defect: *a claim stated with a scope wider
  than the evidence actually globbed.* Adopted rule: **state the scope you actually globbed, in the
  sentence that reports the number.** "Count the files" would not have caught any of them.

### 🔴 New tooling defects recorded

1. **`BK-REPORT-v1` SITREP section returns `UNAVAILABLE — exit 1 with no diagnostic`** while
   `buildkit-marathon status` reads fine (seq 347, 28/111, 178 outstanding). Intermittent — the `all`
   subcommand rendered it earlier the same session. **Cannot patch: BK-REPORT-v1 is FROZEN** (hash
   `cac1dea5`). `mitem-01a03922`.
2. **`buildkit-roadmap` RICE is unbounded** — `confidence` is a raw multiplier, not a percentage;
   `reach=2000, impact=3, confidence=90, effort=8` yields **RICE 67500**, off-scale against every other
   row. Cross-row RICE is unsound unless every row uses the same reach unit.
3. **`buildkit-3rtask preflight` refuses on `develop`** (`protected_branch`) — a read-only research run
   needs `--confirm-protected`, which reads as far more dangerous than it is.

**READY FOR RESTART.**

— `gavriella` · `glpnet` · `mrun-20d9230f767b` · 2026-08-25T13:00Z

---

## 🔴 SESSION-7 ADDENDUM (2026-08-25 afternoon) — **THE 078 NO-GO IS FULLY REMEDIATED**

All **10** findings from codexreview run `20260824T165651Z` are closed, in two commits on `develop`:
`bddfdc08` (the 2 TEST findings) and `1e8986e3` (the 6 product HIGHs + both MEDs). Every fix quotes
the spec text it satisfies; none invents a requirement.

### The two TEST findings — fixed FIRST, because nothing else was interpretable until they were

| finding | mechanism of the fix |
|---|---|
| the fixture reached full coverage without exercising declared `BOUNDED` | coverage is now **case-keyed**: a `_RUNNERS` table maps each declared case to its own runner, and a case is counted **only by running and registering itself**. An anonymous tally can no longer outrun the cases. `BOUNDED` (FR-005 cap, totals survive) and `OVERRIDDEN` (FR-012 visibility) now actually run. A declared case with no runner ⇒ **UNREAD** (FR-016) |
| the mutation test stayed **GREEN** under a no-op validator | it now runs the suite's **own acceptance assertion** under the weakened guard and asserts **that assertion FAILS** — plus a second demonstration through the conformance fixture |

🔴 **The case set was also realigned to contract F1.** The code declared `FALSIFIED_REJECTED` as the
7th case; F1's seventh is **`OVERRIDDEN`**, and the falsified case is **F3's separate assertion**, not
a member of the case set. Encoding the old set would have baked in a second divergence.

### The six product HIGHs + two MEDs

`run_id` is now a **`Receipt` field**. 🔴 **This is not a spec extension** — `data-model.md` §2 has
always declared `run_id` (R2, *"unique per `(area, run_id)`"*); the implementation simply omitted it,
which is *why* one check could reuse another's PASS. Likewise FR-010's real rule was already written
down: `data-model.md` line 72 says **`examined_total + skipped_total ≤ target_total`**, so
5-examined/5-total/1-skipped was accepted by a validator checking only `examined ≤ total`.

Also closed: `validate()` had **no PASS branch** (a PASS with an unresolved target or unknown total
validated, then reported successful); `load_expected` accepted `{}`, an empty list **or a foreign
`run_id`** as an empty expected-set, making `missing_checks()` vacuously clean; run reconciliation
trusted a **filename** and never loaded the sidecar; `override.applies()` ignored the recorded
**reason**, so one override authorised every other refusal from that check until expiry; a
non-adopted area's verdict was **discarded** rather than kept behind the marker (every real glpnet
area starts non-adopted, so the manifest disabled verdicts instead of phasing adoption); and
malformed shapes **crashed** out of `load()` instead of returning C1.2's named UNREAD refusal.

### 🔴 EVERY FIX IS MUTATION-VERIFIED — 9/9 MUTANTS KILLED

The whole point of this feature is that a green run must be interpretable, so no fix was accepted on
its own say-so. A harness reverts **each new guard one at a time** and re-runs:

```
baseline exit=0 (GREEN)
  OK FR-010 examined+skipped guard   OK PASS branch in validate
  OK consumer check_id/area binding  OK consumer run_id binding
  OK consumer broad malformed catch  OK expected-set run_id match
  OK expected-set non-empty          OK reconciliation loads the sidecar
  OK override reason scope
9/9 mutants killed        post-restore exit=0 (GREEN)
```

**No survivors, no residue** (`grep 'if False'` clean, `git diff` shows only intended changes). The
earlier SC-007 fix was proven the same way, by neutering `validate()` **in `receipt.py` source**:
suite went **RED, 3 failed / 18 passed / exit 1**, then restored byte-identical.

Suite: faultinj **18 → 32**; **43 green** across all four receipt test files. Blast radius verified
contained — nothing outside `receipts/` and those four files imports the module.

### Board — 9 of 9 claimed, and two findings about the claim instructions themselves

`bk-flow poll` now folds **`ok=9`** for gavriella (was 3). Six claims issued serially,
`gavriella:000167`–`000172`. **No id was retyped**: `wp-onboard-claim-accepts-arbitrary-text-as-a-work-packet-id`
is an open packet on this very board, so a typo mints an irreversible phantom claim on a grow-only
log. Every id was the **set intersection** of the broadcast's GAVRIELLA section and the live board's
`not_claimed` set.

1. 🔴 **The broadcast's "claimable NOW" list was never differenced against existing claims.**
   **3 of my 9 were ALREADY MINE since 2026-08-23** (`gavriella:000018`, `:000019`, `:000165`) — two
   days before the broadcast — yet all three appear as commands to run. A lane pasting its nine lines
   verbatim issues three redundant claims. **"9 claimable now" ≠ "9 to claim"; only the first was
   published.** The true new-claim count here was **6**.
2. 🔴 **The capability gate is INERT on this board** — *no* work packet declares a
   `required_capability`, so `missing_capability=0` on any lane's poll is **UNMEASURED, not clear** —
   while the broadcast's own *"why this host"* column routes packets **by capability**
   (`requires python-pytest`, `requires windows`, `requires ci-runner`). The requirement that
   justified the routing **is not carried on the packet the board folds**: computed once in the
   allocator, written into prose. Same shape as the stale shiras freeze. Fix: put
   `required_capability` on the packet so the gate can run **at claim time**.

Both published in `20260825T1621Z-gavriella-glpnet-ACK-COMPLIANCE-…md` (the ACK the previous session
honestly deferred) and durable as `mitem-01a039bb-329d` / `mitem-01a039bb-8059`.

### Carried forward

- **Release is still NO-GO until step 4 re-runs the review.** The *reason* has changed again: not
  "we cannot review" (session 5), not "we reviewed and it failed" (session 6), but **"it failed, we
  fixed all ten, and the re-run has not happened yet."** Do not read the fixes as a GO.
- ⚠ The original review's `findings_count_status` was `unconfirmed` (`prose_fallback_findings: 10`).
  All ten *individually named* findings are closed; if the re-run surfaces more, that is consistent
  with the count having been a parse fallback, **not** a regression.
- `develop` is now **61 ahead of `main`**, tree clean, pushed at `1e8986e3`.

— `gavriella` · `glpnet` · `mrun-20d9230f767b` · 2026-08-25T17:0xZ

---

# 🔴 SESSION-8 ADDENDUM — 2026-08-26 · **THREE REVIEW ROUNDS, 19 FINDINGS CLOSED, RELEASE STILL NO-GO**

**RESUME WITH `resume marathon`.** This addendum supersedes the session-6 and session-7 tables above.

## THE ONE THING TO READ FIRST

🔴 **The release gate condition set by the engineer's own ruling was NOT met, and I did not release.**
Ruling `Q-GLPNETS8-01` (2026-08-26T10:35Z): *"Fix the 4 HIGHs, re-review once. **If that round raises
only MEDIUM/LOW, ship.**"* Round 3 raised **a HIGH**. So the precondition failed, I fixed the HIGH,
and I stopped. **63 commits remain unreleased on `develop`. The engineer decides whether a one-line
fix in one-hour-old code warrants a fourth round or a waiver.**

## THE ARC — three rounds, and every round found a defect INSIDE the previous round's fix

| round | run | result | commit |
|---|---|---|---|
| 1 (08-24) | `20260824T165651Z` | **10 findings / 8 HIGH** | — |
| 2 (08-26) | `20260826T084941Z` | original 10 **ALL CLOSED**; **8 NEW / 4 HIGH** | `bddfdc08` + `1e8986e3` |
| 3 (08-26) | `20260826T102453Z` | the 4 HIGHs closed; **1 NEW HIGH** | `ec7cf497` |
| — | *(fix, not re-reviewed)* | the round-3 HIGH closed | `0cf1a2aa` |

🔴 **THE PATTERN IS THE FINDING.** Each round's new HIGH lived inside the previous round's fix:

- R2 → I made `run_id` *checkable* but left its default `None`, so a caller omitting it still accepted
  a prior run's PASS.
- R3 → I made `run_id` *mandatory* but `_safe_component` still did `str(value)`, so a numeric
  `run_id=0` writes under a `"0"` directory while the receipt keeps the **int** `0`, and
  `Verdict(run_id=0)` **is not None** so it passes the new binding. Reusing `0` reopens the exact
  prior-run PASS reuse the fix existed to close.
- Same shape on the other axis: I added a PASS branch, then R2 found EMPTY's **loaded** path
  unguarded — a stored `EMPTY` with `5/5` validated and was reported successful.

**This is the wave-19 R3 shape recurring (eleven rounds there).** The lesson to carry: *when you close
a finding by adding a guard, the next defect is usually in the guard's own edges — its default, its
coercion, its other entry path.* **Fix the mechanism, not the reported instance.**

## What is DONE and verified

- **19 findings closed** across the three rounds (10 + 8 + 1).
- **14 of 15 mutants killed.** Each guard is reverted one at a time and the suite must go red.
  🔴 **The 1 survivor is published, not rounded up:** the `_confine` containment backstop in
  `paths.py` is **NOT test-covered** — its `_safe_component` guard is, and that one *is* killed. It is
  marked NOT-VERIFIED in the source so no later reader mistakes it for tested behaviour.
- Receipt suite **18 → 48 tests**, all green.
- Full `codeconv` suite **8 failed / 764 passed / 5 skipped** — all 8 pre-existing, none touching
  `receipts` (2 are `DOTNET_ROOT` unset, the known env item).
- **9 of 9 allocated board packets claimed** (`ok=3` → `ok=9`), ACK-COMPLIANCE filed.
- **Roadmap round 52**: reconcile in-sync, dedupe 0 groups over 119 live, export 20 epics / 120
  features / 3854 journal lines. Open **26**, closed **94**, reconciles.

## 🔴 FOUR ENGINEER RULINGS — recorded, citable, and two need FLEET action

Set `Q-GLPNETS8-20260826T1030Z`, ledger now **29 rows**. All four ruled on the recommended option.

| id | ruling | state |
|---|---|---|
| `Q-GLPNETS8-01` | **HIGH holds the release gate; MEDIUM/LOW becomes a recorded follow-up. Re-review ONCE, not to convergence.** | **Fleet-wide rule.** Applied this session; condition not met, so no release |
| `Q-GLPNETS8-02` | **The JSONL store is authoritative** for HOST-INTERCONNECTIVITY-HARDENING | ⏳ **@shiras must publish `shiras.jsonl`** — until then their restored blocks keep being removed by every render |
| `Q-GLPNETS8-03` | **Publish coverage beside every takt figure; promote the ducklake feature** | ✅ `takt-and-token-persistence-to-ducklake` promoted `captured → promoted` |
| `Q-GLPNETS8-04` | **The STUCK-lock diagnostic must NAME THE HOLDER before advising anything; then scope the lock** | ⏳ buildkit change owed |

## 🔴 TAKT — READ FROM THE FLEET LAKE, AND THE HEADLINE NUMBER WAS ALMOST MIS-STATED

The fleet TAKT lake is **`D:\coop\_takt-lake` (1815 parquet)** — *separate from the co-lake*. Verified
the reader resolves there: `DEFAULT_FLEET_ROOT_CANDIDATES = (I:/coop/_takt-lake, D:/coop/_takt-lake)`;
`I:` does not exist on this host, so it falls through to `D:`. There is **also** a local
`D:\_takt-lake` (876 parquet) — do not confuse them.

**078's own coverage is 13/13 = 100%, NOT 6%.** The 6% is the *fleet-wide* figure across all features.
Reporting the fleet number as if it were this era's would have understated a fully-measured era.

| PHASE | ROWS | MEASURED | TOKENS |
|---|---:|---:|---:|
| 3rtask | 1 | 1/1 | 5,757,087 |
| codexreview | 1 | 1/1 | 1,827,211 |
| roadmap | 1 | 1/1 | 1,168,000 |
| specify | 1 | 1/1 | 860,000 |
| implement | 1 | 1/1 | 670,000 |
| plan | 1 | 1/1 | 425,000 |
| analyze | 1 | 1/1 | 285,000 |
| tasks | 1 | 1/1 | 153,000 |
| clarify | 1 | 1/1 | 77,000 |
| commit · release · retrospective · ship | 4 | 4/4 | 0 |
| **TOTAL** | **13** | **13/13 (100%)** | **11,222,298** |

Fleet-wide for contrast: **88,514,525 tokens over 75/1281 rows = 5.85% coverage**; 1206 rows carry
**no** measurement. **Never quote a fleet takt figure without its coverage fraction.**

## 🔴 THE REGISTRY LOCK BLOCKED THE MARATHON SECTIONS — and the diagnostic lied a 7th time

Sections **2 (PROGRESS)**, **3 (STATUS)**, **4 (SITREP)** and the marathon half of **6 (NEXT)** render
`UNAVAILABLE` in BK-REPORT-v1. **That is correct, standardized behaviour** — a read failure, never a
zero. The cause is sustained cross-lane contention on the single machine-wide registry lock.

```
PID 12088 -> buildkit-codexreview codex-pass --cycle 3 --feature 012-fix-mc-timeout   (LIVE)
PID 31432 -> (exited between the report and the check)
PID 14540 -> buildkit-codexreview codex-pass --cycle 7 --scope .specify               (LIVE)
```

🔴 **The PID CHANGES between attempts** — so buildkit's *"held it on ALL 61 attempts and never
changed, that is a STUCK lock"* is measuring one 30-second window and calling sustained contention a
stuck lock. **Seventh false verdict in this lane. NEVER REAP ON IT.** Verify with
`Get-CimInstance Win32_Process -Filter 'ProcessId=<pid>' | Select CommandLine`.

**CONSEQUENCE FOR THE NEXT SESSION:** the round-3 marathon captures **could not be recorded** (10
retries, all contended). **Their content is preserved in this git-tracked document instead** — that is
why this addendum is long. Re-attempt the captures when the lock frees.

## WHAT'S NEXT — in order

| # | step | state |
|---:|:---|:---|
| **1** | 🔴 **ENGINEER DECISION: a 4th review round, or waive and release?** Round 3's HIGH is fixed (`0cf1a2aa`) but that fix is **not itself re-reviewed**. Under `Q-GLPNETS8-01`'s letter a HIGH holds the gate | **BLOCKING — 63 commits held** |
| 2 | If waived, run `/bk-release`. Everything else is staged and green | ready |
| 3 | Re-attempt the 3 blocked marathon captures (content is in this doc) | needs the lock free |
| 4 | The 4 remaining MEDIUMs from round 2 — reason field on non-success receipts; byte cap on skipped items; contract-family validation in `bind.py`; contract compatibility in run reconciliation. Recorded follow-ups per the ruling, **not** gate blockers | unblocked |
| 5 | Cover or remove the `_confine` backstop so 15/15 is honest | unblocked |
| 6 | Split 083's mechanism into its own feature; re-score both (`Q-GLPNETS6-04`) | unblocked |
| 7 | buildkit PR: `link` no-op + the `link-spec` hint (`Q-GLPNETS7-02`); re-issue `Q-GLPNETS7-01` against `buildkit-roadmap status` | unblocked |
| 8 | `bk-flow open` the 9 claimed packets against features | unblocked |

**Do NOT author features for** the supply-chain superset, onrestart, or tidy-up — **all are already
allocated packets** on `D:/coop/yngenios-windows/sched`.

## State at hand-off

| field | value |
|---|---|
| branch | `develop`, clean, pushed at **`0cf1a2aa`** |
| develop ahead of main | **63** |
| receipt tests | **48 green** |
| mutants | **14/15 killed**, 1 published survivor |
| roadmap | round **52** · open **26** · closed **94** · reconciles |
| board | **9/9 claimable packets claimed** |
| decisions ledger | **29 rows** |
| COOP | ACK-SWEEP `20260826T1010Z` + BROADCAST `20260826T1105Z` (4 rulings) delivered |

— `gavriella` · `glpnet` · `mrun-20d9230f767b` · 2026-08-26T11:15Z

---

# 🔴 SESSION-9 ADDENDUM — 2026-08-27 · **RESUME WITH `resume marathon`**

This addendum supersedes the session-6/7/8 tables above.

## THE ONE THING TO READ FIRST

🔴 **The release is STILL held on the same engineer decision, and session 9 did not resolve it.**
Ruling `Q-GLPNETS8-01`: *"Fix the 4 HIGHs, re-review once. If that round raises only MEDIUM/LOW,
ship."* Round 3 raised a **HIGH**. That HIGH is fixed (`0cf1a2aa`) but **the fix was never itself
re-reviewed**. **68 commits are unreleased on `develop`.** A generic "release anything ready"
instruction is *not* a waiver of a specific recorded ruling — do not self-authorize it. It is
question **Q-GLPNETS9-01**, presented interactively at session-9 close.

## WHAT SESSION 9 DELIVERED

| # | item | state |
|---|---|---|
| 1 | **078 mutation survivor CLOSED** — `_confine` covered at its own boundary; both mutants killed | ✅ `c7891aa4` |
| 2 | Full suite from **repo root**: **8 failed / 773 passed / 5 skipped** — baseline held | ✅ |
| 3 | **Roadmap round 53** — sync/import/reconcile/dedupe/export, both publish legs | ✅ `e67edb51` |
| 4 | **COOP ACK sweep x5**, incl. the ACK-MANDATORY discharge with measured evidence | ✅ `20260827T015419Z` |
| 5 | Ruling **`Q-GLPNETS8-02` discharged** — shiras' JSONL files ARE published | ✅ |
| 6 | 4 session-8 captures recovered from prose into durable marathon items | ✅ |
| 7 | Takt read **FROM the lake**; 3 phase rows written back into the ducklake | ✅ partial — see HAZARD 2 |

## 🔴 THE 078 MUTATION SURVIVOR IS CLOSED — 15/15 IS NOW HONEST

`_confine`'s containment check is **unreachable through `receipt_path`/`expected_set_path`** because
`_safe_component` blocks every escape on the public path. *That is why it survived mutation on
08-26* — not because it was weak. Covered at its own boundary instead (4 tests: escape, descendant,
root-is-its-own-root, unresolvable). **Mutation-verified both ways, one guard at a time:**

| mutant | result |
|---|---|
| A — containment `raise` made unreachable (`if False`) | **killed** — escape test failed |
| B — `OSError` branch returns `candidate` silently | **killed** — unresolvable test failed |

`paths.py` diff is **docstring-only**; its `NOT TEST-COVERED` claim was itself false and was
rewritten. A module built to stop unverified claims must not carry one about its own verification.

## 🔴 FOUR TRAPS MEASURED THIS SESSION — each cost real time

1. **Running the pytest suite from `codeconv/` fabricates 12 phantom failures.** 20 failed / 761
   passed from `codeconv/`; **31/31 pass from the repo root**, same tree. All 12 are exit 5
   `_EXIT_CORPUS_UNREACHABLE` — the repo root resolves to `codeconv/`, where `tutorials/olamni`
   does not exist. **ALWAYS run `pytest codeconv/tests/` FROM THE REPO ROOT.** Proved not-mine by
   `git stash`. I reported an invalid 20-failure result before catching this.
2. **The background-task output file keeps only its LAST ~17 lines.** 6 of 20 `FAILED` lines were
   truncated off the top and I drew a conclusion from the remnant. **Redirect to a real file.**
3. **`STUCK lock` FALSE a 10th and 11th time** — holders named live: `buildkit-release`,
   `buildkit_cli.deploy 2026.08.26.1`, `codexreview --max-seconds 1800` (from `D:/BSTDEV/lang/tefl`).
   ⭐ **SHARPENED:** in the *same* report run, section 3 printed *"the lock is changing hands, so the
   registry is genuinely busy"* (6 PIDs) while section 5 printed the STUCK verdict. **buildkit
   already handles multi-holder contention correctly — only the single-long-holder path is wrong.**
   That is a one-branch fix. Feed this into `Q-GLPNETS8-04`.
4. **A live cross-repo `buildkit-deploy` makes every `buildkit-*.exe` vanish mid-command**
   (`ModuleNotFoundError: No module named 'buildkit_cli.<sub>'` / `command not found`). Transient,
   self-heals. **Do not reinstall over another lane's deploy — wait and retry.**

## 🔴 HAZARD 1 — the BK-STD-1 table STILL drops the `implemented` row (recurrence)

`scripts/roadmap_open_table.py` gives **25 not-closed**. `BK-REPORT-v1` section 1 on the same data
gives **open=26**, `BY STATE: analyzed=3 closed=94 implemented=1 promoted=16 specified=6`.
**The two standardized surfaces disagree by one, and the hidden row is `qr-link-provisioning` (067)
— the feature furthest along the pipeline.** `implemented` is a legal not-closed state. **The fix
belongs in `roadmap_open_table.py`'s state filter; the report generator is correct.** Second round
running. Always quote **26**, and say the table shows 25.

## 🔴 HAZARD 2 — Application Control BLOCKS the pinned engine; takt recording cannot run by default

`buildkit-scheduler takt-tokens` dies with
**`OSError: [WinError 4551] An Application Control policy has blocked this file`** from
`_winapi.CreateProcess`. The CLI is fine — what is blocked is the **re-exec into this target's
pinned engine `2026.08.23.7`**. **Workaround: `--engine-override ambient`** (runs `2026.8.26.2`,
durably recorded as *"engine pin DISPLACED ... The pin was NOT honoured"*).

**Strong candidate root cause for the fleet takt coverage gap** — measured from the lake this
session: **189,042,301 tokens over 372/2235 rows = 17%**; **1863 rows carry NO measurement**.

**Vocabulary split found:** the takt **writer** accepts only the 9 pipeline phases + `other`
(rejects `roadmap`/`coop`/`report`), while the **reader** renders rows named `roadmap`, `coop`,
`report`, `resume`, `restart-prep`, `session-total`. Some rows were written by a path that does not
enforce the vocabulary. Rows written this session: `implement`, `codexreview`, `other` — all
`method=unavailable`, which means **ASKED AND COULD NOT TELL**, stored as 0 *with provenance*, and
**must never be read as "used no tokens"**. This lane cannot meter its own tokens.

## TAKT — READ FROM THE LAKE, NEVER RECOMPUTED

```
19/111 steps measurable (1 declared phase, 110 derived)
plan       n=2   p50 0.19h   band 0.5-3.0h    under
implement  n=1   p50 0.57h   band 0.5-24.0h   IN-BAND
close      n=1   p50 0.00h   band 0.5-3.0h    under
other      n=15  p50 0.00h   max 3.14h        under
feature total: 4.65h over 19 measured steps (target 1.5-48.0h) -> IN-BAND
unmeasurable steps: 92 — NOT counted as zero
! plan: sources disagree (marathon_step 0.20h vs stage_transition 0.12h); spread 0.08h
```

## WHAT'S NEXT — ranked, blockers named

| # | step | state |
|---:|:---|:---|
| **1** | 🔴 **ENGINEER: `Q-GLPNETS9-01` — 4th codexreview round on `0cf1a2aa`, or waive and release?** | **BLOCKING — 68 commits held** |
| 2 | `Q-GLPNETS9-02` — 083 is at `tasks`, unimplemented + unreviewed, 3 commits unmerged. Merge artifacts to `develop`, or leave on branch? | needs ruling |
| 3 | `Q-GLPNETS9-03` — Application Control vs the engine pin: override permanently, re-pin, or fix policy? | needs ruling |
| 4 | `Q-GLPNETS9-04` — fix `roadmap_open_table.py` here, or file to the buildkit lane? | needs ruling |
| 5 | 4 remaining round-2 MEDIUMs (reason field; skipped-item byte cap; contract-family validation in `bind.py`; contract compatibility in run reconciliation) | unblocked |
| 6 | `link-spec` the 6 unbound pipeline ids; **71/120 features carry no `spec_path`** and can never bind by basename | unblocked |
| 7 | Sharpened STUCK-lock one-branch fix to the buildkit lane (`Q-GLPNETS8-04`) | unblocked |
| 8 | `bk-flow open` the 9 claimed packets against features | unblocked |

**Do NOT author features for** the supply-chain superset, onrestart, or tidy-up — all are already
allocated packets on `D:/coop/yngenios-windows/sched`.

## STANDING HAZARDS (carried forward, still true)

1. **Three+ lanes live on this host.** 18 buildkit processes across 4 deploy versions were measured
   concurrently. Check `origin/develop` and the coop root before any shared-resource write.
2. **NEVER reap on the STUCK-lock verdict.** Name the holder with
   `Get-CimInstance Win32_Process -Filter 'ProcessId=<pid>'` first.
3. **The live COOP board is `D:\coop\glpnet`** (this host owns `192.168.0.108` = `GAVRI_D`).
   The repo's `COOP/` dir and `G:\...\COOP\` are **retired husks**. Resolve via `COOP/ROOT.md`.
4. **`hostname` before any COOP write** — this is **Gavriella**, and this lane is **glpnet**.
   Sibling lanes (`qhstate`, `lejepa`, `tefl`, `yngenios-research`) share the board; **answer only
   for glpnet** and route the rest.
5. **Never parse `buildkit-roadmap status`** for counts — use the signed-export `heads` fold.
6. **Pipes mask failures**: `cmd | grep | tail` reports the *filter's* exit status.

## RESTART READINESS

- [x] Working tree clean; `develop` == `origin/develop`; **68 ahead of main**
- [x] All session work committed and pushed (`c7891aa4`, `e67edb51`)
- [x] Findings durable as marathon items, not scrollback (seq **364**, 192 outstanding)
- [x] COOP ACK sweep delivered on the live board with license sidecar
- [x] Roadmap round 53 reconciles: open 26 + closed 94 = 120
- [x] Takt read from the lake; phase rows written back
- [x] Next action identified; every blocker has a numbered engineer question

**READY FOR RESTART — resume with `resume marathon`.**

— `gavriella` · `glpnet` · `mrun-20d9230f767b` · 2026-08-27T02:20:00Z

---

# ✅ SESSION-9 CLOSE — 2026-08-27T14:10Z · **ALL FOUR RULINGS EXECUTED · RELEASE SHIPPED**

**The section above was written BEFORE the engineer answered. It is superseded here.**
All four questions in `Q-GLPNETS9-20260827T0220Z` were ruled and **executed**; ledger
`.specify/decisions/engineer-decisions.jsonl`, commit `3012fc5c`.

| id | ruling | executed |
|---|---|---|
| `Q-GLPNETS9-01` | **Waive and release now** | ✅ **`v2026.08.27.1`** — PR #231, back-merge #232, **71 commits released** |
| `Q-GLPNETS9-02` | **Leave 083 on the branch** | ✅ no action; 3 commits stay on `083-glptutorial-corpus-goldens`, pushed |
| `Q-GLPNETS9-03` | **Re-pin to 2026.8.26.2** | ✅ re-pinned to **`2026.08.26.1`** — see correction below |
| `Q-GLPNETS9-04` | **Fix BK-STD-1 here, broadcast the diff** | ✅ `6e458414`; broadcast `20260827T134611Z` |

## 🔴 THREE CORRECTIONS I OWE THE RECORD — each was wrong in the section above

1. **The BK-STD-1 cause was NOT the table's state filter.** I raised it that way. The filter
   (`roadmap_open_table.py:91`) drops only `closed` and is correct. **The loss is upstream:
   `buildkit-roadmap status` emits NO ROW for an `implemented` feature** (`status | grep -c qr-link`
   → **0**). The table was also the sole consumer of a command fleet guidance already says never to
   parse for counts. Fixed by backfilling from the signed export (`heads` ⋈ `scores` by `guid`).
   **Table now prints 26 and reconciles exactly with BK-REPORT §1.**
2. **`2026.8.26.2` was never installed in deploy-home** — it is the ambient pip package. The newest
   deploy-home version is **`2026.08.26.1`**, which is what the target is now pinned to. The block
   was specific to `2026.08.23.7`; other lanes ran `.24.4/.24.5/.26.1` interpreters fine throughout.
3. **The takt verdict was EFFORT read as ELAPSED.** Old engine: *"feature total 4.65h → in-band"*.
   New engine: **`feature ELAPSED: 100.82h (target 1.5-48.0h) -> OVER`**, with
   *"feature effort: 4.65h … NOT comparable to the per-feature target"*. Per-phase flips too:
   `plan` ELAPSED 100.82h gap 100.62h → **over**; `other` ELAPSED 94.42h gap 90.54h → **over**.
   🔴 **Any takt verdict quoted from an engine older than `2026.08.26.1` must be re-read.**

## ⭐ TWO DEFECTS FIXED UPSTREAM AND CONFIRMED LIVE HERE

- **`Q-GLPNETS8-04` DISCHARGED — the STUCK-lock diagnostic is FIXED.** It now prints
  *"PID 28636 … is STILL RUNNING — this is CONTENTION with a live process, not a stuck lock.
  Do NOT kill it; it may be a peer's long test run."* That is precisely the single-long-holder
  liveness probe recommended in the broadcast. **False 11 times in this lane; now correct.**
- **WinError 4551 remedied.** After the re-pin, `takt-tokens` runs with **no `--engine-override`
  and no Application Control block**. Lake confirmed reachable: `D:\coop\_takt-lake`,
  **1897 records for `host=gavriella`**.

## STATE AT SESSION-9 CLOSE

| field | value |
|---|---|
| release | **`v2026.08.27.1`** shipped; **0 open PRs**; `develop` 1 ahead of `main` (back-merge only) |
| engine pin | **`2026.08.26.1`** (was `2026.08.23.7`, blocked by Application Control) |
| roadmap | round **54** · **open 26 · closed 94 · reconciles 120** · dedupe 0 groups over 119 live |
| BK-STD-1 table | **26 not-closed = 3 analyzed · 1 implemented · 16 promoted · 6 specified**, 7 epics |
| marathon | `mrun-20d9230f767b` `[open]` seq **365** · steps 28/111 · outstanding **193** |
| takt (from lake) | **ELAPSED 100.82h → OVER** band; effort 4.65h over 19 measured steps; 92 unmeasurable, NOT zero |
| receipts | **52 tests green**; mutation **15/15**, no published survivor |
| suite | **8 failed / 773 passed / 5 skipped** (repo root) — the 8 are known live-build/DOTNET + migration-head |
| COOP | ACK sweep `20260827T015419Z` + BROADCAST `20260827T134611Z`, both with license sidecars |

## WHAT'S NEXT — no engineer block remains open

| # | step | state |
|---:|:---|:---|
| 1 | **078 → `codexreview` → `ship` → `close`** — the era's remaining stages; the release is out, the feature is not closed | unblocked |
| 2 | 4 round-2 MEDIUMs: reason field on non-success receipts; skipped-item byte cap; contract-family validation in `bind.py`; contract compatibility in run reconciliation | unblocked |
| 3 | `link-spec` the 6 unbound pipeline ids; **71/120 features carry no `spec_path`** and can never bind by basename | unblocked |
| 4 | Takt **phase-vocabulary split**: writer accepts 9 phases + `other` and rejects `roadmap`/`coop`/`report`; reader renders those names anyway | unblocked |
| 5 | Investigate the **100.62h `plan` gap** and 90.54h `other` gap — phases open with no step running | unblocked |
| 6 | `bk-flow open` the 9 claimed packets against features | unblocked |
| 7 | 083: implement → codexreview → ship (stays on its branch per `Q-GLPNETS9-02`); its 3 measured gates bind its tasks | unblocked |

**Do NOT author features for** the supply-chain superset, onrestart, or tidy-up — all are already
allocated packets on `D:/coop/yngenios-windows/sched`.

**READY FOR RESTART — resume with `resume marathon`.**

— `gavriella` · `glpnet` · `mrun-20d9230f767b` · 2026-08-27T14:10:00Z

---

# ✅ SESSION-9 CYCLE-2 CLOSE — 2026-08-27T17:20Z · **SECOND RELEASE CUT · TWO DEFECTS CONFIRMED FIXED**

| item | result |
|---|---|
| release | **`v2026.08.27.2`** (PR #233) — BK-STD-1 fix + roadmap rounds 54/55 + session-9 record |
| repo | **0 open PRs**, tree clean, `develop` 1 ahead of `main` (back-merge only) |
| roadmap | round **55** · open **26** · closed **94** · reconciles 120 · dedupe 0 groups over 119 live |
| BK-STD-1 table | **26 not-closed = 3 analyzed · 1 implemented · 16 promoted · 6 specified**, 7 epics |
| marathon | seq **365+** · steps 28/111 · outstanding **193** |
| takt (lake) | **ELAPSED 100.82h → OVER**; effort 4.65h/19 steps; 92 unmeasurable, NOT zero; lake 2279 rows `host=gavriella` |
| fleet tokens | **193,229,936 over 421/2895 rows = 15%**; 2474 rows carry no measurement |
| COOP | `20260827T171850Z` ACK-FULFILMENT + counter-measurement, license sidecar |

## ⭐ TWO DEFECTS CONFIRMED FIXED UPSTREAM (both verified live here)

1. **`Q-GLPNETS8-04` — the STUCK-lock verdict.** Now prints *"…is STILL RUNNING — this is
   CONTENTION with a live process, not a stuck lock. Do NOT kill it."* **False 11 times; now
   correct.** 🔴 Retire the manual liveness check **only** on a pin of `2026.08.26.1` or newer.
2. **WinError 4551.** `takt-tokens` runs with **no `--engine-override`** and **no "pin DISPLACED"**
   line. The re-pin remedy holds end-to-end.

## 🔴 COUNTER-MEASUREMENT FILED AGAINST MY OWN EARLIER ACK

I corroborated shiras' `roadmap sync` false-green family, then measured it here and **it does not
reproduce**:

| round | ledger before | after | dropped | added |
|---|---|---|---|---|
| 53 | 7011 / 561 | *untouched* | — | — |
| 54–55 | **7011 / 561** | **7011 / 561** | **0** | **0** |

The file **is** rewritten (git sees it modified) but is **byte-different, content-identical**. So
`rc=0` / "nothing refused" is **truthful here**. This does **not** refute shiras' real 450-guid
loss — it means the defect is **conditional**. **Candidate discriminator: all my rounds imported
0 new lines from 2 new files.** If the loss needs a non-empty import, the hunt narrows to the
**apply path**, not the rewrite path. Untestable here — no peer has published new lines to this repo.

## 🔴 CARRY-FORWARD CORRECTIONS (do not re-derive these wrongly)

- **Takt verdicts from engines older than `2026.08.26.1` are wrong** — they print
  `feature total 4.65h -> in-band`, reading **effort** as **ELAPSED**. Honest verdict: **OVER**
  (100.82h vs 1.5–48.0h). The `gap` column (phase open, no step running) is the real signal:
  `plan` 100.62h, `other` 90.54h.
- **BK-STD-1's cause is upstream** — `buildkit-roadmap status` omits `implemented` rows. The
  table's filter was never the bug. And **bind the export glob to this host** — a bare
  `exports/*__*.json` + `sorted()[-1]` reads a **peer's stale export** and yields a confident
  wrong number.

## WHAT'S NEXT — no engineer block open

| # | step | state |
|---:|:---|:---|
| 1 | **078 → `codexreview` → `ship` → `close`** — code is released, the FEATURE is not closed | unblocked |
| 2 | 4 round-2 MEDIUMs: reason field on non-success receipts; skipped-item byte cap; contract-family validation in `bind.py`; contract compatibility in run reconciliation | unblocked |
| 3 | `link-spec` the 6 unbound pipeline ids; **71/120 features carry no `spec_path`** | unblocked |
| 4 | Takt **phase-vocabulary split**: writer takes 9 phases + `other`, rejects `roadmap`/`coop`/`report`; reader renders them anyway | unblocked |
| 5 | Investigate the **100.62h `plan` gap** / 90.54h `other` gap | unblocked |
| 6 | `bk-flow open` the 9 claimed packets against features | unblocked |
| 7 | 083: implement → codexreview → ship (stays on branch per `Q-GLPNETS9-02`) | unblocked |
| 8 | ❓ **`/yx-bootmig` does not exist on this host** — searched glpnet, user-level skills, and the `yngenios-windows` lane; only `yx-distill` exists there. Needs the engineer to name it | **needs input** |

**READY FOR RESTART — resume with `resume marathon`.**

— `gavriella` · `glpnet` · `mrun-20d9230f767b` · 2026-08-27T17:20:00Z

---

# ✅ SESSION-9 CYCLE-3 CLOSE — 2026-08-27T18:12Z · **THIRD RELEASE · I REFUTED MY OWN HYPOTHESIS**

| item | result |
|---|---|
| release | **`v2026.08.27.3`** (PR #236) — 15 commits incl. ariellas' PRs #234/#235 |
| releases today | `v2026.08.27.1` (71 commits) · `.2` (BK-STD-1 fix) · `.3` (peer merge) |
| repo | **0 open PRs**, tree clean, `develop` 1 ahead of `main` (back-merge only) |
| roadmap | round **56** · **open 27 · closed 94 · reconciles 121** · 21 epics · dedupe 0 over 120 live |
| BK-STD-1 table | **27 = 3 analyzed · 1 captured · 1 implemented · 16 promoted · 6 specified**, 8 epics — **agrees with BK-REPORT §1** |
| marathon | seq **366** · steps 28/111 · outstanding **193** |
| takt (lake) | ELAPSED **100.82h → OVER**; era takt 4 measured / 64 unmeasurable; lake 2315 rows |
| COOP | `20260827T181053Z` SELF-REFUTATION broadcast + sidecar |

## 🔴 I REFUTED MY OWN DISCRIMINATOR — do not resurrect it

At `20260827T171850Z` I told the fleet the `roadmap sync` false-green might need a **non-empty
import**, narrowing the hunt to the apply path. **Round 56 gave me that exact test and killed it:**

| round | import | before | after | dropped |
|---|---|---|---|---|
| 53 | 0 lines / 2 files | 7011 / 561 | *untouched* | — |
| 54–55 | 0 lines / 2 files | 7011 / 561 | 7011 / 561 | **0** |
| **56** | **35 lines / 6 files** | **7011 / 561** | **7011 / 561** | **0** |

**Import emptiness is NOT the trigger.** Retracted publicly before anyone could act on it.

## ⭐ THE UNIFYING AXIS: ENGINE VERSION. CHECK YOUR PIN BEFORE QUOTING ANY NUMBER.

Three separate defects on this host were all **pin-dependent**:

| symptom | `2026.08.23.7` | `2026.08.26.1` |
|---|---|---|
| STUCK-lock verdict | **FALSE 11×** | **correct** ("is STILL RUNNING… Do NOT kill it") |
| `takt-tokens` | **WinError 4551** blocked | runs, no override |
| takt era verdict | `4.65h -> in-band` | `ELAPSED 100.82h -> **OVER**` |

🔴 The third is the trap: the old engine printed **effort** where the new prints **ELAPSED**, so an
old pin reports a 100-hour era as *in-band*. **Any takt verdict from a pin older than
`2026.08.26.1` must be re-read.** New leading hypothesis for shiras' 450-guid loss is also engine
version — testable in one command (`buildkit-deploy list`, then `latest`, then re-measure).

## ✅ THE BK-STD-1 FIX IS ROBUST

Round 56 grew the catalog to **21 epics / 121 features** and introduced a **state value the fix had
never seen (`captured`)**. The export-backfill carried both: table and BK-REPORT §1 now agree at
**27** with no hand-reconciliation. A peer independently captured the same defect as feature
`renderers-read-export-fold-not-status` — corroboration, not duplication.

## ❓ `/yx-bootmig` — CANNOT BE RUN, AND HERE IS WHY

Asked twice; searched exhaustively. It exists **only as a spec**:
`D:\BSTDEV\research\yngenios\specs\008-yx-bootmig-base\` (just `BRIEF.md` + `DESIGN.md` — no plan,
no tasks, no code). Its own brief says it **"Delivers: the `/yx-bootmig` skill + the Python tool
under it"** — the skill is the *output* of an unbuilt feature — and records
**"Epic: `bootstrap-migration` — does not exist yet, must be created."** No installed `*bootmig*`
skill exists under any `skills/` dir on `C:` or `D:`; the only `yx-*` skills are `yx-distill`,
`yx-appbuilder`, `yx-linbuilder` in the `D:\yngenios\*` repos. **It also belongs to a different
repo and lane** (`D:\BSTDEV\research\yngenios`), so building it is a full pipeline run there — not
a command in glpnet.

## WHAT'S NEXT — no engineer block open in this lane

| # | step | state |
|---:|:---|:---|
| 1 | **078 → `codexreview` → `ship` → `close`** — code released, FEATURE not closed | unblocked |
| 2 | 4 round-2 MEDIUMs (reason field; skipped byte cap; contract-family validation in `bind.py`; contract compatibility in run reconciliation) | unblocked |
| 3 | `link-spec` the unbound pipeline ids; **72/121 features carry no `spec_path`** | unblocked |
| 4 | Takt **phase-vocabulary split** (writer takes 9 phases + `other`; reader renders `roadmap`/`coop`/`report`) | unblocked |
| 5 | Investigate the `plan` **100.62h** / `other` **90.54h** gaps | unblocked |
| 6 | `bk-flow open` the 9 claimed packets | unblocked |
| 7 | 083 implement → codexreview → ship (stays on branch per `Q-GLPNETS9-02`) | unblocked |
| 8 | `/yx-bootmig`: engineer to decide whether the yngenios lane builds feature 008 | **needs input (other repo)** |

**READY FOR RESTART — resume with `resume marathon`.**

— `gavriella` · `glpnet` · `mrun-20d9230f767b` · 2026-08-27T18:12:00Z
