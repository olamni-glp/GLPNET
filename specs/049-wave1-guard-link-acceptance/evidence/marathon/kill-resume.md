# US4 scenario 1 — mid-flight kill + fresh-session resume (T023, SC-007)

- **Criterion**: FR-012 / US4 scenario 1 — killed run resumes purely from durable rows; no re-execution; no loss
- **Host(s)**: Olamnit
- **Run**: `mrun-9724364d684a` — state before kill: checkpoints 6 (`66e31e35`) and 7 (`415cbff5`) complete, step `us4-step-3` RUNNING (started 16:17:47Z)
- **Kill**: owning `buildkit-marathon checkpoint` process spawned by this session and killed mid-flight via
  `Stop-Process -Force` — pid **29072**, killed **2026-07-08T16:20:26.186Z**, `was_alive_at_kill=True`
  (two further mid-flight kills during T024 probing: pid 6316 @16:21:32.577Z, pid 2328 @16:22:08.271Z).
  The long-lived PGlite bridge daemons are ephemeral per-invocation (observed exiting on their own), so the
  per-invocation CLI process is the owning process.
- **Command (fresh session)**: `buildkit-marathon.exe resume --json` then `position --json`
- **Output**: resume reported `position: done=2, total=3`, `next_action: resume_step us4-step-3`,
  `recovery.in_flight_steps: [us4-step-3 running, original started_at intact]`,
  `recovery.redrive_checkpoints: []`, `repair: {redriven: [], steps_healed: 0, lock_reclaimed: true}`
- **Assertions**:
  - reported position == durable rows: **PASS** (done=2/3; step-3 running with original timestamps)
  - completed checkpoints NOT re-executed: **PASS** (`repair.redriven=[]`; commit shas `66e31e35`/`415cbff5` unchanged in git log)
  - zero recorded state lost: **PASS** (both checkpoints, item state, step-3 running state all present; stale session lock reclaimed)
  - additional observed property: the durable checkpoint write is **atomic** — every kill that landed before
    the row write left NOTHING half-written (no orphan row, no orphan commit, working file unchanged)
- **Verdict**: PASS
- **Date**: 2026-07-08
