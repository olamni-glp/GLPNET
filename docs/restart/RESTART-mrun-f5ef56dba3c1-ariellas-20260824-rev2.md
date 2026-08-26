<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# SAFE-RESTART PREP · rev2 — `mrun-f5ef56dba3c1` · ariellas / glpnet · 2026-08-24T22:35Z

**Resume phrase in the new session: `resume marathon`** — nothing else is needed.
Supersedes rev1 (`RESTART-mrun-f5ef56dba3c1-ariellas-20260824.md`).

---

## 1 · Objective position

```
mrun-f5ef56dba3c1 · feature glpnet-full-completion-programme
seq 297+ · steps 40/91 · outstanding items 144 · open (in_progress)
roadmap: 20 epics · 120 features · 26 open · 94 closed   (reconciles 26+94=120)
```

🔴 **`buildkit-marathon` MUST be given `--feature glpnet-full-completion-programme`.** The bare
command resolves `.specify/feature.json` (→ `085-onrestart-fleet-resume`) and **falsely reports
"no active marathon run"**. Single most likely trap for a fresh session.

## 2 · Standard tooling — USE THESE, do not hand-render

| purpose | command |
|---|---|
| **full standardised report** (roadmap → progress → status → sitrep → takt → next) | `python scripts/BK-REPORT-v1-generator-20260823.py all --feature glpnet-full-completion-programme` |
| not-closed roadmap table | `python scripts/roadmap_open_table.py` |
| takt (also read from the TAKT DuckLake) | the `takt` section of BK-REPORT-v1 |

- Canonical formats: `docs/SITREP-FORMAT.md` · `.specify/STANDARD-SITREP-AND-ROADMAP-TABLE-v1.md` (buildkit repo)
- Engineer questions: **BK-STD-2**, `I:\coop\BK-STD-2-ENGINEER-QUESTION-TEMPLATE.md`, adopted verbatim
  (+ gavriella's A1 `cost: band|measured,n=k` and A2 `ORIGIN: superseded-by-evidence`)
- **TAKT DuckLake**: `I:\coop\_takt-lake` — 809 records; **naive read now works** (no `union_by_name` needed)
- CO lake (different store, do not confuse): `.specify/co-lake`

## 3 · Delivered this session

| item | evidence |
|---|---|
| **RELEASE `v2026.08.24.1`** — develop 100 ahead → **1**; PR #226 merged, tagged, back-merged; 0 open PRs | Q01 |
| **T16** — 2 C: scratchpad clones deleted, ~103 MB, preservation verified first | marathon checkpoint |
| **T18** — `bk-flow` + `bk-proof` **installed** (premise corrected: install, not authoring); verified live | `88174d1b` |
| **083 FR-002 RULED (b)** record-the-rejection → FR-009 **in scope**, B02 unblocked, B10 confirmed | `88174d1b` |
| **Takt lake normalised** — 4 legacy JSON `reason` files (all mine) → VARCHAR; originals md5-preserved; naive fleet read fixed | `D:\BSTDEV\evidence\takt-lake-schema-normalise-20260824\` |
| roadmap rounds 47 + 48; BK-REPORT-v1 + ruled table adopted | PR #228 |
| 2 features raised: `takt-and-token-persistence-to-ducklake`, `renderers-read-export-fold-not-status` | roadmap |
| BK-STD-2 questions Q01–Q04, all decided | `docs/questions/QID-glpnet-ariellas-Q01-Q04-20260824.md` |

**Open PR**: #228 (`091-bkstd1-round42` → develop) — round 48 + generators + questions.

## 4 · 🔴 Two of my own claims were WRONG and are retracted — do not rebuild on them

1. **"The ruled renderer has a `{promoted,specified,captured}` whitelist"** — **FALSE.** The renderer
   is faithful (`line 91` skips only `closed`). **`buildkit-roadmap status` emits no row for
   `implemented` features.** Mechanism in `_cmd_status` **NOT located**; `implemented` is a legal
   state and the feature has a valid epic, so both obvious hypotheses are refuted. **Do not guess a
   third.** Filed to buildkit lane.
2. **"The ariellas lake has ZERO takt rows"** — **FALSE, wrong store.** I measured `.specify/co-lake`.
   The takt lake is `I:\coop\_takt-lake`. Tokens **are** recorded: **17,728,085 over 43/569 rows,
   coverage 8%** — a coverage gap, not an absence.

Both retracted fleet-wide (`20260824T203000Z`, `20260824T223000Z`). A third correction: the 4
divergent takt files were **mine**, not olamnit's.

## 4a · 🆕 ZA-SERIES LANDED — the specified-features completion spine

**20 durable steps** added to this run (`91 → 111`), parent item
`mitem-01a035f2-a1a3-778a-9e5f-9ae17bdfdf3e`. Plan:
`docs/research/specified-completion-crdt-plan-ZA-series-ariellas-2026-08-24.md`.

**All six `specified` features already have code on `origin/develop`** — verified with
`merge-base --is-ancestor`: `8a83bfc2` (083) · `fb038d11` (079) · `3037f155` (085) ·
`78c056a4` (080). **The stall is in the record, not the work** — but `close` must NOT be reached by
stamping the record: that code never passed `/bk-codexreview`, which is the exact class feature 078
exists to eliminate. Every ZA spine routes through `codexreview` before `ship`.

🔴 **COORDINATION**: gavriella's `mrun-20d9230f767b` holds Z00–Z08 for the **same six**.
Proposed split broadcast (`20260824T231000Z`): **ariellas takes 083 + 079** (no gates);
**gavriella keeps 080/082/085/065** (their Z-series carries those gates).
**Until ACKed, this lane starts ONLY ZA00/ZA01/ZA08 and touches none of the gated four.**

Four gates owed: **G080** (Udi §1.14 — `UnifyFail` vs `CompileError`) · **G085** (homing) ·
**G082** (fold + **no `feature_pipeline` row**) · **G065** (G2/FR-008).

## 5 · WHAT'S NEXT — ranked, blockers named

| rank | step | size | state | blocked-by |
|---:|---|---|---|---|
| 0 | **ZA18** broadcast lane split | nano 1 | ✅ **DONE** | — |
| 1 | **ZA00** reconcile the record for all six | micro 3 | ✅ **UNBLOCKED** | — |
| 1 | **ZA01 / B02** — 083 `/bk-plan` | midi 11 | ✅ **UNBLOCKED — START HERE** | — (FR-002 ruled) |
| 1 | **ZA08** — 079 record the skipped clarify | nano 1 | ✅ **UNBLOCKED** | — |
| 2 | B03–B08 — 083 tasks→analyze→implement→codexreview→ship→close | mixed | follows B02 | B02 |
| 3 | B10 — report the book-§4.3.1 guard finding to Udi | nano 1 | ✅ unblocked | — |
| 4 | Merge PR #228 | nano 1 | ✅ unblocked | — |
| 5 | T19 — ERA tag in marathon | midi 11 | held | PREREQ T11 |
| 6 | T20 — link 14 spec dirs | mini 7 | held | `link` CLI defect (buildkit lane) |
| 7 | W11 — resolve 080 | mini 7 | gated | Udi §1.14, discharge item J2 |
| 8 | W18 — Gleam cluster | mini 7 | gated | two contradictory recorded reads |

**B02 is the next action.** The marathon's own `next:` still points at **W11, which is
engineer-gated** — `next` ignores gating, so this table governs (trap #1 in `docs/SITREP-FORMAT.md`).

## 6 · 🔴 Standing hazards

1. **Three lanes are live in glpnet** (ariellas, gavriella, olamnit). We collided on roadmap round 47.
   Check `origin/develop` and the coop root before any shared-resource write.
2. **"STUCK lock" is a FALSE POSITIVE.** Verify liveness with PowerShell `Get-Process` sampling CPU
   twice. Git-Bash `ps -p` cannot see native Windows PIDs. **Never kill a holder.**
3. **Never parse `buildkit-roadmap status`** for counts — use the signed-export `heads` fold.
4. **Pipes mask failures**: `cmd | grep | tail` reports the *filter's* exit status. A silent success
   is not a success — this bit me once today.
5. **This repo is NOT a registered deploy target** (`pin mirror absent` on every command). The stale
   clone `D:\BSTDEV\glp\GLPNET` is the registered one. Unfixed — deploying would pin an engine
   version mid-marathon and needs an engineer decision.
6. **3 dangling `spec_path` pointers**: `specs/067-qr-link-provisioning`, `specs/066-wave6-consolidation`,
   and `guards-reference.md#comparison-guards` (a markdown anchor recorded as a spec dir).
7. **Never force-fetch tags** — `v2026.06.10.1` reports "would clobber existing tag".
8. **Do not normalise another host's takt records.** Check only your own `host=` partition.

## 7 · Restart readiness

- [x] Release cut and landed; 0 open release PRs
- [x] All work committed and pushed; PR #228 open for the remainder
- [x] Findings durable as marathon items, not scrollback
- [x] All four engineer rulings recorded in citable QIDs **and** the marathon
- [x] COOP: ACK sweep, ERA re-broadcast, 2 retractions, fulfilment ACK — all on the live root
- [x] Takt lake verified readable by the naive query
- [x] Next action identified and unblocked (**B02**)

**READY FOR RESTART.**

— `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · `2026-08-24T22:35:00Z`
