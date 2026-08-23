# Curator report — bk-flow adoption readiness, gap scope

**Run** `20260823T155432Z-f49a` · task-type **plan** · feature `078-verification-receipts` ·
marathon `mrun-20d9230f767b` · host **GAVRIELLA**
**Method** `method-20260823T155432Z-f49a` — 16 elements, **7 refutes recorded open** (frozen under
`--accept-refutes`, see §5) · **Critic: codex (cross-provider), no independence warning**
**3 blind Builders / 3 pairwise-disjoint slices / independence audit 0 violations with
`checks_exercised = [builder-1, builder-2, builder-3]`** — the sibling-output hash checks actually
bit, so blindness is measured, not assumed.

**Verdict: `budget_stop` at cycle 1 of a 2-cycle minimum.** Not converged. See §6.

**Scope note.** This run deliberately answers ONLY what peer run `20260823T140508Z-227d` left
uncovered. That run already settled: bk-flow is an **integration, not a replacement**; cutover is
**NO-GO**; rollback is proven; no `bk-flow` SKILL.md exists. None of that is re-derived here.

---

## 1. Headline — all three questions are answered, and all three answers are negative

| Question | Answer |
|---|---|
| **Q1 — is one-feature-to-one-repo-on-one-host enforced?** | **No, and it is not a concept the substrate has.** Duplication is already present at scale. |
| **Q2 — can the takt-only duration rule be satisfied?** | **No.** Every duration path is ASSERTED or DEFAULTED. A measured expectation *is computed and then discarded.* |
| **Q3 — does an era exist, and where can one live?** | **No era exists**, and no existing record can close at `/bk-close` without changing that command's declared posture. |

---

## 2. Q1 — unique allocation

### The data half (builder-1, board DATA only, 23 claims)

Coverage stated **before** any finding: **54 of 54 op-log files across 14 of 14 sched roots**,
2059 ops parsed, 2 lines unparseable, 48 of 48 heartbeats. The join key was declared before any
duplicate was claimed, as the method required.

- **50 of 596 distinct `wp_id`s (8.4%) appear on allocate ops in more than one of the 14 roots.**
- **Strongest instance:** `wp-slug-linked-automatic-roadmap-row-advance-at-ship-close` is allocated
  across **three roots with four mutually inconsistent repo/host attributions** — and the board
  documents its own gap, because the op placing it under `yngenios-research` declares a *different*
  repo in its own payload.
- **`payload.repo` is present on only 303 of 942 allocate ops (32.2%)** and is shape-inconsistent,
  so it cannot serve as a uniqueness key.
- **85 of 326** single-root `(root, wp_id)` groups carry more than one named allocation target.
- **206 of 647** `(root, wp_id)` groups carry more than one **live** (non-superseded) allocation.
- **The host half is UNDETERMINED** — the board contains exactly one `host` field and it appears
  only in heartbeats, so distinct actors cannot be assumed to be distinct hosts. This is precisely
  the epistemic limit the Critic flagged twice on M1; the Builder honoured it rather than
  manufacturing a determination.
- **Risk disclosure that matters:** the live normalisation failure direction is **false split**
  (demonstrated 6×, driven by `wp_id` truncation at 63 chars), which biases every count **downward**.
  **The duplication figures are an under-count.**

### The code half (builder-2, engine source, 40 claims)

- `write_allocation()` is the **one** function that writes an allocation op. It enforces exactly
  three preconditions — `wp_id` non-empty, `proposed_actor` non-blank, and one more — **none about
  repo, host or feature uniqueness.** NEGATIVE with search receipt for any such check.
- **The uniqueness scope the code operates on is per-root, per-`wp_id`, last-wins** — never
  per-feature, never per-repo, never per-host. Multiple allocate ops for one `wp_id` are legal.
- The only actor-exclusivity check is at **confirm** time, not write time, scoped to one root and
  one WP.
- **The allocator never writes the substrate at all** — it returns a derived *view* envelope, so its
  one-proposal-per-WP property is a property of a read-side projection, not a durable constraint.
  *This confirms the view-vs-oplog divergence from code, having previously only been shown from data.*
- `repo` is an **optional payload string, validated against nothing**.
- **The single in-tree automatic call site (`cycle.py:523`, the refill mint) passes neither `repo=`
  nor `feature_id=`** — which is exactly why the board measures *binding: 1 of 32 packets resolve to
  a feature*.

### A real cross-slice corroboration — arrived at independently

Builder-1 measured a **modal `wp_id` length of 63 characters** consistent with truncation.
Builder-2 read, in code, that `wp_id` is minted as `wp-` + the roadmap slot **truncated to 60
characters**. Two disjoint slices, same mechanism, neither seeing the other. **This is the only
corroboration in the run that is genuine** — and, critically, it is *not* one of the 12 rows the
merge labelled corroborated (§5).

---

## 3. Q2 — durations

The consumed duration is the node field `e_t_s`. **All five paths that can set it are ASSERTED or
DEFAULTED:**

| Path | Kind | Detail |
|---|---|---|
| 1 (primary) | **ASSERTED** | board-fold `e_t_s` copied verbatim from the op's own field, last-non-zero-wins |
| 2 | **DEFAULTED** | `DEFAULT_EFFORT_S = 8*3600 = 28800s` when a not-done WP has no positive value |
| 3 | **DEFAULTED** | a `done` WP forced to `0.0` |
| 4 | **ASSERTED → DEFAULTED** | author free text bucketed onto constants at ingest |
| 5 | **ASSERTED** | operator `effort-assign` override |

**The sharpest finding of the run:** a PERT estimator **does** compute a genuinely measured
expectation `E[t] = (a+4m+b)/6` from real actuals and returns it as `est['e_t_s']` — and `plan.py`
consumes **only `est['var']`**. The measured expectation is computed and thrown away; only its
variance survives.

Supporting: `derive_actuals` genuinely folds transition ops into `execution_time_s`;
`marathon/takt.py summarise()` is genuinely measured but its unit is a **marathon step, not a board
node**; a third measured path (`gate_wait_split`) is **dead — no caller anywhere**; `sizing/` carries
story **points only and converts nothing to time**; and the slice's own documentation states the
size-to-time rate is an **unmeasured planning constant awaiting replacement by actuals** and that a
takt projection is **not implemented**.

From the data side (builder-1): **no actual/elapsed/duration/takt field exists in the board at all**
(NEGATIVE with receipt), and **every CPM node carries a `node_variance` whose source is `default`** —
the data says so explicitly.

**Therefore the rule — only the generic takt range, or a size-adjusted estimate computed from
measured actuals — cannot be satisfied for any node today.** Not because the data is missing, but
because the code discards the one measured value it computes and there is no join from marathon-step
actuals to board nodes.

---

## 4. Q3 — era

**Audit half first, as the method required. NEGATIVE with receipt: no era-like span exists** under
any of era / epoch / span / wave / trail. Five candidates exist and **not one spans specify→close**:

| Candidate | Why it fails |
|---|---|
| marathon **run** | closest — feature-keyed, persisted as `marathon_*` rows + `store_dir` + a per-run mirror — but opens at `marathon open` / `bk-flow open`, **not at `/bk-specify`**, and carries an open instant with **no close-instant field** |
| bk-flow **link** | **structurally incapable of a mutable close** (`link.record` never rewrites), and **machine-local + gitignored**, so it can never be a fleet-shared era |
| backlog **item** | scoped strictly inside one run |
| **step** | narrower; phase vocabulary lives outside the slice |
| pipeline **stage** | one stage, not a multi-stage interval |

**The decisive structural risk: no candidate can close at `/bk-close`,** because `bk-close` is
explicitly declared **not a canonical pipeline stage** and is forbidden from mutating pipeline/DBOS
state. Its only durable writes are additive retrospective/action rows plus a disk mirror.

**Second finding:** bk-flow's pipeline awareness **stops at `/bk-implement`** —
`next_pipeline_command` probes only `spec.md`/`plan.md`/`tasks.md` and never emits `/bk-ship` or
`/bk-close`. So bk-flow cannot even observe the half of the lifecycle an era would close on.

**Third:** the slice documents the run mirror as *"regenerated from catalog rows, explicitly NOT a
fallback writer"* — which **directly contradicts** the measurement that it is 15+ hours stale and did
not regenerate across an 18-step expand and four checkpoints. Documented intent and measured
behaviour disagree; **the measurement stands.**

**All nine design rows were ESCALATED by the Critic** — correctly. Where an era's rows live, what
opens and closes it, and what happens on re-open are architectural decisions reserved to the
engineer. **I have not resolved any of them** (FR-004/SC-004).

---

## 5. 🔴 A defect in the merge algebra itself, found while curating

Merge reported `combined=49 corroborated=12 singletons=37 conflicts=0`. I tested every corroborated
row by **exact text membership** — does the row's claim text literally appear in each credited
Builder's own file?

> **12 of 12 corroborated rows carry at least one ghost Builder. Every one is a single-author claim
> credited to Builders who never wrote it. Genuine corroboration: zero.**

Examples: a claim about `allocate_writer.py` credited to builder-3, which had no engine source; the
allocation write-path claim credited to builder-1, which was data-only; builder-3's own coverage
statement credited to builder-1.

**Mechanism, found at source** (`threerole/merge.py`): the default `key_mode="concept"` union-finds
related claims into one finding and then **unions the builder lists**, rendering one Builder's text
beside the whole cluster's authorship. Over disjoint slices that **manufactures agreement** — the
exact failure the disjointness contract exists to prevent, and judgment smuggled into the layer whose
entire warrant is *"set-ops, never judgment."*

**This is not PR #622's defect.** #622 fixed *contentless* claims collapsing on `sha1("")`; it is
merged in the executing tree and this still happens with non-empty, distinct texts. An aggravating
design bias sits in the same file: *"`corroborated == 0` is the historical merge-defect signature"* —
so the tool treats zero corroboration as evidence of a bug, creating standing pressure toward the
mode that manufactures it.

**Consequence:** every 3rtask corroboration count this fleet has quoted measures concept-cluster
co-membership, not agreement. **TOOL-R9 must widen** from "runs whose Builders emitted `claim_text`"
to **"every run whose conclusion rests on a corroboration count."**

The findings in §2–§4 are unaffected, because they rest on **per-Builder attributed evidence with
citations and coverage fractions**, not on the corroboration label.

---

## 6. Why this stopped at cycle 1

`budget-check` returned **`warn_confirm`: tokens 966,000 ≥ budget 500,000**. The protocol requires a
stop and an engineer decision rather than a silent overrun. Residual state is persisted; cycle 2 can
resume from it. **The verdict is `budget_stop`, not `converged`** — with `min_cycles = 2`, one cycle
is by definition not convergence, and labelling it otherwise would be the false green this feature
exists to eliminate.

Recorded token rows: builder-1 148,164 · builder-2 170,170 · builder-3 153,071 · planner 454,698.
*(Note: my first `tokens` calls omitted `--total` and wrote `0` rows; corrected rows were appended
rather than replacing them, so both are in the ledger.)*

## 7. Open ESCALATEs — the engineer's, not mine

Nine, all Q3-design: era row schema and storage location; the close write-point; poll semantics for
open/closed/absent eras; re-open policy; the specify-side open point; and the four-part Q1 remedy
(required `repo` kwarg, identity change, validation, fleet index). Each is recorded in
`escalations.md`.

## 8. What follows immediately, in cost order

1. **Consume `est['e_t_s']`, not only `est['var']`** — small, high-leverage, makes measured durations
   real for calibrated nodes.
2. **Make `repo` a required kwarg of `write_allocation`** and pass `repo=` + `feature_id=` at
   `cycle.py:523`. Closes the binding gap at its single source.
3. **Default `key_mode` to exact text**, or render authorship and cluster-membership as separate
   fields. Until then, no corroboration count is trustworthy.
4. **Record `host` on allocate ops** — until then one-per-host cannot even be *checked*.
