<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART BRIEF — `olamnit.glpnet` · run `mrun-b7b5fa047190` · **rev 7** · 2026-09-07

**Resume with:** `resume marathon`
**Host:** OLAMNIT · **Branch:** `develop` (109 is SHIPPED — no feature branch to return to)
**Supersedes `RESTART-mrun-b7b5fa047190-olamnit-glpnet-20260906-rev6.md`.**
Trust `git log --oneline -1` over any hash written here.

---

## 0 · WHAT SESSION 15 DID

**Feature 109 SHIPPED and RELEASED as `v2026.09.06.5`** — PR #322 (feature→develop) → #323
(release→main) → #324 (back-merge), all merged, tag verified on origin. All nine pipeline stages
ran. Roadmap: `released`.

| # | delivered | evidence |
|---|---|---|
| 1 | **109 US1** — the differential harness | suite `Section Y`; `scripts/differential_gate.py`; 595 → **604/604 executed, 0 failures** |
| 2 | **T058 executed reversion** — the real C# fix reverted, rebuilt, measured DIVERGE, restored, measured AGREE | `.specify/differential/reversion-20260906.md` |
| 3 | **`/bk-codexreview`: 21 findings, 6 high — ALL fixed, no deferrals** | `scripts/tests` 63 → **118** |
| 4 | **Four engineer rulings** taken via `AskUserQuestion` | §5 |
| 5 | **P0 broadcast: 6 of 8 PBFT members equivocate in term 3** | broadcast `20260906T2145Z` |
| 6 | **P0 broadcast: OB-8's verify step is broken on Windows** | broadcast `20260907T0010Z` |
| 7 | Board: 67 not-closed, **0 captured, 0 refined** — every row scored and promoted or beyond | `scripts/roadmap_open_table.py` |

---

## 1 · 🔴 THE TWO FINDINGS TO CARRY FORWARD

### 1.1 Six of eight PBFT members equivocate, and the tally hides it

Measured 2026-09-06T21:38Z against `D:\coop\ynet` (113 pbft records, 0 quarantined) on **two
engines** — `origin/develop@3b10b85f` and `547a3fc0` — with identical results.

Term 3: **16 prepares from 8 actors → 8 counted, 8 silently dropped, `discarded: {}`.** Six of the
eight actors prepared for **two different candidates in the same term**. The rule is
`prepares.setdefault(...)` over timestamp-ascending records — **first-prepare-wins, written down
nowhere.** Under last-wins the same records give `QuorumUnattainable` instead of `Decided`.

**ENGINEER RULING TAKEN: discard the equivocating actor for that term, and report every drop.**
Consistent with `OB-5`/`Q99=a` (discard the vote, never void the term). **Owner
`@shiras-olamnit`** (`tools/ynet`, `Q59`) — this lane raises, does not patch.

### 1.2 OB-8's verify step reports DIFFERS on a byte-identical file, on every Windows host

Measured 2026-09-07T00:05Z on this repo's copy of the ruled template:

```
worktree 38983 bytes  sha a23f7be9…      |  483 lines, 483 CRs, delta exactly 483
committed 38500 bytes sha 528611d722e269ac  <- matches OB-8's figure for GLPNET exactly
sha256(worktree | tr -d '\r') = 528611d722e269ac   IDENTICAL
```

**OB-8's numbers are right.** But its remedy step (b) — *"every lane verifies and reports
MATCHES/DIFFERS"* — hashes the CHECKOUT, which is CRLF on Windows. Three of four hosts are
Windows. **Run it as written and the fleet manufactures the fork it is trying to measure.**

Fix, one line: `git show HEAD:<path> | sha256sum` (the stored object, LF everywhere), or
`tr -d '\r' < <path> | sha256sum`.

---

## 2 · ✋ THREE THINGS THIS LANE GOT WRONG AND CORRECTED ITSELF

1. **I authored an 8th plan document.** `FLEETWIDE-TACTICAL-ACTION-PLAN v5.0` was broadcast at
   22:35Z **before I had read OB-8**, which forbids exactly that until remedy step (a) lands.
   **Stood down as a rival document** at 00:10Z. The *mechanism* (`docs/fleet/plan/plan_crdt.py` —
   grow-only per-actor op log, add-wins ACKs, actor-mismatch refusal, a losslessness `check` that
   exits 2 and caught a real loss on its first run) is offered to the ruled stream
   `docs/fleet/ftap/ftap.crdt.jsonl`; the text is not.
2. **My quorum denominator was wrong.** I told the engineer the roster is 15 lanes and recommended
   11/15. **`Q80=a` rules it 60 — 4 hosts × 15 — with the bar ≥45.** The engineer's 45 was right.
   The answer he gave rests on my malformed question and this lane is not acting on it.
3. **OB-9 applies to me.** v5.0 claimed losslessness against a directive stored nowhere. **An
   unstored source makes every losslessness claim unverifiable by construction**, mine included.

---

## 3 · 🔴 THE DEFECT CLASS THIS SESSION KEEPS FINDING — grep your own suite for it

Three of the six new Section Y checks used a success sentinel that is a **substring of their own
failure string**: `grep -q AGREE` matches `DISAGREE`; `ACCOUNTED` matches `UNACCOUNTED`;
`CONSISTENT` matches `INCONSISTENT`. All three passed unconditionally. `X-4`, inherited from 108,
had it too.

**And V-26 — the regression control I wrote for this feature's own freshness fix — compared a value
against the mtime of a file INSIDE the directory that value is computed over.** It held by
construction and could not fail in any state, including the exact reversion its comment claimed it
would catch.

The fix that removes the class rather than the instances is **`check_exact`** (equality), not five
renamed sentinels. **Grep every suite in the fleet for a `check`-style helper whose success token is
a prefix of its failure token.**

Related, and separately broadcast: a build-freshness gate that stats `glp_repl.exe` measures the age
of a **.NET apphost stub an incremental build does not rewrite**. Date a build from the newest file
in its **output directory**.

---

## 4 · WHAT IS **NOT** DONE, AND WHY

| item | state | reason |
|---|---|---|
| **SC-003 — a live refusal in an adopted area** | **NOT MET, disclosed in `spec.md`** | The only always-non-conforming surface sits in area `coop`, declared **non-adopted**. Flipping it would assert an adoption this lane has not performed. Carried as roadmap feature `sc003-live-refusal-in-an-adopted-area` (WSJF 5.33). |
| **The audit widening** | **NOT DONE, disclosed in `spec.md`** | `scoped_regions` is still byte-identical to `develop` — five regions. ~477 sites need ~477 dispositions at once, and defaulting them is how 25 surfaces came to claim `owned` falsely. Carried as `audit-widening-codeconv-and-remaining-csharp` (WSJF 3.00). |
| **Feature 110 `[03]` YQuery/DuckLake** | **NOT STARTED, BLOCKED** | Its conformance evidence must be measured against a real Postgres node (`Q-olg17-04`). `com.docker.service` is **Stopped (Manual)** and `Olamnit\smbuser` is **not in `docker-users`** (only `Olamnit\gavri`). **Both need administrator rights.** OLAMNIT is **dormant, not bare**: 26.7 GB of PG18 Docker data survives at `D:\pgdata\pg-node-{a,b}` — **assess those two clusters, do not provision over them.** |
| **`/bk-close` retrospective for 109** | **NOT RUN** | Ship and release completed; the close-out retrospective did not. Run `buildkit-close` (or `/bk-close`) against `differential-cross-runtime-acceptance-gate` first thing. |

🔴 **All four are DISCLOSED, not silent.** The standing peer ruling (`shiras-tefl`,
2026-09-04T23:55Z) is that a disclosed gap is not cheating; concealment is.

---

## 5 · ENGINEER RULINGS FROM THIS SESSION (`AskUserQuestion`, all four answered)

| ruling | decision |
|---|---|
| **109 disposition** | **Ship with both gaps disclosed**, each opened as its own roadmap feature — done |
| **`declared-unproven`** | **Ratified as a fourth tier; FR-019 amended** — done, `7b6fd6ec` |
| **Plan quorum** | answered 11/15 — **NOT acted on; the question was malformed (see §2.2)** |
| **PBFT equivocation** | **Discard the equivocating actor for that term, and report the drop** — for `@shiras-olamnit` |

---

## 6 · WHAT'S NEXT, IN ORDER

1. **`git fetch origin` FIRST** (`C-19`). Several lanes push this repo; two peer tags landed
   mid-session and a second `specs/109-*` directory arrived on develop.
2. **`/bk-close` feature 109** — the one pipeline stage that did not run.
3. **Next single-feature era, board candidate:**
   **`cross-runtime-link-parity-intermittent-empty-list`** (WSJF 5.00, RICE 28800) — a C# consumer
   returns `Got = []` and prints `succeeds`, green in 2 runs of 3. **It is 109's vacuous-agreement
   defect at link level**, so this lane's proven method transfers directly, and its regression bar
   is an **ITERATED** run (≥20), never a single green. Alternative: `sc003-live-refusal-in-an-
   adopted-area` (WSJF 5.33), which closes 109's own disclosed gap.
4. **Ask the engineer for the two administrator actions** if 110 `[03]` is wanted: add
   `Olamnit\smbuser` to `docker-users`, and start `com.docker.service`.
5. **Re-ask `@gavriella-glpnet` for the literal `space_id`** (`Q-olg15-04`: do not mint one).

---

## 7 · STANDING RULINGS AND ENVIRONMENT

- **`Q-olg15-09`** 108 is ONE sibling to 078; **do NOT re-open 078.** FR-013's extraction is a
  behaviour-identical move. 🟡 *One declared exception this session:* `record()` gained a `now`
  parameter mirroring `applies()`, so the new "expiry must be in the future" check works under a
  pinned clock. Additive; two 078 fixtures updated; 79 faultinj tests green.
- **`Q-glpnetshiras-50`** `YngeniOS.Ynet.Client` is canonical; this lane authors no client.
- **`Q59`** `tools/ynet` is `@shiras-olamnit`'s. **`R-S5-04`** `[04]` is `@shiras-glpnet`'s.
- **`Q80=a`** fleet roster is **60** (4 × 15); quorum bar ≥45. **Not 15.**
- 🔴 **The classifier is intermittent. RETRY BEFORE ESCALATING.** Confirmed across five sessions.
- 🔴 **Heredocs mangle escapes in this shell** — it bit twice more this session and broke a
  broadcast. **Write patch scripts and long documents with the Write tool.**
- 🔴 **Never read `$?` through a pipe.** Both the audit and the differential gate warn when stdout
  is not a terminal. Run bare.
- `dotnet` at `C:\Users\smbuser\AppData\Local\Microsoft\dotnet`, **not on PATH.**
- `DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart.exe`, **not on PATH** — export before the suite.
- Use `codeconv/.venv/Scripts/python.exe` for repo scripts; **`scripts/roadmap_open_table.py` needs
  the buildkit venv** `/d/bstdev/research/buildkit/.venv313/Scripts/python.exe` (it imports
  `buildkit_cli`).
- 🔴 **Rebuild the Debug C# REPL** before trusting the suite:
  `dotnet build out/csharp/glp_repl/glp_repl.csproj -c Debug`.
- Coop: `/d/coop`, 47 channels, written three times this session.

---

## 8 · RESTART CHECKLIST

1. `resume marathon`
2. `git fetch origin --tags` — expect movement; several lanes push this repo.
3. `git checkout develop && git pull --ff-only` — **109 is shipped; there is no feature branch.**
4. `buildkit-marathon status --feature differential-cross-runtime-acceptance-gate`
   (run `mrun-b7b5fa047190`).
5. Read **§4** (what is NOT done), **§1** (the two P0s), **§2** (what this lane got wrong).
6. Rebuild the Debug C# REPL, then run the suite bare.

---

## 9 · BASELINE

| | session start | session end |
|---|---|---|
| REPL suite | 595/595 executed, 0 fail, 2 named not-run | **604/604 executed, 0 fail**, same 2 named not-run |
| `scripts/tests` | 63 | **118** |
| codeconv faultinj | — | **79 passed** |
| evidence-signal audit | exit 1 · 7/7 checks · 0 errors | exit 1 · **9/9 checks** · 0 errors · 0 refusals |
| differential gate | did not exist | **1 criterion MEASURED-AGREE**, exit 0, control executed |
| board (not closed) | 55 | **67 · 0 captured · 0 refined** |
| git | 3 unpushed commits | **clean, 0 ahead, 0 behind** |
