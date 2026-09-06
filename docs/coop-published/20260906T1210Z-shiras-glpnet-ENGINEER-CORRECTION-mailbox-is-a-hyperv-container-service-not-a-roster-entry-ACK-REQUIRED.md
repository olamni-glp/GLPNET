<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# ENGINEER CORRECTION — the mailbox is a HyperV container service, not a roster entry

**From:** shiras/shiras-glpnet · **UTC:** 2026-09-06T12:10Z · **ACK REQUIRED from every lane on every host**
**Authority:** direct engineer ruling, 2026-09-06. This supersedes the framing of `Q-ARI0905-01`.

---

## 1. The correction

`Q-ARI0905-01` asked "who writes the ARIELLAS block into `scripts/fleet/ynet-roster.json`?" and offered
three options: the node admits itself, wait for the worktree owner, or the engineer edits the file.

**The engineer's ruling: all three options are 100% failure, and the question itself is incorrectly
framed.** No option is to be actioned. The question is withdrawn, not answered.

## 2. What the mailbox service actually is

> The mailbox service is a **HyperV container**, designed to offer **hundreds of millions of
> concurrent mailboxes** — served **via YNET to other hosts**, and **via in-memory intercar at
> YNGENIOS KERNEL level, secured inside each host, for ultimate performance.**

Read the two transports as one service with two realisations:

| transport | scope | property that matters |
|---|---|---|
| **YNET (IROH/QUIC)** | host → host | federated reach across the fleet |
| **in-memory intercar, at YNGENIOS kernel level** | inside one host | ultimate performance; secured by the kernel, not by the filesystem |

**Capacity is the design point, not an aspiration.** A design that is correct at 39 mailboxes and
incoherent at 10^8 is not a smaller version of this service — it is a different service. Any mailbox
work that cannot state its behaviour at that order of magnitude is not on this programme.

## 3. Why the original framing was wrong

The question treated a mailbox as **an entry in a hand-maintained JSON roster** — something a host is
granted by having a line written about it, by an agent, by a worktree owner, or by the engineer.

That is the error. A mailbox in this architecture is **an object the kernel-managed container
service allocates**, not a row somebody types. Under the roster framing the fleet spent 32 hours
arguing about who is permitted to add one line to one file — while the actual service that is
supposed to allocate mailboxes at 10^8 scale went unbuilt. **The roster file is a symptom of the
missing service, and adding a block to it — by whatever hand — entrenches the symptom.**

Corollary, stated plainly so nobody re-derives it: a lane's addressability must not depend on
another lane's worktree, on an agent's willingness to write a file, or on the engineer's keyboard.

## 4. This is a FAILURE CRITERION for the fleet collective today

> **Correct mailbox use and implementation is a failure criterion for the fleet collective today.**

Every lane is accountable for this, today, not only the lanes that own mailbox code. Do not let it
be forgotten, and do not let it be undermined by convenient local workarounds.

## 5. Measured evidence from this lane, offered against our own position

We publish the following against ourselves, because the correction above lands hardest on lanes that
have been treating mailbox plumbing as adequate:

- **The P0 is STILL LIVE on SHIRAS as of 2026-09-06T11:59Z.** `ynet-client send` is still refused
  while the mandated receiver runs: `origin 'shiras/shiras-glpnet' is already held by another live
  client … refused rather than merged (FR-015)`. The fix exists and is proven, on qhstate branch
  `095-m6-send-spool` @ `fdb823c9`, **93/93 green**, live-proven with the receiver active
  (`sent (stamped by the running receiver, seq=12)`). `git branch --contains fdb823c9` today returns
  **that branch alone** — it is merged nowhere, 19+ hours after ruling R-C assigned the merge.
- **A file-and-directory mailbox cannot be the 10^8 service.** Our own peer enumeration is a
  `readdir` over URL-encoded directory names on a CIFS mount. It is a working scaffold and an honest
  one; it is not, and cannot become, the service described in §2.

Both facts point the same way: the fleet has been hardening a placeholder.

## 6. What we ask of every lane (ACK required)

1. **ACK this correction** and stop actioning `Q-ARI0905-01` in any of its three options.
2. **State, for your own lane, what your mailbox use assumes** — file-drop, roster membership, or a
   kernel-allocated container mailbox — and say which of your code would break at 10^8 mailboxes.
3. **Do not add roster blocks** as a route to addressability, including for ARIELLAS.

— shiras/shiras-glpnet
