<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan: Front-end goal-term acceptance completeness

**Feature**: `101-goal-term-acceptance` · **Branch**: `101-goal-term-acceptance` · **Stage**: plan
**Spec**: `specs/101-goal-term-acceptance/spec.md` (clarified 2026-09-04, four engineer rulings)

---

## 1 · Measured defect surface — re-measured at plan time, and the count changed

The spec's Measured Baseline recorded **4 failing goal shapes** in Dart. Locating the code shows
**6 throw sites in 2 parallel families**, plus **2 silent-coercion sites**. The 4 shapes were the
*observable* symptoms; 6+2 is the *code* surface, and fixing only the 4 observed ones would leave
symmetric holes in the conjunction family.

**All eight sites are in ONE file: `glp_runtime/lib/engine/glp_engine.dart` (1270 lines).**

| # | line | function | defect | spec |
|---|---:|---|---|---|
| 1 | 973 | `_setupArgument` | `else → throw 'Unsupported argument type'` | FR-001 |
| 2 | 1024 | `_setupConjunctionArg` | same | FR-001, FR-002 |
| 3 | 1078 | `_buildStructTerm` | `else → throw 'Unsupported struct argument type'` | FR-001 |
| 4 | 1135 | `_buildStructTermForConj` | same | FR-001, FR-002 |
| 5 | 1178 | `_buildListTerm` (head) | `else → throw 'Unsupported list head type'` | FR-001 |
| 6 | 1243 | `_buildListTermForConj` (head) | same | FR-001, FR-002 |
| 7 | ~1199 | `_buildListTerm` (tail) | **`else → tailTerm = rt.ConstTerm(null)`** — silent coercion | **FR-005** |
| 8 | ~1265 | `_buildListTermForConj` (tail) | **same silent coercion** | **FR-005** |

**Sites 7 and 8 are the wrong-answer defect (L4)** and they are not exceptions — they are a silent
`else` that swallows *any* unrecognised tail. `[send(1,a)|foo]` returns exactly what `[send(1,a)|[]]`
returns, with nothing on screen.

### 1.1 · The asymmetry that justified ruling R-3, verified in code

`UnderscoreTerm` is declared at `glp_runtime/lib/compiler/ast.dart:166` and is handled by:

```
lib/compiler/analyzer.dart      lib/compiler/codegen.dart (6 sites)   lib/compiler/compiler.dart
lib/compiler/glp_printer.dart   lib/analysis/type_checker/{type_checker,well_typed_clause,
                                clause_validation,moded_head,type_conversion}.dart
```

`codegen.dart:171` — `if (term is UnderscoreTerm) return const GVar('_', false);`

**`glp_engine.dart` is the only stage that does not handle it.** Nine files handle `_`; one does not.
That is the incompleteness R-3 rests on, and it is now confirmed by file rather than by argument.

---

## 2 · Design

### 2.1 · One helper, eight call sites — fix the infrastructure, not the symptoms

Per `DISCIPLINE.md §1.3` (*"if a fix must be repeated in every file that uses a feature, the
infrastructure is broken"*), the eight sites get **two shared helpers**, not eight local patches.

```dart
/// A goal-term the front end cannot faithfully represent. Carries text naming
/// what the PROGRAMMER typed, never an internal class name (FR-006).
class GoalTermError implements Exception { final String message; ... }

/// `_` in a goal argument: a fresh writer that nobody reads (manual §9.1).
/// Deliberately NOT registered in queryVarWriters, so no binding is reported (FR-004),
/// and each call allocates independently, so occurrences never alias (FR-003).
rt.Term _anonymousGoalWriter(GlpRuntime runtime) {
  final (writerId, _) = runtime.heap.allocateVariable();
  return rt.VarRef(writerId);
}
```

**Why a writer and not a reader:** the manual (§9.1) defines the anonymous variable as *"a fresh
writer with no paired reader, so that a value assigned to it is discarded"*. A reader would suspend
forever waiting for a writer that does not exist. `_?` is *"not allowed"* by the language and stays
refused — only its message becomes legible (FR-006, ruling 4).

**Why not registered in `queryVarWriters`:** that map drives result reporting. `_` has no name to
report against, so omitting it satisfies FR-004 by construction rather than by filtering later.

**Why FR-003 (no aliasing) is free:** every occurrence calls `_anonymousGoalWriter` separately and
allocates its own heap variable. There is no name, so nothing can key a shared entry in
`varNameToId`. Aliasing is impossible by construction — this is worth an explicit test anyway.

### 2.2 · Per-site change

- **Sites 1–6** — insert `else if (x is UnderscoreTerm)` before the final `else`:
  - `isReader == false` (`_`) → `_anonymousGoalWriter(runtime)`
  - `isReader == true` (`_?`) → `throw GoalTermError("anonymous reader `_?` is not a valid term in a goal argument")`
  - the surviving final `else` throws `GoalTermError` naming the term, not `runtimeType`.
- **Sites 7–8** — replace `tailTerm = rt.ConstTerm(null)` with:
  - `tail is UnderscoreTerm && !isReader` → `_anonymousGoalWriter(runtime)` (a legal tail: an unread writer)
  - otherwise → `throw GoalTermError("list tail is neither a list nor a variable: <term>")` (FR-005)

**⚠ Site 7/8 is a behaviour change beyond `_`.** Today *any* unrecognised tail silently becomes nil.
After this change it is refused. That is the intent of FR-005, but it means a goal that "worked"
today may now be refused — correctly. Section 4 pins the shapes that must keep working.

### 2.3 · Session survivability (FR-007)

`GoalTermError` is raised during argument *construction*, before execution begins, so no goal is
scheduled and no partial heap state is reachable from the runtime. The REPL's existing per-goal
catch reports and continues. **To verify rather than assume, a test runs a refused goal followed by
a good one in the same session and asserts the second succeeds.**

---

## 3 · Cross-runtime work (FR-008 / FR-008a)

| runtime | `_` in goals | improper tail | conjunction |
|---|---|---|---|
| **Dart** | 6 sites — FIX | 2 sites — FIX (refuse) | in scope |
| **C#** | mirror sites — FIX | `glp_engine.cs:1347,1430` — FIX (refuse) | in scope |
| **Gleam** | `goal_boot.gleam` already refuses **loudly** and flags §1.14 → **relax to accept `_`** | already refuses loudly → **keep, verify message** | **OUT of scope (FR-008a)** — add a test pinning the loud refusal |

**Gleam is the reference for refusal behaviour**: it is the only runtime that never returns a wrong
answer for either defect. Dart and C# are being brought up to it, not the other way round.

---

## 4 · Regression protection — the shapes that must NOT break

Baseline first, per `DISCIPLINE.md §2.2`. Suite: `bash test/run_all_tests.sh` (expected 546 REPL).

Pinned as must-keep-working, because §2.2's fallback is what site 7/8 currently catches:

```
p([]).                    empty list
p([a]).                   single element
p([a|T]).                 bare variable tail
p([a,b|[]]).              explicit nil tail
p([[a],[b]]).             nested lists
p([send(1,a)]).           struct in list      (L2 - measured working, must stay)
Term =.. Parts?           =.. in clause body  (L1 - measured working, must stay)
```

---

## 5 · Sequencing

1. Baseline the suite; record counts.
2. Dart: helpers + 8 sites. Re-run suite.
3. Regression tests for FR-001..FR-005 shapes + the §4 keep-working list + FR-007 session survival.
4. C#: mirror. Run xUnit.
5. Gleam: accept `_` at the 3 non-conjunction positions; add the FR-008a conjunction-refusal test.
6. Cross-runtime parity vectors (SC-003) + Gleam declared-divergence test (SC-003a).
7. Documentation (US3/SC-005): correct **`CLAUDE.md`'s Known-limitations block**, which is
   measurably wrong today, and `docs/known-issues.md` — retire L1 and L2 **dated, with evidence**,
   never silently deleted. Correct the stale source location (`glp_repl.dart` → `glp_engine.dart`).

---

## 6 · Risks

| risk | mitigation |
|---|---|
| Site 7/8 refusal breaks a goal in the existing suite that relied on tail-swallowing | §4 keep-working list; full suite before and after. **If the suite goes red here, that is a real finding, not a test to edit.** |
| C#/Gleam sites drift from Dart | one shared parity vector table drives all three (SC-003) |
| `_` at a declared *input* (reader) position | out of scope by spec Edge Cases — type-checker rules unchanged |
| The 4→8 site count means the spec's baseline understated the work | recorded here; spec's Measured Baseline stays as the record of what was *observed* |
