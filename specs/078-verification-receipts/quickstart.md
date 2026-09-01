<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 9c14e8a5-0b73-4f26-a8d1-6e2f37b94c08
-->

# Quickstart — Verification receipts

**Feature**: `078-verification-receipts` · **Rewritten and executed**: 2026-09-01 (task T061)

> 🔴 **THIS DOCUMENT WAS ITSELF AN INSTANCE OF THE DEFECT IT DESCRIBES.**
> Every command in the 2026-08-18 version was fictional: it imported
> `buildkit_cli.receipts` (the module is `codeconv.receipts`), constructed
> `Receipt(...)` from fields that do not exist (`target_requested`,
> `examined_total`, `reason`), **passed `outcome=` in by hand** — the one thing
> the design forbids, because a caller that can write `PASS` is a caller that can
> lie — and pointed at `tests/receipts/test_fault_injection.py`, a
> `vectors.json`, an env var and three CLI entry points that were never written.
> A quickstart nobody has run is a check that never ran, wearing documentation.
> **Every command below was executed on 2026-09-01 and its real output is
> recorded.** If you change the API, re-run them; do not re-imagine them.

Four things you will actually do: emit a receipt from Python, emit the identical
document from bash, read the coverage position honestly, and make a guard fail on
purpose. The last is the one that matters — instance 12 is a guard that passed
cleanly on the very case it existed to catch, because nobody had ever made it
fail.

Run everything from the repo root. `PY` below is
`codeconv/.venv/Scripts/python.exe` on Windows.

---

## 1. Emit a receipt (Python)

**The caller never states the outcome.** It states what it resolved and what it
examined; `emit` derives the classification, validates it, and only then writes.

```python
from codeconv.receipts import Target, emit

r = emit(
    check_id="reconcile",
    area="roadmap-sync",
    target=Target(kind="path", identity=str(resolved_path), resolved=True,
                  requested="catalog"),        # FR-003: as RESOLVED, not as asked
    examined_count=len(examined),
    total_count=len(all_features),             # None ⇒ "unknown" ⇒ UNREAD
    skipped=[Skip(item=f, reason="epic_id null") for f in dropped],
    run_id=run_id,
    root=".specify/receipts/runs",
)
print(r.outcome.value, r.verdict_pointer)
```

Written to `<root>/<area>/<run-id>/<check-id>.receipt.json`. `emit` bounds the
document before writing (FR-005): enumerations cap at `bind.MAX_ENUM`, the
`*_count` totals are **never** capped, and anything dropped is declared in
`truncated`. An impossible receipt — `examined_count > total_count`,
`examined + skipped > total`, a `PASS` over an unresolved target — raises
`ReceiptInvalid` and is **not written**.

Executed 2026-09-01:

```
$ PY -c "from codeconv.receipts import Target, emit; \
    r = emit(check_id='demo', area='reference', \
             target=Target('path','t',resolved=True), \
             examined_count=3, total_count=5, run_id='qs', root='/tmp/qs', write=False); \
    print(r.outcome.value, r.examined_count, r.total_count)"
UNREAD 3 5
```

3 of 5 examined is **UNREAD**, not "3 findings, clean". That is the whole
feature in one line.

## 2. Emit the identical document from bash

```bash
. test/receipts/emit.sh

receipt_start harness.section-I-self-glp test-harness path "test/run_all_tests.sh"
receipt_examined "self.glp procedure tests"
receipt_skip "cross_runtime_link" "unsupported platform: no QUIC transport on this host"
receipt_total 2
receipt_emit "$RUN_ID" ".specify/receipts/runs"
```

🔴 **Key on `(letter, slug)`, never the letter alone.** `test/run_all_tests.sh`
declares `Section I` twice — `self.glp Procedure Tests` and the
`Cross-runtime Gleam × C# link suite`. Keying on the letter alone makes one
receipt silently overwrite the other, manufacturing this feature's own defect
inside the feature (T045).

The two emitters are held to **byte-level parity** on the seven conformance
vectors, because a single implementation is its own oracle and its bugs are
invisible (FR-024):

```
$ bash test/receipts/assert.sh
  parity vectors exercised: 7/7
  assertions: 18 passed, 0 failed
  RESULT: 7/7 parity, all assertions pass
```

## 3. Read the coverage position honestly

```bash
PY -m pytest codeconv/tests/faultinj -q
```

The suite prints its own SC-001 position at session end. Executed 2026-09-01:

```
===================== SC-001 witnessed-instance coverage ======================
SC-001 instance coverage: 6 of 13 (UNREAD — partial coverage is not a pass)
  [examined] instance  2 … instance  5 … instance  6 … instance  7 …
             instance  9 … instance 12
  [UNREAD]   owner=buildkit: instances [1, 3, 4, 8, 10, 11, 13]
  receipt outcome: UNREAD (6/13) — NOT a pass (FR-016)
  a green test suite does NOT mean SC-001 is met
```

🔴 **69 tests green and SC-001 still UNREAD, and that is correct.** Seven of the
thirteen witnessed instances are defects in **buildkit** tools this repo cannot
inject. Engineer ruling `Q-GLPNETS14-01` keeps the denominator at thirteen and
makes those seven read UNREAD with a named owner rather than shrinking the bar
until glpnet can clear it. `PASS` is **unreachable from this repository alone**,
by design.

To let the bash harness's instances register, give both runs the same run:

```bash
export CODECONV_RECEIPTS_ROOT="$PWD/.specify/receipts/runs"
export CODECONV_RECEIPTS_RUN_ID="run-$(date -u +%Y%m%dT%H%M%SZ)"
bash test/receipts/assert.sh
PY -m pytest codeconv/tests/faultinj -q     # -> absorbed from receipts: [5, 7]
```

Absorption accepts a claim **only from a receipt whose own outcome is
successful**. A failing harness proves the check ran, not that the injection was
demonstrated; counting it would be instance 5 by another route.

## 4. Make it fail on purpose

Every assertion in `codeconv/tests/faultinj/test_instance_registry.py` names, in
its docstring, the mutation it kills — because the 2026-08-24 adversarial review
found the previous mutation test staying **green under a no-op validator**, the
inverse of SC-007.

```bash
PY -m pytest codeconv/tests/faultinj/test_instance_registry.py -q     # 12 passed
PY -m pytest codeconv/tests/faultinj/test_vacuous_guard.py -q         #  3 passed
```

Prove the suite can go red. Weaken the denominator by hand and re-run:

```bash
# in codeconv/tests/faultinj/instances.py, drop instance 13 from INSTANCES
PY -m pytest codeconv/tests/faultinj/test_instance_registry.py -q     # MUST fail
```

Executed 2026-09-01 — **4 failed, 8 passed**, and the four are the right four:
`test_all_thirteen_instances_are_declared`,
`test_ownership_split_is_explicit_and_totals_thirteen`,
`test_one_missing_instance_drops_the_whole_receipt_off_pass` and
`test_unread_are_attributed_to_a_named_owner`. The file was restored and re-ran
12 passed. If that mutation ever passes, the registry is measuring nothing and
the coverage claim is unearned.

## 5. Read the adoption position

```bash
PY -c "from codeconv.receipts import load_adoption; \
       [print(f'{a:14} {s}') for a, s in load_adoption().items()]"
```

Executed 2026-09-01:

```
reference      adopted
test-harness   adopted
build-gate     non-adopted
coop           non-adopted
roadmap-sync   non-adopted
```

An area **missing** from `.specify/receipts/adoption.json` raises
`MissingDeclaration` — absence is an error (FR-020), never rendered as
"not adopted". A repeated area raises too: two declarations mean one state is
read and one is invisible. A state outside `adopted|non-adopted` raises rather
than being interpreted, because the consumer gates on equality with
`non-adopted` and any other value would silently take **adopted** semantics.

🔴 **Five areas, not six, and that is ruled correct.** `Q-GLPNETS12-02` scoped
078 in glpnet to its eleven glpnet-side tasks and five areas; the buildkit-side
`3rtask` and `codexreview` areas are a buildkit-owned successor with 078 as its
spec of record. **Declared cost, carried knowingly: FR-017's six-area guarantee
is satisfied by no single repo**, so a fleet adoption claim must read both
manifests together.

## 6. Declare what a run expects, before it runs

```bash
PY -c "from codeconv.receipts import declare_expected, missing_checks; \
       declare_expected('/tmp/qs','r1',['a','b']); print(missing_checks('/tmp/qs','r1'))"
```

Executed 2026-09-01 → `['a', 'b']`: both expected checks are missing because
neither ran. A run that declares **no** expected set raises `UndeclaredRun` — it
is not a run in which nothing was expected, it is an unverifiable run, and it
refuses rather than reports (FR-023). An **empty** declared list refuses for the
same reason; so does a declaration carrying a different `run_id`, because another
run's declaration is not this run's.

The checked-in baseline set is `.specify/receipts/expected-checks.json` (T049).
It is a template: `load_expected` requires the live file's `run_id` to equal the
run being read, so the live file is necessarily per-run and cannot be checked in.
What is checked in is the expected **set**, so a check that silently stops
existing is detectable against version control rather than against whatever the
last run happened to produce — the rejected alternative was deriving the expected
set from the last successful run, a ratchet that only ever loosens.
