# Quickstart — `/D2NET-init` Skill

**Feature**: `006-d2net-init-skill` — see [spec.md](spec.md), [plan.md](plan.md), [contracts/skill-contract.md](contracts/skill-contract.md)

## Prerequisites

- A clone of GLPNET at a recent enough revision to include the spec-005 PGLite-backed `d2net-init` (v0.2.0).
- `.NET 8 SDK` installed (the skill triggers `dotnet build` on confirmation; users running only a pre-built binary need only the .NET runtime).
- `Node.js >= 20` on PATH (the underlying binary spawns a Node bridge subprocess at run time — see spec 005 R8).
- Claude Code itself, with the repo open as the working directory of the session.

After cloning, the skill is automatically discoverable: Claude Code's loader reads `.claude/skills/D2NET-init/SKILL.md` on session start and registers `/D2NET-init` as a slash command.

## 1. Initialise a workspace from inside Claude Code

Open Claude Code in the repo root. Type:

```
/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net
```

Expected behaviour:

1. Claude locates the binary in `tools/d2net/src/D2Net.Init/bin/Release/net8.0/d2net-init.exe` (preferring Release; falls back to Debug; falls back to `dotnet run --project ...` with a one-line "slower fallback" notice).
2. If the binary is missing or stale, Claude prompts to build it and waits for a `yes` reply.
3. Claude derives the resolved flag set: `--source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive`.
4. Claude invokes the binary, captures stdout/stderr, and surfaces the result.
5. On success Claude appends a one-line recap: "Workspace at `<path>`; indexed `<N>` dart files; bridge port `<port>`."

## 2. Natural-language form

```
/D2NET-init initialise the workspace for glp_runtime targeting glp_runtime_net with extension _net
```

Same outcome as Step 1. Claude derives the same flag set from the natural-language phrasing.

## 3. Single-token shortcut

```
/D2NET-init glp_runtime
```

Claude detects that `glp_runtime/` exists as a direct subdirectory and proposes the conventional defaults:

> Init with `source=glp_runtime, extension=_net, target=glp_runtime_net`? (yes/no)

Reply `yes` → Claude invokes the binary with the derived flags. Reply `no` → stop.

## 4. Inspection

```
/D2NET-init list
/D2NET-init exclusions
/D2NET-init exclusions in json
/D2NET-init current phase
```

Plain-text outputs longer than 50 lines are truncated with a `... and N more lines` footer; reply `show all` or `filter <substring>` to recover the full output without re-invoking the binary. JSON outputs are surfaced verbatim regardless of size.

## 5. Force-rebuild flow

```
/D2NET-init force rebuild
```

Claude detects the destructive marker `force` and emits a confirmation prompt naming the absolute path:

> "This will delete the existing `D:\repo\.D2NET` workspace and rebuild it from scratch. Proceed? (yes/no)"

Reply `yes` → Claude adds `--FORCE --DELETE-EXISTING` and proceeds. Reply `no` → stop. The destructive path is remembered for the rest of this conversation; subsequent destructive invocations against the same `.D2NET/` skip the prompt.

## 6. Help

```
/D2NET-init
/D2NET-init help
/D2NET-init --help
```

All three forms invoke the binary's `--help` and surface the result.

## 7. Recovery from bridge-port collision

If the user's machine has the default port `54400` already bound (e.g., by a stale background bridge or an unrelated local Postgres):

```
/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net
```

The binary exits with code `5` (`BridgePortInUse`). Claude surfaces the error and suggests:

> "Bridge port `54400` is in use. Retry with `--bridge-port 54401`? (yes/no)"

Reply `yes` → Claude re-invokes with `--bridge-port 54401`. Up to 3 walk-forward attempts; after that, Claude asks the user to specify a port manually.

## 8. Recovery from corrupt PGLite data tree

If a previous run was hard-killed and left the data tree corrupt, the binary exits with `8` (`DbOpenFailed`) and the `pglite_init_failed` BRIDGE_ERROR. Claude surfaces the binary's `--FORCE --DELETE-EXISTING` recovery hint verbatim — but does NOT auto-run it. To proceed, type:

```
/D2NET-init force rebuild
```

…and confirm the destructive prompt as in Step 5.

## 9. Pass-through of raw flags

If you already know the exact flag invocation you want, pass it verbatim:

```
/D2NET-init --source glp_runtime --target-extension _net --target glp_runtime_net --bridge-port 55001
```

Claude treats this as pass-through, adds `--non-interactive` (and `--accept-suggested-exclusions` if no `--exclude` flags were supplied), and runs the binary.

## What to do when the skill misbehaves

- **Skill not registered**: confirm `.claude/skills/D2NET-init/SKILL.md` exists in the repo. If you added it just now, restart Claude Code so the loader re-scans.
- **Wrong casing on Linux/macOS**: the directory must be exactly `D2NET-init` (uppercase D, N, E, T). On Windows this is cosmetic; on case-sensitive filesystems it matters.
- **Stale-binary loop**: if the staleness check fires on every invocation, build once with `dotnet build tools/d2net/D2Net.sln` and the loop stops until the next `.cs` edit.
