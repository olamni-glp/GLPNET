> **SUPERSEDED 2026-05-04** — This file is from the prior ch07 implementation (`26e01792` / `f094f9db`) that was rejected by the project owner. Preserved per the no-removal directive but **not part of this chapter's runnable content**. The current ch07 (one-play-per-exercise REPL walkthroughs) is at the [chapter signpost](../ch07_tutorial.md). Content below is the prior implementation, kept as record.

---

# Exercise 09 — Cluster B CSSG accept + reject (plays 4–5 per Q4a)

Welcome to chapter 7, exercise 9 — the second cluster B exercise.
Where ex-08 covered cluster B's plays 1–3 (the §7.3 cold-call subset
mirroring cluster A), this exercise picks up at plays 4–5: the first
two §7.7 CSSG plays, which introduce **parent-mediated child
introductions** with a parent-approval gate that has no analogue in
§7.3.  Per spec Q-amendment Q4a, ex-09 covers play4 (all four parties
accept) + play5 (Bob — the second parent — rejects).

## What you'll learn

* The §7.7 parent-mediated child introduction protocol (book p 61, use
  case (c)) — a 4-party handshake gated by parent approval, where a
  child A cannot connect to child B until both parents have explicitly
  approved.
* The parent-approval-gate mechanism: each parent must respond to a
  `child_befriend(...)` notification with an explicit
  `approve_child_intro(...)` or `reject_child_intro(...)` decision
  before the child-side handshake can proceed.
* The structural difference between the §7.3 protocol (cold-call,
  2-party, no approval gate) and the §7.7 protocol (CSSG, 4-party,
  with parent-approval gate plus cross-agent `child(...)` channels
  routed via `output(child(...), ...)` + `merge/3`).
* How a parent rejection short-circuits the protocol — the rejection
  propagates through the agent into a `rejected(...)` notification on
  the initiator-child's mediator, and the other-side child is never
  engaged.

## The CSSG protocol — message flow

Reading boot.glp's `play4` (lines 288–339) + `play5` (lines 345–396)
together with the actor scripts in `ui/actors.glp`, the §7.7 CSSG
protocol proceeds in four phases:

1. **Parent-level befriending** (§7.3 reused).  Alice issues
   `connect(bob)`.  Bob's mediator surfaces `befriend(alice, ReqId)`.
   Bob's actor responds `decision(yes, alice, ReqId)` — a §7.3
   cold-call exchange identical in shape to plays 1–3.  The parents
   are now friends.
2. **Child-introduce** (§7.7-specific).  Alice's actor, on
   `connected(bob)`, issues `child_introduce(carol, bob, dave)` — "I
   want my child Carol to befriend your (bob's) child Dave."  This is
   a new command kind, unique to §7.7.
3. **Parent-approval gate** (§7.7-specific, the key new mechanism).
   Bob's mediator surfaces `child_befriend(alice, carol, ReqId)`.
   Bob's actor must respond with EXACTLY ONE of:
   * `approve_child_intro(carol, dave, ReqId)` — proceed to phase 4;
   * `reject_child_intro(carol, ReqId)` — abort the protocol.

   The protocol cannot proceed without Bob's explicit decision.  In
   play4 Bob approves; in play5 Bob rejects.
4. **Child-side handshake** (only on approval).  When both parents
   approve, each child's mediator surfaces a `child_befriend(...)`
   notification, and each child responds with
   `accept_child_intro(...)`.  The cross-agent `child(...)` channels
   then carry direct child-to-child messages — in play4 the children
   exchange `'Hi Dave'` and `'Hi Carol'`.

When a parent rejects (play5), phase 4 is replaced by **rejection
propagation**: the rejection becomes a `rejected(...)` notification on
the initiator-child's mediator, the child seals its command stream,
and the other-side child is never engaged.

## Play 4 — All four accept

`play4`'s actors (`ui/actors.glp` lines 269–333) walk through every
phase end-to-end.

**alice4** (lines 269–278) — issues `connect(bob)`; on `connected(bob)`
issues `child_introduce(carol, bob, dave)` and her command stream ends
there.  The introduction is fire-and-forget from Alice's side.

**bob4** (lines 280–294) — accepts the parent-level befriend with
`decision(yes, alice, ReqId)` (phase 1).  When the
`child_befriend(alice, carol, ReqId)` notification arrives (phase 3),
`bob4_wait_child_intro` matches it and emits
`approve_child_intro(carol, dave, ReqId)`.  This is the
**parent-approval gate opening on Bob's side**.

**carol4** (lines 296–316) — her clause head matches
`child_befriend(alice, dave, ReqId)` and immediately emits
`accept_child_intro(dave, ReqId)` (phase 4).  When the cross-agent
`child(...)` channels carry `connected(dave)`, Carol sends `'Hi Dave'`
and waits for Dave's reply.

**dave4** (lines 318–333) — clause-head matches
`child_befriend(bob, carol, ReqId)` and emits
`accept_child_intro(carol, ReqId)`.  On `received(carol, _)` he
replies `'Hi Carol'`.

The play4 trace (Phase B for play4 in `ex-09-repl-trace.md`) ends at
`→ suspended` — the protocol completed all four phases, the children
exchanged greetings, and the channels remain open by design.

## Play 5 — Bob rejects

`play5`'s actors (`ui/actors.glp` lines 339–381) are structurally
identical to play4 through phases 1–2, then diverge at the
parent-approval gate.

**alice5** (lines 339–348) — IDENTICAL to alice4.  Alice has no
visibility (yet) that Bob will reject.

**bob5** (lines 350–364) — the decisive divergence.  Bob still accepts
the parent-level befriend.  But at the parent-approval gate, where
bob4 emitted `approve_child_intro(carol, dave, ReqId)`:

```glp
procedure bob5_wait_child_intro(UserNotifyStream?, UserCmdStream).
bob5_wait_child_intro([child_befriend(alice, carol, ReqId)|_],
                      [reject_child_intro(carol, ReqId?)]) :-
    ground(ReqId?) | true.
```

bob5 emits `reject_child_intro(carol, ReqId)`.  Note the absence of
`dave` — Bob is refusing on Carol's behalf without involving Dave at
all.

**carol5** (lines 366–378) — propagates the rejection.  Carol's
clause head matches `child_befriend(alice, dave, ReqId)` and (not yet
knowing of Bob's rejection) emits `accept_child_intro(dave, ReqId)`.
But the reject from Bob propagates back through Alice's agent into a
`rejected(dave)` notification on Carol's mediator stream;
`carol5_wait_rejected` matches that and seals the command stream with
`[]`.

**dave5** (lines 380–381) is trivially minimal:

```glp
exported procedure dave5(ActorChannel?).
dave5(ch(_, [])).
```

Dave never receives a `child_befriend(...)` notification at all,
because Bob's rejection short-circuits the protocol before Dave's
mediator is engaged.  This is structural evidence that the
parent-approval gate intercepts the protocol BEFORE the other-side
child is engaged — the rejection is parent-level, not child-level.

The play5 trace ends at `→ suspended` — Carol sealed her command
stream, Dave never engaged, and the channels remain open by design.

## Why CSSG-specific

The §7.3 cold-call protocol cannot model parent-mediated child
introductions:

1. **No parent-approval gate.**  §7.3's befriending is a 2-party
   handshake (befriender + befriendee).  In §7.7, the parent is a
   third party who can intercept and approve/reject.
2. **No cross-agent `child(...)` channels.**  §7.3 routes everything
   through the network.  §7.7 adds `output(child(carol), ...)` +
   `merge/3` wiring (boot.glp lines 294–300, 305–311 for play4) so
   that parents can talk to their children's agents directly.
3. **No 4-agent topology.**  §7.3 plays use 3 agents + `network3/3`;
   §7.7 plays use 4 agents + `network2/2` + per-agent
   `output(child(...), ...)` channels.

The §7.7 protocol is the **Child-Safe Social Graph** (CSSG) protocol —
named because the parent-approval gate provides safety for child
agents: a child cannot be approached by another child without the
parent's explicit consent.  This safety property is expressed
structurally in the clause structure of `bob4_wait_child_intro` /
`bob5_wait_child_intro` / `carol4` / `carol5` / `dave4` / `dave5` — it
is a logic-level guarantee, not an external policy.

## Run the plays

```bash
cd D:/bstdev/research/GLP/GLP && printf "%s\n:limit 1000000\nplay4.\n:quit\n" "$(pwd -W)/olamni/tutorial/ch07/cssg-modules" | "/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill 2>&1 | head -40

cd D:/bstdev/research/GLP/GLP && printf "%s\n:limit 1000000\nplay5.\n:quit\n" "$(pwd -W)/olamni/tutorial/ch07/cssg-modules" | "/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill 2>&1 | head -40
```

Cross-check: `ex-09-repl-trace.md` records both verbatim REPL sessions.
Both end at `→ suspended` — per CLAUDE.md §12 a valid play outcome,
indicating the protocol completed without fault and the channels
remain open by design.

## Multimodule-project-derivation note

Per the `multimodule-project-derivation` cross-chapter relationship
contract (research R-008), cluster B's source canonical is
`programs/cssg_modules/` (the book §7.7 validation example, p 61), and
ALL cluster B files — `self.glp`, `agent.glp`, `boot.glp`,
`ui/mediator.glp`, `ui/actors.glp` — are byte-exact from the canonical.
Cluster B is the unmodified §7.7 example; cluster A was the pruned
3-agent subset (only `boot.glp` derived).  Running play4 + play5 from
cluster B exercises the canonical CSSG protocol end-to-end with no
derivation distance to factor out.

## Next

Exercise 10 covers the remaining child-side rejection variants: play6
(Carol — initiator-child — rejects) and play7 (Dave — second-parent's
child — rejects).  Together with ex-09's play4 (all-accept) and play5
(parent rejects), ex-10's two plays complete the §7.7 use case (c)
quartet of accept + parent-reject + child-reject branches.  See
`ex-10-tutorial.md` and `ex-10-repl-trace.md`.
