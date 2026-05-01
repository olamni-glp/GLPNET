# Quickstart — `d2net-init --add-exclude`

A one-page operator guide for incrementally adding directory exclusions to an already-initialised D2NET workspace, without rebuilding it.

## Prerequisites

- An existing D2NET workspace at the current working directory (`.D2NET/D2NET-Settings.json` and `.D2NET/pgdb/` present). If you don't have one, run `d2net-init --source <name> --target-extension <ext> --target <name>` first.
- Node.js ≥ 20 on PATH (required for the per-invocation PGLite bridge subprocess).
- `tools/d2net/src/D2Net.Init/bin/Debug/net8.0/d2net-init.exe` (Windows) or the matching Release / non-Windows binary, OR `dotnet` on PATH for the `dotnet run` fallback.

## Inspect the current exclusion list

```text
d2net-init --Exclusions
```

Lists the directories the workspace is currently ignoring. The output is human-readable; pass `--json` for a stable structured form.

## Add one exclusion

```text
d2net-init --add-exclude test_archive
```

Adds `test_archive` to the exclusion list and removes every `dart_files` row whose path falls under `<source>/test_archive/`. Idempotent: a second run with the same argument is a no-op.

## Add several exclusions in one call

```text
d2net-init --add-exclude glp --add-exclude docs --add-exclude test/programs
```

All three are applied in a single transaction. If any path is invalid the entire invocation is rejected with no changes applied.

## Get machine-readable output (for scripts and skills)

```text
d2net-init --add-exclude glp --add-exclude docs --json
```

Stdout contains a single JSON object. Example:

```json
{
  "result": "applied",
  "added": ["glp", "docs"],
  "redundant": [],
  "removed_rows": [
    { "exclusion": "glp",  "rows": 0 },
    { "exclusion": "docs", "rows": 0 }
  ],
  "totals": { "added": 2, "redundant": 0, "removed_rows": 0 }
}
```

## Skill-driven batch flow (the canonical use case)

The `/D2NET-init` skill scans the source tree, presents 5 candidate directories at a time with notes, collects the operator's approvals one batch at a time, and then invokes `d2net-init` once per approved batch. A typical session shape:

1. Skill: "Batch 1 of 3 — recommend adding `glp/`, `docs/`, `test/programs/`, `test/module/files`. Reply with which to add."
2. Operator: "1,2,3,4"
3. Skill runs: `d2net-init --add-exclude glp --add-exclude docs --add-exclude test/programs --add-exclude test/module/files --json`
4. Skill parses JSON, surfaces the totals, and presents Batch 2.

At every step:
- `phase_sequence` and `phase_status` are untouched, so any in-flight downstream phase work survives all batches.
- A second invocation of the same batch is safe (idempotent).

## Verify the result

```text
d2net-init --Exclusions
```

The new directories MUST appear in the list. To confirm `dart_files` shrinking, compare:

```text
d2net-init --list | wc -l        # before
d2net-init --add-exclude foo
d2net-init --list | wc -l        # after — smaller iff foo contained .dart files
```

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Exit 1 (`ArgumentError`) | typo or missing path after `--add-exclude` | Re-issue with the correct path |
| Exit 6 (`WorkspaceMissingForInspection`) | no `.D2NET/` here | Run `d2net-init` (init mode) first |
| Exit 12 (`AddExcludePathOutsideSource`) | path escapes the source root | Supply a path under `<source>/` |
| Exit 13 (`AddExcludeSettingsWriteFailed`) | rare; database updated but settings JSON rename failed | Re-run the same command to resync |
| Exit 14 (`AddExcludeDbWriteFailed`) | DB transaction failed; no changes applied | Inspect stderr; check `.D2NET/pgdb/` permissions |
| Exit 15 (`AddExcludeWorkspaceLocked`) | another `d2net-init` process is running | Wait for it to finish, then retry |
| Exit 16 (`AddExcludePathIsFile`) | path is a file, not a directory | Exclusions are directory-only; supply a directory |
