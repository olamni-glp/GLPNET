<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Restart pointer — **THIN POINTER ONLY, NOT A WORK LEDGER**

> Last verified **2026-08-31T11:30Z** by the `gavriella` lane, against durable rows — not from a
> summary. Per CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*, the **roadmap + buildkit
> marathon state are the source of truth**. This file exists only to name the live run so a restart
> does not have to guess.

🔴 **This file was itself the defect on 2026-08-31.** It pointed at `mrun-f5ef56dba3c1` /
`glpnet-full-completion-programme` (roadmap round 40, 38/91 steps) for **eight days after that run
was superseded** — exactly the *"hand-written pointers drift stale and send restarts into finished
work"* failure CLAUDE.md warns about. **If the run below does not match
`buildkit-marathon status`, believe the CLI and fix this file.**

---

## Resume in one line

```
resume marathon
```

which is:

```
buildkit-marathon resume --feature 078-verification-receipts
```

🔴 **`--feature` is mandatory** — there is no `.specify/feature.json` in this repo, by design.

## The live run

| | |
|---|---|
| run | **`mrun-20d9230f767b`** [open] |
| feature | **`078-verification-receipts`** |
| lane / host / repo | `gavriella` @ **GAVRIELLA** · **GLPNET** |
| position | seq **378** · steps **28/111** · outstanding **204** |
| roadmap | round **59** · **28 not-closed** over 21 epics / 122 features |

## 🔴 READ THIS BEFORE ANYTHING ELSE

**`docs/research/RESTART-PREP-gavriella-glpnet-mrun-20d9230f767b.md`**

Read it **from the bottom up** — it is append-only and **the LAST section supersedes every section
above it**. Current tail: *SESSION 12 CLOSE* + *SESSION 12 ADDENDUM* (2026-08-31), which carries the
four engineer rulings `Q-GLPNETS12-01..04`, the ruled next actions, and the standing constraints.

**Do not resume from this file, from a compaction summary, or from any prose plan.** Derive position
from `buildkit-marathon status` and the durable rows; use the restart doc for *why*, not *where*.

## Other lanes' runs in this repo — do not resume into these

| run | lane | note |
|---|---|---|
| `mrun-f77f62158255` | `shiras-glpnet` | peer lane, Linux host; `RESTART-PREP-shiras-…md` |
| `mrun-f5ef56dba3c1` | historical | **superseded** — was wrongly named here until 2026-08-31 |
