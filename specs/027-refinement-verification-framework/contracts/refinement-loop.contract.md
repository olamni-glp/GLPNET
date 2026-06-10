# Contract: GEPA/DSPy Refinement Loop  (FR-010–013)

**Artifact**: `REFINEMENT-METHOD.md` §1 (finalized) — documents the loop; no new runtime code.

## Provides
A documented, seed-instantiable bounded refinement loop whose seams map 1:1 onto the in-repo precedent.

## Acceptance (must all hold)
1. Loop documented as: seed → candidate → evaluate against metric combination → GEPA reflective mutation /
   DSPy compile-time optimization → repeat until thresholds hold (FR-010).
2. Seam structure (generate / propose / evaluate / budget) matches `codeconv/src/codeconv/tools/codegen_opt/optimize.py`
   `run_optimize` (~lines 257–335) with **zero unmatched seams** (FR-011, SC-002).
3. A budget-capped run yields the **best-so-far** candidate, never unbounded (FR-013, US2-AC2).
4. **No-API rule**: all LM steps run in Claude via Agent-tool seams / MCP; any "needs an API" line is a
   defect to delete (FR-012).

## Verification
- Trace the documented seams against `optimize.py:257–335` (mapping table in research.md §4).
- `grep -rEi 'OPENAI_API_KEY|litellm|(^|[^a-z])openai'` over all framework artifacts + any example code →
  **zero** matches on a refinement/verification path (SC-003, US2-AC3).
