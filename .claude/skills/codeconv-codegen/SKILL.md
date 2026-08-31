---
name: codeconv-codegen
description: Drive deterministic Dart→C#/.NET code generation — the build-gated, escalate-don't-guess production stage. Use when the user types `/codeconv-codegen` or asks to generate C# for ready files, advance the codegen frontier, ingest/build-gate a produced .cs, retry a stale/failed file, run the human-review/promotion gate, or aggregate codegen escalations.
argument-hint: "[status|next|ingest|retry|record-review|promote-batch|aggregate-escalations] [flags]"
compatibility: "Claude Code (Agent tool required for the codegen sub-agent loop)"
---

# /codeconv-codegen

Wrapper over `codeconv codegen` for **all deterministic state**
(codegen-readiness, batch selection, the two-phase `dart_codegen`
lifecycle, the `dotnet build` gate, escalation aggregation, the
promotion gate) — the Python CLI is the single source of truth for state
and the skill forwards arguments verbatim for every state operation.

The skill **additionally** carries the codegen sub-agent + human-review
orchestration loop (spawning the ≤7 codegen sub-agents via the Claude
Code **Agent tool** and requesting human review), because spawning
Claude sub-agents and asking the human for review are harness
capabilities the Python CLI structurally cannot perform (017/018
precedent; contract `agent_orchestration.md`). The skill contains **no
deterministic-state logic** — every state mutation goes through
`codeconv codegen`.

## What this skill does

1. Resolve the codeconv venv: `codeconv/.venv/Scripts/python.exe` on
   Windows, `codeconv/.venv/bin/python` on POSIX. If absent, instruct
   Gabi to run `python -m venv codeconv/.venv &&
   codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]`.
2. Run from the repo root. Every invocation SHOULD pass
   `--data-dir D:/bstdev/research/glp/glpnet/.pgdb` for consistency and to reuse
   the already-running shared bridge (CLAUDE.md 🔴 convention; D: is
   NTFS so this is no longer a filesystem necessity).
3. For a bare `/codeconv-codegen` (or an explicit subcommand): run
   `codeconv codegen <args verbatim>` and show stdout/stderr.
4. For the **orchestration** flow (bare `/codeconv-codegen` meaning
   "generate everything ready"): run the loop in § "Orchestration loop".

## Subcommands and flags

`/codeconv-codegen [subcommand] [flags]`

| Subcommand | Purpose |
|---|---|
| `status` (default) | Readiness + lifecycle counts (`not_started`/`codegen_ready`/`in_progress`/`built`/`converted`/`escalated`), `promoted_total`, `open_escalations_total`, stale list, and whether an optimized prompt is in use (warns if baseline). No agents, no writes. |
| `next [--limit 7]` | Emit the next codegen-ready batch as JSON (the loop consumes this). Read-only. Deps must be codegen-complete; an SCC is one unit. |
| `ingest <path> [--respec]` | Validate the produced `.cs` is real C#, run the build (Inc-2: test) gate, two-phase `dart_codegen` write. Returns `built｜needs_agent_work｜escalated`. `--respec` re-opens on source-sha drift. |
| `record-review <batch_id> --file <p> --score <1-5> [--note <text>]` | Record one sampled human review (US3). |
| `promote-batch <batch_id>` | Apply the promotion gate (100% build + human median ≥4/5); set `promoted` on pass, else list blockers (US3). |
| `retry <path>` | Re-open a file (clear build/escalation/terminal state) for re-generation (stale or failed). |
| `aggregate-escalations [--report-out p]` | Walk `.codeconv/conversion-code/`; write `_escalations-report.md` (FR-009). |

| Flag | Applies to | Default | Effect |
|---|---|---|---|
| `--limit <n>` | next | 7 | Soft cap on files (SCC units NEVER split; the loop also throttles to ≤7 concurrent Agent calls). |
| `--respec` | ingest | off | Re-open a completed row on source-sha drift (FR-008/FR-019). |
| `--report-out <p>` | aggregate-escalations | `.codeconv/conversion-code/_escalations-report.md` | Override the report path. |
| `--json-out <p>` | next | stdout | Override the JSON destination. |
| `--dry-run` | next / aggregate | off | Compute everything; write NOTHING and spawn NO agents. |
| `--no-tombstone-update` | ingest / retry | off | Skip the tombstone YAML write (testing only). |
| `--quiet` / `--json` | all | off | Suppress logging / emit a JSON summary. |
| `--data-dir <path>` | all (top-level) | `<repo>/.pgdb` | Use the canonical shared cluster: `--data-dir D:/bstdev/research/glp/glpnet/.pgdb`. |

## Pre-execution checks

- The unified bridge daemon must be reachable (`codeconv codegen` calls
  `acquire_or_discover`, auto-spawning it; ~7 s PGLite cold-init on the
  first call).
- Migrations must have run at least once (`/codeconv-runner migrate` —
  applies Alembic `0007_codegen`).
- A populated `codeconv.dart_depgraph` (run `/codeconv-discover` →
  `/codeconv-depgraph`) AND ratified per-file convspecs + plans (run
  `/codeconv-convspec` / `/codeconv-planagents` or `/codeconv-builder`)
  are required. Empty/absent depgraph ⇒ exit 2 with `"No depgraph. Run
  /codeconv-depgraph first."` (unconditionally, including under `--json`).
- **R11 git policy (RESOLVED 2026-05-23):** `out/csharp/` IS committed
  (reviewable, build-gated product, parallel to checked-in
  convspecs/plans). The optimizer/DBOS-state dirs stay gitignored.

## Orchestration loop (FR-002/FR-005/FR-007/FR-009; contract `agent_orchestration.md`)

Bare `/codeconv-codegen` (no subcommand) resolves the venv/repo-root,
then runs:

```
loop:
  b := codeconv codegen next --limit 7 --json --data-dir D:/bstdev/research/glp/glpnet/.pgdb
  if b is exit 2 (depgraph empty): surface the error verbatim; STOP
  if b.batch is empty: report b.message ("nothing to generate"); break
  for each file f in b.batch, keeping AT MOST 7 codegen Agent calls in flight
      (an SCC — same cycle_group_id — is one coordinated batch: spawn one
       agent per member, pass each its f.scc_siblings; do NOT promote any
       member until ALL members build):
      codeconv codegen ingest f.path --batch-id <batch> --data-dir D:/bstdev/research/glp/glpnet/.pgdb
        # --batch-id tags EVERY file of this `next` batch (built or not) so
        # the promotion gate later sees all members, not just the reviewed
        # sample. Use one stable id per `next` batch.
      if outcome == needs_agent_work:
         spawn ONE codegen sub-agent for f (Agent tool; prompt = the
           optimized prompt from `prompt.load()` + § "Codegen sub-agent
           prompt contract"; pass f.path, the real <rel>.dart source,
           f.spec, f.plan, the public C# surfaces of f's already-generated
           deps from out/csharp/, the relevant conversion idioms, and
           f.scc_siblings)
         codeconv codegen ingest f.path --data-dir D:/bstdev/research/glp/glpnet/.pgdb
           # re-drive: the .cs now exists → real-C# validate → build gate
         if STILL needs_agent_work with build_status=fail:
            return the parsed build_errors to the SAME sub-agent for ONE
            bounded repair attempt; re-ingest once more.
            persistent failure ⇒ the agent writes a structured escalation
            artifact at .codeconv/conversion-code/<rel>.dart.md (NEVER a
            guessed/forced .cs); re-ingest → escalated.
      # outcome == built  → ready for the review gate below
      # outcome == escalated → conversion-blocked; leave for the engineer
  # --- human-review + promotion gate (US3 / FR-006) ---
  for each promotion batch <batch_id> whose files all built:
     sample := max(3, ceil(20% of the batch))
     request human review on each sampled file: a 1–5 score + free-text;
       record via: codeconv codegen record-review <batch_id> --file <p>
         --score <1-5> --note "<verbatim free-text>"
     codeconv codegen promote-batch <batch_id> --data-dir D:/bstdev/research/glp/glpnet/.pgdb
       # gate: 100% build AND human median ≥ 4/5 ⇒ promoted; else blockers
       # on fail: retry/escalate the blocking files; the free-text notes are
       # carried to the OFFLINE optimizer's dataset (never silently rewrite
       # production code with them)
codeconv codegen aggregate-escalations --data-dir D:/bstdev/research/glp/glpnet/.pgdb
```

Concurrency-cap is **dual**: (1) `next --limit 7` never returns an
already-in-progress file (a resumed loop cannot double-spawn an in-flight
file); (2) the skill runs at most 7 codegen Agent calls concurrently and
only issues the next `next` when a slot frees. An SCC unit taken whole
may transiently exceed the soft `--limit` count — the skill still
throttles actual concurrent Agent calls to ≤7.

## Codegen sub-agent prompt contract (FR-007; contract `agent_orchestration.md`)

Each codegen sub-agent is spawned with **exactly one file** (an SCC =
one coordinated batch of sub-agents with shared sibling cross-refs). The
prompt MUST supply, and the agent MUST honour, all of:

1. **Inputs**: the real source `<rel>.dart` (read it — do not rely only
   on scraped metadata); the ratified convspec
   `.codeconv/conversion-specs/<rel>.dart.md`; the ratified plan
   `.codeconv/conversion-plans/<rel>.dart.md`; the **public C# surfaces
   of already-generated dependencies** under `out/csharp/`; the relevant
   `conversion_idioms` rows; and the **GEPA-optimized prompt** from
   `prompt.load()` (baseline if none).
2. **Must produce**: the real, compilable C#/.NET 10 at
   `out/csharp/<target>.cs` (per the plan's `target_code_unit` /
   conversion-units). The agent does NOT write `dart_codegen` or
   tombstones (the Python CLI does, via `ingest`).
3. **Emit REAL C# (hard, the INVERSE of the convspec spec-only rule)**:
   a single raw `.cs` source file. NO prose, NO markdown fences, NO
   leftover Dart, NO empty stub. `ingest` rejects any of these as
   `needs_agent_work` (and the build gate is the ultimate validator).
4. **Honor recorded conventions + idioms verbatim**: keep `*Error` type
   names; apply `getX` → `LookupX`; use dependency APIs **exactly as
   generated** — never invent a signature.
5. **Escalate-don't-guess (FR-007 / DISCIPLINE §1.2/§1.10)**: a
   construct whose faithful C# cannot be derived from plan + convspec +
   idioms, or a build error not resolvable within the plan, ⇒ a
   structured escalation written to
   `.codeconv/conversion-code/<rel>.dart.md` (`### E<n>` entries with the
   five bullets `Kind` (`undecidable｜build_unrecoverable｜dependency_missing`),
   `File(s)`, `Detail`, `Needs`, `Status: open`) — NEVER a guessed
   translation or a silently-accepted non-compiling file.
6. **SCC awareness (FR-002)**: if `scc_siblings` is non-empty, generate
   with consistent cross-references; no member is finished until all
   members build (the readiness gate keeps downstream blocked otherwise).

Output: a single real `.cs` (or, when blocked, an escalation artifact).
`codeconv codegen` will NOT judge code quality beyond the structural
real-C# gate + the `dotnet build` (Inc-2: `dotnet test`) gate + the
sampled human-review gate.

## Build-feedback loop (contract `agent_orchestration.md` § Build-feedback)

A `build_status=fail` file is returned to the codegen sub-agent with the
parsed compiler errors for **one** bounded repair attempt; persistent
failure ⇒ a `build_unrecoverable` escalation (no infinite retry, no
silent accept).
