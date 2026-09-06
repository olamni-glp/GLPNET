<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Data Model — feature 108

Three records. Everything else is derived.

---

## 1. `SignalSurface` — one entry in the declared manifest (FR-014a)

Lives in `.specify/evidence-signals/manifest.json`, checked in, and **is** the denominator for
SC-002.

| field | type | rule |
|---|---|---|
| `id` | string | stable, kebab-case, unique. Never renamed once published — a renamed id reads as a removed surface plus a new unproven one. |
| `path` | string | repo-relative path to the producing surface. Forward slashes always, so the manifest is identical on Windows and Linux. |
| `symbol` | string | the observable: a method, predicate, verb, or exit-status contract. |
| `kind` | enum | `wait` \| `idle-predicate` \| `liveness-flag` \| `exit-status` \| `emptiness`. Determines which FR applies. |
| `consumers` | string[] | ≥1 entry. A surface with no consumer is not evidence-bearing and does not belong here (FR-002). |
| `governed_by` | string[] | subset of `FR-004`, `FR-007`, `FR-012`. At least one. |
| `conformance_check` | string \| null | dotted path to the pytest test that proves it. `null` ⇒ classified **unproven**, never conforming (FR-015). |
| `negative_control` | string \| null | dotted path to the test proving the check can fail (FR-018a). Required whenever `governed_by` includes `FR-004`. |
| `iterations` | integer \| null | required when `governed_by` includes `FR-004`; **40** (FR-018a). |
| `contention` | string \| null | required alongside `iterations`; describes the load the pass was obtained under (FR-018). |
| `owner` | string | the lane that owns the fix. May be another lane — that is how SC-001 is met by disclosure. |
| `disposition` | enum | `owned` \| `disclosed` \| `not-reproduced-on-this-build`. |
| `notes` | string | free text; where an instance number from `spec.md` is cited. |

**Validation rules**
- `id` unique across the manifest; duplicate ⇒ error.
- `conformance_check == null` ⇒ classification is `unproven` regardless of any other field.
- `governed_by` contains `FR-004` and `negative_control == null` ⇒ **error**, not unproven: the
  manifest is asserting a contention property with no way to be wrong.
- `disposition == "disclosed"` requires `owner` to name a lane other than this one.

---

## 2. `ConformanceReport` — the audit's output (FR-014, FR-019, FR-020)

Written to `.specify/evidence-signals/report.json` and rendered to stdout.

| field | type | meaning |
|---|---|---|
| `generated_utc` | string | ISO-8601 |
| `manifest_sha256` | string | binds the report to the exact manifest it scored |
| `surfaces` | `SurfaceVerdict[]` | one per manifest entry |
| `scan_only` | `ScanHit[]` | scan found it, manifest does not list it ⇒ **ERROR** (FR-014b) |
| `manifest_only` | string[] | manifest lists it, scan cannot locate it ⇒ **ERROR** (FR-014b) |
| `regions_examined` | string[] | paths actually read |
| `regions_unexamined` | `{path, reason}[]` | read failure, exclusion, or unsupported type — **reported, never omitted** (FR-020) |
| `totals` | object | `conforming`, `non_conforming`, `unproven`, `errors` |
| `receipt_path` | string | the 078-conforming receipt for this run (FR-017) |

`SurfaceVerdict`: `{id, classification: conforming|non-conforming|unproven, failed_frs: string[],
consumers: string[], evidence: string|null}` — FR-019 requires `failed_frs` and `consumers` to be
present on every non-conforming or unproven verdict, so a reader can act without re-deriving.

---

## 3. `AdoptionDeclaration` — reuses 078's, does not define a new one (FR-006a)

This feature declares **no new record type** for adoption. It reuses feature 078's per-area adoption
manifest and its informed-consent override verbatim (FR-006b). The only addition is that an area's
declaration is read as covering both features.

**Consequence, stated because it is a real constraint**: an area that declared non-adoption for 078
is also non-adopting for 108. That is deliberate — one declaration, one override, one audit trail.
Splitting them would let an area adopt the cheaper half, and the fleet already has one measured case
of a guard that existed in one of two scripts and therefore was not a guard.

---

## State transitions

A surface moves `unproven → conforming` only by acquiring a `conformance_check` **and** (where
FR-004 applies) a `negative_control` that is demonstrated to fail. There is no path from `unproven`
to `conforming` by assertion, edit, or age. A surface moves `conforming → non-conforming` whenever
its check fails, which is the point of the check being live rather than recorded (FR-016).
