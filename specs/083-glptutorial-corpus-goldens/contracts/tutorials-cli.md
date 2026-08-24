<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract — `codeconv tutorials` CLI surface (feature 083)

**Date**: 2026-08-24 · The corpus tool is a **CLI**, so its contract is its command surface, exit
codes and report shape. Everything below is either **observed today** or **required by this
feature**; the two are labelled and never mixed.

---

## C-1 · `tutorials propose` — the divergence report (FR-001, FR-007, FR-010)

### Observed today

```
$ codeconv tutorials propose
4 proposal(s) (read-only; --apply requires --approve + --rationale):
  [drift_gap]        ch07     (drift-gap-cssg)
  [run_manifest]     ch07     (run-manifest-ch07)
  [stale_artefact]   ch04/08  (stale-golden-ch04-ex08)
  [layout_normalise] ch04/07  (spec-violation-ch04-ex07)
```

### Required

| # | requirement | from |
|---|---|---|
| C-1.1 | Read-only by default. **`--apply` MUST refuse without both `--approve` and `--rationale`.** | FR-006 |
| C-1.2 | MUST NOT report a clean corpus while any divergence exists. | FR-001 |
| C-1.3 | After a repair, MUST report **exactly** the unrepaired remainder. | FR-007 |
| C-1.4 | Each proposal carries a stable `proposal_id`, so "this one is fixed" is citable. | FR-007 |
| C-1.5 | 🔴 The `drift-gap-cssg` remedy text MUST state vendoring **and** manifest, not "or". | FR-010, C2 |

**C-1.5 is a live defect.** Today's string reads *"Vendor cssg_modules/ **or** record a
run-manifest."* The spec's C2 rules that "or" wrong: they address different defects and both are
MUSTs. A remedy that offers an either/or invites delivering half the requirement.

### Exit codes

| code | meaning |
|---:|---|
| 0 | zero proposals |
| non-zero | ≥1 proposal outstanding |

---

## C-2 · `tutorials sync --check` — the drift guard (FR-004, SC-003)

### 🔴 Observed today — SATURATED

```
$ codeconv tutorials sync --check
exit = 1
67 drift lines, ALL 13 chapters   (ch04: 5, ch07: 5, ch06: 7, …)
```

Two drift classes appear, and both must remain distinguishable:

| line form | means |
|---|---|
| `vendored content differs from **sibling**: <path>` | the live substrate moved |
| `vendored content differs from **manifest**: <path>` | the recorded digest moved |

### Required

| # | requirement | from |
|---|---|---|
| C-2.1 | A modification to the ch07 substrate MUST cause a non-zero exit **naming the drifted path**. | FR-004 |
| C-2.2 | Byte-exact equivalence is the comparison, per `ch07-specification-input-prompt.md:26`. | FR-004 |
| C-2.3 | 🔴 **MUST support per-chapter scoping** (e.g. `--chapter ch07`), so "ch07 is clean" is expressible. | **R-2** |
| C-2.4 | Sibling-drift and manifest-drift MUST stay separately reported. | R-2 |

### 🔴 Why C-2.3 is mandatory rather than convenient

FR-004 is a **signal** requirement: modifying the substrate must *cause* a failure. Today the guard
fails on an untouched tree, so its failure **does not depend on the substrate at all** — the signal
is unconditioned. Vendoring `cssg_modules/` into a guard in this state satisfies FR-004's letter
and none of its intent.

**A check whose result does not depend on what it checks is the defect class feature 078 exists to
eliminate.** Per-chapter scoping is the minimum that makes the ch07 signal real, without dragging
57 out-of-scope drift lines into this feature.

### Exit codes

| code | meaning |
|---:|---|
| 0 | in-scope tree matches sibling and manifest |
| non-zero | ≥1 drift, each named on its own line |

---

## C-3 · Golden record format (FR-009, FR-003, FR-001)

### Required — the `outcome_kind` discriminator

| value | payload MUST carry |
|---|---|
| `loaded` | the result bindings, mechanically comparable |
| **`rejected`** | the refusal's **mechanical identity** (diagnostic/rule id + offending clause) |
| `error` | the failure detail — **never** conflated with `rejected` |

| # | requirement | from |
|---|---|---|
| C-3.1 | A golden MUST be able to record a **correct refusal** as a first-class outcome. | **FR-009** |
| C-3.2 | `rejected` MUST NOT be expressed as free prose — comparison must survive rewording. | FR-001 |
| C-3.3 | `rejected` and `error` MUST be distinct values. | FR-009 + Edge Case |
| C-3.4 | ch04/08's golden MUST record **both** `dart` and `csharp` backends. | FR-003 |
| C-3.5 | ch04/07's **source stays byte-exact**; only its golden changes. | **FR-002 ruling (b)** |

---

## C-4 · Change provenance (FR-006, FR-008, SC-005, SC-006)

| # | requirement | from |
|---|---|---|
| C-4.1 | Every applied change records `approved` **and** `rationale`. | FR-006 |
| C-4.2 | The rationale MUST be recoverable from the corpus afterwards. | FR-006, SC-005 |
| C-4.3 | Every change carries `change_class ∈ {repair, recapture}`. | FR-008 |
| C-4.4 | 🔴 `recapture` is permitted **only** with a non-null `cited_cause` naming a specific runtime change (commit/PR/spec amendment). **Absent a citation it is a `repair`.** | FR-008, C4 |
| C-4.5 | A reviewer can classify each changed golden **without reading the implementation**. | SC-006 |

**C-4.4 is what stops a re-capture silently blessing a regression** — the spec's first Edge Case.

**Worked, in scope**: ch04/08 → `recapture`, cited cause = the C# `is_list` guard fix.
ch04/07 → `repair` (no runtime change; the golden was simply false).

---

## C-5 · Run manifest (FR-005, SC-004)

| # | requirement | from |
|---|---|---|
| C-5.1 | Each ch07 exercise resolves to exactly one `(program, play, step_limit)`. | FR-005, SC-004 |
| C-5.2 | `program` for ch07 is **`programs/cssg_modules/`** — confirmed, not `_v2`. | C1, R-3 |
| C-5.3 | A missing mapping is a **failure**, never a silent default. | SC-004 |
| C-5.4 | The manifest is drift-checkable by C-2. | FR-005 |

---

## C-6 · Out-of-contract — stated so it cannot be assumed

- **Not** repairing the 57 out-of-scope drift lines (ch01–ch03, ch05, ch06, ch08–ch13). Reported,
  not fixed — the spec scopes this feature to ch04 and ch07.
- **Not** re-litigating the C# `is_list` guard fix.
- **Not** any GLP language or type-system change (Constitution IV-a). The B10 finding — that a
  byte-exact book §4.3.1 transcription is rejected by the guard rules — is **reported to Udi**,
  never fixed here.
- **Not** a new approval mechanism; the existing `--apply --approve --rationale` flow is used.
