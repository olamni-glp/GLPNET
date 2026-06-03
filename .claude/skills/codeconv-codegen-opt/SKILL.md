---
name: codeconv-codegen-opt
description: Run the OFFLINE GEPA/DSPy optimizer for the codegen prompt — the only LM-backed, non-durable codeconv tool. Use when the user types `/codeconv-codegen-opt` or asks to optimize/evaluate the codegen instructions, export the optimized prompt to production, or show the current optimized-prompt provenance.
argument-hint: "[optimize|eval|export-prompt|show|dataset|score] [--subsystem S] [--budget N] [--seed-prompt P]"
compatibility: "Claude Code (Agent tool drives the LM in-session); requires the codeconv[opt] extra (dspy). NO external API / NO OPENAI_API_KEY."
---

# /codeconv-codegen-opt

Thin wrapper over `codeconv codegen_opt` — the **offline, non-durable**
GEPA/DSPy optimizer that improves the codegen sub-agent's instructions
against the composite build/test/human metric on a held-out eval set,
then exports the result as the production optimized-prompt artifact
`.codeconv/codegen-prompt/optimized.md`.

**🔴 LM = Claude, in-session, NO external API.** GEPA's generation and
reflective instruction-proposal run as **Claude sub-agents** (the Agent
tool) driven by this skill's loop — never litellm/OpenAI, never
`OPENAI_API_KEY`. `dspy.GEPA` is model-agnostic; the Python optimizer
provides the deterministic scaffold (dataset split, the `dotnet build`
gate, prompt serialization) and the `generate_fn`/`propose_fn` seams are
injected with Claude-backed callables (no API default). It is **never** a
DBOS/durable stage and is never imported by the deterministic production
codegen path (R3/R10). Its sole output into production is the
optimized-prompt artifact, which `/codeconv-codegen` loads via
`prompt.load()`.

> **CLI note:** the subcommand is `codeconv codegen_opt` (underscore) —
> Python package directories cannot contain `-`. The contract's
> `codeconv codegen-opt` is the conceptual name.

## What this skill does

1. Resolve the codeconv venv with the optimizer extra installed:
   `python -m pip install -e 'codeconv[opt]'` (dspy/gepa — the LM is
   Claude sub-agents; no openai/litellm needed).
2. **No API key.** The LM is Claude sub-agents (this skill's loop);
   nothing reads `OPENAI_API_KEY`. A bare `codeconv codegen_opt optimize`
   with no injected Claude backend exits 2 with an actionable message
   pointing back to this skill loop — NEVER an OpenAI fallback.
3. Run `codeconv codegen_opt <args verbatim>` from the repo root and show
   stdout/stderr.

## Subcommands and flags

`/codeconv-codegen-opt [subcommand] [flags]`

| Subcommand | Purpose |
|---|---|
| `optimize [--subsystem S] [--seed-prompt P] [--budget N] [--model M] [--eval-size K] [--seed S] [--increment 1\|2]` | Run budget-capped GEPA. With `--subsystem`, uses that subsystem's per-subsystem dataset + carry-forward seed; serializes the best-so-far to the per-subsystem scratch candidate (`.candidate-<S>.md`, gitignored). **A bare CLI `optimize` exits 2** (no injected Claude backend) — the real run is the orchestration loop below. |
| `dataset --subsystem S [--held-out-frac F]` | Print the subsystem's deterministic train/held-out split (rel_paths + expected units). Read-only, LM-free. The loop consumes this. |
| `score --file C.cs [--expected-units a,b] [--dep D.cs ...] [--increment 1\|2]` | Build-gate score ONE candidate `.cs` (the Python scorer the loop calls): materialize a throwaway net10.0 classlib (+ any `--dep` sources for in-context compile), run the SAME `dotnet build` gate as production, print `{score, build_status, feedback}`. Non-compiling ⇒ 0.0. Deterministic, OFFLINE, LM-free. |
| `eval [--subsystem S] [--prompt P]` | Score a given (or the production per-subsystem) instruction set on the held-out set; print the composite metric. |
| `export-prompt [--subsystem S] [--out P] [--from C] [--instructions-file F --score N --budget N --budget-used N --dataset-hash H]` | Promote to the PRODUCTION artifact (`<S>.md` with `--subsystem`, else `optimized.md`) — the single writer. `--instructions-file` promotes the skill-authored best prompt with provenance from the flags. |
| `show [--subsystem S]` (default) | Print the production artifact's provenance — or that it's absent (production uses baseline / `_base.md`). |

| Flag | Default | Effect |
|---|---|---|
| `--subsystem <S>` | (none) | `heap｜bytecode｜compiler｜runtime-core｜multiagent`. Selects the per-subsystem dataset, seed, scratch candidate, and prompt artifact. Omit for the legacy global prompt. |
| `--seed-prompt <P>` | baseline | Carry-forward seed instructions file (`_base.md` or the prior subsystem's `<k>.md`) — the curriculum transfer. |
| `--budget <N>` | 20 | **HARD** max metric-calls (each may run a `dotnet build`). The loop stops at the cap and keeps best-so-far (SC-006). |
| `--model <id>` | `claude-in-session` | Provenance label only — the LM is Claude sub-agents (the Agent tool), not an external API. |
| `--eval-size <K>` | 10 | Held-out eval split size (global runs only; per-subsystem uses `--held-out-frac`). |
| `--held-out-frac <F>` | 0.30 | Per-subsystem held-out fraction (deterministic sha256(path) scheme). |
| `--seed <S>` | 0 | Deterministic train/eval split seed (global runs only). |
| `--increment <1\|2>` | 1 | 1 = no ported tests (build signal); 2 = `dotnet test` weighting (0.6 tests / 0.4 human). |
| `--json` | off | Emit a JSON summary. |

## The metric (contract `metric_contract.md`)

- **Build gate (hard):** a non-compiling candidate scores **0.0** — no
  partial credit.
- **Compiling:** `0.6·test_pass_rate + 0.4·norm(human)`, `norm(1..5) =
  (s-1)/4`. Increment 1 omits the tests term; **pre-review** (no recorded
  human score) the build gate is the sole signal (compiling = 1.0).
- The SAME metric (via `buildgate.py`) drives the production promotion
  gate, so the optimizer and production agree by construction.

## Per-subsystem GEPA orchestration loop (T035 / FR-011 / FR-015)

This is the real GEPA run. The Python tool is the deterministic scaffold
(dataset split, the `dotnet build` scorer, prompt serialization); **Claude
sub-agents (the Agent tool) ARE the LM** — the generator and the reflector.
There is no in-process Python LM and no external API: a bare `codeconv
codegen_opt optimize` exits 2 by design. The skill drives the loop turn by
turn, in dependency/curriculum order: `heap → bytecode → compiler →
runtime-core → multiagent` (multiagent LAST, and gated on T039).

For each subsystem `S` (in curriculum order):

```
# 1. seed: the prior subsystem's frozen prompt, else _base.md (carry-forward).
seed := .codeconv/codegen-prompt/<prev>.md  (or _base.md for the first)

# 2. dataset: the deterministic per-subsystem split.
d := codeconv codegen_opt dataset --subsystem S --json
    # d.train = what GEPA reflects on; d.held_out = the fixed SC-003 eval set.

instructions := seed.instructions
best := (instructions, score(instructions on d.held_out))   # see step 4
budget_used := 0

# 3. GEPA rounds until the HARD --budget metric-calls are spent:
while budget_used < budget:
    # 3a. GENERATE: for each train example e in d.train (≤7 Agent calls in
    #     flight), spawn ONE generator sub-agent with `instructions` + e's
    #     (real Dart source, convspec, plan, dep .cs surfaces from out/csharp,
    #     idioms). It writes a candidate .cs to a scratch path (NOT out/csharp).
    # 3b. SCORE each candidate via the Python build gate (NOT the LM):
    #         codeconv codegen_opt score --file <cand.cs>
    #             --expected-units <e.units> --dep <e's dep .cs ...> --json
    #     budget_used += (#scored). Stop mid-batch if budget hits the cap.
    # 3c. mean_score := mean(scores); reflections := the failing `feedback`
    #     strings (parsed `dotnet build` errors) from 3b — GEPA's signal.
    # 3d. if mean_score > best.score: best := (instructions, mean_score)
    # 3e. REFLECT: spawn ONE reflector sub-agent with (`instructions`,
    #     reflections) → propose improved `instructions` for the next round.

# 4. score-on-held-out (the SC-003 number): same step-3b scoring over
#    d.held_out under best.instructions (omit huge held-out files — e.g. the
#    runner — to keep the build cheap; log what was skipped, never silently).

# 5. FREEZE: promote best as the durable artifact (single writer):
codeconv codegen_opt export-prompt --subsystem S
    --instructions-file <best.instructions.md>
    --score <held_out_score> --baseline-score <seed_score>
    --budget <budget> --budget-used <budget_used> --dataset-hash <d.hash>
# → .codeconv/codegen-prompt/S.md (checked in, provenance front-matter).
# Record "Last GEPA artifact written: S" in docs/current_plan.md POSITION.
# Carry S.md forward as the seed for the next subsystem.
```

**Generator sub-agent** = a codegen sub-agent (identical contract to
`/codeconv-codegen` § "Codegen sub-agent prompt contract") but prompted with
the **candidate** `instructions` under test (not the production prompt), and it
writes to a SCRATCH `.cs` path the loop scores — it does NOT touch `out/csharp`,
`dart_codegen`, or tombstones (the optimizer is offline/non-durable).

**Reflector sub-agent** = given the current `instructions` + the round's
`reflections` (the parsed build errors / divergences), proposes a single
improved instruction set. It edits ONLY the instruction prose — never the
dataset, never code.

**Build-only metric, for now (decision 1, 2026-06-03)**: GEPA is wired BEFORE
the runnable C# REPL exists, so the scorer is the build gate (`composite_score`
via `score`). Once US2 yields a runnable REPL, the metric swaps to
`tools/equiv/fidelity.py` (T031) with the `DivergenceRecord` as the reflective
feedback — the SAME score as the production gate (SC-004). The loop shape is
unchanged; only the scorer deepens.

**Budget + restart (SC-006)**: `--budget` is a HARD cap on metric-calls (each
`score` is one). On cap, freeze best-so-far. A killed run loses only that run:
the durable resume point is the checked-in `<S>.md` (+ `_base.md`), never
in-memory state. Re-running re-seeds from the last frozen prompt.

## IP / privacy boundary

**No source ever leaves the session.** GEPA's generation + reflection run
as Claude sub-agents in-session (the Agent tool); Dart source + plans are
**not transmitted to any external API** (there is none). Only the
optimized *instructions* (no source) ship in the prompt artifact, loaded
by `/codeconv-codegen` via `prompt.load()`.

## Typical flow (per subsystem; the loop is driven by the skill, not one CLI call)

```
codeconv codegen_opt dataset --subsystem bytecode --json    # train / held-out
# … run the orchestration loop above: generator + reflector sub-agents,
#   scoring each candidate with `codeconv codegen_opt score …` …
codeconv codegen_opt export-prompt --subsystem bytecode \
    --instructions-file <best>.md --score <held_out> --budget 30 --budget-used N
codeconv codegen_opt show --subsystem bytecode              # confirm provenance
# /codeconv-codegen loads the tuned instructions via prompt.load(subsystem).
```

A bare `codeconv codegen_opt optimize` (no injected Claude backend) exits 2
with a message pointing back to this loop — never an external-API fallback.
Failure/timeout/empty ⇒ the prior `<subsystem>.md` is left intact; the
optimizer never writes a degraded prompt silently.
