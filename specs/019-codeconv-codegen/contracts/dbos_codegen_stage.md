# Contract — DBOS `codegen` step (durable, replay-safe)

> **Amendment (decision B, 2026-05-23, Gabi-approved).** codegen is a
> **separately-driven** durable phase, **not** auto-chained into the
> builder's default per-unit sequence. The `codegen` DBOS step IS
> registered (`codeconv.durable.steps.step_codegen` — replay-safe,
> available for explicit/future chaining), but is intentionally absent
> from `durable.workflows.PER_UNIT_STAGES`/`POST_STAGES` so a `builder
> run` keeps its clean "completed-at-plan" semantics. Codegen is driven
> by `/codeconv-codegen` (`codeconv codegen ingest`), which calls the
> SAME deterministic, replay-safe `run_codegen_step` core. Rationale:
> codegen is a heavy generative + build + human-review step, distinct
> from the lightweight analysis/plan pipeline; auto-chaining it would
> make every `builder run` report `needs_agent_work` (no `.cs` yet) and
> conflate two qualitatively different phases. The original "stage AFTER
> plan in the child workflow" wording below is superseded by this
> amendment; the *step body, invariants, and ordering of readiness*
> below remain authoritative.

The deterministic, replay-safe step body below is driven per-file by the
codegen tool / `/codeconv-codegen`; codegen-readiness still follows
`plan` in dependency terms (a file is codegen-ready only once its deps
are codegen-complete).

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
