SUCCESS: The process with PID 46556 (child process of PID 53740) has been terminated.
SUCCESS: The process with PID 48864 (child process of PID 53740) has been terminated.
The depgraph implementation has functional gaps in the documented slash-command surface, default CLI alias behavior, and tombstone round-trip state. The new filesystem guard also is not consistently handled by commands that share the bridge acquisition path.

Full review comments:

- [P2] Preserve target_path in tombstone round trips — D:\BSTDEV\research\GLP\GLPNET\codeconv\src\codeconv\tools\depgraph\workflow.py:513-516
  When `mark-completed --target <path>` or `stamp-tombstones` is used, the target path is stored in `codeconv.dart_conversions` but deliberately omitted from tombstone YAML here. That means `rebuild-conversions-from-tombstones` cannot restore `target_path` after a DB wipe, even though the feature contract says tombstones carry `target_path` for round-trip conversion state.

- [P2] Support compute flags on the default depgraph alias — D:\BSTDEV\research\GLP\GLPNET\codeconv\src\codeconv\tools\depgraph\__init__.py:62-70
  When users invoke the advertised default alias with compute flags, e.g. `codeconv depgraph --dry-run` or the slash wrapper forwarding `/codeconv-depgraph --dry-run`, those options are parsed against the callback, not the `compute` command. Because the callback declares no `--dry-run`, `--json-out`, or `--json` options and simply invokes `compute` with defaults, the alias only works for the no-arg case and breaks the documented flag surface unless users know to type `compute` explicitly.

- [P2] Add the missing codeconv-depgraph skill wrapper — D:\BSTDEV\research\GLP\GLPNET\specs\015-codeconv-depgraph\contracts\depgraph_cli.md:188-190
  The feature contract requires `.claude/skills/codeconv-depgraph/SKILL.md`, but the diff/tree does not add that directory or file. As a result the requested `/codeconv-depgraph` slash workflow is not invokable even though the Python CLI exists, so users must bypass the documented skill interface.

- [P2] Handle filesystem guard errors outside migrate — D:\BSTDEV\research\GLP\GLPNET\codeconv\src\codeconv\bridge_client.py:197-200
  On Windows when the default data dir is on exFAT and the user forgets `--data-dir`, this shared `acquire_or_discover` path now raises `DataDirFilesystemError`. Only `codeconv migrate` catches that new exception, so `doctor`, `discover`, and the new `depgraph` commands surface it as a generic bridge/internal failure instead of the intended usage-64 actionable message.
