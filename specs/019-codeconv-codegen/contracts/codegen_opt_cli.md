# Contract — `codeconv codegen-opt` CLI (offline DSPy/GEPA optimizer)

**OFFLINE, non-durable, NOT auto-registered as a durable stage.** The ONLY place an LM client (litellm/OpenAI) + API key live. Output into production = the optimized-prompt artifact only.

| Subcommand | Purpose |
|---|---|
| `optimize [--budget N] [--model M] [--eval-size K]` | Run GEPA over the codegen `dspy.Module` against `metric.py` on the held-out set; bounded by the budget/rollout cap; on completion serialize the best instructions. |
| `eval [--prompt P]` | Score a given (or current) instruction set on the eval set; print the composite metric. |
| `export-prompt [--out .codeconv/codegen-prompt/optimized.md]` | Serialize the current best instructions + provenance to the production artifact. |
| `show` | Print the current artifact's provenance (optimizer version, score, dataset hash, UTC). |

**Flags**: `--model` (litellm id; default strongest available OpenAI reasoning model), `--budget` (hard max metric-calls/rollouts; conservative default — SC-006), `--eval-size`, `--seed`.
**Env**: `OPENAI_API_KEY` (read here only). If absent ⇒ exit with an actionable message; never falls back to a guess.
**Invariants**:
- Never imported by `tools/codegen/`, `durable/`, or any DBOS step (replay-safety, R3/R10).
- Budget cap is HARD: a capped run still emits a usable best-so-far artifact (SC-006).
- Failure/timeout/empty ⇒ report + leave the prior artifact intact; never write a degraded prompt silently.
- IP: transmits Dart source/plans to OpenAI — the accepted offline-only tradeoff (Clarification Q3); the production path does not.
