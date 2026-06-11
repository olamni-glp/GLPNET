# GLPnet Constitution

This constitution is the **frozen, non-negotiable** governance authority for glpnet. It exists so the `/buildkit-analyze` Constitution Check is a real gate: each principle below carries a normative MUST/SHOULD, an Evidence anchor verified on disk, a buildkit analog where one exists, and an explicit gate-ability label — exactly one of `machine-checkable` | `judgement-gate-able` | `advisory`. It **references** the authoritative sources (`docs/DISCIPLINE.md`, `CLAUDE.md`, `specs/`) rather than duplicating them (single source of truth).

**How the analyze LM uses this file**: extract each principle's MUST; compare the artifacts under review (the feature's `spec.md` / `plan.md` / `tasks.md`) against it; a conflict with a MUST is CRITICAL. Where a principle is worded as a scan instruction (III, V, VI-a), execute that scan **against the artifacts under review only** — see the self-mention boundary in Governance.

## Core Principles

### I. Spec-First — Code Is Never the Source of Truth
No implementation — including actor scripts and demo plays — may proceed without an identified, quoted, and consistency-checked spec. When code and spec conflict, the spec wins and is fixed first; code is never the authority over the spec. The Bug-Protocol (no workarounds; STOP and report) is part of this principle.
- **Evidence**: `docs/DISCIPLINE.md` § 1.1 Specification-First Development; `CLAUDE.md` "Spec-First Development — No Implementation Without Spec".
- **buildkit analog**: the specify → clarify → plan → tasks → analyze pipeline ordering.
- **Gate-ability**: judgement-gate-able.

### II. Bug-Protocol / No-Workarounds
On a discovered bug, STOP and report (expected vs actual vs where; check the spec) before any fix. Forbidden: try/catch or null-check "robustness" that masks a caller's bug; race/interleaving workarounds without a protocol spec. "Robustness" is often a workaround in disguise — fix the caller, not the symptom.
- **Evidence**: `docs/DISCIPLINE.md` § 1.2 No Workarounds, § 1.8 Bug Handling: Never Bypass, Always Report.
- **buildkit analog**: none.
- **Gate-ability**: judgement-gate-able.

### III. SRSW Is an Inviolable Invariant
Single-Reader / Single-Writer is mandatory: each variable occurs at most once per clause; no `skipSRSW` escape may ever be proposed or used. **Scan instruction**: in the artifacts under review, a nonzero count of the literal token `skipSRSW` ⇒ CRITICAL.
- **Evidence**: `CLAUDE.md` GLP Quick Reference — "SRSW … **Mandatory** — never invent or use a `skipSRSW` option".
- **buildkit analog**: none.
- **Gate-ability**: machine-checkable.

### IV-a. Language Authority
The GLP language definition — guards, system predicates, body kernels, directives, type-system features, primitive types — may not be revised, extended, or added to without explicit owner approval. Propose first, wait for approval, then implement.
- **Evidence**: `docs/DISCIPLINE.md` § 1.14 Language Design Authority; `CLAUDE.md` "Language Authority".
- **buildkit analog**: none.
- **Gate-ability**: judgement-gate-able.

### IV-b. Preserve Working Internals
Load-bearing internals must never be removed without explicit approval: `_ClauseVar`, `_TentativeStruct`, fallback / edge-condition branches, or any code not fully understood. The implementation may differ from textbook WAM — respect existing patterns.
- **Evidence**: `CLAUDE.md` "Preserve Working Code".
- **buildkit analog**: none.
- **Gate-ability**: judgement-gate-able.

### V. Claude-Only LM / No External API
All LM-in-the-loop work (generation, proposal, LLM-assisted verification) runs in Claude via Agent-tool seams / MCP — never OpenAI, litellm, or `OPENAI_API_KEY`. Any "needs an external API" requirement is a defect to delete, not a constraint to satisfy. **Scan instruction**: in the artifacts under review, a nonzero count of `OPENAI_API_KEY` / `litellm` / `openai` on any LM path ⇒ CRITICAL.
- **Evidence**: `specs/027-refinement-verification-framework/spec.md` FR-012 (and SC-003).
- **buildkit analog**: none.
- **Gate-ability**: machine-checkable.

### VI-a. Additive-Only, Idempotent, Single-Head Persistence
Database migrations are additive and idempotent; prior heads are never destructively rewritten. The single linear migration head is asserted by the test family, not by counting files. **Scan instruction**: the single linear migration head is asserted by `test_migration_*_single_head.py` (currently `heads == [0010]`) — **not** by a `versions/` filename count.
- **Evidence**: `codeconv/tests/test_migration_*_single_head.py` (incl. `test_migration_0010_single_head.py`); current head `codeconv/src/codeconv/db/migrations/versions/0010_marathon_schema.py`.
- **buildkit analog**: Alembic single-head discipline.
- **Gate-ability**: machine-checkable.

### VI-b. Single OS-Lock-Guarded PGLite Cluster
There is exactly one PGLite deployment per repo at `<repo>/.pgdb/`, guarded by an OS-level cross-process lock at the sibling path `<repo>/.pgdb.bridge.lock/`. Every consumer auto-spawns or discovers the shared bridge; no second cluster is created.
- **Evidence**: `specs/012-codeconv-runner/contracts/bridge_lifecycle.md`; `CLAUDE.md` "Migration to unified bridge".
- **buildkit analog**: none.
- **Gate-ability**: judgement-gate-able.

### VII. Test-Gated, Commit-Scoped Shipping
Baseline green before a change; re-test after. Commit only the files you worked on (never `git add -A`); never revert, reset, or undo others' commits. Ship via the buildkit GitFlow (`feature → develop → release/* → main`); never hand-merge a feature into `main`.
- **Evidence**: `CLAUDE.md` "Test Protocol" + "Git Workflow"; `docs/BRANCHING.md`.
- **buildkit analog**: `buildkit ship` preflight + GitFlow.
- **Gate-ability**: advisory.

### VIII. Single Source of Truth & Traceability
Each subsystem has ONE authoritative spec; other documents reference it, not duplicate it. Work is traceable through roadmap → pipeline → tasks. The commit-scope discipline of Principle VII is part of this traceability. The roadmap-linkage clause is **advisory**: pre-existing slug-drift or out-of-scope reconcile situations are not retroactively flagged.
- **Evidence**: `docs/DISCIPLINE.md` § 1.4 Traceability; `CLAUDE.md` "Single source of truth".
- **buildkit analog**: roadmap → pipeline → tasks traceability.
- **Gate-ability**: judgement-gate-able (roadmap-linkage clause: advisory).

## Governance

This constitution supersedes ad-hoc practice within its scope. It is FROZEN: amendments require an explicit, owner-approved constitution update (a separate change outside `/buildkit-analyze`), with the `Version` bumped semantically and the `Last Amended` date restamped. `/buildkit-analyze` MUST NOT dilute, reinterpret, or silently ignore a principle; a conflict requires adjusting the spec/plan/tasks under review, not the principle.

**Self-mention boundary (non-negotiable for the analyze LM).** This document necessarily contains the literal tokens `skipSRSW`, `OPENAI_API_KEY`, `litellm`, and `openai` because Principles III and V instruct the LM to scan for them. The scan instructions target the **artifacts under review** (the feature's `spec.md` / `plan.md` / `tasks.md`) — **never** this constitution document, which supplies the instruction. The constitution's own mention of these tokens MUST NOT be read as a violation.

**Non-elevation note.** `docs/DISCIPLINE.md` § 1.12 (GLP-First Implementation Principle) and § 1.13 (FCP Reference Architecture) are deliberately **not** raised to principles. They are implementation-methodology guidance, not gate-able invariants over spec/plan/tasks artifacts; elevating them would inject advisory-only noise into every analyze run without a checkable conformance signal. They remain authoritative as guidance in `docs/DISCIPLINE.md`.

**Reference, don't duplicate.** The authoritative texts for every principle above live in `docs/DISCIPLINE.md`, `CLAUDE.md`, and the cited `specs/`. This file points to them; it does not restate them.

**Version**: 1.0.0 | **Ratified**: 2026-06-10 | **Last Amended**: 2026-06-10
