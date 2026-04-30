# Feature Specification: `/D2NET-init` — Claude Code Skill Wrapper Around `d2net-init`

**Feature Branch**: `006-d2net-init-skill`
**Created**: 2026-04-30
**Status**: Draft
**Input**: User description: "Create a skill wrapper around D2NET.init as /D2NET-init"

## Background

`d2net-init` is the .NET CLI shipped under `tools/d2net/src/D2Net.Init/` (specs 002 + 005). It creates and inspects a `.D2NET` workspace from the repository root. Today, the only way to invoke it from inside a Claude Code session is to drop to the shell and run `tools/d2net/src/D2Net.Init/bin/Debug/net8.0/d2net-init.exe ...` — clunky, error-prone (path memorisation, flag spelling), and provides no Claude-side post-processing of the result.

This feature ships a Claude Code Skill at `.claude/skills/D2NET-init/SKILL.md` so the user can type `/D2NET-init <freeform args>` from any Claude Code session and Claude will (a) locate the binary, (b) translate the user's intent to CLI flags, (c) confirm before any destructive flag combination, (d) run the binary, and (e) surface the result with the hint messages the binary already emits for known failure modes (Node missing, bridge port in use, etc.). Skill name preserves the user's chosen casing — uppercase `D2NET-init` — to match the brand of the underlying CLI.

## Clarifications

### Session 2026-04-30

- Q: When the binary is missing or stale, should the skill auto-build, warn-only, or offer to build with a confirmation step? → A: Offer-build with single confirmation. The skill detects missing or stale state, prints a one-line confirmation ("binary is missing/stale; build now? (yes/no)"), and on `yes` runs `dotnet build tools/d2net/D2Net.sln` then proceeds with the original request; on `no` it stops. Mirrors the destructive-operation safety pattern (FR-012).
- Q: How should the skill truncate output when the binary emits JSON? → A: JSON-mode invocations bypass truncation entirely. The skill detects `--json` in the resolved flag set (literal flag, or natural-language "in json" / "as json") and surfaces the binary's stdout verbatim regardless of size. Plain-text mode keeps the 50-line truncation rule.
- Q: How should `/D2NET-init <single-bare-token>` be interpreted? → A: When the single token names an existing direct subdirectory of the repo root, the skill treats it as `--source <token>` and derives `--target-extension _net` + `--target <token>_net` as conventional defaults, then prints a single confirmation prompt naming the derived flags before invoking the binary. When the token does not name an existing direct subdirectory (and is not a recognised verb), the skill falls through to the help-text path of FR-011.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Initialise a workspace from inside Claude Code (Priority: P1)

A developer who has just opened a Claude Code session in a repo that needs a `.D2NET` workspace types `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net`. Claude reads the user's intent, derives the equivalent CLI flags (`--source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive`), locates the `d2net-init` binary in the project's build output, runs it, and reports the binary's stdout summary verbatim to the user along with a short Claude-side recap (workspace path, dart-file count, bridge port). No second prompt, no second shell.

**Why this priority**: This is the entire MVP. Without P1, the skill provides no value over directly invoking the shell — and the whole point of the skill is to make the init step a one-line operation inside Claude Code.

**Independent Test**: From a clean Claude Code session in a repo that has `glp_runtime/` as a subdirectory and no `.D2NET/` workspace, type `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net`. Verify (a) Claude locates the d2net-init binary without asking the user where it is, (b) Claude does not ask "what extension?" or "what target?" — those are derivable from the user's input, (c) Claude does NOT ask interactive-prompt questions that the binary would normally ask in a TTY (because `--non-interactive` is supplied automatically), (d) the binary runs once, exits with status 0, and (e) Claude's reply contains the binary's success summary plus the resolved workspace path.

**Acceptance Scenarios**:

1. **Given** a repo root with a `glp_runtime` source directory and no pre-existing `.D2NET` folder, **When** the developer types `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net`, **Then** Claude runs `d2net-init.exe --source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive`, the binary exits with status 0, `.D2NET/` is created with a PGLite data tree, and Claude's reply includes the dart-file count and bridge port.
2. **Given** the same repo, **When** the developer types a more natural-language form like `/D2NET-init initialise the workspace for glp_runtime targeting glp_runtime_net with extension _net`, **Then** Claude derives the same flags and produces the same outcome.
3. **Given** the binary has not yet been built (no `bin/Debug/net8.0/d2net-init.exe`), **When** the developer invokes the skill, **Then** Claude prints a single-confirmation prompt naming the missing binary and the `dotnet build tools/d2net/D2Net.sln` command; on the developer's `yes` reply Claude runs the build, then proceeds with the original `/D2NET-init` request and reports its result. On `no` Claude stops without invoking the binary or starting the build.

---

### User Story 2 - Inspect the workspace from inside Claude Code (Priority: P2)

After User Story 1, the developer types `/D2NET-init list`, `/D2NET-init exclusions`, or `/D2NET-init current-phase` to read back workspace state without leaving Claude. Claude maps each to the corresponding inspection flag (`--list`, `--Exclusions`, `--current-phase`), runs the binary, and surfaces the output. For `list` and `exclusions` against larger source trees, Claude is free to summarise the output (e.g., "237 dart files indexed, here are the first 10 …") rather than dumping every line.

**Why this priority**: The init step delivers MVP; inspection is a frequent follow-up that benefits from the same one-line UX. Not strictly required for the workspace to work, but high-value daily ergonomics.

**Independent Test**: After User Story 1 has run successfully, type `/D2NET-init list`. Verify Claude runs the binary's `--list` form and reports the results. Type `/D2NET-init exclusions --json`. Verify Claude passes the `--json` flag through and the resulting output parses as JSON.

**Acceptance Scenarios**:

1. **Given** an initialised workspace, **When** the developer types `/D2NET-init list`, **Then** Claude runs `d2net-init.exe --list`, surfaces the dart-file inventory, and the binary modifies no file under `.D2NET/`.
2. **Given** an initialised workspace, **When** the developer types `/D2NET-init exclusions --json`, **Then** Claude runs `d2net-init.exe --Exclusions --json` and the output is valid JSON.
3. **Given** an initialised workspace whose `phase_status` table is empty, **When** the developer types `/D2NET-init current-phase`, **Then** Claude reports "no active phase" verbatim from the binary.

---

### User Story 3 - Confirm before destructive operations (Priority: P2)

When the developer's request implies `--FORCE --DELETE-EXISTING` (e.g., `/D2NET-init reinitialise from scratch` or `/D2NET-init force delete and rebuild`), Claude MUST present a single confirmation message naming the destructive action ("This will delete the existing `.D2NET/` workspace and rebuild it from scratch — proceed?") and wait for the user's explicit approval before invoking the binary. Implicit destructive actions, where the user's literal text does not include "force", "delete", "rebuild", "reset", "recreate", or similar markers, MUST NOT trigger the destructive flag combination — Claude either runs the safe form (which the binary will refuse with `WorkspaceAlreadyExists`) or asks the user.

**Why this priority**: Important for safety. P2 because the binary's own `WorkspaceAlreadyExists` refusal is a real safety net even without the skill's confirmation step — but adding the confirmation matches the careful-actions discipline elsewhere in this codebase.

**Independent Test**: With an existing `.D2NET/` workspace, type `/D2NET-init reinitialise from scratch`. Verify Claude (a) detects the destructive intent, (b) prints a single confirmation message naming the affected directory, and (c) does NOT run the binary until the user replies affirmatively. Type `/D2NET-init` with no destructive verbs and confirm Claude does not pass `--FORCE --DELETE-EXISTING` automatically.

**Acceptance Scenarios**:

1. **Given** an existing `.D2NET/` workspace and the developer's input contains a destructive verb (force / delete / rebuild / reset / recreate / reinitialise / nuke), **When** the developer invokes the skill, **Then** Claude prints a confirmation message naming the destructive action and waits for an affirmative reply before running the binary.
2. **Given** an existing `.D2NET/` workspace and the developer's input contains no destructive verb, **When** the developer invokes the skill, **Then** Claude runs the binary without `--FORCE --DELETE-EXISTING`; the binary refuses with `WorkspaceAlreadyExists` (exit code 3); Claude surfaces that error with the binary's hint message.
3. **Given** the developer has confirmed a destructive operation in this session, **When** the binary subsequently exits with status 0, **Then** Claude reports the rebuild summary and notes that the previous workspace was deleted.

---

### Edge Cases

- **Binary not built**: when `bin/Debug/net8.0/d2net-init.exe` (or the platform equivalent) does not exist, Claude prints a single-confirmation prompt ("d2net-init binary is missing at <path>; build now? (yes/no)") and on `yes` runs `dotnet build tools/d2net/D2Net.sln` then proceeds with the original request. On `no` (or any non-affirmative reply) Claude stops without invoking the binary.
- **Binary present but stale**: when the binary exists but a `.cs` file under `tools/d2net/src/D2Net.Init/` has a newer mtime than the binary, Claude prints the same single-confirmation prompt ("d2net-init binary may be stale (source newer than binary); rebuild now? (yes/no)") and proceeds the same way. Stale-binary confirmation MAY be skipped only when the user already explicitly opted out in the current session ("don't ask about staleness again").
- **Binary's `NodeMissing` exit (10)**: Claude surfaces the binary's stderr verbatim ("The PGLite bridge requires Node.js >= 20 on PATH. Install Node.js LTS from https://nodejs.org/ and retry.") and stops.
- **Binary's `BridgePortInUse` exit (5)**: Claude surfaces the error, names the conflicting port, and offers to retry with `--bridge-port <other>`. The user can confirm a specific alternative port or accept Claude's suggestion of the next-higher available port.
- **Binary's `DbOpenFailed` exit (8) with `pglite_init_failed`**: Claude surfaces the binary's recovery hint verbatim (the `--FORCE --DELETE-EXISTING` suggestion) but does NOT auto-run it; the user must explicitly confirm per User Story 3.
- **Binary's `WrongCwd` exit (2)**: Claude surfaces the error and reminds the user that the skill operates against the current working directory; offers to inspect the directory contents to help diagnose.
- **User passes raw flag-style args** (e.g., `/D2NET-init --source glp_runtime --target-extension _net --target glp_runtime_net`): Claude accepts the flag-style input as a pass-through and adds only `--non-interactive` if the user did not already supply it. No translation needed.
- **User mixes natural-language and flag-style** (e.g., `/D2NET-init init for glp_runtime --bridge-port 55000`): Claude derives the missing flags from the natural-language portion and preserves the explicit flags as-is.
- **Output too large**: when `--list` against a 5,000-file source tree returns 5,000 lines, Claude truncates the surfaced output to the first ~50 lines plus a count summary, and offers to pipe to a file or filter. The binary's full output is preserved in the run record so it is not lost.
- **Non-interactive disambiguation absent**: when the user's input is genuinely ambiguous (e.g., `/D2NET-init` with no args at all), Claude invokes the binary's `--help` form and reports the help text, prompting the user to specify their intent.
- **Skill invoked outside a Claude Code session**: not applicable — Claude Code skills are session-scoped by definition.

## Requirements *(mandatory)*

### Functional Requirements

#### Skill registration

- **FR-001**: The skill MUST be invocable as `/D2NET-init` (preserving the user's chosen casing) from any Claude Code session in a repo that contains the skill's `.claude/skills/D2NET-init/` directory.
- **FR-002**: The skill MUST be implemented as a `SKILL.md` markdown file with valid Claude Code skill frontmatter (matching the convention of the existing `.claude/skills/speckit-*` skills): `name`, `description`, `argument-hint`, `compatibility`, `metadata`, `user-invocable: true`, `disable-model-invocation: false`.
- **FR-003**: The skill MUST accept a single freeform argument string (`$ARGUMENTS`) covering both natural-language descriptions of the user's intent AND raw flag-style invocations of the underlying binary. Empty arguments MUST be treated as a request for help.

#### Binary discovery and invocation

- **FR-004**: The skill MUST locate the `d2net-init` binary via the following search order, stopping at the first hit:
  1. `tools/d2net/src/D2Net.Init/bin/Release/net8.0/d2net-init.exe` (or `d2net-init` on non-Windows).
  2. `tools/d2net/src/D2Net.Init/bin/Debug/net8.0/d2net-init.exe`.
  3. The fallback form `dotnet run --project tools/d2net/src/D2Net.Init -- <args>`.

  When the search reaches step 3, the skill MUST inform the user that it is using the slower fallback and recommend running `dotnet build` once.
- **FR-005**: When neither the Release nor the Debug binary exists AND the fallback `dotnet run` would also fail (e.g., `dotnet` not on PATH), the skill MUST report the missing prerequisites clearly with concrete paths and stop without running anything.
- **FR-006**: When the binary is missing OR a `.cs` file under `tools/d2net/src/D2Net.Init/` has a newer mtime than the binary, the skill MUST emit a single confirmation prompt naming the situation ("missing" vs "stale") and the build command (`dotnet build tools/d2net/D2Net.sln`) and MUST wait for an affirmative single-word reply (`yes`, `y`, `confirmed`, `proceed`) before running `dotnet build`. On affirmative reply, the skill MUST run the build, surface its output verbatim, and on success continue with the original `/D2NET-init` request in the same response. On any non-affirmative reply (or no reply within the session), the skill MUST stop and MUST NOT invoke the binary. The skill MUST NEVER run `dotnet build` without an affirmative confirmation in this session, EXCEPT that for the stale-binary case the user MAY opt out for the current session by replying with a phrase such as "don't ask about staleness" — after which the stale-binary confirmation is suppressed and the skill proceeds with the existing binary.
- **FR-007**: The skill MUST always pass `--non-interactive` to the binary, even when the user did not request it explicitly, because Claude Code cannot drive the binary's interactive TTY prompts. If the user omits `--accept-suggested-exclusions` AND does not supply `--exclude` flags AND the binary would otherwise prompt, the skill MUST add `--accept-suggested-exclusions` so the run does not fail with `InteractivePromptCancelled` (exit code 9). The skill MAY warn the user that auto-acceptance was applied.

#### Intent translation

- **FR-008**: When the user's input is parseable as flag-style CLI args (every token is either a `--flag` or a value for the prior flag), the skill MUST treat the input as a pass-through and forward verbatim to the binary, augmenting only as required by FR-007.
- **FR-009**: When the user's input is natural-language, the skill MUST derive flags from the input. The supported parameter shapes are at minimum:
  - **Key-value pairs**: `source=<name>`, `extension=<ext>`, `target=<name>`, `bridge-port=<int>`.
  - **Positional verbs**: `init`, `list`, `exclusions`, `current-phase`, `help`, `version` (mode selection).
  - **Single-bare-token shortcut**: when the user's input is exactly one token AND that token names an existing direct subdirectory of the current working directory AND is not a recognised verb, the skill MUST treat it as `--source <token>` and derive `--target-extension _net` and `--target <token>_net` as conventional defaults. The skill MUST then emit a single confirmation prompt naming the derived flags ("Init with source=<token>, extension=_net, target=<token>_net? (yes/no)") and MUST wait for an affirmative reply before invoking the binary. Single-token inputs that do not match an existing subdirectory and are not verbs fall through to FR-011 (help text).

  The skill MUST document the full grammar in its own SKILL.md so a user reading the skill's `--help` understands what they can ask for.
- **FR-010**: When the user's input is mixed (natural-language prefix + raw flags), the skill MUST take the raw flags verbatim and derive only the un-supplied flags from the natural-language portion.
- **FR-011**: When the user's input is empty OR consists only of `help` / `--help` / `-h`, the skill MUST run the binary's `--help` form and surface the result.

#### Destructive-operation safety

- **FR-012**: When the user's input contains any of the destructive markers `force`, `delete`, `rebuild`, `reset`, `recreate`, `reinitialise`, `reinitialize`, `nuke`, `wipe`, `redo`, OR the explicit flag pair `--FORCE --DELETE-EXISTING`, the skill MUST treat the request as destructive. The skill MUST emit a single confirmation message naming the affected `.D2NET` directory absolute path and the specific destructive action ("delete and rebuild from scratch"), and MUST wait for an affirmative single-word reply (`yes`, `y`, `confirmed`, `proceed`) before invoking the binary with `--FORCE --DELETE-EXISTING`.
- **FR-013**: When the user has already confirmed a destructive operation in the current Claude Code session for the same `.D2NET` directory, the skill MUST NOT prompt again for the same operation in the same session. (Each fresh session re-prompts.)
- **FR-014**: The skill MUST NEVER pass `--FORCE --DELETE-EXISTING` to the binary unless either (a) the user's literal input contained that exact flag pair, OR (b) the destructive-marker confirmation flow of FR-012 was completed affirmatively in this session.

#### Result surfacing

- **FR-015**: After the binary returns, the skill MUST surface the binary's stdout verbatim, then the exit code, then a brief Claude-side recap when applicable: workspace path on success-init, dart-file count, bridge port, the recovery hint on `pglite_init_failed`. The recap MUST NOT contradict or substitute for the binary's own output — it is supplementary.
- **FR-016**: When the binary exits with `BridgePortInUse` (5), the skill's response MUST suggest a concrete alternative port (the next-higher port in the user-supplied range, or `54401`/`54402`/etc. if the default `54400` was in use). The user can accept that suggestion (skill re-runs with the suggested `--bridge-port`) or supply their own.
- **FR-017**: When the binary's stdout is **plain text** (no `--json` flag in the resolved invocation) AND exceeds **50 lines** (e.g., `--list` against a large tree), the skill MUST truncate the surfaced output to the first ~50 lines plus a "... and N more lines (total: N+50). Reply 'show all' to see everything, or 'filter <substring>' to narrow." footer. The full output MUST be preserved in the skill's run record so the user can request it without re-running the binary.

  When the resolved invocation includes `--json` (whether the user supplied it literally or the skill translated it from natural-language phrases such as "in json" / "as json" / "give me json"), the skill MUST surface the binary's stdout **verbatim regardless of line count** to preserve JSON parseability for downstream tooling. The 50-line threshold MUST NOT apply to JSON outputs.
- **FR-018**: When the binary exits with any non-zero status, the skill MUST surface the binary's stderr verbatim, then the exit code. The skill MUST NOT silently swallow errors.

### Key Entities *(include if feature involves data)*

- **Skill (`.claude/skills/D2NET-init/SKILL.md`)**: A markdown file with YAML frontmatter that Claude Code loads when the user types `/D2NET-init <args>`. The body is procedural instructions Claude follows: locate binary, parse intent, confirm if destructive, invoke, surface results.
- **Binary discovery result**: One of three states — Release binary present, Debug binary present, fallback `dotnet run` required (each step in FR-004 maps to one state).
- **Argument bundle**: The translated set of CLI flags Claude derived from the user's input. Internal to the skill; does not persist beyond a single invocation.
- **Destructive-confirmation cache**: A per-session in-memory record of `.D2NET` paths the user has already confirmed for destruction (FR-013). Internal to the skill; does not persist across sessions.
- **All entities from `specs/005-d2net-pglite-bridge/spec.md`** (workspace, settings file, PGLite data tree, bridge subprocess, etc.) are unchanged — the skill is a thin invocation wrapper.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a Claude Code session in a repo where a Release or Debug binary already exists, the user can complete a fresh `D2NET.Init` run end-to-end via a single `/D2NET-init <args>` message in under **30 seconds** wall-clock (typical range 5–20 s, dominated by PGLite WASM cold-init in the bridge subprocess).
- **SC-002**: The skill correctly translates each of the following natural-language inputs into the documented flag set:
  - "init for glp_runtime targeting glp_runtime_net with extension _net" → `--source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive`
  - "list" → `--list --non-interactive` (note: `--non-interactive` is a no-op on inspection but skill always supplies it per FR-007)
  - "exclusions in json" → `--Exclusions --json --non-interactive`
  - "current phase" → `--current-phase --non-interactive`
  - "help" → `--help` (skill short-circuits Steps 5–11; no augmentation)
  - "version" → `--version` (skill short-circuits Steps 5–11; no augmentation)
  - "glp_runtime" (when `glp_runtime/` exists as a direct subdirectory) → after a one-message confirmation, `--source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive`. Without the existing-subdirectory match, the same input routes to `--help` instead.
- **SC-003**: A destructive-marker input (`/D2NET-init force rebuild`) produces zero binary invocations until the user replies `yes` (or equivalent affirmative); a confirmed destructive flow produces exactly one binary invocation with `--FORCE --DELETE-EXISTING` in the args.
- **SC-004**: A non-destructive input against an existing `.D2NET/` workspace produces an invocation that exits with `WorkspaceAlreadyExists` (3); the skill surfaces that error with the binary's "use --FORCE --DELETE-EXISTING" hint and does NOT silently retry with the destructive flags.
- **SC-005**: When the binary is not yet built, the skill emits exactly one confirmation prompt naming the missing binary path AND the `dotnet build` command. If the user replies affirmatively, the skill runs the build, then runs the binary, and reports both — exactly two subprocess invocations (`dotnet build` + binary). If the user declines, zero subprocess invocations occur.
- **SC-006**: When `--bridge-port 54400` is in use (the default), the skill's response suggests at least one specific alternative port number; accepting the suggestion produces a single retry invocation with the new port.
- **SC-007**: Plain-text output exceeding 50 lines is truncated in Claude's response with a count of remaining lines; the user can recover the full output via a single follow-up message ("show all") without re-invoking the binary. JSON output is always surfaced verbatim regardless of line count, so a downstream `jq` or test assertion against `--list --json` always parses successfully.

## Assumptions

- The skill is shipped as a tracked file under `.claude/skills/D2NET-init/SKILL.md`. Claude Code's skill loader picks it up automatically on session start; no registration step is required beyond committing the file.
- Skill name casing (`D2NET-init`) follows the user's literal request. The filesystem path uses the same casing. On case-insensitive filesystems (Windows default) this is cosmetic; on case-sensitive filesystems (Linux, macOS with case-sensitive APFS) the user must type the casing exactly.
- The skill operates against the **current working directory** of the Claude Code session as the repo root, mirroring the binary's FR-002 (002 spec). The skill does NOT walk up to find a `.git/` ancestor.
- Build configurations: Release is preferred over Debug because Release is faster, but in a developer inner-loop only Debug typically exists. The skill picking Release-then-Debug-then-fallback matches the typical developer workflow.
- Claude Code's Bash tool (or equivalent) is available to invoke `d2net-init.exe`. The skill does not invent a new tool surface; it uses whatever shell-invocation primitive Claude Code provides at run time.
- The destructive-marker word list (`force`, `delete`, `rebuild`, `reset`, `recreate`, `reinitialise`, `reinitialize`, `nuke`, `wipe`, `redo`) is a closed list. Tuning it is out of scope for the MVP; the user can always invoke `--FORCE --DELETE-EXISTING` literally to bypass any English-language matching.
- Output truncation threshold (50 lines, FR-017) is a UX trade-off informed by typical Claude Code response readability. It is not a security or correctness boundary.
- The skill itself contains no secrets, no credentials, no environment-specific configuration — it is a pure invocation wrapper. All credentials (the placeholder `d2net`/`d2net` Postgres user/password) live in the binary's own connection-string output, unchanged by this feature.

## Out of Scope

- **Wrapping `D2NET.Scaffold`**: only `D2NET.Init` is wrapped by this skill. A sibling `/D2NET-scaffold` skill (or a unified `/d2net <subcommand>` skill) is a possible future feature.
- **Cross-repo discovery**: the skill operates against the current Claude Code session's CWD. It does not scan other repos or remember the last-used repo across sessions.
- **Workspace migration helpers**: when a SQLite-era `.D2NET/` is detected, the skill surfaces the binary's refusal but does NOT offer to migrate data or transform the schema. The binary's `--FORCE --DELETE-EXISTING` rebuild path (User Story 3) is the only supported recovery.
- **Authentication / authorisation**: the skill does not introduce any security boundary beyond the underlying binary's. The placeholder Postgres credentials remain `d2net`/`d2net`; the skill does not rotate or generate credentials.
- **Telemetry or analytics**: the skill does not record or transmit anything about the user's invocations.
- **Silent auto-rebuild**: the skill never runs `dotnet build` without explicit user confirmation (FR-006). Stale-binary detection prompts; users who do not want to be asked can opt out for the current session, after which the skill proceeds with the existing binary without rebuilding.
