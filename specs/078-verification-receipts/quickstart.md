<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 9c14e8a5-0b73-4f26-a8d1-6e2f37b94c08
-->

# Quickstart — Verification receipts

**Feature**: `078-verification-receipts` · **Date**: 2026-08-18

Three things you will actually do: emit a receipt, consume one, and make a guard fail on purpose.
The third is the one that matters — the spec's instance 12 is a guard that passed cleanly on the very
case it existed to catch, because nobody had ever made it fail.

---

## 1. Emit a receipt (Python — five of six areas)

```python
from buildkit_cli.receipts import Receipt, Outcome, emit

r = Receipt(
    area="roadmap-sync",
    check_id="reconcile",
    target_requested="catalog",
    target_resolved=str(resolved_path),      # FR-003: as RESOLVED, not as asked
    target_total=len(all_features),
    examined_total=len(examined),
    skipped=[{"item": f, "reason": "epic_id null"} for f in dropped],
    outcome=Outcome.UNREAD if dropped else Outcome.PASS,
    reason="30 features dropped by the renderer" if dropped else None,
)
emit(r)      # -> .specify/receipts/roadmap-sync/<run-id>.json
```

`emit()` bounds the document before writing (FR-005): enumerations are capped, `*_total` counters are
never capped, and anything dropped is declared in `truncated`.

## 2. Emit a receipt (bash — the test harness)

```bash
. test/receipts/emit.sh

receipt_start test-harness "section-I-self-glp"      # keyed (letter, slug) — see below
receipt_examined 42
receipt_skip "cross_runtime_link" "unsupported platform"
receipt_emit EMPTY
```

🔴 **Key on `(letter, slug)`, never the letter alone.** `test/run_all_tests.sh` declares `Section I`
twice — line 1653 (`self.glp Procedure Tests`) and line 2219 (`Cross-runtime Gleam × C# link suite`).
Keying on the letter would make one receipt silently overwrite the other, manufacturing this feature's
own defect inside the feature.

## 3. Consume a verdict (the FR-008 gate)

```python
from buildkit_cli.receipts import verify, AdoptionManifest

verdict = verify(receipt_path, area="roadmap-sync", manifest=AdoptionManifest.load())
if not verdict.usable:
    raise SystemExit(verdict.message)   # names what was expected, found, and where (FR-011)
```

`verify()` refuses in four distinct ways, and the distinction is the point:

| condition | result |
|---|---|
| receipt absent or malformed | **refuse** — treated as `UNREAD`; the receipt mechanism is subject to its own invariant |
| area **not listed** in the adoption manifest | **refuse** — FR-020: absence is an error, never non-adoption |
| area listed `not-adopted` | **usable**, carrying a visible non-adoption marker (FR-008 phased) |
| outcome `UNREAD` / `UNSEARCHABLE` | **refuse** — never aggregated into success (FR-007) |

## 4. Make it fail on purpose (US3 — the part that earns the feature)

```bash
pytest tests/receipts/test_fault_injection.py -q
```

Each of the 13 witnessed instances has an injector. The suite asserts a **loud refusal**; a clean pass
under injection *fails the suite* (SC-007). Run one directly:

```bash
pytest tests/receipts/test_fault_injection.py -k retired_root -q
```

That reproduces instance 10 — a poll against a retired root reporting *0 actors, empty board, exit 0*.
The assertion is that it now reports `UNSEARCHABLE` and names the path it could not resolve.

**Prove the suite can go red** (SC-007) — weaken one guard and confirm:

```bash
BUILDKIT_RECEIPTS_WEAKEN=reconcile pytest tests/receipts/test_fault_injection.py -q   # MUST fail
```

If that command passes, the acceptance suite is not measuring anything and the feature is unearned.

## 5. Check the contract still holds

```bash
python -m buildkit_cli.receipts.conformance
```

Runs the 7 vectors in `contracts/conformance/vectors.json` — 2 that must be accepted, 5 that must be
rejected (missing reason on a non-success outcome; a crash reported as PASS; an outcome outside the
five; an override with no expiry; an undeclared area). **Verified 2026-08-18: 7/7 behave as declared.**

The conformance run's own output is a receipt (FR-024), so the contract is demonstrated under the
invariant it defines rather than asserted. Both repositories run the same vectors; that is what makes
single-authority safe without trusting a version pin — pins on this fleet have been measured to be
entirely decorative.

## 6. Read the adoption position honestly

```bash
python -m buildkit_cli.receipts.manifest report
```

Prints all six areas with state and the date it was set. An area missing from the manifest is an
**error**, printed as such — never omitted, and never rendered as "not adopted". Silence is not
coverage (FR-018).
