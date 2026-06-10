# Implementation Plan: Engine Review + Refactoring Design Dossier

**Branch**: `026-engine-review-dossier` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/026-engine-review-dossier/spec.md`

## Summary

Produce a single **authoritative design dossier** for the *separation-of-REPL-front-end-from-engine-execution-scheduler* epic. This is marathon **step 2** (the refactoring design) **+ step 3** (turn the design into pipeline-ready features). Step 1 — the read-only multi-agent engine review — is already complete in `docs/research/repl-engine-separation/investigation.md`.

The deliverable is a documentation artifact only: it **changes no engine, runtime, or REPL code** (FR-015, SC-006). The dossier covers seven design areas (seam contract, wire shapes, control-program/client model, liveness/crash/restart, persistent-vs-ephemeral state + DB-abstraction + bootstrap + resume, mailbox decision, MVP slice), reconciles two requirement/code premise mismatches, presents every open design question as fully-researched **options for the owner to decide** (the dossier recommends but does not unilaterally settle forks — FR-011, FR-018), authors an ordered topologically-valid successor-feature breakdown, and — **only after the owner approves the dossier** — seeds successor features 2–16 into `buildkit-roadmap` as candidates (FR-019).

Technical approach: re-read the as-built C# reference (`out/csharp`) + feature-025 link layer (`csharp/glp_link`) + the durable store (`codeconv/.../marathon`), cross-checked against the Dart source (`glp_runtime`), re-verifying every claim inherited from step 1 against current code (FR-016). Each design area is tagged reuse / refactor / net-new with `file:line` citations (FR-014, SC-008). The dossier consolidates and supersedes the design/recommendation content sketched in `investigation.md` §1–§8; `investigation.md` remains the read-only step-1 review of record.

## Technical Context

**Language/Version**: N/A — documentation-only deliverable (Markdown). The *subject* code is C# (.NET, `out/csharp`) with a Dart mirror (`glp_runtime`, Dart ^3.9.4); no code in either is modified.
**Primary Dependencies**: None built. Read-only source inputs: `out/csharp` (engine reference), `csharp/glp_link` (feature-025 transport/frame/payload), `codeconv/src/codeconv/marathon` (durable-store template), `glp_runtime` (Dart cross-check). Tool dependency for the post-approval step only: `buildkit-roadmap` CLI (candidate seeding, FR-019).
**Storage**: The dossier is a Markdown file under `docs/research/repl-engine-separation/`. The post-approval roadmap candidates persist in the buildkit roadmap store (DBOS-on-PGLite); this feature seeds rows but defines no schema.
**Testing**: No executable tests — this feature ships no code. Verification is by **checklist against the spec**: the spec's per-story Independent Tests + Acceptance Scenarios + the measurable Success Criteria (SC-001..SC-010) are the acceptance gate. A "passing" dossier is one where every SC is satisfiable by reading the dossier alone (SC-005).
**Target Platform**: N/A (documentation). Subject engine targets Linux/Windows OS-level hosting (liveness/restart design area).
**Project Type**: Documentation / design dossier (single authored document + roadmap-seeding action). Not a code project.
**Performance Goals**: N/A. Quality goal instead: every owner-decision option is grounded in cited `file:line` evidence and/or named prior art, stated concisely (option + consequences + trade-off in a few lines, not a narrative) — FR-018, SC-009.
**Constraints**: Read-only w.r.t. engine/runtime/REPL code (FR-015); diff touches documents only (SC-006). Present-options / owner-decides — no fork recorded as settled (FR-011). Roadmap seeding is gated on owner approval and seeds candidates only — no successor feature is specified/planned/implemented (FR-019, SC-010). C#-first reference, Dart parity noted where applicable.
**Scale/Scope**: One dossier covering 7 design areas + 2 premise reconciliations + the full open-question set + a 16-entry feature breakdown + a risk register. ~16 successor-feature candidates seeded post-approval.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The project constitution (`.specify/memory/constitution.md`) is an **unfilled template** — it defines no ratified project-specific principles or gates. There are therefore no constitution gates to evaluate against. The governing constraints for this feature are the spec's own (read-only, present-options, evidence-grounded, approval-gated seeding), all of which the plan honours.

| Gate | Status | Note |
|------|--------|------|
| Constitution principles defined | N/A | Template unpopulated; no gates to check |
| Read-only-code constraint (FR-015) | PASS | Plan produces docs + a post-approval roadmap-seed action; touches no engine/runtime/REPL code |
| Present-options / owner-decides (FR-011) | PASS | Plan treats every fork as an owner decision; recommendations marked advisory |
| Evidence-grounded options (FR-018) | PASS | Plan mandates `file:line` / prior-art citation per option |
| Approval-gated seeding (FR-019) | PASS | Roadmap seeding sequenced strictly after owner approval, candidates-only |

**Result: PASS** (no violations; Complexity Tracking not required).

## Project Structure

### Documentation (this feature)

```text
specs/026-engine-review-dossier/
├── plan.md              # This file (/buildkit-plan output)
├── research.md          # Phase 0 output — planning decisions + source-input map
├── data-model.md        # Phase 1 output — the dossier's content entities
├── quickstart.md        # Phase 1 output — author/verify the dossier; how successors cite it
├── contracts/           # Phase 1 output
│   ├── dossier-outline.md          # the required section structure (the FR→section map)
│   └── roadmap-candidate.md        # the post-approval roadmap-seed candidate schema
├── checklists/
│   └── requirements.md  # spec-quality checklist (already PASS)
├── spec.md              # feature spec (clarified)
└── tasks.md             # Phase 2 output (/buildkit-tasks — NOT created here)
```

### Source Code (repository root)

This feature writes **no source code**. The only repository artifact it creates/updates outside `specs/026-engine-review-dossier/` is the dossier and (post-approval) roadmap rows:

```text
docs/research/repl-engine-separation/
├── investigation.md             # step-1 review of record (READ-ONLY input; not modified)
├── requirements.md              # owner requirements (READ-ONLY input)
├── feature-definition.md        # marathon framing §8 (READ-ONLY input)
├── llvm-feasibility.md          # READ-ONLY input (feeds experiment-feature entries)
├── research-programme.md        # READ-ONLY input (feeds research-programme entry)
└── design-dossier.md            # ← THE DELIVERABLE (net-new, authored by this feature)
```

Read-only subject trees (cited, never modified): `out/csharp/` (engine + REPL reference), `csharp/glp_link/` (feature-025 link layer), `codeconv/src/codeconv/marathon/` (durable-store template), `glp_runtime/` (Dart cross-check), `programs/self.glp` (bootstrap + GLP link wrappers).

**Structure Decision**: Documentation-deliverable layout. The dossier lives at `docs/research/repl-engine-separation/design-dossier.md`, alongside its source inputs (per spec Assumptions "Output location"), so successor features cite one canonical path. No `src/`/`tests/` trees are created — this is not a code feature.

## Complexity Tracking

> No constitution violations. Section intentionally empty.
