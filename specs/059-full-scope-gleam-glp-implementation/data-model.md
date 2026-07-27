<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 1 Data Model — Full-scope Gleam GLP implementation

These are the **governance/traceability entities** the feature manipulates (spec § Key Entities). They
are documents/registers under `docs/research/fullscope-gleam/` and this spec dir, not database tables.

## Entity: Work Package (WP)

The unit of the FINAL plan. Authoritative inventory:
`docs/research/fullscope-gleam/feature-outline-plan-FINAL-2026-07-20.md`.

| Field | Type | Notes |
|---|---|---|
| `id` | slug | e.g. `freeze-runtime-term-heap`, `verify-link-inbound-pump`, `close-multiagent-…` |
| `kind` | enum | `freeze` \| `guard` \| `verify` \| `close` \| `build` \| `rule-request` \| `accept` |
| `wave` | 1..5 | 1 freeze/guard · 2 verify/rule-request · 3 close · 4 build · 5 accept |
| `backing_detail_ids` | [detail_id] | inventory rows this WP discharges |
| `deliverable` | text | the artifact/behavior produced |
| `acceptance_evidence` | command \| test-path \| artifact | **restart-safe**, checkable from a fresh session (FR-013) |
| `deps` | [WP.id] | post-binding dependencies (plan asserts zero dangling) |
| `risk` | S/M/L | plan sizing |
| `status` | enum | `captured` → `in-progress` → `checkpointed` → `accepted` (or `blocked` / `ruled-out`) |

**State transitions**:
`captured → in-progress → checkpointed → accepted`.
Side edges: `→ blocked` (unruled escalation gate) ; `→ ruled-out` (recorded engineer ruling only).

**Key relationship — verify→close activation edge**: a `verify` WP emitting an **ABSENT** verdict
*activates* its paired `close` WP (that close WP is `captured` until then). A **DELIVERED** verdict
marks its detail_ids `delivered-confirmed` with no close work.

## Entity: Frozen-Interface Register entry

Live register: `docs/research/fullscope-gleam/frozen-interface-register.md`.

| Field | Type | Notes |
|---|---|---|
| `interface` | name | a delivered interface pinned in wave 1 (runtime/term/heap, compiler pipeline, engine, REPL, codecs, link wire, transport seam, …) |
| `protected_test_files` | [path] | tripwire tests that must not be modified or shrunk |
| `unfreeze_path` | ref | the rule-request an interface change MUST file + await a ruling (FR-002) |

**Invariant**: no WP changes a frozen interface without a recorded unfreeze ruling; the pinned suites
are **grow-only** (never shrink, never go red) for the feature's whole duration (FR-003, SC-001).

## Entity: Escalation Register entry

| Field | Type | Notes |
|---|---|---|
| `id` | slug | e.g. `rule-quic-sideprocess-relay`, `rule-embeddability-api-yngenios-wiring` |
| `status` | enum | `open` \| `resolved` |
| `ruling_cite` | ref | (resolved) the recorded ruling |
| `due_before` | gate | (open) the WP/wave that must not start until it is ruled |

Current rows: see `research.md` open-items ledger (FR-011; SC-009 requires zero `open` at close).

## Entity: Coverage / Traceability row

The 154 inventory detail_ids (+ open-items rows) each map to their WPs and a terminal disposition.

| Field | Type | Notes |
|---|---|---|
| `detail_id` | id | inventory capability id |
| `capability` | text | short name |
| `wps` | [WP.id] | verify/close/build WPs touching it |
| `terminal_disposition` | enum | `closed-to-parity` \| `delivered-confirmed` \| `ruled-out-of-scope` |

**Invariant (SC-003)**: 100% of rows reach a terminal disposition — zero silent exits.

## Entity: Gate Ruling

| Field | Type | Notes |
|---|---|---|
| `id` | id | `G1`..`G5`, `G3-A`, and future rulings |
| `decision` | text | the binding scope decision |
| `binds` | scope | what it fixes (e.g. G2 multiagent in-scope; G4 parity-normative; G3-A yngenios delivery frame) |

Rulings are the **only** mechanism by which scope changes (spec Assumptions; FR-012). Authoritative
text: `docs/research/fullscope-gleam/phase2-verify/rulings.md`.

## Aggregate relationships

```text
Gate Ruling ──binds──> scope of ──> Work Package(s)
Work Package ──backing_detail_ids──> Coverage row(s)
verify WP ──ABSENT verdict──activates──> paired close WP
Frozen-Interface Register ──protects──> delivered Work Packages' interfaces
Escalation Register.open ──due_before──blocks──> dependent wave-4 Work Package(s)
```
