# Contract — DBOS Workflow/Step Model (D1=a; FR-002/003/004; D2 principle)

## Taxonomy (Amendment 2 / FR-044 — agent-gate split)

> **Amendment 2 (2026-05-19, facet-3 remediation).** The original single
> per-unit *child* workflow was infeasible across the agent gate: a
> `@DBOS.workflow` that *returns* on the `needs_agent_work` sentinel is
> terminally `SUCCESS`, so its checkpointed `convspec` step never re-runs
> — an agent-written spec could never be ingested (the genuine live pass
> proved this). The per-unit work is split into two workflows around the
> out-of-DBOS agent gate.

- **Outer workflow** `@DBOS.workflow` id `builder:{workspace_id}:{run_epoch}`
  — walks feature-015 `dart_depgraph` order (read-only); per unit drives
  **PRE → artifact-gate → POST**. **Run-epoch rule:** a plain `builder
  run` *reuses* the most-recent epoch (resume, not restart — R9/FR-004/
  SC-002) **except** when the most-recent run ended `needs_agent_work`,
  which is an *awaiting-agent* terminal state, not a crash: a new epoch
  is then minted so the artifact gate is re-evaluated on a plain re-drive
  (no `--restart-run` — the skill loop never passes it). `--restart-run`
  always mints a new epoch (R13).
- **PRE-agent workflow** `@DBOS.workflow` id `file:pre:{h(rel)}` /
  `scc:pre:{h(sorted members)}` — stages `scaffold` + `convspec`. Writes
  `convspec_started_at` (idempotent); with no artifact, `convspec`
  returns the deterministic `needs_agent_work` sentinel **recorded in
  the wf result** (the outer aggregation surfaces it — the wf still
  completes terminally & idempotently; deterministic id ⇒ recovered on
  resume, R9/FR-003/FR-004/SC-002).
- **POST-agent workflow** `@DBOS.workflow` id
  `file:post:{h(rel)}:{art_digest}` /
  `scc:post:{h(members)}:{art_digest}` — **content-addressed** by the
  agent-written artifact (`art_digest` = stable SHA-256/16-hex over the
  artifact text; for an SCC, over the sorted members' per-artifact
  digests). The outer launches POST **only when the artifact(s) exist**
  (an SCC requires *all* members' artifacts — FR-002). Stages `convspec`
  (re-invoked verbatim → now finds the artifact → `specced`) + `plan`.
  Because the id is keyed to the artifact, a re-spec ⇒ a *new* id ⇒ a
  fresh deterministic ingest, while resume within one artifact recovers
  identically (SC-002).
- **Stage step** `@DBOS.step`, one per stage
  (`discover｜depgraph-gate｜scaffold｜convspec｜plan｜…`). **Step body calls
  the existing tool entrypoint verbatim** (R1/R8, D2) and writes the same
  two-phase + tombstone projection the tool already writes — *unchanged*
  by Amendment 2 (only `durable/workflows.py` + the id helpers in
  `durable/__init__.py` change; `durable/steps.py` is reused verbatim).

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
PRE-agent **workflow** records `needs_agent_work` in its result and
completes; the **outer** workflow aggregates it (the convspec
short-circuit is otherwise invisible — the facet-1 defect: `builder run`
wrongly reported `completed` with zero specs persisted, violating US2
Acceptance 1) and the run ends in the durable **awaiting-agent** status
`outcome = 'needs_agent_work'` (`builder_runs`, migration 0006 widens
the CHECK to allow it), surfaced in `builder run --json`
(`{"outcome":"needs_agent_work","needs_agent_work":[…paths…]}`) — not a
Python exception. The **skill** detects it from `builder run --json`,
spawns the analysis sub-agent (+ separate research sub-agent only on a
KB miss), waits for the written artifact, then re-drives **plain**
`builder run` (Amendment 2 / FR-044 — *no* `--restart-run`): the
awaiting-agent epoch rule mints a fresh outer epoch, the deterministic
PRE wf is *recovered* (its steps skipped — FR-003/FR-004), the artifact
gate now passes, and a **fresh content-addressed POST wf**
(`…:post:…:{art_digest}`) runs `convspec` (finds the artifact →
`specced`) + `plan` to completion. (The pre-Amendment-2 text — "DBOS
recovers the same child workflow id and the step now finds the artifact
and completes" — was infeasible and is void: a returned child is
terminal and its checkpointed step never re-runs.)

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
