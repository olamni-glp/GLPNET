<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract — Embeddability Service-Box (wave 4)

**Governs**: spec FR-008, FR-010; SC-008; US4/US6. Ruling: G3-A (delivery frame = yngenios), with
`rule-embeddability-api-yngenios-wiring` **RESOLVED 2026-07-20 — Option C, full wiring**.

## Delivery frame

The feature is delivered **inside the yngenios architecture**: the Gleam GLP engine is embedded as the
**controller** across all four frozen spec-056 services — **S1 storage, S2 network, S3 kv, spine** —
over their **shared mailbox binding**, and the yngenios fabric's own test suites pass against the
Gleam-controlled data plane.

## Service-box API (on the engine facade)

- A ratified **service-box contract** and a **service-box API on the engine facade** expose the
  embedded back-end engine to an external controller through the mailbox binding.
- Each of the four services drives the engine **through the mailbox binding**, touching **no frozen
  interface** (any needed change follows the unfreeze protocol).

## Integration boundary (cross-repo only)

- Wiring is **cross-repo integration only**. Yngenios lives at `D:\bstdev\research\yngenios-003`
  (frozen spec-056 Gleam/BEAM data plane), checked out and buildable on the **same machine**.
- **No yngenios sources are imported into this repo.** If the spec-056 seams (C1–C6, frozen) do not
  admit an embedded external controller as-is, that is a **cross-repo escalation** — never a unilateral
  change to either side.
- The **S4 kernel (mint/policy)** remains language-authority-gated per yngenios design 70.

## Store-kernel scope (escalated, not team-resolved)

Whether object persistence is `store_put`/`store_get` **kernels** vs a **host-owned log** remains
**escalated to the engineer** (FR-010). Wave-4 wiring proceeds on the mailbox binding without
pre-empting this decision.

## Acceptance (SC-008)

```text
For each of S1 storage, S2 network, S3 kv, spine:
  start the service against the embedded Gleam BE engine
  → the service drives the engine via the mailbox binding (no frozen-interface touch)
  → the service's own `gleam test` suite passes
Then:
  run one end-to-end object-PUT across the spine on the Gleam-controlled data plane → completes
  → the engineer's contract sign-off is recorded
```
