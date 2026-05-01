# Quickstart — `d2net-scaffold`

A one-page operator guide for scaffolding a target tree from an initialised D2NET workspace.

## Prerequisites

- An existing D2NET workspace at the current working directory (`d2net-init` has been run).
- Node.js ≥ 20 on PATH (for the PGLite bridge).
- The `d2net-scaffold` binary in `tools/d2net/src/D2Net.Scaffold/bin/Debug/net8.0/` or `Release/net8.0/`.

## Inspect the workspace state

```text
d2net-init --Exclusions
d2net-init --list
d2net-init --current-phase
```

Confirm the source / target / extension settings and the exclusion list match what you intended.

## Scaffold the target tree

```text
d2net-scaffold
```

Walks the source tree, skips every excluded subtree, copies the rest to the target tree (created on first run), creates an empty `__<basename>/` working directory next to every copied `.dart` file, and updates the `dart_files` table with the two new columns.

Sample output:

```
d2net-scaffold: target tree scaffolded at D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net
  source            : glp_runtime
  target            : glp_runtime_net
  extension         : _net
  exclusions        : 8 directories
  files copied      : 247
  __ working dirs   : 128
  dart_files updated: 128
  duration          : 4.7 seconds

reconciliation summary:
  added paths   : 247
  removed paths : 0
```

## Get machine-readable output

```text
d2net-scaffold --json
```

Stdout is a single JSON object matching the schema in `contracts/scaffold-cli-contract.md`.

## Re-run after changing the exclusion list

```text
d2net-init --add-exclude misc
d2net-scaffold
```

Scaffold reconciles automatically: directories that became excluded are removed from the target tree; directories that became included are populated. The reconciliation summary names the deltas.

## Override a pre-existing non-scaffold target

If you hand-created the target directory (or a previous tool produced it), scaffold refuses by default with exit 24 (`ScaffoldTargetNotEmptyAndNotManaged`). To authorise destruction:

```text
d2net-scaffold --FORCE --DELETE-TARGET
```

The tool emits an interactive prompt naming the absolute target path:

```
d2net-scaffold: --FORCE --DELETE-TARGET supplied. This will recursively delete
D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net and all of its contents. Proceed? (yes/no)
```

Reply `yes` to proceed. Any other reply (including empty input or EOF) cancels with exit 29 (`ScaffoldOperatorCancelledTargetDeletion`).

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Exit 1 (`ArgumentError`) | flag mistyped or `--FORCE` / `--DELETE-TARGET` supplied alone | Re-issue with the correct flag pair |
| Exit 22 (`ScaffoldWorkspaceMissing`) | no `.D2NET/` here | Run `d2net-init` first |
| Exit 23 (`ScaffoldSourceMissing`) | configured source dir was deleted from disk | Restore the source dir or re-init |
| Exit 24 (`ScaffoldTargetNotEmptyAndNotManaged`) | target exists with non-scaffold content | Either move the existing content aside, or re-issue with `--FORCE --DELETE-TARGET` |
| Exit 25 (`ScaffoldWorkdirCollision`) | `__<basename>` would collide with an unrelated path in the source | Resolve the conflict in the source (rename or remove the conflicting entry) |
| Exit 26 (`ScaffoldCopyError`) | filesystem IO failure (disk full, permissions) | Inspect stderr and the staging dir; retry |
| Exit 27 (`ScaffoldDbWriteFailed`) | DB transaction failed | Inspect stderr; check `.D2NET/pgdb/` permissions |
| Exit 28 (`ScaffoldWorkspaceLocked`) | another `d2net-init` / `d2net-scaffold` is running | Wait, then retry |
| Exit 29 (`ScaffoldOperatorCancelledTargetDeletion`) | you declined the destructive prompt | If you meant to proceed, re-issue and answer `yes` |
