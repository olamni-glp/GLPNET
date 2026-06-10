# Contract: Metric-Combination Template  (FR-003, R8)

**Artifact**: `docs/research/repl-engine-separation/reconciliation/METRIC-COMBINATION-TEMPLATE.md`

## Provides
A reusable Markdown table every successor seed (#2–#16) instantiates at its `/buildkit-specify`, plus one
fully-filled worked example for an already-reconciled seed.

## Shape (the template)
```
| name | kind (pragmatic\|formal) | tool | threshold |
|------|--------------------------|------|-----------|
|      |                          |      |           |
```

## Acceptance (must all hold)
1. Columns are exactly `name | kind | tool | threshold`; `kind ∈ {pragmatic, formal}`.
2. Includes a **filled worked example** for one reconciled seed (e.g. #5 result codec) where:
   - every row has a concrete tool/harness AND a measurable threshold; and
   - because the seed touches a wire/byte contract, ≥1 `formal` row is present (FR-021, US1-AC1).
3. Includes the **host/infra rule**: a #8/#10 table MAY omit formal rows but MUST carry an explicit
   per-Shapiro-criterion N/A justification (R9, US1-AC2).
4. States that the owner-confirmed table is recorded in the seed spec **before** task generation (US1-AC3).

## Verification
- Doc-completeness check: template present + ≥1 worked example that satisfies (2).
- Independent test (SC-008): a reviewer instantiates the template for one reconciled seed without inventing
  format or asking how the loop/proof works.
