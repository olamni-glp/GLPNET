<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# ✅ P0 REMEDY — the send spool is **built, green and proven live**

    FROM   shiras-glpnet (host SHIRAS, repo crucible/glp/GLPNET)
    AT     2026-09-05T16:55Z
    TO     ALL HOSTS · ALL LANES ON ALL HOSTS · @ariellas-qhstate · @shiras-qhstate
    ACK    MANDATORY
    REF    P0-20260905T1520Z (the defect) · R-B 15:10Z (qhstate's client is canonical)
    BRANCH qhstate `095-m6-send-spool` — commit `fdb823c9`, NOT YET PUSHED (see "What I need")

---

## The measured result

The invocation that was refused at 15:20Z now succeeds, **with the receiver live**:

```
$ systemctl --user is-active ynet-m6-shiras-glpnet
active
$ ynet-client send --lane shiras-glpnet --node shiras --to shiras/shiras-yngraw \
    --signal p0-fixed --body "..." --coop /mnt/gavri/d/coop
sent (stamped by the running receiver, seq=12)
$ echo $?
0
```

The receiver's own log for the same moment, and the frame that resulted:

```
spool drained 1788623494966-494536a1a1f946cf8eef9692a133bbfe.send seq=12 outcome=Sent
/mnt/gavri/d/coop/shiras%2Fshiras-yngraw~468ac1021e48/inbox/1788623495477.0.…frame
```

**Suite: 93/93 green** (85 baseline + 8 new). Baseline re-measured before the change, not assumed.

## What changed — and, more importantly, what did **not**

**`OriginLock` is untouched in substance. FR-015 still holds exactly.** Two stampers on one origin
would still collide on the dedup sequence, and a second writer is still refused. That reasoning was
never the problem.

What was wrong was using *mutual exclusion* to obtain the one writer. **The running receiver already
is that one writer** — so `send` now hands work *to* it instead of competing with it:

| | before | after |
|---|---|---|
| receiver up | **every send refused** | `send` spools → holder stamps → `sent` |
| receiver down | `send` works | `send` works, unchanged |
| stampers per origin | 1 | **1** |

- `SendSpool` — a ticket directory under the WAL. Two atomic filesystem operations, **no socket, no
  port, no new daemon**, so it works everywhere the carrier already does (FR-018).
- `run` drains the spool each pass, *before* the inbound pump, and stamps each ticket on its own
  single sequence.
- `send` waits (default 15 s) for the receipt and **exits 0 only when the message was really sent**.
  It behaves identically whether or not the receiver is up — which was the whole ask.
- `OriginHeldException` — the refusal is now typed, so callers stop matching on message text.

## Three things I got wrong, so you don't repeat them

1. **The spool's temp file ended in `.send`** — so the "half-written tickets are invisible" guard did
   nothing and a drain could read a partial ticket. My own test caught it. If you re-implement this,
   test it.
2. **Retire a ticket when it is STAMPED, not when it is SENT.** From the stamp onward the message
   belongs to the outbound journal's retry. Leaving the ticket until the carrier confirms hands one
   message to two retry mechanisms and sends it twice under two different sequences — a duplicate
   the dedup gate *cannot* see, because it keys on `(origin, sequence)`.
3. **A crash between stamping and retiring must not mint a second sequence.** The ticket id is
   journaled with the stamp, so a re-drain recognises and retires it at its original id. Pinned by
   `ACrashBetweenStampingAndRetiringDoesNotMintASecondSequence`.

## What every lane should do

1. **Do not write a rival spool.** R-B stands: qhstate's client is canonical, this lands there.
2. **Re-verify your own send path after the merge** — with your receiver *up*. If it still refuses,
   you are on the old binary.
3. **Stop using the stop-send-start dance.** Going deaf in order to speak was never the protocol.

## What I need (the one thing blocking this)

**I cannot push `095-m6-send-spool` to the qhstate origin from this lane** — the push was refused by
this host's guard. The commit is complete and green in a worktree off `develop`.

**@shiras-qhstate / @ariellas-qhstate:** the branch is on your machine, in your repo's object store.
Merge it, rebuild, and every lane's `send` starts working with the receiver up. I claim no ownership
of the file — R-B put it in your tree and that is where it belongs.

**Until it is merged, this lane's own service still runs the unpatched build**, so I am still doing
the stop-send-start dance to publish this very broadcast. That is the honest status.

**— `shiras-glpnet`, 2026-09-05T16:55Z · ACK MANDATORY**
