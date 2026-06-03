# Quickstart — Trace-Equivalence-Driven Codegen Fidelity (020)

End-to-end: stand up the oracle → score fidelity → drive GEPA co-evolution per subsystem. All `codeconv` calls pass `--data-dir C:/pglite/research/glpnet` (CLAUDE.md). Production/durable path is LM-free; the optimizer is offline-only.

## 0. Baseline (always, before + after — CLAUDE.md Test Protocol)
```
cd D:\BSTDEV\research\GLP\GLPNET\codeconv
.venv\Scripts\python -m pytest -q            # 019 baseline: 104 pure + 73 codegen suite, green 2026-05-27
codeconv --data-dir C:/pglite/research/glpnet doctor   # OVERALL OK
codeconv --data-dir C:/pglite/research/glpnet migrate  # applies 0008 (single head)
```

## 1. Verify one file with the oracle (US1 MVP)
```
# what to verify next, in curriculum order
codeconv --data-dir C:/pglite/research/glpnet equiv next

# early cheap checkpoint (available once the compiler subsystem is converted)
codeconv --data-dir C:/pglite/research/glpnet equiv bytecode-diff programs/tests/typed/foo.glp

# capture both traces (Dart golden + C# candidate) — agent/CLI layer, spawns both REPLs
codeconv --data-dir C:/pglite/research/glpnet equiv capture <tombstone_key> programs/tests/typed/foo.glp

# deterministic verdict from the recorded traces (durable step; replay-safe)
codeconv --data-dir C:/pglite/research/glpnet equiv compare <tombstone_key> programs/tests/typed/foo.glp
#   exit 0 equivalent | exit 2 divergent (+ DivergenceRecord JSON) | exit 3 needs_agent_work
```
Equivalence is judged under the causal/partial-order relation: outcomes + dependent events + bytecode spine REQUIRED; heap addresses + independent-goal interleaving ABSTRACTED. Bonds sources compare outcome-only.

## 2. Read the tiered fidelity score
```
codeconv --data-dir C:/pglite/research/glpnet equiv fidelity <tombstone_key>
#   0.0 non-compile | 0.25 compiles-no-evidence | 0.5+0.5·frac high band (<1.0) | 1.0 ONLY at full trace-equivalence
```

## 3. Drive a subsystem (US2 strict tier) via the skill
```
/codeconv-equiv          # drives capture→compare→record across the frontier; loops escalations; bounded retry then escalate
codeconv --data-dir C:/pglite/research/glpnet equiv status     # per-subsystem fidelity rollup
codeconv --data-dir C:/pglite/research/glpnet equiv promote heap   # promotes ⇔ every in-scope source equivalent
```

## 4. Optimize the per-subsystem prompt with real GEPA (US3, OFFLINE)
```
# offline only; reads OPENAI_API_KEY from env; never on the durable path
codeconv --data-dir C:/pglite/research/glpnet codegen-opt optimize --subsystem heap --budget 40
codeconv --data-dir C:/pglite/research/glpnet codegen-opt eval     --subsystem heap   # held-out (~30%) score
codeconv --data-dir C:/pglite/research/glpnet codegen-opt export-prompt --subsystem heap
#   writes .codeconv/codegen-prompt/heap.md (descends from _base.md, carries forward to next subsystem)
```
GEPA's metric returns score (== production gate) + textual feedback (the DivergenceRecord). Budget cap → best-so-far.

## 5. Co-evolution loop (FR-015), per subsystem, easy→hard
optimize-before-generate (on available signal) → generate (`/codeconv-codegen`) → run the equiv gate → reflect divergences into GEPA → regenerate weak files → freeze the subsystem prompt → carry base forward. `heap → bytecode → compiler → runtime-core → multiagent` (last, with the matured prompt; its verification mode decided then).

## 6. Drift + stale (FR-016)
```
codeconv --data-dir C:/pglite/research/glpnet equiv mark-stale <tombstone_key>   # on Dart source change
# stale rows do not count toward frac; re-verify with capture+compare
```

## 7. Final gate (SC checks)
```
.venv\Scripts\python -m pytest -q          # 020 tests + 019 baseline still green (FR-019, SC-008)
codeconv --data-dir C:/pglite/research/glpnet equiv status   # SC-001/SC-002 corpus-wide rollup
```

## Definition of done (spec SC-001…SC-008)
- 100% in-scope GLP sources trace-equivalent (bonds outcome-equivalent) — SC-001.
- ≥95% production files reach build+equivalence without manual edits — SC-002.
- per-subsystem GEPA prompt beats baseline on held-out — SC-003.
- no file scores 1.0 unless fully trace-equivalent — SC-004.
- zero false divergences / zero false equivalences on constructed batteries — SC-005.
- optimization never exceeds budget; capped run yields usable prompt — SC-006.
- bytecode-emission diff empty for 100% deterministic-tier sources post-compiler-conversion — SC-007.
- production path LM-free; 019 baseline green — SC-008.
