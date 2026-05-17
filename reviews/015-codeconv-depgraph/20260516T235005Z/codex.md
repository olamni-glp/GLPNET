SUCCESS: The process with PID 25876 (child process of PID 56940) has been terminated.
SUCCESS: The process with PID 16940 (child process of PID 56940) has been terminated.
The patch is documentation/specification only, but it contains contradictions that would make required behaviours such as replan and SCC crash recovery fail or be implemented in the wrong layer. These should be corrected before implementation proceeds from the spec.

Full review comments:

- [P2] Wire `--replan` into a mutating command — D:\BSTDEV\research\GLP\GLPNET\specs\017-conversion-plan-agents\contracts\planagents_cli.md:102-102
  As written, `--replan` only applies to the read-only `next` command, while `plan-started` has no replan flag and explicitly no-ops when a row is already completed. In the stale-plan scenario from FR-015/T041, `next --replan` can re-select the file but the following `plan-started <path>` will warn `already completed` instead of resetting `plan_completed_at`, so the file cannot actually be replanned.

- [P2] Resume in-progress SCC members, not only singletons — D:\BSTDEV\research\GLP\GLPNET\specs\017-conversion-plan-agents\contracts\plan_readiness_algorithm.md:49-49
  This candidate rule only re-emits `plan_completed_at IS NULL` rows for singletons, so an interrupted SCC member that already has a `dart_plans` row is never selected again. In the documented partial-batch case where A/B are done and C is still in progress, C remains stuck forever and downstream files stay blocked, contradicting T030's `C resumable` requirement.

- [P2] Align the spec on which layer spawns agents — D:\BSTDEV\research\GLP\GLPNET\specs\017-conversion-plan-agents\spec.md:31-31
  This clarification still says the Python tool is the orchestrator that spawns planning and research agents, but FR-002 and the plan later say Python cannot spawn Claude agents and the skill owns the Agent-tool loop. Because this repo requires implementing from the spec first, leaving both statements makes the implementation target ambiguous and can put non-deterministic agent spawning into the Python CLI instead of the skill.
