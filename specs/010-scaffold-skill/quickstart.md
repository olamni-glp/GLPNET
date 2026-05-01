# Quickstart — `/D2NET-scaffold` Skill

**Feature**: `010-scaffold-skill` — see [spec.md](spec.md), [plan.md](plan.md), [contracts/skill-contract.md](contracts/skill-contract.md)

## Prerequisites

- A clone of GLPNET at a recent enough revision to include the spec-009 source-tree-mirror `d2net-scaffold` AND the spec-005/006 PGLite-backed `d2net-init`.
- `.NET 8 SDK` installed (the skill triggers `dotnet build` on confirmation; users running only a pre-built binary need only the .NET runtime).
- `Node.js >= 20` on PATH (the underlying binary spawns a Node bridge subprocess at run time — see spec 005 R8).
- A populated `.D2NET/` workspace at the current working directory, created by an earlier `/D2NET-init` invocation. Without it, the binary returns `ScaffoldWorkspaceMissing` (22).
- Claude Code itself, with the repo open as the working directory of the session.

After cloning, the skill is automatically discoverable: Claude Code's loader reads `.claude/skills/D2NET-scaffold/SKILL.md` on session start and registers `/D2NET-scaffold` as a slash command.

## 1. Run scaffold from inside Claude Code (the MVP one-liner)

After `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net` has populated the workspace, type:

```
/D2NET-scaffold
```

Expected behaviour:

1. Claude locates the binary at `tools/d2net/src/D2Net.Scaffold/bin/Release/net8.0/d2net-scaffold.exe` (preferring Release; falls back to Debug; falls back to `dotnet run --project ...` with a one-line "slower fallback" notice).
2. If the binary is missing or stale, Claude prompts to build it and waits for a `yes` reply.
3. Claude resolves the flag set to `[]` (empty input → default scaffold mode; clarified Q4).
4. Claude invokes the binary with no flags, captures stdout/stderr, and surfaces the result.
5. On success Claude appends a one-line recap: `Target at <path>; <N> files copied; <M> working directories created; <K> dart_files rows updated; <T>s wall-clock.`

## 2. JSON output

```
/D2NET-scaffold as json
```

or equivalently:

```
/D2NET-scaffold --json
```

Claude resolves the flag set to `[--json]`, runs the binary, and surfaces the binary's structured JSON output **verbatim** with **no Claude-side recap appended** (clarified Q1). Downstream tooling like `jq`, `cat | jq`, or assertion-based smoke tests can consume the response cleanly.

## 3. Help and version

```
/D2NET-scaffold help
/D2NET-scaffold --help
/D2NET-scaffold version
/D2NET-scaffold --version
```

All four forms short-circuit Claude's parsing and surface the binary's `--help` / `--version` output verbatim. **Note**: empty `/D2NET-scaffold ` does NOT route to help — it runs the scaffold operation per Step 1. To see help, you must explicitly type a help token.

## 4. Re-scaffold idempotently

After the initial scaffold from Step 1, edit some `.glp` settings or change the exclusion list via `/D2NET-init --add-exclude` / `--remove-exclude`, then re-run:

```
/D2NET-scaffold
```

The binary re-reads the workspace settings (idempotent reconciliation per spec 009 FR-010 / FR-011) and brings the target tree into sync. The recap shows net additions / removals so you can see what changed.

If nothing has changed since the last run, the recap reports `0 files copied; 0 working directories created; 0 dart_files rows updated`.

## 5. Force-delete-target flow

When the target tree was hand-created (or left over from a prior tool that scaffold doesn't recognise), the binary refuses the run with `ScaffoldTargetNotEmptyAndNotManaged` (24). To overwrite, type:

```
/D2NET-scaffold force delete target
```

Claude detects the destructive markers and emits a skill-layer confirmation prompt:

> "This will recursively delete `<abs target path>` and all of its contents. Proceed? (yes/no)"

Reply `yes` → Claude:
1. Records a structured marker in the conversation: `[D2NET-scaffold: destructive-confirmed = <abs target path> @ <ISO time>]`.
2. Invokes the binary as `echo yes | d2net-scaffold.exe --FORCE --DELETE-TARGET` (PowerShell: `'yes' | d2net-scaffold.exe --FORCE --DELETE-TARGET`).
3. The binary emits its OWN interactive prompt naming the absolute target path; the `yes` Claude piped to stdin satisfies that prompt.
4. The binary deletes the target tree and proceeds with a fresh scaffold.
5. Claude surfaces the binary's prompt text along with the driven `yes` so the safety flow is auditable.

Reply `no` (or anything non-affirmative) → Claude stops without invoking the binary.

The destructive path is remembered for the rest of this conversation; subsequent destructive invocations against the same target absolute path skip the SKILL-LAYER prompt — but ALWAYS still drive the binary's own prompt (the binary re-prompts every invocation by design).

## 6. Workspace missing

```
/D2NET-scaffold
```

If no `.D2NET/` workspace exists at the current working directory, the binary exits 22 (`ScaffoldWorkspaceMissing`). Claude surfaces the binary's stderr and appends:

> "No .D2NET/ workspace at this directory. Run /D2NET-init first."

Claude does NOT auto-invoke `/D2NET-init` — workspace creation is a deliberate operator action.

## 7. Pass-through of raw flags

If you already know the exact flag invocation you want, pass it verbatim:

```
/D2NET-scaffold --json --bridge-port 55001
```

Claude treats this as pass-through, runs `d2net-scaffold.exe --json --bridge-port 55001`, and (because `--json` is in play) suppresses the recap.

## 8. Pure unrecognized natural-language

```
/D2NET-scaffold please scaffold quickly
```

The tokens `please` / `quickly` match no recognized verb, marker, or flag. Per FR-010a (clarified Q5), Claude resolves the flag set to `[--help]` and surfaces the binary's help text. This signals to the user that the input was not interpreted and lets them re-invoke with a recognized form.

## 9. Output truncation and "show all" / "filter"

When the binary's plain-text stdout exceeds 50 lines (rare for scaffold — its summary is concise — but possible if the binary emits per-file lines for very large reconciliations), Claude truncates with the standard footer:

> "... and N more lines (total: M). Reply 'show all' to see everything, or 'filter <substring>' to narrow."

`show all` and `filter <substring>` are NOT skill sub-commands. They are free-text replies you type in your next turn; Claude reads the buffered stdout from the conversation transcript and emits the requested portion (clarified Q3). When the binary's response is in `--json` mode, no truncation is applied — JSON is always surfaced verbatim regardless of size.

## What to do when the skill misbehaves

- **Skill not registered**: confirm `.claude/skills/D2NET-scaffold/SKILL.md` exists in the repo. If you added it just now, restart Claude Code so the loader re-scans.
- **Wrong casing on Linux/macOS**: the directory must be exactly `D2NET-scaffold` (uppercase D, N, E, T; lowercase `scaffold`). On Windows this is cosmetic; on case-sensitive filesystems it matters.
- **Stale-binary loop**: if the staleness check fires on every invocation, build once with `dotnet build tools/d2net/D2Net.sln` and the loop stops until the next `.cs` edit. You can also opt out for the current session by replying "don't ask about staleness" at any staleness prompt.
- **Destructive prompt re-asked after `yes`**: the structured marker may have been dropped by auto-compaction. Re-confirming is safe; the binary's own prompt is the second guard. There is no filesystem persistence by design.
- **Binary's interactive prompt not driven**: if you see the binary's stdin-prompt text in the response but no `yes` was driven, either the skill-layer confirmation was not affirmative or the Bash tool's stdin redirection didn't take. Check that the response shows both (a) your `yes` reply at the skill layer AND (b) the structured `[D2NET-scaffold: destructive-confirmed = ...]` marker. If both are present and the binary still hangs, that's an implementation bug — file under spec 010.
