---
name: glptutorial-list
description: Read-only browser for the vendored GLP tutorial corpus. Use when the user types `/glptutorial-list` or asks to list/browse the GLP tutorials or the scripts within a chapter. Thin front-end over `codeconv tutorials list` — the selection front-end for the companion `/glptutorial-run`.
---

# /glptutorial-list

Thin front-end over `codeconv tutorials list`. Forwards arguments verbatim and
relays the output unchanged — the CLI is the single engine, so the skill and the
CLI produce equivalent listings (FR-009). Read-only: it never runs a tutorial
(execution is the companion `/glptutorial-run` feature, FR-010).

## What this skill does

1. Resolve the codeconv venv (`codeconv/.venv/Scripts/python.exe` on Windows,
   `codeconv/.venv/bin/python` on POSIX). If absent, instruct Gabi to run
   `python -m venv codeconv/.venv && codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]` first.
2. Invoke `codeconv tutorials list <args verbatim>` from the repo root.
3. Show stdout/stderr from the run unchanged. Add no behavior beyond forwarding.

This path is **bridge-free** (research D1): `codeconv tutorials list` is a pure
filesystem walk of the vendored corpus at `tutorials/olamni/`. It does NOT spin
up the PGLite bridge, start DBOS, or spawn the REPL, so there is no cold-init
penalty and no migration prerequisite.

## Arguments and flags

`/glptutorial-list [TUTORIAL] [flags]`

| Arg/Flag | Default | Effect |
|---|---|---|
| `TUTORIAL` | — | Chapter id/prefix/title (e.g. `ch03`, `3`, `core`). Omit → full catalog (FR-001); present → only that chapter (FR-002). |
| `--corpus <path>` | `tutorials/olamni` | Override the vendored corpus root (testing). |
| `--json` | off | Emit the structured model instead of human-readable text (FR-009 parity). |
| `--quiet` | off | Suppress non-error warnings (FR-011 warnings; errors still print). |

## Output

- Human-readable, grouped and indented as chapter → exercise → script, each
  script line `name — description`. Empty (planned) chapters show `(no scripts)`
  (FR-008); a script with no derivable description shows `(no description)`.
- Warnings (non-standard dirs, FR-011) and errors (corpus unreachable, unknown
  identifier) go to stderr.

## Exit codes

| Code | Condition |
|---|---|
| `0` | Listing produced (full catalog, single chapter, or empty chapters present). |
| `3` | Unknown tutorial identifier — stderr lists available ids (SC-003). |
| `4` | Ambiguous identifier (≥2 matches) — stderr lists candidates. |
| `5` | Corpus unreachable / unreadable — stderr names the path tried (FR-006). |

## Examples

- `/glptutorial-list` → full catalog, every chapter → exercise → script.
- `/glptutorial-list ch03` → only chapter 3.
- `/glptutorial-list 3` → zero-pad normalized → ch03.
- `/glptutorial-list core` → title substring → ch03 "GLP Core".
- `/glptutorial-list --json` → machine-readable model (skill↔CLI parity).

## What this skill does NOT do

- Does NOT execute any tutorial script — that is the companion `/glptutorial-run`
  (read-only, FR-010).
- Does NOT read the sibling GLP repo at list time — only the build-time
  `codeconv tutorials sync` does (FR-007).
- Does NOT enrich descriptions with an LM — extraction is mechanical (D7).

## Supporting: refresh the vendored snapshot (build-time, D3)

- `codeconv tutorials sync` re-vendors the sibling corpus into `tutorials/olamni/`
  and rewrites `SNAPSHOT.md` + `.snapshot.json`.
- `codeconv tutorials sync --check` verifies the vendored tree against the
  manifest (and the sibling if present); non-zero exit on drift.

## Contract

`specs/022-glptutorial-list/contracts/tutorials_cli.md` is the source of truth.
This skill MUST stay in sync with that contract; if you change behavior here,
update the contract first.
