# Quickstart — Driving a refined marathon

The refined harness is **workload-agnostic**: the stage list is data you supply, not a fixed vocabulary.
State lives in a **per-run isolated store outside the repo**, owned by a background keeper. Everything below
has a 1:1 library equivalent ([`contracts/library-api.md`](./contracts/library-api.md)).

> Per-run store path must be on **NTFS/ReFS** (the `_check_data_dir_filesystem` guard, exit 64). The default
> marathon root is a guaranteed-NTFS user-level path (e.g. under `C:/pglite/marathon/`); override with
> `--data-dir` if needed.

## 1. Register a run with your own stages
```
codeconv marathon register --run mywork-2026q3 --title "My work" \
    --stages discover,design,build,verify --budget 500000 --budget-unit tokens
```
Resume now reports `done=0/4`, next action `run discover`. (Empty `--stages` is legal → next action
`register stages`.)

## 2. Keeper comes up automatically; check health
```
codeconv marathon keeper start --run mywork-2026q3     # spawns the per-run isolated store, publishes endpoint
codeconv marathon doctor --run mywork-2026q3 --json    # endpoint, active store, last-seq, open escalations, budget
```

## 3. Work a stage → checkpoint (a scoped commit boundary)
```
codeconv marathon stage-start --run mywork-2026q3 discover
# ... do the work, then checkpoint ONLY this block's paths ...
codeconv marathon checkpoint --run mywork-2026q3 discover \
    --paths src/discover.py,tests/test_discover.py --budget 38000 -m "discover: initial sweep"
codeconv marathon status --run mywork-2026q3
#  marathon mywork-2026q3 | done=1/4 | open=0 | budget=38000tokens | next=run design
```

## 4. Grow the run mid-flight (US1)
```
codeconv marathon append-stage --run mywork-2026q3 harden
codeconv marathon status --run mywork-2026q3
#  ... | done=1/5 | ...        # total grew from 4 to 5, reported against the new total
```

## 5. Capture emergent work → mini-pipeline (US2)
```
# A blocking missing-prerequisite discovered while in 'build':
codeconv marathon capture --run mywork-2026q3 --kind missing-prerequisite \
    --title "schema migration must land first" --blocks build
codeconv marathon resume --run mywork-2026q3 --json
#  next_action: "run mini-specify for item-1"   # routed AHEAD of 'build' (5 mini-stages inserted before it)
```
Advance each mini-stage explicitly (advisory / default-deny — the harness never auto-advances):
```
codeconv marathon stage-start --run mywork-2026q3 item-1:mini_specify
codeconv marathon checkpoint  --run mywork-2026q3 item-1:mini_specify --paths .../items/1/spec.md
# ... mini-clarify, mini-plan, mini-tasks, mini-analyze ...
```
When `mini_analyze` checkpoints, item-1 flips to `done`; its planning artifacts in
`<store_root>/items/1/` feed the marathon's single `implement` stage (no per-item implement).

## 6. Survive a crash (US3)
```
# (process killed abruptly mid-run, leaving stale lock/lifecycle residue)
codeconv marathon resume --run mywork-2026q3      # recovers the store automatically — no manual deletion
# A second concurrent writer is refused with a message distinct from a recoverable stale condition.
```

## 7. Preserved 024 strengths (US5) — all over the new stage model
```
codeconv marathon gate --stage <id> --approve --by gabi      # per-stage approval; resume short-circuits if approved
codeconv marathon rerun --stage <id> --subagent design-b     # isolated re-run; reports untouched siblings
codeconv marathon trace --run mywork-2026q3 --subject design --input '{"k":1}' --accept
codeconv marathon reconcile --run mywork-2026q3              # PGLite ↔ JSON mirror; fast-forward or escalate fork (exit 2)
```
Budget ceiling: when advancing would exceed `--budget`, the harness halts and writes a `budget_exceeded`
escalation (exit 2) rather than overrunning (zero overruns).

## 8. Finalise + graceful shutdown
```
codeconv marathon finalize --run mywork-2026q3          # only when every (current) stage is complete
codeconv marathon keeper stop --run mywork-2026q3       # flushes; next start needs no recovery
```

## Resume after context loss (SC-008)
`codeconv marathon resume --run <id>` is computed **solely from durable rows** — it returns the identical
position whether you have full conversation context or none. This is the restart/resume backbone the
CLAUDE.md protocol points at.
