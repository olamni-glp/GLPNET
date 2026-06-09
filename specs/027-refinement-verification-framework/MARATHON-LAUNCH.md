# 027 — Marathon launch prompt (safe-restart signal)

**Purpose:** paste the block below into a *fresh* session (after `/clear`) to drive feature
`027-refinement-verification-framework` under the marathon harness. This file is the **launch
signal only** — it is NOT a work-state ledger. Authoritative state lives in the buildkit roadmap,
the pipeline (DBOS/PGLite), and `specs/027-refinement-verification-framework/tasks.md`. The harness
owns the durable checkpoint + compaction/crash recovery.

---

## Paste this into the fresh session

```
/marathon-stage-harness

Drive feature 027-refinement-verification-framework (engine-separation epic, seed #1a) through the
buildkit pipeline as a marathon: durable per-stage checkpoints, an approval gate per stage, and
preauthorized per-block commit/push.

Do NOT trust this prompt for state — re-locate objectively: buildkit-roadmap next → the 027 spec dir
→ specs/027-refinement-verification-framework/tasks.md for the WIP position. Then resume at the first
uncompleted stage.

Feature shape (28 tasks; checklist passes; 026 input artifacts REFINEMENT-METHOD.md /
DECISIONS-FOR-OWNER.md / DECISIONS-LOG.md / DEFERRALS.md all present under
docs/research/repl-engine-separation/reconciliation/):
  - SAFE in-repo block — do these first, commit per block:
      T001–T002 spike skeleton + venv baseline
      T003 read-only input gate (already verified present)
      T004–T005 finalize REFINEMENT-METHOD.md + DECISIONS-FOR-OWNER.md
      T006–T008 metric-combination template + worked example (US1, MVP)
      T009–T010 loop↔precedent seam mapping + no-API grep gate (US2)
      T011 LEAN-TACTIC-LOOP.md, T016 MLIR-GLP-DIALECT.md, T021 PROTOCOL-VERIFICATION-ARMOURY.md
  - HARD real-tool block — STOP and get Gabi's go before starting:
      T012 Lean 4 + Lean-LSP-MCP in WSL2 · T017 real MLIR Python bindings · T022 real SPIN + gcc
      → harnesses T014 / T019 → recorded real-tool runs T015 / T020 / T024

Hard guardrails:
  - No external LM API anywhere (FR-012/073). All LM steps run in Claude via Agent-tool/MCP. grep-gated.
  - STOP at the real-tool boundary (before T012/T017/T022). WSL2 + Lean/MLIR/SPIN installs are
    environment-mutating and hard-to-reverse, with unknowns (is WSL2 provisioned? net access for
    elan/Lean? an MLIR wheel for this host?). Confirm provisioning with Gabi first.
  - T005 / DECISIONS-FOR-OWNER content + the Shapiro mandatory/advisory map + prover choice + tactic
    depth are OWNER-gated by design — surface options, do not decide unilaterally.
  - The three RESULT.md spikes ARE the acceptance tests (R13/R14). Desk research does NOT satisfy them.
  - Marathon safety: never start/stop/resume/touch the workflow unbidden; gates + escalations are the
    only autonomous boundary; checkpoint to disk before signalling /clear.
```

---

## Safe-restart sequence — when & how

**When:** only after the 027 pipeline artifacts are committed + pushed (they are currently UNTRACKED:
`plan.md`, `tasks.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/`, this file, and the
`CLAUDE.md` edit). A `/clear` keeps the working tree, but a durable git checkpoint is the real safety net.

**How:**
1. Commit + push the 027 pipeline artifacts → durable base.
2. This launch file is already on disk.
3. Run `/clear`.
4. Fresh session auto-reads CLAUDE.md (mandatory 4-doc reading) + MEMORY.md, then you paste the block above.
