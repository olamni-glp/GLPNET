# Contract — `d2net-scaffold` CLI

This contract pins the CLI surface, exit codes, and stdout/stderr formats for the refactored `d2net-scaffold` binary. Any breaking change requires a new spec.

## Invocation forms

```text
d2net-scaffold [--json] [--FORCE --DELETE-TARGET]
d2net-scaffold --help
d2net-scaffold --version
```

- **No positional arguments**. Source / target / extension / exclusions all come from the workspace at `<cwd>/.D2NET/`.
- `--json` switches the success summary and error envelope to a stable JSON document.
- `--FORCE` and `--DELETE-TARGET` MUST be supplied as a literal pair (mirroring `d2net-init`'s `--FORCE --DELETE-EXISTING` shape). Supplying only one is exit 1.
- `--help` and `--version` are mutually exclusive with all other flags.

## Pre-conditions

- `.D2NET/` workspace MUST exist at CWD. Absence → exit 22 (`ScaffoldWorkspaceMissing`).
- `<source_dir>/` configured in the workspace MUST exist on disk. Absence → exit 23 (`ScaffoldSourceMissing`).
- The PGLite bridge MUST be acquirable. Lock contention → exit 28 (`ScaffoldWorkspaceLocked`).

## `--FORCE --DELETE-TARGET` interactive flow

When both flags are supplied AND a non-scaffold-managed target tree exists:

1. The tool emits a single confirmation prompt to stderr:
   ```
   d2net-scaffold: --FORCE --DELETE-TARGET supplied. This will recursively delete
   <abs target path> and all of its contents. Proceed? (yes/no)
   ```
2. The tool reads one line from stdin.
3. Affirmative replies (`yes`, `y`, `confirmed`, `proceed`, case-insensitive) cause the destructive flow. Any other reply (including `no`, empty input, EOF) causes exit 29 (`ScaffoldOperatorCancelledTargetDeletion`).
4. The interactive prompt MUST NOT be skipped by any flag. There is no `--non-interactive` option for this gate; the safety check is hard.

When both flags are supplied but the target tree does NOT exist or is already scaffold-managed, the prompt is skipped (no destruction needed) and scaffold proceeds normally.

## Success output — text (default)

Exit 0. Stdout summary:

```
d2net-scaffold: target tree scaffolded at <abs target path>
  source            : <source_dir>
  target            : <target_dir>
  extension         : <ext>
  exclusions        : <N> directories
  files copied      : <F>
  __ working dirs   : <D>
  dart_files updated: <U>
  duration          : <T> seconds

reconciliation summary:
  added paths   : <A>
  removed paths : <R>

[--FORCE --DELETE-TARGET path only:]
destructive override:
  deleted target tree at <abs target path>
```

The reconciliation block appears even on first run (added = all, removed = 0).

## Success output — JSON (`--json`)

Exit 0. Stdout MUST contain exactly one JSON object:

```json
{
  "result": "applied",
  "source": "<source_dir>",
  "target": "<target_dir>",
  "target_abs": "<abs target path with native separators>",
  "extension": "<ext>",
  "destructive_override_used": false,
  "totals": {
    "exclusions": <N>,
    "files_copied": <F>,
    "workdirs_created": <D>,
    "dart_files_updated": <U>,
    "added_paths": <A>,
    "removed_paths": <R>,
    "duration_seconds": <T>
  }
}
```

- `destructive_override_used` is `true` if the run took the `--FORCE --DELETE-TARGET` destructive path; `false` otherwise.
- Empty arrays MUST be `[]`; numeric zeros MUST be `0` (not omitted).
- Field order is fixed.

## Error output — stderr + exit code

| Exit | Constant | When | stderr format |
|---|---|---|---|
| 1 | `ArgumentError` | usage error: missing flag pair, conflicting flags, unknown flag | `d2net-scaffold: <diagnostic>` plus `--help` block |
| 22 | `ScaffoldWorkspaceMissing` | no `.D2NET/` at CWD | `d2net-scaffold: no D2NET workspace at <abs cwd>/.D2NET/. Run d2net-init first.` |
| 23 | `ScaffoldSourceMissing` | configured source dir not on disk | `d2net-scaffold: source directory <source_dir> not found at <abs cwd>/<source_dir>/.` |
| 24 | `ScaffoldTargetNotEmptyAndNotManaged` | target exists with non-scaffold content; no override | `d2net-scaffold: target directory <abs target path> exists but was not produced by a prior scaffold run. Re-issue with --FORCE --DELETE-TARGET to delete it (interactive confirmation required).` |
| 25 | `ScaffoldWorkdirCollision` | a planned `__<basename>/` collides with a real file or non-empty dir at the same source-relative path | `d2net-scaffold: __<basename> collision detected at <relative path>. Source contains both <name>.dart and an unrelated entry that would be overwritten by the working directory.` (one line per offender) |
| 26 | `ScaffoldCopyError` | filesystem IO failure during staging copy or atomic rename | `d2net-scaffold: filesystem error during copy/rename: <inner>. Staging directory <staging path> may have been retained for inspection.` |
| 27 | `ScaffoldDbWriteFailed` | DB transaction failed (DDL / UPDATE / UPSERT / COMMIT) | `d2net-scaffold: workspace database update failed: <inner>. No changes applied; staging directory removed.` |
| 28 | `ScaffoldWorkspaceLocked` | bridge startup detected lock contention | `d2net-scaffold: workspace database is locked by another process. Retry shortly or stop the conflicting invocation.` |
| 29 | `ScaffoldOperatorCancelledTargetDeletion` | operator declined the FR-012a confirmation | `d2net-scaffold: --FORCE --DELETE-TARGET cancelled by operator. No changes made.` |

For exit codes 22–29, when `--json` is supplied the tool emits a single-line JSON object on stdout in addition to the stderr message:

```json
{ "result": "error", "code": <exit>, "message": "<one-line message>" }
```

For exit 24 with `--json`, the tool additionally includes the colliding paths if any (none for code 24, but kept consistent with other commands' shape):

```json
{ "result": "error", "code": 24, "message": "...", "target_abs": "<abs path>" }
```

For exit 25 with `--json`, the tool additionally includes every collision:

```json
{ "result": "error", "code": 25, "message": "...", "collisions": [{"source_path":"<rel>","conflict":"<rel>"}, ...] }
```

## `--help` block

```text
d2net-scaffold — bootstrap a .NET conversion scaffold from a Dart source tree.

Usage:
  d2net-scaffold [--json] [--FORCE --DELETE-TARGET]
  d2net-scaffold --help | --version

The tool reads source, target, extension, and exclusions from the workspace
at <cwd>/.D2NET/D2NET-Settings.json (created by d2net-init). It walks the
on-disk source tree, skips every excluded directory and its descendants,
copies every other file to the target tree at <target_dir>/, and creates
an empty __<basename>/ working directory next to every copied .dart file.

Flags:
  --json                       Emit success summary / error envelope as JSON.
  --FORCE --DELETE-TARGET      Authorise destruction of a non-scaffold-managed
                               target directory. Both flags MUST be supplied
                               together. An interactive confirmation prompt
                               will name the absolute target path before any
                               deletion; reply 'yes' (or 'y') to proceed.

  --help                       Show this help and exit.
  --version                    Show the binary version and exit.

Notes:
  Storage: the workspace database is single-user PGLite (WASM) accessed via
  a per-invocation Node.js bridge subprocess. Requires Node.js >= 20 on PATH.

  Idempotent: re-running with no underlying changes produces a target tree
  byte-identical to the prior state. Exclusion-list changes (via
  d2net-init --add-exclude / --remove-exclude) are picked up on the next
  scaffold run automatically.

  Atomicity: scaffold writes to a sibling staging directory (<target>.d2net-tmp/)
  and renames it over the live target only after the workspace database
  transaction commits. On any failure pre-rename, the staging directory is
  removed and the live target is untouched.
```

## Stability commitments

- Exit codes 22–29 are part of the contract. New conditions add new codes; existing codes do not renumber.
- JSON success-output schema is part of the contract; new fields may be added; existing fields must not be renamed or removed without a major version bump of `d2net-scaffold`.
- The `--FORCE --DELETE-TARGET` interactive prompt format is part of the contract; the absolute path is always named.
- Text success-output layout is human-readable; consumers must use `--json`.
