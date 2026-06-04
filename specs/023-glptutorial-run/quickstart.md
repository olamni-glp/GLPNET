# Quickstart — /glptutorial-run

**Feature**: `023-glptutorial-run` | **Date**: 2026-06-04

Run and explain a single GLP tutorial example, unified across both chapter shapes.
Selection reuses `/glptutorial-list` (feature 022); execution runs the real example
through a REPL backend (C# default, Dart on demand).

> Prerequisite: the codeconv venv (`codeconv/.venv` on Windows). Run from the repo
> root `D:\bstdev\research\glp\glpnet`. Bridge-free — no PGLite/DBOS needed.

---

## 1. Find an example (feature 022)

```
codeconv tutorials list ch01
```

Pick a chapter + exercise — e.g. `ch01` / `exercise-01`.

## 2. Preview before running (US3, FR-005)

```
codeconv tutorials preview ch01 01
```

Shows the load target, the documented goal(s), and the expected outcome from the
tutorial `.md` — **nothing is executed**.

## 3. Run it (US1, section-driven)

```
codeconv tutorials run ch01 01
```

Loads the single `.glp` on the **C# REPL** (the mandated default), runs the
documented goal, and reports the outcome-only result:

```
backend: csharp
GLP> merge([1,2,3],[a,b],Xs).
Xs = [1, a, 2, b, 3]
→ succeeds
verdict: match (vs ex-01-repl-trace.md golden)
```

## 4. Run a use-case example with the SAME command (US2, the unification)

```
codeconv tutorials run ch07 01
```

Resolves `ch07/exercise-01` to the canonical project `programs/cssg_modules/` in the
sibling repo and the play goal `fplay1.`, loads the module project, runs the play,
and reports the outcome — **no shape-specific step**:

```
backend: csharp
✓ Loaded project: .../programs/cssg_modules
GLP> :limit 1000000
GLP> fplay1.
tagged(alice, cmd(connect(bob)))
…
→ suspended
verdict: match (suspended is the documented steady state)
```

`→ suspended` is a **valid** outcome here (FR-010), not an error.

## 5. Explain the outcome (US4, FR-009)

```
codeconv tutorials explain ch01 01
```

Runs, then compares to the golden and explains the result with reference to the
guide `.md` — reporting a match or surfacing+explaining any difference (never a
silent pass).

## 6. Choose the backend (US5, FR-007)

```
codeconv tutorials run ch01 01 --backend dart     # Dart REPL on demand
codeconv tutorials run ch01 01 --backend cs       # C# (default; mandated)
```

If the C# backend is unavailable or wrong, the tool reports a **critical P1 defect**
(exit 8) — optionally falling back to Dart with a prominent P1 notice — never a
silent hang/crash/pass.

## 7. JSON for tooling

```
codeconv tutorials run ch01 01 --json
codeconv tutorials explain ch07 01 --json
```

## 8. Restructuring proposals (read-only; FR-013)

```
codeconv tutorials propose ch07            # read-only normalization report
codeconv tutorials propose ch07 --apply --approve 01 --rationale "add run-manifest"
```

`--apply` is **approval-gated** (FR-019): targets the sibling source of truth, then
re-vendors; semantics/clause-text-preserving and revertible. Without the approval
flags, nothing is modified.

## 9. Keep the snapshot honest (drift guard, FR-012)

```
codeconv tutorials sync --check     # selected == executed; non-zero on drift
```

`run`/`explain` refuse to run a drifted example (exit 11) and tell you to re-sync.

---

## Skill front-end (FR-014)

The `/glptutorial-run` skill forwards verbatim to `codeconv tutorials <verb> …` and
produces equivalent behaviour:

```
/glptutorial-run run ch07 01
/glptutorial-run explain ch01 01
```

---

## Not-yet-implemented chapters

`ch08`–`ch13` are planned stubs at spec time:

```
codeconv tutorials run ch08 01
# error: ch08 is not yet available (no runnable examples). (exit 9)
```
