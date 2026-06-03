# Exercise 04 — REPL trace

Captured 2026-05-04 against `programs/cssg_modules/` on a Windows host.

## Steps 1–5: components and actors driven through their notify sequences

```
[REPL banner]

GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules

GLP> network2(ch([msg(bob, intro(alice, R))], AIncoming), ch(BOutgoing, BIncoming)).
R = <unbound>
AIncoming = <unbound>
BOutgoing = <unbound>
BIncoming = [msg(bob, intro(alice, X10)) | X26]
→ suspended

GLP> alice4(ch([connected(bob)], A4Out)).
A4Out = [connect(bob), child_introduce(carol, bob, dave)]
→ succeeds

GLP> bob4(ch([befriend(alice, req(1)), child_befriend(alice, carol, req(2))], B4Out)).
B4Out = [decision(yes, alice, req(1)), approve_child_intro(carol, dave, req(2))]
→ succeeds

GLP> carol4(ch([child_befriend(alice, dave, req(1)), connected(dave), received(dave, 'Hi Carol')], C4Out)).
C4Out = [accept_child_intro(dave, req(1)), send(dave, Hi Dave)]
→ succeeds

GLP> dave4(ch([child_befriend(bob, carol, req(1)), connected(carol), received(carol, 'Hi Dave')], D4Out)).
D4Out = [accept_child_intro(carol, req(1)), send(carol, Hi Carol)]
→ succeeds
```

## Step 7: full fplay4

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

GLP> :quit
Goodbye!
```

The CSSG protocol terminates after the greeting exchange. The X-numbers (e.g., `X10`, `X26`) are fresh internal variable names per session.
