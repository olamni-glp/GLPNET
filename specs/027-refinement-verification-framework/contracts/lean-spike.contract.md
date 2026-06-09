# Contract: Lean 4 Tactic-Loop Validation Spike  (FR-030–035, FR-070–074, R13)

**Artifacts**: `LEAN-TACTIC-LOOP.md` (sketch) + `docs/research/repl-engine-separation/spikes/lean/` (runnable spike).

## Provides
An empirical demonstration that a bounded Claude-over-MCP Lean 4 tactic loop discharges (or `sorry`-isolates +
escalates) one concrete GLP property against a **real Lean 4 install**.

## Acceptance (must all hold)
1. **Sketch (FR-030–034)**: bounded tactic loop with Claude as model-agnostic tactic driver (generate tactic →
   Lean kernel feedback over Lean-LSP-MCP → lemma retrieval/repair → repeat); tactic-attempt budget **start 20,
   tuned**; on exhaustion → `sorry`-isolate + escalate as owner open obligation; Rocq named as the documented
   alternative (→ DEF-F-tooling); WSL2/container Windows setup path documented (R10).
2. **Spike (FR-035, runnable)**: a Python harness drives the loop on one concrete GLP property (SRSW
   preservation on a toy clause; fallback: unification soundness on a toy term) against a real Lean 4 toolchain.
3. **No-API (FR-073)**: tactic generation runs in Claude via MCP; the Lean kernel is deterministic local tooling.
   Zero `OPENAI_API_KEY`/`litellm`/`openai`.
4. **Bounded (US3-AC2)**: exceeding the budget records a `sorry` + surfaces an owner obligation — not silent
   drop, not unbounded run.
5. **Reproducible (FR-071)**: committed `run.sh`/`run.ps1`, `tool-versions.txt`, and a recorded `RESULT.md`
   carrying outcome (proved | sorry-isolated) + tactic-attempt count.

## Verification
- Run the harness against the real toolchain (WSL2/container per R10) → `RESULT.md` produced (US3-AC1/AC3).
- Re-run from the committed command → same recorded result (SC-009).
- **Closes**: SC-006, SC-010 (Lean limb). Desk re-evaluation does NOT satisfy this contract (FR-070).
