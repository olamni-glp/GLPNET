# Feature Specification: Evidence-Based Constitution

**Feature Branch**: `028-evidence-based-constitution`  
**Created**: 2026-06-10  
**Status**: Draft  
**Input**: User description: "Populate glpnet's empty .specify/memory/constitution.md with an evidence-based, non-negotiable constitution so the /buildkit-analyze Constitution Check becomes a real gate instead of a cosmetic one. … (full prescriptive brief — frozen 8 principles, owner-merge floor 6; per-principle Evidence + buildkit-analog + gate-ability label; principles III/V/VI-a worded as analyze-LM scan instructions; negative-control + before/after analyze baseline validation; governance-documentation only; per-principle owner walkthrough before write)."

## Clarifications

### Session 2026-06-10

- Q: Where should the captured-evidence notes (before/after analyze transcripts, negative-control demonstration) live? → A: `specs/028-evidence-based-constitution/evidence/` (e.g. `analyze-before.md`, `analyze-after.md`, `negative-control.md`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The Constitution Check becomes a real gate (Priority: P1)

The owner runs `/buildkit-analyze` on a downstream feature (e.g. a future spec/plan/tasks set). Today the Constitution Check loads `.specify/memory/constitution.md`, finds only `[PLACEHOLDER]` tokens, and therefore extracts no MUST statements — so it passes vacuously. After this feature, the file holds a frozen set of normative MUST/SHOULD principles; the analyze LM extracts each MUST, compares the artifacts under review against it, and emits genuine CRITICAL / judgement findings instead of a cosmetic pass.

**Why this priority**: This is the entire point of the feature. Without a populated, MUST-bearing constitution, every downstream `/buildkit-analyze` Constitution Check is theatre. Delivering only this story already converts the gate from cosmetic to real.

**Independent Test**: Capture a `/buildkit-analyze` Constitution-Check transcript on feature 026 or 027 **before** the file is populated (baseline: no MUSTs extracted / vacuous pass), populate the file, re-run on the same feature, and confirm the **after** transcript extracts the principles' MUSTs and reasons about them. The before/after pair is the deliverable evidence.

**Acceptance Scenarios**:

1. **Given** the unfilled constitution template, **When** `/buildkit-analyze` runs its Constitution Check, **Then** it extracts zero MUST statements (baseline captured verbatim).
2. **Given** the populated constitution, **When** `/buildkit-analyze` runs on the same feature, **Then** it extracts each principle's MUST and reports per-principle conformance/conflict.
3. **Given** the populated constitution, **When** an artifact under review contains the literal token `skipSRSW`, **Then** principle III's scan instruction causes the finding to be flagged CRITICAL.
4. **Given** the populated constitution, **When** an artifact under review contains `OPENAI_API_KEY`, `litellm`, or `openai`, **Then** principle V's scan instruction causes the finding to be flagged CRITICAL.

### User Story 2 - Per-principle owner walkthrough before any write (Priority: P1)

Before a single byte is written to `constitution.md`, the feature presents each candidate principle to the owner (Gabi) one at a time — its normative statement, its on-disk Evidence line, its buildkit analog (if any), and its gate-ability label — and obtains explicit per-principle approval, edit, or rejection. The file is written only after the full walkthrough, reflecting exactly the approved set.

**Why this priority**: The constitution is FROZEN and non-negotiable once written; it becomes the authority every later feature is gated against. An unreviewed auto-write would bake in unverified or unwanted rules. Owner sign-off per principle is the safety gate that makes freezing acceptable.

**Independent Test**: Run the feature against a scratch copy; confirm no write to `constitution.md` occurs until every principle has been individually presented and approved, and that a rejected principle does not appear in the written file.

**Acceptance Scenarios**:

1. **Given** the grounding scan has produced candidate principles, **When** the walkthrough begins, **Then** each principle is presented individually with statement + Evidence + analog + gate-ability label.
2. **Given** the owner rejects or edits a principle, **When** the file is written, **Then** the written content matches the approved/edited set, not the original proposal.
3. **Given** the walkthrough is incomplete, **When** at any point before full approval, **Then** `constitution.md` remains the pre-existing template (no partial write).
4. **Given** approvals would leave fewer than 6 principles, **When** counting before write, **Then** the feature surfaces the owner-merge floor (6) rather than silently writing below it.

### User Story 3 - Evidence is grounded, freshly verified, and dropped if absent (Priority: P2)

The feature runs a Claude-only (no external API) read-only repo scan to ground each principle in a re-verified, heading-anchored glpnet artifact (`docs/DISCIPLINE.md`, `CLAUDE.md`, a `specs/NNN` doc, an FR number, or a codeconv migration/test). Any Evidence line whose artifact or anchor cannot be located on disk at scan time is dropped — never fabricated or guessed.

**Why this priority**: "Evidence-based" is only meaningful if each citation is real and current. Stale or invented anchors would make the constitution untrustworthy and the gate unjustifiable. This hardens stories 1–2 but they remain demonstrable without it, so P2.

**Independent Test**: Inspect every Evidence line in the proposal; each must resolve to an existing file + heading/FR/test on disk. Plant one deliberately-wrong anchor in a candidate and confirm the scan drops it rather than emitting it.

**Acceptance Scenarios**:

1. **Given** a candidate principle cites a heading-anchored artifact, **When** the scan verifies it, **Then** the artifact and anchor are confirmed present on disk before the Evidence line is kept.
2. **Given** a cited artifact or anchor is absent, **When** the scan runs, **Then** that Evidence line is dropped (and the principle either re-grounded on a located artifact or surfaced to the owner as unsupported).

### Edge Cases

- **Self-trigger of the III/V scan instructions.** The constitution itself will contain the literal tokens `skipSRSW`, `OPENAI_API_KEY`, `litellm`, `openai` (it must, to instruct the analyze LM). The scan instructions target the **artifacts under review** (spec.md / plan.md / tasks.md of the feature being analyzed), not the constitution document that supplies the instruction — the constitution's own mention must not be read as a violation. This boundary is stated explicitly so the analyze LM does not flag the constitution against itself.
- **`constitution.md` is not literally empty** — it is the pristine buildkit template (all `[PLACEHOLDER]` tokens). The write overwrites the template in place; it is not an append.
- **Numeral stability under the two sanctioned merges.** II merges into I and VII's commit-clause merges into VIII, but the numerals III / IV / V / VI MUST remain stable so downstream references don't drift. The principle count is frozen **before** writing.
- **Approvals drop below the floor.** Default 8 principles; owner-merge floor 6. If the walkthrough would leave fewer than 6, surface it rather than write.
- **Owner edits a machine-checkable MUST into an unverifiable one.** If an edit removes the literal scan token (e.g. drops `skipSRSW` from III), its gate-ability label must change accordingly (machine-checkable → judgement/advisory) so the label never overstates determinism.

## Requirements *(mandatory)*

### Functional Requirements

**Content of the constitution**

- **FR-001**: The feature MUST populate `.specify/memory/constitution.md` with a FROZEN set of principles — default 8, with an owner-merge floor of 6 — replacing the unfilled template.
- **FR-002**: The frozen set MUST comprise: **I** Spec-First (code is never the source of truth); **II** Bug-Protocol / No-Workarounds; **III** SRSW is an inviolable invariant; **IV-a** Language Authority + **IV-b** Preserve Working Internals; **V** Claude-Only LM / No External API; **VI-a** Additive-Only Idempotent Single-Head Persistence + **VI-b** Single OS-Lock-Guarded PGLite Cluster; **VII** Test-Gated Commit-Scoped Shipping; **VIII** Single Source of Truth & Traceability.
- **FR-003**: Each principle MUST carry (a) a normative MUST/SHOULD statement, (b) an Evidence line citing a re-verified, heading-anchored glpnet artifact (one of `docs/DISCIPLINE.md`, `CLAUDE.md`, a `specs/NNN` doc, an FR number, or a codeconv migration/test), (c) a buildkit analog where one exists, and (d) an explicit gate-ability label of exactly one of: `machine-checkable` | `judgement-gate-able` | `advisory`.
- **FR-004**: Principles III, V, and VI-a MUST word their MUST as an explicit instruction the analyze LM executes:
  - **III**: scan the artifact text under review for the literal token `skipSRSW`; a nonzero count ⇒ CRITICAL.
  - **V**: scan the artifact text under review for `OPENAI_API_KEY` / `litellm` / `openai`; a nonzero count ⇒ CRITICAL.
  - **VI-a**: the single linear migration head is asserted by the test family `test_migration_*_single_head.py` (currently `heads == [0010]`) — **not** by a `versions/` filename count.
- **FR-005**: The scan instructions in FR-004 MUST be scoped to the artifacts under review (the feature's spec/plan/tasks), and MUST NOT cause the constitution's own mention of those literal tokens to be flagged.
- **FR-006**: Principle VII MUST be labelled **advisory** at the analyze layer. Principle VIII's roadmap-linkage clause MUST be kept **advisory** (the out-of-scope 027 reconcile / slug-drift situation MUST NOT be retroactively flagged).
- **FR-007**: The numerals **III / IV / V / VI MUST remain stable** even though the two sanctioned merges apply (II merged into I; VII's commit-clause merged into VIII). The principle count MUST be frozen before any content is written. (Clarification: the default proposed set is 8 distinct numerals per FR-002; the two sanctioned merges are *owner options offered at the walkthrough* that reduce the count toward the owner-merge floor of 6 — they are not pre-applied, and applying them never renumbers III / IV / V / VI.)
- **FR-008**: The `Version` MUST be a semantic version (e.g. `1.0.0`), **not** a CalVer tag, and MUST be stamped together with `Ratified` and `Last Amended` dates.
- **FR-009**: The constitution MUST reference `docs/DISCIPLINE.md`, `CLAUDE.md`, and the relevant `specs/` rather than duplicate their content (single source of truth).
- **FR-010**: The constitution MUST record, explicitly, **why** `docs/DISCIPLINE.md`'s GLP-First section and its FCP-Reference-Architecture section are **not** raised to principles.
- **FR-011**: Every Evidence line that cannot be located on disk (artifact missing or anchor/FR/test not found) MUST be dropped — never fabricated, paraphrased into existence, or guessed.

**Process / behaviour of the feature**

- **FR-012**: The grounding step MUST be a Claude-only (no external API), read-only repo scan — it MUST NOT call OpenAI/litellm or require `OPENAI_API_KEY`, consistent with principle V it is establishing.
- **FR-013**: The feature MUST walk the owner (Gabi) through every principle point-by-point and obtain per-principle approval **before** `constitution.md` is written; no partial or pre-approval write is permitted.
- **FR-014**: The feature MUST NOT auto-invoke any pipeline command (no `/buildkit-plan`, `/buildkit-tasks`, `/buildkit-analyze`, etc., triggered by the feature itself). This prohibits *autonomous pipeline auto-advance*; it does NOT prohibit the explicit, owner-driven `/buildkit-analyze` runs that capture the FR-017 before/after baseline, which are measurements of the deliverable, not the feature advancing its own pipeline stage.
- **FR-015**: The feature MUST NOT modify the `/buildkit-analyze` skill, and MUST NOT implement a grep harness or any external scanning tool — the determinism is honest instruction-level best-effort LM compliance achieved by wording, not by code.
- **FR-016**: The feature MUST validate the machine-checkable principles with a concrete negative control: a planted `skipSRSW` fragment MUST be flagged CRITICAL via principle III, and a planted `OPENAI_API_KEY` fragment MUST be flagged CRITICAL via principle V (demonstration captured as evidence under `specs/028-evidence-based-constitution/evidence/` (e.g. `negative-control.md`), not added as a permanent harness).
- **FR-017**: The feature MUST capture a before/after `/buildkit-analyze` Constitution-Check baseline on feature **026 or 027** (before = vacuous/no-MUST pass against the template; after = MUSTs extracted and reasoned about), saved under `specs/028-evidence-based-constitution/evidence/` (e.g. `analyze-before.md`, `analyze-after.md`).
- **FR-018**: The feature is governance-documentation **only**: it MUST touch no GLP runtime code, no `.glp` source, and MUST NOT extend, revise, or add to the GLP language definition.

### Key Entities *(include if feature involves data)*

- **Constitution document** (`.specify/memory/constitution.md`): the single frozen governance artifact. Attributes: ordered principles (I–VIII with sub-letters), per-principle normative statement, Evidence line, buildkit analog, gate-ability label; a Governance section; semantic `Version` + `Ratified` + `Last Amended` dates; an explicit non-elevation note for GLP-First and FCP-Reference-Architecture.
- **Principle**: one governance rule. Attributes: numeral/sub-letter, MUST/SHOULD statement, gate-ability label (`machine-checkable` | `judgement-gate-able` | `advisory`), Evidence anchor (file + heading/FR/test), optional buildkit analog.
- **Evidence anchor**: a re-verified pointer into a glpnet artifact on disk (file path + heading / FR number / test name). Dropped if it does not resolve.
- **Negative-control fragment**: a transient demonstration input (`skipSRSW`; `OPENAI_API_KEY`) used once to prove III/V fire CRITICAL; not persisted as a harness.
- **Analyze baseline pair**: the before/after Constitution-Check transcripts on feature 026/027 that evidence the cosmetic→real transition.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After this feature, `/buildkit-analyze` on feature 026 or 027 extracts **≥ 6** principle MUST statements from the constitution, where before it extracted **0** (before/after pair captured).
- **SC-002**: A planted `skipSRSW` fragment in an artifact under review is reported as **CRITICAL** by the Constitution Check (principle III); a planted `OPENAI_API_KEY` fragment is reported as **CRITICAL** (principle V) — both demonstrated.
- **SC-003**: **100%** of Evidence lines in the written constitution resolve to an existing on-disk artifact + anchor; **0** fabricated or unresolved citations remain.
- **SC-004**: The written file contains exactly the frozen principle set (6–8 after owner walkthrough), with numerals III / IV / V / VI unchanged from the proposed numbering, a semantic `Version`, and `Ratified` + `Last Amended` dates.
- **SC-005**: The constitution's own occurrences of `skipSRSW` / `OPENAI_API_KEY` / `litellm` / `openai` do **not** cause the Constitution Check to flag the constitution against itself.
- **SC-006**: No GLP runtime file, `.glp` source, or GLP language-definition artifact is modified; the `/buildkit-analyze` skill is unmodified; no grep harness is added (diff confined to the constitution file plus this feature's spec artifacts and captured-evidence notes under `specs/028-evidence-based-constitution/evidence/`).
- **SC-007**: No pipeline command is auto-invoked by the feature, and no write to `constitution.md` occurs before the owner has approved every principle.

## Assumptions

- "Empty" in the brief means the pristine, unfilled buildkit template (all `[PLACEHOLDER]` tokens); the file currently exists at ~2.4 KB. The write overwrites that template.
- The owner chooses whether the before/after analyze baseline is captured on feature 026 or feature 027; either satisfies FR-017.
- The negative-control demonstration (FR-016) is a one-time validation captured as evidence under this feature's artifacts; it is not committed as a recurring test, consistent with "not a grep harness."
- `/buildkit-analyze` is an LM reviewer that loads `.specify/memory/constitution.md`, extracts each MUST, and treats conflicts as automatically CRITICAL; the determinism of III/V/VI-a is therefore best-effort LM instruction-following, not a guaranteed deterministic check.
- The two sanctioned merges (II→I, VII-commit-clause→VIII) are content merges; the displayed numeral sequence is preserved per FR-007 so existing references stay valid.
- The gate-ability taxonomy is exactly three values — `machine-checkable`, `judgement-gate-able`, `advisory` — and each principle is labelled with exactly one.
- This feature reuses the existing `.specify/` buildkit layout and the existing constitution template structure; it adds no new tooling.
