# CLI Contract — `d2net-scaffold`

This is the public command-line contract for the `d2net-scaffold` tool. It is the single interface the toolkit exposes; downstream tools and CI scripts depend on its arguments, exit codes, and stdout/stderr behaviour.

## Invocation

```text
d2net-scaffold <source> <target> [--refresh] [--help] [--version]
```

In the MVP, the canonical run form during development is:

```text
dotnet run --project tools/d2net/src/D2Net.Scaffold -- <source> <target> [--refresh]
```

After a single-file publish (`dotnet publish -c Release`), invocation becomes `d2net-scaffold.exe <source> <target> [--refresh]`.

## Positional arguments

| Position | Name | Required | Description |
|---------:|------|:--------:|-------------|
| 1 | `<source>` | Yes | Path to the source directory to scaffold from. Must exist and be a directory. Relative paths are resolved against the current working directory. |
| 2 | `<target>` | Yes | Path to the target directory the tool will create (default mode) or update (with `--refresh`). |

## Options

| Flag | Description |
|------|-------------|
| `--refresh` | Run in refresh mode: target must already exist. Refreshes pruned-dir state, non-Dart files, and `.dart.src` files; preserves all companion files (`.cs`, `.ana`, …) and the existing `d2net-tracker.json`. New Dart files get fresh companion stubs but no tracker entries (reported in summary). Per FR-011 and spec Q5. |
| `--help`, `-h` | Print usage and exit 0. |
| `--version` | Print version and exit 0. |

## Pruned directories

The directories `.dart_tool`, `build`, `.git`, `.idea`, `.vscode` are skipped at every depth (per FR-002 and spec Q1). Pruning is by directory name, not path.

## Exit codes

| Code | Meaning |
|-----:|---------|
| 0 | Success: scaffold (or refresh) completed and the run summary was printed. |
| 1 | Generic argument or invocation error (bad arg, missing source, etc.). Usage hint on stderr. |
| 2 | Source directory does not exist or is not a directory. |
| 3 | Target directory already exists (default mode, no `--refresh`). Per FR-011. |
| 4 | Target path is the same as, or nested inside, source path. Per FR-014. |
| 5 | Pre-flight collision detected (FR-012). The full collision list is printed to stderr; nothing has been written to the target. |
| 6 | Refresh mode invoked but target does not exist. |

A non-zero exit code MUST be accompanied by a human-readable explanation on stderr.

## stdout (FR-013, spec Q4)

On a successful run, the tool writes a single human-readable summary block to stdout. Example:

```text
d2net-scaffold: scaffold complete.
  Source: <abs source path>
  Target: <abs target path>
  Mode  : fresh                          (or: refresh)
  Directories created   : 142
  Non-Dart files copied : 318
  .dart.src files       : 207
  Companion stubs       : 1863
  Tracker records       : 207
  Tracker file          : <abs path to d2net-tracker.json>
  Pruned directories    : .dart_tool, build, .git, .idea, .vscode
```

In `--refresh` mode the same block is printed, plus a final line listing newly-discovered Dart files (those for which stubs were freshly created but no tracker entry was added):

```text
  New Dart files (no tracker entry, please update d2net-tracker.json manually):
    lib/foo/new_file.dart
    test/widget/another.dart
```

If there are no new Dart files in refresh mode, the line `  New Dart files: (none)` is printed.

## stderr

- Argument errors → exit 1, brief usage hint.
- Pre-existing target → exit 3, message includes the absolute target path.
- Pre-flight collisions → exit 5, message lists every collision in the form:
  `collision: <relative source path of dart file> would generate <ext> stub but <relative path of existing file> already exists`.

## Idempotency guarantees

- Default mode is **strictly create-only**: if it would overwrite anything in the target, it refuses (FR-011) or aborts (FR-012).
- `--refresh` is **idempotent over source**: running `--refresh` twice in a row with no source changes produces zero file mutations on the second run for any tracked-by-source file. Companion files and the tracker are never touched in either run.

## Notes for downstream tools

- The tracker file's location and name (`<target>/d2net-tracker.json`) and its closed status enumeration (`todo`, `in-progress`, `done`, `blocked`) are part of this contract — see `tracker-schema.json`.
- The `.dart.src` rename rule is part of this contract: a Dart source file whose name is `<basename>.dart` becomes `<basename>.dart.src` in the target. The nine companion files are `<basename>.cs`, `<basename>.ana`, `<basename>.tst`, `<basename>.con`, `<basename>.dep`, `<basename>.cgn`, `<basename>.iss`, `<basename>.sta`, `<basename>.ver`.
