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

## NEXT (post-restart, in order) — per owner directive 2026-07-04
0. **RESOLVE roadmap duplicate GUIDs** from the CRDT merge FIRST — many features doubled
   (`abandon-operation` + `abandon-operation--01kwjqmh`, `zmq-comm-base` + `…--01kwjqmh`, etc.). The build order
   lists both → marathon would double-run. Dedup/reconcile before driving.
1. **`/bk-upgrade` project artifacts → buildkit latest** (source tag **v2026.07.03.2**, HEAD feat 047).
   Only read-only `status` + `--help` ran this session — NO writes. Path = `apply` (installed_kind=buildkit),
   dirty-tree gate needs a clean tree or `--yes`. Then **verify**.
2. **`/bk-deploy` buildkit latest** into deploy-home (`C:\Users\smbuser\AppData\Local\buildkit\deploy-home`,
   default currently **v2026.07.03.1** → advance to latest) + register this repo. Then **verify**.
3. **Marathon the roadmap** (owner directive): per feature via **/bk-marathon**:
   specify→clarify→plan→tasks→analyze(top remedies)→implement→codexreview→close→commit/push/merge, checkpointing
   for safe restart after clarify/analyze/implement/close and after MVP within implement. First pending in the
   owner's order: **abandon-operation**. **Host-blocked** (skip/defer here): `http3-quic-ws-link-full-acceptance`
   (Profile C needs MSVC quicer NIF; two-host e2e needs the `gavri` host).

## History (done — do not resume)
- `037-virtual-3270-term` folded into **040** (shipped). `036-http3-quic-ws-link` shipped v2026.07.02.3;
  `038` v2026.07.02.1; `039` v2026.06.30.1. Earlier: 034/035/030.
