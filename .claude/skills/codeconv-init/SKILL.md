---
name: codeconv-init
description: Configure a Dart→C# conversion workspace (selected language pair, source/target paths, directory exclusions, conversion-phase tracking) and delegate the per-file inventory to discover. Use when the user types `/codeconv-init` or asks to initialise/configure a conversion workspace, add/remove a directory exclusion, or rebuild the workspace.
argument-hint: "Pass --flag args verbatim (e.g. --source glp_runtime_net --target out/csharp --accept-suggested-exclusions --non-interactive), or a subcommand (add-exclude <dir> / remove-exclude <dir> / list / inspect). Empty input shows the run summary."
compatibility: "Requires the codeconv venv (codeconv/.venv) and Node.js >= 20 (the unified PGLite bridge). De-brand of /D2NET-init — the .NET tool is removed."
---

# /codeconv-init

Thin wrapper over `codeconv init`. Forwards arguments verbatim. The CLI
is authoritative; this skill adds **no** business logic — only the
destructive-operation confirmation gate (Step 4).

Contract source of truth: `specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_init_cli.md`.
This skill MUST stay in sync with that contract; change the contract first.

## What this skill does

1. Resolve the codeconv venv (`codeconv/.venv/Scripts/python.exe` on
   Windows, `codeconv/.venv/bin/python` on POSIX). If absent, instruct
   Gabi to run `python -m venv codeconv/.venv && codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]` first.
2. Apply the destructive-operation gate (Step 4) if the input requests
   `--rebuild` (or natural-language `rebuild`/`reset`/`recreate`/`wipe`).
3. Invoke `codeconv init <args verbatim>` from the repo root.
4. Show stdout/stderr from the run verbatim.

## Command tree and flags

`/codeconv-init [run] [flags]` (default) plus subcommands:

```text
/codeconv-init [run]                       configure + delegate inventory to discover
/codeconv-init add-exclude <path>          add a manual exclusion, re-sync inventory
/codeconv-init remove-exclude <path>       remove an exclusion, re-sync inventory
/codeconv-init list                        list in-scope inventoried files
/codeconv-init inspect [--exclusions|--current-phase]   workspace introspection
```

`run` flags:

| Flag | Default | Effect |
|---|---|---|
| `--source <path>` | `glp_runtime_net` | source subtree (repo-relative); validated (exists, in-repo, not reserved) |
| `--target <path>` | (required) | target tree root (repo-relative) |
| `--source-lang <id>` | `dart` | source language id |
| `--target-lang <id>` | `csharp` | target language id |
| `--exclude <path>` (repeatable) | — | manual directory exclusions |
| `--accept-suggested-exclusions` | off | accept the pair's tool-exclusion recommendations non-interactively |
| `--non-interactive` | off | no prompts; requires the above or explicit `--exclude` |
| `--rebuild` | off | destructive re-init (discards existing workspace state) — requires `--confirm-rebuild` |
| `--confirm-rebuild` | off | confirmation token for `--rebuild` (this skill supplies it after Step 4) |
| `--quiet` / `--json` | off | per top-level convention |

The canonical repo-local cluster (checkout is NTFS) is passed as `--data-dir D:/bstdev/research/glp/glpnet/.pgdb`
as a top-level flag (the guard hard-fails otherwise). The skill forwards
all top-level flags verbatim.

## Step 4 — Destructive-operation gate (FR-010 / FR-021)

Detect destructive intent if **either**:

- The argument string (case-insensitive) contains `--rebuild`, or any of
  the marker words `rebuild`, `reset`, `recreate`, `reinitialise`,
  `reinitialize`, `wipe`, `nuke`, `redo`, `force-rebuild`.

If destructive:

- Compute the absolute target tree path (`<repo>/<--target>`), or use
  the literal target token from the args.
- Look in the **current conversation transcript** for a structured
  marker matching `[codeconv-init: rebuild-confirmed = <target> @ <ISO timestamp>]`
  for that target. If found AND not dropped by auto-compaction, treat
  the target as already-confirmed and add `--rebuild --confirm-rebuild`
  without prompting.
- Otherwise, emit ONE confirmation prompt naming the absolute target:
  `This will DISCARD the existing workspace state and rebuild it from scratch (target: <target>). Proceed? (yes/no)`.
- Wait for an affirmative reply (`yes`, `y`, `confirmed`, `proceed`).
- On affirmative, append a structured marker line to the response on a
  line of its own: `[codeconv-init: rebuild-confirmed = <target> @ <ISO timestamp>]`,
  then add `--rebuild --confirm-rebuild` to the forwarded flags.
- On non-affirmative reply, stop without invoking the CLI.

If the marker is absent from surviving context (earlier turn summarised
by auto-compaction), re-prompt. Re-prompting is the safe failure mode;
the skill MUST NOT use filesystem persistence to compensate.

If the input is **not** destructive, never add `--rebuild` /
`--confirm-rebuild`, even if the CLI's "already initialized" result
suggests it later.

## Pre-execution checks

- The unified bridge daemon must be reachable. `codeconv init` calls
  `acquire_or_discover` which auto-spawns the daemon if needed. The
  first invocation in a fresh repo pays a ~7 s PGLite cold-init penalty.
- Schema migrations must have run at least once (`/codeconv-runner migrate`
  or `codeconv migrate`); otherwise the tool fails with an undefined-table
  error (the feature's tables are created by Alembic migration 0003).

## What init writes

| Target | Content |
|---|---|
| `codeconv.workspace_settings` | `source_lang`, `target_lang`, `source_path`, `target_path` (the single authority for which pair the workspace is bound to). |
| `codeconv.excluded_directories` | The pair's tool-exclusion recommendations (`kind='tool'`) + any `--exclude` (`kind='manual'`). |
| `codeconv.phase_sequence` / `codeconv.phase_status` | Conversion-phase ordering + per-phase status (seeded `discover`, `scaffold`). |
| `codeconv.dart_files` (+ imports/callers/tombstones) | Populated by the **delegated discover** (D3 — init never scans the source tree itself), then pruned to the exclusion scope. |

## Idempotence (SC-002)

Re-`/codeconv-init` with unchanged inputs ⇒ "already initialized", exit
0, zero change to the four workspace tables and a no-op discover.

## What this skill does NOT do

- Does NOT scan the source tree itself — the inventory is delegated to
  discover (single inventory source of truth, D3).
- Does NOT create any `public.*` table or a `scaffold_tracker` (FR-020).
- Does NOT translate Dart → C# (that is the scaffold/conversion stages).
- Does NOT interpret args beyond the destructive gate — everything else
  is forwarded verbatim to the CLI.

## Examples

- `/codeconv-init --source glp_runtime_net --target out/csharp --accept-suggested-exclusions --non-interactive`
  → configures a Dart→C# workspace and delegates the inventory.
- `/codeconv-init add-exclude glp_runtime_net/lib/generated`
  → adds a manual exclusion and re-syncs the inventory.
- `/codeconv-init list --json` → lists the in-scope inventoried files.
- `/codeconv-init --rebuild` → triggers Step 4 confirmation, then
  forwards `--rebuild --confirm-rebuild` on an affirmative reply.
