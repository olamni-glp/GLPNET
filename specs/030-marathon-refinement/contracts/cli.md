# Contract — CLI surface

Command root: `codeconv marathon …` (statically registered Typer sub-app, unchanged from 024). Global
options `--repo-root`, `--data-dir`, `--json` are inherited from the `codeconv` callback. Each subcommand
is a thin wrapper over exactly one library function ([`library-api.md`](./library-api.md)) — FR-025 parity.

## Exit codes (unchanged from 024 convention)
| Code | Meaning |
|---|---|
| 0 | OK |
| 2 | ESCALATION — blocked, not an error (fork divergence, budget halt, push_blocked, prereq-against-done) |
| 64 | USAGE — filesystem guard (non-NTFS/ReFS per-run store path), ambiguous `--feature`/`--run` |
| 70 | INTERNAL — unexpected failure |

## Subcommands
| Subcommand | Args | Library call |
|---|---|---|
| `register` | `--run <id> [--stages a,b,c] [--title] [--budget <n>] [--budget-unit]` | `register_run(...)` |
| `append-stage` | `--run <id> <name>` | `append_stage(run, name)` |
| `stage-start` | `--run <id> <name>` | `start_stage(run, name)` |
| `checkpoint` | `--run <id> <name> [--completed json] [--remaining json] [--wip] [--budget n] [--paths a,b] [--issues …] [-m msg]` | `checkpoint(...)` |
| `capture` | `--run <id> --kind <k> --title <t> [--description] [--blocks <stage>]` | `capture_item(...)` |
| `resume` | `--run <id> [--json]` | `resume(run)` → on fork exit 2 |
| `position` | `--run <id> [--json]` | `resume(run)` (alias surfacing the four-field position) |
| `status` | `--run <id> [--emit] [--json]` | `status_line` / `emit_status` |
| `gate` | `--stage <id> [--approve \| --change --plan <ref>] [--by <name>]` | `present_gate` / `record_decision` |
| `rerun` | `--stage <id> [--subagent <name>]` | `rerun_block` / `rerun_subagent` |
| `trace` | `--run <id> --subject <s> --input <json> [--score f] [--accept \| --reject]` | `write_trace` |
| `reconcile` | `--run <id> [--json]` | `reconcile(run)` → on fork exit 2 |
| `finalize` | `--run <id>` | `finalize(run)` |
| `keeper start\|stop\|recover` | `--run <id>` | `start_keeper`/`stop_keeper`/`recover_keeper` |
| `doctor` | `--run <id> [--json]` | endpoint reachability, active store, last-seq per store, open escalations, budget headroom |

`--run <id>` (canonical; the run's `run_id`) identifies the run in every subcommand. `--feature <slug>` is a
**deprecated alias** accepted only for back-compat with 024's driving skill (it derives `--run`).
