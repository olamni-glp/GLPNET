<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 4d90c7b3-6a12-4e58-9f31-c8b52e7a10d6
-->

# Phase 1 data model — Verification receipts

**Feature**: `078-verification-receipts` · **Date**: 2026-08-18

Five entities. All are **data**, not classes with behaviour: two runtimes must produce them and the
bash one cannot import Python, so the shared artifact is the document (plan → Structure decision).

---

## 1. Outcome

A closed enumeration of exactly five values (FR-006). Exactly one applies to any check.

| value | meaning | successful? |
|---|---|---|
| `PASS` | target resolved, examined in full, findings present and reported | ✅ |
| `EMPTY` | target resolved, examined **in full**, genuinely nothing there | ✅ |
| `UNREAD` | target exists and holds items; some or all were **not examined** | ❌ |
| `UNSEARCHABLE` | target could not be examined at all — absent, unreachable, unsupported, wrong format, permission-refused | ❌ |
| `FAIL` | target resolved and examined; the check found a problem | ❌ |

**Rules.**

- Only `PASS` and `EMPTY` may be treated as success, aggregated into success, or rendered in a way a
  reader could mistake for success (FR-007).
- `EMPTY` requires `examined_total == target_total`. An `EMPTY` that cannot demonstrate full
  examination is `UNREAD`. This is the single most abusable distinction in the model — the spec's
  Assumptions warn that if legitimate emptiness became impossible to express, engineers would suppress
  the mechanism, so `EMPTY` stays a real pass, but it must be *earned*.
- `UNSEARCHABLE` requires a non-empty `reason` (FR-011).

**Validation.** A value outside the five is not a receipt. A consumer treats it as a missing receipt
and refuses (FR-008), rather than guessing the nearest neighbour.

## 2. Receipt

The evidence a check ran, bound to exactly one verdict.

| field | type | requirement | notes |
|---|---|---|---|
| `schema_version` | string | FR-004 | contract version this receipt claims |
| `area` | string | FR-017 | MUST be one of the six enumerated areas |
| `check_id` | string | FR-013/FR-023 | stable id; MUST appear in the run's expected-checks manifest |
| `run_id` | string | R2 | `<UTC ts>-<8 hex>`; unique per `(area, run_id)` |
| `target_requested` | string | FR-003 | what the check was *asked* to examine |
| `target_resolved` | string | FR-003 | what it *actually* resolved to — POSIX-normalised |
| `target_total` | integer \| null | FR-010 | true size of the target; `null` only when genuinely unknowable, which is itself `UNSEARCHABLE` |
| `examined_total` | integer | FR-002 | count examined |
| `skipped` | array of `Skip` | FR-002 | bounded (see Bounding) |
| `skipped_total` | integer | FR-005/FR-010 | **always** the true count, even when `skipped` is truncated |
| `outcome` | Outcome | FR-006 | exactly one of the five |
| `reason` | string \| null | FR-011 | required for `UNREAD`, `UNSEARCHABLE`, `FAIL` |
| `started_at` / `ended_at` | RFC3339 UTC | FR-002 | `ended_at` null ⇒ the run crashed; never a pass |
| `truncated` | `Truncation` \| null | FR-005 | self-declared; null means nothing was dropped |
| `override` | `Override` \| null | FR-012 | present only when a refusal was overridden |
| `children` | array of receipt pointers | FR-009 | for aggregates |

**Derived rules.**

- **Crash (Edge Cases).** `ended_at == null` ⇒ outcome MUST NOT be `PASS`/`EMPTY`. A partial run never
  presents as a whole one.
- **Aggregation (FR-009).** An aggregate's outcome is the *worst* of itself and every child:
  `UNSEARCHABLE > UNREAD > FAIL > PASS/EMPTY` for the purpose of "may this be reported as success".
  A parent cannot report clean while any child is `UNREAD` or `UNSEARCHABLE`.
- **Reconciliation (FR-010).** `examined_total + skipped_total ≤ target_total` where `target_total`
  is known. A receipt violating it is self-inconsistent and MUST be refused — this is fault-injection
  scenario 3.5 (a falsified count exceeding the target's true size).
- **Identity conflict (Edge Cases).** Two receipts for the same `check_id` disagreeing on
  `target_resolved` surface as a conflict; they are not resolved by precedence.

## 3. Skip and Truncation

```
Skip        { item: string, reason: string }        # reason is mandatory (FR-002)
Truncation  { field: string, kept: int, total: int, by: "count" | "bytes" }
```

**Bounding (FR-005, R5).** Enumerations are capped; `*_total` counters are **never** capped. A byte
backstop caps any single string field. Every drop is declared in `truncated` — a bounded receipt is
still honest, and a reader can always tell a small target from a truncated view of a large one.

## 4. Adoption manifest (FR-019/020/021)

One checked-in document enumerating **all six** areas.

```
{ schema_version, entries: [ { area, state: "adopted" | "not-adopted", set_on: date, note? } ] }
```

- Every area named in FR-017 MUST appear. An area **absent** from the manifest is an **error** — not a
  pass, and not equivalent to declared non-adoption (FR-020).
- Behaviour never implies adoption: emitting a conforming receipt does not constitute a declaration
  (FR-019).
- SC-002's denominator is this enumeration, not the set that happens to have declared (FR-021), so an
  empty manifest fails FR-020 before it can satisfy SC-002.

## 5. Expected-checks manifest (FR-023)

The per-run declaration that gives FR-013 its meaning.

```
{ schema_version, run_id, expected: [ check_id, … ] }
```

- A run with **no** expected-checks manifest is not a run in which nothing was expected; it is
  unverifiable and MUST refuse.
- A `check_id` in `expected` with no receipt at the end of the run ⇒ the check **did not run**, and
  that absence is reported (FR-013).
- A receipt whose `check_id` is not in `expected` is a surprise check — reported, not silently
  accepted.

**Why two manifests and not one (R6).** They answer different questions but fail the same way, and
they share one rule: *absence of a declaration is an error.* One rule, two documents, no second place
for a silent pass to hide.

## 6. Override (FR-012)

```
{ briefing, acknowledged_by, rationale, scope: { area, check_id, reason }, expires_on }
```

- No indefinite override: `expires_on` is mandatory.
- An override applies **only** within its recorded scope, so one recorded once can never silently
  authorise every future refusal of its kind.
- It remains visible in the receipt permanently — it converts a refusal into a *recorded, expiring,
  scoped* proceed, never into a pass.
- Overrides are engineer decisions; the mechanism records them and does not grant them (Assumptions).

---

## Relationships

```
ExpectedChecks(run) ──expects──> check_id ──produces──> Receipt ──may carry──> Override
                                                          │
AdoptionManifest ──governs whether FR-008 binds──> area <─┘
                                                          │
                                              Receipt ──children──> Receipt   (FR-009)
```

## State transitions

A receipt has no lifecycle: it is written once per `(area, run_id)` and never mutated
(constitution VI-a). A re-run produces a **new** `run_id`. The only "transition" is a consumer's
verdict on it — accepted, or refused with a named reason — and that verdict is itself receipted when
the consumer is a check.
