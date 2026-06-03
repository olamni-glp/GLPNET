> **SUPERSEDED 2026-05-04** — This file is from the prior ch07 implementation (`26e01792` / `f094f9db`) that was rejected by the project owner. Preserved per the no-removal directive but **not part of this chapter's runnable content**. The current ch07 (one-play-per-exercise REPL walkthroughs) is at the [chapter signpost](../ch07_tutorial.md). Content below is the prior implementation, kept as record.

---

# Exercise 08 — Cluster B cold-call befriending (plays 1–3)

Welcome to chapter 7, exercise 8 — the first cluster B play-sequence
exercise.  Where ex-07 just loaded the cluster B project, this exercise
runs three concrete plays through it: `play1`, `play2`, `play3`.
Together they exercise the §7.3 `agent/4` clause's three accept / reject
branches by varying ONLY the actor scripts — every other module
(`agent.glp`, `ui/mediator.glp`, `ui/actors.glp`'s shared utilities,
`boot.glp`'s `network3/3` switch, `self.glp`'s ancestor types) is
identical across the three plays.  The actor scripts encode the user's
decisions, so by reading them you can see exactly which branch each
play visits.

## What you'll learn

- The §7.3 `agent/4` clause has three observable branches — accept,
  asymmetric, and reject — selected by the user's response on the
  `befriend(From, Resp?)` channel and the follow-up
  `befriend_intro(...)` channel.
- The same multimodule project (cluster B) can host arbitrarily many
  plays without modification — each play is just a different goal in
  `boot.glp` plus its accompanying actor scripts in `ui/actors.glp`.
- `→ suspended` is the expected outcome for all three plays; the
  difference between accept / asymmetric / reject is observable in
  the actor scripts (which messages they emit, which they wait for),
  not in the trace's terminal status.

## The cold-call protocol (§7.3)

The cold-call befriending protocol from book §7.3 is a three-step
asynchronous handshake that the §7.3 `agent/4` clause encodes:

1. **`connect(Target)`** — the initiating actor (Alice) submits a
   `connect(bob)` command on its actor channel.  The mediator forwards
   this to the agent, which emits a `befriend(alice, ReqId)` request on
   the network out-stream.  The network switch (`network3/3`)
   delivers it to Bob's network in-stream as
   `msg(alice, befriend(alice, ReqId))`.
2. **`befriend(From, Resp?)` → user decision** — Bob's agent receives
   the `befriend(alice, ReqId)` and surfaces it to Bob's mediator as a
   user-visible notification.  Bob's actor sees `befriend(alice, ReqId)`
   and chooses a `decision(yes|no, alice, ReqId?)` response.  In
   plays 1–3, every `bobN`'s response is `decision(yes, alice, ReqId?)`
   — the cold-call between Alice and Bob always succeeds.
3. **Friend-mediated introduction** — once Alice and Bob are friends,
   Bob can introduce Alice to a third party (Charlie) via
   `[introduce(alice, charlie)]`.  Alice and Charlie each then receive
   `befriend_intro(bob, OTHER, ReqId)` notifications and choose
   between `accept_intro(OTHER, ReqId?)` and `reject_intro(OTHER, ReqId?)`.
   This is where plays 1–3 diverge.

Each play's actor scripts (`alice1` / `bob1` / `charlie1`,
`alice2` / `bob2` / `charlie2`, `alice3` / `bob3` / `charlie3`) are
defined in `cssg-modules/ui/actors.glp` and selected by the
corresponding `play1` / `play2` / `play3` clauses in
`cssg-modules/boot.glp`.

## Play 1 — Both accept

Both Alice and Charlie accept the friend-mediated introduction.  This
is the §7.3 happy-path acceptance branch:

- Alice issues `connect(bob)` and waits.
- Bob accepts the cold-call (`bob1`'s
  `decision(yes, alice, ReqId?)`).  Alice gets `connected(bob)`.
- Alice sends "hello" to Bob (`send(bob, hello)`).
- Bob receives the message, then issues `[introduce(alice, charlie)]`.
- The agent sends `befriend_intro(bob, charlie, ReqId)` to Alice and
  `befriend_intro(bob, alice, ReqId)` to Charlie.
- Alice's `alice1_wait_intro` matches `befriend_intro(bob, charlie, ReqId)`
  and replies `accept_intro(charlie, ReqId?)`.
- Charlie's `charlie1_wait_intro` matches
  `befriend_intro(bob, alice, ReqId)` and replies
  `accept_intro(alice, ReqId?)`.
- Alice and Charlie now see `connected(charlie)` and `connected(alice)`
  respectively; Alice sends "Hi Charlie", Charlie sends "Hi Alice"; each
  side receives the reply and the actor scripts terminate.

```bash
cd D:/bstdev/research/GLP/GLP && printf "%s\n:limit 1000000\nplay1.\n:quit\n" "$(pwd -W)/olamni/tutorial/ch07/cssg-modules" | "/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill 2>&1 | head -40
```

Cross-check: `ex-08-repl-trace.md` Phase B records the verbatim REPL
session for this play.

## Play 2 — Alice accepts, Charlie rejects

Alice accepts the friend-mediated introduction; Charlie rejects it.
This is the §7.3 asymmetric branch:

- Steps 1–4 are the same as play1: Alice→Bob cold-call succeeds, Bob
  introduces Alice and Charlie.
- Alice's `alice2_wait_intro` matches
  `befriend_intro(bob, charlie, ReqId)` and replies
  `accept_intro(charlie, ReqId?)` — Alice would accept.
- Charlie's `charlie2_wait_intro` matches
  `befriend_intro(bob, alice, ReqId)` but replies
  `[reject_intro(alice, ReqId?)]` — Charlie rejects.
- Because Charlie rejected, the agent sends Alice a
  `rejected(charlie)` notification rather than `connected(charlie)`.
- Alice's follow-up `alice2_wait_rejected` consumes the
  `rejected(charlie)` notification and terminates without sending
  anything further.
- No friend channel is established between Alice and Charlie.

```bash
cd D:/bstdev/research/GLP/GLP && printf "%s\n:limit 1000000\nplay2.\n:quit\n" "$(pwd -W)/olamni/tutorial/ch07/cssg-modules" | "/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill 2>&1 | head -40
```

Cross-check: `ex-08-repl-trace.md` Phase C records the verbatim REPL
session for this play.

## Play 3 — Both reject

Both Alice and Charlie reject the friend-mediated introduction.  This
is the §7.3 symmetric reject branch:

- Steps 1–4 are again the same: Alice→Bob cold-call still succeeds
  (`bob3`'s `decision(yes, alice, ReqId?)` clause is unchanged from
  bob1 / bob2), and Bob still issues `[introduce(alice, charlie)]`.
- Alice's `alice3_wait_intro` clause matches
  `befriend_intro(bob, charlie, ReqId)` and replies
  `[reject_intro(charlie, ReqId?)]` — Alice rejects.
- Charlie's `charlie3_wait_intro` clause matches
  `befriend_intro(bob, alice, ReqId)` and replies
  `[reject_intro(alice, ReqId?)]` — Charlie also rejects.
- The agent's response routes `reject_intro` decisions back through
  Bob (the introducer) without producing `connected(...)` or
  `rejected(...)` notifications back to Alice or Charlie.  Both
  `alice3_wait_intro` and `charlie3_wait_intro` terminate after issuing
  their reject responses (their bodies do not recurse on the
  follow-up notification stream).
- No friend channel is established between Alice and Charlie; this
  is the end-to-end exercise of the §7.3 reject branch.

```bash
cd D:/bstdev/research/GLP/GLP && printf "%s\n:limit 1000000\nplay3.\n:quit\n" "$(pwd -W)/olamni/tutorial/ch07/cssg-modules" | "/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill 2>&1 | head -40
```

Cross-check: `ex-08-repl-trace.md` Phase D records the verbatim REPL
session for this play.

## Run all 3 plays

The three plays must be run in SEPARATE REPL invocations (not in a
single interactive session); the REPL kernel snapshot retains
suspended goals from prior queries, so launching all three in one
session would entangle their channel state.  The bash sequence
below runs each in its own process:

```bash
cd D:/bstdev/research/GLP/GLP && for p in play1 play2 play3; do echo "=== $p ==="; printf "%s\n:limit 1000000\n%s.\n:quit\n" "$(pwd -W)/olamni/tutorial/ch07/cssg-modules" "$p" | "/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill 2>&1 | head -40; done
```

All three plays terminate in `→ suspended` — per CLAUDE.md §12, this
is a valid play outcome (the protocol completed without fault and
the channels remain unsealed, awaiting further input that never
arrives).  The visible difference between the three plays is in the
actor scripts (`alice1`/`charlie1` send + receive messages; `alice2`
sees a `rejected(charlie)`; neither `alice3` nor `charlie3` sees any
post-decision messages), not in the REPL's terminal status.

## Multimodule-project-derivation note

Cluster B's source canonical is `programs/cssg_modules/` (book §7.7
validation example, p 61).  Cluster B inherits ALL six files
byte-exact from the canonical: `self.glp`, `agent.glp`, `boot.glp`,
`mad_boot.glp`, `ui/mediator.glp`, `ui/actors.glp` (per
research R-008's `multimodule-project-derivation` cross-chapter
relationship contract — cluster B is the unpruned canonical
reproduction; cluster A is the pruned 3-agent subset).  Running
plays 1–3 here verifies the cluster B canonical reproduces the
§7.3 cold-call behaviour exactly as cluster A does — the same
`agent/4`, `ui_mediator/5`, `network3/3`, and actor logic is
exercised in both, so any divergence in this trace would indicate a
copy bug.

## Next

Exercise 9 covers §7.7 use case (b) — CSSG accept + reject — by
running plays 4 and 5 from cluster B's `boot.glp`.  These are the
4-agent plays that exercise the parent-mediated child-introduction
protocol (`child_introduce` / `child_befriend`) which cluster A does
not have (cluster A's `boot.glp` was pruned to remove plays 4–7).
See `ex-09-tutorial.md` and `ex-09-repl-trace.md`.
