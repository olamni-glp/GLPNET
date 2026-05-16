# Current Plan: 016-codeconv-init-scaffold-langpair (speckit-implement)

Started: 2026-05-16
Branch: `016-codeconv-init-scaffold-langpair` | Base HEAD: `177a33f8`

## Steps
- [x] 0. Read all design docs; create 016 venv; install codeconv editable
- [x] 1. Phase 1 Setup (T001-T004)
- [x] 2. Phase 2 Foundational (T005-T012)
- [x] 3. Phase 3 US1 init (T013-T017) [+ US4 tests T027 written]
- [x] 4. Phase 4 US2 scaffold (T018-T023) [spec-conflict reconciled: managed-target]
- [ ] 5. Phase 5 US3 registry extensibility (T024-T026) <- CURRENT
- [ ] 6. Phase 6 US4 exclusions (T027-T029)
- [ ] 7. Phase 7 US5 pipeline regression (T030-T031)
- [ ] 8. Phase 8 Polish + removal (T032-T037)

## Context
Port D2NET Init+Scaffold into codeconv as `codeconv init`/`codeconv scaffold`
behind a pluggable langpair registry (dart_csharp). Alembic 0003 moves D2NET
public.* tables into codeconv schema. Remove tools/d2net/ + D2NET-* skills.

## Env notes (load-bearing)
- Worktree: D:\BSTDEV\research\GLP\GLPNET-016
- venv: D:\BSTDEV\research\GLP\GLPNET-016\codeconv\.venv (created this session;
  codeconv installed editable -> 016 src)
- Bash CWD locked to MAIN worktree (GLPNET, branch 015). `cd` to Windows
  `D:/...` denied; `cd /d/.../GLPNET-016/...` (git-bash forward slash) OK;
  `git -C "D:\...\GLPNET-016"` OK. Always dangerouslyDisableSandbox=true.
  Invoke venv python as `cd /d/.../GLPNET-016/codeconv && .venv/Scripts/python.exe`.
- DB data-dir for live: C:/pglite/research/glpnet-016 (NOT glpnet — PG16/shared).
  Tests use tmp_path (NTFS) — no override needed.
- pytest synchronously; PGLite cold-init ~7s; ≥60s timeout first bridge call.
