# Tidy-up CRDT workplan — 2026-08-22 (supersedes the 2026-08-20 plan)

**Marathon**: `mrun-20d9230f767b` · **Feature**: `078-verification-receipts` · **Host**: GAVRIELLA
**Sizing**: `default` — nano=1 · micro=3 · mini=7 · midi=11 · maxi=17 · saga=35

> **Why this file exists.** Marathon steps are durable in the catalog, but a step's *content* lives
> only in its name, and `expand --steps` is comma-delimited with no escaping — which on 2026-08-20
> truncated two steps and merged two more, permanently, because steps are grow-only with no delete
> verb. This file is the **authoritative content**; marathon steps are the **state machine**.
> **Where a step name and this file disagree, this file wins.** Step names below are deliberately
> comma-free.

The 2026-08-20 plan (`tidyup-crdt-workplan-2026-08-20.md`) is **superseded**: W02/W03/W04/W08/W09/
W10/W12c/W13/W14 are delivered, and W11 was overtaken — another lane cut the release. What follows
is measured today, not carried forward.

---

## Ledger

| ID | Step | Size | Pts | State | Evidence / blocker |
|---|---|---|---|---|---|
| X01 | Preserve the 078 local-only commit | nano | 1 | ✅ DONE | `315e3be5` existed on ONE local branch, no remote, no tag; now on `origin/078-verification-receipts` |
| X02 | Delete provably-contained local branches | nano | 1 | ✅ DONE | 10 → 5 heads; each deleted branch verified 0 ahead of `origin/develop` |
| X03 | Drop `058` under R2 | nano | 1 | ✅ DONE | archive tag verified pushed AND byte-identical to tip before delete; tip recorded in a tracked file; origin heads 19 → 18 |
| X04 | W07b triage review of `050`/`059` | mini | 7 | ✅ DONE | `docs/handover/w07b-050-059-triage-review-20260822.md` |
| X05 | Scheduler: onboard + unstick + allocate | mini | 7 | ✅ DONE | 35d × 3×8h = 105 slots / 840h; `ready` 4 → 6; 078 allocated + feature-bound |
| X06 | Roadmap round 30 | micro | 3 | ✅ DONE | reconcile + import + dedupe (115 scanned, 0 dup groups) + export 20/116/3761; both publish legs OK |
| X07 | **`067` private-key rotation** | maxi | 17 | 🔴 **ENGINEER — URGENT** | keys are **PUBLIC on `main`**, not merely on a branch — see below |
| X08 | Merge `067-qr-link-provisioning` | midi | 11 | 🔴 BLOCKED | gated on X07; 26 commits ahead |
| X09 | Merge `066-wave6-consolidation` | maxi | 17 | ⏸ HELD | 23 ahead; CLAUDE.md content conflict + modify/delete — a review, not a tidy-up |
| X10 | `050`-vs-`059` survivor ruling | midi | 11 | 🔴 ENGINEER | the question as posed is unanswerable — they are complementary |
| X11 | Backfill 59 tag GitHub Releases | mini | 7 | ⛔ BLOCKED | `gh release create` refused by the auto-mode classifier |
| X12 | Make `buildkit release` publish the Release | midi | 11 | 🔴 ENGINEER | root cause of X11; the gap re-opens on every cut |
| X13 | 4 claimed-but-never-ready WPs | micro | 3 | 🤝 PEER | ariellas ×3, olamnit ×1 — not mine to transition |
| X14 | `/bk-implement` 078 through to close | saga | 35 | ◐ PART-DONE | 3 of 66 tasks shipped; **54 of 66 target `bk:` (buildkit)** — blocked on the two-repo ruling |
| X15 | 078 US4 harness slice (T045/T046/T047) | midi | 11 | ✅ DONE | shipped in **v2026.08.22.1**; 2 codexreview rounds, 8 findings, all closed |
| X16 | Release `v2026.08.22.1` | micro | 3 | ✅ DONE | tag on `origin/main`, verified by content |
| X17 | Roadmap rounds 31 + 32 | micro | 3 | ✅ DONE | both publish legs OK each round |

**Delivered: 37 pts** (X01–X06 · X15 · X16 · X17) — **Remaining: 95 pts**, of which **56 are
engineer rulings** (X07 · X10 · X12), **7 permission-blocked** (X11), **3 peer-owned** (X13), and
**29 agent-executable** (X09, plus X14's `gn:` remainder; X08 gated on X07 and X14's bulk on the
two-repo ruling).

### What shipped in v2026.08.22.1

078's three glpnet-local US4 tasks — applying 078's own thesis to the merge gate. The staleness
guard **immediately caught a live stale binary** (built 08-16, source 08-19) and the suite honestly
reported `554/552/2 Unsearchable 1`: **seven checks in the 561/559/2 baseline were coming from a
binary older than its own source.** After `dotnet build` the suite returned to `561/559/2/0/0` and
**the two Section T failures survived the rebuild — they are real defects, not stale-binary
artifacts**, unlike the wave-13 077 case.

Two review rounds raised 8 findings, all correct, all closed. The sharpest: the summary printed
*"N group(s) did not run"* and then **exited 0** — the truth on stdout, a lie to the caller. And
**B2 was a regression I introduced** while rewiring Section U, caught by review rather than by me.

---

## 🔴 X07 is the most urgent item in this repo

The 2026-08-20 framing was that an archive-tag push *would* republish `glpquick.key`. That
understates it. Measured today:

- `glpquick.key`, `.pem`, `.pfx` are in the history **reachable from `origin/main`**, not only
  from a feature branch.
- The repository is **PUBLIC**.
- They entered at `94fbe87d` — the **`v2026.07.09.1` release commit** — and are reachable from
  **23 of 65 version tags**.
- `glpquick.key` opens `-----BEGIN PRIVATE KEY-----`. It is real key material, exposed for 44 days.
- The files were untracked on 2026-08-10 (`9382981c`), and `.gitignore` **does** carry
  `glpquick-cert/` (line 114 on both `main` and `develop`), so **recurrence is prevented** — but
  history is unchanged and history is what is published.

Rotation is therefore not preventive; it is overdue remediation. Ruling R1 already said *rotate
first* — that ruling is correct and is now the critical path for X07/X08.

## Facts that must not be re-derived wrongly

- **glpnet has ZERO linked worktrees.** Re-verified today: `git worktree list` returns only the
  main tree and `.git/worktrees` does not exist. Every `wt-*` / `bk-wt*` path on `D:` resolves to
  `D:/BSTDEV/research/buildkit/.git` or `D:/BSTDEV/research/.git`. **Deleting them as glpnet
  cleanup destroys another repo's worktrees.**
- **`050` and `059` are complementary, not redundant.** `059` holds the compiler / type-checker /
  bytecode stack plus a Lean proof; `050` holds the QUIC transport and link-lifecycle layer.
  Overlap is 15 files. Picking one discards a subsystem.
- **Both look far bigger than they are.** 41 000-line generated roadmap-sync JSON exports dominate
  both diffs; `050` additionally carries **679 committed `.obj` build artifacts**.
- **Never quote a branch count without naming the ref.** Local heads 10 → 5 today; origin heads
  19 → 18. Different objects, different numbers.
- **`origin/develop` is not 55 or 69 ahead of `main`** — that figure is stale. Another lane cut
  `v2026.08.21.1`/`.2`; after `v2026.08.21.3` and round 30 it is a handful of commits.

## Binding safety rules (unchanged, still in force)

1. **No deletion may claim a reflog recovery window.** 54 of 77 per-branch reflogs measured
   zero-byte on 2026-08-20. Every delete is class **C2**: preserve, verify the preservation, then
   delete.
2. **An archive tag is preservation only when verified.** Tags were cut 08-20; branches can move.
   Verify tag-commit == branch tip **at delete time**, and that the tag is on `origin`. Done for
   `058` today; all five surviving archive tags re-verified IDENTICAL.
3. **A git bundle is NEVER content preservation** — it packs reachable objects, not untracked bytes.
4. **The merge gate is local only.** CI exists (5 CodeQL `Analyze` jobs) but **no CI runs
   `test/run_all_tests.sh`**. Baseline **561 / 559 passed / 2 failed** (the two known 064 Section T).
   Re-verify after every merge.

---

*Authoritative content for marathon `mrun-20d9230f767b`. Update this file, then reflect state in
the marathon; never the reverse.*
