# Contract — DBOS Workflow/Step Model (D1=a; FR-002/003/004; D2 principle)

## Taxonomy

- **Outer workflow** `@DBOS.workflow` id `builder:{workspace_id}:{run_epoch}`
  — walks feature-015 `dart_depgraph` order (read-only), enqueues child
  workflows.
- **Child workflow** `@DBOS.workflow` id `file:{h(rel_path)}` or
  `scc:{h(sorted members)}` — one per file or per SCC group (SCC = **one
  indivisible unit**, FR-002). Body = ordered `@DBOS.step`s.
- **Stage step** `@DBOS.step`, one per stage
  (`discover｜depgraph-gate｜scaffold｜convspec｜plan｜…`). **Step body calls
  the existing tool entrypoint verbatim** (R1/R8, D2) and writes the same
  two-phase + tombstone projection the tool already writes.

## Replay-safety (HARD)

Every step body is deterministic and idempotent: DB/file reads + the existing
pure entrypoint + a two-phase write whose `*_completed_at` is the **terminal
action**. No LLM/web/network call inside any step (R3). On recovery DBOS
**skips completed steps** and re-enters at the interrupted step → crash
mid-file resumes at the interrupted *stage* (FR-003), and a resumed run is
bit-identical to an uninterrupted one (FR-004/SC-002).

## `needs_agent_work` protocol (convspec; R2/R3)

`convspec` step body: idiom-KB lookup → if artifact present+valid, record &
return; else **return the deterministic typed result `needs_agent_work(path)`**
— a *successful, replay-safe* step output, **never a raised exception**.
(Raising inside an `@DBOS.step` is recorded by DBOS as a **failed**
step/workflow, which is wrong for the normal first-time path and would break
the MVP convspec flow before the agent can produce the artifact.) The
**workflow** observes `needs_agent_work` and ends in the durable
**awaiting-agent** status (recorded via `builder_runs`/durable status),
surfaced by `builder run`'s exit code — not a Python exception. The **skill**
detects awaiting-agent from `builder status`/exit code, spawns the analysis
sub-agent (+ separate research sub-agent only on a KB miss), waits for the
checked-in artifact, then re-drives `builder run` → DBOS recovers the same
child workflow id and the step now finds the artifact and completes
deterministically.

## Concurrency / recovery (R12; D2)

- DBOS `Queue` worker concurrency **default 1** (serial through the 012
  single-writer bridge lock — proven flow, not a new concurrency model).
  Agent parallelism (≤N sub-agents) lives in the skill, not DBOS workers.
- Launch reuses `setup_dbos(endpoint)` + the vendored uuid-ossp patch
  verbatim (the already-proven `codeconv migrate` path). Embedded DBOS;
  conductor/external orchestration **disabled**.
- Recovery = DBOS startup `recover_pending_workflows` + explicit
  `codeconv builder resume`. No custom recovery loop (D2).

## Mid-run code change (R13)

Recovered workflow replays against current code: completed steps not re-run;
remaining steps run new code. `builder_runs.code_version` records git HEAD at
launch so the change is visible in `trace`; operator may opt into
`--restart-run` (explicit, non-default).

## Determinism tests

`test_workflow_id_determinism.py` (pure): id derivation stable across
processes; `test_builder_resume.py` / `test_builder_idempotent_rerun.py`
(@needs_bridge): kill mid-step → completed steps skipped, final state ==
uninterrupted.
