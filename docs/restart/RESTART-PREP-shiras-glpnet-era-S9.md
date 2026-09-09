<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras-glpnet — era S9 — 2026-09-09

**Resume with exactly: `resume marathon`.**

    run       mrun-f77f62158255 [open] · feature glpnet-shiras-tidyup-and-scheduler-rootcause
              steps 9/9 · 62 outstanding backlog items (long-lived tidy-up run, NOT the era)
    era       feature 108 evidence-signal-ordering — CLOSED this session (40/40 tasks)
    next era  per-host-toolchain-and-environment-contract-declared-machine-checked-loudly-refused
              (buildkit-roadmap next, rank 24) — see §4, this era generated three of its cases
    suites    REPL 604/604 executed-and-passing, 2 groups unrun (named prerequisites, exit 2)
              C# transport 217/217 · C# client 190/192 (the 2 red ARE the FR-012 disclosure)
    branch    develop
    M6        code-based client active; spool at 159 records, 0 acknowledged — see §3

---

## 1 · What closed feature 108

T036, T039 and T040 were the last three open tasks. All three are now measured, not asserted.

- **T039** — every success criterion SC-001..SC-007 is scored by
  `scripts/evidence_signal_sc.py`, which runs the named checks and their **negative controls**
  and writes the verdicts into `.specify/evidence-signals/report.json` under
  `success_criteria`. All seven `met`, plus SC-003 `met` a second time on the **C# product**
  surface (`T012` 40/40, with `T013` shown to fire). The scorer's vocabulary is three-valued —
  a control that did not run leaves the criterion `unmeasured`, never `met` — and exit **2**
  means *nothing failed but something was never measured*, distinct from exit 1.
- **T040** — audit **17.88 s** against a 60 s budget; conformance harness **3.23 s** against
  120 s. Both within.
- **T036** — see §2; the full ship record is
  `specs/108-evidence-signal-ordering/evidence/ship-note.md`.

## 2 · 🔴 Three defects of the SAME class, found in this lane's own harness while closing an era about that class

Every one of these made a missing prerequisite look like something else. Read them before
trusting any suite number on a new host.

**(a) Section X reported a FAIL for a missing prerequisite, and a PASS because nothing ran.**
`resolve_python` asks only *can this run `print()`*; on SHIRAS that is `/usr/bin/python3`, which
has no `pytest`. Section X then printed, adjacently:

    FAIL: X-1: conformance harness + audit tests all pass (expected: passed)
    PASS: X-2: no test in the harness failed

X-2 was green because the token `failed` does not occur in `No module named pytest`. Under
`bkvenv` the harness is 127/127 — no test was ever failing. **Fixed:** Section X resolves an
interpreter that can `import pytest`, and says UNSEARCHABLE **by name** when none can.

**(b) The same audit gives a different VERDICT under a different interpreter.**
`scripts/evidence_signal_audit.py` reports `8 not-executable · 0 conforming` under a
pytest-less Python and `8 pass · 3 conforming` under `bkvenv`. Both are honest — an
unexecutable check leaves a surface `unproven`, never `conforming` — but **the host decides the
answer and nothing in the invocation says which answer you got.** Read `checks executed` before
anything else, and prefer `/home/shira/.local/share/bkvenv/bin/python`.

**(c) Five check groups said "C# REPL not built" while the program sat in that directory.**
The harness looked for `glp_repl.exe`. `dotnet build` on Linux emits the same apphost as
`glp_repl`, no extension. **It was looking for a filename, not for a program.** Fixed in
`test/run_all_tests.sh`, the shared helper `test/lib/tfm.sh::glp_repl_exe`, and
`scripts/differential_gate.py`.

🔴 **And the trap behind (c): PRESENT IS NOT RUNNABLE.** Once resolved, the apphost still
refused to start — `You must install .NET to run this application`, because `DOTNET_ROOT` was
unset and `dotnet` reached PATH only through a symlink into `~/.dotnet`. Resolving the file and
running it blindly would have converted five honest SKIPs into a stream of FAILURES blamed on
the code. The harness now derives `DOTNET_ROOT` from the `dotnet` on PATH, **probes the binary
once** (stdin closed, `timeout 60` — a first cut of that probe hung the suite for two minutes),
and routes a start failure to UNSEARCHABLE naming the runtime.

## 3 · ⚠ THE ALERT SPOOL RESURRECTED. DO NOT READ `unacked` AS WORK OUTSTANDING.

    .specify/ynet/shiras-glpnet/alerts   159 records · 0 acknowledged
    distinct arrived_utc across all 159:  ONE — every record reads 2026-09-09T10:15:05
    every one of the 159 files rewritten today (mtime)

Era S8 closed its gate at **66 alerts, 66 acked, 0 unacked**, censused from two directories.
One startup replay re-raised the whole spool and reset every flag. This is **measured instance
8** (FR-012, owner @ariellas-qhstate) reproducing on a **third host, at fleet scale, on a later
build, after the disclosure**. Recorded in `docs/known-issues.md`.

**Consequence for your restart gate: the `unacked` count measures the time since the last
replay, not the work you did.** Verify from the acks you emitted. `scripts/ynet_alert_census.py`
is still the right tool for *counting* (it refuses rather than reporting a false clean), but its
number is not a measure of a reader's diligence.

**Not re-acked in bulk, deliberately.** These were answered once already; re-answering 159
resurrected records publishes duplicate ACKs to lanes that already hold this lane's answer —
a failure this lane has committed before. The queue is left intact for triage rather than
drained, because a drain deletes the record.

## 4 · The next era, and why this one argues for it

`buildkit-roadmap next` returns
**per-host-toolchain-and-environment-contract-declared-machine-checked-loudly-refused**
(rank 24, RCA cluster F3). Hand it to the pipeline yourself — the roadmap never does:

    /bk-specify "Per-host toolchain and environment contract (declared, machine-checked, loudly refused)"

**All three findings in §2 are new instances of exactly that brief**, and they are worth
carrying into the spec because each fails in a *different* direction, which is the argument for
a declared contract rather than more per-section guards:

| finding | how the missing prerequisite presented |
|---|---|
| §2(a) | as a **code defect** (FAIL) — and simultaneously as a **pass** (nothing ran) |
| §2(b) | as a **different verdict**, silently, from the same command |
| §2(c) | as **"not built"** for a program that was built — then nearly as a **run of failures** |
| S8 §1b | as an **invisible not-run** that a careless reader called a pass |

The invariant the brief already states — *a machine-checkable contract declares every required
tool/version/path; a host failing it is named loudly BEFORE any suite runs* — would have caught
all four. Concretely, this host's contract needs at least: a Python that can `import pytest`,
`DOTNET_ROOT` (or a discoverable .NET), `gleam` on PATH, the `ms_message` venv, and the QUIC
trust material `glpquick.pfx`.

## 5 · What still does not run on SHIRAS, and why

Every one is a **named missing prerequisite**, not a failure:

| group | missing |
|---|---|
| Section S (ms_message mesh) | `ms_message` venv absent |
| Section T (064 service-box drills) | QUIC trust material `glpquick.pfx` absent |

**The list was SIX groups at the start of this session.** Sections I, U, V-18..V-23 and Y-7 were
never missing anything — they were blocked by §2(c) alone, and they now run and pass:

| group | result now |
|---|---|
| Section I (cross-runtime Gleam × C#) | `link_both_ways` 4/4, `round_trip` passing |
| Section U (077 cyclic diagnostics) | 7 checks incl. U-4's real-walker probe |
| V-18..V-23 (101 Dart vs C# parity) | **V-20: the transcripts are BYTE-IDENTICAL** |
| Y-7 (109 declared criteria) | `every declared criterion was actually measured` |

🔴 CLAUDE.md warns that *"SC-003's three-runtime agreement was carried by a claim, not a
measurement."* On this host it had quietly gone back to being carried by a claim — because the
measurement could not find a file. It is a measurement again. **Suite trajectory this session:
586 checks / 585 pass / 6 groups unrun → 604 / 604 / 2 groups unrun.**
