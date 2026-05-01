# Feature Specification: `/D2NET-scaffold` — Claude Code Skill Wrapper Around `d2net-scaffold`

**Feature Branch**: `010-scaffold-skill`
**Created**: 2026-05-01
**Status**: Draft
**Input**: User description: "create a skill wrapper for the D2NET.scaffold CLI tool"

## Background

`d2net-scaffold` is the .NET CLI shipped under `tools/d2net/src/D2Net.Scaffold/` (spec 009). It mirrors the source tree onto the target tree, honouring the workspace's exclusion list, and creates a per-`.dart`-file `__<basename>/` working directory next to every copied Dart source. Source / target / extension / exclusions all come from the workspace at `<cwd>/.D2NET/D2NET-Settings.json` (created earlier by `d2net-init`); the binary takes no positional arguments and only a tiny set of flags (`--help`, `--version`, `--json`, `--bridge-port <N>`, and the destructive override pair `--FORCE --DELETE-TARGET`).

Today, invoking the tool from inside a Claude Code session means dropping to the shell and running `tools/d2net/src/D2Net.Scaffold/bin/Debug/net8.0/d2net-scaffold.exe ...` — clunky, error-prone (path memorisation, build state), and offers no Claude-side post-processing of the result. It is also unsafe to drive the binary's `--FORCE --DELETE-TARGET` flow blind, because that flow opens an interactive `yes/no` prompt from the binary itself naming the absolute path of the target tree about to be deleted; piping `yes` without the operator's explicit consent violates spec 009 FR-012a's hard safety gate.

This feature ships a Claude Code Skill at `.claude/skills/D2NET-scaffold/SKILL.md` so the user can type `/D2NET-scaffold <freeform args>` from any Claude Code session. Claude will (a) locate the binary, (b) translate the user's intent to CLI flags, (c) confirm before any destructive flag combination AND honour the binary's own interactive prompt by surfacing it to the user before piping the affirmative reply, (d) run the binary, and (e) surface the result with hints tailored to scaffold's documented exit codes (workspace missing, source missing, target not empty, working-dir collision, etc.). Skill name preserves the casing convention established by the sibling `D2NET-init` skill — uppercase `D2NET-scaffold` — to match the brand of the underlying CLI.

## Clarifications

### Session 2026-05-01

- Q: When `--json` is in the resolved flag set, what should the Claude-side recap (FR-017) do? → A: Suppress the recap entirely. JSON-mode invocations produce a response containing ONLY the binary's verbatim JSON stdout, so downstream tooling (`jq`, parser-based assertions) consumes the response cleanly. Humans who want the recap omit `--json`.
- Q: What cache key identifies "the same destruction" for the per-session destructive-confirmation cache (FR-013)? → A: Target directory absolute path. The destruction is of a specific path on disk; the cache key matches on that path. Re-init that changes the configured target re-prompts (safe — a different directory is being deleted); repeat invocations against the same target tree within the session do not re-prompt at the skill layer (ergonomic). The binary's own interactive prompt still fires every time the binary runs with `--FORCE --DELETE-TARGET`.
- Q: How should the truncation footer's `show all` / `filter <substring>` hints actually work (FR-018)? → A: Rely on Claude's conversation context. The skill preserves the full binary stdout in the conversation transcript through the truncation footer text; when the user replies in free text with "show all" or "filter <substring>", Claude (the model, not a programmatic sub-command of the skill) reads the buffered transcript and emits the rest. The skill itself is invocation-only and owns no buffer state outside the conversation. Matches the `/D2NET-init` (spec 006) precedent.
- Q: What does `/D2NET-scaffold` with truly empty arguments do? → A: Run scaffold in default mode (run the binary with no flags). The underlying binary's CLI surface treats no-arg invocation as "run the operation"; the skill mirrors that. Help is reached only via the explicit `help` / `--help` / `-h` tokens. This deliberately diverges from the `/D2NET-init` (spec 006) precedent because `d2net-init` requires parameters (so empty = help is right there) whereas `d2net-scaffold` takes no parameters at all (so empty = run is the only meaningful default). FR-011 and User Story 4 are revised to remove the "empty = help" wording; User Story 1 acceptance scenario 1 is canonical.
- Q: What happens when non-empty input contains no recognized verb, marker, or flag (e.g., `/D2NET-scaffold foo` or `/D2NET-scaffold please scaffold quickly`)? → A: Treat as a help request — run the binary's `--help` form. The user has typed something the skill could not interpret; surfacing help is the safest, most discoverable response (the user sees what the skill actually accepts, can correct their input, and re-invoke). Distinct from empty-input behaviour: empty arguments run scaffold in default mode (Q4); only **non-empty unrecognized** input routes to help. The skill MUST NOT silently run the binary against unrecognized natural-language tokens, because that would suppress the signal that the user's intent was not understood.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mirror the source tree from inside Claude Code (Priority: P1)

A developer who has already run `/D2NET-init` (so a `.D2NET/` workspace exists with the source / target / extension / exclusion list populated) types `/D2NET-scaffold` with no arguments. Claude reads the user's intent, locates the `d2net-scaffold` binary in the project's build output, runs it, and reports the binary's stdout summary verbatim along with a short Claude-side recap (target path, files copied, working directories created, dart-files-table rows updated, wall-clock duration). No second prompt, no second shell.

**Why this priority**: This is the entire MVP. Without P1, the skill provides no value over directly invoking the shell — and the whole point of the skill is to make the scaffold step a one-line operation inside Claude Code. The downstream conversion pipeline is blocked until scaffold completes successfully, so making that completion ergonomic is high-leverage.

**Independent Test**: From a Claude Code session in a repo where `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net` has already produced a populated `.D2NET/` workspace, type `/D2NET-scaffold`. Verify (a) Claude locates the d2net-scaffold binary without asking the user where it is, (b) Claude does not ask the user for source / target / extension / exclusions — those come from the workspace, not the user, (c) the binary runs once, exits with status 0, and the target tree `glp_runtime_net/` exists with the expected mirrored layout, (d) Claude's reply contains the binary's success summary plus a one-line recap noting the target path, the file count, and the working-directory count.

**Acceptance Scenarios**:

1. **Given** a repo root with an already-initialised `.D2NET/` workspace and the configured source directory present on disk, **When** the developer types `/D2NET-scaffold`, **Then** Claude runs `d2net-scaffold.exe`, the binary exits with status 0, the target tree exists with every non-excluded source file copied and every `.dart` file's `__<basename>/` working directory created, and Claude's reply includes the binary's summary plus a one-line recap.
2. **Given** the same workspace, **When** the developer types `/D2NET-scaffold as json`, **Then** Claude derives `--json`, runs `d2net-scaffold.exe --json`, and surfaces the binary's structured output verbatim regardless of size so downstream tooling (`jq`, parser-based assertions) can consume it.
3. **Given** the binary has not yet been built (no `bin/Debug/net8.0/d2net-scaffold.exe`), **When** the developer invokes the skill, **Then** Claude prints a single-confirmation prompt naming the missing binary and the `dotnet build tools/d2net/D2Net.sln` command; on the developer's `yes` reply Claude runs the build, then proceeds with the original `/D2NET-scaffold` request and reports its result. On `no` Claude stops without invoking the binary or starting the build.

---

### User Story 2 - Re-scaffold idempotently after exclusion-list or source changes (Priority: P2)

After User Story 1, the developer adjusts the exclusion list (via `/D2NET-init --add-exclude` or `--remove-exclude`) or edits files under the source tree, and re-invokes `/D2NET-scaffold`. The binary's idempotency / reconciliation guarantee (spec 009 FR-010 / FR-011) means a re-run is safe: directories newly excluded are removed from the target; directories newly included are added; bytes that haven't changed are not re-copied unless required by the reconciliation. Claude surfaces the binary's net-additions / net-removals summary so the developer can see what the re-run did.

**Why this priority**: Without ergonomic re-runs, every exclusion-list change forces the developer to think about whether scaffold needs to be re-invoked, which is friction. Making re-runs as easy as the first run lets developers trust the workflow: "after any setting change, just `/D2NET-scaffold` again."

**Independent Test**: After User Story 1 has run successfully, type `/D2NET-init --add-exclude bin`, then type `/D2NET-scaffold` again. Verify Claude runs the binary; verify the binary exits 0; verify `glp_runtime_net/bin/` no longer exists in the target; verify the binary's summary line reports the directories pruned. Repeat with `/D2NET-init --remove-exclude bin --allow-system-exclusions` followed by `/D2NET-scaffold` and verify the directory is recreated.

**Acceptance Scenarios**:

1. **Given** a target tree from a prior scaffold run AND a subsequent exclusion-list change, **When** the developer types `/D2NET-scaffold`, **Then** Claude runs the binary, the target tree is reconciled to the new exclusion list, and the binary's net-additions / net-removals counts are reflected in Claude's response.
2. **Given** a re-run with no underlying changes since the last scaffold run, **When** the developer types `/D2NET-scaffold`, **Then** the binary exits 0, the binary's summary reports zero net additions and zero net removals, and Claude's recap notes "no changes" prominently.
3. **Given** the source directory has been edited (Dart files modified, new files added, files deleted) since the last scaffold run, **When** the developer types `/D2NET-scaffold`, **Then** Claude runs the binary, the target tree is brought into sync (modified files re-copied, new files added, deleted files removed), and the binary's summary reflects the deltas.

---

### User Story 3 - Confirm before destructive target deletion (Priority: P2)

When the developer's request implies `--FORCE --DELETE-TARGET` (e.g., `/D2NET-scaffold force delete target` or `/D2NET-scaffold reset and rebuild target`), Claude MUST present a single confirmation message at the skill layer naming the destructive action ("This will recursively delete `<abs target path>` and all of its contents — proceed?") and wait for the user's explicit approval **before** invoking the binary. Furthermore, because the binary itself emits an interactive `yes/no` prompt on `--FORCE --DELETE-TARGET` (spec 009 FR-012a, a hard safety gate that cannot be bypassed by `--non-interactive`), Claude MUST drive that prompt via stdin only after the skill-layer confirmation has been answered affirmatively by the user — Claude MUST NOT pre-pipe `yes` blindly. The two confirmations together (skill layer + relayed binary prompt) form a single coherent safety flow from the user's perspective.

Implicit destructive actions, where the user's literal text does not include a destructive marker (`force`, `delete`, `rebuild`, `reset`, `recreate`, `nuke`, `wipe`, `redo`), MUST NOT trigger `--FORCE --DELETE-TARGET` automatically — Claude either runs the safe form (which the binary will refuse with `ScaffoldTargetNotEmptyAndNotManaged` exit 24 if a non-scaffold-managed target tree exists) or asks the user for clarification.

**Why this priority**: Important for safety. P2 because (a) the binary's own interactive prompt is a real safety net even without the skill's pre-confirmation, and (b) the binary's `ScaffoldTargetNotEmptyAndNotManaged` (24) refusal is the safe default for non-destructive runs. Adding the skill-layer confirmation matches the careful-actions discipline elsewhere in this codebase and the precedent set by the D2NET-init skill (spec 006 User Story 3).

**Independent Test**: With a target tree present that scaffold did not produce (e.g., the developer hand-created `glp_runtime_net/` with arbitrary content), type `/D2NET-scaffold force delete target`. Verify Claude (a) detects the destructive intent and emits a skill-layer confirmation message naming the absolute target path, (b) does NOT invoke the binary until the user replies affirmatively, (c) on affirmative reply, runs the binary with `--FORCE --DELETE-TARGET` AND drives the binary's interactive prompt with `yes` via stdin, (d) reports both the binary's prompt text and the user-confirmed reply in the surfaced output so the safety flow is auditable. Type `/D2NET-scaffold` (no destructive verbs) against the same target and confirm Claude does NOT pass `--FORCE --DELETE-TARGET` automatically — the binary refuses with exit 24 and Claude surfaces the hint.

**Acceptance Scenarios**:

1. **Given** a target tree not managed by a prior scaffold run AND the developer's input contains a destructive verb (`force` / `delete` / `rebuild` / `reset` / `recreate` / `reinitialise` / `reinitialize` / `nuke` / `wipe` / `redo`), **When** the developer invokes the skill, **Then** Claude emits a skill-layer confirmation message naming the absolute target path; on affirmative reply Claude invokes `d2net-scaffold.exe --FORCE --DELETE-TARGET` AND pipes `yes\n` to the binary's stdin to satisfy the binary's own prompt; on non-affirmative reply Claude exits without invoking the binary.
2. **Given** an unmanaged target tree AND the developer's input contains no destructive verb, **When** the developer invokes the skill, **Then** Claude runs the binary without `--FORCE --DELETE-TARGET`; the binary refuses with `ScaffoldTargetNotEmptyAndNotManaged` (exit 24); Claude surfaces that error and appends a hint suggesting the destructive override (`/D2NET-scaffold force delete target`) without auto-running it.
3. **Given** the developer has already affirmatively answered the skill-layer destructive confirmation in the current Claude Code session for this same `.D2NET` workspace, **When** the developer issues a second destructive scaffold request in the same session, **Then** the skill MAY skip the skill-layer confirmation but MUST still drive the binary's own interactive prompt with `yes` via stdin (the binary re-prompts every invocation as designed by spec 009).

---

### User Story 4 - Help, version, and inspection of binary surface (Priority: P3)

The developer types `/D2NET-scaffold help`, `/D2NET-scaffold --help`, or `/D2NET-scaffold version` to read the binary's help text or version banner without leaving Claude. The skill maps each form to the corresponding flag (`--help` or `--version`), runs the binary, and surfaces the output verbatim. Note: empty `/D2NET-scaffold ` arguments do NOT route to `--help` — they run the scaffold operation in default mode (User Story 1). The user must explicitly type a help token to see help.

**Why this priority**: Discoverability ergonomics. Not strictly required for the workflow to function — developers can read `tools/d2net/src/D2Net.Scaffold/Program.cs` for the surface — but the one-line skill experience is part of the value proposition.

**Independent Test**: Type `/D2NET-scaffold help` and verify Claude runs `d2net-scaffold.exe --help` and surfaces the binary's documented usage block. Type `/D2NET-scaffold version` and verify Claude runs `d2net-scaffold.exe --version` and surfaces the version line. Type `/D2NET-scaffold` (empty arguments) and verify Claude does NOT route to `--help` — it runs the scaffold operation per User Story 1.

**Acceptance Scenarios**:

1. **Given** a Claude Code session in any directory, **When** the developer types `/D2NET-scaffold help` (or `--help` / `-h`), **Then** Claude runs the binary's `--help` form and surfaces the result verbatim. The skill MUST NOT augment, truncate, or paraphrase the help text.
2. **Given** the same session, **When** the developer types `/D2NET-scaffold version`, **Then** Claude runs `--version` and surfaces the version line verbatim.
3. **Given** the same session AND no `.D2NET/` workspace at the current working directory, **When** the developer types `/D2NET-scaffold help`, **Then** the binary still emits the help text (help does not require a workspace) and Claude surfaces it. (Help is purely informational and does not depend on workspace state.)

---

### Edge Cases

- **Binary not built**: when `bin/Debug/net8.0/d2net-scaffold.exe` (or the platform equivalent) does not exist AND the Release path also does not exist, Claude prints a single-confirmation prompt ("d2net-scaffold binary is missing at <path>; build now? (yes/no)") and on `yes` runs `dotnet build tools/d2net/D2Net.sln` then proceeds with the original request. On `no` (or any non-affirmative reply) Claude stops without invoking the binary.
- **Binary present but stale**: when the binary exists but a `.cs` file under `tools/d2net/src/D2Net.Scaffold/` has a newer mtime than the binary, Claude prints the same single-confirmation prompt ("d2net-scaffold binary may be stale (source newer than binary); rebuild now? (yes/no)") and proceeds the same way. Stale-binary confirmation MAY be skipped only when the user already explicitly opted out in the current session ("don't ask about staleness again"). The staleness check MUST exclude any `pgbridge/` subtree (Node sidecar artefacts unrelated to .NET compilation), mirroring the D2NET-init skill's convention.
- **Workspace missing (exit 22, `ScaffoldWorkspaceMissing`)**: Claude surfaces the binary's stderr verbatim and appends a one-line hint suggesting `/D2NET-init` to create the workspace first. Claude does NOT auto-invoke `/D2NET-init` — the user must explicitly choose.
- **Source directory missing (exit 23, `ScaffoldSourceMissing`)**: Claude surfaces the binary's stderr (which names the missing path) and offers to inspect the parent directory for typo-style help. Claude does NOT auto-create the directory.
- **Target not empty and not scaffold-managed (exit 24, `ScaffoldTargetNotEmptyAndNotManaged`)**: Claude surfaces the binary's stderr and appends a one-line hint suggesting either (a) `/D2NET-scaffold force delete target` to overwrite (which routes through User Story 3's destructive-confirmation flow) or (b) manual cleanup of the target before retrying. Claude does NOT auto-retry with the destructive flags.
- **Working-directory collision (exit 25, `ScaffoldWorkdirCollision`)**: Claude surfaces the binary's stderr verbatim (which names every offending path). The user is responsible for resolving the collision in the source tree (typically by renaming or removing the conflicting `__<basename>` artefact); Claude does NOT attempt automatic resolution.
- **Copy error (exit 26, `ScaffoldCopyError`)**: Claude surfaces the binary's stderr verbatim. This is the post-COMMIT rename window described in spec 009 FR-014; Claude appends a one-line hint noting that re-running scaffold (FR-010 idempotency) typically reconciles the half-state.
- **DB write failed (exit 27, `ScaffoldDbWriteFailed`)**: Claude surfaces the binary's stderr verbatim. No specific recovery hint — the underlying cause is workspace-database corruption or PGLite bridge failure, both of which need operator diagnosis.
- **Workspace lock contention (exit 28, `ScaffoldWorkspaceLocked`)**: Claude surfaces the binary's stderr. Optional one-line hint: another `d2net-init` or `d2net-scaffold` invocation is currently writing to the workspace; retry in a moment.
- **Operator declined the destructive prompt (exit 29, `ScaffoldOperatorCancelledTargetDeletion`)**: Claude surfaces the binary's stderr (which confirms no changes were made) and reports cleanly. This is the binary's response when the user typed `no` (or anything non-affirmative) at the binary's interactive prompt — even after the skill-layer confirmation; Claude treats this as a clean exit with no further action needed.
- **Argument error (exit 1, `ArgumentError`)**: typically because the user supplied `--FORCE` without `--DELETE-TARGET` (or vice versa), or supplied an unknown flag. Claude surfaces the binary's stderr and reminds the user that the destructive flag pair must be supplied together.
- **User passes raw flag-style args** (e.g., `/D2NET-scaffold --json --bridge-port 55000`): Claude accepts the flag-style input as a pass-through. No translation needed. The destructive-pair detection (`--FORCE --DELETE-TARGET`) still applies and triggers User Story 3's flow.
- **User mixes natural-language and flag-style** (e.g., `/D2NET-scaffold scaffold the target as json`): Claude derives the missing flags from the natural-language portion (`--json` from "as json") and combines with any explicit flags. Same precedent as `/D2NET-init` (spec 006 FR-010).
- **Output too large**: when `--json` is supplied, the binary's structured output is surfaced verbatim regardless of size (the same JSON-bypass rule as spec 006 FR-017). When plain-text output exceeds 50 lines (rare for scaffold — its summary is concise — but possible if `--json` is absent and the binary lists every file copied), Claude truncates to the first ~50 lines plus a count footer ("... and N more lines (total: M). Reply 'show all' to see everything"). The full output is preserved in the conversation history for follow-up.
- **Skill invoked outside a Claude Code session**: not applicable — Claude Code skills are session-scoped by definition.
- **`--FORCE --DELETE-TARGET` supplied but target directory does not exist**: per spec 009's documented behaviour, the binary skips the destructive prompt (nothing to delete) and proceeds with the normal scaffold flow. Claude relays the binary's behaviour as-is and notes the no-op deletion in its recap so the user understands what happened.

## Requirements *(mandatory)*

### Functional Requirements

#### Skill registration

- **FR-001**: The skill MUST be invocable as `/D2NET-scaffold` (preserving the casing convention established by the sibling `/D2NET-init` skill — uppercase `D2NET`, lowercase `scaffold`) from any Claude Code session in a repo that contains the skill's `.claude/skills/D2NET-scaffold/` directory.
- **FR-002**: The skill MUST be implemented as a `SKILL.md` markdown file with valid Claude Code skill frontmatter, matching the convention of the existing `.claude/skills/D2NET-init/SKILL.md` and `.claude/skills/speckit-*/SKILL.md` files: `name`, `description`, `argument-hint`, `compatibility`, `metadata`, `user-invocable: true`, `disable-model-invocation: false`.
- **FR-003**: The skill MUST accept a single freeform argument string (`$ARGUMENTS`) covering both natural-language descriptions of the user's intent AND raw flag-style invocations of the underlying binary. Empty arguments MUST be treated as a request to run the binary in **default scaffold mode** (no flags), because the binary's own CLI surface treats no-arg invocation as "run the operation" and source/target/extension/exclusions are read from the workspace, not from CLI input. (Clarified 2026-05-01; deliberately diverges from `/D2NET-init` where empty = help, because `d2net-scaffold` takes no parameters.)

#### Binary discovery and invocation

- **FR-004**: The skill MUST locate the `d2net-scaffold` binary via the following search order, stopping at the first hit:
  1. `tools/d2net/src/D2Net.Scaffold/bin/Release/net8.0/d2net-scaffold.exe` (or `d2net-scaffold` on non-Windows).
  2. `tools/d2net/src/D2Net.Scaffold/bin/Debug/net8.0/d2net-scaffold.exe`.
  3. The fallback form `dotnet run --project tools/d2net/src/D2Net.Scaffold -- <args>`.

  When the search reaches step 3, the skill MUST inform the user that it is using the slower fallback and recommend running `dotnet build` once.
- **FR-005**: When neither the Release nor the Debug binary exists AND the fallback `dotnet run` would also fail (e.g., `dotnet` not on PATH), the skill MUST report the missing prerequisites clearly with concrete paths and stop without running anything.
- **FR-006**: When the binary is missing OR a `.cs` file under `tools/d2net/src/D2Net.Scaffold/` (excluding the `pgbridge/` subtree, mirroring the D2NET-init skill's staleness convention) has a newer mtime than the binary, the skill MUST emit a single confirmation prompt naming the situation ("missing" vs "stale") and the build command (`dotnet build tools/d2net/D2Net.sln`) and MUST wait for an affirmative single-word reply (`yes`, `y`, `confirmed`, `proceed`) before running `dotnet build`. On affirmative reply, the skill MUST run the build, surface its output verbatim, and on success continue with the original `/D2NET-scaffold` request in the same response. On any non-affirmative reply (or no reply within the session), the skill MUST stop and MUST NOT invoke the binary. The skill MUST NEVER run `dotnet build` without an affirmative confirmation in this session, EXCEPT that for the stale-binary case the user MAY opt out for the current session by replying with a phrase such as "don't ask about staleness" — after which the stale-binary confirmation is suppressed and the skill proceeds with the existing binary. **Compaction degradation**: auto-compaction may drop the opt-out signal from the surviving conversation context; in that case the skill MUST re-prompt (safe failure mode, mirroring FR-013's compaction behaviour). The skill MUST NOT use filesystem persistence to compensate.
- **FR-007**: The skill MUST NOT pass any `--non-interactive`-style flag to `d2net-scaffold` because the binary does not support such a flag (the binary's CLI surface is `--help`, `--version`, `--json`, `--bridge-port <N>`, `--FORCE --DELETE-TARGET`). The binary's interactive prompt for `--FORCE --DELETE-TARGET` is a hard safety gate (spec 009 FR-012a) and MUST be honoured by the skill via the User Story 3 flow rather than bypassed.

#### Intent translation

- **FR-008**: When the user's input is parseable as flag-style CLI args (every token is either a `--flag`, `-h`, or a value for the prior flag), the skill MUST treat the input as a pass-through and forward verbatim to the binary, EXCEPT that the destructive pair detection of FR-012 still applies and may insert the User Story 3 confirmation flow.
- **FR-009**: When the user's input is natural-language, the skill MUST derive flags from the input. The supported parameter shapes are at minimum:
  - **Verbs and their resolved-flag mappings**:
    - `scaffold` (or no verb) → no flag (default scaffold mode; equivalent to running the binary with no arguments).
    - `help` → `--help`. Short-circuit: skip Steps 5–11 of the procedure; surface the binary's help text and stop.
    - `version` → `--version`. Short-circuit similarly.
  - **JSON markers**: phrases containing "json" / "as json" / "in json" / "give me json" / "structured" → add `--json` to the resolved flag set.
  - **Bridge-port markers**: phrases like "bridge port 55001", "on port 55001", or `bridge-port=55001` → add `--bridge-port 55001` to the resolved flag set.
  - **Destructive markers** (full list under FR-012): trigger the User Story 3 confirmation flow and, on affirmative reply, add `--FORCE --DELETE-TARGET` to the resolved flag set.

  The skill MUST document the full grammar in its own SKILL.md so a user reading the skill's help understands what they can ask for.
- **FR-010**: When the user's input is mixed (natural-language prefix + raw flags), the skill MUST take the raw flags verbatim and derive only the un-supplied flags from the natural-language portion. Mirrors `/D2NET-init` (spec 006 FR-010).
- **FR-010a**: When the user's non-empty input contains tokens that match no recognized verb (`scaffold` / `help` / `version`), no recognized marker (JSON marker, destructive marker, bridge-port marker), and no flag-style token, the skill MUST run the binary's `--help` form and surface the help text (rather than running the scaffold operation). This routes the user to the discoverability path so they can see what the skill accepts, correct their input, and re-invoke. The skill MUST NOT silently run the binary against unrecognized natural-language tokens, because that would suppress the signal that the user's intent was not understood. (Distinct from empty-input behaviour per FR-003: empty arguments run scaffold in default mode; only **non-empty unrecognized** input routes to help.) Clarified 2026-05-01.
- **FR-011**: When the user's input consists only of `help` / `--help` / `-h`, the skill MUST run the binary's `--help` form and surface the result. Empty input is NOT treated as a help request — see FR-003 (empty = run scaffold in default mode).

#### Destructive-operation safety

- **FR-012**: When the user's input contains any of the destructive markers `force`, `delete`, `rebuild`, `reset`, `recreate`, `reinitialise`, `reinitialize`, `nuke`, `wipe`, `redo`, OR the explicit flag pair `--FORCE --DELETE-TARGET`, the skill MUST treat the request as destructive. The skill MUST emit a single skill-layer confirmation message naming the affected target directory's absolute path and the specific destructive action ("recursively delete `<abs target path>` and all of its contents"), and MUST wait for an affirmative single-word reply (`yes`, `y`, `confirmed`, `proceed`) before invoking the binary with `--FORCE --DELETE-TARGET`.
- **FR-013**: Once the skill-layer confirmation has been answered affirmatively in the current Claude Code session for a given **target directory absolute path**, the skill MAY skip the skill-layer confirmation on subsequent invocations within the same session whose resolved target absolute path matches the cached value, BUT the skill MUST still drive the binary's own interactive `yes/no` prompt every time the binary runs with `--FORCE --DELETE-TARGET`. (The binary re-prompts every invocation by design — spec 009 FR-012a — and the skill must honour that.) The cache key is the **target directory absolute path** as resolved at invocation time; if a re-init of the workspace between invocations changes the configured target, the new target absolute path will not match the cached value and the skill MUST re-prompt at the skill layer (clarified 2026-05-01). The cache is in-memory only and does not persist across Claude Code sessions.
- **FR-014**: When invoking the binary with `--FORCE --DELETE-TARGET` after an affirmative confirmation, the skill MUST drive the binary's interactive prompt by piping `yes\n` to its stdin. The skill MUST NOT pre-pipe `yes` blindly (i.e., without the user's explicit prior consent in the same session). The skill MUST also surface, in its response, the binary's prompt text along with the resolved `yes` reply so the safety flow is auditable in the conversation transcript.
- **FR-015**: The skill MUST NEVER pass `--FORCE --DELETE-TARGET` to the binary unless either (a) the user's literal input contained that exact flag pair AND the destructive-marker confirmation flow of FR-012 was completed affirmatively in this session, OR (b) the user's natural-language input contained a destructive marker AND the same FR-012 flow was completed affirmatively. Implicit `--FORCE --DELETE-TARGET` injection is forbidden.
- **FR-016**: The skill MUST refuse to honour the destructive flag pair if only one of the two flags is supplied (e.g., `/D2NET-scaffold --FORCE` alone). The skill MAY surface the binary's exit-1 argument-error message in this case rather than rejecting the input at the skill layer; either treatment is acceptable as long as the binary does not actually delete anything.

#### Result surfacing

- **FR-017**: After the binary returns, the skill MUST surface the binary's stdout verbatim, then the exit code, then a brief Claude-side recap when applicable: target tree path on success, files copied, working directories created, dart-files-table rows updated, and total wall-clock duration. The recap MUST NOT contradict or substitute for the binary's own output — it is supplementary.

  **JSON-mode suppression**: When `--json` is in the resolved flag set (whether the user supplied it literally or the skill translated it from natural-language phrases such as "as json"), the skill MUST suppress the Claude-side recap entirely. The response contains ONLY the binary's verbatim JSON stdout (plus, on non-zero exit, the binary's stderr and exit code per FR-019), so downstream tooling (`jq`, parser-based assertions) consumes the response cleanly. Humans who want the recap omit `--json`. Clarified 2026-05-01.
- **FR-018**: When the binary's stdout is **plain text** (no `--json` flag in the resolved invocation) AND exceeds **50 lines**, the skill MUST truncate the surfaced output to the first ~50 lines plus a "... and N more lines (total: M). Reply 'show all' to see everything, or 'filter <substring>' to narrow." footer. The full output MUST be preserved in the conversation history so the user can request it without re-running the binary. The `show all` / `filter <substring>` follow-ups are NOT programmatic sub-commands of the skill; they are free-text replies the user types in the next turn, which Claude (the model) services by reading the buffered stdout from the conversation transcript and emitting the requested portion. The skill itself owns no buffer state outside the transcript (clarified 2026-05-01).

  When the resolved invocation includes `--json` (whether the user supplied it literally or the skill translated it from natural-language phrases such as "as json"), the skill MUST surface the binary's stdout **verbatim regardless of line count** to preserve JSON parseability for downstream tooling. The 50-line threshold MUST NOT apply to JSON outputs. Mirrors `/D2NET-init` (spec 006 FR-017).
- **FR-019**: When the binary exits with any non-zero status, the skill MUST surface the binary's stderr verbatim, then the exit code. The skill MUST NOT silently swallow errors. For specific exit codes, the skill MUST append a one-line hint:
  - **Exit 22 (`ScaffoldWorkspaceMissing`)**: "No `.D2NET/` workspace at this directory. Run `/D2NET-init` first."
  - **Exit 23 (`ScaffoldSourceMissing`)**: surface the binary's path; offer to inspect the parent directory for typo-style help.
  - **Exit 24 (`ScaffoldTargetNotEmptyAndNotManaged`)**: "The target tree contains content not produced by a prior scaffold run. Reply `/D2NET-scaffold force delete target` to overwrite (a confirmation prompt will follow)."
  - **Exit 25 (`ScaffoldWorkdirCollision`)**: surface the binary's listed offending paths; user must resolve manually.
  - **Exit 26 (`ScaffoldCopyError`)**: "Filesystem error during scaffold. The binary's idempotency property means re-running typically reconciles a half-state."
  - **Exit 27 (`ScaffoldDbWriteFailed`)**: surface the binary's stderr; no auto-recovery.
  - **Exit 28 (`ScaffoldWorkspaceLocked`)**: "Another `d2net-init` or `d2net-scaffold` invocation holds the workspace lock. Retry shortly."
  - **Exit 29 (`ScaffoldOperatorCancelledTargetDeletion`)**: treat as a clean stop; surface the binary's stderr noting no changes were made.
  - **Exit 1 (`ArgumentError`)**: surface the binary's stderr; remind the user that `--FORCE` and `--DELETE-TARGET` must be supplied together.

  For other non-zero exit codes, the skill MUST surface stderr only; no specific hint required.

### Key Entities *(include if feature involves data)*

- **Skill (`.claude/skills/D2NET-scaffold/SKILL.md`)**: A markdown file with YAML frontmatter that Claude Code loads when the user types `/D2NET-scaffold <args>`. The body is procedural instructions Claude follows: locate binary, parse intent, confirm if destructive, invoke (driving the binary's stdin if destructive), surface results.
- **Binary discovery result**: One of three states — Release binary present, Debug binary present, fallback `dotnet run` required. Each step in FR-004 maps to one state.
- **Argument bundle**: The translated set of CLI flags Claude derived from the user's input. Internal to the skill; does not persist beyond a single invocation.
- **Destructive-confirmation cache**: A per-session in-memory record of target directory absolute paths the user has already confirmed for destruction at the skill layer (FR-013). Internal to the skill; does not persist across sessions.
- **All entities from `specs/009-scaffold-mirror/spec.md`** (workspace settings, source / target / extension / exclusion list, dart_files table, working directories, target tracker, etc.) are unchanged — the skill is a thin invocation wrapper.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a Claude Code session in a repo where a Release or Debug binary already exists AND the workspace is initialised, the user can complete a fresh `D2NET.Scaffold` run end-to-end via a single `/D2NET-scaffold` message in under **70 seconds** wall-clock for a workspace with up to 1,000 dart + 5,000 non-dart files (spec 009 SC-001's binary-side ceiling is 60 s; the skill adds at most ~10 s of binary-discovery and result-surfacing overhead).
- **SC-002**: The skill correctly translates each of the following inputs into the documented flag set:
  - empty arguments → no flag (default scaffold mode; runs the binary with no flags). Empty does NOT route to `--help` (clarified 2026-05-01).
  - `help` / `--help` / `-h` → `--help` (skill short-circuits; no augmentation).
  - `version` / `--version` → `--version` (skill short-circuits; no augmentation).
  - `scaffold` (or other natural-language phrasings without a verb token) → no flag (default scaffold mode), same outcome as empty input.
  - `as json` / `in json` / `give me json` / `--json` → `--json`.
  - `bridge-port=55001` / `on bridge port 55001` / `--bridge-port 55001` → `--bridge-port 55001`.
  - `force delete target` → after a one-message skill-layer confirmation answered affirmatively, `--FORCE --DELETE-TARGET` AND `yes\n` piped to the binary's stdin.
  - `--FORCE --DELETE-TARGET` (literal pair) → same as above (same confirmation flow + same stdin drive).
- **SC-003**: A destructive-marker input (`/D2NET-scaffold force rebuild target`) produces zero binary invocations until the user replies `yes` (or equivalent affirmative) to the skill-layer confirmation; a confirmed destructive flow produces exactly one binary invocation with `--FORCE --DELETE-TARGET` in the args AND `yes\n` driven into stdin.
- **SC-004**: A non-destructive input against a target tree not produced by a prior scaffold run produces an invocation that exits with `ScaffoldTargetNotEmptyAndNotManaged` (24); the skill surfaces that error with the destructive-override hint and does NOT silently retry with the destructive flags.
- **SC-005**: When the binary is not yet built, the skill emits exactly one confirmation prompt naming the missing binary path AND the `dotnet build` command. If the user replies affirmatively, the skill runs the build, then runs the binary, and reports both — exactly two subprocess invocations (`dotnet build` + binary). If the user declines, zero subprocess invocations occur.
- **SC-006**: When `--bridge-port <X>` is supplied AND that port is in use, the binary's exit signal flows through the skill: the skill surfaces the binary's stderr (exit code is `ScaffoldDbWriteFailed` 27 or `ScaffoldWorkspaceLocked` 28 depending on which subsystem fails first; both are documented in FR-019). The skill does NOT auto-retry with a different port — `d2net-scaffold` is not interactive in the same way `d2net-init` is, and the bridge-port flag is operator-supplied for advanced cases only.
- **SC-007**: Plain-text output exceeding 50 lines is truncated in Claude's response with a count of remaining lines; the user can recover the full output via a single follow-up message ("show all") without re-invoking the binary. JSON output is always surfaced verbatim regardless of line count, so a downstream `jq` pipeline or test assertion against `--json` always parses successfully.
- **SC-008**: Each of the seven scaffold-specific failure exit codes (22, 23, 24, 25, 26, 27, 28) AND the operator-cancelled exit code (29) AND the argument-error code (1) MUST be surfaced with the correct exit number and the correct one-line hint (FR-019) in 100 % of triggering test runs. Exit codes Claude does not have a specific hint for (the "others" bucket of FR-019) MUST surface the binary's stderr verbatim with no inferred hint.

## Assumptions

- The skill is shipped as a tracked file under `.claude/skills/D2NET-scaffold/SKILL.md`. Claude Code's skill loader picks it up automatically on session start; no registration step is required beyond committing the file.
- Skill name casing (`D2NET-scaffold`) follows the convention established by the sibling `D2NET-init` skill (uppercase `D2NET`, lowercase tool name). The filesystem path uses the same casing. On case-insensitive filesystems (Windows default) this is cosmetic; on case-sensitive filesystems (Linux, macOS with case-sensitive APFS) the user must type the casing exactly.
- The skill operates against the **current working directory** of the Claude Code session as the repo root, mirroring `d2net-scaffold`'s own FR-002 (spec 009). The skill does NOT walk up to find a `.git/` ancestor.
- Build configurations: Release is preferred over Debug because Release is faster, but in a developer inner-loop only Debug typically exists. Release-then-Debug-then-fallback matches the typical developer workflow.
- Claude Code's Bash tool (or equivalent) is available to invoke `d2net-scaffold.exe`. The skill does not invent a new tool surface; it uses whatever shell-invocation primitive Claude Code provides at run time.
- The destructive-marker word list (`force`, `delete`, `rebuild`, `reset`, `recreate`, `reinitialise`, `reinitialize`, `nuke`, `wipe`, `redo`) is the same closed list used by `/D2NET-init` (spec 006). Tuning is out of scope; the user can always invoke `--FORCE --DELETE-TARGET` literally to bypass any English-language matching.
- **Bridge-port collision testing**: SC-006 covers the user-supplied-`--bridge-port` collision path. Reproducing the collision requires manually binding the port (e.g., a stale background process on 54400). Smoke-walk testing of this scenario is treated as a manual operator test, not part of the routine smoke matrix in `tasks.md` Phase 6.
- **Plain-text truncation testing**: SC-007's plain-text-truncation half is hard to trigger naturally because `d2net-scaffold`'s plain-text summary is concise (5–10 lines typical). The 50-line truncation rule is identical to `/D2NET-init` FR-017, where it IS exercised against `--list` against a 1000-file tree; the rule's correctness inherits from that test coverage. JSON-verbatim half is exercised directly in tasks.md T011.
- Output truncation threshold (50 lines, FR-018) matches the precedent from `/D2NET-init` (spec 006 FR-017). It is not a security or correctness boundary.
- The skill itself contains no secrets, no credentials, no environment-specific configuration — it is a pure invocation wrapper. All credentials and connection strings live inside the binary's own outputs and the workspace settings file, unchanged by this feature.
- The binary's interactive prompt for `--FORCE --DELETE-TARGET` accepts `yes` / `y` / `confirmed` / `proceed` (matching the broader convention in this codebase and spec 009 FR-012a). The skill drives the binary's stdin with the literal `yes\n` to be unambiguous; the binary's prompt parser MUST accept that as affirmative.
- Bridge-port reuse is rare in practice for scaffold (the bridge subprocess is short-lived), so the skill does not implement automatic port-bumping retry analogous to `/D2NET-init`'s `BridgePortInUse` (5) hint. Operators who hit a port collision can re-run with an explicit `--bridge-port <other>`.

## Out of Scope

- **Wrapping additional D2NET tools**: only `D2NET.Scaffold` is wrapped by this skill. Future tools (analyzer, porter, etc.) get their own sibling skills or a unified `/d2net <subcommand>` skill — out of scope here.
- **Cross-session memory**: the skill operates against the current Claude Code session's CWD and does not remember past invocations across sessions. Each fresh session re-prompts for destructive operations even if a prior session confirmed them.
- **Workspace migration helpers**: the skill does not transform workspace database schemas, migrate dart_files row formats, or convert SQLite-era workspaces. Those concerns live with `D2NET.Init` (spec 006 / 009).
- **Authentication / authorisation**: the skill does not introduce any security boundary beyond the underlying binary's. The placeholder Postgres credentials remain `d2net`/`d2net`; the skill does not rotate or generate credentials.
- **Telemetry or analytics**: the skill does not record or transmit anything about the user's invocations.
- **Silent auto-rebuild**: the skill never runs `dotnet build` without explicit user confirmation (FR-006). Stale-binary detection prompts; users who do not want to be asked can opt out for the current session, after which the skill proceeds with the existing binary without rebuilding.
- **Bridge-port auto-retry**: unlike `/D2NET-init` (which suggests `--bridge-port <X+1>` on `BridgePortInUse` exit 5), the scaffold skill does NOT implement automatic port-bumping retry. The operator is expected to supply `--bridge-port` only as an advanced override; retry is the operator's responsibility.
- **Auto-invoking `/D2NET-init` on `ScaffoldWorkspaceMissing` (22)**: the skill surfaces the hint but does NOT chain into `/D2NET-init` automatically. Workspace creation is a deliberate operator action with its own destructive flags and exclusion-list decisions; chaining the two skills would surrender that deliberation.
