# Skill Contract — `.claude/skills/D2NET-init/SKILL.md`

**Feature**: `006-d2net-init-skill` — see [spec.md](../spec.md), [plan.md](../plan.md), [research.md](../research.md), [data-model.md](../data-model.md)

This document is the procedural contract Claude follows when the user invokes `/D2NET-init`. The implementation phase MUST produce a `SKILL.md` whose body is functionally equivalent to this contract — every numbered step here MUST appear in the SKILL.md procedure with at most cosmetic prose differences. Adding extra steps requires a corresponding spec update; removing steps requires re-running `/speckit-clarify`.

## Frontmatter (verbatim contract)

```yaml
---
name: "D2NET-init"
description: "Wrap the d2net-init CLI: locate binary, derive flags from natural-language or pass-through, confirm before destructive operations, run, and surface results."
argument-hint: "Describe the action (e.g. 'init for glp_runtime', 'list', 'force rebuild') or pass --flag args verbatim. Empty input shows --help."
compatibility: "Requires tools/d2net/src/D2Net.Init/ in the repo and a built or buildable d2net-init binary. Node.js >= 20 required at runtime (the binary's PGLite bridge subprocess)."
metadata:
  author: "GLPNET"
  source: "specs/006-d2net-init-skill/spec.md"
user-invocable: true
disable-model-invocation: false
---
```

`name`, `user-invocable`, `disable-model-invocation` MUST be exactly as above. Other fields MAY be reworded for clarity but MUST preserve their semantics.

## Procedure (numbered steps)

### Step 1 — User Input

Echo the standard `## User Input` block with `$ARGUMENTS` placeholder. Match the sibling-skill convention.

### Step 2 — Pre-flight: locate the binary (FR-004 / FR-005)

Search in this exact order, stopping at the first hit:

1. `tools/d2net/src/D2Net.Init/bin/Release/net8.0/d2net-init.exe` (Windows) or `…/d2net-init` (other).
2. `tools/d2net/src/D2Net.Init/bin/Debug/net8.0/d2net-init.exe` (or platform equivalent).
3. Fallback: `dotnet run --project tools/d2net/src/D2Net.Init -- <args>`. Inform the user this is slower and recommend `dotnet build` once.

If neither binary exists AND `dotnet` is not on PATH, emit a single message naming the missing prerequisites and stop without running anything (FR-005). No binary invocation in this case.

### Step 3 — Pre-flight: detect missing or stale (FR-006)

Compute staleness: `mtime(binary)` < `max(mtime(file) for file in tools/d2net/src/D2Net.Init/**/*.cs)` (excluding the `pgbridge/` subtree, which is a runtime asset).

If the binary is missing OR stale, emit ONE confirmation prompt:

> "d2net-init binary is **missing** at `<path>` — build now? (yes/no)"

or

> "d2net-init binary may be **stale** (newest source is `<path>` modified `<time>`; binary built `<time>`) — rebuild now? (yes/no)"

Wait for an affirmative single-word reply (`yes`, `y`, `confirmed`, `proceed`). On affirmative, run `dotnet build tools/d2net/D2Net.sln` via Bash, surface its output, and on success continue with the original `/D2NET-init` request. On any non-affirmative reply, stop without invoking the binary.

Stale-only (not missing) confirmation MAY be skipped for the rest of this conversation if the user replies with a phrase containing "don't ask" / "skip staleness" / "ignore stale".

### Step 4 — Parse user intent (FR-008 / FR-009 / FR-010 / FR-011)

Branch on the user's `$ARGUMENTS`:

- **Empty / `help` / `--help` / `-h`**: invoke binary with `--help` only (Step 8). Skip all subsequent gates.
- **All tokens flag-style** (every token is `--flag` or a value following one): treat as pass-through. Forward verbatim to Step 7.
- **Mixed natural-language + raw flags**: take raw flags verbatim; derive missing flags from the natural-language portion as described below.
- **Pure natural-language**: derive flags via the grammar:
  - **Key-value pairs**: `source=X`, `extension=X`, `target=X`, `bridge-port=N`.
    - Note: there is no `exclude=` natural-language form in v1. Users add exclusions only via the literal `--exclude <path>` flag (repeatable). Mixed inputs (natural-language + raw `--exclude` flags) are supported per FR-010 — the raw flags are preserved verbatim and only the un-supplied flags are derived from natural language.
  - **Verbs** and their resolved-flag mappings:
    - `init` → init mode (apply Step 5 / Step 7 / Step 8 with no extra inspection flag).
    - `list` → `--list`.
    - `exclusions` → `--Exclusions`.
    - `current-phase` (or `current phase`) → `--current-phase`.
    - `help` → `--help` (skips Steps 5–11; surface output and exit).
    - `version` → `--version` (skips Steps 5–11; surface output and exit).
  - **JSON markers**: phrases containing "json" / "as json" / "in json" / "give me json" → add `--json` to the resolved flag set.
  - **Single bare token**: see Step 5.

### Step 5 — Single-token shortcut (FR-009 sub-bullet)

If `$ARGUMENTS` is exactly one token AND that token is not in `{init, list, exclusions, current-phase, help, version}` AND a directory with that exact name exists as a direct subdirectory of the current working directory:

1. Derive `--source <token> --target-extension _net --target <token>_net`.
2. Emit ONE confirmation prompt: "Init with `source=<token>, extension=_net, target=<token>_net`? (yes/no)".
3. On affirmative reply, proceed to Step 6 with the derived flags.
4. On non-affirmative, stop without invoking the binary.

If the single token does not match an existing directory and is not a verb, fall through to the help text branch in Step 4.

### Step 6 — Destructive-operation gate (FR-012 / FR-013 / FR-014)

Detect destructive intent if any of the following match:
- `$ARGUMENTS` (case-insensitive) contains any of the closed marker word list: `force`, `delete`, `rebuild`, `reset`, `recreate`, `reinitialise`, `reinitialize`, `nuke`, `wipe`, `redo`.
- The resolved flag set contains both `--FORCE` and `--DELETE-EXISTING` (literal pair).

If destructive AND the absolute `.D2NET/` path is NOT in the conversation's confirmed-destructive set:

1. Emit ONE confirmation prompt naming the absolute path and the action: "This will delete the existing `<abs path to .D2NET>` workspace and rebuild it from scratch. Proceed? (yes/no)".
2. Wait for an affirmative reply (`yes`, `y`, `confirmed`, `proceed`).
3. On affirmative, add the path to the confirmed-destructive set (R4: write a structured marker line into Claude's response history) AND add `--FORCE --DELETE-EXISTING` to the resolved flag set.
4. On non-affirmative, stop without invoking the binary.

If destructive AND the path IS already in the confirmed-destructive set for this conversation, add `--FORCE --DELETE-EXISTING` without prompting.

If destructive AND the path's confirmation marker has been lost from context (e.g., due to Claude Code auto-compaction summarising earlier turns), the skill MUST treat the cache as empty and re-prompt. Re-prompting is the safe failure mode and SHOULD NOT be papered over with filesystem persistence (see research.md R4).

If not destructive, never add `--FORCE --DELETE-EXISTING` to the resolved flag set, even if the binary's `WorkspaceAlreadyExists` (3) error subsequently suggests it.

### Step 7 — Augment (FR-007)

To the resolved flag set:

- For init mode (no inspection flag set):
  - Always add `--non-interactive` if absent.
  - If neither `--accept-suggested-exclusions` nor any `--exclude` flag is present, add `--accept-suggested-exclusions` AND emit a one-line warning "(no `--exclude` flags supplied; auto-accepting suggested exclusions to avoid `InteractivePromptCancelled`)".

For inspection (`--list` / `--Exclusions` / `--current-phase`) and help / version modes, no augmentation applies. Inspection commands are non-interactive by construction; the binary rejects `--non-interactive` on inspection invocations as a usage error, and `--accept-suggested-exclusions` is irrelevant outside init.

### Step 8 — Invoke

Build the command line. Two forms based on Step 2's discovery result:

- Binary path resolved: `<binary path> <flag1> <flag2> ...`
- Fallback: `dotnet run --project tools/d2net/src/D2Net.Init -- <flag1> <flag2> ...`

For the special-case verbs `help` (resolved to `--help`) and `version` (resolved to `--version`), the resolved flag set MUST contain only the corresponding short-output flag — no `--non-interactive` augmentation, no `--accept-suggested-exclusions`. These verbs short-circuit and skip Steps 5–11; surface the binary's stdout/stderr verbatim and stop.

Invoke via the Bash tool. Capture stdout, stderr, and exit code separately.

### Step 9 — Surface results (FR-015 / FR-017 / FR-018)

Branch on exit code:

- **Exit 0**: success.
  - If `--json` was in the resolved flag set: surface stdout verbatim regardless of size (FR-017).
  - Otherwise (plain text): if stdout is ≤ 50 lines, surface verbatim; else truncate to first 50 lines + footer "... and N more lines (total: M). Reply 'show all' to see everything, or 'filter <substring>' to narrow." Preserve full stdout in conversation history so 'show all' / 'filter' follow-ups don't need re-invocation.
- **Exit non-zero**: surface the binary's stderr verbatim, then the exit code on its own line (FR-018). Add specific hints per Step 11 below.

### Step 10 — Recap (FR-015)

For exit 0 + init mode only, parse the binary's stdout summary block (the lines "workspace ready at … / Source / Target / PGLite data tree / Excluded dirs / Dart files / Created at") and append a one-line Claude-side recap: "Workspace at `<path>`; indexed `<N>` dart files; bridge port `<port>`."

The recap MUST NOT contradict the binary's own summary — it is supplementary.

### Step 11 — Hint (FR-015 / FR-016)

For specific exit codes, add a hint after the surfaced output:

- **Exit 5 (`BridgePortInUse`)**: suggest a specific next port: `(last_attempted_port + 1)` if < 65535, else 54401. Format: "Bridge port `<old>` is in use. Retry with `--bridge-port <new>`? (yes/no)". On affirmative reply, re-invoke from Step 8 with the new port. After 3 consecutive collisions in this invocation chain, stop suggesting and ask the user to specify a port manually.
- **Exit 8 (`DbOpenFailed`) with stderr containing `pglite_init_failed`**: surface the binary's `--FORCE --DELETE-EXISTING` recovery hint verbatim, but DO NOT auto-run it. The user must explicitly use a destructive verb in a follow-up message to trigger the rebuild path (Step 6).
- **Exit 10 (`NodeMissing`)**: surface the binary's "install Node.js LTS" message verbatim. Stop. Do not retry.
- **Exit 11 (`BridgeBundleMissing`)**: surface the binary's "reinstall d2net-init" message verbatim. Stop.
- **Exit 2 (`WrongCwd`)**: remind the user the skill operates against the current working directory; offer to inspect directory contents to diagnose. Do NOT attempt to walk up.
- **Exit 3 (`WorkspaceAlreadyExists`)**: surface the binary's "use --FORCE --DELETE-EXISTING" hint. Do NOT silently retry — the user must use a destructive verb.

For other non-zero exit codes, surface the stderr only; no specific hint.

## Negative-space contract — what the SKILL.md MUST NOT do

- MUST NOT pass `--FORCE --DELETE-EXISTING` to the binary unless the destructive confirmation flow of Step 6 was completed affirmatively in this conversation OR the user supplied the literal flag pair AND completed Step 6.
- MUST NOT run `dotnet build` without an affirmative reply to Step 3's confirmation prompt.
- MUST NOT walk up to find `.git/` or otherwise change the working directory the user invoked from.
- MUST NOT modify `.D2NET/D2NET-Settings.json` directly — only the binary writes settings; the skill is invocation-only.
- MUST NOT run more than one `dotnet build` per skill invocation. After a single build attempt, if the build failed, surface the build's stderr and stop; do not retry.
- MUST NOT invent CLI flags the binary does not support. The full flag list is the union of: every flag in `tools/d2net/src/D2Net.Init/Program.cs`'s ArgParser PLUS the well-known `--help` / `--version` shortcuts.

## Test surface

The skill is a markdown file; there is no programmatic test harness. Validation is the smoke walkthrough recorded in `validation.md` per data-model.md's table.
