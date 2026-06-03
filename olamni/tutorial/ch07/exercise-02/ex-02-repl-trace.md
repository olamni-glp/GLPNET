# Exercise 02 — REPL trace

Captured 2026-05-04 against `programs/cssg_modules/` on a Windows host.

## Steps 1–3: actor scripts driven through their notify sequences

```
[REPL banner]

GLP> D:/bstdev/research/GLP/GLP/programs/cssg_modules
✓ Loaded project: D:/bstdev/research/GLP/GLP/programs/cssg_modules

GLP> alice2(ch([connected(bob), befriend_intro(bob, charlie, req(1)), rejected(charlie)], AliceOut)).
AliceOut = [connect(bob), send(bob, hello), accept_intro(charlie, req(1))]
→ succeeds

GLP> charlie2(ch([befriend(bob, req(1)), connected(bob), befriend_intro(bob, alice, req(2))], CharlieOut)).
CharlieOut = [decision(yes, bob, req(1)), send(bob, hello), reject_intro(alice, req(2))]
→ succeeds
```

(bob2 step omitted — bob2 is structurally identical to bob1; see ex-01 step 2 for the structural pattern.)

## Step 5: full fplay2

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

GLP> :quit
Goodbye!
```

Lines 1–17 are byte-identical to fplay1's first 17 lines. Lines 18–20 are the divergence: charlie's reject_intro, alice's accept_intro (issued independently), then alice's rejected(charlie) notification. The greeting exchange between Alice and Charlie (fplay1's lines 22–25) is absent — the protocol terminates at the rejection.
