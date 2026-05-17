SUCCESS: The process with PID 51784 (child process of PID 26808) has been terminated.
SUCCESS: The process with PID 49752 (child process of PID 26808) has been terminated.
The patch is specification/planning-only, but it introduces concrete task and contract defects that would make the planned implementation fail: the test command is invalid, the normal convspec agent handoff is modeled as a failed DBOS step, and the schema does not enforce the promised research cache invariant.

Full review comments:

- [P2] Make the pytest baseline command runnable — D:\BSTDEV\research\GLP\GLPNET\specs\018-codeconv-builder\tasks.md:20-20
  When this task is followed as written, `pytest` aborts before running any tests because `codeconv/tests/conftest.py` only registers `--run-perf`, not `--data-dir`. This affects both the required baseline and final verification commands, so the plan should not pass `--data-dir` to pytest unless a pytest option is added.

- [P1] Do not model agent work as an uncaught DBOS step exception — D:\BSTDEV\research\GLP\GLPNET\specs\018-codeconv-builder\contracts\dbos_workflow_model.md:28-31
  For the normal first-time convspec path where no artifact exists yet, `raise NeedsAgentWork(path)` inside an `@DBOS.step` will be recorded by DBOS as a failed step/workflow, not as the pending `needs_agent_work` result that the skill loop expects. That makes the MVP convspec flow fail before the agent can produce the artifact; surface this as a normal durable status/output or catch it before DBOS marks the step failed.

- [P2] Enforce one research finding per construct key — D:\BSTDEV\research\GLP\GLPNET\specs\018-codeconv-builder\data-model.md:56-56
  The cache rule says a `construct_key` is researched once and never re-researched, but the schema allows multiple `research_findings` rows for the same key. When parallel convspec/research agents encounter the same construct, duplicate findings can be inserted and later lookups become ambiguous, breaking FR-012/FR-024; add a uniqueness constraint or equivalent conflict handling for `research_findings.construct_key`.
