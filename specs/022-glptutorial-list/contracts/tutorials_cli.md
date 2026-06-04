# Contract: `codeconv tutorials` CLI + `/glptutorial-list` skill

**Feature**: `022-glptutorial-list` | **Date**: 2026-06-03
**Refs**: spec FR-001…FR-011, SC-001…SC-005; research D1, D5, D8

This is the authoritative interface contract for the Python engine and its skill
front-end. The two surfaces MUST produce equivalent listings (FR-009).

---

## Command: `codeconv tutorials list [TUTORIAL] [OPTIONS]`

Pure, bridge-free built-in (research D1) — wired via `app.add_typer(...)` in
`codeconv/src/codeconv/cli.py`, NOT through `runner.tool_registry()`. It MUST NOT
acquire the PGLite bridge, start DBOS, or spawn the REPL.

### Arguments
| Arg | Required | Meaning |
|---|---|---|
| `TUTORIAL` | no | A chapter identifier (id/prefix/title, D5). Omitted → full catalog (FR-001). Present → only that chapter (FR-002). |

### Options
| Flag | Default | Effect |
|---|---|---|
| `--corpus <path>` | `<repo-root>/tutorials/olamni` | Override the vendored corpus root (tests, FR-007). |
| `--json` | off | Emit the structured model instead of human-readable text (FR-009 parity, testing). |
| `--quiet` | off | Suppress non-error stderr warnings (FR-011 warnings still suppressible; errors are not). |

### stdout — human-readable listing (default; FR-005)
```
ch03 — GLP Core
  exercise-01
    ch-03-ex-01-glp-fair-stream-merger.glp  — Program 3.1: GLP Fair Stream Merger pipeline
    ch-03-ex-01-producer-consumer.glp       — producer/2 + consumer/3 exemplar (book §4.2)
  exercise-02
    ch-03-ex-02-defined-guards.glp          — §3.2 defined guards
ch08 — The Grassroots Social Graph
  (no scripts)
```
- Grouped by chapter → exercise → script, indented for scannability (FR-005).
- Every chapter present in the corpus appears (SC-002); empty chapter shows
  `(no scripts)` (FR-008).
- Each script shows `name — description`; `(no description)` when none (US3 #2).

### stdout — `--json`
```json
{
  "corpus_root": "tutorials/olamni",
  "chapters": [
    {
      "id": "ch03",
      "title": "GLP Core",
      "is_empty": false,
      "exercises": [
        {
          "number": "01",
          "md_description": "Program 3.1 + ch4 producer/consumer composed pipeline",
          "scripts": [
            {"name": "ch-03-ex-01-glp-fair-stream-merger.glp",
             "path": "tutorials/olamni/ch03/exercise-01/ch-03-ex-01-glp-fair-stream-merger.glp",
             "description": "Program 3.1: GLP Fair Stream Merger pipeline",
             "description_source": "exercise_md"}
          ]
        }
      ]
    }
  ],
  "warnings": ["skipped non-standard dir: ch03/spec-rev-eng-input"]
}
```

### stderr
- FR-011 warnings: `warning: skipped non-standard dir: <relpath>` (suppressed by
  `--quiet`).
- FR-006 errors: corpus-unreachable names the path tried; unknown identifier
  prints "no match" + available ids.

### Exit codes
| Code | Condition |
|---|---|
| `0` | Listing produced (full catalog, single chapter, or empty chapters present). |
| `3` | Unknown tutorial identifier (no match) — stderr lists available ids (SC-003). |
| `4` | Ambiguous identifier (≥2 matches) — stderr lists candidates (D5). |
| `5` | Corpus unreachable / unreadable — stderr names the path tried (FR-006). |

(Codes 3/4/5 are listing-specific; they sit alongside codeconv's generic
0/1/2/64/65/70 and do not collide with bridge codes, since this path never
touches the bridge.)

### Behavioral guarantees
- **Read-only** (FR-010): never executes a `.glp`, never writes to the corpus.
- **Deterministic order**: chapters by id, exercises by number, scripts by name.
- **Performance**: full catalog < 3 s (SC-005).
- **Coverage**: 100% of chapters and 100% of `.glp` scripts under recognized
  exercises listed (SC-002).

---

## Reserved (companion / supporting) commands — declared, not built here

| Command | Status | Purpose |
|---|---|---|
| `codeconv tutorials run …` | reserved | Companion `/glptutorial-run` execution feature. |
| `codeconv tutorials sync [--check]` | supporting (D3) | Re-vendor from sibling + write/verify `SNAPSHOT.md`/`.snapshot.json`; build-time only; `--check` exits non-zero on drift. |

---

## Skill: `/glptutorial-list`

`.claude/skills/glptutorial-list/SKILL.md` — a thin front-end (FR-009), modeled on
the existing `codeconv-*` skills.

Contract:
1. Resolve the codeconv venv (`codeconv/.venv/Scripts/python.exe` on Windows,
   `codeconv/.venv/bin/python` on POSIX). If absent, instruct Gabi to create it.
2. Invoke `codeconv tutorials list <args verbatim>` from the repo root.
3. Relay stdout/stderr unchanged.
4. MUST NOT add behavior beyond forwarding — the CLI is the single engine; the
   two surfaces produce equivalent listings (FR-009).
5. Does NOT run any tutorial (read-only, FR-010) — running is `/glptutorial-run`.

### Equivalence test (FR-009)
A test asserts the skill's documented invocation maps 1:1 to the CLI command and
that `--json` output is identical regardless of entry point (the skill adds no
transformation).
