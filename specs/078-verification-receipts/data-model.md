<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 1 Data Model: Verification receipts

Entities are the shapes the receipt contract defines. The authoritative machine schema is
**owned by buildkit** (FR-024); this file is the design of those shapes and the validation rules
glpnet's reference implementation enforces. Field modes: `req` = required, `opt` = optional.

## Receipt

The evidence a check ran, bound to exactly one verdict (FR-002, FR-004).

| Field | Mode | Type | Rule |
|---|---|---|---|
| `schema_version` | req | string | The pinned buildkit contract version this receipt conforms to (FR-024). |
| `contract_version` | req | string | Same as above, recorded on the receipt so emitter/consumer skew is visible (research D3). |
| `check_id` | req | string | Stable identity of the check within its area (FR-013 uses it to detect a missing expected check). |
| `area` | req | enum | One of the FR-017 areas: `3rtask \| codexreview \| build-gate \| coop \| roadmap-sync \| test-harness \| reference`. |
| `resolved_target` | req | Target | The target **as actually resolved at run time** (FR-003), never as requested. |
| `outcome` | req | Outcome | Exactly one classification (FR-006). |
| `examined_count` | req | int ≥ 0 | Items actually examined. |
| `total_count` | req | int ≥ 0 \| `unknown` | True target size (FR-010). `EMPTY` requires `examined_count == total_count`. |
| `skipped` | req | list<Skip> | Each skipped item with a reason (FR-002); enumeration capped (FR-005), `skipped_total` always present. |
| `examined` | opt | list<string> | Optional enumeration, **capped** at the declared max (FR-005). |
| `truncated` | req | Truncation | States whether any enumeration/field was truncated and by how much (FR-005) — `{enumerations: bool, dropped: int, byte_capped: [field...]}`. |
| `ran_at` | req | timestamp | When the check ran (FR-002). |
| `override` | opt | Override | Present only if a refusal was overridden (FR-012); remains visible thereafter. |
| `verdict_pointer` | req | string | Back-pointer from the verdict to this sidecar path (FR-022); the receipt records its own canonical path. |

**Invariants enforced by the reference validator**
- `outcome == EMPTY` ⇒ `resolved_target` is resolved **and** `examined_count == total_count` (D4).
- `outcome == UNREAD` ⇒ `total_count` known **and** `examined_count < total_count`.
- `outcome == UNSEARCHABLE` ⇒ `resolved_target` carries an unresolved reason; counts are `unknown`.
- `examined_count` may never exceed `total_count` when `total_count` is known (FR-010; catches a falsified count, US3 scenario 5).
- A receipt whose enumeration exceeds the declared cap MUST set `truncated.enumerations = true` and record `dropped` (FR-005) — a bounded receipt is still honest.

## Target

What a check examines, identified by whatever makes it unambiguous in its domain (FR-003).

| Field | Mode | Type | Rule |
|---|---|---|---|
| `kind` | req | enum | `path \| revision \| host \| root \| cursor \| item-set`. |
| `identity` | req | string | The resolved identifier (e.g. absolute path, commit sha, host name, cursor position). |
| `requested` | opt | string | What was asked for, if it differs from `identity` — a divergence is visible (FR-003; catches instances 9, 10). |
| `resolved` | req | bool | `false` ⇒ the outcome MUST be `UNSEARCHABLE` (edge case: retired root / wrong directory). |
| `unresolved_reason` | opt | string | Required when `resolved == false` (FR-011). |

## Outcome

The five-valued classification (FR-006). Exactly one per verdict.

| Value | Successful? | Meaning |
|---|---|---|
| `PASS` | yes | Ran, examined items, found no problems. |
| `EMPTY` | yes | Resolved and examined in full; genuinely nothing there. **A legitimate pass** (Assumptions — must stay expressible). |
| `UNREAD` | **no** | Target exists and holds items; some/all were not examined. States how many were left unexamined. |
| `UNSEARCHABLE` | **no** | Could not be examined at all (absent, unreachable, unsupported, wrong format, permission-refused). Names the reason. |
| `FAIL` | no | Ran, examined, found a problem. |

## AdoptionManifest (per-repo, FR-019/020/021)

The single checked-in enumeration of **every** FR-017 area, the sole authority for whether FR-008 binds.

| Field | Mode | Type | Rule |
|---|---|---|---|
| `areas` | req | list<AreaEntry> | MUST enumerate every FR-017 area for this repo's scope. |
| AreaEntry.`area` | req | enum | One FR-017 area. |
| AreaEntry.`state` | req | enum | `adopted \| non-adopted`. |
| AreaEntry.`since` | req | date | When the state was set (FR-019). |

**Rules**: an area **absent** from the manifest is an **error** (FR-020) — not a pass, not
non-adoption; a consumer encountering an unlisted area refuses under FR-008 and names the missing
declaration under FR-011. Emitting a conforming receipt does **not** by itself constitute a
declaration. SC-002's denominator is FR-017's enumeration, not the declared subset (FR-021).

## ExpectedSet (per-run, FR-023)

Declares, in advance, the set of checks a run expects to contain. Defines FR-013's "expected".

| Field | Mode | Type | Rule |
|---|---|---|---|
| `run_id` | req | string | The run this declaration governs. |
| `expected_checks` | req | list<string> | `check_id`s expected in the run. |

**Rules**: a run with **no** ExpectedSet is an **error** (FR-023) — an unverifiable run refuses
rather than reports. After the run, each expected `check_id` with no receipt at
`<receipts-root>/<area>/<run-id>/<check-id>.receipt.json` is reported as a **missing check**
(FR-013) — indistinguishable-from-passed is exactly what this forbids. Same absence-is-an-error
rule as the manifest (research D7).

## Override (FR-012)

A recorded engineer decision to proceed past a refusal. Engineer-only (Assumptions), never granted by the mechanism.

| Field | Mode | Type | Rule |
|---|---|---|---|
| `briefing` | req | string | What is being overridden. |
| `acknowledged` | req | bool | Explicit acknowledgement. |
| `rationale` | req | string | Why (SC-006 — 100%, zero silent suppressions). |
| `scope` | req | Scope | `{area, check, reason}` — the override applies to nothing beyond this. |
| `expiry` | req | timestamp | **Mandatory** — no indefinite override. |

Reuses the `bk-guardian` informed-consent shape (research D6); an override outside its recorded
scope or past its expiry is inert and the underlying refusal stands.

## State transitions (a check run)

```
resolve target ──not-resolvable──▶ UNSEARCHABLE (never clean)
      │ resolved
      ▼
examine ──crash/stop-early──▶ UNREAD (partial never presents as whole)
      │ examined in full
      ▼
total==0 ? ──yes──▶ EMPTY (legitimate pass)
      │ no
      ▼
problem found ? ──yes──▶ FAIL   ──no──▶ PASS
```

Every terminal state emits a Receipt; a verdict without one is refused by the consumer as
incomplete (FR-008) and treated as UNREAD (edge case: missing/malformed receipt).
