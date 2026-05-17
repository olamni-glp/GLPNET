SUCCESS: The process with PID 45980 (child process of PID 57584) has been terminated.
SUCCESS: The process with PID 31076 (child process of PID 57584) has been terminated.
The new dangling-edge cleanup can permanently lose an import edge across idempotent discover runs when the missing target is later added. That breaks the inventory and downstream depgraph correctness for a realistic source evolution.

Review comment:

- [P2] Reinsert previously dangling imports when targets appear — D:\BSTDEV\research\GLP\GLPNET\codeconv\src\codeconv\tools\discover\workflow.py:321-332
  When a source imports an in-subtree file that does not exist yet, this deletes the dangling edge from `dart_imports`; on a later run where that target file has been created, the importer is unchanged and `_process_one_file` skips it by sha, so the now-valid edge is never reinserted and tombstones/depgraph remain incomplete until the importer itself changes.
