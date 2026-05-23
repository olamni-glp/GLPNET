# Contract — composite feedback metric + GEPA wiring

## The metric (FR-006, Clarification Q2)
Per file:
- **Build gate (hard)**: `dotnet build` fail ⇒ score = **0.0** (floor). No partial credit for non-compiling output.
- **Compiling candidates**: `score = 0.6 · test_pass_rate + 0.4 · norm(human_review)` where `norm(1..5) = (s-1)/4`.
  - **Increment 1** (no tests in scope): `score = norm(human_review)` (the `0.6·tests` term omitted).
  - **Increment 2** (tests converted): full `0.6/0.4` weighting; `test_pass_rate ∈ [0,1]` from `dotnet test`.
- **Batch promotion gate**: a batch is `promoted` ⇔ 100% of its files build AND median human_review ≥ 4/5. Else: re-generate (retry) or escalate.

`tools/codegen/` computes the *production* gate; `codegen_opt/metric.py` computes the *same* metric for GEPA (build via `buildgate.py`; human term from recorded reviews on the eval set, or omitted when optimizing pre-review).

## GEPA wiring (`codegen_opt/optimize.py`)
- Dataset: `dataset.py` builds a held-out eval split over (plan, convspec, dep-interfaces, expected-construct) tuples.
- GEPA optimizes `program.py`'s instructions to maximize the mean metric on the eval set, using free-text human-review notes as reflective signal where available.
- **Budget cap (HARD, SC-006)**: `--budget` = max metric-calls (each call may run a `dotnet build`); GEPA stops at the cap and returns best-so-far. A capped run still yields a usable instruction set.
- Output: `export-prompt` serializes best instructions + `metric_score` provenance.

## Acceptance (SC-003)
Post-optimization instructions MUST score ≥ baseline on the held-out eval set; `test_codegen_opt_metric_mocked.py` asserts improvement with a MOCKED LM + fixture metric (no real LM/GEPA in CI).
