<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# HOST-INTERCONNECTIVITY-HARDENING — ROOTCAUSES (CRDT, multi-contributor)

**Feature:** `host-interconnectivity-hardening` · **Kind:** fleet coordination / infrastructure
**Status:** Draft (evidence-gathering) · **Origin:** shiras partial-board-absence 3rtask (`mrun-76da6e46bd44`, olamnit/glpnet)

> **CRDT merge rule (R1).** This is a grow-only, multi-writer document. Each contributor appends
> ONLY under their own `## contributor: <lane@host>` block (newest entry first within it) and
> may append rows to the shared **Consolidated rootcause register** table by adding NEW rows
> (never rewriting or deleting another actor's row). Conflicts are surfaced as a new
> `CONFLICT:` row, never resolved by overwrite. Union-by-(rootcause-id) on merge; a divergent
> byte-copy is quarantined, not silently merged. The engineer ratifies a rootcause by setting
> its `RATIFIED` column.

---

## Consolidated rootcause register (append rows; union-by-RC-id)

| RC-id | Rootcause (one line) | Evidence | Raised by | Corroborated by | RATIFIED |
|-------|----------------------|----------|-----------|-----------------|----------|
| RC-01 | **Onboarding is per-board and manual** — a host must run `buildkit-scheduler onboard` per board; no sweep onboards a host to its full board-membership set. | shiras in caps on 8/14 boards, absent from buildkit/lejepa/mstack/olamnit/tefl/yngenios | olamnit@glpnet | _(awaiting)_ | — |
| RC-02 | **Onboarding ≠ allocation (SCHED-R7 binding gap)** — no writer binds `ready` WPs to onboarded actors, so a steady feature stream never reaches a host even where present. | glpnet board 32 WPs, 0 allocated to olamnit despite onboarding; scheduler `cycle` emits `starved`/`stalled` but no bind-writer | olamnit@glpnet | _(awaiting)_ | — |
| RC-03 | **No host→board membership contract** — nothing declares which hosts SHOULD be on which boards, so absence is invisible (no gate fires on a missing expected member). | 6-board absence went unremarked until this census | olamnit@glpnet | _(awaiting)_ | — |
| RC-04 | **Arrival lag / no auto-onboard on host registration** — a new host (shiras, first glpnet ACK 2026-08-25) integrates board-by-board over days. | shiras first glpnet ACK today; Linux host crucible/glp/GLPNET | olamnit@glpnet | _(awaiting)_ | — |
| RC-05 (CANDIDATE) | **Mount/drive-mapping reach** — Linux hosts reach the volume as `/mnt/gavri/d`; a mapping gap may block reach to some boards. Needs shiras evidence (shared-dir + SSH). | shiras `root` cited `/mnt/gavri/d/coop/glpnet/sched` | olamnit@glpnet | _(needs shiras)_ | — |

> **Extrapolation to ALL host-host interconnectivity (engineer directive 2026-08-25):** RC-01…RC-04
> are not shiras-specific — they are the general failure modes of *any* host joining *any* board.
> The fix must be host-symmetric (works for ariellas↔gavriella↔olamnit↔shiras and future hosts),
> not a shiras patch.

---

## contributor: olamnit@glpnet
- **2026-08-25T11:39Z** — cycle-0 census + RC-01…RC-05 above. Primary-source: `I:/coop/*/sched/caps/`,
  `ls | grep -ci shiras` across I:/D:/H:, `buildkit-scheduler board`. Broadcast for cross-critique:
  `I:/coop/20260825T113917Z-olamnit-glpnet-BROADCAST-3RTASK-EVIDENCE-...ACK-REQUESTED.md`.
  RC-02 shares a rootcause with roadmap rank-13 `coordination-feature-stream-durable-superset-fix`
  and olamnit backlog P01 — treat as ONE superset fix.

<!-- append new contributor blocks below; do not edit blocks above yours -->
