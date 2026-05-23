# Implementation Plan: codeconv-codegen — GEPA/DSPy-optimized Dart→C#/.NET code generation

**Branch**: `019-codeconv-codegen` | **Date**: 2026-05-23 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification at `specs/019-codeconv-codegen/spec.md` (clarified Session 2026-05-23 — 5 Q&A; architecture **(C) hybrid** confirmed; composite metric `0.6·tests + 0.4·human` with build hard-gate + sampled review + median≥4/5 promotion gate; staged test-scope; OpenAI-via-litellm offline-only optimizer + GEPA budget cap; `dart_codegen` table + migration `0007` + `codegen` builder stage).

> Authored under the buildkit toolchain (migrated from spec-kit 2026-05-23). Pipeline-tracked feature `019-codeconv-codegen`; this is the `plan` stage.

## Summary

The terminal codeconv stage. It turns the 130 ratified per-file conversion plans + convspecs into compilable C#/.NET, filling the `out/csharp/` scaffold tree, in feature-015 topological/SCC order, gated by a composite feedback metric (build hard-gate → ported-test pass-rate → human review) with batched promotion. Codegen quality is **actively improved** by an offline **DSPy program optimized with GEPA** (reflective Pareto prompt evolution) against that same metric.

**Architecture — (C) hybrid (confirmed):** two cleanly separated halves.

- **Production path (deterministic, replay-safe):** a new `codeconv codegen` tool (`tools/codegen/`) that — exactly like `convspec`/`planagents` — owns ALL deterministic state (codegen-readiness, frontier selection, two-phase `dart_codegen` writes, artifact ingest, `dotnet build`/`dotnet test` gate, human-review recording, batch promotion, escalation aggregation). The C# is authored by **harness-spawned Claude Code codegen sub-agents** (carried by the `/codeconv-codegen` skill, the feature-017/018 justified-deviation precedent) consuming the **GEPA-optimized prompt** + the file's plan + convspec + dependency interfaces + idiom KB. The DBOS `codegen` step body is a **deterministic ingest of the checked-in agent-produced `.cs` + recorded build/test result**; absent `.cs` ⇒ typed `needs_agent_work` sentinel (never raises) — the convspec R3 pattern verbatim. **No in-package LM call on the production/durable path.**
- **Offline optimizer (separate, non-durable):** a new `tools/codegen_opt/` harness running a real `dspy` codegen module under **GEPA** against the test-feedback metric, calling an LM via **litellm/OpenAI**, bounded by a hard budget/rollout cap. Its **only** output into production is a serialized **optimized-prompt artifact** (checked in). Never invoked by the durable pipeline.

This honors "use GEPA/dspy actively to improve codegen" while keeping the durable pipeline deterministic and replay-safe with no in-package model client on the production path — the cross-pipeline rule (012/017/018 R3).

**Staging (FR-012):** Increment 1 (US1, MVP) = production `lib/` code; metric = build hard-gate + human review. Increment 2 (US3) = convert the test tree and add ported-test pass-rate (the `0.6/0.4` weighting). US2 (GEPA optimization) and US4 (resumable/idempotent/escalation state) layer across both.

**Schema (FR-013):** new `codeconv.dart_codegen` two-phase table; migration `0007_codegen.py` chained after `0006`; tombstone keys appended after the plan keys; `codegen` stage added to the 018 durable builder after `plan`. No `public`/`dbos` objects authored by Alembic; CREATE TABLE IF NOT EXISTS; single head.

**Net code:** ~900–1200 lines new Python in `tools/{codegen,codegen_opt}/` + a `durable/` codegen step + 1 migration; 2 new `SKILL.md`; a `_FIELD_ORDER` extension; the optimized-prompt artifact + `.codeconv/conversion-code/` output tree. No Dart/Node/`glp_runtime/` change; DSPy/GEPA/litellm/OpenAI confined to the offline `codegen_opt` harness.

## Technical Context

**Language/Version**: Python 3.11+ (`codeconv/pyproject.toml`). Agent layer: Claude Code Agent tool. Target output: C#/.NET 10 (`dotnet 10.0.200-preview` on PATH).
**Primary Dependencies**: existing — `dbos`, `sqlalchemy>=2.0` + `psycopg[binary]`, `PyYAML`, `typer`. Offline-optimizer-only — `dspy 3.2.1`, `gepa 0.0.27`, `litellm 1.85.0`, `openai 2.37.0` (already in `codeconv/.venv`). Feedback signal — the `dotnet` CLI. **No new Python dependency.**
**Storage**: PGLite via the unified bridge. Reads (read-only): `codeconv.dart_depgraph` (015), `codeconv.dart_convspecs` + convspec artifacts (018), `codeconv.dart_plans` + plan artifacts (017/018), `codeconv.conversion_idioms`. New (`codeconv` schema): `dart_codegen`. DBOS owns its `dbos`-schema tables. (NB: this is the codeconv cluster at `C:/pglite/research/glpnet` — distinct from buildkit's own `pgdb/`.)
**Testing**: `pytest codeconv/tests/`. Pure logic unit-tested without a bridge. Bridge-needing tests `@needs_bridge`, serial, through the 012 OS lock; PGLite cold-init ~7 s. The `dotnet` build/test gate is exercised on a tiny fixture project, skipped where `dotnet` absent. GEPA/LM never called in tests — optimizer tested with a mocked LM + fixture metric.
**Target Platform**: Windows 11 primary; cross-platform Python.
**Performance Goals**: deterministic Python sub-second per stage call; `status` ≤5 s warm. End-to-end dominated by codegen sub-agents + (offline) GEPA rollouts — no hard SLA. The `dotnet build` gate is incremental against already-generated dependency assemblies (topo order makes per-batch builds cheap).
**Constraints**: `--data-dir C:/pglite/research/glpnet` (CLAUDE.md). 012 FR-026/FR-027 carry-forward. DBOS+PGLite single-writer (R12 carry-forward). The optimizer's API key is read from env, lives ONLY in `codegen_opt`, never imported by the production tool or any DBOS step (replay-safety). GEPA bounded by a hard budget cap.
**Scale/Scope**: 130 inventoried files (≈83 `lib/` + the rest test/bin), 443 in-subtree edges. 0 new schemas, 1 new table + 1 migration, 2 new skills, 2 new tool subpackages + 1 durable step, 1 optimized-prompt artifact, 0 new Python deps.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is unfilled template placeholders. Per `CLAUDE.md` / `docs/DISCIPLINE.md` (operative authority), the gates:

| Gate | Pass? | Note |
|---|---|---|
| Spec-First | PASS | spec.md clarified (5 Q&A); checklist green |
| Never program based on ignorance | PASS | recon verified inputs/tooling; (C) + metric decided with Gabi |
| DISCIPLINE §1.1 Specification-First | PASS | plan derives entirely from spec FRs |
| DISCIPLINE §1.4 Traceability | PASS | every artefact cites FR + the 015/017/018 mechanism it extends |
| DISCIPLINE §1.7 Errors not "limitations" | PASS | undecidable construct / failed build → escalation (FR-007) |
| DISCIPLINE §1.2 / §1.10 no workarounds / spec authority | PASS by design | escalate-don't-guess as a tool requirement; build failure never silently accepted |
| DISCIPLINE §2.2 baseline before/after | PASS by design | tasks.md sequences baseline-pytest |
| Feature 012 (auto-discovery; schema isolation; tombstone round-trip) | PASS | new table in `codeconv`; FS-convention registration; round-trips through tombstones |
| Feature 015 (read depgraph; MUST NOT recompute) | PASS | consumes `dart_depgraph` read-only |
| Feature 016/017/018 capability preservation | PASS by design | upstream untouched; codegen additive |
| Skill-as-thin-wrapper convention | **DEVIATION — justified (×2)** | `/codeconv-codegen` carries codegen sub-agent + human-review orchestration; `/codeconv-codegen-opt` drives the optimizer — 017/018 class. See Complexity Tracking. |
| In-package LM client + API key | **GATED — see Complexity Tracking** | confined to offline `codegen_opt`; production tool + every DBOS step LM-free, replay-safe (R3) |
| DBOS replay-safety (R3) | PASS (HARD GATE) | codegen step = deterministic ingest of checked-in `.cs` + recorded build result; no model call inside any step; GEPA non-determinism offline only |

**Result**: GATE PASSED; two justified deviations recorded below; the in-package-LM risk contained by the (C) split and flagged as the top `/buildkit-analyze` item.

## Project Structure

### Documentation (this feature)

```text
specs/019-codeconv-codegen/
├── plan.md                              # This file (/buildkit-plan output)
├── spec.md                              # Clarified spec (5 Q&A 2026-05-23)
├── checklists/requirements.md           # Spec quality checklist (passing)
├── research.md                          # Phase 0 — R1–R12
├── data-model.md                        # Phase 1 — dart_codegen + tombstone keys + artifacts + migration linearization
├── quickstart.md                        # Phase 1 — end-to-end codegen + optimize + review/promote flow
├── contracts/
│   ├── codegen_cli.md                   # `codeconv codegen [status|next|ingest|record-review|promote-batch|aggregate-escalations|retry]`
│   ├── codegen_opt_cli.md               # `codeconv codegen-opt [optimize|eval|export-prompt|show]` (offline; DSPy/GEPA)
│   ├── dbos_codegen_stage.md            # durable codegen step (deterministic ingest+build gate; needs_agent_work; replay-safety)
│   ├── codegen_artifact_format.md       # produced .cs location/validation + optimized-prompt artifact format
│   ├── metric_contract.md               # composite metric + GEPA wiring + budget cap
│   ├── codegen_schema.md                # dart_codegen DDL; migration 0007 single-head proof; schema isolation
│   └── agent_orchestration.md           # codegen sub-agent prompt contract + human-review loop + concurrency cap + SCC batch
└── tasks.md                             # Phase 2 — /buildkit-tasks (next)
```

### Source Code (repository root)

Touches only `codeconv/`, `.claude/skills/`, the `out/csharp/` generated tree, and `.codeconv/`. No Dart/Node/`glp_runtime/` change.

```text
codeconv/src/codeconv/
├── tools/
│   ├── codegen/                              # NEW — production deterministic tool (auto-discovered)
│   │   ├── __init__.py                       # Typer app (status/next/ingest/record-review/promote-batch/aggregate-escalations/retry); bare = status
│   │   ├── readiness.py                      # pure codegen-readiness predicate (deps codegen-complete; SCC=one batch)
│   │   ├── workflow.py                       # register() durable step + run_codegen_ingest (two-phase dart_codegen + dotnet gate)
│   │   ├── buildgate.py                      # deterministic dotnet build/test invocation + result parse
│   │   ├── review.py                         # human-review recording + batch promotion gate (median≥4/5, 100% build)
│   │   ├── prompt.py                          # load the optimized-prompt artifact for the production codegen sub-agent
│   │   └── artefact.py                       # produced-.cs path/validation (MUST be real C# — inverse of convspec's no-C# rule)
│   └── codegen_opt/                          # NEW — OFFLINE DSPy/GEPA optimizer (NOT durable-registered)
│       ├── __init__.py                       # Typer app (optimize/eval/export-prompt/show); reads OPENAI_API_KEY from env
│       ├── program.py                        # the dspy.Module codegen signature (plan+convspec+deps+idioms → C#)
│       ├── metric.py                         # composite metric fn for GEPA (build hard-gate, 0.6/0.4, human feed)
│       ├── dataset.py                        # eval/train split over plans/convspecs (held-out set)
│       └── optimize.py                       # GEPA driver + budget cap; serializes optimized prompt
├── durable/{steps.py,workflows.py}           # MODIFIED — register codegen step; add `codegen` stage after `plan`
├── tools/discover/tombstone.py              # MODIFIED — extend _FIELD_ORDER with codegen-state keys (append-only, after plan keys)
└── db/migrations/versions/0007_codegen.py    # NEW — dart_codegen (down_revision 0006)

.claude/skills/
├── codeconv-codegen/SKILL.md                 # NEW — codegen sub-agent prompt contract + human-review loop + frontier driver
└── codeconv-codegen-opt/SKILL.md             # NEW — offline optimizer driver

.codeconv/
├── conversion-code/_escalations-report.md    # NEW (checked in, FR-009)
├── codegen-prompt/optimized.md               # NEW (checked in) — GEPA-optimized prompt (production input)
└── tombstones/<rel>.dart.md                  # MODIFIED — appended codegen-state keys

out/csharp/                                   # FILLED — generated .cs (git policy: see research R11)
```

**Structure Decision**: Single-project Python additions inside `codeconv/`, mirroring 015–018. The architectural addition is the **two-tool split**: `tools/codegen/` (deterministic, auto-discovered, the only one in the durable pipeline) and `tools/codegen_opt/` (offline optimizer, NOT auto-registered, the only place DSPy/GEPA/litellm/OpenAI + API key live). The `durable/` codegen step wraps the production tool's pure entrypoint (D2 wrapping discipline). Tombstone keys append at the END of `_FIELD_ORDER`. The two skill loops are isolated to their `SKILL.md`s and justified below.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| **In-package DSPy/GEPA/litellm/OpenAI + API key** confined to `tools/codegen_opt/`. | The user requires GEPA/DSPy **actively** improving codegen against testing feedback — a real optimizer with a real LM. | (A) optimizing LM client on production/durable path — poisons DBOS replay-safety, adds secret/network/non-determinism. (B) no GEPA — drops the requested optimization. (C) keeps GEPA real but offline; production input is a static checked-in prompt artifact. |
| **`/codeconv-codegen` + `/codeconv-codegen-opt` not pure thin wrappers** — codegen sub-agent + human-review orchestration; optimizer driver. | FR-001/FR-006 require spawning Claude codegen sub-agents + a human-review loop; spawning Claude sub-agents is a harness capability the CLI lacks (017/018 precedent). | Python-spawns-agents via SDK — secret/network/non-determinism, breaks DBOS step determinism. `claude -p` shell-out — fragile, untestable. The split keeps Python + every DBOS step deterministic. |

## Phase 0: Research outputs

See [research.md](./research.md) for R1–R12 (C-hybrid topology; DSPy+GEPA wiring; replay-safe codegen step; dotnet build/test harness; codegen-readiness/batching; composite metric + human gate; optimized-prompt artifact; idiom-KB/conventions in context; codegen schema/migration; LM backend; cost/IP + `out/csharp/` git policy; crash/SCC + durable-builder integration). The one genuine open question — whether `out/csharp/` is committed or treated as regenerable output (R11) — is the top `/buildkit-analyze`/human-gate item.

## Phase 1: Design artefacts

See contracts/ (codegen_cli, codegen_opt_cli, dbos_codegen_stage, codegen_artifact_format, metric_contract, codegen_schema, agent_orchestration), [data-model.md](./data-model.md), and [quickstart.md](./quickstart.md).

The agent context file (`CLAUDE.md`) is updated this run to reference this plan between the `<!-- BUILDKIT START -->` / `<!-- BUILDKIT END -->` markers.
