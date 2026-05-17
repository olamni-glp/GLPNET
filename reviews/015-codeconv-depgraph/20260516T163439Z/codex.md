SUCCESS: The process with PID 52916 (child process of PID 46408) has been terminated.
SUCCESS: The process with PID 46884 (child process of PID 46408) has been terminated.
The new verify mode can incorrectly succeed on malformed tombstones and is not fully read-only in clean checkouts. These are contract deviations, though not broad runtime breakages.

Full review comments:

- [P2] Reject tombstones missing sha256 in verify mode — D:\BSTDEV\research\GLP\GLPNET\codeconv\src\codeconv\tools\discover\workflow.py:1062-1065
  When `--verify-tombstones` sees a tombstone with a valid `path` but missing or invalid `sha256`, it currently falls through and is reported as a `stale_tombstone` with exit 0 if the source file exists. The verify-mode contract says format-invalid tombstones abort with exit 65, and `sha256` is required for the audit, so this lets CI pass an invalid tombstone instead of failing.

- [P3] Avoid filesystem writes in verify mode — D:\BSTDEV\research\GLP\GLPNET\codeconv\src\codeconv\tools\discover\workflow.py:112-114
  When `--verify-tombstones` runs in a checkout without `.codeconv/tombstones`, `run_discover` has already executed the unconditional `tombstones_root.mkdir(...)` above this new read-only branch, so the audit creates directories on disk. This violates the new read-only/no-write mode in clean repos; defer creating the tombstone directory until normal/write modes need it.
