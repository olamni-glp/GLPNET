<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# BK-STD-3 — Precedence between engineer rulings

**Status:** ACTIVE. Established by ruling `Q-GLPNETO16-01` (2026-09-01, olamnit/glpnet).

## Why this exists

On 2026-08-31 three lanes each took a ruling that declared itself to govern
`/bk-release` across the whole fleet, and the three bars were mutually
inconsistent:

| ruling | lane | decided | bar |
|---|---|---|---|
| `Q-hardening-03` | olamnit | 13:08Z | Patch-level qualifies |
| `Q-GLPNETA13-01` | ariellas | 13:44Z | Feature-level bar |
| `Q-GLPNETS13-03` | gavriella | 17:56Z | Content bar, directive overrides |

None cited either of the others. Nothing anywhere said which one wins. Each
lane then executed a different bar in good faith, so release receipts stopped
being comparable across hosts and the legitimacy of a cut tag became
un-decidable. BK-STD-2 makes a *single* decided question citable; it says
nothing about *two* decided questions on the same subject. This standard closes
that gap.

## The rules

1. **Same subject, latest wins.** Where two rulings govern the same subject and
   are inconsistent, the one with the later `decided_at` governs the fleet from
   its own decision time. Earlier ones are superseded, not deleted.

2. **A superseding ruling MUST cite what it supersedes.** Its `background` names
   every ruling it displaces, by `set_id` and `qid`, with their decided
   timestamps. A ruling that silently contradicts a live ruling is malformed and
   the next lane to notice must raise the collision rather than pick a side.

3. **Supersession is written back onto the record.** The displaced question gets
   a `superseded_by` object (`set_id`, `qid`, `date`, `reason`) and a
   `kind: "supersession"` row in `engineer-decisions.jsonl`. This is the
   BK-STD-2 cite-never-re-ask duty applied to displacement.

4. **Fleet scope must be declared, not assumed.** A ruling governs only the lane
   and repo in its ledger row unless its `blocks` list explicitly names
   fleet-wide or cross-lane scope. A lane-scoped ruling never silently
   overrides another lane.

5. **Retrospective effect is stated explicitly.** A superseding ruling says
   whether actions already taken under the displaced bar stand or are re-opened.
   Silence means they stand.

6. **A collision is raised, never resolved unilaterally.** A lane that finds two
   live inconsistent rulings on one subject raises a BK-STD-2 question with all
   of them in `background` and picks no side until it is answered.

## The governing release bar (as at 2026-09-01)

`Q-GLPNETO16-01` selects **content bar, directive overrides**:

- A release requires **at least one `feat:` or `fix:` commit since the last
  tag** — docs-, roadmap- and chore-only accumulation does not qualify; **and**
- an **explicit engineer directive overrides** the measured bar in either
  direction.

Under rule 5, `v2026.09.01.1` — cut on 3 feat/fix commits plus a directive —
stands as legitimate.

## Applying it

Before citing any ruling, check for a `superseded_by` field on the record and
for a later same-subject ruling in the ledger. Cite the governing one, and cite
the displaced one only as history.
