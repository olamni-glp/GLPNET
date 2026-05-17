SUCCESS: The process with PID 32480 (child process of PID 46348) has been terminated.
SUCCESS: The process with PID 42360 (child process of PID 46348) has been terminated.
The patch adds major CLI workflows, but documented shorthand invocations fail and init can silently ignore new exclusions or retain stale inventory in deferred source flows. These are user-facing correctness issues that should be fixed before landing.

Full review comments:

- [P2] Enable the documented init shorthand — D:\BSTDEV\research\GLP\GLPNET-016\codeconv\src\codeconv\tools\init\__init__.py:39-43
  The contract and skill examples use `codeconv init [run]`, including `/codeconv-init --source ...`, but this Typer group only registers a `run` subcommand and has no default callback. In that documented shorthand, options like `--source` are parsed at the group level and fail as unknown options, so users must know to insert `run` manually.

- [P2] Put default mirror flags on the callback — D:\BSTDEV\research\GLP\GLPNET-016\codeconv\src\codeconv\tools\mirror\__init__.py:57-61
  The documented shorthand `/codeconv-mirror --refresh` / `codeconv mirror --refresh` will not reach this `ctx.invoke(run)` path because `--refresh` is only declared on the `run` subcommand, not on the group callback that parses the shorthand invocation. This makes the skill's refresh example fail unless the user manually writes `run`; the callback needs to accept and forward the same options, or the wrapper must insert `run` before flags.

- [P2] Include exclusions in init idempotence — D:\BSTDEV\research\GLP\GLPNET-016\codeconv\src\codeconv\tools\init\workflow.py:353-356
  After a workspace is initialized, rerunning `codeconv init run` with the same paths but a new `--exclude` is treated as an unchanged no-op because this check only compares `desired` settings. That silently drops a documented input path; users who add exclusions through the init command get exit 0 but `excluded_directories` and the pruned inventory are never updated.

- [P2] Clear stale inventory before deferred init — D:\BSTDEV\research\GLP\GLPNET-016\codeconv\src\codeconv\tools\init\workflow.py:443-450
  When an existing workspace is re-pointed or rebuilt to a source path that does not exist yet, the new deferred-inventory path skips discover and pruning, but the old `codeconv.dart_files` rows remain and are counted. That leaves the workspace bound to the new source while scaffold can still plan from stale rows, producing placeholder output from paths that are not in the new source; clear the inventory when the source changes or during rebuild before taking the deferred branch.
