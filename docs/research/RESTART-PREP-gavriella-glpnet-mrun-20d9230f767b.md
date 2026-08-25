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
| written at | **2026-08-25T13:00Z — SESSION 6 CLOSE** |

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
