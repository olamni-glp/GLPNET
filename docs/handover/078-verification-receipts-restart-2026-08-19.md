# 078 verification-receipts — SAFE RESTART (2026-08-19)

**Status:** In Progress — **MVP implemented & green; next gate = `/bk-codexreview`.**
**Branch:** `078-verification-receipts` (feature.json → `specs/078-verification-receipts`).
**Feature:** the fleet's #1 (WSJF 7.80, ships-first, hard-blocks #24/#30/#34/#35/#59). Size = saga/35.

## Pipeline position

specify ✓ → clarify ✓ (6 decisions **engineer-ratified**) → plan ✓ → tasks ✓ (42) → analyze ✓ (0 CRITICAL, 100% coverage) → **implement ✓ (MVP, sidecar recorded complete)** → **NEXT = `/bk-codexreview`** → STOP at ship gate for **SHIP-TOKEN**.

## What was ratified this session (all on the recommended option)

- **6 clarify decisions** (spec commit `4f145ea9`): FR-022 sidecar+pointer · FR-023 per-run expected-set (undeclared=error) · FR-024 buildkit owns contract, glpnet binds by version + fixture · FR-005 cap enumerations/keep totals/byte backstop · FR-012 informed-consent override (scope+mandatory expiry) · SC-003 blind reader + cross-lane corroboration, real receipts only.
- **3 plan-forks:** bind-by-version (078 = glpnet consumer + 4 glpnet-area adoptions + fixture; buildkit contract = companion via gavriella lane) · JSON schema + Python ref lib + thin per-area emitters · MVP mechanism first (US1-3 on a reference check).
- **Ship dependency (2026-08-19):** **SHIP THE MVP ON THE DRAFT contract** (`buildkit-draft-0`); re-pin bind.py at **T037** when the buildkit contract lands. 078 does NOT wait on the buildkit lane to reach its first SHIP-TOKEN.
- **Pre-existing red suite:** ruled **file separately, proceed** — see `docs/handover/078-preexisting-test-failures-2026-08-19.md`.

## What is built (MVP = Phases 1–5, 31 tasks marked [X])

`codeconv/src/codeconv/receipts/`: `paths.py` (FR-022 locations) · `outcome.py` (5-value + worst-wins) · `bind.py` (draft contract resolver, MAX_ENUM/MAX_FIELD_BYTES) · `receipt.py` (Target/Receipt/classify/validate/emit/load, bounding) · `consumer.py` (refusal + aggregate) · `manifest.py` (adoption + per-run expected-set) · `override.py` (informed-consent).
Tests: `codeconv/tests/test_receipt_us1.py`, `test_receipt_us2.py`, `test_bounded_receipt.py`, and `tests/faultinj/` (reference_check + conformance fixture + 10 fault tests). **29/29 green.**
Checked-in manifest: `.specify/receipts/adoption.json` (reference=adopted; build-gate/coop/roadmap-sync/test-harness=non-adopted — honest partial coverage).
Commits: spec `4f145ea9`; plan `plan(078)`; tasks `tasks(078)`; impl `feat(078): MVP verification-receipts…`.

## What remains (post-MVP, NOT in first ship)

- **US4 (T031–T037):** retrofit the 6 real areas incrementally, each reproducing its historical instance; flip adoption per area; **T037 re-pins bind.py to the released buildkit contract**. Buildkit companion change (schema+fixture+3rtask/codexreview adoptions) delivered by the buildkit/gavriella COOP lane.
- **Polish (T039/T040):** SC-001 (13/13), SC-002 (100% areas), **SC-003 blind-reader gate**, T042 baseline regression.
- These are where SC-001/002/003 close; the **first ship gate closes SC-005/006/007 on the reference check only.**

## RESTART PROCEDURE (next session)

1. Mandatory reading (CLAUDE.md + 3 GLP docs) → acknowledge.
2. On branch `078-verification-receipts`; confirm feature.json. Baseline: `cd codeconv && .venv/Scripts/python.exe -m pytest tests/test_receipt_us1.py tests/test_receipt_us2.py tests/test_bounded_receipt.py tests/faultinj/ -q` → expect 29 green.
3. **Run `/bk-codexreview`** on the MVP diff (default multi-cycle adversarial). Address findings.
4. STOP at ship gate → request **SHIP-TOKEN** from Gabi → `/bk-ship` (buildkit GitFlow) on the DRAFT contract → `/bk-close`. Then the 5 downstream features unblock.
5. Beyond: US4 retrofits (coordinate buildkit contract landing via COOP), then Polish SCs.

## Environment gotchas

- codeconv tests use `codeconv/.venv/Scripts/python.exe` (NOT the buildkit .venv313, which lacks pytest).
- The full codeconv suite is slow (~35 min) and has **18 pre-existing failures** (filed separately) — do NOT block 078 on them.
- Buildkit catalog writes intermittently blocked: version-lag (installed 2026.8.10.1 < marathon run 2026.8.14.1) AND a concurrent session's `pgdb/.lock` (PID 33588). Advisory tools degrade; git/memory are source of truth (FLEET-INCIDENT).
- Never run the full suite through `| tail` or a short `timeout` — it masks the real exit code (that hid the red baseline this session).
