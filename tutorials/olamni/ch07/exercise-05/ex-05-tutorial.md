# Exercise 05 — fplay5: CSSG Bob rejects the child introduction (parental veto)

`fplay5` is the second CSSG play. Same parent-mediated child introduction protocol as fplay4, but Bob now exercises the parental veto: when his agent surfaces `child_befriend(alice, carol, ReqId)` to his UI, bob5 issues `reject_child_intro(carol, ReqId)` instead of `approve_child_intro`. The child introduction is blocked at the parental approval step. The result is an 11-line tagged stream — seven lines shorter than fplay4 — terminating with Carol receiving a `rejected(dave)` notification.

## Open the REPL and load CSSG

```bash
cd D:/bstdev/research/GLP/GLP
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

```
GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules
```

## Step 1 — `alice5/1`: same as alice4

`alice5` is structurally identical to alice4 — issues `connect(bob)` then `child_introduce(carol, bob, dave)` after seeing `connected(bob)`. No divergence:

```
GLP> alice5(ch([connected(bob)], A5Out)).
A5Out = [connect(bob), child_introduce(carol, bob, dave)]
→ succeeds
```

## Step 2 — `bob5/1`: the rejection — Bob vetoes the child introduction

`bob5` (ui/actors.glp:344–358) accepts Alice's befriend (same as bob4), then in `bob5_wait_child_intro` produces `reject_child_intro(carol, ReqId)` instead of approve. Drive bob5 through the notify sequence:

```
GLP> bob5(ch([befriend(alice, req(1)), child_befriend(alice, carol, req(2))], B5Out)).
B5Out = [decision(yes, alice, req(1)), reject_child_intro(carol, req(2))]
→ succeeds
```

Two commands: the friend-accept (same as bob4) then the rejection of Carol's befriend request to Dave. The reject closes the introduction channel with a `nack` instead of forwarding it to Dave.

## Step 3 — `carol5/1`: accepts on her side, then receives rejected(dave)

`carol5` (ui/actors.glp:360–372) still issues `accept_child_intro(dave, ReqId)` after seeing the request — she got the request from Alice's side via the parent-child channel BEFORE Bob's reject propagated. She then waits in `carol5_wait_rejected` and terminates after seeing `rejected(dave)`. The accept-then-rejected pattern is the same as alice2 in fplay2 (each side decides independently; the introduction channel's nack determines the outcome). The probe shape would mirror alice2's; refer to ex-02 step 1 for the structural pattern.

## Step 4 — `dave5/1`: silent — never sees the request

`dave5` (ui/actors.glp:374–375) is a one-clause stub: `dave5(ch(_, []))`. He outputs the empty command list and terminates. He never sees the `child_befriend` notification because Bob's reject prevented Bob's agent from forwarding the introduction to Dave. From Dave's perspective, the protocol simply never started.

## Step 5 — Body components: same as fplay4's

The body wiring is structurally identical to fplay4's: network2 + four agents (Alice's and Bob's with three outputs each, Carol's and Dave's with two outputs each) + four cross-merge connections + the per-agent `merge(NetIn, ChildIn, NetAndChildIn)` calls. See ex-04 step 6 for the structural detail; nothing in the body changes between fplay4 and fplay5.

## Step 6 — `fplay5` itself: 11 tagged lines, terminating in parental veto

```
GLP> :limit 5000000
Goal reduction limit set to 5000000

GLP> fplay5.
tagged(alice, cmd(connect(bob)))
tagged(bob, notify(befriend(alice, req(1))))
tagged(bob, cmd(decision(yes, alice, req(1))))
tagged(bob, notify(connected(alice)))
tagged(alice, notify(connected(bob)))
tagged(alice, cmd(child_introduce(carol, bob, dave)))
tagged(bob, notify(child_befriend(alice, carol, req(2))))
tagged(carol, notify(child_befriend(alice, dave, req(1))))
tagged(bob, cmd(reject_child_intro(carol, req(2))))
tagged(carol, cmd(accept_child_intro(dave, req(1))))
tagged(carol, notify(rejected(dave)))
→ suspended
```

## The full effect

**Lines 1–8 — Cold-call befriending plus child introduction proposal.** Identical to fplay4's lines 1–8. Alice and Bob become friends; Alice issues `child_introduce(carol, bob, dave)`; Alice's agent's `child_introduce` clause allocates a fresh introduction handshake channel and sends `child_intro(...)` messages over both Alice's parent-child channel (to Carol) and Alice's friend channel (to Bob). Bob's agent receives Alice's child_intro and surfaces `child_befriend(alice, carol)` (line 7); Carol's agent receives Alice's child_intro and surfaces `child_befriend(alice, dave)` (line 8). At this point both Bob and Carol have seen the request and need to decide.

**Line 9 — Bob's veto.** bob5 issues `reject_child_intro(carol, req(2))`. Bob's mediator looks up `req(2)` in its pending list, retrieves the channel that came in with the `child_befriend` notification, and forwards `reject_child_intro(carol, channel(C2Ch))` to Bob's agent. The agent's `reject_child_intro` clause (agent.glp:180) matches the form `reject_child_intro(_, channel(ch(_, [nack|[]])))` — meaning the message ALREADY has a `nack` written to its channel half. The agent simply consumes the message without forwarding anything. Crucially, Bob's agent never executes the `approve_child_intro` clause (agent.glp:184) which would have forwarded the introduction to Dave via `lookup_send(child(dave), msg(bob, dave, child_intro(carol, Ch)), …)`. Dave therefore never receives a `child_befriend` notification.

**Line 10 — Carol still accepts on her side.** carol5 sees her own `child_befriend(alice, dave, req(1))` notification (line 8) and issues `accept_child_intro(dave, req(1))`. Carol's mediator forwards the channel-form message; Carol's agent's `accept_child_intro` clause (agent.glp:173) writes `ack(carol)` on the introduction channel and starts `intro_await_peer/3` reading the OTHER half. But the other half was closed with `nack` by Bob's reject (the channel structure carries the nack from the parent's reject_child_intro path). `intro_await_peer/3`'s second clause matches: `intro_await_peer(Other, ch([nack|_], []), intro_rejected(Other?))`. The result `intro_rejected(dave)` is injected into Carol's UserIn.

**Line 11 — Carol's panel shows rejected(dave).** Carol's agent's `intro_rejected` clause (agent.glp:153) matches the injected message and produces `rejected(dave)` on Carol's '_user'; the mediator translates and forwards to Carol's panel. carol5's `carol5_wait_rejected` matches `[rejected(dave)|_]` and terminates with the empty command list.

The protocol settles: Alice and Bob remain adult friends, but the child introduction never completes. Carol learns that Dave was unreachable; Dave never knew anything was attempted. fplay5 demonstrates the parental veto power — either parent can block their own child from being befriended via this protocol, regardless of the other side's choices, by issuing reject at the approval step.

## Close the session

```
GLP> :quit
Goodbye!
```

## Reference

- `programs/cssg_modules/boot.glp` lines 657–708 — `fplay5`'s body (structurally identical to fplay4's).
- `programs/cssg_modules/ui/actors.glp` lines 333–375 — `alice5`, `bob5`, `carol5`, `dave5` scripts.
- `programs/cssg_modules/agent.glp` line 180 — the agent's `reject_child_intro` clause (consumes the rejection without forwarding).
- `programs/cssg_modules/agent.glp` line 184 — the agent's `approve_child_intro` clause (the path NOT taken in fplay5).
- ex-04 — fplay4 (all accept) — full component walkthrough.
- ex-06 (next) — fplay6: Carol rejects (the child rejects rather than the parent).
