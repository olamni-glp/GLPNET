# Marathon block 03 — `m57f4c46e:implement:3` (implement_session)

**Feature**: 027-refinement-verification-framework
**Block kind**: `implement_session`
**Scope**: HARD real-tool provisioning — the three env setups, front-loaded (research §5, the critical path). **Owner-authorized** 2026-06-09: Gabi confirmed WSL2 + Lean/MLIR/SPIN provisioning ("approved").

## Environment (probed read-only, this session)
- WSL2 v2.6.3.0, default distro **Ubuntu** (v2); Python 3.12.3; gcc/cc/make/git/curl/unzip/pipx present; pip 24.0.
- **sudo requires a password (non-interactive sudo unavailable)** → all steps below use a **sudo-free** path (user-space installs only).
- Network: pypi 200, elan host reachable.

## Units (tasks.md)
- **T022 [US5]** — Install real **SPIN**: build from source (`github.com/nimble-code/Spin`) → `~/.local/bin/spin`; gcc compiles `pan.c`. Capture version in `spikes/spin/tool-versions.txt`. (sudo-free; apt's spin 6.5.2 would need sudo.)
- **T012 [US3]** — Install **Lean 4**: `elan` user-space (`~/.elan`) → `lean`/`lake`; wire **Lean-LSP-MCP** (pipx). Capture versions in `spikes/lean/tool-versions.txt`. (sudo-free.)
- **T017 [US4]** — Acquire **MLIR Python bindings**: pip wheel into a venv (py3.12) first. Capture versions in `spikes/mlir/tool-versions.txt`. (sudo-free.) **Fallback** (WSL2 LLVM build, `-DMLIR_ENABLE_BINDINGS_PYTHON=ON`) is hours-long + may need sudo → **ESCALATE to Gabi before committing to it**.

## Out of scope (later blocks)
- Subjects T013/T018/T023, harnesses T014/T019, recorded runs T015/T020/T024 — per-spike blocks AFTER the tools are up.
- Doc tasks T006–T011, T016, T021 — interleaved/after (US1/US2 + the three approach docs).

## Boundary
Checkpoint == commit/push boundary. On completion: final checkpoint, then commit + push ONLY this block's files (the three `tool-versions.txt`, this plan, tasks.md). Installs land in WSL `$HOME` (outside the repo) — only the captured version files are committed.

## Escalation triggers
- MLIR wheel unavailable for py3.12 → `stage_flagged` escalation, ask Gabi re: LLVM-build fallback (time/space/sudo).
- Any step needing sudo that has no user-space path → hand Gabi a `! sudo …` one-liner.
