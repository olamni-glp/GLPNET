SUCCESS: The process with PID 43620 (child process of PID 48792) has been terminated.
SUCCESS: The process with PID 8500 (child process of PID 48792) has been terminated.
The patch is documentation/specification-only, but it introduces several inconsistencies that would lead future implementation or agent sessions down incompatible paths. These should be resolved before treating the feature plan as correct.

Full review comments:

- [P2] Update the AGENTS current-plan pointer too — D:\BSTDEV\research\GLP\GLPNET\CLAUDE.md:549-549
  On this repo Codex sessions are instructed to read `AGENTS.md` first, but this patch updates only `CLAUDE.md`; `AGENTS.md` still points at `specs/015-codeconv-depgraph/plan.md`. Any new Codex session will therefore load the old feature plan instead of the new 017 plan, so the SPECKIT marker should be kept in sync across both instruction files.

- [P2] Align R3 with crashed-agent resume semantics — D:\BSTDEV\research\GLP\GLPNET\specs\017-conversion-plan-agents\research.md:37-37
  For crashed/resumed runs this says `next` never includes a `plan_in_progress` tombstone, but the later contracts and tasks require prior-run in-progress singletons/SCC members to be re-emitted for resume. If an implementer follows R3 here, crashed agents remain stuck and the T010/T020 resume tests cannot pass.

- [P2] Include planagents_runs in the allowed write surface — D:\BSTDEV\research\GLP\GLPNET\specs\017-conversion-plan-agents\spec.md:155-155
  When `planagents_runs` is enabled, the workflow is explicitly planned to write rows there, but FR-020 confines writes to only `dart_plans`, artefacts, the report, and tombstone keys. That makes the optional traceability table violate the feature's own write-surface requirement unless this list either includes `codeconv.planagents_runs` or the table is made read/DDL-only.

- [P2] Specify stale-plan output in status — D:\BSTDEV\research\GLP\GLPNET\specs\017-conversion-plan-agents\contracts\planagents_cli.md:42-42
  For source-drift cases FR-015 and T041 require `status` to report stale plans, but the CLI contract's `status` output only lists the four lifecycle counts plus open escalations. Without a required stale count/list in this contract, an implementation can satisfy the documented `status` shape while failing to surface stale plans to the user.
