# Skill Contract — `.claude/skills/D2NET-scaffold/SKILL.md`

**Feature**: `010-scaffold-skill` — see [spec.md](../spec.md), [plan.md](../plan.md), [research.md](../research.md), [data-model.md](../data-model.md)

This document is the procedural contract Claude follows when the user invokes `/D2NET-scaffold`. The implementation phase MUST produce a `SKILL.md` whose body is functionally equivalent to this contract — every numbered step here MUST appear in the SKILL.md procedure with at most cosmetic prose differences. Adding extra steps requires a corresponding spec update; removing steps requires re-running `/speckit-clarify`.

## Frontmatter (verbatim contract)

```yaml
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
```

`name`, `user-invocable`, `disable-model-invocation` MUST be exactly as above. Other fields MAY be reworded for clarity but MUST preserve their semantics.

## Operating Constraints (verbatim contract)

The implementation MUST include this constraint block (or functionally equivalent prose):

- **NEVER pass `--FORCE --DELETE-TARGET`** to the binary unless the destructive-operation gate (Step 5) was completed affirmatively in this conversation OR the user supplied the literal flag pair AND completed Step 5.
- **NEVER drive `yes\n` into the binary's stdin** without an affirmative reply to Step 5's confirmation prompt (or a marker showing the prompt was answered earlier in this conversation for the same target absolute path).
- **NEVER run `dotnet build`** without an affirmative reply to Step 3's confirmation prompt in this conversation.
- **NEVER walk up** to find `.git/` or change the working directory the user invoked from.
- **NEVER modify** `.D2NET/D2NET-Settings.json` or any workspace database file directly. Only the `d2net-init` and `d2net-scaffold` binaries write workspace state; this skill is invocation-only.
- **NEVER invent CLI flags** the binary does not support. The full flag list is `--help`, `--version`, `--json`, `--bridge-port <N>`, `--FORCE --DELETE-TARGET`. Anything else returns argument error (exit 1).
- **AT MOST ONE** `dotnet build` per skill invocation. If the build fails, surface its stderr and stop.
- **AT MOST ONE** binary invocation per skill invocation in non-destructive flows. The destructive flow may bridge multiple turns (skill-layer confirm in turn N, binary invocation in turn N+1) but still produces exactly one binary process.

## Procedure (numbered steps)

### Step 1 — User Input

Echo the standard `## User Input` block with `$ARGUMENTS` placeholder. Match the sibling-skill convention.

### Step 2 — Locate the binary (FR-004 / FR-005)

Search in this exact order, stopping at the first hit:

1. `tools/d2net/src/D2Net.Scaffold/bin/Release/net8.0/d2net-scaffold.exe` (Windows) or `…/d2net-scaffold` (other).
2. `tools/d2net/src/D2Net.Scaffold/bin/Debug/net8.0/d2net-scaffold.exe` (or platform equivalent).
3. Fallback: `dotnet run --project tools/d2net/src/D2Net.Scaffold -- <args>`. Inform the user this is slower and recommend `dotnet build` once.

If neither binary exists AND `dotnet` is not on PATH, emit a single message naming the missing prerequisites and stop without running anything (FR-005). No binary invocation in this case.

Resolve all paths relative to the current working directory.

### Step 3 — Detect missing or stale binary (FR-006)

If Step 2 selected the fallback path because **no binary exists**, the binary is **missing**. Otherwise check **staleness**: compute `mtime(binary)` and `max(mtime(.cs file under tools/d2net/src/D2Net.Scaffold))` excluding the `pgbridge/` subtree.

- If the binary is missing OR the binary's mtime is older than the newest `.cs` file under `tools/d2net/src/D2Net.Scaffold` (excluding `pgbridge/**`), emit ONE confirmation prompt:
  - Missing: `d2net-scaffold binary is missing at <path> — build now? (yes/no)`
  - Stale: `d2net-scaffold binary may be stale (newest source is <path> modified <time>; binary built <time>) — rebuild now? (yes/no)`
- Wait for an affirmative reply (`yes`, `y`, `confirmed`, `proceed`).
- On affirmative, run `dotnet build tools/d2net/D2Net.sln` via Bash, surface its output, and on success continue with the original `/D2NET-scaffold` request.
- On any non-affirmative reply, stop without invoking the binary.

If the user previously replied with a phrase containing "don't ask" / "skip staleness" / "ignore stale" in this conversation, suppress the **stale-only** prompt for the rest of the conversation (still prompt on missing binary).

Use the conversation transcript to detect the opt-out phrase from prior turns.

### Step 4 — Parse user intent (FR-008 / FR-009 / FR-010 / FR-010a / FR-011)

Branch on `$ARGUMENTS` in this precedence order:

1. **Empty** → set resolved flag set to `[]` (default scaffold mode). Skip Steps 5 (destructive gate doesn't apply: empty input has no destructive markers). Proceed to Step 6 with empty flag set. (Clarified Q4.)
2. **Pure `help` / `--help` / `-h` token** → set resolved flag set to `[--help]`. Short-circuit: skip Steps 5–8, surface the binary's help text via Step 6 (invoke), stop.
3. **Pure `version` / `--version` token** → set resolved flag set to `[--version]`. Short-circuit similarly.
4. **All tokens flag-style** (every token is `--<flag>` or a value following one) → treat as pass-through. Initial resolved flag set = the user's tokens verbatim. Proceed to Step 5 (destructive gate may still apply if `--FORCE --DELETE-TARGET` is in the literal flags).
5. **Mixed** (some flag-style + some natural-language) → take the raw flags verbatim into the resolved flag set; derive only the un-supplied flags from the natural-language portion using the grammar below.
6. **Pure natural-language** with at least one recognized marker → derive flags via the grammar:
   - `scaffold` → no extra flag (default scaffold mode; semantically same as empty).
   - JSON markers: phrases containing `json` / `as json` / `in json` / `give me json` / `structured` → add `--json`.
   - Bridge-port markers: phrases like `bridge port 55001` / `on port 55001` / `bridge-port=55001` → add `--bridge-port 55001`.
   - Destructive markers (full list under Step 5): mark the bundle as destructive; the gate fires in Step 5.
7. **Pure natural-language** with NO recognized verb / marker / flag (and non-empty) → set resolved flag set to `[--help]` (FR-010a, clarified Q5). Short-circuit: skip Steps 5, 7, 8; surface the binary's help text via Step 6, stop.

If after parsing the resolved flag set is empty AND the input was empty, the binary runs in default scaffold mode. (This is the canonical MVP form.)

### Step 5 — Destructive-operation gate (FR-012 / FR-013 / FR-014 / FR-015 / FR-016)

Detect destructive intent if **either**:

- `$ARGUMENTS` (case-insensitive) contains any of the closed marker word list: `force`, `delete`, `rebuild`, `reset`, `recreate`, `reinitialise`, `reinitialize`, `nuke`, `wipe`, `redo`.
- The resolved flag set contains both `--FORCE` and `--DELETE-TARGET` (literal pair, in any order).

If only ONE of `--FORCE` / `--DELETE-TARGET` is present and the other is absent (per FR-016), do NOT trigger the gate; the binary's `ArgParser` will reject the unbalanced pair with exit 1. Surface that error in Step 7 unchanged.

If destructive:

- Compute the absolute path to the **target directory** by reading `<cwd>/.D2NET/D2NET-Settings.json` and resolving its `target` field against `<cwd>`. (If the workspace is missing, the binary will exit 22; surface that and skip the destructive gate — there's nothing to destroy.)
- Look in the **current conversation transcript** for a structured marker matching `[D2NET-scaffold: destructive-confirmed = <abs target path> @ <ISO timestamp>]` for that exact path. If a marker is found AND has not been dropped by auto-compaction, treat the path as already-confirmed at the skill layer — skip the skill-layer prompt but ALWAYS still drive the binary's interactive prompt in Step 6.
- Otherwise, emit ONE skill-layer confirmation prompt naming the absolute target path and the action: `This will recursively delete <abs target path> and all of its contents. Proceed? (yes/no)`.
- Wait for an affirmative reply (`yes`, `y`, `confirmed`, `proceed`).
- On affirmative, append a structured marker line to the response (on a line of its own: `[D2NET-scaffold: destructive-confirmed = <abs target path> @ <ISO timestamp>]`) so subsequent invocations in this conversation can detect the prior confirmation. Then ensure `--FORCE --DELETE-TARGET` is in the resolved flag set.
- On non-affirmative reply, stop without invoking the binary.

If the marker is absent from the surviving context (e.g., earlier turn was summarised by auto-compaction), re-prompt. Re-prompting is the safe failure mode; the skill MUST NOT use filesystem persistence to compensate.

If the input is **not destructive**, never add `--FORCE --DELETE-TARGET` to the resolved flag set, even when the binary's `ScaffoldTargetNotEmptyAndNotManaged` (24) error suggests it later in Step 8.

### Step 6 — Invoke

Build the command. Two forms based on Step 2's discovery and Step 5's destructive flag state:

- **Non-destructive** (no `--FORCE --DELETE-TARGET` in the resolved flag set): `<binary path> <flag1> <flag2> …` (or fallback: `dotnet run --project tools/d2net/src/D2Net.Scaffold -- <flag1> <flag2> …`). No stdin drive needed.
- **Destructive** (resolved flag set contains `--FORCE --DELETE-TARGET`): `echo yes | <binary path> --FORCE --DELETE-TARGET <other flags>` on POSIX, or `'yes' | <binary path> --FORCE --DELETE-TARGET <other flags>` via PowerShell on Windows. The skill MUST drive `yes\n` to stdin; the binary will read it as the operator confirmation for its hard-safety-gate prompt (spec 009 FR-012a).

For `--help` / `--version` short-circuits, the resolved flag set is exactly `[--help]` or `[--version]`. No augmentation, no destructive gate. Surface stdout/stderr verbatim and stop (skip Steps 7–8).

For all other invocations, run via the Bash tool. Capture stdout, stderr, and exit code separately.

### Step 7 — Surface results (FR-017 / FR-018 / FR-019)

Branch on exit code AND `--json` presence:

- **Exit 0 AND `--json` in resolved flag set**: surface stdout **verbatim regardless of size**. **DO NOT append a Claude-side recap** (clarified Q1). The 50-line truncation does NOT apply to JSON output.
- **Exit 0 AND `--json` NOT in resolved flag set** (plain-text mode): if stdout is ≤ 50 lines, surface verbatim. Otherwise truncate to first 50 lines plus footer:
  ```
  ... and N more lines (total: M). Reply 'show all' to see everything, or 'filter <substring>' to narrow.
  ```
  Preserve the full stdout in the conversation history so 'show all' / 'filter' follow-ups can be serviced from the transcript without re-invoking the binary. Then append a Claude-side recap on the next line (FR-017): `Target at <path>; <N> files copied; <M> working directories created; <K> dart_files rows updated; <T>s wall-clock.` (Parse the binary's stdout summary block to fill in the values.)
- **Exit non-zero**: surface the binary's **stderr verbatim**, then a single line `Exit code: <code>`. Apply the per-exit-code hint from Step 8. Do NOT swallow errors. Do NOT append the success recap. (When `--json` is in the resolved flag set, surface the stderr verbatim as well — JSON-mode stderr is part of the contract for downstream tooling that wants to inspect failures.)

### Step 8 — Hint pass-through for known exit codes (FR-019)

For specific exit codes, append a one-line hint after the surfaced output:

- **Exit 22 (`ScaffoldWorkspaceMissing`)**: `No .D2NET/ workspace at this directory. Run /D2NET-init first.`
- **Exit 23 (`ScaffoldSourceMissing`)**: surface the binary's path; offer to inspect the parent directory for typo-style help (e.g., "Should I list `<parent dir>` to confirm the source name?").
- **Exit 24 (`ScaffoldTargetNotEmptyAndNotManaged`)**: `The target tree contains content not produced by a prior scaffold run. Reply '/D2NET-scaffold force delete target' to overwrite (a confirmation prompt will follow).` Do NOT auto-retry with `--FORCE --DELETE-TARGET`.
- **Exit 25 (`ScaffoldWorkdirCollision`)**: surface the binary's listed offending paths; the user must resolve manually (rename or remove the conflicting `__<basename>` artefact in the source tree).
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

→ Resolved flags: `[]` (empty). Binary runs in default scaffold mode. On exit 0, recap appended.

### Default scaffold with JSON

```
User: /D2NET-scaffold as json
```

→ Resolved flags: `[--json]`. Binary runs and emits JSON. **No recap appended** (clarified Q1).

### Help and version

```
User: /D2NET-scaffold help
User: /D2NET-scaffold --help
User: /D2NET-scaffold version
User: /D2NET-scaffold --version
```

→ Each form short-circuits; surface the binary's `--help` or `--version` output verbatim.

### Unrecognized non-empty input

```
User: /D2NET-scaffold please scaffold quickly
```

→ Resolved flags: `[--help]` (FR-010a, clarified Q5). User sees help text; can re-invoke with recognized form.

### Force delete target (destructive)

```
User: /D2NET-scaffold force delete target
Skill: This will recursively delete <abs target path> and all of its contents. Proceed? (yes/no)
User: yes
Skill: [D2NET-scaffold: destructive-confirmed = <abs target path> @ <ISO time>]
       <invokes: echo yes | d2net-scaffold.exe --FORCE --DELETE-TARGET>
       <surfaces binary stdout including its own prompt and the 'yes' it received>
       <recap if exit 0>
```

→ Resolved flags include `--FORCE --DELETE-TARGET` AND `yes\n` is piped to stdin. Two confirmations total: skill layer + binary's interactive prompt.

### Pass-through

```
User: /D2NET-scaffold --json --bridge-port 55001
```

→ Pass-through. Skill runs `d2net-scaffold.exe --json --bridge-port 55001` directly. JSON suppresses recap.

### Workspace missing

```
User: /D2NET-scaffold
<binary exits 22>
Skill: <binary stderr verbatim>
       Exit code: 22
       No .D2NET/ workspace at this directory. Run /D2NET-init first.
```

### Re-invocation against existing scaffold-managed target (idempotent)

```
User: /D2NET-scaffold
<binary exits 0; stdout reports 0 net additions, 0 net removals>
Skill: <binary stdout verbatim>
       Exit code: 0
       Target at <path>; 0 files copied; 0 working directories created; 0 dart_files rows updated; <T>s wall-clock. (No changes since last scaffold run.)
```
