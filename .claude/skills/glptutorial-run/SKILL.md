---
name: glptutorial-run
description: Run & explain a single GLP tutorial example on the C# GLP REPL (mandated default; Dart on demand). Use when the user types `/glptutorial-run` or asks to run/preview/explain a GLP tutorial exercise, or to propose corpus normalizations. Thin front-end over `codeconv tutorials {preview,run,explain,propose}`; the companion of `/glptutorial-list`.
---

# /glptutorial-run

Thin front-end over the bridge-free `codeconv tutorials` run layer. Forwards
arguments verbatim and relays output unchanged — the CLI is the single engine,
so the skill and the CLI produce equivalent behaviour (FR-014, like
`/glptutorial-list`). One **unified run-model** turns a selected exercise into an
executed outcome across BOTH chapter shapes (section-driven single/multi-file and
the ch07 use-case project), so the examples `/glptutorial-list` shows as
`(no scripts)` are runnable here too.

## What this skill does

1. Resolve the codeconv venv (`codeconv/.venv/Scripts/python.exe` on Windows,
   `codeconv/.venv/bin/python` on POSIX). If absent, instruct Gabi to run
   `python -m venv codeconv/.venv && codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]` first.
2. Invoke `codeconv tutorials <verb> <args verbatim>` from the **repo root**.
3. Show stdout/stderr and the exit code unchanged. Add no behaviour beyond forwarding.

This path is **bridge-free** (research D1): it reads files and shells out to a
GLP REPL subprocess. It does NOT spin up the PGLite bridge, start DBOS, or import
the engine — guarded by `test_tutorials_no_bridge.py`. No LM on the run path.

## Verbs

`/glptutorial-run <verb> <CHAPTER> <EXERCISE> [flags]` — verb ∈ {preview, run, explain}.
`/glptutorial-run propose [CHAPTER] [--apply ...]`.

| Verb | Effect |
|---|---|
| `preview` | Show the documented goal(s) + expected outcome from the tutorial `.md` — **no execution** (FR-005). |
| `run` | Load + run the example on the selected backend; report the actual outcome (bindings + `→ succeeds｜suspended｜failed`) + a brief match/difference verdict (FR-006/008). |
| `explain` | `run`, then compare to the golden and explain it referencing the `.md`; a difference is always surfaced (FR-009/010). |
| `propose` | Read-only normalization report (run-manifests, drift gaps, stale/spec-violating artefacts). `--apply` is approval-gated (FR-013/019). |

`CHAPTER` = id/prefix/title (`ch01`, `1`, `core`); `EXERCISE` = number (`01`, `1`).

## Flags

| Flag | Default | Effect |
|---|---|---|
| `--goal "<text>"` | from the guide | Run a chosen/extra goal (repeatable); for `run`/`explain`. |
| `--backend cs｜dart` | `cs` | Backend. **C# is the mandated default**; `dart` on demand (FR-007/018). |
| `--limit N` | from the guide | Reduction limit (`:limit N`) — needed for plays. |
| `--timeout SECS` | 120 | Bound a non-terminating goal → a reported P1, not a hang. |
| `--corpus / --sibling-corpus / --sibling-glp-root` | see contract | Selection snapshot + the two execution roots (D4). |
| `--json` | off | Emit the structured model (skill↔CLI parity). |

## Backend policy (FR-007/018)

The **C# GLP REPL is the mandated default**. A non-working / wrong C# backend is
a **critical P1 defect** — surfaced loudly (exit 8) with the captured error, never
a silent hang/crash/pass. An optional Dart fallback runs only with a prominent
`p1_notice` and never masks the C# failure.

## Exit codes (extend the 022 set)

| Code | Condition |
|---|---|
| `0` | OK |
| `3` / `4` / `5` | no match / ambiguous / corpus unreachable (reused) |
| `6` | example has no resolvable load target |
| `7` | no resolvable goal and none supplied (`--goal`) |
| `8` | selected backend unavailable / C# P1 defect |
| `9` | chapter/example not yet implemented (or a deferred shape) |
| `10` | goal hit a documented REPL limitation |
| `11` | snapshot/sibling drift — refused to run |

## Examples

- `/glptutorial-run preview ch01 01` → goals + expected outcome, no execution.
- `/glptutorial-run run ch01 01` → runs the fair-merge goals on C#, reports outcomes.
- `/glptutorial-run run ch07 01` → loads `programs/cssg_modules`, runs `fplay1` (→ suspended).
- `/glptutorial-run explain ch01 01 --json` → run + verdict + explanation, machine-readable.
- `/glptutorial-run propose` → read-only corpus normalization report.

## What this skill does NOT do

- Does NOT modify the corpus except via the approval-gated `propose --apply`
  (requires `--approve <EXERCISE>` + `--rationale`).
- Does NOT use an LM on the run path — explanations are assembled from the guide
  prose + the verdict.

## Contract

`specs/023-glptutorial-run/contracts/tutorials_run_cli.md` and
`contracts/repl_backend.md` are the source of truth. Keep this skill in sync; if
you change behaviour here, update the contract first.
