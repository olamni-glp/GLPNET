# 🔴 SAFE-RESTART HANDOFF — 020-trace-equivalence-fidelity (2026-06-03)

**READ THIS FIRST after the CLAUDE.md start-of-session ritual.** It is the single
authoritative "what's true, what's next, what must not drift" doc for the remaining
work. The detailed phase history lives in `docs/current_plan.md` (the ledger); this
doc is self-sufficient for resuming **safely** without losing fidelity.

Branch: `020-trace-equivalence-fidelity` · Anchor commit: **`79d9add5`**-and-later
(re-check `git log -1`). NOT pushed (Gabi merges). Canonical PGLite cluster:
`C:/pglite/research/glpnet`.

---

## 0. One-line state

Stages 1–4 of the `/codeconv-runner` 5-stage plan are **DONE + committed**. **Only
Stage 5 remains** (T017 C# REPL trace instrumentation → T022 e2e → T031 fidelity-metric
swap + GEPA re-run → T026–T029 equiv CLI/skill/gate). Stage 3 (runner.cs) unblocked it.

### Progress 2026-06-03 (Stage 5 session in progress)
- **T017(i) DONE — commit `7c3def56`.** `out/csharp/glp_repl/Program.cs` placeholder replaced
  by a thin delegating entrypoint → `glp_repl.exe` now RUNS the converted REPL
  (`GlpRuntime.Repl.Program` in the library). The exe edit does NOT touch Converted.props /
  the 75-file frontier. Self.glp loads; outcome reporting works.
- **First runtime fidelity bug FOUND + FIXED — commit `2cab78db`.** The oracle premise paid
  off on the first executing reduction: `append([a,b],[c,d],Zs)` gave wrong status on C#
  (`[a|X11]`/failed) vs Dart `[a,b,c,d]`/succeeds. Root cause was NOT a runner.cs arithmetic
  bug — it was a **stub-era gap in `scheduler.cs` `DrainWithStatus`**: `hadReduction` was
  hardcoded `false` (the `onReduction` callback was stubbed out while runner.cs was a
  placeholder), so every `RunResult.Terminated`-after-reduction (a SUCCESS) hit
  `hasFailed=true`. Fix wires the full `RunnerContext(... onReduction ...)` ctor + calls
  `RunWithStatus(cx)`, mirroring `scheduler.dart` lines 258-289. runner.cs reduction logic
  was correct. Verified C# ≡ Dart golden on append/reverse/quicksort (succeeds, exact bindings).
  ⚠️ **Carry-forward**: `scheduler.cs`'s convspec/plan was authored when runner.cs was a stub,
  so a naive re-codegen of scheduler.cs would REINTRODUCE this. Update its convspec/plan/prompt
  before any regen (follow-on, not yet done).
- **REMAINING in T017**: part (ii) — the canonical EV/OUT trace emission (the 5 R1 event kinds)
  in the converted runner, candidate-side, per `contracts/trace_normalization.md`. Design input
  = the Dart golden's live `:trace`+`:debug` text on a real reduction (now possible — reductions
  succeed). Then T022 e2e.

## 1. Verified-green anchor (re-verify BEFORE touching anything)

At handoff, ALL of these were green — **RE-VERIFIED still green 2026-06-03 (fresh session):
build 0 err · pure subset 36/36 · frontier 74/1/0**. On restart, reproduce them to confirm
no drift, then resume. Do NOT stack new work on a red baseline (CLAUDE.md Test Protocol).

```
# (a) Full C# solution builds — runner.cs included (74 built + 1 no_emit).
cd D:\bstdev\research\glp\glpnet\out\csharp
dotnet build glp_runtime_net.sln --nologo -v quiet        # → 0 errors (~5s)

# (b) Session-touched Python pure tests (Stage 1 + Stage 4).
cd D:\bstdev\research\glp\glpnet\codeconv
.venv\Scripts\python.exe -m pytest -p no:randomly -p no:xdist -q -o addopts="" \
  tests/test_codegen_opt_subsystem.py tests/test_codegen_opt_metric_mocked.py \
  tests/test_codegen_prompt_artifact.py tests/test_fidelity_metric.py \
  tests/test_codegen_no_emit.py tests/test_migration_0009_single_head.py   # → 36 passed

# (c) Conversion frontier (bridge; canonical cluster).
#     🔴 RUN FROM REPO ROOT (D:\...\glpnet), NOT from codeconv\ — codeconv\ has its own
#     pyproject.toml, so repo-root detection stops there → bridge-script lookup fails
#     (FileNotFoundError pglite_bridge.mjs). Do NOT reuse (b)'s `cd ...\codeconv`.
cd D:\bstdev\research\glp\glpnet
codeconv --data-dir C:/pglite/research/glpnet codegen status --json
#   → files_total:75, built:74, no_emit:1, escalated:0, open_escalations_total:0
```
NOTE: the FULL pytest suite is ~477 tests / ~80 min (bridge-heavy; `test_depgraph_*`
are NOT excluded by `-m "not needs_bridge"` — the marker doesn't filter them). For
iteration use targeted file runs; reserve the full run for a phase checkpoint.

## 2. What's DONE (commits on the branch)

| Stage | Result | Key commits |
|---|---|---|
| 1 — Claude-driven GEPA wiring | per-subsystem dataset/`prompt.load(subsystem)`/skill-loop + `dataset`/`score` CLI + `_base.md`+5 subsystem prompts; `run_optimize` subsystem+seed | `72ca51d1` |
| 2 — GEPA on bytecode (build-only) | real loop run; **build-only metric at ceiling 1.0** for bytecode leaves → prompt frozen unchanged w/ measured provenance | `9506ac81` |
| spec — gepa_optimizer NO-API revision | the spec-first basis Stage 1 implements | `1597cfd6` |
| 3 — runner.cs CONVERTED | 4863-line interpreter → 5740-line `runner.cs` via E1 6-chunk split; full sln green; ingested→built; E1 resolved | `fa8edb5e`→`97a0ffdf`, `6820275e` |
| 4 — first-class `no_emit` | migration `0009`; `status` precedence; `mark-no-emit` CLI; readiness `.satisfied`; goal_queue marked no_emit on canonical; E1 resolved | `66e061b4`, +goal_queue commit |

## 3. OUTSTANDING — Stage 5 (the only remaining work)

Do these in order; each is the gate for the next. Use the canonical cluster + the
discipline in §5. The Stage-3 semantic-risk list (§4.A) is the FIRST thing T017/T022
must exercise — that is the whole point of the trace-equivalence oracle.

- **T017 — C# REPL trace instrumentation** (`@needs_runtime`). runner.cs now builds, so a
  runnable REPL is possible. (i) Wire the converted `out/csharp/lib/bin/glp_repl.cs` (or
  wherever the converted REPL entry sits — `grep -rl "static.*Main\|glp_repl" out/csharp`)
  as the REAL `glp_repl` entry, replacing the placeholder `out/csharp/glp_repl/Program.cs`;
  (ii) add structured trace hooks emitting the R1 event kinds (UNIFY outcome / SUSPEND /
  REACTIVATE / WRITER_BIND / BYTECODE_OP) comparable to Dart `:trace`/`:debug`, **candidate
  side only — the Dart golden is READ-ONLY**, per `contracts/trace_normalization.md`.
- **T022 — e2e** (`@needs_runtime`): known-equivalent pair → exit 0 equivalent; a bonds
  source → outcome-only verdict. `codeconv/tests/test_equiv_oracle_e2e.py`. THIS is where
  the runner's semantic fidelity actually gets tested (build gate was compile-only).
- **T031 — fidelity-metric swap**: rewrite `tools/codegen_opt/metric.py` so the GEPA metric
  returns `dspy.Prediction(score=tools/equiv/fidelity.py score, feedback=DivergenceRecord-as-text)`
  — score IDENTICAL to the production gate (SC-004). Then **re-run the per-subsystem GEPA
  loops** (Stage-2 mechanism, §5) — now there is a real fidelity gradient ABOVE the build
  ceiling, so `bytecode.md` etc. can actually improve. (Build-only `score` CLI stays for
  pre-REPL use.)
- **T026–T029**: `equiv next/status/ingest/retry` + `equiv aggregate-escalations` + the
  `/codeconv-equiv` skill + the `@needs_runtime` strict-tier gate test
  (`test_strict_tier_gate.py`). Most need the runnable REPL (T017) first.

Also pending (Polish, post-Stage-5): T046–T050 (FR-017 surfacing, tombstone round-trip,
docs, full-suite green commit, SC roll-up).

## 4. 🔴 ANTI-DRIFT CRITICAL FACTS (do not lose — these prevent distortion/fidelity loss)

### A. runner.cs is BUILD-GATE-VERIFIED ONLY — NOT semantically verified
The full sln compiles, but **the build gate is compile-only**. runner.cs's behavioural/
trace fidelity is UNVERIFIED until Stage 5 (T017/T022 trace-equivalence). Do NOT treat
runner.cs as known-correct. The chunk sub-agents flagged these specific **semantic-risk
spots** — exercise them FIRST under T017/T022, and if one diverges, fix runner.cs (NOT
the oracle):
- `ExecGuardNeedReader`/`GuardNeedReaderArg`: the bound-path returns `_Step.Jump(cx.Pc)`
  (re-loops same pc) — verify this is not an infinite loop / matches Dart's advance.
- `ExecV2SetVariable` / `ExecV2PutVariable`: ancestor-completion uses raw `+1` reader
  arithmetic (`writerAddr+1`) where `ExecV2UnifyVariable` uses `PairedReaderAddr(...)` —
  matched the Dart line-for-line (runner.dart 2204/2215 vs 1610); confirm the Dart is
  itself consistent (if the Dart is wrong → CLAUDE.md Bug-Protocol report, do NOT silently
  "fix" the C#).
- `ExecSetConstant` / `ExecPutStructure`: nested-ancestor structure-completion loops.
- `_evaluateArithmetic`: Dart `num` → C# `double` widening; integer-vs-real behaviour.
- `_termsEqual`: `HashSet<(int,int)>` cycle detection.

### B. runner.cs STRUCTURE is intentional — do not "refactor back"
The inline 3700-line `runWithStatus` cascade was **deliberately** refactored to
method-per-arm: a `_Step{Advance|Jump|Stop}` control struct + a `Dispatch(op) switch` +
60 `Exec<Op>(cx, op)` methods + helpers. This is faithful (every branch preserved) and is
what made the 6-chunk split possible. Control-flow mapping convention: Dart fall-through →
`_Step.Advance()`; `pc=X;continue` → `_Step.Jump(X)`; `return RunResult.X` → `_Step.Stop(...)`.
Any runner repair MUST stay in this shape and reuse the existing helpers
(`_softFailToNextClause`, `_findNextClauseTry`, `_suspendAndFail*`, `_dereferenceWithTracking`,
`_evaluateGuard`, `_convertTentativeToStruct`, `_termsEqual`, `MutableArgs`, `NullArgs`).

### C. runner.cs PUBLIC SURFACE is a contract — downstream depends on it
Downstream `.cs` (scheduler, glp_engine, isolate_manager, codegen, linter, bin/glp_repl,
…) were built against the public signatures (`RunStep(cx,env,reductions)`, the enums,
`BytecodeProgram`/`CallEnv`/`EnvironmentFrame`/`RunnerContext`/`ReplModule*` ctors+members,
`BytecodeRunner`). Do NOT change a public signature without updating every caller. StructTerm
built in BODY arms uses a **mutable `List<Term>`** (write-mode arms cast via `MutableArgs`).

### D. GEPA: build-only metric is at CEILING for bytecode — the real gradient is T031
Stage 2 ran GEPA on bytecode and found the build-only metric already at 1.0 (the leaves
compile). Do NOT re-run build-only GEPA expecting prompt improvement — there's no gradient.
The genuine optimization is T031 (swap to `tools/equiv/fidelity.py`, then re-run). The
`bytecode.md` prompt is frozen (`optimizer: gepa-build-only`); T031's re-run overwrites it.

### E. NO API — GEPA LM work runs in Claude only (HARD RULE)
GEPA generation + reflection run as **Claude sub-agents** (Agent tool), never
litellm/openai/`OPENAI_API_KEY`. Contract `gepa_optimizer.md` was revised to this (`1597cfd6`).
A bare `codeconv codegen_opt optimize` with no injected callable exits 2 BY DESIGN. Keep
`tools/equiv/`, `tools/codegen/`, `durable/` import-free of dspy/litellm/openai (T038 guards it).

### F. Decisions locked 2026-06-03 (do not relitigate)
(1) GEPA wired BEFORE the runner; build-only metric OK pre-REPL. (2) NO API (E). These are
why Stage 2 used build-only and Stage 5 does the fidelity swap.

### G. Shared-cluster migrations need explicit Gabi OK
Running `codeconv migrate` against the canonical cluster `C:/pglite/research/glpnet` is a
high-severity shared-resource change — the auto-mode classifier blocks it without explicit
Gabi authorization (it correctly blocked Stage 4 until Gabi granted OK). Migration `0009` is
applied. The next migration (if any) needs the same explicit OK.

### H. GLP authority + oracle fidelity (Stage 5)
Trace event kinds are GLP three-phase (HEAD/GUARD/BODY) + SRSW + writer-MGU semantics — do
NOT invent events; if a needed event is absent from Dart `:trace`, STOP & report. FR-017: if
a divergence traces to a Dart original that violates the GLP spec → CLAUDE.md Bug-Protocol
report; do NOT alter the C# to match a wrong oracle. The Dart golden (`glp_runtime/`) is
READ-ONLY; trace hooks go in the converted C# REPL (`out/csharp/`) only.

### I. Working-tree hygiene — do NOT commit the recovery churn
~129 modified files under `.codeconv/tombstones/` are regenerable re-discover churn from the
2026-06-03 recovery (NOT this work). Leave them uncommitted; stage only files you change, by
name. Never `git add -A`.

## 5. Stage-5 execution recipe (the mechanism that worked)

**runner.cs conversion proved the orchestration pattern** — reuse it for any large Stage-5
synthesis (e.g. the REPL wiring, the equiv CLI): sequential Agent-tool sub-agents, each
given a precise contract, **build-gated between each**, the orchestrator (you) committing
each checkpoint. For the GEPA re-run (T031), drive the per-subsystem loop:
`codeconv codegen_opt dataset --subsystem S --json` → generator sub-agent(s) write candidate
`.cs` to `.codeconv/codegen-prompt/.gepa-scratch/<S>/` (gitignored) → score (post-T031:
fidelity via the runnable REPL; pre: `codeconv codegen_opt score --file … --dep …`) →
reflector sub-agent → `export-prompt --subsystem S --instructions-file … --score …`. Skill:
`.claude/skills/codeconv-codegen-opt/SKILL.md` § "Per-subsystem GEPA orchestration loop".

## 6. Git / merge

Commit by name at each green checkpoint; never merge to `main` (only Gabi). End-of-task
merge template (branch is local-only — push first):
```
git push origin 020-trace-equivalence-fidelity
cd D:\BSTDEV\RESEARCH\glp\glpnet
git checkout main && git pull origin main
git fetch origin 020-trace-equivalence-fidelity
git merge -m "Merge 020-trace-equivalence-fidelity into main" origin/020-trace-equivalence-fidelity
git push origin main
```
