---
name: codeconv-scaffold
description: Mirror the in-scope source tree into the target location with the selected pair's file extension + per-file working-directory convention, record each produced target path into the conversion-tracking tombstone, and advance the scaffold phase. Use when the user types `/codeconv-scaffold` or asks to scaffold/produce the C# target tree for an initialised Dart→C# workspace.
argument-hint: "Pass --flag args verbatim (e.g. --json, --force-delete-target). Empty input runs scaffold against the initialised workspace."
compatibility: "Requires an initialised workspace (run /codeconv-init first), the codeconv venv (codeconv/.venv), and Node.js >= 20 (the unified PGLite bridge). De-brand of /D2NET-scaffold — the .NET tool is removed."
---

# /codeconv-scaffold

Thin wrapper over `codeconv scaffold`. Forwards arguments verbatim. The
CLI is authoritative; this skill adds **no** business logic — only the
destructive-operation confirmation gate (Step 4).

Contract source of truth: `specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_scaffold_cli.md`.
This skill MUST stay in sync with that contract; change the contract first.

## What this skill does

1. Resolve the codeconv venv (`codeconv/.venv/Scripts/python.exe` on
   Windows, `codeconv/.venv/bin/python` on POSIX). If absent, instruct
   Gabi to run `python -m venv codeconv/.venv && codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]` first.
2. Apply the destructive-operation gate (Step 4) if the input requests
   `--force-delete-target` (or natural-language `force`/`delete`/
   `overwrite`/`wipe`/`rebuild`).
3. Invoke `codeconv scaffold run <args verbatim>` from the repo root.
4. Show stdout/stderr from the run verbatim.

## Subcommand and flags

`/codeconv-scaffold [run] [flags]`

| Flag | Default | Effect |
|---|---|---|
| `--force-delete-target` | off | destructive: overwrite a non-empty target — requires the Step-4 confirmation |
| `--no-tombstone-update` | off | skip writing `target_path` into tombstones (testing only) |
| `--quiet` / `--json` | off | per top-level convention |

On this exFAT checkout the operator passes `--data-dir C:/pglite/research/glpnet`
as a top-level flag. The skill forwards all top-level flags verbatim.

## Step 4 — Destructive-operation gate (FR-017 / FR-021)

Detect destructive intent if **either**:

- The argument string (case-insensitive) contains `--force-delete-target`,
  or any of the marker words `force`, `delete`, `overwrite`, `wipe`,
  `nuke`, `rebuild`, `redo`, `recreate`.

If destructive:

- Compute the absolute target tree path by reading the workspace's
  `target_path` (run `codeconv init inspect --json` if needed) and
  resolving it against the repo root. This is the **cache key**.
  - If no workspace is initialised, `codeconv scaffold` exits 2
    ("run codeconv init first"); surface that and skip the gate — there
    is nothing to destroy.
- Look in the **current conversation transcript** for a structured
  marker matching `[codeconv-scaffold: destructive-confirmed = <abs target path> @ <ISO timestamp>]`
  for that exact path. If found AND not dropped by auto-compaction,
  treat the path as already-confirmed — skip the prompt and add
  `--force-delete-target` (the CLI re-checks the non-empty target every
  run; there is no interactive CLI prompt to drive).
- Otherwise, emit ONE confirmation prompt naming the absolute target:
  `This will recursively delete <abs target path> and all of its contents, then rebuild it. Proceed? (yes/no)`.
- Wait for an affirmative reply (`yes`, `y`, `confirmed`, `proceed`).
- On affirmative, append a structured marker line on a line of its own:
  `[codeconv-scaffold: destructive-confirmed = <abs target path> @ <ISO timestamp>]`,
  then ensure `--force-delete-target` is in the forwarded flags.
- On non-affirmative reply, stop without invoking the CLI. Do NOT write
  the marker.

If the marker is absent from surviving context (auto-compaction),
re-prompt. Re-prompting is the safe failure mode; the skill MUST NOT use
filesystem persistence to compensate.

If the input is **not** destructive, never add `--force-delete-target`,
even when the CLI's non-empty-target refusal (exit 2) suggests it later.

## Pre-execution checks

- An initialised workspace is required (run `/codeconv-init` first).
  Scaffold refuses (exit 2) if the workspace/inventory is absent.
- The unified bridge daemon must be reachable (auto-spawned by
  `acquire_or_discover`; ~7 s PGLite cold-init on first use).
- Schema migrations must have run at least once (`codeconv migrate`).

## What scaffold writes

| Target | Content |
|---|---|
| `<target>/**` | The mirrored target tree: every non-excluded source file at its pair-mapped path (`.dart` → `.cs`, mirrored dirs) plus the per-file `__<base>/` working directory. The *skeleton* — not converted code (conversion is a future stage). |
| `.codeconv/tombstones/<rel>.dart.md` `target_path` | The produced target rel-path, written into the **existing feature-015 tombstone key** via the canonical writer (idempotent). A missing tombstone is a WARNING, not a failure. |
| `codeconv.phase_status['scaffold']` | Advanced to `COMPLETE`; `codeconv.phase_sequence` gains `scaffold` if absent. |

Staging: the tree is written under `<target>.codeconv-scaffold-tmp/`
then atomically moved into place — a failure never leaves a half-written
target (FR-017). The staging dir is never committed.

## Idempotence (SC-002)

Re-`/codeconv-scaffold` on an unchanged inventory ⇒ identical target
tree (no churn), zero phase duplication, zero tombstone diff (canonical
writer + same `target_path` values).

## What this skill does NOT do

- Does NOT translate Dart → C# — it produces the target *skeleton/tree*,
  not converted code (conversion is a future stage).
- Does NOT create a `scaffold_tracker` table or any `public.*` object;
  does NOT add a new tombstone key (only fills the existing
  `target_path` — D4/FR-020).
- Does NOT interpret args beyond the destructive gate — everything else
  is forwarded verbatim to the CLI.

## Examples

- `/codeconv-scaffold` → mirrors the in-scope source tree into the
  workspace target with `.cs` + `__<base>/` workdirs.
- `/codeconv-scaffold --json` → same, with a JSON summary.
- `/codeconv-scaffold --force-delete-target` → triggers Step 4
  confirmation, then forwards `--force-delete-target` on an affirmative
  reply (overwrites the non-empty target via staged atomic replace).
