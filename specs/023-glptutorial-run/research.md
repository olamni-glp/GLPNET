# Phase 0 — Research: /glptutorial-run

**Feature**: `023-glptutorial-run` | **Date**: 2026-06-04 | **Spec**: [spec.md](./spec.md)

This document resolves every technical unknown for the run-&-explain feature. All
four spec Clarifications (2026-06-04) are already resolved; the decisions below
translate those resolutions into concrete engineering choices. No
`NEEDS CLARIFICATION` markers remain.

Format per decision: **Decision** · **Rationale** · **Alternatives rejected** ·
**Spec trace**.

---

## D1 — Engine hosting & bridge-free invariant

**Decision.** Implement run/preview/explain/propose as **new subcommands on the
existing feature-022 `tutorials` Typer sub-app** (`codeconv/src/codeconv/tutorials/`),
replacing the reserved `run` stub in `tutorials/cli.py` (currently exits 64). The
new behaviour stays **pure / bridge-free**: it reads files and shells out to a REPL
subprocess; it MUST NOT import `dbos`, `sqlalchemy`, `psycopg`, or
`codeconv.{bridge_client,runner,db}`. It is wired through the existing direct
`app.add_typer(tutorials_app, name="tutorials")` in `codeconv/cli.py` — never
through `runner.tool_registry()`.

**Rationale.** Feature 022 already made `tutorials` bridge-free by deliberate
design, guarded by `codeconv/tests/test_tutorials_no_bridge.py`. Running a tutorial
needs no database: it is filesystem reads + a subprocess + stdout parsing. Reusing
the sub-app reuses the whole corpus-discovery layer (`load_corpus`, `match_tutorial`)
as the selection front-end (FR-001) and keeps one consistent CLI/skill surface with
`/glptutorial-list`.

**Alternatives rejected.** (a) A new auto-discovered tool subpackage under
`codeconv/src/codeconv/tools/` — would acquire the bridge + DBOS via `tool_registry`,
violating the bridge-free invariant for no benefit. (b) A standalone package — new
packaging, duplicates the corpus layer, breaks the single CLI surface.

**Spec trace.** FR-001, FR-014, FR-015; reuses 022's `tutorials/cli.py` `run` stub.

---

## D2 — Chapter-shape detection & load-target kind

**Decision.** Detect shape **from the corpus model already produced by
`load_corpus`**: an `Exercise` whose `scripts` tuple is **non-empty** is
**section-driven** (load the single `.glp`); an `Exercise` with **empty** `scripts`
but a present `ex-MM-tutorial.md` guide is **use-case-driven** (resolve to a
module-project + play goal). A chapter that is a stub (only `chNN-sources.md` +
`spec-rev-eng-input/`, no `exercise-MM` with runnable content) reports
**"not yet available"**.

**Rationale.** The 022 model already records `Exercise.scripts` and
`Exercise.md_description`; the empty-vs-nonempty `scripts` set is exactly the
"`(no scripts)`" signal the 022 lister surfaces for ch07. No new walk is needed —
the run layer adds a *resolver* on top of the existing model.

**Alternatives rejected.** Re-walking the filesystem with new shape heuristics —
duplicates 022 and risks divergence between what `list` shows and what `run`
resolves.

**Spec trace.** FR-002, FR-003, FR-011; SC-007.

---

## D3 — Resolving load target + goal(s) from documentation

**Decision.** Resolve BOTH the load target and the goal(s) by **parsing the
exercise's `ex-MM-tutorial.md`** for fenced REPL blocks:

- **Load target** = the argument of the guide's load line. The guide shows a
  `GLP>` prompt whose first non-`:` line is either a path ending in `.glp`
  (section-driven → single-file load) or a directory path (use-case-driven →
  project load). The resolver normalizes that target against the configured
  execution roots (D4) rather than trusting the absolute path baked into the `.md`.
- **Goal(s)** = the `GLP> <goal>.` lines in the guide that are not REPL meta
  commands (not starting with `:`) and not a load line. For section-driven
  exercises there are typically several inspection goals; for use-case exercises
  the **play goal `fplayMM.`** (the final "full play" step) is the primary goal and
  the per-step component goals are secondary.
- When a guide documents **multiple goals**, the tool lists them and lets the user
  choose one or run them in sequence (FR-004). When **no goal** is resolvable, the
  tool says so and accepts a user-supplied goal (FR-004 / edge case).

**Rationale.** This is the single unified mechanism that makes both shapes behave
identically (the spec's hard requirement): the only difference is whether the
parsed load target is a file or a directory. It is data-driven (honours FR-004
"resolve goals from documentation") and degrades gracefully. The guide format is
stable and machine-parseable — confirmed against `ch01/exercise-01/ex-01-tutorial.md`
(single `.glp`, goals like `merge([1,2,3],[a,b],Xs).`) and
`ch07/exercise-01/ex-01-tutorial.md` (project load + `fplay1.`).

**Alternatives rejected.** (a) Hard-coded per-exercise goal table — brittle, drifts
from the corpus, fails FR-004's "from documentation" intent. (b) Inferring goals
from `.glp` source — section-driven exercises expose no top-level goal in code, and
use-case plays live in a separate project file.

**Spec trace.** FR-003, FR-004, FR-005; edge cases "multiple goals", "goal not
resolvable".

---

## D4 — Hybrid corpus model & sibling execution roots

**Decision.** **Selection/discovery reads the vendored snapshot**
(`tutorials/olamni/`, the 022 default) via `load_corpus`. **Execution resolves the
load target against the sibling repo in place.** Two independently-configurable
execution roots are needed, because the use-case projects live *outside* the
tutorial corpus:

1. `--sibling-corpus` (default `D:/bstdev/research/glp/GLP/olamni/tutorial`) — the
   sibling copy of the tutorial corpus; the execution root for **section-driven**
   `.glp` files (the same relative path as the vendored snapshot).
2. `--sibling-glp-root` (default `D:/bstdev/research/glp/GLP`) — the sibling GLP
   repo root; the execution root for **use-case module-projects**, which the
   guides load from `programs/cssg_modules/` (NOT from inside the tutorial corpus —
   see D5).

A **drift guard** (`codeconv tutorials sync --check`, already implemented in 022)
keeps the vendored snapshot aligned with the sibling corpus so the example
*selected* is the example *executed*; on detected drift the tool **warns and
refuses to run a mismatched example** rather than running silently.

**Known gap (recorded, not silently swallowed).** `sync --check` covers the
vendored tutorial corpus but **NOT** `programs/cssg_modules/`, which is not
vendored. For ch07 the drift guarantee is therefore weaker: the guide is vendored
but the project it loads is the live sibling `programs/`. This is surfaced as a
**risk** and as a **proposal candidate** (D9: vendor `cssg_modules/` or add a
run-manifest), never as a silent assumption.

**Rationale.** Directly implements the Clarification's hybrid resolution. Splitting
the two roots is forced by D5's finding that the canonical ch07 substrate is
`programs/cssg_modules/`.

**Alternatives rejected.** (a) Execute from the vendored snapshot — the snapshot's
`ch07/cssg-modules/` is a *stale derivative* (D5); running it would diverge from the
documented, validated substrate. (b) Single execution root — cannot address a
project that lives outside the corpus tree.

**Spec trace.** FR-012; Dependencies ("hybrid corpus model"); edge case
"Corpus unreachable / unknown identifier".

---

## D5 — ch07 canonical substrate & exercise→(project, goal) mapping

**Decision.** Per the authoritative `ch07/ch07_tutorial.md`, the **canonical
runnable substrate for ch07 is the sibling `programs/cssg_modules/`** (a four-module
project: `agent.glp`, `ui/mediator.glp`, `ui/actors.glp`, `boot.glp`, + shared
`self.glp`). The `ch07/cssg-modules/` and `ch07/simple-multimodule/` directories
inside the corpus are **stale derivative copies explicitly marked NOT part of the
runnable content**, and exercises **08–12 are SUPERSEDED (2026-05-04)**. The
runnable use-case set is **exercise-01 … exercise-07**, mapped **1:1 to
`fplay1` … `fplay7`** (the play goal documented in each guide). Module load order
is handled by the REPL's directory-`loadProject` (it resolves SRSW + partial eval +
type check + `imported procedure` links across all modules in one load); no manual
ordering or manifest is required.

**Rationale.** `ch07_tutorial.md` §"Stale prior-implementation artefacts" states
the canonical project is `programs/cssg_modules/` and the in-corpus copies are
superseded derivatives; `ex-01-tutorial.md` loads exactly that path and runs
`fplay1.`. Trusting the in-corpus copy would run abandoned code.

**Alternatives rejected.** Treating `ch07/cssg-modules/` as the load target —
contradicts the chapter's own authoritative signpost; would execute stale code.

**Spec trace.** FR-003, FR-017; the use-case-shape Clarification ("the play is the
goal-within-the-exercise"); SC-002.

---

## D6 — REPL backend abstraction (C# default, Dart on demand)

**Decision.** A backend abstraction with two implementations behind one interface:

- **C# (default, mandated).** The fully-implemented C# GLP REPL produced by the
  Dart→C# conversion at `out/csharp/` (entry project `out/csharp/glp_repl/`, runner
  `out/csharp/lib/bytecode/runner.cs` — verified *not* a stub, all 60 opcode arms
  implemented, feature-020 build-green). Invoked via `dotnet run --project
  out/csharp/glp_repl` (or a built executable when present). This backend MUST
  always be used unless the user explicitly selects Dart.
- **Dart (on demand).** The sibling `glp_runtime` REPL: `dart run bin/glp_repl.dart`
  or the prebuilt `glp_runtime/glp_repl.exe`.

**Non-interactive driving.** Both REPLs are line-oriented loops reading stdin and
printing to stdout. The backend feeds a scripted stdin sequence:

```
<load-target>            # single .glp path OR project directory
:limit <N>               # only when the exercise needs it (plays)
<goal>.                  # one or more goals
:quit
```

and captures stdout. Both backends print the identical outcome grammar (D7), so one
parser serves both.

**C#-failure policy (P1).** A non-working C# backend, or one that yields a wrong
result, is a **critical P1 defect**: surfaced loudly (clearly labelled P1, with the
captured error), never an unexplained hang/crash/silent pass. The tool MAY fall
back to Dart **with a prominent P1 notice** to keep the learner unblocked, but MUST
NOT mask or downgrade the C# failure. A run timeout bounds non-terminating goals so
"hang" becomes a reported P1, not a freeze.

**Rationale.** Directly implements the resolved backend Clarification + FR-007 /
FR-018 and Gabi's 2026-06-04 directive (the C# REPL is the mandated default, not a
stub). Identical stdout grammar across backends is what makes outcome-only
comparison backend-agnostic.

**Alternatives rejected.** (a) Dart-effective-default — contradicts FR-018. (b)
Silent fallback to Dart — masks the P1 the spec demands be surfaced. (c)
Programmatic in-process invocation of the C# runner — crosses the Python/.NET
boundary needlessly; subprocess + stdout parsing is simpler and uniform with Dart.

**Spec trace.** FR-006, FR-007, FR-018; US5; SC-006; edge case "C# backend fails".

---

## D7 — Outcome-only capture & golden parsing

**Decision.** Capture is **outcome-only** (FR-008, per feature-020 FR-006): from the
backend's stdout, for each executed goal, extract the **binding lines**
(`Name = value`, including `Name = <unbound>`) and the **single status line**
(`→ succeeds` | `→ suspended` | `→ failed`). The step-by-step reduction trace,
bytecode, and suspension/reactivation events are **not** captured. The expected
**golden** is parsed identically from the exercise's `ex-MM-repl-trace.md`: split on
`GLP> <goal>` blocks, take the following binding + status lines.

**Fresh-variable normalization.** Internal variable names (`X60`, `X84`, `X8`, …)
are fresh per session and differ between runs; the guides say so explicitly. Before
comparison the parser **normalizes fresh-var tokens** (e.g. `X<digits>`) to a
canonical placeholder so that two outcomes differing only in fresh-var numbering
compare equal. `→ status` and ground bindings are compared verbatim.

**Rationale.** Matches the established 020 outcome-only convention and the actual
text format confirmed in both `ex-01-repl-trace.md` files. Normalization is
necessary because the goldens themselves note the X-numbers vary per run.

**Alternatives rejected.** Full-trace capture/diff — explicitly out of scope by
FR-008; brittle (interleaving, fresh-var noise).

**Spec trace.** FR-008, FR-009, FR-010; Key Entities "Expected outcome (golden)",
"Run result".

---

## D8 — Explain & comparison verdict

**Decision.** `explain` runs the example, then compares the actual outcome-only
result to the golden and emits one of: **match** (actual ≡ golden after D7
normalization) or **explained difference** (actual ≠ golden — the difference is
shown field-by-field and explained), each **referencing the tutorial `.md`** prose.
A `→ suspended` outcome is reported as a **valid** outcome wherever the guide
documents suspension (e.g. plays with escrow timers / steady-state waits), not as a
failure. A difference is **always surfaced** — never a silent pass.

**Rationale.** Implements US4 + FR-009 + FR-010. The guide prose (already extracted
per-exercise via 022's `describe` + the full `.md`) supplies the human explanation
anchor.

**Alternatives rejected.** LM-generated explanations on the production path —
unnecessary and against the bridge-free / no-LM-on-production-path discipline 022
established; the explanation is assembled from the guide text + the verdict.

**Spec trace.** FR-009, FR-010; US4; SC-005; edge cases "Outcome differs from
golden", "Suspended outcome".

---

## D9 — Restructuring proposals (read-only default, approval-gated apply)

**Decision.** `propose` is **read-only**: it emits a normalization report/map of
corpus inconsistencies and suggested improvements WITHOUT mutating any file.
Concrete proposal classes for this corpus:

- An explicit **ch07+ exercise → (project, goal) run-manifest** (closes the
  "no resolvable mapping" edge case deterministically).
- Flag the **`programs/cssg_modules` vs vendored `ch07/cssg-modules` drift gap**
  (D4 known gap): propose vendoring `cssg_modules/` or recording a manifest.
- Flag **stale/superseded** artefacts (ch07 exercise-08…12, `simple-multimodule/`)
  for disposition.
- Layout normalisation where section-driven exercises diverge.

**Applying** a proposal (`propose --apply`, FR-019) requires **explicit
engineer/operator approval per example + a recorded improvement rationale**; it
targets the **sibling source of truth** then re-vendors (`tutorials sync`); it is
**layout/metadata-level only** (preserves program semantics and book-exact clause
text); and each change is **independently revertible**. Absent approval, nothing is
mutated.

**Rationale.** Implements the A+B-with-gated-C Clarification + FR-013 / FR-019 and
the CLAUDE.md spec-first / no-unapproved-`.glp`-edit discipline.

**Alternatives rejected.** (a) Auto-applying normalisations — violates FR-013/019
and the corpus charter. (b) Read-only-only with no apply path — under-delivers the
resolved scope (gated C).

**Spec trace.** FR-013, FR-015, FR-019; Assumptions ("Restructuring = read-only
proposals + approval-gated apply").

---

## D10 — CLI / skill contract & exit codes

**Decision.** Surface three learner verbs + one maintenance verb on
`codeconv tutorials`: **`preview`** (FR-005, no execution), **`run`** (FR-006/008,
execute + report actual outcome), **`explain`** (FR-009, run + compare + explain),
**`propose [--apply]`** (FR-013/019). All accept the **chapter + exercise** selector
(the uniform unit, both shapes) consistent with 022's matcher; `run`/`explain` accept
`--goal`, `--backend cs|dart`, `--json`. The `/glptutorial-run` skill is a **thin
forwarder** producing equivalent behaviour (FR-014), exactly like
`/glptutorial-list`. Exit codes **extend** 022's set:

| code | meaning |
|---|---|
| 0 | ok |
| 3 | no tutorial match (reused) |
| 4 | ambiguous match (reused) |
| 5 | corpus unreachable (reused) |
| 6 | example has no resolvable load target |
| 7 | no resolvable goal (and none supplied) |
| 8 | selected backend unavailable / C# P1 defect |
| 9 | chapter/example not yet implemented |
| 10 | goal hit a documented REPL limitation |
| 11 | snapshot/sibling drift detected — refused to run |

**Rationale.** Reuses 022's selection codes for consistency; adds run-specific codes
so every FR-016 error class is distinguishable by callers and tests. Three learner
verbs map 1:1 to US3/US1+US2/US4 for independently-shippable stories.

**Alternatives rejected.** Folding explain into `run --explain` only — workable, but
a dedicated `explain` keeps the user-story slices clean and the learner verb
obvious; `run` still emits a brief verdict so the capability composes.

**Spec trace.** FR-014, FR-016; all user stories; edge cases enumerated in FR-016.

---

## D11 — Testing strategy

**Decision.** pytest in `codeconv/tests/`, fixture-driven and mostly hermetic:

- **Resolver/parser units** (no REPL): shape detection (D2), load-target + goal
  extraction from guide `.md` (D3), golden parsing + fresh-var normalization (D7),
  verdict logic (D8), proposal generation (D9) — against a shaped fixture corpus
  (section-driven single + multi-script, use-case guide, stub chapter, no-goal,
  multi-goal, superseded exercise).
- **Bridge-free guard**: extend `test_tutorials_no_bridge.py` so the new modules
  (`resolve`, `backends`, `outcome`, `explain`, `propose`, plus `cli`) are covered
  by both the AST import-surface check and the clean-subprocess `sys.modules` check.
- **Backend integration** (gated on backend availability): a small fake/echo backend
  for hermetic outcome-parsing tests, plus an opt-in real-backend test that runs
  `ch01/exercise-01` and asserts the outcome matches its golden — skipped when the
  C# build (or Dart REPL) is absent, reported (never silently passed).
- **Skill≡CLI parity** test (FR-014), mirroring 022.

Baseline discipline (CLAUDE.md Test Protocol): the GLP REPL suite
(`test/run_all_tests.sh`) is untouched by this Python feature; run the codeconv
pytest suite green before/after.

**Rationale.** Matches 022's proven pure-unit + guarded-invariant approach; the only
new wrinkle is the backend, isolated behind a fake for hermetic tests and an opt-in
real run for fidelity.

**Alternatives rejected.** Mandatory real-REPL runs in CI — non-hermetic, depends on
a built C# solution; kept opt-in/gated instead, with explicit skip reporting.

**Spec trace.** FR-014; SC-003 (≥90% golden match for implemented examples); SC-007.

---

## Resolved unknowns summary

| Unknown | Resolution |
|---|---|
| Where does the engine live? | Extend bridge-free `tutorials` sub-app; replace `run` stub (D1) |
| How to tell the two shapes apart? | `Exercise.scripts` empty vs non-empty (D2) |
| Where do goals/load targets come from? | Parse `ex-MM-tutorial.md` REPL blocks (D3) |
| Vendored vs sibling at run time? | Select vendored, execute sibling-in-place; two roots (D4) |
| ch07 load target? | Sibling `programs/cssg_modules/`; ex-MM→fplayMM (D5) |
| Which backend, how invoked? | C# default via `dotnet run`; Dart on demand; piped stdin (D6) |
| Outcome capture format? | Bindings + `→ status`; fresh-var normalized (D7) |
| Explain mechanism? | Compare to golden, reference `.md`, surface diffs (D8) |
| Restructuring scope? | Read-only proposals; approval-gated apply (D9) |
| CLI/skill + errors? | preview/run/explain/propose; exit codes 0–11 (D10) |
| Testing? | Fixture units + bridge guard + gated backend + parity (D11) |

All `NEEDS CLARIFICATION` resolved → ready for Phase 1 design.
