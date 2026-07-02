# Restart pointer — NOT a work ledger

> This file is intentionally thin. Do **not** write a full multi-step plan here and
> "resume from the current step" — that mechanism drifted stale (it once pointed
> restarts at already-shipped work). The **roadmap + buildkit pipeline / marathon state**
> are the source of truth. See CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*.

## How to locate yourself on any restart (fresh / post-compaction / post-crash)

1. **What feature / what stage?** → `buildkit-roadmap next` (or `buildkit-roadmap status`).
2. **In progress?** → a feature with a spec dir (`.specify/feature.json` → `specs/<NNN>/`)
   has entered the pipeline; a `bk-marathon` run drives the bigger ones.
3. **Where in the feature (WIP position)?** → the marathon durable rows (objective) +
   the feature's `tasks.md`.

## Active now — two parallel threads (merged 2026-07-02, 036↔develop integration)

### Thread A — `036-http3-quic-ws-link` (this branch; pinned in `.specify/feature.json`)

- HTTP/3 (QUIC) + WebSocket channel-link prototype for GLP. Core **US0–US3 Profile A** verified green
  (18 `glp_quick` pytest + 104 `glp_link` xUnit; REPL 524/525, the lone failure a pre-existing,
  unrelated AOT-exe smoke case).
- Three environment-blocked acceptance items — **T032** Profile C (`quicer`/MsQuic in-process, needs
  MSVC), **T040** two-host LAN e2e (needs the `gavri` host), **T003/T036** marathon durability of the
  never-persisted `mrun-15d7dd0ffbc2` — were **carved out (2026-07-02) into roadmap feature
  `http3-quic-ws-link-full-acceptance`** (epic `distributed-glp-connectivity`, promoted). Those 036
  tasks are marked `[>]` deferred; brief at `specs/036-http3-quic-ws-link/followup-full-acceptance-brief.md`.
- Remaining native 036 task: **T037** (single-host quickstart docs). Downstream: `/bk-codexreview` → ship.

### Thread B — PROGRAM `036-glp-gleam-baseline-program` (from develop; run `mrun-5611c436ba95`)

- GLP→Gleam/AtomVM research/verification/reconfiguration program under `/bk-marathon`. Work tasks
  **T001–T013 COMPLETE + checkpointed; T014 discharge gate APPROVED (Gabi 2026-06-30)**. **Remaining:
  T015** — the single roadmap migration (create epics **Optional features** + **Full Gleam
  implementation**; add the 24 features per the ADVISORY MIGRATION MAPPING in
  `docs/research/glp-gleam-baseline/pipelines/P8-synthesis/RECONFIGURATION.md`; then `marathon discharge`).
  The 16 owner-decisions **D1–D16** are feature-level — resolve **after** the migration.
- **Resume objectively:** `PYTHONUTF8=1 python -m buildkit_cli.marathon resume --run mrun-5611c436ba95`
  (→ done 13/14, discharge 1/1, next = T015). Position comes from the durable rows, never this file.
- Deliverables committed under `docs/research/glp-gleam-baseline/` (CORPUS-INDEX, PROOF-HARNESS,
  pipelines P1b/P2/P3/P4/P6/P7/P8, ANTLR-integration).

## History (do not resume these — they are done/parked)

- `034-glp-gleam-core-terms-and-heap` (gleam-atomvm F4) **SHIPPED** `v2026.06.25.1`.
- `030-marathon-refinement` **SHIPPED** (`v2026.06.12.1` + Phase 8 `v2026.06.19.1`).
- `035-semantic-tombstone-enrichment` **SHIPPED** `v2026.06.26.1`.
