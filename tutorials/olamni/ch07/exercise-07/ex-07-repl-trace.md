# Exercise 07 — REPL trace

Captured 2026-05-04 against `programs/cssg_modules/` on a Windows host.

## Steps 2–3: actor scripts

```
[REPL banner]

GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules

GLP> carol7(ch([child_befriend(alice, dave, req(1)), rejected(dave)], C7Out)).
C7Out = [accept_child_intro(dave, req(1))]
→ succeeds

GLP> dave7(ch([child_befriend(bob, carol, req(1))], D7Out)).
D7Out = [reject_child_intro(carol, req(1))]
→ succeeds
```

(alice7 / bob7 steps omitted — both are structurally identical to alice4 / bob4; see ex-04 steps 2 and 3.)

## Step 5: full fplay7

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

GLP> :quit
Goodbye!
```

Lines 1–11 byte-identical to fplay4. Lines 12–13 are Dave's reject and Carol's notification — the symmetric mirror of fplay6.
