# Restart pointer — NOT a work ledger (updated 2026-07-27)

> Intentionally thin. The **roadmap + buildkit pipeline / marathon state** are the source of truth
> (CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*). Do not resume from a hand-written plan.

## 🔴 Environment gotchas — read first (2026-07-27, post drive-swap)

The machine rebuild left several things unset. A fresh shell needs:

```
$env:PATH = "C:\Program Files\nodejs;C:\Program Files\Git\cmd;C:\Program Files\GitHub CLI;$env:PATH"
$env:PYTHONUTF8 = 1
```

- **`node` is NOT on PATH** — every `buildkit_cli` command that touches PGlite exits 2 with
  "Node 20+ not found on PATH" until you prepend it. Node itself is fine (v24.18.0).
- **`git` and `gh` are NOT on PATH** either.
- **git `safe.directory`** was needed (files carry the old machine's SID); already added globally for
  both `D:/BSTDEV/research/GLP/GLPNET` and the lowercase spelling.
- **git identity** is set **repo-locally** to `vonwenm <mvw@bancstreet.com>` (matches commit history;
  there was no global identity at all).
- **`gh`** is authenticated as `vonwenm` (keyring). `gh pr merge` is now in
  `.claude/settings.local.json`'s allow-list.
- **`python -m buildkit_cli.*` works with system Python 3.14** — no venv needed for roadmap/pipeline/
  marathon. `buildkit-roadmap` / `buildkit-size` console scripts are **not** on PATH
  (`buildkit_cli.size` does not exist as a module — sizing steps skip silently).

## How to locate yourself on any restart

1. **Roadmap** → `python -m buildkit_cli.roadmap status` (56 closed / 37 open across 10 epics).
2. **Active feature** → `.specify/feature.json` = `specs/060-wave3-full-gleam-chain`.
3. **Pipeline** → `python -m buildkit_cli.pipeline.cli status`.
4. **Marathon** → `python -m buildkit_cli.marathon status --feature wave-3-consolidated-full-gleam-chain`
   (run `mrun-e300493d5a6d`), or `marathon resume --run mrun-e300493d5a6d`.

## Where things stand (2026-07-27)

- **059** `full-scope-gleam-glp-implementation` — **merged to develop** (PR #115, merge `f08940ce`).
  Carries a live escalation: T051 parity **44 missing corpus goldens** (evidence-reproducibility drift).
  Wave 3 inherits it as FR-018a/FR-018b/SC-010.
- **060** `wave3-full-gleam-chain` — branch `060-wave3-full-gleam-chain`, off develop.
  Pipeline: specify ✅ clarify ✅ plan ✅ tasks ✅ analyze ✅ → **implement is next**.
  Marathon `mrun-e300493d5a6d`: **5/10 steps complete**.

## NEXT — `/bk-implement` on 060

Artifacts are all on disk under `specs/060-wave3-full-gleam-chain/`:
`spec.md` (34 FR, 10 SC, 5 user stories) · `plan.md` · `research.md` (decisions D1–D4, gap analysis
G1–G10) · `data-model.md` · `quickstart.md` · `contracts/{repl-commands,link-handshake,corpus-report}.md`
· `tasks.md` (**52 tasks, 8 phases, MVP = phases 1–3**) · `analysis-findings.md`.

**Before writing any code**: Phase 1 T001–T004 capture the non-regression baseline
(`gleam test` in `glp_gleam/`, expected **465 green**; `bash test/run_all_tests.sh`). Constitution VII
makes this non-negotiable — a change against a red baseline cannot be attributed.

**Two open findings from analyze** (see `analysis-findings.md`, not yet applied):
- **C1 (HIGH)** — FR-007 writer-MGU has no task, though this wave changes the loader and engine seam.
- **B1 (MEDIUM)** — the Bug-Protocol "STOP, don't regenerate a golden from the runtime under test"
  obligation is prose under Phase 5, not inside task T029.

**Advisory, unresolved**: wave 3 is roadmap-state `captured` and recorded blocked-by
`wave-2-consolidated-repl-engine-split-spine`, which has not been built — the owner accepted jumping
that order. The roadmap will **not** auto-link this spec (slug `wave-3-consolidated-full-gleam-chain`
vs dir `060-wave3-full-gleam-chain`); Constitution VIII marks that clause advisory.

## History (done — do not resume)

- 2026-07-27: roadmap sweep (39 `released` → `closed`); 059 merged; 060 specified through analyze.
- Earlier: `037` folded into **040** (shipped). `036` v2026.07.02.3; `038` v2026.07.02.1;
  `039` v2026.06.30.1. Earlier still: 034/035/030.
