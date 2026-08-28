# Consolidated hardening — 2026-08-23

**Marathon**: `mrun-20d9230f767b` · **Feature**: `078-verification-receipts` · **Host**: GAVRIELLA
**Roadmap feature this codifies into**: `scheduler-feature-stream-durable-healing-and-hardening`
(promoted; WSJF 2.62 / RICE 311.54) — **no new feature was minted.**

> **Why this file exists.** Marathon steps are the state machine; a step's *content* lives only in
> its name, and `expand --steps` splits on commas with no escaping, permanently. This file is the
> **authoritative content** for the codify step `mstep-01a0199b-a88c`. **Where a step name and this
> file disagree, this file wins.**

This is the codification of the `codify-consolidated-hardening-feature` step. It folds four
independently-measured strands into the one existing hardening feature rather than minting a fifth
roadmap row: the scheduler four-break chain (already in the feature), the fleet-wide Lock 1
measurement, the board→pipeline binding gap, and the toolchain-identity defect that undermines the
evidence base of all three.

---

## What the existing feature already carries — unchanged

BREAK 0 (refuted — supply is fine), BREAK 1 (`backlog→ready` has no writer in any cycle path),
BREAK 2 (vacuous readiness recommender), BREAK 3 (efforts exceed capacity, unplaceable proposals
emitted silently), BREAK 4 (the allocate **view** contradicts every durable allocate **op**).
Remedy shape (a)–(d) as recorded in the feature's notes. None of that is revised here.

---

## Strand 1 — SCHED-R5 is DONE, and it changed the conclusion for three boards

`docs/research/scheduler-lock1-fleet-audit-2026-08-23.md` closes the scope note in
`scheduler-feature-stream-rootcause-2026-08-22.md`, which required exactly this before the fix
could be called fleet-wide.

14 boards measured read-only under `D:/coop/*/sched`, folding to the **current** addressee per WP
(last `allocate` in R2 order) rather than counting history:

- **Three boards were reading a false all-clear on Lock 1** — `glpnet` (22 of 28 unowned),
  `yngenios-windows` (27 of 28), `lejepa` (30 of 35). All three have **zero blanks**, so the old
  presence test found nothing to report while nearly all their work sat unowned.
- **`lejepa` proves a hard-coded pool vocabulary is insufficient**: its pool actor is
  `ariellas-lejepa`, which no built-in list contains. Before the fix its 30 unowned WPs fell
  through to *unknown* — reported but deliberately **not** gated. The false green in a new costume.
- **`buildkit` and `ospark` are healthy**, and revision 1 of that audit said otherwise. Judging on
  history alone libels a board that has done the work. Revision 1's figures are **withdrawn**.
- `yngenios-research` is the only board with **Lock 2 open**; `yngenios` is empty and stays
  **UNMEASURED** — an empty board has been shown empty, not healthy.

**Bearing:** this measures 14 boards *as visible from this host*. It establishes nothing about the
engines installed on peer hosts. The fix is branch-local (`086-sched-r3-placeholder-addressee`,
pushed, not merged) and the two-repo ship ruling is owed.

## Strand 2 — SCHED-R6: the capability gate is inert, and says so

First `bk-flow poll` ever run in glpnet (root `D:/coop/glpnet/sched`, actor `gavriella`) reports:

> `capability_gate_inert: no work packet declares a required_capability, so the capability-fit
> ranking never executed — missing_capability=0 here means UNMEASURED, not clear. 50 capabilities
> published by this actor were never compared against anything.`

That is 078's thesis working correctly inside the scheduler's own reporting: an unmeasured check
declares itself unmeasured instead of passing. **SCHED-R6** = declare `required_capability` on WPs,
or keep the gate honestly inert and never let a downstream reader treat `missing_capability=0` as
a clear. Sized **mini (7)**.

## Strand 3 — the binding gap is a SEPARATE defect from the readiness gap

Same poll, 32 work packets:

| reason | count |
|---|---:|
| `not_ready` | 25 |
| `claimed_by_other` | 3 |
| `not_claimed` | 2 |
| `ok` (dispatchable by me) | 2 |

> `binding: 1 of 32 packet(s) resolve to a feature; 31 cannot.`

Only 078 is feature-bound. **A WP that reaches `ready` still has nowhere to go**, so fixing
BREAK 1 alone does not start the stream — it moves the stall one hop downstream. This is not
covered by SCHED-R1..R5 and is added here as **SCHED-R7** (bind WPs to features, or refuse to
report a board as dispatchable when its binding rate is near zero). Sized **midi (11)**.

**Premise verified while establishing this** — `bk-flow` **depends on** marathon, it does not
replace it. `bk-flow open` calls `_open_marathon_run(feature_id, project_root)` and persists the
run id into `.specify/flow/links/<wp>.json`; `bk-flow takt` reports per-phase takt against *that
feature's marathon run*. Any migration plan that treats bk-flow as a marathon replacement is wrong
at the premise. Adoption state in glpnet: the CLI is installed (`bk-flow.exe`, all five verbs exit
0) but there is **no `bk-flow` skill** among the 39 `bk-*` skills, `.specify/flow/links/` is
**empty**, and `.specify/feature.json` is **absent**.

## Strand 4 — the toolchain that reports on all of this has 078's own defect

**Measured, and stated at the precision the measurement supports.**

`site-packages/_editable_impl_buildkit_cli.pth` points at `D:\BSTDEV\research\buildkit\src`, so
every `bk-*` invocation on this host executes the buildkit **working tree** — currently branch
`087-import-untrusted-key-warning` — and never a deploy-home version. `buildkit-deploy list` shows
all **29 targets `active` at 2026.08.23.1**, and deploy-home holds a full ~443 MB copy per version
across 12 versions that **nothing on `sys.path` references**.

Divergence between the pinned tree and the executing tree, measured honestly:

| | count |
|---|---:|
| files differing | 51 |
| **line-ending-only** | **48** |
| **real content differences** | **3** |

The 3 are `threerole/__main__.py`, `threerole/merge.py`, `threerole/model.py` — and all 3 are
**uncommitted** (7 modified files in total, including `templates/commands/buildkit-3rtask.md` and
3 test files). **`/bk-3rtask` on this host runs code that exists in no released version and in no
commit.** A third version surface disagrees again: pip's dist-info says `buildkit_cli-2026.8.19.1`
while the module self-reports `2026.8.23.1`.

> ### 🔴 RE-MEASURED 2026-08-23T14:40Z — the figures immediately above are SUPERSEDED
>
> The buildkit repo **changed under this session**: it was on branch
> `087-import-untrusted-key-warning` with 7 modified files at ~14:00Z; at 14:40Z it is on
> `flow-adoption-3rtask` with a **clean tree**. Both readings were true when taken. Re-measured
> against the same pinned `2026.08.23.1`:
>
> | | ~14:00Z | **14:40Z** |
> |---|---:|---:|
> | line-ending-only | 48 | 48 |
> | **real content differences** | 3 | **24** |
> | **files absent from the pinned version entirely** | 0 | **5** |
> | uncommitted files in the executing tree | 7 | **0** |
>
> **WITHDRAWN:** "exactly 3 real" and "runs code that exists in no commit". Both were accurate at
> 14:00Z and are wrong now. The corrected statement is **stronger**: the executing tree is *ahead
> of* the pinned version by 24 changed files plus 5 that do not exist in it at all — including
> `scheduler/engine/daemon/{allocate_writer,ingest,cycle,plan,board,substrate_io}.py`, i.e. **the
> exact scheduler code this feature's root cause is about**, and new `pipeline/takt.py` +
> `pipeline/takt_store.py`. The divergence is *shipped work the pin cannot see*, not dirt.
>
> **Two items discharge as a result, both by another lane while this session ran:**
> - The 3rtask false-corroboration fix **merged as PR #622** (`f50e1e87`,
>   *"refuse empty claim text instead of merging it as corroborated"*). TOOL-R8's
>   uncommitted-work half is closed; **TOOL-R9 still stands** — the corroboration counts this
>   feature's evidence rests on have still not been re-checked.
> - `7a7e1285` / PR #618 — *"expand splits on COMMA and never said so"* — **fixes the
>   `expand --steps` delimiter defect this marathon has carried since 2026-08-20**, the one that
>   permanently mangled seven steps on this very run.
>
> This paragraph is itself the rule in action: *a measured claim is stamped, and a claim older
> than the measurement is a hypothesis until re-run.*

*Not overclaimed:* two tombstoned dists (`~uildkit_cli-2026.7.21.1`, `~uildkit_cli-2026.8.15.1`)
and an orphan `site-packages/buildkit_cli/core_pack` with no `__init__.py` are present, but the
orphan was **verified not to shadow** — a regular package later on `sys.path` beats a namespace
portion earlier, and the import resolves to the dev tree.

### Why this is load-bearing for the feature, not a side note

Those uncommitted `threerole/` files are a fix for a **false-corroboration defect in `/bk-3rtask`**.
Claim identity keys on the claim-text SHA-1, so every contentless record hashes to `sha1("")` and
collapses into **one identity carrying every Builder**, which merge then reports as *corroborated
by all of them*. The new `EmptyClaimError` docstring records the measurement: **74 records emitted
under the key `claim_text`** — a spelling the parser never read — **became one row reported as
corroborated by all three Builders.** The fix reads `CLAIM_TEXT_KEYS = ("claim", "claim_text")` and
refuses empty text at parse exactly as an empty `source_citation` is refused (FR-005).

**This marathon's scheduler root cause and its tidy-up plan were both adjudicated on 3rtask
corroboration counts.** Two actions follow, and neither is discretionary:

1. Ship the 7 files out of the dev tree — **owed to the two-repo ruling**, not doable here.
2. **Re-check the corroboration counts of the runs this feature's evidence rests on**, for any run
   whose Builders emitted `claim_text`.

Until (2) is done, every corroboration count quoted in the 08-22 root cause is a **hypothesis**,
not a measurement. The root cause itself was proven *both ways* by direct board manipulation
(078 allocated by hand confirmed instantly; two merely-proposed WPs refused until addressed), and
that proof does **not** depend on 3rtask — so the conclusion stands while the corroboration
*figures* are pending re-check.

---

## Remediation ledger after this codification

| ID | Item | Size | Pts | State |
|---|---|---|---:|---|
| SCHED-R1 | readiness writer — declared `backlog→ready` promotion policy | maxi | 17 | pending |
| SCHED-R2 | allocator persists proposals as addressed ops, not only a view | midi | 11 | pending (**falsely marked complete once — it is NOT done**) |
| SCHED-R3 | audit must verify `proposed_actor` names a DECLARED ACTOR | mini | 7 | shipped to branch `086-sched-r3-placeholder-addressee`, **not merged** |
| SCHED-R4 | declare dependency edges so `edge_coverage` stops being 0.0 | midi | 11 | pending |
| SCHED-R5 | verify the same links on every OTHER board | mini | 7 | ✅ **DONE** — 14 boards, rev 2 |
| SCHED-R6 | capability gate inert — declare `required_capability` or keep it honestly unmeasured | mini | 7 | **new today** |
| SCHED-R7 | board→pipeline binding — 31 of 32 WPs resolve to no feature | midi | 11 | **new today** |
| TOOL-R8 | toolchain identity — the running code is not the pinned version | midi | 11 | **new today**, engineer/two-repo |
| TOOL-R9 | re-check 3rtask corroboration counts for `claim_text` runs | mini | 7 | **new today**, gated on TOOL-R8 |

**Total 89 pts · 7 delivered (R5) · 82 remaining**, of which **18 are engineer/two-repo gated**
(TOOL-R8, TOOL-R9) and 7 are shipped-but-unmerged (R3).

## Open decision carried forward, unresolved

The feature's own notes say glpnet `082-feature-stream-superset` **"should be folded into this or
explicitly scoped as its engine half."** That is still undecided, and `082` was merged to `develop`
on 2026-08-20 (TIDY-W03) while the roadmap row for this feature stayed `promoted`. Folding vs
scoping is an engineer call; this file records it as open rather than picking a side.

## Binding rules that survive from the 08-22 plan

1. No deletion may claim a reflog recovery window — every delete is class **C2**.
2. An archive tag is preservation **only when verified** at delete time, on `origin`.
3. A git bundle is **never** content preservation.
4. The merge gate is **local only** — no CI runs `test/run_all_tests.sh`. Baseline **561 / 559
   passed / 2 failed**; the 2 Section T failures are **real** (they survive a rebuild).
5. Never quote a branch count, an ahead-count, or a board number without naming the ref or root.

---

---

## Appendix — unshipped inventory, measured 2026-08-23T14:40Z

Step `inventory-unshipped-features-patches-chores` (`mstep-01a0199b-c0b1`). Every figure below
names its ref, per binding rule 5.

**glpnet working tree:** clean (`git status --porcelain -uall` = 0). **Linked worktrees: 0** —
re-verified again today; every `wt-*` path on `D:` belongs to another repo.

**glpnet local heads: 5** — `066-wave6-consolidation`, `067-qr-link-provisioning`,
`078-verification-receipts`, `develop`, `main`. Unpushed: `067-qr-link-provisioning` **+1** and
`develop` **+1** (this session's codify commit). All others level with their upstream.

**glpnet origin heads: 21** (an earlier count of 22 included the `refs/remotes/origin` listing
artifact, which is not a branch). `origin/develop` is **13 ahead / 0 behind** `origin/main` — one
uncut release. **17 of 21 are UNMERGED into `origin/develop`:**

| branch | ahead of develop | last commit |
|---|---:|---|
| `050-full-gleam-combined` | 48 | 2026-07-29 |
| `059-full-scope-gleam-glp-implementation` | 32 | 2026-07-29 |
| `067b-qr-link-continuation` | 27 | 2026-08-11 |
| `051-ynet-transport` | 26 | 2026-07-16 |
| `067-qr-link-provisioning` | 25 | 2026-08-11 |
| `066-wave6-consolidation` | 23 | 2026-08-11 |
| `backup/030-phase8-polish` | 9 | 2026-06-15 |
| `030-phase8-polish` | 8 | 2026-06-13 |
| `085-onrestart-fleet-resume` | 7 | **2026-08-23 — live, another lane** |
| `backup/078-olamnit-impl-preserve` | 5 | 2026-08-19 |
| `chore/tidy-up-branches-worktrees-20260822-olamnit` | 4 | 2026-08-23 |
| `083-glptutorial-corpus-goldens` | 2 | 2026-08-22 |
| `080-occurs-checked-substitution` | 2 | 2026-08-16 |
| `017-conversion-plan-agents` | 2 | 2026-07-19 |
| `016-codeconv-init-scaffold-langpair` | 2 | 2026-07-19 |
| `backup/upgrade/buildkit-migration-20260627T220138Z` | 1 | 2026-06-27 |
| `078-verification-receipts` | 1 | 2026-08-20 |

Contained (safe to consider for cleanup under the C2 rules): `082-feature-stream-superset`,
`065-ynet-consolidation`, plus `main` and `develop`.

`051-ynet-transport` (26 ahead, untouched since 2026-07-16) does **not** appear in the 08-22
ledger's X01–X17 and is **newly surfaced here**. `050` and `059` remain complementary — the X10
ruling is still owed and picking one still discards a subsystem.

**Connected repos — patches/chores:** `D:/BSTDEV/research/buildkit` is on `flow-adoption-3rtask`,
**clean, 0 unpushed**, and carries **32 linked worktrees** (the volatile-`TEMP` population already
logged as `mitem-01a02e87`). Nothing unshipped there as of this measurement — a state that
changed twice during this session, so it is stamped, not assumed.

---

*Authoritative content for the `codify-consolidated-hardening-feature` and
`inventory-unshipped-features-patches-chores` steps of marathon `mrun-20d9230f767b`. Update this
file, then reflect state in the marathon; never the reverse.*
