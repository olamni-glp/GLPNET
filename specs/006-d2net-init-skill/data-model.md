# Data Model — `/D2NET-init` Skill Wrapper

**Feature**: `006-d2net-init-skill` — see [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

The feature introduces no persistent data structures. All "entities" below are either filesystem assets (the SKILL.md file) or in-memory abstractions Claude maintains during a single skill invocation.

## File-system entities

### `.claude/skills/D2NET-init/SKILL.md`

The single tracked artifact. Markdown with YAML frontmatter.

**Frontmatter shape** (matches spec-kit convention; identical key set to the existing `.claude/skills/speckit-*` skills):

| Key                         | Type    | Required | Value for this skill                                                                                                                                                                          |
|-----------------------------|---------|----------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `name`                      | string  | yes      | `"D2NET-init"`                                                                                                                                                                                 |
| `description`               | string  | yes      | One-sentence summary of what the skill does. Drives the slash-command listing.                                                                                                                 |
| `argument-hint`             | string  | yes      | Short hint shown in the slash-command picker.                                                                                                                                                  |
| `compatibility`             | string  | yes      | Runtime / repository requirements. Mentions Node.js >= 20 (transitive from spec 005).                                                                                                          |
| `metadata.author`           | string  | yes      | `"GLPNET"` (or repo-specific equivalent).                                                                                                                                                      |
| `metadata.source`           | string  | yes      | `"specs/006-d2net-init-skill/spec.md"` — points downstream maintainers at this spec.                                                                                                           |
| `user-invocable`            | boolean | yes      | `true` — required so `/D2NET-init` is bindable as a slash command.                                                                                                                              |
| `disable-model-invocation`  | boolean | yes      | `false` — the model may invoke this skill autonomously when relevant; safe because the skill always gates destructive ops (FR-012) and `dotnet build` (FR-006) on user confirmation.            |

**Body shape**: numbered procedure with named sections. Section list (in order):

1. **User Input** — boilerplate `$ARGUMENTS` block. Identical pattern to sibling skills.
2. **Pre-flight: locate the binary** — implements FR-004.
3. **Pre-flight: detect missing or stale binary** — implements FR-006 (single-confirmation prompt; affirmative reply runs `dotnet build`).
4. **Parse user intent** — implements FR-008 / FR-009 / FR-010 / FR-011 (raw-flag pass-through, key-value, verbs, single-token shortcut, empty/help).
5. **Single-token shortcut** — implements FR-009 sub-bullet (existing-subdirectory check, derived defaults, confirmation prompt).
6. **Destructive-operation gate** — implements FR-012 / FR-013 / FR-014 (closed marker word list + literal flag detection, conversation-scoped confirmation cache).
7. **Augment** — adds `--non-interactive` always (FR-007); adds `--accept-suggested-exclusions` when the user did not supply `--exclude` and would otherwise hit `InteractivePromptCancelled`.
8. **Invoke** the binary via Bash tool with the resolved argument list.
9. **Surface results** — implements FR-015 / FR-017 / FR-018 (verbatim for JSON, 50-line truncation for plain text, stderr verbatim on failure).
10. **Recap** — on success-init, append the workspace path / dart-file count / bridge port (parsed from the binary's stdout summary).
11. **Hint** — for `BridgePortInUse` (5), suggest next port (FR-016); for `DbOpenFailed` (8) with `pglite_init_failed`, surface recovery hint without auto-running `--FORCE --DELETE-EXISTING`.

## In-memory entities (per invocation)

### Argument bundle

The translated set of CLI flags Claude derived from the user's input. Conceptual fields:

| Field                | Type           | Source                                                                                                                                              |
|----------------------|----------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `mode`               | enum           | `init` / `list` / `exclusions` / `current-phase` / `help` / `version` — derived from verbs, raw flags, or single-token shortcut.                     |
| `source`             | string?        | From `--source X` literal, `source=X` natural, or single-token shortcut.                                                                            |
| `target-extension`   | string?        | From `--target-extension X`, `extension=X`, or default `_net` for single-token shortcut.                                                            |
| `target`             | string?        | From `--target X`, `target=X`, or default `<source>_net` for single-token shortcut.                                                                  |
| `exclude[]`          | list of string | Repeatable `--exclude X` literal flags only — no natural-language equivalent in the v1 grammar.                                                      |
| `accept-suggested`   | bool           | True if the user passed `--accept-suggested-exclusions` literally OR FR-007 added it because no `--exclude` was supplied.                            |
| `non-interactive`    | bool           | Always `true` after FR-007 augmentation.                                                                                                             |
| `bridge-port`        | int?           | From `--bridge-port N`, `bridge-port=N`, or omitted (binary uses its default 54400, or — on inspection — the persisted port).                        |
| `force`              | bool           | True only after FR-012 destructive confirmation.                                                                                                    |
| `delete-existing`    | bool           | True only after FR-012 destructive confirmation.                                                                                                    |
| `json`               | bool           | True from `--json` literal OR translated natural-language ("in json" / "as json" / "give me json"). Used by FR-017 for output-format detection.       |

The bundle is internal; it is not persisted anywhere on disk.

### Destructive-confirmation cache

A conversation-scoped set of absolute paths. Per FR-013, when the user confirms a destructive operation against `<repo-root>/.D2NET/`, that path is added to the cache. Subsequent invocations in the same conversation that target the same path skip the FR-012 prompt. The cache is empty at the start of every fresh Claude Code session and is never written to disk (R4).

Implementation note for the SKILL.md author: Claude maintains this state by writing a structured marker into its own response history — for example, a single-line note `[D2NET-init: destructive-confirmed = <abs path> @ <ISO timestamp>]` after a successful destructive run — and reading those markers from the conversation transcript on subsequent invocations.

**Post-compaction degradation**: Claude Code's auto-compaction may drop the structured marker from the surviving context. The skill MUST NOT rely on the cache surviving compaction — if the marker is absent on a subsequent invocation against an already-confirmed path, the skill MUST re-prompt. Re-prompting is the safe failure mode (one extra round-trip beats a silent destructive action).

## Validation artifact

### `specs/006-d2net-init-skill/validation.md`

Generated during `/speckit-implement` Phase 6. Records the smoke-walkthrough outcomes against this repo's `glp_runtime/` source. Format:

```markdown
# Validation — /D2NET-init smoke walkthrough

**Date**: <ISO timestamp>
**Repo state**: <git rev-parse HEAD>
**Binary version**: <output of d2net-init --version>

## Test cases

| # | Input                                                                            | Expected resolved flags                                                                                              | Result |
|---|----------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------|--------|
| 1 | `/D2NET-init`                                                                    | `--help`                                                                                                              | PASS   |
| 2 | `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net`           | `--source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive` | PASS   |
| 3 | `/D2NET-init list`                                                               | `--list --non-interactive`                                                                                            | PASS   |
| 4 | `/D2NET-init exclusions in json`                                                 | `--Exclusions --json --non-interactive`                                                                               | PASS   |
| 5 | `/D2NET-init current phase`                                                      | `--current-phase --non-interactive`                                                                                   | PASS   |
| 6 | `/D2NET-init glp_runtime` (single-token shortcut)                                | After confirm: `--source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive` | PASS |
| 7 | `/D2NET-init force rebuild`                                                      | After confirm: prior flags + `--FORCE --DELETE-EXISTING`                                                              | PASS   |
| 8 | `/D2NET-init` re-invocation against existing workspace (no destructive verb)     | Binary exits 3 (`WorkspaceAlreadyExists`); skill surfaces hint without retry                                          | PASS   |

## Notes

<freeform observations>
```

The exact case list is generated at validation time; the table above is the seed.
