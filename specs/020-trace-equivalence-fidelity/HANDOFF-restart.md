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
- **T017(ii) DONE — commit `8e8dccf4`.** New hand-written `out/csharp/lib/runtime/equiv_trace.cs`
  (`GlpRuntime.EquivTrace`, registered via an explicit `<Compile>` in the csproj — NOT
  Converted.props) emits `normalize.py`'s canonical EV/OUT wire format at the runner seams:
  BYTECODE_OP @ the dispatch loop (the spine), WRITER_BIND + UNIFY-success + REACTIVATE @
  `ExecCommit` (post-`ApplySigmaHatFCP`), SUSPEND + UNIFY-suspend @ `ExecNoMoreClauses`/
  `ExecSuspendEnd`, UNIFY-fail on definitive fail; OUT @ `glp_engine` RunGoalAsync + conjunction
  (with `_StatusWord`: succeed|suspend|fail — note OUT uses "succeed" but UNIFY uses "success").
  **Flag-gated by `GLP_EQUIV_TRACE`=<file>**; off → one cached-bool no-op, behaviour + perf
  unchanged (verified: append OFF = `[a,b,c,d]` succeeds; ON = correct output + a clean canonical
  trace file). Dart golden untouched (R10/HARD GATE 6).
- **FINDINGS from the first real capture:**
  1. **[RESOLVED — option-a, commit `593cb989`]** Dart `:debug` is a PARTIAL op-spine (silent on
     `Label`/`HeadNil`/`Otherwise`, the body ops, and the conditionally-printed `GetValue`); the C#
     emitted the FULL spine. Fixed by aligning the C# BYTECODE_OP emission to the 14 Dart-observable
     ops via a `_spineOps` allow-list in `equiv_trace.cs` (`ClauseTry, Push, Pop, UnifyStructure,
     HeadStructure, UnifyVariable, GetVariable, Commit, NoMoreClauses, Guard, Ground, NoReaders,
     GroundEqual` — GetValue EXCLUDED: its Dart print fires only in the VarRef-alias sub-case,
     runner.dart:2037). Verified: the append recursive + base clause spines now match the golden
     EXACTLY. Dart prints `COMMIT`, C# emits `Commit` → parse_dart lowercases/maps at T022.
  2. **[RESOLVED — commit `bfa32841`; NOT a runner bug]** The "Ground→Commit" spine difference was
     NOT a behavioural divergence: BOTH REPLs execute `Commit(pc7)` and take the `resolvedSi` early
     soft-fail there; the Dart simply does not PRINT `COMMIT` on that path (its COMMIT print,
     runner.dart:2400, is past the resolvedSi check). So `Commit` is conditionally observable (like
     GetValue). Fixed by removing `Commit` from the dispatch-loop allow-list and emitting it via
     `EquivTrace.OpAt("Commit", pc)` from inside `ExecCommit` ONLY past the resolvedSi check (the
     commit-proceeds point). Verified: proceeding commits (append pc16/pc5) emit; the serve early-exit
     commit (pc7) is suppressed → the full append spine now matches the Dart-observable spine EXACTLY
     across all three goals. `ExecGround` is correct; no runner change was needed.
  3. **OUT binding shape is shallow** (`Zs=./2(var,var)` not the full `./2(const(a),...)`): `ShapeOf`
     does not deref VarRefs through the heap. Fine for status; for outcome-mode binding fidelity, OUT
     should pass a recursively-deref'd term. Easy refinement, deferred.
- **REMAINING**: T022 e2e → T031 fidelity-metric swap + GEPA re-run → T026–T029 equiv CLI/skill/gate.

### T022 scoping (2026-06-03, in progress) — `parse_dart` finalization + normalization
`verdict.compare_recorded` calls `parse_dart(golden_text)` + `parse_csharp(candidate_text)` →
`relation.compare`. `parse_csharp` is DONE (C# emits canonical EV/OUT). `parse_dart` still expects
canonical format — it must be FINALIZED to adapt the Dart `:trace`+`:debug` text:
- BYTECODE_OP from `[DEBUG] PC X: <Op>` lines — COLLAPSE consecutive same-(pc,op) sub-lines into ONE
  event; include the SAME 13-op allow-set as the C# (+ Commit, which Dart prints only on a proceeding
  commit — already symmetric with the C# OpAt); SKIP GetValue (C# excludes it).
- WRITER_BIND from the `  Wx → shape` lines under a COMMIT print; CANONICALIZE the Dart term display
  (`./2(Var@10, Var@12)`) to the C# address-free shape (`./2(var,var)` / `const(a)`).
- SUSPEND from `NoMoreClauses - SUSPENDING on readers: [..]`; UNIFY synthesized (success@COMMIT,
  suspend@SUSPENDING) to mirror the C#; OUT from the `Var = val` + `→ status` lines.
- 🔴 **Normalization decision (touches contract "compared fields")**: the `goal` field in
  SUSPEND/REACTIVATE is NOT recoverable from the Dart text (no numeric goal id) and the two REPLs use
  different id schemes — so `goal` must DROP OUT of the compared model (compare SUSPEND by `reader`
  only). relation.STRICT (`_event_eq`, full payload) + DYNAMIC (`_payload_no_vars` keeps `goal`) BOTH
  currently compare `goal`. Plan: drop `goal` from the emitted/compared payload on BOTH sides
  (C# `equiv_trace` + `parse_dart`), OR add it to the dropped set. Recommend dropping `goal`.
- append is `tier: strict` (corpus.yml) → STRICT = full-event-list positional equality, so the
  synthesized UNIFY + WRITER_BIND events must align positionally with the C#. Build `parse_dart`
  against the real captured append pair (Dart `:trace`+`:debug` text + the C# canonical EV/OUT) as the
  first e2e fixture; the live-spawn capture backend (T018, now unblocked by T017) + bonds outcome-mode
  follow.

### T022 — DONE so far + TURNKEY `parse_dart` build spec (next increment)
**DONE**: goal kept via `GoalId` relabeling (commit `063717c7`, separate `g`-namespace; 34 equiv pure
tests green). Matched fixtures committed (`33c1e08b`): `codeconv/tests/fixtures/equiv/append_csharp.txt`
(the C# canonical EV/OUT, 28 events + OUT) + `append_dart.txt` (the Dart `:trace`+`:debug`, 76 lines).
`parse_csharp` already produces the right model from the C# fixture.

**TODO — `parse_dart` (replace the stub that just calls `_parse(..., "dart")`)**: adapt the Dart text →
the SAME model as the C# fixture. Line-by-line mapping (append fixture line → target event), VERIFIED:
- `[DEBUG] PC <pc>: <Op> …` → ONE BYTECODE_OP per dispatch: COLLAPSE consecutive same-(pc,op) sublines
  (GetVariable prints 3-7, HeadStructure 2-5). Use the SAME 13-op allow-set as the C# `_spineOps`;
  SKIP `GetValue`; map `COMMIT`→`Commit`.
- IGNORE: `[DEBUG _finalUnboundVar] …`, reduction lines `<head> :- <body>`, and the secondary COMMIT
  lines `… COMMIT - Applying …` / `… COMMIT - Applied successfully, reactivating N goal(s)` (keep only
  the FIRST `COMMIT - σ̂w contains N bindings:` line for the Commit op + read the `reactivating N` count).
- COMMIT block: first `PC <pc>: COMMIT - σ̂w contains N bindings:` → BYTECODE_OP Commit, THEN UNIFY
  success (vars = the `W#` writers in order), THEN one WRITER_BIND per `  W# → <shape>` subline, THEN
  N REACTIVATE from `reactivating N goal(s)`. (Early-soft-fail commits print NO COMMIT line → none, matches C#.)
- `[DEBUG] NoMoreClauses - SUSPENDING on readers: [r1, …]` → UNIFY suspend (vars=readers) + one SUSPEND
  per reader; `goal` = the goal token from the FOLLOWING `<goal-display> → suspended` line (relabeled g_i).
- OUT: the `Var = <value>` lines + the `→ succeeds|suspended|failed` line.
- **Shape canonicalizer** `dart_display → C# ShapeOf form` (recursive): `Const(x)`→`const(x)` (incl
  `Const(nil)`→`const(nil)`); `Var@n`→`var`; `./2(a,b)` AND `.(a,b)`→`./<arity>(<canon args>)`; **GLP list
  syntax** `[a, c]`→nested `./2(const(a),./2(const(c),const(nil)))`, `[a | X?]`→`./2(const(a),var)`.
- **C# OUT-shape deref fix (finding #3, REQUIRED for strict OUT match)**: the C# OUT binding shape is
  shallow (`Zs=./2(var,var)`) because `ShapeOf` doesn't deref VarRefs through the heap; the Dart OUT
  shows the full `[a, c]`. Fix the engine OUT emission to pass the RECURSIVELY-dereferenced term so the
  C# shape becomes `./2(const(a),./2(const(c),const(nil)))`, matching the canonicalized Dart `[a, c]`.
- Then `test_equiv_oracle_e2e.py`: `verdict.compare_recorded(read(append_dart), read(append_csharp),
  compare_mode="trace", tier="strict")` → `.equivalent is True`. Add a bonds outcome-only case next.
NOTE: rushing this ~250-line load-bearing adapter was deferred at the tail of the 2026-06-03 session
(quality gate); the mapping above is exhaustive — build it directly against the two committed fixtures.

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
