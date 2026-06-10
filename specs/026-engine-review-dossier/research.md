# Phase 0 Research: Engine Review + Refactoring Design Dossier

**Feature**: `026-engine-review-dossier` | **Date**: 2026-06-09

This feature is documentation-only. There are **no `NEEDS CLARIFICATION` markers** in the spec — the three Session-2026-06-09 clarifications resolved decision-authority, option-quality, and roadmap-seeding. Phase-0 "research" here therefore records the *planning-level* decisions (how the dossier is produced, what it cites, how options are framed), not new engineering unknowns. The deep engineering unknowns were already burned down by marathon step 1 (`investigation.md`); resolving them *for the owner* is the dossier's own content, produced during implementation.

---

## Decision 1 — Dossier is a single Markdown document, not a multi-file set

- **Decision**: One authoritative file `docs/research/repl-engine-separation/design-dossier.md`.
- **Rationale**: FR-001 mandates "a single authoritative design dossier document that successor features cite as the source of truth." A single path makes citation unambiguous (FR-013/SC-004: every successor entry cites a dossier section). Section anchors give per-area citations.
- **Alternatives considered**: (a) one file per design area — rejected: fragments the "single source of truth," complicates the FR-013 cite-a-section requirement. (b) Edit `investigation.md` in place — rejected: the spec keeps `investigation.md` as the read-only step-1 review of record and has the dossier *supersede* its design content (spec Assumptions).

## Decision 2 — Output location: alongside source inputs

- **Decision**: `docs/research/repl-engine-separation/` (with the step-1 review, requirements, and feature-definition).
- **Rationale**: Spec Assumptions "Output location" names this dir unless the owner designates otherwise; co-location keeps the read-only inputs and the deliverable together for successor authors.
- **Alternatives considered**: a top-level `docs/design/` — deferred to owner; not chosen absent instruction.

## Decision 3 — Source inputs and re-verification protocol (FR-016)

- **Decision**: Treat `investigation.md` as the design draft to consolidate, but **re-read the cited code** (`out/csharp`, `csharp/glp_link`, `codeconv/.../marathon`, `glp_runtime`, `programs/self.glp`) and record current reality where it diverges. Every claim carried into the dossier carries a `file:line` citation re-checked against current `HEAD`.
- **Rationale**: FR-016 + Edge Case "step-1 review and as-built code disagree": correct stale claims, don't propagate. SC-008 requires every design area to cite ≥1 code location.
- **Alternatives considered**: trust `investigation.md` verbatim — rejected: violates FR-016 and the edge-case rule.

## Decision 4 — Options framing: present-options, owner decides

- **Decision**: Each genuine design/scope fork is rendered as 2–5 mutually-exclusive options, each with consequences + trade-off + `file:line`/prior-art evidence, concisely (a few lines, not narrative). The dossier MAY mark one option **advisory-recommended** but MUST NOT record any fork as settled.
- **Rationale**: Clarification Q1 (present-options), Q2 (fully-researched + concise) → FR-011, FR-018; SC-003, SC-009. Forced designs (no genuine alternative) are stated as such instead of manufacturing fake options.
- **Alternatives considered**: dossier decides each fork itself — explicitly rejected by the owner in clarification.

## Decision 5 — Roadmap seeding is post-approval, candidates-only (FR-019)

- **Decision**: The 16-entry breakdown is authored as **dossier content** during implementation. Seeding into `buildkit-roadmap` (one candidate per successor 2–16, carrying kind/scope/why/depends-on) happens **only after the owner approves the dossier** at the marathon gate. No successor feature is specified/planned/implemented.
- **Rationale**: Clarification Q3 (Option B) → FR-019, SC-010. Keeps the marathon-gate authority with the owner and avoids forward-running the pipeline.
- **Alternatives considered**: (A) author breakdown only, no seeding — rejected by owner (Option B chosen). (C) seed immediately at authoring time — rejected: violates the approval gate.

## Decision 6 — Acceptance is by checklist-against-spec, not executable tests

- **Decision**: Because the feature ships no code (FR-015/SC-006), the acceptance gate is: every Success Criterion SC-001..SC-010 is satisfiable by reading the dossier (and, for SC-010, by querying the roadmap post-approval). The per-story Independent Tests map 1:1 to dossier sections.
- **Rationale**: There is nothing to compile or run; the deliverable's correctness is content completeness + evidence-grounding + topological validity of the breakdown.
- **Alternatives considered**: write a doc-linter — out of scope; the spec's measurable SCs already define the gate.

---

## Net-new vs reuse map (carried from step 1, to be re-verified in the dossier)

| Capability | Status in code today | Substrate to reuse |
|---|---|---|
| IL/bytecode wire codec | **net-new** (zero today) | recursive constant-term sub-encoder may share `PayloadSerializer` tag scheme |
| Result-envelope codec (engine→client) | **net-new** | rides `FrameCodec`/`TcpTransport`; server-side deep-resolve reuses `_ResolveDeepForTrace` |
| OS-liveness / crash-signal / watchdog | **net-new** | `Microsoft.Extensions.Hosting` BackgroundService; sd_notify / Windows Service |
| Engine-state serialization / persistence | **net-new** | `MarathonStore` shape (PGLite-primary + JSON-fallback, monotonic seq) |
| Transport + framing | **reuse as-is** | feature-025 `FrameCodec`/`TcpTransport`/`ILinkTransport` |
| Ground-term value codec | **reuse (partial)** | `PayloadSerializer` (ground-only; throws on unbound — net-new for suspended results) |
| Multi-accept listener | **refactor** (Phase-6 deferred) | `TcpTransport.ListenAsync` one-accept → many |
| Compiler relocation (front-end IL) | **refactor** (large) | Lexer/Parser/TypeChecker/Compiler currently engine-internal |

**Output**: all planning unknowns resolved; no blocking NEEDS CLARIFICATION. Ready for Phase 1.
