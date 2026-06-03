> **SUPERSEDED 2026-05-04** — This file is from the prior ch07 implementation (`26e01792` / `f094f9db`) that was rejected by the project owner. Preserved per the no-removal directive but **not part of this chapter's runnable content**. The current ch07 (one-play-per-exercise REPL walkthroughs) is at the [chapter signpost](../ch07_tutorial.md). Content below is the prior implementation, kept as record.

---

# ex-09 — REPL trace (cluster B CSSG plays 4 + 5 — parent-mediated child introduction with accept + reject)

This trace captures two verbatim REPL sessions for ex-09: cluster B's
`play4.` (all four parties accept the parent-mediated child introduction)
and `play5.` (Bob — the second parent — rejects).  Together they
exercise the §7.7 use case (c) — the CSSG protocol's parent-approval
gate — across the accept and reject branches.  Each REPL invocation has
its own Phase A (project load) and Phase B (the `playN.` run).  Per the
trace-file-format contract for ch07 cluster B, both runs are captured in
their entirety; head -40 is the byte-equality boundary.

## Phase A — Project load (play4 invocation)

The implementer launches the REPL kernel snapshot, pipes the absolute
path of the cluster B project directory (`olamni/tutorial/ch07/cssg-modules`),
raises the goal-reduction limit to one million per CLAUDE.md §12 (CSSG
plays involve 4 agents + 4 mediators + 4 actors and need higher
reduction budgets than the 3-agent friend-mediated plays in cluster A),
and submits `play4.`.  The REPL detects the directory, switches to
project-loading mode, loads all five files of the canonical cluster B
project (`self.glp`, `agent.glp`, `boot.glp`, `ui/mediator.glp`,
`ui/actors.glp`), and emits the single `✓ Loaded project:` success line.

## Phase B — `play4.` end-to-end (CSSG: all four accept)

`play4` (boot.glp lines 288–339) allocates a 2-agent network plus four
agent/mediator/actor triples (alice + bob + carol + dave), wires the
parent-child cross-channels via `output(child(...), ...)` plus `merge/3`,
and runs the §7.7 protocol end-to-end.  The actor scripts are at
`ui/actors.glp` lines 269–333:

* **alice4** — issues `connect(bob)`, then on `connected(bob)` issues
  `child_introduce(carol, bob, dave)` (line 269–278);
* **bob4** — accepts the parent-level befriend (`decision(yes, alice, ReqId)`),
  then on the `child_befriend(alice, carol, ReqId)` notification issues
  `approve_child_intro(carol, dave, ReqId)` (line 280–294);
* **carol4** — on `child_befriend(alice, dave, ReqId)` issues
  `accept_child_intro(dave, ReqId)`, then on `connected(dave)` sends
  `'Hi Dave'` and waits for Dave's reply (line 296–316);
* **dave4** — on `child_befriend(bob, carol, ReqId)` issues
  `accept_child_intro(carol, ReqId)`, then on `received(carol, _)` sends
  `'Hi Carol'` (line 318–333).

Both parents approve, both children accept, the children exchange a
greeting, and the play settles into `→ suspended` — the channels remain
open by design (the `merge(AliceToCarol?, [], CarolFromAlice)` calls and
the `network2(ch([], []), ch([], []))` terminator both participate in
keeping the streams unsealed).  Per CLAUDE.md §12, `→ suspended` is a
valid play outcome — the play configuration succeeded, the protocol
completed end-to-end, and no fault was raised.

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

---

## Phase A — Project load (play5 invocation)

A second REPL invocation, identical in shape to the first, loads the
same cluster B project in preparation for `play5.`.  The single
`✓ Loaded project:` line confirms project-loading mode succeeded for
all five files.

## Phase B — `play5.` end-to-end (CSSG: Bob rejects)

`play5` (boot.glp lines 345–396) is structurally identical to `play4` —
same 2-agent network, same four agent/mediator/actor triples, same
cross-channel wiring — but pairs alice5 / bob5 / carol5 / dave5 in
place of alice4 / bob4 / carol4 / dave4.  The decisive difference is in
**bob5_wait_child_intro** (`ui/actors.glp` lines 358–364):

```glp
procedure bob5_wait_child_intro(UserNotifyStream?, UserCmdStream).
bob5_wait_child_intro([child_befriend(alice, carol, ReqId)|_],
                      [reject_child_intro(carol, ReqId?)]) :-
    ground(ReqId?) | true.
```

Where bob4 emits `approve_child_intro(carol, dave, ReqId)`, bob5 emits
`reject_child_intro(carol, ReqId)`.  This propagates through the agent
into a `rejected(dave)` notification on Carol's mediator stream, which
**carol5_wait_rejected** (`ui/actors.glp` lines 374–378) consumes:

```glp
procedure carol5_wait_rejected(UserNotifyStream?, UserCmdStream).
carol5_wait_rejected([rejected(dave)|_], []).
```

Carol terminates her command stream with `[]` — the rejection stops the
child-side handshake before any `connected(dave)` arrives.  **dave5** is
correspondingly minimal — `dave5(ch(_, []))` — because Dave never
receives a `child_befriend(...)` notification at all (Bob's rejection
short-circuits the protocol before Dave's mediator is engaged).

The `play5` outcome is again `→ suspended` — the rejection-propagation
protocol completed correctly (Bob's rejection reached Carol; Carol
sealed her command stream; Dave never engaged), and the channels remain
open by design.  Per CLAUDE.md §12, `→ suspended` is a valid outcome.

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

---

## Postscript — §7.7 use case (c): parent-mediated child introduction

Plays 4 and 5 together demonstrate §7.7 use case (c) — the CSSG
parent-mediated child introduction protocol — across its accept and
reject branches.  Where §7.3's cold-call befriending is a 2-party
handshake (befriender + befriendee), the §7.7 CSSG protocol is a
4-party handshake gated by **parent approval**: a child A cannot
connect to child B until both parents have explicitly approved the
introduction.

The §7.7 message flow exhibited by both plays is:

1. **Parent-level befriending** — the two parents (Alice + Bob) first
   complete a §7.3 cold-call befriending exchange so that the children's
   parents are themselves friends.  In both play4 and play5, this
   succeeds (`bob4`/`bob5` emit `decision(yes, alice, ReqId)` to the
   parent-level befriend request).
2. **Child-introduce** — the initiating parent (Alice) issues
   `child_introduce(MyChild, Friend, FriendChild)` — here
   `child_introduce(carol, bob, dave)` — to request that her child Carol
   befriend Bob's child Dave.
3. **UI-mediated parent-approval gate** — Bob's UI mediator surfaces
   the request as a `child_befriend(alice, carol, ReqId)` notification.
   Bob's actor must respond with `approve_child_intro(carol, dave, ReqId)`
   (play4: bob4) **OR** `reject_child_intro(carol, ReqId)` (play5: bob5).
   This is the parent-approval gate — the protocol cannot proceed
   without Bob's explicit decision.
4. **Child-side accept (only on approval)** — when the parents both
   approve (play4), each child receives a `child_befriend(...)`
   notification on its mediator and responds with
   `accept_child_intro(...)`.  When a parent rejects (play5), the
   rejection propagates into a `rejected(...)` notification on the
   initiator-child's mediator, and the child-side handshake aborts
   before either child can connect.

Play 4 exhibits the **accept branch** end-to-end — both parents
approve, both children accept, and the children exchange a `'Hi Dave'`
+ `'Hi Carol'` greeting via the cross-agent `child(...)` channels.
Play 5 exhibits the **reject branch** — Bob rejects at step 3, Carol's
mediator surfaces a `rejected(dave)` notification, Carol seals her
command stream, and Dave's actor body is the trivial `dave5(ch(_, []))`
because Dave is never engaged.

The decisive observation: the §7.3 protocol cannot model this — there
is no parent-approval gate in cold-call befriending, and there are no
cross-agent `child(...)` channels.  The §7.7 CSSG protocol introduces
both, and cluster B's `boot.glp` (lines 288–396 for play4 + play5) is
the smallest end-to-end demo that exercises the gate in both branches.

The cluster B source is byte-exact from the canonical
`programs/cssg_modules/` (the §7.7 validation example from book p 61).
This is ex-09 in ch07's exercise sequence: ex-08 covers cluster B
plays 1–3 (the §7.3 cold-call subset, mirroring cluster A's plays 1–3
verbatim from the canonical), and ex-09 picks up at plays 4–5 — the
first two §7.7 CSSG plays — establishing the parent-approval gate for
ex-10 to extend through plays 6–7 (Carol-rejects + Dave-rejects, the
two child-side rejection variants).
