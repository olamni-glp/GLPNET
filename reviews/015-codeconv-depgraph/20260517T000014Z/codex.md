SUCCESS: The process with PID 55340 (child process of PID 19936) has been terminated.
SUCCESS: The process with PID 26408 (child process of PID 19936) has been terminated.
The patch is a planning/spec change, but several introduced contracts are internally inconsistent or omit required behavior. Following them would break readiness selection, dry-run safety, stale-plan reporting, and agent startup context.

Full review comments:

- [P2] Allow readiness to read the dependency edges — D:/BSTDEV/research/GLP/GLPNET/specs/017-conversion-plan-agents/contracts/plan_readiness_algorithm.md:17-17
  For files with dependencies, `codeconv.dart_depgraph` only stores per-file topo/cycle/status/counts, not the actual dependency targets. This contract says `cross_scc_deps` comes from a depgraph edge view and later excludes reading `codeconv.dart_imports`, so an implementation cannot determine whether each cross-SCC dependency is planned and may either block forever or mark files ready too early. The workflow needs to read `codeconv.dart_imports` read-only and join it with `dart_depgraph`.

- [P2] Short-circuit the skill loop for dry runs — D:/BSTDEV/research/GLP/GLPNET/specs/017-conversion-plan-agents/contracts/planagents_cli.md:123-127
  When `/codeconv-planagents --dry-run` is used, FR-019/SC-008 require no agents and no DB/tombstone/artefact writes, but this normative loop ignores the user flags and proceeds from `next` to `plan-started` and Agent spawning. Even if `next` is read-only, the following lifecycle calls and agents will mutate state, so the skill needs an explicit dry-run branch before dispatching or completing plans.

- [P2] Surface stale plans in status output — D:/BSTDEV/research/GLP/GLPNET/specs/017-conversion-plan-agents/contracts/planagents_cli.md:41-42
  For the source-drift case, FR-015 and the quickstart require `status` to report stale plans, but this status contract only classifies lifecycle states and emits counts without comparing `dart_files.sha256` to `sha256_of_dart_at_plan_start`. A changed file with a completed old plan would appear simply as `planned`, leaving users with no way to discover what to pass to explicit `--replan`.

- [P2] Update AGENTS.md with the active Spec Kit plan — D:/BSTDEV/research/GLP/GLPNET/CLAUDE.md:549-549
  This updates the Spec Kit pointer in `CLAUDE.md`, but `AGENTS.md` still points at `specs/015-codeconv-depgraph/plan.md`. Since this repo mandates reading `AGENTS.md` at startup, Codex sessions will continue loading the old 015 plan even though `.specify/feature.json` now selects 017; update the AGENTS marker as well.
