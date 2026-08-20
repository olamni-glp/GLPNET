<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Coordination remediation — Programme B authoritative behaviour list + run plan (safe-restart input)

**Owner:** olamnit (curation seam, per gavri §5.1). **Source:** gavriella `130500Z` (authoritative), her `112500Z` (Programme B method), olamnit `ba84` + olamnit-assistant `111500Z` rootcause. **Vehicle:** roadmap feature #13 `coordination-feature-stream-durable-superset-fix`. **Codify:** `cn-20260816T122806`.

## Rootcause (Programme A, CLOSED — triangulated 3 ways)
The feature stream is severed in 6 links + a 7th (authored fixes don't propagate). Mechanism = **curation failure**: multiple scheduler writers touch the same 3 files (`__main__.py`/`board.py`/`cycle.py`) and none lands; `allocate` has readers, **0 writers**. Do NOT re-run Programme A.

## 🔴 Ruling (2026-08-16, binding)
- **DO NOT MERGE `069`** — red suite (migration-0032 DDL 120>119) + `recovery.py:216` writes an **unredacted secret** into an authoritative JSONL.
- **Do NOT cherry-pick `20d78ba4`** — not an ancestor of shipped R2; cherry-pick **reverts R2**.
- **Route = REIMPLEMENT** on top of shipped R2, on `develop`, as ONE serial increment. **No merge. No ship.** Terminates at a **decision brief → engineer canonical-writer ruling**.

## ⭐ THE AUTHORITATIVE BEHAVIOUR LIST (gavri `130500Z` §3) — B scores BEHAVIOURS, not shas
| # | Behaviour (what must be true) | Reference impl | State |
|---|---|---|---|
| **B1** | a verb durably appends an `allocate` op to the op log under the proposing actor, carrying `proposed_actor`, **non-zero `e_t_s`**, `requires`, `required_capability` | **NONE** (all allocator paths end in a view file `dispatch.py:291`, never the op log) | 🔴 build |
| **B2** | operator can set a real `e_t_s`; a WP-scoped op with no estimate is **refused, not defaulted to 0.0** | **NONE** (writers hardcode `0.0`: `confirm.py:329/360`, `onboard.py:143`; no parser flag; `plan.py:479` drops `e_t_s<=0`) | 🔴 build |
| **B3** | admission **refuses an unaddressed proposal** with a named reason, never guesses an assignee | ✅ **`bc2037944f9baf4a8ffbe3e33c3bb5c151b454d1`** (`confirm.py:173/223-228/78,88`) | ✅ **WRITTEN — DELIVER, do NOT re-author.** On one branch `origin/feat/scheduler-transition-verb-20260816`, untagged, absent from develop/main/v2026.08.15.1 |
| **B4** | allocator decides fit by forward-projecting committed load + WP effort across declared windows over the horizon (== the CPM feasibility fn) so allocator+plan agree by construction | `20d78ba4` (**DESIGN REFERENCE ONLY** — pre-R2 lineage, cherry-pick reverts R2) | ⚠️ **RE-IMPLEMENT on shipped R2 + new tests** |
| **B5** | engineer with no declared window → **named refusal with its own gate label**, not a `0.0` that reads as a resource limit | partial in `20d78ba4` (fixes message, keeps `0.0`, mislabels BOTH branches `gate:capacity`) | 🔴 build (~2 lines; **ariellas' T2, theirs by discovery**) |

**Composition (ONE serial increment):** `B1 + B2 + B4 + B5` (build) **+ B3 (deliver, don't re-author)**. 🔴 **B1 and B2 cannot be split** (B1 without B2 mints proposals with `e_t_s=0.0` that `priority_ranks` drops — a stream that reports success while moving nothing). **Hard rule: no addressing/ownership field ships without its READER, and no reader without its WRITER, in the same increment.**

## 🔴 Why olamnit SPECIFICALLY gets no stream (gavri §5 — new, sharp)
The R7 capability gate is **fail-closed on exact `(kind,name)` equality with `verified==True`** (`allocator.py:97-119,344-352`). ariellas+gavriella declare `buildkit-*`; **olamnit declares `bk-*`** → any proposal requiring `buildkit-scheduler` **silently eliminates olamnit's lane** (elimination is a gate outcome, not an error — no signal). Not cured by `resolve_host` (capacity only). **olamnit answer: `bk-*` is an ARTEFACT of this repo's buildkit→bk skill-rename migration; fix = a NORMALISER (fold `buildkit-*`↔`bk-*` in capability matching), NOT a re-declaration (re-declaring would diverge olamnit from its own installed skill names). Final call = engineer.** This behaviour belongs in the increment as **B6 (capability-name normaliser)**.

## #13 housekeeping (gavri §2, my row/my call)
- 🔴 **`#13` has `epic_id = NULL` → invisible to `roadmap status`** (D8 class; 12/27 not-closed features affected). Fix: `edit-feature --epic <slot>` — DO before B's decision brief.
- `#13` carries a **pointer** to `buildkit#scheduler-pipeline-dispatch-superset` (row_version 8) + tracked file `docs/research/wp-supply-rca-addendum3-reader-exists-2026-08-16.md` (commit f364760c) — buildkit row is a **reference**, not a rival. Notes have a ~2600-char ceiling → keep the behaviour list in THIS tracked doc, not the row.

## Programme B run plan (next session — olamnit owns the seam)
1. Slices = candidate SOLUTIONS: one blind builder per behaviour's reference impl + **integration-base slice (`develop`)**. B3's `bc203794`, B4's `20d78ba4` (design-ref), B1/B2/B5/B6 = greenfield-on-R2.
2. Per-candidate rubric (correctness-from-diff, coverage, test-carriage, blast-radius, conflict-surface, reversibility, base-delta).
3. **Conflict graph in PLAIN CODE** (fleet #56: 3rtask merge returns null on disjoint slices — do not present null as consensus).
4. Judge-panel = 1 codex critic per candidate PAIR on conflict edges (DOMINATES/COMPOSABLE/IRRECONCILABLE).
5. Graft-not-discard synthesis → **decision brief** (recommended reimplement composition + shas + grafts + residual conflicts + **explicit engineer canonical-writer decision request**). **NO merge.**
6. ⚠️ gavri ESC-2: her run `6bb9` slice S4 (COOP operational record) never ran (API limit) → E12/E22 lack primary input; **re-run that slice before B's decision brief is final**.

## Open ACKs / asks
- gavri §6: (1) confirm `wp-supply-…` is a THIRD lane's vehicle (consult that lane before superseding) — **olamnit ACK: yes, will not touch it**; (2) confirm B3 is DELIVER not reimplement — **olamnit ACK: yes, deliver**.
- ariella: Q4–Q7 (spec-citation `<repo>#<nnn>`, ospark polling, buildkit-board owner re-seed, actor-id grammar); Q8 engineer canonical-writer ruling (terminal).
- engineer: canonical-writer ruling; bk-*↔buildkit-* normaliser vs re-declare; run Programme B now or next session.
