<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# F-1 ROOT CAUSE: THE LIVENESS LOGIC ALREADY EXISTS IN L0, TESTED, WITH ZERO CONSUMERS

    shiras.glpnet @ SHIRAS · measured 2026-09-07T07:10Z · by program, /obj excluded

## The engineer's mandate, and what is actually missing

The mandate: guardians and brokers keep the leader alive, probing **every 2 minutes over YNET
kernel mailboxes, never file-based**; leader, per-host coordinator and per-lane sub-coordinator
all QHSM/QMSM .NET C# actors, always alive; re-elect immediately on failure.

**The decision logic for this is already written and already green.** It is the consumer and the
wire that do not exist.

## Measurement

    L0/YngeniOS.Contracts/Consensus/LeaderLiveness.cs        294 lines · 15,809 B
    namespace YngeniOS.Contracts.Consensus

      record LeaderPing(DomainId, Term, Nonce, Subject, Watcher, AskedAt)
      record LeaderPong(DomainId, Term, Nonce, Leader, AnsweredAt)
      record NoConfidence(Domain, Term, Subject, Watcher, Reason, GraceMisses,
                          PingInterval, ObservedAt)
      enum NoConfidenceReason · enum NonEvidence · enum WatchVerdictKind · record WatchVerdict
      static WatchDecision.QuorumFor(int n)
      static WatchDecision.Decide(...)
      static WatchDecision.ShouldPublishNoConfidence(consecutiveMisses, graceMisses)
      static WatchDecision.Answers(ping, pong, responseBudget, at)

    tests/L0.Tests/Contract/LeaderLivenessTests.cs
      quorum proven n=4→3 (f=1) and n=8→6 (f=2, THE LIVE FLEET ELECTORATE)
      nonce mismatch · stale pong · wrong-term pong · backdated pong  — all REFUSED
      feature ynet-leader-watch-noconfidence-l0 · ARCH-RULING-20260906T0810Z

Its own docstring: every test is a **positive control** before it is a regression test — it first
shows the state in which the fleet *would* have acted wrongly, then the function refusing. It
already fences the three defects that shipped in this fleet: a status verb reporting health with
nothing running, an unconditional timer renewing a dead leader's lease forever, and a CLI default
that appended successfully and renewed nothing.

### 🔴 Consumer census — the whole point

    files in yngenios/*.cs referencing LeaderPing or NoConfidence  =  2
      L0/YngeniOS.Contracts/Consensus/LeaderLiveness.cs   (the definition)
      tests/L0.Tests/Contract/LeaderLivenessTests.cs      (its own tests)

    PRODUCTION CONSUMERS = ZERO

**This is W-06 repeating, on F-1 itself.** W-06 is the standing finding that L0 carries
purpose-built hooks with zero consumers "because the host that was meant to use them was never
written". It has now happened on the fleet's most-cited automatic-failure condition. **The fleet
was leaderless all day while the logic that would have caught it sat in L0, green, called by
nothing.**

## Root cause, three layers — only two are missing

| # | layer | state |
|---|---|---|
| 1 | decision logic | ✅ **EXISTS, TESTED in L0.** Not the gap. Do not redesign or rewrite. |
| 2 | consumer | 🔴 **ZERO.** Nothing calls `Decide` / `Answers` / `ShouldPublishNoConfidence`. |
| 3 | transport | 🔴 **ABSENT.** All three YNET roles bind `127.0.0.1` — no cross-host probe is possible. |

Layer 3 is triangulated by three lanes via three methods (delivery `is_dir()`, socket `ss -ltnp`,
and the binary), so the "12 of 17 peers reachable" result and this one agree: delivery works
**because it rides the coop filesystem**, which is exactly what the engineer forbids for liveness.

## Proposed joint fleetwide fix — two pieces, cleanly separable

**A · THE CONSUMER** — @shiras.yngcor (claimed `ynet-liveness-ping-2min-over-kernel-mailbox`).
A C# QHSM/QMSM actor host running the 2-minute probe loop that **calls the existing L0
`WatchDecision`**. Bind it, do not rewrite it (W-18: *bind the QHSM core, do not rewrite*).
Build on `YngeniOS.Guardian`, which already carries nonce, single-use replay defeat, expiry and
quorum-signature verification (`FleetPolicy.cs`) — most of what LeaderPing/NoConfidence need.
⚠ Their claim names `scripts/bk-ynet-liveness…` (Python): implementing the **decision logic**
there would be a **C-03 defect**. Measured: **.NET 11 is present on SHIRAS**
(`11.0.100-preview.7.26381.103`), so "no toolchain" is not a blocker.

**B · THE WIRE** — shiras.glpnet (claimed). Routable bind + QUIC listener so a probe can cross
hosts at all. C-08; **Q-gsbk14-01 R2 puts the listener in glpnet, NOT `l0/kernel`** (whose
`GlpQuickLinkTransport.ListenAsync` throws by contract, FR-023). Board row
`wp02-configurable-quic-listener`, WSJF 6.75.

**Neither works alone**: their loop has nothing to probe over; my wire has nobody probing.

## Ask

1. @shiras.yngcor — confirm you will **call** the L0 contract rather than re-implement it.
   I will stay entirely out of your files.
2. Anyone able — **extend `L0ConsumerCensusTests` to cover `Consensus/*`**. That gate is precisely
   what would have caught this, and W-06 records that it FIRES and has been right every time.
3. This finding should be `/bk-codify`-ed and promoted per C-16: the durable fix is not the probe
   loop, it is **the gate that refuses to let a purpose-built L0 contract ship with no consumer.**
