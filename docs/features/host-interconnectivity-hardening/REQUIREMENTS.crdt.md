<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# HOST-INTERCONNECTIVITY-HARDENING — FEATURE REQUIREMENTS (CRDT, multi-contributor)

**Feature:** `host-interconnectivity-hardening` · **Status:** Draft · **Backing rootcauses:** [[ROOTCAUSES.crdt.md]]

> **CRDT merge rule (R1).** Grow-only, multi-writer. Append NEW `FR-nn`/`NFR-nn` rows (never
> rewrite/delete another actor's row); record disagreement as a `CONFLICT:` row. Union-by-(FR-id)
> on merge; the engineer ratifies via the RATIFIED column. Each contributor also keeps a dated
> `## contributor:` block (newest first).

---

## Background (one paragraph)

Hosts join the fleet's per-board scheduler + COOP channel one board at a time, manually, with no
declared membership contract and no writer that binds ready work to onboarded actors. The result
is silent partial-absence (a host present on some boards, missing from others) and starvation
(onboarded but never allocated). Witnessed via shiras (present 8/14 boards, 0 steady allocation)
but structural for every host. This feature makes host↔board↔COOP interconnectivity **declared,
swept, auto-healed, and observable**, host-symmetrically.

## Consolidated requirements register (append rows; union-by-FR-id)

| FR-id | Requirement | Addresses | Raised by | RATIFIED |
|-------|-------------|-----------|-----------|----------|
| FR-01 | A **declared host→board membership contract** (config) states which hosts belong on which boards; drift from it is a loud finding. | RC-03 | olamnit@glpnet | — |
| FR-02 | A **fleet-onboard sweep** command onboards a host to every board in its membership set, idempotently (no duplicate grow-only rows). | RC-01, RC-04 | olamnit@glpnet | — |
| FR-03 | Close the **SCHED-R7 binding gap**: a writer binds `ready` WPs to onboarded, capability-fit actors so a steady stream reaches every host (converge with rank-13 superset fix / P01). | RC-02 | olamnit@glpnet | — |
| FR-04 | **Auto-onboard on host registration** + a `register`/`unregister` verb that captures a host's board-membership set. | RC-04 | olamnit@glpnet | — |
| FR-05 | **Membership-gate**: a board/scheduling report flags any declared member that is absent from `caps` (absence becomes a firing gate, not silence). | RC-03 | olamnit@glpnet | — |
| FR-06 | **Mount/reach preflight**: each host verifies it can reach every board root it is a member of (Linux `/mnt/...`, Windows drive letters); unreachable → loud, not silent-empty. | RC-05 | olamnit@glpnet | — |
| NFR-01 | Host-**symmetric**: works identically for ariellas/gavriella/olamnit/shiras and future hosts; no per-host special-casing. | all | olamnit@glpnet | — |
| NFR-02 | **No data loss, no silent deferral**: never minimize scope or silently defer to ease an unresolved tension (fleet reporting standard). | all | olamnit@glpnet | — |

## Open engineer questions (surface via bkquestion; do not self-resolve)
- Q-A: Where does the host→board membership contract live — buildkit config, or a COOP-shared registry? (authority + single-source-of-truth)
- Q-B: Is FR-03 the SAME deliverable as rank-13 `coordination-feature-stream-durable-superset-fix` (merge) or a companion?
- Q-C: Which lane/host OWNS this feature (it is fleet-wide; candidate = buildkit lane as the scheduler home)?

---

## contributor: olamnit@glpnet
- **2026-08-25T11:39Z** — FR-01…FR-06 + NFR-01/02 from the shiras census. Feature seeded on the
  glpnet roadmap for score/promote; ownership (Q-C) is an open engineer question — likely buildkit lane.

<!-- append new contributor blocks below; do not edit blocks above yours -->
