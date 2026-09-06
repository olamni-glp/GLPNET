<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan: 109 — Differential acceptance, an enforcing gate, and an honest denominator

**Branch**: `109-differential-acceptance-gate` · **Spec**: `spec.md` · **Created**: 2026-09-06

---

## Constitution check

| Gate | Verdict | Evidence |
|---|---|---|
| Spec-first | PASS | `spec.md` written and committed **before** any code (`80ca26f9`). |
| No workaround | PASS | Three defects are being fixed at their cause: an audit that reports without refusing, a scanner blind to its own repo's dominant idiom, and a criterion discharged from one runtime. |
| Single source of truth | PASS | FR-013 **reduces** the number of implementations of the adoption/override rules from a threatened two to one. |
| Language authority (§1.14) | **N/A — and deliberately so** | Nothing here touches the GLP language: no guard, kernel, directive, primitive type or type-system feature. The work is Python tooling, a bash suite section, and a JSON manifest schema. |
| Preserve working code | PASS | The 078 extraction is a **move**; `codeconv.receipts` keeps its public API and its existing tests are the regression proof. |
| Test protocol | PASS | Baseline recorded before any change; suite re-run after. See "Baseline" below. |

**Baseline recorded 2026-09-06T20:07Z, before any change:**

```
evidence_signal_audit.py   exit 1
  regions examined 91 · scope boundary 1329 · regions UNREAD 0
  checks executed 7 pass / 0 fail / 0 not-executable
  conforming 2 · non-conforming 1 · unproven 26 · errors 0
REPL suite (recorded, feature 108 close)   595/595 executed, 0 failures, 2 named not-run
```

---

## Architecture

Three thin, separable pieces. None of them introduces a new runtime dependency, and each can be
reverted alone.

### Piece A — `scripts/lib/adoption_gate.py` (new, **stdlib only**)

The single implementation of feature 078's adoption and override **rules**, reading 078's existing
on-disk **formats**. Public surface:

```
load_adoption(path)            -> {area: state}          # refuses an unknown state
adoption_state(manifest, area) -> "adopted"|"non-adopted"   # raises on absence (FR-010)
override_applies(rec, area, check, reason, now) -> bool     # scope + expiry (FR-011/FR-012)
validate_override(rec)         -> None | raises             # rejects missing expiry AT RECORD TIME
```

**Why a new module rather than moving the existing files.** `codeconv/src/codeconv/receipts/` is
importable only with the codeconv venv on `sys.path`; the audit must run without it (FR-014). The
module therefore lives under `scripts/lib/`, which the audit already owns and which is plain
stdlib. `codeconv.receipts.override` and `.manifest` then **delegate** to it — their public
functions keep their signatures and their tests, so 078 is not re-opened semantically (FR-024).

**How FR-013 is proven rather than asserted.** A test asserts that
`codeconv.receipts.override.applies` and `scripts.lib.adoption_gate.override_applies` are **the same
underlying function object**, and a second test drives the identical override record through both
call paths and requires identical verdicts. A second implementation cannot be introduced without
failing one of them (SC-004).

### Piece B — audit changes (`scripts/evidence_signal_audit.py`)

1. **Refusal (FR-009/010).** After classification, for each non-conforming surface, resolve its area's
   adoption state through Piece A. `adopted` and no valid override ⇒ `EXIT_REFUSED`. `non-adopted`
   ⇒ proceed with a visible marker. No declaration ⇒ `EXIT_USAGE` (error, not a pass).
2. **Unopened-file census (FR-016).** `scan()` already walks every file and filters on
   `SCAN_SUFFIXES`. Count the rejects **by suffix, per region**, and carry them into the report.
   This is the change that converts `regions UNREAD 0` from misleading to true.
3. **The two-step status idiom (FR-017).** Two added patterns: an assignment `VAR=$?` and a decision
   `if [ "$VAR" -eq/-ne ... ]` / `if [ $VAR -eq ... ]`. Scoped to `.sh`/`.ps1`.
4. **Declared suffix set (FR-018).** `SCAN_SUFFIXES` becomes a table of
   `(suffix, scanned, rationale)`, and the unscanned entries are **printed in the report** so the
   gap is visible on every run.
5. **Disposition (FR-019/020/021).** Manifest load refuses a surface with no `disposition`;
   per-disposition required fields enforced; the summary prints per-disposition counts and
   **no blended percentage**.

### Piece C — the differential harness (`scripts/differential_gate.py` + suite Section Y)

A declaration file `.specify/differential/criteria.json` and a runner:

```
{ "criteria": [ { "id", "participants":[≥2], "script", "normalisations":[…],
                  "negative_control": {...} } ] }
```

Each participant names a command. The runner starts every participant, normalises, requires
non-empty **first**, then compares. Outcome ∈ `MEASURED-AGREE | MEASURED-DIVERGE | NOT-MEASURED`,
with the reason and the participant named on NOT-MEASURED. Wired into `test/run_all_tests.sh` as a
new **Section Y**, alongside the existing V-18..V-23 which remain as the reference implementation.

**The V-18..V-23 relationship, stated so it is not mistaken for duplication.** V-18..V-23 are a
hand-rolled Dart-vs-C# comparison. Section Y is the *declared, general* form. This feature does
**not** delete V-18..V-23; it declares the same criterion in the new format and requires the two to
agree — which is itself a differential test of the differential harness.

---

## Risks

| Risk | Mitigation |
|---|---|
| Turning refusal on trips the suite on day one. | Adoption is the phasing mechanism, and only `test-harness` and `reference` are `adopted` today. The blast radius is bounded by `.specify/receipts/adoption.json`, which this feature does not edit. |
| The 078 extraction breaks a codeconv consumer. | The extraction is a delegation; 078's own tests run unchanged as the regression proof, and are run **before and after**. |
| New scan patterns explode the manifest and force a huge declaration burden. | FR-017 adds patterns only to already-scoped regions; the widening (rows in the manifest) is a separate, staged step under the tiered disposition of `Q-olg17-03`. |
| The C# REPL is stale, so Section Y silently measures nothing. | The freshness gate already exists and reads `bin/Debug/net11.0`; Section Y reports NOT-MEASURED naming the participant, which is exactly FR-003. A stale binary can no longer read as green. |

---

## Out of scope, declared

- Promoting the harness to a reusable `bk-guards` capability (buildkit-owned tree).
- Making `.gleam` / `.glp` / `.mjs` **scannable**. FR-018 requires the gap be **declared**; closing it
  is a larger piece of work and is recorded as a follow-up rather than silently attempted.
- Widening the manifest to `codeconv/tests`' 387 sites. The *mechanism* (disposition) lands here;
  the bulk declaration is staged.
