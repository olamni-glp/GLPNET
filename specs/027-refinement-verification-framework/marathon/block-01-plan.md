# Marathon block 01 — `m57f4c46e:implement:1` (implement_session)

**Feature**: 027-refinement-verification-framework
**Block kind**: `implement_session` (first of the implement series)
**Scope**: Setup + Foundational read-only gate — the SAFE, no-owner-decision, no-real-tool, no-LM block.
**Approved by**: gabi (session of 2026-06-09; "block 1 ok").

## Units (tasks.md)
- **T001** — Create the spike subtree skeleton `docs/research/repl-engine-separation/spikes/{lean,mlir,spin}/`, each with placeholder `run.sh`, `run.ps1`, `tool-versions.txt`, and `RESULT.md` stubs.
- **T002 [P]** — Confirm `codeconv/.venv` is usable for the harnesses and record the baseline Python version in each spike's `tool-versions.txt`.
- **T003** — Read-only input gate: verify the 026 artifacts (`REFINEMENT-METHOD.md`, `DECISIONS-FOR-OWNER.md`, `DECISIONS-LOG.md`, `DEFERRALS.md`) are present + authoritative under `docs/research/repl-engine-separation/reconciliation/`; STOP + report if any missing.

## Out of scope (deferred to later blocks)
- T004–T005 (owner-gated: prover choice, Shapiro map, MLIR primitives, tactic depth) — block 2.
- T006–T011, T016, T021 (doc authoring) — later blocks.
- T012/T017/T022 + downstream (real-tool installs: WSL2 + Lean/MLIR/SPIN) — HARD STOP, await Gabi's provisioning go.

## Boundary
Checkpoint == commit/push boundary. On block completion: final checkpoint, then commit + push ONLY this block's files under the standing grant (no `git add -A`, no force, no hook bypass).

## Notes / issues surfaced
- The venv Python is **3.14.3** (tasks.md assumed 3.11 — stale guess). T002 records the actual baseline; not a blocker.
