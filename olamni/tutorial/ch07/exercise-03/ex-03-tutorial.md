# Exercise 03 — fplay3: both Alice and Charlie reject the introduction

`fplay3` is the third play of CSSG. Its body wiring (boot.glp:572–602) is structurally identical to fplay1 and fplay2 — same `network3`, `agent`, `ui_mediator`, `tee`, `send_to_user_tagged` components — with the actor names swapped to `alice3`, `bob3`, `charlie3`. The new divergence in fplay3 vs fplay2: **alice3 also rejects** the introduction (in addition to charlie3, who rejects identically to charlie2). The result is a 19-line tagged stream — one line shorter than fplay2 — terminating with both reject_intro commands and NO `rejected(...)` notification on either side.

This exercise focuses on alice3's rejection (the new divergence vs fplay2) and how the protocol terminates cleanly when both parties reject independently.

## Open the REPL and load CSSG

```bash
cd D:/bstdev/research/GLP/GLP
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

```
GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules
```

## Step 1 — `alice3/1`: Alice rejects the introduction

`alice3` (ui/actors.glp:190–208) is the new divergence in fplay3. Like alice2, alice3 issues `connect(bob)` first, then `send(bob, hello)` after seeing `connected(bob)`. But `alice3_wait_intro` produces `reject_intro(charlie, ReqId)` instead of accept — and there is no `alice3_wait_rejected` or `alice3_wait_charlie` follow-up clause; alice3 terminates at the reject:

```glp
procedure alice3_wait_intro(UserNotifyStream?, UserCmdStream).
alice3_wait_intro([befriend_intro(bob, charlie, ReqId)|_],
                  [reject_intro(charlie, ReqId?)]) :-
    ground(ReqId?) | true.
```

Drive alice3 through the notify sequence she would receive in fplay3:

```
GLP> alice3(ch([connected(bob), befriend_intro(bob, charlie, req(1))], AliceOut)).
AliceOut = [connect(bob), send(bob, hello), reject_intro(charlie, req(1))]
→ succeeds
```

Three commands: `connect(bob)`, `send(bob, hello)`, then `reject_intro(charlie, req(1))` — the closed list of length one terminates alice3. Compare to alice2's three commands `[connect(bob), send(bob, hello), accept_intro(charlie, req(1))]`: same structure for the first two, divergence at the third.

## Step 2 — `charlie3/1`: identical to charlie2 — also rejects

`charlie3` (ui/actors.glp:242–257) has the same clauses as charlie2: accept Bob's befriend, send hello to Bob, then reject Alice's intro via `charlie3_wait_intro`. Drive charlie3 through the notify sequence:

```
GLP> charlie3(ch([befriend(bob, req(1)), connected(bob), befriend_intro(bob, alice, req(2))], CharlieOut)).
CharlieOut = [decision(yes, bob, req(1)), send(bob, hello), reject_intro(alice, req(2))]
→ succeeds
```

Identical commands to charlie2 step 3 of ex-02. fplay3 reuses charlie2's reject behavior; the new divergence is on Alice's side only.

## Step 3 — `bob3/1`: structurally identical to bob1 and bob2

bob3 (ui/actors.glp:210–240) has the same clauses as bob1/bob2 — Bob is the introducer in all three plays and never sees the introduction outcome on either side. No behavioral divergence to demonstrate.

## Step 4 — Body components identical to fplay1's

The other components — `network3`, `agent`, `ui_mediator`, `tee`, `send_to_user_tagged` — are the same as in fplay1's body. The bindings each component produces in isolation are documented in exercise-01 steps 1, 3, 4, 5, and 6.

## Step 5 — `fplay3` itself: 19 tagged lines, both reject_intro at the end

```
GLP> :limit 1000000
Goal reduction limit set to 1000000

GLP> fplay3.
tagged(alice, cmd(connect(bob)))
tagged(bob, notify(befriend(alice, req(1))))
tagged(bob, cmd(decision(yes, alice, req(1))))
tagged(bob, notify(connected(alice)))
tagged(alice, notify(connected(bob)))
tagged(alice, cmd(send(bob, hello)))
tagged(bob, notify(received(alice, hello)))
tagged(bob, cmd(connect(charlie)))
tagged(charlie, notify(befriend(bob, req(1))))
tagged(charlie, cmd(decision(yes, bob, req(1))))
tagged(charlie, cmd(send(bob, hello)))
tagged(charlie, notify(connected(bob)))
tagged(bob, notify(connected(charlie)))
tagged(bob, notify(received(charlie, hello)))
tagged(bob, cmd(introduce(alice, charlie)))
tagged(charlie, notify(befriend_intro(bob, alice, req(2))))
tagged(alice, notify(befriend_intro(bob, charlie, req(1))))
tagged(charlie, cmd(reject_intro(alice, req(2))))
tagged(alice, cmd(reject_intro(charlie, req(1))))
→ suspended
```

Nineteen tagged lines. One line shorter than fplay2: there is no `tagged(alice, notify(rejected(charlie)))` line because Alice never accepted — she rejected on her own side, and the protocol does not need to inform her that the other party also rejected.

## The full effect

Lines 1–17 are byte-identical to fplay1 and fplay2: cold-call befriending of both pairs, plus the two `befriend_intro` notifications.

**Lines 18 and 19 are the divergence**: both Charlie's `reject_intro(alice, req(2))` and Alice's `reject_intro(charlie, req(1))`. Each actor's reject travels through their own mediator → their own agent. The agent's `reject_intro` clause (agent.glp:140) matches the message and consumes it without producing any '_user' output: `agent(Id, [msg('_user', Id1, reject_intro(_, channel(ch(_, [nack|[]])))) | UserIn], NetIn, Outs) :- Id =?= Id1 | agent(Id, UserIn?, NetIn?, Outs?)`. The `[nack|[]]` pattern is the channel mediator's signal that the user rejected; the agent simply recurses without further output.

In fplay2 only Charlie rejected; Alice accepted on her side, so Alice's `accept_intro` triggered `intro_await_peer/3` which read the `nack` from the introduction channel (Charlie's reject had closed it) and produced `intro_rejected(charlie)` injected into Alice's UserIn — leading to the `rejected(charlie)` notification she saw on her '_user' panel.

In fplay3 BOTH parties reject. Neither agent ever calls `intro_await_peer` because neither produced an `accept_intro` command. The introduction channel's nack writes propagate but are never read by `intro_await_peer`. No `intro_rejected` value is injected into either UserIn. Result: neither Alice nor Charlie receives a `rejected(...)` notification on their panel — they just see their own reject command, and the protocol terminates.

This makes pedagogical sense: each party who rejects already KNOWS the introduction will not happen on their own side; there is no need for the protocol to inform them of the other party's independent decision. The `rejected(Other)` notification (seen in fplay2 line 20) exists specifically for the party who accepted but had their accept overridden by the other party's reject.

## Close the session

```
GLP> :quit
Goodbye!
```

## Reference

- `programs/cssg_modules/boot.glp` lines 572–602 — `fplay3`'s body (structurally identical to fplay1's, with actor3 substitution).
- `programs/cssg_modules/ui/actors.glp` lines 190–257 — `alice3`, `bob3`, `charlie3` scripts.
- `programs/cssg_modules/agent.glp` lines 140–142 — the agent's `reject_intro` clause.
- ex-01 — fplay1 (both accept) — full component walkthrough.
- ex-02 — fplay2 (Alice accepts, Charlie rejects) — Alice receives `rejected(charlie)` notification.
- ex-04 (next) — fplay4: CSSG parent-mediated child introduction (4 agents — Alice + Carol + Bob + Dave), all four accept.
