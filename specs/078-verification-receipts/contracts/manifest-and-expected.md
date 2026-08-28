<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: Adoption manifest (FR-019/020/021) + per-run expected-set (FR-023)

One **absence-is-an-error** rule applied at two granularities (research D7): an *area* that never
declares, and a *run* that never declares its expected checks.

## M1 — Per-repo adoption manifest (FR-019)

- **Location**: `.specify/receipts/adoption.json`, checked in. One manifest **per repo** (D3 —
  glpnet declares its own areas; buildkit declares its own). It is the **sole authority** for
  whether FR-008 binds.
- **Enumeration requirement**: the manifest MUST list **every** FR-017 area in this repo's scope.
  For glpnet that is `build-gate`, `coop`, `roadmap-sync`, `test-harness` (the buildkit-side
  `3rtask`, `codexreview` live in buildkit's manifest).

```json
{
  "areas": [
    { "area": "build-gate",   "state": "non-adopted", "since": "2026-08-18" },
    { "area": "coop",         "state": "non-adopted", "since": "2026-08-18" },
    { "area": "roadmap-sync", "state": "non-adopted", "since": "2026-08-18" },
    { "area": "test-harness", "state": "non-adopted", "since": "2026-08-18" }
  ]
}
```

- **Rules**: an area absent from `areas` is an **error** (FR-020), refused under FR-008 and named
  under FR-011. `state: adopted` binds FR-008 for that area; `non-adopted` keeps verdicts usable
  behind a visible marker. Emitting a receipt does **not** imply a declaration. SC-002's
  denominator is FR-017's full enumeration (FR-021), so an empty/partial manifest fails FR-020
  before it can trivially satisfy SC-002.
- **MVP note**: at first ship the reference check is `adopted`; the four glpnet areas start
  `non-adopted` and flip to `adopted` as each US4 retrofit lands — that transition is the honest,
  visible record of incremental coverage (FR-017/018).

## M2 — Per-run expected-check set (FR-023)

- **Location**: `<receipts-root>/<run-id>/expected.json`, written at run start.
- **Content**: `{ "run_id": "...", "expected_checks": ["check_id", ...] }`.
- **Rules**: a run with **no** `expected.json` is an **error** — an unverifiable run **refuses**
  rather than reports (FR-023). This defines FR-013's "expected" and nothing else does. After the
  run, every `check_id` in `expected_checks` lacking a receipt under the run's dir is reported as a
  **missing check** (FR-013) — a vanished check is as loud as an un-adopted area.
- **Anti-ratchet**: the expected set is declared per run, **not** derived from the last successful
  run — a check that vanished two runs ago must never become permanently "not expected".
