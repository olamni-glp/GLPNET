<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: Consumer refusal (glpnet-side, FR-008 / FR-009 / FR-011)

The glpnet-owned behaviour of any component that **reads a check verdict**. Implemented in
`codeconv/src/codeconv/receipts/consumer.py`.

## C1 — Refuse a verdict lacking a conforming receipt (FR-008)

Given a verdict for a check in an area the manifest marks **adopted**, the consumer MUST:

1. Resolve the receipt at the pointer / conventional path (FR-022).
2. If **absent or malformed** → treat as **UNREAD** (edge case) and **refuse** — never default to pass.
3. If present → **validate** against the pinned schema. Invalid → refuse, naming the violation.
4. If the receipt's `outcome ∈ {UNREAD, UNSEARCHABLE}` → **not a pass** (FR-007); surface as-is.
5. If `outcome ∈ {PASS, EMPTY}` → the verdict may be treated as successful.

For an area marked **non-adopted**, the verdict remains usable but MUST carry a **visible
non-adoption marker**. For an area **absent from the manifest**, the consumer MUST refuse under
FR-008 and name the missing declaration under FR-011 (FR-020) — absence is an error, not non-adoption.

## C2 — Aggregate propagation (FR-009)

An aggregating check (a parent over children) MUST NOT report success while **any** constituent is
`UNREAD` or `UNSEARCHABLE`. The aggregate outcome is the **worst** child outcome under the ordering
`PASS ≈ EMPTY  <  UNREAD  <  UNSEARCHABLE  <  FAIL` (worst wins). Child outcomes propagate; they are
never summarised away. This closes instance 13 (an aggregate `reconcile` reporting in-sync while a
constituent `link` reported "no spec dirs matched").

## C3 — Refusal message (FR-011)

A refusal MUST name **what was expected, what was found, and where it looked** — sufficient to act
on without re-running. Minimum: `check_id`, expected vs found (e.g. "expected receipt at PATH,
found none"), and the resolved `area`/`run_id`. No refusal may be suppressed by ordinary
configuration (FR-012); the only bypass is a recorded, scoped, expiring override.

## C4 — Missing expected check (FR-013 via FR-023)

After a run, the consumer reconciles the per-run **ExpectedSet** against the receipts present under
the run's receipts dir. Every expected `check_id` with **no** receipt is reported as a **missing
check** — a check that did not run must not be indistinguishable from one that passed.
