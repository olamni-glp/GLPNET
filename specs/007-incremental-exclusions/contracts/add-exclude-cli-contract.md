# Contract — `d2net-init --add-exclude` CLI

This contract pins the CLI surface, exit codes, and stdout/stderr formats that downstream callers (the `/D2NET-init` skill, scripts, tests) MAY depend on. Any breaking change to this surface requires a new spec.

## Invocation forms

```text
d2net-init --add-exclude <path> [--add-exclude <path> ...] [--json]
```

- `--add-exclude` is repeatable. Each occurrence MUST be followed by exactly one path argument.
- `--json` is optional; when supplied the success summary is rendered as a stable JSON document (see "Success output — JSON" below).
- `--add-exclude` MUST NOT be combined in the same invocation with init-mode flags (`--source`, `--target-extension`, `--target`, `--exclude`, `--accept-suggested-exclusions`, `--FORCE`, `--DELETE-EXISTING`, `--non-interactive`, `--bridge-port`) or with inspection flags (`--list`, `--Exclusions`, `--current-phase`). Combining them is a usage error and exits with code 1 (`ArgumentError`).
- `--add-exclude` MAY be combined with `--bridge-port <N>` to override the live bridge port for this invocation only (does not modify the persisted port). This mirrors the existing inspection-mode override semantics.

## Path argument grammar

- A path is a non-empty string after trim. Whitespace-only paths are a usage error (exit 1).
- Path separators MAY be forward slash (`/`) or backslash (`\`); they are normalised to forward slash internally.
- Trailing separators are stripped.
- The path MUST resolve under the source root recorded in `D2NET-Settings.json`. Absolute paths are permitted iff they resolve under the source root after normalisation. Paths containing `..` segments that escape the source root MUST be rejected with exit code 12 (`AddExcludePathOutsideSource`).
- The path SHOULD refer to a directory. If it exists and is a regular file, OR it does not exist and ends with a recognised file suffix (`.dart`, `.zip`, `.exe`, `.dll`, `.so`, `.dylib`, `.json`, `.txt`, `.md`, `.lock`), it MUST be rejected with exit code 16 (`AddExcludePathIsFile`).
- The path MUST NOT be the workspace folder itself (`.D2NET`) — rejecting such an attempt is exit code 12.

## Pre-conditions

- The current working directory MUST contain a `.D2NET/` workspace. Absence MUST exit with code 6 (`WorkspaceMissingForInspection` — reused for the operation; see research R3) and a stderr line of the form:

  ```
  d2net-init: no D2NET workspace at <abs cwd>/.D2NET/. Run d2net-init --source <name> --target-extension <ext> --target <name> first.
  ```

- The PGLite data directory MUST be available for exclusive access. If another `d2net-init` process holds the lock at bridge startup, exit with code 15 (`AddExcludeWorkspaceLocked`) and a stderr line:

  ```
  d2net-init: workspace database is locked by another process. Retry shortly or stop the conflicting invocation.
  ```

## Success output — text (default)

Exit code 0. Stdout MUST contain a summary in the following layout (one blank line between blocks; lines are wrapped here for readability):

```
d2net-init: incremental exclusions applied.
  added:      <N>
  redundant:  <M>
  removed:    <K> dart_files row(s)

added paths:
  <path1>
  <path2>
  ...

redundant paths (already excluded or covered by an ancestor):
  <pathA> -- already excluded
  <pathB> -- covered by ancestor "<ancestor>"
  ...

removals by exclusion:
  <path1>: <rows1> row(s)
  <path2>: <rows2> row(s)
  ...
```

- The `redundant paths` and `removals by exclusion` blocks MAY be omitted when `<M>` or `<K>` are zero respectively.
- The `added paths` block MAY be omitted when `<N>` is zero (an all-redundant no-op run).

## Success output — JSON (`--json`)

Exit code 0. Stdout MUST contain exactly one JSON object (no leading or trailing log lines):

```json
{
  "result": "applied",
  "added": ["<path1>", "<path2>"],
  "redundant": [
    { "path": "<pathA>", "reason": "already-excluded" },
    { "path": "<pathB>", "reason": "covered-by-ancestor", "ancestor": "<ancestor>" }
  ],
  "removed_rows": [
    { "exclusion": "<path1>", "rows": <rows1> },
    { "exclusion": "<path2>", "rows": <rows2> }
  ],
  "totals": {
    "added": <N>,
    "redundant": <M>,
    "removed_rows": <K>
  }
}
```

- Field order is fixed as shown.
- Empty arrays MUST be emitted as `[]` (not omitted) so consumers can rely on field presence.
- The JSON MUST be parseable by any standard JSON library.

## Error output — stderr + exit code

| Exit | Constant | When | stderr format |
|---|---|---|---|
| 1 | `ArgumentError` | usage error: missing path argument, conflicting flag combination, malformed JSON option | One line beginning `d2net-init: ` followed by the parser's diagnostic, then the standard `--help` block. |
| 6 | `WorkspaceMissingForInspection` | no `.D2NET/` workspace at CWD | `d2net-init: no D2NET workspace at <abs cwd>/.D2NET/. Run d2net-init --source <name> --target-extension <ext> --target <name> first.` |
| 7 | `BridgeStartFailed` | bridge spawn failed for reasons unrelated to lock contention | (existing format from feature 005) |
| 8 | `DbOpenFailed` | bridge reported `pglite_init_failed` | (existing format from feature 005) |
| 12 | `AddExcludePathOutsideSource` | one or more paths resolves outside the source root, or names the workspace folder | `d2net-init: --add-exclude path "<offending path>" resolves outside source root "<source>".` |
| 13 | `AddExcludeSettingsWriteFailed` | settings JSON rename failed after DB commit (rare divergence window) | `d2net-init: workspace database updated, but D2NET-Settings.json could not be rewritten: <inner exception>. Re-run with the same arguments to resync the settings file.` |
| 14 | `AddExcludeDbWriteFailed` | INSERT, DELETE, or COMMIT raised | `d2net-init: workspace database update failed: <inner exception>. No changes applied.` |
| 15 | `AddExcludeWorkspaceLocked` | bridge startup detected an existing data-dir lock | `d2net-init: workspace database is locked by another process. Retry shortly or stop the conflicting invocation.` |
| 16 | `AddExcludePathIsFile` | a `--add-exclude` path is an existing file or has a known file suffix | `d2net-init: --add-exclude path "<offending path>" is a file. Exclusions are directory-only.` |

For exit codes 12, 13, 14, 15, 16, the `--json` flag (if supplied) MUST emit a single-line JSON object on stdout (regardless of stderr content):

```json
{ "result": "error", "code": <exit>, "message": "<one-line message>" }
```

Stderr still contains the human-readable form. This dual emission lets the calling skill parse JSON from stdout while preserving readable diagnostics for direct CLI use.

## `--help` block to be added to `Program.cs`

Insert the following lines before the existing `Inspection (mutually exclusive):` block, separated by a blank line, so the new mode is documented alongside init and inspection:

```text
Incremental exclusion update (mutually exclusive with init and inspection):
  d2net-init --add-exclude <path> [--add-exclude <path> ...] [--json] [--bridge-port <port>]
             Adds one or more directory exclusions to an existing .D2NET workspace.
             Removes any dart_files rows that fall under the new exclusions in one
             transaction. Does not touch phase_sequence or phase_status. Requires
             an existing workspace; will not auto-init.
```

The closing "Notes:" block of `--help` is extended with one line:

```text
  --add-exclude is non-destructive and idempotent. Re-running with the same paths
  is a no-op. Use --FORCE --DELETE-EXISTING only to wipe and rebuild the workspace.
```

## Stability commitments

- The exit-code table above is part of the contract. New error conditions in future versions MAY add new codes but MUST NOT renumber existing ones.
- The JSON success-output schema is part of the contract. New fields MAY be added; existing fields MUST NOT be renamed or removed without a major version bump of `d2net-init`.
- The text success-output layout SHOULD be considered human-readable and is not a stable parsing target. Consumers must parse `--json` if they need machine-readable output.
