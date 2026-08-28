<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# `bkquestion` — the pre-coded interactive question template

**Status: v0 reference implementation, fleet-adoptable, deliberately unhardened.**
A refined version is scoped as a buildkit feature — see [§7](#7--what-v1-needs-that-v0-does-not-have).

---

## 1 · Why this exists

Searched on 2026-08-24 across `ospark/**`, `.specify/templates/`, `D:\coop` and `D:\yngenios`:
**there was no pre-coded interactive question template anywhere on this host.** Every lane that has
put a question to the engineer invented its own shape — the same *five lanes, five shapes* problem
the R-1/R-2/R-3 report standard exists to end.

This is modelled on the harness's **built-in interactive prompt**, so a validated set renders
straight into it with no translation, and it adds the five things that shape does not carry.

---

## 2 · Use it

```bash
cp tools/bkquestion/TEMPLATE-question-set.json .specify/decisions/Q-<feature>-<UTC>.json
# edit it, then:
python tools/bkquestion/bkquestion.py validate .specify/decisions/Q-<feature>-<UTC>.json
python tools/bkquestion/bkquestion.py payload  .specify/decisions/Q-<feature>-<UTC>.json   # -> the prompt
python tools/bkquestion/bkquestion.py render   .specify/decisions/Q-<feature>-<UTC>.json   # -> Markdown
```

Ask the questions using the `payload` output. Then **record the answers**:

```bash
python tools/bkquestion/bkquestion.py record .specify/decisions/Q-<feature>-<UTC>.json \
    --by engineer --answer 'Q-020-01=Ship mine + take their _parent.py'

python tools/bkquestion/bkquestion.py decisions              # read them back
python tools/bkquestion/bkquestion.py decisions --expired    # acceptances that quietly became policy
```

**Stdlib only** (Constitution I). No pip dependency; the JSON-Schema check is hand-written for
exactly the keyword subset the schema uses, so it cannot pass a document by ignoring a keyword it
did not understand.

---

## 3 · The five additions over the built-in shape

| # | addition | why |
|---|---|---|
| 1 | **stable `set_id` + per-question `id`** | so an answer can be **cited** (`Q-020-03`). Before this, the five rulings of 2026-08-24 were recoverable only from a transcript |
| 2 | **declared `kind`** | a `risk-acceptance` **expires**; a `ruling` does **not**. Collapsing them is how a temporary acceptance becomes permanent policy without anyone deciding it should |
| 3 | **validated `cost`** on every option | an option with no stated downside is advocacy wearing the costume of a choice. Mandatory and **enforced**, not conventional |
| 4 | **a recorded decision** | the gap that mattered most. There was **no verb anywhere that records an engineer decision**. A ruling nobody can query is a ruling that gets re-litigated — this fleet re-litigated one contract question in four consecutive review cycles |
| 5 | **`escalate_after_hours`** | so a blocking question does not simply sit |

---

## 4 · The rules the validator enforces (not just the shape)

- The question **ends with `?`**. If it cannot, it is a statement and does not belong in a prompt.
- **At most one** option marked `recommended`; the renderer moves it **first** and appends
  `(Recommended)`, so accepting takes one keystroke. Writing `(Recommended)` into a label by hand is
  **refused**.
- `kind: risk-acceptance` **requires** `expires_after_days`; `kind: ruling` **forbids** it.
- A `ruling` / `prioritisation` / `tie-break` must name what it **blocks** and carry an escalation
  window. A question blocking nothing does not need an interactive prompt.
- Labels are **1–5 words**; headers are **≤ 12 characters**.
- **At most 4 questions per set.** The prompt takes four; a fifth means split the set.

### Both of these fired on their author, immediately

Writing this tool, the validator rejected **my own** template: `"Duplicate 020"` is **13** characters
against the 12-character chip limit — a limit I had exceeded earlier the same day without noticing.
It then rejected my attempt to record the day's five rulings as one set, because **four is the
maximum** and they were really asked in two rounds of three and two. Both are recorded here rather
than quietly fixed, because a validator that never fires on its author has not been tested.

---

## 5 · The decision ledger

`.specify/decisions/engineer-decisions.jsonl` — **append-only, one JSON object per line,
git-tracked**.

Git-tracked on purpose: the PGlite catalogs on this host are unreplicated, and on 2026-08-24 one of
them read 67 rows and then 0 with nothing noticing. **A ruling that exists in exactly one
unreplicated place is a ruling that can vanish silently.**

Each row carries `set_id`, `question_id`, `kind`, the full question text, the answer, `decided_by`,
`decided_at`, `expires_at`, and what it `blocks` — enough to answer *"who decided what, when, and
what was it blocking?"* without a transcript.

---

## 6 · Worked examples

`TEMPLATE-question-set.json` ships with **two real questions from 2026-08-24**, not invented ones —
a template with invented examples teaches the wrong reflexes. The five actual rulings of that day
are in `.specify/decisions/Q-020A-*.json` and `Q-020B-*.json`, with their answers in the ledger.

---

## 7 · What v1 needs that v0 does not have

Stated as work, not as a wish list, and scoped as a buildkit roadmap feature:

1. **A `buildkit-question` console entry**, so this is a first-class verb rather than a repo-local
   script that each lane copies and drifts.
2. **Catalog persistence alongside the JSONL floor** — the dual-sink pattern `buildkit-registry`
   already uses, with the git-tracked file authoritative on divergence.
3. **Expiry enforcement that acts**, not just reports: a `risk-acceptance` past its date should
   re-raise itself rather than wait to be queried.
4. **Escalation that actually escalates** — emit to the coop channel when
   `escalate_after_hours` passes with no recorded answer.
5. **A cross-lane decision view**, so one lane can see what another was already told, which is the
   fleet-level version of the re-litigation problem.
6. **CO/DuckLake mirroring**, so decisions land in the same observability substrate as takt.
7. **A skill wrapper** (`/bk-question`) so the pipeline stages can raise blocks through it uniformly.

Until v1 lands, **v0 is the standard**: copy it, use it, and do not invent a sixth shape.
