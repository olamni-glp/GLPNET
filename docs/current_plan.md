# Restart pointer — NOT a work ledger (updated 2026-07-04)

> Intentionally thin. The **roadmap + buildkit pipeline / marathon state** are the source of truth
> (CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*). Do not resume from a hand-written plan.

## 🔴 Tooling gotcha that bit the 2026-07-04 session — read first
Two buildkit installs exist. Use the RIGHT one:
- `D:/bstdev/research/buildkit/.venv/Scripts/python.exe` → **buildkit-cli 2026.7.1.1** — HAS
  `roadmap {export,import,replay}` + `upgrade` + `deploy`. **Use this for roadmap sync / upgrade / deploy.**
- `D:/bstdev/research/buildkit/.venv313` → **stale 2026.6.27.1** — LACKS import/export. The old CLAUDE.md
  "run via .venv313" note is out of date for these commands. (marathon/DBOS may still need 3.13 — verify.)

## How to locate yourself on any restart
1. **Feature states** → `<.venv python> -m buildkit_cli.roadmap status` (25 epics / 140 features).
   `next` → recommends the build order.
2. `.specify/feature.json` → `specs/040-rcopy-file-transfer-service` (shipped; a finished pointer, not WIP).
3. Branch **`develop`** @ pushed HEAD; tree clean. `main` = **v2026.07.04.3**; develop = main + F3 retro + sync chores.

## DONE this session (2026-07-04, all committed+pushed)
- **Ship**: cut **v2026.07.04.1** from develop (`buildkit release`, PR #75 merged, back-merge #76). main later
  advanced to **v2026.07.04.3** from the other host (gavriellas).
- **Fixed red baseline before ship**: `test_duplicate_announced_id_never_evicts_the_incumbent` — ROOT CAUSE was a
  **stale `glp_quick_host.dll`** (2026-06-28, pre-040-routing-fix), not a code bug. Rebuilt host (killed 6 orphaned
  host procs holding the dll lock) → glp_quick **178 pass / 1 skip**. No source change (bin/ gitignored).
- **Roadmap CRDT sync**: `roadmap import`→`export`→`import` (olamnit↔gavriellas), idempotent. Pulled in the 11
  features that looked "missing" pre-import (crdtmsg-*, glp-gleam-*, three-role-agent-team, cross-runtime tests).

## NEXT (post-restart, in order) — per owner directive 2026-07-04 (rev. 083x)
0. **✅ DONE — roadmap dedup.** The 55 dup-GUID groups from the olamnit↔gavriellas CRDT merge were
   resolved via NEW buildkit soft-tombstone primitives (`delete/reject/supersede/merge` + epic
   `delete`), landed on buildkit branch **`roadmap-dedup-primitives`** (commit `bcb866a`, 8 tests
   green, regression-clean). 55 features superseded + 7 empty dup epics deleted → **0 dup groups,
   78 live features, 18 epics**. Backup export `…083157Z.json` (pre) + `…083445Z.json` (post).
   The tombstones are standard CRDT (`action='tombstoned'`), so the dedup **persists even without
   merging the buildkit branch** (develop's fold already honours tombstones). `/bk-codify` note
   `cn-20260704T083015-4ebcfa42` seeds the proper spec-driven `dedup` feature later.
   **`/bk-upgrade` + `/bk-deploy` SKIPPED** per owner (2026-07-04).
1. **Marathon the roadmap** via **/bk-marathon**: per feature run
   specify→clarify→plan→tasks→analyze(top remedies)→implement→codexreview→close→commit/push/merge,
   checkpointing for safe restart after clarify/analyze/implement/close and after MVP within implement.
   **`roadmap next` now recommends `crdtmsg-mvp`** (CRDT multi-format messaging MVP, state=promoted) —
   START HERE with `/bk-specify` (research already done: `docs/research/crdt-multiformat-messaging/`).
   **Host-blocked** (skip/defer here): `http3-quic-ws-link-full-acceptance`
   (Profile C needs MSVC quicer NIF; two-host e2e needs the `gavri` host).
   Buildkit branch `roadmap-dedup-primitives` is ready to ship to buildkit `develop` (optional; not
   required for the dedup to hold).

## History (done — do not resume)
- `037-virtual-3270-term` folded into **040** (shipped). `036-http3-quic-ws-link` shipped v2026.07.02.3;
  `038` v2026.07.02.1; `039` v2026.06.30.1. Earlier: 034/035/030.
