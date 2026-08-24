<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 1 — Data model: 083 glptutorial corpus-golden reconciliation

**Date**: 2026-08-24 · **Branch**: `083-glptutorial-corpus-goldens`

Entities are taken from the spec's *Key Entities* and given the fields the requirements actually
force. Every field below is traced to the FR or SC that requires it; a field no requirement needs
is not here.

---

## E1 · Exercise

A tutorial unit: source program, tutorial text, recorded outcome.

| field | type | required by | notes |
|---|---|---|---|
| `chapter` | `ch01…ch13` | FR-005 | scope of this feature: `ch04`, `ch07` only |
| `exercise_id` | string (`MM`) | FR-005 | e.g. `07`, `08` |
| `source_path` | path | FR-002 | **immutable for ch04/07** — ruled byte-exact from book §4.3.1 p 37 |
| `tutorial_md` | path | FR-010 | the prose the outcome is explained against |
| `golden` | → **E2** | FR-001 | exactly one |
| `run` | → **E4** | FR-005 | required for ch07; absent elsewhere today |

**Invariant**: `source_path` for `ch04/07` MUST NOT change. FR-002 ruling (b) keeps the exercise
byte-exact; only its golden changes. A task that edits it violates the ruling **and** the Code
Modification Protocol (a `.glp` file written by Gabi).

---

## E2 · Golden — the recorded outcome

**This is the entity FR-009 changes.** Today a golden can only express a successful load; it must
gain the ability to express a correct refusal.

| field | type | required by | notes |
|---|---|---|---|
| `outcome_kind` | enum | **FR-009** | **NEW** — see below |
| `payload` | structured | FR-001, FR-003 | kind-specific; mechanically comparable, never free prose |
| `backend` | `dart` \| `csharp` | FR-003 | ch04/08 requires BOTH recorded |
| `provenance` | → **E5** | FR-006, FR-008 | why this golden reads as it does |

### `outcome_kind` — the enumeration

| value | means | example in scope |
|---|---|---|
| `loaded` | program loaded and ran; `payload` carries the result bindings | ch04/08 → `F=[5,4,3,2,1]`, no `[WARN]` |
| **`rejected`** | the runtime **correctly refused** the program; `payload` carries the diagnostic identity | **ch04/07** — the ruled case |
| `error` | the run failed for a reason that is *not* a correct refusal | (none in scope; kept so `rejected` cannot absorb genuine breakage) |

🔴 **`rejected` and `error` must be distinct.** Collapsing them would let a real runtime breakage
be recorded as an expected refusal — the exact false-green this repo is trying to eliminate.

**`payload` when `outcome_kind = rejected`** must identify the refusal *mechanically* (diagnostic
code / rule identity + the offending clause), **not** by message text. Message text drifts; FR-001
needs a comparison that survives rewording.

---

## E3 · Proposal — a detected divergence

Read-only output of `codeconv tutorials propose`. **Derived, never stored as truth**: it is
recomputed from corpus-vs-live each run, which is what makes FR-007 meaningful.

| field | type | required by |
|---|---|---|
| `kind` | `layout_normalise` \| `stale_artefact` \| `drift_gap` \| `run_manifest` | FR-001 |
| `exercise_ref` | → E1 | FR-001 |
| `proposal_id` | string | FR-007 | e.g. `spec-violation-ch04-ex07` |
| `remedy_text` | string | FR-010 | 🔴 today `drift-gap-cssg` says "vendor **or** manifest" — corrected to a conjunction (research R-4) |

**Invariant (FR-007)**: after applying a repair, re-running `propose` yields exactly the
unrepaired remainder — never a clean report while any divergence stands (FR-001).

---

## E4 · Run manifest

The deterministic mapping FR-005 requires. Absent today; created by this feature.

| field | type | required by |
|---|---|---|
| `exercise_ref` | → E1 (`chXX/MM`) | FR-005 |
| `program` | path | FR-005 | for ch07: `programs/cssg_modules/` (confirmed, research R-3) |
| `play` | symbol | FR-005 | e.g. `fplayMM` |
| `step_limit` | int | FR-005 | the `:limit` value |

**Invariant (SC-004)**: every ch07 exercise resolves to **exactly one** `(program, play, limit)`
triple — no ambiguous and no missing mappings. A missing mapping is a failure, not a default.

---

## E5 · Change provenance — why a golden reads as it does

Carries FR-006 (approval + rationale, recoverable) **and** FR-008 (stale vs regression). One
entity, because C4 makes the FR-008 discriminator a property of the rationale itself.

| field | type | required by | notes |
|---|---|---|---|
| `change_class` | `repair` \| `recapture` | FR-008 | see the C4 rule below |
| `approved` | bool | FR-006 | `--approve` was given |
| `rationale` | string | FR-006 | `--rationale` text; MUST remain recoverable from the corpus |
| `cited_cause` | string \| null | FR-008 | commit / PR / spec amendment that changed runtime behaviour |

### 🔴 The C4 rule, mechanised

> `change_class = recapture` is permitted **only if** `cited_cause` is non-null and names a
> specific runtime change. **Absent a citation the change is a `repair`.**

This makes FR-008 checkable without reading anyone's intent, and satisfies SC-006 (a reviewer can
tell stale-golden from runtime-change **without reading the implementation**).

**In scope, worked**: ch04/08 → `recapture`, `cited_cause` = the C# `is_list` guard fix.
ch04/07 → `repair` (the golden was simply false; no runtime change caused it).

---

## E6 · Vendored substrate

The corpus-local copy of a live sibling tree, plus the manifest the drift guard recomputes against.

| field | type | required by |
|---|---|---|
| `sibling_path` | path | FR-004 | `programs/cssg_modules/` |
| `vendored_path` | path | FR-004 | corpus-local copy |
| `manifest_digest` | hash per file | FR-004 | byte-exact equivalence, per `ch07-specification-input-prompt.md:26` |
| `scope_chapter` | `chXX` | **R-2** | **NEW** — see below |

🔴 **`scope_chapter` exists because the guard is saturated.** Measured 2026-08-24: `sync --check`
reports **67 drift lines across all 13 chapters** on an unmodified tree and exits 1. Without a
per-chapter scope there is no way to state "ch07 is clean", so FR-004's failure signal carries no
information (research R-2). The guard must be able to answer per chapter before vendoring adds to
it.

**Two drift classes exist and must stay distinguishable** (both observed today):
`vendored ≠ sibling` (the substrate moved) and `vendored ≠ manifest` (the record moved).

---

## Relationships

```
Exercise 1─1 Golden 1─1 ChangeProvenance
Exercise 0..1─1 RunManifest          (required for ch07)
Exercise 1─0..n Proposal              (derived, recomputed each run)
VendoredSubstrate 1─n Exercise        (ch07 → programs/cssg_modules/)
```

## State transitions — Golden

```
                    propose (read-only, derived)
                              │
   [current golden] ──────────┴────────────► [divergence detected]
                                                     │
                            --apply --approve --rationale
                                                     │
                                                     ▼
                                        change_class = repair | recapture
                                        (recapture REQUIRES cited_cause)
                                                     │
                                                     ▼
                                            [reconciled golden]
                                                     │
                                    re-run propose ──┴──► remainder only (FR-007)
```
