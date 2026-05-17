# Contract: conversion-plan artefact + escalation + tombstone YAML delta

Implements spec FR-006, FR-007, FR-008, FR-010, FR-013, FR-016, FR-017; SC-003, SC-004, SC-005. Artefact content is authored by the planning sub-agent (`agent_orchestration.md`); structural validation + path resolution is `codeconv/src/codeconv/tools/planagents/artefact.py`.

## Source of truth references

- Spec FR-010 (one artefact per tombstone, mandated sections, checked in), FR-016 (aggregated report), FR-017 (escalations gate conversion not planning).
- Research R7 (path/git), R10 (escalations), R11 (tombstone keys).

## Artefact path (FR-010 / R7)

`.codeconv/conversion-plans/<rel>.dart.md` — a parallel mirrored tree of `.codeconv/tombstones/<rel>.dart.md`. **Checked into git.** One artefact per tombstone (one Dart file). Default root overridable via the CLI; `<rel>` is the same subtree-relative POSIX path used by `dart_files.path` / the tombstone tree.

## Mandated artefact structure (FR-010 / SC-004)

A valid artefact is Markdown with this exact top-level section order (structural check in `artefact.py`; SC-004):

```markdown
---
path: <rel>.dart
cycle_group_id: <int>
scc_siblings: [<path>, ...]            # empty list for singletons
generated_at: <ISO8601 Z>             # the ONLY field allowed to vary on an idempotent re-plan (SC-003)
source_sha256: <hex of the .dart at plan time>
schema_version: 1
---

# Conversion Plan: <rel>.dart

## 1. Source Analysis
<summary grounded in ACTUAL .dart inspection (FR-006) — public surface,
 types, async/stream usage, mixins, extension methods, codegen, etc.>

## 2. Dart → C#/.NET Conversion Plan
<interface/semantics/results/observable-behaviour-preserving mapping (FR-006).
 Each Dart construct → its C#/.NET equivalent, with the rationale.>

## 3. Decomposed Task Units
<small, individually & reliably implementable units (FR-007), each with a
 one-line definition-of-done. Ordered; numbered T1, T2, ….>

## 4. Research Findings
<"none required" OR findings returned by the SEPARATE research sub-agent,
 each with provenance + the VERBATIM external request(s) issued (FR-009).>

## 5. Consistency Pass
<cross-check of §2 vs §3 vs §4 vs spec/referenced contracts (FR-008).
 Each gap: either "fixed (pre-specified, incremental) — derived from <cite>"
 OR "ESCALATED → see §6".>

## 6. Escalations
<zero or more structured escalation entries — schema below. Empty section
 contains the literal line: "None.">

## 7. Cycle Siblings                    # present ONLY when scc_siblings non-empty
<for each sibling: the cross-reference note explaining the shared-cycle
 coupling and which decisions are co-dependent (FR-011).>
```

Sections 1–6 are mandatory and MUST appear in this order (SC-004). Section 7 is mandatory iff `scc_siblings` is non-empty, forbidden otherwise.

## Structured escalation schema (FR-008 / FR-016 / SC-005)

Each entry in `## 6. Escalations`:

```markdown
### E<n>: <one-line title>
- **File(s)**: <rel>.dart [, sibling paths if SCC-coupled]
- **Observed**: <what was found in the source/plan/research>
- **Why not pre-specified+incremental**: <which of: language-semantics
  judgement / unwritten mapping / scope growth — and why it is not
  verbatim-derivable from spec / a referenced 012/015 contract / a written
  project convention (FR-008)>
- **Decision required**: <the precise question the engineer must answer>
- **Status**: open            # or: resolved (<date> — <resolution ref>)
```

- An escalation MUST be raised (not guessed) whenever the resolution is not verbatim-derivable per FR-008 (DISCIPLINE.md §1.2/§1.10 encoded). SC-005: zero un-escalated unresolved gaps.
- "research unavailable for X" is a valid escalation `Observed`/`Why` (Clarification Q6 / R10): the plan still completes best-effort and is marked completed-with-escalation; conversion is blocked, planning is not.
- A `--replan` that supersedes a prior artefact MUST carry forward prior open escalations as `### E… Status: open (carried from <prior generated_at>)` — never silently dropped (R9).

## `open_escalation_count` (FR-017)

= the number of `Status: open` entries in `## 6`. Recorded into `dart_plans.open_escalation_count` by `plan-completed --escalations <n>` (the skill parses the artefact and passes the count). `> 0` ⇒ the plan is `planned` for the **planning** frontier (downstream planning proceeds — FR-004/FR-017) but flagged **conversion-blocking** (queryable via the schema index — `planagents_schema.md`).

## Aggregated escalations report (FR-016)

`codeconv planagents aggregate-escalations` walks `.codeconv/conversion-plans/**.dart.md`, collects every `Status: open` entry, and writes `.codeconv/conversion-plans/_escalations-report.md` (path overridable). The report is **checked in**, ordered by `(path ASC, E-number ASC)`, and each item reproduces File(s)/Observed/Why/Decision verbatim from the source artefact plus a back-link to `<rel>.dart.md#e<n>`. The `_`-prefix sorts the report first and cannot collide with a `<rel>.dart.md` artefact path.

## Tombstone YAML delta (FR-013 / R11)

Four keys appended to `_FIELD_ORDER` after feature-015's six (data-model §2): `plan_started_at`, `plan_completed_at`, `plan_path`, `open_escalation_count`. Null-vs-missing convention identical to feature 015 (omitted ⇒ no `dart_plans` row; present-`null` ⇒ row exists, field NULL). Artefact **content is NOT mirrored into YAML** — only plan *state* (FR-010/FR-013); the artefact is the durable content record.

## Idempotence (SC-003)

- A re-`stamp-tombstones` / re-`plan-completed` on unchanged state is byte-identical (append-only `_FIELD_ORDER`, canonical YAML emitter — data-model §2).
- A re-plan of an unchanged source + plan state produces an artefact that differs only in the front-matter `generated_at` field (SC-003). `artefact.py`'s structural validator treats `generated_at` as the sole volatile field.
- Idempotent recovery: a half-written artefact from a crashed agent (no matching `plan_completed_at`) is NOT treated as complete; the file stays `plan_in_progress` and is re-plannable (edge case "plan-started but never completed").

## Structural validation (SC-004) — `artefact.py`

`validate(path)` returns ok iff: front-matter has all required keys; sections 1–6 present in order; section 7 present iff `scc_siblings` non-empty; every `### E<n>` has all five bullet fields; `## 6` is either ≥1 entry or the literal `None.`. Used by `test_planagents_escalations.py` and the SC-004 audit. `validate` does NOT judge plan *quality* (that is the agent's job) — only structural conformance.
