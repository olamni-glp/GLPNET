# Exercise 07 — fplay7: CSSG Dave rejects the child introduction

`fplay7` is the fourth and final CSSG play. The mirror image of fplay6: Bob approves the child introduction, Carol accepts on her side — but **Dave** rejects. The result is a 13-line tagged stream symmetric to fplay6's: Carol learns Dave rejected via the introduction channel's nack propagating back as `rejected(dave)`.

## Open the REPL and load CSSG

```bash
cd D:/bstdev/research/GLP/GLP
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

```
GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules
```

## Step 1 — `alice7/1` and `bob7/1`: same as fplay4

`alice7` and `bob7` (ui/actors.glp:433–458) are structurally identical to alice4 and bob4. No divergence to demonstrate at the prompt.

## Step 2 — `carol7/1`: accepts on her side, then receives rejected(dave)

`carol7` (ui/actors.glp:460–472) accepts the child befriend (same first-clause shape as carol4), then `carol7_wait_rejected` waits for `[rejected(dave)|_]`. Drive carol7:

```
GLP> carol7(ch([child_befriend(alice, dave, req(1)), rejected(dave)], C7Out)).
C7Out = [accept_child_intro(dave, req(1))]
→ succeeds
```

One command (the accept) — the rejected(dave) notification is consumed by carol7_wait_rejected and produces no further command. This is the same accept-then-rejected pattern as alice2 in fplay2 and dave6 in fplay6.

## Step 3 — `dave7/1`: Dave rejects

`dave7` (ui/actors.glp:474–479) is symmetric to carol6 — a two-clause actor that immediately rejects on receiving `child_befriend(bob, carol, ReqId)`. Drive dave7:

```
GLP> dave7(ch([child_befriend(bob, carol, req(1))], D7Out)).
D7Out = [reject_child_intro(carol, req(1))]
→ succeeds
```

A single command — Dave's rejection.

## Step 4 — Body components: same as fplay4's

Structurally identical to fplay4's body. See ex-04 step 6.

## Step 5 — `fplay7` itself: 13 tagged lines, Dave's reject

```
GLP> :limit 5000000
Goal reduction limit set to 5000000

GLP> fplay7.
tagged(alice, cmd(connect(bob)))
tagged(bob, notify(befriend(alice, req(1))))
tagged(bob, cmd(decision(yes, alice, req(1))))
tagged(bob, notify(connected(alice)))
tagged(alice, notify(connected(bob)))
tagged(alice, cmd(child_introduce(carol, bob, dave)))
tagged(bob, notify(child_befriend(alice, carol, req(2))))
tagged(carol, notify(child_befriend(alice, dave, req(1))))
tagged(bob, cmd(approve_child_intro(carol, dave, req(2))))
tagged(carol, cmd(accept_child_intro(dave, req(1))))
tagged(dave, notify(child_befriend(bob, carol, req(1))))
tagged(dave, cmd(reject_child_intro(carol, req(1))))
tagged(carol, notify(rejected(dave)))
→ suspended
```

## The full effect

**Lines 1–11 are byte-identical to fplay4's lines 1–11**: cold-call befriending, child introduction, Bob's approval, both children receiving the request, Carol accepting on her own side, Dave receiving the request via Bob's forwarded approval. Up to and including line 11, fplay4 and fplay7 are indistinguishable.

**Line 12 — Dave's reject.** dave7 sees his `child_befriend(bob, carol, req(1))` notification (line 11) and immediately produces `reject_child_intro(carol, req(1))`. Dave's mediator forwards the channel form; Dave's agent's `reject_child_intro` clause closes Dave's side of the channel with `nack`. Crucially, in fplay4 this position would have been Dave's `accept_child_intro` followed by `connected(carol)` notifications.

**Line 13 — Carol receives rejected(dave).** Carol's agent had ALREADY started `intro_await_peer/3` after her own accept (line 10). She was reading the OTHER half of the introduction channel, waiting for either `ack(dave)` or `nack`. Dave's reject wrote `nack` to that side; `intro_await_peer/3`'s second clause matches `[nack|_]` and produces `intro_rejected(dave)`. Carol's agent's `intro_rejected` clause emits `rejected(dave)` on her '_user'; the mediator forwards. Carol's `carol7_wait_rejected` matches and terminates.

The protocol settles: Alice and Bob remain friends; Bob approved on his side; Carol accepted on her side; but Dave rejected. The introduction does not happen. fplay7's reject path is the symmetric mirror of fplay6 — by Dave instead of Carol — and produces an equivalently shaped 13-line tagged stream.

The four CSSG plays (fplay4–7) together exercise all four reject branches of the parent-mediated child introduction protocol: both accept (fplay4), Bob rejects (fplay5), Carol rejects (fplay6), Dave rejects (fplay7). Note Alice does NOT have an independent reject branch — she initiates, but the protocol does not require her further consent after her initial `child_introduce` command. The four explicit reject branches are: parent-of-initiator (Bob), child-of-initiator (Carol), other-parent (already covered by Bob's approve gate), and other-child (Dave). The protocol's design ensures that any single party can veto the friendship via their own reject without needing to coordinate.

## Close the session

```
GLP> :quit
Goodbye!
```

## Reference

- `programs/cssg_modules/boot.glp` lines 763–814 — `fplay7`'s body.
- `programs/cssg_modules/ui/actors.glp` lines 433–479 — `alice7`, `bob7`, `carol7`, `dave7` scripts.
- `programs/cssg_modules/agent.glp` line 180 — the `reject_child_intro` clause.
- `programs/cssg_modules/agent.glp` lines 60–69 — `intro_await_peer/3` (the nack propagation that converts Dave's reject into Carol's `rejected(dave)`).
- ex-04 — fplay4 (all accept) — full component walkthrough.
- ex-05 — fplay5 (Bob rejects) — parental-veto path.
- ex-06 — fplay6 (Carol rejects) — child-veto path on initiator's child side.
