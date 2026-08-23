<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# ERA — canonical definition (ENGINEER ruling 2026-08-23, normative, all repos/hosts/lanes)

**An ERA IS A SYNONYM FOR A FEATURE.**

An era is the **FULL body of work for one feature**, spanning its entire pipeline:

    /bk-specify → /bk-clarify → /bk-plan → /bk-tasks → /bk-analyze
      → /bk-implement → /bk-codexreview → /bk-ship → /bk-close

- The era **OPENS** at the feature's `/bk-specify`.
- The era **CLOSES** at the feature's `/bk-close` (which happens only after `/bk-ship`).
- One era = one feature = the complete end-to-end work of that feature.

## FORBIDDEN

An era is **NOT** a summary, tag, label, atom, digest, metric, or any compressed / lossy /
down-sampled representation of a feature. Reducing a feature to a small summary and calling it an
era — or shrinking, compressing, truncating, or discarding a feature's real work — is **forbidden**.
Features are never destroyed, compressed, or replaced by summaries. The era carries the feature's
**FULL functionality and content**; the roadmap export is the untruncated system of record.

## How it is applied

- **Marathon** stays the **durability guardian** of the era.
- **Takt** measures the era: **feature phase 30 min–3 h**, **feature/era 1.5–6 h**; tail-control
  alarm; **actual measurements only — NEVER LLM estimates**.
- Carried into **`/bk-flow`** as a per-phase SLA gate, **one era (feature) at a time**.
- **`/bk-scheduler`**: one era = one feature = the unit of allocation — one feature to exactly one
  repo on exactly one host; no duplicate allocation.

*Canonical cross-fleet source: crucible `.specify/program/ERA-DEFINITION.md` (commit 697ba70).
This glpnet copy is verbatim-aligned; where they differ, the engineer ruling governs.*
