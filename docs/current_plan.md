# Current Plan: 020 Trace-Equivalence Fidelity — Implementation (safe-restart ledger)

Started: 2026-05-27
Branch: `020-trace-equivalence-fidelity`
Source of truth for tasks: `specs/020-trace-equivalence-fidelity/tasks.md` (50 tasks, 8 phases)
Handoff: `specs/020-trace-equivalence-fidelity/HANDOFF-implement.md`

---

## 🔴🔴 TOP-PRIORITY MANDATE (Gabi, 2026-06-03): Dart convergence glpnet ⇐ sibling GLP

Bring glpnet `glp_runtime/` Dart to **100% byte-level convergence** with the sibling GLP
repo `D:/bstdev/research/glp/GLP` (authoritative — all tutorials pass there; verified
88/88 modulo fresh-var names + the sibling test suite). Static (byte-identical Dart) AND
dynamic (tutorials + tests pass identically). **Commits + pushes at each checkpoint** for
revert safety (mandated). This is a prerequisite for the oracle: glpnet's Dart REGRESSED
(e.g. `append_and_sum` → failed in glpnet, `Sum=21` in sibling).

**Drift (glp_runtime, sibling vs glpnet) — small, mostly glpnet-behind:**
- lib/ 9 overwrite: `bytecode/runner.dart` (+is_list/+tuple), `compiler/{compiler,analyzer,
  partial_evaluator}.dart`, `engine/glp_engine.dart`, `analysis/type_checker/{prelude,
  program_dfa,well_typed_term}.dart`, `multiagent/repl_play_runner.dart` (glpnet `_v2`
  workaround).
- lib/ DELETE `compiler/unify_result.dart` (glpnet-only refactor; sibling lacks it; imported
  by glpnet analyzer+partial_evaluator → sibling versions drop that import).
- bin/ overwrite `glp_repl.dart` (sibling has `_resolveRootSelfGlpPath` + Windows/abs path
  handling — fixes glpnet's `glp/`-prefix bug); ADD `bin/triage_loader.dart`.

**Steps:** [x] verify sibling green · [ ] baseline+push · [ ] converge Dart + static diff=0
+ commit+push · [ ] rebuild glpnet exe + dynamic verify (88 tutorials == sibling) +
commit+push · [ ] glpnet suite vs sibling (converge programs/test only if a dynamic gap
needs it) · [ ] (rest of scope) C# re-catch-up vs corrected golden + GetVariable emission fix.

**Downstream note:** runner.cs was converted from the OLD glpnet runner.dart → after
convergence it lags the corrected golden; re-catch-up is the "rest of scope", not this task.
Oracle work already committed (goals/capture/driver in codeconv/ + out/csharp/) is untouched
by this convergence.

---

> ## 🔴 RESTART HERE (2026-06-03): read `specs/020-trace-equivalence-fidelity/HANDOFF-restart.md` FIRST.
> Stages 1–4 of the `/codeconv-runner` 5-stage plan are DONE+committed; **only Stage 5 remains**
> (T017 C# REPL trace → T022 e2e → T031 fidelity-metric swap+GEPA re-run → T026–T029). The handoff
> doc has the verified-green anchor, the anti-drift critical facts (esp. runner.cs is COMPILE-verified
> only — semantic fidelity is verified by Stage 5), and the execution recipe.

> This file is the **resumable checkpoint ledger** for a long, multi-cycle session
> (real `dspy.GEPA` + codegen refinement loops). On any restart — fresh session OR
> post-compaction — read this file FIRST, confirm POSITION, verify the last green
> baseline, then resume from the CURRENT marker. Never assume prior in-memory state.

---

## 🔴 RECOVERY 2026-06-03 — DB index lost + scope correction (READ FIRST)

**What happened.** Between the May-28 bulk drive and 2026-06-03 the PGLite **DB index was lost**: both reachable clusters (`<repo>/.pgdb` 38 MB May-28, and canonical `C:/pglite/research/glpnet`) reported `schema_codeconv FAIL` / `dart_depgraph` missing. `.dbsnapshots/` only has a **PG16 May-17 snapshot (predates the drive)** — useless for restore. **No work lost** — all artifacts are committed (out/csharp `.cs`, Converted.props, .sln, 179 tombstones, 130 convspecs/plans, 5 escalations). In codeconv's design tombstones are the durable source of truth; the DB is a rebuildable index.

**Recovery executed (canonical `--data-dir C:/pglite/research/glpnet`):**
- `migrate` ✅ (codeconv + dbos schemas recreated, Alembic→head incl. 0008).
- `discover run` ✅ (179 `.dart` inventoried from the populated `glp_runtime_net/`).
- `depgraph compute` ✅ (**124** in-scope nodes, 1 cycle, ready≈24).
- `depgraph rebuild-conversions-from-tombstones` → 0 upserted/179 skipped (only restores 015 `dart_conversions`, NOT 019 `dart_codegen`).
- **Codegen built-state restore IN PROGRESS** via re-`ingest` of the 73 `build_status: pass` files (build-gate re-verifies; `--no-tombstone-update` to avoid timestamp churn). One ingest ≈12 s.

**🔴 SCOPE: the "124 / 61%" alarm was a RECOVERY ARTIFACT — DISREGARD. True scope = 75; 97.3% was correct.** The fresh `discover` over-counted to 124 only because the lost workspace config dropped two `excluded_directories`: `lib/multiagent/archive-irma-2026-01-30` (49 files) and `test_archive` (11 files). Both re-added via `init run --exclude ... ` + `init add-exclude test_archive`. After recompute: **`files_total=75, built=73, escalated=2, not_started=0`** — exactly the May-28 frontier. All 73 conversions re-verified green through the build gate. **Remaining work = exactly the 2 escalations (runner + goal_queue).**

**RECOVERY COMPLETE (2026-06-03).** migrate ✓ · init run (workspace_settings + 2 manual exclusions + tool-exclusions) ✓ · discover ✓ · depgraph compute ✓ (130 raw / 75 codegen-scope, 1 cycle) · 73 built-state re-ingested+green ✓ · runner+goal_queue escalations re-registered ✓. Side-effect: ~131 tombstones rewritten by re-discover (codegen keys preserved; timestamps refreshed) — regenerable churn, not yet committed. Canonical cluster = `C:/pglite/research/glpnet` (re-confirm it is the intended store; `<repo>/.pgdb` also exists but was schema-less).

**Gabi decisions locked this session (2026-06-03):**
1. GEPA wired **before** the runner conversion (build-only metric is fine — needs no runnable REPL).
2. **NO API / NO `OPENAI_API_KEY`.** GEPA generation+reflection run **in Claude here** (Agent tool / sub-agents via the injectable `generate_fn`/`propose_fn` seams), never litellm/openai. This conflicts with contract `gepa_optimizer.md` T030 ("real dspy.GEPA" + "reads OPENAI_API_KEY") — that contract must be revised to the Claude-driven mechanism.
3. "Continue code conversion as per the codeconv workflow + python tools."

**Revised next-step order:** (a) finish built-state restore → coherent 73-built/2-escalated frontier; (b) **resume the codegen drive over the ~46 unconverted in-scope files** via `/codeconv-codegen` (sub-agent loop) — this is "continue conversion"; (c) wire Claude-driven GEPA (no API), refine the `bytecode`/`runtime-core` prompt; (d) **runner.cs** chunked conversion (6-chunk split per its escalation E1) under the refined prompt; (e) T017 C# REPL trace + T022 e2e; (f) Phase-5 fidelity-metric swap + tests; (g) T026–T029 equiv CLI/skill/gate. goal_queue → first-class `no_emit` status (escalation 1) folded into (b)/(c).

---

## 🟢 SESSION 2026-06-03 (GEPA wiring) — Stages 1+2 DONE; runner is the gate for the rest

Driven via `/codeconv-runner` with the 5-stage plan (= the recovery "Revised next-step
order" (c)→(g)). Delivered this session, committed on `020-trace-equivalence-fidelity`
(NOT pushed; Gabi merges):

- **Stage 1 — Claude-driven GEPA wiring (commit `72ca51d1`)**: T032/T033/T034/T035/T036
  + optimize.py subsystem/seed. See the "Last GEPA artifact written" + "Stage 1 … DONE"
  bullets in POSITION for the full breakdown. 24/24 targeted pure tests green.
- **Stage 2 — GEPA run on `bytecode` (commit `9506ac81`)**: ran the real loop — a generator
  sub-agent regenerated `opcodes.dart`→C# (no peeking) under the `bytecode.md` seed; the
  Python `score` build-gate scored it **1.0**. Existing baseline outputs also build (1.0),
  so the **build-only metric is at its ceiling** for the bytecode leaves → no build-gradient
  → instructions frozen UNCHANGED, `bytecode.md` provenance flipped to `gepa-build-only`
  (metric_score 1.0, dataset_hash bbb9bece11321f97). Honest finding, not a fabricated edit.
  **Real prompt refinement needs the fidelity metric (T031), which needs a runnable REPL.**

### UPDATE 2026-06-03 (cont.) — Stage 3 DONE, Stage 4 code DONE (canonical step gated), Stage 5 unblocked

- **Stage 3 — runner.cs CONVERTED ✅** (commits `fa8edb5e`→`97a0ffdf`, ingest `6820275e`). The 4863-line interpreter → `out/csharp/lib/bytecode/runner.cs` (5740 lines) via the E1 6-chunk split, orchestrated as 6 sequential codegen sub-agents (method-per-arm refactor of the inline 3700-line `runWithStatus` cascade into a `_Step{Advance|Jump|Stop}` dispatch + 60 `Exec<Op>` methods + 6 helpers). Full `glp_runtime_net.sln` builds GREEN (0 errors); ZERO `NotImplementedException`. `codeconv codegen retry+ingest` → **built** (build_status pass); E1 escalation resolved. Frontier: **75 files, 74 built, 1 escalated** (the 1 = goal_queue, addressed in Stage 4). ⚠️ Build-gate is COMPILE-ONLY — behavioural/trace fidelity is unverified until Stage 5 (T017/T022). Carry these semantic-risk notes from the chunk sub-agents into Stage-5 verification: GuardNeedReader bound-path `_Step.Jump(cx.Pc)`; ExecV2SetVariable/PutVariable raw `+1` reader arithmetic (matched Dart verbatim); SetConstant/PutStructure nested-ancestor completion loops; `_evaluateArithmetic` num→double widening.
- **Stage 4 — no_emit COMPLETE ✅** (`66e061b4` code + goal_queue commit). First-class `no_emit` status: migration `0009_no_emit` (single-head off `0008`, additive `ALTER TABLE … ADD COLUMN no_emit boolean`), `status()._classify_codegen_row` (no_emit precedence over escalated), `mark-no-emit` CLI (upsert no_emit=true + oec=0 + tombstone `codegen_no_emit`), readiness `.satisfied`=built∨no_emit. Offline tests 19/19 + regression green. **Gabi granted explicit OK 2026-06-03** → `migrate` applied 0009 to the canonical cluster; `mark-no-emit lib/runtime/goal_queue.dart` done; goal_queue E1 escalation resolved (option-a no_emit). Verified live: `codegen status` → `built:74, no_emit:1, escalated:0, open_escalations_total:0` (75 total, clean frontier).
- **Stage 5 — now UNBLOCKED by Stage 3 (next dedicated session).** runner.cs builds, so a runnable C# REPL is now possible. T017: wire the converted `bin/glp_repl.cs` as the real `glp_repl` entry (replace the placeholder `Program.cs`) + add structured trace hooks (R1 event kinds) per `contracts/trace_normalization.md`. T022: `@needs_runtime` e2e. T031: swap the GEPA metric build-only→`tools/equiv/fidelity.py` (SAME scorer as the prod gate, SC-004) + re-run the per-subsystem GEPA loops (now a real fidelity gradient above the build ceiling — re-do `bytecode.md` etc.). T026–T029: `equiv next/status/ingest/retry` + `aggregate-escalations` + `/codeconv-equiv` skill + `@needs_runtime` strict-tier gate test. Stage-3's semantic-risk notes (above) are the first things T017/T022 should exercise.

### (superseded) original handoff bullets:

- **Stage 3 — runner.cs (the gate).** Convert `lib/bytecode/runner.dart` (4863 lines) per
  its E1 escalation `.codeconv/conversion-code/lib/bytecode/runner.dart.md` in the recorded
  **6-chunk split** (header+classes+enums → HEAD-phase arms ×2 → Unify arms →
  Commit/ClauseControl/BODY → Spawn/Requeue/Distribute/Transmit/Guards/Helpers/_TentativeStruct),
  appended via Edit, each cross-validated against two-phase HEAD/GUARD/BODY semantics
  (σ̂w/Si/U), WAM read/write mode, FCP wake-on-binding, tail-call kappa, GlpChannel RPC.
  **ALL-OR-NOTHING per session**: the current `runner.cs` STUB builds green; a partial
  conversion breaks the sln. Do all 6 chunks in one session, then `codeconv codegen ingest
  out/csharp/lib/bytecode/runner.cs --data-dir C:/pglite/research/glpnet` → build-gate the
  full sln. Use the `bytecode.md` prompt (its runner section has the confirmed dep signatures).
  This is the load-bearing blocker for Stage 5. Why not this session: explicitly a
  multi-turn/dedicated-session task per E1 + the bulk-drive precedent.
- **Stage 4 — goal_queue → first-class `no_emit`.** A codegen-tool enhancement: add a
  `no_emit` status orthogonal to escalated/built (the E1 note's "future enhancement").
  Likely a migration `0009` (single-head off `0008`) adding a `no_emit` bool to
  `codeconv.dart_codegen`, `status()` counting it separately, `readiness.classify_all`
  treating no_emit as satisfied (not ready/pending), a `codegen mark-no-emit` CLI, marking
  `lib/runtime/goal_queue.dart`, a tombstone key, + tests. **Bridge-touching schema
  migration on the canonical cluster — confirm with Gabi before running.** Non-urgent:
  goal_queue is correct-by-design today (0 consumers, build green; it currently shows as
  1 `escalated`/open-escalation — cosmetic).
- **Stage 5 — BLOCKED ON STAGE 3.** T017 (C# REPL trace instrumentation) + T022 (e2e) need
  a runnable converted REPL (⇒ runner.cs). Then T031 (swap the GEPA metric from build-only
  to `tools/equiv/fidelity.py` — the SAME scorer as the production gate, SC-004; re-run the
  per-subsystem GEPA loops to get a real fidelity gradient above the build ceiling), then
  T026–T029 (`equiv next/status/ingest/retry` + `aggregate-escalations` + `/codeconv-equiv`
  skill + `@needs_runtime` strict-tier gate test).

### Stage-2 mechanism recap (for re-running the GEPA loop per subsystem):
`codeconv codegen_opt dataset --subsystem S --json` → train/held-out · spawn generator
sub-agent(s) per train file under the `<S>.md` (or `_base.md`) seed → write candidate `.cs`
to `.codeconv/codegen-prompt/.gepa-scratch/<S>/` (gitignored) → `codeconv codegen_opt score
--file <cand> [--dep <dep.cs> …] --json` → reflect (sub-agent) if a build fails → freeze via
`export-prompt --subsystem S --instructions-file <best> --score … --dataset-hash …`. Skill:
`.claude/skills/codeconv-codegen-opt/SKILL.md` § "Per-subsystem GEPA orchestration loop".

---

## POSITION (update on every phase boundary)

- **Current phase**: Phase 4 — US2 strict tier (T023–T029). Step-side plumbing landed; bulk codegen is the gated long-pole.
- **Current task**: **A (bulk codegen drive) COMPLETE — 73/75 built (97.3%); 2 escalated.** Status: `files_total=75, not_started=0, codegen_ready=0, in_progress=0, built=73, escalated=2`. Full `dotnet build out/csharp/glp_runtime_net.sln` is GREEN (0 errors, 140 warnings). `bin/glp_repl.cs` converted (the REPL EXE entry point). The 2 open escalations are intentional (one no-emit by design, one is the 4863-line WAM interpreter deferred to a multi-pass session). **NEXT SESSION**: T017 (live C# REPL trace instrumentation per `contracts/trace_normalization.md`) — but T017 requires a runnable REPL, which in turn requires `runner.cs`'s full implementation (currently a stub throwing `NotImplementedException`). The pragmatic next steps are: (a) full `runner.dart` → `runner.cs` conversion (chunked multi-pass; see escalation at `.codeconv/conversion-code/lib/bytecode/runner.dart.md`); (b) THEN T017 trace hooks; (c) T022 `@needs_runtime` e2e; (d) T026–T029 (CLI next/status/ingest/retry/escalations + `/codeconv-equiv` skill + strict-tier gate test). T025 (durable-stage wiring) DONE.
- **Resolution summary** (2026-05-28, Gabi-approved): the 3 escalations that paused the drive at 48/75 were resolved as follows, unblocking 25 more files:
  - **`lib/runtime/heap_fcp.dart`** — Closer analysis showed `cells.cs`'s `CellTag { writer, reader }` and `heap_fcp.cs`'s `CellTag { WrtTag, RoTag, ValueTag }` are two SEPARATE Dart enums (one per `.dart` library) that only collide in C#'s flat namespace. **Resolution**: renamed heap_fcp.cs's enum `CellTag` → `HeapCellTag` (~40 sites) to disambiguate. cells.cs unchanged. Downstream files (commit.cs, suspend_ops.cs, etc.) use `HeapCellTag.*` for heap-layer tags. Commit `3a18e6f3`. Escalation status: resolved.
  - **`lib/compiler/pmt/mode_table.dart`** — `ModeDeclaration` + `ModedArg` were genuinely absent from glpnet's Dart sources (referenced from pmt/{mode_table,type_checker}.dart but never defined; the canonical Dart shape lives in the sibling GLP repo). **Resolution**: created `out/csharp/lib/compiler/pmt/mode_declaration.cs` defining `ModedArg(IsReader, TypeName, TypeParams)` + `ModeDeclaration(Signature, Args, TypeName)` + computed `Predicate` property + a `Module.ModeDeclarations()` extension. Records sized by the convspec's documented surface; non-`ast.cs` location keeps already-built `ast.cs` untouched. Commits `3a18e6f3`, `d6d442ad`, `dd5ad5f`. Escalation status: resolved.
  - **`lib/runtime/goal_queue.dart`** — Dart `export` directive with no types; spec/plan mandate no-emit. Verified by reverse-dep analysis: **0 direct consumers in the depgraph**, so no downstream is blocked. **Resolution**: left as-is (permanent no-emit). Escalation status: open (correct-by-design).
- **Final escalation surface** (2 entries open, both intentional):
  - **`lib/runtime/goal_queue.dart`** (Kind: undecidable; 0 consumers) — see resolution above. Future tools/codegen enhancement could add a `no_emit` first-class status orthogonal to `escalated`/`built`.
  - **`lib/bytecode/runner.dart`** (Kind: undecidable; 1 stub in place) — 4863-line WAM/FCP bytecode interpreter exceeded single-pass codegen budget (sonnet hit 32K output token cap; opus 4.7 declined as well). `out/csharp/lib/bytecode/runner.cs` now contains a **stub** declaring the full public type surface (`BytecodeProgram`, `BytecodeRunner`, `CallEnv`, `EnvironmentFrame`, `RunnerContext`, `ReplModuleTarget`, `ReplModuleContext`, enums `RunResult`, `UnifyMode`, `GuardResult`, plus a `BytecodeProgram.Merge(...)` method) with bodies throwing `NotImplementedException(_RunnerStub.Deferred)`. This allowed all 6 SCC siblings (runtime, body_kernels, glp_activation, system_predicates, mad_context — plus the Dart-level sibling runner.dart itself escalated) AND the 7 downstream files (asm, scheduler, linter, codegen, compiler, glp_engine, isolate_manager, agent_runtime, bin/glp_repl, etc.) to build green. The full runner conversion is the **NEXT load-bearing task** before T017 can do live trace instrumentation.
- **Class-rename note** (2026-05-28, batch 13 cascade): the Dart class `GLPRuntime` was converted to C# `class GlpRuntime` in `namespace GlpRuntime.Runtime;` — but `GlpRuntime` is also the root namespace, causing CS0118 ambiguity in every SCC sibling that wrote `GlpRuntime rt` parameters. **Resolution**: renamed `class GlpRuntime` → `class GlpRuntimeEngine` across runtime.cs + the 5 SCC siblings (1 substitution in runtime.cs, 42 in body_kernels.cs, etc.). Future converted files (downstream and any re-conversion) must use `GlpRuntimeEngine` — the per-file sub-agent prompts now include this naming notice.
- **Last green baseline**: 46/46 pure equiv tests + 15/15 isolated planagents (warm bridge) + 13/13 `buildprops` pure tests (incl. regression for header-comment-include bug) + 5/5 ingest tests + **48/75 files built green** via `dotnet build out/csharp/glp_runtime_net.csproj` (Converted.props lists the 48 accepted entries; no escalated file is in Converted.props) — 2026-05-28.
- **Last checkpoint commits** (chain, 2026-05-28): `824b8d46` T016 corpus.py + corpus.yml + subsystems.yml · `2ae54423` T018/T019 capture/compare/bytecode-diff · `58bfbf99` T023/T024 durable-step PURE core · `dc997583` T025 + C# REPL infrastructure · `bfd00a8a` Converted.props codegen append hook (pre-req B) · `311057c9` ledger flip to A in-progress · `b6079a73` **buildprops fix**: ignore example `<Compile Include="..."/>` inside header comment + regression test (caught on batch-1 first-pass) · `9289328a` batch 1: 7/7 built (analysis_phase, type_checker/{mode,prelude,type_ast}, bytecode/{opcodes,opcodes_v2}, compiler/error) · `5b72cc64` batch 2: 7/7 (compiler/pmt/errors, compiler/token, engine/claude_adapter, glp_runtime, multiagent/{boot_loader,global_writers_table,message_queue}) · batch 3 commit `5b72cc64` actually batch 3: 6/7 built + 1 escalated (runtime/{cells,machine_state,suspension,terms} + multiagent/repl_play_runner + type_checker/moded_term; goal_queue escalated) · `0f3df5c8` batch 4: 7/7 (type_checker/program_dfa, compiler/{ast,lexer}, multiagent/{mad_helpers,variable_table}, runtime/{abandon,fairness}) · `597d2418` batch 5: 7/7 (runtime/hanger + 6 type_checker files; 3 needed bounded repairs) · `36b1c843` batch 6: 5 built + 2 escalated (compiler/{glp_printer,pmt/type_table,unify_result} + multiagent/{global_send,payload_serializer}; mode_table dep_missing, heap_fcp CellTag conflict) · `e427b892` batch 7: 3/3 (runtime/suspend, type_checker/well_typed_clause, compiler/parser — parser needed manual long→int patch at line 600 after sub-agents repeatedly missed the site because their bare `dotnet build` sees Converted.props without the in-flight file) · `b33522b8` batch 8: 2/2 (type_environment_builder, partial_evaluator; 1 repair) · `9f64d293` batch 9: 3/3 (type_checker, analyzer, module_hierarchy — first pass) · `aeb6a298` ledger checkpoint at 47/75 · `ae2bab8c` batch 10: 1/1 (project_linker; manual patch for 2nd `Clause(...)` missing `guards:` after the same agent-build-view issue as parser.cs). All commits on branch `020-trace-equivalence-fidelity`; NOT pushed (Gabi's call).
- **Sub-agent prompt template lessons** (carry into future batches): (a) READ actual built dep `.cs` files before using their APIs — NEVER invent signatures; first-pass agents that "follow spec/plan" often hit CS0246/CS0117 because plan API names drifted from what the dep actually emitted (e.g. `getType`→`LookupType`, `TypeDefinition`→`TypeDef`). (b) Sub-agent's own `dotnet build` view is misleading — Converted.props excludes the in-flight file (the hook adds it only at ingest time + reverts on fail), so a bare build appears green even with errors in THIS file. The prompt now tells the agent NOT to use its own build to verify. (c) Escalation file format is strict: `### E<n>: <title>` (colon, not em-dash) + bullets `- **Field**: value` (colon OUTSIDE the `**`, not `**Field:**`). Kind must be one of `undecidable｜build_unrecoverable｜dependency_missing`.
- **Last GEPA artifact written**: `_base.md` + `{heap,bytecode,compiler,runtime-core,multiagent}.md` — **authored seeds** (provenance `optimizer: seed-authored`, not yet a GEPA-run output), committed `72ca51d1`. The bulk-drive idioms (getX→LookupX, *Error names, HeapCellTag, GlpRuntimeEngine, opcodes long→int, V2 alias, runner 6-chunk) are encoded as the curriculum seed. A real per-subsystem GEPA run overwrites the relevant `<subsystem>.md`.
- **Stage 1 (Claude-driven GEPA wiring) DONE 2026-06-03 — commit `72ca51d1`.** T032 (per-subsystem `dataset.py`: `classify_examples`/`build_subsystem_examples`/`subsystem_split`, longest-prefix via `tools/equiv/manifest.py`, content-free sha256 70/30), T033 (program.py `subsystem` field), T034 (`codegen/prompt.py:load(repo_root, subsystem)` chain: `<s>.md`→`_base.md`→`optimized.md`→baseline + `subsystem_prompt_path`/`base_prompt_path`), optimize.py (`run_optimize`/`evaluate` gain `subsystem`+`seed_instructions`; per-subsystem dataset+seed; provenance records subsystem), T035 (`/codeconv-codegen-opt` skill: per-subsystem orchestration-loop section + `dataset`/`score` CLI subcommands + `--subsystem`/`--seed-prompt`/`export-prompt --instructions-file`), T036 (the 6 prompt artifacts above). **Build-only metric** for now (decision 1: GEPA wired before runnable REPL; fidelity swap = T031 later). 24/24 targeted pure tests green (`test_codegen_opt_subsystem` + metric_mocked + prompt_artifact + fidelity); `score` gate verified end-to-end with real `dotnet build` (valid→1.0, broken→0.0+CS-error feedback). NOTE: production `prompt.load()` (no subsystem) now returns `_base.md` (a superset of baseline) — `/codeconv-codegen` could pass `subsystem` to use per-subsystem prompts (small follow-on, not in Stage-1 scope). NEXT = Stage 2: run the GEPA loop on `bytecode` (train={opcodes,opcodes_v2}, held-out={asm,runner}) → refined `bytecode.md`.
- **C# REPL state**: `out/csharp/` has 177 feature-016 scaffold stubs (Dart content under `.cs`); 0 codegen rows; 20 ready; 75 files in scope. `glp_runtime_net.sln` builds GREEN with `dotnet build` (lib compiles to empty assembly via `EnableDefaultCompileItems=false` + empty `Converted.props`; `glp_repl` exe builds via the placeholder `Program.cs`). Bulk codegen via `/codeconv-codegen` is what populates `Converted.props` and the actual library.

### Phase 1 Setup status — DONE
- [X] T001 — baseline accepted green (see above).
- [X] T002 `tools/equiv/` skeleton — auto-discovered; bare→status works.
- [X] T003 artifact dirs + READMEs (`equiv-manifest`, `codegen-prompt`, `conversion-equiv`).
- [X] T004 `@needs_runtime` marker in conftest (skips until built C# REPL).

### Carried finding (pre-existing, NOT introduced)
- **019 codegen bare-invocation bug**: `codeconv codegen` (no subcommand) crashes with
  `TypeError: status() missing 1 required positional argument: 'ctx'` — `ctx.invoke(status)`
  doesn't inject `ctx` under the installed Click/Typer. Equiv avoids it (delegates to
  `_run_status`). Flagged to Gabi; codegen fix deferred (019 code, needs approval).

---

## Restart procedure (do this on ANY new session / post-compaction)

1. **Start-of-session ritual** (CLAUDE.md): read `CLAUDE.md`, `docs/DISCIPLINE.md`,
   `docs/typed-glp-manual.md`, `docs/glp-cheat-sheet.md` → acknowledge each. The GLP
   trace-event semantics (three-phase HEAD/GUARD/BODY, SRSW, writer-MGU, three-valued
   unification) ARE the oracle's measured events — this reading is load-bearing for
   T007/T013/T014.
2. If **emerging from compaction**: STOP, tell Gabi, summarise from this ledger, ask
   how to proceed (CLAUDE.md mandate). Do not silently continue.
3. Read this file's **POSITION** block. Confirm branch is `020-trace-equivalence-fidelity`.
4. **Verify the last green baseline still holds** before touching code:
   `cd codeconv && .venv\Scripts\python -m pytest -q` (use `--test-concurrency=1`; PGLite cold-init ~7 s).
   If it is NOT green, STOP and report — do not stack new work on a red baseline.
5. Resume at the CURRENT task. Within a phase, follow the task order in `tasks.md`;
   `[P]` tasks may run together (different files).

---

## Phase checkpoints — each is a SAFE RESTART POINT

A phase is "checkpoint-green" when its pytest subset passes AND the 019 baseline is still
green (FR-019). At every checkpoint-green boundary: update POSITION above, then
**stage by name and commit** a checkpoint (do NOT `git add -A`; do NOT merge to main —
only Gabi merges). The commit is what makes the restart trivial.

- [X] **Phase 1 Setup** (T001–T004) — DONE 2026-05-27. baseline recorded; `tools/equiv/` skeleton + artifact dirs + `@needs_runtime` marker.
      Restart-green: 019 baseline green (modulo bridge flakiness) + `tools/equiv` auto-discovered + collect-only clean.
- [X] **Phase 2 Foundational** (T005–T012) — DONE 2026-05-27. migration `0008` (single head off `0007`), `trace.py`, `fidelity.py`, `manifest.py`, `subsystems.yml`, tombstone keys.
      Restart-green: 14 pure tests GREEN (T012 tier boundaries + offline single-head/chain). Manifest validated vs real inventory (0 ties/0 unclassified). Bridge-gated migrate-idempotency (T006/0008) deferred to full checkpoint run. **Runtime-free.**
- [~] **Phase 3 US1 oracle (MVP)** (T013–T022) — DONE except runtime-gated tail. T013/T014/T015 (pure normalize/relation/bytecode_diff) + T016 (corpus.py + reviewed corpus.yml) + T018 (capture orchestration; live-spawn backend gated on T017) + T019 (compare/bytecode-diff standalone deterministic, NO DB write — Gabi decision b) + T020/T021 (SC-005 batteries). 46/46 pure equiv tests green. REMAINING (runtime-gated, blocked on bulk codegen → runnable C# REPL): T017 live C# REPL trace instrumentation (`@needs_runtime`), T022 e2e (`@needs_runtime`).
      Finding (no escalation; R10/B1): live Dart `:trace` is reduction-level only — fine-grained UNIFY/WRITER_BIND/REACTIVATE/BYTECODE_OP events live in `:debug` per-op prints; `parse_dart` live-text wiring consumes both, finalized at T017/T022 against real captures.
- [~] **Phase 4 US2 strict tier** (T023–T029) — STEP-SIDE PLUMBING LANDED. T023 `readiness.py` (PURE, four-state classify + curriculum order) + T024 durable equiv-step PURE core (`workflow.compute_step_result` + `step_equiv` bridge wrapper) + T025 (this session: registered `step_equiv` in `durable/steps.py`, placement note in `durable/workflows.py`; equiv mirrors codegen's separately-driven pattern — registered but NOT in `PER_UNIT_STAGES`, driven by `/codeconv-equiv`). REMAINING: T026 (`equiv next/status/ingest/retry` CLI), T027 (`equiv aggregate-escalations`), T028 (`/codeconv-equiv` skill), T029 (`@needs_runtime` strict-tier gate test). Most need the runnable C# REPL → bulk codegen first.
      Restart-green: T029 strict-tier gate (`@needs_runtime`).
- [~] **Phase 5 US3 real GEPA (OFFLINE)** (T030–T038) — WIRING DONE (Stage 1, commit `72ca51d1`): T030 (strip: Claude-driven `run_optimize` w/ injected `generate_fn`/`propose_fn`, NO API), T032 (per-subsystem dataset), T033 (program subsystem field), T034 (`prompt.load(subsystem)`), T035 (skill loop + `dataset`/`score` CLI), T036 (`_base.md`+5 seeds). REMAINING: **T031** (metric→`fidelity.py`, the fidelity swap — needs the runnable C# REPL, deferred to Stage 5), **T037** (mocked-LM GEPA ≥ baseline + budget cap test — `test_codegen_opt_metric_mocked` covers the global path; a per-subsystem variant is the gap), **T038** (no-LM-import-on-production-path guard test — not yet written). Stage 2 = the first real per-subsystem run (`bytecode`).
      Restart-green: T037 (mocked-LM ≥ baseline + budget cap) + T038 (no-LM-import on production path). **See GEPA restart notes below.**
- [ ] **Phase 6 US4 dynamic tier** (T039–T042) — T039 mode DECISION (gate, precedes bulk multiagent gen) → T040 both modes behind the flag, reclassification, dynamic test.
      Restart-green: T042 (`@needs_runtime`). **Do NOT bulk-generate `multiagent` before T039 is recorded in contracts/subsystem_curriculum.md.**
- [ ] **Phase 7 US5 promotion** (T043–T045) — `equiv fidelity|promote|mark-stale`, `stale.py`, promotion-gate test.
      Restart-green: T045 (promote only at corpus full-equivalence).
- [ ] **Phase 8 Polish** (T046–T050) — FR-017 spec-violation surfacing, tombstone round-trip, docs, **T049 full 020+019 suite green + commit**, T050 SC roll-up.
      Restart-green: T049 full suite green.

---

## HARD GATES / carried risks (re-read before the relevant phase — from HANDOFF + plan)

1. **B1 bootstrapping (by design)**: US1 `@needs_runtime` tasks (T017 C#-REPL trace, T022 e2e)
   only fully run once US2 produces a runnable converted C# REPL. Build the pure core
   (T013–T015) + schema first; runtime-coupled tests land as the strict tier compiles.
   Pure modules are fully testable immediately — do not block on the REPL.
2. **Replay-safety (R12, top risk)**: NEVER spawn a REPL or read wall-clock inside the
   durable `equiv` step. Nondeterministic capture lives in the CLI / `/codeconv-equiv`
   skill ONLY; the step is a pure verdict ingest of recorded traces (019 `needs_agent_work`).
3. **LM containment (SC-008)**: `tools/equiv/`, `tools/codegen/`, `durable/` import NO
   dspy/litellm/openai. T038 guards it — keep green. GEPA/LM live ONLY in `tools/codegen_opt/`, offline.
4. **Migration single-head**: `0008` chains off the single `0007` head (historical dual-`0003` exists). T006 asserts one head.
5. **GLP authority**: trace event kinds (unify outcome / suspend / reactivate / writer-bind /
   bytecode-op) are GLP three-phase + SRSW + writer-MGU semantics. Do NOT invent events.
   If a needed event is absent from Dart `:trace`, STOP & report. If a divergence traces to
   a Dart original that violates the GLP spec → CLAUDE.md Bug-Protocol report, do NOT alter
   C# to match a wrong oracle (FR-017).
6. **Dart golden is read-only**: no change to `glp_runtime/`. Trace hooks go in the
   *converted* C# REPL (`out/csharp/`) only.

---

## GEPA / DSPy cycle restart notes (Phase 5, and any re-optimization)

The optimizer is the one long, non-deterministic, expensive, LM-bearing stage. Restart safety here is different from deterministic phases:

- **Offline + non-durable by design** — GEPA runs are NOT inside DBOS. A killed GEPA run loses
  only that run; it never corrupts the durable pipeline or the production path.
- **Hard budget cap (SC-006)**: every optimize invocation takes `--budget`; on cap it must
  return **best-so-far**. A restart re-runs from the carried-forward seed prompt, not from zero.
- **Carry-forward seed**: per-subsystem prompts descend from `_base.md`. The frozen prompt for
  each completed subsystem is the durable artifact in `.codeconv/codegen-prompt/<subsystem>.md`
  (checked in, with provenance front-matter). THAT file — not in-memory GEPA state — is the
  resume point. Record "Last GEPA artifact written" in POSITION after each subsystem freezes.
- **Metric identity**: the GEPA metric score MUST equal the production gate via
  `import tools.equiv.fidelity` (T031/SC-004). If they ever diverge, STOP — the optimizer is
  chasing a different target than the gate.
- **Tests use a MOCKED LM only** (T037). Never call a real LM in pytest.

---

## Bridge-test flakiness (operational — affects every checkpoint re-run)

The `@needs_bridge` tests (`test_depgraph_*`, `test_discover_*`, `test_phase7_verifications::test_schema_isolation`)
each cold-spawn a PGLite bridge (~7–13 s) + run the full migrate chain. Two failure modes observed
2026-05-27, BOTH non-regressions (identical symptom: `codeconv migrate: bridge unreachable:
timed out waiting for bridge to become reachable after 60.0s`):
1. **Concurrent-run collision** — a second `pytest` running at the same time (another session) made
   bridge spawns time each other out → 10 spurious failures. **Mitigation: never run the full
   codeconv suite while another pytest/bridge is live.** Check first:
   `Get-CimInstance Win32_Process -Filter "Name='python.exe'" | ? { $_.CommandLine -like '*pytest*' }`
   and `Name='node.exe'` for stray bridges.
2. **Transient cold-spawn timeout** — even solo, an occasional bridge spawn exceeds the 60 s window
   → 1 spurious failure; passes on isolated re-run (15 s).

**Checkpoint/restart rule**: a `@needs_bridge` failure that is the `bridge unreachable … 60.0s`
timeout is NOT a regression — re-run that test alone to confirm green before treating it as red.
A real regression is an assertion on data (row contents, diff, tombstone), not a spawn timeout.
The full suite takes ~1 h wall-clock; for fast iteration use pure-subset runs
(`-m "not needs_bridge and not needs_runtime"`) and reserve the full bridge run for phase checkpoints.

---

## Safe restart for A (bulk codegen drive) — 2026-05-28

**This is the next session's job.** Gabi's plan: B (infra) DONE this session → C (T025
durable wiring) DONE this session → **A (bulk codegen) in a NEW dedicated session**.

### Mission
Convert the 75 in-scope Dart files under `glp_runtime/lib/...` to C# under `out/csharp/lib/...`
via the `/codeconv-codegen` agent loop, in dependency order, until the strict-tier subsystems
(`heap` / `bytecode` / `compiler` / `runtime-core`) compile as a coherent library and a
runnable `glp_repl` executable can be built. This unblocks T017 (live trace instrumentation)
and T022 (e2e). The conversion is LLM-driven, build-gated, escalate-don't-guess (019 design).

### Starting state (committed; verify on entry)
- `out/csharp/glp_runtime_net.sln` + `glp_runtime_net.csproj` + `Converted.props` + `glp_repl/{glp_repl.csproj,Program.cs}` ALL build green (`dotnet build out/csharp/glp_runtime_net.sln` → 0 errors, ~6 s).
- 177 feature-016 scaffold stubs (Dart content under `.cs`) under `out/csharp/lib/`,
  EXCLUDED from compilation by `EnableDefaultCompileItems=false` + empty `Converted.props`.
- `codeconv codegen status` (`--data-dir C:/pglite/research/glpnet`): `files_total=75,
  codegen_ready=20, not_started=75, optimized_prompt=false, prompt_warning="no optimized
  prompt; using baseline"`. The 20 ready files (topo_level=0) are the leaves — start there.
- Per-subsystem optimized prompts (T036) NOT authored → baseline prompt is the active one.
  This is acceptable for the first pass (the bulk drive); GEPA refinement (US3) is later.

### What's needed before the first `/codeconv-codegen` invocation
1. **Append hook in `codeconv/src/codeconv/tools/codegen/workflow.py`** — on a successful
   build-gate accept, append `<Compile Include="lib/<rel>.cs" />` to `out/csharp/Converted.props`
   (idempotent — don't dup). Roughly 10–15 lines of code. The build-gate must run AFTER the
   include is in place, else the gate trivially passes on an unincluded file. Order:
   write `.cs` → append to `Converted.props` → `run_build` → on fail, optionally revert the
   append (or leave + let the next attempt overwrite). Keep the revert simple.
2. **Verify**: with `Converted.props` listing the first file, `dotnet build out/csharp/glp_runtime_net.sln`
   must still pass (catches a workflow misuse before bulk).

### Bulk drive loop (per file)
1. `codeconv --data-dir C:/pglite/research/glpnet codegen next --json` → pick one ready file.
2. Invoke `/codeconv-codegen` for that file. The skill orchestrates: read the plan + convspec +
   the Dart source, write `out/csharp/<rel>.cs`, run the build-gate (`dotnet build` via the
   new Converted.props), accept on pass / escalate on fail (escalation artifact under
   `.codeconv/codegen-escalations/<rel>.dart.md`).
3. On accept → `dart_codegen.codegen_completed_at` is set → the file unblocks its downstream.
4. Loop to next ready. Re-check `codegen status` periodically to confirm progress + green.

### Build-time test (cheap; run every ~5–10 files accepted)
- `dotnet build out/csharp/glp_runtime_net.sln --nologo -v quiet` → must stay GREEN.
- A red build is a regression in the LAST converted file → escalate (the build-gate should
  have caught it; if it slipped through, there's a workflow bug — STOP & report).

### Stopping conditions for the session
- All 75 files converted + library + REPL placeholder build green → ready for **T017**
  (replace `glp_repl/Program.cs` with the converted REPL + add `:trace` instrumentation
  per `contracts/trace_normalization.md`).
- Context pressure → checkpoint commit ("bulk codegen: N/75 files converted; build green"),
  update POSITION above, signal Gabi for the NEXT bulk session.

### Hard discipline (re-read first on entry)
- **Spec-first** (CLAUDE.md): conversion idioms come from `.codeconv/conversion-plans/<rel>.dart.md`
  + `.codeconv/conversion-specs/<rel>.dart.md` (017/018 artifacts) + `conversion_idioms` KB. The
  agent DOES NOT INVENT — it follows the plan/spec; ambiguities STOP and ask.
- **No LM call from `tools/codegen` / `tools/equiv` / `durable/`** (SC-008). The agent (Claude)
  IS the LM here; `tools/codegen` itself stays LM-free — the agent runs OUTSIDE the tool.
- **No `git add -A`**. Stage by name. Commit boundaries: every accepted file OR every N files
  (e.g., N=10) with a one-line checkpoint message.
- **`--data-dir C:/pglite/research/glpnet`** on every bridge-touching call (CLAUDE.md mandate).

### Why NOT this session
This is a long, LM-heavy, file-by-file synthesis loop. Doing it here would burn the context
window on a small number of files. The dedicated session starts fresh, with this ledger as
its restart map, and runs as long as it can. Signal Gabi when ready to start that session.

---

## Context

Feature 020 = a **fidelity layer over 019's deterministic codegen engine**: a deterministic,
LM-free differential equivalence oracle (Dart golden vs converted-C# candidate, normalized
causal/partial-order traces) + a tiered fidelity metric + real `dspy.GEPA` (offline,
per-subsystem) driving codegen toward trace-equivalence. MVP = US1 (the oracle alone, usable
as a conformance harness). 019's (C)-hybrid LM-containment invariant is preserved verbatim.

Commit discipline (CLAUDE.md): stage by name only; commit checkpoint-green boundaries; offer
Gabi the merge template at end; never merge to `main`. Every bridge-touching `codeconv` call:
`--data-dir C:/pglite/research/glpnet`, `--test-concurrency=1`.
