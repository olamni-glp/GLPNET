# Exercise 04 — fplay4: CSSG parent-mediated child introduction (all four accept)

`fplay4` is the first of the four CSSG-specific plays (4–7). It introduces the **parent-mediated child introduction** protocol: two adults (Alice and Bob) become friends in the usual cold-call way, then Alice arranges an introduction between her child Carol and Bob's child Dave — but the children only befriend each other after BOTH parents (Alice and Bob) and BOTH children (Carol and Dave) approve. fplay4 walks the all-accept happy path; fplays 5–7 walk the three reject branches.

The body wiring (boot.glp:604–655) is structurally **different** from fplay1–3: four agents instead of three, `network2` instead of `network3` (only the two adults talk over the network), each adult's agent has THREE outputs (`'_user'`, `'_net'`, `child(<otherChild>)`), each child's agent has TWO outputs (`'_user'`, `child(<myParent>)`), and `merge/3` combines each agent's network input with its child input into a single `NetInStream`. This exercise walks through these new components plus the four actor scripts, and then runs `fplay4` to capture its 18-line tagged stream.

## Open the REPL and load CSSG

```bash
cd D:/bstdev/research/GLP/GLP
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

```
GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules
```

## Step 1 — `network2/2`: the two-adult network switch

`fplay4` opens with `network2(ch(AliceNetOut?, AliceNetIn), ch(BobNetOut?, BobNetIn))` (boot.glp:605–606). This two-channel switch handles the cold-call between Alice and Bob (the two adults). The children Carol and Dave never use the network directly — their parent-to-child channels are separate. Drive network2 with one cold-call from Alice → Bob:

```
GLP> network2(ch([msg(bob, intro(alice, R))], AIncoming), ch(BOutgoing, BIncoming)).
R = <unbound>
AIncoming = <unbound>
BOutgoing = <unbound>
BIncoming = [msg(bob, intro(alice, X10)) | X26]
→ suspended
```

`BIncoming` was bound to the routed cold-call. The mechanic is the same as `network3`'s but for two channels rather than three; the four routing clauses cover Alice→Bob and Bob→Alice in both cold-call (2-arg `msg`) and friend-message (3-arg `msg`) shapes.

## Step 2 — `alice4/1`: Alice's script (initiates the child introduction)

`alice4` (ui/actors.glp:263–272) issues `connect(bob)` first, then on seeing `connected(bob)` issues `child_introduce(carol, bob, dave)` — the new CSSG command that proposes "let my child Carol befriend Bob's child Dave". Drive alice4 through the notify sequence:

```
GLP> alice4(ch([connected(bob)], A4Out)).
A4Out = [connect(bob), child_introduce(carol, bob, dave)]
→ succeeds
```

Two commands: the cold-call to Bob (same as alice1), then `child_introduce(carol, bob, dave)`. Alice4 has only one branch — there is no `wait_intro` follow-up because the parents do not see the introduction's accept/reject outcome directly; that is mediated through the children.

## Step 3 — `bob4/1`: Bob accepts Alice's friend request, then approves the child intro

`bob4` (ui/actors.glp:274–288) accepts Alice's befriend (same as bob1), then waits for `child_befriend(alice, carol, ReqId)` — the notification that "Alice's child Carol wants to befriend your child Dave". Bob's decision is `approve_child_intro(carol, dave, ReqId)`:

```
GLP> bob4(ch([befriend(alice, req(1)), child_befriend(alice, carol, req(2))], B4Out)).
B4Out = [decision(yes, alice, req(1)), approve_child_intro(carol, dave, req(2))]
→ succeeds
```

Two commands: the friend-accept and the child-intro-approve. The approve produces a downstream `child_befriend(bob, carol, ReqId)` notification on Dave's UI panel — Bob's approval forwards the introduction to Dave.

## Step 4 — `carol4/1`: Carol accepts the cross-family child befriend request

`carol4` (ui/actors.glp:290–310) waits for `child_befriend(alice, dave, ReqId)` — "your parent Alice's friend's child Dave wants to befriend you" — and accepts via `accept_child_intro(dave, ReqId)`. After the connection is established, Carol greets Dave:

```
GLP> carol4(ch([child_befriend(alice, dave, req(1)), connected(dave), received(dave, 'Hi Carol')], C4Out)).
C4Out = [accept_child_intro(dave, req(1)), send(dave, Hi Dave)]
→ succeeds
```

Two commands: the accept, then the greeting. The notify sequence shows what Carol receives across her panel: the `child_befriend` request, then `connected(dave)` once both children have accepted, then `received(dave, 'Hi Carol')` after Dave's reply. Carol4 terminates after seeing Dave's reply.

## Step 5 — `dave4/1`: Dave accepts and replies

`dave4` (ui/actors.glp:312–327) is symmetric to carol4 but receives `child_befriend(bob, carol, ReqId)` (the request originating from his parent Bob's friend's child Carol):

```
GLP> dave4(ch([child_befriend(bob, carol, req(1)), connected(carol), received(carol, 'Hi Dave')], D4Out)).
D4Out = [accept_child_intro(carol, req(1)), send(carol, Hi Carol)]
→ succeeds
```

Two commands: the accept, then `send(carol, 'Hi Carol')` after seeing `received(carol, 'Hi Dave')`. Note the asymmetry: Dave's `dave4_wait_msg` first consumes any `connected(_)` notification then awaits `received(carol, _)` before sending his reply (this gives Carol's greeting a deterministic place in the protocol).

## Step 6 — Body components: agents, mediators, child-channels, merges

The other components common with fplay1 (agent, ui_mediator, tee, send_to_user_tagged) are the same procedures called individually in exercise-01. Three new structural elements that do NOT appear in fplay1–3:

**Three-output agents for the parents.** Alice's and Bob's `agent#agent(...)` calls take three outputs in their `OutputsList`: `output('_user', AliceAgentToUser)`, `output('_net', AliceNetOut)`, **and** `output(child(carol), AliceToCarol)`. The third output is the parent-to-child channel — a bidirectional channel the parent's agent uses to forward `child_intro(...)` messages to its own child and receive replies (this is how `agent.glp`'s `child_introduce` clause at line 166 routes the introduction request out, and how `child_befriend` notifications come back).

**Two-output agents for the children.** Carol's and Dave's `agent#agent(...)` calls take two outputs: `output('_user', CarolAgentToUser)` and `output(child(alice), CarolToAlice)`. Children have NO `'_net'` output — they don't speak directly to the network; everything goes through their parent's channel.

**Cross-merge connections.** The four `merge(...)` calls at the end of fplay4's body wire the parents' child-output streams to the children's network-input streams: `merge(AliceToCarol?, [], CarolFromAlice)`, `merge(CarolToAlice?, [], AliceFromCarol)`, `merge(BobToDave?, [], DaveFromBob)`, `merge(DaveToBob?, [], BobFromDave)`. Each merge combines the parent's outgoing child-stream with an empty stream (the [] argument) into a single stream that becomes the child's NetIn. This is how Carol's child(alice) channel becomes Alice's view of Carol's outgoing.

The `merge(AliceNetIn?, AliceFromCarol?, AliceNetAndChildIn)` calls (one per agent) combine the agent's two incoming streams — network input + child input — into a single stream that the agent's `NetInStream` consumes.

## Step 7 — `fplay4` itself: 18 tagged lines, all-accept happy path

```
GLP> :limit 5000000
Goal reduction limit set to 5000000

GLP> fplay4.
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
tagged(dave, cmd(accept_child_intro(carol, req(1))))
tagged(dave, notify(connected(carol)))
tagged(carol, notify(connected(dave)))
tagged(carol, cmd(send(dave, Hi Dave)))
tagged(dave, notify(received(carol, Hi Dave)))
tagged(dave, cmd(send(carol, Hi Carol)))
tagged(carol, notify(received(dave, Hi Carol)))
→ suspended
```

Eighteen tagged lines (note: a higher `:limit` is needed than fplay1 because fplay4's wiring involves more concurrent goals due to the four-merge child channels). Every one of the four actors contributes commands and receives notifications.

## The full effect

**Lines 1–5 — Alice and Bob become friends (cold call).** Identical to fplay1's lines 1–5: Alice issues `connect(bob)`, the cold-call routes through `network2` to Bob, Bob's mediator translates to `befriend(alice, req(1))`, bob4 issues `decision(yes, alice, req(1))`, the mediator looks up the pending response and forwards it to Bob's agent which establishes the friend channel and emits `connected(alice)` on Bob's panel and `connected(bob)` on Alice's panel.

**Line 6 — Alice initiates the child introduction.** Alice4 sees `connected(bob)` (line 5) and issues `child_introduce(carol, bob, dave)` (line 6). This is THE new CSSG command. Alice's mediator forwards to Alice's agent; the agent matches its `child_introduce` clause (agent.glp:166) which **allocates a fresh handshake channel `new_channel(C1Ch, C2Ch)`** and sends two intros: `msg(alice, carol, child_intro(dave, C1Ch))` to her own child Carol via `output(child(carol), ...)`, and `msg(alice, bob, child_intro(carol, C2Ch))` to her friend Bob via `output(friend(bob), ...)`.

**Line 7 — Bob receives the child befriend request.** Alice's outgoing message to Bob travels through the friend channel that was established when they became friends. Bob's agent matches its `child_intro` reception clause (agent.glp:211) which produces `child_befriend(alice, carol, C2Ch)` on his '_user'; Bob's mediator translates the channel to `req(2)` (line 7).

**Line 8 — Carol receives the child befriend request from her parent's friend's child.** Alice's outgoing message to Carol travels through the parent-child channel `output(child(carol), ...)` which after merging becomes Carol's NetIn. Carol's agent matches the same `child_intro` clause with her id, producing `child_befriend(alice, dave, C1Ch)` on her '_user'; her mediator translates to `req(1)` (line 8).

**Lines 9–12 — Both parents and Carol approve; Dave receives and approves.** Bob4 sees Carol's incoming child_befriend (line 7) and issues `approve_child_intro(carol, dave, req(2))` (line 9). The mediator looks up `req(2)`'s pending channel and forwards `approve_child_intro(carol, dave, channel(C2Ch))` to Bob's agent. The agent's `approve_child_intro` clause (agent.glp:184) **forwards** the channel from the parent-side to the child-side via `lookup_send(child(dave), msg(bob, dave, child_intro(carol, C2Ch)), ...)`. Carol4 sees her `child_befriend` notification (line 8) and issues `accept_child_intro(dave, req(1))` (line 10). Carol's mediator looks up the channel, forwards `accept_child_intro(dave, channel(C1Ch))` to her agent, which writes `ack(carol)` on the channel via `send/3` and starts `intro_await_peer/3` waiting for Dave's ack. Then Dave receives the forwarded child_befriend on his panel (line 11) — note this came from BOB's agent forwarding (line 9's approve), not from Alice — and issues `accept_child_intro(carol, req(1))` (line 12).

**Lines 13–14 — Both children's `intro_await_peer/3` reads the peer's ack.** Carol's agent already wrote `ack(carol)` to her side of C1Ch and was waiting on the reader for Dave's ack. Dave's `accept_child_intro` causes his agent to write `ack(dave)` to his side of C2Ch (which is the same channel Alice-side had; Bob's `approve_child_intro` forwarded it). Both `intro_await_peer/3` calls read each other's ack and return `intro_result(Other, NewFriendChannel)`. The intro_result is injected into each child's UserIn; their `intro_result` clauses (agent.glp:145) add the friend output and emit `connected(Other)` on their '_user'. Mediator forwards to UI: Dave panel shows `connected(carol)` (line 13), Carol panel shows `connected(dave)` (line 14).

**Lines 15–18 — Greeting exchange.** Carol4 sees `connected(dave)` (line 14), issues `send(dave, 'Hi Dave')` (line 15); Dave's agent on the friend channel emits `received(carol, 'Hi Dave')` (line 16); Dave4 sees this and issues `send(carol, 'Hi Carol')` (line 17); Carol's agent emits `received(dave, 'Hi Carol')` (line 18). Both children have completed their actor scripts; the protocol's open channels sit on reader-waits; the goal suspends.

The CSSG happy path: four parties (two parents + two children) agree to introduce two children to each other, with parental approval as a prerequisite — the parents authorize, the children consent, and the children become friends. fplays 5–7 break this sequence at three different reject points.

## Close the session

```
GLP> :quit
Goodbye!
```

## Reference

- `programs/cssg_modules/boot.glp` lines 604–655 — `fplay4`'s body (18-goal conjunction).
- `programs/cssg_modules/boot.glp` lines 152–168 — `network2/2` clauses.
- `programs/cssg_modules/ui/actors.glp` lines 263–327 — `alice4`, `bob4`, `carol4`, `dave4` scripts.
- `programs/cssg_modules/agent.glp` lines 166–187 — `child_introduce`, `accept_child_intro`, `reject_child_intro`, `approve_child_intro` clauses.
- `programs/cssg_modules/agent.glp` lines 211–214 — the `child_intro` reception clause that produces `child_befriend(...)` notifications.
- ex-01 — fplay1 (cold-call + friend-mediated intro) — body components common to all plays.
- ex-05 (next) — fplay5: Bob rejects the child introduction (rejected at the parental-approval step).
