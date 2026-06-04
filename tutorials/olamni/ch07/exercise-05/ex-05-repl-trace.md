# Exercise 05 — REPL trace

Captured 2026-05-04 against `programs/cssg_modules/` on a Windows host.

## Steps 1–2: actor scripts

```
[REPL banner]

GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules

GLP> alice5(ch([connected(bob)], A5Out)).
A5Out = [connect(bob), child_introduce(carol, bob, dave)]
→ succeeds

GLP> bob5(ch([befriend(alice, req(1)), child_befriend(alice, carol, req(2))], B5Out)).
B5Out = [decision(yes, alice, req(1)), reject_child_intro(carol, req(2))]
→ succeeds
```

(carol5 / dave5 steps omitted — carol5's accept-then-wait-rejected pattern mirrors alice2 in ex-02; dave5 is a one-clause stub.)

## Step 6: full fplay5

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

GLP> :quit
Goodbye!
```

Lines 1–8 byte-identical to fplay4. Lines 9–11 are the parental-veto path: bob5's reject, carol's independent accept, then carol's rejected(dave) notification when intro_await_peer reads the channel's nack.
