# Contract — `codeconv builder` CLI + skill loop

Auto-discovered by the feature-012 runner (subpackage `tools/builder/`,
exports `app: typer.Typer` + `register_workflows`). Global `--repo-root` /
`--data-dir` inherited from the console script.

## Subcommands

| Cmd | Effect | Idempotence |
|---|---|---|
| `builder run` | launch/resume the outer workflow; drive frontier in 015 topo/SCC order | re-run resumes (R9); never double-processes |
| `builder resume` | explicitly recover pending DBOS workflows | safe to repeat |
| `builder status` | unified per-file state + counts, <5 s (FR-017/SC-009) | read-only |
| `builder trace [--file R｜--run ID]` | DBOS workflow/step history (D1=a) | read-only |
| `builder retry --file R` | re-drive one file/SCC without disturbing others (FR-018) | scoped |
| `builder redrive` | recompute frontier after escalations resolved | safe |
| `builder aggregate-escalations` | single `_escalations-report.md` (FR-013/014) | regenerated |

Default (bare `codeconv builder`) = `status` (spawns nothing).

## Flags

`--dry-run` (plan only, write nothing), `--json` / `--quiet`,
`--limit N` (agent concurrency cap, default from R12), `--respec`
(opt-in re-convspec on drift), `--restart-run` (explicit, non-default;
R13), `--data-dir`.

## Exit codes (carry-forward 012/015/017)

`0` ok / nothing-to-convert (FR-020); `2` usage; `3` bridge unreachable;
`4` stale tombstone↔DB divergence (FR-019, refuses to proceed); `5` open
escalations block conversion; `64` non-NTFS data-dir guard.

## JSON shapes

`status` → `{files:[{path,state,deps_blocking,open_escalations}],counts,
run:{outer_workflow_id,outcome}}`. `trace` → `{workflow_id,steps:[{stage,
status,started,finished}]}` projected from `dbos.*` via `builder_runs`.

## Skill durable-orchestration loop (pseudocode — `/codeconv-builder`)

```
resolve venv + repo-root (as codeconv-depgraph skill)
loop:
  r = codeconv builder run --json
  if r.outcome == nothing_to_convert: report; break
  if r.needs_agent_work:                       # NeedsAgentWork surfaced
     for file in r.needs_agent_work (≤ --limit, SCC grouped):
        spawn convspec analysis sub-agent(file)         # see agent_orchestration
        if agent requests research and KB miss:
           spawn SEPARATE research sub-agent(construct) # FR-010, official-docs
     continue          # re-drive: DBOS recovers same workflow ids
  if r.escalations: surface _escalations-report.md; await human (FR-013/014)
  if r.outcome == completed: report counts; break
```

The skill carries ONLY orchestration + agent spawning (justified deviation,
plan Complexity Tracking). All state/decisions are the deterministic Python
tool's; the skill never invents state.
