# Data Model — `/D2NET-scaffold` Skill Wrapper

**Feature**: `010-scaffold-skill` — see [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

The feature introduces no persistent data structures. All "entities" below are either filesystem assets (the SKILL.md file) or in-memory abstractions Claude maintains during a single skill invocation.

## File-system entities

### `.claude/skills/D2NET-scaffold/SKILL.md`

The single tracked artifact. Markdown with YAML frontmatter.

**Frontmatter shape** (matches `/D2NET-init` (006) and the spec-kit convention; identical key set to the existing `.claude/skills/D2NET-init/SKILL.md` and `.claude/skills/speckit-*` skills):

| Key                         | Type    | Required | Value for this skill                                                                                                                                                                                       |
|-----------------------------|---------|----------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `name`                      | string  | yes      | `"D2NET-scaffold"`                                                                                                                                                                                         |
| `description`               | string  | yes      | One-sentence summary. See research.md R1 for the agreed text.                                                                                                                                              |
| `argument-hint`             | string  | yes      | Short hint shown in the slash-command picker. Notes that empty = run scaffold (NOT help).                                                                                                                  |
| `compatibility`             | string  | yes      | Runtime / repository requirements. Mentions Node.js >= 20, populated `.D2NET/` workspace, .NET 8 SDK or runtime.                                                                                            |
| `metadata.author`           | string  | yes      | `"GLPNET"`.                                                                                                                                                                                                |
| `metadata.source`           | string  | yes      | `"specs/010-scaffold-skill/spec.md"`.                                                                                                                                                                      |
| `user-invocable`            | boolean | yes      | `true` — required so `/D2NET-scaffold` is bindable as a slash command.                                                                                                                                      |
| `disable-model-invocation`  | boolean | yes      | `false` — the model may invoke this skill autonomously when relevant; safe because the skill always gates destructive ops (FR-012) and `dotnet build` (FR-006) on user confirmation, AND the binary's hard-safety-gate prompt fires every destructive run. |

**Body shape**: numbered procedure with named sections. Section list (in order):

1. **User Input** — boilerplate `$ARGUMENTS` block. Identical pattern to sibling skills.
2. **Goal** — short statement (one paragraph) of what the skill does.
3. **Operating Constraints** — the inviolable constraints (FR-006 build-confirmation, FR-014 stdin-drive, FR-015 destructive-flag-pair forbidden without confirmation, "never walk up to find `.git/`").
4. **Procedure** — numbered Step 1 … Step N, mapping 1:1 to the procedural contract in `contracts/skill-contract.md`.
5. **Examples** — concrete invocations (default scaffold, JSON, force delete, help, version, pass-through) with expected resolved flag sets.

## In-memory entities (per invocation)

### Argument bundle

The translated set of CLI flags Claude derived from the user's input. Conceptual fields:

| Field                | Type           | Source                                                                                                                                              |
|----------------------|----------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `mode`               | enum           | `scaffold` (default) / `help` / `version` — derived from verbs, raw flags, or empty input.                                                           |
| `json`               | bool           | True from `--json` literal OR translated natural-language ("in json" / "as json" / "give me json" / "structured"). Used by FR-018 for output-format detection AND by FR-017 for recap suppression (Q1). |
| `force`              | bool           | True only after FR-012 destructive confirmation OR if the user supplied `--FORCE` literally as one half of the flag pair.                           |
| `delete-target`      | bool           | True only after FR-012 destructive confirmation OR if the user supplied `--DELETE-TARGET` literally as one half of the flag pair.                   |
| `bridge-port`        | int?           | From `--bridge-port N`, `bridge-port=N`, or "(on) bridge port N" natural language. Defaults to omitted; binary uses its persisted default.          |
| `unrecognized-tokens`| list of string | Tokens from the user's natural-language input that matched no verb / no marker / no flag. If non-empty AND `mode == scaffold`, FR-010a routes the resolved mode to `help` instead. |

**DEVIATION FROM 006's argument bundle**: No `source` / `target-extension` / `target` / `exclude[]` / `accept-suggested` / `non-interactive` fields. Those are init-only; scaffold reads them from the workspace, not from user input.

The bundle is internal; it is not persisted anywhere on disk.

### Destructive-confirmation cache

A conversation-scoped set of **target-directory absolute paths** (clarified Q2). Per FR-013, when the user confirms a destructive operation against a specific target tree, that target's absolute path is added to the cache. Subsequent invocations in the same conversation that resolve to the same target absolute path skip the FR-012 skill-layer prompt — but ALWAYS drive the binary's own interactive prompt (the binary re-prompts every invocation by design; FR-014).

The cache is empty at the start of every fresh Claude Code session and is never written to disk (R4).

**Implementation note for the SKILL.md author**: Claude maintains this state by writing a structured marker into its own response history — `[D2NET-scaffold: destructive-confirmed = <abs target path> @ <ISO timestamp>]` — and reading those markers from the conversation transcript on subsequent invocations.

**Cache key derivation**: To compute the resolved target absolute path for cache lookup at invocation time, the skill MUST read `<cwd>/.D2NET/D2NET-Settings.json`, parse the `target` field, and resolve it against `<cwd>` (the repo root). This produces an OS-native absolute path matching the format the cached marker was written with.

**Post-compaction degradation**: Same as 006's cache. Auto-compaction may drop the structured marker; the skill MUST NOT rely on the cache surviving compaction. If the marker is absent on a subsequent invocation against an already-confirmed target, the skill MUST re-prompt. Re-prompting is the safe failure mode.

**DEVIATION FROM 006**: The cache key is the **target directory absolute path** (where deletion happens), not the workspace `.D2NET/` path. This protects the operator if `/D2NET-init` is re-run between scaffold invocations to retarget; the new target re-prompts because its absolute path differs from the cached value.

### Resolved-flag set

The actual list of CLI tokens passed to the binary. Derived from the argument bundle:

```
[<binary path>]
[--FORCE --DELETE-TARGET]   if force AND delete-target
[--bridge-port <N>]         if bridge-port set
[--json]                    if json
[--help | --version]        if mode = help/version (and these are mutually exclusive with all other flags per binary's ArgParser)
```

The resolved-flag set is what gets passed to the Bash tool's command. For the destructive case, the actual command line is:

```
echo yes | <binary path> --FORCE --DELETE-TARGET [--bridge-port N] [--json]
```

(or the PowerShell equivalent on Windows: `'yes' | <binary path> ...`)

## Validation artifact

### `specs/010-scaffold-skill/validation.md`

Generated during `/speckit-implement` Phase 6 (smoke walkthrough). Records the outcomes against this repo's `glp_runtime_net/` target tree after `/D2NET-init` has populated the workspace. Format:

```markdown
# Validation — /D2NET-scaffold smoke walkthrough

**Date**: <ISO timestamp>
**Repo state**: <git rev-parse HEAD>
**Binary version**: <output of d2net-scaffold --version>
**Workspace**: <output of d2net-init --list summary>

## Test cases

| # | Input                                                         | Expected resolved flags                                                                                                                                       | Result |
|---|---------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------|--------|
| 1 | `/D2NET-scaffold help`                                        | `--help`                                                                                                                                                      | PASS   |
| 2 | `/D2NET-scaffold version`                                     | `--version`                                                                                                                                                   | PASS   |
| 3 | `/D2NET-scaffold` (empty)                                     | (no flags; binary runs in default scaffold mode)                                                                                                              | PASS   |
| 4 | `/D2NET-scaffold as json`                                     | `--json`; recap suppressed in skill response                                                                                                                  | PASS   |
| 5 | `/D2NET-scaffold --json --bridge-port 55001`                  | `--json --bridge-port 55001` (pass-through)                                                                                                                   | PASS   |
| 6 | `/D2NET-scaffold force delete target` (against unmanaged target) | After skill-layer confirm + binary stdin-drive: `--FORCE --DELETE-TARGET` AND `yes\n` piped in                                                                  | PASS   |
| 7 | `/D2NET-scaffold` (no `.D2NET/` present)                      | Binary exits 22 (`ScaffoldWorkspaceMissing`); skill surfaces hint "Run /D2NET-init first"                                                                       | PASS   |
| 8 | `/D2NET-scaffold foo` (unrecognized non-empty)                | `--help` (per FR-010a)                                                                                                                                        | PASS   |
| 9 | `/D2NET-scaffold` against existing target NOT scaffold-managed | Binary exits 24 (`ScaffoldTargetNotEmptyAndNotManaged`); skill surfaces destructive-override hint                                                              | PASS   |
| 10| `/D2NET-scaffold force delete target` (operator types `no` at skill prompt) | Zero binary invocations; skill stops cleanly                                                                                                                  | PASS   |

## Notes

<freeform observations: bridge port persistence, idempotency observed on re-runs, exact recap format, etc.>
```

The exact case list is generated at validation time; the table above is the seed.

## Comparison with `/D2NET-init` data model (spec 006)

Both skills share the SKILL.md file shape, the destructive-cache concept, and the validation-artifact pattern. Key differences:

| Aspect | `/D2NET-init` (spec 006) | `/D2NET-scaffold` (spec 010) |
|--------|---------------------------|-------------------------------|
| Argument-bundle field count | 10 (mode + 9 flags / values) | 6 (mode + 5 flags / values) |
| Single-token shortcut | YES (token = source dir) | NO (no positional) |
| Key-value translation | YES (`source=`, `extension=`, `target=`) | NO |
| Destructive cache key | Workspace `.D2NET/` absolute path | Target directory absolute path |
| Empty input | Routes to `--help` | Routes to default scaffold mode |
| Unrecognized non-empty input | (not specified — implicit help fallback) | Routes to `--help` (FR-010a, clarified Q5) |
| Stdin drive | Not needed (`--non-interactive` flag) | Required for `--FORCE --DELETE-TARGET` (FR-014) |
| Recap on `--json` | Appended | **Suppressed** (clarified Q1) |
| Bridge-port auto-retry | YES (3-attempt walk-forward) | NO (operator's responsibility) |

These differences are deliberate and stem from the underlying binary's CLI surface, not from arbitrary divergence.
