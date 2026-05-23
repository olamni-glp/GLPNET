# Contract — DBOS `codegen` stage (durable, replay-safe)

Adds a `codegen` stage AFTER `plan` in the 018 per-file/per-SCC child workflow.

## Step body (deterministic, replay-safe — R3)
`run_codegen_step(repo_root, data_dir, rel_path, respec=False)`:
1. Phase-1: `INSERT … dart_codegen (path, codegen_started_at, sha) ON CONFLICT DO NOTHING`; `--respec` re-opens a completed row on sha drift.
2. Locate the produced `.cs` at `out/csharp/<target>`. **Absent ⇒ return `{"needs_agent_work": True, "path": rel}`** (typed sentinel, NEVER raise).
3. Present ⇒ validate it is real C# (artefact.py) → run `dotnet build` (Inc-2: `dotnet test`) via `buildgate.py` → record `build_status`/`test_pass_rate`/escalations.
4. Phase-2 (terminal, only on build pass): set `codegen_completed_at`, `target_cs_path`, `open_escalation_count`.
5. Return `built` | `escalated` (escalations present) — never raises on the normal paths.

## Invariants
- **No LM/network in the step** — the codegen sub-agent runs in the skill layer; the step only ingests the checked-in `.cs`. Replay re-reads the artifact (same result), never re-calls a model.
- **SCC = one indivisible unit**: all members generated + gated together; none promoted in isolation.
- **Deterministic IDs**: `codegen:{sha-stable-rel-path}` child step; re-drive recovers in-flight, doesn't restart.
- **needs_agent_work surfacing**: the child workflow returns the sentinel up so `builder run` exit + the skill detect it (not a caught exception), spawn the codegen sub-agent, re-drive. (018 Amendment-2 PRE/POST split applies if the agent gate sits between deterministic halves.)
- Build failure is NOT an exception — it is a recorded `build_status=fail` feedback fact; the file is retried/optimized or escalated.

## Placement
`discover → depgraph → scaffold → convspec → plan → codegen`. `codegen`-readiness gates on all cross-SCC deps being `codegen`-complete (mirrors plan-readiness).
