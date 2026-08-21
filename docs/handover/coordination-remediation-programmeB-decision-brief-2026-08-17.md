<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Coordination remediation — Programme B DECISION BRIEF (2026-08-17, olamnit)

**Terminal artifact of Programme B** (design/evaluate/curate/synthesize). Produced by direct
mechanical measurement of the candidate branches on this host, **not** a blind re-derivation —
because the candidate solutions already exist as branches and the deterministic conflict graph is
computable exactly. Feeds roadmap **#13** `coordination-feature-stream-durable-superset-fix` and a
`/bk-codify` consolidated hardened note. **Advisory. NO merge, NO ship. Terminates at the engineer's
canonical-writer + release/deploy decision.**

**Owner:** olamnit (curation seam, gavri §5.1). **Coordinator:** gavriella (engineer ruling
`192028Z`). **Inputs:** behaviour list `docs/handover/coordination-remediation-programmeB-behaviour-list-2026-08-16.md`; engineer writer-ruling (gavriella `205000Z`); live measurement of `D:/bstdev/research/buildkit` @ 2026-08-17T07Z.

## 0 · 🔴 The behaviour list is PARTLY STALE — corrected by measurement

The B1–B6 list was measured on released `v2026.08.15.1`. **develop has moved since.** Measured today:

| behaviour | list said | MEASURED on `origin/develop` today | corrected status |
|---|---|---|---|
| **B1** allocate writer appends to op log | "NONE — all paths end in a view file" | `scheduler/engine/daemon/allocate_writer.py` EXISTS (landed `ad796d02`, 2026-08-16 20:01) | ✅ **MERGED to develop** |
| **B2** e_t_s set / refuse-not-default | "NONE — writers hardcode 0.0" | allocate_writer refuses to emit a half-broken record (no real e_t_s → refusal) | ✅ **MERGED to develop** (in allocate_writer) |
| **proposed_actor writer** (engineer enlarged D1) | "zero occurrences on develop" | `proposed_actor` occurs 16× on develop (`__main__.py` 6, `allocate_writer.py` 10); it is a FIELD on the record, not a 2nd writer | ✅ **MERGED to develop** |
| **B3** admission refuses unaddressed | `bc203794`, DELIVER, unmerged | `confirm.py` (`requires_proposed_actor: True`, refuses empty proposed_actor) still ONLY on `feat/scheduler-transition-verb` | 🔴 **UNMERGED candidate** |
| **flow bridge** (board→pipeline read) | Inc2 | `flow/{identity,link,view,__main__}.py` + `flow open` still ONLY on `feat/scheduler-transition-verb` | 🔴 **UNMERGED candidate** |
| **B4** allocator forward-projects fit | `20d78ba4` design-ref | not separately verified this pass | ⚠️ carry-forward |
| **B5** engineer no-window → named refusal | ~2 lines, ariellas' T2 | not on develop | 🔴 build (ariellas) |
| **B6** `bk-*`↔`buildkit-*` normaliser | build | inconclusive (grep too broad); **assume still to build** | ⚠️ **unverified — build unless found** |

## 1 · 🎯 The REAL current rootcause (2 precise causes, in order)

The live glpnet board shows **7 WPs all backlog, 0 allocate ops, 0 proposed_actor**. But the writer
that would produce them **is merged to develop.** So the stream is severed by:

1. **DEPLOY LAG (dominant, immediate).** olamnit runs engine `2026.8.10.1` (pin `2026.07.30.1`) —
   older than even `v2026.08.15.1`, and the writer landed on develop *after* that tag. **Every host
   runs a pre-writer engine.** develop's `pyproject` still reads `2026.08.15.1` — develop is a day
   ahead of the last release, un-cut. The fix exists; nothing has released or deployed it.
2. **B3 gate + flow bridge unmerged.** The addressing-refusal gate (`confirm.py`) and the
   board→pipeline `flow open` bridge sit on `feat/scheduler-transition-verb` (`bc203794` lineage),
   never merged. Without B3, an addressed WP is not *refused-when-unaddressed*; without `flow open`,
   a claimed WP never binds to a feature+marathon (the consumption link gavriella measured).

## 2 · ⚙️ Mechanical conflict graph (plain code — fleet-#56 safe, no null-as-consensus)

Diff of each candidate branch vs `origin/develop` merge-base:

```
B1-allocate-writer      0 files ahead  → already on develop
allocate-repo-field     3 files: __main__.py, engine/daemon/allocate_writer.py, test_allocate_writer_pcore.py
B3-transition-verb     68 files (~25 are .specify/codify/notes NOISE); code = flow/*, confirm.py, board.py, __main__.py
X6X7-hardening          NO merge-base with develop (branched off-line) — FLAG
consolidation-015       0 files ahead  → already on develop
```

Pairwise overlap = conflict edges:
```
B1 ↔ B3                 COMPOSABLE (disjoint)
B1 ↔ allocate-repo      COMPOSABLE (disjoint)
B3 ↔ allocate-repo      CONFLICT  →  src/buildkit_cli/scheduler/__main__.py   ← the ONLY real edge
all others              COMPOSABLE
```

**The single residual conflict is `__main__.py`** — both B3 and allocate-repo-field register CLI
verbs there. This is the exact "writers collide on `__main__`" rootcause mechanism, reproduced. It is
a **verb-registration merge**, not a semantic conflict: compose both verb registrations, keep both.

## 3 · ✅ Recommended composition (2 serial increments — updated for measured reality)

**Increment 1 — RELEASE + DEPLOY what is already built, then land the gate (unblocks the stream):**
- **1a. RELEASE develop** — `buildkit release` from develop: bump `pyproject` past `2026.08.15.1`,
  tag, so the merged allocate/e_t_s/proposed_actor writer becomes a versioned engine. *(This is most
  of the "durable fix" — it already exists in develop; it just needs cutting.)*
- **1b. DEPLOY to all hosts** — `buildkit-deploy latest all` on every host + re-onboard with **real
  caps** (fixes the `0 caps` olamnit onboard). This is the "enable and deploy on all other hosts".
- **1c. LAND B3 + resolve the one `__main__.py` conflict** — engineer picks canonical writer, composes
  `feat/scheduler-transition-verb` (B3 confirm gate + `flow open` bridge) with the allocate-repo-field
  verb registration, merges to develop, re-releases, re-deploys.
- **1d. B6 normaliser** (`bk-*`↔`buildkit-*`) — verify absence, then build (~localized); without it
  olamnit stays gate-eliminated even after 1a–1c.

**Increment 2 — durable-read + identity + hardening:** X6/X7 (`feat/scheduler-driver-hardening-x6x7`,
re-base first — no merge-base), run-identity seam, the ONE `/bk-guards` aggregate-honesty rule.

**Hard rule preserved:** no field ships without its reader, no reader without its writer, per
increment. The writer (B1/B2/proposed_actor) is on develop; B3 is its refusal-reader → 1a–1c keep
them together within one release train.

## 4 · ⚠️ Why the FULL multi-agent Programme B was NOT run (and should not be, as-is)

- Its premise ("B1 = NONE, build from scratch") is **falsified by measurement** — a blind run would
  re-derive a stale question at 3–15× cost.
- The deterministic core (conflict graph) is **done here, exactly**, in plain code.
- The remaining choices are the **engineer's** (release now? canonical writer for B3? deploy window?)
  — not a judge-panel's to adjudicate.
- The scheduler/board is **gavriella's coordinator domain** with 3rtask runs already in flight
  (`cdba`, `6bb9`). A 4th olamnit blind run over the same branches would reproduce the very
  writers-collide rootcause. **ESC-2** (gavri's `6bb9` slice S4 never ran) is moot for THIS brief —
  the brief now rests on direct develop measurement, not on that slice.

## 5 · 🔴 Engineer decisions requested (terminal)

1. **Release now?** Cut `buildkit release` from develop to version the already-merged writer. (My rec: **yes** — it is the single highest-leverage, lowest-risk unblock; the code is merged and reviewed.)
2. **Canonical writer for B3 + the `__main__.py` composition?** Who lands `feat/scheduler-transition-verb` (confirm gate + flow bridge) + folds the allocate-repo-field verb. (Coordinator = gavriella; olamnit holds the curation seam.)
3. **Deploy authority** — `buildkit-deploy latest all` per host + re-onboard with real caps. Cross-host, so per-host operator action.
4. **B6 normaliser** — build (fold `bk-*`↔`buildkit-*`) vs re-declare olamnit's caps (re-declare breaks olamnit's local skill names → normaliser preferred).

— olamnit · 2026-08-17 · host OLAMNIT · nothing merged · no peer stream written · advisory
