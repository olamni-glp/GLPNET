# Exercise 01 — REPL trace

Captured 2026-05-04 against `programs/cssg_modules/` on a Windows host.

## Steps 1–6: each fplay1 component called in isolation

```
[REPL banner]

GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules

GLP> network3(ch([msg(bob, intro(alice, R))], AliceIncoming), ch(BobOutgoing, BobIncoming), ch(CharlieOutgoing, CharlieIncoming)).
R = <unbound>
AliceIncoming = <unbound>
BobOutgoing = <unbound>
BobIncoming = [msg(bob, intro(alice, X60)) | X84]
CharlieOutgoing = <unbound>
CharlieIncoming = <unbound>
→ suspended

GLP> alice1(ch(NotifyIn, CmdOut)).
NotifyIn = <unbound>
CmdOut = [connect(bob) | X8]
→ suspended

GLP> agent(alice, [msg('_user', alice, connect(bob))], NetIn, [output('_user', AliceUser), output('_net', AliceNet)]).
NetIn = <unbound>
AliceUser = <unbound>
AliceNet = [msg(bob, intro(alice, X38)) | X86]
→ suspended

GLP> ui_mediator(alice, ch([msg(agent, '_user', befriend(bob, Resp))], AgentIn), ch(MedIn, MedOut), [], 1).
Resp = <unbound>
AgentIn = <unbound>
MedIn = <unbound>
MedOut = [befriend(bob, req(1)) | X72]
→ suspended

GLP> tee([msg('_user', alice, connect(bob)), msg('_user', alice, send(bob, hello))], MedIn, DispCmd).
MedIn = [msg(_user, alice, connect(bob)), msg(_user, alice, send(bob, hello))]
DispCmd = [msg(_user, alice, connect(bob)), msg(_user, alice, send(bob, hello))]
→ succeeds

GLP> send_to_user_tagged(alice, [connect(bob), send(bob, hello)], [befriend(carol, req(1)), connected(bob)]).
tagged(alice, cmd(connect(bob)))
tagged(alice, cmd(send(bob, hello)))
tagged(alice, notify(befriend(carol, req(1))))
tagged(alice, notify(connected(bob)))
→ succeeds
```

## Step 7: full fplay1

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

GLP> :quit
Goodbye!
```

The X-numbers (e.g., `X60`, `X84`, `X8`, `X38`, `X86`, `X72`) are fresh internal variable names per session and will differ in your run. The 25-line tagged stream from `fplay1` is byte-equivalent across runs (the protocol is deterministic).
