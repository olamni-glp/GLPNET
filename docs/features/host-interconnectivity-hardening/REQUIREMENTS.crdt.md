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
| FR-07 | **First-party measurement over projection**: host-state / absence / executability MUST be determined by first-party measurement over the verified SSH mesh (`ssh <host>`), never by concluding host-state from an SMB share (a partial projection). Absence-by-share is forbidden. | RC-06 | olamnit@glpnet | — |
| FR-08 | **Dispatchability joins host executability**: a WP is dispatchable to an actor only if that actor's host can execute it (platform + toolchain fit), not on board presence alone; the allocator/`poll` reason must cite the executability check. | RC-07 | olamnit@glpnet | — |
| FR-09 | **Root-named claims**: every absence/allocation/board claim MUST name the board root it is made against (the board is multi-legged); a claim without a named root is rejected. | RC-08 | olamnit@glpnet | — |
| FR-10 | **Typed PLATFORM capability with polarity + retraction**, consuming crucible ruling Q-041-01 (not a rival), read by the allocator/matcher; supports an explicit negative token beside a stale/false declaration. | RC-10 | olamnit@glpnet | — |
| FR-11 | **Engine-version guard on shared boards**: refuse or quarantine a write from an engine version that would fork the board (e.g. a UNC-rewrite), so a lane on an older pin cannot silently fork. | RC-09 | olamnit@glpnet | — |
| NFR-01 | Host-**symmetric**: works identically for ariellas/gavriella/olamnit/shiras and future hosts; no per-host special-casing. | all | olamnit@glpnet | — |
| NFR-02 | **No data loss, no silent deferral**: never minimize scope or silently defer to ease an unresolved tension (fleet reporting standard). | all | olamnit@glpnet | — |
| NFR-03 | **Compose EXISTING verbs only** (`buildkit-scheduler` {ingest/allocate/transition/stock-edges/effort-assign/onboard…} + `bk-flow` {poll/claim/open/report}); inventing a verb is out of scope and must be reported as a required upstream change. | RC-07, RC-02 | olamnit@glpnet | — |
| NFR-04 | **No identity-forging**: a fix must be executable by the target host itself or by explicit engineer instruction; a fix requiring one host to write another host's identity/caps/ops is INVALID. | RC-10 | olamnit@glpnet | — |
| NFR-05 | **No root-identity stamping until ruled**: `--ensure-identity` on a board root is out of scope until the engineer rules on convergence-vs-stamp; grow-only single-writer streams are inviolate. | RC-08, RC-09 | olamnit@glpnet | — |

## Open engineer questions (surface via bkquestion; do not self-resolve)
- Q-A: Where does the host→board membership contract live — buildkit config, or a COOP-shared registry? (authority + single-source-of-truth)
- Q-B: Is FR-03 the SAME deliverable as rank-13 `coordination-feature-stream-durable-superset-fix` (merge) or a companion?
- Q-C: Which lane/host OWNS this feature (it is fleet-wide; candidate = buildkit lane as the scheduler home)?

---

## contributor: olamnit@glpnet
- **2026-08-28T02:30Z** — added **FR-07…FR-11 + NFR-03…NFR-05** grounded by 3rtask run
  `20260828T012249Z-b414` (see [[ROOTCAUSES.crdt.md]] RC-06…RC-10). FR-07 (first-party over projection)
  and FR-08 (dispatch joins executability) are the load-bearing additions — they turn the unified RC-07
  defect into concrete requirements; NFR-03…NFR-05 encode the 3rtask hard constraints (existing verbs
  only, no identity-forging, no root-identity stamping yet). Open engineer question Q-C (feature owner /
  lane) is unchanged and still gates promotion — candidate = buildkit lane.
- **2026-08-25T11:39Z** — FR-01…FR-06 + NFR-01/02 from the shiras census. Feature seeded on the
  glpnet roadmap for score/promote; ownership (Q-C) is an open engineer question — likely buildkit lane.

<!-- append new contributor blocks below; do not edit blocks above yours -->
