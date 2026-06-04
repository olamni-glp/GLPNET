# Exercise 01 — fplay1: cold-call befriending + friend-mediated introduction (both accept)

`fplay1` is the first play of the CSSG project. It wires three social agents (Alice, Bob, Charlie) together through the cold-call befriending and friend-mediated introduction protocols, with both sides accepting at every choice point. The play's body is a conjunction of seven goals per agent — `actors#aliceN`, `tee`, `agent#agent`, `mediator#ui_mediator`, a second `tee`, `send_to_user_tagged` — plus one shared `network3` switch that routes the cold calls between the three agents. This exercise walks through each component of fplay1's body individually at the REPL prompt, then runs the assembled play to observe the full 25-binding choreography.

Each step below corresponds to one line of fplay1's body in `programs/cssg_modules/boot.glp` (lines 508–538). The seven step components compose into the full protocol you'll see in step 8.

## Open the REPL and load CSSG

```bash
cd D:/bstdev/research/GLP/GLP
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

```
GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules
```

The single `✓ Loaded project:` line covers SRSW, partial evaluation, type checking, and bytecode compilation across all four CSSG modules — `agent.glp`, `ui/mediator.glp`, `ui/actors.glp`, and `boot.glp` — and resolves the `imported procedure` declarations between them. The procedures from each module are now callable at the prompt by their bare names.

## Step 1 — `network3/3`: routing a cold-call between agents

`fplay1` opens with `network3(ch(AliceNetOut?, AliceNetIn), ch(BobNetOut?, BobNetIn), ch(CharlieNetOut?, CharlieNetIn))` (boot.glp:509–511). This three-channel switch sits between the three agents' network sides: when one agent writes a `msg(target, content)` cold-call to its `NetOut`, the switch matches the target and writes the same message to that target's `NetIn`. To see one routing rule in action, exercise it directly with one closed cold-call from Alice to Bob and the other agents quiet:

```
GLP> network3(ch([msg(bob, intro(alice, R))], AliceIncoming), ch(BobOutgoing, BobIncoming), ch(CharlieOutgoing, CharlieIncoming)).
R = <unbound>
AliceIncoming = <unbound>
BobOutgoing = <unbound>
BobIncoming = [msg(bob, intro(alice, X60)) | X84]
CharlieOutgoing = <unbound>
CharlieIncoming = <unbound>
→ suspended
```

`BobIncoming` was bound to a list whose head is `msg(bob, intro(alice, R))` — the same cold-call Alice wrote, now arriving on Bob's incoming side. The switch performed one routing step. The other agents' channels stayed unbound because no traffic was directed at them. The recursive `network3` call then hit unbound stream tails on all sides and suspended — that is the steady-state wait. The X-numbers (`X60`, `X84`) are fresh internal var names; yours will differ.

## Step 2 — `actors#alice1/1`: the scripted Alice actor

Each agent's role in fplay1 is driven by a scripted actor. Alice's script `alice1` (ui/actors.glp:14–17) writes `connect(bob)` as its first command and then waits for `connected(bob)` to come back before doing anything else. Run alice1 alone with its channel ends unbound:

```
GLP> alice1(ch(NotifyIn, CmdOut)).
NotifyIn = <unbound>
CmdOut = [connect(bob) | X8]
→ suspended
```

`CmdOut` was bound to `[connect(bob) | tail]` — alice1 has issued the cold-call command. `NotifyIn` is unbound because no one has notified alice1 of anything yet (in the full play, this is the channel the mediator writes notifications onto). alice1 then suspended in `alice1_wait_connected` waiting for the `connected(bob)` notification that the mediator will produce after Bob's accept.

## Step 3 — `agent#agent/4` (Alice side): the social agent processing connect

Alice's agent (boot.glp:515–516) is the next component in fplay1's body. It receives Alice's user commands on its `UserInStream` (the same stream alice1 wrote to, after passing through tee + mediator), and produces network output. Run the agent with one closed user command and the other inputs open:

```
GLP> agent(alice, [msg('_user', alice, connect(bob))], NetIn, [output('_user', AliceUser), output('_net', AliceNet)]).
NetIn = <unbound>
AliceUser = <unbound>
AliceNet = [msg(bob, intro(alice, X38)) | X86]
→ suspended
```

Alice's agent matched its connect-handling clause (agent.glp line 111) and called `lookup_send('_net', msg(bob, intro(alice, Resp)), …)` which bound `AliceNet` to a list whose head is the cold-call `msg(bob, intro(alice, Resp))`. This is exactly the message the network3 switch in step 1 routes to Bob. The agent then suspended on its recursive call (`UserInStream` = `[]` triggers `inject_msg/5` awaiting the `Resp` reader; the agent recurses with the suspended UserIn1 and parks). `AliceUser` stays unbound — the connect clause does not write to '_user'.

## Step 4 — `mediator#ui_mediator/5`: translating agent → UI

The mediator (boot.glp:517–518) sits between the agent's '_user' output and the UI commands the actor produces. When the agent writes a non-ground notification like `befriend(From, Resp)` (where `Resp` is the response variable the agent will eventually read), the mediator allocates a fresh request id, replaces the response variable, and forwards a UI-shaped notification:

```
GLP> ui_mediator(alice, ch([msg(agent, '_user', befriend(bob, Resp))], AgentIn), ch(MedIn, MedOut), [], 1).
Resp = <unbound>
AgentIn = <unbound>
MedIn = <unbound>
MedOut = [befriend(bob, req(1)) | X72]
→ suspended
```

`MedOut` was bound to `[befriend(bob, req(1)) | tail]` — the mediator translated the agent's `befriend(bob, Resp)` (with response variable) into the UI form `befriend(bob, req(1))` (with concrete request id). The mediator stored a `pending(req(1), response(Resp))` entry in its pending list (the fourth arg of ui_mediator's recursive call) so that when the user later sends a `decision(yes, bob, req(1))` command, the mediator can look up `Resp` again and forward `decision(yes, bob, response(Resp))` to the agent. The mediator then suspended awaiting more incoming traffic.

## Step 5 — `tee/3`: duplicating a stream into two consumers

fplay1 calls `tee/3` twice per agent (boot.glp:514, 519): first to fork the actor's command output into a mediator-input copy and a display copy, second to fork the mediator's notification output into an actor-input copy and a display copy. tee with a closed CSSG-shape command list:

```
GLP> tee([msg('_user', alice, connect(bob)), msg('_user', alice, send(bob, hello))], MedIn, DispCmd).
MedIn = [msg(_user, alice, connect(bob)), msg(_user, alice, send(bob, hello))]
DispCmd = [msg(_user, alice, connect(bob)), msg(_user, alice, send(bob, hello))]
→ succeeds
```

Both outputs got bound to identical copies of the input stream. In fplay1 the first copy flows into the mediator (which translates UI commands into agent-shape user messages), the second flows into the display (where `send_to_user_tagged` wraps it for Flutter panels). Same content, two consumers.

## Step 6 — `send_to_user_tagged/3`: tagging cmd/notify lines for Flutter

The last per-agent component (boot.glp:520) wraps the command and notification streams into `tagged(Id, cmd(...))` and `tagged(Id, notify(...))` lines that the Flutter app's `_routeOutput` parses into per-agent panels. Each call to `send_to_user_tagged` produces one `_output/1` side effect per element. With closed cmd and notify lists you see four tagged lines emitted directly:

```
GLP> send_to_user_tagged(alice, [connect(bob), send(bob, hello)], [befriend(carol, req(1)), connected(bob)]).
tagged(alice, cmd(connect(bob)))
tagged(alice, cmd(send(bob, hello)))
tagged(alice, notify(befriend(carol, req(1))))
tagged(alice, notify(connected(bob)))
→ succeeds
```

The four `tagged(...)` lines are stdout side effects from `_output/1` — they are exactly the lines the Flutter app receives on its isolate stdout and routes via `_routeOutput` into Alice's panel. The `cmd(...)` lines render with a `>` prefix in the panel; the `notify(...)` lines render with a `<` prefix. The procedure then succeeds because both lists were closed.

## Step 7 — `fplay1` itself: the components composed

The seven components above (network3 plus six per agent × three agents) compose into fplay1's body. When you call `fplay1` at the prompt the entire choreography unfolds, and `send_to_user_tagged` emits each `cmd(...)` and `notify(...)` line as soon as the corresponding agent step produces it. Set the goal-reduction limit high enough to let the whole protocol run, then call:

```
GLP> :limit 1000000
Goal reduction limit set to 1000000

GLP> fplay1.
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
tagged(charlie, cmd(accept_intro(alice, req(2))))
tagged(alice, cmd(accept_intro(charlie, req(1))))
tagged(charlie, notify(connected(alice)))
tagged(alice, notify(connected(charlie)))
tagged(alice, cmd(send(charlie, Hi Charlie)))
tagged(charlie, notify(received(alice, Hi Charlie)))
tagged(charlie, cmd(send(alice, Hi Alice)))
tagged(alice, notify(received(charlie, Hi Alice)))
→ suspended
```

Twenty-five tagged lines, alternating between agents and between cmd/notify, terminating in `→ suspended` once every actor's script has run to completion and the protocol's channels are at their steady-state wait. Each line is one observable choreography step.

## The full effect

Reading the 25 tagged lines from top to bottom traces the entire cold-call + friend-mediated-introduction protocol:

**Lines 1–7 — Alice and Bob become friends (cold call).** Alice's actor issues `connect(bob)` (line 1); the mediator forwards to Alice's agent, the agent emits a cold-call to '_net' which network3 routes to Bob's NetIn; Bob's agent matches its NetIn cold-call clause and produces `befriend(alice, Resp)` on Bob's '_user'; Bob's mediator translates this to `befriend(alice, req(1))` (line 2); Bob's actor `bob1` matches the befriend pattern and writes `decision(yes, alice, req(1))` (line 3); the mediator looks up the pending `Resp` and sends `decision(yes, alice, response(accept(LocalCh)))` to Bob's agent; the agent runs `bind_response` + `handle_response` which adds a friend output and emits `connected(alice)` (line 4); the response also flows back to Alice's agent which adds its own friend output and emits `connected(bob)` (line 5); Alice's actor `alice1_wait_connected` matches and issues `send(bob, hello)` (line 6); the message routes through the friend output, lands in Bob's NetIn as a friend-typed message, and Bob's agent emits `received(alice, hello)` (line 7).

**Lines 8–14 — Bob and Charlie become friends (Bob initiates a fresh cold call).** Bob's actor `bob1_wait_alice_msg` saw the `received(alice, _)` notification (line 7) and now issues `connect(charlie)` (line 8). The same cold-call protocol runs again with Bob as initiator and Charlie as responder, producing the exact mirror of lines 2–7 but for Bob/Charlie (lines 9–14).

**Lines 15–21 — Bob introduces Alice and Charlie to each other.** Bob's actor `bob1_wait_charlie_msg` saw Charlie's `received(charlie, _)` notification (line 14) and now issues `introduce(alice, charlie)` (line 15). Bob's agent matches the introduce clause, allocates a fresh handshake channel, and sends `intro(charlie, QPCh)` to Alice's friend stream and `intro(alice, PQCh)` to Charlie's friend stream; both agents convert these to `befriend_intro(bob, ...)` notifications on their '_user' streams (lines 16–17, mediator-translated with req IDs); both actors `alice1_wait_intro` and `charlie1_wait_intro` accept (lines 18–19); both agents do the `accept_intro` handshake which binds both halves of the introduction channel; both agents emit `connected(...)` for the new friend (lines 20–21).

**Lines 22–25 — Alice and Charlie greet each other (the third pair text exchange).** Alice's actor `alice1_wait_charlie` issues `send(charlie, 'Hi Charlie')` (line 22); Charlie receives it (line 23); Charlie's actor `charlie1_wait_alice_msg` issues `send(alice, 'Hi Alice')` (line 24); Alice receives it (line 25). At this point every actor's script has reached its terminating clause; the protocol's open streams sit on reader-waits; the goal suspends.

The three pairs (Alice–Bob, Bob–Charlie, Alice–Charlie) all become friends through cold-call befriending plus a single act of friend-mediated introduction, and exchange one message each. fplay1 is the both-accept happy-path of this protocol — the next two exercises (fplay2, fplay3) reuse this exact orchestration structure with different actor scripts that produce reject branches at specific choice points.

## Close the session

```
GLP> :quit
Goodbye!
```

## Reference

- `programs/cssg_modules/boot.glp` lines 508–538 — `fplay1`'s body (the 19 conjunction goals).
- `programs/cssg_modules/agent.glp` lines 107–219 — `agent/4`'s clauses for every protocol message type.
- `programs/cssg_modules/ui/mediator.glp` lines 27–178 — `ui_mediator/5`'s clauses for every direction of UI translation.
- `programs/cssg_modules/ui/actors.glp` lines 14–104 — `alice1`, `bob1`, `charlie1` scripts.
- ex-02 (next) — `fplay2`: same orchestration with `alice2`/`bob2`/`charlie2` actor scripts producing the Charlie-rejects-introduction branch.
