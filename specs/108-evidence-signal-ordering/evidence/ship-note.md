<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Ship note — feature 108, evidence-signal ordering

**Host** SHIRAS · **date** 2026-09-09 · **branch** `develop` · **lane** shiras-glpnet
**Closes** T036 (baseline suites), T039 (every SC measured), T040 (both budgets measured).

---

## 0 · The one sentence that matters

Every success criterion was **measured by a command in this note**, and the verdict vocabulary
is three-valued — `met` / `not-met` / `unmeasured` — because the defect this feature exists to
remove is exactly the fold of *did-not-run* into *passed*. The era's own previous restart
pointer (S8 §1b) records this lane committing that fold against its own test suite two days
ago. So the measurement is scripted, not asserted: `scripts/evidence_signal_sc.py`.

## 1 · SC-001..SC-007 — measured values

Produced by `scripts/evidence_signal_sc.py` and written into
`.specify/evidence-signals/report.json` under `success_criteria`. Reproduce with:

    /home/shira/.local/share/bkvenv/bin/python scripts/evidence_signal_audit.py   # writes report.json
    /home/shira/.local/share/bkvenv/bin/python scripts/evidence_signal_sc.py      # scores it

| criterion | verdict | measured value |
|---|---|---|
| **SC-001** | `met` | 8 of 8 table instances carry both a disposition and a named owner; instance 9 section present; 9/9 accounted |
| **SC-002** | `met` | 36/36 manifest surfaces classified (denominator is the manifest, FR-014a); scan_only=0, manifest_only=0 |
| **SC-003** | `met` | python conformance suite: 1 check(s) and 1 negative control(s) executed, 1/1 checks pass, 1/1 controls fire |
| SC-003 (C# product surface) | `met` | C# product surface (40 iterations): 1 check(s) and 1 negative control(s) executed, 1/1 checks pass, 1/1 controls fire |
| **SC-004** | `met` | python conformance suite: 3 check(s) and 1 negative control(s) executed, 3/3 checks pass, 1/1 controls fire |
| **SC-005** | `met` | python conformance suite: 4 check(s) and 3 negative control(s) executed, 4/4 checks pass, 3/3 controls fire |
| **SC-006** | `met` | python conformance suite: 2 check(s) and 2 negative control(s) executed, 2/2 checks pass, 2/2 controls fire |
| **SC-007** | `met` | 125 regions examined-and-clean, 1342 not-examined each carrying a stated reason (1342/1342); 0 regions omitted |

**How each verdict is reached, and why it cannot be talked up:**

- **SC-001** is parsed out of the instance table in `docs/known-issues.md` — a row with a blank
  disposition or a blank owner scores `not-met`. It is not read off a claim that the table is
  complete.
- **SC-002** takes its denominator from the **manifest** (FR-014a), never from the subset that
  happened to be examined, and additionally requires `scan_only == 0` and `manifest_only == 0`
  — the two-way cross-check, so under-declaration and scan blind spots each fail loudly.
- **SC-003** is scored on **two independent surfaces**: the Python model
  (`test_wait_reports_idle_only_after_the_work_completed`) and the **C# product** surface
  (`T012_WaitForIdle_is_correct_on_all_40_contended_iterations`, `DeclaredIterations = 40`).
  Neither substitutes for the other, and each is scored **only once its negative control has
  been shown to fire** (FR-018a) — `test_early_wait_negative_control_fails` and
  `T013_the_same_harness_FAILS_the_pre_fix_ordering`. An unfalsifiable 100% scores zero.
- **SC-004/005/006** are scored the same way, each with its controls named in the script's
  `PYTEST_CRITERIA` table. A criterion whose control is missing from that table **cannot score
  `met` at all**.
- **SC-007** requires every not-examined region to carry a stated reason; a region with no
  reason is an omission, not a boundary.

**The scorer's own negative control**, executed while writing this note — it must not be
possible to reach `met` by accident:

    _verdict_from({"a":"pass"}, ["f::a"], ["f::missing_control"], …) -> unmeasured
    _verdict_from({"a":"pass","b":"fail"}, ["f::a"], ["f::b"], …)   -> not-met
    _verdict_from(None, …)                                          -> unmeasured

A control that did not run leaves the criterion `unmeasured`. It is never folded into `met`.

## 2 · Exit codes — silence is not success

`scripts/evidence_signal_sc.py` separates the two failure modes rather than returning a single
non-zero:

| exit | meaning |
|---|---|
| 0 | every criterion measured, every criterion met |
| 1 | a criterion was **measured and failed** |
| 2 | every measured criterion passed, but **at least one was never measured** |

A caller that only tests `!= 0` still cannot read silence as a pass.

## 3 · Budgets — T040

The plan states 60 s for the audit and 120 s for the conformance harness. Both measured on this
host with `/usr/bin/time`:

| what | budget | measured | verdict |
|---|---|---|---|
| `scripts/evidence_signal_audit.py` (checks executed) | 60 s | **17.88 s** | within |
| `scripts/evidence_signal_audit.py` (checks not executable) | 60 s | **1.39 s** | within |
| `pytest scripts/tests/test_evidence_signal_conformance.py` (17 tests) | 120 s | **3.23 s** | within |
| `scripts/evidence_signal_sc.py` end-to-end (incl. the filtered `dotnet test`) | — | 65.62 s | no stated budget |

🔴 **The two audit rows are the same command and differ by a factor of 13 — because the
interpreter changes the ANSWER, not just the speed.** Run under a Python without `pytest`, the
audit reports `8 not-executable`, `0 conforming`; run under
`/home/shira/.local/share/bkvenv/bin/python` it reports `8 pass`, `3 conforming`. The tool is
honest in both cases — a check it cannot execute classifies the surface `unproven`, never
conforming — but **the host's toolchain decides the verdict**, and nothing in the invocation
warns you which one you got. Use the `bkvenv` interpreter, or read `checks executed` before you
read anything else. This is the same argument S8 §1b made from the six silently-skipped suite
sections, and it is a live case for the board's
`per-host-toolchain-and-environment-contract-declared-machine-checked-loudly-refused`.

## 4 · Baseline suites — T036

### The REPL suite — `bash test/run_all_tests.sh`

Run **four times**, because each run exposed a further defect in the harness itself — all of
them the same class the feature governs, all of them found while closing it.

| run | Total | Passed | Failed | Skipped | Unsearch. | groups not run | harness verdict |
|---|---|---|---|---|---|---|---|
| 1 baseline | 586 | 585 | **1** | 4 | 2 | 6 | `SOME TESTS FAILED` |
| 2 after fix (a) | 586 | 586 | 0 | 4 | 2 | 6 | `INCOMPLETE … 6 group(s) did not run (exit 2)` |
| 3 after fix (c) | 604 | 603 | **1** | 1 | 1 | 2 | `SOME TESTS FAILED` |
| **4 final** | **604** | **604** | **0** | 1 | 1 | **2** | `INCOMPLETE … 2 group(s) did not run (exit 2)` |

**18 more checks now actually execute, and four groups moved from never-run to measured.**

#### (a) Section X reported a FAIL for a missing prerequisite — and a PASS because nothing ran

Section X resolves an interpreter with `resolve_python`, which asks only *can this run
`print()`*. On SHIRAS that answers `/usr/bin/python3`, which has no `pytest`. The section then
printed, in adjacent lines:

    FAIL: X-1: conformance harness + audit tests all pass (expected: passed)
    PASS: X-2: no test in the harness failed

X-1 misreported a **missing prerequisite as a defect**. X-2 went **green because nothing ran** —
the token `failed` does not occur in `No module named pytest`. That is measured instance 4
(FR-007, an emptiness read as evidence) and S8 §1b's error, one in each direction, inside the
section built to catch them. Under `bkvenv` the harness is 127/127; no test was ever failing.

**Fixed:** Section X resolves an interpreter that can `import pytest` (`$PY_BIN`, then
`$HOME/.local/share/bkvenv/bin/python`, then `codeconv/.venv`), and when none can it reports
**UNSEARCHABLE by name** — not `skip`, because it did not decline to look, it could not. X-3/X-4
now run the audit under that same interpreter, so it executes its eight checks. Both branches of
the resolver were executed as a pair before shipping: the real host resolves `bkvenv`; a probe
with no pytest-bearing candidate returns empty and takes the UNSEARCHABLE branch.

#### (c) Five groups said "C# REPL not built" for a program sitting in that directory

The harness looked for `glp_repl.exe`. `dotnet build` on Linux emits the same apphost as
`glp_repl`, no extension. **It was looking for a filename, not for a program.** Same defect at
`term_traversal_probe.exe`, which failed U-4 loud — correctly by its own design, for a probe
that was present. Fixed in `test/run_all_tests.sh`, the shared helper
`test/lib/tfm.sh::glp_repl_exe`, and `scripts/differential_gate.py`.

🔴 **And the trap behind it: PRESENT IS NOT RUNNABLE.** Once resolved, the apphost still refused
to start — `You must install .NET to run this application`, because `DOTNET_ROOT` was unset and
`dotnet` reached PATH only through a symlink into `~/.dotnet`. Resolving the file and running it
blindly would have converted five honest SKIPs into a stream of FAILURES blamed on the code. The
harness now derives `DOTNET_ROOT` from the `dotnet` on PATH, **probes the binary once** (stdin
closed, `timeout 60` — a first cut of that probe hung the suite for two minutes and had to be
killed), and routes a start failure to UNSEARCHABLE naming the runtime, never "not built".
Both probe branches were executed before shipping: with a stripped environment the binary does
not start and the UNSEARCHABLE branch fires; with the derived `DOTNET_ROOT` it starts.

#### What that recovered

These groups had been silently unrun on this host and are now **measured, and green**:

| group | result now |
|---|---|
| Section I (cross-runtime Gleam × C# link suite) | runs; `link_both_ways` 4/4, `round_trip` passing |
| Section U (077 cyclic diagnostics) | 7 checks, including U-4's real-walker probe |
| V-18..V-23 (101 Dart vs C# parity) | **V-20: the two transcripts are BYTE-IDENTICAL**, with the non-empty guards V-18/V-19 firing first |
| Y-7 (109 declared criteria) | `every declared criterion was actually measured` |

CLAUDE.md records that "SC-003's three-runtime agreement was carried by a claim, not a
measurement". On this host it had quietly gone back to being carried by a claim, because the
measurement could not find a file. It is a measurement again.

#### Exit 2 is the correct final state, and it is not a green

All 604 executed checks pass; two groups never ran, each for a **named missing prerequisite**:

| group | missing |
|---|---|
| Section S (ms_message durable mesh) | `ms_message` venv absent |
| Section T (064 service-box drills) | QUIC trust material `glpquick.pfx` absent |

### The C# suites

| suite | result |
|---|---|
| `csharp/ynet_transport.tests` | **Passed! 217 / 217**, 0 failed, 0 skipped |
| `csharp/ynet_client.tests` | **190 passed, 2 failed**, 0 skipped |


**The two red C# tests are the disclosure, not a regression.** `AckDurabilityDisclosureTests`
asserts that the disclosed FR-012 defect is **still present**; both were red when the ruling of
2026-09-08 (`1847ba3b`, "real canonical binary RED, in-repo model RED, merge-by-message-id
control GREEN") was recorded, and they are red here for the same reason and with the same
messages. A green result from either would mean the disclosure had gone stale and needs
re-measuring — which is the point of writing a disclosure as an executing test.

## 5 · Audit state at ship

    regions examined  125        regions UNREAD  0        errors 0        REFUSALS 0
    checks executed   8 pass / 0 fail / 0 not-executable
    conforming 3 · non-conforming 1 · unproven 32
    dispositions      owned=5  declared-unproven=27  not-a-signal=3  disclosed=1

**32 unproven and 1 non-conforming is the honest ship state, and is not a defect of the ship.**
FR-015 is explicit that a surface with no conformance check is `unproven`, never `conforming` —
good shape is not evidence. What this feature delivered is that those 33 surfaces are now
**named, counted, owned and re-derivable on every run**, where before they were invisible. The
single non-conforming surface is `ynet-client-alert-acknowledged` (FR-012), which is the
disclosed ack-durability defect owned by @ariellas-qhstate; see §6.

## 6 · A third measurement of instance 8, taken while closing this era

The lane's own alert spool made the case for FR-012 again, unprompted:

    .specify/ynet/shiras-glpnet/alerts   159 records · 0 acknowledged
    distinct arrived_utc across all 159:  ONE — every record reads 2026-09-09T10:15:05
    every one of the 159 files rewritten today (mtime)

Era S8 closed its restart gate at **66 alerts, 66 acked, 0 unacked**, censused from two working
directories. A single startup replay re-raised the whole spool and reset every flag. Recorded in
`docs/known-issues.md` under instance 8. Consequence for anyone reading a restart gate: **the
`unacked` count measures the time since the last replay, not the work the reader did.** Verify
from the acks you emitted; never by re-counting the spool.

**Not re-acked in bulk here, deliberately.** These are fleet messages that were already answered
once; re-answering 159 resurrected records would publish duplicate ACKs to lanes that already
have this lane's answer, which is a failure this lane has committed before. The finding is
published instead, and the queue is left intact for triage rather than drained — a drain deletes
the record.
