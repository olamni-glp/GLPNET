# Design — Combined comprehensive equivalence test driver + goal-bearing corpus

**Status**: ratified 2026-06-03 (Gabi). Spec-first basis for the T018 live-capture
backend and the T031-part-b GEPA re-run. Extends feature 020; does not alter the
existing pure modules (`normalize`/`relation`/`fidelity`/`verdict`/`recorded`).

## 1. Problem

The fidelity GEPA re-run (T031 part b) and the strict-tier gate (T029) need
**per-source trace-equivalence verdicts** — run a GLP program through the Dart
golden REPL and the converted-C# candidate REPL, normalize both, compare. The
oracle's `capture` step (T018) was stubbed (`_default_capture_backend` →
`needs_agent_work`) for two reasons: (a) the C# REPL had no trace instrumentation
(closed by T017), and (b) **the corpus records source files but no run-goal** —
`corpus.py:CorpusSource` has `path/suite/compare_mode/tier/kind`, no goal. A
behavioural trace requires a goal. This design supplies the goals.

## 2. Goal-bearing inputs (ingested into one corpus-with-goals)

The driver derives `(source, goal, expected_outcome)` from goal-bearing inputs
already reviewed in-repo / in the sibling repo:

1. **REPL suite heredocs** — `test/run_all_tests.sh`, `test/run_book_tests.sh`,
   `test/cssg_modules_test.sh`. Each `$DART run "$REPL" <<HEREDOC` block lists
   `.glp` load paths then goal lines (`append([a,b],[c,d],Zs).`) then `:quit`.
   The goals are the suite's own reviewed exercises.
2. **Sibling GLP tutorial corpus** — `D:/bstdev/research/glp/GLP/olamni/tutorial/`
   (`[[reference_glp_tutorial_corpus]]`). Each `chNN/exercise-MM/` ships the `.glp`
   source, `ex-NN-tutorial.md` (goals as fenced `GLP> <goal>.` lines), and
   `ex-NN-repl-trace.md` (human-approved golden capture). ch01–ch06 are the
   approved single-file strict core.

## 3. Key finding that shapes the design

The tutorial `repl-trace.md` golden captures — and the suite heredoc checks — are
**OUTCOME-ONLY** (`Var = […]` + `→ succeeds|suspended`); they were captured
WITHOUT `:trace`/`:debug`, so they carry **no instruction spine** (no
`[DEBUG] PC N:` / COMMIT / SUSPEND lines). The strict-tier relation needs the
bytecode-op spine + UNIFY/WRITER_BIND/SUSPEND/REACTIVATE events.

⇒ **Two-level use of the validated outcomes** (not a blocker):
- The documented outcome is a **human-validated expected-outcome assertion**.
- The driver **re-captures both REPLs itself** with tracing on (Dart
  `:trace`+`:debug`; C# `GLP_EQUIV_TRACE=<file>`) to get the instruction-level
  traces the oracle compares, and **cross-checks the golden capture's final
  binding/status against the documented outcome** — so goal-misextraction or REPL
  drift is caught, and the corpus stays "human-in-the-loop testable later".

## 4. Ratified decisions (2026-06-03)

1. **Tutorial `.glp` run IN PLACE** via a configured sibling-repo root — never
   copied into `programs/` (respects FR-006 single-source-of-truth). The location
   is recorded in memory (`[[reference_glp_tutorial_corpus]]`).
2. **Expected-outcome cross-check ON**: each ingested goal carries the documented
   `Var=…/→status`; the Dart golden capture MUST reproduce it (else flag, do not
   silently record a drifted golden).
3. **Per-goal recorded entries**: a goal-slug extends the recorded key, so a
   multi-goal source yields one `(source × goal)` entry each (the recorded layout
   already keys per `(key × source)` — add a goal component).
4. **Build order**: ch01–ch06 approved single-file **strict** triples first
   (append/merge/reverse/quicksort/… — exactly the bytecode/runtime-core sources
   the GEPA re-run needs). Defer ch07 project-load multi-goal plays and the
   ch04 ex06–10 "pending review" exercises.

## 5. Architecture

```
ingest (offline, reviewed)                  drive (nondeterministic — CLI/agent layer, R12)
──────────────────────────                  ────────────────────────────────────────────────
suite heredocs ─┐                            for each (source, goal, expected):
tutorial triples┤→ goal-bearing manifest →     spawn Dart golden REPL  (:trace+:debug)   → golden text
                │   (source, goal,              spawn C#   candidate REPL (GLP_EQUIV_TRACE)→ candidate text
                │    expected_outcome,          normalize.parse_dart / parse_csharp
                │    tier, compare_mode)         relation.compare(mode, tier) → Verdict
                                                 cross-check golden outcome == expected   (decision 2)
                                                 recorded.write_entry(key, source, goal, …)
```

- **Goal-bearing manifest**: a reviewed, checked-in artifact (mirrors `corpus.yml`
  g1=c) — `.codeconv/equiv-manifest/goals.yml` (or extend `corpus.yml` rows with a
  `goals:` list). Seeded by a one-shot parser over the suites + tutorials, then
  reviewed (the diff is the review). Each entry:
  `{ source, goal, expected_status, expected_bindings, origin, tier, compare_mode }`.
- **Live capture backend** replaces `_default_capture_backend` (injectable
  `CaptureBackend` seam already exists). Spawns both REPLs per `(source, goal)`:
  - Dart golden: `glp_runtime/glp_repl.exe`, stdin `:trace\n:debug\nload <src>\n<goal>.\n:quit\n`
    (or the proven `printf` form), cwd = repo root.
  - C# candidate: `out/csharp/glp_repl/bin/Debug/net10.0/glp_repl.exe` with
    `GLP_EQUIV_TRACE=<tmpfile>`, stdin `load <src>\n<goal>.\n:quit\n`, cwd = repo root.
  - Captures are NONDETERMINISTIC ⇒ this lives in the CLI/agent layer, NEVER in a
    DBOS step (R12). The durable `equiv` step stays a pure verdict-ingest of the
    recorded artifacts (T024, unchanged).
- **Driver** (`/codeconv-equiv` skill + `equiv capture/next/ingest`, T026/T028):
  walks the goal-bearing manifest in curriculum order, captures, records, compares.

## 6. Relation to existing artifacts

- `corpus.yml` (goal-less `programs/` enumeration) stays as the subsystem/split/
  tier source of truth; the new goal-bearing manifest adds the *goals* (+ the
  tutorial/suite origin + expected outcome). A source may appear in both.
- `recorded.py` layout gains a goal component in the entry key (decision 3).
- `fidelity.py` / `verdict.py` / `relation.py` / `normalize.py` — UNCHANGED (pure,
  already done). The driver feeds them recorded text.
- `metric.py` (T031 part a, done `1142e602`) consumes the per-source verdicts via
  the injected `oracle_fn`; this driver is what produces them for part b.

## 7. Anti-drift (carry-forward)

- Dart golden (`glp_runtime/`) is READ-ONLY; trace hooks are candidate-side only
  (`out/csharp/`). If a divergence traces to a Dart original that violates the GLP
  spec → CLAUDE.md Bug-Protocol report (FR-017); do NOT alter C# to match a wrong
  oracle.
- NO API: any LM work (GEPA generate/reflect in part b) runs as Claude sub-agents
  (`[[project_gepa_no_api_claude_only]]`).
- Nondeterministic capture NEVER in a DBOS step (R12).
- Outcome cross-check (decision 2) guards against silently recording a drifted
  golden — a mismatch is surfaced, not absorbed.

## 8. Increments

1. **Goal-bearing corpus seed + model** (PURE, unit-testable now): parser over the
   suite heredocs + the tutorial `ex-NN-tutorial.md`/`repl-trace.md`; reviewed
   `goals.yml`; `(source, goal, expected_outcome)` model + extraction tests.
2. **Live capture backend** (replaces the stub): spawn both REPLs per `(source,
   goal)`, normalize, cross-check outcome, write recorded artifacts. Behind the
   injectable seam so orchestration stays unit-testable with a fake backend.
3. **Driver wiring** (`equiv capture/next` + `/codeconv-equiv` skill, T026/T028).
4. **Feed part b**: the GEPA `oracle_fn` runs increment-2 capture over a file's
   in-scope sources → per-source verdicts → fidelity score.

## 9. Open follow-ons (not in the first increments)

- ch07 project-load multi-goal plays (`:limit`, `programs/cssg_modules/`) and the
  dynamic/outcome (bonds) tier.
- `_dart_to_wire` REACTIVATE goal-token fidelity for N>0-reactivation commits
  (T022 follow-on; append reactivates 0).
- ch04 ex06–10 once approved.
