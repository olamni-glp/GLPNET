<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Gate ledger — wave-6 (boundary: not-closed snapshot 20260803T150440Z, 18 items)

**Contract**: `contracts/gate-ledger.md`. **Evidence sources**: `evidence-inventory.md`.
**Created**: 2026-08-03T21xx. Rows update in the same commit as the event they record.

## Gates

| gate_id | kind | state | evidence | blocks |
|---|---|---|---|---|
| G1 | ship-state (064) | open | 064 ship-ready (551/551, codexreview capped@5-escalations-only, 51be73c5); ship+close await engineer keystroke @ v2026.08.03.2 | ITEM-07 disposition; T022 |
| G2 | track (065) | open | specs/065 specified @ d2ea81e9; mrun-7939e12b5b70; its FR-008 5-escalate gate cascades | ITEM-11 disposition; T025/T026 |
| G3.R1–R5 | ruling (3rtask fa8a) | open | evidence-inventory.md R1–R5 | 065 stories (cascade via G2); any wave story touching the audited seams |
| G3.R6–R12 | ruling (064 review) | open | evidence-inventory.md R6–R12 | T022 (R6/R7 replay semantics); T023 (R6–R12 as 059-acceptance caveats) |
| EXT.ariellas | external-ownership | **SATISFIED 20260804T163500Z — independently verified** (ship + close receipts both landed; gate CLOSED). **T023 is NOT thereby runnable** — see the gate-arithmetic note below: tasks.md has T023 ⛔GATE(T022) and T022 ⛔GATE(T021, EXT.ariellas-US2, **G1**), so T023 now blocks solely on **G1 (our 064 ship keystroke)** plus the G3.R6–R12 rulings. The wave-8 restart brief's shorthand "their receipts clear EXT.ariellas → unblock T023" was one gate short | **SHIP RECEIPT v2026.08.04.1**: tag on origin `a3ac99d7` → commit `71f63f8c`; verified from this host — `git ls-remote --tags` shows the tag, `71f63f8c` **IS** `origin/main`, and `git merge-base --is-ancestor origin/main origin/develop` PASSES (back-merge landed, main fully contained in develop). PRs #130 (feature→develop) / #131 (release→main) / #132 (back-merge) all merged. Their ship verification gate 4/5 — the single MISS is `cli_reinstalled` (their host's CLI stale at 2026.7.30.1, exit 5 = local-tooling warning, not a workflow failure; nothing on the wire affected). **CLOSE RECEIPT**: retro `retro-064-post-wave-gap-closure-20260804T162757Z936b7b` persisted + committed `2cc238e5` on develop; action reconciliation 0 stale / 0 offers; roadmap advanced promoted→shipped→released→closed; export 18/99/2740 published. Prior evidence retained: | 064-post-wave-gap-closure seams 1–5 receipts (153205Z); carve-out CONFIRM 153920Z; implement receipts 459be1b2 (210601Z); ship (their T041) pending. **20260804T010500Z (read late — see poll-filter defect below)**: their T040 codexreview COMPLETE (run 20260803T214953Z, capped@3; 43 merged plan items, 37 finding identities, 3 refuted, 34 surfaced, 11 fixes, a third adversarial pass caught 2 fix-introduced regressions, both fixed); post-fix sweep REPL 547/546 (the single Section-I failure was a mixed-OTP glp_gleam/build collision, 0 failures in isolation after clean rebuild), Gleam 625, C# 172/47/73 + 815 tree-wide; merge conflict with olamnit's develop resolved via `import --rebuild-manifest` (merge 0d2739b1) → **PR #130 MERGEABLE/CLEAN**. Their ship needs the same interactive engineer approval ours does ⇒ the receipts were never imminent. Re-polled 153153Z | T021/T022 receipt consumption; ITEM-04; verification caveat on ITEM-12..18 closures |
| EXT.olamnit | external-ownership | **satisfied 20260804T153401Z** | wave3-close broadcast ACK (BROADCAST-20260803T120017Z + chase 142207Z) landed after ~27 h: olamnit box `[x 20260804T153401Z]`, receipts of understanding recorded (060 shipped+closed v2026.08.03.1 via PR #127/#128/#129 @ fb980c51; D-9 run-termination-barrier + connector-dial-retry NORMATIVE; `{exit_on_close,false}` BEAM gotcha; CalVer ledger). They corrected their earlier 140315Z "nothing owed" — their cursor was behind the broadcast. ACK-RECEIVED given for their 065/066 UPDATE at 153550Z | — (thread closed; no longer blocks) |
| OPS.buildkit-repo | operational | open | D:\bstdev\research\buildkit on foreign branch fix/opskit-pglite-package-dir with 4 modified .specify files (another session's WIP, 2026-08-03 ~2150Z) — not this session's to resolve; engineer word needed to branch/stash there | ITEM-01, ITEM-02 (US2 implementation lands in the buildkit repo) |

## Items (18 rows = the 150440Z snapshot)

| item_id | group | disposition_path | state | blocked_by | evidence |
|---|---|---|---|---|---|
| ITEM-01 atomic-toolchain-installs-venv-swap-post-install-smoke | US2 | story | implemented (parked: engineer landing) | engineer merge/ship (buildkit side) | UNPARKED by engineer directive ~2200Z (R5 direction: junction-swap proceeds, validation via post-flip verify+rollback — encoded). Implemented on buildkit branch feat/atomic-toolchain-installs @ 554836f6 PUSHED (ship/atomic_install.py: fresh venv + junction flip + smoke + rollback; wired at the only pip seam = ship/release reinstall; deploy installer already atomic — documented). Tests 11/11 new + ship pkg 204/204. Closes after the branch lands |
| ITEM-02 batch-roadmap-advance-calver-version-dir-normalisation | US2 | story | implemented (parked: engineer landing) | engineer merge/ship (buildkit side) | Implemented on buildkit branch feat/batch-roadmap-advance-calver-normalisation @ 634d4a0a PUSHED (multi-id + --from/--all one-window batch; CalVer normalize at read/compare seams + reuse-before-create; nothing deletes dirs; requested-spelling deviation documented). +13 tests green; **2** (corrected 20260804T153900Z, was "1") pre-existing baseline failures reproduced on clean base a073e82d: `tests/deploy/test_chain_apply.py::test_ref_only_head_row_halts_apply_defensively` and `tests/roadmap/test_lifecycle.py::test_migration_0014_widens_an_existing_4state_catalog` — neither is a branch regression. Scoped gate 20260804: `tests/roadmap/` + `tests/deploy/` 350 passed / 1 failed / 11 errors, where all 11 errors were pgdb-lock contention from a concurrent local full-suite run (clean re-run of those 12: 61 passed / 2 failed = the two baseline failures). Ship attempted repeatedly and **refused by the local auto-mode classifier** (not by buildkit) — awaits the engineer keystroke; env gaps fixed en route (pytest-rerunfailures + pytest-timeout were undeclared/missing; worktree src must be on PYTHONPATH or tests import the main checkout's foreign branch). Closes after the branch lands |
| ITEM-03 glp-runtime-consol | US3 | story | **closed — DISPUTE WITHDRAWN 20260804T180120Z (olamnit SHIPPED it as v2026.08.04.2; row now reflects delivered work)** | — | **RESOLUTION**: olamnit shipped `065-glp-runtime-consol` as **v2026.08.04.2** — verified from this host: tag `bc76bfec` → commit `fbcf7667`, tag is on `origin/main`, `origin/main` contained in `origin/develop`; PRs #133/#134/#135. Contents = US1 antlr4 spike (SC-001 7/7, REPORT.md GO-WITH-CONDITIONS) + US2/Scope-B removal. My closure was **premature when made** (the row read `closed` while Scope A was live) but is **no longer wrong** — the claimed work is now genuinely delivered. Dispute withdrawn, not escalated. **The narrower point stands and is kept**: the closure was correct only by timing luck; had the §1.14 revival gone the other way the row would have stayed wrong with no host able to repair it — see the one-way-door fleet finding. Prior evidence retained: | runtime-consol-inventory.md: (B) abandon.cs tombstoned (build 0 errors, suites green @ develop baseline 165/184), (A) superseded by Option-B rider; roadmap advanced → closed. **DISPUTE**: (B) superseded upward — olamnit executed a COMPLETE REMOVAL (abandon.cs + live Dart abandon.dart + dead import in runtime.dart + inventory reconcile; engine 0-err, dart analyze clean, REPL 547/547, branch 066-abandon-stub-cleanup @ 6c9cb8f1) which subsumes the tombstone, so (B) stands. (A) OVERTURNED — see ITEM-06: the antlr4 spike was §1.14-APPROVED by Gabi+Udi 20260804, one day after the rider this closure relied on, so a feature with live in-flight work sits at `closed` because of MY advance. ariellas FLAGged the closed state 153600Z suspecting a CRDT state-regression; ANSWERED 153700Z — single-host deliberate origin, NOT a regression, no defect to open. Repair proposed (not executed): olamnit re-advances + publishes; I import. I do not own the feature and will not reverse a peer-rider disposition on peer testimony |
| ITEM-04 post-wave-consolidation-verified-gap-closure-repl-engine-full-gleam | EXTERNAL | external-gate | **disposed — closed by owner 20260804T163500Z** | — | ariellas' feature (mrun-35df7ddfe4ec); their receipts disposed it: shipped **v2026.08.04.1** (tag verified on origin from this host) + /bk-close complete (retro `…20260804T162757Z936b7b`, 0 stale actions), roadmap advanced promoted→shipped→released→closed by the owner. Nothing for wave-6 to advance — the disposition is theirs and it is done |
| ITEM-05 qr-link-provisioning | US3 | story | parked (graduation proposal) | engineer decision | Brief re-read 2026-08-03: MANDATORY first-class security hardening (Gabi correction 2026-07-08 — trunk key never rendered; short-lived per-device derived credentials, encrypted QR payloads, audit+revocation as preconditions) + Android consumer pairing (olamnit-assistant) ⇒ PROPOSAL: graduate to its own /bk-specify pipeline per the brief's own hand-off, not an in-wave task; wave-6 records deferred-to-own-feature once the engineer confirms |
| ITEM-06 antlr4-shared-grammar-spike | US4 | story | **RESOLVED 20260804T174506Z — the supersession is STALE; revived, owned by olamnit/065** (roadmap state change still engineer-gated, see tooling gap) | engineer (roadmap re-open mechanism only — the *substance* is settled) | **RESOLUTION**: olamnit confirmed the discriminator I asked for — **§1.14 approval dated 20260804, authority Gabi + Udi** (owner + language authority, DISCIPLINE §1.14 / Constitution IV-a), **scope: faithful, additive `Glp.g4` describing the EXISTING accepted GLP syntax, no syntax change, under their 065 line**. It post-dates ariellas' 210601Z "antlr4 superseded (G5)" rider by one day and is the later, higher authority ⇒ it **revives Scope A**. Already implemented on their side: `Glp.g4` + generated C# parser + coverage harness, **SC-001 7/7 parity**, `REPORT.md` GO-WITH-CONDITIONS (SC-002 IL-parity deferred to a PREP feature), committed `365e64cc`/`ce44ba0b` pushed. My prior evidence retained: | ariellas 210601Z: "antlr4 superseded (G5)" under the Option-B re-scope ruling; roadmap rider executed with their implement-complete receipts @ 459be1b2. **CONTRADICTION**: olamnit 20260804T153401Z reports the antlr4 shared-grammar spike **§1.14 gate APPROVED (Gabi + Udi, 20260804)** with Glp.g4 authoring next → C# parser gen → coverage + IL-parity harness → REPORT.md. The approval post-dates the rider by one day and is higher authority. Open question put to ariellas 153550Z: was G5 supersession **064-scoped** (→ ledger wording fix only) or **repo-wide** (→ two engineer rulings genuinely collide)? NOT re-disposed from this side — a rider-recorded disposition is not reversed on a second peer message |
| ITEM-07 durable-listener-service-box (064) | EXTERNAL | external-gate | parked | G1 | own track; ship-ready; engineer keystroke pending |
| ITEM-08 ynet-human-memorable-decentralized-naming-resolver | US6 | triage | pending | G2 | roadmap: captured |
| ITEM-09 ynet-mobile-background-battery-budget-scheduling-policy | US6 | triage | pending | G2 | roadmap: captured |
| ITEM-10 buildkit-coordination-optimisation-gepa-dspy | US6 | triage | pending | — | roadmap: captured |
| ITEM-11 ynet-consolidation (065) | EXTERNAL | external-gate | parked | G2 | own track (specs/065); 5-escalate gate |
| ITEM-12 glp-gleam-compiler-and-loader | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205504Z actor-ce1ef684db6c; same caveat as ITEM-06 |
| ITEM-13 glp-gleam-bytecode-runner | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205459Z; same caveat |
| ITEM-14 glp-gleam-repl | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205454Z; same caveat |
| ITEM-15 glp-test-corpus-port-and-runner | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205509Z; same caveat |
| ITEM-16 glp-gleam-link-layer | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205606Z; same caveat; overlaps their US1 touch-set |
| ITEM-17 cross-runtime-csharp-gleam-distributed-tests | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205513Z; same caveat |
| ITEM-18 full-scope-gleam-glp-implementation | US5 | story | pending | ~~EXT.ariellas~~ **SATISFIED 163500Z**; remaining: **G1** (via T022 — our 064 ship keystroke) + G3.R6–R12 (059-acceptance caveats) | roadmap: specified (specs/059); T023 reconcile is the wave's verification hook for ITEM-06/12–17 closures too |

## Drift record (T004 reconcile, 2026-08-03 ~2110Z)

- Live not-closed set = 13 vs snapshot 18: ITEM-06 (superseded) and ITEM-12..17 (closed) were
  disposed by ariellas' engineer-directed roadmap rider (batch 20:54–20:56Z; "Option-B
  re-scope" ruling on their record, 210601Z), consumed via imports e21edf62 + 210353Z. Wave-6
  does NOT rebuild them (FR-004). **Receipts landed 210601Z**: their implement COMPLETE @
  459be1b2 pushed (REPL 381 · C# 360 · gleam 618 · corpus 206/206, zero regression); their
  ship (T041) still pending → T023's 059-reconcile remains the wave's final verification hook,
  and EXT.ariellas stays open until their ship/close receipts post.
- FR-001/FR-002 of their 064 (dist-unify + distributed quiescence) were TRANSFERRED to the new
  capture `distributed-unification-quiescence-protocol-two-runtime-spec-first` — outside the
  wave boundary (wave-7+), now carrying real transferred requirements (noted for the roadmap).
- Post-snapshot capture `distributed-unification-quiescence-protocol-two-runtime-spec-first`
  (ariellas, 20:52Z) is OUTSIDE the wave boundary (assumption: wave-7+).
- `wave6-consolidation` itself appears in the live not-closed set (it is this wave's own
  feature, not a wave item).

## Peer-round record — 2026-08-04 (COOP, host gavriella)

- **Poll-filter defect (mine, corrected)**: the COOP poll excluded any path matching `gavriella`,
  which also excluded `inbox/gavriella/` — the directory peers write **to** me. ariellas'
  20260804T010500Z ACK+POLL sat unread ~14.5 h and was surfaced only by the file monitor. Filter
  now excludes own *writes* by filename, not by path. Any "peer silence" recorded earlier today
  should be re-read with this in mind.
- **Number collisions, second instance**: origin carries both `066-wave6-consolidation` (mine) and
  `066-abandon-stub-cleanup` (olamnit's) — same shape as the 064 pair
  (`064-durable-listener-service-box` vs `064-post-wave-gap-closure`). Proposed to both peers:
  same renumber-at-merge rule, first-lander keeps the number, engineer rules at that moment.
  Directory names differ ⇒ numbering-convention item, not a path conflict. **067 is taken**
  (`067-qr-link-provisioning`); next free is 068.
- **CalVer**: told olamnit NOT to reserve `.2` for our 064 (engineer-gated, no ETA) — they take the
  next free at their cut after a `git tag -l` + `ls-remote` re-check; we re-announce when ours
  actually cuts. ariellas' `.1` remains uncontested; we confirmed to them 064 is **NOT cut**.
  Note: a `v2026.08.04.1` tag will appear in the **buildkit** repo (different repository,
  different tag namespace) — not a glpnet crossing.
- **Repo-wide finding raised to both peers + the engineer (Bug Protocol, nothing changed)**:
  `glpquick-cert/glpquick.key` and `glpquick.pfx` are **tracked in git** and on origin despite
  `.gitignore:114` — the files predate the ignore rule, so it is inert. Trunk private-key material
  for public infrastructure, present in every clone. Added on or before `94fbe87d`
  ("release: v2026.07.09.1"). Remediation (history rewrite vs. key rotation vs.
  accept-and-document) is an engineer ruling: removal from history does not undo exposure, only
  rotation does, and rotation invalidates every pinned 036/049 endpoint. Both peers told **not** to
  fix it independently (a one-host history rewrite forks the other two). This halted
  `/bk-implement` on 067 at T002.
- **Sync rounds**: 153055Z (import 0-delta, reconcile clean, 0 dups, 99 live, export 18/99/2658
  published, replay-verify ✓) and 153951Z after importing ariellas' 153531Z + olamnit's rows
  (79 new journal lines; 99 live / 12 not-closed / 0 dups; export 18/99/2737 published;
  replay-verify ✓).

## T023 gate arithmetic — recorded 20260804T164000Z (why the ext-gate clearing did NOT start US5's tail)

EXT.ariellas is satisfied and independently verified, but **T023 did not become runnable**.
tasks.md is explicit:

- `T023 ⛔GATE(T022)`
- `T022 ⛔GATE(T021, EXT.ariellas-US2, G1)`
- `US5 strictly serial T017→…→T023, start gated on T016-go + EXT receipts (+G1 at T022)`

Two gates therefore remain in front of T023, neither peer-owned:

1. **G1 — our own 064 ship state.** `064-durable-listener-service-box` is ship-ready
   (551/551, codexreview capped@5-escalations-only, 51be73c5) and **not cut**; it awaits the
   engineer keystroke. ariellas has now taken `v2026.08.04.1`, so ours takes **`.2` or next
   free** with a fresh announce + `git tag -l`/`ls-remote` re-check at the cut (the day may roll).
2. **G3.R6–R12** — the 064-review rulings carried as 059-acceptance caveats (evidence-inventory).

**Nothing was run out of order.** T017–T022 correspond to ITEM-12..17, all disposed
`closed (by peer)` under ariellas' rider — wave-6 does not rebuild them (FR-004) — but the
*task-level* gates T021/T022 still require our local verification of their US1/US2 receipts plus
G1 before the T023 reconcile is entitled to run. Recording this rather than forcing the reconcile:
running T023 now would produce a disposition resting on an unverified 064 ship state, which is
exactly the "inherited-not-verified scoping premise" ariellas' own retrospective names as
systematic finding #3.

**Consequence for the wave**: with EXT.ariellas closed and EXT.olamnit closed, **every remaining
wave-6 gate is engineer-owned** — G1 (064 ship), G2 (065 track), G3.R1–R12 (rulings), the two
buildkit branch landings (ITEM-01/02), the ITEM-03/ITEM-06 contradictions, and ITEM-05's
graduation confirm. No peer dependency remains on the critical path.

### Peer intelligence consumed from their 163500Z receipt (acted on where it is ours)

- **`.import-manifest.json` conflicts on every multi-host ship — third occurrence**
  (`b5998681`, `7e1c055e`, `0d2739b1`), same mechanical repair each time. ariellas proposes
  gitignoring the mirror (the PGlite catalog is authoritative and the manifest is derivable) or a
  merge driver that runs the rebuild. **This will hit our 064 ship too.** Not fixed unilaterally —
  it is a fleet-wide convention change and needs the engineer, but it is now a *known* pre-ship
  hazard for G1, not a surprise.
- **A fixer's own green run is not evidence a finding closed** — 2 of their 11 review fixes
  introduced new defects caught only by a separate post-fix adversarial pass. Adopt for our
  /bk-codexreview fix mode at T029.
- **`glp_gleam/build` is single-OTP**, so WSL and Windows runs collide and the failure
  masquerades as a code regression (they have documented it in CLAUDE.md).
- **4 T040 escalations** remain engineer items in `specs/064-post-wave-gap-closure/t040-escalations.md`;
  the load-bearing one is the **bridge trust boundary** — FR-004 is silent on authentication/bind
  scope for the Gleam↔bridge leg and the two runtimes ship **opposite defaults on that one seam**.
  That asymmetry is jointly ours once the engineer rules; it stays in our evidence inventory,
  unfixed from this side.

## FLEET FINDING — `closed` is a one-way door in the roadmap CLI (engineer decision needed)

Surfaced by olamnit 20260804T174506Z while answering the ITEM-06 contradiction, and it outranks
the individual dispute that exposed it:

> "the roadmap `reconcile`/`advance` tooling currently can't reverse a `closed` state, so the
> mechanism itself needs the engineer too"

**Why this matters beyond one row.** `closed` is reachable by a **single host's** advance but is
**not reversible by any host's** advance, in a fleet whose three journals merge by CRDT union.
That combination means one host can permanently pin a shared feature's state on evidence that a
peer later overturns — which is exactly what happened here:

1. I advanced `glp-runtime-consol` → `closed` (wave-6 ITEM-03) on a premise that included
   ariellas' G5 supersession of Scope A.
2. A **later, higher engineer authority** (§1.14, 20260804, Gabi + Udi) revived Scope A under
   olamnit's 065.
3. The row is now wrong on every host, and **no host can advance it back** — the repair needs the
   engineer both for the ruling *and* for the mechanism.

This shape recurs every time a consolidation wave disposes an item a peer later revives, so it is
not a one-off. Recorded as a fleet finding, **not** fixed from this side.

**Companion item, same class**: ariellas' `.import-manifest.json` conflict-on-every-multi-host-ship
(third occurrence: `b5998681`, `7e1c055e`, `0d2739b1`). Both are "multi-host roadmap mechanics need
an engineer decision, not a per-host workaround". My position on that one, if asked: gitignore the
mirror — the PGlite catalog is authoritative and the manifest is a derived projection. olamnit has
since confirmed **no objection to a re-derive** (their 174506Z), so the fleet has no dissent on the
substance, only an unmade decision.

## Peer-round record — 2026-08-04 evening (all three hosts converged)

- **ITEM-06 CONTRADICTION → RESOLVED** (above). The substance is settled by olamnit's confirmation;
  only the roadmap re-open remains, blocked by the tooling gap.
- **ITEM-03 Scope-B**: olamnit agreed the annotation — complete removal @ `6c9cb8f1` subsumes the
  tombstone; no re-open on that half.
- **CONFLICT-2 (066 number) — CLOSED**: olamnit accepted renumber-at-merge in full; first-lander
  (`066-wave6-consolidation`, ours) keeps 066; their `066-abandon-stub-cleanup` renumbers at merge;
  their next branch is 068+; `067` is ours. Directory names differ ⇒ no path conflict.
- **CalVer**: olamnit ANNOUNCEd `v2026.08.04.2` for their 065 (antlr4 US1 + Scope B, `ce44ba0b`);
  **ACKed uncontested from here** — our 064 is not mid-cut and will re-announce for whatever is
  free when it finally cuts. Verified `v2026.08.04.1` (ariellas) is the only 08-04 tag on origin.
- **Fleet fact worth the engineer's attention**: *all three hosts* are now blocked on the same
  interactive ship approval — ariellas hit it on 064 (before their engineer cleared it), olamnit
  reports it for 065, and this host hit it on both buildkit branches. It is one systemic gate, not
  three independent stalls.
- **Repo-wide trunk-key finding**: three-for-three — both peers have explicitly declined to fix it
  independently, so no host will fork history behind the others' backs.

## Ship round 2026-08-04 evening — both peers shipped; ours is the only one left

| host | feature | CalVer | verified from this host |
|---|---|---|---|
| ariellas | 064-post-wave-gap-closure | **v2026.08.04.1** | tag `a3ac99d7` → `71f63f8c` = `origin/main`; back-merge contained ✓ (164100Z) |
| olamnit | 065-glp-runtime-consol | **v2026.08.04.2** | tag `bc76bfec` → `fbcf7667` on `origin/main`; back-merge contained ✓ (180120Z) |
| gavriella | 064-durable-listener-service-box | **next free `.3`** | NOT CUT — G1, engineer keystroke |

Both verified independently (`ls-remote --tags` + `merge-base --is-ancestor`), never on receipt
text alone. **Next free CalVer is `.3`**; announce + re-check `git tag -l`/`ls-remote` at our cut,
since the day may roll first.

**The systemic gate, now with evidence from all three hosts.** Every host hit the *same*
interactive ship-approval block today: ariellas on 064, olamnit on 065, this host on both buildkit
branches and on 064. Two were subsequently cleared by their engineers; this host was not — and
that asymmetry, not any technical blocker, is the sole reason our 064 remains uncut and therefore
the sole reason **G1 → T022 → T023 → the whole US5 tail** is still parked. It is one systemic gate,
not three independent stalls.

**Peer-flagged roadmap candidate (recorded, NOT captured from here)**: olamnit's SC-002
AST-lowering bridge (~250–400 LOC) to carry the antlr4 spike from SC-001 parity to IL-parity,
deferred out of 065 under their GO-WITH-CONDITIONS. Deliberately not captured on the roadmap by
this host — a capture from a non-owning host is the same class of one-way cross-host action that
produced the ITEM-03/ITEM-06 tangle. Engineer's call.
