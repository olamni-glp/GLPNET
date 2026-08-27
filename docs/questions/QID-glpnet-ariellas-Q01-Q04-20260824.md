<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# ENGINEER QUESTIONS — `glpnet` / `ariellas` · 2026-08-24

    schema:   bk-engineer-question/1   (BK-STD-2, adopted verbatim)
    template: I:\coop\BK-STD-2-ENGINEER-QUESTION-TEMPLATE.md
    run:      mrun-f5ef56dba3c1
    amendments adopted: A1 (cost states band vs measured) · A2 (ORIGIN may be superseded-by-evidence)

---

## QID `glpnet-ariellas-Q01`

**BLOCK** — glpnet cannot be released: `origin/develop` is 100 commits ahead of `origin/main` with no tag cut today.

**ORIGIN** — `contradiction`

**BACKGROUND**
- `git rev-list --count origin/main..origin/develop` = **100** (measured 2026-08-24T20:2xZ).
- Latest glpnet tag is **`v2026.08.23.2`** (`git tag --sort=-creatordate`) — **no release cut today**.
- I held the release on your 06:xx ruling pending gavriella's quiescence ACK. **No ACK has arrived.**
- gavriella has since posted `191500Z-tefl`: *"roadmap-lifecycle-writeback is ARIELLAS, I STAND OFF"* — which reads as them standing off, but is about **tefl**, not glpnet, so it does not discharge the glpnet hold.
- gavriella cut `v2026.08.24.1` **for tefl** with 3 disclosed red tests — precedent that a release with disclosed reds is acceptable in this fleet.

**IMPACT IF UNANSWERED** — 100 commits stay unreleased and the gap grows daily; `main` drifts further from reality; any consumer pinning `main` gets a 2-day-stale glpnet. The hold was correct for one turn; it is now the thing causing the harm.

**AFFECTED LANES** — glpnet (ariellas, gavriella, olamnit) — lane-local, but three lanes commit here.

**OPTIONS**

| # | option | consequence | cost | reversibility |
|---|---|---|---|---|
| 1 | Cut `v2026.08.24.1` now, disclosing that gavriella never ACKed | main current; follows the tefl precedent of releasing with disclosure | `micro 3` (band) | **one-way** — a tag is public; forecloses a clean re-cut at this SHA |
| 2 | Re-ask gavriella, hold again | zero risk of tagging in-flight work | `nano 1` (band) | reversible |
| 3 | Hold until a lane explicitly owns glpnet releases | fixes the root cause (no named release owner) | `mini 7` (band) | reversible |

**RECOMMENDATION — Option 1.** The hold has already served its purpose: gavriella has had 13 hours and their own message shows them stepping back from this repo's roadmap work. Option 2 beats it only if gavriella is still mid-flight, and the evidence now points the other way; meanwhile Option 2 pays the growing-gap cost for another cycle. Disclosure in the release notes gives the same protection the tefl precedent used.

**DECISION** [ ]  **DATE** [ ]  **RATIONALE** [ ]

---

## QID `glpnet-ariellas-Q02`

**BLOCK** — `buildkit-roadmap status` emits no row for features in state `implemented`; every renderer that parses `status` silently under-reports, and I cannot locate the source line.

**ORIGIN** — `measurement`

**BACKGROUND**
- `status` state-row census: `1 captured · 94 closed · 14 promoted · 9 specified`. **No `implemented` row.**
- `qr-link-provisioning` (state `implemented`, WSJF **4.00** — a top-third row) gets no row, yet `status` lists it in its own *"Recommended build order"* and *"Parallel-safe"* lines.
- The signed-export `heads` fold **does** carry it. Renderer count **23** vs fold **24**.
- The ruled renderer is **not** at fault (`line 91` skips only `closed`; `line 179` totals are dynamic; byte-identical to `origin/develop` after CRLF normalisation).
- Two obvious hypotheses **refuted**: `implemented` is a legal state (`roadmap/model.py:24`), and the feature has a valid epic.
- I broadcast a wrong mechanism for this earlier today and have retracted it (`203000Z`).

**IMPACT IF UNANSWERED** — every lane's BK-STD-1 table under-reports by the number of `implemented` features it holds, and `--check` exits 0 regardless. Work that is *implemented but not shipped* is exactly the work most at risk of being forgotten.

**AFFECTED LANES** — **fleet-wide**: every lane rendering the ruled table from `status`.

**OPTIONS**

| # | option | consequence | cost | reversibility |
|---|---|---|---|---|
| 1 | File to buildkit lane with the measurement; they bisect | correct ownership; consistent with the Q3-2026-08-24 `link` ruling | `nano 1` (measured — the write-up exists) | reversible |
| 2 | I bisect `_cmd_status` and PR the fix | fastest close | `mini 7` (band) | reversible |
| 3 | Re-point every renderer at the export fold instead of `status` | removes the whole class of blind spot | `midi 11` (band) | reversible |

**RECOMMENDATION — Option 1, with Option 3 raised as a follow-on feature.** Option 1 matches the ownership ruling already made today for the `link` defect and keeps me out of another lane's code. Option 3 is the better *engineering* answer — `status` is a presentation surface and no renderer should depend on it — but it is a fleet-wide change that needs its own spec, not a same-session patch. Option 2 is rejected: I have already been wrong once about this mechanism today.

**DECISION** [ ]  **DATE** [ ]  **RATIONALE** [ ]

---

## QID `glpnet-ariellas-Q03`

**BLOCK** — the fleet takt lake holds **zero `tokens` records for `repo=glpnet` from any host**, so per-phase token use cannot be reported for this repo as the standard requires.

**ORIGIN** — `measurement`

**BACKGROUND**
- Fleet takt lake `I:\coop\_takt-lake`: **591** records — `olamnit` 229 · `gavriella` 181 · `ariellas` 181.
- `tokens` records by repo: `buildkit`/ariellas **16**, `yngenios-windows`/gavriella **16**, `hatzinor`/olamnit **12**, `yngenios-windows`/olamnit 9, `LeJEPA`/gavriella 5, `olamnit-assistant` 3, `ospark` 2, `yngenios-windows`/ariellas 1. **`glpnet`: 0.**
- glpnet **era** records do exist (2 of mine): `glpnet-full-completion-programme`, `total_seconds` 83199.6 (= 23.11 h), `measurable=False`, `unmeasurable_steps=9` — consistent with the live command, so the era path works here.
- So this is **narrow**: era/stage recording works for glpnet; the **token ledger is not wired for this repo** while it is for at least six others.

**IMPACT IF UNANSWERED** — the standard mandates per-phase token use be stored in and served from the lake. For glpnet that is unsatisfiable, so every glpnet token figure is either absent or hand-derived — the exact "estimate" the takt discipline forbids.

**AFFECTED LANES** — glpnet, all three lanes. Possibly others with no `tokens` rows.

**OPTIONS**

| # | option | consequence | cost | reversibility |
|---|---|---|---|---|
| 1 | Find why `buildkit-size tokens` records for buildkit but not glpnet; fix config | closes it at the cause; likely a per-repo enablement gap | `mini 7` (band) | reversible |
| 2 | Fold into the existing `takt-and-token-persistence-to-ducklake` feature | one spec covers it; no parallel work | `nano 1` (measured — row exists) | reversible |
| 3 | Accept and disclose "no glpnet token data" in every sitrep | honest, zero effort | `nano 1` (measured) | reversible |

**RECOMMENDATION — Option 1, tracked under the Option 2 feature row.** olamnit reported `buildkit-size tokens report` returning a real glpnet-lane figure (501,922 tokens / 15 records) from the **store**, which means the data exists somewhere and only the **lake mirror** is missing — a much smaller fix than the feature row implies. Option 3 alone is capitulation on a requirement you have called mandatory.

**DECISION** [ ]  **DATE** [ ]  **RATIONALE** [ ]

---

## QID `glpnet-ariellas-Q04`

**BLOCK** — a naive fleet-wide read of the takt lake raises an exception instead of returning data, and which lane it breaks for depends on file ordering.

**ORIGIN** — `measurement`

**BACKGROUND**
- `read_parquet('I:/coop/_takt-lake/takt/**/*.parquet', hive_partitioning=true)` →
  `ConversionException: failed to cast column "reason" from type VARCHAR to JSON`, on
  `kind=era/host=olamnit/date=2026-08-24/olamnit-20260824t062135679252.parquet`.
- Offending value is free text: `"missing steps: specify, clarify, plan, tasks, a..."` — not JSON and never will be.
- DuckDB infers schema from the **first** file read, so whether a lane's query works depends on which host's file sorts first. **Order-dependent, therefore intermittent.**
- Workaround **verified working**: `union_by_name=true`.

**IMPACT IF UNANSWERED** — lanes that hit a same-schema file first believe the lake is healthy; lanes that do not get a hard failure. A clean result is not evidence of a clean lake, which makes this self-concealing.

**AFFECTED LANES** — **fleet-wide**: any lane querying the shared takt lake.

**OPTIONS**

| # | option | consequence | cost | reversibility |
|---|---|---|---|---|
| 1 | Pin `reason` to VARCHAR at write time + add `union_by_name=true` to documented reads | fixes cause and symptom; VARCHAR already holds free text | `micro 3` (band) | reversible |
| 2 | Documentation-only: mandate `union_by_name=true` | zero code; every future reader must remember | `nano 1` (band) | reversible |
| 3 | Rewrite existing divergent parquet files to one schema | lake internally consistent | `mini 7` (band) | **one-way** — rewrites peer-authored records |

**RECOMMENDATION — Option 1.** It beats Option 2 because a rule every reader must remember is a rule that fails the first time a lane writes a query from memory — and this defect is self-concealing, so that failure will not be obvious. Option 3 is rejected outright: rewriting other hosts' records in a shared store is exactly the unilateral mutation the fleet's single-writer discipline forbids.

**DECISION** [ ]  **DATE** [ ]  **RATIONALE** [ ]
