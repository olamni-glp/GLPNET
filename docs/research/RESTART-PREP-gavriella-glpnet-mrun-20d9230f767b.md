<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART PREP — resume with `resume marathon`

🔴 **Trap 13: never select a restart document by filename.** This table identifies the run. If
these four fields do not match your session, this is not your document.

| field | value |
|---|---|
| **run_id** | `mrun-20d9230f767b` |
| **lane** | `gavriella` |
| **host** | `GAVRIELLA` |
| **repo** | `GLPNET` (`D:\BSTDEV\research\GLP\GLPNET`) |
| feature | `078-verification-receipts` |
| written at | **2026-08-24T18:3xZ (session 5 close)** |

## Resume in one line

```
buildkit-marathon resume --feature 078-verification-receipts
```

🔴 **`--feature` is mandatory** — there is no `.specify/feature.json` in this repo, by design.

🔴 **Do NOT use `glpnet-full-completion-programme`.** That name resolves to `mrun-f5ef56dba3c1`,
the **ariellas lane's** run, absent from this machine's store. `docs/current_plan.md` is the
ariellas pointer, not this lane's. Marathon state is per-machine and out-of-repo.

🔴 **Run buildkit commands SERIALLY.** See the STUCK-lock entry below — it fired twice more today.

## 🔴 SESSION-5 GAP: SEVEN marathon captures did NOT land, and the reason is contention, not content

**Everything session 5 found is durable in tracked files (this doc + the codexreview write-up +
the COOP sitrep). What is missing is the marathon's own `capture` rows — seven of them.**

Two causes, both worth knowing:

1. **My error, now fixed in the script:** I invented `--kind finding` / `--kind decision`.
   The allowed set is **`bug, idea, issue, latent-requirement, missing-prerequisite`**. Lock
   contention hid this for the whole session — the first call that actually reached the CLI
   rejected it in under a second.
2. **The real blocker — the registry lock is effectively never free on this host.** Across two
   runs and **~50 minutes of retrying (69 lock refusals), ZERO captures landed.** A concurrent
   buildkit session runs `pytest tests/roadmap -q` and `pytest tests/scheduler tests/refine -q`
   more or less continuously, and **any** such run holds
   `deploy-home/registry/pgdb/.lock` for its whole duration.

> **So `buildkit-marathon capture` is unavailable on this host whenever another session is running
> its test suite.** That is an architectural contention problem, not a transient. Do not plan a
> session around landing marathon rows while a sibling session is testing — **write findings to
> tracked files first and treat the marathon row as best-effort.**

**To re-land the seven rows next session** (a ready-made retry driver, kinds already corrected):
`…/scratchpad/captures.py` from session 5 — or simply re-derive them from this doc, which carries
every one of them. Their subjects: the codexreview unblock + NO-GO · SCHED-R1/R4 already shipped ·
SCHED-R4 discharged with the 252-unresolvable caveat · the dropped-`implemented` table defect ·
the STUCK-lock defect · the onboard + escalation · the two merges + the standards fork.

---

## 🔴 THE HEADLINE: the release gate is no longer a tool block. It is a NO-GO.

Session 4 recorded, in bold, that `/bk-codexreview` **cannot be discharged on this host** and that
**no release can be cut** until buildkit fixed one of two defects. **Both halves of that are now
superseded.**

1. **Defect 2 is root-caused — and it is in git, not buildkit.** `scope.resolve_path` runs
   `git ls-files -- <path> <8 × :(exclude)…>`. On **git 2.55.0.windows.3** two of those excludes —
   `:(exclude)**/*.map` and `:(exclude)reviews/**` — each **independently empty a nested pathspec
   they cannot possibly match**. buildkit's `empty_scope` refusal is honest; its input is wrong.
2. **The working route:** use a **single-component (repo-root) directory** as `--scope`. Measured:
   `codeconv` → **332 files** with all 8 excludes; `codeconv/src` → 0; `docs/research` → 245.
3. **The review then RAN** — run `20260824T165651Z`, exit 0, not timed out — and returned
   **10 findings, 8 HIGH**, all on the 078 receipts module itself.

**So: do not retry the "is codexreview broken" investigation. It works. The blocker is now the
review's own verdict**, which is a much better problem to have. Full write-up, tracked on
`develop`: **`docs/research/codexreview-unblocked-and-078-no-go-2026-08-24.md`**.

> ⚠️ **Count caveat, do not drop it:** `run.json` says `findings_count_status: "unconfirmed"`,
> `prose_fallback_findings: 10`. codex returned **prose, not structured JSON**, so 10 is a parse
> fallback. The individual findings are the evidence; the total is approximate.

### The 8 HIGH findings ARE the next implementation slice for 078

| file:line | defect |
|---|---|
| `receipts/consumer.py:73-74` | accepts a PASS receipt from **another check / area / prior run** — no run ID in either model |
| `receipts/receipt.py:162-170` | validation has **no PASS branch**; PASS with an unresolved target validates |
| `receipts/receipt.py:157-161` | enforces only `examined ≤ total`; **FR-010 requires examined + skipped ≤ total** |
| `receipts/manifest.py:72-80` | `expected.json` that is `{}` / empty / **run_id-mismatched** is accepted as an empty expected set |
| `receipts/manifest.py:88-90` | run reconciliation trusts a **filename**, never loads the sidecar |
| `receipts/override.py:66-73` | `applies()` ignores the recorded **reason** — one override authorises every other refusal |
| `tests/faultinj/conformance.py:61-68` | fixture reaches `passed == len(_CASES)` **without exercising the declared BOUNDED case** |
| `tests/faultinj/test_guard_weakening.py:22-27` | the mutation test **stays GREEN under a no-op validator** — the inverse of SC-007 |

🔴 **Fix the two TEST findings first.** While a conformance fixture reports full coverage without
running a declared case, and a mutation test stays green when its guard is removed, **every green
run this repo produces is uninterpretable — including the runs that would certify the other six
fixes.** Instruments before readings.

---

## State at hand-off

| field | value |
|---|---|
| branch | `develop`, clean, pushed at **`4f7c68b9`** |
| marathon | run open, seq 338+, feature `078-verification-receipts` |
| develop ahead of main | **93** |
| open PRs | 0 |
| unmerged origin heads | **5** (was 7) — all engineer-gated or archive |
| board | `D:/coop/glpnet/sched` — 32 WPs: backlog 23 · ready 3 · in-progress 4 · done 1 · escalated 1 |
| roadmap | round 48 done; **25 not-closed** (see the count defect below), 6 epics with open work |
| regression gate | ✅ **561 / 559 passed / 2 failed / 0 skipped** — re-run this session over BOTH merges, **identical to baseline, zero regression**. The 2 are the known pre-existing `Section T` 064 service-box drills (T-1 US1 resume, T-2 US2 history) |

## Delivered this session (session 5)

| item | result |
|---|---|
| **SCHED-R4** | ✅ **DISCHARGED** — `stock-edges` projected **27 of 279** deps (6 confirmed / 21 heuristic / 0 cycles). `edge_coverage` off 0.0 |
| **SCHED-R1** | ⭐ **premise corrected — already shipped upstream**, not a maxi/17 build |
| **onboard** | ✅ 35-day 3×8h calendar, verified by content: 38 full days, 00:00/08:00/16:00, to 2026-09-27 |
| **codexreview** | ⭐ **unblocked + root-caused + run** → NO-GO |
| merge `091-bkstd1-round42` | ✅ `2b0f9122` clean — brings **bk-flow + bk-proof skills** and roadmap round 47 |
| merge olamnit tidy-up | ✅ `6a261b1d` — 2 add/add conflicts resolved to develop |
| **TIDY-Y15** | ✅ **discharged by ariellas** — `.claude/skills/bk-flow/SKILL.md` arrived on the 091 merge. **Do not author a competing one** |
| roadmap round 48 | ✅ import 0 new / reconcile in-sync / dedupe 0 groups over 118 live / export 20/119/3823, both legs |
| engineer brief | ✅ **10 blocks** published — `claude.ai/code/artifact/77dcfcf1` |

## 🔴 Corrections carried forward (do not re-derive)

0. **NEVER sum raw `kind=stage` seconds from the takt lake — it double-counts.** `emit_stage` has
   no idempotence on `(feature, phase, seconds)`. Quote the **marathon** figure per feature:
   **4.65 h over 19 measured steps**, band 1.5–6.0 h, in-band — with **78 of 97 steps unmeasurable
   and NOT folded in as zero**.
1. **The BK-STD-1 open table DROPS `state=implemented`.** Export fold = **25 not-closed**
   (94 closed · 15 promoted · 6 specified · 3 analyzed · **1 implemented**); the renderer prints
   **24**. The hidden row is **`qr-link-provisioning` (067)** — the feature under an open
   graduation ruling. ariellas filed this on branch 091; **I corroborated it by measurement.**
2. **Epic heads carry NO `state` field** — all 20 are `None`. **No lane may state an epic state
   count**; the export cannot support one.
3. **SCHED-R2's `complete` mark on this run is FALSE and cannot be undone.** Report **points**,
   never steps-done/total.
4. **My "3+ path components are emptied" rule is WITHDRAWN** — measured with one exclude, false
   under all eight (`docs/research` is 2 components and survives; `codeconv/src` is 2 and does not).
5. **The dropped Y11 branch carried a complete 078 MVP** — tag
   `archive/backup__078-olamnit-impl-preserve-20260820`. Already merged; the codexreview above is
   *of that code*.

## 🔴 The STUCK-lock verdict was FALSE a 4th and 5th time — and this time I identified the holder

`buildkit-marathon` refused repeatedly with *"PID 38152 held it on ALL 61 attempts and never
changed — that is a STUCK lock, not contention."* It was **alive the whole time**:

```
Get-CimInstance Win32_Process -Filter 'ProcessId=38152' | Select-Object CommandLine
→ python.exe -m pytest tests/roadmap/test_link_refusals.py … -q
```

A **live pytest run from another buildkit session**, 80 s CPU. **Use that one command** — it names
the holder outright, where `Get-Process` only proves existence. **Never reap on the STUCK verdict.**

🔴 **Then it happened again with a DIFFERENT holder — a 6th false verdict.** When 38152 finally
exited (confirmed via `Get-Process`), the very next attempt reported *"PID **416** held it on ALL 61
attempts and never changed — that is a STUCK lock"*. `Get-CimInstance` named it immediately:
`python.exe -W ignore -m pytest tests/scheduler tests/refine -q`, started 18:12. **Two independent
holders, both live, both reported as STUCK.** The verdict has now been wrong 6 times and right 0.
Treat it as meaning **"busy"** and nothing more. A low PID (416) is *not* evidence of a recycled or
stale PID — check, don't infer.

*Fix owed upstream:* the lock message should carry the holder's command line, and "STUCK" should be
reserved for a PID `Get-Process` cannot find.

## TAKT DuckLake — required config on this host

`config.local.json` (gitignored) MUST carry — **verified present today**:

```json
{ "sched_root": "D:/coop/glpnet/sched",
  "takt_lake_root": "D:/_takt-lake",
  "takt_lake_fleet_root": "D:/coop/_takt-lake" }
```

🔴 Without `takt_lake_fleet_root` the tool defaults to `I:\coop\_takt-lake`, **not mounted here**,
and every fleet write fails **silently**.

---

## What's next — in order

| # | step | size | state | blocked-by |
|---:|:---|:---|:---|:---|
| 1 | **078: fix the 2 TEST findings** (`conformance.py`, `test_guard_weakening.py`) | mini/7 | **unblocked** | — do this first; they make every green run uninterpretable |
| 2 | **078: fix the 6 product HIGHs** | midi/11 | **unblocked** | run ID identity · PASS branch · FR-010 skipped · expected.json · manifest sidecar · override reason |
| 3 | re-run `/bk-codexreview --scope codeconv` and, on GO, `/bk-release` | — | **unblocked** | the route is proven; 93 commits are waiting |
| 4 | **TIDY-Y14** C2 remote cleanup | mini/7 | **unblocked** | must run LAST of the Y-series; 5 heads remain, all gated |
| 5 | **SCHED-R7** bind WPs to features | midi/11 | **unblocked** | 1 of 32 WPs resolves to a feature; **dependent of R1, which is now shipped** |
| 6 | Y16 / Y17 / Y18 | midi/maxi/midi | **unblocked** | era metric · unique allocation · takt-only durations |
| 7 | **B1–B10 engineer rulings** | — | **GATED** | `claude.ai/code/artifact/77dcfcf1` — B1 and B8 gate the most |

**Do NOT start:** `/bk-clarify 082` (no `feature_pipeline` row; would evict 078 from the single
active slot — see block B7), Y06/Y07/Y09 (engineer rulings owed), Y02 (peer-owned).

## Engineer questions are asked in BK-STD-2 shape

`BK-STD-2` (ariellas', adopted unchanged) is the fleet question format. There is **no precoded
template file anywhere** — that absence is established and not worth re-searching.

## Evidence caveats

- **Quote takt bands with any takt figure** (trap 10).
- **Owner coverage in `.specify/roadmap-owners.json` is 2 of 24 rows**, each backed by a durable
  op. The other 22 are deliberately undeclared — a guessed owner is how the 077 duplicate
  allocation happened. 🔴 **olamnit's branch carried an EMPTY `{}` for this file**; the merge
  resolved to develop's. If it ever reads `{}` again, that is a regression, not a reset.
- **Pipeline-derived counts are not comparable across lanes** (trap 12); only roadmap counts are.
- **Name the root/ref with every board number, branch count or ahead-count** (binding rule 5).
