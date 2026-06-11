# Analyze Baseline — AFTER (populated constitution)

**Captured**: 2026-06-10 | **Target feature**: 027-refinement-verification-framework | **FR-017 / SC-001 (after half)**

## State of `.specify/memory/constitution.md` at capture

Populated, frozen, `Version 1.0.0`. The Constitution Check now extracts real MUST statements.

## MUST statements extracted (8 principles)

| # | Principle | Normative MUST extracted | Label |
|---|---|---|---|
| I | Spec-First | No implementation without an identified, quoted, consistent spec; spec wins on conflict | judgement |
| II | Bug-Protocol | STOP+report before any fix; no robustness-as-workaround | judgement |
| III | SRSW | scan artifacts under review for `skipSRSW`; nonzero ⇒ CRITICAL | machine-checkable |
| IV-a | Language Authority | no GLP language change without explicit owner approval | judgement |
| IV-b | Preserve Internals | never remove load-bearing internals without approval | judgement |
| V | Claude-Only LM | scan for `OPENAI_API_KEY`/`litellm`/`openai` on LM path; nonzero ⇒ CRITICAL | machine-checkable |
| VI-a | Single-Head Persistence | single head asserted by `test_migration_*_single_head.py` (`heads==[0010]`) | machine-checkable |
| VI-b | Single PGLite Cluster | one `.pgdb/` + sibling lock; shared bridge | judgement |
| VII | Test-Gated Shipping | baseline-green, commit-scoped, GitFlow ship | advisory |
| VIII | Single Source of Truth | one authoritative spec per subsystem; roadmap→pipeline traceability | judgement |

**Extracted MUSTs: 10 normative statements across 8 principles** (≥ 6 required by SC-001). Before = 0.

## Reasoning applied against feature 027's artifacts

- **I / VIII**: 027 has a full spec→plan→tasks set and references its sources rather than duplicating — **conforms**.
- **III (scan)**: `skipSRSW` count in 027 spec/plan/tasks = **0** → no finding.
- **V (scan, judgement nuance worth recording)**: the literal tokens `OPENAI_API_KEY`/`litellm`/`openai` **do appear** in 027's artifacts — but exclusively as the *prohibition rule* (027's own no-API gate: "MUST forbid OpenAI/litellm/`OPENAI_API_KEY`… any 'needs an API' requirement is a defect to delete"), **not** on an LM execution path. The analyze LM correctly distinguishes a prohibition-mention from an API-usage path and does **not** flag 027 CRITICAL. This demonstrates the honest limit of the `machine-checkable` label: token *presence* is mechanical, but classifying it as "on an LM path" is best-effort LM judgement (consistent with the spec's Assumption). For an artifact that actually *used* the API, V fires CRITICAL — see `negative-control.md`.
- **VI-a (scan)**: 027 introduces no migration; head test family still asserts `heads == [0010]` → no finding.
- **VII**: advisory — not gated.

## Outcome

**Before: 0 MUSTs extracted (vacuous pass). After: 10 MUSTs across 8 principles, reasoned against 027.** The cosmetic→real transition is demonstrated (SC-001). The constitution's own token mentions did not self-flag (SC-005), because the self-mention boundary in the Governance section scopes III/V to the artifacts under review.
