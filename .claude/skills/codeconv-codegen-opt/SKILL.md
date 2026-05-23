---
name: codeconv-codegen-opt
description: Run the OFFLINE GEPA/DSPy optimizer for the codegen prompt — the only LM-backed, non-durable codeconv tool. Use when the user types `/codeconv-codegen-opt` or asks to optimize/evaluate the codegen instructions, export the optimized prompt to production, or show the current optimized-prompt provenance.
argument-hint: "[optimize|eval|export-prompt|show] [--budget N] [--model M] [--eval-size K]"
compatibility: "Claude Code; requires OPENAI_API_KEY and the codeconv[opt] extra"
---

# /codeconv-codegen-opt

Thin wrapper over `codeconv codegen_opt` — the **offline, non-durable**
GEPA/DSPy optimizer that improves the codegen sub-agent's instructions
against the composite build/test/human metric on a held-out eval set,
then exports the result as the production optimized-prompt artifact
`.codeconv/codegen-prompt/optimized.md`.

This is the **only** codeconv tool that calls an LM (litellm/OpenAI) and
reads `OPENAI_API_KEY`. It is **never** a DBOS/durable stage and is
never imported by the deterministic production codegen path (R3/R10).
Its sole output into production is the optimized-prompt artifact, which
`/codeconv-codegen` loads via `prompt.load()`.

> **CLI note:** the subcommand is `codeconv codegen_opt` (underscore) —
> Python package directories cannot contain `-`. The contract's
> `codeconv codegen-opt` is the conceptual name.

## What this skill does

1. Resolve the codeconv venv with the optimizer extra installed:
   `python -m pip install -e 'codeconv[opt]'` (dspy/gepa/litellm/openai).
2. Ensure `OPENAI_API_KEY` is exported. If absent, `optimize`/`eval`
   exit 2 with an actionable message — NEVER a guessed fallback.
3. Run `codeconv codegen_opt <args verbatim>` from the repo root and show
   stdout/stderr.

## Subcommands and flags

`/codeconv-codegen-opt [subcommand] [flags]`

| Subcommand | Purpose |
|---|---|
| `optimize [--budget N] [--model M] [--eval-size K] [--seed S] [--increment 1\|2]` | Run budget-capped GEPA over the codegen `dspy.Module` against `metric.py` on the held-out set; serialize the best-so-far to the scratch candidate (`.codeconv/codegen-prompt/.candidate.md`, gitignored). |
| `eval [--prompt P]` | Score a given (or the production) instruction set on the eval set; print the composite metric. |
| `export-prompt [--out P] [--from C]` | Promote the scratch candidate to the PRODUCTION artifact `.codeconv/codegen-prompt/optimized.md` (the single writer). |
| `show` (default) | Print the production artifact's provenance (optimizer version, score, dataset hash, model, UTC) — or that it's absent (production uses baseline). |

| Flag | Default | Effect |
|---|---|---|
| `--budget <N>` | 20 | **HARD** max metric-calls (each may run a `dotnet build`). The run stops at the cap and returns the best-so-far — a capped run still yields a usable instruction set (SC-006). |
| `--model <id>` | strongest available OpenAI reasoning model (litellm id) | The LM backing GEPA's generation + reflective proposal. |
| `--eval-size <K>` | 10 | Held-out eval split size (deterministic given `--seed`). |
| `--seed <S>` | 0 | Deterministic train/eval split seed. |
| `--increment <1\|2>` | 1 | 1 = no ported tests (human-review/build signal); 2 = `dotnet test` weighting (0.6 tests / 0.4 human). |
| `--json` | off | Emit a JSON summary. |

## The metric (contract `metric_contract.md`)

- **Build gate (hard):** a non-compiling candidate scores **0.0** — no
  partial credit.
- **Compiling:** `0.6·test_pass_rate + 0.4·norm(human)`, `norm(1..5) =
  (s-1)/4`. Increment 1 omits the tests term; **pre-review** (no recorded
  human score) the build gate is the sole signal (compiling = 1.0).
- The SAME metric (via `buildgate.py`) drives the production promotion
  gate, so the optimizer and production agree by construction.

## IP / privacy boundary (Clarification Q3)

`optimize`/`eval` transmit Dart source + plans to OpenAI — the accepted
**offline-only** tradeoff. The production codegen path does NOT transmit
source anywhere; only the optimized *instructions* (no source) ship in
`optimized.md`. Surface this to the user before a first `optimize` run.

## Typical flow

```
codeconv codegen_opt optimize --budget 30 --eval-size 12   # GEPA → candidate
codeconv codegen_opt eval                                  # sanity-check score
codeconv codegen_opt export-prompt                         # promote to production
codeconv codegen_opt show                                  # confirm provenance
# /codeconv-codegen now loads the optimized instructions via prompt.load()
```

Failure/timeout/empty ⇒ the prior production artifact is left intact;
the optimizer never writes a degraded prompt silently.
