# Marathon block 06 — `m57f4c46e:implement:6` (implement_session)

**Feature**: 027-refinement-verification-framework
**Block kind**: `implement_session`
**Scope**: the two remaining **real-tool spike RUNS** — US5 SPIN (T024) and US3 Lean (T014→T015).
These are the recorded acceptance evidence (R13/R14, FR-070) for the two P1 spikes whose docs +
subjects landed in block 05. **Owner-authorized** 2026-06-10: Gabi "Approve block 06 pls proceed."

## Order (de-risked first, highest-risk last)
1. **T024 [US5] — SPIN run** (mechanical; model already verified clean in block 05). Run the full
   chain against real SPIN, record RESULT.md + fill run.sh/run.ps1.
2. **T014 [US3] — Lean harness**: `spikes/lean/harness.py` — deterministic local scaffolding for the
   bounded tactic loop: apply a candidate tactic block (replacing the `sorry` in a working COPY, never
   mutating the committed file), run the real `lean` kernel, parse proved|errors|sorry, account the
   attempt against the budget (start 20), enforce sorry-isolation when the budget exhausts. **No
   external API** — the tactic DRIVER is Claude via the Agent-tool seam (this session), the Lean
   kernel is the deterministic oracle (FR-073).
3. **T015 [US3] — Lean run**: drive the bounded loop against the real toolchain on
   `SRSWPreservation.lean`'s `rename_preserves_SRSW`. Record RESULT.md (outcome **proved | sorry-
   isolated**, tactic-attempt count) + run.sh/run.ps1.

## Honest-outcome rule (spec-first; the spike tests the MECHANISM)
`proved` and `sorry-isolated + owner-escalated` are BOTH valid spike outcomes (US3-AC, SC-006). The
spike's value is demonstrating the bounded loop + budget + sorry-isolation against REAL Lean — not
forcing a proof. If the loop cannot close `rename_preserves_SRSW` within budget 20, I `sorry`-isolate
and **escalate to Gabi** (the built-in `stage_flagged` outcome) — never fake a proof, never delete the
theorem, never weaken it to pass.

## Out of scope
- Polish T025–T028 → block 07.
- Full Lean proofs / Rocq / full protocol models — later seeds (#4/#11/#12, #5/#6).

## Boundary
Checkpoint == commit/push boundary. On completion: final checkpoint, then commit + push ONLY this
block's files (`spikes/spin/{RESULT.md,run.sh,run.ps1}`, `spikes/lean/{harness.py,RESULT.md,run.sh,
run.ps1}`, this plan, tasks.md). Lean/SPIN installs live in WSL `$HOME` — only repo artifacts committed.

## Escalation triggers
- Lean budget (20) exhausts without a kernel-checked proof → `stage_flagged`, sorry-isolated, escalate
  to Gabi with the attempt count + best partial goal state.
- SPIN reports a counterexample (deadlock / lost progress) on the minimal handshake → that is a real
  finding: record the trace, report; do not paper over.
