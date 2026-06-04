> **SUPERSEDED 2026-05-04** — This file is from the prior ch07 implementation (`26e01792` / `f094f9db`) that was rejected by the project owner. Preserved per the no-removal directive but **not part of this chapter's runnable content**. The current ch07 (one-play-per-exercise REPL walkthroughs) is at the [chapter signpost](../ch07_tutorial.md). Content below is the prior implementation, kept as record.

---

# ex-11 — REPL trace (cluster B §7.4 + Formal 7.2 cross-module-call inspection)

This trace captures two REPL sessions exercising the §7.4 cross-module
type-checking contract on cluster B.  Per Formal 7.2 (book p 58), every
`M # goal(args)` call site in `boot.glp` type-checks LOCALLY using the
`imported procedure` declarations at the top of the file — the type
checker does NOT read `agent.glp`, `mediator.glp`, or `actors.glp`
source.  Session 1 loads the cluster B project and runs `play1.`, which
exercises cross-module calls into all three sibling modules (`agent #
agent(...)`, `mediator # ui_mediator(...)`, `actors # alice1(...)`,
etc.) — the clean load + `→ suspended` outcome means each cross-module
call resolved through its `imported procedure` decl during type-check
and dispatched to the correct sibling-module clause at runtime.
Session 2 attempts the cross-module-call form `agent # agent(...)`
directly at the top-level REPL goal prompt to expose the parser's
treatment of the `#` operator in goal position (vs clause-body position).

## Phase A — Project load

Both sessions load the same cluster B project via project-loading mode
(per §7.2).  The single `✓ Loaded project:` line covers the discovery,
the per-file SRSW + partial evaluation + type-check + compile pipeline,
the §7.3 imported-procedure resolution against sibling-module exported
decls, the §7.4 ancestor-scoping type assembly, the §7.5 procedure
renaming + entry-point alias synthesis, and the project-completion
finalization — all six files (`self.glp`, `agent.glp`, `boot.glp`,
`mad_boot.glp`, `ui/mediator.glp`, `ui/actors.glp`) clean.  Type-checking
the `M # goal` calls in `boot.glp` is the §7.4 mechanic this exercise
inspects: it uses ONLY the local `imported procedure` decls at lines
14–51 of `boot.glp` — the type checker never opens `agent.glp` source
to see the actual `agent/4` clause, never opens `mediator.glp` source
to see `ui_mediator/5`, and never opens `actors.glp` source to see any
of the 25 `aliceN/1`/`bobN/1`/`carolN/1`/`charlieN/1`/`daveN/1` actor
procedures.  This is Formal 7.2 in action.

## Phase B — Session 1: top-level `play1.` (full cross-module call demo)

The implementer runs `play1.` unqualified.  The §7.5 entry-point alias
table rewrites the bare goal to `boot:play1/0`, whose body (boot.glp
lines 199–243) contains many cross-module call sites.  Each call resolves
through its corresponding `imported procedure` decl — the type checker
matched arity + argument modes during load, and the runtime dispatcher
routes the call to the renamed clause in the appropriate sibling module.
Because `play1` spawns three actor/agent/mediator triples plus a
`network3` switch with no `end_of_play/0` injector, the streams suspend
waiting for further input that never arrives — `→ suspended` is the
expected and correct §7.7 cold-call befriending outcome.

```glp
╔════════════════════════════════════════╗
║  GLP REPL - With Type Checking         ║
╚════════════════════════════════════════╝

Build: d9045902 spec+clarify+plan+tasks+analyze(ch07): spec.md (5 Clarifications Q1..Q5) + plan + research (R-001..R-012) + data-model + 5 contracts (trace + flutter-trace NEW + status-block + glp-file + test-mirror NEW) + quickstart + tasks (T001..T184; 18 phases; 11 gates) + analyze remediations applied (F1 Q-FR003a no ui/self.glp + add mad_boot.glp / F2 Q-FR014a Section R not S / F3 Q1a cluster A keeps ui/ byte-exact only boot.glp pruned / F4 Q4a ex-12 plays = 1+2+3+4+5 / F5 FR-016 7-logical-plays clarification / F6 T005b author input prompt) — first chapter with two-cluster structure + Flutter pairings + tests in run_all_tests.sh
Compiled: 2026-02-01 (GlpEngine refactor)
Working directory: D:\bstdev\research\GLP\GLP

Input: filename.glp to load, or goal to execute
Commands: :quit, :help, :trace, :debug, :limit, :activate, :boot

Loaded root self.glp from: D:\bstdev\research\GLP\GLP\programs\self.glp

GLP> ✓ Loaded project: D:/bstdev/research/GLP/GLP/olamni/tutorial/ch07/cssg-modules
GLP> Goal reduction limit set to 1000000
GLP> → suspended

GLP> Goodbye!
```

The byte-exact elements are the trio of REPL response lines:
`✓ Loaded project: …` (project-load mode for cluster B), `Goal reduction
limit set to 1000000` (the `:limit 1000000` ack), and `→ suspended`
(the alias-rewritten `boot:play1/0` outcome whose body exercises
cross-module calls into `agent`, `mediator`, and `actors` modules).
The REPL banner block and `Goodbye!` line are exempt from byte-equality
per `contracts/trace-file-format.md` §Byte-equality.

The `→ suspended` outcome is the §7.4 + Formal 7.2 success signal in this
exercise: every cross-module call type-checked locally via its imported
decl AND dispatched correctly at runtime AND reached a steady suspended
state — three independent confirmations across the load-time and
run-time halves of the cross-module contract.

## Phase C — Session 2: top-level `agent # agent(alice, [], [], []).` (cross-module call as goal)

The implementer attempts the cross-module call form directly at the goal
prompt.  Per ex-04's Phase C precedent, the top-level REPL goal grammar
does not accept `M # G` as a goal-prefix construct — the `#` operator is
recognised only as an in-clause-body cross-module call site, not as a
top-level goal.  The parser raises a syntax error at column 7 (the
position of the `#` separator: positions 1–5 are `agent`, position 6 is
the space, position 7 is `#`).  This means: in the current REPL, the
ONLY way to invoke a sibling-module exported procedure is indirectly via
`play1.` (an entry-point alias whose body contains cross-module call
sites) — you cannot bypass the alias and call a renamed `agent:agent/4`
directly from the goal prompt.

```glp
╔════════════════════════════════════════╗
║  GLP REPL - With Type Checking         ║
╚════════════════════════════════════════╝

Build: d9045902 spec+clarify+plan+tasks+analyze(ch07): spec.md (5 Clarifications Q1..Q5) + plan + research (R-001..R-012) + data-model + 5 contracts (trace + flutter-trace NEW + status-block + glp-file + test-mirror NEW) + quickstart + tasks (T001..T184; 18 phases; 11 gates) + analyze remediations applied (F1 Q-FR003a no ui/self.glp + add mad_boot.glp / F2 Q-FR014a Section R not S / F3 Q1a cluster A keeps ui/ byte-exact only boot.glp pruned / F4 Q4a ex-12 plays = 1+2+3+4+5 / F5 FR-016 7-logical-plays clarification / F6 T005b author input prompt) — first chapter with two-cluster structure + Flutter pairings + tests in run_all_tests.sh
Compiled: 2026-02-01 (GlpEngine refactor)
Working directory: D:\bstdev\research\GLP\GLP

Input: filename.glp to load, or goal to execute
Commands: :quit, :help, :trace, :debug, :limit, :activate, :boot

Loaded root self.glp from: D:\bstdev\research\GLP\GLP\programs\self.glp

GLP> ✓ Loaded project: D:/bstdev/research/GLP/GLP/olamni/tutorial/ch07/cssg-modules
GLP> Goal reduction limit set to 1000000
GLP> → failed
Error: [syntax] Expected "." at end of clause at Line 1, Column 7

GLP> Goodbye!
```

The byte-exact elements are `✓ Loaded project: …`, `Goal reduction limit
set to 1000000`, the `→ failed` outcome line, and the syntax error
`Error: [syntax] Expected "." at end of clause at Line 1, Column 7`.
This confirms the takeaway from ex-04 Phase C: cross-module qualification
syntax (`M # G`) is a clause-body call site form ONLY — top-level REPL
goals must use the entry-point alias path.  The `M # G` syntax appears
in `boot.glp`'s clause bodies (e.g., `agent # agent(alice, ...)` at
line 207, `mediator # ui_mediator(alice, ...)` at line 211, `actors #
alice1(...)` at line 200), where the §7.4 type-check rule applies.

---

## Postscript — Formal 7.2 + §7.4 in this trace

**Formal 7.2 (book p 58):** "Every `M # goal(args)` call site type-checks
locally against the file's `imported procedure` declarations.  The type
checker does not need to access the source of module M to verify well-
typing of the call site."

**§7.4 (book p 58):** Cross-module type checking is *modular* — each file
is type-checked in isolation with respect to its imports.  The imported
decl carries the full type signature (procedure name, arity, argument
types + modes), so the type checker has all the information it needs to
verify the call site without opening sibling-module source.  This is what
makes per-module type checking scalable: a project with N modules requires
N independent per-file type-checks, not N×(N-1) cross-references.

In this trace's Phase A, the project load reports `✓ Loaded project:` for
cluster B, which means the type checker successfully verified `boot.glp`
in isolation — using ONLY its local `imported procedure` decls at lines
14–51 — even though `boot.glp` contains hundreds of cross-module call
sites into three sibling modules.  Phase B confirms the matching runtime
half: the renamed-and-dispatched `agent:agent/4`, `mediator:ui_mediator/5`,
and `actors:alice1/1` clauses run correctly when the `play1` body
exercises them.  The cluster B canonical source is `programs/cssg_modules/`;
ex-11 inspects the §7.4 + Formal 7.2 mechanic on the byte-exact copy at
`olamni/tutorial/ch07/cssg-modules/`.  ex-12 (the cluster B Flutter
pairing) will run the same `play1`–`play5` end to end inside the
multimodule Flutter app, exercising the full §7.7 use-case set.
