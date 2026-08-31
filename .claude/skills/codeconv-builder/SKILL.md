---
name: codeconv-builder
description: Drive the unified, DBOS-durable Dart→C#/.NET conversion pipeline with one resumable command. Use when the user types `/codeconv-builder` or asks to run/resume the whole conversion pipeline, check builder status/trace, retry a file, or re-drive the frontier.
argument-hint: "[run|resume|status|trace|retry|redrive|aggregate-escalations] [flags]"
compatibility: "Claude Code (Agent tool required for the convspec orchestration loop)"
---

# /codeconv-builder

Wrapper over `codeconv builder` for **all deterministic state** (frontier
in feature-015 topo/SCC order, durable workflow launch/resume, status,
trace). The Python CLI is the single source of truth for state and the
skill forwards arguments verbatim for every state operation.

The skill **additionally** carries the durable-orchestration loop and
the **`needs_agent_work`** handler — detecting when the convspec stage
needs an agent (via `codeconv builder run`'s exit code / `--json`
outcome, **not** a caught Python exception) and spawning the convspec
analysis sub-agent (and, on an idiom-KB miss, a SEPARATE research
sub-agent) through the Claude Code **Agent tool**. Spawning Claude
sub-agents is a harness capability the Python CLI structurally cannot
perform; this is the only "skill machinery" beyond the
`/codeconv-discover` / `/codeconv-depgraph` thin-wrapper pattern,
required by FR-009/FR-010 and recorded in plan Complexity Tracking +
research R1/R2. The skill contains **no deterministic-state logic**.

## What this skill does

1. Resolve the codeconv venv: `codeconv/.venv/Scripts/python.exe` on
   Windows, `codeconv/.venv/bin/python` on POSIX. If absent, instruct
   Gabi to create it (`python -m venv codeconv/.venv && …pip install -e
   codeconv[dev]`).
2. Run from the repo root. Per `CLAUDE.md`, pass
   `--data-dir D:/bstdev/research/glp/glpnet/.pgdb` for **consistency + shared-
   bridge reuse** (a healthy bridge already runs there). D: is NTFS, so
   this is a convention, not a filesystem necessity; the CLI guard
   (exit 64) no longer triggers on D:.
3. For an explicit subcommand: run `codeconv --data-dir
   D:/bstdev/research/glp/glpnet/.pgdb builder <args verbatim>` and show
   stdout/stderr.
4. For the **orchestration** flow (bare `/codeconv-builder` or
   `/codeconv-builder run`): run the loop in § "Orchestration loop".

## Subcommands and flags

| Subcommand | Purpose |
|---|---|
| `run` | Launch/resume the outer durable workflow; drive the frontier in 015 topo/SCC order. Re-run **resumes** (deterministic workflow id, R9) — never double-processes (FR-004). |
| `resume` | Explicitly recover pending DBOS builder workflows (R12). Idempotent. |
| `status` (default — bare `/codeconv-builder` → status) | Per-file unified state (`not_started｜blocked_on_deps｜analysed｜specced｜scaffolded｜converted｜escalated｜complete`) + counts (FR-017). Read-only. |
| `trace [--file R｜--run ID]` | DBOS workflow/step history for debugging/planning (D1=a). Read-only. |
| `retry --file R` | Re-drive one file/SCC without disturbing others (FR-018). |
| `redrive` | Recompute the frontier after escalations resolved (FR-018). |
| `aggregate-escalations` | Single `.codeconv/conversion-idioms/_escalations-report.md` (FR-013/014). |

| Flag | Applies | Default | Effect |
|---|---|---|---|
| `--dry-run` | run | off | Compute frontier; write/launch NOTHING, spawn NO agents. |
| `--limit <n>` | run | R12 cap | Cap units this drive (agent concurrency throttle; SCC units never split). |
| `--restart-run` | run | off (R13) | Explicit, non-default: mint a NEW run epoch instead of resuming the most-recent non-terminal run. |
| `--respec` | run | off | Opt-in re-convspec on tombstone↔DB drift. |
| `--json` / `--quiet` | all | off | JSON summary / suppress logging. |
| `--data-dir <path>` | all (top-level) | `<repo>/.pgdb` | Pass `D:/bstdev/research/glp/glpnet/.pgdb` for shared-bridge reuse (convention). |

## Pre-execution checks

- Schema migrations must have run (`/codeconv-runner migrate` — single
  head `0005`). Empty/absent `codeconv.dart_depgraph` ⇒ `run` exits 0
  with **"nothing to convert"** (FR-020) — not an error.
- The unified bridge auto-spawns on first call (~5–7 s PGLite cold-init,
  memory `project_pglite_cold_init_windows.md`).
- Tombstone↔DB divergence ⇒ exit 4 ("stale — rebuild required");
  open escalations blocking conversion ⇒ exit 5. Do **not** work around
  either — surface to Gabi (CLAUDE.md Bug Protocol).

## Orchestration loop (bare `/codeconv-builder` or `run`)

Per `contracts/builder_cli.md`. The loop owns ONLY agent spawning +
re-drive; **all** state/decisions are the deterministic CLI's.

```
resolve venv + repo-root (as the /codeconv-depgraph skill does)
loop:
  r = codeconv --data-dir D:/bstdev/research/glp/glpnet/.pgdb builder run --json
  if r.outcome == "nothing_to_convert":  report; break          # FR-020, exit 0
  if r.exit_code == 4:  surface "stale tombstone↔DB"; STOP (ask Gabi) # FR-019
  if r.exit_code == 5 or r.escalations:                          # FR-013/014
       run `builder aggregate-escalations`; surface
       .codeconv/conversion-idioms/_escalations-report.md; await human
       break
  if r.needs_agent_work:                # awaiting-agent (NOT an exception)
       for unit in r.needs_agent_work (≤ --limit, SCC members as ONE batch):
          spawn convspec analysis sub-agent(unit)        # see /codeconv-convspec
          if agent reports an idiom-KB miss for a construct:
              spawn a SEPARATE research sub-agent(construct)  # FR-010, official-docs
       continue       # re-drive: DBOS recovers the SAME workflow ids (R9)
  if r.outcome == "completed":  report counts; break
```

The analysis + research sub-agent prompt contracts live in
`/codeconv-convspec` (`agent_orchestration.md`): escalate-don't-guess
(FR-013), spec-only / never emit C# (FR-023), official-docs
authoritative with recorded provenance (FR-024). On re-drive the
convspec DBOS step finds the checked-in artifact and completes
deterministically (replay-safe — R3).

## Discipline

- The skill never invents or mutates state — every decision is the
  CLI's JSON output. If the CLI and an agent disagree, the CLI wins.
- A non-zero exit that is NOT `needs_agent_work` (4 stale / 5 escalations
  / 3 bridge / 64 guard / 2 usage) is surfaced verbatim to Gabi, not
  worked around (CLAUDE.md Bug Protocol).
- SCC groups are one indivisible unit — never spawn a partial cycle.
