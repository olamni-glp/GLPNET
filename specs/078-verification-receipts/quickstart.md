<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Quickstart: emit, consume, and fault-inject a receipt

Illustrative usage of the glpnet reference implementation (`codeconv.receipts`). The authoritative
schema is resolved from the pinned buildkit version (FR-024); these snippets show intent, not final
signatures — those are fixed in `/bk-tasks` / implementation.

## 1. Emit a receipt beside a verdict (US1)

```python
from codeconv.receipts import receipt, Target

r = receipt.emit(
    check_id="codeconv.build-gate.dart_csharp",
    area="build-gate",
    target=Target(kind="path", identity="/abs/out/csharp", requested="out/csharp", resolved=True),
    examined_count=42, total_count=42,          # examined in full …
    problems=[],                                 # … and nothing wrong  ⇒ PASS
    run_id="run-abc",
)
# → writes receipts/build-gate/run-abc/codeconv.build-gate.dart_csharp.receipt.json
# → returns the verdict pointer to attach beside the human/machine verdict
```

A target that would not resolve is **never** clean:

```python
Target(kind="root", identity="I:/coop/glpnet/sched", requested=..., resolved=False,
       unresolved_reason="retired root")           # ⇒ outcome UNSEARCHABLE (instance 10)
```

## 2. The three "nothing found" cases never collapse (US2)

```python
receipt.emit(..., examined_count=0, total_count=0)      # EMPTY   — legitimate pass
receipt.emit(..., examined_count=3, total_count=9)      # UNREAD  — states 6 unexamined; NOT a pass
Target(..., resolved=False, unresolved_reason=...)      # UNSEARCHABLE — names the reason; NOT a pass
```

## 3. Consume a verdict — refuse an unearned green (US1/FR-008)

```python
from codeconv.receipts import consumer

result = consumer.read(verdict)          # verdict carries a receipt pointer
# no receipt / malformed  → UNREAD, refused (never a silent pass)
# outcome UNREAD/UNSEARCHABLE → surfaced as-is, refused as success
# area unlisted in .specify/receipts/adoption.json → refused, names missing declaration (FR-020)
```

## 4. Declare a run's expected checks (FR-023) and detect a vanished check (FR-013)

```python
from codeconv.receipts import manifest
manifest.declare_expected(run_id="run-abc",
                          expected_checks=["codeconv.build-gate.dart_csharp",
                                           "receipts.conformance-fixture"])
# after the run:
manifest.missing_checks(run_id="run-abc")   # any expected check_id with no receipt → reported loud
# a run with no expected.json at all → refuses (unverifiable run)
```

## 5. Fault-inject and assert a loud refusal (US3)

Each fault is one test file under `codeconv/tests/faultinj/`; the suite fails if any injected fault
produces a clean pass (FR-015), and its own non-execution is loud (FR-016 — it is in the ExpectedSet).

```python
def test_removed_target_refuses():          # US3.1 — deleted target
    r = run_reference_check(target_removed=True)
    assert r.outcome == "UNSEARCHABLE"      # a clean pass here FAILS the suite

def test_suppressed_output_block_is_unread():   # US3.2 — instance 2 (findings block omitted)
    r = run_reference_check(suppress_output=True)
    assert r.outcome == "UNREAD"            # never read as "0 findings"

def test_falsified_count_detected():        # US3.5 — examined > total
    with raises(ReceiptInvalid):
        receipt.emit(..., examined_count=99, total_count=10)
```

## 6. Override a refusal — recorded, scoped, expiring (FR-012)

```python
from codeconv.receipts import override
override.record(area="test-harness", check="section.T", reason="glpquick.pfx absent on this host",
                rationale="tracked in #NNN; unblock suite locally", acknowledged=True,
                expiry="2026-08-25T00:00:00Z")   # mandatory expiry; no indefinite override
# the override stays visible in the receipt; outside its scope/expiry the refusal stands
```

## Gate to green (definition of done for the MVP increment)

- `pytest codeconv/tests/` green including `faultinj/` — every injected fault refuses loudly (SC-004/SC-007).
- The conformance fixture validates the Python + bash emitters; its output is itself a valid receipt (FR-024).
- The reference check is `adopted` in `.specify/receipts/adoption.json`; the four glpnet areas listed as `non-adopted` (honest partial coverage — FR-017/018).
- No regression in the existing REPL suite (`bash test/run_all_tests.sh`) — receipts are additive.
