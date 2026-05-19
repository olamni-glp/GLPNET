SUCCESS: The process with PID 38364 (child process of PID 41620) has been terminated.
SUCCESS: The process with PID 34376 (child process of PID 41620) has been terminated.
The new implementation tasks direct bridge-backed tests to use the repo-local PGLite default on an exFAT drive, contradicting the repository's mandatory NTFS data-dir requirement and causing the specified test workflow to fail.

Review comment:

- [P1] Keep bridge tests on the NTFS data dir — D:\BSTDEV\research\GLP\GLPNET\specs\018-codeconv-builder\tasks.md:8-11
  On this checkout the repo is on D: exFAT, and AGENTS.md requires every bridge-backed `codeconv` invocation to pass `--data-dir C:/pglite/research/glpnet`; this instruction instead tells implementers to run bridge tests without `--data-dir` and claims `<repo>/.pgdb` is NTFS-valid. Any `@needs_bridge` test following these tasks will use the exFAT repo-local cluster and fail the filesystem guard or crash PGLite, blocking the baseline and final test steps.
