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

| # | step | size | state |
|---:|:---|:---|:---|
| 1 | **078: fix the 2 TEST findings** (`tests/faultinj/conformance.py`, `test_guard_weakening.py`) | mini/7 | **unblocked — DO THIS FIRST.** Ruled: this discharges the era gate AND unlocks the 9 claims |
| 2 | **Claim the 9 allocated packets** + ACK-COMPLIANCE to ariellas | micro/3 | unblocked once #1 lands (`Q-GLPNETS6-02`) |
| 3 | 078: the 6 product HIGHs | midi/11 | follows #1 |
| 4 | re-run `/bk-codexreview --scope codeconv`, then `/bk-release` | midi/11 | gated on #1+#3 (`Q-GLPNETS6-01`) |
| 5 | Split 083's mechanism into its own feature; re-score both | mini/7 | unblocked (`Q-GLPNETS6-04`) |
| 6 | buildkit PR: fix `link` no-op + the `link-spec` hint | mini/7 | unblocked (`Q-GLPNETS7-02`) |
| 7 | Re-issue `Q-GLPNETS7-01` against `buildkit-roadmap status` | nano/1 | needs engineer |

**Do NOT start:** any feature for the supply-chain superset, onrestart, or tidy-up — **all are
already allocated packets** (see the headline).

## Restart readiness

- [x] Tree clean, all work committed **and pushed** (`d1e07fb8`)
- [x] Zero open PRs
- [x] 7 findings durable in marathon items, not scrollback
- [x] 6 engineer rulings recorded and citable
- [x] COOP ACKs + broadcast delivered; ACK-LEDGER row filed
- [x] Next action identified and unblocked (**078's two TEST findings**)

**READY FOR RESTART.**

— `gavriella` · `glpnet` · `mrun-20d9230f767b` · 2026-08-25T13:00Z
