# Implementation Plan: `/D2NET-init` — Claude Code Skill Wrapper

**Branch**: `006-d2net-init-skill` | **Date**: 2026-04-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/006-d2net-init-skill/spec.md`

## Summary

Ship a single Claude Code skill at `.claude/skills/D2NET-init/SKILL.md` that wraps the `d2net-init` CLI (built by spec 005). When a user types `/D2NET-init <freeform args>`, Claude follows the SKILL.md procedure to (a) locate the binary in build outputs (Release → Debug → `dotnet run` fallback), (b) detect missing/stale state and offer to `dotnet build` with single confirmation, (c) parse intent (raw flags / key-value / verbs / single-token shortcut), (d) confirm before destructive operations, (e) invoke the binary, (f) surface results — verbatim for JSON, 50-line truncated for plain text — and (g) hint recovery actions for known failure modes.

The feature is intentionally small: one new file, no new code paths, no test infrastructure beyond the underlying binary's existing 89-test suite. Validation is a recorded manual smoke walkthrough against this very repo's `glp_runtime/` source.

## Technical Context

**Language/Version**: Markdown with YAML frontmatter — Claude Code's skill format. No code generation, no compilation step.
**Primary Dependencies**:
- The shipped `d2net-init` binary at `tools/d2net/src/D2Net.Init/bin/<config>/net8.0/d2net-init.exe` (spec 005, v0.2.0). Required at run time of the skill.
- `dotnet build` (build-time, on user confirmation only).
- The Bash tool exposed by Claude Code at run time (the skill's only invocation primitive).
- Node.js >= 20 on PATH (transitive — the binary's PGLite bridge subprocess; not the skill's own concern).

**Storage**: None. The skill itself is stateless. The destructive-confirmation cache (FR-013) is conversation-scoped, not persisted.
**Testing**:
- Static lint: YAML frontmatter parses, expected sections present.
- Smoke walkthrough: manually invoke `/D2NET-init` with each of the SC-002 inputs against this repo's `glp_runtime/` source; record outcomes in `specs/006-d2net-init-skill/validation.md`.
- Reuses the underlying binary's 89 D2Net.Init.Tests for end-to-end coverage of the binary itself.

**Target Platform**: Any host where Claude Code runs AND the underlying `d2net-init` binary runs. Per spec 005's Q2 clarification, that means Windows is the release-blocking host; macOS / Linux are best-effort.
**Project Type**: Tooling artifact (one markdown file). No project structure to set up.
**Performance Goals**:
- SC-001: full `/D2NET-init init` round-trip in under **30 seconds** wall-clock when binary already built (5-20 s typical).
- The skill itself adds < 1 s of overhead beyond the binary's own runtime.

**Constraints**:
- The skill MUST NEVER pass `--FORCE --DELETE-EXISTING` without explicit confirmation in this session (FR-014).
- The skill MUST NEVER auto-run `dotnet build` without explicit confirmation in this session (FR-006).
- The skill is a tracked file under `.claude/skills/`, committed alongside the binary's source.
- Casing is exactly `D2NET-init` (filesystem path, frontmatter `name`, slash-command invocation). On case-sensitive hosts the user must type the casing exactly.

**Scale/Scope**: One skill file. ~250 lines of markdown. One smoke-walkthrough validation document. No code, no tests, no schema.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is unfilled template (placeholders only). No principles ratified. **Gate status: pass (vacuously)** — same disposition as the shipped 002 / 005 plans.

**Post-Phase-1 re-check**: still vacuous-pass. The Phase 1 design adds one markdown file; no architectural risk to evaluate.

## Project Structure

### Documentation (this feature)

```text
specs/006-d2net-init-skill/
├── plan.md                       # This file
├── spec.md                       # Feature specification (already exists, 3 clarifications resolved)
├── research.md                   # Phase 0 output (10 R-decisions)
├── data-model.md                 # Phase 1 output (skill file shape; argument-bundle entity)
├── quickstart.md                 # Phase 1 output (developer invocation walkthrough)
├── contracts/
│   └── skill-contract.md         # The procedural contract Claude follows when /D2NET-init is invoked
├── checklists/
│   └── requirements.md           # Spec quality checklist (already exists)
├── validation.md                 # Smoke-walkthrough record produced during /speckit-implement
└── tasks.md                      # Phase 2 output (/speckit-tasks command - NOT created here)
```

### Source Code (repository root)

```text
.claude/
└── skills/
    ├── speckit-*                                # Existing — UNTOUCHED
    └── D2NET-init/                              # NEW
        └── SKILL.md                             # The skill — frontmatter + procedural body

tools/d2net/                                     # Existing — UNTOUCHED by this feature
└── src/D2Net.Init/...                           # The binary the skill wraps

scripts/                                         # Existing — no additions for this feature
```

**Structure Decision**: One new file under `.claude/skills/D2NET-init/SKILL.md`. No new directories beyond that one folder. No changes to `tools/d2net/` (the binary is unchanged; only its invocation wrapper is added). No changes to `scripts/`. No changes to project files (`.csproj`, `.sln`).

## Complexity Tracking

> No constitution violations. No extra projects. The feature is one markdown file.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| (none)    | (none)     | (none)                               |
