# Contract — `d2net-init --remove-exclude` CLI

This contract pins the CLI surface, exit codes, and stdout/stderr formats for the `--remove-exclude` mode. Any breaking change requires a new spec.

## Invocation forms

```text
d2net-init --remove-exclude <path> [--remove-exclude <path> ...] [--allow-system-exclusions] [--json] [--bridge-port <port>]
```

- `--remove-exclude` is repeatable. Each occurrence MUST be followed by exactly one path argument.
- `--allow-system-exclusions` is a binary flag (no value). Default: false. When supplied, `kind != 'manual'` rows may be removed alongside manual rows. When absent, encountering any non-manual row in the supplied set rejects the entire invocation.
- `--json` switches the success summary to a stable JSON document.
- `--bridge-port <N>` overrides the live bridge port for this invocation only (does not modify the persisted port).
- Mutually exclusive with: init flags (`--source`, `--target`, `--target-extension`, `--exclude`, `--accept-suggested-exclusions`, `--FORCE`, `--DELETE-EXISTING`, `--non-interactive`); inspection flags (`--list`, `--Exclusions`, `--current-phase`); and `--add-exclude`. Combining any of these with `--remove-exclude` is a usage error (exit 1).

## Path argument grammar

Identical to `--add-exclude` (feature 007 contract). Reuse:

- Non-empty after trim. Whitespace-only is exit 1.
- Forward slash or backslash separators normalised to forward slash.
- Trailing separators stripped.
- MUST resolve under the source root recorded in `D2NET-Settings.json`. Absolute paths permitted iff they normalise to a sub-path of the source root. Paths that escape MUST exit with `RemoveExcludePathOutsideSource` (17).
- MUST NOT refer to a file (existing file at the path, or non-existing path with a known file suffix). Exit code mirrors 007's `AddExcludePathIsFile` semantic — proposed new code or reuse — see Exit codes below.

## Pre-conditions

- `.D2NET/` workspace MUST exist at CWD. Absence → exit 6 (`WorkspaceMissingForInspection`, reused).
- The PGLite bridge MUST be acquirable. Lock contention → exit 20 (`RemoveExcludeWorkspaceLocked`).

## Success output — text (default)

Exit 0. Stdout contains the run summary in the following layout:

```
d2net-init: incremental exclusions removed.
  removed:              <R>
  not-currently-excluded: <N>
  covered-by-ancestor:    <C>
  inserted:               <K> dart_files row(s)

removed paths:
  <path1> (kind: manual)
  <path2> (kind: tool)            -- only if --allow-system-exclusions
  ...

not-currently-excluded paths:
  <pathA>
  ...

covered-by-ancestor paths:
  <pathB> -- covered by ancestor "<ancestor>"
  ...

inserts by removed exclusion:
  <path1>: <rows1> row(s)
  <path2>: <rows2> row(s)
  ...
```

- Sub-blocks are omitted when their counter is zero.
- The `(kind: ...)` annotation appears for every removed path so the operator can see whether system-kind rows were touched.

## Success output — JSON (`--json`)

Exit 0. Stdout MUST contain exactly one JSON object:

```json
{
  "result": "applied",
  "removed": [
    { "path": "<path1>", "kind": "manual" },
    { "path": "<path2>", "kind": "tool" }
  ],
  "not_present": ["<pathA>"],
  "covered_by_ancestor": [
    { "path": "<pathB>", "ancestor": "<ancestor>" }
  ],
  "inserted_rows": [
    { "exclusion": "<path1>", "rows": <rows1> }
  ],
  "totals": {
    "removed": <R>,
    "not_present": <N>,
    "covered_by_ancestor": <C>,
    "inserted_rows": <K>
  }
}
```

- Field order is fixed as shown.
- Empty arrays MUST be `[]`.
- The JSON MUST be parseable by any standard library.

## Error output — stderr + exit code

| Exit | Constant | When | stderr format |
|---|---|---|---|
| 1 | `ArgumentError` | usage error: missing path, conflicting flags | `d2net-init: <diagnostic>` plus `--help` block |
| 6 | `WorkspaceMissingForInspection` | no `.D2NET/` | `d2net-init: no D2NET workspace at <abs cwd>/.D2NET/. Run d2net-init first.` |
| 7 | `BridgeStartFailed` | bridge spawn failure unrelated to lock contention | (existing format) |
| 8 | `DbOpenFailed` | bridge `pglite_init_failed` payload | (existing format) |
| 17 | `RemoveExcludePathOutsideSource` | one or more paths resolves outside source root or names the workspace folder | `d2net-init: --remove-exclude path "<offending>" resolves outside source root "<source>".` |
| 18 | `RemoveExcludeSettingsWriteFailed` | settings JSON rename failed after DB commit | `d2net-init: workspace database updated, but D2NET-Settings.json could not be rewritten: <inner>. Re-run with the same arguments to resync the settings file.` |
| 19 | `RemoveExcludeDbWriteFailed` | DELETE / INSERT / COMMIT raised | `d2net-init: workspace database update failed: <inner>. No changes applied.` |
| 20 | `RemoveExcludeWorkspaceLocked` | bridge startup detected lock contention | `d2net-init: workspace database is locked by another process. Retry shortly or stop the conflicting invocation.` |
| 21 | `RemoveExcludeSystemKindRefused` | one or more supplied paths have `kind != 'manual'` AND `--allow-system-exclusions` was not supplied | `d2net-init: --remove-exclude path "<offending>" has kind='<kind>' which is protected. Re-issue with --allow-system-exclusions to override.` (one line per offending path) |

For all exit codes 17/18/19/20/21, when `--json` is supplied an additional single-line JSON object is emitted on stdout (regardless of stderr content):

```json
{ "result": "error", "code": <exit>, "message": "<one-line message>" }
```

The system-exclusion-refused JSON variant additionally lists every offending path with its kind:

```json
{ "result": "error", "code": 21, "message": "system-exclusion-refused", "offenders": [{"path":"<p>","kind":"<k>"}] }
```

## `--help` block to be added to `Program.cs`

Insert before the existing "Inspection" block:

```text
Incremental exclusion removal (mutually exclusive with init, inspection, and --add-exclude):
  d2net-init --remove-exclude <path> [--remove-exclude <path> ...]
             [--allow-system-exclusions] [--json] [--bridge-port <port>]
             Removes one or more directory exclusions from an existing
             .D2NET workspace and re-indexes any .dart files under those
             directories that are not still covered by a surviving
             ancestor exclusion. Single transaction; phase tables
             untouched. By default refuses to remove rows whose kind is
             'tool' or 'pattern' (init's auto-detected exclusions);
             supply --allow-system-exclusions to override.
```

Append to "Notes:":

```text
  --remove-exclude is non-destructive and idempotent. Re-running with the
  same paths is a no-op for paths already absent from the exclusion list.
```

## Stability commitments

- Exit-code numbers are part of the contract; new conditions add new codes; existing codes do not renumber.
- JSON success-output schema is part of the contract; new fields may be added; existing fields must not be renamed or removed without a major version bump of `d2net-init`.
- Text success-output layout SHOULD be considered human-readable and is not a stable parsing target. Consumers must use `--json`.
