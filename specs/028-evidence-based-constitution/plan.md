# Implementation Plan: Evidence-Based Constitution

**Branch**: `028-evidence-based-constitution` | **Date**: 2026-06-10 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/028-evidence-based-constitution/spec.md`

## Summary

Populate the pristine `.specify/memory/constitution.md` template with a FROZEN, evidence-grounded set of governance principles (default 8 / owner-merge floor 6) so the `/buildkit-analyze` Constitution Check stops passing vacuously and becomes a real gate. Each principle carries a normative MUST/SHOULD, an on-disk-verified Evidence anchor, a buildkit analog where one exists, and an explicit gate-ability label (`machine-checkable` | `judgement-gate-able` | `advisory`). Principles III/V/VI-a are worded as scan instructions the analyze LM executes against the *artifacts under review*. The write happens only after a per-principle owner walkthrough; the deliverable is validated by a planted negative control (III/V fire CRITICAL) and a before/after analyze baseline on feature 026/027. Governance-documentation only — no GLP runtime/`.glp`/language-definition changes, no `/buildkit-analyze` edit, no grep harness.

## Technical Context

**Language/Version**: N/A — Markdown governance document (no executable code).
**Primary Dependencies**: buildkit `/buildkit-analyze` skill (consumer of the artifact, unmodified); existing `.specify/` layout + constitution template.
**Storage**: Files only — `.specify/memory/constitution.md` (the deliverable) + `specs/028-evidence-based-constitution/evidence/` (captured-evidence notes).
**Testing**: No new automated tests. Validation is (a) a one-time negative-control demonstration (planted `skipSRSW` / `OPENAI_API_KEY` → CRITICAL) and (b) a before/after `/buildkit-analyze` Constitution-Check transcript pair on feature 026/027. Evidence-anchor resolution is verified by read-only on-disk lookup at scan time.
**Target Platform**: Repo-local (glpnet on Windows; platform-agnostic Markdown).
**Project Type**: Governance documentation (single-artifact authoring feature).
**Performance Goals**: N/A.
**Constraints**: Claude-only / no external API (FR-012, consistent with the principle V it establishes); read-only grounding scan; no write before full owner walkthrough; numerals III/IV/V/VI frozen-stable; diff confined to the constitution file + this feature's spec artifacts + `evidence/`.
**Scale/Scope**: One ~6–8-principle document; one before/after baseline pair; one negative-control demonstration.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Pre-design gate result: PASS (vacuous).** `.specify/memory/constitution.md` is the pristine buildkit template — all `[PLACEHOLDER]` tokens, zero extractable MUST statements. The Constitution Check therefore has no principles to gate against and passes vacuously. **This vacuous pass is not incidental — it is precisely the "before" baseline (FR-017 / SC-001) that this feature exists to eliminate.** No violations are possible because there is nothing yet to violate.

**Self-conformance note (the feature governs itself):** although the constitution is not yet populated, this plan is authored to conform to the principles it will introduce, so the feature does not contradict its own future gate:
- **I Spec-First**: spec.md authored and clarified before this plan; no code precedes spec. ✓
- **V Claude-Only LM / No External API**: the grounding scan is Claude-only, read-only; no OpenAI/litellm/`OPENAI_API_KEY` (FR-012). ✓
- **VII Test-Gated, Commit-Scoped Shipping**: diff confined to constitution + feature artifacts (FR-018, SC-006); commit by name only. ✓
- **VIII Single Source of Truth**: the constitution references `docs/DISCIPLINE.md` / `CLAUDE.md` / `specs/` rather than duplicating them (FR-009). ✓
- **III SRSW / VI Persistence / II Bug-Protocol / IV Language-Authority**: not engaged — no GLP code, no migrations, no language changes (FR-018). N/A. ✓

**Post-design re-check (after Phase 1): PASS.** The Phase 1 artifacts add only documentation (data-model, the constitution-structure contract, quickstart, research). No design decision introduces a constitution violation; the self-conformance items above still hold. The first *real* (non-vacuous) Constitution Check will run when `/buildkit-analyze` executes on the populated file — that run is the "after" half of the FR-017 baseline, captured as evidence, not gated here.

## Project Structure

### Documentation (this feature)

```text
specs/028-evidence-based-constitution/
├── plan.md              # This file (/buildkit-plan output)
├── spec.md              # Feature spec (clarified)
├── research.md          # Phase 0 output — grounding-scan method, evidence-anchor decisions, merge/numeral policy
├── data-model.md        # Phase 1 output — Constitution / Principle / Evidence-anchor entity shapes
├── quickstart.md        # Phase 1 output — owner-walkthrough + negative-control + baseline-capture runbook
├── contracts/
│   └── constitution-structure.md   # Phase 1 output — the on-disk shape the written constitution.md MUST satisfy
├── checklists/
│   └── requirements.md  # Spec quality checklist (already present)
├── evidence/            # Captured-evidence notes (created during /buildkit-implement)
│   ├── analyze-before.md       #   FR-017 baseline "before" transcript (vacuous pass)
│   ├── analyze-after.md        #   FR-017 baseline "after" transcript (MUSTs extracted)
│   └── negative-control.md     #   FR-016 planted skipSRSW / OPENAI_API_KEY → CRITICAL demonstration
└── tasks.md             # Phase 2 output (/buildkit-tasks — NOT created by /buildkit-plan)
```

### Source Code (repository root)

No source tree is created or modified. The single non-spec artifact written by this feature is:

```text
.specify/memory/constitution.md   # template overwritten in place with the frozen principle set
```

**Structure Decision**: This is a governance-documentation feature, not a code feature — there is no `src/`/`tests/` layout to choose. The deliverable is one Markdown file (`.specify/memory/constitution.md`); all other outputs are this feature's own spec-dir artifacts (`research.md`, `data-model.md`, `contracts/constitution-structure.md`, `quickstart.md`, and the `evidence/` notes). Per FR-018/SC-006 the diff is confined to those paths — explicitly excluding GLP runtime code, `.glp` source, the GLP language definition, and the `/buildkit-analyze` skill.

## Complexity Tracking

> No Constitution Check violations — this table is intentionally empty. The pre-design gate passes vacuously (nothing to violate yet) and the post-design re-check introduces no violations.
