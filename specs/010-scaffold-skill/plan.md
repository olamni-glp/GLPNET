# Implementation Plan: `/D2NET-scaffold` — Claude Code Skill Wrapper

**Branch**: `010-scaffold-skill` | **Date**: 2026-05-01 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/010-scaffold-skill/spec.md`

## Summary

Ship a single Claude Code skill at `.claude/skills/D2NET-scaffold/SKILL.md` that wraps the `d2net-scaffold` CLI (built by spec 009). When a user types `/D2NET-scaffold <freeform args>` (or empty `/D2NET-scaffold`), Claude follows the SKILL.md procedure to (a) locate the binary in build outputs (Release → Debug → `dotnet run` fallback), (b) detect missing/stale state and offer to `dotnet build` with single confirmation, (c) parse intent (raw flags, JSON / destructive / bridge-port markers, help/version verbs, empty = run scaffold default mode, unrecognized non-empty = run `--help`), (d) confirm at the skill layer before destructive operations AND drive the binary's own interactive `yes/no` prompt with the user's affirmative reply, (e) invoke the binary, (f) surface results — verbatim for JSON (no recap appended), 50-line truncated for plain text — and (g) hint recovery actions for known failure modes (workspace missing, source missing, target not empty, working-dir collision, copy error, DB write fail, lock contention, operator cancellation).

The feature is intentionally small: one new file, no new code paths, no test infrastructure beyond the underlying binary's existing test suite. Validation is a recorded manual smoke walkthrough against this very repo's `glp_runtime/` source after a successful `/D2NET-init`. The skill mirrors the structure of the sibling `/D2NET-init` skill (spec 006) with five well-defined deviations driven by the binary's smaller surface and clarifications Q1–Q5 (recorded in spec.md `## Clarifications`).

## Technical Context

**Language/Version**: Markdown with YAML frontmatter — Claude Code's skill format. No code generation, no compilation step.

**Primary Dependencies**:
- The shipped `d2net-scaffold` binary at `tools/d2net/src/D2Net.Scaffold/bin/<config>/net8.0/d2net-scaffold.exe` (spec 009). Required at run time of the skill.
- `dotnet build` (build-time, on user confirmation only).
- The Bash tool exposed by Claude Code at run time (the skill's only invocation primitive). Stdin redirection support is required for the destructive-confirmation drive (FR-014 — `yes\n` piped to the binary's stdin).
- Node.js >= 20 on PATH (transitive — the binary's PGLite bridge subprocess; not the skill's own concern).
- A populated `.D2NET/` workspace at the current working directory, created by an earlier `/D2NET-init` invocation. Without it, the binary returns `ScaffoldWorkspaceMissing` (22) and the skill surfaces a hint to run `/D2NET-init` first.

**Storage**: None. The skill itself is stateless. The destructive-confirmation cache (FR-013) is conversation-scoped, not persisted, and is keyed by the target directory's absolute path (clarified Q2).

**Testing**:
- Static lint: YAML frontmatter parses, expected sections present.
- Smoke walkthrough: manually invoke `/D2NET-scaffold` with each of the SC-002 inputs against this repo's `glp_runtime/` source after running `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net`; record outcomes in `specs/010-scaffold-skill/validation.md` during `/speckit-implement`.
- Reuses the underlying binary's spec-009 test suite for end-to-end coverage of the binary itself.

**Target Platform**: Any host where Claude Code runs AND the underlying `d2net-scaffold` binary runs. Per spec 005's Q2 / spec 009's inheritance, that means Windows is the release-blocking host; macOS / Linux are best-effort.

**Project Type**: Tooling artifact (one markdown file). No project structure to set up.

**Performance Goals**:
- SC-001: full `/D2NET-scaffold` round-trip in under **70 seconds** wall-clock for a workspace with ≤ 1,000 dart + 5,000 non-dart files (binary-side ceiling 60 s per spec 009 SC-001; skill adds ≤ 10 s overhead).
- The skill itself adds < 1 s of Claude-side processing beyond the binary's own runtime.

**Constraints**:
- The skill MUST NEVER pass `--FORCE --DELETE-TARGET` without explicit confirmation in this session (FR-015).
- The skill MUST NEVER auto-run `dotnet build` without explicit confirmation in this session (FR-006).
- The skill MUST drive the binary's interactive prompt by piping `yes\n` to stdin only AFTER the skill-layer confirmation has been answered affirmatively (FR-014).
- The skill is a tracked file under `.claude/skills/`, committed alongside the binary's source.
- Casing is exactly `D2NET-scaffold` (filesystem path, frontmatter `name`, slash-command invocation). On case-sensitive hosts the user must type the casing exactly.
- The skill MUST suppress its Claude-side recap entirely when `--json` is in the resolved flag set (clarified Q1) so downstream tooling (`jq`, parser-based assertions) consumes the response cleanly.

**Scale/Scope**: One skill file. ~250–300 lines of markdown. One smoke-walkthrough validation document. No code, no tests, no schema.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is unfilled template (placeholders only). No principles ratified. **Gate status: pass (vacuously)** — same disposition as the shipped 002 / 005 / 006 / 007 / 008 / 009 plans.

**Post-Phase-1 re-check**: still vacuous-pass. The Phase 1 design adds one markdown file; no architectural risk to evaluate.

## Project Structure

### Documentation (this feature)

```text
specs/010-scaffold-skill/
├── plan.md                       # This file
├── spec.md                       # Feature specification (5 clarifications resolved)
├── research.md                   # Phase 0 output (R-decisions covering all spec-time deferrals)
├── data-model.md                 # Phase 1 output (skill file shape; argument bundle; destructive cache)
├── quickstart.md                 # Phase 1 output (developer invocation walkthrough)
├── contracts/
│   └── skill-contract.md         # The procedural contract Claude follows when /D2NET-scaffold is invoked
├── checklists/
│   └── requirements.md           # Spec quality checklist (already exists from /speckit-specify)
├── validation.md                 # Smoke-walkthrough record produced during /speckit-implement
└── tasks.md                      # Phase 2 output (/speckit-tasks command - NOT created here)
```

### Source Code (repository root)

```text
.claude/
└── skills/
    ├── speckit-*                                # Existing — UNTOUCHED
    ├── D2NET-init/                              # Existing — UNTOUCHED (spec 006)
    └── D2NET-scaffold/                          # NEW
        └── SKILL.md                             # The skill — frontmatter + procedural body

tools/d2net/                                     # Existing — UNTOUCHED by this feature
└── src/D2Net.Scaffold/...                       # The binary the skill wraps (spec 009)

scripts/                                         # Existing — no additions for this feature
```

**Structure Decision**: One new file under `.claude/skills/D2NET-scaffold/SKILL.md`. No new directories beyond that one folder. No changes to `tools/d2net/` (the binary is unchanged; only its invocation wrapper is added). No changes to `scripts/`. No changes to project files (`.csproj`, `.sln`).

## Complexity Tracking

> No constitution violations. No extra projects. The feature is one markdown file.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| (none)    | (none)     | (none)                               |
