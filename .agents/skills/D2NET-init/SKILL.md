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


## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Goal

Run the `d2net-init` CLI on behalf of the user. Translate freeform input (natural-language or raw flags) into the binary's CLI flag set, locate the binary, confirm before destructive operations or `dotnet build`, invoke, and surface the result.

The procedure below is the contract from `specs/006-d2net-init-skill/contracts/skill-contract.md`. Follow each step in order.

## Operating Constraints

- **NEVER pass `--FORCE --DELETE-EXISTING`** to the binary unless the destructive-operation gate (Step 6) was completed affirmatively in this conversation OR the user supplied the literal flag pair AND completed Step 6.
- **NEVER run `dotnet build`** without an affirmative reply to Step 3's confirmation prompt in this conversation.
- **NEVER walk up** to find `.git/` or change the working directory the user invoked from.
- **NEVER modify** `.D2NET/D2NET-Settings.json` directly. Only the binary writes settings; this skill is invocation-only.
- **NEVER invent CLI flags** the binary does not support. The full flag list is the union of every flag in `tools/d2net/src/D2Net.Init/Program.cs` ArgParser plus the `--help` / `--version` shortcuts.
- **AT MOST ONE** `dotnet build` per skill invocation. If the build fails, surface its stderr and stop.

## Procedure

### Step 1 — Read user input

The user's freeform argument string is in the `$ARGUMENTS` block above. Treat the empty string identically to the literal token `help`.

### Step 2 — Locate the binary (FR-004 / FR-005)

Search in this exact order, stopping at the first hit. On Windows the binary is `d2net-init.exe`; on macOS / Linux it is `d2net-init`.

1. `tools/d2net/src/D2Net.Init/bin/Release/net8.0/d2net-init[.exe]`.
2. `tools/d2net/src/D2Net.Init/bin/Debug/net8.0/d2net-init[.exe]`.
3. Fallback to `dotnet run --project tools/d2net/src/D2Net.Init -- <args>`. If selecting this path, emit a one-line note: "Using `dotnet run` fallback (slower). Run `dotnet build tools/d2net/D2Net.sln` once to enable the direct-binary path."

Use the Bash tool with `ls` / `test -f` (or `Test-Path` via PowerShell) to verify file presence before adopting a path. Resolve `tools/d2net/src/D2Net.Init/bin/Debug/net8.0/d2net-init.exe` relative to the current working directory.

If neither Release nor Debug binary exists AND `dotnet --version` fails (no .NET SDK on PATH), emit a single message naming all three missing prerequisites and stop. Do not invoke anything.

### Step 3 — Detect missing or stale binary (FR-006)

If Step 2 selected the fallback path because **no binary exists**, the binary is **missing**. Otherwise check **staleness**: compute `mtime(binary)` and `max(mtime(.cs file under tools/d2net/src/D2Net.Init))`. (Feature 012 removed the bundled `pgbridge/` subtree — the unified bridge at `prereq-patterns/pglite/pglite_bridge.mjs` is now the source of truth, and any change to it does NOT trigger a d2net-init rebuild.)

- If the binary is missing OR the binary's mtime is older than the newest `.cs` file under `tools/d2net/src/D2Net.Init`, emit ONE confirmation prompt:
  - Missing: `d2net-init binary is missing at <path> — build now? (yes/no)`
  - Stale: `d2net-init binary may be stale (newest source is <path> modified <time>; binary built <time>) — rebuild now? (yes/no)`
- Wait for an affirmative reply (`yes`, `y`, `confirmed`, `proceed`).
- On affirmative, run `dotnet build tools/d2net/D2Net.sln` via Bash, surface its output, and on success continue with the original `/D2NET-init` request.
- On any non-affirmative reply, stop without invoking the binary.

If the user previously replied with a phrase containing "don't ask" / "skip staleness" / "ignore stale" in this conversation, suppress the **stale-only** prompt for the rest of the conversation (still prompt on missing binary).

Use the conversation transcript to detect the opt-out phrase from prior turns.

### Step 4 — Parse user intent (FR-008 / FR-009 / FR-010 / FR-011)

Branch on `$ARGUMENTS`:

- **Empty / `help` / `--help` / `-h`** → set resolved flag set to `[--help]`. Skip Steps 5, 6, 7 (no augmentation, no destructive gate, no shortcut). Proceed to Step 8 with `[--help]`.
- **All tokens flag-style** (every token is `--<flag>` or a value following one) → treat as pass-through. Initial resolved flag set = the user's tokens verbatim. Proceed to Step 6 (destructive gate may still apply if `--FORCE --DELETE-EXISTING` is in the literal flags).
- **Mixed** (some flag-style + some natural-language) → take the raw flags verbatim into the resolved flag set; derive only the un-supplied flags from the natural-language portion using the grammar below.
- **Pure natural-language** → derive flags via the grammar:
  - **Key-value pairs**: `source=X` → `--source X`; `extension=X` → `--target-extension X`; `target=X` → `--target X`; `bridge-port=N` → `--bridge-port N`. **Note (feature 012)**: `--bridge-port` is now a no-op — the unified PGLite bridge at `<repo>/.pgdb/` always uses an OS-allocated ephemeral port, with discovery via `<repo>/.pgdb/bridge.json` rather than caller-supplied port. The flag is still accepted for backwards-compat; its value is ignored. There is no `exclude=` form. The user adds exclusions only via the literal `--exclude <path>` flag (repeatable); mixed inputs preserve raw `--exclude` flags verbatim.
  - **Verbs and their resolved-flag mappings**:
    - `init` → init mode (no extra inspection flag; require source/extension/target via Step 5 or other tokens).
    - `list` → `--list`.
    - `exclusions` → `--Exclusions`.
    - `current-phase` (or `current phase`) → `--current-phase`.
    - `help` → resolved flag set = `[--help]`. Short-circuit: skip Steps 5–7, 9–11; proceed to Step 8.
    - `version` → resolved flag set = `[--version]`. Short-circuit: skip Steps 5–7, 9–11; proceed to Step 8.
  - **JSON markers**: phrases containing "json" / "as json" / "in json" / "give me json" → add `--json` to the resolved flag set.
  - **Single bare token**: see Step 5.

If after parsing the resolved flag set is empty AND the input is not `init`, route to the `--help` branch above.

### Step 5 — Single-token shortcut (FR-009 sub-bullet)

If `$ARGUMENTS` is **exactly one token** AND that token is **not** in the verb set `{init, list, exclusions, current-phase, help, version}` AND a directory whose name matches that token exactly exists as a **direct subdirectory** of the current working directory:

1. Set the resolved flag set to `[--source <token>, --target-extension _net, --target <token>_net]`.
2. Emit ONE confirmation prompt: `Init with source=<token>, extension=_net, target=<token>_net? (yes/no)`.
3. On affirmative reply, proceed to Step 6 with the derived flag set.
4. On non-affirmative reply, stop without invoking the binary.

If the single token does not match an existing direct subdirectory and is not a verb, fall through to the `--help` branch in Step 4.

### Step 6 — Destructive-operation gate (FR-012 / FR-013 / FR-014)

Detect destructive intent if **either**:

- `$ARGUMENTS` (case-insensitive) contains any of the closed marker word list: `force`, `delete`, `rebuild`, `reset`, `recreate`, `reinitialise`, `reinitialize`, `nuke`, `wipe`, `redo`.
- The resolved flag set contains both `--FORCE` and `--DELETE-EXISTING` (literal pair).

If destructive:

- Compute the absolute path to `<cwd>/.D2NET`.
- Look in the **current conversation transcript** for a structured marker matching `[D2NET-init: destructive-confirmed = <abs path> @ <ISO timestamp>]` for that path. If a marker is found AND has not been dropped by auto-compaction, treat the path as already-confirmed and add `--FORCE --DELETE-EXISTING` to the resolved flag set without prompting.
- Otherwise, emit ONE confirmation prompt naming the absolute path and the action: `This will delete the existing <abs path> workspace and rebuild it from scratch. Proceed? (yes/no)`.
- Wait for an affirmative reply (`yes`, `y`, `confirmed`, `proceed`).
- On affirmative, append a structured marker line to the response (e.g., on a line of its own: `[D2NET-init: destructive-confirmed = <abs path> @ <ISO timestamp>]`) so subsequent invocations in this conversation can detect the prior confirmation. Then add `--FORCE --DELETE-EXISTING` to the resolved flag set.
- On non-affirmative reply, stop without invoking the binary.

If the marker is absent from the surviving context (e.g., earlier turn was summarised by auto-compaction), re-prompt. Re-prompting is the safe failure mode; the skill MUST NOT use filesystem persistence to compensate.

If the input is **not destructive**, never add `--FORCE --DELETE-EXISTING` to the resolved flag set, even when the binary's `WorkspaceAlreadyExists` (3) error suggests it later.

### Step 7 — Augment (FR-007)

If the resolved flag set is `[--help]` or `[--version]`, skip this step entirely.

For inspection (`--list` / `--Exclusions` / `--current-phase`) skip this step entirely as well — inspection commands are non-interactive by construction; the binary rejects `--non-interactive` on inspection invocations as a usage error.

Otherwise (init mode):

- Always add `--non-interactive` if absent.
- If **neither** `--accept-suggested-exclusions` nor any `--exclude` flag is present, add `--accept-suggested-exclusions` AND emit a one-line warning: "(no `--exclude` flags supplied; auto-accepting suggested exclusions to avoid `InteractivePromptCancelled`)".

### Step 8 — Invoke

Build the command. Two forms based on Step 2's discovery:

- Binary path resolved: `<binary path> <flag1> <flag2> …`
- Fallback: `dotnet run --project tools/d2net/src/D2Net.Init -- <flag1> <flag2> …`

For `--help` / `--version` short-circuits, the resolved flag set is exactly `[--help]` or `[--version]`. Surface stdout/stderr verbatim and stop (skip Steps 9–11).

For all other invocations, run via the Bash tool. Capture stdout, stderr, and exit code separately.

### Step 9 — Surface results (FR-015 / FR-017 / FR-018)

Branch on exit code:

- **Exit 0** (success):
  - If `--json` is in the resolved flag set: surface stdout **verbatim regardless of size**. The 50-line truncation does NOT apply to JSON output (FR-017 second paragraph).
  - Otherwise (plain text): if stdout is ≤ 50 lines, surface verbatim. Otherwise truncate to first 50 lines plus footer:
    ```
    ... and N more lines (total: M). Reply 'show all' to see everything, or 'filter <substring>' to narrow.
    ```
    Preserve the full stdout in the conversation history so 'show all' / 'filter' follow-ups don't need re-invocation.
- **Exit non-zero**: surface the binary's **stderr verbatim**, then a single line `Exit code: <code>`. Do NOT swallow errors.

### Step 10 — Recap (FR-015)

Apply only on **exit 0 + init mode** (resolved set has no inspection flag).

Parse the binary's stdout summary (the `d2net-init: workspace ready at … / Source / Target / PGLite data tree / Excluded dirs / Dart files / Created at` block) and append a one-line Codex-side recap after the binary's output:

```
Workspace at <path>; indexed <N> dart files; bridge port <port>.
```

The recap is supplementary — it MUST NOT contradict the binary's own output.

### Step 11 — Hint (FR-015 / FR-016)

For specific exit codes, append a hint after the surfaced output:

- **Exit 5 (`BridgePortInUse`)**: suggest the next port. If the user supplied `--bridge-port <X>`, suggest `<X+1>` (when ≤ 65535) else 54401. Format:
  ```
  Bridge port <X> is in use. Retry with --bridge-port <X+1>? (yes/no)
  ```
  On affirmative reply, re-invoke from Step 8 with the new port. Track consecutive collisions in this invocation chain — after 3 collisions, stop suggesting and ask the user to specify a port manually.
- **Exit 8 (`DbOpenFailed`) when stderr contains `pglite_init_failed`**: surface the binary's `--FORCE --DELETE-EXISTING` recovery hint verbatim. Do NOT auto-run it. The user must explicitly use a destructive verb in a follow-up message to trigger the rebuild path (Step 6).
- **Exit 10 (`NodeMissing`)**: surface the binary's "install Node.js LTS" stderr verbatim. Stop.
- **Exit 11 (`BridgeBundleMissing`)**: surface the binary's "reinstall d2net-init" stderr verbatim. Stop.
- **Exit 2 (`WrongCwd`)**: remind the user the skill operates against the current working directory. Offer to inspect directory contents to help diagnose. Do NOT walk up.
- **Exit 3 (`WorkspaceAlreadyExists`)**: surface the binary's "use --FORCE --DELETE-EXISTING" hint. Do NOT silently retry — the user must use a destructive verb to trigger Step 6.

For other non-zero exit codes, surface stderr only; no specific hint.

## Examples

### Init from a clean repo

```
User: /D2NET-init source=glp_runtime extension=_net target=glp_runtime_net
```

→ Resolved flags: `--source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive`.

### Single-token shortcut

```
User: /D2NET-init glp_runtime
Skill: Init with source=glp_runtime, extension=_net, target=glp_runtime_net? (yes/no)
User: yes
```

→ Resolved flags: `--source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive`.

### Inspection

```
User: /D2NET-init list
```

→ Resolved flags: `--list`. Plain-text output truncated at 50 lines.

```
User: /D2NET-init exclusions in json
```

→ Resolved flags: `--Exclusions --json`. Output surfaced verbatim regardless of size.

### Force rebuild (destructive)

```
User: /D2NET-init force rebuild
Skill: This will delete the existing <abs path>/.D2NET workspace and rebuild it from scratch. Proceed? (yes/no)
User: yes
Skill: [D2NET-init: destructive-confirmed = <abs path>/.D2NET @ <ISO time>]
```

→ Resolved flags include `--FORCE --DELETE-EXISTING`.

### Help / version

```
User: /D2NET-init
User: /D2NET-init help
User: /D2NET-init version
```

→ Each form short-circuits Steps 5–11; surface the binary's `--help` or `--version` output verbatim.

### Pass-through

```
User: /D2NET-init --source glp_runtime --target-extension _net --target glp_runtime_net --bridge-port 55001
```

→ Pass-through. Skill adds `--non-interactive` and `--accept-suggested-exclusions` (no `--exclude` was supplied), runs the binary.
