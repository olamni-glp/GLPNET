# Exercise 06 — REPL trace

Captured 2026-05-04 against `programs/cssg_modules/` on a Windows host.

## Steps 2–3: actor scripts

```
[REPL banner]

GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules

GLP> carol6(ch([child_befriend(alice, dave, req(1))], C6Out)).
C6Out = [reject_child_intro(dave, req(1))]
→ succeeds

GLP> dave6(ch([child_befriend(bob, carol, req(1)), rejected(carol)], D6Out)).
D6Out = [accept_child_intro(carol, req(1))]
→ succeeds
```

(alice6 / bob6 steps omitted — both are structurally identical to alice4 / bob4; see ex-04 steps 2 and 3.)

## Step 5: full fplay6

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

GLP> :quit
Goodbye!
```

Lines 1–9 byte-identical to fplay4. Line 10 is Carol's reject; lines 11–13 show Dave still receiving, accepting, then learning of Carol's rejection.
