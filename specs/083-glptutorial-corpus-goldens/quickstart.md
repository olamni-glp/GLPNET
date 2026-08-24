<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Quickstart — 083 glptutorial corpus-golden reconciliation

**Branch**: `083-glptutorial-corpus-goldens` · **Date**: 2026-08-24

Every command below was **run on 2026-08-24** and its output is what is quoted.

## Prerequisites

```
cd D:\BSTDEV\research\GLP\GLPNET
set PYTHONUTF8=1
```

Use the repo venv: `codeconv/.venv/Scripts/python.exe -m codeconv.cli …`

🔴 **The Dart and C# runtimes must be runnable on this host** to re-capture goldens. If a toolchain
is absent the run MUST fail loudly — an absent toolchain silently substituted is the F1/F3 exemplar
#5 defect and would produce a golden captured from nothing. Verify first:

```
dart --version && dotnet --version
```

## 1 · See the divergence set (read-only, safe)

```
codeconv tutorials propose
```

Expected today — **4 proposals**:

| kind | exercise | id |
|---|---|---|
| `layout_normalise` | ch04/07 | `spec-violation-ch04-ex07` |
| `stale_artefact` | ch04/08 | `stale-golden-ch04-ex08` |
| `drift_gap` | ch07 | `drift-gap-cssg` |
| `run_manifest` | ch07 | `run-manifest-ch07` |

This is SC-001's baseline. **Success is this reaching zero for ch04 and ch07.**

## 2 · See the drift guard's current state

```
codeconv tutorials sync --check
```

🔴 Expected today — **exit 1, 67 drift lines across all 13 chapters** (ch04: 5, ch07: 5).
**This is the saturation problem**: the guard is red before you touch anything, so its failure
proves nothing. Read `research.md` R-2 before assuming a red guard means you broke something.

## 3 · Verify the substrate identity before vendoring

```
ls programs/cssg_modules      # 5 files  <- THIS is ch07's substrate
ls programs/cssg_modules_v2   # 6 files  <- NOT ch07's
```

Vendoring `_v2` would produce "a guard that passes while guarding nothing" (spec Edge Case).

## 4 · Inspect one exercise before changing its golden

```
codeconv tutorials preview --chapter ch04 --exercise 07   # no execution
codeconv tutorials run     --chapter ch04 --exercise 07   # live outcome
codeconv tutorials explain --chapter ch04 --exercise 07   # live vs golden
```

For **ch04/07** the live runtime **correctly rejects** the program: `natural_number/1` is a
two-clause procedure and manual §8 requires a defined guard to be a single-unit-clause procedure.
**The rejection is right; the golden's `✓Loaded` is the falsehood.**

🔴 **Do not edit `ch04/07`'s source.** FR-002 was ruled **(b) record the rejection** — the exercise
stays byte-exact from book §4.3.1 p 37.

## 5 · Apply a repair (approval-gated)

```
codeconv tutorials propose --apply \
    --approve \
    --rationale "ch04/08 recapture; cited cause: C# is_list guard fix"
```

**`--apply` refuses without both `--approve` and `--rationale`** (FR-006).

🔴 **The rationale is load-bearing, not a comment.** Per C4/FR-008 a change may be recorded as a
**`recapture`** only if the rationale **cites the specific runtime change** that altered behaviour.
Without a citation it is a **`repair`**. This is what stops a re-capture silently blessing a
regression.

| exercise | class | cited cause |
|---|---|---|
| ch04/08 | `recapture` | the C# `is_list` guard fix |
| ch04/07 | `repair` | none — no runtime changed; the golden was simply false |

## 6 · Verify the remainder shrinks correctly

```
codeconv tutorials propose
```

FR-007: the report must show **exactly the unrepaired remainder** — never a clean report while any
divergence stands. A clean report with work outstanding is a false green and is itself a defect.

## 7 · Run the regression gate

```
bash test/run_all_tests.sh
```

🔴 **The baseline is 561 total / 559 pass / 2 fail / 0 skip**, measured 2026-08-24 — **not** the
`546 / 0 / 1` in SC-007, which is stale (see `research.md` R-5). The two failures are the known
pre-existing `Section T` 064 drills (`T-1`, `T-2`); they survive a rebuild and are real.

**The bar for this feature: introduce no new failure against 561/559/2/0.**

🔴 If a count looks better than expected, check the binary is not stale before believing it:

```
ls -l glp_runtime/glp_repl.exe   # must be NEWER than its sources
```

A stale `glp_repl.exe` has already produced seven false "passes" in this repo once.

## Done when

| criterion | check |
|---|---|
| SC-001 | `tutorials propose` → **0** proposals for ch04 and ch07 |
| SC-003 | ch07 substrate modified → guard fails **naming the path**; unmodified in-scope tree → OK |
| SC-004 | every ch07 exercise → exactly one `(program, play, limit)` |
| SC-005 | every applied repair carries a recoverable approval + rationale |
| SC-006 | each changed golden classifiable stale-vs-regression **without reading the implementation** |
| SC-007 | no new failure vs **561/559/2/0** (re-based; see R-5) |

## Reporting obligation that is not a code change

**B10**: report to Udi that a byte-exact transcription of book §4.3.1 `lesseq` is **rejected** by
the typed-GLP guard rules. This is a genuine book-versus-manual finding surfaced by the corpus, and
per the Bug Protocol it is **reported, not silently fixed**.
