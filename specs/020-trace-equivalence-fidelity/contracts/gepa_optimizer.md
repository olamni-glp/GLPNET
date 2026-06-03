# Contract — Real `dspy.GEPA` per-subsystem optimizer (FR-010, FR-011, FR-012)

`tools/codegen_opt/` — MODIFIED from 019's hand-rolled `dspy.Predict` reflective loop to real `dspy.GEPA`. **OFFLINE-only, non-durable, NOT auto-registered** as a durable step.

**🔴 LM = Claude, in-session, NO external API.** GEPA's generation and reflective instruction-proposal run as **Claude sub-agents** (the Agent tool, driven by the `/codeconv-codegen-opt` skill loop) — exactly how `/codeconv-codegen` already produces every `.cs`. There is **NO `OPENAI_API_KEY`, NO litellm, NO openai** anywhere on this path. `dspy.GEPA` is model-agnostic; "real GEPA" never required OpenAI. (The prior contract text mandating an OpenAI/litellm API + `OPENAI_API_KEY` was a defect and is **deleted**, not a constraint — ruled 2026-06-03.) `codeconv_opt` imports `dspy`/`gepa` (the signature/program + GEPA algorithm scaffold) only; the `generate_fn`/`propose_fn` seams are injected with Claude-backed callables and have no API default.

## Module + metric (FR-010)
- `program.py`: a `dspy.Module` codegen signature `(plan, convspec, dep_interfaces, idiom_kb, subsystem) → C# source`. Same shape as 019; gains `subsystem` for prompt selection.
- `metric.py`: GEPA metric callable returning `dspy.Prediction(score=<float>, feedback=<str>)`:
  - `score` = `tools/equiv/fidelity.py:score(...)` — IDENTICAL to the production gate (SC-004).
  - `feedback` = the textual divergence: `dotnet build` error, or the failing back-test assertion, or the `DivergenceRecord` from `tools/equiv/relation.py` rendered as text. This is GEPA's reflective signal (the reason GEPA, not a scalar optimizer, is used).

## Per-subsystem prompts on a shared base (FR-011)
- `_base.md` (shared optimized base) seeds every subsystem; each subsystem's optimized prompt descends from it and is checked in at `.codeconv/codegen-prompt/<subsystem>.md`.
- **Carry-forward**: subsystem `k+1`'s GEPA run is seeded from subsystem `k`'s optimized prompt (the curriculum transfer, decision 6/8). The shared base accumulates the transferable GLP→C# idioms.
- Production selection: `tools/codegen/prompt.py:load(subsystem)` reads the checked-in per-subsystem artifact — **no LM/dspy/network** (asserted by `test_codegen_prod_no_lm_import`).

## Dataset split (FR / SC-003, R9)
- `dataset.py` reads `.codeconv/equiv-manifest/subsystems.yml`: per-subsystem sources split train(~70%)/held-out(~30%), deterministic.
- GEPA generates + reflects on **train**; SC-003 improvement measured on **held-out** (fixed, auditable — no run-to-run wobble).

## Budget cap (SC-006, FR-012)
`--budget` = hard cap on metric-calls/rollouts (each may run `dotnet build` + a REPL trace capture). On cap, GEPA returns **best-so-far** per-subsystem prompt. A capped run still yields a usable artifact. Mid-curriculum exhaustion: later subsystems run on the last frozen base prompt (spec edge case).

## Co-evolution loop (FR-015) — per subsystem
```
optimize-before-generate (on currently-available signal:
    build + back-test before a runnable C# runtime; trace-equivalence once runnable)
  → generate (codegen sub-agents, /codeconv-codegen, 019)
  → run available equivalence gate (tools/equiv)
  → reflect DivergenceRecords into GEPA
  → regenerate weak files
  → freeze subsystem prompt
  → carry base forward to next subsystem
```

## CLI (`codeconv codegen-opt`, extended)
`optimize --subsystem <s> --budget <n>` | `eval --subsystem <s>` (held-out score) | `export-prompt --subsystem <s>` | `show` (provenance of all checked-in prompts).

## Tests (mocked LM — no real GEPA/LM in CI)
- `test_codegen_opt_gepa_mocked`: with a MOCKED LM + fixture metric, optimized prompt scores ≥ baseline on the held-out split (SC-003); budget cap halts with best-so-far (SC-006).
- `test_no_lm_on_production_path`: `tools/equiv/`, `tools/codegen/`, `durable/` import no dspy/litellm/openai (SC-008).
