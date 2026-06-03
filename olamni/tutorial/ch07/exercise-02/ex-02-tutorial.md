# Exercise 02 — fplay2: Charlie rejects the friend-mediated introduction

`fplay2` is the second play of CSSG. Its body wiring (boot.glp:540–570) is structurally identical to fplay1's body — the same `network3`, `agent`, `ui_mediator`, `tee`, and `send_to_user_tagged` components covered in detail in exercise-01 — with one substitution: the actor names are `alice2`, `bob2`, `charlie2` instead of `alice1`, `bob1`, `charlie1`. The new actor scripts produce a different protocol outcome: Alice and Bob still become cold-call friends, Bob still introduces Alice and Charlie, but **charlie2 rejects** the introduction from Bob, and Alice receives a `rejected(charlie)` notification instead of the greeting exchange.

This exercise walks through the actor-script divergences that produce the reject path, then runs `fplay2` to capture the full 20-line tagged stream (5 lines shorter than fplay1, terminating at Alice's `rejected(charlie)` notification rather than continuing to the greeting exchange). Because the body components are unchanged from fplay1, this exercise focuses on what is NEW in fplay2 — the actor scripts.

## Open the REPL and load CSSG

```bash
cd D:/bstdev/research/GLP/GLP
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

```
GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules
```

## Step 1 — `alice2/1`: same accept-the-intro, then wait for rejection

`alice2` (ui/actors.glp:110–135) issues `connect(bob)` first, accepts the intro to Charlie when it arrives, and then sits in `alice2_wait_rejected` waiting for a `rejected(charlie)` notification. Drive alice2 through the full notify sequence she would receive in fplay2:

```
GLP> alice2(ch([connected(bob), befriend_intro(bob, charlie, req(1)), rejected(charlie)], AliceOut)).
AliceOut = [connect(bob), send(bob, hello), accept_intro(charlie, req(1))]
→ succeeds
```

Three commands in `AliceOut`: `connect(bob)` (the initial cold-call), `send(bob, hello)` (after Alice sees `connected(bob)`), and `accept_intro(charlie, req(1))` (after Alice sees `befriend_intro(bob, charlie, req(1))`). Alice does NOT yet know that Charlie will reject — she accepts the intro on her side. Then `alice2_wait_rejected` consumes the `rejected(charlie)` notification and terminates with the empty command list. The contrast with `alice1` is entirely in what comes AFTER the intro: alice1 would have waited for `connected(charlie)` and then sent `Hi Charlie`; alice2 waits for `rejected(charlie)` and terminates.

## Step 2 — `bob2/1`: structurally identical to bob1

`bob2` (ui/actors.glp:137–167) has the same clauses as bob1, just renamed. Bob is the introducer in both plays — he accepts Alice's cold-call, accepts Charlie's cold-call, and issues `introduce(alice, charlie)`. He does not see the introduction outcome on either side. There is no behavioral divergence to demonstrate at the prompt; refer back to ex-01 step 2 (alice1 demonstration) for the structural pattern bob2 follows.

## Step 3 — `charlie2/1`: the rejection — charlie2_wait_intro produces `reject_intro` not `accept_intro`

`charlie2` (ui/actors.glp:169–184) is the agent that diverges decisively from charlie1. The first clause is identical (Charlie accepts Bob's cold-call befriend), but `charlie2_wait_intro` rejects the introduction instead of accepting it:

```
charlie2_wait_intro([befriend_intro(bob, alice, ReqId)|_], [reject_intro(alice, ReqId?)]) :-
    ground(ReqId?) | true.
```

Compare to charlie1's accept clause: `[accept_intro(alice, ReqId?)|Out?]` followed by `charlie1_wait_alice_msg(In?, Out)`. charlie2 produces `[reject_intro(alice, ReqId)]` (a closed list of length one) and terminates — no further messages.

Drive charlie2 through the full notify sequence she would receive in fplay2:

```
GLP> charlie2(ch([befriend(bob, req(1)), connected(bob), befriend_intro(bob, alice, req(2))], CharlieOut)).
CharlieOut = [decision(yes, bob, req(1)), send(bob, hello), reject_intro(alice, req(2))]
→ succeeds
```

Three commands: `decision(yes, bob, req(1))` (Charlie accepts the cold-call from Bob), `send(bob, hello)` (Charlie's greeting to Bob, same as charlie1), and `reject_intro(alice, req(2))` (the divergence — Charlie rejects the introduction Bob proposed). The closed `[reject_intro(alice, req(2))]` is the last command Charlie ever issues; the actor terminates here.

## Step 4 — Body components identical to fplay2's

The other components of fplay2's body — `network3`, `agent`, `ui_mediator`, `tee`, `send_to_user_tagged` — are the same procedures as in fplay1's body. The bindings each component produces in isolation are documented in exercise-01 steps 1, 3, 4, 5, and 6; running them again with fplay2-shape inputs produces the same shape of binding because the components are protocol-step-agnostic. The protocol-specific behavior comes entirely from the actors.

## Step 5 — `fplay2` itself: 20 tagged lines, terminating in rejection

Set the goal-reduction limit and call fplay2:

```
GLP> :limit 1000000
Goal reduction limit set to 1000000

GLP> fplay2.
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
tagged(alice, cmd(accept_intro(charlie, req(1))))
tagged(alice, notify(rejected(charlie)))
→ suspended
```

Twenty tagged lines, terminating in `→ suspended` once the actors' scripts have completed and the protocol channels reach their steady-state wait. Five fewer lines than fplay1 — the introduction's accept handshake produces a `connected(charlie)` notification on Alice's panel, but the rejection produces a `rejected(charlie)` notification with no greeting exchange follow-up.

## The full effect

Lines 1–17 are byte-identical to fplay1's first 17 lines: the cold-call befriending of Alice/Bob and Bob/Charlie, plus Bob's introduce, plus both `befriend_intro` notifications appearing on Alice's and Charlie's panels. The protocols are the same up to and including the moment both parties have been notified of the proposed introduction.

**Line 18 is where fplay1 and fplay2 diverge.** In fplay1, `charlie1_wait_intro` produced `accept_intro(alice, req(2))`; in fplay2, `charlie2_wait_intro` produces `reject_intro(alice, req(2))`. Both forms travel through Charlie's mediator → Charlie's agent.

Inside Charlie's agent, the `reject_intro` clause (agent.glp:140) consumes the message but does NOT call `intro_await_peer` (which is what the accept clause does to wait for the peer's ack). Instead the agent simply recurses without producing any '_user' output, and crucially without binding the introduction's handshake channel — the `nack` half of `IntroChannel` propagates back through the channel structure that Bob's introduce had set up.

Line 19 — `alice, cmd(accept_intro(charlie, req(1)))` — shows Alice still issuing her own accept on her side. She was notified at the same time as Charlie and made her decision independently. Her `accept_intro` reaches Alice's agent, calls `intro_await_peer` on the introduction channel, and reads the **nack** from the channel's writer side (Charlie's reject closed the channel with a nack). `intro_await_peer`'s second clause matches: `intro_await_peer(Other, ch([nack|_], []), intro_rejected(Other?))`. The result `intro_rejected(charlie)` is injected into Alice's UserIn stream, where Alice's agent's `intro_rejected` clause produces `rejected(charlie)` on her '_user' output (line 20). Alice's actor `alice2_wait_rejected` consumes this notification and terminates without further commands.

Charlie's actor terminated at his own `reject_intro`. Bob's actor's `bob2_wait_charlie_msg` saw `received(charlie, hello)` (line 14) and produced `introduce(alice, charlie)` (line 15); after that Bob has no further commands and his actor sits at the end. All three actors have run to completion; the protocol's open channels sit on reader-waits; the goal suspends.

The reject path tested by fplay2 confirms the cold-call protocol's introduction-channel handshake: a `nack` written to the channel by either party propagates as `rejected(Other)` to the other party, regardless of what that other party decided on their own side. Alice's accept does not override Charlie's reject — the nack closes the introduction.

## Close the session

```
GLP> :quit
Goodbye!
```

## Reference

- `programs/cssg_modules/boot.glp` lines 540–570 — `fplay2`'s body (structurally identical to fplay1's, with `actorN→actor2` substitution).
- `programs/cssg_modules/ui/actors.glp` lines 110–184 — `alice2`, `bob2`, `charlie2` scripts.
- `programs/cssg_modules/agent.glp` lines 60–69 — `intro_await_peer/3`, the `nack` propagation that converts Charlie's reject into Alice's `rejected(charlie)`.
- `programs/cssg_modules/agent.glp` lines 140–142 — the agent's `reject_intro` clause.
- ex-01 — fplay1's body components walked through individually.
- ex-03 (next) — fplay3: Alice and Charlie BOTH reject the introduction.
