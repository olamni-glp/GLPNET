# Phase 1 Data Model: Engine Review + Refactoring Design Dossier

**Feature**: `026-engine-review-dossier` | **Date**: 2026-06-09

This feature has no runtime data model (it ships no code). The "entities" below are the **content structures** of the dossier — the shapes every section and roadmap candidate must conform to. They are drawn from the spec's Key Entities and turned into validatable field sets so the dossier can be checked for completeness.

---

## Entity: Design Dossier

The authoritative output document.

| Field | Required | Description |
|---|---|---|
| `title` / `epic` / `date` | yes | Identifies the dossier and the epic it serves |
| `source_inputs[]` | yes | Read-only inputs cited (investigation, requirements, feature-definition, llvm-feasibility, research-programme, code trees) |
| `design_areas[]` | yes | The 7 named areas, each a **Decision Point** (see below) |
| `premise_reconciliations[]` | yes | ≥2 (compiler location; runtime-IL generation) |
| `open_question_sets[]` | yes | One **Open-Question Option Set** per step-1 open question |
| `feature_breakdown[]` | yes | Ordered list of **Successor-Feature Entry** (topologically valid) |
| `risk_register[]` | yes | Top risks, each with a mitigation (FR-017) |

**Validation**: must satisfy SC-001 (7/7 areas), SC-002 (2/2 premises), SC-003 (100% open questions as options), SC-004 (breakdown well-formed), SC-007 (MVP deps explicit), SC-008 (every area tagged + cited).

## Entity: Decision Point  *(one per design area)*

| Field | Required | Description |
|---|---|---|
| `area` | yes | One of: seam contract; wire shapes; control-program/client; liveness/crash/restart; persistent-vs-ephemeral (+DB-abstraction+bootstrap+resume); mailbox; MVP slice |
| `classification` | yes | `reuse` \| `refactor` \| `net-new` (FR-014) |
| `code_locations[]` | yes (≥1) | `file:line` citations (SC-008) |
| `resolution` | yes | Either a **forced design** (no genuine alternative, stated as such) **or** an **options set** (see Open-Question Option Set) |
| `net_new_flag` | conditional | If the area depends on a capability absent from code today, flagged net-new with the substrate to reuse/build (US1 scenario 3) |

**State transition**: `drafted → evidence-verified → owner-decided` (the last only at the approval gate; the dossier never pre-sets it to decided).

## Entity: Premise Reconciliation  *(≥2)*

| Field | Required | Description |
|---|---|---|
| `premise` | yes | The original requirement assumption |
| `as_built_reality` | yes | What the code actually does, **with `file:line`** |
| `reconciling_option(s)` | yes | The decision(s) available with consequences (owner decides where a genuine choice exists) |
| `downstream_consequence` | yes | Which successor features the decision splits/affects |

Mandated instances: (1) compiler location (parser-in-front-end / compiled-IL-on-wire vs engine-internal compiler); (2) runtime-IL generation (no bytecode synthesised at runtime; runtime goal-term assembly + dispatch). Validates SC-002, US2.

## Entity: Open-Question Option Set  *(one per step-1 open question)*

| Field | Required | Description |
|---|---|---|
| `question` | yes | The previously-open fork |
| `options[]` | yes (2–5) | Mutually-exclusive options |
| `options[].consequences` | yes | What each option entails |
| `options[].trade_off` | yes | The cost stated concisely (≤ a few lines) |
| `options[].evidence` | yes | `file:line` and/or named prior art (FR-018) |
| `recommendation` | optional | Marked **advisory**; decision left to owner (FR-011) |
| `settled` | always `false` | The dossier never records the fork as settled |

Source open questions (from `investigation.md` §8.3): compiler-location; output streaming vs terminal envelope; encoding of unbound/MutualRef/ModuleTerm bindings + suspended-goal/blocking-reader round-trip; var-name→writer identity scheme; which DB underlies the store + what is "full current state"; snapshot granularity + consistency point; where the snapshot/resume driver lives (FR-057); store-as-source-of-truth-for-code vs reload-`.glp`; in-flight-request loss vs replay. Validates SC-003, US3.

## Entity: Successor-Feature Entry  *(one per breakdown item, ~16)*

| Field | Required | Description |
|---|---|---|
| `name` | yes | Feature slug |
| `kind` | yes | `prep` \| `experiment` \| `mvp` \| `follow-up` |
| `scope` | yes | One-line scope |
| `why` | yes | Rationale |
| `depends_on[]` | yes | Set of earlier entries (no forward dependency) |
| `dossier_section_ref` | yes | The motivating dossier section (FR-013) |

**Validation**: topological order — no entry depends on a later entry (SC-004); 100% carry all five fields + a section ref; MVP entries enumerate net-new deps and explicit defers (SC-007).

## Entity: Roadmap Candidate  *(post-approval projection of a Successor-Feature Entry)*

The seeded form (FR-019). Created **only after owner approval**. One per successor 2–16.

| Field | Required | Description |
|---|---|---|
| `kind` | yes | prep/experiment/mvp/follow-up |
| `scope` | yes | from the entry |
| `why` | yes | from the entry |
| `depends_on[]` | yes | from the entry |
| `state` | yes | `candidate` only — never `specified`/`planned`/`implemented` (SC-010) |

See `contracts/roadmap-candidate.md` for the seeding contract.

## Entity: Source Inputs (read-only)

`investigation.md`, `requirements.md`, `feature-definition.md` (+ `llvm-feasibility.md`, `research-programme.md`), plus the C# reference (`out/csharp`), feature-025 link layer (`csharp/glp_link`), durable store (`codeconv/.../marathon`), and Dart cross-check (`glp_runtime`). Never modified (FR-015).
