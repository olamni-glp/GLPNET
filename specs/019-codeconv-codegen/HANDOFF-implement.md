# Handoff → `/buildkit-implement` (new session)

**Feature**: `019-codeconv-codegen` · **Branch**: `019-codeconv-codegen` · **Date**: 2026-05-23

## State (all green, committed, clean tree)
Pipeline (buildkit DB, `buildkit-builder status`): specify ✅ clarify ✅ plan ✅ tasks ✅ analyze ✅ → **Next: `/buildkit-implement`**.
- spec.md (5 clarifications, 0 markers), plan.md, research.md (R1–R12), data-model.md, 7 contracts, tasks.md (46 tasks). `/buildkit-analyze`: **0 CRITICAL**, 100% FR/SC coverage.
- Toolchain is **buildkit** (migrated from spec-kit). Use `/buildkit-*` commands. buildkit CLI on PATH (`buildkit`, `buildkit-builder`); pgdb local cluster (gitignored).

## Before implementing — RESOLVE THIS FIRST
- **T045 / research R11 — `out/csharp/` git policy**: decide commit vs gitignore the generated C# BEFORE bulk generation. Proposed default: **commit** (reviewed, build-gated product). Not yet decided.

## How to start the new session
1. New session reads CLAUDE.md (mandatory reading) → STOP & report per CLAUDE.md, await direction.
2. `buildkit-builder status` → confirms Next = /buildkit-implement.
3. Settle T045 (git policy) with Gabi.
4. `/buildkit-implement` — executes tasks.md. **MVP = US1** (T001–T021): build-passing `lib/` C# + escalate-don't-guess, no optimizer/review-gate yet. Then US2 (GEPA optimizer), US3 (human/test gate), US4 (durable), Polish.

## Key design (from plan.md / contracts)
- **(C) hybrid**: deterministic `tools/codegen/` (production, auto-discovered, in durable pipeline, LM-free) spawns harness codegen sub-agents (via `/codeconv-codegen`) that emit real C#; build-gated by `dotnet`. Offline `tools/codegen_opt/` runs DSPy+GEPA (OpenAI via litellm, `OPENAI_API_KEY` env, budget-capped) → a checked-in optimized-prompt artifact. **No LM on the production/durable path** (R3 replay-safety).
- Metric: build hard-gate → `0.6·tests + 0.4·human` (human-only pre-tests) → batch promote at 100% build + human median ≥4/5.
- Schema: `dart_codegen` + migration `0007` (down_revision 0006); tombstone codegen keys; `codegen` DBOS stage after `plan`.
- Bridge: codeconv cluster `C:/pglite/research/glpnet` (the codegen tool's DB) — distinct from buildkit's own `pgdb/`.

## Branch note
This branch was fast-forwarded to include the buildkit migration (commits `1e48904e`, `7058117a`) + plan/tasks. The `upgrade/buildkit-migration-20260523T101935Z` branch points at the same migration commits. Merge `019` → `main` is Gabi's call (main is at `f6693c30`, the pushed codeconv-018 work).
