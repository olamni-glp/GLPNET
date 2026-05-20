# Current Plan: 018 genuine live pass — PAUSED at 108/128 (Anthropic rate limit)

Paused: 2026-05-20 (Anthropic limit resets ~04:20 Europe/London)
Resumes: post-reset, when you re-invoke this session

## Status
- **108/128 progressed** on the canonical PG17 cluster `C:/pglite/research/glpnet`
  (104 scaffolded + 4 escalated). **20 remain** at `analysed` (no artifact).
- Last commits on `main`:
  - `0b33dc93` — facet-1/2/3 remediation (Option-4 split, migration 0006,
    spec Amendment 2 + FR-044, test re-baseline). Validated: pure 17/17,
    real-bridge builder suite 23/23, facet-3 acceptance gate green.
  - `05e0824d` — live-pass snapshot (108 conversion-spec artifacts +
    128 tombstone stamps). Protects the genuine-pass state across the pause.
- Cluster has dead terminal epochs (harmless, history). The `_resume_epoch`
  awaiting-agent rule means a plain `builder run` post-reset mints a fresh
  epoch and re-evaluates the gate; recovered PRE wfs are recovered (cheap),
  POST wfs are content-addressed (fresh per artifact).

## 4 open escalations (your ruling needed; non-blocking — other files continued)
1. **`compiler/error.dart`** — exception naming: keep Dart-source `CompileError`
   OR rename to `CompileException` (.NET-idiomatic suffix per Microsoft Learn).
2. **`compiler/glp_printer.dart`** — suspected `_isAtom` *latent bug in Dart
   source itself*; agent refused to silently resolve.
3. **`runtime/heap_fcp.dart`** — HeapFCP threading model: single-owning-context
   (current isolate model) vs ConcurrentDictionary/Interlocked. Inherited by:
   `runner.dart`, `body_kernels.dart`, `scheduler.dart`,
   `system_predicates_impl.dart`, `mad_context.dart`,
   `mad_cold_call_isolate_test.dart`.
4. **`compiler/analyzer.dart`** — duplicate `UnifyResult` ADT + `PartialEvaluator`
   class declared in BOTH `analyzer.dart` and `partial_evaluator.dart`. Dart
   library-private allows; C# doesn't. Rename / lift / nest?

Aggregate-escalations report (deterministic, no agent needed) — run any time:
```
codeconv/.venv/Scripts/codeconv.exe --data-dir C:/pglite/research/glpnet \
  convspec aggregate-escalations
```

## Resume protocol (next session, post-reset)

Step 1 — confirm cluster healthy + current frontier:
```
codeconv/.venv/Scripts/codeconv.exe --data-dir C:/pglite/research/glpnet builder status --json
codeconv/.venv/Scripts/codeconv.exe --data-dir C:/pglite/research/glpnet builder run --json | tail -1
```
Expect `naw ≈ 20`, `analysed ≈ 20`. If counts shifted, sanity-check before
spawning agents.

Step 2 — pick next 5 files with no artifact (skip already-specced/escalated):
```python
# inline (see commits 0b33dc93/05e0824d for the helper pattern)
import json, os
naw = json.loads(...)['needs_agent_work']
batch = [r for r in naw
         if not os.path.isfile('.codeconv/conversion-specs/' + r + '.md')][:5]
```

Step 3 — spawn 5 parallel convspec analysis sub-agents (general-purpose),
one per rel-path, using the established prompt template (see
`/codeconv-convspec` skill for the contract). Each agent: reads
`specs/018-codeconv-builder/contracts/convspec_artifact_format.md` +
`convspec_idiom_schema.md` + the target `.dart`; produces
`.codeconv/conversion-specs/<rel>.md`; self-verifies via
`convspec ingest <rel>`. SPEC-ONLY (no C# blocks), ESCALATE-DON'T-GUESS
(FR-013), FR-024 official-docs authoritative.

Step 4 — after the batch completes, plain `builder run --json` to ingest +
advance. Repeat steps 2–4 until `needs_agent_work` is empty or only the 4
escalated files remain.

Step 5 — final aggregate-escalations report; commit a "live-pass complete"
snapshot; offer push / merge.

## 20-file frontier (next session starts here)
- lib/engine/glp_engine.dart (1182 LOC)
- lib/multiagent/agent_runtime.dart
- lib/multiagent/isolate_manager.dart
- test/compiler/reserved_constant_test.dart
- test/heap/arithmetic_pointer_test.dart
- test/runtime/module_activation_test.dart
- test/runtime/rpc_routing_test.dart
- test/srsw_test.dart
- test/test_agent_init_goal.dart
- test/test_constant_compile.dart
- test/dynamic_dispatch_test.dart
- test/engine/glp_engine_test.dart
- test/multiagent/output_kernel_test.dart
- test/multiagent/ui_mediator_test.dart
- test/debug_four_agents_modules.dart
- test/multiagent/bonds_v2_isolate_test.dart
- test/multiagent/cssn_v2_isolate_test.dart
- test/multiagent/isolate_manager_test.dart
- test/multiagent/multiagent_glp_test.dart
- test/multiagent/multiagent_modules_test.dart
