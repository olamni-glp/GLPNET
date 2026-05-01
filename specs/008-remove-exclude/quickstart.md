# Quickstart — `d2net-init --remove-exclude`

A one-page operator guide for incrementally removing directory exclusions from an already-initialised D2NET workspace, without rebuilding it.

## Prerequisites

- An existing D2NET workspace at the current working directory (`.D2NET/D2NET-Settings.json` and `.D2NET/pgdb/` present).
- Node.js ≥ 20 on PATH.
- The `d2net-init` binary in `tools/d2net/src/D2Net.Init/bin/Debug/net8.0/` or `Release/net8.0/`, or `dotnet` on PATH for the run-from-source fallback.

## Inspect the current exclusion list

```text
d2net-init --Exclusions
```

## Remove one exclusion

```text
d2net-init --remove-exclude test/programs
```

Removes `test/programs` from the exclusion list and re-indexes every `.dart` file found under `<source>/test/programs/` (zero rows if the directory holds no `.dart` files).

If `test/programs` is **not** currently excluded, the command exits 0 and reports the path as `not-currently-excluded` in the summary.

## Remove several exclusions in one call

```text
d2net-init --remove-exclude test/programs --remove-exclude lib/legacy --remove-exclude does_not_exist
```

All three are processed in a single transaction. If any path is invalid the entire invocation is rejected with no changes applied.

## Override the safety default for system-kind exclusions

By default the command refuses to remove rows that init's auto-detection flagged as `'tool'` (e.g., `.git`, `.dart_tool`, `build`, `bin`) or `'pattern'` (archive markers). Removing those rows would re-index large irrelevant trees and is almost always a mistake.

If you genuinely intend to override this protection — for example, to re-index a directory that init mis-classified — supply `--allow-system-exclusions`:

```text
d2net-init --remove-exclude bin --allow-system-exclusions
```

The summary reports the kind of every removed row so you can audit what was touched.

## Ancestor-survival case

If you remove a child exclusion while its ancestor remains excluded — for example, removing `bin/archive` while `bin` is still excluded — the row for `bin/archive` is removed but the `.dart` files under it stay logically excluded by the surviving `bin` ancestor. Zero rows are inserted into `dart_files`. The summary explicitly reports:

```text
covered-by-ancestor paths:
  bin/archive -- covered by ancestor "bin"
```

If you also want to re-index those files, follow up with `--remove-exclude bin` (which itself requires `--allow-system-exclusions` if `bin` was added by init's heuristics).

## Get machine-readable output (for scripts and skills)

```text
d2net-init --remove-exclude test/programs --json
```

Stdout is a single JSON object matching the schema in `contracts/remove-exclude-cli-contract.md`.

## Skill-driven add-and-undo flow (the canonical use case)

The `/D2NET-init` skill, after a future contract amendment, can drive an interactive batch flow that includes undo:

1. Skill: "Approve `glp`, `docs`, `test/programs`?"
2. Operator: "1,2"
3. Skill: `d2net-init --add-exclude glp --add-exclude docs --json`
4. Operator (later): "I changed my mind about docs."
5. Skill: `d2net-init --remove-exclude docs --json`
6. Workspace returns to the desired state without losing any phase progress.

## Verify the result

```text
d2net-init --Exclusions
d2net-init --list | wc -l
```

The removed entries no longer appear in `--Exclusions`; `--list` grows by exactly the number of `.dart` files re-indexed (and stays the same when ancestors survive).

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Exit 1 (`ArgumentError`) | typo, missing path, conflicting flags | Re-issue with the correct args |
| Exit 6 (`WorkspaceMissingForInspection`) | no `.D2NET/` here | Run `d2net-init` (init mode) first |
| Exit 17 (`RemoveExcludePathOutsideSource`) | path escapes the source root | Supply a path under `<source>/` |
| Exit 18 (`RemoveExcludeSettingsWriteFailed`) | rare: DB updated but settings JSON rename failed | Re-run the same command to resync |
| Exit 19 (`RemoveExcludeDbWriteFailed`) | DB transaction failed; no changes applied | Inspect stderr; check `.D2NET/pgdb/` permissions |
| Exit 20 (`RemoveExcludeWorkspaceLocked`) | another `d2net-init` is running | Wait, then retry |
| Exit 21 (`RemoveExcludeSystemKindRefused`) | path is a tool/pattern row | Re-issue with `--allow-system-exclusions` if you really mean it |
