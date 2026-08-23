# M01 — `/bk-marathon` → `/bk-flow` migration plan

**3rtask run** `20260823T140508Z-227d` · task-type **plan** · feature `tidy-up-branches-worktrees-olamnit` · marathon item `mitem-01M0Q4KS3JEYDA0PB1NZ9PDVHN`
**Method** `method-20260823T140508Z-227d` (9 frozen elements, 5 engineer-open ESCALATEs) · **Critic** codex (cross-provider) · 3 blind builders / 3 pairwise-disjoint slices / 0 independence violations
**Advisory only — no cutover executed, no deploy mutation, no push/merge.**

---

## 0. Headline finding — the "migration" is an INTEGRATION, not a replacement

The Critic's E10 parity set-difference over marathon's 10 canonical run-harness capabilities returned:

- **parity_met: [] (zero)**
- **GAP: all 10** — `cap.backlog_park, cap.backlog_sequence, cap.step_intake, cap.step_expansion_lineage, cap.resume_from_state, cap.crash_recovery, cap.verification_trace, cap.approval_gate, cap.discharge_gate, cap.durable_state`

Critic verbatim: *"bk-flow is an advisory board-to-pipeline bridge (poll/claim/open/report/takt), NOT a marathon run-state manager with backlog, checkpoint, trace, gate, discharge, or catalog persistence verbs."*

The decisive mechanical evidence: **bk-flow's `open` verb *binds a claimed WP to a feature + marathon run*** (builder-1, cited from `bk-flow open --help`). bk-flow does not *replace* marathon — it sits **in front** of it: a CRDT board dispatches a work-package → `bk-flow claim` → `bk-flow open` **opens a marathon run** → the marathon harness runs the durable pipeline → `bk-flow report` marks the WP done → `bk-flow takt` reads the marathon run's per-phase takt.

**Therefore "retire `/bk-marathon`, cut over to `/bk-flow`" is a category error.** The engineer directive ("auto-upgrade + verify build green + verify ALL /bk-* tools work + evaluate safety before cutover") is coherent only as: **adopt `bk-flow` as the board→pipeline dispatch front-end that opens marathon runs**, keeping `bk-marathon` as the durable run harness underneath. There is no marathon capability to decommission.

---

## 1. Go/No-Go readiness — per prerequisite

Two dated readings are reconciled: the **2026-08-20 readiness doc** (builder-3, blind to this session) and **2026-08-23 session re-verification** (curator-held disjoint fact).

| Prereq (readiness doc §6) | Doc 2026-08-20 | Session 2026-08-23 | Verdict |
|---|---|---|---|
| Canonical size scheme active | ✅ GO | — | **GO** |
| Per-stage token ledger | ✅ GO | — | **GO** |
| Step start/complete timestamps | ✅ GO | — | **GO** |
| `takt` projection emitted per step | ❌ blocking | ✅ `marathon takt` works, reconciles 4/4 sources | **GO (now met)** |
| `bk-flow` reachable on PATH | ❌ blocking | ✅ on PATH (`Python313/Scripts/bk-flow`), all subcmds present | **GO (now met)** |
| Step board integrity (grow-only + delimiter) | ❌ blocking | ⚠️ `expand --steps` now refuses `\|`; comma-split + grow-only (no void verb) remain | **PARTIAL / NO-GO** |
| Reproducible phase-exit gates (Critic determinism) | ❌ blocking | ⚠️ unverified; this very run showed verdict churn under append-only re-review | **NO-GO** |

**Net**: the doc's "4 of 7 unmet, 10–20 sessions out" is **stale** — 2 of the 4 blockers are now cleared. **2 blockers remain**: board integrity (§5.1) and reproducible gates (§5.2). Migration readiness has moved from *4 unmet* to **2 unmet**, but is **not GO** for full adoption until those two are fixed (the doc's own §7 sequence: fix integrity FIRST, before takt data accretes on a corrupt board).

---

## 2. Cutover-safety gate (E11) — evaluated now → **NO-GO**

Binary AND of five slice-traced fields:

| Field | Value now | Evidence |
|---|---|---|
| `readiness_all_green` | **false** | §1 — 2 prereqs still NO-GO |
| `fleet_quiescent` | **false** | 11+ live fleet sessions; live registry lock hit twice this session (PID 24200, PID 1988); `backlog --json` blocked |
| `rollback_path_proven` | partial-true | `deploy --version <older-V>` re-pins to prior version; re-deploy is a no-op; targets never hard-deleted — BUT precondition: older V must still be installed (`tidy --apply` can prune it) |
| `all_13_items_preserved_or_ported` | **N/A** | no port needed — marathon stays; its runs are the system of record |
| `parity_gaps_zero_or_waived` | **false** (gap=10) | §0 — but see waiver note |

**Gate result: NO-GO for cutover.** Two independent reasons: readiness not green, and fleet not quiescent. `buildkit-deploy latest` mutates the shared per-home registry under a single advisory lock; running it while 11+ siblings hold that lock risks corrupting shared state (builder-2/3 corroborated — the one corroborated cross-slice fact).

**Parity-gap waiver (E11, ENGINEER-only):** the gap=10 is *expected and benign* once M01 is read as integration (§0) — bk-flow is not meant to provide those caps. Setting `parity_gaps_zero_or_waived = true` on that basis is an **engineer waiver** (id + rationale), not a builder/curator call. Recommended rationale: *"bk-flow complements marathon; the 10-cap gap is by design, not a regression."*

---

## 3. Reversibility / rollback (E9) — proven mechanics

From `bk-deploy` SKILL.md (builder-3, cited):
- **Revert a target to a prior version**: `buildkit-deploy deploy --version <older-V>` re-pins the target. Precondition: `<older-V>` still installed (discover via `buildkit-deploy version`, read-only).
- **`latest` is forward-only** — advances to newest CalVer, cannot revert.
- **Re-deploy of a complete version is a no-op** (idempotent).
- **Targets are never hard-deleted** — vanished→`missing`, de-install→`deinstalled` tombstone, re-deploy reactivates the same canonical-path row.
- **Risk**: `tidy --apply` can prune an orphaned older version (keep-current + keep-last-N floor protect the pinned + recent ones) — so **do not `tidy --apply` the rollback target between cutover and sign-off.**

---

## 4. The missing `/bk-flow` skill (E2, E13) — a hard prerequisite

There is **no `.claude/skills/bk-flow/` directory** (builder-1, verified). `/bk-flow` cannot be invoked as a slash-command; only the CLI exists. An authoritative `SKILL.md` must be authored covering: the advisory "never invokes a pipeline command" identity; the shared `--root` default (`R1 sched_root / coop/sched`); `--actor` resolution (flag / `SCHEDULER_ACTOR` / `HOST/lane`); `--dry-run` on the mutating verbs (claim/open/report); `open`'s first-time `--feature` requirement; the cross-repo `--repo` refusal; and the read-only (poll/takt/version) vs mutating (claim/open/report) split. Until then the contract is CLI-introspected, not documented.

---

## 5. Recommended SAFE / IDEMPOTENT / REVERSIBLE runbook (advisory — do NOT execute cutover unattended)

**Phase A — prerequisites (no shared-registry mutation; safe now):**
1. Fix step-board integrity (§5.1 of readiness doc): `expand --steps` comma-escaping + add a step-void verb. *(engineer-scheduled dev work)*
2. Establish reproducible phase-exit gates (§5.2): pin Critic determinism or make the phase-exit gate non-Critic. *(engineer-scheduled)*
3. Author `.claude/skills/bk-flow/SKILL.md` (§4). *(safe, additive)*
4. Record the ENGINEER parity waiver (§2) — id + rationale.

**Phase B — cutover (shared-registry mutation; GATED):**
5. **Quiesce the fleet** — no other buildkit session may hold the deploy-home lock. (No enumerated "safe-while-live" condition exists in `bk-deploy` — builder-3 gap; E8 escalate. Treat "fleet quiescent" as mandatory.)
6. Snapshot the rollback target: `buildkit-deploy version` → record current pinned version; ensure it stays installed (no `tidy --apply`).
7. `buildkit-deploy latest all` (or `latest <repo>`) — idempotent; already-latest is a no-op.
8. Verify build green + verify ALL `/bk-*` tools resolve and run (per directive).
9. Smoke `bk-flow poll --actor olamnit` → `claim` → `open` (binds a marathon run) → `report`, using `--dry-run` first on each mutating verb.

**Rollback (any step 7–9 failure):** `buildkit-deploy deploy --version <recorded-older-V>` (§3).

**Idempotency**: every Phase-B mutation is a documented no-op when already in the target state (re-deploy no-op, `latest` already-latest no-op, `--dry-run` writes nothing).

---

## 6. Open ESCALATEs — the ENGINEER's to resolve (curator must not)

**Planning-method ESCALATEs (5):**
- **E2** — scope of `bk-flow` doc authoring (constrain to CLI-observed contract?).
- **E4 / E10** — the CAP-to-verb crosswalk requires *some* semantic judgement; the parity gap is mechanical only under a fixed crosswalk. *(Resolved in practice: gap=10, integration reading — but the mapping authority is yours.)*
- **E6** — marathon states no explicit "no-data-loss" guarantee for intact/paused/discharged; no formal "paused" state defined.
- **E8** — no enumerated "safe-to-mutate-while-live" condition exists for the shared deploy-home; is fleet-quiesce the accepted gate?

**Execution ESCALATEs (4):** citation-truncation artifacts of the curator's compact adjudication view (`deploy` idempotency detail, claim/open/report mutating-label, `--root` shared-by-all, durable checkpoint detail) — non-substantive; full citations in the builder claims resolve them.

**Recommended engineer decisions:** (a) accept integration reading + record parity waiver; (b) treat fleet-quiesce as the mandatory cutover gate; (c) schedule the 2 remaining readiness fixes before Phase B; (d) authorize authoring `/bk-flow` SKILL.md.

---

## 7. Bottom line

- **M01 is not a replacement; it is adopting `bk-flow` as marathon's board→pipeline front-end.** `bk-marathon` stays.
- **Cutover verdict now: NO-GO** — 2 readiness blockers remain + the fleet is live. Not fleet-safe today.
- **Readiness improved**: from 4 unmet (doc) to **2 unmet** (session-verified). Roughly 2 focused fixes from Phase-B-ready, not "10–20 sessions."
- **Reversible**: `deploy --version <older-V>` is a proven rollback; protect the target from `tidy`.
- **The plan is advisory** — Phase A is safe to start now; Phase B waits for fleet-quiesce + your go.
