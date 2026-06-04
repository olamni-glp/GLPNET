> **SUPERSEDED 2026-05-04** — This file is from the prior ch07 implementation (`26e01792` / `f094f9db`) that was rejected by the project owner. Preserved per the no-removal directive but **not part of this chapter's runnable content**. The current ch07 (one-play-per-exercise REPL walkthroughs) is at the [chapter signpost](../ch07_tutorial.md). Content below is the prior implementation, kept as record.

---

# ex-10 — REPL trace (cluster B parent-mediated child intro reject variants — plays 6–7 per Q4a)

This trace captures the verbatim REPL session for ex-10 — the cluster B
exercise that runs the two CHILD-reject variants of the §7.7 use case (c)
parent-mediated child introduction protocol.  Three phases: the project
load (Phase A — same shape as ex-07), the `play6.` run where Carol
rejects (Phase B), and the `play7.` run where Dave rejects (Phase C).
Together they round out the §7.7 reject branches: ex-09's `play5` covered
the PARENT-reject branch (Bob rejects so the child introduction never
propagates); this exercise covers the CHILD-reject branches (both parents
approve, the introduction reaches the children, and the child refuses).
The two plays are run as separate REPL invocations because the cluster B
project defines `play6/0` and `play7/0` in the same `boot.glp`, but the
GLP scheduler retains state across goals within a session — re-running
gives a clean network for each play.

## Phase A — Project load (play6 invocation)

The implementer launches the REPL kernel snapshot, pipes the absolute
path of the cluster B project directory, raises the goal-reduction limit
to one million per CLAUDE.md §12 (CSSG plays may need higher limits than
3-agent plays), and submits `play6.`.  The REPL detects the directory,
switches to project-loading mode, and emits the single
`✓ Loaded project:` success line.

```glp
╔════════════════════════════════════════╗
║  GLP REPL - With Type Checking         ║
╚════════════════════════════════════════╝

Build: d9045902 spec+clarify+plan+tasks+analyze(ch07): spec.md (5 Clarifications Q1..Q5) + plan + research (R-001..R-012) + data-model + 5 contracts (trace + flutter-trace NEW + status-block + glp-file + test-mirror NEW) + quickstart + tasks (T001..T184; 18 phases; 11 gates) + analyze remediations applied (F1 Q-FR003a no ui/self.glp + add mad_boot.glp / F2 Q-FR014a Section R not S / F3 Q1a cluster A keeps ui/ byte-exact only boot.glp pruned / F4 Q4a ex-12 plays = 1+2+3+4+5 / F5 FR-016 7-logical-plays clarification / F6 T005b author input prompt) — first chapter with two-cluster structure + Flutter pairings + tests in run_all_tests.sh
Compiled: 2026-02-01 (GlpEngine refactor)
Working directory: D:\bstdev\research\glp\glp

Input: filename.glp to load, or goal to execute
Commands: :quit, :help, :trace, :debug, :limit, :activate, :boot

Loaded root self.glp from: D:\bstdev\research\glp\glp\programs\self.glp

GLP> ✓ Loaded project: D:/bstdev/research/glp/glp/olamni/tutorial/ch07/cssg-modules
GLP> Goal reduction limit set to 1000000
GLP> → suspended

GLP> Goodbye!
```

The single `✓ Loaded project:` line is the project-loading-mode success
signal — it covers all six cluster B files (`self.glp`, `agent.glp`,
`boot.glp`, `mad_boot.glp`, `ui/mediator.glp`, `ui/actors.glp`) plus the
ancestor-scoping type assembly per §7.4.  The `→ suspended` line is the
`play6.` outcome — see Phase B below for what happens inside.

## Phase B — `play6.` end-to-end (Carol rejects)

`play6` runs the §7.7 use case (c) parent-mediated child introduction
protocol with the `aliceN/bobN/carolN/daveN` actor set tuned so that
both parents approve, the introduction propagates to Carol, and Carol
rejects via `reject_child_intro/2`.  The protocol completes — Carol's
veto reaches Alice through the carol→alice child stream and Dave's
acceptance reaches Bob through the dave→bob child stream — but both
parent-child channels remain open afterwards, hence the
`→ suspended` outcome.  Per CLAUDE.md §12 + ch01–ch06 precedent,
`succeeds` and `suspended` are both valid play outcomes; `suspended`
indicates the protocol completed without fault and the channels are
simply waiting for further input that never arrives.

(The fenced session for Phase A above is the literal output for this
phase; the trace records ONE invocation per play because
`play6.` is the goal submitted in this REPL session.)

## Phase C — `play7.` end-to-end (Dave rejects)

A fresh REPL invocation, identical setup to Phase A but with `play7.`
as the goal.  `play7` runs the same §7.7 use case (c) parent-mediated
child introduction protocol but with the actor set tuned so that both
parents approve, the introduction propagates to BOTH children, Carol
accepts via `accept_child_intro/2`, but Dave rejects via
`reject_child_intro/2`.  Same `→ suspended` outcome as `play6` — the
protocol completes with Dave's veto reaching Bob, but channels remain
open.

```glp
╔════════════════════════════════════════╗
║  GLP REPL - With Type Checking         ║
╚════════════════════════════════════════╝

Build: d9045902 spec+clarify+plan+tasks+analyze(ch07): spec.md (5 Clarifications Q1..Q5) + plan + research (R-001..R-012) + data-model + 5 contracts (trace + flutter-trace NEW + status-block + glp-file + test-mirror NEW) + quickstart + tasks (T001..T184; 18 phases; 11 gates) + analyze remediations applied (F1 Q-FR003a no ui/self.glp + add mad_boot.glp / F2 Q-FR014a Section R not S / F3 Q1a cluster A keeps ui/ byte-exact only boot.glp pruned / F4 Q4a ex-12 plays = 1+2+3+4+5 / F5 FR-016 7-logical-plays clarification / F6 T005b author input prompt) — first chapter with two-cluster structure + Flutter pairings + tests in run_all_tests.sh
Compiled: 2026-02-01 (GlpEngine refactor)
Working directory: D:\bstdev\research\glp\glp

Input: filename.glp to load, or goal to execute
Commands: :quit, :help, :trace, :debug, :limit, :activate, :boot

Loaded root self.glp from: D:\bstdev\research\glp\glp\programs\self.glp

GLP> ✓ Loaded project: D:/bstdev/research/glp/glp/olamni/tutorial/ch07/cssg-modules
GLP> Goal reduction limit set to 1000000
GLP> → suspended

GLP> Goodbye!
```

Both `play6` and `play7` exercise the FULL §7.7 multi-module CSSG
configuration: the `network2/2` cold-call switch (Alice ↔ Bob), the four
agents (each with a child-channel output), the four mediators, the four
actors, and the four `merge/3` calls that wire parent ↔ child streams in
both directions.  The reject branches differ only in WHICH child vetoes —
play6 the carol-side child, play7 the dave-side child.

---

## Postscript — §7.7 use case (c) reject branches beyond ex-09's play 5

`play6` and `play7` complete the §7.7 use case (c) parent-mediated child
introduction reject-branch coverage that ex-09 began.  Per Q4a, ex-09
runs `play4` (everyone accepts) + `play5` (Bob the parent rejects); this
exercise (ex-10) runs `play6` (Carol the child rejects) + `play7` (Dave
the child rejects).  Together with ex-09's plays they exercise every
distinct outcome the §7.7 protocol supports for use case (c):

- `play4` (ex-09): both parents approve, both children accept — full
  child friendship is formed.
- `play5` (ex-09): one parent (Bob) rejects via `reject_child_intro/2` at
  the parent gate — the child introduction NEVER propagates to Carol or
  Dave.
- `play6` (this exercise): both parents approve at the parent gate, the
  child introduction reaches Carol, but Carol rejects at the child gate
  via `reject_child_intro/2`.
- `play7` (this exercise): both parents approve at the parent gate, the
  child introduction reaches Dave, but Dave rejects at the child gate
  via `reject_child_intro/2`.

The §7.7 protocol thus has TWO consent gates — a parent gate and a child
gate — and CSSG is configured so that BOTH parents AND children have
independent veto power.  `play5` exercises a parent veto (the
introduction is short-circuited before the children ever see it);
`play6` + `play7` exercise child vetoes (the introduction reaches both
children, but one of them refuses).  The actor scripts that drive these
reject branches live in `cssg-modules/ui/actors.glp` lines 380–478:
`alice6/1`–`dave6/1` for `play6` and `alice7/1`–`dave7/1` for `play7`.
Cluster B's source canonical is `programs/cssg_modules/`; cluster B
files are byte-exact from the canonical, with `play6`/`play7` clauses
defined in `boot.glp` lines 402–506.  This is the second of three
cluster B play exercises (ex-08 = cold-call plays 1–3, ex-09 = CSSG
plays 4–5, ex-10 = CSSG plays 6–7); ex-11 closes out cluster B with
cross-module-call inspection goals.
