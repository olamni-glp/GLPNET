# Contract — `codeconv tutorials {preview,run,explain,propose}` + `/glptutorial-run` skill

**Feature**: `023-glptutorial-run` | **Date**: 2026-06-04
**Depends on**: [research.md](../research.md) (D1, D10), [data-model.md](../data-model.md)

This is the authoritative interface contract for the run/preview/explain/propose
surface. The CLI is the engine; the `/glptutorial-run` skill forwards to it and
MUST produce equivalent behaviour (FR-014, mirroring `/glptutorial-list`).

The surface extends the existing **bridge-free** `tutorials` Typer sub-app
(`codeconv/src/codeconv/tutorials/cli.py`), replacing the reserved `run` stub. It
never acquires the PGLite bridge / DBOS (D1).

---

## Selector (shared by all four verbs)

Every verb takes the **uniform selectable unit** (FR-003): a chapter identifier +
an exercise number, resolved consistently with `/glptutorial-list`'s matcher.

```
codeconv tutorials <verb> <CHAPTER> <EXERCISE> [options]
```

- `CHAPTER` — chapter id/prefix/title (`ch01`, `1`, `core`), via `match_tutorial`.
- `EXERCISE` — exercise number (`01`, `1`); zero-pad normalized.

Shared options:

| Option | Default | Meaning |
|---|---|---|
| `--corpus PATH` | `tutorials/olamni` | vendored snapshot root (selection) |
| `--sibling-corpus PATH` | `D:/bstdev/research/glp/GLP/olamni/tutorial` | sibling corpus exec root (section-driven) |
| `--sibling-glp-root PATH` | `D:/bstdev/research/glp/GLP` | sibling GLP repo root (use-case projects) |
| `--json` | off | emit the structured model instead of human text |
| `--quiet` | off | suppress non-error warnings |

---

## `preview` (FR-005 / US3) — no execution

```
codeconv tutorials preview <CHAPTER> <EXERCISE> [--json]
```

Shows, **without running anything**: the resolved load target, the documented
goal(s) (all of them, so the user can choose — FR-004), and the expected outcome
drawn from the guide / golden. Attributes goals + expected outcome to the tutorial
`.md`. If no goal is resolvable, says so and indicates a goal must be supplied to
run (exit 7 only applies to `run`/`explain`; `preview` reports it as text, exit 0).

---

## `run` (FR-006/008 / US1+US2) — execute, report actual outcome

```
codeconv tutorials run <CHAPTER> <EXERCISE> \
    [--goal "<text>"]... [--backend cs|dart] [--limit N] [--timeout SECS] [--json]
```

Loads the resolved target on the selected backend (default C#), runs the goal(s),
captures the **outcome-only** result (bindings + `→ succeeds|suspended|failed`,
D7), and reports it naming the backend used. Emits a brief match-or-difference
verdict line vs the golden (the full elaboration is `explain`).

| Option | Default | Meaning |
|---|---|---|
| `--goal "<text>"` | (from guide) | run a chosen/extra goal; repeatable; runs in sequence |
| `--backend cs\|dart` | `cs` | backend selection (FR-007: `cs` is the mandated default) |
| `--limit N` | (from guide) | reduction limit (`:limit N`) — needed for plays |
| `--timeout SECS` | sane default | bound a non-terminating goal → reported, not a hang |

**C# P1 behaviour (FR-007/018):** a non-working/wrong C# backend exits 8 with a
clearly-labelled P1 message and the captured error; `--backend cs` MAY fall back to
Dart **only with a prominent P1 notice** in output — never silently, never masking.

---

## `explain` (FR-009 / US4) — run + compare + explain

```
codeconv tutorials explain <CHAPTER> <EXERCISE> [--goal "<text>"]... [--backend cs|dart] [--json]
```

Runs as `run`, then compares the actual outcome-only result to the example's golden
and explains it **referencing the tutorial `.md`**:

- `MATCH` — reports the match and explains the outcome from the guide prose.
- `DIFFERENCE` — surfaces the difference field-by-field and explains it; **never** a
  silent pass.
- A `→ suspended` outcome is explained as the example's expected behaviour where the
  guide documents suspension (FR-010).
- `NO_GOLDEN` — reports the actual outcome and states no golden exists.

---

## `propose` (FR-013/019) — read-only normalization report; gated apply

```
codeconv tutorials propose [<CHAPTER>] [--apply --approve <EXERCISE> --rationale "<why>"] [--json]
```

Default (no `--apply`): emits a **read-only** report of inconsistencies + suggested
improvements (run-manifests, drift-gap flags, stale-artefact flags, layout
normalisations — D9). Mutates nothing.

`--apply` (FR-019): requires `--approve <EXERCISE>` **and** `--rationale`; applies
the layout/metadata-level change to the **sibling source of truth**, re-vendors
(`tutorials sync`), preserves program semantics + book-exact clause text, and is
independently revertible. Absent both flags, no file is modified.

---

## Exit codes (extends the 022 set; D10)

| code | meaning | verbs |
|---|---|---|
| 0 | ok | all |
| 3 | no tutorial match | all |
| 4 | ambiguous tutorial match | all |
| 5 | corpus unreachable | all |
| 6 | example has no resolvable load target | run, explain, preview* |
| 7 | no resolvable goal and none supplied | run, explain |
| 8 | selected backend unavailable / C# P1 defect | run, explain |
| 9 | chapter/example not yet implemented | run, explain, preview |
| 10 | goal hit a documented REPL limitation | run, explain |
| 11 | snapshot/sibling drift — refused to run | run, explain |

\* `preview` reports conditions textually (exit 0) where it does not execute, except
unreachable corpus / no-match / not-implemented which use the shared codes.

Every non-zero exit prints an **actionable** stderr message naming what was tried
(FR-016): the path, the identifier, the backend, or the limitation.

---

## JSON model (`--json`)

`run`/`explain` emit a stable, indented object:

```json
{
  "chapter": "ch01",
  "exercise": "01",
  "shape": "section_driven",
  "load_target": { "kind": "single_file",
                   "select_path": "tutorials/olamni/ch01/exercise-01/ch-01-ex-01-fair-stream-merger.glp",
                   "exec_path": "D:/bstdev/research/glp/GLP/olamni/tutorial/ch01/exercise-01/ch-01-ex-01-fair-stream-merger.glp" },
  "backend_used": "csharp",
  "goals": [
    {
      "text": "merge([1,2,3],[a,b],Xs).",
      "source": "guide",
      "actual":  { "bindings": [{"name":"Xs","value":"[1, a, 2, b, 3]"}], "status": "succeeds" },
      "golden":  { "bindings": [{"name":"Xs","value":"[1, a, 2, b, 3]"}], "status": "succeeds" },
      "verdict": { "kind": "match", "diffs": [] }
    }
  ],
  "p1_notice": null,
  "warnings": []
}
```

`preview` omits `actual`/`verdict`/`backend_used`; `propose` emits a `proposals`
array. Human (default) output is the readable rendering of the same model.

---

## Skill ↔ CLI equivalence (FR-014)

`.claude/skills/glptutorial-run/SKILL.md` is a thin forwarder: resolve the codeconv
venv (`codeconv/.venv/Scripts/python.exe` on Windows), run
`codeconv tutorials <verb> <args…>` verbatim from the repo root, relay stdout/stderr
and the exit code unchanged. No behaviour is added beyond forwarding. A parity test
asserts identical output/exit for representative invocations (mirrors 022).
