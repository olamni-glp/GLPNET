# Contract — Tiered fidelity metric (FR-013, SC-004)

`tools/equiv/fidelity.py`. **Pure**, the SINGLE scorer imported by BOTH the production gate (`tools/equiv/promote`, `tools/codegen/`) AND the GEPA metric (`codegen_opt/metric.py`). One implementation ⇒ they agree by construction (SC-004).

## Signature
```
score(file_state: FidelityInputs) -> float   # in [0.0, 1.0]

FidelityInputs = {
  builds:        bool
  back_tested:   bool
  trace_captured: bool
  in_scope_sources: int          # denominator for frac
  trace_equivalent_sources: int  # numerator (outcome-equivalent counts for bonds)
}
```

## Exact tiers (verbatim from FR-013)
```
if not builds:                              return 0.0           # hard floor
frac = trace_equivalent_sources / in_scope_sources   # 0 if denom 0
if not (back_tested and trace_captured):    return 0.25          # flat low band
if frac >= 1.0:                             return 1.0           # total trace-equivalence ONLY
return min(0.5 + 0.5 * frac, NEXTBELOW_1)   # high band, clamped strictly < 1.0
```
- `0.0` non-compile hard floor.
- flat `0.25` compiling-unreviewed, no equivalence evidence.
- high band `0.5 + 0.5·frac`, **monotonic** in `frac` (continuous GEPA gradient), clamped strictly below `1.0` until `frac == 1.0`.
- exactly `1.0` reserved for total trace-equivalence (the `frac == 1.0` snap).

`NEXTBELOW_1` = the largest representable float `< 1.0` (so a file at frac=0.999… never reads as 1.0). No partial state — not compile, not human approval, not back-tests alone — reaches 1.0 (SC-004).

## Promotion (FR-014)
A subsystem/runtime promotes to "converted" iff **every** in-scope source is trace-equivalent (outcome-equivalent for bonds) — i.e. `score == 1.0` for every file. Compile / human-approval / back-test-pass alone MUST NOT promote.

## GEPA wiring (cross-reference `gepa_optimizer.md`)
`codegen_opt/metric.py` builds `FidelityInputs` from the same `dart_equivalence` aggregation the gate uses and calls THIS `score()`. The GEPA metric additionally returns textual feedback (the `DivergenceRecord`) — the scalar is identical to the gate's.

## Tests (SC-004)
- non-compile ⇒ 0.0; compile-no-evidence ⇒ 0.25; back-tested+captured partial ⇒ in (0.5,1.0); frac=1.0 ⇒ exactly 1.0.
- a compiling, back-test-passing, human-approved but NOT trace-equivalent file ⇒ strictly < 1.0.
- gate score == GEPA-metric score on identical inputs (same function — asserted by import identity).
