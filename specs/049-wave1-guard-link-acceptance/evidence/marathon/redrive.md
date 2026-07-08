# US4 scenario 2 — durable-first checkpoint / commit re-drive (T024, FR-012)

- **Criterion**: US4 scenario 2 — checkpoint written durable-first with its scoped commit withheld; resume completes the commit exactly once
- **Host(s)**: Olamnit
- **Method**: three timing-based mid-flight kills (1.2s/2.0s/2.6s) never landed inside the row-write→commit
  window (each died pre-row, confirming write atomicity — recorded in kill-resume.md). The withheld-commit
  state was then produced **deterministically** with real git contention: `.git/index.lock` held while
  `buildkit-marathon checkpoint --step us4-step-3 --paths evidence/marathon/run.md` ran.
- **Command + output (withhold)**: checkpoint exited 1 with `git add of scoped paths failed: ... index.lock: File exists`,
  yet the durable row landed FIRST: checkpoint **8** `status: complete`, `committed_paths: [evidence/marathon/run.md]`,
  **`commit_sha: None`**, `pushed: false` — exactly the durable-first/commit-withheld state.
- **Command + output (re-drive)**: lock removed; fresh `buildkit-marathon.exe resume --json` →
  `recovery.redrive_checkpoints: [checkpoint 8 (commit_sha None)]`, `repair.redriven: [8]` — the scoped commit
  was completed by the resume as `6e46ad55` ("step-3 checkpoint durable-first, commit withheld by index.lock (T024)").
- **Exactly-once**: git log shows ONE new commit for the scoped path; working tree clean; a second
  `resume --json` reports `redrive: []`, `redriven: []` — idempotent, zero duplication.
- **Verdict**: PASS
- **Date**: 2026-07-08
