---
name: "D2NET-scaffold"
description: "Wrap the d2net-scaffold CLI: locate binary, parse intent (empty = run scaffold; markers like 'json' / 'force delete target' translate to flags), confirm before destructive operations AND drive the binary's interactive prompt, run, and surface results."
argument-hint: "Empty runs the scaffold operation. Use 'help' for binary --help. Use 'force delete target' (or '--FORCE --DELETE-TARGET') for destructive override. Pass --json for machine-readable output."
compatibility: "Requires tools/d2net/src/D2Net.Scaffold/ in the repo and a built or buildable d2net-scaffold binary. Node.js >= 20 required at runtime (the binary's PGLite bridge subprocess). A populated .D2NET/ workspace at CWD (created by /D2NET-init) is required for any non-help invocation."
metadata:
  author: "GLPNET"
  source: "specs/010-scaffold-skill/spec.md"
user-invocable: true
disable-model-invocation: false
---


## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Goal

Run the `d2net-scaffold` CLI on behalf of the user. The binary takes no positional arguments — its inputs are the workspace populated by an earlier `/D2NET-init`. Translate freeform input (natural-language markers or raw flags) into the binary's flag set, locate the binary, confirm before destructive operations or `dotnet build`, drive the binary's own interactive `yes/no` prompt with `yes\n` when running destructively, invoke once, and surface the result. Empty input runs the scaffold operation in default mode (NOT help). Unrecognized non-empty input routes to `--help`.

The procedure below is the contract from `specs/010-scaffold-skill/contracts/skill-contract.md`. Follow each step in order.

## Operating Constraints

- **NEVER pass `--FORCE --DELETE-TARGET`** to the binary unless the destructive-operation gate (Step 5) was completed affirmatively in this conversation OR the user supplied the literal flag pair AND completed Step 5.
- **NEVER drive `yes\n` into the binary's stdin** without an affirmative reply to Step 5's confirmation prompt (or a marker showing the prompt was answered earlier in this conversation for the same target absolute path).
- **NEVER run `dotnet build`** without an affirmative reply to Step 3's confirmation prompt in this conversation.
- **NEVER walk up** to find `.git/` or change the working directory the user invoked from.
- **NEVER modify** `.D2NET/D2NET-Settings.json` or any workspace database file directly. Only the `d2net-init` and `d2net-scaffold` binaries write workspace state; this skill is invocation-only.
- **NEVER invent CLI flags** the binary does not support. The full flag list is `--help`, `--version`, `--json`, `--bridge-port <N>`, `--FORCE --DELETE-TARGET`. Anything else returns argument error (exit 1).
- **AT MOST ONE** `dotnet build` per skill invocation. If the build fails, surface its stderr and stop.
- **AT MOST ONE** binary invocation per skill invocation in non-destructive flows. The destructive flow may bridge multiple turns (skill-layer confirm in turn N, binary invocation in turn N+1) but still produces exactly one binary process.

## Procedure

### Step 1 — Read user input

The user's freeform argument string is in the `$ARGUMENTS` block above. Empty input is valid and means "run the scaffold operation in default mode" — do NOT route it to help (clarified Q4).

### Step 2 — Locate the binary (FR-004 / FR-005)

Search in this exact order, stopping at the first hit. On Windows the binary is `d2net-scaffold.exe`; on macOS / Linux it is `d2net-scaffold`.

1. `tools/d2net/src/D2Net.Scaffold/bin/Release/net8.0/d2net-scaffold[.exe]`.
2. `tools/d2net/src/D2Net.Scaffold/bin/Debug/net8.0/d2net-scaffold[.exe]`.
3. Fallback to `dotnet run --project tools/d2net/src/D2Net.Scaffold -- <args>`. If selecting this path, emit a one-line note: "Using `dotnet run` fallback (slower). Run `dotnet build tools/d2net/D2Net.sln` once to enable the direct-binary path."

Use the Bash tool with `ls` / `test -f` (or `Test-Path` via PowerShell) to verify file presence before adopting a path. Resolve `tools/d2net/src/D2Net.Scaffold/bin/<config>/net8.0/d2net-scaffold.exe` relative to the current working directory.

If neither Release nor Debug binary exists AND `dotnet --version` fails (no .NET SDK on PATH), emit a single message naming all three missing prerequisites and stop. Do not invoke anything (FR-005).

### Step 3 — Detect missing or stale binary (FR-006)

If Step 2 selected the fallback path because **no binary exists**, the binary is **missing**. Otherwise check **staleness**: compute `mtime(binary)` and `max(mtime(.cs file under tools/d2net/src/D2Net.Scaffold))`. (Feature 012 removed the bundled `pgbridge/` subtree; staleness check no longer needs to exclude it.)

- If the binary is missing OR the binary's mtime is older than the newest `.cs` file under `tools/d2net/src/D2Net.Scaffold`, emit ONE confirmation prompt:
  - Missing: `d2net-scaffold binary is missing at <path> — build now? (yes/no)`
  - Stale: `d2net-scaffold binary may be stale (newest source is <path> modified <time>; binary built <time>) — rebuild now? (yes/no)`
- Wait for an affirmative reply (`yes`, `y`, `confirmed`, `proceed`).
- On affirmative, run `dotnet build tools/d2net/D2Net.sln` via Bash, surface its output, and on success continue with the original `/D2NET-scaffold` request.
- On any non-affirmative reply, stop without invoking the binary.

If the user previously replied with a phrase containing "don't ask" / "skip staleness" / "ignore stale" in this conversation, suppress the **stale-only** prompt for the rest of the conversation (still prompt on missing binary).

Use the conversation transcript to detect the opt-out phrase from prior turns.

### Step 4 — Parse user intent (FR-008 / FR-009 / FR-010 / FR-010a / FR-011)

Branch on `$ARGUMENTS` in this **precedence order** (first match wins):

1. **Empty** → set resolved flag set to `[]` (default scaffold mode). Skip Step 5 (no destructive markers possible). Proceed to Step 6 with empty flag set. (Clarified Q4.)
2. **Pure `help` / `--help` / `-h` token** (single recognized help verb, possibly with surrounding whitespace) → set resolved flag set to `[--help]`. Short-circuit: skip Steps 5, 7, 8; surface the binary's help text via Step 6 (invoke), stop.
3. **Pure `version` / `--version` token** → set resolved flag set to `[--version]`. Short-circuit similarly.
4. **All tokens flag-style** (every token is `--<flag>` or a value following one) → treat as pass-through. Initial resolved flag set = the user's tokens verbatim. Proceed to Step 5 (destructive gate may still apply if `--FORCE --DELETE-TARGET` is in the literal flags).
5. **Mixed** (some flag-style + some natural-language) → take the raw flags verbatim into the resolved flag set; derive only the un-supplied flags from the natural-language portion using the grammar below.
6. **Pure natural-language with at least one recognized marker** → derive flags via the grammar:
   - `scaffold` verb → no extra flag (default scaffold mode; semantically same as empty).
   - **JSON markers**: phrases containing `json` / `as json` / `in json` / `give me json` / `structured` → add `--json`.
   - **Bridge-port markers**: phrases like `bridge port 55001` / `on port 55001` / `bridge-port=55001` → add `--bridge-port 55001`.
   - **Destructive markers** (full list under Step 5): mark the bundle as destructive; the gate fires in Step 5.
7. **Pure natural-language with NO recognized verb / marker / flag** (and non-empty) → set resolved flag set to `[--help]` (FR-010a, clarified Q5). Short-circuit: skip Steps 5, 7, 8; surface the binary's help text via Step 6, stop. Do NOT silently run the binary against unrecognized tokens.

Marker grammars (case-insensitive):

- **JSON marker tokens / phrases**: `json`, `as json`, `in json`, `give me json`, `structured`.
- **Bridge-port marker forms**: `bridge port <N>`, `on port <N>`, `bridge-port=<N>`, `bridge-port <N>`. `<N>` is a base-10 integer 1–65535. Translate to literal `--bridge-port <N>`.
- **Destructive marker word list**: `force`, `delete`, `rebuild`, `reset`, `recreate`, `reinitialise`, `reinitialize`, `nuke`, `wipe`, `redo`. Any of these (case-insensitive) anywhere in `$ARGUMENTS` triggers Step 5.

If after parsing the resolved flag set is empty AND the input was empty, the binary runs in default scaffold mode. (This is the canonical MVP form.)

### Step 5 — Destructive-operation gate (FR-012 / FR-013 / FR-014 / FR-015 / FR-016)

Detect destructive intent if **either**:

- `$ARGUMENTS` (case-insensitive) contains any of the closed marker word list: `force`, `delete`, `rebuild`, `reset`, `recreate`, `reinitialise`, `reinitialize`, `nuke`, `wipe`, `redo`.
- The resolved flag set contains BOTH `--FORCE` AND `--DELETE-TARGET` (literal pair, in any order).

If only ONE of `--FORCE` / `--DELETE-TARGET` is present and the other is absent (per FR-016 unbalanced-pair pass-through), do NOT trigger the gate; the binary's `ArgParser` will reject the unbalanced pair with exit 1. Surface that error in Step 7 unchanged.

If destructive:

- Compute the absolute path to the **target directory** by reading `<cwd>/.D2NET/D2NET-Settings.json` and resolving its `target` field against `<cwd>` (the repo root). This is the **cache key** (clarified Q2 — target absolute path, NOT workspace path).
  - If the workspace settings file is missing, the binary will exit 22 (`ScaffoldWorkspaceMissing`); surface that and skip the destructive gate — there's nothing to destroy.
- Look in the **current conversation transcript** for a structured marker matching `[D2NET-scaffold: destructive-confirmed = <abs target path> @ <ISO timestamp>]` for that exact path. If a marker is found AND has not been dropped by auto-compaction, treat the path as already-confirmed at the skill layer — skip the skill-layer prompt but ALWAYS still drive the binary's interactive prompt in Step 6 (the binary re-prompts every invocation by design).
- Otherwise, emit ONE skill-layer confirmation prompt naming the absolute target path and the action: `This will recursively delete <abs target path> and all of its contents. Proceed? (yes/no)`.
- Wait for an affirmative reply (`yes`, `y`, `confirmed`, `proceed`).
- On affirmative, append a structured marker line to the response on a line of its own: `[D2NET-scaffold: destructive-confirmed = <abs target path> @ <ISO timestamp>]` so subsequent invocations in this conversation can detect the prior confirmation. Then ensure `--FORCE --DELETE-TARGET` is in the resolved flag set (add both if absent).
- On non-affirmative reply, stop without invoking the binary. Do NOT write the marker.

If the marker is absent from the surviving context (e.g., earlier turn was summarised by auto-compaction), re-prompt. Re-prompting is the safe failure mode; the skill MUST NOT use filesystem persistence to compensate.

If the input is **not destructive**, never add `--FORCE --DELETE-TARGET` to the resolved flag set, even when the binary's `ScaffoldTargetNotEmptyAndNotManaged` (24) error suggests it later in Step 8.

### Step 6 — Invoke

Build the command. Two forms based on Step 2's discovery and Step 5's destructive flag state:

- **Non-destructive** (no `--FORCE --DELETE-TARGET` in the resolved flag set):
  - Direct binary: `<binary path> <flag1> <flag2> …`
  - Fallback: `dotnet run --project tools/d2net/src/D2Net.Scaffold -- <flag1> <flag2> …`
  - No stdin drive.

- **Destructive** (resolved flag set contains `--FORCE --DELETE-TARGET`):
  - POSIX (Bash on macOS / Linux): `echo yes | <binary path> --FORCE --DELETE-TARGET <other flags>`
  - PowerShell (Windows): `'yes' | <binary path> --FORCE --DELETE-TARGET <other flags>`
  - Fallback (POSIX): `echo yes | dotnet run --project tools/d2net/src/D2Net.Scaffold -- --FORCE --DELETE-TARGET <other flags>`
  - The skill MUST drive `yes\n` to stdin only AFTER Step 5 has resolved affirmatively; the binary will read it as the operator confirmation for its hard-safety-gate prompt (spec 009 FR-012a).
  - Surface the binary's prompt text along with the `yes` reply that was driven in the response, so the safety flow is auditable in the conversation transcript (FR-014).

For `--help` / `--version` short-circuits, the resolved flag set is exactly `[--help]` or `[--version]`. No augmentation, no destructive gate. Surface stdout/stderr verbatim and stop (skip Steps 7–8).

For all other invocations, run via the Bash tool. Capture stdout, stderr, and exit code separately.

### Step 7 — Surface results (FR-017 / FR-018 / FR-019)

Branch on exit code AND `--json` presence:

- **Exit 0 AND `--json` in resolved flag set** (JSON mode):
  - Surface stdout **verbatim regardless of size**. The 50-line truncation does NOT apply to JSON output.
  - **DO NOT append a Codex-side recap** (clarified Q1 — recap is suppressed entirely under `--json` so downstream tooling consumes the response cleanly).

- **Exit 0 AND `--json` NOT in resolved flag set** (plain-text mode):
  - If stdout is ≤ 50 lines, surface verbatim.
  - Otherwise truncate to first 50 lines plus footer:
    ```
    ... and N more lines (total: M). Reply 'show all' to see everything, or 'filter <substring>' to narrow.
    ```
    Preserve the full stdout in the conversation history so 'show all' / 'filter <substring>' follow-ups can be serviced from the transcript without re-invoking the binary (clarified Q3 — these are free-text replies, not skill sub-commands).
  - **Append a Codex-side recap** on the next line (FR-017). Parse the binary's stdout summary block (target path, files copied, working dirs created, dart_files rows updated, wall-clock duration) and emit:
    ```
    Target at <path>; <N> files copied; <M> working directories created; <K> dart_files rows updated; <T>s wall-clock.
    ```
    The recap is supplementary — it MUST NOT contradict the binary's own output. If the binary's stdout summary block cannot be parsed, omit the recap silently rather than inventing values.

- **Exit non-zero**:
  - Surface the binary's **stderr verbatim**, then a single line `Exit code: <code>`. Do NOT swallow errors.
  - When `--json` is in the resolved flag set, surface stderr verbatim as well — JSON-mode stderr is part of the contract for downstream tooling that wants to inspect failures.
  - Do NOT append the success recap.
  - Apply the per-exit-code hint from Step 8.

### Step 8 — Hint pass-through for known exit codes (FR-019)

For specific exit codes, append a one-line hint after the surfaced output:

- **Exit 22 (`ScaffoldWorkspaceMissing`)**: `No .D2NET/ workspace at this directory. Run /D2NET-init first.`
- **Exit 23 (`ScaffoldSourceMissing`)**: surface the binary's named path; offer to inspect the parent directory for typo-style help (e.g., "Should I list `<parent dir>` to confirm the source name?"). Do NOT walk up beyond the parent.
- **Exit 24 (`ScaffoldTargetNotEmptyAndNotManaged`)**: `The target tree contains content not produced by a prior scaffold run. Reply '/D2NET-scaffold force delete target' to overwrite (a confirmation prompt will follow).` Do NOT auto-retry with `--FORCE --DELETE-TARGET`.
- **Exit 25 (`ScaffoldWorkdirCollision`)**: surface the binary's listed offending paths; the user must resolve manually (rename or remove the conflicting `__<basename>` artefact in the source tree). No auto-retry.
- **Exit 26 (`ScaffoldCopyError`)**: `Filesystem error during scaffold. The binary's idempotency property means re-running typically reconciles a half-state.`
- **Exit 27 (`ScaffoldDbWriteFailed`)**: surface the binary's stderr; no auto-recovery hint. The cause is workspace-database corruption or PGLite bridge failure, both of which need operator diagnosis.
- **Exit 28 (`ScaffoldWorkspaceLocked`)**: `Another /D2NET-init or /D2NET-scaffold invocation holds the workspace lock. Retry shortly.` Do NOT auto-retry.
- **Exit 29 (`ScaffoldOperatorCancelledTargetDeletion`)**: treat as a clean stop; surface the binary's stderr noting no changes were made. Suggest running again without destructive markers if the target tree was actually intended to remain.
- **Exit 1 (`ArgumentError`)**: surface the binary's stderr; if it mentions `--FORCE` / `--DELETE-TARGET`, append: `--FORCE and --DELETE-TARGET must be supplied together. Use the destructive-marker form (e.g., 'force delete target') for the safer skill-layer flow.`

For other non-zero exit codes, surface stderr only; no specific hint.

## Examples

### Default scaffold (the MVP one-liner)

```
User: /D2NET-scaffold
```

→ Resolved flags: `[]` (empty). Binary runs in default scaffold mode. On exit 0, recap appended:
`Target at <path>; <N> files copied; <M> working directories created; <K> dart_files rows updated; <T>s wall-clock.`

### Default scaffold with JSON

```
User: /D2NET-scaffold as json
```

→ Resolved flags: `[--json]`. Binary runs and emits JSON. Stdout surfaced verbatim. **No recap appended** (clarified Q1).

### Help and version

```
User: /D2NET-scaffold help
User: /D2NET-scaffold --help
User: /D2NET-scaffold version
User: /D2NET-scaffold --version
```

→ Each form short-circuits Steps 5, 7, 8; surface the binary's `--help` or `--version` output verbatim.

### Unrecognized non-empty input

```
User: /D2NET-scaffold please scaffold quickly
```

→ Resolved flags: `[--help]` (FR-010a, clarified Q5). User sees help text; can re-invoke with a recognized form. Skill does NOT run the binary against the unrecognized tokens.

### Force delete target (destructive)

```
User: /D2NET-scaffold force delete target
Skill: This will recursively delete <abs target path> and all of its contents. Proceed? (yes/no)
User: yes
Skill: [D2NET-scaffold: destructive-confirmed = <abs target path> @ <ISO time>]
       <invokes: echo yes | d2net-scaffold.exe --FORCE --DELETE-TARGET>
       <surfaces binary stdout including its own prompt and the 'yes' it received>
       <recap if exit 0 and not --json>
```

→ Resolved flags include `--FORCE --DELETE-TARGET` AND `yes\n` is piped to stdin. **Two confirmations total**: skill layer + binary's interactive prompt.

### Pass-through

```
User: /D2NET-scaffold --json --bridge-port 55001
```

→ Pass-through. Skill runs `d2net-scaffold.exe --json --bridge-port 55001` directly. JSON suppresses recap.

### Pass-through with destructive flag pair

```
User: /D2NET-scaffold --FORCE --DELETE-TARGET
Skill: This will recursively delete <abs target path> and all of its contents. Proceed? (yes/no)
User: yes
Skill: <same flow as 'force delete target' example above>
```

→ The literal flag pair triggers Step 5 just like the natural-language form. Stdin drive (`yes\n`) still applies.

### Unbalanced flag pair (FR-016)

```
User: /D2NET-scaffold --FORCE
```

→ Pass-through. The single flag does NOT trigger Step 5 (only one half of the pair). The binary's `ArgParser` rejects the unbalanced flag with exit 1; Step 8 surfaces the argument-error hint.

### Workspace missing

```
User: /D2NET-scaffold
<binary exits 22>
Skill: <binary stderr verbatim>
       Exit code: 22
       No .D2NET/ workspace at this directory. Run /D2NET-init first.
```

### Idempotent re-invocation (no changes)

```
User: /D2NET-scaffold
<binary exits 0; stdout reports 0 net additions, 0 net removals>
Skill: <binary stdout verbatim>
       Target at <path>; 0 files copied; 0 working directories created; 0 dart_files rows updated; <T>s wall-clock.
```

### Re-invocation against same target after destructive confirm (no skill re-prompt; binary re-prompted)

```
User: /D2NET-scaffold force delete target   # earlier in same conversation: confirmed
Skill: <reads cached marker for the same abs target path from transcript>
       <skips skill-layer prompt>
       <invokes: echo yes | d2net-scaffold.exe --FORCE --DELETE-TARGET>
       <binary re-prompts; piped 'yes' satisfies it>
```
