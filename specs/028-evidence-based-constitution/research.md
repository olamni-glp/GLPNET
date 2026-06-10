# Phase 0 Research: Evidence-Based Constitution

**Feature**: 028-evidence-based-constitution | **Date**: 2026-06-10

The Technical Context carries no `NEEDS CLARIFICATION` markers (governance-documentation feature, no tech-stack unknowns). Research here resolves the *method* questions: how to ground evidence, how to label gate-ability, how to handle the sanctioned merges, and which artifacts back each principle. Candidate evidence anchors were spot-verified on disk during planning; FR-011 requires a final fresh re-verification at implement time.

## Decision 1 — Grounding-scan method (FR-012)

- **Decision**: A Claude-only, read-only repo scan. For each candidate principle, locate a heading-anchored artifact (`docs/DISCIPLINE.md` heading, `CLAUDE.md` section, a `specs/NNN` doc, an FR number, or a codeconv migration/test name) and confirm both the file and the anchor exist on disk. Any anchor that does not resolve is dropped (never fabricated); the principle is either re-grounded on a located artifact or surfaced to the owner as unsupported.
- **Rationale**: "Evidence-based" is only meaningful if each citation is real and current (US3). A Claude-only read-only scan satisfies FR-012 and is itself consistent with principle V the feature establishes (no OpenAI/litellm/`OPENAI_API_KEY`).
- **Alternatives rejected**: (a) a grep/scanning harness — explicitly forbidden by FR-015 (determinism is honest instruction-level wording, not code); (b) trusting the prescriptive brief's anchors without re-verification — violates FR-011/US3.

## Decision 2 — Gate-ability taxonomy (FR-003)

- **Decision**: Exactly three labels — `machine-checkable` | `judgement-gate-able` | `advisory` — one per principle.
  - `machine-checkable`: III, V (literal-token scan ⇒ CRITICAL), VI-a (single-head asserted by `test_migration_*_single_head.py`).
  - `judgement-gate-able`: I, II, IV-a/IV-b, VI-b, VIII (LM compares artifacts against the MUST and reasons).
  - `advisory`: VII, and VIII's roadmap-linkage clause (FR-006 — out-of-scope 027 reconcile / slug drift MUST NOT be retroactively flagged).
- **Rationale**: The label must never overstate determinism. If an owner edit removes a literal scan token (e.g. drops `skipSRSW` from III), the label downgrades accordingly (Edge Case: machine-checkable → judgement/advisory).
- **Alternatives rejected**: a binary pass/fail label — loses the honest distinction between token-scan determinism and LM judgement.

## Decision 3 — Sanctioned merges & numeral stability (FR-007)

- **Decision**: Default set is 8 (I–VIII with sub-letters IV-a/b, VI-a/b). Two *content* merges are pre-identified as owner options during the walkthrough: **II → I** (Bug-Protocol folds under Spec-First) and **VII commit-clause → VIII**. Regardless of merges, the displayed numerals **III / IV / V / VI remain stable** so downstream references don't drift. The principle count is frozen *before* any byte is written. Owner-merge floor = 6.
- **Rationale**: Stable middle numerals keep any existing/future cross-references valid; freezing the count before write prevents mid-write renumbering. The walkthrough (US2) is where merges are actually decided.
- **Alternatives rejected**: renumber-on-merge (would drift references); hard-coding 6 (removes owner discretion below the floor-aware default of 8).

## Decision 4 — Verified candidate evidence anchors

Spot-verified on disk 2026-06-10 (final fresh re-verification is an implement task per FR-011):

| Principle | Candidate anchor (verified present) | buildkit analog | Gate-ability |
|---|---|---|---|
| I Spec-First | `docs/DISCIPLINE.md` §1.1 Specification-First Development; `CLAUDE.md` "Spec-First Development" | spec→plan→tasks pipeline ordering | judgement-gate-able |
| II Bug-Protocol / No-Workarounds | `docs/DISCIPLINE.md` §1.2 No Workarounds, §1.8 Bug Handling | — | judgement-gate-able (merge-candidate → I) |
| III SRSW inviolable | `docs/DISCIPLINE.md` (SRSW), `CLAUDE.md` "SRSW … never invent or use a `skipSRSW` option" | — | **machine-checkable** (scan `skipSRSW`) |
| IV-a Language Authority | `docs/DISCIPLINE.md` §1.14 Language Design Authority; `CLAUDE.md` "Language Authority" | — | judgement-gate-able |
| IV-b Preserve Working Internals | `CLAUDE.md` "Preserve Working Code" (`_ClauseVar`/`_TentativeStruct`) | — | judgement-gate-able |
| V Claude-Only LM / No External API | memory `project_gepa_no_api_claude_only`; codeconv-codegen-opt skill | — | **machine-checkable** (scan `OPENAI_API_KEY`/`litellm`/`openai`) |
| VI-a Additive-only single-head persistence | `codeconv/tests/test_migration_*_single_head.py` (incl. `_0010_`); head = `0010_marathon_schema.py` | Alembic single-head test family | **machine-checkable** (test asserts `heads == [0010]`) |
| VI-b Single OS-lock-guarded PGLite cluster | `specs/012-codeconv-runner/contracts/bridge_lifecycle.md`; `.pgdb.bridge.lock` sibling-path | — | judgement-gate-able |
| VII Test-Gated Commit-Scoped Shipping | `CLAUDE.md` "Test Protocol" / "Git Workflow / Commit scope"; `docs/BRANCHING.md` | buildkit ship preflight | **advisory** (FR-006) |
| VIII Single Source of Truth & Traceability | `docs/DISCIPLINE.md` §1.4 Traceability; `CLAUDE.md` "Single source of truth" | roadmap→pipeline traceability (roadmap-linkage clause advisory) | judgement-gate-able |

## Decision 5 — Non-elevation note (FR-010)

- **Decision**: The constitution records explicitly *why* `docs/DISCIPLINE.md` §1.12 (GLP-First Implementation) and §1.13 (FCP Reference Architecture) are **not** raised to principles: they are implementation-methodology guidance, not gate-able invariants over spec/plan/tasks artifacts, so elevating them would create advisory-only noise in every analyze run without a checkable conformance signal.
- **Both sections verified present** in `docs/DISCIPLINE.md` (§1.12, §1.13).

## Decision 6 — Baseline target (FR-017)

- **Decision**: Default to feature **027** for the before/after `/buildkit-analyze` baseline (just shipped 2026-06-10, complete spec/plan/tasks on disk, freshest). Spec Assumptions keep 026 as an owner-selectable alternative — either satisfies FR-017. Owner confirms at implement time.
- **Rationale**: A feature with a full, on-disk artifact set gives the analyze LM real MUSTs to extract in the "after" run.

## Decision 7 — Version stamp (FR-008)

- **Decision**: Semantic `Version: 1.0.0` (first ratification), with `Ratified: 2026-06-10` and `Last Amended: 2026-06-10`. **Not** a CalVer tag (CalVer is for release tags, not the governance doc).
