# Quickstart — codeconv-codegen (Flow C: generate → gate → review → promote)

Prereqs: bridge up at `--data-dir C:/pglite/research/glpnet`; migrations through `0007`; ratified plans+convspecs+idiom KB (all 0-escalation); `out/csharp/` scaffold present; `dotnet` on PATH.

## 0. (Once / periodically) Optimize the codegen prompt — OFFLINE
```
export OPENAI_API_KEY=...                       # read ONLY by codegen_opt
codeconv codegen-opt optimize --budget 200 --eval-size 12
codeconv codegen-opt export-prompt              # → .codeconv/codegen-prompt/optimized.md
codeconv codegen-opt show                       # provenance + metric score
```

## 1. Generate the production tree (Increment 1) — durable, deterministic
```
/codeconv-codegen                               # skill loop: next → (sub-agent emits .cs) → ingest(build gate) per batch
# or one batch at a time:
codeconv codegen next --limit 7 --json
#   spawn codegen sub-agent per file (uses optimized prompt + plan + convspec + dep interfaces + idioms)
codeconv codegen ingest lib/runtime/terms.dart  # validates real C# + dotnet build gate → built|needs_agent_work|escalated
```

## 2. Human-review gate + promote
```
codeconv codegen record-review <batch_id> --file lib/runtime/terms.dart --score 5 --note "faithful"
codeconv codegen promote-batch <batch_id>       # 100% build + median≥4/5 ⇒ promoted; else blockers
```

## 3. Status / escalations
```
codeconv codegen status                         # counts: built / converted / escalated / stale
codeconv codegen aggregate-escalations          # → .codeconv/conversion-code/_escalations-report.md
```

## 4. Increment 2 — test files + ported-test metric
After lib/ converts, generate the test tree; the metric gains `0.6·test_pass_rate`:
```
codeconv codegen next --limit 7   # now offers test/ files; ingest runs dotnet test too
```

## 5. Durable builder integration
`codeconv builder run` drives `… → plan → codegen`; a missing `.cs` surfaces `needs_agent_work`; `/codeconv-codegen` handles it and re-drives. Kill/resume skips completed files (R12).

## Reverse / retry
```
codeconv codegen retry lib/runtime/terms.dart   # re-open a stale/failed file for regeneration
```
