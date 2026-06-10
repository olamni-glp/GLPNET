# Quickstart / Runbook: Evidence-Based Constitution

**Feature**: 028-evidence-based-constitution | **Date**: 2026-06-10

The implement-stage runbook. Ordering is load-bearing: **grounding → before-baseline → owner walkthrough → write → after-baseline + negative-control → verify diff**. No write to `constitution.md` happens before the owner has approved every principle (FR-013).

## Step 1 — Fresh grounding scan (Claude-only, read-only) [FR-011, FR-012]

For each candidate principle, re-verify its Evidence anchor on disk *now* (don't trust the research table — re-check):
- `docs/DISCIPLINE.md` §1.1/§1.2/§1.4/§1.8/§1.12/§1.13/§1.14 headings present.
- `CLAUDE.md` sections: Spec-First, SRSW/`skipSRSW`, Language Authority, Preserve Working Code, Test Protocol, Single source of truth.
- `codeconv/tests/test_migration_*_single_head.py` exists incl. `_0010_`; current head = `0010_marathon_schema.py`.
- `specs/012-codeconv-runner/contracts/bridge_lifecycle.md`; memory `project_gepa_no_api_claude_only`.
- Any anchor that fails to resolve → drop the Evidence line; re-ground or surface the principle as unsupported.

## Step 2 — Capture the "before" baseline [FR-017, SC-001]

With the constitution still the pristine template, run `/buildkit-analyze` against feature **027** (or 026 — owner's call) and save the Constitution-Check section verbatim to `specs/028-evidence-based-constitution/evidence/analyze-before.md`. Expected: 0 MUSTs extracted / vacuous pass.

## Step 3 — Per-principle owner walkthrough [FR-013, US2 — DO NOT SKIP]

Present each candidate principle to Gabi one at a time: normative statement + resolved Evidence line + buildkit analog + gate-ability label. For each, obtain approve / edit / reject. Track the running count. If approvals would drop below **6**, surface the owner-merge floor rather than proceeding silently (US2 scenario 4). If an edit removes a literal scan token, downgrade that principle's gate-ability label accordingly (Edge Case). **Nothing is written to `constitution.md` during this step.**

## Step 4 — Freeze count, then write [FR-001, FR-007, FR-008]

Freeze the approved principle count (6–8). Confirm numerals III/IV/V/VI are unchanged. Overwrite `.specify/memory/constitution.md` in place with the approved set, per `contracts/constitution-structure.md`. Stamp `Version: 1.0.0`, `Ratified: 2026-06-10`, `Last Amended: 2026-06-10`. Include the Governance section + the non-elevation note (FR-010).

## Step 5 — Capture the "after" baseline [FR-017, SC-001]

Re-run `/buildkit-analyze` against the same feature; save the Constitution-Check section to `evidence/analyze-after.md`. Expected: ≥6 MUSTs extracted and reasoned about. The before/after pair is the deliverable evidence.

## Step 6 — Negative-control demonstration [FR-016, SC-002]

In a throwaway artifact-under-review, plant a `skipSRSW` fragment and (separately) an `OPENAI_API_KEY` fragment; confirm the Constitution Check flags each CRITICAL (III and V). Record to `evidence/negative-control.md`. Do **not** commit a recurring test. Also confirm the constitution's *own* token mentions did not self-flag (SC-005).

## Step 7 — Verify scope & ship [FR-018, SC-006, SC-007]

`git diff --stat` MUST be confined to `.specify/memory/constitution.md` + `specs/028-evidence-based-constitution/**`. Confirm: no GLP runtime/`.glp`/language-definition file touched; `/buildkit-analyze` skill unmodified; no grep harness added; no pipeline command was auto-invoked. Commit by name only, then ship via the buildkit GitFlow.

## Done when

- SC-001 before=0 / after≥6 captured · SC-002 both CRITICAL demonstrated · SC-003 100% anchors resolve · SC-004 frozen set written with stable numerals + semantic version · SC-005 no self-flag · SC-006 diff confined · SC-007 no auto-invoke + no pre-approval write.
