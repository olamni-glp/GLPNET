<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: Conformance fixture (FR-024)

A fixture that **ships with the buildkit-owned contract** and is run by **every** implementation
(buildkit reference emitter, glpnet Python emitter, the bash emitter). It is what makes single
authority safe: instead of *trusting* that two repos agree, each **runs the fixture** and its
**own output is itself a receipt** — so conformance is demonstrated under the invariant this
feature defines, not asserted.

## F1 — What the fixture does

1. Drives the emitter-under-test through each terminal outcome: `PASS`, `EMPTY`, `UNREAD`,
   `UNSEARCHABLE`, `FAIL`, plus a **bounded/truncated** case and an **overridden** case.
2. Validates each produced sidecar against the pinned schema.
3. Emits **its own receipt** naming: `contract_version`, the emitter identity (the resolved
   target — FR-003), `examined_count` = number of outcome cases exercised, `total_count` = the
   full case set, and outcome `PASS` iff every case validated. A partial fixture run is `UNREAD`
   (FR-016 — the fixture is subject to its own invariant), never a silent green.

## F2 — Non-collapse assertions (US2)

The fixture asserts the three "nothing found" cases produce **three distinct** receipts:
- EMPTY: `resolved == true`, `examined_count == total_count`.
- UNREAD: `resolved == true`, `examined_count < total_count`, unexamined count stated.
- UNSEARCHABLE: `resolved == false`, `unresolved_reason` present.
Any two of these validating identically fails the fixture.

## F3 — Reconciliation assertion (FR-010)

The fixture includes a case whose `examined_count` is **deliberately set to exceed** `total_count`
and asserts the validator **rejects** it (US3 scenario 5 — a falsified count is detectable).

## F4 — Where glpnet runs it

`codeconv/tests/faultinj/` invokes the fixture against the Python reference emitter and the bash
emitter as a `pytest` target; its receipt is written under the run's receipts dir like any other
check. The fixture's own non-execution is loud (FR-016): the per-run ExpectedSet includes the
fixture `check_id`, so a run where it did not produce a receipt reports a missing check (FR-013).
