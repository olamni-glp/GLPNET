# Contract: MLIR/GLP-Dialect Round-Trip Validation Spike  (FR-040–043, FR-070–074, R13)

**Artifacts**: `MLIR-GLP-DIALECT.md` (spec) + `docs/research/repl-engine-separation/spikes/mlir/` (runnable spike).

## Provides
An empirical demonstration that the GLP/FCP MLIR dialect realizes the four primitives and round-trips a minimal
GLP IL fragment under `decode(encode(p)) ≡ p` against **real MLIR**.

## Acceptance (must all hold)
1. **Spec (FR-040–042)**: the four primitives `HEAD-unify`, `GUARD-test`, `BODY-spawn`, `suspend-reactivate`
   each defined at name + GLP-semantic level; progressive-lowering intent stated; `decode(encode(p)) ≡ p` stated
   as the **primary, deterministic** metric for IL-touching seeds; the mis-attributed `2502.06854` citation
   recorded as an open item (DEF-B2; candidate LingoDB VLDB 2022) and NOT blocking.
2. **Spike (FR-043, runnable)**: a Python harness using MLIR Python bindings realizes the four primitives for a
   minimal GLP IL fragment (one clause touching each primitive once) and asserts round-trip identity.
3. **LLM restriction (US4-AC3)**: Claude is restricted to structural generation; the deterministic round-trip
   oracle is the pass/fail metric (mitigates U4). No LM on the verification path → no-API trivially holds.
4. **Reproducible (FR-071)**: committed `run.sh`/`run.ps1`, `tool-versions.txt`, recorded `RESULT.md` (pass/fail).

## Verification
- Run the harness against real MLIR → round-trip identity demonstrated on the fragment; `RESULT.md` = pass
  (US4-AC1).
- Re-run from the committed command → same result (SC-009).
- **Closes**: SC-007, SC-010 (MLIR limb). Desk argument does NOT satisfy this contract (FR-070).
