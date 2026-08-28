---
name: yx-bootmig
description: Drive the yngenios bootstrap-migration programme — resolve and content-verify source roots, build the cross-repo relation, delineate product scope per source under an engineer ruling, then migrate source→target pairs as /bk-marathon eras with per-source attribution. Advisory and refusing-by-default. Use when the user asks to run, plan, or continue yx-bootmig, the bootstrap migration, or an L0–L4 capability migration into the YNGENIOS targets.
---

# `/yx-bootmig` — bootstrap-migration programme driver

> **PROVENANCE — read this before anything else.**
> The specification for this skill lives in **another repository**:
> `D:/BSTDEV/research/yngenios/specs/008-yx-bootmig-base/{BRIEF.md,DESIGN.md}`.
> That repo is the **owner**. This copy was installed into `olamnit-assistant` by engineer
> decision on **2026-08-27**, over a recorded recommendation to build it in `yngenios` instead.
> **Read the BRIEF and DESIGN before acting** — they carry amendments A1, A2 and A3 and the
> rulings, and this file is a driver, not a substitute for them.
> If the two ever disagree, **the yngenios spec wins** and this file is the thing to fix.

## What this drives

A tool + programme that migrates **yngenios-scope capability** out of N source repos into four
targets, so each target holds every capability from the sources plus everything it already had,
consistently.

| role | repo | layers |
|---|---|---|
| target + L0 source | `YNGENIOS` | **L0 only** |
| target only | `YNGENIOS-WINDOWS`, `YNGENIOS-LINUX`, `YNGENIOS-APP` | **L1–L4 only** |
| source only | `qhstate`, `olamnit`, `glpnet`, `buildkit` | — |

**Role is a first-class axis** (A1/FR-10): source-only, target-only, or both. A design that models
only "where it runs" cannot express this, and neither may an invocation.

## Hard preconditions — REFUSE, never default

Check these first and **stop** with the named blocker if any fails. Refusing is the correct
outcome; defaulting is the defect this programme exists to remove.

1. **P0 — L3/L4 are UNDEFINED** (measurement M2). `docs/architecture/LATTICE.md` defines
   **L0/L1b/L2 only**. The "L1–L4 only" routing rule is therefore **unevaluable**, and ruling
   **R-L4** blocks on it. **Do not infer L3/L4 membership.** Surface the gap.
2. **Epic `bootstrap-migration` does not exist** (M4). It must be created on the roadmap first.
3. **Three of the four targets are absent from this host** (M3): `yngenios-windows`,
   `yngenios-linux` and `yngenios-app` are lane slugs and coop channels, **not repo roots on this
   disk**. Any migration into them is `NOT-DISCHARGEABLE-HERE`, never "done".
4. **An undelineated source is REFUSED** (FR-2). No P3 manifest ⇒ no P4.

## The six phases

    P0  DEFINE L3/L4                     [BLOCKING · ruling R-L4]
        membership rule per layer + a named disposition for capability fitting neither

    P1  RESOLVE + VERIFY ROOTS           [FR-9]
        roots come from CONFIG and are verified BY CONTENT
        (git rev-parse / ls-files / marker), never from a lane slug
        every unreadable root is NAMED, never skipped

    P2  BUILD THE CROSS-REPO RELATION
        extend the callgraph node key beyond {repo}:{rel} so edges cross repos
        this is the substrate a content-based scope predicate needs and lacks today

    P3  SCOPE DELINEATION, PER SOURCE    [GATED · rulings R-A3.1 + R-A3.2]
        PROPOSE boundaries as RULES or SUBTREES — never file lists (FR-5)
        each option shows BOTH sides of the line WITH denominators, before the ruling
        present under BK-STD-2; the ENGINEER decides; record the ruling durably (FR-6)
        output: an approved scope manifest; every excluded file carries its reason

    P4  MIGRATE, PER SOURCE→TARGET PAIR  [one era each · ruling R-ERA]
        copy | resynthesis | re-engineering, per the manifest
        union-by-identity with per-source attribution (FR-7)
        same identity + different semantics ⇒ ESCALATE with both provenances (FR-8)

    P5  VERIFY + REPORT
        coverage as a source × target MATRIX naming both axes
        and naming the roots it could not read (FR-11)
        per-era close via /bk-close; programme progress is the FOLD, never one era

## Root resolution — the part that has already bitten

Resolve from configuration, verify by content. **A name is not an identity.**

| source | verified root | tracked files |
|---|---|---:|
| yngenios | `D:/BSTDEV/research/yngenios` | — |
| qhstate | `D:/BSTDEV/research/qhstate` | 3,983 |
| olamnit | `D:/BSTDEV/research/olamnit` | 2,970 |
| **glpnet** | **`D:/BSTDEV/research/GLP/glpnet`** | 7,896 |
| buildkit | `D:/BSTDEV/research/buildkit` | 5,736 |

⚠ **`glpnet` is NOT at `D:/BSTDEV/research/glpnet`.** It is nested under `GLP/` beside six
similarly-named siblings, and **`GLPNET` (capitals) is a DIFFERENT directory** — the same
case-collision class that destroyed documents in this fleet on 2026-08-25.

⚠ **Measured decoys that pass an exists-only check**: `qhstate-Yngenios` is **not a git repo**;
`olamnit-assistant` is a git repo with **0 tracked files**. An `exists` test is not a verification.

## Scope delineation — and why the obvious instrument is banned

Path-name proxy over tracked paths (2026-08-26): qhstate 607/3,983 · olamnit 248/2,970 ·
**glpnet 1/7,896** · buildkit 380/5,736 — **1,236 of 20,585 = 6.0%**.

**~94% of the combined corpus is not yngenios scope**, so wholesale migration would import four
unrelated products into the targets.

🔴 **A2.2 — that proxy MUST NOT be the delineator.** It fails in both directions: glpnet scoring
1 of 7,896 is far likelier a proxy failure than a real finding, and yngenios-scope code (a kernel,
a mailbox, a WAL) need never say "yngenios" in its path. Use it to size the problem; **never to
decide it**.

## Reporting rules — inherited, non-negotiable

- **Every count carries its denominator, and every report NAMES the roots it did not read.**
  A missing root reads as an empty one, and that is this programme's house defect class.
- **State the refuter for each claim** — the concrete observation that would kill it.
- Questions to the engineer go through **BK-STD-2** (`.specify/standards/bk_question.py`):
  validate → present interactively, recommended option first → `decide` so the ruling round-trips
  onto the `qid`. A decided question is cited, never re-asked.
- Takt and per-phase token use are **recorded into and read from the TAKT DuckLake**
  (`buildkit-scheduler takt-tokens` + `takt-sync`). An unmeasured phase reports `unmeasured`,
  **never zero**, and a split across phases is **never apportioned by guess**.
- Report with the shipped generator only: `.specify/standards/bk_report_v1.py all --feature <id>`,
  order **ROADMAP → PROGRESS → STATUS → SITREP → TAKT → NEXT**.

## Harness binding

Each source→target era is a `/bk-marathon` run; `/bk-flow` opens the work packet and binds it to the
era's feature; `/bk-scheduler` supplies the board. **If the harness offers no programmatic
approval/discharge for the P3 gate, the refusal lives inside this skill and says so** — never claim
an integration that does not exist.

## Advisory boundaries — non-negotiable

- **Advisory only.** Never auto-invoke a `/buildkit-*` or `/bk-*` pipeline stage.
- **Never push, never merge**, never mutate DBOS/pipeline state.
- **Never migrate an undelineated source.** Refuse.
- **Never pick between conflicting capabilities.** Surface both with provenance; the engineer picks.
- **Never claim completeness for limb (a).** 87 of 94 roadmap features carry no `spec_path`, so
  "satisfy the requirements of all open features" addresses a corpus that largely has no
  requirements. The deliverable there is an **engineer-approved treatment**, not a completeness
  claim.

## Honest limits, carried from the design

- **One analysis cycle, `budget_stop` — not `converged`.** A second pass would likely surface more.
- **One vantage.** Every measurement is from GAVRIELLA's disk. The sources' presence on ARIELLAS /
  SHIRAS / OLAMNIT is **unmeasured**.
- **13 singleton claims were demoted** for lack of disjoint corroboration and **may not be used as
  design constraints** without a second independent derivation; **4 were refuted outright**.
- **`yx` already exists** in the yngenios repo (M1): `yx = yx_distill.cli:main`, 69 modules, prior
  verbs `yx wave` / `yx matrix export` / `yx callgraph compute`. **REUSE-vs-REBUILD is a
  first-class question and duplicating it would be a defect.**

## Invocation

State which phase you are running and against which roots. If a precondition above fails, say which
one and stop — that is a successful outcome, not a failure.
