# Phase 1 Data Model: Evidence-Based Constitution

**Feature**: 028-evidence-based-constitution | **Date**: 2026-06-10

This feature's "data" is the structure of the governance document and its sub-parts. No database, no runtime state — these are documentation entities whose "validation rules" are the FR/SC constraints.

## Entity: Constitution document

The single frozen governance artifact at `.specify/memory/constitution.md`.

| Field | Type | Rule |
|---|---|---|
| Project name / title | string | Replaces template `[PROJECT_NAME]` (e.g. "GLPnet Constitution"). |
| Principles | ordered list of **Principle** | 6–8 after owner walkthrough (default 8, floor 6, FR-001). Numerals I–VIII; III/IV/V/VI stable (FR-007). |
| Governance section | prose | Supersession + amendment procedure; references `docs/DISCIPLINE.md`/`CLAUDE.md`/`specs/` rather than duplicating (FR-009). |
| Non-elevation note | prose | Records why DISCIPLINE §1.12 GLP-First and §1.13 FCP-Reference-Architecture are not principles (FR-010). |
| Version | semantic version | `1.0.0` — NOT CalVer (FR-008). |
| Ratified | date | `2026-06-10` (FR-008). |
| Last Amended | date | `2026-06-10` (FR-008). |

**State transition**: pristine template (`[PLACEHOLDER]` tokens) → [owner walkthrough approves N principles] → frozen populated file. No partial/intermediate write state is ever persisted (FR-013; US2 scenario 3).

## Entity: Principle

One governance rule. Default set per FR-002: I, II, III, IV-a, IV-b, V, VI-a, VI-b, VII, VIII.

| Field | Type | Rule |
|---|---|---|
| Numeral / sub-letter | string | I–VIII (+ a/b on IV, VI). III/IV/V/VI immutable (FR-007). |
| Normative statement | MUST/SHOULD prose | (FR-003a). III/V/VI-a worded as analyze-LM scan instructions (FR-004). |
| Evidence anchor | **Evidence anchor** | Exactly one resolved anchor (FR-003b); dropped if unresolved (FR-011). |
| buildkit analog | string \| none | Present only where one genuinely exists (FR-003c). |
| Gate-ability label | enum | Exactly one of `machine-checkable` \| `judgement-gate-able` \| `advisory` (FR-003d). Must not overstate determinism (Edge Case). |

**Scan-instruction sub-type (III/V/VI-a, FR-004/FR-005)**: the MUST is phrased as an instruction the analyze LM executes against the **artifacts under review** (the feature's spec/plan/tasks), explicitly **not** against the constitution document itself.
- III: nonzero count of literal `skipSRSW` in artifacts under review ⇒ CRITICAL.
- V: nonzero count of `OPENAI_API_KEY` / `litellm` / `openai` in artifacts under review ⇒ CRITICAL.
- VI-a: single linear migration head asserted by `test_migration_*_single_head.py` (`heads == [0010]`) — not by a `versions/` filename count.

## Entity: Evidence anchor

A re-verified pointer into a glpnet artifact on disk.

| Field | Type | Rule |
|---|---|---|
| File path | path | Must exist on disk at scan time (FR-011). |
| Locator | heading \| FR number \| test name | Must resolve within the file at scan time (FR-011). |
| Resolution status | resolved \| dropped | Unresolved ⇒ dropped, never fabricated (FR-011, SC-003 = 100% resolve). |

Permitted artifact classes (FR-003b): `docs/DISCIPLINE.md`, `CLAUDE.md`, a `specs/NNN` doc, an FR number, or a codeconv migration/test.

## Entity: Negative-control fragment

Transient demonstration input — not persisted as a harness (FR-016).

| Field | Type | Rule |
|---|---|---|
| Token | `skipSRSW` \| `OPENAI_API_KEY` | Planted once in a throwaway artifact-under-review. |
| Expected verdict | CRITICAL | Via principle III (skipSRSW) / V (OPENAI_API_KEY), SC-002. |
| Persistence | none | Captured to `evidence/negative-control.md`; not added as a recurring test. |

## Entity: Analyze baseline pair

The before/after Constitution-Check transcripts evidencing the cosmetic→real transition.

| Field | Type | Rule |
|---|---|---|
| Target feature | 026 \| 027 | Owner choice; default 027 (FR-017, research Decision 6). |
| Before transcript | text | Template loaded → 0 MUSTs extracted → vacuous pass. Saved to `evidence/analyze-before.md`. |
| After transcript | text | Populated file → ≥6 MUSTs extracted + reasoned. Saved to `evidence/analyze-after.md`. |
| Outcome metric | counts | before MUSTs = 0; after MUSTs ≥ 6 (SC-001). |
