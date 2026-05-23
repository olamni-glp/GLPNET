# Contract — `codeconv codegen` CLI (production, deterministic)

Auto-discovered tool (012 FR-006). Owns ALL deterministic codegen state. No LM/network. Bare `codeconv codegen` = `status`.

| Subcommand | Purpose | Writes |
|---|---|---|
| `status` (default) | codegen-readiness counts (`not_started｜built｜converted｜escalated｜stale`) + `promoted`/escalation totals. Read-only. | — |
| `next [--limit 7]` | Next codegen-ready batch as JSON (deps codegen-complete; SCC=one unit). Read-only, deterministic. | — |
| `ingest <path> [--respec] [--increment 1\|2] [--batch-id <id>]` | Deterministically validate the produced `.cs`, run the build (and test, Inc-2) gate, two-phase `dart_codegen` write. Returns `built｜needs_agent_work｜escalated`. `--respec` re-opens on sha drift (FR-019/FR-008). `--batch-id` tags the file into a promotion batch **at ingest** so the promotion gate sees EVERY batch member (built + unbuilt), not only the reviewed sample. | `dart_codegen` |
| `record-review <batch_id> --file <p> --score <1-5> [--note <text>]` | Record a sampled human review. | `dart_codegen` |
| `promote-batch <batch_id>` | Apply the promotion gate (100% build + human median ≥4/5); set `promoted` on pass, else report blockers. | `dart_codegen` |
| `aggregate-escalations [--report-out p]` | Write `.codeconv/conversion-code/_escalations-report.md` (FR-009). | report file |
| `retry <path>` | Re-open a file (clear build/escalation state) for re-generation (stale or failed). | `dart_codegen` |

**Flags**: `--data-dir` (mandatory `C:/pglite/research/glpnet`), `--json`, `--dry-run` (compute, write nothing, spawn nothing), `--limit`.
**Exit codes**: `0` ok; `2` no depgraph / nothing-to-do; escalations are reported (not a nonzero exit) but mark files conversion-blocked.
**Idempotence**: re-run on unchanged source + state generates nothing; no duplicate rows; no `.cs` diff except deterministic regeneration is gated by `--retry`.

## Skill loop (`/codeconv-codegen`)
```
loop:
  b := codeconv codegen next --limit 7 --json
  if b empty: report "nothing to generate"; break
  for each file f in b (≤7 concurrent codegen Agent calls; SCC batched whole):
     codeconv codegen ingest f            # build gate on the (maybe absent) .cs
     if needs_agent_work:
        spawn ONE codegen sub-agent for f (prompt = optimized prompt + plan+convspec+dep-interfaces+idioms)
        codeconv codegen ingest f          # re-drive: .cs now present → build gate
  sample := max(3, 20% of batch); request human review on sample (record-review)
  codeconv codegen promote-batch <batch_id>   # gate; on fail, retry/escalate
codeconv codegen aggregate-escalations
```
The skill carries the agent + human-review orchestration (justified deviation); the Python CLI is the single source of deterministic state.
