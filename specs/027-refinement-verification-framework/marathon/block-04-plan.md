# Marathon block 04 — `m57f4c46e:implement:4` (implement_session)

**Feature**: 027-refinement-verification-framework
**Block kind**: `implement_session`
**Scope**: the **US4 / MLIR vertical slice** (P2) — author the dialect spec doc + the real-MLIR
round-trip validation spike end-to-end. **Owner-authorized** 2026-06-10: Gabi granted block-4
plan-approval ("block-4 plan-approval granted"), US4 explicitly NOT to be deferred (matches the
T017 resolution note). The block-3 critical-path tool (T017, real MLIR bindings) is already up +
smoke-verified, so this block is doc + subject + harness + recorded run.

## Environment (probed read-only, this session)
- Durable state: `marathon resume` → block-3 complete, committed, 0 escalations, stores aligned
  seq 6, budget headroom 24.8M. Clean block-3→block-4 boundary.
- Real tool (T017): WSL2 Ubuntu, `~/mlir-spike/wheel-venv` py3.12.3,
  `mlir-python-bindings 22.0.0.2025112901` (makslevental find-links; REAL compiled `mlir.ir`).
  Re-verified live this session: `import mlir.ir` + `Module.parse` round-trip → True.
- The spike runs in **WSL2** (no compiled-LLVM cp314 Windows wheel) — `run.sh` is canonical,
  `run.ps1` wraps it via `wsl.exe`.

## Units (tasks.md — the US4 chain, in dependency order)
- **T016 [US4]** — Author `reconciliation/MLIR-GLP-DIALECT.md` (FR-040–042): the four primitives
  `HEAD-unify` / `GUARD-test` / `BODY-spawn` / `suspend-reactivate` each with GLP-semantic meaning,
  progressive-lowering intent, the `decode(encode(p)) ≡ p` primary deterministic criterion (Claude =
  structural only), and DEF-B2 (`2502.06854` mis-attributed; candidate LingoDB VLDB 2022) recorded
  open. Consistent with REFINEMENT-METHOD §4 slot 2. **Non-blocking** (parallel-safe).
- **T018 [US4]** — Define the minimal GLP IL fragment **ILFRAG-1** (one clause touching each of the
  four primitives once) as `spikes/mlir/ilfrag1.py` — a Claude-free structural Python dataclass.
  Subject clause: `p(X, Y?) :- ground(X?) | q(Y).` (head-unify on `p/2`, guard-test `ground(X)`,
  body-spawn `q(Y)`, suspend-reactivate on reader `Y`).
- **T019 [US4]** — Implement `spikes/mlir/harness.py` using the **real `mlir.ir`** bindings:
  `encode(p)` builds an MLIR module realizing the four primitives as ops in an (unregistered) `glp`
  dialect; print→parse→walk = `decode`; oracle asserts `decode(encode(p)) == p` (structural IL
  equality) AND textual idempotence `str(m1)==str(m2)`. **No LM in the verification path** (FR-073);
  Claude restricted to structural generation. Depends on T017 (done) + T018.
- **T020 [US4]** — Run the spike against real MLIR in WSL2; record `spikes/mlir/RESULT.md`
  (pass/fail on ILFRAG-1, Claude-structural-only confirmation) + fill reproduction
  `spikes/mlir/run.sh` (canonical) / `run.ps1` (wsl.exe wrapper). Depends on T019.

## Out of scope (later blocks / later seeds)
- US1 (T006–T008 template/MVP), US2 (T009–T010 loop), US3 (T011/T013–T015 Lean), US5
  (T021/T023–T024 SPIN), Polish (T025–T028). Separate blocks.
- The FULL opcode set / lowering pass / production dialect — #4/#11 (DEF-B1/H1). This is a
  **minimal feasibility spike only** (FR-074): one clause, four primitives once each.
- Pinning the DEF-B2 citation — recorded open, anchored #4/#12; MUST NOT block (FR-042).

## Boundary
Checkpoint == commit/push boundary. On completion: final checkpoint, then commit + push ONLY this
block's files — `MLIR-GLP-DIALECT.md`, `spikes/mlir/{ilfrag1.py,harness.py,RESULT.md,run.sh,run.ps1}`,
this plan, `tasks.md` (T016/T018/T019/T020 → [X]). The wheel-venv lives in WSL `$HOME` (outside the
repo) — only repo artifacts are committed.

## Escalation triggers
- `decode(encode(p)) == p` **fails** on ILFRAG-1 → that is a real finding, NOT a thing to work
  around (no try/catch papering): `stage_flagged` escalation, report to Gabi (the round-trip claim
  under test would be disconfirmed — bug-protocol, not a fix-by-guess).
- mlir.ir API gap blocking honest construction of the four primitives → `stage_flagged`, report
  (do NOT silently downgrade to a non-MLIR / pure-python stand-in — that violates R13 "real MLIR").
