SUCCESS: The process with PID 47448 (child process of PID 29948) has been terminated.
SUCCESS: The process with PID 56064 (child process of PID 29948) has been terminated.
The patch adds useful reconciliation behavior, but the new verify mode is not truly read-only and the from-tombstones preflight under-validates required tombstone fields.

Full review comments:

- [P2] Keep verify-tombstones read-only — D:\BSTDEV\research\GLP\GLPNET\codeconv\src\codeconv\tools\discover\workflow.py:112-114
  When `--verify-tombstones` runs in a repo without `.codeconv/tombstones`, `run_discover` has already executed `tombstones_root.mkdir(...)` before reaching this branch, so the supposedly read-only audit creates directories and can dirty CI/working trees. Move that mkdir below the verify-mode dispatch or only create it in write modes.

- [P2] Validate every required tombstone field — D:\BSTDEV\research\GLP\GLPNET\codeconv\src\codeconv\tools\discover\workflow.py:670-670
  In `--from-tombstones` mode, a tombstone missing required frontmatter like `name`, `dependencies`, `callers`, or `mtime` passes preflight because only `path` and `sha256` are checked. The tombstone contract makes all eight feature-012 fields required, so this silently defaults bad input and mutates the DB instead of aborting with exit 65.
