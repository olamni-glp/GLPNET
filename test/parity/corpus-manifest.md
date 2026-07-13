# Corpus parity manifest (feature 050, US3 — M1 LOCK)

**Task:** T037 (analyze A1 — pin the case list before recording goldens).
**Contract:** `specs/050-full-gleam-combined/contracts/corpus-parity.md`.
**Governs:** SC-001 (100% agreement is measured against *this* manifest), FR-011
(GAP/FORK cases must exist and pass before parity is declared).

The **pinned case list** is the machine-readable `test/parity/corpus.list` (one
source of truth for the recorder `record_dart_goldens.sh` and the comparator
`run_gleam_corpus.sh`). This document is the **reviewed rationale**: what is in, what
is out, why, and how a case is run on each instance.

---

## 1. The case model — why a "case" is not a `run_all_tests.sh` block verbatim

The reference suite `test/run_all_tests.sh` drives the **Dart** REPL, which
**accumulates loads within a session**: a Section-A block loads several `.glp` files
into one program, then runs several goals against the union.

The Gleam instance's `engine.load` (US1/US2) **replaces** the user program on each
load — `program.merge(user_source, prelude)` — it does **not** accumulate multi-file
loads (engine.gleam:132). Making it accumulate is an engine change beyond US3's
sanctioned scope (it would reopen US2). So a corpus case cannot be "replay the
block's load sequence, then the goal."

**Resolution — the concatenated-case model (stays inside the frozen engine):**

- A **runtime case** (Section A) = `{ id, files[], goals[] }`. Both instances load
  the block's files as **one program** and run each goal against it:
  - **Dart** loads the files sequentially (its native accumulate — identical union).
  - **Gleam** loads the files **concatenated into one source** (one `load`), yielding
    the same procedure set. Within a Dart block the co-loaded files are already
    conflict-free (they are chosen to co-exist), so the concatenation is equivalent.
  - Parity is measured on the **goal outcomes only**. The per-file load-echo lines
    (`✓ Loaded: <path>`) differ trivially (Dart per-file vs Gleam one concatenated
    load) and carry host paths, so the shared normalizer **drops load-echo lines from
    runtime cases**. A load *failure* in a runtime case is a real defect: the recorder
    aborts the block and reports it (never silently records a broken golden).

- A **load-outcome case** (Sections B/C/D/E) = `{ id, section, file }` (or inline
  source for E). It loads exactly one file and the *outcome of the load itself* is the
  parity subject, normalized to a **binary** class token: `LOADED` / `REJECTED`. This
  is the reference suite's own bar (Section B asserts `Loaded: <f>`; C asserts *not*
  loaded; D asserts `SRSW violation | Error loading`; E asserts the specific
  `"true" is not a guard` message). A finer stage token is **not** used as the diff
  target because it is not a reliable cross-implementation signal — e.g. the reference
  itself rejects the Section-D file `merge_reader_at_input.glp` with
  `UnknownTypeError: Stream?` (the **type** stage), not an SRSW message, and the two
  implementations' error *text* can never byte-match. The recorder additionally
  **classifies** Dart's rejection stage (guard / srsw / type / parse / other) into a
  side file `goldens/loadstage.dart.tsv` as **informational metadata** (not diffed);
  it lets T042 spot a genuine stage-ordering difference without fabricating a stricter
  bar than the oracle. The single guard case (E) is where the reference pins a specific
  rule, so its expected stage is `guard`.

Wall-clock (SC-009) is recorded **per case, summed suite-level** (the contract's bound
is `sum(gleam) ≤ 10 × sum(dart)`, not per-case — tolerating per-case noise). Timings
live in `goldens/timings.dart.tsv` (not diffed); the outcome goldens live in
`goldens/<case-id>.golden` (the diff target).

---

## 2. Per-section include / exclude (over `run_all_tests.sh` sections A–K)

| § | Reference content | Parity role | Decision |
|---|---|---|---|
| **A** | Typed runtime goals → bindings + `→ succeeds/failed/suspended` | Goal-outcome parity — the core of M1 corpus | **IN** (all blocks A1–A30; see §3 for per-block notes and GAP flags) |
| **B** | ~110 positive load files (must load clean) | Load-outcome parity (`LOADED`) | **IN** |
| **C** | ~50 negative type files (must be rejected) | Load-outcome parity (`REJECTED`; stage recorded informational) | **IN** |
| **D** | SRSW-violation files (must be rejected) | Load-outcome parity (`REJECTED`; stage informational) | **IN** |
| **E** | Inline `true`-in-guard clause (must be rejected) | Load-outcome parity (`REJECTED`; expected stage `guard`) | **IN** |
| **F** | CSSG modules (project-directory loading) | — | **OUT** — Gleam instance has no project-directory loader |
| **G** | Social-graph UI modules (project loading) | — | **OUT** — same |
| **H** | CSSN plays 1–12 (project loading) | — | **OUT** — same |
| **I** | self.glp procedure tests | — | **OUT** — several are project-loaded / shadowing multi-file; the self.glp procedures themselves are exercised throughout A |
| **J** | CSSG v2 modules (project loading) | — | **OUT** — project loading |
| **K** | CSSN v2 modules (project loading) | — | **OUT** — project loading |

**Sections L–R** (dynamic dispatch `M#goal`, multi-isolate `dart test`, module-boundary,
AOT-exe smoke, ch07 cluster projects) are outside A–K and outside the Gleam M1 surface
(dynamic dispatch and multi-isolate are US4/US5; the AOT exe is Dart-specific). **OUT.**

**Why F–K are OUT, precisely:** they load a *directory* (`project/` static linking with a
per-directory `self.glp` chain and `M # goal` resolution). The Gleam engine's `load`
takes a single source and merges only the **root** prelude — there is no directory
walker, no per-directory `self.glp` chain, no static project linker, and no dynamic
`#`-dispatch service loop. These are legitimately later milestones, not GAPs in the
GAP-Gn sense (a GAP-Gn is a *semantic* faithfulness hole in the shared M1 surface).
They are recorded here as **out-of-M1-surface**, not as parity failures.

---

## 3. Section A — per-block notes and expected-GAP flags

All A-blocks are pinned in `corpus.list`. Most exercise the ported three-phase engine,
guards, and body kernels and are **expected to reach parity**. The blocks below touch
predicates the Gleam MVP does **not** yet port; they are pinned so the runner *surfaces*
the divergence, which is then classified per Bug Protocol at T042 (fix-port / record-GAP
/ escalate) — **never** golden-fudged. The flag here is a prior expectation, not a verdict.

| Block | Content | Flag |
|---|---|---|
| A16 | `abs/sqrt/pow/floor/ceil` arithmetic kernels | **likely GAP** — Gleam `kernels.gleam` ports only `_add/_sub/_mul/_div/_idiv/_mod/_neg` (T024); `idiv` case should still agree |
| A22 | `wait_test` (`wait`/`wait_until`) | **likely GAP** — surfaced `Unimplemented` (effectful; out of pure-engine MVP, T024) |
| A23 | difference-list terms + `\` operator | verify — DiffList sugar / `\` parse on Gleam unconfirmed |
| A26 | `=..` (univ) + `mwm` | **verify** — `=..` univ support on the Gleam goal path unconfirmed; `:=` arithmetic sub-cases should agree |
| A27 | `med/5` goal with a **struct-inside-a-list** argument | **GAP** — Gleam goal-boot deferred structs-inside-lists (restart note); Dart REPL itself documents this limitation (known-issues) |
| A29/A30 | 049 policy guard `satisfiable/2` (forms a & b) | **likely GAP** — the 049 policy-guard predicate is not in the US1 port; form-a uses a Dart-only env toggle |

Blocks A1–A15, A17–A21, A24* , A25, A28 are **expected parity** (core lists, arithmetic
via `:=`, structural/guard/suspension behaviour, defined guards, ground-equal, negation,
standard-order comparison, `atom/1`, `=\=`). Any divergence there is a port bug to fix
(T042), not a GAP.

---

## 4. GAP / FORK named cases (FR-011 gate — T040)

Parity may **not** be declared until these exist as named programs in
`programs/tests/typed/` (single corpus home, no copies) and are recorded here. Defined by
`docs/research/glp-gleam-baseline/pipelines/P2-concerns/REGISTER.md`:

| id | REGISTER row | What it pins | Corpus program (T040) |
|---|---|---|---|
| **GAP-G1** | C-11 | `ground/1` SRSW relaxation — a var grounded by a guard may be read multiply; the SRSW checker must **not** reject | `gap_g1_ground_relax.glp` |
| **GAP-G2** | C-12 | clause-head standardize-apart per reduction + recursion non-aliasing | `gap_g2_standardize_apart.glp` |
| **GAP-G3** | C-13 | fairness/liveness — a perpetually-reducible goal is eventually reduced | `gap_g3_fairness.glp` |
| **GAP-G8** | C-18 | guard three-valued coverage (`=:=`, `<`, type tests, `known`) — succeed/suspend/fail each observable | `gap_g8_guard_three_valued.glp` |
| **FORK-1** | C-19 / C-15 | circular-term deref discriminator — was an **OPEN owner-gated** fork (loud-all vs structural-vs-cycle); **RESOLVED owner-directed 2026-07-13** to structural cycle detection. All three runtimes now render `<circular>` at the revisit — an **AGREEING** block | `fork_1_circular_deref.glp` |

FORK-1 was owner-gated (must NOT be Claude-decided). It was **RESOLVED owner-directed
2026-07-13** in favour of structural cycle detection: the Gleam envelope deep-resolve now
detects a revisited variable on the deref path and emits a `<circular>` marker (the Dart/C#
REPL deref behaviour), so all three runtimes converge on `f(f(<circular>))` /
`pair(a, pair(a, <circular>))`. `fork_1` is therefore an ordinary **agreeing** corpus block;
`test/parity/expected.list` carries no classification.

---

## 5. Divergence protocol (restated from the contract — never fudge)

On any divergence between a Gleam run and its golden: **STOP**, report three-way (golden /
Gleam output / spec anchor), classify per Bug Protocol —
1. **Gleam port bug** → fix the port.
2. **Dart bug** → report; do **not** mirror the bug into Gleam or the golden.
3. **Spec gap / frozen-semantics gap** → escalate (§1.14 / Constitution IV-a).

A golden is **never** edited to make Gleam pass. Re-recording goldens is explicit (rerun
`record_dart_goldens.sh`), never implicit.

## 6. Parity watch-outs baked into the shared normalizer (restart-note item 5)

The Gleam REPL renders bindings from the 038 `ResultEnvelope`, so three *rendering*
differences (not semantic divergences) are absorbed by `lib/normalize.sh`, identically on
both sides:
- (a) an unbound query var has **no** heap-only reader `?` on the Gleam side (Dart formats
  from the live heap and may show `?`); Dart also prints a fully-unbound result as
  `<unbound>` where Gleam prints `X<id>` — the normalizer canonicalizes both.
- (b) bound vs unbound query vars are split-ordered in the envelope (`resolved_bindings`
  then `var_to_writer`), so a mixed multi-var goal lists the same bindings in a different
  order than Dart's single ordered map — the normalizer **sorts the binding lines** (set +
  status is the parity signal, not order) and **stabilizes variable numbering** (internal
  ids `X<n>`/`_G<n>` → consistent `_V<k>`, preserving sharing like `ch(_V1,_V2)/ch(_V2,_V1)`).
- (c) `:trace` lines are best-effort reference shape and are **not** part of a recorded
  goal outcome (traces are a REPL affordance, not an envelope field).

`agent_id` is provisionally `"gleam"` (engine.gleam:52); it appears only in var→writer /
suspended envelope entries, never in a bound-only outcome. If a suspended-var case is
recorded where it would surface, pin a parity value then (do not guess).
