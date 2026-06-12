# Quickstart end-to-end validation — T057 (2026-06-12)

Scratch run `qs-scratch`, store root `D:/pglite/marathon/quickstart-scratch`
(off-repo NTFS; `prereq-patterns/` junction-linked in). Every quickstart step
executed against the real per-run keeper/bridge — **all green**.

| Quickstart § | Step | Observed | Verdict |
|---|---|---|---|
| 1 | `register` 4 stages, budget 500000 tokens | `status: in_progress`; resume `done=0/4`, `next=run discover` | ✓ |
| 2 | `keeper start` + `doctor` | endpoint published (port 61941); reachable; `active_store: primary`; budget headroom 500000 | ✓ |
| 3 | `stage-start discover` + `checkpoint --remaining [] --budget 38000` | `done=1/4 \| budget=38000tokens \| next=run design`; paths informational without the standing grant (no repo commit) | ✓ |
| 4 | `append-stage harden` | total grew 4→5, reported against the new total | ✓ |
| 5 | `capture --kind missing-prerequisite --blocks build` | item-1 created, `items/1/` artifact dir; after `design` completed, `next=run mini-specify for item-1` (routed ahead of `build`); 5 minis advanced; `mini_analyze` checkpoint flipped item done; `done=7/10`, `next=run build` | ✓ |
| 6 | crash: `taskkill /F` the live bridge pid | next `resume` recovered automatically (no manual deletion), `done=7/10` intact | ✓ |
| 7 | `gate --stage build --approve --by gabi`; `rerun --stage discover --subagent design-b`; `trace --subject design --accept`; `reconcile` | approval recorded (id 1); rerun isolates `to_run=['design-b']`, `untouched=[]`; trace `refine_seq=1, accept`; reconcile `in_sync` | ✓ |
| 8 | complete build/verify/harden → `finalize` → `keeper stop` | `done=10/10`, `next=finalise run`; `status: finalized`; keeper stopped and the sidecar was unlinked (next start needs no recovery) | ✓ |

## Findings (doc fixes applied to quickstart.md)

1. **Store-root provisioning gap**: the keeper resolves the bridge script as
   `<store_root>/prereq-patterns/pglite/pglite_bridge.mjs` (it spawns with
   `repo_root=store_root`), so a fresh store root needs the repo's
   `prereq-patterns/` junction-linked (or symlinked) into it — the same wiring
   the test fixture (`conftest._link_prereq_patterns`) uses. The quickstart
   did not mention this; without it `register` fails with "unified bridge
   script not found". A provisioning note was added.
2. **§5 example nit**: the example shows `next_action: "run mini-specify for
   item-1"` immediately after capture, but with `design` still pending the
   resume-position contract (rule 2 — first non-complete stage in order)
   correctly surfaces `run design` first; the minis route ahead of the
   *blocked* stage (`build`) only. The example now completes `design` first.

No code defects found; the implementation matched `contracts/resume-position.md`,
`keeper-lifecycle.md`, and `cli.md` at every step.
