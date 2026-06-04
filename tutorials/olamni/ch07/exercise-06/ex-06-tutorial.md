# Exercise 06 — fplay6: CSSG Carol rejects the child introduction

`fplay6` is the third CSSG play. Same protocol as fplay4–5, but the rejection is now from the child rather than the parent: Bob approves the child introduction (same as fplay4's both-accept happy path through the parental approval gate), but **Carol** then rejects on her own side. Dave still receives the introduction request — because Bob already forwarded it — and accepts, but Carol's reject closes the channel and Dave receives `rejected(carol)` on his panel.

The result is a 13-line tagged stream — five lines shorter than fplay4, two lines longer than fplay5 (because Dave still receives and acts on the request before learning of Carol's rejection).

## Open the REPL and load CSSG

```bash
cd D:/bstdev/research/GLP/GLP
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

```
GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules
```

## Step 1 — `alice6/1` and `bob6/1`: same as fplay4

`alice6` and `bob6` (ui/actors.glp:381–406) are structurally identical to alice4 and bob4 — Alice initiates the child introduction, Bob accepts both the cold-call befriend and the child-intro approval. No divergence to demonstrate at the prompt; refer to ex-04 steps 2 and 3.

## Step 2 — `carol6/1`: Carol rejects

`carol6` (ui/actors.glp:408–413) has just two clauses: the first matches `[child_befriend(alice, dave, ReqId)|_]` and immediately produces `[reject_child_intro(dave, ReqId)]`; the second handles other notify types via `otherwise`. Drive carol6:

```
GLP> carol6(ch([child_befriend(alice, dave, req(1))], C6Out)).
C6Out = [reject_child_intro(dave, req(1))]
→ succeeds
```

A single command — the rejection. Carol6 has no `wait_rejected` follow-up; she terminates the moment she rejects.

## Step 3 — `dave6/1`: Dave accepts, then receives rejected(carol)

`dave6` (ui/actors.glp:415–427) accepts the child befriend (same first-clause shape as dave4), then `dave6_wait_rejected` waits for `[rejected(carol)|_]` and terminates. Drive dave6 through his notify sequence:

```
GLP> dave6(ch([child_befriend(bob, carol, req(1)), rejected(carol)], D6Out)).
D6Out = [accept_child_intro(carol, req(1))]
→ succeeds
```

One command — the accept. The `rejected(carol)` notification was consumed by `dave6_wait_rejected` and produced no further command (the empty list is the terminating tail).

## Step 4 — Body components: same as fplay4's

Structurally identical to fplay4's body. See ex-04 step 6.

## Step 5 — `fplay6` itself: 13 tagged lines, Carol's reject rather than Bob's

```
GLP> :limit 5000000
Goal reduction limit set to 5000000

GLP> fplay6.
tagged(alice, cmd(connect(bob)))
tagged(bob, notify(befriend(alice, req(1))))
tagged(bob, cmd(decision(yes, alice, req(1))))
tagged(bob, notify(connected(alice)))
tagged(alice, notify(connected(bob)))
tagged(alice, cmd(child_introduce(carol, bob, dave)))
tagged(bob, notify(child_befriend(alice, carol, req(2))))
tagged(carol, notify(child_befriend(alice, dave, req(1))))
tagged(bob, cmd(approve_child_intro(carol, dave, req(2))))
tagged(carol, cmd(reject_child_intro(dave, req(1))))
tagged(dave, notify(child_befriend(bob, carol, req(1))))
tagged(dave, cmd(accept_child_intro(carol, req(1))))
tagged(dave, notify(rejected(carol)))
→ suspended
```

## The full effect

**Lines 1–9 are byte-identical to fplay4's lines 1–9** — Alice and Bob become friends, Alice initiates the child introduction, Bob approves. fplay4 and fplay6 agree on everything up through Bob's approval; the divergence is on Carol's side.

**Line 10 — Carol's reject.** carol6 sees her `child_befriend(alice, dave, req(1))` notification (line 8) and immediately produces `reject_child_intro(dave, req(1))`. Carol's mediator forwards `reject_child_intro(dave, channel(C1Ch))` to her agent; the agent's `reject_child_intro` clause (agent.glp:180) matches and closes Carol's side of the introduction channel with `nack`.

**Line 11 — Dave still receives the child_befriend.** This is the asymmetry between fplay5 (parent-reject) and fplay6 (child-reject). In fplay5 Bob's reject prevented Dave from EVER seeing the request. In fplay6 Bob ALREADY approved (line 9), which means Bob's `approve_child_intro` clause (agent.glp:184) ALREADY ran and forwarded `child_intro(carol, C2Ch)` over the bob-dave parent-child channel. Dave's agent has been processing this in parallel with Carol's reject; Dave's mediator surfaces the notification regardless of what Carol decides.

**Line 12 — Dave accepts on his own side.** dave6 sees the child_befriend notification (line 11) and issues `accept_child_intro(carol, req(1))`. Dave does not yet know that Carol has rejected; his decision is independent.

**Line 13 — Dave receives rejected(carol).** Dave's agent's `accept_child_intro` clause runs, writes `ack(dave)` on the introduction channel, and starts `intro_await_peer/3`. The peer's half of the channel was closed with `nack` by Carol's reject. `intro_await_peer/3`'s second clause matches `[nack|_]` and produces `intro_rejected(carol)`. The agent's `intro_rejected` clause (agent.glp:153) emits `rejected(carol)` on Dave's '_user'; the mediator translates and forwards. Dave's `dave6_wait_rejected` matches and terminates.

The protocol settles: parents Alice and Bob remain friends; Bob approved his side of the child introduction; Carol rejected; Dave accepted but learned post-hoc that Carol said no. Carol's panel terminates at her reject; Dave's panel ends with the rejected notification.

fplay6 demonstrates the child-veto power: even after both parents approve, either child can independently block the friendship by rejecting on their own side. The parental approval gate and the child consent gate are independent.

## Close the session

```
GLP> :quit
Goodbye!
```

## Reference

- `programs/cssg_modules/boot.glp` lines 710–761 — `fplay6`'s body.
- `programs/cssg_modules/ui/actors.glp` lines 381–427 — `alice6`, `bob6`, `carol6`, `dave6` scripts.
- `programs/cssg_modules/agent.glp` line 180 — the `reject_child_intro` clause.
- `programs/cssg_modules/agent.glp` line 145 — the `intro_result` clause (the path NOT taken in fplay6 because the channel carries nack).
- ex-04 — fplay4 (all accept) — full component walkthrough.
- ex-05 — fplay5 (Bob rejects) — parental-veto path.
- ex-07 (next) — fplay7: Dave rejects (the symmetric child-reject branch).

Note: this directory also contains `ex-06-flutter-trace.md` from the prior implementation; that file is preserved per the no-removal directive but is unrelated to this exercise's REPL walkthrough.
